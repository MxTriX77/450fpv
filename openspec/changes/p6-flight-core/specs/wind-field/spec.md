## ADDED Requirements

### Requirement: A stand-in wind, labelled as such
Until `p7c-wind`, the wind SHALL be a mean wind per level (Calm, Windy, Severe) with a per-flight direction and strength drawn from the seed, plus simple seeded turbulence, sampled at the rotors and the body. It SHALL be labelled `stand-in` in the log header and the summary (PHB-06).

#### Scenario: Calm is never perfectly still
- **WHEN** a flight is flown at Calm
- **THEN** the wind at the body is not zero over any 10 s window and it moves in every one of them, its mean stays within the Calm level's range, and the header names the wind model `stand-in`
- **AND** still air and a fixed vector each fail it

#### Scenario: The levels hold their intensity and scale
- **WHEN** 10 or more seeds of 20 min each are drawn at Windy and at Severe
- **THEN** each drawn mean lies in its level's band and the wind is unbiased about it, the horizontal gust's σ/U lies in 0.12–0.15 with the vertical's 0.556 of it within 10 %, and the gust's time constant is L/U within 20 % (L_u 116 m, L_w 20 m)
- **AND** white noise of the same σ, a σ that does not grow with the wind, and a mean 5 % strong each fail the target made for it

#### Scenario: The same seed, the same wind
- **WHEN** two flights share a seed and a level
- **THEN** their wind samples are bit-identical, and a different seed gives a different prevailing direction, uniform over the compass across seeds
- **AND** a turbulence drawn from an unseeded generator, and a direction fixed to one side, each fail it
