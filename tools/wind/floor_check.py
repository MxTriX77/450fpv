"""The attitude floor above 4 Hz in a tracker log: how much of it the tracker makes (p7-micro-motion;
docs/reference-notes/hover-micro-motion.md section 2).

Reads a track_attitude.py CSV (wind_stats.load's columns plus the fit's diagnostics inlier_frac, span and
yaw_del_deg) and reports, over the usable frames (wind_stats' rules):
- the Welch PSD per band from 2 to 15 Hz (wind_stats.welch): real motion falls steeply there, white noise is flat
- the high-frequency statistic h = x[i] - (x[i-1] + x[i+1]) / 2 per axis, as a white-equivalent per-frame RMS
  (h / sqrt(1.5)), its kurtosis and the share of its variance in the frames with the top 5 % of |h|
- h's RMS by quartile of the fit's own frame-to-frame change (inlier share, span). The camera's real motion does
  not know how the fit's inliers changed; a tracker glitch does. The ratio of the top quartile's RMS to the bottom
  one's is about 1 for motion or fit-independent noise (the known-wrong reading), and rises with glitches.
- the strip check: yaw_del_deg, the vertical shift between consecutive yaw strips, which are resampled in each frame's
  own level frame, so it is the horizon's frame-to-frame pitch error (wind.md section 1.5); per frame it is that
  over sqrt(2), robust (IQR) and as a std.

Usage: python tools/wind/floor_check.py LOG.csv
"""
import csv
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import wind_stats as ws  # noqa: E402

PSD_BANDS = ((2, 3), (3, 4), (4, 6), (6, 8), (8, 10), (10, 12), (12, 15))
FLOOR_FROM_HZ = 8.0     # the band the flat floor is read from (wind.md section 3.2: flat above 6-8 Hz)


def diagnostics(path):
    rows = list(csv.DictReader(Path(path).open(encoding="utf-8")))
    num = lambda r, k: float(r[k]) if r.get(k, "") not in ("", None) else math.nan
    return {k: [num(r, k) for r in rows] for k in ("inlier_frac", "span", "yaw_del_deg")}


def quartiles(pairs):
    """RMS of the second item (white-equivalent) in each quartile of the first, ascending."""
    pairs = sorted(pairs)
    k = len(pairs) // 4
    return [math.sqrt(math.fsum(h * h for _, h in pairs[j * k:(j + 1) * k]) / k / 1.5) for j in range(4)]


def analyse(path):
    c = ws.load(path)
    d = diagnostics(path)
    n = len(c["frame"])
    t = c["time_s"]
    fps = (n - 1) / float(t[-1] - t[0])
    ok = [cr >= ws.CONF and cp >= ws.CONF and ws.isf(r) and ws.isf(p)
          for cr, cp, r, p in zip(c["conf_roll"], c["conf_pitch"], c["roll_deg"], c["pitch_deg"])]
    dup = c["dup"]
    spans = ws.runs(ok)
    out = dict(fps=fps, frames=n, usable=sum(ok), axes={})
    trip = [i for i in range(1, n - 1) if ok[i - 1] and ok[i] and ok[i + 1] and not (dup[i - 1] or dup[i] or dup[i + 1])]
    for name in ("roll", "pitch"):
        x = c[name + "_deg"]
        f, specs, _ = ws.welch(ws.where(ok, x), spans, fps)
        m = [math.fsum(col) / len(specs) for col in zip(*specs)] if specs else []
        df = f[1] - f[0]
        band = lambda lo, hi: math.fsum(m[i] for i, fi in enumerate(f) if lo <= fi < hi) * df
        psd = {f"{lo}-{hi}": band(lo, hi) / (hi - lo) for lo, hi in PSD_BANDS}
        floor = band(FLOOR_FROM_HZ, 15) / (15 - FLOOR_FROM_HZ)
        above4 = band(4, 15)
        h = [x[i] - 0.5 * (x[i - 1] + x[i + 1]) for i in trip]
        hs = sorted(h, key=abs)
        top = hs[int(0.95 * len(hs)):]
        out["axes"][name] = dict(
            psd=psd, floor_psd=floor, rms_4_15=math.sqrt(above4), flat_share=floor * 11 / above4 if above4 else math.nan,
            hf_white=math.sqrt(math.fsum(v * v for v in h) / len(h) / 1.5), hf_kurtosis=ws.kurtosis(h),
            hf_top5_share=math.fsum(v * v for v in top) / math.fsum(v * v for v in h),
            by_inlier_change=quartiles([(abs(d["inlier_frac"][i] - 0.5 * (d["inlier_frac"][i - 1] + d["inlier_frac"][i + 1])),
                                         x[i] - 0.5 * (x[i - 1] + x[i + 1])) for i in trip]),
            by_span_change=quartiles([(abs(d["span"][i] - 0.5 * (d["span"][i - 1] + d["span"][i + 1])),
                                       x[i] - 0.5 * (x[i - 1] + x[i + 1])) for i in trip]))
    oky = [cy >= ws.CONF for cy in c["conf_yaw"]]
    yd = sorted(v for v, o, du in zip(d["yaw_del_deg"], oky, dup) if o and ws.isf(v) and not du)
    if len(yd) > 8:
        iqr = (yd[int(0.75 * (len(yd) - 1))] - yd[int(0.25 * (len(yd) - 1))]) / 1.349
        out["strip"] = dict(pairs=len(yd), pitch_noise_robust=iqr / math.sqrt(2), pitch_noise_std=ws.std(yd) / math.sqrt(2))
    return out


def report(o):
    print(f"frames {o['frames']}, usable {o['usable']}, {o['fps']:.3f} fps")
    for name, a in o["axes"].items():
        print(f"{name}: PSD deg^2/Hz " + " ".join(f"{k} {v:.2e}" for k, v in a["psd"].items()))
        print(f"  4-15 Hz RMS {a['rms_4_15']:.4f} deg; flat floor (>= {FLOOR_FROM_HZ:g} Hz) {a['floor_psd']:.2e} deg^2/Hz, "
              f"{a['flat_share']:.0%} of the 4-15 Hz power")
        print(f"  h white-equivalent {a['hf_white']:.4f} deg/frame, kurtosis {a['hf_kurtosis']:.2f}, "
              f"top 5 % of frames carry {a['hf_top5_share']:.0%}")
        print("  h by quartile of |inlier-share change| " + " ".join(f"{v:.4f}" for v in a["by_inlier_change"])
              + f"  (top/bottom {a['by_inlier_change'][3] / a['by_inlier_change'][0]:.2f})")
        print("  h by quartile of |span change|         " + " ".join(f"{v:.4f}" for v in a["by_span_change"])
              + f"  (top/bottom {a['by_span_change'][3] / a['by_span_change'][0]:.2f})")
    if "strip" in o:
        s = o["strip"]
        print(f"strip check: {s['pairs']} pairs, the horizon's pitch noise per frame {s['pitch_noise_robust']:.4f} deg (robust), "
              f"{s['pitch_noise_std']:.4f} deg (std)")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    report(analyse(sys.argv[1]))
