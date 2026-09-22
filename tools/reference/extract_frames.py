"""Extract stills, bursts, contact sheets and per-frame metrics from the local reference footage.

Run headless with Blender 5.2+ from the repo root:
    blender -b --factory-startup --python tools/reference/extract_frames.py -- [--interval 1.0] [--burst-len 8] [--k 10]

Reads every clip and photo under reference/ (except reference/_frames/) and writes only
to reference/_frames/, which git ignores. That folder is wiped and rebuilt on every run.
Clips get letters A.. in sorted path order and photos continue after them. The letter-to-file
map is written only to reference/_frames/index.md; everything else refers to letters.

Every frame of every clip is measured (metrics.csv). Frames whose noise, stripe or diff jumps above
its neighbours by more than the clip's median + k*MAD jump are flagged and exported with +-2
neighbours as native PNG in events/.

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
FORMATS = {".jpg": "JPEG", ".png": "PNG", ".bmp": "BMP"}
SCAN_PCT = 25              # metrics run on a 480x270 copy of each 1920x1080 frame
STRIPE_WIN = 15            # rows in the moving average that removes the smooth vertical gradient
LUMA = np.array([0.299, 0.587, 0.114], dtype=np.float32)  # Rec.601 weights on the encoded values
FLAG_METRICS = ("noise", "stripe", "diff")
COLUMNS = ("frame", "time_s", "luma_mean", "noise", "stripe", "diff", "r_mean", "g_mean", "b_mean", "flag")
NEIGHBOURS = 3             # frames each side forming the local baseline a flagged frame jumps above
EVENT_PAD = 2              # neighbours exported on each side of a flagged frame

REF = Path(__file__).resolve().parents[2] / "reference"
OUT = REF / "_frames"


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="extract_frames.py")
    p.add_argument("--interval", type=float, default=1.0, help="seconds between sampled stills")
    p.add_argument("--burst-len", type=int, default=8, help="consecutive native-rate frames per burst")
    p.add_argument("--k", type=float, default=10.0, help="outlier threshold on frame-to-neighbour jumps: median + k*MAD per clip and metric")
    args = p.parse_args(argv)
    if args.interval <= 0 or args.burst_len < 1 or args.k <= 0:
        p.error("--interval and --k must be > 0 and --burst-len >= 1")
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


def read_rgb(path):
    """Load an 8-bit image as (h, w, 3) floats in 0..255, top row first."""
    img = bpy.data.images.load(str(path))
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)[::-1, :, :3] * 255.0


def frame_metrics(rgb, prev):
    """luma_mean, noise, stripe, diff, r_mean, g_mean, b_mean for one frame (0..255 scale)."""
    y = rgb @ LUMA
    h, w = y.shape
    blur = sum(y[1 + dy:h - 1 + dy, 1 + dx:w - 1 + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1)) / 9
    noise = np.sqrt(np.mean((y[1:-1, 1:-1] - blur) ** 2))       # RMS of luma minus its 3x3 box blur
    rows = y.mean(axis=1)
    pad = STRIPE_WIN // 2
    smooth = np.convolve(np.pad(rows, pad, mode="edge"), np.ones(STRIPE_WIN) / STRIPE_WIN, mode="valid")
    stripe = np.var(rows - smooth)                               # row-to-row variation left over
    diff = 0.0 if prev is None else np.abs(rgb - prev).mean()    # frame 1 has no previous frame
    r, g, b = rgb.reshape(-1, 3).mean(axis=0)
    return [float(v) for v in (y.mean(), noise, stripe, diff, r, g, b)]


def scan_clip(scene, strip, n):
    """Measure every frame 1..n on a SCAN_PCT copy. Returns an (n, 7) array in frame_metrics order."""
    tmp = OUT / "_scan.bmp"
    scene.render.resolution_percentage = SCAN_PCT
    strip.transform.filter = "BOX"  # area-average the downscale instead of point-sampling it
    rows, prev = [], None
    for frame in range(1, n + 1):
        render_frame(scene, frame, tmp)
        rgb = read_rgb(tmp)
        rows.append(frame_metrics(rgb, prev))
        prev = rgb
    tmp.unlink()
    strip.transform.filter = "AUTO"
    scene.render.resolution_percentage = 100
    m = np.array(rows)
    if m.shape != (n, 7) or not np.isfinite(m).all():
        raise RuntimeError(f"scan produced {m.shape} values or non-finite metrics for {n} frames")
    return m


def flag_outliers(m, k):
    """Flag frames that jump above their neighbours: per metric, the value minus the median of the
    NEIGHBOURS frames on each side (itself excluded) is compared with median + k*MAD of those jumps
    over the clip. A raw-level threshold flags sustained fast motion and misses one-frame glitches.
    Returns (flag strings, {metric: jump threshold})."""
    thresholds = {}
    over = []
    for name in FLAG_METRICS:
        col = m[:, COLUMNS.index(name) - 2]
        jump = np.array([col[i] - np.median(np.concatenate((col[max(0, i - NEIGHBOURS):i], col[i + 1:i + 1 + NEIGHBOURS])))
                         for i in range(len(col))])
        med = np.median(jump)
        thresholds[name] = float(med + k * np.median(np.abs(jump - med)))
        over.append(jump > thresholds[name])
    flags = ["+".join(nm for nm, o in zip(FLAG_METRICS, row) if o) or "none" for row in zip(*over)]
    return flags, thresholds


def write_metrics(path, m, flags, fps):
    lines = [",".join(COLUMNS)]
    for i, (row, flag) in enumerate(zip(m, flags)):
        lines.append(f"{i + 1},{i / fps:.4f}," + ",".join(f"{v:.4f}" for v in row) + f",{flag}")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


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
    strip = strips.new_movie(letter, str(path), 1, 1)

    clip_dir = OUT / letter
    clip_dir.mkdir()

    m = scan_clip(scene, strip, n)
    flags, thresholds = flag_outliers(m, args.k)
    write_metrics(clip_dir / "metrics.csv", m, flags, fps)

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

    flagged = [i + 1 for i, f in enumerate(flags) if f != "none"]
    events = sorted({j for f in flagged for j in range(max(1, f - EVENT_PAD), min(n, f + EVENT_PAD) + 1)})
    (clip_dir / "events").mkdir()
    for frame in events:
        render_frame(scene, frame, clip_dir / "events" / f"{frame:04d}.png")

    sheets, peak = write_sheets(stills, clip_dir)
    if peak == 0.0:
        raise RuntimeError(f"clip {letter} decoded to black frames only")

    counts = "/".join(str(sum(nm in f.split("+") for f in flags)) for nm in FLAG_METRICS)
    print(f"extract_frames: {letter} {w}x{h} {fps:.3f} fps {n} frames -> "
          f"{len(stills)} stills, {len(starts)} bursts x {blen}, {sheets} sheets, "
          f"{len(flagged)} flagged (noise/stripe/diff {counts}), {len(events)} event frames")
    return (f"| {letter} | {w}x{h} | {fps:.3f} | {n} | {n / fps:.1f} | {len(stills)} | {len(starts)} x {blen} | {sheets} |",
            f"| {letter} | {n} | " + " | ".join(f"{thresholds[nm]:.4f}" for nm in FLAG_METRICS)
            + f" | {len(flagged)} | {counts} | {len(events)} |")


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
    stats, scans = zip(*(extract_clip(scene, L, p, args) for L, p in zip(letters, clips)))

    with index.open("a", encoding="utf-8") as f:
        f.write("\n".join([
            "## Clip output",
            "",
            f"Settings: `--interval {args.interval}` `--burst-len {args.burst_len}` `--k {args.k}`.",
            "- `still_<sec>.jpg`: one frame every interval, named by its time in seconds.",
            "- `burst_<n>_<frame>.png`: bursts 1/2/3 at the start/middle/end, consecutive native frames (1-based), "
            "lossless at native resolution.",
            f"- `sheet_<n>.jpg`: the stills in order, {GRID}x{GRID} cells row-major from the top-left; "
            f"cell i (0-based) of sheet n is still number (n-1)*{GRID * GRID}+i.",
            "- `metrics.csv`: one row per frame (1-based), measured on a "
            f"{SCAN_PCT}% area-averaged copy, values on the 0..255 scale:",
            "  - `luma_mean`: mean Rec.601 luma of the encoded values; `r_mean` `g_mean` `b_mean` likewise per channel.",
            "  - `noise`: RMS of luma minus its 3x3 box blur (high-pass energy).",
            f"  - `stripe`: variance of the row means after subtracting their {STRIPE_WIN}-row moving average.",
            "  - `diff`: mean absolute RGB difference from the previous frame (0 for frame 1).",
            f"  - `flag`: which of noise/stripe/diff jump above the median of the {NEIGHBOURS} frames on each side "
            "by more than the clip's median + k*MAD jump, joined by `+`; `none` otherwise. "
            "Inside a multi-frame run only the frames that jump are flagged, so read run lengths from the raw columns.",
            f"- `events/<frame>.png`: every flagged frame plus {EVENT_PAD} neighbours each side (clamped to the clip), "
            "lossless at native resolution.",
            "",
            "| Letter | Size | FPS | Frames | Seconds | Stills | Bursts | Sheets |",
            "|---|---|---|---|---|---|---|---|",
            *stats,
            "",
            "| Letter | CSV rows | noise jump > | stripe jump > | diff jump > | Flagged | noise/stripe/diff | Event PNGs |",
            "|---|---|---|---|---|---|---|---|",
            *scans,
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
