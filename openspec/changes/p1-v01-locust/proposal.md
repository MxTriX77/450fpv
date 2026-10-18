# Proposal: p1-v01-locust

**Owner:** asset-artist · **Reviewers:** physics-engineer (the capsule count, the crown shells and the inter-limb gaps, by reading), world-builder (the segment budget against the world query, by reading), qa-engineer
**Requirements:** AST-02, AST-03, AST-05, AST-06, AST-07, AST-08, AST-09, AST-12, REAL-01, REAL-03, REAL-05, SEC-04 · **Decisions:** D-003, D-004, D-020, D-028 · **VISION:** §1 (a copy of the references), §2 (belts the drone flies through and snags in; the belt as a dark band on the horizon)
**Source:** `docs/object-sheets/vegetation.md` §V01, the group's material and colour tables and the shared tree table (the sheet is the input), `openspec/changes/p1-object-sheets/review-buildability.md` (build order step 3: V13 then V01; V01 adds the impostor), `openspec/changes/p1-v13-shrub/design.md` (the skeleton and card modules V01 grows), AST-09.

## Why
Lane B's second step: the default belt tree. It grows the shrub's skeleton and card modules into the tree generator every other tree reuses (sizes, a sinuous trunk, main limbs and orders, an open scraggly crown, states by `leaf_cover`), it is the asset the drone meets most (the belt corridor, the crown snags, the blow-through gaps), and it is where "at 200 m a belt reads as a dark band" (AST-09, VIS-04) is first judged: the impostor and the far colour are built here.

## What Changes
- **CC0 sets pinned** (D-003): the bark set the sheet names (Poly Haven `bark_willow` or `bark_brown_01`, chosen at the comparison) and one ambientCG LeafSet with small oval leaflets; the pod strip as a derived 2-card texture; `CREDITS.md` regenerated.
- **The `tree` generator** on `lib/skeleton.py` and `lib/cards.py` (V13's modules, extended with a trunk, sinuosity, main limbs, branching orders and a crown shape) and params `V01_S`, `V01_M`, `V01_L` (6 / 10 / 14 m), each with two seeds for fork and lean draws; states by `leaf_cover` (G1 builds the late-summer part-bare state of F at 0.6 and the summer state at 0.9), pods on 20 % of twig tips in the autumn and bare states.
- **The impostor LOD:** a far LOD baked in headless Blender at build (an 8-view card atlas from the LOD0 tree, 1k, with the leaf tint), so a belt at 200 m shows crowns of the right darkness and silhouette; the importer builds it as the last LOD before the cull.
- **Physics metadata** (AST-05): trunk capsules per 1–2 m following lean and sinuosity, every limb and branch of 10 mm or more as a capsule (counted and printed by the generator), a `twig` shell per main limb, every inter-limb opening of 0.5 m or more as a gap polygon with its narrowest width, wind volumes (a solid trunk column, crown pieces with porosity by state), `hook` snags on every capsule under 30 mm, no landable surface.
- **The catalog entry V01** and the side-by-side comparison against letters F (the corridor, trunks and part-bare crowns), H (the belt wall) and G (the openwork strip) in a local `reference/_compare/` sheet, never committed (REAL-03).

## Capabilities
### New Capabilities
- none

### Modified Capabilities
- `assets-vegetation`: ADDED requirement for V01 (the capability is introduced by `p1-v13-shrub`, which merges first).
- `asset-pipeline`: ADDED requirement for a baked impostor LOD.

## Non-goals
- No V16 suckers, no V07 damaged or V08 dead variants (the same skeleton, their own changes), no wind sway shader (tech-artist, VIS-05), no belt placement (the world's).
- No leaf translucency shader; the cards use V13's alpha-clipped material.
- No tuning of `bark` and `twig`.

## Impact
`tools/assets/manifest.json`, `CREDITS.md`, `assets-src/materials/{bark_locust,leaf_locust,pod_locust}.json`, `assets-src/vegetation/V01_{S,M,L}.json`, `assets-src/catalog.json`, `tools/blender/generators/tree.py`, `tools/blender/lib/{skeleton,cards,impostor}.py`, `tools/blender/build.py` (the impostor render step), `unity/Assets/_Project/Editor/Assets/CatalogImporter.cs` (impostor LOD), `unity/Assets/_Project/Art/{ThirdParty,Materials,Models/vegetation}`, `unity/Assets/_Project/Prefabs/vegetation/`. Branch: `unity/art/p1-v01-locust` off `unity/main` after `p1-v13-shrub` merges (lane B is sequential: it builds on the shrub's modules).
