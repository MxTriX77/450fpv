"""Closed solids for small machined and printed parts (p5-drone task 1.3): boxes, prisms, lathed bodies and tubes,
emitted into a lib.geometry.Mesh in asset space (lib.frame). Plain Python; no Blender.

Every solid is closed (real thickness, REAL-05). Shading: a generator marks its slots smooth (Mesh.smooth), and these
solids decide what shades smooth by which faces share vertices: a flat face (a box's side, a cap, a prism's top) has
vertices of its own, so its corners take its own normal; a curved surface (a lathe's band, a tube's wall, a prism's
rounded side) shares its vertices along the curve and is split where its direction turns by more than CREASE_DEG.

A paint is (slot, uv): the face's material slot and the one UV every corner of it takes, a palette column
(palette_uv). Palette recipes are flat colour per column (assets-src/materials, kind "palette"), so a face needs no
UV layout of its own.
"""
import math

CREASE_DEG = 35.0  # a profile or outline turning more than this between two segments is a hard edge
EPS = 1e-12


def palette_uv(column, columns):
    """The UV of the centre of a palette's column, laid out left to right and padded to a power of two (derive.py)."""
    padded = 1 << (columns - 1).bit_length()
    return ((column + 0.5) / padded, 0.5)


class Frame:
    """An orthonormal frame: a point of asset space and three unit axes u, v, w (any handedness). Local
    coordinates (a, b, c) are a·u + b·v + c·w from the origin."""

    def __init__(self, origin, u, v, w):
        self.origin = tuple(float(c) for c in origin)
        self.u, self.v, self.w = (normalize(u), normalize(v), normalize(w))
        for x, y in ((self.u, self.v), (self.v, self.w), (self.u, self.w)):
            if abs(dot(x, y)) > 1e-9:
                raise ValueError(f"frame axes must be orthogonal: {self.u}, {self.v}, {self.w}")

    def at(self, a, b, c):
        return tuple(self.origin[i] + a * self.u[i] + b * self.v[i] + c * self.w[i] for i in range(3))

    def dir(self, a, b, c):
        return tuple(a * self.u[i] + b * self.v[i] + c * self.w[i] for i in range(3))


def box(mesh, frame, lo, hi, paint, faces=None):
    """An axis-aligned box of the frame from local corner `lo` to `hi`; every face flat with its own vertices.
    `faces` maps a face name ('-u', '+u', '-v', '+v', '-w', '+w') to its own paint."""
    faces = faces or {}
    for axis, name in enumerate("uvw"):
        for side in (-1, 1):
            corners = []
            for s0 in (0, 1):
                for s1 in (0, 1):
                    local = [0.0, 0.0, 0.0]
                    others = [a for a in range(3) if a != axis]
                    local[axis] = hi[axis] if side > 0 else lo[axis]
                    local[others[0]] = hi[others[0]] if s0 else lo[others[0]]
                    local[others[1]] = hi[others[1]] if s1 ^ s0 else lo[others[1]]
                    corners.append(frame.at(*local))
            normal = frame.dir(*[float(side) if a == axis else 0.0 for a in range(3)])
            _flat(mesh, corners, normal, faces.get(("+" if side > 0 else "-") + name, paint))


def prism(mesh, frame, outline, b0, b1, paint, side_paint=None, smooth_sides=False):
    """The outline, a simple polygon in the frame's (a, c) plane, extruded along v from b0 to b1: two flat caps and
    a side of one quad per edge. `smooth_sides` shares the side's vertices between edges that turn less than
    CREASE_DEG (a rounded end), split elsewhere."""
    pts = _ccw(outline)
    n = len(pts)
    b0, b1 = min(b0, b1), max(b0, b1)
    bottom = [frame.at(a, b0, c) for a, c in pts]
    top = [frame.at(a, b1, c) for a, c in pts]
    _flat(mesh, top, frame.dir(0.0, 1.0, 0.0), paint)
    _flat(mesh, bottom, frame.dir(0.0, -1.0, 0.0), paint)
    side_paint = side_paint or paint
    normals = []
    for i in range(n):
        (a0, c0), (a1, c1) = pts[i], pts[(i + 1) % n]
        normals.append(normalize(frame.dir(c1 - c0, 0.0, -(a1 - a0))))  # outward of a CCW outline in (a, c)
    shared = [smooth_sides and _turn(normals[i - 1], normals[i]) < CREASE_DEG for i in range(n)]
    lo_ids, hi_ids = {}, {}
    for i in range(n):
        if shared[i]:
            lo_ids[i] = mesh.vertex(bottom[i])
            hi_ids[i] = mesh.vertex(top[i])
    for i in range(n):
        j = (i + 1) % n
        a = lo_ids[i] if shared[i] else mesh.vertex(bottom[i])
        b = lo_ids[j] if shared[j] else mesh.vertex(bottom[j])
        c = hi_ids[j] if shared[j] else mesh.vertex(top[j])
        d = hi_ids[i] if shared[i] else mesh.vertex(top[i])
        mesh.face([a, b, c, d], normals[i], side_paint[0], [side_paint[1]] * 4)


def lathe(mesh, frame, profile, segments, paints, closed=False, phase=0.0):
    """A body of revolution about the frame's w axis: `profile` is [(c, r), ...] along w with r >= 0 the radius,
    traced from the body's -w end over its outside to its +w end (a closed ring's profile then runs back inside it,
    `closed`), so each segment's outward normal is its tangent (dc, dr) turned to (-dr, dc). `paints` gives one paint
    per segment. A segment from or to the axis (r = 0) is a flat disc, one face, or a cone fanned to its apex. The
    rings start at angle `phase` from u towards v."""
    count = len(profile) - (0 if closed else 1)
    if len(paints) != count:
        raise ValueError(f"a lathe of {count} segments needs as many paints, got {len(paints)}")
    angles = [phase + 2 * math.pi * k / segments for k in range(segments)]
    segs = [(profile[i], profile[(i + 1) % len(profile)]) for i in range(count)]
    tangents = [normalize2((p1[0] - p0[0], p1[1] - p0[1])) for p0, p1 in segs]
    rings = {}  # (profile index, segment index) -> vertex ids of that point's ring as that segment uses it

    def ring(k, s):
        """The ring of profile point k for segment s: shared with the neighbouring segment unless they crease."""
        point = profile[k % len(profile)]
        if point[1] < EPS:
            return None
        other = s - 1 if k == s else s + 1  # the other segment meeting at point k
        smooth = (0 <= other < count or closed) and _turn2(tangents[s], tangents[other % count]) < CREASE_DEG
        key = (k % len(profile), min(s, other % count) if smooth else s, smooth)
        if key not in rings:
            rings[key] = [mesh.vertex(frame.at(point[1] * math.cos(t), point[1] * math.sin(t), point[0]))
                          for t in angles]
        return rings[key]

    for s, ((c0, r0), (c1, r1)) in enumerate(segs):
        dc, dr = c1 - c0, r1 - r0
        if abs(dc) < EPS and abs(dr) < EPS:
            raise ValueError(f"lathe segment {s} has no length")
        if r0 < EPS and r1 < EPS:
            raise ValueError(f"lathe segment {s} runs along the axis")
        nc, nr = normalize2((-dr, dc))
        slot, uv = paints[s]
        if (r0 < EPS or r1 < EPS) and abs(dc) < EPS:  # a flat disc: one face of its own
            r = max(r0, r1)
            ids = [mesh.vertex(frame.at(r * math.cos(t), r * math.sin(t), c0)) for t in angles]
            mesh.face(ids, frame.dir(0.0, 0.0, nc), slot, [uv] * len(ids))
            continue
        a, b = ring(s, s), ring(s + 1, s)
        for k in range(segments):
            mid = angles[k] + math.pi / segments
            normal = frame.dir(nr * math.cos(mid), nr * math.sin(mid), nc)
            k1 = (k + 1) % segments
            if a is None or b is None:  # a cone to its apex on the axis
                rim = b if a is None else a
                apex = mesh.vertex(frame.at(0.0, 0.0, c0 if a is None else c1))
                mesh.face([rim[k], rim[k1], apex], normal, slot, [uv] * 3)
            else:
                mesh.face([a[k], a[k1], b[k1], b[k]], normal, slot, [uv] * 4)


def tube(mesh, points, radius, sides, paint, caps=True, phase=0.0):
    """A round tube of `radius` along a polyline, its wall smooth (rings carried along the line by parallel
    transport), its two ends flat caps. The polyline needs two points at least, no two equal in a row."""
    if len(points) < 2:
        raise ValueError("a tube needs two points at least")
    dirs = [normalize(sub(points[i + 1], points[i])) for i in range(len(points) - 1)]
    tangents = [dirs[0]] + [normalize(add(dirs[i - 1], dirs[i])) for i in range(1, len(dirs))] + [dirs[-1]]
    helper = (0.0, 1.0, 0.0) if abs(tangents[0][1]) < 0.9 else (1.0, 0.0, 0.0)
    n = normalize(cross(tangents[0], helper))
    rings = []
    for i, (p, t) in enumerate(zip(points, tangents)):
        if i:
            n = normalize(sub(n, scale(t, dot(n, t))))  # parallel transport onto the new tangent
        b = cross(t, n)
        ring = []
        for k in range(sides):
            ang = phase + 2 * math.pi * k / sides
            off = add(scale(n, math.cos(ang) * radius), scale(b, math.sin(ang) * radius))
            ring.append((add(p, off), off))
        rings.append(ring)
    ids = [[mesh.vertex(p) for p, _ in ring] for ring in rings]
    slot, uv = paint
    for i in range(len(points) - 1):
        for k in range(sides):
            k1 = (k + 1) % sides
            normal = normalize(add(rings[i][k][1], rings[i][k1][1]))
            normal = normalize(sub(normal, scale(dirs[i], dot(normal, dirs[i]))))
            mesh.face([ids[i][k], ids[i][k1], ids[i + 1][k1], ids[i + 1][k]], normal, slot, [uv] * 4)
    if caps:
        _flat(mesh, [p for p, _ in rings[0]], scale(dirs[0], -1.0), paint)
        _flat(mesh, [p for p, _ in rings[-1]], dirs[-1], paint)


def ribbon(mesh, frame, path, width, thickness, paints, closed=False):
    """A flat strap of `width` along w and `thickness` along a path in the frame's (a, b) plane: the path is the
    strap's inner face, the outer face lies `thickness` outside it (away from the path's left for a CCW loop),
    `paints` = (outer, inner, edges); an outer paint may be a list of (share of the width, paint) bands across it.
    Open strips get flat end faces."""
    pts = _ccw(path) if closed else list(path)
    n = len(pts)
    seg = n if closed else n - 1
    outs = []
    for i in range(n):  # the outward offset at each point: the bisector of its segments' outward normals
        before = pts[i - 1] if (closed or i) else None
        after = pts[(i + 1) % n] if (closed or i < n - 1) else None
        normals = []
        for p0, p1 in ((before, pts[i]), (pts[i], after)):
            if p0 is not None and p1 is not None:
                d = normalize2((p1[0] - p0[0], p1[1] - p0[1]))
                normals.append((d[1], -d[0]))  # right of the direction: outside a CCW loop
        m = normalize2((sum(x for x, _ in normals), sum(y for _, y in normals)))
        cos_half = max(0.3, m[0] * normals[0][0] + m[1] * normals[0][1])
        outs.append((m[0] / cos_half, m[1] / cos_half))
    outer, inner, edges = paints
    bands = outer if isinstance(outer, list) else [(1.0, outer)]
    cuts = [0.0]
    for share, _ in bands:
        cuts.append(cuts[-1] + share)
    cuts = [c / cuts[-1] for c in cuts]
    ws = [(-0.5 + c) * width for c in cuts]

    def pt(i, off, w):
        a, b = pts[i]
        return frame.at(a + outs[i][0] * off, b + outs[i][1] * off, w)

    for i in range(seg):
        j = (i + 1) % n
        d = normalize2((pts[j][0] - pts[i][0], pts[j][1] - pts[i][1]))
        out_n = frame.dir(d[1], -d[0], 0.0)
        for k, (_, paint) in enumerate(bands):
            _flat(mesh, [pt(i, thickness, ws[k]), pt(j, thickness, ws[k]), pt(j, thickness, ws[k + 1]),
                         pt(i, thickness, ws[k + 1])], out_n, paint)
        _flat(mesh, [pt(i, 0.0, ws[0]), pt(j, 0.0, ws[0]), pt(j, 0.0, ws[-1]), pt(i, 0.0, ws[-1])],
              scale(out_n, -1.0), inner)
        for w, sign in ((ws[0], -1.0), (ws[-1], 1.0)):
            _flat(mesh, [pt(i, 0.0, w), pt(j, 0.0, w), pt(j, thickness, w), pt(i, thickness, w)],
                  frame.dir(0.0, 0.0, sign), edges)
    if not closed:
        for i, sign in ((0, -1.0), (n - 1, 1.0)):
            k = 0 if i == 0 else n - 2
            d = normalize2((pts[k + 1][0] - pts[k][0], pts[k + 1][1] - pts[k][1]))
            _flat(mesh, [pt(i, 0.0, ws[0]), pt(i, thickness, ws[0]), pt(i, thickness, ws[-1]), pt(i, 0.0, ws[-1])],
                  frame.dir(d[0] * sign, d[1] * sign, 0.0), edges)


def circle(cx, cy, r, count, start=0.0, end=None):
    """Points on a circle in a plane, `count` of them from angle `start` (radians), evenly round the whole circle,
    or from start to end inclusive when `end` is given."""
    if end is None:
        return [(cx + r * math.cos(start + 2 * math.pi * k / count), cy + r * math.sin(start + 2 * math.pi * k / count))
                for k in range(count)]
    return [(cx + r * math.cos(start + (end - start) * k / (count - 1)),
             cy + r * math.sin(start + (end - start) * k / (count - 1))) for k in range(count)]


def _flat(mesh, corners, normal, paint):
    slot, uv = paint
    ids = [mesh.vertex(p) for p in corners]
    mesh.face(ids, normal, slot, [uv] * len(ids))


def _ccw(points):
    """The polygon counter-clockwise in its (first, second) coordinates."""
    area = sum(points[i][0] * points[(i + 1) % len(points)][1] - points[(i + 1) % len(points)][0] * points[i][1]
               for i in range(len(points)))
    return list(points) if area > 0 else list(points[::-1])


def _turn(a, b):
    return math.degrees(math.acos(max(-1.0, min(1.0, dot(a, b)))))


def _turn2(a, b):
    return math.degrees(math.acos(max(-1.0, min(1.0, a[0] * b[0] + a[1] * b[1]))))


def normalize2(v):
    length = math.hypot(v[0], v[1])
    if length < EPS:
        raise ValueError("a zero 2D vector")
    return (v[0] / length, v[1] / length)


def add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def scale(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def normalize(v):
    length = math.sqrt(dot(v, v))
    if length < EPS:
        raise ValueError("a zero vector")
    return (v[0] / length, v[1] / length, v[2] / length)


AXES = Frame((0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))
