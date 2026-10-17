"""debris_brick: one clay brick, a box with chamfered edges and slightly worn corners (object sheet K01, AST-04).

Params (assets-src/debris/<variant>.json), metres unless the key says otherwise:
  seed              drives the wear
  size_m            {"length", "width", "height"}: length along +X, height along +Y, width along +Z; the origin is
                    the bottom centre, so the brick lies flat on y = 0
  bevel_mm          {"min", "max"}: every corner draws its own chamfer width from this range, the wear
  physics_material  the collision material id (AST-05); "masonry" for fired clay
  lod               {"lod1_switch_m", "cull_m"}: LOD1, a plain box, from this distance; culled beyond cull_m

LOD0 is the chamfered box: 24 vertices, 6 face quads, 12 chamfer strips and 8 corner triangles, 44 triangles. LOD1
is the plain box, 12 triangles. Collision is the plain box, within the chamfer width of LOD0 everywhere.
The LOD1 distance is where a 2-3 mm chamfer drops under one pixel of the feed (about 935 px/rad, design decision 7),
and the cull distance where the long edge drops under two.
"""
import random

from lib.geometry import Mesh, ring

SLOT = "brick"
CORNERS = [(sx, sy, sz) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]


def build(params):
    size = params["size_m"]
    hx, height, hz = size["length"] / 2, size["height"], size["width"] / 2
    bevel = params["bevel_mm"]
    rng = random.Random(params["seed"])
    chamfer = {c: rng.uniform(bevel["min"], bevel["max"]) / 1000 for c in CORNERS}
    lod = params["lod"]
    physics = params["physics_material"]
    return {
        "lods": [_chamfered_box(hx, height, hz, chamfer), _box(hx, height, hz)],
        "switch_distances_m": [0.0, lod["lod1_switch_m"]],
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": SLOT, "physics": physics}],
        "collision": [{"shape": "box", "center": [0.0, height / 2, 0.0], "size": [2 * hx, height, 2 * hz],
                       "rotation": [0.0, 0.0, 0.0, 1.0], "material": physics, "landable": False}],
        "gaps": [], "wind_volumes": [], "snag": [],
    }


def _corner(c, hx, height, hz):
    return (c[0] * hx, height if c[1] > 0 else 0.0, c[2] * hz)


def _chamfered_box(hx, height, hz, chamfer):
    """Every corner becomes three vertices, one on each face meeting there, pulled in along the two other axes by
    that corner's chamfer width: p[c, axis] lies on the face normal to `axis`."""
    mesh = Mesh()
    p = {}
    for c, w in chamfer.items():
        x, y, z = _corner(c, hx, height, hz)
        p[c, 0] = mesh.vertex((x, y - c[1] * w, z - c[2] * w))
        p[c, 1] = mesh.vertex((x - c[0] * w, y, z - c[2] * w))
        p[c, 2] = mesh.vertex((x - c[0] * w, y - c[1] * w, z))
    for axis in range(3):  # the six faces
        for side in (-1, 1):
            normal = tuple(side if a == axis else 0 for a in range(3))
            indices = [p[c, axis] for c in CORNERS if c[axis] == side]
            mesh.face(ring(mesh.vertices, indices, normal), normal, SLOT)
    for axis in range(3):  # the twelve chamfer strips, one per edge along `axis`
        others = [a for a in range(3) if a != axis]
        for s0 in (-1, 1):
            for s1 in (-1, 1):
                normal = [0, 0, 0]
                normal[others[0]], normal[others[1]] = s0, s1
                ends = [c for c in CORNERS if c[others[0]] == s0 and c[others[1]] == s1]
                indices = [p[c, a] for c in ends for a in others]
                mesh.face(ring(mesh.vertices, indices, normal), normal, SLOT)
    for c in CORNERS:  # the eight corner triangles
        mesh.face(ring(mesh.vertices, [p[c, a] for a in range(3)], c), c, SLOT)
    return mesh


def _box(hx, height, hz):
    mesh = Mesh()
    p = {c: mesh.vertex(_corner(c, hx, height, hz)) for c in CORNERS}
    for axis in range(3):
        for side in (-1, 1):
            normal = tuple(side if a == axis else 0 for a in range(3))
            indices = [p[c] for c in CORNERS if c[axis] == side]
            mesh.face(ring(mesh.vertices, indices, normal), normal, SLOT)
    return mesh
