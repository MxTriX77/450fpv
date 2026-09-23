# Wind reference notes

The pilot marked one clip as the target for how flight in severe wind must feel: "constant and dynamic physics along with motors crying", never "wind goes south so my quad goes south smoothly". These notes measure, frame by frame, how the airframe was thrown around in that flight, and turn the numbers into targets for the sim's severe-wind preset.

- **Source:** clip **P** only, cited by frame (`P f123`, 1-based, 29.917 frames per second).
- **Evidence:** `reference/_frames/P/attitude.csv` (git-ignored), written by `tools/reference/track_attitude.py` for every frame, and every number in §1–§5 printed by `tools/reference/wind_stats.py` from that file. Nothing derived from the footage is in the repo.
- **Reproduce** (from the repo root, Blender 5.2):
  ```
  blender -b --factory-startup --python tools/reference/track_attitude.py -- --letter P [--overlays 20]
  blender -b --factory-startup --python tools/reference/track_attitude.py -- --selftest <scratch folder>
  blender -b --factory-startup --python tools/reference/wind_stats.py -- --letter P --segment 121-330 [--json <file>]
  ```
- **Labels:** every statement in §5 is **measured** (read straight from `attitude.csv`), **derived** (computed from measured numbers with a stated model) or **assumed** (a value or model taken from outside the clip). The pilot's answers (§5.7) come from outside the clip too; they are marked **assumed (pilot, Qn)**.
- **Uncertainty:** `[a, b]` is the 16–84 % interval of 500 block-bootstrap resamples (2 s blocks, seed 0) unless stated otherwise.
- **OPSEC:** clip letter only. No places, landmarks, dates, file names, people or values read from the on-screen display (OSD).
- **Owners:** physics-engineer (§1–§6), tech-artist (§7).

## 1. Method and coverage

### 1.1 The clip

- 1417 frames, 47.4 s, flown in severe wind and heavy rain (the pilot's account). Straight forward flight high above open stubble fields with straw windrows, crossing one tree belt with scrub at about f121–f330 (the contact-sheet stills at 4–11 s show trees and bushes below the drone). Overcast sky.
- The camera looks well below the horizon the whole time, so the horizon runs through the top 5–40 % of the picture, where the lens bends it most.
- The picture drops out three times and comes back each time (blue no-signal screen at f676–f687, f969–f985 and f1145–f1164), plus one black frame at f955 after a burst of impulse noise (f951–f954). 14 other frames repeat their predecessor.
- Camera-fixed clutter: OSD text, a dotted OSD attitude line, two airframe parts in view at mid-height, the receiver's black top border, and propeller blades crossing the top corners.

### 1.2 What is measured

| Quantity | Definition | Depends on |
|---|---|---|
| Roll (deg) | Angle of the undistorted horizon line; positive when the camera is rolled right (the horizon's right end rises) | lens k1 only |
| Pitch (deg) | Elevation of the camera's optical axis: `atan(d / f)`, with `d` the distance of the undistorted horizon line below the frame centre. Negative = looking down | k1 and the focal length `f` |
| Yaw (deg, deg/s) | Heading change about the vertical, positive = turning right. The tracker gives the rate per frame; the statistics integrate it to a heading angle inside each run | k1 and `f` |

These are **camera** angles. The camera has 0° uptilt on the airframe (pilot, Q1), so they are also the airframe's roll, pitch and heading (§5.5). The sim computes the same camera quantities from its own camera pose, so the targets in §6 compare like with like.

Model and assumptions:
- **Lens:** `p_u = p_d · (1 + k1 · r_d²)`, coordinates normalised to the half-width, centre at the frame centre, with k1 = 0.33 from `video-feed.md` O1 (clip A's camera). A one-off development check (residual curvature of the fitted horizon over 142 frames of P, not part of the tools) found P's horizon straightest at k1 ≈ 0.35, inside O1's spread. The effect of k1 = 0.36 is in §1.5.
- **Focal length (assumed):** the undistorted image is treated as a pinhole image with `f = 1/√(3·k1)` = 1.005 half-widths. That is an equidistant (f-theta) lens matched to k1 at small angles. It puts the frame edge at 57° off-axis, a horizontal field of view of about 114°. Other fisheye families give `f` from 0.87 (stereographic) to 1.07 (equisolid), so **pitch and yaw carry a scale uncertainty of −6 % / +15 %.** Roll does not depend on `f`.
- **Skyline, not true horizon:** the line found is the skyline of distant fields and tree lines. From tens of metres up it sits up to about 1° below the true horizon, and nearer tree belts can tilt it by a similar amount. That bias changes only as slowly as the scenery does, so it shifts the means in §2 but hardly the residuals, rates or spectra.

### 1.3 How the tracker works (per frame)

1. Decode the frame through Blender's sequencer at 960 × 540 (area-averaged; the feed carries about 450 × 286 real samples, `video-feed.md` §5).
2. **Camera-fixed mask:** whatever stays sharp in the mean of 120 frames spread over the clip (OSD, airframe parts, border). It covers 14.2 % of the picture, and those pixels take no part in anything below.
3. **Horizon points:** a "sky-likeness" feature (luma minus |R − B|, so grey sky scores high and brown soil or yellow straw low). Per column, the strongest step from brighter-above (16 rows) to darker-below (40 rows). The long lower window keeps thin dark lines such as prop blades from winning.
4. **Undistort** those points with k1, then fit a straight line: RANSAC (400 tries, seeded by the frame number), then total least squares on the inliers.
5. **Confidence** (0–1) = inlier share × contrast term (median inlier step, 15→50 levels mapped to 0→1) × span term (horizontal extent of the inliers, 20→50 % of the width for roll, 10→30 % for pitch). A frame with no picture or no line gets confidence 0 and empty values.
6. **Jump gate:** a roll or pitch more than 15° from the median of its usable neighbours (up to 3 each side) keeps its value but gets 10 % of its confidence (flag `jump`; none occurred in P).
7. **Yaw:** the band from 5° below to 1.5° above the horizon is resampled onto an azimuth × elevation grid (±50°, 0.15° steps) in the camera's level frame, so a heading change becomes a pure sideways shift of distant scenery. Phase correlation with the previous distinct frame gives that shift. Confidence = peak height (0.03→0.12) × uniqueness (main peak over the next-best, 1.3→2.0, which rejects repeating texture) × valid-grid share × both frames' horizon confidence. Repeated frames get no yaw value; the next frame's rate spans both steps.
8. The CSV also keeps diagnostics per frame: columns found, inlier share, contrast, span, line residual, horizon edge width, peak height, uniqueness, and the elevation shift between consecutive strips.

### 1.4 Statistics (`wind_stats.py`)

- **Usable frame:** roll and pitch confidence ≥ 0.5 (yaw: yaw confidence ≥ 0.5), and not within 2 frames of a picture loss. Loss-onset frames can be torn: f1144 shows a new picture above a split line and the old one below it, yet it tracked with high confidence. A **run** is a stretch of consecutive usable frames. Nothing is interpolated across gaps. A single repeated frame keeps a yaw run going, because the recording shows the previous view there.
- **Residual** ("wobble"): the angle minus its Gaussian-weighted (σ = 0.6 s) local-linear trend inside the run. It passes half the amplitude at 0.31 Hz and more than 90 % above 0.6 Hz. It removes the steady lean and the slow course changes. Frames within 17 frames (one σ) of a run end are dropped.
- **Rate:** slope of a least-squares line through 5 frames. **Acceleration:** the matching 5-point quadratic fit. Both resolve motion up to about 4 Hz.
- **Spectra:** Welch, Hann windows of 128 frames (4.3 s, 0.23 Hz resolution), 50 % overlap, linear detrend per window, windows only inside runs. Band RMS values carry a bootstrap interval over windows. Nothing above the 15 Hz Nyquist limit is visible (§3.3).
- **Airframe** (valid for 0° uptilt, pilot Q1; fields under `airframe`):
  - the thrust that holds height, 1/(cos pitch · cos roll) weights
  - the horizontal thrust's sideways part and its direction
  - body rates and accelerations from the ZYX kinematics (products of rates, < 1 °/s², left out)
  - the **thrust-tilt residual** e = roll residual + k · heading residual, with k = −sin(mean pitch), and the share of its variance on the roll, cov(roll, e) / var(e) (§5.5)

  With `--segment`, the tilt and the roll–yaw correlations are also reported inside and outside the segment.
- **Events** (gusts as the recording shows them): runs of frames where an axis's residual exceeds 2 standard deviations of that axis's residual over the whole clip, merged across gaps of up to 3 frames. `roll_or_pitch` means either axis beyond its own threshold. For each event: duration above threshold, the rise and decay time between 1/e of the peak and the peak, and the gap to the next onset. Rates are per minute of residual time, with a Poisson error.

### 1.5 Verification

**Synthetic set** (`--selftest`, generated in the scratchpad):
- 120 frames rendered at 1920 × 1080 through the same lens model: grey sky with cloud texture, a tree line with trees sticking up, and a striped field with haze.
- Degraded like the feed: 288-line fields, horizontal softening and sharpening halos, chroma smear, grain, sparkles, vignette. Camera-fixed OSD blocks, two rods, and a prop blade in half the frames.
- Decoded through the same Blender path.
- Content: 96 frames with a horizon (roll −30…+30°, pitch −22…+8°, heading swinging ±40°, i.e. up to ±125 °/s), then 8 ground-only, 8 sky-only, 4 blue no-signal and 4 snow frames.

| Check (spec scenario) | Result | Limit |
|---|---|---|
| Frames reported | 120 of 120, all 96 horizon frames with values and confidence ≥ 0.5 | every frame |
| Roll error | RMS 0.050°, mean −0.015°, max 0.23° | RMS ≤ 0.5° |
| Pitch error | RMS 0.099°, mean −0.095°, max 0.15° | RMS ≤ 0.7° |
| Yaw-rate error (92 frames, conf ≥ 0.5) | RMS 0.42 °/s, max 1.35 °/s | (no spec limit) |
| Highest confidence without a horizon | ground 0.076, sky 0.050, blue 0.000, snow 0.000 | < 0.2 |
| Tracker k1 = 0.30 / 0.36 on frames made with 0.33 | roll RMS 0.072° / 0.069°, pitch RMS 0.110° / 0.169° | |

The synthetic set has no translation (no parallax), so it tests yaw only for pure rotation.

**Real-frame spot check** (`--overlays 20`, seed 1): 20 random frames with roll and pitch confidence ≥ 0.5, namely f38, 47, 116, 197, 338, 350, 371, 422, 558, 575, 637, 701, 760, 886, 1050, 1144, 1176, 1229, 1334 and 1336. Each overlay draws the fitted horizon and the lines 1° above and below it, and each was inspected at 1.5× zoom at the left, centre and right of the horizon.
- **In all 20, the visible skyline lies inside the ±1° band across the width.**
- Single trees stick up past the upper line, and the fit correctly ignores them. Where a nearer tree belt forms the skyline, the line runs between the crowns and the base.
- At the far left the line sits a few pixels above the dark edge, which is consistent with P's k1 being slightly above 0.33.
- f1144 passes too, but it is the torn loss-onset frame described in §1.4, so the statistics exclude it.

**Internal consistency (measured):** the vertical shift between consecutive yaw strips has a median of −0.002° (10–90 %: −0.045 to +0.036°, 1341 pairs). Frame-to-frame pitch from the horizon therefore agrees with image correlation to about 0.04°.

**Lens sensitivity on P (measured):** re-tracking P with k1 = 0.36 (`--k1 0.36`) changes:
- the roll and pitch residual std by < 1 % (0.707 → 0.702°, 0.311 → 0.308°)
- the means by −0.07° (roll) and −0.20° (pitch)
- the roll–yaw residual correlation from 0.73 to 0.66

The yaw residual std changes from 0.83° to 0.56°, because it rests on a few large bursts (kurtosis 9). **Yaw figures therefore carry a ±35 % lens uncertainty on top of the bootstrap.**

### 1.6 Coverage and frame sets

| Set | Frames | Share of 1417 | Used for |
|---|---|---|---|
| Tracker confidence ≥ 0.5 | roll and pitch 1362, yaw 1330 | 96.1 %, 93.9 % | spec coverage |
| No picture (flag `no_picture`) | 50 (f676–687, f955, f969–985, f1145–1164) | 3.5 % | excluded |
| **U**: usable roll/pitch | 1348, in runs f1–673, f690–913, f915–951, f958–966, f988–1142, f1167–1378, f1380–1417 | 95.1 % | §2 angles and rates |
| **Y**: usable yaw (single repeats held) | 1333, in 13 runs of ≥ 5 frames: f2–162, 165–348, 350–459, 461–481, 483–579, 581–673, 690–796, 805–913, 916–951, 959–966, 988–1142, 1167–1378, 1381–1417 | 94.1 % | §2 yaw |
| **R**: residual roll/pitch | 1135 (U minus 17 frames at each run end) | 80.1 % (37.9 s) | §2 residuals, §4 |
| **Ry**: residual yaw | 927 | 65.4 % (31.0 s) | §2, §4 |
| **W**: Welch windows, roll and pitch | 14 × 128 frames starting at f1, 65, 129, 193, 257, 321, 385, 449, 513, 690, 754, 988, 1167, 1231 | | §3 |
| **Wy**: Welch windows, yaw | 5 × 128 frames starting at f2, 165, 988, 1167, 1231 | | §3 |
| **B** (tree belt) / **F** (open field) | R inside f121–330 (210 frames, 7.0 s) / the rest of R (925 frames, 30.9 s) | | §4.4 |

Coverage is far above the 30 % threshold at which the design would call the results indicative. Rain did not stop the tracker.

### 1.7 Limits

- One flight of 47 s, closed loop (§5.1). Rates of rare events carry wide Poisson errors.
- The recorder samples 50 fields/s at about 30 frames/s with slightly irregular timing (`video-feed.md` §0). Rates are therefore taken over 5 frames, never frame to frame.
- There is no stick log and no telemetry. Pilot corrections and wind cannot be separated exactly; §5 bounds the difference.

## 2. Distributions and rates

All values are camera angles as defined in §1.2. The bracket after the mean or std is its [16–84 %] bootstrap interval.

| Axis · quantity | Frames | Mean | Std | p5 / p95 | p95 of \|x\| | Max \|x\| | Kurtosis |
|---|---|---|---|---|---|---|---|
| Roll angle (deg) | U 1348 | −9.11 [−9.59, −8.65] | 2.41 [2.11, 2.63] | −13.07 / −4.81 | 13.07 [12.3, 13.5] | 14.42 | 2.81 |
| Pitch angle (deg) | U 1348 | −22.71 [−23.0, −22.4] | 1.64 [1.26, 1.86] | −26.35 / −20.18 | 26.35 [25.1, 26.9] | 27.75 | 4.21 |
| Yaw rate as tracked, per frame (°/s) | Y 1321 | −0.10 [−0.32, 0.12] | 4.89 [4.16, 5.49] | −8.78 / +8.30 | 10.0 [9.2, 12.0] | 28.2 | 8.59 |
| **Roll residual (deg)** | R 1135 | −0.02 [−0.06, 0.02] | **0.71 [0.59, 0.79]** | −1.08 / +1.00 | 1.55 [1.09, 1.79] | 2.86 | 4.74 |
| **Pitch residual (deg)** | R 1135 | +0.01 [−0.01, 0.03] | **0.31 [0.29, 0.33]** | −0.54 / +0.48 | 0.62 [0.57, 0.68] | 1.18 | 3.81 |
| **Yaw (heading) residual (deg)** | Ry 927 | −0.01 [−0.06, 0.04] | **0.83 [0.54, 1.01]** | −1.15 / +1.03 | 1.93 [1.04, 2.83] | 3.71 | 9.18 |
| Roll rate (°/s) | U 1320 | −0.17 [−0.39, 0.08] | 3.70 [3.30, 4.02] | −5.87 / +6.06 | 8.37 [7.02, 8.87] | 18.15 | 5.10 |
| Pitch rate (°/s) | U 1320 | −0.01 [−0.16, 0.15] | 2.47 [2.24, 2.64] | −4.13 / +3.92 | 5.19 [4.56, 5.65] | 11.66 | 4.64 |
| Yaw rate, 5-frame (°/s) | Y 1278 | −0.22 [−0.46, 0.07] | 3.96 [3.24, 4.52] | −6.71 / +5.72 | 8.64 [6.92, 10.4] | 21.96 | 9.37 |
| Roll acceleration (°/s²) | U 1320 | −0.3 [−0.7, 0.3] | 76 [71, 81] | −121 / +120 | 164 [144, 177] | 386 | 5.41 |
| Pitch acceleration (°/s²) | U 1320 | −0.1 [−0.5, 0.3] | 45 [41, 47] | −70 / +74 | 97 [88, 102] | 208 | 4.79 |
| Yaw acceleration (°/s²) | Y 1278 | +0.0 [−0.5, 0.5] | 42 [38, 45] | −63 / +65 | 85 [76, 92] | 326 | 7.63 |

What stands out:
- **A steady left bank of 9.1°** (roll never rises above −1.8° in any frame, and 90 % of frames lie between −13.1 and −4.8°), with no net turn: the mean yaw rate is −0.10 °/s, about −5° of heading over the clip. Section 5.3 reads this as a crosswind from the left.
- **The camera looks 22.7° below the horizon** on average and swings between −27.8 and −19.1°.
- **The wobble is heavy-tailed:** residual kurtosis 4.7 (roll), 3.8 (pitch) and 9.2 (yaw) against 3.0 for Gaussian noise. The motion is calm most of the time, with bursts.
- **Roll and pitch rates are symmetric** (p5 and p95 within 5 % of each other); yaw rate leans slightly left (−6.7 against +5.7 °/s).

**Measurement noise** (derived from the flat spectral floor in §3): at most 0.11° per frame in roll, 0.04° in pitch and 0.06° in heading. That adds at most 1.0, 0.4 and 0.5 °/s to the rate std, which is negligible. It adds up to 53, 20 and 26 °/s² to the acceleration std, so noise-free acceleration std is about 54 (roll), 40 (pitch) and 33 °/s² (yaw), and the acceleration maxima are upper bounds.

## 3. Spectra

### 3.1 Band table

Welch spectra of the angles on sets W and Wy. The value is the band RMS in degrees with its bootstrap interval over windows, then the band's share of the 0.23–15 Hz variance. The PSD values themselves are ±27 % (roll, pitch, 14 windows) and ±45 % (yaw, 5 windows).

| Band (Hz) | Roll (deg) | Pitch (deg) | Yaw heading (deg) |
|---|---|---|---|
| 0.23–0.5 | 0.90 [0.80, 0.99] · 72 % | 0.27 [0.23, 0.31] · 50 % | 1.36 [0.40, 1.88] · 81 % |
| 0.5–1 | 0.50 [0.39, 0.58] · 22 % | 0.155 [0.143, 0.165] · 16 % | 0.59 [0.29, 0.79] · 15 % |
| 1–2 | 0.20 [0.16, 0.22] · 3 % | **0.179 [0.148, 0.204] · 22 %** | 0.27 [0.12, 0.36] · 3 % |
| 2–4 | 0.114 [0.103, 0.124] · 1 % | 0.121 [0.105, 0.136] · 10 % | 0.066 [0.057, 0.072] · < 1 % |
| 4–8 | 0.091 [0.083, 0.097] · 1 % | 0.044 [0.040, 0.048] · 1 % | 0.038 [0.034, 0.042] · < 1 % |
| 8–15 | 0.086 [0.081, 0.092] · 1 % | 0.031 [0.029, 0.033] · 1 % | 0.039 [0.031, 0.044] · < 1 % |

### 3.2 Shape and dominant bands

- **Roll:**
  - The PSD is flat at about 1.7–1.8 deg²/Hz from 0.23 to 0.5 Hz.
  - It then falls steeply: 0.86 at 0.70 Hz, 0.20 at 0.93 Hz, 0.036 at 1.40 Hz. That is about f^−4.6 between 0.7 and 1.4 Hz.
  - Above 2 Hz it tails off to 0.004–0.01 deg²/Hz, with a floor of about 0.001 deg²/Hz above 7 Hz.
  - **Dominant band: below 1 Hz (94 %).** Above the manoeuvre band, 0.5–1 Hz carries the most (22 %).
- **Pitch:**
  - 0.19 deg²/Hz at 0.23 Hz, falling to 0.06 at 0.70 Hz.
  - Then **a shelf of 0.03–0.05 deg²/Hz from 0.93 to 2.10 Hz**, falling steeply after it (0.008 at 2.34 Hz, 0.004 at 3 Hz), with a floor of about 1.5·10⁻⁴ deg²/Hz above 6 Hz.
  - **Dominant bands: 0.23–0.5 Hz (50 %) and the 1–4 Hz shelf (32 %).** Pitch is the only axis with a distinct wobble band above 1 Hz.
- **Yaw (heading):** about 4 deg²/Hz at 0.23–0.47 Hz, 1.1 at 0.70 Hz, 0.09 at 1.40 Hz and 0.009 at 1.6–1.9 Hz, with a floor of about 2·10⁻⁴. **Dominant band: below 1 Hz (96 %).**
- The lowest band holds the pilot's slow course and speed corrections as well as the wind (§5.1). The linear detrend per window removes anything slower than one window (4.3 s).

### 3.3 Above what the frame rate shows

- Nothing above the 15 Hz Nyquist limit can appear as an angle. Faster shake either blurs the picture within one exposure or aliases into the flat floor.
- **Aliased floor (measured):** RMS above 4 Hz is 0.13° roll, 0.05° pitch and 0.05° heading, including tracker noise.
- **Blur (measured):** the horizon edge's 10–90 % width is 3.53 px on average (p5 3.11, p95 4.01, std 0.29 px; set U, native 1080-line pixels). The blur-free synthetic set gives 2.50 px with the same pipeline.
- **Bound (derived):** even if all 1.03 px of excess (2.5 px when the widths are subtracted in quadrature) came from angular shake during the exposure, at about 17 px per degree near the horizon it is **≤ 0.15°**. The edge width barely varies over the flight (std 0.29 px, about 0.02°), so there are no intermittent vibration bursts either. The high-frequency shake at the camera is small. The wobble the pilot describes lives below 4 Hz.

## 4. Gust events, spacing and coupling

### 4.1 Events

Thresholds are 2 × the residual std of each axis over set R (Ry for yaw). Durations, rise and decay times are in seconds, as 25th / 50th / 75th / 90th percentiles.

| Axis | Threshold | Events (frames) | Rate per min | Duration | Rise to peak | Decay to 1/e |
|---|---|---|---|---|---|---|
| Roll | 1.41° | 7: f148–158, 168–179, 191–206, 216–226, 270, 276–281, 293–304 | 11.1 ± 4.2 | 0.28 / 0.37 / 0.40 / 0.45 (max 0.53) | 0.13 / 0.27 / 0.27 / 0.28 | 0.27 / 0.30 / 0.38 / 0.49 |
| Pitch | 0.62° | 11: f162–168, 204–212, 222–225, 277, 283–290, 301–304, 391–392, 552–560, 581, 781–785, 1087–1093 | 17.4 ± 5.2 | 0.10 / 0.17 / 0.25 / 0.30 (max 0.30) | 0.10 / 0.13 / 0.18 / 0.30 | 0.12 / 0.17 / 0.22 / 0.27 |
| Yaw | 1.65° | 5: f190–210, 217–231, 276–284, 297–303, 741–744 | 9.7 ± 4.3 | 0.23 / 0.30 / 0.50 / 0.62 (max 0.70) | 0.23 / 0.27 / 0.33 / 0.49 | 0.17 / 0.27 / 0.30 / 0.30 |
| Roll or pitch | 2σ of each | 9: f148–179, 191–226, 270, 276–304, 391–392, 552–560, 581, 781–785, 1087–1093 | 14.2 ± 4.7 | 0.07 / 0.23 / 0.97 / 1.10 (max 1.20) | 0.10 / 0.17 / 0.43 / 0.57 | 0.13 / 0.17 / 0.43 / 0.68 |

Residual time is 0.63 min for roll and pitch and 0.52 min for yaw.

### 4.2 Time between large disturbances

Gaps between consecutive event onsets inside one run, in seconds as 25th / 50th / 75th percentiles, mean and coefficient of variation (CV):
- **Roll or pitch:** 1.09 / 2.04 / 3.54, mean 2.41, CV 0.73 (6 gaps).
- **Roll:** 0.59 / 0.72 / 0.82, mean 0.81, CV 0.61 (6 gaps, all inside the tree-belt crossing).
- **Pitch:** 0.60 / 1.19 / 2.13, mean 1.75, CV 0.92 (8 gaps).
- **Yaw:** 0.80 / 0.90 / 1.44, mean 1.19, CV 0.47 (3 gaps).

A CV near 1 means irregular, Poisson-like arrivals, and a CV near 0 means periodic ones. The large disturbances arrive irregularly, not as a rhythm.

### 4.3 Cross-axis coupling

Pearson r at zero lag with its bootstrap interval, then the strongest r within ±1 s and its lag (a positive lag means the second axis follows):

| Pair | Residuals (R / Ry) | Rates (U / Y) |
|---|---|---|
| Roll ~ pitch | +0.21 [0.16, 0.28]; +0.33 at +0.13 s | +0.13 [0.07, 0.19]; +0.24 at +0.10 s |
| Roll ~ yaw | **+0.73 [0.63, 0.79]**; +0.77 at +0.07 s | **+0.57 [0.48, 0.63]**; +0.63 at +0.07 s |
| Pitch ~ yaw | +0.25 [0.22, 0.29]; +0.33 at −0.10 s | +0.14 [0.09, 0.19]; +0.25 at −0.10 s |

- Roll and pitch disturbances are nearly independent.
- Roll and yaw move together, with yaw following roll by only about 2 frames (§5.5).
- Autocorrelation of the residuals falls to 1/e after 0.30 s (roll), 0.17 s (pitch) and 0.33 s (yaw).

### 4.4 Terrain: tree belt against open field

`--segment 121-330` (set B, over and just past the tree belt) against the rest of R (set F, open field):

| | Roll residual std | Pitch residual std | Yaw residual std | Events per min, roll / pitch / yaw (thresholds of §4.1) |
|---|---|---|---|---|
| **B** (tree belt, 7.0 s) | **1.26° [1.16, 1.32]** | **0.43° [0.40, 0.46]** | **1.67° [1.42, 1.72]** | 60 ± 23 / 51 ± 21 / 41 ± 21 |
| **F** (open field, 30.9 s) | **0.51° [0.47, 0.53]** | **0.28° [0.25, 0.30]** | **0.44° [0.38, 0.50]** | 0 (< 1.9) / 9.7 ± 4.3 / 2.4 ± 2.4 |
| Ratio B / F | 2.5 | 1.6 | 3.8 | |

- All 7 roll events, 6 of the 11 pitch events and 4 of the 5 yaw events fall in f148–f304.
- Over the belt the roll rocks back and forth by 3–5° every 1.3–2.9 s (for example −8.6° at f153, −3.5° at f171, −7.7° at f192, −1.8° at f222, −10.3° at f279).
- Over the field the roll drifts by at most about 3° over several seconds (f701–f899).
- The 5 s blocks show the same picture: residual roll/pitch std 0.54/0.20° in f1–150, **1.39/0.46° in f151–300**, then 0.50–0.61 / 0.21–0.34° in every later block that has enough residual frames.

### 4.5 Picture losses and attitude

The largest 5-frame rate in the second before each loss, measured, in °/s (roll / pitch / yaw):
- f676: 6.0 / 2.5 / 8.5
- f955: 11.7 / 3.8 / 8.6
- f969: 11.7 / 4.5 / 8.7
- f1145: 2.9 / 5.2 / 4.6

The flight's 95th percentiles are 8.4 / 5.2 / 8.6. Two losses follow brisk roll and two do not, so **the losses show no consistent link to hard manoeuvring** (see §5.5).

## 5. Interpretation

### 5.1 The closed-loop limit (read this before the targets)

- **Measured:** every number in §2–§4 is the attitude *after* the flight controller and the pilot had already reacted. There is no stick or telemetry log.
- **Assumed (pilot, Q3):** the flight controller ran in rate (acro) mode, with no self-levelling. In rate mode it holds angular *rate*, not angle, so a gust's angular kick leaves a lasting angle error that only the pilot removes. The pilot also re-banks and re-pitches to stop the drift a gust causes. Part of the attitude motion is therefore the pilot's own correction, which is his "constantly compensating trajectory and horizon".
- **Derived:** model the pilot as a first-order corrector with time constant τ (§5.2). The measured residual is then the attitude the disturbance alone would have caused, times |S(f)| = 2πfτ / √(1 + (2πfτ)²).
  - For roll (τ = 0.30 s), |S| is 0.50 at 0.31 Hz, 0.69 at 0.5 Hz, 0.88 at 1 Hz and 0.97 at 2 Hz.
  - Below 1 Hz, where 94 % of the roll variance sits, the uncorrected disturbance would have been **1.1–2× larger than measured**. Above 2 Hz, measured and uncorrected are about the same.
  - A human reaction delay adds a resonance near 0.5–1 Hz, where the loop can even amplify the disturbance.
- **Measured:** the steady part of the wind never shows as motion, only as the lean the pilot held (§5.3).
- **Consequence:** the true disturbance is larger than any number in §2–§4, and most of all at low frequency. That is why §6 defines its targets on the closed loop, with a simulated pilot of the bandwidth below, never on the sim's open-loop gust response.

### 5.2 The pilot's correction bandwidth

- **Measured** (§4.1, §4.3): excursions decay to 1/e in a median of **0.30 s** for roll (IQR 0.27–0.38), **0.17 s** for pitch (0.12–0.22) and **0.27 s** for yaw (0.17–0.30). The residual autocorrelation falls to 1/e at 0.30, 0.17 and 0.33 s.
- **Derived:** the correction bandwidth 1/(2πτ) is then:
  - **Roll: 0.53 Hz** (0.42–0.59). That is a crossover near 3.3 rad/s, inside the 2–5 rad/s typical of a human closing a compensatory loop (**assumed**, from manual-control literature).
  - **Pitch: 0.94 Hz** (0.72–1.33).
  - **Yaw: 0.59 Hz** (0.53–0.94).
- **Derived:** the residual filter (§1.4) cuts decays slower than about 0.5 s short by up to ~20 %. These τ are therefore lower bounds within that margin.
- **Measured:** rises are nearly as long as decays (roll 0.27 against 0.30 s, pitch 0.13 against 0.17 s). The excursions are pulses of 0.3–0.6 s in total, not sudden steps that are then slowly corrected.
- **Assumed:** pitch's faster decay and its 1–2 Hz shelf (§3.2) may come partly from the airframe and flight controller rather than the pilot: a heavy airframe's slower rate loop, or coupling between throttle and pitch. P cannot separate them.

### 5.3 The mean wind, from the steady lean

**The lean and what it balances**
- **Measured:** the camera holds a left roll of −9.1° [−9.6, −8.7] and a pitch of −22.7° [−23.0, −22.4] through the whole clip, with only about −5° of net heading change (§2). That is a steady sideways force, not a turn. **The drone leans left, into a wind from the left of its path.**
- **Measured** by eye in f1, f705 and f1417: the flight controller's own on-screen attitude indicator leans the same way. The lean is real, not a camera-mount offset.
- **Assumed (pilot, Q1):** the camera has 0° uptilt, so these are the airframe's own roll and pitch. The 22.7° is the airframe's forward pitch; the airspeed it implies is derived below.
- **Derived** (steady level flight; `airframe` fields):
  - The thrust is W / (cos θ · cos φ) = **1.10 W**.
  - Its horizontal part is W · tan|θ| = 0.418 W forward and W · tan|φ| / cos θ = 0.174 W to the left. Together that is **0.453 W, pointing 22.6° left of the nose**.
  - Frame by frame, the sideways part averages 0.174 W [0.165, 0.184]. It lies between 0.091 and 0.252 W in 90 % of frames and never falls below 0.034 W.
  - Its direction stays 12.5–31.0° left of the nose in 90 % of frames, and 4.9–33.4° in all of them.
- **Derived:** in steady flight, drag balances that horizontal thrust. The drone therefore moves through the air toward about 22.6° left of its nose, and the relative wind always came from the front left. That assumes drag acts along the air velocity. If the side drag area is 0.77–1.43 times the front one, the direction moves to 16–28°.

**Crosswind and airspeed**
- **Assumed (pilot, Q7, "roll and keep direction"):** the ground track follows the nose (no crab). The air then crosses the track from the left at w_c = V_a · sin 22.6° = **0.38 V_a**. A wind along the track doesn't show in the attitude: the pilot holds the pitch, so a head or tail wind changes only the ground speed.
- **Assumed** drag: D = m · d_r · V_a + ½ ρ C_D A V_a², with:
  - linear rotor drag d_r = 0.1–0.4 s⁻¹ per unit mass (the value range reported for small quadrotors)
  - body drag area C_D A = 0.03–0.06 m² (airframe, coil, battery and cargo, pitched 22.7°)
  - ρ = 1.225 kg/m³
  - the mass during P, 1.6–3.35 kg (§5.5)
- **Derived:** D = 0.453 W gives:
  - an **airspeed V_a ≈ 12 m/s (8–21 m/s)**
  - a **mean crosswind ≈ 4.5 m/s (2–10 m/s, 7–36 km/h)** across the track. The range includes the drag-direction uncertainty above.
  - At that airspeed, every 1 m/s of extra crosswind needs 2.0° (1.1–3.0°) more lean to hold the track.
- **Assumed (pilot, Q6, Q8):** about 10 km covered in a flight of about 10 minutes is about 17 m/s of mean ground speed, if the flight went mostly outward. That is the same order as V_a, so the derived airspeed is plausible.
- **Derived:** this replaces the earlier estimate of 12 m/s (8–20 m/s). That estimate took the lean alone for a hovering 4–7 kg drone, with rotor drag neglected. The confirmed forward pitch means fast flight, and in fast flight a crosswind's side drag grows with the airspeed, so a smaller crosswind gives the same lean. The pilot's "severe wind" (strength unknown, Q2) then lies more in what P cannot see as a mean:
  - the gusts
  - the belt's wake
  - the direction changes
  - any wind along the track

**Direction (Q2)**
- **Assumed (pilot, Q2):** the wind came mostly from the left, but at random times from other sides too.
- **Measured check:** consistent for these 47 s.
  - The sideways thrust stayed on the left in every frame (at least 0.034 W, roll ≤ −1.8°), so the crosswind never reversed in the toughest stretch.
  - Its spread (std 0.047 W [0.041, 0.051], 27 % of the mean) is the crosswind's variation plus the pilot's course changes.
- **Derived limit:** the lean shows only the wind component across the track. For a wind from abeam, a direction swing changes that component only to second order. P therefore cannot show how far or how often the direction changed.
- **Derived (model requirement):** a prevailing direction per flight plus random direction changes, never a fixed vector. The changes must be:
  - slow or small enough that a ~40 s stretch can keep the crosswind on one side (P)
  - large enough to put the wind on other sides within a flight of about 10 minutes (pilot, Q2, Q6)
- **Assumed** (starting values, tunable):
  - a slow meander around the prevailing direction: a bounded random walk with std 30° and a correlation time of 2 min
  - rare shifts of 45–120° over 10–30 s, at about 1 per 5 min (gust fronts in rain showers)
  - local reversals from the obstacle-wake model, near belts and buildings

  The fast wobble in direction comes from the turbulence in §5.4.

### 5.4 Turbulence intensity and scale, near the ground and near obstacles

**Intensity**
- **Derived:** at a given airspeed, both drag terms make the sideways force linear in the crosswind (§5.3). The sideways thrust's relative spread, 0.047 / 0.174 = 0.27, then bounds **σ_v ≲ 0.27 · w̄_c ≈ 1.2 m/s (0.5–2.7 m/s)**. That covers the slow part and includes the pilot's own course changes, so it is an upper bound for the wind alone.
- **Assumed:** the standard low-altitude turbulence model (MIL-F-8785C, Dryden form) over farmland, at 20–50 m (the height varied, pilot Q5), gives:
  - σ_v/U ≈ 0.12–0.15, which is at least 0.5–0.7 m/s at P's crosswind, inside the bound above
  - length scales L_u = L_v ≈ 115–200 m
  - L_w ≈ the height

**Open field against tree belt** (measured, §4.4)
- **Open field:** the wobble is modest (roll residual 0.51°, pitch 0.28°), and there is not a single 2σ roll event in 30.9 s.
- **Over and just past the tree belt** (7 s): roll 1.26° (2.5×), pitch 0.43° (1.6×) and yaw 1.67° (3.8×). All 7 roll events fall here, and the drone rocks by 3–5° every 1.3–2.9 s.
- **Derived:** suppose the belt's thrust-tilt swings (1.72° std, §5.5) are the pilot's answers to sideways pushes. Through the 2.0° (1.1–3.0°) of lean per m/s (§5.3):
  - the swings are crosswind fluctuations of at least 0.9 m/s std (0.6–1.5 m/s) above 0.3 Hz
  - the 3–5° rocks are gusts of at least 1.5–2.5 m/s (1.0–4.5 m/s)

  Both are lower bounds, because the pilot doesn't fully answer gusts this fast (§5.1).

**Scale**
- **Derived:** the belt multiplies the attitude disturbance by 1.6–3.8, even though the drone is well above the tree tops. Its wake adds turbulence at scales far below the open-field integral scale.
  - Roll events over the belt last 0.28–0.45 s and arrive 0.6–0.8 s apart.
  - At the derived airspeed of 8–21 m/s (§5.3, frozen turbulence assumed), those are eddies of about 2–9 m, spaced 5–17 m apart. That is the size of the trees themselves.
- **Assumed:** field studies of shelterbelt wakes report up to about twice the upstream turbulence intensity from roughly 5 to 15 belt heights downwind, in a wake that grows to 2–3 belt heights tall. That matches the measured 1.6–2.5× in roll and pitch.

**What the model needs** (derived)
1. Broadband turbulence whose energy reaches the airframe at encounter frequencies up to 2–4 Hz, not only slow gusts.
2. Obstacles that shed their own, stronger turbulence (a wake behind tree belts and buildings on top of the background), because that is where the drone "yanks".
3. Intermittent gusts (residual kurtosis 3.8–4.7 in roll and pitch, 9 in yaw), not Gaussian noise.
4. Gusts that act as real sideways forces: drag of the relative wind on the body, coil and rotors, not only moments. That is what forces the horizon–heading trade-off (§5.5).

**Not covered:** nothing in P is flown close to the ground, so turbulence within a few metres of the ground is not measured here.

### 5.5 Mass, motor margin and saturation

This whole section takes the camera's angles as the airframe's (0° uptilt, pilot Q1).

**Mass and inertia (Q4)**

| Item | Mass | Label |
|---|---|---|
| Frame, 4 motors of the 3115 class, 10" three-blade props, ESC, flight controller, fiber air unit, camera, legs | 0.8–1.25 kg (nominal 1.0) | assumed, for the Viriy 10 Opto class the pilot named |
| Fiber coil at take-off | ≈ 1.0 kg (0.9–1.1) | pilot, Q4 |
| Cargo and battery | ≈ 0.8 kg (0.6–1.0, reading "maybe 100–200 g" as up to ±0.2 kg) | pilot, Q4 |
| **Take-off** | **2.3–3.35 kg (nominal 2.8)** | derived |
| Coil during P | 0.2–1.0 kg: the fiber pays out along the flight, and the pilot was about 10 km away (Q8). An empty spool with its housing is taken as at least 0.2 kg | assumed |
| **During P** | **1.6–3.35 kg** | derived |

- **Assumed** layout:
  - motors and props: 4 × 85–105 g, 0.21–0.23 m from the centre
  - arms: 100–160 g
  - battery on top, 5 cm above the arm plane
  - cargo 5 cm forward of the centre and 5 cm below the arm plane
  - coil: a drum 14 cm across and 8 cm tall, centred 10 cm below the arm plane
  - everything else: a 16 × 10 × 6 cm core
- **Derived** (sum over those parts, across both mass ranges):
  - roll inertia **0.013–0.027 kg·m²**, pitch **0.015–0.030 kg·m²** and yaw **0.020–0.033 kg·m²** (nominal take-off: 0.023, 0.025 and 0.027)
  - radii of gyration of about 0.09 m (roll), 0.095 m (pitch) and 0.10–0.11 m (yaw)
  - yaw inertia is only 1.1–1.6 times roll inertia, not twice as before: the hanging coil adds roll and pitch inertia but little yaw inertia
- **Derived:** with a full coil, the centre of mass sits about 3 cm below the arm plane and the coil's centre 7 cm below that. A side gust on the coil is therefore also a rolling moment.

**Collective thrust**
- **Derived** (`airframe.load_factor`): holding height at the measured tilt needs **1.10 W** on average (p95 1.13 W, maximum 1.14 W).
- **Assumed:**
  - 3115-class motors (900 Kv, 6S, the class published for this airframe family) with 10" three-blade props give 2.2–2.8 kgf each, static, on a full battery.
  - P's flight state derates that to 0.55–0.75, from two effects:
    - battery sag under load: thrust at full throttle scales with the voltage squared, giving 0.73–0.85
    - 3–8 m/s of axial inflow through discs tilted 22.7° at 8–21 m/s, giving 0.75–0.9
- **Derived:**
  - At take-off, the static maximum thrust is 2.6–4.9 W.
  - During P, 1.4–5.3 W is available. The mean need of 1.10 W is **21–76 % of it** (nominal 47 %, at the nominal take-off mass of 2.8 kg, 2.5 kgf per motor and a derating of 0.65).
  - That is a **thrust margin of 1.3–4.8 (nominal 2.1)**.
- **Derived:** this replaces the earlier case of 1.52 W and 70–95 % throttle, which assumed 25° of uptilt; Q1 rules that out.
  - Collective thrust is not saturated in the mean. It gets close only at the heavy end with a sagged battery.
  - The "motors crying" fits high rpm at 12 m/s or more, plus fast differential changes (below), better than a throttle held near its limit.

**Roll and pitch authority**
- **Measured** (`airframe.body_accel`, `accel[pitch]`): the 95th-percentile angular acceleration about the body's roll axis is 151 °/s² (maximum 412). In pitch it is 97 °/s² (maximum 208). Both include noise (up to about 53 °/s² std in roll and 20 °/s² in pitch, §2), so they are upper bounds.
- **Assumed:** the motors sit 0.148–0.163 m from the roll and pitch axes (the 0.21–0.23 m arms of a 10" X frame).
- **Derived:** at the 95th percentile these need only **1.3–1.5 % of hover thrust as differential thrust**, and 3.5–4.1 % at the maximum. Pitch needs 0.9–1.1 % and 2.0–2.3 %.
- **Measured:** the roll and pitch rate distributions are symmetric to within 5 %, with no clipped tails. **There is no sign of roll or pitch saturation.**

**Yaw authority**
- **Assumed:** rotor drag torque of 0.015–0.02 N·m per newton of thrust (10" three-blade props).
- **Derived:** the body yaw acceleration of 77 °/s² at the 95th percentile (maximum 288) needs **6–11 % differential thrust** between the two rotor pairs, and **24–43 %** at the maximum. These are upper bounds, because they include noise.
- **Derived:** **yaw is the axis that runs out of authority first**. It needs 4–9 times the roll figure.
  - With the collective at 21–76 % of the available thrust, a 43 % shift between rotor pairs comes close to a motor's limit.
  - Yaw's heavy tail (kurtosis 9) and slow decay fit that.

**The horizon–heading trade-off (Q1, Q7)**
- **Derived (kinematics, 0° uptilt):**
  - Rolling about the body's own axis turns the camera about its optical axis, so it never changes the camera heading.
  - Yawing about the body axis with the nose pitched down by θ rolls the horizon: φ̇ = p + ψ̇ · sin θ, where p is the body roll rate. This ZYX relation is exact.
  - At θ = −22.7°, pure body yaw gives φ̇ = −0.39 ψ̇. That has the **opposite** sign to the measured coupling, so the +0.57 is not geometric.
- **Derived:** only the roll stick tilts the thrust sideways. Yaw about the body axis leaves the thrust vector where it is.
  - So for small changes, the thrust's sideways tilt is **e = φ + k · ψ, with k = −sin θ = 0.386**. The roll stick alone sets it.
  - To hold its line, the drone needs whatever e the crosswind demands. That e has to be shared between the horizon and the heading:
    - **Roll and keep direction:** ψ is held, so φ = e. The horizon swings with every gust.
    - **Yaw and keep the horizon:** φ is held, so ψ = e / k. The heading swings 2.6° for every degree of tilt. Holding P's mean 9.1° lean with a level horizon would need about 25° of crab.
    - **Hold both:** e is then fixed, and the drone drifts off its line.
  - This is the pilot's "either you yaw but lose direction, OR you roll and keep direction but lose the horizon".
- **Measured and derived** (`segment` and `airframe` fields; belt f121–330 against the open field). The correlations are measured; the tilt e and its share on the horizon follow from them through the relation above:

  | | Tilt residual std, e | Roll ~ yaw residual, r | Roll ~ yaw rate, r | Share of e on the horizon |
  |---|---|---|---|---|
  | Belt | **1.72° [1.63, 1.72]** | +0.88 [0.85, 0.90] | **+0.77 [0.73, 0.79]** | 0.64 |
  | Field | 0.60° [0.54, 0.64] | +0.51 [0.39, 0.63] | +0.34 [0.25, 0.42] | 0.80 |
  | Whole clip | 0.92° [0.71, 1.07] | +0.73 [0.63, 0.79] | +0.57 [0.48, 0.63] | 0.70 |

  - The belt's intervals rest on only three bootstrap blocks.
  - At the stick level, the body roll and yaw rates correlate at +0.77 [0.70, 0.81] over the whole clip.
- **Derived:** the coupling concentrates where the gusts are, over the belt, and the horizon and heading swing together, both into the wind. That is the trade-off paid on both sides at once.
  - Over the belt the pilot put 64 % of the tilt on the horizon and 36 % on the heading. Over the field it was 80 % and 20 %.
  - Yaw follows roll by 2 frames (§4.3): the roll stick comes first, and yaw authority is weaker.
- **Assumed:** part of the coupling may be aerodynamic. A side gust on the coil below the centre of mass rolls the drone into the wind, and weathervaning yaws it into the wind; together those would also couple roll and yaw positively. P cannot separate that share from the pilot's inputs. The pilot's account (Q7) and the concentration over the belt point to the pilot.
- **Derived (consequence):** the sim can't fake this. It needs:
  - sideways gusts that are real sideways forces
  - the camera at 0° uptilt on a drone pitched forward
  - exact rate-mode kinematics

  §6 tests it (T10b, T11).

**Picture losses and the fiber (Q8)**
- **Measured** (§4.5):
  - three picture losses of 0.40–0.67 s and one black frame in 47 s, all recovered, with no consistent link to brisk manoeuvres
  - all fall at f676–f1164, 12–27 s after the belt crossing, over open field
  - none falls over the belt, where the wobble was 2.5 times larger
- **Assumed (pilot, Q8):** the cause is unknown, because the pilot was about 10 km away.
  - His hypothesis: wind shakes the optic fiber, making the picture noisier and flashier, and the trees plus the drone's wobble stretch it.
  - `video-feed.md` N12 gives these same events that driver: link-margin dips from fiber tension spikes and tight bends.
  - N12 owns their statistics, so they are not re-measured here.
- **Derived:**
  - P gives the "wobble" part little support (above).
  - The "trees" part fits the timing: fiber paid out over the belt and scrub stays there, swaying with the trees, long after the drone has passed.
  - P has no link data to test it further.
- **Derived:** for N12, the tether model must provide:
  - the tension at the spool exit
  - the tightest bend along the paid-out span, and whether the span touches vegetation
  - the span's wind-driven vibration, from the crosswind across it
- **Derived** (§3.3): shake at the camera is ≤ 0.15° within an exposure and ≤ 0.13° RMS aliased above 4 Hz. The airframe and camera mount keep motor vibration out of the picture, so the "crying" is not visible as shake.

### 5.6 Summary for the wind model

1. A steady crosswind that makes the drone lean about 9° at 22.7° of forward pitch (derived: roughly 4.5 m/s across the path at about 12 m/s of airspeed). It blows from a prevailing direction that changes at random, never a fixed vector (pilot, Q2).
2. Background turbulence that, under a pilot of 0.5 Hz (roll) and 0.9 Hz (pitch) bandwidth, leaves 0.5° of roll and 0.3° of pitch wobble above 0.3 Hz over open field.
3. Wakes behind tree belts (and, by extension, buildings) that multiply that wobble 1.6–4× and deliver a burst every 0.6–0.8 s. The gusts push sideways, so the pilot pays for each one with horizon or heading (§5.5).
4. Intermittent bursts rather than smooth noise. Roll and pitch are nearly independent; roll and heading are coupled through that trade-off.
5. Yaw authority is the weak link. Collective thrust keeps a margin of about 2 (1.3–4.8).
6. Picture losses need a fiber tension, bend and vibration driver (the pilot's hypothesis, `video-feed.md` N12).

### 5.7 The pilot's answers, and what is still open

The pilot answered Q1–Q8 (`pilot-answers.md` in the OpenSpec change). His standing direction for wind: real life, unexpected and undefined.

| # | Question | Answer (pilot) | Where it went |
|---|---|---|---|
| Q1 | Camera uptilt | **0°** | Camera angles are the airframe's (§1.2); thrust and margin (§5.5); the roll–yaw coupling re-explained (§5.5, T10b) |
| Q2 | Wind direction and strength | Mostly from the left, but at random from other sides too. Strength unknown | Checked against the lean, and a prevailing direction plus random changes (§5.3, T0b) |
| Q3 | Flight mode | Acro (rate) | §5.1; the simulated pilot (§6.1) |
| Q4 | Mass and props | Viriy 10 Opto, 10" props, coil about 1 kg, cargo and battery about 800 g ("maybe 100–200 g"), frame mass unknown | Mass range and inertia (§5.5), airspeed (§5.3), test mass (§6.2) |
| Q5 | Height and speed | Varied height | Turbulence scales as a range (§5.4); test heights spread (§6.2) |
| Q6 | Flight duration | About 10 min; this clip was the toughest part | §6 describes the worst stretch; milder bands in §6.4 |
| Q7 | Was the tree belt worse? | Yes. "Either you yaw but lose direction, OR you roll and keep direction but lose the horizon" | The horizon–heading trade-off (§5.5), the pilot's split (§6.1), T10b, T11 |
| Q8 | Cause of the picture losses | Unknown (about 10 km away). Hypothesis: wind shakes the fiber, and trees plus wobbling stretch it | A tether driver for `video-feed.md` N12 (§5.5) |

Still open. Rough answers are enough, and none of them blocks the targets:

| # | Question | What it sharpens |
|---|---|---|
| Q9 | Motor size and battery: cells, capacity, LiPo or Li-ion? | Thrust margin and sag (§5.5) |
| Q10 | Does "maybe 100–200 g" mean the cargo alone, or a correction to the 800 g? | Mass range (§5.5) |
| Q11 | Roughly how many kilometres of fiber did the 1 kg coil hold? | The coil mass left during P (§5.5), and the tether's mass per metre |

## 6. Severe-wind targets

A future `severe wind` preset passes when the sim, flown by the simulated pilot below along the test flight below, reproduces these numbers. Every target is a field of `wind_stats.py`'s JSON, so a physics test can check it automatically. In the field names, `angle[roll]` means the entry of the `angle` list whose `axis` is `roll`, and `bands[i]` counts the §3.1 bands from 0.

**What they describe (pilot, Q6):** P is the toughest 47 s of a flight of about 10 minutes, and the tree belt in a strong crosswind is what made it the toughest (Q7). The targets therefore describe the **worst stretch** of a severe-wind flight, not a whole flight. A whole flight in the same preset should be milder on average, because most of it is open field. §6.4 gives milder bands.

### 6.1 The simulated pilot

- **Rate mode (pilot, Q3).** The sticks command the body rates p, q and r through the sim's own flight-controller model. There is no self-levelling or angle mode anywhere in the loop.
- **Perception.** He sees the sim camera (0° uptilt, pilot Q1), sampled at 29.917 Hz, with a reaction delay of **d = 0.10 s** (assumed; the measured decays already include the real pilot's delay). He reads:
  - the horizon roll φ
  - the camera pitch θ
  - the heading ψ
  - the sideways slide v of the ground across the course, positive to the right
- **Thrust tilt (Q7).** The tilt he wants is e* = ē − v / (g · τ_v), in radians, with **τ_v = 0.5 s** (assumed). ē obeys ē̇ = −v / (g · τ_v · 3 s) (assumed), so it settles on the mean lean into the wind.
- **Split (Q7).** He shares any change of tilt between the horizon and the heading with **s = 0.64** (derived: the belt's share in §5.5):
  - φ_ref = ē + s · (e* − ē)
  - ψ_ref = ψ_course + (1 − s) · (e* − ē) / k, with k = −sin θ̄ from the run's mean pitch
- **Pitch.** θ_ref holds the mean camera elevation at −22.7° ± 2°, P's speed regime.
- **Loops.**
  - On each axis he commands the Euler rate (ref − angle) / τ, with **τ_roll = 0.30 s, τ_pitch = 0.17 s and τ_yaw = 0.27 s** (measured, §5.2).
  - He turns those rates into sticks with the exact ZYX relations p = φ̇ − ψ̇ sin θ, q = θ̇ cos φ + ψ̇ cos θ sin φ and r = ψ̇ cos θ cos φ − θ̇ sin φ.
  - That makes him a fluent rate-mode pilot, who adds roll when he yaws with the nose down.
- **Throttle.** Holds the run's height through a 2 s loop (assumed).
- Makes no other inputs.
- **Variants**, flown on the same seeds and reported alongside the nominal pilot:
  - d = 0 and d = 0.2 s
  - τ_v = 0.3 and 1.0 s
  - for T11b only, s = 1 and s = 0

### 6.2 The test flight and the measurement

1. **Setup:**
   - The sim's drone of the Viriy 10 Opto class, with 0° camera uptilt (pilot, Q1).
   - Mass 2.5 kg, the middle of P's 1.6–3.35 kg, with the inertia of the §5.5 component model at that mass. This is assumed. One extra run each at 1.6 and 3.35 kg is reported but not scored.
   - The `severe wind` preset, with its prevailing direction abeam from the left of the course and its random direction changes switched on (pilot, Q2). Its mean speed is set by T0.
2. **Route:** a straight course, well above the tree tops.
   - P's height varied and is unknown (pilot, Q5), so the runs spread evenly over 20–50 m above ground (assumed).
   - Each run has **32 s over open field and 7 s** from the upwind edge of one tree belt to about 10 belt heights downwind. That is P's mix: 7.0 s of belt in 37.9 s of residual time.
3. **Runs:** at least **5 runs** of the nominal pilot, with independent seeds. The §6.1 variants fly the same seeds. Each run's first 5 s after release are discarded.
4. **Log:** each run as its own CSV in the `attitude.csv` columns at 29.917 Hz, with `conf_* = 1`, `dup = 0` and `flag = ok`. With the world "up" vector **u** in camera coordinates (x right, y down, z forward):
   - `roll_deg = atan2(−u_x, −u_y)`
   - `pitch_deg = asin(u_z)`
   - `yaw_rate_dps` = the frame-to-frame change of the optical axis's azimuth × 29.917

   These match §1.2 exactly.
5. **Measure:** `blender -b --factory-startup --python tools/reference/wind_stats.py -- --csv <run.csv> --segment <belt frames> --json <run.json>`, then average each field over the runs.
6. **Pass:** every target's average over the nominal runs lies inside its band. T11b uses the variant runs.

Bands are about ±35 % on spreads. That covers the bootstrap interval, the ±10 % variation between 5 s open-field blocks in P, the −6/+15 % pitch-scale assumption and the simple pilot model. Yaw quantities and event rates get about ±50 % (lens sensitivity and Poisson counts), and correlations get absolute bands. These are acceptance bands for a first model. The pilot's MVP flights have the final word.

### 6.3 Targets

| ID | Quantity | JSON field | P value [16–84 %] | Target band |
|---|---|---|---|---|
| T0 | Mean lean into the crosswind (calibrates the preset's mean crosswind) | `angle[roll].mean` | −9.1° [−9.6, −8.7] | \|mean\| = 9 ± 3°, leaning into the wind |
| T0b | The wind is never a fixed vector, yet keeps its side through a stretch: relative spread and minimum of the sideways thrust | `airframe.lateral.std` / `airframe.lateral.mean`; `airframe.lateral.min` | 0.27 (0.047 / 0.174); 0.034 W | 0.14–0.40 (at least half of P's; the rest may be the pilot's course changes); min > 0 on the windward side in at least 4 of 5 runs |
| T1 | Residual roll std, open field | `segment.outside.roll.res_std` | 0.51° [0.47, 0.53] | 0.35–0.70° |
| T2 | Residual pitch std, open field | `segment.outside.pitch.res_std` | 0.28° [0.25, 0.30] | 0.19–0.38° |
| T3 | Residual roll std, tree belt | `segment.inside.roll.res_std` | 1.26° [1.16, 1.32] | 0.85–1.70°, and ≥ 1.8 × the sim's T1 |
| T4 | Residual pitch std, tree belt | `segment.inside.pitch.res_std` | 0.43° [0.40, 0.46] | 0.29–0.58°, and ≥ 1.2 × the sim's T2 |
| T5 | Residual yaw (heading) std, open field / tree belt | `segment.outside.yaw.res_std` / `segment.inside.yaw.res_std` | 0.44° [0.38, 0.50] / 1.67° [1.42, 1.72] | 0.22–0.66° / 0.8–2.5° |
| T6a | Spectral band, roll: share of 0.23–15 Hz variance below 1 Hz | `spectra.roll.bands[0..1].share` | 94 % | ≥ 85 % |
| T6b | Spectral band, roll: 2–4 Hz RMS (the "never smooth" floor; P's value includes ≤ 0.04° of noise) | `spectra.roll.bands[3].rms` | 0.114° [0.103, 0.124] | 0.06–0.16° |
| T6c | Spectral band, pitch: share of variance in 1–4 Hz, and 1–2 Hz RMS | `spectra.pitch.bands[2..3].share`, `spectra.pitch.bands[2].rms` | 32 %; 0.18° [0.15, 0.20] | 20–45 %; 0.11–0.25° |
| T6d | High-frequency ceiling: RMS above 4 Hz | `spectra.{roll,pitch}.bands[4..5].rms`, combined | roll 0.125°, pitch 0.054° | ≤ 0.13°, ≤ 0.06° |
| T7 | Gust events, roll or pitch beyond 2σ of each axis, over P's field and belt mix | `events.roll_or_pitch.per_min`; `segment.inside.roll.events` / `events.roll.count` | 14.2 ± 4.7 /min; 7 of 7 | 7–24 /min; ≥ 70 % of roll events inside the belt segment |
| T8 | Time between large disturbances (onset gaps) | `events.roll_or_pitch.gap_q[1]`, `.gap_cv` | median 2.0 s, CV 0.73 | median 1–4 s, CV 0.5–1.2 (irregular, never periodic) |
| T9 | Intermittency: residual kurtosis | `residual[roll].kurtosis`, `residual[pitch].kurtosis` | 4.74, 3.81 | ≥ 3.6, ≥ 3.3 |
| T10a | Cross-axis correlation, roll ~ pitch residual | `corr["roll~pitch residual"].r` | +0.21 [0.16, 0.28] | −0.05 to +0.40 (nearly independent) |
| T10b | Roll ~ yaw rate correlation: the trade-off paid on both axes, strongest over the belt | `corr["roll~yaw rate"].r`; `segment.inside.corr["roll~yaw rate"].r` | +0.57 [0.48, 0.63]; belt +0.77 [0.73, 0.79] | +0.35 to +0.75; belt +0.55 to +0.90, and above the field's `segment.outside.corr["roll~yaw rate"].r` |
| T11a | Thrust-tilt residual std (the sideways push the pilot must answer), tree belt / open field | `segment.inside.tilt.res_std` / `segment.outside.tilt.res_std` | 1.72° [1.63, 1.72] / 0.60° [0.54, 0.64] | 1.1–2.3° / 0.39–0.81°, and belt ≥ 1.8 × field |
| T11b | The trade-off is unavoidable (variant runs over the belt) | with s = 1: `segment.inside.roll.res_std` ÷ `segment.inside.tilt.res_std`; with s = 0: `segment.inside.yaw.res_std` × `airframe.k` ÷ `segment.inside.tilt.res_std`; in both: `segment.inside.tilt.res_std` | P flew s = 0.64: belt roll 1.26° (T3), heading 1.67° (T5), tilt 1.72°. With s = 0, that tilt would need ≈ 4.5° of heading | ≥ 0.8 for s = 1; ≥ 0.8 for s = 0; the tilt within ±25 % of the nominal run's T11a in both |

**Why T11b makes the trade-off unavoidable.** Because e = φ + k · ψ, the pilot can hold the horizon or the heading, but not both, while he holds his line. T11b fails if the sim lets the drone hold its line without tilting the thrust. That happens with gusts that are only moments, with noise added to the pose, or with a drag model that has no sideways part. With s = 0, that pilot would then have no reason to yaw, and the heading would stay calm.

### 6.4 Milder bands (derived, not measured)

The rest of P's flight is not recorded, so no milder band is measured. Two are derived.

1. **Same wind, away from obstacles:** the open-field targets alone. That means T1, T2, the open-field halves of T5 and T11a, and no roll events (P has 0 in 30.9 s, fewer than 1.9 per min). Most of a flight like P's is flown in this regime (assumed).
2. **Milder wind, "typical windy":** a mean crosswind c times P's, at the same airspeed (pitch trim). Assume the turbulence intensity σ/U stays fixed (neutral surface layer) and the closed loop stays linear. Then:
   - **Scaled by c:** every angle spread and band RMS (T1–T5, T6b, the T6c RMS, T6d, T11a), and the tangent of the mean lean (T0).
   - **Unchanged:** shares, event rates (their thresholds scale too), gap statistics, kurtosis and correlations (T0b, T6a, the T6c share, T7–T10).

   The default is **c = 0.5** (assumed), a mean crosswind of about 1–5 m/s:

   | | T0 lean | T1 | T2 | T3 | T4 | T5 field / belt |
   |---|---|---|---|---|---|---|
   | Typical windy band | 4.6 ± 1.5° | 0.18–0.35° | 0.10–0.19° | 0.43–0.85° | 0.15–0.29° | 0.11–0.33° / 0.4–1.25° |

   - The linear scaling is least safe for yaw: P's heavy yaw tail comes from authority running out (§5.5), and a milder wind should soften it.
   - c is a starting value. The pilot's MVP flights set it.

## 7. Rain on the feed

_Reserved for the tech-artist (task 2.3). Physics adds nothing here._
