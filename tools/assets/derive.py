"""Derived textures by script (p1-pipeline-hardening, "Derived textures by script"; D-004, AST-07): the packed mask
map of a material recipe and the base map of an alpha-clipped one, or the whole map set of a ground recipe, built
from the pinned sources with the recipe recorded beside them. Pillow only.

    python tools/assets/derive.py <material>...     derive these recipes (assets-src/materials/<material>.json)
    python tools/assets/derive.py --all             every recipe under assets-src/materials/

A recipe with a "mask" object gets unity/Assets/_Project/Art/Materials/<material>_mask.png, an 8-bit linear RGBA map
that URP/Lit reads twice: _MetallicGlossMap takes metallic from R and smoothness from A, _OcclusionMap takes occlusion
from G; B is unused and 0. The object holds exactly "metallic", "occlusion" and "smoothness", each a constant in
[0, 1] or a map of the recipe's texture set by its manifest map name ("ao", "roughness", ...), inverted as
"1 - roughness". The mask has the sources' size; 16-bit sources are scaled to 8 bits, never clipped. In the mask of
an alpha-clipped recipe whose set ships an opacity map, every texel the opacity clips takes each channel's mean over
the texels it keeps, so the mips of a minified card average its leaves' values, not what lies under the clipped
texels (p1-v01-locust task 1.2g).

A recipe with "alpha_clip" (p1-v13-shrub, "Alpha-clipped two-sided recipes") whose texture set ships its opacity as
a separate map (ambientCG's atlases) gets <material>_base.png: the diffuse's RGB with the opacity map as alpha, an
8-bit sRGB RGBA map the importer assigns as the base map in place of the diffuse, so URP/Lit clips by its alpha. A
set without an "opacity" map needs nothing: the diffuse's own alpha clips.

A far-card recipe {"schema": 1, "far_card_of": <recipe>} (p1-v01-locust task 1.2j) is that leaf recipe for far
cards drawn as two faces with normals of their own: the importer builds it from the leaf recipe and its derived maps,
one-sided and without the normal map, so nothing is derived for it.

Two more base maps (p1-v01-locust, design decision 5), assigned the same way:
- a "groove" object tints a bark's grooves: {"cavity": <map>, "from": <level>, "to": <level>, "tint": [r, g, b]}.
  The cavity weight runs linearly from 0 where the cavity map (8-bit, AO dark in the grooves) reads "from" or more
  to 1 where it reads "to" or less, and each diffuse texel, in linear light, is scaled by 1 + (tint - 1) * weight:
  the recipe's material tint holds the ridge colour and the groove tint the grooves' own on top of it (an 8-bit sRGB
  RGB map; a channel past white clips);
- a "paint" object, in place of a texture set, paints the base map by script: {"pods": <count>, "size_px": [w, h],
  "colour": "#rrggbb"}, the locust's hanging pods side by side from one stalk at the top, flat strips with their
  seeds' bulges, darker at the rims, alpha outside (an 8-bit sRGB RGBA map).

A recipe of kind "ground" (p1-s04-belt-straw, "Ground recipes with a detail layer") names a soil set and a cover set,
a seeded patch description and a target mean albedo, and gets four maps for one patch of size_m x size_m at
output_px (2K at most, AST-08): <material>_albedo.png (sRGB), <material>_normal.png (GL tangent space),
<material>_mask.png (the packed mask above: metallic 0, the sets' AO, 1 - roughness) and
<material>_detail_albedo.png, the detail set's diffuse as grey at its own size with its linear mean moved to 0.5,
so URP/Lit's "detail albedo x2" layer adds grain without shifting the base's mean or hue. The cover is tiled at its
tile_size_m and again at tile_size_m x phi, averaged, so no period repeats inside the patch; it is stretched 1 + bias
along the lodging azimuth and rotated to it (the cover normals turn with it). The bare soil patches, the green
strips and the tussock relief are the asset's own: patches.bare.params (and patches.green.params, the same file)
names the asset's params file, and tools/blender/lib/patches.py and lib/mat.py lay them out from it, the same
layout the generator steps the mesh down along, grows its blades over and shapes its top by, so the map and the
geometry never disagree (review-physics PR5). A floor (inside a step outline) is soil to its wall: FLOOR_EDGE of
full at the outline, full at the outline shrunk by the feather, the ramp being each patch's core (its outline shrunk
by half the feather) filled and blurred over half the feather each side, and nothing outside the outline; the bare
share is the outlines'. The floor is broken by the cover's straws (strewn), so straws lie over its soil, more of
them at its wall, and FLOOR_STRAWS of it all over (H 12, H 14: a floor reads as soil with straws on it, not a hole).
Outside a floor the mat thins over LIP_M: the soil shows between its straws, LIP_LEVEL at the outline falling to
nothing, so a step's lip is ragged; never on a floor, whose soil is the bare mask's alone, and never over a path's
mat bridges (lib/patches.py), where the mat is full depth and so shows as the mat (R7). Elsewhere the gaps between
the cover's straws (gap_share) show the mat's dark depth: the cover recoloured to the cover's optional gap_albedo
(the litter in the straws' shadow, H 12-21), or the soil when it names none. The green strips (lib/mat.py) are filled and
blurred over GREEN_FEATHER_M; the cover under them is tinted to the strip colour and its straws are strewn over
them from GREEN_LEVEL, so a strip is regrowth with straws lying over it. The straws and the strips are
darkened by up to RELIEF_TONE, and the occlusion by up to RELIEF_AO, in the relief's hollows (G 1-12: dark hollows
between pale humps). The soil takes the gain to the soil's optional albedo_target. The straw layer takes the
per-channel gain that lands the patch's linear mean colour on albedo_target with the soil, the gaps and the strips at their
own colours, then a global pass corrects clipping; the achieved mean, the shares and the gains are recorded in the
stamp. The azimuth is degrees from +u towards +v of the map, which is +X towards +Z of the asset when its UVs are
metres.

A recipe of kind "palette" (p1-s04-belt-straw, the straw instance kit) has no texture set: its "colours" object names
sRGB colours, each a flat "#rrggbb" or a list of stops from the bottom of the map to the top, and the build writes
<material>_albedo.png, one column PALETTE_BLOCK_PX wide and PALETTE_HEIGHT_PX tall per colour from left to right in
the object's order, the stops spread evenly up the column and interpolated in linear light (so a column's mean is
its stops' mean), the columns padded with the last one to a power of two (Unity rescales other widths and would
blur them together), so a generator tints a face by UV'ing it into a column at a height (lib/straw.py's PALETTE and
palette_uv). A tinted material without any texture would read to the review capture as an un-smudged LFS pointer, and
a flat map is what a plain tint is.

Beside the PNGs, <material>_mask.recipe.json (masks), <material>_base.recipe.json (alpha, groove and paint base
maps) or <material>.recipe.json (ground, palette) records what made them: the formula, the recipe where there is
one, each source's file name and SHA-256 as found on disk, and the SHA-256 of all of that together
(plus the Pillow version for ground maps, whose resampling it fixes, and for ground and palette maps the value of
every named constant their formula reads, GROUND_CONSTANTS and PALETTE_CONSTANTS, under "constants"; for a ground
map also the bare layout's, the strips' and the relief's inputs and the layout modules' constants and versions,
under "layout"). A run rewrites
the files only when that hash differs from the stamp or a PNG is missing, so an unchanged recipe is a no-op and a
changed recipe, source, constant or formula regenerates; a change to the code itself bumps the formula's version.
Nothing is edited by hand. Derived files live under Art/Materials/, beside the .mat they feed, never
under Art/ThirdParty/: they are not third-party, and fetch.py --verify keeps its "nothing unlisted under ThirdParty"
check without an exception. Exit code 1 on any problem.
"""
import argparse
import hashlib
import json
import math
import os
import random
import re
import sys

import PIL
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageMath, ImageOps, ImageStat

import fetch  # the manifest, its file paths and the repo root; same folder

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "blender"))
from lib import mat, patches  # noqa: E402  the layouts and fields the ground_patch generator shares (plain Python)

RECIPES = os.path.join("assets-src", "materials")
MATERIALS = os.path.join("unity", "Assets", "_Project", "Art", "Materials")
FORMULA = "R metallic, G occlusion, B 0, A smoothness; 8-bit linear at the sources' size"
CLIP_FORMULA = FORMULA + "; every texel the opacity map clips takes each channel's mean over the texels it keeps"
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

GROUND_FORMULA = ("ground v6: cover tiled at tile_size_m and at tile_size_m*phi, averaged, stretched 1+bias along the "
                  "lodging azimuth and rotated to it; soil tiled at its tile_size_m, gained to its albedo_target when "
                  "given; bare = the asset's patch layout (lib/patches.py): the outlines filled times FLOOR_EDGE + "
                  "(1 - FLOOR_EDGE) * the ramp of each core filled and blurred with sigma patches.FEATHER_M / 6, so "
                  "255 on the outline shrunk by the feather, FLOOR_EDGE at the outline and 0 outside it, mapped "
                  "u = x / size_m + 0.5, v = z / size_m + 0.5 with v up the image; green = the asset's strips "
                  "(lib/mat.py) filled and blurred with sigma mat.GREEN_FEATHER_M / 2; strewn masks clamp(2 m - c), c "
                  "the cover luminance high-passed at STRAW_M, blended with LOBE_M value noise and stretched to 0-1: "
                  "the bare mask (noise BARE_LOBES), times the floors' strewn straws (noise GAP_LOBES, m by "
                  "LEVEL_STEPS bisection steps so that they cover FLOOR_STRAWS of the bare mask's soil), the lip "
                  "(m = LIP_LEVEL * min(1, 2 * the outlines blurred with sigma LIP_M / 3) outside the outlines and 0 "
                  "inside them, noise BARE_LOBES), the gaps "
                  "between straws (m = sqrt(gap_share / 2), noise GAP_LOBES) and the strips shown (m = GREEN_LEVEL * green, noise GAP_LOBES); relief = the asset's "
                  "relief (lib/mat.py) sampled on RELIEF_SAMPLES^2 and resized bicubic, h01 in 0-1; albedo = the "
                  "cover times 1 - RELIEF_TONE (1 - h01) with the straw gain for albedo_target, the strips shown "
                  "tinted to the green tint, the gaps the cover gained to gap_albedo (the soil without one), the soil "
                  "under bare and the lip, then a global gain against clipping; normal composited the same with the "
                  "cover normals rotated by the azimuth; mask R 0, G AO times 1 - RELIEF_AO (1 - h01), B 0, "
                  "A 1 - roughness; detail albedo = the detail set's diffuse as grey at its size with a linear mean "
                  "of 0.5; 8-bit sRGB albedos")
GROUND_MAPS = ("albedo", "normal", "mask", "detail_albedo")
PALETTE_FORMULA = ("palette v2: one column PALETTE_BLOCK_PX x PALETTE_HEIGHT_PX per colour, left to right in the "
                   "recipe's order, its stops spread evenly from the bottom to the top and interpolated in linear light, "
                   "padded with the last column to a power of two of columns; 8-bit sRGB")
PALETTE_BLOCK_PX = 16  # a block survives four mip levels before its colour bleeds into its neighbour's
PALETTE_HEIGHT_PX = 64  # a column's gradient: enough rows for a smooth ramp, a power of two
MAX_OUTPUT_PX = 2048  # AST-08
PHI = 1.618033988749895
LOBE_M = 0.4           # the value-noise cell that lobes a bare patch's outline (assumed from H 9-14)
STRAW_M = 0.05         # the high-pass that keeps a cover's straws and gaps and drops its tile-sized shading
GAP_LOBES = 0.0        # the gaps between straws follow the cover's own straws, a little thicker and thinner by area
BARE_LOBES = 0.5       # the share of value noise in a bare patch's strewn feather (assumed)
FLOOR_EDGE = 0.55      # a floor's soil level at its wall before the straws are strewn over it (assumed, H 9-14)
FLOOR_STRAWS = 0.2     # the cover's straws lie over this share of a floor's soil, all over it (assumed, H 12, H 14)
LEVEL_STEPS = 16       # bisection steps of the floor straws' level: 255 / 2^16, far under one 8-bit level
LIP_M = 0.4            # the mat thins over this outside a floor's outline (assumed, H 9-14: the ragged lip)
LIP_LEVEL = 0.6        # the lip's level at the outline: more than half the mat's cut edge shows soil (assumed)
RELIEF_TONE = 0.45     # the straws darken by up to this in the relief's hollows (assumed, G 1-12)
GREEN_LEVEL = 0.6      # a strip's level before the straws are strewn over it: a third shows straws (assumed, H 19-21)
RELIEF_AO = 0.4        # the occlusion falls by up to this in the hollows (assumed, G 1-12)
RELIEF_SAMPLES = 256   # the relief's samples per side before the bicubic resize: 4.7 cm at 12 m
# The named constants the ground and palette formulas read: their values go into the stamp's hash (review R1). The
# bare feather and the patch layout's own constants are lib/patches.py's, the strips' and the relief's lib/mat.py's,
# hashed under "layout".
GROUND_CONSTANTS = ("MAX_OUTPUT_PX", "PHI", "LOBE_M", "STRAW_M", "GAP_LOBES", "BARE_LOBES", "FLOOR_EDGE",
                    "FLOOR_STRAWS", "LEVEL_STEPS", "LIP_M", "LIP_LEVEL", "RELIEF_TONE", "RELIEF_AO", "RELIEF_SAMPLES",
                    "GREEN_LEVEL")
PALETTE_CONSTANTS = ("PALETTE_BLOCK_PX", "PALETTE_HEIGHT_PX")
LUMA = (0.2126, 0.7152, 0.0722)
SRGB_TO_LINEAR = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in (v / 255 for v in range(256))]


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
    """Derives the material's maps when its recipe asks for them, each only when its stamp is stale: a ground or
    palette recipe's set; a tiled set's mask when the recipe has one and its base map when it is alpha-clipped,
    groove-tinted or painted. Returns the report lines."""
    recipe = load_recipe(root, material)
    if "far_card_of" in recipe:
        return far_card(root, material, recipe)
    kind = recipe.get("kind")
    if kind == "ground":
        return derive_ground(root, manifest, material, recipe)
    if kind == "palette":
        return derive_palette(root, material, recipe)
    if kind is not None:
        raise DeriveError(f"{material}: unknown recipe kind {kind!r} (omit it for a tiled set, or 'ground' or 'palette')")
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


def far_card(root, material, recipe):
    """A far-card recipe {"schema": 1, "far_card_of": <recipe>} (p1-v01-locust task 1.2j) reads its leaf recipe's
    derived maps, so it derives nothing."""
    leaf = recipe["far_card_of"]
    if set(recipe) != {"schema", "far_card_of"} or not isinstance(leaf, str):
        raise DeriveError(f"{material}: a \"far_card_of\" recipe names one leaf recipe and holds nothing else")
    if "far_card_of" in load_recipe(root, leaf):
        raise DeriveError(f"{material}: {leaf} is itself a far-card recipe; name the leaf recipe")
    return f"{material}: far cards of {leaf}, whose derived maps it reads; nothing to derive"


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
    clipped = "alpha_clip" in recipe and "opacity" in maps  # the base map clips by this opacity (derive_base)
    if clipped:
        used.add("opacity")
    for name in sorted(used):
        if not os.path.isfile(maps[name]):
            raise DeriveError(f"{fetch.relpath(root, maps[name])}: missing; run fetch.py")
    stamp = {"material": material, "texture_set": texture_set, "formula": CLIP_FORMULA if clipped else FORMULA,
             "mask": mask, "sources": {name: {"name": os.path.basename(maps[name]), "sha256": fetch.sha256(maps[name])}
                                       for name in sorted(used)}}
    keys = ("formula", "mask", "sources")
    if clipped:
        stamp["alpha_clip"] = recipe["alpha_clip"]
        keys += ("alpha_clip",)
    stamp["inputs_sha256"] = _inputs_sha256(stamp, keys)

    png_path = os.path.join(root, MATERIALS, f"{material}_mask.png")
    stamp_path = os.path.join(root, MATERIALS, f"{material}_mask.recipe.json")
    if _up_to_date([png_path], stamp_path, stamp["inputs_sha256"]):
        return f"{fetch.relpath(root, png_path)}: up to date"

    sources = {name: grey(Image.open(maps[name])) for name in sorted(used)}
    sizes = {image.size for image in sources.values()}
    if len(sizes) > 1:
        raise DeriveError(f"{material}: the sources differ in size: "
                          + ", ".join(f"{n} {s[0]}x{s[1]}" for n, s in sorted((n, i.size) for n, i in sources.items())))
    size = sizes.pop() if sizes else (1, 1)
    r, g, a = (channel(mask[name], sources, size) for name in CHANNELS)
    if clipped:
        r, g, a = _fill_clipped((r, g, a), sources["opacity"], recipe["alpha_clip"])
    os.makedirs(os.path.dirname(png_path), exist_ok=True)
    Image.merge("RGBA", (r, g, Image.new("L", size, 0), a)).save(png_path, "PNG")
    stamp["size"] = list(size)
    _write_stamp(stamp_path, stamp)
    return (f"{fetch.relpath(root, png_path)}: derived {size[0]}x{size[1]} from {texture_set} "
            f"({', '.join(sorted(used)) or 'constants only'}); recipe recorded in {os.path.basename(stamp_path)}")


def _fill_clipped(images, opacity, cutoff):
    """The 8-bit L `images` with every texel the alpha test clips (opacity under `cutoff`: the shader drops alpha <
    cutoff) set to the image's mean over the texels it keeps (p1-v01-locust task 1.2g). Those texels never show at
    full resolution, but every mip averages them in: LeafSet002's AO reads 0.82 under its clipped texels against 0.97
    under its leaves, so a minified leaf card took a lower occlusion the farther it was, and its ambient light with
    it. Unchanged when the map keeps nothing."""
    keep = opacity.point(lambda v: 255 if v >= math.ceil(cutoff * 255) else 0)
    if not keep.getbbox():
        return images
    return tuple(Image.composite(image, Image.new("L", image.size, round(ImageStat.Stat(image, keep).mean[0])), keep)
                 for image in images)


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
    stamp["inputs_sha256"] = _inputs_sha256(stamp, ("formula", "sources"))

    png_path = os.path.join(root, MATERIALS, f"{material}_base.png")
    stamp_path = os.path.join(root, MATERIALS, f"{material}_base.recipe.json")
    if _up_to_date([png_path], stamp_path, stamp["inputs_sha256"]):
        return f"{fetch.relpath(root, png_path)}: up to date"

    diffuse = Image.open(maps["diffuse"]).convert("RGB")
    opacity = grey(Image.open(maps["opacity"]))
    if diffuse.size != opacity.size:
        raise DeriveError(f"{material}: the sources differ in size: diffuse {diffuse.size[0]}x{diffuse.size[1]}, "
                          f"opacity {opacity.size[0]}x{opacity.size[1]}")
    os.makedirs(os.path.dirname(png_path), exist_ok=True)
    Image.merge("RGBA", (*diffuse.split(), opacity)).save(png_path, "PNG")
    stamp["size"] = list(diffuse.size)
    _write_stamp(stamp_path, stamp)
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
    stamp["inputs_sha256"] = _inputs_sha256(stamp, ("formula", "groove", "sources"))

    png_path, stamp_path = _outputs(root, material, "base")
    if _up_to_date([png_path], stamp_path, stamp["inputs_sha256"]):
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
    stamp["inputs_sha256"] = _inputs_sha256(stamp, ("formula", "paint", "sources"))

    png_path, stamp_path = _outputs(root, material, "base")
    if _up_to_date([png_path], stamp_path, stamp["inputs_sha256"]):
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


def _write(png_path, stamp_path, image, stamp):
    os.makedirs(os.path.dirname(png_path), exist_ok=True)
    image.save(png_path, "PNG")
    stamp["size"] = list(image.size)
    _write_stamp(stamp_path, stamp)


def _constants(names):
    """The current value of each named module constant, read at call time (a test that sets one sees it here)."""
    return {name: list(value) if isinstance(value, tuple) else value
            for name, value in ((n, globals()[n]) for n in names)}


def _inputs_sha256(stamp, keys):
    """SHA-256 of what the maps depend on."""
    inputs = {k: stamp[k] for k in keys}
    return hashlib.sha256(json.dumps(inputs, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()


def _up_to_date(outputs, stamp_path, inputs_sha256):
    if not all(os.path.isfile(p) for p in outputs) or not os.path.isfile(stamp_path):
        return False
    with open(stamp_path, encoding="utf-8") as f:
        return json.load(f).get("inputs_sha256") == inputs_sha256


def _write_stamp(stamp_path, stamp):
    with open(stamp_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(stamp, indent=2) + "\n")


# --- ground recipes -----------------------------------------------------------------------------------------------

def _number(obj, key, what, lo=None, hi=None, integer=False):
    value = obj.get(key)
    ok = isinstance(value, (int, float)) and not isinstance(value, bool) and (not integer or isinstance(value, int))
    if not ok or (lo is not None and value < lo) or (hi is not None and value > hi):
        span = f" in [{lo}, {hi}]" if lo is not None and hi is not None else f" >= {lo}" if lo is not None else ""
        raise DeriveError(f"{what}.{key}: {value!r} must be {'an integer' if integer else 'a number'}{span}")
    return value


def _range(obj, key, what):
    value = obj.get(key)
    if not (isinstance(value, list) and len(value) == 2 and all(isinstance(v, (int, float)) for v in value)
            and 0 < value[0] <= value[1]):
        raise DeriveError(f"{what}.{key}: {value!r} must be [min, max] in metres with 0 < min <= max")
    return value


def _object(obj, key, what):
    value = obj.get(key)
    if not isinstance(value, dict):
        raise DeriveError(f"{what}: missing object \"{key}\"")
    return value


def _colour(obj, key, what):
    value = obj.get(key)
    match = re.fullmatch(r"#([0-9a-fA-F]{6})", value) if isinstance(value, str) else None
    if not match:
        raise DeriveError(f"{what}.{key}: {value!r} is not an sRGB colour like #7a7055")
    return [SRGB_TO_LINEAR[int(match.group(1)[i:i + 2], 16)] for i in (0, 2, 4)]


def _set_ref(obj, key, what):
    layer = _object(obj, key, what)
    if not isinstance(layer.get("texture_set"), str):
        raise DeriveError(f"{what}.{key}.texture_set: missing")
    _number(layer, "tile_size_m", f"{what}.{key}", lo=0.05, hi=100)
    return layer


def bare_layout_inputs(root, bare, material, size_m):
    """The bare patch layout's inputs (lib/patches.py) from the asset params file that patches.bare.params names,
    whose size_m must be the recipe's: the map spans the asset. Returns (path, inputs, params)."""
    what = f"{material}.patches.bare.params"
    path = bare.get("params")
    if not isinstance(path, str) or not os.path.isfile(os.path.join(root, path)):
        raise DeriveError(f"{what}: {path!r} is not an asset params file (the bare patches are the asset's layout)")
    with open(os.path.join(root, path), encoding="utf-8") as f:
        params = json.load(f)
    try:
        inputs = patches.inputs(params)
    except (KeyError, TypeError) as e:
        raise DeriveError(f"{what}: {path} lacks the layout key {e} (seed, size_m, bare.share, bare.size_m, "
                          "bare.outline_vertices, bare.aspect, bare.path_patches, lodging)")
    if inputs["size_m"] != size_m:
        raise DeriveError(f"{what}: {path} has size_m {inputs['size_m']}, the recipe {size_m}; the map spans the asset")
    if not 0 <= inputs["share"] < 1:
        raise DeriveError(f"{what}: bare.share {inputs['share']} must lie in [0, 1)")
    return path, inputs, params


def ground_recipe(root, recipe, material):
    """Validates a ground recipe and returns its numbers as plain values (shares, sizes, colours in linear RGB)."""
    _number(recipe, "seed", material, integer=True)
    size_m = _number(recipe, "size_m", material, lo=1, hi=200)
    output_px = _number(recipe, "output_px", material, lo=16, hi=MAX_OUTPUT_PX, integer=True)
    soil, cover, detail = (_set_ref(recipe, k, material) for k in ("soil", "cover", "detail"))
    patch_spec = _object(recipe, "patches", material)
    bare, green, lodging = (_object(patch_spec, k, f"{material}.patches") for k in ("bare", "green", "lodging"))
    layout_params, layout_inputs, params = bare_layout_inputs(root, bare, material, size_m)
    if green.get("params") != layout_params:
        raise DeriveError(f"{material}.patches.green.params: {green.get('params')!r} must name the bare patches' params "
                          f"file {layout_params!r} (the strips are the asset's, measured outside its bare patches)")
    try:
        green_inputs, relief_inputs = mat.green_inputs(params), mat.relief_inputs(params)
    except (KeyError, TypeError) as e:
        raise DeriveError(f"{material}.patches.green.params: {layout_params} lacks the key {e} (green.share, "
                          "green.width_m, green.length_m, cell_m, relief_m)")
    green_share = green_inputs["share"]
    if not 0 <= green_share <= 1:
        raise DeriveError(f"{material}.patches.green.params: green.share {green_share} must lie in [0, 1]")
    if layout_inputs["share"] + green_share > 1:
        raise DeriveError(f"{material}.patches: the bare share {layout_inputs['share']} + green.share {green_share} "
                          "exceeds 1")
    return {
        "seed": recipe["seed"], "size_m": size_m, "output_px": output_px,
        "soil": soil, "cover": cover, "detail": detail,
        "soil_target": _colour(soil, "albedo_target", f"{material}.soil") if "albedo_target" in soil else None,
        "gap_share": _number(cover, "gap_share", f"{material}.cover", lo=0, hi=0.5) if "gap_share" in cover else 0.0,
        "gap_albedo": _colour(cover, "gap_albedo", f"{material}.cover") if "gap_albedo" in cover else None,
        "layout_params": layout_params, "layout": layout_inputs, "green": green_inputs, "relief": relief_inputs,
        "green_tint": _colour(green, "tint", f"{material}.patches.green"),
        "azimuth_deg": _number(lodging, "azimuth_deg", f"{material}.patches.lodging", lo=-360, hi=360),
        "bias": _number(lodging, "bias", f"{material}.patches.lodging", lo=0, hi=1),
        "target": _colour(recipe, "albedo_target", material),
    }


def to_srgb8(linear):
    linear = min(1.0, max(0.0, linear))
    c = linear * 12.92 if linear <= 0.0031308 else 1.055 * linear ** (1 / 2.4) - 0.055
    return round(c * 255)


def linear_means(rgb):
    """Per-channel linear mean of an 8-bit sRGB RGB image."""
    h, n = rgb.histogram(), rgb.size[0] * rgb.size[1]
    return [sum(h[band * 256 + v] * SRGB_TO_LINEAR[v] for v in range(256)) / n for band in range(3)]


def luminance(linear_rgb):
    return sum(w * c for w, c in zip(LUMA, linear_rgb))


def gain_lut(gains):
    """An RGB point table applying a linear gain per channel to an sRGB image."""
    return [to_srgb8(SRGB_TO_LINEAR[v] * g) for g in gains for v in range(256)]


def match_mean(rgb, target, start=(1.0, 1.0, 1.0)):
    """The image with per-channel linear gains (from `start`, corrected four times against clipping) so that its
    linear mean colour lands on `target`. Returns (image, gains)."""
    gains, out = list(start), rgb.point(gain_lut(start))
    for _ in range(4):
        means = linear_means(out)
        gains = [g * (t / m) if m > 0 else g for g, t, m in zip(gains, target, means)]
        out = rgb.point(gain_lut(gains))
    return out, gains


def tiled(image, tile_px, canvas_px, stretch=1.0):
    """The image resampled to a tile of tile_px (times `stretch` along x) and repeated over a canvas_px square."""
    tile = image.resize((max(1, round(tile_px * stretch)), max(1, tile_px)), Image.LANCZOS)
    canvas = Image.new(image.mode, (canvas_px, canvas_px))
    for y in range(0, canvas_px, tile.size[1]):
        for x in range(0, canvas_px, tile.size[0]):
            canvas.paste(tile, (x, y))
    return canvas


def cover_field(image, px_per_m, tile_m, out_px, azimuth_deg, bias):
    """The cover map over the patch: the mean of two tilings (tile_m and tile_m * phi) so no period repeats inside the
    patch, stretched 1 + bias along the lodging azimuth and rotated to it on an oversized canvas, then cropped."""
    canvas_px = math.ceil(out_px * math.sqrt(2)) + 2
    a = tiled(image, round(tile_m * px_per_m), canvas_px, 1 + bias)
    b = tiled(image, round(tile_m * PHI * px_per_m), canvas_px, 1 + bias)
    field = Image.blend(a, b, 0.5).rotate(azimuth_deg, resample=Image.BICUBIC)
    off = (canvas_px - out_px) // 2
    return field.crop((off, off, off + out_px, off + out_px))


def rotate_normals(normal, degrees):
    """Turns the tangent-space vectors of a GL normal map (R +x, G +y up the image) by the angle its image was rotated."""
    c, s = math.cos(math.radians(degrees)), math.sin(math.radians(degrees))
    r, g, b = normal.split()
    rf, gf = r.convert("F"), g.convert("F")
    x = ImageMath.lambda_eval(lambda a: (a["r"] - 128) * c - (a["g"] - 128) * s + 128.5, r=rf, g=gf)
    y = ImageMath.lambda_eval(lambda a: (a["r"] - 128) * s + (a["g"] - 128) * c + 128.5, r=rf, g=gf)
    return Image.merge("RGB", (x.convert("L"), y.convert("L"), b))


def coverage(layer, outside=None):
    """The share of an L mask that is set (soft edges count partially), optionally counted outside another mask."""
    if outside is not None:
        layer = ImageChops.multiply(layer, ImageChops.invert(outside))
    h = layer.histogram()
    return sum(v * h[v] for v in range(256)) / (255 * layer.size[0] * layer.size[1])


def to_px(point, out_px, size_m):
    """The pixel of an asset point (x, z) on a map spanning size_m centred on the origin: u = x / size_m + 0.5 and
    v = z / size_m + 0.5, as the importer lays the base map (tiling 1/size_m, offset 0.5), with v up the image."""
    return ((point[0] / size_m + 0.5) * out_px, (0.5 - point[1] / size_m) * out_px)


def outline_layer(bare_patches, out_px, size_m):
    """The L mask of the floors: every step outline filled, 255 inside and 0 outside."""
    layer = Image.new("L", (out_px, out_px), 0)
    draw = ImageDraw.Draw(layer)
    for patch in bare_patches:
        draw.polygon([to_px(p, out_px, size_m) for p in patch["outline"]], fill=255)
    return layer


def bare_layer(bare_patches, out_px, size_m):
    """The L mask of the bare soil: the outlines filled, times FLOOR_EDGE + (1 - FLOOR_EDGE) * a ramp, every patch's
    core filled and blurred with sigma FEATHER_M / 6 (a three-pass box blur reaching about 3 sigma): 255 on the
    outline shrunk by the feather, FLOOR_EDGE at the outline and 0 outside it (PR5)."""
    ramp = Image.new("L", (out_px, out_px), 0)
    draw = ImageDraw.Draw(ramp)
    for patch in bare_patches:
        if len(patch["core"]) >= 3:
            draw.polygon([to_px(p, out_px, size_m) for p in patch["core"]], fill=255)
    ramp = ramp.filter(ImageFilter.GaussianBlur(patches.FEATHER_M / 6 * out_px / size_m))
    floor = outline_layer(bare_patches, out_px, size_m)
    return ImageMath.lambda_eval(lambda a: a["f"] * (FLOOR_EDGE + (1 - FLOOR_EDGE) * a["r"] / 255.0),
                                 f=floor.convert("F"), r=ramp.convert("F")).convert("L")


def lip_layer(bare_patches, out_px, size_m):
    """The L mask of a floor's lip, the mat's cut edge thinned just outside its outline: the outlines filled,
    blurred with sigma LIP_M / 3 and doubled, then cut out of the outlines, so 0 on a floor (its soil and strewn
    straws are the bare mask's), 255 at the outline and falling to nothing LIP_M outside. A path's mat bridges are
    never in it: the mat there is full depth, so the map shows the mat (R7)."""
    floors = outline_layer(bare_patches, out_px, size_m)
    blurred = floors.filter(ImageFilter.GaussianBlur(LIP_M / 3 * out_px / size_m))
    return ImageChops.multiply(blurred.point(lambda v: min(255, 2 * v)), ImageChops.invert(floors))


def green_layer(strips, out_px, size_m):
    """The L mask of the green strips (lib/mat.py): their ellipses filled and blurred with sigma GREEN_FEATHER_M / 2."""
    layer = Image.new("L", (out_px, out_px), 0)
    draw = ImageDraw.Draw(layer)
    for strip in strips:
        for part in strip["parts"]:
            draw.polygon([to_px(p, out_px, size_m) for p in mat.ellipse(part, strip["angle_deg"])], fill=255)
    return layer.filter(ImageFilter.GaussianBlur(mat.GREEN_FEATHER_M / 2 * out_px / size_m))


def relief_layer(relief, relief_m, out_px, size_m):
    """The relief (lib/mat.py) as an L map, 0 at -relief_m, 128 at the mean and 255 at +relief_m: RELIEF_SAMPLES^2
    samples at their pixel centres, resized bicubic to out_px."""
    n = RELIEF_SAMPLES
    small = Image.new("L", (n, n), 128)
    if relief_m > 0:
        small.putdata([max(0, min(255, round(128 + 127 * relief(((i + 0.5) / n - 0.5) * size_m,
                                                                 (0.5 - (j + 0.5) / n) * size_m) / relief_m)))
                       for j in range(n) for i in range(n)])
    return small.resize((out_px, out_px), Image.BICUBIC)


def strewn(bare, cover, rng, px_per_m, lobes=None):
    """The mask `bare` with a lobed outline and the cover's straws lying across its feather: clamp(2 bare - c), c the
    cover's luminance high-passed at STRAW_M (only the straws and the gaps between them, never the cover's tile-sized
    shading, which would repeat) blended with a `lobes` share of seeded value noise of LOBE_M cells, stretched to
    0-255. Where bare is full the soil stays, its feather breaks into lobes and straws over soil, and the outer half
    keeps soil only in the darkest gaps, so a patch has a ragged edge, never a clean ellipse (H 9-14). `lobes` is
    BARE_LOBES unless given."""
    c = straw_field(bare.size, cover, rng, px_per_m, BARE_LOBES if lobes is None else lobes)
    return ImageMath.lambda_eval(lambda a: a["b"] * 2 - a["c"], b=bare.convert("F"), c=c).convert("L")


def straw_field(size, cover, rng, px_per_m, lobes):
    """The c of strewn(), as an F image of `size`: the cover's high-passed luminance blended with a `lobes` share of
    the seeded value noise, stretched to 0-255."""
    cells = math.ceil(size[0] / (LOBE_M * px_per_m)) + 1
    noise = Image.new("L", (cells, cells))
    noise.putdata([rng.randrange(256) for _ in range(cells * cells)])
    noise = noise.resize(size, Image.BICUBIC)
    luma = cover.convert("L")
    local = luma.filter(ImageFilter.GaussianBlur(STRAW_M * px_per_m))
    straws = ImageMath.lambda_eval(lambda a: a["l"] - a["m"] + 128, l=luma.convert("F"), m=local.convert("F")).convert("L")
    return ImageOps.autocontrast(Image.blend(straws, noise, lobes), cutoff=1).convert("F")


def floor_straws(bare, c):
    """The straws of the field c strewn over the soil of the mask `bare` so that they cover FLOOR_STRAWS of it (R8):
    clamp(2 m - c) on a level m found by LEVEL_STEPS bisection steps on coverage(bare x them) = (1 - FLOOR_STRAWS)
    coverage(bare), the level kept on the side that leaves the soil at least that (none covered at FLOOR_STRAWS 0).
    A level's straws cover 2 (1 - m)^2 of a full mask only where c is uniform, and the feather's own straws lie under
    many of them, so the level is solved on the mask itself."""
    soil, lo, hi = (1 - FLOOR_STRAWS) * coverage(bare), 0.0, 255.0

    def straws(m):
        return ImageMath.lambda_eval(lambda a: 2 * m - a["c"], c=c).convert("L")

    for _ in range(LEVEL_STEPS):
        m = (lo + hi) / 2
        if coverage(ImageChops.multiply(bare, straws(m))) > soil:
            hi = m
        else:
            lo = m
    return straws(hi)


def _load_set(root, manifest, texture_set, material, what):
    """The diffuse (RGB), normal (RGB), roughness (L) and AO (L, white when the set has none) of a pinned set."""
    maps = set_maps(root, manifest, texture_set, material)
    for name in ("diffuse", "normal", "roughness"):
        if name not in maps:
            raise DeriveError(f"{material}: {what} set {texture_set} has no {name} map (it has {', '.join(sorted(maps))})")
    for name in sorted(maps):
        if not os.path.isfile(maps[name]):
            raise DeriveError(f"{fetch.relpath(root, maps[name])}: missing; run fetch.py")
    return maps


def derive_ground(root, manifest, material, recipe):
    g = ground_recipe(root, recipe, material)
    sets = {"soil": g["soil"]["texture_set"], "cover": g["cover"]["texture_set"], "detail": g["detail"]["texture_set"]}
    maps = {role: _load_set(root, manifest, texture_set, material, role) for role, texture_set in sets.items()}
    stamp = {"material": material, "kind": "ground", "formula": GROUND_FORMULA, "pillow": PIL.__version__,
             "constants": _constants(GROUND_CONSTANTS),
             "layout": {"params": g["layout_params"], "inputs": g["layout"], "constants": patches.constants(),
                        "green": g["green"], "relief": g["relief"], "fields": mat.constants()},
             "recipe": recipe,
             "sources": {f"{role}/{name}": {"name": os.path.basename(path), "sha256": fetch.sha256(path)}
                         for role in ("soil", "cover", "detail") for name, path in sorted(maps[role].items())
                         if name in ("diffuse", "normal", "roughness", "ao")}}
    stamp["inputs_sha256"] = _inputs_sha256(stamp, ("formula", "pillow", "constants", "layout", "recipe", "sources"))
    outputs = {name: os.path.join(root, MATERIALS, f"{material}_{name}.png") for name in GROUND_MAPS}
    stamp_path = os.path.join(root, MATERIALS, f"{material}.recipe.json")
    if _up_to_date(list(outputs.values()), stamp_path, stamp["inputs_sha256"]):
        return f"{fetch.relpath(root, outputs['albedo'])}: up to date"

    def load(role, name):
        if name == "ao" and "ao" not in maps[role]:
            return Image.new("L", (1, 1), 255)
        image = Image.open(maps[role][name])
        return image.convert("RGB") if name in ("diffuse", "normal") else grey(image)

    out_px, size_m, az, bias = g["output_px"], g["size_m"], g["azimuth_deg"], g["bias"]
    px_per_m = out_px / size_m
    soil_tile, cover_tile = g["soil"]["tile_size_m"], g["cover"]["tile_size_m"]

    def soil(name):
        return tiled(load("soil", name), round(soil_tile * px_per_m), out_px)

    def cover(name):
        return cover_field(load("cover", name), px_per_m, cover_tile, out_px, az, bias)

    rng = random.Random(g["seed"])
    try:
        layout = patches.layout(**g["layout"])
        regrowth = mat.green(**g["green"], bare_patches=layout["patches"])
    except ValueError as e:
        raise DeriveError(f"{material}: {e}")
    bare = bare_layer(layout["patches"], out_px, size_m)
    green = green_layer(regrowth["strips"], out_px, size_m)
    relief = relief_layer(mat.relief(**g["relief"]), g["relief"]["relief_m"], out_px, size_m)
    soil_albedo, cover_albedo = soil("diffuse"), cover("diffuse")
    soil_gain = [1.0, 1.0, 1.0]
    if g["soil_target"] is not None:
        soil_albedo, soil_gain = match_mean(soil_albedo, g["soil_target"])
    bare = strewn(bare, cover_albedo, rng, px_per_m)
    # A floor's lip: the mat thins at its cut edge and the soil shows between its straws.
    lip = strewn(lip_layer(layout["patches"], out_px, size_m).point(lambda v: round(v * LIP_LEVEL)), cover_albedo, rng,
                 px_per_m, BARE_LOBES)
    # The mat's depth shows through the cover's darkest gaps (strewn on a level whose share 2 level^2 is the cover's
    # gap_share), so the straw layer itself is brighter for the same mean.
    gaps = strewn(Image.new("L", (out_px, out_px), round(255 * math.sqrt(g["gap_share"] / 2))), cover_albedo, rng,
                  px_per_m, GAP_LOBES)
    # Straws lie over the regrowth: a strip shows between the cover's straws (strewn at GREEN_LEVEL; H 19-21).
    shown = strewn(green.point(lambda v: round(v * GREEN_LEVEL)), cover_albedo, rng, px_per_m, GAP_LOBES)
    # Straws lie strewn all over a floor's soil (H 12, H 14): the cover's straws over FLOOR_STRAWS of it. The bare
    # mask itself (PR7 c) is the one above.
    floors = outline_layer(layout["patches"], out_px, size_m)
    scatter = floor_straws(bare, straw_field((out_px, out_px), cover_albedo, rng, px_per_m, GAP_LOBES))
    bare_soil_share = coverage(bare)  # the soil the bare mask shows in the floors, before the strewn straws
    bare = ImageChops.multiply(bare, scatter)
    # The relief's hollows: darker straws and less ambient light (G 1-12).
    tone = relief.point(lambda v: round(255 * (1 - RELIEF_TONE * (1 - v / 255))))
    cover_albedo = ImageChops.multiply(cover_albedo, Image.merge("RGB", (tone, tone, tone)))
    soil_mask = ImageChops.lighter(bare, lip)
    bare_share = layout["share"]  # the outlines'
    floor_soil_share = coverage(ImageChops.multiply(soil_mask, floors)) / max(1e-6, coverage(floors))
    gap_share, green_share = coverage(gaps, outside=bare), coverage(green, outside=bare)

    # The straw layer takes the gain that lands the whole patch on the target with the soil, the gaps and the strips
    # at their own colours; a global pass then corrects what clipping and the feathered edges moved.
    if g["gap_albedo"] is None:
        litter = soil_albedo
    else:
        litter, _ = match_mean(cover_albedo, g["gap_albedo"])
    soil_share = coverage(soil_mask)
    litter_share = coverage(gaps, outside=soil_mask)
    strip_share = coverage(shown, outside=ImageChops.lighter(soil_mask, gaps))
    straw_share = max(1e-6, 1 - soil_share - litter_share - strip_share)
    straw_target = [(t - soil_share * s - litter_share * lt - strip_share * tint) / straw_share
                    for t, s, lt, tint in zip(g["target"], linear_means(soil_albedo), linear_means(litter), g["green_tint"])]
    straw, straw_gain = match_mean(cover_albedo, [max(0.001, t) for t in straw_target])
    strips, _ = match_mean(cover_albedo, g["green_tint"])
    albedo = Image.composite(soil_albedo, Image.composite(litter, Image.composite(strips, straw, shown), gaps), soil_mask)
    albedo, final_gain = match_mean(albedo, g["target"])
    normal = Image.composite(soil("normal"), rotate_normals(cover("normal"), az), soil_mask)
    ao = ImageChops.multiply(Image.composite(soil("ao"), cover("ao"), soil_mask),
                             relief.point(lambda v: round(255 * (1 - RELIEF_AO * (1 - v / 255)))))
    roughness = Image.composite(soil("roughness"), cover("roughness"), soil_mask)
    zero = Image.new("L", (out_px, out_px), 0)
    mask = Image.merge("RGBA", (zero, ao, zero, roughness.point(lambda v: 255 - v)))
    # Grey, so URP's "detail x2" adds the straws' grain without moving the base's hue: per-channel gains on the
    # yellow straw set would amplify its weak blue channel into blue and lilac flecks.
    detail, detail_gain = match_mean(load("detail", "diffuse").convert("L").convert("RGB"), [0.5, 0.5, 0.5])

    os.makedirs(os.path.dirname(stamp_path), exist_ok=True)
    for name, image in (("albedo", albedo), ("normal", normal), ("mask", mask), ("detail_albedo", detail)):
        image.save(outputs[name], "PNG")
    means = linear_means(albedo)
    measured = {"mean_linear_rgb": [round(m, 4) for m in means], "mean_luminance": round(luminance(means), 4),
                "target_luminance": round(luminance(g["target"]), 4),
                "luminance_error_pct": round(100 * (luminance(means) / luminance(g["target"]) - 1), 2),
                "bare_share": round(bare_share, 4), "bare_patches": len(layout["patches"]),
                "bare_soil_share": round(bare_soil_share, 4), "floor_soil_share": round(floor_soil_share, 4),
                "green_share": round(green_share, 4),
                "gap_share": round(gap_share, 4), "lip_soil_share": round(coverage(lip, outside=bare), 4),
                "soil_mean_srgb": "#" + "".join(f"{to_srgb8(c):02x}" for c in linear_means(soil_albedo)),
                "soil_gain": [round(x, 3) for x in soil_gain],
                "straw_gain": [round(x, 3) for x in straw_gain], "final_gain": [round(x, 3) for x in final_gain],
                "detail_gain": [round(x, 3) for x in detail_gain]}
    stamp.update({"size": [out_px, out_px], "detail_size": list(detail.size),
                  "outputs": [os.path.basename(outputs[name]) for name in GROUND_MAPS], "measured": measured})
    _write_stamp(stamp_path, stamp)
    return (f"{fetch.relpath(root, outputs['albedo'])}: derived {out_px}x{out_px} ground from soil {sets['soil']} and "
            f"cover {sets['cover']}; mean luminance {measured['mean_luminance']:.4f} (target "
            f"{measured['target_luminance']:.4f}, {measured['luminance_error_pct']:+.1f} %), bare {100 * bare_share:.1f} % "
            f"({len(layout['patches'])} outlines from {g['layout_params']}; soil shown {100 * bare_soil_share:.1f} %, "
            f"{100 * floor_soil_share:.1f} % of the floors with the straws strewn over them), "
            f"green {100 * green_share:.1f} %, gaps {100 * gap_share:.1f} %, straw gain {measured['straw_gain']}; recipe recorded in "
            f"{os.path.basename(stamp_path)}")


# --- palette recipes ----------------------------------------------------------------------------------------------

def derive_palette(root, material, recipe):
    """The albedo of a palette recipe: its colours as columns, left to right in the recipe's order, each flat or a
    gradient of its stops from the bottom up."""
    colours = recipe.get("colours")
    if not isinstance(colours, dict) or not colours or not all(isinstance(k, str) for k in colours):
        raise DeriveError(f"{material}: a palette recipe needs a non-empty \"colours\" object of name -> sRGB colour")
    if "texture_set" in recipe or "mask" in recipe:
        raise DeriveError(f"{material}: a palette recipe is flat colour; it takes neither \"texture_set\" nor \"mask\"")
    stops = {}
    for name, value in colours.items():
        listed = value if isinstance(value, list) and value else [value]
        stops[name] = [_colour({"stop": v}, "stop", f"{material}.colours.{name}") for v in listed]
    stamp = {"material": material, "kind": "palette", "formula": PALETTE_FORMULA,
             "constants": _constants(PALETTE_CONSTANTS), "recipe": recipe}
    stamp["inputs_sha256"] = _inputs_sha256(stamp, ("formula", "constants", "recipe"))
    png_path = os.path.join(root, MATERIALS, f"{material}_albedo.png")
    stamp_path = os.path.join(root, MATERIALS, f"{material}.recipe.json")
    if _up_to_date([png_path], stamp_path, stamp["inputs_sha256"]):
        return f"{fetch.relpath(root, png_path)}: up to date"
    block, height = PALETTE_BLOCK_PX, PALETTE_HEIGHT_PX
    columns = 1 << (len(stops) - 1).bit_length()
    image = Image.new("RGB", (block * columns, height))
    lists = list(stops.values())
    for i in range(columns):
        column = lists[min(i, len(lists) - 1)]
        for y in range(height):
            v = 1 - (y + 0.5) / height  # up the map: row 0 is the top
            image.paste(tuple(to_srgb8(c) for c in _gradient(column, v)), (i * block, y, (i + 1) * block, y + 1))
    os.makedirs(os.path.dirname(png_path), exist_ok=True)
    image.save(png_path, "PNG")
    stamp.update({"size": list(image.size), "columns": list(stops)})
    _write_stamp(stamp_path, stamp)

    def shown(value):
        return "/".join(value) if isinstance(value, list) else value

    return (f"{fetch.relpath(root, png_path)}: derived {image.size[0]}x{image.size[1]} palette "
            f"({', '.join(f'{name} {shown(colours[name])}' for name in stops)}); recipe recorded in "
            f"{os.path.basename(stamp_path)}")


def _gradient(stops, v):
    """The linear colour at height v in [0, 1] of stops spread evenly from 0 to 1."""
    if len(stops) == 1:
        return stops[0]
    t = v * (len(stops) - 1)
    k = min(int(t), len(stops) - 2)
    f = t - k
    return [a + (b - a) * f for a, b in zip(stops[k], stops[k + 1])]


# --- main ---------------------------------------------------------------------------------------------------------

def all_materials(root):
    folder = os.path.join(root, RECIPES)
    return sorted(name[:-5] for name in os.listdir(folder) if name.endswith(".json"))


def main(argv=None):
    parser = argparse.ArgumentParser(description="Derive the packed mask maps and ground maps of the material recipes.")
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
