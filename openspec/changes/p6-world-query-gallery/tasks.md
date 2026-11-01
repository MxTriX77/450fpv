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
- [x] 1.3 [world-builder] The station data (design 3). **Dropped by D-047** (the test range is the final map; the gallery is dropped). The sim loader for `maps/test-range/placements.json` lands with 1.4.
  - **What:** `maps/gallery-g1/stations.json` from `GalleryStations.cs` in `p6-test-range`'s placement form (its design 7, D-044), the scene builder reading it, and the sim loader placing every variant's `.assetmeta.json` at its pose, for the gallery and for `maps/test-range/placements.json` alike.
  - **Verify:** the gallery scene test passes unchanged, and the "sim sees what the scene shows" scenario on both maps (pose parity within 1 mm; the hash moves with a 1 mm pose change).
- [x] 1.4 [world-builder] The queries over the test range (design 4, 5; D-044, D-047: the gallery is dropped):
  - **What:** `SampleGround` with the floor, the patches and holes; `StaticContacts` over primitives, wire capsule chains and runtime objects with the Time < 1 latch on a deterministic 2 m grid; `Raycast`; `Geometry`; the launch stand as a runtime object with reset.
  - **Verify:** the swept-arm, foot-in-the-trench and batched-equals-single scenarios, the golden file, and a concurrency test.
  - **From `p6-flight-core` 1.7:** on the launch stand the step's remaining cost is the stub's `StaticContacts` sweeping the payload and coil capsules beside the posts (about 6 µs each, finding nothing); give the gallery and test range worlds an AABB early reject per shape before the exact test.
  - **As built** (`Runtime/World/{MapWorld,MapWorldLoad}.cs`, new; `ShapeMath.cs`; `FloorWorld.cs` shares the runtime-shape check; the rules in `IWorldQuery.cs` and `WorldTypes.cs`):
    - **The world:** `MapWorld.Load(repositoryRoot, "test-range")` reads `maps/test-range/placements.json` ("fpv450-placements" v1, frame `unity-lh-yup`), each variant's `.assetmeta.json` under `unity/Assets/_Project/Art/Models` once, and `assets-src/physics_materials.json`'s row names. `new MapWorld(placementsJson, readModel, materials)` takes the texts, so a test can edit them. Load takes about 0.9 s cold; it throws `FormatException` naming the file and field.
      - **Ids:** placed object i is instance row i, named by its variant (`PlacedObjects` = 731); its shapes are the metadata's collision shapes in order, then its wires. Runtime objects follow. Materials are the table's rows in file order (1…19); surfaces S01–S13 (`MapWorld.SurfaceIds`, 1…13).
      - **The pose:** position + R(yaw)·local, R = Unity's `Quaternion.Euler(0, yaw, 0)` by `DetMath.SinCos`; a shape's axes are R·rotation (normalised). Nothing is flipped (D-019).
      - **Also public:** `StartPoints` (id, position, heading, legs on, stand), `WirePoints(object, shape)` (the chain, W2), `MapId`, `SurfaceId(name)`, `MaterialNames(json)`.
    - **The ground** (W3, W4 option a): the file's plane (y 0, `belt_straw`), absent over each patch footprint (S04's 46, E01's) and inside each hole band (Z04's shaft, E01's run and niches). `SampleGround` reports `Hole` there; the plane's `StaticContacts` contact is kept only where its point (the lower core end's foot, A on a tie) lies outside them; `Raycast` drops a plane hit inside them and goes on. So S04's soil and straw and E01's fillers are the only ground on them, each once.
    - **Wires** (W1, W2, W6): a capsule chain at half the diameter. A query finds its candidate segments by two binary searches along the chord, the points strictly rising along it (the loader checks). `Distance` is the true signed distance, `WireParam` length along the chain over its length, `Geometry` the chord (centre, axis, `SpanA`/`SpanB`, `Sag`, radius). A ray starting inside any segment skips the wire.
    - **The index:** a 2 m grid over `extent_m` (256 × 256 cells); each shape in every cell its bounds meet, tested once (in its first cell shared with the query), after an AABB check (1.7's early reject); gaps listed the same way. Results stay in canonical order.
    - **The sweep:** generic over the shape's closest approach, so primitives and wires share the Time rule. Task 1.5 added the box clear-bound and the exact box minimiser (below).
    - **Tests** (`Tests/World/MapWorldTests.cs`, 15): the swept arm into `L01_straight` (one timber contact at the analytic first touch, `Time` 0.534); E01's run (`Hole`, then the `belt_bare` floor filler at −1.5 m, no plane); a skid sliding 0.1 mm over the run's floor never latched; S04 (straw and soil each once, no plane); a capsule straddling Z04's band edge; 1000 15 mm spheres at 60 mm a step across a 5 mm wire and 1000 across L03's four conductors, every one caught with `WireParam` within 1e-9 and `Distance` within 1 µm; the true distance deep inside a conductor; a ray re-cast from inside one; the stand at the field start (one contact per post, the same id after a reset); pose parity (731 rows, 43 677 shapes, 1620 wire points: worst 2.9e-14 m against an independent reading); the hash moving with a 1 mm row or shape edit, and CRLF = LF; four threads with one thread's bits; batched equals single. Mutations that drop the gap rule, the wire's inside-ray rule or halve the advancement fail them.
    - **The scene's side** (`unity/Assets/_Project/Tests/EditMode/TestRangeWorldTests.cs`): every collider of every placed prefab in the committed scene lies within 1 mm and 0.01° of the sim's shape, with its size, and no collider is missing or extra (`Fpv450.Tests.EditMode` now references `Fpv450.Sim`).
    - **Golden:** `tools/worldbench/golden/test-range.txt`, 10 000 each of samples, capsules (with a buffer of 64 and of 2, the overflow) and rays over eight spots (V01_L's crown, H01_D, E01, the posadka, an L03 span, Z04, an S04 patch, a fixed stand), the same in Debug and Release, batched equal to single.
    - **Notes:**
      - **The plane's tie:** a level capsule's plane point is A's foot, so across a band's edge the plane answers only if A lies outside. Where a band meets the plane the asset's own shapes carry the inside part (Z04's walls stand 5 mm proud), so nothing falls through; physics' skids rest on their support spheres anyway.
      - **For 1.6b:** on a patch the ground is shapes, not object −1. A rule that skips "the ground" by object id (skids on objects only, the disc gate's "meets anything") now meets S04's and E01's soil; dispatch on `Surface != 0` where the ground is meant.
      - **Not read:** the metadata's `gaps` (H01's door normal lies along its leaf; `GapsNear` is out of this change), `floor_cuts_m` (the scene's floor; the sim's gaps are the patches and bands), `wind_volumes`, `snag`.
- [x] 1.5 [world-builder] Budgets (design 7):
  - **What:** `tools/worldbench --bench` at the three spots, plus the test range's posadka at 2 m (its densest step, D-044), on AC with the power mode printed.
  - **Verify:** the worst-case-step scenario, plus 100 000 capsules and 100 000 rays under 100 ms each.
  - **As built** (`tools/worldbench/TestRange.cs`; `Runtime/World/ShapeMath.cs`; `WorldHash.QueryVersion` 2):
    - **The patterns,** at drone.json's margin (0.1 m), each over 20 000 steps after a warm pass:
      - **physics':** p6-flight-core 1.6's `ContactModel` as it queries: 12 samples; the skids (a capsule and two support spheres each), arms and coil (17); the 4 disc spheres at margin 0, then their chords and ring segments only where they meet something; 8 rotor rays of 5 m.
      - **K§10.5's:** 44 samples, 46 swept (with a 32-segment fiber laid along the last 800 steps of the path, its first segment from the coil), 8 rays.
    - **The paths:** inside V01_L's crown (a Lissajous through its twig shapes' box); round H01_D's walls 0.25 m out of its `bounds.lod`, 0.4–4.5 m up under the eaves; along E01's run at 10 m/s, the skids from the run's floor to 1 m up; the posadka at 2 m (the file's bench leg, 15.75 m/s). An earlier path through the house's walls and through E01's soil was dropped: a drone never flies inside a solid, and it measured only the inside-a-box search.
    - **What it took** (the first run: E01's median 176–205 µs, p99 2.7 ms; H01_D's p99 0.7–0.8 ms):
      - **Clear** (26145af): a box's per-axis bound in its own frame proves when a sweep cannot touch for the rest of the step, so a skid sliding along a face stops creeping (64 Lipschitz steps of 44 evaluations each); before any closest approach it also drops a yawed or tilted box that the AABB check let through. Every result is the same except a sweep that used to give up after 64 steps and count a touch it never made (69 of 200 000 audited capsules, every one such a give-up).
      - **BoxMinimizer** (40bc591): a capsule's closest approach to a box it stays outside, in closed form (≤ 17 cheap candidates, then ≤ 7 quadratic pieces) where the golden-section search took 44 evaluations. Inside a box the search stays: there the deepest point is a tie of two faces, and the exact tie flipped the Time rule's normal test on 7148 of 92 000 deep contacts. Audit, old against new on 120 000 capsules (the stub with three boxes, H01_D, E01, S04): 4763 contacts differ, by at most 3.3e-12 m on the same branch, 3 flip branch (all 34–165 mm deep inside a box), 1 appears that the search placed just past the margin. `QueryVersion` 2 and `golden/floor.txt` re-recorded: only `floor.contacts` moved, with its counts unchanged (1082 capsules in contact, 330 first touches). The 230 sim tests (the flight goldens among them) and `flightbench --probe` pass unchanged.
    - **The bench** on AC (Balanced plan, best power efficiency mode; µs; 0 B allocated everywhere; the mode's clocks move a median by up to ±30 % run to run):

      | step | crown | round H01_D | along E01's run | posadka 2 m |
      |---|---|---|---|---|
      | physics' pattern, median / p99 | 16.7 / 85.4 | 12.8 / 51.2 | 14.5 / 117.5 | 7.0 / 19.1 |
      | K§10.5's batch, median / p99 | 53.9 / 144.4 | 31.9 / 88.2 | 58.1 / 196.1 | 13.4 / 36.6 |

      - **100 000 queries:** physics' swept queries over the four paths 63.8 ms per 100 000 (3.5 million timed), its rotor rays 36.9 ms per 100 000, ground samples 1.7 ms. Shown, not judged: 100 000 capsules at random poses in the golden's eight spot boxes, solids included, 243 ms, and 100 000 rays of ≤ 2 m there 103 ms.
      - **The stub:** a step 9.7 / 21.8, 100 000 capsules 44.0 ms and rays 11.8 ms, as task 1.1.
    - **Open:** K§10.5's batch along E01's run sits at 58 µs median, under the budget by little; its fiber is most of it (its 32 laid segments took 53 of 82 µs before the early reject). Phase 7d's fiber decides how often and how it queries; the p99 of every step (up to 196 µs) is a PLT-04 input for physics' whole-step 300 µs, beside 1.7's stand case.

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios: every `world-query` scenario run by QA, the golden in Debug and Release, pose parity over every station, the budgets on AC, the port's credit lines (D-027: nothing merged from `main`), hygiene
