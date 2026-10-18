# Spec Delta

## ADDED Requirements

### Requirement: Holes in the metadata
The metadata schema MAY carry `holes`: for each cut an ordered centre-line point list, `top_width_m`, `depth_m` and `band_width_m`, so the map compiler can mark the hole cells and raise or lower the heightfield without re-reading the mesh. The array SHALL be written empty when the asset cuts nothing, the validator SHALL check its fields, and the physics-metadata component SHALL expose it.

#### Scenario: A trench writes its hole
- **WHEN** a trench variant of 24 m is built
- **THEN** its metadata holds one `holes` record whose centre line has at least one point per metre and whose band width is at least the top width plus 0.5 m, and the prefab's metadata component exposes it

#### Scenario: Hole validated
- **WHEN** a `holes` record has fewer than two points or a band narrower than its top width
- **THEN** the validator rejects the metadata naming the record and the rule
