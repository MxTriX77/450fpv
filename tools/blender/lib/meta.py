"""Asset metadata, schema 1 (design decision 6): assembly from a generator's result, validation of the schema and
of its conventions (the physics review's R2 and R3), a deterministic writer, and the check against the exported
FBX. Every number is in asset space (lib.frame) and metres."""
import json
import math
import os

from . import catalog, fbxread

SCHEMA = 1
UNITS = "m"
FRAME = "unity-lh-yup"
DECIMALS = 6  # a micrometre: well under any tolerance, and it keeps float noise out of the file
BOUNDS_TOLERANCE_M = 0.001
# Fields per collision or wind-volume shape, besides "shape". Every shape is convex by construction; a concave
# object is a union of shapes. Capsules and cylinders run along local +Y before "rotation", a unit quaternion
# [x, y, z, w] applied about "center"; "height" is the total length, caps included.
SHAPES = {"box": ("center", "size", "rotation"), "sphere": ("center", "radius"),
          "capsule": ("center", "radius", "height", "rotation"), "cylinder": ("center", "radius", "height", "rotation")}
QUATERNION_TOLERANCE = 1e-6
NORMAL_TOLERANCE = 1e-6
PERPENDICULAR_TOLERANCE = 1e-3  # |edge . normal| / |edge|, per polygon edge
PLANAR_TOLERANCE_M = 0.010  # AST-05 gives +-2 cm around openings
SNAG_KINDS = ("edge", "hook")  # a polyline the fiber bends over; a free-ended member from base to tip
KEY_ORDER = ("schema", "id", "variant", "generator", "params_sha256", "units", "frame", "bounds", "lods", "micro",
             "cull_distance_m", "materials", "collision", "gaps", "wind_volumes", "ports", "snag")
# Named attachment points (p1-l01-l03-pole-wires, "Ports and wires in the metadata"): where a generator lets other
# generators and the map attach, a pole's insulators for the wires. Each: a unique id, a position and a unit
# direction. Schema 1 stays: the key is additive and always present, [] when none.
PORT_KEYS = ("id", "position", "direction")
# Visual-only micro-detail meshes (p1-s04-belt-straw, "Visual-only micro-detail meshes"): a generator may return,
# per LOD, meshes the importer renders with that LOD and nothing collides with (the baked straws, until the shared
# micro-detail generator places them at physics parity, AST-10). Listed under "micro" with their kind and count;
# the audit skips them and names them. Schema 1 stays: the key is additive and always present, [] when none.
MICRO_KEYS = ("lod", "name", "kind", "count", "tris")
# The closed, physics-owned list every physics material id must come from (review R3).
PHYSICS_MATERIALS = os.path.join("assets-src", "physics_materials.json")
LABELS = ("pilot", "measured", "published", "derived", "assumed")
# A shape that stands in for ground (a hole filler, a cut wall, a mound top) names a surface of the map's table
# instead, as "surface:<id>": exactly the thirteen S01–S13 names (knowledge §7.2, the earthworks sheet; the
# phase-3 compiler checks the same names against surfaces.json). A plain material id never shares one of them.
SURFACE_PREFIX = "surface:"
SURFACE_IDS = ("meadow_sod", "dry_crust", "crater_spoil", "belt_straw", "belt_bare", "tilled", "burnt_field",
               "weeds", "yard_litter", "rubble", "asphalt", "concrete_slab", "dirt_track")


def load_physics_materials(root):
    """The ids of assets-src/physics_materials.json, after checking that every row carries the fields and the
    label decision 6 asks for. Raises ValueError on a malformed table."""
    with open(os.path.join(root, PHYSICS_MATERIALS), encoding="utf-8") as f:
        table = json.load(f)
    rows = table.get("materials")
    if table.get("schema") != 1 or not isinstance(rows, dict) or not rows:
        raise ValueError(f"{PHYSICS_MATERIALS}: expected schema 1 with a non-empty 'materials' object")
    for material_id, row in rows.items():
        if material_id in SURFACE_IDS or ":" in material_id:
            raise ValueError(f"{PHYSICS_MATERIALS}: {material_id!r} is a surface id; a plain material id never "
                             f"shares a surface's name, and shapes name surfaces as {SURFACE_PREFIX}<id>")
        edge = row.get("edge_radius_m", 0)
        break_stress = row.get("break_stress_pa", 1)
        ok = (isinstance(row, dict)
              and all(_is_num(row.get(k)) and row[k] > 0 for k in ("friction_static", "friction_kinetic"))
              and "edge_radius_m" in row and (edge is None or (_is_num(edge) and edge > 0))
              and ("stiffness_n_per_m" in row) == ("damping_ratio" in row)
              and _is_num(break_stress) and break_stress > 0
              and row.get("label") in LABELS and isinstance(row.get("rationale"), str) and row["rationale"])
        if not ok:
            raise ValueError(f"{PHYSICS_MATERIALS}: {material_id} needs friction_static, friction_kinetic, "
                             "edge_radius_m (a number or null), stiffness_n_per_m with damping_ratio or neither, "
                             f"break_stress_pa positive if present, a label in {', '.join(LABELS)} and a rationale")
    return set(rows)


def assemble(asset_id, variant, generator, params, built, visual_by_slot):
    """The metadata of one variant from the generator's result `built` ({"lods": [Mesh...], "switch_distances_m",
    "cull_distance_m", "materials": [{"slot", "physics"}], "collision", "gaps", "wind_volumes", "snag"}, and
    optionally "micro": [{"lod", "kind", "count", "mesh": Mesh}] and "ports": [{"id", "position", "direction"}]) and
    the catalog's visual material per slot. A micro mesh is named <variant>_LOD<n>_<kind> and counts towards the
    bounds, since it is rendered."""
    lods = built["lods"]
    switches = built["switch_distances_m"]
    if len(switches) != len(lods):
        raise ValueError(f"{variant}: {len(lods)} LODs but {len(switches)} switch distances")
    micro = []
    for m in built.get("micro", []):
        if not 0 <= m["lod"] < len(lods):
            raise ValueError(f"{variant}: a micro mesh names LOD {m['lod']}, which does not exist")
        micro.append({"lod": m["lod"], "name": f"{variant}_LOD{m['lod']}_{m['kind']}", "kind": m["kind"],
                      "count": int(m["count"]), "tris": m["mesh"].triangles()})
    meshes = list(lods) + [m["mesh"] for m in built.get("micro", [])]
    lo = [min(m.bounds()[0][a] for m in meshes) for a in range(3)]
    hi = [max(m.bounds()[1][a] for m in meshes) for a in range(3)]
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
        "micro": micro,
        "cull_distance_m": float(built["cull_distance_m"]),
        "materials": materials,
        "collision": built["collision"], "gaps": built["gaps"], "wind_volumes": built["wind_volumes"],
        "ports": built.get("ports", []), "snag": built["snag"],
    }
    return _rounded(meta)


def capsule(p0, p1, radius, material):
    """A collision capsule of `radius` along the chord p0-p1 (height the total length, caps included), rotated from
    local +Y onto the chord, not landable; a sphere when the chord has no length."""
    d = [p1[a] - p0[a] for a in range(3)]
    length = math.sqrt(_dot(d, d))
    if length == 0:
        return {"shape": "sphere", "center": list(p0), "radius": radius, "material": material, "landable": False}
    return {"shape": "capsule", "center": [(p0[a] + p1[a]) / 2 for a in range(3)], "radius": radius,
            "height": length + 2 * radius, "rotation": rotation_from_y([c / length for c in d]), "material": material,
            "landable": False}


def rotation_from_y(d):
    """The unit quaternion [x, y, z, w] taking local +Y onto the unit direction d (Unity's convention)."""
    c = max(-1.0, min(1.0, d[1]))
    if c > 1 - 1e-9:
        return [0.0, 0.0, 0.0, 1.0]
    if c < -1 + 1e-9:
        return [1.0, 0.0, 0.0, 0.0]
    length = math.sqrt(d[2] * d[2] + d[0] * d[0])
    axis = (d[2] / length, 0.0, -d[0] / length)  # +Y x d, normalised
    half = math.acos(c) / 2
    s = math.sin(half)
    return [axis[0] * s, axis[1] * s, axis[2] * s, math.cos(half)]


def validate(meta, material_ids):
    """Problems with a metadata dict against schema 1 and its conventions, as strings naming the rule; empty when
    it conforms. `material_ids` is the closed physics-material list from load_physics_materials."""
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
    for i, m in enumerate(meta["micro"]):
        ok = (isinstance(m, dict) and all(k in m for k in MICRO_KEYS)
              and isinstance(m["lod"], int) and 0 <= m["lod"] < len(lods)
              and isinstance(m["name"], str) and m["name"] and isinstance(m["kind"], str) and m["kind"]
              and isinstance(m["count"], int) and m["count"] >= 0 and isinstance(m["tris"], int) and m["tris"] >= 0)
        if not ok:
            problems.append(f"micro[{i}]: needs {', '.join(MICRO_KEYS)} with an existing lod and counts >= 0: {m}")
    for i, m in enumerate(meta["materials"]):
        if not all(isinstance(m.get(k), str) and m[k] for k in ("slot", "visual", "physics")):
            problems.append(f"material entry needs slot, visual and physics: {m}")
        else:
            problems += [f"materials[{i}]: physics {p}" for p in _material_problems(m["physics"], material_ids)]
    for i, c in enumerate(meta["collision"]):
        problems += [f"collision[{i}]: {p}" for p in _shape_problems(c)]
        if not isinstance(c.get("material"), str) or not isinstance(c.get("landable"), bool):
            problems.append(f"collision[{i}]: needs a material id and a boolean landable")
        else:
            problems += [f"collision[{i}]: {p}" for p in _material_problems(c["material"], material_ids)]
    for i, g in enumerate(meta["gaps"]):
        polygon = g.get("polygon")
        if not (isinstance(g.get("id"), str) and isinstance(polygon, list) and len(polygon) >= 3
                and all(_is_vec(p) for p in polygon) and _is_vec(g.get("normal")) and _is_num(g.get("narrowest_m"))):
            problems.append(f"gaps[{i}]: needs id, a planar polygon of 3+ points, normal and narrowest_m")
        else:
            problems += [f"gaps[{i}]: {p}" for p in _gap_problems(g)]
    for i, w in enumerate(meta["wind_volumes"]):
        problems += [f"wind_volumes[{i}]: {p}" for p in _shape_problems(w)]
        if not (_is_num(w.get("porosity")) and 0 <= w["porosity"] <= 1):
            problems.append(f"wind_volumes[{i}]: porosity must be in [0, 1]")
    ids = set()
    for i, port in enumerate(meta["ports"]):
        if not (isinstance(port, dict) and isinstance(port.get("id"), str) and port["id"]
                and _is_vec(port.get("position")) and _is_vec(port.get("direction"))):
            problems.append(f"ports[{i}]: needs {', '.join(PORT_KEYS)}: a non-empty id and two 3-vectors: {port}")
            continue
        if port["id"] in ids:
            problems.append(f"ports[{i}]: id {port['id']!r} is not unique")
        ids.add(port["id"])
        length = math.sqrt(_dot(port["direction"], port["direction"]))
        if abs(length - 1) > NORMAL_TOLERANCE:
            problems.append(f"ports[{i}] {port['id']!r}: direction must be a unit vector within {NORMAL_TOLERANCE:g}, "
                            f"|d| = {length:.6f}")
    for i, s in enumerate(meta["snag"]):
        if not (isinstance(s.get("kind"), str) and isinstance(s.get("points"), list)
                and all(_is_vec(p) for p in s["points"]) and _is_num(s.get("radius_m"))):
            problems.append(f"snag[{i}]: needs kind, points and radius_m")
        else:
            if s["kind"] not in SNAG_KINDS:
                problems.append(f"snag[{i}]: kind must be one of {', '.join(SNAG_KINDS)}, got {s['kind']!r}")
            if len(s["points"]) < 2:
                problems.append(f"snag[{i}]: needs at least 2 points, got {len(s['points'])}")
    return problems


def check_against_fbx(meta, fbx_path):
    """Problems where the metadata disagrees with the exported file as Unity will read it: a LOD or micro mesh
    missing from the FBX, or the union of their bounds off the metadata bounds by more than a millimetre."""
    found = fbxread.unity_bounds(fbx_path)
    problems = []
    lo, hi = [None] * 3, [None] * 3
    for name in [lod["mesh"] for lod in meta["lods"]] + [m["name"] for m in meta["micro"]]:
        if name not in found:
            problems.append(f"{name} is not a mesh of {fbx_path}; it holds {', '.join(sorted(found))}")
            continue
        mesh_lo, mesh_hi = found[name]
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


def _material_problems(material, material_ids):
    """A material id is a plain id of the physics table, or surface:<id> with one of the thirteen surface ids."""
    if material.startswith(SURFACE_PREFIX):
        if material[len(SURFACE_PREFIX):] not in SURFACE_IDS:
            return [f"surface material {material!r} is not one of the thirteen surface ids "
                    f"{SURFACE_PREFIX}{{{', '.join(SURFACE_IDS)}}}"]
        return []
    if material not in material_ids:
        return [f"material {material!r} is not in {PHYSICS_MATERIALS}"]
    return []


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
    if problems:
        return problems
    if "rotation" in SHAPES[kind]:
        length = math.sqrt(_dot(shape["rotation"], shape["rotation"]))
        if abs(length - 1) > QUATERNION_TOLERANCE:
            problems.append(f"{kind} rotation must be a unit quaternion within {QUATERNION_TOLERANCE:g}, |q| = {length:.6f}")
    if kind == "capsule" and shape["height"] < 2 * shape["radius"]:
        problems.append(f"capsule height {shape['height']} must be at least 2*radius = {2 * shape['radius']} "
                        "(the total length, caps included)")
    return problems


def _gap_problems(gap):
    """The gap conventions: a unit normal perpendicular to every edge, vertices within 10 mm of the plane through
    the centroid, and narrowest_m within (0, the polygon's extent]. The normal's sign and the winding are free."""
    polygon, n, narrowest = gap["polygon"], gap["normal"], gap["narrowest_m"]
    length = math.sqrt(_dot(n, n))
    if abs(length - 1) > NORMAL_TOLERANCE:
        return [f"normal must be a unit vector within {NORMAL_TOLERANCE:g}, |n| = {length:.6f}"]
    problems = []
    for k, (a, b) in enumerate(zip(polygon, polygon[1:] + polygon[:1])):
        edge = [b[c] - a[c] for c in range(3)]
        edge_length = math.sqrt(_dot(edge, edge))
        if edge_length > 0 and abs(_dot(edge, n)) / edge_length > PERPENDICULAR_TOLERANCE:
            problems.append(f"normal is not perpendicular to the polygon: edge {k} has |e.n|/|e| = "
                            f"{abs(_dot(edge, n)) / edge_length:.3g}, tolerance {PERPENDICULAR_TOLERANCE:g}")
            break
    centroid = [sum(p[c] for p in polygon) / len(polygon) for c in range(3)]
    off_plane = max(abs(_dot([p[c] - centroid[c] for c in range(3)], n)) for p in polygon)
    if off_plane > PLANAR_TOLERANCE_M:
        problems.append(f"polygon must be planar within {PLANAR_TOLERANCE_M * 1000:g} mm, a vertex lies "
                        f"{off_plane * 1000:.1f} mm off its plane")
    extent = max(math.dist(a, b) for a in polygon for b in polygon)
    if not 0 < narrowest <= extent:
        problems.append(f"narrowest_m must lie in (0, polygon extent {extent:.4g}], got {narrowest}")
    return problems


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + (a[3] * b[3] if len(a) == 4 else 0.0)


def _is_num(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool)


def _is_vec(v, n=3):
    return isinstance(v, list) and len(v) == n and all(_is_num(c) for c in v)


def _is_hex(s, n):
    return isinstance(s, str) and len(s) == n and all(c in "0123456789abcdef" for c in s)
