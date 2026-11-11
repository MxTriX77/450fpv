# Proposal: w3-farmland-ground

**Owner:** world-builder (the world-query core's heightfield) · **Also building:** physics-engineer (contact on slopes, the fiber's query cost, sign-off), game-developer (flight on the farmland) · **Reviewers:** tech-artist (drawn terrain against the heightfield near the drone), qa-engineer; **the pilot when they fly the farmland (D-093)**
**Requirements:** WLD-02, WLD-11, WLD-16, PHY-01, PHY-02, PHY-04, PLT-04 · **Decisions:** D-087, D-088, D-091, D-093, D-094 · **VISION:** §2 (lifting off and landing on real, uneven ground; long fiber flights), §3 (physics that is physical, seeded and logged) · **Plan:** `docs/final-world-plan.md` §4, unit W3 (with L6 folded in)

## Why
The sim's ground is a flat plane today. The farmland has rolling hills of 12–42 m, village plots up to 38° and tiles out to about 4.5 km (W2's map package, signed off in its design.md). The drone can't fly it until the world-query core knows those heights. L1 also found that the fiber's per-step shape queries made the model-79 sim fall behind real time; the farmland's 1,650 trees and bushes and its village would make that worse.

## What Changes
- **The heightfield** (world-builder): `SampleGround`, `Raycast` and `StaticContacts` over the 17 tiles of `maps/farmland/`.
  - Each tile is triangulated exactly as `tools/map/validate.py`'s `Tile.height`, in the same operation order: the SW to NE diagonal, + − × ÷ √ only.
  - Each tile has its own lookup. The object grid stays on the 1000 m map.
  - The 12 step lines are vertical walls, never height jumps: the surround's outer ledge and the far seams.
  - The hole mask is read (`holes/<tile>.raw`; none yet), and a point off the world is a hole.
  - Surfaces come per cell from `classes/`.
  - `QueryVersion` 8, 0 B in steady state, worldbench rows and goldens audited case by case.
- **The flight on hills** (physics-engineer): the legs' and crash contact on slopes of 0–38°, and the fiber's ground contact over the heightfield. Whether `tilled` fields with their `furrow_axis` need furrow relief is decided with a reason. Model 82 only if the contact model changes.
- **L6, the fiber's query cost** (physics-engineer): a laid segment resting near shapes no longer runs a full `StaticContacts` query every step. The fix is in the cost path, not for one asset.
- **Flight on the farmland** (game-developer): the player flies from the village start point (D-092). The test range leaves the player and stays for headless tests and benches. The drawn terrain near the drone is close enough to the heightfield that what's drawn is what's touched.

## Non-goals
- No trench holes or flattened footprints (W5), no tree or ruin physics data (W4a, W4b), no wind over hills (W7), and no ground past the far ring yet (W8).

## Impact
The sim package's World folder (the heightfield, `QueryVersion` 8), Contact and Tether (L6, slopes), `tools/worldbench` and `tools/flightbench` (rows, goldens), the session and menu (flight on the farmland), the farmland scene's terrain settings near the drone.
