# Spec Delta

## ADDED Requirements

### Requirement: Wires near a point, with their ends and neighbours
`WiresNear(centre, radius, results)` SHALL return every placed conductor whose rest curve, inflated by its blow-out envelope at 45°, meets the query sphere, with its object and shape, its record's fields (diameter, mass per metre, sag, `axial_stiffness_n`, `breaking_force_n`, `bending_stiffness_n_m2`, material, `edge_radius_m`), its points, its two ends (the port or house anchor each hangs from, and that fitting's kind) and the span sharing each end's port on the same conductor line, in canonical order with the true count, allocation-free, reentrant and identical across machines. Ends SHALL be paired at load with a port within 5 mm; an end with none SHALL be reported at load as a world bug naming the wire.

#### Scenario: The range's spans and their ends
- **WHEN** spheres of 2 m and 30 m are queried at the range's L01 poles and between them, into a buffer large enough and one too small
- **THEN** each result is the brute-force set of conductors in canonical order, each `L03_medium` conductor names its two `L01` ports and its neighbouring span at each (none at a line's last pole), the small buffer holds the canonical prefix and the true count, and a 2 m query takes under 0.1 ms on AC with the power mode printed

### Requirement: Wires left to physics
`StaticContacts` and `Raycast` SHALL take a filter that leaves wire shapes out of their results; with it off every caller SHALL see wires as before, bit for bit.

#### Scenario: The filter
- **WHEN** a capsule and a ray are cast through an `L03_medium` conductor with the filter on and off
- **THEN** with it on neither returns the wire and the ray returns what lies beyond, with it off both return the wire as the golden records, and the golden passes in Debug and Release

### Requirement: Snag records near a point
`SnagsNear(centre, radius, results)` SHALL return every segment of every placed snag record that meets the query sphere within its `radius_m`, with its object, record, segment, kind, `radius_m`, `edge_radius_m`, `barb_pitch_m` and material, from a cell index, in canonical order with the true count, allocation-free and reentrant; the map compiler SHALL refuse more than 2048 record segments meeting any 2 m cube.

#### Scenario: Records near a point
- **WHEN** spheres of 1 m and 2 m are queried at an L01 pole's head, along an L03 span and in V01_L's crown, into a buffer large enough and one too small
- **THEN** each result is the brute-force set in canonical order with every field of its record, the small buffer holds the canonical prefix and the true count, a 1 m query takes under 0.1 ms on AC, and a synthetic map with 2049 segments in a 2 m cube is refused naming the cube
