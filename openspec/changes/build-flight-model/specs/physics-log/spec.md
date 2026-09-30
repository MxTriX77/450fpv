# Spec Delta

## Purpose

The manifesto's closing requirement: "if for example i test and i fuck up my drone - i must know what actually happened in terms of physics to be sure it's not a bug". A log that only shows a trajectory cannot do that. This capability defines what is recorded, at what rate, how a flight is replayed headlessly without Godot (D-010), and the specific forensic questions a log must be able to answer so QA can name a cause instead of guessing.

## ADDED Requirements

### Requirement: Log header fixes the whole flight
Every log SHALL begin with a header holding everything that does not change during the flight:
- the 64-bit flight seed
- every per-flight draw with its parameter name, value and bound (see `flight-randomness`)
- the seeded initial state of every evolving stream
- the pilot's settings: payload, legs on or off, weather level, time of day
- the resulting mass, centre of mass and inertia tensor
- the map package id, the `world-query` content hash and `QueryVersion`
- every runtime object added after the world reset — asset, pose and order — because `api-review.md` §6 notes these are not covered by the content hash
- the build identity, the .NET runtime version, the ISA flags, and whether the asymmetry switch was on

#### Scenario: Header is complete
- **WHEN** any flight log is opened
- **THEN** every field above is present and readable, and the header is self-describing enough that a reader written against a later version can report which fields it does not know

#### Scenario: A world mismatch is refused
- **WHEN** a log is replayed against a map whose content hash or `QueryVersion` differs from the header
- **THEN** the replay refuses to run and reports which of the two differs, rather than producing a wrong result

### Requirement: Full state at the step rate
The log SHALL record the complete integrated state every step at the model's step rate: position, velocity, orientation, body angular rate, per-rotor command and speed, pack voltage, current and state of charge, per-leg plastic set and stick-point state, paid-out tether length and node states, and the net force and moment on the body.
Records SHALL be packed fixed-layout binary, written to a ring buffer and flushed on a worker thread, so logging never stalls a step.

#### Scenario: Every step is recorded
- **WHEN** a 60 s flight finishes
- **THEN** the log holds exactly 60,000 full-state records with no gaps, and the record count matches the step count in the header's summary

#### Scenario: Logging does not stall the step
- **WHEN** the step budget benchmark runs with logging enabled and with it disabled
- **THEN** the 99th-percentile step time with logging enabled is within the flight-dynamics budget, and the difference between the two is reported

#### Scenario: Size budget
- **WHEN** a 10 min flight is logged
- **THEN** the file size is within the stated budget per minute of flight, and the budget is documented in the change's design

### Requirement: Events with causes
The log SHALL record, timestamped at the step rate, at least these events with the fields that identify their cause:
- contact made and broken: the part, the object or terrain, the surface and material, the `FeatureId` and depth if a pitfall, the impact speed and impulse
- hook attach and release: the element `Id`, kind, surface and the release force
- `tunnel`: a part that ended a step through an object, which means a physics bug
- prop strike: the element `Id` and the disc
- `mixer_saturation`: the axis that lost authority
- `vrs_enter` and `vrs_exit`: the descent rate
- spool payout milestones and tether break: the mechanism, the value that caused it and the drawn strength
- `hitch`: the step at which the model thread waited or fell behind, and why
- impact: peak acceleration, contact point and rigid-body impulse

#### Scenario: A crash names its cause
- **WHEN** the drone is crashed in each of five deliberate ways — flown into a wall, tipped over on liftoff by a hooked leg, bounced off the legs and rolled, sunk by a vortex-ring descent, and dropped after a tether break during a hard reverse
- **THEN** in every case the log alone identifies which mechanism happened, with the part, the object or element, and the numbers behind it, and a reader who did not watch the flight reaches the same conclusion

#### Scenario: A bug is distinguishable from pilot error
- **WHEN** QA reviews a crash log
- **THEN** it can determine whether any `tunnel` event occurred, whether any state went non-finite, whether the step rate held, and whether a motor was saturated — so "the model misbehaved" and "the pilot held the sticks" are separable conclusions

#### Scenario: Contacts carry their surface
- **WHEN** a landing on any of the nine surfaces is logged
- **THEN** each foot contact names the surface it touched and the material, and a pitfall entry names the hole

### Requirement: Headless bit-exact replay
`tools/flightbench --replay <log>` SHALL re-run the flight from its header and its recorded stick sequence **without Godot**, and SHALL reproduce every logged full-state record bit-for-bit. It SHALL report the first step at which any divergence occurs, with the field that diverged.

#### Scenario: A flight replays exactly
- **WHEN** a 60 s flight that includes ground contact on all four legs, a hooked liftoff, a tether paid out and turbulence near a tree belt is replayed
- **THEN** every one of the 60,000 records matches bit-for-bit, and the tool exits zero

#### Scenario: Replay needs no Godot
- **WHEN** the replay runs on a machine with no Godot installed and no display
- **THEN** it completes and reports its result

#### Scenario: Divergence is pinpointed
- **WHEN** a log is deliberately altered in one field of one record and replayed
- **THEN** the tool reports that step index and that field, and exits non-zero

#### Scenario: Replay on another machine
- **WHEN** a log recorded on the dev machine is replayed on a second x64 machine
- **THEN** either it matches bit-for-bit, or it refuses to run because the header's ISA flags or runtime version differ — never a silent mismatch

### Requirement: A readable summary
The log reader SHALL be able to print a human-readable summary of a flight without a game running: the header, a timeline of every event, and the state around any chosen step. This is what QA reads first and what the pilot's feedback is discussed against.

#### Scenario: Summary of a crash
- **WHEN** the reader is pointed at a crashed flight
- **THEN** it prints the header, the event timeline, and the full state over a stated window around the last contact, in text, with units on every value

#### Scenario: Works on a truncated log
- **WHEN** a log is truncated mid-record, as a crash of the game itself would leave it
- **THEN** the reader reports everything up to the last complete record and says the log is truncated, rather than failing
