"""build.py end to end in headless Blender, when the pinned Blender is installed: K01 builds into a scratch root
with its audit printed, the outputs agree with the spec scenarios, a second run writes byte-identical files, an
out-of-range param and a mesh grown beyond its box are refused before anything is written, and an impostor asked for
is rendered once as a 1k albedo and a 1k normal atlas. The version pin is checked without Blender."""
import hashlib
import json
import os
import shutil
import subprocess
import textwrap

import pytest
from PIL import Image

import build
from lib import impostor, meta

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


def test_impostor_atlases_are_rendered_at_1k_once_and_removed_when_off(blender, repo_root, tmp_path):
    """p1-v01-locust design decision 3 (asset-pipeline "Baked impostor LOD"): K01 with its params asking for an
    impostor (the brick generator taught the two keys inside this Blender) gets a 1k RGBA albedo atlas beside its FBX
    with every view inside its cell and a 1k RGB normal atlas whose every texel faces its view, and the impostor as
    its last LOD at the params' switch distance; a rebuild finds the atlases up to date and writes identical bytes; a
    deleted atlas renders both again to the same bytes; params that stop asking remove them."""
    shutil.copytree(repo_root / "assets-src", tmp_path / "assets-src")
    for path in ("tools/assets/manifest.json", "unity/Assets/_Project/Art/ThirdParty/polyhaven/red_brick/red_brick_diff_1k.png",
                 "unity/Assets/_Project/Art/Materials/brick_red_mask.png"):
        (tmp_path / path).parent.mkdir(parents=True, exist_ok=True)
        shutil.copy(repo_root / path, tmp_path / path)
    params_path = tmp_path / "assets-src" / "debris" / "K01_whole.json"
    params = json.loads(params_path.read_text(encoding="utf-8"))
    params["impostor"], params["lod"]["impostor_switch_m"] = True, 60.0
    params_path.write_text(json.dumps(params), encoding="utf-8")
    driver = textwrap.dedent(f"""
        import sys
        sys.path.insert(0, {build.HERE!r})
        import build
        from generators import debris_brick
        debris_brick.PARAMS["impostor"] = {{"type": "bool", "range": "true or false", "default": False, "effect": "test"}}
        debris_brick.PARAMS["lod.impostor_switch_m"] = {{"type": "float", "range": (1.0, 2000.0), "default": 60.0,
                                                        "effect": "test"}}
        sys.exit(build.main(["K01", "--root", {str(tmp_path)!r}]))
        """)
    names = ("K01_whole.fbx", "K01_whole.assetmeta.json", "K01_whole_impostor.png", "K01_whole_impostor_normal.png")
    atlas = tmp_path / OUT / names[2]
    outputs, digests = [], []
    for step in ("first", "again", "deleted"):
        if step == "deleted":
            atlas.unlink()
        code, output = run_build(blender, tmp_path, driver)
        assert code == 0 and "build.py: OK" in output, output
        outputs.append(output)
        digests.append({name: hashlib.sha256((tmp_path / OUT / name).read_bytes()).hexdigest() for name in names})
    assert "K01_whole: impostor rendered in" in outputs[0]
    assert "impostor rendered" not in outputs[1], "an up-to-date atlas is not rendered again"
    assert "K01_whole: impostor rendered in" in outputs[2]
    assert digests[0] == digests[1] == digests[2], "the same inputs render the same bytes"
    assert ("K01_whole: impostor K01_whole_impostor.png and K01_whole_impostor_normal.png 1024x1024, 8 views at 15 deg"
            in outputs[0])

    assert impostor.png_header(str(atlas)) == (1024, 1024, 8, 6), "a 1k 8-bit RGBA atlas"
    normals = tmp_path / OUT / names[3]
    assert impostor.png_header(str(normals)) == (1024, 1024, 8, 2), "a 1k 8-bit RGB normal atlas"
    blue = Image.open(normals).getchannel("B")
    assert blue.getextrema()[0] >= 127, "every texel's normal faces its view's camera, the filled ones too"
    data = json.loads((tmp_path / OUT / names[1]).read_text(encoding="utf-8"))
    last = data["lods"][-1]
    assert [lod["mesh"] for lod in data["lods"]] == ["K01_whole_LOD0", "K01_whole_LOD1", "K01_whole_impostor"]
    assert last["switch_distance_m"] == 60.0 and data["cull_distance_m"] == 120.0
    assert last["impostor"]["atlas_sha256"] == digests[0][names[2]]
    assert last["impostor"]["normals_sha256"] == digests[0][names[3]]
    alpha = Image.open(atlas).getchannel("A")
    pad = impostor.PAD_PX
    for k in range(impostor.VIEWS):
        x0, y0 = (k % 4) * 256, (k // 4) * 512
        cell = alpha.crop((x0, y0, x0 + 256, y0 + 512))
        assert cell.getbbox() is not None, f"view {k} is empty"
        left, top, right, bottom = cell.getbbox()
        assert left >= pad - 1 and top >= pad - 1 and right <= 256 - pad + 1 and bottom <= 512 - pad + 1, \
            f"view {k} reaches {cell.getbbox()} into its {pad} px pad"

    params["impostor"] = False
    params_path.write_text(json.dumps(params), encoding="utf-8")
    code, output = run_build(blender, tmp_path, driver)
    assert code == 0, output
    assert not atlas.exists() and not normals.exists(), "atlases the params no longer ask for are removed"
    data = json.loads((tmp_path / OUT / names[1]).read_text(encoding="utf-8"))
    assert [lod["mesh"] for lod in data["lods"]] == ["K01_whole_LOD0", "K01_whole_LOD1"]


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
