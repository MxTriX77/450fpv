# Design

## Context

- The reference set is 8 clips (1920×1080, about 30 fps, 4–25 s each, about 100 s in total) plus 3 stills of 450–2048 px. The clips look like messenger re-encodes, most likely recordings of goggles or a DVR.
- ffmpeg and OpenCV are not installed. Blender 5.2.1 is, and it has ffmpeg (through MovieClip and the sequencer) and numpy.
- Agents can view still images, not video. The repo is public, so nothing visual derived from the footage may be committed.
- **User direction:** the footage *is* the target look, meaning real analog: aliased pixels, lens distortion, colour shifts on approach, and varied noise events (grain, stripes, single-frame full-frame flashes). The feed is part of the realism simulation, not a static filter.
- A partial `tools/reference/extract_frames.py` (214 lines) from an interrupted first run already produces stills, bursts and sheets for A–H. Extend it rather than rewrite it.

## Goals / Non-Goals

**Goals:**
- Give agents cheap overviews (contact sheets) plus detail (full stills and bursts), all from one command.
- Two roles analyse the footage in parallel without git conflicts.

**Non-Goals:**
- Motion analysis, optical flow or automated classification. The analysis is done by humans and agents looking at the frames.

## Decisions

- **Blender headless over installing ffmpeg.** Run `blender -b --factory-startup --python tools/reference/extract_frames.py -- [options]`. It works on this machine with zero installs. The alternatives, installing ffmpeg or pip-installing OpenCV, add a machine dependency just for one tool.
- **Output layout.** `reference/_frames/index.md` holds the letter → file map (local only). For each clip there is `reference/_frames/<L>/still_<sec>.jpg`, `burst_<n>_<frame>.png`, `sheet_<n>.jpg`, `metrics.csv` and `events/<frame>.png`. The existing `/reference/*` ignore rule already covers this, so `.gitignore` doesn't change.
- **Contact sheets:** 4×4 cells at 480×270 give one 1920×1080 sheet for every 16 s of footage. One image then covers one short clip.
- **Stills in `reference/terrain/`** are copied into the index under letters I, J, K so the notes can cite them. Only the index references them; they are not re-encoded.
- **Work split.** world-artist builds the tool first (task 1). After that, world-artist writes `terrain.md` and tech-artist writes `video-feed.md` in parallel on the same branch. Each commits only its own path (`git commit -m "…" -- <path>`) and retries if `index.lock` is held.
- **Scan every frame.** About 2,900 frames in total. Metrics are computed on a downscaled copy (about 480×270) to keep runtime down: high-pass energy for noise, variance of row means for stripes, mean absolute difference for change. Each frame's metric is compared with the median of the 3 frames on each side, excluding the frame itself. The frame is flagged when that jump is above the clip's median plus k × MAD of jumps, with `--k` defaulting to 10. The first version flagged on raw values above the clip's median plus k × MAD. It missed one-frame interference, line dropouts and ghosted frames, and it flagged a fast approach through vegetation. The jump rule catches every glitch confirmed by eye and none of that motion. Sampling was rejected because single-frame flashes fall between the samples.
- **Frame counts.** Rows equal Blender's decoded frame count. For 4 clips the container lists one more frame than Blender decodes. The missing frame is the final frame of a static end run, because Blender sizes a clip as round(duration × fps). This is accepted.
- **Event statistics source.** Rates and durations for N2–N4 (chroma sparkles, rolling lines, diagonal interference) come from the tech-artist's full-resolution detector scans. The 4×4-averaged `metrics.csv` smooths those fine patterns away. `metrics.csv` stays the source for flagging and for the other events. This is an accepted deviation from the "Measured event statistics" scenario.
- **Lossless detail.** Bursts and events are saved as native-resolution PNG. JPEG is kept only for overview stills and sheets, because its own 8×8 blocking would contaminate the pixel-level study.
- **Feed traits carry simulation drivers.** Each trait maps to the physical cause that produces it on a real quad: motor/ESC electrical noise → stripes that scale with throttle; voltage sag or a hard knock → full-frame dropout; low light → gain noise; the frame filling with one colour → AWB/AE shift. The later video-feed implementation then reads simulation state instead of running on timers. The signal list at the end of `video-feed.md` is the contract the physics engineer will expose.
- **Re-encoding caveat (narrow).** Messenger H.264 re-encoding adds macroblocking and block-aligned smearing. Only traits with that clearly block-shaped evidence are classed as re-encoding. Everything else counts as analog until the user says otherwise, and uncertain traits are flagged for them.

## Risks / Trade-offs

- [Blender's Python API differs from older docs, e.g. strips vs sequences] → Use `MovieClip` plus the compositor or sequencer, whichever works on 5.2.1. The script checks the Blender version and fails loudly.
- [Notes leak identifying details by accident] → Spec hygiene requirement + QA scan + user review before merge on a public repo.
- [Sampling at 1 s misses brief objects] → Bursts plus a configurable `--interval`. Agents can re-run it denser on any clip.
- [Decoding every frame through Blender is slow] → Metrics run on downscaled frames. If a full run takes more than about 20 min, the world-artist reports it and proposes an alternative. It does not install anything on its own.
- [Outlier detection flags motion rather than glitches, e.g. fast pans] → Flag on noise and stripe energy as well as frame change. Tech-artist confirms event types by viewing the exported PNGs.
- [100 s of footage under-represents the map's variety] → The catalog marks gaps, and the user can add more clips later. The tool is re-runnable.
