# Spec Delta: repo-hygiene

## Purpose
Defines the pre-commit scan that keeps footage, OPSEC-sensitive strings and stray media out of the public repository, for every agent and every worktree.

## ADDED Requirements

### Requirement: Footage never commits
The pre-commit scan SHALL reject a commit that stages a file with a video extension, a file under `reference/`, or a file whose name matches a camera or messenger export pattern, anywhere in the tree.

#### Scenario: Planted test.mp4 is blocked
- **WHEN** `test.mp4` is force-added (`git add -f test.mp4`) and committed
- **THEN** the commit is rejected with a message naming the rule (not the file's content), and nothing is committed

#### Scenario: Footage under unity/Assets is still blocked
- **WHEN** a `.mov` file is staged under `unity/Assets/`
- **THEN** the commit is rejected

#### Scenario: Camera-style file name is blocked
- **WHEN** a file named like a camera export (for example `DJI_0001.txt` or `VID_20240101_120000.md`) is staged
- **THEN** the commit is rejected

#### Scenario: Anything under reference is blocked
- **WHEN** any file under `reference/` is force-added and committed
- **THEN** the commit is rejected

### Requirement: Media only where assets live
Image and audio files SHALL be committed only under `assets-src/` or `unity/Assets/`.

#### Scenario: Image in docs is blocked
- **WHEN** a `.png` or `.jpg` is staged under `docs/`
- **THEN** the commit is rejected

#### Scenario: Texture in unity/Assets passes
- **WHEN** a `.png` is staged under `unity/Assets/_Project/Art/`
- **THEN** the media rule does not reject it

### Requirement: No OPSEC strings in text
Staged text files SHALL contain no messenger names, and files under `docs/` SHALL contain no coordinate-like strings (decimal latitude/longitude pairs, degree-minute-second values, MGRS grid references).

#### Scenario: Messenger name is blocked
- **WHEN** a staged Markdown file contains the name of a common messenger app, in Latin or Cyrillic script, in any letter case
- **THEN** the commit is rejected and the message names the file and line number

#### Scenario: Decimal coordinates in docs are blocked
- **WHEN** a staged file under `docs/` contains a pair like `48.4567, 35.0123`
- **THEN** the commit is rejected

#### Scenario: The hook's own blocklist does not trip the hook
- **WHEN** the hook's own source files are staged
- **THEN** they pass, because the blocklist is not stored as plain text

### Requirement: No dates in notes
Lines in `docs/reference-notes/` and `docs/object-sheets/` SHALL contain no calendar dates, unless the line cites a decision id (`D-###`) or attributes a statement to the pilot.

#### Scenario: Sortie-like date in an object sheet is blocked
- **WHEN** a staged object sheet contains `seen on 2025-03-14`
- **THEN** the commit is rejected

#### Scenario: Decision date is allowed
- **WHEN** a staged note contains `(the pilot, 2026-10-17)`
- **THEN** the date rule does not reject it

### Requirement: Clean commits pass quickly
A commit with none of the above SHALL pass, and the scan SHALL finish in under 2 s for 200 staged text files on the dev machine.

#### Scenario: Ordinary code commit passes
- **WHEN** only `.cs`, `.py` and `.md` files without violations are staged
- **THEN** the hook exits 0 and the commit is created
