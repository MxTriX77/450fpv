## ADDED Requirements

### Requirement: Randomness only as a bounded physical parameter
Randomness SHALL enter only as a bounded perturbation of a named physical parameter, drawn from a seeded stream and written to the log. It SHALL never enter as a force, a moment, a pose offset or a filter on the output.

#### Scenario: Every draw is named and bounded
- **WHEN** the suite lists every draw a flight makes
- **THEN** each names its parameter, its stream and its bound, each value lies inside its bound, and none is applied to a force, moment or pose directly

### Requirement: One flight seed, independent named streams
A flight SHALL have one 64-bit seed, shown and logged, and every stream SHALL derive from hashing (seed, stream name), so that adding a stream never shifts another.

#### Scenario: Adding a stream shifts nothing
- **WHEN** a new named stream is added and the same seed is flown again
- **THEN** every existing stream's draws are bit-identical to before
