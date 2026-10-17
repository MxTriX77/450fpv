# Spec Delta: asset-pipeline

## Purpose
Defines how every asset goes from editable sources (params plus pinned CC0 files) to a Unity prefab with physics metadata, in one command, with nothing edited by hand.

## ADDED Requirements

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
The metadata JSON SHALL state its schema version and units, and SHALL hold, in Unity's frame (D-019) and in metres: bounds; LODs with triangle counts and switch distances; material slots, each with a visual material and a physics material id; collision primitives (box, sphere, capsule, cylinder), each with a material id and a landable flag; gaps as planar polygons with their narrowest width; wind volumes with per-part porosity; snag-hazard features.

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
