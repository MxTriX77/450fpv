## ADDED Requirements

### Requirement: A fixed 1 kHz step on its own thread
The flight model SHALL advance in fixed steps of exactly 1 ms on a thread of its own, in the engine-free sim package, and publish a double-buffered pose that the renderer interpolates; the engine SHALL never integrate the drone. A step that falls behind real time SHALL be caught up with real 1 ms steps and logged as a `hitch`, never stretched.

#### Scenario: Render rate does not touch the flight
- **WHEN** the same seed and the same logged stick inputs are flown with render caps of 30, 60 and 240 fps, and once with a 250 ms renderer stall
- **THEN** the four logs are bit-identical apart from their hitch and render records

#### Scenario: The worker's timing does not touch the flight
- **WHEN** the same flight on the same stick stream is stepped by a worker on time, by one forced early (waking before its steps are due) and by one forced late (each pass catching up 2–40 steps)
- **THEN** the three logs' step records and events are byte-identical apart from the hitches, and each late pass is one logged hitch

#### Scenario: Nothing outside the state enters a step
- **WHEN** the suite scans the step's code paths
- **THEN** no step reads a wall clock, a thread id, a default hash code or an unordered collection's order

### Requirement: Mass and inertia from the drone's components
The model SHALL build the mass, the centre of mass and the inertia tensor from `drone.json` (DRN-03: every part's mass, size, position and material) by the parallel-axis theorem, at the pilot's payload and legs setting, and SHALL rebuild them when a part's mass changes.

#### Scenario: The bootstrap drone matches K§5.2
- **WHEN** the bootstrap `drone.json` (K§5.2's assumed layout) is built at its nominal loadout
- **THEN** the mass is 4.8 kg within 1 %, and roll, pitch and yaw inertia are 0.044 / 0.054 / 0.050 kg·m² within 5 %, with pitch above roll and yaw between 1.0 and 1.3 × roll

### Requirement: The rigid body
The model SHALL integrate a rigid body with a unit-quaternion attitude by semi-implicit Euler at dt = 1 ms, renormalising the quaternion every step, in the frame D-019 fixes. The attitude SHALL turn at the step's midpoint rate (the implicit midpoint rule on Euler's equation), so that a torque-free body keeps both its angular momentum and its energy.

#### Scenario: Free flight conserves what it should
- **WHEN** a torque-free body spinning at 2 rad/s about each principal axis in turn, with no gravity and no aero, is stepped for 60 s
- **THEN** its angular momentum stays within 0.1 % and the quaternion's norm within 1e-9 of 1

#### Scenario: A tumble keeps its energy
- **WHEN** a torque-free body of the repository's drone, at any loadout, tumbling at 4 rad/s off its principal axes with no gravity and no aero, is stepped for 60 s
- **THEN** its rotational energy and its angular momentum stay within 1e-9 of their start
- **AND** a step that turns the attitude at the step's starting rate gains energy past 1e-4

### Requirement: Aero basics
The model SHALL apply body drag on the relative wind (velocity minus the local wind) with separate along and across areas, a rotor H-force growing with airspeed (and with the rotor's speed), and the coil's drag at the coil, where `drone.json` places it, each on the air's velocity at its own point. The rotors' spin momentum SHALL turn the body as Euler's equation says (the gyroscopic moment −ω × h).

#### Scenario: A crosswind leans the drone
- **WHEN** the repository's drone holds a hover position in a steady 5 m/s crosswind, under the closed-loop simulated pilot
- **THEN** it leans into the wind by 0.95–2.6° per m/s of crosswind (K§6.5, derived; nominal 1.8°), and the lean's direction follows the wind's
- **AND** cruising at wind.md's nominal 13 m/s, where its drag flies clip P's 22.7° of pitch (22.4–23.0°), each m/s of crosswind needs 0.95–2.6° more lean with the nose kept on the track
- **AND** a model without the rotors' in-plane drag, or without any drag, leans less than 0.95° per m/s

#### Scenario: The rotors' spin keeps the total momentum
- **WHEN** a body carrying rotors of spin momentum h tumbles with no outside moment for 10 s
- **THEN** its total angular momentum I·ω + h stays constant in the world within 1 %, and with the gyroscopic moment's sign reversed or left out it does not

### Requirement: Deterministic math and the step budget
The flight loop SHALL use only correctly rounded IEEE operations (+ − × ÷ √) and the project's `DetMath`, with no implicit FMA, SHALL allocate no managed memory in steady state, and SHALL meet PLT-04: p99 step time at most 300 µs on the dev machine, measured on AC power with the power mode printed.

#### Scenario: The budget on AC
- **WHEN** `tools/flightbench --bench` flies a 60 s case on AC power with the full basic model
- **THEN** it prints the p50 and p99 step times, p99 is at most 300 µs, and the steady-state allocation count is 0
- **AND** it prints the world queries' share of the step, in the air, on the floor and on the stand
