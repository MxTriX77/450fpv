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

## Risks / Trade-offs
- **HLSL against GDShader** differ in precision and in their derivative functions. The stage-by-stage still A/B catches drift.
- **The cost:** the Godot pass measured 0.33–1.0 ms at 1080p, unverified. The field-resolution render cuts it, and the budget is 1.0 ms.

## Migration Plan
- None: new code. The gallery gains the toggle.

## Open Questions
- G4's tagged question (`docs/pilot-questions.md`): which no-signal screen the pilot's goggles show. It is asked at G4 only, so both styles are built.
