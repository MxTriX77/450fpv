# Spec Delta

## Purpose

The airframe effects a real heavy quad shows and a clean plant does not: motor asymmetry, ground and ceiling effect, recirculation, the rotors' edgewise-flow moments, the rotors in each other's wake, vortex ring state, the throttle-punch shake, and the battery's and inertia's character, each physical, bounded and logged.

## ADDED Requirements

### Requirement: Motor asymmetry within component tolerances
Each flight SHALL draw each motor's Kv spread, ESC timing jitter and prop imbalance once, from named seeded streams within labelled component tolerances, logged, and the drone SHALL drift and need correction at a rate inside a band computed from those tolerances.

#### Scenario: Asymmetry on and off
- **WHEN** the drone hovers on flat ground in still air for 30 s on five seeds, with asymmetry on and then off
- **THEN** with it on, the correction rate needed lies inside the computed band on every seed and never buzzes or diverges; with it off, the drift is at least halved

### Requirement: The build's placement within its tolerances
Each flight SHALL draw where the pilot's hands set the bottle, each pack, the coil and the main leads, once, from the named seeded stream "build.placement", each within its labelled bound and logged (PHY-24, D-066), never as noise on the pose, so the centre of mass sits off the exact build's by what those bounds allow.

#### Scenario: Off centre, the coil tips at once
- **WHEN** the drone, legs off, rests disarmed on its coil on the floor, the exact build started level and from 0.1° of roll, and the placed build started level on twenty seeds
- **THEN** the exact build balances from level; every placed build tips onto a pack with nothing to start it, most of them sooner than the exact build from 0.1°; its centre of mass across stays inside the bounds' sum, and the flight carries its placement in the log and replays bit for bit

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

### Requirement: The drone is never perfectly still
Each rotor SHALL carry its own wake's thrust and torque unsteadiness at a published level, rising near the ground as its wash re-ingests by published numbers, and each motor's winding resistance, friction and rotor inertia SHALL be drawn per flight within published or labelled tolerances (D-064); each SHALL be a bounded perturbation of a named physical parameter from a named seeded stream, logged, never noise on the pose, and the camera's micro-motion SHALL be graded against the pilot's footage with a perfectly steady rotor as the known-wrong.

#### Scenario: The own wake is the published unsteadiness
- **WHEN** one rotor is held at a hover speed in still air with its wake, and again as a known-wrong perfectly steady rotor
- **THEN** its thrust's spread is the published share within 10 % and its correlation time the published corner's within 15 %, and the steady rotor's thrust does not move

#### Scenario: Near the ground the hover is never still
- **WHEN** the drone, flown by K§8.5's pilot with its stick noise, hovers with its camera 1.25 m and 0.3 m over the floor in still air on three seeds, with the sources on and as the known-wrong steady rotor
- **THEN** at 1.25 m the camera's roll and pitch rate spread exceed 2 °/s and their 4–15 Hz motion the footage's lowest 0.006°, while the steady rotor's stay below 0.1 °/s and 0.001°, and at 0.3 m the 1–4 Hz roll and pitch are at least 1.2× those at 1.25 m

### Requirement: Rotor edgewise-flow moments
Each rotor in edgewise flow SHALL flap and make a hub moment from blade-element theory on the props' sheet with labelled constants: the dissymmetry of lift's rolling moment and the back flapping's tilt away from the airflow, the in-plane force staying the H-force alone so it is not counted twice.

#### Scenario: The hub moment against its published form
- **WHEN** a constant-chord, linearly twisted blade's flap and hub moment are taken at advance ratios 0.05, 0.10 and 0.20, articulated, hingeless and stiff, and a known-wrong rigid rotor without flapping
- **THEN** the flap and hub moment equal Johnson's forms within 1e-9 at each, and the known-wrong rotor misses them by all of the moment

#### Scenario: A gust step tilts the drone harder
- **WHEN** the hovering drone meets a 4 m/s gust step from the front and from the side, with the flapping on and off
- **THEN** with it on, the peak pitch rate (front) and roll rate (side) are at least 1.5 times those without it, and the drone tilts away from the gust

### Requirement: The rotors in each other's wake
In forward flight each rotor's wake SHALL reach the other rotors' discs as an axial inflow from a published wake model with labelled constants: a downwash on the rotors behind it and an upwash on those beside and ahead, from its skew (the advance ratio and the inflow through its tilted disc) and the discs' spacing, formed only while the flow carries the wake away; its own unsteadiness SHALL be a bounded fluctuation of that inflow from a named, logged stream, never on the pose.

#### Scenario: The wake model against its published form
- **WHEN** the wake's normal velocity is taken over the disc at skews of 30°, 60° and 75°, and in the disc's plane outside an unskewed wake
- **THEN** its gradient along the flow is Coleman's tan(χ/2) within 0.2 % and flat across it, and outside the unskewed wake it is nothing (within 0.002 v_i)

#### Scenario: The rear rotors lose thrust in forward flight
- **WHEN** the drone holds 6° nose down at 10 m/s and 12° at 15 m/s in still air with the whole airframe, with the wake and with a known-wrong model without the interaction
- **THEN** with it, the rear pair's thrust at its own speed is 2–20 % under the front pair's (the published order on other frames), the rear rotors spin faster to hold the pitch, and the front pair gains from the upwash; without it the two pairs are equal and the band fails

#### Scenario: Hover and slow flight keep their thrust
- **WHEN** the drone hovers in still air, and flies 3 m/s at 1° nose down, with the wake
- **THEN** the hover takes no added inflow (within 1e-3 m/s) and its rotors' speeds are those without the wake within 0.1 %, and the slow flight's rear pair is under 2 % from the front's

### Requirement: Settling into the own wash
Vortex ring state SHALL set in from the descent rate normalised by the induced velocity at the current thrust, with hysteresis, costing thrust and adding a wandering moment, its entry and exit logged.

#### Scenario: Heavy on low throttle
- **WHEN** the 6.2 kg loadout descends vertically at 3 m/s with the throttle held low, and then the same descent at higher throttle
- **THEN** the first enters the state and logs its entry, loses thrust and wobbles; the second does not enter it; and exit needs a lower normalised descent rate than entry

### Requirement: The throttle-punch shake arises from its causes
After a fast descent is arrested by a sharp throttle increase, a damped high-frequency disturbance SHALL arise that lasts about as long as the arrest (0.3–0.7 s for most of the empty drone's sudden drops, from 9 m/s; shorter, and often under the visible line, from a gentle 2.5–5 m/s descent: the pilot's "doesn't happen much"; D-049, D-053, D-066), seen mainly as camera shake: visibly the rigid body's, jittering and tilting uncommanded while its rotors re-ingest their own wash through the arrest (PHY-09's ring), on the class's ESC, which senses no current (D-066; with 40 A current protection, D-051's alternative, an ESC holds its limit through the arrest, which takes longer, and the drone tilts further), with the frame's modes adding the camera's vibration, the arms' bending (48–63 Hz) the 50–200 Hz band and the loads' strapped mounts (28–36 Hz) the band below it; it depends on descent rate, abruptness and payload, and does not follow every throttle increase. A claim the physics does not keep on every flight is stated as a rate (D-053): "nearly every" is at least 95 % of a cell's 40 seeds, "most" at least 70 %.

#### Scenario: Shake only when its causes meet
- **WHEN** arrested descents run from the hover and from 2.5, 5.4 and 9 m/s down, with a sharp full step, a sharp 0.8 step and a gentle ramp, at 0 and 2 kg, on 40 seeds a cell
- **THEN** from the hover a gentle throttle increase never shows the shake and a sharp one only the motors' spin-up kick, shorter than the pilot's shortest shake (0.3 s); every sharp arrest of a descent rings the frame's band; every full punch from 9 m/s shows the shake, and from slower descents some do (a weak arrest stays under the visible line); in a 5–9 m/s descent a gentle ramp shows PHY-09's wobble instead, its rotors in their ring for over a second; and the shake varies between occurrences

#### Scenario: The shake lasts as long as the arrest
- **WHEN** a sharp full step arrests a descent, on 40 seeds a cell
- **THEN** every one is arrested and its shake never outlasts the arrest by more than the loop's settling; its length follows the descent (the medians on seeds 1–40: 0.04, 0.32 and 0.60 s empty from 2.5, 5.3 and 9 m/s, 0.00, 0.06 and 0.36 s at 2 kg); most of the empty drone's full punches from 9 m/s shake for 0.3–0.7 s, and the median does

#### Scenario: The visible shake is the rigid body's
- **WHEN** the same full punches fly with the airframe's modes, with 40 A current protection, with known-wrong rotors that never re-ingest their wash (no ring) and with a known-wrong rigid airframe
- **THEN** without the re-ingested wash most full punches shake less, and the shake lasts past 0.3 s (the pilot's shortest shake, longer than the motors' spin-up kick from the hover) on at most half as many, so the wash is its main cause; with 40 A current protection an ESC holds its limit through every arrest, which takes longer, and the drone tilts further on every flight; the rigid airframe's visible shake is within 20 % of the flexing one's and as long (the same median, within three fields on nearly every flight); the arms' modes ring the 50–200 Hz band and the modes publish `CameraVibration`, which the rigid airframe does not

### Requirement: The battery's margin and the inertia character
The heavy corner SHALL lose its thrust margin as the pack sags, a low-voltage signal at the pilot's FC profile's 3.00 V a cell on the loaded voltage SHALL reach the OSD, and the inertia ratios SHALL be the ones computed from `drone.json`'s layout, with yaw saturating first (D-048).

#### Scenario: The heavy corner at low charge
- **WHEN** the 6.5 kg loadout flies full throttle from a full pack down through 20 % charge to the cut-off
- **THEN** its thrust over weight falls with the sag the pack model computes, below the margin its full pack had; the low-voltage signal sets at the pack's knee, below 20 %; and no margin is left at the cut-off

#### Scenario: The inertia character on the seven loadouts
- **WHEN** the pitch, roll and yaw inertias are built from `drone.json` on each of the seven loadouts
- **THEN** their ratios equal the ones computed from its layout exactly, and yaw saturates first on every loadout
