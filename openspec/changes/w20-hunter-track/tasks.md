# Tasks

Work branch: `unity/world/w20-hunter-track`, from the build branch. One fresh agent per task, at most two at once. Each portion ends in a build.

## 1. Portion 1: the layout, flyable with simple shapes

- [x] 1.1 [reference-analyst] From the file and sampled frames of clips X and Y, settle each drawn kind's size and origin, and whether the 11 low objects stand in a dip; write `docs/object-sheets/hunter-track.md` with every number labelled. Verify: all 24 drawn kinds have a row, and each of the 11 low objects has a stated resolution.
- [x] 1.2 [world-builder] Write `tools/map/import_track.py`, which reads the track and race files given on its command line into the neutral layout and race order under `maps/hunter-09/`. Verify: pytest covers the counts (334, 278, 56), the poses, byte-identical reruns, the race walk (101 passages, 56 checkpoints), and refusal of an unknown kind.
- [ ] 1.3 [asset-artist] Build the track-kit generator with a params row per kind, drawing simple true-size shapes with collision, surfaces and gate gaps. Verify: `pwsh tools/build_asset.ps1` builds the kit and `python -m pytest tools` passes.
- [ ] 1.3b [reference-analyst] Measure the three houses from the file's markers and clips X and Y: footprint, storeys, wall heights and thickness, roof, rooms, every window and door, and what holds the 19 attached objects; add them to `docs/object-sheets/hunter-track.md`, every number labelled. Verify: each of the 23 markers maps to one opening with a size at least the marker's, or a stated enlargement for 416, 394 and 32; each of the 19 objects names its surface.
- [ ] 1.3c [asset-artist] Build the three houses from 1.3b with a parametric generator, in the village's house materials: real wall thickness, rooms, roofs, openings, collision and gaps. Verify: a test passes the quad's outline through all 23 openings with 20 mm a side, each house has a review sheet at 1.5, 10, 50 and 200 m, and no local-only file is committed.
- [ ] 1.4 [world-builder] Compile the map package: height tiles from the foot samples and 1.1's result, placements of the kit and the houses, manifest and height probes, with the kit's vendor list in the loader. Verify: `python tools/map/validate.py maps/hunter-09` passes, a test checks the ground and opening scenarios, and `flightbench --golden` passes with no re-record.
- [ ] 1.5 [game-developer] Add the map's scene and the third Play card named ХАНТЕР-09, starting at the race's spawn point. Verify: EditMode tests pass, and a smoke flight on the card replays headless.
- [ ] 1.6 [user-review] Merge portion 1 into the build branch without waiting for QA (the pilot, 2026-11-22; D-122), build it, update the build pointer, and the pilot flies the layout. Verify: the build's smoke test passes, and the pilot's notes are recorded in `docs/pilot-questions.md` or the ledger.
- [ ] 1.7 [qa-engineer] Only after the pilot says so: review portion 1 against the layout, neutrality, ground, house, gate and Play scenarios. Verify: the review file lists each scenario's evidence.

## 2. Portion 2: our own objects

- [ ] 2.1 [asset-artist] Model the concrete block (199 placed) with chips and real thickness. Verify: its review sheet at 1.5, 10, 50 and 200 m.
- [ ] 2.2 [asset-artist] Model the gates: the three frame kinds, the light gate and the pylons. Verify: review sheets, and the gate scenario passes on the new collision.
- [ ] 2.3 [asset-artist] Model the ground clutter: tyres, cones, ribbons and steel profiles. Verify: review sheets, with the ribbons' snag flags set.
- [ ] 2.4 [asset-artist] Model the remaining kinds, with no brand marks: boards, tent, notice board, flags, arrows and lights. Verify: review sheets.
- [ ] 2.5 [world-builder] Rebuild the package on the final models and compare views with clips X and Y for layout and scale. Verify: a local compare sheet in `reference/_compare/`, the validator and the goldens pass.
- [ ] 2.6 [qa-engineer] Review portion 2 against the object scenarios, then merge it into the build branch. Verify: the review file lists each scenario's evidence.
- [ ] 2.7 [user-review] Build it, update the build pointer, and the pilot flies it. Verify: the pilot's notes are recorded.

## 3. Portion 3: our own ground

- [ ] 3.1 [world-builder] Add the surround and far ground with gentle relief outside the layout, leaving the ground under it unchanged. Verify: the validator passes and the ground scenario still holds.
- [ ] 3.2 [world-builder] Dress the ground as our own autumn field, with surfaces and grass from the existing materials. Verify: views from the track at the four review distances, and the map's ground classes load in the sim.
- [ ] 3.3 [tech-artist] Check the map's light and feed, and run the PLT-07 benchmark on AC power. Verify: the 1 % low is at least 60 fps and load is at most 20 s, with the power mode printed.
- [ ] 3.4 [user-review] Build it, update the build pointer, and the pilot flies the finished map. Verify: the pilot's notes are recorded.

## 4. Close

- [ ] 4.1 [qa-engineer] Review the whole change against spec scenarios, including the neutrality search and the unchanged goldens. Verify: the review file's verdict is green.
