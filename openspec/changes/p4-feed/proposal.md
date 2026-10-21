# Proposal: p4-feed

**Owner:** tech-artist · **Reviewers:** game-developer (the OSD content model, the camera in the flight session), physics-engineer (the signals the feed reads from `FeedSignals`), reference-analyst (the frame-metrics tool on the footage), qa-engineer
**Requirements:** FEED-00…13, OSD-01…04, PLT-02 (the FPV view's frame rate) · **Decisions:** D-008, D-011, D-017, D-020, D-021, D-026, D-042 · **VISION:** §1 (the pilot judges through the goggles), §2 (an analog PAL feed with its noise, losses and colour)
**Source:** `docs/legacy/video-feed-godot/` (the Godot feed the pilot chose, copied verbatim, with its port spec); `docs/knowledge.md` §11 (the signal chain, noise events, loss state machines, pixel and colour, the exact OSD, the pipeline order, the signals).

## Why
G4 is next after G1 (D-042). The pilot sees the world through an analog PAL feed, and wants its noise "very much similar" to the Godot feed (D-026). This change ports that feed faithfully, A/B-checked against the original, and adds only what the port spec says it lacked: field-resolution rendering, a real AE/AWB loop, lens distortion, the staged losses with blue screens, and the physics signals in place of the stand-ins.

## What Changes
- **The FPV camera rig** (FEED-02): a low-resolution render at field resolution with extra FOV (D-017), the pinned intrinsics (about 106° horizontal, 16:9, f ≈ 1.005 before k1, 0° uptilt), and in-view prop and airframe stand-ins until `p5-drone`.
- **The port** (FEED-00):
  - `video_feed.gdshader` as a URP full-screen pass in HLSL
  - `FeedEvents`, `FeedLoss`, `FeedOsd` and `FeedFont` ported in C#
  - the algorithms, constants, rates and structure kept as they are
  - A/B against the original: the same seed gives an identical event schedule field by field, and matching stills stage by stage
- **The signals** (FEED-03): the feed reads `FeedSignals` (`p6-flight-core` design 1) through `IFeedSignals`, with labelled stubs until the physics fills each one.
- **The traits and states:**
  - the K§11.7 pipeline order at 50 fields/s (FEED-02, FEED-04)
  - staged loss, hard cut, recovering dropouts with clean recovery (FEED-05, FEED-06)
  - the snow texture and the receiver's blue screens (FEED-07)
  - AE and AWB loops, and night as the pipeline's low-light end (FEED-08)
  - rain drops on the lens (FEED-13)
  - all seeded and tunable (FEED-09)
- **The OSD exactly** (OSD-01…04):
  - the character grid, the font and the crosshair
  - "Svinoriz 10 Opto" and "! SAFE !" with every value invented
  - its content from the sim state
  - the analog path's luma and chroma treatment
- **Review and acceptance:**
  - the review tools (FEED-11)
  - the frame-metrics tool run alike on the footage and on captured feed output
  - the local A/B and footage comparison sheets (FEED-12)
  - the cost at most 1.0 ms at 1080p (FEED-10)
- **G4's build:** the gallery with the feed on the noclip camera.

## Capabilities

### New Capabilities
- `analog-feed`: the camera rig, the ported pipeline and its traits, the loss states and recovery, snow, AE/AWB, night, lens drops, seeding, signals, performance, review tools and acceptance
- `osd`: the exact grid, font and strings, the content from sim state, the analog path

### Modified Capabilities
- None.

## Non-goals
- **The physics filling every signal:** this change reads what `p6-flight-core` publishes and stubs the rest, labelled. The fiber's link signals come with 7d.
- **Retuning the Godot look:** the port must not drift (D-026). Changes after G4 come from the pilot's words.

## Impact
- **New code:** `unity/Assets/_Project/Scripts/Feed/`, `Shaders/` (the feed pass), `Settings/` (the feed renderer feature), and `tools/reference/` (the frame-metrics tool).
- **Consumers:** the G1 gallery (G4's noclip view) and `p6-input-app`'s flight session (the FPV camera).
