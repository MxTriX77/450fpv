# Tasks: p6-flight-core

Branch `unity/physics/p6-flight-core` off `unity/main` (D-042). The sim work is engine-free .NET, light on the laptop. Task 1.1 fixes the interfaces the other G7 lanes build against and lands first. Then 1.2 and 1.3 run in parallel on sub-branches (`--log`, `--body`), since they touch separate folders, and merge before 1.4. Then 1.4, 1.5 and 1.6 run in sequence, with 1.6 after `p6-world-query-gallery`'s task 1.1 (the query interface). 1.7 is last.

## 1. Build

- [x] 1.1 [physics-engineer] The core and the interfaces (design 1, 2, 9):
  - **What:** the step on its own thread with the scheduler, the state, the double-buffered `PoseFrame`, the named seed streams, and `StickSample`, `FcSettings`, `FeedSignals` and the flat-ground `IWorldQuery` stub in `Runtime/{Flight,Control}/`. A `tools/flightbench` case that steps a free body. The frame stated once (D-019, refined if needed with a note).
  - **Verify:**
    - the "Nothing outside the state enters a step" scenario as a suite test
    - the render-rate scenario with a stub renderer
    - the "Adding a stream shifts nothing" scenario
    - `dotnet test tools/simtests`
    - Unity's EditMode run compiles the package
  - **As built:**
    - **`StickSample`** (`Fpv450.Sim.Control`): `long HostUs` (`HostClock` µs on arrival), `double Roll, Pitch, Yaw` in −1…1 (+1 = right, forward/nose down, right), `double Throttle` 0…1, `bool Arm`; `Neutral` = centred, throttle 0, disarmed. Written to `StickInbox` (lock-free SPSC ring, 1024, `TryWrite`, `Dropped`). Step k's host time is `StartUs + k·1000 µs`; the sample in force is the latest with `HostUs ≤` it; the recorder gets it every step.
    - **`FcSettings`** (struct, CLI names in CLI units, `FcSettings.Default`): the sim default column (rates 5.5 / 22.5 / 34 roll and pitch, 8.5 / 26 / 15 yaw, held as doubles since Betaflight keeps ×10 integers; `thr_mid` 50, `thr_expo` 10, deadband 2 / 2, `rc_smoothing_auto_factor` 50, `f_*` 50 / 50 / 0, sag compensation 0); the builder's PIDs, `d_min` 60, `iterm_relax_cutoff` 8, `throttle_boost` 5; Betaflight 4.5 defaults for the rest (`p_yaw` 45, `d_min_gain` 37, `d_min_advance` 20, `iterm_limit` 400, the RC smoothing and feedforward defaults, `dshot_idle_value` 550; 1.4 re-checks them); `airmode` on (assumed).
    - **`PoseFrame`** (`Fpv450.Sim.Flight`): `Step`, `Position`, `Attitude` (`Quat`), `Velocity`, `BodyRate`, `RotorSpeed`, `Armed`, `LegCompression` (1.6), `FiberPaidOut` (7d). `PoseBuffer` is a wait-free triple buffer of `PosePair` {`Previous`, `Current`, `Signals`, `StartUs`}, published after every step; `PosePair.Interpolate(hostUs, out PoseFrame)` is linear plus nlerp, held at the ends, never extrapolated. Per-corner values (`Corners`) follow Betaflight's Quad X motor order.
    - **`FeedSignals`**: p4-feed's `IFeedSignals` names and SI units field for field (`MotorElectricalHz`, `MotorCurrentA`, `MotorCurrentTotalA`, `MotorCurrentTotalMaxA`, `MotorCurrentStepAPerS`, `PackVolts`, `CameraAngularVelocity`, `CameraVibration` with X < 50 Hz, Y 50–200 Hz, Z > 200 Hz, `ImpactCount`, `ImpactPeakAccel`, `ImpactImpulse`, `FiberTensionN`, `FiberMinBendRadiusM`, `LinkState` {`Intact`, `Broken`}, `LinkMarginDb`, `AirVelocityAtCamera`), plus `Step` and `Crashed`. `Live` (`FeedSignalSet`) puts each signal at bit 1 << its `FeedSignal` index; only `CameraAngularVelocity` is filled (the body rate), the rest are labelled stubs with the task that fills them. The scene's, app's and setup's rows are not physics fields.
    - **Precision:** the core is `double` throughout, with no `float` anywhere in `Runtime/{Flight,Control,Math}`; the purity scan now refuses `float` in the step (Mono evaluates float expressions in double, CoreCLR does not). The feed's adapter narrows on the Unity side.
    - **The world:** `FlightWorld` wraps `Fpv450.Sim.World.IWorldQuery`, merged from `p6-world-query-gallery`'s 1.1 (`afd554b`); the core's tests run on its `FloorWorld`. No second stub or vector type (D-041).
    - **The step:** `FlightCore.Advance(in StickSample)`, 1 ms, semi-implicit Euler, the quaternion first-order then renormalised; gravity 9.806 65 m/s² (published) is its only force until 1.3. `FlightScheduler` alone reads the clock (`Begin`, `RunDue`, `Start`, `Stop`); a pass with two or more steps due logs one `Hitch` {`Step`, `Steps`, `LateUs`}; the thread sleeps 1 ms with more than 2 ms to go and yields otherwise. `IFlightRecorder` {`RecordStep(state, stick)`, `RecordHitch`} is 1.2's seam.
    - **Randomness:** key = Mix64(seed + Mix64(FNV-1a 64 of the ASCII name)); draw i = SplitMix64's output at key + (i + 1)·γ; units from the top 53 bits. `FlightRandom.Open` once per name, `Draw` records each per-flight draw with its bound. Pinned values match an independent Python implementation.
    - **Numbers:** the free body over 10 s: position 5.0e-11 m from the exact discrete solution, quaternion norm within 2.2e-16, attitude 1.0e-5 rad from the exact rotation (the first-order update's (|ω|·dt)³/12 per step); golden `0xAECE29900A37638B` in Debug and Release. Bench on AC: p50 0.20 µs, p99 0.30 µs, 0 B.
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
  - **What:** body drag with along and across areas, the rotor H-force, coil drag below the CoM, the stand-in mean and turbulence per level, labelled.
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
