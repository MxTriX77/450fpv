# Blender → Godot asset pipeline

Every asset is built by a Python script and exported headless (asset-pipeline spec). Nothing is modelled by hand.

```
blender/<class>/<asset>.py     builder: draws the asset from measured sizes and saves <asset>.blend next to itself
blender/<class>/<asset>.blend  source (Git LFS), one mesh object per LOD: <asset>_LOD0, _LOD1, ...
game/assets/models/<class>/<asset>.glb   export (Git LFS), plus Godot's .glb.import sidecar
game/assets/catalog.json       the asset's physical description, checked against its builder's
```

## Commands

```powershell
python tools/blender/export.py                  # export every source, print the budget report; exit 1 on any problem
python tools/blender/export.py --build          # run every builder first, then export
python tools/blender/export.py --build fence_planks   # only the named assets
```

`export.py` restarts itself in Blender 5.2 (`-b --factory-startup`). Set `BLENDER` to use another `blender.exe`.

- **Reproducible.** The same `.blend` always exports the same `.glb` bytes, so re-exporting unchanged sources leaves `git status` clean. A `.blend` is not byte-stable (Blender stores memory addresses in it), so commit it only when its builder changed.
- **Budget report.** One line per asset: the triangles of each LOD against the class budget, the LOD count, and the pixel size of each texture the asset's materials use. `OVER` marks an excess, and the run exits 1.

| `budget_class` | LOD0 triangles | LOD levels | Textures |
|---|---|---|---|
| `vehicle` | ≤ 40 k | ≥ 3 | ≤ 2K |
| `house` | ≤ 60 k | ≥ 3 | ≤ 2K |
| `block` (the destroyed 5-storey block) | ≤ 200 k | ≥ 3 | ≤ 2K |
| `tree` | ≤ 30 k | ≥ 3, plus a far impostor as the last level | ≤ 2K |
| `prop` (small props) | ≤ 5 k | 1 | ≤ 1K |

## Writing a builder

A builder is a plain script, so it runs alone (`blender -b --factory-startup --python blender/<class>/<asset>.py`) or from `export.py --build`. It uses `tools/blender/assetkit.py`:

- `kit.reset()` starts from an empty factory scene, so the build depends on nothing but the script.
- `kit.Mesh()` holds one LOD. `mesh.box(center, size, material, rotation_deg)` draws a box and returns it as a catalog collision shape. UVs are in metres, with the box's long side along V.
- `kit.finish(__file__, budget_class, [lod0, lod1, ...], materials, entry)` makes the LOD objects, stores the budget class and the catalog entry in the `.blend`, and saves it.

Rules:

- **Asset space** is the catalog's: metres, +X east, +Y up, +Z south. The origin is on the ground at the footprint centre. Rotations use the catalog's `rotation_deg` (Godot's YXZ). The kit converts to Blender's Z-up frame, and the glTF exporter converts back.
- **Dimensions** come from the reference notes (`docs/reference-notes/terrain.md`), named as constants at the top of the builder, with the note's id (B1, R5, ...).
- **Randomness** comes from `random.Random(SEED)` with a fixed seed. Variants are new seeds or parameters, never hand edits.
- **LODs.** A level the class needs but the builder doesn't draw is made at export from LOD0 by decimation (LOD1 50 %, LOD2 20 %). A tree's impostor is always drawn by its builder.
- **Materials** are named by what they are (`wood_weathered`, `brick_silicate`), with a flat albedo at photo-like value from the notes and a roughness. The `.glb` carries no images. Textures are bound in Godot by `game/assets/materials/<name>.tres`, one per material name, from the shared CC0 sets in `game/assets/textures/` (`tools/assets/fetch_cc0.py`). The report reads each `.tres` for its texture sizes. A shared set counts against the library's 2K cap, not the class's, because it is loaded once for every asset that uses it.

## Catalog-entry conventions

The builder produces the entry (`game/maps/README.md` has the field reference), and `export.py` fails until `catalog.json` holds exactly that entry. When it differs, the error prints the builder's entry in catalog layout, ready to paste.

- **Collision: primitives that follow the solid parts.** Draw solid parts as boxes, and use each returned shape. Then collision is the visual, within the rounding of 0.1 mm. Add `sphere`, `cylinder` or `capsule` shapes by hand only for round parts (trunks, poles, wheels), sized to the mesh. Leave out only thin trim, glass and parts under 1 cm, and list each omission in the builder's docstring as a deliberate simplification (spec: collision follows the visual within 0.15 m).
- **Material:** the asset's main contact material from the catalog's `materials` table (`timber`, `masonry`, `steel`, `sheet_metal`, `cable`). A shape of another material carries its own `material`, such as a steel gate in a masonry wall. Add a new material to the table only with the physics engineer.
- **Wind volume:** leave it out when the collision shapes are the solid silhouette (a closed house). Give one when the wind sees more than the collision: a tree crown, a fence bay. It is a few boxes or spheres around the silhouette. `wind_porosity` is the open fraction of the side-on silhouette, computed by the builder on a 1 cm grid, or estimated for crowns (0.4–0.6 in leaf, 0.7–0.8 bare).
- **Gaps:** every opening the drone can fly through, meaning at least 0.6 m clear both ways (the drone is about 0.57–0.65 m tip to tip): doors, window holes, a cellar mouth, broken roof spans, open rooms of the destroyed block. The builder computes centre, width and height from the same constants that cut the opening, so they match the mesh (spec: within 5 cm). The opening faces asset ±Z turned by `yaw_deg`. Narrower openings (fence slots, batten spacing) are not gaps.
- **Snag:** `snag_hazard: true` when the fiber or the legs can catch: post tops, board gaps, battens and rafters, rebar, torn metal, branches, wires. `false` only for smooth closed volumes.
