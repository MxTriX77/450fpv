# Design

## Context

- Nothing exists in `game/maps/` or `game/src/world/` yet. `bootstrap-godot-project` supplies the sandbox, noclip camera, F3 overlay and `--scene` argument, and this change builds on it.
- Physics (a later change) runs a custom flight model at ≥ 1 kHz in C# (D-002). Contacts at 4 legs plus the fiber mean thousands of world queries per second, and every one has to be reproducible from the physics log.
- The reference notes (§6–7) define 9 surfaces and a micro-detail table. The pilot's open terrain questions affect content, not the format.

## Goals / Non-Goals

**Goals:**
- One authored source of truth, readable without Godot. Python and the validator can read it, and it diffs sensibly.
- Physical detail down to the straw without storing straws.
- Perf headroom for an 8 km map on a mid-range laptop GPU.

**Non-Goals:**
- Runtime terrain editing or deformation (legs leave no persistent marks for now).
- Networked or multi-map streaming.

## Decisions

- **Raw `.r16` for heights, PNG for categorical and density layers.**
  - Godot's PNG loader drops 16-bit precision. Raw uint16 is trivial to read in both C# and Python, and Terrain3D imports it too.
  - 1 cm steps over a 655 m range is plenty for Ukrainian relief.
  - The surface and cover layers compress very well as PNG.
  - Alternatives considered: EXR (needs extra libraries in Python) and Godot `.res` (not engine-independent, doesn't diff).
- **Default resolutions:** 1 m heightfield, 0.5 m surface and cover cells. Features smaller than a cell (holes of 0.1–3 m, roughness) are procedural per surface, not rasterised. A 4 km map is then 33 MB of heights and a few MB of PNGs, in LFS.
- **Micro-detail from integer hashes.**
  - Space is divided into 0.25 m cells.
  - For each cell, a `hash(cellX, cellZ, seed, kind)` PCG-style integer hash drives how many elements there are and their parameters, scaled by the cover density.
  - Using integers only until the final conversion to float makes it bit-identical across runs and machines. Elements never straddle cells, so query order doesn't matter.
  - Physics requests a radius of about 2 m. The renderer uses the same generator for a MultiMesh ring around the camera, with cheap density-only far instancing beyond it.
  - Rejected alternative: storing instances, which would mean millions per km² and break the size budget.
- **Ground = terrain + micro-relief + pitfalls,** all procedural per surface. The rendered terrain stays macro. Under grass cover, the difference hides the way it does in reality: the pilot's "unexpected pitfall".
- **Wind-obstacle grid derived at load time** (2 m) from the heightfield and object bounds × catalog porosity. Nothing derived is committed, so it can never go stale.
- **Terrain renderer: spike first** (task 1.1) on a synthetic 4 km map.
  - Candidate A: Terrain3D, an MIT GDExtension with a clipmap LOD and instancer. The risk is compatibility with 4.7.2 and a C++ binary dependency.
  - Candidate B: our own chunked mesh with LODs plus `HeightMapShape3D`.
  - Pick whichever meets the map-loading budget with less code. Record the choice as D-009.
  - Physics never queries the renderer: world-query reads its own C# copy of the layers, so the choice doesn't affect the specs.
- **Surface parameters start from typical soil-mechanics values:** a Winkler bearing modulus per soil class, friction 0.5–0.8, and the porosity values from notes §7. The physics engineer reviews the fields and units before they are frozen (task 2.2) and tunes the values later during flight tests.
- **Wires are polylines with a sag,** built into thin capsule chains for collision. They carry `snag_hazard=true` by default, because the pilot flagged them as the invisible danger.
- **Validator in Python, stdlib only.** It uses `zlib` for PNG decoding, so QA and agents can run it without Godot or Blender. The C# loader applies the same checks at load time.

## Risks / Trade-offs

- [Terrain3D isn't compatible with 4.7.2, or C# interop is awkward] → Candidate B is the fallback. The spike decides it on measured numbers.
- [Hash-based micro-detail looks too uniform] → Per-surface clustering (a density modulated by low-frequency hash noise). The world artist tunes the look with the user during M1 reviews.
- [Physics needs a field that isn't in the surface table] → `format_version` minor bumps allow added optional fields. A major bump is needed only for breaking changes.
- [1 m heights are too coarse for trenches and craters] → Author those features at 1 m (they are 1–4 m wide). The sub-metre shape comes from micro-relief. Re-evaluate on the M1 patches, and the format allows 0.5 m.
