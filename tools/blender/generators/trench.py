"""trench: an open trench cut into its own ground patch, E01 first (p1-e01-trench, design decisions 1-10; object sheet
earthworks-and-surfaces §E01, the trench-family table and the shared rules F-1...F-8). Plain Python on lib.geometry.

Frame (asset space, lib.frame): the origin is the run's first centre-line point, y = 0 the ground, the run starting
along +X turned by half its first bend. Sides: looking along the centre line from its first point, the belt parapet
(parapet_h_m.belt) and the niches are on the run's left, +n with n = (-t_z, t_x) the left normal of a segment's
direction t, and the field parapet (parapet_h_m.field) is on its right (G: the strip on the left, the field on the
right; the review capture stands on the right). A lateral offset d is positive to the left.

The centre line (design 1). Bends of bend_deg (+- BEND_JITTER_DEG) alternate left and right every bend_every_m
(+- LEG_JITTER_M) along the run, the first to a seeded side, the run starting at half the first bend so it wanders
about +X; each bend is an arc of BEND_RADIUS_M, and the first and last END_STRAIGHT_M of the run are straight. The
samples: every metre on the straights (STRAIGHT_STEP_M, shorter to divide a stretch evenly), arcs in an even number
of equal pieces (so a sample stands at the bend vertex, the arc's middle) no longer than ARC_STEP_M and turning at
most JOINT_DEG each, and each niche's segment as one piece. The polyline through the samples is the run: every face,
box and gap is built per segment between two samples, mitred at the samples.

The cut (design 2). Floor at -depth_m, bottom_w_m wide; each wall from the floor edge to the lip, the lip
top_w_m / 2 + c out, c the lip crumble: per side a walk over the samples in [CRUMBLE_MIN_M, lip_crumble_m],
changing by at most CRUMBLE_SLOPE per metre (so a wall's twisted quad stays within 1 cm of its box's plane), the inset
held at each sample so the lip stays within top_w_m / 2 + lip_crumble_m of the centre line on a bend's outside (the
band's margin). The end walls have the side walls' batter: the floor ends (top_w_m - bottom_w_m) / 2 in from the run's ends. Two niches (one
per niche_every_m, NICHE_PILLAR_M in from their segment's ends) are cut into the belt wall in the wall's own frame:
their sill SILL_M above the floor at the mouth, niche_m.h up the wall, niche_m.d into it, niche_m.w along the run;
their crumble is held over their segment so that wall is planar.

The ground (design 1). The patch is the run's bounding box plus margin_m, snapped to CELL_M, built as a closed block
from y = 0 down to FLOOR_BOX_M under the floor (the floor boxes' bottom), so its collision is one solid; its top is a
grid of CELL_M cells, those within FINE_D_M of the run, round a parapet's end and over the parapets split MEDIUM x
MEDIUM and those by a parapet's end crest FINE x FINE (the grid follows the parapets' creases within the audit's
tolerance), the vertices on an edge shared with a coarser
cell interpolated along it (no cracks), and cut along the lips: each cell minus the run's top slot (one convex quad per
segment), the lip at y = 0. Height: the berm level (y = 0) from the lip to the parapets' inner toe, then each parapet,
a ridge along its own centre polyline (the run's offset to the middle of its base) whose cross-section rises at
parapet_slope_deg from the inner toe (top_w_m / 2 + berm_m out) to its height and falls the same way to its outer toe
parapet_base_m out, ending in a cone round each end of the run (the cross-section turned about the ridge's end); plus
S04's tussock relief (lib.mat, RELIEF_M) faded in over the parapets at RELIEF_PARAPET of it and in full past them, none
on the berm. The top takes the ground slot (grassed_spoil: a cover with no bare patch, green strip or relief tone, which
here would be stains with no relief under them) with UVs in metres from the patch's centre, u = x - its x and v = z - its
z, so that recipe's map, GROUND_MAP_M across and centred on the UVs' origin, is laid once over the whole patch, never
wrapped (check: the patch fits it); the block's sides and bottom the section slot (the wall map, a soil section). Every
cut face takes the wall slot (trench_wall) with UVs on lib.strip's strip. LOD0 adds the clods (low sphere caps, clods_per_m, in the clod slot) and, as its visual-only micro
mesh, straws and twigs blown onto the floor at FLOOR_STRAW of S04's densities (lib.straw's kit); LOD1 is LOD0 without
the micro mesh; LOD2 the patch at 1 m with the cut as a plain slot, no niches, no clods.

Collision (design 4, the hole contract F-1...F-8), in this order: per segment the floor box, then per side the wall box
and the lip box (on a niche's segment the belt wall below the sill and above the ceiling and the lip box above and
below the niche, then the niche's sill, ceiling, back and side boxes and the lip box's middle behind and beside it);
then the two end walls and the two end blocks; then the parapets (per straight stretch or arc piece and side: inner
slope, crest, outer slope; then per parapet end CAP_SECTORS cone sectors and a crest cylinder); then the margin
columns; then the clods. Every cut shape (floor, walls, lips, ends, niches) is surface:belt_bare, not landable, at least
FILLER_M thick, its open face on the visual; a box along a segment reaches past each of its joints so that every corner
of its end face stands OVERLAP_M past the joint's mitre plane (_span), so neighbours overlap 0.2 m on every line across
the joint, the wedge on a bend's outside included. The lip boxes are level, their top y = 0, and reach from the lip down
to the block's bottom and out to BACKING_M past every band's edge (F-1: a cell marked by touch reaches 0.71 m past a
band): they are the backing as well; nothing of the cut stands above y = 0 (F-4). The parapets, their caps and the
margins are surface:belt_straw, landable. The clods are surface:crater_spoil spheres, not landable, each under its own
cap of a mesh. No wind volume (F-8), no snag.

Gaps (design 5): at every interior sample the clear cross-section from the floor to the lips in the mitre plane,
`xs_<chainage>`, and per segment the top slot between the lips at y = 0, `slot_<chainage of its start>`; narrowest_m
the inscribed circle (lib.audit). Holes (design 6): the run's record (its samples as [x, 0, z], top_w_m, depth_m, its
band top_w_m + 2 lip_crumble_m + 2 BAND_MARGIN_M) first, then one per niche from the run's centre line square to its
farthest back point, the niche's width and sill depth, a band 2 (niche_m.w / 2 + BAND_MARGIN_M) wide.
"""
import math
import random

from lib import mat, strip, straw
from lib.audit import inscribed_diameter
from lib.geometry import Mesh

GROUND_SLOT = "ground"    # the patch's top: berm, parapets, margins (grassed_spoil)
WALL_SLOT = "wall"        # every cut face: walls, floor, end walls, niches (trench_wall)
CLOD_SLOT = "clod"        # the clods on the floor (trench_wall's spill band)
SECTION_SLOT = "section"  # the block's sides and bottom: a soil section in the wall map, the ground's physics
CUT = "surface:belt_bare"        # every cut face's shape (F-7; review-materials.md §6)
GROUND = "surface:belt_straw"    # parapets and margins (S04's surface)
SPOIL = "surface:crater_spoil"   # loose spoil: the clods
SIDES = (1, -1)           # +1 the belt side (the run's left), -1 the field side (its right)

BEND_RADIUS_M = 4.0       # a bend's arc (assumed: G's bends read as curves over 1-2 m of run, not kinks)
BEND_JITTER_DEG = 5.0     # design 1, assumed
LEG_JITTER_M = 1.0        # design 1, assumed
END_STRAIGHT_M = 1.5      # the run's first and last metres are straight (assumed: the end walls and caps stand square)
MIN_STRAIGHT_M = 0.5      # the least straight between two arcs
STRAIGHT_STEP_M = 1.0     # design 2: 1 m segments on the straights
ARC_STEP_M = 0.5          # design 2: 0.5 m on bends
JOINT_DEG = 5.0           # derived: a box's 0.1 m past a joint on a bend's inside stands 0.1 sin(turn + the lip's kink)
                          # into the next segment's cut: 5 degrees and the crumble's 3.4 keep it under 15 mm (2 cm zone)
CRUMBLE_MIN_M = 0.01      # assumed: a spade-cut lip always crumbles a little (the soil lip radius is 2 cm, a physics
                          # constant), so an established lip never runs on its clean spade line (G); lip_crumble_m 0, a
                          # fresh trench's sharp lip, takes it to 0 (the audit's zone-edge guard then ends on the toe)
CRUMBLE_SLOPE = 0.03      # assumed: the lip's inset changes by at most this per metre of run, so the lip line kinks by
                          # at most 2 x 1.7 degrees at a joint and a wall's twisted quad stays within a centimetre of its box
NICHE_PILLAR_M = 0.1      # wall left either side of a niche in its segment (assumed)
SILL_M = 0.15             # design 2: a niche's sill above the floor at its mouth
FILLER_M = 0.3            # F-5: a cut shape at least this thick behind its open face (carried)
OVERLAP_M = 0.1           # F-5: each shape this past its segment's ends, so neighbours overlap 0.2 m (carried)
FLOOR_BOX_M = 0.35        # the floor box under the floor, and so the block's bottom: FILLER_M and enough that the lip
                          # box under a niche's sill (SILL_M up, its back 5 cm lower) still stands 0.3 m (derived)
WALL_DOWN_M = 0.15        # a wall box reaches this far below the floor, into the floor box (assumed)
FLOOR_SPREAD_M = 0.35     # the floor box reaches this past the floor's edges, under the wall boxes (derived: 0.3 + 0.05)
BAND_MARGIN_M = 0.65      # F-2, settled: every cavity face this far inside the bands (0.25 + half a 0.5 m cell's diagonal)
BACKING_M = 0.75          # settled: the lip boxes reach this past every band's edge (a cell marked by touch: 0.71 m)
PARAPET_T_M = 0.5         # a slope box's thickness under its face: its bottom stays under y = 0 below a 0.45 m crest
CREST_DOWN_M = 0.3        # a crest box reaches this below y = 0
CAP_SECTORS = 16          # a parapet's end cone in sectors of 11.25 degrees: a sector box's corner stands at most 25 mm
                          # proud of the cone by the field crest (12 sectors: 38 mm, with the grid's sag over 50 mm)
CAP_SEAM_M = 0.02         # a sector box reaches this past its sector's edge
PARAPET_END_M = 0.1       # a parapet's straight boxes reach this past the run's end, into its cap
COLUMN_M = 2.0            # the margin columns' width (assumed)
COLUMN_SEAM_M = 0.05      # columns overlap by this (S04's SEAM_M)
CLEAR_M = 1.0             # a margin column stays this far from the run's centre line (inside the lip boxes' reach)
CELL_M = 0.25             # design 1: the patch grid
GROUND_MAP_M = 40.0       # the grassed_spoil map's span (carried, its size_m): the patch centred in it, 51 px/m at 2K
FINE_D_M = 1.0            # cells within this of the run (the lips, the berm, the inner toes, in the gaps' 2 cm zone)
MEDIUM = 2                # and over the parapets split 2 x 2 (12.5 cm), so the toe's crease is followed within 2 cm and the
                          # crests' within the 5 cm soft top (design 1: 0.1 m near the lips)
FINE, CAP_FINE_M = 4, 0.7  # cells within CAP_FINE_M of a parapet's end split 4 x 4 (6.25 cm): the cone's narrow crest
RELIEF_M = 0.04           # S04's tussock relief (carried, S04_patch.json), on the plain margins
RELIEF_PARAPET = 0.5      # design 4: held to +-2 cm on the parapets
RELIEF_CLEAR_M = 0.15     # no relief this far past the inner toe, the ramp to the parapets' share over RELIEF_RAMP_M
RELIEF_RAMP_M = 0.45
RELIEF_OUT_M = 1.0        # the relief rises to its full past the outer toe over this
RELIEF_AZIMUTH_DEG, RELIEF_BIAS = 25.0, 0.3  # S04's lodging (carried)
SWELL = 1.25              # the sheet's swell (assumed): parapet volume per cut volume
SWELL_TOLERANCE = 0.3     # design 10: check refuses parapets off the swell by more than this
BATTER_TOLERANCE_DEG = 0.5  # check: batter_deg against the widths' and depth's within this
CLOD_D_M = (0.1, 0.4)     # the sheet's clods (S03, E01), across
CLOD_H = (0.25, 0.4)      # a clod's height over its width (assumed: low, G's floor lumpy)
CLOD_NOISE_M = 0.008      # a clod's surface off its sphere (within the 2 cm zone of the gaps)
CLOD_AROUND, CLOD_RINGS = 10, 3
CLOD_CLEAR_M = 0.02       # a clod clear of the walls and of every cross-section's plane (the gaps stay clear)
CLOD_TRIES = 200
FLOOR_STRAW = 0.1         # design 3: a tenth of S04's densities on the floor
S04_STRAW_PER_M2 = 340.0  # S04's lying straws, top and body layers (carried, S04_patch.json)
S04_TWIG_PER_M2 = 2.0     # S04's twigs (carried)
STRAW_CLEAR_M = 0.02      # a floor straw's ends this far inside the floor
KIT = {"straw_len_m": {"min": 0.3, "max": 1.0}, "straw_d_mm": {"min": 2.0, "max": 5.0}, "straw_bend": 0.5,
       "stem_h_m": {"min": 0.5, "max": 1.2}, "twig_len_m": {"min": 0.3, "max": 1.0}, "twig_d_mm": {"min": 5.0, "max": 20.0},
       "blade_h_m": {"min": 0.05, "max": 0.15}, "variants": 12, "segments": 4}  # S04's kit (carried, S04_patch.json)
MICRO_KIND = "straw"
SHAPES = ("straight", "zigzag", "curving")
REVETMENTS = ("none", "boards", "wattle", "mesh")
EPS = 1e-9

PARAMS = {
    "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 1,
             "effect": "every draw: the bends, the crumble, the niches' places, the relief, the clods, the floor's straws"},
    "shape": {"type": "str", "range": "straight, zigzag or curving", "default": "curving",
              "effect": "how the run winds: straight, or curving with bends every bend_every_m; zigzag is refused until "
                        "E02-E04 build its corners"},
    "run_m": {"type": "float", "range": (10.0, strip.RUN_M), "default": 24.0,
              "effect": "the run's length along its centre line, at most the trench_wall strip's run (lib.strip)"},
    "bend_deg": {"type": "float", "range": (15.0, 40.0), "default": 25.0,
                 "effect": "each bend's turn, +-5 degrees seeded, alternating left and right (sheet 15-40)"},
    "bend_every_m": {"type": "float", "range": (3.0, 8.0), "default": 5.0,
                     "effect": "the run between bends, +-1 m seeded (sheet 3-8)"},
    "top_w_m": {"type": "float", "range": (0.6, 1.0), "default": 0.8, "effect": "lip to lip, before the crumble"},
    "bottom_w_m": {"type": "float", "range": (0.5, 0.8), "default": 0.6, "effect": "the floor's width"},
    "depth_m": {"type": "float", "range": (1.2, 2.0), "default": 1.5, "effect": "the floor below the lip"},
    "batter_deg": {"type": "float", "range": (75.0, 90.0), "default": 86.0,
                   "effect": "the walls' batter; must agree with the widths and depth within 0.5 degrees (check)"},
    "berm_m": {"type": "float", "range": (0.2, 0.6), "default": 0.3,
               "effect": "level ground from the lip (before the crumble) to each parapet's inner toe"},
    "parapet_h_m.field": {"type": "float", "range": (0.0, 0.6), "default": 0.45,
                          "effect": "the field parapet's height, on the run's right"},
    "parapet_h_m.belt": {"type": "float", "range": (0.0, 0.6), "default": 0.3,
                         "effect": "the belt parapet's height, on the run's left"},
    "parapet_base_m": {"type": "float", "range": (1.5, 3.0), "default": 2.5, "effect": "each parapet's base, toe to toe"},
    "parapet_slope_deg": {"type": "float", "range": (15.0, 40.0), "default": 22.0,
                          "effect": "the parapets' grassed slopes; their volume must match the cut's swell (check)"},
    "niche_every_m": {"type": "float", "range": (5.0, 30.0), "default": 10.0,
                      "effect": "one niche in the belt wall per this much run, on the nearest straight"},
    "niche_m.w": {"type": "float", "range": (0.6, 1.0), "default": 0.8, "effect": "a niche along the run"},
    "niche_m.h": {"type": "float", "range": (0.5, 0.7), "default": 0.6, "effect": "a niche up the wall from its sill"},
    "niche_m.d": {"type": "float", "range": (0.5, 1.0), "default": 0.7, "effect": "a niche into the wall"},
    "lip_crumble_m": {"type": "float", "range": (0.0, 0.1), "default": 0.1,
                      "effect": "the lip's inset, a walk in 0 to this along the run per side; the band holds it"},
    "topsoil_m": {"type": "float", "range": (0.3, 1.0), "default": 0.5,
                  "effect": "the dark topsoil band at the top of the walls (trench_wall's map reads it)"},
    "age": {"type": "float", "range": (0.0, 1.0), "default": 0.8,
            "effect": "0.5-1 the established trench (grassed parapets, crumbled lip, clods: G); under 0.5 the fresh "
                      "state (bare spoil, sharp lip) is refused, not built yet"},
    "clods_per_m": {"type": "float", "range": (0.5, 2.0), "default": 1.0, "effect": "spilled clods per metre of floor"},
    "margin_m": {"type": "float", "range": (4.0, 10.0), "default": 6.0,
                 "effect": "plain ground round the run's bounding box"},
    "revetment": {"type": "str", "range": "none, boards, wattle or mesh", "default": "none",
                  "effect": "E02's lining; only none is built"},
    "collapse": {"type": "float", "range": (0.0, 1.0), "default": 0.0, "effect": "E03's slump; only 0 is built"},
    "overgrowth": {"type": "float", "range": (0.0, 1.0), "default": 0.0, "effect": "E03's weeds; only 0 is built"},
    "lod.lod1_switch_m": {"type": "float", "range": (0.1, 100.0), "default": 8.0,
                          "effect": "distance from which LOD1, without the floor's straws, shows"},
    "lod.lod2_switch_m": {"type": "float", "range": (0.1, 500.0), "default": 60.0,
                          "effect": "distance from which LOD2, the patch at 1 m with the cut as a plain slot, shows"},
    "lod.cull_m": {"type": "float", "range": (1.0, 2000.0), "default": 400.0, "effect": "distance beyond which it is culled"},
}


def check(params):
    """Cross-parameter rules once every key is in range: the shapes and states built, a floor no wider than the lip,
    the batter the widths and depth give, parapets with a crest whose volume matches the cut's swell, bends that fit
    their legs, parapets inside a bend's radius, a niche inside the wall, ordered LODs."""
    problems = []
    if params["shape"] not in SHAPES:
        problems.append(f"'shape' = {params['shape']!r} must be one of {', '.join(SHAPES)}")
    elif params["shape"] == "zigzag":
        problems.append("'shape' = 'zigzag' is not built yet: its corners come with E02-E04")
    if params["revetment"] not in REVETMENTS:
        problems.append(f"'revetment' = {params['revetment']!r} must be one of {', '.join(REVETMENTS)}")
    elif params["revetment"] != "none":
        problems.append(f"'revetment' = {params['revetment']!r} is E02's, not built yet; only none")
    for key in ("collapse", "overgrowth"):
        if params[key] != 0:
            problems.append(f"'{key}' = {params[key]} is E03's, not built yet; only 0")
    if params["age"] < 0.5:
        problems.append(f"'age' = {params['age']} is the fresh state (bare spoil, sharp lip), not built yet; at least 0.5")
    top, bottom, depth = params["top_w_m"], params["bottom_w_m"], params["depth_m"]
    if bottom > top:
        problems.append(f"'bottom_w_m' = {bottom} is wider than the lip 'top_w_m' = {top}")
    else:
        batter = math.degrees(math.atan2(depth, (top - bottom) / 2))
        if abs(batter - params["batter_deg"]) > BATTER_TOLERANCE_DEG:
            problems.append(f"'batter_deg' = {params['batter_deg']} disagrees with the widths and depth, which give "
                            f"{batter:.2f} degrees (within {BATTER_TOLERANCE_DEG})")
    slope = math.radians(params["parapet_slope_deg"])
    base = params["parapet_base_m"]
    area = 0.0
    for side in ("field", "belt"):
        h = params["parapet_h_m"][side]
        if 2 * h / math.tan(slope) > base:
            problems.append(f"'parapet_h_m.{side}' = {h} at {params['parapet_slope_deg']} degrees needs a base of "
                            f"{2 * h / math.tan(slope):.2f} m, more than 'parapet_base_m' = {base}")
        area += h * (base - h / math.tan(slope))
    cut = (top + bottom) / 2 * depth
    if cut > 0 and abs(area / (cut * SWELL) - 1) > SWELL_TOLERANCE:
        problems.append(f"the parapets hold {area:.3f} m3/m, the cut's {cut:.3f} m3/m at a swell of {SWELL} gives "
                        f"{cut * SWELL:.3f}: off by {100 * (area / (cut * SWELL) - 1):+.0f} %, more than "
                        f"{100 * SWELL_TOLERANCE:.0f} %")
    span = params["run_m"] + 2 * params["margin_m"] + 2 * CELL_M  # the patch's longest side at most, snapped to the grid
    if span > GROUND_MAP_M:
        problems.append(f"the patch spans up to {span:.2f} m ('run_m' + 2 'margin_m' + 2 cells), more than the "
                        f"grassed_spoil map's {GROUND_MAP_M} m")
    reach = top / 2 + params["berm_m"] + base
    if reach > BEND_RADIUS_M - 0.5:
        problems.append(f"the parapets reach {reach:.2f} m from the centre line, past a bend's radius "
                        f"{BEND_RADIUS_M} m less 0.5 m")
    turn = math.radians(params["bend_deg"] + BEND_JITTER_DEG)
    if params["shape"] == "curving" and BEND_RADIUS_M * turn + MIN_STRAIGHT_M > params["bend_every_m"] - 2 * LEG_JITTER_M:
        problems.append(f"bends of {params['bend_deg']} +- {BEND_JITTER_DEG} degrees on a {BEND_RADIUS_M} m radius do "
                        f"not fit legs of {params['bend_every_m']} +- {LEG_JITTER_M} m")
    niche = params["niche_m"]
    if SILL_M + niche["h"] > depth - FILLER_M:
        problems.append(f"a niche's top ({SILL_M + niche['h']:.2f} m above the floor) leaves under {FILLER_M} m of wall "
                        f"to the lip")
    lod = params["lod"]
    if not lod["lod1_switch_m"] < lod["lod2_switch_m"] < lod["cull_m"]:
        problems.append("'lod.lod1_switch_m' < 'lod.lod2_switch_m' < 'lod.cull_m' must hold")
    return problems


def build(params):
    run = Run(params)
    clods = _clods(params, run)
    top = _top(run, params, refined=True)
    lod0 = Mesh()
    _merge(lod0, top)
    _block(lod0, run)
    _cavity(lod0, run, niches=True)
    for clod in clods:
        _clod_mesh(lod0, clod)
    lod2 = Mesh()
    _merge(lod2, _top(run, params, refined=False))
    _block(lod2, run)
    _cavity(lod2, run, niches=False)
    micro = Mesh()
    count = _floor_straws(params, run, clods, micro)
    lod = params["lod"]
    collision, joints = _collision(run, clods)
    return {
        "lods": [lod0, lod0, lod2],
        "micro": [{"lod": 0, "kind": MICRO_KIND, "count": count, "mesh": micro}],
        "switch_distances_m": [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"]],
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": GROUND_SLOT, "physics": GROUND}, {"slot": WALL_SLOT, "physics": CUT},
                      {"slot": CLOD_SLOT, "physics": SPOIL}, {"slot": SECTION_SLOT, "physics": GROUND},
                      {"slot": straw.SLOT, "physics": "straw"}],
        "collision": collision,
        "gaps": _gaps(run),
        "holes": run.holes(),
        "wind_volumes": [], "snag": [],
        "lines": joints,
    }


# --- the run ------------------------------------------------------------------------------------------------------

class Run:
    """The run's centre line, its samples and segments, the lip crumble, the niches, the parapets' and the bands'
    numbers; everything the mesh, the collision, the gaps and the holes are built from."""

    def __init__(self, params):
        self.p = params
        self.length = params["run_m"]
        self.top, self.bottom, self.depth = params["top_w_m"] / 2, params["bottom_w_m"] / 2, params["depth_m"]
        self.run_in = self.top - self.bottom  # the walls' (and end walls') horizontal batter
        self.crumble_m = params["lip_crumble_m"]
        self.block = self.depth + FLOOR_BOX_M
        self.toe = self.top + params["berm_m"]
        self.base = params["parapet_base_m"]
        self.slope = math.radians(params["parapet_slope_deg"])
        self.h = {1: params["parapet_h_m"]["belt"], -1: params["parapet_h_m"]["field"]}
        self.band = self.top + self.crumble_m + BAND_MARGIN_M  # the run's band, half its width
        rng = random.Random(f"trench path {params['seed']}")
        self.arcs = self._bends(rng)
        self.niche_spans = self._niche_spans()
        self.chainage, self.points = self._sample()
        n = len(self.points) - 1
        self.n = n
        self.t = [_unit2(_sub2(self.points[i + 1], self.points[i])) for i in range(n)]
        self.normal = [(-t[1], t[0]) for t in self.t]
        self.seg_len = [_dist2(self.points[i], self.points[i + 1]) for i in range(n)]
        # the turn at each sample, positive to the left; 0 at the ends
        self.turn = [0.0] + [math.atan2(_cross2(self.t[j - 1], self.t[j]), _dot2(self.t[j - 1], self.t[j]))
                             for j in range(1, n)] + [0.0]
        self.niche_segments = [i for i in range(n) for a, b in self.niche_spans
                               if abs(self.chainage[i] - a) < 1e-9 and abs(self.chainage[i + 1] - b) < 1e-9]
        self.crumble = self._crumble(rng)
        niche = params["niche_m"]
        self.niche = {"w": niche["w"], "h": niche["h"], "d": niche["d"]}
        self.niche_reach = self._niche_reach() if self.niche_segments else 0.0
        self.backing = {-1: self.band + BACKING_M,
                        1: max(self.band, self.niche_reach + niche["w"] / 2 + BAND_MARGIN_M) + BACKING_M}
        self.parapet_line = {side: [self.at(j, side * (self.toe + self.base / 2)) for j in range(n + 1)] for side in SIDES}

    # the centre line

    def _bends(self, rng):
        """[(start, end, turn)] of the arcs, by chainage."""
        p = self.p
        if p["shape"] != "curving":
            return []
        arcs, sign, k = [], (1 if rng.random() < 0.5 else -1), 1
        while True:
            mid = p["bend_every_m"] * k + rng.uniform(-LEG_JITTER_M, LEG_JITTER_M)
            turn = sign * math.radians(p["bend_deg"] + rng.uniform(-BEND_JITTER_DEG, BEND_JITTER_DEG))
            half = BEND_RADIUS_M * abs(turn) / 2
            if mid + half > self.length - END_STRAIGHT_M:
                return arcs
            low = (arcs[-1][1] + MIN_STRAIGHT_M) if arcs else END_STRAIGHT_M
            if mid - half < low:
                raise ValueError(f"bend {k} at {mid:.2f} m does not fit after the last at {low:.2f} m")
            arcs.append((mid - half, mid + half, turn))
            sign, k = -sign, k + 1

    def _niche_spans(self):
        """[(start, end)] of the niches' segments: one per niche_every_m of run, centred as near as it fits to the
        middle of its share, on a straight at least MIN_STRAIGHT_M from an arc and END_STRAIGHT_M from the ends."""
        span = self.p["niche_m"]["w"] + 2 * NICHE_PILLAR_M
        free, last = [], END_STRAIGHT_M
        for a, b, _ in self.arcs:
            free.append((last, a - MIN_STRAIGHT_M))
            last = b + MIN_STRAIGHT_M
        free.append((last, self.length - END_STRAIGHT_M))
        spans = []
        for k in range(int(self.length // self.p["niche_every_m"])):
            target = self.p["niche_every_m"] * (k + 0.5)
            best = None
            for a, b in free:
                if b - a < span:
                    continue
                c = min(max(target, a + span / 2), b - span / 2)
                if any(abs(c - (s0 + s1) / 2) < span + MIN_STRAIGHT_M for s0, s1 in spans):
                    continue
                if best is None or abs(c - target) < abs(best - target):
                    best = c
            if best is None:
                raise ValueError(f"niche {k}: no straight of {span:.2f} m left near {target:.1f} m")
            spans.append((best - span / 2, best + span / 2))
        return sorted(spans)

    def _sample(self):
        """The samples' chainages and plan points: arcs in an even number of equal pieces, at most ARC_STEP_M and
        JOINT_DEG each, straights in equal pieces of at most STRAIGHT_STEP_M, a niche's segment whole."""
        cuts = [(0.0, "line"), (self.length, "end")]
        for a, b, turn in self.arcs:
            cuts += [(a, ("arc", turn)), (b, "line")]
        for a, b in self.niche_spans:
            cuts += [(a, "niche"), (b, "line")]
        cuts.sort(key=lambda c: c[0])
        chain = [0.0]
        kinds = []
        for (s0, kind), (s1, _) in zip(cuts, cuts[1:]):
            if s1 - s0 < 1e-9:
                continue
            if kind == "niche":
                pieces = 1
            elif isinstance(kind, tuple):
                turn = abs(kind[1])
                pieces = max(math.ceil((s1 - s0) / ARC_STEP_M), math.ceil(math.degrees(turn) / JOINT_DEG))
                pieces += pieces % 2
            else:
                pieces = max(1, math.ceil((s1 - s0) / STRAIGHT_STEP_M - 1e-9))
            for k in range(pieces):
                chain.append(s0 + (s1 - s0) * (k + 1) / pieces)
                kinds.append(kind)
        points, x, z = [(0.0, 0.0)], 0.0, 0.0
        heading = -self.arcs[0][2] / 2 if self.arcs else 0.0
        for (s0, s1), kind in zip(zip(chain, chain[1:]), kinds):
            ds = s1 - s0
            if isinstance(kind, tuple):
                a, b, turn = next(arc for arc in self.arcs if arc[0] - 1e-9 <= s0 and s1 <= arc[1] + 1e-9)
                d_theta = turn * ds / (b - a)
                k = d_theta / ds
                x += (math.sin(heading + d_theta) - math.sin(heading)) / k
                z -= (math.cos(heading + d_theta) - math.cos(heading)) / k
                heading += d_theta
            else:
                x, z = x + math.cos(heading) * ds, z + math.sin(heading) * ds
            points.append((x, z))
        return chain, points

    def _crumble(self, rng):
        """Per side, the lip's inset at each sample: a walk in [CRUMBLE_MIN_M, lip_crumble_m] changing by at most
        CRUMBLE_SLOPE a metre,
        held over each niche's segment, and at a joint at most what keeps the lip within top + lip_crumble_m of the
        centre line (the mitred corner on a bend's outside lies 1 / cos(turn / 2) out)."""
        crumble = {}
        for side in SIDES:
            floor = min(CRUMBLE_MIN_M, self.crumble_m)
            values, c = [], rng.uniform(floor, self.crumble_m)
            for j in range(self.n + 1):
                if j:
                    step = CRUMBLE_SLOPE * self.seg_len[j - 1]
                    c = min(self.crumble_m, max(floor, c + rng.uniform(-step, step)))
                if side == 1 and j - 1 in self.niche_segments:
                    c = values[-1]
                limit = (self.top + self.crumble_m) * math.cos(self.turn[j] / 2) - self.top
                values.append(min(c, limit))
            for i in self.niche_segments if side == 1 else ():
                values[i + 1] = values[i]
            crumble[side] = values
        return crumble

    # points in plan

    def at(self, j, d):
        """The plan point at lateral offset d on sample j's mitre line."""
        m, k = self.mitre(j)
        return (self.points[j][0] + m[0] * d * k, self.points[j][1] + m[1] * d * k)

    def mitre(self, j):
        """(unit mitre direction, the scale that keeps every segment's offset lines at their offset)."""
        if j == 0:
            return self.normal[0], 1.0
        if j == self.n:
            return self.normal[-1], 1.0
        m = _unit2(_add2(self.normal[j - 1], self.normal[j]))
        return m, 1.0 / _dot2(m, self.normal[j])

    def tangent(self, j):
        """The bisecting direction of the run at sample j (the mitre plane's normal)."""
        if j == 0:
            return self.t[0]
        if j == self.n:
            return self.t[-1]
        return _unit2(_add2(self.t[j - 1], self.t[j]))

    def lip(self, j, side):
        return self.side_point(j, side, self.top + self.crumble[side][j], 0.0)

    def foot(self, j, side):
        """The floor's edge at sample j: at the run's ends the floor ends run_in in from the end."""
        return self.side_point(j, side, self.bottom, -self.depth, along=self._end_run(j))

    def side_point(self, j, side, d, y, along=0.0):
        x, z = self.at(j, side * d)
        if along:
            t = self.t[0] if j == 0 else self.t[-1]
            x, z = x + t[0] * along, z + t[1] * along
        return (x, y, z)

    def _end_run(self, j):
        return self.run_in if j == 0 else -self.run_in if j == self.n else 0.0

    def floor_centre(self, j):
        x, z = self.points[j]
        along = self._end_run(j)
        t = self.t[0] if j == 0 else self.t[-1]
        return (x + t[0] * along, -self.depth, z + t[1] * along)

    # the niches

    def niche_frame(self, i):
        """The belt wall's frame on niche segment i: origin at the wall's foot at the segment's start, ex along the
        run, ey up the wall, ez into the soil; and the wall's (u, v) coordinates of the sill, ceiling and lip."""
        foot, lip = self.foot(i, 1), self.lip(i, 1)
        t = self.t[i]
        ex = (t[0], 0.0, t[1])
        ey = _unit(_sub(lip, foot))
        ez = _cross(ex, ey)
        n = self.normal[i]
        if _dot(ez, (n[0], 0.0, n[1])) < 0:
            raise ValueError("the niche's frame faces out of the soil")
        sill = SILL_M / ey[1]
        ceiling = (SILL_M + self.niche["h"]) / ey[1]
        return foot, ex, ey, ez, sill, ceiling, _dot(_sub(lip, foot), ey)

    def _niche_reach(self):
        """The farthest a niche's back reaches from the run's centre line in plan."""
        reach = 0.0
        for i in self.niche_segments:
            foot, ex, ey, ez, sill, ceiling, _ = self.niche_frame(i)
            for v in (sill, ceiling):
                p = _add(foot, _add(_scale(ey, v), _scale(ez, self.niche["d"])))
                reach = max(reach, self.lateral(i, p))
        return reach

    def lateral(self, i, p):
        """The plan offset of a point from segment i's line."""
        o, n = self.points[i], self.normal[i]
        return (p[0] - o[0]) * n[0] + (p[2] - o[1]) * n[1]

    def niche_axis(self, i):
        """The niche's mouth centre chainage offset along segment i: its middle."""
        return self.seg_len[i] / 2

    # holes

    def holes(self):
        """The run's record, then one per niche (design 6)."""
        records = [{"id": "run", "centre_line": [[x, 0.0, z] for x, z in self.points], "top_width_m": 2 * self.top,
                    "depth_m": self.depth, "band_width_m": 2 * self.band}]
        for k, i in enumerate(self.niche_segments):
            o, t, n = self.points[i], self.t[i], self.normal[i]
            a = self.niche_axis(i)
            start = (o[0] + t[0] * a, o[1] + t[1] * a)
            end = (start[0] + n[0] * self.niche_reach_of(i), start[1] + n[1] * self.niche_reach_of(i))
            records.append({"id": f"niche_{k}", "centre_line": [[start[0], 0.0, start[1]], [end[0], 0.0, end[1]]],
                            "top_width_m": self.niche["w"], "depth_m": self.depth - SILL_M,
                            "band_width_m": 2 * (self.niche["w"] / 2 + BAND_MARGIN_M)})
        return records

    def niche_reach_of(self, i):
        foot, ex, ey, ez, sill, ceiling, _ = self.niche_frame(i)
        return max(self.lateral(i, _add(foot, _add(_scale(ey, v), _scale(ez, self.niche["d"])))) for v in (sill, ceiling))

    # the ground's height

    def distance(self, x, z, line=None):
        """Distance in plan to a polyline (the run's by default), its ends included."""
        pts = self.points if line is None else line
        best = math.inf
        for a, b in zip(pts, pts[1:]):
            best = min(best, _seg_dist2((x, z), a, b))
        return best

    def parapet(self, side, r):
        """A parapet's height at plan distance r from its ridge polyline."""
        h = self.h[side]
        crest = self.base / 2 - h / math.tan(self.slope)
        if r <= crest:
            return h
        if r <= self.base / 2:
            return h - (r - crest) * math.tan(self.slope)
        return 0.0

    def relief_weight(self, d):
        """S04's relief's share at plan distance d from the run: none on the berm, RELIEF_PARAPET on the parapets,
        full past them."""
        a = self.toe + RELIEF_CLEAR_M
        if d <= a:
            return 0.0
        if d <= a + RELIEF_RAMP_M:
            return RELIEF_PARAPET * _smooth((d - a) / RELIEF_RAMP_M)
        out = self.toe + self.base
        if d <= out:
            return RELIEF_PARAPET
        return RELIEF_PARAPET + (1 - RELIEF_PARAPET) * _smooth(min(1.0, (d - out) / RELIEF_OUT_M))


# --- the top --------------------------------------------------------------------------------------------------------

def _patch(run, cell=CELL_M):
    xs = [p[0] for p in run.points]
    zs = [p[1] for p in run.points]
    m = run.p["margin_m"]
    x0, x1 = math.floor((min(xs) - m) / CELL_M) * CELL_M, math.ceil((max(xs) + m) / CELL_M) * CELL_M
    z0, z1 = math.floor((min(zs) - m) / CELL_M) * CELL_M, math.ceil((max(zs) + m) / CELL_M) * CELL_M
    nx, nz = round((x1 - x0) / cell), round((z1 - z0) / cell)
    return x0, z0, nx, nz, (x1 - x0) / nx, (z1 - z0) / nz


def _height_fn(run, params):
    relief = mat.relief(seed=params["seed"], size_m=_relief_size(run), cell_m=CELL_M, relief_m=RELIEF_M,
                        azimuth_deg=RELIEF_AZIMUTH_DEG, bias=RELIEF_BIAS)
    x0, z0, nx, nz, cx, cz = _patch(run)
    centre = (x0 + nx * cx / 2, z0 + nz * cz / 2)
    cache = {}

    def height(x, z):
        key = (x, z)
        if key not in cache:
            y = max(run.parapet(side, run.distance(x, z, run.parapet_line[side])) for side in SIDES)
            w = run.relief_weight(run.distance(x, z))
            cache[key] = y + (w * relief(x - centre[0], z - centre[1]) if w else 0.0)
        return cache[key]

    return height


def _relief_size(run):
    x0, z0, nx, nz, cx, cz = _patch(run)
    return max(nx * cx, nz * cz)


def _slot_quads(run):
    """The top slot as convex quads in plan, one per segment, counter-clockwise: right lip i, right lip i+1, left lip
    i+1, left lip i; with their bounding boxes."""
    quads = []
    for i in range(run.n):
        corners = [run.lip(i, -1), run.lip(i + 1, -1), run.lip(i + 1, 1), run.lip(i, 1)]
        poly = [(p[0], p[2]) for p in corners]
        quads.append((poly, (min(p[0] for p in poly), max(p[0] for p in poly),
                             min(p[1] for p in poly), max(p[1] for p in poly))))
    return quads


def _top(run, params, refined):
    """The patch's top: the grid, refined near the run and over the parapets, every cell minus the slot."""
    height = _height_fn(run, params)
    cell = CELL_M if refined else 1.0
    x0, z0, nx, nz, cx, cz = _patch(run, cell)
    uc, vc = x0 + nx * cx / 2, z0 + nz * cz / 2  # the UVs' origin, the patch's centre at either cell size
    mesh = Mesh()
    quads = _slot_quads(run)
    caps = [pts[j] for side in SIDES for pts in (run.parapet_line[side],) for j in (0, -1)]

    def level(i, j):
        if not refined:
            return 1
        xc, zc = x0 + (i + 0.5) * cx, z0 + (j + 0.5) * cz
        d = run.distance(xc, zc) - cx * math.sqrt(2) / 2
        to_cap = min(math.hypot(xc - p[0], zc - p[1]) for p in caps) - cx * math.sqrt(2) / 2
        if to_cap <= CAP_FINE_M:
            return FINE
        if d <= max(FINE_D_M, run.toe + run.base + 0.1) or to_cap <= run.base / 2 + 0.1:
            return MEDIUM
        return 1

    levels = {(i, j): level(i, j) for i in range(nx) for j in range(nz)}
    f = FINE if refined else 1
    vertices, clipped = {}, {}

    def grid_xz(a, b):
        return x0 + a * cx / f, z0 + b * cz / f

    def lowest(cells):
        return min(levels[c] for c in cells if c in levels)

    def grid_height(a, b):
        x, z = grid_xz(a, b)
        on_v, on_h = a % f == 0, b % f == 0
        if on_v and on_h or not (on_v or on_h):
            return height(x, z)
        if on_v:
            i, j = a // f, b // f
            step = f // lowest([(i - 1, j), (i, j)])
            if b % step == 0:
                return height(x, z)
            b0 = b - b % step
            t = (b - b0) / step
            return grid_height(a, b0) * (1 - t) + grid_height(a, b0 + step) * t
        i, j = a // f, b // f
        step = f // lowest([(i, j - 1), (i, j)])
        if a % step == 0:
            return height(x, z)
        a0 = a - a % step
        t = (a - a0) / step
        return grid_height(a0, b) * (1 - t) + grid_height(a0 + step, b) * t

    def vertex(a, b):
        if (a, b) not in vertices:
            x, z = grid_xz(a, b)
            vertices[(a, b)] = mesh.vertex((x, grid_height(a, b), z))
        return vertices[(a, b)]

    def point(x, z):
        key = (round(x, 7), round(z, 7))
        if key not in clipped:
            clipped[key] = mesh.vertex((x, height(x, z), z))
        return clipped[key]

    def tri(ids):
        p = [mesh.vertices[v] for v in ids]
        mesh.face(ids, _up(p), GROUND_SLOT, [(q[0] - uc, q[2] - vc) for q in p])

    for (i, j), lv in sorted(levels.items()):
        step = f // lv
        for a in range(i * f, (i + 1) * f, step):
            for b in range(j * f, (j + 1) * f, step):
                (xa, za), (xb, zb) = grid_xz(a, b), grid_xz(a + step, b + step)
                near = [q for q, (qx0, qx1, qz0, qz1) in quads if qx0 < xb and qx1 > xa and qz0 < zb and qz1 > za]
                corners = [(a, b), (a + step, b), (a + step, b + step), (a, b + step)]
                if not near:
                    ids = [vertex(*c) for c in corners]
                    diagonal = ((a // step) + (b // step)) % 2 == 0
                    for t3 in ((ids[0], ids[1], ids[2]), (ids[0], ids[2], ids[3])) if diagonal else \
                            ((ids[0], ids[1], ids[3]), (ids[1], ids[2], ids[3])):
                        tri(t3)
                    continue
                pieces = [[grid_xz(*c) for c in corners]]
                for q in near:
                    pieces = [part for piece in pieces for part in _subtract(piece, q)]
                for piece in pieces:
                    ids = []
                    for x, z in piece:
                        a_f, b_f = (x - x0) / (cx / f), (z - z0) / (cz / f)
                        if abs(a_f - round(a_f)) < 1e-9 and abs(b_f - round(b_f)) < 1e-9:
                            ids.append(vertex(round(a_f), round(b_f)))
                        else:
                            ids.append(point(x, z))
                    ids = _dedupe(ids)
                    for k in range(1, len(ids) - 1):
                        t3 = (ids[0], ids[k], ids[k + 1])
                        if _area3(mesh, t3) > 1e-10:
                            tri(t3)
    mesh.rim = {"x0": x0, "z0": z0, "nx": nx, "nz": nz, "cx": cx, "cz": cz,
                "edge": {k: vertices[k] for k in vertices if k[0] in (0, nx * f) or k[1] in (0, nz * f)}, "f": f}
    return mesh


def _subtract(poly, quad):
    """The parts of a convex polygon outside a convex counter-clockwise quad, as convex polygons (slivers dropped)."""
    out, rest = [], poly
    for a, b in zip(quad, quad[1:] + quad[:1]):
        outside = _clip(rest, a, b, keep_left=False)
        if _area2(outside) > 1e-10:
            out.append(outside)
        rest = _clip(rest, a, b, keep_left=True)
        if _area2(rest) <= 1e-10:
            return out
    return out


def _clip(poly, a, b, keep_left):
    """Sutherland-Hodgman against the line a-b: the part left of it (inside a counter-clockwise polygon) or right."""
    def h(p):
        v = _cross2(_sub2(b, a), _sub2(p, a))
        return v if keep_left else -v
    out = []
    for p, q in zip(poly, poly[1:] + poly[:1]):
        hp, hq = h(p), h(q)
        if hp >= -EPS:
            out.append(p)
        if (hp > EPS and hq < -EPS) or (hp < -EPS and hq > EPS):
            t = hp / (hp - hq)
            out.append((p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1])))
    return out


def _dedupe(ids):
    out = []
    for v in ids:
        if not out or out[-1] != v:
            out.append(v)
    if len(out) > 1 and out[0] == out[-1]:
        out.pop()
    return out


def _merge(target, source):
    """Appends a mesh's vertices and faces to another."""
    base = len(target.vertices)
    target.vertices += source.vertices
    target.faces += [([v + base for v in ids], n, uvs, slot) for ids, n, uvs, slot in source.faces]
    target.rim = getattr(source, "rim", None)


def _block(mesh, run):
    """The patch's rim walls down to the block's bottom and its bottom, in the section slot (a soil section: the
    wall map's topsoil band over its subsoil; never a face of the cut): the top's rim vertices walked round the patch."""
    rim = mesh.rim
    f, nx, nz = rim["f"], rim["nx"], rim["nz"]
    edge = rim["edge"]
    keys = sorted(edge)
    y = -run.block
    loops = (
        (sorted(k for k in keys if k[1] == 0), (0.0, 0.0, -1.0)),
        (sorted(k for k in keys if k[1] == nz * f), (0.0, 0.0, 1.0)),
        (sorted((k for k in keys if k[0] == 0), key=lambda k: k[1]), (-1.0, 0.0, 0.0)),
        (sorted((k for k in keys if k[0] == nx * f), key=lambda k: k[1]), (1.0, 0.0, 0.0)),
    )
    squeeze = (strip.BELT_ROW[1] - strip.BELT_ROW[0] - 0.05) / run.block  # the block's depth into one wall row

    def uv(p):  # a soil section: the belt wall's row, metres along the rim (the map repeats), squeezed down
        return strip.wall_uv(1, p[0] + p[2], -p[1] * squeeze)

    for ks, normal in loops:
        for ka, kb in zip(ks, ks[1:]):
            a, b = edge[ka], edge[kb]
            pa, pb = mesh.vertices[a], mesh.vertices[b]
            corners = [pa, pb, (pb[0], y, pb[2]), (pa[0], y, pa[2])]
            mesh.face([a, b, mesh.vertex(corners[2]), mesh.vertex(corners[3])], normal, SECTION_SLOT,
                      [uv(p) for p in corners])
    x0, z0, x1, z1 = rim["x0"], rim["z0"], rim["x0"] + nx * rim["cx"], rim["z0"] + nz * rim["cz"]
    base = [mesh.vertex((x0, y, z0)), mesh.vertex((x1, y, z0)), mesh.vertex((x1, y, z1)), mesh.vertex((x0, y, z1))]
    mesh.face(base, (0.0, -1.0, 0.0), SECTION_SLOT, [strip.floor_uv(0.0, 0.0)] * 4)


# --- the cavity -----------------------------------------------------------------------------------------------------

def _cavity(mesh, run, niches):
    """The cut's faces in the wall slot: per segment the floor and two walls (two triangles each: a crumbled lip
    twists the quad), the end walls, and on a niche's segment the belt wall round the mouth and the niche's five
    faces. UVs from lib.strip."""
    for i in range(run.n):
        s0, s1 = run.chainage[i], run.chainage[i + 1]
        f = [run.foot(i, -1), run.foot(i, 1), run.foot(i + 1, 1), run.foot(i + 1, -1)]
        u0 = s0 + (run.run_in if i == 0 else 0.0)
        u1 = s1 - (run.run_in if i == run.n - 1 else 0.0)
        uv = [strip.floor_uv(u0, -run.bottom), strip.floor_uv(u0, run.bottom), strip.floor_uv(u1, run.bottom),
              strip.floor_uv(u1, -run.bottom)]
        mesh.face([mesh.vertex(p) for p in f], (0.0, 1.0, 0.0), WALL_SLOT, uv)
        for side in SIDES:
            if niches and side == 1 and i in run.niche_segments:
                _niche_wall(mesh, run, i)
                continue
            lip0, lip1, foot1, foot0 = run.lip(i, side), run.lip(i + 1, side), run.foot(i + 1, side), run.foot(i, side)
            inward = (-side * run.normal[i][0], 0.0, -side * run.normal[i][1])
            ids = [mesh.vertex(p) for p in (lip0, lip1, foot1, foot0)]
            uv = [strip.wall_uv(side, s0, 0.0), strip.wall_uv(side, s1, 0.0), strip.wall_uv(side, u1, run.depth),
                  strip.wall_uv(side, u0, run.depth)]
            for a, b, c in ((0, 1, 2), (0, 2, 3)):
                mesh.face([ids[a], ids[b], ids[c]], _facing(mesh, [ids[a], ids[b], ids[c]], inward), WALL_SLOT,
                          [uv[a], uv[b], uv[c]])
    for j, sign in ((0, 1), (run.n, -1)):
        t = run.t[0] if j == 0 else run.t[-1]
        inward = (sign * t[0], 0.0, sign * t[1])
        corners = [run.lip(j, -1), run.lip(j, 1), run.foot(j, 1), run.foot(j, -1)]
        u = strip.END_U_M + strip.END_SLOT_M * (0.5 if j == 0 else 1.5)
        uv = [strip.wall_uv(1, u - run.top - run.crumble[-1][j], 0.0),
              strip.wall_uv(1, u + run.top + run.crumble[1][j], 0.0),
              strip.wall_uv(1, u + run.bottom, run.depth), strip.wall_uv(1, u - run.bottom, run.depth)]
        ids = [mesh.vertex(p) for p in corners]
        mesh.face(ids, _facing(mesh, ids[:3], inward), WALL_SLOT, uv)  # battered: its own plane's normal


def _niche_wall(mesh, run, i):
    """The belt wall of niche segment i round the mouth, and the niche's sill, ceiling, back and sides."""
    foot, ex, ey, ez, sill, ceiling, top = run.niche_frame(i)
    length = run.seg_len[i]
    a0 = run.niche_axis(i) - run.niche["w"] / 2
    a1 = a0 + run.niche["w"]
    depth = run.niche["d"]
    s0 = run.chainage[i]

    def p(u, v, w=0.0):
        return _add(foot, _add(_scale(ex, u), _add(_scale(ey, v), _scale(ez, w))))

    def wall_uv(u, v):
        return strip.wall_uv(1, s0 + u, run.depth - v * ey[1])

    into = _scale(ez, -1.0)
    for (ua, ub), (va, vb) in (((0.0, length), (0.0, sill)), ((0.0, length), (ceiling, top)),
                               ((0.0, a0), (sill, ceiling)), ((a1, length), (sill, ceiling))):
        corners = [(ua, va), (ub, va), (ub, vb), (ua, vb)]
        mesh.face([mesh.vertex(p(u, v)) for u, v in corners], into, WALL_SLOT, [wall_uv(u, v) for u, v in corners])
    up, down = ey, _scale(ey, -1.0)
    faces = (
        ([(a0, sill, 0), (a1, sill, 0), (a1, sill, depth), (a0, sill, depth)], up),             # the sill
        ([(a0, ceiling, 0), (a1, ceiling, 0), (a1, ceiling, depth), (a0, ceiling, depth)], down),  # the ceiling
        ([(a0, sill, depth), (a1, sill, depth), (a1, ceiling, depth), (a0, ceiling, depth)], into),  # the back
        ([(a0, sill, 0), (a0, sill, depth), (a0, ceiling, depth), (a0, ceiling, 0)], ex),        # the near side
        ([(a1, sill, 0), (a1, sill, depth), (a1, ceiling, depth), (a1, ceiling, 0)], _scale(ex, -1.0)),  # the far side
    )
    for k, (corners, normal) in enumerate(faces):
        if k < 2:  # the sill and the ceiling: the wall's map folded into the niche, towards its middle
            uv = [wall_uv(u, v + (w if k == 0 else -w) * 0.5) for u, v, w in corners]
        elif k == 2:
            uv = [wall_uv(u, v) for u, v, w in corners]
        else:  # the sides: the wall's map at the side's chainage, laid on into the niche
            uv = [wall_uv(u + (w if k == 3 else -w), v) for u, v, w in corners]
        mesh.face([mesh.vertex(p(u, v, w)) for u, v, w in corners], normal, WALL_SLOT, uv)


# --- clods ----------------------------------------------------------------------------------------------------------

def _clods(params, run):
    """Clods on the floor: clods_per_m per metre, each a low sphere cap (a sphere of radius r sunk so a cap h high
    and CLOD_D_M across stands on the floor, its sphere within the floor box), clear of the walls and of every
    cross-section's plane by CLOD_CLEAR_M and of each other."""
    rng = random.Random(f"trench clods {params['seed']}")
    count = round(params["clods_per_m"] * run.length)
    clods = []
    for _ in range(count):
        for _ in range(CLOD_TRIES):
            i = rng.choices(range(run.n), weights=run.seg_len)[0]
            width = rng.uniform(*CLOD_D_M)
            a = width / 2
            h = max(width * rng.uniform(*CLOD_H), a * a / FILLER_M + 0.005)
            lo = (run.run_in if i == 0 else 0.0) + a + CLOD_CLEAR_M
            hi = run.seg_len[i] - (run.run_in if i == run.n - 1 else 0.0) - a - CLOD_CLEAR_M
            if hi <= lo or run.bottom - a - CLOD_CLEAR_M <= 0:
                continue
            u = rng.uniform(lo, hi)
            d = rng.uniform(-(run.bottom - a - CLOD_CLEAR_M), run.bottom - a - CLOD_CLEAR_M)
            o, t, n = run.points[i], run.t[i], run.normal[i]
            x, z = o[0] + t[0] * u + n[0] * d, o[1] + t[1] * u + n[1] * d
            if any(math.hypot(x - c["x"], z - c["z"]) < a + c["a"] + CLOD_CLEAR_M for c in clods):
                continue
            if not _clear_of_sections(run, i, x, z, a):
                continue
            r = (a * a + h * h) / (2 * h)
            clods.append({"x": x, "z": z, "a": a, "h": h, "r": r, "y": -run.depth + h - r, "u": run.chainage[i] + u,
                          "side": 1 if d > 0 else -1, "seed": rng.random()})
            break
    return clods


def _clear_of_sections(run, i, x, z, a):
    for j in (i, i + 1):
        if 0 < j < run.n:
            t = run.tangent(j)
            if abs((x - run.points[j][0]) * t[0] + (z - run.points[j][1]) * t[1]) < a + CLOD_CLEAR_M:
                return False
    return True


def _clod_mesh(mesh, clod):
    """A clod's cap: an apex and CLOD_RINGS rings of CLOD_AROUND vertices on its sphere, each off it by up to
    CLOD_NOISE_M but the rim, which lies on the floor; UVs in the spill band."""
    rng = random.Random(clod["seed"])
    r, centre = clod["r"], (clod["x"], clod["y"], clod["z"])
    rim = math.acos(max(-1.0, min(1.0, (r - clod["h"]) / r)))
    spin = rng.uniform(0, 2 * math.pi)
    rings = []
    for k in range(1, CLOD_RINGS + 1):
        theta = rim * k / CLOD_RINGS
        ring = []
        for m in range(CLOD_AROUND):
            phi = spin + 2 * math.pi * m / CLOD_AROUND
            rr = r + (0.0 if k == CLOD_RINGS else rng.uniform(-CLOD_NOISE_M, CLOD_NOISE_M))
            direction = (math.sin(theta) * math.cos(phi), math.cos(theta), math.sin(theta) * math.sin(phi))
            ring.append(_add(centre, _scale(direction, rr)))
        rings.append(ring)
    apex = _add(centre, (0.0, r + rng.uniform(-CLOD_NOISE_M, CLOD_NOISE_M), 0.0))

    def uv(p):
        return strip.spill_uv(clod["side"], clod["u"] + (p[0] - clod["x"]) + (p[2] - clod["z"]), p[1] + clod["h"] - clod["y"] - r)

    top = mesh.vertex(apex)
    ids = [[mesh.vertex(p) for p in ring] for ring in rings]
    for m in range(CLOD_AROUND):
        q = (m + 1) % CLOD_AROUND
        tri = [top, ids[0][m], ids[0][q]]
        mesh.face(tri, _outward(mesh, tri, centre), CLOD_SLOT, [uv(mesh.vertices[v]) for v in tri])
        for k in range(CLOD_RINGS - 1):
            for t3 in ((ids[k][m], ids[k + 1][m], ids[k + 1][q]), (ids[k][m], ids[k + 1][q], ids[k][q])):
                mesh.face(list(t3), _outward(mesh, t3, centre), CLOD_SLOT, [uv(mesh.vertices[v]) for v in t3])


def _floor_straws(params, run, clods, micro):
    """Blown-in straws and twigs on the floor at FLOOR_STRAW of S04's densities (design 3), lying flat on it, both
    ends STRAW_CLEAR_M inside the floor and off the clods; visual only (AST-10)."""
    rng = random.Random(f"trench straw {params['seed']}")
    kit = straw.build_kit(KIT, rng)
    lengths = {kind: [m.bounds()[1][0] - m.bounds()[0][0] for m in meshes] for kind, meshes in kit.items()}
    count = 0
    for kind, density in (("straw", S04_STRAW_PER_M2 * FLOOR_STRAW), ("twig", S04_TWIG_PER_M2 * FLOOR_STRAW)):
        for i in range(run.n):
            area = run.seg_len[i] * 2 * run.bottom
            number = int(density * area + rng.random())
            for _ in range(number):
                for _ in range(20):
                    k = rng.randrange(len(kit[kind]))
                    half = lengths[kind][k] / 2
                    u = rng.uniform(0, run.seg_len[i])
                    d = rng.uniform(-run.bottom, run.bottom)
                    o, t, n = run.points[i], run.t[i], run.normal[i]
                    x, z = o[0] + t[0] * u + n[0] * d, o[1] + t[1] * u + n[1] * d
                    yaw = math.atan2(t[1], t[0]) + rng.uniform(-0.6, 0.6)
                    ends = [(x + c * math.cos(yaw) * half, z + c * math.sin(yaw) * half) for c in (-1, 0, 1)]
                    if all(_on_floor(run, e) for e in ends) and not any(
                            math.hypot(e[0] - c["x"], e[1] - c["z"]) < c["a"] + 0.01 for e in ends for c in clods):
                        radius = (KIT["twig_d_mm"]["max"] if kind == "twig" else KIT["straw_d_mm"]["max"]) / 2000
                        straw.place(micro, kit[kind][k], (x, -run.depth + radius, z),
                                    straw.lying_axes(yaw, 0.0, rng.uniform(0, 2 * math.pi)))
                        count += 1
                        break
    return count


def _on_floor(run, p):
    """Whether a plan point lies on the floor STRAW_CLEAR_M in from its edges and ends."""
    best, along = math.inf, None
    for i in range(run.n):
        a, b = run.points[i], run.points[i + 1]
        d = _seg_dist2(p, a, b)
        if d < best:
            best = d
            along = run.chainage[i] + max(0.0, min(run.seg_len[i], _dot2(_sub2(p, a), run.t[i])))
    return best <= run.bottom - STRAW_CLEAR_M and run.run_in + STRAW_CLEAR_M <= along <= run.length - run.run_in - STRAW_CLEAR_M


# --- collision ------------------------------------------------------------------------------------------------------

def _collision(run, clods):
    """The shapes in the module docstring's order, and the per-joint F-5 lines: at every joint, how far the cut's
    boxes of the segments before and after it reach past its mitre plane (the least over each box's end face)."""
    shapes, joints = [], []
    cut_boxes = {}  # segment -> its cut boxes that run along it

    for i in range(run.n):
        o = run.points[i]
        t = run.t[i]
        ex, ey = (t[0], 0.0, t[1]), (0.0, 1.0, 0.0)
        ez = _cross(ex, ey)
        spread = run.bottom + FLOOR_SPREAD_M
        origin = (o[0], -run.depth, o[1])
        v, w = (-(run.block - run.depth), 0.0), (-spread, spread)
        u0, u1 = _span(run, i, origin, ex, ey, ez, v, w)
        segment = [_box(origin, ex, ey, ez, (u0, v[0], w[0]), (u1, v[1], w[1]), CUT, False)]
        for side in SIDES:
            if side == 1 and i in run.niche_segments:
                niche, along = _niche_boxes(run, i)
                segment += along
                shapes_niche = niche
                continue
            segment.append(_wall_box(run, i, side))
            segment.append(_lip_box(run, i, side, -run.block, 0.0))
        shapes += segment
        if i in run.niche_segments:
            shapes += shapes_niche
        cut_boxes[i] = segment
    for j in (0, run.n):
        shapes.append(_end_wall(run, j))
    for j in (0, run.n):
        shapes.append(_end_block(run, j))
    shapes += _parapets(run)
    shapes += _caps(run)
    shapes += _columns(run)
    shapes += [{"shape": "sphere", "center": [c["x"], c["y"], c["z"]], "radius": c["r"], "material": SPOIL,
                "landable": False} for c in clods]
    for j in range(1, run.n):
        before = min(_reach(run, j, box, 1.0) for box in cut_boxes[j - 1])
        after = min(_reach(run, j, box, -1.0) for box in cut_boxes[j])
        overlap = before + after
        if overlap < 2 * OVERLAP_M - 1e-6:
            raise ValueError(f"joint {j}: the cut's boxes overlap {overlap:.3f} m, under {2 * OVERLAP_M} m (F-5)")
        joints.append(f"F-5 joint {j} at {run.chainage[j]:.2f} m (turn {math.degrees(run.turn[j]):+.2f} deg): the cut's "
                      f"boxes reach {before:.3f} m and {after:.3f} m past it, overlap {overlap:.3f} m")
    return shapes, joints


def _span(run, i, origin, ex, ey, ez, v, w):
    """(u0, u1) along ex of a box of segment i, its frame at `origin` and its extents v along ey and w along ez: every
    corner of each end face OVERLAP_M past that end's mitre plane (the run's end plane at its ends), so two
    neighbouring segments' boxes overlap 0.2 m across every line through their joint (F-5), the wedge on a bend's
    outside included; on its inside a face reaches 0.1 m sin(turn) at most into the cut."""
    out = []
    for j, sign in ((i, -1.0), (i + 1, 1.0)):
        tb = run.tangent(j)
        tb = (tb[0], 0.0, tb[1])
        base = _dot(_sub(origin, (run.points[j][0], 0.0, run.points[j][1])), tb)
        a = _dot(ex, tb)
        values = [(sign * OVERLAP_M - base - p * _dot(ey, tb) - q * _dot(ez, tb)) / a for p in v for q in w]
        out.append(min(values) if sign < 0 else max(values))
    return out[0], out[1]


def _reach(run, j, box, sign):
    """How far a box reaches past joint j's mitre plane: towards the next segment (sign +1, a box of the segment
    before) or the one before (-1): the least over the four corners of its end face beyond the plane."""
    tb = run.tangent(j)
    tb = (tb[0], 0.0, tb[1])
    p = (run.points[j][0], 0.0, run.points[j][1])
    distances = sorted(sign * _dot(_sub(c, p), tb) for c in _corners(box))
    return distances[4]  # the four farthest corners are the end face (the box is longer than it is skewed)


def _corners(box):
    x, y, z, w = box["rotation"]
    out = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                r = _rotate((x, y, z, w), (sx * box["size"][0] / 2, sy * box["size"][1] / 2, sz * box["size"][2] / 2))
                out.append(_add(box["center"], r))
    return out


def _rotate(q, v):
    """v rotated by the unit quaternion [x, y, z, w] (Hamilton, as Unity and lib.audit apply it)."""
    x, y, z, w = q
    tx, ty, tz = 2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0])
    return (v[0] + w * tx + (y * tz - z * ty), v[1] + w * ty + (z * tx - x * tz), v[2] + w * tz + (x * ty - y * tx))


def _wall_box(run, i, side):
    """A wall's box: its face the best plane of the wall's quad (a crumbled lip twists it by under a centimetre),
    FILLER_M into the soil, from WALL_DOWN_M under the floor to the lip, past both joints (_span)."""
    lip0, lip1, foot1, foot0 = run.lip(i, side), run.lip(i + 1, side), run.foot(i + 1, side), run.foot(i, side)
    corners = [lip0, lip1, foot1, foot0]
    centre = tuple(sum(p[k] for p in corners) / 4 for k in range(3))
    normal = _unit(_newell(corners))
    n2 = run.normal[i]
    if _dot(normal, (side * n2[0], 0.0, side * n2[1])) < 0:
        normal = _scale(normal, -1.0)
    ey = _unit(_sub((0.0, 1.0, 0.0), _scale(normal, normal[1])))
    ex = _cross(ey, normal)
    t = run.t[i]
    if _dot(ex, (t[0], 0.0, t[1])) < 0:
        ex, ez, w = _scale(ex, -1.0), _scale(normal, -1.0), (-FILLER_M, 0.0)
    else:
        ez, w = normal, (0.0, FILLER_M)
    v_values = [_dot(_sub(p, centre), ey) for p in corners]
    v = (min(v_values[2:]) - WALL_DOWN_M / ey[1], max(v_values[:2]))
    u0, u1 = _span(run, i, centre, ex, ey, ez, v, w)
    return _box(centre, ex, ey, ez, (u0, v[0], w[0]), (u1, v[1], w[1]), CUT, False)


def _lip_box(run, i, side, y0, y1):
    """A lip box: level, its inner face on the lip line, out to the side's backing, from y0 to y1, past both joints
    (_span)."""
    lip0, lip1 = run.lip(i, side), run.lip(i + 1, side)
    ex = _unit((lip1[0] - lip0[0], 0.0, lip1[2] - lip0[2]))
    ey = (0.0, 1.0, 0.0)
    ez = _cross(ex, ey)
    n = run.normal[i]
    back = run.backing[side] - run.top - min(run.crumble[side][i], run.crumble[side][i + 1])
    w = (0.0, back) if _dot(ez, (side * n[0], 0.0, side * n[1])) > 0 else (-back, 0.0)
    u0, u1 = _span(run, i, lip0, ex, ey, ez, (y0, y1), w)
    return _box(lip0, ex, ey, ez, (u0, y0, w[0]), (u1, y1, w[1]), CUT, False)


def _niche_boxes(run, i):
    """Niche segment i's belt side, as (the niche's own boxes, the boxes along the segment): the wall below the sill
    and above the ceiling and the lip box above and below the niche run along the segment past both joints (_span);
    the niche's sill, ceiling, back and side boxes in the wall's frame, and the lip box's middle behind the back and
    beside the niche, are its own. Every box FILLER_M thick at least."""
    foot, ex, ey, ez, sill, ceiling, top = run.niche_frame(i)
    length = run.seg_len[i]
    a0 = run.niche_axis(i) - run.niche["w"] / 2
    a1 = a0 + run.niche["w"]
    d = run.niche["d"]
    over = 0.2  # the niche's boxes reach this past it along the run (into the pillars and the neighbours)
    lo_v = -WALL_DOWN_M / ey[1]
    along = []
    for v in ((lo_v, sill), (ceiling, top)):  # the wall below the sill and above the ceiling
        u0, u1 = _span(run, i, foot, ex, ey, ez, v, (0.0, FILLER_M))
        along.append(_box(foot, ex, ey, ez, (u0, v[0], 0.0), (u1, v[1], FILLER_M), CUT, False))
    shapes = [
        _box(foot, ex, ey, ez, (a0 - over, sill - FILLER_M, 0.0), (a1 + over, sill, d + FILLER_M), CUT, False),  # sill
        _box(foot, ex, ey, ez, (a0 - over, ceiling, 0.0), (a1 + over, ceiling + FILLER_M, d + FILLER_M), CUT, False),
        _box(foot, ex, ey, ez, (a0 - over, sill - over, d), (a1 + over, ceiling + over, d + FILLER_M), CUT, False),  # back
        _box(foot, ex, ey, ez, (a0 - FILLER_M, sill - over, 0.0), (a0, ceiling + over, d + FILLER_M), CUT, False),  # side
        _box(foot, ex, ey, ez, (a1, sill - over, 0.0), (a1 + FILLER_M, ceiling + over, d + FILLER_M), CUT, False),
    ]

    def y(v, w):
        return _add(foot, _add(_scale(ey, v), _scale(ez, w)))[1]

    y_ceiling = (y(ceiling, 0.0) + y(ceiling + FILLER_M, d + FILLER_M)) / 2
    y_sill = (y(sill, d) + y(sill - FILLER_M, 0.0)) / 2
    along.append(_lip_box(run, i, 1, y_ceiling, 0.0))
    along.append(_lip_box(run, i, 1, -run.block, y_sill))
    # behind the back box, and beside the niche: the lip box's middle, cut at the niche's sides
    lip0 = run.lip(i, 1)
    behind = run.lateral(i, _add(foot, _add(_scale(ey, ceiling), _scale(ez, d + FILLER_M / 2))))
    t = run.t[i]
    e = (t[0], 0.0, t[1])
    side_axis = _cross(e, (0.0, 1.0, 0.0))  # the run's left, the belt side
    back = run.backing[1]
    origin = (run.points[i][0], 0.0, run.points[i][1])
    # behind the back box all is soil: the block reaches into the lip box's parts above and below
    shapes.append(_box(origin, e, (0.0, 1.0, 0.0), side_axis, (a0 - over, y_sill - FILLER_M / 2, behind),
                       (a1 + over, y_ceiling + FILLER_M / 2, back), CUT, False))
    lip_off = run.lateral(i, lip0)
    for u0, u1 in ((-OVERLAP_M - 0.2, a0), (a1, length + OVERLAP_M + 0.2)):
        shapes.append(_box(origin, e, (0.0, 1.0, 0.0), side_axis, (u0, y_sill, lip_off), (u1, y_ceiling, back), CUT, False))
    return shapes, along


def _end_wall(run, j):
    """An end wall's box: its face the end wall's plane, FILLER_M into the soil, across the cut and OVERLAP_M past
    its lips, from WALL_DOWN_M under the floor to the lip."""
    t = run.t[0] if j == 0 else run.t[-1]
    n = run.normal[0] if j == 0 else run.normal[-1]
    top_mid = (run.points[j][0], 0.0, run.points[j][1])
    foot_mid = run.floor_centre(j)
    ex = (n[0], 0.0, n[1])
    ey = _unit(_sub(top_mid, foot_mid))
    ez = _cross(ex, ey)
    soil = (-t[0], 0.0, -t[1]) if j == 0 else (t[0], 0.0, t[1])
    w = (0.0, FILLER_M) if _dot(ez, soil) > 0 else (-FILLER_M, 0.0)
    half = run.top + run.crumble_m + OVERLAP_M
    height = _dist(top_mid, foot_mid)
    return _box(foot_mid, ex, ey, ez, (-half, -WALL_DOWN_M / ey[1], w[0]), (half, height, w[1]), CUT, False)


def _end_block(run, j):
    """The level block beyond a run's end: from its end back BACKING_M past the band's end, out to each side's
    backing, from the block's bottom to y = 0."""
    t = run.t[0] if j == 0 else run.t[-1]
    origin = (run.points[j][0], 0.0, run.points[j][1])
    ex = (t[0], 0.0, t[1])
    ez = _cross(ex, (0.0, 1.0, 0.0))
    n = run.normal[0] if j == 0 else run.normal[-1]
    left = 1.0 if _dot(ez, (n[0], 0.0, n[1])) > 0 else -1.0
    lat = sorted((left * run.backing[1], -left * run.backing[-1]))
    back = run.band + BACKING_M
    along = (-back, 0.0) if j == 0 else (0.0, back)
    return _box(origin, ex, (0.0, 1.0, 0.0), ez, (along[0], -run.block, lat[0]), (along[1], 0.0, lat[1]), CUT, False)


def _parapets(run):
    """Per straight stretch or arc piece, per side: the inner slope, the crest and the outer slope."""
    shapes = []
    stretches, start = [], 0
    for j in range(1, run.n + 1):
        if j == run.n or abs(run.turn[j]) > 1e-9:
            stretches.append((start, j))
            start = j
    for a, b in stretches:
        o = run.points[a]
        t, n = run.t[a], run.normal[a]
        length = sum(run.seg_len[a:b])
        for side in SIDES:
            h = run.h[side]
            if h <= 0:
                continue
            reach = run.toe + run.base

            def e(j):
                if j in (0, run.n):
                    return PARAPET_END_M
                return OVERLAP_M + (reach * math.tan(abs(run.turn[j]) / 2) if side * run.turn[j] < 0 else 0.0)

            u0, u1 = -e(a), length + e(b)
            run_in = h / math.tan(run.slope)
            ex = (t[0], 0.0, t[1])
            lat = (side * n[0], 0.0, side * n[1])
            c, s = math.cos(run.slope), math.sin(run.slope)
            toe = _add((o[0], 0.0, o[1]), _scale(lat, run.toe))
            up_slope = _add(_scale(lat, c), (0.0, s, 0.0))
            shapes.append(_slab(toe, ex, up_slope, (u0, u1), (0.0, h / s)))
            crest_in, crest_out = run.toe + run_in, run.toe + run.base - run_in
            ez = _cross(ex, (0.0, 1.0, 0.0))
            sgn = 1.0 if _dot(ez, lat) > 0 else -1.0
            lo, hi = sorted((sgn * crest_in, sgn * crest_out))
            shapes.append(_box((o[0], 0.0, o[1]), ex, (0.0, 1.0, 0.0), ez, (u0, -CREST_DOWN_M, lo), (u1, h, hi), GROUND, True))
            crest = _add(_add((o[0], 0.0, o[1]), _scale(lat, crest_out)), (0.0, h, 0.0))
            down_slope = _add(_scale(lat, c), (0.0, -s, 0.0))
            shapes.append(_slab(crest, ex, down_slope, (u0, u1), (0.0, h / s)))
    return shapes


def _slab(origin, ex, along, u, v):
    """A parapet slope's box: its top face the plane through `origin` spanned by ex (u) and the unit slope
    direction `along` (v), PARAPET_T_M under it."""
    normal = _cross(ex, along)
    if normal[1] < 0:
        normal = _scale(normal, -1.0)
    ey = _unit(along)
    ex2 = _cross(ey, normal)
    flip = _dot(ex2, ex) < 0
    ex2 = _scale(ex2, -1.0) if flip else ex2
    ez = normal if not flip else _scale(normal, -1.0)
    w = (-PARAPET_T_M, 0.0) if not flip else (0.0, PARAPET_T_M)
    return _box(origin, ex2, ey, ez, (u[0], v[0], w[0]), (u[1], v[1], w[1]), GROUND, True)


def _caps(run):
    """Each parapet's end cone, round its ridge's end: CAP_SECTORS slope boxes over the half turned away from the
    run, and a crest cylinder."""
    shapes = []
    c, s = math.cos(run.slope), math.sin(run.slope)
    for side in SIDES:
        h = run.h[side]
        if h <= 0:
            continue
        crest = run.base / 2 - h / math.tan(run.slope)
        for j, back in ((0, -1.0), (run.n, 1.0)):
            pivot = run.parapet_line[side][j]
            t = run.t[0] if j == 0 else run.t[-1]
            n = run.normal[0] if j == 0 else run.normal[-1]
            b = (back * t[0], back * t[1])
            width = run.base / 2 * math.tan(math.pi / (2 * CAP_SECTORS)) + CAP_SEAM_M
            for k in range(CAP_SECTORS):
                a = -math.pi / 2 + (k + 0.5) * math.pi / CAP_SECTORS
                u = (b[0] * math.cos(a) + n[0] * math.sin(a), b[1] * math.cos(a) + n[1] * math.sin(a))
                radial = (u[0], 0.0, u[1])
                tangent = _cross((0.0, 1.0, 0.0), radial)
                origin = (pivot[0] + u[0] * crest, h, pivot[1] + u[1] * crest)
                down = _add(_scale(radial, c), (0.0, -s, 0.0))
                shapes.append(_slab(origin, tangent, down, (-width, width), (0.0, h / s)))
            shapes.append({"shape": "cylinder", "center": [pivot[0], (h - CREST_DOWN_M) / 2, pivot[1]], "radius": crest,
                           "height": h + CREST_DOWN_M, "rotation": [0.0, 0.0, 0.0, 1.0], "material": GROUND,
                           "landable": True})
    return shapes


def _columns(run):
    """The margin columns: axis-aligned boxes COLUMN_M wide across the patch from the block's bottom to y = 0, each
    stopping CLEAR_M from the run's centre line on either side; a column whose inner face would reach past the lip
    boxes' backing is halved until it does not."""
    x0, z0, nx, nz, cx, cz = _patch(run)
    x1, z1 = x0 + nx * cx, z0 + nz * cz
    dense = _densify(run.points, 0.05)
    limit = min(run.backing.values()) - 0.05
    shapes = []

    def column(xa, xb, depth=0):
        hits = [(q[1], math.sqrt(CLEAR_M ** 2 - dx * dx)) for q in dense
                for dx in (max(xa - q[0], 0.0, q[0] - xb),) if dx < CLEAR_M]
        if not hits:
            spans = [(z0, z1)]
        else:
            lo = min(z - r for z, r in hits)
            hi = max(z + r for z, r in hits)
            if depth < 6 and any(run.distance(x, zz) > limit for zz in (lo, hi)
                                 for x in _steps(xa, xb, 0.05)):
                mid = (xa + xb) / 2
                column(xa, mid + COLUMN_SEAM_M / 2, depth + 1)
                column(mid - COLUMN_SEAM_M / 2, xb, depth + 1)
                return
            spans = [(z0, lo), (hi, z1)]
        for za, zb in spans:
            if zb - za > 1e-6:
                shapes.append(_box(((xa + xb) / 2, -run.block / 2, (za + zb) / 2), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0),
                                   (0.0, 0.0, 1.0), (-(xb - xa) / 2, -run.block / 2, -(zb - za) / 2),
                                   ((xb - xa) / 2, run.block / 2, (zb - za) / 2), GROUND, True))

    count = max(1, round((x1 - x0) / COLUMN_M))
    width = (x1 - x0) / count
    for k in range(count):
        column(x0 + k * width - (COLUMN_SEAM_M / 2 if k else 0.0),
               x0 + (k + 1) * width + (COLUMN_SEAM_M / 2 if k < count - 1 else 0.0))
    return shapes


def _densify(points, step):
    out = []
    for a, b in zip(points, points[1:]):
        n = max(1, math.ceil(_dist2(a, b) / step))
        out += [(a[0] + (b[0] - a[0]) * k / n, a[1] + (b[1] - a[1]) * k / n) for k in range(n)]
    return out + [points[-1]]


def _steps(a, b, step):
    n = max(1, math.ceil((b - a) / step))
    return [a + (b - a) * k / n for k in range(n + 1)]


def _box(origin, ex, ey, ez, lo, hi, material, landable):
    """A box from a right-handed orthonormal frame (ez = ex x ey) at `origin` and its extents lo..hi along the axes,
    as a metadata shape with its rotation from the frame."""
    if _dist(_cross(ex, ey), ez) > 1e-6:
        if _dist(_cross(ex, ey), _scale(ez, -1.0)) > 1e-6:
            raise ValueError("a box's frame is not orthonormal")
        ex, lo, hi = _scale(ex, -1.0), (-hi[0], lo[1], lo[2]), (-lo[0], hi[1], hi[2])
    mid = [(a + b) / 2 for a, b in zip(lo, hi)]
    centre = _add(origin, _add(_scale(ex, mid[0]), _add(_scale(ey, mid[1]), _scale(ez, mid[2]))))
    size = [b - a for a, b in zip(lo, hi)]
    if min(size) <= 0:
        raise ValueError(f"an empty box: {size}")
    return {"shape": "box", "center": list(centre), "size": size, "rotation": _quaternion(ex, ey, ez),
            "material": material, "landable": landable}


def _quaternion(ex, ey, ez):
    """The unit quaternion [x, y, z, w] of the rotation whose matrix has columns ex, ey, ez."""
    m00, m01, m02 = ex[0], ey[0], ez[0]
    m10, m11, m12 = ex[1], ey[1], ez[1]
    m20, m21, m22 = ex[2], ey[2], ez[2]
    trace = m00 + m11 + m22
    if trace > 0:
        s = math.sqrt(trace + 1.0) * 2
        q = ((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, s / 4)
    elif m00 > m11 and m00 > m22:
        s = math.sqrt(1.0 + m00 - m11 - m22) * 2
        q = (s / 4, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s)
    elif m11 > m22:
        s = math.sqrt(1.0 + m11 - m00 - m22) * 2
        q = ((m01 + m10) / s, s / 4, (m12 + m21) / s, (m02 - m20) / s)
    else:
        s = math.sqrt(1.0 + m22 - m00 - m11) * 2
        q = ((m02 + m20) / s, (m12 + m21) / s, s / 4, (m10 - m01) / s)
    length = math.sqrt(sum(c * c for c in q))
    return [c / length for c in q]


# --- gaps -----------------------------------------------------------------------------------------------------------

def _gaps(run):
    """The cross-section at every interior sample and the top slot per segment (design 5)."""
    gaps = []
    for j in range(1, run.n):
        polygon = [run.foot(j, -1), run.foot(j, 1), run.lip(j, 1), run.lip(j, -1)]
        t = run.tangent(j)
        normal = [t[0], 0.0, t[1]]
        gaps.append({"id": f"xs_{run.chainage[j]:.2f}", "polygon": [list(p) for p in polygon], "normal": normal,
                     "narrowest_m": inscribed_diameter(polygon, normal)})
    for i in range(run.n):
        polygon = [run.lip(i, -1), run.lip(i + 1, -1), run.lip(i + 1, 1), run.lip(i, 1)]
        gaps.append({"id": f"slot_{run.chainage[i]:.2f}", "polygon": [list(p) for p in polygon],
                     "normal": [0.0, 1.0, 0.0], "narrowest_m": inscribed_diameter(polygon, [0.0, 1.0, 0.0])})
    return gaps


# --- small vector helpers -------------------------------------------------------------------------------------------

def _smooth(t):
    return t * t * (3 - 2 * t)


def _up(p):
    u, v = _sub(p[1], p[0]), _sub(p[2], p[0])
    n = _cross(u, v)
    length = math.sqrt(_dot(n, n))
    n = (n[0] / length, n[1] / length, n[2] / length)
    return n if n[1] > 0 else _scale(n, -1.0)


def _facing(mesh, ids, toward):
    p = [mesh.vertices[v] for v in ids]
    n = _unit(_cross(_sub(p[1], p[0]), _sub(p[2], p[0])))
    return n if _dot(n, toward) > 0 else _scale(n, -1.0)


def _outward(mesh, ids, centre):
    p = [mesh.vertices[v] for v in ids]
    n = _unit(_cross(_sub(p[1], p[0]), _sub(p[2], p[0])))
    c = tuple(sum(q[k] for q in p) / 3 for k in range(3))
    return n if _dot(n, _sub(c, centre)) > 0 else _scale(n, -1.0)


def _area3(mesh, ids):
    p = [mesh.vertices[v] for v in ids]
    n = _cross(_sub(p[1], p[0]), _sub(p[2], p[0]))
    return math.sqrt(_dot(n, n)) / 2


def _area2(poly):
    if len(poly) < 3:
        return 0.0
    return abs(sum(_cross2(a, b) for a, b in zip(poly, poly[1:] + poly[:1]))) / 2


def _newell(points):
    n = [0.0, 0.0, 0.0]
    for a, b in zip(points, points[1:] + points[:1]):
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    return tuple(n)


def _seg_dist2(p, a, b):
    ab, ap = _sub2(b, a), _sub2(p, a)
    length2 = _dot2(ab, ab)
    t = 0.0 if length2 == 0 else min(1.0, max(0.0, _dot2(ab, ap) / length2))
    return math.hypot(ap[0] - t * ab[0], ap[1] - t * ab[1])


def _sub2(a, b):
    return (a[0] - b[0], a[1] - b[1])


def _add2(a, b):
    return (a[0] + b[0], a[1] + b[1])


def _dot2(a, b):
    return a[0] * b[0] + a[1] * b[1]


def _cross2(a, b):
    return a[0] * b[1] - a[1] * b[0]


def _dist2(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def _unit2(v):
    length = math.hypot(v[0], v[1])
    return (v[0] / length, v[1] / length)


def _add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _scale(v, s):
    return (v[0] * s, v[1] * s, v[2] * s)


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _dist(a, b):
    return math.sqrt(_dot(_sub(a, b), _sub(a, b)))


def _unit(v):
    length = math.sqrt(_dot(v, v))
    return (v[0] / length, v[1] / length, v[2] / length)
