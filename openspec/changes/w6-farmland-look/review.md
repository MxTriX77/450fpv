# QA review: w6-farmland-look (task 2.1)

**Verdict: approve, merge.** Nothing blocks.
- **Every spec scenario passes on my own runs or on the quiet PLT-07 run's raw files:**
  - the 10 cameras: my own render of the branch is within **0.65 luma** of the friend's at every view (mean 0.46, two passes), and the sheets show the soft shadows, the AO and the haze
  - the feed through the busiest places: 111.5 fps average, 76.5 fps 1 % low (quiet run)
  - noclip: 94.5 fps 1 % low (quiet run)
  - memory and load: VRAM 1017 MB, RAM 1190 MB, Explore in 1.8 s (quiet run); 0.64 and 0.67 s in my two player runs
- **The feed's passes are byte-identical to the port (D-026):** I captured the stills at the merge base and at the head myself, and every SHA-256 is the same.
- **The friend's scene and the store are untouched:** no diff under `unity/Assets/Farm_land`, and `fetch.py --verify` re-hashes all 292 pinned store files.
- **One finding, F1, fixed in this review** (a test fix, below).
- **Owed, for the orchestrator:** the PLT-07 repeat on a quiet machine. The command and what a pass looks like are at the end.

**Scope:** `unity/physics/physics-integration-2--w6-look` at `abca35d2`, tasks 1.1 and 1.2.
- **Merge base:** `4dd71f7a`. `origin`'s line is at `c63f3bc3` (W4a, W4b and A4 merged since).
- **Merges clean:** `git merge-tree --write-tree origin/unity/physics/physics-integration-2 HEAD` exits 0.
- **Decisions:** this branch's `docs/decisions.md` predates D-088 and D-089, so I read them from `unity/main`.

**How I checked:**
- Every number below is from my own runs, except the PLT-07 figures, which come from the orchestrator's quiet run (its raw JSON, log and GPU CSV, which I read).
- **The worktree:** the brief's, at `abca35d2`, with its warm Library and the store linked.
  - For the base stills, I switched it to `4dd71f7a` (detached) and back. Unity re-imported the URP assets and recompiled at base (`still_base.log`), and the tree was clean both ways.
  - The known-wrong checks below edited tracked files briefly; each was reverted with `git checkout --`.
- **Scratch files** lived in my session's scratchpad (`qa-w6/`). One review-only shot hook (`QaW6Shots.cs`, with `runInBackground` on) went into the worktree for one build only.
  - I deleted it and its `.meta`, rebuilt clean, and checked that the clean build's `Fpv450.App.dll` has no `QaW6Shots`.
  - The review build in `unity/Builds/Windows-Review` is now that clean build of `abca35d2`'s code.
- **The pilot's prefs** (`HKCU\Software\450fpv`, `LocalLow\450fpv`, `%LOCALAPPDATA%\450fpv`) were snapshotted before my first Unity run and put back byte for byte after the player runs and the EditMode runs: 0 file differences, the `HKCU` export identical.
- **The friend's side wasn't re-rendered.** 1.1's renders of the friend's project are still in the scratchpad, and their luma reproduces the friend's column that W1's QA rendered independently to within 0.1 at all 10 views. W1's "ours" column also matches 1.1's "before" build to within 0.2. 1.1's temporary copy of the friend's project is gone.

## Spec scenarios

| Scenario | Result | Evidence |
|---|---|---|
| **The friend's cameras match:** each view within luma 2, and both show soft shadows, AO and the haze | **pass** | My own render, the friend's 10 poses to 0.01 m: worst 0.64 / 0.65, mean 0.46 / 0.46 (two passes, table below). I looked at the soft shadows, the AO and the haze on the sheets and on my crops (below) |
| **The feed over the busiest places:** 1 % low ≥ 60 fps, average ≥ 100 fps | **pass** (one quiet run; repeat owed) | Quiet run: 7173 frames in 64.4 s, 111.5 fps average, 76.5 fps 1 % low, worst frame 15.4 ms |
| **Noclip over the busiest places:** 1 % low ≥ 60 fps | **pass** (same) | Quiet run: 8476 frames in 64.3 s, 131.7 fps average, 94.5 fps 1 % low, worst frame 11.8 ms |
| **Memory and load:** VRAM ≤ 4.5 GB, RAM ≤ 8 GB, first flyable frame ≤ 20 s | **pass** | Quiet run: peak dedicated VRAM 1017 MB; RAM 1190 MB working set, 2440 MB private; Explore 1.80 s. My runs: 0.64 s (shot run), 0.67 s (smoke) |
| *(brief)* The analog feed's passes byte-identical to the port (D-026) | **pass** | `FeedCapture.Stills`, one fixed input frame, fields 500–502, seed 1, run by me at `4dd71f7a` and at `abca35d2`: the same SHA-256 for all 3 stills (`3996de96…`, `63240675…`, `aca0d649…`), also equal to 1.1's and 1.2's stored stills |
| *(brief)* The friend's scene and the store untouched (D-089) | **pass** | `git diff` under `unity/Assets/Farm_land`: empty, and `git status` there is clean. `fetch.py --verify`: OK (it SHA-256s the 292 pinned local-only files). The store's 32,902 files: none written after the proposal's commit (2026-11-11 23:15); the newest is from 15:30 that day |

**The 10 views**, my render (`-qaw6shots`: Explore through `AppFlow` as the menu does, then V to turn the feed off, so `FarmlandFeedDetail` logged "the friend's grass distance"; 1920 × 1080, quality High, field of view 60°, D3D12). The luma is the mean BT.601 luma of the 8-bit sRGB frame.

| View | Friend's | Pass 1 | Pass 2 | 1.1's before (stored) |
|---|---|---|---|---|
| view_junction | 116.1 | 115.8 (−0.26) | 115.8 (−0.26) | 112.5 (−3.56) |
| view_field_furrows | 117.9 | 117.4 (−0.54) | 117.4 (−0.52) | 113.9 (−4.05) |
| view_field_trees | 115.8 | 115.2 (−0.64) | 115.2 (−0.65) | 111.7 (−4.09) |
| view_dry_line | 112.3 | 111.8 (−0.51) | 111.8 (−0.54) | 108.8 (−3.52) |
| view_village_ruins | 87.9 | 87.4 (−0.48) | 87.5 (−0.40) | 84.4 (−3.51) |
| view_village_lane | 71.2 | 70.8 (−0.40) | 70.7 (−0.46) | 66.7 (−4.51) |
| view_village_above | 117.6 | 117.1 (−0.50) | 117.1 (−0.51) | 112.4 (−5.18) |
| view_east | 99.9 | 99.6 (−0.25) | 99.6 (−0.29) | 95.7 (−4.21) |
| view_hills | 112.3 | 111.8 (−0.49) | 111.8 (−0.48) | 108.4 (−3.86) |
| view_elevated_centre | 122.7 | 122.2 (−0.51) | 122.2 (−0.50) | 115.9 (−6.81) |

- **The same script on 1.1's and 1.2's stored frames** reproduces design.md's tables: 1.1 at 0.47 mean / 0.64 worst, 1.2 at 0.43 / 0.64.
- **Saturation:** 108.8 against the friend's 108.3, as design.md says.
- **The luma target fails a known-wrong model:** the build before the change is 4.33 mean and 6.81 worst. But hard shadows (0.70 worst) and SSAO off (0.45) would pass it (design.md §3), so the scenario's other half, the soft shadows and the AO, was judged by eye. It holds.

## Requirements

| ID | Result | Evidence |
|---|---|---|
| **PLT-02** | **met** on the quiet run; one sample, the repeat owed | The quiet run's raw JSON, log and GPU CSV: AC; Windows' power mode Best performance; GPU 2415 MHz median, 64.8 W, 100 % busy; vsync 0; 1920 × 1080 windowed; 2 s warm-up, 64 s measured; no other Unity, .NET or Blender process. Margins: feed average +11.5 %, feed 1 % low +27 %, noclip 1 % low +58 % (see Statistics) |
| **PLT-05** | **met** | VRAM 1017 MB of 4608; RAM 1190 MB working set (2440 MB private) of 8192 |
| **PLT-06** | **met** | 1.80 s (quiet run), 0.64 s and 0.67 s (mine), of 20 s |
| **PLT-07** | **met by the runner** | `tools/bench_farmland.ps1` prints AC, the plan and mode, the GPU's limit, clocks, power, load and temperature, the CPU's effective clock, the other processes, average, 1 % low, worst frame, CPU main and render ms, the wait to present, FTM GPU ms, draw calls, triangles, VRAM and RAM. The path measures 64 s (≥ 30 s; `FarmlandBenchTests`) |
| **FEED-10** | **met** | Below |
| **REAL-01, REAL-03** | **met** | The side-by-side sheets against the friend's renders, the farmland's reference (D-088). Below |
| **D-026** | **held** | The stills, byte-identical at base and head |
| **D-088, D-089** | **held** | The farmland's look matches the friend's, and the scene and store are untouched |

**FEED-10 (≤ 1.0 ms at 1080p output):**
- **The quiet run, through the feed:** `FrameTimingManager`'s GPU time is 0.68 ms.
- **What that time covers:** with the feed on, it covers the output camera, not the FPV camera's scene render.
  - The frame is 8.97 ms and GPU-bound, and the main thread plus the wait to present is 8.96 ms, so FTM's 0.68 ms is not the frame.
  - In design.md §7.8, a quarter-size scene read moved FTM by 0.21 ms, the encode pass's scaling as p4-feed measured it. Taking SSAO off the output camera moved it too (§7.3). So the window holds the output camera's work, the feed's three passes included.
  - 0.68 ms is therefore an upper bound on the feed's cost.
- **The passes themselves didn't change:** the stills are byte-identical, and no feed shader, feature or render size changed. So p4-feed's own FEED-10 measurement (`-feedcost`, the "Feed" sampler, 0.293 ms on a quiet machine) still applies.
- **At the 20 W power limit,** FTM read 1.2 ms. That is over 1.0 ms, but those runs aren't PLT-07 evidence.

## The sheets (looked at)

**1.1's sheets** (friend's | ours before | ours after, feed off | ours after, feed on), all 10, plus the SSAO crop and the test range before/after:
- **view_village_lane:** the friend's broad, soft canopy shadow over the lane. "Before" was a hard leaf dapple; "after" is the friend's soft shadow, and the feed carries it. My full-resolution crop of my own frame shows the same.
- **view_village_above, view_elevated_centre:** "before" had a crisp, saturated horizon. "After" fades into the friend's pale haze.
  - On "before", dark blotches showed in the far field and around the dry trees. They are gone on "after", as in the friend's.
  - The far belts carry shadows.
- **view_east, view_village_ruins:** the bush's foreground shadow and the shadow under the oak are soft like the friend's, where "before" was hard and darker. The distant fields are hazed.
- **view_junction, view_field_furrows, view_field_trees, view_dry_line, view_hills:** the exposure lifts to the friend's. The grass, the tracks, the trees and the house match.
  - view_junction's feed-on panel is the blue "AV IN 1 / PAL 50" screen. That is a scheduled dropout (1.1 didn't pin the field), not a defect.
- **The SSAO crop (village_ruins canopy):** the canopy interior and the branch junctions are darker with SSAO than without it, closer to the friend's. The effect is subtle, as §3 measures (0.19 luma).
- **The test range through the feed:** in the low sun, the 200 m belt now reads as a dark, self-shadowed band. The overcast trench is darker. Both come from the scene's light, not the feed.

**1.2's sheets** (friend | noclip 1.1 | noclip 1.2 | feed 1.1 | feed 1.2, the field pinned at 500), all 10:
- **Noclip, 1.1 against 1.2:** I couldn't tell them apart at any view (SSAO at half resolution).
- **Through the feed, 1.1 against 1.2:** I couldn't tell them apart either. I cropped the far band of view_elevated_centre and view_village_above (grass to 100 m against 200 m): at the feed's resolution the band past 100 m reads the same, so the trade costs nothing the PAL picture shows.
- **The lane's soft shadow** is kept through the feed in 1.2.

## The diff (`git diff origin/unity/physics/physics-integration-2...HEAD`)

25 files, +1154 / −16.
- **Settings:**
  - `URP-High.asset`: HDR grading, 400 m × 4 shadows, the friend's splits and bias, High soft shadows, the output renderer in the list, URP's own prefiltering rewrite
  - `URP-High-Renderer.asset`: SSAO, half resolution
  - the new `URP-High-FeedOutput-Renderer.asset`
  - the URP global settings' two SSAO resource refs, which are defined in the file
  - `GraphicsSettings.asset`: the exponential-squared fog variants kept
  - `ProjectSettings.asset`: frame timing stats on
- **Code:**
  - `FpvCamera.OutputRendererOf` and its use
  - `FeedSetup`'s fourth renderer
  - `FarmlandFeedDetail` (runtime only; the friend's field set by reflection, and restored on V and on disable)
  - `FarmlandBench`, and `AppFlow`'s `-farmbench` hook (review builds only)
- **Tests:** `FarmlandBenchTests`, and `FpvCameraTests.TheOutputCameraHasARendererForTheFeedAlone`.
- **Tools and docs:** `tools/bench_farmland.ps1`, the change's docs, and the report.

**In scope:** everything traces to the proposal's impact list (the URP assets, renderers and volumes; the feed's camera setup; the scripted path and its runner; the report). Nothing touches the friend's files, the store, the feed's shaders or passes, or Medium and Low beyond the fog variants.

**Hygiene:**
- **Commits:** 12, each subject-only (no body, no trailer, no co-author line, nothing naming Claude or Anthropic), at most 70 characters. They are under the role identities: Tech Artist for 10, Orchestrator for 2.
- **Forbidden paths:** nothing from `reference/`, `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/`, or the store's linked folders.
- **The OPSEC scan:** `precommit.py --tree HEAD`, 3344 files, 0 findings. The docs name only the map's own field ids and the village. The timestamps are dev runs.
- **Licences:** no new third-party file; `fetch.py --verify` OK.
- **Builds leave the tree clean:** after my two review builds, `git status` was empty. That confirms design.md's note on URP's prefiltering rewrite.

**Notes, not blocking:**
- **Ownership:** the tech artist wrote `Scripts/App/` (the game developer's) and two tests (QA's). Both are within the proposal's scope, and I reviewed them as such.
- **Frame timing stats now ship in the pilot's build,** not only the review builds. Unity's per-frame cost for them is small, and the quiet run was measured with them on.

## Findings

**F1 (fixed in this review, test).** `TheOutputCameraHasARendererForTheFeedAlone` compared the feed shader with `Is.SameAs`, a C# reference test.
- **The problem:** on a fresh worktree's first import, the active pipeline (High, which now references the output renderer) loads before `Feed.shader` is imported. Its feature then holds another C# wrapper of the same shader.
- **Where it was seen:** W1's review saw that failure in `EveryRendererCarriesTheFeed` (W1 F3). The line fixed that test in `cb99a809` (A4) by comparing as a Unity object.
- **The fix:** after the merge, the new test would be the one first-import red left, so I changed it to `Is.EqualTo`, as A4 did.
- **What I checked, on a warm Library:**
  - 109 of 109 pass after the fix.
  - With a wrong shader planted on the output renderer, the fixed test still fails ("Expected Hidden/Fpv450/Feed, but was … ScreenSpaceAmbientOcclusion").
  - The first-import case itself wasn't reproduced: that needs a cold Library.

**Suggestions (for later work, not this branch):**
- **S1, the bench pauses when its window loses focus.** `runInBackground` is 0, which is why the quiet repeat stalled. `-farmbench` could turn it on and mark a run invalid when `Application.isFocused` goes false, so a touched PC gives a clear failure instead of a 900 s timeout.
- **S2, `FarmlandFeedDetail` has no test.** Its switch (the grass to 100 m through the feed, the friend's 200 m back on V) shows only in the player log. My runs logged both directions ("its grass to 100 m (9 tiles, 1 culler)", then "the friend's grass distance"). A PlayMode or scene-level check would hold it, and also catch the friend's private `grassDistance` field disappearing (it logs an error today).
- **S3, the runner restores only `HKCU`.** It doesn't restore `LocalLow\450fpv` or `%LOCALAPPDATA%\450fpv`. With `-logFile` the player writes no `Player.log`, and my runs left both folders unchanged, but snapshot them around the runner anyway (the PLT-07 command below does).
- **S4, the power mode is the pilot's to know.** PLT-02 is met in Windows' Best performance mode (the vendor's Performance mode). In Best power efficiency the GPU is held at 20 W, and the feed missed PLT-02 at 59–64 fps. That's worth one line in the pilot's notes for the farmland build.

## Statistics

- **The 1 % low** is the 99th-percentile frame of about 7,000–8,500 frames, so about 70–85 frames lie in the tail.
- **The run lengths:** 64 s against PLT-07's 30 s.
- **Repeatability:** the same-run pairs on the shared machine repeated within 2 % (average) and 3 % (1 % low). Across hours, with changing load, they moved up to 8 % (design.md §7.6).
- **The margins** on the quiet run are 11.5 % (feed average), 27 % (feed 1 % low) and 58 % (noclip 1 % low). That is above the same-load spread, so a quiet repeat is unlikely to fail by chance. The feed average's margin is the thinnest.
- **Known-wrong:** the same runner on the same build at 20 W printed "NOT met" for both views, so its verdict discriminates.
- **The busiest place:** the path's 99th-percentile frame is at least as heavy as the heaviest of the friend's 10 views, measured in the same 20 W conditions (derived from 1.2's stored runs). Noclip: the path's p99 is 19.1 ms against view_hills' 18.3 ms. Feed: 23.1 ms against 19.6 ms (that run had Low soft shadows).

## The PLT-07 repeat (owed: the orchestrator, in the next quiet window)

**Before:**
- The machine is quiet: no Unity, Blender, .NET or Python run.
- AC power, and the laptop's Performance mode on (Windows reports "Best performance").
- Nobody touches the PC for about 3 minutes: the player pauses if its window loses focus (S1).
- The build is `unity/Builds/Windows-Review`, my clean build of `abca35d2`'s code (my commit only adds a test).

**The commands,** in PowerShell from `C:\.dev\450fpv\.claude\worktrees\w6-look`, wrapped in the scratchpad's own prefs script:

```powershell
pwsh -NoProfile -File <scratchpad>\prefs.ps1 save
pwsh -File tools/bench_farmland.ps1
pwsh -NoProfile -File <scratchpad>\prefs.ps1 restore
```

**A pass is all of these in the printed report:**
- **Power and machine:**
  - `power: AC`
  - `mode Best performance (ded574b5-…)`
  - the GPU's graphics clock median printed (about 2.4 GHz in the quiet run, not about 1.3 GHz, which means the 20 W limit)
  - `others running:` with no Unity, blender, dotnet, 450fpv or python process (pwsh shells and Unity Hub's `unity serve` are fine)
- **The player:**
  - `exit 0`
  - `HKCU prefs put back: True`
  - `vsync 0, quality High`, `1920x1080 Windowed`
- **The feed run:**
  - at least 30 s measured (about 64 s)
  - `average ≥ 100 fps` and `1 % low ≥ 60 fps`
  - render `2754x1549`
  - the player log has "its grass to 100 m"
- **The noclip run:** `1 % low ≥ 60 fps`
- **Memory and load:**
  - `peak dedicated VRAM ≤ 4608 MB`, `peak RAM ≤ 8192 MB working set`
  - `Explore to the first frame ≤ 20 s`
- **The verdict lines:** `PLT-02 feed: met`, `PLT-02 noclip: met`, `PLT-05: met; PLT-06: met`
- **After the restore:** `prefs.ps1 restore` prints 0 differences and `HKCU identical: True`.
- **FEED-10:** the feed run's `GPU (FrameTimingManager)` ≤ 1.0 ms (output camera only).

The first run's figures, for comparison: feed 111.5 / 76.5 fps, noclip 131.7 / 94.5 fps, VRAM 1017 MB, Explore 1.8 s.
- **A repeat within about 5 % of these** is a pass that confirms the first run.
- **A miss with the GPU near 1.3 GHz** is the power mode, not the build. Re-run with the Performance mode on.

## Checks run

```
> python tools/hygiene/precommit.py --tree HEAD
3344 files scanned, 0 findings                                                exit 0
> python tools/assets/fetch.py --verify
OK                                                                            exit 0
> openspec validate w6-farmland-look --strict
Change 'w6-farmland-look' is valid                                            exit 0
> git merge-tree --write-tree origin/unity/physics/physics-integration-2 HEAD
(merges clean)                                                                exit 0
```

**EditMode** (`-batchmode -nographics -runTests -testPlatform EditMode -testFilter "Feed|Farm"`):

| Run | Result |
|---|---|
| `abca35d2`, warm Library | 109 of 109 passed (74 s): FpvCameraTests 9, PsoLibraryTests 4, FeedLinkTests 16, FeedOsdTests 8, FeedScheduleTests 37, FeedSticksTests 2, FeedUniformsTests 17, RollingShutterTests 5, AppTests 3, FarmlandBenchTests 1, FarmlandStartTests 1, FarmLandSceneTests 1, FiberCoverageTests 4, FiberViewTests 1 |
| Known-wrong: URP-High without its output renderer, and the path's speed 40 m/s | 2 of 2 fail as they should: "High: Expected ≥ 0, But was −1"; "PLT-07: 30 s measured: Expected ≥ 30.0, But was 24.13" (exit 2); reverted |
| Known-wrong for F1's fix: the output renderer's feature on the SSAO shader | fails as it should: "Expected Hidden/Fpv450/Feed, But was Hidden/Universal Render Pipeline/ScreenSpaceAmbientOcclusion"; reverted |
| With F1's fix | **109 of 109 passed** (exit 0) |

**The feed's stills** (`-executeMethod Fpv450.Feed.FeedCapture.Stills -feedField 500-502 -feedInput <1.1's before frame of view_junction, SHA-256 91696841…>`):

```
4dd71f7a (base)   feed-1-0500 3996de963c7aa900  feed-1-0501 63240675d043b209  feed-1-0502 aca0d64961ba21c0   exit 0
abca35d2 (head)   feed-1-0500 3996de963c7aa900  feed-1-0501 63240675d043b209  feed-1-0502 aca0d64961ba21c0   exit 0
1.1 before/after, 1.2 (stored)   the same three hashes
```

**Review builds** (`-executeMethod Fpv450.Build.Windows -review`), both exit 0 and "Build Finished, Result: Success"; Boot, TestRange and FarmLand each with 0 missing references:
- 69 s with the QA hook
- 49 s clean, and the hook isn't in the clean build

**Smoke test** (`450fpv.exe -smoketest -windowed -logFile <scratch>\smoke.log`), exit 0 in 18 s:
```
[Smoke] ok   the menu opens first
[Smoke] ok   the player log has Unity's 'PSO Cache Library Init Failure': the D3D12 pipeline library starts empty
[Smoke] ok   Explore opens the farmland in noclip, the feed on, the stand at (566.500, 37.894, 569.000) on the terrain, its first frame 0.67 s after the press (PLT-06: 20 s)
... 18 more "ok" lines (Train, the range, Fly with legs on and off, the OSD, V's views, the setup saved, Menu)
[Smoke] PASS: 0 checks failed, 0 errors logged
```

**The QA shot run** (the hooked build, `-qaw6shots`), exit 0 in 47 s:
```
[App] the farmland through the feed: its grass to 100 m (9 tiles, 1 culler)
[App] explore: the farmland's first noclip frame 0.64 s after the menu's Explore (PLT-06: 20 s)
[App] the farmland in noclip: the friend's grass distance
[QA] 1920x1080, quality High, fov 60, feed running False, vsync 0, NVIDIA GeForce RTX 4050 Laptop GPU Direct3D12
[QA] pass 1 view_junction at (572.00, 40.01, 570.00) (2, 347, 0)   ... 20 shots
```

**The pilot's prefs**, after every block of runs: `LocalLow : 0 differences`, `Local : 0 differences`, `HKCU identical: True`.
