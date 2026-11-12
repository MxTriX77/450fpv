"""Checks a map package: maps/<id>/ in the format fpv450-map-package (maps/farmland/README.md; w2-farmland-export).

    python tools/map/validate.py maps/farmland

Prints a summary and exits 0 when the package passes; prints every error and exits 1 otherwise. It checks:
- the manifest: format and version, the frame (unity-lh-yup), the map's id, no georeferencing field (SEC-03)
- the files: every one listed with its SHA-256, present, a real file and not a Git LFS pointer, and nothing unlisted
- the tiles: ids and rings, the map tile on map_rect_m, heightmaps of 2^n + 1 samples, samples within 0..32766, the
  seams between abutting tiles equal sample for sample, the ground classes known to assets-src/surfaces.json
- the grass: one plane per listed layer on the map's detail grid
- the stem field (w9-village-physics 2.1): each layer's stems per instance, kind and drawn heights from
  assets-src/vendor_plant_numbers.json, and its stems over the map against the plane's instances
- the placements: ids in order, known kinds and variants, every one on the map, unit rotations, counts as listed
- the outlines: the fields' polygons counter-clockwise and on the map, the village circle on the map and holding
  every ruin and debris piece
- the heights: every probe's Unity height (Terrain.SampleHeight) equals the package's triangulated height within 1 mm

The ground at a point (README): each tile is Unity's heightfield, a sample s is position_y + s / 32766 * size_y, and
each cell splits along its diagonal from its south-west to its north-east corner, as Unity's terrain samples it;
where tiles overlap the ground is the highest of them. Only the standard library: no numpy.
"""
import hashlib
import json
import math
import re
import sys
from array import array
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SURFACES = REPO / "assets-src" / "surfaces.json"
PLANT_NUMBERS = REPO / "assets-src" / "vendor_plant_numbers.json"  # w9-village-physics 2.8: stems per grass instance
# The stem field's kind per grass group (w9-village-physics 2.1; MapWorldStems.cs KindOf): the state the group draws.
STEM_KINDS = {"Green": "green_blade", "GreenClump": "green_blade", "TallGreen": "green_blade",
              "Dry": "dry_culm", "DryClump": "dry_culm", "TallDry": "dry_culm", "Burnt": "burnt_culm", "TallBurnt": "burnt_culm"}

FORMAT = "fpv450-map-package"
FORMAT_VERSION = 1
FRAME = "unity-lh-yup"
MAX_SAMPLE = 32766  # Unity's heightmap: a height is sample / 32766 of the tile's height
PROBE_TOLERANCE_M = 0.001  # the spec's 1 mm (assumed)
KINDS = ("tree", "bush", "ruin", "debris")
RINGS = ("map", "surround", "far")
UNLISTED_OK = {"map.json", "README.md"}
LFS_POINTER = b"version https://git-lfs"
GEO_KEY = re.compile(r"^(lat|lon|lng|latitude|longitude|epsg|crs|srid|utm|wgs84|geo\w*|proj\w*|georef\w*)$", re.IGNORECASE)


class Tile:
    """One terrain tile: its rectangle and its heights, rows north to south as the file holds them."""

    def __init__(self, row, samples):
        self.id = row["id"]
        self.ring = row["ring"]
        self.x0, self.y0, self.z0 = (float(v) for v in row["position_m"])
        self.sx, self.sy, self.sz = (float(v) for v in row["size_m"])
        self.res = int(row["heights"]["resolution"])
        self.samples = samples

    def contains(self, x, z):
        return self.x0 <= x <= self.x0 + self.sx and self.z0 <= z <= self.z0 + self.sz

    def sample(self, column, unity_row):
        """The sample at Unity's indices: column west to east, unity_row south to north."""
        return self.samples[(self.res - 1 - unity_row) * self.res + column]

    def height(self, x, z):
        """Unity's Terrain.SampleHeight plus the tile's y: the cell's triangle on its south-west to north-east diagonal."""
        n = self.res - 1
        fx = min(max((x - self.x0) / self.sx, 0.0), 1.0) * n
        fz = min(max((z - self.z0) / self.sz, 0.0), 1.0) * n
        cx, cz = min(int(fx), n - 1), min(int(fz), n - 1)
        u, v = fx - cx, fz - cz
        h00 = self.sample(cx, cz)
        h11 = self.sample(cx + 1, cz + 1)
        if u > v:
            h10 = self.sample(cx + 1, cz)
            h = h00 + (h10 - h00) * u + (h11 - h10) * v
        else:
            h01 = self.sample(cx, cz + 1)
            h = h00 + (h11 - h01) * u + (h01 - h00) * v
        return self.y0 + h / MAX_SAMPLE * self.sy


def ground_height(tiles, x, z):
    """The highest tile surface at (x, z), or None off every tile."""
    heights = [t.height(x, z) for t in tiles if t.contains(x, z)]
    return max(heights) if heights else None


def surface_ids():
    """surfaces.json's surface names, id i + 1 for row i (MapWorld.SurfaceIds)."""
    surfaces = json.loads(SURFACES.read_text(encoding="utf-8"))["surfaces"]
    return {name: i + 1 for i, name in enumerate(surfaces)}


def _georef_keys(node, where, errors):
    if isinstance(node, dict):
        for key, value in node.items():
            if GEO_KEY.match(key):
                errors.append(f"{where}: georeferencing field '{key}' (SEC-03)")
            _georef_keys(value, where, errors)
    elif isinstance(node, list):
        for value in node:
            _georef_keys(value, where, errors)


def _is_pow2_plus_1(n):
    return n >= 3 and (n - 1) & (n - 2) == 0


def _polygon_area(points):
    return 0.5 * sum(points[i - 1][0] * points[i][1] - points[i][0] * points[i - 1][1] for i in range(len(points)))


def _check_files(root, manifest, errors):
    """Every listed file present, real and matching its SHA-256; nothing unlisted. Returns the files' bytes."""
    listed = manifest.get("files", {})
    contents = {}
    for relative, digest in sorted(listed.items()):
        path = root / relative
        if not path.is_file():
            errors.append(f"{relative}: listed but missing")
            continue
        data = path.read_bytes()
        if data.startswith(LFS_POINTER):
            errors.append(f"{relative}: a Git LFS pointer, not the file (run git lfs checkout)")
            continue
        if hashlib.sha256(data).hexdigest() != digest:
            errors.append(f"{relative}: SHA-256 differs from map.json's")
        contents[relative] = data
    for path in sorted(p for p in root.rglob("*") if p.is_file()):
        relative = path.relative_to(root).as_posix()
        if relative not in listed and relative not in UNLISTED_OK:
            errors.append(f"{relative}: in the package but not listed in map.json")
    return contents


def _load_json(contents, relative, errors):
    if relative not in contents:
        errors.append(f"{relative}: not in the package's files")
        return None
    try:
        return json.loads(contents[relative].decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as e:
        errors.append(f"{relative}: not JSON ({e})")
        return None


def _check_header(document, relative, map_id, errors, kind=None):
    if document.get("format") != (kind or FORMAT) or document.get("format_version") != FORMAT_VERSION:
        errors.append(f"{relative}: format must be {kind or FORMAT} version {FORMAT_VERSION}")
    if document.get("frame") != FRAME:
        errors.append(f"{relative}: frame must be {FRAME}")
    if document.get("map") != map_id:
        errors.append(f"{relative}: map must be '{map_id}'")


def _check_tiles(manifest, contents, errors):
    tiles = []
    ids = set()
    for row in manifest.get("tiles", []):
        tid = row.get("id")
        if tid in ids:
            errors.append(f"tile {tid}: listed twice")
        ids.add(tid)
        if row.get("ring") not in RINGS:
            errors.append(f"tile {tid}: ring must be one of {RINGS}")
        if any(float(v) <= 0 for v in row["size_m"]):
            errors.append(f"tile {tid}: size_m must be positive")
        res = row["heights"]["resolution"]
        if not _is_pow2_plus_1(res):
            errors.append(f"tile {tid}: a heightmap resolution of {res}, not 2^n + 1")
        data = contents.get(row["heights"]["file"])
        if data is None:
            errors.append(f"tile {tid}: no heights file {row['heights']['file']}")
            continue
        if len(data) != res * res * 2:
            errors.append(f"tile {tid}: {len(data)} height bytes for {res} x {res} samples")
            continue
        samples = array("H")
        samples.frombytes(data)
        if sys.byteorder == "big":
            samples.byteswap()
        if max(samples) > MAX_SAMPLE:
            errors.append(f"tile {tid}: a sample above {MAX_SAMPLE}")
        if min(samples) != row["heights"]["min_sample"] or max(samples) != row["heights"]["max_sample"]:
            errors.append(f"tile {tid}: min_sample and max_sample differ from the file")
        tiles.append(Tile(row, samples))
    if [t.ring for t in tiles].count("map") != 1:
        errors.append("exactly one tile must be the map")
    return tiles


def _check_seams(tiles, errors):
    """Abutting tiles of equal size and resolution share their edge samples."""
    seams = 0
    for a in tiles:
        for b in tiles:
            if a is b or (a.res, a.sx, a.sz) != (b.res, b.sx, b.sz):
                continue
            n = a.res
            if a.x0 + a.sx == b.x0 and a.z0 == b.z0:  # b east of a
                edge_a = [a.sample(n - 1, r) for r in range(n)]
                edge_b = [b.sample(0, r) for r in range(n)]
            elif a.z0 + a.sz == b.z0 and a.x0 == b.x0:  # b north of a
                edge_a = [a.sample(c, n - 1) for c in range(n)]
                edge_b = [b.sample(c, 0) for c in range(n)]
            else:
                continue
            seams += 1
            if edge_a != edge_b:
                worst = max(abs(p - q) for p, q in zip(edge_a, edge_b)) / MAX_SAMPLE * a.sy
                errors.append(f"seam {a.id} | {b.id}: edge samples differ, up to {worst * 1000:.2f} mm")
    return seams


def _check_classes(manifest, contents, errors):
    table = manifest.get("ground_classes", {}).get("layers", [])
    known = surface_ids()
    allowed = set()
    for entry in table:
        name, sid = entry.get("surface"), entry.get("id")
        if known.get(name) != sid:
            errors.append(f"ground class of layer {entry.get('layer')}: '{name}' is not surface id {sid} in assets-src/surfaces.json")
        allowed.add(sid)
    for row in manifest.get("tiles", []):
        res = row["classes"]["resolution"]
        data = contents.get(row["classes"]["file"])
        if data is None:
            errors.append(f"tile {row['id']}: no classes file {row['classes']['file']}")
            continue
        if len(data) != res * res:
            errors.append(f"tile {row['id']}: {len(data)} class bytes for {res} x {res} samples")
        unknown = set(data) - allowed
        if unknown:
            errors.append(f"tile {row['id']}: class ids {sorted(unknown)} are not in the ground class table")


def _check_grass(manifest, contents, errors):
    grass = manifest.get("grass", {})
    tile_ids = {row["id"] for row in manifest.get("tiles", [])}
    if grass.get("tile") not in tile_ids:
        errors.append(f"grass: tile '{grass.get('tile')}' is not a tile")
    layers, res = grass.get("layers", []), grass.get("resolution", 0)
    if [entry.get("layer") for entry in layers] != list(range(len(layers))):
        errors.append("grass: layers must be numbered 0, 1, 2 ... in order")
    data = contents.get(grass.get("file"))
    if data is None:
        errors.append(f"grass: no file {grass.get('file')}")
    elif len(data) != len(layers) * res * res:
        errors.append(f"grass: {len(data)} bytes for {len(layers)} layers of {res} x {res}")
    else:
        plane = res * res
        for i, entry in enumerate(layers):
            if sum(data[i * plane:(i + 1) * plane]) != entry.get("instances"):
                errors.append(f"grass layer {i}: the plane's instance count differs from map.json's")
    return sum(entry.get("instances", 0) for entry in layers)


def _check_stems(manifest, contents, errors, numbers_path=PLANT_NUMBERS):
    """The stem field (w9-village-physics 2.1; MapWorldStems.cs) against the export's counts and the plants' numbers.

    Per layer: assets-src/vendor_plant_numbers.json names its prefab with the same layer row, its group has a stem kind,
    its stems per instance is the prefab profile's most, and its drawn heights are mesh_y_m[1] x height_scale (within
    1 mm of the numbers' rounding). The field is count x stems per instance / the cell's area per cell and layer, so each
    layer's stems over the map are the plane's instances x its stems per instance. Returns (stems by kind, the densest
    cell's stems per m2 and its centre), or None when the package has no grass.
    """
    grass = manifest.get("grass")
    data = contents.get((grass or {}).get("file"))
    if not grass or data is None:
        return None
    numbers = json.loads(Path(numbers_path).read_text(encoding="utf-8"))["grass"]
    rows = {row["layer"]: row for row in numbers.get("layers", [])}
    rect = manifest["map_rect_m"]
    res = grass["resolution"]
    plane, side = res * res, (rect[2] - rect[0]) / res
    per_cell = [0.0] * plane
    by_kind = {}
    for i, entry in enumerate(grass["layers"]):
        prefab, group = entry.get("prefab"), entry.get("group")
        kind = STEM_KINDS.get(group)
        if kind is None:
            errors.append(f"stems layer {i}: group '{group}' has no stem kind")
            continue
        p = numbers["prefabs"].get(prefab)
        row = rows.get(i)
        if p is None or row is None or row.get("prefab") != prefab or row.get("group") != group:
            errors.append(f"stems layer {i}: the plants' numbers have no layer {i} of {prefab} ({group})")
            continue
        stems = p["stems"]
        if stems != max(s["stems"] for s in p["profile"]) or row.get("stems_per_instance") != stems:
            errors.append(f"stems layer {i}: {prefab}'s stems per instance is not its profile's most")
            continue
        top, scale = entry.get("mesh_y_m", [0, 0])[1], entry.get("height_scale", [0, 0])
        if any(abs(top * s - h) > 0.001 for s, h in zip(scale, row.get("height_m", []))) or len(scale) != 2:
            errors.append(f"stems layer {i}: drawn heights {[top * s for s in scale]} m are not the plants' numbers' {row.get('height_m')}")
        counted = 0
        for cell, count in enumerate(data[i * plane:(i + 1) * plane]):
            if count:
                counted += count * stems
                per_cell[cell] += count * stems / (side * side)
        if counted != entry.get("instances", 0) * stems:
            errors.append(f"stems layer {i}: the field holds {counted} stems, not instances x stems per instance")
        by_kind[kind] = by_kind.get(kind, 0) + counted
    densest = max(range(plane), key=per_cell.__getitem__)
    row, column = divmod(densest, res)
    centre = (rect[0] + (column + 0.5) * side, rect[3] - (row + 0.5) * side)
    return by_kind, per_cell[densest], centre


def _inside(rect, x, z):
    return rect[0] <= x <= rect[2] and rect[1] <= z <= rect[3]


def _check_placements(placements, manifest, rect, errors):
    variants = {}
    for v in placements.get("variants", []):
        if v.get("kind") not in KINDS:
            errors.append(f"variant {v.get('id')}: kind must be one of {KINDS}")
        if v.get("id") in variants:
            errors.append(f"variant {v.get('id')}: listed twice")
        variants[v.get("id")] = v.get("kind")
    counts = dict.fromkeys(KINDS, 0)
    rows = placements.get("placements", [])
    for i, row in enumerate(rows):
        what = f"placement {row.get('id')}"
        if row.get("id") != i:
            errors.append(f"{what}: ids must run 0, 1, 2 ... in order (expected {i})")
        kind = row.get("kind")
        if kind not in KINDS or variants.get(row.get("variant")) != kind:
            errors.append(f"{what}: {kind} {row.get('variant')} is not a listed variant of that kind")
        else:
            counts[kind] += 1
        values = list(row.get("position_m", [])) + list(row.get("rotation_xyzw", [])) + list(row.get("scale", []))
        if len(values) != 10 or not all(isinstance(v, (int, float)) and math.isfinite(v) for v in values):
            errors.append(f"{what}: position_m, rotation_xyzw and scale must be 3, 4 and 3 finite numbers")
            continue
        x, _, z = row["position_m"]
        if not _inside(rect, x, z):
            errors.append(f"{what}: ({x}, {z}) lies outside the map")
        if abs(math.sqrt(sum(q * q for q in row["rotation_xyzw"])) - 1.0) > 1e-5:
            errors.append(f"{what}: rotation_xyzw is not a unit quaternion")
        if any(s == 0 for s in row["scale"]):
            errors.append(f"{what}: a zero scale")
    for kind in KINDS:
        if counts[kind] != manifest.get("counts", {}).get(kind):
            errors.append(f"placements: {counts[kind]} {kind} rows, map.json counts {manifest.get('counts', {}).get(kind)}")
    return rows


def _check_outlines(outlines, rect, rows, errors):
    names = set()
    for field in outlines.get("fields", []):
        name = field.get("name")
        if name in names:
            errors.append(f"field {name}: listed twice")
        names.add(name)
        points = field.get("outline_m", [])
        if len(points) < 3:
            errors.append(f"field {name}: fewer than 3 outline points")
            continue
        if _polygon_area(points) <= 0:
            errors.append(f"field {name}: the outline must run counter-clockwise seen from above")
        if not all(_inside(rect, x, z) for x, z in points):
            errors.append(f"field {name}: an outline point lies outside the map")
        if not 0 <= field.get("overgrowth", -1) <= 1 or field.get("furrow_axis") not in ("X", "Z"):
            errors.append(f"field {name}: overgrowth must be 0..1 and furrow_axis X or Z")
    village = outlines.get("village", {})
    (cx, cz), r = village.get("centre_m", (math.nan, math.nan)), village.get("radius_m", 0)
    if village.get("shape") != "circle" or not r > 0 or not _inside((rect[0] + r, rect[1] + r, rect[2] - r, rect[3] - r), cx, cz):
        errors.append("village: must be a circle lying on the map")
    for row in rows:
        if row.get("kind") in ("ruin", "debris") and "position_m" in row:
            x, _, z = row["position_m"]
            if math.hypot(x - cx, z - cz) > r:
                errors.append(f"placement {row['id']}: a {row['kind']} outside the village circle")
    return len(names)


def _check_probes(probes, tiles, errors):
    worst = 0.0
    rows = probes.get("probes", [])
    for i, probe in enumerate(rows):
        ground = ground_height(tiles, probe["x"], probe["z"])
        if ground is None:
            errors.append(f"probe {i}: ({probe['x']}, {probe['z']}) lies on no tile")
            continue
        miss = abs(ground - probe["y"])
        worst = max(worst, miss)
        if miss > PROBE_TOLERANCE_M:
            errors.append(f"probe {i} ({probe['ring']}): the package's {ground:.6f} m is {miss * 1000:.3f} mm from Unity's {probe['y']} m")
    return len(rows), worst


def validate(root):
    """All the package's errors (an empty list when it passes) and a one-line summary."""
    root = Path(root)
    errors = []
    manifest_path = root / "map.json"
    if not manifest_path.is_file():
        return [f"{root}: no map.json"], ""
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    map_id = root.name
    _check_header(manifest, "map.json", map_id, errors)
    contents = _check_files(root, manifest, errors)
    documents = {"map.json": manifest}
    for relative in ("placements.json", "outlines.json", "height_probes.json"):
        documents[relative] = _load_json(contents, relative, errors)
    if any(d is None for d in documents.values()):
        return errors, ""
    _check_header(documents["placements.json"], "placements.json", map_id, errors, "fpv450-map-placements")
    _check_header(documents["outlines.json"], "outlines.json", map_id, errors, "fpv450-map-outlines")
    _check_header(documents["height_probes.json"], "height_probes.json", map_id, errors, "fpv450-map-height-probes")
    for relative, document in documents.items():
        _georef_keys(document, relative, errors)

    rect = [float(v) for v in manifest.get("map_rect_m", [])]
    tiles = _check_tiles(manifest, contents, errors)
    maps = [t for t in tiles if t.ring == "map"]
    if len(rect) != 4 or (maps and [maps[0].x0, maps[0].z0, maps[0].x0 + maps[0].sx, maps[0].z0 + maps[0].sz] != rect):
        errors.append("map_rect_m must be the map tile's rectangle [xmin, zmin, xmax, zmax]")
        return errors, ""
    seams = _check_seams(tiles, errors)
    _check_classes(manifest, contents, errors)
    grass = _check_grass(manifest, contents, errors)
    stems = _check_stems(manifest, contents, errors) if not errors else None
    rows = _check_placements(documents["placements.json"], manifest, rect, errors)
    fields = _check_outlines(documents["outlines.json"], rect, rows, errors)
    probes, worst = _check_probes(documents["height_probes.json"], tiles, errors)
    counts = ", ".join(f"{manifest['counts'].get(k)} {k}" for k in KINDS)
    summary = (f"{root.as_posix()}: {len(tiles)} tiles ({seams} seams), {counts}, {grass} grass instances, {fields} fields, "
               f"{probes} height probes within {worst * 1000:.4f} mm of Unity")
    if stems:
        by_kind, densest, (x, z) = stems
        summary += (f"; stems {', '.join(f'{k} {n:,}' for k, n in sorted(by_kind.items()))}, "
                    f"the densest cell {densest:.0f} /m2 at ({x:.3f}, {z:.3f})")
    return errors, summary


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    if len(argv) != 1:
        print(__doc__.split("\n\n")[1])
        return 2
    errors, summary = validate(argv[0])
    for error in errors:
        print(f"ERROR {error}")
    if errors:
        print(f"FAILED {argv[0]}: {len(errors)} errors")
        return 1
    print(f"OK {summary}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
