# Spec Delta

## Purpose

Defines how a map is stored: its layers, encodings, surface physics table, asset catalog and validation. Artists author maps in this format, and both the renderer and the physics read the same source of truth.

## ADDED Requirements

### Requirement: Package layout
A map SHALL be a folder `game/maps/<id>/` containing:
- `map.json` (the manifest)
- `height.r16` (heightfield)
- `surface.png` (surface IDs)
- `cover.png` (cover densities)
- `objects.json` (placed objects and wires)

The surface table `game/maps/surfaces.json` and the asset catalog `game/assets/catalog.json` SHALL be shared by all maps. A map side SHALL be a multiple of 256 m (the collision chunk size) and at most 8192 m.

#### Scenario: Complete package
- **WHEN** the validator runs on `game/maps/sample_patch/`
- **THEN** it finds all five files and exits 0

#### Scenario: Bad map size
- **WHEN** the manifest declares a side that is not a multiple of 256 m, or is larger than 8192 m
- **THEN** the validator exits non-zero and reports the side and the rule

#### Scenario: Missing layer
- **WHEN** any of the five files is missing
- **THEN** the validator exits non-zero and names the missing file

### Requirement: Coordinates and units
All map data SHALL use metres and a right-handed Y-up frame: +X east, +Y up, +Z south (Godot's −Z is north). The map centre SHALL be the world origin. Grid row 0 SHALL be the northmost row and column 0 the westmost.

#### Scenario: Corner lookup
- **WHEN** a 256 m map is queried at world (−128, −128) in x and z
- **THEN** the value comes from row 0, column 0 of each layer

### Requirement: Layer encodings
- `height.r16` SHALL be raw unsigned 16-bit little-endian samples, row-major, on a vertex grid of `size_m / height.resolution_m + 1` samples per side. Height in metres = `height.offset_m + sample × height.scale_m`.
- `surface.png` SHALL be 8-bit greyscale on a cell grid of `size_m / surface.resolution_m` pixels per side. Each value is a surface `index` from the surface table. 0 is invalid.
- `cover.png` SHALL be RGBA8 on the same cell grid as the surface layer. R = grass stems, G = lodged straw, B = twigs, A = leaf litter. Each channel is a 0–1 multiplier on the surface's cover density.

The manifest SHALL declare every resolution, dimension, scale, offset and a 32-bit map `seed`.

#### Scenario: Size mismatch
- **WHEN** `height.r16` has a byte count different from (samples per side)² × 2
- **THEN** the validator exits non-zero and reports the expected and actual sizes

#### Scenario: Unknown surface
- **WHEN** `surface.png` contains an index missing from the surface table
- **THEN** the validator exits non-zero and reports the index and the first pixel where it appears

### Requirement: Surface table
The table SHALL declare one soil reference contact diameter (m) at which the soil moduli are defined. Each surface SHALL define:
- `id` and `index`
- soil contact parameters in SI units: bearing stiffness (N/m³), damping (N·s/m³), static and kinetic friction, maximum sink (m), and an unload stiffness ratio (≥ 1) for plastic sink
- micro-relief: roughness amplitude and wavelength (m), plus optional directional ridges (amplitude and spacing in m, azimuth in degrees)
- pitfalls: density (per m²), depth range and radius range (m)
- per cover entry:
  - type, height range (m), elements per m², element diameter (m)
  - lateral stiffness (N/m): the tip stiffness of an element of mean length and mean diameter
  - hook probability (0–1) and hook release force range (N)
  - an optional mat: depth range (m), compressive modulus (Pa), damping ratio, static and kinetic friction
- dust emission (0–1) and a visual material

The initial table SHALL contain the 9 surfaces of `docs/reference-notes/terrain.md` §7, with values signed off by the physics engineer as starting points.

#### Scenario: Required fields
- **WHEN** any surface lacks a required field or has a value outside its physical range (e.g. negative stiffness, unload ratio < 1, mat depth min > max, kinetic friction > static, zero element length or diameter on a cover that has elements)
- **THEN** the validator exits non-zero and names the surface and the field

### Requirement: Asset catalog and objects
Every object in `objects.json` SHALL reference an asset id from the catalog, with position (m), rotation (yaw, pitch and roll in degrees) and uniform scale.
The catalog SHALL hold a shared material table. Each material gives static and kinetic friction, an optional local contact stiffness (N/m) with damping ratio (absent = rigid), and an edge radius (m).
Catalog entries SHALL give:
- the scene path
- collision as primitive shapes (box, sphere, capsule, cylinder): required unless the asset is tagged `visual_only`
- a `material` for every asset with collision, which any shape may override
- `snag_hazard`
- an optional `wind_volume` (primitive shapes), used instead of collision for wind obstacles
- `wind_porosity`: the optical porosity of the wind volume seen side-on (0 = solid to 1 = open)
- any named fly-through gaps

Wires SHALL be objects of type `wire`, with a point list, sag (m) and diameter (m).

#### Scenario: Unknown asset
- **WHEN** an object references an asset id that is not in the catalog
- **THEN** the validator exits non-zero and names the object index and the id

#### Scenario: Unknown material
- **WHEN** an asset or shape names a material id missing from the material table
- **THEN** the validator exits non-zero and names the asset and the id

#### Scenario: Out of bounds
- **WHEN** an object lies outside the map area
- **THEN** the validator exits non-zero and names the object

### Requirement: Start points
The manifest SHALL accept an optional list of start points, each with a position (m) and yaw (degrees). When legs are off, the game places the launch rails at the chosen start point.

#### Scenario: Start outside the map
- **WHEN** a start point lies outside the map area
- **THEN** the validator exits non-zero and names the start point

### Requirement: Versioning
The manifest SHALL carry `format_version` as `major.minor`. The validator and the loader SHALL reject a major version they don't support, with a message naming both versions.

#### Scenario: Future major
- **WHEN** a package declares a major version higher than supported
- **THEN** loading fails with one clear error, and the sandbox falls back to its placeholder

### Requirement: No real-world reference
Map packages SHALL NOT contain geographic coordinates, real place names or any georeferencing. Maps are composites inspired by the reference, never replicas of a real location.

#### Scenario: Georeference scan
- **WHEN** the validator scans the manifest and objects
- **THEN** it rejects latitude, longitude, geo or EPSG fields and any text field matching a real-place blocklist
