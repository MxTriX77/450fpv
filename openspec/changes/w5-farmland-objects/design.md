# Design: w5-farmland-objects

**Owner of this note:** asset-artist (task 1.1, E01's filler band) · **For:** world-builder (tasks 1.2, 1.3: the hole mask and its validator) · **Requirements:** WLD-16, AST-05, REAL-05 · **Decisions:** D-004, D-090, D-095 · **Source:** W2's design §4, "The hole mask: signed off, with three rules for W5" (`openspec/changes/w2-farmland-export/design.md`)

Every number is labelled: *measured* (read from the built metadata, the build's audit or a bench), *derived*, *assumed* or *published*.

## 1. E01's filler band (task 1.1)

### 1.1 The rule it has to hold
W2 §4 sets two rules for the farmland's hole mask:
- **Rule 2:** a hole cell is a cell that meets the trench's opening (`top_width_m` about the centre line, with its ends).
- **Rule 3, no naked hole:** every point of every hole cell lies inside the asset's filler band, where its shapes are the only ground.

The farmland's hole cells are 1000 / 1024 = 0.9765625 m (derived: the map tile's 1000 m over Unity's hole grid, `holesResolution` = 1024).

### 1.2 The band's derivation
- **A cell that meets the opening lies within its diagonal of it** (derived). Every point of the cell is within the cell's diagonal, √2 × 0.9765625 = 1.381 m, of the point where it meets the opening.
- **So a band covers every hole cell** when it reaches 1.381 m past the farthest the opening reaches from the band's centre line (derived). The band is every point within `band_width_m` / 2 of its centre line.
- **The opening is taken as drawn, not as `top_width_m`** (derived).
  - The run's lip stands out by up to `width_jitter_m` / 2 = 0.025 m and crumbles back by up to `lip_crumble_m` = 0.1 m. So it reaches 0.4 + 0.025 + 0.1 = 0.525 m from the centre line, and the generator holds it there on the bends' outsides.
  - The test range's cut records read 0.520 m (measured, p7c-wind design).
  - A mask built from the record's 0.4 m and a mask built from the drawn lip both lie inside a band built from 0.525 m.
- **A niche's opening** is its plan footprint: 0.4 m (`niche_m.w` / 2) about its own centre line, from the run's centre line to the niche's back, ends included (derived; the niche's width has no jitter).
- **A seam of 0.02 m** (assumed, `HOLE_SEAM_M`): the map rounds centre lines to the millimetre and the validator samples points on the cells' edges, so no boundary case decides. It also takes the niches' band past the proposal's 3.6 m.

Each band's margin past its opening is therefore the larger of F-2's 0.65 m and the cell's diagonal plus the seam:

| | Opening's reach from its centre line | Margin | Half-width | `band_width_m` |
|---|---|---|---|---|
| Run, before | 0.525 m (derived) | 0.65 m (F-2) | 1.175 m | **2.35 m** (measured, HEAD's metadata) |
| Niches, before | 0.4 m (derived) | 0.65 m (F-2) | 1.05 m | **2.1 m** (measured, HEAD's metadata) |
| Run, after | 0.525 m | 1.381 + 0.02 = 1.401 m (derived) | 1.926 m | **3.852136 m** (measured, rebuilt metadata) |
| Niches, after | 0.4 m | 1.401 m | 1.801 m | **3.602136 m** (measured, rebuilt metadata) |

Both are at least 3.6 m wide, with a half-width of at least 1.781 m, as the task asks.

The build's F-2 line confirms the margin on the real geometry. The cut's faces, the crumbled lip and the niches included, lie at least 1.4011 m inside the bands (measured). That is more than the cell's diagonal, 1.381 m. So every cell that meets the drawn cavity lies inside the bands.

### 1.3 What changed in the generator and the params
- **New param `hole_cell_m`** (`assets-src/earthworks/E01_curving.json`: 0.9765625, the farmland's; range 0–1 m).
  - It is the hole cell of the map E01 is placed on.
  - A map with other cells is a params edit, rebuilt by `pwsh tools/build_asset.ps1 E01` (D-004).
- **The bands** (`tools/blender/generators/trench.py`, `Run.margin`): each band reaches max(`BAND_MARGIN_M`, √2 · `hole_cell_m` + `HOLE_SEAM_M`) past its opening. This replaces the fixed `BAND_MARGIN_M` in three places: the run's band, the niches' bands and the belt side's backing.
- **The backing** follows the bands. The lip boxes still reach `BACKING_M`, 0.75 m, past every band's edge. So F-1 holds unchanged for a map that marks 0.5 m cells by touching the band.
  - Field side: 1.925 → 2.676 m from the centre line. That is still under the field parapet, whose outer toe stands 3.2 m out (derived: 0.4 + `berm_m` 0.3 + `parapet_base_m` 2.5).
  - Belt side: 2.9 → 3.644 m. The niches' bands end 1.093 + 1.801 = 2.894 m out, and the niches' backs are 1.083 m and 1.093 m out (measured, their centre lines). So the belt backing passes the belt parapet's outer toe.
- **The belt backing past the parapet's outer toe** (`_lip_boxes`). This is the one new kind of box.
  - The lip box whose top is y = 0 now stops `BERM_SEAM_M` (0.05 m) inside the parapet's outer toe. There it lies under the parapet's slope, 2 cm under its face (derived: 0.05 m × tan 22°).
  - Past that, a backing box goes on to the backing. Its top matches the lip box under the berm (`BERM_BOX_M`, 0.3 m down, or 0.3 m over a niche's ceiling), so it sits under the margin columns.
  - So no `belt_bare` top lies level with the columns' `belt_straw` at y = 0. The existing test that "every top a contact meets first outside the cut is grassed ground, never tied within 1 mm" passes on its 5 cm grid.
- **Every lip box stays at least `FILLER_M` long** (`_lip_box`).
  - On a bend's inside, 3.6 m out on a 3.7 m radius, the mitre planes close in. The first build's new backing boxes came out 0.242 m long, and F-5 failed (measured).
  - Such a box is now lengthened about its middle to 0.3 m. It stays buried under the parapet and the columns.
- **The margin columns** follow the backing as before. They are cut out within the narrower backing less 0.05 m, now 2.626 m from the run instead of 1.875 m, so they need fewer splits.
- **Nothing drawn changes.** The bands and the fillers are collision and metadata only. Against HEAD's metadata (measured):
  - the LODs, the micro meshes, the gaps and the bounds are identical;
  - the FBX is byte-identical, and git sees no change to it.

### 1.4 The physics metadata, before and after (measured)
| | Before (HEAD) | After |
|---|---|---|
| Collision shapes | 831 | 878 |
| `surface:belt_bare` fillers (not landable) | 383 boxes | 432 boxes |
| `surface:belt_straw` ground (landable) | 420 boxes, 4 cylinders | 418 boxes, 4 cylinders |
| `surface:crater_spoil` clods | 24 spheres | 24 spheres |
| Hole records | run 2.35 m; niche_0, niche_1 2.1 m | run 3.852136 m; niche_0, niche_1 3.602136 m |
| Gaps (`xs_`, `slot_`, `mouth_`), wind volumes, snag | 71, none, none | unchanged |

What moved between the two:
- **Fillers +49:** one backing box per belt-side segment, past the parapet's outer toe.
- **Fillers re-sized: 124:**
  - the lip boxes beyond the toe: longer on the field side, stopped at the outer toe on the belt side;
  - the end blocks and their rings;
  - the niches' boxes behind and beside them.
- **Ground −2:** six margin columns that were split to 1.03 m become four whole 2.01 m columns. The two end berm boxes grow from 1.93 × 4.82 m to 2.68 × 6.32 m, over the larger end blocks.

**E01's solid is unchanged.** On a 5 cm plan grid at eleven heights from +0.3 m to −1.7 m (193 875 points each; measured with a point-in-shape test over the metadata), not one point changed between inside some shape and inside none. Only which overlapping shapes fill it changed:
- at y = −0.01 m, 9 657 points gained a `belt_bare` box under the `belt_straw` shapes (the longer lip boxes under the parapets);
- no point at or above y = 0 changed material.

The hole contract on the rebuilt asset (lib.holes in the build, measured):
- F-1: 61 473 rays, 0 miss.
- F-2: 1.4011 m least inside.
- F-3: 25.0 mm, the ring past the ends.
- F-4: 0.0000 m.
- F-5: every box at least 0.300 m; 48 joints at 0.200 m.
- F-7: 432 fillers, 0 landable.
- F-8: no wind volume.

The collision audit (lib.audit) holds every primitive within its 50 mm, or 20 mm near the gaps and landing faces. The build printed `build.py: OK`.

### 1.5 The band is real ground
Over the widened band the ground is the asset's own drawn surface, and its collision follows it as before (REAL-05):
- the berm's boxes (`belt_straw`, landable, top y = 0);
- the parapets' slope and crest boxes, and their end cones;
- the end's berm box past each run end;
- under them, the `belt_bare` lip boxes and backing (F-1, F-3).

The widened band moves no visible face.
- **Where its edge lies:** 1.926 m out on both sides, on both parapets' crests (derived: the field crest runs 1.814–2.086 m out, the belt crest 1.443–2.457 m).
- **At that edge** the fillers' top is y = 0 (F-3), under the parapets' own boxes.

**The review sheet** (`review-sheets/E01/E01_curving_day.png` and `_overcast.png`, local, looked at):
- At 1.5 m along the run, 10 m, 50 m and 200 m, the trench reads as before: the cut between its parapets, the berm and the lodged mat, a dark winding line at 200 m.
- The turntable shows the same patch.
- The collision overlay draws the berm and parapet boxes over the drawn ground, as before. The new boxes are underground.

**A plan view of the bands** (scratch, not committed) shows three things:
- the farmland's cells that meet the opening at yaw 0, all inside the new bands, while several reach past the old ones;
- the niches' bands bulging behind each niche;
- the buried backing boxes past the belt parapet's outer toe.

### 1.6 No naked hole, sampled
`test_the_bands_hold_the_farmlands_hole_cells` places the built E01 on the farmland's grid at five yaws and offsets (0°, 17°, 45°, 133°, 271.5°).
- It takes every cell that meets a stadium of the opening's reach: the run's 0.525 m and the niches' 0.4 m, with round ends. That is the stricter reading of rule 2.
- It samples each cell every 0.1 m, edges included, against the bands.
- **Result:** 0 naked samples at every placement (measured; 65 hole cells at 0°, 69 at 45°).
- **The old bands fail it:** 865 naked samples at 0° and 1 077 at 45° (measured), so the test tells the two apart.

### 1.7 What changed for E01's other uses
**The test range** (`maps/test-range/placements.json`, regenerated by `python tools/map/make_test_range.py`):
- The trench row's three hole records carry the new `band_width_m`: 3 lines of the file.
- Nothing else in the file moves. E01 stands inside its own ground patch, whose shapes were already the only ground there, and the floor cuts are the patch's footprint.
- Its cut records still pass `cut_problems`: the lip width comes from the slots, the end walls close the ends inside the band, and the niches are roofed. `pytest tools/map/tests/test_test_range.py` gives 11 passed.

**The goldens.** The range's content hash moves from 0xF07BA3CC79D02169 to 0x48DBD8F70402A13D (measured). Each case was audited against HEAD's map with a scratch copy of the worldbench, which dumped every input's result on both maps.
- **Unchanged:** `test-range.ground`, `micro`, `micro_overflow`, `wires`, `wires_overflow`, `snags`, `snags_overflow` and the wakes. The micro cases are unchanged, so the wider band drops no other instance's micro-detail element.
- **Re-recorded:** `test-range.contacts`, `contacts_overflow`, `contacts_nowires`, `rays` and `rays_nowires`; `query_version` stays 7, because no query changed.
  - Capsules, 1 078 of 10 000 differ, all in E01's spot and the posadka's spot that crosses it:
    - 519 make the same contacts with another shape's index;
    - 535 start inside the solid, where the overlapping shapes' internal faces changed;
    - 18 start outside and keep the same first touch, adding contacts deeper in;
    - 6 start under the block's bottom (y < −1.85 m). There, coplanar bottom faces of a column and a lip box swap which comes first, or, in one case (4138), an internal edge of the old end block is gone.
  - Rays, 891 differ:
    - 559 hit the same point, at the same distance and material, on another shape's index;
    - 328 start inside the solid;
    - 4 start under the block and hit its bottom face at the same point and distance. The coplanar faces there swap `belt_straw` for `belt_bare`.
  - None of these is a surface the drone can reach. The range's ground plane is absent over E01's patch, and the patch's bottom lies 1.85 m down.
- **Flightbench:** `range-flight` flies its 12 000 steps on S04's straw bit for bit (RangeHash 0xAF745CC52447E017 matches). Only the pinned map hash `GoldenFlights.RangeContent` was re-pinned, with this audit in its comment.

**The gallery** (`GalleryStations`, station 5) places the prefab on its flat floor and reads no band. The prefab's EditMode test (`TrenchPrefabTests`) now expects the new widths.

**The object sheet** (`docs/object-sheets/earthworks-and-surfaces.md` §E01) still derives the band for 0.5 m cells, 2.3 m. That sheet is the reference analyst's; the handoff suggests a note there.

### 1.8 Open points for the world-builder (W5 1.2)
- **Build the mask from the drawn opening,** 0.525 m about the run's line, not from `top_width_m`'s 0.4 m.
  - A cell that meets the drawn lip between 0.4 and 0.525 m, but not the 0.4 m opening, would keep its terrain over up to 0.125 m of the cut's edge: W2 §4's sod roof, only narrower.
  - The band holds either reading.
- **The niches' records** carry `top_width_m` 0.8 about a centre line that ends at the niche's back. Their cells are holes too (W2 §4, rule 2), and the band holds them, ends included.

## 2. Placement, the hole mask and the footprints (task 1.2)

**Owner of this section:** world-builder · **For:** world-builder (1.3, applying at load), physics-engineer (the W3 merge), tech-artist (the drawn edge), qa-engineer (2.1) · **Decisions:** D-090, D-092, D-094, D-095 · **Source:** W2 design §4, rules 2 and 3; §1 above.

### 2.1 What the placer writes
`python tools/map/place_objects.py maps/farmland` runs after the export, which drops our rows. It is idempotent: a second run gives the same bytes (measured). It writes four files, each listed in `map.json` with its SHA-256:
- **`placements.json`:** our 15 rows as ids 1679–1693, after the export's 1,679, and our five variants in the sorted variant list.
  - The export's own bytes are kept: the placer renders the file exactly as the export writes it.
  - It refuses to write if a fresh export doesn't round-trip, and a test checks the committed file.
- **`holes/map.raw`:** the map tile's mask, 1024 × 1024 bytes in LFS, 255 a hole, row 0 the northmost (W2 §4's encoding, signed off). `map.json`'s map tile gains `"holes": {"file": "holes/map.raw", "resolution": 1024}`.
- **`footprints.json`** (`fpv450-map-footprints` v1, new): per object its corners, `level_sample` and `level_m`, and the samples it writes, as runs `[file row, first column, [samples…]]` of the map tile. 20,548 samples in all, 121 KB.
- **`map.json`:** counts `trench` 11, `house` 2, `car` 2, and `"objects": {"placer", "seed": 90, "footprints"}`.

**The package's format stays version 1.** Every addition is optional and additive: the `holes` field W2 already specified, a new file with its own format name, and three new kinds. Nothing here touches a log or the sim, so the log's format 73 (reserved for W3's map identity in the header) is not taken.

### 2.2 The placements
- **The streams:** SplitMix64 (as the height probes draw), one per object, seeded with 90 (assumed) and the object's name mixed in (`E01_curving/F3`, `H01_D/village`).
  - Each draws a spot and a yaw until the spot passes every rule (at most 20,000 draws).
  - Changing one object's rules moves no other.
- **The footprint** is the plan rectangle of the variant's bounds:
  - E01's whole ground patch: 35.25 × 13.75 m, the block from y = 0 down;
  - a house or car with its spilled rubble and parts: H01_D's 16.2 × 20.2 m, R01_A's 6.6 × 4.5 m.
- **The trenches:** a spot in the field's box and a yaw in [0, 360), until the patch lies inside the field at least 11.5 m from its outline. That margin is assumed: the edits' 8.5 m reach, then the friend's 3 m house-to-track clearance.
- **The village objects,** H01_I, H01_D, R01_A, R01_F in that order: a spot in the village circle (75 m about (512, 600)) until the footprint lies inside it.
  - A house turns its front (asset −Z, as in the test range) to the lane through the village's centre, z = 600, within ±6°. So its yaw is about 0 north of the lane and 180 south of it.
  - The ±6° is published: the friend's five ruins turn 176–186° and −5–4°, facing their lane.
  - A car takes any yaw.
- **Free ground** (`clear()`):
  - No edit reaches the ground an exported object rests on: a ruin's or debris piece's plan bounds, or a tree's or bush's pivot. Each lies at least the 8.5 m reach plus a cell's diagonal (1.381 m) from the footprint, so no tree or ruin floats or sinks when the edits apply.
  - A tree's or bush's plan bounds (its crown) stays 3 m off the footprint (published: `HouseTreeClearance`).
  - The start is left untouched: the footprint lies at least the reach, a cell's diagonal and the stand's 0.297 m footprint from it (derived from `drone.json`). The start is D-092's `FlightWorlds.FarmlandStart` at (566.5, 569.0); a test reads the C#.
  - Our footprints lie at least 17 m apart, two reaches, so no sample is edited twice.
  - A house's footprint stays 3 m off every `dirt_track` sample (published: `HouseTrackClearance`; here the class map traces the tracks).
- **Relief:** the ground's samples under a footprint span at most 1 m for a trench or a car, and 2.5 m for a house (both assumed).
  - Under 1 m, the cut and fill stay near ±0.5 m, about E01's 0.45 m field parapet, so the levelled patch reads as spoil rather than a terrace.
  - Measured over 60 free spots per field: the median patch spans 0.9–2.3 m, and only 2–70 % of spots are under 1 m. Unconstrained, the first draws cut up to 1.7 m into a field.
  - H01_D's footprint spans at least 1.95 m anywhere free in the village (measured); the friend's own plots are levelled on that hill.

**As placed.** Map coordinates in metres. "Level" is the row's y; "clear" is the least plan distance from the footprint to any exported object's bounds; "start" is from the start's foot to the footprint.

| Id | Variant | Where | Pivot (x, z) | Yaw ° | Level | Relief | Most moved | Hole cells | Clear | Field edge | Start |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1679 | E01_curving | F1 | (96.88, 197.01) | 123.2 | 22.630 | 0.96 | 0.70 | 66 | 26.2 | 15.4 | 591.0 |
| 1680 | E01_curving | F2 | (188.07, 70.45) | 93.7 | 31.698 | 0.84 | 0.56 | 60 | 9.0 | 12.4 | 616.6 |
| 1681 | E01_curving | F3 | (274.43, 92.67) | 86.0 | 36.451 | 0.68 | 0.49 | 67 | 15.4 | 13.8 | 549.4 |
| 1682 | E01_curving | F4 | (446.99, 345.06) | 11.0 | 36.740 | 0.69 | 0.42 | 62 | 16.3 | 84.1 | 239.2 |
| 1683 | E01_curving | F5 | (486.16, 99.90) | 276.9 | 35.602 | 0.79 | 0.50 | 61 | 52.5 | 48.1 | 446.4 |
| 1684 | E01_curving | F6 | (253.37, 664.47) | 313.4 | 21.179 | 0.67 | 0.46 | 67 | 33.7 | 19.0 | 309.8 |
| 1685 | E01_curving | F7 | (351.19, 854.80) | 23.3 | 23.197 | 0.68 | 0.46 | 63 | 52.7 | 62.7 | 329.6 |
| 1686 | E01_curving | F8 | (650.40, 827.82) | 175.1 | 28.984 | 0.68 | 0.46 | 61 | 9.9 | 88.0 | 254.7 |
| 1687 | E01_curving | F9 | (838.78, 844.71) | 182.6 | 24.281 | 0.59 | 0.48 | 64 | 8.2 | 14.2 | 362.5 |
| 1688 | E01_curving | F10 | (684.00, 412.11) | 303.2 | 31.295 | 0.63 | 0.42 | 66 | 12.1 | 43.8 | 180.4 |
| 1689 | E01_curving | F11 | (697.37, 79.22) | 267.9 | 25.142 | 0.94 | 0.61 | 62 | 17.8 | 38.3 | 476.7 |
| 1690 | H01_I | village, south edge | (473.25, 546.29) | 177.9 | 32.316 | 0.68 | 0.60 | 0 | 19.6 | | 90.3 |
| 1691 | H01_D | village, north-west | (486.70, 653.18) | 3.0 | 26.413 | 2.16 | 1.41 | 0 | 5.1 | | 102.0 |
| 1692 | R01_A | village, south-east | (544.14, 545.12) | 272.9 | 37.850 | 0.99 | 0.82 | 0 | 11.3 | | 28.8 |
| 1693 | R01_F | village, west edge | (441.72, 613.72) | 238.4 | 25.937 | 0.38 | 0.41 | 0 | 10.1 | | 128.9 |

Every number is measured, from the placer's printout and a scratch script over the package. An E01's pivot is its run's first centre-line point, not the patch's centre.

### 2.3 The hole mask
- **Rule 2, as §1.8 asks:** a hole cell is a closed cell square that meets a stadium about one of E01's hole records' centre lines. The lines are placed by the row's yaw and position as stored (rounded).
  - The run's opening is drawn, not `top_width_m`: 0.525 m (derived from E01's params: `top_w_m` / 2 + `width_jitter_m` / 2 + `lip_crumble_m`). The params file matches the metadata's `params_sha256`.
  - A niche's opening is its width's half, 0.4 m.
  - **699 hole cells,** 60–67 per trench (measured; §1.6 found 65–69 on the test grid).
- **Rule 3, no naked hole:** every hole cell sampled every 0.1 m, edges included, lies within a band's half-width of its record's centre line.
  - That is 121 points a cell, 84,579 in all, and **0 naked** (measured).
  - With the old bands (2.35 m, 2.1 m at the niches) the same cells give 8,353 naked samples, so the check tells them apart (measured, `test_the_old_bands_leave_naked_holes`).
- **None of Unity's 1,000 height probes lies in a hole cell** (measured).

### 2.4 The footprints
- **Levelled as the friend levels its house plots** (`LevelVillageGround`, published):
  - the level is the mean of the samples inside the footprint, rounded half up to a sample in integers;
  - a sample within 1.5 m of the footprint (`HouseGroundMargin`) takes the level;
  - farther out it eases back to its own over 7 m (`HouseGroundShoulder`) with Unity's SmoothStep, rounded to a sample.
- **Every cell that meets a footprint is level.** The 1.5 m margin exceeds a cell's diagonal, 1.381 m, so all four corners of any such cell take the level (derived; the validator checks every cell).
- **The cut is flush:** every hole cell's corners stand at its trench's level. So the terrain meets E01's ground (its patch top, y = 0) along the cut's edge (checked).
- **Each row stands at its level** within 0.05 mm: `level_m` is rounded to 0.1 mm, and the spec asks 1 cm.
- **The most any sample moves** is 0.42–0.70 m under a trench, 0.41–0.82 m under a car and 1.41 m under H01_D (measured).
- **No edit reaches a tile's edge,** so the seams with the surround stay equal sample for sample (checked).
- **The heights files are untouched.** 13 of Unity's 500 map probes lie on cells the footprints edit (measured). `validate.py` still checks the probes against the files as exported.

### 2.5 What `validate.py` checks
`place_objects.check` runs whenever `map.json` records `"objects"`:
- **The same seed gives the same rows:** it places again with the recorded seed and requires equality.
- **One trench inside each field** with its 11.5 m margin, **the village's four** inside the circle, and **every one on free ground** (`clear()` against the export's rows, the start and the other 14).
- **The mask equals the one the trenches derive,** and rule 3 holds: 0 naked samples.
- **`footprints.json` equals the footprints the rows derive:**
  - every cell that meets a footprint is level;
  - every hole cell's corners are at its trench's level;
  - no edit reaches the tile's edge;
  - every row stands at its level within 1 cm.
- **Any tile's mask** is N × N bytes of 0 or 255, its resolution N = res − 1.
- **Rows of ours with no objects record** are refused.

It takes about 10 s on the farmland (measured). Its tests:
- `test_place_objects.py`, 17 tests. Each break is caught: a moved row, a missing row, another seed, a flipped hole cell, an edited sample, the old bands.
- `test_validate.py` adds the mask's breaks.

### 2.6 For 1.3 and the W3 merge
- **W3's loader refuses our rows today.** On `unity/physics/physics-integration-2--w3-objects` (4a9dc631), `MapWorld` finds a package row's metadata only through the vendor lists (`VendorPlants`, `VendorRuins`). It throws "variant … is in no vendor list" for `E01_curving`, and its `FarmlandObjectTests` count 1,679 rows.
  - So merging W3 into this branch needs more than golden re-records.
  - 1.3 adds `assets-src/catalog.json` as a third list. Its `assets` have the same `{class, variants}` shape, and no variant appears in both (checked). The counts become 1,694.
- **W3's `Terrain` already reads the map tile's `holes`** (W2's encoding). After the merge the sim cuts the 699 cells at once, so E01's shapes must load in the same step, or those cells are a fall through the world.
- **The footprints are new to W3.** 1.3 writes `footprints.json`'s samples over the map tile's heights before building the heightfield.
  - The 13 probes on edited cells then differ from Unity's.
  - W3's probe golden must read the files as exported, or leave those 13 out.
- **The content hash moves** (`map.json` and `placements.json` change), so every farmland golden re-records, each audited.
- **The drawn terrain under E01's patch** lies exactly at the patch top's level (y = 0), and the patch's tussock relief is 4 cm (`RELIEF_M`). The tech artist should check for z-fighting between the runtime terrain copy and the patch top in 1.3's drawn check.

### 2.7 Review shots
One top-down plot per spot (scratch, not committed; PIL over the package). Each shows:
- the edited terrain in 0.25 m height bands with a hillshade, the level pad flat;
- the hole cells in black and E01's centre lines in red;
- the footprint in white and the edits' reach in yellow;
- the neighbours' plan boxes and pivots, the field outline or village circle, and the start.

Looked at, all 15:
- Every trench's hole cells follow its run and niches, inside its levelled patch, with the shoulder easing into the field's slope.
- No tree, bush or ruin pivot lies in a reach.
- The houses sit at the village's south and north-west edges, facing the lane.
- The cars stand on the meadow at the village's south-east and west edges.
- The drawn check comes in 1.3.
