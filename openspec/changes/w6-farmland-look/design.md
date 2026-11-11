# Design: w6-farmland-look

Task 1.1's part: the settings that make our pipeline render the farmland like the friend's project at their 10 saved cameras, why each one, and what each one does to the picture and to the frame. Task 1.2 adds the frame rate and memory. Numbers are labelled **measured** or **derived**; how they were measured is in §6.

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
