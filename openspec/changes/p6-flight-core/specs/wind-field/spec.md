## ADDED Requirements

### Requirement: A stand-in wind, labelled as such
Until `p7c-wind`, the wind SHALL be a mean wind per level (Calm, Windy, Severe) with a per-flight direction and strength drawn from the seed, plus simple seeded turbulence, sampled at the rotors and the body. It SHALL be labelled `stand-in` in the log header and the summary (PHB-06).

#### Scenario: Calm is never perfectly still
- **WHEN** a flight is flown at Calm
- **THEN** the wind at the body is not zero over any 10 s window, its mean stays within the Calm level's range, and the header names the wind model `stand-in`

#### Scenario: The same seed, the same wind
- **WHEN** two flights share a seed and a level
- **THEN** their wind samples are bit-identical, and a different seed gives a different prevailing direction
