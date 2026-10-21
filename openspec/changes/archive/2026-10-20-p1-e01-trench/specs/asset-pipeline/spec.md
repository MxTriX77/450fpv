# Spec Delta

## ADDED Requirements

### Requirement: Holes in the metadata
The metadata schema MAY carry `holes`: for each cut an ordered centre-line point list, `top_width_m`, `depth_m` and `band_width_m`, so the map compiler can mark the hole cells and raise or lower the heightfield without re-reading the mesh. The array SHALL be written empty when the asset cuts nothing, the validator SHALL check its fields, and the physics-metadata component SHALL expose it.

#### Scenario: A trench writes its hole
- **WHEN** a trench variant of 24 m is built
- **THEN** its metadata holds a `holes` record of the run whose centre line has at least one point per metre and whose band (every point within half its width of the centre line in plan) holds the cavity's widest outline, the crumbled lip included, 0.65 m inside its edge, one record per niche holding the niche the same way, and the prefab's metadata component exposes them

#### Scenario: Hole validated
- **WHEN** a `holes` record has fewer than two points or a band narrower than its top width
- **THEN** the validator rejects the metadata naming the record and the rule
