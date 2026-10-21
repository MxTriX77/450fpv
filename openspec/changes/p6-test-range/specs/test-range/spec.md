# Spec Delta

## Purpose

The test range: a small flat map built from the assets that exist, a yard with its house, a posadka, fields, cars, a power line and a trench, where the pilot looks through the feed in noclip at G4 and flies the first flights at G7 (D-044).

## ADDED Requirements

### Requirement: Contents from what exists
The map SHALL be about 512 m square on flat ground and SHALL hold:
- a yard with the Godot adobe house, plank shed, cellar head and shaft, plank gate, a run of plank fence and two fruit trees at the Godot yard's relative layout, with grass round it
- a posadka a few hundred metres long and 10–30 m wide, trunks 1–3 m apart, mixing the Godot belt trees and shrub with Unity's V01 trees and V13 shrubs, with bushes among the trees and at least one gap through it
- grass fields on both sides of the posadka
- both R01 variants: the intact car in or by the yard, the burnt one on the track
- a power line of at least four L01 poles with L03 wires, and an E01 trench run along the posadka
- once `p1-h01-adobe-house` is merged, H01's intact and damaged houses as the yard's neighbours

No text, sign or name SHALL appear in the map beyond what the assets already carry.

#### Scenario: Everything placed
- **WHEN** the test range scene is opened in the build
- **THEN** the log lists every placed prefab by asset and variant with its count, every item of the list above is present, and no mesh is missing and no material is pink

#### Scenario: The posadka's shape
- **WHEN** the map's placement data is read
- **THEN** the posadka's trunks lie within a strip 10–30 m wide and at least 200 m long, the nearest-neighbour distance between trunks has its median within 1–3 m, at least a fifth of its plants are Unity's V01 or V13, and at least one gap at least 4 m wide crosses it

### Requirement: The Godot stand-ins
Each Godot asset the map uses SHALL be ported from `refs/godot/main` at a pinned commit through the asset pipeline, rebuilt by one command, and labelled a stand-in in the catalog. Each SHALL keep its Godot collision shapes, gaps, wind volumes, porosity, snag flag and LOD distances, carried into Unity's frame, and SHALL get its review sheet. Nothing of the Godot branch SHALL be merged (D-027).

#### Scenario: One command rebuilds a stand-in
- **WHEN** `pwsh tools/build_asset.ps1 <ID>` runs for a stand-in twice
- **THEN** both runs exit 0 and the second leaves `git status` clean

#### Scenario: The metadata survives the port
- **WHEN** a stand-in's metadata is compared with its Godot catalog entry
- **THEN** every collision shape, gap, wind volume and the porosity, snag flag and LOD distances match within 1 mm and 0.01°, after the frame change, and the asset's audit finds every collision shape within its mesh's bounds plus 5 cm

#### Scenario: The door and gate are gaps
- **WHEN** the house's, shed's and gate's metadata is read
- **THEN** each Godot opening (doors, windows, the gate) is a gap at its Godot centre and size within 1 mm

### Requirement: The sim sees what the scene shows
The map's placements SHALL be one data file in the form the gallery's station data uses, read both by the scene builder and by the sim's world loader, so the physics meets each object where it is drawn.

#### Scenario: Pose parity
- **WHEN** the scene is built and the same placement file is loaded for the sim
- **THEN** every placed instance's position agrees within 1 mm and its yaw within 0.01°

### Requirement: Start points
The map SHALL hold at least two start points: one on the yard's track for take-off from the ground with the legs on, and one at a field edge facing the posadka where the launch stand is placed for take-off with the legs off (D-022).

#### Scenario: Start points listed
- **WHEN** the map's data is read
- **THEN** it lists at least two start points with position and heading, one marked legs-on and one marked legs-off with the stand, each on clear ground at least 5 m from any collision shape

### Requirement: Noclip and the look presets
The noclip camera SHALL start above the yard looking along the posadka, and the scene SHALL switch G1's four light presets (day, low sun, overcast, night) by the gallery's keys.

#### Scenario: Presets switch
- **WHEN** the pilot presses a preset key
- **THEN** the light changes to that preset within one frame, as in the gallery

### Requirement: The build and its budget
`Fpv450.Build.TestRange` SHALL build a Windows player of the test range that exits after its first frame on `-smoketest`, loads in at most 10 s from start to the first frame (PLT-06), and holds PLT-02's frame rate in noclip on AC with vsync off (PLT-07). `docs/test-range.md` SHALL tell the pilot how to run it and what the keys do.

#### Scenario: Smoke test
- **WHEN** `450fpv-test-range.exe -smoketest` runs
- **THEN** it exits 0 and its log lists the scene and every placed prefab

#### Scenario: Frame rate
- **WHEN** QA flies the noclip camera along the PLT-07 path at 1080p on AC, with the power mode printed, over the yard, along the posadka at 2 m and over the fields at 50 m
- **THEN** each leg meets PLT-02's noclip row: a 1 % low of at least 60 fps
