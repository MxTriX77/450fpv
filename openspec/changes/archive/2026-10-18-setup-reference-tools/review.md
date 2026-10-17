# Review: setup-reference-tools

**Reviewer:** qa-engineer · **Branch:** `unity/ref/setup-reference-tools` (tip `b01b2c4`, base `3d67b8e`) · **Diff:** `git diff 3d67b8e..HEAD`, 8 files, +491 −1
**Environment:** Python 3.14.6, Pillow 12.3.0, pytest 9.1.1, ffmpeg 9.0.2 (gyan.dev full build), Windows 11.
**Rule kept:** the real `reference/` folder was never opened, listed or probed. Every run below used a fake repo root in the session scratchpad (`<tmp>/qa_root`, passed as `--root`) holding one synthetic ffmpeg `testsrc` clip, exactly as the tests do. The clip was named `*.mp4` and tagged `title=` and `comment=` with the same probe string.

## Automated tests

```
$ python -m pytest tools/reference -q
........................                                                 [100%]
24 passed in 8.22s

$ python -m pytest tools -q            # the CLAUDE.md verification command
24 passed in 3.92s

$ openspec validate setup-reference-tools --strict
Change 'setup-reference-tools' is valid
```

The tests map one-to-one onto the spec scenarios and add: interval/start/duration windows, re-run replacement, bad letters, missing ffmpeg, ffmpeg-failure redaction, not-enough-stills, `--frames` outside, and a byte-level source-name scan. The sheet test checks tile order by image difference against the resampled stills, not just by file name.

## Spec scenarios, hand-run

Setup (nothing of this touches the repo):
```
$ ffmpeg -f lavfi -i "testsrc=size=320x240:rate=25:duration=10" -metadata title=QaProbeZebra8841 -metadata comment=QaProbeZebra8841 -c:v libx264 <tmp>/qa_root/reference/*.mp4
$ ffprobe -show_entries format_tags <clip>
TAG:title=QaProbeZebra8841
TAG:comment=QaProbeZebra8841
```

### Frame extraction / "Two frames per second over ten seconds": PASS
```
$ python tools/reference/extract_frames.py <root>\reference\*.mp4 --letter A --fps 2 --root <root>
extract_frames: A: 20 stills, 0.000-9.520 s -> reference/_frames/A
exit=0
files: 21            (A_0001.png … A_0020.png + index.json)
stills= 20
t_s= [0.0, 0.52, 1.0, 1.52, 2.0, 2.52, 3.0, 3.52, 4.0, 4.52, 5.0, 5.52, 6.0, 6.52, 7.0, 7.52, 8.0, 8.52, 9.0, 9.52]
strictly increasing= True
index keys= ['letter', 'interval_s', 'start_s', 'duration_s', 'stills']   first entry= {'seq': 1, 'file': 'A_0001.png', 't_s': 0.0}
```
20 stills (spec: 20 ±1), timestamps strictly increasing. The 0.52 instead of 0.50 is correct: 0.5 s is not a frame of a 25 fps clip, and the docstring promises the first clip frame at or after each tick (0.52 = frame 13). The PNGs carry only `IHDR pHYs IDAT IEND` chunks (no text chunks).

### Contact sheets / "24-still sheet": PASS
```
$ python tools/reference/extract_frames.py <clip> --letter B --fps 3 --root <root>
extract_frames: B: 30 stills, 0.000-9.680 s -> reference/_frames/B
$ python tools/reference/contact_sheet.py --letter B --count 24 --cols 6 --root <root>
contact_sheet: B: 24 stills, 0.00-7.68 s, 6 columns -> reference/_frames/B/sheets/B_0001-0024.jpg
exit=0
sheet size= (1948, 1060) RGB JPEG   info keys= ['jfif', 'jfif_version', 'jfif_unit', 'jfif_density']
expected 6x4 size= (1948, 1060)
```
Looked at it (the sheet, a 2× crop of the four corner tiles, and a 2× stack of all 24 label strips): a 6 × 4 grid, labels `B_0001 0.00 s` … `B_0024 7.68 s` reading row by row in time order, every tile has its label, and the testsrc second counter runs 0,0,0,1,1,1,2,… across the tiles as 3 fps demands, so the tiles really are in time order, not just the labels.

### Contact sheets / "Out-of-range count is refused": PASS
```
$ python tools/reference/contact_sheet.py --letter B --count 30 --root <root>
contact_sheet: error: --count must be 12 to 24 stills, got 30
exit=2    snapshot of <root> unchanged: yes
$ … --count 11
contact_sheet: error: --count must be 12 to 24 stills, got 11
exit=2
```

### Writes only inside reference/_frames / "Output outside the frames folder is refused": PASS
Both tools, each case, with a file-tree snapshot of `<root>` and of `<tmp>/outside` before and after:
```
a) --out <root>\reference\_frames\..\..\escape        extract: exit=2   sheet: exit=2
b) --out <tmp>\outside\C   (absolute, outside)        extract: exit=2   sheet: exit=2
c) cwd=<root>/reference/_frames, --out ..\..\C        extract: exit=2   sheet: exit=2
d) junction <root>\reference\_frames\jout -> <tmp>\outside (New-Item -ItemType Junction, verified LinkType=Junction)
   --out …\_frames\jout\C                            extract: exit=2   sheet: exit=2
   --frames …\_frames\jout  (read through it)                           sheet: exit=2
e) --out …\_frames  /  …\_frames\  /  …\_frames\A\.. extract: exit=2 ×3  sheet: exit=2
f) --frames <root>\reference                                            sheet: exit=2
message every time: "--out must be inside reference/_frames/" (or "--frames must be …")
outside folder: 1 file before, 1 file after (the copied probe clip only); nothing created under <root> outside _frames
```

### Writes only inside reference/_frames / "Input outside reference is refused": PASS
```
$ extract_frames.py <tmp>\outside\*.mp4 --letter C …     -> "the clip must be a file under reference/"  exit=2
$ extract_frames.py <root>\reference\..\..\outside\*.mp4 -> "the clip must be a file under reference/"  exit=2
$ junction <root>\reference\jin -> <tmp>\outside; extract_frames.py <root>\reference\jin\*.mp4
                                                                        -> "the clip must be a file under reference/"  exit=2
root snapshot unchanged: yes; no _frames/C created
```

### Outputs are named by letter / "Source name absent from outputs": PASS
Clean runs as letter `Z` with stdout and stderr captured to files, then a scan of every byte of every file under `_frames` (ASCII and UTF-16, case-insensitive), the Pillow `info`/EXIF/text of every image, and the four captures:
```
extract_frames: Z: 20 stills, 0.000-9.520 s -> reference/_frames/Z
contact_sheet: Z: 12 stills, 0.00-5.52 s, 4 columns -> reference/_frames/Z/sheets/Z_0001-0012.jpg
scanned 90 output files under _frames (81 png, 3 jpg, 6 json) + 4 stdio captures
PNG chunk types seen: ['IDAT', 'IEND', 'IHDR', 'pHYs']
byte/name hits: []        image metadata hits: []
Z index keys: ['letter', 'interval_s', 'start_s', 'duration_s', 'stills'] | still fields: ['seq', 'file', 't_s']
VERDICT: CLEAN
```
The clip's `title`/`comment` tags are dropped (`-map_metadata -1`, `+bitexact`), the sheet JPEG carries only JFIF fields, and the index holds letter, timing and `Z_NNNN.png` names only.

## Break attempts (13, plus the three junction cases above)

| # | Attempt | Result |
|---|---|---|
| B1 | `--letter AA`, `a`, `1`, `Z1`, `À` | all refused by argparse, exit 2, nothing written |
| B2 | `--out <root>\reference`, `--out <root>` | refused, exit 2 |
| B3 | zero-length clip whose name carries the probe | `ffmpeg failed (exit …)` with the 5-line tail; the path shows as `…\reference\<clip>.mp4`; probe string in stdout/stderr: 0; no stills |
| B3b | the same failing clip run on a letter that already had 21 files | exit 2, **previous stills and index gone** (the documented pre-clean runs before ffmpeg) |
| B4 | zero-length clip with a Cyrillic name | redacted to `<clip>` too; leak count 0 for both the probe and the Cyrillic part |
| B5 | `--fps 0`, `--fps -1`, `--start -1`, `--duration 0`, `--fps` with `--interval`, `--start 99` (past the end) | all exit 2 with a clear message; "no stills extracted: is --start past the end of the clip?" for the last |
| B6 | `--cols 0`, `--cols 25`, `--tile-width 8` | refused. `--first 0` / `--first -5` act as 1; `--first 25 --count 12` → "only 6 stills from number 25"; `--count 24 --cols 7` makes a ragged 7×4 sheet (allowed) |
| B7 | hand-written `index.json` whose `file` entries are `../A/A_0001.png` or absolute paths; index of another letter | "still 1 is missing from the frames folder" / "the index is for letter B, not A"; exit 2 |
| B8 | a stray second positional argument carrying the probe name | argparse echoes it: `unrecognized arguments: *.mp4` (stderr only, nothing written) |
| B9 | `--out` pointing at an existing file inside `_frames` | uncaught `FileExistsError` traceback, exit 1, nothing written; the path shown is the `_frames` file, not a clip |
| B10 | `--letter B --out <root>\reference\_frames\A` | exit 0; A's 20 stills survive but A's `index.json` is replaced by a B index |
| B11 | `--out <root>\REFERENCE\_FRAMES\F` (case variant) | accepted; NTFS is case-insensitive and `resolve()` lands it in the real `_frames/F` |
| B12 | `--root` that does not exist | "the clip must be a file under reference/", exit 2 |
| B13 | a PNG under `reference/` given as the clip | accepted as a 1-frame clip, 1 still; harmless |

None of these writes outside `reference/_frames/` or leaks a source name into a file. B3b, B8, B9 and B10 are rough edges, listed as suggestions below, not blockers.

## .gitignore change (`!/tools/reference/`)

```
$ git check-ignore -v --no-index reference/ reference/*.mp4 reference/_frames/A/A_0001.png foo/reference/ foo/reference/*.mp4 unity/Assets/reference/ docs/reference/x.md tools/reference/*.mp4 tools/reference/*.MOV tools/reference/reference/ tools/reference/reference/*.mp4 tools/reference/tests/__pycache__/x.pyc *.mp4 *.MP4 *.mov *.MOV *.mkv *.avi
.gitignore:3:**/reference/   reference/            (and reference/*.mp4, reference/_frames/A/A_0001.png)
.gitignore:3:**/reference/   foo/reference/        (and foo/reference/*.mp4, unity/Assets/reference/, docs/reference/x.md)
.gitignore:7:*.MP4           tools/reference/*.mp4      .gitignore:9:*.MOV  tools/reference/*.MOV
.gitignore:3:**/reference/   tools/reference/reference/    (and tools/reference/reference/*.mp4)
.gitignore:48:__pycache__/   tools/reference/tests/__pycache__/x.pyc
.gitignore:7:*.MP4  *.mp4 *.MP4   .gitignore:9:*.MOV  *.mov *.MOV   .gitignore:10:*.mkv  *.mkv   .gitignore:11:*.avi  *.avi

$ git check-ignore -v --no-index tools/reference/ tools/reference/extract_frames.py tools/reference/ref_paths.py tools/reference/tests/conftest.py
(none ignored)

$ git ls-files tools/reference
tools/reference/contact_sheet.py  tools/reference/extract_frames.py  tools/reference/ref_paths.py
tools/reference/tests/conftest.py  tools/reference/tests/test_contact_sheet.py  tools/reference/tests/test_extract_frames.py
$ git status --short
(clean)
```
`reference/` at the root, every `reference/` elsewhere, a nested `tools/reference/reference/`, and all six video extensions (even inside `tools/reference/`) stay ignored; only the tool sources are tracked.

## Hygiene

- 7 commits, all `Reference Analyst <reference@450fpv.local>` (team.md), one logical step each, explicit paths, subject only (51–70 chars, `type(scope): summary`, lower case), no body, no trailer, no co-author line.
- `tasks.md` change is the 1.1 tick only.
- OPSEC grep of the diff (dates, coordinates, messengers, camera-style prefixes, media names): only the synthetic `{SECRET}.mp4` in the tests. No place, date, OSD value or reference file name anywhere.
- Nothing from `reference/`, `Library/`, `Temp/` or `Logs/`; no binaries, no media.
- No third-party files added (D-003 not engaged); Pillow and pytest already pinned in `tools/requirements.txt`.

## Requirements

- **SEC-01** met: writes only under the ignored `reference/_frames/`; reads only under `reference/`; `..`, absolute paths and junctions all refused; no reference file name in any output, index, image metadata or printed line.
- **SEC-02** met: every still, index and sheet is named by letter and sequence; labels are letter + timestamp.
- **PRC-09** met: one 12–24-still labelled sheet per clip segment, a few hundred kB, readable as one image.

## Suggestions (not blocking; for a later change if wanted)

1. B3b: the pre-clean of a letter's previous stills runs before ffmpeg, so a failing run (corrupt clip, `--start` past the end) leaves the folder empty. Extracting into a temp folder and swapping on success would keep the old set.
2. B10: `index.json` is deleted regardless of its letter; deleting only when its `letter` matches (or refusing a mismatched folder) would stop one letter clobbering another's index via `--out`.
3. B9: catch `OSError` from `mkdir` and report it as a `RefError` instead of a traceback.
4. B8: argparse echoes unrecognized arguments to stderr; a mistyped command with a second clip path would print that name on the terminal (never into a file). Documenting it is enough.
5. `--out` is free-form inside `_frames/`; an operator could name the folder after the clip. The default per-letter folder is safe, and the notes cite letters only.

Verdict: APPROVED
