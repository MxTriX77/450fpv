"""Extract stills from one reference clip, named by its letter (OPSEC, SEC-01/02).

    python tools/reference/extract_frames.py <clip under reference/> --letter A --fps 2 [--start 30 --duration 60]

Writes <letter>_<seq>.png (native resolution, lossless) and index.json into reference/_frames/<letter>/
(or --out, which must also be inside reference/_frames/). The index gives each still's time in the clip,
in seconds. Each still is the first clip frame at or after its tick (start + k * interval), and its time
is that frame's own timestamp. Nothing written or printed carries the clip's file name: container
metadata is dropped and ffmpeg's log, which names the input, is never shown unredacted.
A re-run into the same folder first deletes that letter's previous stills and index.
"""
import argparse
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

from ref_paths import REPO_ROOT, RefError, clip_path, frames_dir, frames_path, letter, reference_dir

TOLERANCE_S = 0.0005  # a frame this close before a tick still counts as on it (float rounding)
PTS_TIME = re.compile(r"\[Parsed_showinfo_\d+ @ [^\]]+\] n:\s*\d+ .*?pts_time:(\S+)")


def parse_args(argv):
    p = argparse.ArgumentParser(prog="extract_frames.py", description=__doc__.split("\n")[0])
    p.add_argument("clip", help="the clip, a file under reference/")
    p.add_argument("--letter", type=letter, required=True, help="the clip's letter, A-Z")
    rate = p.add_mutually_exclusive_group(required=True)
    rate.add_argument("--fps", type=float, help="stills per second")
    rate.add_argument("--interval", type=float, help="seconds between stills")
    p.add_argument("--start", type=float, default=0.0, help="seconds into the clip to begin (default 0)")
    p.add_argument("--duration", type=float, help="seconds to cover (default: to the end)")
    p.add_argument("--out", help="output folder inside reference/_frames/ (default reference/_frames/<letter>)")
    p.add_argument("--root", default=str(REPO_ROOT), help="repo root holding reference/ (default: this checkout)")
    args = p.parse_args(argv)
    interval = args.interval if args.fps is None else (1.0 / args.fps if args.fps > 0 else 0.0)
    if not interval > 0 or args.start < 0 or (args.duration is not None and not args.duration > 0):
        p.error("--fps/--interval and --duration must be > 0, and --start >= 0")
    args.interval = interval
    return args


def redact(text, root, clip):
    """ffmpeg's log names the input: blank every part of the clip's path below reference/."""
    for part in clip.relative_to(reference_dir(root)).parts:
        for word in {part, Path(part).stem}:
            text = re.sub(re.escape(word), "<clip>", text, flags=re.IGNORECASE)
    return text


def extract(args):
    clip = clip_path(args.root, args.clip)
    out = frames_path(args.root, args.out or frames_dir(args.root) / args.letter, "--out")
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg is None:
        raise RefError("ffmpeg not found on PATH; install it (docs/environment.md)")

    out.mkdir(parents=True, exist_ok=True)
    own = re.compile(rf"{args.letter}_\d{{4,}}\.png")
    for f in out.iterdir():
        if f.is_file() and (own.fullmatch(f.name) or f.name == "index.json"):
            f.unlink()

    window = ["-ss", f"{args.start}"] + ([] if args.duration is None else ["-t", f"{args.duration}"])
    cmd = [ffmpeg, "-hide_banner", "-nostdin", "-y", *window, "-i", str(clip),
           "-map", "0:v:0", "-map_metadata", "-1",
           "-vf", f"select='gte(t+{TOLERANCE_S},{args.interval!r}*selected_n)',showinfo",
           "-fps_mode", "passthrough", "-fflags", "+bitexact", "-flags:v", "+bitexact",
           str(out / f"{args.letter}_%04d.png")]
    run = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if run.returncode != 0:
        tail = "\n".join(run.stderr.strip().splitlines()[-5:])
        raise RefError(f"ffmpeg failed (exit {run.returncode}):\n{redact(tail, args.root, clip)}")

    times = [round(args.start + float(t), 3) for t in PTS_TIME.findall(run.stderr)]
    files = sorted((f.name for f in out.iterdir() if own.fullmatch(f.name)), key=lambda n: int(n[2:-4]))
    if not times:
        raise RefError("no stills extracted: is --start past the end of the clip?")
    if len(files) != len(times):
        raise RefError(f"ffmpeg wrote {len(files)} stills but logged {len(times)} timestamps")
    index = {
        "letter": args.letter,
        "interval_s": args.interval,
        "start_s": args.start,
        "duration_s": args.duration,
        "stills": [{"seq": i, "file": f, "t_s": t} for i, (f, t) in enumerate(zip(files, times), 1)],
    }
    (out / "index.json").write_text(json.dumps(index, indent=1) + "\n", encoding="utf-8")
    return out, times


def main(argv=None):
    args = parse_args(argv)
    try:
        out, times = extract(args)
    except RefError as e:
        print(f"extract_frames: error: {e}", file=sys.stderr)
        return 2
    print(f"extract_frames: {args.letter}: {len(times)} stills, {times[0]:.3f}-{times[-1]:.3f} s -> "
          f"reference/_frames/{out.relative_to(frames_dir(args.root)).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
