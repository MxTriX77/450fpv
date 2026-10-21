# Tasks: p6-test-range

Standard tier (D-033): one QA pass, then the map reaches the pilot inside G4 (noclip through the feed) and G7 (flights). Task 1.1 runs at once on `unity/art/p6-test-range--standins` off `unity/main`. Task 1.2 runs on `unity/world/p6-test-range` off G1's tip until G1 merges, and merges 1.1's branch when 1.1 is done. Task 1.3 follows H01's merge only if 1.2 ran before it.

## 1. Build

- [ ] 1.1 [asset-artist] The Godot stand-ins (design 1–5):
  - **What:** the port generator and a params file for each of `Z01`–`Z13` from `refs/godot/main` at `8340138` (read-only: `git show` and the local LFS objects; nothing merged, D-027); their catalog entries with `"stand_in": true` and the source; each `.assetmeta.json` from the Godot catalog entry through the mesh's own transform; render materials from existing recipes where one fits, any third-party texture pinned and credited. Build Z01 (the house: gaps and many boxes) first and look at its review sheet and collision overlay before the batch.
  - **Verify:** the three stand-in scenarios; `pwsh tools/build_asset.ps1 Z0n` exits 0 for all thirteen, and a second run of each leaves `git status` clean; the audit's line per stand-in; one look at each 10 m sheet for frame errors (a mirrored model, a gap on the wrong wall, collision off the mesh); `python -m pytest tools -q`; `python tools/assets/fetch.py --verify`; the hygiene scan; Unity's EditMode run; `openspec validate p6-test-range --strict`.
- [ ] 1.2 [world-builder] The map, its scene and build (design 5–9):
  - **What:** `tools/map/make_test_range.py` writing `maps/test-range/placements.json` (the yard from `ADOBE_YARD`, the posadka, the fields' grass, R01 A and F, the pole line with its wires, E01, H01 if merged, the start points, the noclip start); the scene builder writing `Scenes/TestRange.unity` from it with the floor's holes for the cellar and the trench; the look presets and noclip from G1; `Fpv450.Build.TestRange`; `docs/test-range.md`.
  - **Verify:** the contents, posadka-shape, start-point and preset scenarios; a test that every placed instance's transform in the scene matches `placements.json` within 1 mm and 0.01°; the generator run twice gives the same bytes; the smoke test exits 0 with every prefab logged; the load time; a first frame-rate reading on AC along the spec's three legs (QA measures it fully); the EditMode run; `python tools/map/validate.py maps/test-range` if it applies to this form, otherwise the placement file's own check.
- [ ] 1.3 [world-builder] H01 in the yard's neighbours (only if 1.2 ran before `p1-h01-adobe-house` merged): merge the base that holds H01, add `H01_I` and `H01_D` to the generator's table, rebuild the map and the scene; verify the contents scenario and the smoke test again.

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios: every `test-range` scenario run by QA; the stand-ins' metadata against the Godot catalog (shape by shape, gaps and wind volumes) and their review sheets; the frame rate on AC along the three legs with vsync off and the power mode printed (PLT-07); the load time (PLT-06); the port's provenance (each stand-in names its Godot source and commit, nothing merged, D-027); hygiene (no text in the map, nothing from `reference/`, subject-only commits)
