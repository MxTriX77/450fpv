"""vendor_ruin: the physics of a vendor mesh we place but never export (w4b-farmland-ruins, D-089, D-090): the
farmland village's ruined houses and their debris, made by the friend's build_village_ruins.py from the Country House
FREE pack. The mesh stays in the local store (vendor/); this reads it (lib.fbxread, as Unity imports it) and returns
the metadata only. tools/blender/vendor_build.py runs it; nothing here writes an FBX.

Params (assets-src/ruins/<variant>.json):
- source: the FBX under vendor/, its SHA-256 (refused on a mismatch: the store's file changed) and the object, the
  friend's prefab's mesh.
- prefab: how the friend's prefab (FarmLandBuilder.Village.cs, SaveVillagePrefab) places that mesh: mirror_x (the
  village's mirrored Ruin_Walls, localScale.x = -1, which a placement row cannot carry), the child's Unity Euler
  rotation (the doors lie on their side, (0, 0, 90)) and recentre (debris: the origin on the ground under the middle
  of its bounds). Asset space is the prefab's local frame.
- intact: the pack's House.fbx objects the ruins were cut from (every one but the glass, which the friend removed),
  read for the openings and the roof and ceiling the ruin lost; empty for debris.
- parts: the rules, first match wins, that give each face its part and physics: the face's material slot (its base
  name; a burnt copy's "_Burnt" is dropped), its normal class (up, down, vertical, sloped, any) and its centroid's box,
  in the source mesh's frame before the prefab moves it. Each rule's layer_m (a lining's share of a layered slab),
  sheet_m (a single-sided face's slab), landable, edge_radius_m (its snag records' arris) and merge_tol_m (its
  rectangles' merge tolerance, where its diagonal edges allow a coarser staircase: a roof's hips).
- slabs: lib.slabs' settings.
- gaps: the parts whose slabs are walls (their enclosed clear regions in the intact house are the openings), the skin
  parts (roof, ceiling: what the ruin lost of them are holes), the smallest gap, the band past a wall's faces that
  counts as its frame, the jamb probe's offset and the share of a skin plane below which it is gone, not holed.
- wind: the parts that block the wind, the coarse cell, and an optional cavity box (a ruin that keeps its roof).
- snag: how far below the intact top a wall's top counts as broken.
- lod: the cull distance (the friend's prefab carries no LOD switch).

Asset space and the gaps' and boxes' rules are lib.slabs' and lib.meta's.
"""
import hashlib
import math
import os

from lib import air, audit, fbxread, slabs
from lib.geometry import Mesh

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
VENDOR = os.path.join(ROOT, "vendor")
NORMAL_CLASSES = ("any", "up", "down", "vertical", "sloped")
UP_MIN, VERTICAL_MAX = 0.9, 0.2  # |n.y| at least this is up or down, at most this is vertical, between is sloped
RULE_KEYS = {"part", "physics", "slots", "normal", "box", "layer_m", "sheet_m", "landable", "edge_radius_m",
             "merge_tol_m"}
CLEAR_M, STEP_M = 1e-4, 2.5e-4  # D-089: no pushed point within CLEAR_M of a vendor vertex; one there moves STEP_M

PARAMS = {
    "source.model": {"type": "str", "range": "an FBX path under vendor/",
                     "default": "world_final/Farming_land/Assets/Farm_land/Models/village_ruins.fbx",
                     "effect": "the vendor file the mesh is read from (local-only, D-089)"},
    "source.model_sha256": {"type": "str", "range": "64 hex digits", "default": "",
                            "effect": "the file's SHA-256; a different file is refused"},
    "source.object": {"type": "str", "range": "a mesh model of the file", "default": "Ruin_Walls",
                      "effect": "the mesh the friend's prefab draws"},
    "prefab.mirror_x": {"type": "bool", "range": "true or false", "default": False,
                        "effect": "the instance's localScale.x = -1, baked in (a placement row has a yaw only)"},
    "prefab.rotation_euler_deg": {"type": "list", "range": "[x, y, z] degrees, Unity's Quaternion.Euler",
                                  "default": [0.0, 0.0, 0.0], "effect": "the prefab child's rotation"},
    "prefab.recentre": {"type": "bool", "range": "true or false", "default": False,
                        "effect": "debris: the origin moves to the ground under the middle of the bounds"},
    "intact.model": {"type": "str", "range": "an FBX path under vendor/, or empty", "default": "",
                     "effect": "the intact house the ruin was cut from: its openings and skins"},
    "intact.model_sha256": {"type": "str", "range": "64 hex digits, or empty", "default": "",
                            "effect": "that file's SHA-256"},
    "intact.objects": {"type": "list", "range": "mesh models of that file", "default": [],
                       "effect": "the intact house's objects (the glass left out, as the friend's script does)"},
    "parts": {"type": "list", "range": "rules of " + ", ".join(sorted(RULE_KEYS)), "default": [],
              "effect": "each face's part and physics, first match wins"},
    "slabs.raster_m": {"type": "float", "range": (0.005, 0.05), "default": 0.01,
                       "effect": "pixel size of the planes' rasters (assumed: 1 cm, a fifth of the 5 cm tolerance)"},
    "slabs.angle_tol_deg": {"type": "float", "range": (0.5, 15.0), "default": 4.0,
                            "effect": "normals within this of a family's share it"},
    "slabs.axis_angle_deg": {"type": "float", "range": (0.0, 15.0), "default": 3.0,
                             "effect": "normals within this of X, Y or Z join that axis's family (a sagging floor)"},
    "slabs.axis_dev_m": {"type": "float", "range": (0.0, 0.05), "default": 0.015,
                         "effect": "a face no deeper than this along X, Y or Z is that axis's (a lap board's bevel)"},
    "slabs.plane_tol_m": {"type": "float", "range": (0.001, 0.1), "default": 0.03,
                          "effect": "faces within this offset share a plane"},
    "slabs.max_thickness_m": {"type": "float", "range": (0.05, 1.0), "default": 0.35,
                              "effect": "a back and a front face at most this far apart enclose solid"},
    "slabs.merge_tol_m": {"type": "float", "range": (0.0, 0.05), "default": 0.015,
                          "effect": "a merged rectangle's end lies at most this off a diagonal edge"},
    "slabs.min_box_m": {"type": "float", "range": (0.005, 0.1), "default": 0.02,
                        "effect": "rectangles narrower than this are raster noise"},
    "slabs.cover_tol_m": {"type": "float", "range": (0.0, 0.05), "default": 0.015,
                          "effect": "a face this close to a box already made is that box's surface"},
    "slabs.air_cell_m": {"type": "float", "range": (0.005, 0.05), "default": 0.02,
                         "effect": "the cell of the air round the mesh (lib.air): a pair whose space is no closed "
                                   "cavity of its own is two skins, open between them; a crack narrower is sealed"},
    "prune.tol_m": {"type": "float", "range": (0.0, 0.05), "default": 0.02,
                    "effect": "a box within this of the others of its material is dropped (lib.slabs.prune)"},
    "gaps.wall_parts": {"type": "list", "range": "part names", "default": [],
                        "effect": "the parts whose slabs are walls with openings"},
    "gaps.skin_parts": {"type": "list", "range": "part names", "default": [],
                        "effect": "the parts whose lost area is a hole (roof, ceiling)"},
    "gaps.min_m": {"type": "float", "range": (0.05, 2.0), "default": 0.25,
                   "effect": "the smallest opening either way"},
    "gaps.band_m": {"type": "float", "range": (0.0, 0.5), "default": 0.12,
                    "effect": "how far past a wall's faces a frame still closes its openings"},
    "gaps.jamb_px": {"type": "int", "range": (1, 20), "default": 3,
                     "effect": "how many pixels outside an opening its jambs are probed, and how far above its "
                               "bottom a jamb may start"},
    "gaps.holes_max": {"type": "int", "range": (1, 100), "default": 12,
                       "effect": "at most this many rectangles inscribed in one skin's lost area, largest first"},
    "gaps.skin_keep_min": {"type": "float", "range": (0.0, 1.0), "default": 0.05,
                           "effect": "a skin plane keeping less than this share is gone, not holed"},
    "wind.parts": {"type": "list", "range": "part names", "default": [],
                   "effect": "the parts that block the wind"},
    "wind.cell_m": {"type": "float", "range": (0.05, 1.0), "default": 0.25,
                    "effect": "the wind volumes' cell"},
    "wind.cavity_box": {"type": "list", "range": "[] or [[x, y, z], [x, y, z]] in asset space", "default": [],
                        "effect": "a sheltered interior (H01's cavity): its box, porosity 1"},
    "snag.broken_drop_m": {"type": "float", "range": (0.0, 1.0), "default": 0.05,
                           "effect": "a wall top this far below the intact one is a broken edge"},
    "snag.open_edges": {"type": "bool", "range": "true or false", "default": False,
                        "effect": "debris: every open edge of the mesh (a sheet's broken rim) is a snag edge"},
    "lod.cull_m": {"type": "float", "range": (1.0, 5000.0), "default": 2340.0,
                   "effect": "the cull distance"},
}


def check(params):
    problems = []
    for key in ("source.model_sha256",):
        if not _is_hex(params["source"]["model_sha256"], 64):
            problems.append(f"'{key}' must be 64 hex digits")
    intact = params["intact"]
    if intact["model"] and not _is_hex(intact["model_sha256"], 64):
        problems.append("'intact.model_sha256' must be 64 hex digits when intact.model is set")
    if bool(intact["model"]) != bool(intact["objects"]):
        problems.append("'intact.objects' must name objects exactly when intact.model is set")
    rot = params["prefab"]["rotation_euler_deg"]
    if not (len(rot) == 3 and all(_is_num(v) for v in rot)):
        problems.append("'prefab.rotation_euler_deg' must be three numbers")
    if not params["parts"]:
        problems.append("'parts' needs at least one rule")
    for i, rule in enumerate(params["parts"]):
        problems += [f"'parts[{i}]' {p}" for p in _rule_problems(rule)]
    names = {r.get("part") for r in params["parts"]}
    for key in ("wall_parts", "skin_parts"):
        problems += [f"'gaps.{key}' names {p!r}, which no rule makes" for p in params["gaps"][key] if p not in names]
    problems += [f"'wind.parts' names {p!r}, which no rule makes" for p in params["wind"]["parts"] if p not in names]
    box = params["wind"]["cavity_box"]
    if box and not (len(box) == 2 and all(len(c) == 3 and all(_is_num(v) for v in c) for c in box)
                    and all(box[0][k] < box[1][k] for k in range(3))):
        problems.append("'wind.cavity_box' must be [] or [[x, y, z], [x, y, z]] with min < max")
    return problems


def _rule_problems(rule):
    if not isinstance(rule, dict) or not RULE_KEYS >= set(rule) or not {"part", "physics"} <= set(rule):
        return [f"must hold part and physics and only {', '.join(sorted(RULE_KEYS))}"]
    problems = []
    if rule.get("normal", "any") not in NORMAL_CLASSES:
        problems.append(f"normal must be one of {', '.join(NORMAL_CLASSES)}")
    box = rule.get("box")
    if box is not None and not (len(box) == 2 and all(len(c) == 3 for c in box)
                                and all(box[0][k] <= box[1][k] for k in range(3))):
        problems.append("box must be [[x, y, z], [x, y, z]] with min <= max")
    for key in ("layer_m", "sheet_m", "edge_radius_m", "merge_tol_m"):
        if rule.get(key) is not None and not (_is_num(rule[key]) and rule[key] > 0):
            problems.append(f"{key} must be a positive number")
    return problems


# --------------------------------------------------------------------------------------------------------- build

def build(params):
    source, prefab, intact = params["source"], params["prefab"], params["intact"]
    rules = params["parts"]
    parts = [slabs.Part(r["part"], r["physics"], r.get("layer_m"), r.get("sheet_m", 0.02), r.get("landable", False),
                        r.get("edge_radius_m", 0.002), r.get("merge_tol_m")) for r in rules]
    mesh = _read(source["model"], source["model_sha256"])[source["object"]]
    place = _placement(mesh["vertices"], prefab)
    ruin_tris, lod0, slot_area = _triangles(mesh, rules, place, prefab["mirror_x"])
    settings = dict(params["slabs"])

    report = []
    intact_out = None
    if intact["model"]:
        house = _read(intact["model"], intact["model_sha256"])
        tris = []
        for name in intact["objects"]:
            tris += _triangles(house[name], rules, place, prefab["mirror_x"])[0]
        intact_out = slabs.decompose(tris, parts, settings)
        seed = [f.n for f in intact_out["families"]]
        if prefab["mirror_x"]:  # the openings' ids from the unmirrored house, so a mirrored ruin's match the ruin's
            unmirrored = _placement(mesh["vertices"], dict(prefab, mirror_x=False))
            tris = [t for name in intact["objects"] for t in _triangles(house[name], rules, unmirrored, False)[0]]
            named = _mirrored(_intact_openings(slabs.decompose(tris, parts, settings), parts, params))
        else:
            named = _intact_openings(intact_out, parts, params)
    else:
        seed = None
    out = slabs.decompose(ruin_tris, parts, settings, seed, air.Air(ruin_tris, settings["air_cell_m"]))
    made = len(out["boxes"])
    kept = slabs.prune(out["boxes"], lambda b: parts[b["part"]].physics, params["prune"]["tol_m"])

    collision = [_shape(b, parts) for b in kept]
    gaps, snag = [], []
    if intact_out is not None:
        kept_corners = [_corners(b) for b in kept]
        openings, edges = _openings(named, out, parts, params, kept_corners)
        holes, hole_edges = _skin_holes(intact_out, out, parts, params, kept_corners)
        gaps, snag = openings + holes, edges + hole_edges
        snag += _broken_tops(intact_out, out, parts, params)
    if params["snag"]["open_edges"]:
        snag += _open_rims(out, lod0, parts, params["slabs"]["raster_m"])
    wind = _wind(out, parts, params, gaps)
    moved = _off_vertices(lod0.vertices, collision + wind,
                          [p for g in gaps for p in g["polygon"]] + [p for e in snag for p in e["points"]])
    materials = []
    for slot in sorted(slot_area):
        physics = max(slot_area[slot].items(), key=lambda kv: (kv[1], kv[0]))[0]
        materials.append({"slot": slot, "physics": physics})
    counts = {}
    for c in collision:
        counts[c["material"]] = counts.get(c["material"], 0) + 1
    report.append(f"{source['object']} of {source['model']} ({source['model_sha256'][:12]}): {len(lod0.faces)} "
                  f"polygons, mirror_x {prefab['mirror_x']}, rotation {prefab['rotation_euler_deg']}, "
                  f"recentre {prefab['recentre']}")
    report.append(f"{len(collision)} boxes ({', '.join(f'{n} {m}' for m, n in sorted(counts.items()))}; "
                  f"{made - len(collision)} of {made} pruned within {params['prune']['tol_m']} m) over "
                  f"{len(out['families'])} plane families; {len(gaps)} gaps, {len(wind)} wind volumes, "
                  f"{len(snag)} snag records; {moved[0]} boxes and {moved[1]} points moved off a vertex (D-089)")
    return {
        "lods": [lod0], "switch_distances_m": [0.0], "cull_distance_m": params["lod"]["cull_m"],
        "materials": materials, "collision": collision, "gaps": gaps, "wind_volumes": wind, "snag": snag,
        "vendor": {"model": source["model"], "model_sha256": source["model_sha256"], "object": source["object"],
                   "mirror_x": prefab["mirror_x"], "rotation_euler_deg": [float(v) for v in
                                                                          prefab["rotation_euler_deg"]],
                   "recentre": prefab["recentre"]},
        "report": report,
        "_debug": {"slabs": out, "intact": intact_out, "parts": parts},
    }


def _read(relative, sha256):
    path = os.path.join(VENDOR, *relative.split("/"))
    if not os.path.exists(path):
        raise FileNotFoundError(f"{path} is missing: the local store is not linked here (pwsh tools/vendor/link.ps1)")
    with open(path, "rb") as f:
        digest = hashlib.sha256(f.read()).hexdigest()
    if digest != sha256:
        raise ValueError(f"{relative}: SHA-256 {digest}, the params pin {sha256}: the store's file changed")
    return fbxread.meshes(path)


def _placement(vertices, prefab):
    """The map from the source mesh's frame to the prefab's: the mirror, the child's rotation, then the debris'
    recentring on the rotated bounds (SaveVillagePrefab: (-centre.x, -min.y, -centre.z))."""
    q = unity_euler(prefab["rotation_euler_deg"])
    sx = -1.0 if prefab["mirror_x"] else 1.0

    def rotate(p):
        return _rotate(q, (sx * p[0], p[1], p[2]))

    offset = (0.0, 0.0, 0.0)
    if prefab["recentre"]:
        pts = [rotate(p) for p in vertices]
        lo = [min(p[k] for p in pts) for k in range(3)]
        hi = [max(p[k] for p in pts) for k in range(3)]
        offset = (-(lo[0] + hi[0]) / 2, -lo[1], -(lo[2] + hi[2]) / 2)

    def point(p):
        r = rotate(p)
        return (r[0] + offset[0], r[1] + offset[1], r[2] + offset[2])

    def direction(n):
        return rotate(n)

    return point, direction


def _triangles(mesh, rules, place, mirror_x):
    """The mesh's triangles in asset space with their outward normals and rule index, the LOD0 Mesh, and per slot the
    area per physics material. The reader negates X, which mirrors the file's counter-clockwise outward winding,
    so the outward normal is minus the polygon's Newell normal (and flips once more with mirror_x, which the normal's
    own transform carries). A concave polygon (the friend's cut leaves floors and walls notched) is ear-clipped as
    the audit does (lib.audit's triangulation), never fanned: a fan spills past its notches, where no face is drawn."""
    point, direction = place
    vertices = mesh["vertices"]
    lod0 = Mesh()
    index = [lod0.vertex(point(p)) for p in vertices]
    tris, slot_area = [], {}
    for poly, slot in zip(mesh["polygons"], mesh["slots"]):
        src = [vertices[i] for i in poly]
        n = _newell(src)
        length = math.sqrt(_dot(n, n))
        if length < 1e-12:
            continue
        outward_src = tuple(-c / length for c in n)
        centroid = tuple(sum(p[k] for p in src) / len(src) for k in range(3))
        rule = classify(rules, slot, outward_src, centroid)
        outward = direction(outward_src)
        lod0.face([index[i] for i in poly], outward, slot)
        pts = [point(p) for p in src]
        if audit._is_convex(pts, outward):
            fan = [(0, k, k + 1) for k in range(1, len(pts) - 1)]
        else:
            fan = audit._triangulate(pts, outward)
        area = 0.0
        for a, b, c in fan:
            tri = (pts[a], pts[b], pts[c])
            tris.append((tri, outward, rule))
            c = _cross(_sub(tri[1], tri[0]), _sub(tri[2], tri[0]))
            area += 0.5 * math.sqrt(_dot(c, c))
        per = slot_area.setdefault(slot, {})
        per[rules[rule]["physics"]] = per.get(rules[rule]["physics"], 0.0) + area
    return tris, lod0, slot_area


def classify(rules, slot, normal, centroid):
    """The index of the first rule that takes a face of `slot` with this outward normal and centroid (source
    frame); the last rule must take every face."""
    base = slot[:-len("_Burnt")] if slot.endswith("_Burnt") else slot
    ny = normal[1]
    kind = "up" if ny >= UP_MIN else "down" if ny <= -UP_MIN else "vertical" if abs(ny) <= VERTICAL_MAX else "sloped"
    for i, rule in enumerate(rules):
        if "slots" in rule and base not in rule["slots"]:
            continue
        if rule.get("normal", "any") not in ("any", kind):
            continue
        box = rule.get("box")
        if box and not all(box[0][k] <= centroid[k] <= box[1][k] for k in range(3)):
            continue
        return i
    raise ValueError(f"no part rule takes a face of {slot} ({kind}) at {centroid}: end the rules with a catch-all")


def _shape(box, parts):
    part = parts[box["part"]]
    return {"shape": "box", "center": list(box["center"]), "size": list(box["size"]),
            "rotation": list(box["rotation"]), "material": part.physics, "landable": bool(part.landable),
            "part": part.name}


# ------------------------------------------------------------------------------------------------------- gaps

def _bands(result, parts, names):
    """Per family, the wall bands: slab regions of the named parts whose [lo, hi] touch, merged: {(family key, lo,
    hi): {"rows": {row: bits}, "layers": [(lo, hi, part)], "family": index}}."""
    out = {}
    for fi, family_regions in enumerate(result["regions"]):
        keys = sorted(k for k in family_regions if parts[k[2]].name in names)
        bands = []
        for lo, hi, pi in keys:
            for band in bands:
                if lo <= band["hi"] + 0.011 and hi >= band["lo"] - 0.011 and band["name"] == parts[pi].name:
                    band["lo"], band["hi"] = min(band["lo"], lo), max(band["hi"], hi)
                    band["layers"].append((lo, hi, pi))
                    break
            else:
                bands.append({"lo": lo, "hi": hi, "layers": [(lo, hi, pi)], "name": parts[pi].name})
        for band in bands:
            rows = {}
            for lo, hi, pi in band["layers"]:
                for j, bits in family_regions[(lo, hi, pi)].items():
                    rows[j] = rows.get(j, 0) | bits
            band["rows"], band["family"] = rows, fi
            out[(result["families"][fi].key, round(band["lo"], 4), round(band["hi"], 4))] = band
    return out


def _matching(bands, key, lo, hi):
    """The band of `bands` on the same family whose faces lie within 2 cm of [lo, hi], or None."""
    return next((b for (k, blo, bhi), b in bands.items() if k == key and abs(blo - lo) < 0.02 and abs(bhi - hi) < 0.02),
                None)


def _obstruction(result, fi, lo, hi, margin, r):
    """Every axis-aligned box of `result` meeting the slab [lo - margin, hi + margin] along family fi's axis,
    projected on its (u, v) raster: {row: bits}."""
    fam = result["families"][fi]
    rows = {}
    for box in result["boxes"]:
        if box["aabb"] is None:
            continue
        blo, bhi = box["aabb"]
        k = fam.axis
        if bhi[k] < lo - margin or blo[k] > hi + margin:
            continue
        ua, ub = slabs._range_on(blo, bhi, fam.u, slabs._axis_index(fam.u))
        va, vb = slabs._range_on(blo, bhi, fam.v, slabs._axis_index(fam.v))
        i0, i1 = math.ceil(ua / r - 0.5), math.floor(ub / r - 0.5)
        j0, j1 = math.ceil(va / r - 0.5), math.floor(vb / r - 0.5)
        if i1 < i0 or j1 < j0:
            continue
        bits = slabs.span(i0, i1)
        for j in range(j0, j1 + 1):
            rows[j] = rows.get(j, 0) | bits
    return rows


def _openings(named, ruin, parts, params, kept_corners):
    """The openings (task 1.3, QA's F2 and F4): every clear region the ruin's own wall band encloses on all sides,
    clear of every kept box of any part within gaps.band_m of the band's faces, covered by rectangles of at least
    gaps.min_m either way (two panes that a raster leak through a glazing bar joins are two gaps), each named after
    the intact house's opening (`named`, _intact_openings in the ruin's frame) it overlaps most, so one opening keeps
    its id in every ruin, the mirrored one too; and each intact opening the ruin no longer encloses (its head broken
    off), cut at the lower of its jambs' tops in the ruin, its clear space below that cut, gone when that leaves less
    than gaps.min_m either way. An opening whose bottom is the floor is a door. Each gap lies at its band's middle; a
    snag ring outlines it at both faces."""
    g, r = params["gaps"], params["slabs"]["raster_m"]
    min_px = math.ceil(g["min_m"] / r)
    names = set(g["wall_parts"])
    flip = -1 if params["prefab"]["mirror_x"] else 1
    found = []
    for (key, lo, hi), band in sorted(_bands(ruin, parts, names).items(), key=lambda kv: _along_source(kv, flip)):
        fam = ruin["families"][band["family"]]
        if fam.axis is None or fam.axis == 1:
            continue
        blocked = _shadow(kept_corners, fam, lo - g["band_m"], hi + g["band_m"], r)
        rects = [q for comp in _enclosed(band["rows"], blocked, seal=True) for q in _cover(comp, min_px, math.inf)]
        backwards = flip < 0 and abs(fam.u[0]) > 0.5
        for rect in sorted(rects, key=lambda q: (-q[1], q[2]) if backwards else q):
            found.append({"key": key, "lo": lo, "hi": hi, "band": band, "fam": fam, "rect": rect})

    def same_wall(a, b):
        return a["key"] == b["key"] and a["lo"] <= b["hi"] + 0.02 and a["hi"] >= b["lo"] - 0.02

    pairs = sorted((-_overlap(f["rect"], o["rect"]), a, b) for a, f in enumerate(found) for b, o in enumerate(named)
                   if same_wall(f, o) and _overlap(f["rect"], o["rect"]))
    name_of, taken = {}, set()
    for _, a, b in pairs:  # the largest overlaps first
        if a not in name_of and b not in taken:
            name_of[a] = b
            taken.add(b)
    top = {}
    for o in named:
        top[(o["band"]["name"], o["kind"])] = max(top.get((o["band"]["name"], o["kind"]), 0), o["number"])
    out = []
    for a, f in enumerate(found):
        if a in name_of:  # at the intact wall's middle and faces, as the opening is in every ruin
            o = named[name_of[a]]
            out.append((name_of[a], o["id"], f["fam"], o["band"], o["lo"], o["hi"], f["rect"]))
        else:  # a region the intact house did not enclose
            i0, i1, j0, j1 = f["rect"]
            kind = "door" if _on_floor(ruin, parts, f["band"]["family"], f["lo"], f["hi"], j0, r) else "window"
            top[(f["band"]["name"], kind)] = top.get((f["band"]["name"], kind), 0) + 1
            gid = f"{f['band']['name']}_{kind}{top[(f['band']['name'], kind)]}"
            out.append((len(named) + a, gid, f["fam"], f["band"], f["lo"], f["hi"], f["rect"]))
    ruin_index = {fm.key: i for i, fm in enumerate(ruin["families"])}
    shadows = {}
    for b, o in enumerate(named):
        if b in taken or any(same_wall(f, o) and _overlap(f["rect"], o["rect"]) for f in found):
            continue
        key, lo, hi = o["key"], o["lo"], o["hi"]
        if (key, lo, hi) not in shadows:
            shadows[(key, lo, hi)] = (
                _obstruction(ruin, ruin_index[key], lo, hi, g["band_m"], r) if key in ruin_index else {},
                _shadow(kept_corners, o["fam"], lo - g["band_m"], hi + g["band_m"], r))
        jambs, blocked = shadows[(key, lo, hi)]
        i0, i1, j0, j1 = o["rect"]
        cut = min(_jamb_top(jambs, i0 - g["jamb_px"], j0, j1, g["jamb_px"]),
                  _jamb_top(jambs, i1 + g["jamb_px"], j0, j1, g["jamb_px"]))
        # the clear space below the cut: a rail the ruin keeps across the opening's foot is its bottom (QA's F4)
        rect = _largest_rectangle({j: slabs.span(i0, i1) & ~blocked.get(j, 0) for j in range(j0, cut + 1)}, min_px)
        if rect:
            out.append((b, o["id"], o["fam"], o["band"], lo, hi, rect))
    gaps, snag = [], []
    for _, gid, fam, band, lo, hi, (i0, i1, j0, j1) in sorted(out, key=lambda t: t[0]):
        corners = _rect_corners(fam, i0, i1, j0, j1, (lo + hi) / 2, r)
        gaps.append({"id": gid, "polygon": corners, "normal": list(fam.n),
                     "narrowest_m": min((i1 - i0 + 1) * r, (j1 - j0 + 1) * r)})
        for face_s, layer in ((lo, min(band["layers"])), (hi, max(band["layers"], key=lambda t: t[1]))):
            ring = _rect_corners(fam, i0, i1, j0, j1, face_s, r)
            part = parts[layer[2]]
            snag.append({"kind": "edge", "radius_m": part.edge_radius_m, "points": ring + [ring[0]],
                         "material": part.physics})
    return gaps, snag


def _along_source(item, flip):
    """A band's sort key along the intact house in the source mesh's frame (the mirror runs x backwards)."""
    (key, lo, hi), band = item
    return (key, flip * lo if abs(key[0]) > 0.5 else lo)


def _intact_openings(intact, parts, params):
    """Every opening of the intact house, decomposed unmirrored (the source mesh's frame): each clear region a wall
    band encloses on all sides, covered by rectangles of at least gaps.min_m either way, largest first. Numbered per
    part and kind along the house: every region's largest rectangle first (1.2's ids), then the others (a pane
    joined to its neighbour by a leak through the glazing bar). Each: {"id", "number", "kind", "key", "lo", "hi",
    "band", "fam", "rect"}."""
    g, r = params["gaps"], params["slabs"]["raster_m"]
    min_px = math.ceil(g["min_m"] / r)
    firsts, extras = [], []
    for (key, lo, hi), band in sorted(_bands(intact, parts, set(g["wall_parts"])).items(),
                                      key=lambda kv: _along_source(kv, 1)):
        fam = intact["families"][band["family"]]
        if fam.axis is None or fam.axis == 1:
            continue
        covers = [_cover(comp, min_px, math.inf)
                  for comp in _enclosed(band["rows"], _obstruction(intact, band["family"], lo, hi, g["band_m"], r))]
        firsts += [(key, lo, hi, band, fam, q) for q in sorted(c[0] for c in covers if c)]
        extras += [(key, lo, hi, band, fam, q) for q in sorted(q for c in covers for q in c[1:])]
    numbered, out = {}, []
    for key, lo, hi, band, fam, rect in firsts + extras:
        kind = "door" if _on_floor(intact, parts, band["family"], lo, hi, rect[2], r) else "window"
        n = numbered[(band["name"], kind)] = numbered.get((band["name"], kind), 0) + 1
        out.append({"id": f"{band['name']}_{kind}{n}", "number": n, "kind": kind, "key": key, "lo": lo, "hi": hi,
                    "band": band, "fam": fam, "rect": rect})
    return out


def _mirrored(openings):
    """The intact house's openings, numbered unmirrored, in a mirrored ruin's frame (x negated): an X-normal band's
    faces negate and swap, a Z-normal band's pixels along u (x) run backwards; the family's basis is the same."""
    out = []
    for o in openings:
        i0, i1, j0, j1 = o["rect"]
        lo, hi, layers = o["lo"], o["hi"], o["band"]["layers"]
        if abs(o["key"][0]) > 0.5:
            lo, hi, layers = -hi, -lo, [(-b, -a, pi) for a, b, pi in layers]
        else:
            i0, i1 = -i1 - 1, -i0 - 1
        out.append(dict(o, lo=lo, hi=hi, rect=(i0, i1, j0, j1), band=dict(o["band"], layers=layers)))
    return out


def _overlap(a, b):
    """The pixels two rectangles (i0, i1, j0, j1) share."""
    w = min(a[1], b[1]) - max(a[0], b[0]) + 1
    h = min(a[3], b[3]) - max(a[2], b[2]) + 1
    return w * h if w > 0 and h > 0 else 0


def _shadow(corner_sets, fam, s0, s1, r):
    """The pixels of family fam's raster that a box (rotated or not; `corner_sets`, each box's _corners) meets
    between the planes n.x = s0 and n.x = s1: each box clipped to that slab, its outline projected on (u, v) and
    rastered as the faces are (lib.slabs: a pixel the outline overlaps): {row: bits}."""
    plane = slabs.Plane(1, 0.0)
    for corners in corner_sets:
        s = [_dot(p, fam.n) for p in corners]
        if max(s) < s0 or min(s) > s1:
            continue
        pts = [p for p, sp in zip(corners, s) if s0 <= sp <= s1]
        for a in range(8):
            for b in range(a + 1, 8):
                if bin(a ^ b).count("1") != 1:
                    continue  # not an edge of the box
                for cut in (s0, s1):
                    if (s[a] - cut) * (s[b] - cut) < 0:
                        t = (cut - s[a]) / (s[b] - s[a])
                        pts.append(tuple(corners[a][k] + t * (corners[b][k] - corners[a][k]) for k in range(3)))
        hull = _hull([(_dot(p, fam.u), _dot(p, fam.v)) for p in pts])
        if len(hull) < 3:
            continue
        lift = [tuple(a * fam.u[k] + b * fam.v[k] for k in range(3)) for a, b in hull]
        for k in range(1, len(lift) - 1):
            slabs._raster(plane, fam, (lift[0], lift[k], lift[k + 1]), 0, r)
    return plane.parts.get(0, {})


def _corners(box):
    """A box's eight corners in asset space, corner a's bit k the sign along the box's axis k."""
    q, half = box["rotation"], [s / 2 for s in box["size"]]
    out = []
    for a in range(8):
        d = _rotate(q, tuple(half[k] if a >> k & 1 else -half[k] for k in range(3)))
        out.append(tuple(box["center"][k] + d[k] for k in range(3)))
    return out


def _hull(points):
    """The convex hull of 2D points, counter-clockwise (Andrew's monotone chain)."""
    pts = sorted(set(points))
    if len(pts) < 3:
        return pts

    def turn(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and turn(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and turn(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


def _enclosed(footprint, obstruction, seal=False):
    """The connected clear regions inside the footprint's bounding rectangle that touch none of its sides: a list
    of {row: bits}. With `seal`, a slit of one or two pixels (a seam between two boxes of a frame) does not join a
    region to its neighbour or to the outside: the regions are found with the obstruction grown a pixel each way,
    then each is grown back a pixel into the clear space."""
    if not footprint:
        return []
    j_lo, j_hi = min(footprint), max(footprint)
    i_lo = min(slabs.runs(bits)[0][0] for bits in footprint.values())
    i_hi = max(slabs.runs(bits)[-1][1] for bits in footprint.values())
    full = slabs.span(i_lo, i_hi)
    clear = {j: full & ~obstruction.get(j, 0) for j in range(j_lo, j_hi + 1)}
    if seal:
        sealed = {j: clear[j] & ~_grow(obstruction, j) for j in clear}
        out = []
        for comp in _enclosed_in(sealed, j_lo, j_hi, i_lo, i_hi):
            out.append({j: _grow(comp, j) & clear[j] for j in range(max(j_lo, min(comp) - 1),
                                                                   min(j_hi, max(comp) + 1) + 1)})
        return out
    return _enclosed_in(clear, j_lo, j_hi, i_lo, i_hi)


def _grow(rows, j):
    """Row j of `rows` grown a pixel each way, diagonals too."""
    bits = 0
    for d in (-1, 0, 1):
        b = rows.get(j + d, 0)
        bits |= b | (b << 1) | (b >> 1)
    return bits


def _enclosed_in(clear, j_lo, j_hi, i_lo, i_hi):
    """The connected regions of `clear` (rows j_lo..j_hi, pixels i_lo..i_hi) that touch none of its sides."""
    border = slabs.span(i_lo, i_lo) | slabs.span(i_hi, i_hi)
    comps = []
    seen = {j: 0 for j in clear}
    for j in range(j_lo, j_hi + 1):
        free = clear[j] & ~seen[j]
        while free:
            low = free & -free
            comp = _flood(clear, seen, j, low, j_lo, j_hi)
            touches = (comp.get(j_lo, 0) or comp.get(j_hi, 0)
                       or any(bits & border for bits in comp.values()))
            if not touches:
                comps.append(comp)
            free = clear[j] & ~seen[j]
    return comps


def _flood(clear, seen, j, seed_bits, j_lo, j_hi):
    """The 4-connected component of `clear` holding seed_bits in row j, by row-wise bit dilation; marks it seen."""
    comp = {j: seed_bits}
    frontier = [j]
    while frontier:
        nxt = []
        for row in frontier:
            bits = comp[row]
            while True:  # grow along the row within the clear run
                grown = (bits | (bits << 1) | (bits >> 1)) & clear[row]
                if grown == bits:
                    break
                bits = grown
            comp[row] = bits
            for other in (row - 1, row + 1):
                if j_lo <= other <= j_hi:
                    add = bits & clear[other] & ~comp.get(other, 0)
                    if add:
                        comp[other] = comp.get(other, 0) | add
                        nxt.append(other)
        frontier = sorted(set(nxt))
    for row, bits in comp.items():
        seen[row] |= bits
    return comp


def _largest_rectangle(comp, min_px=1):
    """The largest-area rectangle (i0, i1, j0, j1) of pixels all in `comp` with both sides at least min_px, by the
    histogram method over rows (every maximal rectangle is met once per limiting bar), or None."""
    rows = sorted(j for j, b in comp.items() if b)
    if not rows:
        return None
    i_lo = min(slabs.runs(comp[j])[0][0] for j in rows)
    i_hi = max(slabs.runs(comp[j])[-1][1] for j in rows)
    width = i_hi - i_lo + 1
    heights = [0] * width
    best, best_rect = 0, None
    for j in range(rows[0], rows[-1] + 1):
        bits = comp.get(j, 0) >> (i_lo - slabs.BASE)
        for x in range(width):
            heights[x] = heights[x] + 1 if bits >> x & 1 else 0
        stack = []
        for x in range(width + 1):
            h = heights[x] if x < width else 0
            start = x
            while stack and stack[-1][1] >= h:
                start, sh = stack.pop()
                area = sh * (x - start)
                if area > best and sh >= min_px and x - start >= min_px:
                    best, best_rect = area, (i_lo + start, i_lo + x - 1, j - sh + 1, j)
            stack.append((start, h))
    return best_rect


def _cover(region, min_px, limit):
    """Up to `limit` rectangles inscribed in `region`, largest first, each at least min_px either way."""
    region, out = dict(region), []
    while len(out) < limit:
        rect = _largest_rectangle(region, min_px)
        if rect is None:
            break
        i0, i1, j0, j1 = rect
        out.append(rect)
        mask = ~slabs.span(i0, i1)
        for j in range(j0, j1 + 1):
            region[j] &= mask
    return out


def _jamb_top(obstruction, i, j0, j1, slack=0):
    """The highest row up to j1 + 1 that column i stays solid to without a break, from its first solid row at most
    `slack` rows above j0 (a jamb's sheet may start a centimetre above the threshold's: the seam between two skins'
    boxes, which a solid wall filled): j0 - 1 when it is clear there."""
    bit = slabs.span(i, i)
    start = next((j for j in range(j0, j0 + slack + 1) if obstruction.get(j, 0) & bit), None)
    if start is None:
        return j0 - 1
    top = start
    for j in range(start, j1 + 2):
        if obstruction.get(j, 0) & bit:
            top = j
        else:
            break
    return min(top, j1)


def _on_floor(result, parts, fi, lo, hi, j0, r):
    """Whether the opening's bottom is the floor: a landable part's box whose top lies within 10 cm of it (a door's
    threshold board stands a few centimetres proud) and which reaches the wall's band."""
    fam, v = result["families"][fi], j0 * r
    for box in result["boxes"]:
        if box["aabb"] is None or not parts[box["part"]].landable:
            continue
        blo, bhi = box["aabb"]
        if abs(bhi[1] - v) <= 0.1 and blo[fam.axis] <= hi + 0.05 and bhi[fam.axis] >= lo - 0.05:
            return True
    return False


def _rect_corners(fam, i0, i1, j0, j1, s, r):
    a0, a1, b0, b1 = i0 * r, (i1 + 1) * r, j0 * r, (j1 + 1) * r
    out = []
    for a, b in ((a0, b0), (a1, b0), (a1, b1), (a0, b1)):
        out.append([a * fam.u[k] + b * fam.v[k] + s * fam.n[k] for k in range(3)])
    return out


def _skin_holes(intact, ruin, parts, params, kept_corners):
    """What the ruin lost of each skin part (roof, ceiling) per family, its planes taken together (a sagging roof
    face spans several): when the ruin keeps at least gaps.skin_keep_min of it, the lost area's clear space, its
    rectangles of at least gaps.min_m either way inscribed in it, largest first and at most gaps.holes_max, each a
    gap at the part's mean offset, outlined by a snag edge. The clear space (task 1.3, QA's F3) is the lost area less
    the shadow of every kept box of any part within gaps.band_m of the skin's planes: a rafter the ruin keeps across
    the hole closes it there. A skin the ruin lost whole is gone, not holed."""
    g, r = params["gaps"], params["slabs"]["raster_m"]
    names = set(g["skin_parts"])
    ruin_families = {f.key: f for f in ruin["families"]}
    gaps, snag = [], []
    for fam in intact["families"]:
        for pi in sorted({pi for plane in fam.planes for pi in plane.parts if parts[pi].name in names}):
            rows, weight, moment = _union(fam, pi)
            kept = _union(ruin_families[fam.key], pi)[0] if fam.key in ruin_families else {}
            total = sum(bin(b).count("1") for b in rows.values())
            left = sum(bin(b & kept.get(j, 0)).count("1") for j, b in rows.items())
            if total == 0 or left < g["skin_keep_min"] * total:
                continue
            offsets = [plane.offset for plane in fam.planes if pi in plane.parts]
            blocked = _shadow(kept_corners, fam, min(offsets) - g["band_m"], max(offsets) + g["band_m"], r)
            lost = {}
            for j, bits in rows.items():
                m = bits & ~_dilate(kept, j) & ~blocked.get(j, 0)
                if m:
                    lost[j] = m
            offset = moment / weight
            for i0, i1, j0, j1 in sorted(_cover(lost, math.ceil(g["min_m"] / r), g["holes_max"])):
                corners = _rect_corners(fam, i0, i1, j0, j1, offset, r)
                gaps.append({"id": f"{parts[pi].name}_hole{sum(1 for x in gaps if x['id'].startswith(parts[pi].name + '_hole')) + 1}",
                             "polygon": corners, "normal": list(fam.n),
                             "narrowest_m": min((i1 - i0 + 1) * r, (j1 - j0 + 1) * r)})
                snag.append({"kind": "edge", "radius_m": parts[pi].edge_radius_m,
                             "points": corners + [corners[0]], "material": parts[pi].physics})
    return gaps, snag


def _union(fam, pi):
    """Part pi's raster over all the family's planes, and its area-weighted offset's weight and moment."""
    rows, weight, moment = {}, 0.0, 0.0
    for plane in fam.planes:
        part_rows = plane.parts.get(pi)
        if not part_rows:
            continue
        count = 0
        for j, bits in part_rows.items():
            rows[j] = rows.get(j, 0) | bits
            count += bin(bits).count("1")
        weight += count
        moment += count * plane.offset
    return rows, weight, moment


def _dilate(rows, j):
    """Row j of `rows` grown by two pixels each way, so the raster's own jitter between two meshes is no hole."""
    bits = 0
    for d in (-2, -1, 0, 1, 2):
        b = rows.get(j + d, 0)
        bits |= b | (b << 1) | (b >> 1) | (b << 2) | (b >> 2)
    return bits


# ------------------------------------------------------------------------------------------------ snag, wind

def _broken_tops(intact, ruin, parts, params):
    """Each wall band's top where it stands lower than the intact wall's by snag.broken_drop_m: a stepped polyline
    along each face, the fiber's arris."""
    r = params["slabs"]["raster_m"]
    drop = params["snag"]["broken_drop_m"] / r
    names = set(params["gaps"]["wall_parts"])
    intact_bands = _bands(intact, parts, names)
    out = []
    for (key, lo, hi), band in sorted(_bands(ruin, parts, names).items()):
        fam = ruin["families"][band["family"]]
        if fam.axis is None or fam.axis == 1:
            continue
        ref = _matching(intact_bands, key, lo, hi)
        tops = _tops(band["rows"])
        ref_tops = _tops(ref["rows"]) if ref else {}
        broken = sorted(i for i, t in tops.items() if i in ref_tops and t < ref_tops[i] - drop)
        for stretch in _stretches(broken):
            line = []
            for i in stretch:
                t = tops[i]
                if not line or line[-1][1] != t:
                    if line:
                        line.append((i, line[-1][1]))
                    line.append((i, t))
            line.append((stretch[-1] + 1, line[-1][1]))
            if len(line) < 2:
                continue
            for face_s, layer in ((lo, min(band["layers"])), (hi, max(band["layers"], key=lambda t: t[1]))):
                points = [[i * r * fam.u[k] + (t + 1) * r * fam.v[k] + face_s * fam.n[k] for k in range(3)]
                          for i, t in line]
                part = parts[layer[2]]
                out.append({"kind": "edge", "radius_m": part.edge_radius_m, "points": points,
                            "material": part.physics})
    return out


def _open_rims(result, lod0, parts, r):
    """Debris: the rims a fiber catches on (a sheet's broken edge), read off the raster as the ruins' broken tops
    are, so no point of the mesh is copied (task 1.3, QA's F1, D-089). An open edge of the mesh (one polygon uses it,
    its ends welded within a millimetre) marks the pixels it crosses, and their neighbours, on the plane its polygon
    was rastered on; each slab region's boundary through those pixels, on the face of that plane, is chained along
    the pixels' corners into a stepped polyline, a snag edge of the region's part."""
    def key(p):
        return tuple(round(c * 1000) for c in p)
    count = {}
    for indices, _, _, _ in lod0.faces:
        pts = [lod0.vertices[i] for i in indices]
        for a, b in zip(pts, pts[1:] + pts[:1]):
            e = tuple(sorted((key(a), key(b))))
            if e[0] != e[1]:
                count[e] = count.get(e, 0) + 1
    open_edges = {e for e, n in count.items() if n == 1}
    masks = {}  # (family, plane offset) -> {row: bits}
    for fi, fam in enumerate(result["families"]):
        for plane in fam.planes:
            for tri, _, _, _, _ in plane.members:
                for a, b in ((tri[0], tri[1]), (tri[1], tri[2]), (tri[2], tri[0])):
                    if tuple(sorted((key(a), key(b)))) not in open_edges:
                        continue
                    rows = masks.setdefault((fi, plane.offset), {})
                    (ua, va), (ub, vb) = [(_dot(p, fam.u) / r, _dot(p, fam.v) / r) for p in (a, b)]
                    steps = max(1, math.ceil(4 * max(abs(ub - ua), abs(vb - va))))
                    for k in range(steps + 1):
                        i, j = math.floor(ua + (ub - ua) * k / steps), math.floor(va + (vb - va) * k / steps)
                        for dj in (-1, 0, 1):
                            rows[j + dj] = rows.get(j + dj, 0) | slabs.span(i - 1, i + 1)
    out = []
    for fi, fam in enumerate(result["families"]):
        for (lo, hi, pi), rows in sorted(result["regions"][fi].items()):
            for face in (lo, hi):
                mask = masks.get((fi, face))
                if mask is None:
                    continue
                part = parts[pi]
                for line in _boundary(rows, mask):
                    out.append({"kind": "edge", "radius_m": part.edge_radius_m, "material": part.physics,
                                "points": [[i * r * fam.u[k] + j * r * fam.v[k] + face * fam.n[k] for k in range(3)]
                                           for i, j in line]})
    return out


def _boundary(rows, mask):
    """The boundary of the pixel set `rows` where its pixel lies in `mask`, chained along the pixels' corners with the
    set on the left, collinear corners dropped: a list of [(i, j), ...] polylines."""
    nxt = {}
    for j, bits in rows.items():
        m = bits & mask.get(j, 0)
        if not m:
            continue
        sides = ((m & ~rows.get(j - 1, 0), (0, 0), (1, 0)), (m & ~(bits >> 1), (1, 0), (1, 1)),
                 (m & ~rows.get(j + 1, 0), (1, 1), (0, 1)), (m & ~(bits << 1), (0, 1), (0, 0)))
        for edge_bits, a, b in sides:
            for i0, i1 in slabs.runs(edge_bits):
                for i in range(i0, i1 + 1):
                    nxt.setdefault((i + a[0], j + a[1]), []).append((i + b[0], j + b[1]))
    ends = {q for qs in nxt.values() for q in qs}
    lines = []
    for start in sorted(nxt, key=lambda p: (p in ends, p)):  # open chains from their heads first, then loops
        while nxt.get(start):
            line = [start]
            while nxt.get(line[-1]):
                q = sorted(nxt[line[-1]]).pop(0)
                nxt[line[-1]].remove(q)
                line.append(q)
            lines.append([p for k, p in enumerate(line) if k in (0, len(line) - 1)
                          or (p[0] - line[k - 1][0], p[1] - line[k - 1][1]) != (line[k + 1][0] - p[0],
                                                                                 line[k + 1][1] - p[1])])
    return lines


def _off_vertices(vertices, boxes, points):
    """D-089 (task 1.3, QA's F1): no pushed point reproduces a vertex of the vendor mesh. The raster puts a few box
    corners, and snag and gap points, on one by chance (a vertex on the 1 cm grid at a plane's offset): such a box
    (collision or wind) grows STEP_M outward at that corner along each of its axes, such a point moves STEP_M off the
    vertex along each axis, until none lies within CLEAR_M of one (per axis). Returns (boxes, points) moved."""
    grid = {}
    for v in vertices:
        grid.setdefault(tuple(math.floor(c / CLEAR_M) for c in v), []).append(v)
    around = [(a, b, c) for a in (-1, 0, 1) for b in (-1, 0, 1) for c in (-1, 0, 1)]

    def near(p):
        k = [math.floor(c / CLEAR_M) for c in p]
        for d in around:
            for v in grid.get((k[0] + d[0], k[1] + d[1], k[2] + d[2]), ()):
                if max(abs(v[i] - p[i]) for i in range(3)) <= CLEAR_M:
                    return v
        return None

    moved = [0, 0]
    for box in boxes:
        hit = next((a for a, c in enumerate(_corners(box)) if near(c)), None)
        moved[0] += hit is not None
        while hit is not None:
            d = _rotate(box["rotation"], tuple(STEP_M / 2 if hit >> k & 1 else -STEP_M / 2 for k in range(3)))
            box["center"] = [box["center"][k] + d[k] for k in range(3)]
            box["size"] = [s + STEP_M for s in box["size"]]
            hit = next((a for a, c in enumerate(_corners(box)) if near(c)), None)
    for p in points:
        v = near(p)
        moved[1] += v is not None
        while v is not None:
            for k in range(3):
                p[k] += STEP_M if p[k] >= v[k] else -STEP_M
            v = near(p)
    return moved


def _tops(rows):
    """Per pixel column, its highest set row."""
    tops = {}
    for j in sorted(rows):
        for i0, i1 in slabs.runs(rows[j]):
            for i in range(i0, i1 + 1):
                tops[i] = j
    return tops


def _stretches(columns):
    out, current = [], []
    for i in columns:
        if current and i != current[-1] + 1:
            out.append(current)
            current = []
        current.append(i)
    if current:
        out.append(current)
    return out


def _wind(result, parts, params, gaps):
    """The wind volumes: each band of the wind parts on a coarse grid (a cell solid when half its pixels are),
    merged into boxes of porosity 0; and the cavity, porosity 1, open through the gaps of the outer walls and skins
    whose centres lie within half a metre of it."""
    w, r = params["wind"], params["slabs"]["raster_m"]
    k = max(1, round(w["cell_m"] / r))
    out = []
    for (key, lo, hi), band in sorted(_bands(result, parts, set(w["parts"])).items()):
        fi = band["family"]
        fam = result["families"][fi]
        coarse = {}
        for j, bits in band["rows"].items():
            cj = math.floor(j / k)
            for i0, i1 in slabs.runs(bits):
                for ci in range(math.floor(i0 / k), math.floor(i1 / k) + 1):
                    a, b = max(i0, ci * k), min(i1, ci * k + k - 1)
                    coarse.setdefault((cj, ci), 0)
                    coarse[(cj, ci)] += b - a + 1
        rows = {}
        for (cj, ci), count in coarse.items():
            if count * 2 >= k * k:
                rows[cj] = rows.get(cj, 0) | slabs.span(ci, ci)
        for i0, i1, j0, j1 in slabs.rectangles(rows, 0, 1):
            box = slabs._box(fam, fi, (i0, i1, j0, j1), lo, hi, None, r * k)
            out.append({"shape": "box", "center": list(box["center"]), "size": list(box["size"]),
                        "rotation": list(box["rotation"]), "porosity": 0.0})
    cavity = w["cavity_box"]
    if cavity:
        lo, hi = cavity
        openings = []
        for g in gaps:
            c = [sum(p[k2] for p in g["polygon"]) / len(g["polygon"]) for k2 in range(3)]
            if all(lo[k2] - 0.5 <= c[k2] <= hi[k2] + 0.5 for k2 in range(3)) and not g["id"].startswith("partition"):
                openings.append(g["id"])
        out.append({"shape": "box", "center": [(lo[k2] + hi[k2]) / 2 for k2 in range(3)],
                    "size": [hi[k2] - lo[k2] for k2 in range(3)], "rotation": [0.0, 0.0, 0.0, 1.0],
                    "porosity": 1.0, "cavity": True, "openings": openings})
    return out


# ---------------------------------------------------------------------------------------------------- maths

def unity_euler(deg):
    """Unity's Quaternion.Euler(x, y, z): z about Z, then x about X, then y about Y, as [x, y, z, w]."""
    def axis(k, a):
        h = math.radians(a) / 2
        q = [0.0, 0.0, 0.0, math.cos(h)]
        q[k] = math.sin(h)
        return q
    return _qmul(axis(1, deg[1]), _qmul(axis(0, deg[0]), axis(2, deg[2])))


def _qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return [aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz]


def _rotate(q, v):
    x, y, z, w = q
    t = (2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0]))
    return (v[0] + w * t[0] + (y * t[2] - z * t[1]), v[1] + w * t[1] + (z * t[0] - x * t[2]),
            v[2] + w * t[2] + (x * t[1] - y * t[0]))


def _newell(points):
    n = [0.0, 0.0, 0.0]
    for a, b in zip(points, points[1:] + points[:1]):
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    return n


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _is_num(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool)


def _is_hex(s, n):
    return isinstance(s, str) and len(s) == n and all(c in "0123456789abcdef" for c in s)
