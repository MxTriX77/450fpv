# Tasks: w4b-farmland-ruins

Order: 1.1, then 2.1. Branch `unity/physics/physics-integration-2--w4b-ruins`, cut from the build line after W0 landed.

## 1. The physics data

- [ ] 1.1 [asset-artist] For the 5 ruins and every debris kind: generate collision following the visual (walls, openings as gaps, roof sheets, broken edges) and materials (wood, burnt wood, sheet metal) from the vendor meshes, by a script with per-piece params. Push params and metadata; keep copied geometry local-only. Write `design.md` with the params and the physics metadata. A review sheet per ruin and debris kind. Checks: `python -m pytest tools`, `fetch.py --verify`, `precommit.py --tree HEAD`, a sim test that crashes into a ruin.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec; look at every review sheet; the checks.
