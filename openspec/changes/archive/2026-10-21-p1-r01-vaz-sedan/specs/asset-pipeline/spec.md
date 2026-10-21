# Spec Delta

## ADDED Requirements

### Requirement: Per-pane crack masks on a transparent material
A generator MAY write a crack mask per glass pane (a derived texture under `textures` with role `crack_mask` and the pane's slot) from the impact points it placed, and a recipe MAY be transparent (`blend: transparent`, a tint and an index of refraction tint); the importer SHALL build a transparent material per pane reading its crack mask, and the pane's holed area SHALL be cut from its geometry, never only painted.

#### Scenario: Crazed windscreen
- **WHEN** a vehicle variant sets `windscreen` to `crazed_holed`
- **THEN** the build writes `<variant>_windscreen_cracks.png` with radial and concentric lines from the impact points, the metadata lists it under `textures` with role `crack_mask` and slot `windscreen`, the imported pane material is transparent and reads it, and the hole is an opening in the pane mesh with `edge` snags

#### Scenario: Intact pane
- **WHEN** a pane is `intact`
- **THEN** no crack mask is written for it and the pane's material is the plain transparent glass

### Requirement: A LOD frame inside the bounds
An asset's metadata MAY carry `bounds.lod`, an axis-aligned box (min and max, metres, Unity's frame) round the body its LOD switches are judged on, for an asset whose bounds also hold loose pieces, such as a vehicle's scatter. The importer SHALL put the LOD group's reference point at that box's centre and size it by the box, and SHALL use the bounds when `bounds.lod` is absent.

#### Scenario: A car's LOD group frames the body, not its scatter
- **WHEN** `R01_A`'s metadata carries bounds 6.63 m long with its scatter and a `bounds.lod` round the body
- **THEN** the prefab's LOD group sits at the body's centre with the body's size (4.07 m), and an asset with no `bounds.lod` frames its LOD group on its bounds as before
