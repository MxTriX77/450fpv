# Spec Delta: repo-hygiene

## MODIFIED Requirements

### Requirement: No footage names inside text
Staged Markdown, text, YAML and JSON files, and every commit message, SHALL contain no camera-export or messenger-export file names and no `<name>.<video extension>` tokens. When a local `reference/` folder exists (in this worktree or in the main checkout), every staged text file and every commit message SHALL also contain no stem of a file in it: every media file's stem (video and image extensions), and any other file's stem when it contains a digit, for stems of at least 8 characters that contain a digit, `_`, `-` or a space. Folders directly under `reference/` whose name starts with `_` (such as `reference/_frames/`, where the reference tools write letter-named stills, sheets and indexes) are the tools' own outputs and SHALL be skipped by the stem walk. Those stems are read at scan time only, and are never stored, cached or printed.

#### Scenario: Camera-style name written in a review is blocked
- **WHEN** a staged `review.md` under `openspec/` mentions a camera-export file name built from the pattern
- **THEN** the commit is rejected, naming the file and line number only

#### Scenario: Reference file stem in a note is blocked
- **WHEN** a local `reference/` holds a clip with a distinctive stem, and a staged note mentions that stem
- **THEN** the commit is rejected, and the output does not contain the stem

#### Scenario: Footage name in a commit message is blocked
- **WHEN** a commit message contains a `<name>.<video extension>` token or a reference file stem
- **THEN** the commit is rejected by the commit-message hook

#### Scenario: Extension patterns pass
- **WHEN** a staged file contains a bare extension pattern such as `*.mp4` (as in `.gitignore`)
- **THEN** the name rule does not reject it

#### Scenario: Tool output stems are not footage
- **WHEN** a sheet image named by letter and sequence range exists under `reference/_frames/<letter>/sheets/` and a staged note mentions its stem
- **THEN** the stem rule does not reject it, while a pilot-style file directly under `reference/` with the same stem would
