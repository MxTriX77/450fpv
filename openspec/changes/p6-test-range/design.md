# Design: p6-test-range

## Context
- **What exists in Unity** (`assets-src/catalog.json` on `unity/main`): S04 (grass patch), V01 (black locust, S/M/L and autumn), V13 (shrub clumps), R01 (VAZ, A intact and F burnt), L01 (poles), L03 (wires), E01 (the trench run), K01, K07. H01 (the adobe house, I and D) is on its branch, near merge. G1's branch holds `LookSetup`, the noclip camera, `ViewpointKeys` and the gallery's scene builder (`GalleryStations.cs`).
- **What exists in Godot** (`refs/godot/main` at `8340138`, read-only; its LFS objects are in the local store): the yard's models `house_adobe`, `shed_plank`, `cellar_head`, `cellar_shaft`, `gate_plank`, `fence_planks`, `tree_fruit`, and the belt's `tree_young`, `tree_maple`, `tree_acacia`, `tree_ash`, `tree_dead`, `shrub_belt`, as `.glb` exports of their Blender generators. `game/assets/catalog.json` gives each its collision (boxes, cylinders, capsules), gaps (doors, windows, the gate), wind volumes, porosity, snag flag and LOD distances. `tools/map/make_uat1_gallery.py` holds the yard's layout (`ADOBE_YARD`) and the five-pole line (`POLE_LINE`).
- **Frames:** Godot is right-handed, +X east, +Y up, +Z south. The Unity side follows D-019 with the Z flip where data enters (`p6-world-query-gallery` 1.1's frame note).

## Goals / Non-Goals
**Goals:** a flyable, tangible map at little cost, built from data by one command per piece, that the physics sees as it is drawn.
**Non-Goals:** the look of the stand-ins, relief, terrain layers, roads (proposal).

## Decisions
1. **Stand-in ids `Z01`–`Z13`.** No object sheet uses Z, so the ids cannot collide with a real asset:

   | Id | Godot asset | | Id | Godot asset |
   |---|---|---|---|---|
   | Z01 | `house_adobe` | | Z08 | `tree_young` |
   | Z02 | `shed_plank` | | Z09 | `tree_maple` |
   | Z03 | `cellar_head` | | Z10 | `tree_acacia` |
   | Z04 | `cellar_shaft` | | Z11 | `tree_ash` |
   | Z05 | `gate_plank` | | Z12 | `tree_dead` |
   | Z06 | `fence_planks` | | Z13 | `shrub_belt` |
   | Z07 | `tree_fruit` | | | |

   Each catalog entry carries `"stand_in": true` and its Godot source (path and commit). When a real asset replaces one, the map's data names the new id and the stand-in is dropped.
2. **One port generator, a params file per stand-in.** `tools/blender/generators/godot_port.py` (`STAND_IN`) reads the pinned Godot model, the LFS object of `refs/godot/main:<path>` at `8340138` (`lib/godot.py`: `git show` and the shared LFS store, SHA-256 checked), and reads the glb's buffers in plain Python (`lib/glb.py`), not through Blender's glTF importer. So the one frame change is Godot (x, y, z) → asset (x, y, −z), for the mesh and the metadata alike. The pipeline does the rest: the FBX export, the Unity prefab, the LOD group and the review sheet, exactly as for a generated asset. Its params file (`assets-src/standins/<id>_<key>.json`) names the Godot model, the commit, its LFS id, the catalog key, the LODs and the holes; `build_asset.ps1 Z0n` rebuilds it. **LODs:** the glbs hold Godot's own LOD meshes, so those are ported and nothing is decimated. The levels switch at Godot's `lod_switch_m` but its last; Godot's last level (a tree's far card, a structure's coarsest mesh) is not ported, so there are no impostors. Every stand-in culls at 600 m, past the longest sight line from the map's corners to its contents (530 m): culled at Godot's last switch (55–200 m), the stand-ins left holes in the view.
3. **Metadata from the Godot catalog.** The port writes each stand-in's `.assetmeta.json` from its catalog entry through the mesh's own mirror: collision shapes, gaps (a rectangle becomes a polygon, `narrowest_m` its smaller side), wind volumes (Godot's `wind_volume`, else its collision shapes; porosity 1 means none) and porosity; every shape is `landable: false`, since Godot has no such flag. The physics ids are Godot's own (`masonry`, `timber`, `surface:belt_bare`), each already in Unity's table, so nothing is mapped. Asset space has no field for Godot's per-object snag flag, so it goes into an optional `stand_in` record beside the source (`snag_hazard`), and `snag` stays empty. The audit (`lib.audit.stand_in`: each collision shape within the LOD0 mesh's bounds plus 5 cm, each gap's `narrowest_m` against its inscribed circle) and the review sheet's collision overlay prove the two agree.
4. **Render materials:** an existing Unity recipe where one fits (bark slots take `bark_locust`, brick takes `brick_red`); every other slot is a palette of the model's own colour (`godot_*`), since the glbs embed no textures. Nothing is third-party, so the manifest is unchanged.
5. **The cellar and the trench cut the floor.** The cellar shaft's opening and E01's run are holes in the floor in the scene and in the sim's ground, in the form the gallery uses for E01.
6. **The map is generated.** `tools/map/make_test_range.py` writes `maps/test-range/placements.json` from a seed (labelled), one row per instance (asset, variant, position in metres, yaw in degrees, in Unity's frame), plus the start points and the noclip start. It ports `ADOBE_YARD`'s relative layout with the Z flip, lays the posadka as rows with jittered spacing (trunks 1–3 m apart, WLD-04; the mix and the gap per the spec), and places the cars, the pole line, the trench and the grass. Its numbers (sizes, spacings, counts) are labelled assumed and set in one table at the top. The same seed gives the same bytes.
7. **The placement form is shared** (D-041). The scene builder reads `placements.json`, and `p6-world-query-gallery` 1.3 converts the gallery's station table into the same form, so one sim loader reads both maps. This change builds the form first; world-query 1.3 adopts it.
8. **The scene:** `Scenes/TestRange.unity`, written by an editor builder from `placements.json` (never hand-placed), reusing the gallery's prefab lookup, `LookSetup` and its presets, the noclip camera and the preset keys. Grass: S04 as the posadka's two verges, a strip of 12 m cells along each edge; the yard, the start points and the rest of the map stand on the floor's straw-and-soil texture, because clusters of patches read as tiles from the air (task 1.2). Every candidate cell along the verges is filled, and the frame rate holds.
9. **The build:** `Fpv450.Build.TestRange` writes `Builds/Windows-TestRange/450fpv-test-range.exe` with `-smoketest`, as `Fpv450.Build.Gallery` does. `docs/test-range.md` is the pilot's one page: how to run it, the keys, what is a stand-in.
10. **Branches:** task 1.1 on `unity/art/p6-test-range--standins` off `unity/main` (it touches only the asset pipeline's files and the new ids). Task 1.2 on `unity/world/p6-test-range` off G1's tip until G1 merges (it needs `LookSetup`, noclip and the gallery builder), merging 1.1's branch once 1.1 is done. H01 is placed when it is on the branch's base; otherwise a late-placement task adds it, as G1's 1.3 does.

## Risks / Trade-offs
- **The Godot models' looks failed parts of the first attempt.** They are stand-ins under D-040; the pilot asked for them, and the yard reads as a yard. Each is swapped for its Unity asset when that lands.
- **Hundreds of trees and grass instances cost frames.** The LODs, the cull distances and the grass density are tuned against the 1 % low on AC; the map shrinks the posadka before it drops below PLT-02.
- **Frame mistakes in the port** (a mirrored house, a door gap on the wrong wall) are caught by the audit, the overlay and the gap scenario, not by eye alone.

## Open Questions
- None for the pilot. The pilot's reading of the map comes at G4 (noclip through the feed) and G7 (flights).
