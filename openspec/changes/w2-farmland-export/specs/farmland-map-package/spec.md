## ADDED Requirements

### Requirement: The farmland exports as a map package
An exporter SHALL write `maps/farmland/` from the scene the friend's generator built, with the heights of the map, the surround and the far ring, the ground class and grass density per cell, every tree, bush, ruin and debris placement with its variant, and the outlines of the fields and the village. The same seed SHALL give the same bytes, and `tools/map/validate.py` SHALL check the package.

#### Scenario: The package validates
- **WHEN** `python tools/map/validate.py maps/farmland` runs on a fresh export
- **THEN** it passes, and two exports from the same seed are byte-identical

#### Scenario: The heights are Unity's
- **WHEN** 1,000 points (seeded) are sampled across the map and the rings
- **THEN** every height read from the package's samples, triangulated as Unity's terrain does, equals Unity's `Terrain.SampleHeight` at that point within 1 mm (assumed)

#### Scenario: Every object is placed
- **WHEN** the export's placements are counted against the scene's terrain instances and village objects
- **THEN** every tree, bush, ruin and debris piece on the map has exactly one placement, and none lies outside the map
