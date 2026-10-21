# Proposal: p6-test-range

**Owner:** world-builder (the map, its scene and build) · **Also building:** asset-artist (the Godot stand-ins, task 1.1) · **Reviewers:** physics-engineer (the stand-ins' metadata the sim reads), qa-engineer; **the pilot at G4 and G7**
**Requirements:** PRD-07 (noclip), WLD-04 (a belt with bushes among the trees), WLD-05 (a yard), WLD-08 (start points, the stand with legs off), REAL-05 (collision follows the visual; every opening is a gap the sim knows), AST-12 (a review sheet per asset), PLT-02, PLT-06, PLT-07 · **Decisions:** D-027 (port, never merge), D-040 (stand-ins), D-041, D-042, **D-044** · **VISION:** §2 (points 1–3: lifting off and landing on real ground, flying among dense trees and through narrow openings), §3 (one map, light on the machine)
**Source:** the pilot, 2026-10-22 (D-044); `refs/godot/main` at `8340138`: `tools/map/make_uat1_gallery.py` (`ADOBE_YARD`, the belt runs, `POLE_LINE`), `game/assets/catalog.json` (each asset's collision, gaps, wind volumes, porosity, snag flag, LOD distances) and `game/assets/models/{structures,vegetation}/*.glb`; Unity's merged assets in `assets-src/catalog.json`.

## Why
The pilot wants to test flights and the complex physics "on something tangible, and not a couple of trees standing alone in a sandbox world" (D-044). The G1 gallery is a row of single stations on a floor. A small map built from what exists today gives G4's noclip through the feed and G7's first flights a yard, a house, a posadka, cars and a power line to fly around and into. It costs little: the Godot attempt already has the house, the yard and the belt's trees as finished models with their physics metadata, and Unity has the grass, a tree, the shrubs, the car, the poles, the wires and the trench.

## What Changes
- **The Godot stand-ins** (asset-artist): the adobe house, the plank shed, the cellar's head and shaft, the plank gate, the plank fence, the fruit tree, the belt's five trees (young, maple, acacia, ash, dead) and its shrub, ported from `refs/godot/main` through the Unity pipeline as stand-ins (D-040: no look pass, no rework). Each keeps its Godot collision, gaps, wind volumes, porosity, snag flag and LOD distances, carried into Unity's frame. Each gets its review sheet (AST-12). One command rebuilds each from the pinned Godot commit.
- **The map** `maps/test-range/` (world-builder), about 512 m square on flat ground:
  - **the yard:** the Godot house and yard as `ADOBE_YARD` lays them out (house, shed, cellar, gate, fence, two fruit trees), S04 grass round it
  - **the posadka:** a belt a few hundred metres long and 10–30 m wide (WLD-04), mixing the Godot belt trees with Unity's V01 trees and V13 shrubs, bushes among the trees
  - **fields** of S04 grass either side of the belt
  - **cars:** R01's two variants, one in the yard and one burnt on the track
  - **and the rest that exists:** L01 poles with L03 wires along the track, E01's trench along the belt, and H01's intact and damaged houses as the yard's neighbours once H01 merges
  - **start points:** the yard's track (legs on, from the ground) and a field edge with the launch stand (legs off, D-022)
- **The scene and build:** `TestRange.unity` built from the map's data by an editor builder, with the noclip camera and G1's look presets, a Windows player `Fpv450.Build.TestRange` with `-smoketest`.
- **The sim's view of it:** the map's placement file is in the gallery's station form, so `p6-world-query-gallery`'s loader (its task 1.3) reads both, and its queries (1.4) and budgets (1.5) run on the test range too.
- **The test range replaces the gallery** as G4's noclip map and G7's flight map (D-044 supersedes D-042's "the test range is the G1 gallery"); the gallery stays the look-lock reference.

## Capabilities

### New Capabilities
- `test-range`: the map's contents and layout, the Godot stand-ins' port and metadata, the scene, the noclip camera and look presets, the start points, the build with its smoke test, and the frame-rate budget

### Modified Capabilities
- None. (`world-query` is still in flight in `p6-world-query-gallery`; its tasks name the test range.)

## Non-goals
- **The look:** no side-by-side against the letters and no tuning for the stand-ins (D-040); the Unity assets keep their own looks. A look the pilot rejects goes to its asset's own change.
- **Relief and terrain layers:** the ground is flat; the rolling ground, surfaces with feathered outlines and roads wait for G3's world (WLD-02, WLD-03, WLD-06).
- **Mass production** or new assets beyond the port.
- **The flight session and app** (`p6-input-app`), **the feed** (`p4-feed`), **the drone** (`p5-drone`), **the world queries' code** (`p6-world-query-gallery`).

## Impact
- **New:** `maps/test-range/`, a stand-in importer path under `tools/blender/` and its params under `assets-src/`, the stand-ins' prefabs under `unity/Assets/_Project/{Art,Prefabs}/`, `Scenes/TestRange.unity` and its builder under `Scripts/World/`, `Fpv450.Build.TestRange`, `docs/test-range.md`.
- **Reads:** `refs/godot/main` at `8340138`, read-only through `git show` and its LFS objects (D-027); the G1 branch's `LookSetup`, noclip and gallery scene builder (the map's task stacks on G1's tip until G1 merges).
- **Re-scoped:** `p6-world-query-gallery` 1.3–1.5, `p6-input-app` 1.4, `p6-flight-core` 1.6, and `p4-feed` 1.8 and 2.2 name the test range where they named the gallery.
