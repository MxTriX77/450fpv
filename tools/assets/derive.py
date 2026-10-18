"""Derived textures by script (p1-pipeline-hardening, "Derived textures by script"; D-004, AST-07): the packed mask
map of a material recipe, and the base map of an alpha-clipped one, built from its pinned sources with the recipe
recorded beside it. Pillow only.

    python tools/assets/derive.py <material>...     derive the maps of these recipes (assets-src/materials/<material>.json)
    python tools/assets/derive.py --all             every recipe under assets-src/materials/

A recipe with a "mask" object gets unity/Assets/_Project/Art/Materials/<material>_mask.png, an 8-bit linear RGBA map
that URP/Lit reads twice: _MetallicGlossMap takes metallic from R and smoothness from A, _OcclusionMap takes occlusion
from G; B is unused and 0. The object holds exactly "metallic", "occlusion" and "smoothness", each a constant in
[0, 1] or a map of the recipe's texture set by its manifest map name ("ao", "roughness", ...), inverted as
"1 - roughness". The mask has the sources' size; 16-bit sources are scaled to 8 bits, never clipped.

A recipe with "alpha_clip" (p1-v13-shrub, "Alpha-clipped two-sided recipes") whose texture set ships its opacity as
a separate map (ambientCG's atlases) gets <material>_base.png: the diffuse's RGB with the opacity map as alpha, an
8-bit sRGB RGBA map the importer assigns as the base map in place of the diffuse, so URP/Lit clips by its alpha. A
set without an "opacity" map needs nothing: the diffuse's own alpha clips.

Two more base maps (p1-v01-locust, design decision 5), assigned the same way:
- a "groove" object tints a bark's grooves: {"cavity": <map>, "from": <level>, "to": <level>, "tint": [r, g, b]}.
  The cavity weight runs linearly from 0 where the cavity map (8-bit, AO dark in the grooves) reads "from" or more
  to 1 where it reads "to" or less, and each diffuse texel, in linear light, is scaled by 1 + (tint - 1) * weight:
  the recipe's material tint holds the ridge colour and the groove tint the grooves' own on top of it (an 8-bit sRGB
  RGB map; a channel past white clips);
- a "paint" object, in place of a texture set, paints the base map by script: {"pods": <count>, "size_px": [w, h],
  "colour": "#rrggbb"}, the locust's hanging pods side by side from one stalk at the top, flat strips with their
  seeds' bulges, darker at the rims, alpha outside (an 8-bit sRGB RGBA map).

Beside each PNG, <material>_<map>.recipe.json records what made it: the formula, the mask recipe where there is one,
each source's file name and SHA-256 as found on disk, and the SHA-256 of all of that together. A run rewrites both
files only when that hash differs from the stamp or the PNG is missing, so an unchanged recipe is a no-op and a
changed recipe, source or formula regenerates; nothing is edited by hand. Derived files live under Art/Materials/,
beside the .mat they feed, never under Art/ThirdParty/: they are not third-party, and fetch.py --verify keeps its
"nothing unlisted under ThirdParty" check without an exception. Exit code 1 on any problem.
"""
import argparse
import hashlib
import json
import math
import os
import re
import sys

from PIL import Image, ImageDraw

import fetch  # the manifest, its file paths and the repo root; same folder

RECIPES = os.path.join("assets-src", "materials")
MATERIALS = os.path.join("unity", "Assets", "_Project", "Art", "Materials")
FORMULA = "R metallic, G occlusion, B 0, A smoothness; 8-bit linear at the sources' size"
BASE_FORMULA = "RGB diffuse, A opacity; 8-bit sRGB at the sources' size"
GROOVE_FORMULA = ("RGB diffuse x (1 + (tint - 1) x weight) in linear light, the weight 0 where the cavity map reads "
                  "from or more and 1 where it reads to or less, linear between; 8-bit sRGB at the sources' size")
PAINT_FORMULA = ("pods side by side from one stalk at the top, each a flat strip of rounded ends with its seeds' "
                 "bulges, the rim at 0.7 of the colour; 4x supersampled, alpha clear outside; 8-bit sRGB RGBA")
CHANNELS = ("metallic", "occlusion", "smoothness")  # R, G, A
MAP_EXPR = re.compile(r"^(1 - )?([a-z]+)$")
HEX = re.compile(r"^#[0-9a-f]{6}$")
PAINT_SUPERSAMPLE = 4
POD_SEEDS = 6               # bulges along a pod (the sheet's 5-10 cm pods hold 4-10 seeds, published)
POD_WIDTH = 0.36            # of the pod's slot across the image
POD_TOP, POD_BOTTOM = 0.1, 0.97   # of the image's height: the stalk above, the pods' tips below
POD_RIM = 0.7               # the rim's share of the colour, and the inner face's share of the pod's width


class DeriveError(Exception):
    """A recipe, source or output problem; the message names it."""


def load_recipe(root, material):
    path = os.path.join(root, RECIPES, material + ".json")
    if not os.path.isfile(path):
        raise DeriveError(f"{material}: no recipe at {fetch.relpath(root, path)}")
    with open(path, encoding="utf-8") as f:
        recipe = json.load(f)
    if recipe.get("schema") != 1:
        raise DeriveError(f"{material}: recipe schema {recipe.get('schema')!r}; derive.py reads schema 1")
    return recipe


def set_maps(root, manifest, texture_set, material):
    """Map name -> path of the pinned file of the manifest entry <source>/<id>."""
    for entry in manifest["entries"]:
        if f"{entry['source']}/{entry['id']}" == texture_set:
            return {f["map"]: fetch.file_path(root, entry, f) for f in entry["files"]}
    raise DeriveError(f"{material}: texture set {texture_set!r} is not in {fetch.MANIFEST}")


def parse_channel(spec, what):
    """(invert, map name) for a map expression, or None for a constant in [0, 1]."""
    if isinstance(spec, (int, float)) and not isinstance(spec, bool):
        if not 0 <= spec <= 1:
            raise DeriveError(f"{what}: constant {spec} is outside [0, 1]")
        return None
    match = MAP_EXPR.match(spec) if isinstance(spec, str) else None
    if not match:
        raise DeriveError(f"{what}: {spec!r} is neither a constant in [0, 1] nor '<map>' or '1 - <map>'")
    return bool(match.group(1)), match.group(2)


def grey(image):
    """The image as 8-bit L. A 16-bit map (Poly Haven's roughness and AO) is scaled and rounded, not clipped."""
    if image.mode.startswith("I;16"):
        return image.convert("I").point(lambda v: v * (255 / 65535) + 0.5).convert("L")
    return image.convert("L")


def channel(spec, sources, size):
    """The 8-bit L image of one channel: a constant, or a loaded source, inverted on request."""
    parsed = parse_channel(spec, "channel")
    if parsed is None:
        return Image.new("L", size, round(spec * 255))
    invert, name = parsed
    image = sources[name]
    return image.point(lambda v: 255 - v) if invert else image


def derive(root, manifest, material):
    """Derives the material's mask when its recipe asks for one, and its base map when the recipe is alpha-clipped,
    each only when its stamp is stale. Returns the report lines."""
    recipe = load_recipe(root, material)
    if ("paint" in recipe) == ("texture_set" in recipe):
        raise DeriveError(f"{material}: give a \"texture_set\" or a \"paint\", not both or neither")
    if "groove" in recipe and "alpha_clip" in recipe:
        raise DeriveError(f"{material}: a \"groove\" tints an opaque bark; it takes no \"alpha_clip\"")
    lines = []
    if recipe.get("mask") is not None:
        lines.append(derive_mask(root, manifest, material, recipe))
    if "paint" in recipe:
        lines.append(derive_paint(root, material, recipe))
    elif "groove" in recipe:
        lines.append(derive_groove(root, manifest, material, recipe))
    elif "alpha_clip" in recipe:
        lines.append(derive_base(root, manifest, material, recipe))
    return "\n".join(lines) if lines else f"{material}: no mask in the recipe, nothing to derive"


def derive_mask(root, manifest, material, recipe):
    """The packed mask of a recipe with a "mask" object: metallic R, occlusion G, smoothness A from its sources."""
    mask = recipe["mask"]
    if not isinstance(mask, dict) or set(mask) != set(CHANNELS):
        raise DeriveError(f"{material}: 'mask' must hold exactly {', '.join(CHANNELS)}")
    texture_set = recipe["texture_set"]
    maps = set_maps(root, manifest, texture_set, material)
    used = set()
    for name, spec in mask.items():
        parsed = parse_channel(spec, f"{material}: mask.{name}")
        if parsed is not None:
            if parsed[1] not in maps:
                raise DeriveError(f"{material}: mask.{name}: {texture_set} has no {parsed[1]} map "
                                  f"(it has {', '.join(sorted(maps))})")
            used.add(parsed[1])
    for name in sorted(used):
        if not os.path.isfile(maps[name]):
            raise DeriveError(f"{fetch.relpath(root, maps[name])}: missing; run fetch.py")
    stamp = {"material": material, "texture_set": texture_set, "formula": FORMULA, "mask": mask,
             "sources": {name: {"name": os.path.basename(maps[name]), "sha256": fetch.sha256(maps[name])}
                         for name in sorted(used)}}
    stamp["inputs_sha256"] = _inputs_sha256(stamp)

    png_path, stamp_path = _outputs(root, material, "mask")
    if _up_to_date(png_path, stamp_path, stamp):
        return f"{fetch.relpath(root, png_path)}: up to date"

    sources = {name: grey(Image.open(maps[name])) for name in sorted(used)}
    sizes = {image.size for image in sources.values()}
    if len(sizes) > 1:
        raise DeriveError(f"{material}: the sources differ in size: "
                          + ", ".join(f"{n} {s[0]}x{s[1]}" for n, s in sorted((n, i.size) for n, i in sources.items())))
    size = sizes.pop() if sizes else (1, 1)
    r, g, a = (channel(mask[name], sources, size) for name in CHANNELS)
    _write(png_path, stamp_path, Image.merge("RGBA", (r, g, Image.new("L", size, 0), a)), stamp)
    return (f"{fetch.relpath(root, png_path)}: derived {size[0]}x{size[1]} from {texture_set} "
            f"({', '.join(sorted(used)) or 'constants only'}); recipe recorded in {os.path.basename(stamp_path)}")


def derive_base(root, manifest, material, recipe):
    """The base map of an alpha-clipped recipe: the diffuse's RGB with the set's separate opacity map as alpha."""
    texture_set = recipe["texture_set"]
    maps = set_maps(root, manifest, texture_set, material)
    if "opacity" not in maps:
        return f"{material}: {texture_set} has no separate opacity map, the diffuse's own alpha clips"
    for name in ("diffuse", "opacity"):
        if not os.path.isfile(maps[name]):
            raise DeriveError(f"{fetch.relpath(root, maps[name])}: missing; run fetch.py")
    stamp = {"material": material, "texture_set": texture_set, "formula": BASE_FORMULA,
             "sources": {name: {"name": os.path.basename(maps[name]), "sha256": fetch.sha256(maps[name])}
                         for name in ("diffuse", "opacity")}}
    stamp["inputs_sha256"] = _inputs_sha256(stamp)

    png_path, stamp_path = _outputs(root, material, "base")
    if _up_to_date(png_path, stamp_path, stamp):
        return f"{fetch.relpath(root, png_path)}: up to date"

    diffuse = Image.open(maps["diffuse"]).convert("RGB")
    opacity = grey(Image.open(maps["opacity"]))
    if diffuse.size != opacity.size:
        raise DeriveError(f"{material}: the sources differ in size: diffuse {diffuse.size[0]}x{diffuse.size[1]}, "
                          f"opacity {opacity.size[0]}x{opacity.size[1]}")
    _write(png_path, stamp_path, Image.merge("RGBA", (*diffuse.split(), opacity)), stamp)
    return (f"{fetch.relpath(root, png_path)}: derived {diffuse.size[0]}x{diffuse.size[1]} from {texture_set} "
            f"(diffuse, opacity); recipe recorded in {os.path.basename(stamp_path)}")


def derive_groove(root, manifest, material, recipe):
    """The base map of a bark with a "groove": the diffuse, its grooves tinted by the cavity map (module docstring)."""
    groove = recipe["groove"]
    if not isinstance(groove, dict) or set(groove) != {"cavity", "from", "to", "tint"}:
        raise DeriveError(f"{material}: 'groove' must hold exactly cavity, from, to, tint")
    tint = groove["tint"]
    if not (isinstance(tint, list) and len(tint) == 3 and all(_is_num(c) and c > 0 for c in tint)):
        raise DeriveError(f"{material}: groove.tint must be three positive linear factors, got {tint!r}")
    if not (_is_num(groove["from"]) and _is_num(groove["to"]) and 0 <= groove["to"] < groove["from"] <= 255):
        raise DeriveError(f"{material}: groove needs 0 <= to < from <= 255 on the 8-bit cavity map")
    texture_set = recipe["texture_set"]
    maps = set_maps(root, manifest, texture_set, material)
    if groove["cavity"] not in maps:
        raise DeriveError(f"{material}: groove.cavity: {texture_set} has no {groove['cavity']} map "
                          f"(it has {', '.join(sorted(maps))})")
    used = ("diffuse", groove["cavity"])
    for name in used:
        if not os.path.isfile(maps[name]):
            raise DeriveError(f"{fetch.relpath(root, maps[name])}: missing; run fetch.py")
    stamp = {"material": material, "texture_set": texture_set, "formula": GROOVE_FORMULA, "groove": groove,
             "sources": {name: {"name": os.path.basename(maps[name]), "sha256": fetch.sha256(maps[name])}
                         for name in used}}
    stamp["inputs_sha256"] = _inputs_sha256(stamp)

    png_path, stamp_path = _outputs(root, material, "base")
    if _up_to_date(png_path, stamp_path, stamp):
        return f"{fetch.relpath(root, png_path)}: up to date"

    diffuse = Image.open(maps["diffuse"]).convert("RGB")
    cavity = grey(Image.open(maps[groove["cavity"]]))
    if diffuse.size != cavity.size:
        raise DeriveError(f"{material}: the sources differ in size: diffuse {diffuse.size[0]}x{diffuse.size[1]}, "
                          f"{groove['cavity']} {cavity.size[0]}x{cavity.size[1]}")
    weights = [min(1.0, max(0.0, (groove["from"] - level) / (groove["from"] - groove["to"]))) for level in range(256)]
    levels = cavity.tobytes()
    bands = []
    for band, factor in zip(diffuse.split(), tint):  # one table per channel, indexed by cavity level and texel
        table = bytes(_encode(_decode(d) * (1 + (factor - 1) * w)) for w in weights for d in range(256))
        bands.append(Image.frombytes("L", band.size, bytes(table[a << 8 | d] for a, d in zip(levels, band.tobytes()))))
    _write(png_path, stamp_path, Image.merge("RGB", bands), stamp)
    return (f"{fetch.relpath(root, png_path)}: derived {diffuse.size[0]}x{diffuse.size[1]} from {texture_set} "
            f"(diffuse, {groove['cavity']}); recipe recorded in {os.path.basename(stamp_path)}")


def derive_paint(root, material, recipe):
    """The painted base map of a recipe with a "paint" (module docstring): the locust's pod strip."""
    paint = recipe["paint"]
    if not isinstance(paint, dict) or set(paint) != {"pods", "size_px", "colour"}:
        raise DeriveError(f"{material}: 'paint' must hold exactly pods, size_px, colour")
    size = paint["size_px"]
    if not (isinstance(paint["pods"], int) and paint["pods"] >= 1 and isinstance(size, list) and len(size) == 2
            and all(isinstance(s, int) and 8 <= s <= 2048 for s in size)
            and isinstance(paint["colour"], str) and HEX.match(paint["colour"])):
        raise DeriveError(f"{material}: paint needs pods >= 1, size_px [w, h] of 8-2048 px and colour '#rrggbb'")
    stamp = {"material": material, "formula": PAINT_FORMULA, "paint": paint, "sources": {}}
    stamp["inputs_sha256"] = _inputs_sha256(stamp)

    png_path, stamp_path = _outputs(root, material, "base")
    if _up_to_date(png_path, stamp_path, stamp):
        return f"{fetch.relpath(root, png_path)}: up to date"
    _write(png_path, stamp_path, _pods(paint["pods"], size, paint["colour"]), stamp)
    return f"{fetch.relpath(root, png_path)}: painted {size[0]}x{size[1]}; recipe recorded in {os.path.basename(stamp_path)}"


def _pods(count, size, colour):
    """`count` pods hanging side by side from a stalk at the top centre, drawn PAINT_SUPERSAMPLE times larger and
    box-filtered down: each a strip whose width swells over its POD_SEEDS seeds and rounds off at both ends, its
    rim in POD_RIM of the colour, the stalk and the pods' tops converging on it."""
    s = PAINT_SUPERSAMPLE
    w, h = size[0] * s, size[1] * s
    base = tuple(int(colour[i:i + 2], 16) for i in (1, 3, 5))
    rim = tuple(round(c * POD_RIM) for c in base)
    image = Image.new("RGBA", (w, h), base + (0,))
    draw = ImageDraw.Draw(image)
    top = POD_TOP * h
    draw.line([(w / 2, 0), (w / 2, top)], fill=rim + (255,), width=max(1, round(0.04 * w)))
    for i in range(count):
        slot = w / count
        tip_x, half = slot * (i + 0.5), POD_WIDTH * slot / 2
        for share, fill in ((1.0, rim), (POD_RIM, base)):
            left, right = [], []
            for k in range(41):
                t = k / 40
                x = w / 2 + (tip_x - w / 2) * t ** 0.5
                y = top + (POD_BOTTOM * h - top) * t
                bulge = 1 + 0.15 * math.sin(POD_SEEDS * math.pi * t) ** 2
                r = share * half * bulge * math.sin(math.pi * min(1.0, t / 0.12, (1 - t) / 0.06) / 2)
                left.append((x - r, y))
                right.append((x + r, y))
            draw.polygon(left + right[::-1], fill=fill + (255,))
    return image.resize(tuple(size), Image.BOX)


def _decode(level):
    """An 8-bit sRGB level as linear light."""
    c = level / 255
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def _encode(value):
    """Linear light as an 8-bit sRGB level, clipped at white."""
    v = min(1.0, max(0.0, value))
    return round(255 * (12.92 * v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055))


def _is_num(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool)


def _outputs(root, material, kind):
    return (os.path.join(root, MATERIALS, f"{material}_{kind}.png"),
            os.path.join(root, MATERIALS, f"{material}_{kind}.recipe.json"))


def _up_to_date(png_path, stamp_path, stamp):
    if not (os.path.isfile(png_path) and os.path.isfile(stamp_path)):
        return False
    with open(stamp_path, encoding="utf-8") as f:
        return json.load(f).get("inputs_sha256") == stamp["inputs_sha256"]


def _write(png_path, stamp_path, image, stamp):
    os.makedirs(os.path.dirname(png_path), exist_ok=True)
    image.save(png_path, "PNG")
    stamp["size"] = list(image.size)
    with open(stamp_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(stamp, indent=2) + "\n")


def _inputs_sha256(stamp):
    """SHA-256 of what the map depends on: the formula, the mask, groove or paint recipe where there is one, and the
    sources' hashes."""
    inputs = {k: stamp[k] for k in ("formula", "mask", "groove", "paint", "sources") if k in stamp}
    return hashlib.sha256(json.dumps(inputs, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()


def all_materials(root):
    folder = os.path.join(root, RECIPES)
    return sorted(name[:-5] for name in os.listdir(folder) if name.endswith(".json"))


def main(argv=None):
    parser = argparse.ArgumentParser(description="Derive the packed mask maps of the material recipes.")
    parser.add_argument("materials", nargs="*", help="recipes to derive, by material name")
    parser.add_argument("--all", action="store_true", help=f"every recipe under {RECIPES}")
    parser.add_argument("--root", default=fetch.REPO_ROOT, help=argparse.SUPPRESS)
    args = parser.parse_args(argv)
    if not (args.materials or args.all):
        parser.error("nothing to do: give material names or --all")
    try:
        manifest = fetch.load_manifest(args.root)
        for material in (args.materials or all_materials(args.root)):
            print(derive(args.root, manifest, material))
    except (DeriveError, fetch.FetchError, OSError, json.JSONDecodeError) as e:
        print(f"ERROR: {e}")
        return 1
    except KeyError as e:
        print(f"ERROR: unexpected recipe or manifest shape: missing key {e}")
        return 1
    print("derive.py: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
