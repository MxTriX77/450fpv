"""derive.py against the spec scenarios "Ground recipe derived and wired" and "Mean albedo hits the target"
(p1-s04-belt-straw): the four maps of a ground recipe from two synthetic sets, the albedo's mean luminance within
+-5 % of the target (printed), the bare and green shares within +-2 points of the recipe, the detail albedo neutral
at a linear mean of 0.5, the recipe recorded beside the maps, identical bytes on a rerun and regeneration on a recipe
or source change, and the failures named with nothing written. The last test measures the real belt_straw albedo."""
import json
import random
from pathlib import Path

import pytest
from PIL import Image

import derive
import fetch
from conftest import SET_ID, THIRD_PARTY, make_root

RECIPES = Path("assets-src/materials")
MATERIALS = Path("unity/Assets/_Project/Art/Materials")
MATERIAL = "fake_ground"
COVER_ID = "fake_cover"
SOIL_RGB, COVER_RGB = (70, 55, 40), (210, 180, 120)
RECIPE = {
    "schema": 1, "kind": "ground", "shader": "Universal Render Pipeline/Lit", "seed": 7, "size_m": 12, "output_px": 256,
    "soil": {"texture_set": f"polyhaven/{SET_ID}", "tile_size_m": 1.3},
    "cover": {"texture_set": f"polyhaven/{COVER_ID}", "tile_size_m": 2.0},
    "patches": {"bare": {"share": 0.08, "size_m": [0.5, 2.0]},
                "green": {"share": 0.15, "width_m": [1.0, 3.0], "length_m": [3.0, 12.0], "tint": "#6a7a3a"},
                "lodging": {"azimuth_deg": 25, "bias": 0.3}},
    "albedo_target": "#7a7055", "tint": [1.0, 1.0, 1.0, 1.0],
    "detail": {"texture_set": f"polyhaven/{COVER_ID}", "tile_size_m": 2.0},
}
TARGET_LUMINANCE = derive.luminance([derive.SRGB_TO_LINEAR[v] for v in (0x7a, 0x70, 0x55)])


def write_recipe(root, recipe):
    (root / RECIPES).mkdir(parents=True, exist_ok=True)
    (root / RECIPES / f"{MATERIAL}.json").write_text(json.dumps(recipe, indent=2) + "\n", encoding="utf-8")


def pin_set(root, manifest, set_id, rgb, roughness, ao, size=(32, 32)):
    """Writes flat diffuse/normal/roughness/AO maps for a polyhaven-style set and pins them in the fake manifest."""
    folder = root / THIRD_PARTY / "polyhaven" / set_id
    folder.mkdir(parents=True, exist_ok=True)
    images = {"diffuse": Image.new("RGB", size, rgb), "normal": Image.new("RGB", size, (128, 128, 255)),
              "roughness": Image.new("L", size, roughness), "ao": Image.new("L", size, ao)}
    files = []
    for map_name, image in images.items():
        name = f"{set_id}_{map_name}_1k.png"
        image.save(folder / name)
        files.append({"map": map_name, "name": name, "url": f"https://dl.polyhaven.org/{name}",
                      "bytes": (folder / name).stat().st_size, "sha256": fetch.sha256(folder / name)})
    for entry in manifest["entries"]:
        if entry["id"] == set_id:
            entry["files"] = files
            return
    manifest["entries"].append({"source": "polyhaven", "id": set_id, "title": set_id, "resolution": "1k", "licence": "CC0",
                                "author": "A. Tester", "page": f"https://polyhaven.com/a/{set_id}",
                                "destination": f"{THIRD_PARTY.as_posix()}/polyhaven/{set_id}", "files": files})


def ground_root(tmp_path):
    root = make_root(tmp_path)
    manifest_path = root / "tools" / "assets" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    for name in ("fake_set_diff_1k.png", "fake_set_nor_gl_1k.png"):  # conftest's byte blobs are not images
        (root / THIRD_PARTY / "polyhaven" / SET_ID / name).unlink()
    pin_set(root, manifest, SET_ID, SOIL_RGB, roughness=180, ao=200)
    pin_set(root, manifest, COVER_ID, COVER_RGB, roughness=220, ao=240)
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    write_recipe(root, RECIPE)
    return root


def run(*argv):
    try:
        return derive.main([str(a) for a in argv])
    except SystemExit as e:
        return e.code


def outputs(root):
    return {name: root / MATERIALS / f"{MATERIAL}_{name}.png" for name in derive.GROUND_MAPS}


def mean_luminance(png):
    return derive.luminance(derive.linear_means(Image.open(png).convert("RGB")))


def test_ground_maps_hit_the_target_and_the_shares(tmp_path, capsys):
    root = ground_root(tmp_path)
    assert run(MATERIAL, "--root", root) == 0
    out = capsys.readouterr().out
    assert f"{MATERIAL}_albedo.png: derived 256x256 ground from soil polyhaven/{SET_ID} and cover polyhaven/{COVER_ID}" in out
    assert "derive.py: OK" in out

    maps = outputs(root)
    albedo, normal, mask, detail = (Image.open(maps[n]) for n in derive.GROUND_MAPS)
    assert (albedo.mode, albedo.size) == ("RGB", (256, 256))
    assert (normal.mode, normal.size) == ("RGB", (256, 256))
    assert (mask.mode, mask.size) == ("RGBA", (256, 256))
    assert (detail.mode, detail.size) == ("RGB", (32, 32)), "the detail albedo keeps the set's own size"

    measured = mean_luminance(maps["albedo"])
    print(f"{MATERIAL}_albedo.png mean luminance {measured:.4f} vs target {TARGET_LUMINANCE:.4f} "
          f"({100 * (measured / TARGET_LUMINANCE - 1):+.2f} %)")
    assert abs(measured / TARGET_LUMINANCE - 1) <= 0.05

    stamp = json.loads((root / MATERIALS / f"{MATERIAL}.recipe.json").read_text(encoding="utf-8"))
    assert stamp["kind"] == "ground" and stamp["formula"] == derive.GROUND_FORMULA and stamp["recipe"] == RECIPE
    assert stamp["outputs"] == [f"{MATERIAL}_{n}.png" for n in derive.GROUND_MAPS] and stamp["size"] == [256, 256]
    assert set(stamp["sources"]) == {f"{role}/{m}" for role in ("soil", "cover", "detail")
                                     for m in ("diffuse", "normal", "roughness", "ao")}
    assert stamp["sources"]["soil/diffuse"]["sha256"] == fetch.sha256(root / THIRD_PARTY / "polyhaven" / SET_ID / f"{SET_ID}_diffuse_1k.png")
    assert len(stamp["inputs_sha256"]) == 64 and stamp["pillow"]
    shares = stamp["measured"]
    print(f"bare {100 * shares['bare_share']:.2f} % (recipe 8), green {100 * shares['green_share']:.2f} % (recipe 15)")
    assert abs(shares["bare_share"] - 0.08) <= 0.02 and abs(shares["green_share"] - 0.15) <= 0.02
    assert abs(shares["mean_luminance"] - measured) < 1e-3

    r, g, b, a = mask.getpixel((5, 5))
    assert (r, b) == (0, 0) and g in (200, 240) and a in (255 - 180, 255 - 220), "metallic 0, the sets' AO, 1 - roughness"
    for channel in derive.linear_means(detail):
        assert abs(channel - 0.5) <= 0.01, "the detail albedo is neutral for URP/Lit's detail x2"


def test_coverage_and_stamped_shares():
    half = Image.new("L", (64, 64), 0)
    half.paste(255, (0, 0, 32, 64))
    assert derive.coverage(half) == 0.5
    assert derive.coverage(half, outside=half) == 0.0
    layer = derive.stamped(random.Random(1), 256, 256 / 12, 0.08, [0.5, 2.0], [0.5, 2.0], lambda r: 0.0, 0.1, "bare")
    assert abs(derive.coverage(layer) - 0.08) <= 0.005
    strips = derive.stamped(random.Random(2), 256, 256 / 12, 0.15, [3.0, 12.0], [1.0, 3.0], lambda r: 25.0, 0.5, "green",
                            outside=layer)
    assert abs(derive.coverage(strips, outside=layer) - 0.15) <= 0.005


def test_ground_is_idempotent_and_regenerates_on_a_change(tmp_path, capsys):
    root = ground_root(tmp_path)
    maps = outputs(root)
    stamp = root / MATERIALS / f"{MATERIAL}.recipe.json"
    assert run(MATERIAL, "--root", root) == 0
    first = {p: p.read_bytes() for p in list(maps.values()) + [stamp]}
    times = {p: p.stat().st_mtime_ns for p in first}
    assert run(MATERIAL, "--root", root) == 0
    assert f"{MATERIAL}_albedo.png: up to date" in capsys.readouterr().out
    assert {p: p.read_bytes() for p in first} == first and {p: p.stat().st_mtime_ns for p in first} == times

    write_recipe(root, dict(RECIPE, seed=8))  # the patches move
    assert run(MATERIAL, "--root", root) == 0
    assert "derived 256x256 ground" in capsys.readouterr().out
    assert maps["albedo"].read_bytes() != first[maps["albedo"]]
    assert json.loads(stamp.read_text(encoding="utf-8"))["recipe"]["seed"] == 8

    write_recipe(root, dict(RECIPE, seed=8, albedo_target="#9a907a"))  # a brighter target
    assert run(MATERIAL, "--root", root) == 0
    brighter = mean_luminance(maps["albedo"])
    assert abs(brighter / derive.luminance([derive.SRGB_TO_LINEAR[v] for v in (0x9a, 0x90, 0x7a)]) - 1) <= 0.05
    assert brighter > TARGET_LUMINANCE

    Image.new("RGB", (32, 32), (200, 200, 200)).save(root / THIRD_PARTY / "polyhaven" / COVER_ID / f"{COVER_ID}_diffuse_1k.png")
    assert run(MATERIAL, "--root", root) == 0  # a source change regenerates
    assert "derived 256x256 ground" in capsys.readouterr().out

    maps["normal"].unlink()  # a missing output is rebuilt even with a current stamp
    assert run(MATERIAL, "--root", root) == 0
    assert "derived 256x256 ground" in capsys.readouterr().out and maps["normal"].is_file()


def test_all_handles_ground_and_mask_recipes_together(tmp_path, capsys):
    root = ground_root(tmp_path)
    brick = {"schema": 1, "shader": "Universal Render Pipeline/Lit", "texture_set": f"polyhaven/{SET_ID}",
             "tile_size_m": [1.0, 1.0], "tint": [1.0, 1.0, 1.0, 1.0],
             "mask": {"metallic": 0.0, "occlusion": "ao", "smoothness": "1 - roughness"}}
    (root / RECIPES / "brick.json").write_text(json.dumps(brick), encoding="utf-8")
    assert run("--all", "--root", root) == 0
    out = capsys.readouterr().out
    assert "brick_mask.png: derived 32x32" in out and f"{MATERIAL}_albedo.png: derived 256x256 ground" in out


def test_ground_failures_name_the_cause_and_write_nothing(tmp_path, capsys):
    root = ground_root(tmp_path)
    without_cover = {k: v for k, v in RECIPE.items() if k != "cover"}
    too_big = dict(RECIPE, output_px=4096)
    too_much = dict(RECIPE, patches=dict(RECIPE["patches"], bare={"share": 0.9, "size_m": [0.5, 2.0]}))
    bad_colour = dict(RECIPE, albedo_target="7a7055")
    cases = [
        (without_cover, f"{MATERIAL}: missing object \"cover\""),
        (too_big, f"{MATERIAL}.output_px: 4096 must be an integer in [16, 2048]"),
        (too_much, f"{MATERIAL}.patches: bare.share + green.share = {0.9 + 0.15} exceeds 1"),
        (bad_colour, f"{MATERIAL}.albedo_target: '7a7055' is not an sRGB colour like #7a7055"),
        (dict(RECIPE, kind="lawn"), f"{MATERIAL}: unknown recipe kind 'lawn' (omit it for a tiled set, or 'ground' or 'palette')"),
        (dict(RECIPE, soil={"texture_set": "polyhaven/nope", "tile_size_m": 1.0}),
         f"{MATERIAL}: texture set 'polyhaven/nope' is not in {fetch.MANIFEST}"),
    ]
    for recipe, message in cases:
        write_recipe(root, recipe)
        assert run(MATERIAL, "--root", root) == 1
        assert f"ERROR: {message}" in capsys.readouterr().out
    assert not (root / MATERIALS).exists()


def test_belt_straw_albedo_mean_hits_the_sheet_target():
    """The real derived map of the belt_straw recipe, when it is on disk (an LFS checkout): prints the measured value."""
    repo = Path(fetch.REPO_ROOT)
    albedo = repo / MATERIALS / "belt_straw_albedo.png"
    if not albedo.is_file() or albedo.stat().st_size < 1024:
        pytest.skip("belt_straw_albedo.png is not derived here (run derive.py belt_straw, or git lfs checkout)")
    recipe = json.loads((repo / RECIPES / "belt_straw.json").read_text(encoding="utf-8"))
    target = derive.luminance(derive._colour(recipe, "albedo_target", "belt_straw"))
    measured = mean_luminance(albedo)
    stamp = json.loads((repo / MATERIALS / "belt_straw.recipe.json").read_text(encoding="utf-8"))["measured"]
    print(f"belt_straw_albedo.png mean luminance {measured:.4f} vs target {target:.4f} "
          f"({100 * (measured / target - 1):+.2f} %); bare {100 * stamp['bare_share']:.1f} %, green {100 * stamp['green_share']:.1f} %")
    assert abs(measured / target - 1) <= 0.05
    assert abs(stamp["bare_share"] - 0.08) <= 0.02 and abs(stamp["green_share"] - 0.15) <= 0.02
    assert Image.open(albedo).size == (2048, 2048), "2K at most (AST-08)"
