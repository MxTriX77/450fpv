# Video feed reference notes

The pilot's goggles feed **is** the target look. These notes break it down trait by trait: what the footage shows, how we would render it, and which part of the simulation drives it (D-008: the feed is simulated, not filtered).

- **Sources:** 9 flight clips **A–H and L** (the 6 photos I–K and M–O are not feed footage and are not used here). Cited by letter and frame number (`B f123` = clip B, frame 123, 1-based, about 29.9 frames per second; L exactly 30). Clip L is a night flight. Clip P, a 47-s flight in severe wind and rain that belongs to the wind study, is cited here only for two feed events: N12 and N13.
- **Evidence:** `metrics.csv` for every frame, the lossless native PNG bursts and event frames, and read-only full-resolution scans of every frame (thin-line, diagonal-interference and chroma-impulse detectors). Nothing was re-extracted and nothing derived from the footage is in the repo.
- **Units:** pixel positions and sizes are on the 1920×1080 recording. "Levels" are 8-bit code values (0–255). `r/g` and `b/g` are the ratios of the frame-mean channels (a white-balance proxy).
- **OPSEC:** no places, positions, dates, file names or on-screen-display (OSD) values. The OSD is described only as a layout. Frame content is described only as far as the optics and exposure need it.
- **Owner:** tech-artist. This is the input to the analog-feed implementation (`game/src/video/`) and to a later physics↔video interface change.

## 0. The signal chain, and where each artifact can come from

The drones are fiber-optic, so there is no radio path, and no trait below is put down to RF breakup. The chain, as the footage lets us infer it:

```
scene → lens → camera sensor + DSP (auto exposure, auto white balance, edge sharpening)
      → composite video (PAL timing, 50 fields/s) → flight-controller OSD insert
      → fiber transmitter → fiber → fiber receiver
      → goggles/receiver (sync, no-signal screen, deinterlace + scale to HD)
      → recorder (≈30 fps, slightly variable rate) → messenger re-encode (H.264)
```

Evidence for the less obvious links:
- **PAL timing.** The picture is built from ≈286 lines per field (§2 P1), and the OSD is a 30×16 character grid (§5). Both are PAL figures; NTSC would give 240 lines and 13 OSD rows.
- **The recording shows fields, not frames.** Each 1080p frame carries only one field's worth of lines, scaled up. On the A/H receiver the line phase flips by half a line in runs of 2–3 frames (H f365–372), which is 50 fields/s bob-deinterlaced and sampled at about 30 fps. The B–G receiver shows the same field-line count without a clear parity flip (B f186–193 phase moves < 0.4 px), so it probably shows one field parity or a fixed blend.
- **Two receiver/recorder set-ups.**
  - A, H, L and P: bright pure-blue no-signal screen (mean B ≈245–255) with small centred text, ≈275 lines per picture height, 1-px black top border. L never loses picture; its border and line period (3.92–3.93 px, as in A and H) place it here. P's blue-outs show this screen. L is recorded at exactly 30.000 fps, the others at 29.84–29.97.
  - B–G: deeper blue no-signal screen (mean B ≈187–205, G ≈10–12; seen in B–F, since G never loses picture) with two short text lines top-left, ≈286 lines, 4-px black top border.
- **Three airframe/camera set-ups,** recognisable by the OSD layout and the parts in view: A; B and G; C, D, E, F, H, L and P.
- **Re-encode.** The messenger H.264 re-encode leaves a weak but frame-aligned block grid in every clip (§2 P10). Only traits with that block-aligned evidence are classed as re-encoding.

Each trait below names its most likely stage: **cam** (sensor/DSP), **cvbs** (composite encoding/decoding), **pwr** (power-rail or ESC interference), **link** (fiber TX/RX), **rx** (goggles/receiver behaviour), **rec** (recorder) or **enc** (re-encode, not to be simulated).

## 1. Noise events

### 1.1 Footage totals and selection bias

| | Frames | Seconds |
|---|---|---|
| All clips (A–H, L) | 3226 | 107.8 |
| Picture (not blue, not snow) | 2579 | 86.2 |
| **In-flight window** = picture minus the last 1.0 s before each loss | 2369 | 79.1 (1.32 min) |
| In-flight window, A–H only (the clips the full-resolution N2–N4 scans cover) | 2159 | 72.1 (1.20 min) |
| Pre-loss windows (last 1.0 s before loss, 7 clips) | 210 | 7.0 |

**Bias.** 7 of 9 clips (A–F, H; G and L never lose picture) end in loss of picture, and every one of those losses comes right after a close approach, landing or contact (A: low over grass; B: frame filled by a close object; C, E: flying into a doorway; D: contact with a stone structure; F: descent onto a ground surface; H: settling into grass). The clips were cut around the end of each flight, so:
- loss sequences are over-represented by roughly **one per clip** rather than a rate in time. They are modelled as **event-driven** (impact, power loss, fiber break), not as a random rate.
- **in-flight rates are from only 1.2–1.3 minutes** of footage from three airframe groups. A trait seen once gives a rate of about 1/min with a very wide error (one event in 1.2 min is consistent with anything from about 0.05 to 4 per minute at 90 %). Rates below are what was seen, not a stable statistic. The sim should expose them as tunables.
- two traits cluster in single clips (diagonal interference only in B, rolling lines only in C). Pooled rates dilute them and per-clip rates inflate them. Both are given.
- one trait (N13) appears only in dim light: in L (7 s at night) and in P (47 s under heavy overcast). Its rate comes from those two clips alone.
- N12 is measured only in clip P (47 s of severe wind and rain), so its rate reflects those conditions.

### 1.2 Event catalogue

Rates are per minute of the in-flight window unless marked. N2–N4 use the A–H window (1.20 min), because the full-resolution scans cover A–H only. L's 52 lossless frames were spot-checked for them instead (see each entry). "Duration" is in recorded frames (≈30 fps). One recorded frame ≈ 1.67 fields.

| ID | Event | Clips / frames | Count | Rate | Duration | Stage |
|---|---|---|---|---|---|---|
| N1 | Always-on baseline grain | all, continuous; heaviest in L | — | continuous | every frame, new pattern each field; never clean | cam, cvbs |
| N2 | Chroma sparkles (coloured impulse dashes) | all; densest H, F, C; near zero A, E (§2 P6) | continuous | continuous background; no bursts beyond content changes | per frame | cvbs/link |
| N3 | Rolling thin dark lines ("stripes") | C only: f3–4, f9–11, f30–53, f80–91 | 4 events: 38 detected frames within runs spanning 41 | 3.3/min pooled (A–H); 50/min within C | 2, 3, 24 and 12 frames (0.07–0.8 s) | pwr |
| N4 | Diagonal dotted interference, flickering | B only: f1–269 | 79 runs (125 frames) | 66 runs/min pooled (A–H); ≈ 510 runs/min (≈ 8.5/s) within B; 45 % of B's in-flight frames | runs of 1–3 frames (max 7), each covering part of a field | pwr |
| N5 | Staged loss, stage 1: partial-frame snow (pilot: "very quick picture glitch") | C f174–175, D f208–209, F f250 | 3 | per loss event | 1–2 frames | rx (or rec) |
| N6 | Staged loss, stages 2–3: blue interlude ("brief flashy moment"), then full snow ("noisy and flashy") | C f176–186, D f210–221, F f251–262 | 3 | per loss event | blue exactly 4 frames → full snow 7–8 frames | rx |
| N7 | Blue no-signal screen ("finally blue"), straight after picture (hard cut) or after N6 | hard cut: A f424, B f310, E f263, H f629; after N6: C f187, D f222, F f263 | 7 | per loss event | permanent to clip end (52–108 frames, 1.7–3.6 s); never recovers in A–H (P's short blue-outs that recover are N12) | rx |
| N8 | H-sync tearing, bottom of frame | H f628 | 1 | pre-loss only | 1 frame (the last picture frame) | link/rx |
| N9 | Frame stutter (duplicate frames) | A–H, clustered; densest E f249–262 (pre-loss); none in L | 29 repeated frames (1.2 % of A–H picture frames) in 15 groups | 11 groups per minute of A–H picture (5.3/min for groups of ≥ 2 repeats) | 1 frame each; a group is 1–5 repeats spaced 2–3 frames apart | rec (uncertain, U3) |
| N10 | Macroblock smear | B f306–309 (pre-loss) | 1 | — | 4 frames | **enc** |
| N11 | Exposure jumps (sun occluded / revealed) | D f183 | 1 | frame-content driven | 1 frame (−13 % mean luma) | cam |
| N12 | Mid-flight dropouts and flashes that recover | P only (severe wind and rain): f676–687, f951–958, f966–985, f1144–1164; also the pilot (U1) | 4 in P; 0 in A–H and L | ≈ 5/min in P; 0 in 1.32 min of the other clips | blue-outs 12–20 frames (0.40–0.67 s); black dropout ≈ 2 frames; precursors 1–3 frames | link/rx |
| N13 | Level steps with one-frame dark dips (dim light) | L: f14, f80–81, f115, f163–164, f200–201 (all 9 of L's flags); P: e.g. f139–141, f176, f372, f437 | L: 5 steps, 4 with a dip; P: 30 dips and 12 plain steps | 43/min in L (steps 1.1–2.2 s apart); ≈ 55/min in P; 0 in A–H | dip 1 frame, then the new level holds | uncertain: cam, pwr or link (U14) |

The pilot's words, mapped to what was measured:
- "baseline noise is always present" → N1 (the feed is never a clean picture; heaviest at night, L).
- "sometimes noise, sometimes stripes, every instance differs" → N1–N4 and N12 (procedural variety, never a repeating texture).
- "mid-flight flashes that recover" → N12, measured in P: brightening bursts of impulse noise, a short black dropout and three blue-outs, all of which recover. A–H and L show none.
- "noise may depend on throttle, but rarely" → motor current is only a weak modulator of N1–N4, never their main trigger.
- "very quick picture glitch → brief flashy moment → noisy and flashy → finally blue" → the staged loss of C, D and F, stage by stage (N5–N7).

### 1.3 Event detail

**N1 Live grain (always-on baseline noise).** Fine luma and chroma noise over the whole picture.
- **Pilot:** baseline noise is **always present** on analog video. The feed is never a clean picture. It varies continuously across instances: sometimes a little noise, sometimes stripes, never a repeating pattern.
- **Measured.** Grain is elongated horizontally: half-correlation length 3–6 px horizontally and 2–3 px vertically, i.e. about one source sample by one field line.
- **Re-encode masks it.** In flat bright sky the recordings keep only σ ≈ 0.9–1.3 levels luma (≈ 1.5–3 levels chroma), and the residual is 60–97 % correlated from frame to frame (B f2–4, G f1–3; it drops to 9 % at G f3 → f4). A live analog grain would be uncorrelated, so the encoder has most likely flattened and frozen it.
- **Low light (L).** At night the grain is the strongest in the set. In static sky (L f4/5, f102/103, f161/162, f165/166, f202/203; luma minus its 5 × 5 box mean) σ ≈ 2.0–2.7 levels, or 2.0–3.6 % of the sky level, and it changes from frame to frame (correlation 0.15–0.35). The same measure on flat day sky gives 0.5–1.0 levels, or 0.3–0.6 % (B f2–4, f186–191; G f1–4). So the recorded night grain is ≈ 2–5× the day level, and ≈ 3–14× relative to the picture level. The `noise` column of `metrics.csv` is not a grain measure: it is a whole-frame texture metric at 25 % scale, and L's mean (8.4) is the lowest of the nine clips (A–H picture frames: 11.2–25.8), because the dark scene has little texture. See also C5.
- **Candidate effect.** Per-field noise, low-passed horizontally to about one source sample and independent per field line. It is always on. Luma σ is tunable, starting at 2–4 levels at normal exposure (above the re-encoded ≈ 1, since the encode suppresses it; U4), with chroma noise of similar amplitude but about 5× wider horizontally. σ scales with the AE gain, so that at night it reaches ≈ 2–5× its day level. A new seed each field; never a static or looping texture.
- **Driver.** Always on (the analog floor). Its level follows camera gain from the auto-exposure loop (**light level**). **Motor current** adds a weak, occasional term (power-rail ripple; the pilot says throttle coupling is rare).

**N2 Chroma sparkles.** Isolated coloured dashes, one field line tall and 3–10 px long, in red, green, magenta and cyan, scattered over textured areas (B f122 ground; E, H grass and walls).
- **Measured.** A full-resolution scan of every picture frame counted pixels whose chroma departs more than 45 levels from its 7×7 local mean.
  - Per-clip median, in pixels per thousand: A 0.00, E 0.00, D 0.04, B 0.07, G 0.14, C 0.21, F 0.27, H 2.98.
  - Frame-to-frame variation is small (90th percentile ≤ 3× the median).
  - The only jumps are F f5–6 and G f112–118, and both are content changes (G ends diving into vegetation).
  - Density follows fine texture (H's grass), so this detector partly counts P5 cross-colour. Softer lilac specks on E's walls fall under the threshold.
  - L was not in the scan. Its colour is killed (C5: per-pixel chroma median 1.1–1.4 levels), and 4 of its lossless frames (f1, f102, f165, f203) have no chroma impulse above 45 levels. So N2 is absent at night.
- **Candidate effect.** Sparse random impulses in chroma (and some in luma), each 1 line × 1–3 source samples, placed per field. The density scales with the amount of fine detail in the frame plus a link-margin term.
- **Driver.** **Link margin** from fiber tension and bend (optical power near the receiver threshold adds impulse noise), plus **motor current** (ESC switching spikes). Free-running random at the measured density is the fallback.

**N3 Rolling thin dark lines.** Thin, perfectly horizontal dark lines across the full width, evenly spaced down the whole picture (C f37: 5 lines).
- **Size.** Each is 2–5 px tall (one field line) and 8–30 levels darker than the rows around it.
- **Spacing and frequency.** 150–157 px apart, which is ≈ 40 field lines, or ≈ 2.5–2.6 ms. That is a periodic transient at ≈ 380–395 Hz, not locked to the field rate.
- **Roll behaviour.**
  - The pattern rolls downward by ≈ 14 px per recorded frame through C f30–44.
  - It stands almost still through f44–53 (± 10 px).
  - It rolls faster, ≈ 30–60 px per frame, through f84–91, while the spacing widens from 152 to 157 px (the frequency drops ≈ 3 %).

  So the source frequency varies during the flight, as a motor-speed-linked source would.
- **Where it occurs.** Found by a full-resolution scan of every picture frame of A–H: 3 or more lines at uniform 140–170 px spacing reaching the upper half of the picture.
  - C: 4 events spanning 41 frames. 38 of them are detected (2, 3, 23 and 10 per run; 27 % of C's 143 in-flight frames). The other 3 sit in gaps of at most 2 frames inside a run, which the event grouping bridges.
  - Every other clip: 0. A single-frame match in H f400 shares a row with the OSD and is not counted.
  - The per-frame metrics flagged only C f37, because the lines are thin against C's busy scene.
  - L was not in the scan. A row-profile check of its 52 lossless frames for dark lines at 140–170 px spacing found none.
- **Candidate effect.** A set of thin dark lines, 1 source line tall and 15–30 levels deep, over the whole width. Spacing = field height × field rate ÷ interference frequency; position rolls each field by the frequency's offset from a multiple of the field rate.
- **Driver.** Whether a flight has it at all is a per-flight random draw (1 of 3 airframe groups here: C only). Within such a flight the onsets are free-running random at C's measured rate (≈ 50/min, runs of 2–24 frames), because the pilot rules out throttle as the main trigger. **Motor electrical frequency** (from motor RPM × pole pairs, or ESC switching harmonics) sets the spacing and roll speed while lines are shown. **Motor current** modulates visibility only weakly (the pilot says throttle coupling is rare). U8 is still open.

**N4 Diagonal dotted interference, flickering.** First seen at B f123 and B f131, the only two frames the per-frame metrics flagged. A sky-region scan of every frame of A, B and G (the clips with open sky) shows it is **not rare in B**:
- **Criterion.** At native 1920×1080, in one fixed open-sky box per clip (A 1100×70 px at x 300, y 100; B 950×190 at 450, 150; G 500×100 at 950, 170), BT.601 luma minus its 5×5 box mean is autocorrelated at (+8 px, +4 rows) and at (−8 px, +4 rows), each normalised by its variance. A frame counts when the two correlations differ by more than 0.2 (either sign) and the high-pass RMS is above 1.2 levels; consecutive counted frames form a run.
- **B.** Present in 125 of B's 279 in-flight frames (45 %), from f1 to f269. It comes in 79 runs, mostly 1 frame long (53 runs; 12 of 2 frames, 11 of 3, 2 of 4, 1 of 7) and mostly separated by a single clean frame (51 of 78 gaps). Examples: B f5–6 show it and f4 is clean.
- **A and G.** None. G is the same airframe group as B, so the source is specific to B's drone or flight. A's four candidate frames were prop-blade slivers.
- **L.** Not in the scan. The same criterion on its 52 lossless frames (sky box 900×200 px at x 500, y 130) finds none.

Details:
- **What it looks like.** About 10 parallel lines falling left to right at a slope of about 0.45–0.5. In f123 they cover only the top 28 % of the picture and in f131 the top 44 %, so each burst lasts only part of one field (≈ 5–8 ms).
- **Timing.** Mostly alternate recorded frames, which suggests a source near half the field rate, or one gated to one field in two, sampled by the ≈ 30 fps recorder.
- **At 4× zoom.** Each line is a staircase of short dashes, one per field line, stepping ≈ 8 px right per line. The dashes alternate blue/white with yellow/green fringes, which is interference near the colour subcarrier beating against the line rate.
- **Visibility.** Only clearly visible over flat sky, but present over the ground too. So in clips without open sky (C–F, H) it cannot be ruled out.
- **Candidate effect.** A sinusoid added to the composite signal before decoding. Its frequency is near a multiple of the line rate plus an offset, which makes the diagonal and, through the chroma decoder, the colour dots. It is gated to a vertical span of 20–50 % of a field, on roughly every other field with random dropouts.
- **Driver.** An **airframe-specific on-board interference source.** Whether a flight has it is a per-flight random draw (1 of 3 airframe groups here: B only). Within such a flight the bursts are free-running random at B's measured statistics (runs of 1–3 frames, ≈ 8.5 runs/s, present in 45 % of frames). **Motor current** and its steps modulate amplitude and burst onset only weakly (the pilot says throttle coupling is rare). What B's drone carried is still open (U7).

**N5–N7 Loss sequences (end of flight, fiber break).** Seven losses, in two variants.

1. **Staged** (C, D, F). All three have the same four stages:
   1. **Partial snow, 1–2 frames** (C f174–175, D f208–209, F f250).
      - Snow replaces the picture down to a horizontal split line, and the previous picture survives below it: the bottom 12–18 %, and in D f208 also a strip at the top (≈ 11 %).
      - The split can move down between frames (C f174 → f175).
      - `noise` is 40–44, against 8–14 in picture.
      - The first snow frame is coloured (chroma median 7–10 levels in the snow) and the second paler (4–7), so the colour killer engages within one or two frames.
   2. **Solid blue, exactly 4 frames** (C f176–179, D f210–213, F f251–254). This is the no-signal screen: flat RGB ≈ (0, 8–11, 197–202), with the receiver's two short lines of green text at the top left (content omitted).
   3. **Full-frame snow, 7–8 frames** (C f180–186, D f214–221, F f255–262).
      - It is colour-killed (chroma median 1.4–2.9), and the same receiver text is drawn over it. So this snow is the receiver showing its raw input, not the camera.
      - Its mean level flickers between 52 and 65 from frame to frame.
   4. **Solid blue, permanently** (C f187, D f222, F f263 to the clip end: 91–99 frames). The text doesn't change.
2. **Hard cut** (A f424, B f310, E f263, H f629). The last picture frame is followed directly by the blue screen, which stays. There are no snow or black frames in between.
   - The last picture frame differs from clip to clip:
     - H: N8 tearing.
     - A: a ≈ 10 % grain rise (high-pass σ +7–11 % against the two frames before) but no tear.
     - B and E: only re-encode and recorder artifacts (N10, N9).
   - Both receivers show hard cuts (A and H; B and E). Only the B–G receiver shows the staged variant.

- **Snow texture** (all 23 full-snow frames, at full resolution). Near-binary white dashes on black, not grey Gaussian noise.
  - 60–68 % of pixels are below 32 levels and 8–12 % above 224. White (≥ 128) covers 18–23 % (mean luma 52–65).
  - The dashes are one field line tall (median vertical run 4 px, 90 % ≤ 9 px) and short (median horizontal run 6–8 px, 90 % ≤ 19 px).
  - A faint diagonal herringbone runs through the whole field (C f175, f182; D f209).
  - The partial snow of stage 1 has the same texture, in colour.
- **The pilot's four stages are the staged variant** (U2, U10):

  | Pilot | Measured stage | Length |
  |---|---|---|
  | "very quick picture glitch" | partial snow over 68–88 % of the height | 1–2 frames |
  | "brief flashy moment" | the solid-blue interlude | exactly 4 frames |
  | "noisy and flashy" | full snow under the receiver text, its level flickering | 7–8 frames |
  | "finally blue" | permanent blue | to the end |

  No clip shows a white flash or a luma spike at any stage. If the pilot meant one, it is unconfirmed and not simulated by default.
- **Randomisation** (the pilot: no two losses alike). The sim runs at 50 fields/s. A stage seen in n recorded frames (≈ 30 fps) lasted between n − 1 and n + 1 frame intervals. Each range below covers what the footage allows, and no more:
  - **Variant.** Measured: staged in 3 of 7 losses (3 of 5 on the B–G receiver, 0 of 2 on the A/H receiver).
    - Draw staged with p = 0.8 when the link margin fell through the receiver threshold over one field or longer: a fade, with the fiber stretching or bending before it breaks.
    - Draw staged with p = 0.2 when the margin vanished within one field: a clean break, or power loss.
    - These are hypotheses, tunable (U2).
  - **Staged.**
    - Partial snow for 1–5 fields. The split line sits at 80–90 % of the height (measured 82–88 %) and may move down. In 1 loss in 3, a strip about 11 % tall also survives at the top. The first 1–2 fields are in colour.
    - Blue for 5–8 fields (100–160 ms). All three losses show exactly 4 frames, which points to a receiver timer, so keep the spread this narrow.
    - Full snow for 10–15 fields (200–300 ms), with a new pattern every field and the mean level redrawn every 1–2 fields within 52–65.
    - Then blue until reset.
  - **Hard cut.** In half of them (2 of 4), the last 1–2 picture fields carry a pre-cut glitch: N8 tearing, or a grain rise of ≈ 10 %. Then blue until reset.
  - The receiver text is the same on every loss.
- **Candidate effect.** A receiver state machine driven by the link state, with durations in fields drawn per loss as above:
  - `PICTURE → PARTIAL_SNOW (1–5) → BLUE (5–8) → SNOW (10–15) → BLUE`
  - `PICTURE → [GLITCH (1–2)] → BLUE`

  Snow is per-field thresholded noise, stretched horizontally, one field line tall, colour-killed after the first 1–2 fields, with the receiver text on top. The two blue-screen styles are as in §0.
- **Driver.** Any of:
  - **Link lost**: a fiber break, or the margin falling below the receiver threshold. How fast the margin fell picks the variant.
  - **Power lost**: battery voltage collapse, or an impact that cuts power.
  - **Camera or transmitter damage** on impact.

  In this footage every loss follows an approach, landing or contact (D is a clear contact case).

**N8 H-sync tearing.** H f628, the last picture frame before blue.
- **What it looks like.** In the bottom ≈ 200 px (18 %), lines are displaced sideways by ≈ 20–60 px in a jagged zig-zag. The OSD rows there are sheared diagonally and doubled.
- **Analog, not codec.** The displacement is per line and not block-aligned.
- **Candidate effect.** A per-line horizontal offset in the lower part of the frame, amplitude rising towards the bottom, random per line, correlated over 2–4 lines.
- **Driver.** **Link margin** falling below the sync threshold, just before loss (and possibly **battery voltage** collapse). Its onset leads loss by at most 1 frame here. In the loss model it is one of the two pre-cut glitches of a hard cut (N5–N7).

**N9 Frame stutter.**
- **Measured.** 29 picture frames across A–H are exact or near-exact repeats of the previous frame (mean difference < 2 levels). They come in groups spaced 2–3 frames apart, e.g. A f190/193/196, D f166/168/171 and E f249/252/255/257/262. E's group sits in the last 0.5 s before loss.
- **L.** No repeats.
  - Its three frames under the 2-level threshold (f192, f204, f210) belong to a 6-frame cadence: every frame whose number divides by 6 differs less from its predecessor (mean `diff` 3.0, against 3.6–4.4).
  - f204 and f210, which are in the lossless burst, keep their own grain: their high-pass correlation with the previous frame is 0.78, the same as the other pairs (0.73–0.79). So they are new frames.
  - The cadence comes from the recorder's conversion to exactly 30 fps (rec).
- **Likely cause.** The clip frame rates are non-standard (29.84–29.97 fps), which is typical of a variable-rate recording converted to a constant rate. So most repeats are **rec**.
- **Candidate effect.** Only if the pilot confirms the goggles stutter before loss (U3): hold the last field for 1 field at random, 2–3 fields apart, while link margin is low.
- **Driver.** Link margin (if confirmed), else not simulated.

**N10 Macroblock smear (re-encoding).** B f306–309, when a close object suddenly fills the frame just before loss.
- **What it looks like.** Large rectangular mosaic blocks.
- **Block evidence.** The luma-gradient ratio at column boundaries x ≡ 0 (mod 16) rises 1.66 → 1.79 → 1.88 over f306–309 (rows: 1.25 → 1.46). A normal B frame shows 1.46 / 1.13.
- **Verdict.** Block-aligned to the 16-px macroblock grid of the frame, so **enc. Not simulated.**

**N11 Single-frame exposure jumps.** D f183: mean luma 119 → 104 → 117 for one frame.
- **What it looks like.** The sun's flare ghost vanishes in that frame and the view is jolted.
- **Not a recording glitch.** The frame is no closer to f190–197 than to its neighbours, so it isn't a displaced frame. It is a real frame in which vegetation briefly hid the sun and removed the veiling glare.
- **Driver.** Frame content (sun visibility), handled by O3 + C1.

**N12 Mid-flight dropouts and flashes that recover (P, and the pilot).**
- **Sources.**
  - Clip P, a 47-s flight in severe wind and rain.
  - The pilot's answer (U1): flashes happen at random in flight, separate from signal loss, and they recover.
  - A–H and L have none (0 in 1.32 min of in-flight footage).
- **Measured in P** (4 events in 47.3 s, ≈ 5/min):

  | Frames | Onset | Outage | Recovery |
  |---|---|---|---|
  | f676–687 | straight from picture | blue screen, 12 frames (0.40 s) | straight back to picture, with the receiver text; at f689 a dark dip and a −36 % level step (N13) |
  | f951–958 | f951–953: the picture brightens (+27 % to +120 % mean luma) under a dense burst of coloured impulse dashes | black, OSD included, for ≈ 2 frames: from row ≈ 585 down in f954, all of f955, and down to row ≈ 585 in f956 (f957 is a recorder repeat) | picture at its old level at f958; no receiver text |
  | f966–985 | f966: brightening (+36 %) with bands of impulse dashes; f967–968 back to normal | blue screen, 17 frames (0.57 s) | straight back to picture, with the receiver text |
  | f1144–1164 | f1144: the top rows shear sideways and a black wedge cuts in from the right edge (N8-style tearing) | blue screen, 20 frames (0.67 s) | straight back to picture, with the receiver text |

  - The blue-outs cut in and out with no snow and no partial frames. The screen is the bright pure blue of the A/H receiver type (§0).
  - After each blue-out the receiver shows its own two short lines of green text at the top right (content omitted) for ≈ 7.3–7.5 s (f688–906, f1165–1383), then clears them. The black dropout doesn't bring the text up, so the receiver kept its lock through it.
  - The pilot's word "flashes" fits the brightening impulse bursts and the short dropouts best. The blue-outs are the longer version of the same event. This mapping is an interpretation.
- **Candidate effect.** A dropout generator in the receiver stage. Draw each part per event, so no two are alike:
  - **Precursor** (2 of 4 events): 1–3 frames (2–5 fields) of brightening (+25 % to +120 % mean luma) under bands of coloured impulse dashes (N2 texture, much denser), or one N8-style tear.
  - **Outage.** Short, ≈ 3–4 fields (1 of 4): black, OSD included, entering and leaving mid-field at a random line. Long, 18–35 fields (0.37–0.70 s; 3 of 4): a hard cut to the blue screen and back.
  - **Recovery.** Straight back to the picture. After a blue-out, show the receiver text for ≈ 7.3 s, and sometimes (1 of 3) an N13-type dip and level step.

  The always-on N1 grain carries on through the picture parts.
- **Rate.** ≈ 5/min in P's severe wind and rain, and none in 1.32 min of calmer flight (a 90 % one-sided bound of ≈ 1.7/min). So the rate comes from sim state, not a fixed clock.
- **Driver.** **Link margin** dips from fiber tension spikes and tight bends that don't break the fiber. That is the tether model under wind load.
  - The hazard rises steeply as the margin nears the receiver threshold.
  - How deep and how long the margin stays below the threshold picks black or blue, and the outage length.
  - **Motor current steps** add a weak term (pwr; the pilot says throttle coupling is rare).
  - A small free-running floor (start at 0.2/min, tunable) keeps them random in calm flight.
  - The link between wind load and P's rate is a hypothesis, because P has no link data. The margin-to-hazard mapping is **unconfirmed, tunable**.

**N13 Level steps with one-frame dark dips (dim light: L, P).** These are all 9 of L's flags and most of P's. The brighter clips A–H show none.
- **Steps in L.** The whole picture jumps to a new brightness and holds it. The framing and horizon carry on smoothly through the step, the OSD layout doesn't change, and there is no motion blur (f162–164, f199–201).

  | Step | Mean luma before → after | Change | Dip frame |
  |---|---|---|---|
  | f14 | 43.1 → 33.8 (f15) | −22 % | f14: below a sharp split at row ≈ 433 (40 % down) the scene goes almost black (ground 31 → 0.7); the rows above are unchanged |
  | f81 | 38.5 → 50.3 | +31 % | f80, mild: the top of the frame is 20–30 % darker than f79, easing towards the bottom, which already shows the new level |
  | f115 | 46.5 → 50.3, then 52.6 at f117 | +8 %, then +13 % | none |
  | f164 | 52.1 → 42.1 | −19 % | f163, whole frame: mean 18.1 (−65 %) |
  | f201 | 43.8 → 27.0 | −38 % | f200, whole frame: mean 10.9 (−75 %) |

- **P** (daylight under heavy overcast and rain, in full colour, mean luma 57–91). The same trait, more often: 30 one-frame dips (−17 % to −53 %) and 12 plain steps in ≈ 45 s of picture, about one every 1.1 s. The level toggles between ≈ 57–63 and ≈ 85–91, with steps from −30 % (f176) to +56 % (f651).
- **The new level is mostly a black-level shift.** A block-by-block fit of each settled frame (L f15, f81, f117, f164, f201) against the frame before the step gives a gain of 0.90–0.98 and an offset of −13 to +13 levels. So dark ground moves much more than the sky in relative terms (f199 → f201: ground 26 → 10, sky 80 → 62). P's larger steps also change the gain: 0.68–0.98, with offsets of −11 to +13 (f138 → f142, f175 → f177, f371 → f373, f436 → f438).
- **The dip.** Gain ≈ 0.43–0.48 with an offset of ≈ −8 levels in L, which crushes the ground to 1–7 levels. In P the gain is 0.47–0.59, with offsets of −2 to −12 (f176, f372).
  - It eases towards the bottom of the frame. In L f163, ground just below the horizon keeps 7–16 % of its f164 level, while the slightly darker ground in the bottom rows keeps 55–75 %. So the dip recovers within about one field.
  - It can start partway down a field (L f14; P f139, where the rows above ≈ 330 are unchanged).
- **Not a camera-only exposure change: the OSD dims too.**
  - Static OSD pixels (bright in every neighbouring frame) lose 10–48 % in L f163, f200 and the dark part of f14, while the scene loses 60–98 %. After each down-step they settle 4–10 % lower (f15, f164, f201).
  - In the day clips the same pixels never move: they stay within ±1 % through D's exposure changes (f182–184 at −13 %, f197–207 at −38 %) and in H.
  - In P the static OSD dims 5–32 % in the dips (f55, f139, f176, f372, f437) and follows the steps both ways (−16 % to +6 %).
  - So at least part of each change acts on the composite signal after the OSD is inserted.
- **Rate.**
  - L: 5 steps in 7.0 s (≈ 43/min), 1.1–2.2 s apart. Every down-step has a dip.
  - P: ≈ 42 events in ≈ 45 s (≈ 55/min), 30 of them with a dip.
  - None in the 72 s of brighter day flight (A–H). That includes H, which has the same airframe group and receiver type as L and P.
- **Cause: unknown (U14).** Candidates:
  - The camera's low-light gain control stepping, with an output transient that the receiver's own gain control follows. This would explain why it appears only in dim light.
  - A supply dip on the rail the camera, OSD and fiber transmitter share (pwr).
  - Steps in optical power as the fiber pays out (link).
- **Candidate effect.** A level-step generator on the composite signal, after the OSD insert.
  - At each event, draw a new gain (0.68–0.98 going down, and its inverse going up) and black offset (−13 to +13 levels on the 8-bit scale).
  - Before a down-step (and sometimes an up-step), insert one dip field (gain 0.43–0.59, offset −2 to −12) that eases back from top to bottom. It sometimes starts partway down the field.
  - Let the OSD whites clip at the top of the range, so they dim less than the scene, as measured.
  - Hold the new level until the next event, or ramp to it over ≤ 2 frames (f115–117).
- **Driver.** **Light level** gates it: it runs only when the scene is dim, at night (L) or under heavy overcast (P: mean luma 57–91, against 102–155 in A–H). It doesn't need the colour to be killed, because P keeps full colour.
  - The timing is free-running random at the measured interval (≈ 1–2 s), because nothing measurable in L or P predicts it.
  - Size and direction are drawn from the measured sets (L: 3 down, 2 up; P: −30 % to +56 %).
  - If U14 finds a supply-dip or fiber cause, the timing moves to motor current steps or fiber margin. Both signals are already in §7.

### 1.4 Not feed noise (for the record)

- **E f206–237 stripe flags are the OSD blinking.** A block of OSD elements is on for ≈ 7 frames and off for ≥ 5 (E f206–219, f227–237). The OSD belongs in the feed (§5) but is not interference.
- **Prop blades in view.** A (dark blade arcs, top corners), B and G (blue-grey blurred blades at the left/right edges, mid-height), D f138–139 (thin dark blade slivers at the top edges). This is frame content from airframe geometry. Blurred blades should be rendered as geometry, not as a feed effect.
- **What looked like ghosted or blended frames is optical.** C f148–157: veiling glare from the sun just outside the frame (O3). D f138–139, f182–184: flare ghost. E f256–262: motion blur in a dark interior (C4). No frame blending was found.

## 2. Pixel-level artifacts

| ID | Trait | Evidence | Candidate effect | Driver | Stage |
|---|---|---|---|---|---|
| P1 | **Field-line structure: the "edgy" pixels** | See below | Render the camera image at field resolution (≈ 450 × 288 effective), pick the field parity per output field, upscale vertically with a sharp (near-nearest) kernel. Diagonals then stair-step and fine detail breaks into line-dashes | Frame content (static format property, no sim state). Parity alternates per field; the recorder's irregular sampling is not simulated | cvbs, rx |
| P2 | **Horizontal softness** | Luma spectrum cut-off ≈ 400–525 effective samples per line (all clips). OSD glyph edges rise 10–90 % in ≈ 3 px (C f142, inserted after the camera, so this is the transmission path alone). Scene edges rise in ≈ 5–6 px (C f142 tarp edge) | Horizontal-only low-pass on luma to ≈ 450 samples per line | Frame content (static) | cvbs |
| P3 | **Sharpening halos** | See below | Horizontal unsharp mask (radius ≈ 1 source sample, strong) plus a weaker vertical one, applied **before** the P2 blur so the halo softens like the footage | Frame content (static). Optionally weaker at high gain (DSP detail enhancement drops as gain rises; hypothesis) → light level | cam |
| P4 | **Chroma smear and bleed** | See below | Chroma low-pass ≈ 5× wider than luma (≈ 90 chroma samples per line), plus a 0–3 px rightward offset | Frame content (static) | cvbs |
| P5 | **Cross-colour rainbow** | See below | Take the luma detail in the band near the colour subcarrier and add it back as false chroma. Phase alternates line to line and field to field (PAL), so it shimmers and crawls | Frame content (fine detail) × camera motion | cvbs |
| P6 | **Chroma sparkles** | See N2. At 4×: coloured dashes one line tall, 3–10 px long (B f122 ground) | See N2 | Link margin, motor current | cvbs/link |
| P7 | **Hard-clipped highlights with colour cast** | See below | Hard clip after the camera's tone curve, plus a slight desaturation or magenta shift in the last ≈ 10 % before clip | Light level (via AE) | cam |
| P8 | **Black frame border** | Top 4 rows (B–G receiver) or 1 row (A/H), bottom 1–2 rows, black on every frame | Draw the border | Frame content (static per receiver style) | rx |
| P9 | **Motion blur in low light** | See C4 | See C4 | Light level × angular rate | cam |
| P10 | **Codec block grid** | See below | **Not simulated** | — | **enc** |

**P1 Field-line structure.** This is the pilot's "edgy, non-tessellated pixels".
- **Line count.** Row-to-row differences repeat every 3.77–3.79 px on the B–G receiver and 3.93 px on the A/H receiver, i.e. ≈ 286 and ≈ 275 lines per picture height: one PAL field (288 active lines), not a full frame.
- **Parity.** H f365–372 alternates between two phases half a period apart in runs of 2–3 frames. That is bob deinterlacing, 50 fields/s sampled at ≈ 30 fps. On the B–G receiver the phase stays within 0.4 px (B f186–193), so there is no visible parity flip.
- **Close up.** At 6× (H f368 tree canopy) fine detail breaks into horizontal colour dashes one field line (≈ 4 px) tall and 6–20 px long with abrupt ends. Diagonals (prop blades, B f122) show ≈ 4-px stair steps.

**P3 Sharpening halos.**
- **Horizontal.** A dark undershoot ≈ 40 levels deep and ≈ 4 px wide on the dark side of a strong edge (C f142 tarp edge, luma 88 → dip 49 → 176). A small overshoot sits on the bright side.
- **Vertical.** Weaker: a bright rim ≈ +18 levels and ≈ 3 px thick above the horizon (B f188).
- **Also visible** as dark outlines around the bright landing-gear rods (C f142).

**P4 Chroma smear and bleed.**
- **Measured.** Chroma transitions span ≈ 25–30 px against ≈ 6 px for luma (C f142 blue-tarp edge, Cb −28 → +40). That is ≈ 5× lower chroma bandwidth. The chroma edge sits 0–6 px right of the luma edge.
- **Visible as** brick red spilling into the mortar and the tarp blue into the door frame (C f142), and colour halos around OSD glyphs.
- **Codec part is negligible.** Chroma values also repeat in 2-px pairs; that is the re-encode's 4:2:0 subsampling, far smaller than the analog smear.

**P5 Cross-colour rainbow.**
- **Where.** Purple/green/yellow false colour on fine luma detail: branches against the sky (F sheet, G f1 top-left, C f142 left vegetation) and dry grass (H).
- **Strength.** At 4× it forms streaky ±30–40-level chroma bands along each field line.
- **Behaviour.** It is strongest on thin, high-contrast, near-vertical detail and changes every frame as the texture moves.

**P7 Hard-clipped highlights with colour cast.**
- **What it looks like.** Bright sky clips flat (σ = 0 in the clipped region of H's overcast sky). The clip edge takes a lilac/magenta tint (C f142 top-left sky, C f150–157 glare).
- **Windows** in dark interiors clip to pure white with a soft bloom (E f253–262).

**P10 Codec block grid.**
- **Measured.** In every clip the mean luma gradient across column boundaries at x ≡ 0 (mod 16) is 1.3–1.5× the average, x ≡ 8 about 1.2× and x ≡ 4 about 1.1×. Rows show the same up to 1.33× (H).
- **Verdict.** Aligned to the absolute frame grid: re-encode fingerprint.

## 3. Optics

| ID | Trait | Evidence | Estimated strength | Candidate effect | Driver |
|---|---|---|---|---|---|
| O1 | **Barrel distortion** (strong, near fisheye) | See below | **k1 ≈ 0.33** (0.30–0.34), with p_u = p_d·(1 + k1·r_d²) and r normalised to the half-width | Inverse radial mapping in the final pass. Render the 3D view with extra field of view to fill the corners (the edge needs ≈ 1.33× the half-width of undistorted image) | Frame content (static per camera) |
| O2 | **Vignetting** (mild) | See below | ≈ 10 % (5–15 %) at the edges, ≈ 10–20 % in the corners | cos⁴-style radial falloff, 10–15 % in the corners | Frame content (static) |
| O3 | **Sun flare and veiling glare** | See below | Glare can lift mean luma by ≈ 10–15 % frame to frame (D f183) | Screen-space ghost placed by the sun's projected position, plus a veiling glare that scales with how close the sun is to the frame and whether it is occluded (depth test against the sun), clipped by a housing edge | Light level (sun direction and visibility vs. camera) |
| O4 | **Lens dirt** | See below | Two blobs, ≈ 60–100 px | Optional dirt mask composited out of focus. It accumulates, it is not a fixed overlay | Contact with vegetation or ground (impact events, including light brushes). Dust is not a driver unless U12 confirms it |
| O5 | **Depth of field** | Fixed focus: sharp from ≈ 1 m to infinity. Very close surfaces (E f253–262, F f249) are soft, but blur from motion and low light dominates | Small | None needed beyond C4 motion blur. Optionally a mild near-field blur below ≈ 0.3 m | Frame content (camera-to-surface distance from the depth buffer) |

**O1 Barrel distortion.**
- **Measured.** The horizon traced across the full width of A f1, A f236 and A f241 gives k1 = 0.30, 0.33 and 0.34, with a residual of 1.6–2.7 px after correction against 6–12 px for a straight line.
- **Aspect.** Square source pixels (native 16:9) fit better than a stretched 4:3 image in every case.
- **Other cameras.** B–H look similarly strong (curved roof eave in E f7, curved door frames in C, curved horizon in G), but they give no clean straight line far from the centre and were not measured separately (U9).

**O2 Vignetting.** The average of 82 picture stills from all clips gives ≈ 0.86–0.94 of centre luma at the left and right mid-edges and ≈ 0.80–0.95 at the corners. This is heavily confounded with content (sky up, ground down), so the confidence is low.

**O3 Sun flare and veiling glare.**
- **Flare ghost.** With the sun in or near the frame, a reddish-magenta round ghost ≈ 150–250 px across sits roughly opposite the sun through the centre (D f138–139, f182–184).
- **Veiling glare.** A bright wash with a straight edge where the lens housing clips it lifts dark areas to grey (C f148–157, sun just outside the top-left).
- **Behaviour.** Both come and go frame to frame as the drone moves and as foliage hides the sun (D f183).

**O4 Lens dirt.** E: two dark, defocused brown blobs, ≈ 60–100 px, bottom-right, fixed in frame while the scene moves. They are absent at E f1, f45 and f175 and present from f206 to the end (clearest at E f211), so they were picked up in flight, most likely while passing through the tall weeds in front of the wall.

## 4. Colour and exposure response

**C1 Auto-exposure holds the frame mean almost constant.**
- **Measured.** Mean luma barely moves while the framing changes:

  | Clip | Mean luma range | Span |
  |---|---|---|
  | B | 139.6–141.4 | whole 10 s picture (sky share changes) |
  | F | 129–131 | 8 s |
  | E | 122–126 | until the approach at f200 |
  | A | 137–155 | |
  | H | 102–114 | |

  Brightness is traded locally: when sky enters, the ground darkens.
- **Transients:**
  - A f70 → f84: 155 → 137 in ≈ 0.45 s, back to ≈ 149 by f100 (≈ 0.5 s). The loop's time constant is ≈ 0.3–0.5 s.
  - Entering deep shade (D f196 → f207): 116 → 66–86, not recovered within 11 frames (0.37 s). The loop is slower towards dark or at the gain limit.
- **Candidate effect.** An exposure controller on the *rendered* frame: measure mean (centre-weighted) luminance, and move exposure/gain towards a target of ≈ 0.47–0.55 of full scale with τ ≈ 0.3 s when brightening and ≈ 0.5–0.8 s when darkening, up to a maximum gain. The gain feeds N1 grain; the exposure time feeds C4 blur.
- **Driver.** **Light level** (scene luminance, sun) and frame content.

**C2 Auto white balance pulls whatever fills the frame towards grey.** This is the pilot's "colours change as you get closer".
- **E f196 → f241:** as the ochre clay wall fills the frame, r/g falls 1.16 → 0.99 and b/g rises 0.81 → 0.99 over ≈ 1.5 s. The wall that read warm ochre from a distance reads grey-lilac up close (E f211 against E f1–181).
- **G f100 → f118:** diving into vegetation, r/g 1.18 → 0.87 and b/g 0.67 → 1.02.
- **A f84 → f125:** the balance drifts steadily (b/g 0.70 → 0.87) as the field in view changes, a slow loop.
- **Estimated speed.** τ ≈ 1–1.5 s, slower than AE.
- **Candidate effect.** Grey-world AWB on the rendered frame: per-channel gains move towards making the frame mean neutral with τ ≈ 1.2 s, clamped to a realistic range (a gain ratio of ≈ 0.7–1.4) so it never fully neutralises a scene. The camera image before AWB should have a warm, saturated "analog camera" colour matrix (B and G read strongly warm/orange with deep blue sky, r/g ≈ 1.13–1.16).
- **Driver.** Frame content, as rendered.

**C3 Saturation drops as the picture gets neutral or dark.**
- **Measured.** Close to a surface (E f241+, D f201+) the frame goes nearly monochrome (r ≈ g ≈ b).
- **Candidate effect.** Saturation scales with AE gain (analog cameras reduce chroma at high gain).
- **Driver.** Light level.

**C4 Motion blur grows in low light.**
- **Measured.** In the dark interior at the end of E (f253–262) the image smears heavily in the direction of rotation. Outdoors in sun (B, G) motion at similar rates is crisp.
- **Candidate effect.** Per-pixel motion blur with length = angular velocity × exposure time, where exposure time comes from the C1 loop (short in sun, up to about one field time in the dark).
- **Driver.** **Light level** (through exposure time) × **camera angular rate** (physics).

**C5 Low light: the night flight (L).** 210 frames (7.0 s) under an overcast night sky.
- **Level and contrast.** Mean luma is 45.6 (10.9–55.0 per frame), against 102–155 in the day clips (C1). The overcast sky is the brightest part of the scene (≈ 60–100 levels) and the ground is dark (≈ 15–35, and 5–12 after the f201 step). Tree crowns, poles and bushes read only as silhouettes against the sky.
- **Monochrome.** Over the clip the channel means are R 45.1, G 46.0 and B 44.8.
  - Per pixel, with black and OSD white excluded, chroma has a median of 1.1–1.4 levels and a 90th percentile of 2.3–3.6. The day clips A, B, C, D, G and H have medians of 18–44 by the same measure.
  - The only colour left is faint fringing on the OSD glyphs (P4; 90th percentile ≈ 8 levels next to them) and a coloured speckle on a bright patch of ground close to the camera (f1–13).
- **Grain.** The strongest in the set, and live. See N1: σ ≈ 2.0–2.7 levels in static sky, 2–5× the day level.
- **Level steps and dark dips.** L shares this trait with P, a dim overcast daytime flight. No bright day clip shows it. See N13.
- **Not measured.**
  - Whether the camera runs at its maximum gain: it stepped *up* twice (f81, f115), so it still had some headroom.
  - The shutter time: L has no fast rotation and no frame with motion blur, so the C4 prediction (longer exposure, more blur) can't be checked.

  Both stay hypotheses.
- **OSD.** The OSD whites (≈ 210–255) are the brightest thing on screen, so the OSD dominates the dark picture.
  - Its edges are soft: they rise 10–90 % in ≈ 4 px (interquartile 3.4–4.8 px; L f1, f162, f165). That is about one source sample, the same as the day OSD measured the same way (3.4–3.9 px in C, D and F; P2).
  - The glyphs have faint coloured fringes (P4), and the OSD dims with N13's dips.
- **No point lights.** No still, burst or event frame of L shows a point light. The row of bright dots near the horizon is the OSD's dotted artificial horizon. How lights bloom at night is **unmeasured**. If the map gets lights, render them through P7 (hard clip) and O3 (flare), as an assumption.
- **Candidate effect.** No separate night filter: the same pipeline, driven to its low-light end.
  - C1 runs at high gain, and N1 grain rises to ≈ 2–5× its day level.
  - C3 takes saturation to 0.
  - C4 lengthens the exposure (assumed).
  - N13 switches on.
  - P2–P4 and the OSD insert are unchanged.
- **Driver.** **Light level** (time of day and scene luminance) through the C1 loop, and **camera angular rate** for C4.

## 5. Resolution feel and OSD layout

**Resolution feel, in short.** The feed carries far less real detail than its 1920×1080 container:

| Channel | Effective detail |
|---|---|
| Luma | ≈ 450 samples per line (P2) × ≈ 286 lines per field (P1) |
| Chroma | ≈ 90 samples per line (P4) |
| Time | 50 fields/s, each field a new vertical sampling phase |

It reads as coarse horizontal line-dashes, soft horizontal edges with dark halos, rainbow crawl on fine textures, and colour that floats off edges. The sim should produce its feed from that effective resolution and only then scale it to the display. That is also a large performance win (§6).

**OSD layout** (general description only: no values and no per-airframe layout).
- **Rendering.**
  - A character OSD on a **30-column × 16-row grid** (each cell ≈ 64 × 67.5 px on the recording, so one text row is 1/16 of the picture height, ≈ 6 %).
  - White fixed-width glyphs with a black outline.
  - It is inserted on the drone before transmission, so it takes the analog treatment: soft horizontal edges (P2), coloured fringes (P4), tearing with the picture (N8, H f628) and cross-colour dots near busy texture.
  - It is **not** lens-distorted: the OSD horizon line stays straight while the real horizon bends (A f236).
  - Warning elements **blink** (≈ 7 frames on, ≥ 5 off; E).
- **Typical layout** (A–H). Readouts sit along the edges and in the corners, and the centre stays clear apart from a small crosshair or aircraft symbol and, optionally, a dotted artificial-horizon line. The top edge holds arming or status text, a heading or name field, and height or distance readouts. The left side holds a flight-mode or status word, the bottom-left corner the battery readouts, and the bottom row a name or message line, timer, current and link-quality readouts, and warning text.
- **Driver.** The content is the sim's flight and warning state (armed state, flight timer, battery voltage, link margin, attitude), which the game's OSD model turns into characters: the **OSD state** signal in §7. A warning is shown while its condition holds, for example battery voltage under load below a threshold or low fiber link margin, and the OSD model toggles it on a fixed cycle that matches the footage. The feed only draws the grid it receives.
- **For the game.** The game-developer owns the OSD content (Train mode shows only what the pilot needs). The tech-artist owns putting it **through the feed pipeline before the analog stages**, so it degrades and tears like the real one.

## 6. Implementation notes for the feed (non-binding)

- **Order of stages:**
  1. Render the scene at camera resolution with extra FOV.
  2. Lens distortion, vignette, flare.
  3. Camera: AE/AWB, tone curve, clip, sharpening.
  4. Add the OSD.
  5. Composite encode: luma/chroma band-limit, cross-colour, interference (N3, N4), grain, sparkles. In dim light, N13's level steps and dips go here, after the OSD, so the OSD dims with them.
  6. Field sampling (P1).
  7. Receiver: sync tearing (N8), recovering dropouts and flashes (N12), snow/blue state machine (N5–N7), border (P8).
  8. Upscale to the screen.
- **Performance** is not measured here (no effect is built in this change). Working at ≈ 720 × 288 per field for stages 4–7 keeps the per-pixel cost to about 10 % of a 1080p pass. The cost of every effect goes in the verification notes of the implementation change, against the 60+ fps budget.
- **Not simulated:** N10 and P10 (re-encode), the recorder's frame-rate conversion, and N9 unless the pilot confirms it (U3).

## Uncertain — status after pilot review

| # | Item | Status / resolution |
|---|---|---|
| U1 | **Mid-flight flashes that recover** | **Answered by the pilot, and seen in clip P** (N12): brightening bursts of impulse noise, a short black dropout and three 0.40–0.67-s blue-outs, all of which recover, at ≈ 5/min in severe wind and rain. A–H and L show none. How the rate maps to link margin is **unconfirmed, tunable.** |
| U2 | **Signal loss sequence** | **Answered by the pilot, and matched to the footage.** The pilot's four stages are the staged loss of C, D and F: partial snow → 4 blue frames → 7–8 snow frames → blue (N5–N7), randomised within the measured ranges. The hard cut (A, B, E, H) stays as the second variant. Which cause gives which variant is a hypothesis. **Unconfirmed, tunable.** |
| U3 | Pre-loss frame stutter | Unanswered by pilot; kept as recording artifact (rec). **Unconfirmed, tunable.** |
| U4 | Live sky grain level | **Partly answered by the pilot:** baseline noise is always present. The live day level still can't be measured, because the encode flattens it. L gives the night level as recorded (N1). Base σ 2–4 levels. **Unconfirmed, tunable.** |
| U5 | Low light and night look | **Partly answered by clip L** (C5, N13): monochrome, the strongest grain, sky brighter than ground, a dominant OSD, and level steps with dark dips. L doesn't show point lights, motion blur at night, the camera's gain limit or its shutter time. Those stay **unconfirmed, tunable.** |
| U6 | Throttle noise coupling | **Answered by pilot.** Noise may depend on throttle, but **rarely**. Throttle coupling is weak and occasional, not a dominant driver. |
| U7 | Diagonal dotted lines (B) | Unanswered by pilot; kept as airframe-specific interference. **Unconfirmed, tunable.** |
| U8 | Rolling lines (C) | Unanswered by pilot; kept as motor/ESC harmonic transient. **Unconfirmed, tunable.** |
| U9 | Lens distortion across airframes | Unanswered by pilot; kept as k1 ≈ 0.33 across airframes. **Unconfirmed, tunable.** |
| U10 | Mid-flight fiber break look | **Answered by the pilot:** the same staged sequence as U2. Every loss in the footage is at the end of a flight, so a mid-flight break uses the same model. |
| U11 | Chroma sparkles in goggles | Unanswered by pilot; kept as link-margin / fiber-threshold impulse noise. **Unconfirmed, tunable.** |
| U12 | Lens dirt accumulation | Unanswered by pilot; kept as contact-event accumulation. **Unconfirmed, tunable.** |
| U13 | No-signal screen style | Unanswered by pilot; kept as blue screens per receiver styles (§0). **Unconfirmed, tunable.** |
| U14 | Dim-light level steps and dark dips (N13) | **New, from clips L and P.** The trait is real, but its cause is unknown (camera low-light gain, a supply dip, or fiber optical power), and two clips give only a rough rate and light threshold. **Unconfirmed, tunable.** |

## 7. Simulation signals the feed needs (task 3.2)

This is the input to the later physics↔video interface change. "Field rate" means the feed samples the value once per video field (50 Hz). Physics steps at 1 kHz and should hand over per-field aggregates (mean, and max where noted) rather than raw samples.

| Signal | Unit | Update rate | Source | Feeds traits |
|---|---|---|---|---|
| Motor electrical frequency, per motor (or RPM × pole pairs) | Hz | 50 Hz (field mean) | physics | N3 spacing and roll |
| Motor current, per motor and total (a throttle/current proxy) | A | 50 Hz (field mean and field max) | physics | N3 visibility, N4 amplitude, N1/N2 power-rail term (all weak and occasional, per the pilot) |
| Motor current step (d/dt of total current) | A/s | 50 Hz (field max) | physics | N4 burst onset and N12 power-rail term (both weak); N13 timing if U14 finds a supply dip |
| Battery voltage under load, pack | V | 50 Hz | physics | Brownout: power-loss hard cut (N7), N8 tearing at collapse, OSD |
| Camera angular velocity (body rates at the camera) | rad/s (3 axes) | per rendered frame (≥ 60 Hz) | physics | C4 motion blur (also at night, C5), P5 crawl |
| Camera vibration (acceleration at the camera mount, RMS in 3 bands: < 50 Hz, 50–200 Hz, > 200 Hz) | m/s² | 50 Hz | physics | Hypothesis only: intermittent sparkles and interference from connector microphonics (N2, N3). Kept as a hook until U8 confirms it |
| Impact event (peak acceleration, contact point, rigid-body impulse) | m/s² and N·s, per event | event, timestamped at physics rate | physics | Loss on hard impact (N5–N7), single-frame jolt, lens-dirt accumulation (O4) |
| Fiber tension at the spool exit | N | 50 Hz | physics (tether model) | Link margin → N2 sparkles, N8 tearing, N12 dropouts, N5–N7 loss |
| Fiber minimum bend radius along the paid-out length | m | 50 Hz | physics (tether model) | Link margin (macro-bend loss); N13 timing if U14 finds a fiber cause |
| Fiber link state: intact / broken, plus optical margin | enum + dB | 50 Hz (break as an event) | physics (tether model) | N5–N7 loss (how fast the margin falls picks staged or hard cut), N12 trigger, N2 density, N8 |
| Scene light level (mean and centre-weighted luminance of the rendered frame, before exposure) | cd/m² (or EV) | per rendered frame | renderer (video module) | C1 AE → N1 grain, C3 saturation, C4 exposure time; N13 low-light gate |
| Sun direction relative to the camera and sun visibility (occluded fraction) | unit vector + 0–1 | per rendered frame | renderer and sky (tech-artist) | O3 flare and veiling glare, N11 |
| Time of day | h (local solar) | on change (user setting) | game settings | Sky and sun, base light level (C5 night) |
| OSD state: the 30×16 character grid as currently shown, blinking cells already toggled, built from sim state (armed state, flight timer, battery voltage, link margin, attitude) | characters (30 × 16 cells) | 50 Hz (per field) | game (OSD model, game-developer) | §5 OSD content and blinking warnings |

Every driver named in §1–§4 is covered:

| Driver named in the traits | Signal(s) |
|---|---|
| Throttle / motor current | motor current; motor current step (weak and occasional coupling per pilot) |
| Motor RPM / electrical frequency | motor electrical frequency |
| Battery voltage sag | battery voltage under load |
| Vibration | camera vibration (hook; hypothesis, U8 open) |
| Impact | impact event |
| Fiber tension / bend / link margin, including how fast the margin falls at a loss | fiber tension, bend radius, link state + margin |
| Light level | scene light level; sun direction and visibility; time of day |
| Camera motion | camera angular velocity |
| OSD flight and warning state | OSD state (built by the game from battery voltage, link state + margin and its own state) |
| Frame content, including the static camera and receiver properties (P1–P4, P8, O1, O2, O5) | (the rendered frame and depth buffer themselves; no signal needed) |
| Free-running random | (the feed's own seeded RNG, at the rates in §1.2: the N1 grain pattern; N3/N4 onsets in a flight that has them, plus the per-flight draw; the N12 floor rate; N13 step timing; and the loss draws: variant, stage lengths, split line, snow pattern) |
