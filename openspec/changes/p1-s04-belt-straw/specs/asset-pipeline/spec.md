# Spec Delta

## ADDED Requirements

### Requirement: Ground recipes with a detail layer
A material recipe MAY be of kind `ground`: it names a soil set and a cover set from the manifest, a seeded patch description (bare patches, regrowth strips, a lodging direction) and a target mean albedo, and the build SHALL derive from them one unique albedo, normal and packed mask for the asset (2K at most), tinted so the mean albedo matches the target, plus a URP/Lit detail layer (albedo and normal of the cover set) tiled at the set's metre size. The derivation is rerun when the recipe changes, and the derived files are never hand-edited.

#### Scenario: Ground recipe derived and wired
- **WHEN** an asset's material recipe is a `ground` recipe
- **THEN** the build writes `<material>_albedo.png`, `<material>_normal.png` and `<material>_mask.png` at 2K or less, with the recipe recorded beside them, and the imported URP/Lit material uses them as its base maps and the cover set as its detail maps at the recipe's tile size

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

### Requirement: Third-party sources are pinnable by URL
Every third-party file SHALL enter the manifest through `fetch.py --add` with a stable download URL and a SHA-256, from a source the fetcher knows (`polyhaven`, `ambientcg`, and `texturecan` once it is added). A source whose files cannot be fetched by a stable URL SHALL NOT be used; the sheet's material line then names the substitute set and why.

#### Scenario: A set from a new source
- **WHEN** `fetch.py --add texturecan:<id> --res 2k` runs
- **THEN** the files download from `texturecan.com`, are hashed and pinned with licence CC0, and `fetch.py --verify` exits 0 afterwards

#### Scenario: A source that cannot be pinned
- **WHEN** a set's download needs a browser session or an unstable URL
- **THEN** the set is not used, and the object sheet's material line records the CC0 substitute from a pinnable source
