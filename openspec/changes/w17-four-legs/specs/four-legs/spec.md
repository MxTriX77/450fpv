## ADDED Requirements

### Requirement: The drone stands on four separate legs
The legs-on drone SHALL stand on four separate legs as photo W shows them: each a narrow straight tube under its arm, pointing perpendicularly into the ground and ending in its own tip, with no rail joining them, in the drawn model, in `drone.json` and in the collision alike (DRN-01, DRN-03, REAL-01, REAL-05).

#### Scenario: The model and the physics agree
- **WHEN** the legs-on drone is built by its generator and loaded by the sim
- **THEN** it has four leg tips as its lowest contact points, no skid part, and every drawn leg has its contact shape

#### Scenario: The legs are the photo's
- **WHEN** the legs-on model is set beside photo W
- **THEN** each leg is a straight tube standing perpendicular to the ground where the photo has it along its arm, with the photo's length and thickness within the photo's measuring error

### Requirement: A leg's tip digs into soil by the soil's own law
A leg's tip SHALL penetrate a surface by that surface's own bearing resistance, under the drone's weight and at a touchdown, and SHALL resist sliding by the soil in front of it while it is in (PHY-01, PHY-03).

#### Scenario: A rough landing digs deeper
- **WHEN** the drone touches down on soft soil at 1.5 m/s and at 0.5 m/s
- **THEN** the tips end deeper after the rougher touchdown, by the hand calculation for that surface

#### Scenario: Hard ground takes no tip
- **WHEN** the drone touches down on asphalt or a concrete slab
- **THEN** no tip penetrates, and no leg is held at the next liftoff

#### Scenario: No sliding like a sled
- **WHEN** the legs stand in soil and the thrust is below the drone's weight with a small sideways push
- **THEN** the drone does not run along the ground: it stays, or turns about the legs that hold

### Requirement: A dug-in leg holds without storing a jump
The hold on a leg leaving the soil SHALL be the friction and adhesion on the shaft still in the soil, falling to nothing as the tip comes out, and SHALL store no energy that is given back at release (PHY-03).

#### Scenario: A careful liftoff
- **WHEN** the throttle rises slowly from a start with the legs standing in soil
- **THEN** the legs leave one by one at their own moments, the drone tips toward the legs still held, and its upward speed as the last leg lets go is no more than the thrust over the weight gives

### Requirement: Each flight starts on its own footing
At the start each leg SHALL stand slightly in the ground to its own depth, a bounded draw of a named physical parameter from a seeded stream, written in the log (PHY-24, LOG-01).

#### Scenario: Two flights differ and one replays
- **WHEN** two flights start on the same soil with different seeds, and one of them is replayed
- **THEN** the two starts have different depths per leg and lift off differently on the same sticks, and the replay is bit for bit

### Requirement: A landing has the drone's weight
A touchdown's energy SHALL go into the soil under the tips, the legs and their clamps as the physics gives it, with the rebound and the slide within the hand calculation for the drone's mass on that surface (PHY-02).

#### Scenario: A touchdown on sod
- **WHEN** the drone touches down level on meadow sod at 0.5 m/s with the throttle cut
- **THEN** it rebounds no higher and slides no farther than the hand calculation gives, and comes to rest within the calculated time

### Requirement: The flight model is untouched
A change to the legs and the ground SHALL move a flight only through the ground contact and the drone's new mass, and every golden SHALL be re-recorded once with an audit against the previous model (LOG-02).

#### Scenario: The kept audit
- **WHEN** a golden is flown with the previous drone file and the previous ground law
- **THEN** it is the previous model's pin, every record and event
