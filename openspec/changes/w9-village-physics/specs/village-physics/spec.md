## ADDED Requirements

### Requirement: Every ground interaction of the test range works on Village
Every ground and vegetation interaction the test range has (slopes that tip the drone, legs that snag, soft soil that legs sink into and stick in, straw and grass, micro detail, furrows, prop strikes on the ground, the fiber on the ground) SHALL work on the farmland from the farmland's own data, or be shown in `audit.md` to be absent from the farmland for a physical reason (PHY-01, PHY-03, PHY-04, PHY-05).

#### Scenario: The audit covers every interaction
- **WHEN** `audit.md` is read against the test range's ground and contact code and the model-79 flights
- **THEN** it lists every interaction, and for each one it names the data the farmland feeds it, a test or measurement that shows it working, or the gap and the task that closes it

#### Scenario: A landing on a slope
- **WHEN** a scripted landing (seeded, logged) sets the drone down on a farmland slope across its fall line, legs on, at a slope given in the run
- **THEN** the drone stands, slides or tips as the leg contact and friction compute, the run prints the slope, the legs' loads and the outcome, and the same run on a test-range slope of the same angle and surface gives the same outcome within 1°

#### Scenario: A landing on soft soil
- **WHEN** a scripted landing sets the drone down on the farmland's softest soil class at 1 m/s
- **THEN** the legs sink by the depth the soil model computes for that class (printed, with the class's bearing parameters), the same as on a test-range cell of that class within 1 mm

### Requirement: The farmland has a stem field
The sim SHALL query, per cell of the farmland, the grass and straw there: stem height, stem density and stem kind, derived from the map package's export data, never hand-painted (AST-10, WLD-11).

#### Scenario: The field matches the drawn grass
- **WHEN** `validate.py` checks the farmland package
- **THEN** every cell's stem density is derived from the export's grass density and surface class by a rule written in design.md, and cells with no drawn grass have no stems

### Requirement: Stems strike and are cut by the props
A rotor whose blade disc meets grass, straw or crown leaves SHALL strike and cut stems, each cut taking an impulse and an energy from published stem properties (diameter, density, cutting energy or shear strength) out of the rotor's torque and thrust. The drone's shaking SHALL be that impulse train through the airframe, never noise on the pose (PHY-05, PHY-24, PHY-26).

#### Scenario: Landing in tall straw
- **WHEN** a scripted landing (seeded) descends at 0.5 m/s into the tallest straw cells of the farmland
- **THEN** the log shows cut events per blade, each with its stem kind and energy, the rotors' speed dips by the computed torque, the body's vibration has its peak at the blade-pass frequency with an amplitude computed from the impulses (printed against a hand calculation in design.md within 10 %), and no random value is added to the pose

#### Scenario: Leaves in a crown
- **WHEN** a scripted flight pushes a rotor into a tree crown at 2 m/s
- **THEN** the cut leaves and twigs take their energy from constants in `physics_materials.json`, each labelled with its source

#### Scenario: Determinism and cost
- **WHEN** the straw landing and the crown flight replay headless
- **THEN** they replay bit for bit, allocate 0 B in steady state, and the step stays within PLT-04's budget on a quiet machine

### Requirement: Cut bits show in front of the camera
The feed SHALL draw cut stem pieces in front of the camera from the sim's logged cut events, so what the pilot sees cut is what the physics cut (PHY-25).

#### Scenario: A replay draws the same bits
- **WHEN** the straw landing is replayed in the player
- **THEN** the cut pieces appear at the cut events' positions and times, and a replay of the same log draws the same pieces
