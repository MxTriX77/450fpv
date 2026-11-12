## ADDED Requirements

### Requirement: The farmland is in the player
The player SHALL open the friend's farmland (D-088) from the menu in noclip, with its terrain tiles, grass, trees, bushes and village as the friend's project shows them. Every third-party file it uses SHALL stay out of git and come through the vendor store's links (D-089).

#### Scenario: Noclip shows the friend's world
- **WHEN** the pilot picks the farmland in noclip and flies to five cameras set in the friend's project (the crossroads, a field, a tree belt, the village, the map's edge)
- **THEN** each view matches the friend's project rendered at the same camera, side by side, in terrain, vegetation and buildings

#### Scenario: A fresh worktree gets the world
- **WHEN** a new worktree runs `tools/vendor/link.ps1` and Unity opens the project
- **THEN** the farmland scene loads with no missing references, and `precommit.py --tree HEAD` is clean

### Requirement: One start point in the village
The farmland SHALL have one start point, in the village, on free level ground clear of the ruins, debris, trees and our objects (D-092). With legs off, the launch stand SHALL stand there.

#### Scenario: The start point is free and level
- **WHEN** the session places the drone at the farmland's start point with legs off
- **THEN** the launch stand stands within the village's outline, its four posts on the terrain, and no ruin, debris piece, tree or object lies within 2 m (assumed) of its footprint
