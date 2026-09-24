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

A map side is a multiple of 256 m (the terrain collision chunk) and at most 8192 m. Other sides would make the terrain fall back to slower mesh collision.

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
  "surface": { "resolution_m": 0.5, "cells_per_side": 512 },
  "starts": [ { "position_m": [12.0, 0.4, -30.0], "yaw_deg": 90.0 } ]
}
```

| Field | Type / unit | Rule |
|---|---|---|
| `format_version` | string `"major.minor"` | This format is `1.0`. Readers reject any major other than 1 and accept any 1.x (minor bumps only add optional fields). |
| `size_m` | number, m | A multiple of 256, from 256 to 8192 |
| `seed` | integer | 32-bit unsigned (0 to 4294967295). Drives all procedural micro-detail, micro-relief and pitfalls. |
| `height.resolution_m` | number, m | > 0, and size_m / resolution_m is a whole number. Default 1 m. |
| `height.samples_per_side` | integer | = size_m / height.resolution_m + 1 |
| `height.offset_m` | number, m | Height of sample value 0 |
| `height.scale_m` | number, m | > 0. Metres per sample step. Default 0.01 (1 cm steps, 655.35 m range). |
| `surface.resolution_m` | number, m | > 0, and size_m / resolution_m is a whole number. Default 0.5 m. |
| `surface.cells_per_side` | integer | = size_m / surface.resolution_m. The cover layer uses this same grid. |
| `starts` | list, optional | Start points. Leave it out for none. When legs are off, the game places the launch rails at the chosen start point. |
| `starts[].position_m` | [x, y, z], m | x and z inside the map. y is absolute. |
| `starts[].yaw_deg` | number, degrees | Heading, the same convention as object yaw |

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

The physics data for every ground type. **Values are starting values, and physics will tune them in flight tests.** The physics engineer reviewed the fields, units and ranges (`openspec/changes/define-map-format/surfaces-review.md`), and this table applies that review.

At the top level, next to `surfaces`:

| Field | Unit | Valid range | Meaning |
|---|---|---|---|
| `soil_reference_diameter_m` | m | 0.005 – 0.1 | The foot diameter at which `bearing_n_per_m3` and `damping_ns_per_m3` are defined (0.015). Physics scales the modulus for a contact of diameter b by (b_ref / b)^0.3. |

Each surface:

| Field | Unit | Valid range | Meaning |
|---|---|---|---|
| `id` | — | `[a-z][a-z0-9_]*`, unique | Name used by tools and logs |
| `index` | — | integer 1–255, unique | Value in `surface.png` |
| `soil.bearing_n_per_m3` | N/m³ | 1e4 – 1e9 | Virgin-loading Winkler modulus at the reference diameter: contact pressure per metre of sink |
| `soil.damping_ns_per_m3` | N·s/m³ | 0 – 1e8 | Contact pressure per m/s of sink rate |
| `soil.friction_static` | — | 0 – 2 | Static friction, leg on this ground |
| `soil.friction_kinetic` | — | 0 – 2, ≤ static | Sliding friction |
| `soil.max_sink_m` | m | 0 – 1 | Total sink (elastic + plastic) at which a firm layer stops the leg |
| `soil.unload_stiffness_ratio` | — | 1 – 100 | Unload and reload stiffness over the loading stiffness. 1 is purely elastic. A fraction 1 − 1/ratio of the loading energy stays as plastic sink, so legs settle and landings thud. |
| `micro_relief.amplitude_m` | m | 0 – 0.5 | RMS height of procedural relief finer than the height grid |
| `micro_relief.wavelength_m` | m | 0.05 – 10 | Its crest-to-crest distance. The relief is smooth value noise with lattice nodes half a wavelength apart. |
| `micro_relief.ridges` | object, optional | — | Directional furrows added on top of the relief. Leave it out for none. |
| `micro_relief.ridges.amplitude_m` | m | 0 – 0.3 | Half the crest-to-trough height |
| `micro_relief.ridges.spacing_m` | m | 0.1 – 5 | Crest to crest |
| `micro_relief.ridges.azimuth_deg` | degrees | 0 – 180 | The direction the crests run. 0 = along X (east–west); positive turns toward −Z, as object yaw does. One field direction per surface variant (`tilled_000`, `tilled_045`, …). |
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
| `height_m` | [min, max], m | 0 – 3, min ≤ max; min > 0 when `stems_per_m2` > 0 | Element length along its axis: standing height for grass, lying length for straw and twigs (measured horizontally, see Micro-detail below), layer thickness for litter |
| `stems_per_m2` | 1/m² | 0 – 5000 | Elements per m² at cover multiplier 1.0 |
| `diameter_m` | [min, max], m | 0 – 0.2, min ≤ max; min > 0 when `stems_per_m2` > 0 | Stem, straw or twig diameter. For litter, the leaf width. |
| `lateral_stiffness_n_per_m` | N/m | 0 – 1e4 | Tip stiffness (sideways force per metre of tip deflection, a cantilever at the root) of an element of **mean** length and **mean** diameter. Each element is scaled by (d / d̄)⁴ × (L̄ / L)³, so all elements of a cover share one tissue modulus. Physics scales it for lower contact points. |
| `hook_probability` | — | 0 – 1 | Chance that an element touching a leg or the fiber hooks it for a moment |
| `hook_release_n` | [min, max], N | 0 – 500, min ≤ max | The pull at which a hooked element lets go (slips off, pulls out or breaks). Each element draws its own value from its hash. |
| `mat` | object, optional | — | A compressible layer on the soil (thatch, lodged straw, leaves). Leave it out for none. The mats of all covers stack in series. |
| `mat.depth_m` | [min, max], m | 0 – 0.5, min ≤ max | Uncompressed depth at cover multiplier 1.0. The depth at a point is smooth hash noise in [min, max] at the surface's relief wavelength, times this cover's `cover.png` channel. It drapes into pitfalls and never bridges them. |
| `mat.modulus_pa` | Pa | 10 – 1e6 | Compressive modulus (pressure per unit strain) |
| `mat.damping_ratio` | — | 0 – 2 | Damping ratio of the mat contact |
| `mat.friction_static`, `mat.friction_kinetic` | — | 0 – 2, kinetic ≤ static | Foot friction on the mat |

How the starting soil values were chosen: the reference notes (§7) give how far a leg sinks on each ground. The bearing stiffness reproduces that sink for a reference contact of 25 N on a 15 mm foot (about 140 kPa, a 10 kg drone on four legs). The damping gives a damping ratio for that contact: low on hard crust and rubble (the drone bounces), high on sod and spoil (the drone is cushioned). Physics replaces the reference contact with the real leg design.

### How the world query turns the table into ground

`game/src/world/WorldQuery.cs` builds everything below the height grid from the map seed and these fields, with integer hashes and plain IEEE arithmetic, so every machine gets the same bits (world-query spec).

- **Ground height** = the rendered terrain triangle + micro-relief + ridges − pitfalls. Relief and mat blend bilinearly over the 4 nearest cell centres, so the ground has no step at a surface border.
- **Micro-relief** is value noise with nodes half a `wavelength_m` apart and quintic fades, scaled so its RMS is `amplitude_m`. Peaks reach about 2.2 × the amplitude.
- **Ridges** use the profile 1 − 2·smoothstep(2q), where q is the distance to the nearest crest in spacings. It is C², close to a cosine, with an RMS of 0.697 × `amplitude_m`. The phase comes from the seed.
- **Pitfalls:** each 1 m world cell holds at most one candidate, at a hashed point. It exists with the `density_per_m2` of the surface under that point, and its radius and depth are drawn uniformly from the ranges. The floor is flat, and the wall is a smoothstep over the outer 25 % of the radius. A pitfall is never clipped at a cell or surface border, and where two overlap, the deeper one counts. Its id is its cell: (x + 32768) in the low 16 bits and (z + 32768) in the high 16 bits.
- **Mat depth** is smooth noise between `mat.depth_m` min and max, with nodes half the surface's relief wavelength apart, times the cover channel. It follows the ground down into pitfalls.
- **Micro-detail:** each 0.25 m world cell holds density × channel × 0.0625 m² elements of each kind. The fraction is dithered over 1 m blocks, so the count per m² matches the table. Length, diameter and hook release are uniform in their ranges, and an element hooks with `hook_probability`. Grass stands rooted on the ground, leaning up to 20° from vertical. Straw, twigs and litter lie straight from the mat top (SupportTop) at their root to the mat top at their tip, which is the drawn length away along their heading. So an element's `Length` is that chord: the drawn length on level ground, a little more on a slope. Over a dip or a pitfall it bridges, and across a hump it cuts in. Each element has a stable 64-bit id: 19 bits of the seed hash, then cell z and cell x (each + 65536, 17 bits), kind (2 bits) and slot (9 bits).
- **Outside the map**, terrain and surfaces continue from the edge cell, and samples carry the `OutsideMap` flag.

There is no soil porosity field (the notes §7 give 0.40–0.60 void fractions). The contact model never used it: "loose" and "porous" ground is carried by the bearing modulus, the unload ratio and the maximum sink, and rubble voids are pitfalls.

## `game/assets/catalog.json` — asset catalog (shared)

```json
{
  "materials": {
    "timber": { "friction_static": 0.45, "friction_kinetic": 0.35, "edge_radius_m": 0.002 }
  },
  "assets": {
    "tree_proxy": {
      "type": "object",
      "scene": "res://assets/models/placeholders/tree_proxy.tscn",
      "material": "timber",
      "collision": [ { "shape": "cylinder", "radius_m": 0.1, "height_m": 6.0, "position_m": [0, 3.0, 0] } ],
      "wind_volume": [ { "shape": "sphere", "radius_m": 3.0, "position_m": [0, 7.0, 0] } ],
      "snag_hazard": true,
      "wind_porosity": 0.5,
      "gaps": []
    }
  }
}
```

### Materials

`materials` is the shared contact-material table. Physics reads it for every contact with an object or a wire.

| Field | Unit | Valid range | Meaning |
|---|---|---|---|
| `friction_static` | — | 0 – 2 | Stick limit for feet, skids and the fiber on this material |
| `friction_kinetic` | — | 0 – 2, ≤ static | Sliding friction |
| `stiffness_n_per_m` | N/m | 1e2 – 1e8, optional | The object's local stiffness under a point load (panel flex, a batten bending), in series with the drone part. Leave it out for a rigid material. |
| `damping_ratio` | — | 0 – 2, optional | Damping of that local stiffness. Default 0.05. Only used with `stiffness_n_per_m`. |
| `edge_radius_m` | m | 1e-4 – 0.05, optional | Radius of box edges where the fiber bends over. Default 0.002. Cylinders, capsules and wires use their own radius. |

There is no restitution field. On rigid materials the drone's leg and frame compliance sets the bounce, and on compliant ones the bounce comes from stiffness plus damping.

### Assets

| Field | Type / unit | Rule |
|---|---|---|
| `type` | `object` or `wire` | A `wire` is placed with points, sag and diameter instead of a transform |
| `scene` | `res://` path | Must exist. For a `wire` it is a unit segment (1 m long along +Z from the origin, 1 m diameter) that the loader stretches along each piece of the sagged curve. |
| `visual_only` | boolean, optional | `true` means no collision. Default `false`. |
| `collision` | list of shapes | Required and non-empty unless `visual_only`. Primitive shapes only (see below); there is no mesh collision. Shapes are in asset space and scale with the object. |
| `material` | material id | Required when the asset has collision. Any collision shape may override it with its own `material`. |
| `snag_hazard` | boolean | Catches the fiber or the legs |
| `wind_volume` | list of shapes, optional | The volume that blocks wind, in the same schema as `collision` (primitives only, no `material`). Leave it out to use the collision shapes. A tree collides only as its trunk, but its crown blocks the wind. |
| `wind_porosity` | 0 – 1 | Optical porosity of the wind volume seen side-on: the fraction of the silhouette you can see through. 0 = solid, 1 = open. |
| `gaps` | list | Named fly-through openings, may be empty |

Collision and wind-volume shapes. Cylinders and capsules stand along asset +Y. `position_m` (default [0, 0, 0]) and `rotation_deg` (default [0, 0, 0], same order as objects) place each shape.

| `shape` | Size fields (m) |
|---|---|
| `box` | `size_m`: [x, y, z] |
| `sphere` | `radius_m` |
| `cylinder` | `radius_m`, `height_m` |
| `capsule` | `radius_m`, `height_m` (total, including the caps) |
| `capsule_chain` | `segment_m`: capsule length along the sagged curve. Wires only, and a wire has exactly this one shape; the radius is half the object's `diameter_m`. Physics treats the chain as the wire's polyline. |

Each gap is a rectangular opening in asset space: `name`, `center_m` [x, y, z], `width_m`, `height_m` and `yaw_deg`. The opening faces asset ±Z, turned by `yaw_deg`.

### How the world query uses objects

`game/src/world/` reads `objects.json` and the catalog without Godot (D-010), and the loader draws from the same rules:

- **Indices.** An object's index is its position in `objects.json`, where wires and visual-only objects count too. Objects the game adds before a flight, such as the launch rails at a start point, get the next indices. Before each flight the game calls `ResetRuntimeObjects`, which removes them from contacts, rays, gaps and the wind grid, so that the world is its `objects.json` state again and the next rails get the same index. The flight log records every object added after the reset. A material id is the material's position in the catalog's `materials` object.
- **Wires.** Each span from point a to point b drops 4·`sag_m`·t·(1 − t) below the straight line, sampled at ⌈|b − a| / `segment_m`⌉ equal steps of t. Contacts and rays use this polyline, thickened to `diameter_m`.
- **Wind grid.** Cells are 2 m, with row 0 north. Each wind-volume shape whose `wind_porosity` β is below 1 marks the cells its footprint (its horizontal convex hull) covers.
  - A cell that is a fraction f covered gets porosity 1 − f·(1 − β^(2 m / D)).
  - D is the footprint's mean width over all directions: perimeter / π, the diameter for a round crown. A path straight across the volume then has porosity about β.
  - Top and base are the shape's highest and lowest points above the terrain at the cell centre, never below 0.
  - Where volumes overlap, porosities multiply, and the highest top and the lowest base win. Wires are not wind obstacles.
- **Contacts.** There is at most one contact per (object, shape), and it carries that shape's material. A query sweeps the capsule from its previous pose. `Time` below 1 means the sweep went into or through the shape during the step and the capsule is now past it, so the contact is reported where the capsule first touched it.
- **Geometry.** `Geometry(object, shape, wireParam)` looks up a contact's shape: its kind, material, centre, axes, half extents, radius and height. For a wire it gives the span that holds the contact's wire parameter: its two attachment points, straight length, sag and diameter, and where on it the contact is, as the t of the sag curve. Physics takes a wire's compliance (T = w·L²/(8·sag)) and the fiber's bend radius over round shapes from it. The lookup is read-only and allocates nothing, and it returns false for terrain, visual-only objects and indices out of range.
- **Rays** hit the rendered terrain triangles only from above, and they ignore a shape they start inside. A terrain hit, like a miss, carries the material `Catalog.NoMaterial` (65535), for which `Material()` returns null. The ground's contact properties are its surface's: `Surface(hit.Surface)`.

### `launch_rails`

With legs off (manifesto §3), the drone lifts off from two parallel steel bars, so the fiber spool under the frame never touches the ground. The game places this asset at the chosen start point, with the start's yaw, before the flight. The pilot's only figure is "spaced about the drone's diameter, the frame rests across both and the spool hangs clear between them". Every dimension below is **derived, tunable until the drone model exists**.

Asset space: the origin is on the ground at the centre, the bars run along Z (the drone's fore-aft axis, nose toward −Z) and they are spaced along X.

| Assumption (drone class) | Value | Basis |
|---|---|---|
| Frame | X-frame, 450 mm motor-to-motor diagonal | 10" heavy fiber quad with 3-blade props. The motors sit at ±159 mm on each axis, 318 mm apart side to side, and the tip-to-tip span is 572 mm. |
| Motor pad | about 40 mm across, so the arm is clear up to about 205 mm from the centre | 31xx–42xx motors on heavy 10" builds |
| Spool | 150–200 mm across, its bottom 100–150 mm below the frame | Typical fiber spools on these drones |
| Props | above the arms (tractor) | The frame's bottom rests on the bars, so the prop disks sit about 50 mm or more above the bar tops |

| Derived | Value | Why |
|---|---|---|
| Profile | 25 × 25 mm square steel tube (a box) | A plain welded-frame profile. Its flat top seats a carbon arm. Material `steel`. |
| Spacing, centre to centre | 0.26 m | Each arm crosses a bar at 0.26/√2 = 184 mm from the centre: on the arm, about 20 mm inboard of the motor pad. The inner gap is 0.235 m, which clears a 200 mm spool by 17.5 mm per side and a 150 mm spool by 42.5 mm. That is about 0.8 of the frame's side-to-side motor spacing, the pilot's "about the drone's diameter". |
| Bar top height | 0.25 m | A spool hanging 150 mm below the frame still clears flat ground by 100 mm, which leaves room for micro-relief and short cover. |
| Length | 0.60 m | About the drone's tip-to-tip span. The arm crossings are at z = ±0.13 m, so the drone can sit up to about 0.15 m off centre fore and aft and still rest on both bars. |
| Supports | four 0.2 m legs under the bar ends, on two ground-level feet | The stand needs cross ties to stand up. They sit on the ground, not at bar height, so nothing behind the drone catches the fiber as it pays out between the bars. |
| `snag_hazard` | false | Smooth tube with no hooks or loose ends |
| Wind | no `wind_volume`, `wind_porosity` 0 | The collision boxes are the wind volume, and solid steel is opaque. The stand's openness comes from the small fraction of each 2 m wind cell that the thin tubes cover. |

The `steel` edge radius (1 mm) is sharper than a real cold-formed tube corner (about 2 × the 2 mm wall), so fiber contact over a bar corner errs toward breaking.

## Content hash

`WorldQuery.ContentHash` identifies the exact world data a flight ran on. The physics log records it, and a replay refuses a world whose hash differs. `WorldQuery.ContentHashOf` computes it from the files alone. It is FNV-1a 64 (offset basis `0xCBF29CE484222325`, prime `0x100000001B3`) over this stream:

1. The files, in this order: the package's `map.json`, `height.r16`, `surface.png`, `cover.png` and `objects.json`, then `game/maps/surfaces.json` and `game/assets/catalog.json`. Only these seven count. The folder names, the `.import` sidecars, any other file and the order in which the file system lists them do not.
2. Each file adds its bytes, then their count as an unsigned 64-bit little-endian integer.
3. In the four JSON files each CR LF pair counts as a single LF, and the count is taken after that, so a CRLF checkout hashes like an LF one. Every other change to the text (spacing, a byte-order mark, key order) changes the hash. The binary files count byte for byte.

A one-byte change always changes the hash, unless it makes or breaks a CR LF pair. Those changes, like any larger change, leave it the same with a chance of 2⁻⁶⁴. It prints as 16 lowercase hex digits. The rule in Python, for checking by hand:

```python
import struct
h = 0xCBF29CE484222325
for path in files:  # the seven files, in the order above
    data = open(path, "rb").read()
    if path.endswith(".json"):
        data = data.replace(b"\r\n", b"\n")
    for byte in data + struct.pack("<Q", len(data)):
        h = ((h ^ byte) * 0x100000001B3) % 2**64
print(f"{h:016x}")
```

The hash covers the data, not the code. `WorldQuery.QueryVersion`, an integer, identifies the query math: it is raised with every code change that alters any query result for the same data, and the golden file (`game/src/world/worldquery_golden.json`) records it. The physics log records it next to the content hash, and a replay refuses a log whose version differs, because the same world would give other results.

## Versioning

`format_version` is `major.minor`. A minor bump adds optional fields that older readers ignore. A major bump breaks compatibility. The validator and the loader reject a major they do not support, and name both versions.

## No real-world reference

Maps are composites inspired by the reference footage, never replicas of a real place. The validator rejects:

- any field whose name has a part (split at `_` and capitals) such as `lat`, `lon`, `lng`, `latitude`, `longitude`, `epsg`, `crs`, `srs`, `utm`, `mgrs`, `wgs84`, `geo`, `georef`, `geolocation`, `geojson` or `geotiff`, anywhere in the manifest, objects, surface table or catalog (so `origin_lat` and `geoRef` are rejected, `geometry` is not)
- any text value that contains a real place name from `tools/map/place_blocklist.txt` (matched at the start of a word, so case endings and adjectives count), a decimal coordinate pair or an MGRS grid reference. This is a safety net and can trip on a common noun such as «лиман»; rephrase the text.
- PNG metadata that could carry a location (`eXIf` chunks, and text chunks that match the rules above)
- GIS sidecar files in the package folder (`.pgw`, `.wld`, `.prj`, `.aux.xml`, `.tfw`, `.kml`, `.kmz`, `.gpx`, `.geojson`)
