# Design: p7h-step-budget

Numbers are labelled **pilot / measured / published / derived / assumed**. Task 1.1 (the step's own clock) needed no design beyond its spec; this file is task 2.1's, the lag cap.

## 1. Why a cap
`FlightScheduler.RunDue` ran every step it owed, back to back. Once steps cost c > 1 ms, the backlog grows by c − 1 ms a step and only shrinks once c < 1 ms. L1 (`docs/reports/lag/model-79-flights.md`) measured the player's steps at 1.3–4.7 ms in the lag stretches and backlogs of 0.98 s, 1.13 s and 25.6 s in flights 37, 38 and 35, 47.8 s in flight 36 (measured). Each step reads the stick in force at its host time, so a backlog of L is L of extra control latency: the pilot's "sluggish" and "feels like RX loss" (D-087).

The cap bounds the latency, not the cost: a slow stretch still runs slowly until L6 cuts the step's cost.

## 2. B, the cap (derived)
The pilot closes the control loop through the feed. The chain, stick to eye:

| Link | Figure | Label |
|---|---|---|
| TX12's USB report interval | 4 ms (250 Hz) | measured per flight (the header's histogram) |
| The step's sampling of the sticks | ≤ 1 ms | derived (1 kHz, PHB-01) |
| The RC link's frame | 4 ms (250 Hz) | pilot (`fc-profiles` §1), `FlightController.RcLinkHz` |
| Stick to motor command in the sim | p50 6.5 ms, p99 8.5 ms | measured (p6-input-app 1.5, the latency test) |
| The feed's field | 20 ms (50 fields/s) | published (PAL), `FlightCore.FieldSteps` |
| A line's read to the screen | 0.1–18.4 ms | derived (p7e-camera-feel design 2) |
| Betaflight's receiver signal-loss timeout | 100 ms | published (Betaflight 4.5), `Failsafe.RxLossSeconds` |

**The pilot sees a backlog only in whole fields.** The FPV view draws a field only once the sim has passed it (`FlightScene.Latch`, p7e design 2). With a backlog L, the field shown at wall time t is the one shown at t − L without it. So:
- **L ≤ one field** shows the picture at most one field behind: at worst one field is held once. This is the plan's "one bounded hiccup".
- **L > one field** can hold two fields or more. Every response then comes a field or more later than any the real chain shows, since a real camera also samples the drone once a field (0–20 ms by phase).

So **B = one field = 20 ms (derived).** Any cap up to a field costs the pilot at most one held field, and the largest such cap drops the least stick input: a stall shorter than B is still run whole (PLT-04's catch-up). A smaller B buys nothing the feed can show. A larger B costs a second field.

**Cross-checks:**
- **Below the lost-link line.** The flight controller never flies a stick older than report + B + frame + step: 4 + 20 + 4 + 1 = 29 ms (derived). That is under Betaflight's 100 ms. An uncapped backlog of 0.4–48 s crossed that line by far, which is how a late sim "feels like RX loss".
- **Above ordinary lateness.** The scheduler sleeps only while more than 2 ms remain, then yields. So its normal lateness is about 2 ms with the 1 ms timer period (derived), and at most 13.6 ms even at Windows' default 15.6 ms sleep granularity (published). The player's steps measured 112 µs p99 and 1.2 ms max in 1.1's 60 s Mono test (measured). The cap never fires in normal flight: only stalls and sustained overruns reach it.
- **The input ring** holds 1,024 reports, 4.1 s at 250 Hz (derived), far over B. So a backlog no longer makes it refuse reports (L1: 18,070 and 30,749 refused in flights 35 and 36).

`FlightScheduler.MaxOwedUs = FlightCore.FieldSteps × FlightCore.StepUs`. It is never tuned: it changes only if the feed's field does.

## 3. The drop
**Where:** `FlightScheduler.RunDue`, before each step.
- Step k is about to start at host time t. For a pass's first step, t is the pass's own now. For each later step of the pass, t is the pass's now plus the clock's time since the pass began.
- Its lateness is L = t − StepHostUs(k). When L > B, `StartUs` moves later by D = L − B, so the step starts exactly B late.
- The steps the pass runs are those due when it began, after its first drop.

**Per step, not per pass.** In a pass of n steps costing c each, the last step starts (n − 1)(c − 1 ms) later than the first. A cap on the pass's first step alone would let a stick age to about c × B: 60 ms at 3 ms steps (derived). Without the in-pass cap, the scenario test fails at step 1011, 22 ms late.

**In a sustained overrun** (c > 1 ms a step):
- The lag grows c − 1 ms a step and reaches B after ⌈B / (c − 1 ms)⌉ steps.
- From then on, each step drops c − 1 ms and the lag holds at B: a constant latency, not a growing one.
- Once steps are cheap again, the B kept is run down by 1 ms − c a step.
- The scenario (c = 3 ms for 500 steps): B is reached after 10 steps, then 490 drops of 2 ms, 980 ms in all (derived, and the test's figures).

**A stall S > B** still freezes the picture for S; nothing can avoid that. The sticks of its first S − B are never read. The FC's RC smoothing sees one step change across the gap.

**What it changes in the flight:** nothing in the physics. Every step is a real 1 ms step and none is skipped; only the host time each step reads its stick at moves.
- In a slow stretch, a step reads the stick c ms of wall time after the one before. So the controller sees the pilot's stick motion c times faster than it was made (its feedforward and RC smoothing too).
- That is inherent to a sim slower than real time with bounded latency. The cure is the step's cost (L6), not the cap.

**What follows the time base:**
- **The sticks:** `Consume` reads at the moved host times. A pause's held loss (`StickInbox.Hold`) is in host time, so `MoveHold` moves its end by D and it keeps the same steps.
- **The poses:** `PoseBuffer.MoveStart` sets the start that the next pair carries. `PosePair.Interpolate` maps host time on the moved base. `FlightScene`'s this-flight test (`pair.StartUs ≥ session.StartUs`) still holds.
- **The session:** `Unpause` starts from the stopped scheduler's `StartUs`, which holds every earlier drop and pause. So a pause moves the steps by the pause alone, and `PausedUs` stays the sum of the pauses.
  - The old rebuild from the session's own start and pauses would have logged a 200 ms pause after an 80 ms drop as 279 ms (the session test's known-wrong run).

## 4. What gets logged
- **A `Hitch` event of cause `HitchCause.Drop` (4):**
  - A: how late the step was about to start, in µs (> B)
  - B: the time dropped, in µs (A − 20,000)
  - its stamp: the state the step makes (k + 1), like `Late` and `Pause`
- **No new event kind and no new field:** the event's A and B carry it, and `Hitch.DroppedUs` carries it to the recorder.
- **Format 72's layout is unchanged.** The new cause value belongs to format 72, which no build has shipped yet. There is no model number, because nothing in the physics changed.
- **A pass's `Late` hitch keeps its meaning:** the steps due when the pass began, the first of them at most B late.
- **Forensics (LOG-03):**
  - Step k's host time is `StartHostUs` + k ms + the B of every `Pause` and `Drop` hitch stamped up to k + 1.
  - Its stick's age is that host time minus the stick's `HostUs` (`LogHeader.StartHostUs`).
  - The scenario test rebuilds every step's host time from the log this way.
- **The summary line:** "hitch: the scheduler was A us behind, past its 20000 us cap: it dropped B us, and the later steps run that much later".

## 5. Replay and determinism
- **The replay is unaffected.** It feeds each step its logged stick and skips hitches (`FlightReplay`), so a flight with drops replays bit for bit (tested on 490 drops, and on the 120 s flight's 30 ms stall).
- **Within B, a late thread still changes nothing it computes** (the early and late workers' test, its long wait now exactly B).
- **Past B, a drop changes which sticks the steps read.** That is the cap's purpose, and the log holds every one.

## 6. Cost
- **A pass of one step** (steady state) reads no extra clock and allocates nothing.
- **A catch-up pass** reads the clock once more per step (`QueryPerformanceCounter`, tens of ns: published order of magnitude).
- **0 B** after the first step, through 490 drops (tested).
- **A false count on .NET, found while testing.**
  - In multi-fixture runs, the test's thread counted exactly 5,264 B in 3 of the 6 runs that named their failures (a seventh failed unnamed), and never in a run alone (measured). The trace put it at step 10, an ordinary one-step pass before the slow stretch, with no collection and no exception in that pass.
  - It is not the drop path. Collections from another thread don't cause it: 30–77 forced or natural ones counted nothing. With `DOTNET_TieredCompilation=0`, the same run counted nothing.
  - So it is CoreCLR's tiered compilation briefly stopping the threads to finish a batch. That counts the unused rest of the thread's allocation chunk as allocated (derived from the above; the runtime's source was not read).
  - The test now empties that chunk with one collection after the first step, so a real allocation still counts and this one can't.
  - The player runs on Mono, which has no tiered compilation of this kind.

## 7. Limits
- **The slow stretch is still slow.** At 3 ms a step, the feed shows about 17 fields a second (derived). L6 is the fix.
- **Tests that compare real-time runs' stick sequences assume no drop** (`RadioTimingTests`, threaded). A host stall over 20 ms there now changes what is read, and logs it.
- **QA's `lagprobe` estimates the cost per step from consecutive `Late` hitches.** Across a drop, the second pass's lateness must add the drop's B.
