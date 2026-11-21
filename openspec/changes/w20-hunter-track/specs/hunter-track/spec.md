# Spec Delta

## Purpose

The ХАНТЕР-09 map: the pilot's own race track, rebuilt from their saved layout with our own ground and objects, flown from Play.

## ADDED Requirements

### Requirement: The layout comes from the pilot's track file
The map SHALL take every object's kind, position, rotation and scale from the pilot's saved track file, through a repeatable import into a neutral layout held in the repository.

#### Scenario: Every object is accounted for
- **WHEN** the import reads the pilot's track file
- **THEN** it reports 334 objects, 278 drawn and 56 kept as data only, and refuses to finish if any object's kind has no entry in its kind table

#### Scenario: Doubled rows stand once
- **WHEN** the map is compiled from the layout
- **THEN** the file's 7 byte-identical doubled rows are each placed once, so 272 distinct drawn objects stand on the map, while the layout keeps all 334 rows

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
- **THEN** each of the 32 distinct upright objects standing on the ground has its foot within 50 mm of the ground

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

### Requirement: The track's three houses are ours, at the track's dimensions
The map SHALL hold three houses of our own design at the places the race's markers show, with the dimensions of the pilot's track as measured from its file and clips X and Y, and with the village map's house textures. They SHALL be proper buildings with real wall thickness, rooms, window and door openings and roofs, and SHALL NOT copy the other game's models or textures.

#### Scenario: Every marked opening exists
- **WHEN** the houses are compared with the 23 opening markers in the layout
- **THEN** each marker lies in a real opening at least its size, centred within 50 mm, except the three widened by the next scenario

#### Scenario: The quad fits every opening
- **WHEN** the drone's level outline with legs on and 1 kg (0.574 m wide, 0.342 m tall) is passed nose-first through each of the 23 openings
- **THEN** it clears each side by at least 20 mm; the openings of markers 416, 394 and 32, which are too small for it as marked, are enlarged no further than 0.62 m wide (416), 0.40 m tall (394) and 0.41 m tall (32, which shares one gap with marker 27)

#### Scenario: Objects fixed to the house stay fixed
- **WHEN** the map loads
- **THEN** each of the 15 drawn objects the sheet places on or in the north house is within 50 mm of a house surface, and each of the 4 it places on the tree beyond the house's end is within 50 mm of our stand-in birch's batten

#### Scenario: Walls are solid and honest
- **WHEN** the world is queried across a wall, a window and a door of each house
- **THEN** the wall answers on both faces at its real thickness, the openings answer nothing, each opening is a gap the sim knows, and collision follows the drawn shape within 20 mm

#### Scenario: The textures stay local
- **WHEN** QA inspects the committed files for the houses
- **THEN** they reference the village's house materials and contain no texture or mesh from a local-only pack

#### Scenario: A house is reviewed
- **WHEN** a house is built
- **THEN** it has a review sheet at 1.5, 10, 50 and 200 m, and its dimensions are compared side by side with clips X and Y

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
