# Proposal: p1-pipeline-hardening

**Owner:** asset-artist · **Reviewers:** tech-artist (its own task), physics-engineer (tolerances), qa-engineer
**Requirements:** AST-03, AST-05, AST-07, AST-12 · **Decisions:** D-004, D-028
**Source:** the phase-0 reviews (`openspec/changes/archive/2026-10-18-setup-asset-pipeline/review.md` suggestions 1–3, `review-physics.md` P1-1 and P1-2, `2026-10-18-setup-review-capture/review.md`) and `docs/reports/2026-10-18.md`.

## Why
Phase 0 proved the pipeline on one brick and found what would bite on the second asset: a nonsense parameter builds "successfully", nothing measures collision against the mesh automatically, cylinders cannot import, roughness and AO are imported but unused, the lone brick wears a wall texture, and an empty stage renders a review sheet with exit 0.

## What Changes
- `build.py`: generator parameter sanity (each generator declares ranges; out-of-range fails), validation before export, and the automated AST-05 audit: two-sided deviation between every collision primitive and the LOD0 mesh, failing beyond ±5 cm (±2 cm within 0.3 m of a gap or a landable face), plus the inscribed-circle check of `narrowest_m`.
- Derived textures by script (D-004): a packed mask map (metallic R, AO G, smoothness A) per material recipe, wired into URP/Lit; the recipe recorded next to the material.
- K01: faces UV'd into one brick cell of the set, so a lone brick shows one brick's surface.
- `CatalogImporter`: `cylinder` primitives as a convex mesh collider (noclip only; the sim reads the metadata).
- `Fpv450.Review.Capture`: fails with exit 1 when the asset renders no mesh in the 1.5 m view (LFS pointers, missing meshes), and prints the LOD bias it ran with.
- Quality levels: `lodBias` 1.0 on Low, Medium and High, so the metadata distances hold (tech-artist).
- The physics material list grows by sign-off: the ids the object sheets propose are added, mapped or declined by the physics engineer, and shapes may name a ground surface as `surface:<id>` (physics review P3-5).

## Capabilities
### New Capabilities
- none

### Modified Capabilities
- `asset-pipeline`: ADDED requirements for parameter sanity, the collision audit, derived textures and cylinder import.
- `asset-review-sheet`: ADDED requirement that an empty stage fails the capture.

## Non-goals
- No new assets; no impostors; no micro-detail; no gap probe (its own change).

## Impact
`tools/blender/`, `tools/assets/` (derived-texture script), `assets-src/materials/`, `CatalogImporter.cs`, `ReviewCapture.cs`, `ProjectSettings/QualitySettings.asset`, K01's params and outputs.
