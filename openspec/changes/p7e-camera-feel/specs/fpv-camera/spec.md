## ADDED Requirements

### Requirement: The camera at each field's own time
In the FPV view the rendered camera SHALL take the sim's pose at each PAL field's own sim time, never the render clock's and never smoothed: the scene is drawn once per field (the latest field whose read is complete), with the camera at the field's reference instant (its middle line's read), at the body's attitude times the frame's elastic turn at the camera (PHY-25, D-059), and held until the next field. The feed's fields SHALL follow the flight's fields while it flies.

#### Scenario: The rendered camera tracks the sim's at field rate
- **WHEN** the repository's drone (1 kg payload, its asymmetry and modes, seed 1) punches out from a hover and the render runs at 60 and at 144 Hz on the flight's clock
- **THEN** every field is drawn once, and the rendered camera's field-to-field angular velocity differs from the mean of the sim's camera angular velocity over the same interval by at most 2 % RMS of that rate's spread over the punch

#### Scenario: The known-wrong camera fails
- **WHEN** the same flight is drawn by the render clock's interpolated rigid pose (the camera before this change) and by a camera smoothed with a 30 ms first-order lag
- **THEN** each one's error is above 20 % RMS of the same spread, so the test above would catch it

#### Scenario: The chase view stays
- **WHEN** V selects the chase view
- **THEN** the camera is placed as before (D-055) and drawn every render frame, with no shutter warp, the feed riding on top

### Requirement: The rolling-shutter read
The camera's sensor SHALL be read line by line (the build's Caddx Ratel 2 is a CMOS sensor, published): field line r is read at (25 + r + ½) × 64 µs after the field starts (PAL timing, published), and its picture SHALL be the scene rotated by the camera's turn between the field's reference instant and that read, from the sim's poses alone (D-008). The OSD is inserted after the camera and is never warped.

#### Scenario: A turn skews the field
- **WHEN** the camera turns at a constant 300 °/s in yaw (the pilot's full stick) over a field
- **THEN** the first line's read is turned against the last's by 300 °/s × 286 × 64 µs = 5.49° (±1 %), the middle line's not at all

#### Scenario: Off reads the scene as before
- **WHEN** the shutter is off in the feed's settings
- **THEN** the field pass reads the scene by the code path it had before this change, and the FEED-00 still A/B is unchanged

### Requirement: Motion blur within the exposure
Each line SHALL be the mean of the scene over its exposure, the window ending at its read, sampled along the camera's actual rotation in that window (C4): up to 8 taps where the exposure's sweep exceeds one render pixel, one tap otherwise. The exposure SHALL come from the scene's light through the reflected-light meter equation, clamped to the Ratel 2's published 1/100 000–1/50 s; while the light is unmeasured (the AE loop, `p4-feed` 1.7) it is the labelled daylight stand-in, 1/8192 s.

#### Scenario: Daylight stays crisp
- **WHEN** the light is unmeasured or daylight's (4096 cd/m²) and the camera turns at 300 °/s
- **THEN** each line uses one tap: the 122 µs exposure sweeps 0.6 render pixels

#### Scenario: Night smears
- **WHEN** the scene's light is 8 cd/m² or less and the camera turns at 100 °/s
- **THEN** the exposure is 1/50 s and each line averages 8 taps spread over its 20 ms window

### Requirement: The shake is the physics'
Every motion the camera shows SHALL come from the sim's poses (the rigid body, the frame's modes): the renderer adds no random jitter, no shake of its own and no filter.

#### Scenario: A still drone shows a still picture
- **WHEN** the drone sits disarmed on its legs
- **THEN** every line's rotation is the identity within 1e-9 rad, and every field's camera pose is the rest pose
