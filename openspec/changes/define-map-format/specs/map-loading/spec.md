# Spec Delta

## Purpose

Turns a map package into a playable, reviewable Godot scene within the performance budget, so the pilot can fly any map in the review sandbox.

## ADDED Requirements

### Requirement: Build a scene from a package
The loader SHALL build from a valid package:
- terrain with collision
- surface-driven materials
- every placed object with the collision given by its catalog entry
- wires as thin geometry with collision
- near-camera micro-detail drawn from the world-query generator

#### Scenario: Sample patch
- **WHEN** `sample_patch` loads
- **THEN** every object in `objects.json` is present at its position within 1 cm, and every surface in the patch is visibly distinct

#### Scenario: Visual and physical stems agree
- **WHEN** the camera sits over `belt_straw`
- **THEN** the drawn stem bases within 3 m of the camera match `MicroDetailNear` positions within 1 mm

### Requirement: Sandbox hook
The review sandbox SHALL load a map when it starts with `-- --map <id>`. An unknown or invalid map SHALL produce one clear error and fall back to the placeholder.

#### Scenario: Fly the sample
- **WHEN** the sandbox starts with `-- --map sample_patch`
- **THEN** the map is loaded and the noclip camera starts above its centre

### Requirement: Rendering and load budget
On the dev machine, in noclip:
- `sample_patch` SHALL render at ≥ 120 fps with 1 % low ≥ 90
- a synthetic 4 km × 4 km terrain-only map SHALL render at ≥ 90 fps with 1 % low ≥ 60, and load in under 10 s

#### Scenario: Budget check
- **WHEN** QA flies a fixed path through each map with the F3 overlay
- **THEN** the recorded fps and 1 % low meet the limits, and the power mode is noted
