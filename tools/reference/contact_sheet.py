"""Tile 12-24 extracted stills of one clip into one labelled contact sheet (PRC-09, OPSEC SEC-01/02).

    python tools/reference/contact_sheet.py --letter A [--first 25] [--count 24] [--cols 6]

Reads the index.json that extract_frames.py wrote in reference/_frames/<letter>/ (or --frames), takes
--count stills in time order from still number --first, and writes <letter>_<first>-<last>.jpg into
reference/_frames/<letter>/sheets/ (or --out, also inside reference/_frames/). Tiles run row by row
from the top left; each has a strip below it labelled with the still's name and its time in the clip.
"""
import argparse
import json
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from ref_paths import REPO_ROOT, RefError, frames_dir, frames_path, letter

MIN_STILLS, MAX_STILLS = 12, 24
GAP = 4                    # px between tiles and around the sheet
LABEL_H, LABEL_PX = 20, 14  # label strip height and font size, px
BACKGROUND = (24, 24, 24)
TEXT = (255, 255, 255)
JPEG_QUALITY = 90          # keeps a 24-tile sheet a few hundred kB, small enough to read as one image


def parse_args(argv):
    p = argparse.ArgumentParser(prog="contact_sheet.py", description=__doc__.split("\n")[0])
    p.add_argument("--letter", type=letter, required=True, help="the clip's letter, A-Z")
    p.add_argument("--first", type=int, default=1, help="number of the first still, as in its file name (default 1)")
    p.add_argument("--count", type=int, default=MAX_STILLS, help=f"stills on the sheet, {MIN_STILLS}-{MAX_STILLS} (default {MAX_STILLS})")
    p.add_argument("--cols", type=int, default=6, help="tiles per row (default 6)")
    p.add_argument("--tile-width", type=int, default=320, help="tile width in px (default 320)")
    p.add_argument("--frames", help="folder holding index.json (default reference/_frames/<letter>)")
    p.add_argument("--out", help="output folder inside reference/_frames/ (default <frames>/sheets)")
    p.add_argument("--root", default=str(REPO_ROOT), help="repo root holding reference/ (default: this checkout)")
    return p.parse_args(argv)


def pick(args, stills_dir):
    """The requested stills from the index, in time order, as (seq, path, t_s)."""
    index_path = stills_dir / "index.json"
    if not index_path.is_file():
        raise RefError("no index.json in the frames folder: run extract_frames.py first")
    index = json.loads(index_path.read_text(encoding="utf-8"))
    if index.get("letter") != args.letter:
        raise RefError(f"the index is for letter {index.get('letter')}, not {args.letter}")
    stills = sorted(index["stills"], key=lambda s: s["t_s"])
    chosen = [s for s in stills if s["seq"] >= args.first][:args.count]
    if len(chosen) < args.count:
        raise RefError(f"only {len(chosen)} stills from number {args.first}; lower --first or --count")
    picked = []
    for s in chosen:
        path = (stills_dir / s["file"]).resolve()
        if path.parent != stills_dir or not path.is_file():
            raise RefError(f"still {s['seq']} is missing from the frames folder")
        picked.append((s["seq"], path, s["t_s"]))
    return picked


def make_sheet(args):
    if not MIN_STILLS <= args.count <= MAX_STILLS:
        raise RefError(f"--count must be {MIN_STILLS} to {MAX_STILLS} stills, got {args.count}")
    if not 1 <= args.cols <= args.count or args.tile_width < 16:
        raise RefError("--cols must be 1 to --count, and --tile-width at least 16")
    stills_dir = frames_path(args.root, args.frames or frames_dir(args.root) / args.letter, "--frames")
    out = frames_path(args.root, args.out or stills_dir / "sheets", "--out")
    picked = pick(args, stills_dir)

    with Image.open(picked[0][1]) as im:
        tile_w, tile_h = args.tile_width, max(1, round(args.tile_width * im.height / im.width))
    rows = math.ceil(args.count / args.cols)
    sheet = Image.new("RGB", (args.cols * (tile_w + GAP) + GAP, rows * (tile_h + LABEL_H + GAP) + GAP), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default(size=LABEL_PX)
    for i, (seq, path, t) in enumerate(picked):
        row, col = divmod(i, args.cols)
        x, y = GAP + col * (tile_w + GAP), GAP + row * (tile_h + LABEL_H + GAP)
        with Image.open(path) as im:
            sheet.paste(im.convert("RGB").resize((tile_w, tile_h), Image.Resampling.BOX), (x, y))
        draw.text((x + 4, y + tile_h + (LABEL_H - LABEL_PX) // 2), f"{args.letter}_{seq:04d}  {t:.2f} s", fill=TEXT, font=font)

    out.mkdir(parents=True, exist_ok=True)
    name = f"{args.letter}_{picked[0][0]:04d}-{picked[-1][0]:04d}.jpg"
    sheet.save(out / name, "JPEG", quality=JPEG_QUALITY)
    return out / name, picked


def main(argv=None):
    args = parse_args(argv)
    try:
        path, picked = make_sheet(args)
    except RefError as e:
        print(f"contact_sheet: error: {e}", file=sys.stderr)
        return 2
    print(f"contact_sheet: {args.letter}: {len(picked)} stills, {picked[0][2]:.2f}-{picked[-1][2]:.2f} s, "
          f"{args.cols} columns -> reference/_frames/{path.relative_to(frames_dir(args.root)).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
