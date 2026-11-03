# Tasks: p7g-legs

Order: 1.1 → 1.2 → 1.3 → 1.4, then 1.5 (the app) once 1.3's `PoseFrame` state exists, then 2.1. The change builds on `unity/main` and merges `p7a-ground` and `p7f-damage` as they land. Model, format and event numbers are assigned by the orchestrator (D-072, D-074).

## 1. The legs

- [x] 1.1 [physics-engineer] The class's legs as data (design 1, 3; D-024, D-079):
  - **Research:** the class's plastic leg tubes and printed legs, the clamp blocks with their wing-nut bolts, and the T-joints, with the pilot's "plastic, 25–35 cm, a little soft" as the brief and letter T for shape. For each: material, section, modulus, yield and ultimate stress, elongation, density, and the published spreads.
  - **The clamp:** the wing nut's hand-tight torque, the nut factor and the friction values.
  - **The clamped tube's edge:** the stress concentration factor.
  - **Write:** the labelled values in `drone.json`'s `legs` block and the parts' materials. The loader refuses an unlabelled value.
  - **Re-derive:** the leg's stiffness from its section in series with the clamp's and T-joint's give, replacing the assumed 10 kN/m. Re-assert the mass build.
  - **Print:** the landing speeds and roll angles at which each mode would start, from the closed forms.

  Verify: "Every leg value is labelled and loaded" and "The legs' stiffness comes from their section"; the goldens audited case by case (the new stiffness moves the contact goldens).

  **As built (model 37, D-072; the log format unchanged).**
  - **The material.** Rigid PVC-U tube, 10 × 8 mm, for the legs and the skids (one stock), in printed PETG clamps and T-joints. The pilot's "plastic, a little soft" sets the family, T the shape: a smooth Ø10 mm tube, which rigid PVC is sold as (10 mm OD × 8 mm ID). The standing check rules out the softer candidates: the four legs sway-buckle (fixed at the clamps, the feet pinned) at π²EI/(4L²) a leg, 3.78 times its share of the take-off weight in PVC-U (E 3.0 GPa); PA6 conditioned (1.2 GPa, BASF Ultramid B3S), PA12 and PP (1.1–1.5 GPa) stand at 1.5 or less. Extruded PETG (2.0 GPa, 2.5 times) is the near alternative.
  - **Sources** (every value labelled and noted in `drone.json`'s `legs.tube`, `legs.clamp`, `legs.tjoint`): Angst+Pfister APSOplast PVC-U data sheets 00.001-01, 00.001-10 and 00.005-01 (E 3000–3300 MPa, σ_y 55–58, bending strength 80, elongation at break 15–20 %, 1.44–1.45 g/cm³, notched Charpy 2–4 kJ/m²); Reichelt Chemietechnik's PVC-U pipe (σ_y 50, E 3000, 1.42); Prusament PETG data sheet v1.1 (printed: E 1.5 ± 0.1 GPa, interlayer 18 ± 4 MPa); Imrhan and Munoz-Vasa 2013 (a 19 mm flange pinched and tightened: 0.768 ± 0.114 N·m); DIN 315 (a 20 mm wing nut is M4); Shigley's Table 8-15 (K 0.20 zinc-plated, 0.16–0.30); the polymer pairs' friction (Engineering ToolBox, the repository's plastic row: 0.3, 0.2–0.4, assumed). Assumed and named: the clamp's edge factor K_t 1.5 (1.2–2.0; press-fit edges are charted by Strozzi et al. 2016 and Croccolo et al. 2012, no value for this joint was found), the cracked tube's stiffness factor 0.5 (design 3's), the T-joint's M3 pinch screw at 0.5 N·m (0.3–0.7).
  - **The loader** (`DroneSpec.ReadLegs`): a legs block with `tube`, `clamp` and `tjoint` needs all three and a note on every value; a removed label or note is refused naming the field (36 values, 72 refusals, `LegBuildTests`). Every bound holds its value. Files without the build (the fixtures) load as before.
  - **The stiffness from the section** (`DroneLegs.VerticalStiffness`, `SideStiffness`): along the leg 52.3 kN/m (derived), in series the tube's EA/L 397 kN/m, the arm bending under the clamp 67.6 kN/m (the arms' own derivation, arm_rr_a's) and the skid's ring in the T-joint 553 kN/m; a push never loads the clamp's grip, since the tube's top bears on the arm. It replaces the assumed 10 kN/m; the damper is re-derived at the same ζ 0.2, 103 N·s/m (was 45). Across the leg at the foot 262 N/m (the tube's 3EI/L³ 268 in series with the jaws' turn), printed; the leg shapes keep model 26's 6600 N/m, relabelled assumed: as a penetration the leg's own 262 N/m lets a wire past the tube's core under a few newtons (1.3 bends the shapes instead). The skid's overhang bends at 2.4 kN/m under its end, which one spring a corner does not carry.
  - **The mass build** (re-pinned, `MassBuildTests`): legs 10.2 g and skids 18.3 g (11 and 20 g of CFRP), 6.6 g lighter in all; take-off (L5) 5.080 kg, ready to fly without a bottle 4.080 kg with the coil full and 3.735 kg half paid out (D-069: 4.09 and 3.74); legs off removes 0.1094 kg. The roll tip 23.33° (23.35), the pitch tip 42.72° (42.75).
  - **1.6's bounce** (down at 0.50 m/s on a rigid floor): held, it leaves at 0.285 m/s and rises 215.8 mm (model 26: 0.281 m/s, 209.5 mm); cut, it hops 6.3 mm (2.8) and rests 0.207 s after the touch (0.239).
  - **The onsets** (`LegBuildTests.TheFailureOnsetsArePrinted`; the throttle cut, the impact's peak by energy, F = W + √(W² + k·m·v²), undamped, so the speeds are lower bounds: ζ 0.2 lowers the dynamic peak to 0.82). Capacities: crack (the edge's first yield, M_y/K_t) 2.13 N·m, set (σ_y·I/c) 3.19 N·m, full hinge 4.47 N·m, fracture (design 3: K_t·M·c/I ≥ σ_u) 3.09 N·m, all at the clamp (14.9 N across the foot sets it), buckling 385 N along the leg; the clamp's slip 576 N of pull (a push bears on the arm), its twist 2.88 N·m (24.5 N sideways at a skid's end); the T-joint's socket 6.13 N·m. Touchdown speeds, m/s:

    | roll | the floor's μ 0.5/0.4 | frictionless |
    |---|---|---|
    | 0° | the feet stick, the legs are struts: buckling 1.44 | the same |
    | 10° | sticks (needs μ 0.18): buckling 1.19; crack 36.5, set 54.8 | slides: crack 0.30, set 0.57, hinge 0.87, fracture 0.54; buckling 3.20 |
    | 20° | sticks (needs μ 0.36): buckling 0.99; crack 15.1, set 22.6 | slides: crack, set and fracture under the weight dropped on it, hinge 0.38 |
    | 30° | slides (needs μ 0.57): crack 0.42, set 0.83, fracture 0.80, hinge 1.30 | slides: every bending mode under the weight dropped on it; buckling 8.67 |

    Past 23.1° of roll the centre of mass is outside the touching skid and the drone tips whatever the legs do. The pilot's normal landing (level, 0.5 m/s) is under every onset at the weakest bounds, by a factor of 2 in speed.
  - **The goldens**, all five moved and audited: on model 26's legs (`GoldenFlights.WithModel26Legs`) each is model 26's pin bit for bit, every record and event (the kept test `OnModel26sLegsEachGoldenIsModel26s`, and `flightbench --golden`), so the legs' data is the whole change. Powered and rain move from step 1 (the lighter body): 273 mm and 204 mm from model 26's ends at 10 s, within 0.33° and 0.21°. Contact: 6 impacts against 5, no crash, at rest 0.2528 m up against 0.2519 (the stiffer spring's smaller set), 74 mm aside, within 80 mm and 1.8° of model 26's path. Range: 5 impacts against 4, at rest 0.4 mm deeper in S04's straw, 1.1 mm aside. Tumble: the lighter L4 body, its checks as before. No stored input changed. The older kept audits fly model 26's legs (`WithModel22Drone` puts them back, model 22's drone had them).
  - **Re-traced tests** (each with its cause): the dead-legs device (`LandingScenarios.DeadLegsCaught`) keeps 10 kN/m, since at ζ √2 on 52.3 kN/m its damper passes the explicit step's stability on a skid end's small effective mass (a sweep in the device's note); the legs' elastic give-back tips a caught skid up to 14 % sooner than the plastic catch (model 26: 10 %), so `LandingTests`' and `flightbench --landing`'s band is 15 %; at S04's soil gap in the crosswind the held bounce now tips the drone over onto the mat (the spring does it, not the mass, nor the stick anchor's level); at 5.475 m in Severe leg_fl now passes L03's cable once after the prop's touch (the lighter legs' path; the 6.6 kN/m leg shape, which 1.2's fracture replaces), an expected count in that one case.
  - **For 1.2** (the handoff names them): design 3 has no buckling, the first mode of a level or a sticking landing; the stick anchor ties the foot's tangential spring to the normal's (0.5 × 52.3 kN/m), 100 times the leg's own 262 N/m across, so the contact's foot force carries moments a strut does not; design 3's stress fracture (3.09 N·m) comes before the set (3.19 N·m) for K_t above 1.45 on a ductile tube.
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
