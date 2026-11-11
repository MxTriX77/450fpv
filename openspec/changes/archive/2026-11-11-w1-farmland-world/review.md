# QA review: w1-farmland-world (task 3.1)

**Verdict: merge.** The farmland is in the player as the friend's project shows it, and every third-party file stays local.
- **Every spec scenario passes:**
  - our noclip matches the friend's renders at the same 10 cameras in terrain, vegetation and buildings
  - a fresh worktree gets the world with no missing reference
  - the start point is free and level, and its test fails on four known-wrong spots
- **The crash on quit is fixed on the evidence:**
  - 20 of 20 smoke runs in a row on a fresh review build exited 0, with no socket and no crash dump
  - putting the stale pipeline library back reproduced the crash once, at the same fault, and the next runs were clean
- **My two pause-time findings are fixed and re-checked:**
  - F1, by task 2.3: the hygiene scan now blocks a force-added store file
  - F2, by task 2.4: `build_latest.ps1` links and unlinks the store, and the build refuses an unlinked farmland
- **Nothing blocks.** The open items below are for the orchestrator (F9, the line's merge into `unity/main`), W5 (F7) and W6 (F11).

**Scope:** `unity/physics/physics-integration-2--w1-world` at `edbd5a5e`, tasks 1.1–2.4.
- **Merge base:** `97686f5e`, the line after W0. The local `unity/physics/physics-integration-2` ref is stale; `origin`'s line is at `849da02d` with W2.
- **The branch merges into the line cleanly:** `git merge-tree --write-tree origin/unity/physics/physics-integration-2 HEAD` exits 0, with no common file. W2's `FarmlandExport` finds `FarmLandBuilder` by reflection, as W1's start test does, with no name clash.
- **Not into `unity/main` yet:** see F9.
- **Decisions:** this branch's `docs/decisions.md` doesn't hold D-088–D-095 (cut before them), so they were read from `unity/main`.

**How I checked:**
- Every result below is from my own runs.
- **The QA worktree:** a fresh detached worktree (`qa-w1`), with `git lfs checkout` and `link.ps1`, moved from `5e9f7256` to `edbd5a5e` after the pause.
- **Scratch files** lived in my session's scratchpad, or briefly in that worktree: a known-wrong start test, and a review-build-only shot hook. Both were removed after use; the worktree was clean before each clean build.
- **The friend's project was never opened in place.** A copy without `Library/` was opened, rendered and deleted.
- **At the end:** `-Unlink`, then `git worktree remove` without `--force`.
- **The pilot's prefs** (`HKCU\Software\450fpv`, `LocalLow\450fpv`, `%LOCALAPPDATA%\450fpv`) were snapshotted before each block of runs and put back byte for byte after it:
  - The smoke runs' cut by the pauses had left the smoke test's `setup.json`.
  - EditMode rewrites `TestResults.xml`.
  - Every player run bumps Unity's session counters in `HKCU`.
  - After the restore: 0 file differences, the `HKCU` export identical, and `%LOCALAPPDATA%\450fpv` unchanged.

## Checks run (at `edbd5a5e`)

```
> python tools/hygiene/precommit.py --tree HEAD
3277 files scanned, 0 findings                                   exit 0
> python tools/assets/fetch.py --verify
OK                                                               exit 0
> openspec validate w1-farmland-world --strict
Change 'w1-farmland-world' is valid                              exit 0
> python -m pytest tools/vendor tools/hygiene -q -p no:cacheprovider
FAILED tools/hygiene/tests/test_precommit.py::test_existing_tree_is_clean[origin/unity/main merged with HEAD]
1 failed, 416 passed in 271.16s                                  exit 1   (F9: a merge conflict with unity/main, not a scan finding)
> python tools/hygiene/bench.py
hygiene scan, 200 staged files, 1000-file reference/: median 0.495 s, worst 0.529 s of 30; limit 2.0 s, margin 1.471 s;
AC, Balanced plan, best power efficiency mode, CPU 3201/3201 MHz                                   exit 0
```
- **The bench (owed from 2.3) ran on AC, but not on a quiet machine:** two other agents' `pytest tools` runs and a Blender batch were running. Load only slows the scan, so the 1.47 s margin holds a fortiori.

**EditMode** (`-batchmode -nographics`, `-testFilter "FarmlandStartTests|FarmLandSceneTests|Fpv450.Tests.App|Fpv450.Feed"`):

| Run | Result |
|---|---|
| `5e9f7256`, fresh worktree, first import (410 s, import and tests) | 121 tests, 119 passed, 2 failed: `LegShotsTests.EachFlightShowsItsLeg` (known) and `FpvCameraTests.EveryRendererCarriesTheFeed` (first import only, F3) |
| `5e9f7256`, warm Library | 120 of 121; only `LegShots` fails |
| **The merge base without W1**, fresh worktree (`Fpv450.Feed`) | 92 of 93; **`FpvCameraTests.EveryRendererCarriesTheFeed` fails the same way**, so it is pre-existing |
| **`edbd5a5e`**, warm Library | 120 of 121; only `LegShots` fails. `FarmLandSceneTests` (now through `SceneReferences`), `FarmlandStartTests` and `PsoLibraryTests` (4 of 4) pass |

- **The compile check:** 0 `error CS` and 0 `warning CS` in every import log.
- **The review builds:** `Build Finished, Result: Success.`, 0 errors.
- **`[Build] Windows`** reports 0 missing references for Boot, TestRange and FarmLand.

## Spec scenarios

| Scenario | Result | Evidence |
|---|---|---|
| **Noclip shows the friend's world** | **Passed** | At the friend's 10 saved cameras, our player's noclip shows the same terrain, the same tree, bush and grass instances, and the same ruins and debris as the friend's project. Look differences belong to W6 (F11; E5) |
| **A fresh worktree gets the world** | **Passed** | A new worktree: `git lfs checkout`, then `link.ps1` (56 links), then a first import. `FarmLandSceneTests` passed, and `precommit.py --tree HEAD` is clean (E1). `build_latest.ps1` did the same in its own fresh worktree (E4) |
| **The start point is free and level** | **Passed** | `FarmlandStartTests`: 62.7 m from the village's centre (radius 75), footprint 0.214 m, the base at most 2.1 mm off the terrain, the nearest object 21.43 m clear. The same measurement fails on four known-wrong spots (E2). The stand is seen on the terrain in the player (E3) |

## Requirements

| ID | Status | Why |
|---|---|---|
| WLD-08 as D-092 | **Met** | One start point (`village`), inside the village's circle, level, 21.4 m clear. With legs off, the launch stand stands there (in noclip, always). The flight's use of it is W3's. See F7 |
| PLT-06 | **Met for noclip** | From the menu's Explore to the farmland's first frame: 0.81–0.97 s in the 20-run sequence, and 1.37 s on `build_latest`'s first run, against 20 s. On AC, Balanced. "Flyable" on the farmland is W3's |
| PRD-05 | **Met** | Seen in the smoke test's menu shot: Train "flies the test range", Calibrate, Explore "the farmland, noclip only for now" (review builds only), Exit |
| PRD-07 | **Met by reuse** | Explore puts the existing `NoclipCamera` on the scene's camera (60° with the feed off, as the friend's capture camera) and switches the friend's `FreeFlyCamera` off. No noclip code changed |

## Decisions

### D-089: nothing third-party pushed; the store linked
- **Nothing pushed is vendor content:**
  - The 307 pushed files under `Farm_land/` are all text (`git diff --numstat` shows no binary).
  - No pushed YAML embeds a mesh or a texture (`git grep "^--- !u!(43|28) "`: 0). The shaders carry the friend's own comments.
  - Nothing under `vendor/`, `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/` is in the diff.
- **Links:**
  - `link.ps1` makes 5 junctions and 51 hard links, all ignored by git (56 of 56), and a second run changes nothing.
  - A plain copy at a link's path stops it at its "exists and is not a link" throw.
  - `-Unlink` leaves that copy with a warning and removes everything else. `git status` stays clean, and relinking restores all 56.
- **precommit blocks the store's paths (2.3, F1):**
  - Before 2.3, a force-added `unity/Assets/Art/DiffusionProfileFoliage.asset` (Book of the Dead) passed `precommit.py` with exit 0.
  - At `edbd5a5e` the same probe, run with a temporary index, is blocked:
    ```
    pre-commit: blocked by the repo hygiene scan (SEC-06). Matched text is not printed.
      ignored-path        unity/Assets/Art/*.asset
      ignored-path        unity/Assets/Farm_land/Terrain/*.meta
    exit 1
    ```
- **A build can't ship a broken farmland (2.4, F2):**
  - After `link.ps1 -Unlink` in the QA worktree, `Fpv450.Build.Windows -review` refused, with exit 1 after 39 s:
    ```
    [Build] Windows: Assets/Farm_land/Scenes/FarmLand.unity has 286 missing references, so the player would show it broken. The farmland's come from the vendor store: run pwsh tools/vendor/link.ps1 in this checkout, then build again
      Terrain: Terrain.m_TerrainData
    ```
  - The `build_latest.ps1` run (E4) linked, built, unlinked and removed its worktree.
- **The store is untouched.** Its project's `Assets/` held 1,046 files with the same sizes and write times before and after all of these:
  - two fresh imports
  - every EditMode run and build
  - 40 player runs
  - the unlink and relink cycles
  - the `build_latest` run
  - the copy and deletion of the friend's project
- **The player includes the world:** the review build is 922 MB and opens FarmLand.

### D-092: the start point
- Passed. See F7 for where it sits.

## The crash fix (task 2.2)
- **The cause, shown on this build.**
  - I put back the stale pipeline library that crashed task 2.1's runs (2,110,204 B, kept by the tech artist), writable, and ran the fixed review build three times:
    ```
    oldlib 01: exit 0xC0000005, [Smoke] PASS, pso 0 B ro, pso-line next run, dump [450fpv.exe.25892.dmp]
    oldlib 02: exit 0x0, [Smoke] PASS, pso 0 B ro, pso-line ok, no sockets, dump []
    oldlib 03: exit 0x0, [Smoke] PASS, pso 0 B ro, pso-line ok, no sockets, dump []
    ```
  - The new dump faults where all 10 of Windows' earlier dumps fault: `0xC0000005`, a read, at `D3D12Core.dll+0x264831`. A stack scan against Unity's player symbols shows `GfxDeviceD3D12::~GfxDeviceD3D12` under `DestroyGfxDevice` under `DoQuit`, the graphics device's teardown at quit.
  - So the stale library alone crashes this build on quit, and once the guard has emptied the file, it doesn't.
- **Not independently checked:**
  - that the faulting call is `ID3D12PipelineLibrary::Serialize`
  - the runtime's "store refused, shader pointers kept" mechanism

  Both need `D3D12Core.dll`'s symbols, a download that needs the pilot's OK. Neither changes the fix or the verdict.
- **No library at all** (`%TEMP%` cleaned): 1 run. Exit 0. The guard made the empty read-only file, and the smoke test deferred its log check to the next run, as designed.
- **The guard is surgical:**
  - one 59-line file (`PsoLibrary`), plus 2.4's `FoundEmpty` and `InitFailure`
  - one `RuntimeInitializeOnLoadMethod`, in D3D12 players only
  - it touches only Unity's pipeline-cache file
  - the branch changes nothing else in the feed: no shader, setting, `FpvCamera` or `FeedFeature`
- **The feed's look stays (D-026), by construction.** The pipeline cache decides how fast a pipeline is created, not what it draws, and every `Fpv450.Feed` test passed.
- **A machine with an old library:**
  - Its first run with the fix crashes once on quit (measured, 1 of 1). Every run after starts empty.
  - That run's smoke test says PASS but exits `0xC0000005`, and `build_latest.ps1`'s exit check catches that.
  - On this machine the file is 0 B and read-only.
- **After a Unity upgrade (2.4, F6):** the smoke test now checks that the player log has "PSO Cache Library Init Failure" whenever the guard found the file empty. A Unity that renames the file fails the smoke test instead of quietly ending the fix. The check passed in all 25 smoke runs that found the file empty, `build_latest`'s included, and deferred correctly in the 2 that dropped or made it.
- **The hangs** design §6 couldn't explain: none in my 40 player runs. 38 finished on their own, and the two pauses stopped 2.

## The smoke test
- **The build:** a fresh review build at `edbd5a5e`, into an emptied `Builds/Windows-Review`, with `Fpv450.Build.Windows -review`: exit 0, 0 missing references, `Build Finished, Result: Success.`
- **How each run was made:** `450fpv.exe -smoketest -windowed -logFile <log>`. While it ran, my script sampled every 0.5 s for listening TCP sockets and UDP endpoints held by the player or its crash handler.
- **Conditions:** AC power, Balanced, the PSO file 0 B and read-only before.
```
final 01: exit 0x0, 30 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.97 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 02: exit 0x0, 20 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.82 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 03: exit 0x0, 20 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.82 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 04: exit 0x0, 22 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.92 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 05: exit 0x0, 21 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.84 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 06: exit 0x0, 21 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.88 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 07: exit 0x0, 21 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.90 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 08: exit 0x0, 20 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.83 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 09: exit 0x0, 20 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.84 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 10: exit 0x0, 20 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.86 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 11: exit 0x0, 20 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.81 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 12: exit 0x0, 21 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.85 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 13: exit 0x0, 21 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.84 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 14: exit 0x0, 20 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.81 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 15: exit 0x0, 20 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.83 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 16: exit 0x0, 21 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.87 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 17: exit 0x0, 20 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.84 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 18: exit 0x0, 20 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.82 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 19: exit 0x0, 20 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.82 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
final 20: exit 0x0, 20 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.85 s, pso 0 B ro, pso-line ok, no sockets, shader-unsupported 0, dump []
```
- **20 of 20 in a row.**
- **Earlier runs:** before the pauses, 10 more runs of the `5e9f7256` build passed the same way.

## The terrain shader warning (the orchestrator's question)
- **Where the warning comes from: the editor's first import, not the player.**
  - "Shader Unsupported: 'Universal Render Pipeline/Terrain/Lit' - All subshaders removed" (and its "Base Pass") came 14 times in each of two logs: `build_latest`'s fresh-worktree build, and the first import of the friend's own project. In both, the editor imports URP's `TerrainLitBase.shader` with a graphics device and a new Library.
  - It does not appear in `-nographics` imports, in warm-Library builds, or in any player log: 0 in all 40 player logs, `build_latest`'s `smoke.log` included.
- **The player has the shader:** the review build compiles `Universal Render Pipeline/Terrain/Lit` with 104 d3d11 programs (`ForwardLit`: 16 vertex, 64 fragment variants after stripping).
- **The farmland's terrain draws in the player:** in every one of the 20 noclip shots (feed on and off) and in the stand shot.
- **The test range has no Unity terrain** (0 `Terrain` components in `TestRange.unity`), so it doesn't use this shader. Only the farmland's 17 terrains do, through `FarmLand_TerrainLit.mat`.
- **Verdict: harmless, no fix needed.** The tech artist may want to confirm that it is URP's subshaders being imported before the pipeline is active.

## Findings
- **F1. Fixed by 2.3.** The scan blocks store paths that `.gitignore` ignores (above).
  - **Note:** `git check-ignore --no-index` reads the worktree's `.gitignore`, so a commit that also deletes the ignore rule would pass. That `.gitignore` change would show in the diff and its review.
- **F2. Fixed by 2.4:**
  - `build_latest.ps1` links after LFS, and unlinks before a kept worktree's checkout and before removing the worktree.
  - It runs only a `link.ps1` that declares `-Unlink`.
  - `Fpv450.Build.Windows` refuses missing references.
  - Measured in E4 and above.
- **F3. Note:** `FpvCameraTests.EveryRendererCarriesTheFeed` fails on a first import, also on the merge base without W1, and passes warm. Add it to the known list.
- **F4. Nit:** design §2 says the pushed text is 1.6 MB; git's blob sizes add up to 0.99 MB for the 307 files.
- **F5. Note:**
  - Design §3 warns that a recursive delete can follow a junction into the store. I tested it in a scratch repo, with a junction and a hard link to a canary folder:
    - `git worktree remove --force` left the junction and the folder behind
    - `Remove-Item -Recurse -Force` and Git Bash's `rm -rf` removed the worktree
  - The canary survived all three. `-Unlink` first stays right.
- **F6. Done in 2.4:** the smoke test's PSO log-line tripwire (above).
- **F7. Risk, for the pilot's farmland flight (D-093) and for W5:**
  - The start is on the hilltop meadow at the village's south-east edge: 62.7 m from the centre of the 75 m circle, 35 m from the nearest ruin.
  - The first view is the village; seen in the stand shot, the stand stands on the gravel verge with the ruin and trees ahead.
  - It meets D-092's wording, and design §6 shows that no 2 m-clear level spot exists between the ruins.
  - Ask the pilot at the flight whether it reads as "in the village".
  - W5's houses and cars (D-090) need level free ground there too. `FarmlandStartTests` holds every renderer 2 m off the stand's footprint.
- **F8. Note:** `link.ps1` tells a hard link from a copy by size and write time. It is enough: a copy with a new write time is refused (measured).
- **F9. For the orchestrator: the hygiene suite's `test_existing_tree_is_clean[origin/unity/main merged with HEAD]` is red.**
  - This branch conflicts with `unity/main` in `tools/build_latest.ps1` and `docs/tooling.md`. The worktree-removal change is on both sides (`8a76fd90` on main, `eb8e9090` here; main's two files are byte-identical to `eb8e9090`'s), and 2.4 edits the same lines.
  - The line itself conflicts with `unity/main` today, in `.gitignore` and `tools/hygiene/tests/test_precommit.py`. That comes from PR #231 putting 2.3's files on main; W1's merge brings the line's copies of those two files level with main's.
  - It doesn't block this merge onto the line. When the line merges into `unity/main`, take the line's side of `tools/build_latest.ps1` and `docs/tooling.md`.
- **F10. The terrain shader warning:** an editor-only first-import warning; the player draws the terrain (above).
- **F11. For W6, the look against the friend's project (E5):**
  - Same composition at every camera.
  - Ours is about 4 luma darker throughout (for example 112.5 against 116.1 at the crossroads).
  - Tree shadows are hard and dappled where the friend's are soft (the village lane).
  - The distant haze is missing: in the elevated views the friend's horizon fades to a pale haze, and ours stays crisp and more saturated (saturation 101.4 against 91.2 at `view_elevated_centre`).
  - There is no ambient occlusion.
  - All four match design §4's table: soft shadows off, LDR grading, no depth texture, no SSAO, and a 50 m single-cascade shadow distance against 400 m and 4 cascades.

## Evidence

**E1. A fresh worktree gets the world.**
```
> git worktree add --detach .claude/worktrees/qa-w1 5e9f7256
> git lfs checkout
Checking out LFS objects: 100% (247/247), 697 MB | 0 B/s, done.
> pwsh tools/vendor/link.ps1
linked: ...\qa-w1\vendor -> C:\.dev\450fpv\vendor
world: linked 56, already linked 0, into ...\qa-w1\unity          (6.7 s; git status: clean)
> Unity -batchmode -nographics -runTests -testPlatform EditMode -testFilter "FarmlandStartTests|FarmLandSceneTests|Fpv450.Tests.App|Fpv450.Feed"
exit 2 after 410 s: Passed FarmLandSceneTests.Scene_OpensWithEveryReferenceFound; the 2 reds as in the table above
```

**E2. The start test against known-wrong spots.**
- A scratch copy of `FarmlandStartTests` takes the start as a parameter and records the three conditions instead of asserting them: inside the circle, the base within 1 cm of the terrain, 2 m clear.
- It was in the QA worktree for one run, then removed.
- 6 of 6 passed, each with the expected outcome.

| Spot | Inside | Level (worst) | Clear (nearest) | Expected |
|---|---|---|---|---|
| The branch's start (566.5, 569.0), 300° | yes | yes (2.1 mm) | yes (Debris_RoofSheet_A_Mesh, 21.43 m) | pass, passed |
| The lane (520, 600) | yes | **no (26.4 mm)** | **no (IslandTree)** | fail, failed |
| 35 m along the heading (536.2, 586.5), at Ruin_Stump | yes | yes (0.0 mm) | **no (Ruin_Stump_Mesh)** | fail, failed |
| 80 m east of the centre (592, 600) | **no (80.0 m, radius 75)** | yes (8.4 mm) | yes | fail, failed |
| On the nearest tree instance (540.71, 577.45) | yes | **no (37.9 mm)** | **no (IslandTree)** | fail, failed |

- **Each condition fails alone at least once:**
  - the circle, on the east spot
  - the renderers' clearance, at the ruin
  - the terrain trees' clearance, at the lane and on the tree

**E3. The menu and the stand, seen.** One run with `-smokeshots`, exit 0, PASS.
- **`01_menu`:** the four items and their two hints (PRD-05).
- **`03_explore_farmland_stand`:**
  - the feed and OSD ("! SAFE !", "SVINORIZ 10 OPTO")
  - D02 standing on the textured terrain at the gravel verge, sparse grass tufts beside it
  - trees and a house ahead
- **`06_waiting_track_stand`:** the test range on the stand, drawn as before.

**E4. `build_latest.ps1` on this branch.** Run with `-PlanFile` (base `unity/physics/physics-integration-2--w1-world`, `-review`) and `-Out` set to a scratch folder, so the pilot's `Builds\Latest` was untouched. Exit 0 after 1,080 s:
```
== Vendor store links
linked: C:\.dev\450fpv-latest\vendor -> C:\.dev\450fpv\vendor
world: linked 56, already linked 0, into C:\.dev\450fpv-latest\unity
[Build] Windows: Assets/Farm_land/Scenes/FarmLand.unity: 0 missing references
C# errors:   0
Unity exit code: 0
== Clean up
unlinked 56 world paths from C:\.dev\450fpv-latest\unity
unlinked: C:\.dev\450fpv-latest\vendor
removed C:\.dev\450fpv-latest and its Unity Library; ...
== Smoke test
  [Smoke] ok   the player log has Unity's 'PSO Cache Library Init Failure': the D3D12 pipeline library starts empty (...)
  [Smoke] ok   Explore opens the farmland in noclip, the feed on, the stand at (566.500, 37.894, 569.000) on the terrain, its first frame 1.37 s after the press (PLT-06: 20 s)
  [Smoke] PASS: 0 checks failed, 0 errors logged
```
- `C:\.dev\450fpv-latest` is gone, `git worktree list` doesn't show it, and the store is unchanged.
- The scratch output was deleted afterwards.

**E5. Our noclip beside the friend's project, at the same cameras.**
- **The friend's side:**
  - A copy of the friend's project without `Library/` (`robocopy /E /XJ /XD Library Temp Logs`), opened with Unity 6000.3.26f1; the friend's project is on 6000.3.0f1.
  - A scratch editor script called the friend's own `FarmLandCapture.SavedView` and `Capture`: 1920 × 1080, 60° vertical, the friend's `PC_RPAsset` at quality "Desktop Unlocked".
- **Our side:**
  - A review build at `edbd5a5e` with a scratch, review-only hook. With `-qashots`, it opens Explore through `AppFlow` as the menu does, moves our `NoclipCamera` to each view (ground plus 2 m, or the elevated heights) and saves the 1920 × 1080 window with the feed off (V), then on.
  - The poses match the friend's to 0.01 m, for example `view_junction` at (572.00, 40.01, 570.00), (2, 347, 0). The field of view is 60° with the feed off and 77.5° with it on.
  - The hook was deleted after the run, and its build went with the worktree.

| View | Mean abs. difference R G B | Pixels differing > 24 | Luma, friend / ours | Saturation, friend / ours |
|---|---|---|---|---|
| view_junction (the crossroads) | 13.0 11.8 9.5 | 15.4 % | 116.1 / 112.5 | 106.9 / 109.4 |
| view_field_furrows (a field) | 19.1 15.8 12.7 | 23.8 % | 117.9 / 113.9 | 109.0 / 111.2 |
| view_field_trees (a tree belt) | 14.2 12.5 11.8 | 17.5 % | 115.8 / 111.7 | 101.4 / 104.6 |
| view_dry_line (a tree belt) | 16.2 14.2 12.7 | 19.9 % | 112.3 / 108.8 | 109.2 / 111.0 |
| view_village_ruins (the village) | 13.9 13.3 11.7 | 15.9 % | 87.9 / 84.3 | 116.5 / 117.8 |
| view_village_lane (the village) | 22.8 20.1 15.7 | 29.3 % | 71.2 / 66.9 | 109.2 / 104.7 |
| view_village_above | 15.0 13.6 13.4 | 19.0 % | 117.6 / 112.4 | 100.9 / 110.0 |
| view_east (the map's edge) | 20.8 18.6 13.5 | 27.0 % | 99.8 / 95.6 | 124.0 / 124.2 |
| view_hills | 20.7 16.7 14.1 | 25.9 % | 112.3 / 108.4 | 114.5 / 116.3 |
| view_elevated_centre | 10.7 11.0 12.1 | 12.8 % | 122.7 / 115.9 | 91.2 / 101.4 |

**What I saw on the sheet** (friend's | ours, feed off | ours, feed on, all 10 views):
- **The same in every view:**
  - the terrain's relief and ground layers (tracks, furrows, dry crust)
  - every tree, bush and dry shrub in its place
  - the five ruins, the debris and the roof sheets
  - the map's edge treeline
- **The pixel differences** come mostly from the grass blades and the shadow edges, plus F11's darker, harder and haze-free look.
- **With the feed on**, the same scene reads through the PAL look and the OSD.
- The sheet and the PNGs stay in my scratchpad, as third-party renders.
