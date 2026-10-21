# Proposal: p5-drone

**Owner:** asset-artist · **Reviewers:** reference-analyst (the object sheet, its own task), physics-engineer (`drone.json`'s mass, inertia and packs, its own task), tech-artist (the in-view parts and prop blur, its own task), qa-engineer
**Requirements:** DRN-01…07, AST-02, AST-03, AST-05, AST-12, REAL-05, VIS-07 · **Decisions:** D-004, D-022, D-024, D-042 · **VISION:** §1 (one drone: the Svinoriz 10 Opto), §2 (the heavy fiber quad the pilot flies: coil below, payload on top, two packs)
**Source:** `docs/knowledge.md` §5 (published specs, the derived mass build, thrust, the gap and stand geometry); `docs/reference-notes/fc-profiles.md` (the class's flight controller and 8S packs); the pilot's answers in `docs/pilot-questions.md` (batteries, coil, uptilt, legs).

## Why
G5 is after G4 (D-042). The flight core starts from a bootstrap `drone.json` built on K§5.2's assumed layout, which predates the pilot's word that the payload sits on top and the drone carries two packs. This change researches the class, writes the real `drone.json` that the model and the physics both read (DRN-03), and builds the Svinoriz 10 Opto in both leg variants with its launch stand, for the pilot to approve.

## What Changes
- **An object sheet** (`docs/object-sheets/drone.md`): typical heavy 10-inch fiber-optic quads from public sources.
  - **Parts:** frame, 3115-class motors, 3-blade 10" props, the coil underneath, two packs and their arrangement, the generic payload on top, the FPV camera at 0° uptilt, transceiver, flight-controller stack, wiring, plastic legs and the four-post stand.
  - **For each part:** dimensions, masses, materials and colours.
- **`drone.json`** (DRN-03), written from the sheet. It replaces `p6-flight-core`'s bootstrap file.
  - K§5.2 re-derived for the payload on top and two packs, with the inertia checked.
  - The pack voltage and arrangement set: 6S or 8S Li-ion, two packs.
- **The model:** a parametric generator reading `drone.json` for every dimension, in the legs-on and legs-off variants.
  - Prop tip to tip exactly 0.574 m nose-first and 0.706 m diagonal (DRN-02).
  - LODs and the review sheet.
- **The launch stand** (DRN-07), sized from `drone.json`, with its collision, `steel`, and its mass.
- **In-view parts and animation hooks** (DRN-04, DRN-05):
  - the props and airframe the FPV camera sees, matching the set-up (VIS-07)
  - prop spin and blur with rpm, leg flex from physics, coil payout
- **A gallery station** (DRN-06): both variants, a turntable and the stand.

## Capabilities

### New Capabilities
- `drone`: the Svinoriz 10 Opto as one component spec read by the model and the physics, its geometry, both variants, the launch stand, the in-view parts and the animation hooks

### Modified Capabilities
- None. `p6-flight-core`'s `drone.json` schema is used as it defines it. A field the research needs is added by the physics engineer in that schema, with a note.

## Non-goals
- **Any functional detail on the payload:** it is a generic box (DRN-01).
- **The trailing fiber line** (7d, after G7 by D-042).
- **The flight model:** this change writes the numbers the core reads, not the core.

## Impact
- **New:**
  - `docs/object-sheets/drone.md`
  - `assets-src/drone/drone.json` (replacing the bootstrap) and the drone and stand params
  - a `drone` generator under `tools/blender/generators/`
  - the prefabs and the review sheets
  - the gallery's drone station
- **Consumers:**
  - `p6-flight-core`: the mass build re-pinned
  - `p6-input-app`: the flight session's drone in place of the stand-in
  - `p4-feed`: the in-view parts
