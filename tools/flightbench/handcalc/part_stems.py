"""Hand numbers for w16-plants-in-the-air design.md "## 1.5": what the drone's parts feel in the stems and in a crown's leaves.
Independent arithmetic from surfaces.json's cover rows and drone.json's shapes; the stem field's stems per m² at the two cells
are carried from IWorldQuery.StemsNear (printed by PartStemTests.TheFieldAtTheCells). Run:
python tools/flightbench/handcalc/part_stems.py (prints every number design.md 1.5 quotes)."""
import json
import math
import os

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "..")
SURF = json.load(open(os.path.join(ROOT, "assets-src", "surfaces.json"), encoding="utf-8"))["cover"]
MAT = json.load(open(os.path.join(ROOT, "assets-src", "physics_materials.json"), encoding="utf-8"))["materials"]["twig"]["crown"]
G = 9.80665
ALLOMETRY = 1 / 1.53            # d = d1*L^(1/1.53) (Niklas 1995, PropStems)
YIELD_STRAIN = 8.41 / 474       # barley straw's bending stress over its bending modulus (stem-properties.md [6], PartStems)
LINK = 4 / 1.875 ** 4           # 1.2's link inertia over mu*L^3


def val(row, key):
    return row["dry"][key]["value"]


def stem(kind, length):
    """A stem of the field's kind at its drawn length (PropStems' tissue): d, EI, mu, the first-order tau, the yield moment."""
    row = SURF[kind]
    d1 = val(row, "diameter_m")
    e = sum(val(row, "elastic_modulus_pa")) / 2
    wall, rho = val(row, "wall_share"), val(row, "density_kg_m3")
    d = d1 * length ** ALLOMETRY
    di = d * (1 - 2 * wall)
    ei = e * math.pi / 64 * (d ** 4 - di ** 4)
    mu = rho * math.pi / 4 * (d ** 2 - di ** 2)
    tau = length ** 2 / (1.875 ** 2 * math.sqrt(ei / mu))
    return dict(d=d, EI=ei, mu=mu, L=length, tau=tau, k4=4 * ei / length, My=YIELD_STRAIN * 2 * ei / d,
                Pcr=math.pi ** 2 * ei / (4 * length ** 2), I=LINK * mu * length ** 3)


def push(s, y, v, r=0.005, steps=4000):
    """One upright stem of tissue s met by a horizontal capsule (core y over its base, radius r) moving across it at v (m/s),
    by the law of PartStems, quasi-statically: the link from the base clears the capsule (its lean phi(x) the least that does),
    the contact's spring 3EI/s at its arm s, the first-order law's damping k*tau*phi', the root's yield, the weight's moment;
    at the first touch the stem's mass at the contact brought to v. Returns the work along the motion (J), the peak force (N)
    and the impulse's share (J)."""
    re = r + s["d"] / 2
    L = s["L"]
    xs = [-re + i * (2 * L + re) / steps for i in range(steps + 1)]
    work = peak = 0.0
    last_phi = 0.0
    g_moment = s["mu"] * G * L * L / 2   # the weight's moment at 90 degrees
    for i, x in enumerate(xs):
        # The pushed stem stays ahead of the part (the side kept): its lean phi toward +x keeps the core r_e behind its line,
        # y*sin(phi) - x*cos(phi) = r_e; once the contact's arm passes the tip, the stem slides under the part and is let go.
        rr = math.hypot(x, y)
        if rr <= re:
            break
        phi = max(0.0, math.atan2(x, y) + math.asin(re / rr))
        arm = x * math.sin(phi) + y * math.cos(phi)
        if arm > L or phi >= math.pi / 2:
            break
        arm = max(1e-3, arm)
        dphi = (phi - last_phi) / ((2 * L + re) / steps) * v if i > 0 else 0.0   # phi' = dphi/dx * v
        moment = min(3 * s["EI"] / arm * phi - g_moment * math.sin(phi) + s["k4"] * s["tau"] * max(dphi, 0.0), s["My"])
        normal = max(0.0, moment) / arm           # across the link at the contact
        fx = normal * math.cos(phi)               # its share along the motion (the normal is across the link, in the plane)
        if i > 0:
            work += fx * (xs[i] - xs[i - 1])
        peak = max(peak, normal)
        last_phi = phi
    s0 = y                                         # the first touch's arm (the stem upright)
    mass = min(s["I"] / s0 ** 2, s["mu"] * L)
    return work + mass * v * v, peak, mass * v * v


def report_push():
    print("== One stem pushed by a skid (r 5 mm) across it ==")
    print(f"{'stem':24} {'y':>5} {'v':>4} {'work J':>8} {'peak N':>7} {'impulse J':>9}")
    for kind, L in (("grass_blade", 0.67), ("dry_culm", 0.75), ("dry_culm", 1.5)):
        s = stem(kind, L)
        for y in (0.3, 0.45):
            if y >= L:
                continue
            for v in (2.0, 5.0):
                w, p, j = push(s, y, v)
                print(f"{kind + ' ' + str(L):24} {y:5.2f} {v:4.1f} {w:8.4f} {p:7.3f} {j:9.4f}")


# The weeds cell (823.00, 34.60) and the densest tall cell (768.07, 917.48): stems per m^2 reaching h, green / dry (carried from
# StemsNear, PartStemTests.TheFieldAtTheCells; burnt counted as dry, its section's wall halved is near enough here).
WEEDS = {0.30: (328, 749), 0.45: (196, 389), 0.53: (138, 270), 0.60: (98, 186), 0.75: (45, 96), 0.83: (31, 72), 1.0: (8, 22)}


def lengths(kind):
    """The weeds cell's stems taller than a height by kind, as a few lengths with weights (its layers' drawn tops; numerical)."""
    return [(0.75, 0.6), (1.1, 0.25), (1.5, 0.15)] if kind == "dry_culm" else [(0.75, 0.7), (1.1, 0.3)]


def skim(H, v):
    """The drone level, its skids' bottoms H over the weeds cell, at v along its nose: each strip across the motion meets the
    stems that reach its lowest part (drone.json: skids at H, the coil at H + 0.076, the packs at H + 0.1875, the arms and motors
    at H + 0.245), each stem met once by the strip's lowest part (the rest pass over it lying), stems over the discs' plane
    (H + 0.283) cut first in their swath. Fresh stems in every strip: an upper bound (no stem shadowed, none cut by the props)."""
    strips = [("skids", H, 2 * 0.010, 0.005), ("coil", H + 0.076, 0.101, 0.0505), ("packs", H + 0.1875, 2 * 0.045, 0.0225),
              ("arms, motors", H + 0.245, 2 * (0.0345 + 0.038), 0.0186)]
    total = 0.0
    rows = []
    for name, y, width, r in strips:
        n_y = min((h for h in WEEDS if h >= y - 1e-9), default=None)
        if n_y is None:
            rows.append((name, y, 0, 0.0))
            continue
        green, dry = WEEDS[n_y]
        f = 0.0
        for kind, count in (("grass_blade", green), ("dry_culm", dry)):
            for L, share in lengths(kind):
                if L <= y:
                    continue
                s = stem(kind, L)
                w, _, _ = push(s, y, v, r=r, steps=800)
                f += count * share * (width + s["d"]) * w
        rows.append((name, y, green + dry, f))
        total += f
    return total, rows


def report_skim():
    print("\n== Skimming the weeds cell: the stems' drag, N (fresh stems in every strip: an upper bound) ==")
    for H in (0.45, 0.75):
        for v in (2.0, 5.0):
            total, rows = skim(H, v)
            print(f"skids {H:.2f} m up, {v:.0f} m/s: {total:.2f} N  (" + ", ".join(f"{n} {f:.2f}" for n, _, _, f in rows) + ")")


def report_touchdown():
    print("\n== A touchdown in the densest tall cell: what a stem under a skid carries ==")
    for kind, L in (("grass_blade", 0.6), ("dry_culm", 0.6), ("dry_culm", 1.2), ("burnt_culm", 1.5)):
        s = stem(kind, L)
        print(f"{kind} {L} m: d {s['d'] * 1e3:.2f} mm, Euler {s['Pcr']:.4f} N, yield {s['My']:.4f} N.m "
              f"(at 20 mm up: {s['My'] / 0.02:.2f} N, 50 mm: {s['My'] / 0.05:.2f} N)")
    # Stems under each skid's footprint (0.44 m long, 10 mm + d wide) at h = 0: the densest tall cell's 2,345 a m^2.
    under = 2345 * 0.44 * 0.0145
    print(f"stems under one skid's footprint: {under:.1f}; under the coil's 0.24 x 0.101 m: {2345 * 0.24 * 0.105:.1f}")


def report_crown():
    print("\n== A crown at the same speeds: its twigs (design 2.3) and its leaves on the body (Newton) ==")
    lad = 0.88                                       # IslandTree's leaf area density (measured, 2.8)
    m_a = MAT["leaf_mass_per_area_kg_m2"]["value"] / MAT["leaf_dry_matter_content"]["value"]
    rho = lad * m_a
    # The drone's parts flying along +z, level (drone.json): Newton's 2/3 on a capsule's projected side, 1/2 on its caps' disc.
    caps = [(0.005, 0.0, 4), (0.0225, 0.0, 2), (0.04, 0.0, 1), (0.017, 0.0, 2), (0.01, 0.0, 1), (0.0505, 0.0, 1),
            (0.0186, 0.0, 4)]                         # radius, core across, count: skids, packs, payload, stacks, electronics, coil, motors
    sides = [(0.005, 0.225, 4), (0.003, 0.174 * 0.7071, 8)]  # the legs broadside, the arms at 45 degrees
    area = sum(c * 0.5 * math.pi * r * r for r, _, c in caps) + sum(c * (2 / 3) * 2 * r * l for r, l, c in sides)
    area += 0.5 * math.pi * 0.0505 ** 2 * 0 + (2 / 3) * 0.101 * 0.137 * math.sin(math.radians(15))   # the coil's tilt
    twigs = 0.75                                     # N, design 2.3: L_V*p/4*W over the whole drone
    for v in (2.0, 5.0):
        print(f"{v:.0f} m/s: leaves rho_l {rho:.3f} kg/m^3 x kappa*A {area:.4f} m^2 x v^2 = {rho * area * v * v:.3f} N; twigs {twigs:.2f} N; "
              f"together {twigs + rho * area * v * v:.2f} N")


if __name__ == "__main__":
    report_push()
    report_skim()
    report_touchdown()
    report_crown()
