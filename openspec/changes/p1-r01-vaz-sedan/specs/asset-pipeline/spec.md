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
