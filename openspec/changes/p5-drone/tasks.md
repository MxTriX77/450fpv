# Tasks: p5-drone

Branch `unity/art/p5-drone` off `unity/main`.
- **The research (1.1)** starts at once.
- **`drone.json` (1.2)** follows it, after `p6-flight-core`'s task 1.3 has fixed the schema.
- **The model (1.3) and the stand (1.4)** follow 1.2 and run in parallel on sub-branches (`--model`, `--stand`).
- **The in-view parts (1.5)** follow 1.3 and `p4-feed`'s camera rig.
- **The station (1.6)** is last.

## 1. Build

- [ ] 1.1 [reference-analyst] The object sheet (design 1):
  - **What:** `docs/object-sheets/drone.md` from public sources: every part's dimensions, mass, material and colour, labelled; the generic payload; the packs' options; the stand.
  - **Verify:** the `object-sheets` scenarios, and the OPSEC scan clean.
- [ ] 1.2 [physics-engineer] `drone.json` (design 2):
  - **What:** written from the sheet in `p6-flight-core`'s schema, with K§5.2 re-derived for letter T's layout (D-043), and the packs set.
  - **Verify:** the re-derivation scenario at seven loadouts, and `p6-flight-core`'s mass-build test re-pinned with each figure labelled.
- [ ] 1.3 [asset-artist] The drone generator (design 3):
  - **What:** `drone.py` reading `drone.json` and the look params, both variants, LODs, and the review sheet at 1.5 / 10 / 50 / 200 m.
  - **Verify:** the prop-tips scenario, `pwsh tools/build_asset.ps1 D01` exits 0, and a second build leaves `git status` clean. Look at the sheet against the object sheet's references.
- [ ] 1.4 [asset-artist] The launch stand (design 4):
  - **What:** its generator from `drone.json`, with its metadata.
  - **Verify:** the "drone rests on the stand" scenario, and `build_asset.ps1 D02` exits 0.
- [ ] 1.5 [tech-artist] In-view parts and hooks (design 5):
  - **What:** the props' rpm blur, the parts in the FPV frustum, leg flex and coil payout from `PoseFrame`.
  - **Verify:** the "props in view" scenario through `p4-feed`'s rig.
- [ ] 1.6 [world-builder] The gallery station (design 6):
  - **What:** both variants, a turntable and the stand, in `stations.json` with a viewpoint.
  - **Verify:** the gallery scene test and the smoke test list them.

## 2. Review and gate

- [ ] 2.1 [qa-engineer] review against spec scenarios:
  - every `drone` scenario, run by QA
  - the sheet's sources and labels
  - `drone.json` against the sheet and the mass build
  - the geometry audit (REAL-05)
  - the stand against the drone
  - budgets (AST-08)
  - hygiene: OPSEC, the generic payload, subject-only commits
- [ ] 2.2 [user-review] **G5:** the pilot looks at the drone station (both variants, the turntable, the stand) and approves the look. The orchestrator records their words in `docs/style-rules.md`.
