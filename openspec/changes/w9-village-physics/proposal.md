# Proposal: w9-village-physics

**Owner:** physics-engineer · **With:** world-builder (the stem field), tech-artist (cut bits on camera) · **Reviewers:** qa-engineer · **Requirements:** PHY-01, PHY-03, PHY-04, PHY-05, PHY-18, PHY-24, PHY-25, PHY-26, WLD-11, AST-10, PLT-04 · **VISION:** §2 (physics first), §3 · **Decisions:** D-088, D-072, D-024 · **Plan:** `docs/handover-2026-11-12.md` §3, unit D · **Tier:** Complex

## Why
The pilot flew the farmland preview (model 85) and said the main focus is physics, on the map they now call Village (D-100):
- in real life, landing in high grass with straw, the straws are visibly cut in front of the camera and the quad shakes, and the shaking must be calculated;
- straw and even leaves affect the motors and props, and so the flight;
- on the test range the drone could tip or snag its legs on sloped ground, and legs sank and stuck a little into softer soil; every ground nuance built for the test range must work on the new map too.

The test range was built around these interactions (p7a-ground, p7g-legs, the S04 straw boxes, micro detail, flight 37). The farmland came in through W2 and W3 with its heights, its surface classes and its objects, but nobody has checked, interaction by interaction, that the farmland feeds each one the data it needs. Known gaps already:
- furrows aren't in the physics (W3 2.2);
- S04-type straw counts only as wind roughness on the farmland (W7 1.1);
- a cut landing across a side slope rolls from 10° (W3 2.2), which the pilot hasn't seen yet.

And one interaction the pilot asks for has never existed: grass and straw stems, and leaves in crowns, struck and cut by the spinning props.

## What Changes
- **An audit first** (`audit.md`, no code): every ground and vegetation interaction the test range and the model-79 flights have, and for each one whether the farmland feeds it its data (surface per cell from `classes/`, soil softness, grass and straw volumes, micro detail), with a test or a measurement for each answer.
- **The farmland's stem field:** a per-cell grass and straw field (height, stem density, stem kind) that the sim can query, derived from W2's export (`grass/map.raw`, `classes/`), never hand-painted.
- **Stems on the props:** blades striking and cutting grass, straw and crown leaves. Each cut is an impulse from published stem properties (diameter, density, cutting energy or shear strength), taken from the rotor's own torque and thrust. The drone's shaking is that impulse train through the airframe, never noise on the pose (PHY-24).
- **Cut bits on camera:** the sim's cut events are logged, and the feed draws the cut pieces in front of the camera from them.
- **Leaves in crowns** extend W3 2.7's twigs (`PropStrikes.Twigs`), whose constants move into `physics_materials.json`.
- **Every gap the audit finds** is fixed in the physics or the data, in the order the audit gives, or shown to be physical. The audit (`audit.md`) found two bugs on both maps: the legs' soil anchors can produce a contact moment with almost no force behind it, which tumbles some cut landings on 10–20° slopes, and a rotor inside a crown logs a zero-mass blade break every step. It also found that the pilot's "a leg sticks" is measured nowhere, so the liftoff harness is built.
- **Stems on the legs and the fiber** from the same stem field, as the test range's straw already acts on the fiber.
- **The test range's straw joins the same law,** since House flies in the player again (D-100).

## Non-goals
- No new surface classes or materials beyond what the audit shows missing.
- No change to the friend's scene or to the store (D-089, D-095).
- No wind work (W7) and no far ground (W8).
- No tuning to make the shake "feel right": numbers are published, measured or derived, each labelled.

## Impact
- The sim package: Contact (`ContactModel`, `PropStrikes`, `BladeStrikes`), World (`MapWorldMicro`, the map loader), Flight, Log. A new model number at merge (D-072), and possibly a log format and event kinds (`docs/handover-2026-11-12.md` §5 holds the next free numbers).
- The map package and `tools/map/` for the stem field (validated by `validate.py`).
- The feed (`Scripts/Feed`) for the cut bits.
- Worldbench and flightbench rows and goldens, every moved golden audited case by case.
- The branch is `unity/physics/physics-integration-2--w9-village`, cut from `--w3-ground` at 241145ea (the pilot's preview). It merges back into `--w3-ground`, and into the next pilot build.
