# Proposal: p7b-airframe

**Owner:** physics-engineer · **Reviewers:** world-builder (the rotor rays against the world's ground and roofs), tech-artist (the camera-vibration signal the feed reads), qa-engineer; **the pilot at G7**
**Requirements:** PHY-07, PHY-08, PHY-09, PHY-10, PHY-11, PHY-12, PHY-14's Calm floor, PLT-04 · **Decisions:** D-024 (computed, never asked), D-042 (the airframe effects are part of G7), D-045 (the builder's filter chain) · **VISION:** §2 points 4 and 6 (motors "a tad bit asynchronously" yanking the drone; the slow glide-path landing), §2's "undefined and, sometimes, unexpected dynamics"
**Source:** `docs/knowledge.md` §5.3, §6.4 and §6.9; `p6-flight-core` 1.4–1.6's as-built notes (propulsion, the ESC limit, the filter chain, contacts); the pilot's answers in `docs/pilot-questions.md` (settling into the wash: "not a question of speed of descent but mostly how much throttle power ... as opposed to drones weight. wobble - yes"; the throttle-punch shake "doesn't happen much").

## Why
The flight core's propulsion is a clean, symmetric plant. The pilot's manifesto lists what real heavy quads do and no simulator has: motors that run a tad asynchronously and yank the drone, the drone settling into its own wash and wobbling, the shake after a punched-out descent, the cushion near the ground and under a roof, a battery that runs out of margin. These are G7's airframe effects (D-042), each a bounded physical mechanism, never noise on the pose.

## What Changes
- **Motor asynchrony** (PHY-07): Kv spread, ESC timing jitter and prop imbalance drawn per flight within researched component tolerances, named, bounded and logged; the drift they cause computed into a band, never asked. Calm's "never perfectly still" floor (PHY-14) comes from here.
- **Ground and ceiling effect, recirculation** (PHY-08): per rotor, Cheeseman–Bennett from per-rotor rays combined with `SampleGround`'s relief and mat top, the ceiling effect under roofs, and prop-wash recirculation in a close hover, never still.
- **Settling into the own wash** (PHY-09): vortex ring state with hysteresis, its onset governed by thrust relative to weight while descending (the pilot's account), entry and exit logged, the wobble from the physics.
- **The throttle-punch shake** (PHY-10): from wash re-ingestion, frame resonance near the 50–60 Hz the real filters notch (D-045's chain is in), and the payload and spool pendulum; seen mainly as camera shake, rare, varying, never tuned to a pilot number; it publishes the camera-vibration signal.
- **Battery and inertia character** (PHY-11, PHY-12): the heavy corner's lost margin and the low-voltage warning signal for the OSD; pitch inertia above roll, yaw at 1.0–1.3 × roll, yaw saturating first, checked on the real `drone.json`.

## Capabilities

### New Capabilities
- `airframe-effects`: the asymmetry, ground and ceiling effect, recirculation, vortex ring state, the throttle-punch shake, the battery's margin and the inertia character, each with its computed band

### Modified Capabilities
- None. (`p6-flight-core`'s `propulsion` is still in flight.)

## Non-goals
- **Wind and turbulence** (`p7c-wind`).
- **Ground materials, hooks and stiction** (7a).
- **The fiber** (7d), **breakage and plastic damage** (phase 7 later).

## Impact
- **Changed:** `Runtime/Flight/Propulsion.cs` and `FlightCore.cs` (per-rotor effects), the randomness streams (new named draws), the log (each mechanism's state and events), the goldens (re-recorded, audited).
- **Reads:** `Raycast` and `SampleGround` per rotor (`p6-world-query-gallery`), the test range's roofs and ground.
- **Runs beside `p7c-wind`:** both touch the per-rotor inflow; the two changes agree its seam first (D-041).
