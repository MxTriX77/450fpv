"""Builds assets from their params in headless Blender (change setup-asset-pipeline, design decisions 4 to 7).

    blender --background --factory-startup --python tools/blender/build.py -- <ID> [<ID> ...]
    blender --background --factory-startup --python tools/blender/build.py -- all

For every variant of each asset id in assets-src/catalog.json, it checks the variant's params
(assets-src/<class>/<variant>.json) against the generator's PARAMS table (lib/params.py), runs the generator,
validates the metadata (lib/meta.py) and audits the collision against the LOD0 mesh (lib/audit.py, with the soft
tops of assets-src/physics_materials.json and the card slots of the alpha-clipped recipes, and the ground below y = 0
as cover for a generator that is PLANTED; a generator's visual-only micro meshes are exported beside their LOD and
skipped by the audit), checks the hole-filler contract of an asset that cuts the ground (lib/holes.py), prints the
generator's own lines (its "lines", if any), and only then
writes unity/Assets/_Project/Art/Models/<class>/<ID>/<variant>.fbx and <variant>.assetmeta.json, both in Unity's
frame (lib/frame.py), the FBX staged beside its final name and moved once it reads back as the metadata says. A
rejected build changes no file. It refuses any Blender other than the pinned major.minor. Exit code 1 on any
problem; nothing is hand-edited.
"""
import argparse
import importlib
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(os.path.dirname(HERE))
BLENDER_PIN = (5, 2)  # the major.minor of docs/environment.md's pinned Blender; change both together
sys.path.insert(0, HERE)  # lib/ and generators/ import from here inside Blender


def check_version(version, pin=BLENDER_PIN):
    """Refuses any Blender whose major.minor differs from the pin, so exports stay reproducible (decision 4)."""
    if tuple(version[:2]) != tuple(pin):
        raise RuntimeError(f"Blender {'.'.join(str(v) for v in version)} is not the pinned {pin[0]}.{pin[1]}.x "
                           "(docs/environment.md; BLENDER_PIN in build.py)")


def build_variant(root, catalog_entry, asset_id, variant, material_ids):
    """Builds one variant and returns its report line, printing the audit's measurements. Raises on any problem,
    all of them found before the first file is written except the read-back of the staged FBX, which is removed
    on failure. `material_ids` is the closed physics-material list (lib.meta.load_physics_materials)."""
    from lib import audit, catalog, export, holes, meta, params as params_lib
    params_path = catalog.params_path(root, catalog_entry, variant)
    params = catalog.load_params(params_path)
    generator = importlib.import_module(f"generators.{catalog_entry['generator']}")
    _refuse(variant, params_lib.validate(generator, params), f"params {os.path.relpath(params_path, root)}: ")
    built = generator.build(params)
    data = meta.assemble(asset_id, variant, catalog_entry["generator"], params, built, catalog_entry["materials"])
    _refuse(variant, meta.validate(data, material_ids))
    problems, lines = audit.run(data, built["lods"][0], soft=audit.soft_materials(root),
                                card_visuals=catalog.card_visuals(root), planted=getattr(generator, "PLANTED", False),
                                clip_boxes=bool(data["holes"]))
    hole_problems, hole_lines = holes.audit(data, built["lods"][0])
    for line in lines + hole_lines + built.get("lines", []):
        print(f"{variant}: {line}")
    _refuse(variant, problems + hole_problems)
    export.reset_scene()
    for n, mesh in enumerate(built["lods"]):
        export.add_lod(f"{variant}_LOD{n}", mesh)
    for entry, micro in zip(data["micro"], built.get("micro", [])):  # extra mesh objects beside their LOD
        export.add_lod(entry["name"], micro["mesh"])
    out_dir = catalog.output_dir(root, catalog_entry, asset_id)
    fbx_path = os.path.join(out_dir, variant + ".fbx")
    staged = os.path.join(out_dir, variant + ".staged.fbx")
    fresh = not os.path.isdir(out_dir)
    os.makedirs(out_dir, exist_ok=True)
    try:
        export.export_fbx(staged)
        _refuse(variant, meta.check_against_fbx(data, staged))
        os.replace(staged, fbx_path)
    finally:
        if os.path.exists(staged):
            os.remove(staged)
        if fresh and not os.listdir(out_dir):
            os.rmdir(out_dir)
    meta.write(os.path.join(out_dir, variant + ".assetmeta.json"), data)
    size = [round(data["bounds"]["max"][a] - data["bounds"]["min"][a], 4) for a in range(3)]
    tris = " / ".join(str(lod["tris"]) for lod in data["lods"])
    micro = "".join(f"  micro {m['name']} {m['count']} {m['kind']} {m['tris']} tris" for m in data["micro"])
    wires = f"  {len(data['wires'])} wires" if data["wires"] else ""
    return (f"{variant}: {os.path.relpath(fbx_path, root)}  size {size} m  tris {tris}{micro}  "
            f"{len(data['collision'])} collision shapes{wires}  fbx {os.path.getsize(fbx_path) / 1048576:.1f} MB  "
            "metadata written")


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
