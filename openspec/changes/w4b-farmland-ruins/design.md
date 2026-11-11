# Design: w4b-farmland-ruins (task 1.1)

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
  `village_ruins.fbx` b87059ac…cb1a (the friend's, not in the manifest: see §10), `House.fbx` 65083981…c279 and
  `Doors.fbx` 5a5d3f6d…f66a (both pinned in `tools/assets/manifest.json` too). **Measured.**

## 2. The ids (for W2's export)

| Asset | Class | Variants (the friend's prefab names) |
|---|---|---|
| `N01` | `ruins` | `Ruin_RoofCollapsed`, `Ruin_Walls`, `Ruin_Walls_mirrored`, `Ruin_Burnt`, `Ruin_Stump` |
| `N02` | `ruins` | `Debris_RoofSheet_A`, `Debris_RoofSheet_B`, `Debris_WallPanel`, `Debris_Window`, `Door01`, `Door02`, `Door03`, `Door04` |

- The metadata lives where the sim's loader looks for it: `unity/Assets/_Project/Art/Models/ruins/<ID>/<variant>.assetmeta.json`
  (a placement row's class/asset/variant). Asset space is the friend's prefab's local frame: a ruin at the model's
  origin, a debris piece recentred as the prefab does.
- **A placement row has a yaw only.** The mirrored house is its own variant, `Ruin_Walls_mirrored`, with the mirror
  baked in: W2 exports that instance under it. The debris' tilt (≤ 6° plus the slope) has no field in a row; with
  yaw only, a 2.3 m door's end sits up to about 0.24 m off (2.3 × sin 6°). **Derived.** That is W2's and W3's call
  (§10).
- `assets-src/vendor.json` lists them (not `catalog.json`, so `build.py` and the Unity importer never try to export
  or import a vendor mesh). The params are `assets-src/ruins/<variant>.json`.

## 3. The method: slabs from the faces (`tools/blender/lib/slabs.py`)

The vendor meshes are open shells: a broken wall is its outer siding and its inner plaster, 0.20 m apart, with no top.
Their collision is built from the faces themselves:

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
5. **Cover.** Families run largest first; an axis family drops the pixels that lie on a box already made (a sill's top
   is the wall's box).
6. **Rectangles.** Each slab region becomes boxes: runs per row, rows merged while each run end stays within the part's
   merge tolerance (a diagonal edge's staircase; an end lies at most that far off the face).
7. **Prune.** A box whose surface lies within `prune.tol_m` of other boxes of its own material is dropped, smallest
   first; a box that covered a dropped one is kept from then on, so a staircase cannot erode step by step. A lining of
   another material is never dropped for its core.

The broken wall's top is the one place the boxes are not the visual: the slab is solid between the two skins where the
visual is open (§8). A frame wall's skins over studs and fill is solid to a drone and a fiber, so the slab is.

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
  per part along the intact house, so `wall_back_window3` is the same opening in every ruin. In a ruin, an opening is
  cut at the lower of its jambs' tops (probed `jamb_px` 3 pixels outside it in the ruin's collision); it is gone when
  less than `gaps.min_m` 0.25 m is left. Each gap is the rectangle at the wall band's middle, normal ±X or ±Z,
  `narrowest_m` its smaller side.
- **Roof and ceiling holes.** What a ruin lost of a skin part (roof, ceiling), per family with its planes taken
  together, when it keeps at least 5 % of it: up to 12 rectangles inscribed in the lost area, largest first, each at
  least 0.25 m either way, at the part's mean offset. A skin lost whole (the roofless ruins) is gone, not holed.
- Counts: Ruin_RoofCollapsed 80 (window panes, 3 doors and the collapse's roof and ceiling holes), Ruin_Burnt 25,
  Ruin_Walls and its mirror 4, Ruin_Stump 1. Debris: none (they lie on the ground).
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
| `prune.tol_m` | 0.02 | published | AST-05's 2 cm at openings and landing surfaces: a pruned sliver stays within it |
| `gaps.band_m` | 0.12 | assumed | the window frames stand ≤ 0.1 m proud (measured) |
| `gaps.jamb_px` | 3 | assumed | 3 cm outside the opening: the frame, not the raster's edge |
| `gaps.min_m`, `holes_max`, `skin_keep_min` | 0.25, 12, 0.05 | assumed | §5 |
| `wind.cell_m` | 0.25 | assumed | half the smallest opening that passes the drone |
| `snag.broken_drop_m` | 0.05 | assumed | AST-05's tolerance |
| `lod.cull_m` | §6 | published / derived | §6 |

## 8. Results

| Asset | Variant | Boxes (materials) | Gaps | Wind | Snag | Size (m) | Tris |
|---|---|---|---|---|---|---|---|
| N01 | Ruin_RoofCollapsed | 7168 (758 asbestos_cement, 880 masonry, 5530 timber) | 80 | 230 | 126 | 14.82 × 7.91 × 11.91 | 6200 |
| N01 | Ruin_Walls | 2523 (462 masonry, 2061 timber) | 4 | 124 | 36 | 13.92 × 3.95 × 11.50 | 3080 |
| N01 | Ruin_Walls_mirrored | 2558 (462 masonry, 2096 timber) | 4 | 124 | 36 | 13.92 × 3.95 × 11.50 | 3080 |
| N01 | Ruin_Burnt | 3405 (610 masonry, 2795 timber) | 25 | 135 | 70 | 13.92 × 3.95 × 11.54 | 3762 |
| N01 | Ruin_Stump | 2013 (415 masonry, 1598 timber) | 1 | 100 | 14 | 13.88 × 3.95 × 11.50 | 1772 |
| N02 | Debris_RoofSheet_A | 164 (asbestos_cement) | 0 | 0 | 1 | 2.97 × 0.83 × 3.39 | 24 |
| N02 | Debris_RoofSheet_B | 104 (asbestos_cement) | 0 | 0 | 1 | 2.23 × 0.43 × 2.55 | 18 |
| N02 | Debris_WallPanel | 3 (timber) | 0 | 0 | 1 | 1.80 × 0.06 × 1.80 | 16 |
| N02 | Debris_Window | 56 (timber) | 0 | 0 | 8 | 1.41 × 0.46 × 1.55 | 114 |
| N02 | Door01–Door04 | 7, 3, 107, 3 (timber) | 0 | 0 | 1 each | 2.32 × 0.09 × 1.30 | 98–154 |

### 8.1 The audit

`lib.audit.run` (AST-05: 50 mm both ways, 20 mm within 0.3 m of a gap or a landable face; the ground below y = 0
covering), `python tools/blender/vendor_build.py <ID> --audit`. It reports; the build does not refuse on it.

| Piece | Time | Mesh → collision, worst | Box → mesh within 50 mm but in a 20 mm zone | Box → mesh over 50 mm (worst) |
|---|---|---|---|---|
| Ruin_RoofCollapsed | 2112 s | 90 mm (1 point) | 1479 | 1385 (155 mm) |
| Ruin_Walls | 638 s | 90 mm (1 point) | 989 | 511 (155 mm) |
| Ruin_Walls_mirrored | 744 s | 90 mm (1 point) | 996 | 553 (155 mm) |
| Ruin_Burnt | 1223 s | 84 mm (1 point) | 1263 | 994 (155 mm) |
| Ruin_Stump | 459 s | 90 mm (1 point) | 1064 | 441 (155 mm) |
| the 8 debris kinds | ≤ 3 s each | within 50 mm | none | none |

Every gap's `narrowest_m` matches its largest inscribed circle within 5 mm (80 + 25 + 4 + 4 + 1 gaps).

What the ruins' findings are (read point by point):
- **Mesh → collision.** One point per ruin, and it is no bare face: the siding runs on behind a window's casing, and
  the casing's box (10 cm proud) buries it 9 cm deep. Every visible face lies on or within 50 mm of a box.
- **Box → mesh: the open shell.** The visual is two skins with nothing between them where the friend's break cut the
  wall, the frames and the stove; the slab is solid there (§3). So a box's face shows where the visual shows a gap:
  a broken wall's top and its steps (up to half the wall, 100 mm), a frame member's side the wall hid before the
  break, the stove's hollow (the 155 mm worst). Those are the over-50 mm findings. The within-50 mm ones are the
  2 cm sheets and linings whose back or side faces fall inside the 20 mm zone of the landable floor (every wall stands
  within 0.3 m of it) or of an opening.
- **The choice.** Solid walls are what a drone and a fiber meet in a frame wall (skins on studs and fill), and what the
  brief asks ("walls with their thickness"). The audit's alternative is two skins and a hollow between them (each skin
  a sheet, nothing paired): it would pass where the visual is open, and leave a 0.16 m slot along every broken top
  that a fiber can fall into. Left to physics and QA (§10).

## 9. The sim test (`unity/Packages/com.fpv450.sim/Tests/RuinContactTests.cs`)

Ruin_RoofCollapsed alone on a flat map at the origin; the repository's drone, seeded as every contact scenario
(`ContactScenarios.Setup`), flown by `SimPilot` in still air:
- **Into its wall:** 8 m/s north at 1.5 m into the back wall's siding at x −3.0 (premise: rays at x −3.45…−2.55 and
  1.2–1.8 m meet `wall_back` at its face). Measured: first touch by `prop_fr` at 8.85 m/s; the crash (acceleration,
  peak 22 202 m/s²) by `motor_fr` names `Ruin_RoofCollapsed`, part `wall_back` (its shape's "part"), material
  `timber`; the contact point lies at the face (z −4.552 against −4.55), nothing reaches past it, no tunnel.
- **Through its door:** from the back-left room at 3 m/s through `partition_door1` (1.30 m wide, the drone 0.574 m)
  along its centre line at 1.6 m: no touch at or before the doorway, no tunnel; the first touch past it is the next
  room's window frame, 4.15 m on.

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
- **Box counts** are high where the faces are many and diagonal (the roof's hips, the floors' sag): 7168 for the
  collapsed-roof ruin, about 18 000 for the village with its debris. The fiber's query cost grows with the shapes near a
  segment (L6, W3).
- **`village_ruins.fbx` is not in the manifest**: it is the friend's file, derived from the pack's geometry, so it is
  local-only; the params pin it. W0's manifest could list it as local-only.
- **The broken tops** are solid where the visual is two open skins (§3, §8.1): the audit's over-50 mm findings. Solid
  or two skins is physics' and QA's call; two skins is a change of the pairing rule, not of the params.
- **Contact events.** After the door pass the drone scrapes the next room's window frame (many small boxes) and the
  step's event buffer overflows (1088 events counted as lost in the test's log): the flight is fine, the log is not
  complete there.
