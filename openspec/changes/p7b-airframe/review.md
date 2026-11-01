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

---

# Re-review at `292c2bc7` (task 2.1, second pass)

**Verdict: changes requested.** One finding blocks. It sits in PHY-10's stated bounds and one test threshold, not in the physics.
- Findings 1–6 of my first review are answered and 7–9 handled.
- D-050's layout fits the drone model, and PHY-12's ratios match my own build of `drone.json`.
- The golden chain is kept from the trunk to model 15, and I reproduced 1.7's model 13 audit.
- Log format 12 walks exactly, replay is bit for bit, and the budget holds on AC.
- On my own seeds 301–340, three of the shake's restated "every flight" claims miss on one to three flights each. The pilot's-band test (at least 8 of 10) fails one of my four 10-seed groups.

**Scope:** `unity/physics/p7b-airframe` at `292c2bc7` (model 15, log format 12): tasks 1.7 and 1.8, and the answers to findings 1–9. It holds `unity/main` at `bf94f75`. 1.3b stays out of scope.

**How I checked:**
- I ran every command below in this worktree. Nothing from the handoff was taken as evidence.
- I used a scratch harness, never committed. It compiles the core's `Runtime/` and the suite's scenario files unchanged, plus my own loops over seeds. A second build swaps in `AirframeModes.cs` with model 13's sling put back; a diff against `a5884ea7`'s file, comments stripped, shows only the fields' order differing.
- Seeds:
  - the shake on 301–340, 40 a cell, sized as `--shake`
  - the ring on 301–340
  - the asymmetry on 31–60
  - The suite now flies 1–40, 101–120 and 1–30, which hold my old seeds, so these are fresh.
- No tracked file was edited but this one.

## The first review's findings, answered

| # | Finding | Answer | Verdict |
|---|---|---|---|
| 1 | The heavy band (0.8–1.4 s) failed, behind a 1.5 s window | Window 3 s. The heavy band is dropped: the length follows the arrest, with its medians stated. The pilot's 0.3–0.7 s is kept for the empty drone from 2.5–5.3 m/s, on most flights and at the median | **Fixed.** The window sees every end: end less arrest at most 0.25 s on my 240 full punches, against `Settle`'s 0.31. My medians: 0.42, 0.62, 0.82 s empty; 0.57, 0.94, 1.28 s at 2 kg. The spec's are 0.44, 0.64, 0.82 and 0.66, 0.96, 1.32. The pilot's band: 37/40 and 40/40. Its test's threshold is B1(d) |
| 2 | The tilt band and the 0.28 s floor applied to the 0.8 step | Both dropped. Kept: every sharp arrest shows it, an ESC meets its limit, and the frame's band rings over 0.3 m/s² | **Fixed** as asked. "Every sharp arrest shows it" now misses on 3 of my 240 0.8 steps: B1(a) |
| 3 | The wrong cause; "the four ESCs" | The cause is the current limit: an ESC through the whole arrest, all four for its first part. The no-limit known-wrong is asserted, `InRing` only printed, G7's tilt question tied to the firmware one | **Fixed** in the spec, design 5, the tests and 1.8's notes. On my seeds an ESC holds to within 0.005 s of the arrest on 240/240, all four for 0.25–1.29 s. The no-limit ESC fails `LastsTheArrest` on 240/240. Still open: D-051, PHY-10's requirement text and G7's question keep pre-1.8 numbers (N1), and the "less than half" bound misses one seed: B1(c) |
| 4 | The ring's wobble statistic was under-powered | Hold 0.6 s, the rings' whole passage (derived); 20 seeds asserted | **Fixed.** Seeds 301–340: heavy 40/40, the higher throttle 40/40, the known-wrong failing 40/40. The wobble is 1.67–24.9 × the ringless flight's, against 1.5 |
| 5 | The summary mislabelled the effects | Each label in its own branch; a test for each effect off alone and for all off | **Fixed.** Reproduced in three cases, one line per effect each: <ul><li>flapping off: "flapping: off", and the ground effect's real line</li><li>ground effect and modes off: "ground effect: off" and "modes: off (a rigid airframe)"</li><li>asymmetry and rings off: "motor asymmetry: off (identical rotors)" and "vortex rings: off"</li></ul> |
| 6 | The asymmetry's yank tolerance failed seed 13 | An absolute floor of 0.03°, assumed, its order derived; 30 seeds asserted | **Fixed.** Seeds 31–60: `AsPredicted` 30/30, `Steady` 30/30. The largest miss beyond 30 % is 0.0289°, inside the floor by 0.001° (N3) |
| 7 | The stand's queries over 60 µs | The world query's, in 1.3b | **Agreed, not this change's.** My bench: 99.5 µs p99 a step. 1.8's 147.5 µs was taken under other agents' load |
| 8 | `Settle` had no anchor | Derived from the loop's roll settling, 0.31 s | **Fixed.** On my 240 full punches the shake ends at most 0.25 s after the arrest |
| 9 | The G7 risk | D-051; the tilt question rests on the firmware question | **Handled.** `pilot-questions.md:70` ties the tilt to the firmware; its number is stale (N1) |

## Findings

### Blocking

**B1. Three of PHY-10's restated "every flight" claims miss on my seeds, and the pilot's-band test is sized at a 3 % false-failure rate.** Owner: the physics engineer (the spec lines, `ShakeTests`, 1.8's notes).
- **Why:** each bound sits just past the extreme of seeds 1–40. A new draw beats the largest of 40 with a chance of 1 in 41, so a fresh 40 seeds passes such a bound about once per claim. That is what I see:
  - **(a)** "every sharp arrest of a descent shows it" fails on 3 of my 240 0.8 steps, all at 2 kg: seeds 314 and 318 from 2.56 m/s, and 324 from 5.43 m/s. Their peaks are 0.067–0.077° against the visible line's 0.088°. Every full punch shows it (240/240).
  - **(b)** The gentle ramp's "an ESC on its limit for at most half the full punch's time" fails on seed 321 (empty, from 8.98 m/s): 0.43 s against the punch's 0.76 s, a share of 0.56. On seeds 1–40 the share is at most 0.43. `Wobbles` fails it.
  - **(c)** "without the limit it tilts less than half as far" fails on seed 333 (empty, from 9.0 m/s): 1.31° against 2.38°, a ratio of 0.55. On seeds 1–40 it is at most 0.43. The cause still holds on that seed: no ESC holds the arrest, and it ends in 0.53 s against 0.74.
  - **(d)** `TheShakeLastsAboutTheArrest` asks at least 8 of 10 seeds inside 0.3–0.7 s:
    - The empty full punch from 2.5 m/s leaves the band on 3 of 40 on seeds 1–40 and on 3 of 40 of mine: p ≈ 0.075.
    - So a fresh 10 seeds fails the test 3.4 % of the time (11 % at p = 0.12).
    - My seeds 311–320 fail it (7 of 10, median 0.38 s). 301–310, 321–330 and 331–340 pass.
- Everything else in the restated scenarios holds on all 40 of my seeds a cell (table below).
- **Fix:** texts and thresholds only; no model, golden or log change:
  - **(a)** "every full punch shows it". State the 0.8 step's rate as measured on 80 seeds or more, for example "nearly every 0.8 step".
  - **(b)** and **(c):** either set the bounds from enough seeds that they keep a margin past their extremes, or state them as rates or quantiles. For example, a share under 0.6; or "less far on every seed, at most half on 95 %".
  - **(d)** at least 7 of 10. That false-fails 0.46 % at p 0.075, and the no-limit known-wrong (1 of 80 inside) still fails it with probability 1 − 5e-12.
  - **The hover's kick:** the spec says "at most 0.1 s", the test asserts under 0.3 s, and 0.10 s is seeds 1–40's largest (mine reach 0.04 s). State one bound and assert it.
  - **Suggested:** `flightbench --shake` prints its table and always exits 0, so the "on 40 seeds a cell" statements are checked by eye only. Let it assert them and exit 1 on a miss; the run takes 12 s.

### Not blocking

**N1. The trunk's texts keep pre-1.8 numbers.** Owner: the orchestrator.
- **D-051** (`docs/decisions.md:461–465`) still says:
  - "from 11–16° to 1.5–3.4°"; 1.8 measures 2.7–10.0° with the limit and 0.1–3.2° without
  - "the empty drone's 0.3–0.7 s (40 of 40)"; it is now 77 of 80
  - "the 6–15° tilt applies to a full punch only"; 1.8 drops the tilt band
  - the heavy band "is re-measured … before it is stated"; it is now stated, as the arrest's length and its medians

  Amend D-051 or add a new entry.
- **`docs/pilot-questions.md:70`:** "the tilt (6–15° on a full punch)".
- **`docs/requirements.md:819` (PHY-10):** "Its occurrence comes out of the physics (wash re-ingestion, frame resonance …, payload and spool pendulum)". D-050 removed the pendulum, and D-051 names the current limit.

**N2. Spec and design wording.** Owner: the physics engineer.
- **The spec:** "the frame's modes and the loads' strapped mounts adding the 50–200 Hz band". The mounts are at 27.6–35.8 Hz, below the band; the arms carry it.
- **Design 5's as-built:** it gives "the bottle's mount 27.6–30.0 Hz, the coil's 34.1–36.2 Hz, the arms 48.0, 54.5, 61.2 and 63.4 Hz", partly model 14's values. `--scenarios` prints 27.6, 29.4, 29.5, 34.1, 35.7, 35.8, 48.0, 54.5, 60.9 and 63.0 Hz.

**N3. `YankFloorDeg` (0.03°) has almost no margin.** It sits just above seeds 1–30's largest miss (0.026°), and seeds 31–60 reach 0.0289°. The code derives the transients' order as about 0.04°; using that value gives the margin. Same root as B1.

**N4. The branch is 14 trunk commits behind** (`2116ec6f`: docs and `tools/build_latest.*`). `git merge-tree` merges them cleanly.

## PHY-10 as restated, against the 40-seed table and on my seeds

`flightbench --shake` reproduces 1.8's table on seeds 1–40:
- the medians
- the 0.3–0.7 s counts: 37/40, 40/40, 4/40, 34/40, 3/40, 0/40
- the tilt ranges, "at least 2.3 times" and "at most 0.43"
- the rigid airframe's ratio, 0.98–1.04

My harness flies the same 37 cells on seeds 301–340 (1480 flights):

| Claim (spec) | Seeds 1–40 (`--shake`) | Seeds 301–340 (QA) |
|---|---|---|
| From the hover, a gentle increase never shows it | 80/80 | 80/80 |
| A sharp one only the kick, at most 0.1 s; the ESCs limited under 0.1 s | at most 0.10 s | at most 0.04 s; 0.05 s |
| Every sharp arrest of a descent shows it | full 240/240; 0.8 240/240 | full 240/240; 0.8 **237/240** (B1a) |
| An ESC meets its limit; the frame's band rings (over 0.3 m/s²) | all | all (Y 0.55 m/s² at the least) |
| Gentle ramp from 5–9 m/s: in its ring over 1 s | 160/160 | 160/160 (1.36 s at the least) |
| Gentle ramp: an ESC limited at most half the punch's time | 160/160 (at most 0.43) | **159/160** (0.56, B1b) |
| A full punch holds an ESC through the arrest, all four for part of it | 240/240 | 240/240 (within 0.005 s; all four 0.25–1.29 s) |
| The shake never outlasts the arrest by more than `Settle` (0.31 s) | at most 0.26 s | at most 0.25 s |
| Medians: empty 0.44/0.64/0.82 s; 2 kg 0.66/0.96/1.32 s | as stated | empty 0.42/0.62/0.82; 2 kg 0.57/0.94/1.28 |
| Empty from 2.5–5.3 m/s: 0.3–0.7 s on most flights and at the median | 77/80, both medians inside | 77/80; medians 0.42 and 0.62 |
| The 0.8 step's ESCs leave sooner | 240/240 | 240/240 |
| The 0.8 step's shake is shorter | 229/240 pairs | 231/240 pairs; shorter summed in every cell |
| No limit: no ESC holds the arrest | 240/240 | 240/240 |
| No limit: it tilts less than half as far | 240/240 | **239/240** (0.55, B1c) |
| Rigid: within 20 %, as long, no `CameraVibration` | 0.98–1.04; within 0.06 s | 0.97–1.03; within 0.04 s; 80/80 |
| It varies between occurrences | yes | neighbouring seeds differ, 39/39 in every cell |
| Rings off: the tilt against the punch's (they coincide, not cause it) | 0.62–1.70 | 0.42–2.19 |

`TheShakeLastsAboutTheArrest`'s statistic on four groups of 10 of my seeds:
- Seeds 301–310, 321–330 and 331–340 pass.
- Seeds 311–320 fail from 2.5 m/s: 7 of 10 inside, median 0.38 s.
- The no-limit known-wrong fails in all four groups (0 of 10 inside).

## "Never buzzes": the in-phase bound

- **The reasoning holds:**
  - Each rotor's unbalance is a pull U·ω² turning at its own speed. Its moment on each body axis is one sinusoid, so the body rate's step-to-step change on that axis is a sinusoid of amplitude α·2 sin(ω·dt/2)/ω.
  - The four rotors run 1–5 Hz apart. Their sinusoids add to one carrier near 145 Hz whose envelope never passes the sum of their amplitudes.
  - A 1 s block holds about 145 carrier cycles, so its mean square is at most (ΣA)²/2 per axis. The sum-frequency term adds about 1e-3 of that (of order 1/(ωT)).
  - This bounds the rigid body: on mean axes the modes leave its motion unchanged.
- **Two approximations,** both small against the measured margin:
  - The prediction uses the hover's mean speed for all four rotors; a rotor 2 % fast pulls about 4 % harder.
  - It leaves out the cross inertia (I_yz −6.5e-5 against 0.06 kg·m²).
- **Measured:** seeds 31–60 reach 0.52–0.86 of the bound (1–30: 0.45–0.86). Against the old quadrature sum they reach 0.98–1.65, so the old 1.42 × bound would have failed some.
- **It fails a known-wrong sharply.** I swept the D gains (and D min) on seeds 1 and 31:

  | D gains | Busiest block / bound | ESCs' largest step |
  |---|---|---|
  | × 1.5 to × 3 | 0.71–0.82 | about 1e-7 |
  | × 3.5 (buzzes) | 22–30 | 1.1e-2 |
  | × 4 (buzzes) | 25–34 | 1.3e-2 |

  The loop jumps from no amplification to a full buzz between × 3 and × 3.5, so there is no middle case for the bound to miss.

## D-050's layout

- **The plates,** from p5-drone's generator (`origin/unity/art/p5-drone`, `plate_heights`). With the 6 mm arm centred at y 0, the 34 mm stack holds:
  - the bottom plate at −0.006 to −0.003
  - the top plate at +0.003 to +0.005
  - the upper plate at +0.025 to +0.028
- **The electronics:** the box (0.005 to 0.025, centred at 0.015) fills the gap between the top and upper plates. The camera's lens at (0, 0.015, 0.115) sits in it, matching the generator's `camera.lens_*` (0.015 and 0.115).
- **The bottle:** Ø0.080 on the upper plate's top at 0.028, so its axis is at 0.068. Its three contact shapes are at 0.068 with radius 0.040.
  - At the hubs' plane (y 0.035) the capsule is 0.045 m wide.
  - In plan, it comes within 0.0248 m of the nearest 10-inch disc (hub at ±0.160, ±0.160; R 0.127): the stated 25 mm.
- **The coil beneath:**
  - its axis at −0.053, 0.047 below the bottom plate's underside
  - its top at −0.0115, 5.5 mm clear of the plate
  - 8.5 mm clear of the packs' inner faces
  - `legs_off.moved` is empty
- **The fit's diff** (`134e899a`) is exactly what `WithModel14Fit` puts back:
  - the electronics' box and capsule
  - the bottle's height and shapes
  - the centre of pressure, −0.01 → −0.018 m, derived in its note

## PHY-12

I built the mass independently in Python: boxes and cylinders from `drone.json`'s parts, summed by the parallel-axis theorem, no sim code. The file as written is L4 (the 1 kg bottle, the half-wound coil):

| L4 | Mass kg | CoM y, z m | Roll | Pitch | Yaw kg·m² | Pitch/roll | Yaw/roll |
|---|---|---|---|---|---|---|---|
| Legs on | 4.832 | −0.0110, +0.0005 | 0.0445 | 0.0474 | 0.0599 | 1.065 | 1.347 |
| Legs off | 4.716 | −0.0063 | 0.0384 | 0.0410 | 0.0572 | 1.069 | 1.492 |

- Legs on, I_yz is −6.51e-5.
- Every figure equals `MassBuildTests`' L4 row to the digit it pins.
- The other six loadouts are pinned the same way and asserted with yaw saturating first (265 of 265).
- K§5.3's 1.0–1.3 yaw/roll now holds only on L6 and L7. D-048 accepts that: the ratios are computed, not targeted.

## The golden chain

| Link | Kept as | My run |
|---|---|---|
| Trunk (model 6) ← model 7 | `AtTheTrunksEscLagEachGoldenIsModel6s` | `0x3F13A5CF5DB7DAFD`, `0x3EE12C8D692BADF3`, `0xE3178EE7FF322698`: the three hashes the trunk at `2116ec6f` pins |
| Model 7 ← … ← model 12 | The kept audits, on `Fixtures/drone-model13.json` | All match. The fixture is byte for byte `a5884ea7`'s `drone.json` |
| Model 13 ← model 14 (the sling strapped, D-050's file) | 1.7's temporary experiment; it cannot be kept, since the code has no sling | **Reproduced.** HEAD's code with only the sling put back flies model 13's file to model 13's three hashes: `0x0F843363CA8DBFBA`, `0xD1EA300928A02EEE`, `0xB371E06FF28075D8`. Strapped, the same file moves powered and contact (`0x67E07B3A8FCBB009`, `0x9A44DD9971654040`) but not the tumble, which has no modes. So the strapped payload is the code's only move |
| Model 14 ← model 15 (the electronics fit) | `OnModel14sFitEachGoldenIsModel14s` | `0x381ABAE6909C139F`, `0xB57132DE1812820D`, `0x26CDDB5EDB155896`: the hashes `263528dc` pinned |
| Model 15 | The pins | `0xCCECAFB7466D6ADE`, `0xC378E1EFFB9AEC93`, `0xFB841727FADDC7A7` |

- **No stored input changed.** Since `a5884ea7`, `GoldenFlights.cs` changes only in its notes, its pins and the two `With…` helpers; its scripts and setups are untouched.
- **Every moved case has a cause shown by experiment.**
- **The runtime's diff since my first review:**
  - the sling removed from `AirframeModes`
  - the lens read from `camera.lens_m`; model 13's file has none, so it keeps the old default
  - a comment in `Aero.cs`
  - the log's fields

## Log format 12

- **The raw walk** of my recorded log:
  - magic `FPV450LG`, format 12
  - a 28 954-byte header (28 970 with the prologue); the walk ends exactly at its end
  - entries in order:
    - 1–5
    - 10–13
    - 16 asymmetry, 49 B
    - 17 vortex ring, 105 B
    - 18 ground effect, 65 B
    - 19 flapping, 527 B
    - **20 modes, 129 B** (format 11's was 161 B; the 32 bytes less are the four sling fields)
    - 14
    - 6–9
    - 15 input, 18 B
- **The modes entry decoded,** 129 of 129 bytes:
  - a presence byte
  - the arms: 55 GPa, 25 × 6 mm, 0.18 m, tip share 0.2357, ζ 0.02
  - the mounts: 30 000 N/m, ζ 0.1
  - the camera at (0, 0.015, 0.115)
  - four tip masses of 0.140079 kg
  - payload part 17, coil part 16
  - no sling, swing, bottom-plate or gravity field
- **The suite** checks that `ModesFieldsLogged` equals `AirframeModesSpec`'s fields.
- **`--summary`** reads "format 12, model 15" and prints one line per airframe effect. The modes' line names the mounts and the camera, and no slings.

## Replay

- **Recorded** with `replay --record --realtime --wind windy`:
  - 120 042 steps and 347 events
  - 37 scheduler hitches, 0 log waits, 0 stick reports dropped
  - landed at 0.483 m/s, no crash
- **Replayed:** 120 042 steps and 310 events (347 less the 37 hitches), bit for bit, exit 0.

## The budget

`flightbench --bench`, alone on the machine (CPU at 8 % before it): AC, Balanced plan, best power efficiency mode, CPU 3201/3201 MHz.

| Case | On: p50 / p99 / max µs | Off: p50 / p99 µs | Effects at p50 | Queries p99 a step | Allocation |
|---|---|---|---|---|---|
| On the stand | 56.9 / 131.8 / 1180.8 | 53.2 / 117.5 | +3.7 (6.5 %) | 99.5 µs | 0 B |
| The close hover | 13.2 / 29.8 / 697.5 | 10.5 / 23.6 | +2.7 (20.5 %) | 8.0 µs | 0 B |
| In the vortex ring (all four latched 100 %) | 12.2 / 29.7 / 873.8 | 9.8 / 20.7 | +2.4 (19.7 %) | 7.3 µs | 0 B |
| The powered flight | 18.3 / 90.5 / 913.1 | — | — | 11.6 µs | 0 B |

- The worst p99 is 131.8 µs, 44 % of PLT-04's 300 µs; my first review measured 129.1.
- The stand's queries take 99.5 µs a step against the 60 µs line. That is the world query's (finding 7). 1.8's 147.5 µs was taken under other agents' load.

## Spec scenarios (`airframe-effects`) on this pass

| Scenario | Result |
|---|---|
| Asymmetry on and off | **Pass:** the suite's seeds 1–5 and 1–30, and mine 31–60 |
| The cushion and the hole | **Pass:** 1.20866 against 1.21008 |
| The ceiling under a roof | **Pass:** 1.08030 against 1.08080 |
| The close hover is never still | **Pass:** 1.48e-2–2.49e-2 rad/s against the 1e-3 floor. The known-wrong gives 2.9e-7–1.2e-6, and the 3 m hover 4.6e-7–1.7e-6 |
| The hub moment against its published form | **Pass:** worst error 2.54e-16 |
| A gust step tilts the drone harder | **Pass:** pitch × 2.48 and roll × 2.52, tilting away from the gust |
| Heavy on low throttle | **Pass:** the suite's seeds 101–120, and mine 301–340 (40/40) |
| Shake only when its causes meet | **Fails in part** on my seeds: an 0.8 step showing it (237/240) and the gentle ramp's ESC share (159/160), B1(a) and (b). Every other clause holds on every flight |
| The shake lasts as long as the arrest | **Pass** on my seeds: it lasts the arrest on 240/240, the medians and the pilot's band hold, and the 0.8 step's ESCs leave sooner. Its asserted 10-seed test false-fails 3.4 % of the time (B1(d)) |
| The visible shake is the rigid body's | **Fails in part** on my seeds: "less than half as far" holds on 239/240 (B1(c)). The rigid airframe and `CameraVibration` clauses pass |
| The heavy corner at low charge | **Pass** (the suite) |
| The inertia character on the seven loadouts | **Pass;** L4 matched by my own build |

## Requirements

| ID | Status |
|---|---|
| PHY-07 | Met |
| PHY-14 | Met |
| PHY-08 | Partly met: the test range is 1.3b |
| PHY-09 | Met; its statistic now sized (finding 4 fixed) |
| PHY-10 | Partly met. It arises from its cause, lasts the arrest, varies and publishes `CameraVibration`. Three stated bounds and one test threshold are sized at their seeds' extremes (B1) |
| PHY-11 | Met |
| PHY-12 | Met, matched independently on L4 |
| PLT-04 | Partly met: p99 ≤ 300 µs and 0 B with every effect on. The queries' 60 µs line is the world query's (finding 7) |

## Commands and output (trimmed)

```
$ dotnet test tools/simtests
Passed!  - Failed:     0, Passed:   265, Skipped:     0, Total:   265, Duration: 4 m 35 s - Fpv450.Sim.Tests.dll (net10.0)

$ dotnet run -c Release --project tools/flightbench -- --golden          # exit 0
powered-flight: 10000 steps, hash 0xCCECAFB7466D6ADE (matches), armed True ...: PASS
contact-flight: 12000 steps, hash 0xC378E1EFFB9AEC93 (matches), 5 impacts, ends 0.2519 m up on 8 touching pairs: PASS
tumble: ... hash 0xFB841727FADDC7A7 (matches), momentum within 7.07E-013, energy within 1.43E-012, ... 5.546 rad/s: PASS
asymmetry off ... rings off ... ground effect off ... flapping off ... model 11's pack table ... modes off ...: PASS (each)
model 14's fit: powered 0x381ABAE6909C139F, contact 0xB57132DE1812820D, tumble 0x26CDDB5EDB155896 (model 14's, bit for bit): PASS
the trunk's 5 ms ESC lag, every mechanism off, over model 7's layout: powered 0x3F13A5CF5DB7DAFD, contact 0x3EE12C8D692BADF3, tumble 0xE3178EE7FF322698 (the trunk's (model 6's), bit for bit): PASS

$ dotnet run -c Release --project tools/flightbench -- --scenarios       # exit 0, 46 rows PASS, every known-wrong "fails, as it must"
  the airframe's modes at 1 kg: 27.6, 29.4, 29.5, 34.1, 35.7, 35.8, 48.0, 54.5, 60.9, 63.0 Hz
  on seeds 101-120: heavy on low throttle, the higher throttle, the known-wrong failing: 20, 20 and 20 of 20; the wobble 1.75-8.65 x ...: PASS
scenarios: PASS
$ dotnet run -c Release --project tools/flightbench -- --contacts        # contacts: PASS, exit 0
$ dotnet run -c Release --project tools/flightbench -- --shake           # exit 0 (prints only), 12 s; reproduces 1.8's table

$ dotnet run -c Release --project tools/flightbench -- --bench           # exit 0
flightbench: 7 bench cases registered; AC, Balanced plan, best power efficiency mode, CPU 3201/3201 MHz
airframe: on the stand ... p50 56.90 µs, p99 131.80 µs, max 1180.8 µs; off ... p50 53.20 µs, p99 117.50 µs; ... 0 B; world queries 75.7 % (45.0 calls a step; p99 99.5 µs ...): PASS
airframe: the close hover ... p50 13.20 µs, p99 29.80 µs ...: PASS
airframe: in the vortex ring ... p50 12.20 µs, p99 29.70 µs ...: PASS
powered-flight: ... p50 18.30 µs, p99 90.50 µs ... 0 B: PASS

$ dotnet run -c Release --project tools/replay -- --record <scratch>/qa-flight.fpvlog --realtime --wind windy
record qa-flight.fpvlog: real time on the flight thread, Windy wind, 120042 steps (120.042 s), 347 events, 37 scheduler hitches, 0 log waits, 0 stick reports dropped
$ dotnet run -c Release --project tools/replay -- <scratch>/qa-flight.fpvlog
  match: 120042 steps and 310 events, bit for bit; exit 0 (Match)
$ dotnet run -c Release --project tools/replay -- --summary <scratch>/qa-flight.fpvlog   # "format 12, model 15", one line per effect

$ & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/qa-editmode.xml -logFile - | Out-Host
test-run result="Passed" total="875" passed="875" failed="0" (Mono, 181 s; run with 7.5 GB free; the goldens and every kept audit among them)

$ python tools/hygiene/precommit.py --tree HEAD
1880 files scanned, 0 findings
$ openspec validate p7b-airframe --strict
Change 'p7b-airframe' is valid
```

QA's experiments, from the scratch harness:

```
sling put back, model 13's file: powered 0x0F843363CA8DBFBA MATCH, contact 0xD1EA300928A02EEE MATCH, tumble 0xB371E06FF28075D8 MATCH
HEAD, model 13's file (strapped): powered 0x67E07B3A8FCBB009, contact 0x9A44DD9971654040 (moved), tumble 0xB371E06FF28075D8 (same)
shake, seeds 301-340, 1480 flights: 4 claims missed:
  2 kg 0.8 steps not shown: seeds 314, 318 (2.56 m/s), 324 (5.43 m/s), peaks 0.067-0.077 deg against 0.088
  seed 321, 0 kg from 8.98 m/s, gentle ramp: ESC 0.43 s against the punch's 0.76 s (0.56)
  seed 333, 0 kg from 9.0 m/s, no limit: tilt 1.31 deg against the punch's 2.38 (0.55)
  the 10-seed band statistic: 311-320 FAIL (7/10, median 0.38); 301-310, 321-330, 331-340 pass; no-limit 0/10 in each
vrs, seeds 301-340: heavy 40/40, higher 40/40, known-wrong fails 40/40; wobble 1.67-24.92 x the ringless, against 1.5
asym, seeds 31-60: AsPredicted 30/30, Steady 30/30; busiest 0.52-0.86 of the in-phase bound; largest miss beyond 30 % 0.0289 deg
  D x 3: Steady, 0.74-0.79 of the bound; D x 3.5: buzzes, 22-30 x, ESC step 1.1e-2; D x 4: 25-34 x
mass build (python, independent), L4: 4.832 kg, CoM y -0.0110, roll 0.0445, pitch 0.0474, yaw 0.0599, 1.065, 1.347; legs off 4.716, 0.0384, 0.0410, 0.0572, 1.069, 1.492
header walk: format 12, 28954 B, tag 20 modes 129 B, ends exactly
```

## Hygiene

- **Commits:** 30 first-parent commits since `a5884ea7`: 29 Physics Engineer and my first review. All are subject-only and at most 72 characters, with no body, trailer or co-author line. The two merges are subject-only.
- **Staging:** the branch's diff holds no file from `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/`, and no video.
- **OPSEC:** the scan finds 0 findings over 1880 files. The new notes cite the drone model and published sources only.
- **Licences:** no third-party file added.
- **Determinism:** the runtime's changes since my first review use basic operations only (the mounts' k and `DetMath.Sqrt`). Every bench row allocates 0 B in steady state.

# Third pass at `a59022fb` (task 2.1: B1 and N2–N4 re-checked)

**Verdict: approved.** B1 and N2–N4 are fixed, and nothing blocks. Two follow-ups remain, neither a model, golden or log change: N5 (the rigid airframe's bench check) and N6 (PHY-07's band stated as a rate).
- Every restated PHY-10 claim holds on `--shake 2001` and `--shake 7001`. It also holds on 12 more fresh groups (5001–5480): 560 fresh seeds a cell, 20 720 flights.
- The "every" claims hold on every flight, with a margin. The rates hold on 98.6–99.9 % of flights, against "nearly every"'s 95 %.
- One group in 14 fails one rate check: `--shake 5161`, the rigid airframe "within three fields", 37 of 40 against 38 (N5).
- `TheShakeLastsAboutTheArrest`'s 7 of 10 false-fails about 0.4 % of the time, and it still rejects the known-wrong.
- **Owed:** Unity EditMode on this branch (the edited tests on Mono). It is not run here, under the pilot's no-GPU rule. No runtime file has changed since my re-review, whose EditMode run passed 875 of 875.

**Scope:** `unity/physics/p7b-airframe` at `a59022fb`: task 1.9 (`4e25472e`, `7fe2c2ce`, `1e99eeba`, `a59022fb`) and the trunk merge `f07ad86a`.
- `git diff 73281683..HEAD` touches nothing under the core's `Runtime/` or `unity/Assets`.
- It stays model 15, log format 12.

**How I checked:**
- I ran every command below in this worktree. No GPU was used: no Unity, Blender, `--bench` or `replay --realtime`.
- I used two scratch harnesses, never committed. They compile the suite's scenario files unchanged. One flies PHY-07's per-seed checks on any seeds; the other prints the airframe's modes by loadout.

## The re-review's findings, answered

| # | Finding | Answer (1.9) | Verdict |
|---|---|---|---|
| B1(a) | "Every sharp arrest shows it" missed 3 of 240 0.8 steps | Every sharp arrest meets a limit and rings; nearly every one shows it | **Fixed.** On my seeds, meets and rings: 6720/6720. Shows: full punches 3357/3360, 0.8 steps 3341/3360 |
| B1(b) | The gentle ramp's half share missed (0.56) | Every ramp is limited for less than the punch's time; nearly every one for at most half | **Fixed.** Less: 2240/2240 (0.59 at most, seed 2040). Half: 2235/2240 |
| B1(c) | The no-limit "less than half" missed (0.55) | Less far on every flight; less than half on nearly every one | **Fixed.** Less far: 3360/3360. Half: 3351/3360 |
| B1(d) | 8 of 10 false-failed 3.4 % of the time | 7 of 10 and the median (`PilotBand`) | **Fixed.** See below |
| B1, the kick | The spec said 0.1 s and the test 0.3 s | One bound: `KickSeconds`, 0.3 s, the pilot's shortest shake | **Fixed.** On 2240 sharp steps from the hover, the kick lasts 0.14 s at most and the ESCs leave their limit within 0.05 s |
| B1, suggested | `--shake` printed and exited 0 | It checks each claim and exits 1 on a miss | **Done.** `--shake 5161` exits 1 on its one miss |
| N2 | Spec and design wording on the modes | The arms (48–63 Hz) carry the 50–200 Hz band, the mounts (28–36 Hz) the band below; design 5 lists model 15's ten modes | **Fixed.** My harness prints the same ten: 1 kg 27.6, 29.4, 29.5, 34.1, 35.7, 35.8, 48.0, 54.5, 60.9, 63.0 Hz; no bottle 31.2, 31.8, 31.8, 48.0, 53.6, 61.5, 62.1 Hz; 2 kg 22.4–33.5 and 48.0–62.1 Hz. The attribution holds by experiment: without the bottle three of the low six go and the four arms stay; at 2 kg the low six fall and the arms move 3 Hz at most |
| N3 | `YankFloorDeg` 0.03° had no margin | 0.04°, derived | **Fixed.** The arithmetic holds: 6e-5 × 2 × 16 / 0.060 = 0.032 rad/s (1.83 °/s), over 0.02 s that is 0.037°. 2 % of 920 rad/s is 18.4 rather than 16, which gives 0.042°: the same order. On my 300 fresh seeds `AsPredicted` holds on 298 (2065 misses by 0.046° beyond 30 %, 7085 by 0.077°). That is an order, not a bound, as the code now says |
| N4 | The branch was 14 trunk commits behind | `unity/main` at `3d35914` merged with `-m` | **Fixed.** Re-merging `73281683` and `3d359146` gives tree `44bfee7f`, the merge's exactly: no conflict, subject-only. The trunk has since moved 4 commits (`19d6d6f8`, one handover doc only), and `git merge-tree` merges it clean |
| N1 | Stale numbers in the trunk's texts | The orchestrator's D-053 | **Fixed** on the trunk and merged: PHY-10 and G7's tilt question cite D-053 (2.7–10°), and the pendulum is gone |

## PHY-10 as restated, on my fresh seeds

**Seeds:** 2001–2040, 7001–7040 and 5001–5480, 40 seeds a cell in 14 runs. None of them overlaps 1.9's (1–40, 301–500, 1001–1200).

| Claim (spec) | As | Mine: 560 seeds a cell |
|---|---|---|
| From the hover, a gentle increase never shows it | every | 1120/1120; peak 0.001° at most, against the visible 0.088° |
| A sharp step only kicks, under 0.3 s, the ESCs off their limit within 0.1 s | every | 2240 steps: 0.14 s at most; 0.05 s |
| Every sharp arrest meets a limit and rings over 0.3 m/s² | every | 6720/6720; limited 0.24 s and Y 0.54 m/s² at the least |
| Nearly every full punch shows it | ≥ 95 % | 3357/3360 (99.9 %) |
| Nearly every 0.8 step shows it | ≥ 95 % | 3341/3360 (99.4 %) |
| Every gentle ramp from 5–9 m/s wobbles: ring over 1 s, the ESC limited for less than the punch | every | 2240/2240; ring 1.36 s at the least, share 0.59 at most |
| …at most half the punch's time | ≥ 95 % | 2235/2240 |
| Every full punch holds the arrest, all four for its first part, and ends within `Settle` | every | 3360/3360; within 0.005 s; all four 0.19 s at the least; the end +0.26 s at most, against 0.31 |
| The medians rise with the descent and the payload | every group | 14/14. Empty: 0.42–0.44 / 0.60–0.66 / 0.78–0.84 s. 2 kg: 0.58–0.70 / 0.90–0.96 / 1.28–1.32 s |
| Most of the empty drone's 2.5–5.3 m/s punches are inside 0.3–0.7 s, and the median | ≥ 70 % and the median | 2.5 m/s: 520/560 (92.9 %), medians 0.42–0.44. 5.3 m/s: 548/560, medians 0.60–0.66. No ten-seed group of 112 fails 7 of 10 |
| Nearly every 0.8 step lets go sooner | ≥ 95 % | 3360/3360; 0.018 s sooner at the least |
| The 0.8 steps are shorter in sum | every cell | 84/84 cells |
| No limit: no ESC holds the arrest | every | 3360/3360 |
| No limit: it tilts less far | every | 3360/3360; 0.76 of the punch's tilt at most (seed 7010, empty from 9 m/s), medians 0.14–0.16 |
| No limit: less than half as far | ≥ 95 % | 3351/3360 |
| Rigid: within 20 %, holding the arrest, publishing no vibration | every | 560/560; 0.92–1.15 |
| Rigid: the same median, within three fields | the median; ≥ 95 % | Medians equal, 14/14. Within three fields: 552/560 (98.6 %). **5161–5200: 37/40, the run exits 1** (N5) |
| It varies; the modes publish `CameraVibration`, the rigid airframe never | every | 14/14; 20 720/20 720 |

**The pilot's band test (B1(d)), `TheShakeLastsAboutTheArrest`:** at least 7 of 10 inside, and the median (`PilotBand`).
- **False failures:** the 2.5 m/s cell leaves the band on 40 of my 560 seeds, 74 of 1000 with 1.9's. At p = 0.074 a fresh ten fails with P(4 or more out) ≈ 0.43 %. My 112 groups of ten pass, as do 1.9's 88.
- **The known-wrong:** the ESC that senses no current is inside on 14 of 560 from 2.5 m/s. It passes 7 of 10 with a probability of about 7e-10, so the test still rejects it.
- **Sized:** the threshold is set by its false-failure rate and fails its known-wrong.

**The thinnest every-flight margins:**
- The no-limit tilt: 0.76 against 1, up from 1.9's 0.67.
- The shake's end: +0.26 s against `Settle`'s 0.31, steady at +0.25–0.27 over every group flown.

Both hold on every flight seen, 6000 for the tilt.

## Findings

### Blocking

None.

### Not blocking

**N5. The rigid airframe's "within three fields" check false-fails a fresh `--shake` about 1 % of the time, not 0.5 %.** Owner: the physics engineer.
- **The count:** it is one cell of 40, and "nearly every" needs 38 of 40. My seeds miss on 8 of 560, 1.9's on 4 of 440: 12 of 1000.
- **The rate:** at p = 0.012, three or more misses in 40 come up 1.2 % of the time. 5161–5200 is one such group: 37 of 40, the medians equal (0.96 s), 0.20 s apart at most.
- **The comment:** `ShakeScenario.NearlyEvery`'s note says "about 0.5 % of the time at most".
- **The claim itself holds:** 98.6 % against 95 %. Only the 40-flight check is too short.
- **The fix:** fly the rigid cell on 160 seeds (about a second more), or ask 37 of 40 (0.13 % at p 0.012). Either way, state the rate in the comment.

**N6. PHY-07's "inside the computed band on every seed", and `AsPredicted`, miss on about 1–3 % of fresh seeds by construction (the physics engineer's note).** Owner: the physics engineer, placed by the orchestrator.
- **My check:** fresh seeds 2001–2100 and 7001–7200, the scenario's code unchanged.
  - `InBand`: 298/300. Seed 2035's yaw is 9.1e-5 against the floor's 1.16e-4, predicted 5.0e-4. Seed 2052's roll is 2.2e-5 against 1.54e-4, predicted 2.1e-4.
  - `AsPredicted`: 298/300.
  - `Steady` and "off, halved": 300/300.
  - The suite's seeds 1–5 pass all four.
  - With 1.9's 184/190, the band holds on 482 of 490 (98.4 %).
- **Judged a follow-up, not a block:**
  - **Different from B1:** the band is not a bound set at its seeds' extremes. It is each axis's 0.5–99.5 % of the linear model over 20 000 draws of the tolerances, computed before any flight. About 2 % of flights fall outside it by design (two axes), and 98.4 % coverage is that design confirmed.
  - **Where it misses:** both of my misses sit at the band's floor, where the measured rate is near zero and the linear model's absolute error is largest.
  - **No flaky test:** the suite asserts it on fixed seeds, so nothing flakes.
  - **No pilot-facing number:** no pilot number or G7 question rests on it.
  - **Nothing to change in the physics:** no model, golden or log change is needed.
- **The defect is the wording:** "on every seed" is a universal claim. A fresh five seeds would fail the scenario as worded about 8 % of the time.
- **The fix:**
  - Restate the scenario under D-053's rule: inside the band on nearly every seed, the band being each axis's 0.5–99.5 %.
  - Add a fresh-seed coverage check, a `flightbench` mode with a first seed like `--shake`'s, which the identical-rotors known-wrong fails.
  - Keep `AsPredicted` as the order the code now calls it.

## Spec scenarios (`airframe-effects`) on this pass

| Scenario | Result |
|---|---|
| Asymmetry on and off | **Pass** on the suite's five seeds. On fresh seeds the band holds on 98.4 % (N6) |
| The cushion and the hole; the ceiling under a roof; the close hover is never still; the hub moment; a gust step | **Pass** (`--scenarios`; no runtime change since the re-review) |
| Heavy on low throttle | **Pass** (`--scenarios`, seeds 101–120) |
| Shake only when its causes meet | **Pass** on 560 fresh seeds a cell, every clause as restated |
| The shake lasts as long as the arrest | **Pass** on 560 fresh seeds a cell; the pilot's band test sized at about 0.4 % |
| The visible shake is the rigid body's | **Pass** on 560 fresh seeds a cell. One 40-seed group fails the bench's three-field check, a check too short for its claim (N5) |
| The heavy corner at low charge; the inertia character | **Pass** (the suite) |

## Requirements

| ID | Status |
|---|---|
| PHY-07 | Met. The band's "every seed" wording is a follow-up (N6) |
| PHY-08, PHY-09, PHY-11, PHY-12, PHY-14 | As in the re-review: met, and PHY-08 partly (its test range is 1.3b) |
| PHY-10 | Met. It arises from its cause, lasts the arrest, varies and publishes `CameraVibration`. Every claim is stated as it holds and checked on fresh seeds |
| PLT-04 | As in the re-review (no runtime change; `--bench` is not re-run under the no-GPU rule) |

## Commands and output (trimmed)

```
$ dotnet test tools/simtests
Passed!  - Failed:     0, Passed:   265, Skipped:     0, Total:   265, Duration: 4 m 35 s - Fpv450.Sim.Tests.dll (net10.0)

$ dotnet run -c Release --project tools/flightbench -- --golden          # exit 0
powered-flight: 10000 steps, hash 0xCCECAFB7466D6ADE (matches) ...
contact-flight: 12000 steps, hash 0xC378E1EFFB9AEC93 (matches), 5 impacts, ends 0.2519 m up on 8 touching pairs: PASS
tumble: ... hash 0xFB841727FADDC7A7 (matches) ...
asymmetry off, rings off, ground effect off, flapping off, model 11's pack table, modes off, model 14's fit: PASS (each, bit for bit)
the trunk's 5 ms ESC lag ...: powered 0x3F13A5CF5DB7DAFD, contact 0x3EE12C8D692BADF3, tumble 0xE3178EE7FF322698 (the trunk's (model 6's), bit for bit): PASS
$ dotnet run -c Release --project tools/flightbench -- --scenarios       # exit 0; 23 known-wrongs "fails, as it must", the 6 no-limit rows among them
  the airframe's modes at 1 kg: 27.6, 29.4, 29.5, 34.1, 35.7, 35.8, 48.0, 54.5, 60.9, 63.0 Hz
scenarios: PASS
$ dotnet run -c Release --project tools/flightbench -- --contacts        # contacts: PASS, exit 0
$ dotnet run -c Release --project tools/flightbench -- --shake           # exit 0; 20 claims PASS on seeds 1-40
  its length follows the arrest: ... empty 0.44 / 0.64 / 0.82 s, 2 kg 0.66 / 0.96 / 1.32 s: PASS
shake: PASS
$ dotnet run -c Release --project tools/flightbench -- --shake 2001      # exit 0, 20 claims PASS
$ dotnet run -c Release --project tools/flightbench -- --shake 7001      # exit 0, 20 claims PASS
$ for f in 5001 5041 ... 5441; do flightbench --shake $f; done           # 11 exit 0; 5161 exit 1:
  known-wrong, a rigid airframe: as long, the same median within a field and within three fields on nearly every seed: medians 0.96 and 0.96 s; 37/40 within 0.06 s, 0.20 s apart at most: FAIL

$ python tools/hygiene/precommit.py --tree HEAD
1882 files scanned, 0 findings
$ openspec validate p7b-airframe --strict
Change 'p7b-airframe' is valid
$ git merge-tree --write-tree 73281683 3d359146    # 44bfee7f..., equal to f07ad86a^{tree}
$ git merge-tree --write-tree HEAD 19d6d6f8        # clean
```

QA's scratch harnesses:

```
asymmetry, seeds 2001-2100: InBand 98/100, AsPredicted 99/100, Steady 100/100, off halved 100/100
  seed 2035: BAND(yaw 9.08E-005 below 1.16E-004; predicted 4.98E-004); seed 2052: BAND(roll 2.23E-005 below 1.54E-004; predicted 2.11E-004)
  seed 2065: ASPRED(beyond 30 % 0.0464 deg)
asymmetry, seeds 7001-7200: InBand 200/200, AsPredicted 199/200 (7085: 0.0768 deg), Steady 200/200, off halved 200/200
asymmetry, seeds 1-5: InBand 5/5, AsPredicted 5/5, Steady 5/5, off halved 5/5
modes: 1 kg 27.6 29.4 29.5 34.1 35.7 35.8 48.0 54.5 60.9 63.0 Hz; 0 kg 31.2 31.8 31.8 48.0 53.6 61.5 62.1 Hz; 2 kg 22.4 25.7 26.3 32.6 33.5 33.5 48.0 54.4 58.0 62.1 Hz
```

## Hygiene

- **Commits:** the five since my re-review (four by the physics engineer, and the merge) are subject-only and 57–67 characters long. Each is under `Physics Engineer <physics@450fpv.local>`, with no body, trailer or co-author line. The merge has its own `-m` subject.
- **Staging:** the branch's diff holds no file from `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/`.
- **OPSEC:** the scan finds 0 findings over 1882 files.
- **Licences:** no third-party file added.
- **Determinism:** no runtime file changed; the goldens and every kept audit are bit for bit.
