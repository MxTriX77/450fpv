## ADDED Requirements

### Requirement: Play opens a carousel of the maps
The main menu SHALL offer Play in place of Train. Play SHALL open a carousel with one card per map, House (map id `test-range`) and Village (map id `farmland`), each with its name, a one-line description and its still. Choosing a card SHALL open that map's setup, then its flight (D-100, PRD-05, APP-02).

#### Scenario: The menu
- **WHEN** the player opens to the main menu
- **THEN** it reads Play, Calibrate, Explore (review builds only) and Exit, and no item or subtitle names a map the item does not open

#### Scenario: Choosing a map
- **WHEN** the pilot moves through the carousel with the keyboard, the mouse or the radio's sticks and chooses a card
- **THEN** that map's setup opens with that map's start points, Fly loads that map, and Esc from the carousel returns to the menu

### Requirement: House flies the test range again
Play → House SHALL fly the test range (its scene, map world, start points and files) with the setup and the flight as before W3 3.1, at the current physics model, and its flight log SHALL replay headless bit for bit.

#### Scenario: A House flight
- **WHEN** the smoke test chooses House, flies from its start and ends the flight
- **THEN** the flight loads the test range, the summary names its map, R and "again" stay on House, and the log replays headless bit for bit

#### Scenario: Village is unchanged
- **WHEN** the smoke test chooses Village and flies
- **THEN** the flight is the farmland flight Train opened at 241145ea, from the village start, and the existing farmland smoke checks pass

### Requirement: Each card's still is generated through the feed
Each map's still SHALL be rendered by a capture method through the analog feed with the OSD, from a pose per map kept in a params file, never made by hand. House's pose SHALL frame the test range's car, fence and house; Village's SHALL be exactly Explore's spawn frame. A still rendering the friend's world SHALL NOT be pushed unless the change shows it is redistributable (D-089).

#### Scenario: Village's still is Explore's first frame
- **WHEN** the capture renders Village's still and Explore's first noclip frame is captured at the same output size
- **THEN** the camera's position, rotation and field of view match to the float, and the two frames show the same scene

#### Scenario: Nothing of the friend's world is pushed
- **WHEN** `python tools/hygiene/precommit.py --tree HEAD` runs on the branch
- **THEN** it passes, and the rendered stills live only in a git-ignored folder and in the player's build

#### Scenario: Rebuilt, never hand-edited
- **WHEN** the build runs from a clean checkout with the store linked
- **THEN** the stills are rendered from their params and carried into the player, with no manual step
