"""Extract stills, bursts and contact sheets from the local reference footage.

Run headless with Blender 5.2+ from the repo root:
    blender -b --factory-startup --python tools/reference/extract_frames.py -- [--interval 1.0] [--burst-len 8]

Reads every clip and photo under reference/ (except reference/_frames/) and writes only
to reference/_frames/, which git ignores. That folder is wiped and rebuilt on every run.
Clips get letters A.. in sorted path order and photos continue after them. The letter-to-file
map is written only to reference/_frames/index.md; everything else refers to letters.

OPSEC: nothing this tool writes may be committed, copied out of reference/ or uploaded.
"""
import argparse
import shutil
import sys
import time
import traceback
from pathlib import Path

import bpy
import numpy as np

MIN_BLENDER = (5, 2, 0)
VIDEO_EXT = {".mp4", ".mov", ".mkv", ".avi", ".webm", ".m4v"}
STILL_EXT = {".jpg", ".jpeg", ".png", ".webp"}
GRID = 4                   # contact sheet is GRID x GRID cells
CELL_W, CELL_H = 480, 270  # 4 x 480x270 cells -> 1920x1080 sheet
JPEG_QUALITY = 95          # high, so the feed's noise survives for study
FORMATS = {".jpg": "JPEG", ".png": "PNG"}

REF = Path(__file__).resolve().parents[2] / "reference"
OUT = REF / "_frames"


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="extract_frames.py")
    p.add_argument("--interval", type=float, default=1.0, help="seconds between sampled stills")
    p.add_argument("--burst-len", type=int, default=8, help="consecutive native-rate frames per burst")
    args = p.parse_args(argv)
    if args.interval <= 0 or args.burst_len < 1:
        p.error("--interval must be > 0 and --burst-len >= 1")
    return args


def find_media():
    files = [p for p in REF.rglob("*") if p.is_file() and OUT not in p.parents]
    key = lambda p: p.relative_to(REF).as_posix()
    clips = sorted((p for p in files if p.suffix.lower() in VIDEO_EXT), key=key)
    stills = sorted((p for p in files if p.suffix.lower() in STILL_EXT), key=key)
    return clips, stills


def setup_scene():
    scene = bpy.context.scene
    scene.view_settings.view_transform = "Standard"  # AgX/Filmic would shift the footage's colours
    scene.view_settings.look = "None"
    scene.render.dither_intensity = 0.0              # add no noise of our own
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = JPEG_QUALITY
    scene.sequence_editor_create()
    return scene


def render_frame(scene, frame, path):
    """Render one sequencer frame; the file format follows the path's suffix."""
    scene.frame_current = frame
    scene.render.image_settings.file_format = FORMATS[path.suffix]
    scene.render.image_settings.color_mode = "RGB"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def load_cell(path):
    """Load an image fitted into one sheet cell; returns (h, w, 4) floats, bottom row first."""
    img = bpy.data.images.load(str(path))
    w, h = img.size
    s = min(CELL_W / w, CELL_H / h)
    cw, ch = max(1, round(w * s)), max(1, round(h * s))
    img.scale(cw, ch)
    px = np.empty(cw * ch * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(ch, cw, 4)


def write_sheets(stills, clip_dir):
    """Tile stills row-major from the top-left, GRID*GRID per sheet. Returns (sheet count, peak value)."""
    per = GRID * GRID
    peak = 0.0
    starts = range(0, len(stills), per)
    for n in starts:
        sheet = np.zeros((CELL_H * GRID, CELL_W * GRID, 4), dtype=np.float32)
        sheet[..., 3] = 1.0
        for i, path in enumerate(stills[n:n + per]):
            cell = load_cell(path)
            peak = max(peak, float(cell[..., :3].max()))
            row, col = divmod(i, GRID)
            ch, cw = cell.shape[:2]
            y = (GRID - 1 - row) * CELL_H + (CELL_H - ch) // 2  # Blender stores the bottom row first
            x = col * CELL_W + (CELL_W - cw) // 2
            sheet[y:y + ch, x:x + cw] = cell
        img = bpy.data.images.new("sheet", CELL_W * GRID, CELL_H * GRID)
        img.pixels.foreach_set(sheet.ravel())
        img.file_format = "JPEG"
        img.save(filepath=str(clip_dir / f"sheet_{n // per + 1}.jpg"), quality=JPEG_QUALITY)
        bpy.data.images.remove(img)
    return len(starts), peak


def extract_clip(scene, letter, path, args):
    mc = bpy.data.movieclips.load(str(path))
    (w, h), fps, n = tuple(mc.size), mc.fps, mc.frame_duration
    bpy.data.movieclips.remove(mc)
    if fps <= 0 or n <= 0:
        raise RuntimeError(f"clip {letter} could not be decoded (fps={fps}, frames={n})")

    # Scene rate == clip rate, so scene frame k shows native clip frame k.
    scene.render.fps = round(fps)
    scene.render.fps_base = round(fps) / fps
    scene.render.resolution_x, scene.render.resolution_y = w, h
    strips = scene.sequence_editor.strips
    for s in list(strips):
        strips.remove(s)
    strips.new_movie(letter, str(path), 1, 1)

    clip_dir = OUT / letter
    clip_dir.mkdir()

    stills = []
    k = 0
    while (frame := 1 + round(k * args.interval * fps)) <= n:
        stills.append(clip_dir / f"still_{k * args.interval:06.2f}.jpg")
        render_frame(scene, frame, stills[-1])
        k += 1

    blen = min(args.burst_len, n)
    starts = (1, 1 + (n - blen) // 2, n - blen + 1)  # start, middle, end
    for b, start in enumerate(starts, 1):
        for frame in range(start, start + blen):
            render_frame(scene, frame, clip_dir / f"burst_{b}_{frame:04d}.png")

    sheets, peak = write_sheets(stills, clip_dir)
    if peak == 0.0:
        raise RuntimeError(f"clip {letter} decoded to black frames only")

    print(f"extract_frames: {letter} {w}x{h} {fps:.3f} fps {n} frames -> "
          f"{len(stills)} stills, {len(starts)} bursts x {blen}, {sheets} sheets")
    return f"| {letter} | {w}x{h} | {fps:.3f} | {n} | {n / fps:.1f} | {len(stills)} | {len(starts)} x {blen} | {sheets} |"


def main():
    if bpy.app.version < MIN_BLENDER:
        raise RuntimeError(f"Blender {bpy.app.version_string} is too old; this tool needs "
                           f"{MIN_BLENDER[0]}.{MIN_BLENDER[1]} or newer")
    args = parse_args()
    t0 = time.time()
    if not REF.is_dir():
        raise RuntimeError(f"no reference folder at {REF}")
    clips, stills = find_media()
    if not clips:
        raise RuntimeError("no video clips found under reference/")
    if len(clips) + len(stills) > 26:
        raise RuntimeError("more than 26 reference files; letters would run past Z")
    letters = [chr(ord("A") + i) for i in range(len(clips) + len(stills))]

    if OUT.exists():
        shutil.rmtree(OUT)
    OUT.mkdir()

    # The map goes first so a failing letter can be looked up even if the run aborts.
    index = OUT / "index.md"
    rows = [f"| {L} | {'clip' if i < len(clips) else 'photo'} | `{p.relative_to(REF).as_posix()}` |"
            for i, (L, p) in enumerate(zip(letters, clips + stills))]
    index.write_text("\n".join([
        "# Reference index: LOCAL ONLY",
        "",
        "Never commit, copy or upload this file or anything in `reference/`.",
        "It is the only place real file names appear; notes, commits and code cite letters only.",
        "",
        "| Letter | Kind | File (under `reference/`) |",
        "|---|---|---|",
        *rows,
        "",
        "",
    ]), encoding="utf-8")

    scene = setup_scene()
    stats = [extract_clip(scene, L, p, args) for L, p in zip(letters, clips)]

    with index.open("a", encoding="utf-8") as f:
        f.write("\n".join([
            "## Clip output",
            "",
            f"Settings: `--interval {args.interval}` `--burst-len {args.burst_len}`.",
            "- `still_<sec>.jpg`: one frame every interval, named by its time in seconds.",
            "- `burst_<n>_<frame>.png`: bursts 1/2/3 at the start/middle/end, consecutive native frames (1-based), "
            "lossless at native resolution.",
            f"- `sheet_<n>.jpg`: the stills in order, {GRID}x{GRID} cells row-major from the top-left; "
            f"cell i (0-based) of sheet n is still number (n-1)*{GRID * GRID}+i.",
            "",
            "| Letter | Size | FPS | Frames | Seconds | Stills | Bursts | Sheets |",
            "|---|---|---|---|---|---|---|---|",
            *stats,
            "",
            "Photos are indexed only; they are not re-encoded.",
            "",
        ]))
    print(f"extract_frames: done, {len(clips)} clips + {len(stills)} photos in {time.time() - t0:.1f} s")


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        traceback.print_exc()
        print(f"extract_frames: FAILED: {e}", file=sys.stderr)
        sys.exit(1)
