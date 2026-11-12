# Proposal: w14-camera-place

**Owner:** asset-artist · **Reviewers:** qa-engineer, physics-engineer (what `drone.json`'s change moves), tech-artist (the props' captures) · **Requirements:** DRN-04, VIS-07, DRN-03, REAL-01, REAL-03, FEED-02 · **VISION:** §2 · **Decisions:** D-028, D-043 · **Reference:** `docs/reference-notes/vegetation-on-the-props.md` §3 · **Tier:** Standard

## Why
`w13-props-in-frame` made the drawn props turn with the sim's rotors. With that, the first-person picture shows two or three dark bars from each side edge in every field of a hover (its design.md §1 and §6). The footage of the pilot's quad class (the reference note §3.1's group of C, D, E, F, H, L and P) shows something else:
- **a blade almost never:** none in C to H at this scale, two frames in D, and in P a thin line high in the top-right corner;
- **two pale rods at mid-height,** one at each side, fixed to the camera in every clip of the group: airframe parts in front of the lens (the note's §3.3). The sim's picture has none.

The cause is the camera's place, not the drawing:
- `drone.json`'s `camera.lens_m`, (0, 0.015, 0.115) m, is labelled assumed. Letter T hides the camera, so the drone sheet put it "at the front of the body between the plates".
- The front hubs are pinned by the pilot's tip-to-tip numbers (DRN-02) and don't move.
- From that lens the front discs lie 20 mm above it and reach to within 33 mm of its axis. That gives the look of the B and G group, not of this airframe.

DRN-04 and VIS-07 ask that the blades and the airframe parts in view match the reference set-up. They can't until the lens sits where the footage and the published frame put it.

## What Changes
- **The camera's place is derived, no longer assumed.** A reading fits the lens's place to what the footage of the airframe's own group shows: where the rods sit in the picture, which parts of letter T's airframe they are, and how rarely a blade enters. The fit goes through the feed's pinned projection (FEED-02) and is checked against the published frame's camera mount.
- **The drone model's camera mount and `drone.json`'s `camera.lens_m` move to it,** rebuilt from the generator's params. Whatever moves in the mass build or the camera point's signals is audited, golden by golden.
- **The airframe parts the footage shows at mid-height are in the picture,** as geometry (VIS-07).
- **The props' captures are repeated** and compared side by side with the group's letters.

## Non-goals
- No change to the lens's intrinsics, to the uptilt (0°, the pilot's) or to the feed's look (FEED-02, D-026).
- No change to the hubs, the props or the frame's wheelbase (DRN-02).
- No second airframe: one drone.

## Impact
- `docs/reference-notes/camera-place.md` (new).
- `assets-src/drone/` (D01's generator params and `drone.json`), the rebuilt D01 model and its review sheet.
- The flight goldens if the mass build moves, with the next free model number at the merge.
- The branch is `unity/physics/physics-integration-2--camera-place`, cut from `--props-in-frame` at a31a6753: it needs the turning props to judge the result. The two merge into `--w3-preview` together, for the pilot's next build.
