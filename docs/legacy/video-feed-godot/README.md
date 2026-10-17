# The Godot analog feed: port it faithfully

**Pilot, 2026-10-17:** the Unity feed's noisiness algorithms must be **"very much similar to those under the godot codebase"** (D-026).

This folder holds that codebase, copied verbatim from `MxTriX77/450fpv` `main` (`b684cce`):

| File | Lines | What it is |
|---|---|---|
| [shaders/video_feed.gdshader](shaders/video_feed.gdshader) | 418 | **The feed the pilot chose** ("mixed"): one full-screen pass, a simulated PAL composite chain plus the receiver's loss states. **The main thing to port.** |
| [src/FeedEvents.cs](src/FeedEvents.cs) | 165 | The feed's seeded event scheduler, stepped once per 50 Hz field: quality drops, recovering dropouts, N3 and N4 |
| [src/FeedLoss.cs](src/FeedLoss.cs) | 260 | The receiver's loss-of-picture state machine: recovering dropout, staged loss, hard cut |
| [src/FeedOsd.cs](src/FeedOsd.cs) | 150 | The OSD character grid, laid out exactly as measured from the footage |
| [src/FeedFont.cs](src/FeedFont.cs) | 153 | The OSD character ROM (5 × 8 glyphs in a 14 × 18 cell) and its atlas |
| [src/FeedSignals.cs](src/FeedSignals.cs) | 58 | The stand-in signal drivers (gain, link margin, motor current, pack volts) |
| [src/VideoFeed.cs](src/VideoFeed.cs) | 260 | The Godot layer that wires them together, plus the review flags (`--video-seed`, `--video-field a-b[+cut\|+hardcut]`, V to cycle modes) |
| [shaders/video_chain.gdshader](shaders/video_chain.gdshader), [video_authored.gdshader](shaders/video_authored.gdshader), [video_hybrid.gdshader](shaders/video_hybrid.gdshader) | ~150 each | The three spike candidates the pilot compared (chain, authored, hybrid). **Reference only**: "mixed" is chain-based, held to the authored version's restraint, and slides into the heavy look of hybrid |

**Run the original:** the Godot attempt still builds from `main`. Two commands are useful for comparisons:
- `& $env:GODOT --path game -- --map sample_patch --video-seed 1 --video-field 600-700` saves one PNG per field.
- Add `+cut` or `+hardcut` to the field range to fire a terminal loss.

## What "very much similar" means
**Port the algorithms, constants, rates and structure as they are.** Godot-specific plumbing (canvas layer, `ShaderMaterial` uniforms) becomes its Unity equivalent: a URP full-screen pass, or a `ScriptableRendererFeature` with an HLSL port. The look must not drift.

### 1. The shader pipeline (per output pixel, `video_feed.gdshader`)
1. **Field sampling (P1).** 286 active lines per field. The field parity alternates every 1/50 s (bob deinterlace). The whole feed is a function of `(seed, field)`.
2. **The N8 line-sync tear** shifts where the whole chain reads from. It is correlated over 3 lines, and its amplitude rises toward the torn edge (top for a dropout tell, bottom for a hard-cut tell). It also cuts a black wedge in from the right edge.
3. **18 band-limited sensor reads** along one field line, paired into **17 composite taps** at 920 samples per line. The pair is the horizontal sensor aperture. The vertical aperture is 0.15 field lines.
4. **P3 sharpening before the encode:** strength 0.30, radius 3 samples, ×(1 + 1.6·degrade).
5. **The OSD is inserted on the drone, before the encode:** white glyphs (0.96) with a black outline (0.02).
   - The luma path gets the hard-edged OSD.
   - The chroma path gets it smeared over 13 samples, with only 10 % (`OSD_COMB`) hard-edged, as the delay line leaves it. **That gives OSD fringing of about 7.9, as in the footage.**
6. **PAL encode:** U = 0.493(B − Y), V = 0.877(R − Y), on a subcarrier of 230 cycles per active line, 0.75 cycles of phase per line, V switched per line.
7. **On the composite signal:**
   - **N1 grain:** σ = 0.011 × gain × (1 + 2.2·degrade) × (1 + grain_boost), flat over one luma sample, a new pattern every line and every field.
   - **N2 impulses:** rate = (0.0002 + 0.010·(1 − link)² + 0.001·current + 0.004·degrade) × texture gate. They land as chroma-band bursts, so they decode as coloured dashes. The tell adds dense bands of them every 24 lines.
   - **N3:** one dark line every `n3_spacing` (≈ 40) field lines, the set rolling, flat and colourless.
8. **Decode:**
   - **Y** uses a **chroma trap** (composite minus the detected chroma), not a low-pass. This keeps one-sample OSD strokes solid.
   - **U/V** use synchronous detection with a 13-sample Hann window (wide) and an 8-sample window (fine).
   - **Cross-colour** = CROSS 2.6 × (fine − wide), with V kept at 0.35 (the delay-line residue), so the fringes read blue-violet.
9. **On the decoded picture:**
   - **N4** diagonal dotted interference, authored: a dash staircase stepping about 8 px per line, gated to the top `n4_span` of the field.
   - **Saturation** 0.78 − 0.32(gain − 1) − 0.35·degrade.
   - **Degrade** pulls the gain and black level.
   - **The tell** brightens everything, the OSD included.
   - **The composite look:** lift 0.05, contrast 0.90, faint vertical banding 0.006.
   - **Vignette** 12 %.
   - **P7** lilac clip above 0.86.
10. **Receiver states:**
    - partial snow above the split, with an optional top strip
    - the no-signal blue (0, 0.039, 0.784)
    - full snow
    - the black dropout between rows `black_a` and `black_b`
    - snow is near-binary dashes (about 20 % white, a level drawn per field at 52–65/255, a faint herringbone, colour only in the first partial-snow fields)
    - the receiver's green text appears **only on blue and snow, never over a recovered picture**
11. **P8 border:** 4 black rows at the top, 2 at the bottom.

The **hash** is the precision-safe one in the shader (`fract`-based, inputs kept small, field taken mod 1024). Port it exactly. A sin-based hash degenerates into a woven pattern as the field counter grows.

### 2. The event scheduler (`FeedEvents.cs`, per 50 Hz field, seeded `System.Random(seed)`)

| Event | Rate and shape |
|---|---|
| **Recovering dropouts (N12)** | **The pilot asked for 3–6 per minute, as a random event.** Gaps of 5–32 s, drawn as min + (max − min)·u², mean about 14 s. Staged by `FeedLoss`. Never fired while a loss is running |
| **Quality drops** | Gaps of 1–15 s (u² shaped, about 10/min); each lasts 0.3–2.2 s with depth 0.25–1.0, under a sine envelope, so the picture slides into the heavy look and back out |
| **N3 rolling lines** | Onset 50/min. Runs of 3–40 fields. Spacing 39–42 field lines. Roll 2–9 lines per field. Amplitude (0.045 + 0.05·current)(1 + degrade) |
| **N4 diagonal bursts** | Onset 8.5/s. Runs of 2–5 fields. Span 20–50 % of the field. Amplitude (0.022 + 0.02·current)(1 + 1.5·degrade) |
| **N3 and N4 presence** | **Both forced on** (`HasN3 = HasN4 = true`) as part of the look the pilot chose; the footage alone would give p ≈ 1/3 per flight |
| **Terminal losses** | **Never on a timer.** `TriggerCut(abrupt)` is called by events: impact, power loss, fiber break |

### 3. The loss state machine (`FeedLoss.cs`; durations in fields)
- **Recovering dropout:** a tell of 2–5 fields, then either black (25 %, 3–4 fields, entering and leaving at 40–70 % of the height) or blue (18–35 fields), then the picture, **clean**.
  - The tell is brightening of +25 % to +120 % luma under dense impulse bands, or (1 in 3) a tear at the top of the field.
- **Staged terminal** (p = 0.8 if the margin faded, 0.2 if it vanished):
  - partial snow for 1–5 fields (split at 0.80–0.90, drifting down 0.015 per field; a top strip of 0.11 in 1 of 3)
  - blue for 5–8 fields
  - full snow for 10–15 fields (level redrawn every 1–2 fields; chroma floor 0.08)
  - blue forever
- **Hard cut:** a tell of 2–5 fields (a tear at the bottom, 50 %, or a +10 % grain rise), then blue forever.
- **There is always a tell before the picture goes** (PR-4).

### 4. The OSD (`FeedOsd.cs`, `FeedFont.cs`)
- **Grid:** cell 67.3 × 71.0 px at 1920 × 1080, origin inset (33.7, 33.5) px, 30 × 16 cells, plus 2 rows of receiver text.
- **Rows:** 0 mode and altitude; 7 the reticle; 10 the arming indicator; 11 the timer; 12–13 battery, name and current.
- **Blink:** 12 fields on, 9 off.
- **The ROM:** 5 × 8 glyphs in a 14 × 18 cell, capitals only, with a one-texel dilated outline.
- **Port with one change:** the Godot code says `Svinorez 10 Opto`. The Unity port uses **`Svinoriz 10 Opto`** (D-011). Because the name is a different length, recheck the column placement of the number to its right.

## What may change, and what may not
**May not change without the pilot's approval:** the algorithms and constants above, the event rates (including the pilot's 3–6 recovering dropouts per minute and about 10 quality drops per minute), forcing N3 and N4 on, the loss sequences, and the OSD geometry.

**Must change (the Unity port's job):**
- **Svinoriz:** the airframe name, as above.
- **Signals:** `FeedSignals`' camera-derived stand-ins become `IFeedSignals` stubs (labelled `Stub*`) in phase 4. Physics replaces them in phases 6–7. The scheduler's timers stay. The link-margin, impact and fiber signals **add** physics-driven events and modulate N2, but don't replace the pilot's chosen baseline rates.
- **Battery:** the stand-in pack is 6S; follow the battery chosen in phase 5.

**Additions** are layered on without altering the ported output when they are off:
- **Lens distortion (O1):** k1 ≈ 0.33, applied to the scene read only (`sensor_read`), never to the OSD. The shader's own comment says so.
- **AE/AWB loops (C1/C2):** these drive `gain`, colour balance and exposure time.
- **N13 dim-light steps.**
- **Rain drops on the lens (FEED-13).**
- **Rendering the FPV camera at reduced resolution** (D-017). Only if the A/B check below still matches; if it doesn't, keep reading the full-resolution scene as Godot did.

## Acceptance: A/B against the original
- **The event schedule is identical.** For the same seed, the Unity `FeedEvents`/`FeedLoss` port produces the same schedule field by field. Both use .NET's seeded `System.Random(seed)`, which keeps its legacy algorithm for seeded use. Verify by dumping 15,000 fields (5 minutes) from both and diffing. If Unity's runtime gives a different sequence, port .NET's seeded algorithm (Knuth's subtractive generator, from the .NET reference source) into our own code and use that. The feed's randomness must never depend on the runtime.
- **Stills match.** For the same seed and field, Unity and Godot stills match visually (stage by stage: baseline, a deep quality drop, N3, N4, every loss state, the tell, the tear, the OSD). Captured with Godot's `--video-seed/--video-field` and the Unity equivalent. Small GPU float differences are acceptable; a different look is not.
- **Then G4:** the pilot looks through the Unity feed.
