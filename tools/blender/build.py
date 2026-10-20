"""Builds assets from their params in headless Blender (change setup-asset-pipeline, design decisions 4 to 7).

    blender --background --factory-startup --python tools/blender/build.py -- <ID> [<ID> ...]
    blender --background --factory-startup --python tools/blender/build.py -- all

For every variant of each asset id in assets-src/catalog.json, it checks the variant's params
(assets-src/<class>/<variant>.json) against the generator's PARAMS table (lib/params.py), runs the generator and
prints its report lines (counts and fit times), validates the metadata (lib/meta.py; each slot's visual material the
variant's, the asset's `materials` overridden by its `variant_materials`, lib/catalog.py) and audits the collision against
the LOD0 mesh (lib/audit.py, with the soft tops of assets-src/physics_materials.json, the card slots of the
alpha-clipped recipes and the generator's stated card reach for the shell's cards; a generator's visual-only micro
meshes are exported beside their LOD and skipped by the audit), and only then
writes unity/Assets/_Project/Art/Models/<class>/<ID>/<variant>.fbx and <variant>.assetmeta.json, both in Unity's
frame (lib/frame.py), the FBX staged beside its final name and moved once it reads back as the metadata says. A
variant whose params set `impostor: true` (p1-v01-locust design decision 3) also gets <variant>_impostor.png and
<variant>_impostor_normal.png, the albedo atlas and the normal atlas with the occlusion in its alpha that
lib/impostor.py renders from LOD0 after the FBX, staged the same way, and its metadata lists the impostor as the last
LOD; atlases the metadata records as up to date are not rendered again, and those the params no longer ask for are
removed. A staged file replaces its final name, tried again for a few seconds while Windows denies access to it. A
rejected build changes no file. It refuses any Blender other than the pinned major.minor. Exit code 1 on any problem;
nothing is hand-edited.
"""
import argparse
import importlib
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(os.path.dirname(HERE))
BLENDER_PIN = (5, 2)  # the major.minor of docs/environment.md's pinned Blender; change both together
REPLACE_TRIES, REPLACE_WAIT_S = 20, 0.5  # Windows denies replacing a file another process holds open (task 1.2l: the
                                        # second build of V13 hit "Access is denied" on V13_low.fbx); assumed: an
                                        # indexer's or a scanner's hold on a fresh file ends within 10 s
sys.path.insert(0, HERE)  # lib/ and generators/ import from here inside Blender


def check_version(version, pin=BLENDER_PIN):
    """Refuses any Blender whose major.minor differs from the pin, so exports stay reproducible (decision 4)."""
    if tuple(version[:2]) != tuple(pin):
        raise RuntimeError(f"Blender {'.'.join(str(v) for v in version)} is not the pinned {pin[0]}.{pin[1]}.x "
                           "(docs/environment.md; BLENDER_PIN in build.py)")


def build_variant(root, catalog_entry, asset_id, variant, material_ids):
    """Builds one variant and returns its report line, printing the audit's measurements. Raises on any problem,
    all of them found before the first file is written except the read-back of the staged FBX and the impostor's
    render, both staged and removed on failure. `material_ids` is the closed physics-material list
    (lib.meta.load_physics_materials)."""
    from lib import audit, catalog, export, impostor, meta, params as params_lib
    params_path = catalog.params_path(root, catalog_entry, variant)
    params = catalog.load_params(params_path)
    generator = importlib.import_module(f"generators.{catalog_entry['generator']}")
    _refuse(variant, params_lib.validate(generator, params), f"params {os.path.relpath(params_path, root)}: ")
    built = generator.build(params)
    for line in built.get("report", ()):
        print(f"{variant}: {line}")
    out_dir = catalog.output_dir(root, catalog_entry, asset_id)
    meta_path = os.path.join(out_dir, variant + ".assetmeta.json")
    atlas_path = os.path.join(out_dir, variant + "_impostor.png")
    normals_path = os.path.join(out_dir, variant + "_impostor_normal.png")
    visual_by_slot = catalog.visuals(catalog_entry, variant)
    switch, materials, impostor_lod = impostor.switch_distance(params), None, None
    if switch is not None:  # the impostor step (p1-v01-locust design decision 3): the record now, the render after
        lod0 = built["lods"][0]
        materials = impostor.slot_materials(root, visual_by_slot, lod0.slots())
        impostor_lod = impostor.lod(variant, switch, lod0, materials)
        record = impostor_lod["impostor"]
        old = impostor.recorded(meta_path, atlas_path, normals_path, record["inputs_sha256"])
        if old:  # up to date: the atlases stand
            record.update({key: old[key] for key in ("atlas_sha256", "normals_sha256")})
    data = meta.assemble(asset_id, variant, catalog_entry["generator"], params, built, visual_by_slot, impostor_lod)
    _refuse(variant, meta.validate(data, material_ids))
    started = time.perf_counter()
    problems, lines = audit.run(data, built["lods"][0], soft=audit.soft_materials(root),
                                card_visuals=catalog.card_visuals(root), card_reach_m=built.get("card_reach_m"))
    for line in lines:
        print(f"{variant}: {line}")
    print(f"{variant}: audit took {time.perf_counter() - started:.1f} s")
    _refuse(variant, problems)
    export.reset_scene()
    for n, mesh in enumerate(built["lods"]):
        export.add_lod(f"{variant}_LOD{n}", mesh)
    for entry, micro in zip(data["micro"], built.get("micro", [])):  # extra mesh objects beside their LOD
        export.add_lod(entry["name"], micro["mesh"])
    fbx_path = os.path.join(out_dir, variant + ".fbx")
    staged = os.path.join(out_dir, variant + ".staged.fbx")
    staged_atlas = os.path.join(out_dir, variant + "_impostor.staged.png")
    staged_normals = os.path.join(out_dir, variant + "_impostor_normal.staged.png")
    fresh = not os.path.isdir(out_dir)
    os.makedirs(out_dir, exist_ok=True)
    try:
        export.export_fbx(staged)
        _refuse(variant, meta.check_against_fbx(data, staged))
        if impostor_lod is not None:
            record = data["lods"][-1]["impostor"]
            if record["atlas_sha256"] == impostor.NO_ATLAS:
                started = time.perf_counter()
                impostor.render(built["lods"][0], record, materials, staged_atlas, staged_normals)
                record["atlas_sha256"] = impostor.file_sha256(staged_atlas)
                record["normals_sha256"] = impostor.file_sha256(staged_normals)
                _refuse(variant, meta.validate(data, material_ids))
                print(f"{variant}: impostor rendered in {time.perf_counter() - started:.1f} s")
            card = record["card"]
            print(f"{variant}: impostor {record['atlas']} and {record['normals']} {record['size_px'][0]}x"
                  f"{record['size_px'][1]}, {record['views']} views at {record['elevation_deg']:g} deg, card "
                  f"{card['width_m']:.3f} x {card['height_m']:.3f} m from {card['bottom_m']:.3f} m, switch "
                  f"{data['lods'][-1]['switch_distance_m']:g} m, cull "
                  f"{data['cull_distance_m']:g} m")
        replace(staged, fbx_path)
        for staged_path, path in ((staged_atlas, atlas_path), (staged_normals, normals_path)):
            if impostor_lod is None and os.path.exists(path):
                os.remove(path)  # the params no longer ask for one
            elif os.path.exists(staged_path):
                replace(staged_path, path)
    finally:
        for path in (staged, staged_atlas, staged_normals):
            if os.path.exists(path):
                os.remove(path)
        if fresh and not os.listdir(out_dir):
            os.rmdir(out_dir)
    meta.write(meta_path, data)
    size = [round(data["bounds"]["max"][a] - data["bounds"]["min"][a], 4) for a in range(3)]
    tris = " / ".join(str(lod["tris"]) for lod in data["lods"])
    micro = "".join(f"  micro {m['name']} {m['count']} {m['kind']} {m['tris']} tris" for m in data["micro"])
    return (f"{variant}: {os.path.relpath(fbx_path, root)}  size {size} m  tris {tris}{micro}  "
            f"{len(data['collision'])} collision shapes  fbx {os.path.getsize(fbx_path) / 1048576:.1f} MB  "
            "metadata written")


def replace(staged, path, tries=REPLACE_TRIES, wait_s=REPLACE_WAIT_S):
    """os.replace(staged, path), tried again every `wait_s` seconds while Windows denies access (PermissionError), up
    to `tries` times; the last denial is raised."""
    for attempt in range(1, tries + 1):
        try:
            os.replace(staged, path)
            return
        except PermissionError:
            if attempt == tries:
                raise
            time.sleep(wait_s)


def _refuse(variant, problems, source=""):
    if problems:
        raise RuntimeError(f"{variant}: {source}" + "; ".join(problems))


def main(argv):
    import bpy
    from lib import catalog, meta
    parser = argparse.ArgumentParser(prog="build.py", description=__doc__.split("\n\n")[0])
    parser.add_argument("ids", nargs="+", metavar="ID", help="asset ids from assets-src/catalog.json, or 'all'")
    parser.add_argument("--root", default=REPO_ROOT, help=argparse.SUPPRESS)
    args = parser.parse_args(argv)
    try:
        check_version(bpy.app.version)
        cat = catalog.load(args.root)
        material_ids = meta.load_physics_materials(args.root)
        print(f"build.py: Blender {bpy.app.version_string}, root {args.root}")
        for asset_id in catalog.asset_ids(cat, args.ids):
            entry = cat["assets"][asset_id]
            for variant in entry["variants"]:
                print(build_variant(args.root, entry, asset_id, variant, material_ids))
    except KeyError as e:
        print(f"ERROR: missing key {e} (params, catalog or FBX)")
        return 1
    except (RuntimeError, OSError, ValueError, catalog.CatalogError) as e:
        print(f"ERROR: {e}")
        return 1
    print("build.py: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []))
