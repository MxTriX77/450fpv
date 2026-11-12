# Design: w2-farmland-export, the physics sign-off (task 1.2)

**Owner of this note:** physics-engineer · **For:** W3 (hilly ground in the sim core), W5 (our objects and the trench holes) · **Requirements:** WLD-11, WLD-16, PHY-01, AST-11 · **Decisions:** D-019, D-088, D-090, D-091

## Context
- **The package** (`maps/farmland/`, README): 17 tiles of Unity heightmaps (the map, 8 surround tiles, 8 far tiles), a ground class per alphamap sample, the grass, the placements and the outlines. It is the farmland's scene as it is (D-088).
- **The sim's world core today** (`Runtime/World/MapWorld*.cs`): a flat ground plane with patches and hole bands, a 2 m object grid over the placement file's `extent_m`, and the wake grid over the same extent. There is no heightfield yet; W3 builds it on this package.
- **Method.** Every verdict below was checked on the package itself:
  - the validator, `python tools/map/validate.py maps/farmland`
  - two read-only scripts over its files, run outside the repo. One covered the heights, the seams and the slopes. The other covered the classes along the generator's own tracks, which were re-traced from `FarmLandBuilder.Tracks.cs`'s control points and its centripetal Catmull-Rom with 8 segments.
- **No randomness enters here.** The ground is world-derived and never drawn.

## Verdicts

| Point | Verdict |
|---|---|
| 1. The triangulation | **Signed off.** + − × ÷ and one √ for the normal; deterministic; checkable bit for bit against `validate.py` |
| 2. Seams and overlap | **Signed off, with one rule made explicit:** the ground is the union of the tiles' solid columns, so its steps are vertical walls |
| 3. The frame | **Signed off.** Nothing in the sim assumes a centred map; a decision entry should accept the frame |
| 4. The hole mask | **Signed off** (place and encoding), **with three rules for W5.** The trench's filler band is too narrow for the farmland's 0.977 m hole cells; W5 must resolve that before cutting |
| 5. The ground classes | **Corrected by one rule:** DryMud painted with gravel counts as `dirt_track`. The rest of the table stands |

## 1. The triangulation: signed off

**The model** (README, `Tile.height`). A tile has its corner (x0, y0, z0), its size (sx, sy, sz) and n = res − 1. Then:
- fx = clamp((x − x0)/sx, 0, 1)·n and fz = clamp((z − z0)/sz, 0, 1)·n
- cx = min(⌊fx⌋, n − 1), cz = min(⌊fz⌋, n − 1), u = fx − cx, v = fz − cz
- the samples are counted in Unity's indices, z from the south
- u > v: h = h00 + (h10 − h00)·u + (h11 − h10)·v
- otherwise: h = h00 + (h11 − h01)·u + (h01 − h00)·v
- y = y0 + h / 32766 · sy

**Measured against Unity's 1,000 probes** (seed 450):

| Interpolation | map | surround | far |
|---|---|---|---|
| SW–NE diagonal (the package) | 0.0065 mm | 0.0046 mm | 0.0112 mm |
| NW–SE diagonal | 5.03 mm | 1.69 mm | 154.6 mm |
| bilinear | 3.87 mm | 0.84 mm | 88.9 mm |

The probes tell the three apart by at least 590× on the map and 7,900× on the far ring (derived). What is left over is Unity's single-precision `SampleHeight`: a float's ulp at 40 m is 3.8 µm (derived). The sim's double is the reference; Unity's float is the copy.

**Deterministic with + − × ÷ √ only:**
- **The cell** is a truncation of a clamped, non-negative double (an exact conversion), plus comparisons.
- **Every spacing is a dyadic rational** (derived): sx/n is 1000/1024 = 0.9765625, 3000/256 = 11.71875 or 3024/256 = 11.8125.
  - So (x − x0)/sx·n and (x − x0)/(sx/n) round to the same bits. Scaling by 2^k commutes with rounding.
  - Measured: 0 difference at the 1,000 probes.
- **u and v are exact:** fx − cx with cx ≤ fx < 2·cx (Sterbenz), or u = fx when cx = 0 (derived).
- **The sample differences are exact integers** below 2^15. What remains is two products, two sums, a quotient, a product and a sum, each rounded once in a fixed order.
- **No transcendental function,** and no FMA as long as each operation is written separately (K§14.5: keep it in double).
- **The facet's normal** is normalize(−∂y/∂x, 1, −∂y/∂z), with one √:
  - on the u > v triangle: ∂y/∂x = (h10 − h00)·k/Δx and ∂y/∂z = (h11 − h10)·k/Δz
  - on the other: (h11 − h01)·k/Δx and (h01 − h00)·k/Δz
  - k = sy/32766, and Δx, Δz are the spacings
  - The normal is constant on each triangle. It changes across the cells' edges and diagonals, as Unity's collider does.
- **The seams are bit-continuous** (derived). On a shared edge, both tiles reduce to the same expression in the same shared samples, for example h10 + (h11 − h10)·v at x = 1000. So the highest-tile `max` picks equal bits there.

**Units and resolution:**
- metres
- a height quantum of 120/32766 = 3.662 mm (derived). It sets the samples' heights, not steps in the ground: between samples the surface is linear
- facets of 0.977 m on the map and the surround, and 11.7–11.8 m on the far ring (derived)

**Slopes, measured over every facet:**

| | under 5° | 5–10° | 10–15° | 15–20° | over 20° | steepest |
|---|---|---|---|---|---|---|
| map | 80.3 % | 17.5 % | 2.1 % | 0.11 % | 0.03 % | 37.6° |
| surround | 69.5 % | 30.3 % | 0.26 % | 0 | 0 | 13.4° |
| far ring | 67.0 % | 32.8 % | 0.27 % | 0 | 0 | 13.2° |

- **Everything over 18.5° is in the village** (measured): 789 of the map's 1,654 cells over 15°.
- **It is the generator's levelled house plots,** which ease back into the hill over 7 m (`LevelVillageGround`). It peaks at 37.6° beside the two northern ruins, around (511, 616) and (539, 616).
- **Feet slip** on dry `dry_crust` above 35° (μs 0.7) and on saturated `meadow_sod` above 18° (μs 0.33). Both values are assumed (surfaces.json), and the angles are derived as atan μs.
- **The start point is in the village (D-092),** so W3's slope checks must run 0–38°.

**For W3:**
- Copy `Tile.height`'s order exactly.
- Add a golden: the C# height at the 1,000 probes equals `validate.py`'s to the bit. CPython's floats are IEEE doubles with each operation rounded on its own, so Python is a second runtime for free.

## 2. Seams and overlap: signed off, with one rule made explicit

**The rule.** The ground is the union of the tiles' solid columns. Tile i's column is every point over its rectangle at or below its surface.
- The top of the union is the highest tile's surface. That is the package's rule, and the overlap's.
- Wherever a neighbour lies lower, its side is a vertical wall. That is how the sim reads a terrain: solid below. It is also the only reading under which a step can be touched rather than teleported through.

**Measured:**

| Where | What |
|---|---|
| Map ↔ surround, and the surround tiles with each other | **12 seams, sample for sample identical** (the validator; my scan: 0.00 mm at 4,001 points per seam) |
| The surround's outer edge, 12 km around [−1000, 2000]² | **A ledge 1.399–1.610 m high** down to the far ring (96,004 points every 0.125 m) |
| The far sides' 24 m reach under the surround | The surround is at least 1.276 m above them, so the highest tile is always the surround there |
| Far corner ↔ far side (8 seams of 3 km) | **Not shared:** the sides' spacing along the seam is 11.8125 m against the corners' 11.71875 m, so the edges cross at **steps of up to 70–99 mm** (measured: 81, 73, 79, 99, 80, 82, 70 and 73 mm) |

The validator compares only tiles of equal size, so it doesn't see the far seams. Both features are the friend's scene as it is (D-088). Unity draws the ledge as an open slot under the surround's edge, and its colliders have no wall there either.

**What W3 must do with them:**
- **Steps are walls.** `StaticContacts` reports each of the 12 step lines as a vertical face where a sweep crosses it below the higher surface:
  - the 4 outer edges of the surround, and the 8 far seams
  - the face's normal is horizontal, out of the higher tile
  - the face's height is the difference of the two tiles' surfaces at that point
  - `SampleGround` still reports the top
- **Never as a height jump.** A foot or a fiber node carried 1.4 m into the ground in one step is a `tunnel` event, a physics bug (K§7.5). The lines are fixed by the tile layout, so W3 derives them at load.
- **The ledge's lip** takes the top tile's surface row and its soil lip radius, 0.02 m (assumed, surfaces.json), for the fiber's edge contact.
- **Off every tile there is no ground.** The world ends at [−4000, 5000]², 4.4–4.6 km from the village (derived), while the spool holds 10 km (published, `drone.json`). See the open question at the end.

## 3. The frame: signed off; a decision entry recommended

**Nothing in the sim assumes a centred map** (read on this branch):
- **The object grid** (`MapWorld`) runs from `extent_m`'s south-west corner (`_gridX`, `_gridZ`); the test range's is [−256, −256, 256, 256]. Outside the extent, `CellX`/`CellZ` clamp to the edge cells and every item is still bounds-checked, so the results stay exact.
- **The region caches** are keyed by world bounds.
- **The wake grid** (`WakeObstacles`, `WakeLayout`) carries its own `OriginX`/`OriginZ`, which the log header records (`LogHeader.WakeFieldsLogged`). It reads open air outside its cells.
- **`WindSetup`** accepts any origin within 1e7 m.
- **The log** stores doubles.
- **The Unity adapters** (`Scripts/App/Session`, `Scripts/World`) hold no map centre.

**Precision:**
- At the world's farthest point, 5000 m out, a double's ulp is 2^−40 m = 9.1e-13 m (derived). Unity's floats get 0.49 mm there (derived), for rendering only.
- The wind's plane waves (k ≤ 2π/0.5 m = 12.57 rad/m, assumed in `WindField`) reach 12.57 × 7071 m = 8.9e4 rad at the far corner (derived). That is far under `DetMath.SinCos`'s exact range of about 1.6e6 rad (derived in `DetMath`).

**What W3 must keep:**
- **The 2 m object grid stays on the map's 1000 m extent,** since objects are on the map only (D-091). A 2 m grid over the rings' 9 km would be 20 M cells, an 81 MB cell table (derived).
- **The 17 tiles get their own lookup.**
- **The wake grid stays on the map too.** The surround's 499 trees are left out (D-091), so the air over the rings is open.

**Recommended decision entry** (next free number; the orchestrator writes it): "The farmland keeps its scene's frame. The map is [0, 1000]² m, its south-west corner at the origin. A map package states its rectangle in `map_rect_m`, and nothing in the sim may assume a centred map. D-019's units, axes (+X east, +Y up, +Z north, left-handed) and grid order (row 0 north, column 0 west) stand." This supersedes only D-019's "the map's centre is the origin".

## 4. The hole mask: signed off, with three rules for W5

**The place and encoding are signed off as proposed:**
- an optional `"holes": {"file": "holes/<tile>.raw", "resolution": res − 1}` per tile
- uint8, 255 for a hole and 0 for ground
- row 0 the northmost, column 0 the westmost
- one value per height cell, on Unity's hole grid (`TerrainData.holesResolution` = heightmap resolution − 1)
- only the map tile will carry one (objects on the map only, D-091); 1 MB in LFS
- the validator rejects any value other than 0 or 255

**The cell of a point** is the one its triangulation uses: (⌊fx⌋, ⌊fz⌋) clamped to n − 1, as in §1. So a point on the edge between a hole cell and a ground cell belongs to the cell its height comes from, and no extra rounding rule is needed.

**The sim** (W3):
- Over a hole cell, the heightfield is absent: `SampleGround` reports `Hole`, and `StaticContacts` and `Raycast` drop the terrain's contact or hit.
- The placed asset's own shapes are the only ground there. This is `MapWorld`'s hole-band rule today, keyed by cell.
- Unity's collider and render cut whole cells as well. The tech artist confirms that the drawn hole edge is the cell edge.

**Rules for W5:**
1. **Unity's sense is inverted.** `TerrainData.GetHoles` and `SetHoles` use `true` for solid ground and `false` for a hole (published, Unity's scripting reference). The export flips them; W5 checks it with a one-cell probe.
2. **The mask is derived data.** W5 writes it from the placed trenches, and `validate.py` recomputes it from them and requires equality. A hole cell is a cell that meets the trench's opening (its `top_width_m` about the centre line, with its ends).
3. **No naked hole.** Every point of every hole cell must lie inside the asset's filler band, where its shapes are the ground. The validator samples each hole cell every 0.1 m (assumed) against the band. A point with no ground under it is a fall through the world.

**The trench doesn't fit the farmland's cells yet (derived):**
- A cell that meets the opening reaches up to one cell diagonal past it: √2 × 0.9765625 = 1.381 m.
- So rules 2 and 3 together need a band half-width of at least 0.4 + 1.381 = 1.781 m, a band of at least 3.56 m.
- E01's metadata gives 2.35 m for the run and 2.1 m for the niches (measured, `E01_curving.assetmeta.json`). The run's band fits a 0.5 m grid (0.4 + 0.707 ≤ 1.175, derived) but not this one.
- Keeping the band and cutting only the cells wholly inside it would leave terrain over up to about 0.2 m of an axis-aligned opening's edge, and more on a diagonal (derived). That trench would look and act partly roofed with sod.
- W5's row says the metadata is reused unchanged, so this needs the orchestrator's call. **Recommended:** the asset artist widens E01's filler band to at least 3.6 m for the farmland, then W5 cuts.

## 5. The ground classes: corrected by one rule

**Where the layers go** (read from the generator, `FarmLandBuilder.Ground.cs`, `.Tracks.cs` and `.Village.cs`):
- **GrassA, GrassGround and GrassDry** are the meadow's green, tan and dried grass (`PaintMeadow`).
  - GrassDry is also most of an abandoned field's weed cover (`BlendFieldSurface`).
- **DryMud** is bare, sun-scorched earth in the meadow's dry centres. The code's "burnt" is the sun's, not fire, so `burnt_field` (S07, ash) doesn't apply.
  - It is also the dry drifts and dried soil of the overgrown fields, and the trampled earth round the houses.
  - On the main tracks it is the 35–80 % of mud drifts over the gravel.
- **Gravel** is the four 6 m main tracks, "muddy rather than clean" in the generator's own words, and up to 35 % gravel round each house.
- **GrassPath** is the side tracks' bare wheel ruts. Their crown is GrassA or GrassDry.
- **AerialMud** is the furrowed bare soil of the fields.

**Measured on the class map now:**

| Where | Classes |
|---|---|
| Main tracks, within 2 m of the centre line | dirt_track 57.9 %, **dry_crust 42.1 %** |
| Side tracks, ruts (1.3–1.5 m off the centre) | dirt_track 92.8 %, meadow_sod 5.0 % |
| Side tracks, crown | meadow_sod 90.1 %, weeds 6.5 % |
| Within 10 m of a ruin's pivot | **dry_crust 92.4 %**, meadow_sod 4.4 %, weeds 3.2 %, dirt_track 0 % |
| Surround and far ring | meadow_sod 69–71 %, weeds 29–31 % |

The README's "Gravel also rings the ruins" holds only for the look. The trampled earth outweighs the gravel everywhere, so the ring reads `dry_crust`, not `dirt_track`.

**The one error.** A track's mud is a film on the compacted track body, and the soil under a foot is the body's. Today 42 % of the main tracks take `dry_crust`, with:
- 2.8× lower bearing (5e7 against 1.4e8 N/m³, assumed)
- 2.8× the static sink: 2.8 mm against 1.0 mm dry, and 9.4 mm against 3.4 mm wet (derived, the 15 mm reference foot at 25 N)
- a 0.1 higher μ

The difference is modest, but the main tracks run past the village, where most takeoffs and landings will happen (D-092). A per-layer table can't separate track mud from field mud, so the fix is one rule in the exporter's class weights.

**The corrected mapping (for the re-export):**

| Layer | Surface (id) | Why |
|---|---|---|
| `GrassGround` | `meadow_sod` (1) | The meadow's tan ground between the grass: the same root-bound sod (assumed) |
| `GrassA` | `meadow_sod` (1) | Green meadow: root-bound sod (assumed) |
| `GrassDry` | `weeds` (8) | An abandoned field's weed cover, which S08 is: firm, rooted loam. And the meadow's dried areas: a drier, so firmer, sod (assumed; the published direction, a soil's Bekker moduli rise as it dries, Wong 2008) |
| `GrassPath` | `dirt_track` (13) | The side tracks' wheel ruts: wheel-compacted. The crown's grass already reads `meadow_sod` |
| `Gravel` | `dirt_track` (13) | The muddy, compacted gravel track body (assumed; there is no gravel row, and `rubble`, S10, is brick and mortar debris, not a wooden village's yard) |
| `AerialMud_0`, `AerialMud_90` | `tilled` (6) | The fields' furrowed loose loam (assumed). Each field's direction is its `furrow_axis` |
| `DryMud` | `dry_crust` (2), **but `dirt_track` (13) on any sample that carries Gravel weight** | Bare dry crusted soil in the meadow, the fields and the house yards (S02's own case). Where gravel is painted with it, it is the track's (or the yard's) mud film on a compacted body |

**The rule, exactly** (`FarmlandExport`, the class weights, per alphamap sample):
- Each layer's alpha goes to its row's surface.
- If `alpha[Gravel] > 0`, `alpha[DryMud]` goes to `dirt_track` instead of `dry_crust`.
- Then the argmax, ties to the lower id, as now.
- The note in `map.json` (`ground_classes`) states the rule.

**Expected after the re-export** (derived from the measured map):
- The main tracks' cores read 100 % `dirt_track`: every main-track sample carries Gravel ≥ 0.2 × its paint's coverage.
- About 0.5 % of the map moves from `dry_crust` to `dirt_track`: 2,120 m of main track along its control points × 6 m = 12,700 m², × 42 % (derived).
- Round the ruins, `dirt_track` replaces `dry_crust` where the generator painted its rubble gravel (its rubble noise over 0.4, about half; assumed). The rest stays `dry_crust`. Both are compacted ground.
- Everything else is unchanged.

**Not a class matter, but W3 and W4a must know:**
- The fields' furrows exist only in the texture and its normal map. The 0.977 m heights can't hold rows about 0.5 m apart (K§7.2's ridges, 0.05 m high at 0.5 m, assumed).
- The package has what a world-derived furrow relief needs: the `tilled` class and each field's `furrow_axis`.
- Whether the sim adds that relief is a W3/W4a decision. Under REAL-05 (no texture or normal-map fakes for anything with a physical role), it should, with the render following.

## What gets logged
- **The map's identity** in the header (format 73, reserved for W2): the package's manifest hash. It covers every file's SHA-256, so the heights, classes and holes are all in it.
- **`QueryVersion` 8** for the heightfield's math.
- **A `tunnel` event** for any part carried through a step wall or the heightfield.
- No random stream: the ground is the world's.

## Open question for the orchestrator (not physics alone)
**The world's edge.** The far ring ends 4.4–4.6 km from the start, against a 10 km spool. D-091 wants no invisible wall, and the sim can't invent ground the scene doesn't draw. The options:
- **Extend the far ring in the scene:** + the edge leaves reach · − scene work, against D-088's "as it is" · risk: low.
- **End the flight at the edge with a logged `world_edge` event:** + simple · − an invisible wall in effect.

Until it's decided, W3 reports off-world points as `Hole` with height −∞, as `MapWorld` does over a hole.
