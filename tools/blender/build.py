"""Builds assets from their params in headless Blender (change setup-asset-pipeline, design decisions 4 to 7).

    blender --background --factory-startup --python tools/blender/build.py -- <ID> [<ID> ...]
    blender --background --factory-startup --python tools/blender/build.py -- all

For every variant of each asset id in assets-src/catalog.json, it runs the catalog's generator on the variant's
params (assets-src/<class>/<variant>.json) and writes unity/Assets/_Project/Art/Models/<class>/<ID>/<variant>.fbx
and <variant>.assetmeta.json, both in Unity's frame (lib/frame.py), then checks the metadata against the written
FBX. It refuses any Blender other than the pinned major.minor. Exit code 1 on any problem; nothing is hand-edited.
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
    """Builds one variant and returns its report line. Raises on any problem. `material_ids` is the closed
    physics-material list (lib.meta.load_physics_materials)."""
    from lib import catalog, export, meta
    params = catalog.load_params(catalog.params_path(root, catalog_entry, variant))
    generator = importlib.import_module(f"generators.{catalog_entry['generator']}")
    built = generator.build(params)
    export.reset_scene()
    for n, mesh in enumerate(built["lods"]):
        export.add_lod(f"{variant}_LOD{n}", mesh)
    out_dir = catalog.output_dir(root, catalog_entry, asset_id)
    os.makedirs(out_dir, exist_ok=True)
    fbx_path = os.path.join(out_dir, variant + ".fbx")
    meta_path = os.path.join(out_dir, variant + ".assetmeta.json")
    export.export_fbx(fbx_path)
    data = meta.assemble(asset_id, variant, catalog_entry["generator"], params, built, catalog_entry["materials"])
    problems = meta.validate(data, material_ids) or meta.check_against_fbx(data, fbx_path)
    if problems:
        raise RuntimeError(f"{variant}: " + "; ".join(problems))
    meta.write(meta_path, data)
    size = [round(data["bounds"]["max"][a] - data["bounds"]["min"][a], 4) for a in range(3)]
    tris = " / ".join(str(lod["tris"]) for lod in data["lods"])
    return f"{variant}: {os.path.relpath(fbx_path, root)}  size {size} m  tris {tris}  metadata written"


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
