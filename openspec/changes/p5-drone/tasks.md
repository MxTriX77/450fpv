# Tasks: p5-drone

Branch `unity/art/p5-drone` off `unity/main`.
- **The research (1.1)** starts at once.
- **`drone.json` (1.2)** follows it, after `p6-flight-core`'s task 1.3 has fixed the schema.
- **The model (1.3) and the stand (1.4)** follow 1.2 and run in parallel on sub-branches (`--model`, `--stand`).
- **The in-view parts (1.5)** follow 1.3 and `p4-feed`'s camera rig.
- **The station (1.6)** is last.

## 1. Build

- [x] 1.1 [reference-analyst] The object sheet (design 1):
  - **What:** `docs/object-sheets/drone.md` from public sources: every part's dimensions, mass, material and colour, labelled; the generic payload; the packs' options; the stand.
  - **Verify:** the `object-sheets` scenarios, and the OPSEC scan clean.
  - **As built:** letter T scaled as a square 452 mm X (1.37 px/mm, the 3115 can and the 10-inch prop agree). T reads as a rear-port three-quarter view: the spool fore-aft on top, its cap aft; the packs along the sides; the payload slung below **crosswise**; carbon tube legs on two fore-aft **skids**. T's props are **2-blade** (DRN-01 says 3), its arms plates (D-043 said tubes). The 10 km, 1 kg coil (pilot) is T's drum scaled 1.25×; legs 0.25 m (pilot) where T scales to 0.18 m. K§5.2 re-derived at seven loadouts: legs-on 3.46–6.46 kg, roll 0.039–0.083, pitch 0.038–0.072, yaw 0.055–0.082 kg·m² (nominal 4.81 kg: 0.056 / 0.053 / 0.062), the coil 0.09–0.13 m above the CoM (legs-off 0.03–0.07 m below). Packs: two 6S2P (6S4P, 1.82 kg) default, 6S3P and 8S2P options. Stand: posts at r 0.200 m, 0.25 m, 1.6 kg, the legs-off spool 0.135 m clear of the base.
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
