# Spec Delta

## Purpose

The rigid body at the centre of the simulator: its frames, its state, the mass and inertia it gets from the pilot's settings, the aerodynamic forces on the airframe itself, and the fixed step that integrates all of it reproducibly and fast enough. Everything else in the flight model adds forces to this body.

## ADDED Requirements

### Requirement: Fixed step at 1 kHz or more
The flight model SHALL integrate with a fixed timestep of at most 1 ms, using semi-implicit (symplectic) Euler, on a thread of its own, and SHALL NOT use Godot's rigid-body integration (D-002, D-010).
The step size SHALL NOT vary with frame rate, load or wall-clock time. When the model thread cannot keep up, it SHALL log a `hitch` event and catch up with additional whole steps of the same size.
The model SHALL publish a double-buffered pose for the renderer to interpolate, and SHALL NOT read the scene tree from the stepping thread.

#### Scenario: The step never varies
- **WHEN** a 60 s flight runs while the renderer is deliberately stalled for 250 ms twice, and the machine is loaded to 100 % on all cores
- **THEN** the log contains exactly 60,000 steps of 1 ms each (± 0 steps), the stalls appear as `hitch` events, and the final state is bit-identical to the same flight run without the stalls

#### Scenario: No Godot in the loop
- **WHEN** the flight model is stepped from `tools/flightbench` with no Godot process running
- **THEN** a full flight runs to completion, producing the same states as the same seed produces inside the game

#### Scenario: Frame rate does not change the physics
- **WHEN** the same seed and the same recorded stick sequence are flown with the renderer capped at 30, 60 and 240 fps
- **THEN** all three logs are bit-identical

### Requirement: Frames and state
The model SHALL use the world frame of `world-query`: right-handed, **Y up**, metres. The body frame SHALL be x forward, y up, z right, with its origin at the arm-plane centre and **not** at the centre of mass, because the centre of mass moves as the coil pays out.
Attitude SHALL be integrated as a unit quaternion, renormalised every step. Euler angles SHALL exist only for logging and for the simulated pilot, in the ZYX order and with the exact `roll_deg`, `pitch_deg` and `yaw_rate_dps` definitions of `wind.md` §6.2, so a sim run is directly comparable with the tracked reference clip.

#### Scenario: Euler output matches the reference definition
- **WHEN** a run's logged attitude is converted to the `attitude.csv` columns and compared with an independent evaluation of the `wind.md` §6.2 formulas from the logged quaternion
- **THEN** every row agrees within 1e-6 deg

#### Scenario: Quaternion stays normalised
- **WHEN** a 10 min flight with sustained maximum rates on all three axes finishes
- **THEN** the attitude quaternion's norm never leaves 1 ± 1e-9, and no Euler singularity produces a non-finite value at any pitch attitude, including straight up and straight down

### Requirement: Mass and inertia from components
Mass, centre of mass and the full inertia tensor SHALL be built by summing the airframe's parts with the parallel-axis theorem, from the component layout of `wind.md` §5.5, and SHALL be rebuilt whenever a part's mass or position changes: the pilot setting payload, the coil paying out, or the legs setting removing the leg members.
The model SHALL NOT take mass, centre of mass or inertia as independently tuned scalars.

#### Scenario: Reproduces the reference loadouts
- **WHEN** the build runs at the seven loadouts of `wind.md` §5.5 (3.6, 4.4, 4.8, 4.9, 5.5, 5.8 and 6.5 kg)
- **THEN** roll inertia falls in 0.033–0.057 kg·m², pitch in 0.040–0.072 and yaw in 0.043–0.060, and at the nominal 4.8 kg they are 0.044, 0.054 and 0.050 kg·m² within 5 %

#### Scenario: Pitch inertia exceeds roll, and yaw is close to roll
- **WHEN** the inertia is built at any loadout that carries cargo
- **THEN** pitch inertia is greater than roll inertia, and yaw inertia is 1.0–1.3 × roll inertia

#### Scenario: Centre of mass moves with the coil
- **WHEN** the coil pays out from full to empty at a fixed payload
- **THEN** the centre of mass rises monotonically from 1–2 cm below the arm plane to about 1 cm above it, and the total mass falls by the coil's mass

#### Scenario: Payload setting changes the airframe, not a fudge factor
- **WHEN** the pilot's payload setting is changed from minimum to maximum
- **THEN** total mass, centre-of-mass position and all three inertias change together, consistently with a cargo box at the layout's stated position, and the change is recorded in the log header

### Requirement: Airframe aerodynamic forces
The model SHALL apply aerodynamic forces from the relative wind (body velocity minus the local wind at each application point), as **forces at their own points of application**, not as moments alone:
- body drag, with separate along-axis and across-axis areas, at the body's centre of pressure
- coil drag at the coil, which sits below the centre of mass, so a side gust on the coil is also a rolling moment
- rotor edgewise (H-force) drag on each disc
- a weathervaning moment from the offset between the centre of pressure and the centre of mass

#### Scenario: A crosswind produces the measured lean
- **WHEN** the drone is trimmed in steady flight at the nominal 4.8 kg loadout in a steady crosswind, over a range of crosswind speeds
- **THEN** the steady lean into the wind is 1.8° per m/s within the derived range 0.95–2.6° per m/s (`wind.md` §5.3)

#### Scenario: A side gust rolls the drone without any pilot input
- **WHEN** a steady 5 m/s side gust is applied to a hovering drone with a full coil, with all stick inputs held at neutral and the rate controller's I-term disabled
- **THEN** the drone both translates downwind and rolls into the wind, and the rolling moment is consistent with the coil drag acting at its logged offset below the centre of mass within 10 %

#### Scenario: Gusts are forces, not pose offsets
- **WHEN** any gust or turbulence is applied at any level
- **THEN** it reaches the body only through the aerodynamic force terms above, and no code path adds a displacement, a velocity, an attitude or an angular-rate offset directly to the integrated state

### Requirement: Deterministic math in the flight loop
Every arithmetic operation in the stepped flight model SHALL use only correctly rounded IEEE operations (+ − × ÷ √). `Math.Sin`, `Math.Cos`, `Math.Tan`, `Math.Exp`, `Math.Pow`, `Math.Log` and their variants SHALL NOT appear in the stepped path, and FMA SHALL NOT be used implicitly. Transcendental needs SHALL be met by the model's own polynomial or rational approximations, following the `world-query` W-13 rule.

#### Scenario: No transcendentals in the stepped path
- **WHEN** the flight model's stepped assemblies are scanned for calls to the forbidden `Math` members, including through the trigonometric helpers of `Vector3`, `Quaternion` and `Basis`
- **THEN** there are no hits, and the check runs as part of the test suite so a later edit cannot reintroduce one

#### Scenario: Same bits in Debug and Release
- **WHEN** the same seed and stick sequence are flown in a Debug and a Release build
- **THEN** every logged state is bit-identical

#### Scenario: Same bits on another machine
- **WHEN** the same seed and stick sequence are flown on a second x64 machine with different ISA support
- **THEN** every logged state is bit-identical, or the run refuses to start because the header's recorded ISA flags do not match

### Requirement: Step budget
With the full world loaded, one flight step SHALL complete within **250 µs** on the dev machine on AC power, measured at the 99th percentile over a 60 s flight that includes ground contact on all four legs, a tether paid out, and turbulence near obstacles. The stepped path SHALL allocate nothing on the managed heap in steady state.
Micro-detail prefetch and the wake precompute SHALL run on worker threads and SHALL NOT appear in a step.
The benchmark SHALL print the CPU clock and the power mode, following the `world-query` W-16 precedent.

#### Scenario: Budget met with the world loaded
- **WHEN** `tools/flightbench --bench` runs the worst-case flight on the dev machine on AC power
- **THEN** the 99th-percentile step is under 250 µs, the median is reported, zero bytes are allocated during the timed section, and the clock and power mode are printed

#### Scenario: Sim rate holds with the renderer running
- **WHEN** the game runs a flight in `sample_patch` with the renderer at its normal load
- **THEN** the model completes at least 1,000 steps per wall-clock second averaged over 60 s, and the log records no more than 1 `hitch` event
