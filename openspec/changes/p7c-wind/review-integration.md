# QA review: the physics integration, its wind and world half

**Verdict: changes requested, one small blocker (B1).** Superseded by "Re-check at 18a594e4" at the end: **approve**. The wind and world half is ready to land once B1 is fixed. The rest are should-fix items for the open task 1.5, the physics engineer and the world builder, not blockers:
- **The map's wind reproduces.** The wakes behind H01_D, the belt's canopy and its porous lee pass on Mono and .NET, and each known-wrong fails. The course along the belt reads the belt with the map's obstacles and nearly the field without them.
- **Severe's grade reproduces exactly:** 10 of 21 on seeds 1–10, the same ten targets as recorded. My own seeds 501–510 give 9 of 21 (T11a drops out).
- **D-060's five items are in and checked.** Each fails its known-wrong.
- **The wind over the holes reads the soil,** as model 20 records. Inside E01's cut the field still blows the open profile from the floor, the known open bug (S5), and I measured it.
- **QueryVersion 4's give-up rule** is the code QA approved at `0ccf424`, unchanged on the line, and physics' sign-off matches QA's numbers.
- **The hygiene of all 106 commits is clean.**
- **Unity EditMode on Mono passes:** 1016 of 1022, with 6 explicit skips. Every golden and the map's wind pin equal .NET's.
- **Both benches pass on AC.** See "The benches".

**B1 blocks the merge.** E01's two widened tolerances are no longer justified: at model 26 neither reading moves at all.

**Scope:**
- **Under review:** `origin/unity/physics/physics-integration` at `2a27993b` (`LogFormat.ModelVersion` 26, `LogFormat.Version` 16), 106 commits over `unity/main` at `5574a141`.
- **`git merge-tree`** against `unity/main` merges without conflict. The merged tree differs from the tip only in `unity/main`'s later docs (`docs/handover-2026-11-03.md`, `docs/decisions.md`, `docs/pilot-questions.md`, `docs/requirements.md`, and `p6-world-query-gallery`'s `review.md` and `tasks.md`), so every check below holds for the merge.
- **My half:**
  - `p7c-wind`'s integration: the map's wakes, the belt's canopy, the wind over soil patches, D-060 and the wind harness
  - E01's widened tolerances
  - QueryVersion 4's give-up rule
  - the hygiene of the 106 commits
  - the owed Unity EditMode run and both benches
- **The other half** (the landing, interference, micro-motion, power, the flight goldens and the full `dotnet test`) is the second QA agent's. I re-ran only the goldens' pins, to compare Mono with .NET.

**How I checked:**
- Every command below was run by me. No author's output was used.
- **Where:**
  - this worktree (`.claude/worktrees/qa-pi-wind`, at `2a27993b` after `git lfs checkout`)
  - Unity EditMode in the clean worktree `.claude/worktrees/phys-int2` (at `2a27993b`, `git lfs checkout` first), read-only, with its results written here
- **Logs:** this worktree's `unity/Logs/qa/` and `unity/Logs/editmode*.{xml,txt}` (git-ignored).
- **Scratch tests**, never committed and deleted after their runs, are kept as text in `unity/Logs/qa/QaScratchE01.cs.txt`. They used the suite's public helpers only:
  - E01's landings against the plane under five steady bearings
  - the field's wind inside the run
  - the run's walls by ray
  - lost contacts across 24 bearings and speeds
- **My seeds:**
  - the harness on 501–510 (the author's are 1–10)
  - T0c on histories 601–640
  - the map's belt course on 1–10 and 501–510

## Findings

### Blocking

**B1. E01's two widened tolerances guard nothing at model 26, and their comments describe numbers the code no longer gives.** Owner: the physics engineer. A test-only fix.
- **What changed** (`277f277c`, at model 24):
  - `WithTheAirframesEffectsSoilIsStillThePlane("run")`, cut: the rest's tolerance went from 10 to 15 ms. Traced then: 0.414 against the stub's 0.425 s.
  - `InASteadyCrosswindSoilIsStillThePlane("run")`, held: the rise's tolerance went from 1 to 2 mm. Traced then: 56.23 against the plane's 55.20 mm.
  - `ContactsCase`'s "throttle cut, as the stub's" row went from 10 to 15 ms.
- **At the tip, on both runtimes, the readings are equal:**

  | Reading | Model 24 (the comment) | Model 26, .NET | Model 26, Mono (EditMode) |
  |---|---|---|---|
  | E01's run, still air, cut: at rest | 0.414 s, stub 0.425 | 0.419 s, stub 0.419 | 0.419 s, stub 0.419 |
  | E01's run, steady 5 m/s from 270°, held: rose | 56.23 mm, plane 55.20 | 111.04 mm, plane 111.04 | 111.04 mm, plane 111.04 |
  | `flightbench --contacts`, E01's run cut, as the stub's | | 0.419 s, stub 0.419 | |

- **So the widening is not justified at the tip.** Models 25 and 26 (1.6c's stick spring, D-066's build) changed these landings, and the model-24 difference is gone. I did not trace which one removed it, and the verdict does not need it. A tolerance that no measured case needs only lets the next drift through.
- **Does it hide the cavity model's gap? No, and it cannot.** `WindSetup.SteadyFrom` is uniform air: `WindField.At` returns the steady vector at every height and never reads the ground (`WindField.cs` 524–528). The run and the plane fly the same air, and a cavity model in the level field's `Air` would not change this test. The check compares the soil's contact law against the plane's in identical air. It says nothing about the trench's wind. That gap is S5.
- **Fix:**
  - restore 0.01 s and 1e-3 m in `RangeContactTests` and 0.01 s in `ContactsCase`
  - replace the model-24 traces with the model-26 readings (or drop them)
  - re-run `dotnet test --filter RangeContactTests` and `flightbench --contacts`

### Should fix (not blocking the landing)

**S5 (still open, now measured): inside E01's cut the field blows the open profile from the floor.** Owner: the orchestrator decides, then the world builder (the lip height) and the physics engineer (the cavity).
- **Model 20's hole rule** reads the run's floor (y −1.5) as the ground under the body. So below the lip the profile starts again at the floor.
- **Measured** in the map's own field (`WindSetup.ForMap`), mean horizontal speed over 120 s, inside the run and over the plane at the same height over the local ground:

  | Severe, seed | 0.5 m | 1.0 m | 1.5 m (the lip) |
  |---|---|---|---|
  | 1 | 1.39 / 1.41 m/s | 2.79 / 2.98 | 3.45 / 3.74 |
  | 2 | 1.34 / 1.12 | 2.55 / 2.17 | 3.15 / 2.69 |
  | 3 | 0.56 / 0.94 | 1.16 / 2.42 | 1.44 / 3.07 |

  Calm reads 0.09–0.14 / 0.18–0.24 m/s at 0.5 m.
- **The physics:** a 0.64 m wide, 1.5 m deep cut (H/W 2.3) is a skimming-flow cavity. Its air turns over slowly under the lip, at a small fraction of the lip's speed (recalled: Oke 1988's street-canyon regimes, not re-read). The sim blows 0.47–1.20 of the plane's reading at the same height there; the spread comes from the two spots' different wakes and eddies.
- **The rule also jumps at the run's edge.** The ground is sampled once, under the body. A drone 0.5 m over the plane whose body crosses the edge suddenly reads its rotors 2.0 m over the floor: Calm's mean goes from about 0.2 to 0.6 m/s in one step.
- **The decision is still missing.** PHY-13 says the cavity is "decided in 7c's design". The proposal's "What Changes" still promises "the cavity under a trench lip". The design never decides it, and `docs/decisions.md` has no entry. The prior review's S5 asked for a decision entry, with the proposal to match. Neither exists.
- **Before the player's map flights:** a decision entry (built in 7c, or deferred to `p7a-ground` or WLD-16), with the proposal and design to match. Until then, the handover and the app's notes should name the trench's wind as a known stand-in.

**S9 (new): in E01's run a wind across it loses contact pairs, which the log names a bug.** Owner: the physics engineer.
- **Found** while answering B1's question. I swept steady winds over the run: 8 bearings × 1, 2 and 5 m/s, held and cut, legs on, the airframe on (`unity/Logs/qa/e01-lost.txt`).
- **Results:**
  - at 5 m/s with 3.5 m/s or more across the run, the landing logs `ContactsLost`:

    | from (wind across the run) | lost |
    |---|---|
    | 197° (5.0 m/s) | 4229 |
    | 17° (5.0 m/s) | 1476 |
    | 242° (3.5 m/s) | 1296 |
    | 332° (3.5 m/s) | 338 |
    | 62° (3.5 m/s) | 307 |
    | 152° (3.5 m/s) | 16 |

  - The same count in the held and the cut flight: the loss comes during the hover and descent in the cut, before the touch, as the wind holds the drone against a wall.
  - 0 lost at 1 and 2 m/s, along the run (107°, 287°), and in the map's own Calm, Windy and Severe fields at the scenario's seed `0x450F`.
  - 0 tunnels and 0 crashes in every case.
  - `FlightLogReader` names this event a bug: "its table or the query buffer was full".
- **Where it comes from (likely, not traced):** within 2.5 m of the foot, the run is about 40 overlapping boxes of `E01_curving#29` (shapes 330–370): floor fillers, walls in two or three layers, lips and slopes. Pressed against them, a contact shape exceeds `PairsPerShape` 8 or the query buffer of 32 (`ContactModel.cs` 54 and 71, unchanged since `fe3936ca`).
- **Why now:** before model 20 the field blew nothing over a hole. Since model 20 the trench is as windy as the open field at the same height over its floor (S5). Severe's stronger draws give about 3.4–4.2 m/s at 1 m over the floor, so a pilot's flight in the trench can reach this.
- **Fix:** trace which limit overflows, then size it, or merge coplanar wall boxes as the seam rule does. Keep a scenario: E01's run in a 5 m/s cross-run steady wind, `Lost == 0` and no tunnel. Not blocking: it needs a wind across a 0.64 m trench, and nothing tunnels.

**S8 (still open, now failing a fresh set): the harness's airspeed band has no false-failure rate, and it fails 3 of my 10 runs.** Owner: the physics engineer (1.5).
- **The check:** each nominal run's mean airspeed within −5 to +8 % of the release speed (20.4 m/s at model 26). It is part of `flightbench --wind-harness`'s exit code.
- **Results:**
  - Seeds 1–10: every run passes, seed 10 at −5.0 %.
  - Seeds 501–510: seeds 502, 508 and 510 fail at −5.0, −5.0 and −5.1 %, so `--wind-harness severe --first-seed 501` exits 1.
  - Without the strip, seed 10 (−5.5 %) and seed 502 (−5.4 %) fail.
- **Over my 20 nominal runs** the offsets sit at −2 to −5 %, about 3 % under the release. That is the closed loop at clip P's pitch in turbulent air.
- **So the pass depends on the seed set.** About 15 % of runs fail, so a default 5-seed run fails on roughly half of the seed sets.
- **Fix:** grade the set's mean against a band sized from the measured spread, or size the per-run band from its false-failure rate (TST-03), as the prior review asked.

**S2 / S3 (still open): three of the ten passes are not evidence, and T11a's pass depends on the seed set.** Owner: the physics engineer (1.5).
- **Known-wrongs that pass their target on seeds 1–10:**
  - T6a's pose noise (0.905 against ≥ 0.85)
  - T6b's `nowake` (0.062 in 0.06–0.16)
  - T11b's `nodrag` (every condition in band)
  - T10a's `steady` (T10a fails anyway)
- **So T6a, T6b and T11b pass** without every known-wrong failing them. T6b's `steady` does fail it.
- **On my set 501,** T11b's `nodrag` fails it (the s0 tilt 1.293), while T6a's pose and T6b's `nowake` still pass.
- On both sets the grader prints "NOT every known-wrong model fails its target".
- **T11a** passes on 1–10 (belt 1.145, ratio 2.03) and fails on 501–510 (belt 0.979 against 1.1–2.3, ratio 1.74 against ≥ 1.8).
- These are the prior review's S2 and S3, unchanged by the integration.

**S10 (new): along the map's belt, the course without the map still reads the belt's stretch 1.5× the field in yaw.** Owner: the physics engineer (1.5, the spec scenario "Along the belt").
- **The scenario** says that without the map's obstacles "the belt stretch reads as the field". It does for roll and tilt (T3/T1 0.81 and 0.95, T11a ratio 0.96 and 1.05 on my two sets). It does not for yaw:

  | Seeds | belt yaw, with the map | field yaw, with the map | belt yaw, without | field yaw, without |
  |---|---|---|---|---|
  | 1–10 | 0.996° | 0.419° | 0.595° | 0.379° |
  | 501–510 | 0.964° | 0.453° | 0.578° | 0.390° |

  Without the map that is ×1.57 and ×1.48.
- **The cause is the pilot, not the air, shown by experiment** (see "The split per stretch, by experiment"). The map course is flown at one height, so the only difference between its stretches is the harness pilot's split per stretch (D-060: s 0.85 over the field, 0.73 over the belt). It uses more yaw over the belt whether or not a belt is there.
  - With one split, the course without the map reads the belt as the field (0.351 against 0.360°).
  - The map's belt then gives 0.58°, which would fail T5 belt's band. So about 40 % of the belt's yaw on the map course is the split's.
- **The scenario's comparison still passes:** the belt's yaw and its share of roll events are higher with the map than without it, on both sets and with either split.
- **Fix (1.5):**
  - restate the scenario's clause for the pilot the harness flies, or fly its known-wrong with the split held constant
  - grade each target read over the belt with the split held constant as well, so the air's share is shown apart from the pilot's

### Low

**L6 (still open): `IWorldQuery`'s give-up rule states two numbers QA's QV4 re-check disproved.** Owner: the world builder. Comments only.
- Lines 45 and 48 still say a bisected touch "was never later" and the remainder is "at most about 6 µm deep".
- QA's re-check (`p6-world-query-gallery/review.md`, L6 on `unity/main`) found 6 touches at Time 0.9999999999999999 and a 42.7 µm remainder.
- Physics' sign-off accepts both and leaves the comments to the world builder.

**L7 (new): the world query's sign-off record is split across two changes.** Owner: the orchestrator, at the merge or the archive.
- On `unity/main`, `p6-world-query-gallery/review.md` says physics' sign-off of the give-up rule "is still open … belongs to the range branch's merge".
- The sign-off itself is in `p6-flight-core/tasks.md` (1.6b over the holes), on this line.
- `review-physics.md` (1.2) predates QueryVersion 4.
- A one-line pointer in the world-query change keeps its archive whole.

**L8 (new): `p7c-wind/tasks.md`'s last grade is model 24's.** Owner: the physics engineer. Its 1.9 "Model 24" line names Severe 10 of 21. Model 26's grade, the same ten (above), is recorded nowhere in `p7c-wind`. Add it, or a pointer to it.

### Observations (no action)

- **Physics' sign-off says "nothing in the law reads Time beyond the latch".** Nearly true. The prop ring keeps its earliest segment by `c.Time < p.Time` (`ContactModel.cs` 768), so an L6.1 late touch could pick a neighbouring segment's point within one step: sub-millimetre, inside one rim.
- **The wire and crown tunnel tests pass on both runtimes at model 26.** The handover expected the wire's `InTheWindNoTunnelThroughARigidShape("wire", 5.51, Severe)` to fail. On .NET (my filtered run) and on Mono (EditMode) all eleven cases pass with 0 tunnels. The full `dotnet test` is the other QA's.
- **`MapWorldWind.cs` and `MapWorldLoad.cs` sit in the world builder's folder** but were written under the Physics Engineer identity (`f0265fc7`), as task 1.9 planned. They add no query and move no hash: `worldbench --golden` keeps the test range's content hash `6114242e06475fb5` and every query golden. A world-builder read before the archive is worth having.

## Spec scenarios (`wind-turbulence`)

| Scenario | Result |
|---|---|
| The tools reject noise and schedules | **Pass.** `python -m pytest tools`: 1537 passed, with Blender set. It includes `test_plain_gaussian_noise_fails_t9_and_t12`, `test_shifts_on_a_fixed_schedule_fail_t0c` and `test_the_other_controls_fail_t0c`. `wind_stats.py`, `wind_history_fixture.py`, `rain_stats.py` and `clip_p.py` are unchanged on the line; `grade_levels.py` gains D-060's families and T0b's share. The grader's Gaussian families fail T9a, T9b and T12 on both of my sets |
| Severe meets every target | **Fail, as D-058 allows (1.5 open):** 10 of 21 on seeds 1–10 (the recorded ten) and 9 of 21 on 501–510. Not every passing target fails its known-wrong (S2/S3) |
| The direction history | **Pass** on my histories 601–640 (40 × 120 min): resultant 0.18, offset +1.0°, spread 49.4° with every seed ≥ 38.8°, 0.160 shifts of 45° or more a minute, gap CV 0.83, periodicity peak 1.54 |
| The trade-off cannot be dodged | **Partly.** T11b passes on both sets (s1 0.990 / 0.996, s0 1.421 / 1.408, tilts 0.96–1.21). Its `nodrag` known-wrong passes on 1–10 (S3). The correlation clause fails: roll with yaw rate over the belt 0.530 / 0.482 against 0.55–0.90, though above the field's on both |
| Same seed, same wind | **Pass.** `TheSameSeedTheSameWind`, `TheSameSeedGivesTheSameWindWithoutAllocating` (the map's field, 0 B) and `TheMapsWindIsTheSameBitsOnEveryRuntime` (`0x0EF8A67AC398C411` on .NET and Mono) pass. `replay` in the map's Severe wind matches 120 000 steps and 203 events bit for bit |
| Behind a house and inside the belt | **Pass on both runtimes:** behind H01_D the far wake is Perera's (bias −0.010), the cavity reattaches at 1.07 X_R, and (T − 1) is 0.913 of the boost. Inside the belt σ/u* is 2.03 / 1.59 / 1.08 and the profile exponential. The lee is porous (lowest u/U 0.282, T 1.90). Every known-wrong fails |
| Along the belt | **Pass on its comparison, with S10.** With the map: belt yaw 0.996° / 0.964° against 0.595° / 0.578° without, and 62 % / 70 % of roll events over the belt against 32 % / 49 %. Without the map, roll and tilt read as the field but yaw 1.5×: the pilot's split per stretch, shown by experiment. With one split the course without the map reads as the field (0.351° against 0.360°), and the map still raises the belt's yaw ×1.66 |
| Clip P's pitch flies the cruise | **Pass.** 20.36 m/s from 13 and 25 m/s at model 26 (17–22). The known-wrong 0.25 N per m/s flies 12.90 m/s. T0 at the cruise passes on both sets (7.20°, 7.63°) |
| Rain without wind | **Pass** (`RainWithoutWindKeepsCalmsWindAndItsDragFormula`, both runtimes) |
| The cost of the wind | **Pass** on AC (Balanced, best performance mode, 3201 MHz). posadka-step p99 70.2 µs and map-step p99 67.5 µs, both 0 B. The bearing moves 10.7°, the worker rebuilds the grid 9 times, with 0 stalls |

## Requirements

| ID | Result |
|---|---|
| PHY-13 | **Partly met.** The log law with class 4's z0, the turbulence to the discs' 0.5 m, the map's wakes, the belt's canopy and the soil under holes are met. The trench cavity and the terrain shelter are missing and undecided (S5) |
| PHY-13a | **Met.** The field is sampled at each hub, with vorticity; `CanopyFieldTests` and `WakeFieldTests` pass on both runtimes |
| PHY-14 | **Partly met** (1.5 open): Severe 10 / 9 of 21 on two sets. Windy and Calm pass the harness's own check on 5 seeds |
| PHY-15 | **Met.** Every draw is named and bounded; T0c passes on my 40 histories |
| PHY-16 | **Partly met.** T11b passes; T10b over the belt fails (0.53 / 0.48) |
| PHY-17 | **Met for 7c's part** (rain's tests on both runtimes) |
| PHY-17a | **Met.** 20.36 m/s |
| PLT-04 | **Met on AC:** every `flightbench --bench` row p99 ≤ 89.9 µs against 300 µs, 0 B; `worldbench --bench` every budget met |
| TST-02 | **Met for my half:** the map's wind pin and the range golden equal on Mono and .NET; no golden moved in my half |
| TST-03 | **Partly met.** Three passing targets do not fail their known-wrong (S2/S3). The harness's airspeed band fails on a fresh set (S8). False-failure rates are still unmeasured |

## QueryVersion 4's give-up rule

- **The code is the one QA approved.** `git diff 0ccf424..2a27993b` touches only `MapWorldLoad.cs` (+9, the wind hooks) and the new `MapWorldWind.cs` in `Runtime/World/`, and none of `ShapeMath`, `WorldHash` or `IWorldQuery`. `WorldHash.QueryVersion` is 4. `worldbench --golden` passes the floor and the test range at query version 4 with QA's hashes.
- **Physics' sign-off** (`p6-flight-core/tasks.md`, "Physics' sign-off of query version 4's give-up rule") matches QA's re-check (`p6-world-query-gallery/review.md` on `unity/main`, "B4: closed"):
  - a step that ends inside is always a first touch (QA: 0 missed of 36 760)
  - a bisected late touch lies on a tangent plane, so it latches with the same depth and force
  - the remainder (at most 42.7 µm) is a graze
  - L6.1's late touches are grazes
- **It keeps review-physics' P1:** "every Time < 1 contact latches, and every impact is one Time < 1 record" (K§7.5). QueryVersion 3 broke that premise (B4); QueryVersion 4 restores it. P2's 0.05 m margin is unchanged.
- **Open items:** L6 (the comments) and L7 (the record's place). The prop ring's read of Time is an observation above.

## The map's wind, the belt and the soil

- **The map's obstacles** (`MapWind.Of`): every placed volume off the `posadka` group as placed, and one porous box per belt section. Each section is read as a strip by its footprint along its principal axis, λ from the crowns' frontal area and α = β^0.4. Every number is labelled derived, recalled or assumed.
  - The determinism rule holds. The sums run in row order. The one sort (the sections' sweep) cannot change a section, since equal starts fall in the same run.
  - `DetMath.Atan2`, `Sqrt`, `Exp` and `Log` are used throughout.
- **The rule at a point** (`WindField.Air`): the canopy's plan weight w takes the mean's share and the wake grid 1 − w, and the eddies √w and √(1 − w). This is design 9's rule. `OutsideTheStripsTheMapsFieldIsItsWakesBitForBit` passes on both runtimes.
- **The soil under holes** (`FlightWorld.SoilUnder`): S04's straw mat reads its top and E01's run its floor (`OverAHoleTheWindReadsTheSoil`: 30 090 points over a hole, at most 4 casts; Calm at 1 m over the straw 0.6160 m/s, the floor's 0.6160). The trench's inside is S5.
- **D-060**, each with its known-wrong failing, on both runtimes:

  | Item | Reading | Its known-wrong |
  |---|---|---|
  | The open field's z0 | class 4's 0.10 m and σ_u/U 0.164 (`TheOpenFieldsRoughnessIsTheLandscapesClass`) | the stubble's 0.03 m reads 0.136 |
  | The turbulence to the discs' 0.5 m | 20–30 Hz at 0.92 of von Kármán's variance | the 1 m cut: 0.05 |
  | The recorder's field pick | σ 5.77 ms | the uniform clock: 0.29 ms |
  | The stick noise | 0.0531 of the command's variance (the model's 0.0497) | −20 dB: 0.168 |
  | The split per stretch | the field's roll share 0.803 on seeds 1–5, and 0.799 / 0.800 (field) with 0.657 / 0.679 (belt) on my two graded sets, against P's 0.80 and 0.64 | the single 0.64: 0.538 |
  | T6d's ceiling | P's real motion with its field pick | the pose noise fails |

## E01's widened tolerances, by experiment

My scratch test flew `InASteadyCrosswindSoilIsStillThePlane`'s two landings, the run and the plane, in other steady air, at model 26 on .NET. Each cell is the run's reading against the plane's.

| Air (from, speed) | Held: rose | Cut: at rest | Lost (run) |
|---|---|---|---|
| 270°, 5 m/s (the test's) | 111.04 / 111.04 mm | 0.305 / 0.305 s | 0 |
| 287°, 5 m/s (along the run) | 108.71 / 108.71 mm | 0.272 / 0.301 s | 0 |
| 197°, 5 m/s (across) | 77.34 / 187.77 mm | 0.282 / 0.840 s | 4229 |
| 17°, 5 m/s (across) | 61.55 / 186.11 mm | never / 0.846 s | 1476 |
| still air | 242.89 / 242.90 mm | 0.419 / 0.419 s | 0 |

- **In the test's air the run lands as the plane to the printed digit.** In air across the run, the walls take the drone and the comparison has no meaning. The loss is S9.
- **The run's width,** by horizontal rays every 15° from the foot (`e01-walls.txt`): the nearest walls lie at 15° and 195°, across the run's 105° axis. They are 0.321 + 0.322 m away at 0.05 m up, 0.339 + 0.337 at 0.20 and 0.358 + 0.352 at 0.35. So the run is 0.64, 0.68 and 0.71 m wide there, as `RangeScenarios.Run`'s comment says (0.64 at the skids, 0.69 at the props).
- **The 270° held flight** ends 0.98 m along and about 0.16–0.19 m across from the foot, bit for bit as on the plane. The walls would not allow that at the floor, so the held throttle must have carried it above the lip by then (inferred, not traced).

## The split per stretch, by experiment

A temporary edit, reverted: `WindHarness.BeltSplit` 0.73 → 0.85, the field's split on both stretches. With it I flew the map course with and without the map on seeds 1–10 and 501–510, graded the same way (`unity/Logs/qa/split-exp*.txt`, `split-grade-*.txt`). `git status` was clean after the revert, and flightbench was rebuilt before the benches.

| Seeds, air | Belt yaw, split per stretch | Belt yaw, one split | Field yaw, one split |
|---|---|---|---|
| 1–10, the map | 0.996° | 0.583° | 0.390° |
| 1–10, `nomap` | 0.595° | **0.351°** | 0.360° |
| 501–510, the map | 0.964° | 0.580° | 0.413° |
| 501–510, `nomap` | 0.578° | **0.354°** | 0.374° |

- **S10's cause is the split.** With one split, the course without the map reads the belt stretch as the field in yaw (0.351 against 0.360, 0.354 against 0.374), as the scenario says.
- **The map's belt still raises the yaw by itself:**
  - with one split it is ×1.66 over the course without the map (0.583 against 0.351)
  - roll events over the belt are 70 % and 83 % with the map, against 39 % and 56 % without it
  - so the scenario's comparison holds
- **About 40 % of the belt's yaw on the map course is the pilot's split, not the air** (0.996 → 0.583°). With one split, T5 belt would fail its band (0.58 against 0.8–2.5). The roll measures move the other way: T3/T1 rises from 1.18 to 1.37 and T7's share from 62 % to 70 %.
- **For 1.5:**
  - the split is set by position (the pilot "knows" the belt), not by the air
  - a target read over the belt (T5 belt, T3, T7's share, T11a) should also be graded with the split held constant, so the air's share is measured apart from the pilot's; or the split should follow the disturbance the pilot meets
  - the synthetic strip's grade uses the same split, so the same question applies to its T5 belt pass

## The harness and the grade

`flightbench --wind-harness calm|windy|severe` (5 seeds, the default) all PASS at model 26. Severe's release is 20.4 m/s, and the 4.742 kg build flies the harness.

| Seeds | Severe: targets passed | Fails | Known-wrongs that pass |
|---|---|---|---|
| 1–10 (the author's) | **10 of 21:** T0 7.20°, T1 0.464°, T5 field 0.404°, T5 belt 1.205°, T6a, T6b 0.062°, T9a 2.81, T11a (belt 1.145°, ratio 2.03), T11b, T12 | T0b (6 of 10 windward), T2 0.179°, T3, T4, T6c (share 0.586), T6d pitch 0.071°, T7 (39/min, 61 % over the belt), T8, T9b 2.96, T10a, T10b | T6a's pose, T6b's `nowake`, T10a's `steady`, T11b's `nodrag` |
| 501–510 (mine) | **9 of 21:** as above without T11a (belt 0.979°, ratio 1.74) | as above, plus T11a | T6a's pose, T6b's `nowake`, T10a's `steady` |

- **The recorded grade** (`p7b-airframe` 1.10's integration notes, model 24) names the same ten at Severe on seeds 1–10. Model 26 changes no verdict there.
  - Like for like, the readings moved a little: T2 0.185 → 0.179°, T6c 0.135 / 0.603 → 0.125 / 0.586, T6b 0.063 → 0.062°, T6d 0.080 / 0.070 → 0.080 / 0.071°.
- **The harness's own check** passes on seeds 1–10 and fails on 501–510 (S8).
- **The map's belt course** (`--map test-range`, nominal, 10 seeds each), graded with the `nomap` runs in the strip's known-wrong slot:

  | Seeds | T5 belt / field | T3/T1 | T4/T2 | T11a ratio | T7 share over the belt |
  |---|---|---|---|---|---|
  | 1–10 | 0.996 / 0.419° | 1.18 | 1.15 | 1.43 | 62 % |
  | 1–10, `nomap` | 0.595 / 0.379° | 0.81 | 1.03 | 0.96 | 32 % |
  | 501–510 | 0.964 / 0.453° | 1.26 | 1.15 | 1.46 | 70 % |
  | 501–510, `nomap` | 0.578 / 0.390° | 0.95 | 1.04 | 1.05 | 49 % |

  The record at model 22 read 0.98 / 0.38°, 1.22, 1.34, 1.49 and 64 % on seeds 1–10. The belt's ratios have eased a little since the airframe merges.

## The benches

Run by `unity/Logs/qa/bench.ps1` (output `bench-1.txt`). It prints the power state, the plan, the clocks, the load and the other heavy processes before, between and after the two benches.

- **The host:**
  - AC (`PowerOnline True`, not discharging), the Balanced plan; both benches print "AC, Balanced plan, best performance mode, CPU 3201/3201 MHz"
  - an AMD Ryzen 7 7735HS at 3201 of 3201 MHz, 6.5–6.7 GB free
  - vsync is not in play: both benches are headless
- **What else ran:**
  - Before the benches another agent's Unity Windows build (`C:\.dev\450fpv-latest`) had been running; it had ended by 11:48:47. Load then was 3 %, with no Unity, Blender or dotnet process but Unity Hub's.
  - `worldbench --bench` ran quiet (11:48:47–11:48:57).
  - During `flightbench --bench` (11:48:57–11:52:02), another agent's `dotnet test` (testhost, from 11:51:50) overlapped its last rows, the airframe cases.
  - Every row passes with a wide margin, so nothing needed a quiet re-run.

| Bench row | p50 | p99 | max | Other | Verdict |
|---|---|---|---|---|---|
| `worldbench`: physics' pattern along and over E01's run | 11.4 µs | 54.5 µs | 1677 µs | 0 B | target 60 µs met (the six paths' slowest) |
| `worldbench`: K§10.5's batch over E01's run | 36.3 µs | 86.5 µs | | | 60 µs missed; shown only, judged at 7d (D-054) |
| `worldbench`: the rest | | | | | every budget met; "bench done, every budget met (judged on AC only)" |
| `flightbench` powered-flight | 18.9 µs | 89.9 µs | | 0 B | PASS |
| `flightbench` logged-step | 24.5 µs | 59.9 µs | | 0 waits | PASS |
| `flightbench` canopy-step, inside the strip | 30.5 µs | 48.3 µs | | outside: p50 19.5, p99 30.6 µs; 0 B | PASS |
| `flightbench` posadka-step | 26.5 µs | 70.2 µs | 622.7 µs | 9 rebuilds (61 ms on the worker), 0 stalls, 0 B | PASS |
| `flightbench` map-step (the test range in its own wind, 2 m inside the belt) | 28.1 µs | 67.5 µs | 1209.9 µs | 9 rebuilds (43 ms), 0 stalls, 0 B | PASS |
| `flightbench`: the slowest other row, resting in S04's straw | 47.9 µs | 83.1 µs | | 0 B | PASS |

`flightbench --bench`: all 16 rows PASS, 0 FAIL, against p99 ≤ 300 µs and 0 B. The largest p99 is powered-flight's 89.9 µs.

- **The wind's cost** meets the spec's scenario: a 10° move of the bearing rebuilds the wake grid 9 times on the worker with no stall on the flight thread, at p99 67.5–70.2 µs.
- **E01's run is the world's costliest path,** consistent with S9's many overlapping boxes: physics' pattern p99 54.5 µs against 60 µs, and K§10.5's batch over its 60 µs. It is a margin to watch when `p7a` adds the ground's grooves.

## Hygiene

- **The 106 commits** (102 Physics Engineer, 4 Reference Analyst; author and committer the same role identity):
  - subject only, `type(scope): summary`, at most 72 characters
  - no body, no trailer, and no co-author, Claude or Anthropic line
  - the 12 merges each carry a `-m` subject with no "# Conflicts" body
- **The roles fit their files:**
  - the Reference Analyst's 4 commits touch only `tools/reference/` and `docs/reference-notes/`
  - the physics commits touch the sim package, `tools/{flightbench,replay,wind,worldbench}`, `drone.json` and the change records
  - one physics commit reaches the world builder's folder (`f0265fc7`, an observation above)
- **Nothing from the forbidden folders:** no path from `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/` is added in any of the 106 commits. `tools/reference/` is the analyst's tool code.
- **OPSEC:**
  - `precommit.py --tree HEAD`: 2284 files, 0 findings
  - my scan of the 10 177 added lines finds no date, coordinate or media file name
  - the reference tools' tests plant a synthetic secret name, and say no test opens the real `reference/`
  - clips are cited by letter only
- **Licences:** no new third-party file. `fetch.py --verify`: OK.
- **`openspec validate --all --strict`:** 24 passed, 0 failed.

## Commands and output

Every log is in this worktree's `unity/Logs/qa/` (the EditMode results in `unity/Logs/`). Trimmed to the lines that carry the verdicts.

```
# worldbench (runs1.sh)
$ dotnet run -c Release --project tools/worldbench -- --golden
worldbench --golden (Release, .NET 10.0.9, Microsoft Windows 10.0.26300, X64)
  PASS floor/query_version 4 … floor: all 5 cases pass
  PASS test-range/query_version 4
  PASS test-range/content_hash 6114242e06475fb5 … test-range: all 6 cases pass
  PASS wakes/wake.version 1 … wakes: all 11 cases pass
worldbench: ALL PASS

# the wind harness, the default 5 seeds
$ dotnet run -c Release --no-build --project tools/flightbench -- --wind-harness calm     -> 35 flights in 6.7 s; wind harness: PASS
$ dotnet run -c Release --no-build --project tools/flightbench -- --wind-harness windy    -> 35 flights in 6.8 s; wind harness: PASS
$ dotnet run -c Release --no-build --project tools/flightbench -- --wind-harness severe   -> 35 flights in 7.3 s; wind harness: PASS
  1.5's cruise lean at 20.4 m/s in a steady 9.5 m/s crosswind, as the tracker's roll: 1.425° per m/s
  known-wrong: no drag, seed 1: -0.082° per m/s, airspeed 132.31 m/s: fails, as it must

# Severe, every family, 10 seeds, on 1–10 and on 501–510 (S = 1, 501)
$ dotnet run -c Release --no-build --project tools/flightbench -- --wind-harness severe --seeds 10 --first-seed S --pilots all [--wrong nodrag|steady|noiseless|nowake | --belt off] --out unity/Logs/qa/grade-S
  S=1:   nominal PASS (seed 10 airspeed −5.0 %); --belt off FAIL (seed 10 airspeed −5.5 %)
  S=501: nominal FAIL (seeds 502, 508, 510 airspeed −5.0, −5.0, −5.1 %); --belt off FAIL (seed 502 −5.4 %)
$ python tools/wind/grade_levels.py unity/Logs/qa/grade-1 severe
severe: 10 of 21 targets pass; NOT every known-wrong model fails its target
  the nominal pilot's roll share of the thrust tilt (K section 8.4, P: 0.80 field, 0.64 belt): field 0.799, belt 0.657
$ python tools/wind/grade_levels.py unity/Logs/qa/grade-501 severe
severe: 9 of 21 targets pass; NOT every known-wrong model fails its target
  the nominal pilot's roll share of the thrust tilt (K section 8.4, P: 0.80 field, 0.64 belt): field 0.800, belt 0.679

# the map's belt course, with and without the map
$ dotnet run -c Release --no-build --project tools/flightbench -- --wind-harness severe --seeds 10 --first-seed S --pilots nominal --map test-range [--wrong nomap] --out unity/Logs/qa/map-S
flightbench: the wind harness along test-range's belt (2 sections, h 10.50 m), Severe, the map's own wind (WindSetup.ForMap), at 1.35–1.60 h, 10 seeds × 1 pilots
$ python tools/wind/grade_levels.py unity/Logs/qa/mapgrade-S severe        # severe = the map runs, severe-nobelt = the nomap runs
  S=1:   T5 belt 0.996 (nobelt 0.595), T3 ratio 1.180 (0.806), T4 ratio 1.153 (1.034), T11a ratio 1.425 (0.960), T7 belt share 0.623 (0.322)
  S=501: T5 belt 0.964 (nobelt 0.578), T3 ratio 1.263 (0.946), T4 ratio 1.153 (1.043), T11a ratio 1.455 (1.052), T7 belt share 0.698 (0.488)

# T0c on my histories
$ dotnet run -c Release --no-build --project tools/flightbench -- --wind-harness severe --seeds 1 --first-seed 601 --pilots nominal --history 40 --out unity/Logs/qa/t0c
$ python tools/wind/wind_stats.py --wind-log unity/Logs/qa/t0c/severe/history/*.csv
  pooled over 40 seeds: draws' resultant 0.18; offset +1.0 deg; mean spread 49.4 deg; 768 shifts in 4800.0 min = 0.160/min, gap CV 0.83 (728 gaps), periodicity peak 1.54 at 73 s; side kept 87.3%
  (each seed's spread 38.8° or more)

# replay in the map's own wind
$ dotnet run -c Release --project tools/replay -- --record unity/Logs/qa/map-severe.f450log --map test-range --wind severe
record map-severe.f450log: virtual clock, …, test-range, Severe wind, Sunny, 120000 steps (120.000 s), 206 events, 3 scheduler hitches, 0 log waits
  landing: touched at 107.632 s at 0.441 m/s down; … 5 impacts, armed False, crashed False
$ dotnet run -c Release --no-build --project tools/replay -- unity/Logs/qa/map-severe.f450log
replay map-severe.f450log: seed 0x000000000000450F, model 26, world 'test-range' 0x6114242E06475FB5, drone.json 0x8808B6996C019E64; recorded by tools/replay 1.0.0+2a27993b…
  match: 120000 steps and 203 events, bit for bit

# E01, the contact rows
$ dotnet run -c Release --no-build --project tools/flightbench -- --contacts
  E01's run, throttle cut, as the stub's: … rose 2.0 mm; … at rest 0.419 s after it; 28 contact events, 0 tunnels …
contacts: PASS

# .NET, the tests my half reads (simtests, Debug)
$ dotnet test tools/simtests --filter "…InASteadyCrosswindSoilIsStillThePlane|…WithTheAirframesEffectsSoilIsStillThePlane|…OverAHoleTheWindReadsTheSoil|QaScratchE01"
  InASteadyCrosswindSoilIsStillThePlane("run"): held: … rose 111.04 mm (plane 111.04) …; cut: … at rest 0.305 s (plane 0.305)
  WithTheAirframesEffectsSoilIsStillThePlane("run"): cut: … at rest 0.419 s (stub 0.419)
$ dotnet test tools/simtests --filter "…InTheWindNoTunnelThroughARigidShape|…ATunnelIsLoggedAsABug|…GoldenFlightTests|…MapWindTests|…TheRainGoldenHolds|…WakeGridTests"
  Total tests: 40, Passed: 40
  powered golden: 0x53F1D10489702031 … contact golden: 0xAAEA31DBFBB13710 … range golden: 0xC79B60F487AEB8B6 … tumble golden: 0xBCBD33E6B6A94844
  the map's wind, Severe seed 21, 4000 steps at 7 points: 0x0EF8A67AC398C411

# Unity EditMode, in .claude/worktrees/phys-int2 at 2a27993b (git lfs checkout first; Unity Hub running; AC)
PS> & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults <qa-pi-wind>/unity/Logs/editmode.xml -logFile - | Out-Host
  Unity 6000.3.26f1, Mono (MonoBleedingEdge unityjit-win32); 11:09–11:20, exit 0, "Test run completed. Exiting with code 0 (Ok)"
  total 1022, passed 1016, failed 0, skipped 6 (the [Explicit] probes: GroundMicroMotionProbe.{SourcesTable,Table}, MicroMotionProbe.{Levels,SourcesTable}, RotorInterferenceTests.{CruiseReadings,TableReadings})
  powered 0x53F1D10489702031, contact 0xAAEA31DBFBB13710, range 0xC79B60F487AEB8B6, tumble 0xBCBD33E6B6A94844, the map's wind 0x0EF8A67AC398C411: each .NET's

# hygiene, licences, specs, Python (runs2.sh)
$ python tools/hygiene/precommit.py --tree HEAD    -> 2284 files scanned, 0 findings
$ python tools/assets/fetch.py --verify            -> OK
$ openspec validate --all --strict                 -> Totals: 24 passed, 0 failed (24 items)
$ python -m pytest tools -q                        -> 1537 passed in 1204.68s (0:20:04)

# commits
$ git log --format='%an <%ae> / %cn <%ce>' origin/unity/main..HEAD | sort | uniq -c
    102 Physics Engineer <physics@450fpv.local> / Physics Engineer <physics@450fpv.local>
      4 Reference Analyst <reference@450fpv.local> / Reference Analyst <reference@450fpv.local>
  bodies: none in 106; trailers, co-author, Claude or Anthropic lines: none; subjects over 72: none; off the type(scope): form: none
$ git merge-tree --write-tree origin/unity/main HEAD  -> exit 0, no conflict

# the benches (bench.ps1 -> bench-1.txt), AC, Balanced, 3201/3201 MHz
$ dotnet run -c Release --project tools/worldbench -- --bench
worldbench --bench: AC, Balanced plan, best performance mode, CPU 3201/3201 MHz
  physics 12 + 17 + 4 discs + 8, along and over E01's run at 10 m/s, its skids from the floor to 1 m up (object 29): median 11.40 us, p99 54.50 us, max 1677.2 us, 21.5 swept queries a step, 0 B; the 60 us target met at p99
worldbench: bench done, every budget met (judged on AC only)
$ dotnet run -c Release --project tools/flightbench -- --bench
flightbench: 10 bench cases registered; AC, Balanced plan, best performance mode, CPU 3201/3201 MHz
posadka-step: … 58000 steps p50 26.50 µs, p99 70.20 µs, max 622.7 µs; the wake grid rebuilt 9 times (last 61 ms on the worker), 0 stalls; allocation 0 B: PASS (p99 ≤ 300 µs, 0 B, ≥ 2 rebuilds, no stall)
map-step: … 58000 steps p50 28.10 µs, p99 67.50 µs, max 1209.9 µs; the wake grid rebuilt 9 times (last 43 ms on the worker), 0 stalls; allocation 0 B: PASS (p99 ≤ 300 µs, 0 B, ≥ 2 rebuilds, no stall)
  16 rows, 0 FAIL; the largest p99s 67.5, 70.2, 83.1, 89.9 µs
```

## Re-check at 18a594e4

**Verdict: approve.** B1 is fixed as it asked, and the wind and world half is ready to land.
- The S and L findings stay with their owners, and none blocks.
- No file of theirs moved in the fixes.
- S5 now has its decision on `unity/main`: D-071 builds the trench's cavity in `p7c-wind`.

**Scope:**
- `origin/unity/physics/physics-integration` at `18a594e4`, still model 26: 18 commits over `2a27993b`, which the flight review's re-check lists.
- My half's part of them: `40b902d3`, the restore.
- **How I checked:**
  - everything run by me, in `.claude/worktrees/qa-pi-recheck` (logs in its `unity/Logs/qa-recheck/`, git-ignored)
  - Unity EditMode in `.claude/worktrees/phys-int2` at `18a594e4`, read-only, after `git lfs checkout`

**B1, fixed (`40b902d3`).**
- **The restore is exactly `277f277c` undone:**
  - 0.01 s in `WithTheAirframesEffectsSoilIsStillThePlane`
  - 1e-3 m in `InASteadyCrosswindSoilIsStillThePlane`
  - 0.01 s in `--contacts`' "E01's run, throttle cut, as the stub's" row
- **The model-24 traces are gone.** The soil spot's 15 ms in the crosswind is older than `277f277c`, and is untouched.
- **At the tip, on both runtimes:**

  | Reading | .NET | Mono (EditMode) |
  |---|---|---|
  | E01's run, still air, cut: at rest | 0.419 s, stub 0.419 | 0.419 s, stub 0.419 |
  | E01's run, steady 5 m/s from 270°, held: rose | 111.04 mm, plane 111.04 | 111.04 mm, plane 111.04 |
  | `flightbench --contacts`, E01's run cut, as the stub's | 0.419 s, stub 0.419: PASS | |

- **The restored tolerances guard something.** I switched `SameSupport`'s coplanar widening off, a temporary edit (the flight review's N7):
  - The still-air cut then rests at 0.408 s against 0.419 s. That fails the restored 10 ms; the old 15 ms would have passed it.
  - The crosswind's held rise reads 107.79 mm against 111.04 mm, and fails either way.

**Unity EditMode on Mono:**
- **The result:** 1025 tests, 1017 passed, 0 failed, 8 skipped. The 8 are the six `[Explicit]` probes and N4's two `[Ignore]` tests. The new case `OnFlushBoardsEachSupportIsOnePoint` passes.
- **The pins equal .NET's:** every golden pin, the map's wind pin `0x0EF8A67AC398C411`, and E01's readings above.
- **How it ran:** on AC, with Unity Hub running.
  - My first launch redirected Unity's output with `*>`, so PowerShell returned at once and Unity ran on, detached. Its console log went nowhere.
  - Its results file is complete: 648 s in the editor's Mono domain ("Unity Child Domain", clr 4.0.30319.42000). I waited on the process before reading it.

**Not re-run:**
- **The benches:** the fixes change no runtime line (two comment lines in `ContactModel.cs`).
- **The wind harness and the grade (S2/S3, S8, S10):** no wind file moved.

**Hygiene:** see the flight review's re-check.
- The 18 commits are subject-only under their role identities, with no trailer.
- No forbidden path; the tree scan finds 0 findings in 2286 files; `openspec validate --all --strict` passes 24 of 24.
- The line merges into `unity/main` (81f3804e) without conflict.

```
$ dotnet test tools/simtests          Total tests: 398, Passed: 396, Skipped: 2; exit 0
  WithTheAirframesEffectsSoilIsStillThePlane("run"): cut: ... at rest 0.419 s (stub 0.419), 28 contact events (stub 26)
  InASteadyCrosswindSoilIsStillThePlane("run"): held: touch 0.4725 m/s (plane 0.4725), rose 111.04 mm (plane 111.04); cut: at rest 0.305 s (plane 0.305)
$ dotnet run -c Release --project tools/flightbench -- --contacts
  E01's run, throttle cut, as the stub's: ... at rest 0.419 s after it; 28 contact events, 0 tunnels, 0 crashes, 0 lost ...: PASS
contacts: PASS
$ QA_NO_WIDEN=1 (scratch worktree) dotnet test --filter <E01's cases> ; ... --contacts
  WithTheAirframesEffectsSoilIsStillThePlane("run"): Expected 0.419 +/- 0.01, But was 0.408
  InASteadyCrosswindSoilIsStillThePlane("run"): Expected 0.11104 +/- 0.001, But was 0.10779
  contacts: FAIL (E01's run, throttle cut, as the stub's)
PS> & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults <qa-pi-recheck>/unity/Logs/qa-recheck/editmode.xml -logFile -
  <test-run total="1025" passed="1017" failed="0" skipped="8" duration="647.96">
  WithTheAirframesEffectsSoilIsStillThePlane("run"): cut: ... at rest 0.419 s (stub 0.419)
  InASteadyCrosswindSoilIsStillThePlane("run"): held: ... rose 111.04 mm (plane 111.04)
  the map's wind, Severe seed 21, 4000 steps at 7 points: 0x0EF8A67AC398C411
```
