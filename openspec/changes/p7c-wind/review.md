# QA review: p7c-wind (task 2.1, landing with 1.5 open under D-058)

**Verdict: changes requested, one small blocker.** The wind is ready to land with task 1.5 open, as D-058 allows, once finding B1 is fixed:
- **The field is physical, seeded and deterministic.** It is a log law, von Kármán modes with vorticity sampled at each hub, the wakes, the canopy strip and the K§8.3 direction model.
- **The tools are ported faithfully.** Their self-checks reject noise and schedules.
- **The cruise is 20.58 m/s.**
- **The merge with the airframe is audited case by case.** I reproduced the model 10 and the model 15 audits myself, both of them temporary edits. In still air, a steady wind and two gust steps, the merged code flies main's goldens bit for bit, and with main's terms off it flies p7c-wind's model 10 goldens.
- **Replay is bit for bit** with `--canopy`, `--wakes` and rain.
- **D-058's grade reproduces exactly** on seeds 1–10: Severe 9 of 21, Windy 10 of 21, Calm 9 of 9.

**B1 blocks the merge:** one test in the repository's Python suite fails on the branch (`tools/flightbench/tests`). The rest are should-fix items for 1.5 or the archive, not blockers.

**What I could not run:** the orchestrator gave the machine and the GPU back to the pilot mid-review. So Unity EditMode and `flightbench --bench` were not run. Timings on a loaded host would not count anyway. Both are owed (see "Runs still owed").

**Scope:** `unity/physics/p7c-wind` at `0aeb1a09` (model 18, log format 13, record 1808 B), holding `unity/main` at `de8d7399` (p7b-airframe, #163/#164). Tasks 1.1–1.4 and 1.6–1.8, and 1.5 as landed open. `git merge-tree` against today's `origin/unity/main` (`06ad87bb`) merges without conflict, and nothing landed on main since `de8d7399` touches the sim package.

**How I checked:**
- Every command below was run by me. No author's output was used.
- The audits and the seed sets ran in scratch worktrees under `unity/Logs/qa/` (git-ignored), never committed, and removed afterwards. Their logs and grades stay in `unity/Logs/qa/`. They were:
  - `wt-main` at `de8d7399`, with the goldens' air settable
  - `wt-head` at `0aeb1a09`, with an audit test file and a seed-base variable for the harness
  - `wt-calm`, with Calm flown with the asymmetry off
- The review worktree itself was not edited, apart from this file.
- My seeds: the harness on 101–110, 201–210 and 301–310 (the author's are 1–10), and T0c on histories 5001–6600 (the author's are 1–1600).

## Findings

### Blocking

**B1. `test_wind_harness_csv.py` fails on the branch.** Owner: the physics engineer.
- `python -m pytest tools/flightbench/tests -q` reports `1 failed, 1 passed`. The failing test is `test_the_run_reads_as_an_attitude_log`, at `tools/flightbench/tests/test_wind_harness_csv.py:76`: `assert all(ok) and all(oky), "every frame usable on every axis"`.
- **The cause:** since 1.5's belt stretch (`071da3d1`), the harness logs the climb between the stretches at confidence 0, as the tracker logs lost frames (tasks.md 1.5). The 1.2 test still asserts that every frame is usable. 1.5's and the merge's verification lists ran `pytest tools/wind`, not this folder, so it has been red since then.
- **It is part of CLAUDE.md's `python -m pytest tools`.** Landing it puts a failing test on `unity/main`.
- **Fix:** assert what the harness now promises:
  - the unusable frames are exactly one contiguous block after the belt segment, the climb plus `SettleSeconds`
  - every other frame is usable on every axis
  - the belt segment's frames are all usable

  Its docstring's "when task 1.1's port lands, the round trip through the ported tool replaces it" is due too: `tools/wind/tests` now reads the files through the port.

### Should fix before 1.5 closes (not blocking the landing)

**S1. Calm is flown with the airframe's asymmetry on.** PHY-14, K§8.6 and the spec ("with Calm flown with the airframe's asymmetry off") all say Calm is flown with it off. Owner: the physics engineer.
- `WindHarness.Setup` calls `Airframe(spec, setup)` for every level (`Tests/WindHarness.cs:284`). That turned the motors' asymmetry on for Calm at the merge. Before the merge 1.5's note said the airframe "has no asymmetry model yet, so it flies with it off".
- **Experiment** (`wt-calm`: `setup.Asymmetry = null` for Calm, seeds 1–10, every family): Calm still passes 9 of 9 (T0 0.979°, T1 0.029°, T2 0.010°, T3 0.087°, T4 0.019°, T5 0.090° / 0.194°, T6d 0.003°, T11a field 0.053°). As built it reads the same to every printed digit. The asymmetry does move the flights: the two seed-1 logs differ from their first frame, by a few thousandths of a degree of roll (frame 200: 0.2061° against 0.2105°). But no reading moves in its third decimal. So D-058's "Calm 9 of 9" stands. Only the harness departs from the spec.
- **Fix:** fly Calm with `Asymmetry = null`, as the spec says.

**S2. Several targets' pass or fail on 10 seeds is not stable from one seed set to the next, and no false-failure rate is measured (TST-03).** Owner: the physics engineer, with the orchestrator for D-058's counts.
- Severe and Windy on four independent 10-seed sets (seeds 1–10 are the author's; the other three are mine):

  | 10 seeds, every family, `--pilots all` | 1–10 | 101–110 | 201–210 | 301–310 |
  |---|---|---|---|---|
  | **Severe: targets passed** | **9 of 21** | **10** | **8** | **9** |
  | **Windy: targets passed** | **10 of 21** | **10** | **9** | **10** |
  | Severe T0 (6–12°) | 7.17 | 9.19 | 6.67 | 8.80 |
  | Windy T0 (3.1–6.1°) | 3.37 | 4.36 | 3.34 | 4.30 |
  | T0b windward (≥ 9 of 10), Severe / Windy | 7 / 6 | 9 / 9 | 7 / 8 | 10 / 10 |
  | T7 rate (7–24 /min), Severe / Windy | 18.7 / 20.4 | 21.4 / 21.0 | 21.9 / 21.9 | **25.7 / 25.1** |
  | T8 median (1–4 s) / CV (0.5–1.2), Severe | 0.85 / 1.48 | 1.04 / 0.98 | 0.74 / 1.18 | 0.92 / 1.22 |
  | T11a belt, Severe (1.1–2.3°) / Windy (0.55–1.15°) | 1.20 / 0.59 | 1.02 / 0.53 | 0.94 / 0.45 | 1.17 / 0.54 |

- **What it means for D-058:** the counts "9 of 21" and "10 of 21" are one seed set's draw: Severe passes 8–10 of 21 and Windy 9–10 across the four sets. Four targets flip between sets at both levels: T0b, T7, T8 and T11a.
- TST-03 asks that test lengths be sized by their measured false-failure rate. T0c's is measured (40 sets). The harness targets' is not.
- T0 is the tuned target, and its 10-seed mean moves 6.67–9.19° across the four sets against a floor of 6.0. Seeds 201–210 clear the floor by 0.67°. Windy's T0 clears its floor by 0.24° on two sets.
- **Fix:** before 1.5 closes, grade on enough independent sets to state each passing target's false-failure rate, and size the seed count from it, as `p7b-airframe`'s shake numbers were sized (D-053). The final counts are then rates, not one draw.

**S3. T11b's known-wrong is caught by a hair, and by the tilt condition only.** Owner: the physics engineer.
- On every set, the no-drag model passes both of T11b's ratios: s1 0.99–1.01 and s0 0.93–1.32 against ≥ 0.8. It also passes the tilt for s1 (0.89–0.94).
- It fails only on "tilt s0 within ±25 %": 1.28–1.47 at Severe and 1.42–1.85 at Windy, against 1.25. Seeds 101–110 clear it by 0.03.
- No drag is a weak known-wrong for a target about gusts as sideways forces. Its flight is nonsense (129 m/s), and the trade-off ratios survive it.
- PHY-16 and K§8.4 name the model that must fail: sideways gusts that are not forces. Fix: add a known-wrong family with the field acting as moments only, or as pose noise, and show that T11b's ratios fail it.

**S4. The d = 0.2 s pilot variant does not do what 1.5's recorded call says.** Owner: the physics engineer.
- tasks.md 1.5 records the orchestrator's call: the d = 0.2 s pilot "scales its τ on each axis to keep the nominal pilot's delay-to-τ ratio". `WindHarness.Pilots` sets `Delay = 0.2` alone (`Tests/WindHarness.cs:91`), and `Fly` passes no scaled τ (`:324`).
- **On my run** (`--wind-harness severe`, seeds 1–5) the d0.2 pilot does not hold its pitch. It flies 8.0–17.4 m/s at −24.5° to −29.6° of pitch (K§8.5: −22.7 ± 2°) and drifts up to 3.97 m off its height.
- Nothing grades the variant, so no target moves. But wind.md §6.1 says the variants are "reported alongside the nominal pilot", and this one reports a flight no pilot flies.
- **Fix:** implement the call (τ × 2 on each axis for d = 0.2), or record that the variant is flown but not reported.
- In the same place: `grade_levels.py` reports neither the d nor the τ_v variants, and wind.md §6.2's extra runs at 3.6 and 6.5 kg ("reported but not scored") are not flown.

**S5. The trench-lip cavity and the terrain shelter are neither built nor decided.** Owner: the orchestrator (the record), the physics engineer.
- The proposal's What Changes lists "the cavity under a trench lip". PHY-13 says the cavity below a trench lip (WLD-16) is "decided in 7c's design". The design is silent and no task builds it.
- `WindField` takes a hole as sheltered ("not finite in a hole, which the field takes as sheltered until 1.3's cavity", `Runtime/Wind/WindField.cs:455`). 1.3 built a bluff body's cavity, not a trench's.
- PHY-13's "terrain shelter" is not built either (1.3's open list: "terrain heights").
- **Fix:** a design decision (build it in 1.5, or defer it to `p7a-ground` or WLD-16 with a decision entry), and the proposal and the comment to match.

**S6. T0b's "windward in ≥ 4 of 5 runs" becomes ≥ 9 of 10 on 10 seeds.** Owner: the physics engineer.
- `grade_levels.py` asks `windward runs >= runs - 1`, which is 90 % at 10 runs against K§8.6's 80 %. wind.md §6.2 derives the rule's false-failure rate (about 13 % of correct 5-run sets without the start rule) for 5 runs only.
- On the four sets T0b's windward count reads 7, 9, 7 and 10 of 10 at Severe, and 6, 9, 8 and 10 at Windy. Under a proportional rule (8 of 10), Windy's seeds 201–210 would pass. They fail as graded.
- **Fix:** state the rule as a share (≥ 80 %), or size it from its false-failure rate at the run count used.

**S7. Stale texts.** Owner: the physics engineer.
- tasks.md 1.7 still describes `camera.lens_position_m` (0, −0.005, 0.115). The merge made it one point, `camera.lens_m` (0, 0.015, 0.115), as 1.5's merge note says. Mark 1.7's line as superseded.
- `Runtime/World/WakeGrid.cs:146`: "as StandInWind's" names the removed class.
- `Runtime/Flight/FlightCore.cs:89`: "the stand-in wind field (WindField)".
- 1.4's gust-front numbers (pitch 5.5 °/s, yaw 10.7 °/s) are model 6's. At model 18 the scenario reads pitch 4.82 °/s at +116 ms, yaw 2.04 °/s at +65 ms and roll 1.77 °/s, against the uniform field's 0.01 / 0.05 / 1.81 °/s. It still passes (each 3× the uniform field's or more), but yaw's coupling is a fifth of 1.4's note. Restate it at the current model.

**S8. The harness's own check fails `--belt off` on 10 seeds.** Owner: the physics engineer.
- `flightbench --wind-harness severe --seeds 10 --belt off` exits 1 on seeds 1–10. One no-strip run's airspeed is −5.3 %, against the −5 to +8 % band, as tasks.md 1.5 notes.
- The band, like the lean's ±15 % and ±20 %, has no measured false-failure rate. Size it, or report the no-strip family without the pass and fail.

### Observations (no action)

- **The stepped-gust audit and a signed zero.** My first rewrite of the merged record's wind slot into main's gust convention (gust = wind − Mean) differed from main's in the sign of one zero: main's −Mean carries −0 in y. With the slot zeroed on both sides, all four airs fly bit for bit. The authors' rewrite ("−Mean before the onset and 0 after") is the same thing.
- **The counter-based draws are logged by stream, not draw by draw.** The header names `wind.meander`, `wind.shift`, `wind.turbulence` (and `wind.canopy` and `wind.wake` when present), and lists the per-flight draws `wind.from_deg` and `wind.mean_speed_m_s`. The history is a pure function of the seed (`TheDirectionHistoryIsTheSeeds`), so this meets PHY-15's "computable from the seed alone". Each meander and shift draw is not printed.
- **The replay counts one event fewer than the recording.** It reports, for example, 130 events against the end block's 131. That is the scheduler's hitch event (1 hitch per recording), which a replay does not re-raise. The flights match.
- **`p6-flight-core`'s `wind-field` spec** ("A stand-in wind, labelled as such ... until p7c-wind") still describes a class this branch removes. Its archive, before this change's, retires it, as the proposal says.

## Checks run

### The suites and benches (no `--bench`)

```
$ dotnet test tools/simtests
Passed!  - Failed:     0, Passed:   292, Skipped:     0, Total:   292, Duration: 9 m 4 s - Fpv450.Sim.Tests.dll (net10.0)

$ dotnet run -c Release --project tools/flightbench -- --golden
free-body: 10000 steps, hash 0x50ED4B05A5D02F20 (matches) ... PASS
powered-flight: 10000 steps, hash 0xFE81E38EFCBD2F4E (matches), armed True, packs at 0.8949 of 0.9; ... (the same): PASS
contact-flight: 12000 steps, hash 0x1152D64875230761 (matches), 5 impacts, ends 0.2518 m up on 8 touching pairs: PASS
rain-flight: 10000 steps in Calm and Severe rain, hash 0xE5D4C472F6CA12D2 (matches), 18.04 mm/h, film 9.57 of 42.45 g: PASS
tumble: 10000 steps ..., hash 0x2A0283BBFE59FD84 (matches), momentum within 7.07E-013, energy within 1.43E-012 ...: PASS
main's rotor drag in still air, over main's layout: powered 0xABF47E6E55E48EB8, contact 0x557DEBE410FAEEA1, tumble 0xFB841727FADDC7A7 (model 15's, bit for bit): PASS
the tumble over main's layout: model 14's fit 0x26CDDB5EDB155896, model 13's file 0xB371E06FF28075D8 (theirs, bit for bit): PASS

$ flightbench --scenarios   -> scenarios: PASS   (0 FAIL lines)
$ flightbench --contacts    -> contacts: PASS    (a tunnel at 40 m/s caught: 2 tunnel events)
$ flightbench --shake       -> shake: PASS
$ flightbench --wind-harness severe | windy | calm   -> wind harness: PASS (each, 5 seeds)
  severe: nominal seeds 1-5 lean -0.3 to -12.0 % off 1.5's at their crosswind; airspeed 19.75-20.76 m/s (-4.1 to +0.8 %);
  known-wrong: no drag, seed 1: 0.017° per m/s, airspeed 128.75 m/s: fails, as it must

$ dotnet run -c Release --project tools/worldbench -- --golden
  wakes: all 11 cases pass
worldbench: ALL PASS

$ python -m pytest tools/wind -q
63 passed in 248.34s (0:04:08)

$ python -m pytest tools/flightbench/tests -q
FAILED tools/flightbench/tests/test_wind_harness_csv.py::test_the_run_reads_as_an_attitude_log
1 failed, 1 passed in 12.98s                       <- B1

$ BLENDER='C:\nonexistent\blender.exe' python -m pytest tools -q --ignore=tools/blender/tests/test_build_blender.py
FAILED tools/flightbench/tests/test_wind_harness_csv.py::test_the_run_reads_as_an_attitude_log
1 failed, 1416 passed, 1 skipped in 1744.76s (0:29:04)      <- B1 is the only failure

$ python tools/hygiene/precommit.py --tree HEAD    -> 1933 files scanned, 0 findings (exit 0)
$ openspec validate p7c-wind --strict              -> Change 'p7c-wind' is valid (exit 0)
$ python tools/assets/fetch.py --verify            -> OK
```

Scenario lines worth reading from `--scenarios` (model 18):
- **Same seed, same wind** (Severe, 20 s held at 30 m): 20 000 of 20 000 steps bit-identical at all 4 rotors and the body. 400 bearings: χ² 5.5 (uniform under 24.3).
- **The spectrum:** σ_u/U at Windy 0.1320 and at Severe 0.1292; σ_w/σ_u 0.629 and 0.639. At 2–4 Hz at the 20.6 m/s cruise, the variance ratio to von Kármán's is 1.11, and at 1–2 Hz 1.21 (band 0.7–1.4).
- **The cruise:** released at 13 and at 25 m/s, clip P's pitch settles at 20.58 m/s both ways (−22.70°). At a 2 m/s crosswind it leans 1.232° per m/s, against wind.md §5.3's 23.4°/V of 1.136.
- **Rain without wind:** the wind stays Calm's. The rate is 18.04 mm/h, inside 10–50. The drag against the labelled formula (Atlas, Marshall–Palmer, re-implemented in the scenario with `Math`) is worst 1.66E-15. The film is 9.57 g of 42.45 g. At rest, ρ_w·R·V on the top silhouette is 1.996 mN against 1.996.

### Allocation, without timing

A scratch test in `wt-head` counts the flight thread's allocations over 20 000 steps after a 2000-step warm-up. It times nothing, so the loaded host does not matter.

```
QA alloc canopy+rain: 0 B over 20000 steps, 7137 steps inside the strip
    (Severe in Severe rain, the harness's strip, seed 1, as canopy-step flies it)
QA alloc wakes+rain+airframe: 0 B over 20000 steps, worker builds 3, stalls 3, crashed False
    (Severe in Severe rain over WakeGridFixture with the whole airframe, as posadka-step flies it)
```

The second run is unpaced: it flies faster than real time, so the worker's stalls are expected there (tasks.md 1.8 notes the same) and say nothing about the 1 kHz thread. The paced posadka-step is owed.

### Replay, record and match (virtual clock, no `--realtime`)

```
$ replay --record severe-canopy.fpvlog --seconds 60 --wind severe --canopy ; replay severe-canopy.fpvlog
  60000 steps, 133 events, 1 scheduler hitches ... match: 60000 steps and 132 events, bit for bit; exit 0 (Match)
$ replay --record severe-wakes.fpvlog --seconds 60 --wind severe --wakes ; replay ...
  match: 60000 steps and 138 events, bit for bit; exit 0 (Match)
$ replay --record severe-rain.fpvlog --seconds 60 --wind severe --rain severerain ; replay ...
  match: 60000 steps and 134 events, bit for bit; exit 0 (Match)
$ replay --record windy.fpvlog --seconds 60 --wind windy ; replay ...    -> match: 60000 steps and 130 events, bit for bit
$ replay --record calm-rain.fpvlog --seconds 60 --wind calm --rain rain ; replay ...  -> match: 60000 steps and 132 events, bit for bit
  every log: format 13, model 18, 1812.2 B a step (the 1808 B record and its 4 B block header)
known-wrong (one bit flipped in step 30000's record of severe-canopy):
  diverged at step 29998 (t = 29.998 s), mode_shape8: logged ... replayed ...; exit 1 (Diverged)
```

### The golden chain, by experiment

Every moved golden has a cause I reproduced. No stored input changed: the goldens' sticks, seeds and levels are main's. The diff adds only optional parameters with defaults that keep them.

| audit | how | result |
|---|---|---|
| main's model 15, from main's own code | `wt-main` at `de8d7399`, the goldens' air set by a temporary edit | still: powered `0xABF47E6E55E48EB8`, contact `0x557DEBE410FAEEA1`; steady 5 m/s from 270°: `0xC64EBF46E14DCB08`, `0x77E7E0AB78B82E81`; 4 m/s from 0° stepping in at 3 s: `0xFEA7140235016C01`, `0xF5C392DF1CB6E9B8`; tumble `0xFB841727FADDC7A7`; default powered `0xCCECAFB7466D6ADE` (main's pin, so the edit changes nothing else). These are the values GoldenFlights.cs cites |
| the merge against main's model 15 | `wt-head`, main's tuned 0.25, hashed over main's 1656 bytes (p7c-wind's 152 at 1296 skipped) | still and steady: main's four hashes bit for bit. The two gust steps (from 0° and from 180°), with the wind slot zeroed on both sides: powered `0x984CC5485FE1B5B1` and `0x0434EFE1A65C2ED4`, contact `0x18EFB5B9FA7D6648` and `0xC9FEB00108119CF3`, equal on main and on the merge |
| the merge against p7c-wind's model 10 | `wt-head` with `RigidBody.MidpointPasses` 0 and `SevereCentre` 7.2 (temporary edits), every airframe mechanism off, 5 ms ESC lag, model 11's packs on model 13's file, 0.044, p7c's lens (0, −0.005, 0.115), over the first 1448 bytes | powered `0xFF9521B7653C909C`, contact `0xBC4E9FA68E254625`, rain `0x6E28860F47C45BC1`: model 10's pins at `6e4ad056`, bit for bit. **Control:** with the midpoint passes left at 3 they read `0x9014C2E470F85999`, `0xF36E8E99C5A18124` and `0x5F96947559B4C716`, not model 10's |
| model 18 against model 17 | `wt-head` with `SevereCentre` 7.2 (a temporary edit) | powered `0x14FA2EF5AE127F25`, contact `0x4D5A4F57872D01F7`, rain `0x5FA1F49E82CBC0D5`, tumble `0x2A0283BBFE59FD84`: model 17's pins at `864ebbeb`, bit for bit |
| the tumble's chain | `--golden` (above) and the suite | main's model 15 over its layout; models 14's and 13's on their files |
| the free body | `--golden` | `0x50ED4B05A5D02F20`, unchanged from main |

Every moved case's cause is therefore proven:
- model 18: the centre
- model 17 against main: the field's draws, since the merged code flies main's air bit for bit
- model 17 against model 10: main's midpoint passes and airframe
- the tumble: the camera's air bytes alone

### The statistics tools and their self-checks (TST-03)

```
$ python tools/wind/wind_stats.py --published --gauss 5
  verdict stationary fails T9 (T9a or T9b) in 1 of 1 groups of 5      (T9b 3.03 against 3.3-5.5)
  verdict stationary fails T12             in 0 of 1 groups of 5      (as wind.md §6.5: stationary noise passes T12)
  verdict calm/busy  fails T9 (T9a or T9b) in 1 of 1 groups of 5      (T9a 4.25 against 2.2-3.5)
  verdict calm/busy  fails T12             in 1 of 1 groups of 5      (CV 0.38 / 0.43 against <= 0.32)
  self-check: plain Gaussian noise fails T9/T12 in every group (ok)
$ python tools/wind/wind_stats.py --csv <clip P's attitude log, local> --segment 121-330 --gauss 5
  residual roll std 0.707, pitch 0.311, yaw 0.826 (wind.md's values); stationary T9b 3.11, calm/busy T9a 4.23, T12 CV 0.38 / 0.42
  self-check: plain Gaussian noise fails T9/T12 in every group (ok)
$ python tools/wind/wind_history_fixture.py --check 40
  verdict random             passes 40 of 40 sets (expected 40): ok
  verdict random-unsmoothed  passes 40 of 40 sets (expected 40): ok
  verdict sched3 / sched3-jitter30 / sched5 / sched8 / sched10 (and their -unsmoothed)   passes 0 of 40 sets (expected 0): ok
  verdict one-direction      passes 0 of 40 sets (expected 0): ok
  verdict fixed              passes 0 of 40 sets (expected 0): ok
  verdict fixed-jitter5      passes 0 of 40 sets (expected 0): ok
  self-check: ok: the starting model passes T0c and schedules fail it
```

The port's provenance (D-027) checks out:
- Each file names its `godot-final` source.
- Every constant of the three originals is in the port, with the same value. The only differences are numpy expressions rewritten as list expressions (D1, D2) and track_attitude's constants inlined.
- T0c's seven conditions are character for character the original's.
- The departures are listed in each header: `random.Random` for PCG64, a radix-2 FFT, and `fsum` sums.

### The harness against K§8.5

- **Met:**
  - rate mode through the core's controller, the sticks at 250 Hz
  - the 29.917 Hz camera frames seen 0.10 s late
  - e* = ē − v/(g·τ_v) with τ_v 0.5 s and ē's 3 s trim
  - s = 0.64, with k = −sin θ_ref
  - pitch held at −22.70° (nominal runs)
  - τ 0.30 / 0.17 / 0.27 s through the exact ZYX relations
  - the 2 s height loop
  - the variants d 0 and 0.2, τ_v 0.3 and 1.0, s 1 and 0, flown on the same seeds
  - at least 5 seeds, the first 5 s discarded
  - the course abeam from the left, from the start second of the 90° rule
  - the tracker's columns, read back through the port
- **Departures, each recorded as the orchestrator's call or by belt-geometry-p.md:**
  - the drone the sim flies: 4.832 kg and 0.0445 / 0.0474 / 0.0599 kg·m², against K§8.5's 4.8 kg and 0.044 / 0.054 / 0.050
  - the cruise's 20.6 m/s release
  - the belt stretch flown through a canopy strip at 0.5–1.0 H. K§8.5 itself records that P's geometry contradicts the old stretch.
- **Not met:** Calm's asymmetry (S1), the d = 0.2 τ call (S4), and the variants and the mass runs not reported (S4).

### The levels against the targets (1.5, open)

```
$ flightbench --wind-harness <level> --seeds 10 --pilots all --out DIR [--wrong nodrag | --wrong steady | --belt off]
$ python tools/wind/grade_levels.py DIR <level>
```

Seeds 1–10 reproduce D-058 and tasks.md 1.5 to the printed digits:
- **Severe: 9 of 21.** T0 7.170°; T5 belt 1.731°; T6a 0.965; T6d 0.020 / 0.021°; T7 18.7/min, 85 % in the belt; T9a 2.880; T11a 1.197 / 0.441°, ratio 2.72; T11b 0.977 and 1.415, tilts 0.938 and 1.185; T12 CV 0.243 / 0.129.
- **Windy: 10 of 21.** T0 3.371°; T5 field 0.326°.
- **Calm: 9 of 9** (T0 0.979°, T5 field 0.090° against ≤ 0.11).

The misses are D-058's:
- **The spreads:** T1 0.254°, T2 0.072°, T3 0.681°, T4 0.141°, T6b 0.021°, T6c RMS 0.055°.
- **The yaw:** T5 field 0.720°, over its band.
- **The rest:** T8 median 0.85 s, T9b 2.87, T10a −0.22, T10b belt 0.51, and T0b windward 7 of 10.

**Each passing target fails its known-wrong model**, at Severe and at Windy, on every set I flew. T6a's pose noise is the noted exception: it reads 0.876–0.919 against ≥ 0.85 at Severe, but fails at Windy (0.645–0.729). At Severe, pose noise of 0.15° is too small next to the field's roll to move its share below 1 Hz. A white-noise gust field (1.4's spectrum known-wrong), which moves the airframe's response itself to high frequency, would be the sharper test. Some margins are thin:
- T6d's pose-noise roll is 0.129 against ≤ 0.13; it fails on pitch, 0.129 against ≤ 0.06.
- T11b's no-drag model fails on one condition only (S3).
- At Calm, Severe's runs pass T2 and T4, as tasks.md says.

**T0c on my own histories** (`--history 1600` with the seed base 5000, so seeds 5001–6600, graded by `wind_history_fixture.py --grade`):

```
T0c on 40 sets of 40 seeds x 120 min from .../qa/t0c/severe/history
simulator: passes all of T0c in 40 of 40 sets; fails per condition: draws 0, offset 0, spread 0, rate 0, gap_cv 0, peak 0, side 0
    draws           min 0.034  median 0.133  max 0.395     (<= 0.7)
    offset          min -3.046 median -0.069 max 2.288     (within ±15°)
    spread          min 47.706 median 49.517 max 51.178    (20-60°)
    min seed spread min 30.941 median 37.376 max 41.967    (>= 10° in every seed)
    rate            min 0.151  median 0.159  max 0.172     (0.1-0.4 per min)
    gap_cv          min 0.766  median 0.847  max 0.940     (>= 0.5)
    peak            min 1.388  median 1.524  max 1.715     (<= 2.4)
    side            min 0.856  median 0.869  max 0.882     (0.75-0.98)
```

The tightest margin is the peak, 1.715 against 2.4. In `--check 40` the lowest schedule peak is 3.175 (every 10 min, unsmoothed), and the starting model's highest is 2.078, so the peak separates the two. 0 failures in 40 sets puts T0c's false-failure rate under about 7 % at 95 %.

## Spec scenarios

| scenario | result |
|---|---|
| The tools reject noise and schedules | **Pass.** Stationary noise fails T9b and calm/busy noise fails T9a and T12, on P's published numbers and on P's own log. Each of the 9 schedules and 3 fixed or one-direction controls passes 0 of 40 sets, and the starting model passes 40 of 40 (`--check 40`; pytest's 40-set check, 63 of 63) |
| Severe meets every target | **Fail, as D-058 records:** 9 of 21 on seeds 1–10, and 8–10 on my sets. Each passing target fails its known-wrong model, except T6a's pose noise |
| The direction history | **Pass.** The field's own histories (seeds 5001–6600, 40 × 40 × 120 min) pass all of T0c in 40 of 40 sets: resultant at most 0.395, offset within 3.05°, spread 47.7–51.2° with every seed ≥ 30.9°, 0.151–0.172 shifts/min, gap CV ≥ 0.766, peak ≤ 1.715, side kept 85.6–88.2 % |
| The trade-off cannot be dodged | **Partly.** T11b passes on every set: both ratios ≥ 0.8 and the tilts within ±25 %. The scenario's correlation clause fails: roll and yaw rate over the belt read 0.44–0.51 at Severe across the four sets (+0.55 to +0.90). "More than over the field" holds (belt minus field +0.25). See S3 for T11b's weak known-wrong |
| Same seed, same wind | **Pass.** 20 000 of 20 000 steps bit-identical at all 4 rotors and the body in-process. Across processes, every replay above matches bit for bit with the wind at the body and the hubs in the record. A different seed gives a different bearing (χ² 5.5 over 400) |
| Clip P's pitch flies the cruise | **Pass.** 20.58 m/s from 13 and from 25 m/s. T0 at that airspeed passes at Severe on every set (6.67–9.19°, against 6–12), though thinly on seeds 201–210 (S2) |
| Rain without wind | **Pass.** The signals are live and logged, the wind stays Calm's, and the drag is within 1.66E-15 of its labelled formula |
| The cost of the wind | **Not testable now:** the bench was not run (the machine was handed to the pilot). 1.8's figures on AC are posadka-step p99 107.0 µs, canopy-step p99 47.8 µs, 0 B, 0 stalls. Owed |

## Requirements

| ID | result |
|---|---|
| PHY-13 | **Partly met.** Met: the log law, the 2–4 Hz turbulence, the wakes with recirculation, the speed-up and the gap jets (`worldbench --golden` wakes 11 of 11), and the canopy strip. Missed: the trench-lip cavity and the terrain shelter, which are neither built nor decided (S5) |
| PHY-13a | **Met.** Vorticity in every mode, the field sampled at each hub. The gust front gives coupled pitch 4.82 °/s and yaw 2.04 °/s, against the uniform field's 0.01 and 0.05 °/s |
| PHY-14 | **Partly met** (1.5 open by D-058). The tools are ported and self-checked. Severe passes 8–10 of 21, Windy 9–10 and Calm 9 of 9, over four 10-seed sets. Calm is flown with the asymmetry on (S1), and the false-failure rates are unmeasured (S2) |
| PHY-15 | **Met.** The direction and strength draws are logged. The meander and the shifts are on named counter-based streams, computable from the seed. It is never a fixed vector (T0c) |
| PHY-16 | **Partly met.** T11b passes; T10b over the belt fails (0.44–0.51 against 0.55–0.90) |
| PHY-17 | **Met for 7c's part:** the rate signal, the air at the lens and the independence from the wind. The drag and the film act on the body. The fiber's and the soil's parts are 7d's and 7a's |
| PHY-17a | **Met.** 20.58 m/s, inside 17–22. The H-force is back to the derived 0.044 (`drone.json`, label "derived"). The known-wrong 0.25 flies 13 m/s (`AeroTests`) |
| PLT-04 | **Not verified by QA.** The bench is owed. 1.8's numbers are within the 300 µs p99 and 0 B |
| TST-02 | **Met.** The re-records are audited case by case, and every cause is reproduced (above) |
| TST-03 | **Partly met.** Every passing target fails a known-wrong model except T6a (noted), and T11b's is weak (S3). Test lengths are not sized by a measured false-failure rate (S2, S6, S8) |

## The merge with the airframe

- **Log format 13, record 1808 B.** It is model 5's 1296 B with the wind at the origin in the stand-in's gust slot (offset 912), then p7c-wind's 152 B (the four hubs' winds, the rain force, the film, the air at the camera), then p7b's 360 B. `GoldenFlightTests`' layout test checks the offsets, and every replay log reads 1812.2 B a step.
- **`StandInWind` is removed.** No code refers to it, and only one comment (S7) still names it.
- **The rotor drag:** `drone.json`'s `h_force_n_per_m_s` is 0.044, labelled "derived" (D-058). Its blade-element note checks against an order-of-magnitude estimate: the profile term σ·c_d0·μ/4·ρA(ΩR)² gives 0.008 N per m/s, the note's figure.
- **One camera point,** `camera.lens_m` (0, 0.015, 0.115), for the modes and the air at the lens.
- **The steady onset** is carried in `WindField` (`_still` before the onset step). Main's gust step flies bit for bit (above).

## Hygiene

- The 52 branch commits are subject-only and at most 72 characters, with no body, trailer or co-author. They are under the Physics Engineer, World Builder and Orchestrator identities. The merges carry no "# Conflicts" body.
- Nothing from `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/` is in the diff.
- The OPSEC scan finds 0 in 1933 files. The ported tools name letters only. `rain_stats.py` resolves clips from the letter map under `reference/` and never prints a name.
- No new third-party files: `fetch.py --verify` OK.
- The port is from this project's own `godot-final` tag (D-027), not merged.

## Runs still owed (when the machine is free)

1. **Unity EditMode**, with Unity Hub running and at least 2.5 GB of free RAM:
   `& $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile - | Out-Host`
   It must pass with the sim package's new files (`Runtime/Wind/{WindField,WindSetup,CanopyFlow,Rain}.cs`, `Runtime/World/WakeGrid*.cs`) compiled under `noEngineReferences`.
2. **`flightbench --bench`** on AC power, vsync off, with the power plan and mode and the clocks printed. Every row must pass, including posadka-step (Severe wind in Severe rain over the wake grid, a 10° turn: p99 ≤ 300 µs, 0 B, ≥ 2 rebuilds, 0 stalls) and canopy-step. Also read posadka-step's max: 1.8's run printed 1594.6 µs, one step past its 1 ms slot. Say whether such a step is a counted hitch.
3. **The tests that launch Blender** (the orchestrator asked to skip them while the pilot plays): `python -m pytest tools/blender/tests/test_build_blender.py`, and `python -m pytest tools` with the real `BLENDER`. My run of the rest set `BLENDER` to a missing path and ignored that file.
4. **Optional, with S2:** the four seed sets' grades again after any fix.

Today's host, for the record: AC power (BatteryStatus 2), the Balanced plan with the balanced mode (overlay 00000000), an AMD Ryzen 7 7735HS at 3201 MHz. Other agents' and the pilot's work was on it, so no timing here counts.
