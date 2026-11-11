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
