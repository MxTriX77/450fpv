"""Per-frame metrics of one reference clip or one captured feed sequence, measured alike (FEED-12, SEC-01/02).

    python tools/reference/frame_metrics.py --letter A [--k 10] [--burst-len 8] [--rows 270]
    python tools/reference/frame_metrics.py --frames <folder of PNG frames> --name godot-mixed --fps 50

A clip is found by its letter in the local letter map (letter_index.py) and every decoded frame is read, at
its own timestamp. A captured sequence is a folder of PNG frames, read in file-name order at --fps. Both go
through the same code from the decoded RGB frame on, so the numbers compare like for like.

Each frame is measured on a grid area-averaged to --rows rows (480 x 270 for a 1080p frame, the first
attempt's scan; a frame no taller than --rows is measured as it is). On the 0-255 scale, with luma
Y = 0.299 R + 0.587 G + 0.114 B of the decoded values:
- luma_mean: the mean of Y.
- noise: the RMS of Y minus its 3 x 3 box mean, over the grid's interior (the high-pass noise).
- stripe: the variance of Y's row means after subtracting their 15-row moving average (row-to-row stripes).
- diff: the mean absolute R, G, B change from the previous frame on the grid (0 for the first frame).
- r_mean, g_mean, b_mean: the channel means over the native frame.
- flag: which of noise, stripe and diff break the local-jump rule, joined by "+", or "none". A frame's jump
  is its value minus the median of the 3 frames on each side (itself excluded); it is flagged when the jump
  exceeds the median jump + k x MAD of the jumps over the whole sequence. One-field glitches stand out;
  sustained fast motion, which moves the neighbours too, does not.

Writes only into reference/_compare/metrics/<letter or name>/, after deleting that folder's previous outputs:
- metrics.csv: frame (1-based), time_s and the metrics above, one row per frame
- bursts/<b>_<frame>.png: 3 bursts (start, middle, end) of --burst-len consecutive native frames, lossless
- events/<frame>.png: every flagged frame with its 2 neighbours on each side, native and lossless
Nothing written or printed carries a clip's file name; ffmpeg's log, which names its input, is redacted.
"""
import argparse
import math
import re
import shutil
import statistics
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageChops, ImageMath, ImageStat

import letter_index
from extract_frames import PTS_TIME, redact
from ref_paths import REPO_ROOT, RefError, compare_dir, compare_path, letter

METRICS = ("luma_mean", "noise", "stripe", "diff", "r_mean", "g_mean", "b_mean")
FLAG_METRICS = ("noise", "stripe", "diff")
COLUMNS = ("frame", "time_s", *METRICS, "flag")
STRIPE_WIN = 15  # rows in the moving average that removes the picture's smooth vertical gradient
NEIGHBOURS = 3   # frames on each side forming the local baseline of the jump rule
EVENT_PAD = 2    # neighbours exported on each side of a flagged frame
NAME = re.compile(r"[a-z0-9][a-z0-9-]+")  # a feed sequence's name: lower case, so never a letter


def parse_args(argv):
    p = argparse.ArgumentParser(prog="frame_metrics.py", description=__doc__.split("\n")[0])
    src = p.add_mutually_exclusive_group(required=True)
    src.add_argument("--letter", type=letter, help="a clip's letter, A-Z, looked up in the letter map")
    src.add_argument("--frames", help="a folder of PNG frames, such as a captured feed sequence")
    p.add_argument("--name", help="with --frames: the output's name, lower case, e.g. godot-mixed")
    p.add_argument("--fps", type=float, help="with --frames: frames per second of the sequence")
    p.add_argument("--k", type=float, default=10.0, help="the jump rule's k (default 10)")
    p.add_argument("--burst-len", type=int, default=8, help="consecutive native frames per burst (default 8)")
    p.add_argument("--rows", type=int, default=270, help="rows of the measuring grid (default 270)")
    p.add_argument("--root", default=str(REPO_ROOT), help="repo root holding reference/ (default: this checkout)")
    args = p.parse_args(argv)
    if args.frames is not None and not (args.name and NAME.fullmatch(args.name) and args.fps and args.fps > 0):
        p.error("--frames needs --name (lower case letters, digits and '-', 2+ characters) and --fps > 0")
    if args.letter is not None and (args.name is not None or args.fps is not None):
        p.error("--name and --fps go with --frames; a clip is named by its letter and timed by its own clock")
    if not args.k > 0 or args.burst_len < 1 or args.rows < 3:
        p.error("--k must be > 0, --burst-len >= 1 and --rows >= 3")
    return args


# --- sources: each yields every frame as an RGB image, in order ---------------------------------------------

def tool(name):
    path = shutil.which(name)
    if path is None:
        raise RefError(f"{name} not found on PATH; install ffmpeg (docs/environment.md)")
    return path


def clip_size(root, clip):
    run = subprocess.run([tool("ffprobe"), "-v", "error", "-select_streams", "v:0", "-show_entries",
                          "stream=width,height", "-of", "csv=p=0", str(clip)],
                         capture_output=True, text=True, encoding="utf-8", errors="replace")
    if run.returncode != 0:
        raise RefError(f"ffprobe failed (exit {run.returncode}):\n{redact(run.stderr.strip(), root, clip)}")
    w, h = (int(v) for v in run.stdout.strip().split(",")[:2])
    return w, h


def clip_frames(root, clip, times):
    """Every decoded frame of the clip, in order; when done, `times` holds their timestamps in s."""
    w, h = clip_size(root, clip)
    cmd = [tool("ffmpeg"), "-hide_banner", "-nostdin", "-noautorotate", "-i", str(clip), "-map", "0:v:0",
           "-vf", "showinfo", "-fps_mode", "passthrough", "-f", "rawvideo", "-pix_fmt", "rgb24", "-"]
    size = w * h * 3
    with tempfile.TemporaryFile() as log:  # the log names the input: it stays in this anonymous file
        proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=log)
        try:
            while len(buf := proc.stdout.read(size)) == size:
                yield Image.frombytes("RGB", (w, h), buf)
        finally:
            proc.stdout.close()
            code = proc.wait()
        log.seek(0)
        text = log.read().decode("utf-8", errors="replace")
    if code != 0:
        tail = "\n".join(text.strip().splitlines()[-5:])
        raise RefError(f"ffmpeg failed (exit {code}):\n{redact(tail, root, clip)}")
    times[:] = [float(t) for t in PTS_TIME.findall(text)]


def folder_frames(paths):
    for path in paths:
        with Image.open(path) as im:
            yield im.convert("RGB")


# --- the metrics ----------------------------------------------------------------------------------------------

def grid_size(size, rows):
    w, h = size
    return (w, h) if h <= rows else (max(3, round(w * rows / h)), rows)


def mean(img):
    """The exact mean of a one-band float image (an area average down to one pixel)."""
    return img.resize((1, 1), Image.BOX).getpixel((0, 0))


def luma(rgb):
    r, g, b = (c.convert("F") for c in rgb.split())
    return ImageMath.lambda_eval(lambda a: a["r"] * 0.299 + a["g"] * 0.587 + a["b"] * 0.114, r=r, g=g, b=b)


def noise(y):
    w, h = y.size
    s = {f"s{dx}{dy}": y.crop((dx, dy, w - 2 + dx, h - 2 + dy)) for dx in range(3) for dy in range(3)}

    def squared_high_pass(a):
        e = a["s11"] - sum((a[k] for k in s), start=0) / 9
        return e * e
    return math.sqrt(mean(ImageMath.lambda_eval(squared_high_pass, **s)))


def stripe(y):
    w, h = y.size
    col = y.resize((1, h), Image.BOX).load()
    rows = [col[0, i] for i in range(h)]
    pad = STRIPE_WIN // 2
    padded = [rows[0]] * pad + rows + [rows[-1]] * pad
    residual = [v - sum(padded[i:i + STRIPE_WIN]) / STRIPE_WIN for i, v in enumerate(rows)]
    return statistics.pvariance(residual)


def frame_metrics(rgb, prev_grid, rows):
    """(metrics in METRICS order, this frame's grid image for the next frame's diff)."""
    size = grid_size(rgb.size, rows)
    grid = rgb.resize(size, Image.BOX)
    y = luma(rgb).resize(size, Image.BOX)
    diff = 0.0 if prev_grid is None else statistics.fmean(ImageStat.Stat(ImageChops.difference(grid, prev_grid)).mean)
    r, g, b = ImageStat.Stat(rgb).mean
    return [mean(y), noise(y), stripe(y), diff, r, g, b], grid


def local_jumps(values, k):
    """(flagged per frame, threshold): the jump over the median of NEIGHBOURS frames each side, against the
    sequence's median jump + k x MAD."""
    jumps = []
    for i, v in enumerate(values):
        around = values[max(0, i - NEIGHBOURS):i] + values[i + 1:i + 1 + NEIGHBOURS]
        jumps.append(v - statistics.median(around))
    med = statistics.median(jumps)
    threshold = med + k * statistics.median(abs(j - med) for j in jumps)
    return [j > threshold for j in jumps], threshold


def flag_outliers(table, k):
    """(flag string per frame, {metric: jump threshold}) for the metrics table, one row per frame."""
    over, thresholds = [], {}
    for name in FLAG_METRICS:
        flagged, thresholds[name] = local_jumps([row[METRICS.index(name)] for row in table], k)
        over.append(flagged)
    flags = ["+".join(n for n, o in zip(FLAG_METRICS, row) if o) or "none" for row in zip(*over)]
    return flags, thresholds


# --- the run --------------------------------------------------------------------------------------------------

def source(args):
    """(output name, a function yielding the frames afresh, their timestamps in s, filled by the first pass)."""
    if args.letter:
        clip, times = letter_index.clip(args.root, args.letter), []
        return args.letter, lambda: clip_frames(args.root, clip, times), times
    folder = Path(args.frames).resolve()
    paths = sorted(p for p in folder.iterdir() if p.suffix.lower() == ".png") if folder.is_dir() else []
    if len(paths) < 2:
        raise RefError("--frames needs a folder holding at least 2 PNG frames")
    return args.name, lambda: folder_frames(paths), [i / args.fps for i in range(len(paths))]


def measure(args):
    name, frames, times = source(args)
    out = compare_path(args.root, compare_dir(args.root) / "metrics" / name, "the output")

    table, prev, native = [], None, None
    for rgb in frames():
        native = native or rgb.size
        if rgb.size != native:
            raise RefError(f"frame {len(table) + 1} is {rgb.size[0]}x{rgb.size[1]}, not {native[0]}x{native[1]}")
        row, prev = frame_metrics(rgb, prev, args.rows)
        table.append(row)
    n = len(table)
    if n < 2 or len(times) != n:
        raise RefError(f"decoded {n} frames with {len(times)} timestamps; need at least 2, one each")
    flags, thresholds = flag_outliers(table, args.k)

    # what the export pass saves: the bursts at the start, middle and end, and each flagged frame +-EVENT_PAD
    length = min(args.burst_len, n)
    starts = (1, 1 + (n - length) // 2, n - length + 1)
    saves = {}
    for b, s in enumerate(starts, 1):
        for i in range(s, s + length):
            saves.setdefault(i, []).append(Path("bursts") / f"{b}_{i:05d}.png")
    flagged = [i for i, f in enumerate(flags, 1) if f != "none"]
    events = sorted({j for f in flagged for j in range(max(1, f - EVENT_PAD), min(n, f + EVENT_PAD) + 1)})
    for i in events:
        saves.setdefault(i, []).append(Path("events") / f"{i:05d}.png")

    for sub in ("bursts", "events"):
        (out / sub).mkdir(parents=True, exist_ok=True)
        for f in (out / sub).glob("*.png"):  # a re-run replaces the previous outputs, only the tool's own
            f.unlink()
    lines = [",".join(COLUMNS)] + [f"{i},{t:.4f}," + ",".join(f"{v:.4f}" for v in row) + f",{flag}"
                                   for i, (t, row, flag) in enumerate(zip(times, table, flags), 1)]
    (out / "metrics.csv").write_text("\n".join(lines) + "\n", encoding="utf-8")
    for i, rgb in enumerate(frames(), 1):  # the export pass: the same frames, decoded again
        for rel in saves.get(i, ()):
            rgb.save(out / rel)

    med = {m: statistics.median(row[c] for row in table) for c, m in enumerate(METRICS)}
    counts = "/".join(str(sum(m in f.split("+") for f in flags)) for m in FLAG_METRICS)
    span = times[-1] - times[0]
    grid = grid_size(native, args.rows)
    return "\n".join([
        f"frame_metrics: {name}: {native[0]}x{native[1]}, {n} frames over {span:.2f} s "
        f"({(n - 1) / span if span > 0 else 0:.2f} fps), grid {grid[0]}x{grid[1]}",
        "  median " + " ".join(f"{m} {med[m]:.3f}" for m in METRICS),
        f"  jump thresholds (k {args.k:g}) " + " ".join(f"{m} {thresholds[m]:.4f}" for m in FLAG_METRICS)
        + f"; flagged {len(flagged)} (noise/stripe/diff {counts})",
        f"  {len(starts)} bursts x {length} frames, {len(events)} event frames -> "
        f"reference/_compare/{out.relative_to(compare_dir(args.root)).as_posix()}"])


def main(argv=None):
    args = parse_args(argv)
    try:
        print(measure(args))
    except RefError as e:
        print(f"frame_metrics: error: {e}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
