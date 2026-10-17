# Spec Delta

## Purpose

The manifesto's method, stated in §2.4: "physics are not random", but a fully deterministic model of everything would need a mainframe, so the sim mimics real-life realism with "a tiny, cunning, and elegant randomness". This capability says exactly where that randomness enters, what bounds it, how it is seeded, and how a flight stays reproducible — which is what separates this approach from the noise that makes other simulators feel wrong after ten minutes.

It is a cross-cutting capability on purpose. Randomness in a flight model cannot be reviewed subsystem by subsystem; it has to be reviewable as one list.

## ADDED Requirements

### Requirement: Randomness enters only as a bounded physical parameter
Every random number in the flight model SHALL be a perturbation of a named physical parameter, within a stated bound, drawn from a seeded stream.
No random number SHALL be added to, or scaled into, any of: a position, a velocity, an attitude, an angular rate, a force, a moment, or a filter on the model's output. No smoothing, jitter or dither SHALL be applied to the published pose.

#### Scenario: The catalogue is complete and matches the code
- **WHEN** the flight model's source is searched for every use of a random generator
- **THEN** each one is reached from the documented catalogue of perturbed parameters, each has a named physical parameter and a stated bound, and there are no uncatalogued uses

#### Scenario: No noise on the output
- **WHEN** the path from the integrated state to the published pose is reviewed
- **THEN** it contains no random term, no smoothing filter and no dither, and the published pose is the integrated state

#### Scenario: Every draw stays in its bound
- **WHEN** 10,000 flight seeds are drawn and every per-flight parameter is recorded
- **THEN** every value lies within its stated bound, and the distribution over seeds covers the bound rather than clustering at its centre

### Requirement: One flight seed, independent named streams
There SHALL be a single 64-bit flight seed, shown to the pilot and written to the log header. Every generator SHALL be derived from it by hashing the seed with a stream name, so that:
- adding a new perturbation later SHALL NOT change any existing stream's sequence
- a log written before the addition SHALL still replay exactly

Streams SHALL be classified and documented as **per-flight draws** (made once at arm time, written to the header), **evolving streams** (stepped during the flight), or **world-derived** (not drawn at all).

#### Scenario: A new stream does not disturb the old ones
- **WHEN** a new named perturbation stream is added to the model and a previously recorded flight is replayed
- **THEN** the replay still reproduces every logged state bit-for-bit

#### Scenario: Streams are independent
- **WHEN** one stream's parameter bound is changed and a flight is re-run on the same seed
- **THEN** only that parameter's drawn value changes, and every other drawn value in the header is unchanged

#### Scenario: The seed is visible and sufficient
- **WHEN** a pilot notes a flight's seed and the same seed, settings, map and stick sequence are used again
- **THEN** the flight reproduces exactly, and the seed plus the header is all that is needed

### Requirement: The ground is not random
Everything about the world under the drone — ground height, micro-relief, ridges, pitfalls, cover density, the position and stiffness of every stem and straw, and every hook release force — SHALL come from the **map** seed through `world-query`, and SHALL NOT be re-drawn from the flight seed.
The same landing spot SHALL therefore behave the same way on every flight, so the pilot can learn it. The ground is unknown, not random.

#### Scenario: The same spot behaves the same way
- **WHEN** the drone lands at the same position on the same map with 20 different flight seeds, from an identical approach
- **THEN** the same pitfalls, stems and hook release forces are encountered every time, and any difference between runs traces to a logged flight-seed draw

#### Scenario: A different map seed gives different ground
- **WHEN** the same position is sampled on two map packages that differ only in their seed
- **THEN** the micro-detail, relief and pitfalls differ

### Requirement: The airframe's asymmetry budget
The per-flight draws that make the drone yank (manifesto §2.4) SHALL be: a **Kv offset per motor**, an **ESC timing offset per motor**, a **prop thrust imbalance per rotor** and a **leg stiffness offset per leg**. Each SHALL have a stated bound.
Together they SHALL produce a disturbance the pilot must correct with roll and yaw, and SHALL NOT produce a high-frequency buzz, a divergence, or a drone that cannot be flown.

#### Scenario: The asymmetry can be switched off as a whole
- **WHEN** the asymmetry is disabled by a single documented switch
- **THEN** every per-motor, per-rotor and per-leg perturbation is at its nominal value, the drone hovers far steadier, and the switch is recorded in the log header — because the Calm wind targets require this mode (`wind.md` §6.4)

#### Scenario: Bounded away from unflyable
- **WHEN** the extreme corners of every asymmetry bound are flown together, on the worst combination of signs, at the heaviest loadout
- **THEN** the drone remains controllable by the simulated pilot for 60 s without hitting the ground, and no axis saturates continuously

#### Scenario: Bounded away from imperceptible
- **WHEN** a hands-off hover is flown with every asymmetry parameter at the smallest magnitude its bound allows
- **THEN** the drone still drifts measurably within 30 s, so no seed produces a drone that holds attitude by itself

### Requirement: Evolving streams are reproducible from the header
Streams stepped during a flight — turbulence, gust arrival, spool payout irregularity — SHALL advance only as a function of the step index and their own seeded state, and SHALL NOT read the wall clock, a thread id, a hash-code, a dictionary iteration order, or any other source that can differ between runs.

#### Scenario: No wall clock or thread identity in the loop
- **WHEN** the stepped path is reviewed for sources of run-to-run variation
- **THEN** it reads no clock, no thread id, no default object hash code and no unordered-collection iteration order, and a check for these runs in the test suite

#### Scenario: Thread count does not change the flight
- **WHEN** the same flight is run with the micro-detail prefetch worker and the wake precompute worker forced to be early and then forced to be late
- **THEN** both runs are bit-identical, and the late run logs hitches where the flight thread waited

### Requirement: Every draw is logged
Every per-flight draw SHALL appear in the log header with its parameter name, its drawn value and its bound. Every evolving stream SHALL have its seeded initial state in the header. Draws made at any other time SHALL be logged as events with the step index at which they were made.

#### Scenario: The header alone reconstructs the draws
- **WHEN** a log header is read by the replay tool
- **THEN** every random value the flight used is either present in the header or reproducible from a seeded stream state in the header, and the replay needs nothing else

#### Scenario: An unlogged draw fails the replay
- **WHEN** a draw is deliberately made from an unlogged source and a flight is replayed
- **THEN** the replay's state diverges from the log and the replay check fails, which is how this requirement is enforced
