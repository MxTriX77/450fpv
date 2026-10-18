"""build.py end to end in headless Blender, when the pinned Blender is installed: K01 builds into a scratch root
with its audit printed, the outputs agree with the spec scenarios, a second run writes byte-identical files, an
out-of-range param and a mesh grown beyond its box are refused before anything is written. The version pin is
checked without Blender."""
import hashlib
import json
import os
import shutil
import subprocess
import textwrap

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


def run_build(blender, root, python_expr=None):
    """Runs build.py on K01 in `root`, or `python_expr` in its place; (exit code, output)."""
    driver = ["--python-expr", python_expr] if python_expr else \
        ["--python", os.path.join(build.HERE, "build.py"), "--", "K01", "--root", str(root)]
    run = subprocess.run([blender, "--background", "--factory-startup", *driver], capture_output=True, text=True,
                         timeout=300)
    return run.returncode, run.stdout + run.stderr


def test_k01_builds_headless_and_reproducibly(blender, repo_root, material_ids, tmp_path):
    shutil.copytree(repo_root / "assets-src", tmp_path / "assets-src")
    digests = []
    for _ in range(2):
        code, output = run_build(blender, tmp_path)
        assert code == 0 and "build.py: OK" in output, output
        files = {name: (tmp_path / OUT / name).read_bytes() for name in ("K01_whole.fbx", "K01_whole.assetmeta.json")}
        digests.append({name: hashlib.sha256(data).hexdigest() for name, data in files.items()})
    assert digests[0] == digests[1], "a rebuild of unchanged params must write identical bytes"
    assert "K01_whole: audit collision[0] box: box->mesh 3.288 mm at (-0.125, 0, 0.06), tolerance 50 mm" in output
    assert "K01_whole: audit mesh: mesh->collision 1.8" in output and "nearest collision[0], tolerance 50 mm" in output
    assert sorted(p.name for p in (tmp_path / OUT).iterdir()) == ["K01_whole.assetmeta.json", "K01_whole.fbx"]
    data = json.loads(files["K01_whole.assetmeta.json"].decode("utf-8"))
    assert meta.validate(data, material_ids) == []
    assert meta.check_against_fbx(data, str(tmp_path / OUT / "K01_whole.fbx")) == []
    size = [data["bounds"]["max"][a] - data["bounds"]["min"][a] for a in range(3)]
    assert size == pytest.approx([0.250, 0.065, 0.120], abs=0.001)


def test_out_of_range_bevel_is_refused_and_the_outputs_stay_untouched(blender, repo_root, k01_out, tmp_path):
    shutil.copytree(repo_root / "assets-src", tmp_path / "assets-src")
    shutil.copytree(k01_out, tmp_path / OUT)
    before = {p.name: p.read_bytes() for p in (tmp_path / OUT).iterdir()}
    params_path = tmp_path / "assets-src" / "debris" / "K01_whole.json"
    params = json.loads(params_path.read_text(encoding="utf-8"))
    params["bevel_mm"] = {"min": 200, "max": 200}
    params_path.write_text(json.dumps(params), encoding="utf-8")
    code, output = run_build(blender, tmp_path)
    assert code == 1, output
    assert ("ERROR: K01_whole: params assets-src\\debris\\K01_whole.json: 'bevel_mm.min' = 200 is outside [0.0, 10.0]; "
            "'bevel_mm.max' = 200 is outside [0.0, 10.0]") in output
    assert "FBX export" not in output and "audit" not in output
    assert {p.name: p.read_bytes() for p in (tmp_path / OUT).iterdir()} == before


def test_mesh_grown_beyond_its_box_fails_the_audit_and_writes_nothing(blender, repo_root, tmp_path):
    shutil.copytree(repo_root / "assets-src", tmp_path / "assets-src")
    grown = textwrap.dedent(f"""
        import sys
        sys.path.insert(0, {build.HERE!r})
        import build
        from generators import debris_brick
        original = debris_brick.build
        def grown(params):
            built = original(params)
            lod0 = built["lods"][0]
            lod0.vertices = [(x + 0.06 if x > 0.1 else x, y, z) for x, y, z in lod0.vertices]
            return built
        debris_brick.build = grown
        sys.exit(build.main(["K01", "--root", {str(tmp_path)!r}]))
        """)
    code, output = run_build(blender, tmp_path, grown)
    assert code == 1, output
    assert "K01_whole: audit collision[0] box: box->mesh 32.500 mm at (0.125, 0.0325, 0), tolerance 50 mm" in output
    assert "ERROR: K01_whole: mesh: mesh->collision 60.000 mm at (0.185, " in output
    assert ") nearest collision[0] exceeds the tolerance 50 mm" in output
    assert "FBX export" not in output
    assert not (tmp_path / "unity").exists()
