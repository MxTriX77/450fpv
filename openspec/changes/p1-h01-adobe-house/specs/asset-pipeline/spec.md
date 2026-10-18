# Spec Delta

## ADDED Requirements

### Requirement: Generator-written mask atlases
A generator MAY write derived textures of its own beside the FBX (a per-wall damage-mask atlas, a facade map), each named in the metadata under `textures` with its role and its channels, regenerated on every build and never hand-edited; a material recipe MAY reference such a texture by role, and the importer SHALL bind it to the material's slot for that role.

#### Scenario: Mask atlas bound
- **WHEN** a house variant is built with `render_loss` 0.3
- **THEN** the build writes `<variant>_damage.png` with R as render depth, G soot, B dirt and A base band, the metadata lists it under `textures` with role `damage_mask`, and the imported facade material reads it in its damage-mask slot

#### Scenario: Mask changes with the params
- **WHEN** `render_loss` is changed to 0.6 and the asset rebuilt
- **THEN** the atlas is rewritten with more of R at full depth and nothing else in the prefab changes

### Requirement: Composed kit pieces
A generator MAY compose pieces of another generator (debris kit pieces as a house's rubble) through that generator's library builder, placing each piece with its own collision shapes, snags and material in the host's metadata; the audit SHALL check the composed pieces as part of the host, and the host's `materials` SHALL list every slot the pieces bring.

#### Scenario: Rubble pieces audited with the house
- **WHEN** a house variant composes 300 tile shards and 40 timber pieces
- **THEN** its metadata holds one shape per piece (two for a cambered tile), each within 5 cm of its mesh in the audit, and the audit prints the piece count it checked
