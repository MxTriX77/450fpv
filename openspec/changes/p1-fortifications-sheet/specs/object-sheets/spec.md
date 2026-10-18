## MODIFIED Requirements

### Requirement: One sheet per class group, every row covered
Each asset-list row SHALL appear in exactly one object sheet, under its ID, with the sections of `docs/object-sheets/_template.md`. Sections 1–10 and 12 of `docs/asset-list.md` are covered by seven sheets, the fortifications (§12) in `docs/object-sheets/fortifications.md`.

#### Scenario: Coverage check
- **WHEN** the IDs in the seven sheets are compared with `docs/asset-list.md`
- **THEN** every ID in sections 1–10 and 12 appears exactly once, and no sheet carries an ID the list lacks

## ADDED Requirements

### Requirement: Research-only sheets
A row that no reference letter shows SHALL say "Seen in: R" and rest on generic published sources only (standards, manufacturers' pages and datasheets, public-domain manuals, encyclopedias), each cited by URL in the row it supports. Its numbers SHALL be labelled published, derived or assumed, never measured. No source SHALL identify a place, unit, date or person.

#### Scenario: Research-only row
- **WHEN** an F row of `docs/object-sheets/fortifications.md` is read
- **THEN** it says "Seen in: R", every published number names its source, no number is labelled measured, and a reviewer finds no source tied to a named place, unit or date
