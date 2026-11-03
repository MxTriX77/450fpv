"""motion_bands.py on synthetic track_motion logs: a planted turn lands in its band at its RMS, the checkerboard noise
and the field pick land where they belong, and glitches tied to the fit's changes read as the tracker's while a
fit-independent floor does not (floor_check.py's test). No test opens the real reference/ folder."""
import csv
import math
import random

import motion_bands as mb
import track_motion as tm

FPS, N = 30.0, 900


def write(path, roll, noise=0.0, glitch=0.0, seed=1):
    """A log of N frames: the roll turn per frame from roll(t) (deg), the checkerboard halves carrying +-noise each,
    pitch and yaw still; with glitch > 0 every frame's roll also jumps by glitch x its inlier-share change."""
    rng = random.Random(seed)
    inl = [0.9 + rng.uniform(-0.03, 0.03) * (1 + 3 * (rng.random() < 0.2)) for _ in range(N)]
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(tm.COLUMNS)
        for k in range(1, N + 1):
            t = (k - 1) / FPS
            row = dict(frame=k, time_s=f"{t:.5f}", dup=0, flag="ok", conf_roll=1, conf_pitch=1, conf_yaw=1,
                       n_fields=200, inlier_frac=inl[k - 1], resid_px=1.0, row_wobble_px=0.3, zoom=0.0)
            if k > 1:
                d = roll(t) - roll(t - 1 / FPS)
                e = rng.gauss(0, noise)
                if glitch:
                    i = k - 1
                    ch = inl[i] - 0.5 * (inl[i - 1] + inl[min(i + 1, N - 1)])
                    d += glitch * ch * (1 if rng.random() < 0.5 else -1)
                row.update(d_roll_deg=d + e, d_roll_a=d + 2 * e, d_roll_b=d, d_roll_top=d + 2 * e, d_roll_bot=d)
                for ax in ("pitch", "yaw"):
                    row.update({f"d_{ax}_{s}": 0.0 for s in ("deg", "a", "b", "top", "bot")})
            w.writerow([row.get(c, "") for c in tm.COLUMNS])


def roll_of(o, seg="all"):
    return o["segments"][seg]["axes"]["roll"]


def test_a_planted_turn_lands_in_its_band(tmp_path):
    p = tmp_path / "m.csv"
    write(p, lambda t: 0.10 * math.sqrt(2) * math.sin(2 * math.pi * 2.5 * t) + 0.05 * math.sqrt(2) * math.sin(2 * math.pi * 7.0 * t))
    a = roll_of(mb.analyse(mb.load(p), [("all", 1, N)], timing=False))
    assert a["angle"][0] < 0.01
    assert abs(a["angle"][1] - 0.10) < 0.01                      # 2.5 Hz, RMS 0.10 deg
    assert abs(a["angle"][2] - 0.05) < 0.005                     # 7 Hz, RMS 0.05 deg
    assert abs(a["rate"][2] - 0.05 * 2 * math.pi * 7.0) < 0.2    # its rate, deg/s


def test_the_checkerboard_noise_is_the_planted_noise(tmp_path):
    p = tmp_path / "m.csv"
    write(p, lambda t: 0.0, noise=0.01)
    a = roll_of(mb.analyse(mb.load(p), [("all", 1, N)], timing=False))
    for got, noise in zip(a["angle"], a["noise_angle"]):
        assert abs(got - noise) < 0.25 * got                     # (a - b) / 2 carries the full estimate's noise


def test_the_field_pick_is_the_rate_times_its_lag():
    sigma = 0.02
    total = math.sqrt(sum(v * v for v in mb.white_angle_bands(sigma, 64, FPS)[0]))
    assert abs(total - sigma * math.sqrt(1 - 0.4 / 15)) < 0.002   # white over 0.4-15 Hz


def test_glitches_tied_to_the_fit_read_as_the_trackers(tmp_path):
    p = tmp_path / "g.csv"
    write(p, lambda t: 0.0, glitch=4.0)
    a = roll_of(mb.analyse(mb.load(p), [("all", 1, N)], timing=False))
    q = a["h_by"]["inlier_frac"]
    assert q[3] / q[0] > 2 and a["glitch_floor"] > 0.5


def test_a_fit_independent_floor_reads_flat(tmp_path):
    p = tmp_path / "n.csv"
    write(p, lambda t: 0.0, noise=0.03)
    a = roll_of(mb.analyse(mb.load(p), [("all", 1, N)], timing=False))
    q = a["h_by"]["inlier_frac"]
    assert 0.75 < q[3] / q[0] < 1.33 and a["h_kurtosis"] < 3.6


def test_segments_parse_and_bad_ones_are_refused():
    assert mb.parse_segment("hover=2-180") == ("hover", 2, 180)
    for bad in ("hover", "=2-3", "a=3-2", "a=x-3"):
        try:
            mb.parse_segment(bad)
        except Exception:
            continue
        raise AssertionError(bad)


def test_the_recorders_field_steps_are_found_and_undone(tmp_path):
    """A steady 10 deg/s roll seen by a 29.97 fps recorder of 50 fields a second: each frame shows the newest field, so
    the turns run 2, 2, 1 fields (0.4, 0.4, 0.2 deg); retiming makes them even again."""
    p = tmp_path / "m.csv"
    q = 50 / 29.97
    write(p, lambda t: 10.0 * math.floor(t * FPS * q + 0.3) / 50.0)
    c = mb.load(p)
    fits = mb.retime(c, FPS)
    assert all(used and gain < 0.1 for *_, gain, used in fits)
    d = [v for v in c["d_roll_deg"][1:] if v == v]
    assert max(d) - min(d) < 0.02 and abs(sum(d) / len(d) - 10.0 / FPS) < 0.01
