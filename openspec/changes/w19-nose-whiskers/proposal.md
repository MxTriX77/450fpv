# Proposal: w19-nose-whiskers

**Owners:** reference-analyst (the geometry), asset-artist (the model), tech-artist (the picture) · **Reviewers:** qa-engineer (physics-engineer for anything the sim core gains) · **Requirements:** VIS-07, DRN-04, REAL-01, REAL-03, FEED-02, PHY-25 · **Decisions:** D-028, D-117 (D-103's camera move is cancelled) · **Tier:** Standard

## Why
The pilot, 2026-11-17 (their words are in Russian in D-117; rendered here):
- **The two pale bars at mid-height in their footage are two thin wire whiskers on the nose of their kit,** neither the props nor the camera's mount. "They do not point parallel, they converge slightly (!) toward the centre point of the camera's picture."
- A second photo (V) shows them from the side: "note how they are mounted relative to the camera … this matters for understanding how the whiskers look in the picture."
- **They do nothing in the flight:** "this does not affect our flight in any way, they are simply there, and from the wind, the speed and so on they can shake a little, and break too of course."
- **The camera stays where it is** ("camera position: let's cancel this one. It seems you got it wrong"). The props "are of course a little visible too, at the right and left of the picture's top."

Every one of the pilot's flight references has the two bars in the picture, and the sim's picture has none, so every side-by-side misses them (REAL-01). The turning props (`w13-props-in-frame`) were built and then held back for a camera move that is now cancelled.

## What Changes
- **Two whiskers on the drone's nose, as geometry** (VIS-07): thin wires with their real thickness, mounted as photos U and V show them against the camera. Through the feed's lens, with the camera where it is, they sit where the footage's bars sit and converge slightly toward the picture's centre.
- **They are drawn parts only.** No mass, no contact shape and no force in the sim: with and without them every flight's log is the same, and no golden moves.
- **They shake a little.** Each whisker is a thin cantilever. Its tip moves in its first bending mode, driven by the air the drone flies through and by the frame's vibration, both read from what the sim already publishes. The motion is computed from the wire's size and material, never animated by hand.
- **They can break.** A whisker that meets the world bends away from it. Past its material's limit it stays bent or parts, and a replay shows the same break in the same field.
- **The turning props go in with the camera where it is.** `w13-props-in-frame` is brought up to the line and checked against the pilot's account.
- **`w14-camera-place` is dropped.** Its branch stays unmerged.

## Non-goals
- No camera move and no change to the lens (FEED-02).
- Only the whiskers' shape and look are in scope. Their geometry comes from the pilot's two photos and from the footage's bars. Nothing else about them is researched or written down.
- No physics role: they are not in `drone.json`'s mass build, they carry no load and they do not touch the flight.

## Impact
- A reference note, `docs/reference-notes/nose-whiskers.md`.
- The asset: `tools/blender/generators/drone.py`, the drone's params (visual-only parts that the mass build and the contact shapes skip), the Unity prefab, the object sheet and the review sheet.
- The player: a view component beside the drawn props (`Scripts/App/PropView`), its tests, the feed's captures.
- The sim core: nothing a step reads. If the break needs the world's queries at a fixed rate, it is an observer the flight never reads back, with a physics engineer's reading.
- The branch is `unity/art/physics-integration-2--nose-whiskers`, cut from the physics line. It merges into the line, then the pilot's build branch, with no model number.
