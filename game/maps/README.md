# Map format 1.0

A map is authored data that stays engine-independent. Godot builds the runtime scene from it at load time, and Python tools read it without Godot.
The contract is the `map-format` spec (`openspec/changes/define-map-format/specs/map-format/spec.md`). This file is its field reference.

Check a package with:

```
python tools/map/validate_map.py game/maps/<id>
```

It exits 0 when the package is valid and prints one `ERROR:` line per problem otherwise.

## Package layout

```
game/maps/
  surfaces.json        shared surface table (all maps)
  <id>/                one map package; the folder name is the map id
    map.json           manifest
    height.r16         heightfield
    surface.png        surface ids
    cover.png          cover densities
    objects.json       placed objects and wires
game/assets/catalog.json   shared asset catalog (all maps)
```

A map side is at most 8192 m.

Inside `game/` each PNG layer also has a Godot sidecar, `surface.png.import` and `cover.png.import`, containing `importer="keep"`. Godot then leaves the layers as raw files and exports them unchanged, instead of converting them to textures. The loader reads the exact bytes. The validator ignores these sidecars.

## Coordinates and units

- Metres, seconds, kilograms and newtons everywhere. Angles are in degrees.
- Right-handed, Y up: **+X east, +Y up, +Z south** (Godot's −Z is north).
- The map centre is the world origin. A map of side `size_m` covers x and z from `−size_m/2` to `+size_m/2`.
- Grid **row 0 is the northmost row** (smallest z) and **column 0 the westmost** (smallest x).
  - Height samples are grid *vertices*. Sample (row r, column c) sits exactly at x = −size_m/2 + c·resolution_m, z = −size_m/2 + r·resolution_m.
  - Surface and cover pixels are grid *cells*. Cell (r, c) covers x from −size_m/2 + c·resolution_m to the next column, and z likewise. A point on the far edge (x or z = +size_m/2) belongs to the last cell.
  - So on a 256 m map, world (−128, −128) reads row 0, column 0 of every layer.
- Heights are absolute world Y in metres. There is no georeferencing (see the last section).

## `map.json` — manifest

```json
{
  "format_version": "1.0",
  "size_m": 256,
  "seed": 20260923,
  "height": { "resolution_m": 1.0, "samples_per_side": 257, "offset_m": -50.0, "scale_m": 0.01 },
  "surface": { "resolution_m": 0.5, "cells_per_side": 512 }
}
```

| Field | Type / unit | Rule |
|---|---|---|
| `format_version` | string `"major.minor"` | This format is `1.0`. Readers reject any major other than 1 and accept any 1.x (minor bumps only add optional fields). |
| `size_m` | number, m | 0 < size_m ≤ 8192 |
| `seed` | integer | 32-bit unsigned (0 to 4294967295). Drives all procedural micro-detail, micro-relief and pitfalls. |
| `height.resolution_m` | number, m | > 0, and size_m / resolution_m is a whole number. Default 1 m. |
| `height.samples_per_side` | integer | = size_m / height.resolution_m + 1 |
| `height.offset_m` | number, m | Height of sample value 0 |
| `height.scale_m` | number, m | > 0. Metres per sample step. Default 0.01 (1 cm steps, 655.35 m range). |
| `surface.resolution_m` | number, m | > 0, and size_m / resolution_m is a whole number. Default 0.5 m. |
| `surface.cells_per_side` | integer | = size_m / surface.resolution_m. The cover layer uses this same grid. |

Unknown fields are ignored, except the georeference fields listed at the end, which are rejected.

## Layer encodings

| File | Encoding | Grid |
|---|---|---|
| `height.r16` | Raw unsigned 16-bit **little-endian** samples, row-major, no header. Height (m) = `offset_m + sample × scale_m`. | `samples_per_side`² samples, so the file is exactly `samples_per_side² × 2` bytes |
| `surface.png` | PNG, 8-bit greyscale, non-interlaced. Each value is a surface `index` from `surfaces.json`. **0 is invalid.** | `cells_per_side`² pixels |
| `cover.png` | PNG, 8-bit RGBA, non-interlaced. **R = grass stems, G = lodged straw, B = twigs, A = leaf litter.** Each channel is a 0–1 multiplier (value / 255) on that cover type's density in the cell's surface. | `cells_per_side`² pixels, the same grid as `surface.png` |

Stems, straws and twigs are never stored. They are generated from an integer hash of world cell, map seed and kind, so visuals and physics see the same ones.

## `objects.json` — placed objects and wires

```json
{
  "objects": [
    { "asset": "house_box", "position_m": [10.0, 1.2, -20.0], "rotation_deg": [30.0, 0.0, 0.0], "scale": 1.0 },
    { "asset": "cable", "points_m": [[-5.0, 7.8, 3.0], [25.0, 7.8, 3.0]], "sag_m": 0.6, "diameter_m": 0.01 }
  ]
}
```

Every object names an `asset` id from `game/assets/catalog.json`. Its fields depend on the asset's `type`:

| Asset type | Field | Type / unit | Rule |
|---|---|---|---|
| `object` | `position_m` | [x, y, z], m | World position of the asset origin. x and z inside the map. y is absolute. |
| | `rotation_deg` | [yaw, pitch, roll], degrees | Applied as yaw about +Y, then pitch about +X, then roll about +Z (Godot's default YXZ order). Positive yaw turns +X toward −Z (counter-clockwise seen from above). |
| | `scale` | number | > 0, uniform |
| `wire` | `points_m` | list of [x, y, z], m | At least 2 points. Every point inside the map. Wire ends are its attachment points. |
| | `sag_m` | number, m | ≥ 0. Mid-span drop of each span below the straight line between its points. |
| | `diameter_m` | number, m | > 0 |

## `surfaces.json` — surface table (shared)

The physics data for every ground type. **Values are starting values, and physics will tune them in flight tests.** The physics engineer signs off the fields, units and ranges (change `define-map-format`, task 2.2).

Each surface:

| Field | Unit | Valid range | Meaning |
|---|---|---|---|
| `id` | — | `[a-z][a-z0-9_]*`, unique | Name used by tools and logs |
| `index` | — | integer 1–255, unique | Value in `surface.png` |
| `soil.bearing_n_per_m3` | N/m³ | 1e4 – 1e9 | Winkler bearing stiffness: contact pressure per metre of sink |
| `soil.damping_ns_per_m3` | N·s/m³ | 0 – 1e8 | Contact pressure per m/s of sink rate |
| `soil.friction_static` | — | 0 – 2 | Static friction, leg on this ground |
| `soil.friction_kinetic` | — | 0 – 2, ≤ static | Sliding friction |
| `soil.max_sink_m` | m | 0 – 1 | Sink depth at which a firm layer stops the leg |
| `soil.porosity` | — | 0 – 1 | Void fraction of the soil (typical for the soil class, from the reference notes §7) |
| `micro_relief.amplitude_m` | m | 0 – 0.5 | RMS height of procedural relief finer than the height grid |
| `micro_relief.wavelength_m` | m | 0.05 – 10 | Its typical horizontal feature size |
| `pitfalls.density_per_m2` | 1/m² | 0 – 1 | Hidden holes per square metre |
| `pitfalls.depth_m` | [min, max], m | 0 – 2, min ≤ max | Hole depth range |
| `pitfalls.radius_m` | [min, max], m | 0 – 2, min ≤ max | Hole radius range |
| `cover` | list | 0–4 entries, one per type | Loose cover on this ground, see below |
| `dust_emission` | — | 0 – 1 | How much dust prop wash lifts near the ground |
| `material.albedo_srgb` | `#rrggbb` | — | Visual base colour at photo-like albedo (the video feed adds its own shift) |
| `material.roughness` | — | 0 – 1 | Visual roughness |

Each `cover` entry:

| Field | Unit | Valid range | Meaning |
|---|---|---|---|
| `type` | — | `grass`, `straw`, `twigs`, `litter` | Picks the `cover.png` channel: R, G, B, A |
| `height_m` | [min, max], m | 0 – 3, min ≤ max | Element length along its axis: standing height for grass, lying length for straw and twigs, layer thickness for litter |
| `stems_per_m2` | 1/m² | 0 – 5000 | Elements per m² at cover multiplier 1.0 |
| `diameter_m` | [min, max], m | 0 – 0.2, min ≤ max | Stem, straw or twig diameter. For litter, the leaf width. |
| `lateral_stiffness_n_per_m` | N/m | 0 – 1e4 | Sideways force per metre of tip deflection for one element, at its full length (a cantilever at the root; physics scales it for lower contact points) |
| `hook_probability` | — | 0 – 1 | Chance that an element touching a leg hooks it for a moment |

How the starting soil values were chosen: the reference notes (§7) give how far a leg sinks on each ground. The bearing stiffness reproduces that sink for a reference contact of 25 N on a 15 mm foot (about 140 kPa, a 10 kg drone on four legs). The damping gives a damping ratio for that contact: low on hard crust and rubble (the drone bounces), high on sod and spoil (the drone is cushioned). Physics replaces the reference contact with the real leg design.

## `game/assets/catalog.json` — asset catalog (shared)

```json
{
  "assets": {
    "pole": {
      "type": "object",
      "scene": "res://assets/models/placeholders/pole.tscn",
      "collision": [ { "shape": "cylinder", "radius_m": 0.11, "height_m": 8.0, "position_m": [0, 4.0, 0] } ],
      "snag_hazard": true,
      "wind_porosity": 0.0,
      "gaps": []
    }
  }
}
```

| Field | Type / unit | Rule |
|---|---|---|
| `type` | `object` or `wire` | A `wire` is placed with points, sag and diameter instead of a transform |
| `scene` | `res://` path | Must exist. For a `wire` it is a unit segment (1 m long along +Z from the origin, 1 m diameter) that the loader stretches along each piece of the sagged curve. |
| `visual_only` | boolean, optional | `true` means no collision. Default `false`. |
| `collision` | list of shapes | Required and non-empty unless `visual_only`. Shapes are in asset space and scale with the object. |
| `snag_hazard` | boolean | Catches the fiber or the legs |
| `wind_porosity` | 0 – 1 | Fraction of wind passing through the object's volume: 0 = solid, 1 = open |
| `gaps` | list | Named fly-through openings, may be empty |

Collision shapes. Cylinders and capsules stand along asset +Y. `position_m` (default [0, 0, 0]) and `rotation_deg` (default [0, 0, 0], same order as objects) place each shape.

| `shape` | Size fields (m) |
|---|---|
| `box` | `size_m`: [x, y, z] |
| `sphere` | `radius_m` |
| `cylinder` | `radius_m`, `height_m` |
| `capsule` | `radius_m`, `height_m` (total, including the caps) |
| `capsule_chain` | `segment_m`: capsule length along the sagged curve. Wires only; the radius is half the object's `diameter_m`. |

Each gap is a rectangular opening in asset space: `name`, `center_m` [x, y, z], `width_m`, `height_m` and `yaw_deg`. The opening faces asset ±Z, turned by `yaw_deg`.

## Versioning

`format_version` is `major.minor`. A minor bump adds optional fields that older readers ignore. A major bump breaks compatibility. The validator and the loader reject a major they do not support, and name both versions.

## No real-world reference

Maps are composites inspired by the reference footage, never replicas of a real place. The validator rejects:

- any field whose name has a part (split at `_` and capitals) such as `lat`, `lon`, `lng`, `latitude`, `longitude`, `epsg`, `crs`, `srs`, `utm`, `mgrs`, `wgs84`, `geo`, `georef`, `geolocation`, `geojson` or `geotiff`, anywhere in the manifest, objects, surface table or catalog (so `origin_lat` and `geoRef` are rejected, `geometry` is not)
- any text value that contains a real place name from `tools/map/place_blocklist.txt` (matched at the start of a word, so case endings and adjectives count), a decimal coordinate pair or an MGRS grid reference. This is a safety net and can trip on a common noun such as «лиман»; rephrase the text.
- PNG metadata that could carry a location (`eXIf` chunks, and text chunks that match the rules above)
- GIS sidecar files in the package folder (`.pgw`, `.wld`, `.prj`, `.aux.xml`, `.tfw`, `.kml`, `.kmz`, `.gpx`, `.geojson`)
