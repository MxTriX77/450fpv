"""debris_brick: one clay brick, a box with chamfered edges and slightly worn corners (object sheet K01, AST-04).

Params (assets-src/debris/<variant>.json) are PARAMS below (lib.params checks them): the origin is the bottom
centre, so the brick lies flat on y = 0; every corner draws its own chamfer width from bevel_mm, the wear.

LOD0 is the chamfered box: 24 vertices, 6 face quads, 12 chamfer strips and 8 corner triangles, 44 triangles. LOD1
is the plain box, 12 triangles. Collision is the plain box, within the chamfer width of LOD0 everywhere.
The LOD1 distance is where a 2-3 mm chamfer drops under one pixel of the feed (about 935 px/rad, design decision 7),
and the cull distance where the long edge drops under two.

UVs (AST-07): every face maps into one clean brick of the wall texture, CELL_M, so a lone brick shows one brick's
surface and no mortar joint crosses a face (the phase-0 review saw joints across the bed face when the 1.4 m wall
tile was laid over a 0.25 m brick). Each face keeps its upright metre UV (lib.geometry.uv_upright, so a chamfer strip
or corner continues its side face's map), placed into the cell at the largest uniform scale that fits the whole face,
centred: the stretcher faces on the long sides, the header faces on the ends, the bed faces on top and bottom.
"""
import random

from lib.geometry import Mesh, ring, uv_upright

SLOT = "brick"
CORNERS = [(sx, sy, sz) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
# The brick cell: pixel columns 388-532 and rows 423-452 (inclusive, rows from the top) of polyhaven/red_brick's 1k
# diffuse map, the plateau of one uniform mid-red stretcher inset 3 px from its mortar joints, measured on the map.
# CELL_M places it in the UV's metres, the set's 1.4 m tile (recipe brick_red, tile_size_m), V up. The photo's bricks
# are about 205 x 49 mm (a Dutch format), so a 250 x 65 mm face fits at 0.63 and a 250 x 120 mm bed face at 0.34 of
# real scale. Not a param: the cell belongs to the texture set, and becomes a table keyed by the object sheet's
# brick_type when the silicate and perforated variants bring their own sets.
CELL_PX = ((388, 423), (532, 452))
SET_PX, SET_M = 1024, 1.4


def _cell_m(cell_px, set_px=SET_PX, set_m=SET_M):
    """A pixel box of the set's map as (min, max) UV corners in metres: row 0 is the top of the map, V runs up."""
    (x0, y0), (x1, y1) = cell_px
    return ((x0 / set_px * set_m, (1 - (y1 + 1) / set_px) * set_m),
            ((x1 + 1) / set_px * set_m, (1 - y0 / set_px) * set_m))


CELL_M = _cell_m(CELL_PX)
# The object sheet's generator table (docs/object-sheets, "Generator parameters"); defaults are K01_whole. The size
# range spans a chip to a block (assumed); the chamfer range holds the 2-3 mm wear of a fired brick with room for a
# battered one, and never more than half the smallest size (check below) so the chamfers cannot meet.
PARAMS = {
    "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 1,
             "effect": "draws each corner's chamfer width within bevel_mm"},
    "size_m.length": {"type": "float", "range": (0.02, 1.0), "default": 0.25, "effect": "extent along +X"},
    "size_m.width": {"type": "float", "range": (0.02, 1.0), "default": 0.12, "effect": "extent along +Z"},
    "size_m.height": {"type": "float", "range": (0.02, 1.0), "default": 0.065,
                      "effect": "extent along +Y, from y = 0"},
    "bevel_mm.min": {"type": "float", "range": (0.0, 10.0), "default": 2.0,
                     "effect": "narrowest chamfer a corner may draw"},
    "bevel_mm.max": {"type": "float", "range": (0.0, 10.0), "default": 3.0,
                     "effect": "widest chamfer a corner may draw; under half the smallest size"},
    "physics_material": {"type": "str", "range": "an id of assets-src/physics_materials.json", "default": "masonry",
                         "effect": "collision material of the box and the brick slot (AST-05)"},
    "lod.lod1_switch_m": {"type": "float", "range": (0.1, 100.0), "default": 3.0,
                          "effect": "distance from which LOD1, the plain box, shows"},
    "lod.cull_m": {"type": "float", "range": (1.0, 2000.0), "default": 120.0,
                   "effect": "distance beyond which the brick is culled; beyond lod1_switch_m"},
}


def check(params):
    """Cross-parameter rules once every key is in range: the chamfer range is ordered and fits the brick."""
    bevel, limit = params["bevel_mm"], min(params["size_m"].values()) * 500  # half the smallest size, in mm
    problems = []
    if bevel["min"] > bevel["max"]:
        problems.append(f"'bevel_mm.min' = {bevel['min']} exceeds 'bevel_mm.max' = {bevel['max']}")
    if bevel["max"] >= limit:
        problems.append(f"'bevel_mm.max' = {bevel['max']} must stay under half the smallest size, {limit:g} mm")
    return problems


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


def _cell_uv(p, normal, lo, hi):
    """The UV of p on a face with this outward normal: its upright metre UV (uv_upright drops the dominant axis,
    ties to the side face on chamfers and corners) placed into CELL_M at the largest uniform scale that fits the
    whole face, centred. lo and hi are the brick's bounds corners, so a face's extent is the box's along its axes."""
    raw, a, b = uv_upright(p, normal), uv_upright(lo, normal), uv_upright(hi, normal)
    (u0, v0), (u1, v1) = CELL_M
    scale = min((u1 - u0) / (b[0] - a[0]), (v1 - v0) / (b[1] - a[1]))
    return (u0 + (u1 - u0 - (b[0] - a[0]) * scale) / 2 + (raw[0] - a[0]) * scale,
            v0 + (v1 - v0 - (b[1] - a[1]) * scale) / 2 + (raw[1] - a[1]) * scale)


def _face(mesh, indices, normal, lo, hi):
    """A convex face in ring order with its cell UVs."""
    order = ring(mesh.vertices, indices, normal)
    mesh.face(order, normal, SLOT, [_cell_uv(mesh.vertices[i], normal, lo, hi) for i in order])


def _chamfered_box(hx, height, hz, chamfer):
    """Every corner becomes three vertices, one on each face meeting there, pulled in along the two other axes by
    that corner's chamfer width: p[c, axis] lies on the face normal to `axis`."""
    mesh = Mesh()
    lo, hi = (-hx, 0.0, -hz), (hx, height, hz)
    p = {}
    for c, w in chamfer.items():
        x, y, z = _corner(c, hx, height, hz)
        p[c, 0] = mesh.vertex((x, y - c[1] * w, z - c[2] * w))
        p[c, 1] = mesh.vertex((x - c[0] * w, y, z - c[2] * w))
        p[c, 2] = mesh.vertex((x - c[0] * w, y - c[1] * w, z))
    for axis in range(3):  # the six faces
        for side in (-1, 1):
            normal = tuple(side if a == axis else 0 for a in range(3))
            _face(mesh, [p[c, axis] for c in CORNERS if c[axis] == side], normal, lo, hi)
    for axis in range(3):  # the twelve chamfer strips, one per edge along `axis`
        others = [a for a in range(3) if a != axis]
        for s0 in (-1, 1):
            for s1 in (-1, 1):
                normal = [0, 0, 0]
                normal[others[0]], normal[others[1]] = s0, s1
                ends = [c for c in CORNERS if c[others[0]] == s0 and c[others[1]] == s1]
                _face(mesh, [p[c, a] for c in ends for a in others], normal, lo, hi)
    for c in CORNERS:  # the eight corner triangles
        _face(mesh, [p[c, a] for a in range(3)], c, lo, hi)
    return mesh


def _box(hx, height, hz):
    mesh = Mesh()
    lo, hi = (-hx, 0.0, -hz), (hx, height, hz)
    p = {c: mesh.vertex(_corner(c, hx, height, hz)) for c in CORNERS}
    for axis in range(3):
        for side in (-1, 1):
            normal = tuple(side if a == axis else 0 for a in range(3))
            _face(mesh, [p[c] for c in CORNERS if c[axis] == side], normal, lo, hi)
    return mesh
