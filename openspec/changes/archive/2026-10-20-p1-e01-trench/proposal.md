# Proposal: p1-e01-trench

**Owner:** asset-artist · **Reviewers:** tech-artist (its own task, the earthwork framing of the review sheet), physics-engineer (the filler boxes and gap polygons against the hole contract, by reading), world-builder (the `holes` record it will compile, by reading), qa-engineer
**Requirements:** AST-02, AST-03, AST-05, AST-07, AST-08, AST-10, AST-12, REAL-01, REAL-03, REAL-05, SEC-04, PR-9 · **Decisions:** D-003, D-004, D-020, D-021 (parameters only), D-028 · **VISION:** §1 (a copy of the references), §2 (trenches a little wider than the drone, flown inside)
**Source:** `docs/object-sheets/earthworks-and-surfaces.md` §E01 and the group's shared rules (ground is geometry; the hole-filler contract F-1…F-8; soil colours; the trench-family table), `openspec/changes/p1-object-sheets/review-buildability.md` (E01 waits on `soil` or `surface:<id>`, now settled by the physics sign-off, and on the trench split; build order step 6), `openspec/changes/p1-pipeline-hardening/review-materials.md` (`soil` → `surface:belt_bare` for cut walls and floors, `surface:crater_spoil` for loose spoil), `docs/legacy/map-and-world/terrain-holes-review.md` §2–3, the handover (E01 is a trench asset placed on a small patch in the look-lock gallery).

## Why
Lane A's last step. The trench is the manifesto's "a little wider than the drone" corridor (PR-9) and the first asset whose ground is cut: it proves the hole-filler contract on real geometry (filler boxes with `surface:belt_bare`, lips level, nothing proud), writes the gap polygons the gap probe will fly, and gives G1 the G view: a narrow cut winding along a belt edge with broad grassed parapets, under low sun.

## What Changes
- **CC0 sets pinned** (D-003): the beaten-loam set the S05 row names for the cut walls (Poly Haven `dirt_floor` or `forest_ground_04`, chosen at the comparison); the parapets and margins take a `grassed_spoil` cover recipe on S04's straw sets (task 1.4: S04's `belt_straw` recipe used as is stamped its bare patches with no relief under them); their physics stays `surface:belt_straw`. `CREDITS.md` regenerated.
- **A `trench_wall` ground recipe** (the S04 `ground` kind in wall space): the loam set under the soil set, tinted to the shaded cut colour, with a wall mask (topsoil band, root fringe in the top 0.2 m, clod spill at the foot) and a spade-facet normal of under 2 cm relief; a strip map along the run with the tiled set as the detail layer.
- **The `trench` generator** (the trench-family table: `shape`, cut sizes, berm, parapets, wander, niches, `age`, `revetment` none, `collapse` 0, `overgrowth` 0) and `assets-src/earthworks/E01_curving.json`: a 24 m curving run (G) cut into its own patch of about 35.5 × 13.75 m on its own grid (not S04's `ground_patch`, task 1.1), with spoil parapets and a berm under a lodged-straw mat baked from S04's kit (task 1.6), two side niches, crumbled lips with grass over them, spilled clods on the floor as geometry and blown-in straw as micro meshes from S04's kit.
- **Physics metadata** (AST-05, the hole contract): per 1 m segment (shorter on bends) a floor box, two wall boxes at the batter and level lip boxes, each 0.3 m thick behind its face and overlapping its neighbours by 0.2 m, faces within 0.05 m of the visual, material `surface:belt_bare`; parapets as tilted boxes with `surface:belt_straw`, landable; clods as `surface:crater_spoil` spheres; a cross-section gap polygon every metre and at every bend, the open top as one slot per metre of run, and each niche's mouth, with `narrowest_m`; no wind volume (F-8); no snag; and a `holes` record (the run's centre line, top width, depth, band width) for the map compiler.
- **The review sheet frames an earthwork**: for catalog class `earthworks` the pilot-height tile is shot from 1.5 m above the lip at one end of the run looking along it (the G view); the other tiles keep their framing.
- **The catalog entry E01** and the side-by-side comparison against letters G (frames 1–12) and B (1–6) in a local `reference/_compare/` sheet, never committed (REAL-03).

## Capabilities
### New Capabilities
- none

### Modified Capabilities
- `assets-earthworks-and-surfaces`: ADDED requirement for E01 (the capability is introduced by `p1-s04-belt-straw`, which merges first).
- `asset-pipeline`: ADDED requirement for the `holes` record in the metadata.
- `asset-review-sheet`: ADDED requirement that an earthwork's pilot-height tile is shot along the run.

## Non-goals
- No straight or zigzag variant built for G1 (the `shape` parameter exists and is range-checked; the zigzag's corner rule is in the design), no fresh state (`age` 0) build, no wet state, no crater damage, no revetment (E02), no collapse (E03), no dugout (E04).
- No terrain hole in a heightfield (phase 3 compiles the `holes` record); no cavity wind model (physics, phase 7); no root snags (deferred X-10).

## Impact
`tools/assets/manifest.json`, `CREDITS.md`, `assets-src/materials/trench_wall.json`, `assets-src/earthworks/E01_curving.json`, `assets-src/catalog.json`, `tools/blender/generators/trench.py`, `tools/blender/lib/straw.py` (reuse from S04), `tools/blender/lib/{holes,strip,audit}.py`, `tools/assets/derive.py`, `assets-src/materials/grassed_spoil.json`, `tools/blender/lib/meta.py` (`holes`), `unity/Assets/_Project/Scripts/World/AssetPhysicsMeta.cs` (`holes`), `unity/Assets/_Project/Editor/Review/ReviewCapture.cs` (earthwork framing), `unity/Assets/_Project/Art/{ThirdParty,Materials,Models/earthworks}`, `unity/Assets/_Project/Prefabs/earthworks/`. Branch: `unity/art/p1-e01-trench` off `unity/main` after `p1-l01-l03-pole-wires` merges (lane A; it needs S04's patch, kit, recipe kind, micro meshes and surface framing).
