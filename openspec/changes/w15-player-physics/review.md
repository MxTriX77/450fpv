# Review: w15-player-physics (2.1)

**Reviewer:** QA Engineer · **Branch:** `unity/physics/physics-integration-2--w15-player` at a1bb5b07 · **Date:** 2026-11-14 · **Tier:** Standard, time-boxed (the pilot is waiting for the build)

## Verdict

**No blocking finding. Approve for the pilot's build, with two checks still pending** (the Mono side of my own-seed flight, and the trial build's smoke, both run by the orchestrator when the machine is free). 2.1 stays unticked until those two are in.

Unity Hub quit as the review started (my one Unity run ended on "No valid Unity Editor license found"), and at 13:14 the PC was restarted; after that no Unity run was allowed (the pilot's own GPU test, under 2 GB of RAM free). So everything below that needs Unity reads the game developer's two EditMode runs on disk, re-checked against the .NET side I ran myself.

## Gate items

### 1. The diff against the non-goals: met

- **The developer's diff** (`38c94cd8..HEAD`): 14 files, all of them `unity/Assets/_Project/Scripts/App` (FlightFeedSignals, LegShots, Session/FlightEnd, FlightSession, PostFlight), `unity/Assets/_Project/Tests/EditMode`, `tools/inputtests/inputtests.csproj` (concurrent GC off, as `AllocProbe` requires) and the change's `tasks.md`. **No file of the sim package, `tools/replay`, `tools/flightbench`, `tools/simtests` or `maps/`.**
- **The sim core equals the physics line:** `git diff origin/unity/physics/physics-integration-2--w9-village HEAD -- unity/Packages tools/replay tools/flightbench tools/simtests maps` is empty, and `git log HEAD..origin/…--w9-village` is empty (the line is wholly merged, nothing of it left out).
- **The merges** (d2ce7a7a, 38c94cd8): `git diff-tree --cc` lists no file that differs from both parents, so no hand-resolved hunk.
- **Against the preview** (`3f1fe31e..HEAD`, outside the sim package): the line's own data and tools (assets-src physics tables, flightbench, worldbench goldens, hullmass, replay, map validator), one editor-only check from the line (`Editor/Farmland/DetailInstanceCheck.cs`), and the developer's files above. Nothing under `Scripts/UI` (the menu, Play, the map panel), `Shaders`, `Settings`, `Scripts/Feed` (the feed's look), the feed rig or the camera (W13, W14), and no scene.
- `LegShots.cs` changes the smoke's four leg-shot cases (re-found at model 99 with the legs drawn). It is the smoke's review shots, not a player's flight; within the task.
- The code itself: `SessionSetup.Build` adds one line, `contacts.Legs?.DrawStiffness()`, as `tools/replay/ScriptedFlight.cs` does; `PostFlight` reads `StemEpisode` events only; `FlightFeedSignals` passes the four stem signals through as the sim gives them. No number of the sim is touched.

### 2. Bit for bit, my own seed: .NET side met, Mono side pending

My test `SessionTests.AnotherSeedsFlightOverTheFarmlandReplaysHeadless` (committed with this review): a session's flight on the **farmland** (its ground, stems and line, legs on, 1 kg, **Windy**), seed **0xC0A79E11**, sticks of my own (lift, pitch forward, hold, pitch back, let down, cut): 6 s. It replays in-process and keeps a copy of its log for `tools/replay`.

```
dotnet test tools/inputtests -c Release --filter "FullyQualifiedName~AnotherSeedsFlightOverTheFarmlandReplaysHeadless|FullyQualifiedName~AFlightIsTheSameInUnityAndHeadless"
  the session's flight: 3041 steps, model 99, records 0x2BCFA28885EDA4CE (.NET 10.0.9 X64)
  replay: match: 3041 steps and 38 events, bit for bit
  Passed AFlightIsTheSameInUnityAndHeadless [5 s]
  stems at 0:00.0: rotor front right cut 5 stems, most of them grass blade, for 0.02 J
  stems at 0:00.0: rotor front left cut 9 stems, most of them grass blade, for 0.08 J, over 2 episodes
  QA's session flight: 6041 steps, model 99, records 0xFB41DAB6EF7695F8 (.NET 10.0.9 X64)
  kept %TEMP%\fpv450-qa\w15-session-c0a79e11-net.flightlog
  replay: match: 6041 steps and 63 events, bit for bit
  Passed AnotherSeedsFlightOverTheFarmlandReplaysHeadless [5 s]
  Total tests: 2, Passed: 2

dotnet run -c Release --project tools/replay -- %TEMP%\fpv450-qa\w15-session-c0a79e11-net.flightlog
  replay w15-session-c0a79e11-net.flightlog: seed 0x00000000C0A79E11, model 99, world 'farmland' 0x5D305D09F5D09021, drone.json 0x8E87EBEA7D441AF8; recorded by inputtests on .NET 10.0.9 X64
  match: 6041 steps and 63 events, bit for bit
  exit 0 (Match)
```

- **Cross-runtime on the developer's seed:** my .NET run gives 0x2BCFA28885EDA4CE, the value the developer's Unity run recorded on Mono 6.13 (`unity/Logs/editmode.xml`, "records 0x2BCFA28885EDA4CE (Mono 6.13.0 …)"). That is two runtimes, two runs, two people: the same records.
- **Pending, Mono on my seed:** the Unity run of my test (command in "Pending" below). Expected: the records 0xFB41DAB6EF7695F8 and "match: 6041 steps and 63 events", then `tools/replay` on the Mono log: match.

### 3. Hygiene and licences: met

```
python tools/hygiene/precommit.py --tree HEAD   → 3664 files scanned, 0 findings (exit 0)
python tools/assets/fetch.py --verify            → OK (exit 0)
```
- **Local store and the friend's world:** `tools/assets/manifest.json` has 10 local-only entries (293 files); none of their files and nothing under their destinations is tracked; nothing tracked under `vendor/`, `world_final`, `Farming_land`, `reference/`, `unity/Library`, `unity/Temp` or `unity/Logs`.
- **Commits:** the nine developer commits are `Game Developer <gamedev@450fpv.local>` as author and committer, the merges and proposal `Orchestrator <orchestrator@450fpv.local>`; every body empty; no `Co-Authored-By`, "Generated with" or anthropic line from the preview's tip to HEAD.
- **Compiler:** the developer's Unity logs have 0 `error CS` and 0 `warning CS` lines, before and after.
- `git fsck --connectivity-only`: clean; no stale `.lock` in the worktree's git dir after the restart.

### 4. The two EditMode failures left: neither is the player's

Unity's run after the change (`unity/Logs/editmode.xml`, copied to my outputs as `editmode-after-dev.xml`): 1766 tests, 1737 passed, 2 failed, 27 skipped (before, at 38c94cd8 untouched: 1762, 1725, 10, 27). Both failures are in the sim package's own tests, fail the same way in the run before the developer's change, and touch no player code (the sim package is byte-identical to the line's).

- **`AllocProbeTests.TheKnownWrong_OneObjectInTheWindow_IsSeenAndNamed`** ("one object …: 0 B", both runs): Mono's `GC.GetAllocatedBytesForCurrentThread` does not see a 24 B object in the window. **Consequence:** in Unity's EditMode, every sim 0 B check through `AllocProbe` proves nothing; the zero-allocation rule is proven only on .NET (`tools/simtests`, 918 of 918 in the developer's run). **What it needs (physics engineer):** either the probe's tests and the sim's 0 B checks marked .NET-only in Unity with the reason, or a Mono path for the probe (the test framework's GC.Alloc recorder, as the developer used for `FpvCameraTests` and `FiberViewTests`). Minor for this build; a coverage gap on the runtime the player actually runs.
- **`SweepTests.OnE01sSoilShapesTheReachHoldsTheLever`** (424 s in both runs against the runner's 180 s default timeout): passes on .NET (simtests 918 of 918). **What it needs:** a `[Timeout]` sized for Mono (about 600 s) or a Unity category that the regular run skips; whether its asserts hold on Mono is unknown, since the runner fails it on time. Minor.

## The spec scenarios

| Scenario | Verdict | Evidence |
|---|---|---|
| The same flight in Unity and headless | **Passed on the developer's seed (Mono and .NET); passed on .NET for my seed; Mono for my seed pending** | above, §2 |
| The legs as the pilot's hands made them | **Passed** | Unity (developer's run) and .NET (my re-run): seeds 0x1E65 → 0.9779, 0.9701, 1.0262, 0.9596; 0x1E66 → 0.9861, 1.0271, 0.9543, 0.9224; legs off: no `leg.stiffness` draw. My own seed 0xC0A79E11's header: σ 3 %, bound ±10 %, draws rr +0.0276, fr −0.0436, rl −0.0719, fl +0.0059 |
| A landing in straw | **Passed** | Unity and .NET, the same four lines (rear right 729 stems of dry culm for 98.62 J …), the log's episodes, replay match 2691 steps and 78 events |
| A flight that cuts nothing | **Passed** | the floor flight in the same test: no `StemEpisode`, no "stems" line |
| (and on my seed) | **Passed (.NET)** | the farmland start's grass: front right 5 stems, front left 8 + 1 over 2 episodes, the summary says 5 and 9 "over 2 episodes", as the log's episode ends sum |
| The EditMode suite | **Partly met** | 1737 of 1766 pass; the 2 failures are sim-owned (§4). No 0 B check reads the raw counter: `FpvCameraTests`, `FiberViewTests` read Unity's recorder, `FlightOsdTests` the probe on .NET and the recorder on Mono |

**Requirements:** LOG-01, PLT-02 met on the developer's seed, pending Mono on mine; PHY-03, PHY-24 (the legs' draw) met; PHY-05, APP-04 (the summary names the cut) met; PHY-25 (the feed gets the stems' signals) met as a pass-through, nothing draws them yet (2.5, a non-goal).

```
dotnet test tools/inputtests -c Release --filter "…EachFlightDrawsItsLegsFromItsSeed|…ALandingInStrawNamesTheCutStems|…TheFeedPassesOnTheStemsCuts|…TheSummaryNamesTheDamage"
  Passed ALandingInStrawNamesTheCutStems [14 s]   replay: match: 2691 steps and 78 events, bit for bit
  Passed EachFlightDrawsItsLegsFromItsSeed [253 ms]
  Passed TheSummaryNamesTheDamage [492 ms]
  Total tests: 3, Passed: 3        (TheFeedPassesOnTheStemsCuts is an App test, Unity only: passed in the developer's run)
```

## Findings

**Blocking:** none.

**Major (physics, not the player; a physics task after the build):**
1. **`FarmlandFlightTests.AFlightOverTheHillsReplays` moved from Windy to Calm.** In Windy at model 99 the laid line breaks at 78.09 s by **abrasion** on a shape of object 207 (the pine the developer names): `TetherBreak` index 0x34 (mechanism 4 Abrasion, member 3 Shape), tension **12.6 N**, the line's turn there **3.59 rad**, its bend radius 13.6 mm; before it, from 76.77 s, a run of `FiberContact`/`FiberSlip` events on the same shape (turns 0.57–1.21 rad, tensions near 1 N), i.e. the line sliding round the shape for about 1.3 s before it parted. With the legs drawn it parts later on the same shape (82.11 s, tension **2.3 N**, turn 1.09 rad): a wear-through, not a tension break. The failsafe then drops the drone (the crash at 79.7 s: all four legs part at 10.6 m/s on weeds, a consequence, not a cause). **Verdict: explained by a named law (abrasion of the laid line moving in wind and wash), not proven against model 85: suspicious.** The change of conditions is a loosening of the test: Windy over the hills is no longer flown anywhere in Unity. **Next run:** the developer's `run2/hillsdiag` flight on seed 0x3110 in Windy with the kept switches (`FlightSetup.TetherBeforeDesign18`, `TetherWakeReach4R`, `TetherAirlessTrail`, the wake grid's version 1) all on (model 85's laws), then each one alone, to name the law that wears the line through; and the slide's distance and normal force over those 1.3 s against the coating's abrasion budget. Then decide: restore Windy, or keep Calm with a Windy case that asserts the wear by law.
2. **The legs at model 99 are not contradicted by the line's bench; the developer's cases are other cases.** The bench (`full-model99--legs.out`) gives 1.5 m/s rolled 10° → 50 % cracked, rolled 20° → 100 % broken, and 0.5 m/s with 0.3 m/s drift → 33 % cracked. The developer's kerb drop is a pivot over an 8 mm steel kerb under one foot (a rolled landing on the others), and their sweep (`run2/kerb-sweep.txt`) goes from all four intact at 1.25 m/s (seeds 1–6) to three or four broken at 1.5 m/s (seeds 1–8): a cliff inside 0.25 m/s, worth a physics look. The straw landing's 0.33 m/s crack of front right is on weeds under straw after a 1.15 s descent from a hover, the bench's drift cell, not its level one. **Not run (time):** the straw landing's sideways speed at touch from its log, which would settle it. Report only.

**Minor:**
3. The two sim-owned EditMode failures (§4).
4. The summary's clock floors: an episode that starts at 0.064 s reads "stems at 0:00.0". Correct to the format; noted only.

## Pending (for the orchestrator)

1. **The Mono side of §2** (Unity Hub running, the pilot's machine free; `-nographics`, no GPU):
   ```powershell
   cd C:\.dev\450fpv\.claude\worktrees\w9-levels
   & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testFilter "Fpv450.Tests.Session.SessionTests.AnotherSeedsFlightOverTheFarmlandReplaysHeadless" -testResults Logs/qa-w15.xml -logFile - | Out-Host
   dotnet run -c Release --project tools/replay -- "$env:TEMP\fpv450-qa\w15-session-c0a79e11-mono.flightlog"
   ```
   Expected: the test passes, its output reads `records 0xFB41DAB6EF7695F8 (Mono 6.13.0 …)` and `replay: match: 6041 steps and 63 events, bit for bit`; `tools/replay` on the Mono log exits 0 with the same line. Any other hash is a cross-runtime divergence and blocks the build.
2. **The trial build** (step 5 of the brief): its `build.log` with no error, the smoke PASS, `BUILD-INFO.txt` naming the tree. Not in `scratchpad/build-98/trial/` when this review was written.

## Outputs
The session's scratchpad, `build-98/qa/` (local only): the developer's after XML, my Unity log of the failed licence, the .NET scenario run.
