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
The footage is the target look. `docs/reference-notes/video-feed.md` SHALL describe the observed analog feed, citing clips for each point, in five areas:
1. **Noise events**: every type observed (at least grain, stripes/bars and full-frame noise flashes, plus any others found), each with its measured rate (events per minute) and duration (frames).
2. **Pixel-level artifacts**: hard-edged or aliased pixels, sharpening halos, colour bleed.
3. **Optics**: lens distortion and vignetting, with an estimated strength.
4. **Colour and exposure response**: how colour and brightness shift as framing changes, including when the camera approaches an object.
5. **Resolution feel and OSD layout** (never OSD values).

Each trait SHALL name a candidate effect and a hypothesised simulation driver: throttle or motor current, battery voltage sag, vibration, impact, fiber tension, light level, frame content, or "free-running random" with its observed rate. A trait SHALL be classed as a re-encoding artifact only when it is clearly block-based. Uncertain traits SHALL stay in the notes, flagged for the user.

#### Scenario: Traits map to effects and drivers
- **WHEN** any trait in `video-feed.md` is read
- **THEN** it cites clip letters and names a candidate effect and a driver, or it is marked as a re-encoding artifact with its block-based evidence

#### Scenario: Measured event statistics
- **WHEN** the noise events section is read
- **THEN** each event type gives its rate per minute and duration in frames, taken from the clips' `metrics.csv`

#### Scenario: Full coverage
- **WHEN** `video-feed.md` is checked against this requirement
- **THEN** all five areas are present, and the uncertain traits are listed together for the user to confirm

### Requirement: Simulation signals for the feed
`video-feed.md` SHALL end with the list of simulation signals the feed needs (for example motor current, battery voltage, vibration spectrum, impact events, fiber tension, scene light level). For each signal it SHALL give the unit and update rate. This list is the input to a later physics↔video interface change.

#### Scenario: Drivers are covered
- **WHEN** the signal list is compared with the drivers named in the traits
- **THEN** every driver except "frame content" and "free-running random" appears in the list with a unit and a rate

### Requirement: User sign-off
The notes SHALL be reviewed by the user before any M1 asset work starts. Corrections from the user SHALL be folded into the notes.

#### Scenario: Gate
- **WHEN** the user has not yet approved the notes
- **THEN** no M1 asset change is proposed as ready for apply
