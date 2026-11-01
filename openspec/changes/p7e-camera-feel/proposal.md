# Proposal: p7e-camera-feel

**Owner:** tech-artist · **Reviewers:** physics-engineer (the pose history and the camera's elastic turn in the sim core), game-developer (FlightScene's field latch beside the OSD work on the player's branch), qa-engineer
**Requirements:** PHY-25 (physics → feed: the camera's angular rate), FEED-02 (the FPV camera), FEED-04's C4 (motion blur), FEED-10 (the feed's cost) · **Decisions:** D-059 (the camera shows the body's every movement), D-055 (the chase view stays), D-026 (the ported output unchanged while the additions are off), D-008 (simulated, not filtered) · **VISION:** §2's "undefined and, sometimes, unexpected dynamics"; the feed as the pilot's goggles
**Tier:** Standard (D-033).

## Why
The pilot, after flying the map build (D-059): "there's still this smoothness to how the camera view looks. the drone's camera must be sensitive to micro movements of the whole drone". The rendered camera takes the rigid body's pose at the render clock (up to the monitor's rate), never the camera on the flexing frame, and reads the whole picture at one instant. A real analog camera (the build's Caddx Ratel 2, a CMOS sensor) delivers 50 fields a second, each read line by line over 18 ms and exposed over its shutter time, on a frame that bends.

## What Changes
- **Find the smoothness** (1.1): measure the sim's camera motion against what the render shows, in a hover and a punch-out, still and severe air; the numbers go in the design.
- **The camera at each field's own time** (1.2, 1.4): the sim keeps its last 128 poses (a wait-free history beside the pose pair) and each pose carries the frame's elastic turn at the camera (`AirframeModes`), so the renderer draws each PAL field once, at its own sim time, with the camera at the body's attitude times the frame's turn, held until the next field. The feed's fields follow the flight's.
- **Rolling shutter and motion blur** (1.3, 1.5): each field line is warped by the camera's rotation between the field's reference instant and that line's read, and blurred along its rotation over the exposure (the Ratel 2's published 1/50–1/100 000 s), from the poses alone. Off-switchable; off reads the scene exactly as before.
- **The camera's mount, derived** (design 3): the micro camera is clamped between the plates; its mount's resonance lies far above the step's 500 Hz, so the camera rides the frame and the frame's modes are the soft part.
- **The physics' share, named** (design 6): like for like, the sim's camera moves 2–5× less than clip P's over 4–15 Hz and 3–6× less over 1–4 Hz; the turbulence's per-rotor gradient (`p7c-wind`) carries that motion. The props' balance grade and damage and the blade pass are vibration, not picture motion (`hover-micro-motion.md`).

## Capabilities

### New Capabilities
- `fpv-camera`: the FPV camera's pose at each field, its rolling-shutter read and exposure blur, all from the sim's poses

### Modified Capabilities
- None. (`p4-feed`'s `analog-feed` is still in flight; this adds to its pipeline without changing its scenarios.)

## Non-goals
- **New shake in the physics:** no turbulence, prop damage or blade-pass forcing here (`p7c-wind`, `p7f-damage`); nothing in the renderer adds motion of its own (D-008).
- **The AE loop** (`p4-feed` task 1.7): the exposure takes the scene's light when measured, a labelled daylight stand-in until then.
- **The chase view** (D-055): it renders as before, the feed riding on top, with no shutter warp.
- **The OSD** (`p4-feed` 1.5, on this branch): untouched; the OSD is inserted after the camera and is never warped or blurred.

## Impact
- **Sim core:** `PoseFrame.CameraTurn`, `PoseBuffer`'s step history, `AirframeModes.FrameTurn`, `FieldCamera` (render-side, as `PosePair.Interpolate`). Output only: the flight's state, the log and the goldens do not change.
- **Feed:** `Feed/Core/RollingShutter.cs` (engine-free), `FpvCamera` (the driven field, the shutter texture), `FeedFeature` and `Feed.shader` (the per-line warp), `FeedSettings` (the switch and the render margin).
- **App:** `FlightScene` (the field latch in the FPV view).
- **Cost:** one extra rotation per scene read, and up to 8 blur taps on lines whose exposure sweeps more than a render pixel (none in daylight).
