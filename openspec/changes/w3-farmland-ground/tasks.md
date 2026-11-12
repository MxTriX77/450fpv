# Tasks: w3-farmland-ground

Order: 1.1, then 2.1; 1.2 once W4a and W4b have merged onto the line; 2.2 alongside 1.2; then 2.5, with 2.6 alongside on `--w3-blade`, then 2.4 and 2.3; 3.1 alongside on `--w3-flight`; then 4.1. 2.1's farmland case is re-run with the objects after 1.2. Timing verdicts need a quiet machine.

## 1. The heightfield

- [x] 1.1 [world-builder] The world-query core over `maps/farmland/`'s 17 tiles: `SampleGround`, `Raycast` and `StaticContacts` triangulated as `Tile.height` (bit-exact at the 1,000 probes), a lookup per tile, the 12 step lines as walls, the hole mask read, off-world as a hole, surfaces per cell. `QueryVersion` 8, 0 B, worldbench rows, goldens audited case by case. Write `design.md`. Checks: `dotnet test tools/simtests`, worldbench `--golden` and `--bench`, flightbench `--golden`, `precommit.py --tree HEAD`.

- [ ] 1.2 [world-builder] The farmland's objects in the world-query core: `placements.json` rows (full `rotation_xyzw`, per-axis `scale`; a ruin with scale x < 0 uses `Ruin_Walls_mirrored`) with W4a's plant metadata (variant → `VFnn` via `assets-src/vendor_plants.json`; capsule ends scaled by (w, h, w), radius by w) and W4b's ruin and debris metadata; gaps and snag records placed the same way. The object grid on `map_rect_m`. Bit-exact goldens, worldbench rows over the village and a tree belt, 0 B. Checks: as 1.1.
  - Also: `FarmlandTests.Queries_AllocateNothingAfterTheFirst` allocates 24 B in Release only (since 1.1); and worldbench's `farmland.fiber` rows use `QueryFilter.NoGround`, as the fiber does.

## 2. The flight on hills

- [x] 2.1 [physics-engineer] L6, the fiber's query cost: no full `StaticContacts` query per step for a resting segment near shapes, and no terrain cost for the fiber's own queries (its nodes take the ground from `SampleGround`; 1.1's design suggests a `QueryFilter.NoGround`). The three model-79 flights' worst stretches (L1's report) and a 1.5 km line laid over the farmland's terrain replay within PLT-04's step budget in .NET; the same line through a tree belt and the village is re-run after 1.2. Same checks as 1.1.
  - As built (design.md §2.1): `QueryFilter.NoGround`, `IWorldQuery.ShapeContact`, the corridor walk in the object grid, near lists per uncertified segment, a hashed `AddHit`; model 79 and `QueryVersion` 8 unchanged, replays bit for bit, 0 B. The fiber's whole `StaticContacts` per step in flight 35 fell from 266–372 µs to 15–21 µs.
  - **The scenario is not yet shown:** on a loaded machine flights 35 and 38 reach p99 507–592 µs; a quiet run is owed (QA). Flight 37's straw stretches (p99 1.2–1.8 ms) spend their time outside the fiber's path: 2.3.
- [x] 2.2 [physics-engineer] The legs' and crash contact on slopes of 0–38°, the fiber's ground contact over the heightfield, and the furrow decision with its reason. flightbench rows on slopes; model 82 if the contact model changes, with goldens audited. Same checks as 1.1.
  - Also: the clearance certificate lets a segment's ends move margin − radius before re-querying, looser than `InsertGap` needs, so a shape can come within the line's radius without a contact slot; fix it (model 82, goldens audited). And the drone's own contacts on the terrain within PLT-04: half the farmland line's slow steps come while the drone sits and climbs.
  - As built (design.md §2.2, model 82): on a terrain a support stands on its sweep's nearest piece (a step's face included), a skid's sweep leaves the ground out, a blade meets the ground as its contact's plane, and a fiber node behind a step's face takes the face; a sample's overlap is r − n_y·(y − h); a leg's anchor takes no slide along a nil stiffness (a 709 km run-away found by the 38° row); the certificate allows the margin less KeepGap (lockstep test: the same bits). No furrows in the physics: they are a texture until the world and the render carry them. The "sits and climbs" cost was the line case's own start: set level on a 5.4° facet, it broke all four legs, whose pieces rolled for 4.5 s; it now starts on the facet. The line case's step passes PLT-04 at p99 (266–287 µs, loaded). Every golden and kept audit unchanged; the model-79 flights replay bit for bit.
  - **Owed:** the timings are a loaded machine's (other sessions at 38–100 % CPU); a quiet run of `farmland-line-step` is QA's (4.1).

- [ ] 2.3 [physics-engineer] Flight 37's straw stretches within PLT-04: the stems' cache walk, the hub rays through the straw boxes (`SoilUnder`, 60–100 µs) and the crash's own contacts (2.1's design names them). The three model-79 flights' worst stretches replay with p99 ≤ 300 µs and world queries < 60 µs on a quiet machine. Same checks as 1.1.

- [ ] 2.4 [physics-engineer] Contact with the farmland's objects (from W4a's and W4b's reviews):
  - a crash into a 2 cm wall skin peaks at 2,705 m/s² against 22,202 on a solid slab at 8.7 m/s: a wall stops a drone the same however thin its collision is;
  - the per-step contact event buffer overflows (4,205 events lost scraping a window frame);
  - `ContactModel.PairsPerShape` is 8, and 8 plant variants put 7–10 crown pieces within a prop's reach: decide whether compliant twig shells get more pairs, with W4a refitting if the rule changes;
  - a flightbench row flies into a bush (GreenBush, 579 placed);
  - snag checks index segments, not records (the debris rims are 1 cm stairsteps, 10,089 segments in the village).
  Model 82 if contact answers change, goldens audited. Same checks as 1.1.

- [ ] 2.5 [physics-engineer] A laid fiber follows the hills: today a long laid segment runs straight between its nodes and cuts through rises (5 of 31 segments on the 1.5 km line dip up to 3.24 m under the ground). Insert a node where a segment dips under the ground, and let the line wrap the ledge's lip (a ground-walls-only query, as 2.2's design suggests). The line's tension, contacts and replays stay physical; model 82's goldens audited if answers change. Same checks as 1.1, plus `flightbench --bench farmland-line-step` with no segment under the ground.

- [ ] 2.6 [physics-engineer] A crash creates no energy. `SessionTests.ACrashIntoR01ANamesThePartTheObjectAndTheMaterial` (A4, red since model 70) shows one 1 ms contact step at crash + 814 ms that gives 38.6 kN and +494 J (27 → 521 J). It happens on prop_fl's blade 0 in the step where the prop is lost and its motor binds. The drone then spins at 112 rad/s and climbs 2.4 m. Find the root cause (the suspect is the blade path's `v`, which includes `_restRate[c]·r`, in that step) and fix it. Over every contact step of a crash, mechanical energy plus rotor kinetic energy never rises by more than the motors' input. The test passes unchanged. On sub-branch `--w3-blade`, before 2.4; model 82, with goldens audited case by case. Same checks as 1.1, plus `dotnet test tools/inputtests`.

## 3. Flight on the farmland

- [ ] 3.1 [game-developer] The player flies the farmland from the village start point; the test range leaves the player (headless tests and benches keep it); the drawn terrain near the drone follows the heightfield (the tech artist checks). Checks: EditMode, the smoke test, PlayMode if any, a 2-minute flight's log replays bit for bit.
  - Runs on the sub-branch `--w3-flight` (from `--w3-ground` after the line merged), in parallel with 1.2 and 2.5; the orchestrator merges it back. The start point is W1's `FlightWorlds.FarmlandStart`. The farmland keeps the friend's light (D-088): the setup's time of day and weather apply to it only if they can without changing that look; record the choice. Wind applies (physics). W6's `FarmlandFeedDetail` comes with W6's merge.

## 4. Review

- [ ] 4.1 [qa-engineer] Review against the spec; the checks; 0 B and bit-exact replays; a low flight over the hills, the village and a tree belt.
