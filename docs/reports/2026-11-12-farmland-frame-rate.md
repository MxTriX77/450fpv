# The farmland's frame rate (W6, task 1.2)

Tech-artist, `w6-farmland-look` 1.2, branch `unity/physics/physics-integration-2--w6-look`. The benchmark is the PLT-07 runner, but **every number here is from a shared machine** (other agents' Unity, Blender, Python and .NET runs) **with the GPU held at 20 W**, so none is budget evidence. The quiet PLT-07 run is owed (below). The details and every lever's measurement are in the change's `design.md` §7.

## Verdict

- **PLT-02 is not met on this machine as it is set**, through the feed or in noclip.
- **PLT-05 and PLT-06 are met** with a wide margin: VRAM 1.06 GB of 4.5, RAM 1.2 GB of 8, Explore's first frame 1.4 s after the press, of 20.
- **The GPU ran at 20 W of its 65 W.** `nvidia-smi` reads an enforced power limit of 20 W, with Windows' power mode at Best power efficiency on AC. Under the bench the GPU sat at 1035–2700 MHz (1305 MHz median, its maximum 3105), 100 % busy. 1.1's numbers were taken the same way.
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

## What is left, for the orchestrator and the pilot

1. **The PLT-07 run (owed).** It needs a quiet machine on AC with **Windows' power mode at Best performance**: a Windows setting the pilot changes, not an agent. Then run `pwsh tools/bench_farmland.ps1` on a review build. It prints the mode and the GPU's power limit in force. With 3.25× the GPU's power the targets may be met as they stand. That is a guess, not measured.
2. **If it still misses,** the levers measured here, with what each costs:

| Lever | Buys (measured here) | Costs |
|---|---|---|
| soft shadows Low through the feed | feed 1 % low 43 → 50 fps on the path | the village lane's broad tree shadow becomes a hard dapple, visible through the feed |
| the grass to 80 m through the feed | −1.1 ms on the views | the far band smoother still |
| SSAO off through the feed | −2.3 ms on the views | no AO under canopies and at contacts |
| **the feed's render at 1280 samples across** | the feed at 91.6 fps, 1 % low 63.2 fps | changes the ported feed (D-026): 83 % of the fine horizontal structure through the feed |
| the feed's render at 960 across | 111.4 fps, 1 % low 67.7 fps: PLT-02 met even at 20 W | 73 % of the fine structure |
| noclip's grass to 150 m | −1.6 ms on noclip's views | the friend's grass ends 50 m sooner at 1080p (luma unchanged) |
