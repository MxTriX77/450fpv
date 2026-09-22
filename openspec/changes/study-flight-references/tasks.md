# Tasks

## 1. Frame extraction tool

- [ ] 1.1 [world-artist] Write `tools/reference/extract_frames.py` (headless Blender 5.2, options `--interval`, `--burst-len`) and verify that a full run exits 0 and fills `reference/_frames/A..H/` with stills, 3+ bursts of 8+ frames, and contact sheets
- [ ] 1.2 [world-artist] Write `reference/_frames/index.md` (letters A–H for clips, I–K for stills) and verify that letters stay the same across two runs, and that `git status --porcelain` shows nothing from the run

## 2. Terrain catalog

- [ ] 2.1 [world-artist] Study every contact sheet, then the stills and bursts where detail is needed, and write `docs/reference-notes/terrain.md` entries (clips, size in metres, materials/colours, density, physical role). Verify that every clip letter is cited
- [ ] 2.2 [world-artist] Add the ranked M1 build list (patches, objects, textures), with a footage-based reason on each line, and verify it has at least 8 items

## 3. Video feed notes

- [ ] 3.1 [tech-artist] Study the bursts and stills and write `docs/reference-notes/video-feed.md`. Each trait cites clips and maps to a candidate effect, or is marked as a re-encoding artifact. Verify there is no OSD value anywhere in the file

## 4. Review

- [ ] 4.1 [qa-engineer] Run the spec scenarios: tool re-run, git clean, letter stability, OPSEC hygiene scan of both notes, and coverage. Write `review.md` with the verdict
- [ ] 4.2 [user-review] User reads `terrain.md` and `video-feed.md`, corrects anything wrong or missing, and approves. The notes are updated with the corrections
- [ ] 4.3 [qa-engineer] Review against spec scenarios after the user's corrections, then clear the branch for merge
