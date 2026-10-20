# asset-review-sheet Specification

## Purpose
Defines the automated review sheet every asset gets before it enters the gallery: which views, which light, which overlays, and where the sheet is written.

## Requirements

### Requirement: Sheet content
For each variant of an asset id, the capture SHALL render, in both day and overcast light: four distance shots at 1.5, 10, 50 and 200 m; an 8-view turntable at 45° steps; and a collision overlay view. Every view SHALL include a 1.8 m human scale bar, and every tile SHALL be labelled with variant, view, distance and light.

#### Scenario: K01 sheet is complete
- **WHEN** `& $env:UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Review.Capture -asset K01 -logFile -` runs
- **THEN** it exits 0 and writes a sheet for `K01_whole` with 4 distance tiles, 8 turntable tiles and 1 overlay tile for each of the two lights (26 tiles), each labelled

#### Scenario: Distances are measured from the asset
- **WHEN** a distance tile is rendered
- **THEN** the camera's straight-line distance to the asset's bounds centre equals the labelled distance within 1 %

### Requirement: Collision overlay shows what the sim knows
The overlay SHALL draw the collision primitives, gaps and wind volumes from the asset's metadata file (not from Unity colliders), each kind in its own colour, over the rendered asset.

#### Scenario: Overlay matches the metadata
- **WHEN** K01's overlay tile is rendered
- **THEN** it shows exactly one box outline matching the metadata box, and a legend naming the colours

### Requirement: Location and failure
Sheets SHALL be written as PNG to `review-sheets/<ID>/`, which is git-ignored, and the capture SHALL exit non-zero if the asset id, a prefab or its metadata is missing.

#### Scenario: Unknown id fails
- **WHEN** the capture runs with `-asset NOPE`
- **THEN** it exits non-zero and writes nothing

### Requirement: Part of the one command
`pwsh tools/build_asset.ps1 <ID>` SHALL end by rendering the review sheet.

#### Scenario: End to end with sheet
- **WHEN** `pwsh tools/build_asset.ps1 K01` runs
- **THEN** it exits 0, and the K01 sheet in `review-sheets/K01/` is newer than the K01 prefab

### Requirement: An empty stage fails the capture
The capture SHALL exit non-zero when the asset's renderers have no mesh or no texture at the 1.5 m view, naming the missing thing, instead of writing a sheet of empty tiles.

#### Scenario: LFS pointer instead of an FBX
- **WHEN** the asset's FBX is an LFS pointer file
- **THEN** the capture exits non-zero and writes no sheet

### Requirement: Surface assets are shot from on the surface
For an asset of catalog class `surfaces`, the review capture SHALL shoot the pilot-height tile from 1.5 m above the surface at the patch centre, looking down at the surface at a steep angle with the pilot-height bar in frame ahead of the camera, instead of placing the camera outside the asset's bounds; the 10 m, 50 m and 200 m tiles, the turntable and the collision overlay keep their framing.

#### Scenario: Pilot-height tile of a ground patch
- **WHEN** the capture renders a `surfaces` asset 12 m across
- **THEN** the 1.5 m tile shows the surface filling the lower frame from under the camera to a few metres ahead, with the pilot-height bar visible, and the capture log prints the camera position and look-down angle it used

#### Scenario: Other classes unchanged
- **WHEN** the capture renders K01
- **THEN** its tiles are pixel-identical to the previous capture

### Requirement: Earthworks are shot along the run
For an asset of catalog class `earthworks` with a `holes` record, the review capture SHALL shoot the pilot-height tile from 1.5 m above the lip at the first point of the hole's centre line, looking along the run and 20° down, with the pilot-height bar in frame beside the cut; the 10 m, 50 m and 200 m tiles, the turntable and the collision overlay keep their framing.

#### Scenario: Pilot-height tile of a trench
- **WHEN** the capture renders an `earthworks` asset with a 24 m run
- **THEN** the 1.5 m tile shows the cut running away from the camera between its parapets with the bar visible, and the capture log prints the camera position and look direction it used

#### Scenario: Other classes unchanged
- **WHEN** the capture renders K01 and S04
- **THEN** their tiles are pixel-identical to the previous capture
