# The camera's micro-motion near the ground, and clip S's behaviour

For D-064 ("the drone is never perfectly still … especially when lifting off or landing"), PHY-24, PHY-25 and D-028. It measures the camera's turn in the pilot's clips, by flight phase, by axis and by band, so that the low-throttle and near-ground sources can be graded against the footage the way `p7c-wind` grades the wind against clip P. The pilot then named clip **S** "exactly what i expect", so S has its own section (§5).

- **Labels:** *measured* (printed by the tools in §8 from the clips), *derived* (from measured numbers by a stated formula), *published*, *assumed*, *pilot*.
- **OPSEC:** clips by letter and by time inside the clip only. No value was read from the on-screen display; heights and speeds come from the scene (§5.2).
- **Companion notes:** [wind.md](wind.md) (clip P, the horizon tracker) and `hover-micro-motion.md` (on `unity/physics/p7-micro-motion`: the sim's floor, P's tracker floor, the `floor_check.py` method followed here).

## 0. The answer

1. **No clip shows the drone on the ground armed, the first 0.5 s of a liftoff, or a touchdown.** Every clip starts in the air and ends in the air or at a picture loss. Those three phases have no footage target (§6); the pilot is asked for a clip that has them (§7).
2. **What the footage does show near the ground** is a hover at about 1–1.5 m (C, D, E), a slow drift at 0.5–1 m in the rotors' dust (F), a creep at 0.3–0.6 m through grass (H), a shallow descent to the grass (A) and short pushes into weeds and a hollow (D, F). The targets per phase and band are in §6.
3. **The near-ground hover is never still.** In E's 5 s hover at about 1–1.5 m (the cleanest segment: Gaussian, no glitch flags) the camera turns 2.1–2.5 °/s RMS on every axis. Its angle moves 0.19 / 0.38 / 0.27° below 1 Hz, **0.12 / 0.16 / 0.11° at 1–4 Hz** and **0.042 / 0.019 / 0.018° at 4–15 Hz** (roll / pitch / yaw, measured). The sim's Calm hover reads 1.6e-5° at 4–15 Hz (`hover-micro-motion.md` §3), about 1000× less: that gap is D-064.
4. **Lower is livelier.** At 1–4 Hz, roll goes from 0.05–0.17° (C, D, E at 1–1.5 m) to 0.28° in F (0.5–1 m, dust raised) and 0.46° in H (0.3–0.6 m). This is closed loop and includes the pilot, so it is a trend, not a law (§4.3).
5. **Clip S is a steady low cruise.** It flies at about 6–9 m (derived) and 8–14 m/s, with the camera 30–35° below the horizon. It wobbles 0.37 / 0.28 / 0.43° (roll / pitch / yaw, about its 0.6 s trend), corrects within 0.2–0.4 s, and has 4–15 Hz motion of **0.04 / 0.045 / 0.02–0.04°**. That is P's open-field wobble without P's storm (§5).
6. **The recorder shows its frames 40, 40 and 20 ms apart, not 33.** At about 30 frames a second from 50 fields a second, each frame shows the newest field, so the fields advance 2, 2, 1. Measured in every clip that turns enough to show it, this is a line at about 10 Hz proportional to the rate. The tools undo it before any band is read (§2.3). It refines `hover-micro-motion.md` §2.3: the field pick is a fixed 2-2-1 pattern, not random jitter.
7. **A second, independent tracker agrees with P's horizon tracker.** Image registration on P's open field reads 0.065–0.086 / 0.026–0.048 / 0.021–0.033° over 4–15 Hz. `hover-micro-motion.md` §2.4 derived 0.06–0.08 / ≈0.04 / ≈0.025° from the horizon (§3).
8. **No rolling-shutter jello is visible** above 0.2 px from row to row (about 0.01°, E's hover, §2.5). Prop vibration does not reach the picture as a shake: it is `CameraVibration`'s, as D-060 found.

## 1. The clips and segments

Times are inside each clip; frames are 1-based at its own rate. Heights are *assumed* from the scene (the camera's view of grass, weeds and doors; ±50 %) except S's, which are derived (§5.2).

| Letter | Phase | Frames | Time | Height above ground | What it shows |
|---|---|---|---|---|---|
| E | **hover** | f2–150 | 0.0–5.0 s | ≈ 1–1.5 m | a steady hover facing a house front, little drift |
| E | slow advance, nose down | f151–235 | 5.0–7.8 s | ≈ 1–1.5 m | moves toward the door; picture lost at 8.6 s |
| D | **hover** | f2–150 | 0.0–5.0 s | ≈ 1 m | weed tops at the lens, into the sun |
| D | push into weeds | f151–205 | 5.0–6.8 s | ≈ 1 m | forward through vegetation, contact |
| C | **hover** | f2–120 | 0.0–4.0 s | ≈ 1–1.5 m | in a yard before a cellar entrance |
| C | slow advance | f121–180 | 4.0–6.0 s | ≈ 1 m | toward the entrance |
| F | **drift in dust** | f2–210 | 0.0–7.0 s | ≈ 0.5–1 m | inside a tree belt; the downwash raises a dust cloud (ground effect) |
| F | steep descent | f211–245 | 7.0–8.2 s | 1 m → contact | into a round hollow; the picture is lost at about 8.3 s |
| H | **creep** | f2–600 | 0.0–20.0 s | ≈ 0.3–0.6 m | lodged grass at the lens, weaving along a belt edge |
| A | shallow descent | f2–240 | 0.0–8.0 s | high → low | glides down toward a grass meadow at 2–6° (derived, §5.3) |
| A | **last of the descent** | f241–390 | 8.0–13.0 s | a few m above the grass | speed over height rises 1.6 → 4.4 /s; the picture is lost at 14.1 s |
| S | **low cruise** | f2–736 | 0.0–24.6 s | ≈ 6–9 m | the whole clip (§5) |
| P | cruise, open field (cross-check) | f331–673 | 11.0–22.5 s | 20–50 m (K§8.5) | the wind clip, after the belt |

Not used:
- **L** (night, black and white): the picture is too dark and noisy for the matcher, which locks onto the on-screen display. It also starts already climbing at a few metres, with no liftoff in view.
- **B, G:** low and fast, with no hover or ground phase.

## 2. Method

### 2.1 The turn between frames (`tools/reference/track_motion.py`)

Clip P's tracker reads the horizon. Near the ground the skyline is a house, a hedge or grass a metre away, so this tracker reads the whole picture instead:
1. ffmpeg's `vidstabdetect` matches 112 px fields on a grid between consecutive frames, to the whole pixel.
2. Each match is refined to a fraction of a pixel: a parabola through the sum of squared differences at its four neighbours.
3. Both ends are undistorted (k1 0.33, f = 1/√(3 k1) half-widths, as wind.md §1.2) and become rays.
4. A robust fit gives the camera's small turn between the frames plus a forward-motion term: two-point RANSAC, then Tukey's biweight. The turn is roll about the optical axis, pitch and yaw, in the camera's axes. With 0° uptilt (pilot, Q1) these are the body rates the gyro measures. Signs as wind.md §1.2.
5. Camera-fixed fields (the on-screen display, the legs, prop blades in the corners) are masked after a first pass, and the fit runs again.

**Verification (measured, `test_track_motion.py`):** on a synthetic 640 px clip turning by known amounts (0.4° at 5 Hz in roll, 0.3° at 2 Hz in pitch, 6 °/s in yaw), the per-frame error is **0.0015–0.0020° RMS**, or 0.01 px. The checkerboard halves' half-difference reads the same (0.0011–0.0038°), so it is a fair noise estimate. At the clips' 1920 px the fit's own noise is 0.002–0.01° a frame (the "noise" column of §8's output).

### 2.2 The ground's flow (`tools/reference/ground_motion.py`)

A turn fitted alone mistakes the flow of the drone's own travel for a turn (parallax). In S's cruise it read a steady +20 °/s "pitch" that is only the ground streaming past. With the top and bottom of the picture disagreeing by 10–12 °/s, it is useless there.

So for S, A and P the matches are refitted as a camera that turns and moves over a flat ground: b = a + a × ω − (a·G)(e − (a·e)a). Here ω is the turn, e the direction of travel, and G is the travel per frame over the height, times the ground's down direction. Houses and trees above the ground are let go as outliers.

Per frame this gives the turn free of ground parallax, plus:
- speed over height
- the camera's tilt and roll against the ground
- the climb angle and the crab angle

The residual falls from about 5 px to 0.8–1.6 px in S. The test recovers a known motion within 2 % in speed over height, 0.2° in tilt and roll, and 0.4° in climb and crab, with 15 % of the matches on houses and 5 % wild (measured over three cases).

**What it cannot do:** it assumes flat ground. Over a wall or a house front (E, C) the hover's own travel is too small to need it, so those clips use §2.1's fit. Its climb carries a bias of a few degrees over uneven ground (§5.1).

### 2.3 The recorder's field steps

**What it is:**
- The feed is 50 fields a second (PAL); the recorders write 29.84–30.02 frames a second.
- Each frame shows the newest whole field, so it shows field ⌊k·q + φ⌋, with q = 50 / the frame rate ≈ 5/3.
- The fields therefore advance 2, 2, 1, 2, 2, 1 …: the frames' real spacing is **40, 40 and 20 ms**, not 33.

**Measured:** regress each frame's turn on the local rate, separately for the three frame classes (k mod 3). In H, D, S and A the slopes are **1.08–1.40, 0.83–1.40 and 0.58–0.89**: that is 40 / 40 / 20 ms over 33.3 ms (1.2 / 1.2 / 0.6). The same holds on all three axes, as timing must. The short class drifts slowly with the clock mismatch. In P, rain and the picture losses blur the regression.

**What it does:**
- Uncorrected, it adds a line near fs/3 ≈ 10 Hz of the rate times ±6.7 ms. In H's creep that inflated roll over 4–15 Hz from 0.081° to 0.091°, and in D's hover from 0.016° to 0.023°.
- `motion_bands.py` fits (q, φ) per 150 frames and rescales each turn to the frame clock before reading any band. The fit lowers the residual power to ×0.49–0.92 wherever the camera turns enough to show it.

**For the sim:** the harness's frames should be the sim's pose at the field instants ⌊k·q + φ⌋ / 50 s, not a uniform 29.917 Hz clock. This is the deterministic form of `hover-micro-motion.md` §2.3's 0–20 ms field pick; its size is the same, but it is a line, not white noise.

### 2.4 Bands, noise and glitches (`tools/reference/motion_bands.py`)

**Bands:** below 1 Hz (0.4–1), 1–4 Hz and 4–15 Hz.
- Each band's angle RMS comes from a Welch spectrum of the summed turn, less each window's trend (Hann, 64 frames, half overlap; 32 when a segment is shorter than 64; none below 32).
- Each band's rate RMS comes from the turn itself.

**The measurement's parts, in the same bands:**
- the fit's random noise: half the difference of two checkerboard halves of the grid
- the parallax: half the top-minus-bottom difference, which holds the noise too

**floor_check.py's test of the high-frequency floor:** h = x[i] − (x[i−1] + x[i+1])/2 on the angle; its kurtosis and the share of its power in the top 5 % of frames; and its RMS by quartile of the fit's own frame-to-frame change (inlier share, residual, field count). Real motion does not know what the fit did. A top-to-bottom ratio near 1 reads clean; a rise bounds the glitches' share at 1 − (bottom quartile / all)².

**The real motion per band, as hover-micro-motion.md §2.4 bounds P's:**
- *high bound:* the band less the noise
- *low bound:* the band less the noise, less the parallax beyond the noise, and, at 4–15 Hz, scaled by the steadiest quartile's share of h

**The wobble:** as wind.md §1.4, the angle less its 0.6 s trend. Its spread, kurtosis, 1/e time, and events beyond 2σ (rate, length, decay from peak to 1/e).

### 2.5 Jello and shimmer

A rolling shutter shaken faster than the frame rate bends vertical edges, so the fields of one picture row move sideways together against the next row. `track_motion.py` keeps the RMS over rows of each row's median sideways residual (`row_wobble_px`).
- **E's hover:** 0.28 px against a fit residual of 1.0 px over about 25 fields a row. Noise alone gives 1.0/√25 = 0.2 px, so jello is not seen above about 0.2 px, or 0.01° (measured, derived).
- **In the near-ground creeps:** row wobble reaches 0.6–1.7 px (D, F, H), but these rows differ in depth (grass at 0.3 m against trees at 10 m), which is parallax.
- **In the contact sheets:** poles, door frames and roof edges stay straight in every clip.
- **Shimmer** is the feed's analog noise and its sparkle, which [video-feed.md](video-feed.md) covers; it is not attitude.

## 3. Cross-check on clip P

P's open field after the belt (f331–673), rotation-only fit: at 20–50 m the parallax over 4–15 Hz is 0.009–0.012°, below the band. The ground fit is not used here: rain and the picture losses make it heavy-tailed. Measured (registration), against the horizon tracker's derived real motion:

| 4–15 Hz | Roll | Pitch | Yaw / heading |
|---|---|---|---|
| Registration, band as read | 0.086° | 0.048° | 0.034° |
| Registration, real (§2.4 bounds) | **0.065–0.086°** | **0.026–0.048°** | **0.021–0.033°** |
| Horizon tracker, real (`hover-micro-motion.md` §2.4) | 0.06–0.08° | ≈ 0.04° | ≈ 0.025° |
| Registration, 1–4 Hz | 0.25° | 0.19° | 0.16° |
| Horizon tracker, 1–2 and 2–4 Hz combined (wind.md §3.1) | 0.23° | 0.22° | 0.28° (heading) |

**What this shows:**
- The two trackers have different noise: one is white in angle, the other white in the turn, so it is small at high frequency. They agree on P's real motion above 4 Hz.
- It confirms that P's raw 0.126° was half tracker floor, and that the like-for-like gap is 3–4× in roll (D-060).
- Body yaw at 1–4 Hz is below the horizon's heading, because heading also takes the roll times sin 22.7°.

## 4. Near the ground

### 4.1 Per segment

Measured. Rate is the RMS of the frame-to-frame rate, and p95 is the 95th percentile of |rate − mean|. *Real* is §2.4's [low–high], below 1 / 1–4 / 4–15 Hz. *k* is h's kurtosis (3 is Gaussian), and *g* the glitch share's lower bound at 4–15 Hz.

| Segment | Axis | Rate RMS, p95 (°/s) | Real angle (°) | Real rate (°/s) | Wobble (°) | k; g |
|---|---|---|---|---|---|---|
| E hover | roll | 2.4, 4.9 | 0.19 / 0.12 / 0.042 | 0.8 / 1.9 / 1.1 | 0.23 | 3.0; 0 % |
| | pitch | 2.5, 5.0 | 0.38 / 0.16 / 0.019 | 1.6 / 1.5 / 0.7 | 0.35 | 3.0; 2 % |
| | yaw | 2.1, 4.3 | 0.27 / 0.11 / 0.018 | 1.2 / 1.3 / 0.6 | 0.29 | 3.4; 2 % |
| D hover | roll | 2.9, 6.9 | 0.30 / 0.17 / 0.006–0.015 | 1.3 / 1.8 / 0.1–0.5 | 0.39 | 10.3; 68 % |
| | pitch | 4.5, 9.8 | 0.61–0.62 / 0.26 / 0.029–0.032 | 2.1 / 3.2 / 0.9–1.0 | 0.70 | 3.7; 14 % |
| | yaw | 3.0, 5.5 | 0.23–0.24 / 0.13 / 0.006–0.013 | 1.2 / 1.3 / 0.2–0.5 | 0.44 | 15.2; 63 % |
| C hover | roll | 2.3, 4.4 | 0.07–0.14 / 0.05 / 0.017–0.027 | 0.4–0.6 / 0.4–0.5 / 0.6–1.0 | 0.16 | 4.0; 46 % |
| | pitch | 6.2, 10.7 | 1.27 / 0.13 / 0.009–0.017 | 3.8 / 1.6 / 0.3–0.6 | 1.12 | 27; 69 % |
| | yaw | 3.6, 6.5 | 0.50–0.51 / 0.15 / 0.009–0.016 | 1.9 / 1.3 / 0.3–0.6 | 0.49 | 5.0; 53 % |
| F drift in dust | roll | 6.1, 11.8 | 0.87 / 0.28 / 0.033–0.044 | 3.9 / 2.6 / 1.7–2.2 | 0.92 | 56; 73 % |
| | pitch | 2.8, 5.7 | 0.27–0.28 / 0.12 / 0.017–0.034 | 0.9 / 1.3 / 0.8–1.5 | 0.36 | 31; 71 % |
| | yaw | 4.3, 8.3 | 0.67–0.68 / 0.15–0.16 / 0.011–0.026 | 2.7 / 1.2 / 0.5–1.3 | 0.78 | 21; 74 % |
| H creep | roll | 10.2, 20.6 | 1.89–1.91 / 0.46 / 0.054–0.081 | 7.3 / 4.7 / 1.7–2.7 | 1.91 | 6.2; 51 % |
| | pitch | 9.1, 17.6 | 1.78–1.84 / 0.28 / 0.048–0.060 | 6.1–6.4 / 3.5 / 1.8–2.2 | 1.89 | 6.5; 32 % |
| | yaw | 8.4, 17.9 | 1.32–1.45 / 0.22–0.23 / 0.017–0.042 | 5.0–5.4 / 1.9–2.0 / 0.7–1.8 | 1.57 | 12; 70 % |
| A last of descent | roll | 2.3, 4.4 | 0.17–0.18 / 0.12 / 0.025 | 0.7 / 1.6 / 0.7 | 0.27 | 3.8; 0 % |
| | pitch | 2.4, 5.2 | 0.20 / 0.12 / 0.028–0.034 | 0.8 / 1.6 / 0.8–1.0 | 0.31 | 4.1; 33 % |
| | yaw | 1.6, 3.4 | 0.14 / 0.07 / 0.012–0.013 | 0.6 / 0.8 / 0.5 | 0.21 | 3.4; 21 % |
| E advance, nose down | roll / pitch / yaw | 5.4 / 12.3 / 4.1, p95 13.5 / 22.9 / 7.8 | 1–4 Hz 0.10 / 0.20 / 0.20 | 4–15 Hz ≤ 0.6 / 0.7 / 0.5 | — | 6–12; 86–90 % |
| C advance | roll / pitch / yaw | 14.0 / 6.3 / 16.2, p95 26 / 10 / 36 | 1–4 Hz 0.54–0.55 / 0.20 / 0.34 | 4–15 Hz ≤ 2.8 / 1.5 / 1.7 | — | 7–11; 83–91 % |
| D push into weeds | roll / pitch / yaw | 31.1 / 14.7 / 13.3, p95 67 / 27 / 21 | 4–15 Hz 0.16–0.22 / **0.39** / 0.19 | 4–15 Hz 8–11 / **21** / 10 | — | 2.8–10; 0–44 % |
| F steep descent (1.2 s, no spectrum) | roll / pitch / yaw | 33 / 11 / 25, p95 50 / 28 / 51 | — | — | — | 1.7–4.5 |

### 4.2 Confidence

- **High:** E hover and A's last descent. Gaussian h, glitch bound ≤ 33 %, noise below 0.01° in every band, parallax below the band.
- **Medium:** D and C hover. Short (4–5 s); one or two axes carry glitch flags, so their 4–15 Hz is a range. C's pitch below 1 Hz (1.27°) is the pilot pitching to frame the entrance, not wobble.
- **Low:** F's drift (dust and branches fill the picture; k 21–56, glitches ≥ 71 %), H's creep (grass at the lens: parallax, glitches 32–70 %), and every advance, push and descent (2–3 s or less).
- **Every number is closed loop:** the pilot's corrections are in it, most of all below 1 Hz (wind.md §5.1).

### 4.3 What the pilot's words point at

"Microshakings … at lower throttle … especially when lifting off or landing":
- **The near-ground wobble is measured.** At 1–1.5 m, a hover holds 0.05–0.26° at 1–4 Hz and 0.006–0.042° at 4–15 Hz, with a 2–3 °/s RMS rate that never settles (E, D, C). Lower, it grows: F at 0.5–1 m in its own dust reads 0.28° roll at 1–4 Hz, and H at 0.3–0.6 m 0.46°.
  - *Published:* rotors in ground proximity ingest their own recirculating wake. The recirculation has a time scale set by thrust and stand-off height, and gives unsteady loading and thrust fluctuation. Its strength peaks at stand-offs near one rotor radius to a few, and the multirotor's thrust gain or loss there varies between experiments:
    - Dekker et al., *Flow* 2023: https://research.tudelft.nl/en/publications/unsteady-flow-behaviour-of-multi-rotors-in-ground-proximity/
    - a rotor in ground effect whose thrust envelope is modulated and whose rpm fluctuates more (*Acoustics Australia* 2024): https://link.springer.com/10.1007/s40857-024-00327-x
    - Recalled from search summaries, not re-read.
  - F's dust cloud shows that recirculation directly.
- **The liftoff kick and the touchdown jitter are not in the footage** (§6, §7).
  - The nearest contact the footage holds is D's push into weeds, which gives the camera 0.39° and 21 °/s of pitch over 4–15 Hz. That is 10–30× the hover's: vegetation contact (PHY-05) is the largest high-frequency source seen.
  - F's steep descent into the hollow runs at 11–33 °/s RMS on each axis in its last 1.2 s.

## 5. Clip S: the low cruise the pilot expects

### 5.1 What happens, second by second

Derived from the ground fit (§2.2), body rates measured:

| Time | Frames | What | Speed over height | Camera tilt below horizon | Roll against ground | Crab |
|---|---|---|---|---|---|---|
| 0–8 s | f2–240 | straight over an open field, toward the village | 1.4–1.84 /s, rising | 32–35° | −7° → +4°, a slow rock | −11° → +4° |
| 3.3–3.8 s | f99–114 | one quick heading correction | — | — | — | — |
| 8–14 s | f241–420 | over the village edge (rubble, gardens, a road crossing) | 1.29–1.67 /s | 30–33° | −2.4° to +1.4° | +2° → +14° |
| 14–24.6 s | f421–736 | along the village road, slower | 1.08–1.21 /s | 31–34° | −1.9° to +2.9° | +1° to +11° |

The heading correction is −6.3° of yaw in 0.5 s, peaking at 40 °/s for one frame, with −1.4° of roll riding on it.

- **The flight is straight and nearly level.** There are no turns beyond small heading corrections (body yaw rate means −0.7 to +0.6 °/s per segment).
  - The fit's climb reads −6° to +4°, with a median of −2°.
  - Two height reads 13 s apart agree within 10 % (§5.2), so the −2° is the fit's bias over uneven ground, not a descent (derived). Read the climb as level within ±3°.
- **It slows down** from about 12–14 m/s over the field to 8–9 m/s along the road (§5.2).
- **The crab of up to 14°** is the drone's travel to the right of its nose: a crosswind from the left, or the pilot's heading lagging the road. The crab grows as the speed drops, as a steady crosswind would make it (derived).
- **The camera's tilt of 30–35°** is the body's pitch if S's camera has 0° uptilt.
  - That is steeper than P's −22.7° at about 20 m/s, for less speed.
  - So either S's quad has more drag or load, or its camera is tilted down on its frame. Pilot question 2 (§7).
  - The skyline in S's stills sits about 29° above the image centre after undistortion, which agrees with the fit's 31° within the skyline's own offset.

### 5.2 Height and speed

The village road is the scale. Its edges, read along rows of the still at 22.53 s and along columns of the still at 9.51 s, were mapped onto the fitted ground plane (`ground_motion.to_ground`).
- **The road's width in heights:** 0.73 ± 0.05 at 22.5 s (the two edges parallel within 2°, a check on the plane) and 0.79 ± 0.13 at 9.5 s (measured; ± is the tilt ±2°).
- **The road's width:** 4.5–6.5 m (*assumed*, a village asphalt road; terrain.md's road class).
- **Height** (*derived*): **6–9 m**, about 7.5 m, the same at both times within the reading's error.
- **Speed** (*derived*, speed over height × height):

| Segment | Speed over height | Speed |
|---|---|---|
| field, 0–8 s | 1.4–1.84 /s | **10–14 m/s** (7.5–17 at the height's limits) |
| village, 8–14 s | 1.29–1.67 /s | **10–12.5 m/s** |
| road, 14–24.6 s | 1.08–1.21 /s | **8–9 m/s** (6.5–11) |

### 5.3 Attitude by band

Measured from the ground fit; real per §2.4. The field steps were undone (residual ×0.88–0.96: S turns gently, so the pattern is weak).

| Segment | Axis | Rate RMS, p95 (°/s) | Real angle, < 1 / 1–4 / 4–15 Hz (°) | Real rate (°/s) | Wobble (°) | 1/e time | Events beyond 2σ | k; g |
|---|---|---|---|---|---|---|---|---|
| **whole** | roll | 3.1, 6.5 | 0.31 / 0.17 / 0.038–0.046 | 1.3 / 2.0 / 1.2–1.6 | 0.37 | 0.27 s | 10/min, 0.25 s long, decay 0.20 s | 7.7; 15 % |
| | pitch | 2.4, 5.4 | 0.27 / 0.12 / 0.044–0.048 | 1.1 / 1.5 / 1.5–1.7 | 0.28 | 0.30 s | 18/min, 0.10 s, 0.17 s | 5.5; 12 % |
| | yaw | 3.1, 4.7 | 0.32 / 0.17 / 0.018–0.036 | 1.4 / 1.8 / 0.8–1.6 | 0.43 | 0.37 s | 8/min, 0.64 s, 0.40 s | 45; 56 % |
| field | roll | 3.0, 5.6 | 0.28–0.29 / 0.14 / 0.041–0.048 | 1.1 / 1.7 / 1.4–1.7 | 0.29 | 0.23 s | | 5.2; 16 % |
| | pitch | 2.9, 6.1 | 0.40 / 0.15 / 0.055–0.057 | 1.6 / 1.7 / 1.9–2.0 | 0.37 | 0.27 s | | 5.5; 1 % |
| | yaw | 4.4, 7.7 | 0.51 / 0.21 / 0.032–0.051 | 2.1 / 2.2 / 1.6–2.5 | 0.70 | 0.47 s | (the 3.3 s correction) | 30; 46 % |
| village | roll | 3.2, 6.6 | 0.16–0.21 / 0.19 / 0.045–0.052 | 1.1 / 2.2 / 1.6–1.9 | 0.29 | 0.17 s | | 8.1; 13 % |
| | pitch | 2.2, 4.8 | 0.18 / 0.10 / 0.039–0.044 | 0.8 / 1.1 / 1.3–1.5 | 0.21 | 0.23 s | | 3.6; 14 % |
| | yaw | 2.0, 4.2 | 0.16–0.17 / 0.14 / 0.021–0.030 | 0.8 / 1.6 / 0.7–1.1 | 0.22 | 0.20 s | | 4.5; 38 % |
| road | roll | 2.8, 6.4 | 0.25 / 0.19 / 0.033–0.042 | 1.2 / 2.2 / 1.0–1.4 | 0.30 | 0.17 s | 32/min, 0.10 s, 0.17 s | 8.2; 29 % |
| | pitch | 2.2, 4.5 | 0.18 / 0.10 / 0.040–0.044 | 0.8 / 1.3 / 1.4–1.6 | 0.22 | 0.30 s | | 4.9; 14 % |
| | yaw | 2.1, 4.2 | 0.19 / 0.16 / 0.021–0.027 | 1.0 / 1.7 / 0.7–1.0 | 0.23 | 0.17 s | 32/min, 0.13 s, 0.17 s | 6.1; 26 % |

### 5.4 S's behaviour, in words

- **It never sits still,** even in straight, level, unhurried flight. The camera turns at 2–3 °/s RMS on every axis all the time, and its wobble about the 0.6 s trend is 0.2–0.4°, near Gaussian (kurtosis 2.2–4.0 on roll and pitch). P's open field in a storm read 0.51 / 0.28 / 0.44°; S reads 0.37 / 0.28 / 0.43° over the whole clip and 0.22–0.30° along the road.
- **The twitches are short and settle fast.** Excursions beyond 2σ come 8–35 a minute per axis, last 0.1–0.25 s (yaw's 0.4–0.64 s) and decay to 1/e in 0.15–0.4 s. The wobble's correlation time is 0.17–0.30 s (roll, pitch) and 0.17–0.47 s (yaw). It is a quick, springy correction, never a slow drift back: about P's measured pilot-and-loop decay of 0.17–0.30 s (K§8.2).
- **It shakes at 4–15 Hz at 0.04–0.05° in roll and pitch, and 0.02–0.04° in yaw.** That is about P's real high-frequency motion (§3) in calmer air, 7.5 m up and at half P's speed. It is 2–2.5× the sim's Severe cruise above 4 Hz in roll and pitch, and over 1000× its Calm hover (`hover-micro-motion.md` §3).
- **Yaw is the odd axis.** It is mostly smooth, with rare quick heading corrections (kurtosis 45 over the whole clip from the single 0.5 s, 6° correction at 3.3 s). Those are pilot inputs: the stick's step and the loop's reply.
- **Gust response cannot be separated from the pilot here.** There is no wind measure in S. The crab angle (§5.1) is the only sign of wind, and it changes slowly (tens of seconds), not in gusts. Over the village edge, roll's 1–4 Hz rises from 0.14° to 0.19° while its band below 1 Hz falls: a livelier, shorter-period wobble over the rough village edge and its gardens than over the open field (measured; the cause, eddies off houses and trees or the pilot's busier hands, is not separable).
- **Vibration signature:** no jello or skew is seen (§2.5), and the feed's noise is not attitude.

## 6. Targets for the physics

For PHY-24/25 and D-064's grading: the sim's camera, sampled at the field instants of §2.3 and run through `motion_bands.py`, should read inside these. The ranges span the clips; the strongest clip is in **bold**.

The pilot's part is in every number: grade with the harness pilot (K§8.5), with its remnant as `hover-micro-motion.md` §5 names it, and never tune to pass.

| Phase | Axis | Rate RMS (°/s) | < 1 Hz (°) | 1–4 Hz (°) | 4–15 Hz (°) | Confidence |
|---|---|---|---|---|---|---|
| On the ground, armed | all | no footage | — | — | — | — (pilot Q1, §7) |
| First 0.5 s of liftoff | all | no footage | — | — | — | — (pilot Q1) |
| Hover at 1–1.5 m (C, D, **E**) | roll | 2.3–2.9 | 0.07–0.31 | 0.05–0.17 | 0.006–0.042 (**0.042**) | high (E), medium (C, D) |
| | pitch | 2.5–6.2 | 0.38–0.62 (C's 1.27 is framing, §4.2) | 0.13–0.26 | 0.009–0.032 (**0.019**) | |
| | yaw | 2.1–3.6 | 0.23–0.51 | 0.11–0.15 | 0.006–0.018 (**0.018**) | |
| Drift at 0.5–1 m in own dust (F) | roll / pitch / yaw | 6.1 / 2.8 / 4.3 | 0.87 / 0.28 / 0.67 | 0.28 / 0.12 / 0.15 | 0.033–0.044 / 0.017–0.034 / 0.011–0.026 | low |
| Creep at 0.3–0.6 m (H) | roll / pitch / yaw | 10.2 / 9.1 / 8.4 | 1.9 / 1.8 / 1.3–1.5 | 0.46 / 0.28 / 0.22 | 0.054–0.081 / 0.048–0.060 / 0.017–0.042 | low |
| Descent, last metres (**A**) | roll / pitch / yaw | 2.3 / 2.4 / 1.6 | 0.17 / 0.20 / 0.14 | 0.12 / 0.12 / 0.07 | 0.025 / 0.028–0.034 / 0.012 | high |
| Touchdown | all | no footage | — | — | — | — (pilot Q1) |
| Contact with weeds (D push) | roll / pitch / yaw | 31 / 15 / 13 | — | 0.84 / 0.55 / 0.36 | 0.16–0.22 / 0.39 / 0.19 | low (1.8 s) |
| Low cruise at 6–9 m, 8–14 m/s (**S**) | roll | 2.8–3.2 | 0.16–0.31 | 0.14–0.19 | 0.033–0.052 | high |
| | pitch | 2.2–2.9 | 0.18–0.40 | 0.10–0.15 | 0.039–0.057 | high |
| | yaw | 2.0–4.4 | 0.16–0.51 | 0.14–0.21 | 0.018–0.051 | medium (pilot steps) |
| S's wobble and settling | per axis | wobble 0.22–0.43° (roll, pitch, yaw over the whole clip 0.37 / 0.28 / 0.43) | 1/e 0.17–0.47 s | events 8–35/min, 0.1–0.64 s long | decay 0.15–0.4 s | high |
| Cruise at 20–50 m in Severe (P, cross-check) | roll / pitch / yaw | — | — | 0.25 / 0.19 / 0.16 | 0.065–0.086 / 0.026–0.048 / 0.021–0.033 | high |

**What each phase asks of the sources D-064 names:**
- **Hover at 1–1.5 m:** 2–3 °/s RMS on every axis with no wind to speak of, and 0.02–0.04° above 4 Hz. Each rotor's own thrust unsteadiness, its motor and ESC dynamics, and the ground effect's unsteady part must reach this together.
  - *Known-wrong:* a perfectly steady rotor. The sim's Calm hover reads 1.6e-5° above 4 Hz and 0.002° at 1–2 Hz (`hover-micro-motion.md` §3).
- **Lower than 1 m:** it should grow, 1.5–3× at 1–4 Hz from 1.5 m to 0.5 m (F, H against E). The ground effect's recirculation is the named source. The measured trend is low-confidence, so grade the direction, not the ratio.
- **Low cruise (S):** the turbulence's per-rotor gradient (D-060) at the class-4 roughness, plus the pilot's remnant, must give S's 0.04–0.05° above 4 Hz and its 0.2–0.4° wobble at 6–9 m.
- **Contact:** a weed or a stem against the props or legs (PHY-05) is a 10–30× jolt above 4 Hz (D's push). It must come from the stem's mechanics, never as noise on the pose.

## 7. Gaps and pilot questions

**Pilot-only questions** (for the orchestrator's `docs/pilot-questions.md`):
1. **A recording with the arming on the ground, a liftoff, a near-ground hover and a landing,** from any of the pilot's flights. It may be cropped to those seconds, cited by a new letter only. None of the 11 clips has these three phases. A 10–20 s stretch of each would let §6's three empty rows be measured the same way.
2. **S's camera tilt on its frame.** Is it the same 0° uptilt as the pilot's own quad (Q1), or tilted down? This decides whether S's 30–35° is the body's pitch, and so how much drag the cruise needs.

**Gaps:**
- **No gust measure in S:** its wind can only be read from the crab (§5.1).
- **Heights in C, D, E, F and H** are scene impressions (±50 %). A door or a person-height cue in a hover clip could fix E's to ±20 %, if the physics needs it.
- **L (night)** could be tracked with the on-screen display masked by hand. It holds only a climb from a few metres, so it was not worth it here.

## 8. Reproduce

```
python tools/reference/track_motion.py <clip under reference/> --letter S --fields            # reference/_frames/S/motion.csv (+ _fields)
python tools/reference/ground_motion.py --letter S --tilt 30                                   # reference/_frames/S/ground.csv
python tools/reference/motion_bands.py --csv reference/_frames/S/ground.csv --segment whole=2-736 --segment field=2-240 --segment village=241-420 --segment road=421-736
python tools/reference/motion_bands.py --letter E --segment hover=2-150 --segment approach=151-235
python tools/reference/motion_bands.py --letter D --segment hover=2-150 --segment push=151-205
python tools/reference/motion_bands.py --letter C --segment hover=2-120 --segment approach=121-180
python tools/reference/motion_bands.py --letter F --segment drift=2-210 --segment descent=211-245
python tools/reference/motion_bands.py --letter H --segment creep=2-600
python tools/reference/ground_motion.py --letter A --tilt 20
python tools/reference/motion_bands.py --csv reference/_frames/A/ground.csv --segment glide=2-240 --segment low=241-390
python tools/reference/motion_bands.py --letter P --segment field=331-673                         # §3
python -m pytest tools/reference                                                               # synthetic only; no test opens reference/
```

The clip's path is passed on the command line and never written: every output is under the git-ignored `reference/_frames/<letter>/` and names only the letter.
