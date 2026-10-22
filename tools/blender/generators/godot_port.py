"""godot_port: a Godot stand-in, the first attempt's model and its catalog entry carried into asset space as they are
(p6-test-range design 1-5; D-040: no look pass, no rework; D-027: read at a pinned commit, never merged).

Params (assets-src/standins/<variant>.json) name the model, its commit, its LFS object and its catalog key
(lib.godot reads them read-only), the LODs to port and the ground the asset cuts. The rest is the pipeline's: the
FBX, the Unity prefab, the LOD group and the review sheet, as for a generated asset.

Mesh: the model's own LOD meshes `<model>_LOD<n>` (the first attempt drew them or decimated them at export), each
glTF triangle a face, through lib.godot's mirror with its corners' normals mirrored too, so it shades as Godot drew
it, and its UVs as Blender wrote them (glTF's V is 1 - Blender's): metres on the structures, Godot's own on the bark
and the leaf cards. One material slot per model material, named as Godot named it; the catalog maps each to a
recipe. The levels ported are the first len(lod.switch_m), switching at those distances and culled at lod.cull_m:
for the design's rule (no impostors, the cull at Godot's last lod_switch_m) that is every level but Godot's last, a
tree's far card or a structure's coarsest mesh, with Godot's lod_switch_m but its last as the switches and its last
as the cull.

Metadata (lib.godot): the entry's collision shapes, gaps, wind volumes and porosity through the same mirror, every
shape not landable (the catalog has no such flag); the snag flag, which asset space has no field for, goes in the
`stand_in` record beside the source, and `snag` stays empty: Godot's flag says the whole object catches, not where.
Every physics id is the entry's own (masonry, timber, surface:belt_bare), each one in assets-src/physics_materials.json,
so nothing is mapped. A slot's physics is the entry's material. `holes` (Godot's frame) are the cuts the asset stands
in, as E01 writes its run.

STAND_IN tells build.py to audit the collision against the LOD0 mesh's bounds (lib.audit.stand_in), not to the full
audit's 5 cm both ways, which the first attempt's collision was never built to, and to skip the hole contract, which
asks for E01's fillers; the review sheet's overlay shows how the two agree."""
import math
import os
import re

from lib import glb, godot
from lib.geometry import Mesh

STAND_IN = True
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
HOLE_KEYS = ("id", "centre_line", "top_width_m", "depth_m", "band_width_m")
PARAMS = {
    "source.commit": {"type": "str", "range": "a 40-digit commit of refs/godot/main",
                      "default": "83401382e8b733389982997180cc181b063f61ae",
                      "effect": "the first attempt's commit the model and its catalog entry are read at (D-027)"},
    "source.model": {"type": "str", "range": "a .glb path at that commit",
                     "default": "game/assets/models/structures/house_adobe.glb",
                     "effect": "the model, a Git LFS pointer at the commit; its stem names its LOD meshes"},
    "source.model_sha256": {"type": "str", "range": "64 hex digits",
                            "default": "e250727975007593e51cd99781250dcbee8e679d839e66bcef753e984cc9651b",
                            "effect": "the model's LFS object id, checked against the pointer and the object's bytes"},
    "source.catalog_key": {"type": "str", "range": "an asset of game/assets/catalog.json at the commit",
                           "default": "house_adobe",
                           "effect": "the entry the collision, gaps, wind volumes, porosity and snag flag come from"},
    "lod.switch_m": {"type": "list", "range": "numbers rising from 0, at most one per model LOD", "default": [0.0, 45.0],
                     "effect": "a ported level per entry, LOD0 first, each shown from its distance (published: "
                               "Godot's lod_switch_m but its last)"},
    "lod.cull_m": {"type": "float", "range": (1.0, 5000.0), "default": 400.0,
                   "effect": "distance beyond which the asset is culled (published: Godot's last lod_switch_m; "
                             "assumed for a model of one level; the test range sets 400 m, its map's sight lines)"},
    "holes": {"type": "list", "range": "records of id, centre_line, top_width_m, depth_m, band_width_m", "default": [],
              "effect": "the cuts in the ground the asset stands in, in Godot's frame (lib.meta holes)"},
}


def check(params):
    """Cross-parameter rules once every key has its type: hex ids, a .glb, rising switches below the cull, and hole
    records with their keys."""
    source, lod, problems = params["source"], params["lod"], []
    if not re.fullmatch(r"[0-9a-f]{40}", source["commit"]):
        problems.append(f"'source.commit' = {source['commit']!r} must be a full 40-digit commit")
    if not re.fullmatch(r"[0-9a-f]{64}", source["model_sha256"]):
        problems.append(f"'source.model_sha256' = {source['model_sha256']!r} must be 64 hex digits")
    if not source["model"].endswith(".glb"):
        problems.append(f"'source.model' = {source['model']!r} must be a .glb")
    switches = lod["switch_m"]
    if not (switches and all(isinstance(s, (int, float)) and not isinstance(s, bool) for s in switches)
            and switches[0] == 0 and all(a < b for a, b in zip(switches, switches[1:]))):
        problems.append(f"'lod.switch_m' = {switches} must be numbers rising from 0")
    elif lod["cull_m"] <= switches[-1]:
        problems.append(f"'lod.cull_m' = {lod['cull_m']} must lie beyond the last switch, {switches[-1]}")
    for i, h in enumerate(params["holes"]):
        if not (isinstance(h, dict) and set(h) == set(HOLE_KEYS)):
            problems.append(f"'holes[{i}]' must hold exactly {', '.join(HOLE_KEYS)}")
    return problems


def build(params):
    source, lod = params["source"], params["lod"]
    commit, path, key = source["commit"], source["model"], source["catalog_key"]
    entry = godot.catalog_entry(ROOT, commit, key)
    scene = "res://" + path[len("game/"):] if path.startswith("game/") else path
    if entry.get("scene") != scene:
        raise ValueError(f"{key}: the Godot entry's scene is {entry.get('scene')!r}, not the model {scene!r}")
    model = glb.read(godot.lfs_object(ROOT, commit, path, source["model_sha256"]))
    nodes = glb.node_meshes(model)
    stem = os.path.splitext(os.path.basename(path))[0]
    levels = [n for n in range(len(nodes)) if f"{stem}_LOD{n}" in nodes]
    if levels != list(range(len(nodes))):
        raise ValueError(f"{path}: expected meshes {stem}_LOD0.. for its {len(nodes)} nodes, got {sorted(nodes)}")
    ported = len(lod["switch_m"])
    if ported > len(levels):
        raise ValueError(f"{path}: lod.switch_m asks for {ported} levels, the model holds {len(levels)}")
    lods, skipped = zip(*(_mesh(nodes[f"{stem}_LOD{n}"]) for n in range(ported)))
    used = {slot for mesh in lods for slot in mesh.slots()}
    slots = [m["name"] for m in glb.materials(model) if m["name"] in used]
    collision, gaps = godot.collision(entry), [godot.gap(g) for g in entry.get("gaps", [])]
    wind, holes = godot.wind_volumes(entry), [godot.hole(h) for h in params["holes"]]
    godot_lods = entry.get("lod_switch_m", [])
    return {
        "lods": list(lods),
        "switch_distances_m": [float(s) for s in lod["switch_m"]],
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": slot, "physics": entry["material"]} for slot in slots],
        "collision": collision, "gaps": gaps, "wind_volumes": wind, "holes": holes, "snag": [],
        "stand_in": {"source": {"commit": commit, "model": path, "model_sha256": source["model_sha256"],
                                "catalog_key": key},
                     "snag_hazard": bool(entry["snag_hazard"])},
        "report": [
            f"stand-in {key}: {path} at {commit[:7]}, LFS {source['model_sha256'][:12]}, {len(levels)} model LODs, "
            f"{ported} ported, {sum(skipped)} degenerate triangles skipped",
            f"Godot lod_switch_m {godot_lods} -> switches {lod['switch_m']} m, cull {lod['cull_m']:g} m"
            + (f"; model LOD{ported}..LOD{len(levels) - 1} not ported" if ported < len(levels) else ""),
            f"{len(collision)} collision shapes ({_kinds(collision)}), {len(gaps)} gaps, {len(wind)} wind volumes "
            f"at porosity {entry['wind_porosity']:g}, snag_hazard {entry['snag_hazard']}, {len(holes)} holes, "
            f"physics {entry['material']}",
        ],
    }


def _mesh(primitives):
    """One LOD in asset space and the count of degenerate triangles left out: every triangle a face, its outward
    normal its glTF winding's (counter-clockwise from the front) mirrored, its corners' UVs and normals carried."""
    mesh, skipped = Mesh(), 0
    for prim in primitives:
        base = len(mesh.vertices)
        positions = prim["positions"]
        for p in positions:
            mesh.vertex(godot.point(p))
        normals = [godot.point(n) for n in prim["normals"]]
        uvs = [(u, 1.0 - v) for u, v in prim["uvs"]]
        indices = prim["indices"]
        for t in range(0, len(indices), 3):
            corner = indices[t:t + 3]
            a, b, c = (positions[i] for i in corner)
            n = _cross([b[k] - a[k] for k in range(3)], [c[k] - a[k] for k in range(3)])
            length = math.sqrt(sum(x * x for x in n))
            if len(set(corner)) < 3 or length < 1e-12:
                skipped += 1
                continue
            shading = [normals[i] for i in corner]
            mesh.face([base + i for i in corner], godot.point([x / length for x in n]), prim["material"],
                      [uvs[i] for i in corner],
                      shading if all(sum(x * x for x in v) > 1e-12 for v in shading) else None)
    return mesh, skipped


def _kinds(shapes):
    counts = {}
    for s in shapes:
        counts[s["shape"]] = counts.get(s["shape"], 0) + 1
    return ", ".join(f"{n} {kind}" for kind, n in sorted(counts.items()))


def _cross(a, b):
    return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
