"""Asset metadata, schema 1 (design decision 6): assembly from a generator's result, validation, a deterministic
writer, and the check against the exported FBX. Every number is in asset space (lib.frame) and metres."""
import json

from . import catalog, fbxread

SCHEMA = 1
UNITS = "m"
FRAME = "unity-lh-yup"
DECIMALS = 6  # a micrometre: well under any tolerance, and it keeps float noise out of the file
BOUNDS_TOLERANCE_M = 0.001
# Fields per collision or wind-volume shape, besides "shape".
SHAPES = {"box": ("center", "size", "rotation"), "sphere": ("center", "radius"),
          "capsule": ("center", "radius", "height", "rotation"), "cylinder": ("center", "radius", "height", "rotation")}
KEY_ORDER = ("schema", "id", "variant", "generator", "params_sha256", "units", "frame", "bounds", "lods",
             "cull_distance_m", "materials", "collision", "gaps", "wind_volumes", "snag")


def assemble(asset_id, variant, generator, params, built, visual_by_slot):
    """The metadata of one variant from the generator's result `built` ({"lods": [Mesh...], "switch_distances_m",
    "cull_distance_m", "materials": [{"slot", "physics"}], "collision", "gaps", "wind_volumes", "snag"}) and the
    catalog's visual material per slot."""
    lods = built["lods"]
    switches = built["switch_distances_m"]
    if len(switches) != len(lods):
        raise ValueError(f"{variant}: {len(lods)} LODs but {len(switches)} switch distances")
    lo = [min(m.bounds()[0][a] for m in lods) for a in range(3)]
    hi = [max(m.bounds()[1][a] for m in lods) for a in range(3)]
    materials = []
    for m in built["materials"]:
        if m["slot"] not in visual_by_slot:
            raise ValueError(f"{variant}: the catalog names no visual material for slot '{m['slot']}'")
        materials.append({"slot": m["slot"], "visual": visual_by_slot[m["slot"]], "physics": m["physics"]})
    meta = {
        "schema": SCHEMA, "id": asset_id, "variant": variant, "generator": generator,
        "params_sha256": catalog.params_sha256(params), "units": UNITS, "frame": FRAME,
        "bounds": {"min": lo, "max": hi},
        "lods": [{"mesh": f"{variant}_LOD{n}", "tris": mesh.triangles(), "switch_distance_m": float(switch)}
                 for n, (mesh, switch) in enumerate(zip(lods, switches))],
        "cull_distance_m": float(built["cull_distance_m"]),
        "materials": materials,
        "collision": built["collision"], "gaps": built["gaps"], "wind_volumes": built["wind_volumes"],
        "snag": built["snag"],
    }
    return _rounded(meta)


def validate(meta):
    """Problems with a metadata dict against schema 1, as strings; empty when it conforms."""
    problems = []
    missing = [k for k in KEY_ORDER if k not in meta]
    if missing:
        return [f"missing keys: {', '.join(missing)}"]
    if meta["schema"] != SCHEMA:
        problems.append(f"schema is {meta['schema']!r}, expected {SCHEMA}")
    if meta["units"] != UNITS or meta["frame"] != FRAME:
        problems.append(f"units/frame are {meta['units']!r}/{meta['frame']!r}, expected {UNITS!r}/{FRAME!r}")
    if not _is_hex(meta["params_sha256"], 64):
        problems.append("params_sha256 is not a 64-digit hex string")
    b = meta["bounds"]
    if not (_is_vec(b.get("min")) and _is_vec(b.get("max"))) or any(b["min"][a] > b["max"][a] for a in range(3)):
        problems.append("bounds need min <= max as two 3-vectors")
    lods = meta["lods"]
    if not lods or any(not (isinstance(l.get("mesh"), str) and isinstance(l.get("tris"), int)
                            and _is_num(l.get("switch_distance_m"))) for l in lods):
        problems.append("lods need at least one entry with mesh, tris and switch_distance_m")
    else:
        switches = [l["switch_distance_m"] for l in lods]
        if switches[0] != 0 or switches != sorted(switches) or len(set(switches)) != len(switches):
            problems.append(f"LOD switch distances must start at 0 and increase, got {switches}")
        if not (_is_num(meta["cull_distance_m"]) and meta["cull_distance_m"] > switches[-1]):
            problems.append("cull_distance_m must lie beyond the last LOD switch")
    for m in meta["materials"]:
        if not all(isinstance(m.get(k), str) and m[k] for k in ("slot", "visual", "physics")):
            problems.append(f"material entry needs slot, visual and physics: {m}")
    for i, c in enumerate(meta["collision"]):
        problems += [f"collision[{i}]: {p}" for p in _shape_problems(c)]
        if not isinstance(c.get("material"), str) or not isinstance(c.get("landable"), bool):
            problems.append(f"collision[{i}]: needs a material id and a boolean landable")
    for i, g in enumerate(meta["gaps"]):
        polygon = g.get("polygon")
        if not (isinstance(g.get("id"), str) and isinstance(polygon, list) and len(polygon) >= 3
                and all(_is_vec(p) for p in polygon) and _is_vec(g.get("normal")) and _is_num(g.get("narrowest_m"))):
            problems.append(f"gaps[{i}]: needs id, a planar polygon of 3+ points, normal and narrowest_m")
    for i, w in enumerate(meta["wind_volumes"]):
        problems += [f"wind_volumes[{i}]: {p}" for p in _shape_problems(w)]
        if not (_is_num(w.get("porosity")) and 0 <= w["porosity"] <= 1):
            problems.append(f"wind_volumes[{i}]: porosity must be in [0, 1]")
    for i, s in enumerate(meta["snag"]):
        if not (isinstance(s.get("kind"), str) and isinstance(s.get("points"), list) and len(s["points"]) >= 1
                and all(_is_vec(p) for p in s["points"]) and _is_num(s.get("radius_m"))):
            problems.append(f"snag[{i}]: needs kind, points and radius_m")
    return problems


def check_against_fbx(meta, fbx_path):
    """Problems where the metadata disagrees with the exported file as Unity will read it: a LOD mesh missing from
    the FBX, or the union of the LOD bounds off the metadata bounds by more than a millimetre."""
    found = fbxread.unity_bounds(fbx_path)
    problems = []
    lo, hi = [None] * 3, [None] * 3
    for lod in meta["lods"]:
        if lod["mesh"] not in found:
            problems.append(f"{lod['mesh']} is not a mesh of {fbx_path}; it holds {', '.join(sorted(found))}")
            continue
        mesh_lo, mesh_hi = found[lod["mesh"]]
        lo = [mesh_lo[a] if lo[a] is None else min(lo[a], mesh_lo[a]) for a in range(3)]
        hi = [mesh_hi[a] if hi[a] is None else max(hi[a], mesh_hi[a]) for a in range(3)]
    if not problems:
        for name, got, want in (("min", lo, meta["bounds"]["min"]), ("max", hi, meta["bounds"]["max"])):
            if any(abs(got[a] - want[a]) > BOUNDS_TOLERANCE_M for a in range(3)):
                problems.append(f"bounds {name}: the FBX gives {[round(v, 4) for v in got]}, the metadata {want}")
    return problems


def dumps(meta):
    """The file text: schema key order, 2-space indent, LF, one trailing newline; identical for identical input."""
    ordered = {k: meta[k] for k in KEY_ORDER}
    return json.dumps(_rounded(ordered), indent=2, ensure_ascii=False) + "\n"


def write(path, meta):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(dumps(meta))


def _rounded(value):
    if isinstance(value, bool):
        return value
    if isinstance(value, float):
        r = round(value, DECIMALS)
        return 0.0 if r == 0 else r
    if isinstance(value, dict):
        return {k: _rounded(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return [_rounded(v) for v in value]
    return value


def _shape_problems(shape):
    kind = shape.get("shape")
    if kind not in SHAPES:
        return [f"shape must be one of {', '.join(SHAPES)}, got {kind!r}"]
    problems = []
    for field in SHAPES[kind]:
        value = shape.get(field)
        ok = (_is_num(value) and value > 0) if field in ("radius", "height") else \
            _is_vec(value, 4) if field == "rotation" else _is_vec(value)
        if not ok:
            problems.append(f"{kind} needs a valid '{field}'")
    return problems


def _is_num(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool)


def _is_vec(v, n=3):
    return isinstance(v, list) and len(v) == n and all(_is_num(c) for c in v)


def _is_hex(s, n):
    return isinstance(s, str) and len(s) == n and all(c in "0123456789abcdef" for c in s)
