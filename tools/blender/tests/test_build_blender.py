"""build.py end to end in headless Blender, when the pinned Blender is installed: K01 builds into a scratch root,
the outputs agree with the spec scenarios, and a second run writes byte-identical files. The version pin is
checked without Blender."""
import hashlib
import json
import shutil
import subprocess

import pytest

import build
from lib import meta

OUT = "unity/Assets/_Project/Art/Models/debris/K01"


def test_version_pin():
    build.check_version((5, 2, 2))
    with pytest.raises(RuntimeError, match=r"4\.2\.0 is not the pinned 5\.2\.x"):
        build.check_version((4, 2, 0))
    with pytest.raises(RuntimeError):
        build.check_version((5, 3, 0))


def test_k01_builds_headless_and_reproducibly(blender, repo_root, material_ids, tmp_path):
    shutil.copytree(repo_root / "assets-src", tmp_path / "assets-src")
    command = [blender, "--background", "--factory-startup", "--python", str(repo_root / "tools" / "blender" / "build.py"),
               "--", "K01", "--root", str(tmp_path)]
    digests = []
    for _ in range(2):
        run = subprocess.run(command, capture_output=True, text=True, timeout=300)
        assert run.returncode == 0 and "build.py: OK" in run.stdout, run.stdout + run.stderr
        files = {name: (tmp_path / OUT / name).read_bytes() for name in ("K01_whole.fbx", "K01_whole.assetmeta.json")}
        digests.append({name: hashlib.sha256(data).hexdigest() for name, data in files.items()})
    assert digests[0] == digests[1], "a rebuild of unchanged params must write identical bytes"
    data = json.loads(files["K01_whole.assetmeta.json"].decode("utf-8"))
    assert meta.validate(data, material_ids) == []
    assert meta.check_against_fbx(data, str(tmp_path / OUT / "K01_whole.fbx")) == []
    size = [data["bounds"]["max"][a] - data["bounds"]["min"][a] for a in range(3)]
    assert size == pytest.approx([0.250, 0.065, 0.120], abs=0.001)
