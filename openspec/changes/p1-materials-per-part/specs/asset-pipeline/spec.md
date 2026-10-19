## ADDED Requirements

### Requirement: Physics material rows are per touching part
Every row of `assets-src/physics_materials.json` SHALL state its contact values for one touching part of the drone (a leg foot, an arm, the body or the coil), since the contact law applies the row to each part that touches it; a row's rationale SHALL name the part it was derived for and the sink or deflection it gives that part, with its label (published, derived or assumed).

#### Scenario: A soft row read per part
- **WHEN** the `fabric` row is read
- **THEN** its stiffness is the one a single 15 mm leg foot meets on a mattress, its rationale gives the sink one foot makes under its share of the drone's weight, and four feet together sink as the sheets that use it say
