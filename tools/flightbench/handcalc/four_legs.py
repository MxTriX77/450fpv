"""Hand numbers for openspec/changes/w17-four-legs/design.md (task 1.1): a leg's tip as a small penetrometer in each
surface's soil, the start's embedment, a careful liftoff's release leg by leg, and touchdowns at 0.5 and 1.5 m/s, dry,
wet and saturated, for the loaded drone on four separate legs (the skids gone). It also prints model 103's skid numbers
the baseline is read against. Run: python tools/flightbench/handcalc/four_legs.py (prints every number the design
quotes). Each input is labelled measured, published, derived or assumed where it is set."""
import json
import math
import os

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "..")
DRONE = json.load(open(os.path.join(ROOT, "assets-src", "drone", "drone.json"), encoding="utf-8"))
# Model 103's drone on its skids (kept as the audits' fixture, w17-four-legs 1.2): what left the drone is read from it.
SKIDS = json.load(open(os.path.join(ROOT, "assets-src", "drone", "drone_skids.json"), encoding="utf-8"))
SURF = json.load(open(os.path.join(ROOT, "assets-src", "surfaces.json"), encoding="utf-8"))
G = 9.81


def v(block, key):
    return block[key]["value"]


# ---------------------------------------------------------------------------------------------------------------------
# The drone on four legs (derived from drone.json and the pilot's logged headers)
# ---------------------------------------------------------------------------------------------------------------------
LEGS = DRONE["legs"]
TUBE = LEGS["tube"]
PARTS = {p["name"]: p for p in DRONE["parts"]}
D_OUT = v(TUBE, "outer_diameter_m")                       # measured (T)
D_IN = v(TUBE, "inner_diameter_m")                        # published
E_TUBE = v(TUBE, "modulus_pa")                            # published (PVC-U)
SIGMA_Y = v(TUBE, "yield_stress_pa")                      # published
K_T_EDGE = v(LEGS["clamp"], "edge_stress_factor")         # assumed (drone.json)
K_ARM = v(LEGS["clamp"], "mount_stiffness_n_per_m")       # derived (drone.json)
# What left the drone with the skids (derived, drone_skids.json's parts, w17-four-legs 1.2): the two skids and the four T-joints.
GONE = [p for p in SKIDS["parts"] if p["name"].startswith(("skid_", "tjoint_"))]
SKID_MASS = sum(v(p, "mass_kg") for p in GONE)
# The tube runs from the clamp's bottom (clamp_rr: y -0.01125 - 0.00825) to the ground at the old skids' underside
# (y -0.253): the leg's free length once its own end is the foot (derived; 1.2's tip may move it).
CLAMP_BOTTOM = v(PARTS["clamp_rr"], "position_m")[1] - v(PARTS["clamp_rr"], "size_m")[1] / 2
TIP_Y = v(PARTS["leg_rr"], "position_m")[1] - v(PARTS["leg_rr"], "length_m") / 2
L_FREE = CLAMP_BOTTOM - TIP_Y
LEG_X = abs(v(PARTS["leg_rr"], "position_m")[0])          # the stance's half width, both ways (derived)
R_LEG = math.hypot(LEG_X, LEG_X)                          # a leg's distance from the centre in plan
A_TUBE = math.pi * (D_OUT ** 2 - D_IN ** 2) / 4
I_TUBE = math.pi * (D_OUT ** 4 - D_IN ** 4) / 64
EI = E_TUBE * I_TUBE
K_AXIAL = 1 / (1 / (E_TUBE * A_TUBE / L_FREE) + 1 / K_ARM)  # the tube's EA/L in series with the arm (the skid's ring gone)
K_SIDE = 3 * EI / L_FREE ** 3                              # the cantilever at its tip (p7g-legs 1.1's law)
ZETA_LEG = 0.2                                             # assumed (drone.json legs.damping_n_s_per_m: the sheet's 0.1-0.3)

# The loaded drone as the pilot flew it (measured: the logs' headers, model 103): 1 kg payload in Sunny, 2 kg in Severe
# rain; mass, centre of mass (body y; the tips at y -0.253) and inertia about the pitch (x) and roll (z) axes.
FLOWN = {
    "1 kg": dict(m=5.0804, com_y=-0.023497, ixx=0.056339, izz=0.051493),
    "2 kg": dict(m=6.0804, com_y=-0.005217, ixx=0.075337, izz=0.063790),
}


def own_inertia(p):
    """A part's own inertia about axes through its centre parallel to body x (pitch) and z (roll): a box's m(b^2 + c^2)/12, a
    cylinder along body z its rod's m(3r^2 + L^2)/12 across and m r^2/2 about its axis (derived)."""
    m = v(p, "mass_kg")
    if p["kind"] == "box":
        sx, sy, sz = v(p, "size_m")
        return m * (sy * sy + sz * sz) / 12, m * (sx * sx + sy * sy) / 12
    r, length = v(p, "diameter_m") / 2, v(p, "length_m")
    return m * (3 * r * r + length * length) / 12, m * r * r / 2


def build(load):
    """The drone of one loadout without what left with the skids (derived): the two skids and four T-joints (68.6 g, drone_skids.json)
    taken off model 103's logged build, their own inertia and their parallel axes about the old centre of mass, then the inertia
    moved to the new centre of mass (the parallel axis theorem)."""
    f = FLOWN[load]
    m = f["m"] - SKID_MASS
    com_y = (f["m"] * f["com_y"] - sum(v(p, "mass_kg") * v(p, "position_m")[1] for p in GONE)) / m
    h = com_y - TIP_Y                                       # the centre of mass over the tips
    ixx, izz = f["ixx"], f["izz"]
    for p in GONE:
        x, y, z = v(p, "position_m")
        ox, oz = own_inertia(p)
        mp = v(p, "mass_kg")
        ixx -= ox + mp * ((y - f["com_y"]) ** 2 + z * z)
        izz -= oz + mp * ((y - f["com_y"]) ** 2 + x * x)
    shift = com_y - f["com_y"]
    ixx -= m * shift * shift
    izz -= m * shift * shift
    i_diag = (ixx + izz) / 2                                # about a diagonal through the centre (the last corner's axis)
    share = i_diag / (i_diag + m * R_LEG ** 2)             # the last held corner's share of the thrust's excess
    c_leg = 2 * ZETA_LEG * math.sqrt(K_AXIAL * m / 4)
    return dict(m=m, h=h, ixx=ixx, izz=izz, i_diag=i_diag, share=share, w_leg=m * G / 4, c_leg=c_leg)


# ---------------------------------------------------------------------------------------------------------------------
# The tip in soil: the new labelled inputs (design decision 1)
# ---------------------------------------------------------------------------------------------------------------------
# A flat tip's resistance in each surface's dry set (Sunny), Pa, read at Z_REF: assumed, inside the published order of
# field penetrometer readings (crop roots slow past 1.5 MPa and stop near 2.5 MPa at field capacity, soilquality.org.au;
# grazed and ungrazed pasture 0.8-3 MPa at 4-8 cm, the Wisconsin pasture study; trafficked passes 2.3-5.4 MPa, the
# Debrecen wheel-track study). The wet and saturated sets take surfaces.json's own wetting.bearing_wet and
# bearing_saturated (derived): the published direction, a soil's penetration resistance falling severalfold as it wets.
Q_TIP = {
    "meadow_sod": 1.5e6,     # root-bound sod over loam: pasture's middle
    "dry_crust": 3.0e6,      # a dry crusted silt: past the roots' stop
    "crater_spoil": 0.4e6,   # loose churned spoil
    "belt_straw": 0.8e6,     # soft moist loam under straw
    "belt_bare": 1.5e6,      # firm bare loam
    "tilled": 0.4e6,         # a loose tilled seedbed
    "burnt_field": 2.0e6,    # crusted loam under ash
    "weeds": 1.5e6,          # firm loam under weeds
    "yard_litter": 2.5e6,    # compacted yard soil
    "rubble": 5.0e6,         # packed brick and mortar debris: the trafficked passes' top
    "dirt_track": 4.0e6,     # a wheel-compacted silt loam track
}
Z_REF = 0.025      # m, derived: the middle of the 0-5 cm layer field readings of a topsoil average over; the resistance
                   # grows from nothing at the loose surface to Q_TIP there and on (assumed form: Bekker's n = 1 line)
ALPHA_E = 2.5      # published: the soil's modulus over its penetration resistance for an axisymmetric load
                   # (Schmertmann, Hartman and Brown 1978)
NU = 0.3           # assumed: a loam's drained Poisson's ratio (the published 0.25-0.35 order)
N_KT = 15.0        # published: penetration resistance over undrained shear strength, 10-20 (Lunne, Robertson and
                   # Powell 1997)
J_MATLOCK = 0.5    # published: Matlock 1970's soft-clay coefficient, p_u = (3 + J z/D) c_u D up to 9 c_u D
K0 = 0.5           # derived: Jaky's at-rest ratio 1 - sin(phi) at phi 30 deg (assumed; Potyondy's phi 30-40 deg the
                   # table's notes cite): the known-wrong lower bound of a dry shaft's grip
R_SHAFT = 0.01     # assumed: the shaft's friction over the tip resistance at its depth, both ways (the soil's grip on a
                   # pushed-in shaft): CPT friction ratios of silt mixtures 2-5 % (Robertson 1990), halved for a shaft
                   # pulled near the free surface (a shaft in tension carries 0.5-0.8 of its compression friction in
                   # sands, De Nicola and Randolph 1993; the push's radial stress is unconfined above it)
SETDOWN = (1.0, 1.5)  # assumed: each tip's set-down load over its static share (the start's draw, decision 4): a
                      # careful hand lets go as the tips touch, pressing a leg at most half its share again
A_TIP = math.pi * D_OUT ** 2 / 4    # the cut end taken as a plugged disc of the tube's outer diameter (assumed)
R_TIP = D_OUT / 2
# The elastic return a tip keeps when it is unloaded (derived): Sneddon's rigid punch, k_e = 2 a E / (1 - nu^2), on
# E = ALPHA_E * q(z_m), q(z) = k_t z, over the loading line's k_t A: a fixed distance, whatever the depth.
RETURN = A_TIP * (1 - NU ** 2) / (2 * R_TIP * ALPHA_E)


def soil(surface, moisture):
    """A surface's set from surfaces.json and its tip numbers (moisture: dry, wet, saturated)."""
    row = SURF["surfaces"][surface][moisture]
    s = {k: v(row, k) for k in row}
    wet = SURF["wetting"]
    factor = 1.0 if moisture == "dry" else v(wet, "bearing_wet") if moisture == "wet" else v(wet, "bearing_saturated")
    if surface not in Q_TIP:
        s.update(rigid=True, q=math.inf, k_t=math.inf)
        return s
    q = Q_TIP[surface] * factor
    s.update(rigid=False, q=q, k_t=q / Z_REF)
    return s


def tip_force(s, z, z_m):
    """The soil's push on a tip at depth z with its deepest z_m (Wong's rule on the tip's own lines): the loading line
    k_t A z past z_m, else the unloading line from (z_m, k_t A z_m) at the slope max(k_t A, k_e(z_m)); the firm layer
    past max_sink (surfaces.json). Never negative."""
    if s["rigid"] or z <= 0:
        return 0.0
    ka = s["k_t"] * A_TIP
    firm = SURF["constants"]["firm_layer_n_per_m3"]["value"] * A_TIP
    if z >= z_m:
        f = ka * z
    else:
        ku = max(ka, ka * z_m / RETURN)
        f = max(0.0, ka * z_m - ku * (z_m - z))
    if z > s["max_sink_m"]:
        f += firm * (z - s["max_sink_m"])
    return f


def sink_at(s, f):
    """The depth the loading line reaches under a push f (below the firm layer's start)."""
    return 0.0 if s["rigid"] else f / (s["k_t"] * A_TIP)


def rest_after(s, peak, rest):
    """A tip pushed to `peak` N, then left at `rest` N: its deepest sink and its depth at rest on the unloading line."""
    z_m = sink_at(s, peak)
    if s["rigid"]:
        return 0.0, 0.0
    ka = s["k_t"] * A_TIP
    ku = max(ka, ka * z_m / RETURN)
    return z_m, z_m - (peak - rest) / ku


def shaft_grip(s, z_e, r_shaft=R_SHAFT):
    """The soil's grip on the embedded shaft, dry or wet: r_s q(z) over pi D dz, r_s k_t pi D z_e^2 / 2 (derived)."""
    return 0.0 if s["rigid"] else r_shaft * s["k_t"] * math.pi * D_OUT * z_e ** 2 / 2


def hold_vertical(s, z_e, r_shaft=R_SHAFT):
    """A tip's hold on the way out at embedment z_e, all of it friction and adhesion (nothing stored): the shaft's grip
    (shaft_grip); wet, the tip's suction-adhesion sigma_a A (the cohesive law's peak, surfaces.json) and the shaft's
    interface adhesion c_a pi D z_e. Also the known-wrong at-rest friction mu K0 rho g pi D z_e^2 / 2 (derived)."""
    if s["rigid"]:
        return 0.0, 0.0, 0.0, 0.0, 0.0
    tipf = s["adhesion_pa"] * A_TIP
    wet = s["interface_adhesion_pa"] * math.pi * D_OUT * z_e
    grip = shaft_grip(s, z_e, r_shaft)
    at_rest = s["friction_static"] * K0 * s.get("bulk_density_kg_m3", 0) * G * math.pi * D_OUT * z_e ** 2 / 2
    return tipf + wet + grip, tipf, wet, grip, at_rest


def hold_lateral(s, z_e):
    """The soil in front of a dug-in tip (Matlock 1970's p_u = (3 + J z/D) c_u D, c_u = q(z)/N_kt), integrated over the
    embedded shaft: (k_t D / N_kt)(1.5 z^2 + J z^3 / (3 D)) (derived, for z below 12 D)."""
    if s["rigid"] or z_e <= 0:
        return 0.0
    return s["k_t"] * D_OUT / N_KT * (1.5 * z_e ** 2 + J_MATLOCK * z_e ** 3 / (3 * D_OUT))


# ---------------------------------------------------------------------------------------------------------------------
# A level touchdown, the throttle cut (thrust 0, assumed: the idle's few per cent left out), four identical legs, each
# its axial spring and damper in series with the tip's soil (a massless node), the soil's damping c_s A (surfaces.json).
# ---------------------------------------------------------------------------------------------------------------------
_TOUCH = {}


def touchdown(s, d, speed, dt=1e-5, t_end=2.0):
    """A quarter of the drone on one leg (the four alike), from the tip's first touch at `speed` down: the body's speed
    and the leg's and the soil's work, its first rebound (the speed the leg leaves the soil with) and when it comes to
    rest (its speed under 1 mm/s from then on)."""
    key = (s.get("q"), s.get("damping_ns_per_m3"), s.get("max_sink_m"), speed, d["m"])
    if key in _TOUCH:
        return _TOUCH[key]
    m4 = d["m"] / 4
    k, c = K_AXIAL, d["c_leg"]
    cs = 0.0 if s["rigid"] else s["damping_ns_per_m3"] * A_TIP
    delta, vel = 0.0, -speed        # the leg top's approach past the first touch (m, into the ground), the body's velocity
    z = z_m = 0.0                   # the tip's depth and its deepest
    w_leg_damp = w_soil_damp = w_soil = 0.0
    peak_f = peak_acc = 0.0
    t = 0.0
    rebound_v, bounces, rest_t = None, 0, None
    was = True
    while t < t_end:
        ddelta = -vel
        zdot = 0.0
        x = delta - z                    # the leg's compression: it touches while it is compressed (the sim's rule)
        contact = x > 0
        fleg = 0.0
        if contact:
            if s["rigid"]:
                fleg = k * x + c * ddelta
            else:
                # the massless node between the leg and the soil: k x + c (ddelta - zdot) = F_s(z) + cs zdot + the
                # shaft's grip against zdot's way; the soil never pulls, so with nothing under the tip it stays put
                fs = tip_force(s, z, z_m)
                grip = shaft_grip(s, z)
                drive = k * x + c * ddelta - fs
                if drive > grip:
                    zdot = (drive - grip) / (c + cs)
                elif drive < -grip and fs > 0:
                    zdot = (drive + grip) / (c + cs)
                else:
                    zdot = 0.0
                fleg = k * x + c * (ddelta - zdot)
            fleg = max(0.0, fleg)        # the leg's force at least 0, as the contact model's (K§7.1 step 8)
        elif was and vel > 0:
            bounces += 1                 # the leg has left the ground on its way up: a rebound
            if rebound_v is None:
                rebound_v = vel
        was = contact
        if contact:
            w_leg_damp += c * (ddelta - zdot) ** 2 * dt
            w_soil_damp += cs * zdot ** 2 * dt
            if not s["rigid"]:
                w_soil += (tip_force(s, z, z_m) + math.copysign(shaft_grip(s, z), zdot)) * zdot * dt
        acc = (fleg - m4 * G) / m4
        peak_f = max(peak_f, fleg)
        peak_acc = max(peak_acc, acc)
        vel += acc * dt
        delta -= vel * dt
        z += zdot * dt
        z_m = max(z_m, z)
        t += dt
        if abs(vel) < 1e-3:
            if rest_t is None and t > 0.02:
                rest_t = t
        else:
            rest_t = None
    rv = rebound_v or 0.0
    r = dict(peak_f=peak_f, peak_g=peak_acc / G + 1, z_m=z_m, z_end=z, rebound_v=rv, h_rebound=rv * rv / (2 * G),
             e_in=0.5 * m4 * speed ** 2 + m4 * G * z_m, w_soil=w_soil, w_leg_damp=w_leg_damp, w_soil_damp=w_soil_damp,
             e_rebound=0.5 * m4 * rv * rv, rest_t=rest_t, bounces=bounces)
    _TOUCH[key] = r
    return r


def nose_over_speed(d):
    """The speed along or across at which a drone whose tips catch goes over the front tips' line (rigid legs, throttle
    cut, no controller): (m v h)^2 / (2 I_p) = m g dh, I_p = I + m (h^2 + a^2) (derived)."""
    a, h = LEG_X, d["h"]
    rho2 = h * h + a * a
    dh = math.sqrt(rho2) - h
    out = {}
    for axis, i0 in (("along (pitch)", d["ixx"]), ("across (roll)", d["izz"])):
        ip = i0 + d["m"] * rho2
        out[axis] = math.sqrt(2 * ip * G * dh / d["m"]) / h
    return out, dh


def skid_nose_over(d, half_length=0.225):
    """The same for model 103's skids along the drone (half length 0.225 m)."""
    h = d["h"]
    rho2 = h * h + half_length ** 2
    dh = math.sqrt(rho2) - h
    ip = d["ixx"] + d["m"] * rho2
    return math.sqrt(2 * ip * G * dh / d["m"]) / h


def main():
    print("== The leg without its skid (derived)")
    print(f"free length {L_FREE:.4f} m (clamp's bottom {CLAMP_BOTTOM:.4f} to the tip {TIP_Y:.4f}); stance {2 * LEG_X:.3f} m square,"
          f" a leg {R_LEG:.4f} m from the centre")
    print(f"axial k {K_AXIAL:.0f} N/m (the tube's EA/L {E_TUBE * A_TUBE / L_FREE:.0f} in series with the arm {K_ARM:.0f}); was"
          f" {v(LEGS, 'stiffness_n_per_m'):.0f} with the skid's ring")
    print(f"side give at the tip 3EI/L^3 {K_SIDE:.1f} N/m (EI {EI:.4f} N m^2); at drone.json's 0.2135 m {3 * EI / 0.2135 ** 3:.1f}")
    my = SIGMA_Y * I_TUBE / (D_OUT / 2)
    zp = (D_OUT ** 3 - D_IN ** 3) / 6
    print(f"clamp edge: first local yield M_y/K_t {my / K_T_EDGE:.3f} N m ({my / K_T_EDGE / L_FREE:.2f} N across at the tip),"
          f" yield M_y {my:.3f} N m ({my / L_FREE:.2f} N), plastic M_p {SIGMA_Y * zp:.3f} N m ({SIGMA_Y * zp / L_FREE:.2f} N)")
    print(f"buckling: clamped-free {math.pi ** 2 * EI / (4 * L_FREE ** 2):.1f} N (a tip free to slide), clamped-pinned"
          f" {2.046 * math.pi ** 2 * EI / L_FREE ** 2:.0f} N (a tip held in soil); the anchors' reach pi^2 L/12 {math.pi ** 2 * L_FREE / 12:.4f} m")
    print(f"tip: a plugged disc of {D_OUT * 1000:.0f} mm, {A_TIP:.4e} m^2; its elastic return on unloading {RETURN * 1000:.2f} mm")
    for load in FLOWN:
        d = build(load)
        no, dh = nose_over_speed(d)
        print(f"{load}: mass {d['m']:.4f} kg (skids {SKID_MASS * 1000:.1f} g off), centre of mass {d['h']:.4f} m over the tips,"
              f" Ixx {d['ixx']:.4f} Izz {d['izz']:.4f} I_diag {d['i_diag']:.4f}, a leg's static share {d['w_leg']:.2f} N,"
              f" the last corner's share of the excess {d['share']:.3f}, leg damping {d['c_leg']:.0f} N s/m")
        print(f"   tips that catch go over at {no['along (pitch)']:.2f} m/s along, {no['across (roll)']:.2f} m/s across (CoM rises"
              f" {dh * 1000:.1f} mm); the skids along held to {skid_nose_over(d):.2f} m/s; tipping by friction alone"
              f" needs mu > a/h = {LEG_X / d['h']:.3f}")

    d = build("2 kg")
    print("\n== Model 103's skids (the baseline's hand numbers, 2 kg loadout)")
    for moist in ("wet", "saturated"):
        s = SURF["surfaces"]["meadow_sod"][moist]
        sa = v(s, "adhesion_pa")
        print(f"meadow_sod {moist}: a corner's half-skid's wetted footprint 2.2e-3 m^2 holds {sa * 2.2e-3:.1f} N (sigma_a {sa:.0f} Pa);"
              f" four: {4 * sa * 2.2e-3:.1f} N = {4 * sa * 2.2e-3 / (d['m'] * G):.2f} W, so the last release needs T/W >= "
              f"{1 + 4 * sa * 2.2e-3 / (d['m'] * G):.2f} if they let go together")

    print("\n== Each surface's tip (static share of the 2 kg loadout, the set-down draw's bounds)")
    print("surface      set        q_t MPa  k_t N/m3   static mm  set down 1x / 1.5x: rest mm (deepest)   vertical hold N at"
          " 1.5x  lateral N at 1.5x   last corner's excess N")
    for surface in ("meadow_sod", "tilled", "dry_crust", "asphalt", "belt_straw", "weeds", "dirt_track", "crater_spoil"):
        for moist in ("dry", "wet", "saturated"):
            s = soil(surface, moist)
            w = d["w_leg"]
            if s["rigid"]:
                print(f"{surface:12s} {moist:9s}  rigid: no tip enters; Coulomb mu_s {s['friction_static']:.2f} only")
                continue
            z1m, z1 = rest_after(s, SETDOWN[0] * w, w)
            z2m, z2 = rest_after(s, SETDOWN[1] * w, w)
            hv, hv_tip, hv_wet, hv_grip, hv_rest = hold_vertical(s, z2)
            hl = hold_lateral(s, z2)
            print(f"{surface:12s} {moist:9s}  {s['q'] / 1e6:6.3f}  {s['k_t']:.3e}  {sink_at(s, w) * 1000:7.2f}"
                  f"     {z1 * 1000:6.2f} / {z2 * 1000:6.2f} ({z2m * 1000:.2f})"
                  f"              {hv:5.2f} (grip {hv_grip:.2f}, tip {hv_tip:.2f}, wet shaft {hv_wet:.2f}; at rest {hv_rest:.4f})"
                  f"   {hl:6.2f}          {hv / d['share']:5.2f} ({hv / d['share'] / (d['m'] * G) * 100:.1f} % W)")

    print("\n== Known-wrong: surfaces.json's own Bekker law at the tip's width (b its radius) under the static share, and a"
          " touchdown's energy on it (undamped, a bound)")
    for surface in ("meadow_sod", "tilled", "dry_crust", "dirt_track"):
        for moist in ("dry", "wet", "saturated"):
            row = SURF["surfaces"][surface][moist]
            kb = v(row, "k_c_n_per_m2") / R_TIP + v(row, "k_phi_n_per_m3")
            ka = kb * A_TIP
            st = d["w_leg"] / ka
            z05 = st + math.sqrt(2 * 0.5 * d["m"] / 4 * 0.5 ** 2 / ka)
            z15 = st + math.sqrt(2 * 0.5 * d["m"] / 4 * 1.5 ** 2 / ka)
            print(f"{surface:12s} {moist:9s}: k {kb:.3e} N/m3, static {st * 1000:.1f} mm (max_sink {v(row, 'max_sink_m') * 1000:.0f} mm);"
                  f" a 0.5 m/s touchdown about {z05 * 1000:.0f} mm, 1.5 m/s about {z15 * 1000:.0f} mm")

    print("\n== A careful liftoff: the last held corner (2 kg loadout, set down at 1.5x)")
    print("measured T/W climb at the pilot's liftoffs (model 103, meadow): Sunny median 0.72 /s, Severe rain 0.41 /s")
    for surface in ("meadow_sod", "tilled", "dry_crust", "asphalt"):
        for moist, rate in (("dry", 0.72), ("wet", 0.41), ("saturated", 0.41)):
            s = soil(surface, moist)
            if s["rigid"]:
                print(f"{surface:12s} {moist:9s}: no hold; every leg leaves as its load reaches nothing")
                continue
            _, z2 = rest_after(s, SETDOWN[1] * d["w_leg"], d["w_leg"])
            hv = hold_vertical(s, z2)[0]
            excess = hv / d["share"]
            t_hold = excess / (rate * d["m"] * G)
            turn = 0.5 * hv * R_LEG / d["i_diag"] * min(t_hold, 0.05) ** 2
            e_store = 0.5 * hv ** 2 / K_AXIAL
            v_store = math.sqrt(2 * 4 * e_store / d["m"])
            print(f"{surface:12s} {moist:9s}: hold {hv:.2f} N lets go at T - W = {excess:.2f} N (T/W {1 + excess / (d['m'] * G):.3f});"
                  f" held {t_hold * 1000:.0f} ms after T = W; its moment {hv * R_LEG:.3f} N m turns the body"
                  f" {math.degrees(turn):.2f} deg toward it in the first 50 ms (no controller); stored in its leg"
                  f" {e_store * 1e6:.1f} uJ, all four's worth {v_store:.4f} m/s; the speed 0.3 s after the release"
                  f" {excess / d['m'] * 0.3:.3f} m/s")
    print("model 103 measured (Severe rain, meadow): T/W at the last release 1.31-1.71 (median 1.47), up 0.34-0.52 m/s"
          " (median 0.46) as it lets go, 0.26-0.45 m in the first 0.3 s; each corner pulled 9.1-10.7 N")

    print("\n== The liftoff after a touchdown: each tip's hold at the depth the landing left it (2 kg loadout)")
    for surface in ("meadow_sod", "tilled", "dry_crust"):
        for moist in ("dry", "wet", "saturated"):
            s = soil(surface, moist)
            for speed in (0.5, 1.5):
                r = touchdown(s, d, speed)
                hv = hold_vertical(s, r["z_end"])[0]
                excess = hv / d["share"]
                print(f"{surface:12s} {moist:9s} after {speed:.1f} m/s: the tip {r['z_end'] * 1000:5.1f} mm deep holds {hv:5.2f} N"
                      f" (four {4 * hv:5.1f} N, {4 * hv / (d['m'] * G) * 100:4.1f} % W); the last corner lets go at T/W"
                      f" {1 + excess / (d['m'] * G):.3f}; sideways it holds {hold_lateral(s, r['z_end']):6.1f} N")
    print("the shaft's grip's share r_s (assumed 0.01) against its bounds, meadow_sod, a leg's hold N:")
    for moist in ("dry", "wet", "saturated"):
        s = soil("meadow_sod", moist)
        _, z_rest = rest_after(s, SETDOWN[1] * d["w_leg"], d["w_leg"])
        z_hard = touchdown(s, d, 1.5)["z_end"]
        row = []
        for rs in (0.0, 0.01, 0.02, 0.04):
            row.append(f"r_s {rs:.2f}: rest {hold_vertical(s, z_rest, rs)[0]:.2f}, after 1.5 m/s {hold_vertical(s, z_hard, rs)[0]:.2f}")
        print(f"  {moist:9s} " + "; ".join(row))

    print("\n== Touchdowns, level, the throttle cut (2 kg loadout); the tips start at the surface")
    print("surface      set        v m/s  peak N/leg  peak g  depth mm (end)  rebound m/s  h mm   energy per leg J: in (kinetic +"
          " weight over the sink) -> soil (net), leg damping, soil damping, the first rebound's; rest s")
    for surface in ("meadow_sod", "tilled", "dry_crust", "asphalt"):
        for moist in ("dry", "wet", "saturated"):
            s = soil(surface, moist)
            for speed in (0.5, 1.5):
                r = touchdown(s, d, speed)
                print(f"{surface:12s} {moist:9s}  {speed:4.1f}  e {r['rebound_v'] / speed:.2f}  {r['peak_f']:8.1f}  {r['peak_g']:6.2f}"
                      f"  {r['z_m'] * 1000:6.2f}"
                      f" ({r['z_end'] * 1000:5.2f})  {r['rebound_v']:8.3f}  {r['h_rebound'] * 1000:5.1f}   {r['e_in']:.3f} -> "
                      f"{r['w_soil']:.3f}, {r['w_leg_damp']:.3f}, {r['w_soil_damp']:.3f}, rebound {r['e_rebound']:.3f};"
                      f" contacts lost {r['bounces']}; rest {r['rest_t'] if r['rest_t'] is None else round(r['rest_t'], 3)}")
    print("model 103 measured: dry crust (Severe rain) at 1.57 m/s: up 0.49 m/s after, 21 mm, rocking 1.4 s; dirt track at"
          " 1.0 m/s down and 1.5 along: 8 bounces to 57 mm, slid 1.84 m; meadow at 1.52 m/s cut: 7 bounces, legs cracked, on"
          " its side (54.6 deg)")

    print("\n== Along the ground after a touchdown (2 kg loadout): the friction's impulse in the impact, the tips' hold")
    for surface in ("meadow_sod", "tilled", "dry_crust", "asphalt"):
        for moist in ("dry", "saturated"):
            s = soil(surface, moist)
            for speed in (0.5, 1.5):
                r = touchdown(s, d, speed)
                dv = speed + r["rebound_v"]
                arrest = s["friction_kinetic"] * dv
                hl = hold_lateral(s, r["z_end"])
                print(f"{surface:12s} {moist:9s} {speed:.1f} m/s: the impact's friction stops up to {arrest:.2f} m/s along;"
                      f" the tips then hold {4 * hl:.1f} N together ({4 * hl / (d['m'] * G):.2f} W) at {r['z_end'] * 1000:.1f} mm"
                      f" against a push with no weight on them")
    no, _ = nose_over_speed(d)
    ke = 0.5 * d["m"] * no["along (pitch)"] ** 2
    bend = math.sqrt(2 * ke / (4 * K_SIDE))
    print(f"a forward {no['along (pitch)']:.2f} m/s on four held tips: {ke:.2f} J bends the legs {bend * 1000:.0f} mm"
          f" ({K_SIDE * bend:.1f} N a tip against the yield's {SIGMA_Y * I_TUBE / (D_OUT / 2) / L_FREE:.1f} N): the legs give"
          f" before the drone goes over")


if __name__ == "__main__":
    main()
