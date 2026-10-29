"""T0c test fixture (docs/reference-notes/wind.md sections 5.3, 6.3 and 6.6; K section 8.3).

Ported to plain Python (standard library only) from godot-final:tools/reference/wind_history_fixture.py,
a Blender Python script (D-037; ported, never merged, D-027). The section 5.3 starting model of the
wind's direction, and the controls that T0c must fail, fed through wind_stats.wind_history exactly as
a simulator's wind log would be. It reads nothing from reference/.

    python tools/wind/wind_history_fixture.py --check 40 [--model random sched5 ...]
    python tools/wind/wind_history_fixture.py --write <folder> [--set 0] [--model random]
    python tools/wind/wind_history_fixture.py --grade <folder> [--seeds 40]

--check builds N independent seed sets of each model (set i uses seed i) and prints how many pass each
T0c condition, with the spread of every statistic, then the self-check: the starting model ("random")
must pass all of T0c in every set and every control must fail it in every set (exit 1 otherwise).
--seeds and --minutes change the history (default: T0c's minimum). --write writes one seed set as
--wind-log CSVs; then run wind_stats.py --wind-log <folder>/*.csv. --grade reads a simulator's own histories (one
--wind-log CSV per seed, named seed-<n>.csv, as flightbench --wind-harness --history writes them), groups them by seed
number into sets of --seeds, and grades every set as --check does (exit 1 unless every set passes all of T0c).

The model (assumed starting values, tunable; K section 8.3):
- each seed draws its prevailing direction, uniform over 0-360 deg
- meander: an Ornstein-Uhlenbeck walk with a 2 min time constant, smoothed by a 30 s first-order lag, std 30 deg
- shifts: Poisson, 1 per 5 min on average; each 45-120 deg either way, a linear ramp over 10-30 s, then an
  exponential return towards the prevailing direction with the meander's 2 min time constant
Controls: the shifts on a fixed schedule (a random phase per seed, optionally jittered), the meander without its lag
(QA's round-2 reading of section 5.3, which passes), one direction for every seed, a fixed vector, and a fixed vector
with 5 deg of white jitter.

Port (where this differs from the Blender original, and why):
- Draws come from Python's random.Random(set index), not numpy's PCG64 default_rng(set index): each set is
  statistically the same model, not the same numbers, so the per-set values differ from wind.md section 6.6's.
  Within a set the order is kept: every seed's white noise first, then per seed its direction, its shift onsets,
  amplitudes, signs and ramps, then its jitter. Poisson onsets are drawn until they pass the end (numpy drew a
  fixed surplus and dropped the rest).
- The shifts' exponential returns are summed by one first-order recurrence (factor exp(-DT / TAU_S) per step)
  instead of an onsets x samples matrix; it is the same sum, to rounding.
- The self-check verdict (EXPECT_PASS, exit code) is new: the original printed the counts and left the reading
  to wind.md section 6.6.
- Arguments: plain argparse (no Blender "--" separator); main(argv) returns an exit code.
"""
import argparse
import csv
import math
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import wind_stats  # noqa: E402

DT = 1.0                            # s, the log's step
SEEDS, MINUTES = 40, 120.0          # T0c's minimum history (wind.md section 6.2)
SIG_DEG, TAU_S, LAG_S = 30.0, 120.0, 30.0
RATE = 1 / 300.0                    # shifts per s
AMP_DEG, RAMP_S = (45.0, 120.0), (10.0, 30.0)
BURN = int(10 * TAU_S / DT)         # meander steps dropped before t = 0, so the log starts stationary

T0C = {                             # K section 8.6 / wind.md section 6.3, T0c, on wind_stats.wind_history's output
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
EXPECT_PASS = {"random", "random-unsmoothed"}   # wind.md section 6.6: these pass T0c, every other model fails it


def meander(white, lag_s):
    """Unit-std meander from one seed's white noise: the OU's exact AR(1) step, then the first-order lag, divided by
    the pair's exact stationary std. The first BURN steps are dropped."""
    a1 = math.exp(-DT / TAU_S)
    a2 = math.exp(-DT / lag_s) if lag_s else 0.0
    gain = math.sqrt((1 - a2) ** 2 * (1 + a1 * a2) / ((1 - a2 * a2) * (1 - a1 * a2))) if lag_s else 1.0
    b1 = math.sqrt(1 - a1 * a1)
    x = y = 0.0
    out = []
    for i, w in enumerate(white):
        y = a2 * y + (1 - a2) * x
        x = a1 * x + b1 * w
        if i >= BURN:
            out.append((y if lag_s else x) / gain)
    return out


def shifts(rng, n, schedule_s, jitter_s):
    """Sum of the direction shifts (deg) at t = 0, DT, ..., (n - 1) DT. Onsets from 10 time constants before t = 0,
    Poisson at RATE or every schedule_s from a random phase (each moved by up to +-jitter_s)."""
    t_end, t0 = (n - 1) * DT, -10 * TAU_S
    if schedule_s:
        start = t0 + rng.uniform(0, schedule_s)
        on = [start + i * schedule_s for i in range(int(math.ceil((t_end - start) / schedule_s)))]
        on = [v + rng.uniform(-jitter_s, jitter_s) for v in on]
    else:
        on, v = [], t0
        while True:
            v += rng.expovariate(RATE)
            if v >= t_end:
                break
            on.append(v)
    amp = [rng.uniform(*AMP_DEG) for _ in on]
    sign = [rng.choice((-1.0, 1.0)) for _ in on]
    ramp = [rng.uniform(*RAMP_S) for _ in on]
    out, enter = [0.0] * n, [0.0] * n
    for o, a, s, r in zip(on, amp, sign, ramp):
        a *= s
        i = max(0, math.ceil(o / DT))
        while i < n and i * DT - o < r:          # the linear ramp
            out[i] += a * (i * DT - o) / r
            i += 1
        if i < n:                                 # the return towards the prevailing direction
            enter[i] += a * math.exp(-(i * DT - o - r) / TAU_S)
    q, d = math.exp(-DT / TAU_S), 0.0
    for i in range(n):
        d = d * q + enter[i]
        out[i] += d
    return out


def seed_sets(indices, seeds=SEEDS, minutes=MINUTES, schedule_s=0.0, jitter_s=0.0, lag_s=LAG_S, meander_on=True,
              shifts_on=True, one_direction=False, jitter_deg=0.0):
    """One list of (time_s, wind_from_deg, prevailing_from_deg) per set index; set i draws from seed i."""
    n = int(round(minutes * 60 / DT)) + 1
    t = [i * DT for i in range(n)]
    out = []
    for idx in indices:
        r = random.Random(idx)
        white = [[r.gauss(0.0, 1.0) for _ in range(BURN + n)] for _ in range(seeds)]
        logs = []
        for s in range(seeds):
            m = [SIG_DEG * v for v in meander(white[s], lag_s)] if meander_on else [0.0] * n
            p0 = 270.0 if one_direction else r.uniform(0, 360)
            d = [p0 + v for v in m]
            if shifts_on:
                d = [a + b for a, b in zip(d, shifts(r, n, schedule_s, jitter_s))]
            if jitter_deg:
                d = [a + r.gauss(0.0, jitter_deg) for a in d]
            logs.append((t, [v % 360 for v in d], [p0] * n))
        out.append(logs)
    return out


def options(name):
    kw = dict(MODELS[name])
    kw["meander_on"], kw["shifts_on"] = kw.pop("meander", True), kw.pop("shifts", True)
    return kw


def check(names, n, seeds=SEEDS, minutes=MINUTES, quiet=False):
    """Runs n seed sets of each model through T0c. Returns {model: [[pass per T0C condition] per set]}."""
    if not quiet:
        print(f"T0c on {n} seed sets of {seeds} seeds x {minutes:g} min each "
              f"({'meets' if seeds >= SEEDS and minutes >= MINUTES else 'below'} T0c's minimum of {SEEDS} x {MINUTES:g} min)")
    out = {}
    for name in names:
        res = [wind_stats.wind_history(logs) for i in range(n) for logs in seed_sets([i], seeds, minutes, **options(name))]
        ok = [[bool(fn(o)) for fn in T0C.values()] for o in res]
        out[name] = ok
        if quiet:
            continue
        fails = ", ".join(f"{k} {sum(not row[j] for row in ok)}" for j, k in enumerate(T0C))
        print(f"{name}: passes all of T0c in {sum(all(row) for row in ok)} of {n} sets; fails per condition: {fails}")
        for k, fn in STATS.items():
            v = [float(fn(o)) for o in res]
            print(f"    {k:15s} min {min(wind_stats.fin(v), default=math.nan):.3f}  p5 {wind_stats.percentile(v, 5):.3f}"
                  f"  median {wind_stats.percentile(v, 50):.3f}  p95 {wind_stats.percentile(v, 95):.3f}"
                  f"  max {max(wind_stats.fin(v), default=math.nan):.3f}")
        sys.stdout.flush()
    return out


def verdict(results):
    """The self-check: models in EXPECT_PASS pass all of T0c in every set, every other model fails it in every set."""
    good = True
    for name, ok in results.items():
        passes = sum(all(row) for row in ok)
        want = len(ok) if name in EXPECT_PASS else 0
        good &= passes == want
        print(f"  verdict {name:18s} passes {passes} of {len(ok)} sets (expected {want}): "
              f"{'ok' if passes == want else 'WRONG'}")
    print(f"  self-check: {'ok' if good else 'FAILED'}: the starting model passes T0c and schedules fail it"
          if good else "  self-check: FAILED")
    return good


def grade(folder, seeds=SEEDS):
    """T0c, as check does, on a simulator's histories: seed-<n>.csv grouped by n into sets of `seeds`; a short last set
    is left out. Returns [[pass per T0C condition] per set]."""
    files = sorted(Path(folder).glob("seed-*.csv"), key=lambda f: int(f.stem.split("-")[1]))
    sets = [files[i:i + seeds] for i in range(0, len(files) - seeds + 1, seeds)]
    if not sets:
        raise SystemExit(f"{folder}: fewer than {seeds} seed-<n>.csv histories")
    res = [wind_stats.wind_history([wind_stats.load_wind(f) for f in group]) for group in sets]
    minutes = (len(wind_stats.load_wind(files[0])[0]) - 1) / 60.0
    print(f"T0c on {len(sets)} sets of {seeds} seeds x {minutes:g} min from {folder}")
    ok = [[bool(fn(o)) for fn in T0C.values()] for o in res]
    fails = ", ".join(f"{k} {sum(not row[j] for row in ok)}" for j, k in enumerate(T0C))
    print(f"simulator: passes all of T0c in {sum(all(row) for row in ok)} of {len(sets)} sets; fails per condition: {fails}")
    for k, fn in STATS.items():
        v = [float(fn(o)) for o in res]
        print(f"    {k:15s} min {min(wind_stats.fin(v), default=math.nan):.3f}  p5 {wind_stats.percentile(v, 5):.3f}"
              f"  median {wind_stats.percentile(v, 50):.3f}  p95 {wind_stats.percentile(v, 95):.3f}"
              f"  max {max(wind_stats.fin(v), default=math.nan):.3f}")
    return ok


def write(folder, index, name, seeds, minutes):
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)
    logs = seed_sets([index], seeds, minutes, **options(name))[0]
    for s, (t, d, p) in enumerate(logs):
        with (folder / f"seed_{s:02d}.csv").open("w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(("time_s", "wind_from_deg", "prevailing_from_deg"))
            w.writerows((f"{a:.1f}", f"{b:.4f}", f"{c:.4f}") for a, b, c in zip(t, d, p))
    print(f"wrote {len(logs)} seed files of model {name}, set {index}, to {folder}")


def main(argv=None):
    p = argparse.ArgumentParser(prog="wind_history_fixture.py")
    p.add_argument("--check", type=int, help="run N seed sets of each model through T0c")
    p.add_argument("--write", help="write one seed set as --wind-log CSVs into this folder")
    p.add_argument("--set", type=int, default=0, help="the seed set --write writes")
    p.add_argument("--grade", help="grade a simulator's own histories (seed-<n>.csv) in sets of --seeds")
    p.add_argument("--model", nargs="+", choices=list(MODELS), help="models to run (default: all for --check, "
                   "random for --write)")
    p.add_argument("--seeds", type=int, default=SEEDS)
    p.add_argument("--minutes", type=float, default=MINUTES)
    a = p.parse_args(argv)
    if not (a.check or a.write or a.grade):
        p.error("give --check N, --write <folder> or --grade <folder>")
    if a.grade:
        return 0 if all(all(row) for row in grade(a.grade, a.seeds)) else 1
    if a.write:
        write(a.write, a.set, (a.model or ["random"])[0], a.seeds, a.minutes)
    if a.check:
        return 0 if verdict(check(a.model or list(MODELS), a.check, a.seeds, a.minutes)) else 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
