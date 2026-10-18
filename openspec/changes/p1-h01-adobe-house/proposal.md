# Proposal: p1-h01-adobe-house

**Owner:** asset-artist · **Reviewers:** tech-artist (its own task, the facade and damage-mask material), physics-engineer (the opening gaps, the debris boxes and the `masonry`/`timber`/`glass` metadata, by reading), qa-engineer
**Requirements:** AST-02, AST-03, AST-04, AST-05, AST-06, AST-07, AST-08, AST-12, REAL-01, REAL-03, REAL-05, SEC-04, PR-5 · **Decisions:** D-003, D-004, D-020, D-028, D-032 · **VISION:** §1 (a copy of the references), §2 (damaged houses flown into through doors, windows and stripped roofs; rubble in the colours of the house)
**Source:** `docs/object-sheets/houses-and-yard.md` §Shared (walls, roofs, joinery, how they fail, the rubble rule, the shared house generator table) and §H01; `docs/object-sheets/lines-roads-debris.md` §K02 and §K03 (the pieces the rubble is made of); `openspec/changes/p1-object-sheets/review-buildability.md` (build order step 4: the largest look risk, built with K02 and K03; needed before G1: the tech-artist's facade-map and damage-mask material); `docs/knowledge.md` §14.4 (the first attempt's chequerboard walls, UVs in asset space); the E reference view (REAL-02).

## Why
Lane B's third step and the largest look risk of the whole phase: the first attempt's walls passed every check and failed by eye. H01 in its three states is where upright continuous facade UVs, the layered whitewash-render-adobe material with its damage mask, jagged openings as real clear space (PR-5), every tile and batten as a mesh, and rubble coloured from the house's own materials are proven once, before sixty more buildings reuse the generator. The E house is the reference view the pilot knows best.

## What Changes
- **CC0 sets pinned** (D-003): the adobe, render, whitewash, interior whitewash, clay-tile, roof-timber and door sets the sheet names (chosen among its candidates at the comparison; `red_brick` is pinned already for the chimney). `CREDITS.md` regenerated.
- **The K02 and K03 debris kits** (`debris_tile`, `debris_timber` generators with their `piece` enums) and the params of the pieces H01's rubble needs: `K02_tile_shard`, `K02_tile_whole`, `K03_batten_broken`, `K03_board`, `K03_rafter_broken`; their piece builders are library modules the house generator composes.
- **The `house` generator** (the shared house table) and params `H01_I`, `H01_D`, `H01_X` of variant 1 (the E house: 10 × 6 m, walls 2.4 m, 0.45 m thick, hip at one end and gable at the other, 38° roof, clay tiles on 0.33 m battens, rafters at 1.0 m, the 0.8 × 1.9 m plank door, 0.8 × 1.0 m windows, chimney, the interior with its main beam, joists, ceiling boards, blue whitewash and floor at grade); the `damage` dial and every item of the table, with I, D and X at 0, 0.55 and 1.0; the generator writes the per-wall damage-mask atlas (R render depth, G soot, B dirt, A base band) as a derived texture beside the FBX.
- **Facade material** (tech-artist): a URP shader that layers whitewash, clay render and adobe (each a tiled set in wall space, upright and continuous) by the mask's R channel, darkens by soot G, streaks by dirt B and tints the base band by A, so the same material dresses every wall of every house from its own mask.
- **Physics metadata** (AST-05, AST-06): walls as boxes per segment between openings, the plate, each rafter, each batten, each remaining tile row, the chimney, the door leaf at its hung angle, each window frame's four members, interior beams and debris as boxes and capsules, the tile heap as a sloped box; `masonry`, `timber`, `glass`, `cable`; every opening's clear polygon after jags as a gap with `narrowest_m` (door, windows with and without frame, roof bays, gable and wall breaches); wind volumes (solid walls, the roof field by its strip, the interior cavity with the door and far window open); snags on batten ends, rafter tails, the chimney cap, the hanging leaf, frame remnants, the interior cable, nails; no landable surface.
- **The catalog entries H01, K02, K03** and the side-by-side comparison against letter E (facade 1–12, door approach 13–15, interior 16–18) and J (stripped roofs) in a local `reference/_compare/` sheet, never committed (REAL-03).

## Capabilities
### New Capabilities
- `assets-houses-and-yard`: the asset requirements of the houses-and-yard object sheet (H01–H06, O01–O06, C01–C02, Y01–Y11), one requirement per asset; this change adds H01.

### Modified Capabilities
- `assets-lines-roads-debris`: ADDED requirements for K02 and K03 (the capability is introduced by `p1-l01-l03-pole-wires`; if that change has not merged first, the delta carries the Purpose).
- `asset-pipeline`: ADDED requirements for generator-written mask atlases and for composed kit pieces.

## Non-goals
- No variants 2 and 3 (the parameters exist), no `burnt` build, no veranda, no furniture beyond the sheet's floor debris, no K02 slate pieces and no K03 beams or log sections (their `piece` values exist and are range-checked; built with H02–H04 and K kits later).
- No tile breakability (a later material property), no runtime rocking planks (phase 7), no yard (O, Y, V, S09 assets).

## Impact
`tools/assets/manifest.json`, `CREDITS.md`, `assets-src/materials/{adobe,clay_render,whitewash,whitewash_blue,clay_tile,roof_timber,plank_door,facade_h01}.json`, `assets-src/houses/H01_{I,D,X}.json`, `assets-src/debris/K02_*.json`, `assets-src/debris/K03_*.json`, `assets-src/catalog.json`, `tools/blender/generators/{house,debris_tile,debris_timber}.py`, `tools/blender/lib/{walls,roof,joinery,rubble,masks}.py`, `tools/blender/lib/{meta,export}.py` (mask atlases), `unity/Assets/_Project/Shaders/FacadeLayered.shader` (tech-artist), `unity/Assets/_Project/Editor/Assets/CatalogImporter.cs` (the facade recipe), `unity/Assets/_Project/Art/{ThirdParty,Materials,Models/{houses,debris}}`, `unity/Assets/_Project/Prefabs/{houses,debris}/`. Branch: `unity/art/p1-h01-adobe-house` off `unity/main` after `p1-v01-locust` merges (lane B; it needs the pipeline as hardened by S04 and V13: micro meshes, alpha-clipped recipes, shells).
