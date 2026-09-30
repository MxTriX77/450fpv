# Pilot requirements

Behaviour the pilot has stated from flying the real aircraft, given directly in conversation rather than derived from footage. This is first-hand knowledge that no reference clip shows well, so it is recorded verbatim first and interpreted second — **if the interpretation and the pilot's words disagree, the pilot's words win.**

Every item here is a requirement for the flight model, the video feed or the OSD. Whoever writes those OpenSpec changes must read this file and turn each item into scenarios with numbers a QA engineer can execute.

Source: the pilot, 2026-09-30. Their aircraft is a Vyriy-class heavy fiber-optic cargo quad.

---

## PR-1 Throttle-punch shake after arresting a fast descent

**What the pilot said.** "I noticed on my Viriy that when I suddenly need to lower altitude and when the alt is what I need, after I suddenly compensate with heavy throttle — the image vibrates for a mere 0.3–0.7 sec."

**The sequence, precisely.** Deliberate rapid descent → the target altitude is reached → a sharp, large throttle increase to arrest the descent → **the image vibrates for 0.3–0.7 s**, then settles. It is the *image* that shakes; the pilot did not describe the aircraft going anywhere it was not commanded.

**Candidate mechanisms** (not yet distinguished, and the model does not need to pick only one):
- The aircraft is descending into its own downwash. A sharp throttle step makes the rotors re-ingest disturbed, recirculating air, so thrust comes back unevenly and unsteadily across the four discs rather than smoothly. This is the region around vortex ring state; a heavy quad with a slung payload sits in it easily on a fast vertical descent.
- The throttle step is a torque step into a heavy airframe. Arms and the payload mount flex, and the frame rings at its own structural frequency until it damps out.
- Payload swing: a slung or bottom-mounted mass (including the fiber spool) excites a pendulum mode, which reacts back into the airframe.

**Requirement.** After a rapid descent is arrested by a sharp throttle increase, the simulation SHALL produce a brief, damped, high-frequency disturbance lasting **0.3–0.7 s**, visible primarily as camera shake rather than as a large trajectory error. Its presence and severity SHALL depend on descent rate, on how abrupt the throttle input is, and on payload mass — not fire on every throttle increase. It SHALL vary between occurrences rather than being an identical canned shake.

**This couples physics to the video feed.** The camera is rigidly mounted, so airframe vibration must reach the rendered image. The feed built on 2026-09-30 has no camera-shake input at all; `FeedSignals` must gain one, and the flight model must publish it.

**Open, only the pilot can answer:** does it happen with an empty aircraft or only when loaded? Does it depend on how close to the ground you are? Is it worse with the fiber spool full or nearly paid out?

---

## PR-2 OSD: copy the real layout, change two strings

**What the pilot said.** "OSD. You should base off of the OSD you see on my videos. Except don't use the real Viriy brand name, use **'Svinorez 10 Opto'**. Also there's '! ACTIVE !' label, instead it should say **'! SAFE !'**. Everything else should stay as it looks."

**Requirement.** The OSD SHALL reproduce the layout in the footage as described in `video-feed.md` §5 — the character grid, the positions, the fields and their formatting — with exactly two substitutions:

| In the footage | In the simulator |
|---|---|
| the real airframe's brand name | **`Svinorez 10 Opto`** |
| `! ACTIVE !` | **`! SAFE !`** |

Everything else stays as it looks. Do not redesign it, do not modernise it, do not "improve" the layout.

**Why the substitutions matter.** `Svinorez 10 Opto` is a deliberate stand-in so the simulator does not carry a real manufacturer's brand. `! SAFE !` replaces a live-armament indicator: this is a training simulator and must not present itself as armed. Neither is a style choice and neither should be reverted by someone tidying up later.

**Also true of the OSD, from the notes:** it is inserted *after* the lens, so **the OSD stays straight while the real horizon bends** (`video-feed.md`, A f236). When barrel distortion (O1) is implemented, the OSD must not be distorted with the picture. It is also inserted before the recorder, so it carries the same composite artifacts as the picture.

**OPSEC.** `video-feed.md` describes the OSD only as a layout and deliberately records no on-screen values. Keep it that way: reproduce the arrangement, invent the numbers.

---

## PR-3 Wind must rotate the aircraft, not just push it

**What the pilot said.** "About the wind logic — I want to make sure that it won't JUST make the drone drift towards the wind's direction, but also cause sometimes drone swirling and swerving in yaw axis, like you know its nose may lift up a bit suddenly AND turn in yaw axis in some direction, and when you try compensate — it can cause some intermediary control issues too. Bottom line — we must have top-notch realistic physics no one implemented in any FPV sim out there yet."

**The failure to avoid.** Wind modelled as a translational velocity field added to the aircraft's airspeed. That produces drift and nothing else, and it is what every FPV simulator already does. The pilot is explicit that this is not enough.

**Requirement.** Wind SHALL apply **moments as well as forces**. Specifically:
- **Yaw disturbance.** The aircraft SHALL sometimes swirl and swerve in yaw from wind alone, with no yaw input from the pilot.
- **Coupled pitch and yaw.** The described event is the nose lifting *and* the aircraft turning in yaw at the same time. These SHALL be able to occur together, as one event, not as two independent noise channels.
- **Correction is not clean.** When the pilot corrects such a disturbance, the correction SHALL be able to create further difficulty of its own — an intermediate, awkward state rather than an immediate return to trim.
- **Irregular.** These events SHALL be occasional and varied in direction, size and duration, not a periodic wobble. See the project's standing principle in `CLAUDE.md` §2 point 4: a tiny, cunning, elegant randomness rather than brute-force simulation.

**Mechanisms that would produce this honestly**, rather than a yaw wobble bolted on:
- **Gust gradients across the airframe.** When wind differs across the span, the four rotors see different inflow, so thrust and drag differ per rotor. Differential rotor drag is a yaw moment; differential thrust is pitch and roll. One gust front crossing the aircraft naturally produces a coupled pitch-and-yaw event, which is exactly what the pilot describes.
- **Rotational content in the wind field.** Real turbulence has vorticity. The wind grid should be able to carry rotation, not only a vector per cell — particularly in the wakes of tree belts and buildings, where `wind.md` already puts the worst turbulence.
- **Asymmetric disc loading.** In translational flight the advancing and retreating sides of each rotor see different relative airflow, which produces moments that change with airspeed and direction.
- **The fiber tether.** A wind-loaded catenary pulls from a point that is not the centre of mass, so it contributes its own yawing and pitching moment that changes as the line pays out.

**Acceptance is behavioural, and the pilot is the judge.** The measurable targets in `wind.md` (Calm / Windy / Severe, D-011) bound the magnitudes. Whether it *feels* right is decided by the pilot flying it, and the scenarios should be written so that the honest answer to "does wind ever turn the aircraft without pilot input" is a yes with numbers attached.

---

## The standing bar

The pilot's closing line applies to everything in this file and to the flight model as a whole: **"we must have top-notch realistic physics no one implemented in any FPV sim out there yet."**

The manifesto's version of the same point (`CLAUDE.md` §2) is that real flight always feels slightly random, that exhaustive physics is not affordable, and that the goal is therefore a small, well-chosen, well-placed randomness that reproduces the feel. Randomness must still be seeded and reproducible (D-010), so that a recorded flight can be replayed and a crash can be explained rather than shrugged at.

---

## PR-4 The loss of picture is staged, and the shipped version is wrong

**What the pilot said** (2026-09-30, after flying the merged feed). "That occasional FULL cut off — it's too sharp now, like it just appears and just disappears, and it's simple-ahh noise."

**They are right, and `video-feed.md` already has the correct behaviour.** What shipped in D-013 snaps to flat snow and snaps back. The footage never does that.

- **Staged loss** (N5–N7, clips C, D, F), in the pilot's own words matched to frames: *"very quick picture glitch"* = partial-frame snow, 1–2 frames → *"brief flashy moment"* = blue interlude, exactly 4 frames → *"noisy and flashy"* = full snow, 7–8 frames → *"finally blue"* = the no-signal screen.
- **Hard cut** (A, B, E, H): straight to blue, but in 2 of 4 the last 1–2 picture fields still carry a pre-cut glitch — N8 tearing (top rows shear sideways, black wedge from the right edge) or a ≈10 % grain rise.
- **Recovering dropout** (N12, clip P, severe wind and rain) — **this is the model for the pilot's "occasional short cut"**: a precursor in 2 of 4 events of 1–3 frames of brightening (+25 % to +120 % mean luma) under dense bands of coloured impulse dashes, or an N8-style tear; then blue about 20 frames (0.67 s); then straight back to picture with the receiver text.

**Requirement.** Short losses SHALL follow N12: precursor → blue no-signal screen → recovery, with randomised durations inside the measured ranges. There SHALL always be a tell before the picture goes. The snow SHALL carry coloured impulse-dash texture (N2), not flat monochrome noise. The blue screen is a real screen with receiver text, in the two variants `video-feed.md` §0 measures, not a blue fill. Terminal losses SHALL use the staged sequence or the hard cut, fired by events (impact, power loss, fiber break) through `FeedEvents.TriggerCut()`.

---

## PR-5 Objects need destruction detail, and it is physical

**What the pilot said.** "In general, we need a bit more details. Windows, trenches etc. are fine, but they are of straight form (like window which is JUST a rectangle). Jagged chunks of brickwork jut out from the windows, and in some places, you might see a bit of rebar sticking out, and so on. Remember, in our simulator, every little texture detail will affect the physics."

**Requirement.** Openings in damaged structures SHALL NOT be clean rectangles. Window and door openings SHALL carry broken edges — jagged brickwork jutting inward, missing courses, rebar protruding — and that geometry SHALL be physical, not a texture or a normal map. The manifesto (§4 item 1) already demands micro-detail that interacts with the quad; this is that requirement applied to structures.

**Consequence for the gap model.** A catalog gap is currently an upright rectangle (yaw only). A real broken window is not rectangular and not necessarily upright. The flyable opening must be the *actual* clear space between the jagged edges, not a rectangle drawn over them, or the pilot will clip geometry the sim believes is open. This is the same limitation already recorded against the house's roof bay and gable breach.

### Immediate defects the pilot named
- **Wall tiling.** Still unfixed: per-panel materials alternate instead of forming patches, each panel crops its texture at its own UV offset, and two clay materials are too close in hue, so seams land on hard squares.
- **Brick textures are upside down.** Asset bug, not a style question.

---

## PR-6 Power lines need their own physics, and thickness matters

**What the pilot said.** "Power lines need to have their own physics so they interact with the drone realistically — for instance, if it flies into one. We have to keep in mind, though, that a drone getting tangled in thin wires behaves differently than it does with thick cables."

**Requirement.** Wires SHALL be simulated as physical lines, not static collision capsules. A strike SHALL depend on the wire's diameter and tension:
- **Thin wire** — tends to catch, wrap and tangle in the props; the aircraft is snagged and dragged rather than bounced, and may be held. `snag_hazard` already exists in the catalog for this.
- **Thick cable** — stiffer and heavier; the aircraft is more likely to be deflected, stopped or flipped than to wrap it.
The map already carries wires as sag polylines with real catenaries, so the geometry is there; what is missing is the strike response. This interacts with the fiber tether model, which is a tensioned line on the same aircraft.

---

## PR-7 Build the objects from the footage, and research the real thing

**What the pilot said.** "In short, look at the footage from my videos and intelligently reconstruct the scenes to scrape objects out of them. If necessary, search online to see what structures in Ukraine actually look like. Take houses, for example: you have information on what they are built of, what they looked like before the war, and how they appear now following the destruction (again, you can find images of destroyed buildings online yourself)."

**Requirement.** Object work SHALL be driven by the footage-derived notes first, and SHALL be supplemented by researching what these structures actually look like — construction, pre-war appearance, and how they fail when hit. An asset that passes its budget but does not read as rural Ukraine to this pilot is a failed asset.

**OPSEC boundary, unchanged.** `reference/` is never opened, copied, committed or uploaded. The derived notes in `docs/reference-notes/` are the sanctioned source. Online research is for general reference on Ukrainian construction and war damage — never for anything that could identify a place, unit or date from the footage.

---

## PR-8 The ground is not flat

**What the pilot said** (2026-09-30, sharing a short clip of a stable feed). "It also gives you idea on terrain, grass, and trees — it's not always flat, sometimes there are ups and downs, our map eventually should also have this."

**What the clip shows.** Gently rolling ground: a low crest running across the middle distance with the land falling away behind it, shallow dips in the foreground, and the horizon sitting well above the base of the far treeline rather than level with it. The grass is pale and matted with green patches through it, not a uniform sward. Bare deciduous trees with fine twig crowns, scattered scrub, low outbuildings, and concrete-slab tracks cutting across the field.

**Requirement.** The world map SHALL have real relief — gentle rises, crests and dips at the scale of tens of metres — not a flat plane with surfaces painted on it. This matters for flight, not only for looks: a crest hides what is behind it, a dip changes how far you can see and where wind separates, and a heavy cargo quad on a slow glide approach meets ground that is not level.

**Where this stands today.** `sample_patch` is flat because it is a 256 m test patch for the map format, not a landscape. The synthetic 4 km package already generates steppe relief, crater fields, a gully and road embankments, so the terrain renderer and the physics handle relief; nothing in the shipped map exercises it. `build-world-map` must, and the review map should carry at least one patch with relief so it is reviewed before the world is built.

**Also visible and worth building:** the pale matted grass with green showing through (our meadow is more uniformly green), fine-twigged bare crowns as a seasonal variant of the belt trees, and concrete-slab tracks, which are a distinctive and simple asset.

---

## PR-2a The OSD must match exactly, and the grid is not what you would guess

**What the pilot said** (2026-09-30, on seeing the first OSD): "the layout should look exactly the same, and the fonts also, and the crosshair also."

Exactly, not approximately. The first attempt was built from a description and was wrong in every dimension. The second was measured at 1:1 against frames of a clip the pilot shared, and the measurements are recorded here because they are not derivable from the picture size and will otherwise be "fixed" back to something plausible and wrong.

**The character cell is 67.3 × 71.0 px on a 1920 × 1080 frame**, measured from glyph centres: advance 67.3 (across six letters), row pitch 71.0 (three consecutive gaps, all exactly 71), grid origin inset half a cell. A 30 × 16 grid at that pitch covers **2019 × 1136 px — larger than the picture.** The last two columns and the last two rows fall outside the frame. This is not a bug and it is why the bottom-left stack sits where it does. Dividing the frame by 30 × 16 instead puts every row and column in the wrong place.

**The glyph is 5 × 8 samples inside a 14 × 18 cell** — small, narrow, widely spaced, ink about 23–24 × 29–30 px. Not a cell-filling interface face. Cap height is ~30 px, not ~52.

**Two decoder facts the comparison proved**, both of which look like defects until you see why:
- **The scene smears horizontally but the OSD stays crisp.** The softness comes from the sensor aperture *before* the composite encode. If the softness is instead put in the decoder's luma path, it notches the subcarrier and eats thin bright strokes — an OSD stroke comes out a 1 px grey sliver. A real receiver **traps** the chroma: luma is the composite minus the chroma just detected, which keeps the bandwidth.
- **The OSD does not rainbow, while foliage of the same contrast does.** A glyph is identical on every line it spans, and the PAL delay line rejects exactly that. An OSD fed hard-edged into the chroma detector tears itself into rainbow stripes; band-limiting it into the detector brings fringing on OSD rows from 105 down to 7.7 against the footage's 7.9.

**OPSEC.** The clip was decoded to a session scratchpad and never entered the repository. Layout, glyph geometry and positions are recorded here; **no on-screen value is**, and none appears in code, comments or commit messages. The receiver's text lines and the timer's unit label are invented marks placed at the measured footprint.
