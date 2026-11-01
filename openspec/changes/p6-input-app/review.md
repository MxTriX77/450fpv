# QA review: p6-input-app (task 2.1, the G7 flow on the player branch)

**Verdict: green for 1.4b, with a merge condition and five fixes before G7.** Every scenario I could run passed, and so did every check the brief lists:
- the timing contract through the session's own threads at 30, 60 and 240 fps
- the failsafe inside one RC frame
- pause and restart in the log
- the summary read from the log
- three flights the player recorded, replayed bit for bit
- the keyboard in development builds only
- both players' smoke tests, PASS with 0 errors, with their shots looked at

**Nothing blocks the merge of 1.4b's own work.** The branch lands only after the work it carries (see "Merge condition"). Findings 14, 15, 16 and 18 are not blocking, and should be fixed before G7. Finding 17 is the orchestrator's.

**Scope:** `unity/game/p6-input-app--player` at `7aae8b83`, reviewed in its own worktree on `unity/qa/p6-input-app--review`.
- **The change's own work here is 1.4b:** the 12 first-parent commits `8f224833..7aae8b83`, about 1 800 lines in `Scripts/App`, `Scripts/UI`, `Editor/Build.cs` and the tests.
- **1.1–1.5 are on `unity/main`** (#144) and were reviewed in `review-interim.md`. Since that merge (`4f6e1c4a`), these show 0 changed lines: the input layer, the sim core and `tools/{simcore,simtests,flightbench,replay,inputcore,inputtests}`. The only exception is 11 lines of `Scripts/Input/NoclipCamera.cs`, which came with the feed.
- **Out of scope:**
  - 1.2b and 1.5b (the pilot's TX12, at G7)
  - the work this branch carries but does not own

**How I checked:**
- the owner's tests rerun, with their printed figures
- a scratch probe of my own (never committed) on the engine-free assemblies: through `FlightSession`'s real threads, with my own radio and my own timing rule
- `tools/replay` on every log
- the two built players' smoke tests, their logs and their shots, looked at (whole, and in crops where it mattered)

## Findings

14. **Not blocking (fix before G7): Restart from the pause panel without a radio throws, and the loss is never logged** (INP-05, APP-04; owner: game developer).
    - **The cause:**
      - `FlightSession.Restart` is `EndFlight(); Start();` (`FlightSession.cs:329-333`).
      - `Start` throws "no radio is connected" in a pilot's build (`:198`).
      - `FlightSessionHost.Restart` (`FlightSessionHost.cs:111-116`) has no catch, unlike `Begin` (`:93-104`). So the exception leaves `FlightScene.OnGUI` (`FlightScene.cs:336`) unhandled.
    - **Probe Q2** (a pilot's build, keyboard not allowed): armed at throttle 0.3, paused, the cable pulled, then Restart.
      - The result: `InvalidOperationException: no radio is connected`.
      - The state is `Ended` and the end `Pilot`. The summary reads "ended by the pilot", with **no failsafe in the log**.
      - End flight after a loss during the pause is the same: the drained loss (`_lostWhilePaused`) only reaches the log through `Resume`.
    - **Why it matters:**
      - The pilot is safe (no step runs), and the app recovers: the summary's Fly again waits for the radio.
      - But a pause to sort out the cable, then Restart, is a likely G7 move.
      - INP-05 says the loss is logged.
    - **Fix (owner's choice):**
      - Have the host's Restart end the flight and ask for the next one (`_wanted = true`, as Fly does), so it starts once the radio is back.
      - Record a loss the pause drained when the pilot ends the flight: in the end, or as a summary line with the loss's stamp.
      - Add Q2's sequence as a test.
15. **Not blocking: the waiting view is the chosen start only before the first flight** (the game developer's open point; owner: game developer).
    - **What happens:** `FlightScene.LateUpdate` previews only while `session.Flights == 0` (`FlightScene.cs:149-153`). After any flight, with the radio out, Fly again (or Setup, then Fly) leaves the view as it was:
      - the camera on the last flight's final pose
      - the last flight's sky and time
      - the last flight's stand and stand-in
    - **What the task claims:** task 1.4b's note says the build waits "after each Fly … with the view from the chosen start at the chosen time".
    - **Judgement: the preview itself is right.** In the review player's shots `waiting_track` and `waiting_field`, it shows the chosen start, its heading and its time of day, at the resting camera height. It is the same picture as the development player's `rest_track` at arming.
      - Run the preview whenever `Host.WaitingForRadio`.
      - Reset it on each Fly.
      - Drop the last flight's stand.
      - Cosmetic, and only in a pilot's build.
16. **Not blocking, nit: the arming hint is the radio's on the keyboard** (the game developer's open point; development builds only).
    - **What it says:** "Arm: switch off, then on, with the throttle down", on every flight (`FlightScene.cs:330`; visible in `rest_track`).
    - **What the keyboard does:** it arms with Enter, as a toggle (`KeyboardFallback`). `HoldArm` applies only to the radio (`FlightSession.cs:220-229`).
    - **Judgement:** key the hint on `session.Input`, for example "Arm: Enter, with the throttle down (S)" on the keyboard. One line.
17. **Not blocking, for the orchestrator: the quality preset has no home** (APP-05; the game developer's open point).
    - **What exists:** nothing offers, saves or restores a quality preset. The player runs at `QualitySettings`' Standalone default, level 2 (High).
    - **Why 1.4b could not add one:** PRD-04 allows the setup exactly five settings plus the start point, and PRD-05 allows the menu exactly Train / Calibrate / Exit. A quality preset would break either.
    - **Judgement: a spec conflict, not a 1.4b defect.** A suggested decision:
      - at G7, the preset is the build's (High)
      - a `-quality Low|Medium|High` option, saved to the profile when given
      - APP-05 amended to match
      - a qualitative question to the pilot at G7, whether they want a choice, only if their frame rate asks for it (D-024 allows preferences)
    - APP-05 is partly met until then.
18. **Not blocking (fix before G7): the grey text renders at 2.2:1, not its designed 6.3:1** (APP-02, "clean, readable"; owner: game developer).
    - **What is dim:**
      - the setup's row labels (Payload … Start)
      - the summary's detail lines (time, distance, battery, the keyboard's warning, the seed, the log)
      - the menu's subtitle and its radio line
    - **Measured** on the development player's shots, against their panel (27, 31, 36):
      - the labels: 2.25:1
      - the summary lines: 2.18:1
      - the white values: 12.2:1
    - **The cause:**
      - `AppStyle.Muted` is (0.58, 0.62, 0.67), which would be 6.3:1. It renders as (75, 87, 103).
      - `Text` (0.93, 0.94, 0.95) renders as (215, 222, 226).
      - Both are the defined value raised to the power 2.2 (predicted (77, 89, 106) and (217, 223, 228)): IMGUI takes the text colours as linear in this Linear-colour-space project (`m_ActiveColorSpace: 1`). The fills keep their values.
    - **Fix:** give the text styles their colours' `.gamma`, or pick `Muted` for its rendered value.
    - **Status:** legible at 1080 lines, but it is the first thing the pilot reads after each flight. The look is judged at G7 (D-042).

**The interim review's findings, at this tip:**
- **3 stands, judged as follows: crash objects are named only when they are runtime objects** (`PostFlight.cs:69`).
  - **Not blocking for 1.4b.** The world is still the floor stub (`FlightWorlds.TestRange`, id "floor"). A flight can touch only the ground ("the ground") and the launch stand, which is a runtime object and is named.
  - **The cost is visible, though:** R01_A stands 17.5 m in front of the `track` start (`rest_track` shows it), and the drone flies through it in this build.
  - **Judgement:** name world objects through the world the header already identifies (`WorldId`, checked by `WorldHash`), the way replay reopens it, not through a new header entry. Make it part of the `MapWorld` swap at the player's world seam, and re-run "a crash into the car" against the placed R01_A then.
- **4 is resolved:**
  - The app-flow spec and the design name the test range.
  - The spec lists the start point.
  - The stale tasks line is gone. `design.md:4` only lists what existed before the change.
- **5 stands:** `LogHeader.cs:195-196` still gives a step's stick age without the pauses.
- **6 stands:** today's latency test prints "p50 2.000, p99 2.200, max 2.176 ms" for the intervals.
- **7 stands:** `LatencyTests` still times real threads, with no stated false-failure rate. It passed in my two runs, one of them beside another agent's Unity and Blender jobs.
- **13 is built as suggested, and accepted:** `CalibrateScreen.SlowRadioNote` names a radio slower than 4 375 µs on the Done screen, for a refusal and a saved profile alike (`AppTests.ASlowRadioIsNamedAfterFinding13`).

**Looked at, and right:**
- **The feed's blue screen.** The development player's `air_chase_track` and `pause` shots show the goggles' blue "AV IN 1 / PAL 50" screen. It is the feed's own recovering dropout: `FeedEvents`' timer, 5–32 s apart, a blue of 18–35 fields, which is the pilot's choice (D-026). The two shots are about 0.35 s apart, and the picture is back by the summary. Not a defect.
- **The flight views:**
  - the FPV view is level at 0° uptilt
  - the OSD reads "Svinoriz 10 Opto" and "! SAFE !"
  - the chase view shows the labelled stand-in on its legs, and on the four-post stand with the legs off
  - the 17:30 start has its low sun's long shadows
  - the menu and the pause panel are clean

**Notes for other changes (not this branch's to fix):**
- **`p4-feed` (FEED-09):**
  - In a flight, the feed draws from `FeedSettings.seed` (`FpvCamera.cs:135`), one fixed seed, on a field counter that starts with the scene.
  - Neither is in the flight log, so a recorded flight cannot replay the feed the pilot saw.
  - Each scene load also repeats the same dropout times.
  - Seed it from the flight and log it, with the field at step 0. The flight wiring (`FlightScene`) would carry it.
- **`p4-feed`'s test `FpvCameraTests.EveryRendererCarriesTheFeed`:**
  - It failed on the first import of a fresh Library: "High: Expected: same as <Hidden/Fpv450/Feed> But was: <Hidden/Fpv450/Feed>".
  - It passed on the rerun (883 of 883).
  - The High renderer is the active level, loaded before the shader's import settles. The test asserts object identity, so a clean clone's first run is red. Compare the GUID, or the shader's name and pass count.

## Merge condition (not a finding against 1.4b)
- **The carried work.** The branch carries work that is not on `unity/main` and has no QA review:
  - `p4-feed` 1.1–1.8 at `fc2cf1d` (its 2.1 is open)
  - `p6-test-range` at `0b7c568c` (its branch tip is `7d47968e`; its 2.1 is open)
  - `p1-g1-look-lock-gallery`

  `unity/main` has no `maps/test-range/` at all.
- **A commit in the carried history.** It holds `c91d0cfa`, a Tech Artist merge with a `# Conflicts:` body and a "Merge remote-tracking branch" subject. Today's commit-msg hook would refuse it. It is the gallery's to settle when it lands.
- **The order.** The handover's merge order already says the player lands after this work.
- **My verdict covers 1.4b's own diff only.** After the carried work lands:
  1. Merge `unity/main` into the player branch.
  2. Check that `git diff unity/main` holds 1.4b's files alone.
  3. Open the PR.

## Scenarios

| scenario | result | evidence |
|---|---|---|
| tx-input: render rate does not touch the sticks | passed | **Owner's test:** at 30 / 60 / 240 fps, identical to the rule, with 618 reports and 1 loss each. Its frame-bound control has 780 of 1200 steps stale. **Probe Q1:** through `FlightSession`'s own flight thread and `RadioReader`'s input thread, on a recorded TX12 stream (`LeadUs` 10 ms, the loss at 2.4 s), with frames at 30, 60 (with a 300 ms pause) and 240 fps. **0 of 2403 / 2102 / 2702 steps** differ from "the latest report at or before the step's host time" (with the logged pause shift), and no step uses a sample from its future. The same rule 1 ms early differs at 1201 / 1051 / 1201 steps, so the check can fail. All three logs replay bit for bit |
| tx-input: latency is measured | passed (synthetic); the TX12 is not testable (1.5b) | First step: p50 0.477, p99 0.561 ms. Motor: p50 6.453, p99 8.460 ms (the mean 7.373 = 0.474 + 2.000 + 1.563 + 3.336). Both are in the header and the summary. Betaflight's chain: 1.562 / 3.335 ms. The same as the interim's |
| tx-input: the TX12 pitfall is caught | passed | The owner's tests rerun: the 79 / 85 spans, the radio's 79 / 1 and 85 / 0, and the E-series of findings 2 and 9–12, among 501 tests. The calibration code is unchanged since the interim's re-checks |
| tx-input: under two minutes, no keyboard | not testable (1.2b) | The synthetic TX12: 19.0 s (owner's test) |
| tx-input: the defaults are the sim default column | passed | `AFreshProfileIsTheSimDefaultColumn` rerun |
| tx-input: pulling the cable mid-flight | passed | **Owner's test:** lost at host 1 271 000 µs, disarmed at the step at 1 274 000 µs. **`PullingTheCableMidFlight`:** lost at 1 234 567 µs, disarmed at step 736. **Probe Q1**, on the real threads: disarmed 2.602 ms (30 fps) and 1.752 ms (60 fps, after the pause) after the loss, inside one 4 ms RC frame, with the failsafe logged with its stamp |
| tx-input: pulling the cable during a pause | passed; finding 14 lies outside it | E3a, E3b and E3f (1, 2, 3 ms) rerun: lost at 1 471 000 µs, logged at step 342, the end at step 345, "failsafe". A Restart or End flight instead of Resume: finding 14 |
| app-flow: into the air in a few steps | passed with the development keyboard; the radio half is not testable (no TX12) | **Development player:** the menu, then Train, then Fly, loads the range and starts flight 1 at once on the keyboard, with the hint up; one arm press arms it (the smoke test's checks). **Review player:** Fly loads the range and waits at the chosen start for the radio, so arming is the only step left once it is in. Nothing sits between Fly and the flight |
| app-flow: pause and restart | passed | **Development player, `track`:** "paused: no step for 0.3 s, at step 4887". The log has `PAUSED for 374484 us` at step 4888, and every later step's stick is from at or before its shifted host time. **`field`:** Restart turned seed `0x7AC386C74D391E0C` (999 steps, "ended by the pilot") into a new flight, seed `0x1A59F3C7C5CF423A`, with a new log. Its pause is at step 4881, 305 378 µs. Both logs replay bit for bit. Owner's tests rerun too |
| app-flow: a crash into the car | partly (finding 3) | "crash: coil hit R01_A (steel), an impact at 4.3 m/s", from the log, with R01_A as a runtime object (owner's test rerun). The placed car is not collidable yet (the floor stub) |
| app-flow: settings survive a restart | passed; the quality preset is not built (finding 17) | `TheRollRateAndThePayloadSurviveARestart` and `SettingsSurviveARestartBitForBit` rerun. Both players: "the last setup is saved for the next start" (the stand, legs off), and the pilot's own setup is put back. Here there was none, and `setup.json` was removed again |

**The checks the brief lists:**
- **The timing contract against recorded traces:** probe Q1 above. On the player's own three logs, no step uses a sample from its future. The stick's age is at most 7.1 ms when no pause intervenes (the keyboard is sampled once a frame). Across a pause, the pre-pause keyboard sample stays in force until the next frame: 382 ms and 312 ms, the pauses' lengths.
- **The failsafe within one RC frame:** passed (above).
- **Pause and restart in the log:** passed (above).
- **The post-flight summary from the log:**
  - `PostFlight.Read`, run by me on each player log, gives exactly the player's `[Flight] summary` lines. For example: "disarmed | time 0:06.3, distance 28 m | battery used 0.4 % | keyboard: never tune from this flight | seed 0xC9AB66FD26799F60 | log …".
  - The core's `replay --summary` agrees: KEYBOARD, development only, 6309 steps, the pause at step 4888, complete.
- **Replay bit for bit on the player's flights:** 3 of 3 match, recorded on Mono 6.13 in the development player and replayed on .NET 10: 6309 steps and 16 events, 999 and 8, 6301 and 16.
- **The keyboard only in development builds:**
  - The development player flies on the keyboard, and the header says "KEYBOARD, development only".
  - The review player (`-review`, not a development build) never flies without the radio. Both its flights wait.
  - `KeyboardFallback.Allowed` is `Debug.isDebugBuild`.
- **The built players:** `-developmentBuild` and `-review` both built with 0 errors and 0 warnings, and each copies the three data files. Both smoke tests: PASS, 0 checks failed, 0 errors logged (40 s and 26 s).
- **The shots:**
  - development: 12 shots (menu, setup, explore, rest / chase / air chase for each start, pause, two summaries)
  - review: 5 shots (menu, setup, explore, two waiting views)

  Findings 15, 16 and 18 come from them. "Looked at, and right" lists the rest.
- **Observation:** each first flight's opening steps run up to 17.6 ms late. They catch up, with hitches at steps 2 and 20, while the drone is disarmed on the ground, and the log replays. Watch it at G7, and do not count it against anything.

**Requirements:**
- **Met:**
  - INP-01 (the TX12 itself at 1.5b)
  - INP-03
  - INP-04 (the TX12's figures at 1.5b)
  - INP-06
  - INP-07
  - APP-03
- **Partly met:**
  - INP-02 (1.2b)
  - INP-05 (finding 14)
  - APP-02 (finding 18; the look is judged at G7, D-042)
  - APP-04 (findings 3 and 14)
  - APP-05 (finding 17)
  - PHB-07 (the gate: the pilot's TX12, and the world's contacts before G7)

## Hygiene
- **Commits:** 1.4b's 12 first-parent commits are all subject-only, with no body and no trailer. All are in `type(scope): summary` form or the `chore: merge …` form, at most 72 characters, by `Game Developer <gamedev@450fpv.local>` as author and committer alike.
- **Paths:**
  - 36 paths, none under `reference/` or `unity/{Library,Temp,Logs,UserSettings}/`
  - no images, logs or flight logs
  - no dates, places or coordinates in the added lines
  - `tools/assets/manifest.json` and `CREDITS.md` are unchanged
- **OPSEC:** the OSD strings are "Svinoriz 10 Opto" and "! SAFE !". The start points have invented names (`track`, `field`), in the map's own frame, with no georeference. The shots and logs stayed in my scratch folder and the local profile.
- **Engine-free:** `Fpv450.App.Session` gains `FlightWorlds` and `StartPoint.Stand`, and still builds as netstandard2.1 in `tools/inputcore`.
- **The carried history:** `c91d0cfa` (see "Merge condition").
- **Side effect of the smoke test:** it leaves its three flight logs in the pilot's flights folder (`%LOCALAPPDATA%\450fpv\flights`). They are keyboard-marked, but a `LogFolder` of its own, or deleting them, would keep the pilot's folder theirs.

## Commands and output (trimmed)

```
git worktree add .claude/worktrees/qa-input-2-1 origin/unity/game/p6-input-app--player --detach   HEAD 7aae8b83
git lfs checkout                                                    (no output)
power: AC (Win32_Battery status 2); free RAM before Unity 5.75 GB (waited while another agent's Unity and Blender ran)

dotnet test tools/inputtests                                        Passed 501/501 (36 s)
dotnet test tools/simtests                                          Passed 215/215 (1 m 50 s)
dotnet run -c Release --project tools/flightbench -- --golden
  free-body 0x50ED4B05A5D02F20 (matches); powered-flight 0x3F13A5CF5DB7DAFD (matches);
  contact-flight 0x3EE12C8D692BADF3 (matches), 5 impacts; tumble 0xE3178EE7FF322698 (matches): PASS
dotnet test tools/inputtests --filter <the 8 timing tests> -v detailed      12/12
  30 fps: 1251 steps, 618 reports and 1 loss; 60 fps 1251; 240 fps 1252; frame-bound control: 780 of 1200 stale
  FAILSAFE: lost at host 1234567 us; step 736 disarmed | lost 1271000, disarmed at the step at 1274000
  latency: first step p50 0.477, p99 0.561 ms; motor p50 6.453, p99 8.460 ms (mean 7.373); intervals p99 2.200, max 2.176
  E3a/E3b/E3f 1,2,3 ms: lost 1471000 us; end at step 345; failsafe
  on the flight thread: 886 steps in 885 ms flown, a pause of 200 ms, 1 hitch
dotnet test tools/inputtests --filter <14 scenario tests> -v detailed      14/14
  crash: coil hit R01_A (steel), an impact at 4.3 m/s; upside down 0:01.0; battery cut-off 0:03.2
  legs on 0.2530 -> 0.2516 m, legs off 0.2530 -> 0.2526 m, 8 touching pairs

Unity -batchmode -nographics -runTests -testPlatform EditMode (fresh Library)   exit 2, 272 s: 882/883,
  failed Fpv450.Feed.Tests.FpvCameraTests.EveryRendererCarriesTheFeed (High: shader not the same instance)
Unity -batchmode -nographics -runTests -testPlatform EditMode (rerun)           exit 0, 153 s: 883/883, 0 "error CS"
  Feed.RigTests 5, Feed.Tests 37, Sim.Tests 215, Tests.App 4, Tests.EditMode 121, Tests.Radio 482, Tests.Session 19
Unity -batchmode -quit -executeMethod Fpv450.Build.Windows -developmentBuild    exit 0, 320 s
  [Build] Windows: Succeeded, 0 errors, 0 warnings, 501081977 bytes; drone.json, physics_materials.json, placements.json copied
Unity -batchmode -quit -executeMethod Fpv450.Build.Windows -review              exit 0, 68 s
  [Build] Windows: Succeeded, 0 errors, 0 warnings, 437655865 bytes; the same three files
Builds/Windows-Development/450fpv.exe -smoketest -windowed -smokeshots <scratch> -logFile -   exit 0, 40 s
  [Smoke] PASS: 0 checks failed, 0 errors logged  (track: rest 0.252 m, climbed 11.17 m, paused at step 4887;
  field: restarted to a new seed and log, rest 0.253 m, climbed 11.72 m; both "the summary from the log: disarmed")
Builds/Windows-Review/450fpv.exe -smoketest -windowed -smokeshots <scratch> -logFile -        exit 0, 26 s
  [Smoke] PASS: 0 checks failed, 0 errors logged  (Explore loads; both flights wait for the radio)
dotnet run -c Release --project tools/replay -- <each player log>
  flight-c9ab66fd26799f60-1: match: 6309 steps and 16 events, bit for bit; exit 0 (Match)  (Mono 6.13 -> .NET 10)
  flight-7ac386c74d391e0c-1: match: 999 steps and 8 events, bit for bit; exit 0 (Match)
  flight-1a59f3c7c5cf423a-1: match: 6301 steps and 16 events, bit for bit; exit 0 (Match)
dotnet run -c Release --project tools/replay -- --summary flight-c9ab66fd26799f60-1
  input: KEYBOARD, development only: never tune from this flight; 6309 step records, complete;
  t = 4.888 s, step 4888: PAUSED for 374484 us
QA probe (scratch, not committed)
  Q1 30 fps: 0 of 2403 differ, control 1201; failsafe disarmed 2.602 ms after the loss; replay Match
  Q1 60 fps + 300 ms pause: 0 of 2102 differ, control 1051; failsafe 1.752 ms; replay Match
  Q1 240 fps: 0 of 2702 differ, control 1201; replay Match (not armed: the stream's throttle was at 5 % at the switch)
  Q2 Restart without a radio: InvalidOperationException: no radio is connected; end Pilot; 0 failsafes logged  (finding 14)
  player logs: 0 steps use a future sample; PostFlight lines equal the player's
shots: setup labels 2.25:1, summary lines 2.18:1, values 12.2:1; Muted (0.58,0.62,0.67)^2.2 = (77,89,106), seen (75,87,103)
python tools/hygiene/precommit.py --tree HEAD                       2216 files scanned, 0 findings (exit 0)
openspec validate p6-input-app --strict                             Change 'p6-input-app' is valid
python tools/assets/fetch.py --verify                               OK
```
