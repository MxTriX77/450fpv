"""Mesh geometry in asset space (lib.frame), built by generators with no Blender dependency."""
import math


class Mesh:
    """One LOD: vertices, polygon faces with an outward normal, per-corner UVs in metres and a material slot per
    face. Winding is not the generator's business: lib.export orients every face by its outward normal. The faces
    of a slot in `smooth` are shaded smooth: a corner takes the mean normal of the faces that share its vertex, so a
    tube of few sides shades as the round wood it stands for (p1-v01-locust task 1.2g); every other face is flat. A
    face given `normals` (one unit vector per corner, kept in `corner_normals` by face index) is shaded with those
    instead: a far card lit with the normals of the crown it stands for, not its plane's (p1-v01-locust task 1.2j).

    A vertex may carry a unit normal and a second UV (p1-l01-l03-pole-wires: a wire tube's radial normal and its
    radius, which the thin-wire shader reads). A face whose every vertex carries a normal is smooth-shaded with them;
    every other face stays flat. The second UV map is exported only when some vertex carries one, (0, 0) elsewhere.
    A mesh takes its normals per vertex or per face corner, not both (lib.export would apply the vertices' last)."""

    def __init__(self):
        self.vertices = []
        self.faces = []  # (indices, outward normal, uvs, slot)
        self.smooth = set()  # slots shaded smooth
        self.corner_normals = {}  # face index -> the corners' own shading normals
        self.normals = {}  # vertex index -> unit normal
        self.uv2 = {}  # vertex index -> second UV

    def vertex(self, p, normal=None, uv2=None):
        """Adds a vertex, with its own normal and second UV when given, and returns its index."""
        self.vertices.append((float(p[0]), float(p[1]), float(p[2])))
        index = len(self.vertices) - 1
        if normal is not None:
            self.normals[index] = tuple(float(c) for c in normal)
        if uv2 is not None:
            self.uv2[index] = tuple(float(c) for c in uv2)
        return index

    def face(self, indices, outward, slot, uvs=None, normals=None):
        """A convex or concave polygon given in cyclic order (see `ring` for convex ones), with its outward normal.
        Without `uvs`, every corner gets uv_upright; with `normals`, the corners are shaded with them."""
        indices = [int(i) for i in indices]
        if len(indices) < 3 or len(set(indices)) != len(indices):
            raise ValueError(f"a face needs at least 3 distinct vertices, got {indices}")
        if uvs is None:
            uvs = [uv_upright(self.vertices[i], outward) for i in indices]
        if normals is not None:
            if len(normals) != len(indices):
                raise ValueError(f"a face of {len(indices)} corners needs as many normals, got {len(normals)}")
            self.corner_normals[len(self.faces)] = [_normalize(n) for n in normals]
        self.faces.append((indices, tuple(float(c) for c in outward), [tuple(uv) for uv in uvs], slot))

    def bounds(self):
        """(min, max) of the vertices as two 3-tuples."""
        if not self.vertices:
            raise ValueError("an empty mesh has no bounds")
        return (tuple(min(v[a] for v in self.vertices) for a in range(3)),
                tuple(max(v[a] for v in self.vertices) for a in range(3)))

    def triangles(self):
        """Triangle count of a fan triangulation, which any triangulation of these polygons shares."""
        return sum(len(indices) - 2 for indices, _, _, _ in self.faces)

    def slots(self):
        return sorted({slot for _, _, _, slot in self.faces})


def uv_upright(p, normal):
    """The UV in metres of a point on a face with this outward normal (AST-07, knowledge.md section 14.4): the
    face's dominant axis is dropped and V runs up on anything vertical. Ties, as on chamfers and corners, go to Z,
    then X, then Y, so a bevel strip continues its side face's map."""
    ax, ay, az = (abs(c) for c in normal)
    if az >= ax and az >= ay:
        return (p[0], p[1])  # side faces: U east, V up
    if ax >= ay:
        return (p[2], p[1])  # end faces: U north, V up
    return (p[0], p[2])  # top and bottom: U east, V north


def ring(points, indices, normal):
    """The cyclic order of a convex face's vertices around their centroid, seen along `normal`, for Mesh.face."""
    n = _normalize(normal)
    helper = (1.0, 0.0, 0.0) if abs(n[0]) < 0.9 else (0.0, 1.0, 0.0)
    u = _normalize(_cross(n, helper))
    v = _cross(n, u)
    centre = tuple(sum(points[i][a] for i in indices) / len(indices) for a in range(3))

    def angle(i):
        d = tuple(points[i][a] - centre[a] for a in range(3))
        return math.atan2(_dot(d, v), _dot(d, u))

    return sorted(indices, key=angle)


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _normalize(v):
    length = math.sqrt(_dot(v, v))
    if length < 1e-12:
        raise ValueError("a zero normal")
    return (v[0] / length, v[1] / length, v[2] / length)
