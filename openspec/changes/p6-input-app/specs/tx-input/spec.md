## ADDED Requirements

### Requirement: The radio through a timing contract
The input layer SHALL read the RadioMaster TX12 in USB joystick mode (EdgeTX or OpenTX) and generic HID joysticks off the render thread. It SHALL timestamp each report on arrival with a high-resolution clock, and pass it lock-free to the physics thread as `StickSample` (`p6-flight-core` design 1). Stick reading SHALL never depend on the render frame rate. The TX12's report interval SHALL be measured and recorded, and the end-to-end latency from a stick movement to the motor command measured and reported (INP-04).

#### Scenario: Render rate does not touch the sticks
- **WHEN** the same recorded HID report stream is played into the input layer at render caps of 30, 60 and 240 fps
- **THEN** the `StickSample` sequence the physics thread receives is identical in values and timestamps

#### Scenario: Latency is measured
- **WHEN** the latency test runs with a TX12 connected
- **THEN** it prints the measured report interval and the stick-to-motor-command latency (median and p99) in ms, and records both in the flight log's header

### Requirement: Calibration with a health check
The calibration wizard SHALL take the pilot through centring and a sweep of the extremes, detect the channel order (AETR or TAER), reversal, endpoints and deadband, assign the arm switch, and show live raw and calibrated bars. It SHALL save named profiles. It SHALL say plainly when an axis spans a tiny range (it behaves like a switch: recalibrate the radio), and name any radio-side curve (expo or dual rates, an endpoint short of full travel) and tell the pilot to switch it off (INP-02).

#### Scenario: The TX12 pitfall is caught
- **WHEN** a recorded sweep with axis spans of 79 and 85 (fc-profiles: the pilot's TX12 MKII on EdgeTX 2.8.0) is calibrated
- **THEN** the wizard names both axes as too narrow and tells the pilot to recalibrate the radio, where spans of 683–903 pass

#### Scenario: Under two minutes, no keyboard
- **WHEN** a TX12 is calibrated from the wizard's first screen
- **THEN** it completes in under 2 minutes with the radio alone after the start

### Requirement: Rates and the throttle curve in Betaflight's terms
The pilot SHALL edit ACTUAL rates (centre sensitivity, full-stick rate and expo per axis) and the throttle curve (`thr_mid`, `thr_expo`, Betaflight's 11-point table) with curve previews. Settings SHALL be stored under Betaflight CLI names in `FcSettings`, with the defaults of `fc-profiles.md`'s sim default column, and the settings layer SHALL be able to expose Betaflight's other flight-feel settings without a redesign (INP-03, INP-07).

#### Scenario: The defaults are the sim default column
- **WHEN** a fresh profile is created
- **THEN** roll and pitch are 55 / 225 °/s with expo 0.34, yaw is 85 / 260 °/s with expo 0.15, `thr_mid` is 50, `thr_expo` is 10, deadband is 2 / 2, RC smoothing is 50 and feedforward is 50, and airmode and motor idle are the profile's

### Requirement: Disconnect and the keyboard fallback
USB loss SHALL failsafe-disarm the drone and be logged (INP-05). A keyboard fallback SHALL exist for development only, and SHALL be marked in the log so it is never used for tuning (INP-06).

#### Scenario: Pulling the cable mid-flight
- **WHEN** the TX12's USB is unplugged in flight
- **THEN** the drone disarms within one RC frame of the loss being detected (the controller reads the sticks at the link's 250 Hz, so at most 4 ms), and the log records the failsafe with its time

#### Scenario: Pulling the cable during a pause
- **WHEN** the radio is lost while the flight is paused, even if it is plugged back in with the arm switch on before Resume, and even if the pilot pauses again within that first RC frame
- **THEN** the failsafe is in force for the first RC frame after Resume, the log records it with the loss's time, and the flight ends on the failsafe
