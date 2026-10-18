# Spec Delta

## ADDED Requirements

### Requirement: Per-variant material overrides
A catalog entry MAY give a variant its own slot-to-material map (`variant_materials`: variant → {slot: material}) that overrides the asset's `materials` for that variant only; the importer SHALL apply it and the review capture SHALL show it.

#### Scenario: Autumn leaf on one variant
- **WHEN** `V13_mid_autumn` maps its `leaf` slot to `leaf_shrub_autumn` while the asset maps `leaf` to `leaf_shrub`
- **THEN** the autumn variant's prefab uses `leaf_shrub_autumn`, every other variant keeps `leaf_shrub`, and rebuilding leaves no diff

### Requirement: Baked impostor LOD
A generator MAY ask for an impostor: the build SHALL render the LOD0 mesh with its materials from a fixed set of views in headless Blender into one atlas (1k at most) with alpha, write it beside the FBX as a derived texture, and the importer SHALL build it as the last LOD before the cull: a camera-facing card set reading the atlas. The atlas is regenerated whenever the params or materials change and is never hand-edited.

#### Scenario: Impostor built and switched
- **WHEN** a tree variant's params set `impostor: true` with a switch distance
- **THEN** the build writes `<variant>_impostor.png` at 1k with alpha, the metadata lists the impostor LOD with its switch distance, and the prefab's LOD group shows the card set beyond that distance and the mesh LODs before it

#### Scenario: Colour of the impostor matches the mesh
- **WHEN** the 50 m tile is rendered once with the impostor forced and once with the last mesh LOD
- **THEN** the mean colour of the crown's pixels differs by less than 6 % in luminance between the two

## MODIFIED Requirements

### Requirement: Asset metadata contract
The metadata JSON SHALL state its schema version and units, and SHALL hold, in Unity's frame (D-019) and in metres: bounds; LODs with triangle counts and switch distances, and a cull distance; material slots, each with a visual material and a physics material id; collision primitives (box, sphere, capsule, cylinder), each with a material id and a landable flag; gaps as planar polygons with their narrowest width; wind volumes with per-part porosity; snag records, each with a kind, at least two points, a radius and the physics material of the member it follows.

The conventions SHALL be fixed and validated at build time: shapes are convex; `rotation` is a unit quaternion `[x, y, z, w]` applied about the shape's `center`; capsules and cylinders run along local +Y before rotation, with `height` the total length including caps (a capsule needs `height ≥ 2·radius`); a gap `polygon` is the throat of the opening, planar within 10 mm, simple, concave allowed, with a unit `normal` of free sign and meaningless winding, and `narrowest_m` the diameter of its largest inscribed circle; empty `wind_volumes` means the asset blocks no wind; `snag[].kind` is `edge` or `hook`, each with at least two points, a `radius_m` and a `material` (`bark` on stems, limbs and branches, the twig slot's physics on twigs); `landable` is informational and never the contact law. Every physics material id SHALL come from the closed, physics-owned list in `assets-src/physics_materials.json`.

#### Scenario: Convention violations are rejected
- **WHEN** a metadata file carries a non-unit quaternion, a capsule with `height < 2·radius`, a gap polygon that is not planar within 10 mm or whose `normal` is not perpendicular to it, a `narrowest_m` outside `(0, polygon extent]`, or a snag of unknown kind or with fewer than two points
- **THEN** the build's validator rejects it, naming the rule, and nothing is written

#### Scenario: A snag record without its material is rejected
- **WHEN** a snag record carries no `material`, or one that is not in `assets-src/physics_materials.json`
- **THEN** the build's validator rejects it, naming the record and the rule

#### Scenario: Unknown material id is rejected
- **WHEN** a shape or material slot names a physics material id that is not in `assets-src/physics_materials.json`
- **THEN** the build's validator rejects it

#### Scenario: Frame is checked by a permanent probe
- **WHEN** the EditMode tests run
- **THEN** a committed asymmetric probe mesh, exported through the same path as every asset, imports with its +X, +Y and +Z features at +X, +Y and +Z in Unity (bounds min at the origin, max at the probe's stated size), so a wrong sign in the frame conversion fails the suite

#### Scenario: Collision follows the visual
- **WHEN** K01's collision primitives are compared with its LOD0 mesh
- **THEN** every primitive surface lies within ±5 mm of the mesh surface it stands for (well inside AST-05's ±5 cm), and each primitive has the material id `masonry`

#### Scenario: Frame is Unity's
- **WHEN** the brick is generated lying flat with its long edge along Blender's X
- **THEN** in Unity the prefab's long edge lies along +X, its 65 mm height along +Y, and the metadata bounds match the imported mesh bounds within 1 mm

### Requirement: Collision audit at build time
The build SHALL measure, for every collision primitive, the two-sided deviation from the LOD0 surface it stands for, and fail beyond ±5 cm, or ±2 cm within 0.3 m of a gap polygon or a landable face; it SHALL also check that `narrowest_m` equals the largest inscribed circle of each gap polygon within 5 mm. The primitive-to-mesh direction SHALL measure only the part of a primitive's surface that lies outside every other rigid (non-shell) primitive of the asset, since the drone meets the union of the rigid primitives and a surface buried in another, such as a capsule's cap inside the next capsule of a chain, can never be touched; a shell piece hides nothing, and the mesh-to-primitive direction measures to the union (`p1-v01-locust` review-physics §A.2).

#### Scenario: Collision drift is caught
- **WHEN** a generator's mesh grows 6 cm beyond its declared box
- **THEN** the build exits non-zero, printing the primitive, the deviation and the tolerance

#### Scenario: Buried caps of a capsule chain
- **WHEN** a trunk is two 80 mm capsules along a domed 12-sided tube, each one's cap at the joint buried in the other
- **THEN** the audit passes, measuring only the exposed surface (within 10 mm of the tube), and a capsule moved 7 cm off its tube still fails

### Requirement: Compliant shells in the collision audit
A collision shape whose material is a crown contact material (`twig` or `foliage`) is a shell. The audit SHALL require (a) every point of the surface of the union of the shell pieces above the ground, not inside another piece, to lie within the envelope tolerance (0.1 m, derived from the drone's push against the `twig` contact stiffness) of the nearest face of the shell's slots; (b) every LOD0 vertex of a geometry slot (an opaque recipe, such as twig tubes) to lie inside the union or within 0.1 m outside it; (c) every LOD0 vertex of a card slot (an alpha-clipped recipe, such as leaf cards) to lie inside the union or within the asset's card allowance outside it: its generator's stated card reach plus the 0.1 m tolerance (0.3 m for the shrub's 0.2 m cards, 0.361 m for the tree's 0.25 m leaf cards, whose far corner reaches 1.044 × the leaf length from the petiole; cards have no contact role, their twigs carry snag records); and (d) at most 64 pieces of one shell material whose axis-aligned boxes meet any axis-aligned 2 m cube (derived from the narrow-phase cost in `p1-v01-locust` review-physics; the map compiler holds 96 summed over all objects). It SHALL print each piece's largest standoff and where it is, per slot the largest outside distance, and the largest count of pieces meeting one 2 m cube and where, and fail beyond each tolerance naming the piece or slot, the distance and the tolerance.

#### Scenario: Pieces hugging the mass pass
- **WHEN** two overlapping `twig` spheres hug a leaf mass, every twig vertex inside them and every card corner within 0.25 m outside
- **THEN** the audit passes the shell and prints each piece's largest standoff and each slot's largest outside distance

#### Scenario: Shell too large for its mass
- **WHEN** a `twig` piece's radius is 0.3 m larger than the leaf mass it carries
- **THEN** the audit fails rule (a) naming the piece, its largest standoff and the 0.1 m tolerance

#### Scenario: Twig outside the shell
- **WHEN** one twig vertex lies 0.15 m outside the `twig` shell
- **THEN** the audit fails rule (b) naming the slot, the distance and the 0.1 m tolerance

#### Scenario: Card outside the shell
- **WHEN** one card's corner lies 0.35 m outside the `twig` shell
- **THEN** the audit fails rule (c) for a shrub (0.3 m allowance) naming the slot, the distance and the allowance

#### Scenario: Card allowance follows the card
- **WHEN** a card corner lies 0.33 m outside the shell
- **THEN** it passes rule (c) for a tree's 0.25 m leaf card (0.361 m allowance) and fails it for a shrub's 0.2 m card (0.3 m)

#### Scenario: Too many pieces in one neighbourhood
- **WHEN** 65 pieces of one shell material lie inside one 2 m cube, and the same 65 are spread along 8 m
- **THEN** the first fails rule (d) naming the count, the cube and the 64 limit, and the second passes
