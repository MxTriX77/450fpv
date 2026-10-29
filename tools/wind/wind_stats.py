"""Wind-disturbance statistics from an attitude log (docs/reference-notes/wind.md sections 1-6).

Ported to plain Python (standard library only) from godot-final:tools/reference/wind_stats.py, a
Blender Python script (D-037, ported, never merged, D-027). Same constants, definitions and output
fields; the departures are listed under "Port" below.

    python tools/wind/wind_stats.py --letter P [--json out.json]
    python tools/wind/wind_stats.py --csv <log> [--segment 121-330] [--json out.json]
    python tools/wind/wind_stats.py --letter P --segment 121-330 --gauss 5
    python tools/wind/wind_stats.py --published --gauss 5
    python tools/wind/wind_stats.py --wind-log <seed1.csv> ... [--json out.json]

Input is reference/_frames/<letter>/attitude.csv from the tracker, or a simulated flight logged in
the same columns (frame, time_s, roll_deg, pitch_deg, yaw_rate_dps, conf_roll, conf_pitch,
conf_yaw; a simulation writes conf 1). The K section 8.6 targets are checked by running it on the
simulator's log. --wind-log takes the weather preset's own wind history instead (T0c): one CSV per
seed with time_s, wind_from_deg and prevailing_from_deg (the direction that seed drew).
wind_history_fixture.py builds such logs from the K section 8.3 starting model and checks T0c.

Definitions (wind.md section 1.4):
- Usable frame: roll and pitch confidence >= CONF (yaw: yaw confidence >= CONF), and not within
  LOSS_GUARD frames of a picture loss (flag no_picture), where frames can be torn. A run is a
  stretch of consecutive usable frames; nothing is interpolated across gaps.
- Residual: the angle minus its Gaussian-weighted (sigma TREND_SIGMA_S) local-linear trend inside
  the run; frames within one sigma of a run end are dropped. Its gain is 0.5 at RES_CORNER_HZ.
- Rate: slope of a least-squares line through 5 consecutive frames; acceleration: the matching
  5-point quadratic fit. Yaw is handled as the heading angle: the running sum of the tracked yaw
  rate inside a yaw run, held through single repeated frames (the recording shows the previous view).
- Spectra: Welch, Hann windows of WELCH_N frames, 50 % overlap, linear detrend per window,
  windows only inside runs.
- Events: frames where the |residual| of an axis exceeds EVENT_K standard deviations of that axis,
  merged across gaps of up to EVENT_GAP frames.
- Uncertainty: 16-84 % interval over BOOT block-bootstrap resamples of BLOCK-frame blocks
  (BLOCK_SEG inside and outside a segment; seed 0), and 1/sqrt(windows) scaling for spectra.
- Segment: inside and outside a frame range, the residual spread, kurtosis and the spread per 5 s
  block (steadiness), plus the thrust tilt and the roll-yaw coupling.
- Airframe (camera with 0 deg uptilt): thrust/weight to hold height 1/(cos pitch cos roll), the
  horizontal thrust's direction, body rates from ZYX kinematics, and the thrust tilt residual
  roll + k * heading with k = -sin(mean pitch) (wind.md section 5.5).

Port (where this differs from the Blender original, and why):
- Plain Python: numpy is not installed here, and the tools must run without Blender. Arrays are
  lists with NaN for "no value"; numpy's FFT, convolution, percentiles and correlations are
  re-implemented (radix-2 FFT, numpy's linear percentile). Sums use math.fsum / math.sumprod,
  which round less than numpy's pairwise sums: results agree to the last digits printed, not bit
  for bit.
- Random draws (bootstrap resamples, the Gaussian check's noise) come from Python's
  random.Random(seed) with the original's seeds, not numpy's PCG64: the intervals and the noise
  logs are statistically the same, not the same numbers.
- The Gaussian check builds each noise log at the next power of two and keeps its first frames
  (numpy's irfft takes any length; this FFT is radix-2). It is the same stationary process: the
  scale comes from the 2^17-frame unit, as in the original.
- The check returns a verdict (gauss_verdict): the self-check of TST-03 fails if a noise family
  passes T9 and T12 together. The original only printed the table and left the reading to wind.md.
- --published runs the Gaussian check from P's published numbers (wind.md sections 1.6, 2, 3.1,
  4.4; see clip_p.py) when reference/ is not on this machine; the band PSD is flat within each
  band of the published table.
- Arguments: plain argparse (no Blender "--" separator); main(argv) returns an exit code.
"""
import argparse
import cmath
import csv
import json
import math
import random
import sys
from itertools import accumulate
from pathlib import Path

CONF = 0.5
LOSS_GUARD = 2                      # frames next to a picture loss are not used (they can be torn)
TREND_SIGMA_S = 0.6                 # s
RES_CORNER_HZ = math.sqrt(math.log(2) / (2 * math.pi ** 2)) / TREND_SIGMA_S
WELCH_N = 128
BANDS = ((0.23, 0.5), (0.5, 1.0), (1.0, 2.0), (2.0, 4.0), (4.0, 8.0), (8.0, 15.0))
EVENT_K = 2.0
EVENT_GAP = 3
BLOCK = 60
BLOCK_SEG = 30                      # bootstrap block inside and outside a segment (the belt holds only 7 s)
BLOCK_S = 150                       # frames per block of the terrain comparison (5 s)
BOOT = 500
SHIFT_DEG, SHIFT_S, SIDE_S = 45.0, 30.0, 40.0   # wind history: a shift is >= 45 deg within 30 s; side kept over 40 s
PEAK_S = (60.0, 600.0)              # trial periods of the shift periodicity peak: 2 x SHIFT_S to the slowest T0c rate
MAX_LAG_S = 1.0
D1 = [v / 10.0 for v in (-2, -1, 0, 1, 2)]      # 5-point first derivative (per frame)
D2 = [v / 7.0 for v in (2, -1, -2, -1, 2)]      # 5-point second derivative (per frame^2)
NAN = math.nan

# The K section 8.6 bands the Gaussian check grades (T9a, T9b, T12; T10b is printed alongside).
T9A, T9B, T12_CV, T12_MIN, T10B = (2.2, 3.5), (3.3, 5.5), 0.32, 0.50, (0.35, 0.75)


# ---------------------------------------------------------------- plain-Python array helpers

def isf(v):
    return math.isfinite(v)


def fin(x):
    return [v for v in x if math.isfinite(v)]


def mean(v):
    return math.fsum(v) / len(v) if len(v) else NAN


def var(v, ddof=0):
    n = len(v)
    if n - ddof <= 0:
        return NAN
    m = math.fsum(v) / n
    return math.fsum((a - m) * (a - m) for a in v) / (n - ddof)


def std(v, ddof=0):
    s = var(v, ddof)
    return math.sqrt(s) if isf(s) else NAN


def nanmean(x):
    return mean(fin(x))


def nanstd(x):
    return std(fin(x))


def percentile(v, q):
    """numpy's default (linear) percentile of the finite values; NaN when there are none."""
    s = sorted(fin(v))
    if not s:
        return NAN
    pos = q / 100.0 * (len(s) - 1)
    lo = int(math.floor(pos))
    hi = min(lo + 1, len(s) - 1)
    return s[lo] + (s[hi] - s[lo]) * (pos - lo)


def pearson(x, y):
    n = len(x)
    if n < 2:
        return NAN
    mx, my = math.fsum(x) / n, math.fsum(y) / n
    dx, dy = [a - mx for a in x], [b - my for b in y]
    sxx, syy = math.sumprod(dx, dx), math.sumprod(dy, dy)
    if sxx <= 0 or syy <= 0:
        return NAN
    return math.sumprod(dx, dy) / math.sqrt(sxx * syy)


def both_finite(a, b):
    return [i for i, (u, v) in enumerate(zip(a, b)) if isf(u) and isf(v)]


def where(mask, a, other=NAN):
    return [v if m else other for m, v in zip(mask, a)]


def fft(a):
    """In-place-style iterative radix-2 FFT of a list of complex numbers (length a power of two)."""
    n = len(a)
    a = list(a)
    j = 0
    for i in range(1, n):
        bit = n >> 1
        while j & bit:
            j ^= bit
            bit >>= 1
        j |= bit
        if i < j:
            a[i], a[j] = a[j], a[i]
    tw = [cmath.exp(-2j * math.pi * k / n) for k in range(n // 2)]
    size = 2
    while size <= n:
        half, step = size // 2, n // size
        w = tw[::step][:half]
        for s in range(0, n, size):
            for k in range(half):
                u, v = a[s + k], a[s + k + half] * w[k]
                a[s + k], a[s + k + half] = u + v, u - v
        size *= 2
    return a


def rfft(x):
    return fft([complex(v) for v in x])[:len(x) // 2 + 1]


def irfft(spec, n):
    """numpy's irfft for an even n: the imaginary parts at DC and Nyquist are dropped."""
    full = [0j] * n
    full[0] = complex(spec[0].real)
    full[n // 2] = complex(spec[n // 2].real)
    for k in range(1, n // 2):
        full[k], full[n - k] = spec[k], spec[k].conjugate()
    return [v.real / n for v in fft([v.conjugate() for v in full])]


def rfftfreq(n, d):
    return [k / (n * d) for k in range(n // 2 + 1)]


def interp(x, xp, fp):
    """numpy's interp for increasing xp, clamped at both ends."""
    if x <= xp[0]:
        return fp[0]
    if x >= xp[-1]:
        return fp[-1]
    lo, hi = 0, len(xp) - 1
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if xp[mid] <= x:
            lo = mid
        else:
            hi = mid
    return fp[lo] + (fp[hi] - fp[lo]) * (x - xp[lo]) / (xp[hi] - xp[lo])


# ---------------------------------------------------------------- the original's functions

def load(path):
    rows = list(csv.DictReader(Path(path).open(encoding="utf-8")))
    num = lambda r, k: float(r[k]) if r.get(k, "") not in ("", None) else NAN
    cols = {k: [num(r, k) for r in rows] for k in
            ("frame", "time_s", "roll_deg", "pitch_deg", "yaw_rate_dps", "conf_roll", "conf_pitch", "conf_yaw",
             "edge_px")}
    cols["dup"] = [r.get("dup", "0") == "1" for r in rows]
    lost = [r.get("flag", "ok") == "no_picture" for r in rows]
    n = len(rows)
    near = [any(lost[max(0, i - LOSS_GUARD):i + LOSS_GUARD + 1]) for i in range(n)]   # torn onset/recovery frames
    for k in ("conf_roll", "conf_pitch", "conf_yaw"):
        cols[k] = where([not v for v in near], cols[k], 0.0)
    cols["guarded"] = sum(1 for a, b in zip(near, lost) if a and not b)
    cols["lost"] = lost
    return cols


def runs(ok):
    """[(start, stop)) index ranges of consecutive True."""
    out, start = [], None
    for i, v in enumerate(ok):
        if v and start is None:
            start = i
        elif not v and start is not None:
            out.append((start, i))
            start = None
    if start is not None:
        out.append((start, len(ok)))
    return out


def per_run(x, spans, fn, pad):
    """Apply fn to x inside each run; results land on the run's frames, NaN within pad of its ends."""
    out = [NAN] * len(x)
    for a, b in spans:
        if b - a > 2 * pad:
            out[a + pad:b - pad] = list(fn(x[a:b]))
    return out


def residual(x, spans, fps):
    """x minus a Gaussian-weighted local-linear trend fitted inside the run (same as the plain
    Gaussian smoother away from run ends, and unbiased by slopes near them). Frames within one
    sigma of a run end are dropped."""
    s = TREND_SIGMA_S * fps
    h = int(3 * s)
    t = list(range(-h, h + 1))
    w = [math.exp(-0.5 * (k / s) ** 2) for k in t]
    wt = [a * k for a, k in zip(w, t)]
    wtt = [a * k * k for a, k in zip(w, t)]
    s0_full, s2_full = math.fsum(w), math.fsum(wtt)
    pad = int(s)

    def detrend(v):
        n = len(v)
        out = []
        for i in range(pad, n - pad):
            lo, hi = max(0, i - h), min(n, i + h + 1)       # neighbours j = i + t inside the run
            ka, kb = lo - (i - h), hi - (i - h)
            seg = v[lo:hi]
            if ka == 0 and kb == len(w):                     # full window: s1 = 0, the trend is the Gaussian mean
                trend = math.sumprod(w, seg) / s0_full
            else:
                ws, wts, wtts = w[ka:kb], wt[ka:kb], wtt[ka:kb]
                s0, s1, s2 = math.fsum(ws), math.fsum(wts), math.fsum(wtts)
                m0, m1 = math.sumprod(ws, seg), math.sumprod(wts, seg)
                trend = (s2 * m0 - s1 * m1) / (s0 * s2 - s1 * s1)
            out.append(v[i] - trend)
        return out
    return per_run(x, spans, detrend, pad)


def deriv(x, spans, fps, kern, power):
    scale = fps ** power
    return per_run(x, spans, lambda v: [math.sumprod(kern, v[i:i + 5]) * scale for i in range(len(v) - 4)], 2)


def kurtosis(v):
    v = list(v)
    if len(v) < 2:
        return NAN
    m = math.fsum(v) / len(v)
    d2 = [(a - m) ** 2 for a in v]
    s2 = math.fsum(d2) / len(v)
    return math.fsum(a * a for a in d2) / len(v) / (s2 * s2) if s2 > 0 else NAN


def boot(values_by_frame, fn, seed=0, block=BLOCK):
    """16-84 % interval of fn over block-bootstrap resamples of the frames that hold values."""
    multi = isinstance(values_by_frame, tuple)
    first = values_by_frame[0] if multi else values_by_frame
    idx = [i for i, v in enumerate(first) if isf(v)]
    blocks = [idx[i:i + block] for i in range(0, len(idx), block)]
    if len(blocks) < 3 or not BOOT:
        return NAN, NAN
    rng = random.Random(seed)
    nb, stats = len(blocks), []
    for _ in range(BOOT):
        pick = [i for j in (rng.randrange(nb) for _ in range(nb)) for i in blocks[j]]
        if multi:
            stats.append(fn(*([v[i] for i in pick] for v in values_by_frame)))
        else:
            stats.append(fn([values_by_frame[i] for i in pick]))
    return percentile(stats, 16), percentile(stats, 84)


def describe(x, name, unit):
    v = fin(x)
    if len(v) < 2 * BLOCK:
        return dict(axis=name, unit=unit, n=len(v), **{k: NAN for k in (
            "mean", "std", "p5", "p95", "p95_abs", "max_abs", "min", "max", "kurtosis")},
            mean_ci=(NAN,) * 2, std_ci=(NAN,) * 2, p95_abs_ci=(NAN,) * 2, kurtosis_ci=(NAN,) * 2)
    absv = [abs(a) for a in v]
    return dict(axis=name, unit=unit, n=len(v), mean=mean(v), mean_ci=boot(x, mean),
                std=std(v), std_ci=boot(x, std), p5=percentile(v, 5), p95=percentile(v, 95),
                p95_abs=percentile(absv, 95), p95_abs_ci=boot(x, lambda a: percentile([abs(b) for b in a], 95)),
                max_abs=max(absv), min=min(v), max=max(v), kurtosis=kurtosis(v), kurtosis_ci=boot(x, kurtosis))


def hanning(n):
    return [0.5 - 0.5 * math.cos(2 * math.pi * k / (n - 1)) for k in range(n)]


def detrend_linear(seg):
    n = len(seg)
    tm = (n - 1) / 2.0
    dt = [k - tm for k in range(n)]
    ym = math.fsum(seg) / n
    slope = math.sumprod(dt, seg) / math.sumprod(dt, dt)
    return [y - ym - slope * d for y, d in zip(seg, dt)]


def welch(x, spans, fps):
    win = hanning(WELCH_N)
    norm = fps * math.fsum(a * a for a in win)
    specs, frames = [], []
    for a, b in spans:
        for s in range(a, b - WELCH_N + 1, WELCH_N // 2):
            seg = detrend_linear(x[s:s + WELCH_N])
            p = [abs(c) ** 2 / norm for c in rfft([u * v for u, v in zip(seg, win)])]
            for i in range(1, len(p) - 1):
                p[i] *= 2
            specs.append(p)
            frames.append((s, s + WELCH_N))
    return rfftfreq(WELCH_N, 1 / fps), specs, frames


def band_table(f, specs):
    """RMS (unit) and share of 0.23-15 Hz variance per band; bootstrap over windows."""
    df = f[1] - f[0]
    sel = [[i for i, fi in enumerate(f) if lo <= fi < hi] for lo, hi in BANDS]

    def avg(ps):
        return [math.fsum(col) / len(ps) for col in zip(*ps)]

    def rms(ps):
        m = avg(ps)
        return [math.sqrt(math.fsum(m[i] for i in s) * df) for s in sel]
    base = rms(specs)
    rng = random.Random(0)
    bs = [rms([specs[rng.randrange(len(specs))] for _ in range(len(specs))]) for _ in range(BOOT)]
    if BOOT:
        lo = [percentile([b[j] for b in bs], 16) for j in range(len(BANDS))]
        hi = [percentile([b[j] for b in bs], 84) for j in range(len(BANDS))]
    else:
        lo = hi = [NAN] * len(BANDS)
    tot = math.fsum(r * r for r in base)
    m = avg(specs)
    band = [i for i, fi in enumerate(f) if BANDS[0][0] <= fi < BANDS[-1][1]]
    peak = max(band, key=lambda i: m[i])
    return dict(bands=[dict(band=b, rms=r, rms_ci=(l, h), share=r * r / tot)
                       for b, r, l, h in zip(BANDS, base, lo, hi)],
                peak_hz=f[peak], windows=len(specs), psd_rel_err=1 / math.sqrt(len(specs)))


def events(x, fps, over=None):
    """Excursions beyond EVENT_K sigma of the residual x (or of a given boolean 'over' series, for
    combined axes). Durations, rise and decay times, gaps between successive onsets."""
    v = [isf(a) for a in x]
    thr = EVENT_K * nanstd(x) if over is None else EVENT_K      # combined: in sigmas of each axis
    if over is None:
        over = [ok and abs(a) > thr for ok, a in zip(v, x)]
    ev = []
    for a, b in runs(over):
        if ev and a - ev[-1][1] <= EVENT_GAP and all(v[ev[-1][1]:a]):
            ev[-1] = (ev[-1][0], b)
        else:
            ev.append((a, b))
    out = []
    for a, b in ev:
        p = max(range(a, b), key=lambda i: abs(x[i]))
        peak = abs(x[p])
        dec = next((j for j in range(p, len(x)) if not v[j] or abs(x[j]) < peak / math.e), None)
        ris = next((j for j in range(p, -1, -1) if not v[j] or abs(x[j]) < peak / math.e), None)
        out.append(dict(start=a, stop=b, peak=p, size=x[p], dur_s=(b - a) / fps,
                        decay_s=(dec - p) / fps if dec is not None and v[dec] else NAN,
                        rise_s=(p - ris) / fps if ris is not None and v[ris] else NAN))
    minutes = max(sum(v) / fps / 60, 1e-9)
    onsets = [e["start"] for e in out]
    gaps = [(b - a) / fps for a, b in zip(onsets, onsets[1:]) if all(v[a:b])]
    return dict(threshold=thr, count=len(out), minutes=minutes, per_min=len(out) / minutes,
                per_min_err=math.sqrt(max(len(out), 1)) / minutes, list=out, gaps_s=gaps)


def summarize_events(e):
    q = lambda key: [ev[key] for ev in e["list"] if isf(ev[key])]
    pct = lambda a: [percentile(a, p) for p in (25, 50, 75, 90)] if len(a) else []
    g = e["gaps_s"]
    return dict(threshold=e["threshold"], count=e["count"], minutes=e["minutes"], per_min=e["per_min"],
                per_min_err=e["per_min_err"], dur_q=pct(q("dur_s")), dur_max=max(q("dur_s"), default=NAN),
                decay_q=pct(q("decay_s")), rise_q=pct(q("rise_s")), n_gaps=len(g), gaps_s=list(g),
                gap_q=pct(g), gap_mean=mean(g) if len(g) else NAN,
                gap_cv=std(g) / mean(g) if len(g) > 1 else NAN)


def corr(a, b, fps, block=BLOCK):
    """Zero-lag Pearson r with bootstrap interval, and the strongest |r| within +-MAX_LAG_S."""
    idx = both_finite(a, b)
    if len(idx) < 2 * BLOCK:
        return dict(r=NAN, r_ci=(NAN,) * 2, n=len(idx), best_r=NAN, best_lag_s=NAN)
    r0 = pearson([a[i] for i in idx], [b[i] for i in idx])
    keep = set(idx)
    ci = boot(([v if i in keep else NAN for i, v in enumerate(a)], [v if i in keep else NAN for i, v in enumerate(b)]),
              pearson, block=block)
    best = (0.0, 0)
    n = len(a)
    for lag in range(-int(MAX_LAG_S * fps), int(MAX_LAG_S * fps) + 1):
        x, y = (a[:n - lag], b[lag:]) if lag >= 0 else (a[-lag:], b[:n + lag])
        m = both_finite(x, y)
        if len(m) > 100:
            r = pearson([x[i] for i in m], [y[i] for i in m])
            if abs(r) > abs(best[0]):
                best = (r, lag)
    return dict(r=r0, r_ci=ci, n=len(idx), best_r=best[0], best_lag_s=best[1] / fps)


def split(a, e):
    """Share of the variance of e carried by a: cov(a, e) / var(e), over frames that hold both."""
    m = both_finite(a, e)
    if len(m) <= 2:
        return NAN
    x, y = [a[i] for i in m], [e[i] for i in m]
    mx, my = mean(x), mean(y)
    return math.fsum((u - mx) * (v - my) for u, v in zip(x, y)) / (len(m) - 1) / var(y, ddof=1)


def acf_time(x, fps):
    """Lag (s) where the residual's autocorrelation first falls below 1/e."""
    for lag in range(1, int(3 * fps)):
        a, b = x[:-lag], x[lag:]
        m = both_finite(a, b)
        if len(m) < 2 * BLOCK:
            return NAN
        if pearson([a[i] for i in m], [b[i] for i in m]) < 1 / math.e:
            return lag / fps
    return NAN


def analyse(c, segment=None):
    t = c["time_s"]
    fps = (len(t) - 1) / float(t[-1] - t[0])
    n = len(c["frame"])
    ok = [cr >= CONF and cp >= CONF and isf(r) and isf(p)
          for cr, cp, r, p in zip(c["conf_roll"], c["conf_pitch"], c["roll_deg"], c["pitch_deg"])]
    oky = [cy >= CONF and isf(y) for cy, y in zip(c["conf_yaw"], c["yaw_rate_dps"])]
    # A single repeated frame shows the previous view: its heading holds, and the tracker's rate for
    # the next frame already spans both frame steps. So a yaw run continues through it (np.roll wraps).
    dup = c["dup"]
    single = [dup[i] and not dup[i - 1] and not dup[(i + 1) % n] for i in range(n)]
    okh = [oky[i] or (single[i] and oky[i - 1] and oky[(i + 1) % n]) for i in range(n)]
    steps = [1 + single[i - 1] for i in range(n)]
    yr = c["yaw_rate_dps"]
    inc = [0.0 if single[i] else (yr[i] if isf(yr[i]) else 0.0) * steps[i] / fps for i in range(n)]
    sp, sph = runs(ok), runs(okh)
    axes = {"roll": (where(ok, c["roll_deg"]), sp),
            "pitch": (where(ok, c["pitch_deg"]), sp),
            "yaw": (per_run(where(okh, inc), sph, accumulate, 0), sph)}    # heading, deg
    res = {k: residual(x, s, fps) for k, (x, s) in axes.items()}
    rate = {k: deriv(x, s, fps, D1, 1) for k, (x, s) in axes.items()}
    acc = {k: deriv(x, s, fps, D2, 2) for k, (x, s) in axes.items()}
    frame = lambda i: int(c["frame"][i])
    out = dict(fps=fps, frames=n, first=frame(0), last=frame(-1), guarded=c["guarded"],
               usable=sum(ok), usable_yaw=sum(okh),
               runs=[(frame(a), frame(b - 1)) for a, b in sp],
               runs_yaw=[(frame(a), frame(b - 1)) for a, b in sph if b - a >= 5],
               residual_frames={k: len(fin(v)) for k, v in res.items()}, res_corner_hz=RES_CORNER_HZ)
    out["angle"] = [describe(axes["roll"][0], "roll", "deg"), describe(axes["pitch"][0], "pitch", "deg"),
                    describe(where(oky, c["yaw_rate_dps"]), "yaw_rate_tracked", "deg/s")]
    out["residual"] = [describe(res[k], k, "deg") for k in axes]
    out["rate"] = [describe(rate[k], k, "deg/s") for k in axes]
    out["accel"] = [describe(acc[k], k, "deg/s^2") for k in axes]
    out["edge"] = describe(where(ok, c["edge_px"]), "horizon_edge_width", "px")
    out["spectra"] = {}
    for k, (x, s) in axes.items():
        f, specs, frames = welch(x, s, fps)
        if len(specs):
            out["spectra"][k] = band_table(f, specs) | {
                "window_frames": [(frame(a), frame(b - 1)) for a, b in frames],
                "psd": [(fi, math.fsum(col) / len(specs)) for fi, col in zip(f, zip(*specs))]}
    ev = {k: events(res[k], fps) for k in axes}
    sd = {k: nanstd(res[k]) for k in ("roll", "pitch")}
    over = [False] * n
    for k in ("roll", "pitch"):     # either axis beyond its own threshold; sizes in roll/pitch sigmas
        over = [o or (isf(v) and abs(v) > EVENT_K * sd[k]) for o, v in zip(over, res[k])]
    norm = []
    for r, p in zip(res["roll"], res["pitch"]):   # np.fmax: the finite one wins over NaN
        a, b = abs(r) / sd["roll"], abs(p) / sd["pitch"]
        norm.append(b if not isf(a) else a if not isf(b) else max(a, b))
    ev["roll_or_pitch"] = events(norm, fps, [o and isf(v) for o, v in zip(over, norm)])
    out["events"] = {k: summarize_events(e) | {"frames": [(frame(x["start"]), frame(x["stop"] - 1)) for x in e["list"]]}
                     for k, e in ev.items()}
    out["corr"] = {f"{a}~{b} {kind}": corr(src[a], src[b], fps) for kind, src in (("residual", res), ("rate", rate))
                   for a, b in (("roll", "pitch"), ("roll", "yaw"), ("pitch", "yaw"))}
    out["acf_s"] = {k: acf_time(res[k], fps) for k in axes}
    # Airframe view, valid when the camera sits on the airframe with 0 deg uptilt (pilot, Q1). ZYX kinematics give
    # the body rates that rate-mode sticks command (products of rates, < 1 deg/s^2, left out of the accelerations).
    # tilt = roll residual + k * heading residual, k = -sin(mean pitch), is the sideways tilt of the thrust vector:
    # yaw about the body axis leaves it unchanged (wind.md section 5.5).
    th = [math.radians(v) for v in axes["pitch"][0]]
    ph = [math.radians(v) for v in axes["roll"][0]]
    k = -math.sin(math.radians(nanmean(axes["pitch"][0])))
    tilt = [r + k * y for r, y in zip(res["roll"], res["yaw"])]
    body = lambda d: {"p": [r - y * math.sin(a) for r, y, a in zip(d["roll"], d["yaw"], th)],
                      "r": [y * math.cos(a) * math.cos(b) - p * math.sin(b)
                            for y, p, a, b in zip(d["yaw"], d["pitch"], th, ph)]}
    brate, bacc = body(rate), body(acc)
    lateral = [math.tan(-b) / math.cos(a) for a, b in zip(th, ph)]
    out["airframe"] = dict(
        k=k, load_factor=describe([1 / (math.cos(a) * math.cos(b)) for a, b in zip(th, ph)], "thrust/weight", "1"),
        lateral=describe(lateral, "lateral thrust/weight", "1"),
        thrust_dir=describe([math.degrees(math.atan2(l, math.tan(-a))) for l, a in zip(lateral, th)],
                            "thrust left of nose", "deg"),
        tilt=describe(tilt, "thrust tilt residual", "deg"), tilt_split=split(res["roll"], tilt),
        body_rate=[describe(v, a, "deg/s") for a, v in brate.items()],
        body_accel=[describe(v, a, "deg/s^2") for a, v in bacc.items()], corr_pr=corr(brate["p"], brate["r"], fps))
    blocks = []                      # residual spread per 5 s of flight, for comparing terrain
    for s in range(0, n, BLOCK_S):
        seg = {k: res[k][s:s + BLOCK_S] for k in ("roll", "pitch")}
        if len(fin(seg["roll"])) >= BLOCK_S * 2 // 3:
            blocks.append(dict(frames=(frame(s), frame(min(n, s + BLOCK_S) - 1)),
                               **{k: nanstd(v) for k, v in seg.items()}))
    out["blocks"] = blocks
    losses = []                      # picture losses, and how busy the attitude was in the second before
    for a, b in runs(c["lost"]):
        pre = slice(max(0, a - LOSS_GUARD - int(fps)), max(0, a - LOSS_GUARD))
        losses.append(dict(frames=(frame(a), frame(b - 1)), dur_s=(b - a) / fps, **{
            f"{k}_rate_max_pre": max((abs(v) for v in fin(rate[k][pre])), default=NAN) for k in axes}))
    out["losses"] = losses
    if segment:                      # the same statistics inside and outside a frame range (e.g. terrain)
        inside = [segment[0] <= f <= segment[1] for f in c["frame"]]
        out["segment"] = {}
        for name, m in (("inside", inside), ("outside", [not v for v in inside])):
            d = {}
            for k in axes:
                v = where(m, res[k])
                nv = len(fin(v))
                minutes = nv / fps / 60
                count = sum(bool(m[x["start"]]) for x in ev[k]["list"])
                sdv = nanstd(v)
                # steadiness: residual spread per 5 s block of this side's frames (blocks with >= 2/3 of their frames)
                bl = [dict(frames=(frame(s), frame(min(n, s + BLOCK_S) - 1)), std=nanstd(v[s:s + BLOCK_S]))
                      for s in range(0, n, BLOCK_S) if len(fin(v[s:s + BLOCK_S])) >= BLOCK_S * 2 // 3]
                bs = [b["std"] for b in bl]
                d[k] = dict(res_std=sdv, res_std_ci=boot(v, std, block=BLOCK_SEG), frames=nv,
                            kurtosis=kurtosis(fin(v)), kurtosis_ci=boot(v, kurtosis, block=BLOCK_SEG),
                            blocks=bl, block_cv=std(bs) / mean(bs) if len(bs) > 1 else NAN,
                            block_min=min(bs) / sdv if len(bs) > 1 else NAN,
                            events=count, per_min=count / minutes if minutes else NAN,
                            per_min_err=math.sqrt(max(count, 1)) / minutes if minutes else NAN)
            v = where(m, tilt)
            d["tilt"] = dict(res_std=nanstd(v), res_std_ci=boot(v, std, block=BLOCK_SEG),
                             frames=len(fin(v)), split=split(where(m, res["roll"]), v))
            d["corr"] = {f"roll~yaw {kind}": corr(where(m, src["roll"]), src["yaw"], fps, block=BLOCK_SEG)
                         for kind, src in (("residual", res), ("rate", rate))}
            out["segment"][name] = d
        out["segment"]["range"] = segment
    return out


def fmt_ci(ci):
    return f"[{ci[0]:.3g}, {ci[1]:.3g}]"


def r2(a):
    return [round(v, 2) for v in a]


def report(o):
    print(f"wind_stats: {o['frames']} frames ({o['first']}-{o['last']}) at {o['fps']:.3f} fps; usable roll/pitch "
          f"{o['usable']} ({o['usable'] / o['frames']:.1%}), yaw {o['usable_yaw']} ({o['usable_yaw'] / o['frames']:.1%}); "
          f"excluded next to picture losses {o['guarded']}; residual frames {o['residual_frames']}; "
          f"residual corner {o['res_corner_hz']:.2f} Hz")
    print(f"  runs roll/pitch: {o['runs']}")
    print(f"  runs yaw (>= 5 frames): {o['runs_yaw']}")
    for sec in ("angle", "residual", "rate", "accel"):
        for d in o[sec]:
            print(f"  {sec:8s} {d['axis']:16s} n {d['n']:5d} mean {d['mean']:+8.3f} {fmt_ci(d['mean_ci']):20s} "
                  f"std {d['std']:7.3f} {fmt_ci(d['std_ci']):20s} p5 {d['p5']:+8.2f} p95 {d['p95']:+8.2f} "
                  f"p95|.| {d['p95_abs']:7.2f} {fmt_ci(d['p95_abs_ci']):18s} max|.| {d['max_abs']:7.2f} "
                  f"kurt {d['kurtosis']:5.2f} {fmt_ci(d['kurtosis_ci'])} {d['unit']}")
    e = o["edge"]
    print(f"  horizon edge width px: n {e['n']} p5 {e['p5']:.2f} mean {e['mean']:.2f} p95 {e['p95']:.2f} std {e['std']:.2f}")
    for k, s in o["spectra"].items():
        bands = "  ".join(f"{lo:g}-{hi:g}Hz {b['rms']:.3f} {fmt_ci(b['rms_ci'])} ({b['share']:.0%})"
                          for b in s["bands"] for lo, hi in [b["band"]])
        print(f"  spectrum {k:6s} windows {s['windows']:3d} (PSD +-{s['psd_rel_err']:.0%}) peak {s['peak_hz']:.2f} Hz | {bands}")
        print(f"  psd {k:6s} deg^2/Hz: " + " ".join(f"{f:.2f}:{p:.3g}" for f, p in s["psd"][1:]))
    for k, e in o["events"].items():
        print(f"  events {k:13s} thr {e['threshold']:.3f} count {e['count']:3d} in {e['minutes']:.2f} min = "
              f"{e['per_min']:.1f} +- {e['per_min_err']:.1f}/min; dur q25/50/75/90 {r2(e['dur_q'])} "
              f"max {e['dur_max']:.2f} s; decay {r2(e['decay_q'])} s; rise {r2(e['rise_q'])} s; "
              f"gaps n {e['n_gaps']} q {r2(e['gap_q'])} mean {e['gap_mean']:.2f} s CV {e['gap_cv']:.2f}; "
              f"frames {e['frames']}")
    for k, r in o["corr"].items():
        print(f"  corr {k:22s} r {r['r']:+.3f} {fmt_ci(r['r_ci'])} n {r['n']}; strongest {r['best_r']:+.3f} at lag {r['best_lag_s']:+.2f} s")
    print("  residual ACF 1/e time (s): " + ", ".join(f"{k} {v:.3f}" for k, v in o["acf_s"].items()))
    for L in o["losses"]:
        print(f"  picture loss frames {L['frames'][0]}-{L['frames'][1]} ({L['dur_s']:.2f} s); max |rate| in the second before "
              f"(deg/s): roll {L['roll_rate_max_pre']:.1f}, pitch {L['pitch_rate_max_pre']:.1f}, yaw {L['yaw_rate_max_pre']:.1f}")
    a = o["airframe"]
    print(f"  airframe (0 deg uptilt): k {a['k']:.4f}; share of thrust tilt on roll {a['tilt_split']:.2f}; body p~r rate "
          f"r {a['corr_pr']['r']:+.3f} {fmt_ci(a['corr_pr']['r_ci'])} n {a['corr_pr']['n']}")
    for d in (a["load_factor"], a["lateral"], a["thrust_dir"], a["tilt"], *a["body_rate"], *a["body_accel"]):
        print(f"  airframe {d['axis']:22s} n {d['n']:5d} mean {d['mean']:+9.4f} {fmt_ci(d['mean_ci']):20s} std {d['std']:8.4f} "
              f"{fmt_ci(d['std_ci']):20s} p5 {d['p5']:+9.4f} p95 {d['p95']:+9.4f} p95|.| {d['p95_abs']:8.4f} "
              f"min {d['min']:+9.4f} max {d['max']:+9.4f} kurt {d['kurtosis']:5.2f} {d['unit']}")
    if "segment" in o:
        for name in ("inside", "outside"):
            s = o["segment"][name]
            print(f"  segment {o['segment']['range']} {name}: " + "; ".join(
                f"{k} res std {d['res_std']:.3f} {fmt_ci(d['res_std_ci'])} ({d['frames']} fr), kurt {d['kurtosis']:.2f} "
                f"{fmt_ci(d['kurtosis_ci'])}, events {d['events']} = {d['per_min']:.1f} +- {d['per_min_err']:.1f}/min"
                for k, d in s.items() if k in ("roll", "pitch", "yaw")))
            print("    5 s blocks: " + "; ".join(
                f"{k} cv {d['block_cv']:.2f} min/std {d['block_min']:.2f} ["
                + ", ".join(f"{b['frames'][0]}-{b['frames'][1]} {b['std']:.2f}" for b in d["blocks"]) + "]"
                for k, d in s.items() if k in ("roll", "pitch", "yaw")))
            t = s["tilt"]
            print(f"    thrust tilt res std {t['res_std']:.3f} {fmt_ci(t['res_std_ci'])} ({t['frames']} fr), share on roll "
                  f"{t['split']:.2f}; " + "; ".join(f"corr {k} r {r['r']:+.3f} {fmt_ci(r['r_ci'])} n {r['n']}"
                                                   for k, r in s["corr"].items()))
    print("  5 s blocks, residual std roll/pitch (deg): " + "; ".join(
        f"{b['frames'][0]}-{b['frames'][1]} {b['roll']:.2f}/{b['pitch']:.2f}" for b in o["blocks"]))


# ---------------------------------------------------------------- T0c: the preset's wind history

def load_wind(path):
    """One seed's wind history (T0c): columns time_s, wind_from_deg, and prevailing_from_deg, the prevailing direction
    that seed drew for its flight (wind.md section 6), the same on every row."""
    rows = list(csv.DictReader(Path(path).open(encoding="utf-8")))
    t, d, p = ([float(r[k]) for r in rows] for k in ("time_s", "wind_from_deg", "prevailing_from_deg"))
    if len(set(p)) != 1:
        raise ValueError(f"{Path(path).name}: prevailing_from_deg must be the seed's one drawn direction on every row")
    return t, d, p


def wrap(a):
    """np.angle(np.exp(1j * a)): the angle a (rad) wrapped to (-pi, pi]."""
    return math.atan2(math.sin(a), math.cos(a))


def wind_history(logs):
    """T0c (wind.md section 6.3) on the weather preset's own wind state, not on the attitude. logs holds one
    (time_s, wind_from_deg, prevailing_from_deg) triple of sequences per seed. Every seed is measured against its own
    drawn prevailing direction; everything else is pooled over the seeds:
    - draws: how alike the seeds' drawn directions are (resultant length: 1 = all the same, near 0 = spread round)
    - offset and spread: the circular mean and the RMS of the wind's deviation from the drawn direction
    - shifts: a change of >= SHIFT_DEG within SHIFT_S, counted once per stretch of such times; their rate, the CV of
      the gaps between onsets, and the periodicity peak: the pooled Rayleigh power of the onset times,
      sum over seeds |sum_k exp(2 pi i f t_k)|^2 / (number of onsets), at its largest over trial periods PEAK_S.
      Random onsets give about 1 at every period; onsets on a schedule pile up power at its period.
    - side_keep: the share of SIDE_S windows in which the wind stays within 90 deg of the drawn direction throughout,
      i.e. keeps one side of a course flown abeam of it."""
    per, gaps, keep_n, keep_sum, zdev, times, draws, span = [], [], 0, 0, 0j, [], [], 0.0
    thr = math.radians(SHIFT_DEG)
    for t, d, p in logs:
        diffs = sorted(b - a for a, b in zip(t, t[1:]))
        m2 = len(diffs) // 2
        dt = diffs[m2] if len(diffs) % 2 else 0.5 * (diffs[m2 - 1] + diffs[m2])     # np.median
        p0 = p[0]
        dev = [wrap(math.radians(x - p0)) for x in d]                       # rad, from the seed's drawn direction
        lag, w = int(round(SHIFT_S / dt)), int(round(SIDE_S / dt))
        onsets, end = [], 0                                                 # crossings < SHIFT_S apart are one shift
        for a, b in runs([abs(wrap(u - v)) >= thr for u, v in zip(dev[lag:], dev)]):
            if not onsets or a - end >= lag:
                onsets.append(a)
            end = b
        other = [0] + list(accumulate(1 if abs(x) >= math.pi / 2 else 0 for x in dev))
        k = [other[i + w] - other[i] == 0 for i in range(len(other) - w)]
        m = (t[-1] - t[0]) / 60
        zs = [cmath.exp(1j * x) for x in dev]
        zsum = sum(zs)
        per.append(dict(prevailing_from_deg=p0 % 360, offset_deg=math.degrees(cmath.phase(zsum / len(zs))),
                        spread_deg=math.degrees(math.sqrt(math.fsum(x * x for x in dev) / len(dev))), shifts=len(onsets),
                        minutes=m, side_keep=sum(k) / len(k)))
        gaps += [(b - a) * dt for a, b in zip(onsets, onsets[1:])]
        keep_n, keep_sum = keep_n + len(k), keep_sum + sum(k)
        zdev += zsum
        times.append([t[i] for i in onsets])
        draws.append(cmath.exp(1j * math.radians(p0)))
        span = max(span, t[-1] - t[0])
    count, minutes = sum(len(x) for x in times), math.fsum(r["minutes"] for r in per)
    step = 1 / (4 * span)                                                   # 4 x finer than the seeds resolve
    f = [1 / PEAK_S[1] + i * step for i in range(int(math.ceil((1 / PEAK_S[0] - 1 / PEAK_S[1]) / step)))]
    power = []
    for fi in f:
        tot = 0.0
        for x in times:
            c = s = 0.0
            for tk in x:
                ang = 2 * math.pi * fi * tk
                c += math.cos(ang)
                s += math.sin(ang)
            tot += c * c + s * s
        power.append(tot / max(count, 1))
    best = max(range(len(f)), key=lambda i: power[i])
    return dict(seeds=per, minutes=minutes, draws=dict(n=len(draws), resultant=abs(sum(draws) / len(draws))),
                offset_deg=math.degrees(cmath.phase(zdev)), spread_deg=mean([r["spread_deg"] for r in per]),
                shifts=dict(count=count, per_min=count / minutes, n_gaps=len(gaps),
                            gap_cv=std(gaps) / mean(gaps) if len(gaps) > 1 else NAN,
                            peak=power[best], peak_period_s=1 / f[best]),
                side_keep=keep_sum / keep_n)


# ---------------------------------------------------------------- the Gaussian check (wind.md section 6.5)

GAUSS_NAMES = ("whole kurt roll", "whole kurt pitch", "field kurt roll", "field kurt pitch", "block cv roll",
               "block cv pitch", "block min roll", "block min pitch", "field res roll", "belt res roll",
               "r roll~yaw rate")


def shaped(o, rng, k, length, fps):
    """Gaussian noise with the Welch PSD of axis k in o (deg^2/Hz), built at the next power of two and cut."""
    f_p, p_p = zip(*o["spectra"][k]["psd"][1:])
    lf, lp = [math.log(v) for v in f_p], [math.log(v) for v in p_p]
    size = 1 << max(1, (length - 1).bit_length())
    f = rfftfreq(size, 1 / fps)
    amp = [0.0] + [math.exp(interp(math.log(max(fi, f_p[0])), lf, lp) / 2) for fi in f[1:]]
    spec = [a * complex(rng.gauss(0, 1), rng.gauss(0, 1)) for a in amp]
    root = math.sqrt(size)
    return [v * root for v in irfft(spec, size)[:length]]


def gauss_check(o, n, quiet=False):
    """Plain Gaussian noise must fail the targets it should fail (wind.md section 6.5). Builds sim-style logs with the
    input's Welch spectra and its inside/outside residual spreads, independent on each axis, and analyses them:
    'stationary' is stationary inside each regime; 'calm/busy' alternates 20 s at 0.6x and 12 s at 1.4x outside.
    Returns {family: rows}, one row of GAUSS_NAMES values per log."""
    global BOOT
    boot_was, BOOT = BOOT, 0                                            # intervals are not needed here
    try:
        fps, seg, N = o["fps"], o["segment"]["range"], o["frames"]
        fr = list(range(1, N + 1))
        inside = [seg[0] <= f <= seg[1] for f in fr]
        unit = {k: nanstd(residual(shaped(o, random.Random(12345), k, 2 ** 17, fps), [(0, 2 ** 17)], fps))
                for k in ("roll", "pitch", "yaw")}
        busy = [0.6 if ((f - 1) / fps) % 32 < 20 else 1.4 for f in fr]
        out = {}
        for fam, field in (("stationary", [1.0] * N), ("calm/busy", busy)):
            rows = []
            for seed in range(n):
                rng = random.Random(seed)
                x = {}
                for k in ("roll", "pitch", "yaw"):
                    si, so = o["segment"]["inside"][k]["res_std"], o["segment"]["outside"][k]["res_std"]
                    x[k] = [v / unit[k] * (si if ins else so * fl)
                            for v, ins, fl in zip(shaped(o, rng, k, N, fps), inside, field)]
                one = [1.0] * N
                yaw = x["yaw"]
                s = analyse(dict(frame=[float(f) for f in fr], time_s=[(f - 1) / fps for f in fr],
                                 roll_deg=[o["angle"][0]["mean"] + v for v in x["roll"]],
                                 pitch_deg=[o["angle"][1]["mean"] + v for v in x["pitch"]],
                                 yaw_rate_dps=[(yaw[i] - (yaw[i - 1] if i else 0.0)) * fps for i in range(N)],
                                 conf_roll=one, conf_pitch=one, conf_yaw=one, edge_px=[NAN] * N,
                                 dup=[False] * N, guarded=0, lost=[False] * N), seg)
                so, si = s["segment"]["outside"], s["segment"]["inside"]
                rows.append([s["residual"][0]["kurtosis"], s["residual"][1]["kurtosis"], so["roll"]["kurtosis"],
                             so["pitch"]["kurtosis"], so["roll"]["block_cv"], so["pitch"]["block_cv"],
                             so["roll"]["block_min"], so["pitch"]["block_min"], so["roll"]["res_std"],
                             si["roll"]["res_std"], s["corr"]["roll~yaw rate"]["r"]])
            out[fam] = rows
            if not quiet:
                print_family(fam, rows)
        return out
    finally:
        BOOT = boot_was


def groups_of_5(col):
    n = len(col)
    return [mean(col[i:i + 5]) for i in range(0, n - n % 5, 5)] if n >= 5 else [mean(col)]


def print_family(fam, rows):
    n = len(rows)
    print(f"gauss {fam}: {n} logs")
    for i, name in enumerate(GAUSS_NAMES):
        col = [r[i] for r in rows]
        g5 = groups_of_5(col)
        print(f"  {name:17s} runs " + (" ".join(f"{v:.2f}" for v in col) if n <= 10 else
              f"min {min(col):.2f} p5 {percentile(col, 5):.2f} p95 {percentile(col, 95):.2f} max {max(col):.2f}")
              + f" | means of 5: {min(g5):.2f}-{max(g5):.2f}")


def gauss_verdict(rows):
    """Grades each group of 5 logs (their means, as the harness grades 5 runs) on T9a, T9b, T12 and T10b.
    Returns one dict per group: the means and whether each target fails."""
    cols = list(zip(*rows))
    g = {name: groups_of_5(list(cols[i])) for i, name in enumerate(GAUSS_NAMES)}
    out = []
    for j in range(len(g[GAUSS_NAMES[0]])):
        v = {name: g[name][j] for name in GAUSS_NAMES}
        t9a = not T9A[0] <= v["field kurt roll"] <= T9A[1]
        t9b = not T9B[0] <= v["field kurt pitch"] <= T9B[1]
        t12 = not (v["block cv roll"] <= T12_CV and v["block cv pitch"] <= T12_CV
                   and v["block min roll"] >= T12_MIN and v["block min pitch"] >= T12_MIN)
        t10b = not T10B[0] <= v["r roll~yaw rate"] <= T10B[1]
        out.append(dict(means=v, fails_T9a=t9a, fails_T9b=t9b, fails_T9=t9a or t9b, fails_T12=t12, fails_T10b=t10b))
    return out


def print_verdict(results):
    """Prints each family's verdict with the margins (TST-06); returns True when every noise group fails T9 or T12."""
    ok = True
    for fam, rows in results.items():
        groups = gauss_verdict(rows)
        for name, key in (("T9 (T9a or T9b)", "fails_T9"), ("T12", "fails_T12"), ("T10b", "fails_T10b")):
            print(f"  verdict {fam:10s} fails {name:15s} in {sum(g[key] for g in groups)} of {len(groups)} groups of 5")
        m = [g["means"] for g in groups]
        lo = lambda k: min(x[k] for x in m)
        hi = lambda k: max(x[k] for x in m)
        print(f"    group means: field kurt roll {lo('field kurt roll'):.2f}-{hi('field kurt roll'):.2f} (T9a {T9A[0]}-{T9A[1]}), "
              f"pitch {lo('field kurt pitch'):.2f}-{hi('field kurt pitch'):.2f} (T9b {T9B[0]}-{T9B[1]}); block cv roll "
              f"{lo('block cv roll'):.2f}-{hi('block cv roll'):.2f}, pitch {lo('block cv pitch'):.2f}-{hi('block cv pitch'):.2f} "
              f"(T12 <= {T12_CV}); block min roll {lo('block min roll'):.2f}-{hi('block min roll'):.2f}, pitch "
              f"{lo('block min pitch'):.2f}-{hi('block min pitch'):.2f} (T12 >= {T12_MIN})")
        ok &= all(g["fails_T9"] or g["fails_T12"] for g in groups)
    print(f"  self-check: plain Gaussian noise {'fails' if ok else 'PASSES'} T9/T12 in every group "
          f"({'ok' if ok else 'the targets cannot reject noise'})")
    return ok


def finite(o):
    """JSON has no NaN; write null, which strict parsers (.NET's default) accept."""
    if isinstance(o, float):
        return o if math.isfinite(o) else None
    if isinstance(o, dict):
        return {k: finite(v) for k, v in o.items()}
    if isinstance(o, (list, tuple)):
        return [finite(v) for v in o]
    return o


def main(argv=None):
    p = argparse.ArgumentParser(prog="wind_stats.py")
    p.add_argument("--letter", help="clip letter: reads reference/_frames/<letter>/attitude.csv")
    p.add_argument("--csv", help="any attitude log in the same columns")
    p.add_argument("--ref", default=str(Path(__file__).resolve().parents[2] / "reference"))
    p.add_argument("--json", help="also write every number to this JSON file")
    p.add_argument("--segment", help="first-last frame: also report residual spread and events inside vs outside")
    p.add_argument("--wind-log", nargs="+", help="T0c: the weather preset's wind history, one CSV per seed")
    p.add_argument("--gauss", type=int, help="after the input, check N plain-Gaussian logs built from it (needs --segment)")
    p.add_argument("--published", action="store_true",
                   help="with --gauss: build the noise from P's published numbers (clip_p.py) instead of an input log")
    a = p.parse_args(argv)
    if not (a.letter or a.csv or a.wind_log or a.published):
        p.error("give --letter, --csv, --wind-log or --published")
    if a.published and not a.gauss:
        p.error("--published needs --gauss")
    o = None
    if a.wind_log:
        o = wind_history([load_wind(x) for x in a.wind_log])
        for r in o["seeds"]:
            print(f"  wind history: drawn {r['prevailing_from_deg']:.1f} deg, offset {r['offset_deg']:+.1f} deg, "
                  f"spread {r['spread_deg']:.1f} deg, {r['shifts']} shifts in {r['minutes']:.1f} min, "
                  f"side kept in {r['side_keep']:.1%} of {SIDE_S:g} s windows")
        s = o["shifts"]
        print(f"  pooled over {o['draws']['n']} seeds: draws' resultant {o['draws']['resultant']:.2f}; offset "
              f"{o['offset_deg']:+.1f} deg; mean spread {o['spread_deg']:.1f} deg; {s['count']} shifts in "
              f"{o['minutes']:.1f} min = {s['per_min']:.3f}/min, gap CV {s['gap_cv']:.2f} ({s['n_gaps']} gaps), "
              f"periodicity peak {s['peak']:.2f} at {s['peak_period_s']:.0f} s; side kept {o['side_keep']:.1%}")
    elif a.published:
        import clip_p
        o = clip_p.summary()
    else:
        path = Path(a.csv) if a.csv else Path(a.ref) / "_frames" / a.letter / "attitude.csv"
        o = analyse(load(path), tuple(int(v) for v in a.segment.split("-")) if a.segment else None)
        report(o)
    if a.json:
        Path(a.json).write_text(json.dumps(finite(o), indent=1, allow_nan=False), encoding="utf-8")
    if a.gauss:
        if "segment" not in o:
            p.error("--gauss needs --segment (or --published)")
        return 0 if print_verdict(gauss_check(o, a.gauss)) else 1
    return 0


if __name__ == "__main__":
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    sys.exit(main())
