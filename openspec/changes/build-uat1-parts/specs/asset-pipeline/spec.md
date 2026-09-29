# Spec Delta

## Purpose

Turns Blender sources into game-ready, physically described assets the map can place. They look realistic at FPV distances and in the analog view, stay within performance and repo-size budgets, and use only licence-clean third-party material.

## ADDED Requirements

### Requirement: Reproducible export
Every asset SHALL have a `.blend` source under `blender/`. A headless script in `tools/blender/` SHALL export it to `.glb` under `game/assets/models/`. Running the export twice on unchanged sources SHALL give the same mesh data.

#### Scenario: One command
- **WHEN** the export script runs on all sources
- **THEN** every catalog asset's `.glb` is regenerated, the run exits 0, and `git status` shows no change to the committed `.glb` files

### Requirement: Physical description in the catalog
Every exported asset SHALL have a catalog entry with:
- primitive collision shapes (box, sphere, capsule, cylinder) that follow its solid parts
- a contact material per asset or shape
- a wind volume and side-on porosity
- a snag-hazard flag
- named fly-through gaps for every opening a drone can pass: doors, windows, a cellar mouth, holes in the destroyed block

Every entry SHALL pass the map validator.

#### Scenario: House door is a gap
- **WHEN** `GapsNear` is queried in front of the adobe house's door
- **THEN** the door gap is returned with its real width and height (within 5 cm of the mesh opening), and a capsule the size of the drone passes through it without contact

#### Scenario: Collision follows the visual
- **WHEN** 1,000 downward rays are cast over each asset's footprint
- **THEN** a ray that hits the visual mesh's solid parts also hits a collision shape within 0.15 m (on solid walls and roofs), and every exception is listed as a deliberate simplification (thin trim, glass)

### Requirement: Budgets and LOD
Each asset class SHALL respect a triangle and texture budget:

| Class | LOD0 triangles | LOD levels | Textures |
|---|---|---|---|
| Vehicle | ≤ 40 k | ≥ 3 | ≤ 2K |
| House | ≤ 60 k | ≥ 3 | ≤ 2K |
| Destroyed block | ≤ 200 k | ≥ 3 | ≤ 2K |
| Tree | ≤ 30 k | ≥ 3 plus a far impostor or billboard | ≤ 2K |
| Small props | ≤ 5 k | | ≤ 1K |

The export SHALL fail when a budget is exceeded.

#### Scenario: Budget report
- **WHEN** the export runs
- **THEN** it prints each asset's triangles per LOD and texture sizes against its class budget, and exits non-zero on any excess

### Requirement: Licence-clean sources
Third-party textures and HDRIs SHALL come only from CC0 sources (Poly Haven, ambientCG), through a script that pins each item by id and checksum. `CREDITS.md` SHALL list every item, its source URL and its licence. Nothing derived from the pilot's reference footage SHALL enter the repo.

#### Scenario: Credits are complete
- **WHEN** QA compares the texture files in `game/assets/textures/` with `CREDITS.md`
- **THEN** every third-party file is listed as CC0 with its source, and every other file is marked as made by the team
