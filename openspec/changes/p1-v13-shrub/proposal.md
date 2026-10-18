# Proposal: p1-v13-shrub

**Owner:** asset-artist · **Reviewers:** physics-engineer (the shell and hook metadata, by reading), qa-engineer
**Requirements:** AST-02, AST-03, AST-05, AST-07, AST-08, AST-09, AST-12, REAL-01, REAL-03, REAL-05, SEC-04 · **Decisions:** D-003, D-004, D-020, D-028 · **VISION:** §1 (a copy of the references), §2 (tree belts with bushes among the trees that the drone pushes into and snags on)
**Source:** `docs/object-sheets/vegetation.md` §V13 and the group's material and colour tables (the sheet is the input), `openspec/changes/p1-object-sheets/review-buildability.md` ("The look-lock set": V13 + V01 buildable now with the fallback ids; build order step 3: the shrub is the tree generator's skeleton and cards at a small scale), `openspec/changes/p1-pipeline-hardening/review-materials.md` (`bark` and `twig` added), PR-10.

## Why
Lane B of the look-lock starts with the understory shrub because it is the vegetation generator's skeleton and leaf cards at a small scale: the stem skeleton, the card mass, the LOD chain and the crown contact shell are written once here and V01's locust adds only size, the trunk and the impostor. V13 is also what makes a belt read as a wall at 1–2 m height (PR-10, letter H) and the first asset whose collision is a compliant shell over cards, which the collision audit cannot judge today.

## What Changes
- **Two CC0 sets pinned** (D-003): the bark set the sheet names for stems (Poly Haven `bark_brown_02`, grey-green tint) and one ambientCG LeafSet with small oval leaves (and, if a second fits the budget, one with small leaflets for the caragana look); `CREDITS.md` regenerated.
- **Leaf materials in the pipeline:** a recipe may be alpha-clipped and two-sided (URP/Lit alpha clipping with a cutoff, double-sided rendering), and the importer wires it.
- **The `shrub` generator** and three params files `V13_low`, `V13_mid`, `V13_tall`: a multi-stem skeleton from the base (stems as tapered tubes, ≥ 10 mm as geometry), a leafy card mass over it, autumn tint and `leaf_cover` as parameters, `bare_base_m` for the leggy inside variant, `spines` and `fruit_tufts` for the silverberry and honeysuckle looks; LOD0–LOD2 and a cull distance from the pixel rule.
- **Compliant shells in the collision audit:** a shape whose material is a crown contact material (`twig`, `foliage`) is a shell: the audit checks that the LOD0 mesh lies inside it (one-sided, within 5 cm) and does not require the shell to lie near the mesh.
- **Physics metadata** (AST-05): stems ≥ 10 mm as `bark` capsules, the leafy mass as a `twig` shell, no gaps inside a clump, one wind volume with the clump's porosity by state, `hook` snags on every stem from base to tip, no landable surface.
- **The catalog entry V13** and the side-by-side comparison against letter H (the edge clumps) and F (under the canopy) in a local `reference/_compare/` sheet, never committed (REAL-03).

## Capabilities
### New Capabilities
- `assets-vegetation`: the asset requirements of the vegetation object sheet (V01–V21), one requirement per asset; this change adds V13.

### Modified Capabilities
- `asset-pipeline`: ADDED requirements for alpha-clipped two-sided recipes and for compliant shells in the collision audit.

## Non-goals
- No impostor (V01 brings it), no wind sway shader (tech-artist, VIS-05), no bare or dry-dead state built as a variant (the parameters exist; G1 shows summer), no burn damage.
- No species label: the mix is a look (honeysuckle, caragana, silverberry), decided by eye at G1.
- No micro-detail, no physics tuning of `twig` and `bark` (the rows are the physics sign-off's).

## Impact
`tools/assets/manifest.json`, `CREDITS.md`, `assets-src/materials/{bark_shrub,leaf_shrub}.json`, `assets-src/vegetation/V13_{low,mid,tall}.json`, `assets-src/catalog.json`, `tools/blender/generators/shrub.py`, `tools/blender/lib/{skeleton,cards}.py` (shared with V01), `tools/blender/lib/audit.py` (shells), `unity/Assets/_Project/Editor/Assets/CatalogImporter.cs` (alpha clip, two-sided), `unity/Assets/_Project/Art/{ThirdParty,Materials,Models/vegetation}`, `unity/Assets/_Project/Prefabs/vegetation/`. Branch: `unity/art/p1-v13-shrub` off `unity/main` after `p1-pipeline-hardening` merges (it needs the parameter ranges, the audit and the `bark`/`twig` rows).
