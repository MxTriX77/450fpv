"""Reads WindLevelsProbe.TheAttribution's runs (p7c-wind 1.5): per variant, grade_levels.py's per-run quantities for
pitch and roll (T1, T2, T4, T5 field, T6b, T6c, T6d, T9b, T10a), averaged over the seeds, so each source's share of the
harness's pitch shows by taking it out.

usage: python tools/wind/pitch_probe.py DIR
"""

import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import grade_levels as gl  # noqa: E402
import wind_stats as ws  # noqa: E402

KEYS = ("T1", "T2", "T4", "T5 field", "T6b", "T6c share", "T6c rms", "T6d roll", "T6d pitch", "T9b", "T10a")


def main():
    root = Path(sys.argv[1])
    print(f"{'variant':26s} " + " ".join(f"{k:>9s}" for k in KEYS) + "  pitch 0.23-0.5 / 0.5-1 Hz")
    for d in sorted(p for p in root.iterdir() if p.is_dir()):
        rows = []
        for csv in sorted(d.glob("seed-*.csv")):
            seg = csv.with_suffix(".segment").read_text().strip()
            seg = tuple(int(v) for v in seg.split("-")) if seg else None
            o = ws.analyse(ws.load(str(csv)), seg)
            v = gl.per_run(o)
            b = o["spectra"]["pitch"]["bands"]
            v["low"], v["mid"] = b[0]["rms"], b[1]["rms"]
            rows.append(v)
        if not rows:
            continue

        def mean(k):
            xs = [r[k] for r in rows if not (isinstance(r[k], float) and math.isnan(r[k]))]
            return sum(xs) / len(xs) if xs else math.nan
        print(f"{d.name:26s} " + " ".join(f"{mean(k):9.3f}" for k in KEYS) + f"  {mean('low'):.3f} / {mean('mid'):.3f}")


if __name__ == "__main__":
    main()
