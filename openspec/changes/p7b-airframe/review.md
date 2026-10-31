# QA review: p7b-airframe (task 2.1)

**Verdict: changes requested.** The mechanisms are physical, bounded, logged and deterministic. The goldens of models 7–13 are audited case by case: I reproduced model 7's audit, which was never kept, and the branch with every mechanism off flies the trunk's three goldens bit for bit. Replay is bit for bit, the budget holds with every effect on, and hygiene is clean.

Five findings block the merge, all in PHY-10's and PHY-09's tests and texts and in the log's summary, not in the physics. On my own seeds, two of the shake's bands and the vortex ring's wobble statistic fail. The cause the spec and D-049 give for the visible shake is not the model's cause. The forensic summary mislabels a flight.

**Scope:** `unity/physics/p7b-airframe` at `a5884ea7` (holds `unity/main` at `88a524c`): tasks 1.0, 1.1, 1.2, 1.3, 1.3c, 1.4, 1.5, 1.6. Out of scope: 1.3b (the test range half, waiting for the map's queries).

**How I checked:**
- Every command below was run by me in this worktree; the author's output was not used.
- I used a scratch harness, never committed. It links the core and the suite's scenario files unchanged, plus a copy of `ShakeScenario` with the window, the hold limit and an all-four-ESCs counter as parameters.
- Seeds: asymmetry 11–15, vortex ring 21–23 and 101–120, close hover 31–32, shake 41–42 and 201–210.
- No file in the worktree was edited; `git status` was clean before this file.

## Findings

### Blocking

1. **The heavy band of "The shake lasts as long as the arrest" (0.8–1.4 s) fails on my seeds, and it was measured through a window that cuts the longest shakes.** Owners: the physics engineer (the test), the orchestrator (D-049's and the spec's number).
   - `ShakeScenario.WindowSeconds` is 1.5 s (`Tests/ShakeScenario.cs:20`). The heavy punch from 9 m/s is arrested at 1.36–1.41 s, and the shake runs to the window's last field.
   - On the suite's own seeds 1 and 2 it reads 0.12–1.50 s (1.38 s) on both: the window's end, inside `Settle`'s 0.25 s, so the end is never seen.
   - With the window at 3 s, the same flights last 1.42 s and 1.46 s, and seed 2 reads 1.5, out of the band. My seeds 41 and 42 give 1.46 s on both. Seeds 201–210 give 1.30–1.48 s, 2 of 10 above 1.4.
   - The low side fails too: the 2 kg full punch from 2.5 m/s lasts 0.68 s and 0.70 s on seeds 41 and 42 (reads 0.7), and 0.62–0.86 s on 201–210.
   - In all, 9 of the 40 heavy or 9 m/s full punches on seeds 201–210 fall outside 0.8–1.4 s, a false-failure rate of about 22 %.
   - **What holds on every seed:**
     - "lasts the arrest" (`LastsTheArrest`): 60 of 60 full punches on 201–210, each ending within `Settle`'s 0.25 s of the arrest (0.16–0.20 s on every full punch I printed with the window at 3 s; the 1.5 s window cuts the heavy ones at 0.09–0.14 s)
     - the empty drone's 0.3–0.7 s: 40 of 40
     - the 0.8 step's shorter shake: every pair on 41–42
   - **Fix:**
     - a window of at least `HoldLimit` + `Settle` plus margin (3 s here)
     - the heavy band re-stated from the physics: "lasts the arrest, longer than the empty drone's at the same descent, up to about 1.5 s heavy from 9 m/s", or a band sized on at least 10 seeds
     - D-049's "0.8–1.4 s" amended to match
2. **The tilt band (5–16°) and the 0.28 s floor are applied to the 0.8 step, where they fail on my seeds.** Owner: physics engineer, with the orchestrator for the spec line.
   - `ShakeTests.cs:72` and `:75` assert `Duration >= 0.28` and `TiltMax` in 5–16° on every sharp arrest, the 0.8 step included. Both bounds are the observed extremes of seeds 1 and 2: the least tilt is 5.01°, on 2 kg, 2.5 m/s, the 0.8 step, seed 1.
   - Seeds 41 and 42: the 0.8 step tilts 3.60°, 4.10° and 4.52°, and lasts 0.22 s and 0.24 s.
   - Seeds 201–210: **23 of 60** 0.8 steps tilt below 5° (2.48–10.09°) and **10 of 60** last under 0.28 s (0.16 s at the least). **0 of 60** full punches leave 5–16° (6.13–14.82°).
   - Neither bound is shown to fail a known-wrong model.
   - **Fix:**
     - apply the tilt band to the full punch, which the spec's "while the ESCs sit at their limit" describes
     - for the 0.8 step, keep only "shows it" (`Fields > 0`) and "shorter than the full punch's"
     - assert the known-wrong in finding 3 (no current limit: tilt 1.5–3.4°) against the band
3. **The spec, D-049, the design and tasks 1.4's notes name the wrong cause for the visible shake, and "the four ESCs" is not what the model does.** Owners: the orchestrator (D-049, the spec, G7's question), the physics engineer (the test and the notes).
   - The texts say the drone tilts "while the four ESCs sit at their current limit and the rotors re-ingest their wash" (`specs/airframe-effects/spec.md:50` and `:62`; `docs/decisions.md:446`; `design.md:50`; `tasks.md:62`; `docs/pilot-questions.md:70`). PHY-10 names wash re-ingestion, frame resonance and the payload pendulum as its sources.
   - **Experiment** (3 s window, every full punch at 0 and 2 kg from 2.5, 5.3 and 9 m/s, seeds 1 and 2):

     | model | tilt | peak field turn | arrest |
     |---|---|---|---|
     | as built | 10.6–15.8° | 0.39–0.83° | 0.44–1.41 s |
     | vortex rings off (the only wash re-ingestion in the code) | 11.7–16.3° | 0.41–0.79° | 0.44–1.39 s |
     | ESC current limit 10 kA (none) | 1.5–3.4° | 0.09–0.30° | 0.23–0.90 s |
     | both off | 1.5–3.1° | 0.10–0.23° | 0.23–0.88 s |
     | asymmetry off | 8.9–12.6° | 0.39–0.91° | 0.44–1.35 s |
     | flapping off | 11.9–16.5° | 0.41–0.83° | 0.44–1.47 s |

     The suite's own rigid run already shows that the modes, and with them the payload's sling, are not the cause (peak 0.93 and 0.84 of the flexing airframe's).
   - **The cause in the model is the ESC current limit** binding under the descending props' load. That limit is an assumed policy (`esc_current_limit_a` 40 A, "assumed" in `drone.json`). The rings coincide with it: they are latched for 0.25–0.93 s of the arrest. `ShakeTests.cs:74`'s `InRing > 0.2` asserts this coincidence, not a cause.
   - **"The four ESCs":** `Limited` counts a step when any ESC is limited (`ShakeScenario.cs:154`, `EscLimited != 0`). All four are limited together for only 0.16–0.44 s of a 0.44–1.41 s arrest (suite seeds; up to 0.68 s on seeds 41–42). The loop lets the others off the limit to correct the attitude.
   - **Fix:**
     - the texts name the current limit as the cause, and "an ESC sits at its limit through the whole arrest, all four for its first part"
     - the test asserts the known-wrong (no limit: the tilt and the shake fail)
     - the `InRing` line is kept only as a description
     - G7's tilt question (`docs/pilot-questions.md:70`) linked to the ESC firmware question (`:68`): with an ESC that senses no current (Bluejay), the model's shake falls to a 1.5–3.4° tilt and 0.09–0.30° peaks, and from the heavy drone it is barely over the visible line
4. **"Heavy on low throttle" fails its wobble statistic on about one seed in five.** Owner: physics engineer.
   - `VortexRingScenario.HeavyOnLowThrottle` needs the 10 ms rate change at least 1.5 times the ringless flight's over the 0.4 s hold.
   - Seeds 101–120: **4 of 20 fail** (104 ×1.46, 107 ×1.27, 117 ×1.28, 119 ×1.44). Seed 23 fails it too; seeds 21 and 22 pass.
   - The ringless flight's own value spreads 3.7e-3–7.8e-3 rad/s from seed to seed, as much as the ring adds. The wobble's corner is about 11 Hz, so 0.4 s gives few independent samples.
   - The rest of the scenario holds on every seed I flew: entry within 34 ms at η 0.45, thrust 93.6–93.9 % of the ringless rotors', exit at η 0.200. The higher throttle's no ring and the known-wrong's failure hold on 21–23, where I flew them.
   - **Fix:** a statistic sized by its false-failure rate. Options: the rings' moment, which is 0 without them; the ratio over several seeds; or a lower threshold whose false-failure rate is measured.
5. **The forensic summary mislabels the airframe's entries** (`Runtime/Log/FlightLogReader.cs:316–333`). Owner: physics engineer.
   - The flapping's `else` prints "ground effect: off". The ground effect and the modes have no `else`.
   - A log with the ground effect on and the flapping off prints both "ground effect: Cheeseman-Bennett …" and "ground effect: off". With the ground effect off it says nothing about it; with the modes off, nothing. Reproduced by recording such a flight and running `FlightSummary.Print`.
   - **Fix:** "flapping: off", "ground effect: off", "modes: rigid airframe", in their own branches, with a test.

### Not blocking

6. **`AsymmetryScenario.AsPredicted` fails seed 13** (owner: physics engineer). The roll yank at 4 s is 0.076° against the 0.053° predicted, +43 % against the 30 % tolerance (`AsymmetryScenario.cs:321`). The relative tolerance fails on small yanks. The spec's scenario holds on all of 11–15 (in the band, steady, off halved, Calm's floor). **Suggestion:** an absolute floor in `Near` (for example ±0.03°).
7. **PLT-04's world queries on the stub's stand stay over 60 µs** (owner: world builder; re-measured in 1.3b).
   - My bench: 98.6 µs p99 a step in `airframe` and 99.7 µs in `contacts`. The physics engineer measured 65 µs in best performance mode.
   - 43 of the 45 calls are the contacts' sweeps on the posts. p6-flight-core's QA review (finding 1) measured 111 µs before this change, which adds the ground effect's 2 calls.
   - The step stays at 43 % of its 300 µs.
8. **`ShakeScenario.Settle` (0.25 s, "assumed") has a physical anchor.** 1.0's step response settles within 5 % in 220–228 ms. With the window at 3 s, every full punch I printed ends 0.16–0.22 s after its arrest, rigid ones included, and all 60 on seeds 201–210 end within 0.25 s. **Suggestion:** derive it from that settle time and label it so.
9. **Risk for G7: the visible shake rests on the assumed current-limit policy.**
   - PR-1's record (`docs/knowledge.md` §4.1) says the image shakes and "the aircraft does not go anywhere uncommanded".
   - The model's full punch tilts 6–16° uncommanded, a trajectory error of about 2 m/s² for half a second.
   - D-049 accepts this and leaves the feel to the pilot. The answer to G7's firmware question decides whether the tilt exists at all (finding 3).

## The physics engineer's five flags, judged

| Flag | Judgement |
|---|---|
| The tilt passes at 5.01° against a 5° floor | **Not acceptable** (finding 2). The floor is the minimum of the two seeds flown, not a bound. 23 of 60 0.8 steps on my seeds fall below it. The full punch never does (6.13° least on 60). |
| The rigid airframe's shake ends at 0.22 s against a 0.25 s bound | **Acceptable.** With the window at 3 s, 0.16–0.22 s after the arrest on every full punch I printed, rigid ones included (0.19–0.22 s on seeds 41–42), and within 0.25 s on all 60 of seeds 201–210. The bound matches the loop's 220–228 ms settling (finding 8). |
| "0.3–0.7 s" read to a tenth: 0.74 counts as 0.7 | **Acceptable for the empty drone's band.** 0.3–0.7 s is the pilot's own one-decimal estimate (K§4.1, PR-1). A field is 20 ms and the seed moves each edge by a field. 40 of 40 on seeds 201–210 read inside. The reading does not rescue the heavy band (finding 1). |
| The 0.8–1.4 s band holds only for a full punch | The spec states it for the full punch, so applying it only there is right. It **fails** for the full punch on my seeds and behind the window (finding 1). |
| The stand's queries at 65 µs against PLT-04's 60 µs | **Not this change's,** and not blocking (finding 7). It must be re-measured on the test range in 1.3b, against the world query's own line. |

## Spec scenarios (`airframe-effects`)

| Scenario | Result |
|---|---|
| Asymmetry on and off | **Pass.** Seeds 1–5 (suite) and 11–15 (mine): in the band, never buzzes or diverges, off halved (off is 5e-17 °/s). The known-wrong identical rotors fail. My known-wrong, tolerances ×3 and ×5, fails the band on 1 and 3 of 5 seeds. |
| The cushion and the hole | **Pass.** 1.20866 against Cheeseman–Bennett's 1.21008 (−0.12 %); over the hole, free air bit for bit. No seed enters it. |
| The ceiling under a roof | **Pass.** 1.08030 against Hsiao–Chirarattananon's 1.08080 (−0.05 %); in the open, free air bit for bit. |
| The close hover is never still | **Pass.** Suite seed and seeds 31–32: every 0.5 s block at 1.30e-2–2.74e-2 rad/s against the 1e-3 floor. Without the wash 3.2e-6–1.5e-5, and 3 m up 4.6e-6–2.1e-5; both fail. |
| The hub moment against its published form | **Pass.** Worst relative error 2.54e-16; the known-wrong rigid rotor 1.0. |
| A gust step tilts the drone harder | **Pass** as the suite flies it (no seed parameter). Pitch 16.38 against 7.28 °/s (×2.25), roll 16.38 against 7.53 (×2.18), tilting away from the gust. |
| Heavy on low throttle | **Pass** on the suite's seed and on 18 of 23 of mine. **Fails the wobble statistic on 5 of 23** (finding 4). Entry, thrust loss and hysteresis hold on every seed, and the known-wrong fails on 21–23. |
| Shake only when its causes meet | **Pass** in substance on every seed. From the hover a gentle input never shows it and a sharp one only a 0.10–0.14 s kick; every sharp arrest shows it; the gentle ramp in a 5–9 m/s descent wobbles below the punch; it varies. The test's 0.28 s floor fails 10 of 60 0.8 steps (finding 2). |
| The shake lasts as long as the arrest | **Fail** on my seeds: the heavy band (finding 1). "Lasts the arrest" 60 of 60, the empty drone's 0.3–0.7 s 40 of 40, the 0.8 step shorter on every pair. |
| The visible shake is the rigid body's | **Partly.** The rigid airframe is within 20 % and as long (0.93 and 0.84 on the suite's seeds, 0.92 and 0.95 on mine), and only the modes publish `CameraVibration`. The tilt band fails on the 0.8 step (finding 2), and "while the four ESCs sit at their limit and the rotors re-ingest their wash" is not the cause (finding 3). |
| The heavy corner at low charge | **Pass.** T/W falls with the sag; the known-wrong stiff pack keeps it; the signal is clear at 20 % and sets at 10–14 %; T/W < 1 at the cut-off. The cut-off is reached in a long flight. |
| The inertia character on the seven loadouts | **Pass.** Yaw saturates first on all seven, legs on and off. The character is as D-048 states it (pitch above roll only on L2). The build is checked independently (`TheBuildIsTheParallelAxisSum`; the sheet's loadouts to 3 %). |

## Requirements

| ID | Status |
|---|---|
| PHY-07 | **Met.** The band from the tolerances, failing known-wrongs on both sides, on my seeds too. |
| PHY-14 (Calm's floor) | **Met.** Every 1 s block above the band's floor with the asymmetry on, on seeds 1–5 and 11–15; 3.6e-8 off. |
| PHY-08 | **Partly met.** Met on the stub; the test range's terrain, holes and mats are 1.3b. |
| PHY-09 | **Met** in the model; the scenario's wobble statistic is under-powered (finding 4). |
| PHY-10 | **Partly met.** Arises, rare, varies, publishes `CameraVibration`, lasts the arrest. The heavy and tilt bands fail on my seeds, and the named causes are not its cause (findings 1–3). |
| PHY-11 | **Met** (D-048). |
| PHY-12 | **Met** (D-048). |
| PLT-04 | **Partly met.** p99 ≤ 300 µs and 0 B with every effect on; the queries' 60 µs line not met on the stub's stand (finding 7, not this change's). |

## Golden re-records, audited case by case

No stored input changed. The golden setups gain only the mechanisms' switches, and the scripts are unchanged (`GoldenFlights.cs` diff). The two `drone.json` edits (τ_esc in 1.0, the OCV table in 1.5) are each audited by switching them back. The merges of `unity/main` moved no golden: the trunk at `88a524c` pins model 6's hashes, which the branch reproduces with its mechanisms off (model 7's row).

| Model | Task, cause | Audit (the parent, bit for bit) | Result |
|---|---|---|---|
| 7 | 1.0, τ_esc 5 → 0.4 ms | 1.0's temporary test, never kept. **QA's experiment:** every mechanism off, τ_esc back at 5 ms, model 11's packs, over the trunk's 1296 bytes: powered `0x3F13A5CF5DB7DAFD`, contact `0x3EE12C8D692BADF3`, tumble `0xE3178EE7FF322698`, the trunk's three. At 0.4 ms: `0xA65FBFBCBCFC5B96`, model 7's. | match |
| 8 | 1.1, the asymmetry | `WithTheAsymmetryOffEachGoldenIsModel7s` | match |
| 9 | 1.2, the rings | `WithTheRingsOffEachGoldenIsModel8s` | match |
| 10 | 1.3, ground and ceiling | `WithTheGroundEffectOffEachGoldenIsModel9s`, `ThePoweredGoldenFindsNoSurface` | match |
| 11 | 1.3c, the flapping | `WithTheFlappingOffEachGoldenIsModel10s` | match |
| 12 | 1.5, the P50B's table | `OnModel11sPackTableEachGoldenIsModel11s` | match |
| 13 | 1.4, the modes | `WithTheModesOffEachGoldenIsModel12s` | match |

All match in .NET Release (`flightbench --golden`), .NET Debug (`dotnet test`) and Mono (Unity EditMode). **Suggestion:** keep my model 7 experiment as a test, so the chain to the trunk is pinned.

## Log format 11

- **The header's tags,** walked raw on my recorded log: 1–14, **15 input (18 B)**, **16 asymmetry (49 B), 17 vortex ring (105 B), 18 ground effect (65 B), 19 flapping (527 B), 20 modes (161 B)**. The walk ends exactly at the header's 28 942 bytes.
- **The trunk's input entry:** `LogHeader.cs`'s tag-15 write (24 lines) and read (16 lines) are byte-identical to `unity/main`'s, and `--summary` reads it ("input: none recorded").
- **The step record** is 1656 B; each parent's bytes stay in place (`TheRecordKeepsEachParentsBytesInPlace`).
- **The draws:** 16 per-flight draws, named, bounded and logged in the header. The two evolving streams, `vrs.thrust` and `ground.recirc`, are named in the randomness entry, bounded (±0.30, ±0.15) and logged through `RingThrust` and `WashInflow` in every record. They perturb a rotor's thrust and inflow, never the pose (`Propulsion.cs:398`, `:440`).

## The budget (1.6's table, re-run)

`flightbench --bench`: AC, Balanced plan, best power efficiency mode, CPU 3201/3201 MHz; another agent's Blender running.

| case | on p50 / p99 / max µs | off p50 / p99 | effects at p50 | queries p99 a step | allocation |
|---|---|---|---|---|---|
| on the stand | 56.3 / 129.1 / 858.4 | 53.1 / 124.9 | +3.2 (5.7 %) | 98.6 µs | 0 B |
| the close hover | 13.3 / 33.0 / 1349.6 | 10.6 / 23.6 | +2.7 (20.3 %) | 7.8 µs | 0 B |
| in the vortex ring (all four latched 100 %) | 12.2 / 26.7 / 367.3 | 9.7 / 22.1 | +2.5 (20.5 %) | 7.3 µs | 0 B |
| the powered flight, every effect on | 17.4 / 108.9 / 903.4 | | | 10.9 µs | 0 B |

The worst p99 is 129.1 µs, 43 % of PLT-04's 300 µs. My p50s and the effects' costs are of the same size as 1.6's; its p99s were taken in best performance mode.

## D-048 and D-049 against the code and tests

- **D-048:**
  - PHY-12's character is asserted as stated: pitch above roll only on L2, yaw/roll outside 1.0–1.3 on L1 and L6.
  - The low-voltage signal is the field's loaded voltage below 3.00 V a cell (`FeedSignals.LowCellVolts`, `FlightCore.cs` `PackLow`). It sets at 13 % under full throttle (test: 10–14 %); the margin falls with the sag; there is no margin at the cut-off.
  - The filter and hysteresis are left to the OSD, as D-048 says.
  - **Agrees.** One wording nit: "sets at the knee" against the table's knee below 10 %; the tests say "near the knee".
- **D-049:**
  - "arms at 48–63 Hz, on the builder's notches": agrees (48.0, 55.7, 60.9, 62.7 Hz).
  - A gentle increase never shows it from the hover, and the gentle ramp in a 5–9 m/s descent wobbles: agrees.
  - "0.3–0.7 s for the empty drone's moderate descents": agrees.
  - "the four ESCs sit at their 40 A limit while the rotors re-ingest their wash": **does not agree** (finding 3).
  - "0.8–1.4 s heavy or from 9 m/s": **does not hold** (finding 1).
  - "tilts 5–16°": holds for the full punch, not for the 0.8 step (finding 2).

## Commands and output (trimmed)

```
$ dotnet test tools/simtests
Passed!  - Failed:     0, Passed:   254, Skipped:     0, Total:   254, Duration: 2 m 12 s - Fpv450.Sim.Tests.dll (net10.0)

$ dotnet run -c Release --project tools/flightbench -- --golden
powered-flight: 10000 steps, hash 0x0F843363CA8DBFBA (matches) ... PASS
contact-flight: 12000 steps, hash 0xD1EA300928A02EEE (matches), 5 impacts, ends 0.2519 m up on 8 touching pairs: PASS
tumble: 10000 steps ..., hash 0xB371E06FF28075D8 (matches) ... PASS
asymmetry off, over model 7's layout: powered 0xA65FBFBCBCFC5B96, contact 0x3AF33645FF8F1919, tumble 0xE3178EE7FF322698 (model 7's, bit for bit): PASS
rings off, over model 8's layout: powered 0x08FBAF9331110BE9, contact 0x5D45B356ECD6B940, tumble 0xF7E0F587B45FA098 (model 8's, bit for bit): PASS
ground effect off, over model 9's layout: powered 0x5AF86BCB6FD0B3A2, contact 0x8D965FFD8708C6C3, tumble 0x50541A5726F695D8 (model 9's, bit for bit): PASS
flapping off: powered 0x0B91602E467653F4, contact 0x05EDD1881425872E, tumble 0x58EF4F6AE8D111D8 (model 10's, bit for bit): PASS
model 11's pack table: powered 0x9057D18CF741A255, contact 0xF6E32F2FDC831268, tumble 0x58EF4F6AE8D111D8 (model 11's, bit for bit): PASS
modes off, over model 12's layout: powered 0x63D668DD01A1E587, contact 0x8B50CC1BD8EBC8FF, tumble 0x58EF4F6AE8D111D8 (model 12's, bit for bit): PASS

$ dotnet run -c Release --project tools/flightbench -- --scenarios     # exit 0, every airframe row PASS, every known-wrong "fails, as it must"
  2 kg, after a 1.0 s drop: sharp:  9.12 m/s down, arrested 1.36 s; shake in 40 fields, 0.12–1.50 s (1.38 s) ...   # the window's end
scenarios: PASS
$ dotnet run -c Release --project tools/flightbench -- --contacts      # contacts: PASS

$ dotnet run -c Release --project tools/flightbench -- --bench
flightbench: 7 bench cases registered; AC, Balanced plan, best power efficiency mode, CPU 3201/3201 MHz
airframe: on the stand ... p50 56.30 µs, p99 129.10 µs, max 858.4 µs; off ... p50 53.10 µs, p99 124.90 µs; ... 0 B; world queries 75.3 % (45.0 calls a step; p99 98.6 µs, max 1188.6 µs a step): PASS
airframe: the close hover ... p50 13.30 µs, p99 33.00 µs ...: PASS
airframe: in the vortex ring ... p50 12.20 µs, p99 26.70 µs ...: PASS

$ dotnet run -c Release --project tools/replay -- --record <scratch>/qa-flight.fpvlog --realtime --wind windy
record qa-flight.fpvlog: real time on the flight thread, Windy wind, 120051 steps (120.051 s), 398 events, 103 scheduler hitches, 0 log waits, 0 stick reports dropped
  landing: touched at 107.529 s at 0.483 m/s down; ... 5 impacts, armed False, crashed False
$ dotnet run -c Release --project tools/replay -- <scratch>/qa-flight.fpvlog
  match: 120051 steps and 295 events, bit for bit                    # 398 = 295 + the 103 hitches
  replayed in 3.22 s ...; exit 0 (Match)

$ & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/qa-editmode.xml -logFile - | Out-Host
test-run result="Passed" total="859" passed="859" failed="0" (Mono; every golden and kept audit among them)

$ python tools/hygiene/precommit.py --tree HEAD
1649 files scanned, 0 findings
$ openspec validate p7b-airframe --strict
Change 'p7b-airframe' is valid
```

QA's experiments, from the scratch harness:

```
audit7:  powered 0x3F13A5CF5DB7DAFD MATCH, contact 0x3EE12C8D692BADF3 MATCH, tumble 0xE3178EE7FF322698 MATCH (the trunk's)
window 3 s, suite seeds: 2 kg drop 1.0 seed 1: 0.12-1.54 s (1.42 s); seed 2: 0.12-1.58 s (1.46 s), band OUT
seeds 41-42 (1.5 s window): 48 flights, 5 failures; (3 s): 7 failures
seeds 201-210, 120 sharp arrests, 3 s window: tilt outside 5-16 deg 23 (least: full 6.13, 0.8 step 2.48); shorter than 0.28 s 10;
  empty moderate outside 0.3-0.7 0 of 40; heavy or 9 m/s full punch below 0.8 7, above 1.4 2 of 40; not lasting the arrest 0 of 60
vrs seeds 101-120: 4 of 20 seeds fail HeavyOnLowThrottle (wobble x1.46, x1.27, x1.28, x1.44 against 1.5)
dump, ground effect on, flapping off: "ground effect: Cheeseman-Bennett below, ..." then "ground effect: off"
```

## Hygiene

- **Commits:** 38 in `unity/main..HEAD` (37 Physics Engineer, 1 Orchestrator). All subject-only and at most 72 characters; no body, trailer or co-author line. The two merges are subject-only.
- **Staging:** no file from `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/`, and no images or video in the diff.
- **OPSEC:** the scan finds 0 findings over 1649 files. The new notes cite published sources only.
- **Licences:** no third-party file added.
- **Determinism:** every new core file uses `DetMath` (no `Math.Sin`/`Cos`/`Exp`/`Pow`, no FMA, no clock); `noEngineReferences` true; 0 B in steady state in every bench row.
