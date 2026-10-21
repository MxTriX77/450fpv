"""The hole-filler contract at build time (p1-e01-trench, "Fillers under the hole contract"; the first attempt's
terrain-holes-review §3, F-1...F-8; the earthworks sheet's shared rules), for an asset whose metadata carries `holes`.
Plain Python, no Blender. The collision audit (lib.audit) already holds every primitive and every LOD0 face to each
other within 5 cm, 2 cm near the gaps and hard landing faces (F-6); this checks the rest against the records, with one
line per rule:

- F-1 sealed below: on a SAMPLE_M grid over every band and RAY_MARGIN_M past its edge (every 0.5 m cell a band can
  mark, by its centre or by touch, reaches at most 0.71 m past it), a vertical line meets a filler.
- F-2 the cavity inside the bands: every face of the cut's slots (the LOD0 faces whose slot's physics is a filler
  material), sampled every SAMPLE_M with its vertices, lies at least BAND_MARGIN_M inside the union of the bands
  (0.25 m inside the hole cells at any yaw).
- F-3 level lips: along every band's edge, SAMPLE_M apart and EDGE_INSET_M inside it (where no other band covers it),
  the highest filler point lies within LIP_TOLERANCE_M of the ground, y = 0.
- F-4 nothing proud: no filler rises above PROUD_M.
- F-5 thick: every filler box at least FILLER_M across in every dimension (each one carries an open face or backs
  one); and every joint overlapped by at least JOINT_OVERLAP_M, this module's own figure, never the generator's
  (p1-e01-trench review S3), one line per joint: a joint is an interior point of a record's centre line, and the
  generator must report each (how far the boxes along the run reach past its plane), or the build fails (S13).
- F-7 surfaces: every filler is a cut's surface and not landable; the other shapes are listed by surface.
- F-8 no wind obstacle: no wind volume.

A filler is a shape whose material is one of FILLER_MATERIALS (the cut's walls, floor, lips and backing, F-7)."""
import math

FILLER_MATERIALS = ("surface:belt_bare",)  # F-7: cut walls and floors are firm loam (review-materials.md §6)
SAMPLE_M = 0.05           # F-1's ray grid, F-2's and F-3's sampling (terrain-holes-review §3: a 5 cm grid)
RAY_MARGIN_M = 0.71       # settled (p1-e01-trench design, open question): a cell marked by touch reaches 0.707 m past
BAND_MARGIN_M = 0.65      # settled: 0.25 m plus half a 0.5 m cell's diagonal, rounded up
LIP_TOLERANCE_M = 0.03    # F-3
PROUD_M = 0.01            # F-4
FILLER_M = 0.3            # F-5
JOINT_OVERLAP_M = 0.2     # F-5: neighbouring boxes overlap this across every joint (terrain-holes-review §3, carried)
EDGE_INSET_M = 0.01       # F-3 samples the band this far inside its edge
BUCKET_M = 0.5            # the fillers' plan index
TOLERANCE = 1e-6          # rounding: the metadata keeps micrometres


def audit(meta, lod0, joints=None):
    """(problems, lines) of the hole contract for `meta` (lib.meta) with its LOD0 mesh and the generator's `joints`
    ([{"joint", "chainage_m", "turn_deg", "before_m", "after_m", "overlap_m"}], or None when it reports none: a
    problem when the holes' centre lines have joints); nothing when it has no holes."""
    records = meta["holes"]
    if not records:
        return [], []
    shapes = meta["collision"]
    fillers = [s for s in shapes if s["material"] in FILLER_MATERIALS]
    index = _PlanIndex([_Box(s) for s in fillers if s["shape"] == "box"])
    lines, problems = [], []
    bands = [_Band(r) for r in records]

    # F-1
    lo, hi = _bounds(bands, RAY_MARGIN_M)
    rays = misses = 0
    first = None
    for x in _steps(lo[0], hi[0]):
        for z in _steps(lo[1], hi[1]):
            if min(b.outside(x, z) for b in bands) > RAY_MARGIN_M:
                continue
            rays += 1
            if index.top(x, z) is None:
                misses += 1
                first = first or (x, z)
    text = (f"F-1 sealed below: {rays} vertical rays on a {SAMPLE_M * 100:g} cm grid over the bands and "
            f"{RAY_MARGIN_M} m past their edges, {misses} miss every filler")
    if misses:
        problems.append(text + f", the first at ({first[0]:.3f}, {first[1]:.3f})")
    lines.append(text)

    # F-2
    slots = {m["slot"] for m in meta["materials"] if m["physics"] in FILLER_MATERIALS}
    worst, at, count = math.inf, None, 0
    for indices, _, _, slot in lod0.faces:
        if slot not in slots:
            continue
        points = [lod0.vertices[i] for i in indices]
        for k in range(1, len(points) - 1):
            for p in _triangle_samples(points[0], points[k], points[k + 1]):
                count += 1
                margin = -min(b.outside(p[0], p[2]) for b in bands)
                if margin < worst:
                    worst, at = margin, p
    text = (f"F-2 the cavity inside the bands: {count} samples of the cut's faces (slots {', '.join(sorted(slots))}), "
            f"the least {worst:.4f} m inside the bands at {_fmt(at)}, at least {BAND_MARGIN_M} m")
    if worst < BAND_MARGIN_M - TOLERANCE:
        problems.append(text)
    lines.append(text)

    # F-3
    samples, worst, at = 0, 0.0, None
    for k, band in enumerate(bands):
        for x, z in band.edge(EDGE_INSET_M):
            if any(other.outside(x, z) < -EDGE_INSET_M - TOLERANCE for j, other in enumerate(bands) if j != k):
                continue
            samples += 1
            top = index.top(x, z)
            off = math.inf if top is None else abs(top)
            if off > worst:
                worst, at = off, (x, z)
    text = (f"F-3 level lips: {samples} samples along the bands' edges, the fillers' top within {worst * 1000:.1f} mm "
            f"of the ground" + (f" (worst at ({at[0]:.3f}, {at[1]:.3f}))" if at else "") +
            f", tolerance {LIP_TOLERANCE_M * 1000:g} mm")
    if worst > LIP_TOLERANCE_M:
        problems.append(text)
    lines.append(text)

    # F-4
    highest = max((max(c[1] for c in _corners(s)) if s["shape"] == "box" else s["center"][1] + s.get("radius", 0.0)
                   for s in fillers), default=-math.inf)
    text = f"F-4 nothing proud: the highest filler point at y = {highest:.4f} m, at most {PROUD_M} m"
    if highest > PROUD_M:
        problems.append(text)
    lines.append(text)

    # F-5
    thinnest = min((min(s["size"]) for s in fillers if s["shape"] == "box"), default=math.inf)
    others = [s for s in fillers if s["shape"] != "box"]
    text = (f"F-5 thick: {len(fillers)} fillers, every box at least {thinnest:.3f} m across, at least {FILLER_M} m"
            + (f"; {len(others)} not boxes" if others else ""))
    if thinnest < FILLER_M - TOLERANCE or others:
        problems.append(text)
    lines.append(text)
    problems += _joints(records, joints, lines)

    # F-7
    landable = sum(1 for s in fillers if s["landable"])
    kinds = {}
    for s in shapes:
        if s["material"] not in FILLER_MATERIALS:
            entry = kinds.setdefault(s["material"], [0, 0])
            entry[0] += 1
            entry[1] += s["landable"]
    text = (f"F-7 surfaces: {len(fillers)} fillers {'/'.join(FILLER_MATERIALS)}, {landable} landable; "
            + "; ".join(f"{n} {m}, {k} landable" for m, (n, k) in sorted(kinds.items())))
    if landable:
        problems.append(text)
    lines.append(text)

    # F-8
    text = f"F-8 no wind obstacle: {len(meta['wind_volumes'])} wind volumes"
    if meta["wind_volumes"]:
        problems.append(text)
    lines.append(text)
    lines.append("F-6 faces within 5 cm of the visual (2 cm near the gaps and hard landing faces): the collision audit's "
                 "lines above")
    return problems, ["holes " + line for line in lines]


def _joints(records, joints, lines):
    """F-5's overlap: a line per joint and a summary; a problem when the generator reports fewer or more joints than
    the records' centre lines have interior points (none reported included), or when any joint overlaps less than
    JOINT_OVERLAP_M."""
    expected = sum(max(0, len(r["centre_line"]) - 2) for r in records)
    joints = joints or []
    problems = []
    if len(joints) != expected:
        problems.append(f"F-5 overlap: the generator reports {len(joints)} joints, the holes' centre lines have "
                        f"{expected}")
    for j in joints:
        lines.append(f"F-5 joint {j['joint']} at {j['chainage_m']:.2f} m (turn {j['turn_deg']:+.2f} deg): the boxes "
                     f"reach {j['before_m']:.3f} m and {j['after_m']:.3f} m past it, overlap {j['overlap_m']:.3f} m")
    overlaps = [j["overlap_m"] for j in joints]
    text = (f"F-5 overlap: {len(joints)} joints, the boxes along the run overlap "
            + (f"{min(overlaps):.3f}-{max(overlaps):.3f} m" if overlaps else "nothing") + f", at least {JOINT_OVERLAP_M} m")
    lines.append(text)
    short = [j for j in joints if j["overlap_m"] < JOINT_OVERLAP_M - TOLERANCE]
    if short:
        problems.append(text + f"; {len(short)} short, the first joint {short[0]['joint']} at "
                               f"{short[0]['chainage_m']:.2f} m overlapping {short[0]['overlap_m']:.3f} m")
    return problems


class _Band:
    """A record's band in plan: every point within band_width_m / 2 of its centre line."""

    def __init__(self, record):
        self.points = [(p[0], p[2]) for p in record["centre_line"]]
        self.half = record["band_width_m"] / 2

    def outside(self, x, z):
        """How far (x, z) lies outside the band (negative inside)."""
        return min(_segment_distance((x, z), a, b) for a, b in zip(self.points, self.points[1:])) - self.half

    def edge(self, inset):
        """Points SAMPLE_M apart on the band's edge, `inset` inside it: both offset lines of every segment and a
        half circle round each end."""
        r = self.half - inset
        out = []
        for a, b in zip(self.points, self.points[1:]):
            length = math.hypot(b[0] - a[0], b[1] - a[1])
            t = ((b[0] - a[0]) / length, (b[1] - a[1]) / length)
            n = (-t[1], t[0])
            for k in range(max(1, math.ceil(length / SAMPLE_M)) + 1):
                s = length * k / max(1, math.ceil(length / SAMPLE_M))
                for side in (1, -1):
                    out.append((a[0] + t[0] * s + n[0] * side * r, a[1] + t[1] * s + n[1] * side * r))
        for end, far in ((self.points[0], self.points[1]), (self.points[-1], self.points[-2])):
            heading = math.atan2(end[1] - far[1], end[0] - far[0])
            steps = max(2, math.ceil(math.pi * r / SAMPLE_M))
            for k in range(steps + 1):
                a = heading - math.pi / 2 + math.pi * k / steps
                out.append((end[0] + r * math.cos(a), end[1] + r * math.sin(a)))
        return out


class _Box:
    """A box as its centre, rotation axes and half sizes, for a vertical line's span through it."""

    def __init__(self, shape):
        self.c = shape["center"]
        x, y, z, w = shape["rotation"]
        self.axes = [_rotate((x, y, z, w), e) for e in ((1, 0, 0), (0, 1, 0), (0, 0, 1))]
        self.half = [s / 2 for s in shape["size"]]
        corners = _corners(shape)
        self.lo = (min(c[0] for c in corners), min(c[2] for c in corners))
        self.hi = (max(c[0] for c in corners), max(c[2] for c in corners))

    def span(self, x, z):
        """The (lowest, highest) y of the vertical line through (x, z) inside the box, or None (the slab method)."""
        lo, hi = -math.inf, math.inf
        d = (x - self.c[0], -self.c[1], z - self.c[2])
        for axis, half in zip(self.axes, self.half):
            o = d[0] * axis[0] + d[1] * axis[1] + d[2] * axis[2]  # the line's origin (y = 0) along the axis
            v = axis[1]  # the line's direction (up) along the axis
            if abs(v) < 1e-12:
                if abs(o) > half + TOLERANCE:
                    return None
                continue
            t0, t1 = (-half - o) / v, (half - o) / v
            lo, hi = max(lo, min(t0, t1)), min(hi, max(t0, t1))
            if lo > hi + TOLERANCE:
                return None
        return lo, hi


class _PlanIndex:
    """Boxes bucketed by their plan bounds, for the fillers under a point."""

    def __init__(self, boxes):
        self.buckets = {}
        for box in boxes:
            for i in range(math.floor(box.lo[0] / BUCKET_M), math.floor(box.hi[0] / BUCKET_M) + 1):
                for j in range(math.floor(box.lo[1] / BUCKET_M), math.floor(box.hi[1] / BUCKET_M) + 1):
                    self.buckets.setdefault((i, j), []).append(box)

    def top(self, x, z):
        """The highest filler point on the vertical line through (x, z), or None when it meets none."""
        best = None
        for box in self.buckets.get((math.floor(x / BUCKET_M), math.floor(z / BUCKET_M)), ()):
            span = box.span(x, z)
            if span is not None and (best is None or span[1] > best):
                best = span[1]
        return best


def _bounds(bands, margin):
    xs = [p[0] for b in bands for p in b.points]
    zs = [p[1] for b in bands for p in b.points]
    reach = max(b.half for b in bands) + margin
    return (min(xs) - reach, min(zs) - reach), (max(xs) + reach, max(zs) + reach)


def _steps(a, b):
    n = math.ceil((b - a) / SAMPLE_M)
    return [a + k * SAMPLE_M for k in range(n + 1)]


def _triangle_samples(a, b, c):
    """The triangle's vertices and points on a grid of about SAMPLE_M over it."""
    n = max(1, math.ceil(max(_dist(a, b), _dist(b, c), _dist(c, a)) / SAMPLE_M))
    out = []
    for i in range(n + 1):
        for j in range(n + 1 - i):
            u, v = i / n, j / n
            w = 1 - u - v
            out.append(tuple(a[k] * w + b[k] * u + c[k] * v for k in range(3)))
    return out


def _corners(shape):
    x, y, z, w = shape["rotation"]
    c, h = shape["center"], [s / 2 for s in shape["size"]]
    out = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                r = _rotate((x, y, z, w), (sx * h[0], sy * h[1], sz * h[2]))
                out.append((c[0] + r[0], c[1] + r[1], c[2] + r[2]))
    return out


def _rotate(q, v):
    """v rotated by the unit quaternion [x, y, z, w] (Hamilton, as Unity and lib.audit apply it)."""
    x, y, z, w = q
    tx, ty, tz = 2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0])
    return (v[0] + w * tx + (y * tz - z * ty), v[1] + w * ty + (z * tx - x * tz), v[2] + w * tz + (x * ty - y * tx))


def _segment_distance(p, a, b):
    ab = (b[0] - a[0], b[1] - a[1])
    length2 = ab[0] * ab[0] + ab[1] * ab[1]
    t = 0.0 if length2 == 0 else min(1.0, max(0.0, ((p[0] - a[0]) * ab[0] + (p[1] - a[1]) * ab[1]) / length2))
    return math.hypot(p[0] - a[0] - t * ab[0], p[1] - a[1] - t * ab[1])


def _dist(a, b):
    return math.sqrt(sum((a[k] - b[k]) ** 2 for k in range(3)))


def _fmt(p):
    return "(" + ", ".join(f"{round(c, 4) + 0.0:g}" for c in p) + ")" if p else "()"
