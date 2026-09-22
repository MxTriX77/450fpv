# Video feed reference notes

The pilot's goggles feed **is** the target look. These notes break it down trait by trait: what the footage shows, how we would render it, and which part of the simulation drives it (D-008: the feed is simulated, not filtered).

- **Sources:** 8 flight clips **A–H** (the 3 photos I–K are not feed footage and are not used here). Cited by letter and frame number (`B f123` = clip B, frame 123, 1-based, about 29.9 frames per second).
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
  - A and H: bright pure-blue no-signal screen (mean B ≈245–255) with small centred text, ≈275 lines per picture height, 1-px black top border.
  - B–G: deeper blue no-signal screen (mean B ≈187–205, G ≈10–12; seen in B–F, since G never loses picture) with two short text lines top-left, ≈286 lines, 4-px black top border.
- **Three airframe/camera set-ups,** recognisable by the OSD layout and the parts in view: A; B and G; C, D, E, F and H.
- **Re-encode.** The messenger H.264 re-encode leaves a weak but frame-aligned block grid in every clip (§2 P10). Only traits with that block-aligned evidence are classed as re-encoding.

Each trait below names its most likely stage: **cam** (sensor/DSP), **cvbs** (composite encoding/decoding), **pwr** (power-rail or ESC interference), **link** (fiber TX/RX), **rx** (goggles/receiver behaviour), **rec** (recorder) or **enc** (re-encode, not to be simulated).

## 1. Noise events

### 1.1 Footage totals and selection bias

| | Frames | Seconds |
|---|---|---|
| All clips | 3016 | 100.8 |
| Picture (not blue, not snow) | 2369 | 79.2 |
| **In-flight window** = picture minus the last 1.0 s before each loss | 2159 | 72.1 (1.20 min) |
| Pre-loss windows (last 1.0 s before loss, 7 clips) | 210 | 7.0 |

**Bias.** 7 of 8 clips (all except G) end in loss of picture, and every one of those losses comes right after a close approach, landing or contact (A: low over grass; B: frame filled by a close object; C, E: flying into a doorway; D: contact with a stone structure; F: descent onto a ground surface; H: settling into grass). The clips were cut around the end of each flight, so:
- loss sequences are over-represented by roughly **one per clip** rather than a rate in time. Quoted per minute (7 / 1.32 min = 5.3/min) the number is meaningless. They are modelled as **event-driven** (impact, power loss, fiber break), not as a random rate.
- **in-flight rates are from only 1.2 minutes** of footage from three airframes. A trait seen once gives a rate of about 1/min with a very wide error (one event in 1.2 min is consistent with anything from about 0.05 to 4 per minute at 90%). Rates below are what was seen, not a stable statistic. The sim should expose them as tunables.
- two traits cluster in single clips (diagonal interference only in B, rolling lines only in C). Pooled rates dilute them and per-clip rates inflate them. Both are given.

### 1.2 Event catalogue

Rates are per minute of the in-flight window unless marked. "Duration" is in recorded frames (≈30 fps). One recorded frame ≈ 1.67 fields.

| ID | Event | Clips / frames | Count | Rate | Duration | Stage |
|---|---|---|---|---|---|---|
| N1 | Live grain | all, continuous | — | continuous | every frame, new pattern each field | cam, cvbs |
| N2 | Chroma sparkles (coloured impulse dashes) | all; densest H, F, C; near zero A, E (§2 P6) | continuous | continuous background; no bursts beyond content changes | per frame | cvbs/link |
| N3 | Rolling thin dark lines ("stripes") | C only: f3–4, f9–11, f30–53, f80–91 | 4 events (38 frames) | 3.3/min pooled; 50/min within C | 2, 3, 24 and 12 frames (0.07–0.8 s) | pwr |
| N4 | Diagonal dotted interference, flickering | B only: f1–269 | 79 runs (125 frames) | 66 runs/min pooled; ≈ 510 runs/min (≈ 8.5/s) within B; 45 % of B's in-flight frames | runs of 1–3 frames (max 7), each covering part of a field | pwr |
| N5 | Loss onset: partial-frame snow | C f174–175, D f208–209, F f250 | 3 | per loss event | 1–2 frames | rx (or rec) |
| N6 | Snow ↔ blue cycle | C f176–186, D f210–221, F f251–262 | 3 | per loss event | blue 4 frames → full snow 7–8 frames | rx |
| N7 | Hard cut to blue no-signal screen | A f424, B f310, E f263, H f629 (and the end of N6 in C, D, F) | 7 | per loss event | permanent to clip end (52–108 frames, 1.7–3.6 s); never recovers | rx |
| N8 | H-sync tearing, bottom of frame | H f628 | 1 | pre-loss only | 1 frame (the last picture frame) | link/rx |
| N9 | Frame stutter (duplicate frames) | all clips, clustered; densest E f249–262 (pre-loss) | 29 repeated frames (1.2 %) in 15 groups | 11 groups/min (5.3/min for groups of ≥ 2 repeats) | 1 frame each; a group is 1–5 repeats spaced 2–3 frames apart | rec (uncertain) |
| N10 | Macroblock smear | B f306–309 (pre-loss) | 1 | — | 4 frames | **enc** |
| N11 | Exposure jumps (sun occluded / revealed) | D f183 | 1 | frame-content driven | 1 frame (−13 % mean luma) | cam |

The pilot's words, mapped to what was measured:
- "sometimes noise" → N1 + N2.
- "sometimes stripes" → N3 (horizontal) and N4 (diagonal).
- "the whole image fully noised for a moment" → N5/N6. **In this footage full-frame noise appears only inside loss sequences**, never as an in-flight flash that recovers. See the uncertain list (U1).

### 1.3 Event detail

**N1 Live grain.** Fine luma and chroma noise over the whole picture.
- **Measured.** Grain is elongated horizontally: half-correlation length 3–6 px horizontally and 2–3 px vertically, i.e. about one source sample by one field line.
- **Re-encode masks it.** In flat bright sky the recordings keep only σ ≈ 0.9–1.3 levels luma (≈ 1.5–3 levels chroma), and the residual is 60–97 % correlated from frame to frame (B f2–4, G f1–4). A live analog grain would be uncorrelated, so the encoder has most likely flattened and frozen it. The true live level is therefore **not measurable from these clips** (U4). A higher reading on B f1 (σ ≈ 5.5) turned out to be the N4 interference pattern, not grain.
- **Low light.** None of the footage is low-light, so grain against light level is not measured (U5).
- **Candidate effect.** Per-field noise, low-passed horizontally to about one source sample and independent per field line. Luma σ is tunable, starting at 2–4 levels at normal exposure (above the re-encoded ≈ 1, since the encode suppresses it), and chroma noise of similar amplitude but about 5× wider horizontally. A new seed each field; never a static texture.
- **Driver.** Camera gain from the auto-exposure loop (**light level**), plus a small term from **motor current** (power-rail ripple; hypothesis, U6).

**N2 Chroma sparkles.** Isolated coloured dashes, one field line tall and 3–10 px long, in red, green, magenta and cyan, scattered over textured areas (B f122 ground; E, H grass and walls).
- **Measured.** A full-resolution scan of every picture frame counted pixels whose chroma departs more than 45 levels from its 7×7 local mean.
  - Per-clip median, in pixels per thousand: A 0.00, E 0.00, D 0.04, B 0.07, G 0.14, C 0.21, F 0.27, H 2.98.
  - Frame-to-frame variation is small (90th percentile ≤ 3× the median).
  - The only jumps are F f5–6 and G f112–118, and both are content changes (G ends diving into vegetation).
  - Density follows fine texture (H's grass), so this detector partly counts P5 cross-colour. Softer lilac specks on E's walls fall under the threshold.
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
  - C: 4 events, 38 frames (27 % of C's in-flight frames).
  - Every other clip: 0. A single-frame match in H f400 shares a row with the OSD and is not counted.
  - The per-frame metrics flagged only C f37, because the lines are thin against C's busy scene.
- **Candidate effect.** A set of thin dark lines, 1 source line tall and 15–30 levels deep, over the whole width. Spacing = field height × field rate ÷ interference frequency; position rolls each field by the frequency's offset from a multiple of the field rate.
- **Driver.** **Motor electrical frequency** (from motor RPM × pole pairs, or ESC switching harmonics) sets the spacing and roll speed. **Motor current** sets visibility. Throttle changes should make the lines crowd, spread and change roll speed.

**N4 Diagonal dotted interference, flickering.** First seen at B f123 and B f131, the only two frames the per-frame metrics flagged. A sky-region scan of every frame of A, B and G (the clips with open sky) shows it is **not rare in B**:
- **B.** Present in 125 of B's 279 in-flight frames (45 %), from f1 to f269. It comes in 79 runs, mostly 1 frame long (53 runs; 12 of 2 frames, 11 of 3, 2 of 4, 1 of 7) and mostly separated by a single clean frame (51 of 78 gaps). Examples: B f5–6 show it and f4 is clean.
- **A and G.** None. G is the same airframe group as B, so the source is specific to B's drone or flight. A's four candidate frames were prop-blade slivers.

Details:
- **What it looks like.** About 10 parallel lines falling left to right at a slope of about 0.45–0.5. In f123 they cover only the top 28 % of the picture and in f131 the top 44 %, so each burst lasts only part of one field (≈ 5–8 ms).
- **Timing.** Mostly alternate recorded frames, which suggests a source near half the field rate, or one gated to one field in two, sampled by the ≈ 30 fps recorder.
- **At 4× zoom.** Each line is a staircase of short dashes, one per field line, stepping ≈ 8 px right per line. The dashes alternate blue/white with yellow/green fringes, which is interference near the colour subcarrier beating against the line rate.
- **Visibility.** Only clearly visible over flat sky, but present over the ground too. So in clips without open sky (C–F, H) it cannot be ruled out.
- **Candidate effect.** A sinusoid added to the composite signal before decoding. Its frequency is near a multiple of the line rate plus an offset, which makes the diagonal and, through the chroma decoder, the colour dots. It is gated to a vertical span of 20–50 % of a field, on roughly every other field with random dropouts.
- **Driver.** An **airframe-specific on-board interference source.** Whether a flight has it is a per-flight random draw (1 of 3 airframe groups here). Its amplitude scales with **motor current**, and bursts are triggered by **current transients**. The pilot should say what B's drone carried (U7).

**N5–N7 Loss sequences (end of flight).** Two receiver behaviours are seen.

1. **Hard cut** (A, B, E, H). The last picture frame is followed directly by the solid blue no-signal screen, which stays. There are no snow or black frames in between. H's last picture frame shows N8 tearing. A's last picture frame shows a ≈ 20 % grain rise but no tear.
2. **Snow cycle** (C, D, F), identical in all three:
   1. **Partial-frame snow, 1–2 frames.** Snow replaces the picture above a horizontal split line, and the previous picture survives below it (bottom 10–18 %, in D f208 also a thin top strip). The first snow frame is **coloured**, the next is **monochrome**, so the colour killer engages within a frame.
   2. **Solid blue, exactly 4 frames.**
   3. **Full-frame monochrome snow, 7–8 frames,** with the receiver's own no-signal text drawn over it. So this snow is the receiver showing its raw input, not the camera.
   4. **Solid blue, permanently.**

   After loss the no-signal text switches to the receiver's own mode detection (content omitted).
- **Snow texture** (C f182, D f217 at 4×). Hard-clipped, near-binary white blobs on black, 1–2 field lines tall and ≈10–40 px long, ≈25 % white coverage (mean luma 53–65, neutral r = g = b). It is not grey Gaussian noise.
- **Candidate effect.** A receiver state machine (`PICTURE → PARTIAL_SNOW → BLUE(4) → SNOW(7–8) → BLUE`, or `PICTURE → BLUE`) driven by the link state. Snow: per-field thresholded noise, horizontally stretched, colour killed, with a small no-signal text overlay. Two blue-screen styles, as in §0.
- **Driver.** **Link lost** (fiber break or disconnection, signal below the receiver threshold), **power lost** (battery voltage collapse, or an impact that cuts power), or **camera/transmitter damage on impact.**
  - In this footage every loss follows an approach, landing or contact (D is a clear contact case).
  - Which variant plays may depend on how the signal dies. A gradual fade (fiber margin running out) gives snow first; an instant cut (power) gives a hard cut (U2).

**N8 H-sync tearing.** H f628, the last picture frame before blue.
- **What it looks like.** In the bottom ≈ 200 px (18 %), lines are displaced sideways by ≈ 20–60 px in a jagged zig-zag. The OSD rows there are sheared diagonally and doubled.
- **Analog, not codec.** The displacement is per line and not block-aligned.
- **Candidate effect.** A per-line horizontal offset in the lower part of the frame, amplitude rising towards the bottom, random per line, correlated over 2–4 lines.
- **Driver.** **Link margin** falling below the sync threshold, just before loss (and possibly **battery voltage** collapse). Its onset leads loss by at most 1 frame here.

**N9 Frame stutter.**
- **Measured.** 29 picture frames across all clips are exact or near-exact repeats of the previous frame (mean difference < 2 levels). They come in groups spaced 2–3 frames apart, e.g. A f190/193/196, D f166/168/171 and E f249/252/255/257/262. E's group sits in the last 0.5 s before loss.
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
| O4 | **Lens dirt** | See below | Two blobs, ≈ 60–100 px | Optional dirt mask composited out of focus. It accumulates, it is not a fixed overlay | Contact with vegetation or ground (impact events, including light brushes) and dust exposure (hypothesis, U12) |
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

## 5. Resolution feel and OSD layout

**Resolution feel, in short.** The feed carries far less real detail than its 1920×1080 container:

| Channel | Effective detail |
|---|---|
| Luma | ≈ 450 samples per line (P2) × ≈ 286 lines per field (P1) |
| Chroma | ≈ 90 samples per line (P4) |
| Time | 50 fields/s, each field a new vertical sampling phase |

It reads as coarse horizontal line-dashes, soft horizontal edges with dark halos, rainbow crawl on fine textures, and colour that floats off edges. The sim should produce its feed from that effective resolution and only then scale it to the display. That is also a large performance win (§6).

**OSD layout** (positions only, no values).
- **Rendering.**
  - A character OSD on a **30-column × 16-row grid** (each cell ≈ 64 × 67.5 px on the recording).
  - White glyphs with a black outline.
  - It is inserted on the drone before transmission, so it takes the analog treatment: soft horizontal edges (P2), coloured fringes (P4), tearing with the picture (N8, H f628) and cross-colour dots near busy texture.
  - It is **not** lens-distorted: the OSD horizon line stays straight while the real horizon bends (A f236).
  - Warning elements **blink** (≈ 7 frames on, ≥ 5 off; E).
- **Layout by airframe group.** Every group has these elements:
  - arming-status word, top-left
  - crosshair or aircraft symbol, centre
  - a bracketed status word, left edge
  - two battery lines (icon + voltage), bottom-left
  - a craft-name line, bottom-centre

  | Element | A | B, G | C, D, E, F, H |
  |---|---|---|---|
  | Top-centre | craft-name field | compass strip with tick marks and cardinal letters | — |
  | Top-right | altitude | distance, then altitude below it | altitude |
  | Left-middle / middle | throttle and pitch readouts, left-middle | status word left-middle, speed value right-middle | status word + flight timer above the battery lines |
  | Artificial-horizon dotted line | yes | no | yes |
  | Bottom-right | distance readout; warning text line bottom-centre | consumption and timer | current readout; link-quality or warning text line in the bottom row |
- **For the game.** The game-developer owns the OSD content (Train mode shows only what the pilot needs). The tech-artist owns putting it **through the feed pipeline before the analog stages**, so it degrades and tears like the real one.

## 6. Implementation notes for the feed (non-binding)

- **Order of stages:**
  1. Render the scene at camera resolution with extra FOV.
  2. Lens distortion, vignette, flare.
  3. Camera: AE/AWB, tone curve, clip, sharpening.
  4. Add the OSD.
  5. Composite encode: luma/chroma band-limit, cross-colour, interference (N3, N4), grain, sparkles.
  6. Field sampling (P1).
  7. Receiver: sync tearing (N8), snow/blue state machine (N5–N7), border (P8).
  8. Upscale to the screen.
- **Performance** is not measured here (no effect is built in this change). Working at ≈ 720 × 288 per field for stages 4–7 keeps the per-pixel cost to about 10 % of a 1080p pass. The cost of every effect goes in the verification notes of the implementation change, against the 60+ fps budget.
- **Not simulated:** N10 and P10 (re-encode), the recorder's frame-rate conversion, and N9 unless the pilot confirms it (U3).

## Uncertain — for the pilot to confirm

| # | Question | Why it matters |
|---|---|---|
| U1 | Do you see **full-frame noise flashes during flight that recover** (a few frames of snow, then picture again)? How often, and when (hard manoeuvres, far out, low battery)? | None exist in these clips: every full-frame snow is part of a loss. Without your answer the sim shows full snow only on loss |
| U2 | Is the **snow → blue → snow → blue** cycle (C, D, F) what your goggles do on a fiber break, and the **hard cut to blue** (A, B, E, H) what they do on power loss or a hard impact? Or is that difference between the two receivers? | Decides which variant each failure triggers |
| U3 | Does the goggle image **stutter or freeze** just before loss (E f249–262), or is that only the recording? | Simulate it or drop it |
| U4 | Is the grain on the goggles **visibly alive on flat sky**, and how strong: barely visible, or clearly "boiling"? The re-encoded clips flatten it to σ ≈ 1 level and freeze it, so the live level cannot be measured | Sets the base grain level (N1) |
| U5 | How does the picture change **at dusk, at night, or in dark interiors**? Grainier, more smear, colour loss? | Time of day is a user setting. There is no low-light footage |
| U6 | Does noise or interference **get worse at high throttle or with a heavy payload** (high current)? | Confirms the motor-current driver for N1–N4 |
| U7 | The **diagonal dotted lines** flicker through almost half of B's flight, but never in G (the same airframe group). What was different on B's drone: a payload-release servo, extra electronics, a damaged video cable or ground? Does it happen on some drones and not others? | The N4 driver, and how often a simulated drone should have it |
| U8 | The **rolling thin horizontal lines** in C change spacing and roll speed during the flight. Do they appear with throttle or motor changes, or on one particular drone? | The N3 driver |
| U9 | Is the **lens distortion** similar on all your drones? A measures k1 ≈ 0.33. Is the picture native 16:9, or is a 4:3 camera stretched? The measurement favours native 16:9 | One k1 or one per airframe |
| U10 | Were the losses in these clips all at **landing, drop or impact**? Does a **mid-flight fiber break** look the same? | Our loss model is built from end-of-flight footage only |
| U11 | **Chroma sparkles** (coloured specks on grass and walls): on the goggles, or only in the recordings? | Could partly be a recording artifact |
| U12 | **Lens dirt** after dusty landings: common enough to simulate? | O4 |
| U13 | Goggles no-signal screen: **blue** (as recorded) or configurable? | N7 look |

## 7. Simulation signals the feed needs (task 3.2)

This is the input to the later physics↔video interface change. "Field rate" means the feed samples the value once per video field (50 Hz). Physics steps at 1 kHz and should hand over per-field aggregates (mean, and max where noted) rather than raw samples.

| Signal | Unit | Update rate | Source | Feeds traits |
|---|---|---|---|---|
| Motor electrical frequency, per motor (or RPM × pole pairs) | Hz | 50 Hz (field mean) | physics | N3 spacing and roll |
| Motor current, per motor and total (a throttle/current proxy) | A | 50 Hz (field mean and field max) | physics | N3 amplitude, N4 trigger, N1/N2 power-rail term |
| Motor current step (d/dt of total current) | A/s | 50 Hz (field max) | physics | N4 burst trigger |
| Battery voltage under load, pack | V | 50 Hz | physics | Brownout: power-loss hard cut (N7), N8 tearing at collapse, OSD |
| Camera angular velocity (body rates at the camera) | rad/s (3 axes) | per rendered frame (≥ 60 Hz) | physics | C4 motion blur, P5 crawl |
| Camera vibration (acceleration at the camera mount, RMS in 3 bands: < 50 Hz, 50–200 Hz, > 200 Hz) | m/s² | 50 Hz | physics | Hypothesis only: intermittent sparkles and interference from connector microphonics (N2, N3). Kept as a hook until U6 or U8 confirms it |
| Impact event (peak acceleration, contact point, rigid-body impulse) | m/s² and N·s, per event | event, timestamped at physics rate | physics | Loss on hard impact (N5–N7), single-frame jolt, lens-dirt accumulation (O4) |
| Fiber tension at the spool exit | N | 50 Hz | physics (tether model) | Link margin → N2 sparkles, N8 tearing, N5–N7 loss |
| Fiber minimum bend radius along the paid-out length | m | 50 Hz | physics (tether model) | Link margin (macro-bend loss) |
| Fiber link state: intact / broken, plus optical margin | enum + dB | 50 Hz (break as an event) | physics (tether model) | N5–N7 loss sequence, N2 density, N8 |
| Scene light level (mean and centre-weighted luminance of the rendered frame, before exposure) | cd/m² (or EV) | per rendered frame | renderer (video module) | C1 AE → N1 grain, C3 saturation, C4 exposure time |
| Sun direction relative to the camera and sun visibility (occluded fraction) | unit vector + 0–1 | per rendered frame | renderer and sky (tech-artist) | O3 flare and veiling glare, N11 |
| Time of day | h (local solar) | on change (user setting) | game settings | Sky and sun, base light level |

Every driver named in §1–§4 is covered:

| Driver named in the traits | Signal(s) |
|---|---|
| Throttle / motor current | motor current; motor current step |
| Motor RPM / electrical frequency | motor electrical frequency |
| Battery voltage sag | battery voltage under load |
| Vibration | camera vibration (hook, pending U6/U8) |
| Impact | impact event |
| Fiber tension / bend / link margin | fiber tension, bend radius, link state + margin |
| Light level | scene light level; sun direction and visibility; time of day |
| Camera motion | camera angular velocity |
| Frame content, including the static camera and receiver properties (P1–P4, P8, O1, O2, O5) | (the rendered frame and depth buffer themselves; no signal needed) |
| Free-running random | (the feed's own seeded RNG, at the rates in §1.2; no signal needed) |
