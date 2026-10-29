# QA review: p6-flight-core (task 2.1)

**Verdict: green.** No blocking finding. Every scenario I could run passes on my own seeds, flights and variants; the goldens match in Debug, Release and Mono; the golden chain from model 6 back to model 3 is reproduced by experiment. Four findings are not blocking: one is a budget gap that PLT-04 sets and this change's scenario does not (1), one is a weak known-wrong check (2), and two are hand-offs and docs (3, 4).

**Scope:** tip `b96907b` of `unity/physics/p6-flight-core`: tasks 1.1–1.7 and 1.4b (1.1–1.3 only where they changed after `review-base.md`). Out of scope: 1.6b, the test range half of the contacts (D-046).

**How I checked:**
- A scratch harness (never committed): the core's public API, with my seeds, loadouts (payload 0, 0.3, 1.3, 1.6, 1.7, 2.0 kg, legs on and off, coil 0.2 and 0.9 paid out), stick scripts and worker schedules. Every replay used the repository's `tools/replay`, not my code.
- Temporary edits for the golden audits, each reverted (`git status` clean after).
- No GPU work (D-046): .NET, and Unity `-nographics` EditMode only.

## Findings

1. **Not blocking: PLT-04's world-query budget fails on the launch stand** (owners: world builder for the query cost, physics engineer for the sweeps; the full world waits for the map, D-046).
   - PLT-04 (`docs/requirements.md:159`) sets "world queries alone < 60 µs worst case". The spec scenario (`specs/flight-dynamics/spec.md:56`) asks only for the share, and `flightbench --bench` prints only that (`tools/flightbench/TimedQuery.cs:77`).
   - I measured the queries' own time per step, through the same `TimedQuery` wrapper, over 60 000 steps:

     | case | p50 | p99 | p99.9 |
     |---|---|---|---|
     | in the air | 3.2 µs | 6.9 µs | 18.2 µs |
     | on the floor | 3.7 µs | 8.0 µs | 25.2 µs |
     | on the stand | 43.9 µs | **111.0 µs** | 270.6 µs |

   - The step itself stays inside its 300 µs (stand p99 115.9 µs), and this is the stub world. The stand case is the queries that find nothing beside the posts (1.7's note).
   - **Suggestion:** have `flightbench` print the per-step query p99 and max against 60 µs, and give the stand's sweep count or the stub's empty-query cost a task before G7 (PHB-07 takes off from the stand).
2. **Not blocking: the wind's unbiased-mean target catches its known-wrong model only about 90 % of the time at Windy** (owner: physics engineer).
   - `WindFieldTests.cs:101` grades 10 seeds × 1200 s with `MeanInBand`, whose bound is 3 standard errors (`WindTargets.cs:177`). At Windy, 3 SE is 0.089 m/s against the known-wrong's 5 % bias of about 0.125 m/s.
   - On 20 independent sets of my 10 seeds, the 5 %-strong mean **passed** the target in 2 of 20 at Windy (0 of 20 at Severe). The suite's seeds 1–10 happen to fail it, so the test is green but the target is weak.
   - False failures were 0 of 20 on every target (intensity, time constant, mean), so the targets themselves are sized right.
   - **Suggestion:** 20 seeds at Windy (power about 99.8 %), or a 10 % bias as the known-wrong.
3. **Not blocking: a crash latches but does not stop the core** (hand-off to `p6-input-app`).
   - `ContactModel.cs:655–658` sets `FlightState.Crashed` and logs `Crash`. `FeedSignals.Crashed` is live, and the core keeps stepping the wreck: in my pole runs, the coil and skids touch after the crash.
   - ground-contact's "SHALL end the flight" (`specs/ground-contact/spec.md:18`) is therefore the session's job. **Suggestion:** name it in `p6-input-app`'s tasks: end the flight on `Crashed` and show the summary.
4. **Not blocking: design.md still carries superseded plan values** (owner: orchestrator, before archive).
   - `design.md:53–59` still has: τ_m as an assumed 40–60 ms first-order lag, κ 0.015–0.02, T_max of 2.6 kgf a motor, the linear inflow derate as the model, R_cell 15 mΩ, and an H-force proportional to thrust.
   - The specs and the as-built notes have what was built: a DC motor, κ 0.0144 m, 2.98 kgf a motor at the 40 A limit, APC's C_T(J), 12 mΩ, and H ∝ ω. Only the specs archive, so nothing stale lands in `openspec/specs/`. **Suggestion:** mark them superseded, or update them.

**`review-base.md`'s findings 2–5 are fixed (re-checked):**
- 2: my tumble at 8 more loadouts holds the energy within 1.6e-11.
- 3: the tumble golden fails with the midpoint passes removed (energy 9.05e-4).
- 4: my cut between a step and its events reads as MatchCut, exit 5.
- 5: `TagRuntimeObjects` is in the header, and the stand's flight replays from it.
- Finding 6's `Cbrt` comment and its test now say 2 ulp.

## Spec scenarios

| Capability · scenario | Result | Evidence (mine unless marked) |
|---|---|---|
| flight-dynamics · Render rate does not touch the flight | pass | Real flight thread; sticks pre-stamped 200 ms ahead; a renderer at 30, 60 and 240 fps and one stalled 250 ms. Steps to 10 000: 12 528 records and events, identical in all four; only the hitches differ (23 / 9 / 8 / 14). With a 2 ms lead the first difference is `stick.host_us` (a late report is a different input), never the state. |
| · The worker's timing does not touch the flight | pass | Seed 0x7E57AB1E00C0FFEE, Severe, 1.3 kg payload, a jittered stick stream. On time: 5999 passes. Early: 20 693 passes, 14 693 with nothing due. Late: 303 passes of 2–40 steps. The 7330 records and events are byte-identical; late gives 303 passes and 303 hitches. |
| · Nothing outside the state enters a step | pass | The suite's scan, and its 15 known-wrong lines, pass on .NET and Mono. My greps over `Runtime/` find no `float`, `Math.` outside DetMath, FMA, Numerics, intrinsics or `unsafe`. The filters' delay lines sit outside `FlightState` by D-045. |
| · The bootstrap drone matches K§5.2 | pass | The suite (`TheBootstrapDroneMatchesK52`); my independent build is in review-base. |
| · Free flight conserves what it should | pass | The suite: momentum within 2.2e-11, norm 3.3e-16. |
| · A tumble keeps its energy | pass | My 8 loadouts (payload 0.3 and 1.7 kg, coil 0.2 and 0.9, legs on and off), 4 rad/s on random axes, 60 s: energy within 1.6e-11, momentum within 8.5e-12. The suite's starting-rate step gains +0.06 % to +10.2 %, all past 1e-4. |
| · A crosswind leans the drone | pass | Steady 5 m/s from 17°, 133° and 251°: 1.329, 1.354 and 1.322 °/(m/s), 0.00° off the wind, under 2 mm of error. Severe on three of my seeds: 1.281–1.355. At 13 m/s cruise: pitch 22.77°, and a 5.83° lean in a 3 m/s crosswind (about 1.94 °/(m/s)). Known-wrong: no rotor drag 0.277, no drag 0.000. |
| · The rotors' spin keeps the total momentum | pass | The suite: 0.27 %; reversed 159 %, left out 82 %. |
| · The budget on AC | pass | p99 76.3 µs powered and 115.9 µs on the stand, 0 B, the shares printed (see Commands). PLT-04's absolute query bound: finding 1. |
| flight-randomness · Every draw is named and bounded | pass | Only the wind draws. On my seed at each level, 5 per-flight draws, each with its stream, parameter and bound, and inside it. `wind.turbulence` is counter-based (draw 3·step + axis): the gust it drives is clamped at ±4σ and logged every step. It enters as the air's velocity; no draw touches a force, moment or pose. |
| · Adding a stream shifts nothing | pass | A stream opened first with 777 draws: the wind's per-flight draws are bit-identical, and so is the gust over 100 000 steps. My Python port of the stream key matches the C# bearings. 20 000 random adjacent seed pairs give a mean bearing difference of 89.6° (uniform: 90). |
| ground-contact · Hold the sticks and it leaves the ground | pass (stub) | Descents of 0.45, 0.5 and 0.55 m/s, straight and 0.25 m/s forward. Held: it separates, rising 190–239 mm, and is never at rest. Cut: a 2.6–3.6 mm hop, at rest 0.28–0.35 s after the touch. In my four 120 s flights (a 0.5 s hold, then a cut) the legs unload during the hold, and the drone is down 0.15–0.44 s after the cut. 0 tunnels, crashes or losses. The test range half is 1.6b. |
| · Liftoff from the stand | pass (stub) | Ramps of 2 and 5 s: 8 steel contacts made and 8 broken, off at duty 0.502 and 0.492, sliding 2.0 and 3.1 mm, resting 321 µm deep. No tunnel, no crash. |
| · Into a pole | pass (stub) | At 9, 10 and 11.5 m/s: a crash by `prop_fr` on `L01_straight`, timber (a prop strike at 6.6, 7.5 and 8.8 m/s); peaks 106, 120 and 226 g, impulses 62.8, 70.4 and 82.2 N·s; 0 tunnels. The test range half is 1.6b. |
| physics-log · A truncated log still reads | pass | Six cuts of my Severe flight (`tools/replay` and `--summary`): inside the header (exit 4, "cut inside its header, at byte 23431 of 23468"); mid-record, inside a block header, after a whole block, between step 61009's record and its event, and without the end block (all exit 5, MatchCut, the place and reason named). |
| · Replay matches the flight | pass | Four 120 s flights with contacts and a landing replay bit for bit: 0xD15EA5E5BADF00D5 (Windy, virtual), 0xFFFFFFFFFFFFFFFE (Calm, 0 kg), 0x1 (Severe, 1.6 kg), and 0x0BADC0DE12345678 (Severe, 2.0 kg, real time: 120 019 steps, 108 hitches). A changed drone.json is refused, naming both hashes. So are a flipped world hash and model 5 (exit 3). A one-ulp flip is named by step and field (exit 1). |
| propulsion · ACTUAL rates as Betaflight computes them | pass | My own `applyActualRates`, written from Betaflight's formula with the deadband divider, at ±0, 0.1, 0.25, 0.5, 0.6, 0.75, 0.9 and 1 on every axis: 0.0 °/s off the model's. |
| · The builder's PIDs hold a stable loop | pass | My variants: −200 °/s roll and pitch on a 0.9 pack, +200 °/s on a 0.6 pack. With the chain: overshoot 36.2–40.0 %, within 5 % from 308–402 ms, last-second mean 200.5–200.8 °/s, peak to peak 4.9–6.3 °/s. Without filters: 14.8–16.3 %. Inside D-045's 30–45 % band and its stability terms. |
| · Each filter as Betaflight computes it | pass | The suite on Mono (−3.01 dB and −90° at each lowpass's cutoff). My check of `filter.c`'s notch Q: 50/35 gives 1.3725 and 60/45 gives 1.714, as built. |
| · Sag compensation holds the hover throttle | pass | The suite: a 1.19 % span, 22.2 % without. My hand check of Betaflight's attenuation, (1.2/4.2)·goodness: 25.0 % at 4.05 V a cell and 8.9 % at 3.37 V, as logged. |
| · Yaw saturates first and the collective yields | pass | The suite: 0.809 and 0.978 of full thrust. |
| · The ESC holds the motor at its current limit | pass | The suite: a 40.0 A peak, 115.3 A without the limit. |
| · The motor's lag is its physics | pass | The suite: 51.8, 43.2 and 37.9 ms. My J/(Kt·Ke/R + ∂Q/∂ω) from drone.json: 42.9 ms at 9000 rpm. |
| · Hover and liftoff as the real drone | pass | The suite on Mono, ESC duty: L6 (6.18 kg) 55.0 % on a full pack and 62.5 % on a half; 6.5 kg 56.8 % on a full one. Full thrust 11.93 kgf against an independent steady state of 11.93. |
| · Fast flight loses thrust to inflow | pass | The suite: 9.65 m/s (mine: 25·sin 22.7° = 9.648); the model's derate 0.812 against momentum theory's 0.791; the linear form 0.633. |
| · A full-collective pull on a sagged pack | pass | The suite: 18.61 V at 147.4 A, equal to n_s·(OCV − I·R); thrust ratio 0.918 > (V/V_full)² 0.673. |
| wind-field · Calm is never perfectly still | pass | My 5 seeds × 120 s: slowest 0.626 m/s, the stillest 10 s window moves 36.6 mm/s, means in band, the header says `stand-in`. |
| · The levels hold their intensity and scale | pass | My 10 seeds × 1200 s. Windy: σ/U 0.1353, w/u 0.549, τ 1.00 and 0.99 of L/U, bias +0.0020 m/s (SE 0.030). Severe: 0.1355, 0.549, 1.01 and 0.98, +0.0005 (SE 0.042). White noise, a flat σ and a 5 % bias each fail on my seeds. Power: finding 2. |
| · The same seed, the same wind | pass | 30 000 of 30 000 samples bit-identical; my 400 bearings give χ² 5.0. |

## Requirements

- **Met:** PHB-01, PHB-02, PHB-03, PHB-04, PHB-06, LOG-02, LOG-05 (no user, host, path or date in my logs' headers), DRN-03 (the schema and the mass build), INP-03 (the controller semantics).
- **Partly met:**
  - PLT-04: the step's p99 is met. The query bound fails on the stand (finding 1), and the full world is not loaded yet.
  - PHB-05: stub half; the test range is 1.6b.
  - LOG-01: its event list beyond this change's mechanisms (hooks, VRS, payout, tether) belongs to phase 7. Size: my real-time Severe flight is 78.0 MB a minute raw and 40.4 deflated (zlib 6), under 50.
  - LOG-04: Mono matches here; the second machine is QA's before release.
  - INP-04: the sim side holds: the latest report at or before the step, every one logged. The report interval and latency are `p6-input-app`'s.

## Golden re-records, audited case by case

| Golden | Hash | Audit (by experiment, edits reverted) |
|---|---|---|
| free body | `0x50ED4B05A5D02F20` | unchanged since 1.3 |
| tumble (new, 1.7) | `0xE3178EE7FF322698` | With the passes at 0 it fails its energy check (9.05e-4 against 1e-10): the known-wrong is caught. |
| powered, model 6 | `0x3F13A5CF5DB7DAFD` | Passes at 0 give model 5's `0x734B61C0C15797BC`, and 1.4b's `0x3695FC49DCA906DC` over its 1176 bytes. Then with every filter off and the gyro at the loop (`FilterBank.cs:258`, `x = now`), over 1.6's 1112 bytes: 1.6's `0x798AFECB92E12DB7`; over model 3's 992 bytes: model 3's `0x734CE3306086F5D7`. |
| contact, model 6 | `0x3EE12C8D692BADF3` | Passes at 0 give model 5's `0xF1D0832353311083`. Then with filters off and the gyro at the loop, over 1112 bytes: 1.6's `0x8B568804552379E6`. |

- **No stored stick input changed:** `PoweredStick` is as `ba856be` pinned it.
- **The setup grew only with its mechanisms,** each audited by its owner: aero and Windy in `c391f8d` (model 3), contacts in `a11fb3b`.
- **drone.json's h-force tune** re-pinned the hash within model 3 (`9db02d6`). The cause is a data change, and the drone hash refuses older logs.
- **Not re-run by me:** the model 2 → 3 audit (the air off and the gyroscopic term out).

## Interfaces (design 1)

| Interface | Result |
|---|---|
| `StickSample`, `PoseFrame` | Unchanged since review-base (no diff). `PoseFrame.LegCompression` is filled, one step behind. |
| `FcSettings` | Betaflight CLI names only; fields added at Betaflight 4.5.0's defaults. `TheDefaultsAreTheProfilesNote` reads fc-profiles.md (D-023). |
| `FeedSignals` | Motor Hz and current, pack volts, camera rate, air at the camera, impact and `Crashed` are live; fiber, link and vibration are labelled stubs. That is design 1's list. |
| `IWorldQuery` | Contacts call `SampleGround` and `StaticContacts` through `FlightWorld`, and `Raycast` is wrapped for later; no second stub. |

## Betaflight port

- **Cited lines checked against the 4.5.0 tag:**
  - `flight/pid.h`: 34–35 (PIDSUM 500 / 400), 44–46 (P, I, D 0.032029, 0.244381, 0.000529), 50 (F 0.013754), 53 (relax 40), 57–58 (anti-gravity 0.34, 0.0034)
  - `fc/rc.c`: `applyActualRates` 209–219, `calculateFeedforward` 499–626, `processRcCommand` 628–682, `updateRcCommands` from 684, `initRcProcessing` from 775
  - `common/filter.c`: 45–49 `pt1FilterGain` to 238–246 `biquadFilterApply`
  - `sensors/battery.c`: 132 (the sag LPF period), 208–225 (cell count), 549–554
  - Every cited range I sampled lands on the named code.
- **No copied code:**
  - no GPL header, `#define`, `->`, C float literal or Betaflight identifier outside comments
  - the C# has its own names and structure, with each function's Betaflight source named in a comment
  - the departures are listed in tasks.md: `DetMath` sine and cosine for `sin_approx`, double precision, exact motor speeds

## Commands and output (trimmed)

```
dotnet test tools/simtests                      Passed 214/214 (Debug, 1 m 22 s)
dotnet test -c Release tools/simtests           Passed 214/214 (23 s)
dotnet run [-c Release] --project tools/flightbench -- --golden   (Debug and Release identical)
  free-body 0x50ED4B05A5D02F20 (matches): PASS
  powered-flight 0x3F13A5CF5DB7DAFD (matches); over 1.4b's 1176-byte layout 0xB4F44B5B5663DCDD with and without contacts: PASS
  contact-flight 0x3EE12C8D692BADF3 (matches), 5 impacts: PASS
  tumble 0xE3178EE7FF322698 (matches), momentum 2.42E-013, energy 4.94E-013: PASS
Unity -batchmode -nographics -runTests -testPlatform EditMode   (free RAM 5.44 GB before; exit 0, 87 s)
  testcasecount=320 passed=320 failed=0; Fpv450.Sim.Tests 214 on Mono
  powered golden: 0x3F13A5CF5DB7DAFD; contact golden: 0x3EE12C8D692BADF3; tumble golden: 0xE3178EE7FF322698
flightbench --bench   power: Online; Balanced (381b4222-...)
  flightbench: 6 bench cases registered; AC, Balanced plan, best power efficiency mode, CPU 3201/3201 MHz
  free-body p50 2.10 µs, p99 3.50 µs, 0 B: PASS
  powered-flight p50 15.00 µs, p99 76.30 µs, 0 B, world queries 29.3 % of the step (56.7 calls a step): PASS
  logged-step with its record p50 13.00 µs, p99 57.40 µs, 0 B, 0 ring waits: PASS
  contacts legs on, floor: p50 11.60 µs, p99 26.80 µs, 0 B, world queries 25.9 %: PASS
  contacts legs off, stand: p50 53.90 µs, p99 115.90 µs, 0 B, world queries 79.7 %: PASS
replay qa-1 / qa-3 / qa-4 / qa-2-rt       match: 120000 steps and 352 / 346 / 377 events; 120019 steps and 417 events, bit for bit; exit 0
replay qa-1 --drone <rotor inertia 6.1e-5>   refused: drone.json differs: ... 0x28D50F6C4B7219D5, this replay has 0xD09C6275C0CCE667; exit 3
replay qa-3 (world hash bit flipped)        refused: the world differs: ... 0xF4E2380A254041C5, this replay's is 0xF4E2380A254041C4; exit 3
replay qa-3 (model 5 in the header)         refused: the flight model differs: the log is of model version 5, this build is version 6; exit 3
replay qa-3 (position.y one ulp at 20000)   diverged at step 20000 (t = 20.000 s), position.y: logged ...F57E, replayed ...F57F; exit 1
replay cut-between-step-and-events          match to the cut: 61009 steps and 232 events; note: the cut took the last step's 1 events; exit 5
python tools/hygiene/precommit.py --tree HEAD     1517 files scanned, 0 findings
openspec validate p6-flight-core --strict         Change 'p6-flight-core' is valid
```

## Hygiene

- The package is engine-free: `noEngineReferences: true`, no `UnityEngine` or `UnityEditor` in `Runtime/`, no unsafe code.
- **58 commits** on the branch since `unity/main`:
  - every one is subject-only (no body or trailer), lower case and at most 72 characters
  - authors are Physics Engineer or Orchestrator; there is no Claude or Anthropic line
  - the merges have no `# Conflicts` body; review-base's `162e7dd` is not in the history
- Nothing from `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/` is in the diff, and no logs.
