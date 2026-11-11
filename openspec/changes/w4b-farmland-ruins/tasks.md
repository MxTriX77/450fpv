# Tasks: w4b-farmland-ruins

Order: 1.1, then 1.2, then 1.3, then 2.1. Branch `unity/physics/physics-integration-2--w4b-ruins`, cut from the build line after W0 landed.

## 1. The physics data

- [x] 1.1 [asset-artist] For the 5 ruins and every debris kind: generate collision following the visual (walls, openings as gaps, roof sheets, broken edges) and materials (wood, burnt wood, sheet metal) from the vendor meshes, by a script with per-piece params. Push params and metadata; keep copied geometry local-only. Write `design.md` with the params and the physics metadata. A review sheet per ruin and debris kind. Checks: `python -m pytest tools`, `fetch.py --verify`, `precommit.py --tree HEAD`, a sim test that crashes into a ruin.
- [x] 1.2 [asset-artist] Collision follows the visual (REAL-05): where the vendor mesh shows a wall as two skins with nothing between them, the collision is the two skins, open between them, not a solid fill. The audit shows no collision more than its tolerance off the visual. Add `village_ruins.fbx` to the manifest as local-only. Keep burnt wood as `timber` and the roof as `asbestos_cement`. W2's placements name the mirrored house `Ruin_Walls` with scale x = -1: keep `Ruin_Walls_mirrored`'s metadata, and note in `design.md` that a placement with a negative scale uses it. Checks: as 1.1, with the audit's counts before and after.

- [x] 1.3 [asset-artist] QA's fix round (`review.md`):
  - (F1, D-089) Snag records and every other pushed field must not reproduce vendor vertices. Today the debris' `_open_edges` copies their boundary vertices exactly. Derive the snag edges from the collision boxes or resample them so that no vendor vertex can be read back. Add a test that fails if any pushed coordinate matches a mesh vertex within 0.1 mm.
  - (F2) Every enclosed opening of at least 0.25 m by 0.25 m that is clear in the visual and in the collision gets a gap, not only the intact house's openings (37 are missing).
  - (F3) A roof hole's gap is the clear space, never overlapping roof-frame members that are still there.
  - Also: F4 (Burnt's collision[777] reaching into porch_wall_window8); F5 (design §2 and §10: rows carry the full `rotation_xyzw`); F6 (a test that flies a capsule through every drone-sized gap).
  - Checks: as 1.2, with QA's vertex-leak and gap probes re-run.

## 2. Review

- [x] 2.1 [qa-engineer] Review against the spec; look at every review sheet; the checks.
