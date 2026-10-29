"""Members as oriented, clipped boxes (p1-h01-adobe-house): the timbers, frames, boards and slabs of the `house`
generator, each one convex solid emitted into a lib.geometry.Mesh with its matching collision box. Plain Python in
asset space (lib.frame), no Blender.

A member lives in its own Frame: local x along it (the grain), y up out of it, z across it. Its solid is the local
box [x0, x1] x [y0, y1] x [z0, z1], clipped by half-spaces given in asset space (a plane through `point` keeping
the side its `normal` points away from), so a rafter ends on the ridge board's face, a jack on the hip rafter's side
and a batten on the hip line. Its collision box is the clipped solid's bounding box in the member's frame: where a
cut leaves the box standing past the solid, the part past it lies inside the member it was cut against, which the
audit counts as covered (lib.audit, "Primitives form a union").

UVs (AST-07): metres, V along local x so a wood set's grain runs along the member, U round the section (a face
across y takes local z, a face across z takes local y, an end takes z), plus the member's own `offset` so members
side by side do not repeat one another.
"""
import math

from lib import kits_vehicle

EPS = 1e-9
SLIVER_M2 = 1e-8   # a clipped face smaller than this is dropped


class Frame:
    """An orthonormal right-handed frame in asset space: `origin` and the unit axes ex, ey, ez = ex x ey (a rotation,
    so its quaternion exists; a left-handed set is refused)."""

    def __init__(self, origin, ex, ey, ez):
        self.origin = tuple(float(c) for c in origin)
        self.ex, self.ey, self.ez = (_unit(ex), _unit(ey), _unit(ez))
        if _dot(_cross(self.ex, self.ey), self.ez) < 1 - 1e-6 or abs(_dot(self.ex, self.ey)) > 1e-6:
            raise ValueError(f"a frame must be orthonormal and right-handed: {ex}, {ey}, {ez}")

    def point(self, local):
        x, y, z = local
        return tuple(self.origin[k] + x * self.ex[k] + y * self.ey[k] + z * self.ez[k] for k in range(3))

    def local(self, p):
        d = [p[k] - self.origin[k] for k in range(3)]
        return (_dot(d, self.ex), _dot(d, self.ey), _dot(d, self.ez))

    def vector(self, local):
        x, y, z = local
        return tuple(x * self.ex[k] + y * self.ey[k] + z * self.ez[k] for k in range(3))

    def quat(self):
        """The rotation from local axes to asset space as a unit quaternion [x, y, z, w]."""
        rows = tuple(tuple((self.ex, self.ey, self.ez)[j][i] for j in range(3)) for i in range(3))
        return list(kits_vehicle.Rigid(rows).quat((0.0, 0.0, 0.0, 1.0)))


def frame_along(p0, p1, up):
    """The frame at p0 with x towards p1, y the part of `up` square to it, and z across."""
    ex = _unit([p1[k] - p0[k] for k in range(3)])
    ey = _unit([up[k] - _dot(up, ex) * ex[k] for k in range(3)])
    return Frame(p0, ex, ey, _cross(ex, ey))


def plane(point, normal):
    """A clipping half-space: keeps the points p with (p - point) . normal <= 0."""
    return (tuple(float(c) for c in point), _unit(normal))


class Member:
    """One clipped box: its faces in asset space as (points, outward normal, kind) with kind "x-", "x+", "y-", "y+",
    "z-", "z+" for the box's own faces and "cut" for a face a plane made."""

    def __init__(self, frame, lo, hi, planes=()):
        self.frame = frame
        corners = {(i, j, k): frame.point((lo[0] if i < 0 else hi[0], lo[1] if j < 0 else hi[1],
                                           lo[2] if k < 0 else hi[2]))
                   for i in (-1, 1) for j in (-1, 1) for k in (-1, 1)}
        faces = []
        for axis, name in enumerate("xyz"):
            for sign in (-1, 1):
                keys = [c for c in corners if c[axis] == sign]
                o = [(axis + 1) % 3, (axis + 2) % 3]
                keys.sort(key=lambda c: math.atan2(c[o[1]], c[o[0]]))
                normal = frame.vector(tuple(float(sign) if a == axis else 0.0 for a in range(3)))
                faces.append(([corners[c] for c in keys], normal, f"{name}{'-' if sign < 0 else '+'}"))
        for point, normal in planes:
            faces = _clip_solid(faces, point, normal)
        self.faces = [f for f in faces if len(f[0]) >= 3 and _area(f[0]) > SLIVER_M2]

    def empty(self):
        return not self.faces

    def points(self):
        return [p for pts, _, _ in self.faces for p in pts]

    def local_bounds(self):
        local = [self.frame.local(p) for p in self.points()]
        return (tuple(min(p[k] for p in local) for k in range(3)), tuple(max(p[k] for p in local) for k in range(3)))

    def emit(self, mesh, slot, offset=(0.0, 0.0), slot_by_kind=None):
        """Adds the faces to `mesh` in `slot` (or slot_by_kind[kind]) with the grain UVs."""
        for pts, normal, kind in self.faces:
            ids = [mesh.vertex(p) for p in pts]
            uvs = [self.uv(p, kind, normal, offset) for p in pts]
            mesh.face(ids, normal, (slot_by_kind or {}).get(kind, slot), uvs)

    def uv(self, p, kind, normal, offset):
        x, y, z = self.frame.local(p)
        if kind == "cut":
            n = self.frame.local(tuple(self.frame.origin[k] + normal[k] for k in range(3)))
            kind = ("x", "y", "z")[max(range(3), key=lambda a: abs(n[a]))] + "+"
        if kind[0] == "y":
            return (z + offset[0], x + offset[1])
        if kind[0] == "z":
            return (y + offset[0], x + offset[1])
        return (z + offset[0], y + offset[1])

    def box(self, material, landable=False):
        """The collision box: the clipped solid's bounds in the member's frame."""
        lo, hi = self.local_bounds()
        centre = self.frame.point(tuple((lo[k] + hi[k]) / 2 for k in range(3)))
        return {"shape": "box", "center": list(centre), "size": [hi[k] - lo[k] for k in range(3)],
                "rotation": self.frame.quat(), "material": material, "landable": landable}


def member(mesh, frame, lo, hi, slot, material, planes=(), offset=(0.0, 0.0), slot_by_kind=None):
    """Emits a member into mesh (if given) and returns (Member, its collision box), or (None, None) when the planes
    leave nothing."""
    m = Member(frame, lo, hi, planes)
    if m.empty():
        return None, None
    if mesh is not None:
        m.emit(mesh, slot, offset, slot_by_kind)
    return m, m.box(material)


def axis_box(lo, hi, material, landable=False):
    """An unrotated collision box between the corners lo and hi."""
    return {"shape": "box", "center": [(lo[k] + hi[k]) / 2 for k in range(3)], "size": [hi[k] - lo[k] for k in range(3)],
            "rotation": [0.0, 0.0, 0.0, 1.0], "material": material, "landable": landable}


def _clip_solid(faces, point, normal):
    """A convex solid's faces clipped by the half-space (p - point) . normal <= 0, with the cap face it leaves."""
    out, cut = [], []
    for pts, n, kind in faces:
        clipped = []
        for a, b in zip(pts, pts[1:] + pts[:1]):
            da, db = _dot(_sub(a, point), normal), _dot(_sub(b, point), normal)
            if da <= EPS:
                clipped.append(a)
            if (da < -EPS and db > EPS) or (da > EPS and db < -EPS):
                t = da / (da - db)
                x = tuple(a[k] + t * (b[k] - a[k]) for k in range(3))
                clipped.append(x)
                cut.append(x)
            elif abs(da) <= EPS:
                cut.append(a)
        if len(clipped) >= 3:
            out.append((_dedupe(clipped), n, kind))
    cap = _dedupe(_order(cut, normal))
    if len(cap) >= 3:
        out.append((cap, tuple(normal), "cut"))
    return out


def _order(points, normal):
    if not points:
        return []
    unique = []
    for p in points:
        if all(math.dist(p, q) > 1e-7 for q in unique):
            unique.append(p)
    if len(unique) < 3:
        return unique
    centre = tuple(sum(p[k] for p in unique) / len(unique) for k in range(3))
    helper = (1.0, 0.0, 0.0) if abs(normal[0]) < 0.9 else (0.0, 1.0, 0.0)
    u = _unit(_cross(normal, helper))
    v = _cross(normal, u)
    return sorted(unique, key=lambda p: math.atan2(_dot(_sub(p, centre), v), _dot(_sub(p, centre), u)))


def _dedupe(points):
    out = []
    for p in points:
        if not out or math.dist(p, out[-1]) > 1e-7:
            out.append(p)
    while len(out) > 1 and math.dist(out[0], out[-1]) <= 1e-7:
        out.pop()
    return out


def _area(points):
    n = [0.0, 0.0, 0.0]
    for a, b in zip(points, points[1:] + points[:1]):
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    return math.sqrt(_dot(n, n)) / 2


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _unit(v):
    n = math.sqrt(_dot(v, v))
    return (v[0] / n, v[1] / n, v[2] / n)


cross, dot, unit = _cross, _dot, _unit
