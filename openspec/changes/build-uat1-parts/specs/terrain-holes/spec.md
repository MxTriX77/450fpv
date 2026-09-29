# Spec Delta

## Purpose

Lets the ground have openings narrower than the 1 m heightfield can express, such as trenches and cellar entrances. They are consistent everywhere: the pilot sees them, the drone and fiber fall into them, and physics knows their walls are soil. This capability extends `map-format` (an optional layer, format 1.1, `surface:<id>` materials) and `world-query` (the `Hole` flag, `Surface` on contacts, hashing, micro-detail, wind).

## ADDED Requirements

### Requirement: Holes in the heightfield
A map package SHALL be able to mark terrain cells as holes with an optional layer `holes.png`:
- 8-bit greyscale on the surface-cell grid (`cells_per_side`² pixels; a cell is `surface.resolution_m`, 0.5 m by default)
- 0 = ground, 255 = hole; any other value is invalid
- it has an `importer="keep"` sidecar like the other layers

Holed terrain SHALL be skipped by the renderer (including its depth and shadow passes) and by terrain collision. Each hole SHALL be filled by placed catalog objects that meet the hole-filler requirement. The layer is format 1.1. The content hash SHALL include `holes.png`, after `cover.png`, when it exists.

#### Scenario: Invalid hole value
- **WHEN** `holes.png` contains a value other than 0 or 255
- **THEN** the validator exits non-zero and names the first such pixel

#### Scenario: Hole layer size mismatch
- **WHEN** `holes.png` isn't `cells_per_side`² pixels
- **THEN** the validator exits non-zero and reports the expected and actual sizes

#### Scenario: Holes are hashed
- **WHEN** one hole pixel is flipped
- **THEN** the content hash changes, and a map without `holes.png` keeps its previous hash

#### Scenario: Trench is open
- **WHEN** a capsule the size of the drone is swept down into the test trench
- **THEN** it passes the terrain level with no terrain contact, and its first contact is a filler's floor or wall

### Requirement: World query knows holes
For a point inside the map whose own cell is a hole, `SampleGround` SHALL:
- set `Hole`
- keep `TerrainHeight` as the triangle height (the lip level)
- return −∞ for `GroundHeight` and `SupportTop`
- return +Y for `Normal`
- return 0 for `MatDepth` and `CoverDensity`, and no feature

The ground in a hole is given only by its fillers' shapes, through `StaticContacts` and `Raycast`. A ray SHALL NOT hit the terrain where it crosses the triangles over a hole cell. The existing jump, normal, relief-RMS, density and lying-element scenarios exclude hole cells. "Matches the rendered surface" still holds for `TerrainHeight` everywhere, and for Jolt rays outside hole cells. `QueryVersion` SHALL be 3.

#### Scenario: Sample over a hole
- **WHEN** `SampleGround` runs on a grid across the test trench
- **THEN** lip-band and cavity points report exactly the values above, and points 1 mm outside the boundary report ordinary ground

#### Scenario: Rays into a trench
- **WHEN** rays are cast down into the trench, and at 45°
- **THEN** each one hits the floor or a wall and carries that shape's `Surface`, never the terrain

### Requirement: Soil materials on shapes
A collision shape's material MAY be `surface:<id>`, naming a surface of `surfaces.json`. Its contacts and ray hits SHALL carry `Surface` = that surface's index and `Material` = `Catalog.NoMaterial`, and `Geometry` SHALL give the same `Surface`. All other contacts carry `Surface` = 0. The validator SHALL reject an unknown surface id, naming the asset and the id.

#### Scenario: Trench wall is soil
- **WHEN** a static contact touches a trench wall
- **THEN** its `Surface` is the trench's surface (`belt_bare`), its `Material` is `NoMaterial`, and `Surface(index)` gives that surface's soil parameters

#### Scenario: Unknown soil surface
- **WHEN** a shape names `surface:no_such_surface`
- **THEN** the validator exits non-zero and names the asset and the id

### Requirement: Micro-detail and wind respect holes
`MicroDetailNear` SHALL NOT return an element rooted in a hole cell, nor a lying element whose tip lies in one. The renderer's near ring and far layer SHALL draw nothing in hole cells. A wind-volume shape SHALL add nothing to a wind cell where its top is at or below the terrain.

#### Scenario: No stems in the trench
- **WHEN** `MicroDetailNear` is queried over the test trench
- **THEN** no element is returned from hole cells, and every other element keeps its `Id` and values

#### Scenario: Buried filler adds no wind obstacle
- **WHEN** the test trench is placed
- **THEN** the wind grid is bit-identical to the grid without it

### Requirement: Hole fillers
The objects filling a hole SHALL meet these rules:
- **F-1, sealed below:** every point of a hole cell has filler collision under it.
- **F-2, cavity inside the hole:** every cavity face below `TerrainHeight` lies at least 0.25 m horizontally inside the hole cells.
- **F-3, level lip:** on the hole side of the boundary, the filler's top is within ±0.03 m of `TerrainHeight`.
- **F-4, buried outside:** outside the hole cells, no filler shape rises above `TerrainHeight` + 0.01 m.
- **F-5, thick and overlapping:** a shape carrying an open face is at least 0.3 m thick behind it, and shapes overlap by at least 0.2 m at every joint.
- **F-6, open faces follow the visual:** within 0.05 m.
- **F-7, walls and floor are soil:** they use `surface:belt_bare` (firm cut soil). Parapets stay terrain.
- **F-8, no wind obstacle** (see the wind rule).

These rules apply to the test trench, the cellar entrance and the trench section asset.

#### Scenario: Sealed and inset
- **WHEN** downward rays from `TerrainHeight` + 0.5 m on a 5 cm grid cover every hole cell, and points within 0.25 m of the boundary are tested from `TerrainHeight` down to the cavity floor
- **THEN** every ray hits a filler shape, and every tested point is inside a filler shape

#### Scenario: Lip and burial
- **WHEN** the filler top is sampled along every boundary edge, and a 5 cm grid covers a 1 m band around each hole
- **THEN** the lip is within ±0.03 m of `TerrainHeight`, and no filler shape rises above `TerrainHeight` + 0.01 m outside the hole cells

#### Scenario: Open faces match the visual
- **WHEN** horizontal rays cross the cavity at three heights
- **THEN** each hits a filler face within 0.05 m of the visual mesh

### Requirement: Golden and performance with holes
A test trench SHALL be added to `sample_patch` with its `holes.png`:
- a straight section and a 30° section, each 0.8 m wide and 1.5 m deep
- a placeholder asset of boxes with `surface:belt_bare`
- its objects appended to `objects.json`, so existing indices don't move

New golden cases cover:
- `SampleGround` across both sections: cavity, lip band, and the boundary ± 1 mm
- rays straight down, at 45° and grazing
- foot-sphere contacts on the floor, on a wall and in the inner corner
- `MicroDetailNear` straddling a boundary
- the wind grid

With holes present, the composite step (< 60 µs, zero allocations) and the micro-detail limits SHALL still hold.

#### Scenario: Golden re-recorded
- **WHEN** the golden file is re-recorded after this change
- **THEN** every case away from the trench keeps its recorded output, and the content hash and `QueryVersion` 3 are the only other changes. `--golden` passes in Debug and Release

#### Scenario: Budget with holes
- **WHEN** the world-query benchmark runs on AC with the test trench present
- **THEN** the composite step stays under 60 µs with zero allocations
