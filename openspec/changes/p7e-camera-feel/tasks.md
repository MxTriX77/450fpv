# Tasks: p7e-camera-feel

Branch `unity/techart/p7e-camera-feel`, cut from the player's branch (`unity/game/p6-input-app--player`, `657fde59`), where FlightScene and the feed live together. The OSD files on that branch belong to `p4-feed` 1.5's agent and stay untouched.

## 1. The camera's feel

- [x] 1.0 [tech-artist] The change: proposal, design, the `fpv-camera` spec delta and these tasks; `openspec validate p7e-camera-feel --strict`
- [x] 1.1 [tech-artist] Measure where the smoothness comes from: `CameraFeelProbe` (sim tests, explicit) in a hover and a punch, still and severe air, 0 and 2 kg; the numbers in design 1
- [x] 1.2 [tech-artist, physics-engineer review] The sim's side, output only: `PoseFrame.CameraTurn` from `AirframeModes.FrameTurn`, `PoseBuffer`'s 128-step history (`TryRead`, `LatestStep`), `FieldCamera` (the field's index, its reference instant, the camera's attitude at any instant of the history); sim tests for the history (wrap, lap, torn reads), the turn and the tracking scenarios
  - As built: `FieldCameraTests` (6 tests). Over the punch the field camera's rate error is 0.00 % of the 0.200 rad/s spread at 60 and 144 Hz, every field drawn once; the render clock's rigid pose misses by 40.5 % (60 Hz) and 24.1 % (144 Hz), the 30 ms lag by 43.7 %. A writer at full speed against a reader: 1.35 M steps, 0.47 M reads kept, none torn. The full sim suite: 309 passed.
- [x] 1.3 [tech-artist] `Feed/Core/RollingShutter.cs`: PAL row timing, the exposure from the light (the stand-in while unmeasured), the per-row taps into the 9 × 287 track; feed tests for the skew, daylight's single tap, night's eight and the still drone's identity
  - As built: `RollingShutterTests` (5 tests): 300 °/s skews the first line against the last by 5.491°; the feed suite: 42 passed.
- [ ] 1.4 [tech-artist] `FlightScene`: the field latch in the FPV view (the scene drawn once per complete field at its reference instant, held between), the feed's field driven by the flight's, the track to the rig; the chase view and the preview as before
  - Code in (`Latch`, `NewField`, `FillSamples`, `Unlatch`) and compiled against Unity 6000.3.26f1's assemblies with the project's scripts; its Unity run is owed (1.7).
- [ ] 1.5 [tech-artist] The feed's read: `FpvCamera` uploads the track, `FeedFeature` binds it, `Feed.shader` warps and blurs each row's scene reads; `FeedSettings.shutter` (on) and its 4° render margin; off is the old read
  - Code in; the C# compiles against Unity's assemblies; the shader's compile and the picture are owed (1.7).
- [ ] 1.6 [tech-artist] An App EditMode test: the field latch draws each field once at its reference pose and the chase view every frame (owed: Unity run)
  - Written: `FieldLatchTests` (App) and `FpvCameraTests.ARigWithATrackReadsThroughIt` (FeedRig); both compile, neither has run in Unity yet.
- [ ] 1.7 [tech-artist] Verify in Unity once the GPU is free: EditMode, both players' `-smoketest -windowed -smokeshots` PASS, before/after frame sequences in a hover and a punch-out, the feed's GPU cost per PLT-07 (owed)
- [ ] 1.8 [qa-engineer] review against spec scenarios
