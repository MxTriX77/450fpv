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
- [x] 1.4 [asset-artist] Before 1.2, task 1.1's two open points:
  - the parapets' and margins' look: S04's `belt_straw` recipe used as is stamps its bare-patch ovals and green strips every 12 m across the parapets, stains with no relief under them (REAL-05); give the parapets and margins their own grassed-spoil recipe (the physics stays `surface:belt_straw`), its patches only where the mesh has the relief that makes them
  - the audit's gap-zone search ends when a covered edge lies exactly on the 0.3 m zone boundary (a guard and a test), so the lip crumble is free to reach 0 where the design wants it
  Verify `pwsh tools/build_asset.ps1 E01` twice byte-identical, K01, S04, L01, L03 and V13 unchanged, the audit lines for F-1…F-8, the EditMode run, `python -m pytest tools -q`, the hygiene scan
  **As built:**
  - **The grassed spoil** (`assets-src/materials/grassed_spoil.json`): a ground recipe whose `patches` name only the lodging. `derive.py` now derives such a recipe as a cover (`COVER_FORMULA`): no bare patch, lip, green strip or relief tone.
    - It carries S04's straw cover, its gaps (0.25 at #3a3227) and its lodging (25°, 0.3) on the sheet's #b49c6a. Measured: luminance 0.3453 against 0.3452, gaps 24.3 %.
    - So the map stamps nothing. The only relief on the top is the mesh's own: S04's tussocks at half on the parapets, in full on the margins, none on the berm. The light finds it.
    - The map spans 40 m at 2048 px (51 px/m) and is laid once: the top's UVs are metres from the patch's centre (`GROUND_MAP_M`). `check` refuses a patch that could outgrow it (`run_m` + 2 `margin_m` + 2 cells over 40 m). Close up, the detail layer carries the straws.
    - The physics stays `surface:belt_straw`; E01's ground slot names `grassed_spoil`. `belt_straw` and its stamp are untouched.
  - **The pale band** is URP/Lit's environment reflection at grazing view (its Fresnel; the grazing reflectance is the smoothness plus 0.04) on the berm, the one level, relief-free strip of the top. On the parapets and margins the relief tilts the normals and breaks the reflection up; the berm reflects whole.
    - A/B on the review sheet with the cover's smoothness (0.42 mean, 1 − the set's roughness) set to 0: the band goes in overcast and nearly so in day. The 1.5 m tile's far ground drops 21 (day) and 33 (overcast) sRGB levels, against 8 on the near ground.
    - Not the recipe's doing: one smoothness over the whole top cannot single out the berm. The berm is level and relief-free by the generator (`RELIEF_CLEAR_M`). Left to 1.2 if G shows no band: about ±1 cm of relief on the berm, inside its 2 cm zone.
  - **The audit guard** (`lib/audit.py`, `ZONE_EDGE_CELL_M`): a cell at the 2 cm zone's edge no longer than 1 mm is not split and counts at its bound. The edge is resolved to a millimetre and errs towards the larger value.
    - The test puts a covered edge exactly on the zone's edge and boxes the search at 300 000 cells. The old code hit the box after 25 s, its cells 24 µm wide and still splitting; the new code ends in 3 s, at 10 mm on the edge.
    - The other 12 variants' audit lines are byte-identical with and without the guard.
  - **The crumble:** E01 with `lip_crumble_m` 0 puts the toe exactly on the zone's edge along every straight. It now audits to the end: 1215 s, 0 problems, the near zone's largest 15.5 mm. `CRUMBLE_MIN_M` stays 1 cm, now for the look only: an established lip never runs on its clean spade line (G), and `lip_crumble_m` 0, a fresh trench's sharp lip, takes the floor to 0.
  - **Build:** two runs byte-identical (17 outputs), their 693 audit, hole and joint lines identical; the geometry is task 1.1's, only the top's UVs moved.
    - F-1: 41 786 rays, 0 miss. F-2: the least 0.6500 m inside the bands. F-3: within 0.0 mm. F-4: the highest at 0.0007 m. F-5: 249 fillers at least 0.300 m, 0.200 m at all 44 joints.
    - F-6: worst 37.2 mm (tolerance 50), and 15.5 mm in the 2 cm zone (tolerance 20). F-7: 249 `belt_bare`, 276 `belt_straw` landable, 24 `crater_spoil`. F-8: 0 wind volumes.
    - Gaps: 44 cross-sections with `narrowest_m` 0.785–0.839 and 45 slots, each on the audit's circle.
  - **Verified:** K01, S04, L01, L03 and V13 rebuilt with `git status` clean; EditMode 54 of 54; `python -m pytest tools -q` 819 passed; `fetch.py --verify` OK; the hygiene scan 0 findings.
  - **Seen on the review sheet:**
    - 1.5 m: the dark slot winding away between pale straw humps. No oval or strip on either parapet. Straw grain near, an even mottle farther, the pale berm strip along the field lip.
    - 10 m: the narrow dark slot between broad pale humps. No repeat in the near ground.
    - 50 m: one even pale block, the thin dark winding line, the parapets' soft humps and their end caps.
- [x] 1.2 [asset-artist] Look at it (after 1.3 and 1.4): the review sheet `review-sheets/E01/` at 1.5 / 10 / 50 / 200 m, the turntable and the collision overlay, and the local side-by-side sheet `reference/_compare/E01.png` against letters G (1–12) and B (1–6) built inside this task from the contact sheets, never committed; tune the sheet-ranged params by eye (bend rhythm, parapet heights, lip crumble, wall tint) and commit the params; verify the pilot-height tile reads as G's dark slot between pale parapets and the handoff lists what matches G and what does not
  **As built:**
  - **The comparison** (local, never committed): `reference/_compare/E01.png` holds G 1–12 and B 1–6 over the day and overcast tiles, three turntable views and the collision overlay; `E01_before.png` holds the same at a4bf050. Every capture ran on AC on the RTX 4050 (the capture log's GPU line). Means below are luminance Y in linear light over hand-picked boxes, the G stills blurred 3 px against the feed's noise.
  - **Params changed:**
    - `E01_curving.json`: `bend_deg` 25 → 30. G's bends (G 4–6) read sharper than 25°; 30° is the most `check` lets legs of 5 ± 1 m take on the 4 m arcs. The run is now 49 points, 600 shapes, 95 gaps, LOD0 39,009 triangles.
    - `grassed_spoil.json`: `cover.gap_share` 0.25 → 0.35, the mottle. The sunlit parapet's spread (coefficient of variation) goes from 0.22 to 0.29, against 0.24–0.30 on G 3, 6 and 9.
    - `grassed_spoil.json`: `cover.smoothness_scale` 0.2, new (`derive.py`'s cover v2). The mask's mean smoothness goes from 0.42 to 0.08, a dry straw mat's.
  - **Kept, with the reason:**
    - `parapet_h_m` 0.45 / 0.3: G 6's field hump and G 9's flatter belt side.
    - `lip_crumble_m` 0.1, the range's top. G's ragged lips are grass hanging over them, not soil (below).
    - The wall tint #4a463f and the loam set `dirt_floor`. The inside reads Y 0.05–0.06 against 0.056–0.074 on G 3, 6 and 9, and G's walls show no grain at its height, so `forest_ground_04` is not pinned.
    - The cover's tint #b49c6a. The sunlit parapets read Y 0.36 against G's 0.32–0.36. G's orange is the low warm sun and the camera: from ours to G's, the shaded inside shifts per linear channel by R 1.05, G 0.87, B 0.57 and the sunlit straw by R 1.30, G 0.80, B 0.29, so the straw's extra is the sun's colour, not its albedo.
  - **The pale band:** G shows none, and no sheen on its far parapets at grazing view either. It was the cover's gloss over the whole top, which only the level berm showed whole.
    - With `smoothness_scale` 0.2 it is gone in both lights. A faint smoother strip stays where the berm and the inner slope are seen at grazing (the map's minification), no brighter than the parapets.
    - No berm relief: ±1 cm on a berm 0.2–0.3 m wide on 12.5 cm cells tilts its normals by about 5° at most. The band went only under the margins' ±4 cm and the parapets' 22° slopes.
  - **Matches G:**
    - A dark narrow slot winding between two broad pale parapets, each 2–4 times the cut's width, at 1.5 m and 10 m (G 1–12).
    - The inside's and the parapets' brightness (above), and alternate S-bends (G 4–6).
    - At 50 m a thin dark winding line (G 10–12's far run).
    - B 1–6: only the cut's dark slot. B's spoil is bare and lumpy (fresh, E07's look).
  - **Does not match G, for later tasks:**
    - **The lips.** Grass hangs over and breaks G's lips every 0.1–0.3 m (G 3, 6, 9); ours is a clean edge, 1–10 cm in.
    - **The parapets' grain.** G's are tussocks of standing dry grass with deep shadows: on the shaded side a spread of 0.45–0.48 with 12–14 % dark pixels (G 3, G 6), against our 0.37 and 4 %. At 10 m ours read as a fine even mat, sand-like.
    - Both want visual-only grass as micro mesh (S04's kit) on the lips and parapets. Any relief past ±2 cm would break the landable 2 cm zone.
    - **The colour.** G's straw is golden-orange and its sunlit wall warm brown (G 9); the sheet's neutral 45° sun gives beige. To check under the low sun and the feed.
    - **The bends.** G's look a little tighter. The params are at `check`'s limit; `BEND_RADIUS_M` and `LEG_JITTER_M` are constants.
  - **Build:** two runs byte-identical, `git status` clean after the second. F-1: 41,815 rays, 0 miss. F-2: the least 0.6500 m. F-3: 0.0 mm. F-4: 0.0007 m. F-5: 264 fillers at least 0.300 m, 0.200 m at all 47 joints. F-6: worst 39.6 mm (tolerance 50), 15.7 mm in the 2 cm zone (tolerance 20). F-7: 264 `belt_bare`, 312 `belt_straw` landable, 24 `crater_spoil`. F-8: 0 wind volumes.
- [x] 1.3 [tech-artist] The earthwork framing of the review capture (design 9): for catalog class `earthworks` with a `holes` record, the pilot-height tile is shot from 1.5 m above the lip at the run's start looking along it 20° down, the pose printed in the log, an EditMode test on the pose; verify the K01 and S04 captures are pixel-identical before and after, and the E01 1.5 m tile shows the run once 1.1 lands
  **As built:** the capture reads the run from the metadata JSON itself, not from `AssetPhysicsMeta`: the first `holes` record (the niches' records after it are ignored), its `centre_line` as `[x, y, z]` points in asset space with their y ignored (the lip at y = 0), and its `top_width_m`. The camera stands 1.5 m above the first point, half `top_width_m` out on the field side, which is the run's right looking along it (G: the strip on the left, the field on the right), so 1.1 puts the 0.45 m field parapet on the run's right; it looks along the first segment's plan direction, 20° down. The bar stands 3 m along the run and 0.2 m out from the lip on the field side, on the berm (assumed: past the 0.1 m crumble, inside the 0.3 m berm). An earthwork without a record keeps the pose beside its bounds. The log line is `[Review] earthwork pilot pose: …`. EditMode tests on a straight and a bent fixture, each with a niche record, and on no record. K01's, S04's and L01's 12 sheets are byte-identical before and after (sha256). The E01 1.5 m tile is still to be checked once 1.1 lands (task 1.2).

- [x] 1.5 [asset-artist] Task 1.2's look gaps against G, as geometry (REAL-05): the grass that overhangs and breaks G's lips every 0.1–0.3 m (G 3, 6, 9) and the parapets' tussocks with their deep shadows (12–14 % dark pixels on G's shaded side against our 4 %, G 3, 6), as visual-only micro meshes from S04's straw kit (no collision, skipped by the audit, the 2 cm landable zone untouched), inside the triangle budget; the bend radius and leg jitter as params with ranges read from G (task 1.2: G's bends look slightly tighter than `check`'s limit allows); verify the 1.5 m and 10 m tiles against G in the local `reference/_compare/E01.png`, `pwsh tools/build_asset.ps1 E01` twice byte-identical with the F-1…F-8 lines, the triangle count per LOD, the EditMode run, `python -m pytest tools -q`, the hygiene scan; G's warm straw and wall colour under low sun are the look setup's and the feed's check (G1 and phase 4), not this task's
  **As built:**
  - **The grass** (`trench._grass`): one visual-only micro mesh, kind `grass`, listed twice in the metadata (`E01_curving_LOD0_grass`, `E01_curving_LOD1_grass`). No collision; the audit skips it; the ground's mesh and its 2 cm zone are untouched.
    - **Why LOD1 as well:** LOD0 shows only within 8 m of the bounds' centre of a 35 m patch. The 1.5 m and 10 m tiles sit about 12 m and 10 m from it, so they show LOD1.
    - **The lips:** a tongue of grass every 0.1–0.3 m (G 3, 6, 9) along both side lips and the end walls, 251 of them (2,008 triangles). Each tongue is 8–18 cm wide. It starts 4–10 cm behind the lip, reaches 1–4 cm past it and 0.5–2.5 cm over it, and hangs 3–12 cm down the battered wall, 0.5–1.5 cm off it. Its hanging edge is rounded. Eight triangles.
    - **The tussocks:** 394 hummocks (11,820 triangles with their stems) at 2.3 per m², from 0.1 m past the widest lip to the parapets' outer toe, thinning to none 0.5 m past it.
      - Each has a 0.2–0.4 m radius and a crown 0.1–0.25 m up (at most 0.8 of the radius).
      - Six lobes reach 0.95–1.25 of the radius, with notches at 0.7–0.85 between them, sunk 1 cm. 24 triangles.
      - Each hummock stays off the cut, and has 0–2 of `lib.straw`'s stems (`tube`, 3–6 mm, 0.15–0.35 m) out of its crown.
    - **The material:** the tongues and hummocks are in the ground slot. Their UVs are the top's (metres from the patch's centre), so `grassed_spoil`'s map runs on over them and over the lip.
    - **Tried first, on the review tiles:** S04's kit as sheaves (fanned stems, fat lodged bundles, rim-to-crown domes) read as claws, agaves and spiders at this budget, and the lip sheaves as pins and flags. At about 85 triangles a m², a clump has to be its volume. The kit's stems stay as the near detail.
  - **Triangles:** LOD0 39,273 + the floor's straws 5,796 + the grass 13,828 = 58,897. LOD1 39,273 + the grass = 53,101. LOD2 1,840, no micro mesh; from 60 m the map's mottle carries the grass. Culled at 400 m.
    - The floor's straws are now in 2 segments, not 4 (483 of them; 11,784 → 5,796 triangles). That leaves the grass its share of the 60 000.
  - **The bends:** `bend_radius_m` (2–6 m) and `leg_jitter_m` (0–1.5 m) are params. The range is read by eye on G 1–12: bends of 25–45° over 1.5–2.5 m of run.
    - `check` keeps the parapets' reach (3.2 m) 0.5 m inside the radius, and the bends inside their legs.
    - `E01_curving`: `bend_radius_m` 3.7, the least `check` allows; `leg_jitter_m` 0.75; `bend_deg` 30 → 35. Bends of 34.9–38.7° on 2.3–2.5 m arcs, the run 50 points, 617 shapes, 97 gaps.
  - **Measured** (linear luminance Y after a 3 px blur; dark is under half the box's mean; boxes on the parapets, clear of the cut and the OSD):

    | | G 3 | G 6 | ours before (task 1.2) | ours now |
    |---|---|---|---|---|
    | field parapet, the shaded side (1.5 m tile) | 15.5 %, cv 0.47 | 13.6 %, cv 0.50 | 2.8 %, cv 0.34 | 10.9 %, cv 0.43 |
    | belt parapet, the sunlit side (1.5 m tile) | 4.6 %, cv 0.28 | 9.7 %, cv 0.35 | 0.1 %, cv 0.23 | 12.6 %, cv 0.40 |
    | the near parapet (10 m tile, sunlit) | — | — | 0.7 %, cv 0.25 | 9.2 %, cv 0.36 |

  - **Matches G** (local `reference/_compare/E01.png`: G 1–12, G 3, 6, 9, the task 1.5 day and overcast 1.5 m and 10 m tiles, task 1.2's, the turntable and the overlay):
    - The lips are broken every 0.1–0.3 m by grass hanging over them on both sides (G 3, 6, 9). The 10 m tile's far lip reads ragged.
    - At 10 m the parapets read as clumps with dark crevices, no longer an even sand-like mat (G 3–6). The dark share and spread are near G's.
    - The bends are sharper and shorter (G 4–6).
  - **Still does not match G:**
    - **The cover.** G's grass covers the parapets whole: a lodged, streaky mat 0.1–0.4 m deep. Ours shows the cover map between faceted hummocks; at 1.5 m they read as grassy molehills, not fibrous tussocks. S04's honest stems run about 8,000 triangles a m², which no 60 000 budget holds; that density is the micro-detail generator's (AST-10, phase 3).
    - **The shaded side** is 3–5 points under G's. The sheet's sun stands at 45° against G's low sun, which casts longer shadows: for the look setup's check (G1).
    - **The lip line** under the tongues is still the 1–10 cm crumble walk. G's lip also shows dark notches cut into the soil.
    - **The bends.** G's arcs look shorter still (1.5–2 m). The parapets' 3.2 m reach holds the radius at 3.7 m.
    - **The colour** (G's golden straw and warm wall under the low sun) is G1's and phase 4's, as the task says.
  - **Build:** two runs byte-identical (FBX, metadata, prefab), `git status` clean after the second.
    - F-1: 41,813 rays, 0 miss. F-2: the least 0.6500 m. F-3: 0.0 mm. F-4: 0.0008 m.
    - F-5: 269 fillers at least 0.300 m, 0.200 m at all 48 joints.
    - F-6: worst 38.7 mm (tolerance 50), 16.1 mm in the 2 cm zone (tolerance 20).
    - F-7: 269 `belt_bare`, 324 `belt_straw` landable, 24 `crater_spoil`. F-8: 0 wind volumes.
    - Gaps: 48 cross-sections, `narrowest_m` 0.786–0.842, and 49 slots.
    - Every capture ran on AC on the RTX 4050 (the capture log's GPU line).
  - **Verified:**
    - EditMode 55 of 55. `TrenchPrefabTests` now expects the micro meshes straw on LOD0 and grass on LOD0 and LOD1, and a new test checks the grass draws on LOD0 and LOD1, not on LOD2, with no collider.
    - `python -m pytest tools -q`: 823 passed. The new trench tests cover the grass (visual only, clear of the 0.287 m nose-first half-span, the tongues in front of the walls), the budget per LOD and the bend params' refusals.
    - `fetch.py --verify` OK; the hygiene scan 0 findings; `openspec validate p1-e01-trench --strict` valid.

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios: the five E01 scenarios and the two deltas, the audit lines against the hole contract (F-1…F-8), the gap list against the sheet's width table, the comparison sheet against G and B read by eye (REAL-03), the geometry audit (REAL-05: lips crumbled as geometry, clods as geometry, fillers follow the faces), budgets (AST-08), `fetch.py --verify`, hygiene (D-003 pins, nothing from `reference/`, subject-only commits)
