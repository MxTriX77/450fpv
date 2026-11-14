## ADDED Requirements

### Requirement: A player's flight is the physics line's flight
A flight flown in the player SHALL be built from the same setup as the headless tools build for the same choices, seed and sticks, with every model the physics line flies by default, and SHALL log bit for bit what the headless replay logs (LOG-01, PLT-02).

#### Scenario: The same flight in Unity and headless
- **WHEN** a session's flight is flown in Unity's EditMode with a fixed seed and scripted sticks, and the same flight is flown by `tools/replay`
- **THEN** both logs are bit-identical, records and events, and the log's model number is the line's

#### Scenario: The legs as the pilot's hands made them
- **WHEN** two sessions' flights with legs on are built with different seeds
- **THEN** each draws its legs' stiffness from its own seed, the log records the draw, and the two draws differ; a flight with legs off draws none

### Requirement: The summary names what the props cut
The post-flight summary SHALL name the stems the props cut in the flight, from the log's stem events alone, beside the incidents it already names (PHY-05, APP-04).

#### Scenario: A landing in straw
- **WHEN** a session's flight lands in the farmland's tall straw and the props cut stems
- **THEN** the log holds the stems' events and the summary has a line that names the cut

#### Scenario: A flight that cuts nothing
- **WHEN** a session's flight never meets a stem
- **THEN** the summary has no such line

### Requirement: Unity's own tests hold on the line's physics
The Unity project SHALL compile against the sim core at the line's model with no error, and its EditMode suite SHALL pass, with every zero-allocation check read through the probe that is not fooled by a background collection.

#### Scenario: The EditMode suite
- **WHEN** Unity runs the EditMode tests in batch mode on the merged tree
- **THEN** every test passes, and no zero-allocation check reads the raw per-thread counter
