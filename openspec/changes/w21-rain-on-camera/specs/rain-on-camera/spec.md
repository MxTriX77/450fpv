# Spec Delta

## Purpose

Rain shows on camera as the pilot asked (D-126): drops on the lens computed from the flight, and the wet look. The flight itself is unchanged.

## ADDED Requirements

### Requirement: The water reaching the lens follows the air and the drone
The lens SHALL receive water at the rate given by the rain rate and the drops' velocity relative to the camera (the wind at the camera, the drops' fall speed, minus the camera's own velocity), projected onto the lens's facing at the drone's attitude, less what the airframe shades.

#### Scenario: Forward flight wets the lens more than a hover
- **WHEN** the same rain is flown in a level hover and in forward flight at the same attitude of the lens
- **THEN** the lens receives more water in forward flight, by the computed ratio of the relative velocities' components along the lens's facing

#### Scenario: Wind direction matters
- **WHEN** a hover in steady rain faces into the wind and then away from it
- **THEN** facing into the wind wets the lens more, as the relative velocity gives, and facing away wets it only as far as the fall and the shelter allow

#### Scenario: Sunny keeps the lens dry
- **WHEN** a flight is Sunny
- **THEN** no water reaches the lens and no drop is drawn

### Requirement: Drops on the lens move by the forces on them
Each drop on the lens SHALL stay while its adhesion holds it, and SHALL slide when the air's shear at the lens plus the force in the lens's frame (gravity at the drone's tilt, minus the camera's acceleration, plus the rotation's terms) exceeds it; drops SHALL merge, leave trails and shed by the same law, with every number labelled measured, published, derived or assumed.

#### Scenario: Tilt moves the drops
- **WHEN** the drone holds a bank in calm air with drops on the lens
- **THEN** drops large enough to overcome their adhesion slide along the lens toward the low side, and smaller ones stay

#### Scenario: Drops stay in fast flight and travel with the airflow at times
- **WHEN** the drone flies fast in rain
- **THEN** small drops stay on the lens and larger ones are driven across it by the air's shear, as FEED-13 asks

### Requirement: The lens's water never changes the flight
The lens-water model SHALL run on the flight's published state only, so the flight log and its replay are the same byte for byte with it on or off, and a replay draws the same drops.

#### Scenario: Goldens unchanged
- **WHEN** the flight and world goldens run with the model in the build
- **THEN** they pass unchanged and no model number moves

### Requirement: The drops are drawn in the feed, and wet surfaces look wet
The feed SHALL draw the lens's drops as out-of-focus refractive blobs and smears ahead of its analog stages, from the physics' rain rate; in Rain and Severe rain, wetted materials SHALL darken and gloss by a published wet-surface law from each material's albedo, and Severe rain SHALL add standing-water sheen and extinction in the distance (VIS-03).

#### Scenario: The drops take the feed's softness
- **WHEN** a rain flight is recorded with the feed on
- **THEN** the drops show through the feed's blur, noise and colour stages like the rest of the picture

#### Scenario: Wet ground darkens by the law
- **WHEN** the same patch of ground is captured Sunny and in Rain at the same light
- **THEN** its drawn luminance falls by the wet-surface law's ratio for that material, within the capture's measurement error
