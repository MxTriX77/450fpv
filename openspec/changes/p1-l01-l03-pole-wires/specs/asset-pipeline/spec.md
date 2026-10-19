# Spec Delta

## ADDED Requirements

### Requirement: Ports and wires in the metadata
The metadata schema MAY carry `ports` (named attachment points: id, position, direction, and `max_diameter_m`, the largest conductor the fitting holds) that a generator writes for other generators and the map to use, and `wires` (per conductor or bundle: `slot`, an ordered point list, `diameter_m`, `mass_kg_per_m`, `sag_m`, `axial_stiffness_n` (EA) and `breaking_force_n`, both positive, `material`, `damage`) for conductors that are map objects rather than collision shapes. Both SHALL be written as empty arrays when absent, the validator SHALL check their fields, and the physics-metadata component SHALL expose them. A snag record MAY carry `edge_radius_m`, the arris the fiber bends over where it is smaller than the record's reach (`radius_m`): positive and at most `radius_m` (the strand of a stranded conductor, a barb's blade; task 1.5's reading).

#### Scenario: Ports written and read
- **WHEN** a pole variant with four hooks is built
- **THEN** its metadata lists four `ports` at the insulators' positions with outward directions, and the prefab's metadata component exposes them

#### Scenario: Wires validated
- **WHEN** a `wires` record names a material other than `cable`, or a point list shorter than two points
- **THEN** the validator rejects the metadata naming the record and the rule

### Requirement: A planted asset's buried surface is not audited
A generator MAY declare its asset planted (`PLANTED`): its members stand in the ground, which covers everything below y = 0. For such an asset the audit SHALL treat every point below the ground plane as covered: a primitive's surface below y = 0 is not measured against the mesh, and no mesh is drawn there, since the drone can never touch it; everything at or above the ground is audited as before.

#### Scenario: A buried capsule end on a planted pole
- **WHEN** a planted log's lowest capsule ends 0.113 m below the ground, 0.163 m from the open log mesh
- **THEN** the audit passes it (the buried end is not measured), and the same capsule on an asset not declared planted fails, naming the end and its distance

### Requirement: Wires are audited against their polyline
For an asset with `wires`, the collision audit SHALL check that every LOD0 vertex of a wire's tube lies within min(5 mm, half the diameter) plus the wire's radius of its point list (so a drawn thin wire always overlaps its physical one), SHALL exclude those vertices from the primitive audit, and SHALL print each wire with the largest deviation it found; the importer SHALL render the tube with the thin-wire material and SHALL create no collider for a wire.

#### Scenario: Tube follows the polyline
- **WHEN** a 40 m conductor of 9 mm is audited
- **THEN** the audit passes it with a largest deviation under 5 mm and prints it, and the primitive audit reports no vertex of the tube

#### Scenario: Tube off its polyline
- **WHEN** a tube vertex lies 20 mm from the point list
- **THEN** the audit fails naming the wire, the deviation and the tolerance
