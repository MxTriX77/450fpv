"""extract_frames.py against the spec scenarios (openspec/changes/setup-reference-tools)."""
import json
import shutil

import pytest

import extract_frames
from conftest import CLIP_FPS, SECRET, run, snapshot


def index(root, letter="A"):
    return json.loads((root / "reference" / "_frames" / letter / "index.json").read_text(encoding="utf-8"))


def test_two_fps_over_ten_seconds(root, clip):
    assert run(extract_frames, clip, "--letter", "A", "--fps", 2, "--root", root) == 0
    stills = index(root)["stills"]
    assert 19 <= len(stills) <= 21
    times = [s["t_s"] for s in stills]
    assert times == sorted(set(times)) and times[0] == 0.0
    names = sorted(p.name for p in (root / "reference" / "_frames" / "A").glob("*.png"))
    assert names == sorted(s["file"] for s in stills) == [f"A_{i:04d}.png" for i in range(1, len(stills) + 1)]


def test_interval_start_and_duration(root, clip):
    assert run(extract_frames, clip, "--letter", "B", "--interval", 1, "--start", 2.3, "--duration", 5, "--root", root) == 0
    times = [s["t_s"] for s in index(root, "B")["stills"]]
    assert len(times) == 5
    assert all(2.3 <= t < 7.3 for t in times) and times == sorted(times)
    # every still sits on a clip frame, at or just after its tick
    assert all(abs(t * CLIP_FPS - round(t * CLIP_FPS)) < 1e-6 for t in times)
    assert all(0 <= t - (2.3 + k) < 1 / CLIP_FPS for k, t in enumerate(times))


def test_rerun_replaces_previous_stills(root, clip):
    assert run(extract_frames, clip, "--letter", "A", "--fps", 2, "--root", root) == 0
    assert run(extract_frames, clip, "--letter", "A", "--fps", 1, "--root", root) == 0
    pngs = list((root / "reference" / "_frames" / "A").glob("*.png"))
    assert len(pngs) == len(index(root)["stills"]) == 10


@pytest.mark.parametrize("out", ["outside", "reference/x", "reference/_frames", "reference/_frames/../../escape"])
def test_output_outside_frames_is_refused(root, clip, out, capsys):
    before = snapshot(root)
    target = root / out
    assert run(extract_frames, clip, "--letter", "A", "--fps", 2, "--out", target, "--root", root) != 0
    assert snapshot(root) == before
    assert "reference/_frames" in capsys.readouterr().err


def test_relative_dotdot_output_is_refused(root, clip, monkeypatch):
    monkeypatch.chdir(root / "reference" / "_frames")
    before = snapshot(root)
    assert run(extract_frames, clip, "--letter", "A", "--fps", 2, "--out", "../../A", "--root", root) != 0
    assert snapshot(root) == before


def test_input_outside_reference_is_refused(root, clip, capsys):
    outside = root / clip.name
    shutil.copy(clip, outside)
    before = snapshot(root)
    assert run(extract_frames, outside, "--letter", "A", "--fps", 2, "--root", root) != 0
    assert snapshot(root) == before
    assert "under reference/" in capsys.readouterr().err


@pytest.mark.parametrize("bad", ["a", "AB", "../A", ""])
def test_bad_letter_is_refused(root, clip, bad):
    before = snapshot(root)
    assert run(extract_frames, clip, "--letter", bad, "--fps", 2, "--root", root) != 0
    assert snapshot(root) == before


def test_missing_ffmpeg_fails_clearly(root, clip, monkeypatch, capsys):
    monkeypatch.setattr(extract_frames.shutil, "which", lambda name: None)
    assert run(extract_frames, clip, "--letter", "A", "--fps", 2, "--root", root) != 0
    assert "ffmpeg not found" in capsys.readouterr().err


def test_ffmpeg_failure_does_not_echo_the_clip_name(root, capsys):
    """ffmpeg's log names its input; the error shown must not."""
    bad = root / "reference" / SECRET / f"{SECRET}.mp4"
    bad.parent.mkdir()
    bad.write_bytes(b"not a video")
    assert run(extract_frames, bad, "--letter", "A", "--fps", 2, "--root", root) != 0
    err = capsys.readouterr().err
    assert "ffmpeg failed" in err and SECRET.lower() not in err.lower()
