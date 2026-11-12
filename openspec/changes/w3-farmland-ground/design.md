# Design: w3-farmland-ground, tasks 1.1 (the heightfield), 2.1 (the fiber's query cost) and 2.2 (the flight on hills)

**Owner of this note:** world-builder · **For:** 2.1 and 2.2 (physics-engineer), 3.1 (game-developer), 4.1 (qa-engineer) · **Requirements:** WLD-02, WLD-11, WLD-16, PHY-01, PLT-04 · **Decisions:** D-088, D-091, D-092, D-093 · **QueryVersion:** 8

Numbers are labelled: **measured** (run here, the command named), **derived** (computed from measured or published values), **assumed** (chosen, with the reason).

## Context
- **The package** is W2's, as the physics engineer signed it off (`openspec/changes/w2-farmland-export/design.md`): 17 tiles of Unity heightmaps, a ground class per alphamap sample, no hole mask yet.
- **The world-query core before this task** knew one ground: a plane, with patch footprints and hole bands cut out of it (`MapWorld`, the test range). The farmland needs the heights, the step lines and the classes, under the same interface (`IWorldQuery`) and the same Time rule.
- **What this task leaves out:** the farmland's objects (W4a, W4b), the trench holes (W5), the fiber's query cost (2.1) and the contact model on slopes (2.2).

## Decisions

### 1. Where it lives
- **`Terrain`** (`Runtime/World/Terrain.cs`, internal): the heightfield, its lookup, its step walls and its three queries.
- **`MapWorld` holds it in the plane's place.** `MapWorld.Load(root, id)` reads `maps/<id>/map.json` when the folder has one, and the placement file otherwise (the test range, unchanged).
- **No objects yet.** The farmland's own `placements.json` is the package's (trees, ruins and debris with no physics data; W4 gives them shapes). So the loader builds the world from an in-memory placement record of no objects, whose extent is `map_rect_m`. Every object path (the 2 m grid, the region caches, wind, micro-detail, wires, snags) runs the test range's code unchanged, on an empty map.
- **The object grid stays on the map's 1000 m** (W2's §3: a 2 m grid over 9 km would be 20 M cells, 81 MB, derived there).

### 2. The heights: a copy of `Tile.height`
- **The operation order is validate.py's,** line for line: the clamped fraction (Python's `max` and `min` written as the same comparisons), the truncated cell held below n, u and v, the three samples in Unity's indices, the two products and two sums, then `y0 + h / 32766 · sy`.
- **Where tiles overlap,** the highest tile wins, the first in manifest order on a tie (Python's `max`).
- **The facet's normal** is the W2 sign-off's: normalize(−∂y/∂x, 1, −∂y/∂z), one √, constant on each facet.
- **The check:** `tools/map/probe_golden.py` writes validate.py's own heights at the package's 1,000 probes as IEEE bits (`tools/map/golden/farmland-probes.txt`). The pytest `tools/map/tests/test_probe_golden.py` keeps that file equal to the reference, and the C# side must match it bit for bit.
  - **Measured:** 1,000 of 1,000 equal in Debug (`dotnet test`, `FarmlandTests`) and in Release (`worldbench --golden`, `farmland.probes`).

### 3. The lookup
- **Each tile is its own grid:** a point's cell is two divisions and a truncation, as `Tile.height` does it.
- **Finding the tiles:** a coarse lookup over the tiles' union, its cells the smallest tile's side (1000 m: 9 × 9 cells on the farmland). Each cell lists, in manifest order, the tiles that hold any point mapping to it: half-open, the last row and column closed. A map cell lists 4 tiles: the map and the three surround tiles touching its south and west edges. A contact or ray whose reach lies in one lookup cell visits that cell's tiles only; any other visits all 17, in manifest order either way.
- **Off the world** (outside [−4000, 5000]² on the farmland, NaN included) there is no ground. SampleGround reports `Hole` with height −∞, a +Y normal and no surface. StaticContacts and Raycast find nothing there.

### 4. Holes
- **The mask is read when a tile lists one** (`holes: {file, resolution: n}`), uint8 255 or 0, row 0 northmost. None exists yet. The loader rejects any other value and a wrong size.
- **A hole cell removes that tile's column over the cell,** so the ground there is the next tile's, if any. It is the cell `Tile.height` would use (W2's §4).
- **SampleGround** reports `Hole` over it.
- **StaticContacts and Raycast** skip the cell's facets. They keep a contact or hit only where its point has ground (the cell rule, as MapWorld's patches and bands do per point).
- **No hole walls.** The hole's sides are the asset's shapes (WLD-16), so a hole has no terrain wall. The world's outer edge has none either.
- **Tested** on a synthetic package (`TerrainTests`): a hole cell samples `Hole`, a ray falls through it, and a sphere over it has no contact.

### 5. Surfaces
- **The class of a point** is its highest tile's nearest class sample. A sample sits where the README places it (x0 + c · sx / (res − 1), row 0 north), and a point rounds half up to it. That is the texel URP's terrain shader centres there.
- **Read as the files hold them.** W2's re-export (its task 1.3) only changes which class some track cells carry; nothing here depends on it.
- **Tested** against the class files read independently: 900 random points over the map, a surround tile and a far tile.

### 6. Step lines: derived at load, faces in the ground
- **The rule** (W2's §2): the ground is the union of the tiles' solid columns, so where the highest tile changes across a tile edge and the heights differ, the step is a vertical wall. The face looks out of the higher side, and SampleGround reports the top.
- **The derivation:**
  1. The tiles' edges cut the plane into regions (8 × 8 on the farmland).
  2. Across each edge between two regions that both have ground, both sides' heights are taken at every breakpoint of either side's tiles: their grid lines, and where their cells' diagonals cross the edge. Between breakpoints both sides are linear.
  3. A quad goes wherever the sides differ, split where the higher side changes.
  4. The loader refuses a package whose overlapping tiles' surfaces cross inside a region, because a side's winner must not change between breakpoints.
- **Measured** (the load; `worldbench --golden` prints them): 20,831 quads on 4 lines of 9 km. They fall into **12 steps**: the ledge's 4 edges and the 8 far seams.

| Step | Where | Face height |
|---|---|---|
| The ledge, west | x = −1000, z −1000 to 2000 | 1.4039–1.5900 m |
| The ledge, east | x = 2000, z −1000 to 2000 | 1.4077–1.5944 m |
| The ledge, south | z = −1000, x −1000 to 2000 | 1.3985–1.6099 m |
| The ledge, north | z = 2000, x −1000 to 2000 | 1.4140–1.5862 m |
| Far seams (8) | the rest of those 4 lines | up to 87.4, 69.9, 87.9, 74.6, 74.0, 98.9, 85.4 and 87.3 mm |

- **They agree with W2's measurements:** the ledge 1.399–1.610 m, the seams 70–99 mm.
  - W2's list of each seam's maximum (81, 73, 79, 99, 80, 82, 70 and 73 mm) differs one by one from these.
  - I re-sampled each seam with validate.py every 0.01 m: 87.2, 69.9, 87.8, 74.6, 74.0, 98.9, 85.3 and 87.2 mm (measured). That is within 0.2 mm of the derived maxima, which lie between those samples.
  - So these are the walls as built. W2's per-seam list was coarser.
- **Tested:** the step through SampleGround at both sides of x = 2000. A ray at each of the 4 ledge edges hits the face at 0.5 m with the face's horizontal normal. A sphere swept across the face latches its first touch on it, Time < 1, never a jump. A synthetic 3 m step checks the same analytically, plus the depth behind a face (below).

### 7. StaticContacts: the ground's one contact
- **One shape.** The terrain is the ground, object `WorldIds.Ground`, shape 0, its walls included: at most one contact per (object, shape), as before. The contact's `Surface` is the class at its point, and its `Material` is none.
- **The pieces:** every facet, and every wall quad's two triangles, whose bounds meet the query's reach. On a long reach (over 16 cells of a tile, assumed: the point where the extra test pays) the cells are filtered further by the sweep's footprint: the hull of its four core ends in x–z, grown by the radius, the margin and 1 µm (assumed, so rounding never drops a piece the reach holds), row by row. A long segment then costs its length, not its bounding box's area.
- **Outside the ground:** the exact Euclidean distance to the nearest piece (`ShapeMath.SegmentTriangle`, Ericson 5.1.5 and 5.1.9).
  - A buried facet (a far tile under the surround) is never nearer than the ground above it: a segment to it crosses the boundary first.
  - **The exact shortcuts:** where both core ends lie above a facet's plane with the nearer end's foot on the facet, the answer is that foot. A piece whose bounds lie as far as the best so far is skipped (the first on a tie stays).
- **Inside the ground:** a core that meets a piece, or has an end below the ground, is inside.
  - **Its depth** is the deepest of its ends and of its crossings of each facet's footprint. Each point is measured under the ground's own facet there (`Top`, SampleGround's rule), to that facet's plane: the supports' measure (K§7.1), with the facet's normal.
  - **A face behind the point:** where a wall's face lies behind it and nearer, the depth is to that face, with its horizontal normal. A core 2 cm behind a 3 m face is 2 cm deep, not under the top (tested).
  - **The approximations:** at a facet edge `Top`'s facet counts, and the max–min over a face and a facet is taken at those points only. Under a higher tile's facets beyond the reach, more than the pad deep, only the facets in reach give crossings. Each is named here, and none is reached in flight short of a crash into the ground or through the ledge.
  - **Why `Top` and not the visited facet's own plane:** a faster version took each crossing's depth from the facet being visited. It lost contacts. A core straddling the ledge's face with the far facets in reach had the surround's top 0.9 m above it, beyond the reach, so only the far facets were measured and the depth came out negative. A regression test pins it (`TerrainTests`, the straddle case).
- **Every shortcut was checked against the work it skips** (a differential run over the golden's 10,000 capsules, each shortcut switched off in turn; temporary switches, not committed):
  - The vertical reject, the clearance, the Lipschitz bound, the bounds prune, the not-below shortcut, the tile lookup and the corridor change no result, to the bit.
  - The facet's foot shortcut moves 1,204 results by rounding alone, at most 2e-11.
  - The same run caught the inside-depth error above. It showed as "the Lipschitz bound loses 4 contacts", because the bad depth broke the distance's continuity.
- **The Time rule:** `ShapeMath.Sweep` runs over the pieces, as it does over a wire's chain (`ShapeMath.Sweep` itself is unchanged). Each piece bounds the sweep along its own nearest direction (`ShapeMath.Bound` with the triangle's support). Two exact proofs come first:
  - **The clearance** (`TerrainSweep.Clearance`): every piece within the pad lies under the footprint's highest cell corner, so the core's lowest end above it, less the radius, is a lower bound of its distance. A part higher above the ground than its margin costs one pass over its cells and no closest approach.
  - **The Lipschitz bound** (`TerrainSweep.Bound`): the distance moves at most the core ends' larger move per unit of t. So a closest approach farther than that move over the rest of the step, plus `TouchDistance` and its slack, proves the sweep apart. That is the advancement's own conclusion, reached without the pieces' passes. A part skimming within the margin costs one closest approach, on slopes too, where the vertical clearance is weak.
  - A first touch is found and latched as on any shape. Tested: a fall onto the village's steepest facet latches at 0.75 within 1e-5, and a sweep across the ledge's face at (0.1 − r) / 0.2.
- **`Geometry(WorldIds.Ground, 0)` is false on a terrain.** It has no one plane. Physics reads a contact's point and normal, or SampleGround's height and normal (below, what 2.2 must know).

### 8. Raycast
- **Per tile,** a walk through the cells the ray crosses. In each cell the ray is split where it crosses the diagonal, and it enters a facet from above where its height over the facet's plane goes from ≥ 0 to ≤ 0, strictly falling. A tile's walk stops at its first entry, or where the ray is already under that tile.
- **Walls:** a ray enters a face from the lower side, between its bottom and top edge at the crossing.
- **The first entry over all tiles and walls wins,** since a ray that starts outside the ground enters the union at the first of its pieces.
- **A ray that starts inside the ground does not hit it** (the plane's "from above only").
- **Tested:** 500 rays down over the whole world equal SampleGround's height within 1e-9 m, with its normal and class to the bit. Rays from inside and rays up miss, and the 4 ledge faces are hit.

### 9. Identity and versions
- **The content hash** continues MapWorld's FNV-1a:
  1. the placement record (the in-memory one of no objects, while the farmland has none)
  2. `map.json` (CR LF read as LF)
  3. the material names
- **The files behind it:** `map.json` lists every file's SHA-256, and the loader checks each one it reads against it, so the hash covers the heights, classes and holes. An LFS pointer fails the check with "run git lfs checkout" (tested on a synthetic package).
- **The farmland's hash** is `0x8d5991fb58de531a` (measured).
- **`QueryVersion` 8:** the terrain is new math. On a plane every query is unchanged (below, the audit).

## Data structures and memory (derived from the file sizes)

| What | Holds | Bytes |
|---|---|---|
| Heights | 9 tiles of 1025² and 8 of 257² samples, uint16, the file's order | 19,968,034 |
| Classes | 2048², 8 × 512², 8 × 256² bytes | 6,815,744 |
| Wall quads | 20,831 of 64 B (7 doubles and a tile index, padded) | 1,333,184 |
| The lookup | 81 cells, their tile lists | under 1 KB |
| **In all** | | **28.4 MB (27.1 MiB)** |

- **Load:** 235–378 ms (measured, Release, `worldbench --bench`'s `farmland.load` over three runs, the slowest on the busiest machine). That covers reading the 34 binaries, their SHA-256 and the step walls.
- **A query allocates nothing.** Every visitor is a struct, every result goes into the caller's span, and a sweep's state is a readonly struct on the stack. Tested: 100 steps of 44 samples, 8 rays and 46 swept capsules allocate 0 B, and the bench rows below do too.
- **Reentrant:** `Terrain` is read-only after load. Tested: 4 threads × 3 rounds of 3,000 seeded samples, rays and sweeps give the bits of one thread.

## Costs (worldbench --bench)

**The run** (measured, `dotnet run -c Release --project tools/worldbench -- --bench`, 2026-11-11 15:38):
- **The power:** "AC, Balanced plan, best power efficiency mode, CPU 3201/3201 MHz" (PLT-07's line).
- **The load:** the machine was shared. Other sessions' jobs held it at 90 % CPU when the run began: eight Python processes and Defender (measured, `Win32_PerfFormattedData`). The test range's rows in the same run are the reference.
- **The judgement** is D-054's, as for the test range: physics' pattern inside PLT-04's 300 µs step at p99 with 0 B, with PLT-04's 60 µs line for the world queries printed as the target (D-061). K§10.5's batch with its fiber is shown.

**Physics' pattern** (the queries `ContactModel` makes each step: 12 samples, 17 swept parts, 4 disc spheres with their chords on demand, 8 rays) along five paths, 20,000 steps each:

| Path | Median | p99 | Max | 60 µs target |
|---|---|---|---|---|
| Low over the village's plots, 0.3–1.5 m up | 7.0 µs | 46.8 µs | 150.9 µs | met |
| Landed on the village's steepest plot (37.6°), tilted onto it, the skids 1 mm in | 29.7 µs | 45.7 µs | 476.6 µs | met |
| Skimming a field at 0.3 m, 15 m/s | 19.5 µs | 31.5 µs | 1250.6 µs | met |
| Along the ledge's face, 0.3 m out at mid-face, 10 m/s | 11.1 µs | 27.0 µs | 88.9 µs | met |
| Over the far ring at 1–3 m (11.8 m facets) | 6.0 µs | 19.5 µs | 182.0 µs | met |

- **For comparison, the test range in the same run:** a median of 3.8–20.0 µs and a p99 of 9.0–103.8 µs. E01's run misses the 60 µs target at 103.8 µs, and its maxima reach 79–1838 µs. The maxima on both maps are the shared machine's preemptions; the medians and p99s are the code.
- **All rows allocate 0 B.**
- **K§10.5's batch** (44 + 46 + 8, with 32 trailing fiber segments), shown:
  - low over the village: 55.7 / 125.9 µs (median / p99)
  - skimming: 25.9 / 41.1 µs
  - along the ledge: 27.1 / 60.7 µs
  - over the far ring: 22.1 / 42.4 µs

  It isn't run on the tilted landing, since its parts can't be turned.
- **K§10.4's budgets in physics' pattern:**
  - capsules: 61.0 ms per 100,000 (budget 100 ms)
  - rays: 31.1 ms per 100,000 (budget 100 ms)
  - samples: 100,000 over the golden's seven spots in 7.64 ms (budget 10 ms)
  - capsules at random poses there, shown: 211.3 ms per 100,000, against the test range's 210.7 ms
  - rays at random poses there, shown: 54.2 ms per 100,000, against the test range's 97.5 ms
- **The farmland's line:** "worldbench: farmland bench done, every budget met".
- **The test range's line:** "1 over budget". It is MicroDetailNear over S04's straw, 378.8 µs at p99 against 250 µs. That code is untouched by this task, and it failed the same way in this session's first run.

**Before the shortcuts** (this session's first run, the same power mode, a lighter load), physics' pattern missed the 60 µs target on four of the five rows, at 58.9–108.8 µs p99. The exact shortcuts (§7) brought them to the table above.

## Golden files, audited case by case
- **`golden/floor.txt`:** `query_version` 7 → 8. Every other case passed unchanged before the re-record: `content_hash`, `floor.ground`, `floor.contacts`, `floor.rays`. The floor stub never touches the terrain.
- **`golden/test-range.txt`:** `query_version` 7 → 8. All 13 other cases passed unchanged before the re-record: the content hash, ground, contacts and their overflow, rays, micro and its overflow, wires and theirs, the no-wire filters, snags and theirs. A world on a plane takes the old code path: the plane's sweep, its `AboveGround` skip, `RayPrim`.
- **`golden/wakes.txt`:** unchanged (not re-written).
- **`golden/farmland.txt`, new:**
  - `query_version`, `content_hash`
  - `farmland.probes`: the 1,000 heights' hash, the case failing unless all equal validate.py's bits
  - `farmland.steps`: the 12 steps' fields
  - `farmland.ground`, `farmland.contacts` and `farmland.rays`: 10,000 each over seven spots near the ground. The spots are the village's steep plots, a field, the map's seam, the ledge, a far seam, the far ring and the world's edge. Samples and rays are batched against one by one.
- **`flightbench --golden`:** no flight hash moved. The flights run on the floor and the test range, and the log header's query version is not in their hashes.

## What 2.1 (the fiber's query cost) must know
- **StaticContacts on a laid segment now pays for the terrain,** even though the fiber discards the ground's contact (its nodes take the ground from SampleGround's batch). A segment lying on the ground is always within the margin of it, so a clearance certificate that counts the ground's contact never clears it.
- **Measured (shown, not judged):** one still query of a laid segment, at the clearance margin 0.5 m. The time grows about linearly with length (the footprint), and the spacing law's long far segments are tens to hundreds of metres.

| A laid segment on the ground | Median | Max (200 queries) |
|---|---|---|
| 0.5 m | 3.4 µs | 33.3 µs |
| 2 m | 5.9 µs | 31.0 µs |
| 10 m | 16.1 µs | 60.4 µs |
| 50 m | 54.3 µs | 164.5 µs |
| 200 m | 179.1 µs | 726.3 µs |

The spacing law (`tether.json`: 0.1 m near the exit, then growing by 0.5 m per material metre) makes the far segments of a 1.5 km line tens to hundreds of metres long. Each of them, queried every step, would cost the step more than PLT-04's 60 µs alone.

- **The cheapest fix is the caller's:** a filter flag, say `QueryFilter.NoGround`, that skips the ground in StaticContacts as `NoWires` skips wires (MapWorld.StaticContacts, the terrain's two lines). The certificate would then ignore the ground, which the node batch already carries. That is an API change, so it is 2.1's to make and sign off; this task does not add an unused flag.

## What 2.2 (contact on slopes) must know
- **SampleGround's normal is the facet's** (constant on each, 0–37.6° on the map). Its height is the top at a step line (the ledge's 1.4–1.6 m face). A support crossing the ledge below the top sees a 1.4 m jump in SampleGround's height. The face is in StaticContacts (shape 0 of the ground), so the leg's crossing should be taken from there.
- **`Geometry(Ground, 0)` is false on the farmland.** Physics still reads it in three places, and each fails safe today, though not correctly:
  - `ContactModel.Footprint` and `Layer.Depth`: false reads as a plane, the same as before.
  - `ContactModel.PastCentre`: false, the same as a plane.
  - `ContactModel.Cuts` and `BladeCut.Nearest`/`Entry`: false means a blade never cuts the terrain. Prop strikes on the ground need the contact's point and normal, or a local plane from SampleGround.
- **A contact's normal:**
  - **Outside:** it is the direction from the nearest piece's point to the core's. On a facet's interior that is the facet's normal; at a crease or a lip it is rounded.
  - **Inside:** it is the deepest point's facet normal, or a face's horizontal normal.
  - Both are continuous where the outside distance reaches 0.
- **The ledge's lip** (W2: the top tile's soil and a 0.02 m lip radius for the fiber's edge contact) is the edge where the surround's facets meet the face. Its rounding is physics' to add.
- **Furrows** are not in the heightfield (0.977 m cells). The `tilled` class and each field's `furrow_axis` are there for 2.2's decision.

## What 3.1 (flight on the farmland) must know
- `MapWorld.Load(root, "farmland")` gives the world. It has no start points: map.json has none, and D-092's village start point has no record in the package yet.
- The load takes about 0.3 s and 28 MB. The world identity a log records is its content hash and `QueryVersion` 8. W2's log format 73 (the package's manifest hash in the header) is not done; ContentHash covers the manifest already.

## Risks
- **The bench ran on a shared machine in "best power efficiency" mode.** The test range's own rows ran slower than D-061 recorded: E01's run was 103.8 µs at p99 in the final run and 93.5 µs in the first, against under 60 then, on unchanged code. The farmland's rows are judged in that run, beside the test range's. QA (4.1) should rerun `--bench` on a quiet machine in the usual mode; a faster run would only lower them.
- **Overlapping tiles whose surfaces cross** are refused at load (the step derivation). The farmland's overlaps are 1.276 m apart at least (W2, measured).

# Task 2.1: the fiber's query cost (L6)

**Owner of this part:** physics-engineer · **For:** 1.2's re-run, 2.2, 4.1 (qa-engineer) · **Requirements:** PLT-04, PHY-18, LOG-02 · **Model:** 79, unchanged (no contact answer changes) · **QueryVersion:** 8, unchanged answers (the API gains a filter and a call)

## The cause (L1, `docs/reports/lag/model-79-flights.md`)
- **Every step,** `FiberContacts.ShapeHits` ran one whole `StaticContacts` query on each laid segment its clearance certificate did not clear: one resting on a shape or within the 0.5 m margin of one. A node with a shape's hit ran a second, smaller query (`NodeShapes`).
- **A query's cost grows with its bounding box,** not its capsule. The far segments are 20–200 m long (500 m on a 2 km line), so a diagonal one walks every 2 m cell of a box a hundred metres wide.
- **Since 1.1 every query near the ground also pays for the terrain,** which the fiber throws away (its nodes take the ground from `SampleGround`'s batch).
- **Profiling the three flights after the first fix** (a diagnostic copy of `tools/lagprobe` that times `Prepare`'s parts; not committed) found two more costs in the fiber's own gathering, both quadratic in the line's contacts:
  - `AddHit` scanned back over the segment's hits for each new one: 36,580 iterations a step in flight 37's crash into the straw.
  - `CacheHits` cut a 199 m segment into 798 pieces of 0.25 m and walked the cache cube's clamped edge cells for each.

## Decisions

### 1. Two additions to the world query (signed off here, the API's physics side)
- **`QueryFilter.NoGround`:** `StaticContacts` leaves the ground out, plane or terrain. Raycast ignores it. Everything else is answered as before.
- **`IWorldQuery.ShapeContact(obj, shape, previous, current, margin, out contact)`:** the contact `StaticContacts` gives for one placed or runtime shape, to the bit: the same bounds check against the query's reach and the same sweep (`SweepPrim`'s, a runtime prim's two lines). False for the ground, a wire or a pair that does not exist.
  - It is an interface call, not a fiber-side copy of `ShapeMath`, so the world owns its math and the benches time it as a world query (`TimedQuery`, `tools/lagprobe`'s `Shape` kind).
  - `FloorWorld`, `MapWorld`, the three test worlds and `TimedQuery` implement it. `FlightWorld` forwards it.
- **QueryVersion stays 8:** no answer to an existing call changes (the goldens below), and 8 is this change's own version, not yet merged.

### 2. The near lists (`FiberContacts`)
- **A segment the certificate does not clear keeps a list:** the shapes one `StaticContacts` found within the margin plus the skin of a core it lay on, the ground left out (`NoWires | NoGround`), with their distances then, in canonical order.
- **The list holds while both ends of the segment lie within the skin of its core.** The distance from a segment is convex, so the whole segment lies within its ends' larger distance d1 of the core. A shape's distance from the segment is then:
  - at least its listed distance less d1, so every shape within the margin of the segment is on the list;
  - at most its listed distance (or 0, if the core met it) plus d2, the core's ends' larger distance from the segment.
- **Each step,** a listed shape whose lower bound leaves it possibly within `KeepGap` is asked alone (`ShapeContact` at the margin): its hit is the query's own, in the query's order. The others only decide the certificate: one surely within the margin denies it, and only where none is sure are those straddling the margin asked.
- **The query's own buffer is kept exact.** The old query counted the ground and every shape within the margin against its 64; on overflow it re-asked at `KeepGap`.
  - Where the listed shapes possibly within the margin reach 64, the list still answers if one is surely within the margin and fewer than 64 may be within `KeepGap`: overflow or not, the hits are the same (each shape within `KeepGap`, its same contact, the same order) and there is no certificate.
  - Otherwise, and where a list overflows its own 256, the old query answers, unchanged.
- **A node's shape** (`Evaluate`) is asked alone when its segment counted fewer than 64 shapes possibly within `KeepGap` (a node on a segment is no nearer a shape than the segment, so the node's own query could not have dropped one), else by the node's query as before. A shape both segments by a node name is asked once.
- **The numbers:**
  - the skin, 0.25 m (assumed): a resting segment keeps its list for good, and a line dragged past shapes at 20 m/s rebuilds about once in 12 steps
  - the slack, 1 µm (numerical): far above a closest approach's rounding a kilometre out (a cylinder's search reads a few nm long), so no bound is decided by rounding
  - the capacity, 256 a list (assumed: four times the query's buffer)
  - the slots, two a node (each node names at most one, and a segment rebuilds at most once a step); a slot no node names is free at the next step
- **Where the list lives:** `CableNode.Near`, the node's slot plus one. It moves with the node and a split's new node takes its segment's, as `Cleared` does. A list is judged only by its own core, so a node holding a stale one just rebuilds.
- **Exact by construction, and checked:** `FiberContacts.NearLists` off gives the old path, the differential tests' reference.

### 3. The object grid's corridor (`MapWorld.StaticContacts`)
- **A reach of more than 16 cells walks only its corridor,** row by row: the cells the hull of the sweep's four core ends reaches in x–z within the pad and 1 µm (numerical). That is the terrain's corridor (1.1, §7) applied to the 2 m object grid; `TerrainSweep.Clip` is shared.
- **Exact:** every core pose of the sweep lies in the hull, so a shape that can come within the margin meets the corridor; the contacts go in canonical order whatever visits them; an item is visited once, in the first corridor cell (row by row) its own cells share. An edge row stands for everything beyond the grid on its side, so its band reaches to the bounds' side.
- **The tree inside an object** (a house's boxes) still walks its object's bounds against the reach box: with the near lists that walk runs only at a rebuild.

### 4. The fiber's own gathering (`FiberContacts`)
- **`AddHit`** finds the segment's earlier hit of a member in a table of that segment's generation (open addressing, 4,096 slots), not by a scan back. A segment holds at most one hit a member, so it finds what the scan found.
- **`CacheHits`** passes over a piece whose cells are the last visited piece's (a piece outside the cube is clamped onto its edge cells), stops once the pieces have met the cube and left it, and rejects a member farther than its reach on one axis before the closest approach.
  - The stop is exact while the line's radius is under `FiberCache.LineReach` (0.25 mm; the wettest line's is 0.2 mm), else it is not taken: a member kept lies within its reach of a point of the segment within the cache's sure radius, the grid lists it in that point's cell, the piece holding the point visits it, and the pieces meeting the cube are one run.

## No answer changed (the evidence)
- **The pilot's three model-79 flights replay bit for bit** on this core: 18,147, 66,941 and 234,901 step records (flights 37, 38 and 35), through `tools/lagprobe phases` and `flightbench --replay-step` alike.
  - The logs are of QueryVersion 7, so `tools/replay` refuses them, as LOG-02 says it must.
  - The two timed replays compare every record instead, so a changed answer would show as a diverging record.
- **The differential tests** (`FiberContactTests.TheNearListsChangeNoBit`) fly two lines in lockstep on the test range, the second with the near lists off. They compare every step's record and events as the log writes them:

| Flight | Steps | First differing | Node-steps held by a shape | Whole queries, near lists / without |
|---|---|---|---|---|
| Dragged through S04's straw, Severe wind | 10,000 | none | 79,874 | 601 / 263,463 (1,666,906 one-shape checks) |
| Held on the yard's gate and house (as flight 35) | 12,000 | none | 28,164 | 407 / 88,283 (162,892 checks) |

- **The world:**
  - `MapWorldTests.StaticContacts_AreEveryShapesOwn_OnLongReaches_AndNoGroundLeavesOnlyThePlane` runs 300 seeded reaches: still segments up to 150 m and sweeps moving up to 5 m, across the yard, the straw and the posadka's verge. 153 of them walk the corridor. `StaticContacts` gives the same contacts to the bit as `ShapeContact` over every shape, and `NoGround` removes the plane's contact and nothing else.
  - `FloorWorldTests.NoGroundAndShapeContact_AreStaticContactsWithoutTheFloor_ShapeByShape` checks the same, still and swept.
  - `worldbench --golden`: all 37 cases pass unchanged, nothing re-recorded (floor 5, test range 14, wakes 11, farmland 7).
- **`flightbench --golden`:** all 6 cases match, nothing re-recorded.
- **So the model stays 79,** and no golden moved.

## Costs

**The machine was loaded** throughout:
- Other sessions held it at 48–100 % CPU (measured, `Win32_Processor.LoadPercentage`).
- It ran on AC, Balanced plan, best power efficiency mode, CPU 3201/3201 MHz.
- Means and p50s repeat. p99s and maxima carry the other sessions' preemptions, so QA reruns these on a quiet machine.

**Before and after, the same tool back to back** (measured, `tools/lagprobe phases`). "Before" is this branch's head before 2.1, built from `git archive`.
- The numbers are means per step, µs.
- "Fiber q" is the world queries inside the line's step: lagprobe's tether q, its per-call timers included.

| Stretch | Step before → after | Fiber q before → after | Whole StaticContacts a step before → after |
|---|---|---|---|
| 35, the yard hover (135–155 s) | 254–429 → 210–236 | 98–193 → 49–62 | 103–199 µs at 80–90 calls → 7–9 µs at 45 calls |
| 35, leaving, the crash and after (160–235 s) | 386–618 → 223–312 | 252–359 → 70–83 | 266–372 µs at 105–137 calls → 15–21 µs at 65–97 calls |
| 38, the laid end on the verge (64–66 s) | 380–584 → 202–239 | 211–377 → 35–47 | 214–376 µs at 47–48 calls → 7–14 µs at 45 calls |
| 37, in the straw (13–14 s) | 374–536 → 382–434 | 169–190 → 140–155 | 146–157 µs at 65–80 calls → 26–36 µs at 45 calls |
| 37, the crash into the straw (17–18 s) | 579–918 → 489–747 | 105–236 → 66–168 | 103–216 µs at 112–162 calls → 41–88 µs at 95–139 calls |

The last column holds the drone's own contact queries too: about 45 calls and 6–9 µs a step in flight.

The one-shape checks that replace the whole queries cost 22–97 µs at 49–185 calls a step in these stretches (lagprobe's `Shape` kind, its timers included).

**The clean timing** (measured, `flightbench --replay-step`). The step is timed with no timer inside it; the world queries are timed in a second pass (`TimedQuery`).

| Window | Step p50 | Step p99 | World queries p50 | World queries p99 | Allocated |
|---|---|---|---|---|---|
| 38, 63.5–67 s | 165.6 µs | 592.2 µs | 35.2 µs | 87.6 µs | 0 B |
| 35, 135–155 s | 172.2 µs | 513.7 µs | 53.1 µs | 130.9 µs | 0 B |
| 35, 155–235 s | 184.4 µs | 506.6 µs | 77.2 µs | 194.3 µs | 0 B |
| 37, 12–14 s | 317.7 µs | 1,233.6 µs | 55.3 µs | 188.6 µs | 0 B |
| 37, 17–18.2 s | 528.7 µs | 1,822.1 µs | 88.1 µs | 459.0 µs | 0 B |

**Judged against PLT-04** (p99 ≤ 300 µs, world queries < 60 µs):
- **The lag's cause is gone.** No resting segment pays a whole query each step. The step's mean in the yard and on the verge is about half what it was, under L1's "kept up" line (about 400 µs in .NET; the player is about 2× slower).
- **Not met at p99 on this machine,** in any window. 35's and 38's p50s are 165–185 µs, so their p99s of 500–600 µs are for a quiet run to judge (QA).
- **35's world queries stay near the line once crashed** (p50 77 µs). A line wrapped on the gate's planks takes about 150 one-shape checks a step: two for each touching shape, the segment's hit and its node's. That is the floor of this contact model's answers, since each hit's point and normal are asked anew each step.
- **37 is not met, and L6 does not reach it.** Its straw stretches spend the step elsewhere, not in the whole-query path this task cuts (a follow-up unit, below):
  - in the line's elements: S04's stems and straws through the caches
  - in the hubs' `SoilUnder` rays through the straw boxes: 60–100 µs, L1's own note
  - in the crash, in the drone's own contacts

**The 1.5 km line over the farmland** (measured, `flightbench --bench farmland-line-step`, the terrain alone):
- **The flight:** 1,602 m of route, 1,977.5 m paid out, 32 nodes, 2 trail points; the line whole and the drone up.
- **The line's costs:** 45,833 whole queries over 205,521 steps, each a certificate's renewal with the ground left out. No one-shape checks: the farmland has no objects yet.
- **The step:** p99 337.0 µs and 352.5 µs over two runs, 0 B after the first step.
  - The route: p99 310.1–330.9 µs.
  - The hover: p99 311.1–375.1 µs.
  - p50 131–181 µs.
- **Its world queries:** p50 17.4–19.1 µs, p99 96.2–107.9 µs, 40.8 calls a step.
  - A diagnostic split of the steps over 60 µs (not committed) puts half of them in the first 12 s, on the ground and in the climb.
  - The drone's own contact queries on the terrain carry them; the fiber's whole queries are 0.06–0.16 a step there. So they are 2.2's slopes, not the line.
- **Before 2.1,** each of those 45,833 queries also swept the terrain: 179 µs at 200 m (1.1's table), more for the 500 m far segments of a 2 km line.

**`worldbench --bench`** (measured; the machine at 100 % CPU when it began) printed 1 row over budget on the floor and 1 on the farmland. Neither is 2.1's code:
- **The floor's capsules (144.0 ms per 100,000) were the load.** Its rays, whose code 2.1 does not touch, ran 4× slower in the same run.
- **Interleaved runs settle it:** this branch's head before 2.1 and after 2.1, built from `git archive` and run three times each in turn, give 33.7–37.0 ms and 32.6–36.5 ms (budget 100 ms).
- **The farmland's 100,000 samples (52.4 ms, budget 10 ms)** run `SampleGround`, which 2.1 does not change.
- **Physics' pattern is within budget on both maps:** test range 80.1 ms per 100,000 capsules and 59.9 ms per 100,000 rays; farmland 91.0 and 58.2 ms.
- **The farmland's laid-segment rows** (the default filter, so they still pay the terrain): medians of 4.0, 5.7, 17.0, 64.2 and 149.4 µs at 0.5, 2, 10, 50 and 200 m.

## For 1.2's re-run and the next units
- **After 1.2,** `flightbench --bench farmland-line-step` lays the same line through the village and the F6|F7, F3|F5 and F4|F10 belts. The case's world is `MapWorld.Load(root, "farmland")`, so the objects come with 1.2 and nothing in the case changes.
- **Suggested follow-up for flight 37's straw** (not this task):
  - the elements' walk (`CacheHits` tests each cached stem per segment)
  - `SoilUnder`'s repeated rays through stacked straw boxes
  - the drone's contacts in a crash
- **Unchanged, flagged:** the clearance certificate lets a segment's ends move the margin less the line's radius from the certified core before the next query.
  - So a shape can come within the line's radius of a certified segment, inside `InsertGap`, without a slot.
  - `margin − InsertGap` would close it, but that changes answers (a new model). It is noted here, not changed.

# Task 2.2: the flight on hills

**Owner of this part:** physics-engineer · **For:** 2.3, 2.4, 3.1, 4.1 (qa-engineer) · **Requirements:** PHY-01, PHY-02, PHY-04, PLT-04, WLD-11, WLD-16 · **Model:** 82 · **QueryVersion:** 8, unchanged (no world code changed)

Numbers are labelled as above. "Before" is this branch at d45f2054 (model 79), built in a worktree with the same scenario files; "after" is the head. The scenarios are `SlopeScenarios` (the suite asserts them in `SlopeTests`), printed by `flightbench --slopes`.

## The rows' world
- **A slope is a package the scenario writes:** one 64 m tile of `meadow_sod` at 1 m spacing, a plane rising north whose rows rise a whole number of 2 mm height units a metre, so every facet has the plane's normal. The facets are 0, 9.98, 20.00, 30.03 and 38.03° (measured from SampleGround's normals). The village's steepest plot is 37.6° (1.1).
- **0° is the stub floor of `meadow_sod`** (FloorWorld, a plane), and a 0° tile of the package is its terrain twin: the same ground through the terrain's code.
- **The steps are the farmland's own** (`maps/farmland/`): the east ledge at x = 2000, z = 500 (1.504 m), and the far_N | far_NE seam's highest step on x = 2000 for z in 2100–4900 (73.8 mm).

## Decisions

### 1. A support stands on the terrain's nearest piece (`ContactModel.TerrainGround`)
- **On a terrain** (`Geometry(Ground, 0)` false) a support's ground pair comes from its own sweep's contact with the ground. That is the piece nearest it: a facet, a step's face or the lip between them. Its plane is measured at the core now, as a latch's (`Depth`). The sweep already ran (the support's own `StaticContacts`); its ground contact was dropped.
- **Why not SampleGround alone:** it reports a step's top, so a support past a face below the top read the step's height as its overlap. At the far seam (73.8 mm) a skid's end sliding into the face put 385 N on its leg and lifted the drone onto the step (the steps' rows, before). At the ledge it would read 1.4–1.6 m.
- **Where the sweep finds no ground** (farther than the margin), the sample stands. It never touches there, since a core inside the ground always has its contact.
- **On a plane nothing changes:** the sample's pair and its transitions, in their order. The goldens are unmoved (below).
- **A sample's overlap on a slope** is r − n_y·(y − h) (`Sunk`): the sphere's overlap of the facet's plane along its normal. K§7.1 step 2's (h − (y − r))·n_y left out r·(1 − n_y), 1.06 mm on a 5 mm support at 38° (derived). On a level plane n_y is 1, and the two are the same bits. The skids' middle nodes read it.
- **A skid's sweep leaves the ground out on a terrain** (`QueryFilter.NoGround`). Its ground is its nodes' (the samples and the supports'), and `Update` dropped the ground's contact anyway. Only a buffer overflow could tell the difference, and it would now keep one more object contact.
- **A skid's middle node still reads the sample.** A straight skid meets a plane face first at an end, where its supports are, so its middle cannot reach a face's column first. At a step's corner (two faces at 90°) it could: named here, not met in the rows.

### 2. The blades meet a terrain's ground (`ContactModel.GroundPlane`)
- **Before:** `Cuts` and `PropStrikes` read the ground's geometry, which a terrain does not have. A disc on the terrain logged its PropStrike and its blades did nothing: no pass, no carve, no damage. The tipped landing on the 0° tile had 0 passes against the plane's 144.
- **Now a disc's pair on a terrain's ground is the plane of the contact that names it,** its point and normal: the gate's contact for the slab's cut, the pair's for its passes. The disc (0.127 m) spans the facets round one 0.977 m cell at most, and its contact names the piece it meets.
- **Measured:** on the 0° tile the tipped landing's blades do what they do on the plane, to the printed digit (144 passes in the soil, 10.77 J, 14 damage events). On 20°: 379 passes, 19.51 J.

### 3. No anchor slide along a leg's nil stiffness (`ContactModel.NilStiffness`), found by the 38° row
- **The blow-up:** the pilot's landing on 38° (nose uphill, the throttle cut) tumbled, and 2.5 s after the touch one step put 3.4e5 N·m on the body with 5 N of contact force: 207,766 m/s², the body rate 7,562 rad/s.
- **Its cause** (measured with a temporary print, not committed): past its cone the legs' law slides a point's anchor by (size − kinetic)/k_tt, k_tt the leg's stiffness along the slip. The rear right support had spent its side give past the sway load (`_side` 0) and slid square to its tube (u_t 5e-5). So k_tt was 5e-7 N/m, 2.7e-9 of its axial spring's share, and the anchor's stretch became 709 km. The foot, the contact point less the stretch, was the moment's lever.
- **The rule:** the slide is taken only where k_tt is more than `NilStiffness` (1e-6, assumed) of the leg's axial spring's share. Below it the leg is a mechanism along the slip, and its stretch stays, as the law already did at k_tt 0.
- **Two wider bounds were tried and dropped** (measured): the stretch held within the leg's lever (0.176 and 0.187 m), and a slide held within the stretch it relieves. Both moved p7g-legs' own scenarios, which slide their anchors past both in ordinary steps (`LegChangeTests.TheNextLandingFeelsADamagedLeg`: the missing leg no longer tipped; `LegReplayTests.ReplayThroughABrokenAndADetachedLeg`; `RangeContactTests.WithoutTheHangingGiveTheWindyCrownIsModel50s`), and the second also the range golden. The nil rule moves none of them.
- **Suggested, a p7g-legs review:** past the sway load the side give is a mechanism, and the trial's force and the cone's slide use different stiffnesses, so a slide can run far past the stretch that loaded it. This rule only stops the division by nothing.

### 4. The ledge's lip: no rounding
- **The legs:** a support's contact over the lip is its distance to the edge, rounded by its own 5 mm radius, and the soil's law takes a lip's load as any face's. A 0.02 m rounding would change no leg's answer that matters at a 1.4 m step.
- **The fiber:** its segments leave the ground out (2.1), so the line meets the lip only at its nodes, and a node behind the face now takes the face (5). At the soil's 0.02 m `lip_edge_radius_m` a bend's stress is E·r/R = 73 GPa × 62.5 µm / 0.02 m = 0.23 GPa (derived), under the proof stress's 0.69 GPa (tether.json). So the rounding could cut nothing either.
- **Not added.** The line's capstan over the lip needs the step walls in the segments' queries: a `QueryFilter` for the ground's walls alone, the world's to add. Suggested for a later unit.

### 5. The fiber's ground over the heightfield
- **The nodes take the ground from SampleGround,** as before: the plane through (x, h, z) with the facet's normal, and the coating's friction on its surface. On a slope that is the facet's own plane, and the gap n·(x − p) − r is the node's true distance.
- **A node behind a step's face takes the face** (`FiberLine.NearestGround`). A node more than `FiberLine.WallDepth` (InsertGap, 0.05 m) under its sample's plane is behind a face, and its plane is the ground's nearest piece, from StaticContacts. Before, the top's plane would have thrown it up the step (1.4 m at the ledge).
  - **Tested** at the ledge (`SlopeTests`): a node 0.3 m under the top and 2 cm behind the face takes the face, its normal (1, 0, 0), 2 cm deep.
  - Only a node that deep pays the query.
- **Segments between laid nodes are straight.** On rolling ground a long one can cut under a rise. The farmland line's case now reports it (8).
- **The line over the ledge** (a row): laid on the far ring and dragged up and over the lip as the drone flies west over the top, it stays whole, its fastest node 3.5 m/s, no node behind the face; the same before and after.

### 6. Furrows: not in the physics, and why
- **They have a physical role** (REAL-05). At K§7.2's 0.05 m ridges every 0.5 m, a skid pair along the rows (0.205 m track) can sit one skid on a crest and one in a trough, tilted 13.7° (derived). At K§9.2's ploughed 0.15–0.30 m ridges, 36–56°.
- **But they are not geometry anywhere yet.** The fields' furrows are the AerialMud texture and its normal map on 0.977 m heights (W2). The package holds only the class and each field's `furrow_axis`: no height, pitch or phase.
- **So the physics adds none on its own.** Rows the pilot cannot see, at a pitch and phase the drawn texture does not share, would break WLD-11 (what is drawn is what is touched) and REAL-05's own rule that collision follows the visual. Their numbers would be assumed, with effects of tens of degrees.
- **Where they belong:** a world unit that puts the relief into the package (height, pitch and phase per field, from the references and the fields' overgrowth) and into the drawn terrain near the drone, with the world-query core answering it. The contact model needs no change to meet it: it meets any facet's normal (the slope rows), and a support meets any piece (1). Suggested.
- **`tilled`'s soil row already carries the ploughed loam** (bearing, unload ratio, friction), so a landing on a field sinks into loose soil now.

### 7. The clearance certificate: the margin less KeepGap (`FiberContacts.ShapeHits`)
- **Before:** a certified segment's ends could move the margin less the line's radius (0.4998 m) before it re-queried. So a shape could come within the line's radius of it, inside InsertGap (2.1's flag).
- **Now the margin less KeepGap** (0.4 m). Every point of the segment then lies within that of the certified core (the distance from a segment is convex), so every shape stays farther than KeepGap from it, the reach of any hit the query gives (a member on a slot keeps it to there). The certificate drops no answer of the query's.
- **Checked:** `FiberContactTests.TheCertificateChangesNoBit` flies 2.1's two lockstep flights against a reference without the certificate (`FiberContacts.Certificates` off: every segment answered every step). Every record and event is the same bits over 10,000 and 12,000 steps. Whole queries: 628 against 716 (straw), 492 against 796 (yard).
- **Its cost:** the farmland line's whole queries rose from 46,019 to 59,813 (the same start, measured), each a certificate's renewal on a map with no objects yet.

### 8. The farmland line's start, and the drone's own cost on the terrain
- **The cause of 2.1's "half the slow steps while the drone sits and climbs"** (measured with a temporary per-kind timer, not committed):
  - `farmland-line-step` set the drone down level on the track's 5.4° facet, its supports up to 23 mm in the ground (derived).
  - On the first step the rear legs broke, and the front ones by 0.45 s. Their four pieces rolled on the terrain until they slept at 4.5 s.
  - The pieces' contacts were 144 of the 173 contact-margin queries a step, and 60–113 µs of world queries a step (loaded).
  - The drone then flew the line without legs (29 such queries a step).
- **The fix is the case's:** the drone is set down on the facet, its up the facet's normal, its skids 10 µm in. No leg breaks.
- **After it, the drone's own terrain cost:** its skids leave the ground out (1). The rest is its 41 shapes' sweeps, about 9 µs a step once it is above the margin (measured, loaded).
- **The line's case now also reports its chain over the terrain at the end** (measured): 5 of its 31 segments cut more than 1 cm under the ground, the deepest 3.24 m, the longest segment 398 m (before: 4.89 m, 433 m). These are the far, laid segments, straight between nodes the spacing law puts hundreds of metres apart. The line still lies on the ground at its nodes. Over a rise between them its tension's way, its friction and its drag there are the chord's, not the ground's. Suggested: a node where a laid segment dips under the ground (a split at its deepest point, as a member's), with the terrain's ray or sampled points for the test.

**The line, interleaved runs on the shared machine** (`flightbench --bench farmland-line-step`, both cores with the fixed start, after-before-after-before; the CPU load as each began, measured by `Win32_Processor.LoadPercentage`; AC, Balanced plan, best power efficiency mode, 3201 MHz):

| Run | Load | Step p99 (all after the first) | The climb's step p50 / p99 | World queries in the climb p50 / p99 | World queries in all p50 / p99 | Whole queries |
|---|---|---|---|---|---|---|
| After, 1 | 54 % | 266.2 µs | 106.1 / 200.2 µs | 18.9 / 61.2 µs | 19.7 / 42.5 µs | 59,813 |
| Before, 1 | 59 % | 298.0 µs | 117.6 / 201.8 µs | 20.7 / 68.9 µs | 21.2 / 44.7 µs | 46,019 |
| After, 2 | 51 % | 287.1 µs | 113.1 / 213.5 µs | 19.7 / 64.1 µs | 21.1 / 43.9 µs | 59,813 |
| Before, 2 | 35 % | 370.4 µs | 118.6 / 246.8 µs | 21.6 / 68.6 µs | 20.9 / 47.4 µs | 46,019 |
| After, the final build | the suite running beside it | 273.8 µs | 110.2 / 195.0 µs | 19.9 / 43.9 µs | 20.6 / 40.5 µs | 59,813 |

- **Both pass PLT-04's 300 µs at p99 in its runs after,** with 0 B after the first step and the line whole at 1,971 m paid out. "Before" passes once (298.0) and misses once (370.4).
- **The final build flies the same flight** (1,971.3 m paid out, 59,813 whole queries, the same 205,521 steps); the runs above it were on an earlier build of this branch with 3's lever bound in the nil rule's place, which this flight never met either.
- **The climb's world queries** (the ground and the climb, sitting 0.5 s on 21 shapes within the margin) are p99 44–64 µs after against 69 before: the skids' sweeps without the ground. In all, p99 41–44 µs, under the 60 µs line.
- **With the level start** (2.1's case, the legs broken) the same core's world queries reached p99 134–193 µs in a busier hour; that was the pieces' cost (8).

- **Not judged here:** the machine was shared and loaded throughout, so the p99s carry other sessions' preemptions. A quiet run is QA's (4.1), with 2.1's owed one.

## The rows (`flightbench --slopes`; before → after)

**At rest, disarmed, 3 s on meadow_sod, its skids along the fall line:** the steepest slope it holds on, by bisection over the tile's whole units. A slip is still moving more than 1 cm in the last second: set down unloaded, it first settles up to 7 cm on its downhill legs' side give.

| Moisture | Holds to, before → after | atan μk | atan μs |
|---|---|---|---|
| Dry | 24.89° → 24.89° | 24.23° | 28.81° |
| Wet (Rain) | 25.27° → 25.36° | 19.80° | 23.75° |
| Saturated (Severe rain) | 25.64° → 25.73° | 15.11° | 18.26° |

- **Dry, it holds between atan μk and atan μs,** as compliant legs give. They share the slope's pull by their side stiffness, not their load, so the light uphill corners slide at μk first (measured: their soil anchors slide at 24° while the downhill ones hold) and the downhill ones hold the rest. A slide the set-down starts stops below atan μk.
- **Wet and saturated, it holds past atan μs** by the wet interface's adhesion (surfaces.json's c_a, 2 and 1 kPa over the grooves' wetted footprints) and the grooves' end resistance.
  - "Saturated meadow slips above about 18°" is the friction's bound alone. With the table's adhesion it holds to 25.7°.
  - The pilot's account, legs that stick and most in rain, is the same adhesion.
- **Across the slope** (skids level) it stands at 10° and rolls over its downhill skid at 20° (dry). The rigid body's tip is 23.99° (LandingStability); the downhill legs' side give and sink take it lower.
- None tunnels.

**The pilot's landing** (0.5 m/s, the throttle cut at the touch, dry): the peak acceleration, then the end.

| Facet | Along, before | Along, after | Across, before | Across, after |
|---|---|---|---|---|
| 0° | 36 m/s², rests | 36, rests | 36, rests | 36, rests |
| 9.98° | 391, rests | 22, rests | 85, rolls onto its side | 93, rolls onto its side |
| 20.00° | 97, slides on at 1.9 m/s | 31, rests on the slope | 97, rolls, slides at 0.5 m/s | 112, rolls, slides at 0.4 m/s |
| 30.03° | 300, tumbles, 6.1 m/s | 311, tumbles, 7.0 m/s | 211, slides at 5.2 m/s | 592, tumbles, 8.0 m/s |
| 38.03° | 622, slides at 9.9 m/s | 367, tumbles, 8.9 m/s | 573, tumbles, 8.3 m/s | 85, slides at 8.4 m/s |

- **The 0° terrain lands as the plane** to the printed digits (sink 3.2657 mm, peak 35.77 m/s²), before and after.
- **On 9.98° and 20° along,** the uphill supports now meet the facet as their sweep finds it: the touch's peak falls from 391 to 22 and from 97 to 31 m/s², and on 20° it now rests where before it slid on.
- **Past atan μs (30° and 38°)** it slides or tumbles either way; which one is the touch's detail.
- **Across a slope the cut landing rolls the drone onto its side from 10°,** before as after. After the uphill skid's touch it falls onto the downhill skid, with the slope's tip 14° away (24° − 10°). On the floor the same 10° roll touchdown does not tip (LandingScenarios, measured). It is the pilot's cut on a side slope.
- **No landing tunnels, and none passes twice the crash's acceleration** (the row's check). On this core before `NilStiffness`, the 38° landing's run-away anchor (3) gave 207,766 m/s².

**A crash into the slope** at 10 m/s uphill, disarmed and level, against the same dive onto the plane turned by the slope (the drone pitched nose-down by it, diving at it):

| Facet | The deepest past the ground, slope / plane | The crash |
|---|---|---|
| 20.00° | 172.52 / 173.47 mm | the canister, 3.66 m/s, 516 m/s² |
| 30.03° | 188.32 / 189.91 mm | the canister, 4.94 m/s, 549 m/s² |
| 38.03° | 193.42 / 194.36 mm | the canister, 5.96 m/s, 576 m/s² |

- Within 2 mm of the plane's (gravity turns with the slope over the crash's 50 ms), unchanged by 2.2, no tunnel.
- The deepest is the front skid's end support, 17–19 cm. That is its leg's travel (a support's overlap is its leg's give) at a 10 m/s skid-first impact, on the plane as on the slope: a p7g-legs question, not the terrain's (suggested).

**A prop's strike** (the tipped landing): the 0° terrain 0 passes → 144 (the plane's 144, 10.77 J); 20°, 0 → 379.

**The steps:**

| Row | Before | After |
|---|---|---|
| Over the ledge's lip (1.504 m): tips over and falls it | 385 N on a leg, 832 m/s², a crash, no tunnel | 353 N, 900 m/s², a crash, no tunnel |
| Into the far seam's face (73.8 mm), nose first at 1 m/s | 385 N on a leg, 155 m/s², rose 1.7 mm onto the step | 51 N, 138 m/s², 0.1 mm |

- The fall from the ledge is a crash on both cores: 1.5 m onto the far ring, 5.4 m/s.
- At the seam the skids' front supports now meet the face, their legs taking it across their tubes. Before, a support past the face read the step's 74 mm top as its overlap.

## Golden files, audited case by case
- **`flightbench --golden`:** all 6 cases and every kept audit pass unchanged (60 PASS lines). Every flight there is on a plane (the floor, the test range):
  - its supports read their samples (n_y 1: the same bits)
  - no skid leaves the ground out, and no blade meets a terrain
  - no leg's anchor slides along a nil stiffness
  - no certified segment met a shape within KeepGap (the tether golden flies the floor, which has none)
- **`worldbench --golden`:** all 37 cases pass unchanged (no world code changed).
- **The suite's pins** (`GoldenFlightTests`: powered, rain, contact, range, tether, tumble and every model's audit): unchanged. Nothing was re-recorded.
- **The model-79 flights** (L1's 35, 37 and 38) replay bit for bit on this core: 234,901, 18,147 and 66,941 step records (`flightbench --replay-step`, from a build whose model refusal was set to 79, temporary and not committed). The certificate and the nil rule changed none of their answers.
- **So model 82 is for the flights that differ:** any flight on a terrain, and a flight on a plane where a shape came within KeepGap of a certified segment, or a leg slid along a nil stiffness. `LogFormat.ModelVersion`'s note lists them.

## For the next units (suggested, not this task's)
- **p7g-legs:** the anchor's slide past the sway load (3), and a support's 17–19 cm travel in a 10 m/s skid-first crash, on the plane as on the slope.
- **The fiber:** a node where a laid segment dips under a rise (5, 8: up to 3.24 m on the farmland line), and the step walls in its segments' queries for the lip's capstan (4).
- **The world:** the fields' furrows as relief in the package and the drawn terrain, from the references (6).
- **QA (4.1):** a quiet run of `farmland-line-step` and of 2.1's replays; this task's timings are a shared machine's.
