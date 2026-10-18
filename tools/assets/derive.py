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
import os
import re
import sys

from PIL import Image

import fetch  # the manifest, its file paths and the repo root; same folder

RECIPES = os.path.join("assets-src", "materials")
MATERIALS = os.path.join("unity", "Assets", "_Project", "Art", "Materials")
FORMULA = "R metallic, G occlusion, B 0, A smoothness; 8-bit linear at the sources' size"
BASE_FORMULA = "RGB diffuse, A opacity; 8-bit sRGB at the sources' size"
CHANNELS = ("metallic", "occlusion", "smoothness")  # R, G, A
MAP_EXPR = re.compile(r"^(1 - )?([a-z]+)$")


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
    lines = []
    if recipe.get("mask") is not None:
        lines.append(derive_mask(root, manifest, material, recipe))
    if "alpha_clip" in recipe:
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
    """SHA-256 of what the map depends on: the formula, the mask recipe where there is one, and the sources' hashes."""
    inputs = {k: stamp[k] for k in ("formula", "mask", "sources") if k in stamp}
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
