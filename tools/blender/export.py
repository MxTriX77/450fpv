"""Exports every Blender asset source to a game .glb with its LODs, and prints the budget report (asset-pipeline spec).

    python tools/blender/export.py [--build] [asset ...]

Run from any Python, it restarts itself in Blender, headless with factory settings (BLENDER names blender.exe; the
default is Blender 5.2's install path). For each source blender/<class>/<asset>.blend, or only the named assets:
- LOD objects are <asset>_LOD0, _LOD1, ... A level the class needs but the source lacks is made from LOD0 with a
  Decimate modifier at export (never saved), up to LOD2. A tree's impostor level is authored by its builder.
- It writes game/assets/models/<class>/<asset>.glb: the LOD meshes with named materials and no images. Textures live
  once in game/assets/materials/<material>.tres and game/assets/textures/, never inside a .glb.
- It checks the asset's game/assets/catalog.json entry against the one its builder stored in the .blend.
- It prints triangles per LOD and texture sizes against the class budget, and exits 1 on any excess or error.
With --build it first runs each builder, blender/<class>/<asset>.py, which rebuilds and saves its .blend.
"""
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BLENDER = os.environ.get("BLENDER", r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe")

try:
    import bpy
except ImportError:
    command = [BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "--python", os.path.abspath(__file__), "--"]
    sys.exit(subprocess.call(command + sys.argv[1:]))

SOURCES = os.path.join(ROOT, "blender")
MODELS = os.path.join(ROOT, "game", "assets", "models")
MATERIALS = os.path.join(ROOT, "game", "assets", "materials")
CATALOG = os.path.join(ROOT, "game", "assets", "catalog.json")
LIBRARY = "res://assets/textures/"  # shared CC0 texture sets, capped at 2K by tools/assets/fetch_cc0.py
LIBRARY_PX = 2048
# Class: (LOD0 triangles, LOD levels including LOD0, texture px). Trees: 3 levels plus a far impostor as the 4th.
BUDGETS = {
    "vehicle": (40_000, 3, 2048),
    "house": (60_000, 3, 2048),
    "block": (200_000, 3, 2048),
    "tree": (30_000, 4, 2048),
    "prop": (5_000, 1, 1024),
}
DECIMATE = {1: 0.5, 2: 0.2}  # LOD0 triangle ratio of a generated level


def sources(names):
    found = []
    for cls in sorted(os.listdir(SOURCES)):
        folder = os.path.join(SOURCES, cls)
        if os.path.isdir(folder):
            found += [(cls, f[:-3]) for f in sorted(os.listdir(folder)) if f.endswith(".py")]
    missing = set(names) - {a for _, a in found}
    if missing:
        sys.exit(f"ERROR: no builder blender/<class>/<asset>.py for {', '.join(sorted(missing))}")
    return [s for s in found if not names or s[1] in names]


def build(cls, asset):
    script = os.path.join(SOURCES, cls, asset + ".py")
    exec(compile(open(script, encoding="utf-8").read(), script, "exec"), {"__name__": "__main__", "__file__": script})


def triangles(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    count = len(mesh.loop_triangles)
    evaluated.to_mesh_clear()
    return count


def textures(material):
    """(texture path, px) for each image the material's Godot .tres uses; None when there is no .tres yet."""
    tres = os.path.join(MATERIALS, material + ".tres")
    if not os.path.isfile(tres):
        return None
    found = []
    for path in re.findall(r'path="(res://[^"]+\.(?:png|jpg|jpeg|webp|exr|hdr))"', open(tres, encoding="utf-8").read()):
        image = bpy.data.images.load(os.path.join(ROOT, "game", path[len("res://"):]), check_existing=True)
        found.append((path, max(image.size)))
    return found


def entry_text(asset, entry):
    """The builder's catalog entry in catalog.json's layout, one shape per line, ready to paste."""
    one = lambda v: json.dumps(v, ensure_ascii=False).replace("{", "{ ").replace("}", " }")
    lines = []
    for key in ("type", "scene", "visual_only", "material", "collision", "wind_volume", "snag_hazard", "wind_porosity", "gaps"):
        if key not in entry:
            continue
        value = entry[key]
        if isinstance(value, list) and value and key != "wind_volume":
            items = ",\n".join(f"        {one(v)}" for v in value)
            lines.append(f'      "{key}": [\n{items}\n      ]')
        elif isinstance(value, list):
            lines.append(f'      "{key}": [ {", ".join(one(v) for v in value)} ]' if value else f'      "{key}": []')
        else:
            lines.append(f'      "{key}": {one(value)}')
    return f'    "{asset}": {{\n' + ",\n".join(lines) + "\n    }"


def same(a, b):
    if isinstance(a, dict) and isinstance(b, dict):
        return a.keys() == b.keys() and all(same(a[k], b[k]) for k in a)
    if isinstance(a, list) and isinstance(b, list):
        return len(a) == len(b) and all(same(x, y) for x, y in zip(a, b))
    if isinstance(a, (int, float)) and isinstance(b, (int, float)) and not isinstance(a, bool) and not isinstance(b, bool):
        return abs(a - b) <= 1e-6
    return a == b


def export(cls, asset, catalog):
    """Exports one asset. Returns its report line and its problems."""
    bpy.ops.wm.open_mainfile(filepath=os.path.join(SOURCES, cls, asset + ".blend"))
    scene = bpy.context.scene
    problems = []
    budget_class = scene.get("budget_class")
    if budget_class not in BUDGETS:
        return f"{cls}/{asset}: no budget class", [f"{asset}: budget_class {budget_class!r} is not one of {', '.join(BUDGETS)}"]
    max_tris, min_levels, max_px = BUDGETS[budget_class]

    lods = {}
    for obj in scene.objects:
        match = re.fullmatch(re.escape(asset) + r"_LOD(\d+)", obj.name)
        if obj.type == "MESH" and match:
            lods[int(match.group(1))] = obj
    if 0 not in lods:
        return f"{cls}/{asset}: no LOD0", [f"{asset}: the source has no mesh object {asset}_LOD0"]
    for level in range(1, min(min_levels, 3)):
        if level not in lods:
            copy = lods[0].copy()
            copy.data = lods[0].data.copy()
            copy.name = copy.data.name = f"{asset}_LOD{level}"
            copy.modifiers.new("lod", "DECIMATE").ratio = DECIMATE[level]
            scene.collection.objects.link(copy)
            lods[level] = copy
    levels = sorted(lods)
    if levels != list(range(len(levels))):
        problems.append(f"{asset}: LOD levels {levels} are not numbered 0, 1, 2, ...")
    tris = [triangles(lods[n]) for n in levels]
    if tris[0] > max_tris:
        problems.append(f"{asset}: LOD0 has {tris[0]} triangles, the {budget_class} budget is {max_tris}")
    if len(levels) < min_levels:
        problems.append(f"{asset}: {len(levels)} LOD levels, a {budget_class} needs {min_levels}")

    notes = []
    for material in sorted({slot.material.name for n in levels for slot in lods[n].material_slots if slot.material}):
        maps = textures(material)
        if maps is None:
            notes.append(f"{material} flat")
            continue
        notes.append(f"{material} " + " ".join(f"{px}" for _, px in maps))
        for path, px in maps:
            limit = LIBRARY_PX if path.startswith(LIBRARY) else max_px
            if px > limit:
                problems.append(f"{asset}: {material} uses {path} at {px} px, the limit is {limit}")
    status = "OVER" if problems else "OK"

    entry = catalog.get("assets", {}).get(asset)
    scene_path = f"res://assets/models/{cls}/{asset}.glb"
    stored = json.loads(scene["catalog_entry"]) if "catalog_entry" in scene else None
    if stored is None:
        problems.append(f"{asset}: the source stores no catalog entry; rebuild it with --build")
    elif stored.get("scene") != scene_path:
        problems.append(f"{asset}: its builder's scene is {stored.get('scene')}, expected {scene_path}")
    elif entry is None or not same(entry, stored):
        state = "missing" if entry is None else "different"
        problems.append(f"{asset}: catalog.json entry is {state}; the builder's entry:\n{entry_text(asset, stored)}")

    for obj in scene.objects:
        obj.select_set(obj in lods.values())
    out = os.path.join(MODELS, cls, asset + ".glb")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=out, export_format="GLB", use_selection=True, export_apply=True, export_yup=True,
        export_image_format="NONE", export_materials="EXPORT", export_texcoords=True, export_normals=True,
        export_tangents=False, export_attributes=False, export_vertex_color="NONE", export_extras=False,
        export_animations=False, export_skins=False, export_morph=False, export_cameras=False, export_lights=False)

    counts = "  ".join(f"{t:>7}" for t in tris) + "        -" * (4 - len(tris))
    line = f"{cls + '/' + asset:<34} {budget_class:<8} {counts}   {max_tris:>7} {len(levels)}/{min_levels}  " \
           f"{', '.join(notes) or '-'} (max {max_px})  {status}"
    return line, problems


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    rebuild = "--build" in args
    todo = sources([a for a in args if a != "--build"])
    if rebuild:
        for cls, asset in todo:
            build(cls, asset)
    catalog = json.load(open(CATALOG, encoding="utf-8"))
    print(f"{'asset':<34} {'class':<8} {'LOD0':>7}  {'LOD1':>7}  {'LOD2':>7}  {'LOD3':>7}   {'budget':>7} lvls  textures px")
    problems = []
    for cls, asset in todo:
        line, found = export(cls, asset, catalog)
        print(line)
        problems += found
    for p in problems:
        print(f"ERROR: {p}")
    print(f"export: {len(todo)} asset(s), {len(problems)} problem(s)")
    sys.exit(1 if problems else 0)


main()
