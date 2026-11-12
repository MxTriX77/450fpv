# Tasks: w9-village-physics

Branch `unity/physics/physics-integration-2--w9-village`, cut from `--w3-ground` at 241145ea (the pilot's preview, model 85). The audit (1.1) is `audit.md`; its §7 orders the work and this list follows it. The audit's "G1…G8" are its gaps (§0).

**Order:**
- first, in parallel: 2.4 (G1), 2.7 (G5) and 2.8 (the plants' numbers);
- 2.6 (G2) after 2.4, and 2.1 (the stem field) after 2.8;
- 2.11 (the published stem and leaf properties) any time before 2.2: it needs no code and no other task;
- 2.2 (stems on the props) after 2.1 and 2.11; then 2.3 (leaves, also after 2.7), 2.5 (cut bits) and 2.10 (stems on the legs and the fiber);
- 2.9 (Village's ground rows and the liftoff harness) after 2.4 and 2.6;
- 3.1 last.

Tasks that run in parallel work on sub-branches of this branch, which the orchestrator merges back.

**Numbers** (`docs/handover-2026-11-12.md` §5, D-072):
- **Model 86** for this change's first physics to merge (2.4), shared by 2.6 and 2.7 until a pilot build ships it. 2.2 takes the next free model at its merge.
- **`QueryVersion` 9** for 2.1.
- **Event kind 39** for 2.2's stem episode. `LogFormat.Version` 73 only if 2.2 changes the cover rows' layout.

**Not in this change** (audit §7, item 9):
- **Furrows.** Neither map has them in the physics, and Village draws them only as a texture and a normal map, so there is nothing drawn for a furrow's collision to follow (REAL-05). They need a world-and-look unit that draws the relief near the drone and stores it in the package. That unit is logged for the plan, not built here; `tilled` already gives the loose loam's bearing and friction.
- **The wind's cover per cell** from the stem field: W7's follow-up after 2.1.

## 1. The audit

- [x] 1.1 [physics-engineer] The audit, `audit.md` in this change. No code.
  - **As built** (304d8c81): every interaction in a table (§1) with its evidence, the three known gaps confirmed or corrected (§2), what W2's export gives a stem field (§3), published stem and leaf properties and the hand calculation of one straw on one blade (§5), and the ordered work (§7).
  - **Works on Village from its own data:** the soil per 0.49 m cell (a 1 m/s landing on `tilled` sinks within 0.08–0.36 mm of a floor of that class, dry, Rain and Severe rain), the wet hold, a drone at rest on a slope (within 1° of W3's slope packages at 10–25°), the props carving the ground, the crowns' twigs.
  - **Gaps:** G1 the soil anchors' runaway lever (a bug, both maps); G2 the legs parting on a 10° side slope; G3 nothing reads Village's grass (0 micro elements against the range's 482,356); G4 no prop has ever cut grass or straw on either map; G5 a crown's repeated zero-mass blade break (a bug); G6 furrows; G7 the held leg is measured nowhere; G8 a strike never loads an arm's mode.

## 2. The physics

- [ ] 2.1 [world-builder] **Village's stem field** (audit §3), after 2.8.
  - Per cell and layer, from `grass/map.raw`, `map.json`'s layers and 2.8's stems per instance: stems per m², the height range, and the kind from the layer's group (green blades, dry culms, burnt culms). A cell with no drawn grass has no stems. Never hand-painted.
  - First check in the editor whether `TerrainData.ComputeDetailInstanceTransforms` gives exactly the drawn instances. Choose per cell or per instance with its cost (2.84 M instances), and say why.
  - A world query for the physics (by point and radius, or per cell), allocation-free, DetMath only. `QueryVersion` 9.
  - `validate.py` checks the field against the export's counts.
  - Checks: `python tools/map/validate.py maps/farmland`, `worldbench --golden` and `--bench`, `dotnet test tools/simtests`, `precommit.py --tree HEAD`. Worldbench's existing farmland cases must not move.
- [ ] 2.2 [physics-engineer] **Stems on the props** (audit §5, G4 and G8), after 2.1. Write design.md first.
  - Per-kind rows in `surfaces.json` from the published values (audit §5.1), and the research the table still owes: green blades, dry culms, weed stalks, the critical cutting speed of an unsupported stem.
  - The downwash bending the stems before the disc reaches them, and the critical speed against v = ω·r, so most stems escape as the pilot describes. Without both, the hand calculation stalls a rotor in 35 ms.
  - The cuts per pass as a mean count with no draw (as `PropStrikes.Twigs`); each cut's work and impulse, with the flung piece's direction from the face it meets; a stem already cut stands lower.
  - The strike's axial share on the arm's mode at its tip, in the step it happens (G8); the rotor's dip and stall.
  - The log: per blade per step in `BladeStrikes`, and one event kind (39) for a rotor's stem episode, start and end. Per-cut events would break LOG-01. The cut pieces for the feed go out in the step's `FeedSignals`.
  - **The range's straw joins the same law:** House flies in the player again (D-100), so S04's elements feed the props as Village's stems do, and the range golden moves where a disc meets S04, audited.
  - Checks: the spec's straw landing (0.5 m/s into the densest tall cell, (768.07, 917.48)) with its spectrum against design.md's hand calculation within 10 %; a hover over grass that cuts itself clear; bit-exact replay, 0 B, PLT-04 on a quiet machine; `dotnet test tools/simtests`, `flightbench --golden`, `precommit.py --tree HEAD`.
- [ ] 2.3 [physics-engineer] **Leaves in crowns**, after 2.7 and 2.2.
  - Each variant's leaf area density from 2.8. The leaf's fling (its fresh mass over the chord; leaf mass per area 40–120 g/m², published) and cut (work to shear 0.022–0.539 J/m, published).
  - Move `CrownTwigLength`, `CrownTwigDiameter` and the leaf's constants into `physics_materials.json` (`twig` and a new `leaf` block), each labelled with its source.
  - Checks: the spec's crown flight (2 m/s into a crown), `ObjectContactTests`.
- [ ] 2.4 [physics-engineer] **G1: the soil anchors' runaway lever** (audit §1.6). Model 86.
  - Reproduce with the audit's two rows: the 9.98° weeds package along the fall line, and Village at (311.19, 50.71) along. Print each corner's anchor stretch and find the line (candidate `ContactModel.cs:3168–3179`).
  - Bound the anchor's stretch by what the leg and skid can physically reach, and settle the question W3 2.2 §3 left: the trial's force and the cone's slide use different stiffnesses.
  - A new test: in no step does the contacts' moment exceed the sum of their forces' sizes times the drone's reach (0.5 m), over every slope row and the audit's Village spots. Today's rule is its known-wrong.
  - Checks: `flightbench --slopes`, `--legs`, `--contacts`, `--golden`; `dotnet test tools/simtests` and `tools/inputtests`; a recorded flight replays bit for bit; 0 B; `precommit.py --tree HEAD`.
  - Goldens it could move, each audited case by case: the range golden, every kept audit whose anchors slide, p7g's `LegChangeTests`, `LegReplayTests` and `RangeContactTests`, `SlopeTests`' landing table.
- [ ] 2.5 [tech-artist] **Cut bits on camera**, after 2.2. From `FeedSignals`' cuts per rotor, drawn as pieces leaving the front discs at the blade's speed, in front of the camera. A replay draws the same pieces. The feed's look is otherwise unchanged (D-026).
- [ ] 2.6 [physics-engineer] **G2: the legs on a side slope** (audit §1.7), after 2.4.
  - Rerun the cross-slope landings and trace the clamp moments. Judge against p7g-legs' failure law and its sources whether 10 × 8 mm PVC-U legs part about 0.3 s after a 0.5 m/s cut landing across 10°, and write the reading in design.md.
  - Checks: `flightbench --legs`, `--slopes land`. Goldens move only if the law changes (then p7g's, audited).
- [ ] 2.7 [physics-engineer] **G5: the crown twig's repeated zero-mass break** (audit §1.15). In parallel with 2.4.
  - Reproduce with a hover inside the bush crown at (525.79, 612.74), 1.24 m up: 200,000 `BladeDamaged` events in 25 s. Fix `PropStrikes.Twig` (a break at the blade's own tip is no break) and bound the events.
  - A new test: a hover in a crown logs no `BladeDamaged` with 0 kg lost.
  - Checks: `ObjectContactTests`, W3 2.7's crown flight re-measured, `dotnet test tools/simtests`, `flightbench --golden`, `--contacts`. No golden is expected to move.
- [x] 2.8 [asset-artist] **The plants' numbers for the props** (audit §3, items 1 and 4). In parallel with 2.4.
  - **As built** (e4f17a6f, d04f5735, 63b184b2): `tools/blender/vendor_plant_numbers.py` → `assets-src/vendor_plant_numbers.json`, method and limits in design.md 2.8. A stem is a drawn blade crossing a horizontal plane (opaque runs joined across a card's triangles), and one instance holds 110–2,050 of them by prefab. Leaf area density is 0.21–2.09 m²/m³ of twig shell over the 16 leaved variants. The re-run is byte-identical, and the hand count on GrassDry_C and Grass_A is within about 15 %.
  - **Stems per instance:** for each of the 14 grass layers' prefabs, how many blades or culms one drawn instance holds, counted from its mesh locally.
  - **Leaf area density:** for each vendor tree and bush variant, m² of drawn leaf per m³ of crown, from its foliage cards.
  - Only the derived numbers are pushed (a params file and the script that measures them); no vendor geometry leaves the store (D-089).
  - Checks: the script re-runs to the same numbers; a hand count on two prefabs; `python -m pytest tools/assets`; `precommit.py --tree HEAD`.
- [ ] 2.9 [physics-engineer] **Village's ground rows and the liftoff harness** (G7), after 2.4 and 2.6.
  - The spec's slope landing and rest on the audit's Village spots against W3's slope packages of the same angle and class, as tests and `flightbench` rows.
  - The soft-soil landing on `tilled` against a floor of `tilled`.
  - p7a-ground design 13's liftoff harness (never built) on Village's classes and slopes, dry, Rain and Severe rain, with its four known-wrong models. Look at why the body sinks further during a held climb on wet soil (audit §1.3).
  - Checks: the rows themselves. New rows only.
- [ ] 2.10 [physics-engineer] **Stems on the legs and the fiber** (G3; audit §7 items 9c and 9d), after 2.2.
  - The legs in standing stems: the touchdown jitter and the hooks (p7a-ground 1.14), from the stem field.
  - The laid fiber over Village's stems as over S04's on the range (flight 37's slips and contacts on stems and straw).
  - Checks: a landing in the densest tall cell prints the stems' forces on each leg; a line laid across a grass field logs its contacts and slips on stems; bit-exact replay, 0 B, the step within PLT-04 on a quiet machine.
- [ ] 2.11 [reference-analyst] **The published stem and leaf properties** that 2.2 and 2.3 need (audit §5.1, "not found readable here"). Research only, no code.
  - Per kind (green grass blades and culms, dry standing grass culms, weed stalks, cereal straw, tree and bush leaves): size, mass per length, bending stiffness, shear strength or cutting energy, each with its moisture state and source.
  - The critical speed for cutting an unsupported stem on impact, and how it moves with the stem's stiffness and mass and the blade's edge; the energy of an impact cut against a quasi-static one.
  - Stand densities (stems per m²) to check the stem field's counts against.
  - Written to `docs/reference-notes/stem-properties.md`: a table per kind with the value, its range, the conditions and the source actually read, then what 2.2 should use and what stays assumed.
  - Checks: every row names a source that was read (the page or abstract, never a guess); `precommit.py --tree HEAD`.

## 3. Review

- [ ] 3.1 [qa-engineer] Review against the spec scenarios: scripted landings on Village on a slope, on soft soil, and in tall grass and straw, plus a crown flight; G1's no-runaway test over every slope row; the liftoff harness's rates against the pilot's account; bit-exact replays and 0 B; the step budget on a quiet machine; every moved golden audited.
