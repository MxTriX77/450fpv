# The camera's micro-motion above 4 Hz: why the sim looks still, and what is missing

For D-059 ("the drone's camera must be sensitive to micro movements"), PHY-25 and PHY-13. The tech artist's `CameraFeelProbe` (`p7e-camera-feel` design §1) found the sim's camera 40–1000× quieter above 5 Hz than clip P. `p7c-wind`'s re-grade (tasks 1.5 and 1.8) found the roll and pitch spreads 2–3× low and the yaw 3× too lively against them. This note measures where both come from. Labels: *pilot*, *measured*, *published*, *derived*, *assumed*. Clips by letter only; no OSD value was read.

## 0. The answer

1. **The 40–1000× compares different things.** `p7e` flew `unity/main`'s stand-in wind (one gust per world axis, the same over the whole drone), in a hover, a 3 s window, and set the true 1 kHz turn above 5 Hz beside P's *measured* floor at 30 frames a second. Each of those moves the figure:
   - On `p7c-wind`'s field, the same loadouts, seed and metric read 0.0082–0.0085° over 35 s (seed 1, 0 and 2 kg, 35 and 300 m), against `p7e`'s 0.0023–0.0033°. Over 5 seeds at the nominal loadout: 0.0134° (measured, §3).
   - P is a cruise at about 20 m/s, not a hover. Cruise in Severe reads 0.0208° roll and 0.0209° pitch in the frames' 4–15 Hz (§3).
   - About half of P's floor is the measurement's (§2). P's real camera motion over 4–15 Hz is about **0.06–0.08° roll, 0.04° pitch and 0.025° heading** (derived), not 0.13 / 0.05 / 0.05°.
   - **Like for like, the physical gap is 3–4× in roll, 2× in pitch and about 5× in heading.** It is not 40–1000×.
2. **The sim's floor above 4 Hz is the turbulence's per-rotor gradient:** 97 % of roll's power and 87 % of pitch's in cruise (§4). The props' unbalance, the blade pass, the blade-to-blade spread, the motors' torque ripple and cogging, the ESCs' commutation and the gyro's noise each move the camera's *angle* by 10⁻⁸–10⁻³°. They are vibration (`CameraVibration`, the mount's acceleration), not attitude. D-059's expectation that "each prop's small factory imbalance … and the frame's modes give the high-frequency shake" does not hold for the picture's motion: even at G 40 the unbalance turns the camera 1.4·10⁻⁴° at 1/rev.
3. **The gap above 4 Hz is the tail of the spreads' gap, and both are largest at 1–4 Hz** (P is 2.6–3.3× the sim at 1–2 Hz and 6× at 2–4 Hz, §3). There is no separate high-frequency source to add.
4. **Two corrections from measured or published numbers close roll's and yaw's spreads; pitch stays 3× low** (§5):
   - the harness pilot's split over the field: K§8.4 *measured* 80 % of the tilt on the horizon over P's field and 64 % over the belt, and the harness flies 0.64 everywhere
   - the open field's turbulence from the landscape's roughness (Davenport–Wieringa class 4, z0 0.10 m, σ_u/U 0.164) instead of the stubble's z0 0.03 m

   Together with the turbulence carried down to the discs' scale (0.5 m), Severe cruise reads roll 0.46° (P 0.51), yaw 0.48° (P 0.44) and pitch 0.094° (P 0.28); above 4 Hz 0.033 / 0.027 / 0.007°.
5. **Yaw does not over-answer the turbulence; the harness pilot does.** With the heading held (split 1) the turbulence moves the heading 0.08°. The 0.57–0.76° is the pilot putting 36 % of each tilt correction on the heading, at 0.93° of heading per degree of tilt (§5).
6. **What remains is 1–4 Hz, mostly pitch.** The named candidates are the human pilot's remnant (the harness pilot is noiseless) and the rear rotors' ingestion of the front rotors' wake in cruise (not modelled). Neither is measured here.

## 1. The probe

`MicroMotionProbe` (`unity/Packages/com.fpv450.sim/Tests/MicroMotionProbe.cs`) flies the repository drone with its whole airframe (asymmetry, rings, ground effect, flapping, modes), as `WindHarness` does:
- **Hover:** `SimPilot` holds a point 35 m up (assumed: inside K§8.5's 20–50 m, at the field's reference height).
- **Cruise:** `HarnessPilot` holds clip P's −22.7° over open field at 35 m, released at 20.6 m/s with the drawn wind abeam from the left, on the start rule's second. No strip.
- **The camera:** the rigid attitude turned by the frame's elastic turn at the lens, every step, in the tracker's roll, pitch and heading (wind.md §1.2).
- **What it reports:**
  - *true bands:* the 1 kHz attitude's Welch PSD (Hann, 8192 steps) as RMS over K§8.6's bands and 15–50, 50–150, 150–500 Hz
  - *frame bands:* the attitude point-sampled at 29.917 Hz (the harness's clock) through `wind_stats.py`'s Welch (128 frames), so anything above 15 Hz folds in as it would in footage
  - *picked frames:* each frame at the newest whole field's horizon row instead (§2.3)
  - *spreads:* the residual about `wind_stats.py`'s σ = 0.6 s trend (K§8.6's T1, T2, T5 over field)
  - *roll share:* cov(roll, e)/var(e) with e = roll + k·heading, `wind_stats.py`'s split (K§8.4)
  - *p7e's figure:* `CameraFeelProbe`'s first-order 5 Hz high-pass of the camera's rate, integrated
- **Checks in the suite:**
  - `TheMeasurementRoundTrips`: sinusoids land in their bands at their RMS; a 100 Hz one folds into the frames' 8–15 Hz; the known-wrong 0.15° of white noise on the pose reads about 0.13° over 4–15 Hz, P's roll floor.
  - `TurbulenceIsTheCruiseFloorAndTheUnbalanceIsNot`: Severe cruise, seed 1, 25 s. 4–15 Hz roll 0.0163° as built, 0.0000° with the known-wrong fixed vector, 0.0162° with the asymmetry off.
- **Tables:** `Levels` and `SourcesTable` (Explicit) print §3 and §4. The sources the runtime does not carry were measured through the scratch hooks of Appendix A, which are not in the tree.

## 2. Clip P's floor: how much is the measurement's

`tools/wind/floor_check.py` reads a tracker log and splits its floor. Its tests plant a fit-independent floor (the known-wrong reading, as real motion would be) and glitches tied to the fit; the first reads flat across the fit's changes (top/bottom quartile 0.98, kurtosis 2.96), the second does not (4.09, 6.45). On P's log (local only, measured):

| | Roll | Pitch | Heading |
|---|---|---|---|
| RMS over 4–15 Hz (wind.md §3.1) | 0.126° | 0.054° | 0.054° |
| PSD 2–3 / 4–6 / 8–10 / 12–15 Hz, deg²/Hz | 8.5e-3 / 2.7e-3 / 1.4e-3 / 7.9e-4 | 1.1e-2 / 7.2e-4 / 1.3e-4 / 1.2e-4 | — |
| Flat floor (≥ 8 Hz) as a share of the 4–15 Hz power | 75 % | 51 % | ≈ 75 % (wind.md) |
| h = x − mean of its neighbours, white-equivalent per frame | 0.122° | 0.049° | 0.061° |
| h's kurtosis; share of h's power in the top 5 % of frames | 6.6; 44 % | 5.9; 41 % | 5.4 |
| h by quartile of the fit's inlier-share change, low → high | 0.078 → 0.160° (× 2.06) | 0.041 → 0.057° (× 1.38) | — |
| h by quartile of the fit's span change, low → high | 0.090 → 0.171° (× 1.89) | 0.038 → 0.066° (× 1.73) | — |
| Strip check: the horizon's pitch error per frame | — | 0.014° robust, 0.035° std | — |

### 2.1 What the numbers say
- **Real motion falls steeply above 4 Hz; P's floor does not.** A rigid body under f^−5/3 turbulence falls as about f^−5.7 in angle. P's roll falls by 2 from 6 to 15 Hz, and its pitch is flat above 8 Hz.
- **It is heavy-tailed and tied to the fit.** A twentieth of the frames carry 41–44 % of its power, and it doubles in roll where the fit's inlier set or span jumps. The camera's real motion does not know what the fit's inliers did. So **at least 46–59 % of roll's high-frequency power and 30–40 % of pitch's is tracker glitches** (measured lower bound: the steadiest quartile still holds some).
- **The rest still holds fit-independent tracker noise:** at least 0.048° a frame in roll (the tracker's own synthetic self-test, wind.md §1.5, a static scene with no rain) and 0.014° in pitch (the strip check, which compares the horizon with the image itself).

### 2.2 The receiver chain
P's receiver (A, H, L, P) bob-deinterlaces with the line phase flipping per parity (video-feed.md §0), so the picture does not jump half a line; P's pitch shows no peak at the parity pattern's 5 Hz (measured).

### 2.3 The recorder's field pick
- **The mechanism:** the recorder shows the newest whole field at each of its 29.917 Hz ticks, so a frame's instant lags the tracker's uniform clock by 0–20 ms, σ = 20 ms/√12 = 5.8 ms (derived; assumed uniform).
- **Its size in P:** times P's rates (wind.md §2: 3.70, 2.47 and 3.96 °/s), that is 0.021, 0.014 and 0.023° a frame, about 0.018 / 0.012 / 0.020° over 4–15 Hz (derived).
- **In the sim:** the probe's picked frames show the same. In Severe cruise the heading goes from 0.0054° to 0.0187° and the roll from 0.0208° to 0.0222°.

### 2.4 P's real motion over 4–15 Hz (derived)

| | Roll | Pitch | Heading |
|---|---|---|---|
| Glitch-free level, in band | 0.080–0.093° | 0.042–0.045° | — |
| less the tracker's fit-independent noise and the pick | ≤ 0.066–0.082° | 0.038–0.042° | — |
| The part rising above the flat floor (4–8 Hz) | 0.063° | 0.038° | 0.025° |
| **P's real camera motion, 4–15 Hz** | **0.06–0.08°** | **≈ 0.04°** | **≈ 0.025°** (lens ±35 %, wind.md §1.5) |

The blur bound agrees: wind.md §3.3 puts the shake within one exposure at ≤ 0.15°, with an edge width that never bursts.

## 3. The sim's floor, level by level

Measured, `Levels`, 5 seeds a row, 35 s after a 5 s discard, at 35 m. "Frames" is the 29.917 Hz series; "true" the 1 kHz attitude.

| Flight | Air, rotor | Frames 4–15 Hz, roll / pitch / heading | True > 15 Hz, roll / pitch | 1–2 Hz roll / pitch | Spread roll / pitch / yaw | p7e's figure |
|---|---|---|---|---|---|---|
| Hover, Still | 0, 146 Hz | 4.9e-6 / 3.6e-6 / 5.9e-6° | 2.0e-5 / 1.4e-5° | 2.0e-6 / 2.1e-6° | 6e-5 / 1e-4 / 2e-5° | 3.4e-5° |
| Hover, Calm | 1.3 m/s, 147 Hz | 1.6e-5 / 1.6e-5 / 1.8e-5° | 1.8e-5 / 1.6e-5° | 0.0017 / 0.0016° | 0.006 / 0.007 / 0.012° | 3.0e-4° |
| Hover, Windy | 4.3 m/s, 147 Hz | 0.0017 / 0.0026 / 0.0004° | 1.8e-5 / 1.6e-5° | 0.011 / 0.013° | 0.038 / 0.040 / 0.031° | 0.0031° |
| Hover, Severe | 9.0 m/s, 149 Hz | 0.0169 / 0.0117 / 0.0019° | 1.9e-5 / 1.6e-5° | 0.023 / 0.025° | 0.123 / 0.127 / 0.046° | 0.0134° |
| Cruise, Calm | 20.3 m/s, 169 Hz | 0.0018 / 0.0029 / 0.0002° | 4.1e-5 / 1.7e-4° | 0.009 / 0.008° | 0.029 / 0.011 / 0.078° | 0.0029° |
| Cruise, Windy | 20.0 m/s, 169 Hz | 0.0070 / 0.0094 / 0.0013° | 1.5e-4 / 5.6e-4° | 0.035 / 0.025° | 0.118 / 0.036 / 0.283° | 0.0100° |
| Cruise, Severe | 20.2 m/s, 173 Hz | **0.0208 / 0.0209 / 0.0054°** | 4.0e-4 / 1.2e-3° | 0.076 / 0.055° | 0.314 / 0.085 / 0.756° | 0.0245° |
| *P (measured, raw)* | | *0.126 / 0.054 / 0.054°* | *(folded in)* | *0.20 / 0.18°* | *0.51 / 0.28 / 0.44°* | |
| *P (real, §2.4)* | | *0.06–0.08 / 0.04 / 0.025°* | | | | |

- Frames and true agree over 4–15 Hz within 2 % in Severe and 15 % elsewhere (the two Welch windows' leakage), with the frames never above: almost nothing above 15 Hz folds in.
- The cruise's spreads match `p7c-wind` 1.5's model-18 re-grade (T1 0.254°, T2 0.072°, T5 0.72° at 20–50 m), which validates the probe.
- The sim's roll share is 0.51 in cruise at every level; P measured 0.80 over the field (§5).

## 4. The sources, ranked

Measured on Severe, seeds 1–3, frames over 4–15 Hz. As built: cruise 0.0180° roll and 0.0186° pitch, hover 0.0143° and 0.0078°. *Scratch* rows ran through Appendix A's hooks.

| # | Source | Estimate | Measured | Known-wrong that fails | Belongs to |
|---|---|---|---|---|---|
| M1 | **Tracker glitches** (the fit's inlier set and span jump) | — | ≥ 46–59 % of P's roll high-frequency power, ≥ 30–40 % of pitch's (§2) | a fit-independent floor reads ×0.98 across the fit's changes; P reads ×1.4–2.1 | `p7c-wind` (grading) and `p7e` (its target) |
| 1 | **Turbulence, the per-rotor gradient** across the 0.32–0.45 m rotor spacing (PHY-13a) | — | 97 % of roll's power, 87 % of pitch's: cruise 0.0180 → 0.0033° roll, 0.0186 → 0.0068° pitch with every hub at the body's wind; hover 0.0143 → 0.0052° | uniform field (*scratch*); fixed vector 3.5e-5° | `p7c-wind` (built) |
| 2 | **Turbulence intensity from the landscape's roughness** | Davenport–Wieringa class 4 "roughly open", z0 0.10 m (published, recalled); σ_u = 2.4 u* (published, Panofsky and Dutton; in WindPreset) at 35 m gives σ_u/U 0.164, class 5 (z0 0.25) 0.196 (derived) | ×1.26 roll, ×1.22 pitch at every band (0.0263 / 0.0255°, 5 seeds) | — | `p7c-wind` |
| 3 | **Turbulence below the field's 1 m cut**, to the discs' scale | the inertial range continues to the disc's 2D = 0.5 m (derived) | +25 % roll, +5 % pitch (0.0260 / 0.0220°, 5 seeds); to 0.25 m +35 % / +15 % (point sampling below a disc's size overstates it) | — | `p7c-wind` |
| M2 | **Recorder field pick** | rate × 5.8 ms: 0.018 / 0.012 / 0.020° in P (derived) | sim heading 0.0054 → 0.0187°, roll 0.0208 → 0.0222° | the uniform clock | `p7c-wind` (the harness's frames) |
| M3 | **Tracker noise, fit-independent** | — | ≥ 0.048° roll per frame (self-test), 0.014° pitch (strip check) | — | measurement |
| 4 | Gyro noise through the filters and D | ICM-42688-P 2.8 mdps/√Hz (published, TDK; the builder's gyro, fc-profiles.md §1); 0.125 °/s per 4 kHz loop; about 2e-4° over 4–15 Hz at unit loop gain (derived; the loop's gain near its crossover makes it 3×) | Still hover 6.6e-4 / 5.8e-4°; Severe cruise +1 %; ×10 for vibration leaking through the filters (assumed) +6 % | — | none needed |
| 5 | Airframe modes (the elastic turn at the lens) | — | +1 % (rigid airframe 0.0178 / 0.0183°) | rigid airframe | `p7b` / `p7e` (built) |
| 6 | Flapping | — | +0 % roll, +6 % pitch above 4 Hz; it lifts the spreads 14 % roll, 48 % pitch, 13 % yaw | no flapping | `p7b` (built) |
| 7 | Prop unbalance, G 6.3 as built, G 16 and G 40 (out of the bag, assumed) | U·ω² = 0.13 N a rotor at 173 Hz; yaw 2.2e-5° at 1/rev (derived) | 4–15 Hz unchanged at G 40; the 1/rev band 3.0e-5° → 1.0e-4° (G 40), linear in G; it folds to 6.5 Hz at 2e-5° | asymmetry off: 0.0179 / 0.0186° | `p7f-damage`: vibration, not attitude |
| 8 | Blade-pass thrust ripple, 2/rev (arm shadow) | 3–10 % of thrust (assumed, recalled from rotor–airframe studies), 0.35–1.2 N at 290–350 Hz; about 1e-5° (derived) | Still hover 5.9e-6 → 1.1e-5° at 10 %; Severe no change (*scratch*) | — | none (vibration) |
| 9 | Blade-to-blade spread, 1/rev | 1–3 % of thrust (assumed) | ≤ 9e-6° (*scratch*) | — | none (vibration) |
| 10 | Motor torque ripple and ESC commutation | six-step with a sinusoidal back-EMF: 13.4 % peak to peak (derived, 1 − cos 30°); 42/rev = 7.3 kHz; yaw 5e-9°; speed ripple 7e-6 of thrust (derived, rotor 6e-5 kg·m²) | not carried: above the step's 500 Hz Nyquist, and a scratch at 7 kHz would fold falsely | — | none |
| 11 | Cogging | 84/rev (12N14P) = 14.5 kHz | below 1e-9° (derived) | — | none |
| — | H-force 0.25 instead of 0.044 | `p7c-wind` 1.6's known-wrong: the cruise falls to 12.97 m/s | above 4 Hz −28 % roll; spreads +46 % roll, +27 % pitch, +31 % yaw | fails "P's pitch flies the cruise" | keep 0.044 |

The vortex rings and the ground effect change nothing at 35 m (measured).

## 5. The spreads and the yaw

Severe cruise, 5 seeds, measured (*scratch* where the intensity or the cut moves):

| Variant | Spread roll / pitch / yaw | Roll share | 1–2 Hz roll / pitch | 2–4 Hz roll / pitch | 4–15 Hz roll / pitch / heading |
|---|---|---|---|---|---|
| As built (split 0.64) | 0.314 / 0.085 / 0.756° | 0.51 | 0.076 / 0.055° | 0.019 / 0.019° | 0.021 / 0.021 / 0.005° |
| Split 0.80 | 0.398 / 0.073 / 0.425° | 0.72 | 0.077 / 0.049° | 0.020 / 0.019° | 0.021 / 0.021 / 0.005° |
| Split 1.00 (the heading held) | 0.536 / 0.064 / 0.081° | 0.99 | 0.078 / 0.042° | 0.021 / 0.018° | 0.021 / 0.021 / 0.006° |
| σ_u/U 0.164 (z0 0.10) | 0.379 / 0.105 / 0.865° | 0.53 | 0.092 / 0.069° | 0.023 / 0.023° | 0.026 / 0.026 / 0.007° |
| σ_u/U 0.164, split 0.80 | 0.496 / 0.090 / 0.490° | 0.74 | 0.093 / 0.062° | 0.024 / 0.023° | 0.025 / 0.026 / 0.007° |
| σ_u/U 0.164, to 0.5 m, split 0.80 | **0.460 / 0.094 / 0.476°** | 0.74 | 0.096 / 0.064° | 0.028 / 0.024° | **0.033 / 0.027 / 0.007°** |
| *P, field* | *0.51 / 0.28 / 0.44°* | *0.80* | *0.20 / 0.18°* | *0.114 / 0.121°* | *0.06–0.08 / 0.04 / 0.025° (real)* |

- **Yaw's 3×** is the harness pilot's split, not the airframe.
  - At 0.64 the pilot puts (1 − s)/k = 0.93° of heading per degree of tilt change on the heading (k = sin 22.7° = 0.386), against 0.64° on the horizon.
  - Hold the heading (split 1) and the turbulence moves it 0.08°.
  - The harness reads its split back as a roll share of 0.51, where P's field measured 0.80. At split 0.80 it reads 0.72, so P's field needs about 0.85, set from P's measured share as K§8.5 set 0.64 from the belt's.
- **Roll** reaches P's 0.51° with that split and the class-4 roughness: 0.46–0.50°.
- **Pitch stays 3× low** (0.09° against 0.28°), and so do its 1–2 Hz (0.06 against 0.18°) and the 2–4 Hz bands (4–5×). This is P's pitch shelf (wind.md §3.2), which no variant here moves more than 25 %.
  - *The human pilot's remnant* (published model, not run here): the optimal-control pilot model's observation noise of about −20 dB and motor noise of about −25 dB of the signals' variance (Kleinman, Baron and Levison 1970; recalled). It is broadband and largest near the pilot's crossover, about 1 Hz on pitch (τ 0.17 s), where P's shelf sits. The harness pilot has none.
  - *The rear rotors in the front rotors' wake in cruise* (not modelled; `Propulsion.RotorInflow` carries recirculation near the ground only):
    - At P's cruise each wake leaves its disc about 64° from the axis (derived: μ ≈ 0.16, λ ≈ 0.077), so it passes the rear disc, 0.32 m behind, about 0.16 m below the plane: the rear rotors partly ingest it.
    - A vertical gust swings that skew and moves the ingestion: a pitch moment at the gusts' frequencies.
    - Multirotor forward-flight tests and CFD report the rear rotors' thrust loss (recalled, not re-read).
    - *Since built* (`p7b-airframe` 1.3d, model 21): Coleman's skewed wake gives the rear pair 6.2 % under the front at P's cruise (χ 57°, the wake below the rear discs as derived above); Severe's T2 rises 20–23 % and T6c's 1–2 Hz 21–27 %, so pitch stays 2.4–2.6× low. Its task lists what may carry the rest.
- **The H-force:** 0.25 lifts roll's spread 46 %, but it is `p7c-wind` 1.6's known-wrong (the cruise falls to 12.97 m/s). 0.044 stands.

## 6. Recommendations

- **`p7c-wind` (turbulence and the harness):**
  1. Fly the open field's turbulence from the landscape's roughness class (z0 0.10 m, class 4: farmland with tree rows far apart) instead of the stubble's 0.03 m. σ_u/U becomes 0.164 (derived, not tuned), and every turbulence-driven number rises 1.2–1.26×.
  2. Carry the turbulence's shortest wavelength to the discs' 0.5 m (+25 % roll above 4 Hz; the spreads do not move). Below it a hub's point sample overstates what a disc feels.
  3. Fly the harness pilot's split per stretch so that the measured roll share matches P's (about 0.85 for the field's 0.80, 0.64 or what reads 0.64 over the belt). Grade T5 after it: T5 field 0.43–0.48° then passes.
  4. Grade above 4 Hz against P's real motion (§2.4), not its raw floor. T6d stays a ceiling. Add the recorder's field pick to the harness's frames, and keep `floor_check.py` beside `wind_stats.py`.
  5. Pitch's 1–4 Hz needs the human pilot's remnant as a harness item (the optimal-control model's published noise ratios), with the noiseless pilot as its known-wrong. It must not be tuned to pass T2.
- **`p7f-damage`:** the unbalance is vibration. Even G 40 turns the camera 1.4·10⁻⁴° at 1/rev and leaves 4–15 Hz untouched. A chipped or bent blade (PHY-26) shows in `CameraVibration` (the mount's 50–200 Hz band, 0.02–0.06 m/s² at G 6.3) and in the gyro's 1/rev peak. Grade it there, not on the picture's attitude.
- **`p7e-camera-feel`:**
  1. Its §1 table flew the stand-in wind; on `p7c-wind`'s field a Severe hover reads 0.008–0.013° above 5 Hz and a cruise 0.025°.
  2. Its "40–1000×" should read **3–4× roll and 2× pitch over 4–15 Hz, and 3–6× over 1–4 Hz, like for like**.
  3. Its §6 list should drop the unbalance and the blade pass as picture-motion sources: they are `CameraVibration`'s.
  4. The camera at each field's instant (its §2) and the rolling shutter stay right. They show what the pose carries, and what the pose lacks is 1–4 Hz.
- **A new flight-model change:** the rotor–rotor wake interaction in forward flight (the rear rotors' ingestion of the front rotors' skewed wake and its gust sensitivity), for pitch's 1–2 Hz. Gyro noise, blade pass, torque ripple and cogging need no model for the picture.

## 7. Reproduce

```
dotnet test tools/simtests --filter "FullyQualifiedName~MicroMotionProbe"            # the two checks
dotnet test tools/simtests -c Release --filter "FullyQualifiedName=Fpv450.Sim.Tests.MicroMotionProbe.Levels" --logger "console;verbosity=detailed"
dotnet test tools/simtests -c Release --filter "FullyQualifiedName=Fpv450.Sim.Tests.MicroMotionProbe.SourcesTable" --logger "console;verbosity=detailed"
python -m pytest tools/wind/tests/test_floor_check.py
python tools/wind/floor_check.py <P's tracker log>                                    # needs reference/ (local only)
```

## Appendix A: the scratch hooks (not in the tree)

The *scratch* rows came from these temporary edits on `0aeb1a09`, each off at 0. They were reverted before the commit. A static switch board, `Scratch`, held the values; the noise was a 12-uniform sum from a xorshift.
- `FlightCore.Propel`:
  - *the uniform field:* every hub's wind is `_wind.At(s.Position)`
  - *gyro noise:* each 4 kHz loop's gyro reading adds σ·N(0, 1) per axis, σ = 2.8 mdps/√Hz × √2000 = 0.125 °/s, or ×10
- `Propulsion.SubStep`, *the ripple:* each rotor's load on the body (not its inflow state) is `thrust·(1 + a₂·cos(2θ + φ₂) + a₁·cos(θ + φ₁))`, θ the rotor's angle, fixed phases per corner; a₂ 0.03 or 0.10, a₁ 0.01 or 0.03.
- `WindField.PresetFor`:
  - *the cut:* `ShortestWavelength` 0.5 or 0.25 m, with `Modes` scaled to keep the modes per e-fold (138 and 148)
  - *the intensity:* `Intensity` 0.164 or 0.194 (the run used 0.194 for class 5's derived 0.196)
