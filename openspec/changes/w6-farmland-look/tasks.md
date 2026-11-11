# Tasks: w6-farmland-look

Order: 1.1, then 1.2, then 2.1. Branch `unity/physics/physics-integration-2--w6-look`, cut from the build line after W1 merged. A fresh worktree needs `git lfs checkout` and `pwsh tools/vendor/link.ps1`.

## 1. The look and the frame rate

- [ ] 1.1 [tech-artist] The farmland looks like the friend's through our pipeline. At the friend's 10 saved cameras (the scene's capture views), in noclip with the feed off, ours matches theirs:
  - mean luma within 2 of 255;
  - soft shadows to a useful distance;
  - screen-space ambient occlusion;
  - the distance haze.
  Change only our pipeline assets, renderers and volumes; the friend's scene and the store stay untouched, and the feed stays as ported (D-026). Render the friend's side from a temporary copy of their project without `Library/`, never the store in place, and delete the copy afterwards. Write `design.md`: each setting changed, why, and its measured effect. Write a side-by-side sheet per camera (local only, `reference/_compare/` or the scratchpad). Checks: EditMode filtered, a review build, the smoke test, `precommit.py --tree HEAD`.
- [ ] 1.2 [tech-artist] Frame rate, memory and load on the farmland (PLT-02, PLT-05, PLT-06), by PLT-07.
  - A fixed scripted camera path over the village, a tree belt and open fields, run with the feed on and in noclip at 1080p.
  - Tune grass distance and density, shadows, LODs and culling until the feed view's 1 % low ≥ 60 fps and average ≥ 100 fps, noclip's 1 % low ≥ 60, VRAM ≤ 4.5 GB, RAM ≤ 8 GB, and Explore loads in ≤ 20 s. Detail the PAL feed can't show goes first.
  - Keep 1.1's look: re-check the 10 cameras after tuning.
  - The report goes in `docs/reports/`: average, 1 % low, worst frame, CPU and GPU ms, draw calls, triangles, VRAM, with the power mode and clocks printed. On AC only, on a quiet machine.
  - Checks: as 1.1, plus the benchmark runner.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec; rerun the PLT-07 benchmark on a quiet machine on AC; look at every side-by-side sheet; the checks.
