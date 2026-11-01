# Tasks: p6-world-query-gallery

Branch `unity/world/p6-world-query-gallery` off `unity/main` once `p1-g1-look-lock-gallery` is merged (it moves the gallery's station table). Task 1.1 may start before that, on the sub-branch `--api` off `unity/main`, since it touches only `Runtime/World/` and `tools/worldbench`. Its interface is merged before `p6-flight-core`'s task 1.6.

## 1. Build

- [x] 1.1 [world-builder] The query interface (design 1, 2, 6):
  - **What:** `IWorldQuery`, its result structs, the content hash and `QueryVersion` in `Runtime/World/`, ported from `refs/godot/main` (fetch it read-only: `git fetch origin main:refs/godot/main`; read with `git show`, never merge). A stub world (the floor plane) implementing it.
  - **Verify:** `dotnet test tools/simtests`, and Unity's EditMode run compiles the package.
  - **As built:**
    - **Files:** `Runtime/World/{IWorldQuery,WorldTypes,ShapeMath,WorldHash,FloorWorld}.cs` (namespace `Fpv450.Sim.World`), `Runtime/Math/Double3.cs`, and `DetMath.Abs/Min/Max`. Each ported file names its source in `refs/godot/main`.
    - **The calls** (`IWorldQuery`):
      - `ulong ContentHash`, `int QueryVersion` (= `WorldHash.QueryVersion` = 1)
      - `void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results)`
      - `int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results)`: one call per capsule, the true count returned, canonical order (object, shape), the ground first
      - `void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits)`
      - `bool Geometry(int obj, int shape, out ShapeGeometry geometry)`
      - `int AddObject(string name, ReadOnlySpan<ShapeGeometry> shapes)`, `void ResetRuntimeObjects()`
      - `string ObjectName(int)`, `string MaterialName(ushort)`, `ushort MaterialId(string)`, `string SurfaceName(byte)`
    - **The results** (all doubles, `Double3` vectors):
      - `GroundSample`: `Height` (−∞ in a hole), `Normal`, `Surface`, `Flags` (`Hole`)
      - `StaticContact`: `Point`, `Normal`, `Distance`, `Object`, `Shape`, `Material`, `Surface`, `WireParam` (−1 off wires), `Time`
      - `RayHit`: `Distance` (+∞ on a miss), `Point`, `Normal`, `Object`, `Shape`, `Material`, `Surface`
      - `ShapeGeometry`: `Kind` (Plane, Box, Sphere, Capsule, Cylinder, Wire), `Center`, `AxisX/Y/Z`, `HalfExtents`, `Radius`, `HalfLength`, `Material`, `Surface`, `SpanA/B`, `Sag`
      - `WorldIds`: `Ground` = −1 (also a miss's object, with `Surface` 0), `NoMaterial` = 0, `NoSurface` = 0
    - **Decisions:**
      - **The Time rule:** every first touch during a step reports `Time` < 1 at the touch (K§7.5's reading). The first attempt reported a same-side entry as `Time` = 1. Touching since the step began gives `Time` = 1, or 0 when it went through.
      - **The ground is in `StaticContacts`:** as object −1, shape 0, a half-space with its surface, absent over a hole. Feet may skip it and read `SampleGround`.
      - **`AddObject` takes world-space shapes,** not a catalog id. Each shape has exactly one of a material and a soil surface.
      - **The frame:** the ported math has no handedness, so it is unchanged. The Z flip is where data enters: rows and yaw (`IWorldQuery.cs`, Frame).
      - **Ids:** each world holds its material and surface tables. The stub's floor surface is 1, and its materials are 1…n in constructor order.
    - **Numbers:**
      - 18 tests pass in `tools/simtests`. Unity's EditMode run compiles the package and passes 124 of 124 tests, the same 18 among them.
      - `--golden`: 5 cases pass in Release and Debug. 10 000 each of samples, capsules (1 082 in contact, 330 with `Time` < 1) and rays (3 297 on the floor, 361 on the probe shapes). Batched equals one by one.
      - `--bench` on the stub (AC, Balanced plan, best power efficiency mode): a step's batch (44 + 46 + 8) has a median of 9.70 µs and a p99 of 32.50 µs. 100 000 capsules take 53.75 ms and 100 000 rays 13.18 ms, with 0 B allocated.
- [x] 1.2 [physics-engineer] Sign off the interface by reading, with a contact probe (a fake drone over the API: four feet, four arms, a coil, four prop discs) in `tools/flightbench`.
  - **Verify:** the probe passes on the stub, and the sign-off goes into `review-physics.md` in this change, with the PHB-05 lessons checked.
  - **As built:**
    - **The probe:** `tools/flightbench/ContactProbeCase.cs`, run with `--probe`, uses the public calls only.
      - The drone is `drone.json` at L4, legs on (4.832 kg) and legs off (4.716 kg).
      - Its parts: four skid segments (a capsule, plus spheres at the overhang end and the T-joint), four arms, the coil, and four prop discs (a bounding sphere, then a 12-capsule rim ring).
      - It drives the drone resting, sliding, dropping, landing on a 9 mm rod, on the stand, through a board and a tube at 10–30 m/s, and with its discs against a wire.
      - Each of `--break latch | margin | refresh | spheres | disc` fails it.
      - `--bench`: p50 3.90 µs clear and 10.50 µs with two rings, 0 B.
    - **Verdict:** signed off for `p6-flight-core` 1.6, with no call or field changed.
      - Findings W1–W8 go to the world-builder. The musts for 1.4 are wires (W1), the ground under a capsule straddling a hole edge (W3), and patch ground answered once (W4).
      - The probe corrects api-review §4's refresh rule (P1, 1.6 and K§7.5): refresh only while the part's core is outside the shape. Without that, a tube was passed on 32 of 48 runs, with no tunnel event.
- [ ] 1.3 [world-builder] The station data (design 3):
  - **What:** `maps/gallery-g1/stations.json` from `GalleryStations.cs` in `p6-test-range`'s placement form (its design 7, D-044), the scene builder reading it, and the sim loader placing every variant's `.assetmeta.json` at its pose, for the gallery and for `maps/test-range/placements.json` alike.
  - **Verify:** the gallery scene test passes unchanged, and the "sim sees what the scene shows" scenario on both maps (pose parity within 1 mm; the hash moves with a 1 mm pose change).
- [ ] 1.4 [world-builder] The queries over the gallery and the test range (design 4, 5; D-044):
  - **What:** `SampleGround` with the floor, the patches and holes; `StaticContacts` over primitives, wire capsule chains and runtime objects with the Time < 1 latch on a deterministic 2 m grid; `Raycast`; `Geometry`; the launch stand as a runtime object with reset.
  - **Verify:** the swept-arm, foot-in-the-trench and batched-equals-single scenarios, the golden file, and a concurrency test.
  - **From `p6-flight-core` 1.7:** on the launch stand the step's remaining cost is the stub's `StaticContacts` sweeping the payload and coil capsules beside the posts (about 6 µs each, finding nothing); give the gallery and test range worlds an AABB early reject per shape before the exact test.
- [ ] 1.5 [world-builder] Budgets (design 7):
  - **What:** `tools/worldbench --bench` at the three spots, plus the test range's posadka at 2 m (its densest step, D-044), on AC with the power mode printed.
  - **Verify:** the worst-case-step scenario, plus 100 000 capsules and 100 000 rays under 100 ms each.

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios: every `world-query` scenario run by QA, the golden in Debug and Release, pose parity over every station, the budgets on AC, the port's credit lines (D-027: nothing merged from `main`), hygiene
