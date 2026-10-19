# object-sheets Specification

## Purpose
Defines what an object sheet holds for each asset class, so a generator can be written from it and judged against the reference, and how the sheet stays OPSEC-clean.

## Requirements

### Requirement: One sheet per class group, every row covered
Each asset-list row SHALL appear in exactly one object sheet, under its ID, with the sections of `docs/object-sheets/_template.md`. Sections 1–10 and 12 of `docs/asset-list.md` are covered by seven sheets, the fortifications (§12) in `docs/object-sheets/fortifications.md`.

#### Scenario: Coverage check
- **WHEN** the IDs in the seven sheets are compared with `docs/asset-list.md`
- **THEN** every ID in sections 1–10 and 12 appears exactly once, and no sheet carries an ID the list lacks

### Requirement: Measured, labelled numbers
Every dimension SHALL give a range and a chosen default, each labelled measured (from a letter, with the still number), published (with its source), derived (with the derivation) or assumed. Exact sizes fixed by AST-04 (brick, doors, storeys) SHALL be used as given.

#### Scenario: Label present
- **WHEN** a dimension row is read
- **THEN** it carries one of the four labels, and a measured one names its letter

### Requirement: Materials and colours resolve to the pipeline
Every material SHALL name a CC0 or CC-BY set (an existing manifest id, or a candidate with its source page) and any derived texture; every colour SHALL give the photo albedo and, where a clip shows it, the feed check value, both as sRGB.

#### Scenario: Material resolves
- **WHEN** a material line is read
- **THEN** it names a set id or a source page with a licence of CC0 or CC-BY, never a store or paid source

### Requirement: Physics role in the pipeline's terms
The physics section SHALL give a collision plan in primitives, material ids from `assets-src/physics_materials.json` (or a proposed id flagged for physics sign-off), every opening with its clear width against the drone's 0.574 m nose-first and 0.706 m diagonal, wind-volume parts with porosity, snag hazards and landable surfaces.

#### Scenario: Gap stated against the drone
- **WHEN** an asset has an opening
- **THEN** the sheet gives its narrowest clear width and says whether the drone passes

### Requirement: OPSEC-clean
A sheet SHALL cite clips and photos by letter only, describe classes rather than identifiable places, buildings, dates or people, and carry no reference file names, OSD values or coordinates.

#### Scenario: Hygiene scan passes
- **WHEN** the sheet is committed
- **THEN** the pre-commit scan reports no findings, and a reviewer finds no identifiable specifics

### Requirement: Buildable
Each sheet SHALL be reviewed by the asset-artist for buildability: every parameter in the generator table has a range and an effect, every damage state says what changes physically, and nothing requires a hand-sculpted part the design does not name.

#### Scenario: Buildability review recorded
- **WHEN** a change adds or reworks a sheet
- **THEN** `review-buildability.md` in that change's folder lists, per row, what is buildable as written and what was sent back, and the sheet reflects the fixes

### Requirement: Research-only sheets
A row that no reference letter shows SHALL say "Seen in: R" and rest on generic published sources only (standards, manufacturers' pages and datasheets, public-domain manuals, encyclopedias), each cited by URL in the row it supports. Its numbers SHALL be labelled published, derived or assumed, never measured. No source SHALL identify a place, unit, date or person.

#### Scenario: Research-only row
- **WHEN** an F row of `docs/object-sheets/fortifications.md` is read
- **THEN** it says "Seen in: R", every published number names its source, no number is labelled measured, and a reviewer finds no source tied to a named place, unit or date

### Requirement: A letter is read for its look and feel
When a reference letter is added, the reference notes SHALL hold its reading: the scene; the viewpoint's height and path, estimated from the scene; the layout with labelled sizes; what stands where; and its look and feel (composition, light, season, colours sampled and labelled, wear, the density of clutter and vegetation, and what reads near, at middle distance and far). Every object sheet whose objects the letter shows SHALL cite it in its reference views and in each such row's "Seen in", with still numbers.

#### Scenario: Letter S read in
- **WHEN** the reference notes and the object sheets are read after letter S is added
- **THEN** the notes hold S's reading with every part above, every sheet row whose object S shows cites S with still numbers, every number taken from S is labelled measured with its still, and the change lists what S implies for each asset already built or in flight

#### Scenario: Nothing of the place
- **WHEN** the hygiene scan runs over the tree
- **THEN** it finds no place name, coordinate, OSD value or reference file name, and nothing under `reference/` is tracked
