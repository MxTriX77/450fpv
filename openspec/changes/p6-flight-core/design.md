# Design: p6-flight-core

## Context
- The flight model was designed in the first attempt and never built: `docs/knowledge.md` §6 condenses `docs/legacy/flight-model/design.md` and its seven specs. This change builds the part PHB-01…06 asks for, on the final architecture.
- The sim package holds only `DetMath` today. `tools/flightbench` and `tools/simtests` exist and compile the package's sources (`sim-core-build`).
- Frames are D-019's:
  - world: Unity's, +X east, +Y up, +Z north, left-handed, SI
  - body: x right, y up, z forward, origin at the arm-plane centre
  - Euler angles ZYX for logging only
- The physics engineer may refine D-019 here, before code, as D-019 allows.

## Goals / Non-Goals
**Goals:**
- the core phase 7 extends without rewriting
- the basic model the pilot first flies
- the interfaces the other G7 lanes build against, fixed in task 1.1 (D-041)

**Non-Goals:** the proposal's.

## Decisions
1. **The shared interfaces, fixed first** (task 1.1, D-041; in the sim package, engine-free):
   - **`StickSample`:** a host timestamp in µs, roll, pitch, yaw and throttle in [−1, 1] or [0, 1] after calibration, and the arm switch. The input layer writes it (`p6-input-app`, INP-04).
     - The sim consumes it at step boundaries by the contract INP-04 fixes: the sample in force at a step is the latest whose timestamp is at or before the step's time, mapped once.
     - Every consumed sample is logged, which is what makes replay possible.
   - **`FcSettings`:** one record named by Betaflight CLI names (D-023): rates (`roll_rc_rate`, `roll_srate`, `roll_expo`, … for ACTUAL), `thr_mid`, `thr_expo`, deadband, RC smoothing, feedforward, the PIDs, `d_min`, I-term relax, throttle boost. Its defaults come from `docs/reference-notes/fc-profiles.md` per D-023. The input layer edits it in Calibrate.
   - **`PoseFrame`:** the double-buffered output, with the sim time, position, attitude, velocity, body rates, rotor speeds and the arm state. The renderer interpolates between the last two frames by its own clock; it never feeds back.
   - **`FeedSignals`:** the K§11.8 list, published with the pose and filled as mechanisms land. This change fills motor speeds, pack voltage, the camera's angular rate and crash; the rest stay labelled stubs.
   - **`IWorldQuery`:** `SampleGround(x, z)`, `StaticContacts(swept shapes)` with the Time < 1 latch, and `Raycast`, as `p6-world-query-gallery` defines them from K§10.4. This change carries a flat-ground stub that implements them for its own tests.
2. **The step and its thread:**
   - **The scheduler:** a dedicated thread runs whole 1 ms steps against a real-time target. Only the scheduler reads the wall clock, never the step. A late step is caught up with real steps and logged as a `hitch` (PLT-04).
   - **The integrator:** semi-implicit Euler (K§6.2), the quaternion renormalised each step, with no sub-stepping except the flight controller's 4 kHz loop (decision 4).
   - **Memory:** zero managed allocation in steady state. Every buffer is preallocated at arm time.
3. **`drone.json`** (DRN-03):
   - **The schema:** `parts[]` (name, kind, mass, box or cylinder size, position, material), `motors[]` (position, spin direction, Kv, the lag τ_m), `props` (diameter, blades, k_T, κ), `packs` (series and parallel cells, R_cell, the OCV curve), `legs` (length, stiffness, damping, foot radius), `coil`, `stand`.
   - **The bootstrap:** K§5.2's assumed layout at nominal 4.8 kg (derived), with each value labelled. `p5-drone` replaces it with the researched build.
   - **The mass build:** a parallel-axis sum. It runs again when the payload changes.
4. **The flight controller** (D-023):
   - Betaflight 4.5's math, implemented from its published formulas and documentation. **Betaflight's GPL source is read for the formulas, never copied.**
   - **The rate path:** ACTUAL rates map the stick to a setpoint in °/s, after the deadband and the RC smoothing filter.
   - **The PID:** Betaflight's scaling (P × 0.032029, I × 0.244381, D × 0.000529, the published constants, checked against the source in task 1.4), with D min, I-term relax and feedforward.
   - **Throttle:** the throttle curve and throttle boost.
   - **The loop:** 4 kHz, four sub-steps per 1 ms step, using the gyro value of the current state (no sensor noise in this change).
5. **Propulsion:**
   - **The mixer:** an X mixer clips each motor to [0, 1], then desaturates the collective (K§5.3).
   - **The ESC:** a first-order command filter, then the motor's first-order lag. τ_esc ≈ 5 ms and τ_m ≈ 40–60 ms for a 3115 motor on a 10-inch 3-blade prop are assumed, and the exact update is n += (n_cmd − n)·dt/(τ + dt), with no `exp`.
   - **Thrust and torque:** T = k_T·n², Q = κ·T (κ 0.015–0.02, assumed). n_max is Kv·V times the unloaded-to-loaded factor, and k_T is set so T_max is K§5.3's nominal 2.6 kgf per motor at full charge (derived).
   - **The inflow derate:** momentum theory with axial inflow V_a through the disc, solving T = 2ρA·v_i·(v_i + V_a) for v_i in closed form (a quadratic, √ only). The thrust at a given n follows the blade's loss of angle of attack, T(n, V_a) = k_T·n²·(1 − V_a / (n·p)) with the prop's pitch speed n·p, clamped at 0 (assumed; the form the legacy draft used, PHB-03's L11 case).
   - **Battery:** V = n_s·(OCV(SoC) − I_cell·R_cell), with R_cell 15 mΩ (assumed) and the OCV curve of a 21700 Li-ion cell, 4.2 → 3.0 V (assumed; table in `drone.json`). The SoC is integrated from I.
6. **Aero basics** (K§6.5):
   - **Body drag:** F = ½ρ·C_d·A_dir·|v_rel|·v_rel with along and across areas (assumed), on v_rel = v − wind at the body.
   - **The rotor H-force:** proportional to the in-plane airspeed and the thrust (assumed coefficient).
   - **Coil drag:** at the coil, where `drone.json` puts it. The bootstrap is K§5.2's, 8–9 cm below the CoM (derived); `p5-drone` moves it to letter T's layout (D-043: above the CoM on the legs-on build).
   - **Calibration:** the areas are tuned to 0.95–2.6° of lean per m/s of crosswind (derived, nominal 1.8°).
7. **Contacts:**
   - **Legs:** each foot is a spring-damper in series with the ground: k and c in `drone.json` (assumed), the ground from `SampleGround` (height, normal and surface, rigid at this stage; soil is 7a).
   - **Swept shapes:** arms, body, coil and prop discs against `StaticContacts` with the Time < 1 latch (K§7.5), load-bearing parts as spheres (PHB-05).
   - **The stand:** the four-post stand is a runtime object with its own pose, logged as asset, pose and order.
   - **A crash:** an impulse or peak acceleration over a threshold (assumed: 50 g peak, or a prop disc hitting a rigid shape at over 3 m/s), named with the part and the object.
8. **The stand-in wind** (PHB-06):
   - **The mean:** per level, Calm 0.8, Windy 2.5 and Severe 5 m/s mean crosswind (K§8.6, derived), each band ±25 %. Direction and strength are drawn per flight (uniform), both logged.
   - **Turbulence:** a first-order Gauss–Markov per axis, σ/U 0.12–0.15 and time constant L_u/U with L_u 115–200 m (K§6.7), from the named stream `wind.turbulence`.
   - Labelled `stand-in` in the header.
9. **Randomness** (K§6.9):
   - **The seed:** one 64-bit flight seed. Streams come from a 64-bit hash of (seed, name) into a counter-based generator.
   - **This change's draws:** `wind.direction`, `wind.strength` (per flight), and `wind.turbulence` (evolving).
   - Every draw is logged with its name and bound.
10. **The log:**
    - **The header:** the seed, the draws, `drone.json`'s hash, `FcSettings`, the world content hash and query version, the build, runtime and ISA flags.
    - **The records:** fixed-layout binary at 1 kHz (state, inputs, rotors, pack, net force and moment; about 200 B a step, about 12 MB a minute, assumed), plus events.
    - **Writing:** a preallocated ring buffer and a flush worker. The step never waits on disk; a full ring logs a hitch.
    - **Replay:** `tools/replay` compiles the same sources and re-runs from the header and the logged inputs, comparing each record bit for bit.

## Risks / Trade-offs
- **The FC model's fidelity:** Betaflight's formulas are mirrored, not its filters. The gyro is clean until a sensor model is asked for. The builder PIDs' stability on the plant is the check (PHB-03).
- **Assumed constants:** τ_m, the drag areas, the leg k and c and the crash threshold are assumed, labelled and tuned to the derived targets (liftoff 50–65 %, lean per m/s, the bounce). The pilot's flights then tune feel, never quantities (D-024).
- **The interface lock:** later lanes depend on decision 1, so changing it after task 1.1 means a superseding decision, not a quiet edit.

## Migration Plan
- **New code only:** `drone.json` is new, and the bootstrap is replaced by `p5-drone`'s file at its merge, with the mass-build test re-pinned.

## Open Questions
- Sensor noise and the gyro filter chain: deferred until the pilot asks (D-023: optional).
