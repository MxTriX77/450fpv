# Spec Delta

## Purpose

The airframe effects a real heavy quad shows and a clean plant does not: motor asymmetry, ground and ceiling effect, recirculation, vortex ring state, the throttle-punch shake, and the battery's and inertia's character, each physical, bounded and logged.

## ADDED Requirements

### Requirement: Motor asymmetry within component tolerances
Each flight SHALL draw each motor's Kv spread, ESC timing jitter and prop imbalance once, from named seeded streams within labelled component tolerances, logged, and the drone SHALL drift and need correction at a rate inside a band computed from those tolerances.

#### Scenario: Asymmetry on and off
- **WHEN** the drone hovers on flat ground in still air for 30 s on five seeds, with asymmetry on and then off
- **THEN** with it on, the correction rate needed lies inside the computed band on every seed and never buzzes or diverges; with it off, the drift is at least halved

### Requirement: Ground effect, ceiling effect and recirculation per rotor
Each rotor SHALL gain thrust near the ground by Cheeseman–Bennett from its own ray and the ground's relief and mat top, none over a hole, the same under a roof for the ceiling effect, and a close hover SHALL never be still.

#### Scenario: The cushion and the hole
- **WHEN** one rotor hovers 0.3 rotor diameters above the floor and another over a hole band
- **THEN** the first rotor's thrust at fixed speed rises by Cheeseman–Bennett's ratio within 2 %, and the second's equals its free-air thrust

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
The heavy corner SHALL lose its thrust margin as the pack sags, a low-voltage signal SHALL reach the OSD, and the inertia SHALL keep pitch above roll and yaw at 1.0–1.3 × roll with yaw saturating first.

#### Scenario: The heavy corner at low charge
- **WHEN** the 6.5 kg loadout flies full throttle at 20 % charge
- **THEN** its thrust over weight falls below the margin its full pack had by the sag the pack model computes, and the low-voltage signal is set
