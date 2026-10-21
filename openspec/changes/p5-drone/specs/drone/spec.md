## ADDED Requirements

### Requirement: One component spec for the model and the physics
`drone.json` SHALL list every part's mass, size, position and material, written from the drone object sheet, and both the visual model's generator and the physics' mass and inertia build SHALL read it (DRN-03). The payload mass SHALL scale the payload part, the coil mass SHALL fall with payout with the CoM shift computed, and the pack arrangement and voltage (6S or 8S Li-ion, two packs) SHALL be set from the research.

#### Scenario: Re-derived for letter T's layout
- **WHEN** `drone.json`'s mass build runs at the sheet's nominal loadout and across the payload range 0–2.0 kg
- **THEN** take-off mass, the CoM and the roll, pitch and yaw inertia land inside the sheet's re-derivation of K§5.2's ranges (derived), each figure labelled, with the spool above the CoM on the legs-on build and below it on the legs-off build (D-043)

### Requirement: Exact geometry
The model SHALL be built by a parametric generator from `drone.json` and its params (D-004), with prop tip to tip 0.574 m nose-first and 0.706 m on the diagonal, each within ±5 mm (DRN-02), real thickness on every part with a physical role (REAL-05), and both the legs-on (plastic legs of 25–35 cm) and legs-off variants (DRN-01).

#### Scenario: The prop tips
- **WHEN** the built LOD0 is measured
- **THEN** tip to tip is 0.574 ± 0.005 m nose-first and 0.706 ± 0.005 m on the diagonal, and every dimension matches `drone.json` within 1 mm

### Requirement: The launch stand
The stand SHALL be an X-shaped steel base with four posts about 0.25 m high, one under each motor on the diagonals, the drone resting on its arms just inboard of the motors and the coil hanging clear, every dimension derived from `drone.json`, with its collision, `steel` and its mass (DRN-07, D-022).

#### Scenario: The drone rests on the stand
- **WHEN** the legs-off drone is placed on the stand
- **THEN** the arms touch all four posts within 1 mm, the coil clears the base by at least 20 mm, and the stand's metadata carries its mass and `steel`

### Requirement: In-view parts and animation hooks
The props and airframe the FPV camera sees SHALL match the reference set-up (VIS-07). The props SHALL spin and blur with rpm, the legs SHALL flex from physics, and the coil SHALL pay out (DRN-04, DRN-05).

#### Scenario: Props in view
- **WHEN** the FPV camera renders at hover rpm and at idle
- **THEN** the in-view props show the blur of their rpm, and the in-view parts' positions match `drone.json` within 1 mm
