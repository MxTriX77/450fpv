# Tasks: p5-drone

Branch `unity/art/p5-drone` off `unity/main`, **stacked on `p6-flight-core`** (merged in at 1.2 for `drone.json`'s schema and validator), so the flight core merges to `unity/main` first.
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
- [x] 1.2 [physics-engineer] `drone.json` (design 2):
  - **What:** written from the sheet in `p6-flight-core`'s schema, with K§5.2 re-derived for letter T's layout (D-043), and the packs set.
  - **Verify:** the re-derivation scenario at seven loadouts, and `p6-flight-core`'s mass-build test re-pinned with each figure labelled.
  - **As built:**
    - **The file** (`assets-src/drone/drone.json`, `"bootstrap": false`): the sheet's parts list on T's layout, 32 parts, every number labelled with its source. Its own loadout is L4, nominal in flight (1.0 kg of payload, the coil half paid out): 4.832 kg. Two mass shapes stand for what they carry: a spinning 2-blade prop is the disc of its mean inertia (Ø0.2074 m, D·2/√6), a plate arm the cylinder of its polar moment (Ø0.021 m).
    - **Two fields the research needed** (the proposal's rule), documented in the file's `fields`:
      - `legs_off` {`removed`, `moved`}: the legs-off build takes off the legs, T-joints and skids (0.116 kg), hangs the spool under the bottom plate at (0, −0.053, 0) and puts the payload on top at (0, +0.096, 0), as D-043 reads it. The validator refuses a build that keeps a leg, removes the coil or the payload, or moves a removed part.
      - The payload part scales with the mass setting: T's body at 1.0 kg, uniformly by the cube root of the mass ratio, growing away from the arm plane (the sheet's rule, which the generator draws). `DetMath.Cbrt` is Newton's iteration on the basic operations, exact on exact cubes.
      - The bootstrap file is a test fixture (`Tests/Fixtures/drone-bootstrap.json`, its `legs_off` the four legs), so the K§5.2 cross-check and the rigid-body tests run on it unchanged.
    - **The packs:** two 6S2P packs of Molicel P50B in parallel (5.0 Ah, 60 A, 71 g, published). That is the pilot's 6S4P: 20 Ah, 432 Wh, 0.92 kg each (12 cells, 0.852 kg, plus 8 %). It is the one even arrangement inside 6S4P–6S5P, and T's packs hold it. **6S**, because the 3115 900 KV motors are rated 3–6S. 8S would need about 675 KV, and on 8S these motors would draw about 80 A each at full throttle. R_cell is 12 mΩ (assumed).
    - **The props:** 2-blade 10 × 4.5. APC 10x4.5MR's published static table gives k_T = 1.40e-5 N·s² (C_T 0.108 at 9000 rpm) and κ = 0.0144 m. κ is below design 5's assumed 0.015–0.02, so yaw is a little weaker. The implied figures (derived, DC motor model between the motor's published 46 mΩ and the Flash Hobby bench's fitted 0.11 Ω):
      - **Hover** at 4.83 kg: 8780 rpm, 11.85 N a motor; **43–47 % motor power** on a full pack, 49–54 % at half charge.
      - **Liftoff** at 5.18 kg: 9090 rpm, 12.7 N a motor; **44–49 %** on a full pack, 50–56 % at half charge, 53–59 % near empty.
      - The plant check asks for 50–65 % (the pilot: about 62 % on a real heavy quad). 1.4 computes it through the Betaflight throttle path.
    - **The re-derivation** (all derived, in `MassBuildTests`): every one of the sheet's seven loadouts, legs on and legs off, lands on the sheet (mass within 1 %, inertia within 3 %, the CoM and the coil's height within 2 mm). L7 runs on the sheet's pack option B.
      - **Legs on:** 3.49–6.50 kg; roll 0.039–0.083, pitch 0.038–0.072, yaw 0.055–0.082 kg·m². L4 is 4.832 kg, its CoM 2.8 cm below the arm plane and 0.6 cm forward, roll / pitch / yaw 0.0562 / 0.0529 / 0.0623 kg·m².
      - **The coil:** 0.086–0.141 m above the CoM legs on, and 0.025–0.079 m below it legs off, over the whole 0–2.0 kg setting.
      - **Roll above pitch:** from 0.7 kg of payload with a full coil (0.4 kg half paid out, 0 kg empty); level with no payload.
    - **Balance kept, not zeroed:** the spool 0.04 m forward puts the CoM 3–10 mm forward and leaves I_yz −1.4 to −5.2 g·m² legs on.
      - A roll moment also yaws the body, the other way, at +2.5 to +6.9 % of its roll rate (−I_yz/I_yy). Roll and yaw inertia are close, so the principal axes in that plane turn 5–35° off the body axes (62° at 2 kg).
      - At hover the front motors carry 3.7 % more than the rear (L4; 6.4 % at L2). Legs off, I_yz is about −0.1 g·m².
    - **Materials:** `DroneSpec`'s list (carbon, aluminium, polymer, li_ion, electronics). `carbon_composite` is not added: contacts (`p6-flight-core` 1.6) need a contact material for the carbon arms, legs and skids.
- [ ] 1.3 [asset-artist] The drone generator (design 3):
  - **What:** `drone.py` reading `drone.json` and the look params, both variants, LODs, and the review sheet at 1.5 / 10 / 50 / 200 m.
  - **Verify:** the prop-tips scenario, `pwsh tools/build_asset.ps1 D01` exits 0, and a second build leaves `git status` clean. Look at the sheet against the object sheet's references.
  - **So far (code only; the GPU went to the pilot):** `tools/blender/generators/drone.py` on `lib/solids.py`, D-050's layout (the coil beneath, a bottle strapped on top) read from `drone.json`, `D01_legs_on` and `D01_legs_off` with four LODs, collision and snag, six palette recipes and the bottle's, the catalog entry, and pure-Python tests (`test_drone.py`, `test_solids.py`; the prop tips 0.5736 / 0.7060 m). The Blender and Unity build, the review sheet and the look against T wait for a fresh task, after the physics engineer's D-050 `drone.json`.
- [ ] 1.4 [asset-artist] The launch stand (design 4):
  - **What:** its generator from `drone.json`, with its metadata.
  - **Verify:** the "drone rests on the stand" scenario, and `build_asset.ps1 D02` exits 0.
- [ ] 1.5 [tech-artist] In-view parts and hooks (design 5):
  - **What:** the props' rpm blur, the parts in the FPV frustum, leg flex and coil payout from `PoseFrame`.
  - **Verify:** the "props in view" scenario through `p4-feed`'s rig.
- [x] 1.6 [world-builder] The gallery station (design 6): **dropped by D-047** (the test range is the final map; G5 folds into G7).
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
