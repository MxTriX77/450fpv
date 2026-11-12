# Design: w3-farmland-ground, tasks 1.1 (the heightfield), 1.2 (the farmland's objects), 2.1 (the fiber's query cost), 2.2 (the flight on hills), 2.5 (a laid fiber follows the hills), 3.1 (flight on the farmland), 3.2 (the drawn terrain near the drone) and 2.7 (contact with the farmland's objects)

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
- **So PHY-04's furrows (rocking across them, not along) stay owed,** to that unit; its slopes are met here.

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

# Task 2.5: a laid fiber follows the hills

**Owner of this part:** physics-engineer · **For:** 2.3, 2.4, 3.1, 4.1 (qa-engineer) · **Requirements:** PHY-18, PLT-04, WLD-11, WLD-16 · **Model:** 82, extended (below) · **QueryVersion:** 8, unchanged answers (the API gains a filter)

Numbers are labelled as above. "Before" is this branch's head (9847c69e) with 2.5's pass switched off by a temporary flag (not committed). It flies the head's flight to the bit: 59,933 whole queries and the same chain at 200 s. "After" is this task's code.

## The cause (2.2's §8, measured again on the head)
- **The chords between laid nodes.** A node takes the ground from SampleGround, and a segment's queries leave the ground out (2.1). So a straight segment between two laid nodes passes under any rise between them.
- **The far line is coarse on purpose:** the spacing law, and on a full chain `MakeRoom`, merge the far pair that overshoots its target least. A node lying on a crest goes as easily as any (its merge lowers the energy), and its neighbours' chord then runs through the hill.
- **On the farmland line** the chain is full (32) from about 40 s, its far segments 50–380 m long.
  - At the head, 6 of 31 segments lie more than 1 cm under the ground at the end, the deepest 4.35 m.
  - 2.2 measured 5 and 3.24 m. That was before the merge brought W2's re-exported classes, which changed the flight.
- **What it gets wrong:** over a rise, the chord sets three things the ground should: the line's turn, its pressure and friction there, and what it meets standing on the crest (stems, a bush, a ruin's rubble, with 1.2). The renderer draws the chain, so the line also vanishes into the hill.

## Decisions

### 1. `QueryFilter.GroundOnly` (the API, signed off here)
- **What it does:** `StaticContacts` and `Raycast` answer the ground alone: its plane or its terrain, a step's faces included, and no placed or runtime shape.
  - `MapWorld` and `FloorWorld` honour it. The test worlds forward to their floor.
- **The same answers:** the ground's contact is the unfiltered query's to the bit. A ray's entry is the unfiltered one's where that ray meets the ground first.
  - Tested: `MapWorldTests.GroundOnly_IsTheGroundsOwnContactAndEntry`, 300 seeded reaches and rays on the test range.
- **Why a filter and not 2.2's "walls alone":** the ground is one shape, its faces included, and the test (2) needs the facets as much as the faces. Without the filter, a ray along a laid segment also walks the object grid's whole box (a 400 m diagonal: 40,000 cells).
- No existing call changes its answers, so `QueryVersion` stays 8 (as 2.1's `NoGround`).

### 2. The test of a segment against the ground (`FiberLine.GroundTest`)
- **The test:** the chord, raised by `CrestGap`, is cast as a ground-only ray from an end above the ground (a ray from inside the ground finds nothing). If the ray enters the ground, the chord passes more than `CrestGap` under it, vertically, somewhere.
- **`CrestGap` is `KeepGap`, 0.1 m** (assumed, from the members' rule): the reach to which a member on a slot keeps its node. A chord over a crest so stays within it of the ground, and of what stands there.
- **The certificate:** a test that finds nothing lets the segment's ends move `CrestSlack` from the tested core before the next test. `CrestSlack` is `KeepGap` (numerical: it sets the tests' cost).
  - A point's depth in the ground (its distance from the surface) moves no more than the point.
  - Every point of a segment whose ends moved δ moved at most δ.
  - A chord no more than `CrestGap` under the ground vertically is no deeper than that.
  - So between tests no segment lies deeper than **`CrestDepth` = 0.2 m** (`FiberLine.CrestDepth`). That is the tolerance this task states.
- **An airborne segment** (both ends higher than `contact.clearance_margin_m` over their ground) first asks `StaticContacts` of the ground alone, at that margin.
  - Finding nothing, it holds until an end moves the margin: it cannot reach the ground before.
  - That costs one pass over its footprint's cells (1.25 µs a call, measured on the farmland line), not a ray along it every 0.1 m of the hanging span's travel.
- **Not tested:** the exit's segment (the spool's, within near_spacing_m of the drone), and a segment shorter than 2·`CrestGap` (no point of it lies farther than that from an end).
- **The certificate rides on the node** (`CableNode.GroundClear`, its core and its slack). A split's new node takes its segment's, as `Cleared` and `Near` do (2.1).

### 3. The crest's node (`FiberLine.GroundTest`, `CableChain.SplitCrest`)
- **Where:** a second ray, from the other end, gives the stretch the raised chord lies in the ground.
  - At `CrestSamples` (9, numerical) points across it, the depth in the ground comes from the ground's own contact with the point (StaticContacts of a point, ground alone).
  - The deepest point that leaves a piece of more than `CrestGap` either side takes the node.
- **On the ground:** the node goes to that point's nearest point on the surface, plus the line's radius along its normal.
  - Under a crest, that is the ground above it.
  - Beside a step's face, it is the face, so the line climbs a face to the lip by nodes and never jumps it.
  - A first version put the node vertically above the point. At the ledge that lifted a taut segment 0.5 m onto the top in one step, and the line whipped (a node at 12.8 m/s against 3.5 m/s before).
- **A stretch beside a face only** takes no node: a chord past the step's corner, under the top's level, with no point deeper than `CrestGap`.
  - It is tested again after the slack its sampled depth leaves: `CrestDepth` less the deeper of two bounds.
  - Outside the stretch the bound is `CrestGap`. Inside it, the deepest point plus half the points' spacing.
- **The line's material:** the segment's material is shared by the two pieces' **taut paths over the ground**, the upper hull of 32 ground samples under each piece's chord (numerical).
  - Each piece then holds at least the line its later crests can lift it to, and the strain is the same on both sides.
  - Sharing by the chord's fraction (a member's node's rule) or by the pieces' straight lengths left a piece still under a crest short. On the ridge (the rows) the second lift stretched it 0.5 %, 10.8 N at step 3, and the line broke at the crest (measured with each).
- **Momentum is kept:** `Split` at that share puts the node's velocity at the share (the masses follow the material), and only its position moves.
- **The lift's energy** (the line's weight raised onto the crest, and any stretch) enters before the step, as a member's node's does. It is the lumping's correction, not a force: the line lay on the crest all along.

### 4. Holding a crest (`FiberLine.Crests`, `CableNode.Crest`)
- **A crest's node is never merged away** (`CableChain.Removable`).
  - Without this, the law merged a new node the next step and the test put it back: 1,000 insertions a second on the first build.
  - That build also released a node not yet pressing the ground. A node now holds while it lies within `CrestGap` of its slot's plane.
- **A node is a crest's** while it lies on the ground (within `CrestGap` of its slot's plane) and more than `CrestKeep` over the chord between its neighbours. `CrestKeep` is `InsertGap`, half `CrestGap`: the hysteresis.
  - A laid node already on a crest becomes one past `CrestGap`, so `MakeRoom` takes a node elsewhere rather than cutting the hill.
  - Node 0, anchored at the trail's end, is never one.
- **The room:** a chain on a terrain holds `FiberLine.CrestNodes` = 16 (assumed) crests' nodes besides `span.max_nodes`.
  - The spacing law's splits, a member's contact node (`SplitAt`) and a trail point lifted back count against the law's own 32, crests left out (`CableChain.SpacingCapacity`, `SpacingFull`).
  - So the line at the drone keeps its resolution.
- **With the room spent,** a crest more than `CrestYield` (2, numerical) times as deep as the shallowest held takes that one's room, so the deepest crests keep it. A refused crest is counted (`CrestsRefused`) and tested again once its ends move.
- **When it runs:** after each step, after the remeshing, so no step starts on (and no pose shows) a chord through the ground. At the launch the tests run until they put no node in.

### 5. The ledge's lip
- **The same rule wraps it.** A chord from the far ring up onto the top cuts the step's corner, and the test finds it. The node goes to the face or the top nearest the corner, and the line then runs over the lip on a node there.
- **No capstan.** The ground is soil, lain on and held by Coulomb's friction at the node, as a trench's soil layers are (`FiberMember.Ground` keeps its rule). A capstan of the lip would let line slide through the node, which the ground's other nodes never do.
- **The bend at the lip** is unchanged: the soil's `lip_edge_radius_m` (0.02 m) gives 0.23 GPa, under the 0.69 GPa proof stress (2.2's §4).

### 6. What a plane's flight keeps
- **The pass runs only on a terrain** (`Geometry(Ground, 0)` false), and a chain on a plane has no crests' room. Its `Capacity` is the table's 32, as before, and every "full" test reads the same.
- **The arrays sized by the nodes** (`FiberContacts`' lists and near slots, `FiberLine`'s batch) hold 48 always. A larger array changes no answer: the first free near slot is the same one.
- **The pose and the bends:** `FiberNodes.Capacity` (the pose the renderer reads) is 48, and `FiberCuts.MaxBends` is 104 (two members a node of 48, and the lip). A pose kept only the first 32 nodes from the far end, which would have dropped the exit's.

### 7. The farmland line's stop (`flightbench --bench farmland-line-step`)
- **The route ended with a step from 12 m/s to a hold**: the scripted pilot's velocity hold was switched off at the last point.
  - On every core the line's lead then wrapped the exit's lip by 165–172° (the lip angle each 0.1 s from 145.6 s). That is 1.8–2.3 GPa of bend stress at 0.07–0.10 N.
  - The head survived it. 2.5's line broke there at 145.94 s, on a 0.34 N blip of the exit's tension, with no crest event within 0.15 s.
  - With 8 crests' nodes of room instead of 16, the same core survived. So the outcome was chance, not 2.5's physics.
- **The route now slows over its last 30 m** at 2.4 m/s² to the hover, as a pilot stops. Both cores fly it whole.
- **The case also reports the line's depth in the ground:**
  - the segments deeper than `CrestDepth` at the end, which must be none for its PASS
  - the deepest at the end, and at the flight's whole seconds
  - 2.2's vertical measure, for comparison
  - the crests' nodes, refusals and tests

## The rows (`flightbench --slopes`, `SlopeTests`)

**A line laid over a ridge** (`SlopeScenarios.LineOverRidge`, `ALineLaidOverARidge_LiesOverItsCrest_AndReplays`):
- **The ridge:** a package tile of meadow_sod with a 3 m crest, 16.7° a side.
- **The line:** laid over it along the ground every metre, its anchor at the ridge's far foot, the drone 48 m from the crest. The drone climbs, flies away at 2 m/s and hovers, for 10 s.

| Measure | Result |
|---|---|
| The spacing law's chords at the launch | 2.685 m under the crest (the line before 2.5) |
| The deepest segment in the ground at the launch, with its crests' nodes | 0.058 m |
| The deepest at any 10th step's end over the flight | 0.075 m |
| The node nearest the crest line | 0.083 m off it, 0.09 mm over the ground, a crest's |
| Crests' nodes | 6 put in, 0 refused |
| The line | 8.0 m paid out, whole, 0 TetherWedges, 0 B after the first step |
| Its log | replays bit for bit, 10,000 steps (`FlightReplay`) |

**The line over the ledge** (2.2's row; `ALineDraggedOverTheLedge_WrapsItsLip`): laid on the far ring and dragged up the 1.5 m face and over the lip.

| Measure | Result |
|---|---|
| The chords between the spacing law's nodes (its crests' left out) | up to 0.520 m in the ground: the step's corner they would cut |
| The deepest segment of the line | 0.105 m |
| A crest's node within `CrestDepth` of the lip's edge | 3,319 steps |
| Crests' nodes put in | 2 |
| The fastest node | 3.50 m/s (2.2: 3.5) |
| The line | 24.3 m paid out, whole |

- **The first builds broke both lines.** They put a node vertically above the deepest point and shared the material by the chord's fraction. They broke the ledge's line by tension (a node at 44.7 m/s) and the ridge's at its crest. That is how 3's placement and share were chosen.
- **The rest of `--slopes` is 2.2's to the printed digit:** the holds, the landings, the crashes, the strikes and the steps. The legs meet no crest code.

## The farmland line (`flightbench --bench farmland-line-step`)

**The runs** (measured; both with the new stop; AC, Balanced plan, best power efficiency mode, 3201 MHz; the CPU load as each began, by `Win32_Processor.LoadPercentage`). Other sessions shared the machine throughout, and each run's own maxima reach 4–24 ms of preemption.

| | Before (2.5 off) | After, A/B pair | After, the committed build |
|---|---|---|---|
| Load at start | 62 % | 68 % | 62 % |
| The line | 1,983.9 m out, 32 nodes, whole | 1,980.1 m, 43 nodes (11 crests'), whole | the same flight to the counts |
| Segments deeper than 0.2 m at the end | 5 of 31 | 0 of 42 | 0 of 42 |
| The deepest at the end, and at whole seconds | 4.100 m, 4.776 m | 0.096 m, 0.110 m | 0.096 m, 0.110 m |
| Vertically more than 1 cm under it (2.2's measure) | 6, the deepest 4.115 m | 13, the deepest 0.092 m | the same |
| Crests' nodes put in / refused; ground tests | – | 387 / 0; 72,396 | 387 / 0; 72,396 |
| Whole shape queries | 59,597 | 65,695 | 65,695 |
| Step p99, all after the first | 371.0 µs | 392.5 µs | 351.3 µs |
| The route p50 / p99 | 157.5 / 367.2 µs | 154.4 / 387.1 µs | 159.1 / 346.6 µs |
| The hover p50 / p99 | 199.2 / 395.9 µs | 199.0 / 412.9 µs | 194.1 / 375.2 µs |
| World queries in all p50 / p99 | 20.2 / 57.1 µs | 21.7 / 84.1 µs | 21.1 / 68.3 µs |
| Allocated after the first step | 0 B | 0 B | 0 B |

- **The line lies over the ground:** no segment deeper than `CrestDepth` (0.2 m) at the end or at any whole second, the deepest 0.110 m, and 0 refused crests. The tolerance is 3D depth; vertically the deepest is 0.092 m.
- **Its cost (loaded, so only the pairs' differences mean anything):**
  - The step's p50 is unchanged.
  - Its p99 rises 21 µs between the A/B pair, the run before at 62 % load and the run after at 68 %.
  - The world queries' p99 rises 11–27 µs, over D-061's 60 µs line: the ground tests' rays, about 0.15 µs a metre along the line (13–15 µs on the line's 100–370 m far segments), and their re-tests while the laid line slides.
- **PLT-04 is not met on this machine by either core** (p99 351–413 µs). 2.2 measured the same case's step at 266–287 µs at 51–54 % load. Today's loads ran 62–68 %, and the case's stop is new. A quiet run is QA's (4.1), as 2.1's and 2.2's are.
- **Where the time goes** (measured with temporary phase timers on an earlier build of 2.5, not committed; µs a step over the whole flight, after against before):

  | Phase | After | Before |
  |---|---|---|
  | `FiberContacts.Prepare` | 50.6 | 46.5 |
  | the loads | 15.8 | 13.5 |
  | the chain's step | 17.4 | 14.8 |
  | the cuts | 5.1 | 4.1 |
  | 2.5's pass | 6.9 | – |

  - The crests' nodes cost about 2 µs each a step, as any node does. The far line carries 10–15 of them over the farmland's hills.
  - That build's rays, 81,359 of them, cost 13 µs each: 5 µs a step. `CrestSlack` at `KeepGap` halved that (72,396 tests now, most of them the airborne ones at 1.25 µs).
- **What drives it:** the far line never freezes into the trail, because in Severe wind its laid nodes slide at 0.04–4.7 m/s.
  - The wind field's profile stands on the ground under the drone, so a node on a hill 20 m higher sees the wind 20 m up (W7's wind over hills).
  - With the near-ground wind right the far line would rest, freeze with its crests' nodes into the trail, and neither cost nor be tested.

## No other answer changed (the evidence)
- **`flightbench --golden`:** all 6 cases and every kept audit pass unchanged (60 PASS lines). Every golden flight is on a plane, where 2.5 does nothing.
- **`worldbench --golden`:** floor 5, test range 14 and wakes 11 pass unchanged.
  - The farmland's content hash, probes, ground, contacts and rays fail. The same 5 cases fail with the same hashes on the head (9847c69e, run in a temporary worktree).
  - The merge (4866ce95) brought W2's re-exported package (classes and map.json) without re-recording `golden/farmland.txt`. That is the world's to re-record (1.2), not 2.5's: the filter changes no existing call's answer.
- **The pilot's three model-79 flights replay bit for bit on this core:** 18,147, 66,941 and 234,901 step records (flights 37, 38 and 35).
  - Run as `flightbench --replay-step` from a build whose model was set to 79, temporary and not committed.
  - They fly the test range, a plane, where the crests' pass never runs.
- **`dotnet test tools/simtests -c Release`:** 808 of 809 pass. The one failure is `FarmlandTests.Queries_AllocateNothingAfterTheFirst`, its 24 B in Release since 1.1 (tasks 1.2 lists it).
- **Nothing was re-recorded.**

## The model
- **82 is kept and extended** (`LogFormat.ModelVersion`'s note):
  - 82 is this change's own number, not merged to `unity/main`, and its definition already covers "any flight on a terrain".
  - A flight on a plane steps as 82 did.
  - A flight on a terrain differs from 2.2's 82. No such log is kept: the tests record and replay their own.
- **For the orchestrator (D-072):** the `--w3-flight` and `--w3-blade` branches are at 82 without 2.5. A farmland log recorded there replays bit for bit only on their core. Assign a new number if one is kept.

## For the next units (suggested, not this task's)
- **W7, the wind over hills:** the profile's ground at each node rather than under the drone. The far laid line would then rest and freeze, which is this task's cost driver and the far line's slide (0.04–4.7 m/s in the farmland line's Severe wind).
- **The world, `Terrain.Ray`:** a ray costs about 0.15 µs a metre on the farmland (two divisions a cell's fractions, four a facet). An incremental walk would cut the ground tests' share of the world queries' p99.
- **The world, `golden/farmland.txt`:** re-record after the merged package, audited (1.2 or QA).
- **The tether, a review:** in the farmland line's last leg the lead leaves the nozzle at 128–136° in steady flight and wraps the lip 165–172° at a stop, 1.8–2.3 GPa at 0.1 N, on both cores. Whether a light line in Severe wind sits so far round the nozzle is for the tether's review against the pilot's account (the sharp reverse).
- **`FiberContacts.Prepare`** costs 46 µs a step on the farmland line with no objects at all (the phase timers above): the caches' refresh and the per-segment walks. That is worth a look with 2.3.
- **QA (4.1):** a quiet run of `farmland-line-step`, with 2.1's and 2.2's owed ones. The Unity EditMode suite should also see the pose's 48 fiber nodes (`FiberView` sizes its rings by `FiberNodes.Capacity`).

# Task 1.2: the farmland's objects

**Owner of this part:** world-builder · **For:** 2.3, 2.4 (physics-engineer), 3.1 (game-developer), 4.1 (qa-engineer) · **Requirements:** WLD-02, WLD-05, WLD-11, WLD-16, PLT-04 · **Decisions:** D-088, D-089, D-091, D-097 · **QueryVersion:** 8, unchanged (no answer of the floor or the test range moved; the farmland's moved with its content, its hash)

Numbers are labelled as above: **measured**, **derived**, **assumed**.

## Context
- **The rows** are W2's `maps/farmland/placements.json` (`fpv450-map-placements` v1, listed in `map.json` with its SHA-256): 1,679 rows of `kind`, `variant`, `position_m`, Unity's `rotation_xyzw` and a per-axis `scale`. 294 trees and 1,352 bushes, yaw only, scale (w, h, w) with w 0.465–1.599 and h/w 0.926–1.087; 5 ruins, yaw only, one at scale (−1, 1, 1); 28 debris pieces, tilted with the ground up to 24.9° (measured).
- **The metadata** is W4a's (`vegetation/VF01`–`VF10`, 22 variants: capsules, twig-shell spheres, wind boxes, snag records) and W4b's (`ruins/N01`, `N02`, 13 variants: boxes, gaps, wind boxes, snag records), each in its prefab's frame.

## Decisions

### 1. The loader reads the package's own rows
- `MapWorld.Load(root, id)` on a package reads `placements.json` too, checked against `map.json`'s SHA-256 (`Terrain.Checked`, as the tiles). 1.1's in-memory "no objects" record is gone.
- **The variant's file:** a row names a variant only. `MapWorld.VendorAssets` maps it to `<class>/<asset>` from `assets-src/vendor_plants.json` (W4a) and `assets-src/vendor.json` (W4b). A variant listed twice is refused.
- **The object grid** stays on `map_rect_m` (1000 m, 500 × 500 cells of 2 m), with no ground record (the terrain is the ground) and no start points.
- **The content hash** runs over `placements.json`, `map.json`, every metadata file read (by path, as on the test range) and the material names: `0x7249f5a8adb997d7` (measured).

### 2. A row's pose is Unity's TRS
- **world = position + R·(scale ⊙ local)**, R the row's quaternion normalised. + − × ÷ √ only: `Quat.Rotate`'s products, and for a scaled capsule one normalisation of its axis and one of its X axis.
- **An unscaled row** (the ruins, the debris, the test range) places each shape exactly as before (`Turned`): the test range's rows are the same operations on the same numbers, so its every answer keeps its bits (the goldens, below).
- **A scaled row** (the trees and bushes, `Scaled`):
  - a sphere's centre and a capsule's two core ends are scaled per axis and placed, so its axis is the scaled one; its X axis is the scaled local X made square to it (one Gram–Schmidt step) and its Z axis X × Y;
  - the radius is scaled by w (the scale's x, which must be its z), as the brief sets it. So the shape lies within radius · |h − w| of the exactly scaled one (an ellipsoid's caps and cross-section): at most **141 mm** on a 1.5 m twig-shell sphere (Pine004_1_Dry at h/w 1.087) and **60 mm** on the wood (measured, `FarmlandObjectTests`);
  - a box takes a uniform scale only (none is scaled on the farmland); a scaled row may carry no wires, ports, micro elements or features (none does). Each is refused at load, so no approximation goes unnamed.
- **The mirrored house:** a row at scale x < 0 takes its variant's `_mirrored` twin (`Ruin_Walls_mirrored`), whose metadata carries the mirror (W4b design §2), at scale (|x|, y, z). `ObjectName` is the twin's.
- **Wind volumes:** a scaled row's boxes are scaled per axis (centre and size), then placed with its yaw (`2·atan2(q_y, q_w)`, DetMath's, load time only). A tilted row may carry none (no debris does). 2,589 volumes: 1,875 the plants', 714 the ruins' (measured).
- **Gaps:** the core reads a variant's gaps only for its level openings' centres, which the test range's cut records use. They are placed by the same TRS. The farmland has no hole records, so none is used. **There is no gap query in the core** (K§10.4's `GapsNear` was never ported: `IWorldQuery`'s header). The ruins' wall openings are clear space in their boxes, which `StaticContacts` and `Raycast` already see.

### 3. Snag records: placed by the whole pose, indexed by segment
- **The index** is the test range's: per variant, its segments (not its records) in a 1 m grid in its own frame, built once; per map, each placement's world bounds in the 2 m grid. A query takes the sphere into the placement's frame and tests only the segments listed in the cells its box meets, in canonical order. The village's 10,089 debris rim segments are indexed one by one, so a rim of 710 points costs its few segments near the sphere.
- **A scaled or tilted placement** (`Pose.PlaceAny`, `LocalAny`): the points by the row's whole TRS, `radius_m` and `edge_radius_m` by w, as the capsules' radii (`barb_pitch_m` as recorded: no scaled row has one).
  - **Exactly the world-space test.** Along local axis i, the sphere and a record's grown radius reach (r + ρ·w)/s_i from the centre, and the segment's box is grown by ρ in its own frame, so the query's box there is r/s_i + ρ_max·max(0, w/s_i − 1) (`NearSide`, derived), and the placement's world bounds grow by ρ_max·(w − min s_i). No segment within reach is skipped (tested, below).
  - **Upright, unscaled placements** (the ruins, the test range) keep the old four-term pose, to the bit.
- **Not simplified.** The debris rims stay the 1 cm stairsteps W4b wrote: the world answers with the metadata's own segments, and a Douglas–Peucker pass belongs in W4b's generator, where the metadata, its review sheets and the D-089 test see it. Indexing the segments is what the cost needs (Costs: a 2 m cache query in the village takes 17 µs at the median, 83 µs at p99).

### 4. What the next units should know
- **The fiber caches can overflow among the bushes** (measured, a 0.5 m lattice over every object's bounds, so lower bounds): the drone's 2 m cache meets up to **4,664 segments** (DeadBush_6 beside GreenBush_Dry at (515, 851); DeadBush_4 4,380; LeafyBush_P2_Dry 3,944) against `tether.json`'s `snag_capacity` of 4,096. A line node's 1 m cache meets at most 2,389. An overflow is a `FiberSnagsFull` world bug with the canonical prefix kept. The map compiler's cap (2,048 in any 2 m cube) never ran on W2's export. For 2.4 or W4a: thin the bushes' hooks, or size the cache.
- **The radius by w** (2) puts a twig shell up to 141 mm from its exactly scaled ellipsoid, and wood up to 60 mm.
- **Memory:** 258,904 shapes in world space (51,612 spheres, 187,121 capsules, 20,171 boxes) as 280-byte prims, about 72 MB (derived); the heap grows by **130 MiB** over the load, terrain included (measured, a process of its own; the bench's process held 186 MiB after it). The load takes **1.65–1.78 s** (measured), against 1.1's 0.24–0.38 s: parsing the 35 metadata files and building 675 objects' trees.

## Evidence

### The pose: every shape where Unity's TRS puts it (`FarmlandObjectTests`, Release and Debug)
- **Parity.** Every row recomputed independently: the files read by `TestJson`, the asset from the vendor lists, R as the quaternion's rotation matrix (not `Quat.Rotate`). Every sphere centre, capsule core end and box centre within 1.6e-13 m, every box axis within 1.5e-15, every radius and size within 1e-9.
- **The mirror.** `Ruin_Walls_mirrored`'s bounds placed at scale 1 equal `Ruin_Walls`' at Unity's (−1, 1, 1) to under 0.5 µm (printed 0.000 mm).
- **SnagsNear** over 240 spheres of 0.05–3 m at the village, the F6|F7 belt, a grove and a bush cluster returns, in canonical order, exactly the segments the world-space test over every placed segment returns: 15,907 segments, 15,301 of scaled rows and 605 of tilted ones.
- **StaticContacts** over 300 reaches (100 of them 0.5–20 m long) there gives, to the bit, every shape's own `ShapeContact` (1,473 contacts), and `NoGround` leaves only the terrain out.
- **Raycast:** a ray down over each of the 294 trees' pivots meets its own tree before the ground, batched as one by one.
- **Wind:** every plant's box at its scaled centre and size within 2.3e-13 m.

### The drawn prefab against the loaded shapes (measured; scratch tools, nothing of the store pushed)
- **The drawn vertices:** 73 rows (the village's 33, the F6|F7 belt's, a grove's, and one row of each of the 35 variants), 306,750 LOD0 vertices read from the local store by W4a's and W4b's own readers and placed by Unity's TRS (the row's own scale, the mirrored house as `Ruin_Walls` at x = −1).
- **Ruins and debris** (unscaled; the debris tilted up to 24.9°, the mirrored house): each vertex's signed distance to its row's loaded shapes equals its distance, in the prefab's frame, to the metadata alone (the loader at the origin): the same to under 1 nm, for every vertex.
- **Trees and bushes:** |d_world − w·d_local| stays under (|d_local| + 1.5 m)·|h − w| at every vertex: at most 134 mm (IslandTree_Dry, w 0.936, h 1.013), mostly 5–60 mm. That is the scale's non-uniformity and the radius by w, not a misplaced shape.
- **Looked at** (`village-plan`, a scaled IslandTree from the side, a door at 24.9° and a roof sheet at 23.4°, rendered from the same data): the drawn vertices sit on the boxes' outlines in all five houses and the debris, the mirrored house lies as Unity draws it, and the tree's trunk and crown fill its capsules and shell.

### Golden files, audited case by case
- **floor, test range, wakes:** all 30 cases pass unchanged, in Release and Debug. So `QueryVersion` stays 8.
- **farmland.txt was already stale before 1.2.** W2's re-export (`c068e854`, the gravel track rule: `classes/map.raw` and `map.json`) reached this line after 1.1 recorded it. This branch's base, built from `git archive` and run, fails `content_hash`, `farmland.probes`, `farmland.ground`, `farmland.contacts` and `farmland.rays` too.
  - `farmland.probes` and `farmland.ground`: the base's values exactly (`4a1198988a929fae`, `a9e87023a54b9bff`); the 1,000 heights are validate.py's to the bit. Only some probes' surface moved, with the classes.
  - `farmland.steps`: unchanged.
  - `farmland.contacts` (per capsule, the base against 1.2): 9,847 of 10,000 identical; the other 153 only gained object contacts, their ground contact the same bits.
  - `farmland.rays`: 9,761 identical; the other 239 now meet an object no farther than the ground before.
  - `content_hash`: the classes, the placements and the 35 metadata files.
- **New:** `farmland.objects` (every placed shape's geometry and every object's name), `farmland.wind`, and 10,000 each of `farmland.object_contacts`, `farmland.object_rays` (batched against one by one) and `farmland.snags` among the objects.
- **flightbench --golden:** all 6 cases and every kept audit pass unchanged (60 PASS lines). Its flights fly the floor and the test range.

### 0 B, and the 24 B since 1.1
- **The 24 B was the test's.** `Queries_AllocateNothingAfterTheFirst` read the counter, ran nothing (0 iterations), and still reported 24 B; four reads in a row reported 0, 0, 0; the same 100 steps checked by `if (allocated != 0) Assert.Fail(...)` report 0. Release's JIT built `Is.EqualTo(0)`'s boxed literal ahead of the last read. The test now builds no constraint before it fails.
- **Every query is allocation-free among the objects:** that test's 100 steps of 44 samples, 8 rays, 46 swept capsules and a SnagsNear in the village; the bench's rows (0 B each); the concurrency test (4 threads × 3 rounds, now with SnagsNear and the object contacts in the hash).

## Costs
**The run** (measured, `dotnet run -c Release --project tools/worldbench -- --bench`, 2026-11-12 05:55):
- **The power:** "AC, Balanced plan, best power efficiency mode, CPU 3201/3201 MHz" (PLT-07's line).
- **Loaded:** other sessions held the CPU at 39 % when it began and 44 % when it ended (`Win32_Processor.LoadPercentage`). The p99s and maxima carry their preemptions; a quiet run is QA's.
- **The judgement** is 1.1's (D-054): physics' pattern inside PLT-04's 300 µs step at p99 with 0 B, PLT-04's 60 µs line printed as the target (D-061).

**Physics' pattern** along 1.1's five paths and three new ones through the objects, 20,000 steps each, all 0 B:

| Path | Median | p99 | Max | 60 µs target |
|---|---|---|---|---|
| Low over the village's plots, 0.3–1.5 m up (1.1's row: it now flies through two ruins' walls) | 10.3 µs | 358.4 µs | 1921.0 µs | missed, **over the step's 300 µs** |
| Landed on the village's steepest plot | 27.8 µs | 59.3 µs | 506.9 µs | met |
| Skimming a field at 0.3 m, 15 m/s (now past its bushes) | 18.6 µs | 75.5 µs | 710.1 µs | missed |
| Along the ledge's face | 10.8 µs | 36.2 µs | 582.2 µs | met |
| Over the far ring at 1–3 m | 7.7 µs | 20.6 µs | 169.9 µs | met |
| **Through the village's ruins and debris**, 0.3–2.5 m up, 10–20 m/s | 10.2 µs | 152.1 µs | 1104.4 µs | missed |
| **Through the F6\|F7 belt's crowns and bushes**, 1–9 m up | 9.7 µs | 147.4 µs | 661.2 µs | missed |
| **Through a bush cluster**, 0.2–1.5 m up | 9.9 µs | 167.4 µs | 679.5 µs | missed |

- **K§10.5's batch** (shown), median / p99: the village's plots 161.5 / 856.2 µs, the ruins 31.5 / 222.1, the belt 27.6 / 173.6, the bushes 24.0 / 195.4, the field 24.9 / 110.8, the ledge 25.3 / 56.2, the far ring 22.3 / 47.0.
- **K§10.4's budgets:** capsules 69.3 ms per 100,000 in physics' pattern (budget 100), rays 33.9 ms (budget 100), samples 8.92 ms (budget 10); at random poses, shown: capsules 155.7 ms, rays 54.2 ms.
- **The bench's last line:** "farmland 1 over budget or allocating", the village's plots row. The test range's is MicroDetailNear over S04's straw again (405 µs against 250 µs), as in 1.1's runs.

**Why the objects' rows miss the target** (measured, a scratch profile of the plots row's parts and disc spheres alone: p99 132 µs, 118 µs without the ground):
- In its slowest 1 % of steps the drone's parts lie inside the ruins' walls and floors: 10.8 boxes' bounds meet a part's reach (up to 60), and a part keeps 4.9 contacts.
- So the time is the sweeps of W4b's dense boxes (7,016 in Ruin_RoofCollapsed, 2 cm skins), not the index: a part's query costs 0.5 µs at the median.
- A drone stops at a wall (a crash latches it), so a path through the walls is the pattern's worst case, not a flight. The rows through the ruins' rooms, the belt and the bushes pass the step's 300 µs; the 60 µs line is not met there on this machine. 2.3 owns the step's cost.

**The fiber's laid segment** (`farmland.fiber`, `QueryFilter.NoGround` as the fiber's own queries, shown), median / max a query and the objects' shapes within its 0.5 m margin: 0.5 m 0.20 / 4.6 µs (0.17 shapes); 2 m 0.20 / 4.2 µs (0.28); 10 m 0.50 / 18.3 µs (1.00); 50 m 3.70 / 102.8 µs (3.07); 200 m 36.2 / 218.8 µs (10.3). 1.1's rows paid the terrain: 3.4–179 µs medians.

**SnagsNear at the fiber caches' radii** (shown: it runs at their refresh, off the step), median / p99 / max, segments a query on average and at most, all 0 B:

| Spot | r = 2 m (the drone's cache) | r = 1 m (a line node's) |
|---|---|---|
| The village's ruins and debris | 16.9 / 83.3 / 243.3 µs; 269, 1,516 | 5.1 / 24.1 / 248.5 µs; 70, 486 |
| The F6\|F7 belt | 20.2 / 85.6 / 1067.8 µs; 215, 743 | 5.3 / 25.6 / 289.1 µs; 47, 236 |
| A bush cluster | 33.6 / 143.2 / 325.0 µs; 728, 2,930 | 13.7 / 84.3 / 518.5 µs; 215, 1,546 |

The test range's SnagsNear budget (100 µs at p99, r = 1 m) is met at every spot.

**The 1.5 km line through the objects** (`flightbench --bench farmland-line-step`, 2.1's and 2.2's case, its world now with the objects) **FAILS** (measured, 05:58–06:01, loaded: 68 % CPU when it began, 37 % when it ended):
- **The line tears while the drone flies along the F6|F7 belt.** At step 61,981 (62 s, 690 m paid out) it broke by tension at node 2, near its laid end (segment 1, at (504, 25, 697): 21.1 N, 1.72 GPa against its 1.29 GPa), the drone 360 m away at (145, 51, 655). The drone crashed 3.2 s later at 21.8 m/s; its cause is not traced here.
- **The line rests on the belt's crowns and bushes as it is laid:** its one-shape checks climb from 0 at 35 s to 5,290 at 40 s, 168,836 at 55 s and 304,470 at 60 s, as the drone turns west along the belt (a diagnostic print of the same seeded flight, not committed). In all, 289,876 whole queries (2.2: 59,813) and 1,787,326 one-shape checks.
- **The step:** p99 10.3 ms after the first; the climb p50 112.9 / p99 301.2 µs; the route p50 221.5 µs, p99 11.5 ms, max 23.7 ms; the hover p50 280.9 / p99 658.8 µs. World queries: the climb p99 51.9 µs; in all p50 151.7 µs, p99 10.2 ms, 124.6 calls a step. 0 B after the first step.
- So 2.1's scenario through a belt and the village is not met with the objects: the line tears where it lies on the trees, and the steps after the tear and the crash run over 30 times PLT-04's 300 µs at p99. That is the fiber's contact with the farmland's objects (2.4) and the step's cost (2.3); this task changes no contact model.

## For the next units
- **2.4:** the farmland line tearing on the F6|F7 belt (Costs), the drone's snag cache among the bushes (4), and W4a's pairs a prop's reach meets in a crown.
- **2.3:** the step inside the ruins (Costs: 10.8 boxes and 4.9 contacts a part in its slowest 1 %) and the line case's 10 ms steps after its tear.
- **3.1:** `MapWorld.Load(root, "farmland")` now takes 1.7 s and 130 MiB; its wind volumes are there for W7.
- **W4b (suggested):** a Douglas–Peucker pass at 1 cm over the debris rims' pixel corners in the generator, if 2.4's snag model reads an edge's direction (W4b's review).
- **QA (4.1):** a quiet run of the bench rows and `farmland-line-step`.

# Task 3.1: flight on the farmland

**Owner of this part:** game-developer · **For:** the tech artist (the drawn terrain near the drone), 4.1 (qa-engineer) · **Requirements:** APP-02, APP-03, APP-04, PRD-04, WLD-11, VIS-01, LOG-02 · **Decisions:** D-063, D-087, D-088, D-092 · **Model:** 82, **QueryVersion:** 8 (no sim code changed)

Numbers are labelled as above. "The player" is this branch's development review build (`Fpv450.Build.Windows -review -developmentBuild`, the one whose smoke test flies the keyboard), on AC, Balanced plan, vsync 1, a 1920 × 1080 window, the machine shared with other sessions.

## Decisions

### 1. Train flies the farmland
- **The flow:** Train → the setup → Fly loads `FarmLand` (the friend's scene file, untouched) and puts the flight on it (`FlightScene.Attach`). The summary's Fly again and Setup stay on the farmland; Explore still opens it in noclip (W1's §6, unchanged).
- **The world:** `FlightWorlds.Farmland(root)`, which is `MapWorld.Load(root, "farmland")`: 1.1's terrain, no objects until 1.2. It is loaded once per scene load (0.3 s, 28 MB, 1.1's figures) and shared by that scene's flights, as the test range was. The log names it `farmland`, so the summary and `tools/replay` reopen it from the repository.
- **The start:** W1's `FlightWorlds.FarmlandStart`, the setup's only start (D-092). An older setup naming `track` falls back to it (`SessionSetup`'s rule).
  - Legs on, the drone stands on its skids on `SampleGround` there (37.894 m, measured).
  - Legs off, `LaunchStand.Build` stands on the same foot. The flight draws D02 at the physics' pose, as on the test range: (566.500, 37.894, 569.000) at 300.0° (measured, the smoke test). That is the pose the explorer draws.
- **The scene's camera** (the friend's `Review Camera`, its `FreeFlyCamera` on it) is switched off once the flight's camera exists. The flight's camera takes its post-processing (on: the scene's Global Volume) and its far plane (6000 m), so the flight draws the friend's look as Explore does. A scene without a camera of its own (the test range) is unchanged.
- **The wait for the radio** previews the start over the drawn terrain there, not at the plane's 0.

### 2. The test range leaves the player
- **The build's scenes** are Boot and FarmLand (`ProjectSetup.Apply`, `EditorBuildSettings.asset`). `Fpv450.Build.Windows` requires FarmLand instead of the test range.
- **StreamingAssets** carry the farmland's `map.json` and the 34 files its tiles list (`FlightWorlds.FarmlandFiles`: heights, classes, and holes when listed; 26.8 MB, derived from the file sizes), beside drone.json and the three tables. The test range's placements and metadata are no longer copied. `AppTests.ThePlayerCarriesEveryFileTheFlightReads` loads the farmland from the copies alone, at the repository's hash, and finds no test range file.
- **Who keeps the test range:** its own player (`Fpv450.Build.TestRange`), `SessionTests`, `LegShotsTests`, flightbench, worldbench and `tools/replay --map test-range`. A pilot's older log on the test range still replays from the repository. `TestRangeBoot`'s and `LookSetup`'s handling in `FlightScene.Attach` stays; it is harmless where they are absent.

### 3. The light: the friend's, at every setup (D-088)
- **Chosen:** the farmland keeps the scene's own light, sky, fog and volume at every time of day and rain. The setup still offers all five settings (PRD-04).
  - **Time of day** goes to the log and to the feed's stub signal, which nothing draws from. The look does not change.
  - **Rain** wets the ground in the physics, as on the test range: the contacts take surfaces.json's moisture sets (the stand's Rain flight loads the straw's wet modulus, the smoke test). The sky does not change.
  - **Wind** blows in the physics: `WindSetup.ForMap` over the farmland, its field at the level chosen. The map has no wakes, canopy or cuts until its objects (1.2).
- **Why not the look:** the friend's scene has one fixed sun, its sky and its Global Volume. Moving the sun for the hour, or swapping in the test range's overcast preset for rain, would change the look D-088 adopted as it is. The test range's `LookSetup` is not in the scene and is not added. The look's time of day and weather belong to the look's own unit (W6 or later).
- **The flight logs it:** "look: the scene's own light; the setup's 12:00 and Sunny leave it as it is".

### 4. The drawn terrain near the drone (WLD-11, VIS-01)
- **The rule** (`FlightScene.NearTerrain`): every terrain tile within 30 m of the drone draws at Unity's finest pixel error, 1. Every other tile keeps the friend's: 5 on the map, 8 on the surround, 20 on the far ring.
- **Why 1:** it is Unity's floor. Its error is counted in the camera's pixels, so it shrinks toward the lens. Nearest the drone the patches are the full heightmap, which is the sim's heightfield (W2: the same within 0.0112 mm at its 1,000 probes).
- **The measure** (`TerrainParity`, from the depth Unity draws):
  - A camera with the FPV camera's lens pose, field of view and render size draws the terrains alone, their trees and grass off, into a depth target.
  - It looks along the lens and straight down, at the friend's pixel errors and at the flight's.
  - Each pixel goes back to the world through the GPU's own projection matrix. Its height is compared with the sim's `SampleGround` there, binned by its horizontal distance from the lens: 2, 10 and 30 m.
  - **It checks itself:** at rest the 2 m band agrees to 0.004 mm over 1.9–4.7 M pixels. The wrong row order would put each depth on another pixel's ray, metres off.
- **Measured along the hills flight** (the smoke test's `-smokehills`, the first of the final build's two runs; 7 places, the larger of the two views; the largest |drawn − sim| in mm):

| Where | The friend's errors: ≤ 2 m / ≤ 10 m / ≤ 30 m | The flight's (1 within 30 m): ≤ 2 m / ≤ 10 m / ≤ 30 m |
|---|---|---|
| At rest on the start | 0.005 / 0.009 / 10.427 | 0.005 / 0.009 / 0.039 |
| Cruising, t = 22 s, (495, 478) | 1.832 / 1.839 / 3.654 | 0.005 / 1.834 / 3.593 |
| Cruising, t = 42 s, (401, 349) | 3.667 / 5.462 / 6.412 | 1.835 / 3.664 / 3.682 |
| Cruising, t = 62 s, (315, 286) | 5.355 / 32.931 / 39.185 | 0.007 / 0.026 / 9.036 |
| Cruising, t = 82 s, (235, 402) | 0.005 / 3.664 / 3.679 | 0.005 / 3.664 / 3.679 |
| Cruising, t = 102 s, (165, 517) | 2.748 / 3.663 / 4.578 | 1.832 / 3.178 / 3.656 |
| Landed in the valley | 0.003 / 1.831 / 2.757 | 0.003 / 1.831 / 1.848 |

- **What it shows** (the four runs that flew the route, the last above):
  - **Where the drone touches the ground** (at rest and landed, the lens 0.2 m over it), the drawn terrain within 2 m is the sim's to 0.005 mm under both settings: the full heightmap.
  - **In flight, 5 m over the ground,** the friend's errors left the drawn terrain up to 6.0 mm off within 2 m, 32.9 mm within 10 m and 39.2 mm within 30 m.
  - **The flight's rule** holds it within 3.0 mm within 2 m, 5.5 mm within 10 m and 9.1 mm within 30 m. Most of that is one height unit or half of one: 3.66 mm, a unit of the 120 m tiles (120 / 32766, derived), and 1.83 mm. It is Unity's level of detail dropping one sample's unit, less than a pixel at its distance from the lens: the 10 m and 30 m bands' worst are 0.64 and 0.35 of a pixel at their edges.
  - **Against WLD-11's 1 mm:** met where the drone touches. In the air at 5–30 m it is not met (up to 9.1 mm), and no Unity setting goes finer. The tech artist's review decides whether that counts as "near the drone".
  - **The smoke test guards it:** with the flight's rule it fails if a view's 10 m or 30 m band passes one pixel at the band's edge, or if on the ground its 2 m band passes 0.01 mm. One pixel is 8.6 mm at 10 m and 25.8 mm at 30 m (the render's 80.21° over 1,625 rows, derived). Pixel error 1 is Unity's promise of about a pixel at most. The friend's errors passed the guard (32.9 mm within 10 m is 3.8 px).
- **The cost is not measured.** Pixel error 1 on the map tile draws more terrain triangles while the drone is over the map's 1 km². The frame rate needs vsync off on AC: the tech artist's run (PLT-07).

### 5. The smoke test on the farmland: what changed
- **The same checks, on the farmland:**
  - Train's setup lists the village's start (D-092; it was the houses', D-077).
  - Fly loads the farmland.
  - The wait for the radio views the start over the terrain there (it was under 0.5 m of the plane's 0).
  - The flight's wind carries the map's own cuts (`MapWind.Cuts`). On the test range the check was "more than none"; the farmland has none, because it has no objects in the sim until 1.2.
- **The house flight became a flight into the ground.** It turns to 158° once 2 m up, as before, then pitches until the nose is 60° down and dives on the hover's command.
  - The summary must name "the ground" (it was `Z01_house_adobe`).
  - When the line holds, R is now pressed from the summary too, so R's restart is checked on either path.
  - In the final runs the crash broke the line (at the spool's exit lip, or in the free span). R then ran after the break (the failsafe disarmed) and again from the summary, each restarting in the frame that read the press.
  - After the crash a broken line's failsafe settles first: the pose and the OSD are read once its stage has reached the disarm (1.5 s after the last good frame) and a frame has drawn it. One run read them a frame before a late break's disarm and failed on the OSD's AIR.
- **Unchanged:** the menu, the PSO tripwire, Explore and its stand, the OSD at rest and in the air, V and its views, the pause, the disarm and re-arm in the air, the stand's flight in Rain, its Restart without the radio, the line drawn and laid again, the blade tables, the summaries, the replays and the saved setup.
- **`-smokehills`** (new) flies the hills flight below in place of the flights.
- **`-smokelegs`** still flies LegShots' cases, now on the farmland's soil. Those cases were found on the test range's soil and are not re-swept here (see the next units).

### 6. The flight on the hills (the scenario, flown by a script)
- **`HillsPilot`** (engine-free, `App/Session`) flies the development keyboard as a pilot's hands would. Its keys are logged as any stick input, so the replay never depends on it.
  - **Acro:** a key held turns the drone at the half stick's rate (about 50 °/s on roll and pitch, derived from the player's default rates). No key holds the attitude.
  - **Control:** it keeps pitch and roll within 1.5° of their aims, and steers the throttle stick to the curve's command for the height's and the climb's errors.
  - **The flight:** it climbs to 5 m over the sim's ground and cruises at 6.5 m/s, looking the ground up 1 s and 2.5 s ahead. Then its height's aim sinks at 0.6 m/s on through the ground to 2 m under it, so the legs press down. It lands and pulls the throttle down, and its caller disarms.
  - **Every phase steers to a height.** The pilot counts its throttle stick from the keys it held over the real time since its last call, as KeyboardSticks counts it. A stick that drifts off its count then costs a height, never the landing.
  - **Why (found in the player):** two runs of the first version failed in the player and none headless. It integrated the keys it chose, not the keys held, over a frame's time, not over the frames since its last call (a shot's frames). Its descent then steered to a sink rate alone, and a count 0.05 off held the drone in the air: it drifted, and once the fiber broke. Both are fixed: the count, and the sinking aim above.
  - **The route:** from the village's start (38 m) south-west over the fields' rise to the hill at (330, 250), 39 m, then north-west down its side to the valley at (120, 620), 16 m. That is about 820 m with 23 m of fall (validate.py's heights every 50 m, derived), a little over two minutes at 6.5 m/s.
- **Headless** (`FarmlandFlightTests.AFlightOverTheHillsReplays`, in `dotnet test tools/inputtests` and the EditMode suite): Windy, legs on, 1 kg, the line paying out, the keys every 16 ms.
  - **Measured:** 124.7 s, 800 m over ground of 16.9–39.8 m. It landed and was disarmed, with no crash and 503.2 m of line out.
  - **Its log replays bit for bit:** 124,657 steps and 3,344 events.
  - **Its cost:** 38 s in .NET Release, 125 s in Unity's EditMode (Mono).
- **In the player** (`-smokehills`: real time on the flight thread, the same pilot each frame):
  - **Measured, the final build's two runs:** 127.6 s and 125.4 s; 803 m and 799 m over ground of 15.7–40.0 m. Each landed in the valley ((119.0, 16.7, 603.2) and (122.6, 17.0, 606.2)) with no crash, on 7.1 % and 7.0 % of the battery.
  - **Their logs replay bit for bit** in the player (127,587 steps and 2,072 events; 125,402 and 298). The first replays headless with `tools/replay` in .NET as well: the same, in 23.3 s.
  - **The steps on the host clock** (the first): p50 519 µs, p99 887 µs, 552 of 127,587 over 1 ms. That is the development player on Mono on a shared machine, not PLT-04's .NET figures.
  - **The log is 452 MB:** the format's 3.5 MB/s (derived), not this task's.

### 7. No step into the ground (`GroundCheck`)
- **Read from the log alone:**
  - its Tunnel events (the contact model's own test, a physics bug);
  - at every step within 2 m of the ground, each contact shape of the drone at the step's pose, measured against the sim's ground under each core end as a support's sample is: r − n_y·(y − h) past the facet's plane (2.2's Sunk).
  - The shapes are the header's, rigid on the body. A support's and a skid's figure is therefore an upper bound by its leg's compression.
- **Measured:**

| Flight | Tunnel events | Body parts, deepest past the ground | Supports and skids (rigid) |
|---|---|---|---|
| Headless | 0 | −11.84 mm (`leg_rr`, the landing; never touched) | 3.44 mm (`support_rr_end`, the landing, t = 122.6 s) |
| The player, run 1 | 0 | −11.23 mm (`leg_rl`, the landing; never touched) | 4.51 mm (`support_fr_end`, the landing, t = 124.3 s) |
| The player, run 2 | 0 | −10.73 mm (`leg_rl`, the landing; never touched) | 4.89 mm (`support_rl_end`, the landing, t = 122.2 s) |

- **The tolerance:** no tunnel, and no body part past the ground at any step. The supports' 3.4–4.9 mm is their 5 mm spheres bearing into the soil at the landing, their legs' compression included. That is the contact law's sinking, within the support's own radius.

## Checks (pasted in the handoff)
- **EditMode,** filtered to App, Session, Feed and Farm: 182 of 185 pass, the new ones included. The 3 that fail run code this branch does not touch:
  - `SessionTests.ACrashIntoR01ANamesThePartTheObjectAndTheMaterial` and `ACrashIntoTheRangesCarNamesIt` fail the same way at 9847c69e (measured, `dotnet test tools/inputtests`).
  - `LegShotsTests.EachFlightShowsItsLeg` flies the test range through files this branch leaves unchanged.
- **`dotnet test tools/inputtests`:** 541 of 543, failing the same two.
- **The rest:** the review build, the smoke test (the default and `-smokehills`), the headless replay, `precommit.py --tree HEAD` and `openspec validate`.

## For the next units (suggested, not this task's)
- **The tech artist:** the review of the drawn terrain near the drone (4), and the frame cost of pixel error 1 on the map tile, vsync off, on AC.
- **1.2's merge:** the farmland's objects bring its wakes, canopy and cuts into the flight's wind, and the smoke test's cuts check then counts them. The crash flight could go back to an object (a ruin by the start).
- **p7g-legs:** LegShots' cases (`-smokelegs`) now fly the farmland's soil. `LegShotsTests` already fails on the test range at this base, so a re-sweep belongs with that fix.
- **`tools/replay`** names the farmland "from the map …/placements.json" though it loaded map.json. Only `ScriptedFlight.OpenWorld`'s message is wrong.
- **QA:** the two `SessionTests` crashes into R01_A and `LegShotsTests` already fail at the line's head, before this task.

# Task 3.2: the drawn terrain near the drone

**Owner of this part:** tech artist · **For:** 4.1 (qa-engineer), W6 (the quiet PLT-07 run) · **Requirements:** WLD-11, VIS-01, PLT-02, PLT-07 · **Model:** 82, **QueryVersion:** 8 (no sim code changed)

Numbers are labelled as above. The runs are this branch's development review build on AC, Balanced plan, "best power efficiency" mode, CPU 3201/3201 MHz (`tools/hygiene/bench.py`'s reading before and after), a 1920 × 1080 window, the machine shared with three other sessions (CPU at about 40 %).

## 1. The measure (`TerrainParity`), reviewed
- **What it gets right** (read in the code, and its floor measured):
  - Its camera copies the FPV camera's pose, field of view and render size (2888 × 1625), so Unity's terrain picks each patch's level of detail as it does for the FPV camera: the pixel error counts that camera's pixels.
  - Each pixel's point comes back through the GPU's own projection, the world's in double, and is compared with `SampleGround` at its own x, z. The row order is picked by fit.
  - **Its floor** (measured, the full heightmap drawn): 0.002–0.04 mm within 10 m and 0.04–0.08 mm within 30 m: float positions 500 m from the origin. It can tell 1 mm.
  - The flight's setting sets every tile, while the rule sets those within 30 m. For the bands' pixels these are the same tiles: a tile holds a pixel within 30 m of the lens only if it lies within 30 m of the lens.
- **What it claimed but did not measure:** "each under a pixel" compared each band's worst error with a pixel at the band's outer edge. An error nearer the lens is a larger part of its pixel, and an error straight under a lens looking down moves nothing on the picture.
  - **Added: the shift on the picture** (`View.MaxPx`): per pixel, the angle between the lens's rays to the drawn point and to the sim's point under it, in the render's pixels. A pixel here is the field of view over the rows (0.861 mrad), 1.2 times smaller than the centre's (1.036 mrad), so the count errs high.
  - **The scale:** a PAL field line is 3.76 of the render's rows at the picture's centre (1080 rows over 287, derived).
- **The premise was wrong:** "Unity's terrain can't go below pixel error 1" holds for the Inspector's slider only. Set from a script, Unity keeps 0.3 and 0.05 (it reads them back) and draws by them (measured below).

## 2. Decision: pixel error 0.05 within 30 m, no patch
- **The rule** (`FlightScene.NearTerrain`, its shape unchanged): every tile within 30 m of the drone draws at pixel error 0.05; every other tile keeps the friend's 5, 8 and 20.
- **Why 0.05** (derived): Unity simplifies a patch only where the error left is under that many of the camera's pixels. The least error a simplification leaves on these tiles is half a height unit: 120 m / 32766 / 2 = 1.83 mm. At the FPV render's 965 px/rad that is 0.05 px at 35 m. So no patch within 35 m of the lens is simplified: past the 30 m radius at any flight height up to 18 m. Higher, the ground 30 m out may draw half a unit off, 0.05 px from 35 m away, where the drone touches nothing.
- **Measured** (the hills flight in the player, `-smokehills`; 7 places and noclip's start; the larger of the two views; the largest |drawn − sim| within 2 / 10 / 30 m of the lens, then the largest shift on the picture):

| Where (the first run, 4 settings) | The friend's errors | Pixel error 1 (3.1's) | 0.3 | **0.05 (the rule)** |
|---|---|---|---|---|
| At rest on the start | 0.007 / 0.010 / 9.743 mm, 0.38 px | 0.007 / 0.010 / 0.053 mm, 0.01 px | as 1 | **0.007 / 0.010 / 0.053 mm, 0.01 px** |
| Cruising, t = 22 s | 1.838 / 1.844 / 3.634, 0.18 | 1.838 / 1.844 / 3.634, 0.18 | 0.010 / 0.019 / 3.634, 0.18 | **0.010 / 0.019 / 0.059, 0.00** |
| Cruising, t = 42 s | 3.658 / 4.564 / 5.508, 0.41 | 1.834 / 2.748 / 3.692, 0.26 | 0.005 / 0.022 / 1.872, 0.10 | **0.005 / 0.022 / 0.057, 0.00** |
| Cruising, t = 62 s | 5.492 / 7.301 / 41.086, 2.26 | 0.005 / 7.301 / 16.197, 0.69 | 0.005 / 0.039 / 3.647, 0.18 | **0.005 / 0.039 / 0.075, 0.00** |
| Cruising, t = 82 s | 0.007 / 31.084 / 31.084, 2.68 | 0.007 / 0.019 / 8.950, 0.35 | 0.007 / 0.019 / 0.064, 0.00 | **0.007 / 0.019 / 0.064, 0.00** |
| Cruising, t = 102 s | 1.838 / 3.664 / 3.664, 0.34 | 1.826 / 2.748 / 3.664, 0.25 | 0.008 / 1.838 / 2.004, 0.17 | **0.008 / 0.023 / 0.062, 0.00** |
| Landed in the valley | 0.003 / 1.841 / 2.809, 0.54 | 0.003 / 0.018 / 1.861, 0.16 | as 1 | **0.003 / 0.018 / 0.048, 0.00** |

  - **The rule's four later runs** (builds of the final rule; the friend's errors beside it): with the rule, at most 0.067–0.070 mm within 30 m at all 7 places, the shift 0.01 px at most; the friend's errors left up to 38.1 mm and 2.24 px there. The one exception is a landing that broke the legs (the checks below): 0.165 mm, the lens down by the ground.
  - **WLD-11 is met as written:** within 30 m of the drone the drawn terrain is the full heightmap, the sim's to 0.075 mm (the measure's floor), in flight as on the ground.
- **Why not pixel error 1:** it is invisible in the feed (0.69 px at most, 0.18 of a field line), but it is not what is touched. The laid line is 0.25 mm thick (radius 0.125 mm), and pixel error 1 leaves the drawn ground 1.8–16.2 mm off within 30 m in flight: where it is high, the line on the sim's ground is drawn under it. At 0.05 the drawn ground is within 0.075 mm, under the line's radius.
- **Why not the patch:** it gives the same 0 mm with a mesh in the terrain's material, and the friend's terrain must then be hidden under it: a hole cut in its data (the store's, untouched) or a clipped copy of its shader, each with shadows and the splat look to match. One constant gives the same answer.
- **Noclip keeps the friend's errors** (Explore does not run the rule). From noclip's start (3 m behind the stand, 1.6 m up) the ground round the stand is the sim's at the friend's errors (measured): within 10 m 0.019 mm through the feed and 0.024 mm without it (V), the shift 0.004 px. The smoke test now guards it at 1 mm.
- **The chase view** (V in the flight) is the same camera 2.3 m from the drone, under the same rule: nearer the lens is finer.

## 3. The cost (`-terrainab`, A/B)
- **The method** (`TerrainCost`; `-smoketest -smokehills -terrainab -vsync 0` in a development build): the hills flight's cruise switches the rule on and off every second, and each frame goes to its second's side. The first 0.25 s of each second are left out (the timings lag a few frames), and so is a quarter-second after each measure or shot. Interleaved, the shared machine's load falls on both sides alike. FrameTimingManager gives the GPU's and the CPU's times; the profiler's counters give triangles and draw calls.
- **Measured** (means; the rule's side / the friend's errors):

| Run | Frames | GPU, ms | Main thread, ms | Triangles, M | Draw calls | Waiting to present, ms | Frame, ms |
|---|---|---|---|---|---|---|---|
| Pixel error 1 (3.1's rule; the first run) | 1,785 / 1,932 | 3.716 / 3.669 | 4.941 / 4.899 | 26.83 / 26.41 | 1,221 / 1,197 | (not recorded) | 22.67 / 22.01 |
| **0.05**, run 1 | 1,686 / 1,869 | 3.764 / 3.762 | 5.236 / 5.008 | 27.58 / 26.92 | 1,247 / 1,218 | 18.64 / 17.49 | 23.94 / 22.52 |
| **0.05**, run 2 | 1,748 / 1,893 | 3.695 / 3.698 | 4.712 / 4.551 | 27.01 / 26.46 | 1,222 / 1,199 | 18.73 / 17.60 | 23.51 / 22.17 |
| **0.05**, run 3 (no frame while a measure's comparison runs) | 658 / 790 | 3.659 / 3.657 | 4.634 / 4.464 | 26.94 / 26.46 | 1,231 / 1,204 | 18.30 / 17.21 | 23.05 / 21.66 |

- **What it shows** (0.05 against the friend's errors, the three runs):
  - **The GPU does not notice it:** +0.00 ms of 3.7 (medians and p99 within 0.08 ms either way). The rule draws 0.5–0.7 M more triangles in a frame of 27 M, and 24–29 more draw calls.
  - **The main thread** takes 0.16–0.23 ms more: Unity's terrain walks and culls more patches.
  - **The frame** takes 1.3–1.4 ms more (6 %) in all three runs, 1.1 ms of it in the wait to present, outside this frame's GPU work and the main thread's. Run 3 rules out the measures' comparisons, which all fall on the rule's seconds. The cause is not found here.
- **An indication only** (a shared machine in its "best power efficiency" mode): the verdict is W6's quiet PLT-07 run, with the same flag.
- **Found on the way, not this task's:** a frame takes 22–24 ms (42–44 fps) while the GPU works 3.8 ms and the main thread 5 ms. The rest, 17–19 ms a frame, is the main thread waiting to present (FrameTimingManager's `cpuMainThreadPresentWaitTime`). PLT-02 asks 100 fps on average in the FPV view: W6 should look at the presentation first (the window, the power mode, the other sessions).

## 4. What changed
- `FlightScene.NearPixelError` 1 → 0.05, and `NearOn`, the A/B's switch.
- `TerrainParity.View.MaxPx`, the shift on the picture.
- The smoke test:
  - with the flight's rule it fails if any view's 30 m band passes 1 mm (it was a pixel at the band's edge), or the 2 m band on the ground passes 0.01 mm;
  - Explore measures the ground round the stand from noclip's start, through the feed and without it, and fails past 1 mm within 10 m;
  - `-terrainab` times the rule (`TerrainCost`).

## Checks (pasted in the handoff)
- **The review build** (`Fpv450.Build.Windows -review -developmentBuild`): succeeded, 0 errors.
- **The smoke test** on it: the default run passes (81 checks, the noclip guard included); `-smokehills` passes (34 checks, the 14 near the drone at 1 mm included), its log replaying bit for bit with no step into the ground.
- **`-smokehills -terrainab -vsync 0`:** runs 1 and 2 pass. Run 3 failed two checks, neither this task's: HillsPilot's landing broke three legs (the front left "at 0.06 m/s") and two props, so `GroundCheck` read the broken legs as rigid on the body (118 mm past the ground), and the landed view's 2 m band read 0.023 mm with the lens down by the ground (the guard is 0.01 mm). The next plain run landed clean.
- **EditMode,** filtered to App, Session, Feed and Farm: 183 of 186 pass. The 3 that fail are 3.1's three (two `SessionTests` crashes into R01_A, `LegShotsTests`), failing before this task.
- **`precommit.py --tree HEAD`:** 3512 files scanned, 0 findings.

# Task 2.7: contact with the farmland's objects

**Owner of this part:** physics-engineer · **For:** 3.3 (game-developer), 4.1 (qa-engineer) · **Requirements:** PHY-01, PHY-05, PHY-26, PLT-04, WLD-16, REAL-05 · **Model:** 85, extended (below; the orchestrator renumbers at the merge) · **QueryVersion:** 8, unchanged (no world code changed)

Numbers are labelled as above. "Before" is this branch's base (1f39bc8c, model 85), run with the same scenario code; "after" is the head. The scenarios are `ObjectContactTests` (new), `RuinContactTests` and flightbench `--contacts`' bush row.

## 1. A wall stops a drone the same however thin its collision is

**What 2,705 against 22,202 was** (measured):
- **The 22,202 m/s² was 1.1's slab at model 82, before 2.6.** Its stopped blades appeared inside the slab and their springs pushed kN each (2.6's bug). At model 85 the same flight into a 1 m timber slab at the ruin's place (x −3, from z −30, 8 m/s) peaks at **5,069 m/s²**; into a 2 cm skin there, **2,704** (the ruin's own wall: 2,705).
- **A disarmed drone thrown at a wall** (8.7 m/s, level, at 1.5 m) peaks at 5,363.3 m/s² on every thickness, before and after. So the body's first blow was never the difference. What differed came after it, in three places:

**A. A part found inside a thin skin past its middle took the far face** (`ContactModel.Update`, a new pair).
- A pair begins with the query's contact. A first touch inside the step latches the face it came in by (K§7.5). A part found already inside, with no first touch, takes the query's nearest face: a blade come back to rest, a skid half a leg's break moved, a pair the step before did not keep.
- In a 2 cm skin that is the far face once the core is 1 cm in. Its normal pushes the part on through. Thrown at a 2 cm skin, the base drone made 5 touches on the far face and left at 3.554 m/s against the slab's 3.541 (8 on a 1 cm skin; on a 5 cm skin 15 touches, 61.25 N·s against 59.41, 3.871 m/s). In the ruin's crash, `skid_fl` latched the far face 22 mm deep and pushed the drone into the wall.
- **Rule (`BodySide`):** the drone is never inside a rigid shape, so a part's way in is from the body. For a new pair on a rigid box with no first touch and the core inside, the face is the one the line from the body's origin to the core enters the box by, as a slab's would be. Boxes only: a round shape's far side stays its tunnel rule's (`Entry`, `PastCentre`). Applied to round shapes too, the rule moved model 50's kept crown audit (`WithoutTheHangingGiveTheWindyCrownIsModel50s`: 2 tunnels against 1), so it is not.

**B. A blade at rest in a skin it reached by its passes** (`ContactModel.Slot`).
- After a jam the strikes' carve leaves the blade where it stopped, inside what it carved; the world is not changed (p7f-damage, assumed). In a slab that is inside the material. In a thin skin the blade's outer segments lie past the far face, in air.
- The base let those segments touch the far face from behind as the drone rebounded, pulling it back into the wall (+1,650 N along +z in the 1 cm skin's rebound, measured).
- **Rule:** a resting blade on a rigid box that its hub's line to the blade's core enters by another face than the contact's lies in the slot it cut (its own root passes through the box). Past the far face it touches nothing; inside, its face is the root's entry face, its overlap slack (2.6's rule: no motion pressed it). A blade pressed in by the body's motion, or resting on a box beside its root (a roof rack's rail), keeps the contact's face.
- A first version took "the face turned away from the hub". It broke `RestTests`' rack (a blade resting over the rail's edge, its hub below the rail's top): the root's line is the condition, not the side.

**C. The strikes took a thin skin for a rod inside the rim** (`PropStrikes.Pass`, `BladeCut.Wide`).
- The rule was "a shape the blade's radial line leaves again inside the tip is met by the leading edge" (a rod, a pole). A 2 cm skin across the disc is left again 2 cm in.
- But in the disc's plane a skin is a strip as wide as the disc: the tip's circle crosses it first, as it crosses a slab's face. **Rule:** a box whose cut still holds the tip's circle beside the line (at the two azimuths whose points lie halfway through the line's cut) is a face. A post inside the rim holds neither point (they lie √(r_tip² − d²) > a aside), so posts, rods, wires and limbs keep the leading edge. Round shapes and wires are not tested.
- A carve of a face then sweeps the strip it holds: the segment past `rm` less the segment past `rout` (air behind the skin). The base swept a pole's disc across the 2 cm: 10.19 J where the slab's segment took 67.70 J (now 62.05).
- At 8.7 m/s from 30 m the base's leading-edge crush broke the blade in the first pass (BladeLost) where the slab's carve jammed the rotor; now both jam.

**After** (measured, `ObjectContactTests`):

| Thrown, disarmed, 8.7 m/s | Peak, m/s² | Impulse along z, N·s | Leaves at, m/s | Touches past the face |
|---|---|---|---|---|
| 1 m slab | 5,363.3 | −59.406 | −3.541 | 0 |
| 2 cm skin | 5,363.3 | −59.410 (before −59.483) | −3.542 (−3.554) | 0 (5) |
| 1 cm skin | 5,363.3 | −59.406 (−59.57) | −3.541 (−3.567) | 0 (8) |
| W4b's two 2 cm skins, 16 cm apart | 5,363.3 | −59.410 | −3.542 | 0 |

| Flown, armed, 8 m/s, at the ruin's place | Before | After |
|---|---|---|
| 1 m slab | 5,069 m/s² | 5,069 |
| 2 cm skin | 2,704 | 3,637 |
| `Ruin_RoofCollapsed`'s wall (`RuinContactTests`) | 2,705, 67.40 N·s | 3,638, 72.98 N·s |

- **The remaining difference is physical:** the stopped blades are pressed on into 1 m of timber, or 2 cm with air behind. Up to the blades' share the steps are the same (1,110 m/s² at the crash step on both). In the slab the outer blade segments, embedded, take 23.7 kN and the prop is lost at the peak; in the skin only the segment in the skin bears. Every pass's mode and radius are the slab's; its work at most the slab's (`APropsTipMeetsAThinSkinAsAFace`).
- 1.1's 22,202 is not reachable at model 85 on any wall: it was 2.6's energy.

## 2. Pairs a shape holds (items 2 and 3)
- **The "events lost" were pairs** (`ContactsLost`), not the step's event buffer: in the door flight a part lodged in the next room's window frame holds up to **14** of its boxes at once (measured with the capacities raised), against `PairsPerShape` 8. The query's buffer (32) was never full (14 at most). The step's event buffer (64) never overflowed (0 `EventsLost`).
- A prop's disc in a crown's densest spot meets 7–10 shell pieces (W4a, measured there), and the crown flight below 4.
- **Decision:** `PairsPerShape` 24 for every shape, twig shells included. W4a's fit keeps its pieces; no refit. Memory: 109 shapes × 24 × 288 B = 0.75 MB (derived). A step's cost follows the pairs held, not the capacity.
- The door flight: 0 lost (before 4,205 at W4b 1.2, 8,369 at this base), 0 `EventsLost`. `RuinContactTests` now asserts it.

## 3. A drone in a crown: twigs cost the blades what they cost
**The hang, reproduced** (measured): IslandTree object 9, the old route's bearing, 6.85 m/s at 5 m, the drone on its legs with the airframe's effects. With the base model it hangs 3.9–4.2 m over the ground for the whole run (60 s), with or without surfaces.json, its four rotors at 230–1,540 rad/s, 24,545 blade strikes at 0 J each in 60 s. The twig shells were a thin compliant row: a glance (p7f-damage design 7 left the plants' cutting to p7a's elements, 1.15, not built, and the vendor crowns have no elements).

**Research** (sources below):
- No controlled study of a UAV propeller striking twigs was found. Operators report a tip snapped off, a divot, a prop stopped by a small branch and the drone falling.
- The cut: impact cutting of woody stems takes of the order of 0.1 J/mm² of section (Miscanthus, 9.5 mm stems, about 10 J/cm², published; willow similar, rising with moisture and independent of diameter). The repository's `surfaces.json` twig carries 80 kJ/m² (assumed there, "the order of woody stems in Persson 1987"): kept.
- The crown's fine wood: crown fuel inventories split crown biomass by twig class (0–0.63 cm, 0.64–2.5 cm: e.g. 9.2 and 28.0 kg a tree for Aleppo pine, published), and crown-fire models count foliage and half the <6 mm twigs as the crown's available fuel. That sets the order of the twigs a disc meets; the shells' own twig count is not in the metadata.

**The model** (`PropStrikes.Twigs`, `Twig`):
- **The twigs:** a crown's `twig` shell stands for twigs of `CrownTwigDiameter` 7.5 mm (assumed: the middle of the shell's 5–10 mm, the row's rationale) at `CrownTwigLength` 2.0 m per m³ (assumed: about one 0.5 m axis of 5–10 mm per foliage spray, the sprays a card's half depth apart, 0.2–0.3 m³ each; of the order the fuel loads give). Their tissue is `surfaces.json`'s twig at the flight's moisture (cut energy, density). Without the table they glance, as before.
- **What a pass meets:** isotropic twigs cross a plane at n_A = L_V/2 per m² (stereology, derived). A blade cuts each crossing it meets, and a cut twig no longer crosses there. So the rotor meets only what its disc's motion brings in: across the plane the immersed region's width times its move (the chord's into the shell, the segment's height along it, the disc's diameter with the hub inside), and along the axis half the crossings sliced again, a slice thinner than the twig costing its share (derived: e_s/d is of the order of green wood's crush). At most every crossing each pass. A disc held still cuts its way clear and then meets nothing.
- **No draw:** the rotor meets its next twig when the crown it has swept holds one more on average (`_twigDue`), the blades in turn. The deepest shell's meeting stands for the union (they overlap, 7–10 at a point).
- **Each twig** (p7a-ground design 9, p7f-damage design 7): at u = ω·r_c (the immersed region's mean radius), E = e_s·πd²/4 + m_f·u² with m_f the twig's mass within one chord. The cutting force E_cut/d bends the inner stations edgewise and breaks the first that fails. The rotor pays E (a stop when it has less), the body the twig's impulse and the reaction, as a pass's. Logged as a `BladeStrike` (Carve or Break) and the step's `BladeStrikes` slot as an `Element` (its diameter and cut energy).
- **What a twig costs** (derived): at 900 rad/s and r_c 0.09 m, 3.5 J to cut and 6.9 J to fling, 10.4 J, about 40 % of the rotor's ½Jω² (27 J); a cutting force of 471 N edgewise. At 7 m/s with half the disc in the shell a rotor meets about 1.8 twigs a second.

**After** (measured, `ADroneInACrownPaysForItsTwigsAndDoesNotHang`, 25 s): first touch at 10.89 s; 15 twigs cut for 135.4 J, no glance; the drone sinks onto the limbs, crashes and leaves the crown (65 m on at 25 s). The known-wrong (no table: the shells glance) hangs 4.17 m up with 8,502 glances. The flight's log replays bit for bit and its crown steps allocate 0 B.
- **Not tuned:** at `CrownTwigLength` 1 and 4 m/m³ (temporary builds, not committed) the drone also leaves the crown, crashed or fallen, within 9–25 s of its first touch (2 and 6 twigs at the entry; at 4 it falls to the ground at 20 s).
- **What it does not do yet:** the leaves (p7a's 1.15, 1.2 J a leaflet, many more crossings) and the shells' push on the body where the blades have cut (the shell still pushes as uncut). Both are p7a's elements' when a crown has them.

## 4. A flightbench row into a bush
`flightbench --contacts`, "the farmland bush": GreenBush (VF07, 579 placed: six twig spheres, no limbs) alone on a meadow plane, the drone 35 m south at the bush's wind-box height (0.93 m), 10 m/s, with surfaces.json and the airframe's effects. Measured: felt first by `skid_left` on its twig shell at 10.01 m/s; 109 twig contacts taking 48.63 N·s; the blades cut no twig (a 0.3 s pass meets about 0.6 per rotor, derived, and the mean's count had not reached one); no crash, no tunnel, no pair lost; the drone ends 98.6 m on, 1.61 m up, at 10.83 m/s. PASS.

## 5. 2.6's two
- **After `PropLost`** (`ContactModel.RestTrace`): the prop is lost in `MotorShaft.Finish`, after the step's resting blades' loads were summed. `SetShaftLoad` is now skipped for a rotor `Propulsion.Lost` names.
- **Without surfaces.json** (`ContactModel.Update`): a turning blade's passes glance through the ground and the soil shapes (no face to them), while a blade at rest bore on them as rigid. That is how 2.6's rotor, its disc 50 mm into the floor, came back to rest inside it. Two ways to agree:
  - **Tried first, the strikes meeting the floor as rigid** (harder than the blade): the stub's tipped landing then made 87 passes for 54.08 J and broke its props. It removed the known-wrong that 10 tests stand on (`StrikeTests`' "the rigid ground without surfaces.json only glances", `PropsThatTouchTheGroundBogDown`'s model-40 glance, `InASteadyCrosswindSoilIsStillThePlane`'s plane), so not taken.
  - **Taken: a resting blade passes them too**, as the strikes do. Without the table the ground is a stub for the blades in both states; every flight that matters carries surfaces.json (the session loads it for each flight), where the ground is physical to both. The stub's tipped landing: 147 glances before and after; resting blades touched the floor 16 times before (and broke on it, the shortest 0 mm), none now (`WithoutSurfacesABladeAtRestPassesTheFloorAsItsPassesDo`).

## Golden files, audited case by case
- **`flightbench --golden`:** every case and kept audit passes unchanged (60 PASS lines), the contact flight without surfaces.json included (model 31's and 26's bits). No golden flight meets a rigid box from inside, a crown, more than 8 pairs on a shape, or a resting blade on a no-table floor.
- **`worldbench --golden`:** ALL PASS (no world code changed; `farmland_wind.version` keeps 85's stamp).
- **`flightbench --slopes`** and **`--contacts`:** PASS (the bush row new).
- **The suite's pins and kept audits** (`GoldenFlightTests`, `RangeContactTests`' model-50 crown, `RestTests`' rack replay): unchanged. Nothing was re-recorded.
- **Two rules were changed on the way because they moved kept audits** (measured, not committed): `BodySide` on round shapes moved model 50's crown audit (2 tunnels against 1), and the blade's "face turned from its hub" broke `RestTests`' rack (the blade bore nothing on 20 of 20 seeds). Both are narrowed as above, and both audits pass to the bit.
- **Replays:** the crown flight's log (twigs cut, a crash, 612 loose-piece records) replays headless bit for bit, 25,016 steps and 906 events, and its steps from the first touch on allocate 0 B (`ADroneInACrownPaysForItsTwigsAndDoesNotHang`).

## The model
- **85 is extended** (`LogFormat.ModelVersion`'s note; the orchestrator renumbers at the merge, D-072).
- A flight steps as in 85 unless: a part's new pair on a rigid box began inside it with no first touch; a resting blade met a box its hub's line enters by another face; a pass met a box its radial line leaves inside the tip while the box holds the tip's circle beside it; a shape held more than 8 pairs; a rotor turned in a crown's twig shell with surfaces.json; a blade at rest met the ground without surfaces.json; or a prop was lost while its blades rested.

## Sources
- Dynamic cutting properties of Miscanthus (giganteus) stems using an impact tester, AgriEngineering 6(3) 116, 2024 (doi 10.3390/agriengineering6030116).
- Nowakowski and Tucki, the specific cutting energy of willow (Salix viminalis) stems, Applied Sciences 15(2) 696, 2025; Rode and Witkowski on willow disc cutting (unit energy rising with moisture, independent of diameter).
- Mitsopoulos and Dimitrakopoulos, crown fuel biomass of Aleppo pine, International Journal of Wildland Fire WF06038, 2007.
- Crown-fire convention: available crown fuel as foliage and half the 1 h twigs (Scott and Reinhardt 2001, RMRS-RP-29).
- Operators' reports of branch strikes (mavicpilots.com, phantompilots.com threads): snapped tips, divots, a prop stopped by a small pine branch and the drone falling.

## For the next units (suggested, not this task's)
- **p7a 1.14 and 1.15:** the vendor crowns' elements (twigs and leaves as records) would replace the shells' mean count with the drawn twigs, and the leaves' strikes.
- **3.3 and QA:** `RuinContactTests`' door flight changes its end: at the base, losing pairs in the window frame (8,369 steps), the drone came down on the floor and crashed by its canister at 1.12 m/s; at the head it stays lodged in the frame to the flight's end, uncrashed (not traced to one rule; the pairs held are the likely one). The test asserts nothing about the end (its subject is the doorway), and a lodged drone at 3 m/s is the frame's to hold.
- **A blade jammed in a carve** grinds bark for many seconds at 2,000 `BladeStrike` events a second (the crown at 1 m/m³): the log's size there is QA's to watch.
- **QA (4.1):** a quiet timing run; this task's costs were not measured beyond the tests' wall time.
