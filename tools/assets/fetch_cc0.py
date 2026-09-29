"""Fetches the CC0 texture sets and sky HDRIs listed in cc0_manifest.json, pins them and writes CREDITS.md. Stdlib only.

    python tools/assets/fetch_cc0.py            fetch what is missing, pin new maps, check every pin, write CREDITS.md
    python tools/assets/fetch_cc0.py --verify   offline: check pins, credits coverage and CREDITS.md; download nothing

Each set is stored as game/assets/textures/<set>/<set>_<map>.<ext>. A map entry in the manifest that is still empty is
fetched and pinned: its URL, byte count and SHA-256 are written back. A pinned map is fetched again only when its file is
missing, and the run fails when a local file or a fresh download does not match its pin, so an upstream change never
slips in. Every download is checked against the size and MD5 the API lists; an unpinned file left by an interrupted run
is adopted only if it passes the same check. Only Poly Haven is supported: its public API (api.polyhaven.com) and its download host (dl.polyhaven.org).
Every set there is CC0. Godot import settings for the texture maps are written once, next to each file, as .import
sidecars: VRAM-compressed with mipmaps, normals as normal maps, and grey maps packed to one channel.
"""
import argparse
import concurrent.futures
import hashlib
import json
import os
import sys
import urllib.parse
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MANIFEST = os.path.join(ROOT, "tools", "assets", "cc0_manifest.json")
TEXTURES = os.path.join(ROOT, "game", "assets", "textures")
CREDITS = os.path.join(ROOT, "CREDITS.md")
API = "https://api.polyhaven.com"
ALLOWED_HOSTS = {"api.polyhaven.com", "dl.polyhaven.org"}
USER_AGENT = "450fpv-fetch-cc0/1.0 (pinned CC0 textures for a flight simulator)"
# Our map name -> (Poly Haven map key, file format). Normals are the OpenGL convention (Y+), which Godot expects.
POLYHAVEN_MAPS = {"albedo": ("Diffuse", "jpg"), "normal": ("nor_gl", "jpg"), "roughness": ("Rough", "jpg"),
                  "ao": ("AO", "jpg"), "height": ("Displacement", "jpg"), "hdri": ("hdri", "hdr")}
MAP_LABELS = {"albedo": "albedo", "normal": "normal (GL)", "roughness": "roughness", "ao": "AO", "height": "height",
              "hdri": "HDR panorama"}
IGNORED = (".import", ".gitkeep")

problems = []


def problem(message):
    problems.append(message)
    print(f"ERROR: {message}")


def request(url):
    if urllib.parse.urlparse(url).hostname not in ALLOWED_HOSTS:
        raise ValueError(f"{url}: host is not Poly Haven, refusing to download")
    return urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": USER_AGENT}), timeout=120)


def api(path):
    with request(f"{API}/{path}") as response:
        return json.loads(response.read())


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def file_path(entry, name):
    ext = POLYHAVEN_MAPS[name][1]
    return os.path.join(TEXTURES, entry["set"], f"{entry['set']}_{name}.{ext}")


def import_sidecar(name, res_path):
    """Godot import settings for one texture map; None for the HDRI, whose use is the sky's business."""
    if name == "hdri":
        return None
    grey = name in ("roughness", "ao", "height")
    return "\n".join([
        "[remap]", "", 'importer="texture"', 'type="CompressedTexture2D"', "",
        "[deps]", "", f'source_file="{res_path}"', "",
        "[params]", "",
        "compress/mode=2",                                      # VRAM compressed
        "compress/high_quality=false",
        f"compress/normal_map={1 if name == 'normal' else 2}",  # 1 enable, 2 disable
        f"compress/channel_pack={1 if grey else 0}",            # 1 = optimized: a grey map becomes one channel
        "mipmaps/generate=true",
        "mipmaps/limit=-1",
        "detect_3d/compress_to=0",                              # already VRAM compressed
        "",
    ])


def download(url, path, attempts=3):
    for attempt in range(attempts):
        try:
            with request(url) as response, open(path, "wb") as out:
                for block in iter(lambda: response.read(1 << 20), b""):
                    out.write(block)
            return
        except OSError:  # timeouts and dropped connections; the file is rewritten from the start
            if attempt == attempts - 1:
                raise


def matches_api(path, source):
    md5 = hashlib.md5()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            md5.update(block)
    return os.path.getsize(path) == source["size"] and md5.hexdigest() == source["md5"]


def fetch_map(entry, name, files):
    """Makes sure one map is on disk and matches its pin. Returns True when it pinned the map."""
    pin = entry["maps"][name]
    path = file_path(entry, name)
    if os.path.isfile(path) and pin.get("sha256"):
        return False  # verify() checks it against the pin
    key, ext = POLYHAVEN_MAPS[name]
    source = files[key][entry["resolution"]][ext]
    if pin.get("url") and pin["url"] != source["url"]:
        problem(f"{entry['set']} {name}: Poly Haven now serves {source['url']}, pinned {pin['url']}")
        return False
    if os.path.isfile(path):  # left by an interrupted run: adopt it only if it is exactly what the API lists
        if not matches_api(path, source):
            problem(f"{os.path.relpath(path, ROOT)}: unpinned and differs from the API's size and MD5; delete it")
            return False
    else:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        partial = path + ".part"
        download(source["url"], partial)
        if not matches_api(partial, source):
            os.remove(partial)
            problem(f"{entry['set']} {name}: download does not match the size and MD5 the API lists")
            return False
        if pin.get("sha256") and sha256(partial) != pin["sha256"]:
            os.remove(partial)
            problem(f"{entry['set']} {name}: upstream file changed since it was pinned")
            return False
        os.replace(partial, path)
        print(f"fetched {os.path.relpath(path, ROOT)} ({source['size'] / 1e6:.1f} MB)")
    changed = not pin.get("sha256")
    pin.update({"url": source["url"], "bytes": source["size"], "sha256": sha256(path)})
    return changed


def fetch_job(job):
    try:
        return fetch_map(*job)
    except OSError as e:
        problem(f"{job[0]['set']} {job[1]}: {e}")
        return False


def fetch(manifest):
    jobs, changed = [], False
    for entry in manifest["sets"]:
        if entry["source"] != "polyhaven":
            problem(f"{entry['set']}: source {entry['source']!r} is not supported (only polyhaven)")
            continue
        pinned = all(os.path.isfile(file_path(entry, n)) and p.get("sha256") for n, p in entry["maps"].items())
        if pinned and "name" in entry:
            jobs += [(entry, n, None) for n in entry["maps"]]  # verify only, no network
            continue
        if "name" not in entry:
            info = api(f"info/{entry['set']}")
            entry["name"], entry["authors"] = info["name"], sorted(info["authors"])
            changed = True
        files = api(f"files/{entry['set']}")
        jobs += [(entry, n, files) for n in entry["maps"]]
    with concurrent.futures.ThreadPoolExecutor(4) as pool:
        for pinned in pool.map(fetch_job, jobs):
            changed |= pinned
    return changed


def verify(manifest):
    known = set()
    for entry in manifest["sets"]:
        for name, pin in entry["maps"].items():
            path = file_path(entry, name)
            known.add(os.path.normcase(path))
            if not pin.get("sha256"):
                problem(f"{entry['set']} {name}: not pinned yet; run without --verify")
            elif not os.path.isfile(path):
                problem(f"{os.path.relpath(path, ROOT)}: missing; run without --verify to fetch it")
            elif sha256(path) != pin["sha256"]:
                problem(f"{os.path.relpath(path, ROOT)}: SHA-256 differs from its pin")
    known |= {os.path.normcase(os.path.join(ROOT, p)) for p in manifest["team_files"]}
    for folder, _, names in os.walk(TEXTURES):
        for n in names:
            path = os.path.join(folder, n)
            if not n.endswith(IGNORED) and os.path.normcase(path) not in known:
                problem(f"{os.path.relpath(path, ROOT)}: not in the manifest's sets or team_files, so CREDITS.md misses it")


def write_sidecars(manifest):
    for entry in manifest["sets"]:
        for name in entry["maps"]:
            path = file_path(entry, name)
            res = "res://" + os.path.relpath(path, os.path.join(ROOT, "game")).replace(os.sep, "/")
            text = import_sidecar(name, res)
            if text and os.path.isfile(path) and not os.path.isfile(path + ".import"):
                with open(path + ".import", "w", encoding="utf-8", newline="\n") as f:
                    f.write(text)


def credits_text(manifest):
    lines = [
        "# Credits",
        "",
        "Third-party material in this repository. Every item is CC0 1.0 (public domain), so no attribution is required;",
        "the authors are named as thanks. Nothing here comes from the pilot's reference footage.",
        "",
        "Generated by `tools/assets/fetch_cc0.py` from `tools/assets/cc0_manifest.json`, which pins every file by",
        "SHA-256. Don't edit by hand; `python tools/assets/fetch_cc0.py --verify` checks that this list covers every file",
        "under `game/assets/textures/`.",
        "",
        "## Textures and skies (`game/assets/textures/<set>/`)",
        "",
        "| Set | Source | Licence | Resolution | Maps | Authors | Used for |",
        "|---|---|---|---|---|---|---|",
    ]
    for e in manifest["sets"]:
        maps = ", ".join(MAP_LABELS[m] for m in e["maps"])
        size_mb = sum(p.get("bytes", 0) for p in e["maps"].values()) / 1e6
        lines.append(f"| {e.get('name', e['set'])} (`{e['set']}`) | [Poly Haven](https://polyhaven.com/a/{e['set']}) "
                     f"| CC0 1.0 | {e['resolution'].upper()} | {maps} ({size_mb:.1f} MB) "
                     f"| {', '.join(e.get('authors', []))} | {e['use']} |")
    lines += ["", "## Made by the team", ""]
    team = manifest["team_files"]
    lines.append("Files under `game/assets/textures/` made by the team: " + (", ".join(f"`{p}`" for p in team) if team else "none yet."))
    lines.append("Everything else under `game/assets/` (models, materials, shaders) and `blender/` is made by the team.")
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description="Fetch and pin the CC0 texture sets.")
    parser.add_argument("--verify", action="store_true", help="check only, download nothing")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")
    with open(MANIFEST, encoding="utf-8") as f:
        manifest = json.load(f)
    if not args.verify and fetch(manifest):
        with open(MANIFEST, "w", encoding="utf-8", newline="\n") as f:
            f.write(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")
    verify(manifest)
    text = credits_text(manifest)
    if args.verify:
        current = open(CREDITS, encoding="utf-8").read() if os.path.isfile(CREDITS) else ""
        if current != text:
            problem("CREDITS.md is out of date; run without --verify to rewrite it")
    elif not problems:
        with open(CREDITS, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        write_sidecars(manifest)
    total = sum(p.get("bytes", 0) for e in manifest["sets"] for p in e["maps"].values())
    sets = sum(1 for e in manifest["sets"] if "hdri" not in e["maps"])
    print(f"{sets} texture sets and {len(manifest['sets']) - sets} HDRIs, {total / 1e6:.1f} MB pinned")
    if problems:
        print(f"FAILED: {len(problems)} problem(s)")
        sys.exit(1)
    print("OK")


if __name__ == "__main__":
    main()
