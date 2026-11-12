# Tasks: w3-farmland-ground

Order: 1.1, then 2.1; 1.2 once W4a and W4b have merged onto the line; 2.2 alongside 1.2; then 2.5, 2.4 and 2.3; 3.1 alongside on `--w3-flight`; then 4.1. 2.1's farmland case is re-run with the objects after 1.2. Timing verdicts need a quiet machine.

## 1. The heightfield

- [x] 1.1 [world-builder] The world-query core over `maps/farmland/`'s 17 tiles: `SampleGround`, `Raycast` and `StaticContacts` triangulated as `Tile.height` (bit-exact at the 1,000 probes), a lookup per tile, the 12 step lines as walls, the hole mask read, off-world as a hole, surfaces per cell. `QueryVersion` 8, 0 B, worldbench rows, goldens audited case by case. Write `design.md`. Checks: `dotnet test tools/simtests`, worldbench `--golden` and `--bench`, flightbench `--golden`, `precommit.py --tree HEAD`.

- [x] 1.2 [world-builder] The farmland's objects in the world-query core: `placements.json` rows (full `rotation_xyzw`, per-axis `scale`; a ruin with scale x < 0 uses `Ruin_Walls_mirrored`) with W4a's plant metadata (variant → `VFnn` via `assets-src/vendor_plants.json`; capsule ends scaled by (w, h, w), radius by w) and W4b's ruin and debris metadata; gaps and snag records placed the same way. The object grid on `map_rect_m`. Bit-exact goldens, worldbench rows over the village and a tree belt, 0 B. Checks: as 1.1.
  - Also: `FarmlandTests.Queries_AllocateNothingAfterTheFirst` allocates 24 B in Release only (since 1.1); and worldbench's `farmland.fiber` rows use `QueryFilter.NoGround`, as the fiber does.
  - As built (design.md, Task 1.2): the package's `placements.json` read and checked against `map.json`; each row's metadata found through the vendor lists; Unity's TRS for every shape, snag record and wind volume (a scaled round shape's radius by w; the mirrored house as its `_mirrored` twin); snag segments indexed per variant and tested in world space. 258,904 shapes, `QueryVersion` 8 (the floor's and the test range's goldens unchanged), 0 B. Every shape sits at its row's TRS within 1.6e-13 m, and the drawn vertices of 73 rows lie where the loaded shapes put them (the ruins and debris to the nanometre). The 24 B was the test's own boxed `EqualTo(0)`, not the world's.
  - **Owed:** the objects' bench rows miss PLT-04's 60 µs line at p99 on a loaded machine (147–167 µs), and 1.1's village row, now through two ruins' walls, misses the step's 300 µs (358 µs): the ruins' dense boxes, 2.3's cost. 2.1's line case re-run with the objects fails: the line tears at 690 m while the drone flies along the F6|F7 belt, the drone crashes, and the steps after reach a p99 of 10 ms (2.4, 2.3). The drone's 2 m snag cache meets up to 4,664 segments among the bushes against a capacity of 4,096 (2.4). A quiet run is QA's.

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

- [x] 2.5 [physics-engineer] A laid fiber follows the hills: today a long laid segment runs straight between its nodes and cuts through rises (5 of 31 segments on the 1.5 km line dip up to 3.24 m under the ground). Insert a node where a segment dips under the ground, and let the line wrap the ledge's lip (a ground-walls-only query, as 2.2's design suggests). The line's tension, contacts and replays stay physical; model 82's goldens audited if answers change. Same checks as 1.1, plus `flightbench --bench farmland-line-step` with no segment under the ground.
  - As built (design.md §2.5, model 82 extended):
    - `QueryFilter.GroundOnly`: StaticContacts and Raycast answer the ground alone.
    - On a terrain a segment is tested by its chord raised by KeepGap as a ground-only ray, held between tests by a certificate (`CrestDepth` 0.2 m).
    - A crest's node goes onto the ground at the deepest point's nearest surface point, the material shared by the pieces' taut paths. It is never merged away, with 16 nodes of room besides the spacing law's 32.
    - The ledge's lip is wrapped by the same rule.
    - Planes step as before: every golden unchanged, the model-79 flights replay bit for bit.
    - The farmland line: 0 of 42 segments deeper than 0.2 m (the deepest 0.096 m; before, 5 of 31, 4.10 m), whole, 0 B.
    - The farmland line's route now stops at 2.4 m/s²: its one-step stop wrapped the lip 165–172° on every core.
  - **Owed:** the step's p99 is +21 µs and the world queries' +11–27 µs (over the 60 µs line) on a loaded machine where neither core meets PLT-04. A quiet run is QA's (4.1). The far line's slide in Severe wind on hills (W7) drives the cost.

## 3. Flight on the farmland

- [x] 3.1 [game-developer] The player flies the farmland from the village start point; the test range leaves the player (headless tests and benches keep it); the drawn terrain near the drone follows the heightfield (the tech artist checks). Checks: EditMode, the smoke test, PlayMode if any, a 2-minute flight's log replays bit for bit.
  - Runs on the sub-branch `--w3-flight` (from `--w3-ground` after the line merged), in parallel with 1.2 and 2.5; the orchestrator merges it back. The start point is W1's `FlightWorlds.FarmlandStart`. The farmland keeps the friend's light (D-088): the setup's time of day and weather apply to it only if they can without changing that look; record the choice. Wind applies (physics). W6's `FarmlandFeedDetail` comes with W6's merge.
  - As built (design.md §3.1):
    - Train flies `FlightWorlds.Farmland` (`MapWorld.Load(root, "farmland")`) on the FarmLand scene from the village's start. The player's scenes are Boot and FarmLand, and its StreamingAssets hold the package's map.json and tiles; the test range left both.
    - The friend's light stays at every setup: time of day and rain change no look. Rain wets the ground and wind blows, in the physics.
    - Tiles within 30 m of the drone draw at pixel error 1. Measured from the drawn depth:
      - on the ground within 2 m, the sim's to 0.005 mm;
      - in flight within 2, 10 and 30 m, up to 3.0, 5.5 and 9.1 mm, each under a pixel;
      - the friend's errors gave up to 6.0, 32.9 and 39.2 mm.
    - `HillsPilot` flies two minutes over the hills (headless test; `-smokehills` in the player). The player's log replays bit for bit in .NET with `tools/replay`, with 0 tunnels and no body part past the ground (`GroundCheck`).
    - The smoke test's house flight became a flight into the ground.
  - **Owed:** the tech artist's review of the drawn terrain near the drone, and its frame cost with vsync off. Three tests already fail at this base (two `SessionTests` crashes into R01_A, `LegShotsTests`); they belong to their owners.

- [x] 3.2 [tech-artist] The drawn terrain near the drone (WLD-11, VIS-01: full resolution inside the physics radius, within 1 mm of `TerrainHeight`).
  - Review 3.1's `TerrainParity` method.
  - Decide with evidence:
    - accept pixel error 1 within 30 m (under a pixel, up to 9.1 mm in flight), with a decision entry and WLD-11's wording to match; or
    - draw a full-resolution patch near the drone from the same triangulation (0 mm, its cost measured).
  - Measure the chosen rule's frame cost against the friend's errors, A/B with vsync off. On a shared machine that is an indication only; the verdict comes in W6's quiet PLT-07 run.
  - On `--w3-flight`. Checks: `TerrainParity`, EditMode filtered, a review build and the smoke test, `precommit.py --tree HEAD`.
  - As built (design.md §3.2):
    - **Neither option as written:** Unity's pixel error goes below 1 when set from a script (only the Inspector stops at 1). Tiles within 30 m of the drone now draw at 0.05, which leaves no patch simplified within 35 m of the lens. That is the full heightmap: within 30 m the sim's to 0.075 mm, the measure's floor, in flight as on the ground. Pixel error 1 left 1.8–16.2 mm, and the friend's errors up to 41.1 mm. No patch, and WLD-11 is met as written.
    - **`TerrainParity`** measures what it says, to a floor of 0.002–0.08 mm. Its "under a pixel" was a band's worst error against a pixel at the band's edge. It now also gives each pixel's shift on the picture: 0.01 px at most under the rule, 0.69 px under pixel error 1, 2.68 px under the friend's errors (a field line is 3.76 px).
    - **Noclip** keeps the friend's errors: from its start the ground round the stand is the sim's to 0.024 mm. The smoke test guards that and the rule's 30 m at 1 mm.
    - **The cost** (`-terrainab`, three interleaved runs, a shared machine in best power efficiency mode): the GPU +0.00 ms of 3.7, 0.5–0.7 M more triangles in 27 M, the main thread +0.16–0.23 ms, the frame +1.3–1.4 ms. Most of that is in the wait to present, a cause not found here: W6's quiet run decides.
  - **Found, not this task's:** the frames take 22–24 ms while the GPU works 3.8 ms, 17–19 ms of each waiting to present (W6). One landing of the hills flight broke three legs, the front left at 0.06 m/s, and `GroundCheck` then reads them as rigid on the body (3.3, the physics engineer).
- [ ] 3.3 [game-developer] The scripted flights at W3's final model, after 2.3, 2.4 and 2.6:
  - re-sweep `LegShots.Cases` and `ACrashIntoTheRangesCarNamesIt` as A4 did at model 79;
  - re-sweep `-smokelegs`, which now flies on the farmland's soil;
  - re-check the smoke test's crash flight and its cuts check, now that 1.2's objects are in.
  Checks: EditMode, the smoke test (default, `-smokelegs`, `-smokehills`).

## 4. Review

- [ ] 4.1 [qa-engineer] Review against the spec; the checks; 0 B and bit-exact replays; a low flight over the hills, the village and a tree belt.
