# Tasks: w6-farmland-look

Order: 1.1, then 1.2, then 2.1. Branch `unity/physics/physics-integration-2--w6-look`, cut from the build line after W1 merged. A fresh worktree needs `git lfs checkout` and `pwsh tools/vendor/link.ps1`.

## 1. The look and the frame rate

- [x] 1.1 [tech-artist] The farmland looks like the friend's through our pipeline. At the friend's 10 saved cameras (the scene's capture views), in noclip with the feed off, ours matches theirs:
  - mean luma within 2 of 255;
  - soft shadows to a useful distance;
  - screen-space ambient occlusion;
  - the distance haze.
  Change only our pipeline assets, renderers and volumes; the friend's scene and the store stay untouched, and the feed stays as ported (D-026). Render the friend's side from a temporary copy of their project without `Library/`, never the store in place, and delete the copy afterwards. Write `design.md`: each setting changed, why, and its measured effect. Write a side-by-side sheet per camera (local only, `reference/_compare/` or the scratchpad). Checks: EditMode filtered, a review build, the smoke test, `precommit.py --tree HEAD`.
  - As built (design.md): `URP-High` takes the friend's HDR grading, 400 m and 4 cascades of soft shadows (High) with their bias, and their SSAO on its renderer; the build keeps the exponential-squared fog variants (the haze). Every camera within 0.63 luma of the friend's (was up to 6.81 off); noclip 11.8 → 15.7–16.5 ms, feed on 18.8 → 27.0 ms; the analog passes byte-identical.
- [x] 1.2 [tech-artist] Frame rate, memory and load on the farmland (PLT-02, PLT-05, PLT-06), by PLT-07.
  - A fixed scripted camera path over the village, a tree belt and open fields, run with the feed on and in noclip at 1080p.
  - Tune grass distance and density, shadows, LODs and culling until the feed view's 1 % low ≥ 60 fps and average ≥ 100 fps, noclip's 1 % low ≥ 60, VRAM ≤ 4.5 GB, RAM ≤ 8 GB, and Explore loads in ≤ 20 s. Detail the PAL feed can't show goes first.
  - Keep 1.1's look: re-check the 10 cameras after tuning.
  - The report goes in `docs/reports/`: average, 1 % low, worst frame, CPU and GPU ms, draw calls, triangles, VRAM, with the power mode and clocks printed. On AC only, on a quiet machine.
  - Checks: as 1.1, plus the benchmark runner.
  - As built (design.md §7, `docs/reports/2026-11-12-farmland-frame-rate.md`): the runner is built (`FarmlandBench`, `tools/bench_farmland.ps1`); the feed's output camera has its own renderer, SSAO runs at half resolution, and the grass is drawn to 100 m while the feed runs. The feed view is 35 % faster (42 → 64 fps on the path), the look kept (within 0.64 luma of the friend's) and the feed's passes byte-identical, On a shared machine with the GPU held at 20 W (power mode Best power efficiency), PLT-02 was missed. The 17–19 ms wait to present that W3's player measured is the GPU-bound frame: the GPU is 99 % busy with the player, and `FrameTimingManager` times only the feed's output camera. It is not a stall of ours (design.md §7.8). The bench now records the wait.
  - **The quiet PLT-07 run** (2026-11-12 11:14, the report's last section): on AC, power mode Best performance, GPU 2415 MHz median at 64.8 W, the same build. Feed 111.5 fps average and 76.5 fps 1 % low; noclip 131.7 and 94.5; VRAM 1017 MB, RAM 1190 MB, Explore in 1.8 s. **PLT-02, PLT-05 and PLT-06 met.** QA (2.1) repeats it; W3's flight player is re-benched once W6 and W3 meet on the line.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec; rerun the PLT-07 benchmark on a quiet machine on AC; look at every side-by-side sheet; the checks.
