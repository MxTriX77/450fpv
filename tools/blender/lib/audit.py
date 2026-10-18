"""The build-time collision audit (p1-pipeline-hardening, "Collision audit at build time"; physics review P1-2;
AST-05), in plain Python so the tests run without Blender.

For every collision primitive, the largest distance from its surface to the LOD0 mesh (an invisible wall), and for
the LOD0 mesh, the largest distance from its surface to the union of the primitives (a visible surface nothing
collides with), each within 1 mm of the true maximum. Beyond 50 mm the build fails, or beyond 20 mm within 0.3 m of
a gap polygon or a landable face (a landable primitive's faces whose normal is within 30 degrees of up). Each gap's
narrowest_m is checked against the diameter of the largest circle inscribed in its polygon, within 5 mm.

Primitives form a union: the part of a primitive's surface that lies inside another primitive is not a collision
surface (a mound sphere's lower half sunk in the ground slab, the slab's top under the mound), so it is not
measured. A shape whose material is a surface (surface:<id>, a stand-in for ground) is landed on over its whole
top, so it widens no 2 cm zone: its top is held to the 5 cm tolerance everywhere (p1-s04-belt-straw, design 6),
and the lines say so. Visual-only micro meshes (meta["micro"]) are not part of the LOD0 mesh the audit sees; the
lines name each one skipped.

Both distances are found by branch and bound over triangles: a cell's three vertices give exact values, a lower
bound; an upper bound comes from convexity; the cell with the largest upper bound is split in four until no cell
can beat the best value by PRECISION_M. The bounds are tight wherever the surfaces coincide, so the search refines
only around chamfers, edges and openings:
- primitive -> mesh: cells are the primitive's tessellation (curved shapes within CHORD_M); the distance to one
  convex mesh face is convex, so its largest value over a cell sits at a vertex. A cell that spans two faces
  sharing an edge is clipped at the fold's bisector plane and each part bounded by its own face.
- mesh -> primitives: cells are the mesh's triangles; each primitive's signed distance is exact. Outside a convex
  primitive the distance is convex (a vertex bound); inside it, the depth is bounded through the shape's own
  planes, centre or axis (exact for a sphere, capsule or cylinder, the nearest face for a box)."""
import heapq
import math

TOLERANCE_M = 0.05          # AST-05: collision follows the visual within +-5 cm
NEAR_TOLERANCE_M = 0.02     # +-2 cm around openings and landing surfaces
NEAR_RANGE_M = 0.3
PRECISION_M = 0.0001        # the search stops when no cell can beat the best by this; with CHORD_M, under 1 mm
CHORD_M = 0.0004            # tessellation error allowed on spheres, capsules and cylinders
NARROWEST_TOLERANCE_M = 0.005
LANDABLE_COS = math.cos(math.radians(30))
UP = (0.0, 1.0, 0.0)
FAR_M = 1000.0              # a cylinder's axis as a segment long enough to act as a line
SURFACE_PREFIX = "surface:"  # lib.meta.SURFACE_PREFIX; a shape standing in for ground
COVERED_M = 1e-9            # a point this deep inside another primitive is on no collision surface
BOX_CELL_M = 0.25           # a box face is tessellated in cells of at most this, so the search never quarters a long
                            # thin wall (12 x 0.2 m) into slivers; a brick's faces stay one cell each


def run(meta, lod0):
    """(problems, report lines) of one variant: `meta` as lib.meta.assemble builds it, `lod0` its first
    lib.geometry.Mesh. Problems name the primitive, the deviation and the tolerance; the lines carry the measured
    numbers for every primitive, the mesh and every gap, success or not."""
    faces = _mesh_faces(lod0)
    index = _FaceIndex(faces)
    shapes = [_Shape(s) for s in meta["collision"]]
    zone = _zone(meta, shapes)
    problems, lines = [], []
    for m in meta.get("micro", []):
        lines.append(f"audit: skipped micro mesh {m['name']} ({m['count']} {m['kind']})")
    surfaces = [f"collision[{i}]" for i, s in enumerate(meta["collision"]) if s["material"].startswith(SURFACE_PREFIX)]
    if surfaces:
        lines.append(f"audit: surface shapes {', '.join(surfaces)} are landed on over their whole top, "
                     f"held to {TOLERANCE_M * 1000:g} mm there")
    for i, shape in enumerate(shapes):
        others = [s for j, s in enumerate(shapes) if j != i]
        cells = [tri[:3] for tri in shape.triangles]

        def value(v, others=others):
            d, which = index.nearest(v)
            return (0.0 if _covered(v, others) else d), which

        def bound(cell, which, others=others):
            return 0.0 if _cell_covered(cell, others) else _faces_bound(cell, which, faces)

        problems += _report(lines, f"collision[{i}] {shape.kind}", f"{shape.kind}->mesh", cells, zone, value, bound)
    if not shapes:
        problems.append("mesh: no collision primitive stands for the LOD0 mesh")
    else:
        cells = [tuple(face.points[i] for i in tri) for face in faces for tri in face.fan()]
        problems += _report(lines, "mesh", "mesh->collision", cells, zone,
                            lambda v: _nearest_shape(v, shapes), lambda cell, which: _shapes_bound(cell, which, shapes),
                            lambda which: f" nearest collision[{which}]")
    for i, gap in enumerate(meta["gaps"]):
        diameter = 2 * _inscribed_radius(_flatten_polygon(gap["polygon"], gap["normal"]))
        text = f"gaps[{i}] '{gap['id']}': narrowest_m {gap['narrowest_m']:.4f}, largest inscribed circle {diameter:.4f}"
        if abs(gap["narrowest_m"] - diameter) > NARROWEST_TOLERANCE_M:
            problems.append(f"{text}: they differ by more than {NARROWEST_TOLERANCE_M * 1000:g} mm")
        lines.append(f"audit {text}, tolerance {NARROWEST_TOLERANCE_M * 1000:g} mm")
    return problems, lines


def _report(lines, label, direction, cells, zone, value, bound, note=lambda which: ""):
    """Measures one direction, appends its report line and returns its problems."""
    distance, at, which = _directed_max(cells, value, bound)
    text = f"{direction} {distance * 1000:.3f} mm at {_fmt(at)}{note(which)}"
    line = f"{label}: {text}, tolerance {TOLERANCE_M * 1000:g} mm"
    problems = [] if distance <= TOLERANCE_M else [f"{label}: {text} exceeds the tolerance {TOLERANCE_M * 1000:g} mm"]
    if zone:
        distance, at, which = _directed_max(cells, value, bound, zone)
        if at is not None:
            text = f"near gaps/landable {distance * 1000:.3f} mm at {_fmt(at)}{note(which)}"
            line += f"; {text}, tolerance {NEAR_TOLERANCE_M * 1000:g} mm"
            if distance > NEAR_TOLERANCE_M:
                problems.append(f"{label}: {text} exceeds the tolerance {NEAR_TOLERANCE_M * 1000:g} mm")
    lines.append("audit " + line)
    return problems


def _directed_max(cells, value, bound, zone=None):
    """The largest value over the surface of the `cells` triangles, within PRECISION_M, as (value, point, which):
    `value(vertex)` gives the exact (distance, which) at a point and `bound(cell, [which per vertex])` an upper
    bound over a cell. Only the points within NEAR_RANGE_M of `zone` count when it is given (point None when no
    such point exists)."""
    cache, near = {}, {}
    best, at, which = -1.0, None, None
    heap = []

    def push(cell):
        nonlocal best, at, which
        for v in cell:
            if v not in cache:
                cache[v] = value(v)
            if zone and v not in near:
                near[v] = _zone_distance(v, zone)
        if zone and min(near[v] for v in cell) - _longest_edge(cell) > NEAR_RANGE_M:
            return
        for v in cell:
            d, w = cache[v]
            if d > best and (not zone or near[v] <= NEAR_RANGE_M):
                best, at, which = d, v, w
        upper = bound(cell, [cache[v][1] for v in cell])
        if upper > best + PRECISION_M:
            heapq.heappush(heap, (-upper, len(heap), cell))

    for cell in cells:
        push(cell)
    while heap and -heap[0][0] > best + PRECISION_M:
        _, _, (a, b, c) = heapq.heappop(heap)
        ab, bc, ca = _mid(a, b), _mid(b, c), _mid(c, a)
        for child in ((a, ab, ca), (ab, b, bc), (ca, bc, c), (ab, bc, ca)):
            push(child)
    return best, at, which


# --- the mesh as convex faces ---------------------------------------------------------------------------------

class _Face:
    """A convex planar polygon of the mesh: points counter-clockwise about its outward normal, the vertex indices
    it was built from (for the shared edge with a neighbour), and its bounding box."""

    def __init__(self, points, indices, outward):
        self.normal = _normalize(outward)  # generators fix only its direction
        if _dot(_newell(points), outward) < 0:
            points, indices = points[::-1], indices[::-1]
        self.points, self.indices = points, indices
        self.lo = tuple(min(p[k] for p in points) for k in range(3))
        self.hi = tuple(max(p[k] for p in points) for k in range(3))
        # Per edge (a, b): the in-plane inward normal n x (b - a), so a point q projects inside the face when
        # (q - a) . w >= 0 on every edge; and the edge vector with its squared length, for the distance to it.
        self.edges = []
        for a, b in zip(points, points[1:] + points[:1]):
            e = _sub(b, a)
            self.edges.append((a, _cross(self.normal, e), e, _dot(e, e)))

    def fan(self):
        return [(0, k, k + 1) for k in range(1, len(self.points) - 1)]

    def centroid(self):
        return tuple(sum(p[k] for p in self.points) / len(self.points) for k in range(3))


def _mesh_faces(mesh):
    """The mesh's faces as _Face polygons: convex ones whole, concave ones ear-clipped into triangles."""
    faces = []
    for indices, outward, _, _ in mesh.faces:
        points = [mesh.vertices[i] for i in indices]
        if _is_convex(points, outward):
            faces.append(_Face(points, list(indices), outward))
        else:
            for tri in _triangulate(points, outward):
                faces.append(_Face([points[i] for i in tri], [indices[i] for i in tri], outward))
    return faces


def _face_distance(p, face):
    """Distance from p to a convex planar face: to its plane when p projects inside, else to its nearest edge.
    The hottest function of the audit, so the arithmetic is written out."""
    px, py, pz = p
    nx, ny, nz = face.normal
    a0 = face.points[0]
    h = (px - a0[0]) * nx + (py - a0[1]) * ny + (pz - a0[2]) * nz
    qx, qy, qz = px - h * nx, py - h * ny, pz - h * nz
    inside = True
    for a, w, _, _ in face.edges:
        if (qx - a[0]) * w[0] + (qy - a[1]) * w[1] + (qz - a[2]) * w[2] < 0:
            inside = False
            break
    if inside:
        return abs(h)
    best = math.inf
    for a, _, e, length2 in face.edges:
        apx, apy, apz = px - a[0], py - a[1], pz - a[2]
        t = 0.0 if length2 == 0 else (apx * e[0] + apy * e[1] + apz * e[2]) / length2
        t = 0.0 if t < 0 else 1.0 if t > 1 else t
        dx, dy, dz = apx - t * e[0], apy - t * e[1], apz - t * e[2]
        d2 = dx * dx + dy * dy + dz * dz
        if d2 < best:
            best = d2
    return math.sqrt(best)


class _FaceIndex:
    """The mesh's faces in a uniform grid of buckets (each face in every bucket its bounding box touches), so the
    nearest face to a point is found among a few buckets instead of the whole mesh: a 12 m ground patch has twenty
    thousand faces, and the search visits most of its vertices. The bucket size is the median face extent."""

    def __init__(self, faces):
        self.faces = faces
        extents = sorted(max(f.hi[k] - f.lo[k] for k in range(3)) for f in faces)
        self.h = max(0.01, extents[len(extents) // 2]) if extents else 1.0
        self.buckets = {}
        for i, face in enumerate(faces):
            lo, hi = self._cell(face.lo), self._cell(face.hi)
            for x in range(lo[0], hi[0] + 1):
                for y in range(lo[1], hi[1] + 1):
                    for z in range(lo[2], hi[2] + 1):
                        self.buckets.setdefault((x, y, z), []).append(i)
        self.reach = max((abs(c[k]) for c in self.buckets for k in range(3)), default=0) + 1

    def _cell(self, p):
        return (math.floor(p[0] / self.h), math.floor(p[1] / self.h), math.floor(p[2] / self.h))

    def nearest(self, p):
        """(distance, index) of the face nearest to p: the rings of buckets around p's, outwards, until no further
        ring can hold a closer face (a face in ring k lies at least (k - 1) bucket sizes away)."""
        px, py, pz = p
        cx, cy, cz = self._cell(p)
        best, which = math.inf, None
        seen = set()
        k = 0
        while True:
            for x in range(cx - k, cx + k + 1):
                for y in range(cy - k, cy + k + 1):
                    edge = max(abs(x - cx), abs(y - cy)) == k
                    for z in (range(cz - k, cz + k + 1) if edge else (cz - k, cz + k) if k else (cz,)):
                        for i in self.buckets.get((x, y, z), ()):
                            if i in seen:
                                continue
                            seen.add(i)
                            face = self.faces[i]
                            lo, hi = face.lo, face.hi
                            dx = lo[0] - px if px < lo[0] else px - hi[0] if px > hi[0] else 0.0
                            dy = lo[1] - py if py < lo[1] else py - hi[1] if py > hi[1] else 0.0
                            dz = lo[2] - pz if pz < lo[2] else pz - hi[2] if pz > hi[2] else 0.0
                            if dx * dx + dy * dy + dz * dz >= best * best:
                                continue
                            d = _face_distance(p, face)
                            if d < best:
                                best, which = d, i
            if best <= k * self.h or k > self.reach + max(abs(cx), abs(cy), abs(cz)):
                return best, which
            k += 1


def _covered(p, others):
    """Whether p lies inside one of the other primitives, so no collision surface passes there."""
    return any(shape.sdf(p) <= COVERED_M for shape in others)


def _cell_covered(cell, others):
    """Whether a whole triangle lies inside one other primitive: every primitive is convex, so its three vertices
    inside the same one is enough."""
    return any(all(shape.sdf(v) <= COVERED_M for v in cell) for shape in others)


def _faces_bound(cell, which, faces):
    """An upper bound of the distance to the mesh over a cell, from the faces nearest to its vertices: the largest
    vertex distance to one face (convex, so its maximum sits at a vertex); when two candidate faces share an edge,
    also the cell clipped at the fold's bisector plane, each part held to its own face."""
    candidates = sorted(set(which))
    upper = min(max(_face_distance(v, faces[i]) for v in cell) for i in candidates)
    if len(candidates) == 2:
        a, b = faces[candidates[0]], faces[candidates[1]]
        shared = [a.points[a.indices.index(i)] for i in a.indices if i in b.indices]
        if len(shared) >= 2:
            axis = _sub(shared[1], shared[0])
            mean = _add(a.normal, b.normal)
            w = _normalize(_cross(axis, mean if _dot(mean, mean) > 1e-12 else a.normal))
            if _dot(_sub(a.centroid(), shared[0]), w) < 0:
                w = (-w[0], -w[1], -w[2])
            side_a, side_b = _clip(cell, shared[0], w)
            upper = min(upper, max([_face_distance(v, a) for v in side_a] + [_face_distance(v, b) for v in side_b]))
    return upper


def _clip(cell, origin, normal):
    """The cell's parts on the positive and the negative side of the plane through `origin` with `normal`."""
    positive, negative = [], []
    for p, q in zip(cell, cell[1:] + cell[:1]):
        hp, hq = _dot(_sub(p, origin), normal), _dot(_sub(q, origin), normal)
        (positive if hp >= 0 else negative).append(p)
        if (hp < 0) != (hq < 0) and hp != hq:
            t = hp / (hp - hq)
            x = (p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1]), p[2] + t * (q[2] - p[2]))
            positive.append(x)
            negative.append(x)
    return positive, negative


# --- the primitives, exact ------------------------------------------------------------------------------------

class _Shape:
    """A collision primitive: its signed distance in asset space (negative inside), the depth bound over a cell,
    and its tessellation for the primitive -> mesh direction and the landable faces."""

    def __init__(self, shape):
        self.kind, self.centre = shape["shape"], tuple(shape["center"])
        self.q = tuple(shape.get("rotation", (0.0, 0.0, 0.0, 1.0)))
        self.r = shape.get("radius")
        self.half = tuple(s / 2 for s in shape["size"]) if self.kind == "box" else None
        self.h = shape["height"] / 2 - (self.r if self.kind == "capsule" else 0.0) if "height" in shape else 0.0
        self.triangles = _tessellate(self)

    def local(self, p):
        x, y, z, w = self.q
        return _rotate((-x, -y, -z, w), _sub(p, self.centre))

    def place(self, p):
        return _add(self.centre, _rotate(self.q, p))

    def sdf(self, p):
        x, y, z = self.local(p)
        if self.kind == "box":
            d = (abs(x) - self.half[0], abs(y) - self.half[1], abs(z) - self.half[2])
            outside = math.sqrt(sum(max(c, 0.0) ** 2 for c in d))
            return outside + min(max(d), 0.0)
        if self.kind == "sphere":
            return math.sqrt(x * x + y * y + z * z) - self.r
        if self.kind == "capsule":
            yy = min(self.h, max(-self.h, y))
            return math.sqrt(x * x + (y - yy) ** 2 + z * z) - self.r
        d = (math.sqrt(x * x + z * z) - self.r, abs(y) - self.h)
        return min(max(d), 0.0) + math.sqrt(max(d[0], 0.0) ** 2 + max(d[1], 0.0) ** 2)

    def bound(self, cell):
        """An upper bound of |sdf| over a cell: outside, the distance to the convex shape is convex (largest at a
        vertex); inside, the depth is at most the depth below the shape's nearest plane (box), or the radius less
        the cell's distance to the centre or axis (sphere, capsule, cylinder), exactly."""
        local = [self.local(v) for v in cell]
        outside = max(max(self.sdf(v), 0.0) for v in cell)
        if self.kind == "box":
            depth = min(max(self.half[k] - s * p[k] for p in local) for k in range(3) for s in (-1, 1))
        elif self.kind == "sphere":
            depth = self.r - _point_triangle_distance((0.0, 0.0, 0.0), local)
        else:
            reach = self.h if self.kind == "capsule" else FAR_M
            depth = self.r - _segment_triangle_distance((0.0, -reach, 0.0), (0.0, reach, 0.0), local)
            if self.kind == "cylinder":
                ys = [p[1] for p in local]
                depth = min(depth, self.h - (0.0 if min(ys) < 0 < max(ys) else min(abs(y) for y in ys)))
        return max(outside, depth, 0.0)


def _nearest_shape(p, shapes):
    return min((abs(shape.sdf(p)), i) for i, shape in enumerate(shapes))


def _shapes_bound(cell, which, shapes):
    return min(shapes[i].bound(cell) for i in set(which))


def _tessellate(shape):
    """(a, b, c, outward normal) triangles of a _Shape's surface in asset space: a box exactly, each face in cells
    of at most BOX_CELL_M, the curved shapes within CHORD_M, built along local +Y, rotated by the shape's
    quaternion and moved to its centre."""
    if shape.kind == "box":
        h = shape.half
        triangles = []
        for axis in range(3):
            u, v = (axis + 1) % 3, (axis + 2) % 3
            nu, nv = (max(1, math.ceil(2 * h[k] / BOX_CELL_M)) for k in (u, v))
            for side in (-1, 1):
                def corner(i, j):
                    p = [0.0, 0.0, 0.0]
                    p[axis], p[u], p[v] = side * h[axis], -h[u] + 2 * h[u] * i / nu, -h[v] + 2 * h[v] * j / nv
                    return shape.place(p)
                for i in range(nu):
                    for j in range(nv):
                        a, b, c, d = corner(i, j), corner(i + 1, j), corner(i + 1, j + 1), corner(i, j + 1)
                        triangles += [(a, b, c), (a, c, d)]
    else:
        r, h = shape.r, shape.h
        step = math.pi / 4 if r <= CHORD_M else math.acos(1 - CHORD_M / r)
        around, half_rings = max(8, math.ceil(2 * math.pi / step)), max(2, math.ceil(math.pi / step / 2))
        ring = lambda y, rho: [(rho * math.cos(2 * math.pi * j / around), y, rho * math.sin(2 * math.pi * j / around))
                               for j in range(around)]
        if shape.kind == "cylinder":
            rings = [[(0.0, -h, 0.0)], ring(-h, r), ring(h, r), [(0.0, h, 0.0)]]
        else:
            lats = [math.pi / 2 * k / half_rings for k in range(half_rings)]  # 0 at the equator, pole excluded
            lower = [[(0.0, -h - r, 0.0)]] + [ring(-h - r * math.sin(t), r * math.cos(t)) for t in reversed(lats)]
            upper = [ring(h + r * math.sin(t), r * math.cos(t)) for t in lats] + [[(0.0, h + r, 0.0)]]
            rings = lower + upper[1:] if shape.kind == "sphere" else lower + upper  # one equator on a sphere
        triangles = []
        for lo, hi in zip(rings, rings[1:]):
            if len(lo) == 1:
                triangles += [(lo[0], hi[j], hi[(j + 1) % around]) for j in range(around)]
            elif len(hi) == 1:
                triangles += [(lo[j], lo[(j + 1) % around], hi[0]) for j in range(around)]
            else:
                for j in range(around):
                    k = (j + 1) % around
                    triangles += [(lo[j], lo[k], hi[k]), (lo[j], hi[k], hi[j])]
        triangles = [tuple(shape.place(p) for p in t) for t in triangles]
    result = []
    for a, b, c in triangles:
        n = _normalize(_cross(_sub(b, a), _sub(c, a)))
        if n == (0.0, 0.0, 0.0):
            continue  # a capsule of height 2r has a zero-length wall
        if _dot(n, _sub(_centroid(a, b, c), shape.centre)) < 0:
            n = (-n[0], -n[1], -n[2])
        result.append((a, b, c, n))
    return result


# --- the 2 cm zone ----------------------------------------------------------------------------------------------

def _zone(meta, shapes):
    """The geometry that tightens the tolerance: (landable faces, gap polygons), or None when there is none. A
    surface shape's top is a landing face everywhere and is held to the 5 cm tolerance, so it adds no zone."""
    faces = [_Face([a, b, c], [0, 1, 2], n) for shape, entry in zip(shapes, meta["collision"])
             if entry["landable"] and not entry["material"].startswith(SURFACE_PREFIX)
             for a, b, c, n in shape.triangles if _dot(n, UP) >= LANDABLE_COS]
    polygons = [(tuple(tuple(p) for p in g["polygon"]), tuple(g["normal"])) for g in meta["gaps"]]
    return (faces, polygons) if faces or polygons else None


def _zone_distance(p, zone):
    faces, polygons = zone
    return min([_face_distance(p, face) for face in faces] + [_gap_distance(p, *g) for g in polygons])


def _gap_distance(p, polygon, normal):
    """Distance from p to a planar, possibly concave polygon: to its plane when p projects inside it, else to its
    nearest edge."""
    u, v = _plane_basis(normal)
    flat = [(_dot(q, u), _dot(q, v)) for q in polygon]
    if _inside_polygon2((_dot(p, u), _dot(p, v)), flat):
        return abs(_dot(_sub(p, polygon[0]), normal))
    return min(_segment_distance(p, a, b) for a, b in zip(polygon, polygon[1:] + polygon[:1]))


# --- gaps: the largest inscribed circle -------------------------------------------------------------------------

def _flatten_polygon(polygon, normal):
    u, v = _plane_basis(normal)
    return [(_dot(p, u), _dot(p, v)) for p in polygon]


def _inscribed_radius(polygon, precision=0.001):
    """The radius of the largest circle inscribed in a simple 2D polygon, within `precision` (Mapbox's polylabel:
    a quadtree of cells ranked by the best radius they could still hold)."""
    xs, ys = [p[0] for p in polygon], [p[1] for p in polygon]
    size = min(max(xs) - min(xs), max(ys) - min(ys))
    if size <= 0:
        return 0.0
    centroid = (sum(xs) / len(xs), sum(ys) / len(ys))
    best = max(_signed_distance2(centroid, polygon),
               _signed_distance2(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2), polygon))
    heap = []

    def push(x, y, h):
        nonlocal best
        d = _signed_distance2((x, y), polygon)
        best = max(best, d)
        heapq.heappush(heap, (-(d + h * math.sqrt(2)), len(heap), x, y, h))

    x = min(xs)
    while x < max(xs):
        y = min(ys)
        while y < max(ys):
            push(x + size / 2, y + size / 2, size / 2)
            y += size
        x += size
    while heap and -heap[0][0] - best > precision:
        _, _, x, y, h = heapq.heappop(heap)
        for dx in (-h / 2, h / 2):
            for dy in (-h / 2, h / 2):
                push(x + dx, y + dy, h / 2)
    return best


def _signed_distance2(p, polygon):
    """Distance from a 2D point to the polygon's boundary, negative outside."""
    d = min(_segment_distance(p, a, b) for a, b in zip(polygon, polygon[1:] + polygon[:1]))
    return d if _inside_polygon2(p, polygon) else -d


def _inside_polygon2(p, polygon):
    """Crossing-number test in 2D."""
    inside = False
    for (ax, ay), (bx, by) in zip(polygon, polygon[1:] + polygon[:1]):
        if (ay > p[1]) != (by > p[1]) and p[0] < ax + (p[1] - ay) * (bx - ax) / (by - ay):
            inside = not inside
    return inside


# --- polygons and triangulation ---------------------------------------------------------------------------------

def _is_convex(points, normal):
    """Every turn of the cyclic polygon has the same sense about `normal` (collinear points allowed)."""
    sense = 0
    for a, b, c in zip(points, points[1:] + points[:1], points[2:] + points[:2]):
        turn = _dot(_cross(_sub(b, a), _sub(c, b)), normal)
        if abs(turn) > 1e-15:
            if sense and (turn > 0) != (sense > 0):
                return False
            sense = turn
    return True


def _triangulate(points, normal):
    """Ear clipping of a simple polygon in cyclic order, seen along `normal`, as index triples; a polygon with no
    ear left (degenerate) is fanned, and lib.export's mesh validation reports it."""
    u, v = _plane_basis(normal)
    flat = [(_dot(p, u), _dot(p, v)) for p in points]
    order = list(range(len(points)))
    if sum(flat[i][0] * flat[j][1] - flat[j][0] * flat[i][1] for i, j in zip(order, order[1:] + order[:1])) < 0:
        order.reverse()
    triangles = []
    while len(order) > 3:
        for k in range(len(order)):
            i, j, l = order[k - 1], order[k], order[(k + 1) % len(order)]
            if _cross2(flat[i], flat[j], flat[l]) <= 0:
                continue
            if any(_inside2(flat[m], flat[i], flat[j], flat[l]) for m in order if m not in (i, j, l)):
                continue
            triangles.append((i, j, l))
            del order[k]
            break
        else:
            return triangles + [(order[0], order[k], order[k + 1]) for k in range(1, len(order) - 1)]
    return triangles + [tuple(order)]


# --- distances --------------------------------------------------------------------------------------------------

def _point_triangle_distance(p, tri):
    return _face_distance(p, _Face(list(tri), [0, 1, 2], _normalize(_cross(_sub(tri[1], tri[0]), _sub(tri[2], tri[0])))))


def _segment_triangle_distance(s0, s1, tri):
    """Distance between the segment s0-s1 and a triangle: zero when the segment pierces it, else the least of its
    end points to the triangle and the segment to the triangle's edges."""
    face = _Face(list(tri), [0, 1, 2], _normalize(_cross(_sub(tri[1], tri[0]), _sub(tri[2], tri[0]))))
    h0, h1 = _dot(_sub(s0, tri[0]), face.normal), _dot(_sub(s1, tri[0]), face.normal)
    if (h0 < 0) != (h1 < 0) and h0 != h1:
        t = h0 / (h0 - h1)
        x = (s0[0] + t * (s1[0] - s0[0]), s0[1] + t * (s1[1] - s0[1]), s0[2] + t * (s1[2] - s0[2]))
        if _face_distance(x, face) < 1e-12:
            return 0.0
    return min([_face_distance(s0, face), _face_distance(s1, face)]
               + [_segment_segment_distance(s0, s1, a, b) for a, b in zip(tri, tri[1:] + tri[:1])])


def _segment_segment_distance(p1, q1, p2, q2):
    """Distance between two segments (Ericson, Real-Time Collision Detection 5.1.9)."""
    d1, d2, r = _sub(q1, p1), _sub(q2, p2), _sub(p1, p2)
    a, e, f = _dot(d1, d1), _dot(d2, d2), _dot(d2, r)
    if a <= 1e-18 and e <= 1e-18:
        return _dist(p1, p2)
    if a <= 1e-18:
        s, t = 0.0, min(1.0, max(0.0, f / e))
    else:
        c = _dot(d1, r)
        if e <= 1e-18:
            s, t = min(1.0, max(0.0, -c / a)), 0.0
        else:
            b = _dot(d1, d2)
            denom = a * e - b * b
            s = min(1.0, max(0.0, (b * f - c * e) / denom)) if denom != 0 else 0.0
            t = (b * s + f) / e
            if t < 0:
                s, t = min(1.0, max(0.0, -c / a)), 0.0
            elif t > 1:
                s, t = min(1.0, max(0.0, (b - c) / a)), 1.0
    c1 = (p1[0] + s * d1[0], p1[1] + s * d1[1], p1[2] + s * d1[2])
    c2 = (p2[0] + t * d2[0], p2[1] + t * d2[1], p2[2] + t * d2[2])
    return _dist(c1, c2)


def _segment_distance(p, a, b):
    """Distance from a point to the segment ab, in 2D or 3D."""
    ab, ap = [b[k] - a[k] for k in range(len(a))], [p[k] - a[k] for k in range(len(a))]
    length2 = sum(c * c for c in ab)
    t = 0.0 if length2 == 0 else min(1.0, max(0.0, sum(x * y for x, y in zip(ab, ap)) / length2))
    return math.sqrt(sum((ap[k] - t * ab[k]) ** 2 for k in range(len(a))))


def _longest_edge(cell):
    return max(_dist(cell[0], cell[1]), _dist(cell[1], cell[2]), _dist(cell[2], cell[0]))


def _newell(points):
    n = [0.0, 0.0, 0.0]
    for a, b in zip(points, points[1:] + points[:1]):
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    return n


def _plane_basis(n):
    helper = (1.0, 0.0, 0.0) if abs(n[0]) < 0.9 else (0.0, 1.0, 0.0)
    u = _normalize(_cross(n, helper))
    return u, _cross(n, u)


def _rotate(q, v):
    """v rotated by the unit quaternion [x, y, z, w] (Hamilton, as Unity applies it)."""
    x, y, z, w = q
    tx, ty, tz = 2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0])
    return (v[0] + w * tx + (y * tz - z * ty), v[1] + w * ty + (z * tx - x * tz), v[2] + w * tz + (x * ty - y * tx))


def _cross2(a, b, c):
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def _inside2(p, a, b, c):
    return _cross2(a, b, p) >= 0 and _cross2(b, c, p) >= 0 and _cross2(c, a, p) >= 0


def _fmt(p):
    return "(" + ", ".join(f"{round(c, 4) + 0.0:g}" for c in p) + ")"


def _mid(a, b):
    return ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2)


def _centroid(a, b, c):
    return ((a[0] + b[0] + c[0]) / 3, (a[1] + b[1] + c[1]) / 3, (a[2] + b[2] + c[2]) / 3)


def _dist(a, b):
    return math.sqrt((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2)


def _add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _normalize(v):
    length = math.sqrt(_dot(v, v))
    return (v[0] / length, v[1] / length, v[2] / length) if length > 0 else (0.0, 0.0, 0.0)
