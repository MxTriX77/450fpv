"""vehicle: a car or truck as a clean base from its model's published dimensions, then one state kit (object sheet
docs/object-sheets/vehicles.md, "Rules shared by every vehicle"; p1-r01-vaz-sedan, design decisions 1-10). Plain
Python on lib.geometry and lib.loft.

This change builds the clean base of R01 (the classic VAZ 2101 sedan) and nothing else: check refuses every other
model, the B and F states, any A kit item and the parameters that need the damage mask ("not built yet"); the kits
are task 1.2's. The PARAMS table is the sheet's shared table plus R01's three rows, so the params files already carry
every key the kits will read.

Frame (asset space, lib.frame): the ground at y = 0, the car's centre line on z = 0, its length along X with the
nose at -X and the middle of its overall length at x = 0, so the review camera (south-west) sees the left side and
the front three-quarter, nose left, as letter M is framed. The left (driver's) side is -Z. Internally a station is
s, metres from the front bumper's tip rearward (x = s - length / 2), and a lateral offset w is positive to the right.

The base preset (PRESETS, design decision 1). The side profile (centre-line silhouette) and plan profile (half-width)
stations are derived from a published outline drawing (the-blueprints.com VAZ 2101 side, front and top views,
https://www.the-blueprints.com/blueprints/cars/lada/3472/view/vaz_2101/): its axle lines set the horizontal scale to
the published wheelbase (6.47 mm/px), at which its length reads 4.072 m and its tyre 0.589 m (published 4.073 and
0.594); its roof stands at 1.327 m, so its heights are scaled by 1.44 / 1.327 to the published unladen height (the
sheet's open point: M settles it). Its top view is scaled to the published width. The params carry the stations,
so the profiles are edited in the params file and scaled to `length`, `width` and `height`.

The clean base (design decision 2):
- The body side, per side, lofted along the stations through one section: a 3 cm tuck under the sill, the flank
  vertical at the plan half-width, then the shoulder's roll of radius ROLL_R_M up to the shoulder line (the bonnet's
  and boot's edges, the belt line in the cabin). Wheel arches of radius tyre / 2 + arch_clearance about the wheel
  centres are cut from it (the rows below the arch's edge clamped up to it), lined by wheelhouses. The doors are
  separate panels with DOOR_GAP_M shut lines, cut at the published panel lines of the preset.
- The tops: the bonnet and the boot lid (separate panels), the scuttle and the strip behind the rear screen, crowned
  across as 1 - u^CROWN_POWER (flat with crisp edges, the 2101's), centre on the side profile, edges on the
  shoulder line (in front of the cowl at most CROWN_MAX_M under the centre; the boot lid flat across).
- The greenhouse from the cowl to the rear screen's base: per side a band (the pillars, chrome window frames and the
  windows: vent, front, rear and rear quarter), leaning in by `tumblehome` from the belt to its edge line (Body.z_e:
  the A pillar, the drip rail, the roof's rear slope, the C pillar); over it the cap (windscreen, roof, rear screen)
  from the edge line to the side profile, a flat crown with an EDGE_R_M roll at its edges. The glass panes are 5 mm slabs set
  GLASS_INSET_M inside their openings: intact in the clean base.
- Panels are 0.8 mm double-sided sheets with their edges at every opening and cut (the sheet's gauge); the band is
  BAND_T_M deep (a door frame's profile). The front and rear faces close the body; the underbody (its top the cabin
  floor) and the wheelhouses close it below.
- Chrome bumpers (a blade swept along the plan curve, wrapping the corners) with two overriders each, the grille's
  bars over a dark backing, twin round headlamps in chrome bezels, front indicators, the tail lamps, door handles,
  wipers; steel wheels with domed hubcaps and 165/80 R13 tyres flattened at the ground; the interior (two front seats
  without headrests, the rear bench, the dashboard, the steering wheel); the tube roof rack on four posts clamped to
  the drip rails (roof_rack). Plates (`plate`): none, or blank plates front and rear with no character on them
  (SEC-02). Door trims, the firewall and the parcel shelf are left to the kits that open the cabin (task 1.2):
  behind intact glass nothing reaches them.

Collision (design decision 7, with what the audit's 2 cm zone round the landable roof, bonnet and boot lid asks):
the closed car is a union of convex primitives whose exposed faces lie on its skin; `collision_plan` lists them
and why. The glass is in, so the greenhouse is solid and the interior's primitives (seats, dashboard, steering
wheel) only stand for its mesh. Material ids: body sheet_metal, bumpers, rack and wheels' steel parts steel, glass
glass, tyres rubber, seats fabric, dashboard plastic. Landable: the roof's three boxes, the bonnet, the boot lid and
the rack's side rails (the legs' touch points, the sheet). Wind volumes:
the lower body (porosity 0) and the greenhouse (0, its glass in). Snags: the rack's tubes and posts and the
bumpers' ends (edge), the wiper arms (hook). No gap (the shared gap table).

LODs (design decision 8, 935 px/rad): LOD0 the above; LOD1 the single-sided skin at half the sampling, no edges,
interior as boxes, wheels 16-sided; LOD2 a coarse hull with the glass in its surface, 8-sided wheels; LOD3 boxes.
"""
import math

from lib import kits_vehicle as kits
from lib import loft, masks, meta
from lib.geometry import Mesh
from lib.skeleton import add, cross, dot, norm, normalize, scale, sub

# The car stands on the ground: its tyres' and front wheelhouses' cylinders reach a few centimetres under y = 0,
# where the terrain is (lib.audit's planted rule).
PLANTED = True

# --- slots and materials ------------------------------------------------------------------------------------------
PAINT, UNDER, CHROME, GLASS, TYRE, SEAT, VINYL, LAMP, LENS, RACK, PLATE = (
    "paint", "under", "chrome", "glass", "tyre", "seat", "vinyl", "lamp", "lens", "rack", "plate")
# the kits' slots (task 1.2): the crazed windscreen's own (the transparent glass, design 6), torn steel rusting, a
# burnt tyre's bead wires, the soot-black paint (its damage mask's A burns it to bare steel), dulled chrome, the soot
# patch
WINDSCREEN, RUST, WIRE, SOOT, CHROME_DULL, SOOT_PATCH = (
    "windscreen", "rust", "wire", "soot", "chrome_dull", "soot_patch")
# the far LODs' glass (task 1.4): LOD0-1's panes are the transparent glass with the cabin behind them, the LOD2 and
# LOD3 hulls have no cabin, so their glass is the opaque car_glass
GLASS_HULL = "glass_hull"
PHYSICS = {PAINT: "sheet_metal", UNDER: "sheet_metal", CHROME: "steel", GLASS: "glass", TYRE: "rubber",
           SEAT: "fabric", VINYL: "plastic", LAMP: "glass", LENS: "plastic", RACK: "steel", PLATE: "sheet_metal",
           WINDSCREEN: "glass", RUST: "sheet_metal", WIRE: "steel", SOOT: "sheet_metal",
           CHROME_DULL: "steel", SOOT_PATCH: "surface:burnt_field", GLASS_HULL: "glass"}
# car_lamps' palette columns (assets-src/materials/car_lamps.json), left to right: the lenses and the blank plate
LAMP_COLUMNS = ("clear", "red", "amber", "plate")
LAMP_UV = {c: ((k + 0.5) / 4, 0.5) for k, c in enumerate(LAMP_COLUMNS)}

# --- the shared table's enumerations (vehicles.md) ------------------------------------------------------------------
MODELS = ("R01", "R02", "R03", "R04", "R05", "R06", "R07", "R08", "R09")
BODIES = ("sedan", "hatchback", "estate", "liftback", "flatbed", "tilt")
FRONTS = ("2101", "2103_2106", "2105_2107")
WHEELS = ("steel_hubcap", "steel", "alloy", "truck_steel")
PAINTS = ("white", "beige", "cherry_red", "sky_blue", "dark_blue", "dark_green", "olive_drab", "faded_blue",
          "silver", "graphite", "black")
STATES = ("A", "B", "F")
GLASS_STATES = ("intact", "crazed_shards", "gone")
WINDSCREENS = ("intact", "crazed", "crazed_holed", "sagging", "gone")
BONNETS = ("closed", "corner_folded", "sprung", "folded_back", "standing", "gone")
DOOR_STATES = ("closed", "ajar", "sprung", "open", "hanging", "gone")
BUMPER_STATES = ("on", "hanging", "gone")
BURN_ORIGINS = ("engine", "cabin", "fuel_tank", "cargo")
INTERIORS = ("intact", "crumbs", "burnt_frames")
GROUNDS = ("asphalt", "soil", "grass", "snow")
POSES = ("upright", "ditch_tilt", "on_side")
PLATES = ("none", "blank")
CORNERS = ("front_left", "front_right", "rear_left", "rear_right")
STATIONS = ("front_bumper", "bonnet_front", "bonnet_mid", "cowl", "windscreen_top", "roof_front", "roof_rear",
            "rear_screen_top", "rear_screen_base", "boot_front", "boot_edge", "rear_bumper")

# --- the model presets (design decision 1) --------------------------------------------------------------------------
# Published: the sheet's R01 Dimensions (en/ru.wikipedia VAZ-2101; auto.ru tyre sizes). Derived: the outline drawing
# (module docstring) at 6.47 mm/px, heights x 1.44 / 1.327. Assumed: as labelled.
PRESETS = {
    "R01": {
        "published": {"length": 4.073, "width": 1.611, "height": 1.44, "wheelbase": 2.424, "track_front": 1.349,
                      "track_rear": 1.305, "ground_clearance": 0.17, "tyre_diameter": 0.594, "tyre_width": 0.165},
        "body": "sedan",
        # the underbody's height along s (derived: the drawing's sill and aprons)
        "underbody": ((0.105, 0.28), (0.40, 0.26), (0.70, 0.246), (3.30, 0.246), (3.985, 0.27)),
        # door panel lines: front door's front edge, the B pillar's shut line, the rear door's rear edge (derived; the
        # rear door's edge is taken straight down ahead of the rear arch, the drawing's at the sill)
        "doors": (1.18, 2.085, 2.69),
        "scuttle_m": 0.08,              # the cowl panel between the bonnet's rear edge and the windscreen (assumed)
        # the greenhouse: the drip rail's height (derived); the A pillar meets it at the windscreen's top and the C
        # pillar leaves it at the rear screen's top (the drawing's 1.415 and 2.95 m: the windscreen's and the
        # roof's curvature, held to the stations so the edge roll keeps its radius)
        "drip_z": 1.355,
        # the band's windows (s0, s1) at band fractions WINDOW_V (derived from the drawing's frames)
        "vent": (1.24, 1.545), "front_window": (1.565, 2.065), "rear_window": (2.125, 2.64),
        "quarter": (2.665, 2.86),
        # the windscreen's and rear screen's glass in the cap: margin along s from the cap's ends, and the section
        # parameter's reach (CAP_FLAT is the flat crown's edge; derived: the A and C pillars' widths)
        "windscreen_glass": (0.03, 0.72), "rear_glass": (0.03, 0.70),
        # headlamps: lateral centre, height, lens radius (derived: the front view; the sheet's ring about 0.18 m)
        "headlamp": (0.595, 0.685, 0.09),
        "grille": (0.475, 0.56, 0.76),          # half-width, bottom, top (derived: the front view)
        "indicator": (0.52, 0.68, 0.50, 0.55),  # lateral from-to, bottom, top (derived)
        "tail_lamp": (0.40, 0.70, 0.60, 0.72),  # lateral from-to, bottom, top (assumed from the class)
        # bumpers: blade bottom and top, depth, the plan curve's s = k w^2, the wrap's end (derived: front and top)
        "bumper_front": (0.39, 0.49, 0.06, 0.04, 0.20),
        "bumper_rear": (0.40, 0.50, 0.06, 0.035, 0.19),
        "overrider": (0.385, 0.34, 0.53),       # lateral, bottom, top (derived: the front view)
        # blank plates: the front one's top at the blade's top, on its middle; the rear one's bottom over the rear
        # bumper, on the rear panel, lit by the plate lamp on the bumper (published: the 2101's FP-141 plate lamp
        # "is installed on the bumper", https://www.vazbook.ru/en/01/2101/main/system/fonari-vnutrennego-i-naruzhnogo-
        # osvescheniya); the heights assumed from the bumpers
        "plates": (0.49, 0.515),
        # roof rack (the sheet: 1.3 m long, rails 0.10 m over the roof, 20 mm tubes): s from-to, half-width, posts' s;
        # 1.2 m across (task 1.4, measured M: the posts rise nearly upright from the drip rails, leaning in 18 degrees,
        # where the sheet's assumed 1.0 m leant them in 41)
        "rack": (1.59, 2.89, 0.60, (1.65, 2.83)),
        # interior (assumed, the class; the drawing's seat outlines for s)
        "front_seat": {"s": (1.50, 1.97), "y": (0.30, 0.55), "w": 0.33, "half": 0.24,
                       "back": ((1.97, 0.52), (2.11, 1.04)), "back_t": 0.10},
        # the rear bench spans between the quarters' trims (task 1.2: 0.665 m, 3.5 cm off the trim, assumed; task 1.1
        # had 0.62 m between the wheel arches, which left the arches' tops bare beside it once a door's glass goes)
        "rear_seat": {"s": (2.32, 2.74), "y": (0.28, 0.52), "w": 0.0, "half": 0.665,
                      "back": ((2.74, 0.50), (2.93, 0.98)), "back_t": 0.10},
        "dashboard": {"s": (1.05, 1.32), "y": (0.66, 0.96), "half": 0.66},
        "steering": {"s": 1.43, "w": -0.33, "y": 0.86, "tilt_deg": 25.0, "r": 0.195, "tube": 0.013},
    },
}

# --- construction constants (assumed unless labelled) ---------------------------------------------------------------
GAUGE_M = 0.0008          # the sheet: main panels 0.8 mm (published)
BAND_T_M = 0.015          # a door frame's profile: the band's depth (assumed; under the audit's 2 cm by the roof)
GLASS_T_M = 0.005         # design decision 6: 5 mm panes
GLASS_INSET_M = 0.008     # a pane's outer face behind its opening's outer face (assumed)
DOOR_GAP_M = 0.004        # shut lines (assumed)
HANDLE_PROUD_M = 0.010    # a door's chrome pull handle stands this proud (assumed)
BLADE_SET_M = 0.025       # a bumper blade's face behind its overriders' faces, which make the published length
                          # (derived: the drawing's extremes are the overriders)
TUCK_W_M, TUCK_H_M = 0.03, 0.06   # the sill's tuck under (derived: the front view, 3 cm in at the bottom)
ROLL_R_M = 0.10           # the shoulder's roll (derived: the front view's flank to the belt within 2.5 cm)
ROLL_STEPS = 6
CROWN_POWER = 8           # tops crowned as 1 - u^8: flat with crisp edges (assumed from the drawing's front view)
CROWN_FRONT_M = 0.02      # the bonnet's crown at its front edge (assumed)
CROWN_MAX_M = 0.03        # the bonnet's and scuttle's crown from the front axle back (derived: the front view's
                          # flat bonnet; the shoulder steps to the belt at the cowl, under the drawing's resolution)
EDGE_R_M = 0.055          # the roof's edge roll down to the drip rail (derived: the front view's 5-6 cm, under the
                          # 5.7 cm the roof stands over the drip at the windscreen's top)
CAP_FLAT = 0.75           # the cap's section parameter where the flat crown meets the edge roll
WINDOW_V = (0.05, 0.93)   # the windows' bottom and top as fractions of the band (derived)
FRAME_M = 0.015           # chrome window frames (assumed)
WHEEL_INNER_GAP_M = 0.02  # the wheelhouse's inner wall inboard of the tyre (assumed)
RIM_R_M = 0.1651          # a 13-inch rim's bead seat (published: 13 in)
HUBCAP_R_M, HUBCAP_DOME_M = 0.155, 0.025  # the sheet's domed hubcap, K07's 0.30-0.33 m, its crown at the tyre's
                                          # sidewall (assumed)
TYRE_LOADED_R_M = 0.282   # the sheet's loaded radius of a 165/80 R13 (derived): the wheel centre's height
TYRE_SEGMENTS = (48, 16, 8)  # the wheels' sides per LOD (task 1.4: 32 showed as facets at 1.5 m)
RACK_R_M = 0.01           # 20 mm tubes (the sheet)
RACK_RISE_M = 0.10        # rails over the roof (the sheet, measured M)
PLATE_M = (0.52, 0.112)   # a plate's width and height (published: the standard Ukrainian plate, en.wikipedia
                          # "Vehicle registration plates of Ukraine"); blank, never a character (SEC-02)
PLATE_T_M = 0.001         # its sheet (assumed)
PLATE_OFF_M = 0.003       # its back off the blade's or the rear panel's face, on its bracket (assumed)
# The collision plan's construction numbers (derived from the shapes above against the audit's 5 cm and 2 cm)
FACE_M = 0.006            # the cores' end faces this far proud of the body's front and rear faces (the lamps stand
                          # 1.0-1.8 cm proud: within the bonnet's 2 cm zone of both)
OVERLAP_M = 0.05          # neighbouring cores overlap by this, so no face of one stands bare inside the car
MID_HALF_M = 0.784        # the middle box: within 2 cm of the 0.787-0.806 m flank, and 4.9 cm outboard of the rear
                          # tyres across their arches (the 5 cm tolerance; the rear track sits 5.6 cm in)
LOWER_MID_TOP_M = 0.815   # under the cabin's 0.835 m crease, over the front axle's 0.781 m one (the roll's +-3 cm)
CORE_HALF_M = 0.62        # the cores and wedges: inside the shoulder's roll and the bands
LID_T_M = 0.02            # the thin lids (the sheet's 0.02 m boxes)
CABIN_CORE_TOP_M = 0.93   # the cabin's lower core: under the rear screen's 0.934 m foot
UPPER_HALF_M = 0.60       # the cabin's upper core: inside the bands' boxes
UPPER_TOP_M = 1.30        # and under the roof's boxes, which reach down to it
WS_HALF_M, RS_HALF_M = 0.62, 0.58   # the glass wedges across the panes (0.57-0.62 m and 0.56-0.61 m)
FRONT_WEDGE_HALF_M = 0.66 # the wedge under the bonnet reaches under its crown's edge, into the shoulder's roll
ROOF_BOX_INSET_M = 0.0    # the roof's boxes reach the edge roll's start, where the roll's cylinder takes over
ROOF_BOX_LEVEL = 0.5      # the roof boxes' tops halfway up the flat crown (3 cm at most): within 1.5 cm
BUMPER_R_M = 0.07         # the bumper capsules: the 0.10 m blade, reaching the overriders' faces
OVERRIDER_R_M = 0.035     # an overrider's capsule (its 0.05 m width and rubber tips; 0.19 m tall)
WHEELHOUSE_GAP_M = 0.022  # the front wheelhouse cylinder's radius over the tyre's: the 0.04 m arch's liner within the
                          # bonnet's 2 cm zone, and the cylinder within 4 cm of the tyre where it meets the ground
CABIN_HALF_M = 0.63       # the cabin's lower core, inside the shoulder's roll and the bands
WEDGE_DEPTH_M = 0.4       # the glass wedges reach this far in from the panes, down to the cabin's lower core
PILLAR_R_M = 0.035        # the A and C pillars' cylinders on the cap's edge roll
ARCH_BOX_M = 0.28         # the rear arches' corner boxes reach this far out from the arch's circle
ARCH_FLANK_M = 0.2        # and the arch boxes this far in from the flank
BAND_BELOW_M = 0.12       # the band's boxes reach this far under the belt, into the shoulder's roll
BAND_DEPTH_M = 0.12       # and this far in from the band's plane
BAND_C_INSET_M = 0.01     # the C pillar's box 1 cm inside the band: its plane, yawed with the tail's taper, runs
                          # outside the band where it reaches ahead of the rear screen
PANE_BACK_M = GLASS_INSET_M + GLASS_T_M - 0.001  # the closed car's band boxes over the windows: their outer face 1 mm
                          # inside each pane's glass box and within the band's 15 mm sheet, so a pane is met as glass
PANE_RISE_M = 0.025       # a pane's glass box per stretch over which its top edge rises at most this: under the
                          # frame's 2.9 cm over the pane (derived: WINDOW_V's 7 % of the 0.42 m band)

# --- the kits' numbers (task 1.2; the sheet's, or labelled) ----------------------------------------------------------
RIM_FLANGE_R_M = 0.183    # the sheet: a 13-inch rim's flange radius (the F drop's 0.282 - 0.183 m)
RIM_WIDTH_M = 0.13        # a 5J rim over its flanges (assumed: 165-section tyres)
DOOR_T_M = 0.10           # a door's depth from its skin to its trim at the belt (assumed: the class), and the cabin's
                          # inner side walls this far in from the flank
SILL_T_M = 0.02           # the cabin floor's and the thin shells' collision boxes (the bonnet's 0.02 m lids)
SHELL_T_M = 0.012         # the open cabin's thin boxes over the roof's halves, the panes and the rolls
BAND_SHELL_M = 0.018      # and over the band (its frame sheet is BAND_T_M deep)
NOTCH_DEPTH_M = 0.10      # the struck corner's lamp tear: how far back along the wing (derived: the tear 0.20 x 0.25 m,
                          # measured M, round a lamp bezel of 0.18 m)
NOTCH_LEVEL = 0.55        # its floor on the section level under the lamp (the lamp's foot, 0.595 m, less its ring)
FLAP_ACROSS = 0.6 / 0.45  # measured M: the flap 0.35-0.45 x 0.6 m; bonnet_flap is the 0.45, along the side
DECK_DROP_M = 0.045       # the engine bay's contents under the torn corner stand this far under the bonnet's mean line
                          # (assumed: the class's master cylinder and heater box under the scuttle; M's "engine bay
                          # under the flap"); the pocket's walls stay within AST-05's 2 cm of it or the bonnet
LID_KIT_M = 0.012         # the torn bonnet's boxes, thin over that pocket
COWL_LEVEL_M = 0.1        # the open cabin's wedge under the bonnet ends level this far ahead of the cowl
COWL_STEP_M = 0.002       # the open cabin's body sides step from the wing's crease to the door's over this
# the open cabin's door tops: (width out from the trim, bottom, top over the roll's centre line) of two steps whose
# outer-top corners lie on the shoulder's 0.1 m roll at 36.9 and 55 degrees, so the inner corner between them
# keeps 8.3 cm from the roll's centre (1.7 cm under the skin) and the arc keeps within 2 cm of the steps
DOOR_TOP_STEPS = ((0.08, 0.0, 0.06), (0.0574, 0.05, 0.0819))
# and under the bonnet's torn side edge the wing's roll the same, over the flank's top 0.1 m from the inner wing out
WING_STEPS = ((0.1, -0.1, 0.0),) + DOOR_TOP_STEPS
# the rear wheel tubs' tops in the open cabin, from the arch's front to the upper bulkhead (at 93 degrees): three
# 30-degree pieces, the arc within 1.2 cm of them and their corners 2.4 cm off it (AST-05's 5 cm there)
TUB_PHI_DEG = (165.0, 135.0, 105.0)
TUB_SPAN_DEG = 30.0
TUB_T_M = 0.012
# and a plate (length, height) on each inner wall's cabin face ahead of the lower bulkhead, from the axle's height:
# where the wall lies over 5 cm from the tubs' boxes and the bulkhead's (its top corner 3 cm over the arch)
ARCH_WALL_M = (0.10, 0.20)
FLAP_STRIP_M = 0.035      # the folded flap's collision strips (their square ends within 2 cm of the torn diagonal)
WS_HOLE_M = (0.20, 0.03)  # the windscreen's hole where the flap's edge struck it, up the pane and across: a slit cut by
                          # the sheet's edge (design decision 3 assumed 0.1 x 0.2 m; a slit closes in the pane's glass
                          # collision, as a fragment hole of 0.25 m or less does, and this one within AST-05's 2 cm:
                          # its half-width with the rim's jitter 1.7 cm)
WS_BULGE_M = 0.012        # the crazed pane bulged inward round the hole (assumed; within AST-05's 2 cm of its box)
WS_BULGE_R_M = 0.30
CRUMB_M = (0.010, 0.010, 0.004)   # design decision 9: crumbs 10 x 10 x 4 mm
CRUMBS_PER_WINDOW = 200   # the sheet's 3 600 capped (QA 2.1's R2: LOD0 with its micro meshes under AST-08's 60 000;
                          # 1 000 drew a carpet M does not show)
CRUMB_INSIDE = 0.9        # measured M: crumbs on the seats, almost none on the ground (the sheet assumed 60-80 %)
CRUMB_SPREAD_M = 3.0      # the rest scattered on the ground to this far out from the sill, evenly (the sheet's A: to
                          # 3 m; M: a few chips, never a band under the doors)
BEAD_SECTION_M = 0.006    # K07's bead wire: a 6 mm bundle (the sheet: 5-8 mm)
BEAD_DIAMETER_M = 0.37    # K07_bead_wire's Ø (the sheet: 0.33-0.41 m)
SPRING_R_M = 0.002        # the sheet: burnt seat springs, wire r 2 mm
FRAME_TUBE_R_M = 0.008    # a seat frame's tubes (assumed: 16 mm)
CANVAS = {"side": {-1: (0.0, 0.0), 1: (0.0, 2.0)}, "band": {-1: (4.0, 0.0), 1: (4.0, 2.0)}, "top": (0.0, 4.0),
          "front": (4.0, 4.0), "rear": (6.0, 4.0)}
# The paint's UV canvas: each panel group's metres moved by whole 2 m tiles of car_paint (so the paint looks the
# same), so no two panels share a UV and the damage mask (2048 x 1024 over CANVAS_MIN..CANVAS_MAX) is one atlas.
CANVAS_MIN, CANVAS_MAX, CANVAS_PX = (-2.2, 0.0), (6.8, 5.0), (2048, 1024)
CRACK_PX = (512, 1024)    # design decision 5: the crack mask, 1k on its long side (across the pane), along x 512
CRACK_HITS = (3, 4)       # seeded fragment hits on the pane besides the flap's strike, and the radial cracks per
CRACK_RAYS = (14, 22)     # impact (task 1.4, measured M: three or four small holes, each a web, crazing the whole pane)
CRACK_HAZE, CRACK_HAZE_FLOOR, CRACK_REACH_M = 0.45, 0.4, 0.8  # the fine cracks under a texel as a haze (task 1.4,
                          # measured M: the pane reads pale #b9cbd3 across, not as clear glass with a few lines)

# --- the parameter table (vehicles.md, shared table and R01) --------------------------------------------------------

def _params():
    p = {
        "model": {"type": "str", "range": ", ".join(MODELS), "default": "R01",
                  "effect": "loads the base preset (only R01 is built)"},
        "body": {"type": "str", "range": ", ".join(BODIES), "default": "sedan", "effect": "body style; R01: sedan"},
        "front": {"type": "str", "range": ", ".join(FRONTS), "default": "2101",
                  "effect": "R01's lamp and grille set (only 2101 is built)"},
        "roof_rack": {"type": "bool", "range": "true or false", "default": True,
                      "effect": "the tube rack on the drip rails: snags and landing touch points"},
        "vent_panes": {"type": "bool", "range": "true or false", "default": True,
                       "effect": "front quarter vents in chrome frames"},
    }
    for key, lo, hi, effect in (
            ("length", 3.5, 7.5, "overall length; the side profile scales to it (+-2 % of the model)"),
            ("width", 1.5, 2.6, "overall width; the plan profile scales to it (+-2 % of the model)"),
            ("height", 1.3, 3.1, "overall height; the side profile scales to it (+-2 % of the model)"),
            ("wheelbase", 2.0, 5.0, "axle to axle"), ("track_front", 1.2, 2.1, "front wheel centres apart"),
            ("track_rear", 1.2, 2.1, "rear wheel centres apart"),
            ("ground_clearance", 0.10, 0.45, "the published lowest point (recorded; the underbody follows the preset)"),
            ("overhang_front", 0.5, 1.3, "front bumper to the front axle"),
            ("overhang_rear", 0.5, 1.6, "rear axle to the rear bumper; with wheelbase and overhang_front, length"),
            ("belt_line", 0.80, 1.00, "the window sills: the greenhouse's base"),
            ("sill_height", 0.22, 0.40, "the door bottoms"),
            ("tumblehome", 0.05, 0.20, "the greenhouse side's inward lean from the belt line to the drip rail"),
            ("arch_clearance", 0.02, 0.06, "wheel-arch radius over the tyre radius"),
            ("tyre_diameter", 0.5, 1.2, "the wheel cylinders' diameter"),
            ("tyre_width", 0.12, 0.40, "the wheel cylinders' width")):
        p[key] = {"type": "float", "range": (lo, hi), "default": PRESETS["R01"]["published"].get(key), "effect": effect}
    for name in STATIONS:
        p[f"side_profile.{name}.s"] = {"type": "float", "range": (0.0, 8.0), "default": None,
                                       "effect": f"the {name.replace('_', ' ')} station, metres from the front tip"}
        p[f"side_profile.{name}.z"] = {"type": "float", "range": (0.0, 3.5), "default": None,
                                       "effect": f"the centre-line height at the {name.replace('_', ' ')} station"}
        p[f"plan_profile.{name}"] = {"type": "float", "range": (0.3, 1.3), "default": None,
                                     "effect": f"the half-width at the {name.replace('_', ' ')} station"}
    p.update({
        "wheel": {"type": "str", "range": ", ".join(WHEELS), "default": "steel_hubcap",
                  "effect": "rim geometry (only steel_hubcap is built)"},
        "paint": {"type": "str", "range": ", ".join(PAINTS), "default": "dark_green",
                  "effect": "body colour, by its recipe (only dark_green, car_paint, is built)"},
        "paint_fade": {"type": "float", "range": (0.0, 1.0), "default": 0.3,
                       "effect": "chroma loss and chalk on upward faces: the damage mask's B (its shader binds it later)"},
        "state": {"type": "str", "range": ", ".join(STATES), "default": "A", "effect": "which kit runs"},
        "severity": {"type": "float", "range": (0.0, 1.0), "default": 0.5,
                     "effect": "the kit's master dial: 0 is the clean base (every item neutral); above 0 the state's kit runs with its items as the file sets them"},
        "burst_dir.azimuth": {"type": "float", "range": (0.0, 360.0), "default": 0.0,
                              "effect": "where the burst came from (deg clockwise from the nose seen from above: 90 right, 270 left): the holed side, the struck front corner, the fold, the scatter"},
        "burst_dir.elevation": {"type": "float", "range": (-10.0, 60.0), "default": 0.0,
                                "effect": "the burst's height (deg): the holes' heights (low for a burst on the ground)"},
        "frag_holes": {"type": "int", "range": (0, 60), "default": 16,
                       "effect": "fragment holes on the burst side, 6 in 16 large (measured M); the struck corner's lamp tear is one of them"},
        "frag_hole_size": {"type": "float", "range": (0.01, 0.25), "default": 0.03,
                           "effect": "the small holes' largest diameter (log-uniform from 0.01 m; the large are 0.08-0.18 m): with the 6 in 16 large, the median of all"},
        "rust_halo": {"type": "float", "range": (1.2, 2.5), "default": 1.8, "effect": "halo size per hole"},
        "months_abandoned": {"type": "int", "range": (0, 36), "default": 6,
                             "effect": "dust and rust streaks in the damage mask (B, R)"},
        "flat_tyres.front_left": {"type": "bool", "range": "true or false", "default": False, "effect": "flat"},
        "flat_tyres.front_right": {"type": "bool", "range": "true or false", "default": False, "effect": "flat"},
        "flat_tyres.rear_left": {"type": "bool", "range": "true or false", "default": False, "effect": "flat"},
        "flat_tyres.rear_right": {"type": "bool", "range": "true or false", "default": False, "effect": "flat"},
        "windscreen": {"type": "str", "range": ", ".join(WINDSCREENS), "default": "crazed_holed",
                       "effect": "the laminated pane's state"},
        "bonnet": {"type": "str", "range": ", ".join(BONNETS), "default": "corner_folded", "effect": "bonnet geometry"},
        "bonnet_flap": {"type": "float", "range": (0.2, 0.7), "default": 0.45, "effect": "corner_folded flap size"},
        "door_bulge": {"type": "float", "range": (0.0, 0.25), "default": 0.0, "effect": "outward bow of sprung doors"},
        "roof_push": {"type": "float", "range": (0.0, 0.2), "default": 0.0, "effect": "roof pushed down or sagged"},
        "panel_tears": {"type": "int", "range": (0, 10), "default": 0, "effect": "large tears"},
        "hanging_wiring": {"type": "int", "range": (0, 6), "default": 0, "effect": "looms and loose wires"},
        "bumper.front": {"type": "str", "range": ", ".join(BUMPER_STATES), "default": "on", "effect": "pose"},
        "bumper.rear": {"type": "str", "range": ", ".join(BUMPER_STATES), "default": "on", "effect": "pose"},
        "soot_film": {"type": "float", "range": (0.0, 1.0), "default": 0.0, "effect": "blast soot without burning"},
        "burn": {"type": "float", "range": (0.0, 1.0), "default": 0.0, "effect": "fraction burnt"},
        "burn_origin": {"type": "str", "range": ", ".join(BURN_ORIGINS), "default": "engine",
                        "effect": "where the fire was hottest"},
        "ride_drop": {"type": "float", "range": (0.0, 0.3), "default": 0.0, "effect": "height lost on the rims"},
        "rims_melted": {"type": "bool", "range": "true or false", "default": False, "effect": "alloy rims collapse"},
        "weeks_since_fire": {"type": "int", "range": (0, 52), "default": 4, "effect": "soot to rust"},
        "interior": {"type": "str", "range": ", ".join(INTERIORS), "default": "intact", "effect": "seats and trim"},
        "overgrowth": {"type": "float", "range": (0.0, 1.0), "default": 0.0, "effect": "weeds round the body"},
        "debris_radius": {"type": "float", "range": (1.0, 20.0), "default": 3.0, "effect": "K07 scatter radius"},
        "debris_count": {"type": "int", "range": (0, 300), "default": 12, "effect": "K07 pieces"},
        "crumb_cover": {"type": "float", "range": (0.0, 1.0), "default": 0.0, "effect": "K07 crumb layer"},
        "soot_patch": {"type": "float", "range": (0.0, 6.0), "default": 0.0, "effect": "burnt ground diameter"},
        "ground": {"type": "str", "range": ", ".join(GROUNDS), "default": "soil", "effect": "soot patch, crumbs"},
        "pose": {"type": "str", "range": ", ".join(POSES), "default": "upright", "effect": "rare blast poses"},
        "plate": {"type": "str", "range": ", ".join(PLATES), "default": "none",
                  "effect": "none, or blank plates front and rear: never characters"},
        "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 1, "effect": "every random choice"},
        "lod.lod1_switch_m": {"type": "float", "range": (5.0, 100.0), "default": 20.0,
                              "effect": "LOD1 from here (design 8)"},
        "lod.lod2_switch_m": {"type": "float", "range": (20.0, 400.0), "default": 80.0, "effect": "LOD2"},
        "lod.lod3_switch_m": {"type": "float", "range": (50.0, 1500.0), "default": 300.0, "effect": "LOD3"},
        "lod.cull_m": {"type": "float", "range": (300.0, 3000.0), "default": 1500.0,
                       "effect": "culled beyond: the 1.44 m height under a pixel at 935 px/rad"},
    })
    for corner in CORNERS:
        p[f"glass_side.{corner}"] = {"type": "str", "range": ", ".join(GLASS_STATES), "default": "intact",
                                     "effect": "the side windows of that door"}
        p[f"doors.{corner}"] = {"type": "str", "range": ", ".join(DOOR_STATES), "default": "closed",
                                "effect": "door geometry"}
    return p


PARAMS = _params()
# The clean base: every kit item at the value that changes nothing (check refuses anything else until task 1.2).
NEUTRAL = {"severity": 0.0, "frag_holes": 0, "months_abandoned": 0, "windscreen": "intact", "bonnet": "closed",
           "door_bulge": 0.0, "roof_push": 0.0, "panel_tears": 0, "hanging_wiring": 0, "bumper.front": "on",
           "bumper.rear": "on", "soot_film": 0.0, "burn": 0.0, "ride_drop": 0.0, "rims_melted": False,
           "interior": "intact", "overgrowth": 0.0, "debris_count": 0, "crumb_cover": 0.0, "soot_patch": 0.0,
           "paint_fade": 0.0}
NEUTRAL.update({f"flat_tyres.{c}": False for c in CORNERS})
NEUTRAL.update({f"glass_side.{c}": "intact" for c in CORNERS})
NEUTRAL.update({f"doors.{c}": "closed" for c in CORNERS})
TOLERANCE = 0.02  # length, width, height and the published dimensions: +-2 % of the model (the shared table)


def check(params):
    """Cross-parameter rules once every key is in range: the enumerations, what is built, the model's published
    dimensions within 2 %, the overhangs and wheelbase adding up to the length, ordered stations and LODs."""
    flat = _flat(params)
    problems = []
    for key, allowed in (("model", MODELS), ("body", BODIES), ("front", FRONTS), ("wheel", WHEELS), ("paint", PAINTS),
                         ("state", STATES), ("windscreen", WINDSCREENS), ("bonnet", BONNETS),
                         ("bumper.front", BUMPER_STATES), ("bumper.rear", BUMPER_STATES), ("burn_origin", BURN_ORIGINS),
                         ("interior", INTERIORS), ("ground", GROUNDS), ("pose", POSES), ("plate", PLATES)):
        if flat[key] not in allowed:
            problems.append(f"'{key}' = {flat[key]!r} must be one of {', '.join(allowed)}")
    for corner in CORNERS:
        if flat[f"glass_side.{corner}"] not in GLASS_STATES:
            problems.append(f"'glass_side.{corner}' must be one of {', '.join(GLASS_STATES)}")
        if flat[f"doors.{corner}"] not in DOOR_STATES:
            problems.append(f"'doors.{corner}' must be one of {', '.join(DOOR_STATES)}")
    if problems:
        return problems
    for key, built, why in (("model", ("R01",), "only R01's preset exists"), ("front", ("2101",), "the 2101 front only"),
                            ("wheel", ("steel_hubcap",), "R01's steel wheels only"),
                            ("paint", ("dark_green",), "only car_paint (dark green) has a recipe"),
                            ("pose", ("upright",), "the ditch and side poses are B's (P2)")):
        if flat[key] not in built:
            problems.append(f"'{key}' = {flat[key]!r} is not built yet: {why}")
    if flat["model"] == "R01" and flat["body"] != PRESETS["R01"]["body"]:
        problems.append(f"'body' = {flat['body']!r} is not built yet: R01 is a sedan (the estate is a later option)")
    if flat["state"] == "B":
        problems.append("'state' = 'B' is not built yet (not in this change)")
    elif flat["severity"] == 0:
        kit = [k for k, v in NEUTRAL.items() if flat[k] != v]
        if kit:
            problems.append("'severity' = 0 is the clean base, so it needs "
                            + ", ".join(f"'{k}' = {NEUTRAL[k]!r}" for k in kit))
    else:
        problems += _kit_problems(flat)
    if problems or flat["model"] not in PRESETS:
        return problems
    published = PRESETS[flat["model"]]["published"]
    for key, value in published.items():
        if abs(flat[key] - value) > TOLERANCE * value:
            problems.append(f"'{key}' = {flat[key]} is more than {TOLERANCE:.0%} off the model's published {value}")
    total = flat["overhang_front"] + flat["wheelbase"] + flat["overhang_rear"]
    if abs(total - flat["length"]) > 0.001:
        problems.append(f"'overhang_front' + 'wheelbase' + 'overhang_rear' = {total:.4f} must make 'length' "
                        f"{flat['length']} within 1 mm")
    s = [flat[f"side_profile.{n}.s"] for n in STATIONS]
    if s[0] != 0.0 or any(b <= a for a, b in zip(s, s[1:])):
        problems.append(f"'side_profile.*.s' must start at 0 and increase in the order {', '.join(STATIONS)}: {s}")
    if not flat["sill_height"] < flat["belt_line"]:
        problems.append("'sill_height' must be under 'belt_line'")
    lod = params["lod"]
    if not lod["lod1_switch_m"] < lod["lod2_switch_m"] < lod["lod3_switch_m"] < lod["cull_m"]:
        problems.append(f"'lod.*' must increase: {lod['lod1_switch_m']}, {lod['lod2_switch_m']}, "
                        f"{lod['lod3_switch_m']}, {lod['cull_m']}")
    if not problems and flat["state"] == "A" and flat["frag_holes"] > 0:
        try:
            Kit(Body(params), params)
        except ValueError as e:  # the holes do not fit on the burst side's panels
            problems.append(f"'frag_holes' = {flat['frag_holes']}: {e}")
    return problems


A_BUILT = {"interior": ("intact", "crumbs"), "windscreen": ("intact", "crazed_holed"),
           "bonnet": ("closed", "corner_folded"), "burn_origin": BURN_ORIGINS}
F_BUILT = {"interior": ("burnt_frames",), "windscreen": ("gone",), "bonnet": ("closed",),
           "burn_origin": ("engine", "cabin")}
# kit items neither kit of this change moves (B's, or later options): they must stay at their neutral value
UNBUILT = ("door_bulge", "panel_tears", "hanging_wiring", "bumper.front", "bumper.rear", "soot_film", "overgrowth",
           "rims_melted") + tuple(f"doors.{c}" for c in CORNERS)


def _kit_problems(flat):
    """The A and F kits' rules (design decisions 3 and 4): what each builds, and that every other item stays
    neutral ("not built yet")."""
    problems = []
    state = flat["state"]
    built = A_BUILT if state == "A" else F_BUILT
    for key, values in built.items():
        if flat[key] not in values:
            problems.append(f"'{key}' = {flat[key]!r} is not built yet in state {state} "
                            f"({', '.join(values)})")
    for key in UNBUILT:
        if flat[key] != NEUTRAL[key]:
            problems.append(f"'{key}' = {flat[key]!r} is not built yet: the A and F kits need {NEUTRAL[key]!r}")
    if flat["ground"] != "soil":
        problems.append(f"'ground' = {flat['ground']!r} is not built yet: the gallery's soil only")
    glass = [flat[f"glass_side.{c}"] for c in CORNERS]
    if any(g == "crazed_shards" for g in glass):
        problems.append("'glass_side.*' = 'crazed_shards' is not built yet (intact or gone)")
    flats = [c for c in CORNERS if flat[f"flat_tyres.{c}"]]
    if state == "A":
        for key in ("burn", "ride_drop", "soot_patch", "roof_push"):
            if flat[key] != 0:
                problems.append(f"'{key}' = {flat[key]} is the F kit's: state A needs 0")
        if len(flats) > 1:
            problems.append(f"'flat_tyres' = {', '.join(flats)}: more than one flat tyre is not built yet")
        if flat["windscreen"] == "crazed_holed" and flat["bonnet"] != "corner_folded":
            problems.append("'windscreen' = 'crazed_holed' needs 'bonnet' = 'corner_folded': the hole is where the "
                            "flap's edge struck it")
        if flat["frag_hole_size"] >= kits.LARGE_MIN_M:
            problems.append(f"'frag_hole_size' = {flat['frag_hole_size']} must stay under the large holes' "
                            f"{kits.LARGE_MIN_M} m (it is the small holes' largest diameter)")
        bv = kits.burst_vector(flat["burst_dir.azimuth"], flat["burst_dir.elevation"])
        if (flat["frag_holes"] > 0 or flat["debris_count"] > 0) and bv[0] >= 0:
            problems.append(f"'burst_dir.azimuth' = {flat['burst_dir.azimuth']}: a burst from behind the car's "
                            "middle (a struck rear corner) is not built yet")
        if flat["bonnet"] == "corner_folded" and flat["frag_holes"] == 0:
            problems.append("'bonnet' = 'corner_folded' needs 'frag_holes' > 0: the fold is the struck front's")
    else:
        if flat["burn"] <= 0:
            problems.append("'burn' must be above 0 in state F")
        if any(g != "gone" for g in glass):
            problems.append("'glass_side.*' must be 'gone' in state F: the fire takes the glass")
        if flats:
            problems.append("'flat_tyres.*' must be false in state F: the tyres are burnt away, the car is on its rims")
        if flat["ride_drop"] < TYRE_LOADED_R_M - RIM_FLANGE_R_M:
            problems.append(f"'ride_drop' = {flat['ride_drop']} is under the tyre's own loss "
                            f"{TYRE_LOADED_R_M - RIM_FLANGE_R_M:.3f} m (loaded radius less the rim's flange)")
        for key in ("frag_holes", "debris_count", "crumb_cover"):
            if flat[key] != 0:
                problems.append(f"'{key}' = {flat[key]} is not built yet in state F (no strike fragments at G1)")
    return problems


def _flat(value, prefix=""):
    if isinstance(value, dict):
        out = {}
        for k, v in value.items():
            out.update(_flat(v, f"{prefix}{k}."))
        return out
    return {prefix[:-1]: value}


# --- the body's numbers -------------------------------------------------------------------------------------------

class Body:
    """Everything the meshes and the collision are built from: the scaled profiles, the stations, the wheels."""

    def __init__(self, params):
        self.p = params
        preset = PRESETS[params["model"]]
        self.preset = preset
        self.L, self.W, self.H = params["length"], params["width"], params["height"]
        side = [(params["side_profile"][n]["s"], params["side_profile"][n]["z"]) for n in STATIONS]
        ks, kz = self.L / side[-1][0], self.H / max(z for _, z in side)
        self.side = [(s * ks, z * kz) for s, z in side]
        plan = [params["plan_profile"][n] for n in STATIONS]
        kw = self.W / 2 / max(plan)
        self.plan = [(s, w * kw) for (s, _), w in zip(self.side, plan)]
        self.st = {n: s for n, (s, _) in zip(STATIONS, self.side)}
        self.s_front, self.s_rear = self.st["bonnet_front"], self.st["boot_edge"]
        self.s_cowl, self.s_rs_base = self.st["cowl"], self.st["rear_screen_base"]
        self.s_ws_top, self.s_rs_top = self.st["windscreen_top"], self.st["rear_screen_top"]
        self.s_boot = self.st["boot_front"]
        self.s_scuttle = self.s_cowl - preset["scuttle_m"]
        self.belt, self.sill = params["belt_line"], params["sill_height"]
        self.tumble = params["tumblehome"]
        self.ks, self.kz = ks, kz
        self.z_re = preset["drip_z"] * kz  # the preset's heights and stations are on the R01 params' scale
        self.H_band = self.z_re - self.belt
        self.axle = (params["overhang_front"], params["overhang_front"] + params["wheelbase"])
        self.track = (params["track_front"], params["track_rear"])
        self.r_tyre, self.w_tyre = params["tyre_diameter"] / 2, params["tyre_width"]
        self.z_wc = TYRE_LOADED_R_M * self.r_tyre / 0.297  # the loaded radius, scaled with the tyre
        self.r_arch = self.r_tyre + params["arch_clearance"]
        self.w_in = tuple(t / 2 - self.w_tyre / 2 - WHEEL_INNER_GAP_M for t in self.track)
        self.doors = tuple(d * ks for d in preset["doors"])
        self.underbody = tuple((s * ks, z) for s, z in preset["underbody"])
        # the edge line (z_e): the A pillar's top at the windscreen's top, the drip level until the roof's rear
        # slope falls to EDGE_R_M over it, then EDGE_R_M under the slope to the rear screen's top, the C pillar down
        self.s_a_top, self.s_c_top = self.s_ws_top, self.s_rs_top
        s_rr = self.st["roof_rear"]
        z0, z1 = self.z_top(s_rr) - EDGE_R_M, self.z_top(self.s_rs_top) - EDGE_R_M
        self.s_drip_end = s_rr + (self.s_rs_top - s_rr) * min(1.0, max(0.0, (z0 - self.z_re) / (z0 - z1)))
        self.edge_line = [(self.s_cowl, self.z_sh(self.s_cowl)), (self.s_a_top, self.z_re),
                          (self.s_drip_end, self.z_re), (self.s_c_top, min(self.z_re, z1)),
                          (self.s_rs_base, self.belt)]
        # the F kit's sagged roof (roof_push): a dish over the flat crown, deepest on the crease across the roof
        # halfway between the windscreen's and the rear screen's tops, where the profile along the car kinks
        self.sag_m = params["roof_push"]
        self.s_crease = (self.s_ws_top + self.s_rs_top) / 2

    def sag(self, s, f):
        """The roof's sag at station s and section fraction f of the flat crown (0 centre, 1 its edge)."""
        if self.sag_m <= 0 or not self.s_ws_top < s < self.s_rs_top or f >= 1:
            return 0.0
        a, b = (self.s_ws_top, self.s_crease) if s <= self.s_crease else (self.s_rs_top, self.s_crease)
        along = math.sin(math.pi / 2 * (s - a) / (b - a))
        return self.sag_m * along * math.cos(math.pi / 2 * f) ** 2

    def uv(self, group, side=0):
        """The paint's UV in its canvas (CANVAS): (u, v) in metres moved by the group's whole tiles."""
        off = CANVAS[group][side] if isinstance(CANVAS[group], dict) else CANVAS[group]
        return lambda u, v: (u + off[0], v + off[1])

    def x(self, s):
        return s - self.L / 2

    def P(self, s, w, y):
        """The asset-space point of station s, lateral w (positive right, +Z) and height y."""
        return (s - self.L / 2, y, w)

    def hw(self, s):
        return loft.interp(self.plan, s)

    def z_top(self, s):
        return loft.interp(self.side, s)

    def z_bot(self, s):
        return loft.interp(self.underbody, s)

    def z_sh(self, s):
        """The shoulder line: in front of the cowl the bonnet's edge, its crown growing from CROWN_FRONT_M at the
        front face to CROWN_MAX_M at the front axle and held there to the cowl; the belt in the cabin; behind the
        rear screen the boot's edge on the side profile, under the belt."""
        if s <= self.s_cowl:
            return self.z_top(s) - loft.interp([(self.s_front, CROWN_FRONT_M), (self.axle[0], CROWN_MAX_M)], s)
        if s <= self.s_rs_base:
            return self.belt
        return min(self.z_top(s), self.belt)

    def crown(self, s):
        """The top's rise from its edge to its centre line."""
        if s <= self.s_cowl:
            return max(0.0, self.z_top(s) - self.z_sh(s))
        return 0.0  # behind the rear screen the side profile is the boot's edge; the lid is flat across

    def z_c(self, s):
        return self.z_sh(s) - ROLL_R_M

    def w_t(self, s):
        """The roll's top: the tops' and the band's edge."""
        return self.hw(s) - ROLL_R_M

    def z_e(self, s):
        """The greenhouse's edge line: the A pillar from the scuttle's edge to the drip rail at the windscreen's
        top, the drip rail level, then EDGE_R_M under the side profile down the roof's rear slope, the C pillar from
        the rear screen's top to the belt at its foot."""
        return loft.interp(self.edge_line, s)

    def w_band(self, s, h):
        """The band's lateral offset h above the belt."""
        return self.w_t(s) - self.tumble * h / self.H_band

    def w_e(self, s):
        return self.w_band(s, self.z_e(s) - self.belt)

    def arch(self, s):
        """(axle index, the arch edge's height) where s lies over an arch, else None."""
        for k, a in enumerate(self.axle):
            d = s - a
            if abs(d) < self.r_arch:
                return k, self.z_wc + math.sqrt(self.r_arch * self.r_arch - d * d)
        return None

    def lateral(self, s, y):
        """The body side's half-width at height y below the roll (the tuck, then the vertical flank)."""
        y1 = self.z_bot(s) + TUCK_H_M
        return self.hw(s) - TUCK_W_M * max(0.0, (y1 - y) / TUCK_H_M)

    def top_point(self, s, u):
        """A point of the bonnet, scuttle, rear strip or boot lid at lateral fraction u of the roll's top."""
        y = self.z_sh(s) + self.crown(s) * (1 - abs(u) ** CROWN_POWER)
        return self.P(s, u * self.w_t(s), y)

    def band_point(self, s, v, side):
        h = v * (self.z_e(s) - self.belt)
        return self.P(s, side * self.w_band(s, h), self.belt + h)

    def cap_point(self, s, u):
        """A point of the cap (windscreen, roof, rear screen) at signed section parameter u in [-1, 1]: |u| up to
        CAP_FLAT a gentle parabolic crown across the flat part, beyond it a quarter round of radius
        min(EDGE_R_M, crown) down to the drip line (the roof's edge roll, which a capsule follows exactly)."""
        w, y = self.cap_section(s, abs(u))
        return self.P(s, math.copysign(w, u) if u else 0.0, y)

    def cap_section(self, s, t):
        ze, we = self.z_e(s), self.w_e(s)
        crown = max(0.0, self.z_top(s) - ze)
        r = min(EDGE_R_M, crown)
        wf = we - r
        if t <= CAP_FLAT:
            f = t / CAP_FLAT
            return f * wf, ze + r + (crown - r) * (1 - f * f) - self.sag(s, f)
        a = math.pi / 2 * (t - CAP_FLAT) / (1 - CAP_FLAT)
        return wf + r * math.sin(a), ze + r * math.cos(a)

    def cap_point_w(self, s, w):
        """The cap's point at lateral w on its flat part (|w| up to the edge roll)."""
        ze, we = self.z_e(s), self.w_e(s)
        crown = max(0.0, self.z_top(s) - ze)
        wf = we - min(EDGE_R_M, crown)
        return self.cap_point(s, math.copysign(CAP_FLAT * min(abs(w) / wf, 1.0), w))

    def cap_height(self, s, w):
        """The cap's height over lateral w at station s: its flat crown, then its edge roll down to the drip line;
        None beyond the drip."""
        ze, we = self.z_e(s), self.w_e(s)
        r = min(EDGE_R_M, max(0.0, self.z_top(s) - ze))
        wf = we - r
        if abs(w) <= wf:
            return self.cap_section(s, CAP_FLAT * abs(w) / wf)[1]
        if abs(w) >= we:
            return None
        return ze + math.sqrt(max(r * r - (abs(w) - wf) ** 2, 0.0))

    def roof_edge(self, s):
        """(lateral, height, radius) of the roof's edge roll's centre line at s."""
        ze, we = self.z_e(s), self.w_e(s)
        r = min(EDGE_R_M, max(0.0, self.z_top(s) - ze))
        return we - r, ze, r

# --- the state kit's decisions ---------------------------------------------------------------------------------------

class Kit:
    """The decisions of one variant's state kit (design decisions 3, 4 and 7), all from its params and seed. The
    clean base (severity 0) decides nothing: no glass gone, no hole, the identity transform.

    - The burst (burst_dir): the side facing it is holed (`side`), its front corner struck.
    - Glass per side and window pair (0 the front door's vent and window, 1 the rear door's window and quarter),
      the windscreen, the rear screen (gone in F); the cabin is `open` once any pane is gone.
    - The body's transform `T`: A's body settled on its four springs over its flat tyre (kits.lean), F's drop onto
      the rims; the hubs in the ground frame (the flat one the tyre's drop down, moved by `W`; F's at the rim's
      flange radius).
    - A: the holes on the burst side, the struck corner's lamp tear (`notch`), the bonnet's folded corner (`flap`)
      and the windscreen's hole where its edge struck.
    - F: the burnt-off steel's reach (`burnt`)."""

    def __init__(self, body, params):
        flat = _flat(params)
        self.body, self.p = body, params
        self.state = params["state"]
        self.clean = params["severity"] == 0
        self.A = not self.clean and self.state == "A"
        self.F = not self.clean and self.state == "F"
        self.burst = kits.burst_vector(flat["burst_dir.azimuth"], flat["burst_dir.elevation"])
        self.side = -1 if self.burst[2] < 0 else 1
        self.glass = {}
        for corner in CORNERS:
            side = -1 if corner.endswith("left") else 1
            self.glass[side, 0 if corner.startswith("front") else 1] = flat[f"glass_side.{corner}"]
        self.windscreen = params["windscreen"]
        self.rear_screen = "gone" if self.F else "intact"
        self.open = ("gone" in self.glass.values() or self.windscreen == "gone" or self.rear_screen == "gone")
        hubs = {}
        for k, a in enumerate(body.axle):
            for side, tag in ((-1, "left"), (1, "right")):
                hubs[f"{('front', 'rear')[k]}_{tag}"] = body.P(a, side * body.track[k] / 2, body.z_wc)
        self.flat = next((c for c in CORNERS if flat[f"flat_tyres.{c}"]), None) if self.A else None
        if self.F:
            self.T = kits.translation((0.0, -params["ride_drop"], 0.0))
        elif self.flat:
            self.T = kits.lean(hubs, self.flat)
        else:
            self.T = kits.Rigid()
        self.hubs = {}
        for corner, h in hubs.items():
            if corner == self.flat:
                # the flat wheel hangs on its unloaded spring under the settled body, its hub the tyre's whole drop
                # down; `W` moves it there with the body's tilt
                moved = self.T.point(h)
                self.hubs[corner] = (moved[0], h[1] - kits.FLAT_DROP_M, moved[2])
                self.W = self.T.then(kits.translation((0.0, self.hubs[corner][1] - moved[1], 0.0)))
            elif self.F:
                self.hubs[corner] = (h[0], RIM_FLANGE_R_M, h[2])
            else:
                self.hubs[corner] = h
        self.holes, self.notch, self.flap, self.ws_hole = [], None, None, None
        if self.A and params["frag_holes"] > 0:
            self.notch = self._notch(body)
            n_large = int(round(params["frag_holes"] * kits.LARGE_SHARE))
            bias = 2.0 ** (1 - flat["burst_dir.elevation"] / 30.0)
            self.holes = kits.sample_holes(max(n_large - 1, 0), params["frag_holes"] - n_large,
                                           params["frag_hole_size"], params["rust_halo"], self.side,
                                           kits.rng_for(params, "holes"), _HoleRegion(body, self), bias)
        # the windscreen's metric along the car: a metre of s is ws_k metres up the raked pane
        rise = body.z_top(body.s_ws_top) - body.z_top(body.s_cowl)
        self.ws_k = math.hypot(1.0, rise / (body.s_ws_top - body.s_cowl))
        if self.A and params["bonnet"] == "corner_folded":
            self.flap = _Flap(body, self, params["bonnet_flap"])
            if self.windscreen == "crazed_holed":  # where the flap struck: (s ws_k, w), elongated up the pane
                s, w = self.flap.contact
                along, across = WS_HOLE_M
                hole = kits.Hole(0, s * self.ws_k, w, along, 1.0, kits.rng_for(params, "windscreen hole"),
                                 cut=True, ellipse=(along / 2, across / 2))
                hole.large, hole.petals = False, []
                self.ws_hole = hole

    def _notch(self, body):
        """The struck front corner's lamp tear (the sheet's torn wing round the headlamp, 0.20 x 0.25 m): the lamp,
        its ring and the wing round it gone back NOTCH_DEPTH_M, from the section's NOTCH_LEVEL up through the roll,
        inboard to the lamp's inner edge."""
        lw, ly, lr = body.preset["headlamp"]
        return {"s0": body.s_front, "s1": body.s_front + NOTCH_DEPTH_M, "w_in": lw - lr, "side": self.side,
                "level": NOTCH_LEVEL}

    def notch_y(self, s):
        """The notch's floor height at station s (on its section level)."""
        b = self.body
        y1 = b.z_bot(s) + TUCK_H_M
        return y1 + NOTCH_LEVEL * (b.z_c(s) - y1)

    def in_notch(self, s, w, y):
        n = self.notch
        return (n is not None and n["s0"] - 1e-9 <= s <= n["s1"] + 1e-9 and w * n["side"] >= n["w_in"] - 1e-9
                and y >= self.notch_y(s) - 1e-9)

    def burnt(self, p):
        """F: whether the body's point p (body frame) is burnt to bare steel: within reach of the hottest place
        (burn_origin) or on the cabin roof's crown, its border ragged by a seeded noise; the reach scales with
        `burn` (assumed shapes: the sheet's engine bay, cabin roof and the like)."""
        b, burn = self.body, self.p["burn"]
        s, w, y = p[0] + b.L / 2, p[2], p[1]
        rough = 0.08 * _value_noise(s * 6.0, y * 6.0, w * 6.0, self.p["seed"])
        if self.p["burn_origin"] == "engine":
            centre, reach = (b.axle[0] + 0.15, 0.75, 0.0), 0.62 * burn
        else:
            centre, reach = ((b.s_cowl + b.s_rs_base) / 2, 1.1, 0.0), 0.70 * burn
        d = math.sqrt(((s - centre[0]) / 1.15) ** 2 + (y - centre[1]) ** 2 + (w - centre[2]) ** 2)
        if d < reach + rough:
            return True
        roof = b.s_ws_top - 0.05 < s < b.s_rs_top + 0.05 and y > b.z_re + 0.01 and abs(w) < 0.62 * burn + rough
        return roof


def _value_noise(x, y, z, seed):
    """A smooth seeded noise in [-1, 1] (trilinear value noise on the integer lattice)."""
    def h(i, j, k):
        n = (i * 73856093) ^ (j * 19349663) ^ (k * 83492791) ^ (seed * 2654435761)
        n = (n ^ (n >> 13)) * 1274126177
        return ((n ^ (n >> 16)) & 0xFFFF) / 32767.5 - 1.0
    i, j, k = math.floor(x), math.floor(y), math.floor(z)
    fx, fy, fz = x - i, y - j, z - k
    sx, sy, sz = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy), fz * fz * (3 - 2 * fz)
    out = 0.0
    for di in (0, 1):
        for dj in (0, 1):
            for dk in (0, 1):
                wgt = (sx if di else 1 - sx) * (sy if dj else 1 - sy) * (sz if dk else 1 - sz)
                out += wgt * h(i + di, j + dj, k + dk)
    return out


class _HoleRegion:
    """Where a seeded hole's patch of radius R fits on the burst side's flank (kits.sample_holes): inside the flank's
    vertical band, clear of the wheel arches, the door lines and the sill's shut line, behind the struck corner's
    tear, and ZONE_CLEAR_M from the bonnet's and the boot lid's landing faces (where a hole closed in collision
    would stand more than AST-05's 2 cm off the visual)."""

    def __init__(self, body, kit):
        self.b, self.kit = body, kit
        s_lo = body.s_front + NOTCH_DEPTH_M + 0.05
        self.bounds = (s_lo, body.s_rear - 0.10, body.z_bot(1.0) + TUCK_H_M, body.belt)

    def __call__(self, s, y, r):
        b = self.b
        for ds in (-r, 0.0, r):
            q = s + ds
            if not b.s_front + NOTCH_DEPTH_M + 0.03 < q < b.s_rear - 0.05:
                return False
            if y - r < b.z_bot(q) + TUCK_H_M + 0.01 or y + r > b.z_c(q) - 0.01:
                return False
        for a in b.axle:  # the square's corner reaches r sqrt(2)
            if math.hypot(s - a, y - b.z_wc) < b.r_arch + 1.42 * r + 0.03:
                return False
        g = DOOR_GAP_M
        for d in b.doors:
            if abs(s - d) < r + g + 0.01:
                return False
        if b.doors[0] - g < s < b.doors[2] + g and abs(y - b.sill) < r + g + 0.01:
            return False
        point = (s, y, self.kit.side * b.hw(s))
        for s0, s1, half in ((b.s_front, b.s_cowl, b.w_t(b.s_front)), (b.s_rs_base, b.s_rear, b.w_t(b.s_rear))):
            cs = min(max(point[0], s0), s1)
            cw = min(max(point[2], -half), half)
            d = math.sqrt((point[0] - cs) ** 2 + (point[1] - _top_mean(b, cs)) ** 2 + (point[2] - cw) ** 2)
            if d < ZONE_CLEAR + r:
                return False
        return True


ZONE_CLEAR = kits.ZONE_CLEAR_M


def _ws_hole_reach(body, s):
    """How far out across the windscreen the centre of its hole may lie at station s: the pane's edge (the cap's
    glass reach) less the half-width of the hole's patch (kits.Hole.patch_half) and 5 mm."""
    ze, we = body.z_e(s), body.w_e(s)
    wf = we - min(EDGE_R_M, max(0.0, body.z_top(s) - ze))
    half = WS_HOLE_M[1] / 2 * (1 + kits.RIM_JITTER) + kits.PATCH_RING_M + 0.01
    return body.preset["windscreen_glass"][1] / CAP_FLAT * wf - half - 0.005


class _Flap:
    """The bonnet's rear corner on the burst side, torn along a diagonal and folded up about the bonnet's rear edge
    until it leans on the windscreen (design decision 3; measured M: about 0.35-0.45 x 0.6 m). In the bonnet's
    (s, u) terms: the corner C at its rear edge and side edge, A on the rear edge `across` m in, B on the side edge
    `along` m forward; the hinge C-A, the tear A-B. `angle` is the fold (from the bonnet's plane), found where the
    flap first meets the windscreen, plus what puts the contact 1 cm through the pane, into its hole."""

    def __init__(self, body, kit, along):
        self.b, self.side = body, kit.side
        across = along * FLAP_ACROSS
        g = DOOR_GAP_M / 2
        self.s_b = body.s_scuttle - g             # the bonnet's rear edge
        self.s_B = self.s_b - along
        self.u_max = 1 - DOOR_GAP_M / body.w_t(body.s_front)
        self.u_C = self.side * self.u_max          # signed: the burst side's edge
        self.u_A = self.side * (self.u_max - across / body.w_t(self.s_b))
        self.hinge = (body.top_point(self.s_b, self.u_C), body.top_point(self.s_b, self.u_A))
        self.angle = self._fold()

    def u_tear(self, s):
        """The tear's u at station s (s_B..s_b): from the side edge at B to A on the rear edge."""
        t = (s - self.s_B) / (self.s_b - self.s_B)
        return self.u_C + (self.u_A - self.u_C) * t

    def flat_point(self, s, f):
        """A point of the flap before folding: at station s, fraction f from the side edge (0) to the tear (1)."""
        u = self.u_C + (self.u_tear(s) - self.u_C) * f
        return self.b.top_point(s, u)

    def rigid(self, angle=None):
        a, c = self.hinge
        axis = normalize(sub(c, a))
        # positive angle lifts the flap's front (the -X side of the hinge) up: choose the sign that raises B
        rot = kits.axis_rotation(a, axis, angle if angle is not None else self.angle)
        return rot

    def _fold(self):
        b = self.b
        a, c = self.hinge
        axis = normalize(sub(c, a))
        tip = self.flat_point(self.s_B, 0.0)
        sign = 1.0 if kits.axis_rotation(a, axis, 0.3).point(tip)[1] > tip[1] else -1.0
        samples = [self.flat_point(self.s_B + (self.s_b - self.s_B) * i / 8, j / 6) for i in range(8) for j in range(7)]

        def depth(angle):
            """How far the deepest flap sample lies under the windscreen's glass or its frame (positive: through)."""
            rot = kits.axis_rotation(a, axis, sign * angle)
            worst, where = -1.0, None
            for q in samples:
                p = rot.point(q)
                s, w = p[0] + b.L / 2, p[2]
                if not b.s_cowl + 0.03 < s < b.s_ws_top - 0.03:
                    continue
                height = b.cap_height(s, w)
                if height is None:
                    continue
                glass = height - GLASS_INSET_M
                if glass - p[1] > worst:
                    worst, where = glass - p[1], p
            return worst, where

        lo, hi = math.radians(60), math.radians(179)
        if depth(hi)[0] < 0:
            raise ValueError("the bonnet's flap does not reach the windscreen: a larger 'bonnet_flap'")
        for _ in range(60):
            mid = (lo + hi) / 2
            if depth(mid)[0] > 0:
                hi = mid
            else:
                lo = mid
        _, point = depth(hi)
        self.sign = sign
        # the windscreen's hole: where the flap struck, moved in across the pane until the hole lies in the glass
        s = point[0] + b.L / 2
        reach = _ws_hole_reach(b, s)
        self.contact = (s, max(-reach, min(reach, point[2])))
        return sign * hi

    def outline(self):
        """The folded flap's corners C, A, B in asset space (body frame)."""
        rot = self.rigid()
        c = self.hinge[0]
        return [c, self.hinge[1], rot.point(self.flat_point(self.s_B, 0.0))]


# --- build --------------------------------------------------------------------------------------------------------

QUALITY = (  # per LOD: s step, roll steps, thickness on, details on, wheel segments
    {"step": 0.05, "roll": ROLL_STEPS, "thick": True, "detail": 2, "seg": TYRE_SEGMENTS[0]},
    {"step": 0.10, "roll": 3, "thick": False, "detail": 1, "seg": TYRE_SEGMENTS[1]},
    {"step": 0.25, "roll": 1, "thick": False, "detail": 0, "seg": TYRE_SEGMENTS[2]},
)


def build(params):
    body = Body(params)
    kit = Kit(body, params)
    pieces = _pieces(body, kit)
    snags = []
    lods = []
    for n, q in enumerate(QUALITY):
        mesh = _lod(body, q, kit, snags if n == 0 else None)
        if n == 0:  # the car's own bounds, without what lies round it: the LOD group's frame (QA 2.1's S2)
            lod_bounds = mesh.bounds()
        if q["detail"] >= 1:
            for piece in pieces:
                kits.append_mesh(mesh, piece["lods"][0 if q["thick"] else 1], piece["rigid"], piece["slots"])
        lods.append(mesh)
    lods.append(_box_hull(body, kit))
    lod = params["lod"]
    plan = collision_plan(body, kit, pieces)
    micro = _micro(body, kit)
    slots = sorted({slot for m in lods for slot in m.slots()} | {slot for m in micro for slot in m["mesh"].slots()})
    textures = _textures(body, kit)
    lines = [f"collision plan: {len(plan)} primitives ({_plan_summary(plan)})"]
    if not kit.clean:
        lines.append(f"collision plan by group: {_plan_groups(plan)}")
        lines.append(f"kit: {_kit_summary(body, kit)}")
    return {
        "lods": lods,
        "lod_bounds": lod_bounds,
        "switch_distances_m": [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"], lod["lod3_switch_m"]],
        "thin_wire": [False] * 4,
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": s, "physics": PHYSICS[s]} for s in slots],
        "collision": [shape for _, shape in plan],
        "gaps": [],
        "wind_volumes": _wind(body, kit),
        "holes": [],
        "micro": micro,
        "textures": textures,
        "snag": _snags(body, kit, pieces) + [kits.transform_snag(s, kit.T) for s in snags],
        "lines": lines,
    }


def _lod(body, q, kit, snags):
    """One LOD of the car itself: the body's parts in its own frame, moved by the kit's transform, the wheels on the
    ground in theirs (build adds the pieces round it). `snags` collects the kit's torn edges (LOD0's)."""
    mesh = Mesh()
    extra_levels = _kit_levels(body, kit, q)
    for side in (-1, 1):
        _body_side(mesh, body, side, q, kit, extra_levels, snags)
        _wheelhouses(mesh, body, side, q)
        _band(mesh, body, side, q, kit)
    _tops(mesh, body, q, kit, snags)
    _ends(mesh, body, q, kit, extra_levels, snags)
    _underbody(mesh, body, q)
    _cap(mesh, body, q, kit)
    _glass(mesh, body, q, kit, snags)
    _bumpers(mesh, body, q, kit)
    _front_details(mesh, body, q, kit)
    _rear_details(mesh, body, q, kit)
    if q["detail"] >= 2 and not kit.F:  # F: the wiper arms fell with the windscreen
        for pivot, tip in _wiper_arms(body):
            _tube(mesh, [pivot, tip], 0.005, 4, RACK)
    wheels = (len(mesh.vertices), len(mesh.faces))
    for k, a in enumerate(body.axle):
        for side in (-1, 1):
            _wheel(mesh, body, a, side * body.track[k] / 2, side, q, kit)
    wheels_end = (len(mesh.vertices), len(mesh.faces))
    if q["detail"] >= 1:
        _interior(mesh, body, q, kit)
    if body.p["roof_rack"] and q["detail"] >= 1:
        _rack(mesh, body, q)
    if kit.F:
        _burnt_slots(mesh, kit, ((0, wheels[1]), (wheels_end[1], len(mesh.faces))))
    end = (len(mesh.vertices), len(mesh.faces))
    for (v0, f0), (v1, f1) in (((0, 0), wheels), (wheels_end, end)):
        kits.transform_range(mesh, (v0, v1), (f0, f1), kit.T)
    return mesh


def _kit_levels(body, kit, q):
    """The section levels the kit adds (fractions, as _section_levels): each cut hole's square's top and bottom on
    its flank, and the struck corner's tear's floor."""
    out = []
    for h in _cut_holes(kit, q):
        y1 = body.z_bot(h.s) + TUCK_H_M
        yc = body.z_c(h.s)
        r = h.patch_radius()
        out += [(h.y - r - y1) / (yc - y1), (h.y + r - y1) / (yc - y1)]
    if kit.notch is not None:
        out.append(NOTCH_LEVEL)
    return out


def _cut_holes(kit, q, side=None):
    """The holes cut in the mesh at this LOD: every one of 30 mm or more at LOD0, those of 0.08 m or more at LOD1
    (design decision 8), none further."""
    if q["detail"] < 1:
        return []
    limit = kits.CUT_MIN_M if q["thick"] else kits.LARGE_MIN_M
    return [h for h in kit.holes if h.cut and h.d >= limit and (side is None or h.side == side)]


# --- the body side ------------------------------------------------------------------------------------------------

def _s_rows(body, lo, hi, step, extra=()):
    """Stations from lo to hi at most step apart, through the stations, the door lines and denser over the arches."""
    g = DOOR_GAP_M / 2
    keys = [s for _, (s, _) in zip(STATIONS, body.side)] + [d + e for d in body.doors for e in (-g, g)]
    rows = loft.samples(lo, hi, step, list(extra) + keys)
    fine = []
    for a in body.axle:
        fine += loft.samples(a - body.r_arch, a + body.r_arch, min(step, 0.03))
    return sorted({round(s, 9) for s in rows + [f for f in fine if lo <= f <= hi]})


def _section_levels(body, q, extra=()):
    """The section's vertical fractions (from the tuck's top to the crease), with the door gap's rows and the
    kit's (holes' squares, the tear's floor)."""
    y1 = body.z_bot(body.doors[0]) + TUCK_H_M
    yc = body.z_c(body.doors[0])
    g = DOOR_GAP_M / 2
    f_lo, f_hi = (body.sill - g - y1) / (yc - y1), (body.sill + g - y1) / (yc - y1)
    base = [0.0, f_lo, f_hi, 1.0] + ([0.3, 0.55, 0.8] if q["thick"] else [0.5]) + list(extra)
    return sorted(set(round(f, 9) for f in base)), f_lo, f_hi


def _section(body, s, q, levels):
    """[(w, y, v)] up the right half-section at s from the tuck's foot over the roll to its top, v the UV's V."""
    y0, yc = body.z_bot(s), body.z_c(s)
    y1 = y0 + TUCK_H_M
    hw = body.hw(s)
    pts = [(hw - TUCK_W_M, y0, y0)]
    for f in levels:
        y = y1 + f * (yc - y1)
        pts.append((hw, y, y))
    for k in range(1, q["roll"] + 1):
        t = math.pi / 2 * k / q["roll"]
        pts.append((hw - ROLL_R_M + ROLL_R_M * math.cos(t), yc + ROLL_R_M * math.sin(t), yc + ROLL_R_M * t))
    arch = body.arch(s)
    if arch is not None:
        top = arch[1]
        pts = [(body.lateral(s, top), top, top) if y < top else (w, y, v) for w, y, v in pts]
    return pts


def _body_side(mesh, body, side, q, kit, extra_levels, snags):
    """One side's body panel and its two doors, lofted through the sections; on the burst side the kit's cut holes
    (each a square of the sheet replaced by its patch, kits.emit_patch) and the struck corner's lamp tear."""
    holes = _cut_holes(kit, q, side)
    notch = kit.notch if kit.notch is not None and kit.notch["side"] == side and q["detail"] >= 1 else None
    extra_rows = [h.s + e * h.patch_radius() for h in holes for e in (-1, 1)]
    if notch:
        extra_rows.append(notch["s1"])
    if kit.open:  # the wing's roll steps down to the door's at the cowl in a row, not over the next row's spacing
        extra_rows.append(body.s_cowl + COWL_STEP_M)
    levels, f_lo, f_hi = _section_levels(body, q, extra_levels)
    rows = _s_rows(body, body.s_front, body.s_rear, q["step"], extra_rows)
    uv = body.uv("side", side)
    grid, uvs = [], []
    for s in rows:
        sec = _section(body, s, q, levels)
        grid.append([body.P(s, side * w, y) for w, y, _ in sec])
        uvs.append([uv(body.x(s), v) for _, _, v in sec])
    j_lo = 1 + levels.index(round(f_lo, 9))
    j_hi = 1 + levels.index(round(f_hi, 9))
    d0, d1, d3 = body.doors
    g = DOOR_GAP_M / 2
    mask = [[not (d0 - g - 1e-9 <= rows[i] and rows[i + 1] <= d3 + g + 1e-9 and j >= j_lo)
             for j in range(len(grid[0]) - 1)] for i in range(len(rows) - 1)]
    outward = _side_outward(body, side)
    t = GAUGE_M if q["thick"] else 0.0
    if notch:
        _notch_side(body, q, kit, side, rows, levels, grid, mask, snags)
    sheets = [(grid, uvs, mask, 0, 0)]
    for a, b in ((d0 + g, d1 - g), (d1 + g, d3 - g)):  # the doors
        i0 = next(i for i, s in enumerate(rows) if s >= a - 1e-9)
        door_rows = [s for s in rows if a - 1e-9 <= s <= b + 1e-9]
        dg, du = [], []
        for s in door_rows:
            sec = _section(body, s, q, levels)[j_hi:]
            dg.append([body.P(s, side * w, y) for w, y, _ in sec])
            du.append([uv(body.x(s), v) for _, _, v in sec])
        sheets.append((dg, du, [[True] * (len(dg[0]) - 1) for _ in range(len(dg) - 1)], i0, j_hi))
        if q["detail"] >= 2:  # the handle near the door's rear edge, under the crease
            yc = body.z_c(b)
            _box(mesh, body.P(b - 0.09, side * (body.hw(b) + 0.003), yc - 0.045), (0.12, 0.025, HANDLE_PROUD_M), CHROME)
    surf = _flank_surface(body, side)
    flank_uv = lambda s, y: uv(body.x(s), y)  # noqa: E731
    for sheet_grid, sheet_uvs, sheet_mask, i0, j0 in sheets:
        seams, patches = set(), []
        for h in holes:
            r = h.patch_radius()
            lo, hi = h.s - r - 1e-9, h.s + r + 1e-9
            y1, yc = body.z_bot(h.s) + TUCK_H_M, body.z_c(h.s)
            f0, f1 = (h.y - r - y1) / (yc - y1) - 1e-7, (h.y + r - y1) / (yc - y1) + 1e-7
            quads = {(i, j) for i in range(len(sheet_grid) - 1) for j in range(len(sheet_grid[0]) - 1)
                     if lo <= rows[i0 + i] and rows[i0 + i + 1] <= hi and sheet_mask[i][j]
                     and 1 <= j + j0 <= len(levels) - 1 and f0 <= levels[j + j0 - 1] and levels[j + j0] <= f1}
            if not quads:
                continue
            for i, j in quads:
                sheet_mask[i][j] = False
            seams |= quads
            patches.append((h, quads))
        loft.sheet(mesh, sheet_grid, sheet_uvs, PAINT, outward, t, sheet_mask, seams=seams)
        for h, quads in patches:
            loop = kits.boundary_loop(quads)
            outer = [(sheet_grid[i][j], (rows[i0 + i], sheet_grid[i][j][1]), sheet_uvs[i][j]) for i, j in loop]
            hole_snags = kits.emit_patch(mesh, h, outer, surf, flank_uv, t, {"paint": PAINT, "torn": RUST},
                                         PHYSICS[PAINT], rim_points=48 if q["thick"] else 16, petals=q["thick"])
            if snags is not None:
                snags += hole_snags
    if q["thick"]:  # the uncut holes' dark openings; every hole's halo is the damage mask's (_damage_mask)
        for h in kit.holes:
            if h.side == side and not h.cut:
                kits.emit_dark(mesh, h, surf, flank_uv, UNDER)


def _flank_surface(body, side):
    """(point, outward unit normal) of the flank at station s and height y (the vertical part of the section)."""
    def surf(s, y):
        p = body.P(s, side * body.hw(s), y)
        e = 1e-4
        slope = (body.hw(s + e) - body.hw(s - e)) / (2 * e)
        n = normalize((-side * slope, 0.0, float(side)))
        return p, n
    return surf


def _notch_side(body, q, kit, side, rows, levels, grid, mask, snags):
    """The struck corner's lamp tear on the wing: the quads from the front face back to the tear's row and from its
    floor level up over the roll are cut (with their edges), and the cut's points jittered along the sheet so the
    edge is torn, not machined. Its torn edge is an `edge` snag."""
    n = kit.notch
    j_n = 1 + levels.index(round(NOTCH_LEVEL, 9))
    i_end = next(i for i, s in enumerate(rows) if s >= n["s1"] - 1e-9)
    for i in range(i_end):
        for j in range(j_n, len(grid[0]) - 1):
            mask[i][j] = False
    rng = kits.rng_for(kit.p, f"notch {q['step']}")
    s1 = rows[i_end]
    for j in range(j_n + 1, len(grid[0])):  # the cut up the wing at the tear's row: jittered along s
        ds = rng.uniform(-0.012, 0.012)
        w, y, _ = _section(body, s1 + ds, q, levels)[j]
        grid[i_end][j] = body.P(s1 + ds, side * w, y)
    for i in range(1, i_end):  # the cut along the floor: jittered in height
        s = rows[i]
        y1 = body.z_bot(s) + TUCK_H_M
        y = y1 + NOTCH_LEVEL * (body.z_c(s) - y1) + rng.uniform(-0.012, 0.012)
        grid[i][j_n] = body.P(s, side * body.hw(s), y)
    if snags is not None:
        edge = [grid[i][j_n] for i in range(i_end + 1)] + [grid[i_end][j] for j in range(j_n + 1, len(grid[0]))]
        snags.append({"kind": "edge", "points": [list(p) for p in edge], "radius_m": kits.EDGE_SNAG_R_M,
                      "material": PHYSICS[PAINT]})


def _side_outward(body, side):
    def outward(p):
        return (0.0, p[1] - 0.6, p[2] if p[2] * side > 0 else side * 0.1)
    return outward


def _wheelhouses(mesh, body, side, q):
    n = 24 if q["thick"] else (12 if q["detail"] else 6)
    for k, a in enumerate(body.axle):
        w_in = body.w_in[k]
        grid, uvs, wall, wall_uv = [], [], [], []
        for i in range(n + 1):
            t = math.pi * i / n
            s = a - body.r_arch * math.cos(t)
            y = body.z_wc + body.r_arch * math.sin(t)
            w_out = body.lateral(s, y)
            grid.append([body.P(s, side * w_in, y), body.P(s, side * w_out, y)])
            uvs.append([(body.x(s), w_in), (body.x(s), w_out)])
            wall.append([body.P(a, side * w_in, body.z_wc), body.P(s, side * w_in, y)])
            wall_uv.append([(body.x(a), body.z_wc), (body.x(s), y)])
        loft.surface(mesh, grid, uvs, UNDER, lambda p, a=a: (body.x(a) - p[0], body.z_wc - p[1], 0.0))
        loft.surface(mesh, wall, wall_uv, UNDER, lambda p: (0.0, 0.0, float(side)))


# --- the tops and the ends ------------------------------------------------------------------------------------------

U_LEVELS = (0.0, 0.3, 0.6, 0.78, 0.87, 0.93, 0.97, 1.0)


def _u_levels(q, extra=()):
    base = U_LEVELS if q["thick"] else (U_LEVELS[::2] + (1.0,))
    half = sorted(set(base) | set(extra))
    return [-u for u in reversed(half) if u > 0] + half


def _top_sheet(mesh, body, s0, s1, u_max, q, flap=None):
    """A crowned top from s0 to s1 across +-u_max; with the bonnet's `flap`, its torn corner left out: from the
    flap's foot back each row runs from the tear to the far edge, through the same column fractions."""
    uv = body.uv("top")
    rows = loft.samples(s0, s1, q["step"], [flap.s_B] if flap else ())
    grid = []
    for s in rows:
        lo, hi = -u_max, u_max
        if flap and s >= flap.s_B - 1e-9:
            if flap.side < 0:
                lo = flap.u_tear(s)
            else:
                hi = flap.u_tear(s)
        grid.append([body.top_point(s, lo + (hi - lo) * (u + 1) / 2) for u in _u_levels(q)])
    uvs = [[uv(p[0], p[2]) for p in row] for row in grid]
    loft.sheet(mesh, grid, uvs, PAINT, lambda p: (0.0, 1.0, 0.0), GAUGE_M if q["thick"] else 0.0)


FLAP_COLUMNS = (0.0, 0.03, 0.07, 0.13, 0.22, 0.4, 0.7, 1.0)  # the flap's columns from its side edge to the tear,
                                                             # dense where the crown rolls over the edge


def _tops(mesh, body, q, kit, snags):
    g = DOOR_GAP_M / 2
    gu = DOOR_GAP_M / body.w_t(body.s_front)
    flap = kit.flap if q["detail"] >= 1 else None
    _top_sheet(mesh, body, body.s_front, body.s_scuttle - g, 1 - gu, q, flap)      # bonnet
    _top_sheet(mesh, body, body.s_scuttle + g, body.s_cowl, 1.0, q)              # scuttle
    _top_sheet(mesh, body, body.s_rs_base, body.s_boot - g, 1.0, q)              # behind the rear screen
    _top_sheet(mesh, body, body.s_boot + g, body.s_rear, 1 - gu, q)              # boot lid
    if flap:
        _flap(mesh, body, q, flap, snags)
        _deck(mesh, body, kit)


def _flap(mesh, body, q, flap, snags):
    """The folded corner: the bonnet's sheet of the corner (its crown as it was), turned about the hinge."""
    uv = body.uv("top")
    rot = flap.rigid()
    rows = loft.samples(flap.s_B, flap.s_b, q["step"])
    flat = [[flap.flat_point(s, f) for f in FLAP_COLUMNS] for s in rows]
    grid = [[rot.point(p) for p in row] for row in flat]
    uvs = [[uv(p[0], p[2]) for p in row] for row in flat]
    normal = rot.vector((0.0, 1.0, 0.0))
    loft.sheet(mesh, grid, uvs, PAINT, lambda p: normal, GAUGE_M if q["thick"] else 0.0, edge_slot=RUST)
    if snags is not None:  # the tear (A-B) and the flap's free edges
        tear = [row[-1] for row in grid]
        side = [row[0] for row in grid]
        for edge in (tear, side):
            snags.append({"kind": "edge", "points": [list(p) for p in edge], "radius_m": kits.EDGE_SNAG_R_M,
                          "material": PHYSICS[PAINT]})


def _deck_box(body, kit):
    """(s0, s1, w0, w1, top, bottom) of the engine bay's contents under the torn corner (DECK_DROP_M under the
    bonnet's mean line, down to the wedge's foot), w signed: from the side edge to 1 cm past the tear's far end on
    the rear edge, and from 1 cm ahead of the corner's foot (its block overlaps the wedges beside it)."""
    flap = kit.flap
    w_edge = body.w_t(flap.s_b) * flap.u_C
    w_far = body.w_t(flap.s_b) * flap.u_A - flap.side * 0.01
    s0, s1 = flap.s_B - 0.01, body.s_cowl
    top = min(_top_mean(body, s0), _top_mean(body, s1)) - DECK_DROP_M
    bottom = max(_top_mean(body, s0), _top_mean(body, s1)) - LID_T_M - 0.2 - 0.015
    return s0, s1, min(w_edge, w_far), max(w_edge, w_far), top, bottom


def _deck(mesh, body, kit):
    """The engine bay's contents under the torn corner as one dark block (assumed: the class's master cylinder,
    heater box and battery under the corner; M's engine bay under the flap, #50545b); its box is its collision."""
    s0, s1, w0, w1, top, bottom = _deck_box(body, kit)
    _box(mesh, body.P((s0 + s1) / 2, (w0 + w1) / 2, (top + bottom) / 2), (s1 - s0, top - bottom, w1 - w0), UNDER)
    # the pocket over it closed where the bay stays full (its boxes' faces): up from the block to the sheet's
    # underside at the corner's foot and along A's line under the bonnet left whole, and the inner wing under the
    # roll's top on the burst side
    flap, side = kit.flap, kit.flap.side
    w_a = body.w_t(flap.s_b) * flap.u_A

    def under(s, w):
        return body.top_point(s, w / body.w_t(s))[1] - GAUGE_M

    def wall(pts, normal):
        mesh.face([mesh.vertex(p) for p in pts], normal, UNDER)
    n = 8
    sB = flap.s_B
    ws = [side * body.w_t(sB) + (w_a - side * body.w_t(sB)) * k / n for k in range(n + 1)]
    for wa, wb in zip(ws, ws[1:]):
        wall([body.P(sB, wa, top), body.P(sB, wb, top), body.P(sB, wb, under(sB, wb)), body.P(sB, wa, under(sB, wa))],
             (1.0, 0.0, 0.0))
    ss = [sB + (body.s_cowl - sB) * k / n for k in range(n + 1)]
    for sa, sb in zip(ss, ss[1:]):
        wall([body.P(sa, w_a, top), body.P(sb, w_a, top), body.P(sb, w_a, under(sb, w_a)), body.P(sa, w_a, under(sa, w_a))],
             (0.0, 0.0, float(side)))
        wa, wb = side * body.w_t(sa), side * body.w_t(sb)
        wall([body.P(sa, wa, top), body.P(sb, wb, top), body.P(sb, wb, body.z_sh(sb)), body.P(sa, wa, body.z_sh(sa))],
             (0.0, 0.0, -float(side)))


def _end_outline(body, s, q, extra_levels=()):
    """The closed outline of the body's section at s (right half up, over the top, left half down)."""
    levels, _, _ = _section_levels(body, q, extra_levels)
    right = [(w, y) for w, y, _ in _section(body, s, q, levels)]
    top = [(u * body.w_t(s), body.z_sh(s) + body.crown(s) * (1 - abs(u) ** CROWN_POWER))
           for u in reversed(_u_levels(q)) if abs(u) < 1.0]
    left = [(-w, y) for w, y in reversed(right)]
    return right + top + left


def _ends(mesh, body, q, kit, extra_levels, snags):
    for s, sign, group in ((body.s_front, -1.0, "front"), (body.s_rear, 1.0, "rear")):
        outline = _end_outline(body, s, q, extra_levels)
        cut = []
        if sign < 0 and kit.notch is not None and q["detail"] >= 1:
            outline, cut = _notch_outline(body, q, kit, outline)
        uv = body.uv(group)
        outer = [mesh.vertex(body.P(s, w, y)) for w, y in outline]
        mesh.face(outer, (sign, 0.0, 0.0), PAINT, [uv(w, y) for w, y in outline])
        if q["thick"]:
            inner = [mesh.vertex(body.P(s - sign * GAUGE_M, w, y)) for w, y in outline]
            mesh.face(inner, (-sign, 0.0, 0.0), PAINT, [uv(w, y) for w, y in outline])
            for (wa, ya), (wb, yb) in zip(cut, cut[1:]):  # the tear's edge strip, facing into the notch
                ids = [mesh.vertex(body.P(s, wa, ya)), mesh.vertex(body.P(s, wb, yb)),
                       mesh.vertex(body.P(s + GAUGE_M, wb, yb)), mesh.vertex(body.P(s + GAUGE_M, wa, ya))]
                n = normalize((0.0, wb - wa, -(yb - ya)))
                if n[1] * 1.0 + n[2] * kit.notch["side"] < 0:
                    n = scale(n, -1)
                mesh.face(ids, n, RUST, [uv(wa, ya), uv(wb, yb), uv(wb, yb - GAUGE_M), uv(wa, ya - GAUGE_M)])
        if cut and snags is not None:
            snags.append({"kind": "edge", "points": [list(body.P(s, w, y)) for w, y in cut],
                          "radius_m": kits.EDGE_SNAG_R_M, "material": PHYSICS[PAINT]})
        if cut:
            _notch_pocket(mesh, body, q, kit)


def _notch_outline(body, q, kit, outline):
    """The front face's outline with the struck corner's tear cut from it: the points in the notch (outboard of its
    inner edge and over its floor) replaced by the torn edge, down from the top at the inner edge and out along the
    floor to the flank. Returns (outline, the cut's polyline from the top to the flank)."""
    n, s = kit.notch, body.s_front
    side, w_in = n["side"], n["w_in"]
    y_n = kit.notch_y(s)
    inside = [side * w > w_in + 1e-9 and y > y_n + 1e-9 for w, y in outline]
    k = len(outline)
    start = next(i for i in range(k) if inside[i] and not inside[i - 1])  # the run's first point (cyclic)
    end = start
    while inside[(end + 1) % k]:
        end = (end + 1) % k
    before, after = outline[start - 1], outline[(end + 1) % k]
    rng = kits.rng_for(kit.p, f"notch face {q['step']}")
    top = max(before, after, key=lambda p: p[1])
    flank = min(before, after, key=lambda p: p[1])
    path = []
    steps = 6
    for i in range(1, steps):  # down the inner edge
        y = top[1] + (y_n - top[1]) * i / steps
        path.append((side * (w_in + rng.uniform(-0.01, 0.01)), y))
    path.append((side * w_in, y_n))
    for i in range(1, 4):  # out along the floor
        w = w_in + (abs(flank[0]) - w_in) * i / 4
        path.append((side * w, y_n + rng.uniform(-0.01, 0.01)))
    if before is flank:
        path = path[::-1]
    run = [i % k for i in range(start, start + ((end - start) % k) + 1)]
    kept = [p for i, p in enumerate(outline) if i not in run]
    at = kept.index(before) + 1
    new = kept[:at] + path + kept[at:]
    cut = [top] + (path if before is top else path[::-1]) + [flank]
    return new, cut


def _notch_pocket(mesh, body, q, kit):
    """The pocket behind the struck corner's tear (dark, the lamp's housing and the inner wing): its back wall at the
    tear's row, its inner wall at the lamp's inner edge and its floor, each facing into the pocket."""
    n = kit.notch
    side, w_in, s0, s1 = n["side"], n["w_in"], n["s0"], n["s1"]
    levels, _, _ = _section_levels(body, q, [NOTCH_LEVEL])
    j_n = 1 + levels.index(round(NOTCH_LEVEL, 9))
    sec = _section(body, s1, q, levels)[j_n:]
    y_top = body.top_point(s1, w_in / body.w_t(s1))[1] - GAUGE_M
    back = [(w_in, kit.notch_y(s1))] + [(w, y) for w, y, _ in sec] + [(w_in, y_top)]
    ids = [mesh.vertex(body.P(s1, side * w, y)) for w, y in back]
    mesh.face(ids, (-1.0, 0.0, 0.0), UNDER, [(side * w, y) for w, y in back])
    y0f, y1f = kit.notch_y(s0), kit.notch_y(s1)
    y_top0 = body.top_point(s0, w_in / body.w_t(s0))[1] - GAUGE_M
    wall = [body.P(s0, side * w_in, y0f), body.P(s1, side * w_in, y1f), body.P(s1, side * w_in, y_top),
            body.P(s0, side * w_in, y_top0)]
    mesh.face([mesh.vertex(p) for p in wall], (0.0, 0.0, float(side)), UNDER)
    floor = [body.P(s0, side * w_in, y0f), body.P(s1, side * w_in, y1f), body.P(s1, side * body.hw(s1), y1f),
             body.P(s0, side * body.hw(s0), y0f)]
    mesh.face([mesh.vertex(p) for p in floor], (0.0, 1.0, 0.0), UNDER)


def _underbody(mesh, body, q):
    """The floor pan, its top the cabin floor, open over the wheels outboard of the wheelhouses' inner walls."""
    rows = _s_rows(body, body.s_front, body.s_rear, max(q["step"], 0.1))
    wf, wr = body.w_in
    inner = sorted({0.0, wf, wr, -wf, -wr})
    grid, uvs, mask = [], [], []
    for s in rows:
        wb = body.hw(s) - TUCK_W_M
        ws = [-wb] + inner + [wb]
        grid.append([body.P(s, w, body.z_bot(s)) for w in ws])
        uvs.append([(body.x(s), w) for w in ws])
    for i in range(len(rows) - 1):
        sm = (rows[i] + rows[i + 1]) / 2
        ws = [p[2] for p in grid[i]]
        row = []
        for j in range(len(ws) - 1):
            lo = min(abs(ws[j]), abs(ws[j + 1])) if ws[j] * ws[j + 1] > 0 else 0.0
            row.append(not any(abs(sm - a) < body.r_arch and lo >= body.w_in[k] - 1e-9
                               for k, a in enumerate(body.axle)))
        mask.append(row)
    loft.sheet(mesh, grid, uvs, UNDER, lambda p: (0.0, -1.0, 0.0), GAUGE_M if q["thick"] else 0.0, mask)

# --- the greenhouse ------------------------------------------------------------------------------------------------

def _windows(body):
    """[(s0, s1)] of the band's openings, front to rear."""
    pre = body.preset
    ks = body.ks
    if body.p["vent_panes"]:
        spans = [pre["vent"], pre["front_window"]]
    else:
        spans = [(pre["vent"][0], pre["front_window"][1])]
    spans += [pre["rear_window"], pre["quarter"]]
    return [(a * ks, b * ks) for a, b in spans]


def _window_pairs(body):
    """Per window of _windows, its glass_side pair: 0 the front door's (vent and window), 1 the rear door's."""
    return [0, 0, 1, 1] if body.p["vent_panes"] else [0, 1, 1]


def _band(mesh, body, side, q, kit):
    windows = _windows(body)
    gone = [kit.glass[side, pair] == "gone" for pair in _window_pairs(body)]
    v0, v1 = WINDOW_V
    fv = FRAME_M / body.H_band
    extra = [e for a, b in windows for e in (a - FRAME_M, a, b, b + FRAME_M)]
    extra += [body.s_a_top, body.s_drip_end, body.s_c_top]
    rows = loft.samples(body.s_cowl, body.s_rs_base, q["step"], extra)
    vs = [0.0, v0 - fv, v0, v1, v1 + fv, 1.0] if q["detail"] else [0.0, v0, v1, 1.0]
    grid = [[body.band_point(s, v, side) for v in vs] for s in rows]
    uv = body.uv("band", side)
    uvs = [[uv(p[0], p[1]) for p in row] for row in grid]
    mask, slots = [], []
    for i in range(len(rows) - 1):
        sm = (rows[i] + rows[i + 1]) / 2
        mrow, srow = [], []
        for j in range(len(vs) - 1):
            vm = (vs[j] + vs[j + 1]) / 2
            inside = any(a < sm < b for a, b in windows) and v0 < vm < v1
            frame = any(a - FRAME_M < sm < b + FRAME_M for a, b in windows) and v0 - fv < vm < v1 + fv
            if q["detail"] == 0:  # the hull: an empty window is a dark opening, an intact one its glass
                empty = inside and any(a < sm < b and g for (a, b), g in zip(windows, gone))
                mrow.append(True)
                srow.append(UNDER if empty else GLASS_HULL if inside else PAINT)
            else:
                mrow.append(not inside)
                srow.append(CHROME if frame and not inside else PAINT)
        mask.append(mrow)
        slots.append(srow)
    outward = lambda p: (0.0, 0.3, float(side))  # noqa: E731
    loft.sheet(mesh, grid, uvs, PAINT, outward, BAND_T_M if q["thick"] else 0.0, mask, slots=slots)


def _cap_glass(body):
    """((s0, s1, t_max), ...) of the windscreen's and the rear screen's glass in the cap (t the section parameter)."""
    m_ws, t_ws = body.preset["windscreen_glass"]
    m_rs, t_rs = body.preset["rear_glass"]
    return ((body.s_cowl + m_ws, body.s_ws_top - m_ws, t_ws), (body.s_rs_top + m_rs, body.s_rs_base - m_rs, t_rs))


CAP_T = (0.0, 0.25, 0.5, 0.65, CAP_FLAT, 0.8, 0.85, 0.9, 0.95, 1.0)


def _cap_levels(q, extra=()):
    base = CAP_T if q["thick"] else (0.0, 0.5, CAP_FLAT, 0.875, 1.0)
    half = sorted(set(base) | set(extra))
    return [-u for u in reversed(half) if u > 0] + half


def _cap(mesh, body, q, kit):
    panes = _cap_glass(body)
    gone = (kit.windscreen == "gone", kit.rear_screen == "gone")
    ft = 0.03
    extra_s = [e for a, b, _ in panes for e in (a - FRAME_M, a, b, b + FRAME_M)]
    extra_s += [body.s_ws_top, body.s_rs_top, body.s_drip_end, body.st["roof_front"], body.st["roof_rear"]]
    if body.sag_m > 0:
        extra_s.append(body.s_crease)
    rows = loft.samples(body.s_cowl, body.s_rs_base, q["step"], extra_s)
    us = _cap_levels(q, [u for _, _, um in panes for u in ((um, um + ft) if q["detail"] else (um,))])
    grid = [[body.cap_point(s, u) for u in us] for s in rows]
    uv = body.uv("top")
    uvs = [[uv(p[0], p[2]) for p in row] for row in grid]
    mask, slots = [], []
    for i in range(len(rows) - 1):
        sm = (rows[i] + rows[i + 1]) / 2
        mrow, srow = [], []
        for j in range(len(us) - 1):
            um = abs((us[j] + us[j + 1]) / 2)
            inside = any(a < sm < b and um < u for a, b, u in panes)
            frame = any(a - FRAME_M < sm < b + FRAME_M and um < u + ft for a, b, u in panes)
            if q["detail"] == 0:  # the hull: an empty screen is a dark opening
                empty = any(a < sm < b and um < u and g for (a, b, u), g in zip(panes, gone))
                mrow.append(True)
                srow.append(UNDER if empty else GLASS_HULL if inside else PAINT)
            else:
                mrow.append(not inside)
                srow.append(CHROME if frame and not inside else PAINT)
        mask.append(mrow)
        slots.append(srow)
    loft.sheet(mesh, grid, uvs, PAINT, lambda p: (0.0, 1.0, 0.0), GAUGE_M if q["thick"] else 0.0, mask, slots=slots)


def _glass(mesh, body, q, kit, snags):
    """The panes that remain, GLASS_T_M slabs GLASS_INSET_M behind their openings; from LOD2 on they are the
    hull's. The kit's gone panes are left out; the crazed windscreen (crazed_holed) is its own slot, bulged in round
    its hole, which is cut from the slab (the hole's patch, its torn rim an `edge` snag)."""
    if q["detail"] == 0:
        return
    thick = GLASS_T_M if q["thick"] else 0.0
    v0, v1 = WINDOW_V
    for side in (-1, 1):
        for (a, b), pair in zip(_windows(body), _window_pairs(body)):
            if kit.glass[side, pair] == "gone":
                continue
            rows = loft.samples(a, b, q["step"])
            grid = [[_inset(body.band_point(s, v, side), _band_normal(body, s, v, side)) for v in (v0, v1)]
                    for s in rows]
            loft.sheet(mesh, grid, [[(p[0], p[1]) for p in r] for r in grid], GLASS,
                       lambda p, side=side: (0.0, 0.3, float(side)), thick)
    for k, (a, b, um) in enumerate(_cap_glass(body)):
        state = kit.windscreen if k == 0 else kit.rear_screen
        if state == "gone":
            continue
        if k == 0 and state == "crazed_holed":
            _crazed_pane(mesh, body, q, kit, a, b, um, thick, snags)
            continue
        rows = loft.samples(a, b, q["step"])
        us = [u for u in _cap_levels(q, [um]) if abs(u) <= um + 1e-9]
        grid = [[_inset(body.cap_point(s, u), _cap_normal(body, s, u)) for u in us] for s in rows]
        loft.sheet(mesh, grid, [[(p[0], p[2]) for p in r] for r in grid], GLASS, lambda p: (0.0, 1.0, 0.0), thick)


def _pane_surface(body, kit):
    """(point, outward unit normal) on the crazed windscreen at (s ws_k, w): the cap's point at lateral w, set in
    by the glass's inset and bulged inward round the hole."""
    hole = kit.ws_hole

    def surf(a, w):
        s = a / kit.ws_k
        u = math.copysign(CAP_FLAT * min(abs(w) / _flat_reach(body, s), 1.0), w)
        n = _cap_normal(body, s, u)
        p = _inset(body.cap_point(s, u), n)
        d = math.hypot(a - hole.s, w - hole.y)
        bulge = WS_BULGE_M * max(0.0, 1 - (d / WS_BULGE_R_M) ** 2) ** 2
        return sub(p, scale(n, bulge)), n
    return surf


def _flat_reach(body, s):
    """The cap's flat crown's half-width at s (where its edge roll begins)."""
    ze, we = body.z_e(s), body.w_e(s)
    return we - min(EDGE_R_M, max(0.0, body.z_top(s) - ze))


def _crazed_pane(mesh, body, q, kit, s0, s1, um, thick, snags):
    """The crazed windscreen: the pane's grid with rows and columns on its hole's rectangle, those quads replaced by
    the hole's patch, every point bulged by _pane_surface; slot WINDSCREEN (the transparent glass with the crack
    mask of _textures)."""
    hole = kit.ws_hole
    ha, hb = hole.patch_half()
    sc = hole.s / kit.ws_k
    s_lo, s_hi = sc - ha / kit.ws_k, sc + ha / kit.ws_k
    rows = loft.samples(s0, s1, q["step"], [s_lo, s_hi])
    reach = _flat_reach(body, sc)
    u_lo = CAP_FLAT * (hole.y - hb) / reach
    u_hi = CAP_FLAT * (hole.y + hb) / reach
    us = sorted(set(u for u in _cap_levels(q, [um]) if abs(u) <= um + 1e-9) | {round(u_lo, 9), round(u_hi, 9)})
    surf = _pane_surface(body, kit)
    grid, uvs, params = [], [], []
    for s in rows:
        row, row_uv, row_p = [], [], []
        for u in us:
            w = math.copysign(_flat_reach(body, s) * abs(u) / CAP_FLAT, u)
            p, _ = surf(s * kit.ws_k, w)
            row.append(p)
            flat = body.cap_point(s, u)
            row_uv.append((flat[0], flat[2]))
            row_p.append((s * kit.ws_k, w))
        grid.append(row)
        uvs.append(row_uv)
        params.append(row_p)
    quads = {(i, j) for i in range(len(rows) - 1) for j in range(len(us) - 1)
             if s_lo - 1e-9 <= rows[i] and rows[i + 1] <= s_hi + 1e-9 and u_lo - 1e-9 <= us[j]
             and us[j + 1] <= u_hi + 1e-9}
    mask = [[(i, j) not in quads for j in range(len(us) - 1)] for i in range(len(rows) - 1)]
    loft.sheet(mesh, grid, uvs, WINDSCREEN, lambda p: (0.0, 1.0, 0.0), thick, mask, seams=quads)
    loop = kits.boundary_loop(quads)
    outer = [(grid[i][j], params[i][j], uvs[i][j]) for i, j in loop]

    def uv(a, w):
        p = body.cap_point_w(a / kit.ws_k, w)
        return (p[0], p[2])
    rim = kits.emit_patch(mesh, hole, outer, surf, uv, thick, {"paint": WINDSCREEN, "torn": WINDSCREEN},
                          PHYSICS[WINDSCREEN], rim_points=40 if q["thick"] else 16, petals=False)
    if snags is not None:
        snags += rim

def _inset(p, n):
    return sub(p, scale(n, GLASS_INSET_M))


def _band_normal(body, s, v, side):
    e = 1e-4
    ds = sub(body.band_point(s + e, v, side), body.band_point(s - e, v, side))
    dv = sub(body.band_point(s, min(1, v + e), side), body.band_point(s, max(0, v - e), side))
    n = normalize(cross(ds, dv))
    return n if n[2] * side > 0 else scale(n, -1)


def _cap_normal(body, s, u):
    e = 1e-4
    ds = sub(body.cap_point(s + e, u), body.cap_point(s - e, u))
    du = sub(body.cap_point(s, u + e), body.cap_point(s, u - e))
    n = normalize(cross(ds, du))
    return n if n[1] > 0 else scale(n, -1)


# --- small parts ---------------------------------------------------------------------------------------------------

def _box(mesh, centre, size, slot, axes=((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)), uvs=None):
    """An oriented box: centre, full sizes along its axes; `uvs` a function of (point, normal) or uv_upright."""
    h = [s / 2 for s in size]
    corner = {}
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                p = add(centre, add(scale(axes[0], sx * h[0]), add(scale(axes[1], sy * h[1]), scale(axes[2], sz * h[2]))))
                corner[sx, sy, sz] = (mesh.vertex(p), p)
    for axis in range(3):
        for sign in (-1, 1):
            keys = [c for c in corner if c[axis] == sign]
            o = [(0, 1, 2)[(axis + 1) % 3], (0, 1, 2)[(axis + 2) % 3]]
            keys.sort(key=lambda c: math.atan2(c[o[1]], c[o[0]]))
            n = scale(axes[axis], sign)
            ids = [corner[c][0] for c in keys]
            mesh.face(ids, n, slot, [uvs(corner[c][1], n) for c in keys] if uvs else None)


def _tube(mesh, points, radius, sides, slot, closed=False, caps=True):
    """A round tube through `points` (a ring per point in the plane bisecting its neighbours)."""
    n = len(points)
    frames = []
    for i in range(n):
        a = points[i - 1] if (i > 0 or closed) else points[i]
        b = points[(i + 1) % n] if (i < n - 1 or closed) else points[i]
        t = normalize(sub(b, a))
        helper = (0.0, 1.0, 0.0) if abs(t[1]) < 0.9 else (1.0, 0.0, 0.0)
        u = normalize(cross(t, helper))
        v = cross(t, u)
        frames.append((u, v))
    rings = []
    for p, (u, v) in zip(points, frames):
        rings.append([add(p, add(scale(u, radius * math.cos(2 * math.pi * k / sides)),
                                 scale(v, radius * math.sin(2 * math.pi * k / sides)))) for k in range(sides)])
    ids = [[mesh.vertex(q) for q in r] for r in rings]
    count = n if closed else n - 1
    length = 0.0
    for i in range(count):
        j = (i + 1) % n
        seg = norm(sub(points[j], points[i]))
        for k in range(sides):
            k2 = (k + 1) % sides
            quad = [ids[i][k], ids[i][k2], ids[j][k2], ids[j][k]]
            mid = scale(add(add(rings[i][k], rings[i][k2]), add(rings[j][k], rings[j][k2])), 0.25)
            centre = scale(add(points[i], points[j]), 0.5)
            out = normalize(sub(mid, centre))
            uv = [(length, radius * 2 * math.pi * k / sides), (length, radius * 2 * math.pi * (k + 1) / sides),
                  (length + seg, radius * 2 * math.pi * (k + 1) / sides), (length + seg, radius * 2 * math.pi * k / sides)]
            mesh.face(quad, out, slot, uv)
        length += seg
    if caps and not closed:
        for i, sign in ((0, -1), (n - 1, 1)):
            t = normalize(sub(points[min(i + 1, n - 1)], points[max(i - 1, 0)]))
            mesh.face(ids[i], scale(t, sign), slot)


def _revolve(mesh, centre, axis, profile, segments, slot, core=None, facing=1.0, flatten_y=None, uv_scale=1.0,
             fixed_uv=None):
    """A surface of revolution about `axis` through `centre` from profile [(radius, offset along axis)]. Each face
    points away from the ring of `core` (radius, offset) when given (a tyre round its carcass), else along
    facing * axis (a lens, a hubcap, a rim's face). flatten_y clamps the points' y from below (the tyre's patch)."""
    helper = (0.0, 1.0, 0.0) if abs(axis[1]) < 0.9 else (1.0, 0.0, 0.0)
    e1 = normalize(cross(axis, helper))
    e2 = cross(axis, e1)

    def at(r, a, k):
        t = 2 * math.pi * k / segments
        return add(centre, add(scale(axis, a), add(scale(e1, r * math.cos(t)), scale(e2, r * math.sin(t)))))

    rings, ids = [], []
    for r, a in profile:
        ring = [at(r, a, k) for k in range(segments)]
        if flatten_y is not None:
            ring = [(p[0], max(p[1], flatten_y), p[2]) for p in ring]
        rings.append(ring)
        ids.append([mesh.vertex(p) for p in ring] if r > 0 else [mesh.vertex(ring[0])] * segments)
    for i in range(len(profile) - 1):
        (r0, a0), (r1, a1) = profile[i], profile[i + 1]
        for k in range(segments):
            k2 = (k + 1) % segments
            corner = [(i, k), (i, k2), (i + 1, k2), (i + 1, k)]
            quad, pts, uv = [], [], []
            for (ri, kk), (u, v) in zip(corner, ((k, a0 + r0), (k + 1, a0 + r0), (k + 1, a1 + r1), (k, a1 + r1))):
                if ids[ri][kk] not in quad:
                    quad.append(ids[ri][kk])
                    pts.append(rings[ri][kk])
                    uv.append(fixed_uv or (uv_scale * u / segments, v))
            if len(quad) < 3:
                continue
            n = _newell(pts)
            if norm(n) < 1e-14:
                continue
            n = normalize(n)
            mid = scale(add(add(pts[0], pts[1]), pts[-1]), 1 / 3)
            if core is not None:
                ref = sub(mid, at(core[0], core[1], (k + 0.5)))
            else:
                ref = scale(axis, facing)
            if dot(n, ref) < 0:
                n = scale(n, -1)
            mesh.face(quad, n, slot, uv)

def _dedupe(ids):
    out = []
    for i in ids:
        if i not in out:
            out.append(i)
    return out


def _dedupe_uv(ids, uvs):
    seen, out = [], []
    for i, uv in zip(ids, uvs):
        if i not in seen:
            seen.append(i)
            out.append(uv)
    return out


def _newell(pts):
    n = [0.0, 0.0, 0.0]
    for a, b in zip(pts, pts[1:] + pts[:1]):
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    return tuple(n)


def _bumper_path(body, front):
    """The blade's outer face in plan, [(s, w)] from left to right: a shallow curve s = k w^2 across the end, then a
    quarter ellipse wrapping each corner back to the preset's wrap station (derived: the drawing's top view)."""
    bottom, top, depth, k, wrap = body.preset["bumper_front" if front else "bumper_rear"]
    sign = 1.0 if front else -1.0
    tip = (0.0 if front else body.L) + sign * BLADE_SET_M
    w_wrap = body.hw(body.s_front if front else body.s_rear) + 0.03
    w_turn = w_wrap - 0.10
    s_turn = tip + sign * k * w_turn * w_turn
    s_end = tip + sign * wrap
    middle = [(tip + sign * k * w * w, w) for w in (w_turn * i / 6 for i in range(-6, 7))]
    right = [(s_turn + (s_end - s_turn) * (1 - math.cos(a)), w_turn + (w_wrap - w_turn) * math.sin(a))
             for a in (math.pi / 2 * j / 5 for j in range(1, 6))]
    left = [(s, -w) for s, w in reversed(right)]
    return left + middle + right, (bottom, top, depth)

def _bumper_inward(body, path, i, front):
    """The unit (ds, dw) at path point i, perpendicular to the path and toward the car."""
    a, b = path[max(i - 1, 0)], path[min(i + 1, len(path) - 1)]
    d = math.hypot(b[0] - a[0], b[1] - a[1])
    inward = ((b[1] - a[1]) / d, -(b[0] - a[0]) / d)
    centre = (body.s_front + 0.4, 0.0) if front else (body.s_rear - 0.4, 0.0)
    if (centre[0] - path[i][0]) * inward[0] + (centre[1] - path[i][1]) * inward[1] < 0:
        inward = (-inward[0], -inward[1])
    return inward


def _bumpers(mesh, body, q, kit):
    for front in (True, False):
        path, (bottom, top, depth) = _bumper_path(body, front)
        sign = 1.0 if front else -1.0
        if q["detail"] == 0:
            s0 = 0.0 if front else body.L - depth
            _box(mesh, body.P(s0 + depth / 2, 0.0, (bottom + top) / 2), (depth, top - bottom, 2 * path[-1][1]), CHROME)
            continue
        # the blade: a closed section swept along the path, its outer face on the path, depth inward
        sec = [(0.0, bottom + 0.012), (0.0, top - 0.012), (0.012, top), (depth, top), (depth, bottom), (0.012, bottom)]
        grid, uvs = [], []
        for i, (s, w) in enumerate(path):
            inward = _bumper_inward(body, path, i, front)
            row = [body.P(s + inward[0] * d, w + inward[1] * d, y) for d, y in sec]
            grid.append(row + [row[0]])
            uvs.append([(w, y) for _, y in sec] + [(w, sec[0][1])])
        loft.surface(mesh, grid, uvs, CHROME, _bumper_outward(body, front))
        for i, j in ((0, 1), (len(path) - 1, len(path) - 2)):  # end caps, facing along the path past the end
            n = normalize((path[i][0] - path[j][0], 0.0, path[i][1] - path[j][1]))
            mesh.face([mesh.vertex(p) for p in grid[i][:-1]], n, CHROME)
        lateral, o_bottom, o_top = body.preset["overrider"]
        s = (0.0 if front else body.L) + sign * 0.025  # the overriders' faces make the overall length
        for side in (-1, 1):
            w = side * lateral
            _box(mesh, body.P(s + sign * 0.0025, w, (o_bottom + o_top) / 2), (0.045, o_top - o_bottom - 0.04, 0.05),
                 CHROME)
            if kit.F:  # the overriders' rubber tips burn away
                continue
            for y in (o_top - 0.02, o_bottom + 0.02):
                _box(mesh, body.P(s, w, y), (0.05, 0.04, 0.055), TYRE)

def _bumper_outward(body, front):
    def outward(p):
        c = body.P(body.s_front + 0.3 if front else body.s_rear - 0.3, 0.0, p[1])
        return (p[0] - c[0], 0.0, p[2] - c[2])
    return outward


def _front_details(mesh, body, q, kit):
    s = body.s_front
    gw, g0, g1 = body.preset["grille"]
    _box(mesh, body.P(s - 0.003, 0.0, (g0 + g1) / 2), (0.004, g1 - g0, 2 * gw), UNDER)  # the dark backing
    if q["detail"] >= 1:
        for y in (g0, g1):
            _box(mesh, body.P(s - 0.008, 0.0, y), (0.012, 0.014, 2 * gw + 0.014), CHROME)
        for w in (-gw, gw):
            _box(mesh, body.P(s - 0.008, w, (g0 + g1) / 2), (0.012, g1 - g0, 0.014), CHROME)
    if q["detail"] >= 2:
        for k in range(1, 16):
            w = -gw + 2 * gw * k / 16
            _box(mesh, body.P(s - 0.010, w, (g0 + g1) / 2), (0.012, g1 - g0, 0.006), CHROME)
        for k in range(1, 4):
            y = g0 + (g1 - g0) * k / 4
            _box(mesh, body.P(s - 0.010, 0.0, y), (0.008, 0.006, 2 * gw), CHROME)
    lw, ly, lr = body.preset["headlamp"]
    seg = 16 if q["detail"] >= 2 else 8
    for side in (-1, 1):
        a, b, y0, y1 = body.preset["indicator"]
        if not kit.F:  # the plastic indicators melt in F
            _box(mesh, body.P(s - 0.007, side * (a + b) / 2, (y0 + y1) / 2), (0.014, y1 - y0, b - a), LENS,
                 uvs=lambda p, n: LAMP_UV["amber"])
        if kit.notch is not None and kit.notch["side"] == side and q["detail"] >= 1:
            continue  # the struck lamp: its bowl and ring lie in the scatter, its socket is the tear
        centre = body.P(s, side * lw, ly)
        axis = (-1.0, 0.0, 0.0)
        _revolve(mesh, centre, axis, [(0.0, 0.018), (lr * 0.6, 0.015), (lr, 0.008)], seg, LAMP,
                 fixed_uv=LAMP_UV["clear"])  # a palette slot: every corner in the clear lens's column
        if q["detail"] >= 1:
            ring = [add(centre, (-0.010, lr * 1.04 * math.sin(2 * math.pi * k / seg),
                                 lr * 1.04 * math.cos(2 * math.pi * k / seg))) for k in range(seg)]
            ring = [(p[0], p[1], p[2]) for p in ring]
            _tube(mesh, [(p[0], centre[1] + (p[1] - centre[1]), centre[2] + (p[2] - centre[2])) for p in ring],
                  0.008, 6 if q["detail"] >= 2 else 4, CHROME, closed=True)
    if q["detail"] >= 1:  # the front plate on the blade's middle, its top at the blade's top
        _plate(mesh, body, BLADE_SET_M - PLATE_OFF_M - PLATE_T_M / 2, body.preset["plates"][0] - PLATE_M[1] / 2)


def _rear_details(mesh, body, q, kit):
    s = body.s_rear
    a, b, y0, y1 = body.preset["tail_lamp"]
    for side in () if kit.F else (-1, 1):  # the plastic tail lamps melt in F
        for k, colour in enumerate(("amber", "clear", "red")):
            w0, w1 = a + (b - a) * k / 3, a + (b - a) * (k + 1) / 3
            _box(mesh, body.P(s + 0.006, side * (w0 + w1) / 2, (y0 + y1) / 2), (0.012, y1 - y0, w1 - w0), LENS,
                 uvs=lambda p, n, c=colour: LAMP_UV[c])
    if q["detail"] >= 1:  # the rear plate on the rear panel between the tail lamps, over the bumper
        _plate(mesh, body, s + PLATE_OFF_M + PLATE_T_M / 2, body.preset["plates"][1] + PLATE_M[1] / 2)


def _plate(mesh, body, s, y):
    """A blank plate (`plate` blank) centred on the centre line at station s and height y; nothing when none."""
    if body.p["plate"] == "blank":
        _box(mesh, body.P(s, 0.0, y), (PLATE_T_M, PLATE_M[1], PLATE_M[0]), PLATE, uvs=lambda p, n: LAMP_UV["plate"])


def _wiper_arms(body):
    """[(pivot, tip)]: two arms parked along the windscreen's foot, 12 mm over the glass, pointing right (assumed:
    0.42 m arms, pivots 0.45 m apart)."""
    s = body.s_cowl + 0.05
    out = []
    for w0 in (-0.50, -0.05):
        a = add(body.cap_point_w(s, w0), (0.0, 0.012, 0.0))
        b = add(body.cap_point_w(s + 0.03, w0 + 0.42), (0.0, 0.012, 0.0))
        out.append((a, b))
    return out


# --- wheels ---------------------------------------------------------------------------------------------------------

def _tyre_profile(body):
    """(radius, offset outboard of the wheel's mid-plane) round the tyre from its inner bead to its outer bead."""
    r, h = body.r_tyre, body.w_tyre / 2
    return [(RIM_R_M, -h + 0.025), (0.20 * r / 0.297, -h + 0.001), (0.255 * r / 0.297, -h), (0.284 * r / 0.297, -h + 0.008),
            (r, -h + 0.022), (r, h - 0.022), (0.284 * r / 0.297, h - 0.008), (0.255 * r / 0.297, h),
            (0.20 * r / 0.297, h - 0.001), (RIM_R_M, h - 0.025)]


FLAT_BULGE = 0.12         # a flat tyre's sidewall bulges out by this share of its half-width at the ground (assumed)
FLAT_BULGE_REACH_M = 0.08 # and the bulge fades out this high (assumed: the folded sidewall at the patch)


def _corner(body, s_axle, side):
    return f"{'front' if s_axle == body.axle[0] else 'rear'}_{'left' if side < 0 else 'right'}"


def _wheel(mesh, body, s_axle, w, side, q, kit):
    corner = _corner(body, s_axle, side)
    if kit.F:
        _rim(mesh, kit.hubs[corner], side, q)
        return
    centre = body.P(s_axle, w, body.z_wc)
    axis = (0.0, 0.0, float(side))  # outboard
    seg = q["seg"]
    profile = _tyre_profile(body)
    if q["detail"] == 0:
        profile = [profile[0], profile[4], profile[5], profile[-1]]
    v0, f0 = len(mesh.vertices), len(mesh.faces)
    _revolve(mesh, centre, axis, profile, seg, TYRE, core=((RIM_R_M + body.r_tyre) / 2, 0.0),
             flatten_y=None if corner == kit.flat else 0.0, uv_scale=2 * math.pi * body.r_tyre)
    v1 = len(mesh.vertices)
    h = body.w_tyre / 2
    # the rim's outer face and flange, the domed hubcap, and the inner face
    _revolve(mesh, centre, axis, [(RIM_R_M, h - 0.025), (RIM_R_M - 0.008, h - 0.015), (HUBCAP_R_M + 0.002, h - 0.022),
                                  (HUBCAP_R_M, h - 0.02)], seg, CHROME)
    dome = [(HUBCAP_R_M, h - 0.02), (HUBCAP_R_M * 0.75, h - 0.02 + HUBCAP_DOME_M * 0.6),
            (HUBCAP_R_M * 0.35, h - 0.02 + HUBCAP_DOME_M * 0.95), (0.0, h - 0.02 + HUBCAP_DOME_M)]
    if q["detail"] == 0:
        dome = [dome[0], dome[-1]]
    _revolve(mesh, centre, axis, dome, seg, CHROME)
    _revolve(mesh, centre, axis, [(RIM_R_M, -h + 0.025), (0.0, -h + 0.03)], seg, UNDER, facing=-1.0)
    if corner == kit.flat:  # the wheel tilts with the body, its hub down by the tyre's drop (kit.W), the tyre
        hub = kit.hubs[corner]  # flattened on the ground over a longer patch, its sidewall bulging out near the ground
        for i in range(v0, len(mesh.vertices)):
            p = kit.W.point(mesh.vertices[i])
            if i < v1 and p[1] < FLAT_BULGE_REACH_M:
                f = 1 - p[1] / FLAT_BULGE_REACH_M
                p = (p[0], max(p[1], 0.0), hub[2] + (p[2] - hub[2]) * (1 + FLAT_BULGE * f))
            mesh.vertices[i] = p
        for k in range(f0, len(mesh.faces)):
            indices, normal, uvs, slot = mesh.faces[k]
            mesh.faces[k] = (indices, kit.W.vector(normal), uvs, slot)


def _rim(mesh, hub, side, q):
    """F's bare steel wheel on the ground (the tyre burnt away, the hubcap gone): the rim's barrel between its two
    flanges and the wheel's disc with its hub, dark burnt steel."""
    axis = (0.0, 0.0, float(side))
    seg = q["seg"]
    h = RIM_WIDTH_M / 2
    barrel = [(RIM_FLANGE_R_M, -h), (RIM_FLANGE_R_M - 0.006, -h + 0.006), (RIM_R_M, -h + 0.012), (RIM_R_M, h - 0.012),
              (RIM_FLANGE_R_M - 0.006, h - 0.006), (RIM_FLANGE_R_M, h)]
    if q["detail"] == 0:
        barrel = [barrel[0], barrel[-1]]
    _revolve(mesh, hub, axis, barrel, seg, CHROME_DULL, core=(0.0, 0.0))
    # the wheel's disc near the outer flange (a steel wheel's offset; within its cylinder's end, AST-05)
    disc = [(RIM_R_M, h - 0.02), (0.10, h - 0.012), (0.06, h - 0.004), (0.0, h - 0.002)]
    _revolve(mesh, hub, axis, disc, seg, CHROME_DULL)
    _revolve(mesh, hub, axis, [(RIM_R_M, h - 0.022), (0.0, h - 0.022)], seg, CHROME_DULL, facing=-1.0)


# --- interior -------------------------------------------------------------------------------------------------------

def _seat_boxes(body):
    """[(centre, size, axes, slot, kind)] of the seats' cushions and backs."""
    out = []
    pre = body.preset
    for key, sides in (("front_seat", (-1, 1)), ("rear_seat", (0,))):
        seat = pre[key]
        for side in sides:
            w = side * seat["w"]
            (s0, s1), (y0, y1) = seat["s"], seat["y"]
            out.append((body.P((s0 + s1) / 2, w, (y0 + y1) / 2), (s1 - s0, y1 - y0, 2 * seat["half"]), _AXES, SEAT,
                        "cushion"))
            (bs0, by0), (bs1, by1) = seat["back"]
            e1 = normalize(sub(body.P(bs1, w, by1), body.P(bs0, w, by0)))  # up the back
            e2 = (0.0, 0.0, 1.0)
            e0 = cross(e1, e2)  # forward-ish, perpendicular to the back
            if e0[0] > 0:
                e0 = scale(e0, -1)
                e2 = (0.0, 0.0, -1.0)
            length = norm(sub(body.P(bs1, w, by1), body.P(bs0, w, by0)))
            mid = scale(add(body.P(bs0, w, by0), body.P(bs1, w, by1)), 0.5)
            centre = add(mid, scale(e0, -seat["back_t"] / 2))
            out.append((centre, (seat["back_t"], length, 2 * seat["half"] - 0.02), (e0, e1, e2), SEAT, "back"))
    return out


_AXES = ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))


def _interior(mesh, body, q, kit):
    if kit.open:
        _cabin_shell(mesh, body, q, kit)
    if kit.F:
        _burnt_interior(mesh, body, q)
        return
    for centre, size, axes, slot, _ in _seat_boxes(body):
        _box(mesh, centre, size, slot, axes)
    d = body.preset["dashboard"]
    (s0, s1), (y0, y1) = d["s"], d["y"]
    _box(mesh, body.P((s0 + s1) / 2, 0.0, (y0 + y1) / 2), (s1 - s0, y1 - y0, 2 * d["half"]), VINYL)
    st = body.preset["steering"]
    hub, axis, e1, e2 = _steering_frame(body)
    seg = 24 if q["detail"] >= 2 else 10
    ring = [add(hub, add(scale(e1, st["r"] * math.cos(2 * math.pi * k / seg)),
                         scale(e2, st["r"] * math.sin(2 * math.pi * k / seg)))) for k in range(seg)]
    _tube(mesh, ring, st["tube"], 8 if q["detail"] >= 2 else 4, VINYL, closed=True)
    if q["detail"] >= 2:
        for side in (-1, 1):  # two spokes, dipped 10 degrees, and the hub
            tip = add(hub, add(scale(e1, side * st["r"] * math.cos(math.radians(10))),
                               scale(e2, -st["r"] * math.sin(math.radians(10)))))
            _tube(mesh, [hub, tip], 0.01, 6, VINYL)
        _revolve(mesh, hub, axis, [(0.045, -0.01), (0.04, 0.02), (0.0, 0.03)], 12, VINYL)
        column_end = body.P(d["s"][1] - 0.02, st["w"], d["y"][0] + 0.06)
        _tube(mesh, [sub(hub, scale(axis, 0.02)), column_end], 0.025, 8, VINYL)


def _seat_back_station(body, y):
    """The station of the rear seat back's rear face at height y (the boot's front behind it)."""
    (bs0, by0), (bs1, by1) = body.preset["rear_seat"]["back"]
    along = math.hypot(bs1 - bs0, by1 - by0)
    rear = body.preset["rear_seat"]["back_t"] * along / (by1 - by0)  # the back's thickness, measured along s
    return bs0 + (y - by0) * (bs1 - bs0) / (by1 - by0) + rear


def _liner_top(body, s):
    """The rear wheelhouse's liner height at s over its arch (the floor elsewhere)."""
    a = body.axle[1]
    d = s - a
    if abs(d) >= body.r_arch:
        return body.z_bot(s) + GAUGE_M
    return body.z_wc + math.sqrt(body.r_arch ** 2 - d * d)


def _bulkhead_station(body):
    """The boot's lower front wall behind the rear seat, between the wheelhouses: at the seat back's rear face where
    the back meets the cushion."""
    return _seat_back_station(body, body.preset["rear_seat"]["back"][0][1])


def _upper_bulkhead_station(body):
    """The boot's upper front wall, over the wheelhouses' liners: behind the leaning seat back at the shelf."""
    return _seat_back_station(body, body.belt - 0.01) + 0.005


def _cabin_shell(mesh, body, q, kit):
    """The cabin's inside, which the closed car never shows (task 1.1 left it to the kits): per side the inner wall
    DOOR_T_M in from the flank, from the floor (over the rear arch, from its liner) to the belt (the door trims, the
    kick panel, the quarter's trim), the firewall at the cowl, the boot's bulkhead behind the rear seat (between the
    wheelhouses below their liners, between the trims above) and the parcel shelf from it to the rear screen.
    Two-sided (seen from the cabin and through the doors' holes). Trim vinyl, the walls dark; F's bare sooted steel."""
    trim = SOOT if kit.F else VINYL
    wall = SOOT if kit.F else UNDER
    s_end = _seat_back_station(body, body.belt) + 0.01
    rows = loft.samples(body.s_cowl, s_end, q["step"] * 2, [body.axle[1] - body.r_arch])
    floor = lambda s: body.z_bot(s) + GAUGE_M  # noqa: E731
    for side in (-1, 1):
        grid = [[body.P(s, side * (body.hw(s) - DOOR_T_M), _liner_top(body, s)),
                 body.P(s, side * (body.hw(s) - DOOR_T_M), body.belt)] for s in rows]
        uvs = [[(body.x(s), _liner_top(body, s)), (body.x(s), body.belt)] for s in rows]
        for facing in (-side, side):
            loft.surface(mesh, grid, uvs, trim, lambda p, f=facing: (0.0, 0.0, float(f)))
        # the rear wheel tub's top in the cabin, from the wheelhouse's inner wall to the trim: the liner seen from
        # above, from the arch's front up to the upper bulkhead (the wheelhouse's liner faces the wheel only)
        a, r = body.axle[1], body.r_arch
        t_up = math.acos(max(-1.0, min(1.0, (a - _upper_bulkhead_station(body)) / r)))
        m = max(2, int(math.ceil(t_up / math.pi * (24 if q["thick"] else 12 if q["detail"] else 6))))
        tub, tub_uv = [], []
        for i in range(m + 1):
            t = t_up * i / m
            s, y = a - r * math.cos(t), body.z_wc + r * math.sin(t)
            tub.append([body.P(s, side * body.w_in[1], y), body.P(s, side * (body.hw(s) - DOOR_T_M), y)])
            tub_uv.append([(body.x(s), body.w_in[1]), (body.x(s), body.hw(s) - DOOR_T_M)])
        loft.surface(mesh, tub, tub_uv, wall, lambda p, a=a: (p[0] - body.x(a), p[1] - body.z_wc, 0.0))
        # and its inner wall's cabin face ahead of the lower bulkhead (the wheelhouse's faces the wheel only)
        t_bh = math.acos(max(-1.0, min(1.0, (a - _bulkhead_station(body)) / r)))
        m = max(2, int(math.ceil(t_bh / t_up * m)))
        inner, inner_uv = [], []
        for i in range(m + 1):
            t = t_bh * i / m
            s, y = a - r * math.cos(t), body.z_wc + r * math.sin(t)
            inner.append([body.P(s, side * body.w_in[1], body.z_wc), body.P(s, side * body.w_in[1], y)])
            inner_uv.append([(body.x(s), body.z_wc), (body.x(s), y)])
        loft.surface(mesh, inner, inner_uv, wall, lambda p, f=-float(side): (0.0, 0.0, f))
    # the firewall, up to 1 cm under the crowned top's edge at the cowl (the scuttle's plenum closes the bay)
    s = body.s_cowl + 0.005
    half = body.hw(s) - DOOR_T_M
    crown = body.crown(body.s_cowl)
    us = [-1.0 + 2.0 * k / 16 for k in range(17)]
    for u0, u1 in zip(us, us[1:]):
        quad = [body.P(s, u0 * half, floor(s)), body.P(s, u1 * half, floor(s))]
        quad += [body.P(s, u * half, body.z_sh(s) + crown * (1 - abs(u) ** CROWN_POWER) - 0.01) for u in (u1, u0)]
        for facing in (1.0, -1.0):
            mesh.face([mesh.vertex(p) for p in quad], (facing, 0.0, 0.0), wall)
    y = body.belt - 0.01
    s_bh, s_up = _bulkhead_station(body), _upper_bulkhead_station(body)
    w_low, w_high = body.w_in[1] - 0.01, body.hw(s_up) - 0.03
    y_liner = body.z_wc + body.r_arch
    for quad, n in (([body.P(s_bh, -w_low, floor(s_bh)), body.P(s_bh, w_low, floor(s_bh)),
                      body.P(s_bh, w_low, y_liner), body.P(s_bh, -w_low, y_liner)], (1.0, 0.0, 0.0)),
                    ([body.P(s_bh, -w_low, y_liner), body.P(s_up, -w_low, y_liner), body.P(s_up, w_low, y_liner),
                      body.P(s_bh, w_low, y_liner)], (0.0, 1.0, 0.0)),
                    ([body.P(s_up, -w_high, y_liner), body.P(s_up, w_high, y_liner), body.P(s_up, w_high, y),
                      body.P(s_up, -w_high, y)], (1.0, 0.0, 0.0))):
        for facing in (1.0, -1.0):
            mesh.face([mesh.vertex(p) for p in quad], scale(n, facing), wall)
    s1 = body.s_rs_base
    shelf = [body.P(s_up, -(body.hw(s_up) - DOOR_T_M), y), body.P(s1, -(body.hw(s1) - DOOR_T_M), y),
             body.P(s1, body.hw(s1) - DOOR_T_M, y), body.P(s_up, body.hw(s_up) - DOOR_T_M, y)]
    for facing in (1.0, -1.0):
        mesh.face([mesh.vertex(p) for p in shelf], (0.0, facing, 0.0), trim)


def _burnt_seats(body):
    """F's seats as what a fire leaves (the sheet: frames and springs): per cushion its frame from the floor to the
    spring mat (side plates, rails front and back) with its zigzag springs, per back its frame round the springs
    in the back's mid-plane. [(kind, centre, size, axes, springs)], springs as polylines; the boxes are their
    collision."""
    out = []
    pre = body.preset
    for key, sides in (("front_seat", (-1, 1)), ("rear_seat", (0,))):
        seat = pre[key]
        for side in sides:
            w = side * seat["w"]
            (s0, s1), (_, y1) = seat["s"], seat["y"]
            half = seat["half"] - 0.01
            y_floor = body.z_bot((s0 + s1) / 2) + GAUGE_M
            y_mat = y1 - 0.07  # the springs sit 7 cm under the burnt cushion's top (assumed)
            springs = []
            n = max(3, int(round(2 * half / 0.075)))
            for k in range(n):
                wk = w - half + 2 * half * (k + 0.5) / n
                pts = []
                steps = 12
                for i in range(steps + 1):
                    f = i / steps
                    s = s0 + 0.02 + (s1 - s0 - 0.04) * f
                    sag = 0.015 * math.sin(math.pi * f)
                    pts.append(body.P(s, wk + (0.018 if i % 2 else -0.018), y_mat - sag))
                springs.append(pts)
            out.append(("cushion", body.P((s0 + s1) / 2, w, (y_floor + y_mat) / 2), (s1 - s0, y_mat - y_floor, 2 * half),
                        _AXES, springs))
            (bs0, by0), (bs1, by1) = seat["back"]
            a, b = body.P(bs0, w, by0), body.P(bs1, w, by1)
            e1 = normalize(sub(b, a))
            e0 = normalize(cross(e1, (0.0, 0.0, 1.0)))
            if e0[0] > 0:
                e0 = scale(e0, -1)
            mid = sub(scale(add(a, b), 0.5), scale(e0, seat["back_t"] / 2))
            length = norm(sub(b, a))
            springs = []
            for k in range(n):
                wk = w - half + 2 * half * (k + 0.5) / n
                pts = []
                steps = 12
                for i in range(steps + 1):
                    f = i / steps
                    base = add(sub(mid, scale(e1, length / 2 - 0.03)), scale(e1, (length - 0.06) * f))
                    pts.append(add((base[0], base[1], wk + (0.018 if i % 2 else -0.018)),
                                   scale(e0, -0.008 * math.sin(math.pi * f))))
                springs.append(pts)
            out.append(("back", mid, (0.03, length, 2 * half), (e0, e1, (0.0, 0.0, 1.0)), springs))
    return out


def _burnt_interior(mesh, body, q):
    """F's interior: the seat frames with their springs, the steering wheel's steel core and the dashboard's steel
    cross-beam (the sheet: plastics gone, frames and springs left)."""
    for kind, centre, size, axes, springs in _burnt_seats(body):
        h = [v / 2 for v in size]

        def at(a, b, c, centre=centre, axes=axes):
            return add(centre, add(scale(axes[0], a), add(scale(axes[1], b), scale(axes[2], c))))
        if kind == "cushion":  # side plates and four rails front and back
            for sz in (-1, 1):
                plate = [at(-h[0], -h[1], sz * h[2]), at(h[0], -h[1], sz * h[2]), at(h[0], h[1], sz * h[2]),
                         at(-h[0], h[1], sz * h[2])]
                for facing in (1, -1):
                    mesh.face([mesh.vertex(p) for p in plate], scale(axes[2], sz * facing), RACK)
            a, b = h[0] - FRAME_TUBE_R_M, h[1] - FRAME_TUBE_R_M  # the rails inside the frame's box, on the floor
            for sx in (-1, 1):
                for f in (-1.0, -1 / 3, 1 / 3, 1.0):
                    _tube(mesh, [at(sx * a, f * b, -h[2]), at(sx * a, f * b, h[2])], FRAME_TUBE_R_M,
                          6 if q["thick"] else 4, RACK)
        else:  # the back's frame round its springs
            corners = [at(0, -h[1], -h[2]), at(0, h[1], -h[2]), at(0, h[1], h[2]), at(0, -h[1], h[2])]
            _tube(mesh, corners, FRAME_TUBE_R_M, 6 if q["thick"] else 4, RACK, closed=True)
        if q["thick"]:
            for pts in springs:
                _tube(mesh, pts, SPRING_R_M, 4, RACK)
    st = body.preset["steering"]
    hub, axis, e1, e2 = _steering_frame(body)
    seg = 16 if q["thick"] else 8
    ring = [add(hub, add(scale(e1, st["r"] * math.cos(2 * math.pi * k / seg)),
                         scale(e2, st["r"] * math.sin(2 * math.pi * k / seg)))) for k in range(seg)]
    _tube(mesh, ring, STEEL_CORE_R_M, 6 if q["thick"] else 4, RACK, closed=True)
    for side in (-1, 1):
        tip = add(hub, add(scale(e1, side * st["r"] * math.cos(math.radians(10))),
                           scale(e2, -st["r"] * math.sin(math.radians(10)))))
        _tube(mesh, [hub, tip], 0.006, 6, RACK)
    d = body.preset["dashboard"]
    column_end = body.P(d["s"][1] - 0.02, st["w"], d["y"][0] + 0.06)
    _tube(mesh, [sub(hub, scale(axis, 0.02)), column_end], 0.02, 8, RACK)
    beam = _dash_beam(body)
    _tube(mesh, list(beam), DASH_BEAM_R_M, 8, RACK)


STEEL_CORE_R_M = 0.006    # the steering wheel rim's steel core (assumed)
DASH_BEAM_R_M = 0.025     # the dashboard's steel cross-beam left by the fire (assumed)


def _dash_beam(body):
    """The cross-beam's ends: on the trims, where it is bolted (its collision a flat-ended cylinder like its tube)."""
    d = body.preset["dashboard"]
    s = (d["s"][0] + d["s"][1]) / 2
    y = d["y"][0] + 0.08
    half = body.hw(s) - DOOR_T_M
    return body.P(s, -half, y), body.P(s, half, y)


def _steering_frame(body):
    """(hub, axis toward the driver, two unit vectors in the wheel's plane: lateral and up its face)."""
    st = body.preset["steering"]
    t = math.radians(st["tilt_deg"])
    axis = (math.cos(t), math.sin(t), 0.0)  # +s rearward toward the driver, tilted up
    e1 = (0.0, 0.0, 1.0)
    e2 = cross(axis, e1)
    if e2[1] < 0:
        e2 = scale(e2, -1)
    return body.P(st["s"], st["w"], st["y"]), axis, e1, e2


# --- the rack ------------------------------------------------------------------------------------------------------

def _rack_points(body):
    """(frame corners [front-left, front-right, rear-right, rear-left], posts [(foot, top)])."""
    s0, s1, half, posts = body.preset["rack"]
    y = body.z_top(body.st["roof_front"]) + RACK_RISE_M
    corners = [body.P(s0, -half, y), body.P(s0, half, y), body.P(s1, half, y), body.P(s1, -half, y)]
    feet = []
    for s in posts:
        for side in (-1, 1):
            foot = body.P(s, side * (body.w_e(s) + 0.005), body.z_e(s) + 0.012)
            top = body.P(s, side * half, y)
            feet.append((foot, top))
    return corners, feet


def _rack(mesh, body, q):
    """The tube rack, bright steel like the bumpers (measured M: it reads pale grey; F dulls it with the chrome)."""
    corners, posts = _rack_points(body)
    sides = 8 if q["detail"] >= 2 else 4
    for a, b in zip(corners, corners[1:] + corners[:1]):
        _tube(mesh, [a, b], RACK_R_M, sides, CHROME)
    for foot, top in posts:
        _tube(mesh, [foot, top], RACK_R_M, sides, CHROME)


# --- LOD3 ----------------------------------------------------------------------------------------------------------

def _box_hull(body, kit):
    mesh = Mesh()
    hw = body.hw(body.s_ws_top)
    paint = SOOT if kit.F else PAINT
    # the greenhouse: its glass, or with every pane gone the dark opening of the cabin (QA 2.1's S6)
    glazed = (any(g != "gone" for g in kit.glass.values()) or kit.windscreen != "gone"
              or kit.rear_screen != "gone")
    _box(mesh, body.P((body.s_front + body.s_rear) / 2, 0.0, (body.z_bot(1.0) + body.belt) / 2),
         (body.s_rear - body.s_front, body.belt - body.z_bot(1.0), 2 * hw), paint)
    _box(mesh, body.P((body.s_ws_top + body.s_rs_top) / 2, 0.0, (body.belt + body.H) / 2),
         (body.s_rs_top - body.s_ws_top, body.H - body.belt, 2 * body.w_e(body.s_ws_top)),
         GLASS_HULL if glazed else UNDER)
    kits.transform_range(mesh, (0, len(mesh.vertices)), (0, len(mesh.faces)), kit.T)
    for k, a in enumerate(body.axle):
        for side in (-1, 1):
            hub = kit.hubs[_corner(body, a, side)]
            if kit.F:
                _box(mesh, hub, (2 * RIM_FLANGE_R_M, 2 * hub[1], RIM_WIDTH_M), RACK)
            else:
                _box(mesh, hub, (2 * body.r_tyre, 2 * hub[1], body.w_tyre), TYRE)
    return mesh


# --- the K07 pieces composed into the car (design decision 9) ------------------------------------------------------

# K07's params (assets-src/debris/K07_*.json; tests/test_vehicle checks they match): the scatter's shards vary round
# the trim plate's 0.35 x 0.2 m (the sheet: 0.1-0.6 m)
K07 = {"lamp_bowl": {"diameter_m": 0.17, "section_m": 0.0008, "length_m": 0.0, "width_m": 0.0, "bend_m": 0.0},
       "bezel_ring": {"diameter_m": 0.18, "section_m": 0.012, "length_m": 0.0, "width_m": 0.0, "bend_m": 0.0},
       "trim_plate": {"diameter_m": 0.0, "section_m": 0.001, "length_m": 0.35, "width_m": 0.2, "bend_m": 0.01},
       "bead_wire": {"diameter_m": BEAD_DIAMETER_M, "section_m": BEAD_SECTION_M, "length_m": 0.0, "width_m": 0.0,
                     "bend_m": 0.01}}
SHARD_SCALE = (0.15, 0.45)  # a scatter chip's size against the trim plate (task 1.4, measured M: chips of 5-15 cm
                            # round the one whole plate; 0.5-1.0 laid ten plates)
SCATTER_MIN_M = 1.0       # the sheet: the A scatter lies 1-4 m from the struck corner
SCATTER_WEDGE_DEG = 150.0 # on the struck side: a wedge about the corner's outward diagonal (measured M: ahead of and
                          # beside the struck corner; the wedge's width assumed)


def _piece(kind, seed, **sizes):
    from generators import debris_car
    params = {"piece": kind, "seed": seed, **K07[kind], **sizes, "physics_material": "steel",
              "lod": {"lod1_switch_m": 8.0, "cull_m": 80.0}}
    return debris_car.build(params)


def _pieces(body, kit):
    """The K07 pieces the kit lays on the ground (each {"kind", "lods", "rigid", "slots", "collision", "snag"} in its
    own frame, placed by "rigid"): A's scatter from the struck corner (a lamp bowl, a bezel ring and shards of trim)
    at 1/r^2 areal density from SCATTER_MIN_M to debris_radius, outside the car; F's two bead-wire rings per wheel,
    one round the rim on the ground and one flat beside the wheel."""
    out = []
    slots = {"chrome": CHROME, "rust": WIRE}
    if kit.A and kit.p["debris_count"] > 0:
        rng = kits.rng_for(kit.p, "scatter")
        corner = (body.x(0.0), 0.0, kit.side * body.hw(body.s_front))
        outward = math.atan2(kit.side, -1.0)  # the struck corner's outward diagonal (ahead and to its side)
        placed = []
        for k in range(kit.p["debris_count"]):
            if k == 0:
                built, reach = _piece("lamp_bowl", k), K07["lamp_bowl"]["diameter_m"] / 2
            elif k == 1:
                built, reach = _piece("bezel_ring", k), K07["bezel_ring"]["diameter_m"] / 2
            elif k == 2:  # the trim plate whole (measured M: one 0.35 x 0.2 m plate), the rest chips of trim
                plate = K07["trim_plate"]
                built, reach = _piece("trim_plate", k), math.hypot(plate["length_m"], plate["width_m"]) / 2
            else:
                f = rng.uniform(*SHARD_SCALE)
                length, width = 0.35 * f, 0.2 * rng.uniform(SHARD_SCALE[0], 1.0)
                built = _piece("trim_plate", k, length_m=round(length, 4), width_m=round(width, 4),
                               bend_m=round(rng.uniform(0.0, 0.02), 4))
                reach = math.hypot(length, width) / 2
            r = kits.scatter_radius(rng, SCATTER_MIN_M, kit.p["debris_radius"])
            for _ in range(400):  # a new bearing at the same distance, so the 1/r^2 law holds
                a = outward + math.radians(SCATTER_WEDGE_DEG) * (rng.random() - 0.5)
                x, z = corner[0] + r * math.cos(a), corner[2] + r * math.sin(a)
                clear_car = abs(x) > body.L / 2 + 0.1 + reach or abs(z) > body.W / 2 + 0.1 + reach
                if clear_car and all(math.hypot(x - px, z - pz) > reach + pr + 0.02 for px, pz, pr in placed):
                    break
            else:
                raise ValueError("no room for the scatter's pieces: fewer or a larger debris_radius")
            placed.append((x, z, reach))
            rigid = kits.axis_rotation((0.0, 0.0, 0.0), (0.0, 1.0, 0.0), rng.uniform(0, 2 * math.pi)).then(
                kits.translation((x, 0.0, z)))
            out.append({"kind": f"scatter_{k}", "lods": built["lods"], "rigid": rigid, "slots": slots,
                        "collision": built["collision"], "snag": built["snag"], "r": r})
    if kit.F:
        rng = kits.rng_for(kit.p, "bead wires")
        ring = _piece("bead_wire", 1)
        for corner, hub in sorted(kit.hubs.items()):
            side = -1 if corner.endswith("left") else 1
            stand = kits.axis_rotation((0.0, 0.0, 0.0), (1.0, 0.0, 0.0), math.pi / 2).then(
                kits.axis_rotation((0.0, 0.0, 0.0), (0.0, 0.0, 1.0), rng.uniform(-0.3, 0.3))).then(
                kits.translation((hub[0], BEAD_DIAMETER_M / 2 - BEAD_SECTION_M / 2, hub[2] + side * 0.03)))
            lie = kits.axis_rotation((0.0, 0.0, 0.0), (0.0, 1.0, 0.0), rng.uniform(0, 2 * math.pi)).then(
                kits.translation((hub[0] + rng.uniform(-0.15, 0.15), 0.0,
                                  hub[2] + side * (RIM_WIDTH_M / 2 + BEAD_DIAMETER_M / 2 + rng.uniform(0.05, 0.15)))))
            for name, rigid in (("rim", stand), ("ground", lie)):
                out.append({"kind": f"bead_{corner}_{name}", "lods": ring["lods"], "rigid": rigid, "slots": slots,
                            "collision": ring["collision"], "snag": ring["snag"]})
    return out


def _micro(body, kit):
    """The visual-only micro meshes (lib.meta "micro"; the audit skips them): A's glass crumbs (design decision 9),
    F's soot patch (the G1 stand-in for the terrain's soot mask, design decision 4) in LOD0 to LOD2."""
    out = []
    count = 0
    crumbs = Mesh()
    rng = kits.rng_for(kit.p, "crumbs")
    if kit.A:
        per = int(round(CRUMBS_PER_WINDOW * kit.p["crumb_cover"]))
        windows = _windows(body)
        for side in (-1, 1):
            for pair in (0, 1):
                if kit.glass[side, pair] != "gone" or per == 0:
                    continue
                spans = [w for w, p in zip(windows, _window_pairs(body)) if p == pair]
                s0, s1 = spans[0][0], spans[-1][1]
                inside = int(round(per * CRUMB_INSIDE))
                seat = body.preset["front_seat" if pair == 0 else "rear_seat"]
                body_frame = Mesh()
                for _ in range(inside):
                    _crumb_inside(body_frame, body, side, seat, s0, s1, rng)
                kits.append_mesh(crumbs, body_frame, kit.T)
                for _ in range(per - inside):  # scattered on the ground beside the door, a few per square metre
                    s = rng.uniform(s0 - 0.3, s1 + 0.3)
                    w = side * (body.hw(s) + 0.05 + (CRUMB_SPREAD_M - 0.05) * rng.random())
                    _crumb(crumbs, rng, (body.x(s), 0.0, w))
                count += per
        if count:
            out.append({"lod": 0, "kind": "glass_crumbs", "count": count, "mesh": crumbs})
    if kit.F and kit.p["soot_patch"] > 0:
        for lod, sectors in ((0, 64), (1, 32), (2, 16)):
            patch, cells = _soot_patch(body, kit, sectors)
            out.append({"lod": lod, "kind": "soot_patch", "count": cells, "mesh": patch})
    return out


def _crumb(mesh, rng, ground):
    size = tuple(c * rng.uniform(0.7, 1.3) for c in CRUMB_M)
    kits.crumb_box(mesh, (ground[0], ground[1] + size[1] / 2 + 0.0005, ground[2]), size, rng.uniform(0, math.pi),
                   LAMP, LAMP_UV["clear"])


def _crumb_inside(mesh, body, side, seat, s0, s1, rng):
    """A crumb inside the car under a broken pane (body frame): on the seat cushion by the door or on the floor
    between the cushion and the door's trim."""
    (c0, c1), (_, top) = seat["s"], seat["y"]
    outer = seat["w"] + seat["half"] if seat["w"] else seat["half"]
    if rng.random() < 0.6:
        s = rng.uniform(max(c0, s0), min(c1, s1))
        w = side * rng.uniform(outer - 0.25, outer - 0.02)
        y = top
    else:
        s = rng.uniform(s0, s1)
        w = side * rng.uniform(outer, body.hw(s) - DOOR_T_M - 0.01)
        y = body.z_bot(s) + GAUGE_M
    _crumb(mesh, rng, (body.x(s), y, w))


SOOT_CORE = 0.62          # the soot patch's solid core reaches this share of its radius; past it, blotches thin out
SOOT_FEATHER = 1.1        # to this share (assumed: a feathered edge 0.5 R wide, no hard rim)


def _soot_patch(body, kit, sectors):
    """F's burnt ground: soot_patch across, 2 mm over the ground, centred under the burn's origin: a solid core with
    a ragged outline, then seeded blotches (irregular polygons, 0.04-0.2 m) thinning and shrinking out to
    SOOT_FEATHER of the radius (a feathered edge as geometry). `sectors` sets the detail per LOD. Returns the mesh
    and its blotch count."""
    mesh = Mesh()
    rng = kits.rng_for(kit.p, f"soot patch {sectors}")
    r_max = kit.p["soot_patch"] / 2
    s_c = body.axle[0] + 0.3 if kit.p["burn_origin"] == "engine" else (body.s_cowl + body.s_rs_base) / 2
    cx = body.x(s_c)
    phase = [rng.uniform(0, 2 * math.pi) for _ in range(6)]
    outline = []
    for k in range(sectors):
        a = 2 * math.pi * k / sectors
        r = r_max * SOOT_CORE * (1 + 0.18 * kits._noise(a, phase))
        outline.append((cx + r * math.cos(a), 0.002, r * math.sin(a)))
    centre = mesh.vertex((cx, 0.002, 0.0))
    ids = [mesh.vertex(p) for p in outline]
    for k in range(sectors):
        a, b = outline[k], outline[(k + 1) % sectors]
        mesh.face([centre, ids[k], ids[(k + 1) % sectors]], (0.0, 1.0, 0.0), SOOT_PATCH,
                  [(cx, 0.0), (a[0], a[2]), (b[0], b[2])])
    count = 0
    for _ in range(sectors * 5):
        f = rng.random()
        rho = r_max * (SOOT_CORE * 0.9 + (SOOT_FEATHER - SOOT_CORE * 0.9) * f)
        if rng.random() > (1 - f) ** 1.5:  # thinning outward
            continue
        count += 1
        a = rng.uniform(0, 2 * math.pi)
        bx, bz = cx + rho * math.cos(a), rho * math.sin(a)
        size = rng.uniform(0.04, 0.2) * (1 - 0.6 * f)
        n = rng.randint(6, 9)
        pts = []
        for k in range(n):
            t = 2 * math.pi * k / n
            r = size * rng.uniform(0.6, 1.0)
            pts.append((bx + r * math.cos(t), 0.002 + 0.0001 * (1 + f), bz + r * math.sin(t)))
        mesh.face([mesh.vertex(p) for p in pts], (0.0, 1.0, 0.0), SOOT_PATCH, [(p[0], p[2]) for p in pts])
    return mesh, count


# --- the masks (design decision 5) --------------------------------------------------------------------------------

def _textures(body, kit):
    """The generator's textures (lib.meta "textures"): the crazed windscreen's crack mask on its slot, and the
    damage mask over the paint's canvas (the A and F kits; on SOOT in F, where the paint is soot)."""
    out = []
    if kit.ws_hole is not None:
        out.append(_crack_mask(body, kit))
    if not kit.clean:
        out.append(_damage_mask(body, kit))
    return out


def _pane_extent(body):
    """The windscreen pane's UV rectangle (its x along the car, its z across): uv_min, uv_max."""
    s0, s1, um = _cap_glass(body)[0]
    half = max(abs(body.cap_point(s, um)[2]) for s in (s0, s1))
    return (body.x(s0), -half), (body.x(s1), half)


def _crack_mask(body, kit):
    """The windscreen's crack mask (CRACK_PX, R: 1 on a crack): from each impact (the flap's strike at the hole and
    CRACK_HITS seeded fragment hits) CRACK_RAYS radial cracks wandering out 0.3-0.9 m and concentric rings of chords
    between them out to 0.46 m, 1-3 px wide (design decision 5), drawn in the pane's metric (metres up the glass and
    across), over a haze of the cracks too fine for a texel (CRACK_HAZE)."""
    rng = kits.rng_for(kit.p, "cracks")
    (u0, v0), (u1, v1) = _pane_extent(body)
    width, height = CRACK_PX
    raster = masks.Raster(width, height, 1)

    def px(a, w):  # (metres up the pane from the car's front, metres across) -> pixels; row 0 is v1
        x = a / kit.ws_k - body.L / 2
        return (x - u0) / (u1 - u0) * width, (v1 - w) / (v1 - v0) * height
    hole = kit.ws_hole
    s0, s1, _ = _cap_glass(body)[0]
    impacts = [(hole.s, hole.y, 1.0)]
    for _ in range(rng.randint(*CRACK_HITS)):
        impacts.append((rng.uniform(s0 * kit.ws_k + 0.1, s1 * kit.ws_k - 0.1), rng.uniform(v0 + 0.15, v1 - 0.15),
                        rng.uniform(0.6, 1.0)))
    for a0, w0, strength in impacts:
        rays = []
        n_rays = rng.randint(*CRACK_RAYS)
        for k in range(n_rays):
            angle = 2 * math.pi * k / n_rays + rng.uniform(-0.2, 0.2)
            length = rng.uniform(0.3, 0.9) * strength
            pts = [(a0, w0)]
            step = 0.02
            heading = angle
            for _ in range(int(length / step)):
                heading += rng.uniform(-0.25, 0.25)
                pts.append((pts[-1][0] + step * math.cos(heading), pts[-1][1] + step * math.sin(heading)))
            rays.append(pts)
            lw = rng.uniform(1.0, 3.0)
            for p, q in zip(pts, pts[1:]):
                raster.line(0, *px(*p), *px(*q), lw, 255)
        for ring_r in (0.03, 0.06, 0.1, 0.15, 0.22, 0.3, 0.38, 0.46):
            if ring_r > 0.6 * strength:
                break
            idx = max(1, int(ring_r / 0.02))
            for k in range(len(rays)):
                a, b = rays[k], rays[(k + 1) % len(rays)]
                if len(a) <= idx or len(b) <= idx or rng.random() < 0.25:
                    continue
                raster.line(0, *px(*a[idx]), *px(*b[idx]), rng.uniform(1.0, 2.0), 255)
    # the web's fine cracks under a texel as a haze: CRACK_HAZE near each impact, thinning out to its reach, never
    # under CRACK_HAZE_FLOOR of it (the pane is crazed across, measured M); in 2 x 2 texel blocks (it is smooth)
    for by in range(0, height, 2):
        w = v1 - (by + 1) / height * (v1 - v0)
        for bx in range(0, width, 2):
            a = ((bx + 1) / width * (u1 - u0) + u0 + body.L / 2) * kit.ws_k
            near = max(st * (1 - math.hypot(a - a0, w - w0) / CRACK_REACH_M) for a0, w0, st in impacts)
            raster.rect(0, bx, by, bx + 2, by + 2, 255 * CRACK_HAZE * max(near, CRACK_HAZE_FLOOR))
    uv_min, uv_max = _pane_extent(body)
    return {"role": "crack_mask", "slot": WINDSCREEN, "name": "windscreen_cracks", "uv_min": uv_min, "uv_max": uv_max,
            "png": raster.png()}


RUST_RIM = 1.12           # a hole's torn rim rusts out to this times its radius (mask R full; measured M: the thin
                          # orange lips of the torn holes)
HALO_FLAKE = (0.40, 0.60) # its halo out to rust_halo: the paint flaked to the primer (B, the dust recipe's #bcab93 is
                          # M's primer #bdb092) over rust (R): M's halo #887465 is about 2 : 3 primer to rust stain
                          # over the paint (derived from the sheet's colours)
HALO_SPREAD = (0.7, 1.3)  # each hole's halo is rust_halo times this (seeded; measured M: the 0.09 m hole's 0.21 m
                          # halo, the 0.15-0.18 m hole's under 1.5 x)
CHIP_R_M = (0.005, 0.025) # the sheet: primer chips 10-50 mm across where the paint flaked (measured M), a jagged blob
DUST_BASE = 0.05          # the dust film on upward faces before months_abandoned and paint_fade add theirs (task 1.4:
                          # 0.15 left M's lit bonnet beige; M reads green with a light film)
BARE_RUST_WEEKS = 12.0    # F: the bare steel fully rust-brown after this many weeks (assumed; the sheet: grey-white
                          # oxide turning rust-brown "within weeks"), blotchy before


def _blob(raster, channel, cx, cy, radius, value, feather, phase):
    """Raises `channel` over a jagged disc about (cx, cy) px: radius(a) px at angle a (kits' noise with `phase` when
    `radius` is a number), `value` inside, falling to 0 over the outer `feather` px."""
    reach = radius if callable(radius) else (lambda a, r=radius: r * (1 + kits.HALO_JITTER * kits._noise(a, phase)))
    top = max(reach(2 * math.pi * k / 16) for k in range(16)) * 1.1 + 1
    for y in range(max(0, int(cy - top)), min(raster.height, int(cy + top) + 2)):
        for x in range(max(0, int(cx - top)), min(raster.width, int(cx + top) + 2)):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            r = reach(math.atan2(-dy, dx))
            d = math.hypot(dx, dy)
            if d < r:
                raster.put(channel, x, y, value * min(1.0, (r - d) / max(feather, 1e-6)))


def _damage_mask(body, kit):
    """The damage mask over the paint's UV canvas (CANVAS, CANVAS_PX, RGBA): R rust (every hole's torn rim, the
    stain under its flaked halo, its rust streaks running down and the sills' rust spots with months_abandoned), G
    soot (F: all over), B dust and chalk on upward faces (months_abandoned, paint_fade; fading over the shoulder's
    roll, so no line stands at its top) and the primer where the paint flaked (each hole's halo and the A kit's chips
    round the struck front), A burnt-off steel (F: Kit.burnt, rusting in R with weeks_since_fire). Fpv450/LayeredSurface
    layers it over the paint (design decision 5)."""
    width, height = CANVAS_PX
    raster = masks.Raster(width, height, 4)
    (cu0, cv0), (cu1, cv1) = CANVAS_MIN, CANVAS_MAX
    kx, ky = width / (cu1 - cu0), height / (cv1 - cv0)

    def px(group, side, u, v):
        off = CANVAS[group][side] if isinstance(CANVAS[group], dict) else CANVAS[group]
        return (u + off[0] - cu0) * kx, (cv1 - (v + off[1])) * ky
    rng = kits.rng_for(kit.p, "damage mask")
    seed = kit.p["seed"]
    months = kit.p["months_abandoned"] / 36.0
    for h in kit.holes:
        x, y = px("side", h.side, body.x(h.s), h.y)
        spread = rng.uniform(*HALO_SPREAD)
        def rim(a, h=h):  # the rusted lip, along the jagged rim of a cut hole
            return (h.rim_radius(a) if h.cut else h.r) * RUST_RIM * kx

        def halo(a, h=h, k=spread):  # lobed round the hole, never inside its rusted lip
            return max(h.halo_radius(a) * k, h.r * RUST_RIM * 1.2) * kx
        _blob(raster, 0, x, y, rim, 255, 1.5, None)
        # the halo's flaked paint between the rim and the halo: primer over a rust stain, mottled by a 1 cm noise
        top = max(halo(2 * math.pi * k / 16) for k in range(16)) * 1.1 + 1
        for py in range(max(0, int(y - top)), min(height, int(y + top) + 2)):
            for qx in range(max(0, int(x - top)), min(width, int(x + top) + 2)):
                dx, dy = qx + 0.5 - x, py + 0.5 - y
                a, d = math.atan2(-dy, dx), math.hypot(dx, dy)
                r_out, r_in = halo(a), rim(a)
                if r_in <= d < r_out:
                    edge = min(1.0, (r_out - d) / (0.25 * (r_out - r_in) + 1e-6))
                    mottle = 0.6 + 0.4 * _value_noise(qx / kx * 100.0, py / ky * 100.0, 0.0, seed)
                    raster.put(2, qx, py, 255 * HALO_FLAKE[0] * edge * mottle)
                    raster.put(0, qx, py, 255 * HALO_FLAKE[1] * edge)
        for _ in range(1 + int(2 * min(1.0, months * 4))):  # faint streaks down from the hole's foot
            sx = x + rng.uniform(-0.6, 0.6) * h.r * kx
            length = rng.uniform(0.05, 0.3) * min(1.0, 0.3 + months * 3)
            raster.line(0, sx, y + h.r * ky, sx + rng.uniform(-2, 2), y + (h.r + length) * ky,
                        rng.uniform(1.5, 3.0), 100)
        if not h.cut:  # a small hit flakes a chip of paint round it (measured M: the primer splashes by the arch)
            r_chip = rng.uniform(1.5, 3.0) * h.r * kx
            _blob(raster, 2, x + rng.uniform(-0.5, 0.5) * h.r * kx, y + rng.uniform(-0.5, 0.5) * h.r * ky,
                  max(r_chip, CHIP_R_M[0] * kx), 255, 1.5, [rng.uniform(0, 2 * math.pi) for _ in range(6)])
    for side in (-1, 1):  # the sills' rust: spots, more with months_abandoned
        for s in loft.samples(body.s_front, body.s_rear, 0.05):
            if rng.random() > 0.15 + 0.5 * min(1.0, months * 2):
                continue
            x, y = px("side", side, body.x(s), body.z_bot(s) + TUCK_H_M)
            cx, radius = x + rng.uniform(-0.02, 0.02) * kx, rng.uniform(0.01, 0.035) * kx
            value = rng.uniform(120, 230) * min(1.0, 0.3 + months * 3)
            _blob(raster, 0, cx, y, radius, value, 3.0, [rng.uniform(0, 2 * math.pi) for _ in range(6)])
    if kit.holes:  # chips where the paint flaked round the struck front: the wing and door tops, the bonnet's edge
        side = kit.side
        for k in range(len(kit.holes) + 1):
            phase = [rng.uniform(0, 2 * math.pi) for _ in range(6)]
            r_chip = rng.uniform(*CHIP_R_M) * kx
            if k % 2:
                s = rng.uniform(body.s_front + NOTCH_DEPTH_M, body.s_scuttle - 0.05)
                w = side * (body.w_t(s) - rng.uniform(0.0, 0.12))
                x, y = px("top", 0, body.x(s), w)
            else:
                s = rng.uniform(body.s_front + NOTCH_DEPTH_M, body.doors[1])
                yc = body.z_c(s)
                x, y = px("side", side, body.x(s), rng.uniform(yc - 0.15, yc + ROLL_R_M * math.pi / 2))
            _blob(raster, 2, x, y, r_chip, 255, 1.5, phase)
    dust = 255 * min(1.0, DUST_BASE + 0.5 * months + 0.3 * kit.p["paint_fade"])
    x0, y1 = px("top", 0, -body.L / 2, -body.W / 2)
    x1, y0 = px("top", 0, body.L / 2, body.W / 2)
    raster.rect(2, x0, y0, x1, y1, dust)
    for side in (-1, 1):  # over the shoulder's roll the film thins with the face's upward share (sin^2 of its angle)
        xa, _ = px("side", side, -body.L / 2, 0.0)
        xb, _ = px("side", side, body.L / 2, 0.0)
        for qx in range(max(0, int(xa)), min(width, int(xb) + 1)):
            s = (qx + 0.5) / kx + cu0 - CANVAS["side"][side][0] + body.L / 2
            yc = body.z_c(s)
            _, ya = px("side", side, 0.0, yc + ROLL_R_M * math.pi / 2)
            _, yb = px("side", side, 0.0, yc)
            for py in range(max(0, int(ya)), min(height, int(yb) + 1)):
                v = cv1 - (py + 0.5) / ky - CANVAS["side"][side][1]
                t = min(max((v - yc) / ROLL_R_M, 0.0), math.pi / 2)
                raster.put(2, qx, py, dust * math.sin(t) ** 2)
    if kit.F:
        raster.fill(1, 255)
        block = 4
        rusted = min(1.0, kit.p["weeks_since_fire"] / BARE_RUST_WEEKS)
        for by in range(0, height, block):
            for bx in range(0, width, block):
                p = _canvas_point(body, cu0 + (bx + block / 2) / kx, cv1 - (by + block / 2) / ky)
                if p is not None and kit.burnt(p):
                    raster.rect(3, bx, by, bx + block, by + block, 255)
                    # the bare steel rusting with weeks_since_fire, in blotches (a 5 cm noise)
                    n = 0.5 + 0.5 * _value_noise(p[0] * 20.0, p[1] * 20.0, p[2] * 20.0, seed + 1)
                    raster.rect(0, bx, by, bx + block, by + block, 255 * rusted * n * n)
    return {"role": "damage_mask", "slot": SOOT if kit.F else PAINT, "name": "damage", "uv_min": CANVAS_MIN,
            "uv_max": CANVAS_MAX, "png": raster.png()}


def _canvas_point(body, u, v):
    """The body-frame point a canvas UV stands for (roughly: on the panel group whose rectangle holds it), or None
    off the panels."""
    for side in (-1, 1):
        du, dv = CANVAS["side"][side]
        if -body.L / 2 <= u - du <= body.L / 2 and 0.2 <= v - dv <= 1.05:
            s = u - du + body.L / 2
            return body.P(s, side * body.hw(s), v - dv)
        du, dv = CANVAS["band"][side]
        if -body.L / 2 <= u - du <= body.L / 2 and body.belt - 0.01 <= v - dv <= body.H:
            s = u - du + body.L / 2
            return body.P(s, side * (body.hw(s) - ROLL_R_M), v - dv)
    du, dv = CANVAS["top"]
    if -body.L / 2 <= u - du <= body.L / 2 and -body.W / 2 <= v - dv <= body.W / 2:
        s = u - du + body.L / 2
        return body.P(s, v - dv, body.z_top(s))
    for group, s in (("front", body.s_front), ("rear", body.s_rear)):
        du, dv = CANVAS[group]
        if -body.W / 2 <= u - du <= body.W / 2 and 0.2 <= v - dv <= 1.0:
            return body.P(s, u - du, v - dv)
    return None


def _burnt_slots(mesh, kit, ranges):
    """F's surfaces after the fire: the paint's faces all on SOOT, whose damage mask's A burns them to bare steel
    where hottest (Kit.burnt, in 1.76 cm texels: task 1.4, no longer per 5 cm face), chrome and the lamps' reflectors
    dulled, the blank plates sooted."""
    rename = {PAINT: SOOT, CHROME: CHROME_DULL, LAMP: CHROME_DULL, PLATE: SOOT}
    for f0, f1 in ranges:
        for k in range(f0, f1):
            indices, normal, uvs, slot = mesh.faces[k]
            mesh.faces[k] = (indices, normal, uvs, rename.get(slot, slot))


# --- collision ------------------------------------------------------------------------------------------------------

def _quat(e0, e1, e2):
    """The unit quaternion [x, y, z, w] of the rotation whose columns are the box's local axes e0, e1, e2."""
    m = ((e0[0], e1[0], e2[0]), (e0[1], e1[1], e2[1]), (e0[2], e1[2], e2[2]))
    tr = m[0][0] + m[1][1] + m[2][2]
    if tr > 0:
        k = 0.5 / math.sqrt(tr + 1.0)
        q = ((m[2][1] - m[1][2]) * k, (m[0][2] - m[2][0]) * k, (m[1][0] - m[0][1]) * k, 0.25 / k)
    elif m[0][0] > m[1][1] and m[0][0] > m[2][2]:
        k = 2.0 * math.sqrt(1.0 + m[0][0] - m[1][1] - m[2][2])
        q = (0.25 * k, (m[0][1] + m[1][0]) / k, (m[0][2] + m[2][0]) / k, (m[2][1] - m[1][2]) / k)
    elif m[1][1] > m[2][2]:
        k = 2.0 * math.sqrt(1.0 + m[1][1] - m[0][0] - m[2][2])
        q = ((m[0][1] + m[1][0]) / k, 0.25 * k, (m[1][2] + m[2][1]) / k, (m[0][2] - m[2][0]) / k)
    else:
        k = 2.0 * math.sqrt(1.0 + m[2][2] - m[0][0] - m[1][1])
        q = ((m[0][2] + m[2][0]) / k, (m[1][2] + m[2][1]) / k, 0.25 * k, (m[1][0] - m[0][1]) / k)
    n = math.sqrt(sum(c * c for c in q))
    return [c / n for c in q]


def _cbox(centre, size, material, landable=False, axes=None):
    e0, e1, e2 = axes or _AXES
    if dot(cross(e0, e1), e2) < 0:
        e2 = scale(e2, -1)
    return {"shape": "box", "center": list(centre), "size": list(size), "rotation": _quat(e0, e1, e2),
            "material": material, "landable": landable}


def _box_on(p0, p1, side_dir, width, depth, material, landable=False):
    """A box whose top face holds the segment p0-p1 (its length), `width` across along side_dir (centred), and
    `depth` below that face along the face's inward normal (cross of the length and side directions, downward)."""
    e0 = normalize(sub(p1, p0))
    e2 = normalize(sub(side_dir, scale(e0, dot(side_dir, e0))))
    e1 = cross(e2, e0)
    if e1[1] < 0:
        e1 = scale(e1, -1)
    centre = sub(scale(add(p0, p1), 0.5), scale(e1, depth / 2))
    return _cbox(centre, (norm(sub(p1, p0)), depth, width), material, landable, (e0, e1, e2))


def collision_plan(body, kit=None, pieces=()):
    """[(name, primitive)] in asset space: the clean base's plan (_clean_plan), or a kit's (_kit_plan) moved by the
    kit's transform, then its wheels on the ground and the composed K07 pieces where they lie."""
    kit = kit or Kit(body, body.p)
    if kit.clean:
        return _clean_plan(body)
    plan = [(name, kits.transform_shape(shape, kit.T)) for name, shape in _kit_plan(body, kit)]
    plan += _kit_wheels(body, kit)
    for piece in pieces:
        for k, shape in enumerate(piece["collision"]):
            plan.append((f"{piece['kind']}_{k}", kits.transform_shape(shape, piece["rigid"])))
    return plan


def _clean_plan(body):
    """[(name, primitive)]: the clean base's collision, a union of convex primitives whose exposed faces lie on the
    skin and whose other faces lie inside a neighbour, so no invisible wall stands inside the closed car. Built to the
    audit's 5 cm, and its 2 cm within 0.3 m of the landable roof, bonnet and boot lid:

    - Lower body, level boxes from the underbody up to near the crease (the roll's cylinders hold +-3 cm of the
      crease within 5 mm, so a level top serves a crease that rises or falls 6 cm): the nose's core and per-side
      flanks (yawed with the plan taper), the middle box from the front axle to the rear arch, the tail's core and
      per-side flanks in two steps. Round each rear arch the flank is three boxes per side, above it and on the
      octagon's two upper corners, so the arch stays open: the rear tyres stand 5.6 cm inside the body side, more
      than the 5 cm a face across the arch would allow. The front arches are closed across at the body side (the
      tyres 3 cm in), each lined by a wheelhouse cylinder 2 cm over the tyre from its inner wall, which holds the
      wheelhouse within the bonnet's 2 cm zone (labelled rubber: outside the body it stands for the tyre).
    - The tops: a wedge pitched under the bonnet and one under the boot lid, the thin landable bonnet and boot lid
      boxes on them (the scuttle and the strip behind the rear screen with them).
    - The shoulder's roll: per side and stretch a cylinder on the roll's centre line, radius ROLL_R_M, flat-ended
      at the body's faces and overlapping its neighbours.
    - The greenhouse, solid with its glass in: the cabin's lower and upper cores; per side four boxes on the band
      (along the A pillar, under the level drip, under the falling drip, along the C pillar; the first three set in
      PANE_BACK_M, behind GLASS_T_M glass boxes over each pane, QA 2.1's R3); the windscreen's and
      the rear screen's wedges; the roof's three landable boxes; per side cylinders on the cap's edge roll along the
      A pillar, the level and falling drip and the C pillar.
    - Wheels: four rubber cylinders (the spec's 0.594 x 0.165 m). Bumpers: per bumper three capsules along the
      blade and a capsule per overrider. The interior (seats, dashboard, steering wheel) inside the solid cabin, for
      its mesh. The rack's four tubes (its side rails landable: the legs' touch points) and four posts."""
    plan = []
    add_ = plan.append
    sm = "sheet_metal"
    af, ar = body.axle
    s_f, s_r = body.s_front - FACE_M, body.s_rear + FACE_M
    yb = min(y for _, y in body.underbody)
    sides = ((-1, "left"), (1, "right"))
    arch_f, arch_r = ar - body.r_arch, ar + body.r_arch

    def crease_top(s0, s1):
        return (body.z_c(s0) + body.z_c(s1)) / 2

    # lower body
    nose_top = crease_top(body.s_front, af)
    add_(("nose_core", _level(body, s_f, af + OVERLAP_M, body.hw(body.s_front) - 0.08, yb, nose_top)))
    for side, tag in sides:
        add_((f"nose_{tag}", _flank_box(body, s_f, af + OVERLAP_M, side, yb, nose_top, body.s_front, af, 0.15)))
    add_(("lower_mid", _level(body, af, arch_f, MID_HALF_M, yb, LOWER_MID_TOP_M)))
    for side, tag in sides:
        for name, phi, top in (("arch_front", 135.0, None), ("arch_top", 90.0, LOWER_MID_TOP_M),
                               ("arch_rear", 45.0, None)):
            add_((f"{name}_{tag}", _arch_box(body, 1, side, phi, top)))
    tail_mid = (body.s_rs_base + body.s_rear) / 2
    for name, a, b, ends, top in (
            ("tail_a", arch_r - OVERLAP_M, tail_mid + OVERLAP_M, (arch_r, tail_mid), crease_top(arch_r, tail_mid)),
            ("tail_b", tail_mid, s_r, (tail_mid, body.s_rear), crease_top(tail_mid, body.s_rear))):
        for side, tag in sides:
            add_((f"{name}_{tag}", _flank_box(body, a, b, side, yb, top, ends[0], ends[1], 0.2)))
    add_(("tail_core", _level(body, arch_f, s_r, CORE_HALF_M, yb, body.z_c(body.s_rear) - 0.005)))
    # the tops
    add_(("front_wedge", _lid_box(body, s_f - 0.01, body.s_cowl, FRONT_WEDGE_HALF_M, LID_T_M + 0.2, -LID_T_M + 0.005, False,
                                  (body.s_front, body.s_cowl))))
    add_(("bonnet", _lid_box(body, body.s_front, body.s_cowl, body.w_t(body.s_front), LID_T_M, 0.0, True)))
    add_(("rear_wedge", _lid_box(body, body.s_rs_base - OVERLAP_M, s_r, CORE_HALF_M, LID_T_M + 0.25,
                                 -LID_T_M + 0.005, False, (body.s_rs_base, body.s_rear))))
    add_(("boot_lid", _lid_box(body, body.s_rs_base, body.s_rear, body.w_t(body.s_rear), LID_T_M, 0.0, True)))
    # the shoulder's roll
    stretches = (("roll_nose", body.s_front, af), ("roll_front", af, body.s_cowl),
                 ("roll_cabin", body.s_cowl, body.s_rs_top), ("roll_tail_a", body.s_rs_top, body.s_boot),
                 ("roll_tail_b", body.s_boot, body.s_rear))
    for side, tag in sides:
        for name, s0, s1 in stretches:
            p0 = body.P(s0, side * body.w_t(s0), body.z_c(s0 + 1e-6 if s0 == body.s_cowl else s0))
            p1 = body.P(s1, side * body.w_t(s1), body.z_c(s1))
            ends = _extend(p0, p1, _end_reach(p0, p1, s0 == body.s_front), -_end_reach(p1, p0, s1 == body.s_rear))
            add_((f"{name}_{tag}", _cylinder(*ends, ROLL_R_M, sm)))
    # the greenhouse
    for shape in _greenhouse(body, yb):
        add_(shape)
    # wheels and the front wheelhouses
    for k, a in enumerate(body.axle):
        for side, tag in sides:
            centre = body.P(a, side * body.track[k] / 2, body.z_wc)
            add_((f"wheel_{('front', 'rear')[k]}_{tag}",
                  _cylinder(sub(centre, (0.0, 0.0, body.w_tyre / 2)), add(centre, (0.0, 0.0, body.w_tyre / 2)),
                            body.r_tyre, "rubber")))
    for side, tag in sides:
        w0, w1 = body.w_in[0], body.hw(af) - 0.01
        add_((f"wheelhouse_front_{tag}", _cylinder(body.P(af, side * w0, body.z_wc), body.P(af, side * w1, body.z_wc),
                                                   body.r_tyre + WHEELHOUSE_GAP_M, "rubber")))
    # bumpers: a capsule across the end and one round each wrapped corner, and the overriders
    plan += _plan_bumpers(body)
    # interior: seats, dashboard, steering wheel (inside the solid cabin, for its mesh)
    for k, (centre, size, axes, slot, kind) in enumerate(_seat_boxes(body)):
        add_((f"seat_{k}_{kind}", _cbox(centre, size, PHYSICS[slot], False, axes)))
    d = body.preset["dashboard"]
    (s0, s1), (y0, y1) = d["s"], d["y"]
    add_(("dashboard", _cbox(body.P((s0 + s1) / 2, 0.0, (y0 + y1) / 2), (s1 - s0, y1 - y0, 2 * d["half"]), "plastic")))
    hub, axis, _, _ = _steering_frame(body)
    st = body.preset["steering"]
    add_(("steering_wheel", _cylinder(sub(hub, scale(axis, 0.025)), add(hub, scale(axis, 0.025)),
                                      st["r"] + st["tube"], "plastic")))
    plan += _plan_rack(body)
    return plan


def _end_reach(p, q, at_face):
    """How far a roll cylinder from p (toward q) grows past p: into its neighbour by 0.03 m, or at the body's
    face as far as FACE_M less what its tilted end disc gains (ROLL_R_M times its pitch's sine)."""
    if not at_face:
        return -0.03
    d = normalize(sub(q, p))
    return -(FACE_M - ROLL_R_M * abs(d[1]))


def _extend(p0, p1, before, after):
    """The segment p0-p1 grown by `before` (negative: grown) at p0 and `after` at p1 along itself."""
    d = normalize(sub(p1, p0))
    return add(p0, scale(d, before)), add(p1, scale(d, after))


def _cylinder(p0, p1, radius, material):
    """A collision cylinder of `radius` from p0 to p1 (flat ends), not landable."""
    d = sub(p1, p0)
    return {"shape": "cylinder", "center": list(scale(add(p0, p1), 0.5)), "radius": radius, "height": norm(d),
            "rotation": meta.rotation_from_y(normalize(d)), "material": material, "landable": False}


def _level(body, s0, s1, half, y0, y1):
    """A level, axis-aligned box over [s0, s1], +-half across, from y0 to y1."""
    return _cbox(body.P((s0 + s1) / 2, 0.0, (y0 + y1) / 2), (s1 - s0, y1 - y0, 2 * half), "sheet_metal")


def _bumper_axis(body, path, point, front, y):
    """The capsule's axis under a path point: BUMPER_R_M less the overriders' reach inward of the blade's face."""
    i = path.index(point)
    inward = _bumper_inward(body, path, i, front)
    d = BUMPER_R_M - BLADE_SET_M
    return body.P(point[0] + inward[0] * d, point[1] + inward[1] * d, y)


def _flank_box(body, s0, s1, side, y0, y1, a, b, depth):
    """A level box per side over [s0, s1] whose outer face is the flank's plan line through the stations a and b
    (yawed with the taper), from y0 to y1, `depth` deep."""
    pa = body.P(a, side * body.hw(a), 0.0)
    pb = body.P(b, side * body.hw(b), 0.0)
    e0 = normalize(sub(pb, pa))
    e1 = (0.0, 1.0, 0.0)
    e2 = cross(e0, e1)
    if e2[2] * side < 0:
        e2 = scale(e2, -1)  # outward
    mid_s = (s0 + s1) / 2
    t = (mid_s - a) / (b - a)
    mid = add(pa, scale(sub(pb, pa), t))
    length = (s1 - s0) / max(abs(e0[0]), 1e-9)
    centre = add(mid, add((0.0, (y0 + y1) / 2, 0.0), scale(e2, -depth / 2)))
    return _cbox(centre, (length, y1 - y0, depth), "sheet_metal", False, (e0, e1, e2))


def _arch_box(body, k, side, phi_deg, top=None, depth=ARCH_FLANK_M):
    """A box per side round arch k: its inner face tangent to the arch's circle at angle phi (degrees from the
    car's rearward axis, up positive) on the octagon round it, reaching ARCH_BOX_M out (or up to `top` for the box
    above), `depth` (ARCH_FLANK_M) in from the flank, which it follows in plan."""
    a, r = body.axle[k], body.r_arch
    phi = math.radians(phi_deg)
    n = (math.cos(phi), math.sin(phi))
    tg = (-math.sin(phi), math.cos(phi))
    half_t = r * math.tan(math.pi / 8) + 0.10
    thick = (top - body.z_wc - r) if top is not None else ARCH_BOX_M
    cs, cy = a + n[0] * (r + thick / 2), body.z_wc + n[1] * (r + thick / 2)
    slope = (body.hw(cs + 0.05) - body.hw(cs - 0.05)) / 0.1
    out = normalize((-slope, 0.0, float(side)))
    e0 = normalize((tg[0], tg[1], 0.0))
    e0 = normalize(sub(e0, scale(out, dot(e0, out))))
    e1 = cross(out, e0)
    centre = add(body.P(cs, side * body.hw(cs), cy), scale(out, -depth / 2))
    return _cbox(centre, (2 * half_t, thick, depth), "sheet_metal", False, (e0, e1, out))


def _top_mean(body, s):
    """The crowned top's mean height across at s."""
    return body.z_sh(s) + body.crown(s) * (1 - 1 / (CROWN_POWER + 1))


def _lid_box(body, s0, s1, half, depth, offset, landable, line=None):
    """A box pitched along the lid's mean surface (the line through its mean at the stations `line`, default s0
    and s1), its top `offset` above that line, `depth` deep, +-half across."""
    a, b = line or (s0, s1)
    pa, pb = body.P(a, 0.0, _top_mean(body, a)), body.P(b, 0.0, _top_mean(body, b))
    e0 = normalize(sub(pb, pa))
    p0 = add(pa, scale(e0, (s0 - a) / e0[0]))
    p1 = add(pa, scale(e0, (s1 - a) / e0[0]))
    up = (-e0[1], e0[0], 0.0)
    return _box_on(add(p0, scale(up, offset)), add(p1, scale(up, offset)), (0.0, 0.0, 1.0), 2 * half, depth,
                   "sheet_metal", landable)


def _greenhouse(body, yb):
    out = []
    sm, gl = "sheet_metal", "glass"
    out.append(("cabin_core", _level(body, body.s_cowl - OVERLAP_M, body.s_rs_base + OVERLAP_M, CABIN_HALF_M, yb,
                                     CABIN_CORE_TOP_M)))
    out.append(("cabin_upper", _level(body, body.s_ws_top - 0.03, body.s_rs_top, UPPER_HALF_M, CABIN_CORE_TOP_M - 0.03,
                                      UPPER_TOP_M)))
    s_d = body.s_drip_end
    for side, tag in ((-1, "left"), (1, "right")):
        for name, s0, s1, inset in (("band_a", body.s_cowl, body.s_ws_top, PANE_BACK_M),
                                    ("band_mid", body.s_ws_top, s_d, PANE_BACK_M),
                                    ("band_drop", s_d, body.s_rs_top, PANE_BACK_M),
                                    ("band_c", body.s_rs_top, body.s_rs_base, BAND_C_INSET_M)):
            out.append((f"{name}_{tag}", _band_box(body, side, s0, s1, inset)))
        out += _pane_boxes(body, side, tag, lambda pair: True)
    t_ws, t_rs = body.preset["windscreen_glass"][1], body.preset["rear_glass"][1]
    out.append(("windscreen", _cap_box(body, body.s_cowl, body.s_ws_top, gl, False, WEDGE_DEPTH_M, t_ws, WS_HALF_M, 0.5)))
    out.append(("rear_screen", _cap_box(body, body.s_rs_top, body.s_rs_base, gl, False, WEDGE_DEPTH_M, t_rs, RS_HALF_M,
                                        0.5)))
    for name, a, b in (("roof_front", body.s_ws_top, body.st["roof_front"]),
                       ("roof_mid", body.st["roof_front"], body.st["roof_rear"]),
                       ("roof_rear", body.st["roof_rear"], body.s_rs_top)):
        half = min(body.roof_edge(a)[0], body.roof_edge(b)[0]) - ROOF_BOX_INSET_M
        out.append((name, _cap_box(body, a, b, sm, True, 0.16, CAP_FLAT, half, ROOF_BOX_LEVEL)))
    for side, tag in ((-1, "left"), (1, "right")):
        for name, s0, s1, radius in (("pillar_a", body.s_cowl, body.s_ws_top, PILLAR_R_M),
                                     ("roof_edge", body.s_ws_top, s_d, EDGE_R_M),
                                     ("roof_edge_drop", s_d, body.s_rs_top, EDGE_R_M),
                                     ("pillar_c", body.s_rs_top, body.s_rs_base, PILLAR_R_M)):
            w0, y0, r0 = body.roof_edge(s0 + 1e-6)
            w1, y1, r1 = body.roof_edge(s1 - 1e-6)
            if name.startswith("pillar"):  # the roll shrinks toward the pillars' feet: keep the cylinder inside
                w0, w1 = w0 + r0 - max(r0, radius), w1 + r1 - max(r1, radius)
            if name == "pillar_c":  # its foot sits on the strip behind the rear screen: the cylinder's top there
                y1 = body.belt - PILLAR_R_M
            # overlaps at the joints; none where an end would stand out of the skin (the drip's end over the rear
            # screen, the C pillar's foot on the strip behind it), and the A pillar's foot set back along it so its
            # tilted end disc stays under the scuttle
            before = PILLAR_R_M * 0.7 if name == "pillar_a" else -0.02
            after = {"pillar_a": 0.02, "roof_edge": 0.02, "roof_edge_drop": 0.0, "pillar_c": -0.03}[name]
            p0, p1 = _extend(body.P(s0, side * w0, y0), body.P(s1, side * w1, y1), before, after)
            out.append((f"{name}_{tag}", _cylinder(p0, p1, radius, sm)))
    return out


def _pane_boxes(body, side, tag, intact):
    """Per side window whose pane is in (`intact(pair)`, its glass_side pair) GLASS_T_M `glass` boxes fitted to the
    pane's slab, their outer face on the pane's (QA 2.1's R3: the sheet's thin glass boxes): one per stretch of the
    window over which its top edge rises or falls at most PANE_RISE_M, so under the A pillar's slope (the vent) or
    the drip's fall (the quarter) each box's top corner stays on the band's frame over the pane."""
    out = []
    v0, v1 = WINDOW_V
    for k, ((a, b), pair) in enumerate(zip(_windows(body), _window_pairs(body))):
        if not intact(pair):
            continue

        def pt(s, v):
            return _inset(body.band_point(s, v, side), _band_normal(body, s, v, side))
        n = max(1, math.ceil((b - a) / 0.005))
        ss = [a + (b - a) * i / n for i in range(n + 1)]
        tops = [pt(s, v1)[1] for s in ss]
        pieces, start, lo, hi = [], 0, tops[0], tops[0]
        for i in range(1, n + 1):
            if max(hi, tops[i]) - min(lo, tops[i]) > PANE_RISE_M:
                pieces.append((ss[start], ss[i - 1]))
                start, lo, hi = i - 1, tops[i - 1], tops[i - 1]
            lo, hi = min(lo, tops[i]), max(hi, tops[i])
        pieces.append((ss[start], b))
        for j, (s0, s1) in enumerate(pieces):
            samples = [pt(s0 + (s1 - s0) * i / 4, v0 + (v1 - v0) * m / 2) for i in range(5) for m in range(3)]
            corners = [pt(s0, v0), pt(s1, v0), pt(s1, v1), pt(s0, v1)]
            out.append((f"band_pane_{k}_{j}_{tag}", _fitted_box(samples, corners, (0.0, 0.3, float(side)), GLASS_T_M,
                                                                "glass")))
    return out


def _band_box(body, side, s0, s1, inset=0.0, depth=BAND_DEPTH_M, below=BAND_BELOW_M, v_low=None, v_top=1.0):
    """A box per side whose outer face is the band's plane (`inset` inside it), its top edge on the band's top edge
    (or the line at fraction v_top of it) from s0 to s1, and reaching down in the plane `below` under the belt at
    both ends (or, given v_low, down to that fraction of the band), `depth` deep."""
    top0, top1 = body.band_point(s0, v_top, side), body.band_point(s1, v_top, side)
    if norm(sub(top1, top0)) < 1e-6:
        top1 = add(top0, (1e-3, 0.0, 0.0))
    e0 = normalize(sub(top1, top0))
    normal = _band_normal(body, (s0 + s1) / 2, 0.5, side)
    e1 = normalize(cross(normal, e0))
    if e1[1] > 0:
        e1 = scale(e1, -1)  # down the band
    if v_low is None:
        feet = [sub(body.band_point(s, 0.0, side), (0.0, below, 0.0)) for s in (s0, s1)]
    else:
        feet = [body.band_point(s, v_low, side) for s in (s0, s1)]
    reach = max(dot(sub(f, top0), e1) for f in feet)
    centre = add(scale(add(top0, top1), 0.5), add(scale(e1, reach / 2), scale(normal, -depth / 2 - inset)))
    return _cbox(centre, (norm(sub(top1, top0)), reach, depth), "sheet_metal", False, (e0, e1, normal))


def _cap_box(body, s0, s1, material, landable, depth, t_max, half, level):
    """A box pitched along the cap from s0 to s1, +-half across, `depth` deep, its top at `level` of the way from the
    lowest to the highest point of the cap's section over |t| <= t_max at each end (0.5 the midrange)."""
    p0 = body.P(s0, 0.0, _cap_level(body, s0, t_max, level))
    p1 = body.P(s1, 0.0, _cap_level(body, s1, t_max, level))
    return _box_on(p0, p1, (0.0, 0.0, 1.0), 2 * half, depth, material, landable)


def _cap_level(body, s, t_max, level):
    ys = [body.cap_section(s, t_max * k / 16)[1] for k in range(17)]
    return min(ys) + level * (max(ys) - min(ys))


# --- the kits' collision (design decision 7; task 1.2) ------------------------------------------------------------

def _kit_plan(body, kit):
    """[(name, primitive)] of a kit's body in the body's own frame (collision_plan moves it). As _clean_plan where the
    kit leaves the shape alone, and where it does not, the plan the audit's 5 cm and 2 cm ask of the edited shape:

    - The struck corner's tear (A): the nose's core, its flank on that side, the roll and the wedge under the bonnet
      stop at the tear's faces (its floor, its back at NOTCH_DEPTH_M, its inner edge), so the pocket stays open: it
      is a lamp's socket and the inner wing behind, not a fragment hole.
    - The bonnet's folded corner (A): the bonnet as three landable boxes round the torn-off triangle (in front of it,
      beside it, and one turned along the tear), the scuttle under the hinge, the wedge stepped down to the engine
      bay's contents under the opening (their block is its top; the pocket over it is closed by walls, _deck), the
      wing's roll under the torn side edge as steps from the inner wing out (WING_STEPS), and the flap as strips
      parallel to its hinge, each fitted to the folded sheet (_fitted_box), 4.5 cm wide so a strip's square end
      stands within 2 cm of the torn diagonal.
    - Fragment holes of 0.25 m or less stay closed in collision (the sheet's stated deviation): the panels' boxes
      run over them; their petals (kits) reach the open centre within AST-05's 5 cm, and none lies in the 2 cm zone.
    - The open cabin (any pane gone): no solid cabin. The floor, per side the walls (skin to trim, DOOR_T_M) to the
      crease, the door tops' roll as steps from the trim out (DOOR_TOP_STEPS; at the cowl, where the wing's roll steps
      down to the door's, one more on the wing's line), thin boxes on the rear wheel tubs and their inner walls, the
      firewall (the lower front's face) and, behind the rear seat, the boot from its bulkheads (its sides as the clean
      base's arch boxes); over the belt per side the band's frame round its windows and GLASS_T_M glass boxes over
      each pane still in, one 45-degree chamfer per stretch of the cap's edge roll, the roof as thin halves fitted to
      its crown (and its sag), each screen's frame and its remaining pane as thin glass boxes (the crazed windscreen
      in two across, closed over its slit); the seats, the dashboard and the steering wheel as their own shapes (rim,
      spokes, hub and column).
    - F: the front arches open as the rear ones (the tyres burnt, the rims well inside), with a core between the
      wheelhouses; the seats as their frames (the cushion's frame on the floor, the back's round its springs), the
      steering wheel's core, the dashboard's beam."""
    plan = []
    add_ = plan.append
    af, ar = body.axle
    s_f, s_r = body.s_front - FACE_M, body.s_rear + FACE_M
    yb = min(y for _, y in body.underbody)
    sides = ((-1, "left"), (1, "right"))
    arch_f, arch_r = ar - body.r_arch, ar + body.r_arch
    nose_top = (body.z_c(body.s_front) + body.z_c(af)) / 2
    notch = kit.notch
    core_half = body.hw(body.s_front) - 0.08
    # --- the nose
    if kit.F:  # the front arches open: the nose stops at them, a core runs between the wheelhouses
        fa0, fa1 = af - body.r_arch, af + body.r_arch
        add_(("nose_core", _level(body, s_f, fa0, core_half, yb, nose_top)))
        for side, tag in sides:
            add_((f"nose_{tag}", _flank_box(body, s_f, fa0, side, yb, nose_top, body.s_front, fa0, 0.15)))
            # a 12-gon round the open arch (its corners 1.2 cm off the liner: the bonnet's 2 cm zone reaches it)
            for k, phi in enumerate((150.0, 120.0, 90.0, 60.0, 30.0)):
                reach = (0.05, 0.02) if phi == 150.0 else (0.0, 0.05) if phi == 30.0 else (0.05, 0.05)
                add_((f"front_arch_{k}_{tag}", _front_arch_box(body, side, phi,
                                                               LOWER_MID_TOP_M if phi == 90.0 else None, reach)))
            # over the arch, from its top to just over the crease (under the roll), in past the wheelhouse's wall
            depth = body.hw(af) - body.w_in[0] + 0.03
            for name, s0, s1 in (("front", fa0 - OVERLAP_M, af), ("rear", af, fa1 + OVERLAP_M)):
                add_((f"front_arch_over_{name}_{tag}", _flank_box(body, s0, s1, side,
                                                                  body.z_wc + body.r_arch - 0.005,
                                                                  body.z_c(s0) + 0.03, fa0, fa1, depth)))
        add_(("axle_core", _level(body, fa0 - OVERLAP_M, fa1 + OVERLAP_M, body.w_in[0], yb, nose_top)))
        lower_from = fa1
    else:
        y_n = min(kit.notch_y(body.s_front), kit.notch_y(notch["s1"])) if notch else None
        if notch:
            side = notch["side"]
            near, far = (-core_half, notch["w_in"]) if side > 0 else (-notch["w_in"], core_half)
            add_(("nose_core", _level_w(body, s_f, af + OVERLAP_M, near, far, yb, nose_top)))
            band = (notch["w_in"], core_half) if side > 0 else (-core_half, -notch["w_in"])
            add_(("nose_core_low", _level_w(body, s_f, af + OVERLAP_M, *band, yb, y_n)))
            add_(("nose_core_back", _level_w(body, notch["s1"], af + OVERLAP_M, *band, y_n - 0.01, nose_top)))
        else:
            add_(("nose_core", _level(body, s_f, af + OVERLAP_M, core_half, yb, nose_top)))
        for side, tag in sides:
            if notch and side == notch["side"]:
                add_((f"nose_{tag}_low", _flank_box(body, s_f, af + OVERLAP_M, side, yb, y_n, body.s_front, af, 0.15)))
                add_((f"nose_{tag}_back", _flank_box(body, notch["s1"], af + OVERLAP_M, side, y_n - 0.01, nose_top,
                                                     body.s_front, af, 0.15)))
            else:
                add_((f"nose_{tag}", _flank_box(body, s_f, af + OVERLAP_M, side, yb, nose_top, body.s_front, af,
                                                0.15)))
        lower_from = af
    if kit.open:
        add_(("lower_front", _level(body, lower_from, body.s_cowl + 0.005, MID_HALF_M, yb, LOWER_MID_TOP_M)))
    else:
        add_(("lower_mid", _level(body, lower_from, arch_f, MID_HALF_M, yb, LOWER_MID_TOP_M)))
    # --- the rear arches and the tail (as the clean base); in the open cabin the arches' boxes over the cabin reach in
    # only to the quarter's trim and up to the door tops' height (the one behind, in the closed boot, as the clean
    # base's), thin boxes lie on the wheel tubs' tops in the cabin, the tail's core starts behind the rear seat
    arch_top = body.z_c(body.doors[1]) + 0.04 if kit.open else LOWER_MID_TOP_M
    arch_depth = DOOR_T_M if kit.open else ARCH_FLANK_M
    boot_depth = body.hw(body.axle[1] + body.r_arch * math.sqrt(0.5)) - body.w_in[1] + 0.01  # to the cores
    for side, tag in sides:
        for name, phi, top in (("arch_front", 135.0, None), ("arch_top", 90.0, arch_top), ("arch_rear", 45.0, None)):
            depth = boot_depth if kit.open and name == "arch_rear" else arch_depth
            add_((f"{name}_{tag}", _arch_box(body, 1, side, phi, top, depth)))
        if kit.open:
            for k, phi in enumerate(TUB_PHI_DEG):
                add_((f"arch_tub_{k}_{tag}", _tub_box(body, side, phi)))
    tail_mid = (body.s_rs_base + body.s_rear) / 2
    # F: nothing fills the arch, so no box reaches into it; A: 3 cm in, as the lean lifts the far rear corner's liner
    # off its tyre (the clean base's 5 cm would hold the liner there 5 cm deep)
    tail_a0 = arch_r if kit.F else arch_r - 0.03
    for name, a, b, ends in (("tail_a", tail_a0, tail_mid + OVERLAP_M, (arch_r, tail_mid)),
                             ("tail_b", tail_mid, s_r, (tail_mid, body.s_rear))):
        top = (body.z_c(ends[0]) + body.z_c(ends[1])) / 2
        for side, tag in sides:
            add_((f"{name}_{tag}", _flank_box(body, a, b, side, yb, top, ends[0], ends[1], 0.2)))
    s_back_top = _seat_back_station(body, body.belt)
    s_bh, s_up = _bulkhead_station(body), _upper_bulkhead_station(body)
    tail_from = body.s_rs_base - OVERLAP_M if kit.open else arch_f  # open: the boot's boxes reach it (below)
    tail_top = body.z_c(body.s_rear) - 0.005
    if kit.F:  # nothing fills the rear wheel wells: between them the core keeps inside the wheelhouses' walls, and
        # behind them it starts a centimetre into the well
        add_(("tail_core_axle", _level(body, tail_from, arch_r, body.w_in[1] - 0.01, yb, tail_top)))
        add_(("tail_core", _level(body, arch_r - 0.01, s_r, CORE_HALF_M, yb, tail_top)))
    else:
        add_(("tail_core", _level(body, tail_from, s_r, CORE_HALF_M, yb, tail_top)))
    if kit.open:
        y_liner = body.z_wc + body.r_arch
        # the boot behind the bulkheads (_cabin_shell): between the wheelhouses up to their liners from the lower
        # one, between the trims above from the upper one, behind the seat back
        add_(("boot_low", _level(body, s_bh, body.s_rs_base + OVERLAP_M, body.w_in[1] - 0.01, yb, y_liner)))
        add_(("boot_high", _level(body, s_up, body.s_rs_base, body.hw(body.s_rs_base) - 0.03, y_liner - 0.04,
                                   body.belt - 0.01)))
        # the wheelhouses' inner walls under the rear seat, ahead of the lower bulkhead (their cabin faces)
        for side, tag in sides:
            w = side * (body.w_in[1] - 0.005)
            add_((f"arch_wall_{tag}", _cbox(body.P(s_bh - ARCH_WALL_M[0] / 2, w, body.z_wc + ARCH_WALL_M[1] / 2),
                                            (ARCH_WALL_M[0], ARCH_WALL_M[1], 0.01), "sheet_metal")))
        add_(("floor", _level(body, body.s_cowl - OVERLAP_M, arch_f, MID_HALF_M - DOOR_T_M + 0.02, yb - SILL_T_M,
                              yb)))
        add_(("floor_rear", _level(body, arch_f - OVERLAP_M, s_bh, body.w_in[1] - 0.01, yb - SILL_T_M, yb)))
        d1 = body.doors[1]
        z_c = body.z_c(d1)
        for side, tag in sides:  # the doors and the quarters: skin to trim up the flank, and the roll's section
            add_((f"side_front_{tag}", _flank_box(body, body.s_cowl, d1 + OVERLAP_M, side, yb, z_c + 0.04, body.s_cowl,
                                                  d1, DOOR_T_M)))
            add_((f"side_rear_{tag}", _flank_box(body, d1, arch_f, side, yb, z_c + 0.04, d1, arch_f, DOOR_T_M)))
            # the door top's roll as two steps along it from the trim out, each outer-top corner on the arc
            # (DOOR_TOP_STEPS), so the arc keeps within 2 cm of them and nothing stands in the cabin
            for k, (out_w, y0, y1) in enumerate(DOOR_TOP_STEPS):
                add_((f"door_top_{k}_{tag}", _roll_box(body, side, body.s_cowl, s_back_top, (0.0, out_w), y0, y1,
                                                       (0.0, 0.02))))
            # at the cowl, where the wing's roll steps down 1.8 cm to the door's (COWL_STEP_M): the upper step on the
            # wing's line, inside the front roll and just past the step (the roll's end disc leans forward with its
            # pitch and leaves the step's top bare)
            out_w, y0, y1 = DOOR_TOP_STEPS[-1]
            if not (kit.flap and side == kit.flap.side):  # there the torn roll's steps reach past it
                add_((f"door_top_cowl_{tag}", _roll_box(body, side, body.s_cowl - 0.04, body.s_cowl, (0.0, out_w),
                                                        y0, y1, (0.0, COWL_STEP_M + 0.001))))
    # --- the tops
    add_tops = _kit_tops(body, kit)
    plan += add_tops
    # --- the shoulder's roll (cylinders where the cabin is closed or the stretch is not the cabin's)
    stretches = [("roll_nose", body.s_front, af), ("roll_front", af, body.s_cowl)]
    stretches += ([("roll_tail_a", body.s_rs_top, body.s_boot)] if kit.open else
                  [("roll_cabin", body.s_cowl, body.s_rs_top), ("roll_tail_a", body.s_rs_top, body.s_boot)])
    stretches.append(("roll_tail_b", body.s_boot, body.s_rear))
    flap = kit.flap
    for side, tag in sides:
        torn = flap is not None and side == flap.side  # the bonnet's side edge torn from the flap's foot to the cowl
        for name, s0, s1 in stretches:
            if notch and name == "roll_nose" and side == notch["side"]:
                s0 = notch["s1"]
            if kit.open and name == "roll_tail_a":
                s0 = max(s0, s_back_top)
            if torn and name in ("roll_nose", "roll_front"):
                s1 = min(s1, flap.s_B)
                if s1 <= s0:
                    continue
            p0 = body.P(s0, side * body.w_t(s0), body.z_c(s0 + 1e-6 if s0 == body.s_cowl else s0))
            p1 = body.P(s1, side * body.w_t(s1), body.z_c(s1))
            before = _end_reach(p0, p1, s0 == body.s_front)
            after = -_end_reach(p1, p0, s1 == body.s_rear)
            if kit.open and name == "roll_front":
                # past the cowl just so far that its forward-leaning end disc holds the door tops' front faces:
                # further, its lower half would stand in the open cabin
                after = 0.01
            if torn and s1 == flap.s_B:
                after = 0.01  # into the torn stretch's steps, not into the pocket behind the corner's foot
            if (kit.open and name == "roll_tail_a") or (notch and name == "roll_nose" and side == notch["side"]):
                before = 0.0  # nor reach into the cabin, or into the struck corner's tear
            add_((f"{name}_{tag}", _cylinder(*_extend(p0, p1, before, after), ROLL_R_M, "sheet_metal")))
        if torn:  # under the torn edge the roll's outer half alone, as steps from the inner wing out (WING_STEPS)
            cuts = [flap.s_B] + ([af] if flap.s_B < af else []) + [body.s_cowl]
            for j, (a, b) in enumerate(zip(cuts, cuts[1:])):
                # into the roll ahead (its end disc leans with its pitch) and past each other where the line kinks
                grow = (0.03 if j == 0 else 0.02, COWL_STEP_M + 0.001 if b == body.s_cowl else 0.02)
                for k, (out_w, y0, y1) in enumerate(WING_STEPS):
                    add_((f"roll_torn_{j}_{k}_{tag}", _roll_box(body, side, a, b, (0.0, out_w), y0, y1, grow)))
    # --- the greenhouse
    plan += _greenhouse_open(body, kit) if kit.open else _greenhouse(body, yb)
    plan += _plan_bumpers(body, kit)
    plan += _kit_interior(body, kit)
    plan += _plan_rack(body)
    if kit.flap:
        plan += _flap_strips(body, kit.flap)
    return plan


def _kit_wheels(body, kit):
    """The wheels on the ground: A's tyres (the flat one leaning with the body on its dropped hub) and the front
    wheelhouses' cylinders round them (the clean base's: with its tyre where the wheel stands, with the body where
    it is the flat one's), F's bare rims at their flange radius (steel, the sheet's rim cylinders)."""
    out = []
    if not kit.F:
        af = body.axle[0]
        for side, tag in ((-1, "left"), (1, "right")):
            w0, w1 = body.w_in[0], body.hw(af) - 0.01
            shape = _cylinder(body.P(af, side * w0, body.z_wc), body.P(af, side * w1, body.z_wc),
                              body.r_tyre + WHEELHOUSE_GAP_M, "rubber")
            if _corner(body, af, side) == kit.flat:
                shape = kits.transform_shape(shape, kit.T)
            out.append((f"wheelhouse_front_{tag}", shape))
    for k, a in enumerate(body.axle):
        for side, tag in ((-1, "left"), (1, "right")):
            hub = kit.hubs[_corner(body, a, side)]
            if kit.F:
                h = RIM_WIDTH_M / 2
                shape = _cylinder(sub(hub, (0.0, 0.0, h)), add(hub, (0.0, 0.0, h)), RIM_FLANGE_R_M, "steel")
                out.append((f"rim_{('front', 'rear')[k]}_{tag}", shape))
            else:
                h = body.w_tyre / 2
                if _corner(body, a, side) == kit.flat:
                    # it tilts with the body, on its dropped hub, from its tread flattened on the ground to its top:
                    # the round tyre would sink its drop into the ground the mesh stands on
                    centre = body.P(a, side * body.track[k] / 2, body.z_wc)
                    hub_y = hub[1]
                    r = (hub_y + body.r_tyre) / 2
                    shape = kits.transform_shape(_cylinder(sub(centre, (0.0, 0.0, h)), add(centre, (0.0, 0.0, h)),
                                                           r, "rubber"), kit.W)
                    shape["center"][1] += r - hub_y
                else:
                    shape = _cylinder(sub(hub, (0.0, 0.0, h)), add(hub, (0.0, 0.0, h)), body.r_tyre, "rubber")
                out.append((f"wheel_{('front', 'rear')[k]}_{tag}", shape))
    return out


def _level_w(body, s0, s1, w0, w1, y0, y1):
    """A level, axis-aligned box over [s0, s1], from w0 to w1 across, from y0 to y1."""
    return _cbox(body.P((s0 + s1) / 2, (w0 + w1) / 2, (y0 + y1) / 2), (s1 - s0, y1 - y0, w1 - w0), "sheet_metal")


def _cap_roll(body, s):
    """The cap's edge roll at s: (its centre's lateral offset, its centre's height)."""
    w, y, _ = body.roof_edge(s)
    return w, y


def _chamfer_box(body, side, s0, s1, alpha_deg, roll, radius, depth, material="sheet_metal", span=22.5):
    """A thin box per side whose outer face is the tangent plane at `alpha_deg` (from outward, up positive) to a
    roll of radius radius(body, s) about the centre line roll(body, s), from s0 to s1 (each end at its own radius),
    spanning the circumscribed polygon's side between the tangents `span` degrees either way, `depth` deep."""
    a = math.radians(alpha_deg)
    reach = max(radius(body, s0), radius(body, s1)) * math.tan(math.radians(span))

    def tangent(s):
        w, y = roll(body, s)
        r = radius(body, s)
        return body.P(s, side * (w + r * math.cos(a)), y + r * math.sin(a))
    c0, c1 = tangent(s0), tangent(s1)
    e0 = normalize(sub(c1, c0))
    n = normalize((0.0, math.sin(a), side * math.cos(a)))
    n = normalize(sub(n, scale(e0, dot(n, e0))))
    e2 = normalize(cross(n, e0))
    centre = sub(scale(add(c0, c1), 0.5), scale(n, depth / 2))
    return _cbox(centre, (norm(sub(c1, c0)), depth, 2 * reach), material, False, (e0, n, e2))


def _front_arch_box(body, side, phi_deg, top=None, reach=(0.05, 0.05)):
    """F's open front arch: as _arch_box round the front axle, but shorter along the arch (the wing ahead of it is
    0.17 m) and thinner, and reaching in from the flank to just inside the wheelhouse's inner wall (the axle's core
    covers its inner face). Along the arch it spans the 12-gon's side and `reach` past it at its ends towards the
    smaller and the larger phi: 5 cm so neighbours overlap out to their outer faces, less at the run's two ends,
    where nothing follows and a longer box would stand off the body (2 cm in front, none behind, where it would
    reach past the firewall into the footwell)."""
    a, r = body.axle[0], body.r_arch
    phi = math.radians(phi_deg)
    n = (math.cos(phi), math.sin(phi))
    tg = (-math.sin(phi), math.cos(phi))
    half_t = r * math.tan(math.pi / 12) + (reach[0] + reach[1]) / 2
    shift = (reach[1] - reach[0]) / 2  # along tg, towards the upper end
    thick = (top - body.z_wc - r) if top is not None else 0.16  # up under the box over the arch
    cs, cy = a + n[0] * (r + thick / 2), body.z_wc + n[1] * (r + thick / 2)
    slope = (body.hw(cs + 0.05) - body.hw(cs - 0.05)) / 0.1
    out = normalize((-slope, 0.0, float(side)))
    e0 = normalize((tg[0], tg[1], 0.0))
    e0 = normalize(sub(e0, scale(out, dot(e0, out))))
    e1 = cross(out, e0)
    depth = body.hw(cs) - body.w_in[0] + 0.03
    centre = add(body.P(cs, side * body.hw(cs), cy), add(scale(out, -depth / 2), scale(e0, shift)))
    return _cbox(centre, (2 * half_t, thick, depth), "sheet_metal", False, (e0, e1, out))


def _tub_box(body, side, phi_deg):
    """A thin box on the rear wheel tub's top in the open cabin: tangent to the liner's arc at phi (degrees from the
    car's rearward axis, up positive) over TUB_SPAN_DEG of it and 2 cm on, from inside the wheelhouse's inner wall to
    2 cm under the quarter's trim."""
    a, r = body.axle[1], body.r_arch
    phi = math.radians(phi_deg)
    n = (math.cos(phi), math.sin(phi), 0.0)
    e0 = (-math.sin(phi), math.cos(phi), 0.0)
    half_t = r * math.tan(math.radians(TUB_SPAN_DEG / 2)) + 0.02
    cs, cy = a + n[0] * (r + TUB_T_M / 2), body.z_wc + n[1] * (r + TUB_T_M / 2)
    w0, w1 = body.w_in[1] - 0.01, body.hw(cs) - DOOR_T_M + 0.02
    return _cbox(body.P(cs, side * (w0 + w1) / 2, cy), (2 * half_t, TUB_T_M, w1 - w0), "sheet_metal", False,
                 (e0, n, (0.0, 0.0, 1.0)))


def _roll_box(body, side, s0, s1, lateral, y0, y1, grow=(0.0, 0.0)):
    """A box along the shoulder roll's centre line from s0 to s1, straight between its ends as the roll's cylinders
    are: from lateral[0] to lateral[1] out of the line (the open cabin's trim lies on it) and from y0 to y1 over it,
    grown by grow[0] and grow[1] at its ends along itself (into the neighbouring rolls)."""
    w0, w1 = lateral

    def at(s):
        return body.P(s, side * (body.w_t(s) + (w0 + w1) / 2), body.z_c(s) + (y0 + y1) / 2)
    c0, c1 = at(s0 + 1e-6), at(s1 - 1e-6)  # inside the span: the crease steps at the cowl
    e0 = normalize(sub(c1, c0))
    c0, c1 = sub(c0, scale(e0, grow[0])), add(c1, scale(e0, grow[1]))
    up = normalize(sub((0.0, 1.0, 0.0), scale(e0, e0[1])))
    return _cbox(scale(add(c0, c1), 0.5), (norm(sub(c1, c0)), y1 - y0, w1 - w0), "sheet_metal", False,
                 (e0, up, cross(e0, up)))


def _fitted_box(points, outline, outward, thickness, material, landable=False, centred=False):
    """A thin box fitted to sheet points: its plane's normal from the `outline` ring (Newell's, turned to
    `outward`), its in-plane extent the points' bounds along the outline's first edge and across it, its outer face
    at the midrange of the points' heights over the plane (or the box centred on them, `centred`), `thickness`
    inward."""
    n = normalize(_newell(outline))
    if dot(n, outward) < 0:
        n = scale(n, -1)
    e0 = sub(outline[1], outline[0])
    e0 = normalize(sub(e0, scale(n, dot(e0, n))))
    e2 = cross(n, e0)
    origin = outline[0]
    a = [dot(sub(p, origin), e0) for p in points]
    b = [dot(sub(p, origin), e2) for p in points]
    h = [dot(sub(p, origin), n) for p in points]
    top = (max(h) + min(h)) / 2 + (thickness / 2 if centred else 0.0)
    centre = add(origin, add(scale(e0, (max(a) + min(a)) / 2), add(scale(e2, (max(b) + min(b)) / 2),
                                                                     scale(n, top - thickness / 2))))
    return _cbox(centre, (max(a) - min(a), thickness, max(b) - min(b)), material, landable, (e0, n, e2))


def _bonnet_piece(body, corners, landable, depth=LID_T_M, offset=0.0):
    """A box under the bonnet's mean plane (pitched along the car, flat across) over the plan quadrilateral
    `corners` [(s, w)] (a rectangle, square or turned), its top `offset` above the plane, `depth` deep."""
    a, b = body.s_front, body.s_cowl
    ya, yb = _top_mean(body, a), _top_mean(body, b)

    def on(s, w):
        return body.P(s, w, ya + (yb - ya) * (s - a) / (b - a) + offset)
    pts = [on(s, w) for s, w in corners]
    e0 = normalize(sub(pts[1], pts[0]))
    e2 = normalize(sub(pts[3], pts[0]))
    n = cross(e2, e0)
    if n[1] < 0:
        n = scale(n, -1)
    e2 = cross(n, e0)
    length, width = norm(sub(pts[1], pts[0])), norm(sub(pts[3], pts[0]))
    centre = sub(scale(add(pts[0], pts[2]), 0.5), scale(n, depth / 2))
    return _cbox(centre, (length, depth, width), "sheet_metal", landable, (e0, n, e2))


def _lid_box_w(body, s0, s1, w0, w1, depth, offset, landable, line=None):
    """_lid_box across [w0, w1] instead of +-half."""
    box = _lid_box(body, s0, s1, (w1 - w0) / 2, depth, offset, landable, line)
    box["center"][2] += (w0 + w1) / 2
    return box


def _kit_tops(body, kit):
    """The bonnet, the wedge under it, the boot lid and its wedge, with the struck corner's tear and the folded
    corner cut from them (see _kit_plan)."""
    out = []
    s_f, s_r = body.s_front - FACE_M, body.s_rear + FACE_M
    notch, flap = kit.notch, kit.flap
    line = (body.s_front, body.s_cowl)
    depth, offset = LID_T_M + 0.2, -LID_T_M + 0.005
    half = FRONT_WEDGE_HALF_M
    # the wedge under the bonnet, in pieces along the car: the tear's rows, the middle, the folded corner's rows
    s_mid0 = notch["s1"] if notch else s_f - 0.01
    s_mid1 = flap.s_B if flap else body.s_cowl
    if notch:
        lo, hi = (-half, notch["w_in"]) if notch["side"] > 0 else (-notch["w_in"], half)
        out.append(("wedge_front", _lid_box_w(body, s_f - 0.01, s_mid0 + OVERLAP_M, lo, hi, depth, offset, False, line)))
    # the open cabin: the pitched wedge's foot would reach past the firewall, so its last COWL_LEVEL_M is level
    s_end = body.s_cowl - COWL_LEVEL_M if kit.open else body.s_cowl
    top_cowl = _top_mean(body, body.s_cowl) + offset

    def cowl_piece(name, w0, w1):
        if kit.open:
            out.append((name, _level_w(body, s_end, body.s_cowl, w0, w1, top_cowl - depth, top_cowl)))
    out.append(("wedge_mid", _lid_box(body, s_mid0, s_end if not flap else s_mid1, half, depth, offset, False, line)))
    if not flap:
        cowl_piece("wedge_cowl", -half, half)
    if flap:
        w_a = body.w_t(flap.s_b) * flap.u_A
        lo, hi = (w_a, half) if flap.side < 0 else (-half, w_a)
        out.append(("wedge_rear", _lid_box_w(body, flap.s_B, s_end, lo, hi, depth, offset, False, line)))
        cowl_piece("wedge_cowl", lo, hi)
        s0, s1, w0, w1, top, bottom = _deck_box(body, kit)
        out.append(("wedge_deck", _cbox(body.P((s0 + s1) / 2, (w0 + w1) / 2, (top + bottom) / 2),
                                        (s1 - s0, top - bottom, w1 - w0), "sheet_metal")))
        # the bonnet round the torn-off triangle B (side edge), A (rear edge), C (the corner)
        edge = body.w_t(body.s_front) * flap.u_max
        far = -flap.side * edge
        s_b = flap.s_b
        w_a2 = w_a
        # ahead of the corner's foot and beyond the tear's far end the lids over the wedges, as the clean base's
        out.append(("bonnet_front", _bonnet_piece(body, [(body.s_front, -edge), (flap.s_B, -edge), (flap.s_B, edge),
                                                         (body.s_front, edge)], True, LID_T_M)))
        lo, hi = sorted((w_a2, far))
        out.append(("bonnet_side", _bonnet_piece(body, [(flap.s_B, lo), (body.s_cowl, lo), (body.s_cowl, hi),
                                                        (flap.s_B, hi)], True, LID_T_M)))
        # over the pocket, thin and turned along the tear (from the sheet's edge at B to A), reaching the right angle
        # R of the triangle the other two leave uncovered
        w_c = flap.side * body.w_t(flap.s_B) * flap.u_max
        b_pt, a_pt, r_pt = (flap.s_B, w_c), (s_b, w_a2), (flap.s_B, w_a2)
        length = math.hypot(a_pt[0] - b_pt[0], a_pt[1] - b_pt[1])
        t = ((a_pt[0] - b_pt[0]) / length, (a_pt[1] - b_pt[1]) / length)
        v = (r_pt[0] - b_pt[0], r_pt[1] - b_pt[1])
        along = v[0] * t[0] + v[1] * t[1]
        perp = (v[0] - along * t[0], v[1] - along * t[1])
        quad = [b_pt, a_pt, (a_pt[0] + perp[0], a_pt[1] + perp[1]), (b_pt[0] + perp[0], b_pt[1] + perp[1])]
        out.append(("bonnet_tear", _bonnet_piece(body, quad, True, LID_KIT_M)))
        lo, hi = sorted((flap.side * body.w_t(s_b) * flap.u_max, w_a2))
        out.append(("scuttle_hinge", _bonnet_piece(body, [(s_b, lo), (body.s_cowl, lo), (body.s_cowl, hi), (s_b, hi)],
                                                   False, LID_KIT_M)))
    else:
        out.append(("bonnet", _lid_box(body, body.s_front, body.s_cowl, body.w_t(body.s_front), LID_T_M, 0.0, True)))
    out.append(("rear_wedge", _lid_box(body, body.s_rs_base - OVERLAP_M, s_r, CORE_HALF_M, LID_T_M + 0.25,
                                       -LID_T_M + 0.005, False, (body.s_rs_base, body.s_rear))))
    out.append(("boot_lid", _lid_box(body, body.s_rs_base, body.s_rear, body.w_t(body.s_rear), LID_T_M, 0.0, True)))
    return out


def _flap_strips(body, flap):
    """The folded flap as strips parallel to its hinge, each a thin box fitted to the folded sheet from the side edge
    to the tear at the strip's middle (centred on the sheet, not landable)."""
    out = []
    along = flap.s_b - flap.s_B
    n = max(2, math.ceil(along / FLAP_STRIP_M))
    rot = flap.rigid()
    normal = rot.vector((0.0, 1.0, 0.0))
    for k in range(n):
        s0 = flap.s_b - along * k / n
        s1 = flap.s_b - along * (k + 1) / n
        sm = (s0 + s1) / 2
        f_mid = 1.0  # out to the tear at the strip's middle

        def pt(s, f):
            u = flap.u_C + (flap.u_tear(sm) - flap.u_C) * f
            return rot.point(body.top_point(s, u))
        outline = [pt(s0, 0.0), pt(s0, f_mid), pt(s1, f_mid), pt(s1, 0.0)]
        samples = [pt(s0 + (s1 - s0) * i / 3, j / 6) for i in range(4) for j in range(7)]
        out.append((f"flap_{k}", _fitted_box(samples, outline, normal, SHELL_T_M, "sheet_metal", centred=True)))
    return out


def _plan_bumpers(body, kit=None):
    """Per bumper three capsules along the blade and a capsule per overrider (the clean base's)."""
    out = []
    for front in (True, False):
        path, (bottom, top, depth) = _bumper_path(body, front)
        y = (bottom + top) / 2
        n = len(path)
        for a, b, tag in ((path[5], path[n - 6], "end"), (path[5], path[0], "left"), (path[n - 6], path[-1], "right")):
            pa = _bumper_axis(body, path, a, front, y)
            pb = _bumper_axis(body, path, b, front, y)
            out.append((f"bumper_{'front' if front else 'rear'}_{tag}", meta.capsule(pa, pb, BUMPER_R_M, "steel")))
        lateral, o_bottom, o_top = body.preset["overrider"]
        s = (0.0 if front else body.L) + (1.0 if front else -1.0) * 0.025
        tips = 0.02 if kit is not None and kit.F else 0.0  # F: the rubber tips burnt off
        for side, tag in ((-1, "left"), (1, "right")):
            out.append((f"overrider_{'front' if front else 'rear'}_{tag}",
                        meta.capsule(body.P(s, side * lateral, o_bottom + tips + OVERRIDER_R_M),
                                     body.P(s, side * lateral, o_top - tips - OVERRIDER_R_M), OVERRIDER_R_M, "steel")))
    return out


def _plan_rack(body):
    out = []
    if body.p["roof_rack"]:
        corners, posts = _rack_points(body)
        for k, (a, b) in enumerate(zip(corners, corners[1:] + corners[:1])):
            cap = meta.capsule(a, b, RACK_R_M, "steel")
            cap["landable"] = k in (1, 3)  # the side rails: the legs' touch points (the sheet)
            out.append((f"rack_tube_{k}", cap))
        for k, (foot, top) in enumerate(posts):
            out.append((f"rack_post_{k}", meta.capsule(foot, top, RACK_R_M, "steel")))
    return out


def _greenhouse_open(body, kit):
    """The open cabin's greenhouse (see _kit_plan): per side the band's thin frame and glass boxes over each pane still
    in (either face of a pane is met as glass, from the cabin as from outside), the cap's edge roll's chamfers, the
    roof's fitted halves, the screens' remaining panes' glass boxes."""
    out = []
    s_d = body.s_drip_end
    windows = _windows(body)
    v0, v1 = WINDOW_V
    fv = FRAME_M / body.H_band
    stretches = (("a", body.s_cowl, body.s_ws_top), ("mid", body.s_ws_top, s_d), ("drop", s_d, body.s_rs_top),
                 ("c", body.s_rs_top, body.s_rs_base))
    for side, tag in ((-1, "left"), (1, "right")):
        out += _band_open(body, side, tag)
        out += _pane_boxes(body, side, tag, lambda pair, side=side: kit.glass[side, pair] != "gone")
        for name, s0, s1 in stretches:
            out.append((f"cap_edge_{name}_{tag}", _chamfer_box(body, side, s0 + 1e-6, s1 - 1e-6, 45.0, _cap_roll,
                                                               _cap_r, SHELL_T_M)))
    # the roof: per stretch (and the sag's crease) two halves fitted to the crown
    cuts = [body.s_ws_top, body.st["roof_front"], body.st["roof_rear"], body.s_rs_top]
    if body.sag_m > 0:
        cuts = sorted(set(cuts + [body.s_crease]))
    for k, (a, b) in enumerate(zip(cuts, cuts[1:])):
        for side, tag in ((-1, "left"), (1, "right")):
            out.append((f"roof_{k}_{tag}", _roof_half(body, a, b, side)))
    t_ws, t_rs = body.preset["windscreen_glass"][1], body.preset["rear_glass"][1]
    panes = _cap_glass(body)
    for name, pane, t_max, state in (("windscreen", panes[0], t_ws, kit.windscreen),
                                     ("rear_screen", panes[1], t_rs, kit.rear_screen)):
        s0, s1, _ = pane
        # its frame round the glass or the opening: the strips along its top and foot and the pillars' flats
        out += _screen_frame(body, name, s0, s1, t_max)
        if state == "gone":
            continue
        crazed = state == "crazed_holed"
        # the pane in three along it (it narrows toward one end), the crazed one also in two across (its bulge on
        # the crown across stands too far off one plane)
        across = ((-t_max, 0.0, "_left"), (0.0, t_max, "_right")) if crazed else ((-t_max, t_max, ""),)
        for k in range(3):
            piece = (s0 + (s1 - s0) * k / 3, s0 + (s1 - s0) * (k + 1) / 3, None)
            for u0, u1, tag in across:
                out.append((f"{name}_{k}{tag}", _pane_box(body, kit, piece, (u0, u1), crazed)))
    return out


def _cap_box_over(body, s0, s1, t0, t1, side=1, material="sheet_metal", landable=False):
    """A thin box fitted to the cap from s0 to s1 over its section parameter t0..t1 on one side (t signed by side),
    square to the strip's middle line (along an edge, the cap's taper would skew a wide strip's box)."""
    def pt(s, t):
        return body.cap_point(s, side * t)
    tm = (t0 + t1) / 2
    corners = [pt(s0, tm), pt(s1, tm), pt(s1, t1), pt(s0, t1)]
    samples = [pt(s0 + (s1 - s0) * i / 4, t0 + (t1 - t0) * j / 4) for i in range(5) for j in range(5)]
    return _fitted_box(samples, corners, (0.0, 1.0, 0.0), SHELL_T_M, material, landable, True)


def _screen_frame(body, name, s0, s1, t_max):
    """The frame round a screen's glass or opening: the cap's strip between the glass and the roof or the scuttle at
    each end, and per side the pillar's flat between the glass and its edge roll (1 cm over the glass's edge)."""
    out = []
    a, b = (body.s_cowl, body.s_ws_top) if name == "windscreen" else (body.s_rs_top, body.s_rs_base)
    out.append((f"{name}_frame_a", _cap_box_over(body, a, s0 + 0.01, -CAP_FLAT, CAP_FLAT)))
    out.append((f"{name}_frame_b", _cap_box_over(body, s1 - 0.01, b, -CAP_FLAT, CAP_FLAT)))
    for side, tag in ((-1, "left"), (1, "right")):
        out.append((f"{name}_frame_{tag}", _cap_box_over(body, a, b, t_max - 0.01, CAP_FLAT, side)))
    return out


def _cap_r(body, s):
    return body.roof_edge(s)[2]


def _band_height(body, s):
    return body.z_e(s) - body.belt


def _band_abs(body, side, s0, s1, h0, h1, depth=BAND_SHELL_M):
    """A thin box over the band from s0 to s1 between the heights h0 and h1 above the belt (along the band, clamped
    to its top), its outer face fitted to the band."""
    def pt(s, h):
        top = _band_height(body, s)
        return body.band_point(s, min(h, top) / top if top > 1e-6 else 0.0, side)
    corners = [pt(s0, h0), pt(s1, h0), pt(s1, h1), pt(s0, h1)]
    samples = [pt(s0 + (s1 - s0) * i / 4, h0 + (h1 - h0) * j / 2) for i in range(5) for j in range(3)]
    return _fitted_box(samples + corners, corners, (0.0, 0.3, float(side)), depth, "sheet_metal")


def _band_triangle(body, side, s_corner, s_far, h_top, name):
    """The band under a pillar's slanted top edge, a right triangle with its right angle at (s_corner, 0) (the
    window's edge at the belt), its legs to s_far along the belt and h_top up the window's edge: covered by three
    rectangles (the one reaching the slanted edge's middle, then one into each corner triangle it leaves); the strip
    along the edge (_band_box) covers the slivers left under it."""
    ds = s_far - s_corner
    rects = [(0.0, 0.5, 0.0, 0.5), (0.0, 0.25, 0.5, 0.75), (0.5, 0.75, 0.0, 0.25)]
    out = []
    for k, (a0, a1, b0, b1) in enumerate(rects):
        s0, s1 = sorted((s_corner + ds * a0, s_corner + ds * a1))
        out.append((f"{name}_{k}", _band_abs(body, side, s0, s1, h_top * b0, h_top * b1)))
    return out


def _band_open(body, side, tag):
    """The band's frame in the open cabin (see _kit_plan), round its windows: the sill under the windows and the
    strips over them (along the A pillar's slope, the drip and its fall), the posts between the windows, the A
    pillar's triangle in front of the vent, and behind the quarter the C pillar's front part and its triangle under
    the pillar's slope, with a strip along that slope."""
    out = []
    windows = _windows(body)
    v0, v1 = WINDOW_V
    first, last = windows[0][0], windows[-1][1]
    s_d, s_c, s_b = body.s_drip_end, body.s_rs_top, body.s_rs_base
    out.append((f"band_sill_{tag}", _band_box(body, side, first, last, 0.0, BAND_SHELL_M, 0.02, None, v0)))
    for name, s0, s1 in (("a", body.s_cowl, body.s_ws_top), ("mid", body.s_ws_top, s_d), ("drop", s_d, s_c)):
        out.append((f"band_top_{name}_{tag}", _band_box(body, side, s0, s1, 0.0, BAND_SHELL_M, None, v1)))
    for k in range(len(windows) - 1):
        s0, s1 = windows[k][1], windows[k + 1][0]
        out.append((f"band_post_{k}_{tag}", _band_rect(body, side, s0, s1, v0, v1)))
    out += _band_triangle(body, side, first, body.s_cowl, _band_height(body, first), f"band_a_{tag}")
    out.append((f"band_c_front_{tag}", _band_abs(body, side, last, s_c, 0.0, _band_height(body, s_c))))
    out += _band_triangle(body, side, s_c, s_b, _band_height(body, s_c), f"band_c_{tag}")
    out.append((f"band_c_edge_{tag}", _band_box(body, side, s_c, s_b, BAND_C_INSET_M, BAND_SHELL_M, None, 0.85)))
    return out


def _band_rect(body, side, s0, s1, v0, v1, depth=BAND_SHELL_M):
    """A thin box over the band from s0 to s1 between the band fractions v0 and v1, its outer face on the band."""
    corners = [body.band_point(s0, v0, side), body.band_point(s1, v0, side), body.band_point(s1, v1, side),
               body.band_point(s0, v1, side)]
    samples = [body.band_point(s0 + (s1 - s0) * i / 4, v0 + (v1 - v0) * j / 2, side) for i in range(5) for j in range(3)]
    outward = (0.0, 0.3, float(side))
    return _fitted_box(samples + corners, corners, outward, depth, "sheet_metal")


def _roof_half(body, a, b, side):
    """A thin landable box over one half of the roof from a to b, fitted to the (sagged) crown out to the edge roll's
    67.5-degree corner (where the cap's chamfer takes over), centred on the sheet: the crown's curve stands up to
    a centimetre off a plane, and the box's faces keep it within the 2 cm either side."""
    def pt(s, f):
        w, y = body.cap_section(s, CAP_FLAT * f)
        return body.P(s, side * w, y)
    reach = []
    for s in (a, b):
        w, _, r = body.roof_edge(s)
        reach.append(w + r * math.tan(math.radians(22.5)) - 0.005)
    samples = [pt(a + (b - a) * i / 6, j / 8) for i in range(7) for j in range(9)]
    corners = [body.P(a, 0.0, pt(a, 0)[1]), body.P(b, 0.0, pt(b, 0)[1]), body.P(b, side * reach[1], pt(b, 1)[1]),
               body.P(a, side * reach[0], pt(a, 1)[1])]
    return _fitted_box(samples + corners, corners, (0.0, 1.0, 0.0), SHELL_T_M, "sheet_metal", True, True)


def _pane_box(body, kit, pane, span, crazed):
    """A thin glass box fitted to a pane that remains (its slab, the crazed one's bulge) from pane[0] to pane[1]
    over the section parameters span[0]..span[1], closed over the crazed pane's slit (as a fragment hole is)."""
    s0, s1, _ = pane
    samples = []
    for i in range(7):
        s = s0 + (s1 - s0) * i / 6
        for j in range(9):
            u = span[0] + (span[1] - span[0]) * j / 8
            if crazed:
                w = math.copysign(_flat_reach(body, s) * abs(u) / CAP_FLAT, u)
                p, _ = _pane_surface(body, kit)(s * kit.ws_k, w)
            else:
                p = _inset(body.cap_point(s, u), _cap_normal(body, s, u))
            samples.append(p)
    # the outline along the pane's centre line and across it, so the box is square to the car
    corners = [samples[4], samples[6 * 9 + 4], samples[6 * 9 + 8], samples[8]]
    return _fitted_box(samples, corners, (0.0, 1.0, 0.0), SHELL_T_M, "glass", False, True)


def _kit_interior(body, kit):
    """The interior's collision for the open cabin (its shapes are exposed): A's seats, dashboard and the steering
    wheel's rim, spokes, hub and column; F's seat frames (the burnt cushions' frames, the backs round their springs),
    the steering wheel's core and the dashboard's beam. The closed cabin keeps the clean base's (inside its cores)."""
    out = []
    if not kit.open:
        for k, (centre, size, axes, slot, kind) in enumerate(_seat_boxes(body)):
            out.append((f"seat_{k}_{kind}", _cbox(centre, size, PHYSICS[slot], False, axes)))
        d = body.preset["dashboard"]
        (s0, s1), (y0, y1) = d["s"], d["y"]
        out.append(("dashboard", _cbox(body.P((s0 + s1) / 2, 0.0, (y0 + y1) / 2), (s1 - s0, y1 - y0, 2 * d["half"]),
                                       "plastic")))
        hub, axis, _, _ = _steering_frame(body)
        st = body.preset["steering"]
        out.append(("steering_wheel", _cylinder(sub(hub, scale(axis, 0.025)), add(hub, scale(axis, 0.025)),
                                                st["r"] + st["tube"], "plastic")))
        return out
    st = body.preset["steering"]
    hub, axis, e1, e2 = _steering_frame(body)
    tube = STEEL_CORE_R_M if kit.F else st["tube"]
    material = "steel" if kit.F else "plastic"
    ring = [add(hub, add(scale(e1, st["r"] * math.cos(2 * math.pi * k / 8)), scale(e2, st["r"] * math.sin(2 * math.pi * k / 8))))
            for k in range(8)]
    for k in range(8):
        out.append((f"steering_rim_{k}", meta.capsule(ring[k], ring[(k + 1) % 8], tube, material)))
    for k, side in enumerate((-1, 1)):
        tip = add(hub, add(scale(e1, side * st["r"] * math.cos(math.radians(10))),
                           scale(e2, -st["r"] * math.sin(math.radians(10)))))
        out.append((f"steering_spoke_{k}", meta.capsule(hub, tip, 0.006 if kit.F else 0.01, material)))
    d = body.preset["dashboard"]
    column_end = body.P(d["s"][1] - 0.02, st["w"], d["y"][0] + 0.06)
    out.append(("steering_column", meta.capsule(sub(hub, scale(axis, 0.02)), column_end, 0.02 if kit.F else 0.025,
                                                material)))
    if kit.F:
        for k, (kind, centre, size, axes, _) in enumerate(_burnt_seats(body)):
            out.append((f"seat_{k}_{kind}_frame", _cbox(centre, size, "steel", False, axes)))
        out.append(("dash_beam", _cylinder(*_dash_beam(body), DASH_BEAM_R_M, "steel")))
        return out
    out.append(("steering_hub", _cylinder(sub(hub, scale(axis, 0.01)), add(hub, scale(axis, 0.03)), 0.045, "plastic")))
    for k, (centre, size, axes, slot, kind) in enumerate(_seat_boxes(body)):
        out.append((f"seat_{k}_{kind}", _cbox(centre, size, PHYSICS[slot], False, axes)))
    d = body.preset["dashboard"]
    (s0, s1), (y0, y1) = d["s"], d["y"]
    out.append(("dashboard", _cbox(body.P((s0 + s1) / 2, 0.0, (y0 + y1) / 2), (s1 - s0, y1 - y0, 2 * d["half"]),
                                   "plastic")))
    return out


PLAN_GROUPS = (  # the plan's groups by name prefix, first match (the build's line and the tests pin them)
    ("lower body", ("nose_", "lower_", "arch_", "tail_", "axle_core", "front_arch_", "floor", "side_", "door_top_",
                    "boot_low", "boot_high")),
    ("tops", ("front_wedge", "wedge_", "bonnet", "rear_wedge", "boot_lid", "scuttle_")),
    ("shoulder rolls", ("roll_", "chamfer_")),
    ("greenhouse", ("cabin_", "band_", "windscreen", "rear_screen", "roof_", "pillar_", "cap_edge_")),
    ("wheels", ("wheel_", "wheelhouse_", "rim_")),
    ("bumpers", ("bumper_", "overrider_")),
    ("interior", ("seat_", "dashboard", "steering", "dash_beam")),
    ("rack", ("rack_",)),
    ("flap", ("flap_",)),
    ("scatter", ("scatter_",)),
    ("bead wires", ("bead_",)),
)


def plan_group(name):
    return next(group for group, prefixes in PLAN_GROUPS if name.startswith(prefixes))


def _plan_groups(plan):
    counts = {}
    for name, _ in plan:
        group = plan_group(name)
        counts[group] = counts.get(group, 0) + 1
    return ", ".join(f"{group} {counts[group]}" for group, _ in PLAN_GROUPS if group in counts)


def _kit_summary(body, kit):
    parts = [f"state {kit.state}", f"burst side {'left' if kit.side < 0 else 'right'}"]
    if kit.holes:
        cut = [h for h in kit.holes if h.cut]
        parts.append(f"{len(kit.holes)} holes ({len(cut)} cut, {len(kit.holes) - len(cut)} decal-only; diameters "
                     + ", ".join(f"{h.d:.3f}" for h in kit.holes) + " m)")
    if kit.notch:
        parts.append("the struck corner's lamp tear")
    if kit.flap:
        parts.append(f"bonnet flap folded {abs(math.degrees(kit.flap.angle)):.1f} deg")
    if kit.ws_hole:
        parts.append("windscreen crazed with its slit")
    if kit.flat:
        settled = _settle(body, kit)
        parts.append(f"flat {kit.flat}: hub {body.z_wc - kit.hubs[kit.flat][1]:.3f} m down, the body on its springs "
                     + " / ".join(f"{c} {settled[c] * 1000:+.1f}" for c in CORNERS)
                     + f" mm (centre {settled['centre'] * 1000:+.1f}), across {settled['across']:.2f} deg, "
                     f"along {settled['along']:.2f} deg, tilt {settled['tilt']:.2f} deg")
    if kit.F:
        parts.append(f"on its rims {kit.p['ride_drop']:.3f} m down, roof sag {body.sag_m:.3f} m")
    return "; ".join(parts)


def _settle(body, kit):
    """How the body settled over its flat tyre: per corner the body's point over its hub moved up (m), the same at
    the hubs' centre, and its lean across and along and its whole tilt (degrees)."""
    out = {}
    pts = {}
    for k, a in enumerate(body.axle):
        for side, tag in ((-1, "left"), (1, "right")):
            pts[f"{('front', 'rear')[k]}_{tag}"] = body.P(a, side * body.track[k] / 2, body.z_wc)
    pts["centre"] = tuple(sum(p[i] for p in pts.values()) / 4 for i in range(3))
    for name, p in pts.items():
        out[name] = kit.T.point(p)[1] - p[1]
    out["across"] = abs(math.degrees(math.asin(kit.T.vector((0.0, 0.0, 1.0))[1])))
    out["along"] = abs(math.degrees(math.asin(kit.T.vector((1.0, 0.0, 0.0))[1])))
    out["tilt"] = math.degrees(math.acos(min(1.0, kit.T.vector((0.0, 1.0, 0.0))[1])))
    return out


def _plan_summary(plan):
    kinds = {}
    for _, shape in plan:
        kinds[shape["shape"]] = kinds.get(shape["shape"], 0) + 1
    return ", ".join(f"{n} {k}{'es' if k == 'box' and n > 1 else ('s' if n > 1 else '')}" for k, n in sorted(kinds.items()))


# --- wind and snags ------------------------------------------------------------------------------------------------

GREENHOUSE_POROSITY = {"A": 0.35, "F": 0.55}  # the sheet's shared wind plan: side glass gone (A), burnt (F)


def _wind(body, kit):
    """The shared plan: the lower body (porosity 0) and the greenhouse (0 with its glass in; 0.35 in A with the side
    glass gone, 0.55 in F), moved with the body."""
    hw = body.hw(body.st["roof_front"])
    lower = _cbox(body.P((body.s_front + body.s_rear) / 2, 0.0, (body.z_bot(1.5) + body.belt) / 2),
                  (body.s_rear - body.s_front, body.belt - body.z_bot(1.5), 2 * hw), "sheet_metal")
    s0, s1 = body.s_cowl, body.s_rs_base
    gh = _cbox(body.P((s0 + s1) / 2, 0.0, (body.belt + body.H) / 2), (s1 - s0, body.H - body.belt,
                                                                       2 * body.w_t(body.st["roof_front"])), "glass")
    porosity = GREENHOUSE_POROSITY[kit.state] if kit.open else 0.0
    return [{k: v for k, v in kits.transform_shape(shape, kit.T).items() if k not in ("material", "landable")}
            | {"porosity": p} for shape, p in ((lower, 0.0), (gh, porosity))]


def _snags(body, kit, pieces=()):
    """The snag records: the clean base's (the rack, the bumpers' ends, the wipers) and F's seat springs (hooks)
    moved with the body; F's rim flanges (edges) at the hubs; the composed pieces' where they lie. Each names the
    physics material of the member it follows (PHYSICS of its slot; lib.meta's snag rule), as the kits' torn edges do."""
    out = [kits.transform_snag(s, kit.T) for s in _base_snags(body, wipers=not kit.F)]
    if kit.F:
        for _, _, _, _, springs in _burnt_seats(body):
            for pts in springs:
                out.append(kits.transform_snag({"kind": "hook", "points": [list(p) for p in pts],
                                                "radius_m": SPRING_R_M, "material": PHYSICS[WIRE]}, kit.T))
        for corner, hub in sorted(kit.hubs.items()):
            side = -1 if corner.endswith("left") else 1
            for e in (-1, 1):
                ring = [[hub[0] + RIM_FLANGE_R_M * math.cos(2 * math.pi * k / 24),
                         hub[1] + RIM_FLANGE_R_M * math.sin(2 * math.pi * k / 24), hub[2] + e * side * RIM_WIDTH_M / 2]
                        for k in range(25)]
                out.append({"kind": "edge", "points": ring, "radius_m": 0.003, "material": PHYSICS[CHROME_DULL]})
    for piece in pieces:
        out += [kits.transform_snag(s, piece["rigid"]) for s in piece["snag"]]
    return out


def _base_snags(body, wipers=True):
    """The clean base's snags: the rack's tubes and posts and the bumpers' ends (edges), the wiper arms (hooks)."""
    out = []
    if body.p["roof_rack"]:
        corners, posts = _rack_points(body)
        for a, b in zip(corners, corners[1:] + corners[:1]):
            out.append({"kind": "edge", "points": [list(a), list(b)], "radius_m": RACK_R_M, "material": PHYSICS[RACK]})
        for foot, top in posts:
            out.append({"kind": "edge", "points": [list(foot), list(top)], "radius_m": RACK_R_M,
                        "material": PHYSICS[RACK]})
    for front in (True, False):
        path, (bottom, top, depth) = _bumper_path(body, front)
        y = (bottom + top) / 2
        for end in (path[:4], path[-4:]):  # the blade's wrapped ends
            out.append({"kind": "edge", "points": [list(body.P(s, w, y)) for s, w in end], "radius_m": 0.006,
                        "edge_radius_m": 0.002, "material": PHYSICS[CHROME]})
    for pivot, tip in _wiper_arms(body) if wipers else ():  # tubes of the rack's steel (_lod)
        out.append({"kind": "hook", "points": [list(pivot), list(tip)], "radius_m": 0.005, "material": PHYSICS[RACK]})
    return out
