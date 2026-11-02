"""Rain on the feed: the rain clip P against the dry control clips (docs/reference-notes/wind.md section 7).

Ported to plain Python (standard library only; ffmpeg and ffprobe decode the clips) from
godot-final:tools/reference/rain_stats.py, a Blender Python script, together with the parts of
godot-final:tools/reference/track_attitude.py it imports (decoder, camera-fixed mask, horizon fit, lens
model). D-037; ported, never merged, D-027.

    python tools/wind/rain_stats.py [--reuse] [--ref <repo>/reference]

Decodes every frame of P, and every in-flight frame of the dry controls A-F, H and L, at native size,
measures each one and prints every number of wind.md section 7. It writes only inside reference/_frames/P/
(git-ignored):
- rain_stats.json: every printed number, missing values as null
- rain_stats/<letter>.json: the per-frame measurements. --reuse reads them instead of decoding again.

Definitions (wind.md section 7.1):
- Frame sets. P: its picture frames outside the four N12 outages (video-feed.md, precursor frames
  included), with MARGIN frames on each side. Controls: the in-flight window of video-feed.md 1.1,
  i.e. the picture frames before the last 1.0 s ahead of the first loss (L never loses picture).
- Statistics use a 2x2 area-averaged copy (960x540) without the camera-fixed mask (OSD, airframe
  parts, border). Sparkles, grain, sky blobs and the rod edge use the native frame.
- Levels are 8-bit code values. Luma is BT.601 and chroma the length of (Cb, Cr). Texture is the mean of
  |3x3 box mean - 13x13 box mean| of luma; relative texture divides it by the mean luma.
- Horizon: the tracker's fit on the 960x540 copy, compared with P's attitude.csv when it exists.
  Profiles use the frames with confidence >= 0.5, and each pixel's depression below the skyline from
  that frame's roll and pitch.
- Sparkles (video-feed.md N2): native pixels whose chroma departs > 45 levels from its 7x7 mean, per
  thousand unmasked pixels. Grain (N1): the robust (MAD) sigma of luma minus its 5x5 mean over sky
  at least 3 deg above the skyline.
- Veil: the darkest scene pixels (p0.5) above the receiver's own black, the darkest 1 % of the
  camera-fixed pixels (OSD outlines and border).
- Rod: the 10-90 % width of the right airframe rod's upper edge, from its oversampled edge-spread
  function, per frame and in the clip's mean image; the undershoot on its dark side (P3 halo) as a
  share of the edge step.
- Edge bands: the mean luma of a band at the left and right edge a quarter of the way down (where the
  front blades cross), minus the frame's median block luma (N13's level steps), against the mean of
  its two neighbour frames.

Port (where this differs from the Blender originals, and why):
- Decoding: ffmpeg (on PATH, docs/environment.md) pipes every frame as 8-bit RGB in decode order
  (-fps_mode passthrough), where Blender's sequencer rendered each frame; the frame count and rate
  come from ffprobe. Both give integer code values, as the original rounded them. Frame numbers follow
  ffmpeg's decode order; the outage and belt ranges are the original's (Blender's) numbers, so
  tracker_fit's per-frame comparison with attitude.csv is the check that they line up.
- No numpy: images are flat arrays; box means are separable edge-padded prefix sums (the original's
  2-D cumulative sum, the same numbers to rounding), polyfit and SVD are closed-form least squares.
  It is far slower than the original (several seconds per native frame): run it as a long job.
- Random draws (RANSAC pairs, the 2000 pre-onset windows) come from Python's random.Random with the
  original's seeds (the frame number, 0), not numpy's PCG64: the same procedure, not the same numbers.
- The per-frame files are JSON (rain_stats/<letter>.json) instead of numpy's .npz.
- The clip is located through reference/_frames/letters.md, the restart's letter map (the first
  attempt's index.md, read as a fallback, names files that are no longer there), and checked by
  tools/reference/ref_paths.py (it must lie under reference/); no message names it, and ffmpeg's log is
  never shown.
- A picture frame without a confident horizon keeps NaN bands, as the original's array did; the
  rod's mean image takes every picture frame of the window, as before.
- Arguments: plain argparse (no Blender "--" separator; no Blender version check); main(argv) returns an
  exit code.

OPSEC: letters only. Nothing this tool writes may be committed, copied out of reference/ or uploaded.
"""
import argparse
import csv
import json
import math
import random
import shutil
import subprocess
import sys
import time
import traceback
from array import array
from bisect import bisect_right
from itertools import accumulate, chain, compress, repeat
from operator import sub
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parent / "reference"))
from wind_stats import NAN, fin, finite, mean, percentile, std  # noqa: E402  (JSON without NaN, statistics)
import ref_paths  # noqa: E402  (clips only from under reference/, SEC-01)

VERSION = 1                          # of the per-frame files; --reuse decodes again on a mismatch
RAIN, DAY, ROD, NIGHT = "P", ("A", "B", "C", "D", "E", "F"), "H", "L"
CLIPS = (RAIN,) + DAY + (ROD, NIGHT)
FIRST_LOSS = dict(A=424, B=310, C=174, D=208, E=263, F=250, H=628)   # video-feed.md N5-N8
LOSS_GUARD = 30                      # the in-flight window ends 1.0 s before the first loss
OUTAGES = ((676, 687), (951, 958), (966, 985), (1144, 1164))       # P's N12 outages, precursors included
MARGIN = 2                           # frames kept clear on each side of a P outage (onsets can be torn)
BELT = (121, 330)                    # P's tree-belt crossing (wind.md 1.6)
STILL_S, BURST = 1.0, 8              # extract_frames.py's defaults: stills every 1 s, bursts of 8 frames
DEP = dict(sky=(-12.0, -2.0), skyline=(-2.0, 0.0), far=(1.0, 3.0), mid=(5.0, 12.0), near=(25.0, 50.0))
MIN_BAND_PX = 2000                   # half-res pixels a band needs in a frame for the per-frame ratio
SPARKLE_DEV, SPARKLE_R = 45.0, 3     # N2: chroma departure (levels) from its (2r+1)^2 mean
GRAIN_DEG, GRAIN_R, GRAIN_EDGE = 3.0, 2, 8   # N1: sky >= 3 deg above the skyline, luma minus 5x5 mean
GRAIN_MIN_PX = 5000
BLOB_DEG, BLOB_CELL, BLOB_DEV, BLOB_R = 6.0, 8, 8.0, 30   # sky-blob search (drops), native px
BLOB_MIN_SKY, BLOB_CELL_SHARE, BLOB_CELL_SKY, BLOB_MIN_CELLS, BLOB_ASPECT = 20000, 0.4, 32, 4, 3.0
TEX_R = (1, 6)                       # texture: 3x3 minus 13x13 box mean
BLOCK = 16                           # half-res blocks for the edge bands
EDGE_BANDS = dict(left=(96, 160, 0, 176), right=(96, 160, 784, 960))   # half-res rows, cols
BAND_STEP = 8.0                      # levels of brightening that count
ROD_BOX = (1560, 1900, 520, 880)     # native cols, rows searched for the right rod's upper edge
ROD_STEP, ROD_BRIGHT, ROD_MIN_COLS, ROD_MIN_RISE = 8.0, 120.0, 50, 40.0
PRE, PRE2 = 30, 60                   # 1 s and 2 s before an outage onset (up to its MARGIN)
DRAWS = 2000                         # random 1 s windows for the pre-onset grain check
HEIGHTS = (20.0, 50.0)               # m above ground, assumed (wind.md 6.2)
DROP_MM, DROP_M = 2.0, 0.5           # R2: a drop this big this far from the lens
SOURCE_SAMPLES = 450                 # real samples per line (video-feed.md 5)
KOSCHMIEDER = 3.912                  # visibility = 3.912 / extinction (2 % contrast)
MASS_KG, RHO, G, DISC_IN = 4.8, 1.225, 9.81, 10.0   # R8: nominal mass during P (wind.md 5.5), 10" props

# track_attitude.py's constants (godot-final), the ones this tool uses
K1 = 0.33                        # video-feed.md O1 (0.30-0.34)
FOCAL = 1.0 / math.sqrt(3 * K1)  # half-widths; equidistant lens matched to K1 (assumption)
WORK_PCT = 50                    # decode at 960x540: the feed carries ~450 x 286 real samples
BORDER_TOP, BORDER_BOTTOM = 3, 2  # working-res rows of receiver border never searched (P8)
STEP_WIN = 16                    # rows above a candidate edge (32 px at 1080p, ~8 field lines)
BELOW_WIN = 40                   # rows below it: the ground side must stay darker for longer
MIN_ABOVE, MIN_BELOW = 3, 12     # fewest unmasked rows each side (the frame edges shorten windows)
STEP_MIN = 8.0                   # weakest per-column step (feature levels) that may vote
CONTRAST_LO, CONTRAST_HI = 15.0, 50.0  # median inlier step mapped to 0..1 (real horizons: 70+)
RANSAC_ITERS = 400
RANSAC_TOL = 0.012               # half-widths (~6 px at 540p): inlier distance to the line
MIN_INLIER_COLS = 0.08           # of the frame width; fewer inliers -> no line
SPAN_ROLL = (0.2, 0.5)           # inlier span (of the width) mapped to 0..1 roll confidence
SPAN_PITCH = (0.1, 0.3)          # pitch needs less span than roll
MASK_FRAMES = 120                # frames sampled for the static (camera-fixed) mask
MASK_SHARP = 4.0                 # |mean image - its 9x9 mean| (levels) that marks camera-fixed detail
MASK_DENSE = 0.3                 # share of a 5x5 box that must be sharp (drops thin specks)
MASK_GROW = 3                    # pixels the static mask is dilated by
CONF = 0.5                       # track_attitude.CONF_OK: "high confidence" used by the statistics
LUMA = (0.299, 0.587, 0.114)


# ---------------------------------------------------------------- images: (w, h, channels as flat arrays)

class Img:
    """An image of w x h pixels, top row first, as flat float arrays: one per channel (r, g, b) or one (a)."""

    def __init__(self, w, h, *ch):
        self.w, self.h, self.ch = w, h, ch


def box_sum_rows(a, w, h, r):
    k = 2 * r + 1
    out = array("d")
    for y in range(h):
        row = a[y * w:(y + 1) * w]
        c = list(accumulate(chain(repeat(row[0], r + 1), row, repeat(row[-1], r))))
        out.extend(map(sub, c[k:], c[:-k]))
    return out


def transpose(a, w, h):
    t = array("d")
    for x in range(w):
        t.extend(a[x::w])
    return t


def box_mean(a, w, h, r):
    """Mean over a (2r+1)^2 box, edges padded by repetition (track_attitude.box_mean)."""
    s = transpose(box_sum_rows(transpose(box_sum_rows(a, w, h, r), w, h), h, w, r), h, w)
    inv = 1.0 / (2 * r + 1) ** 2
    return array("d", [v * inv for v in s])


def box_sum(a, w, h, r):
    return transpose(box_sum_rows(transpose(box_sum_rows(a, w, h, r), w, h), h, w, r), h, w)


def half(img):
    """2x2 area average."""
    w, h = img.w // 2, img.h // 2
    out = []
    for c in img.ch:
        o = array("d")
        for y in range(h):
            a, b = c[2 * y * img.w:(2 * y + 1) * img.w], c[(2 * y + 1) * img.w:(2 * y + 2) * img.w]
            s = [u + v for u, v in zip(a, b)]
            o.extend([(u + v) * 0.25 for u, v in zip(s[0::2], s[1::2])])
        out.append(o)
    return Img(w, h, *out)


def ycc(img):
    r, g, b = img.ch
    y = array("d", [LUMA[0] * u + LUMA[1] * v + LUMA[2] * q for u, v, q in zip(r, g, b)])
    return y, array("d", [0.564 * (q - v) for q, v in zip(b, y)]), array("d", [0.713 * (u - v) for u, v in zip(r, y)])


def luma(img):
    r, g, b = img.ch
    return array("d", [LUMA[0] * u + LUMA[1] * v + LUMA[2] * q for u, v, q in zip(r, g, b)])


def undistort(xd, yd, k1):
    s = 1.0 + k1 * (xd * xd + yd * yd)
    return xd * s, yd * s


def pixel_rays(h, w):
    """Unit camera rays (x right, y down, z forward) of every pixel centre after undistortion, as three arrays."""
    xs, ys, zs = array("d"), array("d"), array("d")
    for row in range(h):
        yd = (row + 0.5 - h / 2) / (w / 2)
        for col in range(w):
            xu, yu = undistort((col + 0.5 - w / 2) / (w / 2), yd, K1)
            n = math.sqrt(xu * xu + yu * yu + FOCAL * FOCAL)
            xs.append(xu / n)
            ys.append(yu / n)
            zs.append(FOCAL / n)
    return xs, ys, zs


def depression(roll, pitch, rays):
    """Depression of every pixel below the horizon (deg; negative = sky) for a camera at roll/pitch.
    The inverse of track_attitude.level_to_camera."""
    xc, yc, zc = rays
    cr, sr = math.cos(math.radians(roll)), math.sin(math.radians(roll))
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    d = math.degrees
    return array("d", [d(math.asin(max(-1.0, min(1.0, (x * sr + y * cr) * cp - z * sp)))) for x, y, z in zip(xc, yc, zc)])


def is_picture(img):
    """False for the receiver's blue no-signal screen and for black frames (track_attitude.is_picture)."""
    r, g, b = img.ch
    n = len(r)
    blue = math.fsum(b) / n > 200 and math.fsum(r) / n < 30
    return not blue and math.fsum(luma(img)) / n > 12


def static_mask(frames, w, h):
    """Camera-fixed pixels (OSD, airframe parts, receiver border) of half-res frames: what stays sharp in the mean
    of many frames, thin specks dropped, blobs grown by MASK_GROW (track_attitude.static_mask). A bytearray of 0/1."""
    lum = [luma(f) for f in frames]
    m = array("d", [math.fsum(col) / len(lum) for col in zip(*lum)])
    sharp = array("d", [1.0 if abs(u - v) > MASK_SHARP else 0.0 for u, v in zip(m, box_mean(m, w, h, 4))])
    dense = array("d", [1.0 if v > MASK_DENSE else 0.0 for v in box_mean(sharp, w, h, 2)])
    return bytearray(1 if v > 0 else 0 for v in box_mean(dense, w, h, MASK_GROW))


def upsample(mask, w, h):
    """A half-res mask at native size (each pixel to 2 x 2)."""
    out = bytearray()
    for y in range(h):
        row = mask[y * w:(y + 1) * w]
        line = bytes(chain.from_iterable(zip(row, row)))
        out += line + line
    return out


# ---------------------------------------------------------------- the horizon (track_attitude.py)

def feature(img):
    """Sky-likeness: luma minus colourfulness. Grey sky stays high, brown soil and yellow straw drop."""
    r, g, b = img.ch
    return array("d", [LUMA[0] * u + LUMA[1] * v + LUMA[2] * q - abs(u - q) for u, v, q in zip(r, g, b)])


def column_edges(feat, mask, w, h):
    """Per column, the strongest step from brighter above (STEP_WIN rows) to darker below (BELOW_WIN rows).
    Camera-fixed pixels take no part in either mean. Returns (cols, rows, steps) for the columns whose step
    reaches STEP_MIN; rows are edge positions in pixels from the top (sub-pixel)."""
    top, bot = BORDER_TOP, h - BORDER_BOTTOM
    rs = range(top + MIN_ABOVE, bot - MIN_BELOW + 1)
    cols, rows, steps = [], [], []
    for x in range(w):
        f, m = feat[x::w], mask[x::w]
        cs = [0.0] + list(accumulate(0.0 if mi else fi for fi, mi in zip(f, m)))
        cn = [0] + list(accumulate(0 if mi else 1 for mi in m))
        s = []
        for r in rs:
            lo, hi = max(top, r - STEP_WIN), min(bot, r + BELOW_WIN)
            na, nb = cn[r] - cn[lo], cn[hi] - cn[r]
            if na < MIN_ABOVE or nb < MIN_BELOW:
                s.append(-math.inf)
            else:
                s.append((cs[r] - cs[lo]) / max(na, 1) - (cs[hi] - cs[r]) / max(nb, 1))
        k = max(range(len(s)), key=s.__getitem__)
        best = s[k]
        if not best >= STEP_MIN:
            continue
        a, b = s[max(k - 1, 0)], s[min(k + 1, len(s) - 1)]
        den = a - 2 * best + b
        off = 0.5 * (a - b) / den if math.isfinite(den) and den < 0 else 0.0
        cols.append(x)
        rows.append(rs[k] + max(-0.5, min(0.5, off)))
        steps.append(best)
    return cols, rows, steps


def principal(x, y):
    """Mean point and unit principal direction (dx >= 0) of points: the SVD's first right vector."""
    n = len(x)
    px, py = math.fsum(x) / n, math.fsum(y) / n
    sxx = math.fsum((a - px) ** 2 for a in x)
    syy = math.fsum((b - py) ** 2 for b in y)
    sxy = math.fsum((a - px) * (b - py) for a, b in zip(x, y))
    t = 0.5 * math.atan2(2 * sxy, sxx - syy)
    d = (math.cos(t), math.sin(t))
    return (px, py), (d if d[0] >= 0 else (-d[0], -d[1]))


def fit_line(x, y, rng):
    """RANSAC, then total least squares on the inliers (twice). Returns (point, unit direction with dx >= 0,
    inlier flags) or None."""
    n = len(x)
    if n < 2:
        return None
    ii = [rng.randrange(n) for _ in range(RANSAC_ITERS)]
    jj = [rng.randrange(n) for _ in range(RANSAC_ITERS)]
    best, inl = -1, None
    for i, j in zip(ii, jj):
        dx, dy = x[j] - x[i], y[j] - y[i]
        ln = math.hypot(dx, dy)
        if not ln > 0.05:
            continue
        nx, ny, x0, y0 = -dy / ln, dx / ln, x[i], y[i]
        cand = [abs(nx * (a - x0) + ny * (b - y0)) < RANSAC_TOL for a, b in zip(x, y)]
        c = sum(cand)
        if c > best:
            best, inl = c, cand
    if inl is None:
        return None
    for _ in range(2):
        if sum(inl) < 2:
            return None
        p, d = principal(list(compress(x, inl)), list(compress(y, inl)))
        inl = [abs(-d[1] * (a - p[0]) + d[0] * (b - p[1])) < RANSAC_TOL for a, b in zip(x, y)]
    return p, d, inl


def ramp(v, lo, hi):
    return max(0.0, min(1.0, (v - lo) / (hi - lo)))


def no_line(flag):
    return dict(roll=NAN, pitch=NAN, conf_roll=0.0, conf_pitch=0.0, n_cols=0,
                inlier_frac=0.0, contrast=0.0, span=0.0, resid_px=NAN, edge_px=NAN, flag=flag)


def median(v):
    return percentile(v, 50)


def horizon(img, mask, k1, focal, rng):
    """Fit the horizon of one working-res frame. Returns a dict with roll, pitch (deg, NaN if no line), their
    confidences and diagnostics (track_attitude.horizon)."""
    w, h = img.w, img.h
    out = no_line("no_line")
    feat = feature(img)
    cols, rows, steps = column_edges(feat, mask, w, h)
    out["n_cols"] = len(cols)
    if len(cols) < MIN_INLIER_COLS * w:
        return out
    pts = [undistort((c + 0.5 - w / 2) / (w / 2), (r - h / 2) / (w / 2), k1) for c, r in zip(cols, rows)]
    xu, yu = [p[0] for p in pts], [p[1] for p in pts]
    fit = fit_line(xu, yu, rng)
    if fit is None:
        return out
    p, d, inl = fit
    if sum(inl) < MIN_INLIER_COLS * w:
        return out
    nrm = (-d[1], d[0])                            # points down the image (d[0] >= 0)
    dist = nrm[0] * p[0] + nrm[1] * p[1]           # signed distance of the line below the centre
    xi, yi = list(compress(xu, inl)), list(compress(yu, inl))
    resid = [abs(nrm[0] * (a - p[0]) + nrm[1] * (b - p[1])) for a, b in zip(xi, yi)]
    si, ci, ri = list(compress(steps, inl)), list(compress(cols, inl)), list(compress(rows, inl))
    frac = len(si) / len(cols)
    contrast = median(si)
    span = (max(ci) - min(ci) + 1) / w
    q = frac * ramp(contrast, CONTRAST_LO, CONTRAST_HI)
    # Edge width: step / steepest one-row drop near the edge ~ 10-90 % rise (motion blur, focus, lines).
    clip = lambda r: max(0, min(h - 1, r))
    ratios = []
    for st, c0, r in zip(si, ci, ri):
        r0 = int(round(r))
        drop = max(feat[clip(r0 + k) * w + c0] - feat[clip(r0 + k + 1) * w + c0] for k in range(-3, 3))
        ratios.append(st / max(drop, 1e-6))
    out.update(roll=math.degrees(math.atan2(-d[1], d[0])), pitch=math.degrees(math.atan(dist / focal)),
               conf_roll=q * ramp(span, *SPAN_ROLL), conf_pitch=q * ramp(span, *SPAN_PITCH),
               inlier_frac=frac, contrast=contrast, span=span,
               resid_px=math.sqrt(math.fsum(v * v for v in resid) / len(resid)) * 960.0,
               edge_px=median(ratios) * 100 / WORK_PCT, flag="ok")    # native px
    return out


# ---------------------------------------------------------------- per-frame measurement

def sky_blobs(y, sky, w, h):
    """Compact blobs in the sky, the way a drop on the lens would look: luma departing > BLOB_DEV from the sky-only
    local mean, grouped on BLOB_CELL cells (a cell counts when more than BLOB_CELL_SHARE of its sky pixels depart);
    connected groups of >= BLOB_MIN_CELLS cells whose bounding box is at most BLOB_ASPECT times longer than wide.
    Returns their count, or NaN with too little sky."""
    if sum(sky) < BLOB_MIN_SKY:
        return NAN
    s = array("d", iter(sky))            # iter: a bytearray initialiser would be read as raw bytes
    num = box_sum(array("d", [a * b for a, b in zip(y, s)]), w, h, BLOB_R)
    den = box_sum(s, w, h, BLOB_R)
    hit = [k and abs(v - n / max(d, 1)) > BLOB_DEV for k, v, n, d in zip(sky, y, num, den)]
    c = BLOB_CELL
    H, W = h // c, w // c
    hc, sc = [0] * (H * W), [0] * (H * W)
    for row in range(H * c):
        base, cr = row * w, (row // c) * W
        for q in range(W):
            seg = slice(base + q * c, base + q * c + c)
            hc[cr + q] += sum(hit[seg])
            sc[cr + q] += sum(sky[seg])
    grid = [a > BLOB_CELL_SHARE * max(b, 1) and b > BLOB_CELL_SKY for a, b in zip(hc, sc)]
    seen, count = [False] * (H * W), 0
    for start in range(H * W):
        if not grid[start] or seen[start]:
            continue
        stack, cells = [start], []
        seen[start] = True
        while stack:
            i = stack.pop()
            cells.append(i)
            r, q = divmod(i, W)
            for rr, qq in ((r + 1, q), (r - 1, q), (r, q + 1), (r, q - 1)):
                j = rr * W + qq
                if 0 <= rr < H and 0 <= qq < W and grid[j] and not seen[j]:
                    seen[j] = True
                    stack.append(j)
        if len(cells) >= BLOB_MIN_CELLS:
            rs, qs = [i // W for i in cells], [i % W for i in cells]
            bh, bw = max(rs) - min(rs) + 1, max(qs) - min(qs) + 1
            count += max(bh, bw) / min(bh, bw) <= BLOB_ASPECT
    return count


def polyfit2(x, y):
    """Least-squares quadratic (np.polyfit(x, y, 2)), returned as a function; x is centred and scaled first."""
    xm = math.fsum(x) / len(x)
    sc = max(abs(v - xm) for v in x) or 1.0
    u = [(v - xm) / sc for v in x]
    s = [math.fsum(a ** k for a in u) for k in range(5)]
    t = [math.fsum(b * a ** k for a, b in zip(u, y)) for k in range(3)]
    m = [[s[0], s[1], s[2], t[0]], [s[1], s[2], s[3], t[1]], [s[2], s[3], s[4], t[2]]]
    for i in range(3):                               # Gauss-Jordan with partial pivoting
        p = max(range(i, 3), key=lambda r: abs(m[r][i]))
        m[i], m[p] = m[p], m[i]
        for r in range(3):
            if r != i:
                f = m[r][i] / m[i][i]
                m[r] = [a - f * b for a, b in zip(m[r], m[i])]
    c = [m[i][3] / m[i][i] for i in range(3)]
    return lambda v: c[0] + c[1] * ((v - xm) / sc) + c[2] * ((v - xm) / sc) ** 2


def interp1(x, xp, fp):
    """np.interp on two points (the original's crossing)."""
    if xp[1] == xp[0]:
        return fp[0]
    t = (x - xp[0]) / (xp[1] - xp[0])
    return fp[0] if t <= 0 else fp[1] if t >= 1 else fp[0] + t * (fp[1] - fp[0])


def rod_edge(y, w, h):
    """10-90 % width (native px) of the right airframe rod's upper edge in a native luma image, and the undershoot on
    its dark side as a share of the edge step. The rod is camera-fixed, so the width is the whole chain's response
    (lens, camera, link, recorder) with nothing moving in the scene. The edge lies within ~10 deg of horizontal and
    is measured vertically. None where no edge is found."""
    x0, x1, r0, r1 = ROD_BOX
    xs, rows = [], []
    for x in range(x0, x1):
        col = [y[(r + 1) * w + x] - y[r * w + x] for r in range(r0, r1)]     # positive: brighter below
        k = max(range(len(col)), key=col.__getitem__)
        below = [y[r * w + x] for r in range(r0 + k + 3, min(h, r0 + k + 8))]
        if col[k] < ROD_STEP or not below or mean(below) < ROD_BRIGHT:
            continue
        off = 0.0
        if 0 < k < len(col) - 1 and col[k - 1] - 2 * col[k] + col[k + 1] < 0:
            off = 0.5 * (col[k - 1] - col[k + 1]) / (col[k - 1] - 2 * col[k] + col[k + 1])
        xs.append(float(x))
        rows.append(r0 + k + 1.0 + off)                  # the boundary between rows k and k + 1
    if len(xs) < ROD_MIN_COLS:
        return None
    for _ in range(3):
        f = polyfit2(xs, rows)
        res = [r - f(x) for x, r in zip(xs, rows)]
        lim = max(1.5, 2.5 * std(res))
        keep = [abs(v) < lim for v in res]
        xs, rows = list(compress(xs, keep)), list(compress(rows, keep))
    f = polyfit2(xs, rows)
    bins = [-14 + 0.25 * k for k in range(113)]          # np.arange(-14, 14.01, 0.25)
    per = [[] for _ in range(len(bins) - 1)]
    for x in xs:                                         # oversampled edge-spread function
        x = int(x)
        e = f(x)
        for rr in range(int(e) - 14, int(e) + 14):
            i = bisect_right(bins, rr + 0.5 - e) - 1
            if 0 <= i < len(per) and 0 <= rr < h:
                per[i].append(y[rr * w + x])
    esf = [median(v) if len(v) > 3 else NAN for v in per]
    mid = [0.5 * (a + b) for a, b in zip(bins[1:], bins[:-1])]
    lo = median([v for v, m in zip(esf, mid) if m < -9])
    hi = median([v for v, m in zip(esf, mid) if 5 < m < 9])
    if not hi - lo >= ROD_MIN_RISE:                      # no bright rod below a dark background
        return None
    m = [a for a, v in zip(mid, esf) if math.isfinite(v)]
    e = [(v - lo) / (hi - lo) for v in esf if math.isfinite(v)]
    c = min(range(len(m)), key=lambda i: abs(m[i]))

    def cross(level):
        i = c
        if e[c] < level:
            while i < len(e) - 1 and e[i] < level:
                i += 1
            return interp1(level, (e[i - 1], e[i]), (m[i - 1], m[i]))
        while i > 0 and e[i] >= level:
            i -= 1
        return interp1(level, (e[i], e[i + 1]), (m[i], m[i + 1]))
    under = [v for v, a in zip(e, m) if -8 < a < 0]
    return cross(0.9) - cross(0.1), -min(under)


def no_bands():
    return [[NAN] * 4 for _ in DEP]


FIELDS = ("pic", "luma", "luma_std", "chroma", "red", "green", "blue", "luma_p05", "luma_p99", "black",
          "block_median", "band_left", "band_right", "roll", "pitch", "conf", "edge", "sparkle", "grain",
          "grain_std", "grain_sky", "blobs", "rod", "rod_under")


def measure(full, mask, rays_half, rays_full, rod, frame):
    """Every per-frame quantity of one native frame (NaN where it can't be measured)."""
    r = {k: NAN for k in FIELDS}
    img = half(full)
    w, h = img.w, img.h
    r["pic"] = float(is_picture(img))
    if not r["pic"]:
        return r, None
    keep = bytearray(1 - v for v in mask)
    y, cb, cr = ycc(img)
    chroma = array("d", [math.hypot(a, b) for a, b in zip(cb, cr)])
    tex = [abs(a - b) for a, b in zip(box_mean(y, w, h, TEX_R[0]), box_mean(y, w, h, TEX_R[1]))]
    yk = list(compress(y, keep))
    r.update(luma=mean(yk), luma_std=std(yk), chroma=mean(list(compress(chroma, keep))),
             black=percentile(list(compress(y, mask)), 1))
    r["red"], r["green"], r["blue"] = (mean(list(compress(c, keep))) for c in img.ch)
    r["luma_p05"], r["luma_p99"] = percentile(yk, 0.5), percentile(yk, 99)
    H, W = h // BLOCK, w // BLOCK                        # block means; a block with a masked pixel is NaN
    blocks = [NAN] * (H * W)
    for by in range(H):
        for bx in range(W):
            idx = [(by * BLOCK + j) * w + bx * BLOCK for j in range(BLOCK)]
            if not any(any(mask[i:i + BLOCK]) for i in idx):
                blocks[by * W + bx] = math.fsum(math.fsum(y[i:i + BLOCK]) for i in idx) / (BLOCK * BLOCK)
    r["block_median"] = median(blocks)
    for name, (a, b, c0, c1) in EDGE_BANDS.items():
        sel = fin([blocks[by * W + bx] for by in range(a // BLOCK, b // BLOCK) for bx in range(c0 // BLOCK, c1 // BLOCK)])
        r["band_" + name] = mean(sel) if sel else NAN
    hz = horizon(img, mask, K1, FOCAL, random.Random(frame))
    r.update(roll=hz["roll"], pitch=hz["pitch"], conf=min(hz["conf_roll"], hz["conf_pitch"]), edge=hz["edge_px"])
    fw, fh = full.w, full.h
    yf, cbf, crf = ycc(full)
    keep_f = upsample(keep, w, h)
    dev = [math.hypot(a - b, c - d) for a, b, c, d in
           zip(cbf, box_mean(cbf, fw, fh, SPARKLE_R), crf, box_mean(crf, fw, fh, SPARKLE_R))]
    r["sparkle"] = 1000.0 * sum(1 for v, k in zip(dev, keep_f) if k and v > SPARKLE_DEV) / sum(keep_f)
    bands = no_bands()                                   # a picture without a confident horizon: NaN bands
    if r["conf"] >= CONF:
        dep = depression(r["roll"], r["pitch"], rays_half)
        for i, (lo, hi) in enumerate(DEP.values()):
            s = [k and lo <= d < hi for k, d in zip(keep, dep)]
            bands[i] = [float(sum(s)), math.fsum(compress(y, s)), math.fsum(compress(chroma, s)), math.fsum(compress(tex, s))]
        dep = depression(r["roll"], r["pitch"], rays_full)
        sky = bytearray(1 if k and d <= -GRAIN_DEG else 0 for k, d in zip(keep_f, dep))
        for row in range(GRAIN_EDGE):
            sky[row * fw:(row + 1) * fw] = bytes(fw)
        for row in range(fh):
            sky[row * fw:row * fw + GRAIN_EDGE] = bytes(GRAIN_EDGE)
            sky[(row + 1) * fw - GRAIN_EDGE:(row + 1) * fw] = bytes(GRAIN_EDGE)
        if sum(sky) > GRAIN_MIN_PX:
            hp = [a - b for a, b in compress(zip(yf, box_mean(yf, fw, fh, GRAIN_R)), sky)]
            mh = median(hp)
            r["grain"] = 1.4826 * median([abs(v - mh) for v in hp])
            r["grain_std"], r["grain_sky"] = std(hp), mean(list(compress(yf, sky)))
        r["blobs"] = sky_blobs(yf, bytearray(1 if k and d <= -BLOB_DEG else 0 for k, d in zip(keep_f, dep)), fw, fh)
    if rod:
        e = rod_edge(yf, fw, fh)
        if e:
            r["rod"], r["rod_under"] = e
    return r, bands


# ---------------------------------------------------------------- decoding and scanning a clip

class Decoder:
    """Every frame of a clip in decode order, as native 8-bit RGB, through ffmpeg (replaces the Blender sequencer)."""

    def __init__(self, path):
        ffprobe, self.ffmpeg = shutil.which("ffprobe"), shutil.which("ffmpeg")
        if not (ffprobe and self.ffmpeg):
            raise RuntimeError("ffmpeg and ffprobe must be on PATH (docs/environment.md)")
        run = subprocess.run([ffprobe, "-v", "error", "-select_streams", "v:0", "-count_packets", "-show_entries",
                              "stream=width,height,avg_frame_rate,nb_read_packets", "-of", "json", str(path)],
                             capture_output=True, text=True)
        if run.returncode:
            raise RuntimeError("ffprobe could not read the clip")     # its log names the clip: never shown
        s = json.loads(run.stdout)["streams"][0]
        num, den = (int(v) for v in s["avg_frame_rate"].split("/"))
        self.path, self.size, self.fps, self.n = path, (int(s["width"]), int(s["height"])), num / den, int(s["nb_read_packets"])
        if not self.fps or self.n <= 0:
            raise RuntimeError(f"could not decode the clip (fps={self.fps}, frames={self.n})")

    def frames(self):
        """Yields (frame number from 1, Img) for every decoded frame."""
        w, h = self.size
        p = subprocess.Popen([self.ffmpeg, "-v", "error", "-nostdin", "-i", str(self.path), "-fps_mode", "passthrough",
                              "-f", "rawvideo", "-pix_fmt", "rgb24", "-"], stdout=subprocess.PIPE,
                             stderr=subprocess.DEVNULL)
        try:
            k, size = 0, w * h * 3
            while True:
                buf = p.stdout.read(size)
                if len(buf) < size:
                    break
                k += 1
                yield k, Img(w, h, array("d", iter(buf[0::3])), array("d", iter(buf[1::3])), array("d", iter(buf[2::3])))
        finally:
            p.stdout.close()
            p.kill()
            p.wait()


LETTER_MAPS = ("letters.md", "index.md")   # the restart's letter map first, then the first attempt's index


def clip_path(ref, letter):
    """The clip file for a letter, from the letter map under reference/_frames/ (never printed), checked to lie
    under reference/."""
    for name in LETTER_MAPS:
        path = ref / "_frames" / name
        if not path.exists():
            continue
        for line in path.read_text(encoding="utf-8").splitlines():
            cells = [c.strip() for c in line.strip().strip("|").split("|")]
            if len(cells) == 3 and cells[0] == letter and cells[1] == "clip":
                return ref_paths.clip_path(ref.parent, ref / cells[2].strip("`"))
    raise RuntimeError(f"letter {letter} is not a clip in the letter map")


def outage_mask(frames):
    return [any(a - MARGIN <= f <= b + MARGIN for a, b in OUTAGES) for f in frames]


def scan(ref, letter):
    """Decode and measure one clip. Returns a dict of per-frame lists plus the clip-level values."""
    t0 = time.time()
    dec = Decoder(clip_path(ref, letter))
    n = dec.n
    last = FIRST_LOSS[letter] - LOSS_GUARD - 1 if letter in FIRST_LOSS else n
    m = min(n, MASK_FRAMES)
    picks = sorted({int(round(1 + i * (n - 1) / (m - 1))) for i in range(m)}) if m > 1 else [1]
    sample = []
    for f, full in dec.frames():                         # pass 1: the camera-fixed mask
        if f in picks:
            img = half(full)
            if is_picture(img):
                sample.append(img)
    w, h = dec.size[0] // 2, dec.size[1] // 2
    mask = static_mask(sample, w, h)
    rod = letter in (RAIN, ROD)
    rays_half, rays_full = pixel_rays(h, w), pixel_rays(h * 2, w * 2)
    frames = list(range(1, last + 1))
    window = [not v for v in outage_mask(frames)] if letter == RAIN else [True] * len(frames)
    rows, bands, mean_y, n_mean = [], [], None, 0
    for f, full in dec.frames():                         # pass 2: every frame of the window
        if f > last:
            break
        r, b = measure(full, mask, rays_half, rays_full, rod, f)
        rows.append(r)
        bands.append(b if b is not None else no_bands())  # no picture: NaN bands, as the original's array
        if b is not None and rod and window[f - 1]:
            y = luma(full)
            mean_y = y if mean_y is None else array("d", [u + v for u, v in zip(mean_y, y)])
            n_mean += 1
        if f % 100 == 0:
            print(f"rain_stats: {letter} {f}/{last} frames, {time.time() - t0:.0f} s", flush=True)
    s = {k: [r[k] for r in rows] for k in FIELDS}
    rm = rod_edge(array("d", [v / n_mean for v in mean_y]), w * 2, h * 2) if n_mean else None
    s.update(frames=frames[:len(rows)], bands=bands, mask_share=sum(mask) / len(mask), fps=dec.fps, n=n,
             version=VERSION, seconds=time.time() - t0, rod_mean=list(rm) if rm else [NAN, NAN])
    return s


def load_or_scan(ref, letter, folder, reuse):
    path = folder / f"{letter}.json"
    if reuse and path.exists():
        s = json.loads(path.read_text(encoding="utf-8"))
        if s.get("version") == VERSION:
            return unnull(s), True
    s = scan(ref, letter)
    path.write_text(json.dumps(finite(s), allow_nan=False), encoding="utf-8")
    return s, False


def unnull(o):
    """The per-frame file's null back to NaN."""
    if o is None:
        return NAN
    if isinstance(o, dict):
        return {k: unnull(v) for k, v in o.items()}
    if isinstance(o, list):
        return [unnull(v) for v in o]
    return o


# ---------------------------------------------------------------- statistics

def q(a, p):
    a = fin(a)
    return [percentile(a, v) for v in p] if a else [NAN] * len(p)


def med(a):
    return q(a, [50])[0]


def top(a):
    a = fin(a)
    return max(a) if a else NAN


def span(values):
    v = fin(values)
    return [min(v), max(v)] if v else [NAN, NAN]


def pick(a, ok):
    return [v for v, k in zip(a, ok) if k]


def window_of(letter, s):
    ok = [p > 0 for p in s["pic"]]
    return [k and not o for k, o in zip(ok, outage_mask(s["frames"]))] if letter == RAIN else ok


def frame_stats(s, ok):
    y = pick(s["luma"], ok)
    veil = [a - b for a, b in zip(pick(s["luma_p05"], ok), pick(s["black"], ok))]
    return dict(frames=sum(ok), luma_q=q(y, [10, 50, 90]), contrast=med([a / b for a, b in zip(pick(s["luma_std"], ok), y)]),
                chroma=med(pick(s["chroma"], ok)), r_g=med([a / b for a, b in zip(pick(s["red"], ok), pick(s["green"], ok))]),
                b_g=med([a / b for a, b in zip(pick(s["blue"], ok), pick(s["green"], ok))]), veil_q=q(veil, [10, 50, 90]),
                veil_share=med([a / b for a, b in zip(veil, pick(s["luma_p99"], ok))]), black=med(pick(s["black"], ok)),
                sparkle_q=q(pick(s["sparkle"], ok), [50, 90]))


def profile(s, ok):
    """Horizon-relative bands over the frames with a horizon."""
    ok = [k and c >= CONF for k, c in zip(ok, s["conf"])]
    b = [x for x, k in zip(s["bands"], ok) if k and x is not None]   # frames x bands x (px, luma, chroma, texture)
    names = list(DEP)
    tot = [[math.fsum(v for v in (fr[i][j] for fr in b) if math.isfinite(v)) for j in range(4)] for i in range(len(names))]
    band = {k: dict(luma=tot[i][1] / tot[i][0], chroma=tot[i][2] / tot[i][0], texture_rel=tot[i][3] / tot[i][1],
                    px=tot[i][0]) for i, k in enumerate(names)}
    sky = band["sky"]["luma"]
    for k in band:
        band[k]["luma_rel_sky"] = band[k]["luma"] / sky
    i_sky, i_far, i_near = names.index("sky"), names.index("far"), names.index("near")
    good = [fr for fr in b if all(fr[i][0] > MIN_BAND_PX for i in (i_sky, i_far, i_near))]
    ratio = [(fr[i_far][1] / fr[i_far][0]) / (fr[i_near][1] / fr[i_near][0]) for fr in good]
    return dict(frames=sum(ok), bands=band, far_near_q=q(ratio, [10, 50, 90]), far_near_frames=len(good),
                far_near_chroma=band["far"]["chroma"] / band["near"]["chroma"],
                far_near_texture=band["far"]["texture_rel"] / band["near"]["texture_rel"])


def edge_band(s, ok, side):
    """Frames where a band brightens by > BAND_STEP against its neighbours after the frame-wide level change is
    removed, as a share of the frames whose neighbours are both in the set."""
    m, a, fr = s["band_" + side], s["block_median"], s["frames"]
    hits, tested = [], 0
    for i in range(1, len(m) - 1):
        d = (m[i] - 0.5 * (m[i - 1] + m[i + 1])) - (a[i] - 0.5 * (a[i - 1] + a[i + 1]))
        if ok[i] and ok[i - 1] and ok[i + 1] and math.isfinite(d):
            tested += 1
            if d > BAND_STEP:
                hits.append(fr[i])
    gaps = [b - a for a, b in zip(hits, hits[1:])]
    return dict(tested=tested, frames=len(hits), share=len(hits) / max(tested, 1),
                gaps_2=sum(1 for g in gaps if g == 2), gaps=len(gaps), first=[int(f) for f in hits[:12]])


def rod_stats(s, ok):
    burst_starts = (1, 1 + (s["n"] - BURST) // 2, s["n"] - BURST + 1)     # extract_frames.py's bursts
    by = dict(zip(s["frames"], zip(s["rod"], s["rod_under"])))
    bursts = []
    for b0 in burst_starts:
        fr = [f for f in range(int(b0), int(b0) + BURST) if f in by]
        if len(fr) == BURST:
            w = [by[f][0] for f in fr]
            u = [by[f][1] for f in fr]
            bursts.append(dict(frames=[fr[0], fr[-1]], found=len(fin(w)), width=span(w), under=span(u)))
    w = pick(s["rod"], ok)
    return dict(mean_image=dict(width=s["rod_mean"][0], under=s["rod_mean"][1]),
                frames=len(fin(w)), of=sum(ok), width_q=q(w, [10, 50, 90]),
                under_q=q(pick(s["rod_under"], ok), [10, 50, 90]), bursts=bursts)


def outages(s, ok):
    """Sparkles, grain and the skyline edge around each P outage."""
    fr = s["frames"]
    at = {int(f): i for i, f in enumerate(fr)}

    def vals(key, frames, sel=None):
        """Values over the picture frames among `frames` (any frame, outages included)."""
        return [s[key][at[f]] for f in frames if f in at and s["pic"][at[f]] > 0 and (sel is None or sel[at[f]])]
    good = [c >= CONF for c in s["conf"]]
    out, pre1 = [], []
    for a, b in OUTAGES:
        before, after = range(a - PRE, a - MARGIN), range(b + MARGIN + 1, b + PRE + 1)
        g1 = vals("grain", before)
        pre1.append(g1)
        e_pre, e_post = vals("edge", before, good), vals("edge", after, good)
        inside = [f for f in range(a, b + 1) if f in at and s["pic"][at[f]] > 0]
        out.append(dict(onset=a, end=b, sparkle_pre_max=top(vals("sparkle", before)),
                        grain_pre2=med(vals("grain", range(a - PRE2, a - MARGIN))), grain_pre1=med(g1),
                        edge_pre=mean(fin(e_pre)), edge_post=mean(fin(e_post)), edge_max=top(e_pre + e_post),
                        sparkle_inside={int(f): s["sparkle"][at[f]] for f in inside}))
    pooled = med([v for g in pre1 for v in g])
    last_ok = max(f for f, k in zip(fr, ok) if k)
    starts = [f for f, k in zip(fr, ok) if k and f + PRE - MARGIN <= last_ok]
    rng, hits = random.Random(0), 0
    grain_ok = [g if k else NAN for g, k in zip(s["grain"], ok)]
    for _ in range(DRAWS):
        v = [x for f in (rng.choice(starts) for _ in OUTAGES) for x in grain_ok[at[f]:at[f] + PRE - MARGIN]]
        hits += med(v) >= pooled
    return dict(each=out, grain_pre1_pooled=pooled, random_share=hits / DRAWS)


def analyse(sc):
    P = sc[RAIN]
    okP = window_of(RAIN, P)
    fps, n = float(P["fps"]), int(P["n"])
    o = dict(clips={L: dict(scanned=len(s["frames"]), window=sum(window_of(L, s)),
                            horizon=sum(k and c >= CONF for k, c in zip(window_of(L, s), s["conf"])),
                            mask_share=float(s["mask_share"])) for L, s in sc.items()})
    stills = [f for f in (1 + round(k * STILL_S * fps) for k in range(n)) if f <= n]
    o["stills"] = dict(count=len(stills), first=stills[0], last=stills[-1], share_bound_95=1 - 0.05 ** (1 / len(stills)))
    o["frame"] = {L: frame_stats(s, window_of(L, s)) for L, s in sc.items()}
    o["profile"] = {L: profile(sc[L], window_of(L, sc[L])) for L in (RAIN, "A")}
    tracked = [p > 0 and c >= CONF for p, c in zip(P["pic"], P["conf"])]
    o["edge"] = {}
    for L in (RAIN, "A"):
        sel = [k and c >= CONF for k, c in zip(window_of(L, sc[L]), sc[L]["conf"])]
        e = pick(sc[L]["edge"], sel)
        o["edge"][L] = dict(frames=sum(sel), q=q(e, [5, 50, 95]), std=std(fin(e)))
    o["edge"][RAIN].update(tracked=sum(tracked), tracked_max=top(pick(P["edge"], tracked)))
    belt = [BELT[0] <= f <= BELT[1] for f in P["frames"]]
    g = pick(P["grain"], okP)
    o["grain"] = dict(frames=len(fin(g)), q=q(g, [10, 50, 90]), std=med(pick(P["grain_std"], okP)),
                      sky=med(pick(P["grain_sky"], okP)), belt=med(pick(P["grain"], [a and b for a, b in zip(okP, belt)])),
                      field=med(pick(P["grain"], [a and not b for a, b in zip(okP, belt)])))
    o["grain"]["rel_sky"] = o["grain"]["q"][1] / o["grain"]["sky"]
    sp = [v if k else NAN for v, k in zip(P["sparkle"], okP)]
    busiest = max((i for i in range(len(sp)) if math.isfinite(sp[i])), key=sp.__getitem__)
    o["sparkle"] = dict(P=dict(median=med(sp), busiest=int(P["frames"][busiest]), max=top(sp),
                               belt=med(pick(sp, belt)), field=med(pick(sp, [not b for b in belt]))),
                        A=dict(median=o["frame"]["A"]["sparkle_q"][0]))
    o["blobs"] = {}
    for L in (RAIN, "A"):
        b = fin(pick(sc[L]["blobs"], window_of(L, sc[L])))   # NaN = too little sky, not "no blob"
        o["blobs"][L] = dict(tested=len(b), share=sum(1 for v in b if v > 0) / len(b) if b else NAN)
    o["outages"] = outages(P, okP)
    o["edge_bands"] = {L: {side: edge_band(sc[L], window_of(L, sc[L]), side) for side in EDGE_BANDS}
                       for L in (RAIN, "C", "D", "E")}
    o["rod"] = {L: rod_stats(sc[L], window_of(L, sc[L])) for L in (RAIN, ROD)}
    o["derived"] = derived(o)
    return o


def tracker_fit(ref, s):
    """This tool's horizon fit for P (on its 2x2 copy) against the tracker's attitude.csv: the frames each trusts,
    and the largest differences where both do."""
    path = ref / "_frames" / RAIN / "attitude.csv"
    if not path.exists():
        return None
    rows = {int(r["frame"]): r for r in csv.DictReader(path.open(encoding="utf-8"))}
    col = lambda k: [float(rows[int(f)][k] or "nan") if int(f) in rows else NAN for f in s["frames"]]
    conf = [min(a, b) for a, b in zip(col("conf_roll"), col("conf_pitch"))]
    both = [a >= CONF and b >= CONF for a, b in zip(s["conf"], conf)]
    diff = {k: [abs(a - b) for a, b, m in zip(s[k], col(c), both) if m]
            for k, c in (("roll", "roll_deg"), ("pitch", "pitch_deg"), ("edge", "edge_px"))}
    return dict(tool=sum(c >= CONF for c in s["conf"]), tracker=sum(c >= CONF for c in conf), both=sum(both),
                p99={k: percentile(d, 99) for k, d in diff.items()}, max={k: top(d) for k, d in diff.items()})


def derived(o):
    """Numbers computed from the measured ones with the stated models (wind.md section 7)."""
    pr = o["profile"][RAIN]["bands"]
    sky_line, far, near = pr["skyline"]["luma"], pr["far"]["luma"], pr["near"]["luma"]
    t = (sky_line - far) / (sky_line - 0.5 * near)       # Koschmieder, far terrain >= half the near's brightness
    mid_far = 0.5 * sum(DEP["far"])
    dist_km = [hh / math.tan(math.radians(mid_far)) / 1000 for hh in HEIGHTS]
    ext = [-math.log(t) / d for d in dist_km]
    ranges = {k: [HEIGHTS[0] / math.tan(math.radians(DEP[k][1])), HEIGHTS[1] / math.tan(math.radians(DEP[k][0]))]
              for k in ("far", "mid", "near")}
    ep, ea = o["edge"][RAIN]["q"][1], o["edge"]["A"]["q"][1]
    wet = math.sqrt(max(ep ** 2 - ea ** 2, 0))
    sigma = wet / (2 * 1.2816)                           # 10-90 % of a Gaussian is 2.563 sigma
    return dict(distance_m=ranges, koschmieder=dict(skyline=sky_line, far=far, near=near, t_min=t, at_km=dist_km,
                                                    extinction_per_km=[min(ext), max(ext)], visibility_km=KOSCHMIEDER / max(ext)),
                wet_blur=dict(width_px=wet, sigma_px=sigma, of_sample=sigma / source_sample_px()),
                drop=drop(), prop_wash=prop_wash())


def source_sample_px():
    return 2 * 960 / SOURCE_SAMPLES                      # native px per source sample


def drop():
    """R2: the angle and centre size (native px) of a DROP_MM drop DROP_M from the lens."""
    deg = 2 * math.degrees(math.atan(DROP_MM / 2000 / DROP_M))
    return dict(deg=deg, px=math.radians(deg) * FOCAL * 960, sample_px=source_sample_px())


def prop_wash():
    """R8: the rotors' induced velocity in hover at MASS_KG, by momentum theory, v = sqrt(T / (2 rho A))."""
    disc = math.pi * (DISC_IN * 0.0254 / 2) ** 2
    thrust = MASS_KG * G / 4
    return dict(mass_kg=MASS_KG, disc_m2=disc, thrust_n=thrust, induced_mps=math.sqrt(thrust / (2 * RHO * disc)))


# ---------------------------------------------------------------- report

def f3(v):
    return " / ".join(f"{x:.2f}" for x in v)


def report(o):
    c = o["clips"]
    print("rain_stats: frames scanned / in the set / with a horizon; camera-fixed mask")
    for L, d in c.items():
        print(f"  {L}: {d['scanned']} / {d['window']} / {d['horizon']}; mask {d['mask_share']:.1%}")
    t = o.get("tracker_fit")
    if t:
        print(f"horizon fit against attitude.csv: confidence >= {CONF} in {t['tool']} / {t['tracker']} frames, {t['both']} in "
              f"both; there, p99 (max) |difference| roll {t['p99']['roll']:.3f} ({t['max']['roll']:.3f}) deg, pitch "
              f"{t['p99']['pitch']:.3f} ({t['max']['pitch']:.3f}) deg, edge {t['p99']['edge']:.3f} ({t['max']['edge']:.3f}) px")
    st = o["stills"]
    print(f"R1 stills: {st['count']} at {STILL_S:g} s (f{st['first']}-f{st['last']}); none with a drop bounds the "
          f"share at < {st['share_bound_95']:.1%} (95 %)")
    e = o["edge"][RAIN]
    print(f"R1 skyline edge over {e['tracked']} tracked frames: max {e['tracked_max']:.2f} px")
    print("R1 sky blobs: " + ", ".join(f"{L} {d['share']:.1%} of {d['tested']} frames" for L, d in o["blobs"].items()))
    d = o["derived"]
    print(f"R2 drop {DROP_MM:g} mm at {DROP_M:g} m: {d['drop']['deg']:.3f} deg = {d['drop']['px']:.2f} px at the centre; "
          f"one source sample = {d['drop']['sample_px']:.2f} px")
    for L, p in o["profile"].items():
        b = p["bands"]
        print(f"R3 {L} ({p['frames']} frames): sky 2-12 deg above luma {b['sky']['luma']:.1f} chroma {b['sky']['chroma']:.1f} "
              f"texture_rel {b['sky']['texture_rel']:.4f}; skyline 0-2 deg {b['skyline']['luma']:.1f}")
        for k in ("far", "mid", "near"):
            print(f"   {k:4s} {DEP[k][0]:g}-{DEP[k][1]:g} deg below: luma {b[k]['luma']:.1f}, / sky {b[k]['luma_rel_sky']:.3f}, "
                  f"chroma {b[k]['chroma']:.1f}, texture_rel {b[k]['texture_rel']:.3f}")
        print(f"   far / near luma per frame p10/50/90 {f3(p['far_near_q'])} ({p['far_near_frames']} frames); "
              f"far / near chroma {p['far_near_chroma']:.2f}, texture_rel {p['far_near_texture']:.2f}")
    k = d["koschmieder"]
    print(f"R3 Koschmieder: t >= {k['t_min']:.3f} at {k['at_km'][0]:.2f}-{k['at_km'][1]:.2f} km; extinction <= "
          f"{k['extinction_per_km'][0]:.3f}-{k['extinction_per_km'][1]:.3f} /km; visibility >= {k['visibility_km']:.1f} km")
    print("R3 distances (m): " + ", ".join(f"{b} {v[0]:.0f}-{v[1]:.0f}" for b, v in d["distance_m"].items()))
    print("R3-R7 per clip: luma p10/50/90 | contrast std/mean | chroma | r/g b/g | veil p10/50/90, share of p99 | black | sparkle p50")
    for L, s in o["frame"].items():
        print(f"  {L} {s['frames']:5d}: {f3(s['luma_q'])} | {s['contrast']:.3f} | {s['chroma']:.1f} | {s['r_g']:.3f} {s['b_g']:.3f} | "
              f"{f3(s['veil_q'])}, {s['veil_share']:.1%} | {s['black']:.1f} | {s['sparkle_q'][0]:.3f}")
    g = o["grain"]
    print(f"R5 grain over {g['frames']} frames: MAD sigma p10/50/90 {f3(g['q'])}, {g['rel_sky']:.2%} of the sky "
          f"({g['sky']:.1f}); plain std {g['std']:.2f}")
    print(f"R6 skyline edge p5/50/95 (px): P {f3(o['edge'][RAIN]['q'])} (std {o['edge'][RAIN]['std']:.2f}), "
          f"A {f3(o['edge']['A']['q'])}")
    w = d["wet_blur"]
    print(f"R6 all of the difference as blur: {w['width_px']:.2f} px 10-90, sigma {w['sigma_px']:.2f} px = "
          f"{w['of_sample']:.2f} of a source sample")
    for L, r in o["rod"].items():
        print(f"R6 rod {L}: mean image {r['mean_image']['width']:.2f} px (undershoot {r['mean_image']['under']:.2f}); "
              f"{r['frames']} of {r['of']} frames: p10/50/90 {f3(r['width_q'])} px, undershoot {f3(r['under_q'])}")
        for b in r["bursts"]:
            print(f"   burst f{b['frames'][0]}-{b['frames'][1]}: {b['found']} found, {b['width'][0]:.2f}-{b['width'][1]:.2f} px, "
                  f"undershoot {b['under'][0]:.2f}-{b['under'][1]:.2f}")
    for L, sides in o["edge_bands"].items():
        print(f"edge bands {L}: " + "; ".join(f"{k} {v['frames']} of {v['tested']} ({v['share']:.1%}), "
                                              f"{v['gaps_2']} of {v['gaps']} gaps are 2 frames, first {v['first']}"
                                              for k, v in sides.items()))
    sp, ou = o["sparkle"], o["outages"]
    print(f"7.4 sparkle per mille: P median {sp['P']['median']:.3f}, busiest f{sp['P']['busiest']} {sp['P']['max']:.3f}; "
          f"belt {sp['P']['belt']:.3f}, field {sp['P']['field']:.3f}; A median {sp['A']['median']:.3f}")
    for x in ou["each"]:
        print(f"  outage f{x['onset']}-{x['end']}: sparkle max in the 1 s before {x['sparkle_pre_max']:.3f}; grain 2 s / 1 s "
              f"before {x['grain_pre2']:.3f} / {x['grain_pre1']:.3f}; edge 1 s before / after {x['edge_pre']:.2f} / "
              f"{x['edge_post']:.2f} (max {x['edge_max']:.2f}); sparkle inside "
              + " ".join(f"f{f}:{v:.3f}" for f, v in x["sparkle_inside"].items()))
    print(f"  pooled 1 s pre-onset grain {ou['grain_pre1_pooled']:.3f}; {len(OUTAGES)} random 1 s windows reach it in "
          f"{ou['random_share']:.0%} of {DRAWS} draws; grain belt {g['belt']:.3f}, field {g['field']:.3f}")
    pw = d["prop_wash"]
    print(f"R8 prop wash at {pw['mass_kg']:g} kg: {pw['thrust_n']:.2f} N per {DISC_IN:g}\" disc of {pw['disc_m2']:.4f} m2 -> "
          f"{pw['induced_mps']:.2f} m/s induced in hover")


# ---------------------------------------------------------------- main

def main(argv=None):
    p = argparse.ArgumentParser(prog="rain_stats.py")
    p.add_argument("--ref", default=str(HERE.parents[1] / "reference"))
    p.add_argument("--reuse", action="store_true", help="read the per-frame files of an earlier run instead of decoding")
    a = p.parse_args(argv)
    ref = Path(a.ref).resolve()
    out = ref / "_frames" / RAIN
    if not out.is_dir():
        raise RuntimeError(f"no extractor output for {RAIN}; run extract_frames.py first")
    folder = ref_paths.frames_path(ref.parent, out / "rain_stats", "the per-frame folder")
    folder.mkdir(exist_ok=True)
    t0, sc = time.time(), {}
    for L in CLIPS:
        sc[L], reused = load_or_scan(ref, L, folder, a.reuse)
        print(f"rain_stats: {L} {'reused' if reused else 'scanned'} ({len(sc[L]['frames'])} frames)", flush=True)
    o = analyse(sc)
    o["tracker_fit"] = tracker_fit(ref, sc[RAIN])
    report(o)
    (out / "rain_stats.json").write_text(json.dumps(finite(o), indent=1, allow_nan=False), encoding="utf-8")
    print(f"rain_stats: done in {time.time() - t0:.0f} s")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except SystemExit:
        raise
    except Exception as e:
        traceback.print_exc()
        print(f"rain_stats: FAILED: {e}", file=sys.stderr)
        sys.exit(1)
