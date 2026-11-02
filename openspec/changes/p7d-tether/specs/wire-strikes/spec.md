# Spec Delta

## Purpose

Overhead conductors as the pilot knows them: "soft and a bit elastic", tensioned between their poles, swaying in the wind, giving under a push, stretching when the push goes on, sliding along an arm, winding round a prop or wrapping a leg, and twisting, holding or flipping the drone; a thin wire catches and tangles, a thick cable deflects, stops or flips (PR-6, D-059). Every number comes from the conductor's record and published cable theory; a strike hard enough breaks the conductor or pulls it out of its tie.

## ADDED Requirements

### Requirement: Every conductor swings in the wind
Every conductor not in a chain SHALL hang as its record's catenary turned about its chord by one first-mode pendulum angle, driven toward atan(q/w) by the wind field's drag per metre q at mid-span against its weight per metre w, at the taut string's first-mode frequency √(T/μ)/(2L), with the table's structural damping.

#### Scenario: Blow-out and frequency
- **WHEN** a steady 10 m/s wind blows square across each of `L03_thin`, `L03_medium` and `L03_thick`'s spans, and is then removed
- **THEN** each settles at atan(q/w) within 0.5°, its mid-span swing equals its sag times the sine of that angle within 1 mm, and released it swings at √(T/μ)/(2L) within 1 % (0.62 Hz for `L03_medium`)

### Requirement: A conductor near the drone becomes a chain
A conductor SHALL become a cable chain on its record's points, from its swung shape and velocity, with its record's mass per metre and EA and the rest lengths that give its record's tension T = w·L²/(8·sag), when the drone or an active fiber segment comes within 2 m of it; segments within 1 m of the drone SHALL be refined to 0.05 m and merged beyond 2 m; an intact conductor SHALL return to its swing only when every node has stayed within 5 mm of the swung shape for 1 s with no contact; at most 8 SHALL be active, overflow logged as a `WorldBug`.

#### Scenario: Activation is seamless
- **WHEN** the drone flies past `L03_medium` 1.5 m clear in Windy, never touching it
- **THEN** its four conductors activate and later return to the swing, no node jumps more than 1 µm at either change, and the conductors' swing after the pass equals a run where they were never activated within 1 mm

### Requirement: The published stiffness of a soft wire
An active conductor SHALL reproduce, from its chain alone, the taut string's sideways stiffness T·L/(x(L − x)) at a point x along its span, Irvine's linear-theory stiffness pressed down in its own plane, its slack taken up and then its stretch through EA, and its impedance 2√(Tμ) to a sudden push.

#### Scenario: Sideways and in plane
- **WHEN** a slow 2 N push is applied to `L03_medium`'s conductor across its plane at mid-span and 2 m from a pole, and then down in its plane at mid-span
- **THEN** the stiffnesses are 33.1 and 174 N/m sideways within 3 %, and 116 N/m pressed down within 5 % (λ² 240)

#### Scenario: Slack, stretch and break
- **WHEN** `L03_medium`'s conductor is pushed sideways at mid-span with its ends held rigidly until it breaks
- **THEN** the force rises at about 4T/L to 30.6 N at a 0.92 m push within 5 % (its 42.7 mm of slack used), then steepens through the conductor's stretch, and it breaks at its drawn strength after absorbing 431 J × (the draw)² within 3 %

#### Scenario: The first instant of a strike
- **WHEN** a 5 kg rigid bar meets `L03_medium`'s conductor at mid-span at 20 m/s
- **THEN** the force in the first 10 ms equals 2√(Tμ) × 20 m/s within 10 % (267 N)

### Requirement: Wires meet the drone's parts and slide along them
Each active segment SHALL meet the drone's contact shapes as a capsule of the conductor's radius, the part's stiffness in series as for every body pair, with stick anchors along the part and along the wire at the `cable` row's μ; past the anchor the contact point SHALL slide along both. The conductor SHALL bend round a part no tighter than its elastica allows from its record's bending stiffness and its tension.

#### Scenario: An arm slides along a wire
- **WHEN** the drone drifts at 0.5 m/s into `L03_medium`'s conductor with an arm across it at 30° from square, and again at 15°
- **THEN** at 30° the contact point slides out along the arm (tan 30° > 0.30) to the can and off, and at 15° it holds where it touched until the drone's push turns the angle past atan(0.30)

#### Scenario: A stiff conductor stands off a thin part
- **WHEN** a conductor at its tension is pulled round a leg tube (r 5 mm) through 90°
- **THEN** its bend radius is max(5 mm + D/2, √(EI/T)/(2·sin(22.5°))) within 1 %, and a slack 3 mm wire wraps the tube at its own radius

### Requirement: Wrapped, entangled, twisted and flipped
A conductor on two or more of the drone's parts carrying more than half the drone's weight for 0.5 s SHALL be logged as `WireEntangled`; the drone SHALL hang, swing, turn and flip on the wire only by the contacts' forces and moments.

#### Scenario: Caught above the centre of mass
- **WHEN** the drone flies at 6 m/s level into `L03_medium`'s lowest conductor at the arms' height, and again at the legs' height, holding hover throttle
- **THEN** at the arms the drone pitches over the wire toward it with the moment of the logged contact forces about the centre of mass within 1 %, and at the legs it swings under; the log names every part in contact and its force

#### Scenario: A slack wire tangles the drone
- **WHEN** the drone hovers into a synthetic dropped thin wire (`L03_thin`'s record, one end free) and yaws 180°
- **THEN** the wire wraps the legs and arms it touches, `WireEntangled` is logged once the wire carries half the weight, and the drone is held within the wire's free length of where it caught

### Requirement: Props strike and wind wires
A conductor segment inside a turning disc SHALL be struck at each blade pass; the blade SHALL stop on it (`RotorJammed`, caught) when the impulse to move the wire's chord-length of mass and its impedance over the pass, times the strike radius, reaches the rotor's angular momentum, and otherwise SHALL clear it, the rotor losing that angular momentum and the wire taking the impulse. A wire at the hub SHALL wind onto it at the hub's radius while the motor's torque exceeds the wire's tension times that radius. Each pass SHALL be written to `FlightState.BladeStrikes` (kind `wire`) for `p7f-damage`.

#### Scenario: Where the blade meets the wire
- **WHEN** a prop at 9000 rpm meets `L03_medium`'s taut conductor (331 N) at 50, 90 and 127 mm from its hub, and a slack conductor at the same radii
- **THEN** on the taut conductor the blade clears at 50 mm losing the derived 44 % of its speed within 2 %, stops at 127 mm, and is logged caught or cleared at 90 mm as the impulse rule gives; on the slack one every pass clears, and every pass appears in `BladeStrikes` in its step

#### Scenario: An intact span stalls the motor, a slack one winds
- **WHEN** a caught intact `L03_thin` conductor and a caught dropped one are each held at the hub with the motor at full command
- **THEN** the intact span stalls the rotor within its 42.7 mm of slack plus its stretch at the motor's torque, and the dropped wire winds turn after turn until its tension times 7.5 mm equals the motor's torque, `WireWound` logging the turns and the length

### Requirement: A hard strike breaks or drops a wire
An active conductor SHALL break where its tension reaches its drawn strength (`WireBroken`, the two pieces each a chain hanging from its end), and an end SHALL slip in its fitting once its tension passes the fitting's drawn hold (`WireSlip`, length drawn from the neighbouring span) and leave its seat after the table's slip length (`WireDropped`, that end free). A broken or dropped conductor SHALL stay so for the flight and, at rest on the ground or objects, SHALL freeze and remain a snag for the line, the legs and the props.

#### Scenario: Energy decides
- **WHEN** the drone strikes `L03_medium`'s conductor at mid-span at 5, 10, 15 and 20 m/s, square, at the frame's height, on 20 seeds each
- **THEN** no strike below the energy at which its fittings first slip (derived from the drawn holds) slips or breaks anything, every slip, drop and break is logged with the energy the drone gave the wire, the wire's tension and the fitting or place, and the energy budget of the drone and the wire closes within 1 %

### Requirement: Wires are physics' own
Physics SHALL meet conductors only through its own set (swung or active), with the world's wire filter leaving them out of its `StaticContacts` and `Raycast` calls; the rotor rays SHALL never read a wire as a ceiling or a floor; the world's wire SHALL stay the rest shape in the content hash.

#### Scenario: A rotor ray passes a wire
- **WHEN** a rotor's down ray is cast through `L03_medium`'s conductor onto the ground beneath
- **THEN** the ray returns the ground, and the content hash is unchanged by any strike

### Requirement: Thin against thick
`tools/flightbench --wires` SHALL strike synthetic spans of `L03_thin`'s and `L03_thick`'s records 100 times each on a deterministic grid (speeds 5–20 m/s, headings, heights through the props, the frame and the legs, places along the span) and class each outcome 2 s after the first touch as wrapped, entangled, held, stopped, deflected, flipped, broken or dropped; thin SHALL wrap, entangle or hold more often than thick, and thick SHALL deflect, stop or flip more often than thin, each by more than the two rates' 95 % binomial margin. The rigid capsule wire and a model with one mass and tension for every wire SHALL each fail it.

#### Scenario: The pilot's account of thin and thick
- **WHEN** the grid is flown with the soft wires, then with the rigid capsule wire, then with every wire given `L03_medium`'s mass and tension
- **THEN** the soft wires pass both comparisons, and each known-wrong model fails at least one, with the class counts printed for all three

### Requirement: The wires' randomness, budget and log
Each conductor's strength (`wire.strength`, 0.9–1.2 × its record's) and each end's hold (`wire.tie`, its fitting's band) SHALL be drawn on activation, bounded and logged; the active chains SHALL be preallocated with 0 B in steady state, inside the step budget, and replayed bit for bit; their nodes SHALL be published to the renderer and logged at 50 Hz with each active conductor's tension every step.

#### Scenario: Replay through a strike
- **WHEN** a flight striking `L03_medium`, winding a prop and dropping a conductor is recorded and replayed headless
- **THEN** every step record and event matches bit for bit, including `WireActivated`, `WireContact`, `WireSlip`, `WireDropped`, `WireWound` and `BladeStrikes`

### Requirement: The wires' live look
Each conductor SHALL be drawn where the physics has it: turned by its swing, along its active chain's nodes while active, its broken ends hanging and its dropped span lying where it fell, with the thin-wire material and its tube's diameter, judged against letters J and K for the intact lines and D for a slack cable draped over shrubs.

#### Scenario: The drawn wire is the physics' wire
- **WHEN** the gallery shows `L03_medium` swinging in Windy, struck and dropped, at 1.5, 10 and 50 m
- **THEN** every drawn tube's centre line lies within min(5 mm, D/2) of the frame's published physics curve, and the side-by-side sheets against J, K and D read as the same kind of line
