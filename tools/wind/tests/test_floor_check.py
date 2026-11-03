"""floor_check.py on synthetic tracker logs: a floor of fit-independent noise (the known-wrong reading of a tracker
floor, as real motion would be) reads flat across the fit's changes; glitches tied to the fit's inlier changes read
as such; the strip check returns the planted horizon pitch noise. No test opens the real reference/ folder."""
import csv
import math
import random

import floor_check as fc

COLUMNS = ("frame", "time_s", "roll_deg", "pitch_deg", "yaw_rate_dps", "conf_roll", "conf_pitch", "conf_yaw", "dup",
           "n_cols", "inlier_frac", "contrast", "span", "resid_px", "edge_px", "yaw_peak", "yaw_unique", "yaw_del_deg", "flag")
FPS, N = 29.917, 1400


def write(path, glitch, seed=1, pitch_noise=0.02):
    rng = random.Random(seed)
    inlier = [min(1.0, max(0.5, 0.9 + rng.uniform(-0.03, 0.03) * (1 + 3 * (rng.random() < 0.2)))) for _ in range(N)]
    span = [min(1.0, max(0.3, 0.95 + rng.uniform(-0.02, 0.02))) for _ in range(N)]
    noise = [rng.gauss(0, pitch_noise) for _ in range(N)]
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(COLUMNS)
        for i in range(N):
            t = i / FPS
            motion = sum(a * math.sin(2 * math.pi * hz * t + hz) for a, hz in ((1.0, 0.4), (0.5, 0.8), (0.2, 1.5), (0.1, 3.0)))
            prev, nxt = inlier[max(i - 1, 0)], inlier[min(i + 1, N - 1)]
            change = abs(inlier[i] - 0.5 * (prev + nxt))
            # Glitches: the fit's inlier set jumps and the line with it (4 deg per unit of share, either way); the
            # known-wrong floor: the same power as fit-independent white noise.
            extra = (4.0 * change if rng.random() < 0.5 else -4.0 * change) if glitch else rng.gauss(0, 0.12)
            roll = motion + extra
            pitch = -22.7 + 0.3 * motion + noise[i]
            yaw_del = "" if i == 0 else f"{noise[i] - noise[i - 1]:.5f}"
            w.writerow((i + 1, f"{t:.4f}", f"{roll:.5f}", f"{pitch:.5f}", "" if i == 0 else "0.0", "1", "1", "0" if i == 0 else "1",
                        "0", 960, f"{inlier[i]:.4f}", 100, f"{span[i]:.4f}", 4.0, 3.5, "", "", yaw_del, "ok"))


def test_a_fit_independent_floor_reads_flat_across_the_fits_changes(tmp_path):
    p = tmp_path / "noise.csv"
    write(p, glitch=False)
    roll = fc.analyse(p)["axes"]["roll"]
    q = roll["by_inlier_change"]
    assert 0.8 < q[3] / q[0] < 1.25
    assert roll["hf_kurtosis"] < 3.6
    assert abs(roll["hf_white"] - 0.12) < 0.02


def test_glitches_tied_to_the_fit_read_as_the_trackers(tmp_path):
    p = tmp_path / "glitch.csv"
    write(p, glitch=True)
    roll = fc.analyse(p)["axes"]["roll"]
    q = roll["by_inlier_change"]
    assert q[3] / q[0] > 2
    assert roll["hf_kurtosis"] > 4


def test_the_strip_check_returns_the_horizons_pitch_noise(tmp_path):
    p = tmp_path / "noise.csv"
    write(p, glitch=False, pitch_noise=0.02)
    s = fc.analyse(p)["strip"]
    assert abs(s["pitch_noise_robust"] - 0.02) < 0.003
    assert abs(s["pitch_noise_std"] - 0.02) < 0.003
