"""The layered recipes of the repo: one format for the car and the house (p1-r01-vaz-sedan design decision 5;
p1-h01-adobe-house design decision 2), which Shaders/LayeredSurface and the catalog importer read.

A layered recipe is an ordinary recipe, the material its slot draws without a damage mask, that lists "layers": 1 to
5 films over a base, in order, each {"channel": "r" | "g" | "b" | "a", "recipe", "threshold": 0-1, "feather": (0, 1]}
covering saturate((channel - threshold) / feather + 0.5) of what lies below it (threshold 0.5 and feather 1 by
default: the channel's value), each layer an opaque tiled set or a palette of one colour. The base is the recipe's own
surface, or its optional "base" recipe's. Its optional "channels" name what the mask's R, G, B and A hold, the names
a mask record lists under "channels" (lib.meta): the importer refuses a record that names others.

The house's facade and interior: their own material is the bare adobe, on URP/Lit with a constant smoothness (the house
builds before its mask exists), and their layers lie over the whitewash (the interior's blue): render by R from 0.25,
the base band by A (paint over the render, under the bare adobe), adobe by R from 0.75, both feathered over 0.1 (R 0
whitewash, 0.5 render, 1 adobe), then soot by G and dirt by B, each covering its channel's value.
"""
import json
import re
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[3]
RECIPES = ROOT / "assets-src" / "materials"
HEX = re.compile(r"^#[0-9a-f]{6}$")
CHANNELS = ("r", "g", "b", "a")
LAYER_KEYS = {"channel", "recipe", "threshold", "feather"}
MAX_LAYERS = 5  # the shader's
HOUSE = {"facade_h01": "whitewash", "interior_h01": "whitewash_blue"}
HOUSE_CHANNELS = ["render_depth", "soot", "dirt", "base_band"]
HOUSE_LAYERS = [
    {"channel": "r", "recipe": "clay_render", "threshold": 0.25, "feather": 0.1},
    {"channel": "a", "recipe": "base_band"},
    {"channel": "r", "recipe": "adobe", "threshold": 0.75, "feather": 0.1},
    {"channel": "g", "recipe": "wall_soot"},
    {"channel": "b", "recipe": "wall_dirt"},
]


def recipe(name):
    return json.loads((RECIPES / f"{name}.json").read_text(encoding="utf-8"))


ALL = sorted(p.stem for p in RECIPES.glob("*.json"))
LAYERED = [name for name in ALL if "layers" in recipe(name)]


def test_one_format():
    assert not [name for name in ALL if "layered" in recipe(name)], "1.1's layered block is gone"
    assert set(HOUSE) <= set(LAYERED)


def surface_problems(name):
    """Why a recipe cannot be a layered surface's base or layer: it must be an opaque URP/Lit tiled set or a palette of
    one colour (its columns would stripe the metres)."""
    r = recipe(name)
    problems = [f"{name}: {key}" for key in ("alpha_clip", "two_sided", "blend") if key in r]
    if r["shader"] != "Universal Render Pipeline/Lit":
        problems.append(f"{name}: shader {r['shader']}")
    if r.get("kind") == "palette":
        if len(r["colours"]) != 1:
            problems.append(f"{name}: a palette of {len(r['colours'])} colours")
    elif "kind" in r or "texture_set" not in r:
        problems.append(f"{name}: neither a tiled set nor a palette")
    return problems


@pytest.mark.parametrize("name", LAYERED)
def test_layers(name):
    r = recipe(name)
    layers = r["layers"]
    assert 1 <= len(layers) <= MAX_LAYERS
    named = r.get("channels")
    if named is not None:
        assert 1 <= len(named) <= len(CHANNELS) and len(set(named)) == len(named)
        assert all(isinstance(c, str) and c for c in named)
    for layer in layers:
        assert set(layer) <= LAYER_KEYS and layer["channel"] in CHANNELS, layer
        assert 0 <= layer.get("threshold", 0.5) <= 1 and 0 < layer.get("feather", 1.0) <= 1, layer
        assert not surface_problems(layer["recipe"])
        if named is not None:
            assert CHANNELS.index(layer["channel"]) < len(named), f"{name}: {layer} reads a channel it does not name"
    if "base" in r:
        assert not surface_problems(r["base"])


@pytest.mark.parametrize("name", sorted(HOUSE))
def test_house_fallback_is_the_bare_adobe(name):
    r, adobe = recipe(name), recipe("adobe")
    assert "kind" not in r and r["shader"] == "Universal Render Pipeline/Lit"
    assert {k: r[k] for k in ("texture_set", "tile_size_m", "tint")} == \
        {k: adobe[k] for k in ("texture_set", "tile_size_m", "tint")}
    assert "mask" not in r and 0 <= r["smoothness"] <= 1, "a constant smoothness: no derived duplicate of the adobe's mask"


@pytest.mark.parametrize("name", sorted(HOUSE))
def test_house_layers(name):
    r = recipe(name)
    assert r["base"] == HOUSE[name]
    assert r["channels"] == HOUSE_CHANNELS
    assert r["layers"] == HOUSE_LAYERS
    band, render = recipe("base_band"), recipe("clay_render")
    assert {k: band[k] for k in ("texture_set", "tile_size_m", "mask")} == \
        {k: render[k] for k in ("texture_set", "tile_size_m", "mask")}, "the band is paint on the render: its relief"
    for palette in ("wall_soot", "wall_dirt"):
        assert all(HEX.match(c) for c in recipe(palette)["colours"].values())
