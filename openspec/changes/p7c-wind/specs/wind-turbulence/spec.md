# Spec Delta

## Purpose

The wind the drone flies through: a seeded field of mean profile, turbulence and wakes, drawn per flight, graded against clip P's measured targets by a simulated pilot, with the airframe's drag calibrated to the pilot's cruise.

## ADDED Requirements

### Requirement: The statistics tools are ported and self-checked
The first attempt's `wind_stats.py`, `wind_history_fixture.py` and `rain_stats.py` SHALL run as plain Python under `tools/wind/`, and each SHALL reject its known-wrong inputs before it grades the model.

#### Scenario: The tools reject noise and schedules
- **WHEN** the ported tools are given stationary Gaussian noise with clip P's spectra, and a wind history with shifts on a fixed schedule
- **THEN** the noise fails T9 (stationary noise on T9b, noise alternating calm and busy on T9a) and, when it alternates calm and busy, T12; the schedule fails T0c; and the starting direction model of K§8.3 passes T0c in every one of 40 seed sets

### Requirement: The levels meet the targets under the simulated pilot
Severe, Windy and Calm SHALL meet the K§8.6 target table when K§8.5's simulated pilot flies the test flight over the test range on at least five seeds, measured by the ported tools, with Calm flown with the airframe's asymmetry off.

#### Scenario: Severe meets every target
- **WHEN** the harness flies Severe on five seeds
- **THEN** T0 through T12 each fall in Severe's column, and each also fails when its known-wrong model replaces the field

#### Scenario: The direction history
- **WHEN** 40 seeds × 120 min of wind history are drawn at 1 Hz or faster
- **THEN** T0c holds: draws differ (resultant ≤ 0.7), centred within ±15°, RMS deviation 20–60° with at least 10° in every seed, 0.1–0.4 shifts of 45° or more per minute, gap CV at least 0.5, periodicity peak at most 2.4

### Requirement: Gusts are real forces with per-rotor gradients
The field SHALL act through drag on the body, the coil and each rotor, sampled at each rotor separately, with vorticity in wakes, so that a gust front crossing the drone produces coupled pitch-and-yaw events and wind-alone yaw swerves.

#### Scenario: The trade-off cannot be dodged
- **WHEN** the variant pilots with split s = 1 and s = 0 fly Severe over the posadka
- **THEN** T11b holds: both ratios at least 0.8, the tilt within ±25 % of nominal, and roll and yaw rate correlate +0.55 to +0.90 over the belt, more than over the field

### Requirement: The wind per flight is drawn, never fixed
Each flight SHALL draw its prevailing direction (uniform) and strength (uniform in the level's band) from its seed, with meander and rare shifts, all logged, so the history is computable from the seed alone.

#### Scenario: Same seed, same wind
- **WHEN** two flights run with the same seed and level
- **THEN** their wind at every rotor is bit-identical at every step, and a different seed gives a different prevailing direction

### Requirement: Cruise at the published speed
The airframe's drag SHALL be calibrated so that clip P's 22.7° of pitch at about 4.8 kg gives the published cruise of 17–22 m/s, every drag number labelled.

#### Scenario: Clip P's pitch flies the cruise
- **WHEN** the drone holds 22.7° of pitch in still air at the nominal loadout
- **THEN** it settles between 17 and 22 m/s, and T0's mean lean into a Severe crosswind holds at that airspeed

### Requirement: Rain is a level of its own
Rain SHALL be chosen independently of the wind level, add its drag and wet mass to the body, and publish its rate and the air velocity at the camera for the feed.

#### Scenario: Rain without wind
- **WHEN** a flight runs Calm with heavy rain
- **THEN** the rain-rate signal and the air velocity at the camera are live and logged, the wind stays Calm's, and the rain's drag matches its labelled formula within 1 %

### Requirement: The field stays within the budget
The field's sampling SHALL stay within PLT-04's share of the step, with the wake grid's precompute off the flight thread.

#### Scenario: The cost of the wind
- **WHEN** `flightbench --bench` runs Severe over the posadka on AC with the power mode printed
- **THEN** the step's p99 stays at or under 300 µs with 0 B allocated, and a direction change of 10° recomputes the wake grid without a hitch on the flight thread
