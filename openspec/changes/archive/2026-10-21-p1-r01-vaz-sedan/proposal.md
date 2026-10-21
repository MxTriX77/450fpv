# Proposal: p1-r01-vaz-sedan

**Owner:** asset-artist · **Reviewers:** tech-artist (its own task, the crazed-glass material), physics-engineer (the `sheet_metal`, `rubber`, `fabric` and landable metadata, by reading), qa-engineer
**Requirements:** AST-02, AST-03, AST-05, AST-06, AST-07, AST-08, AST-12, REAL-01, REAL-03, REAL-05, SEC-02 (blank plates), SEC-04 · **Decisions:** D-003, D-004, D-020, D-028 · **VISION:** §1 (a copy of the references), §2 (wrecks the drone lands on and snags in: roofs, bonnets, torn sheet)
**Source:** `docs/object-sheets/vehicles.md` §Rules shared by every vehicle (the clean base plus state kits, geometry, the three kits, the gap table, the shared physics, materials, the `vehicle` table) and §R01 (the sheet is the input); `openspec/changes/p1-object-sheets/review-buildability.md` (R01 needs the profile stations first, `rubber` and `fabric` for A: both added by the physics sign-off; build order step 5: F, then A); the M reference view (REAL-02).

## Why
Lane B's last step. The classic sedan is the most common vehicle of the map and the look-lock's test of torn sheet steel: fragment holes with petals, a folded bonnet, a crazed windscreen that is geometry, a car sitting on its rims after a fire, rust and soot from one damage mask. The `vehicle` generator (a clean base lofted through profile stations plus physical state kits) is written once here for R02–R09.

## What Changes
- **CC0 sets pinned** (D-003): the paint, rust, soot, chrome, tyre, seat and burnt-ground sets the sheet names (chosen among its candidates at the comparison). `CREDITS.md` regenerated.
- **The `vehicle` generator** with the R01 preset (the 2101's published dimensions: 4.073 × 1.611 × 1.44 m, wheelbase 2.424 m, tracks 1.349 / 1.305 m, clearance 0.17 m, 165/80 R13 wheels 0.594 m) and its side and plan profile stations derived by the asset-artist from published drawings (labelled derived in the preset); the clean base: a shell with real panel thickness, four framed doors with vent panes, bonnet, boot lid, chrome bumpers with overriders, mesh grille, twin round lamps, steel wheels with domed hubcaps, interior (seats, dashboard, steering wheel), the tube roof rack; blank plates.
- **The A and F kits** as physical edits (`state`, `severity`, `burst_dir` and the table's items): A (M) with the measured holes and petals, the bonnet corner folded against the crazed and holed windscreen, side glass gone with crumbs, one flat front tyre and the 1–2° lean, rust halos and dust in the mask; F with the car 0.17 m down on its rims, bead-wire rings, chrome dulled, the interior burnt to frames and springs, the roof sagging, soot and burnt-off steel in the mask, a feathered soot patch 4 m across under it. The A scatter pieces (a lamp bowl, a bezel ring, a trim plate) are built in the change as the first K07 pieces; glass crumbs are micro meshes.
- **The crazed-glass material** (tech-artist): a transparent slightly green glass shader reading a per-pane crack mask the generator writes from the impact points (radial and concentric lines), with the holed area cut from the pane's geometry.
- **Physics metadata** (AST-05, AST-06): the shared car plan, with as many primitives as AST-05's 2 cm zone round the landing faces needs (110 in the clean base as built, its panes as glass boxes, design 7): (three body boxes, thin boxes for bonnet, boot lid and roof, pillar capsules, glass boxes while in place, wheel cylinders, bumper boxes, interior boxes and the steering-wheel cylinder, the folded flap, the rack as capsules); `sheet_metal`, `steel`, `glass`, `rubber`, `fabric`; no gap (doors on); wind volumes (lower body 0, greenhouse 0.35 in A and 0.55 in F); `edge` snags on torn edges, petals of holes ≥ 0.08 m, the flap, bumper ends, rack tubes and burnt rim flanges, `hook` snags on seat springs and bead wires; landable roof boxes and rack side rails, bonnet and boot lid.
- **The catalog entries R01 and K07** and the side-by-side comparison against letter M (the A car) and O with the research for F in a local `reference/_compare/` sheet, never committed (REAL-03).

## Capabilities
### New Capabilities
- `assets-vehicles`: the asset requirements of the vehicles object sheet (R01–R09, K07), one requirement per asset; this change adds R01 and the first K07 pieces.

### Modified Capabilities
- `asset-pipeline`: ADDED requirement for generator-written crack masks bound to a transparent material (the `textures` role mechanism of `p1-h01-adobe-house`, extended to a per-pane role).

## Non-goals
- No B state, no variants 2 and 3 (2106 / 2107 fronts), no estate, no `pose` other than upright, no overgrowth (V17 does not exist yet), no `ground` other than soil for the gallery; the parameters exist and are range-checked.
- No R02–R09 presets, no trucks; no K07 beyond the three A pieces and the crumbs and the F bead wires; no strike or dent physics.

## Impact
`tools/assets/manifest.json`, `CREDITS.md`, `assets-src/materials/{car_paint,car_rust,car_soot,chrome,tyre,seat_fabric,soot_patch,car_glass}.json`, `assets-src/vehicles/R01_{A,F}.json`, `assets-src/debris/K07_*.json`, `assets-src/catalog.json`, `tools/blender/generators/{vehicle,debris_car}.py`, `tools/blender/lib/{loft,kits_vehicle,masks}.py`, `unity/Assets/_Project/Shaders/CrazedGlass.shader` (tech-artist), `unity/Assets/_Project/Editor/Assets/CatalogImporter.cs` (transparent recipe), `unity/Assets/_Project/Art/{ThirdParty,Materials,Models/{vehicles,debris}}`, `unity/Assets/_Project/Prefabs/{vehicles,debris}/`. Branch: `unity/art/p1-r01-vaz-sedan` off `unity/main` after `p1-h01-adobe-house` merges (lane B; it needs the `textures` roles and the mask atlas mechanism).
