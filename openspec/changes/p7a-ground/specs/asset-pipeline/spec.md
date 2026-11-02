# Spec Delta

## MODIFIED Requirements

### Requirement: Asset metadata contract
The metadata JSON SHALL state its schema version and units, and SHALL hold, in Unity's frame (D-019) and in metres: bounds; LODs with triangle counts and switch distances, and a cull distance; material slots, each with a visual material and a physics material id; collision primitives (box, sphere, capsule, cylinder), each with a material id and a landable flag; gaps as planar polygons with their narrowest width; wind volumes with per-part porosity; snag records, each with a kind, at least two points, a radius and the physics material of the member it follows. It MAY hold (`p7a-ground`): `micro_elements`, the drawn elements physics reads, each with its kind (`straw`, `stem`, `twig` or `leaf`), its centre line as two or three points, its diameter (a leaf's thickness is ignored), a leaf's area, whether it is anchored (rooted, or grown from a parent), and the index of the element or collision shape it grows from (−1 for none), in a sidecar file the metadata names when it is large, hashed with the metadata; `features`, each ground feature of the asset with its kind (`pitfall`), id, centre, radius, depth and the indices of the collision shapes it is made of; and on a `bark` or `deadwood` capsule a `cantilever` record, the chain length from the member's root to the capsule's end A (m).

The conventions SHALL be fixed and validated at build time: shapes are convex; `rotation` is a unit quaternion `[x, y, z, w]` applied about the shape's `center`; capsules and cylinders run along local +Y before rotation, with `height` the total length including caps (a capsule needs `height ≥ 2·radius`); a gap `polygon` is the throat of the opening, planar within 10 mm, simple, concave allowed, with a unit `normal` of free sign and meaningless winding, and `narrowest_m` the diameter of its largest inscribed circle; empty `wind_volumes` means the asset blocks no wind; `snag[].kind` is `edge` or `hook` (a `hook` is a free tip: no other member leaves its last point), each with at least two points, a `radius_m` and a `material` (`bark` on stems, limbs and branches, the twig slot's physics on twigs); `landable` is informational and never the contact law; a micro element's diameter is positive, its points distinct and its parent index in range; a feature's shapes exist and lie within its radius plus their own size; a `cantilever` length is non-negative and sits only on a `bark` or `deadwood` capsule. Every physics material id SHALL come from the closed, physics-owned list in `assets-src/physics_materials.json`.

#### Scenario: Convention violations are rejected
- **WHEN** a metadata file carries a non-unit quaternion, a capsule with `height < 2·radius`, a gap polygon that is not planar within 10 mm or whose `normal` is not perpendicular to it, a `narrowest_m` outside `(0, polygon extent]`, or a snag of unknown kind or with fewer than two points
- **THEN** the build's validator rejects it, naming the rule, and nothing is written

#### Scenario: A snag record without its material is rejected
- **WHEN** a snag record carries no `material`, or one that is not in `assets-src/physics_materials.json`
- **THEN** the build's validator rejects it, naming the record and the rule

#### Scenario: Unknown material id is rejected
- **WHEN** a shape or material slot names a physics material id that is not in `assets-src/physics_materials.json`
- **THEN** the build's validator rejects it

#### Scenario: A malformed element is rejected
- **WHEN** a micro element has a zero diameter, two equal points, an unknown kind or a parent index out of range, a feature names a shape that does not exist, or a `cantilever` record sits on a `timber` capsule
- **THEN** the build's validator rejects it, naming the record and the rule, and nothing is written

#### Scenario: Frame is checked by a permanent probe
- **WHEN** the EditMode tests run
- **THEN** a committed asymmetric probe mesh, exported through the same path as every asset, imports with its +X, +Y and +Z features at +X, +Y and +Z in Unity (bounds min at the origin, max at the probe's stated size), so a wrong sign in the frame conversion fails the suite

#### Scenario: Collision follows the visual
- **WHEN** K01's collision primitives are compared with its LOD0 mesh
- **THEN** every primitive surface lies within ±5 mm of the mesh surface it stands for (well inside AST-05's ±5 cm), and each primitive has the material id `masonry`

#### Scenario: Frame is Unity's
- **WHEN** the brick is generated lying flat with its long edge along Blender's X
- **THEN** in Unity the prefab's long edge lies along +X, its 65 mm height along +Y, and the metadata bounds match the imported mesh bounds within 1 mm

### Requirement: Visual-only micro-detail meshes
A generator MAY return, per LOD, meshes that are visual only (lying straws, stems, litter). The metadata SHALL list each such mesh under `micro` with its kind and count, the collision audit SHALL skip those meshes and name them in its lines, and the importer SHALL render them with their LOD without creating a collider for them. Where physics reads the micro-detail (`p7a-ground`: S04's top-layer straws, standing stems and twigs; a crown's twigs and leaves), the generator SHALL write the very elements it draws as `micro_elements`, so the drawn and the simulated elements are the same at 0 mm (AST-10); elements with no physical role (the mat's body straws, blades, litter) stay visual only.

#### Scenario: Micro meshes skipped by the audit
- **WHEN** a variant's LOD0 holds a micro mesh of 10 000 straws over a flat landable box
- **THEN** the audit passes the box against the ground mesh alone, prints the micro mesh's name and count as skipped, and the prefab's LOD0 renders both meshes

#### Scenario: Micro meshes carry no collision
- **WHEN** the prefab is built
- **THEN** no collider is created for a micro mesh and the physics metadata component lists it under `micro`

#### Scenario: Micro elements are the drawn ones
- **WHEN** each `micro_elements` record of a variant is compared with the strand or card it stands for in the baked mesh
- **THEN** every record's centre line lies on its strand's within 1e-6 m and its diameter equals the strand's, every drawn strand of a physical kind has exactly one record, and the audit prints the counts by kind

### Requirement: Physics material rows are per touching part
Every row of `assets-src/physics_materials.json` SHALL state its contact values for one touching part of the drone (a leg foot, an arm, the body or the coil), since the contact law applies the row to each part that touches it; a row's rationale SHALL name the part it was derived for and the sink or deflection it gives that part, with its label (published, derived or assumed). A row whose stiffness comes from a layer under a spread load (`straw`, `fabric`, `sandbag`) SHALL name its `area_law` (`mat`, `punch` or `winkler`) and its `area_reference_depth_m`, by which the contact law scales it to each part's footprint; every compliant row's stroke SHALL be capped by the shape's own thickness (`p7a-ground`); `bark` and `deadwood` SHALL carry an `elastic_modulus_pa` for their slender members' bending; and a row MAY carry a `wet` block of the values rain changes, each labelled.

#### Scenario: A soft row read per part
- **WHEN** the `fabric` row is read
- **THEN** its stiffness is the one a single 15 mm leg foot meets on a mattress, its rationale gives the sink one foot makes under its share of the drone's weight, and four feet together sink as the sheets that use it say

#### Scenario: An area-type row says its law
- **WHEN** `straw`, `fabric` and `sandbag` are read, and a copy of `straw` without `area_law`
- **THEN** each names its law and reference depth with a label and a rationale that derives the reference part's value, and the copy is refused naming the row and the missing field
