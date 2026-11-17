# Tasks: w17-four-legs

Branch `unity/physics/physics-integration-2--w17-four-legs`, cut from the physics line. Order: 1.1 and 1.2 together; then 1.3 (after `w16-plants-in-the-air` 1.5's follow-up has landed in `Runtime/Contact/`) and 1.5 side by side (1.5 on its own branch, `unity/art/physics-integration-2--w17-loop-legs`, named before D-119); the goldens are re-recorded once, after 1.5 has merged into 1.3's branch; then 1.4; then 2.1; then 3.1.

## 1. The four legs

- [x] 1.1 [physics-engineer] **The baseline from the pilot's flights, and the design.** No change to a flight yet.
  - *As built:* `design.md` §1 (the 30 logs since model 103, read from their step records: 28 liftoffs, 63 touchdowns), §2 decisions 1–8 and §4's targets; `tools/flightbench/handcalc/four_legs.py` prints every hand number. The magnet is the wet half-skids' cohesive pull (−3.0 to −10.7 N a corner, the last release at T/W 1.31–1.71), which presses nothing and so leaves the drone frictionless while held; the toy plane is the skids' skating (0.4–4.4 m), chatter and rocking. New labelled inputs for 1.3: `surfaces.json` `tip_resistance_pa` per surface, z_ref, α_E, ν, r_s, N_kt, J, the `leg.setdown` draw [1, 1.5]. Not settled: r_s (0–0.04 band) carries the dry account; wet tilled ground and spoil are mud (39–117 mm at rest); questions on the pilot's leg ends in design §"Questions".
  - **Measure the pilot's own flights** (the logs since the model 103 build, about 30). For every liftoff:
    - the throttle and the thrust over weight at the first motion;
    - how far the drone slid before it left the ground;
    - its vertical speed as the last contact let go (the "jump");
    - which contact held it, and with what force.
  - For every touchdown:
    - the touch speed and the peak acceleration;
    - each rebound's height;
    - the slide and the time to rest;
    - what the legs did.
  - Say in numbers what "a magnet" and "a light plastic plane" are in these flights.
  - **Design** (`design.md`):
    - a leg's tip in each surface's soil: its penetration under the weight and at a touchdown, from the surface table's own soil law;
    - the hold on the way out (the shaft's friction and adhesion, storing nothing);
    - a dug-in tip's resistance to sliding;
    - the start's embedment per leg as a bounded draw of a named physical parameter;
    - what the legs' failure law reads without the skids' frame.
  - **Hand calculations** for this drone's mass:
    - a careful liftoff from sod, tilled soil, dry crust and asphalt (each leg's release);
    - touchdowns at 0.5 and 1.5 m/s on the same four (the penetration, the rebound, the energy's split);
    - the same for a wet set.
  - Each number labelled measured, published, derived or assumed.
- [x] 1.2 [asset-artist] **The model on four separate legs.**
  - *As built* (`drone.json`, `drone.py`, `D01_*.json`; the drone's object sheet, Letter U; every number labelled):
    - **What U shows of the legs** (measured, at 1.9–2.1 mm/px from its two motor cans, ±20 %): four separate legs, no rail or joint between them; each ends in **one narrow tip as wide as its stock (6–12 mm), nothing below it** (no foot, cap, ball or joint); open or plugged cannot be seen. The pilot's legs are **loops** of bent stock (a narrow teardrop under the arm, 45–55 mm across its top, held at two points along the arm), 0.10–0.13 m from the arm to the tip on the one leg seen whole, vertical within ±10°. Not copied: a loop is a new leg and failure law (the proposal's non-goals) and the pilot's 25–35 cm stands (D-043); open point 8 of the sheet, with its kit questions.
    - **The tip** (for 1.3): the 10/8 mm tube's own cut end, square and open, **Ø0.010 m** (measured, T), plugged by the soil as a disc, **A_t 7.854e-5 m²** (assumed, design 1's default: U cannot show the end); no mass of its own (derived); at **(±0.1025, −0.253, ±0.1025) m**, where the skids met the ground, so the stance height is unchanged (derived). `legs.foot_radius_m` 0.005.
    - **The leg:** the part unchanged (Ø0.010 × 0.25 m, 10.2 g, centre y −0.128, vertical, r 0.145 m; the pilot's length); **free length 0.2335 m** (derived: the clamp's underside y −0.0195 to the tip); the four alike. `legs.stiffness_n_per_m` **57.0 kN/m** and `damping_n_s_per_m` **107 N·s/m** as design 1 derives them (no skid ring; ζ 0.2 on a quarter of 5.0118 kg); the `legs.tjoint` block gone; `legs_off.removed` the four legs only.
    - **The contact shapes:** per corner one support `support_<c>` on part `leg_<c>`, a sphere r 0.005 m at (±0.1025, −0.248, ±0.1025), its lowest point the tip; the leg's capsule `leg_<c>` (role body, as before) now from y −0.008 to **−0.248**, so it ends at the tip and its end sphere is the support's (1.3: a body touch within the support's radius is the support's); no skid capsule, no T-joint shape.
    - **Masses** (derived, a parallel-axis sum of the parts): **68.6 g leave** (two skids 2 × 18.3 g, four T-joints 4 × 8 g); nothing comes. The file's loadout 4.7354 → **4.6668 kg** legs-on (CoM y −0.0177 → −0.0143 m), legs-off unchanged 4.6260 kg. The pilot's loadouts (model 103's logged headers, measured): **1 kg 5.0804 → 5.0118 kg**, CoM 0.2325 m over the tips, I_xx 0.0519, I_zz 0.0473 kg·m², a leg's share 12.29 N; **2 kg 6.0804 → 6.0118 kg**, 0.2505 m over the tips, I_xx 0.0704, I_zz 0.0591 kg·m², 14.74 N (the design's 6.0438 kg took the skids alone).
    - **Not changed (the air is a non-goal):** `aero.body_area_m2` still counts the skids and T-joints, 0.0043 side, 0.0071 top and 0.0003 m² front of it (derived, a 0.5 mm raster); 1.3 or the orchestrator decides.
    - **The fixture:** `assets-src/drone/drone_skids.json`, byte for byte model 103's `drone.json` (SHA-256 f5c5d361…), so its content hash is model 103's.
    - **Both variants** build with `pwsh tools/build_asset.ps1 D01` (legs on, legs off and the bottle); each legs-on corner is two readable meshes (stub, leg), the leg open at both ends at LOD0–1. Review sheets 1.5 / 10 / 50 / 200 m, turntable and overlay; side-by-sides against T and U local in `reference/_compare/`.
    - **Red until 1.3 and 1.4** (expected): the sim's loader refuses the file, `legs.tjoint: missing` (`DroneSpec.ReadLegs`), so every test and bench that reads `drone.json` fails there (Unity's EditMode: 76 of the 98 tests in the drone's classes); past the loader `BuildLegDamage` and `BuildLegFrame` look up `tjoint_*` and `skid_*` and `ContactModel` splits half-skids; `tools/flightbench/handcalc/four_legs.py` stops at `PARTS["skid_right"]`; the player's `LegView.Kinds` and its tests still expect `tjoint` and `skid` meshes.
  - The skid rails leave the generator, the params and `drone.json`.
  - Each leg ends in its own tip as photos T and U show it.
  - The legs' contact shapes and masses are derived.
  - Both variants (legs on, legs off) still build with one command.
  - The object sheet and the review sheet are redone, with a side-by-side against photos T and U (local only).
- [ ] 1.3 [physics-engineer] **The law, built,** after 1.1 and 1.2.
  - The tips' penetration, hold and sliding resistance in `Runtime/Contact/`; the start's embedment drawn and logged.
  - A switch that flies the previous model's ground law with the skids' drone (a fixture) for the kept audits, in a header entry of its own.
  - Tests against 1.1's hand calculations:
    - two seeds start differently, and a replay is bit for bit;
    - a careful liftoff releases leg by leg and adds no upward speed at release;
    - a touchdown's rebound and slide stay within the calculation.
  - The goldens are re-recorded once and audited case by case.
  - The pilot's account in PHY-03 holds (sometimes on firm dry ground, more often on softer ground, most of the time in rain).
  - 0 B in steady state; the step's cost is measured against PLT-04.
- [ ] 1.4 [game-developer] **The player on four legs,** after 1.3.
  - The drone's view and leg placement without skids.
  - The leg shots of the smoke test.
  - The summary's names.
  - The start standing in the ground as the flight drew it.
- [ ] Checks for 1.3 and 1.4: `dotnet test tools/simtests -c Release`; `flightbench --golden`, `--landing`, `--legs`, `--ground`, `--slopes`, `--contacts`; Unity's EditMode suite; `python tools/hygiene/precommit.py --tree HEAD`; `openspec validate w17-four-legs --strict`.
- [ ] 1.5 [asset-artist] **The legs as photo W shows them** (the pilot, 2026-11-17, D-119: "narrow tubes pointing perpendicularly into the ground"). Task 1.2's tubes are refined, not replaced.
  - **Read photo W,** each number with its error:
    - where each leg stands along its arm, and how it is fixed there;
    - the tube's outer diameter, and its length from the arm to the tip;
    - whether the four are alike and perpendicular to the ground;
    - what the tip's end is;
    - the clearance between the canister and the ground.
  - **The model:** the four tubes moved, resized and fixed as read. The canister and everything else stay as they are.
  - **For the physics** (task 1.3 reads them from `drone.json`): the tips' places, the free length, the tube's section, the contact shapes, and the masses and totals with what changed. The legs' stiffness and damping are 1.3's to derive.
  - Both variants (legs on, legs off) still build with one command. The skids' drone stays as the fixture.
  - The object sheet and the review sheet are redone, with a side-by-side against photo W (local only).

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec's scenarios. Re-fly the pilot's liftoffs and touchdowns on their own sticks and compare with 1.1's baseline. Check that nothing outside the ground contact moved beyond the drone's new mass. Look at the drone on its legs in the game.

## 3. The build

- [ ] 3.1 [orchestrator] Merge into the line with its model number, then the pilot's build branch; the build pointer; tell the pilot.
