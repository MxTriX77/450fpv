"""T0c test fixture (docs/reference-notes/wind.md sections 5.3, 6.3 and 6.6).

The section 5.3 starting model of the wind's direction, and the controls that T0c must fail, fed through
wind_stats.wind_history exactly as a simulator's wind log would be. It reads nothing from reference/.

Run with Blender's Python (for numpy) from the repo root:
    blender -b --factory-startup --python tools/reference/wind_history_fixture.py -- --check 1000 [--model random ...]
    blender -b --factory-startup --python tools/reference/wind_history_fixture.py -- --write <folder> [--set 0]
--check builds N independent seed sets of each model (set i uses numpy seed i) and prints how many pass each T0c
condition, with the spread of every statistic. --seeds and --minutes change the history (default: T0c's minimum).
--write writes one seed set as --wind-log CSVs; then run wind_stats.py -- --wind-log <folder>/*.csv.

The model (assumed starting values, tunable):
- each seed draws its prevailing direction, uniform over 0-360 deg (wind.md section 6)
- meander: an Ornstein-Uhlenbeck walk with a 2 min time constant, smoothed by a 30 s first-order lag, std 30 deg
- shifts: Poisson, 1 per 5 min on average; each 45-120 deg either way, a linear ramp over 10-30 s, then an
  exponential return towards the prevailing direction with the meander's 2 min time constant
Controls: the shifts on a fixed schedule (a random phase per seed, optionally jittered), the meander without its lag
(QA's round-2 reading of section 5.3), one direction for every seed, a fixed vector, and a fixed vector with 5 deg of
white jitter.
"""
import argparse
import csv
import math
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import wind_stats  # noqa: E402

DT = 1.0                            # s, the log's step
SEEDS, MINUTES = 40, 120.0          # T0c's minimum history (wind.md section 6.2)
SIG_DEG, TAU_S, LAG_S = 30.0, 120.0, 30.0
RATE = 1 / 300.0                    # shifts per s
AMP_DEG, RAMP_S = (45.0, 120.0), (10.0, 30.0)
BURN = int(10 * TAU_S / DT)         # meander steps dropped before t = 0, so the log starts stationary

T0C = {                             # wind.md section 6.3, T0c, on wind_stats.wind_history's output
    "draws": lambda o: o["draws"]["resultant"] <= 0.7,
    "offset": lambda o: abs(o["offset_deg"]) <= 15,
    "spread": lambda o: 20 <= o["spread_deg"] <= 60 and min(s["spread_deg"] for s in o["seeds"]) >= 10,
    "rate": lambda o: 0.1 <= o["shifts"]["per_min"] <= 0.4,
    "gap_cv": lambda o: o["shifts"]["gap_cv"] >= 0.5,          # NaN (fewer than 2 gaps) fails
    "peak": lambda o: o["shifts"]["peak"] <= 2.4,
    "side": lambda o: 0.75 <= o["side_keep"] <= 0.98,
}
STATS = {"draws": lambda o: o["draws"]["resultant"], "offset": lambda o: o["offset_deg"],
         "spread": lambda o: o["spread_deg"], "min seed spread": lambda o: min(s["spread_deg"] for s in o["seeds"]),
         "rate": lambda o: o["shifts"]["per_min"], "gap_cv": lambda o: o["shifts"]["gap_cv"],
         "peak": lambda o: o["shifts"]["peak"], "side": lambda o: o["side_keep"]}

MODELS = {
    "random": {},
    "random-unsmoothed": dict(lag_s=0.0),
    "sched3": dict(schedule_s=180.0),
    "sched3-jitter30": dict(schedule_s=180.0, jitter_s=30.0),
    "sched5": dict(schedule_s=300.0),
    "sched8": dict(schedule_s=480.0),
    "sched10": dict(schedule_s=600.0),
    "sched3-unsmoothed": dict(schedule_s=180.0, lag_s=0.0),
    "sched5-unsmoothed": dict(schedule_s=300.0, lag_s=0.0),
    "sched8-unsmoothed": dict(schedule_s=480.0, lag_s=0.0),
    "sched10-unsmoothed": dict(schedule_s=600.0, lag_s=0.0),
    "one-direction": dict(one_direction=True),
    "fixed": dict(meander=False, shifts=False),
    "fixed-jitter5": dict(meander=False, shifts=False, jitter_deg=5.0),
}


def meander(white, lag_s):
    """Unit-std meander, one column per seed, from white noise (rows = time steps): the OU's exact AR(1) step, then
    the first-order lag, divided by the pair's exact stationary std. The first BURN rows are dropped."""
    a1 = math.exp(-DT / TAU_S)
    a2 = math.exp(-DT / lag_s) if lag_s else 0.0
    gain = math.sqrt((1 - a2) ** 2 * (1 + a1 * a2) / ((1 - a2 * a2) * (1 - a1 * a2))) if lag_s else 1.0
    x, y = np.zeros(white.shape[1]), np.zeros(white.shape[1])
    out = np.empty((white.shape[0] - BURN, white.shape[1]))
    for i, w in enumerate(white):
        y = a2 * y + (1 - a2) * x
        x = a1 * x + math.sqrt(1 - a1 * a1) * w
        if i >= BURN:
            out[i - BURN] = (y if lag_s else x) / gain
    return out


def shifts(rng, t, schedule_s, jitter_s):
    """Sum of the direction shifts (deg). Onsets from 10 time constants before t = 0, Poisson at RATE or every
    schedule_s from a random phase (each moved by up to +-jitter_s)."""
    t0 = t[0] - 10 * TAU_S
    if schedule_s:
        on = np.arange(t0 + rng.uniform(0, schedule_s), t[-1], schedule_s)
        on = on + rng.uniform(-jitter_s, jitter_s, on.size)
    else:
        on = t0 + np.cumsum(rng.exponential(1 / RATE, int((t[-1] - t0) * RATE * 3) + 20))
        on = on[on < t[-1]]
    amp = rng.uniform(*AMP_DEG, on.size) * rng.choice((-1.0, 1.0), on.size)
    ramp = rng.uniform(*RAMP_S, on.size)[:, None]
    u = t[None, :] - on[:, None]
    return amp @ np.where(u < 0, 0.0, np.where(u < ramp, u / ramp, np.exp(-(u - ramp) / TAU_S)))


def seed_sets(indices, seeds=SEEDS, minutes=MINUTES, schedule_s=0.0, jitter_s=0.0, lag_s=LAG_S, meander_on=True,
              shifts_on=True, one_direction=False, jitter_deg=0.0):
    """One list of (time_s, wind_from_deg, prevailing_from_deg) per set index; set i draws from numpy seed i."""
    n = int(round(minutes * 60 / DT)) + 1
    t = np.arange(n) * DT
    rngs = [np.random.default_rng(i) for i in indices]
    white = np.concatenate([r.standard_normal((BURN + n, seeds)) for r in rngs], axis=1)
    m = SIG_DEG * meander(white, lag_s) if meander_on else np.zeros((n, white.shape[1]))
    out = []
    for j, r in enumerate(rngs):
        logs = []
        for s in range(seeds):
            p0 = 270.0 if one_direction else float(r.uniform(0, 360))
            d = p0 + m[:, j * seeds + s]
            if shifts_on:
                d = d + shifts(r, t, schedule_s, jitter_s)
            if jitter_deg:
                d = d + r.normal(0, jitter_deg, n)
            logs.append((t, d % 360, np.full(n, p0)))
        out.append(logs)
    return out


def options(name):
    kw = dict(MODELS[name])
    kw["meander_on"], kw["shifts_on"] = kw.pop("meander", True), kw.pop("shifts", True)
    return kw


def check(names, n, seeds, minutes, batch=50):
    print(f"T0c on {n} seed sets of {seeds} seeds x {minutes:g} min each "
          f"({'meets' if seeds >= SEEDS and minutes >= MINUTES else 'below'} T0c's minimum of {SEEDS} x {MINUTES:g} min)")
    for name in names:
        res = []
        for i in range(0, n, batch):
            res += [wind_stats.wind_history(logs) for logs in
                    seed_sets(range(i, min(i + batch, n)), seeds, minutes, **options(name))]
        ok = np.array([[bool(fn(o)) for fn in T0C.values()] for o in res])
        fails = ", ".join(f"{k} {int((~ok[:, j]).sum())}" for j, k in enumerate(T0C))
        print(f"{name}: passes all of T0c in {int(ok.all(axis=1).sum())} of {n} sets; fails per condition: {fails}")
        for k, fn in STATS.items():
            v = np.array([fn(o) for o in res], float)
            print(f"    {k:15s} min {np.nanmin(v):.3f}  p5 {np.nanpercentile(v, 5):.3f}  median {np.nanmedian(v):.3f}"
                  f"  p95 {np.nanpercentile(v, 95):.3f}  max {np.nanmax(v):.3f}")
        sys.stdout.flush()


def write(folder, index, name, seeds, minutes):
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)
    for s, (t, d, p) in enumerate(seed_sets([index], seeds, minutes, **options(name))[0]):
        with (folder / f"seed_{s:02d}.csv").open("w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(("time_s", "wind_from_deg", "prevailing_from_deg"))
            w.writerows((f"{a:.1f}", f"{b:.4f}", f"{c:.4f}") for a, b, c in zip(t, d, p))
    print(f"wrote {s + 1} seed files of model {name}, set {index}, to {folder}")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="wind_history_fixture.py")
    p.add_argument("--check", type=int, help="run N seed sets of each model through T0c")
    p.add_argument("--write", help="write one seed set as --wind-log CSVs into this folder")
    p.add_argument("--set", type=int, default=0, help="the seed set --write writes")
    p.add_argument("--model", nargs="+", choices=list(MODELS), help="models to run (default: all for --check, "
                   "random for --write)")
    p.add_argument("--seeds", type=int, default=SEEDS)
    p.add_argument("--minutes", type=float, default=MINUTES)
    a = p.parse_args(argv)
    if not (a.check or a.write):
        p.error("give --check N or --write <folder>")
    if a.write:
        write(a.write, a.set, (a.model or ["random"])[0], a.seeds, a.minutes)
    if a.check:
        check(a.model or list(MODELS), a.check, a.seeds, a.minutes)


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception:
        import traceback
        traceback.print_exc()
        sys.exit(1)
