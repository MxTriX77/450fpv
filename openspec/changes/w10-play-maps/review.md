# Review: w10-play-maps (task 2.1)

**Reviewer:** QA Engineer · **Branch:** `unity/physics/physics-integration-2--w10-play` at 6ae7a188 · **Date:** 2026-11-12
**Under review:** e38cd9b5, ab7d4548 (game developer: Play, the carousel, House), e2ef5fe6 (tech artist: the stills), with dc350af3, cef6d74c, 6ae7a188 (docs). Merged in for integration only, not reviewed: 2619fc12 (the OSD throttle preview), 0115d5ec (w11's copy of `unity/Generated/` into the player).

## Verdict: **approve**

Every spec scenario passes on runs made here. Both maps fly from Play, the House log replays bit for bit headless, the stills are rendered by the build from their params, and nothing rendered is in git. The findings below are low severity and don't block the merge. F1 is owed as a follow-up.

## Spec scenarios

| Scenario | Result | Evidence |
|---|---|---|
| The menu | **pass** | Smoke `01_menu`: Play ("House or Village"), Calibrate, Explore ("Village in noclip"), Exit. `PlayTests.TheMenuReadsPlayCalibrateExploreAndExit` passes. The pilot's build drops Explore (`MenuScreen.Items(false)`). No hint names a map its item doesn't open. |
| Choosing a map | **pass** (sticks: by code and test) | The smoke moves and chooses with ← → and Enter through the Input System: "the carousel's keys chose House: its setup, under 'House'", and the same for Village. Each setup gets its own start (`track` for House, `village` for Village). Fly loads `TestRange` or `FarmLand`. Esc → menu: `AppFlow.Update` handles `View.Play`, and so does the Back button. Mouse: `MapCarousel.cs:65-69` (a side card moves to it, the middle one chooses); not clicked by the smoke. Sticks: see below. |
| A House flight | **pass** | Smoke: "Fly loads the flight over House: the scene 'TestRange', the world 'test-range'". It waits for the radio, then flies on the smoke's keys: armed, climbed 17.00 m, summary "Flight ended · House", the log's world `test-range`. Fly again and R stay on House (flights 2 and 3, from `track`). The log replays bit for bit in the player (4511 steps, 111 events) and headless (below). |
| Village is unchanged | **pass** | The farmland smoke checks all pass: Explore's stand on the terrain, its first frame at 1.50 s, terrain parity within 10 m 0.004 to 0.024 mm, both flights at `village` (legs on, legs off on the stand), the DISARMED OSD, V's views, the saved setup. The only change in the flight path: `FlightSessionHost` now gets its world from `PlayMaps.ForScene`, and Village's is still `FlightWorlds.Farmland`. |
| Village's still is Explore's first frame | **pass**, see F1 | `MapStills.Check`: position, rotation and fov equal to the float, image mean difference 6.18 (bound 8.0). That check compares the same code with itself (F1), so I ran an independent test. The player's own Explore frame (smoke `05_explore_farmland_stand`, runtime NoclipFeed, 2 s after load) lines up with `farmland.png` best at shift (0, 0) out of ±3 px. Same framing, stand, ruin and OSD; only the timer (00:02 against 00:00), the feed's noise and the grass sway differ. |
| Nothing of the friend's world is pushed | **pass** | `precommit.py --tree HEAD`: 3568 files, 0 findings. `git check-ignore -v` matches both stills (`.gitignore:46: unity/[Gg]enerated/`). No image and no `Generated/`, `reference/`, `Library/`, `Temp/` or `Logs/` path in any commit `241145ea..HEAD`, merges included (`-m`). `git ls-files unity/Generated`: 0. |
| Rebuilt, never hand-edited | **pass** | `Build.Windows -review` rewrote both stills (mtime 16:20 → 16:32) and copied them into `generated/stills/`. `test-range.png` came out byte-identical to the earlier render (SHA-256 31A3C800B9D6…). `farmland.png` changed (wind sway), which is expected. No manual step. Not run from a fresh clone. The worktree's Library was warm. |

### The radio's sticks (`StickNav`, MapCarousel.cs:125-147)
Real sticks can't be pressed here, so I judged the code and `TheSticksMoveOneCardAPushAndChooseForward`:
- **Thresholds:** move or choose at |0.6|, re-arm when both sticks are within 0.25. The signs follow `StickSample`: + is roll right and pitch forward. Pitch back never chooses.
- **One action per push.** A move and a choose can't both happen in one step: `choose = move == 0 && …`.
- **A stick held when the screen opens:** `Open` calls `Reset`, so nothing happens until both sticks centre. The test covers a held pitch at open and a held roll after a reset.
- **Setup's Back:** returning to the carousel doesn't call `Open`. That is safe, because a choose leaves `_ready` false, so the sticks must centre again first.
- **Radio unplugged:** `Update` resets.
- **Without a calibration,** the stick reads uncalibrated AETR (`RawStickMap`), which may have the wrong sign on a reversed channel. That is acceptable: Calibrate exists, and the keys and the mouse still work.

## Look (the stills and the carousel, looked at by eye)
- **House (`test-range.png`):**
  - The car (a small hatchback) is at the left, in front of the plank fence and its gate, which run across the frame.
  - The adobe house is whole with its open rafters, its shed at the right, and a tree behind the gate. A second house is far left.
  - The car, the fence and the house are all there. The car is small (about 1/12 of the width) but reads clearly.
- **Village (`farmland.png`):**
  - The launch stand (D02) sits just above the centre cross, in grass, 3 m ahead.
  - The ruined houses and the tree belt are on the rise beyond. This is Explore's spawn, and it matches the player's frame (above).
- **On both:**
  - The analog feed: noise speckle, softening and the PAL tint.
  - The OSD: "AIR", ALT, "! SAFE !", the timer, "SVINORIZ 10 OPTO", both batteries and the current.
  - The throttle element at the right edge: the glyph and "0".
- **The carousel (`02_play_test-range`, `03_play_farmland`):**
  - The selected card is large with its still, name and line, the dashes show where it is, and Back, Choose and the hint are visible.
  - The neighbour is dimmed and smaller.
  - It reads well. F3 is about the neighbour's clipping.

## Findings

| # | Severity | Where | Finding | Owed |
|---|---|---|---|---|
| F1 | low | `Editor/Stills/MapStills.cs:138-144` against `:226-235` | `Check` compares the same code with itself. The "still" and "Explore's first frame" both come from `FarmlandExplore.Open` + `Camera.main` + the same `Shoot` (the same rig, seed and field), so equal position and rotation are certain by construction. What it proves: `FarmlandExplore.Open` places the camera the same way across scene reloads, the fov equals the value from `FeedSettings` (`VerticalFovDeg(Coverage)`, the one independent input), and the edit-mode render repeats within the wind's sway. It doesn't prove the player's runtime Explore frame matches (NoclipFeed's Awake and Start, the field the rig begins on, `ExploreThrottle`, `FarmlandFeedDetail.LateUpdate`). That proof is the smoke-frame comparison above, done by hand. | Say what `Check` proves in its doc and in tasks 1.2's "As built", or have the smoke compare its `explore_farmland_stand` shot with the still (alignment at zero shift). Follow-up, not blocking. |
| F2 | low | `MapStills.cs:43` (`SwayMean = 8.0`) | The image margin is thin and depends on load. The builder measured 4.70 and 4.8. I measured 6.18 on a loaded machine. A one-pixel shift measures 13.0. With other agents rendering, the check may fail on a correct pose. | Measure the spread over a few runs, or compare with the wind frozen (`Shader.SetGlobalFloat` for time, or terrain grass waving off for the check). |
| F3 | low | `UI/MapCarousel.cs:51-61` | At 1920 × 1080 the neighbour card runs 171 px past the window edge: the right card spans x 1496 to 2091 (`03`/`02` shots). That is a cut, not a designed "peek" (no fade). Cosmetic. | The game developer's call: reduce `step` or the side scale, or keep it as a peek on purpose. |
| F4 | info | `PlayMaps.cs:29` | House's `Starts` opens the test range's world (`FlightWorlds.TestRange(root).StartPoints`) just to read one name, on every choose. That costs a small amount of work on the main thread. Not measured as a hitch. | None. |

## Commands run (key output)

```text
# EditMode, targeted, -nographics
& $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode
  -testFilter "Fpv450.Tests.App.PlayTests;Fpv450.Tests.App.AppTests;Fpv450.Feed.Tests" ...
result: Passed  total: 118  passed: 118  failed: 0  (44.3 s)

dotnet test tools/inputtests
Passed!  - Failed: 0, Passed: 545, Skipped: 0, Total: 545, Duration: 9 m 31 s

# the review build (graphics on)
& $env:UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.Windows -review
[Build] Windows: Assets/_Project/Scenes/Boot.unity: 0 missing references   (TestRange, FarmLand: 0 too)
[Stills] test-range: ... 1920x1080, camera at (-178, 3, -34), euler (9.000, 145.000, 0.000), vfov 77.5180359°;
  seed 1 field 0: Picture; OSD ... "! SAFE !" ... "SVINORIZ 10 OPTO" ... 7@13 "{|} 0" (the throttle)
[Stills] farmland: ... camera at (569.0981, 39.4942436, 567.5), euler (28.072, 300.000, 0.000), vfov 77.5180359° ...
Build Finished, Result: Success.  [Build] Windows: Succeeded, 0 errors, 1 warnings
  (the warning: Feed.shader(189) uninitialized screen_tex on d3d11, existing)
[Build] the player's generated files: generated/stills/farmland.png (1219021 bytes)
[Build] the player's generated files: generated/stills/test-range.png (818011 bytes)

# the smoke (review build, windowed); netstat -ano sampled every 5 s for the player's PID
450fpv.exe -smoketest -windowed -smokeshots <scratch>\shots -logFile <scratch>\smoke.log
exit 0; sockets owned by the player: 0 (no listening socket, no connection)
[Smoke] PASS: 0 checks failed, 0 errors logged   (40 "ok" checks)

# House's log, headless
dotnet run -c Release --project tools/replay -- <scratch>\shots\flights\flight-6e90176d8b524f82-1.flightlog
  world 'test-range' from the map maps\test-range\placements.json: 0xF07BA3CC79D02169
  match: 4511 steps and 111 events, bit for bit
  note: recorded on runtime Mono 6.13.0 X64, replayed on .NET 10.0.9 X64; exit 0 (Match)

# the spawn-frame check
& $env:UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.MapStills.Check -out <scratch>\check
[Stills] check farmland position ... (569.0981, 39.4942436, 567.5) vs (569.0981, 39.4942436, 567.5): PASS
[Stills] check farmland rotation ... PASS
[Stills] check farmland field of view: 77.5180359 vs 77.5180359 (NoclipFeed's rig 77.5180359): PASS
[Stills] check farmland image: ... 4.85 % by more than 30, mean difference 6.18 (at most 8.0): PASS
[Stills] check: PASS

# QA's own: the player's Explore frame against the still (PIL, mean |diff| of RGB)
still vs player Explore frame, scene box (300,200)-(1600,700): 14.34; best shift over ±3 px: (0, 0)
still vs itself shifted 1 px: 9.85   (shifting never lowers the difference: the framing is the same to the pixel)

python tools/hygiene/precommit.py --tree HEAD    -> 3568 files scanned, 0 findings
git check-ignore -v unity/Generated/stills/*.png -> .gitignore:46:unity/[Gg]enerated/ (both)
git log 241145ea..HEAD (with -m): no .png/.jpg/.exr/.tga/.psd, nothing under Generated/, reference/, Library/, Temp/, Logs/
```

**Unattended and the pilot's files:**
- Every Unity run was batchmode, and no dialog was raised. No pilot's `450fpv.exe` was running.
- The smoke saves the setup over the pilot's profile and puts it back. I also backed up `calibration.json`, `fc-settings.json` and `setup.json` from the profile folder first. After the smoke, all three hash equal to the backup.

## Hygiene
- **Subjects:** every commit in `241145ea..HEAD` has a subject only (≤ 72 characters, the longest 72), lower case, `type(scope): summary`. No body, no trailer, no co-author.
- **Identities:** Game Developer, Tech Artist, World Builder, Orchestrator, each the author and the committer.
- **Staging:** each commit touches only its task's files (`--stat` above). No stray files.
- **Karpathy:**
  - Restoring House is surgical. It puts back what b0386162 (W3 3.1) removed: the scene in the build settings, `Build.Windows`' scene check, `PlayerFiles`' test-range files, the start-point read (now per map), and the `TestRangeBoot` shut-off, which `FlightScene.Attach` kept.
  - No speculative options. `MapStills.Run -out` is the pose-candidates tool, which the params' "why" cites.
  - `NoclipFeed.Place` is a refactor done so the stills and the feed share one code path.

## Observation: leftover `%TEMP%\fpv450-*` folders (not fixed here)
- **Before my runs:** no `%TEMP%\fpv450-*` folder.
- **After `dotnet test tools/inputtests`:** 7 `fpv450-session-<guid>` folders remain (16:37 to 16:38). Each holds `flights\flight-0000000000005e56-*.flightlog` or `…5e57-1.flightlog` (seeds 0x5E56 and 0x5E57).
- **The creator:** `unity/Assets/_Project/Tests/EditMode/Session/SessionTests.cs:49`. Its `[TearDown]` swallows the `IOException` from `Directory.Delete` (`:56-62`). The likely cause is a flight log still open when the test ends: a session or log writer not disposed before the teardown.
- **Also seen:** a `fpv450-farmland-<guid>` folder (`FarmlandFlightTests.cs:47`, same teardown pattern) existed during the run but was gone at the end.
- **The EditMode run** (PlayTests' `fpv450-stills-*`) left nothing.

## Owed
- F1 (follow-up): say what `MapStills.Check` proves, or add the player-frame comparison to the smoke.
- F2 (follow-up): make the image bound robust to load (freeze the wind for the check, or set the bound from a measured spread).
- F3: the game developer decides on the clipped neighbour card.
- Separately, for the test owner: the `SessionTests` teardown leak above.
