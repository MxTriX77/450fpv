# asset-pipeline Specification

## Purpose
Defines how every asset goes from editable sources (params plus pinned CC0 files) to a Unity prefab with physics metadata, in one command, with nothing edited by hand.

## Requirements

### Requirement: Pinned third-party files
Every third-party file SHALL be listed in `tools/assets/manifest.json` with source page URL, download URL, licence, author, resolution, byte size and SHA-256, and SHALL be fetched only by `tools/assets/fetch.py`. Only CC0 and CC-BY licences SHALL be accepted.

#### Scenario: Verify passes on a clean checkout
- **WHEN** `python tools/assets/fetch.py --verify` runs after a fetch
- **THEN** every manifest file exists with a matching SHA-256, no unlisted file exists under the third-party folder (Unity `.meta` files excepted), `CREDITS.md` matches what the manifest generates, and the exit code is 0

#### Scenario: Tampered file fails verify
- **WHEN** one byte of a fetched file changes
- **THEN** `--verify` exits non-zero naming that file

#### Scenario: Non-free licence is refused
- **WHEN** a manifest entry declares any licence other than CC0 or CC-BY
- **THEN** fetch and verify both exit non-zero

### Requirement: Build from params, headless
`tools/blender/build.py` SHALL build an asset id's variants from `assets-src/<class>/<variant>.json` in headless Blender, writing an FBX and an asset metadata JSON per variant, with no interactive step and no hand edits.

#### Scenario: K01 builds headless
- **WHEN** Blender runs `build.py` for `K01` in background mode
- **THEN** it writes the K01 FBX and its metadata JSON and exits 0

#### Scenario: Brick dimensions are exact
- **WHEN** the built K01 whole-brick mesh is measured
- **THEN** its bounds are 0.250 × 0.120 × 0.065 m within ±1 mm (AST-04)

#### Scenario: Rebuild after a params edit
- **WHEN** one dimension in K01's params changes and the asset is rebuilt
- **THEN** the FBX, the metadata and the Unity prefab all reflect the new value with no other step (AST-03)

### Requirement: Asset metadata contract
The metadata JSON SHALL state its schema version and units, and SHALL hold, in Unity's frame (D-019) and in metres: bounds; LODs with triangle counts and switch distances, and a cull distance; material slots, each with a visual material and a physics material id; collision primitives (box, sphere, capsule, cylinder), each with a material id and a landable flag; gaps as planar polygons with their narrowest width; wind volumes with per-part porosity; snag-hazard features.

The conventions SHALL be fixed and validated at build time: shapes are convex; `rotation` is a unit quaternion `[x, y, z, w]` applied about the shape's `center`; capsules and cylinders run along local +Y before rotation, with `height` the total length including caps (a capsule needs `height ≥ 2·radius`); a gap `polygon` is the throat of the opening, planar within 10 mm, simple, concave allowed, with a unit `normal` of free sign and meaningless winding, and `narrowest_m` the diameter of its largest inscribed circle; empty `wind_volumes` means the asset blocks no wind; `snag[].kind` is `edge` or `hook`, each with at least two points; `landable` is informational and never the contact law. Every physics material id SHALL come from the closed, physics-owned list in `assets-src/physics_materials.json`.

#### Scenario: Convention violations are rejected
- **WHEN** a metadata file carries a non-unit quaternion, a capsule with `height < 2·radius`, a gap polygon that is not planar within 10 mm or whose `normal` is not perpendicular to it, a `narrowest_m` outside `(0, polygon extent]`, or a snag of unknown kind or with fewer than two points
- **THEN** the build's validator rejects it, naming the rule, and nothing is written

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

### Requirement: Import to a prefab
The catalog importer SHALL create or update, without hand steps, a prefab per variant with: one LOD group using the asset's own LODs (Unity's automatic mesh LOD off); URP materials built from the catalog; colliders from the metadata primitives; and a component carrying the full physics metadata.

#### Scenario: K01 prefab is complete
- **WHEN** the importer runs for `K01` in Unity batchmode
- **THEN** the K01 prefab exists with a LOD group, a box collider matching the metadata, a URP material using the pinned brick texture set, and the physics-metadata component holding the `masonry` material id

### Requirement: One command end to end
`pwsh tools/build_asset.ps1 <ID>` SHALL run the Blender build and the Unity import for that id, and exit non-zero if any step fails.

#### Scenario: K01 end to end
- **WHEN** `pwsh tools/build_asset.ps1 K01` runs with no Unity editor holding the project open
- **THEN** it exits 0 and the FBX, metadata and prefab are all up to date

#### Scenario: A failing step fails the command
- **WHEN** Blender or Unity exits non-zero during the run
- **THEN** the script stops at that step and exits non-zero, naming the step

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

### Requirement: Alpha-clipped two-sided recipes
A material recipe MAY declare `alpha_clip` (a cutoff in 0–1) and `two_sided: true`; the importer SHALL build a lit material that clips by the diffuse alpha at that cutoff and renders both faces, a back face lit with its own (flipped) normal, and the texture import SHALL keep the alpha channel and preserve the alpha coverage at the cutoff in every mip level.

#### Scenario: Leaf card material
- **WHEN** a recipe names a LeafSet with `alpha_clip` 0.5 and `two_sided` true
- **THEN** the imported material has alpha clipping on at 0.5 and culls no face, and a leaf card renders its silhouette from both sides in the review sheet

#### Scenario: Back faces lit as leaves
- **WHEN** a two-sided leaf card is seen from its back under the day light of the review rig
- **THEN** each sRGB channel of its mean over the opaque pixels is within 15 % of the same card seen from its front with the sun behind the camera, so the back reads green like the front; a back face lit with the front's normal (URP/Lit with cull off) fails it with its grey sheen

#### Scenario: Coverage kept at distance
- **WHEN** the clipped base map is imported
- **THEN** its importer preserves mip coverage at the recipe's cutoff, and every mip down to 4 px keeps opaque texels after clipping (without preservation the 4 px mip has none)

### Requirement: Compliant shells in the collision audit
A collision shape whose material is a crown contact material (`twig` or `foliage`) is a shell. The audit SHALL require (a) every point of the surface of the union of the shell pieces above the ground, not inside another piece, to lie within the envelope tolerance (0.1 m, derived from the drone's push against the `twig` contact stiffness) of the nearest face of the shell's slots; (b) every LOD0 vertex of a geometry slot (an opaque recipe, such as twig tubes) to lie inside the union or within 0.1 m outside it; and (c) every LOD0 vertex of a card slot (an alpha-clipped recipe, such as leaf cards) to lie inside the union or within 0.3 m outside it (one sprig length plus the tolerance: cards have no contact role, their twigs carry snag records). It SHALL print each piece's largest standoff and where it is, and per slot the largest outside distance, and fail beyond each tolerance naming the piece or slot, the distance and the tolerance.

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
- **THEN** the audit fails rule (c) naming the slot, the distance and the 0.3 m allowance
