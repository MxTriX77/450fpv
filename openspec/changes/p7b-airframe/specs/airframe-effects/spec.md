# Spec Delta

## Purpose

The airframe effects a real heavy quad shows and a clean plant does not: motor asymmetry, ground and ceiling effect, recirculation, the rotors' edgewise-flow moments, vortex ring state, the throttle-punch shake, and the battery's and inertia's character, each physical, bounded and logged.

## ADDED Requirements

### Requirement: Motor asymmetry within component tolerances
Each flight SHALL draw each motor's Kv spread, ESC timing jitter and prop imbalance once, from named seeded streams within labelled component tolerances, logged, and the drone SHALL drift and need correction at a rate inside a band computed from those tolerances.

#### Scenario: Asymmetry on and off
- **WHEN** the drone hovers on flat ground in still air for 30 s on five seeds, with asymmetry on and then off
- **THEN** with it on, the correction rate needed lies inside the computed band on every seed and never buzzes or diverges; with it off, the drift is at least halved

### Requirement: Ground effect, ceiling effect and recirculation per rotor
Each rotor SHALL gain thrust near the ground by Cheeseman–Bennett from its own ray and the ground's relief and mat top, none over a hole, and under a roof by a published ceiling-effect model from its upward ray, and a close hover SHALL never be still, its variation the re-ingested wash's from a named, logged stream and never on the pose.

#### Scenario: The cushion and the hole
- **WHEN** one rotor hovers 0.3 rotor diameters above the floor and another over a hole band
- **THEN** the first rotor's thrust at fixed speed rises by Cheeseman–Bennett's ratio within 2 %, and the second's equals its free-air thrust

#### Scenario: The ceiling under a roof
- **WHEN** one rotor hovers 0.3 rotor diameters below a runtime box roof and another in the open
- **THEN** the first rotor's thrust at fixed speed rises by the published ceiling model's ratio within 2 %, and the second's equals its free-air thrust

#### Scenario: The close hover is never still
- **WHEN** the drone without asymmetry hovers on its legs in still air with its skids about 0.1 m up, with the recirculation and with a known-wrong model without it, and the same hover 3 m up
- **THEN** every half second of the close hover moves the body above a rate floor, while the known-wrong model's and the high hover's stay below a tenth of it

### Requirement: Rotor edgewise-flow moments
Each rotor in edgewise flow SHALL flap and make a hub moment from blade-element theory on the props' sheet with labelled constants: the dissymmetry of lift's rolling moment and the back flapping's tilt away from the airflow, the in-plane force staying the H-force alone so it is not counted twice.

#### Scenario: The hub moment against its published form
- **WHEN** a constant-chord, linearly twisted blade's flap and hub moment are taken at advance ratios 0.05, 0.10 and 0.20, articulated, hingeless and stiff, and a known-wrong rigid rotor without flapping
- **THEN** the flap and hub moment equal Johnson's forms within 1e-9 at each, and the known-wrong rotor misses them by all of the moment

#### Scenario: A gust step tilts the drone harder
- **WHEN** the hovering drone meets a 4 m/s gust step from the front and from the side, with the flapping on and off
- **THEN** with it on, the peak pitch rate (front) and roll rate (side) are at least 1.5 times those without it, and the drone tilts away from the gust

### Requirement: Settling into the own wash
Vortex ring state SHALL set in from the descent rate normalised by the induced velocity at the current thrust, with hysteresis, costing thrust and adding a wandering moment, its entry and exit logged.

#### Scenario: Heavy on low throttle
- **WHEN** the 6.2 kg loadout descends vertically at 3 m/s with the throttle held low, and then the same descent at higher throttle
- **THEN** the first enters the state and logs its entry, loses thrust and wobbles; the second does not enter it; and exit needs a lower normalised descent rate than entry

### Requirement: The throttle-punch shake arises from its causes
After a fast descent is arrested by a sharp throttle increase, a damped high-frequency disturbance of 0.3–0.7 s SHALL arise from wash re-ingestion, frame resonance and the payload and spool pendulum, shown mainly as camera vibration, depending on descent rate, abruptness and payload, and not on every throttle increase.

#### Scenario: Shake only when its causes meet
- **WHEN** fifty arrested descents run across descent rates, throttle steps and payloads on several seeds
- **THEN** the shake appears only where descent rate and abruptness are both high, lasts 0.3–0.7 s when it does, varies between occurrences, and publishes `CameraVibration`; a gentle throttle increase never shows it

### Requirement: The battery's margin and the inertia character
The heavy corner SHALL lose its thrust margin as the pack sags, a low-voltage signal at the pilot's FC profile's 3.00 V a cell on the loaded voltage SHALL reach the OSD, and the inertia ratios SHALL be the ones computed from `drone.json`'s layout, with yaw saturating first (D-048).

#### Scenario: The heavy corner at low charge
- **WHEN** the 6.5 kg loadout flies full throttle from a full pack down through 20 % charge to the cut-off
- **THEN** its thrust over weight falls with the sag the pack model computes, below the margin its full pack had; the low-voltage signal sets at the pack's knee, below 20 %; and no margin is left at the cut-off

#### Scenario: The inertia character on the seven loadouts
- **WHEN** the pitch, roll and yaw inertias are built from `drone.json` on each of the seven loadouts
- **THEN** their ratios equal the ones computed from its layout exactly, and yaw saturates first on every loadout
