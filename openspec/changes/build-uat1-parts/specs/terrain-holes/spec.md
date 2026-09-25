# Spec Delta

## Purpose

Lets the ground have openings narrower than the 1 m heightfield can express, such as trenches and cellar entrances. They are consistent everywhere: the pilot sees them, the drone and fiber fall into them, and physics knows their walls are soil.

## ADDED Requirements

### Requirement: Holes in the heightfield
A map package SHALL be able to mark terrain cells as holes, through an optional hole layer or hole shapes. Holed terrain SHALL be skipped by the renderer and by terrain collision. The hole is filled by a placed mesh asset (a trench section or cellar entrance) whose collision and contact surfaces describe the opening. The format change SHALL be a minor version bump.

#### Scenario: Trench is open
- **WHEN** a capsule the size of the drone is swept down into a trench section of the gallery
- **THEN** it passes the terrain level with no terrain contact, and the first contact is the trench floor or wall

### Requirement: World query knows holes
`SampleGround` SHALL report a `Hole` flag over holed cells. Trench walls and floors SHALL report a **soil surface** (not a rigid material), so legs sink and hook in trench soil as they do on open ground. Rays and static contacts SHALL agree with the rendered trench within the existing tolerances. `QueryVersion` SHALL be raised.

#### Scenario: Trench wall is soil
- **WHEN** a static contact touches a trench wall
- **THEN** its material resolves to the trench's surface (e.g. `crater_spoil`), with that surface's soil parameters

#### Scenario: Golden re-recorded
- **WHEN** the golden file is re-recorded after this change
- **THEN** only cases that touch holes or the new surface change, `QueryVersion` is 3, and `--golden` passes in Debug and Release
