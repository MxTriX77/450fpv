"""Hand numbers for w16-plants-in-the-air design.md §5b (task 1.4a): the loose plant bits the props' wash lifts from covered
ground. Each class's piece (fine chaff and dead leaves as flat pieces of their mass per area, a straw bit as surfaces.json's
straw), its settling speed, the wall jet's friction velocity that starts and keeps it moving, dry and wet; each covered
surface's loose supply; and a hover from rest over each covered surface at a run of heights: the pieces its first tick lifts
over the wash grid's 32 x 0.15 m window, by stem_wash.py's design 19 field (200 heights to 0.1 m a node, the jet's peak
height by design.md §5's rule) and plants_air.py's friction law over the cover's roughness. Nothing here is read from the C#;
the straw and leaf numbers are surfaces.json's cover rows."""
import math

import plants_air as P

G = 9.80665
RHO = 1.225                  # tether.json's air (published, ISA)
SPREAD = 1.5                 # the pieces' thresholds' geometric standard deviation (assumed)
KEEP = 0.8                   # Bagnold's impact over fluid threshold (published for sand, assumed for litter)
GAMMA = 0.072                # water's surface tension, N/m (published, 20 C)
CD_PLATE, CD_CYL = 1.17, 1.1  # a flat plate broadside; a cylinder across, Re 1e2-1e3 (published, recalled)
AIR_DRY, LITTER_WATER = 0.1, 2.0   # kg water per kg dry: air-dry (assumed); litter's water-holding capacity (published)
SIGMA_CHAFF = 0.05           # kg/m2, glumes and dead blades (assumed, within Wright et al. 2004's 30-120 g/m2)
SIGMA_LEAF = P.SURF["cover"]["leaf"]["dry"]["dry_mass_per_area_kg_m2"]["value"]   # the table's leaf row (0.06)
STRAW = P.SURF["cover"]["straw"]["dry"]
STRAW_D = 0.0035             # the straw row's note: a 3.5 mm straw
STRAW_WALL = STRAW["wall_share"]["value"]
STRAW_RHO = STRAW["density_kg_m3"]["value"]
STRAW_WET = P.SURF["wetting"]["straw_density_wet"]["value"]
U10 = (5.5, 7.9)             # Beaufort force 4: dust, leaves and loose paper lifted (published, NWS)
Z0 = 0.03                    # open flat grass (published, WMO)
SIZES = {"chaff": (0.008, 0.003), "straw": (0.04, STRAW_D), "leaf": (0.05, 0.03)}   # m (assumed)
COVER = {                    # surface: the loose top layer's plan cover per class (assumed), the cover's k_s (m, assumed)
    "meadow_sod": ({"chaff": 0.1, "straw": 0.05, "leaf": 0.0}, 0.002),
    "weeds": ({"chaff": 0.05, "straw": 0.05, "leaf": 0.05}, 0.002),
    "belt_straw": ({"chaff": 0.05, "straw": 0.3, "leaf": 0.1}, STRAW_D),
    "yard_litter": ({"chaff": 0.1, "straw": 0.0, "leaf": 0.6}, 0.005),
}


def rim(a, b):
    x, y = a / 2, b / 2
    return math.pi * (3 * (x + y) - math.sqrt((3 * x + y) * (x + 3 * y)))


def piece(kind, wet):
    """mass (kg), settling (m/s), start and keep (u*, m/s), plan area (m2)."""
    a, b = SIZES[kind]
    leaf_start = 0.4 * (sum(U10) / 2) / math.log(10 / Z0)
    if kind == "straw":
        inner = STRAW_D * (1 - 2 * STRAW_WALL)
        line = STRAW_RHO * math.pi / 4 * (STRAW_D ** 2 - inner ** 2)
        centre = 8.5 + 5.75 * math.log10(0.5)
        start = math.sqrt(2 * math.tan(math.radians(30)) * line * G / (RHO * CD_CYL * STRAW_D * math.sin(math.radians(45)))) / centre
        m, f = line * a, (STRAW_WET if wet else 1.0)
        settle = math.sqrt(2 * line * f * G / (RHO * CD_CYL * STRAW_D))
        if wet:
            start *= math.sqrt((m * f * G + 2 * GAMMA * a) / (m * G))
        return m * f, settle, start, KEEP * start, a * b
    dry = SIGMA_LEAF if kind == "leaf" else SIGMA_CHAFF
    area = math.pi / 4 * a * b
    sigma = dry * (1 + (LITTER_WATER if wet else AIR_DRY))
    start = round(leaf_start, 2) * math.sqrt(dry / SIGMA_LEAF)
    if wet:
        start *= math.sqrt((sigma * area * G + 2 * GAMMA * rim(a, b)) / (dry * (1 + AIR_DRY) * area * G))
    return sigma * area, math.sqrt(2 * sigma * G / (RHO * CD_PLATE)), start, KEEP * start, area


def wall(h, x, z, wg, ri, H):
    """The wall jet along the ground at (x, z) as design.md §5 takes it: its peak speed along the ground over 0-0.1 m (200
    heights here; the grid reads 5), at its own peak height 0.165 delta: the merged jet's delta = c R (R/H)^e, R = max(r, r_i),
    where it carries the landing (share >= 1/2), else the near jets' top-hat h_j taken as delta (assumed there)."""
    best = 0.0
    for i in range(1, 201):
        (ux, _, uz), _ = P.W.air(h, x, 0.1 * i / 200, z)
        best = max(best, math.hypot(ux, uz))
    r = math.hypot(x, z)
    if wg >= 0.5:
        big = max(r, ri)
        return best, 0.165 * P.W.C_WALL * big * (big / H) ** P.W.E_WALL
    return best, 0.165 * P.W.H_J


def share(us, start):
    if us <= 0:
        return 0.0
    return 0.5 * (1 + math.erf(math.log(us / start) / math.log(SPREAD) / math.sqrt(2)))


if __name__ == "__main__":
    lo, hi = (0.4 * u / math.log(10 / Z0) for u in U10)
    print(f"== the leaves' start from Beaufort 4 over open grass: u* {lo:.3f}-{hi:.3f} m/s, middle {(lo + hi) / 2:.3f} ==")
    print("== the pieces (dry / wet) ==")
    for kind in SIZES:
        for wet in (False, True):
            m, s, st, kp, area = piece(kind, wet)
            print(f"  {kind} {'wet' if wet else 'dry'}: mass {1e6 * m:.2f} mg, settling {s:.4f} m/s, start {st:.4f} m/s "
                  f"(5-95 % {st / SPREAD ** 1.645:.3f}-{st * SPREAD ** 1.645:.3f}), keep {kp:.4f} m/s")
    print("== the supply (pieces per m2; the loose top layer's mass, g/m2) ==")
    for name, (phi, ks) in COVER.items():
        row, mass = [], 0.0
        for kind in SIZES:
            m, _, _, _, area = piece(kind, False)
            n = phi[kind] / area
            mass += n * m
            row.append(f"{kind} {n:.1f}")
        print(f"  {name}: " + ", ".join(row) + f"; {1e3 * mass:.1f} g/m2; k_s {ks} m")

    print("== a hover from rest: the pieces the first tick lifts over the 32 x 0.15 m window (dry / wet), and the most u* ==")
    heights = [0.3, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0, 4.0, 6.0, 8.0]
    nodes = {}
    for h in heights:
        wg, Ug, ri, H = P.W.landing(h)
        Hj = H if wg > 0 else h
        cells = []
        for i in range(16):
            for j in range(i + 1):
                x, z = (i + 0.5) * 0.15, (j + 0.5) * 0.15
                um, ym = wall(h, x, z, wg, ri, H)
                cells.append((math.hypot(x, z), um, ym, 4 if i == j else 8))
        nodes[h] = (cells, Hj)
    for name, (phi, ks) in COVER.items():
        print(f"  {name}:")
        for h in heights:
            cells, Hj = nodes[h]
            out, most = [], 0.0
            for wet in (False, True):
                total = {k: 0.0 for k in SIZES}
                for r, um, ym, weight in cells:
                    if um <= 0:
                        continue
                    us = P.friction(um, ym, r, Hj, ks)[0]
                    most = max(most, us)
                    for kind in SIZES:
                        if phi[kind] <= 0:
                            continue
                        _, _, st, _, area = piece(kind, wet)
                        total[kind] += weight * 0.15 ** 2 * phi[kind] / area * share(us, st)
                out.append(" ".join(f"{k} {v:.1f}" for k, v in total.items()))
            print(f"    {h} m: dry {out[0]} | wet {out[1]} | u* up to {most:.3f} m/s")
