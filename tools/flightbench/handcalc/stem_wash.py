"""Hand numbers for design.md 2.13: the rotors' wash on a standing stem with the drone's height, by p7d-tether design 19's
law (RotorAir: decision 4's near columns, Bauersfeld et al.'s merged jet from 2.5 l, its landing's wall jet and stagnation
flow, the near landings' fountain), worked again here for a level hover over flat ground in still air, against 2.12's law
(q = T/A on every stem based under a disc) and each stalk's folding load (2.12's lean law). Independent arithmetic: nothing
here is read from the C#; the table's numbers are tether.json's and drone.json's."""
import math

# --- the drone in the stem scenarios' hover (SoilScenarios: 4.735 kg, drone.json's hubs and props) --------------------
G = 9.80665
MASS = 4.735                  # kg, measured in the scenario's setup (FlightCore.Body.Mass)
T = MASS * G / 4              # N a rotor in the hover (derived)
R = 0.127                     # m, the 10-inch props' radius (drone.json, published)
HUB = 0.1598                  # m, each hub's x and z off the centre (drone.json, published: the 452 mm wheelbase)
RHO = 1.225                   # kg/m³, tether.json's fiber.air_density_kg_m3 (published, ISA)

# --- tether.json's rotor_air rows (design 4, 1.6b, 19) --------------------------------------------------------------
WAKE_SPEED = 2.0              # far_wake_speed_ratio (published)
WAKE_RADIUS = 0.7071 * R      # far_wake_radius_ratio (published)
H_J = 0.3536 * R              # wall_jet_thickness_over_radius (derived)
MERGE = (1.0, 2.5)            # wake_merge_over_motor_distance (published)
BD, S, S0 = 10.11, 0.07668, -5.817   # far_jet_decay, _spreading_rate, _virtual_origin_over_motor_distance (published)
C_WALL, E_WALL, DECAY = 0.098, -0.1, 1.1   # wall_jet_half_height_ratio, _exponent, wall_jet_decay_exponent (published)
PA, PB, F2 = 0.68, 1.48, 0.7514   # wall_jet_profile [a, b] (published), its ∫f² (derived)
FOUNTAIN, SPREAD = 0.711, 0.1     # fountain_momentum_ratio (derived), plane_jet_spreading_rate (published)
LN2 = math.log(2)
PROFILE_A = math.sqrt(2) - 1

# --- 2.12's stalks (surfaces.json's cover rows, stem_stalks.py) --------------------------------------------------------
CD = 1.15
EXP = 1 / 1.53
KINDS = {"dry": (4.49e-3, 0.125, 400., 2.0e9), "green": (1.81e-3, 0.125, 1000., 6.4e9)}

A_DISC = math.pi * R * R
V_H = math.sqrt(T / (2 * RHO * A_DISC))
HUBS = [(HUB, -HUB), (HUB, HUB), (-HUB, -HUB), (-HUB, HUB)]   # rr, fr, rl, fl (x right, z forward)
L_MOTOR = 0.5 * sum(math.hypot(x, z) for x, z in HUBS)        # 2 × the hubs' mean distance from their centre


def merged(s):
    t = (s / L_MOTOR - MERGE[0]) / (MERGE[1] - MERGE[0])
    return min(1.0, max(0.0, t))


def landing(h):
    """The merged jet's landing under hubs h over the ground: its weight w(s_g), U_g, r_i and H."""
    w = merged(h)
    if w <= 0:
        return w, 0.0, 0.0, 0.0
    x = h / L_MOTOR - S0
    half = S * x * L_MOTOR
    Ug = V_H * BD / x
    H = h - S0 * L_MOTOR
    c, ri = C_WALL, half
    for _ in range(4):
        ri = half / math.sqrt(6 * PROFILE_A * c * F2)
        c = C_WALL * (ri / H) ** E_WALL
    return w, Ug, ri, H


def verhoff(eta):
    return PB * eta ** (1 / 7) * (1 - math.erf(PA * eta)) if eta > 0 else 0.0


def air(h, px, py, pz):
    """The rotors' added air at (px, py, pz) (world m, the ground at y 0), hubs h up: (u_x, u_y, u_z) m/s, and the parts."""
    near = 1 - merged(h)
    share = 1 - merged(h - py)
    ux = uy = uz = 0.0
    parts = {}
    column = False
    keys = []
    for i, (hx, hz) in enumerate(HUBS):
        dx, dz = px - hx, pz - hz
        f2 = dx * dx + dz * dz
        if f2 > WAKE_RADIUS ** 2:
            keys.append((V_H * V_H / f2, i))
        if py > h:
            continue   # the intake side: not met by the points below
        if f2 < WAKE_RADIUS ** 2 and share > 0 and py > 0:
            uy -= WAKE_SPEED * V_H * share
            parts["column"] = parts.get("column", 0) - WAKE_SPEED * V_H * share
            column = True
            continue
        if py <= H_J and f2 > WAKE_RADIUS ** 2 and near > 0:   # the near wall jet
            scale = V_H * R * R / (2 * H_J * f2) * near
            ux += dx * scale
            uz += dz * scale
    # the merged jet and its landing
    w = merged(h - py)
    wg, Ug, ri, H = landing(h)
    if w > 0:
        s = h - py
        x = s / L_MOTOR - S0
        half = S * x * L_MOTOR
        d = 1 + PROFILE_A * (px * px + pz * pz) / half ** 2
        u = V_H * BD / x / d ** 2 * w
        if wg > 0 and py < 0.5 * ri:
            u *= py / (0.5 * ri) if py > 0 else 0
        uy -= u
        parts["far jet"] = -u
    if wg > 0 and py > 0:
        r = math.hypot(px, pz)
        if r > 0:
            big = max(r, ri)
            um = Ug * r / ri if r < ri else Ug * (ri / r) ** DECAY
            delta = C_WALL * big * (big / H) ** E_WALL
            v = um * verhoff(py / delta) * wg
            ux += px / r * v
            uz += pz / r * v
            parts["far wall jet"] = v
    # the fountain: where the two strongest near wall jets meet (here on a midline), weighted by the near share
    if not column and len(keys) >= 2 and near > 0:
        keys.sort(key=lambda k: -k[0])
        (_, k), (_, m) = keys[0], keys[1]
        rise = fountain(px, py, pz, k, m) * near
        uy += rise
        parts["fountain"] = rise
    return (ux, uy, uz), parts


def fountain(px, py, pz, k, m):
    (kx0, kz0), (mx0, mz0) = HUBS[k], HUBS[m]
    kx, kz, mx, mz = px - kx0, pz - kz0, px - mx0, pz - mz0
    rk, rm = math.hypot(kx, kz), math.hypot(mx, mz)
    uk, um = V_H * R * R / (2 * H_J * rk), V_H * R * R / (2 * H_J * rm)
    gx, gz = uk * um * (mx / rm ** 2 - kx / rk ** 2), uk * um * (mz / rm ** 2 - kz / rk ** 2)
    gn = math.hypot(gx, gz)
    if gn <= 0:
        return 0.0
    nx, nz = gx / gn, gz / gn
    delta = (uk * uk - um * um) / (2 * gn)
    kn = max(0.0, -uk * (kx * nx + kz * nz) / rk)
    mn = max(0.0, um * (mx * nx + mz * nz) / rm)
    flux, momentum = H_J * (kn + mn), FOUNTAIN * H_J * (kn * kn + mn * mn)
    if momentum <= 0:
        return 0.0
    mass, mom = math.sqrt(math.pi / LN2), math.sqrt(math.pi / (2 * LN2))
    y = py + mom / (mass * mass * SPREAD) * flux * flux / momentum
    exponent = -LN2 / SPREAD ** 2 * delta * delta / (y * y)
    if exponent <= -40:
        return 0.0
    centre = math.sqrt(momentum / y / (mom * SPREAD))
    return centre * math.exp(exponent) * (py / H_J if py < H_J else 1)


def stalk(kind, L):
    d1, wall, rho, E = KINDS[kind]
    d = d1 * L ** EXP
    di = d * (1 - 2 * wall)
    EI = E * math.pi / 64 * (d ** 4 - di ** 4)
    mu = rho * math.pi / 4 * (d * d - di * di)
    return d, EI, mu


def fold_pressure(kind, L, phi0, beta=0.0):
    """2.12's lean law, m(φ) = (φ − φ₀) − A·sin²(φ + β) − G·sin φ: an equilibrium is where A = f(φ) = (φ − φ₀ −
    G·sin φ)/sin²(φ + β) with f rising; past f's largest value over φ₀..90° there is none, so the stem runs over to lying
    flat (it folds). Returns that A as a pressure, q = A·8EI/(C_d·d·L³)."""
    d, EI, mu = stalk(kind, L)
    Gw = mu * G * L ** 3 / (8 * EI)
    best = -float("inf")
    for i in range(1, 20001):
        phi = phi0 + (math.pi / 2 - phi0) * i / 20000
        s = math.sin(phi + beta)
        if s <= 0:
            continue
        best = max(best, (phi - phi0 - Gw * math.sin(phi)) / (s * s))
    return max(0.0, best) * 8 * EI / (CD * d * L ** 3)


POINTS = {"under a hub": (HUB, HUB), "between the discs": (0.21, 0.0), "the centre": (0.0, 0.0)}

if __name__ == "__main__":
    print(f"the hover: T {T:.3f} N a rotor, v_h {V_H:.3f} m/s, T/A {T / A_DISC:.1f} Pa (2.12's q), l {L_MOTOR:.4f} m")
    SWIRL = math.radians(6)   # 2.12's hover swirl in a near column, tan β = Q/(2·T·r̄) (derived there, about 6°)
    print("the stalks' folding pressure (Pa) from a natural lean of 7.5° (the hash's mean): no swirl (the merged jet) / "
          "with the column's 6° swirl along the lean (2.12)")
    folds = {}
    for kind in KINDS:
        for L in (0.3, 0.5, 1.0, 1.5, 1.77):
            a, b = fold_pressure(kind, L, math.radians(7.5)), fold_pressure(kind, L, math.radians(7.5), SWIRL)
            folds[kind, L] = (a, b)
            print(f"  {kind} {L:.2f} m: {a:.1f} / {b:.1f} Pa")
    for h in (2.5, 1.5, 1.0, 0.5, 0.3):
        wg, Ug, ri, H = landing(h)
        print(f"hubs {h} m up: the near wake's share at the ground {1 - wg:.3f}; the merged jet lands at {Ug:.2f} m/s, r_i {ri:.3f} m")
        for L in (0.3, 0.5, 1.0, 1.5, 1.77):
            y = 2 / 3 * L
            if y >= h - 0.02:
                continue
            cells = []
            for name, (px, pz) in POINTS.items():
                (ux, uy, uz), parts = air(h, px, y, pz)
                q = 0.5 * RHO * uy * uy if uy < 0 else 0.0
                col = "column" in parts
                verdict = []
                for kind in KINDS:
                    if kind == "green" and L > 1.34:
                        continue
                    f = folds[kind, L][1 if col else 0]
                    verdict.append(f"{kind} {'folds' if q > f else 'stands'}")
                up = f" (up {uy:.1f} m/s)" if uy > 0 else ""
                cells.append(f"{name}: w {max(0, -uy):.2f} m/s, {q:.1f} Pa{up}, {', '.join(verdict)}")
            print(f"  a {L:.2f} m stem, its 2/3 point at {y:.3f} m: " + "; ".join(cells))
        print(f"  2.12's law: {T / A_DISC:.1f} Pa on every stem based under a disc at any height to 2.5 m, none between")
    print("for the test (hubs h, point, y: w down m/s, horizontal m/s):")
    for h in (2.5, 1.5, 1.0, 0.5, 0.3):
        for name, (px, pz) in POINTS.items():
            for y in (0.2, 2 / 3):
                if y >= h - 0.02:
                    continue
                (ux, uy, uz), _ = air(h, px, y, pz)
                print(f"  ({h}, \"{name}\", {y:.6f}): {-uy:.6f}, {math.hypot(ux, uz):.6f}")
