# Spec Delta

## Purpose

The fiber-optic tether, which the pilot says changes your approach to piloting more than anything else (manifesto §2.5): you fly smooth orbits instead of crazy turns, you cannot reverse sharply, and the line may or may not cut. A spool pays out beneath the drone, the paid-out span hangs and drags and catches on things, and the drone gets lighter as it flies. The tether also produces the tension, bend and vibration signals that the analog feed will later read as link margin.

## ADDED Requirements

### Requirement: The spool pays out and never takes line back
The drone SHALL carry a spool that pays line out when the span tension at the exit exceeds the spool's payout threshold, and SHALL NOT be able to wind line back in. Paid-out length SHALL be an integrated state.
The coil's mass SHALL fall as line pays out and SHALL feed the mass and inertia build, so the drone becomes lighter and its centre of mass rises during a flight.

#### Scenario: Line pays out as the drone flies away
- **WHEN** the drone flies a straight course away from its launch point
- **THEN** paid-out length increases monotonically and is at least the straight-line distance from the launch point

#### Scenario: Line never comes back
- **WHEN** the drone flies out and then returns toward its launch point
- **THEN** paid-out length does not decrease, and the slack appears in the span's shape rather than being absorbed by the spool

#### Scenario: The drone gets lighter
- **WHEN** a flight pays out a substantial length of fiber
- **THEN** total mass falls by the mass of the line that left, the centre of mass rises, and the inertia is rebuilt, all consistently with the coil's linear density

### Requirement: The paid-out span is simulated, not abstracted
The span between the drone and its launch point SHALL be a chain of at most 32 nodes carrying gravity, aerodynamic drag along their length from the local wind, and contact with the ground, objects and cover through the same `SampleGround`, `StaticContacts` and `MicroDetailNear` calls the airframe uses. Twigs SHALL act as capstans and hooking straws SHALL catch it, as `surfaces-review.md` §6.4 defines.
The node count SHALL stay within the per-step world-query budget of `surfaces-review.md` §6.1 (≤ 32 node ground samples and ≤ 32 swept segments).

#### Scenario: The span hangs and lies on the ground
- **WHEN** the drone hovers 30 m from its launch point with more line paid out than the straight-line distance
- **THEN** the span sags under gravity and the slack part rests on the ground at the local `SupportTop`, rather than floating or hanging straight

#### Scenario: Wind moves the span
- **WHEN** a crosswind blows across a paid-out span
- **THEN** the span is displaced downwind and vibrates, and the vibration amplitude grows with crosswind speed

#### Scenario: The span catches on vegetation
- **WHEN** the span is dragged across standing weeds and lying twigs
- **THEN** contacts are reported, hooking elements catch it by `Id`, twigs act as capstans, and the resulting tension rise is logged

#### Scenario: The span stays inside the query budget
- **WHEN** the worst-case tether configuration is stepped
- **THEN** the tether's world queries per step do not exceed the `surfaces-review.md` §6.1 allocation, and the total step stays inside the flight-dynamics budget

### Requirement: The tether pulls and rolls the drone
Span tension SHALL be applied to the airframe at the spool's exit point, which lies below the centre of mass, so a loaded tether both decelerates and rolls or pitches the drone. The tether SHALL NOT be modelled as a pure force through the centre of mass.

#### Scenario: A loaded tether tilts the drone
- **WHEN** the drone accelerates away from its launch point fast enough to load the line, with all sticks held
- **THEN** the drone is both pulled back and rotated by the tension acting at the logged exit offset, and the moment matches the logged tension and offset within 10 %

#### Scenario: Sharp reverse loads the line
- **WHEN** the drone flies out and then reverses sharply back along its own path
- **THEN** the slack line cannot be taken up by the spool, the span's own shape and contacts resist, and the logged tension rises well above its steady cruise value

#### Scenario: A smooth orbit does not
- **WHEN** the drone flies a smooth orbit at the same speed as the sharp reverse
- **THEN** the logged peak tension is materially lower than in the reverse case, which is the difference the pilot describes between orbits and crazy turns

### Requirement: The line may cut, but never certainly
The tether SHALL be able to break by three physical mechanisms, each compared against a fiber strength drawn **once per flight** from the flight seed within a bounded range and written to the log header:
- **tension** exceeding the breaking load
- **bend radius** below the minimum, over a contact edge, using the `edge_radius_m` the catalog material table provides
- **abrasion** accumulated by rubbing against an edge or vegetation under load, integrating toward a threshold

A break SHALL be logged as an event naming the mechanism, the value that caused it, the drawn strength and the location. The same manoeuvre on two flight seeds SHALL be able to give different outcomes, and the outcome SHALL be fully determined by the seed.

#### Scenario: The same manoeuvre can go either way
- **WHEN** an identical aggressive reverse manoeuvre is flown from the same position on at least 20 flight seeds
- **THEN** some seeds break the line and others do not, every break names its mechanism and the drawn strength, and each individual seed gives the same outcome every time it is replayed

#### Scenario: Breaking is graded, not binary in cause
- **WHEN** the three mechanisms are exercised separately — a pure tension spike, a tight bend over a sharp steel edge at low tension, and prolonged rubbing at moderate tension
- **THEN** each can independently produce a break, and the logged mechanism matches the one exercised

#### Scenario: Gentle flying does not cut the line
- **WHEN** a full-length flight of smooth orbits and gentle climbs is flown at Calm on 20 seeds
- **THEN** no seed breaks the line, and the logged peak tension and minimum bend radius stay clear of the drawn limits

#### Scenario: A break is not the end of the physics
- **WHEN** the line breaks
- **THEN** the tether's force on the airframe falls to zero, the drone remains flyable as a rigid body, the remaining coil mass stays on board, and the link state logs as broken

### Requirement: Link signals for the feed
The model SHALL compute and log, at 50 Hz or faster: tension at the spool exit, the minimum bend radius along the paid-out span, whether the span is touching vegetation, the span's wind-driven vibration amplitude, and the link state. These are the drivers `video-feed.md` N12 requires.
This change SHALL NOT hand these signals to `game/src/video/`; that is the later interface change.

#### Scenario: Every link signal is present
- **WHEN** a flight log is read
- **THEN** tension, minimum bend radius, a vegetation-contact flag, vibration amplitude and link state are present at 50 Hz or faster for the whole flight, each with its unit

#### Scenario: The signals respond to the flight
- **WHEN** the drone crosses a tree belt at low level, dragging the span through it
- **THEN** logged tension and vibration rise, minimum bend radius falls where the span passes over branches, and the vegetation-contact flag is set

### Requirement: A wedged span degrades safely
A span configuration that cannot be resolved — slack with nowhere to go, a self-intersecting loop — SHALL be detected and resolved as a tension rise, which may break the line. It SHALL NOT produce a non-finite value, an energy gain, or a step that exceeds the budget.

#### Scenario: A deliberate wedge does not explode
- **WHEN** the drone is flown to create the worst slack-and-loop configuration reachable, including flying repeatedly through its own paid-out span
- **THEN** every logged state stays finite, total energy does not increase without a force to account for it, the step budget holds, and the outcome is either a resolved span or a logged break
