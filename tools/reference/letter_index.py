"""The local letter map: which reference file each letter cites (OPSEC SEC-01/02, FEED-12, D-037).

    python tools/reference/letter_index.py           # list the letters; show what new files would take
    python tools/reference/letter_index.py --write   # give the new files their letters

The map is reference/_frames/letters.md, git-ignored and the only place real file names appear. Its rows
read `| <letter> | clip or photo | <path under reference/ in backticks> ...`, and anything after the path
is a free note. Letters are stable because the notes cite them: a row is never changed, renumbered or
reused, even when its file is gone. A clip or photo under reference/ that has no row takes the next letter
after the highest one used (a skipped letter stays skipped), clips before photos, each in path order;
--write appends those rows after the table's last row and leaves the rest of the file as it is.
Folders directly under reference/ whose name starts with "_" hold the tools' own outputs and are skipped.
Nothing printed carries a file name, only letters and kinds.
"""
import argparse
import re
import string
import sys
from pathlib import Path

from ref_paths import REPO_ROOT, RefError, clip_path, frames_dir, reference_dir

CLIP_EXT = {".mp4", ".mov", ".mkv", ".avi", ".webm", ".m4v"}
PHOTO_EXT = {".jpg", ".jpeg", ".png", ".webp", ".avif"}
ROW = re.compile(r"^\| ([A-Z]) \| (clip|photo) \| `([^`]+)`.*\|[ \t]*$", re.M)
HEADER = """# Letter map: LOCAL ONLY (git-ignored). Never copy this file or its names anywhere.

| Letter | Kind | File (under `reference/`) |
|---|---|---|
"""


def map_path(root):
    return frames_dir(root) / "letters.md"


def read(root):
    """[(letter, kind, path relative to reference/)] in file order; empty if there is no map yet."""
    p = map_path(root)
    return ROW.findall(p.read_text(encoding="utf-8")) if p.is_file() else []


def media(root):
    """[(kind, relative path)] of every clip and photo under reference/, outside its "_" folders."""
    ref = reference_dir(root)
    if not ref.is_dir():
        raise RefError("no reference/ folder under the root (the footage lives in the main checkout)")
    found = []
    for top in sorted(ref.iterdir()):
        if top.name.startswith("_"):
            continue
        for p in [top] if top.is_file() else sorted(top.rglob("*")):
            ext = p.suffix.lower()
            if p.is_file() and (ext in CLIP_EXT or ext in PHOTO_EXT):
                found.append(("clip" if ext in CLIP_EXT else "photo", p.relative_to(ref).as_posix()))
    return found


def new_rows(rows, files):
    """[(letter, kind, path)] for the files that have no row yet: the next letters after the highest used."""
    known = {path for _, _, path in rows}
    new = [f for f in files if f[1] not in known]
    new.sort(key=lambda f: (f[0] != "clip", f[1]))
    start = string.ascii_uppercase.index(max(letter for letter, _, _ in rows)) + 1 if rows else 0
    if start + len(new) > 26:
        raise RefError(f"{len(new)} new files would run the letters past Z")
    return [(string.ascii_uppercase[start + i], kind, path) for i, (kind, path) in enumerate(new)]


def write(root, new):
    """Append the new rows after the map's last row (or start the map), changing nothing else."""
    p = map_path(root)
    lines = "".join(f"| {letter} | {kind} | `{path}` |\n" for letter, kind, path in new)
    text = p.read_text(encoding="utf-8") if p.is_file() else HEADER
    last = list(ROW.finditer(text))
    at = last[-1].end() + 1 if last else len(text)
    if at > len(text):  # the last row ends the file without a newline
        text, at = text + "\n", len(text) + 1
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text[:at] + lines + text[at:], encoding="utf-8")


def clip(root, letter):
    """The resolved clip that `letter` cites, which must be a clip row whose file exists under reference/."""
    for row_letter, kind, path in read(root):
        if row_letter == letter:
            if kind != "clip":
                raise RefError(f"letter {letter} is a photo, not a clip")
            if not (reference_dir(root) / path).is_file():
                raise RefError(f"letter {letter}'s file is missing from reference/")
            return clip_path(root, reference_dir(root) / path)
    raise RefError(f"letter {letter} is not in reference/_frames/letters.md (run letter_index.py)")


def main(argv=None):
    p = argparse.ArgumentParser(prog="letter_index.py", description=__doc__.split("\n")[0])
    p.add_argument("--write", action="store_true", help="append the new files' rows to the map")
    p.add_argument("--root", default=str(REPO_ROOT), help="repo root holding reference/ (default: this checkout)")
    args = p.parse_args(argv)
    try:
        rows = read(args.root)
        new = new_rows(rows, media(args.root))
        if args.write and new:
            write(args.root, new)
    except RefError as e:
        print(f"letter_index: error: {e}", file=sys.stderr)
        return 2
    for letter, kind, path in rows:
        gone = "" if (reference_dir(args.root) / path).is_file() else " (file missing; the letter stays)"
        print(f"{letter} {kind}{gone}")
    for letter, kind, _ in new:
        print(f"{letter} {kind} ({'new' if args.write else 'new, not written: run with --write'})")
    print(f"letter_index: {len(rows)} letters, {len(new)} new" + (" added" if args.write and new else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())
