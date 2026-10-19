# Tasks: p1-e01-trench

Branch `unity/art/p1-e01-trench` off `unity/main` once `p1-l01-l03-pole-wires` is merged (lane A). Tasks 1.1–1.2 run sequentially on the branch; 1.3 runs on the sub-branch `unity/art/p1-e01-trench--capture` in parallel with 1.1 (it touches only `ReviewCapture.cs` and its test) and is merged before 1.2. The trench is war terrain: spawn these agents on Opus.

## 1. Build

- [x] 1.1 [asset-artist] Pin the loam set (design 7) at 1k, write `trench_wall.json` and its wall-space derivation (strip map, wall mask, spade-facet normal), build the `trench` generator with `PARAMS`, `check` and `E01_curving.json` on `lib/ground_patch` and `lib/straw` (design 1–5, 8, 10), the `holes` record in `meta.py` and `AssetPhysicsMeta` (design 6), and the E01 catalog entry; verify `python tools/assets/fetch.py --verify` exits 0, `python -m pytest tools/blender -q` passes with tests for the filler contract (sealed from below on a 5 cm grid, faces, crumbled lip and niches 0.65 m inside the bands and the rays 0.71 m past them, thickness, overlap, level lips, nothing proud), the cross-section gap's inscribed circle (0.75 m ± 5 mm for the default section), the `holes` validation and the `check` refusals, `pwsh tools/build_asset.ps1 E01` exits 0 with the audit lines pasted, the triangle count per LOD printed, and a second run leaves `git status` clean
  **As built:**
  - **Loam set:** Poly Haven `dirt_floor` (CC0) is pinned at 1k. Its 2.07 m tile is published by the API. `forest_ground_04` is not pinned: the choice between the two stays task 1.2's.
  - **`trench_wall.json`:** kind `ground` with `"space": "wall"`. `derive.py` composes it on `lib/strip.py`'s strip, 27 × 4 m at 2048 × 1024 (76 × 256 px/m), u along the run and v up the map.
    - Rows, bottom to top: floor, field wall, belt wall. The end walls take u past 24.6 m (`run_m` is capped there); the niches' faces take the wall's map at their own chainage.
    - Base: `dirt_floor` and `brown_mud_02` blended at 0.5 and gained to #4a463f.
    - Wall mask: the topsoil band #33302b over the params' `topsoil_m` (its edge wandering ±8 cm); 14 root traces a metre, at most 0.2 m from the lip (#5e4f3e, assumed); the clod spill #8a7660 at 0.6 in the bottom 0.2 m.
    - Normal: spade facets 0.2–0.3 m across, at most 2 cm proud.
    - Measured: the walls #4c463d, the floor #4a463f.
    - The importer tiles a wall-space recipe 1/u × 1/v with no offset. The block's sides take the same map in a `section` slot: a topsoil-over-subsoil cross-section.
  - **The run** (`E01_curving`, seed 1): 24 m in 45 segments.
    - Four bends of −28.7°, +24.9°, −28.4° and +27.2° on 4 m arcs (assumed). Each arc is cut into 6 pieces of at most 5° and 0.5 m.
    - Two belt-side niches, at 3.04–4.04 m and 16.14–17.14 m: the nearest straights to 5 m and 15 m, clear of the arcs.
    - Sides: looking along the centre line from its first point, the field parapet (0.45 m) is on the right and the belt parapet (0.3 m) and the niches on the left, as the capture of task 1.3 reads them.
    - The patch is 35.5 × 13.75 m, a closed block 1.85 m deep.
    - Triangles: LOD0 39,417, plus 11,784 in the floor's straw micro mesh (491 straws and twigs); LOD1 39,417 at 8 m; LOD2 1,800 at 60 m; culled at 400 m.
  - **Collision:** 549 shapes.
    - 249 fillers (`surface:belt_bare`, not landable): per segment the floor box, the wall boxes and the lip boxes; each niche's sill, ceiling, back and side boxes; the end walls; the end blocks. The lip boxes reach from the lip to the block's bottom and out 1.9 m (field) and 2.85 m (belt, the niches' bands): they are the backing as well.
    - 276 landable `surface:belt_straw` shapes: the parapets' slope and crest boxes per straight or arc piece; at each run end a cone cap of 16 sector boxes and a crest cylinder; the margin columns.
    - 24 clod spheres (`surface:crater_spoil`).
    - A box along the run reaches past each joint so that every corner of its end face stands 0.1 m past the mitre plane (0.2 m overlap at all 44 joints, printed).
  - **Gaps and holes:** 44 cross-sections (`xs_<chainage>`) and 45 top slots (`slot_<chainage>`). The holes are `run` (46 points, band 2.3 m), then `niche_0` and `niche_1` (2.1 m). The ids are local to the asset rather than the design's `E01_curving`.
  - **Pipeline:**
    - `lib/holes.py` prints and enforces F-1…F-5, F-7 and F-8 in the build; `build.py` also prints a generator's own `lines` (the F-5 joints).
    - `lib/audit.py` prunes its lookups by bounding box (the 12 existing variants' audit lines byte-identical).
    - For an asset with `holes`, the audit clips a primitive's cells exactly by the boxes that cover part of them. A whole E01 build now takes 17 minutes, most of it the audit, where the audit alone had run over an hour; the other assets are untouched.
  - **Deviations from the design, for the review:**
    1. **The crumble.** It is a per-side walk of the lip's inset, 1–10 cm and changing at most 3 cm a metre. It leans each wall's top back (a twisted wall within 1 cm of its box), not a 0.1 m jagged inset on 0.1 m cells, which boxes cannot follow within the lips' 2 cm. The 1 cm floor keeps the parapets' toe inside the gaps' 0.3 m zone: with it exactly on that zone's edge, the audit's search never ends.
    2. **The grid.** 0.25 m cells, refined to 12.5 cm near the run and over the parapets and to 6.25 cm at the parapets' ends. The lip itself is cut exactly by the slot polygon.
    3. **Reuse.** `lib/ground_patch` is not reused (its grid is S04's bare-patch stepping); the trench reuses `lib/mat`'s relief and `lib/straw`'s kit.
    4. **Unbuilt states.** `check` refuses `zigzag`, a fresh `age` under 0.5, and any `revetment`, `collapse` or `overgrowth`: none of them is built yet.
  - **Seen on the review sheet, for task 1.2:**
    - At 10 m the cut reads as a narrow dark slot with a wavering lip, the far wall's topsoil band and root fringe, and broad humped parapets. At 50 m it reads as a thin dark winding line.
    - `belt_straw` as it is brings S04's dark bare-patch ovals and green strips onto the parapets and margins, repeating every 12 m. They are stains with no step under them.
    - The flat berm shows a pale band at grazing angles.
    - The 1.5 m tile still uses the default framing and shows the raised block's side (task 1.3 fixes it).
- [ ] 1.2 [asset-artist] Look at it (after 1.3): the review sheet `review-sheets/E01/` at 1.5 / 10 / 50 / 200 m, the turntable and the collision overlay, and the local side-by-side sheet `reference/_compare/E01.png` against letters G (1–12) and B (1–6) built inside this task from the contact sheets, never committed; tune the sheet-ranged params by eye (bend rhythm, parapet heights, lip crumble, wall tint) and commit the params; verify the pilot-height tile reads as G's dark slot between pale parapets and the handoff lists what matches G and what does not
- [ ] 1.3 [tech-artist] The earthwork framing of the review capture (design 9): for catalog class `earthworks` with a `holes` record, the pilot-height tile is shot from 1.5 m above the lip at the run's start looking along it 20° down, the pose printed in the log, an EditMode test on the pose; verify the K01 and S04 captures are pixel-identical before and after, and the E01 1.5 m tile shows the run once 1.1 lands

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios: the five E01 scenarios and the two deltas, the audit lines against the hole contract (F-1…F-8), the gap list against the sheet's width table, the comparison sheet against G and B read by eye (REAL-03), the geometry audit (REAL-05: lips crumbled as geometry, clods as geometry, fillers follow the faces), budgets (AST-08), `fetch.py --verify`, hygiene (D-003 pins, nothing from `reference/`, subject-only commits)
