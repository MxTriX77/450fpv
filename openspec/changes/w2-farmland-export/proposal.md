# Proposal: w2-farmland-export

**Owner:** world-builder · **Reviewers:** physics-engineer (signs off the format W3 reads), qa-engineer
**Requirements:** WLD-09, WLD-11, WLD-12, WLD-16, AST-05, AST-10, AST-11 · **Decisions:** D-088, D-090, D-091 · **VISION:** §3 (one map; physics that is physical and logged) · **Plan:** `docs/final-world-plan.md` §4, unit W2

## Why
The sim core never reads Unity: it reads the engine-agnostic map package (`maps/<id>/`). The farmland has none yet, so the sim cannot fly it, W3 has no heights to triangulate, and W5 has no fields to place our objects in.

## What Changes
- An exporter in our repo (`Fpv450.FarmlandExport`, an editor script) writes `maps/farmland/` from the scene the friend's generator built. It reads the generator's layout by reflection and never edits the generator:
  - **The heights** of the map, the surround and the far ring (D-091: heights everywhere, objects only on the map), as Unity's terrain stores them: the raw heightmap samples, the tile sizes and positions, so W3 can triangulate them exactly as Unity does.
  - **The ground classes:** the 8 layers mapped to classes in `surfaces.json`, per cell.
  - **The grass density** per cell, which drives the micro-detail (AST-10).
  - **Every placement** of a tree, bush, ruin and debris piece, with its variant id, position, yaw and scale.
  - **The outlines** of the 11 fields and the village, for W5.
- The format is documented in the map package's docs, and `tools/map/validate.py` checks it.
- Same seed, same bytes.

## Non-goals
- No sim code (W3 reads the heights), no physics metadata per variant (W4a, W4b), no placement of our objects (W5).

## Impact
The exporter (`unity/Assets/_Project/Editor/Farmland/`), `maps/farmland/` (LFS for the large files), `tools/map/validate.py` and its tests, the map package docs.
