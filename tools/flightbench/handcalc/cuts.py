"""Hand numbers for openspec/changes/p7d-tether/design.md, decisions 25 and 26 (task 1.11i): the pilot's cut table.
Design 25: the line's break over the exit hole's edge, the elastica's bend there under the line's tension and a Weibull of
the glass marked by the hole. Design 26: the empirical law of a liftoff's cut, the pilot's account. Run:
python tools/flightbench/handcalc/cuts.py (prints every number the design quotes). Each input is labelled published,
derived, measured, assumed or pilot where it is set; tether.json's rows carry the same labels and their rationale.

Sources (published):
- Prysmian, "Product lifetime" (fiber reliability note): intrinsic strength of 125 um silica fiber about 4.8 GPa at 5 %/min.
- OFS, "Optical fiber strength and reliability": a pristine population's Weibull slope 50 or more; a damaged, broad one
  2.5-20.
- STL, "Impact of adverse cable handling": about 99 % of long-length breaks above 2.75 GPa.
- Kurkjian group (SPIE): the same marked fibers break with a Weibull modulus of about 2 in tension and about 7 in two-point
  bending; a bend over an edge is a bending test, so m = 7.
- ITU-T G.652/G.657, the fiber's geometry (125 um glass, 250 um coating) and its proof test 0.69 GPa (100 kpsi).
- Design 18: the elastica's free arm over a point edge, R = sqrt(EI/T) / (2 sin(theta/4)), its stress E r / R + T / A_g,
  the edge's tension bounded by the pack, T_edge <= exp(mu theta) G irr.

The one assumed parameter set against the pilot's account (D-121): the hole's scale sigma_0, drawn per flight uniform in
spool.hole_edge_strength_pa (1.0-1.4 GPa). The pilot's canister exit is an open kit question.
"""
import json
import math
import os

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "..")
T = json.load(open(os.path.join(ROOT, "assets-src", "tether.json"), encoding="utf-8"))


def v(block, key):
    return T[block][key]["value"]


E = v("fiber", "youngs_modulus_pa")              # published
R_G = v("fiber", "glass_diameter_m") / 2         # published
EI = v("fiber", "bending_stiffness_n_m2")        # derived
A_G = v("fiber", "glass_area_m2")                # derived
SECTION = v("fiber", "section_length_m")         # assumed
M = v("strength", "edge_weibull_modulus")        # published (Kurkjian group, bending)
HOLE = v("spool", "hole_edge_strength_pa")       # assumed (D-121): [[0, lo], [1, hi]]
S0_LO, S0_HI = HOLE[0][1], HOLE[-1][1]


def edge_stress(t, theta_deg):
    """The glass's stress at the hole's edge, Pa: the elastica's free arm (design 18) at tension t, N, round theta."""
    s = math.sin(math.radians(theta_deg) / 4)
    r = math.sqrt(EI / t) / (2 * s)
    return E * R_G / r + t / A_G


def p_section(sigma, s0):
    """A section's chance to break over the edge at stress sigma: 1 - exp(-(sigma/sigma_0)^m) (FiberCuts.EdgeStrength)."""
    return 1 - math.exp(-((sigma / s0) ** M))


def p_flight(sigma, n):
    """A flight's chance with n sections over the edge at sigma, sigma_0 uniform over the hole's band (one draw a flight)."""
    k = 200
    return sum(1 - (1 - p_section(sigma, S0_LO + (S0_HI - S0_LO) * (i + 0.5) / k)) ** n for i in range(k)) / k


print("Design 25: the edge's stress (derived, the elastica; the pack's bound may hold the edge's tension lower)")
print("  T (N)  " + "".join(f"{th:>9}deg" for th in (60, 90, 120, 150, 170)))
for t in (0.01, 0.02, 0.05, 0.1, 0.2):
    print(f"  {t:5.2f}  " + "".join(f"{edge_stress(t, th) / 1e9:9.3f}GPa" for th in (60, 90, 120, 150, 170)))

print(f"\nThe Weibull per section over the edge (m = {M}, published): its chance at sigma for sigma_0 = 1.0 / 1.2 / 1.4 GPa")
for s in (0.3e9, 0.5e9, 0.6e9, 0.8e9, 1.0e9, 1.2e9):
    print(f"  sigma {s / 1e9:4.2f} GPa: " + " / ".join(f"{100 * p_section(s, s0):6.2f} %" for s0 in (1.0e9, 1.2e9, 1.4e9)))

# Measured (flightbench --cuts 40 on this branch, model 108's build): each row's most stress at the edge per flight while
# whole (mean over the 40 flights) and its rate. n_eff: the sections at that stress that would give the rate (sigma_0 mid-band),
# the line that slides over the edge near its peak, section_length_m each.
ROWS = [
    ("sharp pitch, 110 deg", 0.800, 0.63),
    ("sharp pitch, full throttle", 0.556, 0.83),
    ("roll with yaw", 0.787, 0.90),
    ("roll alone", 0.289, 0.00),
    ("pitch flip", 0.878, 1.00),
    ("proper brake, top speed", 0.508, 0.25),
    ("proper brake, 65 km/h", None, 0.15),
    ("gentle 180 deg turn", 0.369, 0.00),
    ("cruise, Windy", 0.206, 0.08),
]
print("\nRows of the table (measured mean of each flight's most edge stress, the rate): the sections implied at sigma_0 = 1.2 GPa")
for name, s, rate in ROWS:
    if s is None or rate <= 0 or rate >= 1:
        print(f"  {name:28s} {'' if s is None else f'{s:.3f} GPa'} rate {100 * rate:5.1f} %")
        continue
    per = p_section(s * 1e9, 1.2e9)
    n = math.log(1 - rate) / math.log(1 - per)
    print(f"  {name:28s} {s:.3f} GPa  rate {100 * rate:5.1f} %  one section {100 * per:6.3f} %  n_eff {n:7.1f} ({n * SECTION:5.1f} m of line)")
print("  The gap: the column is each flight's most while the line is whole, so a break's own step (its highest stress) is not in\n"
      "  it, and with m = 7 the rate follows the tail, not the mean: n_eff is an upper bound on the line that slid over the edge\n"
      "  near its peak. The full-throttle pitch cuts mostly at a disc (32 of 33), not at the edge; the cruise's 3 cuts are the\n"
      "  edge's at a gust's peak far above its mean. Where the edge cuts, the rows rank by sigma^m: roll alone and the gentle\n"
      "  turn (0.29-0.37 GPa) never, the brake (0.51) a quarter, the snaps and flips (0.79-0.88) most.")

print("\nDesign 26: the liftoff's chance (empirical, the pilot's account; the thrust-over-weight nodes derived from the scenarios)")
CURVE = v("launch", "liftoff_cut_by_thrust")
BACK = v("launch", "liftoff_cut_by_back_speed")
EXTRA, SAFE, CAP = v("launch", "liftoff_lean_extra"), v("launch", "liftoff_safe_chance"), v("launch", "liftoff_cut_cap")


def curve(xy, x):
    if x <= xy[0][0]:
        return xy[0][1]
    for (x0, y0), (x1, y1) in zip(xy, xy[1:]):
        if x < x1:
            return y0 + (y1 - y0) * (x - x0) / (x1 - x0)
    return xy[-1][1]


# Measured (the liftoff probe on the session's loaded drone, seeds 1-3): the window's mean thrust over weight.
for name, tw in (("a 0.5 m/s climb", 1.09), ("the pilot's own careful climb", 1.26), ("80 % throttle in 0.1 s", 1.96),
                 ("max throttle in 0.1 s", 2.46)):
    p = curve(CURVE, tw)
    print(f"  {name:32s} T/W {tw:4.2f}: {100 * p:5.1f} %; nose up or roll: {100 * min(CAP, p + EXTRA):5.1f} %;"
          f" nose down (the safe launch): {100 * min(p, SAFE):4.1f} %")
for vb in (0.5, 1.5, 3.0, 5.0):
    print(f"  tail first at {vb:3.1f} m/s: {100 * curve(BACK, vb):5.1f} %")
print("  The line's own mechanics at these liftoffs (measured, design 25): the exit's tension 0.01-0.05 N, the edge 0.2-0.5 GPa,\n"
      f"  one section's chance there at sigma_0 = 1.2 GPa {100 * p_section(0.5e9, 1.2e9):.3f} % at most: physics does not reach"
      " the pilot's liftoff rows, so the empirical law carries them.")
