"""Hand numbers for design.md 2.12: each kind's stalk at its own length (allometry), its self-buckling height, the
slipstream's fold and the folded arch's crest, its critical speed and a cut's work."""
import math

g = 9.81
EXP = 1 / 1.53              # Niklas 1995: H ∝ D^1.53 over 76 herbaceous species, so D ∝ H^0.654
CD, KEDGE = 1.15, 3.0
Q = 230.0                    # Pa, the hover's T/A (2.2, derived)
CREST = 0.5                  # the folded arch's crest over l_b (elastica.py: 0.43–0.61 for L/l_b 3–6)
HF_TIP, U_TIP = 2.18e-3, 117.5

kinds = {
    #            d at 1 m,  wall/d,  rho,   E,     e_s
    "green":    (1.81e-3, 0.125, 1000., 6.4e9, 10e3),
    "dry":      (4.49e-3, 0.125, 400., 2.0e9, 15e3),
    "burnt":    (4.49e-3, 0.0625, 300., 2.0e9, 7.5e3),
}


def stalk(kind, L):
    d1, w, rho, E, es = kinds[kind]
    d = d1 * L ** EXP
    di = d * (1 - 2 * w)
    A = math.pi / 4 * (d * d - di * di)
    I = math.pi / 64 * (d ** 4 - di ** 4)
    EI, mu = E * I, rho * A
    Lc = 1.986 * (EI / (mu * g)) ** (1 / 3)
    Fc = es * A / d
    vc = KEDGE * (Fc * math.sqrt(d) / (2 * math.sqrt(2 * math.pi) * (EI * mu ** 3) ** 0.25)) ** (2 / 3)
    lb = (EI / (Q * CD * d)) ** (1 / 3)
    Afold = Q * CD * d * L ** 3 / (8 * EI)
    tau = L * L / (1.875 ** 2 * math.sqrt(EI / mu))
    cut = es * A + mu * HF_TIP * U_TIP ** 2
    return dict(d=d * 1e3, EI=EI, mu=mu * 1e3, Lc=Lc, SF=Lc / L, vc=vc, lb=lb, A=Afold, crest=min(L, CREST * lb), tau=tau,
                cut=cut * 1e3, J=cut / U_TIP)


if __name__ == "__main__":
    for kind in kinds:
        print(kind)
        for L in (0.3, 0.5, 0.8, 1.0, 1.3, 1.5, 1.77):
            s = stalk(kind, L)
            print("  L {:.2f} m: d {d:.2f} mm, EI {EI:.2e}, mu {mu:.2f} g/m, self-buckling {Lc:.2f} m (SF {SF:.2f}), v_c {vc:.1f} m/s, "
                  "A at 230 Pa {A:.2f}, l_b {lb:.3f} m, folded crest {crest:.3f} m, tau {tau:.3f} s, cut at the tip {cut:.1f} mJ, J {J:.2e} N s".format(L, **s))
    # the old 1.5 mm culm, for the record
    d, w, rho, E = 1.5e-3, 0.125, 500., 2e9
    di = d * 0.75
    I = math.pi / 64 * (d ** 4 - di ** 4); A = math.pi / 4 * (d * d - di * di)
    print("2.2's dry 1.5 mm culm self-buckles at {:.2f} m".format(1.986 * (E * I / (rho * A * g)) ** (1 / 3)))
    d, rho, E = 1.5e-3, 1000., 1e9
    print("2.2's green 1.5 mm culm self-buckles at {:.2f} m".format(1.986 * (E * I / (rho * A * g)) ** (1 / 3)))
