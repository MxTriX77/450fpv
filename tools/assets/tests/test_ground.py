"""derive.py against the spec scenarios "Ground recipe derived and wired", "Mean albedo hits the target" and "A
formula constant changes" (p1-s04-belt-straw): the four maps of a ground recipe from two synthetic sets, the
albedo's mean luminance within +-5 % of the target (printed), the bare share on the asset's outlines and the green
share within +-2 points, the detail albedo grey at a linear mean of 0.5, the soil on its optional target, a bare
patch's feather broken by the cover, the soil in the gaps between straws at the cover's gap_share, the recipe
recorded beside the maps, identical bytes on a rerun and regeneration on a change of the recipe, a source, a
formula constant, the patch layout's constants or the asset's layout params, and the failures named with nothing
written. The bare mask and the step outlines agree (review-physics PR7 c). The green strips are the asset's
(lib/mat.py) at their share, the relief map is lib/mat.py's relief and darkens the hollows, a floor's lip shows soil
just outside its outline, and the gaps take the cover's gap_albedo. The last test measures the real belt_straw
albedo."""
import json
import math
import random
from pathlib import Path

import pytest
from PIL import Image, ImageChops

import derive
import fetch
from conftest import SET_ID, THIRD_PARTY, make_root
from lib import mat, patches  # tools/blender, which derive.py puts on the path

RECIPES = Path("assets-src/materials")
MATERIALS = Path("unity/Assets/_Project/Art/Materials")
MATERIAL = "fake_ground"
COVER_ID = "fake_cover"
SOIL_RGB, COVER_RGB = (70, 55, 40), (210, 180, 120)
LAYOUT_PARAMS = "assets-src/surfaces/fake_patch.json"  # the asset params the bare patches come from
PARAMS = {"seed": 7, "size_m": 12, "cell_m": 0.25, "relief_m": 0.04,
          "bare": {"share": 0.08, "size_m": {"min": 0.5, "max": 2.0}, "outline_vertices": 8,
                   "aspect": {"min": 1.5, "max": 2.5}, "path_patches": 3},
          "green": {"share": 0.15, "width_m": {"min": 1.0, "max": 3.0}, "length_m": {"min": 3.0, "max": 12.0}},
          "lodging": {"azimuth_deg": 25, "bias": 0.3}}
RECIPE = {
    "schema": 1, "kind": "ground", "shader": "Universal Render Pipeline/Lit", "seed": 7, "size_m": 12, "output_px": 256,
    "soil": {"texture_set": f"polyhaven/{SET_ID}", "tile_size_m": 1.3},
    "cover": {"texture_set": f"polyhaven/{COVER_ID}", "tile_size_m": 2.0},
    "patches": {"bare": {"params": LAYOUT_PARAMS},
                "green": {"params": LAYOUT_PARAMS, "tint": "#6a7a3a"},
                "lodging": {"azimuth_deg": 25, "bias": 0.3}},
    "albedo_target": "#7a7055", "tint": [1.0, 1.0, 1.0, 1.0],
    "detail": {"texture_set": f"polyhaven/{COVER_ID}", "tile_size_m": 2.0},
}
TARGET_LUMINANCE = derive.luminance([derive.SRGB_TO_LINEAR[v] for v in (0x7a, 0x70, 0x55)])


def write_recipe(root, recipe):
    (root / RECIPES).mkdir(parents=True, exist_ok=True)
    (root / RECIPES / f"{MATERIAL}.json").write_text(json.dumps(recipe, indent=2) + "\n", encoding="utf-8")


def write_params(root, params, path=LAYOUT_PARAMS):
    (root / path).parent.mkdir(parents=True, exist_ok=True)
    (root / path).write_text(json.dumps(params, indent=2) + "\n", encoding="utf-8")


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
    write_params(root, PARAMS)
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
    assert stamp["layout"] == {"params": LAYOUT_PARAMS, "inputs": patches.inputs(PARAMS), "constants": patches.constants(),
                               "green": mat.green_inputs(PARAMS), "relief": mat.relief_inputs(PARAMS),
                               "fields": mat.constants()}
    shares = stamp["measured"]
    print(f"bare {100 * shares['bare_share']:.2f} % on {shares['bare_patches']} outlines (params 8), soil shown "
          f"{100 * shares['bare_soil_share']:.2f} %, green {100 * shares['green_share']:.2f} % (recipe 15)")
    assert abs(shares["bare_share"] - 0.08) <= patches.SHARE_TOLERANCE and abs(shares["green_share"] - 0.15) <= 0.02
    assert 0 < shares["bare_soil_share"] < shares["bare_share"], "the feather inside the outlines shows straws"
    assert abs(shares["mean_luminance"] - measured) < 1e-3

    r, g, b, a = mask.getpixel((5, 5))
    assert (r, b) == (0, 0) and a in (255 - 180, 255 - 220), "metallic 0, 1 - roughness"
    assert any(base * (1 - derive.RELIEF_AO) - 1 <= g <= base for base in (200, 240)), "the sets' AO, less in the hollows"
    for channel in derive.linear_means(detail):
        assert abs(channel - 0.5) <= 0.01, "the detail albedo is neutral for URP/Lit's detail x2"
    r, g, b = detail.split()
    assert ImageChops.difference(r, g).getbbox() is None and ImageChops.difference(g, b).getbbox() is None,         "the detail is grey: it adds grain, never a hue"
    assert stamp["measured"]["soil_gain"] == [1.0, 1.0, 1.0], "no soil target: the soil keeps its pinned colour"


def test_soil_lands_on_its_target(tmp_path):
    root = ground_root(tmp_path)
    write_recipe(root, dict(RECIPE, soil=dict(RECIPE["soil"], albedo_target="#433f37")))
    assert run(MATERIAL, "--root", root) == 0
    measured = json.loads((root / MATERIALS / f"{MATERIAL}.recipe.json").read_text(encoding="utf-8"))["measured"]
    got, want = (int(measured["soil_mean_srgb"][i:i + 2], 16) for i in (1, 3, 5)), (0x43, 0x3f, 0x37)
    assert all(abs(a - b) <= 1 for a, b in zip(got, want)), measured["soil_mean_srgb"]
    assert abs(measured["luminance_error_pct"]) <= 5


def test_gaps_show_soil_between_the_straws_and_brighten_them(tmp_path):
    """A cover with straws (bright stripes over a darker ground): gap_share 0.25 lets the soil through about a quarter
    of the straw layer, the mean stays on the target, and the straws take a higher gain to hold it; with a darker
    gap_albedo the gaps take it and the straws brighten further."""
    root = ground_root(tmp_path)
    cover = Image.new("RGB", (64, 64), (150, 125, 80))
    for x in range(0, 64, 3):
        cover.paste((230, 200, 140), (x, 0, x + 1, 64))
    cover.save(root / THIRD_PARTY / "polyhaven" / COVER_ID / f"{COVER_ID}_diffuse_1k.png")

    def measured(gap_share):
        write_recipe(root, dict(RECIPE, cover=dict(RECIPE["cover"], gap_share=gap_share)))
        assert run(MATERIAL, "--root", root) == 0
        return json.loads((root / MATERIALS / f"{MATERIAL}.recipe.json").read_text(encoding="utf-8"))["measured"]

    without, with_gaps = measured(0.0), measured(0.25)
    print(f"gaps {100 * with_gaps['gap_share']:.1f} % (recipe 25), straw gain {without['straw_gain']} -> {with_gaps['straw_gain']}")
    assert without["gap_share"] == 0 and 0.15 <= with_gaps["gap_share"] <= 0.35
    assert abs(with_gaps["luminance_error_pct"]) <= 5
    assert all(b > a for a, b in zip(without["straw_gain"], with_gaps["straw_gain"])), "brighter straws, same mean"
    write_recipe(root, dict(RECIPE, cover=dict(RECIPE["cover"], gap_share=0.25, gap_albedo="#201c16")))
    assert run(MATERIAL, "--root", root) == 0
    litter = json.loads((root / MATERIALS / f"{MATERIAL}.recipe.json").read_text(encoding="utf-8"))["measured"]
    assert abs(litter["luminance_error_pct"]) <= 5
    assert all(b > a for a, b in zip(with_gaps["straw_gain"], litter["straw_gain"])), "darker gaps, brighter straws"


def test_strewn_keeps_the_core_and_breaks_the_feather():
    cover = Image.new("RGB", (64, 64), (40, 40, 40))
    for x in range(0, 64, 4):  # bright straws every 4 px
        cover.paste((220, 200, 150), (x, 0, x + 1, 64))
    full, none, half = (Image.new("L", (64, 64), v) for v in (255, 0, 128))
    assert derive.strewn(full, cover, random.Random(1), 100).getextrema() == (255, 255), "the core stays soil"
    assert derive.strewn(none, cover, random.Random(1), 100).getextrema() == (0, 0), "no soil outside the patch"
    edge = derive.strewn(half, cover, random.Random(1), 100)  # 100 px per m: the high-pass spans the stripes
    on_straw = sum(edge.getpixel((x, y)) for x in range(0, 64, 4) for y in range(64)) / (16 * 64)
    between = sum(edge.getpixel((x + 2, y)) for x in range(0, 64, 4) for y in range(64)) / (16 * 64)
    assert on_straw < between - 40, f"straws lie over the feather: {on_straw:.0f} on a straw, {between:.0f} between"


def test_coverage_and_the_strips_share():
    """The green strips the map fills (lib/mat.py's, blurred over their feather) cover their share outside the bare
    outlines within 2 points, as the generator's blades count them."""
    half = Image.new("L", (64, 64), 0)
    half.paste(255, (0, 0, 32, 64))
    assert derive.coverage(half) == 0.5
    assert derive.coverage(half, outside=half) == 0.0
    bare = patches.layout(**patches.inputs(PARAMS))["patches"]
    strips = mat.green(**mat.green_inputs(PARAMS), bare_patches=bare)
    assert abs(strips["share"] - 0.15) <= mat.SHARE_TOLERANCE + 1e-9
    layer = derive.green_layer(strips["strips"], 512, 12)
    floors = derive.outline_layer(bare, 512, 12)
    print(f"strips {100 * strips['share']:.2f} % on the samples, {100 * derive.coverage(layer, outside=floors):.2f} % in the map")
    assert abs(derive.coverage(layer, outside=floors) - 0.15) <= 0.02


def test_the_relief_map_is_the_meshs_relief_and_darkens_the_hollows(tmp_path):
    """One definition: the relief layer derive.py tones the map by reads lib/mat.py's relief (the generator's slab
    top) at every pixel within a level; the albedo of a flat cover is darker in the relief's hollows than on its
    crowns, and so is the occlusion."""
    relief = mat.relief(**mat.relief_inputs(PARAMS))
    layer = derive.relief_layer(relief, PARAMS["relief_m"], 256, 12)
    for px, py in ((10, 20), (128, 128), (200, 77), (255, 0), (31, 240)):
        x, z = ((px + 0.5) / 256 - 0.5) * 12, (0.5 - (py + 0.5) / 256) * 12
        assert abs(layer.getpixel((px, py)) - (128 + 127 * relief(x, z) / PARAMS["relief_m"])) <= 3
    root = ground_root(tmp_path)
    assert run(MATERIAL, "--root", root) == 0
    albedo = Image.open(outputs(root)["albedo"]).convert("L")
    occlusion = Image.open(outputs(root)["mask"]).getchannel("G")
    floors = derive.outline_layer(patches.layout(**patches.inputs(PARAMS))["patches"], 256, 12)
    big = derive.relief_layer(relief, PARAMS["relief_m"], 256, 12)
    crowns, hollows, ao_crowns, ao_hollows = [], [], [], []
    for py in range(0, 256, 2):
        for px in range(0, 256, 2):
            if floors.getpixel((px, py)):
                continue
            h = big.getpixel((px, py))
            if h > 200:
                crowns.append(albedo.getpixel((px, py)))
                ao_crowns.append(occlusion.getpixel((px, py)))
            elif h < 80:
                hollows.append(albedo.getpixel((px, py)))
                ao_hollows.append(occlusion.getpixel((px, py)))
    mean = lambda v: sum(v) / len(v)  # noqa: E731
    print(f"albedo L crowns {mean(crowns):.1f}, hollows {mean(hollows):.1f}; AO {mean(ao_crowns):.1f} / {mean(ao_hollows):.1f}")
    assert mean(hollows) < mean(crowns) - 10 and mean(ao_hollows) < mean(ao_crowns) - 10


def test_a_floors_lip_shows_soil_just_outside_its_outline(tmp_path):
    """The mat thins at a step's cut edge: soil shows between the cover's straws within LIP_M outside an outline and
    over a path's bridges (the hull of two consecutive patches' cores, task 1.8), and none farther out (the gaps
    aside, which are none here)."""
    root = ground_root(tmp_path)
    assert run(MATERIAL, "--root", root) == 0
    measured = json.loads((root / MATERIALS / f"{MATERIAL}.recipe.json").read_text(encoding="utf-8"))["measured"]
    print(f"lip soil {100 * measured['lip_soil_share']:.2f} % outside the outlines")
    assert 0.002 < measured["lip_soil_share"] < 0.06
    bare = patches.layout(**patches.inputs(PARAMS))["patches"]
    bridges = [derive.bridge(p, bare[p["after"]]) for p in bare if p["after"] is not None]
    assert bridges, "the layout lays patches along paths"
    lip = derive.lip_layer(bare, 256, 12)
    for py in range(0, 256, 3):
        for px in range(0, 256, 3):
            x, z = ((px + 0.5) / 256 - 0.5) * 12, (0.5 - (py + 0.5) / 256) * 12
            d = min(patches.distance((x, z), p["outline"]) for p in bare)
            if min([d] + [patches.distance((x, z), b) for b in bridges]) > derive.LIP_M + 0.1:
                assert lip.getpixel((px, py)) == 0, f"a lip {d:.2f} m out at {(x, z)}"
            elif -0.02 < d < 0.02 or min([1.0] + [patches.distance((x, z), b) for b in bridges]) < -0.05:
                assert lip.getpixel((px, py)) >= 128, f"no lip at the outline or on a bridge at {(x, z)}"


def test_straws_lie_strewn_over_the_floors(tmp_path, monkeypatch):
    """R6 (H 12, H 14): a floor reads as soil with straws lying on it, not a hole. Over a cover of straws, the floors'
    soil is covered by the cover's straws by about FLOOR_STRAWS more than without them, while the bare mask PR7 (c)
    checks stays as it was."""
    root = ground_root(tmp_path)
    cover = Image.new("RGB", (64, 64), (150, 125, 80))
    for x in range(0, 64, 3):
        cover.paste((230, 200, 140), (x, 0, x + 1, 64))
    cover.save(root / THIRD_PARTY / "polyhaven" / COVER_ID / f"{COVER_ID}_diffuse_1k.png")

    def measured(share):
        monkeypatch.setattr(derive, "FLOOR_STRAWS", share)
        assert run(MATERIAL, "--root", root) == 0
        return json.loads((root / MATERIALS / f"{MATERIAL}.recipe.json").read_text(encoding="utf-8"))["measured"]

    bare, strewn = measured(0.0), measured(derive.FLOOR_STRAWS)
    covered = 1 - strewn["floor_soil_share"] / bare["floor_soil_share"]
    print(f"floors' soil {100 * bare['floor_soil_share']:.1f} % -> {100 * strewn['floor_soil_share']:.1f} %: straws "
          f"over {100 * covered:.1f} % of it (FLOOR_STRAWS {100 * derive.FLOOR_STRAWS:.0f} %)")
    assert strewn["bare_soil_share"] == bare["bare_soil_share"], "the bare mask is untouched"
    assert abs(covered - derive.FLOOR_STRAWS) <= derive.FLOOR_STRAWS / 2
    assert abs(strewn["luminance_error_pct"]) <= 5


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

    write_params(root, dict(PARAMS, bare=dict(PARAMS["bare"], share=0.1)))  # the asset's layout changes
    assert run(MATERIAL, "--root", root) == 0
    assert "derived 256x256 ground" in capsys.readouterr().out
    assert json.loads(stamp.read_text(encoding="utf-8"))["layout"]["inputs"]["share"] == 0.1
    write_params(root, PARAMS)

    write_recipe(root, dict(RECIPE, seed=8))  # the strips move
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


@pytest.mark.parametrize("module, name, value", [
    (derive, "STRAW_M", 0.1), (derive, "LOBE_M", 0.8), (derive, "GAP_LOBES", 0.25), (derive, "RELIEF_TONE", 0.2),
    (derive, "LIP_M", 0.5), (derive, "GREEN_LEVEL", 0.8), (derive, "FLOOR_STRAWS", 0.3),
    (mat, "GREEN_FEATHER_M", 0.4), (mat, "GREEN_SPREAD_DEG", 20.0), (mat, "TUSSOCK_M", 0.6),
    (patches, "FEATHER_M", 0.2), (patches, "RADIAL_JITTER", 0.1), (patches, "WINDOW_M", 0.4),
    (patches, "EPS", 1e-3), (patches, "CLUSTER", 0.5), (patches, "PATH_GAP_M", [0.4, 1.5])])
def test_a_changed_formula_constant_re_derives(tmp_path, capsys, monkeypatch, module, name, value):
    """Spec "A formula constant changes" (review R1): the stamp records the value of every constant derive.py, the
    patch layout and the mat's fields read, under "constants" and "layout", and hashes them, so tuning one
    re-derives the maps instead of reporting them up to date."""
    root = ground_root(tmp_path)
    stamp_path = root / MATERIALS / f"{MATERIAL}.recipe.json"
    assert run(MATERIAL, "--root", root) == 0
    before = json.loads(stamp_path.read_text(encoding="utf-8"))
    assert before["constants"] == {n: list(v) if isinstance(v, tuple) else v
                                   for n, v in ((n, getattr(derive, n)) for n in derive.GROUND_CONSTANTS)}
    assert before["layout"]["constants"] == patches.constants() and before["layout"]["fields"] == mat.constants()
    assert run(MATERIAL, "--root", root) == 0 and "up to date" in capsys.readouterr().out
    monkeypatch.setattr(module, name, value)
    assert run(MATERIAL, "--root", root) == 0
    assert f"{MATERIAL}_albedo.png: derived 256x256 ground" in capsys.readouterr().out
    after = json.loads(stamp_path.read_text(encoding="utf-8"))
    recorded = {derive: after["constants"], patches: after["layout"]["constants"], mat: after["layout"]["fields"]}[module]
    assert recorded[name] == value and after["inputs_sha256"] != before["inputs_sha256"]


def test_the_bare_mask_and_the_step_outlines_agree():
    """review-physics PR7 (c), on S04's own layout at the belt_straw map's size: the bare mask derive.py stamps,
    strewn over a cover of straws and before the gaps are merged in, is at least 128 inside every outline shrunk by
    the feather and 0 outside every outline grown by 5 cm."""
    repo_root = Path(fetch.REPO_ROOT)
    recipe = json.loads((repo_root / RECIPES / "belt_straw.json").read_text(encoding="utf-8"))
    params = json.loads((repo_root / recipe["patches"]["bare"]["params"]).read_text(encoding="utf-8"))
    out_px, size_m = recipe["output_px"], recipe["size_m"]
    bare = patches.layout(**patches.inputs(params))["patches"]
    cover = Image.effect_noise((out_px, out_px), 80).convert("RGB")
    mask = derive.strewn(derive.bare_layer(bare, out_px, size_m), cover, random.Random(1), out_px / size_m)
    pixels = mask.load()

    def to_xz(px, py):  # a pixel's centre in asset metres, derive.to_px inverted
        return ((px + 0.5) / out_px - 0.5) * size_m, (0.5 - (py + 0.5) / out_px) * size_m

    inside = 0
    for patch in bare:
        x0, x1, z0, z1 = patch["window"]
        (px0, py0), (px1, py1) = derive.to_px((x0, z1), out_px, size_m), derive.to_px((x1, z0), out_px, size_m)
        for py in range(max(0, int(py0)), min(out_px, math.ceil(py1))):
            for px in range(max(0, int(px0)), min(out_px, math.ceil(px1))):
                if patches.distance(to_xz(px, py), patch["outline"]) <= -patches.FEATHER_M:
                    inside += 1
                    assert pixels[px, py] >= 128, f"soil missing at {to_xz(px, py)}: {pixels[px, py]}"
    outside = 0
    for i, value in enumerate(mask.tobytes()):  # an L image: one byte per pixel, row by row
        if value:
            xz = to_xz(i % out_px, i // out_px)
            near = [p for p in bare if p["window"][0] - 0.1 <= xz[0] <= p["window"][1] + 0.1
                    and p["window"][2] - 0.1 <= xz[1] <= p["window"][3] + 0.1]
            assert any(patches.distance(xz, p["outline"]) <= 0.05 for p in near), f"soil at {xz}, off every floor"
            outside += 1
    print(f"{inside} pixels inside the outlines shrunk by the feather, {outside} soil pixels, all within 5 cm of a floor")
    assert inside > 10000


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
    write_params(root, dict(PARAMS, bare=dict(PARAMS["bare"], share=0.9)), "assets-src/surfaces/too_much.json")
    write_params(root, dict(PARAMS, size_m=10), "assets-src/surfaces/smaller.json")
    write_params(root, {"seed": 1, "size_m": 12}, "assets-src/surfaces/no_bare.json")

    def bare_from(path):
        return dict(RECIPE, patches=dict(RECIPE["patches"], bare={"params": path},
                                         green=dict(RECIPE["patches"]["green"], params=path)))

    write_params(root, {k: v for k, v in PARAMS.items() if k != "green"}, "assets-src/surfaces/no_green.json")

    bad_colour = dict(RECIPE, albedo_target="7a7055")
    cases = [
        (without_cover, f"{MATERIAL}: missing object \"cover\""),
        (too_big, f"{MATERIAL}.output_px: 4096 must be an integer in [16, 2048]"),
        (bare_from("assets-src/surfaces/too_much.json"),
         f"{MATERIAL}.patches: the bare share 0.9 + green.share 0.15 exceeds 1"),
        (bare_from("assets-src/surfaces/nope.json"),
         f"{MATERIAL}.patches.bare.params: 'assets-src/surfaces/nope.json' is not an asset params file"),
        (bare_from("assets-src/surfaces/smaller.json"),
         f"{MATERIAL}.patches.bare.params: assets-src/surfaces/smaller.json has size_m 10, the recipe 12"),
        (bare_from("assets-src/surfaces/no_bare.json"),
         f"{MATERIAL}.patches.bare.params: assets-src/surfaces/no_bare.json lacks the layout key 'bare'"),
        (bad_colour, f"{MATERIAL}.albedo_target: '7a7055' is not an sRGB colour like #7a7055"),
        (bare_from("assets-src/surfaces/no_green.json"),
         f"{MATERIAL}.patches.green.params: assets-src/surfaces/no_green.json lacks the key 'green'"),
        (dict(RECIPE, patches=dict(RECIPE["patches"], green={"params": "assets-src/surfaces/smaller.json", "tint": "#6a7a3a"})),
         f"{MATERIAL}.patches.green.params: 'assets-src/surfaces/smaller.json' must name the bare patches' params file"),
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
