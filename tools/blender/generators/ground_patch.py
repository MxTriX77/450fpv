"""ground_patch: a square patch of ground surface, S04 belt_straw first (p1-s04-belt-straw, design decisions 1, 4, 6
and 8; review-physics PR1-PR8; object sheet §S04). The origin is the patch centre and y = 0 the mean top of the mat.

LOD0 is a closed slab mat_depth_m + SOIL_M deep: its top a grid of cell_m cells with the mat's tussock relief within
+-relief_m (lib.mat, the field the ground recipe darkens its hollows by; the mean over the grid exactly 0) and
round(mounds_per_m2 * area) earth mounds as raised vertices, triangulated so the audit and the renderer see the same
surface; four walls and a bottom close it. The cells under
a mound are refined REFINE x REFINE so the grid follows the mound sphere's steep rim within the audit's tolerance;
the refined vertices on an edge shared with a coarse cell lie on that edge (T-junctions, no cracks). Its UVs are
metres, u = x and v = z, which is how the belt_straw ground recipe lays its unique base map (tiling 1/size_m, offset
0.5). LOD1 is the same slab without the micro mesh, LOD2 the slab with a flat 2 x 2 top.

The bare soil patches (lib.patches, the layout tools/assets/derive.py stamps the bare mask from, so the map and the
mesh never disagree) are where the mat is missing: over each patch's convex step outline the top drops vertically
by the mat depth D to a floor that is the mat surface lowered by D, relief included (PR3). A cell the outline
crosses is cut exactly along it: the part inside is floor, the part outside mat (ear-clipped), and one vertical
wall quad stands on every piece of outline edge, all sharing their vertices with the neighbouring cells; a cell
wholly inside is floor. The floor takes the ground slot. A wall is the mat's cut edge: it takes the straw slot, UV'd
up the litter's palette column (the mat's dark depth, the colour the ground recipe gives the gaps between its
straws) from WALL_V[0] at the floor to WALL_V[1] at the mat's top.

The straw instance kit (lib.straw) is baked as the visual-only micro mesh of LOD0 within bake_radius_m of the
centre, on seeded jittered grids at the kinds' densities, resting on the stepped surface (review-physics N8):
- lying straws: straw_per_m2 in the mat's top layer and body_straw_per_m2 below them, visual only (the mat's body,
  which physics carries as the straw row's compliance, not as hooks). Each rests across the tangle: its two ends at
  depths drawn from its layer's span (TOP_LAYER, BODY_LAYER) of the local loft, loft_m on the relief's crowns and
  loft_m * (1 - clump) in its hollows, so straws lean on straws, never above a MAT_MAX_M mat; deeper straws are
  shaded darker (DEPTH_SHADE). Both layers gather on the crowns: kept with probability (1 + clump c) / (1 + clump)
  of a grid (1 + clump) times denser, c the relief over relief_m. Crosswise, with a `lodging.bias` share within
  +-LODGING_SPREAD_DEG of the local swathe direction, `lodging.azimuth_deg` (degrees from +X towards +Z, the
  recipe's convention) turned by up to SWATHE_DEG along a SWATHE_M value noise, so near straws lie in swathes (H).
- over a green strip the mat thins: a lying straw is kept with probability 1 - GREEN_THIN times the strips' weight.
- over a floor, only the straws strewn on the soil: FLOOR_KEEP[1] of them at the wall, FLOOR_KEEP[0] deeper than
  the bare feather, lying flat on it; a mat straw whose end reaches over a floor within LIP_OVERHANG_M of the
  outline holds that end at the mat's top, overhanging the step: a ragged lip (H 9-14). Besides the scatter,
  LIP_STRAWS_PER_M straws per metre of outline lie across it within LIP_SPREAD_DEG of square, centred on the mat
  within LIP_OVERHANG_M of it: the short ones overhang the step, the long ones drape down to the floor.
- standing stems in tussocks of stems_per_tuft within TUFT_R_M of a tuft's centre, each leaning up to stem_tilt_deg
  away from it (H 3-9), the tufts gathered on the crowns like the straws and none on a floor; a seed_head_share of
  them headed.
- belt twigs, resting across the tangle like the straws (TWIG_LAYER).
- green regrowth blades over the green strips (lib.mat, the strips the recipe tints): blade_per_m2 in tufts of
  BLADE_TUFT within BLADE_TUFT_R_M, kept by the strips' weight, so they thin over the strips' feather, none on a
  floor (H 16, H 19-21).
Over the last bake_feather_m of the radius every kind thins to nothing along a smoothstep, so the bake has no hard
rim. The shared micro-detail generator replaces the bake at physics parity later (AST-10).

A mound is a sphere cap on the slab: radius r = min(the sphere through the mound's rim and apex, the largest sphere
whose bottom stays inside the slab), centre sunk to y = h - r, its cap sampled on the grid; a low skirt (SKIRT_M)
spreads the foot to mound_w_m, and the relief blends back in over BLEND_M past the foot, so every vertex stays
within the audit's tolerance of the sphere or the slab top. A mound keeps MOUND_GAP_M from every other mound, the
patch edge and every bare patch's window, so it stands on the mat. A mound is bare earth (H 1-4): the top
triangles inside its foot take the SOIL_SLOT material.

Collision (design 6, review-physics §3), every shape landable, in this order (PR8), so a logged (object, shape)
reads as floor, mat or mound:
- collision[0], the soil: one box over the whole patch, physics_material (surface:belt_straw), its top one mat
  depth below the mat top and SOIL_M deep (F-5 behind the floors' top); the floors show it;
- the fill boxes: the mat, mat_material (straw), from -D to 0 standing on the soil box, axis-aligned over the patch
  minus every bare patch's window (lib.patches.fill);
- each bare patch's edge boxes in patch order, one per outline edge from its first corner: the mat around the
  patch, mat_material, open exactly over the outline (lib.patches; the window is checked covered every 2 cm);
- the mound spheres, mound_material.
No gaps, wind volumes or snags (the straws' hooks are the micro-detail generator's, AST-10).
"""
import math
import random

from lib.audit import TOLERANCE_M
from lib import mat, patches, straw
from lib.geometry import Mesh

SLOT = "ground"
SOIL_SLOT = "soil"       # a mound's bare earth
SOIL_M = 0.3             # the soil box below the mat: F-5 behind the floors' top (carried; review-physics PR1)
REFINE = 4               # a cell under a mound is split this many times per side
SKIRT_M = 0.03           # a mound's spread foot, under the audit tolerance so it sits on the slab box (assumed)
BLEND_M = 0.1            # the relief returns over this ring past a mound's foot
MOUND_GAP_M = 0.5        # clear ground between mounds, from the patch edge and from every bare patch's window
WALL_V = (0.08, 0.9)     # a step wall's v up the litter's palette column, at the floor and at the mat's top
LODGING_SPREAD_DEG = 20  # a lodged straw lies within this of the local swathe direction
SWATHE_M = 1.2           # the swathes' value-noise lattice (assumed from H 12-21: near straws parallel over a metre)
SWATHE_DEG = 45.0        # the swathe direction turns by up to this from the lodging azimuth (assumed)
TOP_LAYER = (0.35, 1.0)  # the top layer's straw ends rest between these shares of the local loft
BODY_LAYER = (0.0, 0.5)  # the body straws' ends, deeper
TWIG_LAYER = (0.2, 0.8)
STRAW_SINK_M = 0.01      # a straw end at depth 0 lies this far into the mat's top: partly buried (assumed)
DEPTH_SHADE = 0.8        # an instance's palette v shifts by this times its depth share - 0.5: deep straws darker (assumed)
MAT_MAX_M = 0.20         # the sheet's deepest mat (5-20 cm): no straw rests higher above the soil
FLOOR_KEEP = (0.08, 0.5)  # share of straws kept over a floor, deeper than the bare feather and at its wall (assumed, H 9-14)
LIP_OVERHANG_M = 0.15    # a mat straw's end overhangs a step at the mat's top this far at most (assumed, H 9-14)
LIP_STRAWS_PER_M = 30.0  # straws laid across a step's outline from the mat per metre of it (assumed, H 9-14)
LIP_SPREAD_DEG = 35.0    # a lip straw lies within this of square across the outline (assumed)
STEM_SINK_M = 0.02       # a stem's foot below the mat
TUFT_R_M = 0.15          # a tussock's foot radius (assumed from H 3-9: tussocks 0.2-0.4 m across)
BLADE_TUFT = 5           # regrowth blades per tuft (assumed, H 19-21)
BLADE_TUFT_R_M = 0.03    # a blade tuft's foot radius (assumed)
BLADE_TILT_DEG = 40.0    # a blade leans up to this from upright (assumed)
GREEN_THIN = 0.5         # the mat's straws thin by up to this over a green strip: regrowth comes up where the mat is
                         # thin (assumed, H 19-21)
STRAW_PITCH_DEG = 3.0    # extra pitch beyond the line between a straw's two resting ends
MICRO_KIND = "straw"     # the micro mesh's kind: its count is the straw-kit instances baked
IDENTITY = [0.0, 0.0, 0.0, 1.0]

PARAMS = {
    "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 7,
             "effect": "every draw: the relief, the bare patches, the green strips, the mounds, the kit and the "
                     "scatter"},
    "size_m": {"type": "float", "range": (2.0, 50.0), "default": 12.0, "effect": "side of the square patch, centred"},
    "cell_m": {"type": "float", "range": (0.05, 1.0), "default": 0.125,
               "effect": "grid cell of the LOD0/LOD1 top; small enough to follow the mound spheres (check)"},
    "relief_m": {"type": "float", "range": (0.0, TOLERANCE_M), "default": 0.04,
                 "effect": "the mat's tussock relief, +- about the mean top y = 0, within the audit's tolerance; the "
                           "ground recipe darkens its hollows (lib.mat)"},
    "mat_depth_m": {"type": "float", "range": (0.05, 0.2), "default": 0.125,
                    "effect": "the mat's depth D (sheet 5-20 cm): the mat boxes stand from -D to 0 on the soil box and "
                              "a bare patch's floor lies D below the mat; straw's 490 N/m in physics_materials.json is "
                              "derived at 0.125 m, so another depth needs a physics re-derivation"},
    "bare.share": {"type": "float", "range": (0.0, 0.3), "default": 0.08,
                   "effect": "share of the patch under bare soil patches, measured on their step outlines; the ground "
                             "recipe draws its bare mask from the same layout (lib.patches)"},
    "bare.size_m.min": {"type": "float", "range": (0.3, 3.0), "default": 0.5,
                        "effect": "shortest bare patch along the lodging; at least the cell's diagonal"},
    "bare.size_m.max": {"type": "float", "range": (0.3, 3.0), "default": 2.0, "effect": "longest bare patch"},
    "bare.outline_vertices": {"type": "int", "range": (6, 12), "default": 12,
                              "effect": "corners of a bare patch's convex step outline; one mat edge box per edge"},
    "green.share": {"type": "float", "range": (0.0, 0.5), "default": 0.15,
                    "effect": "share of the patch under green regrowth strips, outside the bare patches; the ground "
                              "recipe tints the same strips and the blades grow over them (lib.mat)"},
    "green.width_m.min": {"type": "float", "range": (0.3, 6.0), "default": 1.0, "effect": "narrowest green strip"},
    "green.width_m.max": {"type": "float", "range": (0.3, 6.0), "default": 3.0, "effect": "widest green strip"},
    "green.length_m.min": {"type": "float", "range": (0.3, 20.0), "default": 3.0,
                           "effect": "shortest green strip, along the lodging"},
    "green.length_m.max": {"type": "float", "range": (0.3, 20.0), "default": 12.0, "effect": "longest green strip"},
    "mounds_per_m2": {"type": "float", "range": (0.0, 1.0), "default": 0.02, "effect": "earth mounds per m2 of patch"},
    "mound_w_m.min": {"type": "float", "range": (0.1, 2.0), "default": 0.3, "effect": "narrowest mound foot"},
    "mound_w_m.max": {"type": "float", "range": (0.1, 2.0), "default": 0.5, "effect": "widest mound foot"},
    "mound_h_m.min": {"type": "float", "range": (0.02, 0.5), "default": 0.08, "effect": "lowest mound; above relief_m"},
    "mound_h_m.max": {"type": "float", "range": (0.02, 0.5), "default": 0.15, "effect": "tallest mound"},
    "bake_radius_m": {"type": "float", "range": (0.0, 25.0), "default": 4.0,
                      "effect": "the kit is baked within this of the centre; at most half the size"},
    "bake_feather_m": {"type": "float", "range": (0.0, 25.0), "default": 1.0,
                       "effect": "the scatter thins to nothing over this last ring of the bake radius (0: a hard rim)"},
    "straw_per_m2": {"type": "float", "range": (0.0, 1000.0), "default": 250.0,
                     "effect": "lying straws per m2 in the mat's top layer (the hooking straws of the sheet's cover row)"},
    "body_straw_per_m2": {"type": "float", "range": (0.0, 1000.0), "default": 90.0,
                          "effect": "visual lying straws per m2 below the top layer: the mat's body, which physics "
                                    "carries as the straw row's compliance, not as hooks"},
    "loft_m": {"type": "float", "range": (0.0, 0.15), "default": 0.06,
               "effect": "how high the lying straws stack above the mat's top on the relief's crowns (loft_m * "
                         "(1 - clump) in its hollows), never above a MAT_MAX_M mat; visual only"},
    "clump": {"type": "float", "range": (0.0, 1.0), "default": 0.6,
              "effect": "how much the straws, the loft and the tussocks gather on the relief's crowns (0: even)"},
    "blade_per_m2": {"type": "float", "range": (0.0, 5000.0), "default": 800.0,
                     "effect": "green regrowth blades per m2 inside the green strips, thinning over their feather"},
    "blade_h_m.min": {"type": "float", "range": (0.02, 0.5), "default": 0.05, "effect": "shortest regrowth blade"},
    "blade_h_m.max": {"type": "float", "range": (0.02, 0.5), "default": 0.15, "effect": "tallest regrowth blade"},
    "stem_per_m2": {"type": "float", "range": (0.0, 500.0), "default": 60.0, "effect": "standing stems per m2"},
    "stems_per_tuft": {"type": "int", "range": (1, 50), "default": 10,
                       "effect": "standing stems per tussock, within TUFT_R_M of its centre (1: scattered singly)"},
    "stem_tilt_deg": {"type": "float", "range": (0.0, 80.0), "default": 60.0,
                      "effect": "largest lean of a standing stem from upright, away from its tuft's centre"},
    "twig_per_m2": {"type": "float", "range": (0.0, 50.0), "default": 2.0, "effect": "belt twigs per m2"},
    "lodging.azimuth_deg": {"type": "float", "range": (-360.0, 360.0), "default": 25.0,
                            "effect": "lodging direction, degrees from +X towards +Z; the ground recipe's"},
    "lodging.bias": {"type": "float", "range": (0.0, 1.0), "default": 0.3,
                     "effect": "share of lying straws aligned with the local swathe direction about the azimuth, "
                               "the rest crosswise; bare patches and tussocks are 1 + bias longer along it"},
    "straw_len_m.min": {"type": "float", "range": (0.05, 2.0), "default": 0.3, "effect": "shortest lying straw"},
    "straw_len_m.max": {"type": "float", "range": (0.05, 2.0), "default": 1.0, "effect": "longest lying straw"},
    "straw_d_mm.min": {"type": "float", "range": (0.5, 10.0), "default": 2.0, "effect": "thinnest straw or stem"},
    "straw_d_mm.max": {"type": "float", "range": (0.5, 10.0), "default": 5.0, "effect": "thickest straw or stem"},
    "straw_bend": {"type": "float", "range": (0.0, 1.0), "default": 0.5,
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
                         "default": "surface:belt_straw", "effect": "material of the soil box and the ground slot"},
    "mat_material": {"type": "str", "range": "an id of assets-src/physics_materials.json or surface:<id>",
                     "default": "straw",
                     "effect": "material of the mat boxes and the straw slot: a straw layer over a shape is the "
                               "compliant straw id (review-physics §3)"},
    "mound_material": {"type": "str", "range": "surface:<id> or an id of assets-src/physics_materials.json",
                       "default": "surface:crater_spoil", "effect": "material of the mound spheres"},
    "lod.lod1_switch_m": {"type": "float", "range": (0.1, 100.0), "default": 8.0,
                          "effect": "distance from which LOD1, the slab without the straws, shows"},
    "lod.lod2_switch_m": {"type": "float", "range": (0.1, 500.0), "default": 60.0,
                          "effect": "distance from which LOD2, the flat slab, shows; beyond lod1_switch_m"},
    "lod.cull_m": {"type": "float", "range": (1.0, 2000.0), "default": 400.0,
                   "effect": "distance beyond which the patch is culled; beyond lod2_switch_m"},
}
RANGES = ("mound_w_m", "mound_h_m", "straw_len_m", "straw_d_mm", "stem_h_m", "twig_len_m", "twig_d_mm", "blade_h_m")


def check(params):
    """Cross-parameter rules once every key is in range: ordered ranges, the bake inside the patch, mounds above the
    relief and spheres the grid can follow within the audit's tolerance, mounds that fit the patch, bare patches no
    cell can hold whole, ordered LODs."""
    problems = [f"'{key}.min' = {params[key]['min']} exceeds '{key}.max' = {params[key]['max']}"
                for key in RANGES if params[key]["min"] > params[key]["max"]]
    for key, span in (("bare.size_m", params["bare"]["size_m"]), ("green.width_m", params["green"]["width_m"]),
                      ("green.length_m", params["green"]["length_m"])):
        if span["min"] > span["max"]:
            problems.append(f"'{key}.min' = {span['min']} exceeds '{key}.max' = {span['max']}")
    bare = params["bare"]["size_m"]
    if bare["min"] <= params["cell_m"] * math.sqrt(2):
        problems.append(f"'bare.size_m.min' = {bare['min']} must exceed the cell's diagonal "
                        f"{params['cell_m'] * math.sqrt(2):.3f} m, so no cell holds a whole bare patch")
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
        slab = params["mat_depth_m"] + SOIL_M
        radius = min(_mound_radius(w["min"], h["max"], slab), _mound_radius(w["max"], h["min"], slab))
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
    size, depth = params["size_m"], params["mat_depth_m"]
    slab_m = depth + SOIL_M
    n = max(1, round(size / params["cell_m"]))
    relief = mat.relief(**mat.relief_inputs(params))
    bare = patches.layout(**patches.inputs(params))["patches"]
    for k, patch in enumerate(bare):  # PR4's self-check: the edge boxes open the mat exactly over the outline
        misses = patches.uncovered(patch)
        if misses:
            raise ValueError(f"bare patch {k}: its edge boxes leave {len(misses)} samples of its window open, the "
                             f"first at ({misses[0][0]:.3f}, {misses[0][1]:.3f})")
    strips = mat.green(**mat.green_inputs(params), bare_patches=bare)["strips"]
    mounds = _mounds(rng, params, bare, slab_m)

    def height(x, z):
        """The mat's top."""
        y = relief(x, z)
        for m in mounds:
            d = math.hypot(x - m["x"], z - m["z"])
            if d < m["w"] / 2 + BLEND_M:
                return _mound_height(m, d, y)
        return y

    def surface(x, z):
        """The top a straw rests on: the mat, or a bare patch's floor D below it."""
        return height(x, z) - depth if patches.inside(bare, x, z) else height(x, z)

    def slot(x, z):
        return SOIL_SLOT if any(math.hypot(x - m["x"], z - m["z"]) < m["w"] / 2 for m in mounds) else SLOT

    slab = _slab(n, size, height, _refined_cells(n, size, mounds), slot, slab_m, bare, depth)
    micro = Mesh()
    count = _bake(rng, params, micro, height, surface, relief, bare, strips)
    lod = params["lod"]
    return {
        "lods": [slab, slab, _slab(2, size, lambda x, z: 0.0, set(), lambda x, z: SLOT, slab_m)],
        "micro": [{"lod": 0, "kind": MICRO_KIND, "count": count, "mesh": micro}],
        "switch_distances_m": [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"]],
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": SLOT, "physics": params["physics_material"]},
                      {"slot": SOIL_SLOT, "physics": params["mound_material"]},
                      {"slot": straw.SLOT, "physics": params["mat_material"]}],
        "collision": _collision(params, bare, mounds),
        "gaps": [], "wind_volumes": [], "snag": [],
    }


# --- collision ----------------------------------------------------------------------------------------------------

def _collision(params, bare, mounds):
    """The shapes in PR8's order: the soil box, the fill boxes, each patch's edge boxes, the mound spheres."""
    size, depth, mat = params["size_m"], params["mat_depth_m"], params["mat_material"]
    shapes = [{"shape": "box", "center": [0.0, -depth - SOIL_M / 2, 0.0], "size": [size, SOIL_M, size],
               "rotation": list(IDENTITY), "material": params["physics_material"], "landable": True}]
    for x0, x1, z0, z1 in patches.fill(size, bare):
        shapes.append({"shape": "box", "center": [(x0 + x1) / 2, -depth / 2, (z0 + z1) / 2],
                       "size": [x1 - x0, depth, z1 - z0], "rotation": list(IDENTITY), "material": mat,
                       "landable": True})
    for patch in bare:
        shapes += [_edge_box(rect, depth, mat) for rect in patch["edges"]]
    shapes += [{"shape": "sphere", "center": [m["x"], m["h"] - m["r"], m["z"]], "radius": m["r"],
                "material": params["mound_material"], "landable": True} for m in mounds]
    return shapes


def _edge_box(rect, depth, material):
    """A mat box from an edge rectangle of lib.patches: local X along the edge, local Z across it, from -D to 0. The
    rotation turns local +X onto the edge's direction about +Y (Unity's quaternion, as lib.audit applies it)."""
    (t0, t1), (s0, s1) = rect["t"], rect["s"]
    (ax, az), (dx, dz), (nx, nz) = rect["a"], rect["d"], rect["n"]
    t, s = (t0 + t1) / 2, (s0 + s1) / 2
    half = math.atan2(-dz, dx) / 2
    return {"shape": "box", "center": [ax + dx * t + nx * s, -depth / 2, az + dz * t + nz * s],
            "size": [t1 - t0, depth, s1 - s0], "rotation": [0.0, math.sin(half), 0.0, math.cos(half)],
            "material": material, "landable": True}


# --- the slab -----------------------------------------------------------------------------------------------------

def _smooth(t):
    return t * t * (3 - 2 * t)


def _mound_radius(w, h, slab_m):
    """The mound sphere's radius: through the rim (w/2 at y = 0) and the apex (h), unless its bottom would leave
    the slab, then the largest sphere with its apex at h and its bottom on the slab's bottom."""
    return min((w * w / 4 + h * h) / (2 * h), (slab_m + h) / 2)


def _sagitta(radius, cell):
    """How far a cell diagonal's chord sags below a sphere of this radius."""
    c = cell * math.sqrt(2) / 2
    return radius - math.sqrt(radius * radius - c * c) if c < radius else radius


def _mounds(rng, params, bare, slab_m):
    """The mounds: seeded positions with MOUND_GAP_M of clear ground between them, from the edge and from every bare
    patch's window, each with its foot width w, height h, sphere radius r and cap footprint radius a."""
    size = params["size_m"]
    count = round(params["mounds_per_m2"] * size * size)
    mounds = []
    for _ in range(count):
        w, h = rng.uniform(params["mound_w_m"]["min"], params["mound_w_m"]["max"]), \
            rng.uniform(params["mound_h_m"]["min"], params["mound_h_m"]["max"])
        r = _mound_radius(w, h, slab_m)
        reach = w / 2 + BLEND_M
        for _ in range(1000):
            x, z = rng.uniform(-size / 2 + reach + MOUND_GAP_M, size / 2 - reach - MOUND_GAP_M), \
                rng.uniform(-size / 2 + reach + MOUND_GAP_M, size / 2 - reach - MOUND_GAP_M)
            if all(math.hypot(x - m["x"], z - m["z"]) >= reach + m["w"] / 2 + BLEND_M + MOUND_GAP_M for m in mounds) \
                    and all(_rect_distance(x, z, p["window"]) >= reach + MOUND_GAP_M for p in bare):
                break
        else:
            raise ValueError(f"could not place mound {len(mounds) + 1} of {count} with {MOUND_GAP_M} m between mounds "
                             "and from the bare patches")
        mounds.append({"x": x, "z": z, "w": w, "h": h, "r": r, "a": math.sqrt(r * r - (h - r) ** 2)})
    return mounds


def _rect_distance(x, z, rect):
    x0, x1, z0, z1 = rect
    return math.hypot(max(x0 - x, 0.0, x - x1), max(z0 - z, 0.0, z - z1))


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


def _slab(n, size, height, refined, slot, slab_m, bare=(), depth=0.0):
    """The closed slab: the top as an n x n grid of `height` samples, two triangles per cell (the diagonal
    alternating like a chequerboard, so no ridge runs across the patch), the `refined` cells split REFINE x REFINE
    with their vertices on an edge shared with a coarse cell interpolated along it, each top triangle in the
    material slot(x, z) of its centroid; the cells of the `bare` patches stepped down by `depth` along their
    outlines (_cut_cells); four walls down to -slab_m and the bottom in SLOT; UVs u = x, v = z on the top and bottom
    and upright on the walls."""
    mesh = Mesh()
    cell, k = size / n, REFINE
    vertices = {}  # sub-grid coordinates (coarse * k + fine) -> vertex index on the mat's top
    points = {}    # (key, level) -> vertex index of a cut cell's point at "mat" or "floor" level

    def grid(a):
        return -size / 2 + a * cell / k

    def vertex(a, b, y=None):
        if (a, b) not in vertices:
            x, z = grid(a), grid(b)
            vertices[(a, b)] = mesh.vertex((x, height(x, z) if y is None else y, z))
        return vertices[(a, b)]

    def point(key, xz, level):
        """A point of a cut cell by its key (lib.patches outline corner, outline-grid crossing or grid corner), on
        the mat's top or on the floor D below it; a grid corner on the mat is the grid's own vertex."""
        if key[0] == "g" and level == "mat":
            return vertex(key[1], key[2])
        if (key, level) not in points:
            x, z = xz
            points[(key, level)] = mesh.vertex((x, height(x, z) - (depth if level == "floor" else 0.0), z))
        return points[(key, level)]

    def top_face(tri):
        p = [mesh.vertices[v] for v in tri]
        centroid = (sum(q[0] for q in p) / 3, sum(q[2] for q in p) / 3)
        mesh.face(tri, _up_normal(mesh, tri), slot(*centroid), [(q[0], q[2]) for q in p])

    def triangles(i, j, a, b, c, d):
        for tri in ((a, b, c), (a, c, d)) if (i + j) % 2 == 0 else ((a, b, d), (b, c, d)):
            top_face(tri)

    cut = _cut_cells(n, size, k, grid, bare, refined)
    for (i, j), (p, piece) in sorted(cut.items()):
        _cut_cell(mesh, i, j, k, grid, bare[p]["outline"], piece, point, top_face, triangles)
    for i in range(n):
        for j in range(n):
            if (i, j) in cut:
                continue
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
    def top(i, j):  # a grid vertex on the patch's rim, which no bare patch reaches
        return vertices[(i * k, j * k)]

    y = -slab_m
    bottom = {(sx, sz): mesh.vertex((sx * size / 2, y, sz * size / 2)) for sx in (-1, 1) for sz in (-1, 1)}
    for i in range(n):  # the walls at z = -size/2 and +size/2 run along i, those at x = -size/2 and +size/2 along j
        for (a, b), normal in (((top(i, 0), top(i + 1, 0)), (0.0, 0.0, -1.0)),
                               ((top(i, n), top(i + 1, n)), (0.0, 0.0, 1.0)),
                               ((top(0, i), top(0, i + 1)), (-1.0, 0.0, 0.0)),
                               ((top(n, i), top(n, i + 1)), (1.0, 0.0, 0.0))):
            pa, pb = mesh.vertices[a], mesh.vertices[b]
            la, lb = mesh.vertex((pa[0], y, pa[2])), mesh.vertex((pb[0], y, pb[2]))
            mesh.face([a, b, lb, la], normal, SLOT)
    base = [bottom[(-1, -1)], bottom[(1, -1)], bottom[(1, 1)], bottom[(-1, 1)]]
    mesh.face(base, (0.0, -1.0, 0.0), SLOT, [(mesh.vertices[v][0], mesh.vertices[v][2]) for v in base])
    return mesh


def _cut_cells(n, size, k, grid, bare, refined):
    """(i, j) -> (patch index, the patch's outline clipped to the cell) for every coarse cell an outline reaches. A
    cell two patches or a patch and a mound share is refused (the layout keeps them apart; the check on
    bare.size_m.min keeps a patch from fitting inside one cell)."""
    cell = size / n
    cut = {}
    for p, patch in enumerate(bare):
        xs, zs = [q[0] for q in patch["outline"]], [q[1] for q in patch["outline"]]
        for i in range(max(0, int((min(xs) + size / 2) / cell)), min(n, int((max(xs) + size / 2) / cell) + 1)):
            for j in range(max(0, int((min(zs) + size / 2) / cell)), min(n, int((max(zs) + size / 2) / cell) + 1)):
                piece = _clip_cell(patch["outline"], p, i, j, k, grid)
                if not piece:
                    continue
                if (i, j) in cut or (i, j) in refined:
                    raise ValueError(f"cell ({i}, {j}) is reached by bare patch {p} and by another patch or a mound")
                if all(src[0] == "P" for _, _, src in piece):
                    raise ValueError(f"bare patch {p} lies inside cell ({i}, {j}); make cell_m smaller")
                cut[(i, j)] = (p, piece)
    return cut


def _clip_cell(outline, p, i, j, k, grid):
    """The convex outline clipped to coarse cell (i, j) (Sutherland-Hodgman against its four grid lines), as
    [(key, (x, z), source of the edge leaving this vertex)]. A key names its point exactly, so two cells sharing it
    share the vertex: ("pv", p, m) an outline corner, ("x", p, m, line) where outline edge m crosses a grid line
    ("V", a) at x = grid(a) or ("H", b) at z = grid(b), ("g", a, b) a grid corner. A source is ("P", m), outline edge
    m, or a grid line. Each crossing is computed from the outline edge's own corners, never from a clipped piece, so
    both cells get the same floats."""
    poly = [(("pv", p, m), outline[m], ("P", m)) for m in range(len(outline))]
    for axis, a, keep in ((0, i * k, 1), (0, (i + 1) * k, -1), (1, j * k, 1), (1, (j + 1) * k, -1)):
        line, value = ("V" if axis == 0 else "H", a), grid(a)
        out = []
        for q, (key, xz, src) in enumerate(poly):
            nxz = poly[(q + 1) % len(poly)][1]
            inside, next_inside = keep * (xz[axis] - value) >= 0, keep * (nxz[axis] - value) >= 0
            if inside:
                out.append((key, xz, src))
            if inside != next_inside:
                if src[0] == "P":
                    m = src[1]
                    s, e = outline[m], outline[(m + 1) % len(outline)]
                    other = s[1 - axis] + (value - s[axis]) / (e[axis] - s[axis]) * (e[1 - axis] - s[1 - axis])
                    crossing = (("x", p, m, line), (value, other) if axis == 0 else (other, value))
                else:  # a grid line across this one: the cell's corner
                    va, vb = (a, src[1]) if axis == 0 else (src[1], a)
                    crossing = (("g", va, vb), (grid(va), grid(vb)))
                out.append((*crossing, line if inside else src))  # leaving: along the line; entering: along src
        poly = out
        if not poly:
            return []
    return poly


def _cut_cell(mesh, i, j, k, grid, outline, piece, point, top_face, triangles):
    """One coarse cell an outline reaches: wholly inside, two floor triangles; else the piece inside as floor (a
    fan: it is convex), each part outside as mat (ear-clipped: the mat around a convex outline is not convex) and a
    vertical wall quad on every piece of outline edge, its normal into the patch, in the straw slot up WALL_V of the
    litter's palette column."""
    corner_keys = [("g", i * k, j * k), ("g", (i + 1) * k, j * k), ("g", (i + 1) * k, (j + 1) * k), ("g", i * k, (j + 1) * k)]
    xz = {key: (grid(key[1]), grid(key[2])) for key in corner_keys}
    xz.update({key: q for key, q, _ in piece})
    if not any(src[0] == "P" for _, _, src in piece):
        a, b, c, d = (point(key, xz[key], "floor") for key in corner_keys)
        triangles(i, j, a, b, c, d)
        return
    floor = [point(key, q, "floor") for key, q, _ in piece]
    for q in range(1, len(floor) - 1):
        top_face((floor[0], floor[q], floor[q + 1]))
    x0, x1, z0, z1 = grid(i * k), grid((i + 1) * k), grid(j * k), grid((j + 1) * k)

    def perimeter(key):
        """The point's place on the cell's boundary, counter-clockwise from (x0, z0): 0-1 bottom, 1-2 right, 2-3 top,
        3-4 left."""
        if key[0] == "g":
            return float(corner_keys.index(key))
        (x, z), (axis, a) = xz[key], key[3]
        if axis == "H":
            return (x - x0) / (x1 - x0) if a == j * k else 2 + (x1 - x) / (x1 - x0)
        return 1 + (z - z0) / (z1 - z0) if a == (i + 1) * k else 3 + (z1 - z) / (z1 - z0)

    m = len(piece)
    on_outline = [src[0] == "P" for _, _, src in piece]
    for q in range(m):
        if not on_outline[q] or on_outline[q - 1]:
            continue
        chain = [q]  # a run of outline edges from the boundary point q back to the boundary
        while on_outline[chain[-1]]:
            chain.append((chain[-1] + 1) % m)
        start, end = piece[chain[0]][0], piece[chain[-1]][0]
        ps = perimeter(start)
        span = (perimeter(end) - ps) % 4
        between = sorted(((perimeter(c) - ps) % 4, c) for c in corner_keys if 0 < (perimeter(c) - ps) % 4 < span)
        ring = [start] + [c for _, c in between] + [end] + [piece[c][0] for c in reversed(chain[1:-1])]
        ids = [point(key, xz[key], "mat") for key in ring]
        for a, b, c in _ear_clip([xz[key] for key in ring]):
            top_face((ids[a], ids[b], ids[c]))
        for c0, c1 in zip(chain, chain[1:]):
            edge = piece[c0][2][1]
            s, e = outline[edge], outline[(edge + 1) % len(outline)]
            length = math.hypot(e[0] - s[0], e[1] - s[1])
            nx, nz = (e[1] - s[1]) / length, (s[0] - e[0]) / length  # outward of the outline
            (ka, qa), (kb, qb) = piece[c0][:2], piece[c1][:2]
            quad = [point(ka, qa, "mat"), point(kb, qb, "mat"), point(kb, qb, "floor"), point(ka, qa, "floor")]
            top, foot = straw.palette_uv("litter", WALL_V[1]), straw.palette_uv("litter", WALL_V[0])
            mesh.face(quad, (-nx, 0.0, -nz), straw.SLOT, [top, top, foot, foot])


def _ear_clip(points):
    """Index triples of a simple counter-clockwise polygon in the plane, by ear clipping."""
    order = list(range(len(points)))
    tris = []
    while len(order) > 3:
        for idx in range(len(order)):
            a, b, c = order[idx - 1], order[idx], order[(idx + 1) % len(order)]
            if _cross2(points[a], points[b], points[c]) <= 0:
                continue
            if any(_in_triangle(points[o], points[a], points[b], points[c]) for o in order if o not in (a, b, c)):
                continue
            tris.append((a, b, c))
            del order[idx]
            break
        else:
            raise ValueError(f"no ear left in a mat piece of {len(order)} corners")
    return tris + [tuple(order)]


def _cross2(a, b, c):
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def _in_triangle(p, a, b, c):
    return _cross2(a, b, p) >= 0 and _cross2(b, c, p) >= 0 and _cross2(c, a, p) >= 0


def _up_normal(mesh, tri):
    a, b, c = (mesh.vertices[i] for i in tri)
    u, v = (b[0] - a[0], b[1] - a[1], b[2] - a[2]), (c[0] - a[0], c[1] - a[1], c[2] - a[2])
    nx, ny, nz = u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]
    length = math.sqrt(nx * nx + ny * ny + nz * nz)
    return (nx / length, ny / length, nz / length) if ny > 0 else (-nx / length, -ny / length, -nz / length)


# --- the bake -----------------------------------------------------------------------------------------------------

def _bake(rng, params, micro, height, surface, relief, bare, strips):
    """Scatters the kit into `micro` within bake_radius_m (the module docstring's kinds and rules) and returns the
    instance count. `height` is the mat's top, `surface` the stepped top a straw rests on, `relief` the tussock
    relief (lib.mat), `bare` the bare patches and `strips` the green strips."""
    kit = straw.build_kit(params, rng)
    radius, feather = params["bake_radius_m"], params["bake_feather_m"]
    azimuth, bias = math.radians(params["lodging"]["azimuth_deg"]), params["lodging"]["bias"]
    spread, swathe_turn = math.radians(LODGING_SPREAD_DEG), math.radians(SWATHE_DEG)
    loft, clump, relief_m = params["loft_m"], params["clump"], params["relief_m"]
    top = MAT_MAX_M - params["mat_depth_m"]  # the highest a straw rests: on a MAT_MAX_M mat
    swathe = mat.value_noise(rng, radius * math.sqrt(2) + SWATHE_M, SWATHE_M, SWATHE_M)
    lengths = {kind: [m.bounds()[1][0] - m.bounds()[0][0] for m in meshes] for kind, meshes in kit.items()}
    count = 0

    def kept(x, z):
        """Whether a scatter point stays: all of them inside radius - feather, then fewer along a smoothstep to none
        at the radius."""
        r = math.hypot(x, z)
        if r > radius:
            return False
        return r <= radius - feather or rng.random() < _smooth((radius - r) / feather)

    def crown(x, z):
        """The relief over relief_m, -1 in the deepest hollow to 1 on the highest crown."""
        return relief(x, z) / relief_m if relief_m > 0 else 0.0

    def gathered(x, z):
        """Whether a point of a grid (1 + clump) times denser stays on the crowns: probability (1 + clump c) /
        (1 + clump), so the density is kept on average."""
        return rng.random() < (1 + clump * crown(x, z)) / (1 + clump)

    def rest(x, z, share, on_mat):
        """The height a straw end rests at: `share` of the local loft above the mat's top (the crowns lofted most),
        never above a MAT_MAX_M mat (on a mound, its earth), or on the soil of a floor. A mat straw's end over a floor
        within LIP_OVERHANG_M of its outline is held at the mat's top: it overhangs the step."""
        d = _floor_depth(bare, x, z)
        if d is not None and not (on_mat and d <= LIP_OVERHANG_M):
            return surface(x, z)
        local = loft * (1 - clump + clump * (1 + crown(x, z)) / 2)
        base = height(x, z)
        return min(max(top, base), base - STRAW_SINK_M + share * (local + STRAW_SINK_M))

    def lying(kind, layer, x, z, yaw=None):
        """One lying straw or twig centred on (x, z), its ends resting at depths drawn from `layer`, at `yaw` or
        else crosswise with the swathes' bias."""
        k = rng.randrange(len(kit[kind]))
        if yaw is None:
            local = azimuth + swathe_turn * swathe(x, z)
            yaw = local + rng.uniform(-spread, spread) if rng.random() < bias else rng.uniform(0, 2 * math.pi)
        half = lengths[kind][k] / 2
        dx, dz = math.cos(yaw) * half, math.sin(yaw) * half
        on_mat = _floor_depth(bare, x, z) is None
        shares = (rng.uniform(*layer), rng.uniform(*layer)) if on_mat else (0.0, 0.0)
        y0, y1 = rest(x - dx, z - dz, shares[0], on_mat), rest(x + dx, z + dz, shares[1], on_mat)
        pitch = math.atan2(y1 - y0, 2 * half) + math.radians(rng.uniform(-STRAW_PITCH_DEG, STRAW_PITCH_DEG))
        shade = DEPTH_SHADE * ((shares[0] + shares[1]) / 2 - 0.5) if on_mat else 0.0
        straw.place(micro, kit[kind][k], (x, (y0 + y1) / 2, z),
                    straw.lying_axes(yaw, pitch, rng.uniform(0, 2 * math.pi)), shade)
        return 1

    def strewn(layer, kind):
        """A lying straw anywhere, kept over a floor only as the straws strewn on its soil (FLOOR_KEEP)."""
        def place(x, z):
            d = _floor_depth(bare, x, z)
            if d is not None:
                edge = 1 - _smooth(min(1.0, d / patches.FEATHER_M))
                if rng.random() >= (FLOOR_KEEP[0] + (FLOOR_KEEP[1] - FLOOR_KEEP[0]) * edge) / (1 + clump):
                    return 0
            elif not gathered(x, z) or rng.random() < GREEN_THIN * mat.green_weight(strips, x, z):
                return 0
            return lying(kind, layer, x, z)
        return place

    def tuft(x, z):
        """A tussock on the mat: stems_per_tuft stems within TUFT_R_M of (x, z), each leaning away from the centre."""
        if _floor_depth(bare, x, z) is not None or not gathered(x, z):
            return 0
        tilt_max = math.radians(params["stem_tilt_deg"])
        for _ in range(params["stems_per_tuft"]):
            kind = "stem_head" if rng.random() < params["seed_head_share"] else "stem"
            k = rng.randrange(len(kit[kind]))
            bearing, d = rng.uniform(0, 2 * math.pi), TUFT_R_M * math.sqrt(rng.random())
            sx, sz = x + d * math.cos(bearing), z + d * math.sin(bearing)
            origin = (sx, surface(sx, sz) - STEM_SINK_M, sz)
            tilt = tilt_max * rng.uniform(0.25, 1.0)
            straw.place(micro, kit[kind][k], origin, straw.standing_axes(bearing + rng.uniform(-0.5, 0.5), tilt))
        return params["stems_per_tuft"]

    def blades(x, z):
        """A tuft of regrowth blades, kept by the green strips' weight and never on a floor."""
        if _floor_depth(bare, x, z) is not None or rng.random() >= mat.green_weight(strips, x, z):
            return 0
        for _ in range(BLADE_TUFT):
            k = rng.randrange(len(kit["blade"]))
            bearing, d = rng.uniform(0, 2 * math.pi), BLADE_TUFT_R_M * math.sqrt(rng.random())
            bx, bz = x + d * math.cos(bearing), z + d * math.sin(bearing)
            origin = (bx, height(bx, bz) - STEM_SINK_M, bz)
            tilt = math.radians(BLADE_TILT_DEG) * rng.random()
            straw.place(micro, kit["blade"][k], origin, straw.standing_axes(bearing, tilt))
        return BLADE_TUFT

    for density, per, place in (
            (params["straw_per_m2"] * (1 + clump), 1, strewn(TOP_LAYER, "straw")),
            (params["body_straw_per_m2"] * (1 + clump), 1, strewn(BODY_LAYER, "straw")),
            (params["stem_per_m2"] * (1 + clump), params["stems_per_tuft"], tuft),
            (params["twig_per_m2"], 1, lambda x, z: lying("twig", TWIG_LAYER, x, z)),
            (params["blade_per_m2"], BLADE_TUFT, blades)):
        if density <= 0 or radius <= 0:
            continue
        s = math.sqrt(per / density)
        k = int(math.ceil(radius / s))
        for i in range(-k, k):
            for j in range(-k, k):
                x, z = (i + rng.random()) * s, (j + rng.random()) * s
                if kept(x, z):
                    count += place(x, z)
    for patch in bare:  # the lip: straws laid across every outline edge from the mat, draping over the step
        outline = patch["outline"]
        for a, b in zip(outline, outline[1:] + outline[:1]):
            length = math.dist(a, b)
            dx, dz = (b[0] - a[0]) / length, (b[1] - a[1]) / length
            for i in range(int(length * LIP_STRAWS_PER_M)):
                t, off = (i + rng.random()) / LIP_STRAWS_PER_M, rng.uniform(0.01, LIP_OVERHANG_M)
                x, z = a[0] + dx * t + dz * off, a[1] + dz * t - dx * off  # (dz, -dx) is outward
                if kept(x, z):
                    yaw = math.atan2(dx, -dz) + math.radians(rng.uniform(-LIP_SPREAD_DEG, LIP_SPREAD_DEG))
                    count += lying("straw", TOP_LAYER, x, z, yaw)
    return count


def _floor_depth(bare, x, z):
    """How far (x, z) lies inside a bare patch's step outline, or None on the mat."""
    for p in bare:
        w = p["window"]
        if w[0] <= x <= w[1] and w[2] <= z <= w[3]:
            d = patches.distance((x, z), p["outline"])
            if d <= 0:
                return -d
    return None
