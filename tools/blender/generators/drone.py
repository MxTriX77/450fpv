"""drone: the Svinoriz 10 Opto, D01 (p5-drone design 3; DRN-01, DRN-02, DRN-03, REAL-05): D-050's simple drone, the
fiber coil beneath the frame and a plain plastic bottle strapped on top, its frame, arms, legs and packs as letter T
shows them (D-043).

Every dimension and position comes from the component spec, assets-src/drone/drone.json (DRN-03), which the
physics' mass build reads too; the params (assets-src/drone/<variant>.json, PARAMS below) hold only the look: the arm
plates' outline, the plates' gauges, the props' planform, the spool's ends and ribs, the canister's cone, nozzle
and hangers, the bottle's neck, cap and shoulder, the payload and payout settings, the straps, the camera's place
and the LODs. The spec's parts are mass shapes; each is drawn as follows.

- **Origin and axes:** the spec's body frame (D-019): x starboard, y up, z forward, from the arm-plane centre.
- **Motors** (motor_*): the can is the part's cylinder exactly, base to top: a narrower base (motor.base_*), the
  stator's copper in the gap, the bell with a chamfered rim and, at LOD0, six vents in its top; the top of the part
  is the shaft's boss, inside the prop hub.
- **Props** (motors[].position_m, props.diameter_m, props.blades, props.pitch_m, motors[].spin): the hub centred
  on the hub point, the blades' mid-plane there; each blade twisted to the prop's pitch (capped at
  BLADE_MAX_TWIST_DEG at the root), swept back towards its rounded tip, whose apex lies exactly at props.diameter_m / 2
  from the shaft, its leading edge ahead and up for the motor's spin. The mass part (a disc of the blade's mean
  inertia) is not drawn.
- **Arms** (arm_*): a plate of arm.thickness_m centred on the part's height, along the part's axis from its inner
  end to the motor's axis, where a round motor mount of arm.mount_width_m closes it.
- **Plates** (plates): the part's plan, drawn as the sheet's sandwich: the bottom plate under the arms, the top
  plate on them, black standoffs and the upper plate, the part's height (34 mm, the frame's published) from the
  bottom plate's underside. The spec's box starts at the arms' underside instead; the drawn stack starts a bottom
  plate lower (the arms sandwiched, published construction).
- **Electronics** (inside the electronics part): the flight-controller and ESC stack on nylon spacers, the camera
  with its lens at camera.lens_*, the capacitor and the fiber module at the rear, all between the top and upper
  plates (the upper plate's top is the bottle's); the motor leads along the arms.
- **Packs** (pack_*): the part's box exactly, its long edges rounded (pack.corner_r_m), held by `straps` hook-and-loop
  straps each, which pass through the bottom plate; leads to yellow connectors at the rear, plugged.
- **Coil** (coil; legs_off.moved): T's spool, the wound pack on its bobbin, inside the canister along the part's axis
  (its v the axis' normal towards the ground): the fiber pack's diameter the part's at full spool, shrinking with
  `payout` to the core as its volume leaves; the light grey rear cap with its recessed three-spoke hub and the line's
  hole, the black front hoop; the part's length end to end. The canister hides it; it is drawn for the player's pack.
  The pack (its rear face from the cap's rim, the ribbed winding, the hoop's rear face to its rim: every vertex the
  player moves as the line pays out, p7d-tether 1.14's CoilPack) is its own mesh per LOD, `coil_pack` under the
  metadata's `readable`, so the importer makes only it readable and the rest of the drone keeps no CPU copy.
- **Canister** (canister; the class's, docs/reference-notes/spool-exit-and-rear.md): the part's body exactly (its
  diameter, its length end to end, its axis tilted rear-down), a lid seam near its front face; aft of the body the
  black cone (canister.cone_m) to its collar, then the aluminium nozzle (canister.nozzle_*) with its bore and a mouth
  flared over a rounded lip (lip_r_m), whose tip is the line's exit (`fiber_exit`, the metadata's port: the tip and
  the direction down the axis aft); two black printed hangers from the bottom plate's underside down to the body,
  each with a cable tie round the body through it, and the fiber's connector on the body's top with its lead to the
  plate (the fixed end, to the transceiver).
- **Bottle** (payload; legs_off.moved; payload_kg): a plain plastic bottle (D-050) sized by its water. The part is
  the water as a solid cylinder at the mass the spec's rule gives (the mass build's uniform cube-root scale of its
  size, its face toward the arm plane kept); the bottle's body is the part's diameter, centred on the part, and base
  to cap it is as long as it must be to hold the part's volume below its cap (bottle_length): a rounded base, a
  shoulder, the neck's support ring and the cap; strapped rigidly to its plate by two straps through the plate;
  absent at 0 kg. The `bottle` layout draws it alone at the spec's own mass, its origin where it meets its plate
  (bottle_pivot), so one prefab serves every mass of the setup: the player scales it uniformly there by
  (m / the spec's mass)^(1/3), as the mass build scales the part (D01_bottle; the legs variants draw none).
- **Legs** (leg_*), **clamps** (clamp_*), **T-joints** (tjoint_*), **skids** (skid_*): the leg tubes and skids the
  parts' cylinders exactly (the skids hollow, open-ended, a lime band on the port skid), the clamps the parts' size
  turned along the arm and trimmed to clear the packs, the T-joints the parts' plan, flat-bottomed on the ground.
  Each corner's leg parts are their own meshes per LOD (p7g-legs 1.5: the player draws each leg from PoseFrame, set,
  broken, gone or hanging), under the metadata's `readable` beside the coil pack, LEG_KINDS: the tube cut where the
  sim cuts it, at its clamp's edge (clamp_edge), into the stub in the clamp (stub_<c>, open at the edge, which the
  clamp's underside covers) and the free length below (leg_<c>, its end there the 10/8 mm tube's), its T-joint
  (tjoint_<c>, none at LOD3) and its half of the skid (skid_<c>), the skid cut midway between its two bores
  (skid_cut, the sim's half-skids' inner end), the band with its half. They keep asset space, the drone's origin,
  as every LOD does: the sim's own pivots (DroneSpec.BuildLegDamage's Clamp and Bore, body frame) turn them.

Collision (gallery noclip only: the flight's shapes come from drone.json through the core) follows the drawn parts;
material ids follow the object sheet (the carbon parts `carbon_composite`, signed off by physics in p7b-airframe).
LODs drop detail by the pixel: at the feed's 935 px/rad (CatalogImporter) a feature of s metres is one pixel at
935 s metres away, so LOD1 (3 m) drops what is under 3 mm (vents, ribs, wing nuts, tie tails), LOD2 (10 m) what is
under 1 cm (leads, standoffs, nuts, bolts), LOD3 (35 m) what is under 4 cm (keeping boxes and prisms of the big
parts), and the drone is culled where its 0.706 m span falls under two pixels (330 m).
"""
import json
import math
import os
import random

from lib import meta
from lib.geometry import Mesh
from lib.solids import (AXES, Frame, add, box, circle, cross, dot, lathe, normalize, palette_uv, prism, ribbon,
                        scale, sub, tube)

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
CORNERS = ("rr", "fr", "rl", "fl")  # Betaflight's motor order, the spec's motors[] and legs.parts
LODS = 4
PACK = "coil_pack"  # the coil's pack: its part and its readable meshes' kind (CoilShape.D01PackMesh in the player)
# Each corner's leg parts, their readable meshes' kinds in the sim's corner order (p7g-legs 1.5; the player's names)
LEG_KINDS = tuple(f"{kind}_{corner}" for corner in CORNERS for kind in ("stub", "leg", "tjoint", "skid"))
# The palette recipes' columns, left to right (assets-src/materials/drone_<slot>.json); test_drone checks the order.
PALETTES = {
    "carbon": ("plate", "edge", "tube"),
    "metal": ("bell", "copper", "stator", "zinc", "anodised"),
    "prop": ("blade",),
    "plastic": ("black", "print_grey", "hub_recess", "fiber", "pack_wrap", "lime", "connector", "pcb", "lens"),
    "strap": ("black", "purple"),
    "lead": ("red", "black"),
    "bottle": ("body", "cap"),
}
# The object sheet's material ids (Physics role).
PHYSICS = {"carbon": "carbon_composite","metal": "steel", "prop": "plastic", "plastic": "plastic", "strap": "fabric",
           "lead": "cable", "bottle": "plastic"}
BLADE_MAX_TWIST_DEG = 35.0  # a blade's root turns no steeper than this into the hub (assumed: molded props)
# A blade's chord along the radius, as a share of prop.chord_m at r / R (the sheet: widest near a third of the
# radius, a slender scimitar; assumed shape); the tip's own chord closes it, then a round tip.
CHORD_TABLE = ((0.0, 0.55), (0.15, 0.80), (0.30, 1.0), (0.50, 0.93), (0.70, 0.75), (0.85, 0.55))
SWEEP_FROM = 0.35  # the mid-chord starts sweeping back here (r / R) and reaches prop.sweep_m at the tip
TE_M = 0.0003  # the trailing edge's own thickness
STRAP_W_M, STRAP_T_M = 0.020, 0.0015  # hook-and-loop straps (assumed: 20 mm, the sheet's T)
STRAP_EDGE = 0.1  # the purple edge band's share of a strap's width each side (T)
TIE_W_M, TIE_T_M = 0.0045, 0.0012  # a cable tie's band (assumed: a 4.5 mm tie)
HANGER_HALF_M = 0.022  # a printed hanger's half-width across the canister (assumed)
PIGTAIL_R_M = 0.0015  # the fiber's fixed end, a 3 mm jacketed pigtail (assumed)
MOUTH_BAND_M = 0.0015  # the nozzle mouth's rim band before its flare (assumed)
SEAM_M = 0.001  # the canister lid's seam, its width and depth (assumed)
EXIT = "fiber_exit"  # the line's exit: the metadata's port at the nozzle's tip
NECK_M, RING_M = 0.010, 0.003  # a PET bottle's neck under its cap and its support ring's lip (assumed)
LEAD_R_M = 0.00225  # 10 AWG silicone with its jacket, Ø4.5 mm (the sheet)
XT90_M = (0.040, 0.011, 0.021)  # a mated XT90 pair: length, height, width (published class)

PARAMS = {
    "spec": {"type": "str", "range": "a component spec under assets-src, relative to the repo root",
             "default": "assets-src/drone/drone.json", "effect": "every dimension (DRN-03)"},
    "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 1, "effect": "the cable ties' turn"},
    "layout": {"type": "str", "range": "legs_on | legs_off | bottle", "default": "legs_on",
               "effect": "legs_off: the spec's legs_off build (legs, T-joints and skids off, coil and payload moved); "
                         "bottle: the bottle and its straps alone, its origin where it sits on its plate"},
    "arm.thickness_m": {"type": "float", "range": (0.005, 0.008), "default": 0.006, "effect": "the arm plate"},
    "arm.width_m": {"type": "float", "range": (0.020, 0.032), "default": 0.025, "effect": "the arm strip's width"},
    "arm.mount_width_m": {"type": "float", "range": (0.028, 0.040), "default": 0.032,
                          "effect": "the round motor mount's diameter at the motor's axis"},
    "plates.bottom_m": {"type": "float", "range": (0.002, 0.004), "default": 0.003, "effect": "bottom plate gauge"},
    "plates.top_m": {"type": "float", "range": (0.0015, 0.004), "default": 0.002, "effect": "top plate gauge"},
    "plates.upper_m": {"type": "float", "range": (0.002, 0.004), "default": 0.003, "effect": "upper plate gauge"},
    "plates.corner_r_m": {"type": "float", "range": (0.0, 0.02), "default": 0.010, "effect": "plates' corners"},
    "motor.base_d_m": {"type": "float", "range": (0.025, 0.037), "default": 0.034, "effect": "the motor's base"},
    "motor.base_h_m": {"type": "float", "range": (0.003, 0.010), "default": 0.006, "effect": "the base's height (T)"},
    "prop.hub_d_m": {"type": "float", "range": (0.015, 0.025), "default": 0.019, "effect": "the prop hub"},
    "prop.hub_h_m": {"type": "float", "range": (0.005, 0.010), "default": 0.008, "effect": "the hub's thickness"},
    "prop.chord_m": {"type": "float", "range": (0.022, 0.030), "default": 0.026, "effect": "a blade's widest chord"},
    "prop.tip_chord_m": {"type": "float", "range": (0.004, 0.012), "default": 0.008,
                         "effect": "the chord where the round tip starts"},
    "prop.sweep_m": {"type": "float", "range": (0.0, 0.02), "default": 0.012,
                     "effect": "how far the tip's mid-chord lags the radial line (scimitar)"},
    "prop.thickness_m": {"type": "float", "range": (0.002, 0.006), "default": 0.004,
                         "effect": "a blade's thickness at the root, a quarter of it at the tip"},
    "prop.rest_deg.rr": {"type": "float", "range": (0.0, 360.0), "default": 30.0, "effect": "blade angle at rest"},
    "prop.rest_deg.fr": {"type": "float", "range": (0.0, 360.0), "default": 100.0, "effect": "blade angle at rest"},
    "prop.rest_deg.rl": {"type": "float", "range": (0.0, 360.0), "default": 150.0, "effect": "blade angle at rest"},
    "prop.rest_deg.fl": {"type": "float", "range": (0.0, 360.0), "default": 60.0, "effect": "blade angle at rest"},
    "coil.wound_m": {"type": "float", "range": (0.08, 0.18), "default": 0.15, "effect": "the fiber pack's length"},
    "coil.hoop_len_m": {"type": "float", "range": (0.005, 0.03), "default": 0.015, "effect": "the front hoop"},
    "coil.hoop_d_m": {"type": "float", "range": (0.06, 0.11), "default": 0.090, "effect": "the front hoop"},
    "coil.cap_d_m": {"type": "float", "range": (0.04, 0.09), "default": 0.072, "effect": "the rear cap"},
    "coil.core_d_m": {"type": "float", "range": (0.02, 0.06), "default": 0.039, "effect": "the empty spool"},
    "coil.hole_d_m": {"type": "float", "range": (0.005, 0.02), "default": 0.011, "effect": "the line's hole"},
    "coil.rib_pitch_m": {"type": "float", "range": (0.003, 0.012), "default": 0.0075,
                         "effect": "the winding's ribs (T's 6 mm at 1.25x)"},
    "coil.rib_depth_m": {"type": "float", "range": (0.0, 0.002), "default": 0.0006, "effect": "the ribs' depth"},
    "canister.cone_m": {"type": "float", "range": (0.02, 0.09), "default": 0.047,
                        "effect": "the cone's length from the body to its collar"},
    "canister.collar_d_m": {"type": "float", "range": (0.015, 0.04), "default": 0.024, "effect": "the cone's end"},
    "canister.nozzle_m": {"type": "float", "range": (0.04, 0.10), "default": 0.070,
                          "effect": "the nozzle's length from the collar to its tip"},
    "canister.nozzle_d_m": {"type": "float", "range": (0.010, 0.025), "default": 0.018, "effect": "the nozzle's tube"},
    "canister.bore_d_m": {"type": "float", "range": (0.004, 0.020), "default": 0.012, "effect": "the nozzle's bore"},
    "canister.lip_r_m": {"type": "float", "range": (0.001, 0.010), "default": 0.004,
                         "effect": "the mouth's rounded lip, the radius the line turns over"},
    "canister.mouth_d_m": {"type": "float", "range": (0.012, 0.040), "default": 0.026, "effect": "the flared mouth"},
    "canister.lid_m": {"type": "float", "range": (0.005, 0.05), "default": 0.020,
                       "effect": "the lid's seam from the body's front face"},
    "canister.hanger_front_m": {"type": "float", "range": (0.0, 0.3), "default": 0.040,
                                "effect": "the front hanger's station from the body's front face"},
    "canister.hanger_rear_m": {"type": "float", "range": (0.0, 0.3), "default": 0.170,
                               "effect": "the rear hanger's station from the body's front face"},
    "canister.hanger_len_m": {"type": "float", "range": (0.01, 0.04), "default": 0.020,
                              "effect": "a hanger's length fore and aft"},
    "payout": {"type": "float", "range": (0.0, 1.0), "default": 0.0,
               "effect": "the share of the line paid out: the fiber pack's radius (DRN-05)"},
    "payload_kg": {"type": "float", "range": (0.0, 5.0), "default": 1.0,
                   "effect": "0 leaves it off; else the spec's body scaled by the cube root of the mass ratio"},
    "bottle.neck_d_m": {"type": "float", "range": (0.018, 0.040), "default": 0.025,
                        "effect": "the bottle's neck (a 28 mm PET finish)"},
    "bottle.cap_d_m": {"type": "float", "range": (0.020, 0.045), "default": 0.030, "effect": "the bottle's cap"},
    "bottle.cap_h_m": {"type": "float", "range": (0.008, 0.025), "default": 0.015, "effect": "the cap's height"},
    "bottle.shoulder_m": {"type": "float", "range": (0.02, 0.12), "default": 0.06,
                          "effect": "the shoulder's length from the body to the neck"},
    "bottle.base_r_m": {"type": "float", "range": (0.002, 0.03), "default": 0.012, "effect": "the base's rounding"},
    "pack.corner_r_m": {"type": "float", "range": (0.0, 0.0107), "default": 0.008,
                        "effect": "the packs' long edges, at most a 21700 cell's radius"},
    "straps": {"type": "int", "range": (2, 3), "default": 2, "effect": "straps per pack"},
    "skid_band": {"type": "bool", "range": "true | false", "default": True,
                  "effect": "the lime band on the port skid (T)"},
    "camera.lens_y_m": {"type": "float", "range": (0.0, 0.03), "default": 0.015,
                        "effect": "the lens's height: between the top and upper plates"},
    "camera.lens_z_m": {"type": "float", "range": (0.09, 0.14), "default": 0.115, "effect": "the lens's front"},
    "lod.lod1_switch_m": {"type": "float", "range": (0.5, 20.0), "default": 3.0, "effect": "LOD1 from here"},
    "lod.lod2_switch_m": {"type": "float", "range": (2.0, 60.0), "default": 10.0, "effect": "LOD2 from here"},
    "lod.lod3_switch_m": {"type": "float", "range": (5.0, 150.0), "default": 35.0, "effect": "LOD3 from here"},
    "lod.cull_m": {"type": "float", "range": (50.0, 2000.0), "default": 330.0, "effect": "culled beyond"},
}


# --- the spec ----------------------------------------------------------------------------------------------------

def value(x):
    """A labelled number's value, or the number itself."""
    return x["value"] if isinstance(x, dict) and "value" in x else x


def load_spec(params):
    path = os.path.join(REPO_ROOT, params["spec"])
    with open(path, encoding="utf-8") as f:
        return json.load(f)


class Layout:
    """The spec's parts as drawn for one variant: centres after the legs-off move and the payload's scale, the parts
    a variant drops gone. Every number is the spec's; nothing here is a look param but the payload's mass."""

    def __init__(self, spec, params):
        self.spec = spec
        legs_off = params["layout"] == "legs_off"
        removed = set(spec["legs_off"]["removed"]) if legs_off else set()
        moved = {k: value(v) for k, v in spec["legs_off"]["moved"].items()} if legs_off else {}
        self.parts = {}
        for p in spec["parts"]:
            if p["name"] in removed:
                continue
            part = {k: value(v) for k, v in p.items()}
            if p["name"] in moved:
                part["position_m"] = moved[p["name"]]
            self.parts[p["name"]] = part
        pay = self.parts.get("payload")
        self.payload_scale = 0.0
        if pay:
            if params["payload_kg"] <= 0:
                del self.parts["payload"]
            else:  # the part stays the spec's; the bottle drawn from it is scaled (scaled_payload, bottle_pivot)
                self.payload_scale = (params["payload_kg"] / pay["mass_kg"]) ** (1.0 / 3.0)
        self.hubs = [value(m["position_m"]) for m in spec["motors"]]
        self.spins = [1.0 if m["spin"] == "cw" else -1.0 for m in spec["motors"]]
        self.prop_d = value(spec["props"]["diameter_m"])
        self.blades = int(value(spec["props"]["blades"]))
        self.pitch = value(spec["props"]["pitch_m"])
        self.legs_off = legs_off
        self.bottle_only = params["layout"] == "bottle"

    def scaled_payload(self):
        """The payload part as the mass build sets it at this variant's payload (DroneSpec.ScaleAwayFromArmPlane):
        its size scaled uniformly by payload_scale, its face toward the arm plane kept. The bottle is drawn from the
        spec's part and scaled the same way about that face (bottle_pivot)."""
        pay = dict(self.parts["payload"])
        s, (x, y, z) = self.payload_scale, pay["position_m"]
        pay["position_m"] = [x, y + math.copysign(1.0, y) * (s - 1) * pay["diameter_m"] / 2, z]
        pay["diameter_m"] *= s
        pay["length_m"] *= s
        return pay

    def has(self, name):
        return name in self.parts

    def part(self, name):
        return self.parts[name]


def box_extent(part):
    c, s = part["position_m"], part["size_m"]
    return [c[a] - s[a] / 2 for a in range(3)], [c[a] + s[a] / 2 for a in range(3)]


# --- paints ------------------------------------------------------------------------------------------------------

def paint(slot, colour):
    columns = PALETTES[slot]
    return (slot, palette_uv(columns.index(colour), len(columns)))


CARBON, CARBON_EDGE, TUBE = paint("carbon", "plate"), paint("carbon", "edge"), paint("carbon", "tube")
BELL, COPPER, STATOR, ZINC, ANODISED = (paint("metal", c) for c in ("bell", "copper", "stator", "zinc", "anodised"))
BLADE = paint("prop", "blade")
BLACK, PRINT, RECESS, FIBER = (paint("plastic", c) for c in ("black", "print_grey", "hub_recess", "fiber"))
WRAP, LIME, YELLOW, PCB, LENS = (paint("plastic", c) for c in ("pack_wrap", "lime", "connector", "pcb", "lens"))
STRAP, STRAP_EDGE_PAINT = paint("strap", "black"), paint("strap", "purple")
RED, LEAD_BLACK = paint("lead", "red"), paint("lead", "black")
BODY, CAP = paint("bottle", "body"), paint("bottle", "cap")


# --- params ------------------------------------------------------------------------------------------------------

def check(params):
    """Cross-parameter rules: the layout, the spec present with the build asked for, the payload inside the spec's
    setting, the spool's ends inside its length, the motor mount at least the arm's width, the LODs in order."""
    problems = []
    if params["layout"] not in ("legs_on", "legs_off", "bottle"):
        return [f"'layout' must be legs_on, legs_off or bottle, got {params['layout']!r}"]
    try:
        spec = load_spec(params)
    except (OSError, ValueError) as e:
        return [f"'spec' {params['spec']!r} does not load: {e}"]
    lo, hi = value(spec["payload"]["mass_min_kg"]), value(spec["payload"]["mass_max_kg"])
    if not lo <= params["payload_kg"] <= hi:
        problems.append(f"'payload_kg' = {params['payload_kg']} is outside the spec's setting [{lo}, {hi}]")
    coil = next(p for p in spec["parts"] if p["name"] == "coil")
    c = params["coil"]
    if c["wound_m"] + c["hoop_len_m"] >= value(coil["length_m"]) - 0.01:
        problems.append("the coil's wound length and front hoop leave no rear cap inside its length")
    if not c["core_d_m"] < value(coil["diameter_m"]) < c["hoop_d_m"]:
        problems.append("the coil's core, its full pack (the spec's diameter) and its hoop must grow in that order")
    if not c["hole_d_m"] < c["core_d_m"] < c["cap_d_m"] < value(coil["diameter_m"]):
        problems.append("the line's hole, the core, the rear cap and the full pack must grow in that order")
    problems += _canister_problems(spec, params)
    pay = next((p for p in spec["parts"] if p["name"] == "payload"), None)
    b = params["bottle"]
    if pay and not b["neck_d_m"] < b["cap_d_m"] < value(pay["diameter_m"]):
        problems.append("the bottle's neck, its cap and its body (the spec's diameter) must grow in that order")
    if pay and b["base_r_m"] + b["shoulder_m"] + NECK_M + b["cap_h_m"] >= value(pay["length_m"]):
        problems.append("the bottle's base, shoulder, neck and cap leave no body inside its length")
    if params["layout"] == "bottle":  # the player scales this one prefab to every mass (bottle_pivot)
        if not pay or params["payload_kg"] != value(pay["mass_kg"]):
            problems.append("the bottle layout draws the spec's own payload part: 'payload_kg' must be its mass_kg")
        if "payload" in spec["legs_off"]["moved"]:
            problems.append("the bottle layout serves both builds only while legs_off leaves the payload in place")
    if params["arm"]["mount_width_m"] < params["arm"]["width_m"]:
        problems.append("'arm.mount_width_m' is narrower than 'arm.width_m'")
    lod = params["lod"]
    if not lod["lod1_switch_m"] < lod["lod2_switch_m"] < lod["lod3_switch_m"] < lod["cull_m"]:
        problems.append("the LOD switches and the cull must increase")
    return problems


def _canister_problems(spec, params):
    """The canister's look against its part: the bore, the tube, the collar and the body grow in that order; the
    mouth flares past the tube and the lip; the flare and the lip fit the nozzle; the hangers and the lid's seam lie
    on the body; the coil's bobbin (its part) lies inside the body."""
    can = next((p for p in spec["parts"] if p["name"] == "canister"), None)
    if can is None:
        return ["the spec has no canister part to draw the coil's housing from"]
    coil = next(p for p in spec["parts"] if p["name"] == "coil")
    k = params["canister"]
    R, L = value(can["diameter_m"]) / 2, value(can["length_m"])
    rb, ro, rm = k["bore_d_m"] / 2, k["nozzle_d_m"] / 2, k["mouth_d_m"] / 2
    problems = []
    if not rb < ro < k["collar_d_m"] / 2 < R:
        problems.append("the nozzle's bore, its tube, the cone's collar and the body must grow in that order")
    if not (ro < rm and rb + k["lip_r_m"] < rm):
        problems.append("the mouth must flare past the nozzle's tube and the lip round its bore")
    if MOUTH_BAND_M + (rm - ro) >= k["nozzle_m"] or k["lip_r_m"] >= k["nozzle_m"]:
        problems.append("the mouth's flare and lip leave no tube inside the nozzle's length")
    half = k["hanger_len_m"] / 2
    if not half <= k["hanger_front_m"] < k["hanger_rear_m"] <= L - half or not k["lid_m"] < L:
        problems.append("the hangers (front before rear) and the lid's seam must lie on the canister's body")
    c, w = value(can["position_m"]), normalize(value(can["axis"]))
    cc, cw = value(coil["position_m"]), normalize(value(coil["axis"]))
    for end in (-1.0, 1.0):
        d = sub(add(cc, scale(cw, end * value(coil["length_m"]) / 2)), c)
        along = dot(d, w)
        across = math.sqrt(max(0.0, dot(d, d) - along * along))
        if abs(along) > L / 2 + 1e-9 or across + params["coil"]["hoop_d_m"] / 2 > R:
            problems.append("the coil's bobbin (its part, its hoop) must lie inside the canister's body")
            break
    return problems


# --- build -------------------------------------------------------------------------------------------------------

def build(params):
    spec = load_spec(params)
    layout = Layout(spec, params)
    parts = [part_meshes(params, layout, q) for q in range(LODS)]
    packs = [(q, parts[q].pop(PACK)) for q in range(LODS) if PACK in parts[q]]  # their own meshes, beside their LODs
    legs = [(q, kind, parts[q].pop(kind)) for q in range(LODS) for kind in LEG_KINDS if kind in parts[q]]
    lods = []
    for q in range(LODS):
        mesh = Mesh()
        for m in parts[q].values():
            _append(mesh, m)
        mesh.smooth = set(mesh.slots())
        lods.append(mesh)
    for pack in [p for _, p in packs] + [m for _, _, m in legs]:
        pack.smooth = set(pack.slots())
    lod = params["lod"]
    if layout.bottle_only:
        return _build_bottle(params, layout, lods)
    spans = prop_spans(parts[0], layout)
    collision = _collision(params, layout)
    lens = (0.0, params["camera"]["lens_y_m"], params["camera"]["lens_z_m"])
    report = [f"{'legs off' if layout.legs_off else 'legs on'}, payload {params['payload_kg']:g} kg "
              f"(scale {layout.payload_scale:.4f}), payout {params['payout']:g}; "
              f"prop tips {spans['nose_first']:.4f} m nose-first, {spans['diagonal']:.4f} m on the diagonal "
              f"(DRN-02: 0.574 / 0.706 +-0.005), tip radius {spans['tip_radius']:.5f} m",
              f"FPV lens at ({lens[0]:g}, {lens[1]:g}, {lens[2]:g}) m, 0 deg uptilt, facing +z",
              f"coil pack its own readable mesh per LOD, {' / '.join(str(len(p.vertices)) for _, p in packs)} vertices"]
    tip, out = fiber_exit(params, layout)
    can, k = layout.part("canister"), params["canister"]
    fc, _ = mount_frame(params, layout, "canister")
    zt, _, zr, zf = canister_stations(params, layout)
    lowest = min(fc.at(0, s * r, z)[1] for s in (-1, 1) for z, r in ((zr, can["diameter_m"] / 2),
                 (zf, can["diameter_m"] / 2), (zt, k["mouth_d_m"] / 2)))
    report.append(f"canister body {can['diameter_m']:g} x {can['length_m']:g} m, cone {k['cone_m']:g} m, nozzle "
                  f"{k['nozzle_m']:g} m (tube {k['nozzle_d_m']:g}, bore {k['bore_d_m']:g}, mouth "
                  f"{k['mouth_d_m']:g} m, lip r {k['lip_r_m']:g} m), tilted {math.degrees(math.asin(-out[1])):.2f} deg "
                  f"rear-down; the line's exit at the nozzle tip ({tip[0]:.6f}, {tip[1]:.6f}, {tip[2]:.6f}) m "
                  f"leaving ({out[0]:.6f}, {out[1]:.6f}, {out[2]:.6f}), "
                  f"{min(h[2] for h in layout.hubs) - tip[2]:.4f} m aft of the rear motors' axis and "
                  f"{-tip[1]:.4f} m below the arm plane; its lowest point y {lowest:.4f} m")
    if legs:
        cuts = ", ".join(f"{c} y {clamp_edge(layout, c)[1]:g}" for c in CORNERS)
        report.append(f"leg parts their own readable meshes per LOD (stub, leg, T-joint, skid half a corner), "
                      f"{' / '.join(str(sum(1 for lq, _, _ in legs if lq == q)) for q in range(LODS))} meshes; "
                      f"tubes cut at the clamps' edges ({cuts}), skids at z {skid_cut(layout, 'right'):g} / "
                      f"{skid_cut(layout, 'left'):g} m")
    return {
        "lods": lods,
        "readable": [{"lod": q, "kind": PACK, "mesh": pack} for q, pack in packs]
                    + [{"lod": q, "kind": kind, "mesh": m} for q, kind, m in legs],
        "switch_distances_m": [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"], lod["lod3_switch_m"]],
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": slot, "physics": PHYSICS[slot]}
                      for slot in sorted({s for m in lods + [p for _, p in packs] + [m for _, _, m in legs]
                                          for s in m.slots()})],
        "collision": collision,
        "gaps": [],
        "wind_volumes": [],
        "ports": [{"id": EXIT, "position": list(tip), "direction": list(out), "max_diameter_m": k["bore_d_m"]}],
        "snag": _snag(params, layout),
        "report": report,
    }


def bottle_pivot(params, layout):
    """Where the bottle meets its plate: the spec's payload part's face toward the arm plane, under its centre (on
    the upper plate's top in drone.json). The mass build scales the part about this point (DroneSpec.
    ScaleAwayFromArmPlane keeps that face), so the bottle layout puts its origin here and the player scales it by its
    transform alone."""
    part = layout.part("payload")
    f, _ = mount_frame(params, layout, "payload")
    return tuple(part["position_m"][a] - f.v[a] * part["diameter_m"] / 2 for a in range(3))


def _build_bottle(params, layout, lods):
    """The bottle layout: the bottle and its straps at the spec's own payload mass, everything moved so the origin
    is bottle_pivot; the player sets localPosition to the pivot and localScale to the cube root of the mass ratio."""
    pivot = bottle_pivot(params, layout)
    for mesh in lods:
        mesh.vertices = [tuple(v[a] - pivot[a] for a in range(3)) for v in mesh.vertices]
    collision = [dict(s, center=[s["center"][a] - pivot[a] for a in range(3)]) for s in _bottle_shapes(params, layout)]
    part, lod = layout.part("payload"), params["lod"]
    length = bottle_length(params, layout)
    held = water_volume(_profile_points(params, part["diameter_m"] / 2, length, 0, ring=False)[:-4])
    report = [f"bottle at the spec's {part['mass_kg']:g} kg: body {part['diameter_m']:.4f} m across, "
              f"{length:.4f} m base to cap, holding {held * 1000:.4f} l (the part's "
              f"{math.pi * (part['diameter_m'] / 2) ** 2 * part['length_m'] * 1000:.4f} l)",
              f"origin at its underside on its plate, the drone's ({pivot[0]:g}, {pivot[1]:g}, {pivot[2]:g}) m: "
              f"the player sets localPosition there and localScale to (m / {part['mass_kg']:g} kg)^(1/3), "
              "hidden at 0 kg"]
    return {
        "lods": lods,
        "switch_distances_m": [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"], lod["lod3_switch_m"]],
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": slot, "physics": PHYSICS[slot]} for slot in sorted({s for m in lods for s in m.slots()})],
        "collision": collision,
        "gaps": [],
        "wind_volumes": [],
        "snag": [],
        "report": report,
    }


def part_meshes(params, layout, q):
    """{part: Mesh} of one LOD (q = 0..3), each drawn in asset space; build merges them."""
    out = {}

    def add_part(name, fn, *args):
        m = Mesh()
        fn(m, q, *args)
        if m.faces:
            out[name] = m

    if layout.bottle_only:
        add_part("bottle", _bottle, params, layout)
        add_part("bottle_straps", _bottle_straps, params, layout)
        return out

    for i, corner in enumerate(CORNERS):
        add_part(f"arm_{corner}", _arm, params, layout, corner, i)
        add_part(f"motor_{corner}", _motor, params, layout, corner, i)
        add_part(f"prop_{corner}", _prop, params, layout, i)
        if layout.has(f"leg_{corner}"):  # its tube in two, cut at the clamp's edge as the sim cuts it
            add_part(f"stub_{corner}", _stub, params, layout, corner)
            add_part(f"leg_{corner}", _leg, params, layout, corner)
        for name, fn in ((f"clamp_{corner}", _clamp), (f"tjoint_{corner}", _tjoint)):
            if layout.has(name):
                add_part(name, fn, params, layout, corner)
    add_part("plates", _plates, params, layout)
    add_part("electronics", _electronics, params, layout)
    for side in ("left", "right"):
        if layout.has(f"pack_{side}"):
            add_part(f"pack_{side}", _pack, params, layout, side)
            add_part(f"pack_{side}_straps", _pack_straps, params, layout, side)
            add_part(f"pack_{side}_leads", _leads, params, layout, side)
        if layout.has(f"skid_{side}"):  # a half a corner, cut midway between its bores
            for corner in skid_corners(layout, side):
                add_part(f"skid_{corner}", _skid, params, layout, corner)
    add_part("coil", _coil, params, layout)
    add_part(PACK, _coil_pack, params, layout)
    add_part("canister", _canister, params, layout)
    add_part("canister_mount", _canister_mount, params, layout)
    if layout.has("payload"):
        add_part("bottle", _bottle, params, layout)
        add_part("bottle_straps", _bottle_straps, params, layout)
    return out


def prop_spans(parts, layout):
    """The prop tips' spans (DRN-02) from the drawn blades: each prop's farthest blade point from its shaft is its
    tip radius; nose-first is the widest x extent of the four tip circles, the diagonal the hubs' distance across
    the X plus two tip radii."""
    radii = []
    for i, corner in enumerate(CORNERS):
        hub = layout.hubs[i]
        mesh = parts[f"prop_{corner}"]
        radii.append(max(math.hypot(v[0] - hub[0], v[2] - hub[2]) for v in mesh.vertices))
    xs = [h[0] for h in layout.hubs]
    lo = min(x - r for x, r in zip(xs, radii))
    hi = max(x + r for x, r in zip(xs, radii))
    rr, fl = CORNERS.index("rr"), CORNERS.index("fl")
    diagonal = math.hypot(layout.hubs[rr][0] - layout.hubs[fl][0], layout.hubs[rr][2] - layout.hubs[fl][2])
    return {"nose_first": hi - lo, "diagonal": diagonal + radii[rr] + radii[fl], "tip_radius": max(radii),
            "radii": radii}


def _append(dst, src):
    offset = len(dst.vertices)
    dst.vertices.extend(src.vertices)
    for indices, normal, uvs, slot in src.faces:
        dst.faces.append(([i + offset for i in indices], normal, uvs, slot))


# --- frame parts -------------------------------------------------------------------------------------------------

def _arm_axis(part):
    """The arm's unit direction outward in plan, its inner end's radius and the arm plane's height."""
    axis = normalize((part["axis"][0], 0.0, part["axis"][2]))
    pos = part["position_m"]
    if dot(axis, pos) < 0:
        axis = scale(axis, -1.0)
    r_centre = math.hypot(pos[0], pos[2])
    return axis, r_centre - part["length_m"] / 2, pos[1]


def _arm(m, q, params, layout, corner, i):
    part = layout.part(f"arm_{corner}")
    d, r_in, y = _arm_axis(part)
    hub = layout.hubs[i]
    r_m = math.hypot(hub[0], hub[2])
    a = params["arm"]
    t, w, rho = a["thickness_m"], a["width_m"] / 2, a["mount_width_m"] / 2
    frame = Frame((0.0, y, 0.0), d, (0.0, 1.0, 0.0), cross(d, (0.0, 1.0, 0.0)))
    if q == 3:
        box(m, frame, (r_in, -t / 2, -w), (r_m + rho, t / 2, w), CARBON, {"-w": CARBON_EDGE, "+w": CARBON_EDGE})
        return
    meet = r_m - math.sqrt(rho * rho - w * w)  # the strip's edges meet the mount's circle
    alpha = math.asin(w / rho)
    arc = circle(r_m, 0.0, rho, (16, 10, 6)[q], -(math.pi - alpha), math.pi - alpha)
    outline = [(r_in, -w), (meet, -w)] + arc[1:-1] + [(meet, w), (r_in, w)]
    prism(m, frame, outline, -t / 2, t / 2, CARBON, CARBON_EDGE, smooth_sides=True)


def _rounded_rect(cx, cz, sx, sz, r, per_corner):
    if r <= 0 or per_corner == 0:
        return [(cx - sx / 2, cz - sz / 2), (cx + sx / 2, cz - sz / 2), (cx + sx / 2, cz + sz / 2),
                (cx - sx / 2, cz + sz / 2)]
    pts = []
    for (ex, ez), start in (((1, -1), -math.pi / 2), ((1, 1), 0.0), ((-1, 1), math.pi / 2), ((-1, -1), math.pi)):
        ox, oz = cx + ex * (sx / 2 - r), cz + ez * (sz / 2 - r)
        pts += circle(ox, oz, r, per_corner + 1, start, start + math.pi / 2)
    return pts


def plate_heights(params, layout):
    """(bottom plate, top plate, upper plate) as (y0, y1) each, and the standoffs' span."""
    arm = layout.part("arm_rr")
    t = params["arm"]["thickness_m"]
    under = arm["position_m"][1] - t / 2
    p = params["plates"]
    height = layout.part("plates")["size_m"][1]
    bottom = (under - p["bottom_m"], under)
    top = (under + t, under + t + p["top_m"])
    upper = (bottom[0] + height - p["upper_m"], bottom[0] + height)
    return bottom, top, upper


def _plates(m, q, params, layout):
    part = layout.part("plates")
    cx, _, cz = part["position_m"]
    sx, _, sz = part["size_m"]
    bottom, top, upper = plate_heights(params, layout)
    if q == 3:
        box(m, AXES, (cx - sx / 2, bottom[0], cz - sz / 2), (cx + sx / 2, upper[1], cz + sz / 2), CARBON)
        return
    outline = _rounded_rect(cx, cz, sx, sz, params["plates"]["corner_r_m"], (4, 2, 0)[q])
    for y0, y1 in (bottom, top, upper):
        prism(m, AXES, [(x, z) for x, z in outline], y0, y1, CARBON, CARBON_EDGE, smooth_sides=True)
    if q <= 1:  # standoffs: Ø6 mm black aluminium at the plates' corners
        for ex in (-1, 1):
            for ez in (-1, 1):
                f = Frame((cx + ex * (sx / 2 - 0.007), 0.0, cz + ez * (sz / 2 - 0.010)), (1, 0, 0), (0, 0, 1), (0, 1, 0))
                lathe(m, f, [(top[1], 0.0), (top[1], 0.003), (upper[0], 0.003), (upper[0], 0.0)], (8, 6)[q],
                      [ANODISED] * 3)


def _motor(m, q, params, layout, corner, i):
    part = layout.part(f"motor_{corner}")
    x, y, z = part["position_m"]
    R, L = part["diameter_m"] / 2, part["length_m"]
    y0, y1 = y - L / 2, y + L / 2
    hub_y = layout.hubs[i][1]
    y_bell = hub_y - params["prop"]["hub_h_m"] / 2  # the bell's top: the hub sits on it, the shaft's boss inside
    rb, bh = params["motor"]["base_d_m"] / 2, params["motor"]["base_h_m"]
    r_boss = 0.006
    f = Frame((x, 0.0, z), (1, 0, 0), (0, 0, 1), (0, 1, 0))
    if q == 3:
        lathe(m, f, [(y0, 0.0), (y0, R), (y1, R), (y1, 0.0)], 6, [BELL] * 3)
        return
    segments = (36, 18, 10)[q]
    gap = 0.002 if q <= 1 else 0.0
    lower = [(y0, 0.0), (y0, rb), (y0 + bh, rb)]
    paints = [BELL, BELL]
    if gap:
        r_cu = rb - 0.001
        lower += [(y0 + bh, r_cu), (y0 + bh + gap, r_cu), (y0 + bh + gap, R)]
        paints += [BELL, COPPER, BELL]
    else:
        lower += [(y0 + bh, R)]
        paints += [BELL]
    lower += [(y_bell - 0.001, R), (y_bell, R - 0.001)]
    paints += [BELL, BELL]
    if q == 0:  # six vents in the bell's top between r_v1 and r_v2
        r_v1, r_v2 = r_boss + 0.003, R - 0.004
        lathe(m, f, lower + [(y_bell, r_v2)], segments, paints + [BELL])
        lathe(m, f, [(y_bell, r_v1), (y_bell, r_boss), (y1, r_boss), (y1, 0.0)], segments, [BELL, ZINC, ZINC])
        _vents(m, f, segments, y_bell, r_v1, r_v2)
    else:
        lathe(m, f, lower + [(y_bell, r_boss), (y1, r_boss), (y1, 0.0)], segments, paints + [BELL, ZINC, ZINC])


def _vents(m, f, segments, y_top, r1, r2, depth=0.0015):
    """The bell's top between r1 and r2: six spokes flush with the top and six vents sunk `depth` to the stator,
    each a group of segments / 12 segments, with their walls."""
    per = segments // 12
    step = 2 * math.pi / segments

    def p(r, k, y):
        return f.at(r * math.cos(k * step), r * math.sin(k * step), y)

    for g in range(12):
        k0, k1 = g * per, (g + 1) * per
        if g % 2 == 0:  # a spoke
            for k in range(k0, k1):
                _quad(m, [p(r1, k, y_top), p(r2, k, y_top), p(r2, k + 1, y_top), p(r1, k + 1, y_top)],
                      f.dir(0, 0, 1), BELL)
            continue
        yb = y_top - depth
        for k in range(k0, k1):
            mid = (k + 0.5) * step
            _quad(m, [p(r1, k, yb), p(r2, k, yb), p(r2, k + 1, yb), p(r1, k + 1, yb)], f.dir(0, 0, 1), STATOR)
            _quad(m, [p(r2, k, yb), p(r2, k + 1, yb), p(r2, k + 1, y_top), p(r2, k, y_top)],
                  f.dir(-math.cos(mid), -math.sin(mid), 0), BELL)
            _quad(m, [p(r1, k, yb), p(r1, k + 1, yb), p(r1, k + 1, y_top), p(r1, k, y_top)],
                  f.dir(math.cos(mid), math.sin(mid), 0), BELL)
        for k, sign in ((k0, 1.0), (k1, -1.0)):
            ang = k * step
            _quad(m, [p(r1, k, yb), p(r2, k, yb), p(r2, k, y_top), p(r1, k, y_top)],
                  f.dir(-math.sin(ang) * sign, math.cos(ang) * sign, 0), BELL)


def _quad(m, corners, normal, pnt):
    slot, uv = pnt
    ids = [m.vertex(c) for c in corners]
    m.face(ids, normal, slot, [uv] * len(ids))


# --- props -------------------------------------------------------------------------------------------------------

def _prop(m, q, params, layout, i):
    hub = layout.hubs[i]
    p = params["prop"]
    R = layout.prop_d / 2
    spin = layout.spins[i]
    rest = math.radians(p["rest_deg"][CORNERS[i]])
    r_hub, h = p["hub_d_m"] / 2, p["hub_h_m"]
    for k in range(layout.blades):
        phi0 = rest + 2 * math.pi * k / layout.blades
        if q == 3:
            _blade_box(m, hub, R, r_hub, phi0 + spin * p["sweep_m"] / R / 2)
        else:
            _blade(m, q, hub, R, phi0, spin, p, layout.pitch, r_hub)
    if q == 3:
        return
    f = Frame((hub[0], 0.0, hub[2]), (1, 0, 0), (0, 0, 1), (0, 1, 0))
    y0, y1 = hub[1] - h / 2, hub[1] + h / 2
    lathe(m, f, [(y0, 0.0), (y0, r_hub), (y1, r_hub), (y1, 0.0)], (20, 12, 8)[q], [BLADE] * 3)
    if q <= 1:  # the M5 nut and the shaft's end
        nut = circle(hub[0], hub[2], 0.0046, 6, math.pi / 6)
        prism(m, AXES, nut, y1, y1 + 0.005, ZINC)
        lathe(m, f, [(y1 + 0.005, 0.0), (y1 + 0.005, 0.0025), (y1 + 0.0065, 0.0025), (y1 + 0.0065, 0.0)], 8,
              [ZINC] * 3)


def _blade_box(m, hub, R, r_hub, phi0):
    d = (math.cos(phi0), 0.0, math.sin(phi0))
    f = Frame((hub[0], hub[1], hub[2]), d, (0.0, 1.0, 0.0), cross(d, (0.0, 1.0, 0.0)))
    box(m, f, (r_hub, -0.0015, -0.009), (R, 0.0015, 0.009), BLADE)


def _chord(x, p, R):
    """The blade's chord at r / R = x, and the radius where the round tip starts."""
    c_max, c_tip = p["chord_m"], p["tip_chord_m"]
    x_t = 1.0 - (c_tip / 2) / R
    if x >= x_t:
        h = (x - x_t) * R
        return 2 * math.sqrt(max(0.0, (c_tip / 2) ** 2 - h * h)), x_t
    table = list(CHORD_TABLE) + [(x_t, c_tip / c_max)]
    for (x0, v0), (x1, v1) in zip(table, table[1:]):
        if x <= x1:
            return c_max * (v0 + (v1 - v0) * (x - x0) / (x1 - x0)), x_t
    return c_tip, x_t


def blade_stations(q, R, r_hub, x_t):
    """Radii of a blade's sections: from inside the hub to the round tip, then into the tip (the apex follows)."""
    n = (16, 9, 5)[q]
    r0 = 0.7 * r_hub
    rt = x_t * R
    body = [r0 + (rt - r0) * (k / n) ** 0.9 for k in range(n + 1)]
    tip = [rt + (R - rt) * f for f in ((0.5, 0.85, 0.97), (0.6, 0.93), (0.7,))[q]]
    return body + tip


def _blade(m, q, hub, R, phi0, spin, p, pitch, r_hub):
    """One blade: sections of LE, upper surface, a thin trailing edge and the lower surface (LOD0 and 1), or a
    four-point lens (LOD2), lofted from inside the hub to the round tip and closed on the tip's apex."""
    _, x_t = _chord(0.5, p, R)
    rows = []
    centres = []
    for r in blade_stations(q, R, r_hub, x_t):
        x = r / R
        c, _ = _chord(x, p, R)
        sweep = p["sweep_m"] * max(0.0, (x - SWEEP_FROM) / (1 - SWEEP_FROM)) ** 2
        phi = phi0 + spin * sweep / r
        er = (math.cos(phi), 0.0, math.sin(phi))
        mv = (spin * math.sin(phi), 0.0, -spin * math.cos(phi))  # the blade's motion at this radius
        theta = min(math.radians(BLADE_MAX_TWIST_DEG), math.atan(pitch / (2 * math.pi * r)))
        cd = add(scale(mv, math.cos(theta)), (0.0, math.sin(theta), 0.0))  # TE -> LE, rising
        nn = add(scale(mv, -math.sin(theta)), (0.0, math.cos(theta), 0.0))  # the upper surface's side
        t = p["thickness_m"] * (1 - 0.75 * x) * min(1.0, c / max(1e-9, p["tip_chord_m"]))
        centre = add(hub, scale(er, r))

        def at(f_chord, off):
            return add(add(centre, scale(cd, c / 2 - f_chord * c)), scale(nn, off))

        if q <= 1:
            te = max(TE_M, 0.0) * min(1.0, c / p["tip_chord_m"])
            rows.append([at(0.0, 0.0), at(0.2, 0.6 * t), at(0.55, 0.45 * t), at(1.0, te / 2), at(1.0, -te / 2),
                         at(0.35, -0.4 * t)])
        else:
            rows.append([at(0.0, 0.0), at(0.4, 0.5 * t), at(1.0, 0.0), at(0.4, -0.3 * t)])
        centres.append(centre)
    phi_tip = phi0 + spin * p["sweep_m"] / R
    apex = add(hub, (R * math.cos(phi_tip), 0.0, R * math.sin(phi_tip)))
    ids = [[m.vertex(v) for v in row] for row in rows]
    n = len(rows[0])
    for s in range(len(rows) - 1):
        mid_axis = scale(add(centres[s], centres[s + 1]), 0.5)
        for k in range(n):
            k1 = (k + 1) % n
            quad = [ids[s][k], ids[s][k1], ids[s + 1][k1], ids[s + 1][k]]
            _oriented(m, quad, mid_axis, BLADE)
    root = list(ids[0])
    _oriented(m, [m.vertex(m.vertices[v]) for v in root], add(centres[0], scale(normalize(sub(centres[1], centres[0])), 1.0)), BLADE)
    apex_id = m.vertex(apex)
    for k in range(n):
        _oriented(m, [ids[-1][k], ids[-1][(k + 1) % n], apex_id], centres[-1], BLADE)


def _oriented(m, ids, inside, pnt):
    """A face whose outward normal points away from the point `inside` (Newell's normal, flipped to agree)."""
    pts = [m.vertices[i] for i in ids]
    nrm = [0.0, 0.0, 0.0]
    for a, b in zip(pts, pts[1:] + pts[:1]):
        nrm[0] += (a[1] - b[1]) * (a[2] + b[2])
        nrm[1] += (a[2] - b[2]) * (a[0] + b[0])
        nrm[2] += (a[0] - b[0]) * (a[1] + b[1])
    centroid = [sum(p[a] for p in pts) / len(pts) for a in range(3)]
    if dot(nrm, sub(centroid, inside)) < 0:
        nrm = [-c for c in nrm]
    slot, uv = pnt
    m.face(ids, normalize(nrm), slot, [uv] * len(ids))


# --- legs ---------------------------------------------------------------------------------------------------------

def clamp_edge(layout, corner):
    """Where leg `corner`'s tube leaves its clamp: on the leg's axis at the clamp part's underside, the sim's
    LegDamageSpec.Clamp (DroneSpec.BuildLegDamage), where a broken tube parts and about which a set turns it."""
    leg = layout.part(f"leg_{corner}")["position_m"]
    return (leg[0], box_extent(layout.part(f"clamp_{corner}"))[0][1], leg[2])


def _leg_tube(layout, corner):
    part = layout.part(f"leg_{corner}")
    x, y, z = part["position_m"]
    return (Frame((x, 0.0, z), (1, 0, 0), (0, 0, 1), (0, 1, 0)), part["diameter_m"] / 2,
            y - part["length_m"] / 2, y + part["length_m"] / 2)


def _stub(m, q, params, layout, corner):
    """The tube in the clamp (stub_<c>): from the clamp's edge up to its top at the arm, open at the edge, where the
    clamp's underside covers it (a cap there would fight that face once the leg below is gone)."""
    f, r, _, top = _leg_tube(layout, corner)
    lathe(m, f, [(clamp_edge(layout, corner)[1], r), (top, r), (top, 0.0)], (12, 8, 6, 4)[q], [TUBE] * 2,
          phase=math.pi / 4)


def _leg(m, q, params, layout, corner):
    """The free length (leg_<c>): from its foot in the T-joint up to the clamp's edge, its end there the 10/8 mm
    tube's at LOD0 and 1 (a bore 10 mm deep, as the skids' open ends), seen once it breaks off there."""
    f, r, foot, _ = _leg_tube(layout, corner)
    edge, sides = clamp_edge(layout, corner)[1], (12, 8, 6, 4)[q]
    if q <= 1:
        ri, depth = r - 0.001, 0.010
        lathe(m, f, [(foot, 0.0), (foot, r), (edge, r), (edge, ri), (edge - depth, ri), (edge - depth, 0.0)], sides,
              [TUBE, TUBE, TUBE, BLACK, BLACK], phase=math.pi / 4)
    else:
        lathe(m, f, [(foot, 0.0), (foot, r), (edge, r), (edge, 0.0)], sides, [TUBE] * 3, phase=math.pi / 4)


def clamp_span(params, layout, corner):
    """The clamp's height: the part's box, its top at the arm's underside, its bottom raised to clear the packs'
    tops where they meet in plan (the spec's boxes overlap there by a few millimetres)."""
    lo, hi = box_extent(layout.part(f"clamp_{corner}"))
    under = plate_heights(params, layout)[0][1]
    bottom = lo[1]
    for side in ("left", "right"):
        if layout.has(f"pack_{side}"):
            plo, phi = box_extent(layout.part(f"pack_{side}"))
            if _plan_overlap(_clamp_footprint(params, layout, corner), plo, phi):
                bottom = max(bottom, phi[1] + 0.001)
    return bottom, under


def _clamp_frame(params, layout, corner):
    part = layout.part(f"clamp_{corner}")
    d, _, _ = _arm_axis(layout.part(f"arm_{corner}"))
    c = part["position_m"]
    return Frame((c[0], 0.0, c[2]), d, (0.0, 1.0, 0.0), cross(d, (0.0, 1.0, 0.0))), part["size_m"]


def _clamp_footprint(params, layout, corner):
    f, size = _clamp_frame(params, layout, corner)
    along, across = 0.035, size[0]  # the sheet's 25 x 25 x 35 mm, along the arm
    return [f.at(a * along / 2, 0.0, b * across / 2) for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))]


def _plan_overlap(poly, lo, hi):
    xs, zs = [p[0] for p in poly], [p[2] for p in poly]
    return max(xs) > lo[0] and min(xs) < hi[0] and max(zs) > lo[2] and min(zs) < hi[2] and any(
        lo[0] < p[0] < hi[0] and lo[2] < p[2] < hi[2] for p in poly)


def _clamp(m, q, params, layout, corner):
    if q == 3:
        return
    f, size = _clamp_frame(params, layout, corner)
    bottom, top = clamp_span(params, layout, corner)
    along, across = 0.035, size[0]
    box(m, f, (-along / 2, bottom, -across / 2), (along / 2, top, across / 2), BLACK)
    if q == 0:  # the quick-release bolt across the arm: a hex head one side, the wing nut on the other
        c = layout.part(f"clamp_{corner}")["position_m"]
        side = 1.0 if dot(f.w, (0.0, 0.0, c[2])) > 0 else -1.0
        yb = (bottom + top) / 2
        bf = Frame(f.at(0.0, yb, 0.0), f.u, f.w, f.v)  # extrude along the bolt (across the arm)
        head = [(math.cos(a) * 0.004, math.sin(a) * 0.004) for a in (k * math.pi / 3 + math.pi / 6 for k in range(6))]
        nut0 = side * across / 2
        prism(m, bf, head, -nut0, -nut0 - side * 0.003, ZINC)  # the bolt's head
        prism(m, bf, head, nut0, nut0 + side * 0.005, ZINC)  # the wing nut's body
        for wing in (-1, 1):  # two wings, 20 mm across the pair, standing up and out
            wf = Frame(bf.at(0.0, nut0 + side * 0.0025, 0.0), f.u, f.v, f.w)
            box(m, wf, (wing * 0.004 if wing > 0 else -0.010, -0.0005, -0.00075),
                (0.010 if wing > 0 else -0.004, 0.006, 0.00075), ZINC)


def _tjoint(m, q, params, layout, corner):
    if q == 3:
        return
    part = layout.part(f"tjoint_{corner}")
    lo, hi = box_extent(part)
    skid = layout.part("skid_right" if part["position_m"][0] > 0 else "skid_left")
    ground = skid["position_m"][1] - skid["diameter_m"] / 2  # flat-bottomed on the ground, under the skid
    box(m, AXES, (lo[0], ground, lo[2]), (hi[0], hi[1], hi[2]), BLACK)
    leg = layout.part(f"leg_{corner}")
    f = Frame((leg["position_m"][0], 0.0, leg["position_m"][2]), (1, 0, 0), (0, 0, 1), (0, 1, 0))
    r = part["size_m"][0] / 2
    lathe(m, f, [(hi[1], 0.0), (hi[1], r), (hi[1] + 0.012, r), (hi[1] + 0.013, r - 0.001), (hi[1] + 0.013, 0.0)],
          (12, 8, 6)[q], [BLACK] * 4)  # the leg's socket, closed on the sleeve's top


def skid_corners(layout, side):
    """The corners whose legs stand on skid `side`, in CORNERS order: the sim's, a leg at x >= 0 on the right skid."""
    return [c for c in CORNERS if layout.has(f"leg_{c}")
            and (layout.part(f"leg_{c}")["position_m"][0] >= 0) == (side == "right")]


def skid_cut(layout, side):
    """Where skid `side` is cut into its corners' halves: midway between its two legs' bores, the sim's half-skids'
    inner end (drone.json's skid capsules, ContactModel's Middle)."""
    zs = [layout.part(f"leg_{c}")["position_m"][2] for c in skid_corners(layout, side)]
    return sum(zs) / len(zs)


def _skid(m, q, params, layout, corner):
    """Corner `corner`'s half of its skid (skid_<c>): from the cut out through its T-joint to the skid's open end,
    closed at the cut; the lime band (aft of the port skid's rear T-joint) with the port rear half."""
    side = "right" if layout.part(f"leg_{corner}")["position_m"][0] >= 0 else "left"
    part = layout.part(f"skid_{side}")
    x, y, z = part["position_m"]
    r, L, cut = part["diameter_m"] / 2, part["length_m"], skid_cut(layout, side)
    rear = layout.part(f"leg_{corner}")["position_m"][2] < cut
    f = Frame((x, y, 0.0), (1, 0, 0), (0, 1, 0), (0, 0, 1))
    sides = (12, 8, 6, 4)[q]
    if q <= 1:  # 10/8 mm tube, open-ended (the sheet): the bore 10 mm deep at the end
        ri, depth = r - 0.001, 0.010
        if rear:
            z0 = z - L / 2
            lathe(m, f, [(z0 + depth, 0.0), (z0 + depth, ri), (z0, ri), (z0, r), (cut, r), (cut, 0.0)], sides,
                  [BLACK, BLACK, TUBE, TUBE, TUBE], phase=math.pi / 4)
        else:
            z1 = z + L / 2
            lathe(m, f, [(cut, 0.0), (cut, r), (z1, r), (z1, ri), (z1 - depth, ri), (z1 - depth, 0.0)], sides,
                  [TUBE, TUBE, TUBE, BLACK, BLACK], phase=math.pi / 4)
    else:
        lo, hi = (z - L / 2, cut) if rear else (cut, z + L / 2)
        lathe(m, f, [(lo, 0.0), (lo, r), (hi, r), (hi, 0.0)], sides, [TUBE] * 3, phase=math.pi / 4)
    if side == "left" and rear and params["skid_band"]:
        _skid_band(m, q, params, layout, side)


def _skid_band(m, q, params, layout, side):
    """The lime band round the port skid, aft of its rear T-joint (T)."""
    if q == 3:
        return
    part = layout.part(f"skid_{side}")
    x, y, z = part["position_m"]
    r, L = part["diameter_m"] / 2, part["length_m"]
    f = Frame((x, y, 0.0), (1, 0, 0), (0, 1, 0), (0, 0, 1))
    zb = z - L / 2 + 0.075
    lathe(m, f, [(zb, r), (zb, r + 0.0004), (zb + 0.015, r + 0.0004), (zb + 0.015, r)], (12, 8, 6)[q], [LIME] * 4,
          closed=True, phase=math.pi / 4)


# --- packs --------------------------------------------------------------------------------------------------------

def strap_stations(params, layout, side):
    lo, hi = box_extent(layout.part(f"pack_{side}"))
    zc = (lo[2] + hi[2]) / 2
    return [zc + dz for dz in ((-0.075, 0.075) if params["straps"] == 2 else (-0.08, 0.0, 0.08))]


def _pack(m, q, params, layout, side):
    part = layout.part(f"pack_{side}")
    lo, hi = box_extent(part)
    c = part["position_m"]
    if q == 3:
        box(m, AXES, lo, hi, WRAP)
        return
    f = Frame((c[0], c[1], 0.0), (1, 0, 0), (0, 0, 1), (0, 1, 0))  # outline in (x, y), extruded along z
    sx, sy = part["size_m"][0], part["size_m"][1]
    outline = _rounded_rect(0.0, 0.0, sx, sy, params["pack"]["corner_r_m"], (4, 2, 0)[q])
    prism(m, f, outline, lo[2], hi[2], WRAP, smooth_sides=True)


def _pack_straps(m, q, params, layout, side):
    """A pack's hook-and-loop straps: round the pack and through a slot in the bottom plate, black with purple
    edges (T)."""
    if q == 3:
        return
    part = layout.part(f"pack_{side}")
    lo, hi = box_extent(part)
    c = part["position_m"]
    plates = layout.part("plates")
    bottom = plate_heights(params, layout)[0]
    sgn = 1.0 if c[0] > 0 else -1.0
    edge_x = plates["position_m"][0] + sgn * plates["size_m"][0] / 2
    slot_x = edge_x - sgn * 0.015  # the strap's slot in the bottom plate, 15 mm in from its edge
    inner_x, outer_x = (lo[0], hi[0]) if sgn > 0 else (hi[0], lo[0])
    path = [(slot_x, bottom[0]), (slot_x, bottom[1]), (edge_x, bottom[1]), (outer_x, hi[1]), (outer_x, lo[1]),
            (inner_x, lo[1])]
    bands = [(STRAP_EDGE, STRAP_EDGE_PAINT), (1 - 2 * STRAP_EDGE, STRAP), (STRAP_EDGE, STRAP_EDGE_PAINT)]
    for zs in strap_stations(params, layout, side):
        sf = Frame((0.0, 0.0, zs), (1, 0, 0), (0, 1, 0), (0, 0, 1))
        ribbon(m, sf, path, STRAP_W_M, STRAP_T_M, (bands, STRAP, STRAP_EDGE_PAINT), closed=True)


def connector_box(layout, side):
    """The mated XT90 pair behind a pack: along x, inboard of the pack, at the rear (the sheet's leads, plugged)."""
    lo, hi = box_extent(layout.part(f"pack_{side}"))
    sgn = 1.0 if side == "right" else -1.0
    inner = lo[0] if sgn > 0 else -hi[0]
    length, height, width = XT90_M
    x0 = inner - 0.016
    zc = lo[2] - 0.035
    yc = hi[1] + 0.004
    xs = sorted((sgn * x0, sgn * (x0 + length)))
    return (xs[0], yc - height / 2, zc - width / 2), (xs[1], yc + height / 2, zc + width / 2)


def _leads(m, q, params, layout, side):
    """A pack's red and black leads to its mated XT90 pair and on into the frame's rear, plugged."""
    if q == 3:
        return
    clo, chi = connector_box(layout, side)
    sgn = 1.0 if side == "right" else -1.0
    box(m, AXES, clo, chi, YELLOW)
    if q == 2:
        return
    plo, phi = box_extent(layout.part(f"pack_{side}"))
    outer_end = chi[0] if sgn > 0 else clo[0]
    inner_end = clo[0] if sgn > 0 else chi[0]
    yc, zc = (clo[1] + chi[1]) / 2, (clo[2] + chi[2]) / 2
    xp = layout.part(f"pack_{side}")["position_m"][0]
    top = plate_heights(params, layout)[1]
    sides, per = (6, 4)[q], (8, 3)[q]
    for k, pnt in enumerate((RED, LEAD_BLACK)):
        dz = (k - 0.5) * 0.008
        pack_side = [(xp + sgn * (0.006 - 0.012 * k), plo[1] + 0.022, plo[2]),
                     (xp + sgn * (0.004 - 0.012 * k), plo[1] + 0.025, plo[2] - 0.012),
                     (outer_end + sgn * 0.010, yc + 0.002, zc + dz - 0.004),
                     (outer_end, yc, zc + dz)]
        frame_side = [(inner_end, yc, zc + dz), (inner_end - sgn * 0.010, yc + 0.003, zc + dz + 0.003),
                      (sgn * 0.012 + sgn * 0.006 * k, top[1] + 0.004, plo[2] + 0.012),
                      (sgn * 0.010 + sgn * 0.006 * k, top[1] + 0.004, plo[2] + 0.030)]
        for ctrl in (pack_side, frame_side):
            tube(m, _spline(ctrl, per), LEAD_R_M, sides, pnt)


def _spline(points, per):
    """A Catmull-Rom curve through the points, `per` samples per span."""
    pts = [points[0]] + list(points) + [points[-1]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        for s in range(per):
            t = s / per
            out.append(tuple(0.5 * (2 * p1[a] + (-p0[a] + p2[a]) * t + (2 * p0[a] - 5 * p1[a] + 4 * p2[a] - p3[a]) * t * t
                                    + (-p0[a] + 3 * p1[a] - 3 * p2[a] + p3[a]) * t ** 3) for a in range(3)))
    out.append(tuple(points[-1]))
    return out


# --- electronics -------------------------------------------------------------------------------------------------

def camera_box(params):
    lens_y, lens_z = params["camera"]["lens_y_m"], params["camera"]["lens_z_m"]
    return (-0.0095, lens_y - 0.0095, lens_z - 0.026), (0.0095, lens_y + 0.0095, lens_z - 0.006)


def _electronics(m, q, params, layout):
    if q == 3:
        return
    lo, hi = camera_box(params)
    lens_y, lens_z = params["camera"]["lens_y_m"], params["camera"]["lens_z_m"]
    box(m, AXES, lo, hi, BLACK)
    lf = Frame((0.0, lens_y, 0.0), (1, 0, 0), (0, 1, 0), (0, 0, 1))
    lathe(m, lf, [(hi[2], 0.006), (lens_z - 0.0005, 0.006), (lens_z, 0.0055), (lens_z, 0.0)], (16, 10, 6)[q],
          [BLACK, BLACK, LENS])
    if q == 2:
        return
    part = layout.part("electronics")
    cx, _, cz = part["position_m"]
    bottom, top, upper = plate_heights(params, layout)
    for yb in (top[1] + 0.005, top[1] + 0.013):  # the ESC and the flight controller, 36 mm boards
        box(m, AXES, (cx - 0.018, yb, cz - 0.018), (cx + 0.018, yb + 0.0016, cz + 0.018), PCB)
    for ex in (-1, 1):
        for ez in (-1, 1):  # nylon spacers on the 30.5 mm pattern
            f = Frame((cx + ex * 0.01525, 0.0, cz + ez * 0.01525), (1, 0, 0), (0, 0, 1), (0, 1, 0))
            lathe(m, f, [(top[1], 0.0), (top[1], 0.0025), (upper[0], 0.0025), (upper[0], 0.0)], 6, [BLACK] * 3)
    plate = layout.part("plates")
    z_rear = plate["position_m"][2] - plate["size_m"][2] / 2
    # behind the stack, clear of the standoffs and of the pack leads' ends: the capacitor lying along x, the module
    cap = Frame((0.0, top[1] + 0.005, z_rear + 0.035), (0, 0, 1), (0, 1, 0), (1, 0, 0))
    lathe(m, cap, [(0.008, 0.0), (0.008, 0.005), (0.028, 0.005), (0.028, 0.0)], (12, 8)[q], [BLACK] * 3)
    box(m, AXES, (-0.030, top[1], z_rear + 0.025), (-0.004, top[1] + 0.010, z_rear + 0.055), BLACK)
    for corner in CORNERS:  # the motor leads in black sleeving along each arm's top
        arm = layout.part(f"arm_{corner}")
        d, r_in, y = _arm_axis(arm)
        f = Frame((0.0, y, 0.0), d, (0.0, 1.0, 0.0), cross(d, (0.0, 1.0, 0.0)))
        r_m = math.hypot(*[layout.hubs[CORNERS.index(corner)][a] for a in (0, 2)])
        t = params["arm"]["thickness_m"]
        box(m, f, (0.062, t / 2, -0.004), (r_m - params["motor"]["base_d_m"] / 2, t / 2 + 0.003, 0.004), LEAD_BLACK)


# --- coil -------------------------------------------------------------------------------------------------------

def mount_frame(params, layout, name):
    """A round part's frame: origin the part's centre, w along its axis, v away from the plate it is strapped to
    (up for a part above the upper plate, down for one below the bottom plate; square to a tilted axis), and how far
    that plate's face lies above or below the centre."""
    part = layout.part(name)
    c = part["position_m"]
    axis = normalize(part["axis"])
    bottom, _, upper = plate_heights(params, layout)
    up = c[1] > upper[1]
    v = (0.0, 1.0 if up else -1.0, 0.0)
    u = cross(v, axis)
    if dot(u, u) < 1e-9:
        raise ValueError(f"{name}: its axis is vertical; a strapped round part lies along the plate")
    v = normalize(sub(v, scale(axis, dot(v, axis))))  # square to a tilted axis (the canister's); level ones keep v
    return Frame(c, normalize(cross(v, axis)), v, axis), (c[1] - upper[1]) if up else (bottom[0] - c[1])


def coil_radii(params, layout):
    """The fiber pack's radius at this payout (its volume falls linearly with the line's length) and the spool's
    stations along its axis: the rear end, the pack's rear and front faces, the front end."""
    part = layout.part("coil")
    c = params["coil"]
    R = part["diameter_m"] / 2
    rc = c["core_d_m"] / 2
    rp = math.sqrt(rc * rc + (R * R - rc * rc) * (1 - params["payout"]))
    L = part["length_m"]
    z0, z3 = -L / 2, L / 2
    z2 = z3 - c["hoop_len_m"]
    z1 = z2 - c["wound_m"]
    return rp, (z0, z1, z2, z3)


COIL_SEGMENTS = (32, 20, 10, 6)  # the spool's lathe per LOD


def coil_profile(params, layout, q):
    """The spool's lathe profile at LOD q (T's, under the frame by D-050), its paints and where the pack runs in it,
    profile[a] to profile[b]: from the cap's rim at the pack's rear face (the rear end at LOD3, which has no cap) to the
    hoop's rim at its rear face. Both ends are creases, so the lathes of the three runs are the spool's own."""
    c = params["coil"]
    rp, (z0, z1, z2, z3) = coil_radii(params, layout)
    r_cap, r_hoop, r_hole = c["cap_d_m"] / 2, c["hoop_d_m"] / 2, c["hole_d_m"] / 2
    if q == 3:
        return ([(z0, 0.0), (z0, rp), (z2, rp), (z2, r_hoop), (z3, r_hoop), (z3, 0.0)],
                [PRINT, FIBER, BLACK, BLACK, BLACK], 1, 3)
    if q == 0:  # the recessed three-spoke hub round the line's hole
        r_boss, r_rec, depth = 0.010, 0.025, 0.008
        profile = [(z0 + 0.012, 0.0), (z0 + 0.012, r_hole), (z0, r_hole), (z0, r_boss), (z0 + depth, r_boss),
                   (z0 + depth, r_rec), (z0, r_rec), (z0, r_cap)]
        paints = [RECESS, PRINT, PRINT, PRINT, RECESS, PRINT, PRINT]
    else:
        profile = [(z0 + 0.012, 0.0), (z0 + 0.012, r_hole), (z0, r_hole), (z0, r_cap)]
        paints = [RECESS, PRINT, PRINT]
    a = len(profile)
    profile += [(z1, r_cap), (z1, rp)]
    paints += [PRINT, FIBER]
    ribs = int(round(c["wound_m"] / c["rib_pitch_m"])) if q == 0 and c["rib_depth_m"] > 0 else 0
    for k in range(1, ribs * 4):  # grooves between the winding's ribs, four rings a rib
        groove = 0.5 - 0.5 * math.cos(2 * math.pi * k / 4)
        profile.append((z1 + (z2 - z1) * k / (ribs * 4), rp - c["rib_depth_m"] * groove))
        paints.append(FIBER)
    b = len(profile) + 1
    profile += [(z2, rp), (z2, r_hoop), (z3, r_hoop), (z3, 0.0)]
    paints += [FIBER, BLACK, BLACK, BLACK]
    return profile, paints, a, b


def _coil(m, q, params, layout):
    """The spool without its pack (coil_pack): the light grey rear cap with the line's hole (and at LOD0 its recessed
    three-spoke hub), the black front hoop."""
    f, _ = mount_frame(params, layout, "coil")
    profile, paints, start, end = coil_profile(params, layout, q)
    lathe(m, f, profile[:start + 1], COIL_SEGMENTS[q], paints[:start])
    lathe(m, f, profile[end:], COIL_SEGMENTS[q], paints[end:])
    if q == 0:
        _, (z0, _, _, _) = coil_radii(params, layout)
        for k in range(3):  # the spokes, flush with the rear face
            a = math.pi / 2 + k * 2 * math.pi / 3
            u, v = math.cos(a), math.sin(a)
            sf = Frame(f.at(0, 0, 0), f.dir(u, v, 0), f.dir(-v, u, 0), f.w)
            box(m, sf, (0.0095, -0.003, z0), (0.0255, 0.003, z0 + 0.008), PRINT)


def _coil_pack(m, q, params, layout):
    """The fiber pack, the spool's run between the cap's rim and the hoop's (coil_profile): its rear face, the ribbed
    winding, the hoop's rear face."""
    f, _ = mount_frame(params, layout, "coil")
    profile, paints, start, end = coil_profile(params, layout, q)
    lathe(m, f, profile[start:end + 1], COIL_SEGMENTS[q], paints[start:end])


# --- canister -----------------------------------------------------------------------------------------------------

def canister_stations(params, layout):
    """The canister along its axis from its part's centre: the nozzle's tip, the collar (the nozzle's root, the cone's
    end), the body's rear and front faces."""
    part, k = layout.part("canister"), params["canister"]
    zr, zf = -part["length_m"] / 2, part["length_m"] / 2
    zn = zr - k["cone_m"]
    return zn - k["nozzle_m"], zn, zr, zf


def fiber_exit(params, layout):
    """The line's exit: the nozzle tip's centre and the unit direction the line leaves in, down the axis aft."""
    f, _ = mount_frame(params, layout, "canister")
    return f.at(0, 0, canister_stations(params, layout)[0]), tuple(0.0 - x for x in f.w)


def canister_profile(params, layout, q):
    """The canister's lathe profile at LOD q and its paints: from the bore's end, out through the nozzle's bore and
    over its rounded lip, the rim and the flared mouth, back along the tube to the collar, up the cone, along the body
    (a lid's seam at LOD0) to its front face. LOD3 keeps the tube, the cone and the body."""
    part, k = layout.part("canister"), params["canister"]
    R = part["diameter_m"] / 2
    zt, zn, zr, zf = canister_stations(params, layout)
    rb, ro, rm, rl, rc = k["bore_d_m"] / 2, k["nozzle_d_m"] / 2, k["mouth_d_m"] / 2, k["lip_r_m"], k["collar_d_m"] / 2
    if q == 3:
        return ([(zt, 0.0), (zt, ro), (zn, ro), (zn, rc), (zr, R), (zf, R), (zf, 0.0)],
                [ZINC, ZINC, BLACK, BLACK, PRINT, PRINT])
    arc = (6, 3, 1)[q]
    profile, paints = [(zn, 0.0), (zn, rb), (zt + rl, rb)], [RECESS, ZINC]
    for i in range(1, arc + 1):  # the lip, rounded out to the rim: the radius the line turns over
        a = math.pi / 2 * i / arc
        profile.append((zt + rl - rl * math.sin(a), rb + rl - rl * math.cos(a)))
        paints.append(ZINC)
    profile += [(zt, rm), (zt + MOUTH_BAND_M, rm)]
    paints += [ZINC, ZINC]
    rho = rm - ro
    for i in range(1, arc + 1):  # the flare, concave down to the tube
        a = math.pi / 2 * i / arc
        profile.append((zt + MOUTH_BAND_M + rho - rho * math.cos(a), rm - rho * math.sin(a)))
        paints.append(ZINC)
    profile += [(zn, ro), (zn, rc), (zr, R)]
    paints += [ZINC, BLACK, BLACK]
    if q == 0:  # the lid's seam
        zs = zf - k["lid_m"]
        profile += [(zs - SEAM_M / 2, R), (zs - SEAM_M / 2, R - SEAM_M), (zs + SEAM_M / 2, R - SEAM_M),
                    (zs + SEAM_M / 2, R)]
        paints += [PRINT, RECESS, RECESS, RECESS]
    profile += [(zf, R), (zf, 0.0)]
    paints += [PRINT, PRINT]
    return profile, paints


def _canister(m, q, params, layout):
    """The canister round the coil (the class's: a body, a cone and a nozzle on one axis, tilted rear-down)."""
    f, _ = mount_frame(params, layout, "canister")
    profile, paints = canister_profile(params, layout, q)
    lathe(m, f, profile, COIL_SEGMENTS[q], paints)


def hanger_boxes(params, layout):
    """Each hanger's box (lo, hi), body axes: HANGER_HALF_M either side of the centreline, canister.hanger_len_m fore
    and aft round its station (the body's top line, canister.hanger_*_m from its front face), from the bottom plate's
    underside down to where the body's surface lies lowest under it, so the body fills its foot."""
    f, _ = mount_frame(params, layout, "canister")
    part, k = layout.part("canister"), params["canister"]
    R, c, w = part["diameter_m"] / 2, part["position_m"], f.w
    bottom, _, _ = plate_heights(params, layout)
    lift = math.sqrt(R * R - HANGER_HALF_M * HANGER_HALF_M)
    boxes = []
    for station in (k["hanger_front_m"], k["hanger_rear_m"]):
        top = f.at(0, -R, part["length_m"] / 2 - station)
        z0, z1 = top[2] - k["hanger_len_m"] / 2, top[2] + k["hanger_len_m"] / 2
        # the body's top over (x, z), its axis in the y-z plane: y = c_y + ((z - c_z) w_y + sqrt(R^2 - x^2)) / w_z
        foot = min(c[1] + ((z - c[2]) * w[1] + lift) / w[2] for z in (z0, z1))
        boxes.append(((-HANGER_HALF_M, foot, z0), (HANGER_HALF_M, bottom[0], z1)))
    return boxes


def _canister_mount(m, q, params, layout):
    """The canister's mount: two black printed hangers from the bottom plate, at LOD0 a cable tie round the body
    through each, with its head and cut tail; the fiber's connector on the body's top (LOD0-1) and its lead up to
    the plate (LOD0)."""
    if q == 3:
        return
    for lo, hi in hanger_boxes(params, layout):
        box(m, AXES, lo, hi, BLACK)
    f, _ = mount_frame(params, layout, "canister")
    part, k = layout.part("canister"), params["canister"]
    R = part["diameter_m"] / 2
    if q == 0:
        rng = random.Random(params["seed"])
        for zc, r in tie_stations(params, layout):
            lathe(m, f, [(zc - TIE_W_M / 2, r), (zc - TIE_W_M / 2, r + TIE_T_M), (zc + TIE_W_M / 2, r + TIE_T_M),
                         (zc + TIE_W_M / 2, r)], 32, [BLACK] * 4, closed=True)
            head, tail = tie_head(f, zc, r, rng.uniform(0.6, 1.0))
            hf = Frame(head, normalize(sub(tail, head)), f.w, cross(normalize(sub(tail, head)), f.w))
            box(m, hf, (-0.0035, -0.003, -0.0025), (0.0035, 0.003, 0.0025), BLACK)
            box(m, hf, (0.0035, -TIE_W_M / 2, -TIE_T_M / 2), (math.dist(head, tail), TIE_W_M / 2, TIE_T_M / 2), BLACK)
    if q <= 1:  # the fiber's fixed end leaves the body's top through its connector (the class: measured)
        zc = part["length_m"] / 2 - (k["hanger_front_m"] + k["hanger_rear_m"]) / 2
        box(m, f, (-0.006, -R - 0.008, zc - 0.008), (0.006, -R + 0.002, zc + 0.008), BLACK)
        if q == 0:
            bottom, _, _ = plate_heights(params, layout)
            foot = f.at(0, -R - 0.008, zc)
            tube(m, [foot, (foot[0], bottom[0], foot[2])], PIGTAIL_R_M, 8, LEAD_BLACK)


def tie_stations(params, layout):
    """(axial station, radius) of the two cable ties round the canister's body, each through its hanger."""
    part, k = layout.part("canister"), params["canister"]
    zf, R = part["length_m"] / 2, part["diameter_m"] / 2
    return (zf - k["hanger_front_m"], R), (zf - k["hanger_rear_m"], R)


def tie_head(f, zc, r, turn):
    """A cable tie's head on the body's side and its cut tail's end, standing off tangentially (snag hooks); `turn`
    is the head's angle from u towards v, away from the plate's side."""
    radial = f.dir(math.cos(turn), math.sin(turn), 0.0)
    tangent = f.dir(-math.sin(turn), math.cos(turn), 0.0)
    head = add(f.at(0, 0, zc), scale(radial, r + 0.003))
    return head, add(head, scale(normalize(add(tangent, scale(radial, 0.6))), 0.012))


# --- bottle -------------------------------------------------------------------------------------------------------

def bottle_length(params, layout):
    """The bottle's length base to cap, sized by its water (D-050): the payload part is the water as a solid cylinder
    (drone.json), so the drawn bottle (LOD0's profile, revolved) holds the part's volume, π R² L, below its cap. Only
    the straight body's length depends on it, so one correction from the part's length is exact."""
    part = layout.part("payload")
    R, L = part["diameter_m"] / 2, part["length_m"]
    trial = _profile_points(params, R, L, 0, ring=False)
    held = water_volume(trial[:-4])  # base to the cap's foot
    return L + (math.pi * R * R * L - held) / (math.pi * R * R)


def water_volume(points):
    """The volume a profile [(c, r)] encloses revolved about its axis, c increasing: its frustums."""
    return sum(math.pi / 3 * (c1 - c0) * (r0 * r0 + r0 * r1 + r1 * r1)
               for (c0, r0), (c1, r1) in zip(points, points[1:]))


def bottle_profile(params, layout, q=0):
    """A plain plastic bottle (D-050) on the payload part's axis and centred on it: its body the part's diameter,
    base to cap bottle_length (its water the part's), a rounded base, a shoulder to the neck, the neck's support ring
    and the cap; [(c, r)] along its axis from the base, and the paint per segment."""
    part = layout.part("payload")
    pts = _profile_points(params, part["diameter_m"] / 2, bottle_length(params, layout), q, ring=q <= 1)
    paints = [BODY] * (len(pts) - 5) + [CAP] * 4
    return pts, paints


def _profile_points(params, R, L, q, ring):
    b = params["bottle"]
    rn, rc = b["neck_d_m"] / 2, b["cap_d_m"] / 2
    c0, c4 = -L / 2, L / 2
    c3 = c4 - b["cap_h_m"]  # the cap's foot
    c2 = c3 - NECK_M  # the neck under the cap, with its support ring at its foot
    c1 = c2 - b["shoulder_m"]  # the shoulder's foot
    rb = b["base_r_m"]
    n_base, n_sh = ((4, 8), (3, 5), (2, 3), (1, 2))[q]
    pts = [(c0, 0.0)]
    for k in range(n_base + 1):  # the base's rounded edge: a quarter circle of rb
        t = math.pi / 2 * k / n_base
        pts.append((c0 + rb * (1 - math.cos(t)), R - rb + rb * math.sin(t)))
    pts.append((c1, R))
    for k in range(1, n_sh + 1):  # the shoulder: a smooth fall from R to the neck (half a cosine)
        t = k / n_sh
        pts.append((c1 + (c2 - c1) * t, rn + (R - rn) * (0.5 + 0.5 * math.cos(math.pi * t))))
    if ring:  # the neck's support ring
        pts += [(c2, rn + RING_M), (c2 + 0.002, rn + RING_M), (c2 + 0.002, rn)]
    pts += [(c3, rn), (c3, rc), (c4 - 0.001, rc), (c4, rc - 0.001), (c4, 0.0)]
    return pts


def _bottle(m, q, params, layout):
    f, _ = mount_frame(params, layout, "payload")
    prof, paints = bottle_profile(params, layout, q)
    lathe(m, f, prof, (32, 16, 10, 6)[q], paints)
    _scale_about(m, bottle_pivot(params, layout), layout.payload_scale)


def _scale_about(m, pivot, s):
    """The payload's mass rule on a drawn mesh: uniform scale s about the bottle's pivot (Layout.scaled_payload)."""
    if s != 1.0:
        m.vertices = [tuple(pivot[a] + s * (v[a] - pivot[a]) for a in range(3)) for v in m.vertices]


def bottle_strap_stations(params, layout):
    """The bottle's two straps' stations along its axis: a third and two thirds along its body."""
    L = bottle_length(params, layout)
    b = params["bottle"]
    body = L - b["cap_h_m"] - NECK_M - b["shoulder_m"]
    return [-L / 2 + body * k / 3 for k in (1, 2)]


def _bottle_straps(m, q, params, layout):
    """Two black straps round the bottle and under the plate it sits on, through slots by the plate's edges: rigid,
    the bottle does not swing (D-050)."""
    if q == 3:
        return
    f, d = mount_frame(params, layout, "payload")
    R = layout.part("payload")["diameter_m"] / 2
    gauge = params["plates"]["upper_m"] if f.v[1] > 0 else params["plates"]["bottom_m"]
    under = d + gauge  # the plate's far face from the axis
    path = circle(0.0, 0.0, R, (17, 9, 5)[q], 0.0, math.pi) + [(-R, -under), (R, -under)]
    for station in bottle_strap_stations(params, layout):
        sf = Frame(f.at(0, 0, station), f.u, f.v, f.w)
        ribbon(m, sf, path, STRAP_W_M, STRAP_T_M, (STRAP, STRAP, STRAP), closed=True)
    _scale_about(m, bottle_pivot(params, layout), layout.payload_scale)


# --- collision and snag ---------------------------------------------------------------------------------------------

def _box_shape(centre, size, rot, material):
    return {"shape": "box", "center": list(centre), "size": list(size), "rotation": list(rot), "material": material,
            "landable": False}


def _yaw(direction):
    """The quaternion turning local +x onto a horizontal direction about +y (Unity's convention)."""
    a = math.atan2(-direction[2], direction[0])
    return [0.0, math.sin(a / 2), 0.0, math.cos(a / 2)]


def _cylinder(a, b, r, material):
    shape = meta.capsule(a, b, r, material)
    shape["shape"] = "cylinder"
    shape["height"] = math.dist(a, b)
    return shape


def _collision(params, layout):
    shapes = []
    carbon, plastic = PHYSICS["carbon"], PHYSICS["plastic"]
    bottom, _, upper = plate_heights(params, layout)
    pl = layout.part("plates")
    shapes.append(_box_shape((pl["position_m"][0], (bottom[0] + upper[1]) / 2, pl["position_m"][2]),
                             (pl["size_m"][0], upper[1] - bottom[0], pl["size_m"][2]), [0, 0, 0, 1], carbon))
    a = params["arm"]
    for i, corner in enumerate(CORNERS):
        d, r_in, y = _arm_axis(layout.part(f"arm_{corner}"))
        hub = layout.hubs[i]
        r_m = math.hypot(hub[0], hub[2])
        mid = (r_in + r_m) / 2
        shapes.append(_box_shape((d[0] * mid, y, d[2] * mid), (r_m - r_in, a["thickness_m"], a["width_m"]), _yaw(d),
                                 carbon))
        shapes.append(_cylinder((hub[0], y - a["thickness_m"] / 2, hub[2]), (hub[0], y + a["thickness_m"] / 2, hub[2]),
                                a["mount_width_m"] / 2, carbon))
        motor = layout.part(f"motor_{corner}")
        x, my, z = motor["position_m"]
        shapes.append(_cylinder((x, my - motor["length_m"] / 2, z), (x, my + motor["length_m"] / 2, z),
                                motor["diameter_m"] / 2, PHYSICS["metal"]))
        p = params["prop"]
        top = hub[1] + p["hub_h_m"] / 2 + 0.0065
        shapes.append(_cylinder((hub[0], hub[1] - p["hub_h_m"] / 2, hub[2]), (hub[0], top, hub[2]), p["hub_d_m"] / 2,
                                plastic))
        for k in range(layout.blades):  # one box per blade, round its twist and sweep
            phi = math.radians(p["rest_deg"][corner]) + 2 * math.pi * k / layout.blades
            sweep_ang = layout.spins[i] * p["sweep_m"] / (layout.prop_d / 2) / 2
            dirn = (math.cos(phi + sweep_ang), 0.0, math.sin(phi + sweep_ang))
            r0, r1 = p["hub_d_m"] / 2, layout.prop_d / 2
            centre = add(hub, scale(dirn, (r0 + r1) / 2))
            shapes.append(_box_shape(centre, (r1 - r0, 0.016, p["chord_m"] + p["sweep_m"]), _yaw(dirn), plastic))
        for name in (f"clamp_{corner}",):
            if layout.has(name):
                f, size = _clamp_frame(params, layout, corner)
                b0, b1 = clamp_span(params, layout, corner)
                c = layout.part(name)["position_m"]
                shapes.append(_box_shape((c[0], (b0 + b1) / 2, c[2]), (0.035, b1 - b0, size[0]), _yaw(f.u), plastic))
        if layout.has(f"leg_{corner}"):
            leg = layout.part(f"leg_{corner}")
            x, ly, z = leg["position_m"]
            shapes.append(meta.capsule((x, ly + leg["length_m"] / 2 - leg["diameter_m"] / 2, z),
                                       (x, ly - leg["length_m"] / 2 + leg["diameter_m"] / 2, z),
                                       leg["diameter_m"] / 2, carbon))
        if layout.has(f"tjoint_{corner}"):
            tj = layout.part(f"tjoint_{corner}")
            lo, hi = box_extent(tj)
            skid = layout.part("skid_right" if tj["position_m"][0] > 0 else "skid_left")
            ground = skid["position_m"][1] - skid["diameter_m"] / 2
            top_y = hi[1] + 0.013
            shapes.append(_box_shape(((lo[0] + hi[0]) / 2, (ground + top_y) / 2, (lo[2] + hi[2]) / 2),
                                     (hi[0] - lo[0], top_y - ground, hi[2] - lo[2]), [0, 0, 0, 1], plastic))
    for side in ("left", "right"):
        if layout.has(f"pack_{side}"):
            pk = layout.part(f"pack_{side}")
            shapes.append(_box_shape(pk["position_m"], pk["size_m"], [0, 0, 0, 1], plastic))
            lo, hi = connector_box(layout, side)
            shapes.append(_box_shape([(lo[a] + hi[a]) / 2 for a in range(3)], [hi[a] - lo[a] for a in range(3)],
                                     [0, 0, 0, 1], plastic))
        if layout.has(f"skid_{side}"):
            sk = layout.part(f"skid_{side}")
            x, y, z = sk["position_m"]
            r, L = sk["diameter_m"] / 2, sk["length_m"]
            shapes.append(meta.capsule((x, y, z - L / 2 + r), (x, y, z + L / 2 - r), r, carbon))
    lo, hi = camera_box(params)
    hi = (hi[0], hi[1], params["camera"]["lens_z_m"])  # with the lens
    shapes.append(_box_shape([(lo[a] + hi[a]) / 2 for a in range(3)], [hi[a] - lo[a] for a in range(3)],
                             [0, 0, 0, 1], plastic))
    shapes += _canister_shapes(params, layout)
    if layout.has("payload"):
        shapes += _bottle_shapes(params, layout)
    return shapes


def _canister_shapes(params, layout):
    """The canister (the coil inside it): its body, its cone in three steps at their middles' radii, its nozzle and
    its flared mouth (metal: the motors' steel stands for the aluminium), its hangers."""
    f, _ = mount_frame(params, layout, "canister")
    part, k = layout.part("canister"), params["canister"]
    R, rc = part["diameter_m"] / 2, k["collar_d_m"] / 2
    zt, zn, zr, zf = canister_stations(params, layout)
    plastic, metal = PHYSICS["plastic"], PHYSICS["metal"]
    shapes = [_cylinder(f.at(0, 0, zr), f.at(0, 0, zf), R, plastic)]
    for i in range(3):
        shapes.append(_cylinder(f.at(0, 0, zn + (zr - zn) * i / 3), f.at(0, 0, zn + (zr - zn) * (i + 1) / 3),
                                rc + (R - rc) * (i + 0.5) / 3, plastic))
    shapes.append(_cylinder(f.at(0, 0, zt), f.at(0, 0, zn), k["nozzle_d_m"] / 2, metal))
    flare = MOUTH_BAND_M + (k["mouth_d_m"] - k["nozzle_d_m"]) / 2
    shapes.append(_cylinder(f.at(0, 0, zt), f.at(0, 0, zt + flare), k["mouth_d_m"] / 2, metal))
    for lo, hi in hanger_boxes(params, layout):
        shapes.append(_box_shape([(lo[a] + hi[a]) / 2 for a in range(3)], [hi[a] - lo[a] for a in range(3)],
                                 [0, 0, 0, 1], plastic))
    return shapes


def _bottle_shapes(params, layout):
    """The bottle: its body, its shoulder, its neck and cap, scaled with it."""
    f, _ = mount_frame(params, layout, "payload")
    part, b = layout.part("payload"), params["bottle"]
    R, L = part["diameter_m"] / 2, bottle_length(params, layout)
    c1 = L / 2 - b["cap_h_m"] - NECK_M - b["shoulder_m"]
    c2 = c1 + b["shoulder_m"]
    pivot, s = bottle_pivot(params, layout), layout.payload_scale
    return [dict(c, center=[pivot[a] + s * (c["center"][a] - pivot[a]) for a in range(3)], radius=c["radius"] * s,
                 height=c["height"] * s)
            for c in (_cylinder(f.at(0, 0, -L / 2), f.at(0, 0, c1), R, PHYSICS["bottle"]),
                      _cylinder(f.at(0, 0, c1), f.at(0, 0, c2), (R + b["neck_d_m"] / 2) / 2, PHYSICS["bottle"]),
                      _cylinder(f.at(0, 0, c2), f.at(0, 0, L / 2), b["cap_d_m"] / 2, PHYSICS["bottle"]))]


def _snag(params, layout):
    """Where a wire or the drone's own line can catch (the sheet's snag hazards): the skids' open ends (hooks), the
    legs and skids as edges a line rides into, and the cable ties' cut tails (hooks)."""
    snag = []
    plastic, carbon = PHYSICS["plastic"], PHYSICS["carbon"]
    for side in ("left", "right"):
        if layout.has(f"skid_{side}"):
            sk = layout.part(f"skid_{side}")
            x, y, z = sk["position_m"]
            L, r = sk["length_m"], sk["diameter_m"] / 2
            joints = sorted(layout.part(f"tjoint_{c}")["position_m"][2] for c in CORNERS
                            if layout.has(f"tjoint_{c}") and (layout.part(f"tjoint_{c}")["position_m"][0] > 0) == (x > 0))
            snag.append({"kind": "edge", "points": [[x, y, z - L / 2], [x, y, z + L / 2]], "radius_m": r,
                         "material": carbon})
            for end, joint in ((z - L / 2, joints[0]), (z + L / 2, joints[-1])):
                snag.append({"kind": "hook", "points": [[x, y, joint], [x, y, end]], "radius_m": r, "material": carbon})
    for corner in CORNERS:
        if layout.has(f"leg_{corner}"):
            leg = layout.part(f"leg_{corner}")
            x, y, z = leg["position_m"]
            snag.append({"kind": "edge", "points": [[x, y + leg["length_m"] / 2, z], [x, y - leg["length_m"] / 2, z]],
                         "radius_m": leg["diameter_m"] / 2, "material": carbon})
    f, _ = mount_frame(params, layout, "canister")
    rng = random.Random(params["seed"])
    for zc, r in tie_stations(params, layout):
        head, tail = tie_head(f, zc, r, rng.uniform(0.6, 1.0))
        snag.append({"kind": "hook", "points": [list(head), list(tail)], "radius_m": TIE_W_M / 2, "material": plastic})
    return snag
