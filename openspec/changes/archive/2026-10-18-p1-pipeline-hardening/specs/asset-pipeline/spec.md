# Spec Delta: asset-pipeline

## ADDED Requirements

### Requirement: Parameter sanity
Each generator SHALL declare a range per parameter, and the build SHALL refuse a params file with a value outside its range or a key the generator does not know, before anything is exported.

#### Scenario: Out-of-range bevel refused
- **WHEN** K01's `bevel_mm` is set to 200
- **THEN** the build exits non-zero naming the parameter and its range, and the FBX and metadata on disk are unchanged

### Requirement: Collision audit at build time
The build SHALL measure, for every collision primitive, the two-sided deviation from the LOD0 surface it stands for, and fail beyond ±5 cm, or ±2 cm within 0.3 m of a gap polygon or a landable face; it SHALL also check that `narrowest_m` equals the largest inscribed circle of each gap polygon within 5 mm.

#### Scenario: Collision drift is caught
- **WHEN** a generator's mesh grows 6 cm beyond its declared box
- **THEN** the build exits non-zero, printing the primitive, the deviation and the tolerance

### Requirement: Derived textures by script
Derived textures (packed masks, tints, facade maps, damage masks) SHALL be produced by a script from the pinned sources and a recipe stored with the material; the build SHALL regenerate them when the recipe or a source changes, never by hand.

#### Scenario: Packed mask for a material
- **WHEN** a material recipe names a roughness and an AO source
- **THEN** the build writes `<material>_mask.png` (metallic R, AO G, smoothness A = 1 − roughness) and the URP/Lit material uses it, with the recipe recorded beside it

### Requirement: Cylinder primitives import
A `cylinder` collision primitive SHALL import as a convex mesh collider of the same radius and height, for noclip use only.

#### Scenario: Cylinder in metadata
- **WHEN** a variant's metadata holds a cylinder primitive
- **THEN** the importer creates a convex MeshCollider matching it within 5 mm, and the prefab builds without error

### Requirement: Surface materials on shapes and the material list grows only with sign-off
A collision shape or material slot MAY name a ground surface as its physics material with the form `surface:<surface-id>`, where `<surface-id>` is one of the thirteen surface ids S01–S13's names (meadow_sod, dry_crust, crater_spoil, belt_straw, belt_bare, tilled, burnt_field, weeds, yard_litter, rubble, asphalt, concrete_slab, dirt_track); the validator SHALL accept exactly those. New plain ids SHALL enter `assets-src/physics_materials.json` only through the physics engineer, each row carrying labelled values.

#### Scenario: Surface material accepted
- **WHEN** a trench filler shape names `surface:belt_bare`
- **THEN** the validator accepts it, and `surface:nonsense` is rejected naming the rule

#### Scenario: Proposed ids signed off
- **WHEN** the object sheets propose ids (rubber, fabric, plastic, asphalt, asbestos_cement, soil, straw, bark, twig, deadwood, forb_stem, glass_crumbs, ceramic, …)
- **THEN** each is either added to the list with labelled values and a rationale, mapped to an existing id, or declined with a reason, in a physics review recorded in the change folder
