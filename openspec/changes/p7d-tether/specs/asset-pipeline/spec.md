# Spec Delta

## MODIFIED Requirements

### Requirement: Ports and wires in the metadata
The metadata schema MAY carry `ports` (named attachment points: id, position, direction, and `max_diameter_m`, the largest conductor the fitting holds) that a generator writes for other generators and the map to use, and `wires` (per conductor or bundle: `slot`, an ordered point list, `diameter_m`, `mass_kg_per_m`, `sag_m`, `axial_stiffness_n` (EA), `breaking_force_n` and `bending_stiffness_n_m2` (EI: a solid wire's E·πD⁴/64, a stranded conductor's strands bending freely, a bundle's cores with its insulation's share), all three positive, `material`, `damage`) for conductors that are map objects rather than collision shapes. Both SHALL be written as empty arrays when absent, the validator SHALL check their fields, and the physics-metadata component SHALL expose them. A snag record MAY carry `edge_radius_m`, the arris the fiber bends over where it is smaller than the record's reach (`radius_m`): positive and at most `radius_m` (the strand of a stranded conductor, a barb's blade; task 1.5's reading).

#### Scenario: Ports written and read
- **WHEN** a pole variant with four hooks is built
- **THEN** its metadata lists four `ports` at the insulators' positions with outward directions, and the prefab's metadata component exposes them

#### Scenario: Wires validated
- **WHEN** a `wires` record names a material other than `cable`, or a point list shorter than two points
- **THEN** the validator rejects the metadata naming the record and the rule

#### Scenario: A conductor's bending stiffness
- **WHEN** `L03_thin`, `L03_medium` and `L03_thick` are rebuilt, and a record is validated without `bending_stiffness_n_m2` or with it at zero
- **THEN** their records carry 6.1 N·m² (5 mm solid steel) and 1.75 N·m² (A-50's 7 × 3.00 mm strands) within 1 % and the bundle's derived value, their looks and points are unchanged, a second build leaves `git status` clean, and the two bad records are rejected naming the field
