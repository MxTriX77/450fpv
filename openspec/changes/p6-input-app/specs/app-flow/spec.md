## ADDED Requirements

### Requirement: The menu and the setup
The main menu SHALL offer Train, Calibrate and Exit (plus Explore in review builds), plainly built and readable (APP-02; judged at G7 by D-042). The setup SHALL offer payload 0–2.0 kg in 0.1 kg steps, legs on or off, the time of day (continuous, night included), wind Calm / Windy / Severe and rain Sunny / Rain / Severe rain and the start point (APP-03), with no other settings.

#### Scenario: Into the air in a few steps
- **WHEN** the pilot starts the app with a calibrated profile
- **THEN** Train, the setup's Start and arming the switch take them to a flight over the test range (D-044) with no other screen in between

### Requirement: The flight session over the test range
A flight session SHALL:
- load the test range and its world from `maps/test-range/placements.json` (D-044; a flat floor until its queries land, D-046)
- place the launch stand at the start point when the legs are off
- start the physics thread with the setup's payload, legs, wind and rain and the profile's `FcSettings`
- show the drone at the interpolated `PoseFrame` through the FPV camera
- arm and disarm on the transmitter switch, and offer pause and restart

The drone SHALL be a labelled stand-in until `p5-drone`, and the camera a clean camera with FEED-02's intrinsics until `p4-feed`'s rig lands.

#### Scenario: Pause and restart
- **WHEN** the pilot pauses mid-flight, then restarts
- **THEN** the physics stops stepping on pause and logs it, and the restart begins a new flight from the start point with a new seed, logged

### Requirement: The flight ends, and the pilot reads why
The flight SHALL end on a crash (an impact over its threshold, or upside down on the ground), a battery cut-off or a disarm (APP-04; the fiber's ends come with 7d). The post-flight summary SHALL show the flight from its log: time, distance, the end and its cause (the part and the object for a crash), battery used, and the seed.

#### Scenario: A crash into the car
- **WHEN** the drone flies into R01_A and crashes
- **THEN** the summary names the crash, the part that hit, `R01_A` and the material it touched, read from the log

### Requirement: Persistence
Calibration profiles, rates, the setup and the quality preset SHALL be saved to the user's profile folder and restored on the next start (APP-05).

#### Scenario: Settings survive a restart
- **WHEN** the pilot changes the roll rate and the payload, quits and starts again
- **THEN** both are restored as left
