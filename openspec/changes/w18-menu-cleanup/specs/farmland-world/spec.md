## MODIFIED Requirements

### Requirement: The farmland is in the player
The player SHALL carry the friend's farmland (D-088), flown from Play → Village, with its terrain tiles, grass, trees, bushes and village as the friend's project shows them. No menu item SHALL open it in noclip (the pilot, 2026-11-17); its noclip view SHALL stay for tooling alone, reached by a command-line argument or an editor method: the frame-rate bench (`-farmbench`), the smoke test's world checks in a review build (`-smoketest`) and the stills' capture (`MapStills`). Every third-party file it uses SHALL stay out of git and come through the vendor store's links (D-089).

#### Scenario: Noclip shows the friend's world
- **WHEN** the farmland's noclip view, opened by tooling, is placed at five cameras set in the friend's project (the crossroads, a field, a tree belt, the village, the map's edge)
- **THEN** each view matches the friend's project rendered at the same camera, side by side, in terrain, vegetation and buildings

#### Scenario: Tooling reaches the noclip view
- **WHEN** a review build runs `-smoketest`
- **THEN** it opens the farmland in noclip through the same entry `-farmbench` uses, with the feed on, the launch stand at the village's start on the terrain and the first frame within PLT-06's 20 s of the request, and the drawn terrain round the stand within 1 mm of the sim's

#### Scenario: A fresh worktree gets the world
- **WHEN** a new worktree runs `tools/vendor/link.ps1` and Unity opens the project
- **THEN** the farmland scene loads with no missing references, and `precommit.py --tree HEAD` is clean
