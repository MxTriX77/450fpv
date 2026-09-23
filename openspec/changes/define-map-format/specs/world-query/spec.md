# Spec Delta

## Purpose

The fast, deterministic runtime questions physics asks about the world: ground height and softness under a leg, the stems and straws touching it, and what blocks the wind. They make every landing different, yet exactly replayable.

## ADDED Requirements

### Requirement: Terrain and ground height
The API SHALL provide:
- terrain height: bilinear from the heightfield, the same surface the renderer draws
- ground height: terrain height plus the surface's micro-relief plus pitfalls
- ground normal

Micro-relief and pitfalls SHALL be deterministic functions of world position, map seed and surface.

#### Scenario: Bilinear accuracy
- **WHEN** terrain height is queried at 10,000 random points of `sample_patch`
- **THEN** each result matches an independent bilinear evaluation within 1 mm

#### Scenario: Micro-relief bounded
- **WHEN** ground height is sampled on a 5 cm grid over 100 m² of any surface
- **THEN** its deviation from terrain height has RMS equal to the surface's roughness amplitude ± 20 %, except inside pitfalls, which reach their declared depth range

### Requirement: Surface and cover lookup
The API SHALL return, at any point, the surface record and its effective cover densities (the surface cover density × the cover-layer channel).

#### Scenario: Surface boundary
- **WHEN** two points 0.1 m apart straddle a surface-cell border
- **THEN** each returns the surface of its own cell

### Requirement: Deterministic micro-detail
`MicroDetailNear(center, radius)` SHALL return every stem, straw, twig and litter element in that radius. Each element has kind, base position, height, diameter, lean direction, lateral stiffness and hook flag.
Elements SHALL come from integer hashing of world cells and the map seed. The result SHALL be independent of query order, query radius and chunking. The renderer SHALL draw near-camera micro-detail from the same generator.

#### Scenario: Replay identity
- **WHEN** the same query runs in two separate processes
- **THEN** both return bit-identical element lists

#### Scenario: Overlapping queries agree
- **WHEN** two queries overlap
- **THEN** every element inside both appears in both, with identical values

#### Scenario: Density matches the surface
- **WHEN** elements are counted over 100 m² of `belt_straw` at cover density 1.0
- **THEN** the stems per m² match the surface table within ± 5 %

### Requirement: Wind obstacles
The API SHALL provide, on a 2 m grid, the obstacle top height above ground (m) and porosity (0–1). These are derived at load time from the terrain and from the objects' catalog `wind_porosity`.

#### Scenario: House shadow
- **WHEN** a solid 6 m tall house is placed on flat ground
- **THEN** cells under its footprint report about 6 m (± one grid cell of edge blur) and porosity ≤ 0.1, and open ground reports 0 m

### Requirement: Physics-grade performance
Queries SHALL allocate nothing on the managed heap in steady state. On the dev machine:
- 100,000 combined surface + ground-height queries SHALL complete in under 10 ms
- `MicroDetailNear` with radius 2 m on the densest surface SHALL complete in under 0.25 ms

#### Scenario: Benchmark
- **WHEN** the world-query benchmark self-test runs on the dev machine
- **THEN** it reports both timings under their limits and zero GC allocations during the timed section
