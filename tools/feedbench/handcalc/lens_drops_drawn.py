"""Hand numbers for w21-rain-on-camera task 1.2 (docs/reference-notes/lens-drops.md section 8): how a drop on the FPV camera's
front glass images. The entrance pupil's depth that puts the picture's corner bundle at the clear radius, where the picture's
chief rays cross the dome, the glass's angular scale and a contact line's softness, a drop cap's focal length, its defocus
blur across the pupil and its minification, the rim's deviation, the Fresnel transmission and the critical angle, and a
trail's pearls. Independent arithmetic: nothing here is read from the C#; the glass's numbers are lens-drops.md section 1's,
the picture's are FeedSettings' (k1 0.33, f 1.005 half-widths, 16:9)."""
import math

A_CLEAR = 4.5           # the front glass's clear radius, mm (assumed, section 1)
R_DOME = 8.0            # its radius of curvature, mm (assumed, section 1)
TH_R = math.radians(20)  # water's receding angle on it (assumed, section 4)
N_W = 1.333             # water's refractive index (published)
F_MM, F_NUMBER = 2.1, 2.0  # the class's M12 lens (published)
EP_R = F_MM / F_NUMBER / 2  # the entrance pupil's radius, mm (derived)
K1, FOCAL, ASPECT = 0.33, 1.005, 16 / 9  # the feed's lens (K section 11.4, FEED-02)


def ray(px, py):
    """The picture point (half-widths, y down) to its pinhole ray, camera axes (x right, y up, z out), unit."""
    s = 1 + K1 * (px * px + py * py)
    v = (px * s, -py * s, FOCAL)
    n = math.sqrt(sum(c * c for c in v))
    return tuple(c / n for c in v)


def dome(e, d):
    """Where the line from e along d leaves the dome (apex at the origin, centre at (0, 0, -R))."""
    o = (e[0], e[1], e[2] + R_DOME)
    b = sum(a * c for a, c in zip(o, d))
    c = sum(a * a for a in o) - R_DOME * R_DOME
    t = -b + math.sqrt(b * b - c)
    return tuple(e[i] + t * d[i] for i in range(3))


def footprint_reach(depth, d):
    return max(math.hypot(*dome((EP_R * math.cos(a), EP_R * math.sin(a), -depth), d)[:2])
               for a in (2 * math.pi * k / 360 for k in range(360)))


corner = ray(1, 9 / 16)
lo, hi = 0.01, 2 * R_DOME - 0.01  # the pupil inside the dome's sphere
for _ in range(80):
    mid = 0.5 * (lo + hi)
    lo, hi = (lo, mid) if footprint_reach(mid, corner) > A_CLEAR else (mid, hi)
DEPTH = 0.5 * (lo + hi)
print(f"entrance pupil {2 * EP_R:.2f} mm across, {DEPTH:.3f} mm behind the dome's apex (the corner bundle reaches {footprint_reach(DEPTH, corner):.3f} mm)")

for name, (px, py) in [("centre", (0, 0)), ("edge's middle", (1, 0)), ("top's middle", (0, 9 / 16)), ("corner", (1, 9 / 16))]:
    d = ray(px, py)
    q = dome((0, 0, -DEPTH), d)
    n = (q[0] / R_DOME, q[1] / R_DOME, (q[2] + R_DOME) / R_DOME)
    inc = math.degrees(math.acos(sum(a * b for a, b in zip(n, d))))
    print(f"  {name}: {math.degrees(math.acos(d[2])):.1f} deg off the axis, its chief ray {math.hypot(q[0], q[1]):.2f} mm out on the glass, "
          f"meeting it {inc:.1f} deg from its normal")
d = ray(0.01, 0)
deg_per_mm = math.degrees(math.acos(d[2])) / dome((0, 0, -DEPTH), d)[0]
print(f"  at the centre {deg_per_mm:.1f} deg of the picture per mm of glass: a contact line softened over +-{EP_R * deg_per_mm:.1f} deg")

t = math.tan(TH_R / 2)
mm_per_rad = math.degrees(1) / deg_per_mm
for r in (0.7, 1.5, 2.0):
    h = r * t
    cap = (r * r + h * h) / (2 * h)
    f = cap / (N_W - 1)
    print(f"a cap of {r} mm patch radius: surface radius {cap:.2f} mm, a {f:.1f} mm lens; across the pupil it blurs +-{math.degrees(EP_R / f):.1f} deg; "
          f"at the centre it spans about +-{r * deg_per_mm:.0f} deg and shows the scene at {1 - mm_per_rad / f:.2f} of its size")

print(f"the rim, {math.degrees(TH_R):.0f} deg, turns a ray along the glass's normal by {math.degrees(math.asin(N_W * math.sin(TH_R))) - math.degrees(TH_R):.1f} deg")


def transmission(theta):
    tt = math.asin(N_W * math.sin(theta))
    rs = (N_W * math.cos(theta) - math.cos(tt)) / (N_W * math.cos(theta) + math.cos(tt))
    rp = (math.cos(theta) - N_W * math.cos(tt)) / (math.cos(theta) + N_W * math.cos(tt))
    return 1 - 0.5 * (rs * rs + rp * rp)


worst = math.degrees(math.asin(math.sin(math.radians(26.5)) / N_W)) + math.degrees(TH_R)
print(f"critical angle water to air {math.degrees(math.asin(1 / N_W)):.1f} deg; the steepest ray in the water at the corner's rim {worst:.1f} deg; "
      f"Fresnel transmission {transmission(0):.3f} at normal, {transmission(math.radians(worst)):.3f} there")

# A trail's stretch: its film, Width wide and Film thick, gathered into pearls at the receding angle.
section = (TH_R - math.sin(TH_R) * math.cos(TH_R)) / (4 * math.sin(TH_R) ** 2)
shape = math.pi / 6 * t * (3 + t * t)
for width, film in ((2.0, 4e-3), (1.0, 1e-3), (3.0, 10e-3)):
    water = width * film
    w = math.sqrt(water / section)
    spacing = 4.51 * w
    rp = (water * spacing / shape) ** (1 / 3)
    print(f"a {film * 1e3:.0f} um film {width:.0f} mm wide: a {w:.3f} mm rivulet (section {section:.4f} w^2), pearls of {rp:.3f} mm radius "
          f"{spacing:.2f} mm apart, covering {math.pi * rp * rp / (spacing * width) * 100:.0f} % of the track")
