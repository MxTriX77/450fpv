"""The bare soil patches of a ground patch: one seeded definition for the map and the mesh (p1-s04-belt-straw,
review-physics PR3-PR5, design 6 and 11). Plain Python, no Blender: tools/assets/derive.py draws the bare mask from
it and generators/ground_patch builds the stepped floors and the mat's boxes from it, from the same params, so the
map and the geometry never disagree.

Points are asset metres in the ground plane, (x, z), the origin at the patch centre; angles are degrees from +X
towards +Z (the recipes' lodging convention). Polygons run counter-clockwise in (x, z).

A patch is a convex step outline of `vertices` corners: corners round a circle, each pulled in by up to
RADIAL_JITTER of the radius, scaled to measure `length` along the patch's angle (the lodging azimuth within
ANGLE_SPREAD_DEG) and `width` across, with no exterior angle above MAX_EXTERIOR_DEG, which makes its edge boxes cover
its window (below). A patch is path-like (H 9-14: ragged strips of trodden soil, never round pits): its length over
its width is drawn from `aspect`, its length from size_range_m but at least that ratio times the range's minimum,
and its width is length / ratio, at least the range's minimum. Patches are drawn until their outlines cover `share`
of the area within SHARE_TOLERANCE; the bare share is measured on the outlines (PR5), and a patch that would
overshoot it is shrunk by SHRINK while it stays inside the size range. A patch drawn after n patches in a row found
no place is SHRINK^n as long (at least the range's minimum, so rounder too), so a crowded area (a share above the
sheet's 8 %) still fills its share with smaller patches. The map stamps soil full on the outline shrunk by
FEATHER_M and fading to nothing at the outline: it fills `core`, the outline shrunk by FEATHER_M / 2, and blurs it
over FEATHER_M / 2 each side, so the straws strewn over the feather lie on the floor.

The patches gather into paths rather than spread evenly. A path runs one way along its first patch, and the next
per_path - 1 patches are laid on along it, each turned by up to PATH_TURN_DEG from the last (staying within
ANGLE_SPREAD_DEG of the lodging), stepped sideways by up to PATH_SIDE of its width and set behind the shortest mat
bridge of PATH_GAP_M (tried every PATH_GAP_STEP_M) where it fits; blocked, the path runs on from its first patch
the other way once, and blocked again it ends and the patch starts a new path. With probability CLUSTER a new path
starts near the patches laid so far (its centre NEAR_M from one of theirs) for the first half of its tries, else
anywhere. The edge boxes keep about a metre of mat between two floors (PR4, below), so at the sheet's share the
patches still fill much of the area the boxes may reach and gather only so far.

The mat around a patch is a set of boxes (PR4). The window is the outline's bounding rectangle grown by WINDOW_M.
Per outline edge, one box (`edges`, a rectangle in the plane: t along the edge from its first corner, s outward)
has its inner face on the edge, reaches outward at least REACH_M and SEAM_M past the window's farthest corner, and
past both corners of the edge by that same reach; together they cover the window minus the outline (`uncovered`
checks it every SAMPLE_M). `fill` covers the rest of the area with axis-aligned rectangles overlapping by SEAM_M. A patch is placed only
where none of its edge boxes leaves the area or enters another outline, and none of theirs enters its outline.
"""
import math
import random

LAYOUT = "bare patches v2"  # bumped with any change to the code below; derive.py hashes it with the constants
FEATHER_M = 0.3            # a soil patch's strewn edge (H 9-14, by eye; derive.py's tuned value, moved here)
ANGLE_SPREAD_DEG = 45.0    # a patch lies within this of the lodging azimuth (assumed; derive.py's before)
RADIAL_JITTER = 0.35       # a corner's radius drawn in [1 - this, 1] of the ellipse's (assumed; 0.25 until task 1.7)
MAX_EXTERIOR_DEG = 90.0    # derived: the largest turn at a corner for which the edge boxes cover the window
WINDOW_M = 0.3             # F-5 behind an open face (carried), around the outline's bounding rectangle
REACH_M = 0.3              # F-5: the least an edge box reaches outward
SEAM_M = 0.05              # boxes overlap by this where they meet: an edge box reaches this past the window, a fill
                           # rectangle this past its neighbours (assumed; the collision audit can only prune a seam of
                           # the union down to the overlap's width, and abutting boxes have none)
SHARE_TOLERANCE = 0.005    # the outlines' share lands within this of `share`
SHRINK = 0.75              # the last patch shrinks by this until it fits the share
PLACE_TRIES = 1000         # placements tried per drawn patch before another is drawn
SHAPE_TRIES = 200          # drawn patches that found no place before the layout gives up
PATH_GAP_M = (0.3, 1.5)    # the mat bridge between two patches of a path, the shortest that fits (assumed, H 9-14)
PATH_GAP_STEP_M = 0.1      # the bridges tried, shortest first
PATH_TURN_DEG = 20.0       # a path turns by up to this from one patch to the next (assumed, H 9-14)
PATH_SIDE = 0.5            # a patch steps sideways off its path by up to this share of its width (assumed, H 14)
CLUSTER = 0.85             # a new path starts near the patches laid so far with this probability (assumed, H 9-14)
NEAR_M = (1.0, 2.2)        # near: its centre this far from a laid patch's centre, in the first half of its tries
SAMPLE_M = 0.02            # the coverage self-check's spacing (PR4)
EPS = 1e-9                 # the placement's tolerance on touching shapes
CONSTANTS = ("FEATHER_M", "ANGLE_SPREAD_DEG", "RADIAL_JITTER", "MAX_EXTERIOR_DEG", "WINDOW_M", "REACH_M", "SEAM_M",
             "SHARE_TOLERANCE", "SHRINK", "PLACE_TRIES", "SHAPE_TRIES", "PATH_GAP_M", "PATH_GAP_STEP_M",
             "PATH_TURN_DEG", "PATH_SIDE", "CLUSTER", "NEAR_M", "EPS")


def inputs(params):
    """The layout's inputs from a ground_patch params dict, as layout() takes them (and derive.py records them)."""
    bare = params["bare"]
    return {"seed": params["seed"], "size_m": params["size_m"], "share": bare["share"],
            "size_range_m": [bare["size_m"]["min"], bare["size_m"]["max"]], "vertices": bare["outline_vertices"],
            "azimuth_deg": params["lodging"]["azimuth_deg"], "aspect": [bare["aspect"]["min"], bare["aspect"]["max"]],
            "per_path": bare["path_patches"]}


def constants():
    """The current value of every constant the layout reads, for derive.py's stamp (tuples as lists, as JSON keeps
    them)."""
    return {"LAYOUT": LAYOUT, **{name: list(globals()[name]) if isinstance(globals()[name], tuple) else globals()[name]
                                 for name in CONSTANTS}}


def layout(seed, size_m, share, size_range_m, vertices, azimuth_deg, aspect, per_path):
    """{"patches": [...], "share": the outlines' area over size_m^2}. Each patch: "centre", "angle_deg", "outline",
    "core", "window" (x0, x1, z0, z1), "edges" (one rectangle per outline edge, edge k from corner k), "extent" (the
    edge boxes' bounding rectangle, x0, x1, z0, z1), "path" (its path's number, in the order the paths started) and
    "after" (the index of the patch it was laid on after, None for a path's first; derive.py thins the mat between
    the two). `aspect` is the (min, max) of a patch's length over its width, `per_path` the most patches one path
    holds. Raises ValueError when the share does not fit the area."""
    rng = random.Random(f"bare patches {seed}")
    half, area_m2 = size_m / 2, size_m * size_m
    target, tolerance = share * area_m2, SHARE_TOLERANCE * area_m2
    lo, hi = size_range_m
    patches, covered, failures, paths, streak = [], 0.0, 0, 0, 0
    path = None  # the path being laid: its last patch, the way it runs along that patch's angle (+-1), patches left
    while covered < target - tolerance:
        ratio = rng.uniform(*aspect)
        length = max(lo, rng.uniform(min(hi, ratio * lo), hi) * SHRINK ** streak)  # crowded: shorter, rounder
        width = max(lo, length / ratio)
        if path is None:
            angle = azimuth_deg + rng.uniform(-ANGLE_SPREAD_DEG, ANGLE_SPREAD_DEG)
        else:
            turned = path["last"]["angle_deg"] + rng.uniform(-PATH_TURN_DEG, PATH_TURN_DEG)
            angle = min(azimuth_deg + ANGLE_SPREAD_DEG, max(azimuth_deg - ANGLE_SPREAD_DEG, turned))
        shape = _outline(rng, vertices, length, width, angle)
        scale = 1.0
        while covered + area(shape) * scale * scale > target + tolerance and min(length, width) * scale * SHRINK >= lo:
            scale *= SHRINK
        shape = [(x * scale, z * scale) for x, z in shape]
        patch = None if path is None else _along(rng, path, shape, angle, patches, half)
        if patch is None and path is not None and not path["back"]:  # blocked: the path runs on from its other end
            path.update(last=path["first"], sign=-path["sign"], back=True)
            patch = _along(rng, path, shape, angle, patches, half)
        after = None if patch is None else next(k for k, p in enumerate(patches) if p is path["last"])
        if patch is None:
            near = bool(patches) and rng.random() < CLUSTER
            for k in range(PLACE_TRIES):
                if near and k < PLACE_TRIES // 2:  # a new path beside the patches laid so far: the paths gather
                    other, bearing, r = rng.choice(patches), rng.uniform(0, 2 * math.pi), rng.uniform(*NEAR_M)
                    centre = (other["centre"][0] + r * math.cos(bearing), other["centre"][1] + r * math.sin(bearing))
                else:
                    centre = (rng.uniform(-half, half), rng.uniform(-half, half))
                candidate = _patch(centre, angle, [(x + centre[0], z + centre[1]) for x, z in shape])
                if _fits(candidate, patches, half):
                    patch = candidate
                    break
            else:
                failures, streak = failures + 1, streak + 1
                if failures > SHAPE_TRIES:
                    raise ValueError(f"bare patches: {len(patches)} placed covering {covered / area_m2:.4f} of the "
                                     f"area, no room for more towards a share of {share} after {SHAPE_TRIES} patches")
                path = None
                continue
            path = {"id": paths, "first": patch, "sign": rng.choice((-1, 1)), "back": False,
                    "left": per_path}
            paths += 1
        patch.update(path=path["id"], after=after)
        patches.append(patch)
        streak = 0
        covered += area(patch["outline"])
        path["last"], path["left"] = patch, path["left"] - 1
        if path["left"] == 0:
            path = None
    return {"patches": patches, "share": covered / area_m2}


def fill(size_m, patches):
    """Axis-aligned rectangles (x0, x1, z0, z1) covering the area minus every window: a sweep along x over the
    windows' edges, the free z-intervals of consecutive strips merged when they are the same, then each grown by
    SEAM_M inside the area, so neighbours overlap (a window lies WINDOW_M from its outline, so a grown rectangle
    still keeps WINDOW_M - SEAM_M from every outline)."""
    half = size_m / 2
    windows = [p["window"] for p in patches]
    xs = sorted({-half, half} | {w[0] for w in windows} | {w[1] for w in windows})
    rects, running = [], {}
    for x0, x1 in zip(xs, xs[1:]):
        free, z = [], -half
        for z0, z1 in sorted((w[2], w[3]) for w in windows if w[0] < x1 and w[1] > x0):
            if z0 > z:
                free.append((z, z0))
            z = max(z, z1)
        if z < half:
            free.append((z, half))
        following = {}
        for interval in free:
            rect = running.get(interval)
            if rect is None:
                rect = [x0, x1, interval[0], interval[1]]
                rects.append(rect)
            rect[1] = x1
            following[interval] = rect
        running = following
    return [(max(-half, x0 - SEAM_M), min(half, x1 + SEAM_M), max(-half, z0 - SEAM_M), min(half, z1 + SEAM_M))
            for x0, x1, z0, z1 in rects]


def uncovered(patch, step=SAMPLE_M):
    """The PR4 self-check: the samples of the patch's window, every `step` or a little less, that lie outside its
    outline and in none of its edge boxes. Empty when the boxes open the mat exactly over the outline."""
    x0, x1, z0, z1 = patch["window"]
    nx, nz = math.ceil((x1 - x0) / step), math.ceil((z1 - z0) / step)
    misses = []
    for i in range(nx + 1):
        for j in range(nz + 1):
            p = (x0 + (x1 - x0) * i / nx, z0 + (z1 - z0) * j / nz)
            if distance(p, patch["outline"]) > 0 and not any(in_rect(p, r) for r in patch["edges"]):
                misses.append(p)
    return misses


def inside(patches, x, z):
    """Whether (x, z) lies inside (or on) one of the outlines."""
    return any(p["window"][0] <= x <= p["window"][1] and p["window"][2] <= z <= p["window"][3]
               and distance((x, z), p["outline"]) <= 0 for p in patches)


def distance(p, polygon):
    """Signed distance from p to a convex counter-clockwise polygon's boundary: negative inside."""
    depth = min(_plane(p, a, b) for a, b in _edges_of(polygon))
    if depth >= 0:  # inside or on: the nearest edge's plane
        return -depth
    return min(_segment_distance(p, a, b) for a, b in _edges_of(polygon))


def area(polygon):
    """The shoelace area, positive for a counter-clockwise polygon."""
    return sum(a[0] * b[1] - b[0] * a[1] for a, b in _edges_of(polygon)) / 2


def inset(polygon, d):
    """A convex polygon shrunk by d: clipped by every edge's half-plane moved d inward (empty when nothing is left)."""
    result = list(polygon)
    for a, b in _edges_of(polygon):
        if not result:
            break
        result = _clip(result, a, b, d)
    return result


def rect_corners(rect):
    """The four corners of an edge rectangle, counter-clockwise."""
    (ax, az), (dx, dz), (nx, nz) = rect["a"], rect["d"], rect["n"]
    (t0, t1), (s0, s1) = rect["t"], rect["s"]
    corners = [(ax + dx * t + nx * s, az + dz * t + nz * s) for t, s in ((t0, s0), (t0, s1), (t1, s1), (t1, s0))]
    return corners if area(corners) > 0 else corners[::-1]


def in_rect(p, rect):
    """Whether p lies in an edge rectangle (or on it)."""
    (ax, az), (dx, dz), (nx, nz) = rect["a"], rect["d"], rect["n"]
    t, s = (p[0] - ax) * dx + (p[1] - az) * dz, (p[0] - ax) * nx + (p[1] - az) * nz
    return rect["t"][0] - EPS <= t <= rect["t"][1] + EPS and rect["s"][0] - EPS <= s <= rect["s"][1] + EPS


# --- construction ---------------------------------------------------------------------------------------------

def _outline(rng, n, length, width, angle_deg):
    """A convex counter-clockwise outline about the origin: n corners on a circle at a random phase, each radius
    drawn in [1 - RADIAL_JITTER, 1], scaled to measure exactly `length` along angle_deg and `width` across, redrawn
    until no turn exceeds MAX_EXTERIOR_DEG."""
    c, s = math.cos(math.radians(angle_deg)), math.sin(math.radians(angle_deg))
    for _ in range(1000):
        phase = rng.uniform(0, 2 * math.pi / n)
        unit = []
        for k in range(n):
            t, r = phase + 2 * math.pi * k / n, rng.uniform(1 - RADIAL_JITTER, 1)
            unit.append((r * math.cos(t), r * math.sin(t)))
        us, vs = [u for u, _ in unit], [v for _, v in unit]
        su, sv = length / (max(us) - min(us)), width / (max(vs) - min(vs))
        mu, mv = (max(us) + min(us)) / 2, (max(vs) + min(vs)) / 2
        points = [((u - mu) * su * c - (v - mv) * sv * s, (u - mu) * su * s + (v - mv) * sv * c) for u, v in unit]
        if all(0 < turn <= math.radians(MAX_EXTERIOR_DEG) for turn in _turns(points)):
            return points
    raise ValueError(f"no convex outline of {n} corners within {MAX_EXTERIOR_DEG} degree turns in 1000 draws")


def _turns(polygon):
    """The exterior angle at each corner, radians, positive for a left (counter-clockwise) turn."""
    n = len(polygon)
    turns = []
    for k in range(n):
        a, b, c = polygon[k - 1], polygon[k], polygon[(k + 1) % n]
        d1, d2 = (b[0] - a[0], b[1] - a[1]), (c[0] - b[0], c[1] - b[1])
        turns.append(math.atan2(d1[0] * d2[1] - d1[1] * d2[0], d1[0] * d2[0] + d1[1] * d2[1]))
    return turns


def _patch(centre, angle, outline):
    xs, zs = [p[0] for p in outline], [p[1] for p in outline]
    window = (min(xs) - WINDOW_M, max(xs) + WINDOW_M, min(zs) - WINDOW_M, max(zs) + WINDOW_M)
    corners = [(window[i], window[j]) for i in (0, 1) for j in (2, 3)]
    edges = []
    for a, b in _edges_of(outline):
        length = math.hypot(b[0] - a[0], b[1] - a[1])
        d = ((b[0] - a[0]) / length, (b[1] - a[1]) / length)
        n = (d[1], -d[0])  # outward of a counter-clockwise polygon
        reach = max([REACH_M] + [(c[0] - a[0]) * n[0] + (c[1] - a[1]) * n[1] + SEAM_M for c in corners])
        edges.append({"a": a, "d": d, "n": n, "t": (-reach, length + reach), "s": (0.0, reach)})
    ex, ez = zip(*(c for r in edges for c in rect_corners(r)))
    return {"centre": centre, "angle_deg": angle, "outline": outline, "core": inset(outline, FEATHER_M / 2),
            "window": window, "edges": edges, "extent": (min(ex), max(ex), min(ez), max(ez))}


def _along(rng, path, shape, angle, placed, half):
    """The patch `shape` (centred on the origin, at `angle`) laid on along a path: past the path's last patch the
    way the path runs, stepped sideways by up to PATH_SIDE of its width, behind the shortest mat bridge of PATH_GAP_M
    (every PATH_GAP_STEP_M) where it fits; None when none does (the path ends)."""
    a = math.radians(angle)
    u = (path["sign"] * math.cos(a), path["sign"] * math.sin(a))
    v = (-u[1], u[0])
    last = path["last"]
    ahead = max((p[0] - last["centre"][0]) * u[0] + (p[1] - last["centre"][1]) * u[1] for p in last["outline"])
    behind = max(-(q[0] * u[0] + q[1] * u[1]) for q in shape)
    across = [q[0] * v[0] + q[1] * v[1] for q in shape]
    side = rng.uniform(-PATH_SIDE, PATH_SIDE) * (max(across) - min(across))
    for k in range(round((PATH_GAP_M[1] - PATH_GAP_M[0]) / PATH_GAP_STEP_M) + 1):
        step = ahead + PATH_GAP_M[0] + k * PATH_GAP_STEP_M + behind
        centre = (last["centre"][0] + u[0] * step + v[0] * side, last["centre"][1] + u[1] * step + v[1] * side)
        candidate = _patch(centre, angle, [(x + centre[0], z + centre[1]) for x, z in shape])
        if _fits(candidate, placed, half):
            return candidate
    return None


def _fits(patch, placed, half):
    """Its edge boxes stay inside the area and out of every other outline, and theirs out of its outline."""
    for rect in patch["edges"]:
        if any(abs(c) > half + EPS for corner in rect_corners(rect) for c in corner):
            return False
    for other in placed:
        if not (_meet(patch["extent"], other["window"]) or _meet(other["extent"], patch["window"])):
            continue  # neither's edge boxes reach the other's window, which holds its outline
        if any(_overlap(rect_corners(r), other["outline"]) for r in patch["edges"]):
            return False
        if any(_overlap(rect_corners(r), patch["outline"]) for r in other["edges"]):
            return False
    return True


def _meet(a, b):
    """Whether two axis-aligned rectangles (x0, x1, z0, z1) overlap or touch."""
    return a[0] <= b[1] and b[0] <= a[1] and a[2] <= b[3] and b[2] <= a[3]


def _overlap(a, b):
    """Whether two convex polygons' interiors overlap (separating axis test; touching is no overlap)."""
    for polygon in (a, b):
        for p, q in _edges_of(polygon):
            axis = (q[1] - p[1], p[0] - q[0])
            pa = [x * axis[0] + z * axis[1] for x, z in a]
            pb = [x * axis[0] + z * axis[1] for x, z in b]
            if max(pa) <= min(pb) + EPS or max(pb) <= min(pa) + EPS:
                return False
    return True


def _clip(polygon, a, b, d):
    """The part of a polygon on the inner side of the line a-b moved d inward (the left of a counter-clockwise
    edge)."""
    length = math.hypot(b[0] - a[0], b[1] - a[1])
    n = ((b[1] - a[1]) / length, (a[0] - b[0]) / length)  # outward

    def h(p):
        return (p[0] - a[0]) * n[0] + (p[1] - a[1]) * n[1] + d  # <= 0 is kept

    out = []
    for p, q in _edges_of(polygon):
        hp, hq = h(p), h(q)
        if hp <= 0:
            out.append(p)
        if (hp <= 0) != (hq <= 0):
            t = hp / (hp - hq)
            out.append((p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1])))
    return out


def _edges_of(polygon):
    return zip(polygon, polygon[1:] + polygon[:1])


def _plane(p, a, b):
    """How far p lies on the inner side of the counter-clockwise edge a-b's line (negative outside)."""
    length = math.hypot(b[0] - a[0], b[1] - a[1])
    return -((p[0] - a[0]) * (b[1] - a[1]) + (p[1] - a[1]) * (a[0] - b[0])) / length


def _segment_distance(p, a, b):
    dx, dz = b[0] - a[0], b[1] - a[1]
    t = max(0.0, min(1.0, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dz) / (dx * dx + dz * dz)))
    return math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dz)
