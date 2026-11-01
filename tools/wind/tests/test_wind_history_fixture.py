"""wind_history_fixture.py and wind_stats.wind_history: T0c (K 8.6) and its checks (wind.md 6.6, TST-03)."""
import math

import pytest

import wind_history_fixture as fx
import wind_stats as ws


def log(dirs, p0=200.0, dt=1.0):
    return [i * dt for i in range(len(dirs))], list(dirs), [p0] * len(dirs)


def steps_at(onsets, n=7201, size=60.0, p0=200.0):
    """A wind that steps by `size` deg at each onset (s) and stays there until the next one, alternating sides."""
    d, k = [], 0
    for i in range(n):
        while k < len(onsets) and i >= onsets[k]:
            k += 1
        d.append(p0 + (size if k % 2 else 0.0))
    return d


# ---------------------------------------------------------------- the T0c definitions on hand-built histories

def test_one_step_is_one_shift_and_close_crossings_merge():
    o = ws.wind_history([log(steps_at([1000]))])
    assert o["shifts"]["count"] == 1
    o = ws.wind_history([log(steps_at([1000, 1020]))])        # back again 20 s later: one stretch, one shift
    assert o["shifts"]["count"] == 1
    o = ws.wind_history([log(steps_at([1000, 1100]))])        # 100 s apart: two shifts
    assert o["shifts"]["count"] == 2


def test_a_schedule_piles_up_periodicity_power():
    """Onsets every 300 s over 120 min: 24 onsets in phase at 1/300 Hz (and its harmonics up to 1/75 Hz) give
    |sum|^2 / count = 24, ten times the 2.4 limit."""
    o = ws.wind_history([log(steps_at([300 * k + 100 for k in range(24)]))])
    assert o["shifts"]["count"] == 24
    assert o["shifts"]["peak"] == pytest.approx(24.0, rel=1e-6)
    h = 300.0 / o["shifts"]["peak_period_s"]
    assert h == pytest.approx(round(h), abs=1e-6) and 1 <= round(h) <= 4
    assert o["shifts"]["gap_cv"] == pytest.approx(0.0, abs=1e-9)


def test_side_keep_and_spread_of_a_fixed_vector():
    o = ws.wind_history([log([200.0] * 7201)])
    assert o["side_keep"] == 1.0 and o["spread_deg"] == 0.0 and o["shifts"]["count"] == 0
    assert math.isnan(o["shifts"]["gap_cv"])
    o = ws.wind_history([log([200.0] * 3600 + [200.0 + 120.0] * 3601)])   # half the time beyond 90 deg
    assert o["side_keep"] == pytest.approx(3561 / 7162, abs=1e-3)


def test_offset_wraps_round_north():
    o = ws.wind_history([log([355.0] * 100 + [5.0] * 100, p0=0.0)])
    assert o["offset_deg"] == pytest.approx(0.0, abs=1e-9)
    assert o["seeds"][0]["spread_deg"] == pytest.approx(5.0)


def test_t0c_bands_are_the_specs_literal_numbers():
    """K 8.6 / the spec's 'direction history' scenario, at and just past each limit."""
    base = dict(draws=dict(resultant=0.7), offset_deg=15.0, spread_deg=20.0, seeds=[dict(spread_deg=10.0)],
                shifts=dict(per_min=0.1, gap_cv=0.5, peak=2.4), side_keep=0.75)
    assert all(fn(base) for fn in fx.T0C.values())
    for key, bad in (("draws", 0.71), ("offset", 15.1), ("spread", 19.9), ("spread", 60.1), ("min seed", 9.9),
                     ("rate", 0.09), ("rate", 0.41), ("gap_cv", 0.49), ("peak", 2.41), ("side", 0.74), ("side", 0.99)):
        o = {k: (dict(v) if isinstance(v, dict) else list(v) if isinstance(v, list) else v) for k, v in base.items()}
        o["shifts"] = dict(base["shifts"])
        if key == "draws":
            o["draws"] = dict(resultant=bad)
        elif key == "offset":
            o["offset_deg"] = bad
        elif key == "spread":
            o["spread_deg"] = bad
        elif key == "min seed":
            o["seeds"] = [dict(spread_deg=bad)]
        elif key == "rate":
            o["shifts"]["per_min"] = bad
        elif key == "gap_cv":
            o["shifts"]["gap_cv"] = bad
        elif key == "peak":
            o["shifts"]["peak"] = bad
        else:
            o["side_keep"] = bad
        assert not all(fn(o) for fn in fx.T0C.values()), (key, bad)


# ---------------------------------------------------------------- the fixture's models

def test_meander_has_unit_std_and_the_lag():
    rng = fx.random.Random(5)
    w = [rng.gauss(0, 1) for _ in range(fx.BURN + 200000)]
    m = fx.meander(w, fx.LAG_S)
    assert len(m) == 200000
    assert ws.std(m) == pytest.approx(1.0, abs=0.08)        # about 1000 OU time constants: a few % of sampling error


def test_the_starting_model_passes_t0c_in_every_one_of_40_seed_sets():
    """The spec's scenario: K 8.3's starting model passes T0c in every one of 40 seed sets (40 seeds x 120 min each)."""
    ok = fx.check(["random"], 40, quiet=True)["random"]
    assert len(ok) == 40 and all(all(row) for row in ok)


@pytest.mark.parametrize("model", ["sched3", "sched3-jitter30", "sched5", "sched8", "sched10", "sched3-unsmoothed",
                                   "sched5-unsmoothed", "sched8-unsmoothed", "sched10-unsmoothed"])
def test_shifts_on_a_fixed_schedule_fail_t0c(model):
    ok = fx.check([model], 3, quiet=True)[model]
    peak = list(fx.T0C).index("peak")
    assert not any(all(row) for row in ok)
    assert not any(row[peak] for row in ok)                  # the periodicity peak catches every schedule


@pytest.mark.parametrize("model,condition", [("one-direction", "draws"), ("fixed", "spread"), ("fixed-jitter5", "spread")])
def test_the_other_controls_fail_t0c(model, condition):
    ok = fx.check([model], 1, quiet=True)[model]
    assert not ok[0][list(fx.T0C).index(condition)]


def test_written_logs_give_the_in_memory_numbers(tmp_path):
    """wind.md 6.6: on set 0 the command-line path gives the in-memory numbers, to the CSVs' 4 decimals."""
    assert fx.main(["--write", str(tmp_path), "--set", "0", "--seeds", "5", "--minutes", "30"]) == 0
    files = sorted(tmp_path.glob("seed_*.csv"))
    assert len(files) == 5
    a = ws.wind_history([ws.load_wind(f) for f in files])
    b = ws.wind_history(fx.seed_sets([0], 5, 30.0)[0])
    assert a["shifts"]["count"] == b["shifts"]["count"]
    for key in ("spread_deg", "offset_deg", "side_keep"):
        assert a[key] == pytest.approx(b[key], abs=1e-3)
    assert a["shifts"]["peak"] == pytest.approx(b["shifts"]["peak"], abs=1e-3)


def test_a_log_must_hold_one_drawn_direction(tmp_path):
    (tmp_path / "bad.csv").write_text("time_s,wind_from_deg,prevailing_from_deg\n0,1,10\n1,2,11\n", encoding="utf-8")
    with pytest.raises(ValueError):
        ws.load_wind(tmp_path / "bad.csv")


def _write_set(folder, model, first, seeds=40):
    """One seed set of a fixture model as a simulator writes its histories: seed-<n>.csv, n from first."""
    logs = fx.seed_sets([first], seeds, fx.MINUTES, **fx.options(model))[0]
    for s, (t, d, p) in enumerate(logs):
        rows = "".join(f"{a:.1f},{b:.4f},{c:.4f}\n" for a, b, c in zip(t, d, p))
        (folder / f"seed-{first * seeds + s + 1}.csv").write_text("time_s,wind_from_deg,prevailing_from_deg\n" + rows, encoding="utf-8")


def test_grade_reads_a_simulators_histories_in_sets(tmp_path):
    """--grade (p7c-wind 1.4): a simulator's seed-<n>.csv grouped by n into sets of 40; the starting model passes every
    set, a schedule fails, a short last set is left out."""
    good, bad = tmp_path / "good", tmp_path / "bad"
    good.mkdir()
    bad.mkdir()
    for i in range(2):
        _write_set(good, "random", i)
    _write_set(bad, "sched5", 0)
    (good / "seed-81.csv").write_text((good / "seed-1.csv").read_text(encoding="utf-8"), encoding="utf-8")
    ok = fx.grade(good)
    assert len(ok) == 2 and all(all(row) for row in ok)
    assert fx.main(["--grade", str(good)]) == 0
    assert fx.main(["--grade", str(bad)]) == 1
