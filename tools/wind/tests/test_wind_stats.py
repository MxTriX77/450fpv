"""wind_stats.py: the definitions of wind.md section 1.4, and the Gaussian check of section 6.5 (TST-03)."""
import cmath
import csv
import json
import math
import random

import pytest

import wind_stats as ws

FPS = 29.917


def sim_log(roll, pitch, yaw_rate, flags=None):
    """A sim-style attitude log (conf 1, dup 0) as wind_stats.load returns it."""
    n = len(roll)
    lost = [f == "no_picture" for f in (flags or ["ok"] * n)]
    one = [1.0] * n
    return dict(frame=[float(i + 1) for i in range(n)], time_s=[i / FPS for i in range(n)], roll_deg=list(roll),
                pitch_deg=list(pitch), yaw_rate_dps=list(yaw_rate), conf_roll=one, conf_pitch=one, conf_yaw=one,
                edge_px=[math.nan] * n, dup=[False] * n, guarded=0, lost=lost)


def write_csv(path, rows):
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0]))
        w.writeheader()
        w.writerows(rows)


# ---------------------------------------------------------------- helpers against their numpy definitions

def test_fft_matches_the_dft():
    rng = random.Random(1)
    x = [complex(rng.gauss(0, 1), rng.gauss(0, 1)) for _ in range(64)]
    dft = [sum(v * cmath.exp(-2j * math.pi * k * j / 64) for j, v in enumerate(x)) for k in range(64)]
    assert max(abs(a - b) for a, b in zip(ws.fft(x), dft)) < 1e-9


def test_irfft_inverts_rfft():
    rng = random.Random(2)
    x = [rng.gauss(0, 1) for _ in range(128)]
    assert max(abs(a - b) for a, b in zip(ws.irfft(ws.rfft(x), 128), x)) < 1e-12


def test_percentile_is_numpys_linear():
    assert ws.percentile([1, 2, 3, 4], 50) == 2.5
    assert ws.percentile([10, 20, 30, 40, 50], 16) == pytest.approx(16.4)
    assert ws.percentile([math.nan, 3.0, 1.0], 100) == 3.0
    assert math.isnan(ws.percentile([math.nan], 50))


def test_runs_are_half_open_ranges():
    assert ws.runs([False, True, True, False, True]) == [(1, 3), (4, 5)]


# ---------------------------------------------------------------- wind.md section 1.4

def test_residual_corner_is_0_31_hz():
    assert ws.RES_CORNER_HZ == pytest.approx(0.31, abs=0.005)


def test_residual_removes_a_slope_up_to_the_run_ends():
    """The local-linear trend is unbiased by slopes near the run ends (wind.md 1.4)."""
    x = [3.0 + 0.05 * i for i in range(400)]
    r = ws.residual(x, [(0, 400)], FPS)
    v = ws.fin(r)
    assert len(v) == 400 - 2 * 17                    # one sigma (17 frames at 29.917 fps) dropped at each end
    assert max(abs(a) for a in v) < 1e-9


def test_residual_passes_half_the_amplitude_at_the_corner():
    n = 3000
    x = [math.sin(2 * math.pi * ws.RES_CORNER_HZ * i / FPS) for i in range(n)]
    r = ws.residual(x, [(0, n)], FPS)
    amp = math.sqrt(2) * ws.nanstd(r[200:-200])
    assert amp == pytest.approx(0.5, abs=0.02)


def test_rate_and_acceleration_are_5_point_fits():
    x = [0.5 * (i / FPS) ** 2 * 40.0 + 2.0 * i / FPS for i in range(50)]   # 40 deg/s^2, 2 deg/s at t = 0
    rate = ws.deriv(x, [(0, 50)], FPS, ws.D1, 1)
    acc = ws.deriv(x, [(0, 50)], FPS, ws.D2, 2)
    assert rate[10] == pytest.approx(2.0 + 40.0 * 10 / FPS)
    assert acc[10] == pytest.approx(40.0)
    assert math.isnan(rate[1]) and math.isnan(rate[48])


def test_frames_next_to_a_picture_loss_are_not_used(tmp_path):
    rows = [dict(frame=i + 1, time_s=f"{i / FPS:.4f}", roll_deg=-9, pitch_deg=-22, yaw_rate_dps=0, conf_roll=1,
                 conf_pitch=1, conf_yaw=1, dup=0, edge_px=3.5, flag="no_picture" if i == 50 else "ok")
            for i in range(100)]
    write_csv(tmp_path / "log.csv", rows)
    c = ws.load(tmp_path / "log.csv")
    assert c["guarded"] == 4                                        # LOSS_GUARD = 2 frames on each side
    assert [i for i, v in enumerate(c["conf_roll"]) if v == 0] == [48, 49, 50, 51, 52]


def test_events_merge_across_short_gaps_and_count_per_minute():
    x = [0.0] * 600
    rng = random.Random(3)
    x = [rng.gauss(0, 0.1) for _ in x]
    for a in (100, 101, 104, 300):                  # 100-101 and 104 lie 2 frames apart (<= EVENT_GAP): one event
        x[a] = 5.0
    e = ws.events(x, FPS)
    assert e["count"] == 2
    assert e["per_min"] == pytest.approx(2 / (600 / FPS / 60))


# ---------------------------------------------------------------- a whole log

def gauss_log(n=1500, seed=4):
    rng = random.Random(seed)
    roll = [-9.0 + 0.5 * rng.gauss(0, 1) for _ in range(n)]
    pitch = [-22.7 + 0.3 * rng.gauss(0, 1) for _ in range(n)]
    yaw = [2.0 * rng.gauss(0, 1) for _ in range(n)]
    return sim_log(roll, pitch, yaw)


def test_analyse_reports_the_lean_spectra_and_segment():
    ws.BOOT, was = 20, ws.BOOT
    try:
        o = ws.analyse(gauss_log(), (300, 510))
    finally:
        ws.BOOT = was
    assert o["angle"][0]["mean"] == pytest.approx(-9.0, abs=0.05)
    assert o["airframe"]["k"] == pytest.approx(-math.sin(math.radians(-22.7)), abs=1e-3)
    assert [b["band"] for b in o["spectra"]["roll"]["bands"]] == list(ws.BANDS)
    assert sum(b["share"] for b in o["spectra"]["roll"]["bands"]) == pytest.approx(1.0)
    assert o["segment"]["inside"]["roll"]["frames"] == 211
    assert o["segment"]["outside"]["roll"]["block_cv"] < 0.32           # white noise never goes calm
    assert 2.2 <= o["segment"]["outside"]["roll"]["kurtosis"] <= 3.8


def test_cli_writes_json_with_null_for_nan(tmp_path):
    c = gauss_log(400)
    rows = [dict(frame=int(f), time_s=t, roll_deg=r, pitch_deg=p, yaw_rate_dps=y, conf_roll=1, conf_pitch=1,
                 conf_yaw=1, dup=0, flag="ok") for f, t, r, p, y in
            zip(c["frame"], c["time_s"], c["roll_deg"], c["pitch_deg"], c["yaw_rate_dps"])]
    write_csv(tmp_path / "run.csv", rows)
    ws.BOOT, was = 0, ws.BOOT
    try:
        assert ws.main(["--csv", str(tmp_path / "run.csv"), "--json", str(tmp_path / "run.json")]) == 0
    finally:
        ws.BOOT = was
    o = json.loads((tmp_path / "run.json").read_text(encoding="utf-8"))
    assert o["edge"]["mean"] is None                                    # no edge column: NaN -> null
    assert o["frames"] == 400


# ---------------------------------------------------------------- the Gaussian check (TST-03, wind.md 6.5)

def test_verdict_passes_p_and_grades_the_bands():
    """The verdict is not a constant 'fail': P's own values (K 8.6) pass T9a, T9b, T12 and T10b."""
    p = [4.74, 3.81, 2.51, 3.85, 0.17, 0.23, 0.65, 0.62, 0.51, 1.26, 0.57]
    g = ws.gauss_verdict([p] * 5)[0]
    assert not (g["fails_T9"] or g["fails_T12"] or g["fails_T10b"])
    for i, bad in ((2, 3.51), (2, 2.19), (3, 3.29), (3, 5.51), (4, 0.33), (5, 0.33), (6, 0.49), (7, 0.49), (10, 0.34)):
        q = list(p)
        q[i] = bad
        g = ws.gauss_verdict([q] * 5)[0]
        assert g["fails_T9"] or g["fails_T12"] or g["fails_T10b"], (i, bad)


def test_published_p_spectra_keep_the_band_power():
    import clip_p
    df = clip_p.FPS / clip_p.WELCH_N
    for axis, rms in clip_p.BAND_RMS.items():
        psd = clip_p.psd(axis)
        for (lo, hi), r in zip(clip_p.BANDS, rms):
            assert math.sqrt(sum(p for f, p in psd if lo <= f < hi) * df) == pytest.approx(r)


def test_plain_gaussian_noise_fails_t9_and_t12():
    """The spec's 'tools reject noise' half: noise with P's spectra and spreads (published numbers) fails T9 in both
    families, T12 when it alternates calm and busy, and T10b in both (wind.md 6.5)."""
    import clip_p
    res = ws.gauss_check(clip_p.summary(), 5, quiet=True)
    stat, busy = ws.gauss_verdict(res["stationary"])[0], ws.gauss_verdict(res["calm/busy"])[0]
    assert stat["fails_T9b"] and stat["fails_T10b"]
    assert busy["fails_T9a"] and busy["fails_T12"] and busy["fails_T10b"]
    # the noise has P's spreads (T1, T3 of K 8.6 pass), so only the shape of the wobble can fail it
    assert 0.35 <= stat["means"]["field res roll"] <= 0.70 and 0.85 <= stat["means"]["belt res roll"] <= 1.70
