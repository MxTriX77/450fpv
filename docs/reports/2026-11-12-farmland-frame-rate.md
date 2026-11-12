# The farmland's frame rate (W6, task 1.2)

Tech-artist, `w6-farmland-look` 1.2, branch `unity/physics/physics-integration-2--w6-look`. The benchmark is the PLT-07 runner, but **every number here is from a shared machine** (other agents' Unity, Blender, Python and .NET runs) **with the GPU held at 20 W**, so none is budget evidence. The quiet PLT-07 run is owed (below). The details and every lever's measurement are in the change's `design.md` §7.

## Verdict

- **The quiet PLT-07 run (2026-11-12 11:14) meets PLT-02, PLT-05 and PLT-06** at full power: through the feed 111.5 fps average and 76.5 fps 1 % low; in noclip 131.7 fps and 94.5 fps 1 % low. Its section is below. Nothing was tuned for it: it ran the same review build as the shared-machine runs.
- The rest of this verdict is from the shared machine at 20 W, before that run:
- **PLT-02 was not met on this machine as it was set**, through the feed or in noclip.
- **PLT-05 and PLT-06 are met** with a wide margin: VRAM 1.06 GB of 4.5, RAM 1.2 GB of 8, Explore's first frame 1.4 s after the press, of 20.
- **The GPU ran at 20 W of its 65 W.** `nvidia-smi` reads an enforced power limit of 20 W, with Windows' power mode at Best power efficiency on AC. Under the bench the GPU sat at 1035–2700 MHz (1305 MHz median, its maximum 3105), 100 % busy. 1.1's numbers were taken the same way.
- **The 17–19 ms "wait to present" that W3's player measured is GPU time.** The frame is GPU-bound: the player's own 3D engine on the RTX 4050 is 99 % busy, and no other process uses it. `FrameTimingManager`'s GPU time with the feed on covers only the output camera, so the FPV camera's render shows up as the main thread waiting for the GPU. It is not DWM, the swap chain, a sync in the feed's passes or the sim. The bench now records the wait (below).
- **1.2 made the farmland's feed view 35 % faster** with no change to the feed's passes or render size, and kept noclip's look at the friend's 10 cameras (every view within 0.64 luma of the friend's).

## The numbers (shared machine, GPU at 20 W)

The path: 965 m at 15 m/s, west through the village's lane at 4 m, across the village at 30 m, along the tree belt on field F8's west edge at 5 m, west over the open field F7 at 15 m. 2 s warm-up, 64 s measured, 1920 × 1080 window, vsync off. The 1 % low is the 99th-percentile frame's rate.

Same-run pairs (two passes each):

| | Average | 1 % low | Worst frame | Triangles |
|---|---|---|---|---|
| Feed, before 1.2 | 42.8 / 42.0 fps | 32.1 / 31.1 fps | 33.9 / 34.8 ms | 23.4 M |
| **Feed, 1.2** | **64.3 / 64.1 fps** | **43.3 / 42.5 fps** | 24.3 / 25.2 ms | 7.6 M |
| Noclip, before 1.2 | 70.3 / 69.9 fps | 50.7 / 50.4 fps | 21.4 / 21.6 ms | 19.8 M |
| **Noclip, 1.2** | **73.1 / 72.4 fps** | **52.3 / 52.1 fps** | 21.6 / 21.3 ms | 19.8 M |
| PLT-02 | feed ≥ 100 fps | ≥ 60 fps | | |

The committed runner's single pass on 1.2 (`pwsh tools/bench_farmland.ps1`, a busier hour):

| Run | Average | 1 % low | Worst frame | CPU main / render | GPU | Draw calls (max) | Triangles (max) |
|---|---|---|---|---|---|---|---|
| feed | 59.0 fps (17.0 ms) | 36.2 fps (27.6 ms) | 44.8 ms | 3.0 / 0.3 ms | 17.0 ms (frame-bound: 100 % busy) | 1241 (2019) | 7.8 M (13.1 M) |
| noclip | 69.4 fps (14.4 ms) | 46.3 fps (21.6 ms) | 37.3 ms | 3.1 / 1.5 ms | 14.4 ms (FrameTimingManager) | 1421 (2360) | 19.7 M (30.4 M) |

- **Memory:** peak dedicated VRAM 1060 MB; peak RAM 1194 MB working set, 2247 MB private.
- **Load:** Explore's first frame 1.39 s after the press.
- **Conditions:** AC; the Balanced plan; the power mode Best power efficiency; the GPU's power limit 20 W in force of 65 W, P3–P5, 1305 MHz median, 19.98 W, 57 °C max; the CPU at 2982 MHz effective median.
- **The GPU time:** with the feed on, `FrameTimingManager` sees only the feed's output camera. The runs are GPU-bound (CPU main thread ≤ 3.4 ms, GPU 100 % busy), so the GPU time is the frame time.

## What changed

| Change | Feed | Noclip | The look |
|---|---|---|---|
| The feed's output camera gets a renderer with the feed alone, no SSAO on its empty frame | −0.4 ms | | none: the analog passes' stills byte-identical |
| SSAO at half resolution | −1.4 ms | −0.55 ms | none measurable: luma 0.03 through the feed; noclip's 10 views as 1.1 |
| The grass to 100 m while the feed runs (the friend's 200 m in noclip) | −6.5 ms | | through the feed: luma 0.22 on average; the far band a shade smoother (fine structure −4 %, −12 % on elevated views) |

**The feed's passes, constants and render size are unchanged (D-026).**

## The wait to present (1.2, continued)

W3's player timed each farmland frame through the feed at 22–24 ms. `FrameTimingManager` gave the GPU 3.7 ms and the main thread 4.5–5.2 ms, and the remaining 17–19 ms was "waiting to present". The details are in `design.md` §7.8. Same machine and conditions as above (AC, Best power efficiency, the GPU limited to 20 W, a 1920 × 1080 window, vsync off):

| Run | Render | Frame | Main thread | Wait to present | GPU (FrameTimingManager) | Player's 3D engine busy |
|---|---|---|---|---|---|---|
| W3's player, the hills cruise | 2889 × 1625 | 23.4 ms | 5.0 ms | 18.4 ms | 3.7 ms | 99.2 % |
| W6's path, the feed | 2754 × 1549 | 15.9 ms | 2.9 ms | 13.0 ms | 1.2 ms | 99.5 % |
| W6's path, the feed's scene at a quarter of the pixels | 1377 × 775 | 7.3 ms | 2.9 ms | 4.4 ms | 1.0 ms | 99.2 % |
| W6's path, borderless full screen | 2754 × 1549 | 16.1 ms | 3.0 ms | 13.0 ms | 1.3 ms | 99.5 % |
| W6's path, noclip | 1920 × 1080 | 13.5 ms | 2.8 ms | 10.6 ms | 13.4 ms | 99.5 % |

- **The cause.**
  - The frame is GPU-bound, and the main thread waits in Present for the GPU. In noclip it waits too (10.6 ms).
  - With the feed on, `FrameTimingManager` times only the output camera. Rendering the FPV scene at a quarter of the pixels cut the frame and the wait by the same 8.5 ms, while its GPU time stayed at about 1 ms.
  - So the FPV camera's render is the hidden GPU time.
- **Ruled out:**
  - DWM: full screen splits the same as the window.
  - The sim: W6's path flies no sim and splits the same.
  - A sync or readback in the feed's passes, the swap chain, `targetFrameRate` or `maxQueuedFrames`: the GPU is never idle, and our code sets none of these.
- **The machine's part.** NVIDIA's software power cap is active in 99–100 % of the busy samples. The GPU draws 19.98 W of its 20 W at about 1.3 GHz of 3.1 GHz, which makes the GPU-bound frame long.
- **Our part.** The FPV camera's content at the feed's render size: the levers below.
- **The fix.** Nothing of ours stalls, so the render is unchanged. The bench and `tools/bench_farmland.ps1` now print the main thread's wait to present beside the GPU time. That way a feed run's split adds up (main + wait = frame) and doesn't read as a CPU or present problem.

## What is left, for the orchestrator and the pilot

1. **The PLT-07 run: done**, see "The quiet PLT-07 run" below. The rest of this item is as it stood before it.
   **The PLT-07 run (was owed).** It needs a quiet machine on AC with **Windows' power mode at Best performance**: a Windows setting the pilot changes, not an agent. Then run `pwsh tools/bench_farmland.ps1` on a review build. It prints the mode and the GPU's power limit in force. With 3.25× the GPU's power the targets may be met as they stand. That is a guess, not measured.
2. **If it still misses,** the levers measured here, with what each costs:

| Lever | Buys (measured here) | Costs |
|---|---|---|
| soft shadows Low through the feed | feed 1 % low 43 → 50 fps on the path | the village lane's broad tree shadow becomes a hard dapple, visible through the feed |
| the grass to 80 m through the feed | −1.1 ms on the views | the far band smoother still |
| SSAO off through the feed | −2.3 ms on the views | no AO under canopies and at contacts |
| **the feed's render at 1280 samples across** | the feed at 91.6 fps, 1 % low 63.2 fps | changes the ported feed (D-026): 83 % of the fine horizontal structure through the feed |
| the feed's render at 960 across | 111.4 fps, 1 % low 67.7 fps: PLT-02 met even at 20 W | 73 % of the fine structure |
| noclip's grass to 150 m | −1.6 ms on noclip's views | the friend's grass ends 50 m sooner at 1080p (luma unchanged) |

## The quiet PLT-07 run (2026-11-12 11:14)

The orchestrator ran it with the agents stopped and no Unity, .NET or Blender run on the machine (the runner lists only three PowerShell shells and Unity Hub's service). It used the same review build as above (`unity/Builds/Windows-Review`, built 08:06 from ad2fbc7c's code), with `pwsh -File tools/bench_farmland.ps1`.

- **Power:** AC; plan Balanced; Windows' power mode Best performance (ded574b5), set by the laptop vendor's own Performance mode, which overrides Windows' slider. The GPU's enforced power limit was 45 W at the start; the GPU drew 64.8 W under the bench (Dynamic Boost).
- **Clocks:** the GPU's graphics clock 2415 MHz median (585–2685), memory 8201 MHz, 100 % busy, 70 °C at most. The CPU 3051 MHz effective median (2498–4279).
- **The player:** 1920×1080 windowed, vsync 0, quality High, on a 965 m path. Explore drew its first frame 1.8 s after the press.
- **Memory:** peak dedicated VRAM 1017 MB; RAM 1190 MB working set, 2440 MB private.

| Run | Frames | Average | 1 % low | Worst frame | CPU main / render | Wait to present | GPU (FrameTimingManager) | Draw calls (max) | Triangles (max) | Render |
|---|---|---|---|---|---|---|---|---|---|---|
| feed | 7173 in 64.4 s | 111.5 fps (8.97 ms) | 76.5 fps (13.07 ms) | 15.39 ms | 2.91 / 0.29 ms | 6.05 ms | 0.68 ms (output camera only) | 1227 (2009) | 7.73 M (13.1 M) | 2754×1549 |
| noclip | 8476 in 64.3 s | 131.7 fps (7.59 ms) | 94.5 fps (10.58 ms) | 11.83 ms | 2.51 / 1.19 ms | 5.07 ms | 7.49 ms | 1426 (2358) | 19.8 M (30.1 M) | 1920×1080 |

- **PLT-02:** met through the feed (1 % low ≥ 60 and average ≥ 100 fps) and in noclip (1 % low ≥ 60 fps). **PLT-05 and PLT-06:** met.
- **The frame is still GPU-bound** (100 % busy). With the GPU's clock about 1.85× the 20 W runs', the feed's frame went from 15.9 to 9.0 ms.
- **The repeat run stalled.** A second run straight after drew one frame and then sat with the GPU idle: the player's window had most likely lost focus while the pilot used the machine, so the player paused. It was stopped and left no result. QA's 2.1 repeats the run.
- **Not covered:** W3's flight player, with its near-terrain rule (D-099, pixel error 0.05 within 30 m), grass to 200 m and SSAO on the output camera. Its own `-terrainab` and this runner must be re-run once W6 and W3 meet on the line.
- The raw report and logs are local, in the orchestrator's scratchpad (`bench-perfarmbench-20261112-111411.*`).

