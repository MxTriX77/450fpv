# maps/farmland: the farmland's map package

The friend's 1 × 1 km farmland (D-088) as the sim reads it: the ground's heights over the map, the surround and the far ring (D-091), the ground class and grass per cell, every tree, bush, ruin and debris placement, and the field and village outlines. Then our own objects (D-090) with the terrain edits they need: a trench per field, two houses and two cars in the village, the hole mask and the levelled footprints. Format `fpv450-map-package`, version 1 (w2-farmland-export; our objects w5-farmland-objects, all additive).

**Never edit it by hand.** The exporter `Fpv450.FarmlandExport` (`unity/Assets/_Project/Editor/Farmland/FarmlandExport.cs`) writes it from the scene the friend's seeded generator (`FarmLandBuilder`) built. It reads the scene, the terrain data and, by reflection, the generator's layout, and changes nothing. The same scene gives the same bytes. Then the placer `tools/map/place_objects.py` adds our objects and their edits (see "Our objects" below). The export drops them, so run the placer after every export. To rebuild it:

```powershell
& $env:UNITY -batchmode -nographics -quit -projectPath <project with the farmland scene> -executeMethod Fpv450.FarmlandExport.Run -exportOut <repo>/maps/farmland -logFile - | Out-Host
python tools/map/place_objects.py maps/farmland
python tools/map/validate.py maps/farmland
```

`-exportOut` defaults to the project's `../maps/farmland`, and `-exportScene` to the one scene named `FarmLand`.

## Files

| File | Contents |
|---|---|
| `map.json` | The manifest: frame, tiles, the ground class table, the grass layers, counts, our objects' record, and every other file's SHA-256 |
| `heights/<tile>.r16` | A tile's heightmap: uint16 little-endian samples, resolution × resolution |
| `classes/<tile>.raw` | A tile's ground class per sample: uint8 surface ids, resolution × resolution |
| `grass/map.raw` | The map's grass: uint8 instance counts, one resolution × resolution plane per layer, layer after layer |
| `placements.json` | Every tree, bush, ruin and debris piece on the map, then our trenches, houses and cars |
| `holes/map.raw` | The map tile's hole mask: uint8 per height cell, 255 a hole and 0 ground |
| `footprints.json` | The map tile's height samples our objects level, as the samples to write |
| `outlines.json` | The 11 fields and the village |
| `height_probes.json` | 1,000 heights from Unity's `Terrain.SampleHeight`, which the validator checks the package against |

The binaries are in Git LFS (`*.r16`, `*.raw`). The heights and classes are the friend's generated data, so they can be pushed; no store mesh or texture is in the package.

## Frame and grids

- **Unity's world frame** (D-019): metres, +X east, +Y up, +Z north, left-handed. The coordinates are the friend's scene's own, unchanged, so the sim and the scene need no conversion.
- **The map is [0, 1000] × [0, 1000] m,** its south-west corner at the origin (`map_rect_m`). D-019 puts a map's centre at the origin, but the friend's generator places everything in these coordinates, so the farmland keeps them.
- **Every grid** is row-major, **row 0 the northmost (+Z) row** and column 0 the westmost, rows running south (D-019). Unity's own arrays start from the south; the exporter flips them.

## Tiles

`map.json` lists 17 tiles, each with its id, ring, `position_m` (the terrain's corner, Unity's `Terrain.GetPosition`) and `size_m` (Unity's `TerrainData.size`).

| Ring | Tiles | Size | Height samples | Spacing |
|---|---|---|---|---|
| `map` | `map` | 1000 × 1000 m | 1025² | 0.977 m |
| `surround` | `surround_SW`, `_S`, `_SE`, `_W`, `_E`, `_NW`, `_N`, `_NE` | 1000 × 1000 m, around the map | 1025² | 0.977 m |
| `far` | `far_SW` … `far_NE` | 3000 × 3000 m at the corners, 3000 × 3024 m at the sides | 257² | 11.7–11.8 m |

The far ring reaches 4 km past the map, to [−4000, 5000]². Its side tiles reach 24 m under the surround block, where they lie at least 1.28 m below it (measured).

## Heights

- **A sample** s (0 … 32766) is the height **y = position_m[1] + s / 32766 × size_m[1]**: Unity stores a terrain height as s / 32766 of the tile's height. Every tile here has y = 0 and a height of 120 m.
- **Sample (c, r)** of a file lies at x = x0 + c · sx / (res − 1) and z = z0 + sz − r · sz / (res − 1).
- **Between samples, Unity's triangulation:** with Unity's indices (z from the south), fx = (x − x0) / sx · (res − 1) and fz = (z − z0) / sz · (res − 1); the cell is (⌊fx⌋, ⌊fz⌋), clamped to res − 2, and u, v are the fractions left. With h00, h10, h01 and h11 the cell's south-west, south-east, north-west and north-east samples:
  - u > v: h = h00 + (h10 − h00) · u + (h11 − h10) · v
  - otherwise: h = h00 + (h11 − h01) · u + (h01 − h00) · v

  Each cell splits along its diagonal from the south-west corner to the north-east corner. `tools/map/validate.py` (`Tile.height`) is the reference implementation.
- **Measured:**
  - Unity's `Terrain.SampleHeight` agrees with this within 0.011 mm at the 1,000 probes. The other diagonal misses by up to 155 mm, and bilinear interpolation by up to 89 mm, so the probes tell them apart.
  - Unity's terrain colliders agree with `SampleHeight` within 0.021 mm at the same probes (a ray cast down onto each probe, at export).
  - The trees and bushes stand on this ground within 0.06 mm.
  - The rendered terrain is drawn at a coarser level of detail away from the camera (pixel error 5 on the map, 8 on the surround, 20 on the far ring). That isn't checked here (WLD-11).
- **Where tiles overlap,** the ground is the highest tile's surface (the far ring's sides under the surround block).
- **Seams:** the map and the 8 surround tiles share their edge samples exactly (12 seams, validated).
- **Holes:** an optional `"holes": {"file": "holes/<tile>.raw", "resolution": res − 1}` per tile (W2 design §4, signed off).
  - Unity's hole grid: one byte per height cell, uint8 255 for a hole and 0 for ground, row 0 the northmost.
  - Cell (c, r) with Unity's indices is the square x ∈ [x0 + c · s, x0 + (c + 1) · s], z ∈ [z0 + r · s, z0 + (r + 1) · s], s = sx / (res − 1). A point belongs to the cell its triangulation uses.
  - Over a hole cell the tile has no ground. Unity's own sense is inverted (`TerrainData.SetHoles`: `true` is ground), so the player flips it.
  - Only the map tile carries one: our trenches' (below). The friend's terrain has none.
- **The heights files stay the friend's.** Our footprints are separate data, written over them at load (D-095). The probes check the files as exported.

## Ground classes

- **One surface id per alphamap sample:** the row of `assets-src/surfaces.json`'s `surfaces` plus 1 (`MapWorld.SurfaceIds`). 0 never appears.
- **Sample (c, r)** lies at x = x0 + c · sx / (res − 1) and z = z0 + sz − r · sz / (res − 1). That's where URP's terrain shader centres its control map's texels: `splatUV = (uv · (res − 1) + 0.5) / res`.
- **Resolutions:** 2048 on the map (0.49 m), 512 on the surround (1.96 m) and 256 on the far ring (11.8 m).
- **The class of a sample** is the surface whose layers weigh most there, ties going to the lower id. Each of the 8 terrain layers maps to one surface in `map.json` (`ground_classes.layers`), all **assumed**:

| Layer | Surface | Why |
|---|---|---|
| `GrassGround`, `GrassA` | `meadow_sod` (1) | The meadow: root-bound sod |
| `GrassDry` | `weeds` (8) | Dry grass: the fields' weed patches and the meadow's dried-out third; firmer than the green sod |
| `GrassPath`, `Gravel` | `dirt_track` (13) | The 4 m grassy tracks and the 6 m gravel tracks, wheel-compacted. There is no gravel class |
| `AerialMud_0`, `AerialMud_90` | `tilled` (6) | The abandoned fields' furrows. Their direction is each field's `furrow_axis` in `outlines.json` |
| `DryMud` | `dry_crust` (2), but `dirt_track` (13) where `Gravel` is painted | Bare dry mud on the fields, the house yards and the sun-burnt meadow. With gravel, it is the track's or a yard's mud film on a compacted body |

- **One rule** (the physics sign-off, `ground_classes.rules`): on any sample with `Gravel` weight > 0, `DryMud`'s weight counts toward `dirt_track`, not `dry_crust`. Then the heaviest surface wins, as above.
- **Measured shares on the map:** meadow_sod 33.4 %, weeds 34.2 %, tilled 28.6 %, dirt_track 2.1 % and dry_crust 1.6 %.
- **The main tracks** read `dirt_track` all along their centre lines.
- **Round the ruins,** the trampled earth (`DryMud`) carries the generator's rubble gravel in places. There it reads `dirt_track`, elsewhere `dry_crust`. Within 10 m of a ruin's pivot (measured): dirt_track 55 %, dry_crust 37 %, meadow_sod 4 %, weeds 3 %.

## Grass

- **The map only:** a 1024 × 1024 detail grid. Cell (c, r) covers x ∈ [x0 + c · s, x0 + (c + 1) · s) and z ∈ [z0 + sz − (r + 1) · s, z0 + sz − r · s), with s = 1000 / 1024 m.
- **Each value** is how many instances of that layer grow in the cell. This is Unity's instance-count scatter, which spreads them over the cell.
- **The 14 layers** in `map.json` (`grass.layers`) carry the generator's group (Green, GreenClump, Dry, DryClump, Burnt, TallGreen, TallDry, TallBurnt), the width and height scale ranges, the mesh's vertical extent (`mesh_y_m`) and the layer's total. An instance stands about `mesh_y_m[1]` × its height scale tall. The 2.84 M instances drive the micro-detail on each class (AST-10, W4a).
- **Not exported:** the surround's grass, which grows within 170 m of the map's edge. The sim knows no grass there.

## Placements (`placements.json`)

- **`variants`:** every prefab placed on the map, by its prefab name, with its kind: `tree`, `bush`, `ruin` or `debris` (34 variants).
- **`placements`:** one row per object, with `id` (0, 1, 2 …), `kind`, `variant`, `position_m` (the prefab's pivot), `rotation_xyzw` (Unity's quaternion) and `scale` (Unity's scale on each axis).
- **Trees and bushes** (294 and 1,352) are the map's terrain tree instances, in Unity's order. A prototype's folder (`Trees` or `Bushes`) names its kind.
  - The pivot is on the ground.
  - The rotation is a pure yaw.
  - The scale is (width, height, width).
- **Ruins and debris** (5 and 28) are the `Village` object's children, in hierarchy order. A `Ruin_*` prefab is a ruin, and any other is debris.
  - The houses sit 80 mm into the ground and the debris 20 mm, as the generator places them.
  - The debris is tilted to the ground.
  - One ruin is mirrored (scale x = −1).
- **The map only (D-091):** the surround's 499 trees are left out, and `map.json` counts them. The export checks that every terrain instance and village object has exactly one row, that every row lies on the map, and that nothing else draws on the map.
- **Rounding:** positions to 0.1 mm, quaternions to 1e-7 and scales to 1e-5.

## Our objects (`placements.json`, `holes/`, `footprints.json`)

`tools/map/place_objects.py` (w5-farmland-objects 1.2; D-090, D-092, D-095) writes them from a seed, recorded in `map.json` as `"objects": {"placer", "seed", "footprints"}`. Each object draws on its own SplitMix64 stream, the seed mixed with its name, until its spot passes every rule.

- **The rows:** ids 1679 onwards, after the export's, in its format: `trench` `E01_curving` × 11, then `house` `H01_I` and `H01_D`, and `car` `R01_A` and `R01_F`.
  - Their variants are in our catalog (`assets-src/catalog.json`), not the vendor lists, so a loader finds their metadata there.
  - The rotation is a pure yaw, the scale 1, and y the footprint's level.
- **The footprint** of a row is the plan rectangle of its variant's bounds: E01's whole ground patch, a house or car with its spilled rubble and parts.
- **One trench per field F1–F11,** at a seeded spot and yaw, its patch at least 11.5 m inside the field's outline (the edits' 8.5 m reach, then 3 m).
- **The houses and cars in the village,** each footprint inside the village circle. A house turns its front (asset −Z) to the lane through the village's centre (z = 600) within 6°, as the friend's five ruins stand. A car takes any yaw.
- **Free ground:**
  - no edit reaches a ruin's or a debris piece's plan bounds, a tree's or bush's pivot, the start's stand (`FlightWorlds.FarmlandStart`, D-092) or another of ours;
  - a tree's or bush's plan bounds stays 3 m off the footprint, and a house's footprint 3 m off every `dirt_track` sample (the friend's house clearances);
  - the ground under a footprint spans at most 1 m (2.5 m under a house), so a levelled patch doesn't read as a terrace.
- **The hole mask** (W2 design §4):
  - Rule 2: every cell that meets a trench's drawn opening is a hole. The opening is a stadium about each of E01's hole records' centre lines: the run's lip at 0.525 m (`top_w_m` / 2 + `width_jitter_m` / 2 + `lip_crumble_m`), a niche at 0.4 m.
  - Rule 3: every point of every hole cell lies inside E01's filler bands (3.85 m on the run, 3.60 m at the niches), where its shapes are the only ground.
- **The footprints** (D-095), levelled as the friend's generator levels its house plots (`LevelVillageGround`):
  - the level is the mean of the samples inside the footprint, rounded to a sample;
  - every sample within 1.5 m of the footprint takes it, so every cell that meets the footprint is flat;
  - farther out, a sample eases back to its own over a 7 m shoulder (a smoothstep), rounded to a sample;
  - `footprints.json` lists, per object, its corners, `level_sample` and `level_m`, and the samples it writes as runs `[file row (0 the northmost), first column, [samples…]]` of the map tile. The sim and the player write them over the heights at load.
- **Every hole cell's corners** stand at its trench's level, so the cut meets E01's own ground flush.

## Outlines (`outlines.json`)

- **`fields`:** F1–F11 as the generator defines them (`FarmLandBuilder.Fields`).
  - Each has its name, `overgrowth` (0 for bare furrows to 1 for mostly grown over), `furrow_axis` (`X` or `Z`, the world axis its rows run along) and `outline_m`.
  - The outline is the generator's own polygon: counter-clockwise seen from above, with curved sides as 16 segments.
- **`village`:** the generator's village area, a circle of 75 m about (512, 600). It holds every ruin and debris piece.

## Height probes (`height_probes.json`)

- **1,000 seeded points:** SplitMix64 with seed 450 draws 500 on the map, 300 on the surround and 200 on the far ring, at multiples of 1/1024 m.
- **Each probe's `y`** is Unity's `Terrain.SampleHeight` plus the tile's y, the highest among the tiles that hold the point.
- The validator triangulates the package at each probe and requires a match within 1 mm (spec, assumed).

## Validation

`python tools/map/validate.py maps/farmland` checks:
- the format, the frame and the map's id, and that no field georeferences the map (SEC-03)
- every file's SHA-256, and that no file is an LFS pointer or unlisted
- the tiles' resolutions (2^n + 1), sample range and seams
- the classes against `assets-src/surfaces.json`
- the grass planes against their totals
- the placements: kinds, variants, ids, unit rotations, on the map and counted
- the outlines: counter-clockwise, on the map, and the village holding the ruins and debris
- the 1,000 probes within 1 mm
- a tile's hole mask: its size and its values, 0 or 255
- our objects (`place_objects.check`):
  - the same seed gives the same rows;
  - one trench inside each field with its margin, the village's four inside its circle, every one on free ground;
  - the hole mask equals the one the trenches derive, and every hole cell, sampled every 0.1 m with its edges, lies inside a filler band;
  - the footprints equal the ones the rows derive; every cell that meets a footprint is level, every hole cell's corners are at its trench's level, and no edit reaches the tile's edge;
  - every object stands at its level within 1 cm

Its tests are `tools/map/tests/test_validate.py` and `tools/map/tests/test_place_objects.py`.
