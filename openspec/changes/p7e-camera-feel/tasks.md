# Tasks: p7e-camera-feel

Branch `unity/techart/p7e-camera-feel`, cut from the player's branch (`unity/game/p6-input-app--player`, `657fde59`), where FlightScene and the feed live together. The OSD files on that branch belong to `p4-feed` 1.5's agent and stay untouched.

## 1. The camera's feel

- [x] 1.0 [tech-artist] The change: proposal, design, the `fpv-camera` spec delta and these tasks; `openspec validate p7e-camera-feel --strict`
- [x] 1.1 [tech-artist] Measure where the smoothness comes from: `CameraFeelProbe` (sim tests, explicit) in a hover and a punch, still and severe air, 0 and 2 kg; the numbers in design 1
- [ ] 1.2 [tech-artist, physics-engineer review] The sim's side, output only: `PoseFrame.CameraTurn` from `AirframeModes.FrameTurn`, `PoseBuffer`'s 128-step history (`TryRead`, `LatestStep`), `FieldCamera` (the field's index, its reference instant, the camera's attitude at any instant of the history); sim tests for the history (wrap, lap, torn reads), the turn and the tracking scenarios
- [ ] 1.3 [tech-artist] `Feed/Core/RollingShutter.cs`: PAL row timing, the exposure from the light (the stand-in while unmeasured), the per-row taps into the 9 × 287 track; feed tests for the skew, daylight's single tap, night's eight and the still drone's identity
- [ ] 1.4 [tech-artist] `FlightScene`: the field latch in the FPV view (the scene drawn once per complete field at its reference instant, held between), the feed's field driven by the flight's, the track to the rig; the chase view and the preview as before
- [ ] 1.5 [tech-artist] The feed's read: `FpvCamera` uploads the track, `FeedFeature` binds it, `Feed.shader` warps and blurs each row's scene reads; `FeedSettings.shutter` (on) and its 4° render margin; off is the old read
- [ ] 1.6 [tech-artist] An App EditMode test: the field latch draws each field once at its reference pose and the chase view every frame (owed: Unity run)
- [ ] 1.7 [tech-artist] Verify in Unity once the GPU is free: EditMode, both players' `-smoketest -windowed -smokeshots` PASS, before/after frame sequences in a hover and a punch-out, the feed's GPU cost per PLT-07 (owed)
- [ ] 1.8 [qa-engineer] review against spec scenarios
