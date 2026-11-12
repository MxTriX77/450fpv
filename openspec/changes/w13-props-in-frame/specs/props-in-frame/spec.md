## ADDED Requirements

### Requirement: The drawn props turn with the sim's rotors
In the first-person and the chase view, each prop SHALL be drawn at the sim's rotor angle for the moment the video field is exposed, never at a fixed angle while its rotor turns (REAL-01, PHY-25).

#### Scenario: Two fields in a hover
- **WHEN** the drone hovers and two consecutive fields are drawn
- **THEN** each prop's drawn angle equals the sim's rotor angle at that field's exposure within the smear's width, and the two fields' angles differ by the rotation between them (modulo the blade spacing), as a test on the transforms shows

#### Scenario: A rotor at rest
- **WHEN** the drone is disarmed, or a rotor is stopped
- **THEN** that prop is drawn still, at the angle the sim holds

#### Scenario: Nothing feeds back
- **WHEN** a fixed-input flight is flown with the props drawn and with the drone's model hidden
- **THEN** the two flight logs are identical byte for byte, and no golden moves

### Requirement: A blade in the frame looks as the footage shows it
A blade crossing the first-person picture SHALL appear as a sliver whose angle changes from field to field, smeared over the camera's exposure time by the rotor's speed, and SHALL be absent from the fields that miss it. Its look SHALL be judged side by side against the reference note's letters (REAL-03).

#### Scenario: Over 24 fields in a hover
- **WHEN** 24 consecutive fields of a hover are captured through the feed
- **THEN** no two consecutive fields show a blade at the same angle, the share of fields with a blade in a top corner is printed beside the reference note's counts for the sim's airframe group, and the comparison sheet is in the local-only compare folder

#### Scenario: Bright and dull light
- **WHEN** the same hover is captured at noon and under overcast
- **THEN** the blade's smear in degrees equals the rotor's speed times the exposure time the feed uses for that light, printed for both

### Requirement: The feed's look is unchanged
The change SHALL NOT alter the analog feed's pipeline, noise or OSD (D-026, FEED-00).

#### Scenario: The feed's captures
- **WHEN** the feed's A/B captures and its tests run
- **THEN** they pass unchanged
