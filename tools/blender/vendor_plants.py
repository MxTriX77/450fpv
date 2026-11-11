"""Physics metadata for the farmland's vendor trees and bushes (w4a-farmland-vegetation task 1.1; D-088, D-089).

    python tools/blender/vendor_plants.py <ID|variant|all> [--sheets]

The farmland's plants are the local store's prefabs (vendor/, D-089): their meshes never leave it, and nothing here
copies them. For every variant of assets-src/vendor_plants.json (an asset id per source family, each variant named
as its prefab, so the map export's ids are these), this reads the variant's params (assets-src/vegetation/
<variant>.json), checks every store file they pin by SHA-256, reads the prefab's LOD0 mesh as Unity draws it
(lib.vendor) and builds its physics (lib.plant; the change's design.md): the wood as bark or deadwood capsules, the
foliage as a compliant twig shell, the branch sprays as snag records or capsules, the wind volumes with the porosity
the foliage measures. The metadata is lib.meta's schema 1, validated, its capsules audited against the wood they
follow (lib.audit.run, AST-05's 5 cm, and the spec's 3 cm on members of 5 cm or more), and written to
unity/Assets/_Project/Art/Models/<class>/<ID>/<variant>.assetmeta.json with a .meta whose guid derives from its
path, so the sim's MapWorld finds it by the map row's class, asset and variant, and Unity imports it unchanged.

--sheets also writes each variant's review sheet (AST-12): the mesh, its textures as PNG and the metadata go to a
bundle under review-sheets/<ID>/_bundle/ (git-ignored, so the copied geometry stays local), and the pinned Blender
renders review-sheets/<ID>/<variant>.png from it (tools/blender/vendor_review.py). Exit code 1 on any problem; a
refused variant writes nothing."""
import argparse
import hashlib
import json
import math
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

from lib import audit, meta, params as params_lib, plant, vendor  # noqa: E402
from lib.geometry import Mesh  # noqa: E402

CATALOG = os.path.join("assets-src", "vendor_plants.json")
GENERATOR = "vendor_plant"
STORE = os.path.join("vendor", "world_final", "Farming_land")
MODELS = os.path.join("unity", "Assets", "_Project", "Art", "Models")
SHEETS = "review-sheets"
BLENDER_DEFAULT = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
RENDERERS = 3                 # Blender processes rendering review sheets at once (assumed: the laptop's GPU and RAM)
ROLES = ("wood", "foliage")
SPRAYS = ("none", "hooks", "branches")
SLOT_KEYS = {"wood": ("material", "role", "physics"),
             "foliage": ("material", "role", "material_sha256", "base_map", "base_map_sha256", "sprays")}
SPEC_MEMBER_RADIUS_M = 0.025  # spec scenario: trunks and limbs wider than 5 cm (assumed there)
SPEC_TOLERANCE_M = 0.03       # ... covered by capsules within 3 cm of the mesh (assumed there)
CUBE_MAX = audit.SHELL_NEIGHBOURHOOD_MAX
PROP_REACH_M = 0.127          # a 10-inch prop disc's radius: the drone part that meets most shapes at once (published)
SHELL_PAIRS = 6               # shell pieces within a prop's reach of any point: ContactModel.PairsPerShape (8) holds
                              # a part's pairs at once, a trunk and a branch take two (derived; more is ContactsLost)
TWIG_EDGE_M = 0.0025          # the twig row's edge_radius_m: the thinnest twig the shell stands for
EDGE_UNDER_M = 0.015          # V01's snag rule: a segment under 30 mm across is an edge
TWIG = "twig"
BURIED_M = 0.3                # wood wholly this far under the origin's ground is buried on any slope the map has
                              # (assumed: a 30 % slope across a 1 m trunk); it is left out of the fit and the audit

PARAMS = {
    "source.prefab": {"type": "str", "range": "a .prefab path under the store's root",
                      "default": "Assets/Farm_land/Prefabs/Trees/Pine002_M.prefab",
                      "effect": "the prefab the terrain places: its LOD group, its LOD0 renderer and transforms"},
    "source.prefab_sha256": {"type": "str", "range": "64 hex digits", "default": "",
                             "effect": "the prefab's bytes, checked before it is read"},
    "source.mesh_file": {"type": "str", "range": "the FBX or mesh asset the LOD0 renderer names",
                         "default": "Assets/Art/Environment/Vegetation/Trees/Pines/Pine_002_new/Pine_002_M.FBX",
                         "effect": "the mesh file, which must be the file of the renderer's mesh guid"},
    "source.mesh_sha256": {"type": "str", "range": "64 hex digits", "default": "",
                           "effect": "the mesh file's bytes, checked before it is read"},
    "source.mesh_name": {"type": "str", "range": "'' or the FBX mesh's name", "default": "",
                         "effect": "the mesh when the FBX's .meta names its meshes by hash ('' when it lists them)"},
    "slots": {"type": "list", "range": "one record per LOD0 material: wood {material, role, physics} or foliage "
                                       "{material, role, material_sha256, base_map, base_map_sha256, sprays}",
              "default": [],
              "effect": "what each submesh is: wood (tubes, capsules of that physics) or foliage (alpha-clipped "
                        "cards sampled at their base map's cutoff; sprays: none (leaf clusters), hooks or branches)"},
    "wood.step_m": {"type": "float", "range": (0.02, 0.5), "default": 0.1,
                    "effect": "geodesic spacing of the rings cut through the tubes (assumed: a ring per 10 cm "
                              "follows a 1 cm bend)"},
    "wood.section_tol_m": {"type": "float", "range": (0.005, 0.05), "default": 0.03,
                           "effect": "a segment's rings lie within this of its round capsule, and within half the "
                                     "radius on a thin member (the spec's 3 cm)"},
    "wood.max_len_m": {"type": "float", "range": (0.2, 5.0), "default": 2.0,
                       "effect": "longest capsule (published: V01's trunk capsules per 1-2 m)"},
    "wood.min_radius_m": {"type": "float", "range": (0.001, 0.05), "default": 0.005,
                          "effect": "members thinner get no capsule and join the shell's mass (published: V01's "
                                    "every member of 10 mm or more)"},
    "foliage.spacing_m": {"type": "float", "range": (0.01, 0.1), "default": 0.04,
                          "effect": "sample lattice on the cards (assumed: under the 5 cm porosity raster)"},
    "foliage.reach_m": {"type": "float", "range": (0.0, 0.3), "default": 0.1,
                        "effect": "a sample this far outside the shell still counts as held (derived: lib.audit's "
                                  "envelope tolerance, 3 N at the twig row's 30 N/m)"},
    "foliage.depths_m": {"type": "list", "range": "rising numbers in (0, 1]",
                         "default": [0.1, 0.15, 0.2, 0.25, 0.3, 0.35, 0.4, 0.5],
                         "effect": "the foliage half depths tried in turn; the first whose shell keeps at most 64 "
                                   "pieces in any 2 m cube is used (design 6)"},
    "foliage.max_radius_m": {"type": "float", "range": (0.2, 3.0), "default": 1.0,
                             "effect": "largest shell piece (assumed)"},
    "sprays.min_length_m": {"type": "float", "range": (0.1, 5.0), "default": 1.0,
                            "effect": "a spray shorter than this gets a hook only, never a branch (assumed)"},
    "sprays.slenderness": {"type": "float", "range": (20.0, 200.0), "default": 70.0,
                           "effect": "a branch under a spray is its length over this across at its base "
                                     "(assumed: conifer branches 60-80 long per unit diameter)"},
    "sprays.max_radius_m": {"type": "float", "range": (0.005, 0.1), "default": 0.03,
                            "effect": "thickest branch under a spray (assumed)"},
    "sprays.capsule_m": {"type": "float", "range": (0.2, 3.0), "default": 2.0,
                         "effect": "a branch's capsules are this long, tapering to its tip"},
    "wind.column": {"type": "bool", "range": "true or false", "default": True,
                    "effect": "a solid trunk column under the crown box (V01's convention); false for a bush"},
    "lod.fov_deg": {"type": "float", "range": (10.0, 120.0), "default": 60.0,
                    "effect": "the vertical field of view the LOD switch distances are given at (assumed: the "
                              "review camera's, design 8)"},
}


def check(params):
    """The slot records and the depth ladder."""
    problems = []
    for i, slot in enumerate(params["slots"]):
        role = slot.get("role") if isinstance(slot, dict) else None
        if role not in ROLES:
            problems.append(f"slots[{i}]: role must be one of {', '.join(ROLES)}")
            continue
        if set(slot) != set(SLOT_KEYS[role]):
            problems.append(f"slots[{i}]: a {role} slot holds exactly {', '.join(SLOT_KEYS[role])}")
        elif role == "foliage" and slot["sprays"] not in SPRAYS:
            problems.append(f"slots[{i}]: sprays must be one of {', '.join(SPRAYS)}")
    depths = params["foliage"]["depths_m"]
    if not (depths and all(isinstance(d, (int, float)) and 0 < d <= 1 for d in depths)
            and all(a < b for a, b in zip(depths, depths[1:]))):
        problems.append("foliage.depths_m must be rising numbers in (0, 1]")
    for key in ("prefab_sha256", "mesh_sha256"):
        value = params["source"][key]
        if len(value) != 64 or any(c not in "0123456789abcdef" for c in value):
            problems.append(f"source.{key} must be 64 hex digits")
    return problems


# --- the catalog ----------------------------------------------------------------------------------------------

def load_catalog(root):
    with open(os.path.join(root, CATALOG), encoding="utf-8") as f:
        catalog = json.load(f)
    if catalog.get("schema") != 1 or not isinstance(catalog.get("assets"), dict):
        raise SystemExit(f"{CATALOG}: expected schema 1 with an 'assets' object")
    return catalog


def selected(catalog, wanted):
    """[(asset id, entry, variant)] for 'all', an asset id or a variant."""
    out = []
    for asset_id in sorted(catalog["assets"]):
        entry = catalog["assets"][asset_id]
        for variant in entry["variants"]:
            if wanted in ("all", asset_id, variant):
                out.append((asset_id, entry, variant))
    if not out:
        raise SystemExit(f"{wanted} is no asset id or variant of {CATALOG}")
    return out


def params_path(root, entry, variant):
    return os.path.join(root, "assets-src", entry["class"], variant + ".json")


def store_path(root, relative):
    return os.path.join(root, STORE, *relative.split("/"))


# --- the build ------------------------------------------------------------------------------------------------

def build(root, asset_id, entry, variant, params):
    """(metadata, report lines, bundle) of one variant; raises ValueError or vendor.VendorError on any problem."""
    lines = []
    module = sys.modules[__name__]
    problems = params_lib.validate(module, params)
    if problems:
        raise ValueError("params: " + "; ".join(problems))
    source = params["source"]
    prefab = vendor.pinned(store_path(root, source["prefab"]), source["prefab_sha256"])
    mesh_file = vendor.pinned(store_path(root, source["mesh_file"]), source["mesh_sha256"])
    group = vendor.prefab_lods(prefab)
    renderers = group["lods"][0]["renderers"]
    if len(renderers) != 1:
        raise ValueError(f"{variant}: LOD0 has {len(renderers)} renderers; one is read")
    renderer = renderers[0]
    if renderer["mesh_guid"] != vendor.guid_of(mesh_file):
        raise ValueError(f"{variant}: the LOD0 renderer's mesh is not {source['mesh_file']}")
    raw, tris, uvs = vendor.read_mesh(mesh_file, renderer["mesh_file_id"], source["mesh_name"] or None)
    points = [vendor.apply(renderer["matrix"], p) for p in raw]
    materials = [_material_path(root, prefab, guid) for guid in renderer["materials"]]
    names = [os.path.splitext(os.path.basename(m))[0] for m in materials]
    slots = {s["material"]: s for s in params["slots"]}
    if sorted(slots) != sorted(set(names)) or len(params["slots"]) != len(slots):
        raise ValueError(f"{variant}: the params' slots {sorted(slots)} are not LOD0's materials {names}")
    lines.append(f"source {source['prefab']} LOD0 '{renderer['name']}': {len(points)} vertices, {len(tris)} "
                 f"triangles, materials {names}")
    started = time.perf_counter()

    # wood: the tubes' skeleton and capsules
    wood_tris = [t[:3] for t in tris if slots[names[t[3]]]["role"] == "wood"
                 and max(points[v][1] for v in t[:3]) >= -BURIED_M]
    wood_physics = sorted({s["physics"] for s in slots.values() if s["role"] == "wood"})
    if len(wood_physics) > 1:
        raise ValueError(f"{variant}: wood slots name several physics {wood_physics}; one per plant")
    wood_material = wood_physics[0] if wood_physics else "bark"
    w = params["wood"]
    wpoints, wtris = plant.weld(points, wood_tris)
    nodes, vertex_node = plant.skeleton(wpoints, wtris, w["step_m"]) if wtris else ([], {})
    chains = plant.members(nodes)
    vertices_of = {}
    for v, n in sorted(vertex_node.items()):
        vertices_of.setdefault(n, []).append(v)
    capsules, member_of, twigs = [], {}, []
    for m, member in enumerate(chains):
        tip = None
        if member["free_tip"] and len(member["nodes"]) >= 2:
            before = nodes[member["nodes"][-2]]["centre"]
            tip = max((wpoints[v] for v in vertices_of.get(member["nodes"][-1], [])),
                      key=lambda p: (math.dist(p, before), p), default=None)
        fitted = plant.fit_capsules(nodes, member, w["max_len_m"], w["min_radius_m"], w["section_tol_m"], tip)
        for n in member["nodes"][1:]:
            member_of[n] = m
        if fitted:
            for k, (a, b, r) in enumerate(fitted):
                capsules.append({"a": a, "b": b, "r": r, "member": m, "last": k == len(fitted) - 1,
                                 "free_tip": member["free_tip"]})
        else:
            twigs.append(member)
    capsuled = {c["member"] for c in capsules}
    for n, node in enumerate(nodes):  # a root node belongs to the first member leaving it
        if node["parent"] is None:
            first = next((m for m, member in enumerate(chains) if member["nodes"][0] == n), None)
            if first is not None:
                member_of[n] = first
    lines.append(f"wood ({wood_material}): {len(wtris)} triangles, {len(nodes)} rings in "
                 f"{sum(1 for n in nodes if n['parent'] is None)} systems, {len(chains)} members, "
                 f"{len(capsules)} capsules on {len(capsuled)} members, {len(twigs)} twigs under "
                 f"{w['min_radius_m'] * 2000:g} mm (into the shell)")

    # foliage: the drawn samples, by spray
    f = params["foliage"]
    samples, sprays = [], []
    for slot in params["slots"]:
        if slot["role"] != "foliage":
            continue
        material_path = materials[names.index(slot["material"])]
        vendor.pinned(material_path, slot["material_sha256"])
        maps = vendor.material_maps(material_path, _shader_source(root))
        base_map = vendor.pinned(store_path(root, slot["base_map"]), slot["base_map_sha256"])
        if maps["base_map"] != vendor.guid_of(base_map):
            raise ValueError(f"{variant}: {slot['material']}'s base map is not {slot['base_map']}")
        alpha = vendor.Alpha(base_map, maps["cutoff"] if maps["cutoff"] is not None else 0.0, maps["scale"],
                             maps["offset"])
        index = names.index(slot["material"])
        mine = [i for i, t in enumerate(tris) if names[t[3]] == slot["material"]]
        found, ranges = plant.opaque_samples(points, [tris[i][:3] for i in mine], [uvs[i] for i in mine],
                                             alpha.opaque, f["spacing_m"])
        base = len(samples)
        samples += found
        welded, wt = plant.weld(points, [tris[i][:3] for i in mine])
        for comp in plant.components(wt):
            spray = [p for i in comp for p in samples[base + ranges[i][0]:base + ranges[i][1]]]
            if spray:
                sprays.append({"slot": slot["material"], "mode": slot["sprays"], "samples": spray})
        lines.append(f"foliage {slot['material']}: {len(mine)} triangles, cutoff {maps['cutoff']}, "
                     f"{len(found)} drawn samples at {f['spacing_m'] * 100:g} cm, "
                     f"{sum(1 for s in sprays if s['slot'] == slot['material'])} sprays; slot index {index}")
    twig_points = [wpoints[v] for v, n in sorted(vertex_node.items()) if member_of.get(n) not in capsuled]
    mass = samples + twig_points

    # sprays: hooks, or branches where the mesh draws none
    anchor = _trunk_axis(capsules)
    s = params["sprays"]
    branches, hooks = _sprays(sprays, anchor, s, f["spacing_m"])
    audited = [meta.capsule(c["a"], c["b"], c["r"], wood_material) for c in capsules]
    for b in branches:
        capsules.extend(b)
    lines.append(f"sprays: {len(sprays)}, {len(hooks)} hooks, {len(branches)} branches "
                 f"({sum(len(b) for b in branches)} capsules) of at least {s['min_length_m']:g} m")

    # the shell, at the first depth whose pieces keep the neighbourhood rule
    pieces, depth, info, crowd = [], None, None, (0, None)
    for depth in f["depths_m"]:
        pieces, info = plant.shell(mass, depth, f["reach_m"], f["max_radius_m"])
        crowd = plant.most_in_cube([(_box(c, r)) for c, r in pieces])
        overlap = plant.most_near(pieces, PROP_REACH_M)
        if crowd[0] <= CUBE_MAX and overlap[0] <= SHELL_PAIRS:
            break
    worst, where = plant.standoff(pieces, mass, plant.icosphere_directions(2)) if pieces else (0.0, None)
    lines.append(f"shell (twig): {len(pieces)} pieces at foliage half depth {depth:g} m over {info['cells']} "
                 f"sample cells ({len(samples)} drawn samples, {len(twig_points)} twig vertices), "
                 f"{info['uncovered']} cells unheld; at most {overlap[0]} pieces within {PROP_REACH_M:g} m of one point "
                 f"(rule: {SHELL_PAIRS}) and {crowd[0]} meeting a 2 m cube (rule: "
                 f"{CUBE_MAX}); largest standoff {worst:.3f} m" + (f" on piece {where}" if where is not None else ""))
    if pieces and (crowd[0] > CUBE_MAX or overlap[0] > SHELL_PAIRS):
        raise ValueError(f"{variant}: at every depth tried, {crowd[0]} shell pieces meet a 2 m cube (rule "
                         f"{CUBE_MAX}) and {overlap[0]} lie within {PROP_REACH_M:g} m of one point (rule {SHELL_PAIRS})")
    if info and info["uncovered"]:
        raise ValueError(f"{variant}: {info['uncovered']} sample cells lie outside every shell piece")

    # wind volumes
    wind, wind_lines = _wind(samples, twig_points, wpoints, wtris, nodes, params["wind"]["column"])
    lines += wind_lines

    # snag records
    snag = _snag(capsules, wood_material, twigs, nodes, hooks)
    lines.append(f"snag: {sum(1 for r in snag if r['kind'] == 'edge')} edges, "
                 f"{sum(1 for r in snag if r['kind'] == 'hook')} hooks")

    collision = [meta.capsule(c["a"], c["b"], c["r"], wood_material) for c in capsules]
    collision += [{"shape": "sphere", "center": list(c), "radius": r, "material": TWIG, "landable": False}
                  for c, r in pieces]
    lod0 = _mesh(points, tris, names)
    lo, hi = lod0.bounds()
    lods, cull, frame_problems = _lods(root, prefab, group, params["lod"]["fov_deg"])
    if frame_problems:
        raise ValueError("; ".join(frame_problems))
    lines.append(f"frame: every LOD renderer's box matches the LOD group's centre and size within "
                 f"{vendor.GROUP_TOLERANCE_M * 1000:g} mm")
    data = {
        "schema": meta.SCHEMA, "id": asset_id, "variant": variant, "generator": GENERATOR,
        "params_sha256": _params_sha256(params), "units": meta.UNITS, "frame": meta.FRAME,
        "bounds": {"min": list(lo), "max": list(hi)},
        "lods": lods, "micro": [], "cull_distance_m": cull,
        "materials": [{"slot": n, "visual": n, "physics": slots[n]["physics"] if slots[n]["role"] == "wood"
                       else TWIG} for n in _unique(names)],
        "collision": collision, "gaps": [], "wind_volumes": wind, "ports": [], "wires": [], "holes": [],
        "textures": [], "snag": snag,
    }
    data = meta._rounded(data)
    lines.append(f"built in {time.perf_counter() - started:.1f} s")
    capsuled_faces = [t for t in wtris if all(member_of.get(vertex_node.get(v)) in capsuled for v in t)]
    bundle = {"points": points, "tris": tris, "uvs": uvs, "names": names, "materials": materials,
              "samples": samples, "wood_points": wpoints, "capsuled_faces": capsuled_faces,
              "audited_capsules": meta._rounded(audited), "wood_material": wood_material}
    return data, lines, bundle


def _params_sha256(params):
    from lib import catalog
    return catalog.params_sha256(params)


def _unique(names):
    out = []
    for n in names:
        if n not in out:
            out.append(n)
    return out


def _material_path(root, prefab, guid):
    """The store's material file of a guid, looked for in the prefab's project (its .meta files)."""
    project = os.path.join(root, STORE, "Assets")
    cache = _material_path.__dict__.setdefault("cache", {})
    if not cache:
        for folder, _, files in os.walk(project):
            for name in files:
                if name.endswith(".mat.meta"):
                    path = os.path.join(folder, name[:-5])
                    cache[vendor.guid_of(path)] = path
    if guid not in cache:
        raise ValueError(f"{prefab}: no material of guid {guid} in the store")
    return cache[guid]


def _shader_source(root):
    """guid -> the text of the store's shader of that guid, or None for one outside the store (URP's)."""
    cache = _shader_source.__dict__.setdefault("cache", {})
    if not cache:
        for folder, _, files in os.walk(os.path.join(root, STORE, "Assets")):
            for name in files:
                if name.endswith(".shader.meta"):
                    path = os.path.join(folder, name[:-5])
                    cache[vendor.guid_of(path)] = path

    def source(guid):
        if guid not in cache:
            return None
        with open(cache[guid], encoding="utf-8", errors="replace") as f:
            return f.read()

    return source


def _box(c, r):
    return (tuple(c[a] - r for a in range(3)), tuple(c[a] + r for a in range(3)))


def _trunk_axis(capsules):
    """The axis the sprays hang from: the segments of the capsules of the plant's thickest member."""
    if not capsules:
        return []
    thickest = max(capsules, key=lambda c: (c["r"], -c["member"]))["member"]
    return [(c["a"], c["b"]) for c in capsules if c["member"] == thickest]


def _nearest_on(axis, p):
    best = None
    for a, b in axis:
        ab = [b[k] - a[k] for k in range(3)]
        den = sum(x * x for x in ab)
        t = 0.0 if den == 0 else max(0.0, min(1.0, sum((p[k] - a[k]) * ab[k] for k in range(3)) / den))
        q = tuple(a[k] + ab[k] * t for k in range(3))
        d = math.dist(p, q)
        if best is None or d < best[0]:
            best = (d, q)
    return best[1] if best else None


def _sprays(sprays, axis, s, spacing):
    """(branches, hooks): each spray's centre line from the sample nearest the trunk axis; a spray of a 'branches'
    slot at least min_length long becomes a branch, capsules tapering from its base radius (length over
    slenderness, at most max_radius) to the twig edge at its tip, from the spray's own base by the trunk (not its
    axis, where every branch of a whorl would meet: decision 6's pair rule); every other spray a hook along its
    line. A spray whose line runs within 0.25 m of a longer one's ends (a crossed card
    of the same branch) adds nothing."""
    lines = []
    for spray in sprays:
        if spray["mode"] == "none":
            continue
        anchor = _nearest_on(axis, _mean(spray["samples"])) if axis else (0.0, 0.0, 0.0)
        line = plant.spine(spray["samples"], anchor, max(0.1, 3 * spacing))
        if line:
            lines.append((_length(line), spray["mode"], line, anchor))
    lines.sort(key=lambda t: (-t[0], t[2][0]))
    kept = []
    for length, mode, line, anchor in lines:
        if any(_near_line(line[0], k[2]) and _near_line(line[-1], k[2]) for k in kept):
            continue
        kept.append((length, mode, line, anchor))
    branches, hooks = [], []
    for length, mode, line, anchor in kept:
        if mode == "branches" and length >= s["min_length_m"] and axis:
            base = min(s["max_radius_m"], length / s["slenderness"] / 2)
            branches.append(_taper(line, base, TWIG_EDGE_M, s["capsule_m"]))
        else:
            hooks.append(line)
    return branches, hooks


def _taper(line, base, tip, step):
    """Capsules along a polyline, a new one where the run since the last reaches `step` and at its end, their
    radius falling from `base` to `tip` by the run to their middle."""
    total = _length(line)
    marks, since = [line[0]], 0.0
    for a, b in zip(line, line[1:]):
        since += math.dist(a, b)
        if since >= step or b is line[-1]:
            marks.append(b)
            since = 0.0
    out, run = [], 0.0
    for k, (a, b) in enumerate(zip(marks, marks[1:])):
        mid = run + math.dist(a, b) / 2
        r = base + (tip - base) * min(1.0, mid / total) if total > 0 else base
        out.append({"a": a, "b": b, "r": max(r, TWIG_EDGE_M), "member": -1, "last": k == len(marks) - 2,
                    "free_tip": True})
        run += math.dist(a, b)
    return out


def _near_line(p, line, reach=0.25):
    return any(plant.segment_distance(p, a, b) <= reach for a, b in zip(line, line[1:]))


def _mean(points):
    return tuple(sum(p[a] for p in points) / len(points) for a in range(3))


def _length(line):
    return sum(math.dist(a, b) for a, b in zip(line, line[1:]))


def _wind(samples, twig_points, wpoints, wtris, nodes, column):
    """The crown or bush box with its measured porosity, and the trunk column under it (design 7)."""
    mass = samples + twig_points
    if not mass:
        return [], ["wind: no foliage, no volume"]
    lo = [min(p[a] for p in mass) for a in range(3)]
    hi = [max(p[a] for p in mass) for a in range(3)]
    lo[1] = max(lo[1], 0.0)
    beta, shares = plant.porosity(mass, wtris, wpoints, lo, hi)
    out = [{"shape": "box", "center": [(lo[a] + hi[a]) / 2 for a in range(3)],
            "size": [hi[a] - lo[a] for a in range(3)], "rotation": [0.0, 0.0, 0.0, 1.0], "porosity": beta}]
    text = (f"wind: crown box {_fmt(lo)}..{_fmt(hi)}, optical porosity {beta:.3f} (opaque share by bearing "
            f"{', '.join(f'{s:.3f}' for s in shares)})")
    if column and lo[1] > 0.3 and nodes:
        trunk = [n for n in nodes if n["parent"] is not None and n["system"] == nodes[0]["system"]
                 and n["centre"][1] < lo[1]]
        if trunk:
            r = sorted(n["radius"] for n in trunk)[len(trunk) // 2]
            x = sum(n["centre"][0] for n in trunk) / len(trunk)
            z = sum(n["centre"][2] for n in trunk) / len(trunk)
            out.append({"shape": "box", "center": [x, lo[1] / 2, z], "size": [2 * r, lo[1], 2 * r],
                        "rotation": [0.0, 0.0, 0.0, 1.0], "porosity": 0.0})
            text += f"; trunk column 0..{lo[1]:.2f} m, {2 * r:.3f} m across, porosity 0"
    return out, [text]


def _snag(capsules, wood_material, twigs, nodes, hooks):
    """V01's rule (design 7): a capsule segment under 30 mm across an edge at its radius, the last segment of a
    member ending in a free tip a hook to that tip; a twig (no capsule) one record through its rings, a hook where
    free, an edge where others leave it, twig material, at its mean radius; a spray a hook along its line at the
    twig row's edge, twig material."""
    out = []
    for c in capsules:
        material = wood_material
        if c["last"] and c["free_tip"]:
            out.append({"kind": "hook", "points": [list(c["a"]), list(c["b"])], "radius_m": c["r"],
                        "material": material})
        elif c["r"] < EDGE_UNDER_M:
            out.append({"kind": "edge", "points": [list(c["a"]), list(c["b"])], "radius_m": c["r"],
                        "material": material})
    for member in twigs:
        line = [list(nodes[i]["centre"]) for i in member["nodes"]]
        radii = [nodes[i]["radius"] for i in member["nodes"][1:]]
        if len(line) >= 2:
            out.append({"kind": "hook" if member["free_tip"] else "edge", "points": line,
                        "radius_m": max(sum(radii) / len(radii), TWIG_EDGE_M) if radii else TWIG_EDGE_M,
                        "material": TWIG})
    for line in hooks:
        out.append({"kind": "hook", "points": [list(p) for p in line], "radius_m": TWIG_EDGE_M, "material": TWIG})
    return out


def _mesh(points, tris, names):
    """LOD0 as a lib.geometry.Mesh, a face per non-degenerate triangle, slot its material's name."""
    mesh = Mesh()
    mesh.vertices = [tuple(p) for p in points]
    for a, b, c, s in tris:
        pa, pb, pc = points[a], points[b], points[c]
        n = plant_cross(pb, pa, pc)
        length = math.sqrt(sum(x * x for x in n))
        if a == b or b == c or a == c or length < 1e-12:
            continue
        mesh.faces.append(([a, b, c], tuple(x / length for x in n), [(0.0, 0.0)] * 3, names[s]))
    return mesh


def plant_cross(b, a, c):
    u = [b[k] - a[k] for k in range(3)]
    v = [c[k] - a[k] for k in range(3)]
    return [u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]]


def _lods(root, prefab, group, fov_deg):
    """The prefab's LODs as metadata entries (each LOD's renderer names and triangles), the cull distance, and the
    problems where the boxes of all its renderers miss its LOD group's centre and size (lib.vendor.check_group: the
    frame, units and transforms as Unity draws them). A level shows from the distance its previous level's
    screen-relative height is reached, d = size / (2 h tan(fov/2))."""
    k = 2 * math.tan(math.radians(fov_deg) / 2)
    out, switch, boxes = [], 0.0, []
    for i, lod in enumerate(group["lods"]):
        names = "+".join(r["name"] for r in lod["renderers"])
        tris = 0
        for r in lod["renderers"]:
            path = _mesh_path(root, prefab, r["mesh_guid"])
            try:
                v, t, _ = vendor.read_mesh(path, r["mesh_file_id"])
            except vendor.VendorError:  # a hash-named FBX: the renderer bears its mesh's name (the friend's export)
                v, t, _ = vendor.read_mesh(path, r["mesh_file_id"], r["name"])
            tris += len(t)
            boxes.append(vendor.renderer_bounds(v, r["matrix"]))
        out.append({"mesh": names, "tris": tris, "switch_distance_m": switch, "thin_wire": False})
        switch = group["size"] / (k * lod["screen_relative_height"])
    return out, switch, vendor.check_group(group, boxes, os.path.basename(prefab))


def _mesh_path(root, prefab, guid):
    cache = _mesh_path.__dict__.setdefault("cache", {})
    if not cache:
        for folder, _, files in os.walk(os.path.join(root, STORE, "Assets")):
            for name in files:
                if name.lower().endswith((".fbx.meta", ".asset.meta")):
                    path = os.path.join(folder, name[:-5])
                    cache[vendor.guid_of(path)] = path
    return cache[guid]


def _fmt(p):
    return "(" + ", ".join(f"{v:.2f}" for v in p) + ")"


# --- the audits -----------------------------------------------------------------------------------------------

# --- writing --------------------------------------------------------------------------------------------------

def unity_meta(path, folder=False):
    """A .meta beside `path` with a guid derived from its repository path (stable across checkouts)."""
    relative = os.path.relpath(path, ROOT).replace(os.sep, "/")
    guid = hashlib.md5(("450fpv:" + relative).encode("utf-8")).hexdigest()
    if folder:
        text = (f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    else:
        text = (f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n"
                "  assetBundleName: \n  assetBundleVariant: \n")
    meta_path = path + ".meta"
    if not os.path.isfile(meta_path):
        with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)


def write(root, entry, asset_id, variant, data):
    folder = os.path.join(root, MODELS, entry["class"], asset_id)
    os.makedirs(folder, exist_ok=True)
    unity_meta(folder, folder=True)
    path = os.path.join(folder, variant + ".assetmeta.json")
    meta.write(path, data)
    unity_meta(path)
    return path


def write_bundle(root, asset_id, variant, data, bundle):
    """The review bundle (local only: review-sheets/ is ignored): mesh, base maps as PNG, samples, metadata."""
    from PIL import Image
    folder = os.path.join(root, SHEETS, asset_id, "_bundle")
    os.makedirs(os.path.join(folder, "textures"), exist_ok=True)
    slots = []
    for name, path in zip(bundle["names"], bundle["materials"]):
        maps = vendor.material_maps(path, _shader_source(root))
        texture, tint = None, _tint(path)
        guid = maps["base_map"]
        if guid:
            image_path = _texture_path(root, guid)
            if image_path:
                out = os.path.join(folder, "textures", os.path.basename(image_path) + ".png")
                if not os.path.isfile(out):
                    image = vendor._unity_texture(image_path) if image_path.endswith(".asset") else Image.open(image_path)
                    if image_path.endswith(".asset"):
                        image = image.transpose(Image.FLIP_TOP_BOTTOM)
                    image.convert("RGBA").save(out)
                texture = out
        slots.append({"name": name, "texture": texture, "cutoff": maps["cutoff"], "tint": tint,
                      "scale": list(maps["scale"]), "offset": list(maps["offset"])})
    record = {"variant": variant, "points": [list(p) for p in bundle["points"]],
              "tris": [list(t) for t in bundle["tris"]], "uvs": [[list(c) for c in t] for t in bundle["uvs"]],
              "slots": slots, "samples": [list(p) for p in bundle["samples"][::max(1, len(bundle["samples"]) // 40000)]],
              "metadata": data}
    path = os.path.join(folder, variant + ".json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(record, f)
    return path


def _tint(path):
    import re
    with open(path, encoding="utf-8") as f:
        m = re.search(r"- _BaseColor: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)", f.read())
    return [float(m.group(i)) for i in (1, 2, 3)] if m else [1.0, 1.0, 1.0]


def _texture_path(root, guid):
    cache = _texture_path.__dict__.setdefault("cache", {})
    if not cache:
        for folder, _, files in os.walk(os.path.join(root, STORE, "Assets")):
            for name in files:
                if name.endswith(".meta") and name.lower().rsplit(".", 2)[-2] in (
                        "png", "tif", "tga", "jpg", "asset", "psd"):
                    path = os.path.join(folder, name[:-5])
                    try:
                        cache[vendor.guid_of(path)] = path
                    except vendor.VendorError:
                        pass
    return cache.get(guid)


def render_sheets(root, asset_id, bundles):
    blender = os.environ.get("BLENDER") or BLENDER_DEFAULT
    out = os.path.join(root, SHEETS, asset_id)
    done = subprocess.run([blender, "--background", "--factory-startup", "--python",
                           os.path.join(HERE, "vendor_review.py"), "--", out] + bundles,
                          capture_output=True, text=True, encoding="utf-8", errors="replace")
    for line in done.stdout.splitlines():
        if line.startswith("[review]"):
            print(line)
    if done.returncode != 0:
        print(done.stdout[-4000:], done.stderr[-4000:])
        raise SystemExit(f"vendor_review.py failed for {asset_id} (exit {done.returncode})")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("what", help="an asset id, a variant, or all")
    parser.add_argument("--sheets", action="store_true", help="also render the review sheets (pinned Blender)")
    args = parser.parse_args(argv)
    catalog = load_catalog(ROOT)
    material_ids = meta.load_physics_materials(ROOT)
    failed, bundles = False, {}
    for asset_id, entry, variant in selected(catalog, args.what):
        with open(params_path(ROOT, entry, variant), encoding="utf-8") as f:
            params = json.load(f)
        try:
            data, lines, bundle = build(ROOT, asset_id, entry, variant, params)
        except (ValueError, vendor.VendorError) as e:
            print(f"{variant}: REFUSED: {e}")
            failed = True
            continue
        for line in lines:
            print(f"{variant}: {line}")
        problems = meta.validate(data, material_ids)
        problems_wood, audit_lines = audit_wood(bundle, data)
        for line in audit_lines:
            print(f"{variant}: {line}")
        problems += problems_wood
        if problems:
            for p in problems:
                print(f"{variant}: PROBLEM: {p}")
            failed = True
            continue
        path = write(ROOT, entry, asset_id, variant, data)
        print(f"{variant}: wrote {os.path.relpath(path, ROOT)}: {len(data['collision'])} shapes, "
              f"{len(data['wind_volumes'])} wind volumes, {len(data['snag'])} snag records")
        if args.sheets:
            bundles.setdefault(asset_id, []).append(write_bundle(ROOT, asset_id, variant, data, bundle))
    from concurrent.futures import ThreadPoolExecutor
    with ThreadPoolExecutor(RENDERERS) as pool:  # one Blender per asset id, a few at once
        list(pool.map(lambda item: render_sheets(ROOT, *item), sorted(bundles.items())))
    return 1 if failed else 0


def audit_wood(bundle, data):
    """(problems, lines): the wood capsules against the faces of the members they follow (design 4), reported:
    lib.audit.run (AST-05) on a metadata of those capsules over a mesh of those faces, planted, both ways unsigned:
    its count over 5 cm, the farthest capsule surface from the mesh, the farthest wood from every capsule, and the
    spec's measure on the capsules of members 5 cm or more across. Nothing is refused here: the lobes of scanned
    wood and root flares, which a round capsule stands for by their mean, are a measured deviation the design
    records (risk 6). The sprays' branches follow no wood face and are not in it."""
    faces = bundle.get("capsuled_faces")
    if not faces:
        return [], ["audit: no wood capsules"]
    mesh = Mesh()
    mesh.vertices = [tuple(p) for p in bundle["wood_points"]]
    for a, b, c in faces:
        pa, pb, pc = mesh.vertices[a], mesh.vertices[b], mesh.vertices[c]
        n = plant_cross(pb, pa, pc)
        length = math.sqrt(sum(x * x for x in n))
        if length >= 1e-12:
            mesh.faces.append(([a, b, c], tuple(x / length for x in n), [(0.0, 0.0)] * 3, "wood"))
    capsules = bundle["audited_capsules"]
    sub = dict(data, collision=capsules,
               materials=[{"slot": "wood", "visual": "wood", "physics": bundle["wood_material"]}],
               gaps=[], wires=[], micro=[])
    started = time.perf_counter()
    strict, lines = audit.run(sub, mesh, planted=True)
    thick = [i for i, c in enumerate(capsules) if c["radius"] >= SPEC_MEMBER_RADIUS_M]
    worst, worst_any, mesh_line = 0.0, 0.0, None
    for line in lines:
        if line.startswith("audit collision[") and "->mesh" in line:
            i = int(line.split("[", 1)[1].split("]", 1)[0])
            value = float(line.split("->mesh ", 1)[1].split(" mm", 1)[0]) / 1000
            worst_any = max(worst_any, value)
            if i in thick:
                worst = max(worst, value)
        if line.startswith("audit mesh:"):
            mesh_line = line[len("audit mesh: "):]
    return [], [
        f"AST-05 (reported; lib.audit.run, {len(capsules)} capsules over {len(mesh.faces)} faces of the members they "
        f"follow, {time.perf_counter() - started:.0f} s): {len(strict)} findings over 5 cm; capsule -> mesh at most "
        f"{worst_any * 1000:.1f} mm; " + (mesh_line or "no mesh line"),
        f"spec (reported): {len(thick)} capsules of members 5 cm or more across, the farthest of their surfaces "
        f"{worst * 1000:.1f} mm from the mesh (spec {SPEC_TOLERANCE_M * 1000:g} mm)"]


if __name__ == "__main__":
    sys.exit(main())
