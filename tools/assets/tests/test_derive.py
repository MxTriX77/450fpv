"""derive.py against the spec scenario "Packed mask for a material": the mask's channels from synthetic sources
(metallic R from the recipe, AO G from a 16-bit source scaled not clipped, smoothness A = 1 - roughness, B 0), the
recipe recorded beside it, regeneration when the recipe or a source changes and a no-op when nothing did, --all over
every recipe with mask-less ones skipped, and the failures named: a map the set lacks, an unknown expression, a
malformed mask, a missing recipe; nothing is written on failure. And the base map of an alpha-clipped recipe
("Alpha-clipped two-sided recipes"): the diffuse's RGB with the set's separate opacity map as alpha, skipped when the
set has no opacity map, refused when the two differ in size. And (p1-v01-locust, design decision 5) a bark's grooves
tinted by its cavity map, each texel scaled in linear light by its weight between the recipe's levels, and the pod
strip painted from a recipe with no texture set; malformed groove and paint recipes refused."""
import json
from pathlib import Path

from PIL import Image

import derive
import fetch
from conftest import MAPS, SET_ID, THIRD_PARTY, make_root

SET_DIR = THIRD_PARTY / "polyhaven" / SET_ID
RECIPES = Path("assets-src/materials")
MATERIALS = Path("unity/Assets/_Project/Art/Materials")
SIZE = (8, 4)
MATERIAL = "fake_mat"
MASK = {"metallic": 0.0, "occlusion": "ao", "smoothness": "1 - roughness"}
SOURCES = {"roughness": f"{SET_ID}_rough_1k.png", "ao": f"{SET_ID}_ao_1k.png"}
OPACITY = f"{SET_ID}_opacity_1k.png"
DIFFUSE_RGB = (200, 100, 50)


def gradient(axis, bits):
    """An 8-bit L or 16-bit I;16 image rising along `axis` (0 = x, 1 = y) from 0 to full scale."""
    image = Image.new("L" if bits == 8 else "I;16", SIZE)
    top = (1 << bits) - 1
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            t = x / (SIZE[0] - 1) if axis == 0 else y / (SIZE[1] - 1)
            image.putpixel((x, y), round(t * top))
    return image


def write_recipe(root, mask, alpha_clip=None):
    recipe = {"schema": 1, "shader": "Universal Render Pipeline/Lit", "texture_set": f"polyhaven/{SET_ID}",
              "tile_size_m": [1.0, 1.0], "tint": [1.0, 1.0, 1.0, 1.0]}
    if mask is None:
        recipe["smoothness"] = 0.25
    else:
        recipe["mask"] = mask
    if alpha_clip is not None:
        recipe["alpha_clip"] = alpha_clip
        recipe["two_sided"] = True
    (root / RECIPES).mkdir(parents=True, exist_ok=True)
    (root / RECIPES / f"{MATERIAL}.json").write_text(json.dumps(recipe, indent=2) + "\n", encoding="utf-8")


def derive_root(tmp_path):
    """conftest's fake root plus a real 8-bit roughness map (rising along x) and a 16-bit AO map (rising along y)
    pinned in the manifest, and a recipe asking for the mask."""
    root = make_root(tmp_path)
    gradient(0, 8).save(root / SET_DIR / SOURCES["roughness"])
    gradient(1, 16).save(root / SET_DIR / SOURCES["ao"])
    manifest_path = root / "tools" / "assets" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    for map_name, name in SOURCES.items():
        path = root / SET_DIR / name
        manifest["entries"][0]["files"].append({"map": map_name, "name": name, "url": f"https://dl.polyhaven.org/{name}",
                                                "bytes": path.stat().st_size, "sha256": fetch.sha256(path)})
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    write_recipe(root, MASK)
    return root


def base_root(tmp_path):
    """derive_root plus a real flat-colour diffuse in place of the fake bytes, an 8-bit opacity map rising along x
    pinned as the set's opacity, and a recipe with the mask and alpha_clip 0.5."""
    root = derive_root(tmp_path)
    Image.new("RGB", SIZE, DIFFUSE_RGB).save(root / SET_DIR / MAPS["diffuse"])
    gradient(0, 8).save(root / SET_DIR / OPACITY)
    manifest_path = root / "tools" / "assets" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    path = root / SET_DIR / OPACITY
    manifest["entries"][0]["files"].append({"map": "opacity", "name": OPACITY, "url": f"https://dl.polyhaven.org/{OPACITY}",
                                            "bytes": path.stat().st_size, "sha256": fetch.sha256(path)})
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    write_recipe(root, MASK, alpha_clip=0.5)
    return root


def run(*argv):
    """Run derive.main in-process; returns its exit code."""
    try:
        return derive.main([str(a) for a in argv])
    except SystemExit as e:  # argparse errors
        return e.code


def test_mask_channels_from_synthetic_sources(tmp_path, capsys):
    root = derive_root(tmp_path)
    assert run(MATERIAL, "--root", root) == 0
    out = capsys.readouterr().out
    assert (f"{MATERIALS.as_posix()}/{MATERIAL}_mask.png: derived 8x4 from polyhaven/{SET_ID} (ao, roughness); "
            f"recipe recorded in {MATERIAL}_mask.recipe.json") in out and "derive.py: OK" in out
    mask = Image.open(root / MATERIALS / f"{MATERIAL}_mask.png")
    assert mask.mode == "RGBA" and mask.size == SIZE
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            r, g, b, a = mask.getpixel((x, y))
            assert (r, b) == (0, 0), "metallic 0 from the recipe, B unused"
            assert g == round(y / (SIZE[1] - 1) * 255), "AO: the 16-bit gradient scaled to 8 bits"
            assert a == 255 - round(x / (SIZE[0] - 1) * 255), "smoothness = 1 - roughness"
    stamp = json.loads((root / MATERIALS / f"{MATERIAL}_mask.recipe.json").read_text(encoding="utf-8"))
    assert stamp["material"] == MATERIAL and stamp["texture_set"] == f"polyhaven/{SET_ID}"
    assert stamp["mask"] == MASK and stamp["formula"] == derive.FORMULA and stamp["size"] == [8, 4]
    assert {k: v["name"] for k, v in stamp["sources"].items()} == SOURCES
    for map_name, name in SOURCES.items():
        assert stamp["sources"][map_name]["sha256"] == fetch.sha256(root / SET_DIR / name)
    assert len(stamp["inputs_sha256"]) == 64


def test_regenerated_on_recipe_or_source_change_and_a_no_op_otherwise(tmp_path, capsys):
    root = derive_root(tmp_path)
    png, stamp = root / MATERIALS / f"{MATERIAL}_mask.png", root / MATERIALS / f"{MATERIAL}_mask.recipe.json"
    assert run(MATERIAL, "--root", root) == 0
    first = (png.read_bytes(), stamp.read_bytes(), png.stat().st_mtime_ns, stamp.stat().st_mtime_ns)
    assert run(MATERIAL, "--root", root) == 0
    assert f"{MATERIAL}_mask.png: up to date" in capsys.readouterr().out
    assert (png.read_bytes(), stamp.read_bytes(), png.stat().st_mtime_ns, stamp.stat().st_mtime_ns) == first

    write_recipe(root, dict(MASK, metallic=0.5))  # a recipe change regenerates: R = 128 everywhere
    assert run(MATERIAL, "--root", root) == 0
    assert "derived 8x4" in capsys.readouterr().out
    assert Image.open(png).getpixel((3, 2))[0] == 128
    assert json.loads(stamp.read_text(encoding="utf-8"))["mask"]["metallic"] == 0.5

    ao = root / SET_DIR / SOURCES["ao"]
    Image.new("L", SIZE, 7).save(ao)  # a source change regenerates: G = 7 everywhere
    assert run(MATERIAL, "--root", root) == 0
    assert "derived 8x4" in capsys.readouterr().out
    assert Image.open(png).getpixel((0, 0))[1] == 7

    ao.write_bytes(ao.read_bytes())  # rewritten but identical: the stamp hashes content, not times
    assert run(MATERIAL, "--root", root) == 0
    assert "up to date" in capsys.readouterr().out

    png.unlink()  # a missing output is rebuilt even with a current stamp
    assert run(MATERIAL, "--root", root) == 0
    assert "derived 8x4" in capsys.readouterr().out and png.is_file()


def test_all_derives_every_recipe_and_skips_those_without_a_mask(tmp_path, capsys):
    root = derive_root(tmp_path)
    plain = {"schema": 1, "shader": "Universal Render Pipeline/Lit", "texture_set": f"polyhaven/{SET_ID}",
             "tile_size_m": [1.0, 1.0], "tint": [1.0, 1.0, 1.0, 1.0], "smoothness": 0.25}
    (root / RECIPES / "plain.json").write_text(json.dumps(plain), encoding="utf-8")
    assert run("--all", "--root", root) == 0
    out = capsys.readouterr().out
    assert f"{MATERIAL}_mask.png: derived" in out and "plain: no mask in the recipe, nothing to derive" in out
    assert (root / MATERIALS / f"{MATERIAL}_mask.png").is_file() and not (root / MATERIALS / "plain_mask.png").exists()


def test_palette_recipe_derives_one_block_per_colour_in_order(tmp_path, capsys, monkeypatch):
    """p1-s04-belt-straw: a palette recipe (the straw kit's tints) becomes an albedo of one 16 x 64 px column per
    colour left to right, sRGB-encoded, flat for a single colour, with its stamp; a second run is a no-op; a palette
    with a texture set is refused."""
    root = derive_root(tmp_path)
    palette = {"schema": 1, "kind": "palette", "shader": "Universal Render Pipeline/Lit",
               "colours": {"straw": "#b49c6a", "twig": "#6b5a3e"}, "tint": [1.0, 1.0, 1.0, 1.0], "smoothness": 0.3}
    (root / RECIPES / "pal.json").write_text(json.dumps(palette), encoding="utf-8")
    assert run("pal", "--root", root) == 0
    out = capsys.readouterr().out
    assert f"{MATERIALS.as_posix()}/pal_albedo.png: derived 32x64 palette (straw #b49c6a, twig #6b5a3e); recipe recorded in pal.recipe.json" in out
    image = Image.open(root / MATERIALS / "pal_albedo.png")
    assert image.mode == "RGB" and image.size == (32, 64)
    for y in (0, 31, 63):
        assert image.getpixel((8, y)) == (0xb4, 0x9c, 0x6a) and image.getpixel((24, y)) == (0x6b, 0x5a, 0x3e)
    stamp = json.loads((root / MATERIALS / "pal.recipe.json").read_text(encoding="utf-8"))
    assert stamp["kind"] == "palette" and stamp["columns"] == ["straw", "twig"] and stamp["formula"] == derive.PALETTE_FORMULA
    assert stamp["constants"] == {"PALETTE_BLOCK_PX": 16, "PALETTE_HEIGHT_PX": 64}
    assert run("pal", "--root", root) == 0 and "pal_albedo.png: up to date" in capsys.readouterr().out
    monkeypatch.setattr(derive, "PALETTE_BLOCK_PX", 8)  # a changed constant regenerates (review R1)
    assert run("pal", "--root", root) == 0 and "derived 16x64 palette" in capsys.readouterr().out
    monkeypatch.undo()
    (root / RECIPES / "pal.json").write_text(json.dumps(dict(palette, texture_set="polyhaven/x")), encoding="utf-8")
    assert run("pal", "--root", root) == 1
    assert 'ERROR: pal: a palette recipe is flat colour; it takes neither "texture_set" nor "mask"' in capsys.readouterr().out


def test_base_map_packs_the_opacity_into_the_diffuse_alpha(tmp_path, capsys):
    root = base_root(tmp_path)
    assert run(MATERIAL, "--root", root) == 0
    out = capsys.readouterr().out
    assert f"{MATERIAL}_mask.png: derived 8x4" in out, "the mask of the same recipe is derived too"
    assert (f"{MATERIALS.as_posix()}/{MATERIAL}_base.png: derived 8x4 from polyhaven/{SET_ID} (diffuse, opacity); "
            f"recipe recorded in {MATERIAL}_base.recipe.json") in out and "derive.py: OK" in out
    base = Image.open(root / MATERIALS / f"{MATERIAL}_base.png")
    assert base.mode == "RGBA" and base.size == SIZE
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            assert base.getpixel((x, y)) == DIFFUSE_RGB + (round(x / (SIZE[0] - 1) * 255),), "RGB diffuse, A opacity"
    stamp = json.loads((root / MATERIALS / f"{MATERIAL}_base.recipe.json").read_text(encoding="utf-8"))
    assert stamp["material"] == MATERIAL and stamp["formula"] == derive.BASE_FORMULA and "mask" not in stamp
    assert {k: v["name"] for k, v in stamp["sources"].items()} == {"diffuse": MAPS["diffuse"], "opacity": OPACITY}
    assert stamp["sources"]["opacity"]["sha256"] == fetch.sha256(root / SET_DIR / OPACITY) and stamp["size"] == [8, 4]

    assert run(MATERIAL, "--root", root) == 0
    assert f"{MATERIAL}_base.png: up to date" in capsys.readouterr().out

    manifest_path = root / "tools" / "assets" / "manifest.json"  # a set without a separate opacity map: nothing to pack
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["entries"][0]["files"] = [f for f in manifest["entries"][0]["files"] if f["map"] != "opacity"]
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    (root / MATERIALS / f"{MATERIAL}_base.png").unlink()
    assert run(MATERIAL, "--root", root) == 0
    assert f"{MATERIAL}: polyhaven/{SET_ID} has no separate opacity map, the diffuse's own alpha clips" in capsys.readouterr().out
    assert not (root / MATERIALS / f"{MATERIAL}_base.png").exists()


def test_base_map_refuses_sources_of_different_size(tmp_path, capsys):
    root = base_root(tmp_path)
    Image.new("RGB", (SIZE[0] * 2, SIZE[1]), DIFFUSE_RGB).save(root / SET_DIR / MAPS["diffuse"])
    assert run(MATERIAL, "--root", root) == 1
    assert f"ERROR: {MATERIAL}: the sources differ in size: diffuse 16x4, opacity 8x4" in capsys.readouterr().out
    assert not (root / MATERIALS / f"{MATERIAL}_base.png").exists()


GROOVE = {"cavity": "ao", "from": 200, "to": 100, "tint": [2.0, 1.0, 0.5]}


def groove_root(tmp_path, groove=GROOVE):
    """derive_root plus a flat (100, 100, 100) diffuse, an 8-bit AO map of 255 / 150 / 50 / 0 across its columns
    pinned as the set's AO, and a recipe with the mask and the groove."""
    root = derive_root(tmp_path)
    Image.new("RGB", SIZE, (100, 100, 100)).save(root / SET_DIR / MAPS["diffuse"])
    ao = Image.new("L", SIZE)
    for x in range(SIZE[0]):
        for y in range(SIZE[1]):
            ao.putpixel((x, y), (255, 150, 50, 0)[x % 4])
    ao.save(root / SET_DIR / SOURCES["ao"])
    manifest_path = root / "tools" / "assets" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    for file in manifest["entries"][0]["files"]:
        path = root / SET_DIR / file["name"]
        file["bytes"], file["sha256"] = path.stat().st_size, fetch.sha256(path)
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    recipe = json.loads((root / RECIPES / f"{MATERIAL}.json").read_text(encoding="utf-8"))
    recipe["groove"] = groove
    (root / RECIPES / f"{MATERIAL}.json").write_text(json.dumps(recipe, indent=2) + "\n", encoding="utf-8")
    return root


def test_groove_tints_the_grooves_by_the_cavity_in_linear_light(tmp_path, capsys):
    root = groove_root(tmp_path)
    assert run(MATERIAL, "--root", root) == 0
    out = capsys.readouterr().out
    assert (f"{MATERIALS.as_posix()}/{MATERIAL}_base.png: derived 8x4 from polyhaven/{SET_ID} (diffuse, ao); "
            f"recipe recorded in {MATERIAL}_base.recipe.json") in out
    base = Image.open(root / MATERIALS / f"{MATERIAL}_base.png")
    assert base.mode == "RGB" and base.size == SIZE
    grey = derive._decode(100)
    for x, weight in enumerate((0.0, 0.5, 1.0, 1.0)):  # AO 255 and 150 over from 200 to 100, then 50 and 0
        expected = tuple(derive._encode(grey * (1 + (f - 1) * weight)) for f in GROOVE["tint"])
        assert base.getpixel((x, 1)) == expected, x
    assert base.getpixel((0, 0)) == (100, 100, 100) and base.getpixel((2, 0))[0] > 100 > base.getpixel((2, 0))[2]
    stamp = json.loads((root / MATERIALS / f"{MATERIAL}_base.recipe.json").read_text(encoding="utf-8"))
    assert stamp["groove"] == GROOVE and stamp["formula"] == derive.GROOVE_FORMULA
    assert {k: v["name"] for k, v in stamp["sources"].items()} == {"diffuse": MAPS["diffuse"], "ao": SOURCES["ao"]}
    assert run(MATERIAL, "--root", root) == 0
    assert f"{MATERIAL}_base.png: up to date" in capsys.readouterr().out


def paint_recipe(root, paint):
    recipe = {"schema": 1, "shader": "Universal Render Pipeline/Lit", "paint": paint, "tile_size_m": [1.0, 1.0],
              "tint": [1.0, 1.0, 1.0, 1.0], "smoothness": 0.2, "alpha_clip": 0.5, "two_sided": True}
    (root / RECIPES).mkdir(parents=True, exist_ok=True)
    (root / RECIPES / "pods.json").write_text(json.dumps(recipe, indent=2) + "\n", encoding="utf-8")


def test_paint_draws_the_pods_with_no_texture_set(tmp_path, capsys):
    root = make_root(tmp_path)
    paint = {"pods": 2, "size_px": [32, 64], "colour": "#5c3d28"}
    paint_recipe(root, paint)
    assert run("pods", "--root", root) == 0
    assert f"{MATERIALS.as_posix()}/pods_base.png: painted 32x64; recipe recorded in pods_base.recipe.json" in capsys.readouterr().out
    base = Image.open(root / MATERIALS / "pods_base.png")
    assert base.mode == "RGBA" and base.size == (32, 64)
    assert all(base.getpixel((x, 63))[3] == 0 for x in range(32)) and base.getpixel((0, 0))[3] == 0
    row = [base.getpixel((x, 32)) for x in range(32)]
    assert row[0][3] == row[16][3] == row[31][3] == 0, "clear at the sides and between the pods"
    for half in (row[:16], row[16:]):  # each pod's face, inside its darker rim, in the given colour
        assert (0x5c, 0x3d, 0x28, 255) in half and any(p[3] == 255 and p[0] < 0x5c for p in half)
    stamp = json.loads((root / MATERIALS / "pods_base.recipe.json").read_text(encoding="utf-8"))
    assert stamp["paint"] == paint and stamp["formula"] == derive.PAINT_FORMULA and stamp["sources"] == {}
    assert run("pods", "--root", root) == 0
    assert "pods_base.png: up to date" in capsys.readouterr().out


def test_groove_and_paint_failures_name_the_cause(tmp_path, capsys):
    root = groove_root(tmp_path, dict(GROOVE, cavity="displacement"))
    assert run(MATERIAL, "--root", root) == 1
    assert (f"ERROR: {MATERIAL}: groove.cavity: polyhaven/{SET_ID} has no displacement map "
            "(it has ao, diffuse, normal, roughness)") in capsys.readouterr().out
    for n, (groove, message) in enumerate((
            (dict(GROOVE, to=220), "groove needs 0 <= to < from <= 255 on the 8-bit cavity map"),
            (dict(GROOVE, tint=[1.0, 0.0, 1.0]), "groove.tint must be three positive linear factors"),
            ({"cavity": "ao"}, "'groove' must hold exactly cavity, from, to, tint"))):
        groove_root(tmp_path / f"case{n}", groove)
        assert run(MATERIAL, "--root", tmp_path / f"case{n}") == 1
        assert f"ERROR: {MATERIAL}: {message}" in capsys.readouterr().out
        assert not (tmp_path / f"case{n}" / MATERIALS / f"{MATERIAL}_base.png").exists()
    paint_recipe(root, {"pods": 0, "size_px": [32, 64], "colour": "#5c3d28"})
    assert run("pods", "--root", root) == 1
    assert "ERROR: pods: paint needs pods >= 1, size_px [w, h] of 8-2048 px and colour '#rrggbb'" in capsys.readouterr().out
    recipe = json.loads((root / RECIPES / "pods.json").read_text(encoding="utf-8"))
    recipe["texture_set"] = f"polyhaven/{SET_ID}"
    (root / RECIPES / "pods.json").write_text(json.dumps(recipe), encoding="utf-8")
    assert run("pods", "--root", root) == 1
    assert "ERROR: pods: give a \"texture_set\" or a \"paint\", not both or neither" in capsys.readouterr().out


def test_failures_name_the_cause_and_write_nothing(tmp_path, capsys):
    root = derive_root(tmp_path)
    cases = [
        (dict(MASK, occlusion="displacement"),
         f"{MATERIAL}: mask.occlusion: polyhaven/{SET_ID} has no displacement map (it has ao, diffuse, normal, roughness)"),
        (dict(MASK, smoothness="roughness * 2"),
         f"{MATERIAL}: mask.smoothness: 'roughness * 2' is neither a constant in [0, 1] nor '<map>' or '1 - <map>'"),
        (dict(MASK, metallic=2), f"{MATERIAL}: mask.metallic: constant 2 is outside [0, 1]"),
        ({"metallic": 0.0}, f"{MATERIAL}: 'mask' must hold exactly metallic, occlusion, smoothness"),
    ]
    for mask, message in cases:
        write_recipe(root, mask)
        assert run(MATERIAL, "--root", root) == 1
        assert f"ERROR: {message}" in capsys.readouterr().out
    assert run("no_such", "--root", root) == 1
    assert f"ERROR: no_such: no recipe at {RECIPES.as_posix()}/no_such.json" in capsys.readouterr().out
    assert run("--root", root) == 2  # nothing to do
    assert not (root / MATERIALS).exists()


def test_palette_gradients_run_up_their_columns_padded_to_a_power_of_two(tmp_path, capsys):
    """Task 1.7: a colour given as stops is a gradient from the first at the bottom of its column to the last at the
    top, interpolated in linear light, so the column's linear mean is (s0 + 2 s1 + s2) / 4 for three stops; three
    colours make four columns, the last one repeated."""
    root = derive_root(tmp_path)
    stops = ["#40382a", "#b49c6a", "#e0cc9a"]
    palette = {"schema": 1, "kind": "palette", "shader": "Universal Render Pipeline/Lit",
               "colours": {"a": stops, "b": "#6b5a3e", "c": ["#202020", "#606060"]}, "tint": [1.0, 1.0, 1.0, 1.0],
               "smoothness": 0.3}
    (root / RECIPES / "grad.json").write_text(json.dumps(palette), encoding="utf-8")
    assert run("grad", "--root", root) == 0
    assert "grad_albedo.png: derived 64x64 palette (a #40382a/#b49c6a/#e0cc9a, b #6b5a3e, c #202020/#606060)" in capsys.readouterr().out
    image = Image.open(root / MATERIALS / "grad_albedo.png")
    linear = [[derive.SRGB_TO_LINEAR[v] for v in image.getpixel((8, y))] for y in range(64)]
    bottom, top = image.getpixel((8, 63)), image.getpixel((8, 0))
    assert all(abs(a - b) <= 6 for a, b in zip(bottom, (0x40, 0x38, 0x2a))), "the first stop at the bottom"
    assert all(abs(a - b) <= 6 for a, b in zip(top, (0xe0, 0xcc, 0x9a))), "the last stop at the top"
    want = [(a + 2 * b + c) / 4 for a, b, c in zip(*([derive.SRGB_TO_LINEAR[int(h[i:i + 2], 16)] for i in (1, 3, 5)]
                                                     for h in stops))]
    got = [sum(row[k] for row in linear) / 64 for k in range(3)]
    assert all(abs(g - w) <= 0.01 for g, w in zip(got, want)), (got, want)
    assert image.getpixel((56, 10)) == image.getpixel((40, 10)), "the padding column repeats the last colour"
