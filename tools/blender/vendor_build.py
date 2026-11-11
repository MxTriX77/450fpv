"""Writes the physics metadata of the vendor pieces (w4b-farmland-ruins, D-089): meshes of the local store that the
map places but the pipeline never exports. Plain Python, no Blender:

    python tools/blender/vendor_build.py <ID|variant ...|all> [--check] [--audit]

For every variant of each asset of assets-src/vendor.json: checks its params (assets-src/<class>/<variant>.json)
against the generator's PARAMS (lib/params.py), runs the generator (which reads the vendor FBX through lib.fbxread,
as Unity imports it, and refuses a file whose SHA-256 differs from the params' pin), assembles the metadata
(lib.meta.assemble, then the vendor record and the vendor object's name on LOD0), validates it (lib.meta.validate)
and writes unity/Assets/_Project/Art/Models/<class>/<ID>/<variant>.assetmeta.json, with a Unity .meta beside each new
file and folder (a TextScriptImporter or a folder, its GUID derived from the path). No mesh is written: the
geometry stays in vendor/.

--audit also runs the collision audit (lib.audit.run, the ground below y = 0 covering, as for a planted asset) and
prints its lines and problems; it reports, it does not refuse (design.md: a broken wall's open top is the solid
wall's, not the visual's). --check writes nothing and exits 1 when a rebuilt file differs from the committed one.
Exit code 1 on any problem; the store must be linked (pwsh tools/vendor/link.ps1).
"""
import argparse
import hashlib
import importlib
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

from lib import audit, catalog, meta, params as params_lib  # noqa: E402

VENDOR_CATALOG = os.path.join("assets-src", "vendor.json")


def load(root):
    with open(os.path.join(root, VENDOR_CATALOG), encoding="utf-8") as f:
        data = json.load(f)
    if data.get("schema") != 1 or not isinstance(data.get("assets"), dict):
        raise ValueError(f"{VENDOR_CATALOG}: expected schema 1 with an 'assets' object")
    return data["assets"]


def wanted(assets, names):
    """(asset id, variant) pairs for ['all'], asset ids or variant names."""
    out = []
    for name in names:
        if name == "all":
            out += [(a, v) for a in sorted(assets) for v in assets[a]["variants"]]
        elif name in assets:
            out += [(name, v) for v in assets[name]["variants"]]
        else:
            hit = [(a, name) for a in sorted(assets) if name in assets[a]["variants"]]
            if not hit:
                raise ValueError(f"{name} is neither an asset nor a variant of {VENDOR_CATALOG}")
            out += hit
    return out


def build_variant(root, assets, asset_id, variant, material_ids, run_audit=False):
    """The metadata text of one variant and its report lines; raises on any problem."""
    entry = assets[asset_id]
    params_path = catalog.params_path(root, entry, variant)
    params = catalog.load_params(params_path)
    generator = importlib.import_module(f"generators.{entry['generator']}")
    problems = params_lib.validate(generator, params)
    if problems:
        raise ValueError(f"{os.path.relpath(params_path, root)}: " + "; ".join(problems))
    built = generator.build(params)
    data = meta.assemble(asset_id, variant, entry["generator"], params, built, catalog.visuals(entry, variant))
    data["lods"][0]["mesh"] = built["vendor"]["object"]
    data["vendor"] = built["vendor"]
    problems = meta.validate(data, material_ids)
    if problems:
        raise ValueError(f"{variant}: " + "; ".join(problems[:10]))
    lines = [f"{variant}: {line}" for line in built["report"]]
    if run_audit:
        started = time.perf_counter()
        found, audit_lines = audit.run(data, built["lods"][0], soft=audit.soft_materials(root), planted=True)
        lines += [f"{variant}: {line}" for line in audit_lines]
        lines += [f"{variant}: AUDIT {p}" for p in found]
        lines.append(f"{variant}: audit took {time.perf_counter() - started:.1f} s, {len(found)} problems")
    return meta.dumps(data), lines


def unity_meta(path, folder=False):
    """A Unity .meta for a new file or folder of the project, its GUID the path's MD5 (stable across checkouts)."""
    rel = os.path.relpath(path, REPO_ROOT).replace(os.sep, "/")
    guid = hashlib.md5(("450fpv:" + rel).encode("utf-8")).hexdigest()
    if folder:
        return (f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    return (f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n")


def _write_meta_files(path):
    """The .meta of the file and of each new folder up to the Models folder."""
    stop = os.path.join(REPO_ROOT, catalog.MODELS)
    folder = os.path.dirname(path)
    while os.path.normcase(folder) != os.path.normcase(stop):
        if not os.path.exists(folder + ".meta"):
            _write(folder + ".meta", unity_meta(folder, folder=True))
        folder = os.path.dirname(folder)
    if not os.path.exists(path + ".meta"):
        _write(path + ".meta", unity_meta(path))


def _write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("names", nargs="+")
    parser.add_argument("--check", action="store_true", help="compare with the committed files, write nothing")
    parser.add_argument("--audit", action="store_true", help="run the collision audit and print it")
    args = parser.parse_args(argv)
    assets = load(REPO_ROOT)
    material_ids = meta.load_physics_materials(REPO_ROOT)
    failed = False
    for asset_id, variant in wanted(assets, args.names):
        started = time.perf_counter()
        try:
            text, lines = build_variant(REPO_ROOT, assets, asset_id, variant, material_ids, args.audit)
        except Exception as e:  # report and go on with the next variant
            print(f"{variant}: FAILED: {e}")
            failed = True
            continue
        for line in lines:
            print(line)
        path = os.path.join(catalog.output_dir(REPO_ROOT, assets[asset_id], asset_id), variant + ".assetmeta.json")
        if args.check:
            old = open(path, encoding="utf-8").read() if os.path.exists(path) else None
            same = old == text
            failed |= not same
            print(f"{variant}: {'matches' if same else 'DIFFERS from'} {os.path.relpath(path, REPO_ROOT)}")
        else:
            _write(path, text)
            _write_meta_files(path)
            print(f"{variant}: wrote {os.path.relpath(path, REPO_ROOT)} in {time.perf_counter() - started:.1f} s")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
