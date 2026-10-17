"""Pins every third-party file (CC0 or CC-BY only, D-003 / SEC-04) by SHA-256 and fetches it. Stdlib only.

    python tools/assets/fetch.py <id>...                 fetch the missing files of these manifest entries
    python tools/assets/fetch.py --all                   fetch the missing files of every entry
    python tools/assets/fetch.py --verify                offline check: every file present and matching its pin,
                                                         no stray file under Art/ThirdParty/ (Unity .meta excepted),
                                                         CREDITS.md current, every licence free; exit 1 on any problem
    python tools/assets/fetch.py --credits               rewrite CREDITS.md from the manifest
    python tools/assets/fetch.py --add polyhaven:<id> --res 1k|2k|4k [--displacement]
    python tools/assets/fetch.py --add ambientcg:<ID> --res 1k|2k|4k [--displacement]

`--add` resolves the download URLs through the Poly Haven API (api.polyhaven.com) or the ambientCG API
(ambientcg.com/api/v2), downloads the maps a material needs (diffuse, normal (GL), roughness, AO and, on request,
displacement) at the requested resolution as PNG when the source offers it (else JPG), hashes them and writes the
manifest entry plus CREDITS.md. Files live under unity/Assets/_Project/Art/ThirdParty/<source>/<id>/ and go through
LFS. ambientCG ships zips: the zip is pinned by its own SHA-256 and so is each extracted map.
"""
import argparse
import hashlib
import io
import json
import os
import re
import sys
import urllib.parse
import urllib.request
import zipfile

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MANIFEST = os.path.join("tools", "assets", "manifest.json")
CREDITS = "CREDITS.md"
THIRD_PARTY = os.path.join("unity", "Assets", "_Project", "Art", "ThirdParty")
USER_AGENT = "450fpv-fetch/0.1 (+https://github.com/MxTriX77/450fpv)"
POLYHAVEN_API = "https://api.polyhaven.com"
AMBIENTCG_API = "https://ambientcg.com/api/v2/full_json"
ALLOWED_HOSTS = {"api.polyhaven.com", "dl.polyhaven.org", "ambientcg.com"}
LICENCES = {"CC0": ("CC0 1.0", "https://creativecommons.org/publicdomain/zero/1.0/"),
            "CC-BY": ("CC-BY 4.0", "https://creativecommons.org/licenses/by/4.0/")}
SOURCES = ("polyhaven", "ambientcg")
MAPS = ("diffuse", "normal", "roughness", "ao", "displacement")
OPTIONAL_MAPS = ("ao",)  # taken when the set has it; displacement only on request
POLYHAVEN_KEYS = {"diffuse": "Diffuse", "normal": "nor_gl", "roughness": "Rough", "ao": "AO",
                  "displacement": "Displacement"}
AMBIENTCG_SUFFIXES = {"diffuse": "Color", "normal": "NormalGL", "roughness": "Roughness", "ao": "AmbientOcclusion",
                      "displacement": "Displacement"}


class FetchError(Exception):
    """A refused request or a failed step; the message names the entry or file."""


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def request(url):
    if urllib.parse.urlparse(url).hostname not in ALLOWED_HOSTS:
        raise FetchError(f"{url}: host is not an allowed source, refusing")
    return urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": USER_AGENT}), timeout=120)


def api(url):
    with request(url) as response:
        return json.loads(response.read())


def download(url, path):
    """Streams url to path (through a .part file) and returns its SHA-256."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    partial = path + ".part"
    try:
        with request(url) as response, open(partial, "wb") as out:
            for block in iter(lambda: response.read(1 << 20), b""):
                out.write(block)
        os.replace(partial, path)
    finally:
        if os.path.exists(partial):
            os.remove(partial)
    return sha256(path)


# --- manifest ---------------------------------------------------------------------------------------------------

def load_manifest(root):
    with open(os.path.join(root, MANIFEST), encoding="utf-8") as f:
        manifest = json.load(f)
    for entry in manifest["entries"]:
        if entry["licence"] not in LICENCES:
            raise FetchError(f"{key(entry)}: licence {entry['licence']!r} is not free (only CC0 or CC-BY, D-003)")
        if entry["source"] not in SOURCES:
            raise FetchError(f"{key(entry)}: unknown source {entry['source']!r}")
        expected = f"{THIRD_PARTY}/{entry['source']}/{entry['id']}".replace(os.sep, "/")
        if entry["destination"] != expected:
            raise FetchError(f"{key(entry)}: destination must be {expected}")
    return manifest


def save_manifest(root, manifest):
    with open(os.path.join(root, MANIFEST), "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")


def key(entry):
    return f"{entry['source']}:{entry['id']}"


def file_path(root, entry, file):
    return os.path.join(root, entry["destination"].replace("/", os.sep), file["name"])


# --- fetch and verify --------------------------------------------------------------------------------------------

def fetch_entry(root, entry):
    """Downloads the entry's missing files and checks them against their pins. Returns the problems found."""
    missing = [f for f in entry["files"] if not os.path.isfile(file_path(root, entry, f))]
    if not missing:
        return []
    problems = []
    if "archive" in entry:
        archive = os.path.join(root, entry["destination"].replace("/", os.sep), entry["archive"]["name"] + ".part")
        try:
            if download(entry["archive"]["url"], archive) != entry["archive"]["sha256"]:
                return [f"{key(entry)}: the archive's SHA-256 differs from its pin, upstream changed"]
            with zipfile.ZipFile(archive) as zf:
                for file in missing:
                    with zf.open(file["member"]) as src, open(file_path(root, entry, file), "wb") as out:
                        out.write(src.read())
        finally:
            if os.path.exists(archive):
                os.remove(archive)
        for file in missing:
            problems += check_file(root, entry, file)
    else:
        for file in missing:
            path = file_path(root, entry, file)
            if download(file["url"], path) != file["sha256"]:
                os.remove(path)
                problems.append(f"{relpath(root, path)}: download differs from its pin, upstream changed")
            else:
                print(f"fetched {relpath(root, path)} ({file['bytes'] / 1e6:.1f} MB)")
    return problems


def check_file(root, entry, file):
    path = file_path(root, entry, file)
    if not os.path.isfile(path):
        return [f"{relpath(root, path)}: missing; run fetch.py {key(entry)}"]
    if sha256(path) != file["sha256"]:
        return [f"{relpath(root, path)}: SHA-256 differs from its pin"]
    return []


def verify(root, manifest):
    problems, known = [], set()
    for entry in manifest["entries"]:
        for file in entry["files"]:
            known.add(os.path.normcase(file_path(root, entry, file)))
            problems += check_file(root, entry, file)
    for folder, _, names in os.walk(os.path.join(root, THIRD_PARTY)):
        for name in names:
            path = os.path.join(folder, name)
            if not name.endswith(".meta") and os.path.normcase(path) not in known:
                problems.append(f"{relpath(root, path)}: not in the manifest, so it is unpinned and uncredited")
    credits = os.path.join(root, CREDITS)
    current = open(credits, encoding="utf-8").read() if os.path.isfile(credits) else ""
    if current != credits_text(manifest):
        problems.append(f"{CREDITS}: does not match the manifest; run fetch.py --credits")
    return problems


def relpath(root, path):
    return os.path.relpath(path, root).replace(os.sep, "/")


# --- credits -----------------------------------------------------------------------------------------------------

def credits_text(manifest):
    lines = [
        "# Credits",
        "",
        "Third-party files in this repository, all free: CC0 or CC-BY (D-003, SEC-04). Nothing here comes from the",
        "pilot's references.",
        "",
        "Generated by `python tools/assets/fetch.py --credits` from `tools/assets/manifest.json`, which pins every file",
        "by SHA-256. Do not edit by hand: `python tools/assets/fetch.py --verify` fails when this file does not match",
        "the manifest.",
        "",
        "| Title | Licence | Author | Source | Files |",
        "|---|---|---|---|---|",
    ]
    for e in manifest["entries"]:
        page = urllib.parse.urlparse(e["page"])
        size_mb = sum(f["bytes"] for f in e["files"]) / 1e6
        lines.append(f"| {e['title']} ({e['resolution']}) | {LICENCES[e['licence']][0]} | {e['author']} "
                     f"| [{page.hostname}{page.path}]({e['page']}) "
                     f"| `{e['destination']}/` ({len(e['files'])} files, {size_mb:.1f} MB) |")
    lines += ["", "Licences: " + ", ".join(f"[{name}]({url})" for name, url in LICENCES.values()) +
              ". CC0 needs no attribution; the authors are named as thanks."]
    return "\n".join(lines) + "\n"


def write_credits(root, manifest):
    with open(os.path.join(root, CREDITS), "w", encoding="utf-8", newline="\n") as f:
        f.write(credits_text(manifest))


# --- add ---------------------------------------------------------------------------------------------------------

def wanted_maps(displacement):
    return [m for m in MAPS if m != "displacement" or displacement]


def add_polyhaven(root, asset_id, res, displacement):
    files_api = api(f"{POLYHAVEN_API}/files/{asset_id}")
    info = api(f"{POLYHAVEN_API}/info/{asset_id}")
    entry = {"source": "polyhaven", "id": asset_id, "title": info["name"], "resolution": res, "licence": "CC0",
             "author": ", ".join(sorted(info["authors"])), "page": f"https://polyhaven.com/a/{asset_id}",
             "destination": f"{THIRD_PARTY}/polyhaven/{asset_id}".replace(os.sep, "/"), "files": []}
    for map_name in wanted_maps(displacement):
        formats = files_api.get(POLYHAVEN_KEYS[map_name], {}).get(res)
        if not formats:
            if map_name in OPTIONAL_MAPS:
                print(f"note: {asset_id} has no {map_name} map at {res}, skipped")
                continue
            raise FetchError(f"{asset_id}: no {map_name} map at {res} on Poly Haven")
        source = formats.get("png") or formats.get("jpg")
        if not source:
            raise FetchError(f"{asset_id}: {map_name} at {res} is neither PNG nor JPG")
        file = {"map": map_name, "name": os.path.basename(urllib.parse.urlparse(source["url"]).path),
                "url": source["url"]}
        path = file_path(root, entry, file)
        file["sha256"] = download(source["url"], path)
        file["bytes"] = os.path.getsize(path)
        if file["bytes"] != source["size"]:
            raise FetchError(f"{relpath(root, path)}: {file['bytes']} bytes, the API lists {source['size']}")
        entry["files"].append(file)
        print(f"fetched {relpath(root, path)} ({file['bytes'] / 1e6:.1f} MB)")
    return entry


def add_ambientcg(root, asset_id, res, displacement):
    found = api(f"{AMBIENTCG_API}?{urllib.parse.urlencode({'id': asset_id, 'include': 'downloadData'})}")
    assets = [a for a in found.get("foundAssets", []) if a["assetId"].lower() == asset_id.lower()]
    if not assets:
        raise FetchError(f"{asset_id}: not found on ambientCG")
    asset = assets[0]
    downloads = asset["downloadFolders"]["default"]["downloadFiletypeCategories"]["zip"]["downloads"]
    by_attribute = {d["attribute"]: d for d in downloads}
    zip_info = by_attribute.get(f"{res.upper()}-PNG") or by_attribute.get(f"{res.upper()}-JPG")
    if not zip_info:
        raise FetchError(f"{asset_id}: no {res} PNG or JPG zip on ambientCG")
    entry = {"source": "ambientcg", "id": asset["assetId"], "title": asset["displayName"], "resolution": res,
             "licence": "CC0", "author": "Lennart Demes (ambientCG)", "page": asset["shortLink"],
             "destination": f"{THIRD_PARTY}/ambientcg/{asset['assetId']}".replace(os.sep, "/"),
             "archive": {"name": zip_info["fileName"], "url": zip_info["downloadLink"]}, "files": []}
    archive = os.path.join(root, entry["destination"].replace("/", os.sep), zip_info["fileName"] + ".part")
    try:
        entry["archive"]["sha256"] = download(zip_info["downloadLink"], archive)
        entry["archive"]["bytes"] = os.path.getsize(archive)
        if entry["archive"]["bytes"] != zip_info["size"]:
            raise FetchError(f"{zip_info['fileName']}: {entry['archive']['bytes']} bytes, the API lists {zip_info['size']}")
        with zipfile.ZipFile(archive) as zf:
            members = zf.namelist()
            for map_name in wanted_maps(displacement):
                pattern = re.compile(rf"_{AMBIENTCG_SUFFIXES[map_name]}\.(png|jpg)$", re.IGNORECASE)
                matches = [m for m in members if pattern.search(m)]
                if not matches:
                    if map_name in OPTIONAL_MAPS:
                        print(f"note: {asset_id} has no {map_name} map at {res}, skipped")
                        continue
                    raise FetchError(f"{asset_id}: no {map_name} map in {zip_info['fileName']}")
                member = matches[0]
                file = {"map": map_name, "name": os.path.basename(member), "member": member,
                        "url": zip_info["downloadLink"]}
                path = file_path(root, entry, file)
                with zf.open(member) as src, open(path, "wb") as out:
                    out.write(src.read())
                file["sha256"], file["bytes"] = sha256(path), os.path.getsize(path)
                entry["files"].append(file)
                print(f"extracted {relpath(root, path)} ({file['bytes'] / 1e6:.1f} MB)")
    finally:
        if os.path.exists(archive):
            os.remove(archive)
    return entry


def add(root, manifest, spec, res, displacement):
    source, _, asset_id = spec.partition(":")
    if source not in SOURCES or not asset_id:
        raise FetchError(f"--add takes polyhaven:<id> or ambientcg:<ID>, not {spec!r}")
    if any(e["source"] == source and e["id"].lower() == asset_id.lower() for e in manifest["entries"]):
        raise FetchError(f"{spec}: already in the manifest; remove its entry and files first to re-add it")
    entry = (add_polyhaven if source == "polyhaven" else add_ambientcg)(root, asset_id, res, displacement)
    manifest["entries"].append(entry)
    save_manifest(root, manifest)
    write_credits(root, manifest)
    print(f"added {key(entry)} at {res}: {len(entry['files'])} files, "
          f"{sum(f['bytes'] for f in entry['files']) / 1e6:.1f} MB; manifest and {CREDITS} written")


# --- main --------------------------------------------------------------------------------------------------------

def main(argv=None):
    parser = argparse.ArgumentParser(description="Fetch, pin and credit the third-party files (CC0 / CC-BY only).")
    parser.add_argument("ids", nargs="*", help="manifest entries to fetch, as <id> or <source>:<id>")
    parser.add_argument("--all", action="store_true", help="fetch every entry's missing files")
    parser.add_argument("--verify", action="store_true", help="offline check of files, stray files, CREDITS.md and licences")
    parser.add_argument("--credits", action="store_true", help=f"rewrite {CREDITS} from the manifest")
    parser.add_argument("--add", metavar="SOURCE:ID", help="resolve, download, hash and pin a new set")
    parser.add_argument("--res", choices=("1k", "2k", "4k"), help="resolution for --add")
    parser.add_argument("--displacement", action="store_true", help="also take the displacement map on --add")
    parser.add_argument("--root", default=REPO_ROOT, help=argparse.SUPPRESS)
    args = parser.parse_args(argv)
    if args.add and not args.res:
        parser.error("--add needs --res")
    if not (args.ids or args.all or args.verify or args.credits or args.add):
        parser.error("nothing to do: give ids, --all, --verify, --credits or --add")
    problems = []
    try:
        manifest = load_manifest(args.root)
        if args.add:
            add(args.root, manifest, args.add, args.res, args.displacement)
        if args.credits:
            write_credits(args.root, manifest)
            print(f"{CREDITS} written")
        if args.all or args.ids:
            keys = {key(e) for e in manifest["entries"]} | {e["id"] for e in manifest["entries"]}
            unknown = [i for i in args.ids if i not in keys]
            if unknown:
                raise FetchError(f"not in the manifest: {', '.join(unknown)}")
            for entry in manifest["entries"]:
                if args.all or key(entry) in args.ids or entry["id"] in args.ids:
                    problems += fetch_entry(args.root, entry)
        if args.verify:
            problems += verify(args.root, manifest)
    except (FetchError, OSError, zipfile.BadZipFile, json.JSONDecodeError) as e:
        problems.append(str(e))
    except KeyError as e:
        problems.append(f"unexpected manifest or API shape: missing key {e}")
    for p in problems:
        print(f"ERROR: {p}")
    if problems:
        print(f"FAILED: {len(problems)} problem(s)")
        return 1
    print("OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
