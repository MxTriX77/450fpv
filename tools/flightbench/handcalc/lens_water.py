"""Hand numbers for w21-rain-on-camera design.md (task 1.1): the water on the FPV camera's lens. The rain's spectrum (Marshall-
Palmer's shape, its intercept set so the drops carry the flight's rate), the drops reaching the lens's dome by their velocity
relative to the camera (hover, into and away from a wind, forward flight), what an impact leaves and how far it spreads, a
drop's adhesion against gravity and the air's shear at the lens, its sliding speed and its trail, and a prop disk's share of
the drops crossing it. Independent arithmetic: nothing here is read from the C#; the drone's numbers are drone.json's, the
lens's are lens-drops.md's."""
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
with open(os.path.join(ROOT, "assets-src", "drone", "drone.json"), encoding="utf-8") as f:
    DRONE = json.load(f)

G = 9.80665
RHO_W = 1000.0          # RainPreset.WaterDensity
GAMMA = 0.0735          # water's surface tension at 15 C, N/m (published)
MU_W = 1.138e-3         # water's viscosity at 15 C, Pa s (published)
RHO_A = DRONE["aero"]["air_density_kg_m3"]["value"]   # 1.225, ISA (published)
NU_A = 1.5e-5           # air's kinematic viscosity, m2/s (published, 15-20 C)

A_CLEAR = 4.5e-3        # the front glass's clear radius, m (assumed)
A_BARREL = 7.0e-3       # the barrel's front face radius, m (assumed)
R_CURV = 8.0e-3         # the front glass's radius of curvature, m (assumed)
TH_A = math.radians(50) # advancing angle (assumed)
TH_R = math.radians(20) # receding angle (assumed)
K_RET = 1.0             # retention factor (published, measured 0.98 +- 0.08)
CD = 1.0                # a drop's drag coefficient on its frontal area (assumed)
SLOPE = 0.006           # Ca = SLOPE * (F - Fc) / (gamma V^(1/3)) (derived from Le Grand et al. 2005)
ELL = 15.0              # ln(b/a), 1 mm over 0.3 nm (derived)
LLD = 0.94              # Landau-Levich-Derjaguin (published)
SPLASH_WE = 1.0e6       # Brown, Jepsen and Yoon 2008: splashed share We/(We + 1e6) (published)
LAAN_A = 1.24           # Laan et al. 2014 (published)
HOMANN = 1.312          # axisymmetric stagnation flow's f''(0) (published)
BLASIUS = 0.332         # flat plate (published)

EDGES = [0.1, 0.2, 0.3, 0.5, 0.7, 1.0, 1.3, 1.7, 2.2, 2.8, 3.5, 4.5, 6.0]   # mm


def fall(d_mm):
    return 9.65 - 10.3 * math.exp(-0.6 * d_mm)


def spectrum(rate):
    """Per bin: the number density (m^-3), the geometric-mid diameter (mm), its fall speed and the bin's mean drop volume (m3);
    Marshall-Palmer's slope, the intercept set so the drops carry the rate; and the intercept against 8000."""
    lam = 4.1 * rate ** -0.21
    bins = []
    for d0, d1 in zip(EDGES[:-1], EDGES[1:]):
        n = (math.exp(-lam * d0) - math.exp(-lam * d1)) / lam        # per unit N0 (m^-3 mm^-1 * mm)
        def m3(d):
            return -math.exp(-lam * d) * (d ** 3 / lam + 3 * d * d / lam ** 2 + 6 * d / lam ** 3 + 6 / lam ** 4)
        mean_d3 = (m3(d1) - m3(d0)) / n                                 # mm3
        dm = math.sqrt(d0 * d1)
        bins.append([n, dm, fall(dm), math.pi / 6 * mean_d3 * 1e-9])
    carried = sum(n * v * vol for n, _, v, vol in bins)                 # m/s of water per unit N0
    n0 = rate / 3.6e6 / carried
    for b in bins:
        b[0] *= n0
    return bins, n0


def facets(rings=(1, 6, 12)):
    """The dome's facets: (x, y, normal, true area)."""
    out = []
    nr = len(rings)
    for ring, sectors in enumerate(rings):
        r0, r1 = A_CLEAR * ring / nr, A_CLEAR * (ring + 1) / nr
        for s in range(sectors):
            p0, p1 = 2 * math.pi * s / sectors, 2 * math.pi * (s + 1) / sectors
            rc = math.sqrt(0.5 * (r0 * r0 + r1 * r1)) if ring else 0.0
            pc = 0.5 * (p0 + p1)
            x, y = rc * math.cos(pc), rc * math.sin(pc)
            nz = math.sqrt(R_CURV ** 2 - rc * rc)
            n = (x / R_CURV, y / R_CURV, nz / R_CURV)
            proj = 0.5 * (r1 * r1 - r0 * r0) * (p1 - p0)
            out.append((x, y, n, proj / n[2]))
    return out


def rot_x(v, a):
    """v turned about +x by a (camera axes: +y up, +z forward; a > 0 pitches the nose down for the world-to-camera turn below)."""
    x, y, z = v
    c, s = math.cos(a), math.sin(a)
    return (x, c * y - s * z, s * y + c * z)


def arrivals(rate, air_cam, pitch_down):
    """Drops a second and water m3/s on the dome, no shelter: air_cam is the air relative to the camera in its axes; the fall
    is turned into the camera's axes for a body pitched nose-down by pitch_down."""
    bins, _ = spectrum(rate)
    fall_dir = rot_x((0.0, -1.0, 0.0), -pitch_down)
    count = water = 0.0
    for x, y, n, area in facets():
        for nb, dm, v, vol in bins:
            u = tuple(air_cam[i] + v * fall_dir[i] for i in range(3))
            onto = -(u[0] * n[0] + u[1] * n[1] + u[2] * n[2])
            if onto > 0:
                count += nb * onto * area
                water += nb * onto * area * vol
    return count, water


def cap(volume, theta=TH_R):
    t = math.tan(theta / 2)
    r = (6 * volume / (math.pi * t * (3 + t * t))) ** (1 / 3)
    return r, r * t


def frontal(r, theta=TH_R):
    rs = r / math.sin(theta)
    return rs * rs * (theta - math.sin(theta) * math.cos(theta))


def retention(r):
    return K_RET * GAMMA * 2 * r * (math.cos(TH_R) - math.cos(TH_A))


def bl_share(h, u_n, u_t):
    k = 2 / math.pi * max(u_n, 0) / A_BARREL
    d1 = math.sqrt(NU_A / k) / HOMANN if k > 0 else math.inf
    d2 = math.sqrt(NU_A * A_BARREL / abs(u_t)) / BLASIUS if u_t else math.inf
    d = min(d1, d2)
    x = h / d
    return x * x / 3 if x <= 1 else 1 - 2 / (3 * x)


def main():
    print("== the spectrum: Marshall-Palmer's slope, the intercept that carries the rate (vs 8000 m^-3 mm^-1)")
    for rate in (2.5, 5, 10, 25, 50):
        bins, n0 = spectrum(rate)
        print(f"  R {rate:5.1f} mm/h: N0 {n0:7.0f}  drops/m3 {sum(b[0] for b in bins):6.0f}")

    print("== drops reaching the dome (no shelter), R 5 mm/h; drops/s, mm3/s")
    cases = {
        "hover, calm, level": ((0, 0, 0), 0.0),
        "hover, 5 m/s wind into the lens": ((0, 0, -5.0), 0.0),
        "hover, 5 m/s wind from behind": ((0, 0, 5.0), 0.0),
        "forward 20 m/s, pitched 25 deg": (rot_x((0, 0, -20.0), -math.radians(25)), math.radians(25)),
        "forward 20 m/s, level lens": ((0, 0, -20.0), 0.0),
    }
    for name, (air, pitch) in cases.items():
        c, w = arrivals(5, air, pitch)
        print(f"  {name:34s} {c:6.3f} /s  {w * 1e9:7.4f} mm3/s")
    # the flat-lens ratio the spec's first scenario names: hover in a 5 m/s headwind vs 20 m/s forward, same (level) lens
    bins, _ = spectrum(5)
    flat_h = sum(b[0] * 5.0 for b in bins)
    flat_f = sum(b[0] * 20.0 for b in bins)
    print(f"  flat lens, level: forward 20 / headwind 5 = {flat_f / flat_h:.3f} (the normal components' ratio)")

    print("== an impact: splashed share, deposit, spread radius")
    for d_mm, un in ((0.3, 5), (1.0, 5), (1.5, 18), (2.5, 18), (1.0, 2)):
        d = d_mm * 1e-3
        we = RHO_W * d * un * un / GAMMA
        re = RHO_W * un * d / MU_W
        p = we * re ** -0.4
        beta = re ** 0.2 * math.sqrt(p) / (LAAN_A + math.sqrt(p))
        vol = math.pi / 6 * d ** 3 * (1 - we / (we + SPLASH_WE))
        r, h = cap(vol)
        print(f"  D {d_mm} mm at {un} m/s: We {we:7.0f} splashed {we / (we + SPLASH_WE) * 100:5.2f} %  spread radius "
              f"{beta * d_mm / 2:5.2f} mm  rests r {r * 1e3:5.2f} mm h {h * 1e3:5.3f} mm")

    print("== a drop at the receding angle: retention against gravity (lens vertical) and the air at the lens's centre")
    for vol_mm3 in (0.01, 0.1, 1, 3, 10, 30):
        vol = vol_mm3 * 1e-9
        r, h = cap(vol)
        fr = retention(r)
        fg = RHO_W * vol * G
        a = frontal(r)
        # the air sweeping the lens's centre in forward flight: u_t 20 sin 25, u_n 20 cos 25
        un, ut = 20 * math.cos(math.radians(25)), 20 * math.sin(math.radians(25))
        fa = 0.5 * RHO_A * CD * a * ut * ut * bl_share(h, un, ut)
        # the edge-on speed that moves it alone (no gravity): F_ret = 1/2 rho Cd A u^2 share, share ~1 for h >> delta
        uc = math.sqrt(fr / (0.5 * RHO_A * CD * a))
        print(f"  V {vol_mm3:5.2f} mm3: r {r * 1e3:5.2f} mm  F_ret {fr * 1e6:6.1f} uN  weight {fg * 1e6:6.2f} uN  "
              f"air at 20 m/s {fa * 1e6:6.1f} uN  edge-on speed to move it {uc:5.1f} m/s")
    # the largest drop gravity alone holds on a vertical lens: rho V g = F_ret
    lo, hi = 1e-12, 1e-6
    for _ in range(200):
        mid = math.sqrt(lo * hi)
        if RHO_W * mid * G > retention(cap(mid)[0]):
            hi = mid
        else:
            lo = mid
    print(f"  gravity alone slides a drop over {lo * 1e9:.1f} mm3 (r {cap(lo)[0] * 1e3:.2f} mm) down a vertical lens")
    print(f"  We_h,crit check (drop inside a linear boundary layer): 9 k dcos = {9 * K_RET * (math.cos(TH_R) - math.cos(TH_A)):.2f}"
          f" here; anodised aluminium's dcos 0.8-0.9 gives {9 * 0.8:.1f}-{9 * 0.9:.1f} against the published 7.5 +- 0.5")

    print("== sliding: the speed for an excess force, the trail's onset and film")
    ca_star = TH_R ** 3 / (9 * ELL)
    print(f"  Ca* (Cox-Voinov's receding angle at 0) {ca_star:.2e}: {ca_star * GAMMA / MU_W * 100:.2f} cm/s")
    for vol_mm3, excess_un in ((3, 20), (3, 60), (10, 100)):
        vol = vol_mm3 * 1e-9
        r, h = cap(vol)
        u = SLOPE * excess_un * 1e-6 / (MU_W * vol ** (1 / 3))
        ca = MU_W * u / GAMMA
        film = LLD * h * ca ** (2 / 3) if ca > ca_star else 0
        print(f"  V {vol_mm3} mm3, excess {excess_un} uN: {u * 100:6.1f} cm/s  Ca {ca:.2e}  trail film {film * 1e6:5.2f} um "
              f"(lost {film * 2 * r * A_CLEAR / vol * 100:4.1f} % over the clear radius)")

    print("== evaporation, left out: a 10 um film's life at 95 % humidity (Sibulkin's stagnation mass transfer)")
    d_v, rho_sat, rh = 2.5e-5, 12.8e-3, 0.95     # vapour diffusivity m2/s, saturated vapour kg/m3 at 15 C (published); humidity (assumed)
    for un in (18.0, 5.0):
        k = 2 / math.pi * un / A_BARREL
        h_m = 0.763 * (NU_A / d_v) ** 0.4 * d_v * math.sqrt(k / NU_A)
        life = RHO_W * 10e-6 / (h_m * rho_sat * (1 - rh))
        print(f"  {un:4.1f} m/s onto the lens: h_m {h_m:.3f} m/s, life {life:5.0f} s")

    print("== a prop disk's share of the drops crossing it, at 0.75 R")
    props = DRONE["props"]
    blade = props["blade"]
    st = blade["stations_r_over_r"]["value"]
    i = min(range(len(st)), key=lambda j: abs(st[j] - 0.75))
    radius = props["diameter_m"]["value"] / 2
    c, t, beta = blade["chord_m"]["value"][i], blade["thickness_m"]["value"][i], blade["section_angle_rad"]["value"][i]
    nb = props["blades"]["value"]
    r = st[i] * radius
    kt = props["k_t_n_per_rad2_s2"]["value"]
    omega = math.sqrt(4.8 * G / 4 / kt)
    for w in (1, 5, 10, 20):
        p = min(1.0, nb / (2 * math.pi) * (c / r + omega * (c * math.sin(beta) + t) / w))
        print(f"  hover {omega * 60 / (2 * math.pi):5.0f} rpm, drops crossing at {w:2d} m/s: {p * 100:5.1f} % hit a blade")


if __name__ == "__main__":
    main()
