# Spec Delta

## Purpose
The drone's legs can bend, crack, break or come off when a landing or a strike loads them past what the class's real legs hold. A damaged leg changes how the drone stands and lands for the rest of the flight.

## ADDED Requirements

### Requirement: The legs' strengths are labelled data
Each leg's tube, its clamp under the arm and its T-joint on the skid SHALL carry their material, section, stiffness and strengths in `drone.json`, each value labelled published, measured, derived or assumed, with its source or reason. The loader SHALL refuse a leg block with a value missing its label or note. The material SHALL follow the pilot's words for the class's legs ("plastic, 25–35 cm, a little soft"); letter T sets their shape and look.

#### Scenario: Every leg value is labelled and loaded
- **WHEN** `drone.json` is loaded
- **THEN** every leg, clamp and T-joint value has a label and a note, a copy with one label removed is refused naming the field, and the mass build with the new legs matches the class within the build's tolerance (D-066)

#### Scenario: The legs' stiffness comes from their section
- **WHEN** a leg's vertical and side stiffness are computed from its material and section (and the clamp's and T-joint's give in series)
- **THEN** they are printed, labelled derived, and replace the assumed 10 kN/m. With the new values, 1.6's bounce scenario (down at 0.50 m/s on a rigid floor) still leaves again with the sticks held and rests after a throttle cut, and the leave speed and rest time are printed beside model 26's

### Requirement: A leg fails by the load it carries
Each step, each leg part's load SHALL come from the contact model: the force at the foot, the moment at the clamp, and the axial and side shares. Each failure mode SHALL compare that load with its own capacity:
- yielding of the leg, which leaves a set angle
- the clamp slipping along or rotating about the tube
- cracking, which leaves the leg weaker and softer
- fracture, which parts the tube below the clamp
- the T-joint letting its skid slip or come off

A leg below every capacity SHALL return to its shape exactly, as today.

#### Scenario: A normal landing leaves every leg intact
- **WHEN** the drone lands level (within 2°) at 0.5 m/s down with the throttle cut, on a rigid floor and on the range's soils, on seeds 1–40 of the strength draws
- **THEN** every leg ends intact on every seed, and the flight is bit for bit the flight with the damage law off

#### Scenario: A harsh landing bends a leg
- **WHEN** the drone lands level on a rigid floor at a vertical speed past the lowest leg's yield or rotation capacity (the speed derived and printed)
- **THEN** at least one leg keeps a set angle after the drone rests, the angle and the leg are logged, and the known-wrong elastic leg (today's spring) keeps none

#### Scenario: Not parallel enough to the ground
- **WHEN** the drone touches down at the same vertical speed level, and then rolled 10°, 20° and 30° so one skid lands first, on seeds 1–40
- **THEN** the level landing leaves every leg intact while the rolled landings damage the first-touching legs on a share of seeds that rises with the roll, the shares are printed, and the known-wrong that splits the load evenly over four legs damages no more legs rolled than level

#### Scenario: A hard strike breaks a leg
- **WHEN** a leg's foot meets a rigid edge sideways at a speed whose moment at the clamp passes the leg's fracture capacity (derived and printed)
- **THEN** the leg breaks at its weakest section, the stub stays on the drone, the parted piece leaves as a loose body with its own mass, and the event names the leg, the load and the object hit

#### Scenario: A leg comes off
- **WHEN** a leg's clamp carries an axial pull or a twist past its friction capacity (derived from the bolt's preload and the clamp's friction, printed)
- **THEN** the whole leg leaves the drone with its mass and its T-joint, its skid hangs on the other leg or drops with it as the T-joint's capacity decides, and the event names the leg and the cause

### Requirement: A damaged leg changes the drone
A bent leg SHALL move its contact shapes and its foot by its set angle. A cracked leg SHALL be softer by its cracked stiffness. A broken leg SHALL keep only its stub. A detached leg SHALL take its mass, centre of mass and inertia off the build, which SHALL be rebuilt exactly. The next landing SHALL go through the same contact and landing law (D-067), with nothing scripted.

#### Scenario: The build follows a lost leg
- **WHEN** one leg detaches in flight
- **THEN** the build's mass falls by the leg's mass exactly, its centre of mass and inertia equal the mass build of the remaining parts within 1e-12 relative, and angular momentum is kept through the change within 1e-12 relative

#### Scenario: The next landing feels a damaged leg
- **WHEN** the drone lands at 0.5 m/s down with 0.3 m/s of drift toward each side in turn, intact, then with one leg bent by its first set angle from the harsh-landing scenario, then with that leg missing
- **THEN** the lowest drift speed that tips it is printed for each; the missing build tips at a lower drift speed than the intact one toward the damaged side; the bent build tips at a different speed from the intact one, sooner when its foot is set inward and later when it splays outward, as the stance it leaves decides (restated after 1.3: a set splays a leg's foot outward on a level harsh landing); and the known-wrong that leaves the contact shapes in place after a bend tips like the intact one

### Requirement: Leg damage is random only in its strengths, and it is logged and replayed
Each leg's strengths SHALL be drawn once per flight within published bounds, from named seeded streams, and logged (PHY-24). A leg's state, its set angle and its stiffness SHALL be in the step record, and each change of state SHALL be an event with its cause. Replay SHALL be bit for bit through every failure. The damage law SHALL allocate nothing per step and fit the step's budget.

#### Scenario: The strength draws cover their bounds
- **WHEN** 80 000 flights' draws are taken
- **THEN** each strength reaches within 0.5 % of both its bounds, and each flight's header lists its draws with their bounds

#### Scenario: Replay through a broken and a detached leg
- **WHEN** a recorded flight that bends one leg, breaks another and loses a third is replayed headless
- **THEN** it matches bit for bit, its events name each leg's change in order, and a replay with one strength bound changed is refused

#### Scenario: The cost of the legs
- **WHEN** `flightbench --bench` flies its contact rows with the damage law on, on AC
- **THEN** each step allocates 0 B and the rows' p99 stays within the step's budget, printed beside the rows with the law off

### Requirement: The pilot sees leg damage
`PoseFrame` SHALL carry each leg's state and set angle, so the app can draw a bent, broken or missing leg. The pause menu's summary SHALL name each damaged leg, its state and what did it. A restart (R, D-078) SHALL start with intact legs.

#### Scenario: The summary names the damage
- **WHEN** a flight bends the front-right leg on a landing and loses the rear-left one on a strike
- **THEN** the summary lists both legs with their state, the landing or object, and the speed, and a restart's flight starts with four intact legs
