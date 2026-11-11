# Design: w3-farmland-ground, task 1.1 (the heightfield)

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
