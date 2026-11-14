"""Hand numbers for w16-plants-in-the-air design.md (task 1.2): the plants in the sim's air. Each plant class as one bending
mode, a rigid link of length L on 4EI/L (w9-village-physics 2.12's lean law) whose inertia gives the cantilever's first mode,
driven by the quasi-steady drag of the air normal to it; a culm and a branch under a 1 s gust (the full nonlinear law with the
drag on the air relative to the moving stem, RK4 at 20 µs: independent of the C#'s exact-exponential step); a hover over
grass at 0.5, 1, 2 and 4 m by p7d-tether design 19's wash (stem_wash.py's own arithmetic of RotorAir), the lean of a green
0.6 m and a dry 0.9 m culm by radius; and the wall jet's friction velocity against each surface's threshold for lifting
dust. Nothing here is read from the C#; the stems' numbers are surfaces.json's cover rows, the wash's tether.json's."""
import json
import math
import os

import stem_wash as W

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))

G = 9.80665
RHO = W.RHO                  # tether.json's fiber.air_density_kg_m3 (published, ISA): the line's air, the plants' air
CD = 1.15                    # a stem's drag coefficient across the flow (PropStrikes.StemDrag: published 1.0-1.3)
EXP = 1 / 1.53               # d proportional to L^(1/1.53) (Niklas 1995, published)
ZETA_STEM = 0.08             # a wheat shoot's structural damping (Sterling et al. 2003, published; plants-in-the-air.md §3.2)
NATURAL_LEAN = math.radians(7.5)   # the stems' natural lean in the wash, PropStems' hash mean (assumed there, 0-15 deg)

with open(os.path.join(ROOT, "assets-src", "surfaces.json"), encoding="utf-8") as f:
    SURF = json.load(f)


def cover(kind):
    s = SURF["cover"][kind]["dry"]
    e = s["elastic_modulus_pa"]["value"]
    return {"E": 0.5 * (e[0] + e[1]), "wall": s["wall_share"]["value"], "rho": s["density_kg_m3"]["value"],
            "d1": s.get("diameter_m", {}).get("value", 0.0)}


# The classes: tissue, size law and foliage (plants-in-the-air.md §2, §3.2; the design's table labels each).
LEAF_AREA_MASS = SURF["cover"]["leaf"]["dry"]["mass_per_area_kg_m2"]["value"]   # 0.2 kg/m2 fresh (derived there)
FOLIAGE_CD = 0.3      # foliage drag per one-sided leaf area (assumed, in Katul et al.'s 0.1-0.5, the note's [20])
VOGEL_UC = 4.0        # the speed foliage starts to reconfigure, m/s (assumed: the hardwood tunnels' lowest, [9])
VOGEL = -1.0          # Vogel's exponent of leaves rolled into cones and dense crowns (published, [3])


def tube(d, wall, rho, E):
    di = d * (1 - 2 * wall)
    EI = E * math.pi / 64 * (d ** 4 - di ** 4)
    mu = rho * math.pi / 4 * (d * d - di * di)
    return EI, mu


def mechanics(kind, L):
    """The rigid link's constants: k = 4EI/L, I = k/w1^2 (the cantilever's first mode, 1.875^2 sqrt(EI/mu L^4)), the
    weight's moment mu g L^2/2 (tip mass none), c_s = 2 zeta_s sqrt((k - Gm) I); the drag per length (1/2) rho (C_d d +
    C_f a R(|v|)) |v| v over the span s0..s1 along the link."""
    m = {"kind": kind, "L": L, "s0": 0.0, "s1": L, "foliage": 0.0, "zs": ZETA_STEM}
    if kind in ("green", "dry", "burnt"):
        c = cover({"green": "grass_blade", "dry": "dry_culm", "burnt": "burnt_culm"}[kind])
        d = c["d1"] * L ** EXP
        EI, mu = tube(d, c["wall"], c["rho"], c["E"])
    elif kind == "weed":
        c = cover("stem")
        d = 0.008 * L ** EXP          # 8 mm at 1 m (assumed: stem-properties.md's 5-15 mm weeds)
        EI, mu = tube(d, c["wall"], c["rho"], c["E"])
        mu *= 2                       # the leaves double its mass per length (assumed, the note's §3.2)
        m["zs"] = 0.1
    elif kind in ("twig", "branch"):
        c = cover("twig")
        d = L / 100 if kind == "twig" else 0.04            # 5 mm x 0.5 m twig (assumed d = L/100); 40 mm x 3.5 m branch
        EI, mu = tube(d, 0.5, c["rho"], c["E"])
        load = 0.02 if kind == "twig" else 1.2             # foliage kg/m (assumed: the note's leafy-to-bare frequencies)
        mu += load
        m["foliage"] = 0.5 * RHO * FOLIAGE_CD * load / LEAF_AREA_MASS
        m["zs"] = 0.086                                    # a crown in leaf (Jackson et al. 2019, published)
    else:
        raise ValueError(kind)
    m.update(d=d, EI=EI, mu=mu)
    k = 4 * EI / L
    w1 = 1.875 ** 2 * math.sqrt(EI / (mu * L ** 4))
    I = k / (w1 * w1)
    Gm = mu * G * L * L / 2
    wg = math.sqrt((k - Gm) / I) if k > Gm else 0.0
    m.update(k=k, I=I, Gm=Gm, w1=w1, wg=wg, cs=2 * m["zs"] * math.sqrt(max(k - Gm, 0) * I), stem=0.5 * RHO * CD * d)
    return m


def drag(m, v):
    """Drag per length on air v normal to the element (m/s, signed)."""
    a = abs(v)
    c = m["stem"]
    if m["foliage"] > 0:
        c += m["foliage"] * (1.0 if a <= VOGEL_UC else (a / VOGEL_UC) ** VOGEL)
    return c * a * v


def zeta_air(m, U):
    """The air's damping ratio at a uniform normal air U: c_a = d(drag)/dv integrated with s^2, over 2 I w_g."""
    a = abs(U)
    slope = 2 * m["stem"] * a
    if m["foliage"] > 0:
        slope += m["foliage"] * a * (2 if a <= VOGEL_UC else (2 + VOGEL) * (a / VOGEL_UC) ** VOGEL)
    ca = slope * (m["s1"] ** 3 - m["s0"] ** 3) / 3
    return ca / (2 * m["I"] * m["wg"])


# 24-point Gauss-Legendre on [0, 1] for the span's integrals.
def _gauss(n):
    x, w = [], []
    for i in range(1, n + 1):
        t = math.cos(math.pi * (i - 0.25) / (n + 0.5))
        for _ in range(100):
            p0, p1 = 1.0, t
            for k in range(2, n + 1):
                p0, p1 = p1, ((2 * k - 1) * t * p1 - (k - 1) * p0) / k
            dp = n * (t * p1 - p0) / (t * t - 1)
            t -= p1 / dp
        x.append(0.5 * (1 - t))
        w.append(1.0 / ((1 - t * t) * dp * dp))
    return list(zip(x, w))


GL = _gauss(12)


def moment(m, phi, rate, u, w):
    """The air's moment about the base on the link at lean phi (rad, + toward +u), turning at rate: uniform air (u
    horizontal, w vertical), the drag on the air relative to each element (s rate)."""
    s0, s1 = m["s0"], m["s1"]
    q = 0.0
    for x, wt in GL:
        s = s0 + (s1 - s0) * x
        vn = u * math.cos(phi) - w * math.sin(phi) - s * rate
        q += wt * (s1 - s0) * drag(m, vn) * s
    return q


def settle(f, phi0):
    """The rest the stem reaches from phi0: the first root of the net moment f in the direction it pushes (a scan by
    0.01 rad, then 50 halvings); +-pi/2 (lying on the ground) when none."""
    f0 = f(phi0)
    if f0 == 0:
        return phi0
    d = 1.0 if f0 > 0 else -1.0
    a = phi0
    while True:
        b = a + d * 0.01
        if d * b >= math.pi / 2:
            b = d * math.pi / 2
            if f(b) * d > 0:
                return b
        if f(b) * d <= 0:
            break
        a = b
    for _ in range(50):
        c = 0.5 * (a + b)
        if f(c) * d > 0:
            a = c
        else:
            b = c
    return 0.5 * (a + b)


def static(m, u, w=0.0, phi0=0.0):
    """The steady lean in uniform air, reached from phi0."""
    return settle(lambda p: moment(m, p, 0.0, u, w) + m["Gm"] * math.sin(p) - m["k"] * (p - phi0), phi0)


def gust(m, u0, u1, length=1.0, end=8.0, dt=1e-4):
    """The lean through a rectangular gust of uniform air u0 -> u1 for `length` s from t = 0 -> u0, from rest at u0's lean:
    I phi'' = -k phi + Gm sin phi + Q(phi, phi', u) - c_s phi' by RK4. Returns the samples every 1 ms."""
    phi, rate = static(m, u0), 0.0
    out = []

    def acc(t, p, r):
        u = u1 if 0 <= t < length else u0
        return (-m["k"] * p + m["Gm"] * math.sin(p) + moment(m, p, r, u, 0.0) - m["cs"] * r) / m["I"]

    t = -0.2
    n = 0
    while t < end - 1e-12:
        if n % 10 == 0:
            out.append((round(t, 6), phi, rate))
        k1p, k1r = rate, acc(t, phi, rate)
        k2p, k2r = rate + 0.5 * dt * k1r, acc(t + 0.5 * dt, phi + 0.5 * dt * k1p, rate + 0.5 * dt * k1r)
        k3p, k3r = rate + 0.5 * dt * k2r, acc(t + 0.5 * dt, phi + 0.5 * dt * k2p, rate + 0.5 * dt * k2r)
        k4p, k4r = rate + dt * k3r, acc(t + dt, phi + dt * k3p, rate + dt * k3r)
        phi += dt * (k1p + 2 * k2p + 2 * k3p + k4p) / 6
        rate += dt * (k1r + 2 * k2r + 2 * k3r + k4r) / 6
        t += dt
        n += 1
    return out


def report_gust(name, m, u0, u1):
    p0, p1 = static(m, u0), static(m, u1)
    out = gust(m, u0, u1)
    peak = max(out, key=lambda s: s[1])
    at_end = min(out, key=lambda s: abs(s[0] - 1.0))
    after = [s for s in out if s[0] >= 1.0]
    low = min(after, key=lambda s: s[1])
    band = 0.05 * abs(p1 - p0)
    settle = None
    for i in range(len(after) - 1, -1, -1):
        if abs(after[i][1] - p0) > band:
            settle = after[i][0] if i + 1 < len(after) else None
            break
    zs = [zeta_air(m, u) + m["zs"] for u in (u0, u1)]
    print(f"  {name}: lean {p0:.5f} rad at {u0} m/s, {p1:.5f} at {u1} m/s; zeta {zs[0]:.3f} / {zs[1]:.3f} "
          f"(f_g {m['wg'] / 2 / math.pi:.3f} Hz)")
    print(f"    peak {peak[1]:.5f} rad at {peak[0]:.3f} s; at the gust's end {at_end[1]:.5f}; after it the least {low[1]:.5f} at "
          f"{low[0]:.3f} s (past rest by {p0 - low[1]:.5f} rad, {100 * (p0 - low[1]) / (p1 - p0):.1f} % of the step); "
          f"within 5 % of rest from {settle:.3f} s")
    for t in (0.1, 0.25, 0.5, 1.0, 1.25, 1.5, 2.0, 3.0):
        s = min(out, key=lambda q: abs(q[0] - t))
        print(f"      t {t:.2f} s: {s[1]:.6f} rad, {s[2]:.6f} rad/s")
    return out


# ---- the hover ---------------------------------------------------------------------------------------------------
def profile(h, x, z, top, n=300):
    """The wash on the stem base's vertical: n + 1 heights from 0 to top (u_h along the outward radius, w)."""
    r = math.hypot(x, z)
    ex, ez = (x / r, z / r) if r > 0 else (1.0, 0.0)
    ys, us, ws = [], [], []
    for i in range(n + 1):
        y = top * i / n
        (ux, uy, uz), _ = W.air(h, x, max(y, 1e-6), z)
        ys.append(y)
        us.append(ux * ex + uz * ez)
        ws.append(uy)
    return ys, us, ws


def wash_moment(m, phi, ys, us, ws):
    """The moment on the link leaning phi outward, its element at s taking the air at height y = s cos(phi) on the base's
    vertical (the model's own reading): (1/cos^2 phi) times the integral of drag(v_n(y)) y dy to L cos(phi), by the
    trapezoid on the profile's own 300 steps; a stem lying flat takes the lowest air along its length."""
    L = m["L"]
    c, s = math.cos(phi), math.sin(phi)
    if c < 1e-9:
        vn = us[0] * c - ws[0] * s
        return drag(m, vn) * L * L / 2
    top = L * c
    q = 0.0
    prev_y, prev_g = 0.0, drag(m, us[0] * c - ws[0] * s) * 0.0
    for j in range(1, len(ys)):
        y = ys[j]
        if y > top:
            t = (top - ys[j - 1]) / (ys[j] - ys[j - 1])
            u = us[j - 1] + t * (us[j] - us[j - 1])
            w = ws[j - 1] + t * (ws[j] - ws[j - 1])
            g = drag(m, u * c - w * s) * top
            q += 0.5 * (prev_g + g) * (top - prev_y)
            break
        g = drag(m, us[j] * c - ws[j] * s) * y
        q += 0.5 * (prev_g + g) * (y - prev_y)
        prev_y, prev_g = y, g
    return q / (c * c)


def wash_lean(m, ys, us, ws, phi0):
    """The steady lean under the wash reached from the natural lean phi0 (outward)."""
    return settle(lambda p: wash_moment(m, p, ys, us, ws) + m["Gm"] * math.sin(p) - m["k"] * (p - phi0), phi0)


# ---- the dust ------------------------------------------------------------------------------------------------------
NU = 1.5e-5        # air's kinematic viscosity, m2/s (published, 15-20 C)
DUST = {           # surface: dry threshold u*_t (m/s), its label, the wall's roughness k_s (m, assumed), clay % (assumed)
    "crater_spoil": (0.19, "published band 0.14-0.25 [17]", 0.002, 20),
    "dirt_track": (0.22, "published band 0.19-0.25 [17]", 0.002, 20),
    "belt_bare": (0.27, "assumed 0.20-0.35", 0.002, 20),
    "burnt_field": (0.25, "published ash 0.13-0.38 [19]", 0.002, 20),
    "tilled": (0.375, "published 0.35-0.40 [17]", 0.002, 20),
    "dry_crust": (0.75, "assumed 0.5-1.0 [18]", 0.002, 20),
    "rubble": (0.37, "assumed 0.25-0.5", 0.002, 10),
    "asphalt": (0.25, "assumed 0.2-0.3", 0.0005, 10),
    "concrete_slab": (0.25, "assumed 0.2-0.3", 0.0005, 10),
}


def friction(um, ym, r, H, ks):
    """u* of the wall jet: the smooth wall's Poreh c_f (delta = y_m/0.165) and Nikuradse's fully rough law at y_m; the
    larger (the transitional wall lies between and tends to the larger)."""
    delta = ym / 0.165
    cf = 0.06 * (um * delta / NU) ** -0.3 * (max(r, 1e-3) / H) ** -0.16
    smooth = um * math.sqrt(cf / 2)
    rough = um / (5.75 * math.log10(ym / ks) + 8.5) if ym > ks else smooth
    return max(smooth, rough), smooth, rough


def jet_peak(h, x, z):
    """The wall jet's peak speed along the ground at (x, z) and its height, from 300 heights to 0.25 m."""
    best, at = 0.0, 0.01
    for i in range(1, 301):
        y = 0.25 * i / 300
        (ux, uy, uz), _ = W.air(h, x, y, z)
        s = math.hypot(ux, uz)
        if s > best:
            best, at = s, y
    return best, max(at, 0.165 * W.H_J)   # the near jets' top-hat: its thickness taken as its delta (assumed)


def fecan(water_percent, clay_percent):
    wp = 0.0014 * clay_percent ** 2 + 0.17 * clay_percent
    return 1.0 if water_percent <= wp else math.sqrt(1 + 1.21 * (water_percent - wp) ** 0.68)


if __name__ == "__main__":
    print("== the classes (a rigid link on 4EI/L, its inertia the cantilever's first mode) ==")
    for kind, L in (("green", 0.45), ("green", 0.6), ("green", 0.67), ("green", 0.97), ("green", 1.34), ("dry", 0.2),
                    ("dry", 0.6), ("dry", 0.9), ("dry", 1.6), ("burnt", 0.9), ("weed", 0.5), ("weed", 1.0),
                    ("twig", 0.5), ("branch", 3.5)):
        m = mechanics(kind, L)
        Uc = math.sqrt(m["EI"] / (RHO * m["d"] * L ** 3))
        za = ", ".join(f"{zeta_air(m, u):.3f}" for u in (2, 5, 10))
        print(f"  {kind} {L} m: d {1e3 * m['d']:.2f} mm, EI {m['EI']:.3e} N m2, mu {m['mu']:.3e} kg/m, k {m['k']:.4e} N m/rad, "
              f"I {m['I']:.4e} kg m2, Gm/k {m['Gm'] / m['k']:.3f}, f1 {m['w1'] / 2 / math.pi:.3f} Hz (with weight "
              f"{m['wg'] / 2 / math.pi:.3f}), Cauchy 1 at {Uc:.2f} m/s, zeta_air at 2/5/10 m/s {za}")

    print("\n== a 1 s gust of uniform air (the full law, RK4) ==")
    report_gust("green culm 0.67 m, 5 -> 9 -> 5 m/s", mechanics("green", 0.67), 5.0, 9.0)
    report_gust("green culm 0.67 m, 2 -> 6 -> 2 m/s", mechanics("green", 0.67), 2.0, 6.0)
    report_gust("branch 40 mm x 3.5 m, leafy, 2 -> 6 -> 2 m/s", mechanics("branch", 3.5), 2.0, 6.0)

    print("\n== a hover over grass (stem_wash.py's design 19 field; 4.735 kg, still air) ==")
    print(f"  v_h {W.V_H:.3f} m/s, l {W.L_MOTOR:.4f} m, h_j {W.H_J:.4f} m; natural lean {math.degrees(NATURAL_LEAN)} deg outward")
    hover = {}
    for h in (0.5, 1.0, 2.0, 4.0):
        wg, Ug, ri, H = W.landing(h)
        print(f"  hubs {h} m up: the merged jet's share {wg:.3f}, U_g {Ug:.3f} m/s, r_i {ri:.3f} m")
        for kind, L in (("green", 0.6), ("dry", 0.9)):
            m = mechanics(kind, L)
            rest = settle(lambda p: m["Gm"] * math.sin(p) - m["k"] * (p - NATURAL_LEAN), NATURAL_LEAN)
            if h == 0.5:
                print(f"    {kind} {L} m: its rest under its own weight {rest:.5f} rad (natural {NATURAL_LEAN:.5f}); "
                      f"the bends below are from that rest")
            for name, (ax, az) in (("diagonal", (0.7071067811865476, 0.7071067811865476)), ("side", (1.0, 0.0))):
                row = []
                for i in range(0, 21):
                    r = 0.2 * i
                    x, z = r * ax, r * az
                    if r == 0:
                        x, z = 1e-4 * ax, 1e-4 * az
                    ys, us, ws = profile(h, x, z, L * 1.0001)
                    phi = wash_lean(m, ys, us, ws, NATURAL_LEAN)
                    row.append((r, phi - rest))
                    hover[h, kind, name, round(r, 1)] = phi - rest
                flat = [r for r, b in row if b >= 1.0]
                lean = [r for r, b in row if b >= 0.1]
                print(f"    {kind} {L} m, {name}: bend at r 0..4 m by 0.2: " + " ".join(f"{b:.3f}" for _, b in row))
                print(f"      flat (>= 1 rad) {('to ' + format(max(flat), '.1f') + ' m') if flat else 'nowhere'}; "
                      f"leaning (>= 0.1 rad) {('to ' + format(max(lean), '.1f') + ' m') if lean else 'nowhere'}")

    print("\n== the dust: the wall jet's u* (smooth Poreh c_f, rough Nikuradse at k_s), the largest over the grid's nodes ==")
    heights = [0.3, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0, 4.0, 6.0, 8.0, 10.0, 12.0, 15.0]
    best = {}
    for h in heights:
        wg, Ug, ri, H = W.landing(h)
        Hj = H if wg > 0 else h
        cells = []
        # The wash grid's own nodes (0.15 m cells, centres at (i + 1/2)·0.15) to 2.4 m, one octant (the hover's symmetry).
        for i in range(16):
            for j in range(i + 1):
                x, z = (i + 0.5) * 0.15, (j + 0.5) * 0.15
                r = math.hypot(x, z)
                um, ym = jet_peak(h, x, z)
                if um <= 0:
                    continue
                cells.append((r, um, ym))
        best[h] = cells
        top = max(cells, key=lambda c: c[1])
        print(f"  hubs {h} m: the jet's peak {top[1]:.2f} m/s at r {top[0]:.2f} m, {1e3 * top[2]:.1f} mm up "
              f"(H {Hj:.2f} m)")
    print("  onset per surface (the highest hover whose u* anywhere reaches u*_t), dry and wet:")
    water = SURF["wetting"]["water_content_wet"]["value"]
    for name, (ut, label, ks, clay) in DUST.items():
        bulk = SURF["surfaces"][name]["dry"].get("bulk_density_kg_m3", {}).get("value", 1300.0)
        wet_w = 100 * water * 1000.0 / bulk
        factor = fecan(wet_w, clay)
        res = []
        for thr in (ut, ut * factor):
            on = None
            for h in heights:
                wg, Ug, ri, H = W.landing(h)
                Hj = H if wg > 0 else h
                us = max(friction(um, ym, r, Hj, ks)[0] for r, um, ym in best[h])
                if us >= thr:
                    on = h
            res.append(on)
        print(f"    {name}: u*_t {ut} m/s ({label}), k_s {ks} m: dust from {res[0]} m dry; wet (w {wet_w:.1f} %, "
              f"x{factor:.2f}): from {res[1]}")
    print("  u* per hover height (the largest over the nodes; smooth / rough at k_s 2 mm):")
    for h in heights:
        wg, Ug, ri, H = W.landing(h)
        Hj = H if wg > 0 else h
        sm = max(friction(um, ym, r, Hj, 0.002)[1] for r, um, ym in best[h])
        ro = max(friction(um, ym, r, Hj, 0.002)[2] for r, um, ym in best[h])
        print(f"    {h} m: {sm:.3f} / {ro:.3f} m/s")
