"""pole: the village line pole of the lines sheet's shared pole table, L01 first (p1-l01-l03-pole-wires, design
decisions 1-4, 9 and 10; AST-05, AST-08, REAL-05). `pole_kind` wood_log is a tapered log set in the ground,
wood_on_stub a shorter log banded beside a concrete stub; concrete_sv is L02's later branch and is refused by check.

Frame (lib.frame): the origin is the log's axis at the ground line, +Y up. The line runs along X, so the hooks point
+Z and -Z in a zig-zag down the pole (hook_0 the top one, on +Z), a short crossarm spans Z on the pole's -X face,
the brace and the stub stand on +X (placed so +X faces the pull, the brace works as a strut), and the grounding
wire runs down the -X face. Everything is built upright and then leaned as one rigid body about the foot by
`lean_deg` toward `lean_azimuth_deg` (degrees from +X toward +Z). A braced pole leans only across its brace (azimuth
90 or 270): it hinges about the line through the two feet, so both stay in the ground and the brace triangle keeps
its shape. Nothing is drawn below the ground: every member that reaches it is cut by the plane y = 0 and left open
there, and the generator is PLANTED, so the audit lets the terrain cover the collision below y = 0 (the log's and
the brace's buried capsule caps, the stub's buried end).

Parts, with the sheet's numbers (published or assumed as labelled at each constant):
- the log: `top_section_m` to `butt_section_m` diameter, a linear taper over `length_m` from the butt, which is
  `burial_m` under the ground (wood_log) or on the ground line beside the stub (wood_on_stub, the sheet's 1.6 m
  overlap: the stub shows `stub_above_m`); 12 sides at LOD0 (within 2.3 mm of round at the 0.26 m butt, design 2),
  8 at LOD1, a 6-sided prism at LOD2; its top a flat saw cut;
- LOD1 and LOD2 are drawn with the thin-wire coverage (task 1.4's material, design decision 4), so the far pole
  fades as its width over the distance instead of breaking into dashes once it is under a pixel across: every
  member there is a round bar whose corners lie on its circle at the local radius, each with its radial unit normal
  and that radius as its second UV (the shader's inputs), all of it triangles (lib.export keeps a custom normal only
  through triangles, as wire_span's tubes); the crossarm and the stub, boxes at LOD0, are 4-sided round bars there
  of half their narrower side (the width a level look sees across the crossarm, and the stub's across the line),
  so the far pole is never drawn wider than it is;
- the brace (`brace` single): a 0.16 m log at `brace_angle_deg` from the pole, its axis meeting the pole's axis at
  `brace_attach` of the height above ground, its foot in the ground, its top end hidden inside the pole;
- the head: `hooks_0p4` is `hooks` hooks; `crossarm_0p4` a 0.10 x 0.10 m bar `crossarm_len_m` long whose top lies
  `crossarm_below_m` under the pole's top (L 1-3: 1.2-2 m), with a pin and insulator near each end, and `hooks` hooks
  above it; `none` is a bare pole. A hook is a bent 17 mm bar screwed into the log, out HOOK_REACH_M and up
  HOOK_RISE_M to its pin insulator; the top hook is TOP_HOOK_BELOW_M under the top, each next one HOOK_STEP_M lower on
  the other side;
- insulators (`insulator` porcelain): the TF-20 pin insulator, 0.108 m high and 0.075 m across, an 8-sided lathe;
- wood_on_stub: the stub, a 0.17 x 0.20 m concrete post `stub_length_m` long of which `stub_above_m` shows, and
  `bands` wire bands binding the log to it, each a flat strap round both;
- the grounding wire, 6 mm, stapled down the -X face from the ground to under the head.

Metadata (design decision 3, the sheet's physics role):
- collision: the log as equal stacked segments above the ground at their mid radius, three, or more when the taper
  needs them to stay within the sheet's 10 mm (the 7.5 m log on a stub: four), starting as deep under the foot as
  a leaned log's mesh meets the ground on the side it leans from (`Pole.sink`, 19 mm at 9 degrees), the lower ones
  capsules (their caps buried in the ground or in the next segment) and the top one a cylinder, because the top is
  a flat saw cut a capsule's cap would stand 25-35 mm off; the brace a capsule from its foot to the pole's axis;
  the stub and the crossarm boxes; each hook two capsules (its two legs), each pin one, each insulator a cylinder of
  0.0375 x 0.108 m; materials timber, concrete, steel and glass (porcelain is glass, the physics sign-off);
- the brace triangle as one gap polygon: the real clear space between the pole's and the brace's inner surfaces
  and the ground, in the brace's plane, with `narrowest_m` the diameter of its inscribed circle; its id records the
  heights above the ground under which the drone passes (0.706 m at any yaw, 0.574 m nose-first, the houses sheet's
  spans; on a leaned pole the in-plane heights times the lean's cosine, task 1.5's reading §4);
- one solid wind box around the log and, on a braced pole, one around the brace log, square to it (porosity 0);
  the brace triangle between them stays open to the wind; nothing landable;
- snags, each with the material of the member it follows: a `hook` per hook and pin (8.5 mm, the bar's half
  thickness, steel), an `edge` ring round each insulator's skirt drip edge (5 mm, its reach flush with the cylinder,
  glass), the crossarm's end faces (2 mm, timber), each band on the strap's centre line (3 mm, flush with its faces,
  steel) and the grounding wire (3 mm, cable);
- ports: one per insulator, `hook_<n>` from the top, at the bottom of the neck groove on the side away from the pole
  (where a conductor rests), with the outward unit direction and the groove's width, 0.014 m, as the largest
  conductor it seats; a conductor's centre lies one radius further out. A bare pin seats none and has no port.

Slots: wood (pole_wood, timber: log, brace, crossarm), concrete (pole_concrete), steel (pole_steel: hooks, pins,
bands), insulator (insulator_porcelain, glass), ground_wire (wire_steel, cable), each listed only when LOD0 has
faces of it. UVs are metres in each member's own frame: round wood U around (a whole number of WOOD_TILE_M tiles,
so the wrap has no seam) and V along the grain, offset by `seed` so neighbouring poles differ; steel U along the
bar.

The damage dial (I below 0.25, D, X) and its items, `lamp`, `weathering` and the glass insulator exist with the
sheet's ranges, but only the intact pole is built: anything else is refused by check (G1 shows I, the proposal).
"""
import math
import random

from lib import meta
from lib.geometry import Mesh, uv_upright
from lib.skeleton import add, cross, dot, norm, normalize, scale, sub

PLANTED = True
WOOD, CONCRETE, STEEL, INSULATOR, GROUND_WIRE = "wood", "concrete", "steel", "insulator", "ground_wire"
PHYSICS = {WOOD: "timber", CONCRETE: "concrete", STEEL: "steel", INSULATOR: "glass", GROUND_WIRE: "cable"}
KINDS = ("wood_log", "wood_on_stub")          # concrete_sv is L02's (design decision 1)
BRACES = ("none", "single")                   # a_frame: not built
HEADS = ("hooks_0p4", "crossarm_0p4", "none")  # sip_bracket, crossarm_10kv: not built
INSULATORS = ("porcelain", "none")            # glass: no recipe yet
LOG_SIDES = (12, 8, 6)                        # per LOD (design decision 2)
LOG_SEGMENTS = 3                              # design decision 3: 0-2.5, 2.5-5, 5-7.5 m on the 7.5 m log
LOG_TOLERANCE_M = 0.010                       # the sheet: the log's collision within 10 mm of the visual
WOOD_TILE_M = (0.5, 2.0)                      # pole_wood's tile_size_m (rough_wood's 0.5 m, stretched along the grain)
BRACE_D_M = 0.16                              # the sheet's brace log (0.14-0.20 m; assumed)
HOOK_D_M = 0.017                              # KN-16/18 bar (published designation)
HOOK_SIDES = 6
HOOK_REACH_M = 0.15                           # log surface to the pin's axis (assumed)
HOOK_RISE_M = 0.08                            # the horizontal leg's axis up to the insulator's base (assumed)
HOOK_SCREW_M = 0.06                           # threaded into the log (assumed; under the top's 0.07 m radius)
HOOK_BEND_R_M = 0.02                          # the bend's radius at the bar's axis (assumed)
HOOK_STEP_M = 0.5                             # the sheet: 0.4-0.6 m, 0.5 m
TOP_HOOK_BELOW_M = 0.3                        # the sheet: the top hook 0.2-0.4 m under the top
PIN_IN_M = 0.05                               # a pin's end inside the insulator's bore (assumed)
CROSSARM_M = 0.10                             # the sheet's 0.10 x 0.10 m bar
PIN_INSET_M = 0.06                            # a pin from the crossarm's end (assumed)
PIN_RISE_M = 0.05                             # crossarm top to the insulator's base (assumed)
INSULATOR_H_M, INSULATOR_R_M = 0.108, 0.0375  # TF-20 (published)
INSULATOR_SIDES = 8
NECK_R_M, GROOVE_M = 0.019, (0.068, 0.082)    # the side groove's radius and its heights (assumed from the TF form)
NECK_H_M = 0.075                              # the groove's middle, where a conductor rests
GROOVE_W_M = GROOVE_M[1] - GROOVE_M[0]        # 0.014 m: the largest conductor the groove seats (task 1.5's reading §1)
# The TF-20 profile (radius, height) from the bore's edge out along the bottom, up the skirt, through the neck groove
# and over the head to the apex (assumed from the published form: a skirt, a waist, the groove, a domed head).
INSULATOR_PROFILE = ((0.010, 0.0), (INSULATOR_R_M, 0.0), (INSULATOR_R_M, 0.010), (0.030, 0.040), (0.024, 0.060),
                     (NECK_R_M, GROOVE_M[0]), (NECK_R_M, GROOVE_M[1]), (0.026, 0.090), (0.026, 0.098), (0.015, 0.106),
                     (0.0, INSULATOR_H_M))
STUB_M = (0.17, 0.20)                         # the stub's section, across X and Z (the sheet: 0.16-0.18 x 0.18-0.22)
STUB_BURIED_M = 0.3                           # its collision box below the ground, covered by the terrain
BAND_H_M, BAND_T_M = 0.03, 0.006              # 6 turns of 5 mm wire as one strap (the sheet: 4-6 mm, 6-10 turns)
BAND_SPAN_M = (0.3, 0.15)                     # lowest band above the ground, highest under the stub's top (assumed)
GROUND_WIRE_D_M = 0.006                       # assumed: the usual 6 mm descent
GROUND_WIRE_SIDES = 4
GROUND_WIRE_GAP_M = 0.05                      # its top under the lowest point of the head
# Snag radii (the sheet, as task 1.5's reading §6 rules them): a hook or pin its bar's half thickness; the insulator's
# skirt drip edge 5 mm; a crossarm end timber's 2 mm arris; a band the strap's half thickness, on its centre line, so
# its reach is flush with the strap's faces; the grounding wire its own radius.
SNAG_R = {"hook": HOOK_D_M / 2, "drip": 0.005, "crossarm": 0.002, "band": BAND_T_M / 2, "ground_wire": 0.003}
DRONE_M = {"any_yaw": 0.706, "nose_first": 0.574}  # the drone's spans (houses sheet, "How to read this sheet")
# Params the sheet gives the generator that nothing builds yet: anything but this value is refused, so no edit is
# silently ignored (G1 shows the intact pole, the proposal's non-goals).
UNBUILT = (("lamp", False, "no lamp fitting is modelled"),
           ("shrapnel_scars", 0, "the D state's gouges come with the damage mask"),
           ("broken_insulators", 0.0, "the D state is not built"),
           ("crossarm_state", "intact", "the D and X crossarm poses are not built"),
           ("snap_height_m", 0.0, "the X state is not built"),
           ("snap_hang_deg", 60.0, "concrete_sv's X state, L02's"),
           ("char_height_m", 0.0, "the burnt base comes with the damage mask"),
           ("weathering", 0.5, "the recipe's tint is the weathered grey-silver; a per-variant blend to creosote "
                               "needs per-variant tints, which the look task decides"))
# Keys a kind, brace or head does not read: they keep their default, so an edit to them cannot silently do nothing.
IDLE = {"wood_log": ("stub_length_m", "stub_above_m", "bands"), "wood_on_stub": ("burial_m",),
        "brace none": ("brace_angle_deg", "brace_attach"),
        "head not crossarm_0p4": ("crossarm_len_m", "crossarm_below_m")}
# The object sheet's shared pole table and §L01; defaults are L01_straight.
PARAMS = {
    "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 1,
             "effect": "the wood's texture offset, so neighbouring poles differ; the damage states' draws"},
    "pole_kind": {"type": "str", "range": "wood_log, wood_on_stub (concrete_sv: L02's, not built)", "default": "wood_log",
                  "effect": "a log set in the ground, or a shorter log banded beside a concrete stub"},
    "length_m": {"type": "float", "range": (6.5, 11.0), "default": 9.5,
                 "effect": "the log's length: 8.5-11 m set in the ground, 6.5-8.5 m on a stub (check)"},
    "burial_m": {"type": "float", "range": (1.5, 2.5), "default": 2.0,
                 "effect": "wood_log: depth in the ground; the height above ground is length_m - burial_m"},
    "top_section_m": {"type": "float", "range": (0.14, 0.20), "default": 0.16, "effect": "the log's top diameter"},
    "butt_section_m": {"type": "float", "range": (0.22, 0.32), "default": 0.26,
                       "effect": "the log's butt diameter, a linear taper to the top"},
    "stub_length_m": {"type": "float", "range": (3.3, 4.3), "default": 4.3, "effect": "wood_on_stub: the stub's length"},
    "stub_above_m": {"type": "float", "range": (1.2, 2.0), "default": 1.6,
                     "effect": "wood_on_stub: the stub above the ground, the log's overlap on it"},
    "bands": {"type": "int", "range": (2, 3), "default": 2, "effect": "wood_on_stub: wire bands binding log and stub"},
    "lean_deg": {"type": "float", "range": (0.0, 15.0), "default": 0.0, "effect": "lean of the whole pole about its foot"},
    "lean_azimuth_deg": {"type": "float", "range": (0.0, 360.0), "default": 0.0,
                         "effect": "lean direction, degrees from +X toward +Z; 90 or 270 with a brace (check)"},
    "brace": {"type": "str", "range": "none, single (a_frame: not built)", "default": "none",
              "effect": "a brace log on +X and its gap"},
    "brace_angle_deg": {"type": "float", "range": (15.0, 35.0), "default": 25.0,
                        "effect": "brace angle from the pole; sets the brace gap"},
    "brace_attach": {"type": "float", "range": (0.5, 0.8), "default": 0.65,
                     "effect": "where the brace's axis meets the pole's, as a share of the height above ground"},
    "head": {"type": "str", "range": "hooks_0p4, crossarm_0p4, none (sip_bracket, crossarm_10kv: not built)",
             "default": "hooks_0p4", "effect": "what carries the wires"},
    "hooks": {"type": "int", "range": (0, 6), "default": 4,
              "effect": "hooks with pin insulators zig-zag down the log from the top (above the crossarm with "
                        "crossarm_0p4)"},
    "crossarm_len_m": {"type": "float", "range": (0.6, 2.2), "default": 0.7,
                       "effect": "crossarm_0p4: its length, 0.6-1.2 m (L 1: 0.6-0.8)"},
    "crossarm_below_m": {"type": "float", "range": (0.1, 2.0), "default": 1.5,
                         "effect": "crossarm_0p4: its top's distance under the pole's top, the hooks above it "
                                   "(measured L 1-3: 1.2-2 m, 16-28 % of the visible pole)"},
    "insulator": {"type": "str", "range": "porcelain, none (glass: no recipe yet)", "default": "porcelain",
                  "effect": "the pin insulators' material, or bare pins (which seat no conductor: no ports)"},
    "lamp": {"type": "bool", "range": "true or false", "default": False, "effect": "a street-lamp bracket (not built)"},
    "damage": {"type": "float", "range": (0.0, 1.0), "default": 0.0,
               "effect": "I below 0.25 (built), D 0.25-0.8 and X above 0.8 (not built)"},
    "shrapnel_scars": {"type": "int", "range": (0, 40), "default": 0, "effect": "gouges 10-80 mm on one side (not built)"},
    "broken_insulators": {"type": "float", "range": (0.0, 1.0), "default": 0.0,
                          "effect": "share of insulators shattered to a stub (not built)"},
    "crossarm_state": {"type": "str", "range": "intact (twisted, hanging, gone: not built)", "default": "intact",
                       "effect": "the crossarm's pose"},
    "snap_height_m": {"type": "float", "range": (0.0, 3.0), "default": 0.0, "effect": "X: where the pole broke (not built)"},
    "snap_hang_deg": {"type": "float", "range": (0.0, 90.0), "default": 60.0,
                      "effect": "X concrete: the angle the top hangs at (L02's, not built)"},
    "char_height_m": {"type": "float", "range": (0.0, 2.0), "default": 0.0, "effect": "a burnt base (not built)"},
    "weathering": {"type": "float", "range": (0.0, 1.0), "default": 0.5,
                   "effect": "grey-silver checking against new creosote (not built: the recipe's tint)"},
    "lod.lod1_switch_m": {"type": "float", "range": (1.0, 100.0), "default": 35.0,
                          "effect": "LOD1 (no hooks, insulators, bands or wire) from here: a 0.075 m insulator under 2 px"},
    "lod.lod2_switch_m": {"type": "float", "range": (10.0, 1000.0), "default": 120.0,
                          "effect": "LOD2 (the log alone, 6-sided) from here: the 0.16 m top under 1.5 px"},
    "lod.cull_m": {"type": "float", "range": (100.0, 2500.0), "default": 1200.0,
                   "effect": "culled beyond: at least 1 km for the night silhouette (L)"},
}


def check(params):
    """Cross-parameter rules once every key is in range: the built kinds, braces, heads and insulators; the log's
    length by kind and its taper; idle keys at their defaults; a braced pole leaning only across its brace, the
    brace's end hidden in the pole; the LOD distances in order; nothing unbuilt."""
    p = params
    problems = []
    for key, allowed, why in (("pole_kind", KINDS, "concrete_sv is L02's task on this generator"),
                              ("brace", BRACES, "a_frame is not built yet"),
                              ("head", HEADS, "sip_bracket and crossarm_10kv are not built yet"),
                              ("insulator", INSULATORS, "glass has no recipe yet")):
        if p[key] not in allowed:
            problems.append(f"'{key}' = {p[key]!r} is not built yet ({why}); use one of {', '.join(allowed)}")
    if problems:
        return problems
    stub = p["pole_kind"] == "wood_on_stub"
    lo, hi = (6.5, 8.5) if stub else (8.5, 11.0)
    if not lo <= p["length_m"] <= hi:
        problems.append(f"'length_m' = {p['length_m']} is outside [{lo}, {hi}] for {p['pole_kind']}")
    if p["top_section_m"] >= p["butt_section_m"]:
        problems.append(f"'top_section_m' = {p['top_section_m']} must stay under 'butt_section_m' = {p['butt_section_m']}")
    idle = list(IDLE[p["pole_kind"]])
    idle += IDLE["brace none"] if p["brace"] == "none" else []
    idle += IDLE["head not crossarm_0p4"] if p["head"] != "crossarm_0p4" else []
    for key in idle:
        if p[key] != PARAMS[key]["default"]:
            problems.append(f"'{key}' = {p[key]} is not read here ({p['pole_kind']}, brace {p['brace']}, head "
                            f"{p['head']}); keep {PARAMS[key]['default']}")
    if p["head"] == "none" and p["hooks"]:
        problems.append(f"'hooks' = {p['hooks']} needs a head; head none is a bare pole")
    if p["head"] == "crossarm_0p4" and p["crossarm_len_m"] > 1.2:
        problems.append(f"'crossarm_len_m' = {p['crossarm_len_m']} is over a 0.4 kV crossarm's 1.2 m")
    if p["head"] == "crossarm_0p4" and p["hooks"]:
        pole = Pole(p)
        lowest = pole.levels[-1][0] - HOOK_D_M / 2
        reach = pole.crossarm_top() + PIN_RISE_M + INSULATOR_H_M  # the crossarm's insulators' tops
        if lowest <= reach:
            problems.append(f"'hooks' = {p['hooks']}: the lowest hook ({lowest:.3f} m) would not clear the crossarm's "
                            f"insulators ({reach:.3f} m); the hooks stand above the crossarm, so fewer hooks or a "
                            "larger 'crossarm_below_m'")
    if p["brace"] == "single":
        if stub:
            problems.append("a brace on wood_on_stub is not built (the brace and the stub share +X)")
        if p["lean_deg"] > 0 and p["lean_azimuth_deg"] not in (90.0, 270.0):
            problems.append(f"'lean_azimuth_deg' = {p['lean_azimuth_deg']}: a braced pole leans only across its brace "
                            "(90 or 270), hinging about the line through both feet; any other lean would lift or bury "
                            "the brace's foot")
    if problems:
        return problems
    pole = Pole(p)
    if p["brace"] == "single":
        s = pole.attach_s + pole.brace_r
        if pole.radius(s) < pole.brace_r + 0.005:
            problems.append(f"the brace's end ({BRACE_D_M} m) would show beside the pole, {2 * pole.radius(s):.3f} m "
                            f"thick at {s:.2f} m: raise 'top_section_m' or lower 'brace_attach'")
    lod = p["lod"]
    if not lod["lod1_switch_m"] < lod["lod2_switch_m"] < lod["cull_m"]:
        problems.append(f"'lod.*' must increase: {lod['lod1_switch_m']}, {lod['lod2_switch_m']}, {lod['cull_m']}")
    if p["damage"] >= 0.25:
        problems.append(f"'damage' = {p['damage']} asks for the D or X state, not built yet; keep it under 0.25 (I)")
    for key, idle_value, why in UNBUILT:
        if p[key] != idle_value:
            problems.append(f"'{key}' = {p[key]!r} is not built yet ({why}); keep {idle_value!r}")
    return problems


class Pole:
    """The derived numbers of one params set, in the pole's upright frame (before the lean): the log's height above
    ground, its radius along the axis, the brace's attachment, the head's conductor levels, and the lean."""

    def __init__(self, p):
        self.p = p
        self.stub = p["pole_kind"] == "wood_on_stub"
        self.height = p["length_m"] if self.stub else p["length_m"] - p["burial_m"]
        self.butt_s = 0.0 if self.stub else -p["burial_m"]
        self.rb, self.rt = p["butt_section_m"] / 2, p["top_section_m"] / 2
        self.slope = (self.rt - self.rb) / p["length_m"]
        self.brace_r = BRACE_D_M / 2
        self.attach_s = p["brace_attach"] * self.height
        self.beta = math.radians(p["brace_angle_deg"])
        theta, azimuth = math.radians(p["lean_deg"]), math.radians(p["lean_azimuth_deg"])
        axis = (math.sin(azimuth), 0.0, -math.cos(azimuth))  # up x the lean direction: tips +Y toward it
        self.q = [axis[0] * math.sin(theta / 2), 0.0, axis[2] * math.sin(theta / 2), math.cos(theta / 2)]
        self.levels = self._levels()

    def radius(self, s):
        return self.rb + self.slope * (s - self.butt_s)

    def place(self, p):
        """A point or direction of the upright frame in asset space: the lean about the foot."""
        x, y, z, w = self.q
        tx, ty, tz = 2 * (y * p[2] - z * p[1]), 2 * (z * p[0] - x * p[2]), 2 * (x * p[1] - y * p[0])
        return (p[0] + w * tx + (y * tz - z * ty), p[1] + w * ty + (z * tx - x * tz), p[2] + w * tz + (x * ty - y * tx))

    def _levels(self):
        """(screw-in height, side) of every hook from the top, above a crossarm; the crossarm's own two insulators
        come after them in ports."""
        first = self.height - TOP_HOOK_BELOW_M
        return [(first - n * HOOK_STEP_M, 1.0 if n % 2 == 0 else -1.0) for n in range(self.p["hooks"])]

    def crossarm_top(self):
        return self.height - self.p["crossarm_below_m"]

    def sink(self):
        """How far under the foot, along the axis, the leaned log's LOD0 mesh meets the ground on the side it leans
        from: its corner there, g r out (g = 2 / (1 + cos(pi / 12)), r the radius at that depth), stands g r tan(lean)
        under the foot, solved with the taper. 0 upright. The collision segments start this deep, so the lowest
        capsule's cylinder, not its buried cap, meets the ground on every side (review R1)."""
        g = 2 / (1 + math.cos(math.pi / LOG_SIDES[0]))
        t = math.tan(math.radians(self.p["lean_deg"]))
        return g * self.radius(0.0) * t / (1 + g * self.slope * t)

    def segments(self):
        """The log's collision segments from `sink` under the foot to the top: at least LOG_SEGMENTS, and enough that
        a mid-radius segment and the 12-sided mesh (its faces 2.3 mm about the true radius at most) stay within
        LOG_TOLERANCE_M of each other."""
        sagitta = self.rb * (1 - math.cos(math.pi / LOG_SIDES[0])) / (1 + math.cos(math.pi / LOG_SIDES[0]))
        longest = 2 * (LOG_TOLERANCE_M - sagitta) / abs(self.slope)
        return max(LOG_SEGMENTS, math.ceil((self.height + self.sink()) / longest - 1e-9))


def build(params):
    pole = Pole(params)
    p = params
    rng = random.Random(p["seed"])
    offset = (rng.uniform(0, WOOD_TILE_M[0]), rng.uniform(0, WOOD_TILE_M[1]))
    lods = [Mesh(), Mesh(), Mesh()]
    collision, snag, ports, gaps = [], [], [], []
    log = _Rod((0.0, 0.0, 0.0), (0.0, 1.0, 0.0), pole.radius(0.0), pole.slope, (1.0, 0.0, 0.0))
    for lod, sides in enumerate(LOG_SIDES):
        _rod_mesh(lods[lod], pole, log, pole.height, sides, WOOD, offset, cap=True, thin=lod > 0)
    n, sink = pole.segments(), pole.sink()
    step = (pole.height + sink) / n
    for k in range(n):
        a, b = pole.place((0.0, k * step - sink, 0.0)), pole.place((0.0, (k + 1) * step - sink, 0.0))
        r = pole.radius((k + 0.5) * step - sink)
        collision.append(meta.capsule(a, b, r, PHYSICS[WOOD]) if k < n - 1 else _cylinder(a, b, r, PHYSICS[WOOD]))
    wind = [{"shape": "box", "center": list(pole.place((0.0, pole.height / 2, 0.0))),
             "size": [2 * pole.radius(0.0), pole.height, 2 * pole.radius(0.0)], "rotation": list(pole.q), "porosity": 0.0}]
    if p["brace"] == "single":
        foot = (pole.attach_s * math.tan(pole.beta), 0.0, 0.0)
        length = pole.attach_s / math.cos(pole.beta)
        brace = _Rod(foot, (-math.sin(pole.beta), math.cos(pole.beta), 0.0), pole.brace_r, 0.0, (0.0, 0.0, 1.0))
        for lod in (0, 1):
            _rod_mesh(lods[lod], pole, brace, length, LOG_SIDES[lod], WOOD, offset, cap=False, thin=lod > 0)
        a, b = pole.place(foot), pole.place((0.0, pole.attach_s, 0.0))
        collision.append(meta.capsule(a, b, pole.brace_r, PHYSICS[WOOD]))
        wind.append({"shape": "box", "center": list(scale(add(a, b), 0.5)), "size": [BRACE_D_M, length, BRACE_D_M],
                     "rotation": meta.rotation_from_y(normalize(sub(b, a))), "porosity": 0.0})  # review S11
        gaps.append(_brace_gap(pole))
    if pole.stub:
        _stub(lods, pole, collision, snag)
    head_low, bases = pole.height, []  # the head's lowest point; each insulator's (base, side) from the top
    for s, side in pole.levels:
        head_low = min(head_low, s)
        bases.append(_hook(lods[0], pole, s, side, collision, snag))
    if p["head"] == "crossarm_0p4":
        low, pins = _crossarm(lods, pole, offset, collision, snag)
        head_low, bases = min(head_low, low), bases + pins
    for i, (base, side) in enumerate(bases):
        port = _insulator(lods[0], pole, base, side, i, collision, snag)
        if port:
            ports.append(port)
    _ground_wire(lods[0], pole, head_low - GROUND_WIRE_GAP_M, snag)
    lod = p["lod"]
    return {
        "lods": lods,
        "switch_distances_m": [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"]],
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": slot, "physics": PHYSICS[slot]} for slot in lods[0].slots()],
        "collision": collision,
        "gaps": gaps,
        "wind_volumes": wind,
        "ports": ports,
        "snag": snag,
    }


# --- members ----------------------------------------------------------------------------------------------------

class _Rod:
    """A straight round member of the upright frame: its axis from `base` along the unit `axis`, radius r0 + slope *
    s at s along it, `e1` a unit vector across it (the seam's side); e2 = axis x e1."""

    def __init__(self, base, axis, r0, slope, e1):
        self.base, self.axis, self.r0, self.slope = base, normalize(axis), r0, slope
        self.e1 = normalize(sub(e1, scale(self.axis, dot(e1, self.axis))))
        self.e2 = cross(self.axis, self.e1)

    def across(self, phi):
        return add(scale(self.e1, math.cos(phi)), scale(self.e2, math.sin(phi)))

    def at(self, s, phi, grow=1.0):
        r = (self.r0 + self.slope * s) * grow
        return add(add(self.base, scale(self.axis, s)), scale(self.across(phi), r))


def _ground_s(pole, rod, phi, top, grow=1.0):
    """Where the rod's surface line at `phi` (straight, the taper being linear) meets the ground, as s along it."""
    a, b = pole.place(rod.at(-1.0, phi, grow))[1], pole.place(rod.at(top, phi, grow))[1]
    return -1.0 + (top + 1.0) * a / (a - b)


def _rod_mesh(mesh, pole, rod, top, sides, slot, offset, cap, thin=False, ground=True):
    """A rod from the ground (cut by y = 0, open there) to `top` along its axis (a cut square to it, capped when
    `cap`): one quad per side, U around a whole number of the wood tile, V along the grain, plus the offset. Its
    corners stand 2 / (1 + cos(pi / sides)) of the radius out, so its faces' distance from the axis averages the
    true radius (12 sides: within 2.3 mm of round at the 0.26 m butt).
    With `thin` (the thin-wire LODs, module docstring): its corners on the circle at the local radius, each with its
    radial unit normal and (radius, 0) as its second UV, every face a triangle. Without `ground` it starts at s = 0
    instead of the ground, capped there too."""
    grow = 1.0 if thin else 2 / (1 + math.cos(math.pi / sides))
    wrap = max(1, round(2 * math.pi * (rod.r0 + rod.slope * top / 2) / WOOD_TILE_M[0])) * WOOD_TILE_M[0]
    phis = [2 * math.pi * j / sides for j in range(sides)]
    lows = [_ground_s(pole, rod, phi, top, grow) for phi in phis] if ground else [0.0] * sides

    def corner(s, phi, on_ground=False):
        point = pole.place(rod.at(s, phi, grow))
        point = _ground(point) if on_ground else point
        if not thin:
            return mesh.vertex(point)
        return mesh.vertex(point, normal=pole.place(rod.across(phi)), uv2=(rod.r0 + rod.slope * s, 0.0))

    bottom = [corner(s, phi, ground) for s, phi in zip(lows, phis)]
    upper = [corner(top, phi) for phi in phis]
    axis_mid = pole.place(add(rod.base, scale(rod.axis, top / 2)))
    for j in range(sides):
        k = (j + 1) % sides
        quad = [bottom[j], bottom[k], upper[k], upper[j]]
        u0, u1 = offset[0] + wrap * j / sides, offset[0] + wrap * (j + 1) / sides
        uvs = [(u0, offset[1] + lows[j]), (u1, offset[1] + lows[k]), (u1, offset[1] + top), (u0, offset[1] + top)]
        outward = _outward(mesh, quad, axis_mid)
        if not thin:
            mesh.face(quad, outward, slot, uvs)
            continue
        for a, b, c in ((0, 1, 2), (0, 2, 3)):
            mesh.face([quad[a], quad[b], quad[c]], outward, slot, [uvs[a], uvs[b], uvs[c]])
    ends = [(upper, rod.axis)] if cap else []
    ends += [] if ground else [(bottom, scale(rod.axis, -1.0))]
    for ring, normal in ends:
        if not thin:
            mesh.face(ring, pole.place(normal), slot)
            continue
        for j in range(1, sides - 1):
            mesh.face([ring[0], ring[j], ring[j + 1]], pole.place(normal), slot)


def _ground(point):
    """A point computed on the ground plane, with the float residue of y removed."""
    return (point[0], 0.0, point[2])


def _outward(mesh, indices, inside):
    """The polygon's unit normal (Newell), turned away from the point `inside`."""
    pts = [mesh.vertices[i] for i in indices]
    n = [0.0, 0.0, 0.0]
    for a, b in zip(pts, pts[1:] + pts[:1]):
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    n = normalize(tuple(n))
    centre = scale(tuple(sum(q[a] for q in pts) for a in range(3)), 1.0 / len(pts))
    return n if dot(n, sub(centre, inside)) >= 0 else scale(n, -1.0)


def _cylinder(a, b, radius, material):
    d = sub(b, a)
    return {"shape": "cylinder", "center": list(scale(add(a, b), 0.5)), "radius": radius, "height": norm(d),
            "rotation": meta.rotation_from_y(normalize(d)), "material": material, "landable": False}


def _box(pole, centre, size, material):
    return {"shape": "box", "center": list(pole.place(centre)), "size": list(size), "rotation": list(pole.q),
            "material": material, "landable": False}


def _record(kind, points, radius, slot, pole):
    return {"kind": kind, "points": [list(pole.place(q)) for q in points], "radius_m": radius, "material": PHYSICS[slot]}


def _bar(mesh, pole, points, radius, sides, slot):
    """A round bar along a polyline of the upright frame, open at both ends (they lie inside the log, the crossarm
    or an insulator): rings square to the bisected tangent, framed by parallel transport, U along, V around."""
    tangents = [normalize(sub(points[min(i + 1, len(points) - 1)], points[max(i - 1, 0)])) for i in range(len(points))]
    t0 = tangents[0]
    u = normalize(cross(t0, (1.0, 0.0, 0.0) if abs(t0[0]) < 0.9 else (0.0, 1.0, 0.0)))
    rings, along = [], [0.0]
    for i, (q, t) in enumerate(zip(points, tangents)):
        if i:
            u = normalize(sub(u, scale(t, dot(u, t))))
            along.append(along[-1] + norm(sub(q, points[i - 1])))
        v = cross(t, u)
        rings.append([mesh.vertex(pole.place(add(q, add(scale(u, radius * math.cos(a)), scale(v, radius * math.sin(a))))))
                      for a in (2 * math.pi * j / sides for j in range(sides))])
    step = 2 * math.pi * radius / sides
    for i in range(len(points) - 1):
        mid = pole.place(scale(add(points[i], points[i + 1]), 0.5))
        for j in range(sides):
            k = (j + 1) % sides
            quad = [rings[i][j], rings[i][k], rings[i + 1][k], rings[i + 1][j]]
            mesh.face(quad, _outward(mesh, quad, mid), slot,
                      [(along[i], j * step), (along[i], (j + 1) * step), (along[i + 1], (j + 1) * step), (along[i + 1], j * step)])


# --- the head ---------------------------------------------------------------------------------------------------

def _hook(mesh, pole, s, side, collision, snag):
    """One hook screwed in at height s on the `side` (+1 +Z, -1 -Z) face: its bar, two capsules and its record.
    Returns (insulator base, side)."""
    r = pole.radius(s)
    out = r + HOOK_REACH_M
    corner = (0.0, s, side * out)
    base = (0.0, s + HOOK_RISE_M, side * out)
    inner = (0.0, s, side * (r - HOOK_SCREW_M))
    bend = [(0.0, s + HOOK_BEND_R_M * (1 - math.cos(a)), side * (out - HOOK_BEND_R_M * (1 - math.sin(a))))
            for a in (math.pi / 2 * k / 3 for k in range(4))]
    top = (0.0, base[1] + PIN_IN_M, base[2])
    _bar(mesh, pole, [inner] + bend + [top], HOOK_D_M / 2, HOOK_SIDES, STEEL)
    collision += [meta.capsule(pole.place(inner), pole.place(corner), HOOK_D_M / 2, PHYSICS[STEEL]),
                  meta.capsule(pole.place(corner), pole.place(top), HOOK_D_M / 2, PHYSICS[STEEL])]
    snag.append(_record("hook", [(0.0, s, side * r), corner, base], SNAG_R["hook"], STEEL, pole))
    return base, side


def _insulator(mesh, pole, base, side, n, collision, snag):
    """The pin insulator standing on `base` (upright frame, axis +Y before the lean): its lathe, cylinder, drip-edge
    record and port, which seats a conductor up to the groove's width; with insulator none a bare pin, which seats
    no conductor and has no port (None)."""
    if pole.p["insulator"] == "none":
        return None
    rings = []
    for radius, h in INSULATOR_PROFILE:
        if radius == 0.0:
            rings.append([mesh.vertex(pole.place((base[0], base[1] + h, base[2])))])
        else:
            rings.append([mesh.vertex(pole.place((base[0] + radius * math.cos(a), base[1] + h, base[2] + radius * math.sin(a))))
                          for a in (2 * math.pi * j / INSULATOR_SIDES for j in range(INSULATOR_SIDES))])
    for i in range(len(rings) - 1):
        (r0, h0), (r1, h1) = INSULATOR_PROFILE[i], INSULATOR_PROFILE[i + 1]
        for j in range(INSULATOR_SIDES):
            k = (j + 1) % INSULATOR_SIDES
            a = 2 * math.pi * (j + 0.5) / INSULATOR_SIDES
            normal = pole.place(normalize((math.cos(a) * (h1 - h0), r0 - r1, math.sin(a) * (h1 - h0))))
            lo, hi = rings[i], rings[i + 1]
            face = [lo[j], lo[k]] + ([hi[0]] if len(hi) == 1 else [hi[k], hi[j]])
            mesh.face(face, normal, INSULATOR)
    axis = pole.place((0.0, 1.0, 0.0))
    foot = pole.place(base)
    collision.append(_cylinder(foot, add(foot, scale(axis, INSULATOR_H_M)), INSULATOR_R_M, PHYSICS[INSULATOR]))
    # The skirt's drip edge, where the fiber bends over the insulator (the neck groove lies inside the cylinder,
    # where no fiber reaches): a ring through the rounded corner's centre, its reach flush with the cylinder's side
    # and bottom at the ring's corners.
    r, h = INSULATOR_R_M - SNAG_R["drip"], SNAG_R["drip"]
    ring = [(base[0] + r * math.cos(a), base[1] + h, base[2] + r * math.sin(a))
            for a in (2 * math.pi * j / INSULATOR_SIDES for j in range(INSULATOR_SIDES + 1))]
    snag.append(_record("edge", ring, SNAG_R["drip"], INSULATOR, pole))
    port = (base[0], base[1] + NECK_H_M, base[2] + side * NECK_R_M)
    return {"id": f"hook_{n}", "position": list(pole.place(port)), "direction": list(pole.place((0.0, 0.0, side))),
            "max_diameter_m": GROOVE_W_M}


def _crossarm(lods, pole, offset, collision, snag):
    """The crossarm on the -X face, a 0.10 m square bar along Z, with a pin near each end: its box, the pins'
    capsules, the end and pin records. Returns its bottom and each pin's (insulator base, side)."""
    length = pole.p["crossarm_len_m"]
    top = pole.crossarm_top()
    centre_s = top - CROSSARM_M / 2
    x = -(pole.radius(centre_s) + CROSSARM_M / 2)
    lo, hi = (x - CROSSARM_M / 2, top - CROSSARM_M, -length / 2), (x + CROSSARM_M / 2, top, length / 2)
    _box_mesh(lods[0], pole, lo, hi, WOOD, offset, along=2)
    bar = _Rod((x, centre_s, -length / 2), (0.0, 0.0, 1.0), CROSSARM_M / 2, 0.0, (1.0, 1.0, 0.0))  # corners diagonal
    _rod_mesh(lods[1], pole, bar, length, 4, WOOD, offset, cap=True, thin=True, ground=False)
    collision.append(_box(pole, (x, centre_s, 0.0), (CROSSARM_M, CROSSARM_M, length), PHYSICS[WOOD]))
    for z in (lo[2], hi[2]):
        loop = [(lo[0], lo[1], z), (hi[0], lo[1], z), (hi[0], hi[1], z), (lo[0], hi[1], z), (lo[0], lo[1], z)]
        snag.append(_record("edge", loop, SNAG_R["crossarm"], WOOD, pole))
    bases = []
    for side in (1.0, -1.0):  # a pin through the bar near each end
        z = side * (length / 2 - PIN_INSET_M)
        base = (x, top + PIN_RISE_M, z)
        _bar(lods[0], pole, [(x, centre_s, z), (x, base[1] + PIN_IN_M, z)], HOOK_D_M / 2, HOOK_SIDES, STEEL)
        collision.append(meta.capsule(pole.place((x, centre_s, z)), pole.place((x, base[1] + PIN_IN_M, z)),
                                      HOOK_D_M / 2, PHYSICS[STEEL]))
        snag.append(_record("hook", [(x, top, z), base], SNAG_R["hook"], STEEL, pole))
        bases.append((base, side))
    return lo[1], bases


def _box_mesh(mesh, pole, lo, hi, slot, offset, along, bottom=True):
    """An axis-aligned box of the upright frame, leaned; V runs along axis `along` (the grain), U across. Without
    `bottom` its -Y face is left open and its four side edges are cut by the ground."""
    corners = {}
    for c in [(sx, sy, sz) for sx in (0, 1) for sy in (0, 1) for sz in (0, 1)]:
        q = tuple(hi[a] if c[a] else lo[a] for a in range(3))
        if not bottom and c[1] == 0:
            upper = pole.place((q[0], hi[1], q[2]))
            low = pole.place((q[0], lo[1] - 1.0, q[2]))
            corners[c] = mesh.vertex(_ground(add(low, scale(sub(upper, low), low[1] / (low[1] - upper[1])))))
        else:
            corners[c] = mesh.vertex(pole.place(q))
    centre = pole.place(scale(add(lo, hi), 0.5))
    for axis in range(3):
        for side in (0, 1):
            if axis == 1 and side == 0 and not bottom:
                continue
            face = [corners[c] for c in sorted(corners) if c[axis] == side]
            face = [face[0], face[1], face[3], face[2]]  # the four in cyclic order
            local = [tuple(hi[a] if c[a] else lo[a] for a in range(3)) for c in sorted(corners) if c[axis] == side]
            local = [local[0], local[1], local[3], local[2]]
            across = [a for a in range(3) if a not in (axis, along)]
            u_axis = across[0] if axis != along else [a for a in range(3) if a != along][0]
            v_axis = along if axis != along else [a for a in range(3) if a not in (along, u_axis)][0]
            uvs = [(offset[0] + q[u_axis], offset[1] + q[v_axis]) for q in local]
            mesh.face(face, _outward(mesh, face, centre), slot, uvs)


def _stub(lods, pole, collision, snag):
    """The concrete stub on +X beside the log's butt, its box (on into the ground), and the bands."""
    p = pole.p
    x0 = pole.radius(0.0)
    lo, hi = (x0, 0.0, -STUB_M[1] / 2), (x0 + STUB_M[0], p["stub_above_m"], STUB_M[1] / 2)
    _box_mesh(lods[0], pole, lo, hi, CONCRETE, (0.0, 0.0), along=1, bottom=False)
    post = _Rod((x0 + STUB_M[0] / 2, 0.0, 0.0), (0.0, 1.0, 0.0), min(STUB_M) / 2, 0.0, (1.0, 0.0, 1.0))
    _rod_mesh(lods[1], pole, post, p["stub_above_m"], 4, CONCRETE, (0.0, 0.0), cap=True, thin=True)
    centre = (x0 + STUB_M[0] / 2, (p["stub_above_m"] - STUB_BURIED_M) / 2, 0.0)
    collision.append(_box(pole, centre, (STUB_M[0], p["stub_above_m"] + STUB_BURIED_M, STUB_M[1]), PHYSICS[CONCRETE]))
    low, high = BAND_SPAN_M[0], p["stub_above_m"] - BAND_SPAN_M[1]
    for k in range(p["bands"]):
        s = low + (high - low) * k / (p["bands"] - 1)
        path = _band_path(pole.radius(s), lo, hi)
        _band(lods[0], pole, path, s)
        middle = [(x + reach / 2 * nx, s, z + reach / 2 * nz)  # the strap's centre line
                  for (x, z), (nx, nz, reach) in zip(path, _band_offsets(path))]
        snag.append(_record("edge", middle + middle[:1], SNAG_R["band"], STEEL, pole))


def _band_path(r, lo, hi, points=32):
    """The convex outline round the log's circle (radius r) and the stub's section, counter-clockwise in (x, z)."""
    pts = [(r * math.cos(2 * math.pi * k / points), r * math.sin(2 * math.pi * k / points)) for k in range(points)]
    pts += [(lo[0], lo[2]), (hi[0], lo[2]), (hi[0], hi[2]), (lo[0], hi[2])]
    pts = sorted(set(pts))

    def half(seq):
        out = []
        for q in seq:
            while len(out) >= 2 and ((out[-1][0] - out[-2][0]) * (q[1] - out[-2][1])
                                     - (out[-1][1] - out[-2][1]) * (q[0] - out[-2][0])) <= 1e-12:
                out.pop()
            out.append(q)
        return out

    lower, upper = half(pts), half(pts[::-1])
    return lower[:-1] + upper[:-1]


def _band_offsets(path):
    """Per outline vertex, the unit bisector (nx, nz) the strap's thickness is laid along and how far along it the
    strap's outer face stands (BAND_T_M square to both adjacent edges)."""
    n = len(path)
    normals = []
    for i in range(n):
        (ax, az), (bx, bz) = path[i], path[(i + 1) % n]
        length = math.hypot(bx - ax, bz - az)
        normals.append(((bz - az) / length, -(bx - ax) / length))  # outward of a counter-clockwise edge in (x, z)
    offsets = []
    for i in range(n):
        nx, nz = normals[i - 1][0] + normals[i][0], normals[i - 1][1] + normals[i][1]
        k = math.hypot(nx, nz)
        nx, nz = nx / k, nz / k
        offsets.append((nx, nz, BAND_T_M / max(0.2, nx * normals[i][0] + nz * normals[i][1])))
    return offsets


def _band(mesh, pole, path, s):
    """A flat strap BAND_H_M tall and BAND_T_M thick on the outline at height s: outer, top, inner, bottom faces."""
    n = len(path)
    rows = []
    for (x, z), (nx, nz, reach) in zip(path, _band_offsets(path)):
        rows.append([mesh.vertex(pole.place((x + d * nx, s + h, z + d * nz)))
                     for d, h in ((0.0, -BAND_H_M / 2), (reach, -BAND_H_M / 2), (reach, BAND_H_M / 2), (0.0, BAND_H_M / 2))])
    centre = pole.place((0.0, s, 0.0))
    for i in range(n):
        a, b = rows[i], rows[(i + 1) % n]
        for c in range(4):
            d = (c + 1) % 4
            quad = [a[c], b[c], b[d], a[d]]
            if c == 3:  # the inner face looks toward the log and stub
                mesh.face(quad, scale(_outward(mesh, quad, centre), -1.0), STEEL)
            elif c in (0, 2):
                mesh.face(quad, pole.place((0.0, -1.0 if c == 0 else 1.0, 0.0)), STEEL)
            else:
                mesh.face(quad, _outward(mesh, quad, centre), STEEL)


def _ground_wire(mesh, pole, top, snag):
    """The grounding wire down the -X face, from the ground to `top`, capped at its top."""
    r = GROUND_WIRE_D_M / 2
    base = (-(pole.radius(0.0) + r), 0.0, 0.0)
    end = (-(pole.radius(top) + r), top, 0.0)
    wire = _Rod(base, sub(end, base), r, 0.0, (0.0, 0.0, 1.0))
    length = norm(sub(end, base))
    _rod_mesh(mesh, pole, wire, length, GROUND_WIRE_SIDES, GROUND_WIRE, (0.0, 0.0), cap=True)
    a, b = pole.place(base)[1], pole.place(end)[1]
    foot = add(base, scale(sub(end, base), a / (a - b)))  # where its axis meets the ground
    snag.append(_record("edge", [foot, end], SNAG_R["ground_wire"], GROUND_WIRE, pole))


# --- the brace gap ------------------------------------------------------------------------------------------------

def brace_triangle(pole):
    """The clear triangle of the brace's plane (upright frame, z = 0): the pole's inner surface at the ground, the
    brace's inner surface at the ground, and where the two inner surfaces meet; with the clear width w(y) across it,
    as (vertices, width)."""
    c, s = math.cos(pole.beta), math.sin(pole.beta)
    r0, k = pole.radius(0.0), pole.slope
    reach = pole.attach_s * s - pole.brace_r  # the brace's inner line: c * x + s * y = reach
    apex = (reach - r0 * c) / (k * c + s)
    vertices = [(r0, 0.0, 0.0), (reach / c, 0.0, 0.0), (r0 + k * apex, apex, 0.0)]
    return vertices, lambda y: (reach - s * y) / c - (r0 + k * y)


def inscribed_diameter(vertices):
    """The diameter of a triangle's inscribed circle: 4 * area / perimeter."""
    a, b, c = vertices
    area = norm(cross(sub(b, a), sub(c, a))) / 2
    return 4 * area / (norm(sub(b, a)) + norm(sub(c, b)) + norm(sub(a, c)))


def _brace_gap(pole):
    vertices, width = brace_triangle(pole)
    heights = {}
    for name, span in DRONE_M.items():  # w(y) falls linearly: the height where it equals the span
        w0, w1 = width(0.0), width(1.0)
        heights[name] = pole.place((0.0, (w0 - span) / (w0 - w1), 0.0))[1]  # above the ground, leaned
    gap_id = f"brace_triangle_any_yaw_below_{heights['any_yaw']:.2f}m_nose_first_below_{heights['nose_first']:.2f}m"
    return {"id": gap_id, "polygon": [list(pole.place(v)) for v in vertices], "normal": list(pole.place((0.0, 0.0, 1.0))),
            "narrowest_m": inscribed_diameter(vertices)}
