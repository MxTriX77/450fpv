"""Hand numbers for p7c-wind design.md 16 (task 1.5e): the field's turbulence scales and intensities with height, from
the neutral surface layer's published variances and inertial range, against MIL-HDBK-1797's low-altitude law and
Kaimal et al. (1972)'s spectra. Run: python tools/flightbench/handcalc/field_scale.py (prints every number design 16
quotes). Labels: published, derived, measured (the sim's probes), assumed."""
import math

KARMAN = 0.4                  # published
KNEE = 1.339                  # published: von Karman's k_e = 1/(1.339 L) (MIL-HDBK-1797)
# The neutral surface layer's sigma over u* (published: Panofsky and Dutton 1984, recalled: 2.39 / 1.92 / 1.25;
# Charrondiere and Stiperski 2023, Table 1, MOST at zeta 0, read: 2.55 / 2.05 / 1.25). The field's horizontal is
# isotropic (sigma_v = sigma_u), its sigma_u the 2.4 it already flies (WindPreset.SurfaceSigmaU).
SIGMA_U, SIGMA_W = 2.4, 1.25
# Kaimal et al. (1972)'s neutral spectra, nS/u*^2 at f = n (z - d) / U (published, recalled; their inertial limits
# 105/33^(5/3) = 0.309 and 2.1/5.3 = 0.396 against the a1 0.3 and a3 0.4 Charrondiere and Stiperski 2023 restate, read).
A_U = 105 / 33 ** (5 / 3)
A_W = 2.1 / 5.3
# The cover and the profile (WindPreset): stubble 0.3 m, d = 0.67 h, the landscape's z0 0.10 m, the top 100 m.
COVER, D, Z0, TOP = 0.3, 0.67 * 0.3, 0.10, 100.0
# The canopy-top eddies' isotropic scales over the canopy height (WindPreset.CanopyScaleU, CanopyScaleW: Finnigan, Shaw
# and Patton 2009, of order h, published; w's 0.6 h derived from Kruijt et al. 2000's 0.28 h, as CanopyFlow says).
CANOPY_LU, CANOPY_LW = 1.0, 0.6
# Raupach (1994)'s roughness sublayer, z_w - d = c_w (h - d), c_w 2 (published; CanopyFlow's rule for the belts).
CW = 2.0
# The one-scale field (model 98): MIL-HDBK-1797's L_u and 2 L_w at 35 m at every height, sigma_w 0.594 sigma_u.
OLD_LH, OLD_LW, OLD_RATIO = 167.0, 35.0, 0.594
CUT = 0.5                     # the field's shortest wavelength, m (D-060)

BANDS = ((0.23, 0.5), (0.5, 1.0), (1.0, 2.0), (2.0, 4.0))
# The probe's encounter speeds at each height (measured: TheFieldsVerticalBands, Severe, 20 seeds) and u* there.
SPEED = {2: 18.0, 5: 18.2, 10: 18.4, 20: 18.7, 35: 18.9, 50: 19.1}
USTAR = 0.658


def scale_coefficient(sigma, a_inertial, transverse):
    """L/(z - d) for von Karman's spectrum of variance sigma^2 u*^2 whose inertial range is the surface layer's
    nS/u*^2 = a f^(-2/3) (derived: the longitudinal 1-D form's limit sigma^2 (2/pi) (1.339)^(-5/3) L^(-2/3) k^(-5/3), the
    transverse one's 4/3 of it, with k = 2 pi f / (z - d)). The same as L ~ sigma^3 / epsilon with epsilon = u*^3/(kappa z)."""
    c = (8 / (3 * math.pi)) if transverse else (2 / math.pi)
    return (a_inertial / (sigma ** 2 * c * KNEE ** (-5 / 3))) ** (-1.5) / (2 * math.pi)


C_H = scale_coefficient(SIGMA_U, A_U, False)
C_W = scale_coefficient(SIGMA_W, A_W, True)


def law(z):
    """The law's isotropic scales at height z over the ground, m: the surface layer's c (z - d), or the cover's canopy-top
    eddies where they are larger (the roughness sublayer)."""
    zd = min(z, TOP) - D
    return max(C_H * zd, CANOPY_LU * COVER), max(C_W * zd, CANOPY_LW * COVER)


def handbook(z):
    """MIL-HDBK-1797's low-altitude law (published; MathWorks' von Karman block and Deskos et al. 2020 restate it, read):
    L_u = 2 L_v = h/(0.177 + 0.000823 h)^1.2 and 2 L_w = h, h in ft; sigma_u/sigma_w = 1/(0.177 + 0.000823 h)^0.4. In the
    field's isotropic form L_w is h (its transverse integral scale h/2)."""
    h = z / 0.3048
    q = 0.177 + 0.000823 * h
    return h / q ** 1.2 * 0.3048, z, 1 / q ** 0.4


def integ(fn, lo, hi, m=400):
    s = 0.0
    for j in range(m):
        hz = lo * math.exp(math.log(hi / lo) * (j + 0.5) / m)
        s += fn(hz) * math.log(hi / lo) / m
    return s


def kaimal(comp, z, v, bands, ustar):
    def ns(hz):
        f = hz * (z - D) / v
        if comp == "w":
            return 2.1 * f / (1 + 5.3 * f ** (5 / 3))
        if comp == "u":
            return 105 * f / (1 + 33 * f) ** (5 / 3)
        return 17 * f / (1 + 9.5 * f) ** (5 / 3)
    return [math.sqrt(integ(ns, lo, hi)) * ustar for lo, hi in bands]


def von_karman(transverse, scale, sigma, v, bands):
    def ns(hz):
        k = 2 * math.pi * hz / v
        x = KNEE * scale * k
        if transverse:
            return sigma ** 2 * (scale / math.pi) * (1 + 8 / 3 * x * x) / (1 + x * x) ** (11 / 6) * k
        return sigma ** 2 * (2 * scale / math.pi) / (1 + x * x) ** (5 / 6) * k
    return [math.sqrt(integ(ns, lo, hi)) for lo, hi in bands]


def field(transverse, scale, sigma, v, bands, m=160):
    """The field's own 1-D spectrum along a path, by band (RMS, m/s): von Karman's 3-D E(k) of variance sigma^2 and scale
    `scale` cut at the field's 0.5 m (D-060), E11(k1) = int E/k (1 - k1^2/k^2) dk and E22(k1) = 1/2 int E/k (1 + k1^2/k^2) dk
    over k1 <= k <= 2 pi/0.5 (isotropy's relations, derived); met at v."""
    ke, kmax = 1 / (KNEE * scale), 2 * math.pi / CUT
    c = 1.5 * sigma ** 2 / (ke * 1.0325159139214712)

    def e(k):
        x = k / ke
        return c * x ** 4 / (1 + x * x) ** (17 / 6)

    def ns(hz):
        k1 = 2 * math.pi * hz / v
        if k1 >= kmax:
            return 0.0
        s = 0.0
        for j in range(m):
            k = k1 * math.exp(math.log(kmax / k1) * (j + 0.5) / m)
            w = (1 + k1 * k1 / (k * k)) / 2 if transverse else 1 - k1 * k1 / (k * k)
            s += e(k) * w * math.log(kmax / k1) / m  # dk/k = dln k
        return s * k1
    return [math.sqrt(integ(ns, lo, hi, 200)) for lo, hi in bands]


def ratio(a, b):
    return " / ".join(f"{x / y:.2f}" for x, y in zip(a, b))


def resolved(scale):
    """The share of an isotropic von Karman field's variance on wavelengths over the field's cut (derived)."""
    xmax = KNEE * scale * 2 * math.pi / CUT
    f = lambda x: x ** 4 / (1 + x * x) ** (17 / 6)
    n, top = 4000, 2000.0
    total = sum(f((i + 0.5) * top / n) for i in range(n)) * top / n
    part = sum(f((i + 0.5) * xmax / n) for i in range(n)) * xmax / n
    return part / total


def main():
    print(f"the law (derived): L_h = {C_H:.3f} (z - d), L_w = {C_W:.3f} (z - d) (isotropic; w's along a path half of it, "
          f"{C_W / 2:.3f} (z - d)); sigma_u = sigma_v = {SIGMA_U} u*, sigma_w = {SIGMA_W} u* (sigma_w/sigma_u {SIGMA_W / SIGMA_U:.4f})")
    print(f"  Kaimal's inertial constants a1 {A_U:.4f}, a3 {A_W:.4f}; Mann (1994)'s fit to Kaimal L = 0.59 z with k_e = 1/L "
          f"(DRDMannTurb restates it, read) is {0.59 / KNEE:.3f} z in the field's 1.339 convention")
    zw = D + CW * (COVER - D)
    print(f"  the cover's roughness sublayer (Raupach 1994, c_w {CW}): z_w = {zw:.3f} m, so the taper's height z_w - d = {zw - D:.3f} m; "
          f"the canopy-top floors L_h {CANOPY_LU * COVER:.2f} m, L_w {CANOPY_LW * COVER:.2f} m, met by the surface law at "
          f"z = {D + CANOPY_LU * COVER / C_H:.2f} and {D + CANOPY_LW * COVER / C_W:.2f} m")
    w20 = math.log((20 * 0.3048 - D) / Z0) / KARMAN
    print(f"  the handbook's sigma_w = 0.1 W20 over the field's profile (W20 {w20:.2f} u*): {0.1 * w20:.3f} u*; the one-scale field's "
          f"{OLD_RATIO * SIGMA_U:.3f} u*; its L_w = h over the law's: {35 / law(35)[1]:.2f} at 35 m, {2 / law(2)[1]:.2f} at 2 m")
    # The modes' longest wavenumber stays the one-scale field's, k_min = 0.25/(1.339 * 167 m): the law's largest scale, at the
    # profile's top, has this share of its variance below it (derived).
    lmax = law(TOP)[0]
    xmin = 0.25 / (KNEE * OLD_LH) * KNEE * lmax
    f = lambda x: x ** 4 / (1 + x * x) ** (17 / 6)
    below = sum(f((i + 0.5) * xmin / 2000) for i in range(2000)) * xmin / 2000 / 1.0325159139214712
    print(f"  the modes' k_min (the one-scale's): the law's largest scale {lmax:.1f} m keeps {below * 100:.2f} % of its variance below it")
    print("\nscales by height, m: the law's L_h / L_w; MIL-HDBK-1797's L_u / L_w (isotropic) / sigma_u/sigma_w; the one-scale "
          "field's 167 / 35; the share of w's variance the 0.5 m cut resolves, the law and the one-scale")
    for z in (0.35, 0.5, 0.75, 1.25, 2, 5, 10, 20, 35, 50, 100):
        lh, lw = law(z)
        hu, hw, hr = handbook(z)
        print(f"  {z:6.2f} m: {lh:7.2f} / {lw:6.3f};  handbook {hu:6.1f} / {hw:6.2f} / {hr:.3f};  resolved w {resolved(lw):.2f} / {resolved(OLD_LW):.2f}")

    print("\nw by band against Kaimal's at the probe's heights and encounter speeds (0.23-0.5 / 0.5-1 / 1-2 / 2-4 Hz): "
          "the one-scale field; the handbook's L_w = h with sigma_w 1.25 u*; the law")
    for z, v in SPEED.items():
        k = kaimal("w", z, v, BANDS, USTAR)
        old = von_karman(True, OLD_LW, OLD_RATIO * SIGMA_U * USTAR, v, BANDS)
        hb = von_karman(True, z, SIGMA_W * USTAR, v, BANDS)
        new = von_karman(True, law(z)[1], SIGMA_W * USTAR, v, BANDS)
        print(f"  {z:2d} m: Kaimal " + " / ".join(f"{x:.3f}" for x in k) + f" m/s; one-scale {ratio(old, k)}; handbook {ratio(hb, k)}; law {ratio(new, k)}")
    print("u (longitudinal) and v (transverse) by band against Kaimal's at the same speeds: the one-scale field; the handbook's L_u; the law")
    for z, v in SPEED.items():
        ku, kv = kaimal("u", z, v, BANDS, USTAR), kaimal("v", z, v, BANDS, USTAR)
        su = SIGMA_U * USTAR
        hu = handbook(z)[0]
        print(f"  {z:2d} m: u one-scale {ratio(von_karman(False, OLD_LH, su, v, BANDS), ku)}, handbook {ratio(von_karman(False, hu, su, v, BANDS), ku)}, "
              f"law {ratio(von_karman(False, law(z)[0], su, v, BANDS), ku)}; v one-scale {ratio(von_karman(True, OLD_LH, su, v, BANDS), kv)}, "
              f"law {ratio(von_karman(True, law(z)[0], su, v, BANDS), kv)}")

    print("\na hovering drone (a fixed point), 0.1-1 / 1-4 / 4-15 Hz: Kaimal's RMS at the local mean U(z), m/s; then over it the "
          "one-scale field and the law, each with the field's 0.5 m cut (the disc's average), met as the field is advected: "
          "[at U(z) | at the reference height's U, the field's frozen drift]")
    hover = ((0.1, 1.0), (1.0, 4.0), (4.0, 15.0))
    for name, u35 in (("Calm", 1.425), ("Windy", 4.75)):
        us = KARMAN * u35 / math.log((35 - D) / Z0)
        for z in (0.5, 0.75, 1.25, 2.0, 5.0):
            v = us / KARMAN * math.log((z - D) / Z0)
            lh, lw = law(z)
            cells = []
            for comp, transverse, s_old, l_old, s_new, l_new in (("u", False, SIGMA_U, OLD_LH, SIGMA_U, lh),
                                                                 ("v", True, SIGMA_U, OLD_LH, SIGMA_U, lh),
                                                                 ("w", True, OLD_RATIO * SIGMA_U, OLD_LW, SIGMA_W, lw)):
                k = kaimal(comp, z, v, hover, us)
                cell = []
                for speed in (v, u35):
                    o = field(transverse, l_old, s_old * us, speed, hover)
                    n = field(transverse, l_new, s_new * us, speed, hover)
                    cell.append(f"one-scale {ratio(o, k)}, law {ratio(n, k)}")
                cells.append(f"{comp} " + " / ".join(f"{x:.3f}" for x in k) + " [" + " | ".join(cell) + "]")
            print(f"  {name} at {z} m (U {v:.2f} m/s, u* {us:.3f}): " + "; ".join(cells))


if __name__ == "__main__":
    main()
