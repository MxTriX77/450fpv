## ADDED Requirements

### Requirement: Every ground interaction of the test range works on Village
Every ground and vegetation interaction the test range has (slopes that tip the drone, legs that snag, soft soil that legs sink into and stick in, straw and grass, micro detail, prop strikes on the ground, the fiber on the ground) SHALL work on the farmland from the farmland's own data, or be shown in `audit.md` to be absent from the farmland for a physical reason (PHY-01, PHY-03, PHY-04, PHY-05).

#### Scenario: The audit covers every interaction
- **WHEN** `audit.md` is read against the test range's ground and contact code and the model-79 flights
- **THEN** it lists every interaction, and for each one it names the data the farmland feeds it, a test or measurement that shows it working, or the gap and the task that closes it

#### Scenario: A landing on a slope
- **WHEN** a scripted landing (seeded, logged) sets the drone down on a farmland slope across its fall line and along it, legs on, at the slope the run prints
- **THEN** the drone stands, slides or tips as the leg contact and friction compute, the run prints the slope, the legs' loads and the outcome, and the same run on a slope package of the same angle and surface class (the test range has no slopes) gives the same outcome within 1°

#### Scenario: A landing on soft soil
- **WHEN** a scripted landing sets the drone down on the farmland's softest soil class (`tilled`) at 1 m/s, dry, in Rain and in Severe rain
- **THEN** the legs sink by the depth the soil model computes for that class (printed, with the class's bearing parameters), the same as on a floor of that class within 1 mm

### Requirement: A contact moment has a force behind it
In no step SHALL the contacts' moment about the centre of mass exceed the sum of the contact forces' magnitudes times the drone's reach, 0.5 m (PHY-01, PHY-04).

#### Scenario: Cut landings on slopes
- **WHEN** every slope row of `flightbench --slopes` and the audit's farmland spots run a 0.5 m/s cut landing along and across the fall line
- **THEN** no step's contact moment passes that bound, and the rule in use before this change fails the same test (its known-wrong: 1,455 N·m on 41.5 N at 9.98°)

### Requirement: A held leg is measured
The liftoff from soft and wet ground SHALL be measured by a harness that prints, per surface class and rain level, the throttle at which the drone leaves within 5 s, each corner's release time and the body's largest turn before all four are free, on level ground and on slopes (PHY-03).

#### Scenario: The liftoff harness on the farmland
- **WHEN** the harness runs on the farmland's classes, dry, in Rain and in Severe rain, level and on a 10° slope
- **THEN** it prints those values for each row, the same on a floor of the class within the printed tolerance, and each of its known-wrong models fails its row

### Requirement: The farmland has a stem field
The sim SHALL query, per cell of the farmland, the grass and straw there: stems per square metre, the height range and the stem kind (green blades, dry culms, burnt culms), derived from the map package's export data and each grass prefab's stems per instance, never hand-painted (AST-10, WLD-11).

#### Scenario: The field matches the drawn grass
- **WHEN** `validate.py` checks the farmland package
- **THEN** every cell's stems follow from the export's instance counts and the per-prefab counts by the rule written in design.md, and cells with no drawn grass have no stems

#### Scenario: The densest tall cell
- **WHEN** the world query is asked at (768.07, 917.48)
- **THEN** it returns the stems of that cell's 4 tall and 11 dry or burnt instances, up to 1.77 m tall, with 0 B allocated

### Requirement: Stems strike and are cut by the props
A rotor whose blade disc meets grass, straw or crown leaves SHALL strike and cut stems, each cut taking an impulse and an energy from published stem properties (diameter, density, cutting energy or shear strength) out of the rotor's torque and thrust. Stems the downwash bends away, or that the blade meets below their critical cutting speed, SHALL escape uncut. The drone's shaking SHALL be that impulse train through the airframe and the rotors' speed dips through the flight controller, never noise on the pose (PHY-05, PHY-24, PHY-26).

#### Scenario: Landing in tall straw
- **WHEN** a scripted landing (seeded) comes down into the farmland's densest tall cell, vertically after a hover over it and as the pilot's approach (2 m/s forward, 0.5 m/s down, a flare, the touch)
- **THEN** the field's stems stand as the world places them, each with the diameter of its drawn length and able to stand under its own weight; the slipstream folds most of those under a disc and not all; the front discs cut more than the rear ones on the approach; the cuts a front disc's leading edge meets in a creep are within 10 % of the hand calculation in design.md, and their work too; each cut is a discrete hit in the log (the blade's strikes, and a stem episode per rotor, event kind 39); the rotors' speed dips by the computed torque; and no random value is added to the pose

#### Scenario: The shake grows with the straw
- **WHEN** the same vertical landing is flown over a sweep of the stand's density (half to twice) and the stalks' diameter (0.75 to 1.5 times)
- **THEN** the body's roll and pitch rate at 4–15 Hz and the rotors' speed dips grow with the density and with the diameter, from a shake a pilot lands through to one that stalls a rotor, with no threshold tuned; the log replays bit for bit with 0 B allocated in steady state

#### Scenario: A hover cuts itself clear
- **WHEN** the drone hovers with its discs inside standing grass for 10 s
- **THEN** the cuts stop once the discs have cut their reach clear, and the stem episode ends

#### Scenario: The test range's straw
- **WHEN** a disc descends into an S04 straw patch on the test range
- **THEN** its straws are cut by the same law as the farmland's stems, and the range golden's move is audited case by case

#### Scenario: Leaves in a crown
- **WHEN** a scripted flight pushes a rotor into a tree crown at 2 m/s
- **THEN** the cut leaves and twigs take their energy from constants in `physics_materials.json`, each labelled with its source

#### Scenario: A hover in a crown
- **WHEN** the drone hovers inside a bush crown for 25 s
- **THEN** no `BladeDamaged` event reports 0 kg lost, and a blade breaks at most once at any station

#### Scenario: Determinism and cost
- **WHEN** the straw landing and the crown flight replay headless
- **THEN** they replay bit for bit, allocate 0 B in steady state, and the step stays within PLT-04's budget on a quiet machine

### Requirement: Stems act on the legs and the fiber
Standing stems SHALL act on the legs at touchdown and liftoff, and on a laid fiber, from the same stem field the props read (PHY-05, PHY-18).

#### Scenario: A landing in standing stems
- **WHEN** a scripted landing sets down in the farmland's densest tall cell
- **THEN** the run prints the stems' forces on each leg, and the legs' loads show them

#### Scenario: A line laid across a grass field
- **WHEN** a flight lays its fiber across a farmland field with tall dry grass
- **THEN** the log shows the line's contacts and slips on stems, as it does on the test range's straw

### Requirement: Cut bits show in front of the camera
The feed SHALL draw cut stem pieces in front of the camera from the sim's cuts per rotor in each step, so what the pilot sees cut is what the physics cut (PHY-25).

#### Scenario: A replay draws the same bits
- **WHEN** the straw landing is replayed in the player
- **THEN** the cut pieces appear at the cuts' positions and times, and a replay of the same log draws the same pieces
