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

### Requirement: Ground recipes with a detail layer
A material recipe MAY be of kind `ground`: it names a soil set and a cover set from the manifest, its lodging direction and tints, the bare patches, the regrowth strips and the tussock relief taken from the asset's params through the seeded field definitions the generator also uses (so the map's soil, strips and hollows and the mesh's floors, blades and humps never disagree), and a target mean albedo, and the build SHALL derive from them one unique albedo, normal and packed mask for the asset (2K at most), tinted so the mean albedo matches the target, plus a URP/Lit detail layer (albedo and normal of the cover set) tiled at the set's metre size. The derivation is rerun when the recipe, the sources, the asset's patch params or any constant of the formula changes, and the derived files are never hand-edited.

#### Scenario: Ground recipe derived and wired
- **WHEN** an asset's material recipe is a `ground` recipe
- **THEN** the build writes `<material>_albedo.png`, `<material>_normal.png` and `<material>_mask.png` at 2K or less, with the recipe recorded beside them, and the imported URP/Lit material uses them as its base maps and the cover set as its detail maps at the recipe's tile size

#### Scenario: A formula constant changes
- **WHEN** one of the ground formula's constants (a straw length, a lobe size, a feather width, a tolerance) changes in `derive.py`
- **THEN** the next `derive.py` run re-derives the maps instead of reporting them up to date, because the stamp hashes every constant's value with the recipe and the sources

#### Scenario: Mean albedo hits the target
- **WHEN** the derived albedo is measured over its whole area
- **THEN** its mean luminance lies within ±5 % of the recipe's target, and the mask's patches cover the recipe's shares within ±2 percentage points

### Requirement: Visual-only micro-detail meshes
A generator MAY return, per LOD, meshes that are visual only (lying straws, stems, litter: micro-detail that physics draws from the shared generator later, AST-10). The metadata SHALL list each such mesh under `micro` with its kind and count, the collision audit SHALL skip those meshes and name them in its lines, and the importer SHALL render them with their LOD without creating a collider for them.

#### Scenario: Micro meshes skipped by the audit
- **WHEN** a variant's LOD0 holds a micro mesh of 10 000 straws over a flat landable box
- **THEN** the audit passes the box against the ground mesh alone, prints the micro mesh's name and count as skipped, and the prefab's LOD0 renders both meshes

#### Scenario: Micro meshes carry no collision
- **WHEN** the prefab is built
- **THEN** no collider is created for a micro mesh and the physics metadata component lists it under `micro`

### Requirement: Flat palette recipes
A material recipe MAY be of kind `palette`: a short list of named colours, each a single sRGB value or a gradient of sRGB values; the build SHALL derive one small map with a column per colour (a gradient along the column, the columns padded to a power of two; recorded beside it like every derived texture), and a generator SHALL UV its faces into the column of their colour, along it for a gradient, so that a flat-tinted or shaded material still has a real base map.

#### Scenario: Straw palette
- **WHEN** the `straw` recipe names five gradients (straw pale, mid and dark around #cdb685, #b49c6a and #977f55, twig, green blade), each base to tip
- **THEN** the build writes `straw_albedo.png` with those five gradient columns padded to eight, the importer binds it as the base map, and the review capture's empty-stage guard passes

### Requirement: The collision audit measures the union of the primitives
The collision audit SHALL treat an asset's primitives as one union: the part of a primitive that lies inside another rigid (non-shell) primitive is not measured against the mesh; a crown shell piece (`twig`, `foliage`) hides nothing, so a rigid stem inside the compliant crown is still measured. A landable shape whose top is soft SHALL be held at 5 cm over its whole top as a landing face: its material is `surface:belt_straw`, `surface:crater_spoil` or `surface:tilled` (the surface rows where the reference foot sinks at least 25 mm), or a plain id with `stiffness_n_per_m` of 1000 N/m or less. Every other landing face, and every opening, keeps AST-05's 2 cm near-zone (`review-physics.md` §2: on hard ground a 5 cm float would show).

#### Scenario: Mound spheres sunk in the mat box
- **WHEN** three `surface:crater_spoil` spheres are sunk into a `surface:belt_straw` box over a mat with ±4 cm relief
- **THEN** the audit measures only the spheres' exposed caps and the box's exposed top, holds both at 5 cm, prints a line naming the soft shapes held at 5 cm, and passes

#### Scenario: A hard top keeps 2 cm
- **WHEN** a landable `surface:asphalt` box lies under a mesh with ±4 cm relief, and a landable `straw` box under the same relief
- **THEN** the asphalt box fails the 2 cm near-zone naming the shape, and the straw box passes at 5 cm

#### Scenario: A shell piece hides no rigid primitive
- **WHEN** a `bark` capsule's top stands 70 mm proud of its stem's tip, inside a `twig` shell sphere
- **THEN** the audit still measures it and fails it at 70 mm against the 50 mm tolerance, naming the capsule (`test_a_shell_piece_hides_no_rigid_primitive`)

### Requirement: Third-party sources are pinnable by URL
Every third-party file SHALL enter the manifest through `fetch.py --add` with a stable download URL and a SHA-256, from a source the fetcher knows (`polyhaven`, `ambientcg`, and `texturecan` once it is added). A source whose files cannot be fetched by a stable URL SHALL NOT be used; the sheet's material line then names the substitute set and why.

#### Scenario: A set from a new source
- **WHEN** `fetch.py --add texturecan:<id> --res 2k` runs
- **THEN** the files download from `texturecan.com`, are hashed and pinned with licence CC0, and `fetch.py --verify` exits 0 afterwards

#### Scenario: A source that cannot be pinned
- **WHEN** a set's download needs a browser session or an unstable URL
- **THEN** the set is not used, and the object sheet's material line records the CC0 substitute from a pinnable source

### Requirement: Physics material rows are per touching part
Every row of `assets-src/physics_materials.json` SHALL state its contact values for one touching part of the drone (a leg foot, an arm, the body or the coil), since the contact law applies the row to each part that touches it; a row's rationale SHALL name the part it was derived for and the sink or deflection it gives that part, with its label (published, derived or assumed).

#### Scenario: A soft row read per part
- **WHEN** the `fabric` row is read
- **THEN** its stiffness is the one a single 15 mm leg foot meets on a mattress, its rationale gives the sink one foot makes under its share of the drone's weight, and four feet together sink as the sheets that use it say

### Requirement: Ports and wires in the metadata
The metadata schema MAY carry `ports` (named attachment points: id, position, direction, and `max_diameter_m`, the largest conductor the fitting holds) that a generator writes for other generators and the map to use, and `wires` (per conductor or bundle: `slot`, an ordered point list, `diameter_m`, `mass_kg_per_m`, `sag_m`, `axial_stiffness_n` (EA) and `breaking_force_n`, both positive, `material`, `damage`) for conductors that are map objects rather than collision shapes. Both SHALL be written as empty arrays when absent, the validator SHALL check their fields, and the physics-metadata component SHALL expose them. A snag record MAY carry `edge_radius_m`, the arris the fiber bends over where it is smaller than the record's reach (`radius_m`): positive and at most `radius_m` (the strand of a stranded conductor, a barb's blade; task 1.5's reading).

#### Scenario: Ports written and read
- **WHEN** a pole variant with four hooks is built
- **THEN** its metadata lists four `ports` at the insulators' positions with outward directions, and the prefab's metadata component exposes them

#### Scenario: Wires validated
- **WHEN** a `wires` record names a material other than `cable`, or a point list shorter than two points
- **THEN** the validator rejects the metadata naming the record and the rule

### Requirement: A planted asset's buried surface is not audited
A generator MAY declare its asset planted (`PLANTED`): its members stand in the ground, which covers everything below y = 0. For such an asset the audit SHALL treat every point below the ground plane as covered: a primitive's surface below y = 0 is not measured against the mesh, and no mesh is drawn there, since the drone can never touch it; everything at or above the ground is audited as before.

#### Scenario: A buried capsule end on a planted pole
- **WHEN** a planted log's lowest capsule ends 0.113 m below the ground, 0.163 m from the open log mesh
- **THEN** the audit passes it (the buried end is not measured), and the same capsule on an asset not declared planted fails, naming the end and its distance

### Requirement: Wires are audited against their polyline
For an asset with `wires`, the collision audit SHALL check that every LOD0 vertex of a wire's tube lies within min(5 mm, half the diameter) plus the wire's radius of its point list (so a drawn thin wire always overlaps its physical one) and that its farthest vertex reaches the wire's radius within the same tolerance (so no record is wider than the wire drawn by more than that tolerance), SHALL exclude those vertices from the primitive audit, and SHALL print each wire with the largest deviation it found and how far its tube reaches; the importer SHALL render the tube with the thin-wire material and SHALL create no collider for a wire.

#### Scenario: Tube follows the polyline
- **WHEN** a 40 m conductor of 9 mm is audited
- **THEN** the audit passes it with a largest deviation under 5 mm and prints it, and the primitive audit reports no vertex of the tube

#### Scenario: Tube off its polyline
- **WHEN** a tube vertex lies 20 mm from the point list
- **THEN** the audit fails naming the wire, the deviation and the tolerance

#### Scenario: Record wider than its tube
- **WHEN** the record of a 9 mm tube gives a 20 mm diameter
- **THEN** the audit fails naming the wire, how far its tube reaches (4.5 mm of the record's 10 mm radius) and the tolerance

### Requirement: Thin-wire LODs are named in the metadata
Every LOD entry of every asset's metadata SHALL carry `thin_wire`, set by the generator (false unless it names the LOD): the importer SHALL draw every slot of a `thin_wire` LOD, and no other, with the thin-wire material, its radius read from uv2, never choosing that material from a second UV (also Unity's lightmap channel), and SHALL draw a wire's tube only in a `thin_wire` LOD.

#### Scenario: The flag, not uv2, picks the thin-wire material
- **WHEN** a span whose tubes carry their radius in uv2 is imported with its LODs not flagged `thin_wire`
- **THEN** without its `wires` every slot draws with the recipe's own material, and with them the import fails naming the wire's slot

### Requirement: Holes in the metadata
The metadata schema MAY carry `holes`: for each cut an ordered centre-line point list, `top_width_m`, `depth_m` and `band_width_m`, so the map compiler can mark the hole cells and raise or lower the heightfield without re-reading the mesh. The array SHALL be written empty when the asset cuts nothing, the validator SHALL check its fields, and the physics-metadata component SHALL expose it.

#### Scenario: A trench writes its hole
- **WHEN** a trench variant of 24 m is built
- **THEN** its metadata holds a `holes` record of the run whose centre line has at least one point per metre and whose band (every point within half its width of the centre line in plan) holds the cavity's widest outline, the crumbled lip included, 0.65 m inside its edge, one record per niche holding the niche the same way, and the prefab's metadata component exposes them

#### Scenario: Hole validated
- **WHEN** a `holes` record has fewer than two points or a band narrower than its top width
- **THEN** the validator rejects the metadata naming the record and the rule

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
