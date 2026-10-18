# Spec Delta

## ADDED Requirements

### Requirement: Baked impostor LOD
A generator MAY ask for an impostor: the build SHALL render the LOD0 mesh with its materials from a fixed set of views in headless Blender into one atlas (1k at most) with alpha, write it beside the FBX as a derived texture, and the importer SHALL build it as the last LOD before the cull: a camera-facing card set reading the atlas. The atlas is regenerated whenever the params or materials change and is never hand-edited.

#### Scenario: Impostor built and switched
- **WHEN** a tree variant's params set `impostor: true` with a switch distance
- **THEN** the build writes `<variant>_impostor.png` at 1k with alpha, the metadata lists the impostor LOD with its switch distance, and the prefab's LOD group shows the card set beyond that distance and the mesh LODs before it

#### Scenario: Colour of the impostor matches the mesh
- **WHEN** the 50 m tile is rendered once with the impostor forced and once with the last mesh LOD
- **THEN** the mean colour of the crown's pixels differs by less than 6 % in luminance between the two
