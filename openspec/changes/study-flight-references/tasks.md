# Tasks

## 1. Frame extraction tool

- [x] 1.1 [world-artist] Write `tools/reference/extract_frames.py` (headless Blender 5.2, options `--interval`, `--burst-len`) and verify that a full run exits 0 and fills `reference/_frames/A..H/` with stills, 3+ lossless PNG bursts of 8+ frames, and contact sheets
- [x] 1.2 [world-artist] Write `reference/_frames/index.md` (letters A–H for clips, I–K for stills) and verify that letters stay the same across two runs, and that `git status --porcelain` shows nothing from the run
- [x] 1.3 [world-artist] Add the every-frame scan: per-clip `metrics.csv` plus lossless export of outlier frames with ±2 neighbours into `events/`, threshold via `--k`. Verify that CSV rows equal the frame count for every clip, that every flagged frame has its PNGs, and report the full-run runtime

## 2. Terrain catalog

- [x] 2.1 [world-artist] Study every contact sheet, then the stills and bursts where detail is needed, and write `docs/reference-notes/terrain.md` entries (clips, size in metres, materials/colours, density, physical role). Verify that every clip letter is cited
- [x] 2.2 [world-artist] Add the ranked M1 build list (patches, objects, textures), with a footage-based reason on each line, and verify it has at least 8 items

## 3. Video feed notes

- [x] 3.1 [tech-artist] Study the events, bursts, stills and `metrics.csv` files, and write `docs/reference-notes/video-feed.md` covering all five areas (noise events with rate and duration, pixel artifacts, optics, colour/exposure response, resolution feel and OSD layout). Each trait cites clips and names a candidate effect plus a sim driver; re-encoding is claimed only with block-based evidence; uncertain traits are listed for the user. Verify there is no OSD value anywhere in the file
- [x] 3.2 [tech-artist] End `video-feed.md` with the simulation signal list (unit + update rate for each) and verify that every driver named in 3.1 is covered

## 4. Review

- [x] 4.1 [qa-engineer] Run the spec scenarios: tool re-run, git clean, letter stability, metrics completeness, event export, OPSEC hygiene scan of both notes, and coverage. Write `review.md` with the verdict
- [x] 4.2 [user-review] The user reviewed and answered the open questions (2026-09-23), recorded in `pilot-answers.md`. New reference was added: a night clip and 3 destroyed-car images
- [x] 4.4 [world-artist] Make `extract_frames.py` keep existing letters when new files appear, appending new files with the next letters (so citations of A–K stay valid). Re-run it on the new references. Verify that A–K are unchanged in `index.md` and the new files get new letters
- [ ] 4.5 [tech-artist] Fold `pilot-answers.md` (video) into `video-feed.md`: always-on baseline noise, varied random mid-flight flashes that recover, weak and rare throttle coupling, and the 4-stage randomised loss sequence. Characterise the night clip (low light: gain noise, colour, exposure, lights). Verify that every answered U-item is updated, and the OPSEC scan is clean
- [x] 4.6 [world-artist] Fold `pilot-answers.md` (terrain) into `terrain.md`: vehicle classes from the new images (abandoned vs destroyed, typical models), confirmed trenches, burnt vs tilled fields, and the map composition. Update the build list. Verify coverage of the new letters and that the OPSEC scan is clean
- [ ] 4.3 [qa-engineer] Review against spec scenarios after the user's corrections, then clear the branch for merge
