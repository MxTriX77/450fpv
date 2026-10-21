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

from lib import loft, meta
from lib.geometry import Mesh
from lib.skeleton import add, cross, dot, norm, normalize, scale, sub

# The car stands on the ground: its tyres' and front wheelhouses' cylinders reach a few centimetres under y = 0,
# where the terrain is (lib.audit's planted rule).
PLANTED = True

# --- slots and materials ------------------------------------------------------------------------------------------
PAINT, UNDER, CHROME, GLASS, TYRE, SEAT, VINYL, LAMP, LENS, RACK, PLATE = (
    "paint", "under", "chrome", "glass", "tyre", "seat", "vinyl", "lamp", "lens", "rack", "plate")
PHYSICS = {PAINT: "sheet_metal", UNDER: "sheet_metal", CHROME: "steel", GLASS: "glass", TYRE: "rubber",
           SEAT: "fabric", VINYL: "plastic", LAMP: "glass", LENS: "plastic", RACK: "steel", PLATE: "sheet_metal"}
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
        # roof rack (the sheet: 1.3 x 1.0 m, rails 0.10 m over the roof, 20 mm tubes): s from-to, half-width, posts' s
        "rack": (1.59, 2.89, 0.50, (1.65, 2.83)),
        # interior (assumed, the class; the drawing's seat outlines for s)
        "front_seat": {"s": (1.50, 1.97), "y": (0.30, 0.55), "w": 0.33, "half": 0.24,
                       "back": ((1.97, 0.52), (2.11, 1.04)), "back_t": 0.10},
        "rear_seat": {"s": (2.32, 2.74), "y": (0.28, 0.52), "w": 0.0, "half": 0.62,
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
TYRE_SEGMENTS = (32, 16, 8)
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
                       "effect": "chroma loss and chalk on upward faces (with the damage mask, task 1.2)"},
        "state": {"type": "str", "range": ", ".join(STATES), "default": "A", "effect": "which kit runs"},
        "severity": {"type": "float", "range": (0.0, 1.0), "default": 0.5,
                     "effect": "the kit's master dial; 0 is the clean base"},
        "burst_dir.azimuth": {"type": "float", "range": (0.0, 360.0), "default": 0.0,
                              "effect": "the burst's bearing (deg): hole side, petals, fold, debris bias"},
        "burst_dir.elevation": {"type": "float", "range": (-10.0, 60.0), "default": 0.0,
                                "effect": "the burst's height (deg)"},
        "frag_holes": {"type": "int", "range": (0, 60), "default": 16, "effect": "fragment holes on the burst side"},
        "frag_hole_size": {"type": "float", "range": (0.01, 0.25), "default": 0.03,
                           "effect": "the holes' median diameter (log-uniform)"},
        "rust_halo": {"type": "float", "range": (1.2, 2.5), "default": 1.8, "effect": "halo size per hole"},
        "months_abandoned": {"type": "int", "range": (0, 36), "default": 6,
                             "effect": "dust, streaks, fade, litter, flat tyres"},
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
    elif flat["state"] == "F":
        problems.append("'state' = 'F' is not built yet: the F kit is task 1.2's")
    kit = [k for k, v in NEUTRAL.items() if flat[k] != v]
    if kit:
        problems.append("not built yet: the A kit and the damage mask are task 1.2's, so the clean base needs "
                        + ", ".join(f"'{k}' = {NEUTRAL[k]!r}" for k in kit))
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
            return f * wf, ze + r + (crown - r) * (1 - f * f)
        a = math.pi / 2 * (t - CAP_FLAT) / (1 - CAP_FLAT)
        return wf + r * math.sin(a), ze + r * math.cos(a)

    def cap_point_w(self, s, w):
        """The cap's point at lateral w on its flat part (|w| up to the edge roll)."""
        ze, we = self.z_e(s), self.w_e(s)
        crown = max(0.0, self.z_top(s) - ze)
        wf = we - min(EDGE_R_M, crown)
        return self.cap_point(s, math.copysign(CAP_FLAT * min(abs(w) / wf, 1.0), w))

    def roof_edge(self, s):
        """(lateral, height, radius) of the roof's edge roll's centre line at s."""
        ze, we = self.z_e(s), self.w_e(s)
        r = min(EDGE_R_M, max(0.0, self.z_top(s) - ze))
        return we - r, ze, r

# --- build --------------------------------------------------------------------------------------------------------

QUALITY = (  # per LOD: s step, roll steps, thickness on, details on, wheel segments
    {"step": 0.05, "roll": ROLL_STEPS, "thick": True, "detail": 2, "seg": TYRE_SEGMENTS[0]},
    {"step": 0.10, "roll": 3, "thick": False, "detail": 1, "seg": TYRE_SEGMENTS[1]},
    {"step": 0.25, "roll": 1, "thick": False, "detail": 0, "seg": TYRE_SEGMENTS[2]},
)


def build(params):
    body = Body(params)
    lods = [_lod(body, q) for q in QUALITY] + [_box_hull(body)]
    lod = params["lod"]
    plan = collision_plan(body)
    return {
        "lods": lods,
        "switch_distances_m": [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"], lod["lod3_switch_m"]],
        "thin_wire": [False] * 4,
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": s, "physics": PHYSICS[s]} for s in sorted({slot for m in lods for slot in m.slots()})],
        "collision": [shape for _, shape in plan],
        "gaps": [],
        "wind_volumes": _wind(body),
        "holes": [],
        "snag": _snags(body),
        "lines": [f"collision plan: {len(plan)} primitives ({_plan_summary(plan)})"],
    }


def _lod(body, q):
    mesh = Mesh()
    for side in (-1, 1):
        _body_side(mesh, body, side, q)
        _wheelhouses(mesh, body, side, q)
        _band(mesh, body, side, q)
    _tops(mesh, body, q)
    _ends(mesh, body, q)
    _underbody(mesh, body, q)
    _cap(mesh, body, q)
    _glass(mesh, body, q)
    _bumpers(mesh, body, q)
    _front_details(mesh, body, q)
    _rear_details(mesh, body, q)
    if q["detail"] >= 2:
        for pivot, tip in _wiper_arms(body):
            _tube(mesh, [pivot, tip], 0.005, 4, RACK)
    for k, a in enumerate(body.axle):
        for side in (-1, 1):
            _wheel(mesh, body, a, side * body.track[k] / 2, side, q)
    if q["detail"] >= 1:
        _interior(mesh, body, q)
    if body.p["roof_rack"] and q["detail"] >= 1:
        _rack(mesh, body, q)
    return mesh


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


def _section_levels(body, q):
    """The section's vertical fractions (from the tuck's top to the crease), with the door gap's rows."""
    y1 = body.z_bot(body.doors[0]) + TUCK_H_M
    yc = body.z_c(body.doors[0])
    g = DOOR_GAP_M / 2
    f_lo, f_hi = (body.sill - g - y1) / (yc - y1), (body.sill + g - y1) / (yc - y1)
    base = [0.0, f_lo, f_hi, 1.0] + ([0.3, 0.55, 0.8] if q["thick"] else [0.5])
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


def _body_side(mesh, body, side, q):
    levels, f_lo, f_hi = _section_levels(body, q)
    rows = _s_rows(body, body.s_front, body.s_rear, q["step"])
    grid, uvs = [], []
    for s in rows:
        sec = _section(body, s, q, levels)
        grid.append([body.P(s, side * w, y) for w, y, _ in sec])
        uvs.append([(body.x(s), v) for _, _, v in sec])
    j_lo = 1 + levels.index(round(f_lo, 9))
    j_hi = 1 + levels.index(round(f_hi, 9))
    d0, d1, d3 = body.doors
    g = DOOR_GAP_M / 2
    mask = [[not (d0 - g - 1e-9 <= rows[i] and rows[i + 1] <= d3 + g + 1e-9 and j >= j_lo)
             for j in range(len(grid[0]) - 1)] for i in range(len(rows) - 1)]
    outward = _side_outward(body, side)
    t = GAUGE_M if q["thick"] else 0.0
    loft.sheet(mesh, grid, uvs, PAINT, outward, t, mask)
    for a, b in ((d0 + g, d1 - g), (d1 + g, d3 - g)):  # the doors
        door_rows = [s for s in rows if a - 1e-9 <= s <= b + 1e-9]
        dg, du = [], []
        for s in door_rows:
            sec = _section(body, s, q, levels)[j_hi:]
            dg.append([body.P(s, side * w, y) for w, y, _ in sec])
            du.append([(body.x(s), v) for _, _, v in sec])
        loft.sheet(mesh, dg, du, PAINT, outward, t)
        if q["detail"] >= 2:  # the handle near the door's rear edge, under the crease
            yc = body.z_c(b)
            _box(mesh, body.P(b - 0.09, side * (body.hw(b) + 0.003), yc - 0.045), (0.12, 0.025, HANDLE_PROUD_M), CHROME)


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


def _top_sheet(mesh, body, s0, s1, u_max, q):
    rows = loft.samples(s0, s1, q["step"])
    us = [u * u_max for u in _u_levels(q)]
    grid = [[body.top_point(s, u) for u in us] for s in rows]
    uvs = [[(p[0], p[2]) for p in row] for row in grid]
    loft.sheet(mesh, grid, uvs, PAINT, lambda p: (0.0, 1.0, 0.0), GAUGE_M if q["thick"] else 0.0)


def _tops(mesh, body, q):
    g = DOOR_GAP_M / 2
    gu = DOOR_GAP_M / body.w_t(body.s_front)
    _top_sheet(mesh, body, body.s_front, body.s_scuttle - g, 1 - gu, q)          # bonnet
    _top_sheet(mesh, body, body.s_scuttle + g, body.s_cowl, 1.0, q)              # scuttle
    _top_sheet(mesh, body, body.s_rs_base, body.s_boot - g, 1.0, q)              # behind the rear screen
    _top_sheet(mesh, body, body.s_boot + g, body.s_rear, 1 - gu, q)              # boot lid


def _end_outline(body, s, q):
    """The closed outline of the body's section at s (right half up, over the top, left half down)."""
    levels, _, _ = _section_levels(body, q)
    right = [(w, y) for w, y, _ in _section(body, s, q, levels)]
    top = [(u * body.w_t(s), body.z_sh(s) + body.crown(s) * (1 - abs(u) ** CROWN_POWER))
           for u in reversed(_u_levels(q)) if abs(u) < 1.0]
    left = [(-w, y) for w, y in reversed(right)]
    return right + top + left


def _ends(mesh, body, q):
    for s, sign in ((body.s_front, -1.0), (body.s_rear, 1.0)):
        outline = _end_outline(body, s, q)
        outer = [mesh.vertex(body.P(s, w, y)) for w, y in outline]
        mesh.face(outer, (sign, 0.0, 0.0), PAINT, [(w, y) for w, y in outline])
        if q["thick"]:
            inner = [mesh.vertex(body.P(s - sign * GAUGE_M, w, y)) for w, y in outline]
            mesh.face(inner, (-sign, 0.0, 0.0), PAINT, [(w, y) for w, y in outline])


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


def _band(mesh, body, side, q):
    windows = _windows(body)
    v0, v1 = WINDOW_V
    fv = FRAME_M / body.H_band
    extra = [e for a, b in windows for e in (a - FRAME_M, a, b, b + FRAME_M)]
    extra += [body.s_a_top, body.s_drip_end, body.s_c_top]
    rows = loft.samples(body.s_cowl, body.s_rs_base, q["step"], extra)
    vs = [0.0, v0 - fv, v0, v1, v1 + fv, 1.0] if q["detail"] else [0.0, v0, v1, 1.0]
    grid = [[body.band_point(s, v, side) for v in vs] for s in rows]
    uvs = [[(p[0], p[1]) for p in row] for row in grid]
    mask, slots = [], []
    for i in range(len(rows) - 1):
        sm = (rows[i] + rows[i + 1]) / 2
        mrow, srow = [], []
        for j in range(len(vs) - 1):
            vm = (vs[j] + vs[j + 1]) / 2
            inside = any(a < sm < b for a, b in windows) and v0 < vm < v1
            frame = any(a - FRAME_M < sm < b + FRAME_M for a, b in windows) and v0 - fv < vm < v1 + fv
            if q["detail"] == 0:
                mrow.append(True)
                srow.append(GLASS if inside else PAINT)
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


def _cap(mesh, body, q):
    panes = _cap_glass(body)
    ft = 0.03
    extra_s = [e for a, b, _ in panes for e in (a - FRAME_M, a, b, b + FRAME_M)]
    extra_s += [body.s_ws_top, body.s_rs_top, body.s_drip_end, body.st["roof_front"], body.st["roof_rear"]]
    rows = loft.samples(body.s_cowl, body.s_rs_base, q["step"], extra_s)
    us = _cap_levels(q, [u for _, _, um in panes for u in ((um, um + ft) if q["detail"] else (um,))])
    grid = [[body.cap_point(s, u) for u in us] for s in rows]
    uvs = [[(p[0], p[2]) for p in row] for row in grid]
    mask, slots = [], []
    for i in range(len(rows) - 1):
        sm = (rows[i] + rows[i + 1]) / 2
        mrow, srow = [], []
        for j in range(len(us) - 1):
            um = abs((us[j] + us[j + 1]) / 2)
            inside = any(a < sm < b and um < u for a, b, u in panes)
            frame = any(a - FRAME_M < sm < b + FRAME_M and um < u + ft for a, b, u in panes)
            if q["detail"] == 0:
                mrow.append(True)
                srow.append(GLASS if inside else PAINT)
            else:
                mrow.append(not inside)
                srow.append(CHROME if frame and not inside else PAINT)
        mask.append(mrow)
        slots.append(srow)
    loft.sheet(mesh, grid, uvs, PAINT, lambda p: (0.0, 1.0, 0.0), GAUGE_M if q["thick"] else 0.0, mask, slots=slots)


def _glass(mesh, body, q):
    """The intact panes, GLASS_T_M slabs GLASS_INSET_M behind their openings; from LOD2 on they are the hull's."""
    if q["detail"] == 0:
        return
    thick = GLASS_T_M if q["thick"] else 0.0
    v0, v1 = WINDOW_V
    for side in (-1, 1):
        for a, b in _windows(body):
            rows = loft.samples(a, b, q["step"])
            grid = [[_inset(body.band_point(s, v, side), _band_normal(body, s, v, side)) for v in (v0, v1)]
                    for s in rows]
            loft.sheet(mesh, grid, [[(p[0], p[1]) for p in r] for r in grid], GLASS,
                       lambda p, side=side: (0.0, 0.3, float(side)), thick)
    for a, b, um in _cap_glass(body):
        rows = loft.samples(a, b, q["step"])
        us = [u for u in _cap_levels(q, [um]) if abs(u) <= um + 1e-9]
        grid = [[_inset(body.cap_point(s, u), _cap_normal(body, s, u)) for u in us] for s in rows]
        loft.sheet(mesh, grid, [[(p[0], p[2]) for p in r] for r in grid], GLASS, lambda p: (0.0, 1.0, 0.0), thick)

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


def _bumpers(mesh, body, q):
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
            for y in (o_top - 0.02, o_bottom + 0.02):
                _box(mesh, body.P(s, w, y), (0.05, 0.04, 0.055), TYRE)

def _bumper_outward(body, front):
    def outward(p):
        c = body.P(body.s_front + 0.3 if front else body.s_rear - 0.3, 0.0, p[1])
        return (p[0] - c[0], 0.0, p[2] - c[2])
    return outward


def _front_details(mesh, body, q):
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
        a, b, y0, y1 = body.preset["indicator"]
        _box(mesh, body.P(s - 0.007, side * (a + b) / 2, (y0 + y1) / 2), (0.014, y1 - y0, b - a), LENS,
             uvs=lambda p, n: LAMP_UV["amber"])
    if q["detail"] >= 1:  # the front plate on the blade's middle, its top at the blade's top
        _plate(mesh, body, BLADE_SET_M - PLATE_OFF_M - PLATE_T_M / 2, body.preset["plates"][0] - PLATE_M[1] / 2)


def _rear_details(mesh, body, q):
    s = body.s_rear
    a, b, y0, y1 = body.preset["tail_lamp"]
    for side in (-1, 1):
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


def _wheel(mesh, body, s_axle, w, side, q):
    centre = body.P(s_axle, w, body.z_wc)
    axis = (0.0, 0.0, float(side))  # outboard
    seg = q["seg"]
    profile = _tyre_profile(body)
    if q["detail"] == 0:
        profile = [profile[0], profile[4], profile[5], profile[-1]]
    _revolve(mesh, centre, axis, profile, seg, TYRE, core=((RIM_R_M + body.r_tyre) / 2, 0.0), flatten_y=0.0,
             uv_scale=2 * math.pi * body.r_tyre)
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


def _interior(mesh, body, q):
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
    corners, posts = _rack_points(body)
    sides = 8 if q["detail"] >= 2 else 4
    for a, b in zip(corners, corners[1:] + corners[:1]):
        _tube(mesh, [a, b], RACK_R_M, sides, RACK)
    for foot, top in posts:
        _tube(mesh, [foot, top], RACK_R_M, sides, RACK)


# --- LOD3 ----------------------------------------------------------------------------------------------------------

def _box_hull(body):
    mesh = Mesh()
    hw = body.hw(body.s_ws_top)
    _box(mesh, body.P((body.s_front + body.s_rear) / 2, 0.0, (body.z_bot(1.0) + body.belt) / 2),
         (body.s_rear - body.s_front, body.belt - body.z_bot(1.0), 2 * hw), PAINT)
    _box(mesh, body.P((body.s_ws_top + body.s_rs_top) / 2, 0.0, (body.belt + body.H) / 2),
         (body.s_rs_top - body.s_ws_top, body.H - body.belt, 2 * body.w_e(body.s_ws_top)), GLASS)
    for k, a in enumerate(body.axle):
        for side in (-1, 1):
            _box(mesh, body.P(a, side * body.track[k] / 2, body.z_wc), (2 * body.r_tyre, 2 * body.z_wc, body.w_tyre), TYRE)
    return mesh


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


def collision_plan(body):
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
      (along the A pillar, under the level drip, under the falling drip, along the C pillar); the windscreen's and
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
    for front in (True, False):
        path, (bottom, top, depth) = _bumper_path(body, front)
        y = (bottom + top) / 2
        n = len(path)
        for a, b, tag in ((path[5], path[n - 6], "end"), (path[5], path[0], "left"), (path[n - 6], path[-1], "right")):
            pa = _bumper_axis(body, path, a, front, y)
            pb = _bumper_axis(body, path, b, front, y)
            add_((f"bumper_{'front' if front else 'rear'}_{tag}", meta.capsule(pa, pb, BUMPER_R_M, "steel")))
        lateral, o_bottom, o_top = body.preset["overrider"]
        s = (0.0 if front else body.L) + (1.0 if front else -1.0) * 0.025  # the overriders' middle
        for side, tag in sides:
            add_((f"overrider_{'front' if front else 'rear'}_{tag}",
                  meta.capsule(body.P(s, side * lateral, o_bottom + OVERRIDER_R_M),
                               body.P(s, side * lateral, o_top - OVERRIDER_R_M), OVERRIDER_R_M, "steel")))
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
    if body.p["roof_rack"]:
        corners, posts = _rack_points(body)
        for k, (a, b) in enumerate(zip(corners, corners[1:] + corners[:1])):
            cap = meta.capsule(a, b, RACK_R_M, "steel")
            cap["landable"] = k in (1, 3)  # the side rails: the legs' touch points (the sheet)
            add_((f"rack_tube_{k}", cap))
        for k, (foot, top) in enumerate(posts):
            add_((f"rack_post_{k}", meta.capsule(foot, top, RACK_R_M, "steel")))
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


def _arch_box(body, k, side, phi_deg, top=None):
    """A box per side round arch k: its inner face tangent to the arch's circle at angle phi (degrees from the
    car's rearward axis, up positive) on the octagon round it, reaching ARCH_BOX_M out (or up to `top` for the box
    above), ARCH_FLANK_M in from the flank, which it follows in plan."""
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
    centre = add(body.P(cs, side * body.hw(cs), cy), scale(out, -ARCH_FLANK_M / 2))
    return _cbox(centre, (2 * half_t, thick, ARCH_FLANK_M), "sheet_metal", False, (e0, e1, out))


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
        for name, s0, s1, inset in (("band_a", body.s_cowl, body.s_ws_top, 0.0), ("band_mid", body.s_ws_top, s_d, 0.0),
                                    ("band_drop", s_d, body.s_rs_top, 0.0),
                                    ("band_c", body.s_rs_top, body.s_rs_base, BAND_C_INSET_M)):
            out.append((f"{name}_{tag}", _band_box(body, side, s0, s1, inset)))
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


def _band_box(body, side, s0, s1, inset=0.0):
    """A box per side whose outer face is the band's plane (`inset` inside it), its top edge on the band's top edge
    from s0 to s1, and reaching down in the plane BAND_BELOW_M under the belt at both ends, BAND_DEPTH_M deep."""
    top0, top1 = body.band_point(s0, 1.0, side), body.band_point(s1, 1.0, side)
    if norm(sub(top1, top0)) < 1e-6:
        top1 = add(top0, (1e-3, 0.0, 0.0))
    e0 = normalize(sub(top1, top0))
    normal = _band_normal(body, (s0 + s1) / 2, 0.5, side)
    e1 = normalize(cross(normal, e0))
    if e1[1] > 0:
        e1 = scale(e1, -1)  # down the band
    feet = [sub(body.band_point(s, 0.0, side), (0.0, BAND_BELOW_M, 0.0)) for s in (s0, s1)]
    reach = max(dot(sub(f, top0), e1) for f in feet)
    centre = add(scale(add(top0, top1), 0.5), add(scale(e1, reach / 2), scale(normal, -BAND_DEPTH_M / 2 - inset)))
    return _cbox(centre, (norm(sub(top1, top0)), reach, BAND_DEPTH_M), "sheet_metal", False, (e0, e1, normal))


def _cap_box(body, s0, s1, material, landable, depth, t_max, half, level):
    """A box pitched along the cap from s0 to s1, +-half across, `depth` deep, its top at `level` of the way from the
    lowest to the highest point of the cap's section over |t| <= t_max at each end (0.5 the midrange)."""
    p0 = body.P(s0, 0.0, _cap_level(body, s0, t_max, level))
    p1 = body.P(s1, 0.0, _cap_level(body, s1, t_max, level))
    return _box_on(p0, p1, (0.0, 0.0, 1.0), 2 * half, depth, material, landable)


def _cap_level(body, s, t_max, level):
    ys = [body.cap_section(s, t_max * k / 16)[1] for k in range(17)]
    return min(ys) + level * (max(ys) - min(ys))


def _plan_summary(plan):
    kinds = {}
    for _, shape in plan:
        kinds[shape["shape"]] = kinds.get(shape["shape"], 0) + 1
    return ", ".join(f"{n} {k}{'es' if k == 'box' and n > 1 else ('s' if n > 1 else '')}" for k, n in sorted(kinds.items()))


# --- wind and snags ------------------------------------------------------------------------------------------------

def _wind(body):
    """The shared plan: the lower body (porosity 0) and the greenhouse (0 with its glass in)."""
    hw = body.hw(body.st["roof_front"])
    lower = _cbox(body.P((body.s_front + body.s_rear) / 2, 0.0, (body.z_bot(1.5) + body.belt) / 2),
                  (body.s_rear - body.s_front, body.belt - body.z_bot(1.5), 2 * hw), "sheet_metal")
    s0, s1 = body.s_cowl, body.s_rs_base
    gh = _cbox(body.P((s0 + s1) / 2, 0.0, (body.belt + body.H) / 2), (s1 - s0, body.H - body.belt,
                                                                       2 * body.w_t(body.st["roof_front"])), "glass")
    return [{k: v for k, v in shape.items() if k not in ("material", "landable")} | {"porosity": 0.0}
            for shape in (lower, gh)]


def _snags(body):
    out = []
    if body.p["roof_rack"]:
        corners, posts = _rack_points(body)
        for a, b in zip(corners, corners[1:] + corners[:1]):
            out.append({"kind": "edge", "points": [list(a), list(b)], "radius_m": RACK_R_M})
        for foot, top in posts:
            out.append({"kind": "edge", "points": [list(foot), list(top)], "radius_m": RACK_R_M})
    for front in (True, False):
        path, (bottom, top, depth) = _bumper_path(body, front)
        y = (bottom + top) / 2
        for end in (path[:4], path[-4:]):  # the blade's wrapped ends
            out.append({"kind": "edge", "points": [list(body.P(s, w, y)) for s, w in end], "radius_m": 0.006,
                        "edge_radius_m": 0.002})
    for pivot, tip in _wiper_arms(body):
        out.append({"kind": "hook", "points": [list(pivot), list(tip)], "radius_m": 0.005})
    return out
