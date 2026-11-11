# QA review: w1-farmland-world (task 3.1)

**Status: paused on the GPU. No verdict yet.** The pilot needs the GPU, so the review stopped after its CPU-only parts. Everything below was run by me. The GPU steps still to do are listed at the end, with their exact commands.

**What the CPU checks found so far:**
- **One finding that would block the merge (F1, D-089).** The hygiene scan doesn't block the new local-only paths under `unity/Assets`. A force-added Book of the Dead file passes `precommit.py`.
- **One must-do before the pilot's build carries W1 (F2).** `tools/build_latest.ps1` never runs `link.ps1`.
- Everything else checked so far passes:
  - a fresh worktree gets the world with no missing references
  - the start point is free and level, and the start test fails on four known-wrong spots
  - the hygiene, licence and spec checks
  - 10 smoke runs exited 0 with no socket and no crash dump, but the two pauses cut both sequences, so "10 in a row" is still open

**Scope:**
- The branch `unity/physics/physics-integration-2--w1-world` at `5e9f7256`, tasks 1.1, 2.1 and 2.2.
- **Merge base:** `97686f5e`, the line after W0. The local `unity/physics/physics-integration-2` ref is stale (`163fa29d`); `origin`'s line is at `849da02d`, 12 commits ahead with W2.
- **The merge base doesn't matter for this review:**
  - The branch and W2 touch no common file, and `git merge-tree --write-tree HEAD origin/unity/physics/physics-integration-2` is clean.
  - W2's `FarmlandExport` (an Editor class) finds `FarmLandBuilder` by reflection, as W1's start test does, and has no name clash with W1's types.
  - As in W2's review, this branch's `docs/decisions.md` doesn't hold D-088–D-095 yet. They are read from `unity/main`.
- **Branch diff (25 files outside `Farm_land/`):** `link.ps1`, `.gitignore`, the build's scene list, `FarmlandExplore`, the menu's lines, `FlightWorlds.FarmlandStart`, `PsoLibrary`, three test files and the change folder. Plus `unity/Assets/Farm_land/`: 307 files, all text.

**How I checked:**
- A fresh detached worktree at `5e9f7256` (`.claude/worktrees/qa-w1`), with `git lfs checkout` and `link.ps1`.
- Scratch files lived in my session's scratchpad, outside the repo, or briefly in that worktree, removed after use.
- The friend's project was never opened in place. A copy without `Library/` was opened, rendered, and then deleted.

## Checks run

```
> python tools/hygiene/precommit.py --tree HEAD
3274 files scanned, 0 findings                                  exit 0
> python tools/assets/fetch.py --verify
OK                                                              exit 0
> python -m pytest tools/vendor tools/hygiene -q -p no:cacheprovider
415 passed in 222.07s (0:03:42)                                 exit 0   (tools/vendor has no tests; all 415 are hygiene's)
> openspec validate w1-farmland-world --strict
Change 'w1-farmland-world' is valid                             exit 0
```

**EditMode** (`-batchmode -nographics`, `-testFilter "FarmlandStartTests|FarmLandSceneTests|Fpv450.Tests.App|Fpv450.Feed"`):

| Run | Result |
|---|---|
| Fresh worktree, first import (410 s, import and tests) | 121 tests, 119 passed, 2 failed: `LegShotsTests.EachFlightShowsItsLeg` (known) and `FpvCameraTests.EveryRendererCarriesTheFeed` ("High: Expected: same as <Hidden/Fpv450/Feed> But was: <Hidden/Fpv450/Feed>") |
| Same worktree, warm Library | 121 tests, 120 passed, 1 failed: `LegShotsTests.EachFlightShowsItsLeg` (known) |
| **The merge base without W1, fresh worktree** (`Fpv450.Feed` filter) | 93 tests, 92 passed, 1 failed: **`FpvCameraTests.EveryRendererCarriesTheFeed`, the same message** |

- **The feed test's red is a first-import flake, not this branch's.** It fails on a first import with or without the farmland, and passes on a warm Library (F3).
- **The compile check:** the fresh import's log has 0 `error CS` and 0 `warning CS`, and no GUID conflict. The review build compiled with 0 `error CS` and ended `Build Finished, Result: Success.`.
- **The branch's own tests passed in every run:**
  - `FarmLandSceneTests.Scene_OpensWithEveryReferenceFound`
  - `FarmlandStartTests.TheVillageStartIsFreeAndLevel`
  - `PsoLibraryTests`, 4 of 4
  - every `Fpv450.Tests.App` test except `LegShots`

## Spec scenarios

| Scenario | Result so far | Evidence |
|---|---|---|
| **Noclip shows the friend's world** | **Not done: GPU** | The friend's 10 views are rendered from a copy of the friend's project (E5). Our player's noclip at the same cameras is GPU step 5 |
| **A fresh worktree gets the world** | **Passed** | A new worktree: `git lfs checkout`, then `link.ps1` (56 links, 6.7 s), then a first import. `FarmLandSceneTests` passed, and `precommit.py --tree HEAD` is clean (E1) |
| **The start point is free and level** | **Passed** | `FarmlandStartTests` passes: 62.7 m from the village's centre (radius 75), footprint 0.214 m, base at most 2.1 mm off the terrain, the nearest object 21.43 m clear. The same measurement fails on four known-wrong spots (E2) |

## Requirements

| ID | Status so far | Why |
|---|---|---|
| WLD-08 as D-092 | **Met** | One start point (`village`), inside the village's circle, level, 21.4 m clear. With legs off, the launch stand stands there (in noclip, always). The flight's use of it is W3's (design §6). See F7 for the pilot's reading of "in the village" |
| PLT-06 | **Met for noclip** | From the menu's Explore to the farmland's first frame: 0.83–1.44 s in 10 runs of the fresh review build, against 20 s. On AC, Balanced, machine shared with other agents. "Flyable" on the farmland is W3's |
| PRD-05 | **Met in code; not yet seen** | Train, Calibrate, Explore (review builds only) and Exit, with a quiet hint on Train's and Explore's lines. The screenshot of the menu is GPU step 4 |
| PRD-07 | **Met by reuse** | Explore puts the existing `NoclipCamera` on the scene's camera and switches the friend's `FreeFlyCamera` off. No noclip code changed. The smoke test checks that noclip is enabled on the farmland |

## Decisions

### D-089: nothing third-party pushed; the store linked
- **Nothing pushed is vendor content (checked):**
  - The 307 pushed files under `Farm_land/` are all text: `git diff --numstat` shows no binary.
  - They are 118 `.mat`, 110 `.prefab`, 32 `.cs`, 16 `.terrainlayer`, 6 `.shader`, 4 `.py`, 2 `.unity`, 2 `.hlsl` and 2 `.asset` (YAML), with their metas.
  - No pushed YAML embeds a mesh or a texture (`git grep "^--- !u!(43|28) "`: 0).
  - The shaders carry the friend's own comments, with no pack or Unity copyright.
  - Nothing under `vendor/`, `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/` is in the diff.
  - No tracked file matches an ignore rule (`git ls-files -ci --exclude-standard`: 0).
- **Links:**
  - `link.ps1` made 5 junctions and 51 hard links; a second run reported "linked 0, already linked 56".
  - Every link is ignored by `.gitignore` (`git check-ignore`: 56 of 56).
  - A plain copy at a link's path stops the script at its "exists and is not a link" throw (line 106). `-Unlink` warns, leaves that copy, and removes the other 55 links and the `vendor` junction.
  - After `-Unlink`, `git status` is clean, and relinking makes the 56 links again.
- **The store is untouched:** the store project's `Assets/` held 1,046 files with the same sizes and write times before and after:
  - the first import
  - every EditMode run
  - the review build
  - the smoke runs
  - the unlink and relink cycle
  - the copy and deletion of the friend's project
- **precommit blocks staged vendor paths: only under `vendor/`. See F1.**
- **A fresh worktree opens the scene with no missing references:** passed (E1).
- **The player includes the world:** the review build opened `Assets/Farm_land/Scenes/FarmLand.unity` and is 922 MB.

### D-092: the start point
- Passed, as above. See F7 for where it sits.

## The crash fix (task 2.2)
- **Where it crashes: confirmed on all 10 dumps Windows kept.**
  - I read each dump's exception record and module list with a small minidump reader, against Unity's own `UnityPlayer_Win64_player_mono_x64.pdb`.
  - Every one is `0xC0000005`, a read, in `D3D12Core.dll`, at one of four offsets.
  - 9 of the 10 stack scans show `GfxDeviceD3D12::~GfxDeviceD3D12` called from `DestroyGfxDevice`, the device's teardown at quit.
  - The tenth is the newest dump, written a minute after the fixed build was made: the "first run on the old file put back" of design §6. Its scan is noise, but it faults at the same `D3D12Core.dll` offset as two others.
- **Not independently checked:**
  - that the faulting call is `ID3D12PipelineLibrary::Serialize`
  - the runtime's "store refused, shader pointers kept" mechanism

  Both need `D3D12Core.dll`'s symbols or disassembly. Downloading the symbols needs the pilot's OK, and the causal chain can be tested without them: GPU step 3 repeats design §6's stale-file experiment on this build.
- **Is the guard surgical? Yes.**
  - It is one new 59-line file (`PsoLibrary`).
  - It runs one `RuntimeInitializeOnLoadMethod`, in D3D12 players only, and touches only Unity's pipeline-cache file in `temporaryCachePath`.
  - It has 4 unit tests, which passed.
  - The branch changes nothing else in the feed: no shader, setting, `FpvCamera` or `FeedFeature`.
- **The feed's look stays (D-026), by construction.** The pipeline cache decides how fast a pipeline is created, not what it draws, and every `Fpv450.Feed` test passed. The farmland gets the same `NoclipFeed` as the test range.
- **A machine with an old library:** design §6 says its first run can still crash once on quit, and every later run starts empty. On this machine the file is 0 B and read-only, before and after my runs. The pilot's current build (without the guard) can't write it either, so it doesn't refill. To re-verify: GPU step 3.
- **After a Unity upgrade:**
  - If Unity keeps the file's name and folder, the guard works as today.
  - If an upgrade renames the file, the guard does nothing and says nothing (F6).
  - If Unity deletes and rewrites a read-only file, `KeepEmpty` resets it at the next start. Its tests cover a library, no file, an empty file, and a writable empty file.
- **The hangs** design §6 couldn't explain: none in my 10 completed runs.

## The smoke test (so far)
The fresh review build was made in the QA worktree with `Fpv450.Build.Windows -review` (exit 0, `Build Finished, Result: Success.`, 922 MB, the tree clean before the build). I ran `smoke.ps1` from my scratchpad. Each run is `450fpv.exe -smoketest -windowed -logFile <log>`; while it runs, the script samples every 0.5 s for listening TCP sockets and UDP endpoints held by the player or its crash handler. AC power, Balanced.
```
seq 01: exit 0x0, 50 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 1.44 s, pso 0 B ro, no sockets, dump []
seq 02: exit 0x0, 24 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 1.01 s, pso 0 B ro, no sockets, dump []
seq 03: exit 0x0, 23 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.93 s, pso 0 B ro, no sockets, dump []
seq 04: exit 0x0, 21 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.90 s, pso 0 B ro, no sockets, dump []
seq 05: exit 0x0, 23 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.91 s, pso 0 B ro, no sockets, dump []
seq 06: exit 0x0, 21 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.83 s, pso 0 B ro, no sockets, dump []
seq 07: exit 0x0, 21 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.86 s, pso 0 B ro, no sockets, dump []
seq 08: exit 0x0, 24 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 0.86 s, pso 0 B ro, no sockets, dump []
(seq 09 killed by the pilot's pause)
run 01: exit 0x0, 117 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 1.21 s, pso 0 B ro, no sockets, dump []
run 02: exit 0x0, 24 s, [Smoke] PASS: 0 checks failed, 0 errors logged, explore 1.04 s, pso 0 B ro, no sockets, dump []
(run 03 stopped by me: the GPU went to the pilot)
```
- **10 runs of 10 passed**, with exit 0, no socket and no new crash dump. Neither sequence reached 10 uninterrupted, so "10 in a row" is GPU step 2.
- **The pilot's prefs:** the cut runs left the smoke test's `setup.json` in the profile folder. EditMode runs rewrite `TestResults.xml` there, and every player run updates Unity's session counters in `HKCU\Software\450fpv`. I put all three back from my snapshot: the files are byte-identical to it (SHA-256), the registry export matches it, and `%LOCALAPPDATA%\450fpv` is unchanged.

## Findings so far

- **F1. Would block the merge (D-089): the hygiene scan doesn't block the linked local-only paths.**
  - D-089 says the hygiene scan blocks the store's paths. Since W1, those parts also live at `unity/Assets/Art`, `TerrainSampleAssets`, `ALP_Assets`, `Farm_land/Materials`, `Farm_land/Terrain/Generated`, `Farm_land/Terrain/*.asset`, `Farm_land/Models/*.fbx` and `*.png`. `precommit.py`'s `vendor-path` rule only matches `vendor/`, so `.gitignore` is the one guard, and `git add -f` passes it.
  - Measured, with a temporary index so the worktree's own index stayed empty:
    ```
    > git add unity/Assets/Art/DiffusionProfileFoliage.asset
    The following paths are ignored by one of your .gitignore files: unity/Assets/Art      exit 1
    > git add -f unity/Assets/Art/DiffusionProfileFoliage.asset unity/Assets/Farm_land/Terrain/FarRing_E.asset.meta
    exit 0
    > python tools/hygiene/precommit.py
    exit 0      (no finding)
    ```
  - **A fix that matches the rule:** precommit blocks any staged path, and in `--tree` mode any committed path, that `.gitignore` ignores. Use one `git check-ignore --no-index --stdin` call. Today no tracked file matches an ignore rule, so `--tree HEAD` stays clean. `link.ps1` already refuses to link an unignored path, so every future link is covered too.
  - The owner is QA (`tools/hygiene`); it is one small task, before or with this merge.
- **F2. Must be done before the pilot's build carries W1: `build_latest.ps1` doesn't link the store.**
  - Its worktree (`C:\.dev\450fpv-latest`) runs `git lfs pull` but never `tools/vendor/link.ps1`, so a build there after W1 has a farmland with missing references (design §3: build players only from a linked checkout).
  - It isn't a W1 merge blocker today: `build_latest.json`'s base is `unity/game/p6-input-app--player`.
  - **Suggested:** also make `Fpv450.Build.Windows` fail when the farmland's references are missing, so an unlinked build can't ship a broken Explore.
- **F3. Note:** `FpvCameraTests.EveryRendererCarriesTheFeed` fails on a first import, also on the merge base without W1, and passes warm. It is pre-existing; add it to the known list.
- **F4. Nit:** design §2 says the pushed text is 1.6 MB; git's blob sizes add up to 0.99 MB for the 307 files.
- **F5. Note:**
  - Design §3 warns that a recursive delete can follow a junction into the store. I tested it in a scratch repo, with a junction and a hard link to a canary folder outside the worktree:
    - `git worktree remove --force` unregistered the worktree, but left the junction and the folder behind
    - `Remove-Item -Recurse -Force` and Git Bash's `rm -rf` removed the worktree
  - The canary survived all three.
  - The risk is lower than stated, but `-Unlink` first is still right.
- **F6. Suggestion: a tripwire for a Unity upgrade.** The smoke test checks that the player log has "PSO Cache Library Init Failure". It proves Unity still reads the file the guard empties, so a rename fails the smoke test instead of silently ending the fix.
- **F7. Risk, for the pilot's farmland flight (D-093) and for W5:**
  - The start is on the hilltop meadow at the village's south-east edge: 62.7 m from the centre of the 75 m circle, 35 m from the nearest ruin.
  - It meets D-092's wording, and design §6 shows that no 2 m-clear level spot exists between the ruins.
  - The pilot's "somewhere in the village" may picture the lanes. Ask at the flight; there is no question now.
  - W5's houses and cars (D-090) also need level free ground in the village, and that is the same meadow. W5 must keep 2 m from the stand's footprint, which `FarmlandStartTests` already checks for every renderer.
- **F8. Note:** `link.ps1` tells a hard link from a copy by size and write time, not by file identity. It is enough in practice: a copy with a fresh write time is refused (measured above).

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
exit 2 after 410 s; Passed FarmLandSceneTests.Scene_OpensWithEveryReferenceFound (1.25 s); the 2 reds as in the table above
```

**E2. The start test against known-wrong spots.**
- I ran a scratch copy of `FarmlandStartTests` with the start as a parameter. It records the three conditions (inside the circle, the base within 1 cm of the terrain, 2 m clear) instead of asserting them.
- It was in the QA worktree for one run, then removed.
- 6 of 6 passed: the expected outcome, a pass for the control and a fail for each wrong spot, in every case.

| Spot | Inside | Level (worst) | Clear (nearest) | Expected |
|---|---|---|---|---|
| The branch's start (566.5, 569.0), 300° | yes | yes (2.1 mm) | yes (Debris_RoofSheet_A_Mesh, 21.43 m) | pass, passed |
| The lane (520, 600) | yes | **no (26.4 mm)** | **no (IslandTree)** | fail, failed |
| 35 m along the heading (536.2, 586.5), at Ruin_Stump | yes | yes (0.0 mm) | **no (Ruin_Stump_Mesh)** | fail, failed |
| 80 m east of the centre (592, 600) | **no (80.0 m, radius 75)** | yes (8.4 mm) | yes | fail, failed |
| On the nearest tree instance (540.71, 577.45) | yes | **no (37.9 mm)** | **no (IslandTree)** | fail, failed |

Each condition fails alone at least once: the circle on the east spot, and the renderers' clearance at the ruin. The trees' clearance fails at the lane and on the tree, through the terrain trees' bounds.

**E3. The crash dumps.** All 10 `450fpv.exe.*.dmp` in `%LOCALAPPDATA%\CrashDumps`:
```
exception 0xc0000005 at D3D12Core.dll+0x264831 / +0x264c49 / +0x2649be / +0xa1f5, read (info[0] = 0)
stack scan (9 of 10): ... < GfxDeviceD3D12::~GfxDeviceD3D12 < ... < GfxDeviceD3D12 scalar deleting dtor < DestroyGfxDevice
```

**E4. The junction experiment (F5).**
```
gitforce: worktree gone=False, store canary present=True, content='canary'
pwsh:     worktree gone=True,  store canary present=True, content='canary'
bash:     worktree gone=True,  store canary present=True, content='canary'
```

**E5. The friend's views, rendered from a copy of the friend's project.**
- The copy was made with `robocopy /E /XJ /XD Library Temp Logs`, then opened with Unity 6000.3.26f1; the friend's project is on 6000.3.0f1, and the copy was upgraded.
- A scratch editor script in the copy called the friend's own `FarmLandCapture.SavedView` and `Capture` at 1920 × 1080, 60° vertical.
- The pipeline is the friend's `PC_RPAsset`, at quality "Desktop Unlocked". The run exited 0 after 795 s.
- The 10 views:
  - `view_junction`: the crossroads
  - `view_field_furrows`: a field
  - `view_field_trees` and `view_dry_line`: the tree belts
  - `view_village_ruins`, `view_village_lane` and `view_village_above`: the village
  - `view_east`: the map's edge
  - `view_hills` and `view_elevated_centre`
- The PNGs are kept in my scratchpad (`qa-w1/shots/friend/`). The copy itself is deleted.

## GPU steps left (for the resume)
Before these: Unity Hub running (it was stopped by the pause). The review build is in place, unchanged since it was made: `C:\.dev\450fpv\.claude\worktrees\qa-w1\unity\Builds\Windows-Review\450fpv.exe`, made from `5e9f7256` with a clean tree. `<qa>` is my scratchpad's `qa-w1` folder.

1. **Snapshot the pilot's prefs again:** `HKCU\Software\450fpv`, `%USERPROFILE%\AppData\LocalLow\450fpv` and `%LOCALAPPDATA%\450fpv`, as in `<qa>\prefs\`.
2. **Smoke, 20 in a row:** `pwsh -NoProfile -File <qa>\smoke.ps1 -Count 20 -Tag final`. It passes with every run's exit 0, PASS, no sockets and no dump.
3. **The old-library experiments:**
   - (a) Delete `%TEMP%\450fpv\450fpv\dx12_pso_cache_lib.bin` (clear read-only first) and run 1 smoke run. Expect exit 0, and the file back at 0 B, read-only.
   - (b) Put back the stale library the tech artist kept, `scratchpad\backup\dx12_pso_cache_lib.bin.orig` (2,110,204 B), writable, and run 3 smoke runs. Expect the first to possibly crash on quit, then 2 clean runs and the file at 0 B, read-only.
4. **The menu and the stand, seen:** one run with `-smokeshots <qa>\smokeshots`. Look at the menu (PRD-05) and `explore_farmland_stand` (the stand on the terrain, the village ahead).
5. **Our noclip at the friend's cameras:**
   - Copy `<qa>\scratchfiles\QaShots.cs` into the QA worktree's `unity/Assets/_Project/Scripts/App/`. It is review-build only: with `-qashots <dir>` it opens Explore through `AppFlow`, moves `NoclipCamera` to the 10 views, and saves each with the feed off and on.
   - Build with `Fpv450.Build.Windows -review`.
   - Run `450fpv.exe -windowed -qashots <qa>\shots\ours`.
   - Run `python -I <qa>\sheet.py <qa>\shots`, then look at the sheet: the friend's | ours, feed off | ours, feed on.
   - Delete `QaShots.cs` and its meta.
   - Look differences are W6's: report them, don't block on them. The pipelines differ (design §4: shadow distance, cascades, SSAO, HDR).
6. **Put back the prefs if a run wrote them.**
7. **Clean up:** run `pwsh tools/vendor/link.ps1 -Unlink` in the QA worktree, then `git worktree remove .claude/worktrees/qa-w1`. Check the store again: 1,046 files against `<qa>\store-before.txt`.

Then the verdict, with F1's fix in or out.
