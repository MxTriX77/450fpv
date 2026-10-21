"""The layered recipes of the repo (p1-h01-adobe-house, design decision 2; the facade shader is task 1.2's).

A layered recipe is a tiled-set recipe whose top-level keys are its fallback, the material the importer and derive.py
build today (they read no other key): the set, tile and tint of its last layer, the one its mask's R = 1 shows (the
house's bare adobe, "until it lands the importer binds the adobe set alone"), with a constant smoothness so it derives
no duplicate mask. Its "layered" block is for the layered shader alone:
- "shader": the shader that draws it once the importer binds a slot's mask (task 1.2 may rename it);
- "mask_role": the metadata texture role it reads from its slot ("damage_mask", lib.meta.TEXTURE_ROLES);
- "channels": what that mask's R, G, B and A hold, the names a mask record lists under "channels" (lib.meta);
- "layers": the R channel's layers, each a tiled-set recipe with a packed mask, at R values ascending from 0 to 1;
  between two layers the shader blends over "feather" (in R) about their midpoint;
- one block per further channel, named as the channel: "soot" (G) and "dirt" (B) darken towards their sRGB "colour"
  by up to "strength" at a channel value of 1, "base_band" (A) tints the layers above the last towards its "colour".
"""
import json
import re
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[3]
RECIPES = ROOT / "assets-src" / "materials"
HEX = re.compile(r"^#[0-9a-f]{6}$")


def recipe(name):
    return json.loads((RECIPES / f"{name}.json").read_text(encoding="utf-8"))


LAYERED = sorted(p.stem for p in RECIPES.glob("*.json") if "layered" in recipe(p.stem))


def test_the_house_recipes_are_layered():
    assert LAYERED == ["facade_h01", "interior_h01"]


@pytest.mark.parametrize("name", LAYERED)
def test_fallback_is_the_last_layer_on_urp_lit(name):
    r = recipe(name)
    last = recipe(r["layered"]["layers"][-1]["recipe"])
    assert "kind" not in r and r["shader"] == "Universal Render Pipeline/Lit"
    assert {k: r[k] for k in ("texture_set", "tile_size_m", "tint")} == \
        {k: last[k] for k in ("texture_set", "tile_size_m", "tint")}
    assert "mask" not in r and 0 <= r["smoothness"] <= 1, "a constant smoothness: no derived duplicate of the layer's mask"


@pytest.mark.parametrize("name", LAYERED)
def test_layered_block(name):
    block = recipe(name)["layered"]
    assert isinstance(block["shader"], str) and block["shader"]
    assert block["mask_role"] == "damage_mask"
    channels = block["channels"]
    assert channels == ["render_depth", "soot", "dirt", "base_band"]
    at = [layer["at"] for layer in block["layers"]]
    assert len(at) >= 2 and at[0] == 0 and at[-1] == 1 and at == sorted(set(at))
    for layer in block["layers"]:
        r = recipe(layer["recipe"])
        assert "kind" not in r and "layered" not in r and "mask" in r, f"{layer['recipe']}: a tiled set with a mask"
    gaps = [b - a for a, b in zip(at, at[1:])]
    assert 0 < block["feather"] < min(gaps)
    for channel in channels[1:]:
        assert HEX.match(block[channel]["colour"])
    for channel in ("soot", "dirt"):
        assert 0 <= block[channel]["strength"] <= 1
