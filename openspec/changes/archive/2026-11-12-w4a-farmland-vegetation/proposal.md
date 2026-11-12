# Proposal: w4a-farmland-vegetation

**Owner:** asset-artist · **Reviewers:** physics-engineer (the crown and snag data the sim reads), qa-engineer
**Requirements:** AST-05, AST-09, AST-10, AST-12, PHY-05, REAL-05 · **Decisions:** D-088, D-089 · **VISION:** §2 (flying among dense trees, branches that catch and cut) · **Plan:** `docs/final-world-plan.md` §4, unit W4a

## Why
The friend's 294 trees and 1,352 bushes are visual only: their colliders were removed and they carry no physics data. The drone would fly through them.

## What Changes
- **Every tree variant** (the broadleaf tree and the pines): the trunk and main limbs as capsules that follow the mesh, and the crown as volumes with our crown physics (drag, snag, prop strikes), as our own trees have them.
- **Every bush variant:** soft volumes with porosity and snag.
- **Generated, never hand-edited:** a script with per-variant params reads the vendor meshes and writes the metadata. The params and the metadata are pushed; anything that copies a vendor mesh's geometry stays local-only (D-089).
- **Grass:** no physics per blade. Its density per cell (from W2's export) drives the micro-detail on each ground class (AST-10).
- A review sheet per variant at 1.5 / 10 / 50 / 200 m (AST-12).

## Non-goals
- No change to the vegetation's look or placement, no new tree models, no per-blade grass physics.

## Impact
`assets-src/` (params and catalog entries for the vendor variants), `tools/assets/` or `tools/blender/` (the generator), `review-sheets/`, flightbench crown rows.
