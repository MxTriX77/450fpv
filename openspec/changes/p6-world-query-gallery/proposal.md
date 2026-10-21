# Proposal: p6-world-query-gallery

**Owner:** world-builder · **Reviewers:** physics-engineer (signs off the query API with a contact probe, as `docs/team.md` gives), qa-engineer
**Requirements:** WLD-11 (the queries G7 needs), WLD-16 (holes, at gallery scale), PHB-05, PLT-04 (the world-query share), AST-05 (the collision the queries return) · **Decisions:** D-006, D-019, D-027 (Godot code is ported, never merged), D-041, D-042 · **VISION:** §2 (the drone meets the real things: trees, wires, a car, a trench, a house)
**Source:** `docs/knowledge.md` §10.4 (the queries, their identity and budgets); `docs/legacy/map-and-world/world-query-spec.md` and `api-review.md` (the first attempt's spec and the probe's lessons); the first attempt's C# at `refs/godot/main` (`game/src/world/{WorldQuery,Contacts,Shapes,ContentHash,DetMath}.cs`), read and ported.

## Why
D-042 makes the G1 gallery the test range for the first flights. The flight core asks the world what is under a foot and what a swept arm meets. This change gives it those answers over the gallery's stations, from each asset's own metadata, so the first flights hit the trees, the wires, the car, the trench and the house exactly where they stand.

## What Changes
- **The query interface,** fixed first (D-041): `IWorldQuery` in the sim package, with `SampleGround`, `StaticContacts` (swept spheres and capsules with `Time` and the latch), `Raycast`, `Geometry`, runtime objects with reset, the content hash and `QueryVersion`. It's allocation-free, reentrant and callable from the physics thread.
- **The gallery as a world, from one definition:**
  - G1's station table becomes a data file that both Unity's scene builder and the sim's loader read.
  - The loader places every asset's collision primitives, materials, wires and holes from its `.assetmeta.json` at its pose.
- **The ground:**
  - the floor as a plane with its surface id
  - the S04 and E01 patches by their own ground
  - inside an asset's `holes` band, no plane ground: the asset's fillers are the ground (WLD-16, at gallery scale)
- **Determinism and budgets:** a golden file in Debug and Release, batched results identical to single, a concurrency test, and the K§10.4 budgets in `tools/worldbench`.

## Capabilities

### New Capabilities
- `world-query`: the engine-free queries the physics reads (the G7 subset of WLD-11), their identity, determinism and budgets, and the gallery world built from its station data and the assets' metadata

### Modified Capabilities
- `look-lock-gallery` is not yet in `openspec/specs/` (G1 is not archived): its station table becoming data is recorded in this change's design, and G1's archive syncs it.

## Non-goals
- **WLD-11's remaining queries:** `MicroDetailNear` (straws and twigs as hooks, phase 7a and the micro-detail generator), `WindGrid` and wakes (`p7c-wind`), `GapsNear`.
- **The map format, compiler, terrain and validator** (phase 3, postponed by D-042).
- **The contact laws themselves:** soil, compliant shells, wires' compliance. Those are the physics engineer's (`p6-flight-core` and phase 7). This change returns what is touched and with what material.

## Impact
- **New code:** `unity/Packages/com.fpv450.sim/Runtime/World/`, and `tools/worldbench` cases (golden, bench).
- **Changed:** the G1 gallery's station table (`GalleryStations.cs`) becomes a data file under `maps/gallery-g1/`, read by the scene builder and the loader.
- **Consumers:** `p6-flight-core` (contacts), `p6-input-app` (the flight session loads the gallery world), `p7b-airframe` (ground-effect rays).
