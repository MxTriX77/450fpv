## MODIFIED Requirements

### Requirement: One sheet per class group, every row covered
Each asset-list row SHALL appear in exactly one object sheet, under its ID, with the sections of `docs/object-sheets/_template.md`. Sections 1–10 and 12 of `docs/asset-list.md` are covered by seven sheets, the fortifications (§12) in `docs/object-sheets/fortifications.md`.

#### Scenario: Coverage check
- **WHEN** the IDs in the seven sheets are compared with `docs/asset-list.md`
- **THEN** every ID in sections 1–10 and 12 appears exactly once, and no sheet carries an ID the list lacks

### Requirement: Buildable
Each sheet SHALL be reviewed by the asset-artist for buildability: every parameter in the generator table has a range and an effect, every damage state says what changes physically, and nothing requires a hand-sculpted part the design does not name.

#### Scenario: Buildability review recorded
- **WHEN** a change adds or reworks a sheet
- **THEN** `review-buildability.md` in that change's folder lists, per row, what is buildable as written and what was sent back, and the sheet reflects the fixes

## ADDED Requirements

### Requirement: Research-only sheets
A row that no reference letter shows SHALL say "Seen in: R" and rest on generic published sources only (standards, manufacturers' pages and datasheets, public-domain manuals, encyclopedias), each cited by URL in the row it supports. Its numbers SHALL be labelled published, derived or assumed, never measured. No source SHALL identify a place, unit, date or person.

#### Scenario: Research-only row
- **WHEN** an F row of `docs/object-sheets/fortifications.md` is read
- **THEN** it says "Seen in: R", every published number names its source, no number is labelled measured, and a reviewer finds no source tied to a named place, unit or date
