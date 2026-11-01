"""Grades the K section 8.6 wind targets per level (p7c-wind 1.5, design 2; wind.md sections 6.2-6.4).

Reads `flightbench --wind-harness <level> --out DIR` runs (harness.json and one CSV per run in the tracker's
columns), analyses each with wind_stats.py's `analyse`, averages each field over the nominal runs (T8 pools the
gaps, T11b uses the s = 1 and s = 0 variants) and grades it against the level's column of wind.md's tables:
Severe as section 6.3, Windy with the angle spreads and band RMS scaled by c = 0.5 (the tangent of the mean lean
for T0), Calm ceilings only (section 6.4). Each target is then graded on its known-wrong model's runs, where one was
flown, and must fail there:
- `-nodrag`: the airframe without drag (T0, T11b)
- `-steady`: the drawn strength as one fixed vector, no turbulence, direction changes or strip (T0b, the field's
  spreads and spectra, T7, T8, T10)
- `-nobelt`: the field without the belt's wooded strip, its canopy flow (the belt's spreads and ratios, T7's belt
  share, T10b's belt), flown at the same heights
- `-noiseless`: the pilot without its stick noise (D-060, HarnessPilot's motor noise), a second known-wrong for the
  pitch spread and its 1-4 Hz (T2, T6c), where the human remnant belongs; every flown known-wrong must fail
- `as severe` (Calm only): Severe's runs graded on Calm's ceilings, a calm level as strong as Severe (every ceiling
  but T6d's)
- `pose`: the nominal runs' logs with white noise of 0.15 deg per axis added to the attitude, the "noise on the pose"
  that flight-randomness forbids (T6a, T6d)
- plain Gaussian noise with the run's spectra (wind_stats.py's own check: T9a and T12 on noise alternating calm and
  busy, T9b on stationary noise).
A target with several conditions (T0b, T8, T10b, T11b, T12) passes when all pass, and its known-wrong model fails it
when any fails.

T6d (D-060) is a ceiling at clip P's real camera motion over 4-15 Hz, not P's raw floor (0.125 / 0.054 deg): about half
of that floor is the tracker's (glitches tied to its fit, and fit-independent noise; floor_check.py,
hover-micro-motion.md section 2). P's real motion is 0.06-0.08 deg roll and about 0.04 deg pitch (section 2.4,
derived); the harness's frames carry the recorder's field pick (WindHarness.FieldStep), so the ceiling is that motion's
upper end with P's pick added in quadrature (0.018 / 0.012 deg, section 2.3): 0.082 / 0.044 deg (derived).

usage: python tools/wind/grade_levels.py DIR LEVEL [--json OUT]
DIR holds <level>/, and optionally <level>-nodrag/, <level>-steady/, <level>-nobelt/ and <level>-noiseless/.
"""

import argparse
import json
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import wind_stats as ws  # noqa: E402

# Severe's bands (wind.md section 6.3): (lo, hi), None for open; "scaled" ones are multiplied by c at Windy and become
# ceilings at Windy's lower bound for Calm.
SEVERE = {
    "T0": (6.0, 12.0),
    "T0b spread": (0.14, 0.40),
    "T1": (0.35, 0.70), "T2": (0.19, 0.38), "T3": (0.85, 1.70), "T4": (0.29, 0.58),
    "T5 field": (0.22, 0.66), "T5 belt": (0.8, 2.5),
    "T6a": (0.85, None), "T6b": (0.06, 0.16), "T6c share": (0.20, 0.45), "T6c rms": (0.11, 0.25),
    "T6d roll": (None, 0.082), "T6d pitch": (None, 0.044),       # D-060: P's real motion and pick, not its floor
    "T7 rate": (7, 24), "T7 belt share": (0.70, None),
    "T8 median": (1, 4), "T8 cv": (0.5, 1.2),
    "T9a": (2.2, 3.5), "T9b": (3.3, 5.5),
    "T10a": (-0.05, 0.40), "T10b whole": (0.35, 0.75), "T10b belt": (0.55, 0.90),
    "T11a belt": (1.1, 2.3), "T11a field": (0.39, 0.81),
    "T11b s1": (0.8, None), "T11b s0": (0.8, None), "T11b tilt s1": (0.75, 1.25), "T11b tilt s0": (0.75, 1.25),
    "T12 cv roll": (None, 0.32), "T12 cv pitch": (None, 0.32), "T12 min roll": (0.50, None), "T12 min pitch": (0.50, None),
}
SCALED = {"T1", "T2", "T3", "T4", "T5 field", "T5 belt", "T6b", "T6c rms", "T11a belt", "T11a field"}
CALM = {"T0", "T1", "T2", "T3", "T4", "T5 field", "T5 belt", "T6d roll", "T6d pitch", "T11a belt", "T11a field"}
# The targets in wind.md's order, each with its conditions and its known-wrong model (the family that must fail it).
TARGETS = [
    ("T0", ["T0"], "nodrag"), ("T0b", ["T0b spread", "T0b windward"], "steady"),
    ("T1", ["T1"], "steady"), ("T2", ["T2"], ("steady", "noiseless")), ("T3", ["T3", "T3 ratio"], "nobelt"), ("T4", ["T4", "T4 ratio"], "nobelt"),
    ("T5 field", ["T5 field"], "steady"), ("T5 belt", ["T5 belt"], "nobelt"),
    ("T6a", ["T6a"], "pose"), ("T6b", ["T6b"], "steady"), ("T6c", ["T6c share", "T6c rms"], ("steady", "noiseless")),
    ("T6d", ["T6d roll", "T6d pitch"], "pose"), ("T7", ["T7 rate", "T7 belt share"], "nobelt"),
    ("T8", ["T8 median", "T8 cv"], "steady"), ("T9a", ["T9a"], "calm/busy"), ("T9b", ["T9b"], "stationary"),
    ("T10a", ["T10a"], "steady"), ("T10b", ["T10b whole", "T10b belt", "T10b belt > field"], "nobelt"),
    ("T11a", ["T11a belt", "T11a field", "T11a ratio"], "nobelt"),
    ("T11b", ["T11b s1", "T11b s0", "T11b tilt s1", "T11b tilt s0"], "nodrag"),
    ("T12", ["T12 cv roll", "T12 cv pitch", "T12 min roll", "T12 min pitch"], "calm/busy"),
]
POSE_NOISE_DEG = 0.15


def bands(level):
    out = {}
    for k, (lo, hi) in SEVERE.items():
        if level == "severe":
            out[k] = (lo, hi)
        elif level == "windy":
            if k == "T0":
                out[k] = (3.1, 6.1)          # 4.6 +- 1.5 (the tangent of the lean scaled by c, section 6.4)
            elif k in SCALED:
                out[k] = (lo * 0.5 if lo is not None else None, hi * 0.5 if hi is not None else None)
            else:
                out[k] = (lo, hi)
        elif k in CALM:                       # Calm: ceilings at Windy's lower bounds; T0 <= 2; T6d unchanged
            out[k] = (None, 2.0) if k == "T0" else (None, hi) if k.startswith("T6d") else (None, lo * 0.5)
    return out


def load_runs(d, pose=False):
    doc = json.loads((d / "harness.json").read_text(encoding="utf-8"))
    runs = []
    for r in doc["runs"]:
        if pose and r["pilot"] != "nominal":
            continue
        c = ws.load(str(d / r["csv"]))
        if pose:                                  # the known-wrong noise on the pose, seeded per run
            rng = ws.random.Random(r["seed"])
            c["roll_deg"] = [v + rng.gauss(0, POSE_NOISE_DEG) for v in c["roll_deg"]]
            c["pitch_deg"] = [v + rng.gauss(0, POSE_NOISE_DEG) for v in c["pitch_deg"]]
        seg = tuple(int(v) for v in r["segment"].split("-")) if r.get("segment") else None
        runs.append(dict(meta=r, o=ws.analyse(c, seg)))
    return runs


def g(o, path, default=math.nan):
    try:
        for p in path:
            o = o[p]
        return math.nan if o is None else o
    except (KeyError, IndexError, TypeError):
        return default


def per_run(o):
    """Each target's quantity from one run's analysis (wind.md section 6.3's JSON fields)."""
    ins, out = o.get("segment", {}).get("inside", {}), o.get("segment", {}).get("outside", {})
    sp = o["spectra"]
    lat = o["airframe"]["lateral"]
    v = {
        "T0 mean": o["angle"][0]["mean"],
        "T0b spread": lat["std"] / lat["mean"] if lat["mean"] else math.nan,
        "T0b min": lat["min"],
        "T1": g(out, ("roll", "res_std")), "T2": g(out, ("pitch", "res_std")),
        "T3": g(ins, ("roll", "res_std")), "T4": g(ins, ("pitch", "res_std")),
        "T5 field": g(out, ("yaw", "res_std")), "T5 belt": g(ins, ("yaw", "res_std")),
        "T6a": sp["roll"]["bands"][0]["share"] + sp["roll"]["bands"][1]["share"],
        "T6b": sp["roll"]["bands"][3]["rms"],
        "T6c share": sp["pitch"]["bands"][2]["share"] + sp["pitch"]["bands"][3]["share"],
        "T6c rms": sp["pitch"]["bands"][2]["rms"],
        "T6d roll": math.hypot(sp["roll"]["bands"][4]["rms"], sp["roll"]["bands"][5]["rms"]),
        "T6d pitch": math.hypot(sp["pitch"]["bands"][4]["rms"], sp["pitch"]["bands"][5]["rms"]),
        "T7 rate": o["events"]["roll_or_pitch"]["per_min"],
        "roll events": o["events"]["roll"]["count"], "roll events belt": g(ins, ("roll", "events"), 0),
        "gaps": list(o["events"]["roll_or_pitch"].get("gaps_s") or []),
        "T9a": g(out, ("roll", "kurtosis")), "T9b": g(out, ("pitch", "kurtosis")),
        "T10a": o["corr"]["roll~pitch residual"]["r"], "T10b whole": o["corr"]["roll~yaw rate"]["r"],
        "T10b belt": g(ins, ("corr", "roll~yaw rate", "r")), "T10b field": g(out, ("corr", "roll~yaw rate", "r")),
        "T11a belt": g(ins, ("tilt", "res_std")), "T11a field": g(out, ("tilt", "res_std")),
        "T12 cv roll": g(out, ("roll", "block_cv")), "T12 cv pitch": g(out, ("pitch", "block_cv")),
        "T12 min roll": g(out, ("roll", "block_min")), "T12 min pitch": g(out, ("pitch", "block_min")),
        "belt roll": g(ins, ("roll", "res_std")), "belt yaw": g(ins, ("yaw", "res_std")), "k": o["airframe"]["k"],
    }
    return v


def nm(xs):
    xs = [x for x in xs if isinstance(x, (int, float)) and math.isfinite(x)]
    return sum(xs) / len(xs) if xs else math.nan


def readings(runs):
    """The level's readings: each field's average over the nominal runs, and the pooled ones."""
    nominal = [per_run(r["o"]) for r in runs if r["meta"]["pilot"] == "nominal"]
    v = {k: nm([x[k] for x in nominal]) for k in nominal[0] if k not in ("gaps",)}
    v["T0"] = abs(v["T0 mean"])
    v["T0 into wind"] = v["T0 mean"] < 0
    v["T0b windward runs"] = sum(1 for x in nominal if x["T0b min"] > 0)
    v["T0b runs"] = len(nominal)
    gaps = [gp for x in nominal for gp in x["gaps"]]
    v["T8 median"] = ws.percentile(gaps, 50) if gaps else math.nan
    v["T8 cv"] = ws.std(gaps) / ws.mean(gaps) if len(gaps) > 1 else math.nan
    v["T8 gaps"] = len(gaps)
    ev = sum(x["roll events"] for x in nominal)
    v["T7 belt share"] = sum(x["roll events belt"] for x in nominal) / ev if ev else math.nan
    v["T3 ratio"] = v["T3"] / v["T1"]
    v["T4 ratio"] = v["T4"] / v["T2"]
    v["T11a ratio"] = v["T11a belt"] / v["T11a field"]
    for s in ("s1", "s0"):
        var = [per_run(r["o"]) for r in runs if r["meta"]["pilot"] == s]
        if not var:
            continue
        if s == "s1":
            v["T11b s1"] = nm([x["belt roll"] / x["T11a belt"] for x in var])
        else:
            v["T11b s0"] = nm([x["belt yaw"] * x["k"] / x["T11a belt"] for x in var])
        v["T11b tilt " + s] = nm([x["T11a belt"] for x in var]) / v["T11a belt"]
    return v


def inside(x, band):
    lo, hi = band
    if not isinstance(x, (int, float)) or not math.isfinite(x):
        return False
    return (lo is None or x >= lo) and (hi is None or x <= hi)


def verdicts(v, level):
    """Each target's pass or fail on the readings, {name: (reading, band text, pass)}."""
    b = bands(level)
    out = {}
    for k, band in b.items():
        if k not in v:
            continue
        ok = inside(v[k], band)
        if k == "T0" and level != "calm":
            ok = ok and v["T0 into wind"]
        txt = (f"{band[0]:g}" if band[0] is not None else "") + ("-" if band[0] is not None and band[1] is not None else "") \
            + (f"{band[1]:g}" if band[1] is not None else "")
        txt = ("<= " + txt) if band[0] is None else (">= " + txt) if band[1] is None else txt
        out[k] = (v[k], txt, ok)
    if level != "calm":
        out["T0b windward"] = (v["T0b windward runs"], f">= {v['T0b runs'] - 1} of {v['T0b runs']}", v["T0b windward runs"] >= v["T0b runs"] - 1)
        out["T3 ratio"] = (v["T3 ratio"], ">= 1.8", inside(v["T3 ratio"], (1.8, None)))
        out["T4 ratio"] = (v["T4 ratio"], ">= 1.2", inside(v["T4 ratio"], (1.2, None)))
        out["T11a ratio"] = (v["T11a ratio"], ">= 1.8", inside(v["T11a ratio"], (1.8, None)))
        out["T10b belt > field"] = (v["T10b belt"] - v["T10b field"], "> 0", inside(v["T10b belt"] - v["T10b field"], (1e-12, None)))
    return out


def gauss_wrong(runs):
    """T9a, T9b and T12's readings on plain Gaussian noise built from the first nominal run's spectra (wind_stats.py's
    check, 5 logs per family, their means), as readings the verdicts grade."""
    o = next(r["o"] for r in runs if r["meta"]["pilot"] == "nominal" and "segment" in r["o"])
    res = ws.gauss_check(o, 5, quiet=True)
    out = {}
    for fam, rows in res.items():
        m = ws.gauss_verdict(rows)[0]["means"]
        out[fam] = {"T9a": m["field kurt roll"], "T9b": m["field kurt pitch"], "T12 cv roll": m["block cv roll"],
                    "T12 cv pitch": m["block cv pitch"], "T12 min roll": m["block min roll"], "T12 min pitch": m["block min pitch"]}
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("dir")
    ap.add_argument("level", choices=("severe", "windy", "calm"))
    ap.add_argument("--json")
    a = ap.parse_args(argv)
    ws.BOOT = 0                                   # intervals are not graded
    root = Path(a.dir)
    runs = load_runs(root / a.level)
    v = readings(runs)
    res = verdicts(v, a.level)
    wrong = {}
    for fam in ("nodrag", "steady", "nobelt", "noiseless"):
        d = root / f"{a.level}-{fam}"
        if (d / "harness.json").exists():
            wrong[fam] = verdicts(readings(load_runs(d)), a.level)
    wrong["pose"] = verdicts(readings(load_runs(root / a.level, pose=True)), a.level)
    if a.level == "calm" and (root / "severe" / "harness.json").exists():
        wrong["as severe"] = verdicts(readings(load_runs(root / "severe")), "calm")
    if a.level != "calm":
        b = bands(a.level)
        for fam, vals in gauss_wrong(runs).items():
            wrong[fam] = {k: (x, "", inside(x, b[k])) for k, x in vals.items()}
    print(f"{a.level}: {sum(1 for r in runs if r['meta']['pilot'] == 'nominal')} nominal runs; known-wrong families: {', '.join(wrong)}")
    print(f"  {'target':8s} {'condition':18s} {'reading':>9s}  {'band':14s} {'':4s}  known-wrong")
    passed = graded = 0
    allpass = allcaught = True
    table = []
    for name, conds, fam in TARGETS:
        fams = fam if isinstance(fam, tuple) else (fam,)
        if a.level == "calm" and fams != ("pose",):
            fams = ("as severe",)
        conds = [c for c in conds if c in res]
        if not conds:
            continue
        ok = all(res[c][2] for c in conds)
        # Each known-wrong family fails the target when any of its conditions is out; every flown one must fail it.
        each = {f: (None if wrong.get(f) is None or not any(c in wrong[f] for c in conds)
                    else any(not wrong[f][c][2] for c in conds if c in wrong[f])) for f in fams}
        flown = [x for x in each.values() if x is not None]
        caught = None if not flown else all(flown)
        graded += 1
        passed += ok
        allpass &= ok
        allcaught &= caught is not False
        for i, c in enumerate(conds):
            x, band, cok = res[c]
            xs = f"{x:9.3f}" if isinstance(x, float) else f"{x:>9}"
            parts = []
            for f in fams:
                w = wrong.get(f)
                if w is None or c not in w:
                    continue
                wx = w[c][0]
                parts.append((f"{f} {wx:.3f}" if isinstance(wx, float) else f"{f} {wx}") + (" in" if w[c][2] else " out"))
            wtxt = "; ".join(parts)
            last = i == len(conds) - 1
            tail = (f"  -> {'PASS' if ok else 'FAIL'}; known-wrong " + ", ".join(
                f"{f}: " + ("not flown" if each[f] is None else "fails" if each[f] else "PASSES") for f in fams))
            print(f"  {name if i == 0 else '':8s} {c:18s} {xs}  {band:14s} {'ok' if cok else 'out':4s}  {wtxt}{tail if last else ''}")
        table.append(dict(target=name, passed=ok, wrong=list(fams), wrong_fails=caught))
    print(f"{a.level}: {passed} of {graded} targets pass; "
          f"{'every' if allcaught else 'NOT every'} known-wrong model fails its target")
    if a.json:
        Path(a.json).write_text(json.dumps(ws.finite({"level": a.level, "readings": v, "verdicts": res, "table": table,
                                                       "wrong": wrong}), indent=1), encoding="utf-8")
    return 0 if allpass and allcaught else 1


if __name__ == "__main__":
    sys.exit(main())
