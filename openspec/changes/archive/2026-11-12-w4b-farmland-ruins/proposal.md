# Proposal: w4b-farmland-ruins

**Owner:** asset-artist · **Reviewers:** physics-engineer (collision, gaps and materials the sim reads), qa-engineer
**Requirements:** AST-05, AST-06, AST-12, PHY-23, REAL-05, WLD-05 · **Decisions:** D-088, D-089, D-090 · **VISION:** §2 (narrow openings, ruins to fly into and through) · **Plan:** `docs/final-world-plan.md` §4, unit W4b

## Why
The friend's village (5 ruined wooden houses and 28 pieces of debris) is plain meshes with no physics data. The drone can't hit a wall, fly through a window or name what it crashed into.

## What Changes
- **Each ruin:** collision that follows the visual: walls with their thickness, every opening as a gap the sim knows, roof sheets, and broken edges.
- **Each debris piece:** collision and a material.
- **Materials:** wood, burnt wood and sheet metal, by part.
- **Generated, never hand-edited:** a script with per-piece params reads the vendor meshes and writes the metadata. The params and the metadata are pushed; anything that copies a vendor mesh's geometry stays local-only (D-089).
- A review sheet per ruin and per debris kind at 1.5 / 10 / 50 / 200 m (AST-12).

## Non-goals
- No change to the village's look or layout. No placement of our houses and cars (W5).

## Impact
`assets-src/` (params and catalog entries for the ruins and debris), `tools/assets/` or `tools/blender/` (the generator), `review-sheets/`.
