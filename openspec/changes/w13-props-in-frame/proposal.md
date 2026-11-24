# Proposal: w13-props-in-frame

**Owner:** tech-artist · **Reviewers:** qa-engineer (physics-engineer for the one field added to the pose) · **Requirements:** REAL-01, REAL-03, PHY-25, FEED-00 · **VISION:** §2 · **Decisions:** D-028, D-026 · **Reference:** `docs/reference-notes/vegetation-on-the-props.md` §3 · **Tier:** Standard

## Why
The first-person view draws the drone's props as static meshes. QA's look at the pilot's build of 2026-11-12 found a sharp grey blade parked in the upper left of the picture, unchanged at 78 % throttle. The flight scene has never turned or smeared the props.

The pilot's footage shows something else (the reference note, §3, by letter):
- a blade in the frame is **never static**: at about 290 blade passes a second against 50 fields, each field catches it at an unrelated angle, so its angle and length jump from frame to frame, and a field that misses it shows nothing in that corner;
- it is **mostly a thin sharp sliver**, sometimes a wider translucent wedge;
- **where and how often depends on the airframe**: top corners in letters A and P, side edges in B and G, and almost never in the group the sim's drone belongs to (C to H).

So the sim's blade is wrong in its stillness and in being there in every frame. This also comes first for the Village physics work (`w9-village-physics` 2.5): cut straw will be drawn leaving these discs, and it can't sit next to props that visibly don't turn.

## What Changes
- **The pose carries each rotor's angle.** The sim already integrates it (`FlightState.RotorAngle`); the pose the scene reads gains it, render-side only. The physics, the log and every golden are untouched.
- **The view turns each prop to that angle at each video field's exposure,** in the first-person and the chase view.
- **The blade's smear over the exposure** comes from the rotor's speed and the camera's exposure time as the feed already models it (`RollingShutter`), so a blade reads as a thin sliver in bright light and a wider wedge in dull light. Nothing is a tuned look.
- **A rotor that isn't turning shows its blade still,** where the sim holds it.

## Non-goals
- No change to the feed's look or its noise (D-026), to the camera's place or lens, or to the drone's model.
- No blur disc or prop texture invented beyond what the exposure gives.
- No drawing of prop damage (a broken blade drawn shorter, a lost prop gone): a later change.
- The cut bits themselves (`w9-village-physics` 2.5).

## Impact
- `unity/Packages/com.fpv450.sim/Runtime/Flight/PoseFrame.cs` and where the core fills it (one render-side field).
- `Scripts/App/FlightScene.cs` (the drone's prop transforms) and the field camera in `Scripts/Feed`.
- The branch is `unity/physics/physics-integration-2--props-in-frame`, cut from `--w3-preview` at 3f1fe31e. It merges back into `--w3-preview` for the pilot's next build.
