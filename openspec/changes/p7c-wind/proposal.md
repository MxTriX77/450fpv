# Proposal: p7c-wind

**Owner:** physics-engineer · **Also building:** world-builder (the wake grid, task 1.3) · **Reviewers:** qa-engineer (the statistical harness and the known-wrong models); **the pilot at G7**
**Requirements:** PHY-13, PHY-13a, PHY-14, PHY-15, PHY-16, PHY-17, PHY-17a, PLT-04, TST-03 · **Decisions:** D-011 (levels), D-021 (rain), D-024 (computed, never asked), D-037 (the stats tools ported), D-042 (wind is part of G7), D-044 (the test range) · **VISION:** §2 point 3 ("turbulence ... through narrow holes or among dense woods in a severe wind")
**Source:** `docs/knowledge.md` §6.5, §6.7 and §8 (clip P's measurements, the model, the simulated pilot, the K§8.6 targets); `p6-flight-core` 1.5's stand-in wind and its as-built note; `godot-final`'s `wind_stats.py`, `wind_history_fixture.py` and `rain_stats.py` (to port).

## Why
The pilot's strongest physics point is the wind: "the worst part" is the tree belt, where "either you yaw but lose direction OR you roll and keep direction, but lose horizon". The flight core flies a labelled stand-in (a mean wind and Gauss–Markov turbulence). G7 is meant to carry the real field (D-042): wakes behind belts and buildings, gusts as real sideways forces with per-rotor gradients, a wind that meanders and shifts, graded against clip P's measured targets by a simulated pilot.

## What Changes
- **The statistics tools, ported first** (D-037): `wind_stats.py`, `wind_history_fixture.py` and `rain_stats.py` as plain Python under `tools/wind/`, each with its self-checks (stationary Gaussian noise and scheduled shifts must fail, TST-03).
- **The acceptance harness:** K§8.5's simulated pilot and test flight in full (the 29.917 Hz camera, the 0.10 s delay, the tilt split, its variants), flown over the test range's field and posadka.
- **The wake grid** (world-builder): per wind direction on the 2 m grid, each obstacle's height, base and porosity from the placed assets' wind volumes, with the deficit, the turbulence boost, recirculation, the speed-up and jets through gaps, precomputed on a worker.
- **The field** (PHY-13, 13a, 15): the log-law mean profile, von Kármán turbulence reaching 2–4 Hz, the wakes, vorticity and per-rotor gust gradients, the per-flight direction and strength with meander and rare shifts (K§8.3), and the cavity under a trench lip. It replaces the stand-in.
- **The levels** (PHY-14): Severe tuned until T0 passes, Windy and Calm from it, every K§8.6 target graded on at least five seeds, T0c on 40 seeds × 120 min, and each target shown to fail a known-wrong model.
- **Cruise in wind** (PHY-17a): the airframe drag re-derived so clip P's 22.7° of pitch at about 4.8 kg gives the published 17–22 m/s cruise. This supersedes `p6-flight-core` 1.5's 13 m/s calibration. The horizon–heading trade-off (PHY-16, T10b, T11b) follows from it.
- **The map's wind** (D-059, D-060): a flight over a map carries the map's own obstacles (`WindSetup.ForMap`), its placed wind volumes as the wake grid's obstacles and its tree belt read once, as wooded strips with one porous wake each (design 9).
- **Rain** (PHY-17), the part that is not the fiber's or the ground's: the rain-rate signal, the rain's drag and wet mass on the body, and the air velocity at the camera for the lens drops (FEED-13).

## Capabilities

### New Capabilities
- `wind-turbulence`: the field's layers, the per-flight draws, the levels and their graded targets, the cruise calibration, the rain signals, and the harness that measures them

### Modified Capabilities
- None. (`p6-flight-core`'s `wind-field` stand-in is still in flight; this change's archive follows its and retires the stand-in requirement then.)

## Non-goals
- **The fiber in the wind** (7d): wet fiber mass and drag, tension from the wind.
- **Wet soil and cover** (7a).
- **Thermals over dark fields** (deferred X-12) unless 7c's design finds them needed for a target.
- **The feed's own wind effects** (`p4-feed` 1.7, 7e).

## Impact
- **New:** `Runtime/Wind/` (the field), the wake grid in `Runtime/World/` and its precompute, `tools/wind/` (the ported tools), the harness in `tools/flightbench`.
- **Changed:** `Aero` and `drone.json`'s `aero` block (the drag re-derived), the log (the field's draws and per-rotor wind), the goldens (re-recorded, audited).
- **Depends on:** `p6-flight-core` (the core and the stand-in's seams), `p6-world-query-gallery` 1.4 (the queries over the test range) for the wake grid's obstacles, and `p6-test-range` (the field and the posadka to fly).
