## ADDED Requirements

### Requirement: The world queries the physics reads
The sim package SHALL provide, engine-free, allocation-free, reentrant and callable from the physics thread:
- `SampleGround(points)`: height, normal, surface id and flags
- `StaticContacts(shapes)`: spheres and capsules swept from their previous pose, with a margin, against every placed collision primitive, wire and runtime object, each contact carrying point, normal, signed distance, material, object, shape and `Time`, at most one per (object, shape) under the Time < 1 latch rule (K§7.5)
- `Raycast(rays)`: distance, point, normal, material, object and surface
- `Geometry(object, shape)`
- runtime objects added before a flight (the launch stand), with a reset
- a 64-bit content hash of the loaded world and a `QueryVersion`

#### Scenario: A swept arm meets the pole
- **WHEN** a 0.02 m capsule is swept 0.5 m in one step through `L01_straight`'s collision at the gallery pose
- **THEN** `StaticContacts` returns one contact with `Time` in (0, 1), the material `timber`, the object `L01_straight` and a signed distance within 1 mm of the analytic one

#### Scenario: Batched equals single
- **WHEN** the golden set of 10 000 ground samples, 10 000 swept capsules and 10 000 rays is run batched and one by one, in Debug and in Release
- **THEN** every result is byte-identical across the four runs and matches the committed golden file

### Requirement: The gallery is a world from one definition
The G1 gallery's stations SHALL be described by one data file read both by Unity's scene builder and by the sim's loader. The loader SHALL place every asset's collision primitives, materials, wires and holes from its `.assetmeta.json` at its station pose, and the content hash SHALL change when a station, a pose or an asset's metadata changes.

#### Scenario: The sim sees what the scene shows
- **WHEN** the gallery is loaded by the sim and built by the scene builder from the same data file
- **THEN** every placed prefab's collision primitives in the sim lie within 1 mm of the prefab's colliders in the scene (the load-time pose parity), and the hash differs when one station's pose is moved by 1 mm

### Requirement: The ground, and holes at gallery scale
`SampleGround` SHALL return the floor plane with its surface id outside the stations, and S04's and E01's own ground over their patches. Inside an asset's `holes` band it SHALL report no plane ground, so that the asset's fillers are the ground (WLD-16), and the flight core's legs then meet those fillers through `StaticContacts`.

#### Scenario: A foot in the trench
- **WHEN** a foot point is sampled over the middle of E01's run
- **THEN** `SampleGround` reports a hole there, and a swept foot sphere moved down into the trench meets E01's floor filler in `StaticContacts` with its surface

### Requirement: Budgets
The queries SHALL meet K§10.4's budgets on the dev machine on AC power, with the power mode printed: a worst-case physics step's batch (K§10.5: the ground samples, the swept capsules and the rays a step makes) under 60 µs, 100 000 capsules under 100 ms and 100 000 rays under 100 ms.

#### Scenario: The worst-case step over the busiest station
- **WHEN** `tools/worldbench --bench` runs a step's batch with the drone inside V01's crown and beside the house
- **THEN** the median is under 60 µs, and the p99 is printed beside it
