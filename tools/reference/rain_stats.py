"""Rain on the feed: the rain clip P against the dry control clips (docs/reference-notes/wind.md section 7).

Run headless with Blender 5.2+ (for its numpy and video decoding) from the repo root:
    blender -b --factory-startup --python tools/reference/rain_stats.py -- [--reuse]

Decodes every frame of P, and every in-flight frame of the dry controls A-F, H and L, at native size
through track_attitude.py's decoder, measures each one and prints every number of wind.md section 7.
It writes only inside reference/_frames/P/ (git-ignored):
- rain_stats.json: every printed number, missing values as null
- rain_stats/<letter>.npz: the per-frame measurements. --reuse reads them instead of decoding again
  (about an hour for all clips, 1.1 s per native frame; with --reuse, seconds).

Definitions (wind.md section 7.1):
- Frame sets. P: its picture frames outside the four N12 outages (video-feed.md, precursor frames
  included), with MARGIN frames on each side. Controls: the in-flight window of video-feed.md 1.1,
  i.e. the picture frames before the last 1.0 s ahead of the first loss (L never loses picture).
- Statistics use a 2x2 area-averaged copy (960x540) without the camera-fixed mask (OSD, airframe
  parts, border), both as track_attitude.py builds them. Sparkles, grain, sky blobs and the rod edge
  use the native frame.
- Levels are 8-bit code values. Luma is BT.601 and chroma the length of (Cb, Cr). Texture is the mean of
  |3x3 box mean - 13x13 box mean| of luma; relative texture divides it by the mean luma.
- Horizon: track_attitude.py's fit on the 960x540 copy, compared with P's attitude.csv when it exists.
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

OPSEC: letters only. Nothing this tool writes may be committed, copied out of reference/ or uploaded.
"""
import argparse
import csv
import json
import math
import sys
import time
import traceback
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import track_attitude as ta  # noqa: E402  (decoder, camera-fixed mask, horizon fit, lens model)
from wind_stats import finite  # noqa: E402  (JSON without NaN)

VERSION = 1                          # of the per-frame files; --reuse decodes again on a mismatch
RAIN, DAY, ROD, NIGHT = "P", ("A", "B", "C", "D", "E", "F"), "H", "L"
CLIPS = (RAIN,) + DAY + (ROD, NIGHT)
FIRST_LOSS = dict(A=424, B=310, C=174, D=208, E=263, F=250, H=628)   # video-feed.md N5-N8
LOSS_GUARD = 30                      # the in-flight window ends 1.0 s before the first loss
OUTAGES = ((676, 687), (951, 958), (966, 985), (1144, 1164))       # P's N12 outages, precursors included
MARGIN = 2                           # frames kept clear on each side of a P outage (onsets can be torn)
BELT = (121, 330)                    # P's tree-belt crossing (wind.md 1.6)
CONF = ta.CONF_OK
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
LUMA = ta.LUMA


# ---------------------------------------------------------------- per-frame measurement

def half(rgb):
    h, w = rgb.shape[:2]
    return rgb.reshape(h // 2, 2, w // 2, 2, 3).mean(axis=(1, 3))


def ycc(rgb):
    y = rgb @ LUMA
    return y, 0.564 * (rgb[..., 2] - y), 0.713 * (rgb[..., 0] - y)


def pixel_rays(h, w):
    """Undistorted pinhole coordinates of every pixel centre (half-widths)."""
    cols, rows = np.meshgrid(np.arange(w) + 0.5, np.arange(h) + 0.5)
    return ta.undistort((cols - w / 2) / (w / 2), (rows - h / 2) / (w / 2), ta.K1)


def depression(roll, pitch, rays):
    """Depression of every pixel below the horizon (deg; negative = sky) for a camera at roll/pitch.
    The inverse of track_attitude.level_to_camera."""
    xu, yu = rays
    n = np.sqrt(xu * xu + yu * yu + ta.FOCAL ** 2)
    xc, yc, zc = xu / n, yu / n, ta.FOCAL / n
    cr, sr = math.cos(math.radians(roll)), math.sin(math.radians(roll))
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    y = xc * sr + yc * cr
    return np.degrees(np.arcsin(np.clip(y * cp - zc * sp, -1, 1)))


def box_sum(a, r):
    return ta.box_mean(a, r) * (2 * r + 1) ** 2


def sky_blobs(y, sky):
    """Compact blobs in the sky, the way a drop on the lens would look: luma departing > BLOB_DEV from
    the sky-only local mean, grouped on BLOB_CELL cells (a cell counts when more than BLOB_CELL_SHARE
    of its sky pixels depart); connected groups of >= BLOB_MIN_CELLS cells whose bounding box is at
    most BLOB_ASPECT times longer than wide. Returns their count, or NaN with too little sky."""
    if sky.sum() < BLOB_MIN_SKY:
        return math.nan
    s = sky.astype(np.float64)
    hit = sky & (np.abs(y - box_sum(y * s, BLOB_R) / np.maximum(box_sum(s, BLOB_R), 1)) > BLOB_DEV)
    c = BLOB_CELL
    H, W = y.shape[0] // c, y.shape[1] // c
    hc = hit[:H * c, :W * c].reshape(H, c, W, c).sum(axis=(1, 3))
    sc = sky[:H * c, :W * c].reshape(H, c, W, c).sum(axis=(1, 3))
    grid = (hc > BLOB_CELL_SHARE * np.maximum(sc, 1)) & (sc > BLOB_CELL_SKY)
    seen, count = np.zeros_like(grid), 0
    for r0, c0 in zip(*np.nonzero(grid)):
        if seen[r0, c0]:
            continue
        stack, cells = [(r0, c0)], []
        seen[r0, c0] = True
        while stack:
            r, q = stack.pop()
            cells.append((r, q))
            for rr, qq in ((r + 1, q), (r - 1, q), (r, q + 1), (r, q - 1)):
                if 0 <= rr < H and 0 <= qq < W and grid[rr, qq] and not seen[rr, qq]:
                    seen[rr, qq] = True
                    stack.append((rr, qq))
        if len(cells) >= BLOB_MIN_CELLS:
            rs, qs = [p[0] for p in cells], [p[1] for p in cells]
            bh, bw = max(rs) - min(rs) + 1, max(qs) - min(qs) + 1
            count += max(bh, bw) / min(bh, bw) <= BLOB_ASPECT
    return count


def rod_edge(y):
    """10-90 % width (native px) of the right airframe rod's upper edge in a native luma image, and the
    undershoot on its dark side as a share of the edge step. The rod is camera-fixed, so the width is
    the whole chain's response (lens, camera, link, recorder) with nothing moving in the scene. The
    edge lies within ~10 deg of horizontal and is measured vertically. None where no edge is found."""
    x0, x1, r0, r1 = ROD_BOX
    g = y[1:] - y[:-1]                                   # positive: brighter below
    xs, rows = [], []
    for x in range(x0, x1):
        col = g[r0:r1, x]
        k = int(np.argmax(col))
        if col[k] < ROD_STEP or y[r0 + k + 3:r0 + k + 8, x].mean() < ROD_BRIGHT:
            continue
        off = 0.0
        if 0 < k < len(col) - 1 and col[k - 1] - 2 * col[k] + col[k + 1] < 0:
            off = 0.5 * (col[k - 1] - col[k + 1]) / (col[k - 1] - 2 * col[k] + col[k + 1])
        xs.append(x)
        rows.append(r0 + k + 1.0 + off)                  # the boundary between rows k and k + 1
    if len(xs) < ROD_MIN_COLS:
        return None
    xs, rows = np.array(xs, float), np.array(rows)
    for _ in range(3):
        p = np.polyfit(xs, rows, 2)
        res = rows - np.polyval(p, xs)
        keep = np.abs(res) < max(1.5, 2.5 * np.std(res))
        xs, rows = xs[keep], rows[keep]
    p = np.polyfit(xs, rows, 2)
    d, v = [], []
    for x in xs.astype(int):                             # oversampled edge-spread function
        e = np.polyval(p, x)
        rr = np.arange(int(e) - 14, int(e) + 14)
        d.append(rr + 0.5 - e)
        v.append(y[rr, x])
    d, v = np.concatenate(d), np.concatenate(v)
    bins = np.arange(-14, 14.01, 0.25)
    idx = np.digitize(d, bins) - 1
    esf = np.array([np.median(v[idx == i]) if (idx == i).sum() > 3 else np.nan for i in range(len(bins) - 1)])
    mid = 0.5 * (bins[1:] + bins[:-1])
    lo, hi = np.nanmedian(esf[mid < -9]), np.nanmedian(esf[(mid > 5) & (mid < 9)])
    if not hi - lo >= ROD_MIN_RISE:                      # no bright rod below a dark background
        return None
    ok = np.isfinite(esf)
    m, e = mid[ok], (esf[ok] - lo) / (hi - lo)
    c = int(np.argmin(np.abs(m)))

    def cross(level):
        i = c
        if e[c] < level:
            while i < len(e) - 1 and e[i] < level:
                i += 1
            return np.interp(level, [e[i - 1], e[i]], [m[i - 1], m[i]])
        while i > 0 and e[i] >= level:
            i -= 1
        return np.interp(level, [e[i], e[i + 1]], [m[i], m[i + 1]])
    return float(cross(0.9) - cross(0.1)), float(-np.min(e[(m < 0) & (m > -8)]))


def measure(full, mask, rays_half, rays_full, rod, frame):
    """Every per-frame quantity of one native frame (NaN where it can't be measured)."""
    r = {k: math.nan for k in FIELDS}
    rgb = half(full)
    r["pic"] = float(ta.is_picture(rgb))
    if not r["pic"]:
        return r, None
    keep = ~mask
    y, cb, cr = ycc(rgb)
    chroma = np.hypot(cb, cr)
    m1 = ta.box_mean(y, TEX_R[0])
    tex = np.abs(m1 - ta.box_mean(y, TEX_R[1]))
    yk = y[keep]
    r.update(luma=yk.mean(), luma_std=yk.std(), chroma=chroma[keep].mean(), black=np.percentile(y[mask], 1))
    r["red"], r["green"], r["blue"] = rgb[keep].mean(axis=0)
    r["luma_p05"], r["luma_p99"] = np.percentile(yk, [0.5, 99])
    blocks = np.where(mask, np.nan, y)
    H, W = blocks.shape[0] // BLOCK, blocks.shape[1] // BLOCK
    blocks = blocks[:H * BLOCK, :W * BLOCK].reshape(H, BLOCK, W, BLOCK).mean(axis=(1, 3))
    r["block_median"] = np.nanmedian(blocks)
    for name, (a, b, c0, c1) in EDGE_BANDS.items():
        sel = blocks[a // BLOCK:b // BLOCK, c0 // BLOCK:c1 // BLOCK]
        r["band_" + name] = np.nanmean(sel) if np.isfinite(sel).any() else math.nan
    hz = ta.horizon(rgb, mask, ta.K1, ta.FOCAL, np.random.default_rng(frame))
    r.update(roll=hz["roll"], pitch=hz["pitch"], conf=min(hz["conf_roll"], hz["conf_pitch"]), edge=hz["edge_px"])
    yf, cbf, crf = ycc(full)
    keep_f = ~np.repeat(np.repeat(mask, 2, 0), 2, 1)
    dev = np.hypot(cbf - ta.box_mean(cbf, SPARKLE_R), crf - ta.box_mean(crf, SPARKLE_R))
    r["sparkle"] = 1000.0 * ((dev > SPARKLE_DEV) & keep_f).sum() / keep_f.sum()
    bands = np.full((len(DEP), 4), np.nan)
    if r["conf"] >= CONF:
        dep = depression(r["roll"], r["pitch"], rays_half)
        for i, (lo, hi) in enumerate(DEP.values()):
            s = keep & (dep >= lo) & (dep < hi)
            bands[i] = s.sum(), y[s].sum(), chroma[s].sum(), tex[s].sum()
        dep = depression(r["roll"], r["pitch"], rays_full)
        sky = keep_f & (dep <= -GRAIN_DEG)
        sky[:GRAIN_EDGE] = False
        sky[:, :GRAIN_EDGE] = False
        sky[:, -GRAIN_EDGE:] = False
        if sky.sum() > GRAIN_MIN_PX:
            hp = (yf - ta.box_mean(yf, GRAIN_R))[sky]
            r["grain"] = 1.4826 * np.median(np.abs(hp - np.median(hp)))
            r["grain_std"], r["grain_sky"] = hp.std(), yf[sky].mean()
        r["blobs"] = sky_blobs(yf, keep_f & (dep <= -BLOB_DEG))
    if rod:
        e = rod_edge(yf)
        if e:
            r["rod"], r["rod_under"] = e
    return r, bands


FIELDS = ("pic", "luma", "luma_std", "chroma", "red", "green", "blue", "luma_p05", "luma_p99", "black",
          "block_median", "band_left", "band_right", "roll", "pitch", "conf", "edge", "sparkle", "grain",
          "grain_std", "grain_sky", "blobs", "rod", "rod_under")


# ---------------------------------------------------------------- scanning a clip

def outage_mask(frames):
    out = np.zeros(len(frames), bool)
    for a, b in OUTAGES:
        out |= (frames >= a - MARGIN) & (frames <= b + MARGIN)
    return out


def scan(ref, letter, folder):
    """Decode and measure one clip. Returns a dict of per-frame arrays plus the clip-level values."""
    t0 = time.time()
    dec = ta.Decoder(ta.clip_path(ref, letter), folder / "_decode.bmp")
    try:
        n = dec.n
        last = FIRST_LOSS[letter] - LOSS_GUARD - 1 if letter in FIRST_LOSS else n
        picks = np.unique(np.linspace(1, n, min(n, ta.MASK_FRAMES)).round().astype(int))
        mask = ta.static_mask([f for f in (half(dec.grab(int(i), 100)) for i in picks) if ta.is_picture(f)])
        rod = letter in (RAIN, ROD)
        rays_half, rays_full = pixel_rays(*mask.shape), pixel_rays(mask.shape[0] * 2, mask.shape[1] * 2)
        frames = np.arange(1, last + 1)
        window = ~outage_mask(frames) if letter == RAIN else np.ones(len(frames), bool)
        rows, bands, mean_y, n_mean = [], np.full((len(frames), len(DEP), 4), np.nan), None, 0
        for i, f in enumerate(frames):
            full = dec.grab(int(f), 100)
            r, b = measure(full, mask, rays_half, rays_full, rod, int(f))
            rows.append(r)
            if b is not None:
                bands[i] = b
                if rod and window[i]:
                    y = full @ LUMA
                    mean_y = y if mean_y is None else mean_y + y
                    n_mean += 1
            if f % 100 == 0:
                print(f"rain_stats: {letter} {f}/{last} frames, {time.time() - t0:.0f} s", flush=True)
    finally:
        dec.close()
    s = {k: np.array([r[k] for r in rows], float) for k in FIELDS}
    s.update(frames=frames, bands=bands, mask_share=float(mask.mean()), fps=float(dec.fps), n=n,
             version=VERSION, seconds=time.time() - t0)
    s["rod_mean"] = np.array(rod_edge(mean_y / n_mean) or (math.nan, math.nan)) if n_mean else np.full(2, math.nan)
    return s


def load_or_scan(ref, letter, folder, reuse):
    path = folder / f"{letter}.npz"
    if reuse and path.exists():
        s = dict(np.load(path))
        if int(s["version"]) == VERSION:
            return s, True
    s = scan(ref, letter, folder)
    np.savez_compressed(path, **s)
    return s, False


# ---------------------------------------------------------------- statistics

def q(a, p):
    a = np.asarray(a, float)
    a = a[np.isfinite(a)]
    return [float(v) for v in np.percentile(a, p)] if len(a) else [math.nan] * len(p)


def med(a):
    return q(a, [50])[0]


def top(a):
    a = np.asarray(a, float)
    return float(np.nanmax(a)) if np.isfinite(a).any() else math.nan


def span(values):
    v = [x for x in values if math.isfinite(x)]
    return [min(v), max(v)] if v else [math.nan, math.nan]


def window_of(letter, s):
    ok = s["pic"] > 0
    return ok & ~outage_mask(s["frames"]) if letter == RAIN else ok


def frame_stats(s, ok):
    y = s["luma"][ok]
    veil = (s["luma_p05"] - s["black"])[ok]
    return dict(frames=int(ok.sum()), luma_q=q(y, [10, 50, 90]), contrast=med(s["luma_std"][ok] / y),
                chroma=med(s["chroma"][ok]), r_g=med(s["red"][ok] / s["green"][ok]),
                b_g=med(s["blue"][ok] / s["green"][ok]), veil_q=q(veil, [10, 50, 90]),
                veil_share=med(veil / s["luma_p99"][ok]), black=med(s["black"][ok]),
                sparkle_q=q(s["sparkle"][ok], [50, 90]))


def profile(s, ok):
    """Horizon-relative bands over the frames with a horizon."""
    ok = ok & (s["conf"] >= CONF)
    b = s["bands"][ok]                                   # frames x bands x (px, luma, chroma, texture)
    tot = np.nansum(b, axis=0)
    names = list(DEP)
    band = {k: dict(luma=tot[i, 1] / tot[i, 0], chroma=tot[i, 2] / tot[i, 0], texture_rel=tot[i, 3] / tot[i, 1],
                    px=tot[i, 0]) for i, k in enumerate(names)}
    sky = band["sky"]["luma"]
    for k in band:
        band[k]["luma_rel_sky"] = band[k]["luma"] / sky
    i_sky, i_far, i_near = names.index("sky"), names.index("far"), names.index("near")
    good = (b[:, [i_sky, i_far, i_near], 0] > MIN_BAND_PX).all(axis=1)
    ratio = (b[good, i_far, 1] / b[good, i_far, 0]) / (b[good, i_near, 1] / b[good, i_near, 0])
    return dict(frames=int(ok.sum()), bands=band, far_near_q=q(ratio, [10, 50, 90]), far_near_frames=int(good.sum()),
                far_near_chroma=band["far"]["chroma"] / band["near"]["chroma"],
                far_near_texture=band["far"]["texture_rel"] / band["near"]["texture_rel"])


def edge_band(s, ok, side):
    """Frames where a band brightens by > BAND_STEP against its neighbours after the frame-wide level
    change is removed, as a share of the frames whose neighbours are both in the set."""
    m, a = s["band_" + side], s["block_median"]
    d = (m[1:-1] - 0.5 * (m[:-2] + m[2:])) - (a[1:-1] - 0.5 * (a[:-2] + a[2:]))
    valid = ok[1:-1] & ok[:-2] & ok[2:] & np.isfinite(d)
    hits = s["frames"][1:-1][valid & (d > BAND_STEP)]
    gaps = np.diff(hits)
    return dict(tested=int(valid.sum()), frames=int(len(hits)), share=len(hits) / max(int(valid.sum()), 1),
                gaps_2=int((gaps == 2).sum()), gaps=int(len(gaps)), first=[int(f) for f in hits[:12]])


def rod_stats(s, ok):
    burst_starts = (1, 1 + (s["n"] - BURST) // 2, s["n"] - BURST + 1)     # extract_frames.py's bursts
    by = dict(zip(s["frames"].tolist(), zip(s["rod"], s["rod_under"])))
    bursts = []
    for b0 in burst_starts:
        fr = [f for f in range(int(b0), int(b0) + BURST) if f in by]
        if len(fr) == BURST:
            w = [by[f][0] for f in fr]
            u = [by[f][1] for f in fr]
            bursts.append(dict(frames=[fr[0], fr[-1]], found=int(np.isfinite(w).sum()), width=span(w), under=span(u)))
    w = s["rod"][ok]
    return dict(mean_image=dict(width=float(s["rod_mean"][0]), under=float(s["rod_mean"][1])),
                frames=int(np.isfinite(w).sum()), of=int(ok.sum()), width_q=q(w, [10, 50, 90]),
                under_q=q(s["rod_under"][ok], [10, 50, 90]), bursts=bursts)


def outages(s, ok):
    """Sparkles, grain and the skyline edge around each P outage."""
    fr = s["frames"]
    at = {int(f): i for i, f in enumerate(fr)}

    def vals(key, frames, sel=None):
        """Values over the picture frames among `frames` (any frame, outages included)."""
        idx = [at[f] for f in frames if f in at and s["pic"][at[f]] > 0 and (sel is None or sel[at[f]])]
        return s[key][idx]
    good = s["conf"] >= CONF
    out, pre1 = [], []
    for a, b in OUTAGES:
        before, after = range(a - PRE, a - MARGIN), range(b + MARGIN + 1, b + PRE + 1)
        g1 = vals("grain", before)
        pre1.append(g1)
        e_pre, e_post = vals("edge", before, good), vals("edge", after, good)
        inside = [f for f in range(a, b + 1) if f in at and s["pic"][at[f]] > 0]
        out.append(dict(onset=a, end=b, sparkle_pre_max=top(vals("sparkle", before)),
                        grain_pre2=med(vals("grain", range(a - PRE2, a - MARGIN))), grain_pre1=med(g1),
                        edge_pre=float(np.nanmean(e_pre)), edge_post=float(np.nanmean(e_post)),
                        edge_max=top(np.concatenate([e_pre, e_post])),
                        sparkle_inside={int(f): float(s["sparkle"][at[f]]) for f in inside}))
    pooled = med(np.concatenate(pre1))
    starts = fr[ok & (fr + PRE - MARGIN <= fr[ok].max())]
    rng, hits = np.random.default_rng(0), 0
    grain_ok = np.where(ok, s["grain"], np.nan)
    for _ in range(DRAWS):
        v = np.concatenate([grain_ok[at[int(f)]:at[int(f)] + PRE - MARGIN] for f in rng.choice(starts, len(OUTAGES))])
        hits += med(v) >= pooled
    return dict(each=out, grain_pre1_pooled=pooled, random_share=hits / DRAWS)


def analyse(sc):
    P = sc[RAIN]
    okP = window_of(RAIN, P)
    fps, n = float(P["fps"]), int(P["n"])
    o = dict(clips={L: dict(scanned=int(len(s["frames"])), window=int(window_of(L, s).sum()),
                            horizon=int((window_of(L, s) & (s["conf"] >= CONF)).sum()), mask_share=float(s["mask_share"]))
                    for L, s in sc.items()})
    stills = [f for f in (1 + round(k * STILL_S * fps) for k in range(n)) if f <= n]
    o["stills"] = dict(count=len(stills), first=stills[0], last=stills[-1], share_bound_95=1 - 0.05 ** (1 / len(stills)))
    o["frame"] = {L: frame_stats(s, window_of(L, s)) for L, s in sc.items()}
    o["profile"] = {L: profile(sc[L], window_of(L, sc[L])) for L in (RAIN, "A")}
    tracked = (P["pic"] > 0) & (P["conf"] >= CONF)
    o["edge"] = {L: dict(frames=int((window_of(L, sc[L]) & (sc[L]["conf"] >= CONF)).sum()),
                         q=q(sc[L]["edge"][window_of(L, sc[L]) & (sc[L]["conf"] >= CONF)], [5, 50, 95]),
                         std=float(np.nanstd(sc[L]["edge"][window_of(L, sc[L]) & (sc[L]["conf"] >= CONF)])))
                 for L in (RAIN, "A")}
    o["edge"][RAIN].update(tracked=int(tracked.sum()), tracked_max=float(np.nanmax(P["edge"][tracked])))
    belt = (P["frames"] >= BELT[0]) & (P["frames"] <= BELT[1])
    g = P["grain"][okP]
    o["grain"] = dict(frames=int(np.isfinite(g).sum()), q=q(g, [10, 50, 90]), std=med(P["grain_std"][okP]),
                      sky=med(P["grain_sky"][okP]), belt=med(P["grain"][okP & belt]), field=med(P["grain"][okP & ~belt]))
    o["grain"]["rel_sky"] = o["grain"]["q"][1] / o["grain"]["sky"]
    sp = np.where(okP, P["sparkle"], np.nan)
    o["sparkle"] = dict(P=dict(median=med(sp), busiest=int(P["frames"][np.nanargmax(sp)]), max=top(sp),
                               belt=med(sp[belt]), field=med(sp[~belt])),
                        A=dict(median=o["frame"]["A"]["sparkle_q"][0]))
    o["blobs"] = {}
    for L in (RAIN, "A"):
        b = sc[L]["blobs"][window_of(L, sc[L])]
        b = b[np.isfinite(b)]                            # NaN = too little sky, not "no blob"
        o["blobs"][L] = dict(tested=int(len(b)), share=float((b > 0).mean()) if len(b) else math.nan)
    o["outages"] = outages(P, okP)
    o["edge_bands"] = {L: {side: edge_band(sc[L], window_of(L, sc[L]), side) for side in EDGE_BANDS}
                       for L in (RAIN, "C", "D", "E")}
    o["rod"] = {L: rod_stats(sc[L], window_of(L, sc[L])) for L in (RAIN, ROD)}
    o["derived"] = derived(o)
    return o


def tracker_fit(ref, s):
    """This tool's horizon fit for P (on its 2x2 copy) against track_attitude.py's attitude.csv (on the
    sequencer's 960x540 downscale): the frames each trusts, and the largest differences where both do."""
    path = ref / "_frames" / RAIN / "attitude.csv"
    if not path.exists():
        return None
    rows = {int(r["frame"]): r for r in csv.DictReader(path.open(encoding="utf-8"))}
    col = lambda k: np.array([float(rows[int(f)][k] or "nan") for f in s["frames"]])
    conf = np.minimum(col("conf_roll"), col("conf_pitch"))
    both = (s["conf"] >= CONF) & (conf >= CONF)
    diff = {k: np.abs(s[k] - col(c))[both] for k, c in (("roll", "roll_deg"), ("pitch", "pitch_deg"), ("edge", "edge_px"))}
    return dict(tool=int((s["conf"] >= CONF).sum()), tracker=int((conf >= CONF).sum()), both=int(both.sum()),
                p99={k: float(np.percentile(d, 99)) for k, d in diff.items()}, max={k: float(d.max()) for k, d in diff.items()})


def derived(o):
    """Numbers computed from the measured ones with the stated models (wind.md section 7)."""
    pr = o["profile"][RAIN]["bands"]
    sky_line, far, near = pr["skyline"]["luma"], pr["far"]["luma"], pr["near"]["luma"]
    t = (sky_line - far) / (sky_line - 0.5 * near)       # Koschmieder, far terrain >= half the near's brightness
    mid_far = 0.5 * sum(DEP["far"])
    dist_km = [h / math.tan(math.radians(mid_far)) / 1000 for h in HEIGHTS]
    ext = [-math.log(t) / d for d in dist_km]
    ranges = {k: [HEIGHTS[0] / math.tan(math.radians(DEP[k][1])), HEIGHTS[1] / math.tan(math.radians(DEP[k][0]))]
              for k in ("far", "mid", "near")}
    ep, ea = o["edge"][RAIN]["q"][1], o["edge"]["A"]["q"][1]
    wet = math.sqrt(max(ep ** 2 - ea ** 2, 0))
    sigma = wet / (2 * 1.2816)                           # 10-90 % of a Gaussian is 2.563 sigma
    sample = 2 * 960 / SOURCE_SAMPLES                    # native px per source sample
    drop = 2 * math.degrees(math.atan(DROP_MM / 2000 / DROP_M))
    disc = math.pi * (DISC_IN * 0.0254 / 2) ** 2
    thrust = MASS_KG * G / 4
    return dict(distance_m=ranges, koschmieder=dict(skyline=sky_line, far=far, near=near, t_min=t, at_km=dist_km,
                                                    extinction_per_km=[min(ext), max(ext)], visibility_km=KOSCHMIEDER / max(ext)),
                wet_blur=dict(width_px=wet, sigma_px=sigma, of_sample=sigma / sample),
                drop=dict(deg=drop, px=math.radians(drop) * ta.FOCAL * 960, sample_px=sample),
                prop_wash=dict(mass_kg=MASS_KG, disc_m2=disc, thrust_n=thrust, induced_mps=math.sqrt(thrust / (2 * RHO * disc))))


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

def main():
    if bpy.app.version < ta.MIN_BLENDER:
        raise RuntimeError(f"Blender {bpy.app.version_string} is too old; this tool needs 5.2 or newer")
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="rain_stats.py")
    p.add_argument("--ref", default=str(Path(__file__).resolve().parents[2] / "reference"))
    p.add_argument("--reuse", action="store_true", help="read the per-frame files of an earlier run instead of decoding")
    a = p.parse_args(argv)
    ref = Path(a.ref)
    out = ref / "_frames" / RAIN
    if not out.is_dir():
        raise RuntimeError(f"no extractor output for {RAIN}; run extract_frames.py first")
    folder = out / "rain_stats"
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


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:
        traceback.print_exc()
        print(f"rain_stats: FAILED: {e}", file=sys.stderr)
        sys.exit(1)
