# Design: w4b-farmland-ruins (tasks 1.1 and 1.2)

The farmland village's ruins and debris get their physics from the vendor meshes, by a generator with per-piece
params. The meshes stay in the local store (D-089); what is pushed is the params, the generator and the metadata.
Every number below carries its label: **measured** (read off the vendor mesh or file), **published** (the friend's
code or the pack), **derived** (computed from the others, the formula given), **assumed** (a choice, the reason given).

## 1. The source

- **The ruins.** The friend's `Farm_land/Models/build_village_ruins.py` cut four ruins from the Country House FREE
  pack's `House.fbx` (every part cut on a 0.45 × 0.45 × 0.3 m grid, faces above a noise height field deleted, the
  glass removed, a burnt copy of each material for the burnt ones) into `village_ruins.fbx`. **Published.**
- **The village** (`FarmLandBuilder.Village.cs`, seed 8120): five houses from four meshes, `Ruin_Walls` twice and
  once mirrored (localScale.x = −1), each sunk 0.08 m; 4–7 debris pieces per house, 28 in all, of eight kinds:
  `Debris_RoofSheet_A`, `Debris_RoofSheet_B`, `Debris_WallPanel`, `Debris_Window` (from `village_ruins.fbx`) and
  the pack's `Door01`–`Door04` (from `Doors.fbx`, laid on their side by a child rotation of (0, 0, 90)), each
  recentred so its origin is the ground under the middle of its bounds, then yawed at random and tilted up to 6°
  plus the terrain's slope. **Published.**
- **The frame.** Each FBX is read as Unity imports it (`lib.fbxread.meshes`): X negated, file units to metres, the
  node's transform dropped, as the friend's prefab takes only the mesh. **Measured** check: the ruins' bounds,
  x −7.41…7.41 and z −5.06…6.85, match the friend's `HouseFootprint` (−7.4…7.4, −5.1…6.9), the porch towards +Z as
  its comment says; the doors' mesh stands upright with its thin side along X, as the friend's comment says.
- **Pinned.** Each params file pins its FBX's SHA-256 and the generator refuses another file:
  `village_ruins.fbx` b87059ac…cb1a, `House.fbx` 65083981…c279 and `Doors.fbx` 5a5d3f6d…f66a, all three pinned in
  `tools/assets/manifest.json` too, local-only (D-089). `village_ruins.fbx` is the friend's file but carries the
  pack's geometry, so its entry carries the pack's licence (`unity-asset-store:country_house_free_village_ruins`,
  task 1.2). **Measured.**

## 2. The ids (for W2's export)

| Asset | Class | Variants (the friend's prefab names) |
|---|---|---|
| `N01` | `ruins` | `Ruin_RoofCollapsed`, `Ruin_Walls`, `Ruin_Walls_mirrored`, `Ruin_Burnt`, `Ruin_Stump` |
| `N02` | `ruins` | `Debris_RoofSheet_A`, `Debris_RoofSheet_B`, `Debris_WallPanel`, `Debris_Window`, `Door01`, `Door02`, `Door03`, `Door04` |

- The metadata lives where the sim's loader looks for it: `unity/Assets/_Project/Art/Models/ruins/<ID>/<variant>.assetmeta.json`
  (a placement row's class/asset/variant). Asset space is the friend's prefab's local frame: a ruin at the model's
  origin, a debris piece recentred as the prefab does.
- **A placement row has a yaw only.** The mirrored house is its own variant, `Ruin_Walls_mirrored`, with the mirror
  baked in. W2's export (`maps/farmland/placements.json`) names that instance `Ruin_Walls` with scale x = −1: **a
  placement with a negative scale uses `Ruin_Walls_mirrored`'s metadata**: whoever reads the row (W3's loader) takes
  `Ruin_Walls` with scale x < 0 as that variant, and never mirrors `Ruin_Walls`' own metadata at run time (its gap
  ids, snag edges and box order were built for the unmirrored mesh). The debris' tilt (≤ 6° plus the slope) has no
  field in a row; with
  yaw only, a 2.3 m door's end sits up to about 0.24 m off (2.3 × sin 6°). **Derived.** That is W2's and W3's call
  (§10).
- `assets-src/vendor.json` lists them (not `catalog.json`, so `build.py` and the Unity importer never try to export
  or import a vendor mesh). The params are `assets-src/ruins/<variant>.json`.

## 3. The method: slabs from the faces (`tools/blender/lib/slabs.py`, `lib/air.py`)

The vendor meshes are open shells: a broken wall is its outer siding and its inner plaster, 0.20 m apart, with no top.
Their collision is built from the faces themselves (each polygon ear-clipped as the audit does, `lib.audit`'s
triangulation: a fan of a notched floor polygon spilt past its notches, where no face is drawn, task 1.2):

1. **Families.** Faces are grouped by normal up to sign: within `axis_angle_deg` of X, Y or Z, or no deeper than
   `axis_dev_m` along that axis (a lap board's bevel), a face joins that axis; any other face joins the nearest family
   within `angle_tol_deg`, whose normal is its members' area-weighted mean. A ruin takes its intact house's families,
   so both raster alike.
2. **Planes.** Within a family, faces whose offset (normal · centroid) lies within `plane_tol_m` share a plane, front
   faces (normal along +n) and back faces (along −n) apart.
3. **Raster.** Each plane is rastered at `raster_m` on a grid aligned to multiples of it, conservatively (a pixel is the
   face's when the face overlaps it: a spike's tip narrower than a pixel is not lost).
4. **Pairing.** Outward normals put solid between a back face and the next front face along +n. Per pixel, each back
   plane pairs with the nearest front plane within `max_thickness_m`; a pixel left alone is a **sheet**, a slab of its
   part's `sheet_m` behind the face. A slab between two materials is **layered**: the lining part takes its `layer_m`
   from its face (the plaster on a log wall), the other part the rest.
4a. **Air (task 1.2, REAL-05).** A pair is solid only where the space between its faces is a closed cavity of its
   own. `lib.air` voxelises the mesh at `air_cell_m` (every voxel a face touches is wall, conservatively, so faces
   meeting at an edge leave no crack; the ground below y = 0 is wall), floods the air from the grid's sides, and labels
   the air the flood does not reach as closed voids, each with its box. Per pixel, the pair's middle decides: in open
   air (a broken wall's siding and plaster, the outside reaching between them over the break), or in a closed void
   reaching more than a cell past either face (a hollow its faces close all round, the stove's body), or below the
   ground (which covers it), the pair is **two sheets**, each of its part's `sheet_m` behind its face, **open between
   them**; in a closed cavity between the two faces, or in a wall voxel (a space too thin for air), it stays a slab.
   A pair narrower than its two sheets plus a cell stays a slab (a door leaf's open end leaves no space the air
   resolves). The wall's *shape* (the bands the gaps, broken tops and wind volumes read, §5, §6) is still the pair:
   a wall band spans both skins.
5. **Cover.** Families run largest first; an axis family drops the pixels that lie on a box already made (a sill's top
   is the wall's box).
6. **Rectangles.** Each slab region becomes boxes: runs per row, rows merged while each run end stays within the part's
   merge tolerance (a diagonal edge's staircase; an end lies at most that far off the face); by rows and by columns,
   whichever needs fewer (task 1.2: a siding run whose one end steps along a diagonal was a stack of 2 cm strips the
   wall's full width by rows, one rectangle and a narrow staircase by columns).
7. **Prune.** A box whose surface lies within `prune.tol_m` of other boxes of its own material is dropped, smallest
   first; a box that covered a dropped one is kept from then on, so a staircase cannot erode step by step. A lining of
   another material is never dropped for its core. Since task 1.2 a box is dropped only when, besides, the line along
   its slab's normal through each point of its footprint meets another kept box of its material within `prune.tol_m`
   past its faces: the 1.1 rule dropped every other strip of such a stack, leaving 1–3 cm slits through the siding,
   which a solid wall hid behind them and a 2 cm sheet does not (the sim test's rays passed through one).

1.1 filled the space between a broken wall's two skins (§8.1's 1.1 findings over 50 mm); since 1.2 the boxes are the
two skins where the mesh shows them with nothing between. A frame wall's studs and fill are not in the mesh, so a
drone or a fiber that drops into the open top of a broken wall falls between its skins, as the visual shows.

## 4. Parts and materials

Rules, first match wins, on the face's slot (a burnt copy is its slot), its normal class (up, down, vertical, sloped)
and its centroid's box, in the source mesh's frame. The ruins' rules are one list, in all five params files; the
debris' one rule each. **Measured**: every box below was read off `House.fbx`'s parts (heating_stove, the chimney
above 3.95 m, Window, windowsCeiling, door_frames, the wall faces).

| Part | Physics | Faces | Notes |
|---|---|---|---|
| `stove` | masonry | any in x −4.54…−1.99, y 0.3…3.95, z −0.84…0.32 | the pack's plastered stove; edge radius 3 mm |
| `chimney` | masonry | vertical or up, x −4.6…−3.7, y > 3.95, z −0.45…0.5 | |
| `window_frame` | timber | WindowsMat01; Wood02 in the back window's and the porch glazing's boxes | |
| `door_frame` | timber | Wood02 in door_frames' box above 0.4 m | |
| `floor` | timber | up-facing below 0.6 m | **landable**; sheet 0.02 m |
| `ceiling` | masonry | IndoorWall01, down-facing at 3.8…4.0 m | plaster on boards; sheet 0.02 m |
| `roof` | asbestos_cement | IndoorWall01, sloped, above 3.5 m | sheet 0.02 m; merge 0.03 m (hips) |
| `roof_frame` | timber | anything else above 3.95 m | fascia, soffit, rafters; merge 0.03 m |
| `wall_stove_end`, `wall_far_end`, `wall_back`, `wall_front`, `porch_wall`, `partition` | masonry (IndoorWall01, a 0.02 m lining layer) and timber (the rest) | vertical, in each wall's band | the outer walls by band, the porch's beyond z 3.45, the rest partitions |
| `trim` | timber | the rest | sills, steps, skirting |
| debris `roof_sheet` | asbestos_cement | all | sheet 0.01 m; edge radius 3 mm |
| debris `wall_panel`, `window_frame`, `door_leaf` | timber | all | sheet 0.02 m |

- **The roof is asbestos-cement slate, not sheet metal.** The roof's texture region (`OldHouseMapIndoorWall01`) is
  grey corrugated slate in overlapping courses with moss: the village's шифер. **Measured** (looked at). The physics
  table's `asbestos_cement` row is that ("slate sheets and fragments"). The brief's "sheet metal": no metal is drawn on
  these houses.
- **Burnt wood is `timber`.** The physics table (`assets-src/physics_materials.json`, physics-owned) has no charred
  row; a new row changes the table's hash, which every flight log header carries and every replay checks. The burnt
  ruins' parts carry `timber` and their visual slot names the burn (`…_Burnt`, the friend's darkened copies).
  **Assumed**, as an open question for physics (§10).
- **Plaster is `masonry`** (its rationale: "brick, block, render"). Lining 0.02 m: lime plaster on lath. **Assumed.**
- Edge radii (the snag records' arris): timber 2 mm (H01's), plaster and slate 3 mm (half the 5.8 mm sheet).
  **Assumed.**

## 5. Gaps

- **Wall openings.** The intact house (`House.fbx`, the glass left out) is decomposed with the same rules; in each wall
  band (a wall part's slab with its layers, ± `band_m` 0.12 m for a frame standing proud) every clear region the house
  encloses on all sides is an opening, as its largest inscribed rectangle: the window panes between sashes, the doors
  (an opening whose bottom is within 0.10 m of a landable top: the threshold board stands proud). Openings are numbered
  per part along the intact house in the source mesh's frame, so `wall_back_window3` is the same opening in every
  ruin, the mirrored one too (task 1.2: the mirrored house numbered them along its own +x, backwards). In a ruin, an
  opening is cut at the lower of its jambs' tops (probed `jamb_px` 3 pixels outside it in the ruin's collision, the
  jamb's solid starting at most `jamb_px` rows above the opening's bottom: task 1.2, a two-skin wall's jamb sheet may
  start a centimetre above the threshold's box, a seam the solid wall filled); it is gone when less than `gaps.min_m`
  0.25 m is left. Each gap is the rectangle at the wall band's middle, normal ±X or ±Z, `narrowest_m` its smaller
  side.
- **Roof and ceiling holes.** What a ruin lost of a skin part (roof, ceiling), per family with its planes taken
  together, when it keeps at least 5 % of it: up to 12 rectangles inscribed in the lost area, largest first, each at
  least 0.25 m either way, at the part's mean offset. A skin lost whole (the roofless ruins) is gone, not holed.
- Counts (task 1.2): Ruin_RoofCollapsed 82 (window panes, 3 doors and the collapse's roof and ceiling holes),
  Ruin_Burnt 26, Ruin_Walls 21 and its mirror 20, Ruin_Stump 4. Debris: none (they lie on the ground). 1.1 found 80,
  25, 4, 4 and 1: its probe dropped an opening whose jamb column was clear in the opening's bottom row, and a sill's
  or a threshold's box often stops a centimetre or two below it (measured on `Ruin_Walls`: without the slack, 6 gaps;
  the two more than 1.1 are the front doors its prune's slits had cut). So the broken walls' surviving windows and
  the roofless ruins' front doors are gaps now. Every 1.1 gap is kept (two narrowest values moved by 1 cm). The
  mirror lacks two 0.28 m porch panes of `Ruin_Walls` and has one it lacks: each is cut within 4 cm of `gaps.min_m`,
  and the two meshes' raster ties fall either side of it.
- `gaps.min_m` 0.25 m: below it no drone part passes (the drone spans 0.574 m; a pane of 0.3 m still passes a fiber
  and the wind). **Assumed.**

## 6. Wind, snag, LOD

- **Wind volumes.** Each band of the walls, partitions, roof and chimney on a 0.25 m grid (a cell solid when half its
  pixels are), merged into boxes of porosity 0. Ruin_RoofCollapsed keeps its roof, so its interior below the ceiling
  (x ±6.72, y 0.37…3.90, z −4.35…3.08, **measured**: the inner faces) is a **cavity**, porosity 1, open through its
  outer walls' openings and its ceiling and roof holes (H01's convention). The roofless ruins have none. Debris: none
  (K01–K07's convention).
- **Snag.** Each opening's outline at both faces of its wall (the arris the fiber bends over), each skin hole's
  outline, and each wall's broken top (where it stands more than `snag.broken_drop_m` 0.05 m below the intact wall) as
  a stepped polyline along both faces, with the material of the layer at that face. Debris: every open edge of the mesh
  (a sheet's broken rim), chained.
- **LOD.** The vendor mesh is drawn as the friend's prefab draws it; the metadata names it as LOD0 (`lods[0].mesh` =
  the vendor object). Cull: ruins 2340 m (H01's, **published** in its params; the friend's ruins carry no LOD group);
  debris the friend's LOD cull at 0.4 % of the screen height under Unity's default 60° vertical field of view:
  d = size / (0.004 × 2 tan 30°) = 216.5 × size, 337–734 m. **Derived.**

## 7. Params (`assets-src/ruins/<variant>.json`; `generators/vendor_ruin.py` PARAMS)

| Key | Value | Label | Why |
|---|---|---|---|
| `slabs.raster_m` | 0.01 | assumed | a fifth of the 5 cm tolerance |
| `slabs.angle_tol_deg` | 6 | assumed | a roof's facets differ by ≥ 9° (porch and main pitch: 46° and 36°, measured), its sub-planes by ≤ 2° |
| `slabs.axis_angle_deg` | 6 (ruins), 3 (debris) | assumed | a sagging floor (1.3°, derived: 0.37 to 0.46 m over about 4 m, measured) and lap boards (≤ 5°) stay on their axis; a debris sheet lying 5° askew keeps its plane |
| `slabs.axis_dev_m` | 0.015 | assumed | a bevel no deeper than this is its wall's face |
| `slabs.plane_tol_m` | 0.03 | assumed | the siding's boards lie within 3 cm (measured 6.88–6.93 m) |
| `slabs.max_thickness_m` | 0.35 (0.12 for the loose window) | assumed | the walls are 0.14–0.24 m (measured); the window's members ≤ 0.1 m, its open sides would pair across air |
| `slabs.merge_tol_m` | 0.015 (0.01 debris; 0.03 roof parts) | assumed | an end at most this off a diagonal edge |
| `slabs.min_box_m` | 0.02 | assumed | under 2 pixels both ways is raster noise |
| `slabs.cover_tol_m` | 0.015 | assumed | a face this close to a box is its surface |
| `slabs.air_cell_m` | 0.02 | assumed | under a third of the narrowest broken wall's space between its skins (0.10 m, measured: 0.14 m wall less its 2 cm sheets), so its middle keeps free voxels; a crack under 2 cm counts as closed |
| `prune.tol_m` | 0.02 | published | AST-05's 2 cm at openings and landing surfaces: a pruned sliver stays within it |
| `gaps.band_m` | 0.12 | assumed | the window frames stand ≤ 0.1 m proud (measured) |
| `gaps.jamb_px` | 3 | assumed | 3 cm outside the opening: the frame, not the raster's edge; and a jamb may start 3 cm above the opening's bottom (a seam between a threshold's box and a skin's) |
| `gaps.min_m`, `holes_max`, `skin_keep_min` | 0.25, 12, 0.05 | assumed | §5 |
| `wind.cell_m` | 0.25 | assumed | half the smallest opening that passes the drone |
| `snag.broken_drop_m` | 0.05 | assumed | AST-05's tolerance |
| `lod.cull_m` | §6 | published / derived | §6 |

## 8. Results

Task 1.2's metadata (1.1's in brackets where it changed):

| Asset | Variant | Boxes (materials) | Gaps | Wind | Snag | Size (m) | Tris |
|---|---|---|---|---|---|---|---|
| N01 | Ruin_RoofCollapsed | 7016 [7168] (720 asbestos_cement, 797 masonry, 5499 timber) | 82 [80] | 230 | 130 [126] | 14.82 × 7.91 × 11.91 | 6200 |
| N01 | Ruin_Walls | 2690 [2523] (470 masonry, 2220 timber) | 21 [4] | 124 | 70 [36] | 13.92 × 3.95 × 11.50 | 3080 |
| N01 | Ruin_Walls_mirrored | 2710 [2558] (467 masonry, 2243 timber) | 20 [4] | 124 | 68 [36] | 13.92 × 3.95 × 11.50 | 3080 |
| N01 | Ruin_Burnt | 3665 [3405] (618 masonry, 3047 timber) | 26 [25] | 136 [135] | 72 [70] | 13.92 × 3.95 × 11.54 | 3762 |
| N01 | Ruin_Stump | 2033 [2013] (406 masonry, 1627 timber) | 4 [1] | 100 | 20 [14] | 13.88 × 3.95 × 11.50 | 1772 |
| N02 | Debris_RoofSheet_A | 194 [164] (asbestos_cement) | 0 | 0 | 1 | 2.97 × 0.83 × 3.39 | 24 |
| N02 | Debris_RoofSheet_B | 140 [104] (asbestos_cement) | 0 | 0 | 1 | 2.23 × 0.43 × 2.55 | 18 |
| N02 | Debris_WallPanel | 5 [3] (timber) | 0 | 0 | 1 | 1.80 × 0.06 × 1.80 | 16 |
| N02 | Debris_Window | 66 [56] (timber) | 0 | 0 | 8 | 1.41 × 0.46 × 1.55 | 114 |
| N02 | Door01–Door04 | 13, 13, 104, 14 [7, 3, 107, 3] (timber) | 0 | 0 | 1 each | 2.32 × 0.09 × 1.30 | 98–154 |

- **Shapes in the village** (W2's placements: the five houses, `Ruin_Walls` twice, once with scale x = −1, and 28
  debris pieces: roof sheet A ×5, B ×2, wall panel ×1, window ×5, doors 01–04 ×4, 4, 3, 4): **20 171**, of which the
  houses 18 114 and the debris 2 057; 1.1: 19 351 (17 667 and 1 684). Per house: 7016, 2690, 2710, 3665, 2033.
- **Where the change comes from.** The walls' two skins replace one slab with two sheets where 1.1 had a slab and a
  lining (about the same count). Rectangles by columns (§3, step 6) remove the stacks of strips. The prune now keeps a
  strip whose neighbours only touch it (§3, step 7): that is most of the rise, and in the debris all of it (1.1's
  roof sheets and doors had 1–3 cm slits between their strips). Door02 and Door04 are two sheets each: the pack's
  leaf has no face at one end, so 2 cm of air reaches between its faces.

### 8.1 The audit

`lib.audit.run` (AST-05: 50 mm both ways, 20 mm within 0.3 m of a gap or a landable face; the ground below y = 0
covering), `python tools/blender/vendor_build.py <ID> --check --audit`. It reports; the build does not refuse on it.
Counted per box from the audit's lines (a box over 50 mm counts there, not again in the zone; 1.1's §8.1 counted the
problem lines, so its figures read higher), before (1.1's committed metadata, re-audited) and after (task 1.2):

| Piece | Box → mesh over 50 mm (worst) | Box → mesh within 50 mm, over 20 mm in a zone | Mesh → collision, worst | Audit's problems | Time |
|---|---|---|---|---|---|
| Ruin_RoofCollapsed | 1154 (155 mm) → **109** (97 mm) | 1450 → 1744 | 90 → 43 mm | 2866 → 1949 | 2146 → 1049 s |
| Ruin_Walls | 361 (155 mm) → **26** (65 mm) | 984 → 1233 | 90 → 42 mm | 1502 → 1303 | 719 → 386 s |
| Ruin_Walls_mirrored | 404 (155 mm) → **23** (65 mm) | 991 → 1268 | 90 → 42 mm | 1551 → 1336 | 749 → 390 s |
| Ruin_Burnt | 758 (155 mm) → **68** (82 mm) | 1244 → 1464 | 84 → 42 mm | 2259 → 1619 | 1267 → 611 s |
| Ruin_Stump | 276 (155 mm) → **15** (63 mm) | 1062 → 1222 | 90 → 34 mm | 1507 → 1263 | 516 → 233 s |
| the 8 debris kinds | none → none | none → none | within 50 mm | 0 → 0 | ≤ 6 s each |
| **all** | **2953 → 241** | 5731 → 6931 | | 9685 → 7470 | |

Every gap's `narrowest_m` matches its largest inscribed circle within 5 mm (82 + 21 + 20 + 26 + 4 gaps).

What is left (read box by box):
- **Over 50 mm: 241 boxes, worst 97 mm.** 233 of them are faces approximated by a
  family's plane: a trim, roof, roof-frame, floor or stove face up to 6° off its family's normal (`angle_tol_deg`,
  `axis_angle_deg`) lies up to 4–10 cm off the plane over its width (§10), so its sheet's far corner does: the plinth's
  sloped cap and the sills (trim, 52 in the collapsed-roof ruin, 43 in the burnt one), the hips' sheets (roof 18,
  roof frame 9), the floor's underside by the stove and the thresholds (11–14 per ruin), the stove's stepped top (2–13).
  1.1 had them too, partly hidden inside the walls' slabs, which the audit counts as covering. The other 8 are 2 cm
  tall slabs across a wall's two skins (`wall_back`, `wall_front`, `wall_stove_end`; up to 82 mm): the 2 cm voxels
  seal a small pocket of the cavity (one read: under a sill) whose neighbouring rows are open, so the slab's face
  shows in the open cavity.
- **Mesh → collision**: every visible face lies within 43 mm of a box (1.1: 90 mm, the siding buried in a casing's
  box, gone with the casing's fill). Within a zone up to 43 mm: the floor's tilted faces above their axis sheet.
- **The 20 mm zone: 6931 boxes, 20–50 mm.** A 2 cm sheet behind a one-sided face has its back 20 mm behind it plus its
  plane's spread; within 0.3 m of the floor (every wall's foot) or of an opening the audit holds that back to 20 mm.
  Two skins put more sheet backs there than one slab did, and the found openings (21 gaps where 1.1 had 4) widen the
  zone. On `Ruin_Stump`, 73 % of them lie 20–30 mm off the visual, half are tilted families' sheets, and all but two
  are boxes 21 mm thin or less. Physics' and QA's call (§10).
- **What 1.1's findings were** (for the record): the slab between a broken wall's two skins, its top up to half the
  wall (100 mm) off the visual, a frame member's side, and the stove's closed body partly filled (the 155 mm worst):
  those are gone. A closed hollow the faces close all round (the stove's body) is now its faces' sheets.

## 9. The sim test (`unity/Packages/com.fpv450.sim/Tests/RuinContactTests.cs`)

Ruin_RoofCollapsed alone on a flat map at the origin; the repository's drone, seeded as every contact scenario
(`ContactScenarios.Setup`), flown by `SimPilot` in still air:
- **Into its wall:** 8 m/s north at 1.5 m into the back wall's siding at x −3.0 (premise: rays at x −3.45…−2.55 and
  1.2–1.8 m meet `wall_back` at its face). Measured (task 1.2, the wall two 2 cm skins): first touch by `prop_fr` at
  8.85 m/s; the crash (acceleration, peak 2 705 m/s², 1.1's solid slab 22 202) by `motor_fr` at 8.69 m/s names
  `Ruin_RoofCollapsed`, part `wall_back` (its shape's "part"), material `timber`; the contact point lies at the face
  (z −4.5520 against −4.55), nothing reaches past it, no tunnel, no event lost. Before task 1.2's prune rule the
  premise failed: the ray at 1.5 m passed a 2 cm slit in the siding's sheet and met the plaster at z −4.37.
- **Through its door:** from the back-left room at 3 m/s through `partition_door1` (1.30 m wide, the drone 0.574 m)
  along its centre line at 1.6 m: no touch at or before the doorway, no tunnel; the first touch past it is the next
  room's window frame, 4.15 m on (302 touches of the ruin past the doorway).
- The tests also check (`tools/blender/tests/test_vendor_ruin.py`) that the committed `Ruin_RoofCollapsed` is two
  skins at the crash point: the siding's sheet at z −4.552…−4.532, the plaster's at −4.372…−4.352, nothing between.

## 9.1 Review sheets

`pwsh tools/vendor_asset.ps1 <ID|variant|all>` writes `review-sheets/<ID>/<variant>.png` (git-ignored: the pack's
textures are in it) and its tiles: 1.5 / 10 / 50 / 200 m from the south-west at the pilot's height (50 and 200 m
looking down 20°), an 8-view turntable, the collision overlay from the south-west, the north-east and above, and for
a piece with openings the four sides and the top from 10 m and every gap from 1.5 m on both sides, with the overlay;
a 1.8 m bar of 0.1 m bands stands beside each piece. Rendered in Blender (EEVEE textured, Workbench X-ray for the
overlays), since the vendor meshes are not in our Unity project.

## 10. Limits and open questions

- **Burnt wood** has no physics row; `timber` stands in (§4). Physics may add a charred row (its friction, crush and
  edge radius), a table change every log carries.
- **Placement rows carry a yaw only**: the mirrored house is a variant; the debris' tilt is lost unless the row
  format gains a rotation (W2, W3).
- **Box counts** are high where the faces are many and diagonal (the roof's hips, the floors' sag): 7016 for the
  collapsed-roof ruin, 20 171 for the village with its debris (W2's placements: the five houses and 28 debris
  pieces; 19 351 at 1.1, §8). The fiber's query cost grows with the shapes near a segment (L6, W3).
- **The 20 mm zone** (§8.1): a 2 cm sheet behind a one-sided face, within 0.3 m of the floor or an opening, has its
  back 20 mm behind the face plus the plane's spread, and the zone holds every surface there to 20 mm. That is the
  bulk of what the audit still reports. Physics' call: keep 2 cm skins (a siding board, a floor board, lath and
  plaster are 2 cm or more) and read the zone as the landing faces' and the openings' clear side, or thin the sheets
  (12 mm sheets left about a third of them in a trial on `Ruin_Stump`, 426 against 1223 with the same families; a
  crash's depth past a sheet's middle is a Tunnel event, sooner on a thinner sheet).
- **Tilted faces snapped to an axis.** A floor or threshold face up to 6° off its axis (`axis_angle_deg`) joins the
  axis family at its plane's mean offset: over a 0.45 m face that is up to 4 cm, the over-50 mm findings that remain
  (the floor's underside by the stove and the thresholds, the stove's stepped top) and a floor top up to 34 mm under
  the visual floor there. A depth bound on the snap (a face joins only when it lies within 2 cm of the plane) was
  tried on `Ruin_Stump`: more families and more findings (25 over 50 mm, worst 106 mm), so not taken.
- **Contact events.** After the door pass the drone scrapes the next room's window frame (many small boxes) and the
  step's event buffer overflows (1088 events counted as lost in the test's log at 1.1, 4205 at 1.2 with the skins'
  sheets and the kept strips): the flight is fine, the log is not complete there.
