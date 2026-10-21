"""The recipes' colour encoding (p1-g1-look-lock-gallery task 1.4, QA's S8 from R01 task 1.4): for every recipe under
assets-src/materials, the colour the GPU draws, the base map's mean in linear light times the material tint as Unity
linearises a material colour, against the recipe's documented target. Tints are written sRGB-encoded (V13's
convention, p1-v13-shrub design 3): a tint written as a linear gain is linearised again, so the colour lands far
from its target (R01's car greens drew teal-blue), and the check fails it and says so. A recipe with no documented
target is listed, not failed. The table is printed (pytest -s shows it).

The base map is the one CatalogImporter assigns: the set's diffuse, or the derived _base map of a groove, a paint or
an alpha-clipped set with a separate opacity (its mean over the texels the cutoff keeps), the derived _albedo of a
ground (a wall strip's floor row) or a palette. Ground, palette and paint recipes carry their targets themselves;
the tinted sets' targets are their notes', in TARGETS."""
import json
import math
from pathlib import Path

import pytest
from PIL import Image

import derive
import fetch
from lib import strip  # tools/blender, which derive.py puts on the path

REPO = Path(fetch.REPO_ROOT)
RECIPES = REPO / "assets-src" / "materials"
MATERIALS = REPO / "unity" / "Assets" / "_Project" / "Art" / "Materials"
TOLERANCE = 3  # sRGB levels per channel: an encoded tint lands within 1-2 (R01's QA); a linear gain misses by more

# The documented target of each tinted-set recipe: (sRGB, where it is written, the texels it is the mean of).
TARGETS = {
    "bark_locust": ("#6e6559", "p1-v01-locust design 5", "ridges"),  # the AO at or above the groove's "from"
    "bark_shrub": ("#7a7466", "p1-v13-shrub design 3", None),
    "belt_soil": ("#4a463f", "belt_straw soil.albedo_target (p1-s04-belt-straw design)", None),
    "leaf_locust": ("#4f6a2e", "p1-v01-locust design 5", None),
    "leaf_locust_autumn": ("#746f30", "p1-v01-locust design 5", None),
    "leaf_shrub": ("#58602c", "p1-v13-shrub design 3", None),
    "leaf_shrub_autumn": ("#756d30", "p1-v01-locust design 5", None),
    "pole_concrete": ("#9a978e", "p1-l01-l03-pole-wires design", None),
    "pole_steel": ("#5e4a3a", "p1-l01-l03-pole-wires design", None),
    "pole_wood": ("#8a8478", "p1-l01-l03-pole-wires design", None),
}


def unity_linear(c):
    """A material colour channel as the GPU gets it in a linear-space project: Unity's gamma-to-linear conversion
    (UnityCG.cginc GammaToLinearSpaceExact), the sRGB curve below 1 and c^2.2 from 1 up."""
    if c <= 0.04045:
        return c / 12.92
    return ((c + 0.055) / 1.055) ** 2.4 if c < 1.0 else c ** 2.2


def hex_linear(colour):
    return [derive.SRGB_TO_LINEAR[int(colour[i:i + 2], 16)] for i in (1, 3, 5)]


def hex_of(linear):
    return "#" + "".join(f"{derive.to_srgb8(c):02x}" for c in linear)


def mean_linear(image, keep=None):
    """Per-channel linear mean of an 8-bit sRGB image over the texels `keep` (an L mask) marks, or all of them."""
    counts = image.convert("RGB").histogram(keep)
    means = []
    for band in range(3):
        levels = counts[256 * band:256 * (band + 1)]
        means.append(sum(n * derive.SRGB_TO_LINEAR[v] for v, n in enumerate(levels)) / sum(levels))
    return means


def check(mean, tint, target):
    """(drawn, as a linear gain, worst channel error in sRGB levels, verdict) of a base mean in linear light under a
    material tint, against an sRGB target. The verdict is "pass", or "FAIL" with "(linear gain)" when the tint read
    as linear factors would have landed on the target: the encoding error this test exists for."""
    drawn = [m * unity_linear(t) for m, t in zip(mean, tint)]
    as_linear = [m * t for m, t in zip(mean, tint)]

    def error(linear):
        return max(abs(derive.to_srgb8(c) - int(target[i:i + 2], 16)) for c, i in zip(linear, (1, 3, 5)))

    err = error(drawn)
    verdict = "pass" if err <= TOLERANCE else "FAIL (linear gain)" if error(as_linear) <= TOLERANCE else "FAIL"
    return hex_of(drawn), hex_of(as_linear), err, verdict


def recipe(name):
    return json.loads((RECIPES / f"{name}.json").read_text(encoding="utf-8"))


def image(path):
    if not path.is_file() or path.stat().st_size < 1024:
        pytest.skip(f"{path.name} is not here (git lfs checkout, or run derive.py / fetch.py)")
    return Image.open(path)


def base_mean(name, r, part):
    """The linear mean of the base map CatalogImporter assigns to a tinted set, over the texels that show."""
    texture_set = r["texture_set"]
    manifest = fetch.load_manifest(str(REPO))
    maps = derive.set_maps(str(REPO), manifest, texture_set, name)
    if "groove" in r or ("alpha_clip" in r and "opacity" in maps):
        base = image(MATERIALS / f"{name}_base.png")
    else:
        base = image(Path(maps["diffuse"]))
    keep = None
    if "alpha_clip" in r and base.mode == "RGBA":
        cut = math.ceil(r["alpha_clip"] * 255)
        keep = base.getchannel("A").point(lambda v: 255 if v >= cut else 0)
    if part == "ridges":  # the texels the groove leaves alone: the cavity map at or above its "from"
        cavity = derive.grey(image(Path(maps[r["groove"]["cavity"]])))
        keep = cavity.point(lambda v: 255 if v >= r["groove"]["from"] else 0)
    return mean_linear(base, keep)


def rows():
    """One row per recipe (per colour of a palette): (recipe, kind, target, drawn, as linear, error, verdict, source)."""
    out = []
    for path in sorted(RECIPES.glob("*.json")):
        name = path.stem
        r = recipe(name)
        if "far_card_of" in r:  # the importer builds it from its leaf recipe: the leaf's tint over the leaf's maps
            name, r = r["far_card_of"], recipe(r["far_card_of"])
            label = f"{path.stem} (as {name})"
        else:
            label = name
        tint = r["tint"][:3]
        kind = r.get("kind")
        if kind == "palette":
            for colour, stops in r["colours"].items():
                stops = stops if isinstance(stops, list) else [stops]
                linear = [sum(c) / len(c) for c in zip(*(hex_linear(s) for s in stops))]
                target = hex_of(linear)
                out.append((f"{label}:{colour}", "palette", target, *check(linear, tint, target), "its colours"))
        elif kind == "ground" and r.get("space") == "wall":
            albedo = image(MATERIALS / f"{name}_albedo.png")
            px_v = albedo.size[1] / strip.V_M
            bottom, top = strip.FLOOR_ROW
            row = albedo.crop((0, round((strip.V_M - top) * px_v), albedo.size[0], round((strip.V_M - bottom) * px_v)))
            target = r["floor_albedo"]
            out.append((f"{label} (floor row)", "wall", target, *check(mean_linear(row), tint, target),
                        "its floor_albedo"))
        elif kind == "ground":
            target = r["albedo_target"]
            out.append((label, "ground", target, *check(mean_linear(image(MATERIALS / f"{name}_albedo.png")), tint,
                                                         target), "its albedo_target"))
        elif "paint" in r:
            target = r["paint"]["colour"]
            out.append((label, "paint", target, *check(hex_linear(target), tint, target), "its paint colour"))
        elif name in TARGETS:
            target, source, part = TARGETS[name]
            what = f"{label} ({part})" if part else label
            out.append((what, "set", target, *check(base_mean(name, r, part), tint, target), source))
        else:
            mean = base_mean(name, r, None)
            drawn = hex_of([m * unity_linear(t) for m, t in zip(mean, tint)])
            out.append((label, "set", "-", drawn, "-", None, "listed: no documented target", "-"))
    return out


def test_every_recipe_draws_its_documented_target():
    table = rows()
    print(f"\n{'recipe':46} {'kind':8} {'target':8} {'drawn':8} {'as lin.':8} {'err':>3}  verdict  (target from)")
    for name, kind, target, drawn, as_linear, err, verdict, source in table:
        print(f"{name:46} {kind:8} {target:8} {drawn:8} {as_linear:8} {'-' if err is None else err:>3}  {verdict}"
              f"  ({source})")
    failed = [row[0] for row in table if row[6].startswith("FAIL")]
    assert not failed, f"drawn off target by more than {TOLERANCE} sRGB levels: {', '.join(failed)}"


def test_the_check_fails_a_linear_gain_and_passes_its_encoding():
    """The R01 case on LeafSet002's opaque mean, #7f9147 taken to a green: the linear gains that land it, written as the
    tint, fail as a linear gain; the same gains sRGB-encoded pass."""
    mean, target = hex_linear("#7f9147"), "#4f6a2e"
    gains = [t / m for t, m in zip(hex_linear(target), mean)]
    assert check(mean, gains, target)[3] == "FAIL (linear gain)"
    encoded = [1.055 * g ** (1 / 2.4) - 0.055 for g in gains]  # each below 1, so the sRGB curve
    assert check(mean, encoded, target)[3] == "pass"
