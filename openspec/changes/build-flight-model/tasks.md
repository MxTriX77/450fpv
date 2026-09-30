# Tasks

Order matters. Sections 1–3 build a drone that flies in still air on flat ground; 4–6 add the world's disturbances; 7 tunes to the measured targets; 8 reviews. The log and the replay check (section 2) come early on purpose: a replay that has to be retrofitted onto a finished model never passes, because every unlogged draw has to be hunted down afterwards.

## 1. The body and the step

- [ ] 1.1 [physics-engineer] Add the fixed-step rigid-body core in `game/src/physics/`: frames, quaternion state, semi-implicit Euler at 1 kHz, its own thread, the double-buffered pose, and `DetMath` (the correctly rounded approximations that replace `Math.Sin`, `Cos`, `Exp` and `Pow`). Verify the never-varies, no-Godot, frame-rate, quaternion, Euler-definition, no-transcendentals and Debug-equals-Release scenarios
- [ ] 1.2 [physics-engineer] Add the component mass and inertia build from `wind.md` §5.5, driven by the payload and legs settings and by coil mass. Verify the seven-loadout, pitch-exceeds-roll, coil-movement and payload scenarios
- [ ] 1.3 [physics-engineer] Add `tools/flightbench` as a headless harness: run a seed with a scripted stick sequence, no Godot, print states. Verify that a flight runs to completion headless and that two runs of one seed agree

## 2. The log and the replay

- [ ] 2.1 [physics-engineer] Add the log header and the packed full-state record in `game/src/telemetry/`, with the ring buffer and worker flush. Verify the header-complete, every-step, no-stall and size-budget scenarios
- [ ] 2.2 [physics-engineer] Add the event log with the full event set and their cause fields. Verify the contacts-carry-their-surface scenario now, and the crash-names-its-cause scenario at 8.1 once every mechanism exists
- [ ] 2.3 [physics-engineer] Add `--replay` with bit-exact comparison and divergence reporting, plus the world-hash and ISA refusals. Verify the replays-exactly, needs-no-Godot, divergence-pinpointed and world-mismatch scenarios on the section 1 model, then re-verify after every later section
- [ ] 2.4 [physics-engineer] Add the readable summary printer. Verify the summary-of-a-crash and truncated-log scenarios

## 3. Propulsion

- [ ] 3.1 [physics-engineer] Add the rate controller and the X mixer with honest saturation and collective desaturation. Verify the sticks-released, rate-command, yaw-runs-out-first and not-silently-exceeded scenarios
- [ ] 3.2 [physics-engineer] Add the ESC filter, the motor time constant, rotor speed as a state, and the thrust and torque curves with axial-inflow derate. Verify the thrust-lags, rotor-speed-is-a-state, maximum-thrust, hover-throttle and forward-flight scenarios
- [ ] 3.3 [physics-engineer] Add the pack model: OCV against state of charge, cell resistance, sag under current, and charge integration. Verify the thrust-falls and hard-pull scenarios
- [ ] 3.4 [physics-engineer] Add the seed architecture and the per-flight draw catalogue from `flight-randomness`: one flight seed, named streams by hashing, per-flight draws in the header, and the single asymmetry switch. Verify the catalogue-complete, no-noise-on-output, stays-in-bound, new-stream, streams-independent, seed-visible and header-reconstructs scenarios
- [ ] 3.5 [physics-engineer] Add the per-motor asynchrony as parameters: Kv offset, ESC timing offset, prop imbalance. Verify the hands-off-drift, asymmetry-off-is-steadier, scales-with-throttle, parameter-not-noise and both asymmetry-bound scenarios. **This is manifesto §2.4 and the change's hardest scenario to get honest** — the drift band is set here and revisited at 7.3
- [ ] 3.6 [physics-engineer] Add airframe aerodynamics: body, coil and rotor-disc drag with their points of application, and weathervaning. Verify the crosswind-lean, side-gust-rolls and gusts-are-forces scenarios
- [ ] 3.7 [physics-engineer] Add per-rotor ground and ceiling effect from the rotor raycasts, vortex ring state with hysteresis, and inter-rotor prop wash. Verify the extra-lift, uneven-ground-rolls, over-an-object, vertical-descent-sinks, no-chatter, forward-flight-clears and close-hover scenarios

## 4. Ground contact

- [ ] 4.1 [physics-engineer] Implement the `surfaces-review.md` §1 contact solve: elasto-plastic Winkler soil with foot-size scaling and the firm layer, the mat in series, stick-point friction with the μ blend, ploughing, and the footprint ring. Verify the static-sink table, plastic-set and no-new-fields scenarios
- [ ] 4.2 [physics-engineer] Add cover forces from cached `MicroDetailNear` per `surfaces-review.md` §6.4, with the prefetch worker, the deterministic swap step, and hook state keyed by element `Id`. Verify the grass-jitter scenario and the thread-count-does-not-change-the-flight scenario
- [ ] 4.3 [physics-engineer] Add uncertain ground: pitfalls, directional ridges and the query normal. Verify the leg-drops-into-a-hole, furrows-depend-on-heading and loose-soil scenarios
- [ ] 4.4 [physics-engineer] Add compliant legs with their per-leg stiffness spread, and the legs-off configuration. Verify the legs-carry-the-compliance and legs-off scenarios
- [ ] 4.5 [physics-engineer] Add non-leg swept contacts for arms, body, coil and prop discs, following the `api-review.md` §4 plane rule, with `tunnel` detection and prop strikes. Verify the fast-arm-versus-wire, contact-plane and prop-strike scenarios
- [ ] 4.6 [physics-engineer] Add launch rails as runtime objects for the legs-off case. Verify the spool-clears-the-ground and narrow-hard-contact scenarios
- [ ] 4.7 [physics-engineer] Verify the landing bounce (manifesto §2.6): the hold-the-sticks, cut-the-throttle, needs-the-legs and no-restitution-coefficient scenarios. Tune leg stiffness and damping to a plausible bounce, and record the value as an answer to open question O-4
- [ ] 4.8 [physics-engineer] Verify the liftoff leg snag (manifesto §2.1): the hooked-leg, not-a-dice-roll, surface-dependent and effective-weight scenarios. Re-measure the `belt_straw` hook rate after the world's F1 change, as `api-review.md` §7 asks, and compare it with open question O-7

## 5. Wind

- [ ] 5.1 [physics-engineer] Add the mean log-law profile derived from surfaces and cover, sampled per point. Verify the falls-off-toward-the-ground and rougher-cover scenarios
- [ ] 5.2 [physics-engineer] Add the seeded Dryden turbulence field as a spatial field with the §5.4 intensities and scales. Verify the field-is-spatial scenario; the spectral scenarios are graded at 7.1
- [ ] 5.3 [physics-engineer] Add the wake precompute on the `world-query` wind grid per `surfaces-review.md` §6.5, on a worker thread, plus gap jets from `GapsNear`. Verify the solid-building, gap-makes-a-jet and precompute-stays-out-of-the-step scenarios
- [ ] 5.4 [physics-engineer] Add the Calm / Windy / Severe presets with the per-flight direction and strength draws and the in-flight meander and shifts. Verify the two-flights-differ and levels-do-not-overlap scenarios
- [ ] 5.5 [physics-engineer] Add the wind-history logger (no drone, ≥ 40 seeds × 120 min at ≥ 1 Hz) and run T0c. Verify the never-a-fixed-vector scenario in full against every T0c field

## 6. The tether

- [ ] 6.1 [physics-engineer] Add the spool with one-way payout and the coil mass feeding the inertia build. Verify the pays-out, never-comes-back and gets-lighter scenarios
- [ ] 6.2 [physics-engineer] Add the paid-out span as a node chain with gravity, wind drag and world contacts inside the query budget. Verify the hangs-and-lies, wind-moves-the-span, catches-on-vegetation and query-budget scenarios
- [ ] 6.3 [physics-engineer] Add tension at the exit point below the centre of mass. Verify the loaded-tether-tilts, sharp-reverse and smooth-orbit scenarios
- [ ] 6.4 [physics-engineer] Add the three break mechanisms against a per-flight drawn strength, and the link signals at ≥ 50 Hz. Verify the either-way, graded-not-binary, gentle-flying, not-the-end-of-the-physics, every-link-signal, signals-respond and wedged-span scenarios

## 7. Tuning to the measured targets

- [ ] 7.1 [physics-engineer] Build the `wind.md` §6.1 simulated pilot and the §6.2 test flight in `tools/flightbench`, emitting the `attitude.csv` columns at 29.917 Hz. Verify that `wind_stats.py` reads the output and that the Euler columns match the §6.2 definitions
- [ ] 7.2 [physics-engineer] Tune the Severe preset and the turbulence and wake parameters until every target T0–T12 of `wind.md` §6.3 is inside its band. Verify with at least 5 seeds plus the §6.1 variant runs, and report every target's value against its band
- [ ] 7.3 [physics-engineer] Tune Windy and Calm to the `wind.md` §6.4 columns, Calm with the asymmetry switch off. Then set Calm's floor with the asymmetry on, as `wind.md` §6.4 assigns to this change, and verify the Calm-has-a-floor and floor-is-not-a-noise-term scenarios. Record the floor as an answer to open question O-12
- [ ] 7.4 [physics-engineer] Meet the performance budget: the step benchmark with the full world loaded, zero steady-state allocation, prefetch and precompute off the step, clock and power mode printed. Verify the budget-met and sim-rate-holds scenarios, and re-run `--replay` afterwards because optimisations can change results

## 8. Review

- [ ] 8.1 [physics-engineer] Run the five deliberate crashes of the `physics-log` forensics scenario and confirm each is diagnosable from its log alone. Verify the crash-names-its-cause and bug-versus-pilot-error scenarios
- [ ] 8.2 [physics-engineer] Write `openspec/changes/build-flight-model/parameter-report.md`: every parameter with its final value and its published / pilot / derived / assumed / constant label, and the status of each `design.md` open question — answered, narrowed, or still open for the pilot
- [ ] 8.3 [qa-engineer] Replay the bit-exact check and the golden flights on a **second machine**, closing the gap `api-review.md` §6 flagged and the flight-dynamics same-bits-on-another-machine scenario
- [ ] 8.4 [qa-engineer] review against spec scenarios
