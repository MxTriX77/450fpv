# Spec Delta

## Purpose

The fast, deterministic runtime questions physics asks about the world: ground height and softness under a leg, the stems and straws touching it, contacts with objects and wires, and what blocks the wind. They make every landing different, yet exactly replayable.

## ADDED Requirements

### Requirement: Batch ground sample
`SampleGround(points, results)` SHALL return, for each (x, z) point, in world frame and metres:
- `TerrainHeight`: the rendered triangle at that point, using the exact triangulation of the renderer and Jolt collision
- `GroundHeight`: terrain height plus micro-relief, ridges and pitfalls
- `Normal`: the unit normal of `GroundHeight` on the rendered facet
- `MatDepth`: per cover kind (m)
- `SupportTop`: `GroundHeight` + ΣMatDepth
- `CoverDensity`: effective elements per m² per kind (table × cover channel)
- `Surface`, plus the blend surfaces and weights over the 4 nearest cell centres
- `Feature`, `FeatureId` and `FeatureDepth` (pitfalls)
- `Flags`

Relief, ridges and pitfalls SHALL be deterministic functions of position, map seed and surface. Single-point helpers MAY exist for tools.

#### Scenario: Matches the rendered surface
- **WHEN** 10,000 random points of `sample_patch` are sampled, including cell diagonals and chunk seams
- **THEN** each `TerrainHeight` matches an independent evaluation of the renderer's triangle, and a downward Jolt ray at the same point, within 1 mm

#### Scenario: Continuous across surface borders
- **WHEN** every surface border of `sample_patch` is walked in 1 mm steps
- **THEN** no two adjacent `GroundHeight` samples differ by more than 1 mm, because relief and mat blend bilinearly over the 4 nearest cell centres and pitfalls are never clipped at a cell border

#### Scenario: Normal accuracy
- **WHEN** the normal is checked at 10,000 points away from facet creases
- **THEN** it agrees with a 1 mm central difference of `GroundHeight` within 1e-3 rad. Pitfall walls have finite slope, smoothed over the outer 25 % of the radius.

#### Scenario: Micro-relief bounded
- **WHEN** `GroundHeight` is sampled on a 5 cm grid over 100 m² of any surface, excluding pitfalls
- **THEN** its deviation from `TerrainHeight` has RMS equal to √(amplitude² + ridge-profile RMS²) ± 20 %

#### Scenario: Pitfall is identifiable
- **WHEN** a sample lands in a pitfall
- **THEN** `Feature` is pitfall, and `FeatureId` and `FeatureDepth` identify it, so the physics log can name the hole a leg dropped into

#### Scenario: Outside the map
- **WHEN** a point lies outside the map
- **THEN** terrain and surface clamp to the edge cell, `OutsideMap` is set, and nothing throws

### Requirement: Deterministic micro-detail
`MicroDetailNear(center3D, radius, kindMask, results)` SHALL return every stem, straw, twig and litter element of the masked kinds **whose segment (with its radius) meets the query sphere**, even when its base lies outside the sphere.
Elements SHALL come from integer hashing of world cells, kind, slot and the map seed, in the canonical order (cell z, cell x, kind, slot). Each element SHALL carry:
- a stable 64-bit `Id`
- `Kind` and `Surface`
- `Base`: standing grass roots at `GroundHeight`; lying elements rest on `SupportTop`
- `Direction`, `Length`, `Diameter`
- `TipStiffness` = table × (d/d̄)⁴ × (L̄/L)³
- `HookRelease`, drawn by the element hash from the cover's range
- `Hooks`

Results SHALL go into a caller buffer, and the true count SHALL be returned so overflow is visible. The renderer SHALL draw near-camera micro-detail from the same generator.

#### Scenario: Replay identity
- **WHEN** the same query runs in two separate processes
- **THEN** both return bit-identical element lists

#### Scenario: Overlapping queries agree
- **WHEN** two queries overlap
- **THEN** every element inside both appears in both, with identical values and in the same relative order

#### Scenario: Crossing straw is found
- **WHEN** a 1 m lying straw is rooted outside the query sphere but crosses through it
- **THEN** the straw is returned

#### Scenario: Density matches the surface
- **WHEN** elements are counted over 100 m² of `belt_straw` at cover density 1.0
- **THEN** the elements per m² match the surface table within ± 5 %

#### Scenario: Overflow reported
- **WHEN** the caller buffer is smaller than the result
- **THEN** the buffer is filled in canonical order, and the returned count exceeds its length

### Requirement: Static contacts
The API SHALL answer sphere and capsule queries, including a swept form from the previous pose to the current one, with a margin. The queries run against every catalog collision shape, every wire and every runtime object. Each `StaticContact` gives:
- point
- outward normal
- signed distance (negative = penetration)
- material
- object and shape index
- the wire parameter (0–1, or −1)

Results SHALL be in canonical order (object, shape), allocation-free, in pure C# with no scene tree (D-010).

#### Scenario: No tunnelling through a wire
- **WHEN** a 15 mm sphere is swept 60 mm per step across a 5 mm wire, at any phase
- **THEN** the contact is always reported

#### Scenario: Material reported
- **WHEN** a capsule touches a steel-rail shape
- **THEN** the contact carries the steel material id

### Requirement: Raycast
`Raycast(rays, maxDistance, hits)` SHALL test a batch of rays against terrain and objects. Each `RayHit` gives distance (or +∞), point, normal, material, object (−1 for terrain) and surface (for terrain hits). Results are allocation-free, in pure C#.

#### Scenario: Roof under a rotor
- **WHEN** a ray is cast downward from 1 m above a sheet-metal roof
- **THEN** it hits at 1 m ± 1 mm, with the sheet-metal material and that roof's object index

### Requirement: Wind obstacles
The API SHALL expose, read-only on a 2 m grid, `TopM` and `BaseM` (m above terrain) and `Porosity`. Row 0 is north, and cell centres are at −size/2 + (i + 0.5) × 2 m.
`Porosity` is the optical porosity of a 2 m horizontal path through the cell, so a path through n cells has porosity β₁·β₂·…·βₙ. The loader SHALL derive it from each asset's wind volume, or its collision shapes if it has none, as β_cell = 1 − f·(1 − β_obj^(2 m / D_obj)), where f is the covered fraction and D_obj is the volume's mean horizontal extent. Overlaps multiply porosity, take the maximum top and the minimum base.

#### Scenario: House shadow
- **WHEN** a solid 6 m tall house stands on flat ground
- **THEN** cells under its footprint report `TopM` ≈ 6 m (± one cell of edge blur), `BaseM` = 0 and porosity ≤ 0.1, while open ground reports 0 m

#### Scenario: Tree crown
- **WHEN** a `tree_proxy` stands on flat ground
- **THEN** cells under its crown report `BaseM` ≈ 4 m and `TopM` ≈ 10 m, and a horizontal path across the crown has porosity ≈ its `wind_porosity`

### Requirement: Gaps near a point
`GapsNear(center, radius, gaps)` SHALL return the world-space fly-through gap rectangles (centre, normal, up, width, height, object, name) from the catalog.

#### Scenario: Door gap
- **WHEN** the query is centred 5 m in front of the gate asset
- **THEN** the gate's named gap is returned with its world-space centre and size

### Requirement: Runtime objects
Before a flight starts, the game SHALL be able to add catalog objects, such as the launch rails at a start point. Once added, they are static and included in static contacts, raycasts and the wind grid.

#### Scenario: Rails added
- **WHEN** the rails asset is added at a start point
- **THEN** a capsule resting on them returns contacts with the steel material

### Requirement: Content hash
The API SHALL expose a 64-bit hash of the map package files plus `surfaces.json` and `catalog.json`. The physics log records it, so a replay can refuse to run on a mismatched world.

#### Scenario: Any change is detected
- **WHEN** one byte of any hashed file changes
- **THEN** the content hash changes

### Requirement: Determinism and threading
The ground function, the micro-detail generator, static contacts and raycasts SHALL use only correctly rounded IEEE operations (+ − × ÷ √): no `Math.Sin/Cos/Exp/Pow` and no implicit FMA. They SHALL be reentrant, keep no shared scratch state, need no scene tree, and be callable from a physics thread while the renderer uses the generator.

#### Scenario: Golden file
- **WHEN** the determinism selftest runs the committed golden inputs
- **THEN** every output hash matches the committed golden file in Debug and Release builds, and on any other machine that runs it

#### Scenario: Concurrent use
- **WHEN** a physics thread and the render thread query the same region at the same time for 10 s
- **THEN** every result equals the single-threaded result

### Requirement: Physics-grade performance
Queries SHALL allocate nothing on the managed heap in steady state. On the dev machine:
- 100,000 ground samples SHALL take under 10 ms
- `MicroDetailNear` with r = 2 m on the densest surface SHALL take under 0.25 ms
- 100,000 capsule queries near `sample_patch` objects SHALL take under 100 ms
- 100,000 rays of ≤ 2 m SHALL take under 100 ms
- one worst-case physics step (44 ground samples + 46 swept capsules + 8 rays) SHALL take under 60 µs

#### Scenario: Benchmark
- **WHEN** the world-query benchmark selftest runs on the dev machine
- **THEN** every timing is under its limit, with zero GC allocations during the timed sections
