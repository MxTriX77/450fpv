# Design: w6-farmland-look

Task 1.1's part: the settings that make our pipeline render the farmland like the friend's project at their 10 saved cameras, why each one, and what each one does to the picture and to the frame. Task 1.2's part, the frame rate and memory, is §7. Numbers are labelled **measured** or **derived**; how 1.1's were measured is in §6, 1.2's in §7.1.

## 1. The settings changed
Each one is the friend's `PC_RPAsset` / `PC_Renderer` value (W1 design §4), on our `URP-High` only. Medium and Low keep theirs; only the fog variants (a project setting) reach them.

| File | Setting | Was | Now | Why |
|---|---|---|---|---|
| `Settings/URP-High.asset` | colour grading | LDR | **HDR** | the farmland's volume grades with ACES, contrast +10 and saturation −4. In LDR mode URP tonemaps first and grades the clamped result; in HDR it grades in log space, then tonemaps. That ordering is most of W1's "4 luma darker": 3.2–3.4 of the ground views' 3.5–4.5 (§3) |
| | shadow distance, cascades | 50 m, 1 | **400 m, 4** | the friend's trees cast shadows to the horizon; ours stopped at 50 m, so far belts and hedges stood sunlit |
| | cascade splits, border | 0.067 / 0.2 / 0.467, 0.2 | **0.123 / 0.293 / 0.536, 0.108** | the friend's; the near cascade reaches 49 m |
| | depth bias, normal bias | 1, 1 | **0.1, 0.5** | the friend's sun takes the asset's bias. With 1 / 1 the village lane, under a tree, reads 2.8 luma darker than with theirs |
| | soft shadows, quality | off, Medium | **on, High** | the friend's soft tree shadows. Our sun already asked for soft (the scene's light, and `LookSetup` on the test range); the asset refused it |
| `Settings/URP-High-Renderer.asset` | renderer features | the feed | the feed, **screen-space ambient occlusion** | the friend's feature with its settings: depth-normals source, blue noise, medium samples, intensity 0.4, radius 0.3, direct-light strength 0.25, falloff 100 m, full resolution, not after opaque |
| `ProjectSettings/GraphicsSettings.asset` | fog variants kept | exponential | exponential, **exponential squared** | **the haze.** The farmland's fog is exponential squared (0.0006 /m, pale blue). Since `d095404d` our builds strip fog variants by hand and kept only the test range's exponential, so the player had no shader for the farmland's fog and drew none. The editor never strips, which is why the friend's render had it. The one change outside `Settings/`: it is ours, not the friend's scene or the store |
| `Settings/UniversalRenderPipelineGlobalSettings.asset` | runtime resources | | the SSAO's two resource sets | written by URP on the build, because a renderer now uses SSAO |

URP's build also rewrote `URP-High`'s shader prefiltering fields (the SSAO keywords kept); they are committed as it wrote them, so a later build leaves the tree clean.

**Not changed, and why:**
- **Depth and opaque textures** (theirs on): nothing on the farmland samples them (no `SceneDepth`, `SceneColor` or `_CameraDepthTexture` in the friend's shaders or the packs). SSAO asks for its own depth and normals, and URP's motion blur asks for depth itself. On, they are a copy pass each for no pixel.
- **Additional-light shadows, light layers, reflection-probe blending and box projection** (theirs on): the farmland has one light, the sun, and no reflection probe.
- **Shadow-map resolution** stays 2048, as theirs. With 4 cascades each gets 1024², so the near cascade has half the texel density the single 50 m cascade had. 4096 measured +0.5 ms and moved the views' luma by at most 1.2 (§4), so it is 1.2's to trade, not needed for the match.
- **The friend's pipeline volume profile** (`SampleSceneProfile`): it holds bloom, vignette, tonemapping and an inactive motion blur, and the farmland's own volume overrides every one of them. Our default profile is neutral. No volume changes.
- **`useLegacyDetailDistribution`** (theirs on, ours off, a quality setting): the friend's project rendered with it off moved the 10 views by 0.09 luma on average, 0.34 at worst (measured). Not changed.

## 2. The match at the friend's 10 cameras
Our player's Explore, noclip, the feed off, against the friend's own render (measured). Luma is BT.601 on the 8-bit sRGB frame, and saturation the mean HSV S, as QA measured in W1 (the friend's column reproduces W1's to 0.1).

| View | Friend's luma | Ours before | Ours now | Friend's sat. | Before | Now |
|---|---|---|---|---|---|---|
| view_junction | 116.1 | 112.5 (−3.6) | 115.8 (−0.3) | 106.9 | 109.4 | 107.1 |
| view_field_furrows | 117.9 | 113.9 (−4.0) | 117.4 (−0.5) | 109.0 | 111.2 | 109.5 |
| view_field_trees | 115.8 | 111.7 (−4.1) | 115.2 (−0.6) | 101.4 | 104.6 | 102.3 |
| view_dry_line | 112.3 | 108.8 (−3.5) | 111.8 (−0.5) | 109.2 | 111.0 | 109.7 |
| view_village_ruins | 87.9 | 84.4 (−3.5) | 87.6 (−0.3) | 116.5 | 117.8 | 117.0 |
| view_village_lane | 71.2 | 66.7 (−4.5) | 70.5 (−0.6) | 109.5 | 104.6 | 108.9 |
| view_village_above | 117.6 | 112.4 (−5.2) | 117.1 (−0.5) | 100.9 | 110.0 | 101.6 |
| view_east | 99.9 | 95.7 (−4.2) | 99.6 (−0.3) | 124.0 | 124.2 | 124.5 |
| view_hills | 112.3 | 108.4 (−3.9) | 111.8 (−0.5) | 114.5 | 116.2 | 114.9 |
| view_elevated_centre | 122.7 | 115.9 (−6.8) | 122.2 (−0.5) | 91.2 | 101.4 | 91.8 |
| **mean** | 107.4 | 103.0 (4.34 off, worst 6.81) | 106.9 (**0.47 off, worst 0.63**) | 108.3 | 111.0 | 108.8 |

- **Every view is within 0.63 luma** of the friend's (the spec asks 2). A second pass in the same run gave 0.45 mean, 0.64 worst. What's left is a steady −0.5, from no setting tested here (the editor's D3D11 against the player's D3D12, or the wind's phase, are guesses).
- **Seen on the sheets** (friend's | before | now, feed off | now, feed on):
  - the village lane's tree shadow is soft and broad as in the friend's, where before it was a hard dapple;
  - the elevated views' far fields and horizon fade into the same pale haze, where before the horizon stayed crisp and saturated (saturation 101.4 against 91.2; now 91.8);
  - the far belts and hedges carry their own shadows to the horizon;
  - SSAO darkens the canopies' interiors and the ground under them as in the friend's.

## 3. What each setting does (measured)
One player run, each setting switched back to its old value alone at runtime, the rest as now. Mean |Δluma| over the 10 views against "now", and the worst view.

| Switched back | Mean | Worst | Where |
|---|---|---|---|
| HDR → LDR grading | 3.07 | 3.41 | everywhere: the exposure and the saturation |
| fog variant stripped (fog off) | 0.95 | 3.45 | the elevated views' haze (village_above −3.1, elevated_centre −3.4); −0.6 to +0.3 on the ground |
| 400 m × 4 → 50 m × 1 shadows | 0.50 | 1.76 | the distant belts: village_above +1.8, east −0.7 |
| bias 0.1 / 0.5 → 1 / 1 | 0.39 | 2.77 | the village lane's canopy shadow |
| SSAO off | 0.19 | 0.45 | local: canopies and contacts, not exposure |
| soft → hard shadows | 0.16 | 0.70 | local: the shadow edges, not exposure |
| *for 1.2:* 200 m shadows | 0.27 | 1.04 | the east view's far belt |
| *for 1.2:* 150 m shadows | 0.40 | 1.46 | the village lane (−1.5) and the east view's far belt (−1.2) |
| *for 1.2:* 4096 shadow map | 0.35 | 1.22 | the far belts |

The runtime switches are trustworthy: all six switched back at once gave the separate "before" build's frames within 0.4 luma at every view (mean 4.34 against the friend's, as the build's own).

## 4. What it costs (measured, indicative)
Frame time at 1920 × 1080, mean over the 10 views, 90 frames each after a settle. GPU-bound throughout (GPU time = frame time within 0.02 ms with the feed off). **Not a PLT-07 benchmark:** AC power and the Balanced plan, but on a shared machine with other agents' Unity runs; the same configuration measured twice in one run differed by 0.8 ms.

| | Noclip, feed off | Feed on |
|---|---|---|
| Before (the old build) | 11.8 ms | 18.8 ms |
| **Now** | **15.7–16.5 ms** | **27.0 ms** |
| of which soft shadows, High | ≈ 2.0 ms (1.7–2.4) | ≈ 2.4 ms |
| of which SSAO | ≈ 2.2 ms (1.8–2.6) | ≈ 4.5 ms |
| of which 400 m × 4 over 50 m × 1 | ≈ 0.5 ms (at the noise) | ≈ 0.5 ms |
| HDR grading, the fog variant, the bias | not measurable (< the noise) | |
| 200 m or 150 m instead of 400 m | no saving measurable | |
| 4096 shadow map instead of 2048 | +0.5 ms | |

- **With the feed on, SSAO and soft shadows cost about twice as much:** the FPV camera renders 2754 × 1549 (4.3 MP against 2.1). The feed's output camera also runs the SSAO, on an empty frame at the screen's size, since every camera on the renderer gets the feature (derived, not measured apart). It draws nothing with it: the analog passes' output is byte-identical (§5).
- **For 1.2:** the old pipeline already missed PLT-02 with the feed on (18.8 ms against 10). The trades this table offers, cheapest look loss first: SSAO off for the output camera (a second renderer for it), SSAO at half resolution, soft-shadow quality Medium or Low, then SSAO off with the feed on. Shadow distance buys nothing here.

## 5. The feed stays as ported (D-026)
- **The analog passes alone:** `FeedCapture.Stills` on one fixed input frame (`-feedInput`, fields 500–502, seed 1), before and after: **byte-identical** (0 pixels differ in each of the 3). No feed shader, feature, setting or rig changed.
- **The feed's test scene rendered** (`FeedCapture.Stills` on `FeedTest.unity`, fields 500–502): 3.3 % of pixels differ, mean 0.9 levels, luma −0.8. That is the scene's render (the belt's soft shadows and AO), not the feed's processing.
- **The test range through the feed**, in the player, the feed pinned at field 500, each number-key view under each light preset (24 frames):

| Views | Mean abs. difference (levels, mean of R, G, B) | Luma change |
|---|---|---|
| map from 50 m, night and overcast | 0 (identical) | 0 |
| map from 50 m, day and low sun | 0.3–0.6 | −0.3 to −0.5 |
| yard, start track, start field, tree belt edge: night and overcast | 0.0–1.7 | −0.0 to −1.6 |
| the same, day | 0.8–1.9 | −0.5 to −1.5 |
| the same, low sun | 2.5–6.1 | −1.3 to −5.2 (start field) |
| trench | 2.6–8.9 | −2.3 to −6.7 (overcast) |

  What changed, seen: the tree belt 200 m out across the start field now carries its own shadows in the low sun and reads as a dark band (VIS-04's rule), where before its crowns stood lit past the 50 m shadow; the trench and the ground around it are darker under the overcast sky (SSAO). So **the feed's passes are unchanged, and the test range's picture changes where the scene's light does.** The test range leaves the player for W3 (`docs/final-world-plan.md`, W1's row), and stays for headless tests and benches.
- **Also changed by the same settings** (derived): the gallery build, and our asset review sheets, which render through `URP-High` (`ReviewCapture` caps its shadow distance at the game's, now 400 m). New sheets won't be byte-identical to older ones.

## 6. How it was measured
- **The friend's side:** a copy of their project without `Library/`, `Temp/` or `Logs/` (`robocopy /E /XJ`), opened with our Unity 6000.3.26f1 in batchmode (the editor on D3D11), its quality "Desktop Unlocked" with `PC_RPAsset`. A scratch editor script, in the copy only, opened `FarmLand.unity` and called the friend's own `FarmLandCapture.SavedView` and `Capture` at 1920 × 1080 (60°, post on, 30 warm-up renders). The copy was deleted afterwards; the store's 1,082 files kept their sizes and write times.
- **Our side:** a scratch hook in two review builds, never committed. It opens Explore through `AppFlow` as the menu does, switches the feed off (as V) and the noclip's mouse off, and puts the scene camera at each saved pose (the friend's positions to 0.01 m, e.g. `view_junction` at (572.00, 40.01, 570.00), (2, 347, 0)); then 45 frames and 0.75 s to settle the grass and the motion blur, 90 frames timed with `FrameTimingManager` (frame-timing stats switched on in the scratch builds only), and the screen saved. Settings were switched at runtime through the pipeline asset, the sun and the SSAO feature. With `-capture`, the same run then opened the test range on its own, pinned the feed's field and let `TestRangeBoot` save its views.
- **Both sides' ambient probe** is the same to four decimals (measured), so the remaining offset isn't the sky light.
- **Conditions:** AC power, Balanced plan, RTX 4050 Laptop GPU on D3D12, a 1920 × 1080 window, vsync off. The pilot's prefs (`LocalLow\450fpv`, `%LOCALAPPDATA%\450fpv`, `HKCU\Software\450fpv`) were saved before the player runs and put back byte for byte after them.
- **The sheets** (friend's | before | now, feed off | now, feed on, one per camera) and every frame stay local, in the session's scratchpad: they hold third-party renders.

## 7. The frame rate and memory (task 1.2)
Every number here is from a **shared machine** (other agents' Unity, Blender and .NET runs) with the GPU **held at 20 W** (§7.6), so none is PLT-07 budget evidence; each A/B difference comes from one run. The PLT-07 run is owed.

### 7.1 The benchmark runner
- **`FarmlandBench`** (`Scripts/App/FarmlandBench.cs`): a review build started with `-farmbench <file>` presses Explore as the menu does (`AppFlow`), then flies the camera along a fixed path, once per run of `-benchruns` (default `feed,noclip`): through the feed, then in noclip with the feed off. Each run holds the path's first pose for the 2 s warm-up, then measures the whole path: average, 1 % low (the 99th-percentile frame's rate, as the test range's bench), worst frame, the CPU's main and render thread and `FrameTimingManager`'s GPU time, draw calls, batches, set-pass calls and triangles (`ProfilerRecorder`, the release player's counters), and Unity's system memory. It writes JSON and quits.
- **The path** (965 m at 15 m/s, 64 s measured): west through the village's lane between the five ruins at 4 m; up past the village's west edge and across the village at 30 m, looking down on the ruins; down to the tree belt on field F8's west edge and along it at 5 m, 12 m off it; west over the open field F7 at 15 m. Heights are over the terrain, sampled along the path once, so a frame allocates nothing. `FarmlandBenchTests` holds it over the map, through the village's circle, and at least 30 s long.
- **`tools/bench_farmland.ps1`** runs it in a 1920 × 1080 window with vsync off and adds what only the OS sees: AC power, the power plan and Windows' power mode, the GPU's power limit in force, the GPU's clocks, power, load and temperature while it runs (`nvidia-smi`), the CPU's effective clock, the process's peak dedicated VRAM (`\GPU Process Memory`) and peak RAM, and the other heavy processes. The window prefs the player writes on quit (`HKCU\Software\450fpv`) are saved first and put back byte for byte. It prints a table and a PLT-02/05/06 verdict per run.
- **The GPU time with the feed on:** `FrameTimingManager` covers only the output camera (1.6 ms, measured), not the FPV camera's render into its own target, and with the grass at 100 m it reports 0. The runs are GPU-bound (the CPU's main thread 2.6–3.4 ms, its render thread ≤ 1.3 ms, the GPU 100 % busy), so the GPU time is the frame time. **Frame timing stats** are now on in `ProjectSettings` (they were off), for the CPU times.

### 7.2 Where the frame goes (measured)
The friend's 10 views, 60 frames each after a settle, with the feed on (the farmland's worst case: the FPV camera renders 2754 × 1549, 2.06× a 1080p frame). Each lever alone against the nearest baseline in the same run; the baseline drifted 25.6 → 27.2 ms over a run, and repeats came 0.1–1.5 ms apart.

| Lever, with the feed on | Δ frame | What it does to the look |
|---|---|---|
| **the terrain's grass off** (attribution) | **−13.5 ms** of 25.6–27 | the farmland's 2.84 M grass instances: 23.4 M triangles a frame through the feed, 7.6 M with the grass to 100 m |
| the grass to 150 / 120 / **100** / 80 m (the friend's 200) | −2.6 / −4.2 / **−5.3** / −6.4 ms | through the feed at 100 m, luma within 0.31 and the fine horizontal structure 4 % less (11–12 % on the two elevated views); 80 m, luma within 0.56 (§7.4) |
| the terrain's heightmap off (attribution) | −7 to −8 ms | the 8-layer splat at 4.3 MP |
| SSAO off | −3.7 to −4.0 ms | the AO goes |
| **SSAO at half resolution** | **−1.3 to −1.5 ms** (noclip −0.4) | none measurable: luma 0.03 through the feed, the 10 noclip views as before (§7.4) |
| soft shadows Low / hard / the sun's shadows off | −1.7 to −2.4 / −2.0 to −3.0 / −2.6 ms | Low: the village lane's broad tree shadow becomes a hard dapple, plain through the feed (luma −0.6 there) |
| **the output camera without the SSAO** (its own renderer) | **−0.3 to −0.46 ms** | none: the analog passes' stills byte-identical (§7.5) |
| the basemap distance 300 / 150 / 80 m (1500) | −0.4 / −0.8 / −1.0 ms | at the noise |
| post-processing off, motion blur off, LOD bias 0.5, pixel error 20, 2 cascades, trees and bushes off, the far ring off | within ±1 ms (the noise) | |
| detail density 0.5 | 0 | the runtime change did nothing measurable |
| SSAO from depth (not depth and normals) | **+8.4 ms** | a depth prepass that draws the grass |
| **depth priming** (Auto or Forced) | −6.8 ms, **rejected** | the friend's grass and tree shaders have a `DepthOnly` pass but no `DepthNormals`, so SSAO's prepass leaves them out and the primed opaque pass culls them: luma +6.4 on average, +11.9 at worst |

So the farmland's frame through the feed is mostly its grass and its terrain at the feed's render size, then SSAO and the soft shadows' filter.

### 7.3 What 1.2 changed
| File | Setting | Was | Now | Δ, on the path (§7.6) |
|---|---|---|---|---|
| `Settings/URP-High-FeedOutput-Renderer.asset` (new), `URP-High.asset`'s renderer list, `Scripts/Feed/FpvCamera.cs` | the feed's output camera's renderer | High's scene renderer, SSAO included | `FpvCamera` gives the output camera the pipeline's renderer whose only feature is the feed: on High the new one, on Medium and Low their own (unchanged) | feed −0.4 ms |
| `Settings/URP-High-Renderer.asset` | SSAO resolution | full | **half** (1377 × 775 through the feed, still more than the field's 720 × 287) | feed −1.4 ms, noclip −0.55 ms |
| `Scripts/App/FarmlandFeedDetail.cs` (new; `FarmlandExplore` adds it) | the farmland's grass distance **while the feed runs** | 200 m | **100 m**; the friend's 200 m back the moment the feed goes off (V). Runtime only: it sets the friend's `SurroundGrassCulling` distance and the map tile's, and the scene and the store keep theirs | feed −6.5 ms |
| `ProjectSettings/ProjectSettings.asset` | frame timing stats | off | on | the bench's CPU times |
| `Scripts/Feed/Editor/FeedSetup.cs` | the renderers it keeps the feed feature on | three | four (the output renderer) | |

**Not changed, and why:**
- **Soft shadows Low through the feed** (−1.5 ms on the path, the 1 % low 43 → 50 fps): measured and looked at. The village lane's broad, soft tree shadow (1.1's match to the friend's) turns back into a hard dapple, and the feed shows it. It is the pilot's tradeoff (§7.7), not detail the feed can't show.
- **The feed's render size** (D-026, p4-feed 1.2): the analog passes still read the scene at 1920 samples across. §7.7 has what a smaller read buys.
- **Noclip's grass** stays the friend's 200 m: 150 m measured luma 0.46 off the friend's (the same as now) and −1.6 ms on the views, but it is the friend's world seen at 1080p, so it is listed for the pilot (§7.7).

### 7.4 The look kept (measured)
- **Noclip, the friend's 10 cameras** (SSAO at half resolution; noclip's grass and shadows as 1.1): every view within **0.64** luma of the friend's, 0.44 on average (1.1: 0.47, worst 0.63); saturation 108.8 against the friend's 108.3, as 1.1. Soft shadows, AO and the haze are all on the sheets (friend's | 1.1 noclip | 1.2 noclip | 1.1 feed | 1.2 feed, local in the scratchpad).

| View | Friend's | 1.1 | 1.2 |
|---|---|---|---|
| view_junction | 116.1 | 115.8 | 115.8 (−0.3) |
| view_field_furrows | 117.9 | 117.4 | 117.4 (−0.5) |
| view_field_trees | 115.8 | 115.2 | 115.2 (−0.6) |
| view_dry_line | 112.3 | 111.8 | 111.8 (−0.5) |
| view_village_ruins | 87.9 | 87.6 | 87.4 (−0.5) |
| view_village_lane | 71.2 | 70.5 | 70.8 (−0.4) |
| view_village_above | 117.6 | 117.1 | 117.1 (−0.5) |
| view_east | 99.9 | 99.6 | 99.8 (−0.1) |
| view_hills | 112.3 | 111.8 | 111.8 (−0.5) |
| view_elevated_centre | 122.7 | 122.2 | 122.2 (−0.5) |

- **Through the feed**, the field pinned at 500 so the analog noise is the same in every shot: 1.2 against 1.1's settings, luma **0.22** on average and 0.45 at worst (east −0.45, village_above +0.40: the far fields a touch lighter where the terrain shows instead of grass). The mean absolute difference, 4.1 levels, sits at the floor the grass's wind leaves between any two shots (3.4–4.6). The fine horizontal structure (the mean |ΔL| along the lines over the picture's middle) is 4 % less on average, 11–12 % on the two elevated views, where the most field lies past 100 m: at the field's 1:1 the band past 100 m reads the same, and side by side it is a shade smoother.

### 7.5 The feed stays as ported (D-026)
`FeedCapture.Stills` on the fixed input frame (`-feedInput`, fields 500–502, seed 1) with the output camera on its own renderer: **byte-identical** to 1.1's stills (the same SHA-256 for all three). No feed shader, pass, setting or render size changed.

### 7.6 The numbers (shared machine, the GPU at 20 W)
The path, the same run for each pair, two passes each (run 5 for the feed, run 4 for noclip):

| | Average | 1 % low | Worst frame | Triangles |
|---|---|---|---|---|
| Feed, 1.1's settings | 42.8 / 42.0 fps (23.4 / 23.8 ms) | 32.1 / 31.1 fps (31.2 / 32.2 ms) | 33.9 / 34.8 ms | 23.4 M |
| **Feed, 1.2** | **64.3 / 64.1 fps (15.6 / 15.6 ms)** | **43.3 / 42.5 fps (23.1 / 23.6 ms)** | 24.3 / 25.2 ms | 7.6 M |
| Noclip, 1.1's settings | 70.3 / 69.9 fps (14.2 / 14.3 ms) | 50.7 / 50.4 fps (19.7 / 19.8 ms) | 21.4 / 21.6 ms | 19.8 M |
| **Noclip, 1.2** | **73.1 / 72.4 fps (13.7 / 13.8 ms)** | **52.3 / 52.1 fps (19.1 / 19.2 ms)** | 21.6 / 21.3 ms | 19.8 M |
| PLT-02 | feed ≥ 100 fps | ≥ 60 fps, feed and noclip | | |

The same build through the committed runner (`pwsh tools/bench_farmland.ps1`: the feed first, then noclip, one pass each; the machine busier than in runs 4 and 5, with another agent's Python and .NET runs), and the runner on the build before 1.2's changes, earlier the same night:

| Run | Average | 1 % low | Worst frame | CPU main / render | Draw calls (max) | Triangles (max) | Render |
|---|---|---|---|---|---|---|---|
| feed, before 1.2 | 42.5 fps (23.5 ms) | 31.0 fps (32.3 ms) | 35.4 ms | 3.3 / 0.6 ms | 1650 (2542) | 23.4 M (35.4 M) | 2754 × 1549 |
| **feed, 1.2** | **59.0 fps (17.0 ms)** | **36.2 fps (27.6 ms)** | 44.8 ms | 3.0 / 0.3 ms | 1241 (2019) | 7.8 M (13.1 M) | 2754 × 1549 |
| noclip, before 1.2 | 70.2 fps (14.2 ms) | 51.4 fps (19.5 ms) | 20.7 ms | 2.5 / 1.2 ms | 1428 (2365) | 19.8 M (30.0 M) | 1920 × 1080 |
| **noclip, 1.2** | **69.4 fps (14.4 ms)** | **46.3 fps (21.6 ms)** | 37.3 ms | 3.1 / 1.5 ms | 1421 (2360) | 19.7 M (30.4 M) | 1920 × 1080 |

The runner's single passes swing with the machine's load (noclip's 1 % low 46–52 fps between hours, the same build); the same-run pairs above are the comparison. **Memory and load (1.2):** Explore's first frame 1.39 s after the press (PLT-06: 20 s); peak dedicated VRAM **1060 MB** (PLT-05: 4.5 GB; 1158 MB before 1.2); peak RAM **1194 MB** working set, 2247 MB private (8 GB). The GPU at P3–P5, 1305 MHz median (1035–2700), 19.98 W, 100 % busy; the CPU at 2982 MHz effective median; AC, the Balanced plan, the mode Best power efficiency, the GPU's power limit 20 W in force of 65 W.

**PLT-02 is not met on this machine as it is set.** The feed is 35 % faster and noclip 4 %, but both miss: in the same-run passes the feed's average by 36 fps and its 1 % low by 17, noclip's 1 % low by 8; in the runner's single pass, on a busier machine, by 41, 24 and 14. PLT-05 and PLT-06 are met with a wide margin.

**The GPU was held at 20 W of its 65 W.** `nvidia-smi` reads an enforced power limit of 20 W (65 W the maximum) with Windows' power mode at **Best power efficiency** on AC (the plan is Balanced; 1.1's "Balanced plan" was the plan, not the mode). Under the bench the GPU sat at P3–P5, 1065–2685 MHz (1275 MHz median), 20 W, 100 % busy; its maximum is 3105 MHz. Whether the mode alone sets the limit isn't verified (the laptop's own tools may too). The power mode is a Windows setting the pilot changes, not an agent. **The PLT-07 run is owed:** a quiet machine on AC with the power mode at Best performance, `pwsh tools/bench_farmland.ps1`, which prints the mode and the limit in force.

### 7.7 What is left to trade (for the orchestrator and the pilot)
If the PLT-07 run at full power still misses, these are the levers, each measured on this machine, with what each costs:

| Lever | Buys | Costs |
|---|---|---|
| the power mode at Best performance (the GPU's 65 W) | not measured: a Windows setting no agent may change | nothing in the look |
| soft shadows Low through the feed | feed −1.5 ms on the path, its 1 % low 43 → 50 fps | the village lane's broad tree shadow becomes a hard dapple through the feed (luma −0.6 there) |
| the grass to 80 m through the feed | feed −1.1 ms more on the views | the far band a little smoother still; luma within 0.56 |
| SSAO off through the feed | feed −2.3 ms more on the views | the AO under canopies and at contacts goes (1.1: 0.19 luma on average, 0.45 at worst) |
| the feed's render at 1280 samples across (1836 × 1033; `-feedwidth 1280`) | the path through the feed at 91.6 fps (10.9 ms), 1 % low 63.2 fps, against 59.0 fps at the full read in the same hour | changes the feed (D-026, p4-feed 1.2): the fine horizontal structure through the feed is 83 % of the full read's at the farmland's 10 views (76–90 %), luma 0.21; p4-feed's still A/B against Godot lost 19–54 % at 1600–960 across |
| the feed's render at 960 across (1377 × 775) | 111.4 fps (9.0 ms), 1 % low 67.7 fps: PLT-02 met even at 20 W | the same, more: 73 % of the fine structure (61–82 %), luma 0.39 |
| noclip's grass to 150 m | noclip −1.6 ms on the views | luma 0.46 off the friend's (the same as now); the friend's grass ends 50 m sooner at 1080p |
