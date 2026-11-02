"""The camera's turn between consecutive frames of one reference clip, by image registration (D-064, OPSEC SEC-01/02).

    python tools/reference/track_motion.py <clip under reference/> --letter S [--k1 0.33]

Clip P's tracker (track_attitude.py, wind.md section 1.3) reads the horizon. Near the ground and in a village the
skyline is houses and trees a few metres away, or out of view, so this one reads the whole picture instead
(docs/reference-notes/ground-micro-motion.md section 1):
1. ffmpeg's vidstabdetect matches square fields on a fixed grid between consecutive frames, to the whole pixel.
2. Each match is refined to a fraction of a pixel: the sum of squared differences at the match and its four
   neighbours, a parabola through each axis.
3. Both ends of a match are undistorted (k1, video-feed.md O1) and turned into rays through a pinhole of
   f = 1/sqrt(3 k1) half-widths, as track_attitude.py does (wind.md section 1.2).
4. A robust least-squares fit (two-point RANSAC, then Tukey's biweight) of a small rotation plus a forward-motion
   term gives the camera's turn between the frames: roll about the optical axis, pitch and yaw.
5. Fields that stay put while the fit says the picture moves (the OSD, the legs and the frame's parts in view) are
   masked after a first pass, and the fit runs again.

Signs as wind.md section 1.2: roll positive when the camera rolls right, pitch positive nose up, yaw positive turning
right. These are body (camera) rates: with 0 deg uptilt and near-level flight they are the attitude's.

Writes reference/_frames/<letter>/motion.csv (git-ignored; --out must also be inside reference/_frames/), one row
per frame, in track_attitude.py's columns where they mean the same (frame, time_s, roll_deg, pitch_deg,
yaw_rate_dps, conf_*, dup, flag), so wind_stats.py can read it. roll_deg and pitch_deg are the turns summed inside
each run of usable frames (relative, not the horizon's absolute angle). Per frame it also keeps:
- d_roll_deg, d_pitch_deg, d_yaw_deg: the turn from the previous frame; zoom: the forward-motion term
- the fit's diagnostics: n_fields, n_inliers, inlier_frac, span (the inliers' horizontal extent over the width),
  resid_px (the inliers' RMS residual)
- the same turn fitted on two halves of the inliers: a checkerboard of the grid (_a, _b), whose difference is the
  fit's own noise, and the top and bottom of the picture (_top, _bot), whose difference adds the parallax of the
  drone's own motion (near scenery below, far above)
- with --fields, each frame's matches and their weights in the fit, beside the log (motion_fields.csv), for
  ground_motion.py
- row_wobble_px: the RMS over field rows of the rows' median sideways residual; skew: the sideways residual's slope
  down the picture. A rolling shutter shaken faster than the frame rate (jello) shows here, not in the turn.
Nothing written or printed carries the clip's file name: ffmpeg's log is redacted.
"""
import argparse
import csv
import math
import random
import re
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageStat

from extract_frames import redact
from ref_paths import REPO_ROOT, RefError, clip_path, frames_dir, frames_path, letter

K1 = 0.33                  # video-feed.md O1, clip A's camera; P's horizon read straightest at 0.35 (wind.md 1.2)
SHAKINESS = 6              # vidstabdetect: search +-max(16, min(w, h) * 6 / 40) px = +-162 px at 1080 lines
MIN_CONTRAST = 0.15
RANSAC_TRIES = 40
INLIER_PX = 1.0            # a match within this of the fit (or 3 robust sigmas, if larger) is an inlier
STATIC_PX = 0.3            # a field moving less than this ...
STATIC_PRED_PX = 0.8       # ... while the fit says it should move more than this ...
STATIC_SHARE = 0.6         # ... in this share of its frames is camera-fixed
SMALL = (240, 135)         # the copy used for repeated frames and picture loss
COLUMNS = ("frame", "time_s", "roll_deg", "pitch_deg", "yaw_rate_dps", "conf_roll", "conf_pitch", "conf_yaw", "dup",
           "flag", "d_roll_deg", "d_pitch_deg", "d_yaw_deg", "zoom", "n_fields", "n_inliers", "inlier_frac", "span",
           "resid_px", "row_wobble_px", "skew",
           "d_roll_a", "d_pitch_a", "d_yaw_a", "d_roll_b", "d_pitch_b", "d_yaw_b",
           "d_roll_top", "d_pitch_top", "d_yaw_top", "d_roll_bot", "d_pitch_bot", "d_yaw_bot")
LM = re.compile(r"\(LM (-?\d+) (-?\d+) (-?\d+) (-?\d+) (\d+) ([-\d.eE]+) ([-\d.eE]+)\)")
FRAME = re.compile(r"^Frame (\d+) \(List \d+ \[(.*)\]\)\s*$")


def parse_args(argv):
    p = argparse.ArgumentParser(prog="track_motion.py", description=__doc__.split("\n")[0])
    p.add_argument("clip", help="the clip, a file under reference/")
    p.add_argument("--letter", type=letter, required=True, help="the clip's letter, A-Z")
    p.add_argument("--k1", type=float, default=K1, help=f"lens distortion (default {K1}); 0 for a pinhole")
    p.add_argument("--focal", type=float, help="pinhole focal length in half-widths (default 1/sqrt(3 k1))")
    p.add_argument("--out", help="CSV inside reference/_frames/ (default reference/_frames/<letter>/motion.csv)")
    p.add_argument("--fields", action="store_true",
                   help="also write each frame's matches beside it (<out>_fields.csv: frame, fx, fy, dx, dy, weight)")
    p.add_argument("--root", default=str(REPO_ROOT), help="repo root holding reference/ (default: this checkout)")
    args = p.parse_args(argv)
    if args.focal is None:
        if not args.k1 > 0:
            p.error("--focal is needed when --k1 is 0")
        args.focal = 1 / math.sqrt(3 * args.k1)
    if not args.focal > 0 or args.k1 < 0:
        p.error("--focal must be > 0 and --k1 >= 0")
    return args


# ---------------------------------------------------------------- vidstabdetect's matches

def parse_trf(text):
    """{frame (1-based): [(vx, vy, fx, fy, size, contrast)]} from vidstabdetect's ASCII result.
    The field at (fx, fy) in the frame matches the previous frame's at (fx + vx, fy + vy)."""
    out = {}
    for line in text.splitlines():
        m = FRAME.match(line)
        if m:
            out[int(m.group(1))] = [(int(a), int(b), int(c), int(d), int(e), float(f))
                                    for a, b, c, d, e, f, _ in LM.findall(m.group(2))]
    return out


def ssd(cur, prev, box, dx, dy):
    a = cur.crop(box)
    b = prev.crop((box[0] + dx, box[1] + dy, box[2] + dx, box[3] + dy))
    return ImageStat.Stat(ImageChops.difference(a, b)).sum2[0]


def vertex(m, c, p):
    """Offset of a parabola's vertex through (-1, m), (0, c), (1, p), within +-0.5."""
    den = m - 2 * c + p
    return 0.0 if den <= 0 else max(-0.5, min(0.5, 0.5 * (m - p) / den))


def refine(cur, prev, lm):
    """The match to a fraction of a pixel: (fx, fy, dx, dy), or None at the frame's edge."""
    vx, vy, fx, fy, size, _ = lm
    w, h = cur.size
    s2 = size // 2
    box = (fx - s2, fy - s2, fx - s2 + size, fy - s2 + size)
    inside = lambda dx, dy: (box[0] + dx >= 0 and box[1] + dy >= 0 and box[2] + dx <= w and box[3] + dy <= h)
    if not (box[0] >= 0 and box[1] >= 0 and box[2] <= w and box[3] <= h):
        return None
    for _ in range(3):                      # walk to the whole-pixel minimum, if the search's step missed it
        if not all(inside(vx + a, vy + b) for a, b in ((-1, 0), (1, 0), (0, -1), (0, 1))):
            return None
        c = ssd(cur, prev, box, vx, vy)
        xm, xp = ssd(cur, prev, box, vx - 1, vy), ssd(cur, prev, box, vx + 1, vy)
        ym, yp = ssd(cur, prev, box, vx, vy - 1), ssd(cur, prev, box, vx, vy + 1)
        step = min(((xm, -1, 0), (xp, 1, 0), (ym, 0, -1), (yp, 0, 1)))
        if step[0] >= c:
            return fx, fy, vx + vertex(xm, c, xp), vy + vertex(ym, c, yp)
        vx, vy = vx + step[1], vy + step[2]
    return None


# ---------------------------------------------------------------- rays and the rotation fit

class Lens:
    def __init__(self, width, height, k1, focal):
        self.cx, self.cy, self.hw, self.k1, self.f = (width - 1) / 2, (height - 1) / 2, width / 2, k1, focal
        self.f_px = focal * width / 2

    def ray(self, px, py):
        x, y = (px - self.cx) / self.hw, (py - self.cy) / self.hw
        s = 1 + self.k1 * (x * x + y * y)
        x, y, z = x * s, y * s, self.f
        n = math.sqrt(x * x + y * y + z * z)
        return x / n, y / n, z / n


def rows(a):
    """The model's three rows for ray a: b - a = a x w + tau * (az ax, az ay, az^2 - 1), unknowns (wx, wy, wz, tau).
    w is the camera's turn in its own axes (x right, y down, z forward)."""
    ax, ay, az = a
    return ((0.0, -az, ay, az * ax), (az, 0.0, -ax, az * ay), (-ay, ax, 0.0, az * az - 1))


def solve(eqs, weights=None):
    """Weighted least squares of [(row, rhs)] for the 4 unknowns; None if singular."""
    n = 4
    m = [[0.0] * (n + 1) for _ in range(n)]
    for k, (r, y) in enumerate(eqs):
        w = 1.0 if weights is None else weights[k // 3]
        if w == 0:
            continue
        for i in range(n):
            wi = w * r[i]
            for j in range(n):
                m[i][j] += wi * r[j]
            m[i][n] += wi * y
    for i in range(n):                       # Gauss-Jordan with partial pivoting
        p = max(range(i, n), key=lambda k: abs(m[k][i]))
        if abs(m[p][i]) < 1e-18:
            return None
        m[i], m[p] = m[p], m[i]
        for k in range(n):
            if k != i:
                f = m[k][i] / m[i][i]
                for j in range(i, n + 1):
                    m[k][j] -= f * m[i][j]
    return [m[i][n] / m[i][i] for i in range(n)]


def equations(pairs):
    out = []
    for a, b in pairs:
        for r, k in zip(rows(a), range(3)):
            out.append((r, b[k] - a[k]))
    return out


def residuals(pairs, x):
    """Per pair, the misfit's length (ray units) and its sideways part (x, ray units)."""
    out = []
    for a, b in pairs:
        e = [b[k] - a[k] - sum(r[i] * x[i] for i in range(4)) for k, r in enumerate(rows(a))]
        out.append((math.sqrt(e[0] ** 2 + e[1] ** 2 + e[2] ** 2), e[0]))
    return out


def fit(pairs, f_px, seed):
    """Robust fit: (x, weights) with x = (wx, wy, wz, tau), weights 0..1 per pair (0 = outlier); None if too few."""
    if len(pairs) < 6:
        return None
    eqs = equations(pairs)
    rng = random.Random(seed)
    thr = INLIER_PX / f_px
    best, best_n = None, -1
    for _ in range(RANSAC_TRIES):
        i, j = rng.sample(range(len(pairs)), 2)
        x = solve(eqs[3 * i:3 * i + 3] + eqs[3 * j:3 * j + 3])
        if x is None:
            continue
        n = sum(1 for r, _ in residuals(pairs, x) if r < thr)
        if n > best_n:
            best, best_n = x, n
    if best is None:
        return None
    x = best
    for _ in range(8):                       # Tukey's biweight, scale from the inliers' spread
        res = [r for r, _ in residuals(pairs, x)]
        core = sorted(r for r in res if r < 3 * thr) or sorted(res)
        sigma = max(1.4826 * core[len(core) // 2], 0.15 / f_px)
        c = max(4.685 * sigma, thr)
        w = [(1 - (r / c) ** 2) ** 2 if r < c else 0.0 for r in res]
        if sum(1 for v in w if v > 0) < 6:
            break
        nx = solve(eqs, w)
        if nx is None:
            break
        x = nx
    res = [r for r, _ in residuals(pairs, x)]
    core = sorted(r for r in res if r < 3 * thr) or sorted(res)
    sigma = max(1.4826 * core[len(core) // 2], 0.15 / f_px)
    c = max(4.685 * sigma, thr)
    return x, [(1 - (r / c) ** 2) ** 2 if r < c else 0.0 for r in res]


def angles(x):
    """(roll, pitch, yaw) in degrees from the camera's turn (wx, wy, wz): roll about z (forward), pitch about x
    (right; nose up is positive), yaw about y (down; a right turn is positive)."""
    return math.degrees(x[2]), math.degrees(x[0]), math.degrees(x[1])


def subfit(pairs, weights, keep):
    eqs = equations([p for p, k in zip(pairs, keep) if k])
    w = [v for v, k in zip(weights, keep) if k]
    if sum(1 for v in w if v > 0) < 6:
        return None
    x = solve(eqs, w)
    return None if x is None else angles(x)


# ---------------------------------------------------------------- the clip

def decode(ffmpeg, clip, args, width, height):
    """Yield each frame as a greyscale PIL image (the luma vidstabdetect matched)."""
    cmd = [ffmpeg, "-hide_banner", "-nostdin", "-loglevel", "error", "-i", str(clip), "-map", "0:v:0",
           "-fps_mode", "passthrough", "-pix_fmt", "gray", "-f", "rawvideo", "-"]
    proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    n = width * height
    try:
        while True:
            buf = proc.stdout.read(n)
            if len(buf) < n:
                break
            yield Image.frombytes("L", (width, height), buf)
    finally:
        proc.stdout.close()
        proc.wait()


def probe(ffprobe, clip, args):
    run = subprocess.run([ffprobe, "-v", "error", "-select_streams", "v:0", "-show_entries",
                          "stream=width,height:frame=best_effort_timestamp_time", "-of", "csv=p=0", str(clip)],
                         capture_output=True, text=True, encoding="utf-8", errors="replace")
    if run.returncode != 0:
        raise RefError(f"ffprobe failed (exit {run.returncode}):\n{redact(run.stderr[-400:], args.root, clip)}")
    times, size = [], None
    for line in run.stdout.split():
        parts = [p for p in line.split(",") if p]
        if len(parts) == 2 and size is None and "." not in parts[0]:
            size = int(parts[0]), int(parts[1])
        elif len(parts) == 1:
            try:
                times.append(float(parts[0]))
            except ValueError:
                raise RefError("a frame without a timestamp") from None
    if size is None or not times:
        raise RefError("ffprobe found no video frames")
    return size, [t - times[0] for t in times]


def stabdetect(ffmpeg, clip, args, workdir):
    trf = workdir / "motion.trf"
    vf = (f"vidstabdetect=shakiness={SHAKINESS}:accuracy=15:stepsize=6:mincontrast={MIN_CONTRAST}"
          f":fileformat=1:result={trf.name}")
    run = subprocess.run([ffmpeg, "-hide_banner", "-nostdin", "-loglevel", "error", "-i", str(clip),
                          "-map", "0:v:0", "-fps_mode", "passthrough", "-vf", vf, "-f", "null", "-"],
                         cwd=workdir, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if run.returncode != 0 or not trf.is_file():
        raise RefError(f"vidstabdetect failed (exit {run.returncode}):\n{redact(run.stderr[-400:], args.root, clip)}")
    text = trf.read_text(encoding="utf-8", errors="replace")
    trf.unlink()
    return parse_trf(text)


def clamp01(v):
    return max(0.0, min(1.0, v))


def track(args):
    clip = clip_path(args.root, args.clip)
    out = frames_path(args.root, args.out or frames_dir(args.root) / args.letter / "motion.csv", "--out")
    ffmpeg, ffprobe = shutil.which("ffmpeg"), shutil.which("ffprobe")
    if ffmpeg is None or ffprobe is None:
        raise RefError("ffmpeg/ffprobe not found on PATH; install them (docs/environment.md)")
    out.parent.mkdir(parents=True, exist_ok=True)
    (width, height), times = probe(ffprobe, clip, args)
    lms = stabdetect(ffmpeg, clip, args, out.parent)
    lens = Lens(width, height, args.k1, args.focal)

    # Pass 0: refine every match, and keep each frame's small copy for repeats and picture loss.
    matches, diff, spread, prev, prev_small = {}, [], [], None, None
    for k, img in enumerate(decode(ffmpeg, clip, args, width, height), 1):
        small = img.resize(SMALL, Image.BOX)
        spread.append(ImageStat.Stat(small).stddev[0])
        diff.append(ImageStat.Stat(ImageChops.difference(small, prev_small)).mean[0] if prev_small else math.nan)
        if prev is not None:
            ref = [(refine(img, prev, lm), lm[4]) for lm in lms.get(k, [])]
            matches[k] = [(r, size) for r, size in ref if r is not None]
        prev, prev_small = img, small
    n = len(diff)
    if n != len(times):
        raise RefError(f"decoded {n} frames but ffprobe listed {len(times)}")
    fps = (n - 1) / (times[-1] - times[0])
    moving = sorted(d for d in diff if d == d)
    dup_thr = 0.15 * moving[len(moving) // 2] if moving else 0.0
    dup = [d == d and d < dup_thr for d in diff]
    lost = [s < 4.0 for s in spread]

    def pairs_of(k, mask):
        out_ = []
        for (fx, fy, dx, dy), size in matches.get(k, []):
            if (fx, fy) in mask:
                continue
            out_.append(((lens.ray(fx + dx, fy + dy), lens.ray(fx, fy)), (fx, fy, size)))
        return out_

    def run_fits(mask):
        res = {}
        for k in range(2, n + 1):
            if dup[k - 1] or lost[k - 1] or lost[k - 2]:
                continue
            pf = pairs_of(k, mask)
            r = fit([p for p, _ in pf], lens.f_px, seed=k)
            if r is not None:
                res[k] = (pf, r)
        return res

    # Pass 1, then mask the fields that stay put while the picture moves, then pass 2.
    first = run_fits(set())
    still, seen = {}, {}
    for k, (pf, (x, _)) in first.items():
        for ((a, b), (fx, fy, _)) in pf:
            pred = [a[i] + sum(r[j] * x[j] for j in range(4)) for i, r in enumerate(rows(a))]
            pred_px = math.dist(pred, a) * lens.f_px
            if pred_px > STATIC_PRED_PX:
                seen[(fx, fy)] = seen.get((fx, fy), 0) + 1
                if math.dist(b, a) * lens.f_px < STATIC_PX:
                    still[(fx, fy)] = still.get((fx, fy), 0) + 1
    mask = {p for p, c in seen.items() if c >= 10 and still.get(p, 0) / c > STATIC_SHARE}
    fits = run_fits(mask)

    rows_out = []
    for k in range(1, n + 1):
        row = dict(frame=k, time_s=f"{times[k - 1]:.4f}", dup=int(dup[k - 1]), flag="no_picture" if lost[k - 1] else "ok")
        if k in fits:
            pf, (x, w) = fits[k]
            inl = [i for i, v in enumerate(w) if v > 0]
            d = angles(x)
            xs = [pf[i][1][0] for i in inl]
            span = (max(xs) - min(xs)) / width if xs else 0.0
            res = residuals([p for p, _ in pf], x)
            resid = math.sqrt(sum(res[i][0] ** 2 for i in inl) / len(inl)) * lens.f_px if inl else math.nan
            frac = len(inl) / len(pf)
            conf = clamp01((len(inl) - 8) / 24) * clamp01((frac - 0.25) / 0.35) * clamp01((span - 0.2) / 0.3)
            xs_all = sorted({q[1][0] for q in pf})
            ys_all = sorted({q[1][1] for q in pf})
            ix = {v: i for i, v in enumerate(xs_all)}
            iy = {v: i for i, v in enumerate(ys_all)}
            pairs = [p for p, _ in pf]
            ca = subfit(pairs, w, [(ix[q[1][0]] + iy[q[1][1]]) % 2 == 0 for q in pf])
            cb = subfit(pairs, w, [(ix[q[1][0]] + iy[q[1][1]]) % 2 == 1 for q in pf])
            top = subfit(pairs, w, [q[1][1] < height / 2 for q in pf])
            bot = subfit(pairs, w, [q[1][1] >= height / 2 for q in pf])
            by_row = {}
            for i in inl:
                by_row.setdefault(pf[i][1][1], []).append(res[i][1] * lens.f_px)
            meds = [(fy, sorted(v)[len(v) // 2]) for fy, v in by_row.items() if len(v) >= 3]
            wobble = math.sqrt(sum(m * m for _, m in meds) / len(meds)) if len(meds) >= 3 else math.nan
            if len(meds) >= 3:
                my = sum(fy for fy, _ in meds) / len(meds)
                sxx = sum((fy - my) ** 2 for fy, _ in meds)
                skew = sum((fy - my) * m for fy, m in meds) / sxx if sxx > 0 else math.nan
            else:
                skew = math.nan
            row.update(d_roll_deg=d[0], d_pitch_deg=d[1], d_yaw_deg=d[2], zoom=x[3], n_fields=len(pf),
                       n_inliers=len(inl), inlier_frac=frac, span=span, resid_px=resid, row_wobble_px=wobble,
                       skew=skew, conf=conf)
            for tag, v in (("a", ca), ("b", cb), ("top", top), ("bot", bot)):
                if v is not None:
                    row.update({f"d_roll_{tag}": v[0], f"d_pitch_{tag}": v[1], f"d_yaw_{tag}": v[2]})
        elif dup[k - 1] and not lost[k - 1] and k > 1:
            row.update(d_roll_deg=0.0, d_pitch_deg=0.0, d_yaw_deg=0.0, conf=1.0)
        rows_out.append(row)

    # Angles summed inside runs of usable frames, from 0 at each run's first frame; a repeated frame holds the run
    # (the recording shows the same view).
    roll = pitch = None
    for row in rows_out:
        c = row.pop("conf", 0.0)
        if c >= 0.5:
            roll = 0.0 if roll is None else roll + row["d_roll_deg"]
            pitch = 0.0 if pitch is None else pitch + row["d_pitch_deg"]
            row.update(roll_deg=roll, pitch_deg=pitch, yaw_rate_dps=row["d_yaw_deg"] * fps)
        else:
            roll = pitch = None
        row.update(conf_roll=c, conf_pitch=c, conf_yaw=c)

    with out.open("w", newline="", encoding="utf-8") as f:
        wr = csv.writer(f)
        wr.writerow(COLUMNS)
        for row in rows_out:
            wr.writerow([fmt(row.get(c, "")) for c in COLUMNS])
    if args.fields:
        with out.with_name(out.stem + "_fields.csv").open("w", newline="", encoding="utf-8") as f:
            wr = csv.writer(f)
            wr.writerow(("frame", "fx", "fy", "dx", "dy", "weight"))
            for k, (pf, (_, w)) in sorted(fits.items()):
                for ((fx, fy, dx, dy), _), v in zip(((m[0], m[1]) for m in matches[k] if (m[0][0], m[0][1]) not in mask), w):
                    wr.writerow((k, fx, fy, f"{dx:.4f}", f"{dy:.4f}", f"{v:.3f}"))
    return out, n, fps, len(mask), sum(1 for r in rows_out if r["conf_roll"] >= 0.5)


def fmt(v):
    if isinstance(v, float):
        return "" if v != v else f"{v:.6g}"
    return v


def main(argv=None):
    args = parse_args(argv)
    try:
        out, n, fps, masked, usable = track(args)
    except RefError as e:
        print(f"track_motion: error: {e}", file=sys.stderr)
        return 2
    print(f"track_motion: {args.letter}: {n} frames at {fps:.3f} fps, {usable} usable, {masked} camera-fixed fields "
          f"masked -> reference/_frames/{out.relative_to(frames_dir(args.root)).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
