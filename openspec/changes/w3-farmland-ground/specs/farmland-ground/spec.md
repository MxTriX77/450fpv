## ADDED Requirements

### Requirement: The sim stands on the farmland's heights
The world-query core SHALL answer `SampleGround`, `Raycast` and `StaticContacts` over every tile of `maps/farmland/`, triangulated exactly as the map package's reference (`tools/map/validate.py`, `Tile.height`), with only + − × ÷ √, no managed allocation in steady state, and `QueryVersion` 8. A step line between tiles SHALL be a vertical wall, a hole cell or a point off the world SHALL be a hole, and each cell SHALL carry its surface class.

#### Scenario: The heights are the package's to the bit
- **WHEN** `SampleGround` is called at the package's 1,000 height probes
- **THEN** every height equals the reference's to the bit

#### Scenario: A step is a wall
- **WHEN** a ray or a swept contact crosses the surround's outer ledge (1.40–1.61 m, measured)
- **THEN** it reports a vertical wall contact there, never a jump through the step

#### Scenario: Off the world is a hole
- **WHEN** a point lies beyond the far ring
- **THEN** `SampleGround` reports a hole, not a height

### Requirement: The fiber's contacts cost a bounded time
A laid fiber segment resting near shapes SHALL NOT cost a full static-contact query on every step. The flights that lagged in model 79 SHALL replay within PLT-04's step budget.

#### Scenario: The model-79 stretches fit the budget
- **WHEN** the worst stretches of the three model-79 flights (L1's report) replay headless in .NET
- **THEN** the p99 step time is at most 300 µs and the world queries alone stay under 60 µs (PLT-04)

#### Scenario: A line through the farmland fits the budget
- **WHEN** a seeded flight lays 1.5 km of fiber through a tree belt and the village, then hovers 60 s
- **THEN** the p99 step time is at most 300 µs, and the flight allocates 0 B after its first step

### Requirement: A laid fiber lies over the ground
On a terrain, the fiber's chain SHALL NOT pass through the ground between its nodes: where a segment's chord passes more than InsertGap (0.05 m) under the ground, a node SHALL go onto the ground at its deepest point, and a node the ground holds over the chord between its neighbours SHALL NOT be merged away. Between the tests no segment SHALL lie deeper in the ground than the stated tolerance. A flight on a plane SHALL step as before, bit for bit.

#### Scenario: A line laid over a crest
- **WHEN** a line is laid over a 3 m ridge whose spacing-law chords pass more than a metre under its crest, and the drone flies away from it
- **THEN** a node lies on the ground at the crest line, no segment lies deeper in the ground than the tolerance at any step's end, the line stays whole, and the flight replays bit for bit

#### Scenario: A line wraps the ledge's lip
- **WHEN** a line laid on the far ring is dragged up the ledge's 1.5 m face and over its lip
- **THEN** a node lies on the lip while the line runs over it, and no segment cuts through the step's corner deeper than the tolerance

### Requirement: The player flies the farmland
The player SHALL fly the farmland from its village start point (D-092), and the test range SHALL leave the player while it stays for headless tests and benches.

#### Scenario: A flight on the hills replays
- **WHEN** the pilot flies 2 minutes over the hills from the village start point
- **THEN** the log replays bit for bit headless, and no step tunnels into the ground beyond the contact tolerance
