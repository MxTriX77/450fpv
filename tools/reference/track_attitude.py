"""Frame-by-frame camera attitude of a reference clip, from the horizon and image motion.

Run headless with Blender 5.2+ (for its numpy and video decoding) from the repo root:
    blender -b --factory-startup --python tools/reference/track_attitude.py -- --letter P [--overlays 20]
    blender -b --factory-startup --python tools/reference/track_attitude.py -- --selftest <scratch dir>

For every frame of the clip it writes one row of reference/_frames/<letter>/attitude.csv (git-ignored):
camera roll and pitch from the horizon after the lens distortion is removed, yaw rate from phase
correlation of a horizon strip, and a 0..1 confidence for each. Frames without a usable horizon keep
empty values or low confidence; nothing is interpolated. --selftest renders a synthetic clip with
known attitude through the same lens model and analog-like degradation into the given folder, tracks
it through the same decoding path and prints the errors.

Camera model (docs/reference-notes/wind.md section 1):
- Coordinates are normalised to the half-width, origin at the frame centre, y down.
- Lens: p_u = p_d * (1 + K1 * r_d^2), K1 from video-feed.md O1. The undistorted image is a pinhole
  image with focal length FOCAL half-widths.
- FOCAL is an assumption: an equidistant (f-theta) lens matched to K1 at small angles, K1*f^2 = 1/3.
  It scales pitch and yaw rate, never roll.
- Roll: angle of the undistorted horizon line, positive when the camera is rolled right (the
  horizon's right end rises). Pitch: elevation of the optical axis, atan(d / FOCAL), d = signed
  distance of the line below the centre; positive = looking up. Yaw rate: heading rate about the
  vertical, positive = turning right.

OPSEC: letters only. Nothing this tool writes may be committed, copied out of reference/ or uploaded.
"""
import argparse
import math
import sys
import time
import traceback
from pathlib import Path

import bpy
import numpy as np

MIN_BLENDER = (5, 2, 0)
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
DUP_DIFF = 2.0                   # mean |RGB difference| below which a frame repeats its predecessor
JUMP_DEG = 15.0                  # roll/pitch jump vs neighbour median that is rejected
GATE_NEIGH = 3                   # neighbours each side for the jump gate
CONF_OK = 0.5                    # "high confidence" used by the yaw strip, overlays and statistics
STRIP_AZ = 50.0                  # yaw strip: azimuth half-range, deg
STRIP_EL = (-5.0, 1.5)           # yaw strip: elevation range relative to the horizon, deg
STRIP_RES = 0.15                 # yaw strip sample spacing, deg
MAX_SHIFT = (15.0, 3.0)          # largest believable azimuth / elevation shift per frame pair, deg
PEAK_LO, PEAK_HI = 0.03, 0.12    # phase-correlation peak height mapped to 0..1 yaw confidence
UNIQUE_LO, UNIQUE_HI = 1.3, 2.0  # main peak / next-best peak mapped to 0..1 (repetitive texture)
PEAK_EXCL = 3                    # samples around the main peak ignored when finding the next-best
LUMA = np.array([0.299, 0.587, 0.114], dtype=np.float32)

COLUMNS = ("frame", "time_s", "roll_deg", "pitch_deg", "yaw_rate_dps", "conf_roll", "conf_pitch",
           "conf_yaw", "dup", "n_cols", "inlier_frac", "contrast", "span", "resid_px", "yaw_peak",
           "yaw_unique", "yaw_del_deg", "flag")


# ---------------------------------------------------------------- geometry

def undistort(xd, yd, k1):
    s = 1.0 + k1 * (xd * xd + yd * yd)
    return xd * s, yd * s


def distort(xu, yu, k1):
    """Inverse of undistort: solve k1*r^3 + r = r_u for r (Cardano, one real root for k1 > 0)."""
    ru = np.hypot(xu, yu)
    p, q = 1.0 / (3.0 * k1), ru / (2.0 * k1)
    disc = np.sqrt(q * q + p ** 3)
    rd = np.cbrt(q + disc) + np.cbrt(q - disc)
    s = np.where(ru > 1e-12, rd / np.maximum(ru, 1e-12), 1.0)
    return xu * s, yu * s


def level_to_camera(az, el, roll, pitch):
    """Unit rays given by azimuth/elevation (deg, level frame of the camera's heading) in camera
    coordinates (x right, y down, z forward) for a camera at roll/pitch (deg)."""
    a, e = np.radians(az), np.radians(el)
    wx, wy, wz = np.cos(e) * np.sin(a), -np.sin(e), np.cos(e) * np.cos(a)
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    x, y, z = wx, wy * cp + wz * sp, -wy * sp + wz * cp
    cr, sr = math.cos(math.radians(roll)), math.sin(math.radians(roll))
    return x * cr + y * sr, -x * sr + y * cr, z


def project(az, el, roll, pitch, k1, focal, w, h):
    """Level-frame directions -> distorted pixel coordinates (col, row, pixel centres at +0.5) at
    frame size w x h. Directions behind the camera come back as NaN."""
    x, y, z = level_to_camera(az, el, roll, pitch)
    front = z > 1e-6
    zs = np.where(front, z, 1.0)
    xd, yd = distort(focal * x / zs, focal * y / zs, k1)
    col = np.where(front, xd * (w / 2) + w / 2, np.nan)
    row = np.where(front, yd * (w / 2) + h / 2, np.nan)
    return col, row


# ---------------------------------------------------------------- horizon

def feature(rgb):
    """Sky-likeness: luma minus colourfulness. Grey sky stays high, brown soil and yellow straw drop."""
    return rgb @ LUMA - np.abs(rgb[..., 0] - rgb[..., 2])


def column_edges(feat, mask):
    """Per column, the strongest step from brighter above (STEP_WIN rows) to darker below
    (BELOW_WIN rows, so a thin dark line such as a prop blade scores low). Camera-fixed pixels take
    no part in either mean. Returns (cols, rows, steps) for the columns whose step reaches STEP_MIN;
    rows are edge positions in pixels from the top (sub-pixel)."""
    h, w = feat.shape
    top, bot = BORDER_TOP, h - BORDER_BOTTOM
    cs, cn = np.zeros((h + 1, w)), np.zeros((h + 1, w))
    np.cumsum(np.where(mask, 0.0, feat), axis=0, out=cs[1:])
    np.cumsum(~mask, axis=0, out=cn[1:])
    rs = np.arange(top + MIN_ABOVE, bot - MIN_BELOW + 1)
    lo, hi = np.maximum(top, rs - STEP_WIN), np.minimum(bot, rs + BELOW_WIN)
    na, nb = cn[rs] - cn[lo], cn[hi] - cn[rs]
    s = (cs[rs] - cs[lo]) / np.maximum(na, 1) - (cs[hi] - cs[rs]) / np.maximum(nb, 1)
    s[(na < MIN_ABOVE) | (nb < MIN_BELOW)] = -np.inf
    k = np.argmax(s, axis=0)
    cols = np.arange(w)
    best = s[k, cols]
    km, kp = np.clip(k - 1, 0, len(rs) - 1), np.clip(k + 1, 0, len(rs) - 1)
    den = s[km, cols] - 2 * best + s[kp, cols]
    with np.errstate(invalid="ignore"):
        off = np.where(np.isfinite(den) & (den < 0), 0.5 * (s[km, cols] - s[kp, cols]) / np.where(den < 0, den, -1), 0.0)
    rows = rs[k] + np.clip(np.nan_to_num(off), -0.5, 0.5)
    ok = best >= STEP_MIN
    return cols[ok], rows[ok], best[ok]


def fit_line(x, y, rng):
    """RANSAC, then total least squares on the inliers (twice). Returns (point, unit direction with
    dx >= 0, inlier mask) or None."""
    n = len(x)
    if n < 2:
        return None
    i, j = rng.integers(0, n, RANSAC_ITERS), rng.integers(0, n, RANSAC_ITERS)
    dx, dy = x[j] - x[i], y[j] - y[i]
    ln = np.hypot(dx, dy)
    good = ln > 0.05
    if not good.any():
        return None
    i, dx, dy, ln = i[good], dx[good], dy[good], ln[good]
    dist = np.abs((-dy / ln)[:, None] * (x[None] - x[i][:, None]) + (dx / ln)[:, None] * (y[None] - y[i][:, None]))
    inl = dist[np.argmax((dist < RANSAC_TOL).sum(axis=1))] < RANSAC_TOL
    for _ in range(2):
        if inl.sum() < 2:
            return None
        p = np.array([x[inl].mean(), y[inl].mean()])
        _, _, vt = np.linalg.svd(np.stack([x[inl] - p[0], y[inl] - p[1]], axis=1), full_matrices=False)
        d = vt[0] if vt[0][0] >= 0 else -vt[0]
        inl = np.abs(-d[1] * (x - p[0]) + d[0] * (y - p[1])) < RANSAC_TOL
    return p, d, inl


def ramp(v, lo, hi):
    return float(np.clip((v - lo) / (hi - lo), 0.0, 1.0))


def no_line(flag):
    return dict(roll=math.nan, pitch=math.nan, conf_roll=0.0, conf_pitch=0.0, n_cols=0,
                inlier_frac=0.0, contrast=0.0, span=0.0, resid_px=math.nan, flag=flag)


def horizon(rgb, mask, k1, focal, rng):
    """Fit the horizon of one working-res frame. Returns a dict with roll, pitch (deg, NaN if no
    line), their confidences and diagnostics."""
    h, w = rgb.shape[:2]
    out = no_line("no_line")
    cols, rows, steps = column_edges(feature(rgb), mask)
    out["n_cols"] = len(cols)
    if len(cols) < MIN_INLIER_COLS * w:
        return out
    xu, yu = undistort((cols + 0.5 - w / 2) / (w / 2), (rows - h / 2) / (w / 2), k1)
    fit = fit_line(xu, yu, rng)
    if fit is None:
        return out
    p, d, inl = fit
    if inl.sum() < MIN_INLIER_COLS * w:
        return out
    nrm = np.array([-d[1], d[0]])                  # points down the image (d[0] >= 0)
    dist = float(nrm @ p)                          # signed distance of the line below the centre
    resid = np.abs(nrm[0] * (xu[inl] - p[0]) + nrm[1] * (yu[inl] - p[1]))
    frac = inl.sum() / len(cols)
    contrast = float(np.median(steps[inl]))
    span = float(cols[inl].max() - cols[inl].min() + 1) / w
    q = frac * ramp(contrast, CONTRAST_LO, CONTRAST_HI)
    out.update(roll=math.degrees(math.atan2(-d[1], d[0])), pitch=math.degrees(math.atan(dist / focal)),
               conf_roll=q * ramp(span, *SPAN_ROLL), conf_pitch=q * ramp(span, *SPAN_PITCH),
               inlier_frac=float(frac), contrast=contrast, span=span,
               resid_px=float(np.sqrt(np.mean(resid ** 2))) * 960.0, flag="ok")
    return out


def box_mean(a, r):
    """Mean over a (2r+1)^2 box, edges padded by repetition."""
    k = 2 * r + 1
    c = np.pad(a.astype(np.float64), ((r + 1, r), (r + 1, r)), mode="edge").cumsum(0).cumsum(1)
    return (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)


def is_picture(rgb):
    """False for the receiver's blue no-signal screen and for black frames."""
    blue = rgb[..., 2].mean() > 200 and rgb[..., 0].mean() < 30
    return not blue and float((rgb @ LUMA).mean()) > 12


def static_mask(frames):
    """Camera-fixed pixels (OSD, airframe parts, receiver border). Moving scenery blurs out in the
    mean of many frames; whatever stays sharp there is fixed to the camera. Thin specks are
    dropped, blobs are kept and grown by MASK_GROW."""
    mean = np.mean([f @ LUMA for f in frames], axis=0)
    sharp = np.abs(mean - box_mean(mean, 4)) > MASK_SHARP
    dense = box_mean(sharp, 2) > MASK_DENSE
    return box_mean(dense, MASK_GROW) > 0


# ---------------------------------------------------------------- yaw

STRIP_AZS = np.arange(-STRIP_AZ, STRIP_AZ + 1e-9, STRIP_RES)
STRIP_ELS = np.arange(STRIP_EL[1], STRIP_EL[0] - 1e-9, -STRIP_RES)   # top row = highest elevation
WINDOW = np.outer(np.hanning(len(STRIP_ELS)), np.hanning(len(STRIP_AZS)))


def strip(luma, mask, roll, pitch, k1, focal):
    """Resample the band around the horizon onto an azimuth x elevation grid in the camera's level
    frame. A heading change then moves distant scenery sideways only. Returns (windowed strip ready
    for phase correlation, fraction of the grid with valid pixels)."""
    h, w = luma.shape
    col, row = project(STRIP_AZS[None, :], STRIP_ELS[:, None], roll, pitch, k1, focal, w, h)
    x, y = col - 0.5, row - 0.5
    ok = np.isfinite(x) & (x >= 0) & (x <= w - 2) & (y >= BORDER_TOP) & (y <= h - BORDER_BOTTOM - 2)
    x, y = np.where(ok, x, 0), np.where(ok, y, 0)
    x0, y0 = x.astype(int), y.astype(int)
    fx, fy = x - x0, y - y0
    v = (luma[y0, x0] * (1 - fx) * (1 - fy) + luma[y0, x0 + 1] * fx * (1 - fy)
         + luma[y0 + 1, x0] * (1 - fx) * fy + luma[y0 + 1, x0 + 1] * fx * fy)
    ok &= ~mask[y0, x0] & ~mask[y0 + 1, x0 + 1]
    if ok.sum() < 100:
        return None, float(ok.mean())
    taper = box_mean(ok, 3) * ok                      # soft edges where the grid leaves the picture
    return (v - v[ok].mean()) * taper * WINDOW, float(ok.mean())


def phase_shift(a, b):
    """Phase correlation. Returns (rows, cols, peak, uniqueness) with b(x) ~ a(x - shift), sub-sample;
    uniqueness = peak / highest value outside +-PEAK_EXCL samples of it (near 1 = ambiguous)."""
    r = np.fft.fft2(b) * np.conj(np.fft.fft2(a))
    c = np.fft.ifft2(r / (np.abs(r) + 1e-9)).real
    i, j = np.unravel_index(np.argmax(c), c.shape)
    out = []
    for k, n, line in ((i, c.shape[0], c[:, j]), (j, c.shape[1], c[i, :])):
        lo, mid, hi = line[(k - 1) % n], line[k], line[(k + 1) % n]
        den = lo - 2 * mid + hi
        s = k + (0.5 * (lo - hi) / den if den < 0 else 0.0)
        out.append(s - n if s > n / 2 else s)
    rest = np.roll(np.roll(c, -i + PEAK_EXCL, 0), -j + PEAK_EXCL, 1)
    rest[:2 * PEAK_EXCL + 1, :2 * PEAK_EXCL + 1] = -np.inf
    return out[0], out[1], float(c[i, j]), float(c[i, j] / max(rest.max(), 1e-9))


# ---------------------------------------------------------------- tracking

def gate(rows):
    """Reject roll/pitch jumps of more than JUMP_DEG against the median of up to GATE_NEIGH usable
    neighbours each side, unless those neighbours agree with the jump. Rejected rows keep their
    values but get 10 % of their confidence and the flag 'jump'."""
    usable = [i for i, r in enumerate(rows) if max(r["conf_roll"], r["conf_pitch"]) >= 0.2]
    pos = {i: n for n, i in enumerate(usable)}
    hits = []
    for i in usable:
        n = pos[i]
        neigh = usable[max(0, n - GATE_NEIGH):n] + usable[n + 1:n + 1 + GATE_NEIGH]
        neigh = [j for j in neigh if abs(j - i) <= 2 * GATE_NEIGH]
        if len(neigh) < 2:
            continue
        for key in ("roll", "pitch"):
            if abs(rows[i][key] - np.median([rows[j][key] for j in neigh])) > JUMP_DEG:
                hits.append(i)
                break
    for i in hits:
        rows[i]["conf_roll"] *= 0.1
        rows[i]["conf_pitch"] *= 0.1
        rows[i]["flag"] = "jump"
    return rows


def track(dec, k1, focal):
    """Every frame of the decoder -> list of row dicts (COLUMNS)."""
    picks = np.unique(np.linspace(1, dec.n, min(dec.n, MASK_FRAMES)).round().astype(int))
    mask = static_mask([f for f in (dec.grab(int(i)) for i in picks) if is_picture(f)])
    rows, prev, last = [], None, None      # last = (row index, strip) of the latest distinct frame
    for i in range(1, dec.n + 1):
        rgb = dec.grab(i)
        dup = prev is not None and float(np.abs(rgb - prev).mean()) < DUP_DIFF
        prev = rgb
        r = horizon(rgb, mask, k1, focal, np.random.default_rng(i)) if is_picture(rgb) else no_line("no_picture")
        r.update(frame=i, time_s=(i - 1) / dec.fps, dup=int(dup), yaw_rate=math.nan, conf_yaw=0.0,
                 yaw_peak=math.nan, yaw_unique=math.nan, yaw_del=math.nan, pair=None)
        if dup:
            r["flag"] = "dup" if r["flag"] == "ok" else r["flag"]
            rows.append(r)
            continue
        cur = None
        if min(r["conf_roll"], r["conf_pitch"]) >= CONF_OK:
            cur, valid = strip(rgb @ LUMA, mask, r["roll"], r["pitch"], k1, focal)
        if cur is not None and last is not None and i - rows[last[0]]["frame"] <= 2:
            dr, dc, peak, unique = phase_shift(last[1], cur)
            steps = i - rows[last[0]]["frame"]
            r.update(yaw_rate=-dc * STRIP_RES * dec.fps / steps, yaw_peak=peak, yaw_unique=unique,
                     yaw_del=dr * -STRIP_RES, pair=last[0])
            plausible = abs(dc * STRIP_RES) <= MAX_SHIFT[0] * steps and abs(dr * STRIP_RES) <= MAX_SHIFT[1]
            r["conf_yaw"] = (plausible * ramp(peak, PEAK_LO, PEAK_HI) * ramp(unique, UNIQUE_LO, UNIQUE_HI)
                             * ramp(valid, 0.3, 0.6))
        last = (len(rows), cur) if cur is not None else None
        rows.append(r)
    rows = gate(rows)
    for r in rows:            # yaw needs both of its frames' horizons
        if r["pair"] is not None:
            p = rows[r["pair"]]
            r["conf_yaw"] *= min(1.0, min(r["conf_roll"], r["conf_pitch"], p["conf_roll"], p["conf_pitch"]) / CONF_OK)
    return rows, mask


def write_csv(path, rows):
    def fmt(v, nd):
        return "" if v is None or (isinstance(v, float) and math.isnan(v)) else f"{v:.{nd}f}"
    lines = [",".join(COLUMNS)]
    for r in rows:
        lines.append(",".join([str(r["frame"]), fmt(r["time_s"], 4), fmt(r["roll"], 3), fmt(r["pitch"], 3),
                               fmt(r["yaw_rate"], 2), fmt(r["conf_roll"], 3), fmt(r["conf_pitch"], 3),
                               fmt(r["conf_yaw"], 3), str(r["dup"]), str(r["n_cols"]), fmt(r["inlier_frac"], 3),
                               fmt(r["contrast"], 1), fmt(r["span"], 3), fmt(r["resid_px"], 2),
                               fmt(r["yaw_peak"], 3), fmt(r["yaw_unique"], 2), fmt(r["yaw_del"], 3), r["flag"]]))
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


# ---------------------------------------------------------------- decoding and images

class Decoder:
    """Frames of a movie file, or of a folder of PNGs, through the sequencer as in extract_frames.py."""

    def __init__(self, source, tmp, fps=None):
        scene = bpy.context.scene
        scene.view_settings.view_transform = "Standard"   # no colour shift of the footage
        scene.view_settings.look = "None"
        scene.render.dither_intensity = 0.0
        scene.render.image_settings.file_format = "BMP"
        scene.render.image_settings.color_mode = "RGB"
        scene.sequence_editor_create()
        strips = scene.sequence_editor.strips
        for s in list(strips):
            strips.remove(s)
        if source.is_dir():
            files = sorted(source.glob("*.png"))
            img = bpy.data.images.load(str(files[0]))
            (w, h), n = tuple(img.size), len(files)
            bpy.data.images.remove(img)
            strip = strips.new_image("src", str(files[0]), 1, 1)
            for f in files[1:]:
                strip.elements.append(f.name)
        else:
            mc = bpy.data.movieclips.load(str(source))
            (w, h), fps, n = tuple(mc.size), mc.fps, mc.frame_duration
            bpy.data.movieclips.remove(mc)
            strip = strips.new_movie("src", str(source), 1, 1)
        if not fps or n <= 0:
            raise RuntimeError(f"could not decode the source (fps={fps}, frames={n})")
        scene.render.fps = round(fps)
        scene.render.fps_base = round(fps) / fps
        scene.render.resolution_x, scene.render.resolution_y = w, h
        scene.frame_end = n
        strip.transform.filter = "BOX"                    # area-average the downscale
        self.scene, self.tmp, self.fps, self.n, self.size = scene, tmp, fps, n, (w, h)

    def grab(self, frame, pct=WORK_PCT):
        """Frame (1-based) as (h, w, 3) float32 in 0..255, top row first."""
        self.scene.frame_current = frame
        self.scene.render.resolution_percentage = pct
        self.scene.render.filepath = str(self.tmp)
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(str(self.tmp))
        w, h = img.size
        px = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        bpy.data.images.remove(img)
        return np.round(px.reshape(h, w, 4)[::-1, :, :3] * 255.0)

    def close(self):
        self.tmp.unlink(missing_ok=True)


def save_png(rgb, path):
    h, w = rgb.shape[:2]
    img = bpy.data.images.new("out", w, h)
    px = np.ones((h, w, 4), dtype=np.float32)
    px[..., :3] = np.clip(np.round(rgb), 0, 255)[::-1] / 255.0
    img.pixels.foreach_set(px.ravel())
    img.file_format = "PNG"
    img.save(filepath=str(path))
    bpy.data.images.remove(img)


def draw_horizon(rgb, roll, pitch, k1, focal):
    """Overlay the fitted horizon (red) and the lines 1 deg above and below it (yellow)."""
    h, w = rgb.shape[:2]
    az = np.arange(-89.0, 89.0, 0.02)
    for el, colour in ((1.0, (255, 230, 0)), (-1.0, (255, 230, 0)), (0.0, (255, 0, 0))):
        col, row = project(az, np.full_like(az, el), roll, pitch, k1, focal, w, h)
        ok = np.isfinite(col) & (col >= 1) & (col < w - 1) & (row >= 1) & (row < h - 1)
        c, r = col[ok].astype(int), row[ok].astype(int)
        for dr in (-1, 0):
            for dc in (-1, 0):
                rgb[r + dr, c + dc] = colour
    return rgb


def write_overlays(dec, rows, folder, count, k1, focal):
    good = [r for r in rows if r["flag"] == "ok" and min(r["conf_roll"], r["conf_pitch"]) >= CONF_OK]
    picks = sorted(np.random.default_rng(1).choice(len(good), min(count, len(good)), replace=False))
    folder.mkdir(exist_ok=True)
    for k in picks:
        r = good[k]
        save_png(draw_horizon(dec.grab(r["frame"], 100), r["roll"], r["pitch"], k1, focal),
                 folder / f"{r['frame']:04d}.png")
    return [good[k]["frame"] for k in picks]


# ---------------------------------------------------------------- synthetic self-test

SYN_W, SYN_H, SYN_FPS, SYN_ALT = 1920, 1080, 30.0, 40.0
SYN_LINES = 288                  # one PAL field (video-feed.md P1)


def synth_plan():
    """(frame, kind, roll, pitch, heading) for 120 frames: attitude sweeps with a visible horizon,
    then ground only, sky only, blue no-signal and snow, then the horizon again."""
    plan = []
    for i in range(1, 121):
        roll = 30 * math.sin(2 * math.pi * i / 45)
        pitch = -7 + 15 * math.sin(2 * math.pi * i / 70 + 1)
        heading = 40 * math.sin(2 * math.pi * i / 60)
        kind = "scene"
        if 91 <= i <= 98:
            kind, pitch = "ground", -75.0
        elif 99 <= i <= 106:
            kind, pitch = "sky", 60.0
        elif 107 <= i <= 110:
            kind = "blue"
        elif 111 <= i <= 114:
            kind = "snow"
        plan.append((i, kind, roll, pitch, heading))
    return plan


def synth_world(rng):
    """World-fixed skyline (gentle zero-mean undulation plus sparse trees that stick up) and
    non-repeating textures: tree-line brightness along azimuth, field patches over azimuth x distance."""
    grid = np.arange(0.0, 360.0, 0.01)
    a = np.radians(grid)
    sky = sum(amp * np.sin(k * a + rng.uniform(0, 6.3)) for amp, k in ((0.12, 5), (0.08, 13), (0.05, 31)))
    for c in np.arange(0, 360, 7.0):
        if rng.random() < 0.3:
            hw, top = rng.uniform(0.3, 1.0), rng.uniform(0.3, 0.7)
            d = (grid - c - rng.uniform(0, 5) + 180) % 360 - 180
            sky = np.maximum(sky, top * np.clip(1 - (d / hw) ** 2, 0, None))
    tree = hblur(rng.normal(0, 1, (1, len(grid))), 7)[0]
    patch = box_mean(rng.normal(0, 1, (3600, 48)), 1)
    return dict(grid=grid, sky=sky, tree=tree / tree.std(), patch=patch / patch.std())


def line_rows(h):
    starts = np.round(np.arange(SYN_LINES) * h / SYN_LINES).astype(int)
    return starts, np.searchsorted(starts, np.arange(h), side="right") - 1


def hblur(a, r):
    c = np.pad(a, [(0, 0), (r + 1, r)] + [(0, 0)] * (a.ndim - 2), mode="edge").cumsum(axis=1)
    return (c[:, 2 * r + 1:] - c[:, :-2 * r - 1]) / (2 * r + 1)


def analog(img, rng):
    """Feed-like degradation (video-feed.md): field lines, soft sharpened lines, chroma smear, grain,
    sparkles, vignette."""
    h, w = img.shape[:2]
    img = img + 0.3 * (img - hblur(img.swapaxes(0, 1), 4).swapaxes(0, 1))   # weak vertical halo (P3)
    starts, of_row = line_rows(h)
    lines = np.add.reduceat(img, starts, axis=0) / np.diff(np.append(starts, h))[:, None, None]
    lines = hblur(lines, 2)
    lines = lines + 0.8 * (lines - hblur(lines, 4))                       # sharpening halo (P3)
    y = lines @ LUMA
    lines = y[..., None] + hblur(lines - y[..., None], 12)                # chroma smear (P4)
    lines = lines + hblur(rng.normal(0, 4.0, (SYN_LINES, w)), 1)[..., None]  # luma grain (N1)
    lines = lines + hblur(rng.normal(0, 3.0, (SYN_LINES, w, 3)), 4)       # chroma grain
    k = rng.integers(0, SYN_LINES * w, SYN_LINES * w // 3000)             # sparkles (N2)
    lines.reshape(-1, 3)[k, rng.integers(0, 3, len(k))] += rng.choice((-60.0, 60.0), len(k))
    out = lines[of_row]
    xd, yd = np.meshgrid((np.arange(w) + 0.5 - w / 2) / (w / 2), (np.arange(h) + 0.5 - h / 2) / (w / 2))
    return np.clip(out * (1 - 0.1 * (xd ** 2 + yd ** 2))[..., None], 0, 255)


def synth_scene(roll, pitch, heading, world):
    """Render sky, tree line and a striped field through the lens model at SYN_W x SYN_H."""
    w, h = SYN_W, SYN_H
    xd, yd = np.meshgrid((np.arange(w) + 0.5 - w / 2) / (w / 2), (np.arange(h) + 0.5 - h / 2) / (w / 2))
    x, y = undistort(xd, yd, K1)
    z = np.full_like(x, FOCAL)
    cr, sr = math.cos(math.radians(roll)), math.sin(math.radians(roll))
    x, y = x * cr - y * sr, x * sr + y * cr                      # undo roll, then pitch
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    wy, wz = y * cp - z * sp, y * sp + z * cp
    el = np.degrees(np.arctan2(-wy, np.hypot(x, wz)))
    az = (np.degrees(np.arctan2(x, wz)) + heading) % 360
    a = np.radians(az)
    sky_el = np.interp(az, world["grid"], world["sky"])
    tree_el = sky_el - 0.7 - 0.2 * np.sin(7 * a)
    cloud = 8 * np.sin(3 * a + np.radians(el) * 9) + 5 * np.sin(11 * a - np.radians(el) * 23)
    img = np.empty((h, w, 3))
    img[:] = (185.0 + 0.6 * np.clip(el, 0, 40) + cloud)[..., None] + np.array([0.0, 0.0, 6.0])
    trees = (el <= sky_el) & (el > tree_el)
    img[trees] = np.array([48.0, 60.0, 40.0]) + (14 * np.interp(az[trees], world["grid"], world["tree"]))[:, None]
    ground = el <= tree_el
    dist = SYN_ALT / np.tan(np.radians(np.clip(-el[ground], 0.05, None)))
    gx, gy = dist * np.sin(a[ground]), dist * np.cos(a[ground])
    straw = (np.sin(gx * 0.08 + 2 * np.sin(gy * 0.013)) > 0.55) * np.clip(120 / dist, 0, 1)
    col = np.array([95.0, 66.0, 38.0]) + straw[:, None] * np.array([105.0, 99.0, 32.0])
    col += (10 * np.sin(gx * 0.9) * np.sin(gy * 0.7) * np.clip(60 / dist, 0, 1))[:, None]
    cell = world["patch"][(az[ground] * 10).astype(int) % 3600, np.clip(np.log(dist / 10) * 7, 0, 47).astype(int)]
    col += (12 * cell)[:, None]                                  # field patches, visible at every distance
    haze = (1 - np.exp(-dist / 1500))[:, None]
    img[ground] = col * (1 - haze) + np.array([150.0, 140.0, 128.0]) * haze
    return img


def synth_clutter(img, rng):
    """Camera-fixed parts like the clip's: OSD glyphs, crosshair, two airframe rods, sometimes a blade."""
    h, w = img.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]

    def segment(p, q, half, colour):
        d = np.array(q, float) - p
        t = np.clip(((xx - p[0]) * d[0] + (yy - p[1]) * d[1]) / (d @ d), 0, 1)
        img[np.hypot(xx - p[0] - t * d[0], yy - p[1] - t * d[1]) < half] = colour

    segment((0, 690), (430, 628), 9, (215, 215, 220))
    segment((1460, 612), (1920, 700), 9, (215, 215, 220))
    glyphs = [(x, 45) for x in (45, 105, 165, 1575, 1650, 1720, 1790)]
    glyphs += [(x, y) for y in (760, 830, 900, 970) for x in range(40, 440, 68)]
    glyphs += [(x, 900) for x in range(580, 1420, 68)]
    for x, y in glyphs + [(942, 540), (982, 540)]:
        img[y:y + 44, x:x + 36] = 0.0
        img[y + 5:y + 39, x + 5:x + 31] = 235.0
        img[y + 16:y + 28, x + 12:x + 24] = 0.0
    if rng.random() < 0.5:                                       # a prop blade crossing the top-right
        y0 = rng.uniform(15, 230)
        segment((1440 + 60 * rng.random(), y0), (1920, y0 - 25), rng.uniform(3, 7), (40, 42, 48))
    return img


def synth_frame(kind, roll, pitch, heading, world, rng):
    h, w = SYN_H, SYN_W
    if kind == "blue":
        return np.clip(np.array([3.0, 5.0, 252.0]) + rng.normal(0, 1, (h, w, 3)), 0, 255)
    if kind == "snow":
        starts, of_row = line_rows(h)
        blobs = np.repeat(rng.random((SYN_LINES, w // 8)) > 0.75, 8, axis=1)[of_row] * 255.0
        return analog(np.repeat(blobs[..., None], 3, axis=2), rng)
    return analog(synth_clutter(synth_scene(roll, pitch, heading, world), rng), rng)


def evaluate(rows, plan, fps):
    """Errors against the synthetic truth. Returns (report lines, passed)."""
    byf = {r["frame"]: r for r in rows}
    out, passed = [], len(rows) == len(plan)
    out.append(f"rows reported: {len(rows)} of {len(plan)} frames")
    scene = [p for p in plan if p[1] == "scene"]
    er = np.array([byf[p[0]]["roll"] - p[2] for p in scene])
    ep = np.array([byf[p[0]]["pitch"] - p[3] for p in scene])
    hi = np.array([min(byf[p[0]]["conf_roll"], byf[p[0]]["conf_pitch"]) >= CONF_OK for p in scene])
    missing = int(np.isnan(er).sum())
    rms_r, rms_p = float(np.sqrt(np.nanmean(er ** 2))), float(np.sqrt(np.nanmean(ep ** 2)))
    passed &= missing == 0 and rms_r <= 0.5 and rms_p <= 0.7
    out.append(f"horizon frames: {len(scene)}, without a value: {missing}, high confidence: {int(hi.sum())}")
    out.append(f"roll error: RMS {rms_r:.3f} deg, mean {np.nanmean(er):+.3f}, max |{np.nanmax(np.abs(er)):.3f}| (limit RMS 0.5)")
    out.append(f"pitch error: RMS {rms_p:.3f} deg, mean {np.nanmean(ep):+.3f}, max |{np.nanmax(np.abs(ep)):.3f}| (limit RMS 0.7)")
    if hi.any():
        out.append(f"high-confidence only: roll RMS {np.sqrt(np.mean(er[hi] ** 2)):.3f}, "
                   f"pitch RMS {np.sqrt(np.mean(ep[hi] ** 2)):.3f}")
    truth_rate = {p[0]: (p[4] - q[4]) * fps for q, p in zip(plan, plan[1:])}
    ey = np.array([byf[f]["yaw_rate"] - t for f, t in truth_rate.items() if byf[f]["conf_yaw"] >= CONF_OK])
    if len(ey):
        out.append(f"yaw rate (conf >= {CONF_OK}): {len(ey)} frames, error RMS {np.sqrt(np.mean(ey ** 2)):.2f} deg/s, "
                   f"mean {ey.mean():+.2f}, max |{np.abs(ey).max():.2f}|; "
                   f"truth spans +-{max(map(abs, truth_rate.values())):.0f} deg/s")
    for kind in ("ground", "sky", "blue", "snow"):
        fr = [byf[p[0]] for p in plan if p[1] == kind]
        worst = max(max(r["conf_roll"], r["conf_pitch"], r["conf_yaw"]) for r in fr)
        passed &= worst < 0.2
        out.append(f"no horizon, {kind}: {len(fr)} frames, highest confidence {worst:.3f} (limit < 0.2)")
    return out, passed


def selftest(folder):
    frames = folder / "frames"
    frames.mkdir(parents=True, exist_ok=True)
    plan = synth_plan()
    rng = np.random.default_rng(7)
    world = synth_world(rng)
    t0 = time.time()
    for i, kind, roll, pitch, heading in plan:
        save_png(synth_frame(kind, roll, pitch, heading, world, rng), frames / f"{i:04d}.png")
    (folder / "truth.csv").write_text("frame,kind,roll_deg,pitch_deg,heading_deg\n" + "".join(
        f"{i},{k},{r:.4f},{p:.4f},{hd:.4f}\n" for i, k, r, p, hd in plan), encoding="utf-8")
    print(f"track_attitude: selftest rendered {len(plan)} frames in {time.time() - t0:.0f} s")
    ok = True
    for k1 in (K1, 0.30, 0.36):     # the tracker's k1, then a sensitivity check at other lens values
        dec = Decoder(frames, folder / "_track.bmp", fps=SYN_FPS)
        try:
            rows, _ = track(dec, k1, FOCAL)
        finally:
            dec.close()
        lines, passed = evaluate(rows, plan, SYN_FPS)
        verdict = ""
        if k1 == K1:
            write_csv(folder / "attitude.csv", rows)
            ok, verdict = passed, (": PASS" if passed else ": FAIL")
        print(f"track_attitude: selftest, tracker k1 {k1:.2f} on frames rendered with {K1:.2f}{verdict}")
        for line in lines:
            print("    " + line)
    return ok


# ---------------------------------------------------------------- main

def clip_path(ref, letter):
    """The clip file for a letter, from reference/_frames/index.md (never printed)."""
    for line in (ref / "_frames" / "index.md").read_text(encoding="utf-8").splitlines():
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) == 3 and cells[0] == letter and cells[1] == "clip":
            return ref / cells[2].strip("`")
    raise RuntimeError(f"letter {letter} is not a clip in the index")


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="track_attitude.py")
    p.add_argument("--letter", help="clip letter from reference/_frames/index.md")
    p.add_argument("--ref", default=str(Path(__file__).resolve().parents[2] / "reference"),
                   help="the reference folder (default: the repo's)")
    p.add_argument("--overlays", type=int, default=0, help="random high-confidence frames to overlay")
    p.add_argument("--k1", type=float, default=K1, help="lens coefficient, for sensitivity runs")
    p.add_argument("--selftest", help="scratch folder for the synthetic self-test")
    args = p.parse_args(argv)
    if not args.selftest and not args.letter:
        p.error("give --letter or --selftest")
    return args


def main():
    if bpy.app.version < MIN_BLENDER:
        raise RuntimeError(f"Blender {bpy.app.version_string} is too old; this tool needs 5.2 or newer")
    args = parse_args()
    if args.selftest:
        sys.exit(0 if selftest(Path(args.selftest)) else 1)
    t0 = time.time()
    ref = Path(args.ref)
    out = ref / "_frames" / args.letter
    if not out.is_dir():
        raise RuntimeError(f"no extractor output for {args.letter}; run extract_frames.py first")
    dec = Decoder(clip_path(ref, args.letter), out / "_track.bmp")
    try:
        rows, mask = track(dec, args.k1, FOCAL)
        name = "attitude.csv" if args.k1 == K1 else f"attitude_k1_{args.k1:.2f}.csv"
        write_csv(out / name, rows)
        picks = write_overlays(dec, rows, out / "overlays", args.overlays, args.k1, FOCAL) if args.overlays else []
    finally:
        dec.close()
    n = len(rows)
    flags = {f: sum(r["flag"] == f for r in rows) for f in ("ok", "dup", "no_picture", "no_line", "jump")}
    shares = ", ".join(f"{k} {sum(r['conf_' + k] >= CONF_OK for r in rows) / n:.1%}" for k in ("roll", "pitch", "yaw"))
    print(f"track_attitude: {args.letter} k1 {args.k1:.2f}: {n} frames in {time.time() - t0:.0f} s, "
          f"static mask {mask.mean():.1%} of the frame")
    print(f"track_attitude: confidence >= {CONF_OK}: {shares}; flags {flags}")
    if picks:
        print(f"track_attitude: overlays for frames {picks}")


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:
        traceback.print_exc()
        print(f"track_attitude: FAILED: {e}", file=sys.stderr)
        sys.exit(1)
