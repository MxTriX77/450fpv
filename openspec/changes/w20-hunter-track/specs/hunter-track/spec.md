# Spec Delta

## Purpose

The ХАНТЕР-09 map: the pilot's own race track, rebuilt from their saved layout with our own ground and objects, flown from Play.

## ADDED Requirements

### Requirement: The layout comes from the pilot's track file
The map SHALL take every object's kind, position, rotation and scale from the pilot's saved track file, through a repeatable import into a neutral layout held in the repository.

#### Scenario: Every object is accounted for
- **WHEN** the import reads the pilot's track file
- **THEN** it reports 334 objects, 278 drawn and 56 kept as data only, and refuses to finish if any object's kind has no entry in its kind table

#### Scenario: Poses are exact
- **WHEN** the imported layout is compared with the file
- **THEN** every object's position is within 1 mm, its rotation within 0.01° and its scale within 0.1 %

#### Scenario: The import is repeatable
- **WHEN** the import runs twice on the same file
- **THEN** the two outputs are byte-identical

### Requirement: Nothing of the source game enters the repository
The map SHALL hold only the pilot's layout and our own work. No file, mesh, texture, terrain or catalogue data SHALL be taken from the other game's install, and neither its name, its item ids nor any brand name SHALL appear in committed files for this map.

#### Scenario: The committed map is neutral
- **WHEN** QA searches the map's committed files and its import tool for the source game's name, its item ids and the brand names in the source file
- **THEN** there are no matches

#### Scenario: The import reads only the pilot's file
- **WHEN** the import runs
- **THEN** the only input it opens is the track and race file paths given on its command line

### Requirement: The race order is kept
The map's data SHALL keep the race's order so checkpoints can be added later, and the app SHALL NOT build checkpoints, timing or laps from it.

#### Scenario: The order survives the import
- **WHEN** the stored order is walked from its start
- **THEN** it visits 101 passages through 56 distinct checkpoints in the file's order and ends at the finish

### Requirement: The ground carries the layout
The ground under the layout SHALL be derived from the heights of the objects standing on it, without using the other game's terrain.

#### Scenario: Standing objects meet the ground
- **WHEN** the map loads
- **THEN** each of the 34 upright objects standing on the ground has its foot within 50 mm of the ground

#### Scenario: Openings are clear
- **WHEN** the map loads
- **THEN** the whole opening of each of the 10 gates (9 frames and the light gate) is above the ground

### Requirement: Objects have real shapes and physics
Each drawn object SHALL be our own generated model of its kind at the size the file or the clips give, with collision that follows the visual, physics metadata and a review sheet. Every opening SHALL be a gap the sim knows.

#### Scenario: A gate can be flown through
- **WHEN** the world is queried along the axis through the centre of each gate's opening
- **THEN** the query hits nothing inside the opening, the gap query returns that opening, and a ray aimed at the gate's frame member hits it

#### Scenario: An asset is reviewed
- **WHEN** a kind's final model is built
- **THEN** it has a review sheet at 1.5, 10, 50 and 200 m and carries no brand mark

#### Scenario: Layout and scale match the pilot's flights
- **WHEN** views from the track are compared side by side with clips X and Y (REAL-03)
- **THEN** the objects' arrangement, scale and flyable gaps match; the source game's art is not a target

### Requirement: The map is flown from Play
Play SHALL offer the map as a third card named ХАНТЕР-09, with no describing label, and a flight SHALL start at the race's spawn point. House and Village SHALL fly exactly as before.

#### Scenario: Flying the new map
- **WHEN** the pilot chooses the ХАНТЕР-09 card and starts a flight
- **THEN** the drone starts upright at the race's spawn point with the file's heading, on its legs or on the launch stand as set up, and the flight's log names the map so it replays headless

#### Scenario: The other maps are untouched
- **WHEN** the flight goldens run after this change
- **THEN** they pass with no re-recorded file and the model number is unchanged

#### Scenario: It meets the budgets
- **WHEN** the PLT-07 benchmark flies the map on AC power with vsync off
- **THEN** the 1 % low is at least 60 fps (PLT-02) and the time from the menu to flyable is at most 20 s (PLT-06)
