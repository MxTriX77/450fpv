"""ground_patch: a square patch of ground surface, S04 belt_straw first (p1-s04-belt-straw, design decisions 1, 4, 6
and 8; object sheet §S04). The origin is the patch centre and y = 0 the mean top of the mat.

LOD0 is a closed slab SLAB_M deep: its top a grid of cell_m cells with seeded value-noise relief within +-relief_m
(the mean over the grid exactly 0) and round(mounds_per_m2 * area) earth mounds as raised vertices, triangulated so
the audit and the renderer see the same surface; four walls and a bottom close it. The cells under a mound are
refined REFINE x REFINE so the grid follows the mound sphere's steep rim within the audit's tolerance; the refined
vertices on an edge shared with a coarse cell lie on that edge (T-junctions, no cracks). Its UVs are metres, u = x
and v = z, which is how the belt_straw ground recipe lays its unique base map (tiling 1/size_m, offset 0.5). LOD1
is the same slab without the micro mesh, LOD2 the slab with a flat 2 x 2 top.

The straw instance kit (lib.straw) is baked as the visual-only micro mesh of LOD0 within bake_radius_m of the
centre: lying straws, standing stems (a seed_head_share of them headed) and belt twigs on a seeded jittered grid at
their densities, resting on the mat's local height, crosswise with a weak lodging bias (a `lodging.bias` share of
the lying straws within +-LODGING_SPREAD_DEG of `lodging.azimuth_deg`, degrees from +X towards +Z, the recipe's
convention). The standing stems grow in tussocks of stems_per_tuft within TUFT_R_M of a tuft's centre, each leaning
up to stem_tilt_deg away from it (H 3-9). Over the last bake_feather_m of the radius every kind thins to nothing
along a smoothstep, so the bake has no hard rim. The shared micro-detail generator replaces the bake at physics
parity later (AST-10).

A mound is a sphere cap on the slab: radius r = min(the sphere through the mound's rim and apex, the largest sphere
whose bottom stays inside the slab), centre sunk to y = h - r, its cap sampled on the grid; a low skirt (SKIRT_M)
spreads the foot to mound_w_m, and the relief blends back in over BLEND_M past the foot, so every vertex stays
within the audit's tolerance of the sphere or the slab top. A mound is bare earth (H 1-4): the top triangles inside
its foot take the SOIL_SLOT material. Collision (design 6): one box of the slab, landable, physics_material; one
sphere per mound, landable, mound_material; no gaps, wind volumes or snags (the straws' hooks are the micro-detail
generator's, AST-10).
"""
import math
import random

from lib.audit import TOLERANCE_M
from lib import straw
from lib.geometry import Mesh

SLOT = "ground"
SOIL_SLOT = "soil"       # a mound's bare earth
SLAB_M = 0.2            # the slab's depth, the collision box of design 6 (the mat is 5-20 cm deep)
REFINE = 4               # a cell under a mound is split this many times per side
NOISE_M = (1.0, 0.5)     # value-noise octaves of the relief: lattice spacings, the second at half amplitude (assumed)
SKIRT_M = 0.03           # a mound's spread foot, under the audit tolerance so it sits on the slab box (assumed)
BLEND_M = 0.1            # the relief returns over this ring past a mound's foot
MOUND_GAP_M = 0.5        # clear ground between mounds and from the patch edge
LODGING_SPREAD_DEG = 20  # a lodged straw lies within this of the azimuth
STRAW_LIFT_M = (-0.005, 0.03)  # a lying straw's centre above the mat: part buried to stacked (assumed)
TWIG_LIFT_M = (0.0, 0.01)
STEM_SINK_M = 0.02       # a stem's foot below the mat
TUFT_R_M = 0.15          # a tussock's foot radius (assumed from H 3-9: tussocks 0.2-0.4 m across)
STRAW_PITCH_DEG = 6.0    # extra pitch beyond the mat's slope between a straw's ends
MICRO_KIND = "straw"     # the micro mesh's kind: its count is the straw-kit instances baked

PARAMS = {
    "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 1,
             "effect": "every draw: the relief, the mounds, the kit and the scatter"},
    "size_m": {"type": "float", "range": (2.0, 50.0), "default": 12.0, "effect": "side of the square patch, centred"},
    "cell_m": {"type": "float", "range": (0.05, 1.0), "default": 0.25,
               "effect": "grid cell of the LOD0/LOD1 top; small enough to follow the mound spheres (check)"},
    "relief_m": {"type": "float", "range": (0.0, TOLERANCE_M), "default": 0.04,
                 "effect": "the mat's unevenness, +- about the mean top y = 0, within the audit's tolerance"},
    "bare.share": {"type": "float", "range": (0.0, 0.3), "default": 0.08,
                   "effect": "share of the patch under bare soil patches, measured on their step outlines; the ground "
                             "recipe draws its bare mask from the same layout (lib.patches)"},
    "bare.size_m.min": {"type": "float", "range": (0.3, 3.0), "default": 0.5,
                        "effect": "shortest bare patch along the lodging; at least the cell's diagonal"},
    "bare.size_m.max": {"type": "float", "range": (0.3, 3.0), "default": 2.0, "effect": "longest bare patch"},
    "bare.outline_vertices": {"type": "int", "range": (6, 12), "default": 8,
                              "effect": "corners of a bare patch's convex step outline; one mat edge box per edge"},
    "mounds_per_m2": {"type": "float", "range": (0.0, 1.0), "default": 0.02, "effect": "earth mounds per m2 of patch"},
    "mound_w_m.min": {"type": "float", "range": (0.1, 2.0), "default": 0.3, "effect": "narrowest mound foot"},
    "mound_w_m.max": {"type": "float", "range": (0.1, 2.0), "default": 0.5, "effect": "widest mound foot"},
    "mound_h_m.min": {"type": "float", "range": (0.02, 0.5), "default": 0.08, "effect": "lowest mound; above relief_m"},
    "mound_h_m.max": {"type": "float", "range": (0.02, 0.5), "default": 0.15, "effect": "tallest mound"},
    "bake_radius_m": {"type": "float", "range": (0.0, 25.0), "default": 4.0,
                      "effect": "the kit is baked within this of the centre; at most half the size"},
    "bake_feather_m": {"type": "float", "range": (0.0, 25.0), "default": 1.0,
                       "effect": "the scatter thins to nothing over this last ring of the bake radius (0: a hard rim)"},
    "straw_per_m2": {"type": "float", "range": (0.0, 1000.0), "default": 250.0, "effect": "lying straws per m2"},
    "stem_per_m2": {"type": "float", "range": (0.0, 500.0), "default": 60.0, "effect": "standing stems per m2"},
    "stems_per_tuft": {"type": "int", "range": (1, 50), "default": 10,
                       "effect": "standing stems per tussock, within TUFT_R_M of its centre (1: scattered singly)"},
    "stem_tilt_deg": {"type": "float", "range": (0.0, 80.0), "default": 60.0,
                      "effect": "largest lean of a standing stem from upright, away from its tuft's centre"},
    "twig_per_m2": {"type": "float", "range": (0.0, 50.0), "default": 2.0, "effect": "belt twigs per m2"},
    "lodging.azimuth_deg": {"type": "float", "range": (-360.0, 360.0), "default": 25.0,
                            "effect": "lodging direction, degrees from +X towards +Z; the ground recipe's"},
    "lodging.bias": {"type": "float", "range": (0.0, 1.0), "default": 0.3,
                     "effect": "share of lying straws aligned with the azimuth; the rest lie crosswise"},
    "straw_len_m.min": {"type": "float", "range": (0.05, 2.0), "default": 0.3, "effect": "shortest lying straw"},
    "straw_len_m.max": {"type": "float", "range": (0.05, 2.0), "default": 1.0, "effect": "longest lying straw"},
    "straw_d_mm.min": {"type": "float", "range": (0.5, 10.0), "default": 2.0, "effect": "thinnest straw or stem"},
    "straw_d_mm.max": {"type": "float", "range": (0.5, 10.0), "default": 5.0, "effect": "thickest straw or stem"},
    "straw_bend": {"type": "float", "range": (0.0, 1.0), "default": 0.3,
                   "effect": "curvature and kinks along a straw, 0 straight (lib.straw)"},
    "stem_h_m.min": {"type": "float", "range": (0.1, 2.0), "default": 0.5, "effect": "shortest standing stem"},
    "stem_h_m.max": {"type": "float", "range": (0.1, 2.0), "default": 1.2, "effect": "tallest standing stem"},
    "seed_head_share": {"type": "float", "range": (0.0, 1.0), "default": 0.35,
                        "effect": "share of standing stems with a seed head"},
    "twig_len_m.min": {"type": "float", "range": (0.05, 2.0), "default": 0.3, "effect": "shortest twig"},
    "twig_len_m.max": {"type": "float", "range": (0.05, 2.0), "default": 1.0, "effect": "longest twig"},
    "twig_d_mm.min": {"type": "float", "range": (1.0, 50.0), "default": 5.0, "effect": "thinnest twig"},
    "twig_d_mm.max": {"type": "float", "range": (1.0, 50.0), "default": 20.0, "effect": "thickest twig"},
    "variants": {"type": "int", "range": (4, 16), "default": 12, "effect": "distinct kit meshes per kind"},
    "segments": {"type": "int", "range": (2, 6), "default": 4, "effect": "tube segments per straw at LOD0"},
    "physics_material": {"type": "str", "range": "surface:<id> or an id of assets-src/physics_materials.json",
                         "default": "surface:belt_straw", "effect": "material of the slab box and both slots"},
    "mound_material": {"type": "str", "range": "surface:<id> or an id of assets-src/physics_materials.json",
                       "default": "surface:crater_spoil", "effect": "material of the mound spheres"},
    "lod.lod1_switch_m": {"type": "float", "range": (0.1, 100.0), "default": 8.0,
                          "effect": "distance from which LOD1, the slab without the straws, shows"},
    "lod.lod2_switch_m": {"type": "float", "range": (0.1, 500.0), "default": 60.0,
                          "effect": "distance from which LOD2, the flat slab, shows; beyond lod1_switch_m"},
    "lod.cull_m": {"type": "float", "range": (1.0, 2000.0), "default": 400.0,
                   "effect": "distance beyond which the patch is culled; beyond lod2_switch_m"},
}
RANGES = ("mound_w_m", "mound_h_m", "straw_len_m", "straw_d_mm", "stem_h_m", "twig_len_m", "twig_d_mm")


def check(params):
    """Cross-parameter rules once every key is in range: ordered ranges, the bake inside the patch, mounds above the
    relief and spheres the grid can follow within the audit's tolerance, mounds that fit the patch, ordered LODs."""
    problems = [f"'{key}.min' = {params[key]['min']} exceeds '{key}.max' = {params[key]['max']}"
                for key in RANGES if params[key]["min"] > params[key]["max"]]
    bare = params["bare"]["size_m"]
    if bare["min"] > bare["max"]:
        problems.append(f"'bare.size_m.min' = {bare['min']} exceeds 'bare.size_m.max' = {bare['max']}")
    if params["bake_radius_m"] > params["size_m"] / 2:
        problems.append(f"'bake_radius_m' = {params['bake_radius_m']} reaches beyond the patch; at most size_m / 2 = "
                        f"{params['size_m'] / 2:g}")
    if params["bake_feather_m"] > params["bake_radius_m"]:
        problems.append(f"'bake_feather_m' = {params['bake_feather_m']} exceeds 'bake_radius_m' = "
                        f"{params['bake_radius_m']}")
    w, h = params["mound_w_m"], params["mound_h_m"]
    if h["min"] <= params["relief_m"]:
        problems.append(f"'mound_h_m.min' = {h['min']} must rise above 'relief_m' = {params['relief_m']}, or the "
                        "mound vanishes in the relief")
    if w["max"] + 2 * (BLEND_M + MOUND_GAP_M) > params["size_m"]:
        problems.append(f"'mound_w_m.max' = {w['max']} does not fit the patch with its blend and gap")
    if params["mounds_per_m2"] > 0:
        # A sunk sphere is steep at its rim, so the refined grid misses it by up to its half-diagonal there, and a
        # chord between two grid points on the sphere sags below it; both must stay inside the audit's tolerance
        # (a bound: S04 at 0.25 m measures 34-35 mm against the 44 mm half-diagonal).
        radius = min(_mound_radius(w["min"], h["max"]), _mound_radius(w["max"], h["min"]))
        half_diagonal = params["cell_m"] / REFINE * math.sqrt(2) / 2
        if half_diagonal > TOLERANCE_M or _sagitta(radius, params["cell_m"] / REFINE) > TOLERANCE_M:
            problems.append(f"'cell_m' = {params['cell_m']} cannot follow the mound spheres (smallest radius {radius:.3f} m) "
                            f"within {TOLERANCE_M * 1000:g} mm, even refined {REFINE}x under the mounds; use a cell of at "
                            f"most {TOLERANCE_M * REFINE * math.sqrt(2):.3f} m")
    lod = params["lod"]
    if not lod["lod1_switch_m"] < lod["lod2_switch_m"] < lod["cull_m"]:
        problems.append("'lod.lod1_switch_m' < 'lod.lod2_switch_m' < 'lod.cull_m' must hold")
    return problems


def build(params):
    rng = random.Random(params["seed"])
    size = params["size_m"]
    n = max(1, round(size / params["cell_m"]))
    relief = _relief(rng, n, size, params["relief_m"])
    mounds = _mounds(rng, params)

    def height(x, z):
        y = relief(x, z)
        for m in mounds:
            d = math.hypot(x - m["x"], z - m["z"])
            if d < m["w"] / 2 + BLEND_M:
                return _mound_height(m, d, y)
        return y

    def slot(x, z):
        return SOIL_SLOT if any(math.hypot(x - m["x"], z - m["z"]) < m["w"] / 2 for m in mounds) else SLOT

    slab = _slab(n, size, height, _refined_cells(n, size, mounds), slot)
    micro = Mesh()
    count = _bake(rng, params, micro, height)
    lod = params["lod"]
    physics = params["physics_material"]
    return {
        "lods": [slab, slab, _slab(2, size, lambda x, z: 0.0, set(), lambda x, z: SLOT)],
        "micro": [{"lod": 0, "kind": MICRO_KIND, "count": count, "mesh": micro}],
        "switch_distances_m": [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"]],
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": SLOT, "physics": physics}, {"slot": SOIL_SLOT, "physics": params["mound_material"]},
                      {"slot": straw.SLOT, "physics": physics}],
        "collision": [{"shape": "box", "center": [0.0, -SLAB_M / 2, 0.0], "size": [size, SLAB_M, size],
                       "rotation": [0.0, 0.0, 0.0, 1.0], "material": physics, "landable": True}]
                     + [{"shape": "sphere", "center": [m["x"], m["h"] - m["r"], m["z"]], "radius": m["r"],
                         "material": params["mound_material"], "landable": True} for m in mounds],
        "gaps": [], "wind_volumes": [], "snag": [],
    }


# --- the slab -----------------------------------------------------------------------------------------------------

def _relief(rng, n, size, relief):
    """The relief as a function (x, z) -> height: seeded value noise (NOISE_M octaves, smoothstep-interpolated),
    shifted and scaled so that over the n x n grid's vertices the mean is exactly 0 and the largest magnitude
    exactly `relief` (0 everywhere when relief is 0). Its extremes sit on lattice nodes, which the grid samples
    when cell_m divides the lattice spacings."""
    lattices = []
    for spacing, amplitude in zip(NOISE_M, (1.0, 0.5)):
        count = int(math.ceil(size / spacing)) + 2
        lattices.append((spacing, amplitude, [[rng.uniform(-1, 1) for _ in range(count)] for _ in range(count)]))

    def noise(x, z):
        value = 0.0
        for spacing, amplitude, lattice in lattices:
            u, v = min(max((x + size / 2) / spacing, 0.0), count - 1.000001), min(max((z + size / 2) / spacing, 0.0), count - 1.000001)
            iu, iv = int(u), int(v)
            fu, fv = _smooth(u - iu), _smooth(v - iv)
            row0, row1 = lattice[iu], lattice[iu + 1]
            value += amplitude * ((row0[iv] * (1 - fu) + row1[iv] * fu) * (1 - fv)
                                  + (row0[iv + 1] * (1 - fu) + row1[iv + 1] * fu) * fv)
        return value

    samples = [noise(-size / 2 + i * size / n, -size / 2 + j * size / n) for i in range(n + 1) for j in range(n + 1)]
    mean = sum(samples) / len(samples)
    peak = max(abs(s - mean) for s in samples)
    scale = relief / peak if peak > 0 else 0.0
    return lambda x, z: (noise(x, z) - mean) * scale


def _smooth(t):
    return t * t * (3 - 2 * t)


def _mound_radius(w, h):
    """The mound sphere's radius: through the rim (w/2 at y = 0) and the apex (h), unless its bottom would leave
    the slab, then the largest sphere with its apex at h and its bottom on the slab's bottom."""
    return min((w * w / 4 + h * h) / (2 * h), (SLAB_M + h) / 2)


def _sagitta(radius, cell):
    """How far a cell diagonal's chord sags below a sphere of this radius."""
    c = cell * math.sqrt(2) / 2
    return radius - math.sqrt(radius * radius - c * c) if c < radius else radius


def _mounds(rng, params):
    """The mounds: seeded positions with MOUND_GAP_M of clear ground between them and from the edge, each with its
    foot width w, height h, sphere radius r and cap footprint radius a."""
    size = params["size_m"]
    count = round(params["mounds_per_m2"] * size * size)
    mounds = []
    for _ in range(count):
        w, h = rng.uniform(params["mound_w_m"]["min"], params["mound_w_m"]["max"]), \
            rng.uniform(params["mound_h_m"]["min"], params["mound_h_m"]["max"])
        r = _mound_radius(w, h)
        reach = w / 2 + BLEND_M
        for _ in range(1000):
            x, z = rng.uniform(-size / 2 + reach + MOUND_GAP_M, size / 2 - reach - MOUND_GAP_M), \
                rng.uniform(-size / 2 + reach + MOUND_GAP_M, size / 2 - reach - MOUND_GAP_M)
            if all(math.hypot(x - m["x"], z - m["z"]) >= reach + m["w"] / 2 + BLEND_M + MOUND_GAP_M for m in mounds):
                break
        else:
            raise ValueError(f"could not place mound {len(mounds) + 1} of {count} with {MOUND_GAP_M} m between mounds")
        mounds.append({"x": x, "z": z, "w": w, "h": h, "r": r, "a": math.sqrt(r * r - (h - r) ** 2)})
    return mounds


def _refined_cells(n, size, mounds):
    """The (i, j) grid cells any part of which lies under a mound's foot or blend ring."""
    cells = set()
    cell = size / n
    for m in mounds:
        outer = m["w"] / 2 + BLEND_M
        for i in range(max(0, int((m["x"] - outer + size / 2) / cell)), min(n, int((m["x"] + outer + size / 2) / cell) + 1)):
            for j in range(max(0, int((m["z"] - outer + size / 2) / cell)), min(n, int((m["z"] + outer + size / 2) / cell) + 1)):
                x0, z0 = -size / 2 + i * cell, -size / 2 + j * cell
                dx = max(x0 - m["x"], 0.0, m["x"] - x0 - cell)
                dz = max(z0 - m["z"], 0.0, m["z"] - z0 - cell)
                if math.hypot(dx, dz) < outer:
                    cells.add((i, j))
    return cells


def _mound_height(m, d, relief):
    """The mound's surface at distance d from its centre over this relief: max(cap, skirt) inside the cap, blended
    into the relief from the cap's rim to BLEND_M past the foot."""
    outer = m["w"] / 2 + BLEND_M
    cap = m["h"] - m["r"] + math.sqrt(max(0.0, m["r"] ** 2 - d * d)) if d <= m["a"] else 0.0
    skirt = SKIRT_M * (1 - (d / outer) ** 2) ** 2 if d < outer else 0.0
    f = _smooth(min(1.0, max(0.0, (d - m["a"]) / (outer - m["a"])))) if outer > m["a"] else 1.0
    return max(cap, skirt) * (1 - f) + relief * f


def _slab(n, size, height, refined, slot):
    """The closed slab: the top as an n x n grid of `height` samples, two triangles per cell (the diagonal
    alternating like a chequerboard, so no ridge runs across the patch), the `refined` cells split REFINE x REFINE
    with their vertices on an edge shared with a coarse cell interpolated along it, each top triangle in the
    material slot(x, z) of its centroid; four walls down to -SLAB_M and the bottom in SLOT; UVs u = x, v = z on the
    top and bottom and upright on the walls."""
    mesh = Mesh()
    cell, k = size / n, REFINE
    vertices = {}  # sub-grid coordinates (coarse * k + fine) -> vertex index

    def vertex(a, b, y=None):
        if (a, b) not in vertices:
            x, z = -size / 2 + a * cell / k, -size / 2 + b * cell / k
            vertices[(a, b)] = mesh.vertex((x, height(x, z) if y is None else y, z))
        return vertices[(a, b)]

    def triangles(i, j, a, b, c, d):
        for tri in ((a, b, c), (a, c, d)) if (i + j) % 2 == 0 else ((a, b, d), (b, c, d)):
            points = [mesh.vertices[v] for v in tri]
            centroid = (sum(p[0] for p in points) / 3, sum(p[2] for p in points) / 3)
            mesh.face(tri, _up_normal(mesh, tri), slot(*centroid), [(p[0], p[2]) for p in points])

    for i in range(n):
        for j in range(n):
            if (i, j) not in refined:
                triangles(i, j, vertex(i * k, j * k), vertex((i + 1) * k, j * k),
                          vertex((i + 1) * k, (j + 1) * k), vertex(i * k, (j + 1) * k))
                continue
            corners = {(di, dj): mesh.vertices[vertex((i + di) * k, (j + dj) * k)][1] for di in (0, 1) for dj in (0, 1)}
            for a in range(k + 1):
                for b in range(k + 1):
                    y = None  # the mound profile, unless the vertex lies on an edge shared with a coarse neighbour
                    if a in (0, k) and 0 < b < k and (i + (1 if a else -1), j) not in refined:
                        y = corners[(a // k, 0)] + (corners[(a // k, 1)] - corners[(a // k, 0)]) * b / k
                    elif b in (0, k) and 0 < a < k and (i, j + (1 if b else -1)) not in refined:
                        y = corners[(0, b // k)] + (corners[(1, b // k)] - corners[(0, b // k)]) * a / k
                    vertex(i * k + a, j * k + b, y)
            for a in range(k):
                for b in range(k):
                    triangles(a, b, vertex(i * k + a, j * k + b), vertex(i * k + a + 1, j * k + b),
                              vertex(i * k + a + 1, j * k + b + 1), vertex(i * k + a, j * k + b + 1))
    top = [[vertices[(i * k, j * k)] for j in range(n + 1)] for i in range(n + 1)]
    y = -SLAB_M
    bottom = {(sx, sz): mesh.vertex((sx * size / 2, y, sz * size / 2)) for sx in (-1, 1) for sz in (-1, 1)}
    for i in range(n):  # the walls at z = -size/2 and +size/2 run along i, those at x = -size/2 and +size/2 along j
        for (a, b), normal in (((top[i][0], top[i + 1][0]), (0.0, 0.0, -1.0)),
                               ((top[i][n], top[i + 1][n]), (0.0, 0.0, 1.0)),
                               ((top[0][i], top[0][i + 1]), (-1.0, 0.0, 0.0)),
                               ((top[n][i], top[n][i + 1]), (1.0, 0.0, 0.0))):
            pa, pb = mesh.vertices[a], mesh.vertices[b]
            la, lb = mesh.vertex((pa[0], y, pa[2])), mesh.vertex((pb[0], y, pb[2]))
            mesh.face([a, b, lb, la], normal, SLOT)
    base = [bottom[(-1, -1)], bottom[(1, -1)], bottom[(1, 1)], bottom[(-1, 1)]]
    mesh.face(base, (0.0, -1.0, 0.0), SLOT, [(mesh.vertices[v][0], mesh.vertices[v][2]) for v in base])
    return mesh


def _up_normal(mesh, tri):
    a, b, c = (mesh.vertices[i] for i in tri)
    u, v = (b[0] - a[0], b[1] - a[1], b[2] - a[2]), (c[0] - a[0], c[1] - a[1], c[2] - a[2])
    nx, ny, nz = u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]
    length = math.sqrt(nx * nx + ny * ny + nz * nz)
    return (nx / length, ny / length, nz / length) if ny > 0 else (-nx / length, -ny / length, -nz / length)


# --- the bake -----------------------------------------------------------------------------------------------------

def _bake(rng, params, micro, height):
    """Scatters the kit into `micro` within bake_radius_m and returns the instance count."""
    kit = straw.build_kit(params, rng)
    radius, feather = params["bake_radius_m"], params["bake_feather_m"]
    azimuth, bias = math.radians(params["lodging"]["azimuth_deg"]), params["lodging"]["bias"]
    spread = math.radians(LODGING_SPREAD_DEG)
    lengths = {kind: [m.bounds()[1][0] - m.bounds()[0][0] for m in meshes] for kind, meshes in kit.items()}
    count = 0

    def kept(x, z):
        """Whether a scatter point stays: all of them inside radius - feather, then fewer along a smoothstep to none
        at the radius."""
        r = math.hypot(x, z)
        if r > radius:
            return False
        return r <= radius - feather or rng.random() < _smooth((radius - r) / feather)

    def lying(kind, lift, x, z):
        k = rng.randrange(len(kit[kind]))
        yaw = azimuth + rng.uniform(-spread, spread) if rng.random() < bias else rng.uniform(0, 2 * math.pi)
        half = lengths[kind][k] / 2
        dx, dz = math.cos(yaw) * half, math.sin(yaw) * half
        y0, y1 = height(x - dx, z - dz), height(x + dx, z + dz)
        pitch = math.atan2(y1 - y0, 2 * half) + math.radians(rng.uniform(-STRAW_PITCH_DEG, STRAW_PITCH_DEG))
        origin = (x, (y0 + y1) / 2 + rng.uniform(*lift), z)
        straw.place(micro, kit[kind][k], origin, straw.lying_axes(yaw, pitch, rng.uniform(0, 2 * math.pi)))

    def tuft(x, z):
        """A tussock: stems_per_tuft stems within TUFT_R_M of (x, z), each leaning away from the centre."""
        tilt_max = math.radians(params["stem_tilt_deg"])
        for _ in range(params["stems_per_tuft"]):
            kind = "stem_head" if rng.random() < params["seed_head_share"] else "stem"
            k = rng.randrange(len(kit[kind]))
            bearing, d = rng.uniform(0, 2 * math.pi), TUFT_R_M * math.sqrt(rng.random())
            sx, sz = x + d * math.cos(bearing), z + d * math.sin(bearing)
            origin = (sx, height(sx, sz) - STEM_SINK_M, sz)
            tilt = tilt_max * rng.uniform(0.25, 1.0)
            straw.place(micro, kit[kind][k], origin, straw.standing_axes(bearing + rng.uniform(-0.5, 0.5), tilt))

    for density, per, place in ((params["straw_per_m2"], 1, lambda x, z: lying("straw", STRAW_LIFT_M, x, z)),
                                (params["stem_per_m2"], params["stems_per_tuft"], tuft),
                                (params["twig_per_m2"], 1, lambda x, z: lying("twig", TWIG_LIFT_M, x, z))):
        if density <= 0 or radius <= 0:
            continue
        s = math.sqrt(per / density)
        k = int(math.ceil(radius / s))
        for i in range(-k, k):
            for j in range(-k, k):
                x, z = (i + rng.random()) * s, (j + rng.random()) * s
                if kept(x, z):
                    place(x, z)
                    count += per
    return count
