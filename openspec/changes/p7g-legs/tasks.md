# Tasks: p7g-legs

Order: 1.1 → 1.2 → 1.3 → 1.4, then 1.5 (the app) once 1.3's `PoseFrame` state exists, then 2.1. The change builds on `unity/main` and merges `p7a-ground` and `p7f-damage` as they land. Model, format and event numbers are assigned by the orchestrator (D-072, D-074).

## 1. The legs

- [ ] 1.1 [physics-engineer] The class's legs as data (design 1, 3; D-024, D-079):
  - **Research:** the class's plastic leg tubes and printed legs, the clamp blocks with their wing-nut bolts, and the T-joints, with the pilot's "plastic, 25–35 cm, a little soft" as the brief and letter T for shape. For each: material, section, modulus, yield and ultimate stress, elongation, density, and the published spreads.
  - **The clamp:** the wing nut's hand-tight torque, the nut factor and the friction values.
  - **The clamped tube's edge:** the stress concentration factor.
  - **Write:** the labelled values in `drone.json`'s `legs` block and the parts' materials. The loader refuses an unlabelled value.
  - **Re-derive:** the leg's stiffness from its section in series with the clamp's and T-joint's give, replacing the assumed 10 kN/m. Re-assert the mass build.
  - **Print:** the landing speeds and roll angles at which each mode would start, from the closed forms.

  Verify: "Every leg value is labelled and loaded" and "The legs' stiffness comes from their section"; the goldens audited case by case (the new stiffness moves the contact goldens).
- [ ] 1.2 [physics-engineer] The failure law (design 2, 3, 6), after 1.1:
  - the loads at each leg's clamp from the contact model
  - yield with its set angle, clamp rotation and slip, crack, fracture, and the T-joint's slip and fracture
  - the `leg.strength.<leg>` streams and their logged draws
  - each leg's state in the step record, and the events

  Verify: "A normal landing leaves every leg intact", "A harsh landing bends a leg", "Not parallel enough to the ground", "A hard strike breaks a leg", "A leg comes off", "The strength draws cover their bounds", each with its known-wrong.
- [ ] 1.3 [physics-engineer] A damaged leg changes the drone (design 4, 5, 8), after 1.2:
  - the bent leg's shapes rotated about the clamp, the cracked leg's stiffness, the broken leg's stub
  - the detached and parted pieces as loose bodies in the 50 Hz side record
  - the exact rebuild of the build and the airframe's modes
  - `PoseFrame`'s leg states and set angles

  Verify: "The build follows a lost leg", "The next landing feels a damaged leg".
- [ ] 1.4 [physics-engineer] The harness, the log and the budget (design 7), after 1.3:
  - `flightbench --legs`: a grid of touchdown speed × roll × drift over 40 seeds, giving each outcome's share (intact, set, cracked, broken, detached, tipped), the sensitivity to the clamp's preload and the plastic's yield, and the known-wrong models
  - the header's and record's new fields, and replay through every failure
  - `--bench`'s contact rows with the law on, on AC

  Verify: "Replay through a broken and a detached leg", "The cost of the legs"; the grid's shares written into the as-built note for QA to judge against D-079.
- [ ] 1.5 [game-developer] The pilot sees it, after 1.3:
  - `FlightScene` draws each leg from `PoseFrame`: rotated by its set angle, a stub when broken, gone when detached, and a dropped piece where the side record puts it
  - the pause menu's summary lists each damaged leg, its state, and the landing or object and speed that caused it
  - R's restart starts intact

  Verify: "The summary names the damage"; the smoke test draws a bent and a missing leg (looked at, the shots in the handoff).

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios: every `leg-damage` scenario run on QA's own seeds; each known-wrong failing; the goldens' audits; replay and determinism through failures; 0 B and the budget on AC; `flightbench --legs`' shares judged against D-079's account; the summary and the drawn states looked at.
