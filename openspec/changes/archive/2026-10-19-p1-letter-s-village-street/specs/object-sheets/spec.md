# Spec Delta: object-sheets

## ADDED Requirements

### Requirement: A letter is read for its look and feel
When a reference letter is added, the reference notes SHALL hold its reading: the scene; the viewpoint's height and path, estimated from the scene; the layout with labelled sizes; what stands where; and its look and feel (composition, light, season, colours sampled and labelled, wear, the density of clutter and vegetation, and what reads near, at middle distance and far). Every object sheet whose objects the letter shows SHALL cite it in its reference views and in each such row's "Seen in", with still numbers.

#### Scenario: Letter S read in
- **WHEN** the reference notes and the object sheets are read after letter S is added
- **THEN** the notes hold S's reading with every part above, every sheet row whose object S shows cites S with still numbers, every number taken from S is labelled measured with its still, and the change lists what S implies for each asset already built or in flight

#### Scenario: Nothing of the place
- **WHEN** the hygiene scan runs over the tree
- **THEN** it finds no place name, coordinate, OSD value or reference file name, and nothing under `reference/` is tracked
