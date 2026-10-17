# Proposal: setup-asset-pipeline

**Owner:** asset-artist · **Reviewers:** physics-engineer (the metadata contract), qa-engineer
**Requirements:** AST-03, AST-04, AST-05, AST-07, AST-08, SEC-04 · **Decisions:** D-003, D-004, D-019, D-028 · **VISION:** editability of models and textures
**Lessons:** knowledge.md §14.4 (one LOD system only; UVs in asset space)

## Why
Phase 1 builds about 100 assets. Without one pipeline, every agent would invent its own export and import, and editability (AST-03) would break. One command must take a params file to a Unity prefab with collider, material and physics metadata.

## What Changes
- `tools/assets/fetch.py` and `tools/assets/manifest.json`: pinned CC0 downloads from Poly Haven and ambientCG by id, resolution and SHA-256, with `--verify` and `CREDITS.md` generation.
- `tools/blender/build.py`: headless Blender run, params JSON → FBX (with LODs) plus an asset metadata JSON (collision primitives, material ids, gaps as polygons, wind volumes, snag flags, landable flags, LOD distances), in Unity's frame.
- The first generator and params: **K01, one whole brick** (250 × 120 × 65 mm, AST-04).
- `unity/Assets/_Project/Editor/CatalogImporter.cs` with `assets-src/catalog.json`: catalog plus metadata → prefab with LOD group, URP materials, colliders and a physics-metadata component.
- `tools/build_asset.ps1 <ID>`: the end-to-end command.

## Capabilities
### New Capabilities
- `asset-pipeline`: fetching third-party sources, building assets from params, importing them into Unity, and the asset metadata contract.

### Modified Capabilities
- none

## Non-goals
- No assets beyond K01 whole (phase 1).
- No review sheet here: `setup-review-capture` adds that step to `build_asset.ps1`.
- No impostors, no micro-detail, no gap probe (phase 1).

## Impact
New `tools/assets/`, `tools/blender/`, `assets-src/`, `tools/build_asset.ps1`, `unity/Assets/_Project/{Art,Prefabs,Editor}/`, `CREDITS.md`. Depends on `setup-unity-skeleton` for tasks 1.3 onward. Third-party files are committed through LFS (quota recorded in `docs/environment.md`).
