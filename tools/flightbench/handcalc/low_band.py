"""Hand numbers for p7c-wind design.md 15 (task 1.5c): what the air's vertical gusts can give the drone's slow pitch, and
what clip P's slow pitch would need. Run: python tools/flightbench/handcalc/low_band.py (prints every number design 15
quotes). Labels: published, derived, measured (the sim's probes, model 97: WindLevelsProbe.TheGustTransfer and
TheFieldsVerticalBands), assumed."""
import cmath
import math

KARMAN = 0.4                 # published
SIGMA_W_OVER_USTAR = 1.25    # published: the neutral surface layer's phi_w(0) (Panofsky and Dutton 1984; MOST's
#                              1.25(1 - 3 zeta)^(1/3), as Charrondiere and Stiperski 2023, Table 1, restate it; read)
IEC_LW_OVER_Z = 0.66 * 0.7   # published: IEC 61400-1's Kaimal model, L_w = 0.66 Lambda, Lambda = 0.7 z below 60 m (read
#                              through scientimate's windspectrum notes, a secondary source); sigma_w = 0.5 sigma_u

# The field (WindPreset, model 97): Severe's centre at 35 m over class 4's z0 and the stubble's d.
SEVERE, Z_REF, Z0, D = 9.5, 35.0, 0.10, 0.2
INTENSITY = 2.4 * KARMAN / math.log((Z_REF - D) / Z0)     # derived: sigma_u / U, 0.164
FIELD_SIGMA_W = 0.594 * INTENSITY                           # the field's sigma_w / U (MIL-HDBK-1797's ratio at 35 m)
USTAR_OVER_U = KARMAN / math.log((Z_REF - D) / Z0)         # derived: the log law's u* / U at the reference height

BANDS = ((0.23, 0.5), (0.5, 1.0), (1.0, 2.0), (2.0, 4.0))

# Clip P's pitch angle by band (measured, wind.md section 3.1, the whole clip's Welch spectra), degrees.
P_BANDS = (0.27, 0.155, 0.179, 0.121)

# The closed loop's pitch per m/s of a uniform step in the air (measured: WindLevelsProbe.TheGustTransfer, the noiseless
# human at clip P's trim, the whole airframe, model 97), degrees per m/s at each frequency (Hz).
H_HZ = (0.23, 0.3, 0.4, 0.5, 0.7, 1.0, 1.5, 2.0, 3.0, 4.0)
H_UP = (0.0785, 0.1295, 0.1950, 0.2513, 0.3218, 0.4051, 0.3195, 0.2219, 0.1181, 0.0706)
H_HEAD = (0.0243, 0.0319, 0.0432, 0.0509, 0.0594, 0.0764, 0.0584, 0.0371, 0.0263, 0.0254)
H_LEFT = (0.0053, 0.0069, 0.0098, 0.0158, 0.0176, 0.0497, 0.0165, 0.0240, 0.0097, 0.0110)

# The open-loop pitching moments at clip P's trim (measured: WindLevelsProbe.TheBudgetAtTheCruise, model 97, the hull's
# moment off as in flight), N m: per m/s of updraft, and per (m/s)/m of an updraft growing forward.
M_UPDRAFT = -0.0295 - 0.0137
M_GRADIENT = -0.0719
# The gust's gradient across each disc, over four rotors (derived, design 11): N m per (1/s).
M_DISC = 0.016

# The field's band RMS along the harness's course, Severe, 20 seeds x 120 s (measured: TheFieldsVerticalBands), m/s:
# w, headwind, crosswind, and w's difference over the hubs' 0.32 m.
FIELD = {
    2: ((0.465, 0.265, 0.209, 0.180), (0.400, 0.215, 0.196, 0.150), (0.415, 0.225, 0.214, 0.177), (0.036, 0.037, 0.055, 0.079)),
    5: ((0.460, 0.262, 0.207, 0.178), (0.398, 0.215, 0.195, 0.150), (0.421, 0.227, 0.217, 0.172), (0.034, 0.037, 0.054, 0.079)),
    10: ((0.461, 0.263, 0.208, 0.180), (0.396, 0.208, 0.194, 0.150), (0.428, 0.226, 0.212, 0.171), (0.035, 0.038, 0.055, 0.079)),
    20: ((0.450, 0.264, 0.207, 0.181), (0.394, 0.205, 0.192, 0.151), (0.432, 0.237, 0.216, 0.174), (0.036, 0.038, 0.055, 0.079)),
    35: ((0.457, 0.261, 0.205, 0.177), (0.387, 0.209, 0.193, 0.150), (0.424, 0.232, 0.217, 0.181), (0.035, 0.038, 0.055, 0.079)),
    50: ((0.454, 0.264, 0.209, 0.179), (0.392, 0.210, 0.196, 0.151), (0.423, 0.232, 0.217, 0.180), (0.034, 0.037, 0.055, 0.079)),
}
FIELD_SPEED = {2: 18.0, 5: 18.2, 10: 18.4, 20: 18.7, 35: 18.9, 50: 19.1}   # the encounter speed there, m/s (measured)
USTAR = 0.658                                                              # the 20 seeds' mean u*, m/s (measured)

# The footage's pitch angle at 0.4-1 Hz in its calm phases (measured, ground-micro-motion.md section 6), degrees.
CALM_PHASES = {"hover at 1-1.5 m (C, D, E)": (0.38, 0.62), "descent, last metres (A)": (0.20, 0.20),
               "low cruise at 6-9 m (S)": (0.18, 0.40)}

# The human pilot (design 13): McRuer's crossover and delay (published).
OMEGA_C, TAU_E = 4.3, 0.10


def kaimal_w(z, speed, lo, hi, ustar=USTAR, m=400):
    """Kaimal et al. (1972)'s neutral w spectrum, nS_w/u*^2 = 2.1 f/(1 + 5.3 f^(5/3)), f = n (z - d)/V (published,
    recalled; its inertial limit 0.396 f^(-2/3) against the 0.4 Charrondiere and Stiperski 2023 restate, read), as the
    drone meets it at V: the band's RMS, m/s."""
    s = 0.0
    for j in range(m):
        hz = lo * math.exp(math.log(hi / lo) * (j + 0.5) / m)
        f = hz * (z - D) / speed
        s += 2.1 * f / (1 + 5.3 * f ** (5 / 3)) * math.log(hi / lo) / m
    return math.sqrt(s) * ustar


def von_karman_w(scale, sigma, speed, lo, hi, m=400):
    """The field's w along a horizontal path: von Karman's transverse one-dimensional spectrum of an isotropic field of
    integral scale `scale` (WindField: E(k) with k_e = 1/(1.339 L)), F(k) = sigma^2 (L/pi) (1 + 8/3 x^2)/(1 + x^2)^(11/6),
    x = 1.339 L k, one-sided (derived; its integral scale L/2, MIL-HDBK-1797's L_w), met at `speed`: the band's RMS, m/s."""
    s = 0.0
    for j in range(m):
        hz = lo * math.exp(math.log(hi / lo) * (j + 0.5) / m)
        k = 2 * math.pi * hz / speed
        x = 1.339 * scale * k
        s += sigma ** 2 * (scale / math.pi) * (1 + 8 / 3 * x * x) / (1 + x * x) ** (11 / 6) * k * math.log(hi / lo) / m
    return math.sqrt(s)


def kaimal_peak():
    """The neutral w spectrum's peak f = nz/V, and its wavelength over z (derived from the form)."""
    f = (1 / (2 / 3 * 5.3)) ** 0.6
    return f, 1 / f


def interp(x, xs, ys):
    """Log-log interpolation of a measured transfer between its frequencies."""
    for i in range(len(xs) - 1):
        if xs[i] <= x <= xs[i + 1]:
            t = math.log(x / xs[i]) / math.log(xs[i + 1] / xs[i])
            return math.exp(math.log(ys[i]) + t * (math.log(ys[i + 1]) - math.log(ys[i])))
    raise ValueError(x)


def band_gain(lo, hi, h, m=200):
    """The transfer's RMS over a band (flat spectrum within it, assumed), per m/s."""
    s = 0.0
    for j in range(m):
        hz = lo * math.exp(math.log(hi / lo) * (j + 0.5) / m)
        s += interp(hz, H_HZ, h) ** 2 / m
    return math.sqrt(s)


def sensitivity(hz):
    """|1/(1 + L)| for McRuer's crossover pilot L = (w_c/s) e^(-s tau_e) closing the attitude loop (derived)."""
    s = 2j * math.pi * hz
    return abs(1 / (1 + OMEGA_C / s * cmath.exp(-s * TAU_E)))


def main():
    print(f"the field: sigma_u/U {INTENSITY:.3f}, sigma_w/U {FIELD_SIGMA_W:.4f}; the log law's u*/U {USTAR_OVER_U:.4f}, so the "
          f"published sigma_w/U {SIGMA_W_OVER_USTAR * USTAR_OVER_U:.4f} (the field's {FIELD_SIGMA_W / (SIGMA_W_OVER_USTAR * USTAR_OVER_U):.2f}x)")
    print(f"  at Severe's centre {SEVERE} m/s: the field's sigma_w {FIELD_SIGMA_W * SEVERE:.3f} m/s, published {SIGMA_W_OVER_USTAR * USTAR_OVER_U * SEVERE:.3f}")
    fp, lam = kaimal_peak()
    print(f"Kaimal's w peaks at f = nz/V {fp:.3f}: a wavelength of {lam:.2f} z; IEC's integral scale L_w {IEC_LW_OVER_Z:.3f} z; "
          f"the field's (its isotropic L 35 m at every height, w's along a path L/2) 17.5 m = {17.5 / 35:.2f} z at 35 m, {17.5 / 2:.1f} z at 2 m")

    sw = FIELD_SIGMA_W * USTAR / USTAR_OVER_U                  # the 20 seeds' field sigma_w, m/s
    print(f"\nw's RMS by band, 0.23-0.5 / 0.5-1 / 1-2 / 2-4 Hz, m/s, band integrals (like for like): the field's spectrum (sigma_w {sw:.3f}, "
          "L 35 m), Kaimal's (published), their ratio; MIL-HDBK-1797's own height law (L = h, the field's sigma_w) over Kaimal's; "
          "and the field as sampled (measured, Welch: its lowest band carries the 4.3 s windows' leakage from below 0.23 Hz)")
    for z, rows in FIELD.items():
        v = FIELD_SPEED[z]
        k = [kaimal_w(z, v, lo, hi) for lo, hi in BANDS]
        f = [von_karman_w(35, sw, v, lo, hi) for lo, hi in BANDS]
        h = [von_karman_w(z, sw, v, lo, hi) for lo, hi in BANDS]
        print(f"  {z:2d} m: field " + " / ".join(f"{x:.3f}" for x in f) + "; Kaimal " + " / ".join(f"{x:.3f}" for x in k)
              + "; field/Kaimal " + " / ".join(f"{a / b:.2f}" for a, b in zip(f, k))
              + "; L = h over Kaimal " + " / ".join(f"{a / b:.2f}" for a, b in zip(h, k))
              + "; sampled " + " / ".join(f"{x:.3f}" for x in rows[0]) + " (" + " / ".join(f"{a / b:.2f}" for a, b in zip(rows[0], f)) + " of the spectrum)")

    print("\nthe closed loop's pitch per m/s by band (the measured transfer's band RMS), deg/(m/s): updraft / headwind / from the left")
    gains = [(band_gain(lo, hi, H_UP), band_gain(lo, hi, H_HEAD), band_gain(lo, hi, H_LEFT)) for lo, hi in BANDS]
    for (lo, hi), g in zip(BANDS, gains):
        print(f"  {lo}-{hi} Hz: {g[0]:.3f} / {g[1]:.3f} / {g[2]:.3f}")
    c = H_UP[H_HZ.index(1.0)] / sensitivity(1.0)
    print(f"a pencil check: the crossover pilot's |S| (w_c {OMEGA_C}, tau_e {TAU_E} s) at 0.23 / 0.3 / 0.5 / 1 Hz "
          + " / ".join(f"{sensitivity(x):.3f}" for x in (0.23, 0.3, 0.5, 1.0))
          + f"; times the controller's own offset per m/s c = {c:.3f} deg/(m/s) (read at 1 Hz): "
          + " / ".join(f"{c * sensitivity(x):.3f}" for x in (0.23, 0.3, 0.5))
          + " against the measured " + " / ".join(f"{interp(x, H_HZ, H_UP):.3f}" for x in (0.23, 0.3, 0.5)))
    print(f"  so the controller's I-term holds the updraft's moment ({M_UPDRAFT:+.4f} N m per m/s) at {c:.3f} deg, a stiffness of "
          f"{abs(M_UPDRAFT) / math.radians(c):.1f} N m/rad, and the pilot's loop high-passes it below its crossover")

    print("\nthe pitch the air gives by band, deg (each component through its transfer, in quadrature), and clip P's")
    for z in (10, 35):
        _, hw, cw, _ = FIELD[z]
        f = [von_karman_w(35, sw, FIELD_SPEED[z], lo, hi) for lo, hi in BANDS]
        k = [kaimal_w(z, FIELD_SPEED[z], lo, hi) for lo, hi in BANDS]
        field = [math.sqrt((a * g[0]) ** 2 + (b * g[1]) ** 2 + (e * g[2]) ** 2) for a, b, e, g in zip(f, hw, cw, gains)]
        pub = [math.sqrt((a * g[0]) ** 2 + (b * g[1]) ** 2 + (e * g[2]) ** 2) for a, b, e, g in zip(k, hw, cw, gains)]
        print(f"  {z} m: the field's air " + " / ".join(f"{x:.3f}" for x in field) + "; with Kaimal's w " + " / ".join(f"{x:.3f}" for x in pub)
              + "; P " + " / ".join(f"{x:.3f}" for x in P_BANDS))
    need = [p / g[0] for p, g in zip(P_BANDS, gains)]
    k35 = [kaimal_w(35, FIELD_SPEED[35], lo, hi) for lo, hi in BANDS]
    print("  the updraft P's bands would need alone, m/s: " + " / ".join(f"{x:.2f}" for x in need)
          + "; Kaimal's at 35 m " + " / ".join(f"{x:.3f}" for x in k35) + ", so " + " / ".join(f"{a / b:.1f}x" for a, b in zip(need, k35))
          + f"; the published sigma_w of the whole spectrum {SIGMA_W_OVER_USTAR * USTAR:.2f}")

    print("\nthe gradients' moments in the low band (0.23-0.5 Hz), N m, against the uniform updraft's")
    f, _, _, dw = FIELD[35]
    uniform = abs(M_UPDRAFT) * f[0]
    grad = abs(M_GRADIENT) * dw[0] / 0.32
    disc = M_DISC * dw[0] / 0.32
    print(f"  uniform {uniform:.4f}; front to rear over 0.32 m {grad:.4f} ({grad / uniform:.2f} of it, in the field: each hub samples it); "
          f"across each disc, at most {disc:.4f} ({disc / uniform:.3f}, not in the field)")
    print(f"  P's 0.23-0.5 Hz needs a moment of {P_BANDS[0] / (gains[0][0] / abs(M_UPDRAFT)):.3f} N m RMS in that band")

    print("\nthe footage's own calm phases, pitch at 0.4-1 Hz, deg (against P's storm at 0.5-1 Hz, 0.155):")
    for k, (lo, hi) in CALM_PHASES.items():
        print(f"  {k}: {lo:.2f}-{hi:.2f}")


if __name__ == "__main__":
    main()
