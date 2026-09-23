"""Validates a map package against map format 1.0 (see game/maps/README.md). Stdlib only.

    python tools/map/validate_map.py game/maps/<id> [--surfaces PATH] [--catalog PATH]

Prints one ERROR line per problem and exits 1, or prints OK and exits 0. The shared surface table and asset
catalog default to the ones in this repository; scene paths resolve against its game/ folder.
"""
import argparse
import json
import math
import os
import re
import sys
import zlib

from mappng import GREY, RGBA, PngError, read_png, scanlines

SUPPORTED_MAJOR = 1
GAME = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "game")
FILES = ["map.json", "height.r16", "surface.png", "cover.png", "objects.json"]
MAX_SIZE_M = 8192
CHUNK_M = 256
COVER_TYPES = ["grass", "straw", "twigs", "litter"]

# Surface fields: (low, high), or (low, high, "range") for a [min, max] pair.
SURFACE_FIELDS = {
    "soil": {
        "bearing_n_per_m3": (1e4, 1e9),
        "damping_ns_per_m3": (0, 1e8),
        "friction_static": (0, 2),
        "friction_kinetic": (0, 2),
        "max_sink_m": (0, 1),
        "unload_stiffness_ratio": (1, 100),
    },
    "micro_relief": {"amplitude_m": (0, 0.5), "wavelength_m": (0.05, 10)},
    "pitfalls": {"density_per_m2": (0, 1), "depth_m": (0, 2, "range"), "radius_m": (0, 2, "range")},
    "material": {"roughness": (0, 1)},
}
RIDGE_FIELDS = {"amplitude_m": (0, 0.3), "spacing_m": (0.1, 5), "azimuth_deg": (0, 180)}
COVER_FIELDS = {
    "height_m": (0, 3, "range"),
    "stems_per_m2": (0, 5000),
    "diameter_m": (0, 0.2, "range"),
    "lateral_stiffness_n_per_m": (0, 1e4),
    "hook_probability": (0, 1),
    "hook_release_n": (0, 500, "range"),
}
MAT_FIELDS = {"depth_m": (0, 0.5, "range"), "modulus_pa": (10, 1e6), "damping_ratio": (0, 2),
              "friction_static": (0, 2), "friction_kinetic": (0, 2)}
MATERIAL_FIELDS = {"friction_static": (0, 2), "friction_kinetic": (0, 2)}
MATERIAL_OPTIONAL = {"stiffness_n_per_m": (1e2, 1e8), "damping_ratio": (0, 2), "edge_radius_m": (1e-4, 0.05)}
SHAPE_FIELDS = {"box": ["size_m"], "sphere": ["radius_m"], "cylinder": ["radius_m", "height_m"],
                "capsule": ["radius_m", "height_m"], "capsule_chain": ["segment_m"]}

GEO_KEYS = {"lat", "lon", "lng", "latitude", "longitude", "epsg", "crs", "srs", "utm", "mgrs", "wgs84",
            "geo", "georef", "georeference", "geolocation", "geojson", "geotiff", "geotransform"}
COORDINATE_PAIR = re.compile(r"-?\d{1,3}\.\d{3,}\s*[,;/ ]\s*-?\d{1,3}\.\d{3,}")
MGRS = re.compile(r"\b\d{1,2}[C-HJ-NP-X]\s?[A-HJ-NP-Z]{2}\s?\d{2,5}\s?\d{2,5}\b")
GIS_SIDECARS = (".pgw", ".wld", ".prj", ".aux.xml", ".tfw", ".kml", ".kmz", ".gpx", ".geojson")

errors = []


def error(message):
    errors.append(message)
    print(f"ERROR: {message}")


def is_number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def is_vec3(value):
    return isinstance(value, list) and len(value) == 3 and all(is_number(v) for v in value)


def check_fields(obj, spec, where, prefix=""):
    """Checks every field in spec is present and within its physical range."""
    for field, rule in spec.items():
        name = prefix + field
        if not isinstance(obj, dict) or field not in obj:
            error(f"{where}: {name} is missing")
            continue
        value, low, high = obj[field], rule[0], rule[1]
        if len(rule) == 3:
            if not (isinstance(value, list) and len(value) == 2 and all(is_number(v) for v in value)):
                error(f"{where}: {name} must be [min, max]")
            elif not (low <= value[0] <= value[1] <= high):
                error(f"{where}: {name} = {value} must satisfy {low} <= min <= max <= {high}")
        elif not is_number(value):
            error(f"{where}: {name} must be a number")
        elif not low <= value <= high:
            error(f"{where}: {name} = {value} is outside {low} to {high}")


def check_friction(obj, where, prefix):
    if isinstance(obj, dict) and is_number(obj.get("friction_kinetic")) and is_number(obj.get("friction_static")) \
            and obj["friction_kinetic"] > obj["friction_static"]:
        error(f"{where}: {prefix}friction_kinetic is greater than {prefix}friction_static")


def load_json(path):
    try:
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError) as e:
        error(f"{path}: not readable JSON ({e})")
        return None


def check_surfaces(table):
    """Returns the set of valid surface indices."""
    indices, ids = set(), set()
    surfaces = table.get("surfaces") if isinstance(table, dict) else None
    if not isinstance(surfaces, list) or not surfaces:
        error("surfaces.json: needs a non-empty 'surfaces' list")
        return indices
    check_fields(table, {"soil_reference_diameter_m": (0.005, 0.1)}, "surfaces.json")
    for n, surface in enumerate(surfaces):
        sid = surface.get("id") if isinstance(surface, dict) else None
        where = f"surface '{sid}'" if isinstance(sid, str) else f"surface #{n}"
        if not isinstance(surface, dict):
            error(f"{where}: must be an object")
            continue
        if not isinstance(sid, str) or not re.fullmatch(r"[a-z][a-z0-9_]*", sid):
            error(f"{where}: id is missing or not lower_snake_case")
        elif sid in ids:
            error(f"{where}: id is used twice")
        ids.add(sid)
        index = surface.get("index")
        if not isinstance(index, int) or isinstance(index, bool) or not 1 <= index <= 255:
            error(f"{where}: index is missing or outside 1 to 255")
        elif index in indices:
            error(f"{where}: index {index} is used twice")
        else:
            indices.add(index)
        for group, spec in SURFACE_FIELDS.items():
            if not isinstance(surface.get(group), dict):
                error(f"{where}: {group} is missing")
                continue
            check_fields(surface[group], spec, where, group + ".")
        check_friction(surface.get("soil"), where, "soil.")
        relief = surface.get("micro_relief")
        if isinstance(relief, dict) and "ridges" in relief:
            check_fields(relief["ridges"], RIDGE_FIELDS, where, "micro_relief.ridges.")
        albedo = surface.get("material", {}).get("albedo_srgb") if isinstance(surface.get("material"), dict) else None
        if not isinstance(albedo, str) or not re.fullmatch(r"#[0-9a-fA-F]{6}", albedo):
            error(f"{where}: material.albedo_srgb is missing or not #rrggbb")
        check_fields(surface, {"dust_emission": (0, 1)}, where)
        cover = surface.get("cover")
        if not isinstance(cover, list):
            error(f"{where}: cover is missing (use [] for none)")
            continue
        seen = set()
        for entry in cover:
            kind = entry.get("type") if isinstance(entry, dict) else None
            if kind not in COVER_TYPES:
                error(f"{where}: cover type {kind!r} is not one of {', '.join(COVER_TYPES)}")
                continue
            if kind in seen:
                error(f"{where}: cover type '{kind}' is listed twice")
            seen.add(kind)
            check_fields(entry, COVER_FIELDS, where, f"cover.{kind}.")
            if is_number(entry.get("stems_per_m2")) and entry["stems_per_m2"] > 0:
                for field, what in (("height_m", "length"), ("diameter_m", "diameter")):
                    value = entry.get(field)
                    if isinstance(value, list) and value and is_number(value[0]) and value[0] <= 0:
                        error(f"{where}: cover.{kind}.{field} min must be above 0: a cover with elements needs an "
                              f"element {what}")
            if "mat" in entry:
                check_fields(entry["mat"], MAT_FIELDS, where, f"cover.{kind}.mat.")
                check_friction(entry["mat"], where, f"cover.{kind}.mat.")
    return indices


def known(material, materials):
    return isinstance(material, str) and material in materials


def check_materials(catalog):
    """Returns the set of material ids."""
    materials = catalog.get("materials") if isinstance(catalog, dict) else None
    if not isinstance(materials, dict) or not materials:
        error("catalog.json: needs a non-empty 'materials' object")
        return set()
    for mid, material in materials.items():
        where = f"material '{mid}'"
        check_fields(material, MATERIAL_FIELDS, where)
        if isinstance(material, dict):
            check_fields(material, {f: r for f, r in MATERIAL_OPTIONAL.items() if f in material}, where)
            check_friction(material, where, "")
    return set(materials)


def check_shapes(where, label, shapes, wire, materials):
    """Collision shapes (primitives, or one capsule_chain for a wire) or wind-volume shapes (primitives only)."""
    for shape in shapes:
        kind = shape.get("shape") if isinstance(shape, dict) else None
        allowed = [k for k in SHAPE_FIELDS if label == "collision" or k != "capsule_chain"]
        if kind not in allowed:
            error(f"{where}: {label} shape {kind!r} is not one of {', '.join(allowed)}")
            continue
        if label == "collision" and (kind == "capsule_chain") != wire:
            error(f"{where}: wires collide as one capsule_chain, other assets never do")
        for field in SHAPE_FIELDS[kind]:
            value = shape.get(field)
            ok = all(is_number(v) and 0 < v <= 1000 for v in value) if field == "size_m" and is_vec3(value) \
                else is_number(value) and 0 < value <= 1000
            if not ok:
                error(f"{where}: {label} {kind} {field} is missing or not a positive size in metres")
        for field in ("position_m", "rotation_deg"):
            if field in shape and not is_vec3(shape[field]):
                error(f"{where}: {label} {kind} {field} must be [x, y, z]")
        if label == "collision" and "material" in shape and not known(shape["material"], materials):
            error(f"{where}: {kind} shape material {shape['material']!r} is not in the material table")


def check_catalog(catalog):
    """Returns {asset id: type} for the assets that are usable."""
    assets = catalog.get("assets") if isinstance(catalog, dict) else None
    if not isinstance(assets, dict):
        error("catalog.json: needs an 'assets' object")
        return {}
    materials = check_materials(catalog)
    usable = {}
    for aid, asset in assets.items():
        where = f"asset '{aid}'"
        if not isinstance(asset, dict) or asset.get("type") not in ("object", "wire"):
            error(f"{where}: type must be 'object' or 'wire'")
            continue
        scene = asset.get("scene")
        if not isinstance(scene, str) or not scene.startswith("res://"):
            error(f"{where}: scene must be a res:// path")
        elif not os.path.isfile(os.path.join(GAME, scene[len("res://"):])):
            error(f"{where}: scene {scene} does not exist")
        visual_only = asset.get("visual_only", False)
        if not isinstance(visual_only, bool):
            error(f"{where}: visual_only must be true or false")
        shapes = asset.get("collision", [])
        if not isinstance(shapes, list) or (not visual_only and not shapes):
            error(f"{where}: collision is required unless the asset is visual_only")
            shapes = []
        check_shapes(where, "collision", shapes, asset["type"] == "wire", materials)
        if shapes and "material" not in asset:
            error(f"{where}: material is required because the asset has collision")
        elif shapes and not known(asset["material"], materials):
            error(f"{where}: material {asset['material']!r} is not in the material table")
        if "wind_volume" in asset:
            volume = asset["wind_volume"]
            if not isinstance(volume, list) or not volume:
                error(f"{where}: wind_volume must be a non-empty list of shapes")
            else:
                check_shapes(where, "wind_volume", volume, False, materials)
        if not isinstance(asset.get("snag_hazard"), bool):
            error(f"{where}: snag_hazard must be true or false")
        check_fields(asset, {"wind_porosity": (0, 1)}, where)
        gaps = asset.get("gaps")
        if not isinstance(gaps, list):
            error(f"{where}: gaps must be a list (use [] for none)")
            gaps = []
        for gap in gaps:
            if not (isinstance(gap, dict) and isinstance(gap.get("name"), str) and is_vec3(gap.get("center_m"))
                    and all(is_number(gap.get(f)) and gap[f] > 0 for f in ("width_m", "height_m"))
                    and is_number(gap.get("yaw_deg"))):
                error(f"{where}: each gap needs name, center_m, width_m > 0, height_m > 0 and yaw_deg")
        usable[aid] = asset["type"]
    return usable


def check_manifest(manifest):
    """Returns (size_m, height samples per side, surface cells per side), or None if unusable."""
    if not isinstance(manifest, dict):
        error("map.json: must be an object")
        return None
    version = manifest.get("format_version")
    match = re.fullmatch(r"(\d+)\.(\d+)", version) if isinstance(version, str) else None
    if not match:
        error(f"map.json: format_version {version!r} is not 'major.minor'")
        return None
    if int(match.group(1)) != SUPPORTED_MAJOR:
        error(f"map.json: format_version {version} is not supported; this validator reads {SUPPORTED_MAJOR}.x")
        return None
    size = manifest.get("size_m")
    if not is_number(size) or not 0 < size <= MAX_SIZE_M or size % CHUNK_M:
        error(f"map.json: size_m {size!r} breaks the side rule: a multiple of {CHUNK_M} m (the collision chunk) "
              f"and at most {MAX_SIZE_M} m")
        return None
    seed = manifest.get("seed")
    if not isinstance(seed, int) or isinstance(seed, bool) or not 0 <= seed <= 0xFFFFFFFF:
        error("map.json: seed must be a 32-bit unsigned integer")
    counts = []
    for layer, count, extra in (("height", "samples_per_side", 1), ("surface", "cells_per_side", 0)):
        section = manifest.get(layer)
        resolution = section.get("resolution_m") if isinstance(section, dict) else None
        if not is_number(resolution) or resolution <= 0:
            error(f"map.json: {layer}.resolution_m must be a positive number")
            return None
        cells = size / resolution
        if abs(cells - round(cells)) > 1e-9:
            error(f"map.json: size_m {size} is not a whole number of {layer} steps of {resolution} m")
            return None
        expected = round(cells) + extra
        if section.get(count) != expected:
            error(f"map.json: {layer}.{count} is {section.get(count)!r}, expected {expected} "
                  f"(size_m / resolution_m{' + 1' if extra else ''})")
        counts.append(expected)
    height = manifest["height"]
    if not is_number(height.get("offset_m")):
        error("map.json: height.offset_m must be a number")
    if not is_number(height.get("scale_m")) or height["scale_m"] <= 0:
        error("map.json: height.scale_m must be a positive number")
    return size, counts[0], counts[1]


def check_layers(folder, size, samples, cells, surface_indices):
    actual = os.path.getsize(os.path.join(folder, "height.r16"))
    if actual != samples * samples * 2:
        error(f"height.r16 is {actual} bytes, expected {samples * samples * 2} ({samples}² samples × 2 bytes)")

    for name, color_type, kind in (("surface.png", GREY, "8-bit greyscale"), ("cover.png", RGBA, "8-bit RGBA")):
        try:
            png = read_png(os.path.join(folder, name))
        except PngError as e:
            error(f"{name}: {e}")
            continue
        scan_png_text(name, png.ancillary)
        if png.color_type != color_type or png.bit_depth != 8 or png.interlace != 0:
            error(f"{name} must be {kind}, non-interlaced")
            continue
        if (png.width, png.height) != (cells, cells):
            error(f"{name} is {png.width}×{png.height} pixels, expected {cells}×{cells}")
            continue
        try:
            if name == "cover.png":
                if len(png.raw) != cells * (1 + 4 * cells):
                    raise PngError(f"image data is {len(png.raw)} bytes, expected {cells * (1 + 4 * cells)}")
                continue
            valid = bytes(sorted(surface_indices))
            reported = set()
            resolution = size / cells
            for r, row in enumerate(scanlines(png)):
                if not row.translate(None, valid):
                    continue
                for c, value in enumerate(row):
                    if value not in surface_indices and value not in reported:
                        reported.add(value)
                        x, z = -size / 2 + (c + 0.5) * resolution, -size / 2 + (r + 0.5) * resolution
                        why = " (0 is invalid)" if value == 0 else ""
                        error(f"surface.png: unknown surface index {value}{why}, first at row {r}, column {c} "
                              f"(world x={x:g}, z={z:g})")
        except PngError as e:
            error(f"{name}: {e}")


def check_objects(objects, assets, size):
    items = objects.get("objects") if isinstance(objects, dict) else None
    if not isinstance(items, list):
        error("objects.json: needs an 'objects' list")
        return
    half = size / 2
    for i, obj in enumerate(items):
        aid = obj.get("asset") if isinstance(obj, dict) else None
        if aid not in assets:
            error(f"object {i}: unknown asset {aid!r}")
            continue
        where = f"object {i} ({aid})"
        if assets[aid] == "wire":
            points = obj.get("points_m")
            if not (isinstance(points, list) and len(points) >= 2 and all(is_vec3(p) for p in points)):
                error(f"{where}: points_m needs at least 2 [x, y, z] points")
                points = []
            if not is_number(obj.get("sag_m")) or obj["sag_m"] < 0:
                error(f"{where}: sag_m must be a number >= 0")
            if not is_number(obj.get("diameter_m")) or obj["diameter_m"] <= 0:
                error(f"{where}: diameter_m must be a number > 0")
        else:
            points = [obj.get("position_m")] if is_vec3(obj.get("position_m")) else []
            if not points:
                error(f"{where}: position_m must be [x, y, z]")
            if not is_vec3(obj.get("rotation_deg")):
                error(f"{where}: rotation_deg must be [yaw, pitch, roll]")
            if not is_number(obj.get("scale")) or obj["scale"] <= 0:
                error(f"{where}: scale must be a number > 0")
        for x, _, z in points:
            if not (-half <= x <= half and -half <= z <= half):
                error(f"{where}: x={x:g}, z={z:g} is outside the map (x and z within ±{half:g} m)")
                break


def scan_text(where, value, blocklist):
    found = blocklist.search(value) or COORDINATE_PAIR.search(value) or MGRS.search(value)
    if found:
        error(f"{where}: text {found.group(0)!r} looks like a real-world place or coordinate")


def scan_json(where, node, blocklist, path=""):
    if isinstance(node, dict):
        for key, value in node.items():
            parts = re.findall(r"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+", key)
            if GEO_KEYS & {p.lower() for p in parts}:
                error(f"{where}: field '{path}{key}' is a georeference field")
            scan_text(where, key, blocklist)
            scan_json(where, value, blocklist, f"{path}{key}.")
    elif isinstance(node, list):
        for item in node:
            scan_json(where, item, blocklist, path)
    elif isinstance(node, str):
        scan_text(where, node, blocklist)


def scan_png_text(name, chunks):
    for kind, body in chunks:
        if kind == "eXIf":
            error(f"{name}: has EXIF metadata, which can hold a GPS position; save it without metadata")
        elif kind in ("tEXt", "zTXt", "iTXt"):
            keyword, _, rest = body.partition(b"\0")
            try:
                if kind == "zTXt":
                    rest = zlib.decompress(rest[1:])
                elif kind == "iTXt":
                    compressed, rest = rest[0], rest[2:].split(b"\0", 2)[-1]
                    rest = zlib.decompress(rest) if compressed else rest
            except (zlib.error, IndexError):
                error(f"{name}: {kind} metadata chunk is corrupt")
                continue
            text = keyword.decode("latin-1") + " " + rest.decode("utf-8", "replace")
            scan_json(f"{name} {kind} metadata", {keyword.decode("latin-1"): text}, BLOCKLIST)


def load_blocklist():
    """Names match at the start of a word, so case endings and adjectives (Києва, Kyivska) are caught too."""
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "place_blocklist.txt"), encoding="utf-8") as f:
        names = [line.strip() for line in f if line.strip() and not line.startswith("#")]
    return re.compile(r"(?<!\w)(?:" + "|".join(map(re.escape, names)) + ")", re.IGNORECASE)


BLOCKLIST = load_blocklist()


def main():
    parser = argparse.ArgumentParser(description="Validate a map package against map format 1.0.")
    parser.add_argument("package")
    parser.add_argument("--surfaces", default=os.path.join(GAME, "maps", "surfaces.json"))
    parser.add_argument("--catalog", default=os.path.join(GAME, "assets", "catalog.json"))
    args = parser.parse_args()
    folder = args.package
    sys.stdout.reconfigure(encoding="utf-8")  # messages can quote Cyrillic; Windows pipes default to cp1252

    missing = [f for f in FILES if not os.path.isfile(os.path.join(folder, f))]
    for name in missing:
        error(f"missing layer: {os.path.join(folder, name)}")
    if not missing:
        for name in os.listdir(folder):
            if name.lower().endswith(GIS_SIDECARS):
                error(f"{name}: GIS georeference sidecar files are not allowed in a map package")
        documents = {}
        for label, path in (("surfaces.json", args.surfaces), ("catalog.json", args.catalog),
                            ("map.json", os.path.join(folder, "map.json")),
                            ("objects.json", os.path.join(folder, "objects.json"))):
            documents[label] = load_json(path)
            if documents[label] is not None:
                scan_json(label, documents[label], BLOCKLIST)
        indices = check_surfaces(documents["surfaces.json"]) if documents["surfaces.json"] is not None else set()
        assets = check_catalog(documents["catalog.json"]) if documents["catalog.json"] is not None else {}
        grid = check_manifest(documents["map.json"]) if documents["map.json"] is not None else None
        if grid:
            check_layers(folder, grid[0], grid[1], grid[2], indices)
            if documents["objects.json"] is not None:
                check_objects(documents["objects.json"], assets, grid[0])

    if errors:
        print(f"FAILED: {len(errors)} error(s) in {folder}")
        sys.exit(1)
    print(f"OK: {folder} is a valid map package (format {documents['map.json']['format_version']}, {grid[0]:g} m)")


if __name__ == "__main__":
    main()
