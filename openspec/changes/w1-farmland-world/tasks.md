# Tasks: w1-farmland-world

Order: 1.1, then 2.1, then 2.2, then 2.3 and 2.4, then 3.1. Branch `unity/physics/physics-integration-2--w1-world`, cut from the build line after W0 landed.

## 1. The world in our project

- [x] 1.1 [world-builder] Bring the farmland's scene, terrain tiles, vegetation and village into `unity/`. Extend `link.ps1` for the packs (local-only), push the friend's own text, keep our pipeline asset and renderer, add only the packages it needs. Write `design.md`: what is pushed and what stays local, how a fresh worktree gets the world, and what the friend's generator needs. Checks: EditMode, `precommit.py --tree HEAD`, `fetch.py --verify`, a fresh worktree opens the scene with no missing references.

## 2. The player on the farmland

- [x] 2.1 [game-developer] The menu opens the farmland in noclip. Place one start point in the village on free level ground (D-092), with the launch stand when legs are off. Flight stays on the test range until W3. Checks: EditMode, the smoke test, load time to the first frame (PLT-06).

- [x] 2.2 [tech-artist] The player quits and flies again cleanly after the feed has drawn the farmland. Today the D3D12 review player crashes on quit (0xC0000005 in D3D12Core.dll) in 30 of 36 runs, or hangs at startup or in the next flight. The feed off, `-force-d3d11` and the test range's feed all exit cleanly; design.md §6 has the bisection. Find the root cause and fix it in our feed or renderer, not by switching the graphics API or the feed off. Checks: the smoke test exits 0 in 20 runs in a row on the review build, EditMode, `precommit.py --tree HEAD`.

- [ ] 2.3 [qa-engineer] QA's F1 (`review.md`): the hygiene scan blocks any staged path, and in `--tree` mode any committed path, that `.gitignore` ignores (one `git check-ignore --no-index --stdin` call), so a store file linked into `unity/Assets` can't be pushed with `git add -f` (D-089). A test with a forced-added linked file. Checks: `python -m pytest tools/hygiene`, `precommit.py --tree HEAD` (0 findings), `python tools/hygiene/bench.py` under its budget.
- [ ] 2.4 [game-developer] QA's F2: `tools/build_latest.ps1` runs `link.ps1` in its worktree (and `-Unlink` before removing it), `Fpv450.Build.Windows` fails when the farmland scene has missing references, and the smoke test fails if the player log lacks the PSO guard's "PSO Cache Library Init Failure" line on D3D12. Checks: EditMode filtered, a review build, the smoke test, `precommit.py --tree HEAD`.

## 3. Review

- [ ] 3.1 [qa-engineer] Review against the spec; shots of the farmland in noclip beside the friend's project at the same cameras; the checks.
