## ADDED Requirements

### Requirement: The wind's height is over the local ground
Every wind query SHALL take its height over the ground under the queried point (each rotor, the body, each fiber node), never the ground under the drone when the point lies elsewhere (PHY-13).

#### Scenario: A laid fiber on a hillside
- **WHEN** a fiber lies on a slope far from the drone in Severe wind
- **THEN** each node's wind is the profile at that node's own height over its own ground, and a node resting on the ground feels the wind of the surface layer there

#### Scenario: Flat ground is unchanged
- **WHEN** the floor's and the test range's wind goldens run
- **THEN** they pass, or each moved case is audited and explained in design.md

### Requirement: Hills shape the wind
The wind field SHALL speed up over the farmland's crests and shelter or separate in the lee of steep slopes and below the ledge, following a published model within its stated validity range. No value is tuned to look right (PHY-13).

#### Scenario: Crest and lee rows
- **WHEN** worldbench's farmland wind rows sample a crest, a lee slope, below the ledge and an open field
- **THEN** each row's speed-up or shelter matches the published model's value for that terrain, and the rows are goldens

### Requirement: Belts and the village shelter the wind
The wind behind the farmland's tree belts and among the village's ruins SHALL follow the existing wakes and canopy flow, checked against published shelterbelt data (PHY-13).

#### Scenario: Behind a belt
- **WHEN** a row samples the wind at several belt heights downwind of a farmland belt
- **THEN** the reduction against the open field matches the published data within its spread

### Requirement: A laid fiber rests where the wind lets it
In Severe wind over the farmland, a laid fiber's nodes SHALL move only as their own wind and ground contact make them, and resting nodes freeze into the trail (PHY-18, PLT-04).

#### Scenario: The farmland line in Severe wind
- **WHEN** `flightbench --bench farmland-line-step` runs in Severe wind
- **THEN** it reports the far nodes' speeds and how many freeze, the line stays whole and stays over the ground, and the step's cost is reported before and after the change
