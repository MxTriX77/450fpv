"""Derived textures by script (p1-pipeline-hardening, "Derived textures by script"; D-004, AST-07): the packed mask
map of a material recipe, or the whole map set of a ground recipe, built from the pinned sources with the recipe
recorded beside them. Pillow only.

    python tools/assets/derive.py <material>...     derive these recipes (assets-src/materials/<material>.json)
    python tools/assets/derive.py --all             every recipe under assets-src/materials/

A recipe with a "mask" object gets unity/Assets/_Project/Art/Materials/<material>_mask.png, an 8-bit linear RGBA map
that URP/Lit reads twice: _MetallicGlossMap takes metallic from R and smoothness from A, _OcclusionMap takes occlusion
from G; B is unused and 0. The object holds exactly "metallic", "occlusion" and "smoothness", each a constant in
[0, 1] or a map of the recipe's texture set by its manifest map name ("ao", "roughness", ...), inverted as
"1 - roughness". The mask has the sources' size; 16-bit sources are scaled to 8 bits, never clipped.

A recipe of kind "ground" (p1-s04-belt-straw, "Ground recipes with a detail layer") names a soil set and a cover set,
a seeded patch description and a target mean albedo, and gets four maps for one patch of size_m x size_m at
output_px (2K at most, AST-08): <material>_albedo.png (sRGB), <material>_normal.png (GL tangent space),
<material>_mask.png (the packed mask above: metallic 0, the sets' AO, 1 - roughness) and
<material>_detail_albedo.png, the detail set's diffuse at its own size with its linear mean per channel moved to 0.5,
so URP/Lit's "detail albedo x2" layer adds grain without shifting the base's mean. The cover is tiled at its
tile_size_m and again at tile_size_m x phi, averaged, so no period repeats inside the patch; it is stretched 1 + bias
along the lodging azimuth and rotated to it (the cover normals turn with it). Blobby bare patches (soil shows) and
green strips along the azimuth (the cover tinted to the strip colour) are stamped from the seed until they cover
their shares, feathered 0.1 m and 0.5 m. The straw layer takes the per-channel gain that lands the patch's linear mean colour on
albedo_target with the soil and the strips at their own colours, then a global pass corrects clipping; the achieved
mean, the shares and the gains are recorded in the stamp. The azimuth is degrees from +u towards +v of the map,
which is +X towards +Z of the asset when its UVs are metres.

Beside the PNGs, <material>_mask.recipe.json (masks) or <material>.recipe.json (ground) records what made them: the
formula, the recipe, each source's file name and SHA-256 as found on disk, and the SHA-256 of all of that together
(plus the Pillow version for ground maps, whose resampling it fixes). A run rewrites the files only when that hash
differs from the stamp or a PNG is missing, so an unchanged recipe is a no-op and a changed recipe, source or formula
regenerates; nothing is edited by hand. Derived files live under Art/Materials/, beside the .mat they feed, never
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
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageMath

import fetch  # the manifest, its file paths and the repo root; same folder

RECIPES = os.path.join("assets-src", "materials")
MATERIALS = os.path.join("unity", "Assets", "_Project", "Art", "Materials")
FORMULA = "R metallic, G occlusion, B 0, A smoothness; 8-bit linear at the sources' size"
CHANNELS = ("metallic", "occlusion", "smoothness")  # R, G, A
MAP_EXPR = re.compile(r"^(1 - )?([a-z]+)$")

GROUND_FORMULA = ("ground v1: cover tiled at tile_size_m and at tile_size_m*phi, averaged, stretched 1+bias along the "
                  "lodging azimuth and rotated to it; soil tiled at its tile_size_m; seeded blobs (an ellipse and three "
                  "satellites) stamped to the bare and green shares, feathered 0.1 m (bare) and 0.5 m (green along the "
                  "azimuth, counted outside bare); albedo = cover with the straw gain for albedo_target, strips tinted "
                  "to the green tint, soil under bare, then a global gain against clipping; normal composited the same "
                  "with the cover normals rotated by the azimuth; mask R 0, G AO, B 0, A 1 - roughness; detail albedo "
                  "= the detail set's diffuse at its size with a linear mean of 0.5 per channel; 8-bit sRGB albedos")
GROUND_MAPS = ("albedo", "normal", "mask", "detail_albedo")
MAX_OUTPUT_PX = 2048  # AST-08
PHI = 1.618033988749895
BARE_FEATHER_M = 0.1   # a soil patch's edge: straw thins out over about a hand's width (assumed)
GREEN_FEATHER_M = 0.5  # regrowth fades into the mat over half a metre (assumed)
SHARE_TOLERANCE = 0.005
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
    """Derives the material's maps when its recipe asks for them and the stamp is stale. Returns a report line."""
    recipe = load_recipe(root, material)
    kind = recipe.get("kind")
    if kind == "ground":
        return derive_ground(root, manifest, material, recipe)
    if kind is not None:
        raise DeriveError(f"{material}: unknown recipe kind {kind!r} (omit it for a tiled set, or 'ground')")
    mask = recipe.get("mask")
    if mask is None:
        return f"{material}: no mask in the recipe, nothing to derive"
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
    stamp["inputs_sha256"] = _inputs_sha256(stamp, ("formula", "mask", "sources"))

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
    os.makedirs(os.path.dirname(png_path), exist_ok=True)
    Image.merge("RGBA", (r, g, Image.new("L", size, 0), a)).save(png_path, "PNG")
    stamp["size"] = list(size)
    _write_stamp(stamp_path, stamp)
    return (f"{fetch.relpath(root, png_path)}: derived {size[0]}x{size[1]} from {texture_set} "
            f"({', '.join(sorted(used)) or 'constants only'}); recipe recorded in {os.path.basename(stamp_path)}")


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


def ground_recipe(recipe, material):
    """Validates a ground recipe and returns its numbers as plain values (shares, sizes, colours in linear RGB)."""
    _number(recipe, "seed", material, integer=True)
    size_m = _number(recipe, "size_m", material, lo=1, hi=200)
    output_px = _number(recipe, "output_px", material, lo=16, hi=MAX_OUTPUT_PX, integer=True)
    soil, cover, detail = (_set_ref(recipe, k, material) for k in ("soil", "cover", "detail"))
    patches = _object(recipe, "patches", material)
    bare, green, lodging = (_object(patches, k, f"{material}.patches") for k in ("bare", "green", "lodging"))
    bare_share = _number(bare, "share", f"{material}.patches.bare", lo=0, hi=1)
    green_share = _number(green, "share", f"{material}.patches.green", lo=0, hi=1)
    if bare_share + green_share > 1:
        raise DeriveError(f"{material}.patches: bare.share + green.share = {bare_share + green_share} exceeds 1")
    return {
        "seed": recipe["seed"], "size_m": size_m, "output_px": output_px,
        "soil": soil, "cover": cover, "detail": detail,
        "bare_share": bare_share, "bare_size_m": _range(bare, "size_m", f"{material}.patches.bare"),
        "green_share": green_share, "green_width_m": _range(green, "width_m", f"{material}.patches.green"),
        "green_length_m": _range(green, "length_m", f"{material}.patches.green"),
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


def blob(rng):
    """The outline of one patch: a main ellipse and three satellites, as (dx, dy, scale) fractions of its axes, so
    no patch is a clean ellipse."""
    return [(0.0, 0.0, 1.0)] + [(rng.uniform(-0.35, 0.35), rng.uniform(-0.35, 0.35), rng.uniform(0.45, 0.8))
                                for _ in range(3)]


def stamp_blob(layer, centre, px_per_m, length_m, width_m, angle_deg, feather_m, parts):
    """Pastes one feathered blob, length_m along angle_deg and width_m across, centred on `centre` (pixels)."""
    feather = max(1, round(feather_m * px_per_m))
    length, width = round(length_m * px_per_m), round(width_m * px_per_m)
    pad = 2 * feather + max(length, width) // 2
    size = (length + 2 * pad, width + 2 * pad)
    shape = Image.new("L", size, 0)
    draw = ImageDraw.Draw(shape)
    for dx, dy, scale in parts:
        ox, oy, half_l, half_w = size[0] / 2 + dx * length, size[1] / 2 + dy * width, length * scale / 2, width * scale / 2
        draw.ellipse((ox - half_l, oy - half_w, ox + half_l, oy + half_w), fill=255)
    shape = shape.filter(ImageFilter.GaussianBlur(feather / 2)).rotate(angle_deg, resample=Image.BILINEAR, expand=True)
    layer.paste(255, (centre[0] - shape.size[0] // 2, centre[1] - shape.size[1] // 2), shape)


def stamped(rng, out_px, px_per_m, share, length_m, width_m, angle, feather_m, what, outside=None):
    """An L mask of random blobs (length drawn from length_m along `angle(rng)`, width from width_m, edges feathered
    over feather_m) stamped until `share` of the area is covered (counted outside the `outside` mask), each stamp
    scaled down when it would overshoot the share by more than SHARE_TOLERANCE, so the result lands within it."""
    layer = Image.new("L", (out_px, out_px), 0)
    for _ in range(10000):
        if coverage(layer, outside) >= share - SHARE_TOLERANCE:
            return layer
        length, width, degrees, parts = rng.uniform(*length_m), rng.uniform(*width_m), angle(rng), blob(rng)
        centre = (rng.randrange(out_px), rng.randrange(out_px))
        for _ in range(12):
            trial = layer.copy()
            stamp_blob(trial, centre, px_per_m, length, width, degrees, feather_m, parts)
            if coverage(trial, outside) <= share + SHARE_TOLERANCE or min(length, width) <= 0.3:
                break
            length, width = length * 0.75, width * 0.75
        layer = trial
    raise DeriveError(f"{what}: could not reach a share of {share} in 10000 stamps")


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
    g = ground_recipe(recipe, material)
    sets = {"soil": g["soil"]["texture_set"], "cover": g["cover"]["texture_set"], "detail": g["detail"]["texture_set"]}
    maps = {role: _load_set(root, manifest, texture_set, material, role) for role, texture_set in sets.items()}
    stamp = {"material": material, "kind": "ground", "formula": GROUND_FORMULA, "pillow": PIL.__version__,
             "recipe": recipe,
             "sources": {f"{role}/{name}": {"name": os.path.basename(path), "sha256": fetch.sha256(path)}
                         for role in ("soil", "cover", "detail") for name, path in sorted(maps[role].items())
                         if name in ("diffuse", "normal", "roughness", "ao")}}
    stamp["inputs_sha256"] = _inputs_sha256(stamp, ("formula", "pillow", "recipe", "sources"))
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
    stretch = 1 + bias
    bare = stamped(rng, out_px, px_per_m, g["bare_share"],
                   [s * stretch for s in g["bare_size_m"]], [s / stretch for s in g["bare_size_m"]],
                   lambda r: az + r.uniform(-45, 45), BARE_FEATHER_M, f"{material}: bare patches")
    green = stamped(rng, out_px, px_per_m, g["green_share"], g["green_length_m"], g["green_width_m"],
                    lambda r: az + r.uniform(-10, 10), GREEN_FEATHER_M, f"{material}: green strips", outside=bare)
    bare_share, green_share = coverage(bare), coverage(green, outside=bare)

    # The straw layer takes the gain that lands the whole patch on the target with the soil and the strips at their
    # own colours; a global pass then corrects what clipping and the feathered edges moved.
    soil_albedo, cover_albedo = soil("diffuse"), cover("diffuse")
    straw_share = max(1e-6, 1 - bare_share - green_share)
    straw_target = [(t - bare_share * s - green_share * tint) / straw_share
                    for t, s, tint in zip(g["target"], linear_means(soil_albedo), g["green_tint"])]
    straw, straw_gain = match_mean(cover_albedo, [max(0.001, t) for t in straw_target])
    strips, _ = match_mean(cover_albedo, g["green_tint"])
    albedo = Image.composite(soil_albedo, Image.composite(strips, straw, green), bare)
    albedo, final_gain = match_mean(albedo, g["target"])
    normal = Image.composite(soil("normal"), rotate_normals(cover("normal"), az), bare)
    ao = Image.composite(soil("ao"), cover("ao"), bare)
    roughness = Image.composite(soil("roughness"), cover("roughness"), bare)
    zero = Image.new("L", (out_px, out_px), 0)
    mask = Image.merge("RGBA", (zero, ao, zero, roughness.point(lambda v: 255 - v)))
    detail, detail_gain = match_mean(load("detail", "diffuse"), [0.5, 0.5, 0.5])

    os.makedirs(os.path.dirname(stamp_path), exist_ok=True)
    for name, image in (("albedo", albedo), ("normal", normal), ("mask", mask), ("detail_albedo", detail)):
        image.save(outputs[name], "PNG")
    means = linear_means(albedo)
    measured = {"mean_linear_rgb": [round(m, 4) for m in means], "mean_luminance": round(luminance(means), 4),
                "target_luminance": round(luminance(g["target"]), 4),
                "luminance_error_pct": round(100 * (luminance(means) / luminance(g["target"]) - 1), 2),
                "bare_share": round(bare_share, 4), "green_share": round(green_share, 4),
                "straw_gain": [round(x, 3) for x in straw_gain], "final_gain": [round(x, 3) for x in final_gain],
                "detail_gain": [round(x, 3) for x in detail_gain]}
    stamp.update({"size": [out_px, out_px], "detail_size": list(detail.size),
                  "outputs": [os.path.basename(outputs[name]) for name in GROUND_MAPS], "measured": measured})
    _write_stamp(stamp_path, stamp)
    return (f"{fetch.relpath(root, outputs['albedo'])}: derived {out_px}x{out_px} ground from soil {sets['soil']} and "
            f"cover {sets['cover']}; mean luminance {measured['mean_luminance']:.4f} (target "
            f"{measured['target_luminance']:.4f}, {measured['luminance_error_pct']:+.1f} %), bare {100 * bare_share:.1f} %, "
            f"green {100 * green_share:.1f} %, straw gain {measured['straw_gain']}; recipe recorded in "
            f"{os.path.basename(stamp_path)}")


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
