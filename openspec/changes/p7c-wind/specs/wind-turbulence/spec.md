# Spec Delta

## Purpose

The wind the drone flies through: a seeded field of mean profile, turbulence, wakes and the canopy flow of wooded strips, drawn per flight, graded against clip P's measured targets by a simulated pilot, with the airframe's drag calibrated to the pilot's cruise.

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

### Requirement: A map's wind is its own obstacles, its belt counted once
A flight over a map SHALL carry the wakes of the map's placed wind volumes and the canopy flow of its tree belt, derived from the world and never drawn, with the belt read once: its canopy inside and over it, its porous wake on its lee, never its plants as thin obstacles.

#### Scenario: Behind a house and inside the belt
- **WHEN** the field flies Severe over the test range with the map's obstacles, and again without them (the known-wrong)
- **THEN** behind H01_D the mean reverses and reattaches near Fackrell's X_R, the far wake is Perera's at the house's height and porosity, and the turbulence is boosted; inside the belt σ/u* at the canopy's top is the canopy's and the mean profile its exponential; behind the belt the wake is porous; and the field without the map's obstacles fails each

#### Scenario: Along the belt
- **WHEN** the simulated pilot flies Severe along the test range's belt over its crowns on ten seeds, with the map's obstacles and without them
- **THEN** the yaw residual over the belt and the share of roll events there are higher with them, and without them the belt stretch reads as the field

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

### Requirement: The wind inside a cut is a cavity flow
Below a cut's lip, a flight in a pilot's level SHALL fly the cut's own flow, never the open profile restarted at its floor. The flow SHALL be driven by the field's mean above the lip, split into the components across and along the cut, and SHALL take one of two forms by the cut's length over its depth along each component: a recirculating cell, or a bubble that reattaches on the floor. The field's turbulence SHALL be reduced inside. Above the opening, the field SHALL take the lip as its ground. The cuts SHALL be derived from the world's hole records, never drawn.

#### Scenario: Across a deep cut
- **WHEN** the map's Severe wind blows over the test range on seeds 1–3 for 120 s, read at 0.5 m and 1.0 m over E01's run floor and at the same heights over the plane at QA's spots (`review-integration.md` S5), and again in a steady preset (σ 0) blowing across a straight cut of E01's section (0.8 × 1.5 m)
- **THEN** the run's mean horizontal speed is at most 0.25 of the plane's at the same height over the local ground. Across the straight cut, the flow 0.1 m below the lip runs with the wind and the flow from 0.5 m below the lip to the floor runs against it, up the upwind wall and down the downwind wall (one vortex). Model 26's open profile from the floor (0.47–1.20 of the plane, downwind at every depth) fails each

#### Scenario: A cut's regimes by its length over its depth
- **WHEN** a steady preset (σ 0, 9.5 m/s at 35 m) blows across straight cuts 1.0 m deep and 0.5, 1, 2, 4, 6, 8, 10 and 20 m wide, read 0.4 m over the floor along the wind
- **THEN** up to 4 m wide the floor flow is reversed across the whole cut (one cell). At 6, 8, 10 and 20 m it turns forward at 5.2, 5.8, 6.8 and 7.0 depths from the upwind wall, ± 0.1 depth (Hotta et al. 1998's 5.2, 5.8 and 6.8, and the step's 7.2). The open profile from the floor (forward everywhere) and a still hole (model 19: no flow) each fail

#### Scenario: Oblique wind and a cut's ends
- **WHEN** a steady preset (σ 0) blows at 90°, 60°, 30°, 10° and 2° to the axis of a straight 24 m cut of E01's section with closed ends
- **THEN** the cross cell scales with the sine of the angle and the along flow with its cosine, within 1e-9 relative. The along flow is at most 0.1 of the plane's at 1.0 m over the floor and falls to 0 at the end walls. One 2-D cavity on the wind's line (length W/sin θ) reads at least 0.4 of the plane at 1.0 m over the floor at 2°, and fails

#### Scenario: Over the opening and at its edge
- **WHEN** the drone is carried across E01's run at 0.5 m over the plane and 1 m/s in the map's Calm wind (σ 0), and held 0.5 m over the run's opening
- **THEN** over the opening the field at every point above the lip is, bit for bit, the field of the same setup with the lip passed as the ground. No hub's mean changes by more than 0.01 m/s in one step at the opening's edge. Model 20's profile from the floor fails each: QA saw 0.2 → 0.6 m/s in one step, and it reads the field over the opening 1.5 m higher

#### Scenario: The cavity's bits and cost
- **WHEN** two flights with the same seed fly the map's Severe wind through E01's run (held in it 60 s, then climbing out), and `flightbench --bench`'s map-step holds the drone in the run on AC with the power mode printed
- **THEN** the wind at every hub is bit-identical between the two flights, and replay reproduces every step and event bit for bit. The step allocates 0 B and its p99 stays at or under 300 µs. A flight that never enters a cut's opening steps as at model 26
