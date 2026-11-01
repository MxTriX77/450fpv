## ADDED Requirements

### Requirement: The menu and the setup
The main menu SHALL offer Train, Calibrate and Exit (plus Explore in review builds), plainly built and readable (APP-02; judged at G7 by D-042). The setup SHALL offer payload 0–2.0 kg in 0.1 kg steps, legs on or off, the time of day (continuous, night included), wind Calm / Windy / Severe and rain Sunny / Rain / Severe rain and the start point (APP-03), with no other settings.

#### Scenario: Into the air in a few steps
- **WHEN** the pilot starts the app with a calibrated profile
- **THEN** Train, the setup's Start and arming the switch take them to a flight over the test range (D-044) with no other screen in between

### Requirement: The flight session over the test range
A flight session SHALL:
- load the test range and its world from `maps/test-range/placements.json` (D-044; its queries, `MapWorld`)
- place the launch stand at the start point when the legs are off
- start the physics thread with the setup's payload, legs, wind and rain and the profile's `FcSettings`
- show the drone at the interpolated `PoseFrame` through the FPV camera
- arm and disarm on the transmitter switch, and offer pause and restart

The drone SHALL be `p5-drone`'s D01 prefab for the setup's legs, and the FPV camera `p4-feed`'s rig at `drone.json`'s `camera.lens_m`.

#### Scenario: Pause and restart
- **WHEN** the pilot pauses mid-flight, then restarts
- **THEN** the physics stops stepping on pause and logs it, and the restart begins a new flight from the start point with a new seed, logged

### Requirement: V switches the view, and nothing else
In every build, V SHALL switch the flight's camera between the FPV camera at `drone.json`'s `camera.lens_m` and a third-person chase view behind and above the drone, and change nothing else (D-055): the feed, its noise and the OSD SHALL apply in both views, and the physics, the flight log and the input SHALL never see the key. The keys over a flight SHALL be Esc (pause), V (the view) and, in review builds, F (the feed off and on); C is retired.

#### Scenario: V changes only the camera
- **WHEN** the pilot presses V during a flight, and again
- **THEN** the picture switches to the chase view behind the drone and back to the lens, through the feed with its OSD in both
- **AND** the flight's log is the same, byte for byte, as without the presses, and replays bit for bit

### Requirement: The flight ends, and the pilot reads why
The flight SHALL end on a crash (an impact over its threshold, or upside down on the ground), the failsafe, a battery cut-off, or End flight from the pause menu (APP-04, D-062; the fiber's ends come with 7d), and only then SHALL the post-flight summary show. A disarm on the switch SHALL NOT end the flight: the OSD reads DISARMED, the drone stays in the world under physics, and the switch re-arms it to fly on; the log keeps the arm state of every step. The post-flight summary SHALL show the flight from its log: time, distance, the end and its cause (the part and the object for a crash), battery used, and the seed.

#### Scenario: A disarm in the air
- **WHEN** the pilot disarms on the switch in the air, then re-arms with the throttle down
- **THEN** the flight goes on with no summary, the OSD reads DISARMED and then AIR, and the drone falls under physics until the motors take it again
- **AND** End flight from the pause menu ends it, and the summary reads "ended by the pilot"

#### Scenario: A crash into the car
- **WHEN** the drone flies into R01_A and crashes
- **THEN** the summary names the crash, the part that hit, `R01_A` and the material it touched, read from the log

### Requirement: Persistence
Calibration profiles, rates, the setup and the quality preset SHALL be saved to the user's profile folder and restored on the next start (APP-05).

#### Scenario: Settings survive a restart
- **WHEN** the pilot changes the roll rate and the payload, quits and starts again
- **THEN** both are restored as left
