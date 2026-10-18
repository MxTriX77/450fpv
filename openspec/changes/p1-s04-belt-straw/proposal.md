# Proposal: p1-s04-belt-straw

**Owner:** asset-artist · **Reviewers:** tech-artist (its own task, the surface framing of the review sheet), physics-engineer (the surface metadata, by reading), qa-engineer
**Requirements:** AST-02, AST-03, AST-05, AST-07, AST-08, AST-10, AST-11, AST-12, REAL-01, REAL-03, REAL-05, SEC-04 · **Decisions:** D-003, D-004, D-020, D-028, D-032 · **VISION:** §1 (the game is a copy of how the references look), §2 (the ground the drone lands on: uneven straw, hooked legs)
**Source:** `docs/object-sheets/earthworks-and-surfaces.md` §S04 (the sheet is the input; its numbers are not re-derived here), `openspec/changes/p1-object-sheets/review-buildability.md` ("The look-lock set": S04 is buildable now; recommended build order, step 1), `docs/briefs/phase-1-assets.md` step 4, `docs/reference-notes/terrain.md` T4.

## Why
The look-lock gate G1 needs one ground surface under every other station, and S04 belt_straw is the ground of the manifesto's landing case: a lodged mat of dry straw over dark soil, standing stems through it, legs resting at different heights. It is the cheapest look-lock asset and it proves three pipeline pieces every later asset needs: the CC0 fetch of a second and third set, a derived ground map tinted to a measured albedo, and "no visible tiling at 10 m" (AST-07). It is also the first asset whose close look (0.3–1.5 m, letter H) must read as separate straws, never as a texture (REAL-05).

## What Changes
- **Two CC0 sets pinned** (D-003): the soil set the sheet names (Poly Haven `brown_mud_02`) and the straw set it names (TextureCan Ground 0018) if its files can be pinned by URL and SHA-256 from a `texturecan` source of `fetch.py`; otherwise an equivalent CC0 straw or hay ground set from Poly Haven or ambientCG, with the substitution recorded in the sheet's material line. `CREDITS.md` regenerated.
- **A `ground` material recipe kind** (D-004 derived textures): `tools/assets/derive.py` blends the soil and straw sets by a seeded patch mask (bare soil patches, green regrowth strips, a weak lodging direction) into one unique 2K albedo, normal and packed mask for the patch, tinted to the sheet's carried mean albedo, and the recipe names the straw set as a URP/Lit **detail layer** tiled at the set's metre size, so the far layer has grain without tiling. The importer wires the detail layer.
- **The `ground_patch` generator** and `assets-src/surfaces/S04_patch.json`: a 12 × 12 m patch with the mat's relief and small earth mounds, and the **S04 instance kit** (lying straws, standing stems with seed heads, belt twigs: tapered tubes from a `straw_kit` module with the sheet's parameters) baked as visual-only micro-detail into LOD0 within 4 m of the centre, so the 1.5 m view shows straws as geometry at G1. The physics-parity scatter of phase-1 step 7 (AST-10) replaces the bake and reuses the kit module.
- **Visual-only micro-detail meshes in the pipeline:** a generator may return meshes that are visual only (the baked straws); the metadata lists them with their kind, the collision audit skips them, the importer renders them with their LOD.
- **Physics metadata** authored with the asset (AST-05): the mat as a landable box with `surface:belt_straw`, each mound its own landable shape with `surface:crater_spoil`, no gaps, no wind volumes; the straws' hooks are the micro-detail generator's (AST-10), stated as a G1 deviation.
- **The review sheet frames a surface** (AST-12): for a `surfaces` asset the 1.5 m tile stands at pilot height on the patch looking down at the mat (the H view), instead of being pushed outside the 12 m bounds; the other tiles keep their slant shots.
- **The catalog entry S04** and the side-by-side comparison against letter H (and G for the low-sun look) in a local `reference/_compare/` sheet, never committed (REAL-03).

## Capabilities
### New Capabilities
- `assets-earthworks-and-surfaces`: the asset requirements of the earthworks-and-surfaces object sheet (E01–E07, S01–S13), one requirement per asset; this change adds S04.

### Modified Capabilities
- `asset-pipeline`: ADDED requirements for the `ground` recipe kind with a detail layer, for visual-only micro-detail meshes, and for a pinnable third source (if TextureCan is taken).
- `asset-review-sheet`: ADDED requirement that a surface asset's pilot-height tile is shot from on the surface.

## Non-goals
- No physics-parity micro-detail, no near-ring fade, no far density layer (AST-10, phase-1 step 7): the baked straws are a G1 stand-in.
- No wet variant (D-021), no blast-flattened or burnt damage pattern: the dry autumn look of H only. The wet rules are parameters the material table already carries.
- No terrain blending or feathering into neighbouring surfaces (phase 3); no other surface of S01–S13.
- No change to the straw's physics values: the `belt_straw` row is carried from the first attempt (`surfaces.json`), and the sheet's hook rate is a flight-tuning point.

## Impact
`tools/assets/fetch.py` (a source, if taken), `tools/assets/derive.py` (ground recipes), `tools/assets/manifest.json`, `CREDITS.md`, `assets-src/materials/belt_straw.json`, `assets-src/surfaces/S04_patch.json`, `assets-src/catalog.json`, `tools/blender/generators/ground_patch.py`, `tools/blender/lib/straw.py`, `tools/blender/lib/{audit,export,meta}.py` (micro meshes), `unity/Assets/_Project/Editor/Assets/CatalogImporter.cs` (detail layer, micro meshes), `unity/Assets/_Project/Editor/Review/ReviewCapture.cs` (surface framing), `unity/Assets/_Project/Art/{ThirdParty,Materials,Models/surfaces}`, `unity/Assets/_Project/Prefabs/surfaces/`. Branch: `unity/art/p1-s04-belt-straw` off `unity/main` after `p1-pipeline-hardening` merges (it needs the parameter ranges, the audit, `derive.py` and `surface:<id>`).
