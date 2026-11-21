# Review: w18-menu-cleanup (2.1)

**Reviewer:** QA Engineer · **Branch:** `unity/game/physics-integration-2--menu-cleanup` at c74bde22 · **Commits reviewed:** e0d1dceb, 32f3eea6, 2363d842, 57d4d936, c74bde22 (on 88acbec1) · **Requirements:** PRD-05, PRD-07, APP-02, APP-03, APP-04, INP-02, PLT-06 · **The pilot, 2026-11-17 (D-117)**

## Verdict: **approve**

The menu offers Play, Calibrate and Exit in both build kinds, and nothing a pilot can press, type or move opens the farmland's noclip view. Tooling still opens it: the review build's smoke test and `-farmbench`, and the build's stills. Every describing label named in the as-built list is gone, and the layout closes up where each one stood. No text the pilot needs was removed. All five noclip map-panel checks the smoke test dropped are still proved in flight on the same farmland image. Both players built at c74bde22 pass their smoke test with exit 0 and open no socket.

Nothing blocks the merge. Two findings should be fixed soon, and neither was caused by this change:
- F1: the Rates tab's − and + buttons draw no glyph. It is visible in the captures from before the change too.
- F8: the session test that compares Unity with headless pins a hash from an older physics model. Unity and .NET agree with each other; the pin is out of date.

The remaining findings are notes.

## Scenarios

My runs are the review player built at c74bde22 (Mono, 97 s) and its `-smoketest`, the pilot's player built at c74bde22 and its `-smoketest`, `-farmbench` in each, and Unity's whole EditMode suite (output at the end). My review smoke's screen shots are pixel-identical to the developer's `after/` ones (01, 02, 03, 04, 06, 09, 16: no pixel differs by more than 30).

| Spec · scenario | Result | Evidence |
|---|---|---|
| app-flow · Into the air in a few steps | **pass** | Smoke, both builds: "Play opens the carousel", "the carousel's keys chose House: its setup", "Fly loads the flight over House", "armed on House". No screen between them (`AppFlow.Choose` → `SetupChoice.Fly` → `Load` → the flight). |
| app-flow · A review build's menu | **pass** (wording, F4) | Review smoke `01_menu`: 450FPV, Svinoriz 10 Opto, Play, Calibrate, Exit, the radio's line, "build 1.0 · review"; log "[App] menu: Play, Calibrate, Exit (a review build)". `MenuChoice` is `{ None, Play, Calibrate, Exit }`; `MenuScreen.Items()` takes no build kind. |
| app-flow · The menu | **pass** | `01_menu` in both builds: no hint beside any item. `PlayTests.TheMenuReadsPlayCalibrateAndExit`. |
| app-flow · The setup | **pass** | `06_setup` (Village), `16_setup_house`: the name, Payload/Legs/Time of day/Wind/Rain (no Start: each map has one start), On/Off, Back and Fly, no note. `Notes` gets only `SetupStore`'s read error (`AppFlow.cs:147`) and is cleared on Setup from the summary (`:245`). |
| app-flow · Loading | **pass** | `09_loading_village`: "Loading Village…" alone. `PlayTests.TheCardsTheSetupAndTheLoadingScreenReadNoDescriptions` pins both maps' lines. |
| app-flow · The flight's first seconds | **pass** | `10_waiting_village`: "Connect the radio by USB, in joystick mode · Esc for the menu"; `18_rest_house` (keyboard): "Arm: Enter, with the throttle down (S) · V: 1st / 3rd person · M: map · Esc pauses". The problem hint is `FlightScene.cs:652`, unchanged. |
| app-flow · Calibrate | **pass** (before Start by code only, F2) | `03_calibrate_rates`: tabs, curves, names, values, Defaults, no footnote. Before Start with a radio (`Idle`): Start and Profiles only, by the diff; no run has a radio. |
| play-maps · The menu | **pass** | `01_menu` in the review build and in the pilot's build. |
| play-maps · Choosing a map | **pass** (keyboard by run; mouse, sticks and Esc by code, unchanged since w10) | Smoke: "the carousel's keys chose Village / House: its setup, under …", then "Fly loads the flight over …" at that map's start. Esc from the carousel: `AppFlow.cs:271`. |
| play-maps · A card | **pass** | `04_play_test-range`, `05_play_farmland`: the still, the name, the dashes; `MapCard`'s fields are Name and Still (test). |
| world-map · Flying with the map open | **pass** | `AppTests.MTogglesOnlyTheMap` in the EditMode run. The only `MapPanel.Add` left is the flight's (`FlightScene.cs:172`); the noclip view has none. |
| farmland-world · Noclip shows the friend's world | **not testable here** (F5) | Needs the friend's project rendered at the five cameras; this change only removes components that draw nothing in a render (the panel shows on M, the throttle element read the radio). |
| farmland-world · Tooling reaches the noclip view | **pass** | Review smoke: "the noclip view opens the farmland, the feed on, the stand at (566.500, 37.894, 569.000) on the terrain, its first frame 1.84 s after the request (PLT-06: 20 s)" and the four terrain checks (0.019 / 0.006 / 0.024 / 0.004 mm within 10 m, WLD-11: 1 mm). Smoke and bench both call `AppFlow.Load(explore: true)` (`AppFlow.cs:101`, `:932`). |
| farmland-world · A fresh worktree gets the world | **pass in substance** | Not a fresh worktree (the links were in place); both builds log "FarmLand.unity: 0 missing references"; `precommit.py --tree HEAD` 0 findings. |

**Requirement IDs:**
- **PRD-05, APP-02: met** as the pilot directed on 2026-11-17 (Play, Calibrate, Exit in every build, no hints); their text in `docs/requirements.md` is the orchestrator's to rewrite (F7).
- **PRD-07: met** for what remains: the gallery's and the test range's builds keep their noclip (untouched), and the farmland's noclip view stays for tooling with the same `NoclipCamera`. No pilot reaches it any more, by the pilot's word.
- **APP-03: met** (the setup's options unchanged, the legs by name). **APP-04: met** (the flight's hints, pause and summary unchanged; the smoke's House flight replays bit for bit).
- **INP-02: met** (the wizard untouched; only Idle's describing line went).
- **PLT-06: met** for the noclip view (1.84 s in the smoke, 3.32 s in the bench run, against 20 s), measured from the request as before (the press called the same `Load`).

### No way into the noclip view from the player

- **The menu:** no item (`MenuChoice`, `MenuScreen.Items`), in either build: the shots and the logs of both smokes.
- **A key:** `AppFlow.Update` handles Esc on Play, Setup and Flight only (`AppFlow.cs:268-273`); nothing reads a key into `Load(explore: true)`. The Boot scene's `NoclipCamera` is switched off (`Boot.cs:16-17`); a House flight switches the range's noclip rig off whole (`FlightScene.cs:157-158`) and a Village flight the friend's camera (`:179`). V is the 3rd-person view, not noclip.
- **A stick:** the carousel's sticks only move and choose (`PlayScreen`); no stick path reaches `Load(explore: true)`.
- **The command line:** `Load(explore: true)` has two callers, `Bench()` (only when `AppData.Review` and `-farmbench`, `AppFlow.cs:94`) and the smoke's review-only block (`:927`). In the pilot's build `AppData.Review` is `Debug.isDebugBuild` = false. My run of the pilot's player with `-farmbench` sat at the menu for 40 s, wrote no file and logged no `[App] explore` line (below).
- **Tooling still works:** the review player's `-smoketest` (above) and `-farmbench <file> -benchruns noclip` (exit 0, the file written, the path 965 m flown) open it; the build's `MapStills.RenderAll` rendered Village's still from `FarmlandExplore.Open` ("[Stills] farmland: … camera at (569.0981, 39.4942436, 567.5)"), in both builds. I did not run `MapStills.Check` (the still against the noclip view's first frame): the two components that left `FarmlandExplore.Open` draw nothing in an editor render (the panel only after M in `OnGUI`, the throttle element only from a radio in a player's `Update`), so it can't tell this change apart.

### The smoke test's checks in noclip

The developer's `before/smoke.log` (56 ok) against my review run (51 ok), check by check, numbers masked:

| Before | After | Verdict |
|---|---|---|
| Explore opens the farmland in noclip, the feed on, the stand … its first frame … after the press | the noclip view opens the farmland, … after the request | **renamed**, same condition (`AppFlow.cs:941-942`) |
| Esc's route back to the menu | the menu again after the noclip view | **renamed**; it always called `Menu()`, never pressed Esc, so the old name over-claimed |
| the map in Explore: 'farmland', its image 2048 px with 12 mips, hidden | the map in flight, waiting for the radio: 'farmland', … hidden | **covered**: the same farmland image loaded and hidden |
| M held in Explore: the flight's keys are none | M held in flight, waiting for the radio (and over House) | **covered**, and in noclip there are no flight keys left to leak into |
| M in Explore: the map shows, 950×950 … margins … | M in flight, waiting for the radio: the map shows, 950×950 … (and over House) | **covered**: same panel, same window |
| the marker in Explore at (569.1, 567.5) m … 0.000 image px …, heading -60.0° | the marker in flight, waiting for the radio at (566.5, 569.0) m … 0.000 image px …, heading -60.0° (and House's) | **covered**: same farmland transform, a target 3 m away, same heading |
| M again in Explore: the map hides | M again in flight … (and over House) | **covered** |

What the five proved about the noclip camera alone (a panel following a camera that is not a drone) is gone with the panel, which is what the pilot asked. Nothing the smoke proved about the farmland went unreplaced. Against the base 88acbec1 (55 checks), the smoke gained one check in 32f3eea6 ("Calibrate opens from the menu, and its Back returns to the menu", already in the developer's `before/` run of 56) and three shots (Calibrate's two tabs, the loading screen), and lost the five above: 51 in the review build. The pilot's build runs no noclip block: 43 checks, every one of them also in the review run.

## Every text the player draws

Every draw call with text in `Scripts/UI` and `Scripts/App` (`grep -rn "GUI\.(Label|Button|Box|Toggle|TextField)|GUILayout\.(Label|Button|Box|Toggle|TextField|Toolbar|SelectionGrid)"`: 73 calls), plus every string that reaches them: `FlightScene` (hints, titles), `PostFlight.Lines`, `CalibrationWizard.Prompt`, `FcSettingRow.Rows`, `PlayScreen.Hint`, `AppFlow.RadioLine`, `AppFlow.LoadingLine`, `SetupScreen`'s options and `Notes`. The only `OnGUI`s are `AppFlow`, `FlightScene`, `CalibrateScreen`, `MapPanel` and, outside this change, `Feed/NoclipFeed` and `Feed/LookSetup` (the second is switched off in a flight, `FlightScene.cs:168-169`).

**Removed (each checked against the code and, where a capture can show it, against before and after):**

| Text | Where | Evidence |
|---|---|---|
| "House or Village" beside Play; Explore and "Village in noclip" | `MenuScreen` | `01_menu` before and after; `MenuChoice` has no `Explore` |
| "· heavy fiber-optic cargo FPV" after the drone's name | `MenuScreen` | `01_menu` |
| Each card's line ("The test range: …", "The farmland: …") | `PlayMaps`, `MapCard`, `MapCarousel` | `04_play_test-range`, `05_play_farmland`: the pixels that differ are only the line and the dashes (bbox 657–1263 × 850–901); the dashes moved up 36 px, 24 px under the name's rect, as they stood under the line |
| "· launch stand" after Off | `SetupScreen.Legs` | `06_setup`, `16_setup_house`: the only differing pixels are the Legs row (838–1313 × 312–356) |
| "arm once the drone is in"; the noclip view's "noclip" | `AppFlow.LoadingLine` | `09_loading_village` against the before's `10_`: the only differing pixels are right of "Loading Village…" (326–550 × 498–514) |
| "About a minute, with the radio alone after Start." | `CalibrateScreen.Idle` | code only: Idle draws only with a radio in, and no smoke run has one (`02_calibrate_radio` is pixel-identical before and after) |
| "Betaflight 4.5, ACTUAL rates. Changes are saved as you make them." | `RatesPanel.Draw` | `03_calibrate_rates`; Defaults stands alone, its horizontal group gone |

**Kept, each needed to operate its screen (my verdict on each, by the brief's rule):**
- Names of items, options and controls: Play, Calibrate, Exit; the carousel's title "Play", each card's name; the setup's heading and rows (Payload, Legs, Time of day, Wind, Rain, Start) and options; Calibrate's title, tabs, "Profiles", "Raw", "Calibrated", ch1…, Roll/Pitch/Yaw/Throttle, the rate groups and rows (Centre, Max rate, Expo, Mid); every button.
- Prompts: the carousel's "← → or the roll stick · Enter or pitch forward to choose · Esc back"; the flight's arm hint with "V: 1st / 3rd person · M: map · Esc pauses"; the wizard's prompts.
- States and values: the menu's radio line ("No radio: plug the TX12 in by USB, in joystick mode", or the device with its profile), "build … · review", the payload and clock, "Loading <map>…", the flight's wait for the radio with Esc, Calibrate's "Connect the radio by USB in joystick mode." or the device, "Step n of m", the result and "Saved as the active profile.", each profile's channel order, the bars' values, "Buttons on: …", ARMED/disarmed, the plots' scales and "Full stick …"/"Half stick …", "Paused", the summary (title with the map, APP-04's lines, seed and log).
- Problems and errors: the setup's unreadable-setup note (the only `Notes.Add`, `AppFlow.cs:147`), a flight's problem with Esc, "The log could not be read", the calibration's findings and slow-radio note, the stores' save errors, "keyboard: never tune from this flight", "No map for …".
- The two on the menu that are not operations, kept as the orchestrator's brief to the developer named them: the drone's name under the title, and the build's version.

**No describing label is left, and no text the pilot needs was removed by this change.** One text the pilot needs was already missing before it (F1: the Rates tab's − and + draw no glyph).

## Findings

**Blocks the merge:** none.

**Fix soon:**
- **F1 (pre-existing, not this change's): the Rates tab's − and + step buttons draw no glyph.** `RatesPanel.cs:168` and `:176` draw `"-"` and `"+"` with the skin's button, which `AppStyle.Begin` sets to `Secondary` (`AppStyle.cs:109`); in a 26 × 22 px button its font and padding leave no room, so each is a blank dark square in the 1920 × 1080 captures, before and after this change (zoomed crop `qa-menu/rates_steps_zoom.png`). The as-built list keeps "−, +" as buttons the pilot needs; on this tab they are drawn but unreadable. The setup's − and + (`AppStyle.Segment`, 52 × 44) are fine. Suggestion for the game developer: give the two step buttons a small style (the segment style at this size, or a smaller font with no padding).
- **F8 (pre-existing, outside this change): `SessionTests.AFlightIsTheSameInUnityAndHeadless` fails on its pinned hash.** In the whole EditMode run: "the records as .NET and Mono both fly them, Expected: 3156920571056202958 (0x2BCFA28885EDA4CE), But was: 10779114225721958495", with the output "3041 steps, model 103, records 0x95971B473184605F (Mono 6.13.0)". The same test on .NET (`dotnet test tools/inputtests -c Release --filter …AFlightIsTheSameInUnityAndHeadless`) gives the same **0x95971B473184605F (.NET 10.0.9)**: Unity and headless agree, so the cross-runtime property holds. The pin (`SessionTests.cs:580`) dates from c5070213 (2026-11-14, model 99), and the model has moved to 103 since (`LogFormat.cs:1033`). This change touches nothing the test reads: `git diff --stat 88acbec1..c74bde22 -- unity/Packages unity/Assets/_Project/Scripts/App/Session unity/Assets/_Project/Scripts/Input unity/Assets/_Project/Tests/EditMode/Session maps assets-src tools` is empty. It needs a re-pin by its owner, audited with each model step since 99 named as its cause; not here.

**Notes:**
- **F2: the removed Calibrate line is proved by code only.** `CalibrateScreen.Idle` draws only with a radio connected, and the smoke test always runs without one, so `02_calibrate_radio` is pixel-identical before and after. The diff (`CalibrateScreen.cs`, `Idle()` now Start and Profiles alone) is the evidence. The smoke's check "Calibrate opens from the menu, and its Back returns to the menu" asserts only the return; the open is proved by the two shots and by `_calibrate.Tab` not throwing.
- **F3: stale names of the noclip view as "Explore".** In comments and log lines that no pilot sees: `tools/bench_farmland.ps1:1-2` ("Explore opened as the menu does") and `:104` ("Explore to the first frame"), `FarmlandBench.cs:87` and `:91` (log lines), `MapStills.cs` and `MapStills.json` ("Explore's spawn frame"), `FlightScene.cs:178`, `FarmlandTreeWood.cs:15`, `FarmlandFeedDetail.cs:78` (logs "the farmland in noclip: the friend's grass distance" whenever a Village flight's scene unloads, so even the pilot's build's smoke log carries the word, with no noclip view open), and `w10-play-maps`' scenario "Village's still is Explore's first frame". The view is still `View.Explore` in `AppFlow`, so these read as the view's name; worth renaming at the archive. `tools/build_latest.json:15`'s `contents` still says "menu (Train, Calibrate, Exit, Explore)" (the orchestrator's file, already stale before this change).
- **F4: `app-flow`'s scenario "A review build's menu"** says "nothing opens the farmland in noclip", while `farmland-world`'s says a review build's `-smoketest` and `-farmbench` do. Read as "nothing in the menu", which is what the code does; suggest that wording at the archive.
- **F5: `farmland-world`'s "Noclip shows the friend's world"** now opens the view "by tooling" and places it at five cameras; no tool places the noclip view at those five cameras (the scenario was the pilot's flying before). Not testable here, and unchanged in substance by this change.
- **F6: style.** `PlayScreen.cs:31` `Still =PlayMaps.LoadStill(...)` (a missing space); `Build.cs:28` is a 166-character comment line where the file wraps at 120; `SetupScreen.Legs` is now a public mutable array (for the test; `IReadOnlyList` would do).
- **F7: the requirements' text is stale until the orchestrator rewrites it:** PRD-05 and APP-02 still read "Train / Calibrate / Exit … plus Explore in review builds" (`docs/requirements.md:119`, `:652`). The proposal hands the new wording back, as the brief asked.

**Karpathy guidelines:** surgical. Every changed line traces to the pilot's request, the smoke's new shots, or a comment that named the menu's Explore. `ExploreThrottle` and the noclip map panel are deleted, as only the menu's item used them; no new abstraction (`LoadingLine`, `Subtitle`, `Tab`, `Back()` exist for the tests and the smoke).

## Commands and key output

Each Unity and player run under the session's Unity lock (`unity-lock.ps1 -Acquire/-Release -Owner "QA, menu"`), released after each hold. Unity 6000.3.26f1; the laptop on AC (`Win32_Battery` status 2). No frame rate here is evidence (vsync on, a shared machine): the bench run checks that the entry works.

```text
# Hygiene, specs, commits
> python tools/hygiene/precommit.py --tree HEAD
3699 files scanned, 0 findings
> openspec validate w18-menu-cleanup --strict
Change 'w18-menu-cleanup' is valid
  [INFO] app-flow / play-maps / world-map: Archive would refuse this delta: target spec does not exist
  (they modify p6-input-app's, w10-play-maps' and w11-world-map's added requirements: archive after those three)
> git log --format='%h|%an <%ae>|%cn <%ce>|%s|body:[%b]' 88acbec1..HEAD
c74bde22|Game Developer <gamedev@450fpv.local>|Game Developer <gamedev@450fpv.local>|docs(w18): the menu cleanup as built|body:[]
57d4d936|Game Developer …|feat(ui): the screens drop the labels that only describe|body:[]
2363d842|Game Developer …|feat(app): no explore in the menu, the noclip view is tooling's alone|body:[]
32f3eea6|Game Developer …|test(app): the smoke test shoots the loading and calibrate screens|body:[]
e0d1dceb|Game Developer …|docs(w18): propose the menu without explore or describing labels|body:[]
  subjects 36–69 characters; git log --format='%(trailers)' → 0 lines; the identity is team.md's
> git log --name-only --format= 88acbec1..HEAD | grep -iE 'Library/|Temp/|Logs/|UserSettings/|reference/|\.(png|jpe?g|mp4|exe|dll|log)$'
(nothing)
> git diff --check 88acbec1..c74bde22
(nothing)
> git rev-parse HEAD origin/unity/game/physics-integration-2--menu-cleanup
c74bde22… c74bde22…   (pushed)
> git grep -n "0255d3ab5669f554eafdb7e0fb7ec7a5\|ExploreThrottle\|MenuChoice.Explore\|Items(true)\|Items(false)"
only the proposal, the tasks and w10's old review mention them; no scene, prefab or code references the deleted script's GUID

# Hold A: the review player at c74bde22
> Unity -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.Windows -review -logFile build-review.log | Out-Host
[Build] Windows: Assets/_Project/Scenes/Boot.unity / TestRange.unity / Assets/Farm_land/Scenes/FarmLand.unity: 0 missing references
[Stills] farmland: …\unity\Generated\stills\farmland.png (1218447 bytes), 1920x1080, camera at (569.0981, 39.4942436, 567.5) …
[Build] Windows: Mono2x, managed stripping Disabled
Build Finished, Result: Success.
build-review: exit 0 after 97 s
> 450fpv.exe -smoketest -windowed -smokeshots qa-menu\review\shots -logFile smoke-review.log   (sockets polled every 2 s)
[App] menu: Play, Calibrate, Exit (a review build); …
[Smoke] ok   Calibrate opens from the menu, and its Back returns to the menu
[Smoke] ok   the noclip view opens the farmland, the feed on, the stand at (566.500, 37.894, 569.000) on the terrain, its first frame 1.84 s after the request (PLT-06: 20 s)
[Smoke] ok   the drawn terrain round the stand in noclip's start, … within 10 m 0.019 / 0.006 / 0.024 / 0.004 mm (WLD-11: 1 mm)
[Smoke] ok   the menu again after the noclip view
[Smoke] ok   the marker in flight, waiting for the radio at (566.5, 569.0) m: image pixel (1114.8, 929.8), 0.000 image px from the transform's, heading -60.0° against -60.0°
[Smoke] ok   the flight over House, flown with V presses, replays bit for bit: match: 4495 steps and 83 events, bit for bit
[Smoke] PASS: 0 checks failed, 0 errors logged          (51 checks ok, 22 shots)
smoke-review : exit 0 after 77 s; socket polls 31, TCP entries 0 (listening 0), UDP endpoints 0
> 450fpv.exe -farmbench qa-menu\farmbench-review.json -benchruns noclip -windowed -logFile farmbench-review.log
[App] explore: the farmland's first noclip frame 3.32 s after the request (PLT-06: 20 s)
[Bench] noclip (noclip, the feed off): 5555 frames over 64.3 s …
[Bench] written to …\qa-menu\farmbench-review.json        (path_m 965.162, 1 run)
farmbench-review : exit 0 after 78 s; socket polls 33, TCP entries 0 (listening 0), UDP endpoints 0
> my shots against the developer's after/ (PIL difference > 30): 01, 02, 03, 04, 06, 09, 16 → no pixel

# Hold B: the pilot's player at c74bde22 (the developer did not build it)
> Unity -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.Windows -logFile build-pilot.log | Out-Host
[Build] Windows: … FarmLand.unity: 0 missing references; Mono2x; Build Finished, Result: Success.
build-pilot: exit 0 after 129 s
> 450fpv.exe -smoketest -windowed -smokeshots qa-menu\pilot\shots -logFile smoke-pilot.log
[App] menu: Play, Calibrate, Exit (the pilot's build); …
[Smoke] PASS: 0 checks failed, 0 errors logged          (43 checks ok, no "[App] explore" line, 20 shots)
smoke-pilot : exit 0 after 40 s; socket polls 18, TCP entries 0 (listening 0), UDP endpoints 0
> pilot 01_menu against review 01_menu: differs only at (1633–1770, 1022–1035), the "· review" after "build 1.0"
> 450fpv.exe -farmbench qa-menu\farmbench-pilot.json -windowed -logFile farmbench-pilot.log   (killed after 40 s by design)
[App] quality: High (the build's)
[App] menu: Play, Calibrate, Exit (the pilot's build); …
farmbench-pilot json written: False                     (no [App] explore, no [Bench] line: it sat at the menu)

# The pilot's profile files, before and after both smoke runs (the smoke now opens Calibrate and presses its Back)
> sha256sum -c profile-before.sha256      (calibration.json, fc-settings.json, setup.json)
calibration.json: OK  fc-settings.json: OK  setup.json: OK
```

```text
# Hold C: Unity's whole EditMode suite at c74bde22
> Unity -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/qa-w18-editmode.xml -logFile editmode.log | Out-Host
editmode: exit 2 after 3537 s
total 1812, passed 1778, failed 3, skipped 31, inconclusive 0, duration 3450.8 s
FAILED Fpv450.Sim.Tests.AllocProbeTests.TheKnownWrong_OneObjectInTheWindow_IsSeenAndNamed: one object, its header and method table pointer: 0 B
FAILED Fpv450.Sim.Tests.SweepTests.OnE01sSoilShapesTheReachHoldsTheLever: Timeout value of 180000 ms was exceeded.
FAILED Fpv450.Tests.Session.SessionTests.AFlightIsTheSameInUnityAndHeadless: the records as .NET and Mono both fly them
       Expected: 3156920571056202958  But was: 10779114225721958495   (F8)
  The first two are the sim's, and the same two failed in the last whole run on record
  (w9-levels' unity/Logs/editmode.xml, 2026-11-14: 1766 tests, 1737 passed, 2 failed).
Fpv450.Tests.App: 39 passed, among them PlayTests.TheMenuReadsPlayCalibrateAndExit,
  PlayTests.TheCardsTheSetupAndTheLoadingScreenReadNoDescriptions, PlayTests.PlayOffersHouseThenVillage,
  AppTests.MTogglesOnlyTheMap (2.8 s), AppTests.EveryOsdModeShowsTheThrottle, AppTests.TheAppsKeysNeverReachTheFlight

# F8's cause, headless
> dotnet test tools/inputtests -c Release --filter "FullyQualifiedName~AFlightIsTheSameInUnityAndHeadless"
the session's flight: 3041 steps, model 103, records 0x95971B473184605F (.NET 10.0.9 X64)
Failed: Expected: 3156920571056202958  But was: 10779114225721958495
```

Outputs (local, not committed): `feedback-1117/qa-menu/` in the session's scratchpad (both builds' and smokes' logs, `review/shots/` and `pilot/shots/`, `editmode.xml`, `farmbench-review.json`, `rates_steps_zoom.png`). The pilot's player built here is in this worktree's `unity/Builds/Windows/` (git-ignored).
