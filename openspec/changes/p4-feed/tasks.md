# Tasks: p4-feed

Branch `unity/techart/p4-feed` off `unity/main`, once `p1-g1-look-lock-gallery` is merged (G4 flies the gallery). Tasks 1.1 and 1.6 may start before that, off `unity/main`: 1.1 touches only `Scripts/Feed/` and its tests, and 1.6 only `tools/reference/`. 1.2 to 1.5 follow 1.1 in sequence. 1.7 and 1.8 come last.

## 1. Build

- [x] 1.1 [tech-artist] The scheduler and loss machine (design 1, 2):
  - **What:** `FeedEvents` and `FeedLoss` ported in C# with `System.Random(seed)`, `IFeedSignals` with labelled stubs, and a headless schedule dump.
  - **Verify:** the "same seed, the same schedule" scenario over 15 000 fields against the legacy C#'s dump.

  **As built:**
  - *Where:* the engine-free core is `unity/Assets/_Project/Scripts/Feed/Core/` (asmdef `Fpv450.Feed.Core`, `noEngineReferences`, namespace `Fpv450.Feed`). `tools/feedcore` compiles those files for .NET. `tools/feedbench schedule --seed <n> [--fields <n>] [--current <0-1>] [--cut <field>[:abrupt]]` writes one row per field, and prints the summary, the digest and the stubbed signals to stderr. `tools/feedtests` runs `Tests/EditMode/Feed/` (asmdef `Fpv450.Feed.Tests`) outside Unity.
  - *The port:* `FeedEvents` and `FeedLoss` keep Godot's constants, draw order and structure. Each literal names its line in the verbatim copy. Godot 4.7.2's float `Mathf.Sin` and `Mathf.Pi` are `MathF.Sin` and `MathF.PI` (read off its GodotSharp).
  - *Unity's Mono:* it draws the same `System.Random` sequence. It evaluates float expressions in double precision, though, so the first port drifted a few ulps per field, and `N3Roll` accumulated the drift: the events matched, the bits did not. Every float operation is now cast to `float` (no-op on CoreCLR), and Unity matches bit for bit. New C# float math in the feed needs the same casts.
  - *The A/B:* the verbatim legacy files, compiled in a scratch project against Godot 4.7.2's own GodotSharp, dump the same rows. 12 runs of 15 000 fields: seeds 1–3, each timer-only at motor current 0 and 0.6, and with a faded and an abrupt cut at field 7500. Every diff is empty. The runs reach every state (Picture, PartialSnow, Blue, Snow, Black) and every tell (brightening, top tear, bottom tear, grain rise). EditMode pins ten of those digests (`FeedScheduleTests`). The Godot code gives 6.4–8.8 quality drops/min and 3.6–4.4 dropouts/min at seeds 1–3. Each drop's gap counts from its end, so the README's "about 10/min" reads high. The code stays unchanged.
  - *Signals:* `IFeedSignals` carries the K§11.8 list in its units (A, V, rad/s, dB, cd/m²…), with `SourceOf` per signal and `FeedSignalLog.Describe` for the log. Every signal on `StubFeedSignals` is a settable stub: a parked drone, no fiber (infinite margin and bend radius), a 6S pack at 25.2 V (Godot's), noon, no rain, a blank OSD. `FeedEvents.AdvanceTo` still takes Godot's 0–1 motor current. The amps-to-fraction scale and the dB-to-0–1 link margin are 1.2's.
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
  - **What:** the feed's GPU time on AC at 1080p, and the gallery player with the feed toggle and the review overlay.
  - **Verify:** the cost scenario, PLT-02 in the FPV view, and `Fpv450.Build.Gallery` with its smoke test.

## 2. Review and gate

- [ ] 2.1 [qa-engineer] review against spec scenarios:
  - every `analog-feed` and `osd` scenario, run by QA
  - the schedule and still A/B
  - the metrics against two letters, read on the local sheets (REAL-03)
  - the cost on AC
  - hygiene: nothing from `reference/`; OSD strings and invented values only
- [ ] 2.2 [user-review] **G4:** the pilot flies noclip through the gallery, looking through the feed, and judges noise, losses, colour and the OSD. The orchestrator records their words in `docs/style-rules.md` and asks only the G4-tagged question in `docs/pilot-questions.md`.
