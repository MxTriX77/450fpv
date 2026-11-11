# Tasks: w1-farmland-world

Order: 1.1, then 2.1, then 3.1. Branch `unity/physics/physics-integration-2--w1-world`, cut from the build line after W0 landed.

## 1. The world in our project

- [ ] 1.1 [world-builder] Bring the farmland's scene, terrain tiles, vegetation and village into `unity/`. Extend `link.ps1` for the packs (local-only), push the friend's own text, keep our pipeline asset and renderer, add only the packages it needs. Write `design.md`: what is pushed and what stays local, how a fresh worktree gets the world, and what the friend's generator needs. Checks: EditMode, `precommit.py --tree HEAD`, `fetch.py --verify`, a fresh worktree opens the scene with no missing references.

## 2. The player on the farmland

- [ ] 2.1 [game-developer] The menu opens the farmland in noclip. Place one start point in the village on free level ground (D-092), with the launch stand when legs are off. Flight stays on the test range until W3. Checks: EditMode, the smoke test, load time to the first frame (PLT-06).

## 3. Review

- [ ] 3.1 [qa-engineer] Review against the spec; shots of the farmland in noclip beside the friend's project at the same cameras; the checks.
