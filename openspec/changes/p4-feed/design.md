# Design: p4-feed

## Context
- **The source:** the Godot feed is copied verbatim in `docs/legacy/video-feed-godot/`. Its README is the port spec: the shader pipeline step by step, the event scheduler, the loss machine, the OSD and the acceptance A/B. `docs/knowledge.md` §11 holds the measured parameters.
- **The renderer:** URP Forward+, with G1's look setup (`LookSetup`, four presets) on the gallery branch. MSAA stays 1× (G1 task 1.1).

## Goals / Non-Goals
- **Goals:** the feed the pilot chose, ported without drift, with the four additions the port spec names. G4's noclip view through it.
- **Non-Goals:** the proposal's.

## Decisions
1. **The port, by the README:**
   - **The shader:** `video_feed.gdshader` becomes one HLSL full-screen pass (a `ScriptableRendererFeature` on the FPV camera), with its uniforms as a C# settings asset. Constants are copied literally, and each block names its README step.
   - **The C#:** `FeedEvents`, `FeedLoss`, `FeedOsd` and `FeedFont` are ported into `Scripts/Feed/`. They keep the seeded `System.Random(seed)` the README requires: its legacy seeded algorithm makes the schedule A/B exact.
2. **The A/B:**
   - **The schedule:** the scheduler runs headless in .NET, and the Godot copy's schedule is dumped by the same C# from the legacy files, so no Godot install is needed.
   - **The stills:** these need the Godot original (`--video-seed`, `--video-field`), run from a read-only, detached worktree of `refs/godot/main` (never committed to, D-027). If Godot is not installed on the machine, the tech-artist stops and reports, and the orchestrator asks the pilot before any download.
3. **The camera rig:**
   - **The render:** a low-resolution target at field resolution (about 720 × 286 per field, assumed from K§11.5's effective resolution) with extra FOV (D-017), and the lens's k1 in the feed pass.
   - **The in-view stand-ins:** props and airframe until `p5-drone`'s in-view parts (DRN-04).
   - **The fallback:** if the reduced resolution breaks the still A/B, the full-resolution read stays, as Godot did (README).
4. **Signals:** `IFeedSignals` reads `p6-flight-core`'s `FeedSignals` when present, or a stub per signal, labelled. Until the physics thread exists, the gallery's noclip camera feeds the camera's angular rate.
5. **The OSD content:** a small model in `Scripts/UI/` (the game-developer's) maps the sim state to OSD-03's fields, with invented values where a real one would identify anything.
6. **The AE/AWB loop and night:** first-order loops on the scene's measured luma and white point, with K§11.4's time constants. Night is the pipeline's low-light end, not a separate pass.
7. **The metrics tool:** `tools/reference/frame_metrics.py` (the reference-analyst's), one tool for footage frames and captured feed frames, writing per-frame CSVs to `reference/_compare/` only.
8. **G4's build:** the test range player (D-044, which moved G4 there from the gallery) gains a feed toggle (on by default) on the noclip camera, plus the review overlay key: V and O (task 1.8).
9. **The camera's noise, not a link's (D-065, task 1.9).** The pilot: the noise is right, but "those black sticks" are unnecessary, because the link is a fiber, not a radio, and most noise comes from the analog camera's encoding. So each Godot effect is read by its stage in the chain (`video-feed.md` §0: cam, cvbs, pwr, link, rx), and only the link's free-running ones go.
   - **The "black sticks" are the tear's black wedge** (gd:382-385): black bars, 3 field lines tall and up to a tenth of the width, cut in from the right edge at the top of the field before a timer dropout (seed 1, fields 1365–1368, in the before stills), or at the bottom before a hard cut. Nothing else in the feed draws black bars: N2's dashes decode coloured, N3's dark lines run the full width, and the black outage is a band.
   - **Link, gone:**
     - *the free-running recovering dropouts* (N12, Godot's 3–6 a minute, D-026) and with them their tells (the brightening under dense impulse bands, the top tear) and their black or blue outages. A fiber does not drop out on a clock; its margin dip does (p7d-tether).
     - *the tear's black wedge*, in every path. The tear's sideways shear stays, so a fiber loss still has its tell (N8, H f628).
   - **Link, kept as the fiber's entry points** (nothing fires them on its own): the recovering dropout's sequence (`FeedLoss.StartDropout`, for a margin dip), the terminal loss (`FeedEvents.TriggerCut`, for a break: staged or hard cut by how fast the margin fell), and N2's link-margin term 0.010 (1 − margin)², which is 0 at full margin.
   - **Camera, kept as they are:** P1 field sampling, the sensor's apertures and P2, P3 sharpening, the PAL encode, the chroma trap and P4–P5 cross-colour, N1 grain, C3 saturation, the lift, contrast and banding, O2, P7, the OSD's insert and fringing (on board, before the encode). N2's sparkles keep their base, current and quality-drop terms: Godot put them down to the link, but the footage has them on every fiber clip at a density that follows fine texture, much of it the encoding's own cross-colour (N2), and they decode as coloured dashes.
   - **On board, kept:** N3's rolling dark lines and N4's diagonal dots, the drone's motors and ESCs on the camera's supply (pwr), seen on fiber drones C and B.
   - **Kept, with no measured cause:** the quality drops. Godot authored them, and every term they move is the camera's or the encoder's (grain, sharpening, saturation, black level, the depth of N2–N4), so they read as the camera's own excursions, inside the noise the pilot finds right.
   - **The receiver, kept:** snow, the blue screens and their text, reached only through the fiber's entry points; the P8 border.
   - **Godot's feed stays one switch away, for the FEED-00 A/B:** `FeedEvents.GodotLinkArtefacts` (`FeedSettings.godotLinkArtefacts`, `FeedCapture -feedGodot`, `feedbench --godot`) puts back the timer and the wedge, field for field. Off, the timer's first gap is still drawn, so the schedule is Godot's up to the field of Godot's first dropout; after it the same draws land on other fields, with the same statistics.

## Risks / Trade-offs
- **HLSL against GDShader** differ in precision and in their derivative functions. The stage-by-stage still A/B catches drift.
- **The cost:** the Godot pass measured 0.33–1.0 ms at 1080p, unverified. The field-resolution render cuts it, and the budget is 1.0 ms.

## Migration Plan
- None: new code. The gallery gains the toggle.

## Open Questions
- G4's tagged question (`docs/pilot-questions.md`): which no-signal screen the pilot's goggles show. It is asked at G4 only, so both styles are built.
- After D-065: whether the quality drops read to the pilot as the camera or as link fading (decision 9). If the latter, they are the next link effect to go.
