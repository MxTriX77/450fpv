## ADDED Requirements

### Requirement: Our objects stand in the farmland
The farmland SHALL hold one `E01_curving` trench in each field F1–F11, at a seeded random spot and yaw fully inside the field, and the houses `H01_I` and `H01_D` and the cars `R01_A` and `R01_F` in the village on free ground, clear of the friend's ruins, debris and trees and of the start point (D-090, D-092).

#### Scenario: Placements validate
- **WHEN** `tools/map/validate.py` checks `maps/farmland/`
- **THEN** every trench lies inside its field with its margin, every village object is clear of the ruins, debris, trees and the start point's footprint, and the same seed gives the same placements

### Requirement: A trench is a hole in the ground with ground of its own
Each trench SHALL cut the terrain as hole cells derived from its opening, and every point of every hole cell SHALL lie inside the trench's filler band, whose shapes are the only ground there (WLD-16). The cut and the flattened footprints SHALL be data in `maps/farmland/`, applied at load to the sim and to a runtime copy of the drawn terrain, never to the store (D-095).

#### Scenario: No naked hole
- **WHEN** the validator samples every hole cell every 0.1 m
- **THEN** every sample lies inside a filler band

#### Scenario: The drawn hole is the sim's hole
- **WHEN** the player loads the farmland
- **THEN** the drawn terrain has a hole exactly at each hole cell of the mask, the store's terrain files are unchanged, and a capsule dropped into a trench lands on the trench's floor

#### Scenario: Houses and cars stand level
- **WHEN** a house or car is placed
- **THEN** the terrain under its footprint is flat in the sim and in the drawn terrain, and its base meets the ground within 1 cm
