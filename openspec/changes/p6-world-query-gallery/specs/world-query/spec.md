## ADDED Requirements

### Requirement: The world queries the physics reads
The sim package SHALL provide, engine-free, allocation-free, reentrant and callable from the physics thread:
- `SampleGround(points)`: height, normal, surface id and flags
- `StaticContacts(shapes)`: spheres and capsules swept from their previous pose, with a margin, against every placed collision primitive, wire and runtime object, each contact carrying point, normal, signed distance, material, object, shape, wire parameter and `Time`, at most one per (object, shape) under the Time < 1 latch rule (K§7.5)
- `Raycast(rays)`: distance, point, normal, material, object and surface
- `Geometry(object, shape)`
- runtime objects added before a flight (the launch stand), with a reset
- a 64-bit content hash of the loaded world and a `QueryVersion`

#### Scenario: A swept arm meets the pole
- **WHEN** a 0.02 m capsule is swept 0.5 m in one step through `L01_straight`'s collision at its test range pose
- **THEN** `StaticContacts` returns one contact with `Time` in (0, 1), the material `timber`, the object `L01_straight` and a signed distance within 1 mm of the analytic one

#### Scenario: A small fast sphere never crosses a wire unseen
- **WHEN** a 15 mm sphere is swept 60 mm in one step across a 5 mm wire, or across an `L03_medium` conductor of the test range, at any point of the span and from any side
- **THEN** `StaticContacts` returns that wire once with `Time` in (0, 1), its `Distance` within 1 µm of 0 and its `WireParam` the crossing's length along the chain over the chain's length; at rest on the conductor the `Distance` is the true signed distance, deep inside too

#### Scenario: A rotor ray re-cast from inside a conductor goes on
- **WHEN** a ray down onto an `L03_medium` conductor is re-cast from 1 mm inside it
- **THEN** the re-cast does not hit that wire and returns the ground below it, and a ray from inside a segment along the conductor hits nothing of it

#### Scenario: The launch stand is added and reset
- **WHEN** the launch stand is added at the test range's stand start point, and the world is then reset
- **THEN** a foot resting on each post meets that post once, with the stand's id after the placed objects; after the reset the stand is gone, and added again it gets the same id

#### Scenario: Batched equals single
- **WHEN** the golden set of 10 000 ground samples, 10 000 swept capsules and 10 000 rays over the test range's busiest spots is run batched and one by one, in Debug and in Release
- **THEN** every result is byte-identical across the four runs and matches the committed golden file

#### Scenario: Threads share one world
- **WHEN** four threads run the same queries on one loaded test range at once
- **THEN** each gets exactly the bits of a single thread's run

### Requirement: The test range is a world from one definition
The test range's placement file (`maps/test-range/placements.json`, D-047) SHALL be read both by Unity's scene builder and by the sim's loader. The loader SHALL place every asset's collision primitives, materials and wires from its `.assetmeta.json` at its row's pose, and the content hash SHALL change when a row, a pose or an asset's metadata changes.

#### Scenario: The sim sees what the scene shows
- **WHEN** the test range is loaded by the sim and built by the scene builder from the same placement file
- **THEN** every placed prefab's collider in the scene lies within 1 mm and 0.01° of its shape in the sim, with the same size, every shape and wire point in the sim lies within 1 mm of its row's pose applied to its metadata, and the hash differs when one row's pose or one shape's centre moves by 1 mm

### Requirement: The ground, its patches and its holes
`SampleGround` SHALL return the plane with its surface id outside the ground patches and hole bands. Over a ground patch's footprint (S04, E01) and inside an asset's `holes` band it SHALL report a hole and no plane ground, so that the asset's own shapes (the patch's soil and straw, a hole's fillers and walls) are the only ground there, through `StaticContacts` (WLD-16, review-physics W4). In `StaticContacts` the plane's contact SHALL be kept only where its point lies outside every patch and band (W3).

#### Scenario: A foot in the trench
- **WHEN** a foot point is sampled over the middle of E01's run, and a foot sphere is then dropped into the run in one step
- **THEN** `SampleGround` reports a hole there, and the sphere's first contact is E01's floor filler at the run's 1.5 m depth with its surface, with no plane contact

#### Scenario: A patch is the ground once
- **WHEN** a foot rests on one of S04's straw strips with a margin that reaches the soil below it
- **THEN** `SampleGround` reports a hole there, and `StaticContacts` returns the straw strip and the soil box, each once, and no plane

#### Scenario: A capsule straddling a band's edge
- **WHEN** a capsule lies across the edge of Z04's shaft band, one core end over the shaft's wall and the other over the plane
- **THEN** the plane answers at the lower core end's foot when that end lies outside the band, is absent when the lower end lies inside it, and the wall answers for the end over it either way

### Requirement: Budgets
The queries SHALL meet K§10.4's budgets on the dev machine on AC power, with the power mode printed: a worst-case physics step's batch (physics' pattern of review-physics W7 and K§10.5's: the ground samples, the swept capsules and the rays a step makes, at physics' margin) under 60 µs at the median, and 100 000 capsules and 100 000 rays of physics' pattern under 100 ms each and 100 000 ground samples under 10 ms.

#### Scenario: The worst-case step over the busiest spots
- **WHEN** `tools/worldbench --bench` runs a step's batch with the drone inside V01_L's crown, round H01_D's walls under its eaves, along and over E01's run, and along the posadka at 2 m
- **THEN** the median is under 60 µs at every spot, and the p99 and the largest step are printed beside it
