# Tasks: p4-feed

Branch `unity/techart/p4-feed` off `unity/main`, once `p1-g1-look-lock-gallery` is merged (G4 flies the gallery). Tasks 1.1 and 1.6 may start before that, off `unity/main`: 1.1 touches only `Scripts/Feed/` and its tests, and 1.6 only `tools/reference/`. 1.2 to 1.5 follow 1.1 in sequence. 1.7 and 1.8 come last.

## 1. Build

- [ ] 1.1 [tech-artist] The scheduler and loss machine (design 1, 2):
  - **What:** `FeedEvents` and `FeedLoss` ported in C# with `System.Random(seed)`, `IFeedSignals` with labelled stubs, and a headless schedule dump.
  - **Verify:** the "same seed, the same schedule" scenario over 15 000 fields against the legacy C#'s dump.
- [ ] 1.2 [tech-artist] The camera rig and the pass (design 1, 3):
  - **What:** the FPV camera at field resolution with extra FOV and the intrinsics, and `video_feed.gdshader` as the HLSL full-screen pass, stage by stage, constants literal.
  - **Verify:**
    - the still A/B scenario against the Godot original (design 2; stop and report if Godot is not installed)
    - the lens scenario
- [ ] 1.3 [tech-artist] Losses, recovery, snow and the blue screens (FEED-05…07):
  - **What:** both no-signal styles, as built in the Godot feed.
  - **Verify:** the recovering-dropout scenario, snow statistics against K§11, and both blue screens.
- [ ] 1.4 [tech-artist] The OSD rendering (`FeedOsd`, `FeedFont`, the analog path):
  - **Verify:** the "grid matches the Godot OSD" scenario and the fringing against K§11.
- [ ] 1.5 [game-developer] The OSD content model (design 5):
  - **What:** OSD-03's fields from the sim state, with invented values.
  - **Verify:** the voltage scenario with a stub pack signal, and with `p6-flight-core`'s once it lands.
- [ ] 1.6 [reference-analyst] The frame-metrics tool (design 7):
  - **What:** one tool for footage and captured feed frames, with the local-jump outlier rule and burst extraction, plus the stable letter index (FEED-12, D-037).
  - **Verify:** it runs on two letters' frames and on a captured feed sequence, writing only to `reference/_compare/`, and the OPSEC scan stays clean.
- [ ] 1.7 [tech-artist] AE/AWB, night and lens drops (design 6, FEED-13):
  - **Verify:** the night scenario, and the AE and AWB τ against K§11.
- [ ] 1.8 [tech-artist] Performance and G4's build (design 8):
  - **What:** the feed's GPU time on AC at 1080p, and the test range player (`p6-test-range`, D-044) with the feed toggle and the review overlay.
  - **Verify:** the cost scenario, PLT-02 in the FPV view over the test range's posadka, and `Fpv450.Build.TestRange` with its smoke test.

## 2. Review and gate

- [ ] 2.1 [qa-engineer] review against spec scenarios:
  - every `analog-feed` and `osd` scenario, run by QA
  - the schedule and still A/B
  - the metrics against two letters, read on the local sheets (REAL-03)
  - the cost on AC
  - hygiene: nothing from `reference/`; OSD strings and invented values only
- [ ] 2.2 [user-review] **G4:** the pilot flies noclip through the test range (D-044), looking through the feed, and judges noise, losses, colour and the OSD. The orchestrator records their words in `docs/style-rules.md` and asks only the G4-tagged question in `docs/pilot-questions.md`.
