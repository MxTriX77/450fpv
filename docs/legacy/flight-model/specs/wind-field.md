# Spec Delta

## Purpose

The air the drone flies through: a mean profile that thickens near the ground, seeded broadband turbulence, and the wakes behind tree belts and buildings where the pilot says the drone actually yanks (manifesto §2.3). The pilot picks Calm, Windy or Severe (D-011) and nothing inside a level is an option; each flight then draws its own wind from its seed, so no two flights meet the same air.

This capability is graded against a suite that already exists. `docs/reference-notes/wind.md` §6 measures 20 targets from the pilot's own footage, defines a scripted simulated pilot, a test route and a measurement script. The requirements below adopt it rather than invent new criteria.

## ADDED Requirements

### Requirement: Mean wind profile
The wind SHALL have a log-law mean profile whose roughness length and displacement height are derived from the surfaces and cover beneath the drone (`surfaces-review.md` §6.5): roughly z0 ≈ 0.1 × cover height and d ≈ 0.67 × cover height, and over bare ground z0 derived from the surface's relief amplitude.
The profile SHALL be sampled at the rotors, the body and the tether nodes separately, not once for the airframe.

#### Scenario: Wind falls off toward the ground
- **WHEN** the mean wind is sampled on a vertical line from 0.2 m to 50 m over meadow_sod
- **THEN** it increases monotonically with height, follows the log law within 5 %, and is materially lower at 0.5 m than at 20 m

#### Scenario: Rougher cover slows the low wind
- **WHEN** the mean wind at 1 m is compared over dry_crust and over tall standing cover, at the same reference wind
- **THEN** the wind over the taller cover is lower, consistent with the derived roughness lengths

### Requirement: Seeded turbulence with energy at the frequencies that matter
Turbulence SHALL be generated as a seeded broadband field of Dryden form, with intensity σ/U and length scales taken from `wind.md` §5.4 (σ_v/U ≈ 0.12–0.15, L_u = L_v ≈ 115–200 m, L_w ≈ the height). Energy SHALL reach the airframe at encounter frequencies up to **2–4 Hz**, not only as slow gusts.
Turbulence SHALL be a spatial field sampled at the airframe's points, so the four rotors and the body can see different air at the same instant.

#### Scenario: The spectrum reaches the required band
- **WHEN** a Severe run is flown and measured by `wind_stats.py`
- **THEN** the roll spectrum meets T6a (≥ 85 % of 0.23–15 Hz variance below 1 Hz) and T6b (2–4 Hz RMS 0.06–0.16°), and the pitch spectrum meets T6c (20–45 % of variance in 1–4 Hz, 1–2 Hz RMS 0.11–0.25°)

#### Scenario: The field is spatial
- **WHEN** the turbulent wind is sampled at the four rotor positions of a hovering drone over 10 s
- **THEN** the four time series are not identical, and their differences are consistent with the field's length scales rather than being independent noise

#### Scenario: High-frequency shake stays under its ceiling
- **WHEN** a Severe or Windy run is measured
- **THEN** T6d holds: RMS above 4 Hz is ≤ 0.13° in roll and ≤ 0.06° in pitch

### Requirement: Obstacle wakes
Wakes SHALL be computed from the existing `world-query` wind grid (`TopM`, `BaseM`, path-composable `Porosity`) by marching upwind per cell, as `surfaces-review.md` §6.5 sets out: nearest obstacle distance, top, base and path porosity; deficit and turbulence boost as functions of x/H, z/H and β; porous-fence behaviour for β > 0.3; bluff-body recirculation with reverse flow for β < 0.1; speed-up of about 1.2 H over the top; a jet beneath crowns whose base is above ground; attenuation inside a canopy; and jets through the openings `GapsNear` returns.
The precompute SHALL run on a worker thread when the wind direction changes by more than 5° or the window moves, and SHALL NOT run inside a flight step.

#### Scenario: A tree belt multiplies the wobble
- **WHEN** the §6.2 test route is flown at Severe, with 32 s over open field and 7 s from the belt's upwind edge to about 10 belt heights downwind
- **THEN** T3 and T4 hold: residual roll std over the belt is 0.85–1.70° and at least 1.8 × the open-field value, and residual pitch std is 0.29–0.58° and at least 1.2 × the open-field value

#### Scenario: Bursts happen where the obstacles are
- **WHEN** the same runs are measured
- **THEN** T7 holds: 7–24 gust events per minute over the route's mix, with at least 70 % of roll events falling inside the belt segment

#### Scenario: Gusts arrive irregularly
- **WHEN** the onset gaps of all nominal runs are pooled
- **THEN** T8 holds: median gap 1–4 s with a coefficient of variation of 0.5–1.2, and no periodicity

#### Scenario: A solid building recirculates
- **WHEN** the wind field is sampled in the lee of a solid house (porosity ≤ 0.1) within 2–3 building heights
- **THEN** reverse flow is present below the building's height, and the deficit recovers with downwind distance

#### Scenario: A gap makes a jet
- **WHEN** the wind field is sampled in the opening of the gate asset with the wind normal to it
- **THEN** the wind speed in the opening exceeds the speed in the wall's lee beside it

#### Scenario: The precompute stays out of the step
- **WHEN** a flight turns through a wind-direction change of more than 5° while the step timing is measured
- **THEN** no step exceeds the flight-dynamics step budget, and the precompute appears on a worker thread

### Requirement: Weather is a fixed pilot setting with a per-flight draw
The pilot SHALL choose **Calm**, **Windy** or **Severe**, and the level SHALL never be chosen at random (D-011). Within a level, the preset SHALL fix the strength band, σ/U, the length scales, the wake parameters and the direction-change parameters.
Each flight SHALL draw, once, from its flight seed and write to the log header: a **prevailing direction** uniform over the compass and a **mean strength** uniform within the level's band (centre ± 25 %). During the flight the direction SHALL meander and shift at random about the drawn prevailing direction, and SHALL never be a fixed vector.
Level centres SHALL be Severe tuned to T0, Windy at 0.5 × Severe's centre and Calm at 0.15 × it (`wind.md` §6.4), and the levels' bands SHALL NOT overlap.

#### Scenario: The wind is never a fixed vector
- **WHEN** the preset's own wind history is logged for at least 40 seeds of 120 min each at 1 Hz or faster, with no drone, and pooled
- **THEN** T0c holds in full: the seeds' drawn directions have a resultant ≤ 0.7; the mean offset from each seed's drawn direction is within ±15°; the RMS deviation from it averages 20–60° and is at least 10° in every seed; shifts of ≥ 45° within 30 s occur 0.1–0.4 per minute; the gap coefficient of variation is ≥ 0.5 and the periodicity peak ≤ 2.4; and the wind stays within 90° of the drawn direction throughout 75–98 % of 40 s windows

#### Scenario: Two flights at the same level differ
- **WHEN** two flights are flown at the same level with different flight seeds
- **THEN** their drawn prevailing directions and mean strengths differ, both appear in each log header, and each flight replays exactly from its own header

#### Scenario: The lean matches the level
- **WHEN** the §6.2 runs are flown at each level
- **THEN** T0 holds per level: mean lean into the crosswind 9 ± 3° at Severe, 4.6 ± 1.5° at Windy and ≤ 2° at Calm

#### Scenario: The levels do not overlap
- **WHEN** the strength bands of the three levels are compared
- **THEN** each spans 0.75–1.25 × its own centre and no two bands intersect

### Requirement: The wind acceptance suite
The wind model SHALL be graded by the procedure of `wind.md` §6.2 exactly: the §6.1 simulated pilot with its stated delay, time constants and split; mass 4.8 kg with the §5.5 inertia; the straight route with the drawn wind abeam from the left; heights spread over 20–50 m; at least 5 runs on independent seeds with the first 5 s discarded; logging in the `attitude.csv` columns at 29.917 Hz with the §6.2 angle definitions; and measurement by `tools/reference/wind_stats.py`.
A level SHALL pass when every target's average over the nominal runs lies inside its band, with T8 pooling gaps across runs, T0c pooling seeds and T11b using the variant runs. **Calm SHALL be flown with the airframe's own asymmetry disabled**, so its ceilings measure the wind alone (`wind.md` §6.4).

#### Scenario: Severe passes its targets
- **WHEN** the §6.2 procedure runs at Severe
- **THEN** every target T0–T12 of `wind.md` §6.3 lies inside its band, and the run JSONs and the pass or fail per target are reported

#### Scenario: Windy and Calm pass their columns
- **WHEN** the same procedure runs at Windy and at Calm with their own presets, Calm with asymmetry disabled
- **THEN** every applicable target of the `wind.md` §6.4 table lies inside its band or under its ceiling

#### Scenario: Gusts force the horizon-heading trade-off
- **WHEN** the variant runs of `wind.md` §6.1 are flown over the belt
- **THEN** T11a and T11b hold: the thrust-tilt residual is 1.1–2.3° over the belt and 0.39–0.81° over the field with the belt at least 1.8 × the field; the s = 1 and s = 0 ratios are each ≥ 0.8; and the tilt stays within ±25 % of the nominal run's value in both variants

#### Scenario: The wobble never goes smooth over open ground
- **WHEN** the open-field segments of the nominal runs are measured
- **THEN** T12 holds: the coefficient of variation of the 5 s block residual standard deviations is ≤ 0.32 and the smallest block is ≥ 0.50 of the segment's standard deviation, in both roll and pitch; and T9 holds, with open-field roll kurtosis 2.2–3.5 and pitch kurtosis 3.3–5.5

### Requirement: Calm is never perfectly still
With the weather level set to Calm and the airframe's own asymmetry **enabled**, the residual roll and pitch wobble SHALL stay above a stated floor, because the airframe's own motor asymmetry, ESC jitter and prop wash disturb it even in light air. `wind.md` §6.4 assigns this floor to this change.

#### Scenario: Calm has a floor
- **WHEN** the §6.2 procedure runs at Calm with the asymmetry enabled, on at least 5 seeds
- **THEN** the residual roll and pitch standard deviations are above the stated floor on every seed, and the same runs with asymmetry disabled fall below it, which shows the floor comes from the airframe

#### Scenario: The floor is not a noise term
- **WHEN** the source of the Calm floor is traced in a log
- **THEN** it resolves to the logged per-motor perturbations and the prop-wash term, and no code path adds a disturbance to the pose or the rates to create it
