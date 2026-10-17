# Physics review: asset metadata schema v1 and K01

**Reviewer:** physics-engineer · **Task:** 1.4 · **Branch:** `unity/art/setup-asset-pipeline` · **Tip:** `0e2e6a3`
**Against:** AST-04, AST-05 (requirements §5), D-005, D-007, D-019, knowledge §7 (materials) and §10.6 (the two limits of the old format), design decisions 5–8, spec "Asset metadata contract"
**Read:** `tools/blender/lib/meta.py`, `frame.py`, `geometry.py`, `export.py`, `fbxread.py`, `generators/debris_brick.py`, `K01_whole.assetmeta.json`, `K01_whole.fbx` (parsed as Unity reads it), `AssetPhysicsMeta.cs`, `CatalogImporter.cs`, `Json.cs`, `CatalogImporterTests.cs`, the old catalog on `godot-final` (`game/assets/catalog.json`, `game/maps/README.md`) for the conventions the sim core already relied on, and `docs/legacy/map-and-world/{surfaces-review,api-review}.md`

I am the consumer of this data: the 1 kHz core reads it for contacts (feet, arms, body, coil, prop discs, ≤ 32 fiber nodes), for the fiber's bend and snag model, for the wind grid and wakes, for gap jets and the simulated pilot, and for the log's forensics. Everything below is judged against what those consumers will need, not against Unity.

## Summary

The schema's structure is right. It carries everything the sim core needs, in a form the engine-free core can read without Unity: plain numbers, metres, Unity's frame (D-019), quaternions `[x, y, z, w]`, deterministic text. It fixes both limits of the old format (§10.6): gaps are planar polygons in any orientation, and porosity sits on each wind-volume part. K01's metadata is correct: the box follows the chamfered mesh within 3.3 mm (measured), material `masonry`, not landable, empty arrays written.

What is missing is not fields but **conventions**: the schema does not say which way a capsule points, what its `height` includes, what a gap's `normal` and `narrowest_m` mean, where the gap polygon lies in a thick wall, what an empty `wind_volumes` means, what a snag `kind` may be, or which material ids exist. K01 exercises none of these, so nothing catches a phase-1 generator that guesses. The architecture rule (final from the first line) says settle them now, while it costs a paragraph and a few validator lines. One further item is a record gap: design decision 5 promised that task 1.3 verifies the Unity X-sign assumption with an asymmetric mesh, and no result is recorded.

**Verdict: CHANGES REQUESTED**, with three required changes (§4). None of them changes K01's files or the schema number.

## 1. Does the schema carry what the sim core needs?

| Consumer (phase) | Needs | Schema | Verdict |
|---|---|---|---|
| Contacts (6): sphere and swept-capsule queries against every shape (knowledge §10.4, D-005) | convex analytic primitives with pose, dimensions and a material id per shape | `collision[]`: box, sphere, capsule, cylinder; `center`, `size` or `radius`/`height`, `rotation`; `material` per shape | **present.** All four kinds are convex, so the sweep stays analytic and allocation-free; concave objects are unions. There is no mesh primitive, which is right |
| Fiber bend and cut risk (6, PHY-19): bend radius over an edge, `edge_radius_m` per material; snags on battens, rebar, branches, torn metal | material id per shape (gives `edge_radius_m`), the shape's own radius for round members, and the snag features | `material` on each shape; `radius` on capsule/cylinder/sphere; `snag[]` with `kind`, `points`, `radius_m` | **present.** The `radius_m` on a snag feature is what the capstan and stress formulas take (`E·r/R`) |
| Wire strikes by diameter (PHY-22) inside an asset (a fence's wire, a downed line on a pole) | thin capsules with their real radius | `capsule` with `radius` | **present.** The sweep guarantee ("a 15 mm sphere swept 60 mm across a 5 mm wire is always caught") applies to capsules; map-level wires stay map objects (D-007) |
| Wind grid and wakes (7): top, base and porosity per 2 m cell, derived per obstacle part (§10.3) | wind volumes as primitives, each with its own porosity, base height from the shape itself | `wind_volumes[]` with `porosity` per part | **present.** A house with solid walls and a stripped roof is now two parts with two porosities (D-007); a crown's base comes from the sphere's centre and radius |
| Gap jets (7) and the simulated pilot (§8.5), `GapsNear` (§10.4), PR-5, PHY-23 | planar polygons in any orientation, measured from the real clear space, the narrowest width | `gaps[]`: `id`, `polygon` (3+ points), `normal`, `narrowest_m` | **present**, conventions missing (§2) |
| Landing detection, start-point checks, the post-flight summary | which surfaces the drone may come to rest on | `landable` per collision shape | **present.** The contact law never reads it (a foot on a roof is a contact with `sheet_metal`); it is for the detector and the setup validator |
| Replay and forensics (6): the log names (object, shape index, material) per contact and must replay bit-exactly from the same data | stable shape order, stable identity of the file | ordered arrays; `id`, `variant`, `params_sha256`; a deterministic writer (6 decimals, LF, fixed key order) | **present.** The phase-3 package must hash the metadata bytes, not only `params_sha256` (§5, P3-1), because a generator-code change alters the output under the same params hash |
| Engine-free reading (D-006) | plain JSON numbers, no Unity types, no conversion at the boundary | yes; `frame: "unity-lh-yup"`, `units: "m"` are asserted by the writer and by the importer | **present** |

Deliberately absent, and I agree with each: mass or density (the world is static, D-005; movable debris would be a schema 2), restitution (the drone's own compliance sets the bounce, knowledge §7.4), LOD data for physics (render-only), per-shape visual data.

## 2. Ambiguities a phase-1 generator or a phase-3/6/7 consumer would have to guess

Each line says what is undefined today, what I need, and the convention I ask for (R2 in §4).

1. **Capsule and cylinder axis.** `meta.py` says only `(center, radius, height, rotation)`. `AssetPhysicsMeta.cs` says "height along the rotated Y axis" and the importer sets `capsule.direction = 1`. The old format said "along asset +Y". **Convention:** the axis is local +Y, then `rotation` is applied; the shape's world-space pose is `p = R(q)·p_local + center`.
2. **`height` of a capsule.** Caps included or not? Unity's `CapsuleCollider.height` and the old format both mean the total end-to-end length. The importer passes the number straight to Unity, so the de-facto convention is "total, caps included", but no text says so, and a generator that writes the cylinder-part length produces capsules 2r too long in Unity and in the sim. **Convention:** `height` is the total length, caps included; the validator requires `height ≥ 2·radius` for capsules (Unity silently clamps a shorter one to a sphere, and the sim would not).
3. **Rotation.** `[x, y, z, w]` is stated (decision 6) and the importer reads it in that order (`Json.Quat`), but nothing requires a unit quaternion, and a non-unit one is normalised by Unity and would be read raw by the sim: two different shapes from one file. **Convention:** unit quaternion, Unity's (Hamilton) convention, applied about the shape's own `center`; validator: `| |q| − 1 | ≤ 1e-6`. Boxes may be rotated freely (a tilted slab, a leaning plank); the importer already handles that through the child transform.
4. **Gap polygon winding and the "through" side.** An opening has no inside: the drone flies through it either way, a jet follows the wind, and the simulated pilot approaches from wherever it is. So `normal`'s sign carries no meaning and the winding carries none either. What must hold is geometry: the polygon is planar and `normal` is its unit plane normal. **Convention:** `normal` is a unit vector perpendicular to the polygon's plane, sign free; winding free; every vertex within 10 mm of the plane (AST-05 gives ±2 cm around openings); the validator checks unit length, perpendicularity (every edge · normal within 1e-3 of zero, scaled by edge length) and planarity. Consumers must never derive a side from the winding. (I considered requiring a winding tied to `normal`; in a left-handed frame the "counter-clockwise" rule flips sign against the textbook formula, and it would buy nothing.)
5. **Where the polygon lies in a thick wall.** A window in a 0.38 m brick wall, with jagged courses, has a different clear cross-section at every depth. The drone must pass all of them. **Convention:** the polygon is the throat: the intersection of the clear cross-sections over the opening's depth, placed at the depth where that throat lies (the wall's mid-plane for a clean opening). Concave polygons are allowed (jagged edges); self-intersecting ones are not. An optional `depth_m` can come with schema 2 if the wind jet or the pilot needs it (P3-6).
6. **`narrowest_m`.** "Narrowest width" of an irregular hole is ambiguous (minimum caliper width, shortest edge-to-edge distance, inscribed circle). The acceptance probe is a drone-sized capsule, 0.574 m nose-first (§5.4). **Convention:** `narrowest_m` is the diameter of the largest circle inscribed in the polygon, in its plane: the widest round body that passes. For a rectangle it is the shorter side. Validator now: `0 < narrowest_m ≤` the polygon's largest extent; the exact inscribed-circle check belongs to the phase-1 geometry audit (P1-2).
7. **Empty `wind_volumes`.** The old format fell back to the collision shapes with the asset's single porosity. With porosity per part there is no sensible default porosity, and an implicit fallback would make a brick a wind obstacle. **Convention:** an empty array means the asset does not block wind; every obstacle authors its wind volumes explicitly. K01's `[]` is right under that rule.
8. **Snag `kind`.** Free string today, and `points` may hold one point. The fiber model needs two behaviours: a line that bends over a sharp edge, and a free end that catches a leg or the line and lets it slide off the tip. **Convention:** closed list `edge` (a polyline of ≥ 2 points the fiber bends over; `radius_m` is its edge radius and overrides the material's `edge_radius_m`) and `hook` (a free-ended member listed from base to tip, ≥ 2 points; `radius_m` is half its thickness). The validator checks the kind and `≥ 2` points. Hold or release forces are not authored: physics derives them from the member's material and radius (as hooks on straw are derived from the surface table), so no number is asked of the artist.
9. **Material id vocabulary.** `material` and `materials[].physics` are free strings; the validator accepts anything. AST-05 names steel, sheet_metal, timber, masonry, concrete, glass, cable, foliage "and so on": open-ended over time, but closed at any moment, and the sim rejects an id it has no parameters for. **Convention:** a shared physics-material table, physics-owned, is the single closed list (R3 in §4).
10. **`landable` semantics.** A flag per shape, but a rotated box has one upward face. **Convention:** a landable shape's upward-facing surface (normal·up > cos 30°, assumed) is a landing surface; the flag feeds the landing detector, the start-point validator and the post-flight summary, never the contact law.
11. **Convexity.** Implicit. **Convention:** state that every primitive is convex and that concave objects are unions of primitives; no mesh primitive, by design (analytic sweeps, zero allocation).

Not ambiguous, confirmed: the frame (asset space is Unity's world frame placed at the prefab origin; +X east, +Y up, +Z north for an object at yaw 0), units, `bounds` as the union of LOD bounds, the deterministic writer, and that positions are relative to the prefab origin.

## 3. K01 against AST-04, AST-05 and D-019

Measured with a probe over the generator and the committed params (seed 1), and over the committed FBX parsed as Unity reads it (`lib.fbxread.unity_bounds`, X negated, centimetres to metres). The probe is in my scratchpad, not committed; its output is in §6.

- **Dimensions (AST-04):** bounds `[-0.125, 0, -0.06]…[0.125, 0.065, 0.06]` in the metadata, in the generator, and in both FBX meshes: 0.250 × 0.065 × 0.120 m exactly. Long edge along +X, 65 mm height along +Y, lying on y = 0, as the "Frame is Unity's" scenario asks.
- **Collision follows the visual (AST-05, spec scenario ±5 mm):** one box, `center [0, 0.0325, 0]`, `size [0.25, 0.065, 0.12]`, identity rotation. The eight corner chamfers drawn by seed 1 are 2.13–2.85 mm (the design's assumed 2–3 mm). **Box → mesh: 3.288 mm** at a box corner, which is the analytic `2·w_max/√3` of a corner triangle; **mesh → box: 1.898 mm** at a corner triangle's centroid (`2·w/3`); **no mesh vertex lies outside the box (0.000 mm)**. The two-sided deviation is 3.3 mm: inside the scenario's ±5 mm and far inside AST-05's ±5 cm. The box's sharp arris plus `masonry`'s `edge_radius_m` of 2 mm is consistent with the real 2–3 mm chamfer for the fiber model.
- **Material:** `masonry` on the shape and on the `brick` slot: right for fired clay (knowledge §7.4). **Landable:** `false`, right: a 250 × 120 mm brick cannot carry a 450 mm airframe. **Empty arrays** `gaps`, `wind_volumes`, `snag` are present, not omitted; a brick blocks no wind and has no snag (a broken brick variant would carry an `edge`).
- **D-019:** the metadata is in Unity's frame with no conversion downstream; the importer places the collider as the metadata says (`CatalogImporterTests.BoxCollider_MatchesTheMetadataBox`, ±1 mm). **One gap in the record:** decision 5 says the file-to-Unity map negates X "an assumption until task 1.3 sees it in Unity", to be checked "with an asymmetric throwaway mesh". Task 1.3 is ticked, the test checks only the symmetric K01, and neither `design.md` nor any commit records the asymmetric result. K01 cannot expose a wrong sign, but the first house with a door at +X would: its collision and gaps would be mirrored against its visual. That is R1.

## 4. `AssetPhysicsMeta.cs` against the JSON

Field by field, the component mirrors schema 1 with no field dropped and no semantic change: `schema`, `id`, `variant`, `generator`, `paramsSha256`, `units`, `frame`, `bounds`, `lods[]` (mesh, tris, switchDistanceM), `cullDistanceM`, `materials[]` (slot, visual, physics), `collision[]` (kind, center, size, radius, height, rotation, material, landable), `gaps[]` (id, polygon, normal, narrowestM), `windVolumes[]` (+ porosity), `snag[]` (kind, points, radiusM), plus `sourceJson`, the `.assetmeta.json` itself as a `TextAsset`. The importer refuses any schema, units or frame other than `1`, `m`, `unity-lh-yup`.

Lossy only at a level nothing physical reads: numbers go through `float` (`Json.Num` casts the parsed double), and `bounds` is re-expressed as centre and extents. For coordinates beyond about 10 m that is a 1e-6 m rounding, the same as the file's own 6 decimals. Unity-only types (`Vector3`, `Quaternion`, `Bounds`, `TextAsset`, `MonoBehaviour`) are fine because the component exists for Unity consumers (noclip colliders, the LOD group, the inspector). **The sim core will never read this component**: it takes the JSON text, through the adapter from `sourceJson.text` in Unity, and from the file on disk headless, and parses it as doubles. The phase-3 package should carry the metadata so the core parses one format (P3-2).

Two small notes, no change needed now: the `Shape` doc comment should say "height: total length, caps included" once R2 settles it; and the `[Range(0, 1)]` on `porosity` is an inspector hint only (the writer's validator is the real check).

## 5. Required changes (this change) and recommendations

### Required, before QA 2.1 closes

**R1. Record the Unity X-sign check (D-019).** Run the asymmetric throwaway build that decision 5 promised (a mesh with one feature at asset +X, imported, its position read in Unity against the metadata) and write the result into design decision 5. If the sign is wrong, `lib/frame.py` is the one-line fix and K01 rebuilds unchanged. Owner: asset-artist, or QA as part of 2.1. Evidence: the Unity log line or an EditMode assertion.

**R2. Write the conventions into design decision 6 and the spec requirement "Asset metadata contract", and enforce the cheap ones in `meta.validate`** (items 1–8, 10 and 11 of §2). Proposed text for the design:

> Shapes are convex; concave objects are unions. `rotation` is a unit quaternion `[x, y, z, w]` (Unity's convention) applied about the shape's `center`: `p = R(q)·p_local + center`. Capsules and cylinders run along local +Y before rotation; `height` is the total length, caps included, so a capsule needs `height ≥ 2·radius`. A gap `polygon` is the throat of the opening (the intersection of its clear cross-sections over its depth), planar within 10 mm, simple, concave allowed; `normal` is its unit plane normal with free sign, and winding carries no meaning; `narrowest_m` is the diameter of the largest inscribed circle. An empty `wind_volumes` means the asset does not block wind. `snag[].kind` is `edge` (a polyline the fiber bends over; `radius_m` overrides the material's edge radius) or `hook` (a free-ended member from base to tip; `radius_m` is half its thickness), each with at least two points. A landable shape's upward-facing surface is a landing surface for the landing detector and the setup validator; the contact law ignores the flag.

Validator additions: unit quaternion (1e-6), capsule `height ≥ 2·radius`, gap normal unit and perpendicular, vertices within 10 mm of the plane, `0 < narrowest_m ≤` the polygon's extent, snag kind in the list with ≥ 2 points. One test per rule in `test_meta.py`. K01's output is unchanged, so `test_committed_metadata_matches_committed_params` keeps passing.

**R3. A closed physics-material list, physics-owned.** Add `assets-src/physics_materials.json` (schema 1): id → `friction_static`, `friction_kinetic`, optional `stiffness_n_per_m` (absent = rigid) with `damping_ratio`, and `edge_radius_m`. `meta.validate` rejects a `material` or `materials[].physics` id that is not in it. The phase-3 compiler copies the table into the map package and hashes it (the old content hash did the same, knowledge §10.4). Starting rows, every value labelled; the ids are what this change needs, the values are mine to tune in phases 3 and 6:

| id | μs / μk | stiffness N/m | ζ | edge radius m | label and rationale |
|---|---|---|---|---|---|
| steel | 0.35 / 0.28 | rigid | — | 0.001 | assumed, carried from surfaces-review §5 (polymer on dry mild steel, rolled or sawn edge) |
| sheet_metal | 0.30 / 0.25 | 2e4 | 0.03 | 0.0003 | assumed, carried: 0.5 mm corrugated sheet over a 1 m purlin span; its half-thickness edge cuts the fiber |
| timber | 0.45 / 0.35 | rigid | — | 0.002 | assumed, carried; a batten asset may override with ≈ 1e5 |
| masonry | 0.65 / 0.55 | rigid | — | 0.002 | assumed, carried: brick, block, render; a worn arris |
| concrete | 0.60 / 0.50 | rigid | — | 0.003 | assumed: plastic feet on cast concrete sit in the published 0.5–0.7 band; a cast arris. Broken edges and rebar are `edge` snags with their own radius |
| glass | 0.40 / 0.30 | rigid | — | 0.0001 | assumed: plastic on glass; a broken pane edge is a blade. Breakability is a later material property (P3-6) |
| cable | 0.30 / 0.25 | rigid locally | — | own radius | assumed, carried; span compliance `T = w·L²/(8·sag)` comes from the wire geometry |
| foliage | 0.50 / 0.40 | 2e2 | 0.3 | own radius | derived: a 20 mm green branch at 1 m, E ≈ 10 GPa (published 8–12 GPa for green wood), `I = πd⁴/64 = 7.85e-9 m⁴`, `k = 3EI/L³ ≈ 236 N/m`; ζ assumed for a leafy branch |

New ids are added to this file with physics sign-off (team.md "Catalog physics metadata"), never invented in a generator.

### Recommendations

**Phase 1 (asset pipeline, next assets):**
- **P1-1.** `CatalogImporter.EnsureColliders` throws on `cylinder`. The sim does not need Unity colliders at all (PhysX is noclip-only, D-005), so for noclip a convex `MeshCollider` from a generated cylinder mesh, or a capsule of the same radius and height, is enough. Resolve before the first pole or trunk asset.
- **P1-2.** Automate the AST-05 geometry audit in `build.py` the way my probe did it: the two-sided deviation between each collision primitive and the LOD0 mesh it stands for, failing beyond ±5 cm (±2 cm within 0.3 m of a gap polygon or a landable face), and the inscribed-circle check of `narrowest_m`. The review sheet still gets the overlay; the number stops regressions.
- **P1-3.** Thin members (wire, rebar, battens, branches) are capsules with their real radius; that is what the sweep guarantee and the strike-by-diameter model rely on. Say so in the generator guidelines.

**Phase 3 (map format and world query):**
- **P3-1.** The package's content hash covers the bytes of every referenced `.assetmeta.json` (or the compiler copies the metadata into the package). `params_sha256` alone does not pin the output.
- **P3-2.** The map compiler, not the sim core, reads the `.assetmeta.json` files and emits the package's catalog; the core parses one format, so the asset pipeline's file layout never leaks into the engine-free package. `QueryVersion` is bumped when that parsing changes.
- **P3-3.** The wind grid is derived per wind-volume part with that part's porosity (D-007); the house-with-stripped-roof case is the test.
- **P3-4.** `GapsNear` returns polygons (an offset and count into a shared vertex table, plus `normal`, `narrowest_m`, object and gap id). A vertex cap of 64 per gap in the validator lets the core size fixed buffers.
- **P3-5.** Accept `surface:<id>` as a shape material for cut soil (trench walls, cellar fillers), as the old X-7 did, checked against the surface table.
- **P3-6.** Schema 2 candidates, only if a consumer asks: an optional per-shape `edge_radius_m` override, an optional gap `depth_m`, and a `break_force_n` on glass and tile in the material table (X-8).

**Phase 6 (flight model), obligations on my side:** the core's quaternion type documents the same `[x, y, z, w]` Hamilton convention once (D-019); the loader allocates at load only; contacts log (object, shape index, material id) so QA can name the primitive in a crash.

## 6. Evidence

```
$ git -C <worktree> switch unity/art/setup-asset-pipeline && git log --oneline -1
0e2e6a3 docs(env): keep unity hub running for batchmode licensing

$ git check-attr -a -- unity/Assets/_Project/Art/Models/debris/K01/K01_whole.fbx
... filter: lfs        (the file is the 18,556-byte binary, "Kaydara FBX Binary", not a pointer)

$ python -I <scratchpad>/k01_deviation.py        # generator with the committed params; committed FBX via lib.fbxread
chamfer widths per corner (mm): [2.134, 2.255, 2.449, 2.495, 2.652, 2.764, 2.789, 2.847]
box -> mesh max deviation: 3.288 mm at (-0.125, 0.0, 0.06)
mesh -> box max deviation: 1.898 mm at (-0.1231, 0.0019, 0.0581)
any mesh vertex outside the box by: 0.000000 mm
analytic corner bound 2*w_max/sqrt(3) = 3.288 mm, edge bound w_max/sqrt(2) = 2.013 mm
K01_whole_LOD0 unity bounds min [-0.125, 0.0, -0.06] max [0.125, 0.065, 0.06]
K01_whole_LOD1 unity bounds min [-0.125, 0.0, -0.06] max [0.125, 0.065, 0.06]
metadata bounds {'min': [-0.125, 0.0, -0.06], 'max': [0.125, 0.065, 0.06]} collision [0.0, 0.0325, 0.0] [0.25, 0.065, 0.12]

$ python -m pytest tools/blender -q
...........................                                              [100%]
27 passed in 13.06s
```

No Unity run was made for this review (the editor may hold the project, and nothing here needed it); the Unity-side facts come from reading `CatalogImporter.cs`, `Json.cs` and `CatalogImporterTests.cs`.

Verdict: CHANGES REQUESTED
