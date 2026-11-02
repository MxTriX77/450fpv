# Spec Delta

## ADDED Requirements

### Requirement: Uneven ground on the test range
The test range's map SHALL hold soil zones (a dirt track with ruts, a tilled field with furrows and pitfalls, a shelled patch with small craters and pitfalls), each with its surface, its micro-relief, its directional ridges and its placed analytic features (centre, radius, depth, profile, id), evaluated by one ground function from IEEE basic operations only. `SampleGround` SHALL return the zones' ground with `FeatureId` and `FeatureDepth` (0 outside a feature), and the zones' floors SHALL be drawn from the same function's triangulation, so the sampled ground is the drawn triangle. The relief SHALL meet the plane continuously at a zone's edge, each point SHALL have one surface, and the content hash and `QueryVersion` SHALL move with the change.

#### Scenario: The drawn floor is the sampled ground
- **WHEN** 10 000 points of each zone are sampled, cell diagonals and seams included, and compared with the drawn floor's triangles in the committed scene
- **THEN** every height agrees within 1 mm and every normal is the drawn triangle's within 1e-6 rad

#### Scenario: Pitfalls and furrows are in the samples
- **WHEN** a pitfall's centre, its rim and a point beside it are sampled, and the tilled field is sampled along and across its furrows at 1 cm steps
- **THEN** the centre reports the pitfall's id and its full depth, the rim a depth between it and zero, the point beside it id 0; across the furrows the height repeats at their spacing within 1 mm and along them it varies only by the micro-relief

#### Scenario: Continuity at a zone's edge
- **WHEN** every zone's edge is walked in 1 mm steps
- **THEN** no two adjacent heights differ by more than 1 mm, and the surface changes at the edge alone

### Requirement: Micro-detail elements near a point
`MicroDetailNear(centre, radius, kinds, results)` SHALL return every element of every placed instance whose segment, with its radius, meets the query sphere (S04's straws, stems and twigs; the crowns' twigs and leaves), from a 3-D cell index, in canonical order (instance, then element), with a stable 64-bit id (the instance and the element's index) and the true count on overflow, no element rooted in a hole, allocation-free, reentrant and identical across machines. `Feature(object, shape)` SHALL name the ground feature an asset's shape belongs to.

#### Scenario: Elements near a point
- **WHEN** spheres of 2 m are queried in S04, in V01_L's crown and over the plane, with every kind and with one kind masked out, into a buffer large enough and one too small
- **THEN** each result set is the brute-force set of the instances' records in canonical order, a straw rooted outside the sphere but crossing it is returned, the masked kind is absent, the small buffer holds the canonical prefix and the true count is returned, the same query gives the same ids every time, and a query takes less than 0.25 ms on AC with the power mode printed

#### Scenario: A pit names its feature
- **WHEN** `Feature` is asked for each shape of one of S04's pits and for a shape of the mat outside it
- **THEN** each pit shape returns the pit's id, centre, radius and depth from its `features` record, and the mat shape returns none

### Requirement: A runtime object moves between steps
A runtime object (the launch stand) SHALL be movable to a new pose between steps by `MoveObject`, on the physics thread, deterministically, outside the content hash; every later `StaticContacts`, `Raycast` and `Geometry` call SHALL see it at that pose, and placed objects SHALL refuse to move.

#### Scenario: A moved stand is where physics put it
- **WHEN** the stand is added at the field start, moved 0.1 m and tilted 5°, and queried by a capsule over a post, a ray down onto it and `Geometry`
- **THEN** every result is the moved stand's within 1e-12 m, its id is unchanged, the content hash is unchanged, moving a placed object throws, and four threads querying between moves get one thread's bits

### Requirement: A broad phase inside each object
An object with many shapes (S04's patch, a crown, the stand) SHALL be searched through an index of its own shapes, so physics' query pattern on the straw and at the field stand stays toward PLT-04's 60 µs line, with results bit for bit those of the exhaustive search or a `QueryVersion` bump with every changed result audited.

#### Scenario: The straw rest's queries
- **WHEN** `worldbench --bench` times physics' pattern resting in S04's straw and at the field stand's launch, on AC with the power mode printed
- **THEN** the queries' p50, p99 and max a step are printed against 1.6b's 194.5 and 92.9 µs at p99 and against the 60 µs line, 0 B is allocated, and the golden passes in Debug and Release
