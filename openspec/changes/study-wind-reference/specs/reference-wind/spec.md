# Spec Delta

## Purpose

Measures how a real heavy FPV drone was thrown around in severe wind, frame by frame, and turns it into quantitative targets that the sim's turbulence model must reproduce.

## ADDED Requirements

### Requirement: Frame-by-frame attitude tracking
The tracker SHALL estimate, for **every** decoded frame of a given clip:
- camera roll (deg) and pitch (deg) from the horizon line, after the measured lens distortion is removed (k1 from `video-feed.md`)
- yaw rate (deg/s) from horizontal image motion
- a confidence (0–1) for each value

Frames where the horizon isn't visible SHALL be marked low-confidence, never guessed. Output SHALL go only to `reference/_frames/<letter>/attitude.csv`, which is git-ignored.

#### Scenario: Synthetic accuracy
- **WHEN** the tracker runs on synthetic frames with known roll −30° to +30° and known pitch offsets, rendered through the same k1 distortion and with added analog-like noise
- **THEN** roll error has RMS ≤ 0.5° and pitch error RMS ≤ 0.7°, and every frame is reported

#### Scenario: Real-frame spot check
- **WHEN** 20 random high-confidence frames of the wind clip are checked by eye against the reported horizon, overlaid on the frame
- **THEN** the overlaid horizon lies on the visible horizon within 1° in every frame

#### Scenario: No guessing
- **WHEN** a frame shows no horizon (ground only, sky only, or signal loss)
- **THEN** its confidence is below 0.2 and the statistics exclude it

### Requirement: Wind disturbance measurements
`docs/reference-notes/wind.md` SHALL report, from high-confidence frames of the wind clip, for roll, pitch and yaw rate separately:
- the distribution: mean, standard deviation, 95th percentile and maximum
- angular rate statistics
- a power spectrum with its dominant frequency bands
- gust events (excursions beyond a stated threshold): count per minute and duration distribution
- the cross-axis correlation, and the time between successive large disturbances

Every number SHALL state its unit, the frames it came from, and its uncertainty.

#### Scenario: Complete measurement set
- **WHEN** QA checks `wind.md`
- **THEN** every listed statistic is present for all three axes, each with unit, frame range and uncertainty, and reproducible from `attitude.csv` with the documented method

### Requirement: Physical interpretation and limits
The notes SHALL explain:
- what the measurements imply about turbulence intensity and scale near ground and obstacles
- the motor thrust margin the corrections needed, including signs of saturation
- the **closed-loop limit**: the pilot was correcting constantly, so the measured attitude is the residual after correction, and the true disturbance is larger

Every inference SHALL be marked as measured, derived or assumed.

#### Scenario: Limits stated
- **WHEN** any interpretive statement in `wind.md` is read
- **THEN** it is labelled measured, derived or assumed, and the closed-loop limit is stated before the targets

### Requirement: Targets for the wind model
The notes SHALL end with quantitative acceptance targets for each of the pilot's weather levels (D-011: **Calm / Windy / Severe**, always chosen by the pilot). **Severe** is measured from the clip. **Windy** and **Calm** are derived bands, labelled as such. The targets SHALL include a check on the preset's own wind history (a prevailing direction with random shifts, never a fixed vector), and a check that the wobble never goes smooth over open ground. For **Severe and Windy**: residual roll and pitch standard deviations, spectral band, gust-event rate and cross-axis correlation, each with a tolerance, measured with a simulated pilot of stated correction bandwidth. **Calm** is defined by ceilings only, plus the wind-history check. Its "never perfectly still" floor comes from the airframe (motor asymmetry), and belongs to the flight-model change's acceptance. Calm's ceilings apply to the wind-driven part, measured with airframe asymmetry off. The targets SHALL be phrased so a physics test can check them automatically.

#### Scenario: Testable targets
- **WHEN** the physics engineer reads the targets
- **THEN** each target is a number with a unit, a tolerance and a measurement procedure

### Requirement: Rain on the feed
The notes SHALL describe the rain effects visible in the wind clip: drops and streaks on the lens, contrast and colour loss, blur, flare, and clearing by prop wash or airspeed. Each SHALL be mapped to a candidate effect and a simulation driver (rain rate, airspeed, attitude, throttle, or free-running random with its measured rate).

#### Scenario: Rain traits mapped
- **WHEN** any rain trait is read
- **THEN** it cites the clip letter and frames, and names an effect and a driver

### Requirement: OPSEC
The wind notes SHALL follow the same rules as the other reference notes: clips by letter only, and no place names, coordinates, OSD values, dates, file names, unit information, identifiable landmarks, positions or people.

#### Scenario: Hygiene scan
- **WHEN** QA scans `wind.md` and all commit messages
- **THEN** there are no hits in any banned category
