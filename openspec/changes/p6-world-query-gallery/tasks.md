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

- [x] 1.6 [world-builder] QA's review (`review.md`: B2, B3, S1, L2, L3) and the dense objects' speed (D-054):
  - **What:** merge `unity/main` and re-pin the content line (B2); no false first touch (B3); tests for review-physics W5 and the ray's gap rule (S1); parity tests that read what they say (L2); the CPU clock in the bench (L3); a broad phase inside each dense object, aimed at PLT-04's 60 µs line, and the budget scenario restated per D-054.
  - **As built:**
    - **B2:** `unity/main` at `7d72db8` merged with `-m`, no conflicts (`48ab26e`). `test-range/content_hash` moves from `d6de238216ceb4b9` to `6114242e06475fb5` by `assets-src/physics_materials.json`'s new `carbon_composite` row alone (`ace2e02`): the material names are hashed (design 6), the row is last, so no material id moved, and every query hash of both goldens stayed at query version 2.
    - **Narrow phase, the same bits** (`f8e67f2`): a box the whole sweep is clear of (`Clear`) skips the start pose's closest approach; the search inside a box or cylinder evaluates the distance alone (`Sdf`, LocalSdf's value to the bit); a segment whose ends both lie beyond one face's plane skips the minimiser's entry test. Both goldens and physics' recorded straw and stand query streams give the same bits.
    - **B3, no false first touch** (`144e717`, `QueryVersion` 3, `IWorldQuery`'s Time rule restated):
      - **The bound** (`ShapeMath.Bound`): any unit n holds a convex shape below its support max x·n, so a core point is at least x·n − support from it; the core's least is an end's, and each end's is linear in t. Along a closest approach's normal it equals the distance there. Supports: plane (its normal only), sphere, capsule, box, cylinder; a wire's chain bounds each candidate segment along its own normal (the chain is not convex).
      - **Its uses:** before the start pose's closest approach (along the current pose's normal) and at every step it proves a sweep clear for the rest of the step, so a slide past or along any kind of shape ends at once; on the plane its step (to where the bound reaches 0) is the exact first touch; after 64 distance / speed steps (the steps 1.4 took, so every sweep they settle keeps its bits) the sweep steps by the later of the two, a Newton step on the distance; a sweep unsettled after 128 steps counts no first touch (the current contact, if within the margin).
      - **Why 64 first:** with the bound's steps from the start for every kind, the trunk's flight goldens held but `p6-flight-core--range`'s range golden moved (`0xB604F94528D16457` for `0x292C0858705902B6`, the same flight to every printed digit: sunk 21.98 mm, 5 impacts, 17 pairs). With 64 first it keeps its bits (below).
      - **The golden re-record, case by case** (an old/new dump of every contact; a 200 000-sample scan of each sweep's distance by `ShapeMath.Closest` as the truth):

        | | floor (1113 contacts) | test range (8099) |
        |---|---|---|
        | unchanged | 890 | 7832 |
        | first touch on the plane, moved | 222: 220 stopped within 1 µm before, 2 give-ups at up to 0.139 mm; now Distance ≤ 3.5e-17 m | 226: 220 and 6 give-ups; now ≤ 4.9e-17 m |
        | give-up on a shape, now a first touch | 0 | 35, now Distance ≤ 0.86 µm |
        | give-up on a shape, now Time 1 or gone | 1 (case 1381, the probe's yawed box at 2.67 mm): never touched, least 0.58 mm | 4 (two Z04 walls, an S04 strip, an E01 filler): never touched, least 1.06 µm to 0.96 mm |

        Every moved first touch is a touch by the scan and never after its first (worst 12 µm of travel before it: the 1 µm stop on a slant); the old ones lay up to 15 mm of travel early. Counts: the floor's first touches 330 → 329; the test range's Time < 1 contacts 4140 → 4136, capsules in contact 3965 → 3964. Both golden files re-recorded at query version 3 (`144e717`).
      - **Physics' goldens:** simtests 287 of 287 (the trunk's flight goldens and audits unchanged). On `p6-flight-core--range` (`8a5389a`) with this branch's `Runtime/World`: its Range and GoldenFlight tests 37 of 37, the range golden `0x292C0858705902B6` unchanged.
    - **Tests** (`ad40aa1`; `FloorWorldTests`, `MapWorldTests`), each failing its mutation:
      - B3: a skid 0.1 mm over the floor at 10, 20, 30 mm a step (Time 1, Distance 0.1 mm); a sphere sinking 50 µm while crossing 20 mm of floor (Time 0.8 and Distance 0 within 1e-12); a sphere sliding 0.2 mm past a trunk cylinder (Time 1); 500 spheres grazing the 5 mm wire and 400 the L03 conductors at 0.990–0.998 of R + r (each once, not after the analytic first touch, Distance within 1 µm). Every wire crossing now also checks its Time against the analytic touch and its Distance within 1 µm (was 10 µm).
      - S1 (W5): a level skid 1 mm over a box's top face, axis-aligned and yawed 3-4-5, either way round: A's foot when A lies over the face; with A over the face and B past its edge (an exact tie at the edge), A's foot; from B's side, the edge. The ray rule: a ray down in E01's run meets its floor filler at −1.5 m, over S04 its straw strip at the plane's height (a tie the plane would win), on the open field the plane.
      - Mutations: the flat piece at its end (`u1`) and a tie keeping the last piece (`f <= best`): both W5 tests fail; the ray keeping the plane in gaps (QA's M6): the ray test fails; no bound with a give-up counted a touch: the floor graze, the trunk and the wire grazes fail; no bound with a give-up counted none: the floor graze fails.
    - **L2:** the sim's pose parity checks all three axes (`ad40aa1`, worst 1.6e-15); the scene's (`2e7bb9c`) reads each collider as PhysX places it: its centre through its transform, its scale (a sphere's largest, a capsule's across and along its `direction`'s axis), a capsule's axis by its `direction`, a cylinder mesh's bounds (radius on x and z, half height on y).
    - **L3 and D-054** (`57ff178`): `worldbench --bench` prints the CPU clock beside the power source, plan and mode; it judges physics' pattern inside PLT-04's 300 µs step at p99 with 0 B, prints the 60 µs target beside each row, and shows K§10.5's batch with its fiber (judged at 7d). Two rows like 1.6b's: resting in S04's straw (the skids 13 mm in) and lifting off the stand. The spec's budget requirement and scenario are restated so.
    - **The broad phase inside each dense object** (`9e3a503`): an object of more than 256 shapes (H01_D 4047, H01_I 1207, E01 831, V01_L 511, V01_M 386) holds a tree over its shapes' bounds (leaves of 4, halved along the axis whose halves' bounds have the least surface, ties by index) and the grid lists it by its bounds; every other shape stays in the grid itself. A query tests exactly the shapes whose bounds meet its own either way, so every result keeps its bits (both goldens, physics' streams). S04 stays in the grid: its 195 long, crossing strips gave a tree of 24 node visits and 9 shapes a query where the grid walks 20 shapes, 20 % slower on physics' straw stream. Runtime objects (the stand) are checked by their bounds first.
    - **What the time was:** on physics' recorded straw rest a step made 214 closest approaches (22 of them searches inside a strip) and walked 860 grid entries, so the narrow phase was most of it (the walk alone 10–17 µs of 45–80 on a loaded run). On the flight paths the walk dominated (2491 entries a step in V01_L's crown for 18 shapes in reach, 2070 round H01_D for 2) and the creep (43.5 advancement steps a step in the crown); the bound took the creep to 2.5, the trees the walk.
    - **The bench** (µs a step, 0 B everywhere; AC, Balanced plan, CPU 3201/3201 MHz; the mode is printed with each run, balanced for all of these). This machine carried other agents' load (Windows read 13–37 % CPU), so physics' numbers are taken interleaved, old against new:
      - **Physics' own query timer** (`p6-flight-core--range` at `8a5389a` with its own `Runtime/World` against this branch's, `TimedQuery` as its `--bench` reads it, old/new/old/new):

        | physics' world queries | before (`97f353f`) | after |
        |---|---|---|
        | resting in S04's straw, p99 of 3 × 20 000 steps, twice | 67.6, 67.9, 74.8; 68.3, 71.8, 138.3 | 49.3, 44.7, 88.4; 40.1, 42.6, 84.9 |
        | the straw: share of the step | 69–73 % | 63–67 % |
        | the field stand's launch, p99 of 12 × 5016 steps, twice | 138.7, 60.9; 76.7, 33.1 | 52.2, 35.7; 64.9, 20.1 |
        | the stand: share of the step | 53–57 % | 41–45 % |

        Its whole `--bench`, run four times in turn (before, after, before, after), drifted with the load (the straw step's p50 56.7, 84.2, 94.3, 132.9 in run order), so it is no comparison; every row passed (p99 ≤ 300 µs, 0 B).
      - **Physics' recorded queries replayed** (its straw rest's 5000 steps and the stand's launch's 5016, on one core at high priority, each step's best of 7; the same result bits before and after): the straw p50 32.5 → 23.2, p99 33.5 → 23.9; the stand p50 3.7 → 2.7, p99 21.0 → 11.3.
      - **`worldbench --bench`**, physics' pattern (median / p99), QA's three runs → now:

        | | crown | round H01_D | E01's run | posadka | S04's straw | the stand |
        |---|---|---|---|---|---|---|
        | QA (1.5's code) | 16.2–22.0 / 81–89 | | 17.9–20.7 / 124–149 | | | |
        | now | 6.2 / 31.1 | 3.6 / 15.2 | 11.0 / 53.2 | 4.7 / 12.1 | 7.7 / 12.5 | 2.0 / 9.3 |

        Every row under the 60 µs target at p99. K§10.5's batch (shown): E01's run 35.5 median (QA: 67.7–72.4), p99 85.9; the rest at most 14.0 / 34.5. 100 000 capsules of physics' pattern 24.7 ms, its rays 14.5 ms; at random poses in the spot boxes 90.6 ms and 57.9 ms (QA: 243–270 and 103–136); samples 0.99 ms.
    - **Seams (physics' note):** physics marks a sphere's contact on a filler's edge flush with a deeper one's face (`ContactModel.Seam`). The query could mark them in part: the loader can find the coplanar, flush faces of an object's boxes (E01's fillers, S04's strips on its soil) and give a contact on such an internal edge its face's normal (the internal-edge correction of Bullet and PhysX), which removes the 10° edge normal. The second contact, on the next piece, stays: the API answers once per (object, shape), so merging pieces needs a field or a merged shape, a change to `IWorldQuery` that physics signs off. Not built here.
    - **Open:**
      - The search inside a box (44 evaluations) is the straw rest's largest remaining cost; making it exact moves every deep contact's bits (1.5 found it flipped the Time rule's normal test on 7148 of 92 000), so it waits for physics.
      - QA's L1 (a ray lying exactly on a seam line falls through) is not changed.

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios: every `world-query` scenario run by QA, the golden in Debug and Release, pose parity over every station, the budgets on AC, the port's credit lines (D-027: nothing merged from `main`), hygiene
