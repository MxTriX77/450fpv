# Design

## Context

- The reference set is 8 clips (1920×1080, about 30 fps, 4–25 s each, about 100 s in total) plus 3 stills of 450–2048 px. The clips look like WhatsApp re-encodes, most likely recordings of goggles or a DVR.
- ffmpeg and OpenCV are not installed. Blender 5.2.1 is, and it has ffmpeg (through MovieClip and the sequencer) and numpy.
- Agents can view still images, not video. The repo is public, so nothing visual derived from the footage may be committed.

## Goals / Non-Goals

**Goals:**
- Give agents cheap overviews (contact sheets) plus detail (full stills and bursts), all from one command.
- Two roles analyse the footage in parallel without git conflicts.

**Non-Goals:**
- Motion analysis, optical flow or automated classification. The analysis is done by humans and agents looking at the frames.

## Decisions

- **Blender headless over installing ffmpeg.** Run `blender -b --factory-startup --python tools/reference/extract_frames.py -- [options]`. It works on this machine with zero installs. The alternatives, installing ffmpeg or pip-installing OpenCV, add a machine dependency just for one tool.
- **Output layout.** `reference/_frames/index.md` holds the letter → file map (local only). For each clip there is `reference/_frames/<L>/still_<sec>.jpg`, `burst_<n>_<frame>.jpg` and `sheet_<n>.jpg`. The existing `/reference/*` ignore rule already covers this, so `.gitignore` doesn't change.
- **Contact sheets:** 4×4 cells at 480×270 give one 1920×1080 sheet for every 16 s of footage. One image then covers one short clip.
- **Stills in `reference/terrain/`** are copied into the index under letters I, J, K so the notes can cite them. Only the index references them; they are not re-encoded.
- **Work split.** world-artist builds the tool first (task 1). After that, world-artist writes `terrain.md` and tech-artist writes `video-feed.md` in parallel on the same branch. Each commits only its own path (`git commit -m "…" -- <path>`) and retries if `index.lock` is held.
- **Re-encoding caveat.** WhatsApp H.264 adds macroblocking, banding and smearing on motion. `video-feed.md` must separate these from real analog traits (CVBS noise, colour bleed, rolling bars, exposure pumping) so we don't simulate the messenger's compression.

## Risks / Trade-offs

- [Blender's Python API differs from older docs, e.g. strips vs sequences] → Use `MovieClip` plus the compositor or sequencer, whichever works on 5.2.1. The script checks the Blender version and fails loudly.
- [Notes leak identifying details by accident] → Spec hygiene requirement + QA scan + user review before merge on a public repo.
- [Sampling at 1 s misses brief objects] → Bursts plus a configurable `--interval`. Agents can re-run it denser on any clip.
- [100 s of footage under-represents the map's variety] → The catalog marks gaps, and the user can add more clips later. The tool is re-runnable.
