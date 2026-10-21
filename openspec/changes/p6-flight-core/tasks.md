# Tasks: p6-flight-core

Branch `unity/physics/p6-flight-core` off `unity/main` (D-042). The sim work is engine-free .NET, light on the laptop. Task 1.1 fixes the interfaces the other G7 lanes build against and lands first. Then 1.2 and 1.3 run in parallel on sub-branches (`--log`, `--body`), since they touch separate folders, and merge before 1.4. Then 1.4, 1.5 and 1.6 run in sequence, with 1.6 after `p6-world-query-gallery`'s task 1.1 (the query interface). 1.7 is last.

## 1. Build

- [ ] 1.1 [physics-engineer] The core and the interfaces (design 1, 2, 9):
  - **What:** the step on its own thread with the scheduler, the state, the double-buffered `PoseFrame`, the named seed streams, and `StickSample`, `FcSettings`, `FeedSignals` and the flat-ground `IWorldQuery` stub in `Runtime/{Flight,Control}/`. A `tools/flightbench` case that steps a free body. The frame stated once (D-019, refined if needed with a note).
  - **Verify:**
    - the "Nothing outside the state enters a step" scenario as a suite test
    - the render-rate scenario with a stub renderer
    - the "Adding a stream shifts nothing" scenario
    - `dotnet test tools/simtests`
    - Unity's EditMode run compiles the package
- [ ] 1.2 [physics-engineer] The log and replay (design 10):
  - **What:** the header, the 1 kHz records and events, the ring buffer and flush worker, `tools/replay`, the summary reader.
  - **Verify:** both physics-log scenarios on a free-body flight, and the truncation case.
- [ ] 1.3 [physics-engineer] `drone.json` and the rigid body (design 3):
  - **What:** the schema with a validator, the bootstrap file from K§5.2 with every value labelled, the parallel-axis mass build, the quaternion rigid body.
  - **Verify:** the "bootstrap drone matches K§5.2" and "free flight conserves" scenarios.
- [ ] 1.4 [physics-engineer] The flight controller and propulsion (design 4, 5):
  - **What:** the Betaflight 4.5 model at 4 kHz (from its published formulas, its scaling constants checked against the source), the X mixer, the ESC and motor lags, thrust and torque, the inflow derate, battery sag on two packs.
  - **Verify:**
    - every propulsion scenario (ACTUAL rates within 0.1 °/s, the builder PIDs' step response, yaw saturation past about 0.72 collective, hover margin and liftoff at 50–65 %, the 25 m/s inflow, sag)
    - the defaults read from `fc-profiles.md` per D-023
- [ ] 1.5 [physics-engineer] Aero basics and the stand-in wind (design 6, 8):
  - **What:** body drag with along and across areas, the rotor H-force, coil drag at the coil as `drone.json` places it, the stand-in mean and turbulence per level, labelled.
  - **Verify:** the crosswind-lean scenario with a simple closed-loop pilot in `tools/flightbench`, and both wind-field scenarios.
- [ ] 1.6 [physics-engineer] Contacts (design 7), after `p6-world-query-gallery` 1.1:
  - **What:** legs on `SampleGround`, the four-post stand, swept shapes on `StaticContacts` with the Time < 1 latch, prop strikes, crash and tunnel events.
  - **Verify:**
    - the ground-contact scenarios on the stub, and on the gallery world once `p6-world-query-gallery` lands it: the bounce with throttle held and cut, liftoff from the stand, into L01 at 10 m/s
    - the replay scenario on a 120 s flight with contacts and a landing
- [ ] 1.7 [physics-engineer] The budget and determinism (PLT-04):
  - **What:** `tools/flightbench --bench` on AC with the power mode printed: p50 and p99 step times, steady-state allocations, the world-query share.
  - **Verify:** p99 ≤ 300 µs and 0 allocations; a worker forced early or late changes only the logged hitches; the golden run identical in .NET and in the Unity editor (LOG-04's first half).

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios: every scenario of the six capabilities, run by QA with its own seeds; replay bit-exactness on QA's own flights; the budget on AC; the randomness rule (every draw named, bounded, logged); the interfaces as decision 1 fixes them; hygiene (engine-free package, no copied GPL code, subject-only commits)
