"""Fixtures: a fake repo root in tmp_path with its own reference/, and a synthetic ffmpeg testsrc clip.
No test opens the real reference/ folder."""
import shutil
import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

SECRET = "QxSecretClipName7731"  # a distinctive source name that must never reach an output
CLIP_S, CLIP_FPS = 10, 25


@pytest.fixture(scope="session")
def synthetic_clip(tmp_path_factory):
    """A 10 s 25 fps 320x240 testsrc clip whose file name and title tag carry SECRET."""
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg is None:
        pytest.fail("ffmpeg not found on PATH; the reference tools need it (docs/environment.md)")
    clip = tmp_path_factory.mktemp("clip") / f"{SECRET}.mp4"
    subprocess.run([ffmpeg, "-hide_banner", "-loglevel", "error", "-f", "lavfi",
                    "-i", f"testsrc=size=320x240:rate={CLIP_FPS}:duration={CLIP_S}",
                    "-metadata", f"title={SECRET}", "-metadata", f"comment={SECRET}",
                    "-pix_fmt", "yuv420p", "-c:v", "libx264", str(clip)], check=True)
    return clip


@pytest.fixture
def root(tmp_path, synthetic_clip):
    """A fake repo root: reference/<SECRET>.mp4 and an empty reference/_frames/."""
    (tmp_path / "reference" / "_frames").mkdir(parents=True)
    shutil.copy(synthetic_clip, tmp_path / "reference" / synthetic_clip.name)
    return tmp_path


@pytest.fixture
def clip(root):
    return root / "reference" / f"{SECRET}.mp4"


def run(tool, *argv):
    """Run a tool's main() in-process; returns its exit code."""
    try:
        return tool.main([str(a) for a in argv])
    except SystemExit as e:  # argparse errors
        return e.code


def snapshot(root):
    """Every file and folder under root, to show that a refused run wrote nothing."""
    return sorted(Path(root).rglob("*"))
