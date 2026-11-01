"""The drone's motion over the ground from a track_motion.py match log: speed over height, climb, crab, and the camera's
tilt and roll against the ground (D-064; docs/reference-notes/ground-micro-motion.md section 5).

    python tools/reference/ground_motion.py --letter S [--every 3] [--k1 0.33] [--size 1920x1080]

Reads reference/_frames/<letter>/motion_fields.csv (track_motion.py --fields) and fits, per frame, the flow of a flat
ground seen by a camera that turns and moves:
    b = a + a x w - (a . G) (e - (a . e) a)
with a and b a match's rays in the previous and the current frame, w the camera's turn, e the direction of its motion
(unit) and G = (the frame's travel / height) times the ground's down direction, in the camera's axes. A ray that looks
above the horizon (a . G <= 0) sees no ground and is taken as far. Scenery above the ground (houses, trees) and
mismatches are down-weighted (Gauss-Newton, Tukey's biweight). So per frame:
- v_over_h: speed over height, 1/s (|G| times the frame rate)
- tilt_deg: the camera's optical axis below the horizon; roll_ground_deg: the camera's roll against the ground (positive
  rolled right), both from G's direction
- climb_deg: the motion above the horizontal; crab_deg: the motion's direction right of the camera's heading
- inlier_frac, resid_px: the fit's own diagnostics
A height in metres needs a scale in the scene (ground-micro-motion.md section 5.2, with to_ground()); the speed is
v_over_h times it.
Writes reference/_frames/<letter>/ground.csv (git-ignored) in track_motion.py's columns, with this fit's turn (its
halves refitted with the motion held at the whole fit's), so motion_bands.py --csv reads it, plus the columns above;
prints the per-second medians.
"""
import argparse
import csv
import math
import sys
from pathlib import Path

from ref_paths import REPO_ROOT, RefError, frames_dir, frames_path, letter
from track_motion import COLUMNS, K1, Lens, clamp01, fmt
from track_motion import rows as tm_rows

NAN = math.nan
GROUND = ("v_over_h", "tilt_deg", "roll_ground_deg", "climb_deg", "crab_deg")


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def unit_e(ex, ey):
    n = math.sqrt(ex * ex + ey * ey + 1)
    return ex / n, ey / n, 1 / n


def predict(a, p):
    """The model's b for ray a and parameters p = (wx, wy, wz, Gx, Gy, Gz, ex, ey)."""
    w, g, e = p[0:3], p[3:6], unit_e(p[6], p[7])
    inv = max(0.0, dot(a, g))
    ae = dot(a, e)
    rx = cross(a, w)
    return tuple(a[k] + rx[k] - inv * (e[k] - ae * a[k]) for k in range(3))


def solve(m, v):
    n = len(v)
    m = [row[:] + [v[i]] for i, row in enumerate(m)]
    for i in range(n):
        piv = max(range(i, n), key=lambda k: abs(m[k][i]))
        if abs(m[piv][i]) < 1e-30:
            return None
        m[i], m[piv] = m[piv], m[i]
        for k in range(n):
            if k != i:
                f = m[k][i] / m[i][i]
                for j in range(i, n + 1):
                    m[k][j] -= f * m[i][j]
    return [m[i][n] / m[i][i] for i in range(n)]


def fit(pairs, p0, f_px, iters=12):
    """Gauss-Newton with Tukey weights; returns (p, weights, residual RMS of the inliers in px) or None."""
    p = list(p0)
    w = [1.0] * len(pairs)
    for it in range(iters):
        res = []
        for a, b in pairs:
            q = predict(a, p)
            res.append([b[k] - q[k] for k in range(3)])
        norms = [math.sqrt(dot(r, r)) for r in res]
        core = sorted(norms)
        sigma = max(1.4826 * core[len(core) // 2], 0.2 / f_px)
        c = 4.685 * sigma
        w = [(1 - (r / c) ** 2) ** 2 if r < c else 0.0 for r in norms]
        if sum(1 for v in w if v > 0) < 12:
            return None
        jt = [[0.0] * 8 for _ in range(8)]
        jr = [0.0] * 8
        for (a, b), r, wi in zip(pairs, res, w):
            if wi == 0:
                continue
            q0 = predict(a, p)
            cols = []
            for j in range(8):
                h = 1e-6 if j < 6 else 1e-4
                pj = p[:]
                pj[j] += h
                q = predict(a, pj)
                cols.append([(q[k] - q0[k]) / h for k in range(3)])
            for k in range(3):
                for i in range(8):
                    jr[i] += wi * cols[i][k] * r[k]
                    for j in range(8):
                        jt[i][j] += wi * cols[i][k] * cols[j][k]
        for i in range(8):
            jt[i][i] *= 1.0 + 1e-6
        step = solve(jt, jr)
        if step is None:
            return None
        p = [x + s for x, s in zip(p, step)]
        if max(abs(s) for s in step[:6]) < 1e-9:
            break
    inl = [math.sqrt(dot(r, r)) for r, v in zip(res, w) if v > 0]
    return p, w, math.sqrt(sum(r * r for r in inl) / len(inl)) * f_px


def describe(p, fps):
    g, e = p[3:6], unit_e(p[6], p[7])
    gn = math.sqrt(dot(g, g))
    d = tuple(v / gn for v in g)                                  # the ground's down direction, camera axes
    tilt = math.degrees(math.asin(max(-1.0, min(1.0, d[2]))))    # optical axis below the horizon
    roll = math.degrees(math.atan2(d[0], d[1]))                  # rolled right: down leans to the camera's +x
    climb = math.degrees(math.asin(max(-1.0, min(1.0, -dot(e, d)))))
    # the motion's direction against the camera's heading, both projected on the level plane
    fwd = tuple(z - d[2] * dk for z, dk in zip((0.0, 0.0, 1.0), d))
    eh = tuple(ek - dot(e, d) * dk for ek, dk in zip(e, d))
    right = cross(d, fwd)                                         # level, to the camera's right (y down x z forward)
    crab = math.degrees(math.atan2(dot(eh, right), dot(eh, fwd)))
    return dict(v_over_h=gn * fps, tilt_deg=tilt, roll_ground_deg=roll, climb_deg=climb, crab_deg=crab)


def to_ground(lens, px, py, tilt_deg, roll_deg):
    """A pixel's point on the flat ground, (ahead, right) in heights, for a camera tilted tilt_deg below the horizon
    and rolled roll_deg right (describe()'s angles); None above the horizon."""
    t, r = math.radians(tilt_deg), math.radians(roll_deg)
    d = (math.cos(t) * math.sin(r), math.cos(t) * math.cos(r), math.sin(t))   # the ground's down, camera axes
    fwd = tuple(z - d[2] * dk for z, dk in zip((0.0, 0.0, 1.0), d))
    n = math.sqrt(dot(fwd, fwd))
    fwd = tuple(v / n for v in fwd)
    right = cross(d, fwd)
    a = lens.ray(px, py)
    s = dot(a, d)
    if s <= 0:
        return None
    return dot(a, fwd) / s, dot(a, right) / s


def load_fields(path):
    out = {}
    for r in csv.DictReader(Path(path).open(encoding="utf-8")):
        out.setdefault(int(r["frame"]), []).append((int(r["fx"]), int(r["fy"]), float(r["dx"]), float(r["dy"])))
    return out


def turn_only(pairs, p, keep):
    """The turn (roll, pitch, yaw, deg) of a subset of the matches with the motion held at the full fit's."""
    m = [[0.0] * 3 for _ in range(3)]
    v = [0.0] * 3
    n = 0
    zero = [0.0, 0.0, 0.0] + list(p[3:])
    for (a, b), k in zip(pairs, keep):
        if not k:
            continue
        n += 1
        q = predict(a, zero)                                     # the ray after the motion alone
        for r, j in zip(tm_rows(a), range(3)):
            y = b[j] - q[j]
            for i in range(3):
                v[i] += r[i] * y
                for l in range(3):
                    m[i][l] += r[i] * r[l]
    if n < 6:
        return None
    x = solve(m, v)
    return None if x is None else (math.degrees(x[2]), math.degrees(x[0]), math.degrees(x[1]))


def run(args):
    base = frames_dir(args.root) / args.letter
    fields_path, motion_path = base / "motion_fields.csv", base / "motion.csv"
    if not fields_path.is_file() or not motion_path.is_file():
        raise RefError("no match log: run track_motion.py --fields first")
    out = frames_path(args.root, base / "ground.csv", "the output")
    motion = {int(r["frame"]): r for r in csv.DictReader(motion_path.open(encoding="utf-8"))}
    times = [float(r["time_s"]) for r in motion.values()]
    fps = (len(times) - 1) / (times[-1] - times[0])
    fields = load_fields(fields_path)
    lens = Lens(args.width, args.height, args.k1, args.focal)
    rows, p_prev = {}, None
    for k in sorted(fields)[::args.every]:
        fl = fields[k]
        pairs = [(lens.ray(fx + dx, fy + dy), lens.ray(fx, fy)) for fx, fy, dx, dy in fl]
        if len(pairs) < 30:
            p_prev = None
            continue
        m = motion[k]
        w0 = [math.radians(float(m[c] or 0)) for c in ("d_pitch_deg", "d_yaw_deg", "d_roll_deg")]
        tilt0 = math.radians(args.tilt)
        p0 = p_prev or (w0 + [0.0, 0.03 * math.cos(tilt0), 0.03 * math.sin(tilt0), 0.0, -math.tan(tilt0)])
        r = fit(pairs, w0 + list(p0[3:]), lens.f_px)
        if r is None:
            p_prev = None
            continue
        p, w, resid = r
        p_prev = p
        inl = [i for i, v in enumerate(w) if v > 0]
        xs = [fl[i][0] for i in inl]
        span = (max(xs) - min(xs)) / args.width
        frac = len(inl) / len(w)
        conf = clamp01((len(inl) - 8) / 24) * clamp01((frac - 0.25) / 0.35) * clamp01((span - 0.2) / 0.3)
        gx, gy = sorted({f[0] for f in fl}), sorted({f[1] for f in fl})
        ix, iy = {v: i for i, v in enumerate(gx)}, {v: i for i, v in enumerate(gy)}
        live = [v > 0 for v in w]
        halves = dict(a=[l and (ix[f[0]] + iy[f[1]]) % 2 == 0 for l, f in zip(live, fl)],
                      b=[l and (ix[f[0]] + iy[f[1]]) % 2 == 1 for l, f in zip(live, fl)],
                      top=[l and f[1] < args.height / 2 for l, f in zip(live, fl)],
                      bot=[l and f[1] >= args.height / 2 for l, f in zip(live, fl)])
        roll, pitch, yaw = math.degrees(p[2]), math.degrees(p[0]), math.degrees(p[1])
        row = dict(frame=k, time_s=float(m["time_s"]), dup=m["dup"], flag=m["flag"], d_roll_deg=roll, d_pitch_deg=pitch,
                   d_yaw_deg=yaw, zoom=math.sqrt(dot(p[3:6], p[3:6])), n_fields=len(w), n_inliers=len(inl),
                   inlier_frac=frac, span=span, resid_px=resid, conf=conf)
        for tag, keep in halves.items():
            t = turn_only(pairs, p, keep)
            if t is not None:
                row.update({f"d_roll_{tag}": t[0], f"d_pitch_{tag}": t[1], f"d_yaw_{tag}": t[2]})
        row.update(describe(p, fps))
        rows[k] = row
    # the same log as track_motion.py's, with the motion over the ground beside it
    out_rows, roll = [], None
    for k, m in sorted(motion.items()):
        row = rows.get(k) or dict(frame=k, time_s=float(m["time_s"]), dup=m["dup"], flag=m["flag"])
        if k not in rows and m["dup"] == "1" and m["flag"] == "ok" and k > 1:
            row.update(d_roll_deg=0.0, d_pitch_deg=0.0, d_yaw_deg=0.0, conf=1.0)
        c = row.pop("conf", 0.0)
        if c >= 0.5:
            roll = (0.0, 0.0) if roll is None else (roll[0] + row["d_roll_deg"], roll[1] + row["d_pitch_deg"])
            row.update(roll_deg=roll[0], pitch_deg=roll[1], yaw_rate_dps=row["d_yaw_deg"] * fps)
        else:
            roll = None
        row.update(conf_roll=c, conf_pitch=c, conf_yaw=c)
        out_rows.append(row)
    cols = COLUMNS + GROUND
    with out.open("w", newline="", encoding="utf-8") as f:
        wr = csv.writer(f)
        wr.writerow(cols)
        for r in out_rows:
            wr.writerow([fmt(r.get(c, "")) for c in cols])
    return [r for r in out_rows if "v_over_h" in r]


def main(argv=None):
    p = argparse.ArgumentParser(prog="ground_motion.py", description=__doc__.split("\n")[0])
    p.add_argument("--letter", type=letter, required=True, help="the clip's letter, A-Z")
    p.add_argument("--every", type=int, default=1, help="fit every n-th frame (default 1)")
    p.add_argument("--k1", type=float, default=K1)
    p.add_argument("--focal", type=float, help="pinhole focal length in half-widths (default 1/sqrt(3 k1))")
    p.add_argument("--size", default="1920x1080", help="the clip's frame size (default 1920x1080)")
    p.add_argument("--tilt", type=float, default=30.0, help="first guess of the camera's tilt below the horizon, deg")
    p.add_argument("--root", default=str(REPO_ROOT), help="repo root holding reference/ (default: this checkout)")
    args = p.parse_args(argv)
    args.width, args.height = (int(v) for v in args.size.split("x"))
    args.focal = args.focal or 1 / math.sqrt(3 * args.k1)
    try:
        rows = run(args)
    except RefError as e:
        print(f"ground_motion: error: {e}", file=sys.stderr)
        return 2
    by_s = {}
    for r in rows:
        by_s.setdefault(int(r["time_s"]), []).append(r)
    print("second  v/h 1/s  tilt  roll  climb  crab  inliers  resid px")
    for s, rs in sorted(by_s.items()):
        med = lambda k: sorted(x[k] for x in rs)[len(rs) // 2]
        print(f"{s:5d}  {med('v_over_h'):7.3f}  {med('tilt_deg'):5.1f}  {med('roll_ground_deg'):+5.1f}  {med('climb_deg'):+5.1f}  "
              f"{med('crab_deg'):+5.1f}  {med('inlier_frac'):6.2f}  {med('resid_px'):6.2f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
