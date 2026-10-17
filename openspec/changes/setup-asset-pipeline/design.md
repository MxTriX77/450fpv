# Design: setup-asset-pipeline

## Context
Measured on the dev machine: Blender 5.2 LTS at `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` (not on PATH), Python 3.14.6, Unity 6000.3.26f1 (see `docs/environment.md` for the exact paths and the `UNITY` variable). The first attempt's fetcher and Blender kits are on the `godot-final` tag (`tools/assets/fetch_cc0.py`, `tools/blender/*.py`) and may be read for reference with `git show godot-final:<path>`.

## Decisions
1. **Locations.**
   | What | Where |
   |---|---|
   | params, one file per variant | `assets-src/<class>/<variant>.json` (K01 whole: `assets-src/debris/K01_whole.json`) |
   | catalog (asset id → class, variants, generator, materials) | `assets-src/catalog.json` |
   | visual material recipes | `assets-src/materials/<name>.json` (shader, texture-set id, tiling in metres, tint) |
   | generators | `tools/blender/generators/<generator>.py`, shared helpers in `tools/blender/lib/` |
   | third-party files (committed via LFS) | `unity/Assets/_Project/Art/ThirdParty/<source>/<id>/` |
   | exports and metadata | `unity/Assets/_Project/Art/Models/<class>/<ID>/<variant>.fbx` and `<variant>.assetmeta.json` |
   | prefabs | `unity/Assets/_Project/Prefabs/<class>/<variant>.prefab` |
   | the physics-metadata component | `unity/Assets/_Project/Scripts/World/AssetPhysicsMeta.cs` (world-builder takes ownership later) |
   The metadata file is called `.assetmeta.json`, never `.meta.json`, so nobody confuses it with Unity's own `.meta` files.
2. **Third-party files are committed through LFS**, matching `.gitattributes` and the first attempt (old D-005), so a clone opens in Unity without a fetch. `fetch.py --verify` proves they match the manifest. LFS headroom: 10 GiB storage and 10 GiB bandwidth a month on GitHub Free (published), about 0.28 GiB used by the Godot attempt (measured at the push).
3. **fetch.py.** `fetch.py <id>…`, `--all`, `--verify`, `--credits` and `--add <source>:<id> --res <1k|2k|4k>` (resolves URLs through the Poly Haven API or the ambientCG API, downloads, computes SHA-256, writes the entry). It sends a descriptive User-Agent (Poly Haven asks for one). ambientCG zips are pinned by the zip's SHA-256 plus each extracted file's. `CREDITS.md` at the repo root is generated, never edited. K01 pins **one** brick texture set at 1k, chosen by the asset-artist from Poly Haven or ambientCG (CC0).
4. **build.py** runs as `blender --background --factory-startup --python tools/blender/build.py -- <ID|all>`. It refuses a Blender major.minor other than the one pinned in `docs/environment.md`, so exports are reproducible. Each generator is a pure function of its params (with an explicit `seed` where randomness is used) that returns meshes per LOD plus the metadata. build.py does the export and the frame conversion.
5. **Frame conversion happens once, in build.py.** Blender is right-handed Z-up; Unity is left-handed Y-up (D-019). The FBX export uses Unity-friendly axes and applied transforms. The metadata is written **already converted** to Unity's frame, and a check compares the metadata bounds with the exported mesh bounds after conversion. Consumers (importer, world builder, physics) never convert again. **Task 1.2 implemented this with one stated assumption:** Unity's FBX importer negates X when taking the right-handed file into its left-handed frame (so Blender (x, y, z) → Unity (−x, z, −y)). K01 is symmetric in X and Z and cannot expose a wrong sign, so task 1.3 checks it with an asymmetric throwaway mesh; a wrong sign is a one-line fix in `tools/blender/lib/frame.py`.
6. **Metadata schema v1** (`<variant>.assetmeta.json`). Numbers are metres, positions relative to the prefab origin, rotations quaternions `[x, y, z, w]`:
   ```json
   { "schema": 1, "id": "K01", "variant": "K01_whole", "generator": "debris_brick",
     "params_sha256": "…", "units": "m", "frame": "unity-lh-yup",
     "bounds": { "min": [..], "max": [..] },
     "lods": [ { "mesh": "K01_whole_LOD0", "tris": 0, "switch_distance_m": 0.0 } ],
     "cull_distance_m": 0.0,
     "materials": [ { "slot": "brick", "visual": "brick_red", "physics": "masonry" } ],
     "collision": [ { "shape": "box", "center": [..], "size": [..], "rotation": [0,0,0,1],
                      "material": "masonry", "landable": false } ],
     "gaps": [ { "id": "…", "polygon": [[..],[..],[..]], "normal": [..], "narrowest_m": 0.0 } ],
     "wind_volumes": [ { "shape": "box", "center": [..], "size": [..], "rotation": [..], "porosity": 0.0 } ],
     "snag": [ { "kind": "edge", "points": [[..],[..]], "radius_m": 0.0 } ] }
   ```
   Shapes: `box` (center, size), `sphere` (center, radius), `capsule` and `cylinder` (center, radius, height, axis rotation). Empty arrays are written, not omitted. `switch_distance_m` is the distance at which that LOD takes over (0 for LOD0); `cull_distance_m` (added in task 1.2) is where the LOD group stops rendering, and must exceed the last switch. The physics engineer reviews this schema before merge (team.md).
7. **K01 whole brick:** 0.250 × 0.120 × 0.065 m (AST-04, published standard), a small edge bevel and slight wear (an assumed 2–3 mm bevel), UVs in asset space and upright (§14.4). LOD0 bevelled, LOD1 a plain box, culled beyond about 120 m (derived: at 1080 px and a 60° vertical FOV, 935 px/rad, so the 0.25 m long edge drops under 2 px beyond 117 m). Collision: one box, material `masonry`, not landable. No gaps, no wind volume, no snag.
8. **CatalogImporter** (Editor): `Fpv450.Assets.CatalogImporter.Import(string id)`, plus a batchmode entry `Fpv450.Assets.CatalogImporter.ImportFromCommandLine` reading `-asset <ID>` (or `-asset all`), exiting 1 on any error. An `AssetPostprocessor` sets the FBX import settings for files under `Art/Models/` (scale 1, `generateMeshLods = false` because Unity 6.3 has no project-wide Mesh LOD switch and the project uses its own LOD groups, no Unity-generated colliders, materials not imported). Prefabs are rebuilt idempotently: rerunning with unchanged inputs produces no diff.
9. **build_asset.ps1** (PowerShell 7): `pwsh tools/build_asset.ps1 <ID>` → Blender build → Unity import (batchmode `-quit -nographics`). It reads `$env:UNITY` and the Blender path from `docs/environment.md`'s variables (`$env:UNITY`, `$env:BLENDER`), checks that no Unity editor holds the project open (the `Temp/UnityLockfile` lock), and stops at the first failing step.

## Risks / Trade-offs
- [FBX files embed a creation timestamp, so rebuilds churn the LFS blob] → acceptable in phase 0; build.py may normalise it if the exporter allows. Logged, not hidden.
- [Unity 6.x automatic Mesh LOD double-switching with our LODs, §14.4] → the postprocessor turns it off; QA checks the import settings.
- [API shapes of Poly Haven or ambientCG change] → `fetch.py` fails loudly; pinned SHA-256 values catch silent changes.
