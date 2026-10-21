# Design: p6-world-query-gallery

## Context
- **The first attempt** built and measured these queries in C# (`refs/godot/main`, `game/src/world/`): a worst-case step's batch took 34.7 µs median and 72.9 µs p99, against 60 µs (K§6.11, measured).
- **The probe's lessons** are in `docs/legacy/map-and-world/api-review.md` §2.4: load-bearing parts are spheres, rigid materials ignore the damping ratio, and surface parameters are read-only to physics.
- **What the assets carry:** each `.assetmeta.json` already holds boxes, spheres, capsules and cylinders with a material and a landable flag, wires as point lists with a diameter, holes, and gaps (`asset-pipeline`).

## Goals / Non-Goals
- **Goals:** the queries the first flights need, over the gallery, deterministic and inside budget; one station definition for scene and sim.
- **Non-Goals:** the proposal's.

## Decisions
1. **The interface first** (task 1.1, D-041):
   - **What:** `IWorldQuery` and its result structs in `Runtime/World/`, from K§10.4 and the first attempt's `WorldQuery.cs`, cut to this change's calls.
   - **The contract:** the physics engineer signs it off before `p6-flight-core`'s contact task (1.6) consumes it, and any later change to it is a superseding decision.
2. **Porting, never merging** (D-027):
   - **The sources:** `Contacts.cs`, `Shapes.cs`, `ContentHash.cs` and the query core are read from `refs/godot/main` (fetched read-only) and rewritten for Unity's frame (D-019: +Z north).
   - **The math:** `DetMath` only, IEEE basic operations.
   - **Credit:** each ported file says what it came from.
3. **The station data:**
   - **The file:** `maps/gallery-g1/stations.json` holds each station's number, letter, origin, its placements (asset, variant, position, yaw) and its viewpoints. It is what `GalleryStations.cs` holds today, moved.
   - **The two readers:** the Unity scene builder reads it to place prefabs, and the sim loader reads it to place each variant's `.assetmeta.json`. Scene and sim cannot drift apart.
4. **Shapes:**
   - **Primitives:** boxes, spheres, capsules and cylinders, as the metadata gives them. A cylinder is exact in the sim, where Unity's is a convex mesh for noclip only (`asset-pipeline`).
   - **Wires:** a capsule chain along the point list at the record's radius.
   - **Shells:** shell pieces (`twig`, `foliage`) return their material like any shape; the flight core's law gives them their compliance.
   - **The index:** a uniform 2 m grid over the gallery, holding object and shape ids in a fixed order, so iteration is deterministic.
5. **The ground:**
   - **The floor:** a plane at y = 0 with the floor's surface id.
   - **The patches:** S04's and E01's ground meshes are the asset's collision as ground: each patch's top primitives answer `SampleGround` where they lie.
   - **Holes:** inside an asset's `holes` band (`asset-pipeline`: a centre line and a band of half its width), `SampleGround` reports a hole, and the fillers answer through `StaticContacts` (WLD-16's rule, at gallery scale; the terrain-hole machinery is phase 3's).
6. **Identity:** FNV-1a over `stations.json` and every referenced `.assetmeta.json`, CRLF read as LF (K§10.4), plus `QueryVersion` 1. The flight log's header records both.
7. **Budgets:** `tools/worldbench --bench` runs the K§10.5 batch at three spots (inside V01_L's crown, beside H01_D, over E01's run) on AC with the power mode printed; `--golden` writes and checks the golden file.

## Risks / Trade-offs
- **Shell density:** V01's crowns hold up to 64 shell pieces per 2 m cube. The 2 m grid keeps a step's contact candidates to that cube's, inside budget (`p1-v01-locust` review-physics §A.3).
- **The cylinder:** Unity has no cylinder collider, so the sim's exact cylinder and noclip's convex mesh differ by the mesh's facets. Only the sim's matters for flight.

## Migration Plan
- G1's scene builder reads the data file instead of the C# table. The scene's content must not change: the gallery's EditMode scene test passes unchanged.

## Open Questions
- None.
