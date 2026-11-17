## ADDED Requirements

### Requirement: The nose whiskers are in the picture
The drone model SHALL carry two thin wire whiskers on its nose, as geometry with their real thickness, placed so that through the feed's lens, with the camera where it is, they appear as the two pale bars of the pilot's footage: at mid-height at the left and right of the picture, converging slightly toward its centre (VIS-07, DRN-04, REAL-01).

#### Scenario: A hover seen through the feed
- **WHEN** the drone hovers and the feed is captured
- **THEN** two thin pale bars show at the left and right of the picture where the footage's bars sit, and they are not parallel: their inner ends point slightly toward the picture's centre

#### Scenario: The camera has not moved
- **WHEN** the change is merged
- **THEN** the lens's place and intrinsics are what they were before it (FEED-02)

### Requirement: The whiskers change nothing in the flight
The whiskers SHALL have no mass, no contact shape and no force in the sim: with and without them every flight's log SHALL be identical (D-117).

#### Scenario: The same flight with and without them
- **WHEN** a flight is flown with the whiskers drawn and again with them hidden, on the same seed and sticks
- **THEN** the two logs are identical bit for bit, and no golden moves

### Requirement: The whiskers shake a little
Each whisker SHALL move as a thin cantilever in its first bending mode, driven by the air the drone flies through and by the frame's vibration, computed from the wire's size and material and never animated by hand (REAL-03).

#### Scenario: Faster flight and a gust
- **WHEN** the drone flies faster, or a gust of the flight's wind reaches it
- **THEN** the whiskers' tips shake more, at the wire's own frequency, and settle when the air does

#### Scenario: A still hover in calm
- **WHEN** the drone hovers in Calm
- **THEN** the whiskers only tremble with the frame's vibration

### Requirement: The whiskers can break
A whisker that meets the world SHALL bend away from it, and past its material's limit it SHALL stay bent or part for the rest of the flight. A replay of the flight's log SHALL show the same break in the same field (PHY-25).

#### Scenario: The nose meets a wall
- **WHEN** the drone's nose meets a wall hard enough to bend one whisker past its limit
- **THEN** that whisker is drawn bent or gone for the rest of the flight, the other is unchanged, and the flight itself is as it would be without them

#### Scenario: A replay
- **WHEN** that flight's log is replayed
- **THEN** the whisker breaks in the same field
