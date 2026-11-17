## MODIFIED Requirements

### Requirement: Play opens a carousel of the maps
The main menu SHALL offer Play in place of Train. Play SHALL open a carousel with one card per map, House (map id `test-range`) and Village (map id `farmland`), each with its still and its name, and no line on what the map is (the pilot, 2026-11-17). Choosing a card SHALL open that map's setup, then its flight (D-100, PRD-05, APP-02).

#### Scenario: The menu
- **WHEN** the player opens to the main menu, in any build
- **THEN** it reads Play, Calibrate and Exit, and no item carries a hint naming a map

#### Scenario: Choosing a map
- **WHEN** the pilot moves through the carousel with the keyboard, the mouse or the radio's sticks and chooses a card
- **THEN** that map's setup opens with that map's start points, Fly loads that map, and Esc from the carousel returns to the menu

#### Scenario: A card
- **WHEN** the carousel shows a map's card in the middle
- **THEN** under the still it reads the map's name, then the dash per card, with no describing line between them
