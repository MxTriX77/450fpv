# Spec Delta

## Purpose

The asset pipeline's optional metadata for drawing a far layer and for parts the player rewrites: cross-faded LOD switches and generated base and mask maps per slot (D-081, S04's straw at a distance), and readable LOD parts (p7d-tether 1.14, D01's coil pack).

## ADDED Requirements

### Requirement: Cross-faded LOD switches
A generator MAY return `lod_fade_share`, a number in [0, 1), written to the metadata after the schema's keys. With it, each mesh LOD SHALL dissolve into the next from (1 − share) of the next LOD's switch distance to the switch itself, and the importer SHALL set the LOD group to Unity's dithered cross-fade over those bands; the cull SHALL never be faded. Without it every switch SHALL stay instant and the LOD group's fade mode none, so every other asset's prefab is unchanged. The validator SHALL refuse a share outside [0, 1), and the importer SHALL refuse it too.

#### Scenario: S04's switches cross-fade over their last quarter
- **WHEN** `S04_patch` is imported with `lod_fade_share` 0.25 and its switches at 8 m and 60 m
- **THEN** its LOD group cross-fades by distance, never by time: LOD0 into LOD1 from 6 m to 8 m and LOD1 into LOD2 from 45 m to 60 m, each band read back within 1 cm from the heights and widths Unity draws with, and the cull at 400 m is not faded

#### Scenario: A share outside [0, 1) is refused
- **WHEN** a metadata file carries a `lod_fade_share` of 1, below 0 or not a number
- **THEN** the build's validator rejects it, naming the rule, and nothing is written

### Requirement: Generated base and mask maps per slot
A generator MAY write, under `textures`, a slot's albedo with role `base_map` and its packed mask (metallic R, occlusion G, smoothness A, linear) with role `mask_map`, each laid over the slot's UVs from its `uv_min` to its `uv_max`. The importer SHALL draw that slot with its recipe's material, every property, keyword and queue kept, over the base map, and over the mask map in place of the recipe's mask when there is one, as `<variant>_<slot>_base.mat` beside the prefab; a material left from a build in which the slot had its base map SHALL be deleted. The importer SHALL refuse a mask map without a base map on its slot, a mask map over other UVs than its base map, and a base map on a slot that also has a crack or damage mask. Nothing drawn from these maps SHALL be read by physics.

#### Scenario: S04's slab draws the near map at LOD0 and the far map beyond
- **WHEN** `S04_patch` is built and imported
- **THEN** its metadata lists `base_map` and `mask_map` for the `ground` slot (the near maps) and the `far` slot (the far maps) over the patch's square, its prefab draws LOD0's slab with `S04_patch_ground_base.mat` and LOD1's and LOD2's in the `far` slot with `S04_patch_far_base.mat`, each `belt_straw`'s material with only its base map and packed mask swapped, laid once over the patch's 12 m, with URP's `_MainTex` on the same map, and the physics metadata holds no shape, gap or material from the far layer

#### Scenario: A rebuild leaves a base-map material unchanged
- **WHEN** `S04_patch` is imported again over its existing `S04_patch_ground_base.mat` and `S04_patch_far_base.mat`
- **THEN** both come out byte-identical to the first import's, `_MainTex` included

#### Scenario: A mask map without its base map is refused
- **WHEN** a metadata file lists a `mask_map` for a slot that has no `base_map`, or over other UVs than its base map
- **THEN** the importer refuses it, naming the slot

### Requirement: Readable LOD parts
A generator MAY return, per mesh LOD, parts the player rewrites at run time (D01's coil pack, which the fiber's CoilPack shrinks as the line pays out). The metadata SHALL list each under `readable` with its LOD, its kind, its name `<variant>_LOD<n>_<kind>` and its triangles, written only when there are some and after the schema's keys; the part SHALL widen the bounds and be audited, and impostor-rendered, with its LOD as drawn, and the build SHALL export it as its own object beside its LOD. The importer SHALL draw it with its LOD from a readable copy saved beside the prefab as `<name>.asset`, rewritten only when the mesh changes, so in the player only these parts keep a CPU copy and the rest of the model's import stays unreadable. The validator SHALL refuse an entry that names no mesh LOD or an impostor, lacks a key, has no triangles, or is named otherwise or as another mesh.

#### Scenario: D01's coil pack is the only readable mesh
- **WHEN** `D01_legs_on` and `D01_legs_off` are built and imported
- **THEN** each metadata lists one `coil_pack` part per LOD (LOD0 to LOD3), each prefab draws `<variant>_LOD<n>_coil_pack` from its `.asset` beside the prefab, those meshes are readable and no other mesh of the model is, and a rebuild leaves every file byte-identical

#### Scenario: A bad readable entry is refused
- **WHEN** a generator returns a readable part for a LOD that does not exist, or the metadata's entry lacks a key, has zero triangles or is named as another mesh
- **THEN** the build refuses it, naming the entry, and nothing is written
