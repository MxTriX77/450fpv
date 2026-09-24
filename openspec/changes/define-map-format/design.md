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
- **The renderer is our own quadtree heightmap (D-009, spike result).** It meets the 4 km budget about 4× over, with no dependency. Terrain3D failed the compatibility gate (no stated 4.7 support). Terrain height queries use the renderer's triangulation, not bilinear, so physics, Jolt and the pixels agree. The gap between the two can reach centimetres on crater rims.
- **Physics contacts are pure C# over catalog primitives (D-010).** Static contacts and raycasts run against box, sphere, capsule and cylinder shapes and wire polylines in our own code, not Godot or Jolt queries. Godot's C# query calls allocate on every call, can't run on a physics thread, and aren't available to a headless replay. Custom code also lets us control cross-machine determinism. It costs about 300 lines of closest-point code. Jolt stays for render-side and gameplay collision.
- **Replay-grade world.** A 64-bit content hash over the package, `surfaces.json` and `catalog.json` goes into every physics log. The world functions use only correctly rounded IEEE operations. The query path is reentrant and free of scene-tree access, so physics can run on its own thread and QA can replay flights headless.
- **Physics review folded in** (`surfaces-review.md`, Physics Engineer):
  - plastic sink via an unload ratio
  - a compressible mat layer per cover
  - directional ridges for tilled fields
  - hook release forces
  - object materials and wind volumes
  - porosity dropped from soil, because the contact model never uses it
  - four value corrections
- **Surface parameters start from typical soil-mechanics values:** a Winkler bearing modulus per soil class, friction 0.5–0.8, and the porosity values from notes §7. The physics engineer reviews the fields and units before they are frozen (task 2.2) and tunes the values later during flight tests.
- **Wires are polylines with a sag,** built into thin capsule chains for collision. They carry `snag_hazard=true` by default, because the pilot flagged them as the invisible danger.
- **Validator in Python, stdlib only.** It uses `zlib` for PNG decoding, so QA and agents can run it without Godot or Blender. The C# loader applies the same checks at load time.

## Risks / Trade-offs

- [Terrain3D isn't compatible with 4.7.2, or C# interop is awkward] → Candidate B is the fallback. The spike decides it on measured numbers.
- [Hash-based micro-detail looks too uniform] → Per-surface clustering (a density modulated by low-frequency hash noise). The world artist tunes the look with the user during M1 reviews.
- [Until wind volumes and tree-crown snag volumes land, tree belts are invisible to wind and contacts] → Wind volumes land in this change (4.1). Crown snag volumes, debris chunks and trench holes are deferred to `build-m1-terrain-patches` (review §8, X-1 to X-8).
- [Launch-rail geometry is derived, not measured] → From the pilot's "about the drone's diameter" and a 10" X-frame (450 mm wheelbase): two 25 mm square steel tubes, 0.26 m apart centre to centre, 0.25 m high and 0.6 m long. The arms rest on them, and the spool clears. Everything is tunable until the drone model exists. The steel edge radius (1 mm) is sharper than a real tube corner, so fiber-break risk errs on the high side; physics tunes it.
- [Swept contacts need time of impact] → `StaticContact.Time` was added in 3.4 and accepted into the spec. The physics engineer confirms the response to Time < 1 (for example, rewinding the step) in 3.7.
- [Contact and ray timings in a Debug build are above the 1 µs per-query share: static capsule 3.6 µs, swept 6.9 µs, ray 1.6 µs] → Release benchmark and optimisation in 3.6. Box and cylinder closest points use a 40-step golden-section search, the first candidate to replace with closed form.
- [The wind grid is 12 B/cell: 50 MB at 4 km, 200 MB at 8 km] → Acceptable for the 4 km world. Pack it if an 8 km map is ever built.
- [The catalog has no real sheet-metal roof; the roof scenario uses a test-only asset] → A corrugated-roof asset is part of `build-uat1-parts`.
- [Benchmarks depend on the laptop's power state: 3.3 GHz on AC, 1.5–2.4 GHz on battery] → Budgets are judged on AC. Each benchmark line prints its clock and power source. After 3.6, ground takes 9.55 ms on AC (thin margin) and micro-detail about 0.18–0.20 ms on meadow. Straw and litter surfaces take about 0.6–0.9 ms, so physics prefetches micro-detail off the step's critical path.
- [First query timings exceed budget: ground 28–36 ms per 100k (budget 10), MicroDetailNear 1.7–2.1 ms (budget 0.25)] → Optimise in 3.6 before the golden file (3.5), because a hash change alters every output.
- [Physics needs a field that isn't in the surface table] → `format_version` minor bumps allow added optional fields. A major bump is needed only for breaking changes.
- [Map sides that aren't multiples of 256 m fall back to slower mesh collision] → The format requires multiples of 256 m.
- [Trenches 0.6–1.0 m wide can't be made from a 1 m, or even 0.5 m, heightfield] → Terrain holes plus a trench mesh asset, in `build-m1-terrain-patches`.
- [1 m heights are too coarse for trenches and craters] → Author those features at 1 m (they are 1–4 m wide). The sub-metre shape comes from micro-relief. Re-evaluate on the M1 patches, and the format allows 0.5 m.
