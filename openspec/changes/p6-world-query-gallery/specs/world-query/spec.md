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

#### Scenario: A first touch is a touch
- **WHEN** a skid slides 0.1 mm over the open plane at 10, 20 and 30 m/s, a 5 mm sphere slides 20 mm in a step 0.2 mm past a trunk cylinder, a sphere sinks 50 µm while moving 20 mm across the plane, and 15 mm spheres graze a 5 mm wire and an `L03_medium` conductor at 0.990–0.998 of R + r
- **THEN** the slides are never a first touch (the current contact, `Time` 1), the sinking sphere's first touch on the plane is exact (`Time` and `Distance` within 1e-12), and every graze is one contact with `Time` in (0, 1), not after its analytic first touch, and its `Distance` within 1 µm of 0

#### Scenario: A pitching core's touch is latched
- **WHEN** cores of 0.1–0.3 m pitch about their middle at 290–2000 °/s (1 kHz) across a 10 mm twig cylinder, a 0.15 m trunk cylinder and an `L03_medium` conductor, their middle approaching at 10–100 µm a step and sliding up to twice that, judged against a brute-force truth (a Lipschitz branch and bound over the step)
- **THEN** every sweep that starts more than 5 µm clear and ends more than 1 µm inside is one contact with `Time` in (0, 1), not after the truth's first touch, its `Distance` within 1 µm of 0 and a touch by the truth; a sweep that never touches is never a first touch; one that dips in and comes back clear within the step may count none

#### Scenario: The separating plane never steps past a touch
- **WHEN** 7.5 mm spheres moved 60 mm in a step graze a sphere, a capsule, a cylinder's side and rim, and a turned box's edges and corners at 0.990–0.998 of their touch distance, or sink 15–75 µm into a cylinder's cap or a box's face across 60 mm
- **THEN** each is one contact with `Time` in (0, 1), not after its analytic first touch, and its `Distance` within 1 µm of 0

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
The queries SHALL be judged as D-054 sets it, on the dev machine on AC power with the power plan, mode and CPU clock printed (PLT-07): at G7 on physics' pattern (review-physics W7: the samples, swept capsules and rays `ContactModel` makes each step, at physics' margin), inside PLT-04's whole-step budget of `p6-flight-core` (p99 at most 300 µs, 0 B); 100 000 capsules and 100 000 rays of physics' pattern under 100 ms each and 100 000 ground samples under 10 ms. PLT-04's 60 µs line for the queries is the target of their speed work, printed beside each row. K§10.5's batch with the fiber's segments is shown, and judged when the fiber is built (phase 7d).

#### Scenario: The worst-case step over the busiest spots
- **WHEN** `tools/worldbench --bench` runs physics' pattern with the drone inside V01_L's crown, round H01_D's walls under its eaves, along and over E01's run, along the posadka at 2 m, resting in S04's straw and lifting off the field start's stand
- **THEN** every row allocates 0 B and its p99 is under 300 µs, with its median, p99, largest step and the 60 µs target printed beside it; and `p6-flight-core`'s `flightbench --bench` gives the queries' p99 a step on the straw rest and the stand's launch inside its whole-step budget
