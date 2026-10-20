# assets-earthworks-and-surfaces Specification

## Purpose
The assets of the earthworks-and-surfaces object sheet (trenches, craters, banks, pits and the thirteen ground surfaces S01–S13): what each built asset must show and carry, judged against its reference letter and its object sheet. One requirement per asset id.

## Requirements

### Requirement: S04 belt_straw ground patch
The pipeline SHALL build asset S04 (`ground_patch` generator, variant `S04_patch`) from its params file into a Unity prefab that copies the belt-edge ground of letter H: a lodged mat of dry straw over dark soil with bare soil patches, short green regrowth strips, small earth mounds, and, within 4 m of the patch centre, lying straws, standing stems with seed heads and belt twigs as geometry. Its dimensions, densities and colours SHALL sit inside the ranges of `docs/object-sheets/earthworks-and-surfaces.md` §S04, and its physics metadata SHALL carry the soil as a landable `surface:belt_straw` shape one mat depth below the mat top, the mat over it as landable `straw` shapes open over every bare patch, and each mound as a landable `surface:crater_spoil` shape (`review-physics.md` §3).

#### Scenario: Built from params by one command
- **WHEN** `pwsh tools/build_asset.ps1 S04` runs on a clean checkout
- **THEN** it exits 0, writes `S04_patch.fbx`, `S04_patch.assetmeta.json`, the prefab and the review sheet, and a second run leaves `git status` clean

#### Scenario: Straws read as geometry at the pilot's height
- **WHEN** the review sheet's 1.5 m tile of S04 is read
- **THEN** separate lying straws (layered, resting on one another), standing stems, twigs and green blades are visible as meshes over the mat within 4 m of the centre, and the tile is compared side by side with letter H (frames 1–21) in the local comparison sheet, where the mat's colour family, its structure (a shaded tangle, tussocks, bare patches read as ground, blades over the strips) match by eye; what a G1 stand-in cannot show (the feed, the view height, heap height and density beyond the budget, the far layer) is listed in design §10 with where each is fixed

#### Scenario: No visible tiling at 10 m
- **WHEN** the 10 m and 50 m tiles are read
- **THEN** no repeating pattern of the straw set or the soil set is visible across the 12 m patch, and the far layer still shows straw grain

#### Scenario: Collision follows the mat
- **WHEN** the collision audit runs on `S04_patch`
- **THEN** every non-micro LOD0 vertex lies within 5 cm of the landable shapes and every shape within 5 cm of the mesh, the mounds included, and the audit's lines name the baked straws as micro-detail meshes it skipped

#### Scenario: Bare patches are ground, not stains
- **WHEN** the S04 metadata and LOD0 mesh are read where the derived map shows a bare soil patch
- **THEN** the mesh steps down through the mat there by the mat depth D = 0.125 m along the patch's convex step outline, no shape stands above that floor, the floor is the `surface:belt_straw` soil box exposed through the opened `straw` mat, and the patch layout in the map and in the geometry come from one seeded definition, so the two never disagree

#### Scenario: Colour inside the sheet's range
- **WHEN** the derived albedo's mean over the patch is measured
- **THEN** it lies within ±10 % luminance of the sheet's carried mean #7a7055, with bare soil patches inside 10YR 2/1–3/1 (the G1 value; the sheet now measures H's trodden floors at #59544c, the target after G1 if the side-by-side asks) and the straw inside the sheet's dry-straw value, and the review sheet's day tile is compared with the H feed values in the comparison sheet

### Requirement: E01 open trench with spoil parapets
The pipeline SHALL build asset E01 (`trench` generator; variant `E01_curving`) from its params file into a Unity prefab that copies the belt-edge trench of letter G: a narrow cut 0.6–1.0 m wide at the lip and 1.2–1.8 m deep with near-vertical spade-cut walls, a trampled floor with spilled clods, a level berm, broad low grassed spoil parapets on both sides, side niches, and a wander of 15–40° bends every 3–8 m, on its own ground patch. Dimensions SHALL sit inside `docs/object-sheets/earthworks-and-surfaces.md` §E01; the physics metadata SHALL carry the cut as filler shapes under the hole contract (sealed below, 0.3 m thick behind each open face, overlapping 0.2 m, lips level within 3 cm, nothing proud of the ground outside the cut, faces within 5 cm of the visual) with `surface:belt_bare`, the parapets and margins as landable `surface:belt_straw` shapes, the clods as `surface:crater_spoil` shapes, a gap polygon per metre and per bend, the open top as one slot per metre of run, and each niche's mouth, no wind volume, no snag, and a `holes` record of the run and one per niche.

#### Scenario: Built from params by one command
- **WHEN** `pwsh tools/build_asset.ps1 E01` runs on a clean checkout
- **THEN** it exits 0, writes `E01_curving.fbx`, its metadata, prefab and review sheet, and a second run leaves `git status` clean

#### Scenario: The corridor against the drone
- **WHEN** the default cut (0.8 m lip, 0.6 m floor, 1.5 m deep) is built and its cross-section gaps are read
- **THEN** every cross-section polygon spans from the floor to the lip with the walls at the batter, its `narrowest_m` matches the audit's inscribed circle within 5 mm, and the handoff lists the sheet's verdicts: nose-first everywhere (13 mm a side at the floor, 113 mm at the lip), yawed only in the top 0.7 m, a bend turnable only near the lip

#### Scenario: Fillers under the hole contract
- **WHEN** the collision audit and the validator run on `E01_curving`
- **THEN** every open face of a wall, floor or lip box lies within 5 cm of the visual (2 cm at the lips), each box is at least 0.3 m thick behind its face and overlaps its neighbour by at least 0.2 m, no box top stands above the ground outside the cut, the lip boxes are level with the patch within 3 cm, a downward ray on a 5 cm grid over the `holes` bands and 0.71 m past their edges (every cell a band can mark, by centre or by touch) hits a filler (F-1), every cavity face, the crumbled lip and the niches included, lies at least 0.65 m inside the union of the bands, so at least 0.25 m inside the hole cells at any placement yaw (F-2), and the audit prints the per-segment measurements

#### Scenario: The G look
- **WHEN** the review sheet's pilot-height tile (shot along the run from 1.5 m above the lip) and the 10 m tile are compared side by side with letter G (frames 1–12) in the local comparison sheet
- **THEN** the cut reads as a dark narrow slot winding between two broad pale grassed parapets, the parapets each read 2–4 times wider than the cut, the inside reads uniformly dark in shade with the sheet's colour, and the lips are crumbled, never a clean edge

#### Scenario: Parapets and clods are ground
- **WHEN** the metadata is read
- **THEN** every parapet and margin shape names `surface:belt_straw` and is landable, every clod names `surface:crater_spoil`, the cut's shapes name `surface:belt_bare` and are not landable, `wind_volumes` and `snag` are empty, and the `holes` record's centre line follows the run within 5 cm with the cut's top width and depth
