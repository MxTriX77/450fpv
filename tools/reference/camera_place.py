"""Where the FPV camera sits: the feed's projection, the share of a turn a front blade is in the picture,
and a scan of the lens's place against it (w14-camera-place 1.1, docs/reference-notes/camera-place.md).

    python tools/reference/camera_place.py                 # the scan over the lens's height and depth
    python tools/reference/camera_place.py --lens 0 0.015 0.115

Reads no reference file: the measured rod lines are typed into the note, and the geometry is drone.json's.
Body axes as drone.json: x right, y up, z forward; the camera looks along +z at 0 deg uptilt.
The projection is the feed's (FEED-02): pinhole f in half-widths, then p_u = p_d (1 + k1 r_d^2) with r in
half-widths, a 16:9 picture (u in [-1, 1], v in [-9/16, 9/16], v down).
"""
import argparse
import math

F, K1, HALF_H = 1.005, 0.33, 9 / 16
HUB_Y, HUB_XZ, PROP_R, HUB_R = 0.035, 0.15981, 0.127, 0.0095  # drone.json motors, props; the hub's 19 mm


def distort(uu, vu, k1=K1):
    """Undistorted half-width coordinates to the picture's: solve p_u = p_d (1 + k1 r_d^2) along the ray."""
    ru = math.hypot(uu, vu)
    if ru == 0:
        return 0.0, 0.0
    rd = ru
    for _ in range(40):  # Newton on rd (1 + k1 rd^2) = ru
        rd -= (rd * (1 + k1 * rd * rd) - ru) / (1 + 3 * k1 * rd * rd)
    return uu * rd / ru, vu * rd / ru


def undistort(ud, vd, k1=K1):
    s = 1 + k1 * (ud * ud + vd * vd)
    return ud * s, vd * s


def project(p, lens, f=F):
    """A body point to the picture (u right, v down, half-widths), or None behind the lens."""
    dx, dy, dz = p[0] - lens[0], p[1] - lens[1], p[2] - lens[2]
    if dz <= 1e-6:
        return None
    return distort(f * dx / dz, -f * dy / dz)


def in_picture(uv):
    return uv is not None and abs(uv[0]) <= 1 and abs(uv[1]) <= HALF_H


def blade_share(lens, side=1, steps=720, stations=40):
    """Share of a turn one blade of the front prop on `side` (+1 right, -1 left) has any part of its centre
    line in the picture (w13's design.md §1), and the topmost and lowest picture rows it reaches there, as
    fractions of the picture's height from its top edge (None when it never enters)."""
    hub = (side * HUB_XZ, HUB_Y, HUB_XZ)
    seen = 0
    rows = []
    for i in range(steps):
        th = 2 * math.pi * i / steps
        c, s = math.cos(th), math.sin(th)
        hit = False
        for j in range(stations + 1):
            r = HUB_R + (PROP_R - HUB_R) * j / stations
            uv = project((hub[0] + r * c, hub[1], hub[2] + r * s), lens)
            if in_picture(uv):
                hit = True
                rows.append((uv[1] + HALF_H) / (2 * HALF_H))
        seen += hit
    return seen / steps, ((min(rows), max(rows)) if rows else None)


def rod_slope(lens, x, y):
    """A rod parallel to the lens's axis at body (x, y): its undistorted image is the line v = slope * |u| through
    the centre, slope = (lens_y - y) / |x| (positive: below the lens)."""
    return (lens[1] - y) / abs(x)


def main(argv=None):
    p = argparse.ArgumentParser(prog="camera_place.py", description=__doc__.split("\n")[0])
    p.add_argument("--lens", type=float, nargs=3, metavar=("X", "Y", "Z"))
    a = p.parse_args(argv)
    if a.lens:
        share, band = blade_share(tuple(a.lens))
        where = f", from {band[0]:.0%} to {band[1]:.0%} of the height down" if band else ""
        print(f"lens {a.lens}: a front blade in the picture {share:.1%} of a turn{where}")
        return
    ys = [round(-0.040 + 0.010 * i, 3) for i in range(7)]
    zs = [round(0.115 + 0.015 * i, 3) for i in range(7)]
    print("share of a turn (%) a front blade is in the picture; rows lens y (m), columns lens z (m)")
    print("y \\ z  " + " ".join(f"{z:6.3f}" for z in zs))
    for y in ys:
        print(f"{y:+.3f} " + " ".join(f"{100 * blade_share((0.0, y, z), steps=360)[0]:6.1f}" for z in zs))


if __name__ == "__main__":
    main()
