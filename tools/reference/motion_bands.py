"""The camera's turn by band and by flight phase, from a track_motion.py log, with the measurement's own share split off
(D-064; docs/reference-notes/ground-micro-motion.md).

    python tools/reference/motion_bands.py --letter S --segment cruise=40-280 --segment turn=281-370 [--json out.json]
    python tools/reference/motion_bands.py --csv <motion.csv> --segment hover=2-180

A segment is NAME=FIRST-LAST, in the log's 1-based frames, both included. Per segment and axis (roll, pitch, yaw), over
its usable frames (conf >= 0.5, not within 2 frames of a picture loss, wind_stats' rules):
- the rate's mean and spread (deg/s), and its 95th percentile of |rate|
- the turn's power by band, from a Welch spectrum of the frame-to-frame turn (Hann, 64 frames, half overlap; 32 frames
  when no run holds 64; none when no run holds 32): RMS of the angle (deg) and of the rate (deg/s) in BANDS. The angle's
  PSD is the turn's over 4 sin^2(pi f / fps), the rate's is the turn's times fps^2.
- the measurement's parts, in the same bands:
  - noise: half the difference of the turn fitted on the two checkerboard halves of the grid (the fit's own
    random error, which a rigid turn shares and noise does not)
  - parallax: half the top-minus-bottom difference (near scenery below, far above: the drone's own motion), which
    holds the noise too
  - the recorder's field pick (hover-micro-motion.md section 2.3): each frame shows the newest whole field, so its
    instant lags the frame clock by 0-20 ms, sigma 5.8 ms; times the rate's spread it is a white error on the angle
- floor_check.py's test of the high-frequency floor (hover-micro-motion.md section 2): h = x[i] - (x[i-1] + x[i+1]) / 2
  on the summed angle, its kurtosis and the share of its power in the top 5 % of frames, and its RMS by quartile of the
  fit's own frame-to-frame change (inlier share, residual, field count). About 1 from the bottom quartile to the top
  means the floor does not know what the fit did; a rise means glitches, and 1 - (bottom / all)^2 bounds their share.
- the wobble (wind.md section 1.4): the summed angle less its 0.6 s trend; its spread, kurtosis and 1/e correlation
  time, and its events beyond 2 sigma (per minute, median duration and decay from the peak to 1/e)
- the rolling-shutter check: the fit's per-row sideways residual (row_wobble_px), against the fit's residual
- the forward-motion term (zoom: forward speed over depth, per frame)
"""
import argparse
import csv
import json
import math
import sys
from itertools import accumulate
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "wind"))
import wind_stats as ws  # noqa: E402

from ref_paths import REPO_ROOT, RefError, frames_dir, frames_path, letter  # noqa: E402

AXES = ("roll", "pitch", "yaw")
BANDS = ((0.4, 1.0), (1.0, 4.0), (4.0, 15.5))     # "below 1 Hz", "1-4 Hz", "4-15 Hz" (the frames' Nyquist)
PICK_S = 0.020 / math.sqrt(12)                    # the field pick's lag spread, s (uniform over a 20 ms field)
FIELD_HZ = 50.0                                   # PAL fields a second
CONF = ws.CONF
NAN = math.nan


def load(path):
    rows = list(csv.DictReader(Path(path).open(encoding="utf-8")))
    num = lambda r, k: float(r[k]) if r.get(k, "") not in ("", None) else NAN
    keys = [k for k in rows[0].keys() if k not in ("dup", "flag")] if rows else []
    cols = {k: [num(r, k) for r in rows] for k in keys}
    lost = [r.get("flag", "ok") == "no_picture" for r in rows]
    n = len(rows)
    cols["near_loss"] = [any(lost[max(0, i - ws.LOSS_GUARD):i + ws.LOSS_GUARD + 1]) for i in range(n)]
    cols["dup"] = [r.get("dup", "0") == "1" for r in rows]
    return cols


def parse_segment(text):
    name, _, rng = text.partition("=")
    a, _, b = rng.partition("-")
    if not name or not a.isdigit() or not b.isdigit() or int(b) < int(a):
        raise argparse.ArgumentTypeError(f"segment {text!r}: want NAME=FIRST-LAST")
    return name, int(a), int(b)


def welch(x, spans, n, linear):
    """Mean one-sided PSD per frame-frequency (unit^2 per cycle/frame) of x over Hann windows of n inside spans,
    each window less its linear trend (linear) or its mean."""
    win = ws.hanning(n)
    norm = math.fsum(a * a for a in win)
    specs = []
    for a, b in spans:
        for s in range(a, b - n + 1, n // 2):
            seg = ws.detrend_linear(x[s:s + n]) if linear else [u - math.fsum(x[s:s + n]) / n for u in x[s:s + n]]
            p = [abs(c) ** 2 / norm for c in ws.rfft([u * v for u, v in zip(seg, win)])]
            for i in range(1, len(p) - 1):
                p[i] *= 2
            specs.append(p)
    return [math.fsum(col) / len(specs) for col in zip(*specs)], len(specs)


def summed(d, spans):
    """The angle: the turns summed inside each run, from 0."""
    x = [NAN] * len(d)
    for a, b in spans:
        acc = 0.0
        for i in range(a, b):
            acc += d[i]
            x[i] = acc
    return x


def band_rms(psd, n, fps, gain=lambda f: 1.0):
    df = fps / n
    return [math.sqrt(math.fsum(p * gain(i * df) for i, p in enumerate(psd) if i and lo <= i * df < hi) * df / fps)
            for lo, hi in BANDS]


def spectra(d, spans, n, fps):
    """(angle band RMS, rate band RMS, the angle's PSD): the angle's from the summed turn less each window's trend
    (wind_stats' way, so a low band does not take a high band's leakage), the rate's from the turn itself."""
    pa, nw = welch(summed(d, spans), spans, n, True)
    pr, _ = welch(d, spans, n, False)
    return band_rms(pa, n, fps), band_rms(pr, n, fps, lambda f: fps * fps), pa, nw


def white_angle_bands(sigma, n, fps):
    """(angle, rate) band RMS that a white error of sigma per frame on the angle adds (the field pick)."""
    flat = [2 * sigma * sigma] * (n // 2 + 1)
    turn = [2 * sigma * sigma * 4 * math.sin(math.pi * i / n) ** 2 for i in range(n // 2 + 1)]
    return band_rms(flat, n, fps), band_rms(turn, n, fps, lambda f: fps * fps)


def quartiles(pairs):
    pairs = sorted(pairs)
    k = len(pairs) // 4
    if k < 3:
        return [NAN] * 4
    return [math.sqrt(math.fsum(h * h for _, h in pairs[j * k:(j + 1) * k]) / k) for j in range(4)]


def second_diff(v, i):
    return abs(v[i] - 0.5 * (v[i - 1] + v[i + 1]))


def field_steps(c, lo, hi, fps, field_hz=FIELD_HZ):
    """The fields each frame advanced in frames [lo, hi): the recorder shows the newest whole field at each of its
    ticks, so frame k shows field floor(k q + phi), q = field_hz / the frame rate, and the steps run 2, 2, 1 at about
    30 frames a second (40, 40 and 20 ms, not 33). (q, phi) is the pair that best explains the turns: each turn is the
    local rate times its step. Returns (steps, q, phi, gain): gain is the residual's power with the steps over the
    one with uniform frames (below 1 when the pattern is there)."""
    axes = [c[f"d_{ax}_deg"] for ax in AXES]
    usable = [i for i in range(lo + 3, hi - 3) if all(x[j] == x[j] for x in axes for j in range(i - 3, i + 4))]
    if len(usable) < 30:
        return None
    q0 = field_hz / fps

    pre = [[0.0] + list(accumulate(v if v == v else 0.0 for v in x[lo:hi])) for x in axes]

    def sse(steps):
        ps = [0] + list(accumulate(steps))
        e = 0.0
        for x, px in zip(axes, pre):
            for i in usable:
                j = i - lo
                r = (px[j + 4] - px[j - 3]) / (ps[j + 4] - ps[j - 3])
                e += (x[i] - r * steps[j]) ** 2
        return e
    best = None
    for qi in range(-60, 61):
        q = q0 * (1 + qi * 5e-5)
        for pi in range(20):
            phi = pi / 20
            st = [math.floor(k * q + phi) - math.floor((k - 1) * q + phi) for k in range(lo, hi)]
            if min(st) < 1:
                continue
            e = sse(st)
            if best is None or e < best[0]:
                best = (e, st, q, phi)
    flat = sse([1] * (hi - lo))
    e, st, q, phi = best
    return st, q, phi, e / flat if flat else NAN


def retime(c, fps, block=150):
    """Each frame's turn rescaled to a uniform frame clock: d * (fields per frame on average / its own step).
    Fitted in blocks (the recorder's clock may slip between them). Returns the per-block fits."""
    n = len(c["frame"])
    fits = []
    for lo in range(1, n, block):
        hi = min(n, lo + block)
        r = field_steps(c, lo, hi, fps)
        if r is None:
            continue
        st, q, phi, gain = r
        if not gain < 0.95:                                   # no pattern to correct here (a still camera, say)
            fits.append((lo + 1, hi, q, phi, gain, False))
            continue
        for i in range(lo, hi):
            scale = q / st[i - lo]
            for ax in AXES:
                for tag in ("deg", "a", "b", "top", "bot"):
                    k = f"d_{ax}_{tag}"
                    if k in c and c[k][i] == c[k][i]:
                        c[k][i] *= scale
        fits.append((lo + 1, hi, q, phi, gain, True))
    return fits


def real_bounds(a, retimed):
    """The camera's own motion per band, as [low, high] for the angle (deg) and the rate (deg/s), the way
    hover-micro-motion.md section 2.4 bounds P's: the measured band less, in quadrature,
    - the fit's random noise (the checkerboard halves), both bounds
    - the parallax beyond that noise (top against bottom), the low bound only
    - the field pick, the low bound only, and only when the frames were not retimed
    - in the 4-15 Hz band, the glitches: scaled by the steadiest quartile's h over all h (the low bound by the
      smallest such ratio over the fit's diagnostics, the high bound by the largest)."""
    q = [v[0] / a["h_rms"] for v in a.get("h_by", {}).values() if v[0] == v[0]] if a.get("h_rms") else []
    out = {}
    for kind in ("angle", "rate"):
        lo, hi = [], []
        for j, m in enumerate(a[kind]):
            n = a[f"noise_{kind}"][j]
            px = max(0.0, a[f"parallax_{kind}"][j] ** 2 - n * n)
            pk = 0.0 if retimed else a[f"pick_{kind}"][j] ** 2
            g_lo, g_hi = (min(min(q), 1.0), min(max(q), 1.0)) if (q and BANDS[j][0] >= 4) else (1.0, 1.0)
            hi.append(math.sqrt(max(0.0, (m * g_hi) ** 2 - n * n)))
            lo.append(math.sqrt(max(0.0, (m * g_lo) ** 2 - n * n - px - pk)))
        out[f"real_{kind}"] = list(zip(lo, hi))
    return out


def analyse(c, segments, timing=True):
    n_all = len(c["frame"])
    t = c["time_s"]
    fps = (n_all - 1) / (t[-1] - t[0])
    out = dict(fps=fps, segments={}, retimed=retime(c, fps) if timing else [])
    for name, first, last in segments:
        lo, hi = max(first - 1, 1), min(last, n_all)            # 0-based, [lo, hi); frame 1 has no turn
        ok = [c["conf_roll"][i] >= CONF and not c["near_loss"][i] and c["d_roll_deg"][i] == c["d_roll_deg"][i]
              for i in range(lo, hi)]
        spans = ws.runs(ok)
        longest = max((b - a for a, b in spans), default=0)
        nwin = 64 if longest >= 64 else 32 if longest >= 32 else 0
        idx = [lo + i for i, v in enumerate(ok) if v]
        seg = dict(frames=(first, last), seconds=(hi - lo) / fps, usable=len(idx), window=nwin, axes={})
        if not idx:
            out["segments"][name] = seg
            continue
        fit_rows = [i for i in idx if not c["dup"][i]]
        def med(k):
            v = sorted(c[k][i] for i in fit_rows if c[k][i] == c[k][i])
            return v[len(v) // 2] if v else NAN
        seg.update(inlier_frac=med("inlier_frac"), resid_px=med("resid_px"), row_wobble_px=med("row_wobble_px"),
                   zoom_per_s=med("zoom") * fps, n_fields=med("n_fields"))
        trip = [lo + i for i in range(1, hi - lo - 1) if ok[i - 1] and ok[i] and ok[i + 1]
                and not any(c["dup"][lo + j] for j in (i - 1, i, i + 1))]
        for axis in AXES:
            d = [c[f"d_{axis}_deg"][lo + i] if v else NAN for i, v in enumerate(ok)]
            rates = [v * fps for v in d if v == v]
            mean = math.fsum(rates) / len(rates)
            sd = math.sqrt(math.fsum((r - mean) ** 2 for r in rates) / len(rates))
            a = dict(rate_mean=mean, rate_sd=sd, rate_p95=ws.percentile([abs(r - mean) for r in rates], 95))
            if nwin:
                def series(k, sign=1):
                    out_ = []
                    for i, v in enumerate(ok):
                        if not v:
                            out_.append(NAN)
                            continue
                        x1, x2 = c[f"d_{axis}_{k[0]}"][lo + i], c[f"d_{axis}_{k[1]}"][lo + i]
                        out_.append(0.5 * (x1 + sign * x2) if x1 == x1 and x2 == x2 else 0.0)
                    return out_
                ang, rate, pa, nw = spectra(d, spans, nwin, fps)
                na, nr, _, _ = spectra(series(("a", "b"), -1), spans, nwin, fps)
                xa, xr, _, _ = spectra(series(("top", "bot"), -1), spans, nwin, fps)
                ka, kr = white_angle_bands(sd * PICK_S, nwin, fps)
                a.update(windows=nw, angle=ang, rate=rate, noise_angle=na, noise_rate=nr, parallax_angle=xa,
                         parallax_rate=xr, pick_angle=ka, pick_rate=kr,
                         angle_peak_hz=max(range(1, len(pa)), key=lambda i: pa[i]) * fps / nwin)
            res = ws.residual(summed(d, spans), spans, fps)
            if sum(1 for v in res if v == v) >= 60:
                ev = ws.summarize_events(ws.events(res, fps))
                a["wobble"] = dict(std=ws.nanstd(res), kurtosis=ws.kurtosis([v for v in res if v == v]),
                                   acf_s=ws.acf_time(res, fps), events_per_min=ev["per_min"], events=ev["count"],
                                   dur_s=ev["dur_q"][1] if ev["dur_q"] else NAN,
                                   decay_s=ev["decay_q"][1] if ev["decay_q"] else NAN)
            dd = c[f"d_{axis}_deg"]
            h = [(dd[i] - dd[i + 1]) / 2 for i in trip]              # x[i] - (x[i-1] + x[i+1]) / 2 on the summed angle
            if len(h) >= 12 and any(h):
                hs = sorted(h, key=abs)
                top = hs[int(0.95 * len(hs)):]
                hrms = math.sqrt(math.fsum(v * v for v in h) / len(h))
                q = {k: quartiles([(second_diff(c[k], i), (dd[i] - dd[i + 1]) / 2) for i in trip])
                     for k in ("inlier_frac", "resid_px", "n_fields")}
                a.update(h_rms=hrms, h_kurtosis=ws.kurtosis(h), h_top5_share=math.fsum(v * v for v in top) / math.fsum(v * v for v in h),
                         h_by=q, glitch_floor=max(0.0, max(1 - (v[0] / hrms) ** 2 for v in q.values() if v[0] == v[0])) if hrms else NAN)
            if "angle" in a:
                a.update(real_bounds(a, retimed=bool(out["retimed"]) and all(f[5] for f in out["retimed"])))
            seg["axes"][axis] = a
        out["segments"][name] = seg
    return out


def report(o):
    print(f"fps {o['fps']:.3f}; bands {', '.join(f'{lo:g}-{hi:g}' for lo, hi in BANDS)} Hz")
    for a, b, q, phi, gain, used in o["retimed"]:
        print(f"  field steps f{a}-{b}: {q:.5f} fields/frame, phase {phi:.2f}, residual power x{gain:.2f}"
              f"{' (retimed)' if used else ' (kept)'}")
    for name, s in o["segments"].items():
        print(f"\n{name}: f{s['frames'][0]}-{s['frames'][1]}, {s['seconds']:.2f} s, {s['usable']} usable, window {s['window']}")
        if "inlier_frac" in s:
            print(f"  fit: inliers {s['inlier_frac']:.2f}, residual {s['resid_px']:.2f} px, row wobble {s['row_wobble_px']:.2f} px, "
                  f"fields {s['n_fields']:.0f}, forward/depth {s['zoom_per_s']:.3f} /s")
        for axis, a in s["axes"].items():
            print(f"  {axis}: rate mean {a['rate_mean']:+.2f}, sd {a['rate_sd']:.2f}, p95 {a['rate_p95']:.2f} deg/s")
            if "angle" in a:
                f3 = lambda v: " / ".join(f"{x:.4f}" for x in v)
                f2 = lambda v: " / ".join(f"{x:.2f}" for x in v)
                print(f"    angle RMS {f3(a['angle'])} deg; noise {f3(a['noise_angle'])}; parallax {f3(a['parallax_angle'])}; "
                      f"pick {f3(a['pick_angle'])}; peak {a['angle_peak_hz']:.2f} Hz")
                print(f"    rate RMS  {f2(a['rate'])} deg/s; noise {f2(a['noise_rate'])}; parallax {f2(a['parallax_rate'])}; "
                      f"pick {f2(a['pick_rate'])}")
            if "real_angle" in a:
                print("    real: angle " + " / ".join(f"{l:.4f}-{h:.4f}" for l, h in a["real_angle"]) + " deg; rate "
                      + " / ".join(f"{l:.2f}-{h:.2f}" for l, h in a["real_rate"]) + " deg/s")
            if "wobble" in a:
                w = a["wobble"]
                print(f"    wobble {w['std']:.3f} deg, kurtosis {w['kurtosis']:.2f}, 1/e {w['acf_s']:.2f} s; events {w['events']} "
                      f"({w['events_per_min']:.1f}/min), median {w['dur_s']:.2f} s long, decay {w['decay_s']:.2f} s")
            if "h_rms" in a:
                q = " ".join(f"{k} {v[3] / v[0]:.2f}" for k, v in a["h_by"].items() if v[0] == v[0] and v[0] > 0)
                print(f"    h {a['h_rms']:.4f} deg, kurtosis {a['h_kurtosis']:.2f}, top 5 % {a['h_top5_share']:.0%}; "
                      f"top/bottom quartile by fit change: {q}; glitch share >= {a['glitch_floor']:.0%}")


def main(argv=None):
    p = argparse.ArgumentParser(prog="motion_bands.py", description=__doc__.split("\n")[0])
    src = p.add_mutually_exclusive_group(required=True)
    src.add_argument("--letter", type=letter, help="read reference/_frames/<letter>/motion.csv")
    src.add_argument("--csv", help="a track_motion.py log")
    p.add_argument("--segment", type=parse_segment, action="append", required=True, help="NAME=FIRST-LAST (frames)")
    p.add_argument("--no-retime", action="store_true", help="keep the frames' uniform clock (no field-step fit)")
    p.add_argument("--json", help="also write the numbers here (inside reference/_frames/ when --letter is used)")
    p.add_argument("--root", default=str(REPO_ROOT), help="repo root holding reference/ (default: this checkout)")
    args = p.parse_args(argv)
    try:
        path = Path(args.csv) if args.csv else frames_dir(args.root) / args.letter / "motion.csv"
        if not path.is_file():
            raise RefError("no motion log: run track_motion.py first")
        o = analyse(load(path), args.segment, timing=not args.no_retime)
        if args.json:
            js = frames_path(args.root, args.json, "--json") if args.letter else Path(args.json)
            js.write_text(json.dumps(o, indent=1) + "\n", encoding="utf-8")
    except RefError as e:
        print(f"motion_bands: error: {e}", file=sys.stderr)
        return 2
    report(o)
    return 0


if __name__ == "__main__":
    sys.exit(main())
