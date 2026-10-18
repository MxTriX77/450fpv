# Spec Delta

## ADDED Requirements

### Requirement: Ports and wires in the metadata
The metadata schema MAY carry `ports` (named attachment points: id, position, direction) that a generator writes for other generators and the map to use, and `wires` (per conductor: an ordered point list, `diameter_m`, `mass_kg_per_m`, `sag_m`, `material`, `damage`) for conductors that are map objects rather than collision shapes. Both SHALL be written as empty arrays when absent, the validator SHALL check their fields, and the physics-metadata component SHALL expose them.

#### Scenario: Ports written and read
- **WHEN** a pole variant with four hooks is built
- **THEN** its metadata lists four `ports` at the insulators' positions with outward directions, and the prefab's metadata component exposes them

#### Scenario: Wires validated
- **WHEN** a `wires` record names a material other than `cable`, or a point list shorter than two points
- **THEN** the validator rejects the metadata naming the record and the rule

### Requirement: Wires are audited against their polyline
For an asset with `wires`, the collision audit SHALL check that every LOD0 vertex of a wire's tube lies within 5 mm plus the wire's radius of its point list, SHALL exclude those vertices from the primitive audit, and SHALL print each wire with the largest deviation it found; the importer SHALL render the tube with the thin-wire material and SHALL create no collider for a wire.

#### Scenario: Tube follows the polyline
- **WHEN** a 40 m conductor of 9 mm is audited
- **THEN** the audit passes it with a largest deviation under 5 mm and prints it, and the primitive audit reports no vertex of the tube

#### Scenario: Tube off its polyline
- **WHEN** a tube vertex lies 20 mm from the point list
- **THEN** the audit fails naming the wire, the deviation and the tolerance
