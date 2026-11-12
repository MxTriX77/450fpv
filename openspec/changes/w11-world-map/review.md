# Review: w11-world-map (2.1)

**Reviewer:** QA Engineer · **Branch:** `unity/physics/physics-integration-2--w11-map` at d77493ab (+ the QA test 5e1b3f6b) · **Commits reviewed:** c2307834, b0fbaa8d, dcf72eb7, 4c836d78, d77493ab (not the preview's merge 5d51147b) · **Requirements:** APP-04, PRD-07, PLT-02, PLT-04 · **Decision:** D-101

## Verdict: **approve**

Every scenario passes or is met in substance; nothing blocks the merge. One measurement is owed (the panel's frame cost on a quiet machine in Performance mode, on AC power). The findings are low or informational, listed as suggestions.

## Scenarios

| Scenario | Result | Evidence |
|---|---|---|
| Flying with the map open | **pass** (the test is weaker than its name, F1) | `MTogglesOnlyTheMap`: M pressed 9 times, both logs 5 997 837 bytes, byte for byte the same. The panel holds no reference to the session, the sim or the clock (`MapPanel.cs:47-52`). Nothing in `Scripts/` sets `Time.timeScale`. Its only per-frame read is the drawn body's `Transform`, on the main thread in OnGUI (`MapPanel.cs:146`), so it can't block the 1 kHz thread. The body is set from the lock-free `PosePair` in `FlightScene.LateUpdate` (`FlightScene.cs:289-294`). In the player smoke, M goes through the Input System and `KeyboardFallback.ReadKeys()` returns none while M is held. |
| Explore | **pass** (moving noclip checked by inspection only) | Smoke: "the marker in Explore at (569.1, 567.5) m … 0.000 image px from the transform's, heading -60.0° against -60.0°"; "M again in Explore: the map hides". Noclip reads only W/A/S/D/E/Q/Space/Ctrl/Shift (`NoclipCamera.cs:68-71`), and the panel consumes no input, so noclip keeps moving. No run moves noclip with the panel open. |
| The marker follows the drone | **pass** (new QA test) | `MapMarkerTests.TheMarkerFollowsAMovingTarget` runs the panel's own OnGUI on each repaint of a 30 s path at 60 frames a second. The target flies at 30 m/s and turns, with acro pitch ±40° and roll ±60°. The expected pixel is worked out from the farmland's extent, not with `MapTransform`. 1 431 frames on the image, each within 1 image px with the heading within 2°. 300 frames off the image, each with the edge marker on the image along the line towards the target. The builders' rest checks still pass. |
| The panel's size | **pass** | Smoke at 1920×1080: 950×950, margins 485 / 485 / 65 / 65 px. `ThePanelIsCentredInsideTheWindow` covers 1920×1080, 2560×1440, 1280×720 and 800×600 (4:3). The display here is 1536×864 logical, which gives a 760×760 panel with a 52 px margin. No negative rect for any window at least 48 px on its shorter side (F4). |
| Both maps | **pass** | The build rendered both maps (log below) into `unity/Generated/topdown/`, which is git-ignored, and carried all four files into the player with the same SHA-256 hashes. The panel takes its map id from the flight (`Host.Inputs.WorldId`, `FlightScene.cs:164`). Explore uses `FlightWorlds.FarmlandMap`. The ids `farmland` and `test-range` match `TopDownMaps.json` and `FlightWorlds.cs:35`. House's image was checked in the editor's output (the range isn't in the player on this branch). |
| Nothing of the friend's world pushed | **pass** | `git check-ignore -v unity/Generated/topdown/farmland.png` gives `.gitignore:46:unity/[Gg]enerated/`. No image, `Generated/` or `Farm_land/` path in any commit `241145ea..HEAD`. `git ls-files unity/Generated` returns 0. `precommit.py --tree HEAD` gives 3561 files scanned, 0 findings. |
| The cost | **owed** (indicative only) | Indicative OnGUI main-thread time per repaint: 0.050 ms shown, 0.022 ms hidden. That is about +0.03 ms, measured without the image texture and with the reflection overhead in both numbers. **The laptop was on battery** (`Win32_Battery` status 1, 52 %; `BatteryStatus.PowerOnline False`) and in Quiet mode: `enforced.power.limit` 20.00 W, Balanced scheme. By the project's rule this is not evidence. It is well under 0.2 ms, so it doesn't fail the review. |

**Requirement IDs:**
- **APP-04: met.** M is a keyboard key like R, C, X and V.
- **PRD-07: met.** Nothing pauses.
- **PLT-02 and PLT-04: partly met.** The frame cost on a quiet machine is owed. Decoding the image adds 187 to 205 ms at scene start, under the loading screen.

## What I looked at

- **Both shots** (`map_in_Explore`, `map_in_flight_waiting_for_the_radio`):
  - The map is clean over the feed, and the flight shows on both sides.
  - The arrow sits at the village crossroads east of the houses, pointing north-west (300°).
  - The hint is drawn over the panel's bottom edge.
  - The panel covers the OSD's centre while shown (F6).
- **Village's image:**
  - Readable: the fields, tracks, tree belts, the village cluster and the surround's ring belt.
  - The 1 km map, outlined from the transform on a preview, sits square in the middle two thirds.
  - The start point drawn through the transform lands on the crossroads where the smoke's arrow is.
  - North up holds by the transform check (`TopDownMaps.Check`, 1.1). It can't be judged by eye without a reference.
- **House's image:**
  - Correct but mostly empty: the belt with its verges, the trench, the yard with its houses and the car fill a thin strip in a uniform ground (F2).

## Findings

- **F1 (low): `AppTests.cs:578`.** `MTogglesOnlyTheMap` calls `map.Toggle()` itself, with `Threaded = false` (`AppTests.cs:553`).
  - The panel's own key read (`MapPanel.cs:122-127`) and its OnGUI never run, and the threaded 1 kHz loop isn't flown.
  - It does prove that M never reaches the flight's keys and that toggling doesn't touch the sim. It would not catch a panel that slowed the sim thread.
  - The structural argument in the table above and the player smoke cover the rest.
  - Suggestion: drive the panel's OnGUI as `MapMarkerTests.Repaint` does.
- **F2 (low): `TopDownMaps.json:16-21`.** House spans ±256 m (the placement file's extent), while the range's content fills about a fifth of the image's height.
  - In the 950 px panel the houses are about 10 px.
  - Suggestion for w10-play-maps, when House is back in the player: crop to the content plus a margin, or accept it at the gate.
- **F3 (info): `MapPanel.cs:147`.** The heading comes from the body's forward projected on the ground. Past 90° pitch (inverted in a flip) it swings 180° until the drone comes upright. This is momentary and only in acro.
- **F4 (info): `MapPanel.cs:164-168`.** `Layout` returns a negative rect only in a window under 48 px on its shorter side. It is harmless: nothing draws.
- **F5 (info): the farmland's render is not byte-reproducible.**
  - The builder's run gave 5 925 190 bytes and this run 5 925 917. The test range was identical.
  - There is no consequence, since the files are generated and not pushed, but byte-identical renders can't be used as a check.
- **F6 (info): the panel covers the OSD's centre elements while shown** (the top line's start, the right-middle element). This is inherent in the pilot's "a little bit smaller than whole game window". Mention it at the gate.
- **F7 (info): `AppFlow.cs:968`.** The smoke's "in flight" M check (at rest after a keyboard flight) doesn't run in the review player. Only "waiting for the radio" ran.

**Karpathy guidelines:**
- The hooks are one line each (`FlightScene.cs:164`, `FarmlandExplore.cs:41`), and the panel is a single component.
- Nothing speculative: no zoom, trail or settings.

**Commit hygiene:**
- Subject-only, at most 72 characters (the longest is 67).
- No trailers.
- Role identities: World Builder, Game Developer, Orchestrator.
- No stray paths.

**Observation, not fixed: `%TEMP%\fpv450-*`.** None before or after the EditMode runs, the build and the smoke (0 and 0). `MTogglesOnlyTheMap` creates `fpv450-map-<guid>` and deletes it in `finally`.

## Commands and key output

```text
# Before the runs
> Get-Process Unity,450fpv        # only the Hub's licence server; no 450fpv running (the pilot not flying)
> Get-CimInstance Win32_Battery   # BatteryStatus 1, 52 %   (on battery)
> nvidia-smi --query-gpu=name,enforced.power.limit,power.draw,clocks.gr,clocks.mem --format=csv
NVIDIA GeForce RTX 4050 Laptop GPU, 20.00 W, 1.78 W, 210 MHz, 405 MHz

# Targeted EditMode tests
> & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode
    -testFilter "MapPanelTests|MapMarkerTests|MTogglesOnlyTheMap|VSwitchesOnlyTheView|TheAppsKeysNeverReachTheFlight" ...
Passed MTogglesOnlyTheMap 11.7s      M pressed 9 times; the logs: 5997837 and 5997837 bytes
Passed TheAppsKeysNeverReachTheFlight
Passed VSwitchesOnlyTheView 7.1s     replay: Match, 1671 steps ... bit for bit
Passed OffTheImageTheMarkerSitsAtTheEdge / ThePanelIsCentredInsideTheWindow / TheTransformIsTopDownMaps
Failed TheMarkerFollowsAMovingTarget   frames off the image  Expected: greater than 300  But was: 300
  (the QA test's own count threshold, set to > 200, rerun:)
Passed TheMarkerFollowsAMovingTarget 0.22s
  the image (x:112, y:32, w:416, h:416) in 640×480; frames on the image 1431, off it 300;
  OnGUI's main-thread time a repaint (indicative, no image): shown 0.0497 ms, hidden 0.0221 ms

# Build (graphics on)
> & $env:UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.Windows -review -logFile ... | Out-Host
[TopDown] farmland: ... the tallest in the extent 42.8 m (terrain), the camera at 92.8 m
[TopDown] farmland: ...\unity\Generated\topdown\farmland.png (5925917 bytes), 2048x2048 over x [-250, 1250] z [-250, 1250] m, 0.732 m a pixel
[TopDown] test-range: ... (2747094 bytes), 2048x2048 over x [-256, 256] z [-256, 256] m, 0.25 m a pixel
Build Finished, Result: Success.
[Build] the player's generated files: generated/topdown/{farmland,test-range}.{json,png}
build exit 0
> sha256sum Generated/topdown/* Builds/Windows-Review/450fpv_Data/StreamingAssets/generated/topdown/*   # all four pairs equal

# Smoke (netstat -ano polled every 3 s for the player's pid)
> 450fpv.exe -smoketest -windowed -smokeshots <scratch>\shots -logFile <scratch>\smoke.log
[Map] 'farmland': 2048×2048 with 12 mips, decoded in 187 ms; M shows it
[Smoke] ok   M held in Explore: the flight's keys are none
[Smoke] ok   M in Explore: the map shows, 950×950 px in the 1920×1080 window, margins 485 / 485 / 65 / 65 px
[Smoke] ok   the marker in Explore at (569.1, 567.5) m: image pixel (1118.3, 931.8), 0.000 image px ..., heading -60.0° against -60.0°
[Smoke] ok   M again in Explore: the map hides
[Smoke] ok   the marker in flight, waiting for the radio at (566.5, 569.0) m: ... 0.000 image px ..., heading -60.0° against -60.0°
[Smoke] PASS: 0 checks failed, 0 errors logged
exit 0; netstat lines for the player's pid: 0   (no socket, listening or not)

# D-089, OPSEC, hygiene
> git check-ignore -v unity/Generated/topdown/farmland.png unity/Generated/topdown/test-range.json   # .gitignore:46:unity/[Gg]enerated/
> git log --name-only --format= 241145ea..HEAD | grep -iE '\.(png|jpe?g|exr|tga|tif|psd|bmp)$|Generated|Farm_land'   # nothing
> git ls-files unity/Generated | wc -l    # 0
> python tools/hygiene/precommit.py --tree HEAD    # 3561 files scanned, 0 findings
> git log --format='%h|%an <%ae>|%s|%b' 241145ea..HEAD   # subject-only, ≤ 67 chars, role identities, no trailers
```

## Owed

- **The panel's frame cost**, shown against hidden. Measure the feed's frame time in the player with the image drawn, on AC power, in Performance mode, vsync off, with nobody at the PC, and print the power limit and clocks. Target: at most 0.2 ms, and nothing while hidden.
- **House in the player** (with w10-play-maps): rerun the smoke's M check on House, and decide on F2.
