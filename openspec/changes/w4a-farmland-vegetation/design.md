# Design: w4a-farmland-vegetation

## Context
The farmland (D-088) places 294 trees and 1,352 bushes as terrain instances of 22 prefabs from the local store (D-089): the friend's reduced Poly Haven island tree, Book of the Dead's pines and its Megascans/Quixel bushes, and two Unity Terrain Sample bushes. They draw only: their colliders were removed, and nothing carries physics data. Our own plants (V01, V13) carry bark capsules, a compliant `twig` shell, wind volumes and snag records, generated with the mesh. These vendor plants get the same physics, generated from their meshes, without their geometry leaving the store.

Numbers are labelled **pilot / measured / published / derived / assumed**.

## Goals / Non-Goals
**Goals:** every tree and bush variant the farmland's terrain places gets physics metadata in schema 1 (lib.meta), found by the sim's MapWorld at `unity/Assets/_Project/Art/Models/vegetation/<asset>/<variant>.assetmeta.json`; one command rebuilds it from params (`python tools/blender/vendor_plants.py <ID|variant|all> [--sheets]`, plain Python, the review sheets in the pinned Blender; a full run takes about 50 minutes, the island trees' audits most of it); a review sheet per variant; flightbench crown rows on the farmland's trees.
**Non-Goals:** the look, the placement, new models (proposal); grass (W2 exports its density per cell, AST-10); gap polygons in the crowns (risk 3); the instance scale (risk 1, W2/W3's).

## Decisions

1. **The variants and their ids.** The terrain's tree prototypes (measured: the externals and tree instances of the store's `FarmLand_TerrainData.asset`, read in its binary serialisation) are 22 prefabs, and every variant id is its prefab's name, the name the map export reads off the prototype. Asset ids are per source family, class `vegetation`, in `assets-src/vendor_plants.json` (not `catalog.json`: a catalog entry is built into an FBX and a prefab by build.py and the importer, which would copy the vendor mesh into the pushed tree):

   | Asset | Family | Variants (instances on the map, measured) |
   |---|---|---|
   | VF01 | Poly Haven island_tree_01 (CC0), the friend's card reduction | IslandTree (154), IslandTree_Dry (48) |
   | VF02 | Book of the Dead Pine_002 | Pine002_S2 (6), Pine002_M (8), Pine002_M_Dry (1), Pine002_L (5) |
   | VF03 | Book of the Dead pine_004 | Pine004_1 (1), Pine004_1_Dry (8), Pine004_3 (2) |
   | VF04 | Book of the Dead pine_006 | Pine006_01 (23), Pine006_01_Dry (12), Pine006_02 (21) |
   | VF05 | Book of the Dead PineDead_02 | PineDead_02 (5) |
   | VF06 | Megascans Bush_b, the dead bush | DeadBush_4 (59), DeadBush_6 (9) |
   | VF07 | Quixel GreenBush | GreenBush (579), GreenBush_Dry (190) |
   | VF08 | Megascans Bush_qilgP2, the leafy bush | LeafyBush_P2 (300), LeafyBush_P2_Dry (73) |
   | VF09 | Quixel RedBush | RedBush_Dry (40) |
   | VF10 | Unity Terrain Sample BushDry | SmallBushDry_A (61), SmallBushDry_B (41) |

   294 trees and 1,352 bushes (measured), as the plan counts them. The surround tiles place IslandTree and IslandTree_Dry too; D-091 keeps objects off them. The `_Dry` copies share their green prefab's mesh (the island tree's dry copy has its own) and get their own metadata, since the export names them.

2. **The mesh as Unity draws it** (`lib/vendor.py`). The prefab's LOD group names each LOD's renderer; its MeshFilter names a mesh by guid and fileID, its transforms up to (not including) the root place it (a terrain tree takes its instance's position, yaw and scale, never its root's). FBX meshes are read with lib.fbxread's node tree: the .meta's name table maps a fileID to a mesh (the island tree's hash-named FBX takes the mesh name from the params); Unity's import is reproduced: geometric transform baked, units by UnitScaleFactor/100 × globalScale, X negated (lib.frame); every file is Y-up. Unity YAML meshes (the Terrain Sample's) are read from their vertex and index buffers. **Check (measured):** the box round every renderer of every LOD matches the prefab's LOD group's reference point and size within 2 mm (assumed tolerance: float rounding) for all 22 prefabs; a wrong frame, unit or transform fails here.

3. **Params** (`assets-src/vegetation/<variant>.json`, `tools/blender/vendor_plants.py` PARAMS): the prefab, the mesh file and every foliage material and base map each pinned by SHA-256 (a changed store file is refused; the metadata's `params_sha256` covers the pins), the mesh name for a hash-named FBX, and per LOD0 material its role:
   - **wood** with its physics (`bark` live, `deadwood` for PineDead_02 and the dead bush: the deadwood row snaps at its break stress);
   - **foliage** with its sprays: `none` (a broadleaf's leaf clusters), `hooks` (a spray gets a snag hook) or `branches` (a spray long enough gets a branch, decision 5).

   Shared values, the same in every file:

   | Key | Value | Label |
   |---|---|---|
   | `wood.step_m` | 0.1 | assumed: a ring per 10 cm follows a 1 cm bend |
   | `wood.section_tol_m` | 0.03 | the spec's 3 cm (a round tube's capsule, both ways; half the radius on a member under 6 cm) |
   | `wood.max_len_m` | 2.0 | published: V01's trunk capsules per 1–2 m |
   | `wood.min_radius_m` | 0.005 | published: V01's every member of 10 mm or more |
   | `foliage.spacing_m` | 0.04 | assumed: under the 5 cm porosity raster |
   | `foliage.reach_m` | 0.1 | derived: lib.audit's envelope tolerance, 3 N at the twig row's 30 N/m |
   | `foliage.depths_m` | 0.1 … 0.7 | assumed ladder (decision 6): up to half a 1.4 m leaf-cluster card, the island tree's |
   | `foliage.max_radius_m` | 1.5 | assumed |
   | `sprays.min_length_m` | 1.0 | assumed |
   | `sprays.slenderness` | 70 | assumed: conifer branches 60–80 long per unit of base diameter |
   | `sprays.max_radius_m` | 0.03 | assumed |
   | `sprays.capsule_m` | 2.0 | assumed: as `wood.max_len_m` |
   | `wind.column` | true for trees, false for bushes | V01's convention |
   | `lod.fov_deg` | 60 | assumed: the review camera's |

4. **Wood: capsules that follow the tubes** (`lib/plant.py` skeleton, members, fit_capsules, section). The wood triangles, welded at 1 mm, fall into tube systems (connected sets). A system reaching within 0.3 m of the ground (assumed) is measured from its base band (its vertices within 5 cm of its lowest, assumed); any other (a branch tube, a stem) from its vertex nearest a ground system. Geodesic distance along the edges gives level sets every `step_m`; each cuts the triangles in rings, a ring's centre the length-weighted mean of its crossing segments, its radius their mean distance from it, its points and segments kept. A ring's parent is the ring one level down that the band between them joins. Forks split the node tree into members, and a member is cut into segments from ring centre to ring centre:
   - **a round tube** (every ring point within `section_tol_m` of one capsule round the segment at the points' mean distance, and within half the radius on a thin member, so a bent 2 cm stem is cut where it bends): the longest such segment, at most `max_len_m`, so the capsule follows the tube within 3 cm both ways;
   - **a lumpy section** (not round within that even over one step: the island tree's trunk and thick limbs are scanned wood, lobed by 5–30 cm, measured as the ring points' depth inside their convex hull, median 2 cm, 90th percentile 19 cm, up to 42 cm at the main fork; root flares and stumps spread at the ground): one capsule at the section's mean distance per 3 steps (0.3 m, assumed), so it stands for the lobes by their mean, half of them outside it and half inside (risk 6).
   A member leaving a system's root starts at its first ring (the root band's mean, between spread roots or at an attachment vertex, is no axis). A member with a free tip runs on to its farthest vertex. Members under `min_radius_m` get no capsule: they are twigs, their vertices join the shell's mass and they get snag records (decision 7). Wood wholly more than 0.3 m under the origin is buried on any slope the map has (assumed: 30 % across a 1 m trunk; pine_004 sinks 1.1 m) and left out.
   **Audit, reported and not refused:** lib.audit.run (AST-05: 5 cm both ways, unsigned, planted) holds the capsules to the faces of the members they follow, and the spec's 3 cm is read off it for the capsules of members 5 cm or more across. Round tubes meet both; the scanned lobes and root flares do not, by the amounts in the table below (risk 6). The build refuses a variant on its params, its pins, the frame check, the schema, the shell's coverage or the 64-piece rule, never on these.

5. **Foliage: what the cards draw** (`opaque_samples`, `lib.vendor.Alpha`). Every card triangle is sampled on a barycentric lattice at most `spacing_m` apart, a sample kept where its texel's alpha passes the material's `_Cutoff` when its shader clips (the friend's leaf shader always does, whatever `_AlphaClip` says; the bark shader, which draws the island tree's twig ribbons, never does; read from the shader's source) (its base map at the material's tiling: PNG and TIFF read top row first, Unity's DXT Texture2D assets decoded bottom row first). The shell, the porosity and the sprays work on these samples, not on the quads, so a card's transparent corners stand for nothing. A card component (welded) is a spray; its centre line runs from its sample nearest the trunk's axis (the plant's thickest capsule member) out through the means of its samples binned by distance. A spray within 0.25 m at both ends of a longer one's line (a crossed card of the same branch, assumed) adds nothing.
   **Branches under the sprays** (Book of the Dead's pine_004 and pine_006 draw their branches only as cards): a spray of a `branches` slot at least `sprays.min_length_m` long becomes a `bark` branch along its line from its own base by the trunk (not the trunk's axis, where a whorl's branches would all meet, decision 6), capsules of `sprays.capsule_m` tapering from length / slenderness across (at most `sprays.max_radius_m` in radius) to the twig edge at its tip. Pine_002 and PineDead_02 draw their limbs as tubes, which decision 4 follows instead.

6. **The shell** (`shell`, `standoff`, `most_in_cube`). Material `twig` (30 N/m, compliant, as V01's and V13's). Cells of 0.1 m; the mass is every cell within the foliage half depth of a sample cell (a card stands for a spray of real thickness). A candidate is the largest ball round a cell centre inside the mass, at most `max_radius_m`; sample cells are taken roomiest first, and one no piece yet holds within `reach_m` takes the roomiest candidate that holds it; pieces whose cells all lie in others are dropped, smallest first. The half depth is the first of the ladder whose pieces keep two rules: lib.audit's rule (d), at most 64 pieces meeting any 2 m cube (derived in p1-v01-locust review-physics §2.4: the narrow phase's cost), and **at most 6 pieces within a prop's radius (0.127 m, published: a 10-inch disc) of any point** (derived: the contact model holds 8 pairs per drone part at once, `ContactModel.PairsPerShape`, and a trunk and a branch take two; more is a `ContactsLost` event, which the first crown rows hit at 19 overlapping shapes on pine_006 before this rule; measured at the pieces' centres and where two overlap). Every sample cell lies in a piece or within `reach_m` outside (checked, the build refuses otherwise), and every point of a piece's surface lies within the half depth plus half a cell diagonal of a sample cell (measured and printed as the standoff). V01's 0.1 m standoff to the faces is not asked of cards: a flat card standing for a spray 0.2–0.6 m deep cannot be wrapped by convex pieces within 0.1 m of its plane under the 64-piece rule.

7. **Wind and snag.**
   - **Wind:** one box over the samples (crown or bush), bottom at the ground at the lowest, and for a tree whose crown box starts more than 0.3 m up a solid trunk column (porosity 0) of the trunk's median diameter below it (V01's convention). The box's porosity is its **optical porosity** β, measured: the opaque area at each of 0°, 45°, 90°, 135° (a bearing and its opposite project alike) is the count of 5 cm cells of its frontal rectangle that a drawn sample falls in or a wood triangle covers; β = 1 − mean opaque area / (D × height), D = perimeter / π the box's mean width, which is what MapWind multiplies (1 − β) by, so the box blocks the measured opaque area.
   - **Snag** (V01's rule): every capsule segment under 30 mm across an `edge` at its radius; the last segment of a member with a free tip a `hook` to that tip; a fork no record of its own; a twig without a capsule one polyline record through its rings (`hook` where free, `edge` where others leave it), material `twig`; a spray a `hook` along its line at the twig row's edge radius (2.5 mm), material `twig`. Each record names its member's material.
   - No gaps, nothing landable.

8. **LODs and the review sheet.** `lods` lists the prefab's LOD renderers by name with their triangles; a level shows from the distance where the previous level's screen-relative height is reached at a 60° vertical field of view, d = size / (2 h tan 30°) (derived, assumed fov), and `cull_distance_m` is the last level's. The review sheet (AST-12) is rendered by the pinned Blender (`tools/blender/vendor_review.py`, EEVEE) from a local bundle under `review-sheets/<ID>/_bundle/`: day and overcast; the 1.5 m (pilot height, level), 10, 50 and 200 m tiles and a four-bearing turntable as drawn (cards alpha-clipped at their cutoff and tinted by their base colour), and in the day sheet the capsules and shell, the wind volumes and snag records, and the drawn samples over the pale mesh; a 1.8 m scale bar in 0.1 m bands, the camera to the south-west, 20° down, 60° fov, as `ReviewCapture.cs` lays its sheets out. The look is an approximation of the friend's shaders (Principled BSDF, tints over 1 clamped, so a `_Dry` copy reads green); the sheets judge the physics against the drawn shape, not the colour. Looked at, all 44 (measured): the capsules follow the trunks and limbs, the shells wrap the drawn foliage, the samples sit on the drawn leaves and needles. The Terrain Sample's two bushes draw as opaque quads (their base map has no alpha and imports none), so their shell wraps the quads.

9. **What is pushed (D-089).** Pushed: the params, `assets-src/vendor_plants.json`, the generator and the review script, the metadata and its `.meta` files (a guid derived from the file's path, so every checkout writes the same). Metadata holds capsules, spheres, boxes and polylines fitted to the meshes, not their triangles; nothing reproduces a vendor mesh. Local only: the store, the review bundles and sheets (`review-sheets/` is ignored).

10. **Flightbench crown rows** (`tools/flightbench/ContactsCase.cs`, after the test range's V01_L row): for one variant of each tree family (IslandTree, Pine002_M, Pine004_1, Pine006_01, PineDead_02), a world of that one placement on a meadow plane, the legs-on drone with the airframe's effects on, 35 m south, held at the crown box's middle height and flown north at 10 m/s into it. A row passes when the drone's first touch is that tree (its shell, or PineDead_02's bare deadwood) with no tunnel and no contact pair lost, and prints the twig contacts, the impulse they took, and the prop strikes with what struck them.

## Per variant (measured, `python tools/blender/vendor_plants.py all`)

Wood capsules follow the mesh's tubes; branches are the sprays' (decision 5). The shell's "most at a prop" is the pieces within 0.127 m of one point (rule 6), "most in 2 m cube" lib.audit's rule (d) (64). AST-05's columns are lib.audit.run's, reported (decision 4): its findings over 5 cm, the farthest capsule surface from the wood and the farthest wood from every capsule; in every variant over 5 cm that farthest wood lies at the ground (y −0.64 to +0.02 m: root flares, stumps, buried bases), and the island trees' and pine_002_L's far capsules stand in their lobed bases and forks. Every variant's frame check passed (2 mm), every sample cell lies in a piece or within 0.1 m of one, and no gap or landable shape is written.

| Variant | Wood capsules (+ branches) | Shell pieces · half depth · most at a prop · most in 2 m cube | Optical porosity | Snag edges / hooks | AST-05 over 5 cm · capsule→mesh max · mesh→capsule max (mm) | Spec 3 cm, members ≥ 5 cm (mm) |
|---|---|---|---|---|---|---|
| IslandTree | 171 | 103 · 0.7 m · 9 · 28 | 0.60 | 0 / 459 | 81 · 493 · 511 | 493 (170 capsules) |
| IslandTree_Dry | 171 | 94 · 0.7 m · 9 · 26 | 0.64 | 0 / 459 | 81 · 493 · 511 | 493 (170 capsules) |
| Pine002_S2 | 286 | 7 · 0.5 m · 6 · 7 | 0.51 | 131 / 218 | 0 · 26 · 31 | 26 (10 capsules) |
| Pine002_M | 347 | 101 · 0.4 m · 6 · 38 | 0.71 | 86 / 347 | 5 · 142 · 79 | 142 (133 capsules) |
| Pine002_M_Dry | 347 | 101 · 0.4 m · 6 · 38 | 0.71 | 86 / 347 | 5 · 142 · 79 | 142 (133 capsules) |
| Pine002_L | 440 | 165 · 0.3 m · 6 · 32 | 0.70 | 4 / 415 | 41 · 526 · 293 | 526 (288 capsules) |
| Pine004_1 | 37 (+ 652) | 116 · 0.6 m · 7 · 25 | 0.70 | 26 / 642 | 1 · 42 · 55 | 42 (35 capsules) |
| Pine004_1_Dry | 37 (+ 652) | 116 · 0.6 m · 7 · 25 | 0.70 | 26 / 642 | 1 · 42 · 55 | 42 (35 capsules) |
| Pine004_3 | 28 (+ 269) | 256 · 0.3 m · 8 · 52 | 0.74 | 19 / 312 | 2 · 69 · 108 | 69 (24 capsules) |
| Pine006_01 | 10 (+ 43) | 44 · 0.35 m · 6 · 25 | 0.72 | 4 / 75 | 0 · 33 · 25 | 33 (4 capsules) |
| Pine006_01_Dry | 10 (+ 43) | 44 · 0.35 m · 6 · 25 | 0.72 | 4 / 75 | 0 · 33 · 25 | 33 (4 capsules) |
| Pine006_02 | 11 (+ 74) | 15 · 0.7 m · 6 · 13 | 0.76 | 12 / 109 | 0 · 32 · 23 | 32 (5 capsules) |
| PineDead_02 | 387 | 128 · 0.15 m · 3 · 50 | 0.87 | 232 / 231 | 12 · 169 · 171 | 169 (45 capsules) |
| DeadBush_4 | 349 | 7 · 0.6 m · 7 · 7 | 0.42 | 310 / 248 | 3 · 63 · 141 | 0 (0 capsules) |
| DeadBush_6 | 0 | 19 · 0.7 m · 11 · 18 | 0.50 | 0 / 688 | no wood | – |
| GreenBush | 0 | 6 · 0.7 m · 6 · 6 | 0.51 | 0 / 93 | no wood | – |
| GreenBush_Dry | 0 | 6 · 0.7 m · 6 · 6 | 0.51 | 0 / 93 | no wood | – |
| LeafyBush_P2 | 305 | 97 · 0.3 m · 9 · 55 | 0.45 | 261 / 68 | 3 · 60 · 38 | 0 (0 capsules) |
| LeafyBush_P2_Dry | 305 | 97 · 0.3 m · 9 · 55 | 0.45 | 261 / 68 | 3 · 60 · 38 | 0 (0 capsules) |
| RedBush_Dry | 0 | 13 · 0.4 m · 6 · 13 | 0.47 | 0 / 85 | no wood | – |
| SmallBushDry_A | 0 | 6 · 0.35 m · 6 · 6 | 0.41 | 0 / 10 | no wood | – |
| SmallBushDry_B | 0 | 7 · 0.3 m · 6 · 7 | 0.48 | 0 / 20 | no wood | – |

## Risks / Trade-offs
1. **Instance scale.** The terrain draws each instance at its heightScale (measured 0.5–1.5) and a widthScale within ±8 % of it; the metadata is at scale 1, and the placement rows carry no scale. Until the map format and MapWorld scale an instance's shapes (uniformly by the height scale; the ±8 % width cannot be carried by spheres and capsules exactly), physics and look disagree by up to half a tree. W2 and W3's.
2. **Branches under cards are assumed.** pine_004 and pine_006 draw no branch wood; their branches' size comes from an assumed slenderness, their line from the cards.
3. **No gap polygons in crowns.** The shell and the capsules are what the drone meets; the open crown of the island tree has passages no gap record names.
4. **The shell's standoff is the foliage half depth** (decision 6), larger than V01's 0.1 m: the drone feels a crown up to that far before the drawn needles, 0.3–0.7 m by the table. The pair rule drives it: smaller pieces crowd a prop's reach. Nine variants stay over that rule at every depth (7–11 pieces at a prop: the island trees, pine_004, the dead and leafy bushes) and are kept at their least crowded depth, reported; the crown rows lost no pair on their lines, but a drone pushed into the densest spot of one may (`ContactsLost`). Raising `ContactModel.PairsPerShape` for compliant shells, or a shell fit that overlaps less, is the physics engineer's and the next fit's.
5. **Dry copies** share their green prefab's mesh; their physics is the same but for the foliage material's cutoff.
6. **AST-05 is not met on scanned wood.** The island tree's trunk and thick limbs (69 % of the map's trees) are lobed 5–30 cm and its main fork merges two limbs in one ring; root flares and stumps (pine_002, PineDead_02, the dead bush's stems) spread at the ground. A round capsule at the section's mean stands up to half a lobe off the wood and leaves the other half of it outside (table). Two richer fits were tried and measured on the island tree before this one: strands inscribed in each lumpy section, holding every ring point within 2–4 cm with no cap on their number, took 1,037–2,328 capsules; capped at 8 per 0.3 m they took 603 and still left 255 over 5 cm (their exposed sides under the lobes, their ends outside where the lobes twist); the round fit takes 163 capsules with 68 over 5 cm (measured by sampling each capsule's exposed surface against the wood). QA and the physics engineer decide whether this stands, whether AST-05 gets a measured exception for scanned wood, or whether a finer fit is worth its cost.
7. **Shape counts.** The island tree carries the most shapes (table); 202 of them stand on the map. W3's cost work (L6) and the bench (PLT-04) are where that is weighed.
