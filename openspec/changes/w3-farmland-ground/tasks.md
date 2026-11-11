# Tasks: w3-farmland-ground

Order: 1.1, then 2.1, then 2.2, then 3.1, then 4.1. Branch `unity/physics/physics-integration-2--w3-ground`, cut from W2's branch (it needs `maps/farmland/`); it merges after W2. Numbers from the orchestrator: `QueryVersion` 8; model 82 only if the contact model changes.

## 1. The heightfield

- [ ] 1.1 [world-builder] The world-query core over `maps/farmland/`'s 17 tiles: `SampleGround`, `Raycast` and `StaticContacts` triangulated as `Tile.height` (bit-exact at the 1,000 probes), a lookup per tile, the 12 step lines as walls, the hole mask read, off-world as a hole, surfaces per cell. `QueryVersion` 8, 0 B, worldbench rows, goldens audited case by case. Write `design.md`. Checks: `dotnet test tools/simtests`, worldbench `--golden` and `--bench`, flightbench `--golden`, `precommit.py --tree HEAD`.

## 2. The flight on hills

- [ ] 2.1 [physics-engineer] L6, the fiber's query cost: no full `StaticContacts` query per step for a resting segment near shapes. The three model-79 flights' worst stretches (L1's report) and a farmland case (a laid line through a tree belt and the village) replay within PLT-04's step budget in .NET. Same checks as 1.1.
- [ ] 2.2 [physics-engineer] The legs' and crash contact on slopes of 0–38°, the fiber's ground contact over the heightfield, and the furrow decision with its reason. flightbench rows on slopes; model 82 if the contact model changes, with goldens audited. Same checks as 1.1.

## 3. Flight on the farmland

- [ ] 3.1 [game-developer] The player flies the farmland from the village start point; the test range leaves the player (headless tests and benches keep it); the drawn terrain near the drone follows the heightfield (the tech artist checks). Checks: EditMode, the smoke test, PlayMode if any, a 2-minute flight's log replays bit for bit.

## 4. Review

- [ ] 4.1 [qa-engineer] Review against the spec; the checks; 0 B and bit-exact replays; a low flight over the hills, the village and a tree belt.
