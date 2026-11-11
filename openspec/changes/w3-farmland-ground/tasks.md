# Tasks: w3-farmland-ground

Order: 1.1, then 2.1; 1.2 once W4a and W4b have merged onto the line; 2.2 alongside 1.2; then 2.3 and 2.4, then 3.1, 4.1. 2.1's farmland case is re-run with the objects after 1.2. Timing verdicts need a quiet machine.

## 1. The heightfield

- [x] 1.1 [world-builder] The world-query core over `maps/farmland/`'s 17 tiles: `SampleGround`, `Raycast` and `StaticContacts` triangulated as `Tile.height` (bit-exact at the 1,000 probes), a lookup per tile, the 12 step lines as walls, the hole mask read, off-world as a hole, surfaces per cell. `QueryVersion` 8, 0 B, worldbench rows, goldens audited case by case. Write `design.md`. Checks: `dotnet test tools/simtests`, worldbench `--golden` and `--bench`, flightbench `--golden`, `precommit.py --tree HEAD`.

- [ ] 1.2 [world-builder] The farmland's objects in the world-query core: `placements.json` rows (full `rotation_xyzw`, per-axis `scale`; a ruin with scale x < 0 uses `Ruin_Walls_mirrored`) with W4a's plant metadata (variant → `VFnn` via `assets-src/vendor_plants.json`; capsule ends scaled by (w, h, w), radius by w) and W4b's ruin and debris metadata; gaps and snag records placed the same way. The object grid on `map_rect_m`. Bit-exact goldens, worldbench rows over the village and a tree belt, 0 B. Checks: as 1.1.
  - Also: `FarmlandTests.Queries_AllocateNothingAfterTheFirst` allocates 24 B in Release only (since 1.1); and worldbench's `farmland.fiber` rows use `QueryFilter.NoGround`, as the fiber does.

## 2. The flight on hills

- [x] 2.1 [physics-engineer] L6, the fiber's query cost: no full `StaticContacts` query per step for a resting segment near shapes, and no terrain cost for the fiber's own queries (its nodes take the ground from `SampleGround`; 1.1's design suggests a `QueryFilter.NoGround`). The three model-79 flights' worst stretches (L1's report) and a 1.5 km line laid over the farmland's terrain replay within PLT-04's step budget in .NET; the same line through a tree belt and the village is re-run after 1.2. Same checks as 1.1.
  - As built (design.md §2.1): `QueryFilter.NoGround`, `IWorldQuery.ShapeContact`, the corridor walk in the object grid, near lists per uncertified segment, a hashed `AddHit`; model 79 and `QueryVersion` 8 unchanged, replays bit for bit, 0 B. The fiber's whole `StaticContacts` per step in flight 35 fell from 266–372 µs to 15–21 µs.
  - **The scenario is not yet shown:** on a loaded machine flights 35 and 38 reach p99 507–592 µs; a quiet run is owed (QA). Flight 37's straw stretches (p99 1.2–1.8 ms) spend their time outside the fiber's path: 2.3.
- [ ] 2.2 [physics-engineer] The legs' and crash contact on slopes of 0–38°, the fiber's ground contact over the heightfield, and the furrow decision with its reason. flightbench rows on slopes; model 82 if the contact model changes, with goldens audited. Same checks as 1.1.
  - Also: the clearance certificate lets a segment's ends move margin − radius before re-querying, looser than `InsertGap` needs, so a shape can come within the line's radius without a contact slot; fix it (model 82, goldens audited). And the drone's own contacts on the terrain within PLT-04: half the farmland line's slow steps come while the drone sits and climbs.

- [ ] 2.3 [physics-engineer] Flight 37's straw stretches within PLT-04: the stems' cache walk, the hub rays through the straw boxes (`SoilUnder`, 60–100 µs) and the crash's own contacts (2.1's design names them). The three model-79 flights' worst stretches replay with p99 ≤ 300 µs and world queries < 60 µs on a quiet machine. Same checks as 1.1.

- [ ] 2.4 [physics-engineer] Contact with the farmland's objects (from W4a's and W4b's reviews):
  - a crash into a 2 cm wall skin peaks at 2,705 m/s² against 22,202 on a solid slab at 8.7 m/s: a wall stops a drone the same however thin its collision is;
  - the per-step contact event buffer overflows (4,205 events lost scraping a window frame);
  - `ContactModel.PairsPerShape` is 8, and 8 plant variants put 7–10 crown pieces within a prop's reach: decide whether compliant twig shells get more pairs, with W4a refitting if the rule changes;
  - a flightbench row flies into a bush (GreenBush, 579 placed);
  - snag checks index segments, not records (the debris rims are 1 cm stairsteps, 10,089 segments in the village).
  Model 82 if contact answers change, goldens audited. Same checks as 1.1.

## 3. Flight on the farmland

- [ ] 3.1 [game-developer] The player flies the farmland from the village start point; the test range leaves the player (headless tests and benches keep it); the drawn terrain near the drone follows the heightfield (the tech artist checks). Checks: EditMode, the smoke test, PlayMode if any, a 2-minute flight's log replays bit for bit.

## 4. Review

- [ ] 4.1 [qa-engineer] Review against the spec; the checks; 0 B and bit-exact replays; a low flight over the hills, the village and a tree belt.
