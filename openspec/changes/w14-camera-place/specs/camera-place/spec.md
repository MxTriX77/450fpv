## ADDED Requirements

### Requirement: The camera's place comes from the references
`drone.json`'s `camera.lens_m` SHALL be derived from the footage of the sim's airframe group and from the published frame, with its derivation in a reference note, never assumed (DRN-04, DRN-03, REAL-01).

#### Scenario: The label
- **WHEN** `drone.json`'s `camera.lens_m` is read
- **THEN** its label is derived or published, and its note cites the reference note's section that gives the fit and its residual

### Requirement: What the first-person picture shows of the airframe matches its group
From the camera's place, the blades and the airframe parts in the first-person picture SHALL match the footage of the sim's airframe group: how often a blade enters and where, and the two rods at mid-height (VIS-07, DRN-04, REAL-03).

#### Scenario: Blades over 24 fields of a hover
- **WHEN** 24 consecutive fields of a hover are captured through the feed
- **THEN** the share of fields with a blade in the picture, and where it enters, are printed beside the reference note's counts for the group's letters and beside the share the note's fit predicts, and the captured share is within the fit's stated error of the predicted one

#### Scenario: The rods
- **WHEN** a field of a hover is captured
- **THEN** the airframe parts the reference note names appear at each side at mid-height, within the note's stated error of their place in the group's clips, drawn as geometry

#### Scenario: Side by side
- **WHEN** the comparison sheet is made
- **THEN** it sets the sim's fields beside the group's letters in the local-only compare folder, and a look at it finds no part of the airframe in the sim's picture that the footage lacks

### Requirement: Moving the camera moves nothing else
The change SHALL keep the hubs, the props, the lens's intrinsics and the 0° uptilt, and SHALL audit every golden that moves (DRN-02, FEED-02, D-026).

#### Scenario: The goldens
- **WHEN** `flightbench --golden` and the feed's tests run after the change
- **THEN** every case that moved is listed with its cause in design.md, and a case moves only through the mass build or through the camera point's signals
