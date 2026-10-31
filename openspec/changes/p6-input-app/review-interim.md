# Interim QA review: p6-input-app (1.1, 1.2, 1.3, 1.4a, 1.5)

**Verdict: blocking.** One blocking finding (1): a radio lost during a pause and plugged back in before Resume leaves no failsafe in the log, and the summary blames the pilot ("disarmed"). Everything else I ran is green: every in-scope scenario, my own flights replayed bit for bit (failsafe and pause), the four goldens equal `unity/main`'s on .NET and Mono, a format-5 log is refused, and hygiene is clean. Findings 2–7 are not blocking; 2 should be fixed before G7.

**Scope:** `unity/game/p6-input-app` at `b3dfff4` (`unity/main` `7156dde` merged in). Out of scope: 1.4b (screens, player), 1.2b and 1.5b (the pilot's TX12 at G7). No TX12 here and no GPU (D-046): .NET, and Unity `-nographics` EditMode.

**How I checked:** the owner's tests rerun, then a scratch probe of my own (never committed) on the public APIs: my radio, my clock, my stick scripts, my EdgeTX model and pilot, my Betaflight formulas. Every replay is the core's `FlightReplay` on the session's own log file.

## Findings

1. **Blocking: a loss during a pause is not logged, and the end is called a disarm** (INP-05; owner: game developer).
   - While paused, the frame drains the inbox (`Scripts/App/Session/FlightSession.cs:258-261`). When the radio comes back before Resume, its reports supersede the failsafe sample, so no step record carries `Failsafe` (`FlightLogReader.cs:241`), and `FlightEndRule` (`Scripts/App/Session/FlightEnd.cs:80`) sees the held switch's disarm with a normal stick in force: `Disarm`.
   - Probe E3b: armed, throttle 0.3, paused, cable pulled (host 1 471 000 µs), plugged back in with the switch on 200 ms later, Resume. Session end `Disarm` at step 345, **0 failsafes in the log**, summary "disarmed". E3a, the same without the replug: `Failsafe`, logged once. The log replays in both cases.
   - The drone does disarm (the switch is held off until seen off), so it is safe. The log and the summary are wrong, and the "bug or pilot error" verdict reads them.
   - **Fix (owner's choice):** keep a failsafe seen while paused in force for the first RC frame after Resume, and/or have the end rule call it a failsafe when one was in force since the last armed RC frame. If a log event is chosen, add it now, before format 6 reaches `unity/main`. Add E3b's sequence as a test.
2. **Not blocking (fix before G7): a slow pilot gets past the TX12 pitfall check** (INP-02; owner: game developer).
   - The "jumps" rule times the 10–90 % transit against 0.7 s (`CalibrationWizard.cs:51`, `:276`). On a 79 / 1 radio span the HID value crosses 10–90 % in about 10 % of the stick's sweep, so any sweep of about 7 s or more passes.
   - Probe E9 (my EdgeTX model, `out = d·1024 / max(100, span)`, and my pilot, 2.5× slower after "Too fast"): first sweep 2.0 s, caught; 2.8 s, one axis of two caught; **3.0 s (then 7.5 s), both missed, the profile saved with 0 findings**.
   - The scenario passes at the instructed speed (E8). But fc-profiles records this fault on the pilot's own radio, so a careful first calibration at G7 could save roll and pitch as switches.
   - **Suggestion:** add a signal that does not depend on speed, for example the centre step's rest noise (EdgeTX's ×1024/100 gain amplifies it about 8× against a healthy ×1024/800 axis), or the transit against the throttle's and yaw's. Retune before G7, not during 1.2b.
3. **Not blocking: crash objects are named only when they are runtime objects** (owners: game developer with world builder, for 1.4b and p6-test-range).
   - `PostFlight.cs:69` resolves the object through `h.RuntimeObjectName`. The header holds no world object names, so a crash into a placed test-range car would read "object N".
   - The "crash into the car" scenario passed with R01_A placed as a runtime object (`SessionTests.cs:291`). It cannot be run against a world object yet, because FloorWorld's only objects are the runtime ones and they are reset each flight (probe E10).
   - Decide where world object names come from: a header entry (best before format 6 lands) or the world, opened by `WorldId`.
4. **Not blocking: spec drift** (owner: orchestrator).
   - app-flow `spec.md:8,10,12` and `design.md:5,28` still load the G1 gallery from `maps/gallery-g1/stations.json`. The proposal moved the session to the test range (D-044).
   - app-flow `spec.md:4` says "with no other settings" and leaves out APP-03's start point, which `FlightChoices` has.
   - `tasks.md:101` ("tx-input asks for one step … Open, for physics") is stale since `313350b` made the scenario one RC frame.
5. **Not blocking, docs:** `LogHeader.cs:195-196` gives a step's stick age as `StartHostUs + k·1000 − HostUs` and leaves out the pauses. `HitchCause.Pause` (`StepTrace.cs:123-125`) adds them. One line.
6. **Not blocking, nit:** the percentiles are upper bin edges, so they can exceed the maximum: "p99 2.200, max 2.136 ms" (my `radioprobe` run, and the summary's `InputLine`, `FlightLogReader.cs:400`). Cap them at the maximum, or say "bin".
7. **Not blocking, test sizing:**
   - `LatencyTests.cs:59` (first step within 2 ms) plays the recording at its stamps (`LeadUs` 0) on real threads, so an input thread more than about 1 ms late under load fails it.
   - It passed in every run here, including with Unity compiling alongside. The false-failure rate is not stated.
   - The render-rate test is not exposed: it hands reports over 10 ms early.

## Scenarios

| scenario | result | evidence |
|---|---|---|
| tx-input: render rate does not touch the sticks | passed | Owner's test: 30 / 60 / 240 fps identical to the rule; the frame-bound control has 777 of 1200 steps stale. **Probe E5** through `FlightSession`'s own threads (a recorded stream, `LeadUs` 10 ms, loss at 2.6 s) at 30, 60 (with a 300 ms pause) and 240 fps: 0 of 2503 / 2203 / 2503 steps differ from "the latest report at or before the step's host time". The same rule 1 ms early differs at 1251 / 1101 / 1251 steps, so the check can fail. 0 dropped, each log replays bit for bit |
| tx-input: latency is measured | passed (synthetic); TX12 not testable (1.5b) | Owner's test, identical on .NET 10 and Mono: first step p50 0.477 / p99 0.561 ms, motor p50 6.453 / p99 8.460 ms, both in the header and the summary. My recording through `radioprobe --latency --from`: first step p50 0.909 / p99 0.999, motor p50 6.898 / p99 8.894. The breakdown 0.910 + 2.000 + 1.563 + 3.336 = 7.809 equals the mean |
| tx-input: the TX12 pitfall is caught | passed at the instructed speed | Owner's two tests. **Probe E8**: 79 / 1 and 85 / 0 named "jumps from end to end", nothing saved; healthy spans pass (AETR, 0–2047); a 30 % radio yaw expo is named; a human's eased sweep raises no false curve. Finding 2 covers slower sweeps |
| tx-input: under two minutes, no keyboard | partly; real TX12 not testable (1.2b) | synthetic radio: 19.0 s (owner), 20.0 s (mine) |
| tx-input: the defaults are the sim default column | passed | E6: a fresh `FcSettingsStore.Load` gives 55 / 225 / 0.34, 85 / 260 / 0.15, thr 50 / 10, deadband 2 / 2, smoothing 50, f 50 / 50 / 0, airmode on, idle 550, which is fc-profiles' column. **Probe E7**: the preview against Betaflight 4.5's ACTUAL formula, written out by me: 0.0 °/s over 3 × 101 sticks, and the throttle within 2.8e-16 (integer `lookupThrottleRC`, double interpolation). The formula without the deadband differs by 2.5–15 °/s |
| tx-input: pulling the cable mid-flight | passed; partly (finding 1) | E1: lost at host 3 571 000 µs, disarmed at the step at 3 574 000 µs (3 ms, under 4 ms), logged once with its stamp. Owner's test: 1 271 000 to 1 274 000 µs |
| app-flow: into the air in a few steps | not testable | screens are 1.4b |
| app-flow: pause and restart | passed | E1: no step while paused, the pause logged at step 1542 (B 700 000 µs), the flight replays. E5 at 60 fps: 299 822 µs, every later stick within the rule. Owner's test: a new seed, a new log, the same start, the switch to be cycled |
| app-flow: a crash into the car | partly (finding 3) | "crash: coil hit R01_A (steel), an impact at 4.3 m/s" from the log, with R01_A as a runtime object |
| app-flow: settings survive a restart | passed (quality preset: 1.4b) | E6: `roll_srate` 30, 1.7 kg, legs off, Severe, Rain, 21.5 h restored through `SessionInputs.FromProfile`. Four corrupt kinds (cut, binary, out of range, unknown name) in all three files: 3 problems named, the defaults used |
| APP-04 ends and the summary | passed | Owner's tests: crash, upside down (1.0 s), battery cut-off, disarm and failsafe. E1's summary from the log: 1845 steps read 0:01.8, failsafe at 0:01.8, seed `0x…0A11`, battery 0.2 %. Owner's distance against the published poses: within 1e-9 m |

**Requirements:**
- Met: INP-01 (the TX12 itself at 1.5b), INP-03, INP-04 (the TX12's figures at 1.5b), INP-06, INP-07.
- Partly met:
  - INP-02 (finding 2; 1.2b)
  - INP-05 (finding 1)
  - APP-04 (finding 1's end, finding 3's names)
  - APP-05 (the quality preset is 1.4b)
- In logic only, screens at 1.4b: APP-03.
- Not testable: APP-02.

## The physics-owned files, read as the physics engineer

- **`StickSample.Failsafe`:** a new bool, which the core never reads (it reads `Arm`).
  - Flags bit 3, apart from the crash's bit 2: encoded at `LogFormat.cs:274`, decoded at `:366` and `:437`.
  - `FlightLogTests` round-trips both bits set and apart. The record stays 1296 B.
- **The tag 15 input entry:** written at `LogHeader.cs:632`, read at `:838`.
  - Bounded by its entry's end (`:862`), and a 1.1 entry stops before `StartHostUs`.
  - The device path is never logged (LOG-05): `WinHidInfo.Path`.
- **`HitchCause.Pause`:**
  - written by `RecordPause` on the frame thread, only while the flight thread is joined (`FlightScheduler.Stop` joins)
  - placed before the step it delayed
  - skipped by replay like every hitch (`FlightReplay.cs:141`)
- **The versions:** `LogFormat.Version` 6 and `ModelVersion` 6, the same as `unity/main`.
  - Probe E4 patched a log's prologue to 5. The reader, the replay (`Unreadable`) and the summary all refuse it: "the log's format is version 5; this build reads version 6".
  - I did not write a log on a `unity/main` build. The refusal is the prologue's version check (`LogHeader.cs:687`).
- **The goldens equal `unity/main`'s:**
  - powered `0x3F13A5CF5DB7DAFD`, contact `0x3EE12C8D692BADF3`, tumble `0xE3178EE7FF322698`, free body `0x50ED4B05A5D02F20` (flightbench, Release)
  - the first three also on Mono, in Unity
  - The golden constants and `tools/{simtests,flightbench,replay}` are unchanged against `unity/main`. Nothing was re-recorded.
- **Steady state:** probe E11 measured 0 B allocated over 10 000 reports through `RadioReader.Pump` (raw and calibrated maps), and 0 B over 3000 frames of Pump, Tick, steps, the log ring and the end rule.

## Hygiene

- **Commits:** 31 since `unity/main`, all subject-only (no body, no trailer). 28 are by `Game Developer <gamedev@450fpv.local>` and 3 by `Orchestrator <orchestrator@450fpv.local>`, author and committer alike. Every subject is in `type(scope): summary` form and 72 characters or fewer.
- **Paths:** nothing from `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/`, and no logs or recordings.
- **Copied code:** none found.
  - No GPL header in any new `.cs` file.
  - No `#define`, `->`, C integer type, or EdgeTX or Betaflight identifier outside comments. The `f` literals are Unity `Color` arguments only.
  - EdgeTX's and Betaflight's formulas are cited and re-expressed (my E7 and E8 checked their results).
- **Licences:** `tools/assets/manifest.json` and `CREDITS.md` are unchanged. The NuGet pins in `tools/inputtests` (17.14.1, 3.14.0, 5.0.0) are simtests' own.
- **Engine-free:** `Fpv450.Input.Radio` and `Fpv450.App.Session` have `noEngineReferences: true` and build as netstandard2.1 in `tools/inputcore`.
- **The HID reader:** overlapped reads go into unmanaged buffers, and `CancelIoEx` plus a wait comes before freeing them (`WinHid.cs:313-326`).

## Commands and output (trimmed)

```
git lfs checkout                                                    (no output)
free RAM before Unity: 4.22 GB
dotnet test tools/simtests                                          Passed 214/214 (1 m 19 s)
dotnet test tools/inputtests                                        Passed 32/32 (25 s)
  render rate: 30 fps 42 frames, 1253 steps; 60 fps 84, 1252; 240 fps 336, 1254; 618 reports and 1 loss each
  control: 30 fps frame-bound, 777 of 1200 steps had a stale stick
  FAILSAFE: the radio was lost at host 1234567 us; step 736 (t = 0.736 s) disarmed
  latency: first step p50 0.477, p99 0.561 ms; motor p50 6.453, p99 8.460 ms (mean 7.373)
  lost at host 1271000 us; disarmed at the step at host 1274000 us; failsafe: the radio was lost, and the drone disarmed
  on the flight thread: 889 steps in 889 ms flown, a pause of 200 ms, 0 hitches
dotnet run -c Release --project tools/flightbench -- --golden
  free-body 0x50ED4B05A5D02F20 (matches); powered-flight 0x3F13A5CF5DB7DAFD (matches);
  contact-flight 0x3EE12C8D692BADF3 (matches), 5 impacts; tumble 0xE3178EE7FF322698 (matches): PASS
Unity -batchmode -nographics -runTests -testPlatform EditMode -testResults Logs/qa-editmode.xml
  exit 0, 106 s; 0 "error CS"; testcasecount=352 passed=352 failed=0
  Fpv450.Sim.Tests 214, Fpv450.Tests.EditMode 106, Fpv450.Tests.Radio 20, Fpv450.Tests.Session 12; Mono 6.13.0 X64
  golden: 0x3F13A5CF5DB7DAFD, 0x3EE12C8D692BADF3, 0xE3178EE7FF322698; latency figures as on .NET
dotnet run -c Release --project tools/radioprobe -- --latency --from <QA's 15 s recording>
  10729 steps, 6 hitches, 0 dropped; 120 of 120 timed; 488 intervals, mean 2.000 ms
  first step p50 0.909, p99 0.999 ms; motor p50 6.898, p99 8.894 ms; RC frame 2.000 + smoothing 1.563 + ESC 3.336 (means)
QA probe (scratch, not committed): 52 PASS, 2 FAIL (E3b, finding 1)
  E1 replay: Match, 1845 steps and 24 events, bit for bit (failsafe and a 700 ms pause, on the stand, Windy, 1.5 kg)
  E3a replay Match; E3b: end Disarm, 0 failsafes in the log
  E4: FlightLogReader refuses it: the log's format is version 5; this build reads version 6
  E5 30/60/240 fps: 0 of 2503 / 2203 / 2503 differ; control 1251 / 1101 / 1251; replays Match
  E9: pitfall missed with a 3.0 s first sweep (7.5 s after "Too fast")
  E11: 0 B
python tools/hygiene/precommit.py --tree HEAD                       1609 files scanned, 0 findings (exit 0)
openspec validate p6-input-app --strict                             Change 'p6-input-app' is valid
python tools/assets/fetch.py --verify                               OK
```

## Re-check of finding 1 (at `6e84ee4`)

**Verdict for the interim scope: green.** Finding 1 is fixed. A loss during a pause, replugged before Resume or not, is now the failsafe in force for exactly one RC frame after Resume. It is logged once with the loss's stamp, the end is `Failsafe` and the summary says so. One new finding (8) is not blocking. Findings 2–7 were not re-checked and stand.

**Scope:** `99e7db3` (`StickInbox.Drain` and `Hold`), `b63af4f` (the session's pause and resume, the two E3 tests), `8b2919c` (1.4a's note), `6e84ee4` (the tx-input scenario "Pulling the cable during a pause"). No GPU (D-046).

8. **Not blocking: a second pause inside the first RC frame after Resume drops the hold, and the end is a disarm again** (INP-05, APP-04; owner: game developer).
   - The hold ends at a host time (`FlightSession.cs:311`). A pause moves every later step's host time past it, and `Pause` clears the kept loss (`:287`).
   - Probe E3f: E3b, paused again 1, 2 or 3 ms after Resume (k mod 4 = 1, so the frame is the 4th step), resumed 50 ms later. The records from 342 carry the failsafe up to the re-pause, then the replugged radio's sample. The reader's held-off switch disarms at 345: end **`Disarm`**, summary "disarmed | … | failsafe at 0:00.3". The log replays.
   - It is safe, and out of a pilot's reach: Pause and Resume come once a frame from the pilot's input (`FlightSessionHost.cs:77-78`), and the window is under 4 ms. It is outside the scenario's WHEN.
   - **Suggestion:** count the hold in steps, or have `Pause` keep a loss whose RC frame has not run. Add E3f as a test, with 1.4b's pause controls, before G7.

| scenario | result | evidence |
|---|---|---|
| tx-input: pulling the cable during a pause (new) | passed | the owner's two tests; my E3a–E3e below |
| tx-input: pulling the cable mid-flight | passed | the owner's test: lost at 1 271 000 µs, disarmed at the step at 1 274 000 µs |

**Requirements:** INP-05 met (finding 8 lies outside the scenario). APP-04: finding 1's end is fixed; still partly met for finding 3's names.

**My probes** (scratch, not committed; every one replays its session's own log, Match):

| probe | sequence | result |
|---|---|---|
| E3a | armed at 0.3 for 300 ms, paused, lost, Resume | `Failsafe` at step 345; logged at 342 with 1 471 000 µs |
| E3b | E3a, replugged with the switch on 200 ms before Resume | the same (was `Disarm`, 0 failsafes, at `b3dfff4`) |
| E3c | two losses in one pause (1 471 000 and 1 771 000 µs), each replugged | the same, with the first loss's stamp; the reader counts 2 losses, the log one failsafe |
| E3d | lost in the last paused frame (its Pump and Tick), Resume at that host time | the same |
| E3d′ | the loss reaches the inbox after the last paused frame (no Tick), Resume | the same, without a hold: the first step takes it |
| E3e | E3b paused after steps 341, 342, 343, 344 (k mod 4 = 1, 2, 3, 0) | the end at 345 each time (k + 4, 3, 2, 1), the one frame step; logged at k + 1 with the loss's stamp |
| E3f | finding 8 | `Disarm` |

**The fix read as the physics engineer** (`StickInbox` is physics-owned; the game developer changed it):
- **Without a hold, `Consume` is the old rule.** `_heldUntilUs` starts at `long.MinValue` (`StickInbox.cs:19`), so the test at `:56` never holds. `Drain` is the old loop plus a flag that touches neither `_inForce` nor `_read`.
  - E12c, unity/main's `StickInbox` (copied, renamed) against the branch's on one random stream: 0 of 206 864 steps differ, and 0 writes differ, 0 dropped. The stream has failsafes 1 in 30, reports 0.3–3.7 ms apart and up to 3 ms ahead, and paused stretches through the old `Consume` against the new `Drain`.
  - E12, against my own queue of the rule: 0 of 300 000 steps differ. With a hold, 4 steps are held and the rest are identical.
  - E12b, `Drain` over 100 000 paused frames: it returns true exactly when a report it takes is a failsafe, and gives the first. The sample in force after it is the latest.
- **Nothing allocates.** E13: 0 B for 20 000 TryWrite, Consume and Drain with the hold in force, and 0 B with it expired. 0 B for 3000 paused frames (Pump, Tick's Drain). E11 again: 0 B over 3000 flying frames with their steps.
- **The hold is one RC frame.** `RcFrameSteps` = 16 / 4 = 4 (`FlightSession.cs:127`). It covers the steps timed before `start + (k + 4)·1 ms`, which are the pre-steps k to k + 3 (`StepHostUs`, `FlightScheduler.cs:49`). A frame is taken at `loop % 16 == 0`, with `loop = Step·4 + j` (`FlightCore.cs:261-265`). So exactly one of any four steps takes a frame, at every phase (E3e). The steps' times are the scheduler's own, so a hitch does not shorten the hold; only a pause does (finding 8).
- **Threads:** `_held` and `_heldUntilUs` are the consumer's. `Hold` runs on the frame thread before `Step(start)` starts the flight thread. The paused `Drain` runs after `StopStepping` has joined it. The producer is untouched.
- Engine-free and without arithmetic. The core, log format 6 and the goldens are unchanged.

**Hygiene:** the four commits are subject-only with no trailer. Author and committer are `Game Developer <gamedev@450fpv.local>` (3) and `Orchestrator <orchestrator@450fpv.local>` (1). They touch six paths, none under `reference/`, `Library/`, `Temp/` or `Logs/`. `GoldenFlights.cs`, `FreeBodyCase.cs` and `tools/{simtests,flightbench,replay}` show 0 diff lines against `unity/main`.

```
git rev-parse --short HEAD                                          6e84ee4
dotnet test tools/inputtests                                        Passed 34/34 (32 s)
  E3a: paused after step 341, lost at host 1471000 us; failsafe: the radio was lost, and the drone disarmed | ... | failsafe at 0:00.3
  E3b: paused after step 341, lost at host 1471000 us; failsafe: the radio was lost, and the drone disarmed | ... | failsafe at 0:00.3
  lost at host 1271000 us; disarmed at the step at host 1274000 us; failsafe: the radio was lost, and the drone disarmed
dotnet test tools/simtests                                          Passed 215/215 (1 m 27 s)
dotnet run -c Release --project tools/flightbench -- --golden
  free-body 0x50ED4B05A5D02F20 (matches); powered-flight 0x3F13A5CF5DB7DAFD (matches);
  contact-flight 0x3EE12C8D692BADF3 (matches), 5 impacts; tumble 0xE3178EE7FF322698 (matches): PASS
git lfs checkout                                                    (no output)
free RAM before Unity: 4.86 GB
Unity -batchmode -nographics -runTests -testPlatform EditMode -testResults Logs/qa-recheck-editmode.xml
  exit 0, 109 s; 0 "error CS"; testcasecount=355 passed=355 failed=0
  Fpv450.Sim.Tests 215, Fpv450.Tests.EditMode 106, Fpv450.Tests.Radio 20, Fpv450.Tests.Session 14
  E3a and E3b passed, the same lines; replay: Match, 345 steps match: 345 steps and 16 events, bit for bit
  golden on Mono: 0x3F13A5CF5DB7DAFD, 0x3EE12C8D692BADF3, 0xE3178EE7FF322698
QA probe e3 (the interim's E3, unchanged)                           ALL PASS: E3a and E3b Failsafe at step 345, 1 failsafe, Match
QA probe e3x                                                        3 FAILED: E3f 1, 2, 3 ms (finding 8); every other check PASS
QA probe e12, e13, e11                                              ALL PASS (0 differ; 0 B)
python tools/hygiene/precommit.py --tree HEAD                       1610 files scanned, 0 findings (exit 0)
openspec validate p6-input-app --strict                             Change 'p6-input-app' is valid
```

## Re-check of findings 2 and 8 (at `7522d68`)

**Verdict for the interim scope: green.** Finding 8 is fixed. Finding 2 is fixed for the radio the new rule assumes, at every sweep speed I tried. The rule leaves a gap (2a) and adds a false refusal (9, new), both not blocking and both to settle before G7. Findings 3–7 were not re-checked and stand.

**Scope:** `9d9f8c6` (`PitfallJitter`, the E9 tests), `ab1da12` (`_heldToStep`, the E3f tests), `7522d68` (design 4's third rule, the amended "Pulling the cable during a pause"). No GPU (D-046).

**Finding 2, the rule:** `PitfallJitter` = 1024 / 100 / 2048, and `(int)(PitfallJitter · 2047)` = 10 HID counts. EdgeTX's integer division gives 10 counts per ADC count at its floor of 100, so one count of rest jitter spreads exactly 10, which is caught with no margin (E9v, 0..1).
- I checked the formula against EdgeTX's source. At 2.8.0 (the pilot's, fc-profiles) `getADC` filters the raw value (`hal/adc_driver.cpp`), and the calibration (`evalInputs`, as the owner read it) divides by `max(100, span)` after it. On `main`, `apply_calibration` runs before the filter. The gain is the same either way.

**My probes** (scratch, not committed). E9v uses my EdgeTX model with C's integer division, a 2 ms report and the 2.5× slower repeat:

| probe | radio, rest jitter in ADC counts | sweep | result |
|---|---|---|---|
| E9 (as before) | pitfall 79 / 1, 85 / 0, ±1 | 2.0, 2.8, 3.0, 4.0 s | all caught, roll and pitch, 18.3–24.6 s (was: one axis missed at 2.8 s, both from 3.0 s) |
| E8 (as before) | healthy; pitfall; eased; yaw expo 30 % | 2 s | all PASS; the pitfall is now named by the jitter message |
| E9v | pitfall, 0..1, ±1, −1..2, ±2 | 2, 3, 8, 12 s | caught, rest spread 10–40 |
| E9v | pitfall, none (an ADC still at rest) | 2 s | caught by the jump rule |
| E9v | pitfall, none | 3 s (7.5 s), 8 s, 12 s | **saved with 0 findings** (2a) |
| E9v | pitfall, ±3 | any | stuck at Centre for 180 s: the spread of 61 never fits the 2 % still band. Nothing saved, nothing said (pre-existing) |
| E9v | healthy 683 / 800 / 903, ±1 to ±3 | 3, 8 s | saved, spread 2–8 |
| E9v | healthy 683 and 800, ±4 (8 peak to peak); 903, ±5 | 3, 8 s | **refused**: yaw, pitch and roll "jump at the slightest touch" (9) |
| E9v | healthy 800, ±1, a thumb left on the sticks during Centre | 2 s | ±0.2 % of the half travel saved (spread 6); **±0.4 % (±3 ADC counts) or more refused** (9) |
| E9v | a partial radio calibration, span 120–200, ±1 | 2, 3 s | caught. Spans 250 and 300 are saved (a gain of 3–4, outside fc-profiles' pitfall) |

**Could a healthy radio be blocked? Yes** (finding 9):
- The derivation's "7 counts" holds: 8 counts peak to peak at spans 683 and 800, 10 at 903.
- The spread is a max − min over about 500 reports (1 s), so Gaussian noise of σ ≈ 1.3 ADC counts reaches 8 counts peak to peak (500 draws span about 6.1σ).
- EdgeTX's filter averages raw changes under 10 counts, so this is likelier with the radio's or the model's jitter filter off.
- A thumb's wobble passes the filter either way. The message then sends the pilot to recalibrate the radio, which fixes nothing.
- Before `9d9f8c6`, both cases only widened the deadband.

**Finding 2a, the gap:**
- At 2.8 the filter runs on the raw value, before the ×10.24, so it can hold a pitfall axis still at rest. Its own comment reports 1 count left with the plain average and less with the doubled input, without a figure.
- If a pitfall axis is still at rest on the pilot's TX12, a slow pilot passes again. Design 4 names this, for 1.2b.

**Suggestion (game developer, before G7):** test the sweep's smallest step: the least non-zero change between two consecutive reports within 10–90 %, which is the radio's gain per ADC count.
- In E9v it was 10 on the pitfall in every case, including no jitter, and 1 on every healthy radio, at every jitter and every wobble. Spans 120–300 gave 8 to 3.
- A threshold of about 4 is independent of speed, jitter, filter and thumbs. Use it in place of the rest rule, or ahead of it.
- Have 1.2b record each axis's rest spread and smallest step on the pilot's TX12, with its filter setting.
- If the rest rule stays, say "let go of the sticks" in its message.
- Nit: the comments say the throttle has the jump rule alone, but the code applies the rest rule to it too. It passes only because EdgeTX clamps at the end (a healthy ±6 gives the throttle a spread of 9).

**Finding 8:** fixed.
- `Pause` keeps the loss while `Step < _heldToStep`, and `Resume` keeps the first hold's end step and moves its host time with the new start. `Start` resets both.
- E3f at 1, 2 and 3 ms: `Failsafe` at step 345, logged at 342 with 1 471 000 µs, replayed (was `Disarm`).
- My variants, every log replayed with Match:

| probe | sequence | result |
|---|---|---|
| E3y | E3b at all four phases (k = 341–344), re-paused 1–4 times inside the hold: +0, +0/+0, +1/+1, +1/+2, +2/+1, +1/+1/+1, +0/+1/+1/+1, +3/+0 ms, 50 ms each | 32 of 32: `Failsafe` at 345, one failsafe logged at k + 1 with the loss's stamp, every pause logged (1–5). A re-pause after the frame is a no-op, since the flight has ended |
| E3h | E3b, re-paused at +1 ms, the cable pulled again and replugged in the re-pause | `Failsafe` at 345, one failsafe with the first loss's stamp; the reader counts 2 losses |
| E3i | a pause without a loss, Resume, re-paused 2 ms later, lost and replugged in that pause | a fresh hold: `Failsafe` at 345, logged at 344 with its own stamp |

**Scenarios:** "the TX12 pitfall is caught" passed, now also at 3–12 s sweeps (2a and 9 lie outside its WHEN). "Pulling the cable during a pause" (amended) passed.

**Requirements:** INP-05 met. INP-02 is still partly met (2a, 9; 1.2b).

**Hygiene:**
- The three commits are subject-only with no trailer. Author and committer are `Game Developer <gamedev@450fpv.local>` (2) and `Orchestrator <orchestrator@450fpv.local>` (1).
- They touch 7 paths, none under `reference/`, `Library/`, `Temp/` or `Logs/`.
- The sim package and `tools/{simtests,flightbench,replay}` show 0 diff lines against `ded61bb`.

```
git rev-parse --short HEAD                                          7522d68
dotnet test tools/inputtests                                        Passed 39/39 (25 s)
  E9 3 s then 7.5 s, E9 8 s: Passed
  E3a, E3b, E3f 1/2/3 ms: paused after step 341, lost at host 1471000 us; end at step 345; failsafe ... | failsafe at 0:00.3
dotnet test tools/simtests                                          Passed 215/215 (1 m 20 s)
dotnet run -c Release --project tools/flightbench -- --golden
  free-body 0x50ED4B05A5D02F20 (matches); powered-flight 0x3F13A5CF5DB7DAFD (matches);
  contact-flight 0x3EE12C8D692BADF3 (matches), 5 impacts; tumble 0xE3178EE7FF322698 (matches): PASS
git lfs checkout                                                    (no output)
free RAM before Unity: 4.65 GB
Unity -batchmode -nographics -runTests -testPlatform EditMode -testResults Logs/qa-recheck2-editmode.xml
  exit 0, 109 s; 0 "error CS"; testcasecount=360 passed=360 failed=0
  Fpv450.Sim.Tests 215, Fpv450.Tests.EditMode 106, Fpv450.Tests.Radio 22, Fpv450.Tests.Session 17
  golden on Mono: 0x3F13A5CF5DB7DAFD, 0x3EE12C8D692BADF3, 0xE3178EE7FF322698
QA probe e9                                                         ALL PASS: 2.0, 2.8, 3.0, 4.0 s each failed True, too-narrow 2
QA probe e8                                                         ALL PASS
QA probe e3x                                                        ALL PASS (74 checks; E3f 1, 2, 3 ms: Failsafe at 345)
QA probe e3y                                                        ALL PASS (236 checks: E3y 32 runs, E3h, E3i)
QA probe e9v                                                        pitfall with jitter: 24 of 24 nothing saved (4 stuck at Centre);
                                                                    without: 1 caught (2 s), 3 saved; healthy refused at 8–10 p-p
python tools/hygiene/precommit.py --tree HEAD                       1610 files scanned, 0 findings (exit 0)
openspec validate p6-input-app --strict                             Change 'p6-input-app' is valid
```

## Re-check of finding 9 and gap 2a (at `0da4c97`)

**Verdict for the interim scope: green.** Finding 9 and gap 2a are fixed for a radio that reports every 2 ms. The new rule adds finding 10: it assumes that 2 ms, and a slower radio is refused on a quick sweep. It is not blocking, but it must be settled before G7. Findings 3–7 were not re-checked and stand.

**Scope:** `b0e2386` (`PitfallStep`, `SmallestStep`, `Restless`, the new `CalibrationTests`), `0da4c97` (design 4's third rule, task 1.2's note and 1.2b). No GPU (D-046).

**The rule:** `(int)(PitfallStep · 2047)` = 5, against the pitfall's 10 (C's integer division). It applies only to sweeps the jump rule accepts (10–90 % in 0.7 s or more), on each side of the centre, and takes the larger of the two sides. The throttle is split at 1023.

**My probes** (scratch, not committed). E9w and E9x use my own EdgeTX model, C's integer division and `System.Random` jitter; the pilot is 2.5× slower after "Too fast". The smallest step is my probe's figure, over the wizard's band and split. 281 checks, 0 fail.

| probe | radio | jitter, sweep | result |
|---|---|---|---|
| E9w A | pitfall: two-sided 79 / 1 and 85 / 0, one-sided 79 / 800, throttle 79 / 1 | still, 0..1 cyclic and random, ±1, −1..2, ±2, ±3, ±5; 2, 3, 5, 7, 8, 12, 20 s | **168 of 168 not saved.** 133 named by the step rule (smallest step 10, every case), 14 by the jump rule, 21 (±3 and ±5) held at Centre with "Channel N keeps moving" (was: stuck with nothing said) |
| E9w B | healthy 683 / 800 / 903 | still and ±1 to ±5; 0.9, 1.0, 1.5, 2, 3, 8 s | **108 of 108 saved,** no too-narrow finding. Smallest step 1 in 98, 2–4 in the rest |
| E9w C | healthy 800, ±1, a thumb on the sticks at Centre | ±0.2 % to ±1.5 % of the half travel | saved (rest spread up to 33). At ±3 % it holds at Centre with "Channel 4 keeps moving", the right prompt |
| E9w E | a partial radio calibration, roll and pitch | still and ±1; 2, 3, 8 s | 200, 204 and **205** refused (smallest 5); 210, 250 and 300 saved (smallest 4, 4, 3) |

**The margin at 2 ms:**
- The fastest first sweep the jump rule accepts is a 0.875 s linear one. With a still ADC, its per-report step had a mean of 4.46 and a median of 4. The smallest was 3–4 against the threshold of 5, so the margin is one count. With ±1 jitter the smallest was 1–2.
- That margin holds by construction, not by luck. A transit of 0.7 s at 2 ms is 350 reports over about 1 638 counts, a mean of 4.68, so a steady sweep must have a step of 4 or less.
- The same count breaks above a 2.14 ms interval (1 638 / 5 × interval < 0.7 s).
- Nit: the design says the rule catches spans "up to 204". In my model 205 is caught too (1024 · d / 205 steps by 5, except once in about 205 counts). It is harmless.

**Finding 10 (new, not blocking, before G7): a radio slower than 2 ms is refused on a quick, healthy sweep.** E9w F and E9x tested healthy 683 / 800 / 903 radios, still and ±1:

| reports | linear sweeps 0.9–3 s refused | eased sweeps 1.0–2.5 s refused |
|---|---|---|
| every 2 ms | 0 of 48 | 0 of 36 |
| every 3 ms | 7 of 48 (0.9–1.2 s) | 1 of 36 |
| every 4 ms | 18 of 48 (0.9–1.5 s; the throttle at 1.5 s) | 9 of 36 (1.2–1.5 s) |
| 1 ms, output updated every 2 ms | 0 of 48 | not run |
| 1 ms, output updated every 4 ms | the same 18 of 48 | not run |

- The refused axes are named "moves in jumps of 5–9 … recalibrate the radio's sticks", the same misdirection as finding 9. The prompt asks for about 2 s, and every 2 s sweep passed. The pitfall was caught at every interval.
- The TX12's interval is not measured (1.5b, at G7). Measuring it there finds this only after the pilot has been refused once.
- What counts is how often the radio updates its output, not the USB interval itself (the 1 ms rows).
- **Suggestion (game developer):** gate the refusal by the radio's updates, not by the sweep's time. When the step rule finds a coarse sweep whose 10–90 % band holds fewer than band / (`PitfallStep` − 1) updates (about 410), say "Too fast" instead of refusing.
  - Count the updates from the measured interval, not from the reports: a 1 ms stream can repeat a 4 ms output.
  - At 2 ms this never fires on a healthy radio (pigeonhole), and it delays the pitfall by one slower repeat at most.
  - Add a 4 ms `Tx12Radio` case to `CalibrationTests`.

**Scenarios:** "The TX12 pitfall is caught" passed: both axes named at every sweep and jitter, and spans 683–903 passed at 2 ms. At 3–4 ms the "683–903 pass" half fails on quick sweeps (finding 10, outside the scenario's recorded sweep).

**Requirements:** INP-05 met. INP-02 is still partly met (finding 10; 1.2b).

**Hygiene:**
- Both commits are subject-only with no trailer, by `Game Developer <gamedev@450fpv.local>`.
- They touch 5 paths, none under `reference/`, `Library/`, `Temp/` or `Logs/`.
- The sim package and `tools/{simtests,flightbench,replay}` show 0 diff lines against `f47ff4c`.

```
git rev-parse --short HEAD                                          0da4c97
dotnet test tools/inputtests                                        Passed 97/97 (24 s)
dotnet test tools/simtests                                          Passed 215/215 (1 m 18 s)
dotnet run -c Release --project tools/flightbench -- --golden
  free-body 0x50ED4B05A5D02F20 (matches); powered-flight 0x3F13A5CF5DB7DAFD (matches);
  contact-flight 0x3EE12C8D692BADF3 (matches), 5 impacts; tumble 0xE3178EE7FF322698 (matches): PASS
free RAM before Unity: 4.97 GB; Unity Hub running
Unity -batchmode -nographics -runTests -testPlatform EditMode -testResults Logs/editmode-recheck9.xml
  exit 0; 0 "error CS"; testcasecount=418 passed=418 failed=0 (84.8 s)
  Fpv450.Sim.Tests 215, Fpv450.Tests.EditMode 106, Fpv450.Tests.Radio 80, Fpv450.Tests.Session 17
  golden on Mono: 0x3F13A5CF5DB7DAFD, 0x3EE12C8D692BADF3, 0xE3178EE7FF322698
QA probe e9w                                                        281 PASS, 0 FAIL; A 168 of 168 pitfalls not saved
                                                                    (21 held at Centre, 21 named); B 108 of 108 healthy saved;
                                                                    F 3 ms 7 of 48, 4 ms 18 of 48 healthy refused (finding 10)
QA probe e9x (eased)                                                2 ms 0 of 36, 3 ms 1 of 36, 4 ms 9 of 36 healthy refused
python tools/hygiene/precommit.py --tree HEAD                       1610 files scanned, 0 findings (exit 0)
openspec validate p6-input-app --strict                             Change 'p6-input-app' is valid
```

## Re-check of finding 10 (at `f57ebc6`)

**Verdict for the interim scope: green.** Finding 10 is fixed: no healthy sweep is refused at 2, 3 or 4 ms or 1 ms over 4 ms, and no pitfall is saved. The guard adds finding 11: a pilot who does not keep slowing down is told "Still too fast" forever, and the pitfall is never named. That includes a one-sided pitfall swept at the prompt's own "about 2 seconds", a regression from `0da4c97`. It is not blocking, but it must be settled before G7. Findings 3–7 were not re-checked and stand.

**Scope:** `755c0c8` (the per-side update guard, `UpdateUs`, `Repeats`, `_jumped`, the test pilot, E10) and `f57ebc6` (design 4's note, task 1.2's line). No GPU (D-046).

**My probe E10q** (scratch, not committed) uses my E9w F and E9x radios and sweeps. It changes one thing: the pilot restarts on each new repeat (`Repeats`), not on the first "Too fast" only. There are two pilots: one 2.5× slower at every repeat, and a stubborn one, 2.5× slower once. 1 320 checks, 0 fail.

| interval | healthy 683 / 800 / 903, still and ±1, linear 0.9–3 s and eased 1.0–2.5 s | pitfall, two-sided, one-sided and throttle, still, 0..1 and ±1, 2–20 s: 2.5× slower each repeat | the same pitfalls, stubborn pilot |
|---|---|---|---|
| 2 ms | **84 of 84 saved** (6 asked slower, 1 repeat at most) | **54 of 54 named**, 2 repeats at most, 176.9 s at worst | 40 named, **14 stuck** (180 s, "Still too fast" 6–31 times) |
| 3 ms | **84 of 84 saved** (14 asked slower; 8 refused at `0da4c97`) | **54 of 54 named**, 2 repeats, 176.9 s | 40 named, **14 stuck** |
| 4 ms | **84 of 84 saved** (32 asked slower; 27 refused at `0da4c97`) | **54 of 54 named**, 3 repeats, 116.7 s | 31 named, **23 stuck** |
| 1 ms over 4 ms | **84 of 84 saved** (33 asked slower; 18 of the 48 linear refused at `0da4c97`) | **54 of 54 named**, 3 repeats, 116.6 s | 31 named, **23 stuck** |

Both pilots saved all 84 healthy cases at every interval. No pitfall run was ever saved.

**The per-side guard** departs from my wording, which counted the whole band. I accept it, because the pigeonhole holds per side: the step rule takes each side's smallest step. With the whole band, a lopsided healthy sweep could let one side's updates vouch for the other's coarse steps.
- **Cost:** a one-sided pitfall is judged only on its own coarse side, about 82 ADC counts. It is deferred like a two-sided one.
- **Result:** every one-sided case was named with the slowing pilot (1–3 repeats, 71 s at worst at 4 ms). A one-sided pitfall is still caught.

**`UpdateUs` on a still ADC's first sweep** (healthy 800, linear, read when the throttle step ends):
- **Quick sweeps:** 0.9–3 s give the true interval at every rate (2000, 3000, 4000, and 4000 for 1 ms over 4 ms).
- **Slow sweeps:** the value is inflated, because a still ADC changes less often than the radio updates. A 2 ms radio reads 3 125 at 5 s, 5 125 at 8 s, 7 750 at 12 s and 12 875 at 20 s. A 4 ms radio reads the same from 8 s on.
- **Later steps:** it falls back to the true interval at the end (a minimum, so it only falls). E10's ±40 µs check reads that end value, so it misses this.
- **Effect:** an inflated value makes the guard stricter. It can only defer a pitfall, never refuse a healthy radio. A still-ADC pitfall throttle at 12 or 20 s, at 2 ms, needed 2 repeats: sweeps of 75 s and 125 s. Design 4 says "asked once more" only for a band transit of 0.7–0.82 s at 2 ms, so this half of the note is refuted.

**Finding 11 (new, not blocking, before G7): the guard can defer the pitfall forever.** A side that held fewer than band / 4 updates is never judged, so the pilot must keep slowing down.
- **Where:** the pitfall's side passes about 82 ADC counts. Its full sweep must take about 4 000 × `UpdateUs` before the side counts: about 8 s at 2 ms and 16 s at 4 ms, more with the inflated value. In my runs a sweep was deferred at 8 s and judged at 12 s (2 ms), and deferred at 12 s and judged at 20 s (4 ms).
- **Effect:** a pilot who settles below that time hears "Still too fast" with no end and no diagnosis. The prompt still asks for "about 2 seconds".
- **The one-sided pitfall at the prompt's own speed (E10r, a pilot who sweeps the same every time, ±1):**
  - **Stuck:** the one-sided roll at 2, 2.5 and 3 s never ends at 2 ms or at 4 ms: 32–41 repeats in 180 s, nothing named. Its healthy side makes the transit slow, so the jump rule never fires.
  - **Named:** the two-sided pitfall and the throttle are named by the jump rule after one "Too fast" at every speed (`_jumped` keeps that working). So is the one-sided roll at 1.5 s.
  - **Healthy:** 683 and 903 at 1.5–2.5 s are saved with no repeat.
- **Regression at 2 ms:** at `0da4c97` every one of these pitfalls was named, by the step rule at a smallest step of 10. Now the stubborn pilot leaves 14 of 54 unended, and the literal pilot leaves every one-sided sweep of 2 s or more unended.
- **Risk:** nothing wrong is saved, and the pilot's own TX12 pitfall (79 / 1 and 85 / 0, two-sided) is named at a 2 s sweep.

**Suggestions (game developer):**
- **Say how slow:** the wizard knows the side's time and the time it needs, so the "Still too fast" prompt can ask for that many seconds.
- **Cap the deferral:** after a repeat whose coarse side held no more updates than the last, name the axis: "moves in jumps … sweep slower or recalibrate the radio's sticks".
- **Bound `UpdateUs`** so a still ADC's slow sweep cannot inflate it, for example `min(measured, the slowest supported radio's 4 ms)`.
- **Test it:** add a pilot who sweeps 2 s every time to E10, with the one-sided pitfall, asserting that Roll is named.

**Scenarios:**
- "The TX12 pitfall is caught" passed with the slowing pilot at every interval. The pilot's own two-sided pitfall is also named at a literal 2 s. With the stubborn pilot it is not caught in 74 of 216 runs, nor is the one-sided pitfall at a literal 2–3 s (finding 11).
- The "683–903 pass" half now passes at 3 ms, 4 ms and 1 ms over 4 ms.

**Requirements:** INP-05 met. INP-02 is still partly met (finding 11; 1.2b).

**Hygiene:**
- Both commits are subject-only with no trailer, by `Game Developer <gamedev@450fpv.local>`.
- They touch 5 paths, none under `reference/`, `Library/`, `Temp/` or `Logs/`.
- The sim package and `tools/{simtests,flightbench,replay}` show 0 diff lines against `9fe4af2`.

```
git rev-parse --short HEAD                                          f57ebc6
dotnet test tools/inputtests                                        Passed 268/268 (28 s; 97 + 171 E10)
dotnet run -c Release --project tools/flightbench -- --golden
  free-body 0x50ED4B05A5D02F20 (matches); powered-flight 0x3F13A5CF5DB7DAFD (matches);
  contact-flight 0x3EE12C8D692BADF3 (matches), 5 impacts; tumble 0xE3178EE7FF322698 (matches): PASS
QA probe e10q                                                       1320 checks, 0 FAIL; healthy 672 of 672 saved (both pilots);
                                                                    pitfall 216 of 216 named (slowing pilot), 0 of 432 saved;
                                                                    stubborn pilot 74 of 216 stuck at 180 s (finding 11)
QA probe e10r (literal pilot, ±1)                                   24 checks, 0 FAIL (none saved); one-sided 2 / 2.5 / 3 s stuck
                                                                    at 2 and 4 ms (32–41 repeats); two-sided, throttle named (jumps)
python tools/hygiene/precommit.py --tree HEAD                       1611 files scanned, 0 findings (exit 0)
openspec validate p6-input-app --strict                             Change 'p6-input-app' is valid
free RAM before Unity: 4.53 GB; Unity Hub running
Unity -batchmode -nographics -runTests -testPlatform EditMode -testResults Logs/editmode-recheck10.xml
  exit 0; 0 "error CS"; testcasecount=589 passed=589 failed=0 (91.3 s)
  Fpv450.Sim.Tests 215, Fpv450.Tests.EditMode 106, Fpv450.Tests.Radio 251, Fpv450.Tests.Session 17
  golden on Mono: 0x3F13A5CF5DB7DAFD, 0x3EE12C8D692BADF3, 0xE3178EE7FF322698
```

## Re-check of findings 10 and 11 (at `019362d`)

**Verdict: changes requested (finding 12).** Finding 11 is fixed: no pitfall is left unended at 2–4 ms, whatever the pilot does, and nothing wrong is saved. I accept its trade-off. Finding 10's fix still holds at 2–4 ms. But the fix's 4 ms bound on `UpdateUs` brings finding 10 back, worse, for radios that update slower than 4 ms. EdgeTX's own source shows such radios exist, and a TX12 with its RF module on can be one. The fix is one constant, proven below. Findings 3–7 were not re-checked and stand.

**Scope:** `7d17339` (the `Slower` rule, the "times slower" prompt, `SlowestUpdateUs`, 160 tests) and `019362d` (design 4, task 1.2). No GPU (D-046). I did not rerun Unity: the game developer ran EditMode (749/749), and no probe needed it.

**My probes** are scratch and not committed. E10q and E10r are unchanged from my finding 10 re-check. E11t adds pilots over E10q's matrices. E12u and E12v test radios slower than 4 ms. For the cause, the same probes ran on two scratch builds that swap only the wizard: `f57ebc6`'s, and the tip's with one constant changed.

**Finding 11 is fixed:**

| probe | at `f57ebc6` | at `019362d` |
|---|---|---|
| E10q healthy, 672 (two pilots; 2, 3, 4 ms and 1 over 4 ms) | 672 saved | **672 saved**, 1 repeat at most |
| E10q pitfall, 432 | 358 named, **74 stuck** (stubborn pilot) | **432 named**, 0 stuck, 0 saved; 3 repeats at most, 88.3 s at worst (was 176.9 s) |
| E10r literal pilot (±1; 1.5–3 s; 2 and 4 ms) | one-sided 2–3 s stuck, 32–41 repeats | **18 of 18 pitfalls named** after 1 repeat: the one-sided ones by the new rule, the rest by the jump rule. 12 of 12 healthy saved, no repeat |

- **The owner's tests fail a known-wrong model:** run on `f57ebc6`'s wizard, 36 of the 160 new tests fail. They are every pitfall case: 12 literal one-sided, 24 stubborn. That is the 36 that task 1.2's note claims. At the tip, 428 of 428 pass.
- **`UpdateUs` on a still ADC's slow throttle sweep** now reads 4 000 µs or less at every rate (E10q), where it read up to 12 875 µs before.

**The trade-off** (E11t): 9 pilots, at 4 intervals, over E10q's 84 healthy and 54 pitfall cases. 1 944 checks, 0 fail: no pitfall is saved, and every one is named. Below, healthy radios of 84 refused by the "no slower" rule:

| pilot, at each repeat | 2 ms | 3 ms | 4 ms | 1 over 4 ms |
|---|---|---|---|---|
| sweeps the same (literal) | 0 | 8 | 26 | 26 |
| 1.1× slower | 0 | 6 | 16 | 17 |
| 1.2× slower | 0 | 4 | 16 | 16 |
| 1.3×, 1.5× or 2× slower | 0 | 0 | 0 | 0 |
| asked N, goes N^0.25 slower | 0 | 4 | 7 | 7 |
| asked N, goes √N slower | 0 | 0 | 0 | 0 |
| asked N, goes N slower | 0 | 0 | 0 | 0 |

- **Who is refused:** every refused sweep is a first sweep of 0.9–1.5 s, faster than the prompt's 2 s. At 2–4 ms, no healthy sweep of 2 s or slower is ever repeated. So a refusal takes two departures from the prompt: a quick sweep, then less than 1.25× slower when asked for about 2 or 3 times slower.
- **The margin:** the prompt asks for 2× at least (`Math.Max(2, …)`, `CalibrationWizard.cs:389`), 1.6× over the rule's 1.25.
- **The pitfall:** named with every pilot. A pilot who follows the prompt needs 1 repeat, or 2 at 1 ms over 4 ms (asked up to 5× at 2 ms and 9× at 4 ms), and 83 s at worst.
- **Not new:** the jump rule, unchanged since 1.2, refuses a healthy eased 1.0 s sweep that is repeated at 1.0–1.1 s (6 of 84 at every interval). That is its "twice in a row" design.
- **Accepted:** nothing wrong is saved, the refusal says "sweeping it slower", and a pilot who reads the prompt passes.

**The prompts, read as a pilot:**
- "Too fast. Roll: full left, then slowly full right, about 5 times slower than that, and hold." This is clear. "That" is the sweep just made, and a factor is right, because the wizard cannot time a pitfall's whole sweep.
- "Still too fast. … about 2 times slower than that, and hold." Fine.
- **Nit, the "no slower" refusal** (`:413`): "Yaw moves in jumps of 5 of 2048 or more, swept again no slower: it behaves like a switch. Recalibrate the radio's sticks, then calibrate here again, sweeping it slower." On a healthy radio this blames the radio first, and "swept again no slower" reads awkwardly.
  - Suggestion: when the deferred step is under the pitfall's floor of 10, lead with the speed. For example: "Yaw was not swept slower, so it cannot be told from a switch. Calibrate here again, sweeping it about N times slower. If it is refused again, recalibrate the radio's sticks."
  - The cut at 10 works at 4 ms or faster: on a steady sweep that the jump rule accepts, a healthy step is 9 or less, and the pitfall's is 10 or more.
- **Nit:** `(int)Math.Ceiling(1 / held)` (`:389`) is unbounded. If `held` is 0 (a coarse side whose in-band reports share one stamp), .NET 9 and later saturate to "about 2147483647 times slower". Mono and IL2CPP leave the conversion unspecified (on x64, `int.MinValue`, so 2). I did not reach this case. Capping the factor (say at 10) settles it.

**Finding 12 (new; a regression from finding 11's fix, and from my own suggestion): a radio that updates slower than 4 ms is refused outright** (INP-02; owner: game developer).
- **The bound:** `UpdateUs` is bounded at `SlowestUpdateUs`, 4 ms (`CalibrationWizard.cs:88`, `:154`, `:236`). I suggested that bound in finding 11 on the premise that no radio is slower than 4 ms. **EdgeTX's source refutes the premise:**
  - `getMixerSchedulerPeriod()` (`radio/src/mixer_scheduler.cpp`) uses the internal module's period, else the external module's. The USB joystick's 1 ms applies only when neither module has set one.
  - `mixer_scheduler.h` allows 850 µs to 50 ms (`MAX_REFRESH_RATE 50000`).
  - So a TX12 with its RF module left on, for example ELRS at 150 Hz (6.7 ms) or 50 Hz (20 ms), updates its USB output that slowly.
- **Effect:** an under-read interval makes the guard lax. A slow radio's coarse side is judged after too few updates, so a healthy step of 5–9 is refused as "however slowly it is swept", with no "Too fast" first. This is finding 9's misdirection, at the prompt's own speed.

E12u (1 ms reports over a slower radio; E10q's 84 healthy cases; healthy radios saved, of 84):

| radio updates every | follows the prompt, tip | 2.5× slower each repeat, tip | 2.5× slower each repeat, `f57ebc6` (no bound) | follows the prompt, tip with the bound at 50 ms |
|---|---|---|---|---|
| 1 ms; 4 ms; 5 ms | 84; 84; 84 | 84; 84; 84 | 84; 84; 84 | 84; 84; 84 |
| 7 ms | **77** (7 refused as switches) | 75 | 84 | 84 |
| 9 ms | **54** (30) | 62 | 84 | 84 |
| 20 ms | **0** (84) | 1 | 84 | 84 (2 repeats at most, 90 s at worst) |

- **Which sweeps (E12v):** at 7 ms, half of the healthy linear 2.5 s sweeps are refused, though they are slower than asked. At 20 ms, every sweep is refused at every speed from 0.9 to 3 s, including the prompt's 2 s.
- **The cause, by experiment:** I took the tip's wizard and changed only `SlowestUpdateUs`, to 50 000 (EdgeTX's maximum). Then:
  - **Slow radios:** 84 of 84 are saved at every interval up to 20 ms, with both pilots.
  - **Pitfalls with slow radios:** a pilot who follows the prompt has every pitfall named up to 9 ms, and 17 of 18 at 20 ms (the 18th is not saved).
  - **Everything else:** all 428 owner tests pass, as do E10q (1 320 of 1 320: 672 saved, 432 named), E10r (24 of 24) and E11t (1 944 of 1 944), with the same trade-off counts as above.
  - **The cost:** a pitfall's longest run, with a pilot who follows the prompt, goes from 83 s to 112 s, because a still ADC's inflated interval asks for more. The pitfall is refused either way.
- **The `Slower` rule now bounds the deferral on its own,** so the bound that finding 11 asked for is not needed.
- **Fix (game developer):**
  - Raise `SlowestUpdateUs` to EdgeTX's 50 ms, or drop the bound.
  - Add a healthy `Tx12Radio` case at 1 ms over 20 ms, swept at 2 s by a pilot who follows the prompt.
  - Correct design 4's "bounded at 4 ms".
- **Where it would show:** the TX12's interval and module state are recorded at 1.5b, at G7. That is where this would first show, as a refusal of the pilot's healthy radio.

**Scenarios:**
- **"The TX12 pitfall is caught":** passed with all 11 pilot models at every interval up to 4 ms (E10q, E10r and E11t: every pitfall named, none saved). From 5 to 20 ms the pitfall is named too, never saved.
- **The "683–903 pass" half:** passed at 2–4 ms for any pilot who slows 1.3× or more, or at least √N when asked N, and at 1 and 5 ms with both E12u pilots. It **failed** at 7, 9 and 20 ms (finding 12).

**Requirements:** INP-02 is still partly met (finding 12; 1.2b). INP-05 is met (no session diff since my last re-check).

**Hygiene:**
- Both commits are subject-only with no trailer, by `Game Developer <gamedev@450fpv.local>`.
- They touch 5 paths, none under `reference/`, `Library/`, `Temp/` or `Logs/`.
- The sim package and `tools/{simtests,flightbench,replay}` show 0 diff lines against `9fe4af2`.

```
git rev-parse --short HEAD                                          019362d
dotnet test tools/inputtests                                        Passed 428/428 (30 s; 268 + 160 E10)
dotnet test tools/simtests                                          Passed 215/215 (1 m 12 s)
dotnet run -c Release --project tools/flightbench -- --golden
  free-body 0x50ED4B05A5D02F20 (matches); powered-flight 0x3F13A5CF5DB7DAFD (matches);
  contact-flight 0x3EE12C8D692BADF3 (matches), 5 impacts; tumble 0xE3178EE7FF322698 (matches): PASS
python tools/hygiene/precommit.py --tree HEAD                       1611 files scanned, 0 findings (exit 0)
openspec validate p6-input-app --strict                             Change 'p6-input-app' is valid
QA probe e10q                                                       1320 checks, 0 FAIL; healthy 672 of 672 saved; pitfall 432 of 432
                                                                    named, 0 stuck, 0 saved; 3 repeats, 88.3 s at worst
QA probe e10r (literal pilot, ±1)                                   24 checks, 0 FAIL; 18 of 18 pitfalls named after 1 repeat; 12 healthy saved
QA probe e11t (9 pilots)                                            1944 checks, 0 FAIL; every pitfall named; healthy refused by "no slower":
                                                                    literal 0 / 8 / 26 / 26 at 2 / 3 / 4 / 1-over-4 ms, 0 for >= 1.3x or sqrt N
QA probe e12u (1 ms over 5 / 7 / 9 / 20 ms)                         216 checks, 0 FAIL (no pitfall saved); healthy saved, prompt pilot:
                                                                    84 / 77 / 54 / 0 (finding 12)
QA scratch: 160 new tests on f57ebc6's wizard                       Failed 36, Passed 124 (every pitfall case)
QA scratch: tip's wizard, SlowestUpdateUs 50000                     inputtests 428/428; e10q 1320/1320; e10r 24/24; e11t 1944/1944;
                                                                    e12u 216/216, healthy 84 of 84 at every interval
```
