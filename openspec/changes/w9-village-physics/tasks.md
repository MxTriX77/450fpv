# Tasks: w9-village-physics

Branch `unity/physics/physics-integration-2--w9-village`, cut from `--w3-ground` at 241145ea (the pilot's preview, model 85). The audit (1.1) is `audit.md`; its §7 orders the work and this list follows it. The audit's "G1…G8" are its gaps (§0).

**Order:**
- first, in parallel: 2.4 (G1), 2.7 (G5) and 2.8 (the plants' numbers);
- 2.6 (G2) after 2.4, and 2.1 (the stem field) after 2.8;
- 2.11 (the published stem and leaf properties) any time before 2.2: it needs no code and no other task;
- 2.2 (stems on the props) after 2.1 and 2.11; then 2.3 (leaves, also after 2.7), 2.5 (cut bits) and 2.10 (stems on the legs and the fiber);
- 2.9 (Village's ground rows and the liftoff harness) after 2.4 and 2.6;
- 2.12 (the landing in high grass with straw) after 2.2, before 2.5 and 2.10: they build on its stalks;
- 3.0 (the map-wide sweep) any time: it needs no other task and reruns as fixes land;
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

- [x] 2.1 [world-builder] **Village's stem field** (audit §3), after 2.8.
  - **As built** (51613910, b387499d, e9507ff0; design.md 2.1):
    - **The editor check:** `ComputeDetailInstanceTransforms` gives exactly the counted instances at their drawn places, at any density (`useDensityScaling` is off). It doesn't give the drawn blades: the renderer adds `alignToGround` 0.5's tilt and the shader's sway.
    - **The field:** per cell and layer, derived at load from `grass/map.raw`, `map.json` and 2.8's numbers. It holds stems per m² reaching a height, the kind by group and the drawn tops. No package change; the content hash takes the plants' numbers.
    - **The query:** `StemsNear(XZ centre, double radius, double height, Span<StemPatch>)`, `QueryVersion` 9. Only the version lines and the farmland's hash moved in the goldens.
    - **Its cost:** 14.0 MiB held, 46 ms of load, four rotor discs a step at a median of 3.7 µs with 0 B (indicative).
  - Per cell and layer, from `grass/map.raw`, `map.json`'s layers and 2.8's stems per instance: stems per m², the height range, and the kind from the layer's group (green blades, dry culms, burnt culms). A cell with no drawn grass has no stems. Never hand-painted.
  - First check in the editor whether `TerrainData.ComputeDetailInstanceTransforms` gives exactly the drawn instances. Choose per cell or per instance with its cost (2.84 M instances), and say why.
  - A world query for the physics (by point and radius, or per cell), allocation-free, DetMath only. `QueryVersion` 9.
  - `validate.py` checks the field against the export's counts.
  - Checks: `python tools/map/validate.py maps/farmland`, `worldbench --golden` and `--bench`, `dotnet test tools/simtests`, `precommit.py --tree HEAD`. Worldbench's existing farmland cases must not move.
- [x] 2.2 [physics-engineer] **Stems on the props** (audit §5, G4 and G8), after 2.1. Write design.md first.
  - **As built** (design.md 2.2, its "As built"):
    - `PropStems.cs`: the slipstream (q = T/A) lays the stems over, and a blade cuts only where ω·r·cos φ reaches the stem's critical speed (k_edge 3). The cuts are mean counts for the field and the stalks met per pass for S04's elements. Each disc keeps the height and stubs it cut to and a trail of where; the stalks' cut planes are in a grid.
    - The new cover rows (`grass_blade`, `dry_culm`, `burnt_culm`, with `diameter_m`); straw's 22 kJ/m² published.
    - G8: `AirframeModes.Strike` from `PropStrikes.TipImpulse`.
    - Event 39; `StrikeKind.Stem`; `FeedSignals.StemCuts…`; a header entry `TagStems`. No `LogFormat.Version` change, no `QueryVersion` change, `ContactModel.cs` and `LogFormat.cs` untouched.
  - **Measured:**
    - The spec's vertical straw landing cuts no stem under the discs: Village's 1.5 mm culms lie at 0.12–0.16 m in the 222–236 Pa slipstream, under the standing discs' 0.288 m. The known-wrong takes 8.17 / 7.81 W at 1.0 m against the hand calculation's 8.40 / 8.05.
    - The pilot's forward creep cuts at the front discs' leading edges within 2.1 % of hand calculation C (rate, power and the blade-pass line); the rear discs run in their track.
    - A hover cuts itself clear; S04's stalks are cut by the same law.
    - Bit-exact replay; 0 B; +5.7 µs median a step, indicative.
    - No golden moved (61 PASS).
  - Per-kind rows in `surfaces.json` from the published values (audit §5.1), and the research the table still owes: green blades, dry culms, weed stalks, the critical cutting speed of an unsupported stem.
  - The downwash bending the stems before the disc reaches them, and the critical speed against v = ω·r, so most stems escape as the pilot describes. Without both, the hand calculation stalls a rotor in 35 ms.
  - The cuts per pass as a mean count with no draw (as `PropStrikes.Twigs`); each cut's work and impulse, with the flung piece's direction from the face it meets; a stem already cut stands lower.
  - The strike's axial share on the arm's mode at its tip, in the step it happens (G8); the rotor's dip and stall.
  - The log: per blade per step in `BladeStrikes`, and one event kind (39) for a rotor's stem episode, start and end. Per-cut events would break LOG-01. The cut pieces for the feed go out in the step's `FeedSignals`.
  - **The range's straw joins the same law:** House flies in the player again (D-100), so S04's elements feed the props as Village's stems do, and the range golden moves where a disc meets S04, audited.
  - Checks: the spec's straw landing (0.5 m/s into the densest tall cell, (768.07, 917.48)) with its spectrum against design.md's hand calculation within 10 %; a hover over grass that cuts itself clear; bit-exact replay, 0 B, PLT-04 on a quiet machine; `dotnet test tools/simtests`, `flightbench --golden`, `precommit.py --tree HEAD`.
- [x] 2.3 [physics-engineer] **Leaves in crowns**, after 2.7 and 2.2. Widened (2026-11-13) by 2.4's anchors on shells, 2.7's held props and 2.7's event overflow: what a crown's twigs hold.
  - Each variant's leaf area density from 2.8. The leaf's fling (its fresh mass over the chord; leaf mass per area 40–120 g/m², published) and cut (work to shear 0.022–0.539 J/m, published).
  - Move `CrownTwigLength`, `CrownTwigDiameter` and the leaf's constants into `physics_materials.json` (`twig` and a new `leaf` block), each labelled with its source.
  - Checks: the spec's crown flight (2 m/s into a crown), `ObjectContactTests`.
  - **As built** (d02e4a0e…, design.md 2.3): the twigs hold a part only as they bend and break (W̄ 0.115 J a twig over δ̄ 37 mm, L_V·p/4 twigs a metre: 0.75 N of the drone's 46 N, the drop test within 0.7 % of the hand); a prop is never frozen by them; a leg's anchor lets go past their reach. Leaves per variant (2.8's LAD), flung and cut at the rim, kept cleared in a store (the 2 m/s pass within 5.4 % of the hand). The crowns' constants are `physics_materials.json`'s `twig.crown` (one block for twigs and leaves). The event buffer is sized from the contact table: none refused (the bush's first step makes 271). The crown test flies with the stems on: at 6.85 m/s it crashes on a branch, at 2 m/s it flies through; it never hangs on twigs. No golden moved; `--contacts`' farmland crowns now meet their branches at speed, and two post-crash tunnels fail that bench (named, not fixed: a hanging half and a resting blade through thin bark).
- [x] 2.4 [physics-engineer] **G1: the soil anchors' runaway lever** (audit §1.6). Model 86.
  - Reproduce with the audit's two rows: the 9.98° weeds package along the fall line, and Village at (311.19, 50.71) along. Print each corner's anchor stretch and find the line (candidate `ContactModel.cs:3168–3179`).
  - Bound the anchor's stretch by what the leg and skid can physically reach, and settle the question W3 2.2 §3 left: the trial's force and the cone's slide use different stiffnesses.
  - A new test: in no step does the contacts' moment exceed the sum of their forces' sizes times the drone's reach (0.5 m), over every slope row and the audit's Village spots. Today's rule is its known-wrong.
  - Checks: `flightbench --slopes`, `--legs`, `--contacts`, `--golden`; `dotnet test tools/simtests` and `tools/inputtests`; a recorded flight replays bit for bit; 0 B; `precommit.py --tree HEAD`.
  - Goldens it could move, each audited case by case: the range golden, every kept audit whose anchors slide, p7g's `LegChangeTests`, `LegReplayTests` and `RangeContactTests`, `SlopeTests`' landing table.
  - **As built** (design.md §2.4): the line is the cone's slide, which divided the lever's statics and dampers by the stretch's own spring (9.8 N/m on a 0.2 N corner) and ran the stretch to 39 m. A stretch past what the leg's lever reaches turned square (γL 0.176 m, the frame's 0.187 m) is let go: the hold starts again at the foot (`ContactModel.Reached`). This is applied in a soil corner's trial and slide and to a leg point on the ground; an object's shell is left to 2.3 and 2.7. `AnchorReachTests`: 34 landings, the largest lever 0.357 m; the known-wrong, 85's slide (`ContactModel.AnchorReach` off, unlogged), 35.0 m. Village and package agree at 10° along (both rest). Model 86; no golden re-recorded, 61 PASS. Three p7g pins re-pinned with their audits; `farmland_wind.version` 1.86.
- [ ] 2.5 [tech-artist] **Cut bits on camera**, after 2.2. From `FeedSignals`' cuts per rotor, drawn as pieces leaving the front discs at the blade's speed, in front of the camera. A replay draws the same pieces. The feed's look is otherwise unchanged (D-026).
- [ ] 2.6 [physics-engineer] **G2: the legs on a side slope** (audit §1.7), after 2.4.
  - Rerun the cross-slope landings and trace the clamp moments. Judge against p7g-legs' failure law and its sources whether 10 × 8 mm PVC-U legs part about 0.3 s after a 0.5 m/s cut landing across 10°, and write the reading in design.md.
  - Checks: `flightbench --legs`, `--slopes land`. Goldens move only if the law changes (then p7g's, audited).
- [x] 2.7 [physics-engineer] **G5: the crown twig's repeated zero-mass break** (audit §1.15). In parallel with 2.4.
  - Reproduce with a hover inside the bush crown at (525.79, 612.74), 1.24 m up: 200,000 `BladeDamaged` events in 25 s. Fix `PropStrikes.Twig` (a break at the blade's own tip is no break) and bound the events.
  - A new test: a hover in a crown logs no `BladeDamaged` with 0 kg lost.
  - Checks: `ObjectContactTests`, W3 2.7's crown flight re-measured, `dotnet test tools/simtests`, `flightbench --golden`, `--contacts`. No golden is expected to move.
  - As built: the breaks were `RestDamage`'s, the props standing still in the shells with a load radius of 4.5–5.5 R (the pairs' points on the shell's plane); the radius is held to the tip and `Breaks` skips a station at or past the tip. 150,239 zero-mass breaks in 25 s → 1 real one; no golden moved; W3's crown flight unchanged to the bit (design.md 2.7).
- [x] 2.8 [asset-artist] **The plants' numbers for the props** (audit §3, items 1 and 4). In parallel with 2.4.
  - **As built** (e4f17a6f, d04f5735, 63b184b2): `tools/blender/vendor_plant_numbers.py` → `assets-src/vendor_plant_numbers.json`, method and limits in design.md 2.8. A stem is a drawn blade crossing a horizontal plane (opaque runs joined across a card's triangles), and one instance holds 110–2,050 of them by prefab. Leaf area density is 0.21–2.09 m²/m³ of twig shell over the 16 leaved variants. The re-run is byte-identical, and the hand count on GrassDry_C and Grass_A is within about 15 %.
  - **Stems per instance:** for each of the 14 grass layers' prefabs, how many blades or culms one drawn instance holds, counted from its mesh locally.
  - **Leaf area density:** for each vendor tree and bush variant, m² of drawn leaf per m³ of crown, from its foliage cards.
  - Only the derived numbers are pushed (a params file and the script that measures them); no vendor geometry leaves the store (D-089).
  - Checks: the script re-runs to the same numbers; a hand count on two prefabs; `python -m pytest tools/assets`; `precommit.py --tree HEAD`.
- [x] 2.9 [physics-engineer] **Village's ground rows and the liftoff harness** (G7), after 2.4 and 2.6.
  - The spec's slope landing and rest on the audit's Village spots against W3's slope packages of the same angle and class, as tests and `flightbench` rows.
  - The soft-soil landing on `tilled` against a floor of `tilled`.
  - p7a-ground design 13's liftoff harness (never built) on Village's classes and slopes, dry, Rain and Severe rain, with its four known-wrong models. Look at why the body sinks further during a held climb on wet soil (audit §1.3).
  - Checks: the rows themselves. New rows only.
  - **As built** (design.md §2.9): `flightbench --ground [seeds] [--nominal-legs]` (`GroundCase`, `LiftoffScenarios`): the session's airframe, a cut touch (vertical or the glide), 2 s at rest, the pilot's gentle liftoff (hover + 0.1 over 0.5 s, then + 0.2 a second), on Village's five classes and two 10° slopes and House's track start and S04 patch, dry, Rain and Severe rain, read for design 13's lone corner and for "one or many". 10 seeds a cell: one or many 62 % of soft-ground liftoffs in Rain and 80 % in Severe rain, 3 % dry soft, 0 dry firm; the lone corner 13 % in Severe rain, 2 % in Rain, 0 dry: **design 13's lone-corner targets not met**, the four known-wrongs each miss theirs. Why (measured): the audit's liftoff flew a symmetric drone and a ramp no pilot flies; no soil law was changed (a per-foot hold spread, the groove's venting and its confinement were measured and not built); dry ground needs 2.10's hooks, the lone corner the world's variation under the feet. `leg.stiffness` (D-086, design 12) built as an opt-in drawn build (`LegDamageSpec.DrawStiffness`, σ 3 %, ±10 %); its effect is within the seeds' scatter. `ContactSpec.AnchorReach` and the spread logged in a tagged header entry (28), no format change; no golden moved. Village against the packages within 1° where both stand; tilled landing within 0.30 mm of the floor. S04's patch throws in `PropStrikes.Roots` on every liftoff (not this branch's). The app's `FlightSession` must call `DrawStiffness()`.
- [ ] 2.10 [physics-engineer] **Stems on the legs and the fiber** (G3; audit §7 items 9c and 9d), after 2.2.
  - The legs in standing stems: the touchdown jitter and the hooks (p7a-ground 1.14), from the stem field.
  - The laid fiber over Village's stems as over S04's on the range (flight 37's slips and contacts on stems and straw).
  - Checks: a landing in the densest tall cell prints the stems' forces on each leg; a line laid across a grass field logs its contacts and slips on stems; bit-exact replay, 0 B, the step within PLT-04 on a quiet machine.
- [x] 2.11 [reference-analyst] **The published stem and leaf properties** that 2.2 and 2.3 need (audit §5.1, "not found readable here"). Research only, no code.
  - Per kind (green grass blades and culms, dry standing grass culms, weed stalks, cereal straw, tree and bush leaves): size, mass per length, bending stiffness, shear strength or cutting energy, each with its moisture state and source.
  - The critical speed for cutting an unsupported stem on impact, and how it moves with the stem's stiffness and mass and the blade's edge; the energy of an impact cut against a quasi-static one.
  - Stand densities (stems per m²) to check the stem field's counts against.
  - Written to `docs/reference-notes/stem-properties.md`: a table per kind with the value, its range, the conditions and the source actually read, then what 2.2 should use and what stays assumed.
  - Checks: every row names a source that was read (the page or abstract, never a guess); `precommit.py --tree HEAD`.
  - **As built:** `stem-properties.md` holds 21 sources (full text, abstract or restated, each marked), a derived critical-speed law checked against grass, thick stalks and support distance, and section 4's row per cover kind.
  - The original impact-cutting papers (O'Dogherty & Gale, McRandal & McNulty, Persson) stayed unreadable; a green culm's modulus and cut energy, burnt stems and the prop's edge factor stay assumed.

- [x] 2.12 [physics-engineer] **The landing in high grass with straw, as the pilot describes it.** Their words (2026-11-12): "in real life if i land on a high grass with straw - straws are noticeable cut in front of my quads camera and my quad is shaking. shaking how - again, you calculate properly".
  - **As built** (design.md 2.12): both the inputs and the law were wrong. Each field stem is now placed from the world by its own hash, with d = d₁·L^(1/1.53) (Niklas 1995; d₁ 4.49 mm dry from Yao et al. 2012's wheat, 1.81 mm green), so every drawn stem can stand. A vertical jet folds it (cross-flow sin² load, its own weight, the wake's swirl) at its own first-mode pace, into an arch whose crest is 0.5·l_b. Each cut is a discrete hit. Creep within 4 % of the hand calculation (rate and work). The quad shakes: 2.3 °/s and 0.07° at 4–15 Hz in Village's densest cell's vertical landing; 0.1–16 °/s over a sweep of density ×0.5–2 and diameter ×0.75–1.5, a front rotor stalling at the thick end. 61 goldens unchanged; replay bit for bit; 0 B.
  - **What 2.2 gives today:** every Village kind is a 1.5 mm culm, so the slipstream lays them at 0.12–0.16 m, a vertical landing cuts nothing, a creep cuts about 33 stems a second for 0.7 W, and the shake is nil. That contradicts the account, and the pilot's words win.
  - **Find what is wrong,** by calculation and published sources, in this order:
    - **The tall layers' stalks.** A dry stem standing 1.2–1.8 m can't be 1.5 mm across (check its self-buckling height). Take each layer's diameter, wall and stiffness from its own drawn height: published allometry and measurements of tall dry grass, weed and cereal stalks. Per layer, not per kind.
    - **The lean law's load.** A vertical jet on an upright stem has little cross-flow; near the ground the flow turns into a wall jet. Check how far stems of each stiffness really lie over under a 10-inch disc at 230 Pa against published rotor-downwash-on-canopy measurements, and correct the law if it over-presses.
    - **The landing itself.** A real landing comes in moving and flares; "in front of my camera" is the front discs.
  - **Then the corrected inputs and law,** each number labelled, and the runs: a vertical landing and a landing approach into the densest tall cell and into a cell of high grass with straw. Report the stems cut a second at the front discs, the rotors' speed dips and currents, and the shake: roll and pitch rate in 4–15 Hz and at the blade pass, and the camera's vibration. Compare with the footage note's targets (letter H into straw: 30–120 °/s turns and a residue 3–10 times the calm one; brushing a stalk: 1.7–2.5 times in 4–15 Hz). Say plainly whether the quad shakes, and how.
  - Propose the spec's amended straw-landing scenario (the orchestrator applies it). A hand calculation first, the run within 10 % of it.
  - Checks: the new scenarios' tests, `dotnet test tools/simtests -c Release`, `flightbench --golden` (every moved golden audited), bit-exact replay, 0 B, `precommit.py --tree HEAD`.

## 3. Review

- [ ] 3.0 [qa-engineer] **A map-wide landing sweep with invariants** (the pilot asked for more tests, 2026-11-12). The spec's "every ground interaction works on Village" is tested at hand-picked spots only. This finds the spots nobody picked.
  - **The sweep:** a seeded mode of `flightbench` (`--sweep <map> <points> <seed>`). At each drawn start point, a hover-and-land, a cut landing at 0.5 m/s, a drop from 1 m and a slide at 2 m/s, each at a drawn heading. The points cover every ground class and slope band in proportion to its area, with at least 20 a class. The same seed gives the same rows, bit for bit.
  - **The invariants,** checked every step, each a row's verdict:
    - nothing NaN or unbounded, and the body never under the ground;
    - the contacts' moment at most the sum of their forces' sizes × 0.5 m (the spec's rule for G1);
    - the mechanical energy never rises in a step by more than the rotors', gravity's and the wind's work;
    - no part logs the same event twice in a step (G5's kind);
    - the drone at rest within 10 s of a landing;
    - the log replays bit for bit, and steady state allocates 0 B.
    - Add what the existing scenario tests already assert, where it holds at any spot.
  - **The findings:** one file per run, each failing row with its map point, heading, manoeuvre, seed, first failing step and the command that replays it. Nothing is fixed here: each finding becomes a task.
  - **Run it** on Village and on House at this branch's head, 400 points a map to start, and say how long it takes. Write the findings to `docs/reviews/w9-village-physics-sweep.md`. Rerun it when 2.4 and 2.6 land.
  - Checks: the sweep's own test (a known-wrong rule is caught, as 2.4's test does), the new tests in `dotnet test tools/simtests`, `precommit.py --tree HEAD`.
- [ ] 3.1 [qa-engineer] Review against the spec scenarios: scripted landings on Village on a slope, on soft soil, and in tall grass and straw, plus a crown flight; G1's no-runaway test over every slope row; the liftoff harness's rates against the pilot's account; bit-exact replays and 0 B; the step budget on a quiet machine; every moved golden audited.
