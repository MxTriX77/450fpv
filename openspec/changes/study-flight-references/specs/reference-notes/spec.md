# Spec Delta

## Purpose

The written, public-safe description of the pilot's real flights. The map, the objects and the analog video look are built and judged against it.

## ADDED Requirements

### Requirement: OPSEC-safe content
The reference notes SHALL NOT contain place or settlement names, coordinates, OSD values of any kind, dates or timestamps, reference file names, unit or callsign information, descriptions that identify a unique real landmark, or positions of troops or equipment. Clips SHALL be cited only by letter.

#### Scenario: Hygiene check
- **WHEN** QA scans both notes files
- **THEN** it finds no text matching any of the banned categories above, and no string that appears in a reference file name

### Requirement: Terrain catalog coverage
`docs/reference-notes/terrain.md` SHALL describe every terrain type and object class seen in the reference set. Each entry SHALL give the clips it appears in, its typical size in metres, its materials and colours, how dense or frequent it is, and how it matters physically (walkable/landing surface, obstacle, turbulence source, snag hazard).

#### Scenario: Every clip represented
- **WHEN** the catalog is compared against the contact sheets
- **THEN** every clip letter is cited at least once, and every recurring object class on the sheets has an entry

#### Scenario: Physical relevance stated
- **WHEN** any catalog entry is read
- **THEN** it states its physical role, or explicitly says "visual only"

### Requirement: Prioritised build list
`terrain.md` SHALL end with a ranked list of terrain patches, objects and textures to build first for M1 feedback. Each item SHALL carry a one-line reason tied to the footage.

#### Scenario: Ready for M1 planning
- **WHEN** the orchestrator plans the next M1 change
- **THEN** it can take the top items straight from this list without going back to the footage

### Requirement: Video feed characterisation
`docs/reference-notes/video-feed.md` SHALL describe the observed feed traits: effective resolution feel, noise types and how they vary with light, motion and time, colour and saturation, exposure behaviour, artifacts, and general OSD layout without values. Each trait SHALL map to a candidate effect. Traits that come from re-encoding the clips rather than from the live feed SHALL be marked as such, and not proposed as effects.

#### Scenario: Traits map to effects
- **WHEN** any trait in `video-feed.md` is read
- **THEN** it cites clip letters, and either names a candidate effect or is marked as a re-encoding artifact

### Requirement: User sign-off
The notes SHALL be reviewed by the user before any M1 asset work starts. Corrections from the user SHALL be folded into the notes.

#### Scenario: Gate
- **WHEN** the user has not yet approved the notes
- **THEN** no M1 asset change is proposed as ready for apply
