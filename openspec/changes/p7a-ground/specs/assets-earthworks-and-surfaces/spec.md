# Spec Delta

## MODIFIED Requirements

### Requirement: S04 belt_straw ground patch
The pipeline SHALL build asset S04 (`ground_patch` generator, variant `S04_patch`) from its params file into a Unity prefab that copies the belt-edge ground of letter H: a lodged mat of dry straw over dark soil with bare soil patches, short green regrowth strips, small earth mounds, and, within 4 m of the patch centre, lying straws, standing stems with seed heads and belt twigs as geometry. Its dimensions, densities and colours SHALL sit inside the ranges of `docs/object-sheets/earthworks-and-surfaces.md` §S04, and its physics metadata SHALL carry the soil as a landable `surface:belt_straw` shape one mat depth below the mat top except where a pit cuts it, the mat over it as landable `straw` shapes open over every bare patch and draped into every pit, and each mound as a landable `surface:crater_spoil` shape (`review-physics.md` §3). It SHALL also carry (`p7a-ground`) its hidden pits at the `belt_straw` rate of K§7.2 (0.02 per m², 0.1–0.3 m deep, 0.05–0.3 m in radius), each a `features` record naming its shapes, the pit's floor and walls `surface:belt_straw` with the mat draped down them and never bridging them, and its top-layer straws, standing stems and twigs as `micro_elements`, the very elements its micro mesh draws; its body straws and blades stay visual only.

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
- **THEN** every non-micro LOD0 vertex lies within 5 cm of the landable shapes and every shape within 5 cm of the mesh, the mounds and the pits included, and the audit's lines name the baked straws as micro-detail meshes it skipped

#### Scenario: Bare patches are ground, not stains
- **WHEN** the S04 metadata and LOD0 mesh are read where the derived map shows a bare soil patch
- **THEN** the mesh steps down through the mat there by the mat depth D = 0.125 m along the patch's convex step outline, no shape stands above that floor, the floor is the `surface:belt_straw` soil box exposed through the opened `straw` mat, and the patch layout in the map and in the geometry come from one seeded definition, so the two never disagree

#### Scenario: Colour inside the sheet's range
- **WHEN** the derived albedo's mean over the patch is measured
- **THEN** it lies within ±10 % luminance of the sheet's carried mean #7a7055, with bare soil patches inside 10YR 2/1–3/1 (the G1 value; the sheet now measures H's trodden floors at #59544c, the target after G1 if the side-by-side asks) and the straw inside the sheet's dry-straw value, and the review sheet's day tile is compared with the H feed values in the comparison sheet

#### Scenario: A pit hides under the mat
- **WHEN** the S04 metadata, LOD0 mesh and review sheet are read at each pit
- **THEN** each pit's `features` record names every shape of its floor and walls, the soil is open over the pit and its floor lies at the record's depth, the mat's shapes and the drawn straw step down into it and none spans it, and at the 1.5 m tile the pit reads as a hollow in the straw, not a hole

#### Scenario: The physics elements are the drawn ones
- **WHEN** S04's `micro_elements` are compared with its baked micro mesh
- **THEN** every top-layer straw, standing stem and twig drawn has exactly one record on its centre line within 1e-6 m, no body straw or blade has one, no element is rooted inside a pit's opening or a bare patch's window except the straws strewn on the floor, and the record counts by kind are printed
