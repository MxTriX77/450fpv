"""frame_metrics.py: one tool, alike on a lettered clip and a folder of frames (FEED-12, D-037)."""
import csv
import math
import random
import shutil
import subprocess

import pytest
from PIL import Image

import frame_metrics
from conftest import CLIP_FPS, CLIP_S, SECRET, run, snapshot


@pytest.fixture
def lettered(root):
    """The synthetic clip cited as letter A, and a photo as letter B, in the local letter map."""
    (root / "reference" / f"{SECRET}.jpg").write_bytes(b"x")
    (root / "reference" / "_frames" / "letters.md").write_text(
        f"| Letter | Kind | File |\n|---|---|---|\n| A | clip | `{SECRET}.mp4` |\n| B | photo | `{SECRET}.jpg` |\n",
        encoding="utf-8")
    return root


def metrics_dir(root, name):
    return root / "reference" / "_compare" / "metrics" / name


def table(root, name):
    with open(metrics_dir(root, name) / "metrics.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def noisy_frames(folder, n=40, size=(64, 48), sigma=2.0, glitch=None):
    """n grey frames with seeded Gaussian grain; frame `glitch` (1-based) has 6x the grain."""
    folder.mkdir()
    rnd = random.Random(7)
    for i in range(1, n + 1):
        s = sigma * (6 if i == glitch else 1)
        px = [max(0, min(255, round(120 + rnd.gauss(0, s)))) for _ in range(size[0] * size[1])]
        Image.frombytes("L", size, bytes(px)).convert("RGB").save(folder / f"f{i:03d}.png")
    return folder


def test_clip_by_letter(lettered, capsys):
    assert run(frame_metrics, "--letter", "A", "--root", lettered) == 0
    rows = table(lettered, "A")
    assert len(rows) == CLIP_S * CLIP_FPS
    assert list(rows[0]) == list(frame_metrics.COLUMNS)
    assert [r["frame"] for r in rows] == [str(i) for i in range(1, len(rows) + 1)]
    assert [float(r["time_s"]) for r in rows] == pytest.approx([i / CLIP_FPS for i in range(len(rows))])
    assert float(rows[0]["diff"]) == 0 and all(float(r["noise"]) > 0 for r in rows)
    assert "frame_metrics: A: 320x240, 250 frames" in capsys.readouterr().out


def test_bursts_are_native_consecutive_frames(lettered):
    assert run(frame_metrics, "--letter", "A", "--root", lettered) == 0
    bursts = sorted(p.name for p in (metrics_dir(lettered, "A") / "bursts").glob("*.png"))
    n = CLIP_S * CLIP_FPS
    assert bursts == sorted(f"{b}_{i:05d}.png" for b, s in [(1, 1), (2, 1 + (n - 8) // 2), (3, n - 7)]
                            for i in range(s, s + 8))
    with Image.open(metrics_dir(lettered, "A") / "bursts" / bursts[0]) as im:
        assert im.size == (320, 240)


def test_frames_folder_measures_like_the_clip(lettered, tmp_path):
    """The same frames, decoded once from the clip and once from PNGs, give the same numbers."""
    folder = tmp_path / "pngs"
    folder.mkdir()
    subprocess.run([shutil.which("ffmpeg"), "-hide_banner", "-loglevel", "error", "-i",
                    str(lettered / "reference" / f"{SECRET}.mp4"), "-fps_mode", "passthrough",
                    "-pix_fmt", "rgb24", str(folder / "%05d.png")], check=True)
    assert run(frame_metrics, "--letter", "A", "--root", lettered) == 0
    assert run(frame_metrics, "--frames", folder, "--name", "feed-test", "--fps", CLIP_FPS, "--root", lettered) == 0
    assert table(lettered, "feed-test") == table(lettered, "A")
    burst = "2_00122.png"
    with Image.open(metrics_dir(lettered, "A") / "bursts" / burst) as a, Image.open(folder / "00122.png") as b:
        assert a.tobytes() == b.convert("RGB").tobytes()  # lossless


def test_a_one_frame_glitch_is_flagged_and_exported(lettered, tmp_path):
    folder = noisy_frames(tmp_path / "seq", glitch=20)
    assert run(frame_metrics, "--frames", folder, "--name", "glitch", "--fps", 50, "--root", lettered) == 0
    rows = table(lettered, "glitch")
    assert "noise" in rows[19]["flag"]
    assert [r["frame"] for r in rows if "noise" in r["flag"]] == ["20"]
    events = sorted(p.name for p in (metrics_dir(lettered, "glitch") / "events").glob("*.png"))
    assert {f"{i:05d}.png" for i in range(18, 23)} <= set(events)
    with Image.open(metrics_dir(lettered, "glitch") / "events" / "00020.png") as a, \
            Image.open(folder / "f020.png") as b:
        assert a.tobytes() == b.convert("RGB").tobytes()
    assert float(rows[5]["noise"]) == pytest.approx(2.0 * math.sqrt(8 / 9), rel=0.15)  # sigma * sqrt(1 - 1/9)


def test_local_jump_rule_ignores_smooth_motion_and_catches_a_spike():
    rnd = random.Random(3)
    values = [20 + 15 * (1 - math.cos(2 * math.pi * i / 60)) + rnd.uniform(-0.5, 0.5) for i in range(120)]
    flagged, _ = frame_metrics.local_jumps(values, 10)
    assert not any(flagged)  # a sustained, smooth rise (fast motion) moves the neighbours too
    values[70] += 8
    flagged, threshold = frame_metrics.local_jumps(values, 10)
    assert [i for i, f in enumerate(flagged) if f] == [70] and threshold < 8


def test_rerun_replaces_the_outputs(lettered, tmp_path):
    glitchy = noisy_frames(tmp_path / "a", glitch=20)
    clean = noisy_frames(tmp_path / "b")
    assert run(frame_metrics, "--frames", glitchy, "--name", "seq", "--fps", 50, "--root", lettered) == 0
    assert run(frame_metrics, "--frames", clean, "--name", "seq", "--fps", 50, "--root", lettered) == 0
    rows = table(lettered, "seq")
    flagged = [int(r["frame"]) for r in rows if r["flag"] != "none"]
    expected = {f"{j:05d}.png" for f in flagged for j in range(max(1, f - 2), min(len(rows), f + 2) + 1)}
    assert {p.name for p in (metrics_dir(lettered, "seq") / "events").glob("*.png")} == expected
    assert "00020.png" not in expected


def test_nothing_carries_the_clip_name(lettered, capsys):
    assert run(frame_metrics, "--letter", "A", "--root", lettered) == 0
    out = capsys.readouterr()
    written = [p for p in (lettered / "reference" / "_compare").rglob("*")]
    assert written and all(SECRET.lower() not in p.name.lower() for p in written)
    assert SECRET not in (metrics_dir(lettered, "A") / "metrics.csv").read_text(encoding="utf-8")
    assert SECRET.lower() not in (out.out + out.err).lower()


@pytest.mark.parametrize("argv, message", [
    (["--letter", "B"], "photo"),
    (["--letter", "Q"], "not in"),
])
def test_bad_letters_are_refused(lettered, argv, message, capsys):
    before = snapshot(lettered)
    assert run(frame_metrics, *argv, "--root", lettered) != 0
    assert snapshot(lettered) == before
    err = capsys.readouterr().err
    assert message in err and SECRET.lower() not in err.lower()


@pytest.mark.parametrize("name", ["A", "a", "Feed", "../x", "x/y", ""])
def test_bad_names_are_refused(lettered, tmp_path, name):
    folder = noisy_frames(tmp_path / "seq", n=4)
    before = snapshot(lettered)
    assert run(frame_metrics, "--frames", folder, "--name", name, "--fps", 50, "--root", lettered) != 0
    assert snapshot(lettered) == before


def test_ffmpeg_failure_does_not_echo_the_clip_name(lettered, capsys):
    (lettered / "reference" / f"{SECRET}.mp4").write_bytes(b"not a video")
    assert run(frame_metrics, "--letter", "A", "--root", lettered) != 0
    err = capsys.readouterr().err
    assert "failed" in err and SECRET.lower() not in err.lower()
