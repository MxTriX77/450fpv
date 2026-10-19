# Proposal: p1-materials-per-part

**Owner:** physics-engineer · **Reviewers:** qa-engineer
**Requirements:** AST-05, PHY-01, PHY-03, PHY-05 · **Decisions:** D-024 (calculate, never ask), D-028 · **VISION:** §2.2 (the ground and what lies on it), §2.1 (liftoff stick and shake)
**Source:** the fortifications physics reading (`openspec/changes/archive/2026-10-18-p1-fortifications-sheet/review-physics.md` §4 and §9), `docs/plan.md` phase 1 step 7, `assets-src/physics_materials.json`.

## Why
The contact law applies a material row's `stiffness_n_per_m` to each part of the drone that touches it, not to the whole drone. The fortifications reading found two rows derived for the whole drone: `fabric` (600 N/m) and `mesh` (800 N/m), so four legs on a mattress sink 20 mm where the row meant 50–100 mm. It also accepted two new ids with per-part values, `razor_wire` and `sandbag`, to be added before their assets. Every row must be read the same way before the micro-detail generator and the phase-7 contact model rely on them.

## What Changes
- Every row of `assets-src/physics_materials.json` re-derived or confirmed per touching part (a leg foot, an arm, a prop guard), each with its label and rationale; the `fields` text for `stiffness_n_per_m` says it is per touching part.
- `razor_wire` and `sandbag` added with the fortifications reading's rows.
- `straw` checked against S04's re-measured mat (12.5 cm stands, `docs/object-sheets/earthworks-and-surfaces.md` §S04).
- Consumers checked: the audit's soft-top rule (a plain id at 1000 N/m or less is held at 5 cm), the object sheets that quote a sink or a stiffness, the fortifications sheet's sink numbers.

## Capabilities
### New Capabilities
- none

### Modified Capabilities
- `asset-pipeline`: ADDED a requirement that physics material rows are per touching part.

## Non-goals
- No surface rows (`surface:<id>`): their values are signed off in phase 3 with the surface table.
- No contact-law code (phase 6/7).
- No new asset.

## Impact
`assets-src/physics_materials.json`; possibly a soft-top line in an asset's audit output (reported, not hidden). Branch `unity/physics/p1-materials-per-part` off `unity/main`.
