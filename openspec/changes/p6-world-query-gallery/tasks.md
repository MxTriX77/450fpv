# Tasks: p6-world-query-gallery

Branch `unity/world/p6-world-query-gallery` off `unity/main` once `p1-g1-look-lock-gallery` is merged (it moves the gallery's station table). Task 1.1 may start before that, on the sub-branch `--api` off `unity/main`, since it touches only `Runtime/World/` and `tools/worldbench`. Its interface is merged before `p6-flight-core`'s task 1.6.

## 1. Build

- [ ] 1.1 [world-builder] The query interface (design 1, 2, 6):
  - **What:** `IWorldQuery`, its result structs, the content hash and `QueryVersion` in `Runtime/World/`, ported from `refs/godot/main` (fetch it read-only: `git fetch origin main:refs/godot/main`; read with `git show`, never merge). A stub world (the floor plane) implementing it.
  - **Verify:** `dotnet test tools/simtests`, and Unity's EditMode run compiles the package.
- [ ] 1.2 [physics-engineer] Sign off the interface by reading, with a contact probe (a fake drone over the API: four feet, four arms, a coil, four prop discs) in `tools/flightbench`.
  - **Verify:** the probe passes on the stub, and the sign-off goes into `review-physics.md` in this change, with the PHB-05 lessons checked.
- [ ] 1.3 [world-builder] The station data (design 3):
  - **What:** `maps/gallery-g1/stations.json` from `GalleryStations.cs` in `p6-test-range`'s placement form (its design 7, D-044), the scene builder reading it, and the sim loader placing every variant's `.assetmeta.json` at its pose, for the gallery and for `maps/test-range/placements.json` alike.
  - **Verify:** the gallery scene test passes unchanged, and the "sim sees what the scene shows" scenario on both maps (pose parity within 1 mm; the hash moves with a 1 mm pose change).
- [ ] 1.4 [world-builder] The queries over the gallery and the test range (design 4, 5; D-044):
  - **What:** `SampleGround` with the floor, the patches and holes; `StaticContacts` over primitives, wire capsule chains and runtime objects with the Time < 1 latch on a deterministic 2 m grid; `Raycast`; `Geometry`; the launch stand as a runtime object with reset.
  - **Verify:** the swept-arm, foot-in-the-trench and batched-equals-single scenarios, the golden file, and a concurrency test.
- [ ] 1.5 [world-builder] Budgets (design 7):
  - **What:** `tools/worldbench --bench` at the three spots, plus the test range's posadka at 2 m (its densest step, D-044), on AC with the power mode printed.
  - **Verify:** the worst-case-step scenario, plus 100 000 capsules and 100 000 rays under 100 ms each.

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios: every `world-query` scenario run by QA, the golden in Debug and Release, pose parity over every station, the budgets on AC, the port's credit lines (D-027: nothing merged from `main`), hygiene
