"""Hand numbers for design.md 2.3: what a crown's twigs hold, by the twigs' own bending and breaking, and what its leaves take
from a rotor. Run: python tools/flightbench/handcalc/crown_twigs.py (prints every number design.md 2.3 quotes)."""
import json
import math
import os

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "..")
MAT = json.load(open(os.path.join(ROOT, "assets-src", "physics_materials.json"), encoding="utf-8"))["materials"]["twig"]
G = 9.81
MASS = 4.735                 # kg, the loaded drone in the crown runs (measured, the flights' DroneSpec)


def v(block, key):
    return block[key]["value"]


def twig_numbers(t=None):
    """The crown law's per-twig numbers from physics_materials.json's twig.crown block."""
    t = t or MAT["crown"]
    d, l, sigma, e = v(t, "twig_diameter_m"), v(t, "twig_free_length_m"), v(t, "twig_rupture_stress_pa"), v(t, "twig_elastic_modulus_pa")
    z = math.pi * d ** 3 / 32                    # section modulus
    ei = e * math.pi * d ** 4 / 64
    # A part bears at an arm a, even over (0, l]: the twig breaks at P_b = sigma*Z/a after delta_b = 2*sigma*a^2/(3*E*d).
    p_mid = sigma * z / (l / 2)
    work = sigma ** 2 * z * l / (6 * e * d)       # E_a[P_b*delta_b/2]
    reach = 2 * sigma * l ** 2 / (9 * e * d)       # E_a[delta_b]
    slope = sigma * (l / 2) / (e * d)              # the twig's slope at its break, at the middle arm, rad
    return dict(d=d, l=l, sigma=sigma, E=e, Z=z, EI=ei, P_mid=p_mid, W=work, reach=reach, slope=slope,
                LV=v(t, "twig_length_per_volume_m_per_m3"))


def per_part(lv, w, perimeter):
    """A part of silhouette perimeter p (along its push): n' = L_V*p/4 twigs met a metre, F_y = n'*W, k_e = F_y/reach."""
    n = lv * perimeter / 4
    return n, n * w["W"], n * w["W"] / w["reach"]


def drone_perimeters():
    """The drone's parts as the repository's contact shapes give them (the probe's dump), each pushed straight down: a
    capsule's silhouette is its horizontal length and its diameter (2l + pi*w), a sphere's pi*w. Blades: 2 x 2 x 4 segments
    of 25-32 mm on each of 4 rotors, both edges' shapes."""
    parts = {
        "skids (4, 220 mm, d 10 mm)": 4 * (2 * 0.220 + math.pi * 0.010),
        "legs (4 vertical tubes, d 10 mm)": 4 * (math.pi * 0.010),
        "arms (8, 174 mm, d 6 mm)": 8 * (2 * 0.174 + math.pi * 0.006),
        "motors (4 spheres, d 37 mm)": 4 * math.pi * 0.0372,
        "stacks, electronics, packs, payload, coil": 2 * (2 * 0.166 + math.pi * 0.034) + (2 * 0.080 + math.pi * 0.020)
        + 2 * (2 * 0.175 + math.pi * 0.045) + (2 * 0.180 + math.pi * 0.080) + (2 * 0.137 * 0.96 + math.pi * 0.101),
        "blades (4 rotors x 2 blades x 4 segments x 2 edges)": 4 * 2 * 2 * (2 * (0.025 + 0.032 + 0.032 + 0.025) + math.pi * 2 * (0.0027 + 0.0022 + 0.0014 + 0.0009)),
    }
    return parts


def leaves(lad, u, hf=3.5e-3, v_c=None):
    """One disc's leaves per square metre of fresh disc plane: fling m_A*LAD*h_f*u^2, cut W_s*(pi/4)*LAD where u >= v_c."""
    lf = MAT["crown"]
    m_a = v(lf, "leaf_mass_per_area_kg_m2") / v(lf, "leaf_dry_matter_content")
    ws = v(lf, "leaf_work_to_shear_j_per_m")
    v_c = v_c if v_c is not None else v(lf, "leaf_critical_speed_m_s")
    fling = m_a * lad * hf * u * u
    cut = ws * math.pi / 4 * lad if u >= v_c else 0.0
    return m_a, fling, cut


if __name__ == "__main__":
    w = twig_numbers()
    print("one twig: d %.1f mm, free length %.2f m, sigma %.0f MPa, E %.1f GPa" % (w["d"] * 1e3, w["l"], w["sigma"] / 1e6, w["E"] / 1e9))
    print("  Z %.3e m3, EI %.3f N m2; at the middle arm it breaks at %.1f N, slope there %.1f deg" % (w["Z"], w["EI"], w["P_mid"], math.degrees(w["slope"])))
    print("  mean work to break W %.3f J, mean reach (break deflection) %.1f mm" % (w["W"], w["reach"] * 1e3))
    lv = w["LV"]
    total = 0.0
    print("parts pushed down into a crown of L_V %.1f m/m3:" % lv)
    for name, p in drone_perimeters().items():
        n, fy, ke = per_part(lv, w, p)
        total += p
        print("  %-55s perimeter %.3f m: %.2f twigs/m, F_y %.3f N, k_e %.2f N/m" % (name, p, n, fy, ke))
    n, fy, ke = per_part(lv, w, total)
    weight = MASS * G
    print("whole drone: perimeter %.2f m, %.2f twigs a metre of fall, holds %.2f N against its weight %.1f N (%.1f %%)" % (total, n, fy, weight, 100 * fy / weight))
    print("  the L_V that would hold it: %.0f m/m3 (x%.0f the crown's)" % (4 * weight / (total * w["W"]), 4 * weight / (total * w["W"]) / lv))
    # A blade at rest in the shell: its 8 shapes' F_y, at the friction's mu_s, about the shaft at the load's radius.
    blade = 2 * 2 * (0.025 + 0.032 + 0.032 + 0.025) + math.pi * 2 * (0.0027 + 0.0022 + 0.0014 + 0.0009)
    nb, fb, kb = per_part(lv, w, blade)
    print("one resting blade: F_y %.4f N; its stick friction about the shaft at 0.09 m: %.2e N m (model 89's shell: 25-39 N, 1.1-1.8 N m)" % (fb, 0.5 * fb * 0.09))
    # The pipe model's check on L_V (Shinozaki 1964): a twig of section A carries the leaf area A*AL:AS.
    a = math.pi * w["d"] ** 2 / 4 * 1e4  # cm2
    for lad in (0.21, 0.88, 2.09):
        lo, hi = lad / (a * 1.0) * w["l"], lad / (a * 0.1) * w["l"]
        print("pipe model, LAD %.2f: %.1f-%.1f m/m3 of %.1f mm twigs (AL:AS 1.0-0.1 m2/cm2)" % (lad, lo, hi, w["d"] * 1e3))
    # Leaves on one disc at 2 m/s through the crown (fresh plane 2R*v), and on a disc held still (none once swept).
    for lad in (0.21, 0.88, 2.09):
        for omega in (900.0, 1500.0):
            u = omega * 0.0847  # the mean radius 2R/3
            m_a, fl, ct = leaves(lad, u)
            rate = 2 * 0.127 * 2.0
            print("leaves LAD %.2f at %.0f rad/s: m_A %.3f kg/m2 fresh, %.2f W fling + %.3f W cut a disc at 2 m/s" % (lad, omega, m_a, fl * rate, ct * rate))
    # A disc coming down through leaves sweeps its whole volume: m_A*LAD*A*v_down*u^2 (the hover's mean radius, 0.5 m/s).
    for lad in (0.21, 0.88, 2.09):
        m_a = v(MAT["crown"], "leaf_mass_per_area_kg_m2") / v(MAT["crown"], "leaf_dry_matter_content")
        u = 900.0 * 0.0847
        print("leaves LAD %.2f, a disc coming down at 0.5 m/s at 900 rad/s: %.1f W fling" % (lad, m_a * lad * math.pi * 0.127 ** 2 * 0.5 * u * u))
