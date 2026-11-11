# farmland-ruins Specification

## Purpose
Physics data for the friend's five ruins and their debris (D-088, D-090): collision that follows the visual (two-skin walls, open between), every clear opening as a gap, materials, snag records and wind volumes, generated from the vendor meshes by a script with per-piece params. Nothing pushed reproduces a vendor vertex (D-089).

## Requirements

### Requirement: The farmland's ruins and debris have physics
Each of the village's ruins and debris pieces SHALL carry physics metadata generated from its mesh: collision that follows the visual, every visible opening as a gap, and a material per part (wood, burnt wood, sheet metal). The params and metadata SHALL be pushed; anything that copies vendor geometry SHALL stay local-only.

#### Scenario: A crash names the ruin's material
- **WHEN** a seeded sim test flies the drone into a ruin's wall at 8 m/s
- **THEN** the contact stops it at the wall's surface within the contact tolerance, and the crash event names the ruin, the part and its material

#### Scenario: An opening is a gap
- **WHEN** the drone flies through a ruin's window or door opening along its centre line
- **THEN** it passes without contact where the opening is wider than the drone's frame, and the review sheet shows every opening's gap at 1.5 and 10 m
