# Proposal: p6-flight-core

**Owner:** physics-engineer · **Reviewers:** game-developer (the stick and settings records, the pose the scene reads), world-builder (the world-query interface the contacts consume), qa-engineer
**Requirements:** PHB-01…06, LOG-01, LOG-02, LOG-05, PLT-04, DRN-03 (the schema and a bootstrap file), INP-03 (the flight-controller semantics), INP-04 (consuming the timing contract) · **Decisions:** D-006, D-012, D-019, D-023, D-024, D-042 · **VISION:** §2 (physics that feel like the real heavy quad: the liftoff, the lag behind the sticks, the motors out of sync), §3 (a deterministic, logged physics core)
**Source:** `docs/knowledge.md` §5.2–5.4 (the mass build, thrust and authority) and §6 (the flight model as designed, not built); `docs/legacy/flight-model/` (the first attempt's design and its seven specs); `docs/reference-notes/fc-profiles.md` (the pilot's Betaflight profiles).

## Why
D-042 puts the first flights (G7) ahead of the world. The flight model was designed in the first attempt and never built. This change builds its final architecture and the basic model the pilot first flies: the 1 kHz engine-free core, the Betaflight 4.5 controller, the propulsion chain, aero, contacts, a stand-in wind, and the log with bit-exact replay. Phase 7 adds mechanisms to it and never rewrites it (PHB-01).

## What Changes
- **The core:**
  - in `unity/Packages/com.fpv450.sim`, engine-free (D-006)
  - a fixed 1 ms step on its own thread, the state as K§6.2 gives it
  - a double-buffered pose the renderer interpolates
  - seeded named streams, every draw logged
  - `DetMath` only (no runtime transcendentals, no implicit FMA)
  - zero managed allocation in steady state
- **The interfaces other lanes build against, fixed first:**
  - the stick sample the input layer writes (four axes, the arm switch, a host timestamp)
  - the flight-controller settings record (Betaflight CLI names, D-023)
  - the pose and the feed signals the scene reads
  - the world-query calls the contacts make (`SampleGround`, `StaticContacts`, `Raycast`; from `p6-world-query-gallery`)
- **`drone.json`:** the schema (DRN-03) and a bootstrap file from K§5.2's assumed layout, labelled bootstrap until `p5-drone` writes the researched one. The mass and inertia are built from it by the parallel-axis theorem.
- **The flight controller,** a Betaflight 4.5 model at its real 4 kHz inside the 1 kHz step:
  - ACTUAL rates, the throttle curve, deadband, RC smoothing, feedforward
  - PID with Betaflight's scaling, D min, I-term relax, throttle boost
  - acro only
  - the real builder's PIDs as defaults
- **Propulsion:**
  - an X mixer that saturates honestly
  - ESC and motor first-order lags
  - T = k_T·n² and Q = κ·T
  - the forward-flight derate from axial inflow through the tilted disc
  - battery sag on two packs
- **Aero basics:** body drag on the relative wind with along and across areas, rotor H-force, and coil drag at the coil as `drone.json` places it.
- **Contacts:**
  - legs as spring-dampers on `SampleGround`
  - the launch stand and every object through `StaticContacts` with the Time < 1 latch rule
  - crash detection, and `tunnel` events logged as bugs
- **A stand-in wind** (PHB-06): a mean wind per level plus simple turbulence, labelled stand-in until `p7c-wind`.
- **The log and replay:**
  - one binary file: a header that fixes the flight, the full state and inputs at 1 kHz, events with causes
  - `tools/replay` reproducing every state bit-for-bit without Unity
  - a summary reader that survives a truncated log

## Capabilities

### New Capabilities
- `flight-dynamics`: frames, state, the step and its thread, the mass and inertia build from `drone.json`, the rigid body, aero basics, deterministic math, the step budget
- `propulsion`: the Betaflight 4.5 controller, the mixer, ESC and motor lags, thrust and torque, the inflow derate, battery sag
- `ground-contact`: legs on the ground, the launch stand, object contacts with the Time < 1 rule, crash and tunnel detection
- `wind-field`: the stand-in mean wind and turbulence (replaced by `p7c-wind`)
- `flight-randomness`: the flight seed, named streams, the rule that randomness is only a bounded perturbation of a named physical parameter
- `physics-log`: the header, the 1 kHz state and inputs, events, headless bit-exact replay, the summary

### Modified Capabilities
- None. `sim-core-build`'s rules (engine-free package, sources compiled by the tools) hold unchanged.

## Non-goals
- **The phase-7 mechanisms:**
  - motor asynchrony, ground effect, VRS and prop wash (`p7b-airframe`)
  - the real wind field, wakes, vorticity and rain (`p7c-wind`)
  - soil, mat, hooks and stiction (7a)
  - the fiber (7d, after G7 by D-042)
  - the forensics report (7e)
- The researched drone (`p5-drone` replaces the bootstrap `drone.json`).
- The TX12 input, calibration, menus and the flight session (`p6-input-app`).
- The world queries themselves (`p6-world-query-gallery`).
- The feed and its signals' look (`p4-feed`).

## Impact
- **New code:**
  - `unity/Packages/com.fpv450.sim/Runtime/{Flight,Control,Contact,Wind,Log}/`
  - `tools/flightbench` (cases, golden, bench), `tools/replay`
  - `assets-src/drone/drone.json` (bootstrap)
- **Consumers:**
  - `p6-input-app`: writes stick samples and settings, reads the pose and the summary
  - `p6-world-query-gallery`: answers the contacts
  - `p4-feed`: later reads the signals
  - `p7b-airframe` and `p7c-wind` extend `propulsion` and `wind-field`
