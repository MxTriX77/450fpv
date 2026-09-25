"""Wind-disturbance statistics from an attitude log (docs/reference-notes/wind.md sections 1-5).

Run with Blender's Python (for numpy) from the repo root:
    blender -b --factory-startup --python tools/reference/wind_stats.py -- --letter P [--json out.json]
    blender -b --factory-startup --python tools/reference/wind_stats.py -- --csv <log> [--json out.json]
    blender -b --factory-startup --python tools/reference/wind_stats.py -- --letter P --segment 121-330 --gauss 5
    blender -b --factory-startup --python tools/reference/wind_stats.py -- --wind-log <seed1.csv> ... [--json out.json]

Input is reference/_frames/<letter>/attitude.csv from track_attitude.py, or a simulated flight
logged in the same columns (frame, time_s, roll_deg, pitch_deg, yaw_rate_dps, conf_roll,
conf_pitch, conf_yaw; a simulation writes conf 1). Every number in wind.md comes from this
script, and the section 6 targets are checked by running it on the simulator's log.
--wind-log takes the weather preset's own wind history instead (T0c): one CSV per seed with
time_s, wind_from_deg and prevailing_from_deg (the direction that seed drew). wind_history_fixture.py
builds such logs from the section 5.3 starting model and checks T0c on many seed sets.

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
- Events: frames where the |residual| of an axis exceeds EVENT_K standard
  deviations of that axis, merged across gaps of up to EVENT_GAP frames.
- Uncertainty: 16-84 % interval over BOOT block-bootstrap resamples of BLOCK-frame blocks
  (BLOCK_SEG inside and outside a segment; seed 0), and 1/sqrt(windows) scaling for spectra.
- Segment: inside and outside a frame range, the residual spread, kurtosis and the spread per 5 s block
  (steadiness), plus the thrust tilt and the roll-yaw coupling.
- Airframe (camera with 0 deg uptilt): thrust/weight to hold height 1/(cos pitch cos roll), the
  horizontal thrust's direction, body rates from ZYX kinematics, and the thrust tilt residual
  roll + k * heading with k = -sin(mean pitch) (wind.md section 5.5).
"""
import argparse
import csv
import json
import math
import sys
from pathlib import Path

import numpy as np

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
D1 = np.array([-2, -1, 0, 1, 2]) / 10.0      # 5-point first derivative (per frame)
D2 = np.array([2, -1, -2, -1, 2]) / 7.0      # 5-point second derivative (per frame^2)


def load(path):
    rows = list(csv.DictReader(path.open(encoding="utf-8")))
    num = lambda r, k: float(r[k]) if r.get(k, "") != "" else math.nan
    cols = {k: np.array([num(r, k) for r in rows]) for k in
            ("frame", "time_s", "roll_deg", "pitch_deg", "yaw_rate_dps", "conf_roll", "conf_pitch", "conf_yaw",
             "edge_px")}
    cols["dup"] = np.array([r.get("dup", "0") == "1" for r in rows])
    lost = np.array([r.get("flag", "ok") == "no_picture" for r in rows])
    near = np.convolve(lost, np.ones(2 * LOSS_GUARD + 1), mode="same") > 0   # torn onset/recovery frames
    for k in ("conf_roll", "conf_pitch", "conf_yaw"):
        cols[k] = np.where(near, 0.0, cols[k])
    cols["guarded"] = int((near & ~lost).sum())
    cols["lost"] = lost
    return cols


def runs(ok):
    """[(start, stop)) index ranges of consecutive True."""
    d = np.diff(np.concatenate(([0], ok.astype(int), [0])))
    return list(zip(np.where(d == 1)[0], np.where(d == -1)[0]))


def per_run(x, spans, fn, pad):
    """Apply fn to x inside each run; results land on the run's frames, NaN within pad of its ends."""
    out = np.full(len(x), np.nan)
    for a, b in spans:
        if b - a > 2 * pad:
            out[a + pad:b - pad] = fn(x[a:b])
    return out


def residual(x, spans, fps):
    """x minus a Gaussian-weighted local-linear trend fitted inside the run (same as the plain
    Gaussian smoother away from run ends, and unbiased by slopes near them). Frames within one
    sigma of a run end are dropped."""
    s = TREND_SIGMA_S * fps
    t = np.arange(-int(3 * s), int(3 * s) + 1)
    w = np.exp(-0.5 * (t / s) ** 2)

    def detrend(v):
        conv = lambda a, k: np.convolve(np.pad(a, len(t) // 2), k[::-1], mode="valid")
        ones = np.ones(len(v))
        s0, s1, s2 = conv(ones, w), conv(ones, w * t), conv(ones, w * t * t)   # sums over neighbours j = i + t
        m0, m1 = conv(v, w), conv(v, w * t)
        return v - (s2 * m0 - s1 * m1) / (s0 * s2 - s1 * s1)
    pad = int(s)
    return per_run(x, spans, lambda v: detrend(v)[pad:len(v) - pad], pad)


def deriv(x, spans, fps, kern, power):
    return per_run(x, spans, lambda v: np.convolve(v, kern[::-1], mode="valid") * fps ** power, 2)


def kurtosis(v):
    return float(np.mean((v - v.mean()) ** 4) / v.var() ** 2)


def boot(values_by_frame, fn, seed=0, block=BLOCK):
    """16-84 % interval of fn over block-bootstrap resamples of the frames that hold values."""
    idx = np.where(np.isfinite(values_by_frame[0]) if isinstance(values_by_frame, tuple)
                   else np.isfinite(values_by_frame))[0]
    blocks = [idx[i:i + block] for i in range(0, len(idx), block)]
    if len(blocks) < 3 or not BOOT:
        return math.nan, math.nan
    rng = np.random.default_rng(seed)
    stats = []
    for _ in range(BOOT):
        pick = np.concatenate([blocks[j] for j in rng.integers(0, len(blocks), len(blocks))])
        stats.append(fn(*(v[pick] for v in values_by_frame)) if isinstance(values_by_frame, tuple)
                     else fn(values_by_frame[pick]))
    return tuple(float(v) for v in np.nanpercentile(stats, [16, 84]))


def describe(x, name, unit):
    v = x[np.isfinite(x)]
    if len(v) < 2 * BLOCK:
        return dict(axis=name, unit=unit, n=int(len(v)), **{k: math.nan for k in (
            "mean", "std", "p5", "p95", "p95_abs", "max_abs", "min", "max", "kurtosis")},
            mean_ci=(math.nan,) * 2, std_ci=(math.nan,) * 2, p95_abs_ci=(math.nan,) * 2, kurtosis_ci=(math.nan,) * 2)
    lo, hi = boot(x, np.std)
    return dict(axis=name, unit=unit, n=int(len(v)), mean=float(v.mean()), mean_ci=boot(x, np.mean),
                std=float(v.std()), std_ci=(lo, hi), p5=float(np.percentile(v, 5)), p95=float(np.percentile(v, 95)),
                p95_abs=float(np.percentile(np.abs(v), 95)), p95_abs_ci=boot(x, lambda a: np.percentile(np.abs(a), 95)),
                max_abs=float(np.abs(v).max()), min=float(v.min()), max=float(v.max()),
                kurtosis=kurtosis(v), kurtosis_ci=boot(x, kurtosis))


def welch(x, spans, fps):
    win = np.hanning(WELCH_N)
    specs, frames = [], []
    for a, b in spans:
        for s in range(a, b - WELCH_N + 1, WELCH_N // 2):
            seg = x[s:s + WELCH_N]
            t = np.arange(WELCH_N)
            seg = seg - np.polyval(np.polyfit(t, seg, 1), t)
            p = np.abs(np.fft.rfft(seg * win)) ** 2 / (fps * (win ** 2).sum())
            p[1:-1] *= 2
            specs.append(p)
            frames.append((s, s + WELCH_N))
    f = np.fft.rfftfreq(WELCH_N, 1 / fps)
    return f, np.array(specs), frames


def band_table(f, specs):
    """RMS (unit) and share of 0.23-15 Hz variance per band; bootstrap over windows."""
    df = f[1] - f[0]
    def rms(ps):
        m = ps.mean(axis=0)
        return np.array([math.sqrt(m[(f >= lo) & (f < hi)].sum() * df) for lo, hi in BANDS])
    base = rms(specs)
    rng = np.random.default_rng(0)
    bs = np.array([rms(specs[rng.integers(0, len(specs), len(specs))]) for _ in range(BOOT)])
    lo, hi = np.percentile(bs, [16, 84], axis=0) if BOOT else (np.full(len(BANDS), math.nan),) * 2
    share = base ** 2 / (base ** 2).sum()
    m = specs.mean(axis=0)
    band = (f >= BANDS[0][0]) & (f < BANDS[-1][1])
    return dict(bands=[dict(band=b, rms=float(r), rms_ci=(float(l), float(h)), share=float(s))
                       for b, r, l, h, s in zip(BANDS, base, lo, hi, share)],
                peak_hz=float(f[band][np.argmax(m[band])]), windows=len(specs),
                psd_rel_err=1 / math.sqrt(len(specs)))


def events(x, fps, over=None):
    """Excursions beyond EVENT_K sigma of the residual x (or of a given boolean 'over' series, for
    combined axes). Durations, rise and decay times, gaps between successive onsets."""
    v = np.isfinite(x)
    thr = EVENT_K * np.nanstd(x) if over is None else EVENT_K      # combined: in sigmas of each axis
    if over is None:
        over = v & (np.abs(np.nan_to_num(x)) > thr)
    ev = []
    for a, b in runs(over):
        if ev and a - ev[-1][1] <= EVENT_GAP and np.all(v[ev[-1][1]:a]):
            ev[-1] = (ev[-1][0], b)
        else:
            ev.append((a, b))
    out = []
    for a, b in ev:
        p = a + int(np.argmax(np.abs(x[a:b])))
        peak = abs(x[p])
        dec = next((j for j in range(p, len(x)) if not v[j] or abs(x[j]) < peak / math.e), None)
        ris = next((j for j in range(p, -1, -1) if not v[j] or abs(x[j]) < peak / math.e), None)
        out.append(dict(start=a, stop=b, peak=p, size=float(x[p]), dur_s=(b - a) / fps,
                        decay_s=(dec - p) / fps if dec is not None and v[dec] else math.nan,
                        rise_s=(p - ris) / fps if ris is not None and v[ris] else math.nan))
    minutes = max(v.sum() / fps / 60, 1e-9)
    onsets = [e["start"] for e in out]
    gaps = [(b - a) / fps for a, b in zip(onsets, onsets[1:]) if all(v[a:b])]
    return dict(threshold=float(thr), count=len(out), minutes=float(minutes), per_min=len(out) / minutes,
                per_min_err=math.sqrt(max(len(out), 1)) / minutes, list=out, gaps_s=gaps)


def summarize_events(e):
    q = lambda key: [ev[key] for ev in e["list"] if np.isfinite(ev[key])]
    pct = lambda a: [float(np.percentile(a, p)) for p in (25, 50, 75, 90)] if len(a) else []
    g = np.array(e["gaps_s"])
    return dict(threshold=e["threshold"], count=e["count"], minutes=e["minutes"], per_min=e["per_min"],
                per_min_err=e["per_min_err"], dur_q=pct(q("dur_s")), dur_max=max(q("dur_s"), default=math.nan),
                decay_q=pct(q("decay_s")), rise_q=pct(q("rise_s")), n_gaps=int(len(g)), gaps_s=[float(v) for v in g],
                gap_q=pct(g), gap_mean=float(g.mean()) if len(g) else math.nan,
                gap_cv=float(g.std() / g.mean()) if len(g) > 1 else math.nan)


def corr(a, b, fps, block=BLOCK):
    """Zero-lag Pearson r with bootstrap interval, and the strongest |r| within +-MAX_LAG_S."""
    both = np.isfinite(a) & np.isfinite(b)
    if both.sum() < 2 * BLOCK:
        return dict(r=math.nan, r_ci=(math.nan,) * 2, n=int(both.sum()), best_r=math.nan, best_lag_s=math.nan)
    r0 = float(np.corrcoef(a[both], b[both])[0, 1])
    ci = boot((np.where(both, a, np.nan), np.where(both, b, np.nan)), lambda x, y: np.corrcoef(x, y)[0, 1], block=block)
    best = (0.0, 0)
    for lag in range(-int(MAX_LAG_S * fps), int(MAX_LAG_S * fps) + 1):
        x, y = (a[:len(a) - lag], b[lag:]) if lag >= 0 else (a[-lag:], b[:len(b) + lag])
        m = np.isfinite(x) & np.isfinite(y)
        if m.sum() > 100:
            r = np.corrcoef(x[m], y[m])[0, 1]
            if abs(r) > abs(best[0]):
                best = (float(r), lag)
    return dict(r=r0, r_ci=ci, n=int(both.sum()), best_r=best[0], best_lag_s=best[1] / fps)


def split(a, e):
    """Share of the variance of e carried by a: cov(a, e) / var(e), over frames that hold both."""
    m = np.isfinite(a) & np.isfinite(e)
    return float(np.cov(a[m], e[m])[0, 1] / np.var(e[m], ddof=1)) if m.sum() > 2 else math.nan


def acf_time(x, fps):
    """Lag (s) where the residual's autocorrelation first falls below 1/e."""
    for lag in range(1, int(3 * fps)):
        a, b = x[:-lag], x[lag:]
        m = np.isfinite(a) & np.isfinite(b)
        if m.sum() < 2 * BLOCK:
            return math.nan
        if np.corrcoef(a[m], b[m])[0, 1] < 1 / math.e:
            return lag / fps
    return math.nan


def analyse(c, segment=None):
    fps = (len(c["time_s"]) - 1) / float(c["time_s"][-1] - c["time_s"][0])
    n = len(c["frame"])
    ok = (c["conf_roll"] >= CONF) & (c["conf_pitch"] >= CONF) & np.isfinite(c["roll_deg"]) & np.isfinite(c["pitch_deg"])
    oky = (c["conf_yaw"] >= CONF) & np.isfinite(c["yaw_rate_dps"])
    # A single repeated frame shows the previous view: its heading holds, and the tracker's rate for
    # the next frame already spans both frame steps. So a yaw run continues through it.
    single = c["dup"] & ~np.roll(c["dup"], 1) & ~np.roll(c["dup"], -1)
    okh = oky | (single & np.roll(oky, 1) & np.roll(oky, -1))
    steps = 1 + np.roll(single, 1)
    inc = np.where(single, 0.0, np.nan_to_num(c["yaw_rate_dps"]) * steps / fps)
    sp, sph = runs(ok), runs(okh)
    axes = {"roll": (np.where(ok, c["roll_deg"], np.nan), sp),
            "pitch": (np.where(ok, c["pitch_deg"], np.nan), sp),
            "yaw": (per_run(np.where(okh, inc, np.nan), sph, np.cumsum, 0), sph)}    # heading, deg
    res = {k: residual(x, s, fps) for k, (x, s) in axes.items()}
    rate = {k: deriv(x, s, fps, D1, 1) for k, (x, s) in axes.items()}
    acc = {k: deriv(x, s, fps, D2, 2) for k, (x, s) in axes.items()}
    frame = lambda i: int(c["frame"][i])
    out = dict(fps=fps, frames=n, first=frame(0), last=frame(-1), guarded=c["guarded"],
               usable=int(ok.sum()), usable_yaw=int(okh.sum()),
               runs=[(frame(a), frame(b - 1)) for a, b in sp],
               runs_yaw=[(frame(a), frame(b - 1)) for a, b in sph if b - a >= 5],
               residual_frames={k: int(np.isfinite(v).sum()) for k, v in res.items()}, res_corner_hz=RES_CORNER_HZ)
    out["angle"] = [describe(axes["roll"][0], "roll", "deg"), describe(axes["pitch"][0], "pitch", "deg"),
                    describe(np.where(oky, c["yaw_rate_dps"], np.nan), "yaw_rate_tracked", "deg/s")]
    out["residual"] = [describe(res[k], k, "deg") for k in axes]
    out["rate"] = [describe(rate[k], k, "deg/s") for k in axes]
    out["accel"] = [describe(acc[k], k, "deg/s^2") for k in axes]
    out["edge"] = describe(np.where(ok, c["edge_px"], np.nan), "horizon_edge_width", "px")
    out["spectra"] = {}
    for k, (x, s) in axes.items():
        f, specs, frames = welch(x, s, fps)
        if len(specs):
            out["spectra"][k] = band_table(f, specs) | {"window_frames": [(frame(a), frame(b - 1)) for a, b in frames],
                                                        "psd": [(float(fi), float(p)) for fi, p in zip(f, specs.mean(axis=0))]}
    ev = {k: events(res[k], fps) for k in axes}
    over = np.zeros(n, dtype=bool)
    for k in ("roll", "pitch"):     # either axis beyond its own threshold; sizes in roll/pitch sigmas
        over |= np.abs(np.nan_to_num(res[k])) > EVENT_K * np.nanstd(res[k])
    norm = np.fmax(np.abs(res["roll"]) / np.nanstd(res["roll"]), np.abs(res["pitch"]) / np.nanstd(res["pitch"]))
    ev["roll_or_pitch"] = events(norm, fps, over & np.isfinite(norm))
    out["events"] = {k: summarize_events(e) | {"frames": [(frame(x["start"]), frame(x["stop"] - 1)) for x in e["list"]]}
                     for k, e in ev.items()}
    out["corr"] = {f"{a}~{b} {kind}": corr(src[a], src[b], fps) for kind, src in (("residual", res), ("rate", rate))
                   for a, b in (("roll", "pitch"), ("roll", "yaw"), ("pitch", "yaw"))}
    out["acf_s"] = {k: acf_time(res[k], fps) for k in axes}
    # Airframe view, valid when the camera sits on the airframe with 0 deg uptilt (pilot, Q1). ZYX kinematics give
    # the body rates that rate-mode sticks command (products of rates, < 1 deg/s^2, left out of the accelerations).
    # tilt = roll residual + k * heading residual, k = -sin(mean pitch), is the sideways tilt of the thrust vector:
    # yaw about the body axis leaves it unchanged (wind.md section 5.5).
    th, ph = np.radians(axes["pitch"][0]), np.radians(axes["roll"][0])
    k = -math.sin(math.radians(np.nanmean(axes["pitch"][0])))
    tilt = res["roll"] + k * res["yaw"]
    body = lambda d: {"p": d["roll"] - d["yaw"] * np.sin(th), "r": d["yaw"] * np.cos(th) * np.cos(ph) - d["pitch"] * np.sin(ph)}
    brate, bacc = body(rate), body(acc)
    out["airframe"] = dict(
        k=k, load_factor=describe(1 / (np.cos(th) * np.cos(ph)), "thrust/weight", "1"),
        lateral=describe(np.tan(-ph) / np.cos(th), "lateral thrust/weight", "1"),
        thrust_dir=describe(np.degrees(np.arctan2(np.tan(-ph) / np.cos(th), np.tan(-th))), "thrust left of nose", "deg"),
        tilt=describe(tilt, "thrust tilt residual", "deg"), tilt_split=split(res["roll"], tilt),
        body_rate=[describe(v, a, "deg/s") for a, v in brate.items()],
        body_accel=[describe(v, a, "deg/s^2") for a, v in bacc.items()], corr_pr=corr(brate["p"], brate["r"], fps))
    blocks = []                      # residual spread per 5 s of flight, for comparing terrain
    for s in range(0, n, BLOCK_S):
        seg = {k: res[k][s:s + BLOCK_S] for k in ("roll", "pitch")}
        if np.isfinite(seg["roll"]).sum() >= BLOCK_S * 2 // 3:
            blocks.append(dict(frames=(frame(s), frame(min(n, s + BLOCK_S) - 1)),
                               **{k: float(np.nanstd(v)) for k, v in seg.items()}))
    out["blocks"] = blocks
    losses = []                      # picture losses, and how busy the attitude was in the second before
    for a, b in runs(c["lost"]):
        pre = slice(max(0, a - LOSS_GUARD - int(fps)), max(0, a - LOSS_GUARD))
        losses.append(dict(frames=(frame(a), frame(b - 1)), dur_s=(b - a) / fps, **{
            f"{k}_rate_max_pre": float(np.nanmax(np.abs(rate[k][pre]), initial=np.nan)) for k in axes}))
    out["losses"] = losses
    if segment:                      # the same statistics inside and outside a frame range (e.g. terrain)
        inside = (c["frame"] >= segment[0]) & (c["frame"] <= segment[1])
        out["segment"] = {}
        for name, m in (("inside", inside), ("outside", ~inside)):
            d = {}
            for k in axes:
                v = np.where(m, res[k], np.nan)
                minutes = np.isfinite(v).sum() / fps / 60
                count = sum(bool(m[x["start"]]) for x in ev[k]["list"])
                sd = float(np.nanstd(v))
                # steadiness: residual spread per 5 s block of this side's frames (blocks with >= 2/3 of their frames)
                bl = [dict(frames=(frame(s), frame(min(n, s + BLOCK_S) - 1)), std=float(np.nanstd(v[s:s + BLOCK_S])))
                      for s in range(0, n, BLOCK_S) if np.isfinite(v[s:s + BLOCK_S]).sum() >= BLOCK_S * 2 // 3]
                bs = np.array([b["std"] for b in bl])
                d[k] = dict(res_std=sd, res_std_ci=boot(v, np.nanstd, block=BLOCK_SEG), frames=int(np.isfinite(v).sum()),
                            kurtosis=kurtosis(v[np.isfinite(v)]),
                            kurtosis_ci=boot(v, lambda a: kurtosis(a[np.isfinite(a)]), block=BLOCK_SEG),
                            blocks=bl, block_cv=float(bs.std() / bs.mean()) if len(bs) > 1 else math.nan,
                            block_min=float(bs.min() / sd) if len(bs) > 1 else math.nan,
                            events=count, per_min=count / minutes if minutes else math.nan,
                            per_min_err=math.sqrt(max(count, 1)) / minutes if minutes else math.nan)
            v = np.where(m, tilt, np.nan)
            d["tilt"] = dict(res_std=float(np.nanstd(v)), res_std_ci=boot(v, np.nanstd, block=BLOCK_SEG),
                             frames=int(np.isfinite(v).sum()), split=split(np.where(m, res["roll"], np.nan), v))
            d["corr"] = {f"roll~yaw {kind}": corr(np.where(m, src["roll"], np.nan), src["yaw"], fps, block=BLOCK_SEG)
                         for kind, src in (("residual", res), ("rate", rate))}
            out["segment"][name] = d
        out["segment"]["range"] = segment
    return out


def fmt_ci(ci):
    return f"[{ci[0]:.3g}, {ci[1]:.3g}]"


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
              f"{e['per_min']:.1f} +- {e['per_min_err']:.1f}/min; dur q25/50/75/90 {np.round(e['dur_q'], 2).tolist()} "
              f"max {e['dur_max']:.2f} s; decay {np.round(e['decay_q'], 2).tolist()} s; rise {np.round(e['rise_q'], 2).tolist()} s; "
              f"gaps n {e['n_gaps']} q {np.round(e['gap_q'], 2).tolist()} mean {e['gap_mean']:.2f} s CV {e['gap_cv']:.2f}; "
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


def load_wind(path):
    """One seed's wind history (T0c): columns time_s, wind_from_deg, and prevailing_from_deg, the prevailing direction
    that seed drew for its flight (wind.md section 6), the same on every row."""
    rows = list(csv.DictReader(Path(path).open(encoding="utf-8")))
    t, d, p = (np.array([float(r[k]) for r in rows]) for k in ("time_s", "wind_from_deg", "prevailing_from_deg"))
    if np.unique(p).size != 1:
        raise ValueError(f"{path}: prevailing_from_deg must be the seed's one drawn direction on every row")
    return t, d, p


def wind_history(logs):
    """T0c (wind.md section 6.3) on the weather preset's own wind state, not on the attitude. logs holds one
    (time_s, wind_from_deg, prevailing_from_deg) triple of arrays per seed. Every seed is measured against its own drawn
    prevailing direction; everything else is pooled over the seeds:
    - draws: how alike the seeds' drawn directions are (resultant length: 1 = all the same, near 0 = spread round)
    - offset and spread: the circular mean and the RMS of the wind's deviation from the drawn direction
    - shifts: a change of >= SHIFT_DEG within SHIFT_S, counted once per stretch of such times; their rate, the CV of
      the gaps between onsets, and the periodicity peak: the pooled Rayleigh power of the onset times,
      sum over seeds |sum_k exp(2 pi i f t_k)|^2 / (number of onsets), at its largest over trial periods PEAK_S.
      Random onsets give about 1 at every period; onsets on a schedule pile up power at its period.
    - side_keep: the share of SIDE_S windows in which the wind stays within 90 deg of the drawn direction throughout,
      i.e. keeps one side of a course flown abeam of it."""
    per, gaps, keep, zdev, times, draws, span = [], [], [], 0j, [], [], 0.0
    for t, d, p in logs:
        dt = float(np.median(np.diff(t)))
        dev = np.angle(np.exp(1j * np.radians(d - p[0])))                  # rad, from the seed's drawn direction
        lag, w = int(round(SHIFT_S / dt)), int(round(SIDE_S / dt))
        onsets = []                                                        # crossings < SHIFT_S apart are one shift
        for a, b in runs(np.abs(np.angle(np.exp(1j * (dev[lag:] - dev[:-lag])))) >= math.radians(SHIFT_DEG)):
            if not onsets or a - end >= lag:
                onsets.append(a)
            end = b
        other = np.concatenate(([0], np.cumsum(np.abs(dev) >= math.pi / 2)))
        k = (other[w:] - other[:-w]) == 0
        m = (t[-1] - t[0]) / 60
        per.append(dict(prevailing_from_deg=float(p[0]) % 360, offset_deg=math.degrees(np.angle(np.exp(1j * dev).mean())),
                        spread_deg=math.degrees(math.sqrt(np.mean(dev ** 2))), shifts=len(onsets), minutes=m,
                        side_keep=float(k.mean())))
        gaps += list(np.diff(onsets) * dt)
        keep.append(k)
        zdev += np.exp(1j * dev).sum()
        times.append(t[onsets])
        draws.append(np.exp(1j * math.radians(p[0])))
        span = max(span, t[-1] - t[0])
    g, count, minutes = np.array(gaps), sum(len(x) for x in times), sum(r["minutes"] for r in per)
    f = np.arange(1 / PEAK_S[1], 1 / PEAK_S[0], 1 / (4 * span))            # 4 x finer than the seeds resolve
    power = sum(np.abs(np.exp(2j * math.pi * f[:, None] * x[None, :]).sum(axis=1)) ** 2 for x in times) / max(count, 1)
    return dict(seeds=per, minutes=minutes, draws=dict(n=len(draws), resultant=float(abs(np.mean(draws)))),
                offset_deg=math.degrees(np.angle(zdev)), spread_deg=float(np.mean([r["spread_deg"] for r in per])),
                shifts=dict(count=count, per_min=count / minutes, n_gaps=len(g),
                            gap_cv=float(g.std() / g.mean()) if len(g) > 1 else math.nan,
                            peak=float(power.max()), peak_period_s=float(1 / f[power.argmax()])),
                side_keep=float(np.concatenate(keep).mean()))


def gauss_check(o, n):
    """Plain Gaussian noise must fail the targets it should fail (wind.md section 6.5). Builds sim-style logs with the
    input's Welch spectra and its inside/outside residual spreads, independent on each axis, and analyses them:
    'stationary' is stationary inside each regime; 'calm/busy' alternates 20 s at 0.6x and 12 s at 1.4x outside."""
    global BOOT
    BOOT, fps, seg, N = 0, o["fps"], o["segment"]["range"], o["frames"]    # intervals are not needed here
    fr = np.arange(1, N + 1)
    inside = (fr >= seg[0]) & (fr <= seg[1])

    def shaped(rng, k, length):
        f_p, p_p = np.array(o["spectra"][k]["psd"][1:]).T
        f = np.fft.rfftfreq(length, 1 / fps)
        amp = np.exp(np.interp(np.log(np.maximum(f, f_p[0])), np.log(f_p), np.log(p_p)) / 2)
        amp[0] = 0
        return np.fft.irfft(amp * (rng.standard_normal(len(f)) + 1j * rng.standard_normal(len(f))), length) * math.sqrt(length)
    unit = {k: float(np.nanstd(residual(shaped(np.random.default_rng(12345), k, 2 ** 17), [(0, 2 ** 17)], fps)))
            for k in ("roll", "pitch", "yaw")}
    busy = np.where(((fr - 1) / fps) % 32 < 20, 0.6, 1.4)
    for fam, field in (("stationary", np.ones(N)), ("calm/busy", busy)):
        rows = []
        for seed in range(n):
            rng = np.random.default_rng(seed)
            x = {k: shaped(rng, k, N) / unit[k] * np.where(inside, o["segment"]["inside"][k]["res_std"],
                                                              o["segment"]["outside"][k]["res_std"] * field)
                 for k in ("roll", "pitch", "yaw")}
            one = np.ones(N)
            s = analyse(dict(frame=fr.astype(float), time_s=(fr - 1) / fps, roll_deg=o["angle"][0]["mean"] + x["roll"],
                             pitch_deg=o["angle"][1]["mean"] + x["pitch"], yaw_rate_dps=np.diff(x["yaw"], prepend=0) * fps,
                             conf_roll=one, conf_pitch=one, conf_yaw=one, edge_px=np.full(N, math.nan),
                             dup=np.zeros(N, bool), guarded=0, lost=np.zeros(N, bool)), seg)
            so, si = s["segment"]["outside"], s["segment"]["inside"]
            rows.append([s["residual"][0]["kurtosis"], s["residual"][1]["kurtosis"], so["roll"]["kurtosis"],
                         so["pitch"]["kurtosis"], so["roll"]["block_cv"], so["pitch"]["block_cv"], so["roll"]["block_min"],
                         so["pitch"]["block_min"], so["roll"]["res_std"], si["roll"]["res_std"], s["corr"]["roll~yaw rate"]["r"]])
        a = np.array(rows)
        names = ("whole kurt roll", "whole kurt pitch", "field kurt roll", "field kurt pitch", "block cv roll",
                 "block cv pitch", "block min roll", "block min pitch", "field res roll", "belt res roll", "r roll~yaw rate")
        print(f"gauss {fam}: {n} logs")
        for i, name in enumerate(names):
            g5 = a[:n - n % 5, i].reshape(-1, 5).mean(axis=1) if n >= 5 else a[:, i]
            print(f"  {name:17s} runs " + (" ".join(f"{v:.2f}" for v in a[:, i]) if n <= 10 else
                  f"min {a[:, i].min():.2f} p5 {np.percentile(a[:, i], 5):.2f} p95 {np.percentile(a[:, i], 95):.2f} max {a[:, i].max():.2f}")
                  + f" | means of 5: {g5.min():.2f}-{g5.max():.2f}")


def finite(o):
    """JSON has no NaN; write null, which strict parsers (.NET's default) accept."""
    if isinstance(o, float):
        return o if math.isfinite(o) else None
    if isinstance(o, dict):
        return {k: finite(v) for k, v in o.items()}
    if isinstance(o, (list, tuple)):
        return [finite(v) for v in o]
    return o


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="wind_stats.py")
    p.add_argument("--letter", help="clip letter: reads reference/_frames/<letter>/attitude.csv")
    p.add_argument("--csv", help="any attitude log in the same columns")
    p.add_argument("--ref", default=str(Path(__file__).resolve().parents[2] / "reference"))
    p.add_argument("--json", help="also write every number to this JSON file")
    p.add_argument("--segment", help="first-last frame: also report residual spread and events inside vs outside")
    p.add_argument("--wind-log", nargs="+", help="T0c: the weather preset's wind history, one CSV per seed")
    p.add_argument("--gauss", type=int, help="after the input, check N plain-Gaussian logs built from it (needs --segment)")
    a = p.parse_args(argv)
    if not (a.letter or a.csv or a.wind_log):
        p.error("give --letter, --csv or --wind-log")
    if a.wind_log:
        o = wind_history([load_wind(p) for p in a.wind_log])
        for r in o["seeds"]:
            print(f"  wind history: drawn {r['prevailing_from_deg']:.1f} deg, offset {r['offset_deg']:+.1f} deg, "
                  f"spread {r['spread_deg']:.1f} deg, {r['shifts']} shifts in {r['minutes']:.1f} min, "
                  f"side kept in {r['side_keep']:.1%} of {SIDE_S:g} s windows")
        s = o["shifts"]
        print(f"  pooled over {o['draws']['n']} seeds: draws' resultant {o['draws']['resultant']:.2f}; offset "
              f"{o['offset_deg']:+.1f} deg; mean spread {o['spread_deg']:.1f} deg; {s['count']} shifts in "
              f"{o['minutes']:.1f} min = {s['per_min']:.3f}/min, gap CV {s['gap_cv']:.2f} ({s['n_gaps']} gaps), "
              f"periodicity peak {s['peak']:.2f} at {s['peak_period_s']:.0f} s; side kept {o['side_keep']:.1%}")
    else:
        path = Path(a.csv) if a.csv else Path(a.ref) / "_frames" / a.letter / "attitude.csv"
        o = analyse(load(path), tuple(int(v) for v in a.segment.split("-")) if a.segment else None)
        report(o)
    if a.json:
        Path(a.json).write_text(json.dumps(finite(o), indent=1, allow_nan=False), encoding="utf-8")
    if a.gauss:
        if not a.segment:
            p.error("--gauss needs --segment")
        gauss_check(o, a.gauss)


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception:
        import traceback
        traceback.print_exc()
        sys.exit(1)
