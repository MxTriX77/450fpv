"""derive.py's ground recipes in wall space (p1-e01-trench, design 7: trench_wall's strip, wall mask and spade-facet
normal): the four maps on lib/strip.py's strip from two synthetic sets, the floor row on its target, each wall row's
topsoil band darker at the lip and its clod spill paler at the foot, spade facets in the wall rows' normals and none
on the floor, the recipe recorded and rerun as a no-op, a changed constant re-deriving, and the failures named. The
real trench_wall maps are measured when they are on disk."""
import json
from pathlib import Path

import pytest
from PIL import Image, ImageStat

import derive
import fetch
from conftest import SET_ID, THIRD_PARTY, make_root
from lib import strip
from test_ground import MATERIALS, RECIPES, pin_set, run

MATERIAL = "fake_wall"
LOAM_ID = "fake_loam"
PARAMS_PATH = "assets-src/earthworks/fake_trench.json"
RECIPE = {
    "schema": 1, "kind": "ground", "space": "wall", "shader": "Universal Render Pipeline/Lit", "seed": 3,
    "size_m": [strip.U_M, strip.V_M], "output_px": [512, 256], "params": PARAMS_PATH,
    "loam": {"texture_set": f"polyhaven/{LOAM_ID}", "tile_size_m": 2.0},
    "soil": {"texture_set": f"polyhaven/{SET_ID}", "tile_size_m": 1.3, "share": 0.5},
    "albedo_target": "#4a463f", "topsoil": "#33302b", "roots": "#5e4f3e", "spill": "#8a7660", "floor_albedo": "#4a463f",
    "facets": {"size_m": [0.2, 0.3], "relief_m": 0.02}, "tint": [1.0, 1.0, 1.0, 1.0],
    "detail": {"texture_set": f"polyhaven/{LOAM_ID}", "tile_size_m": 2.0},
}


def wall_root(tmp_path, recipe=RECIPE, topsoil_m=0.5):
    root = make_root(tmp_path)
    manifest_path = root / "tools" / "assets" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    for name in ("fake_set_diff_1k.png", "fake_set_nor_gl_1k.png"):
        (root / THIRD_PARTY / "polyhaven" / SET_ID / name).unlink()
    pin_set(root, manifest, SET_ID, (70, 55, 40), roughness=180, ao=200)
    pin_set(root, manifest, LOAM_ID, (120, 100, 80), roughness=200, ao=220)
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    (root / PARAMS_PATH).parent.mkdir(parents=True, exist_ok=True)
    (root / PARAMS_PATH).write_text(json.dumps({"topsoil_m": topsoil_m}), encoding="utf-8")
    (root / RECIPES).mkdir(parents=True, exist_ok=True)
    (root / RECIPES / f"{MATERIAL}.json").write_text(json.dumps(recipe), encoding="utf-8")
    return root


def band(image, top_m, bottom_m):
    """The strip's rows between two heights (metres up the map), as an image."""
    h = image.size[1]
    return image.crop((0, round((1 - top_m / strip.V_M) * h), image.size[0], round((1 - bottom_m / strip.V_M) * h)))


def luminance(image):
    return derive.luminance(derive.linear_means(image.convert("RGB")))


def test_wall_strip_bands_and_facets(tmp_path, capsys):
    root = wall_root(tmp_path)
    assert run(MATERIAL, "--root", root) == 0, capsys.readouterr().out
    out = capsys.readouterr().out
    assert "derived 512x256 wall strip" in out
    maps = {name: Image.open(root / MATERIALS / f"{MATERIAL}_{name}.png") for name in derive.GROUND_MAPS}
    assert maps["albedo"].size == maps["normal"].size == maps["mask"].size == (512, 256)
    target = derive.luminance(derive._colour(RECIPE, "floor_albedo", MATERIAL))
    floor = band(maps["albedo"], strip.FLOOR_ROW[1], strip.FLOOR_ROW[0])
    assert abs(luminance(floor) / target - 1) < 0.05, "the floor row lands on floor_albedo"
    for bottom, top in (strip.BELT_ROW, strip.FIELD_ROW):
        topsoil = luminance(band(maps["albedo"], top - 0.25, top - 0.4))
        middle = luminance(band(maps["albedo"], top - 0.75, top - 1.0))
        spill = luminance(band(maps["albedo"], bottom + 0.15, bottom + 0.1))
        print(f"row {bottom}-{top} m: topsoil {topsoil:.4f}, middle {middle:.4f}, spill {spill:.4f}")
        assert topsoil < 0.9 * middle, "the topsoil band is darker than the wall below it"
        assert spill > 1.2 * middle, "the clod spill is paler at the wall's foot"
    normal = maps["normal"].convert("RGB")
    wall_spread = ImageStat.Stat(band(normal, strip.BELT_ROW[1], strip.BELT_ROW[0]).split()[0]).stddev[0]
    floor_spread = ImageStat.Stat(band(normal, strip.FLOOR_ROW[1], strip.FLOOR_ROW[0]).split()[0]).stddev[0]
    assert wall_spread > 2 and floor_spread < 0.5, "spade facets tilt the walls' normals, never the floor's"
    tilt = max(abs(v - 128) for v in band(normal, strip.BELT_ROW[1], strip.BELT_ROW[0]).split()[0].getextrema())
    assert tilt <= 127 * 2 * 0.02 / 0.3 + 1, "a facet stands at most relief_m proud: its tangent stays small"
    stamp = json.loads((root / MATERIALS / f"{MATERIAL}.recipe.json").read_text(encoding="utf-8"))
    assert stamp["space"] == "wall" and stamp["layout"]["topsoil_m"] == 0.5 and stamp["layout"]["strip"]["U_M"] == strip.U_M


def test_wall_is_idempotent_and_re_derives_on_a_change(tmp_path, capsys, monkeypatch):
    root = wall_root(tmp_path)
    assert run(MATERIAL, "--root", root) == 0
    before = (root / MATERIALS / f"{MATERIAL}_albedo.png").read_bytes()
    capsys.readouterr()
    assert run(MATERIAL, "--root", root) == 0
    assert "up to date" in capsys.readouterr().out
    monkeypatch.setattr(derive, "TOPSOIL_LEVEL", 0.5)
    assert run(MATERIAL, "--root", root) == 0
    assert "derived" in capsys.readouterr().out
    assert (root / MATERIALS / f"{MATERIAL}_albedo.png").read_bytes() != before
    (root / PARAMS_PATH).write_text(json.dumps({"topsoil_m": 0.7}), encoding="utf-8")
    assert run(MATERIAL, "--root", root) == 0
    assert "derived" in capsys.readouterr().out, "the asset's topsoil_m re-derives the band"


@pytest.mark.parametrize("edit, message", [
    (dict(size_m=[24.0, 4.0]), "fake_wall.size_m: [24.0, 4.0] must be lib/strip.py's"),
    (dict(output_px=[4096, 256]), "fake_wall.output_px: [4096, 256] must be [w, h], integers in [16, 2048]"),
    (dict(params="assets-src/nope.json"), "fake_wall.params: 'assets-src/nope.json' is not an asset params file"),
    (dict(space="floor"), "fake_wall: unknown ground space 'floor'"),
])
def test_wall_failures_name_the_cause(tmp_path, capsys, edit, message):
    root = wall_root(tmp_path, dict(RECIPE, **edit))
    assert run(MATERIAL, "--root", root) == 1
    assert f"ERROR: {message}" in capsys.readouterr().out
    assert not (root / MATERIALS / f"{MATERIAL}_albedo.png").exists()


def test_trench_wall_strip_as_derived():
    """The real trench_wall maps, when on disk: 2K wide at most, the strip's aspect, the walls near the sheet's shaded
    inside #4a463f (the bands move it a little) and the floor on it (prints the measured values)."""
    repo = Path(fetch.REPO_ROOT)
    albedo = repo / MATERIALS / "trench_wall_albedo.png"
    if not albedo.is_file() or albedo.stat().st_size < 1024:
        pytest.skip("trench_wall_albedo.png is not derived here (run derive.py trench_wall, or git lfs checkout)")
    image = Image.open(albedo)
    assert image.size == (2048, 1024)
    target = derive.luminance(derive._colour({"c": "#4a463f"}, "c", "trench_wall"))
    measured = json.loads((repo / MATERIALS / "trench_wall.recipe.json").read_text(encoding="utf-8"))["measured"]
    print(f"trench_wall: walls {measured['wall_mean_srgb']} ({measured['wall_luminance']:.4f}), floor "
          f"{measured['floor_mean_srgb']}, target #4a463f ({target:.4f})")
    assert abs(measured["wall_luminance"] / target - 1) < 0.15
    assert measured["floor_mean_srgb"] == "#4a463f"
