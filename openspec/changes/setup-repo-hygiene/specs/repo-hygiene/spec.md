# Spec Delta: repo-hygiene

## Purpose
Defines the pre-commit and commit-message scans that keep footage, OPSEC-sensitive strings and stray media out of the public repository, for every agent and every worktree, without breaking Git LFS.

## ADDED Requirements

### Requirement: Footage never commits
The pre-commit scan SHALL reject a commit that stages a file with a video extension, a file under `reference/`, or a file whose name matches a camera or messenger export pattern, anywhere in the tree. Findings SHALL name the rule and a masked location (folder and extension), never the file name or content.

#### Scenario: Planted test clip is blocked
- **WHEN** a file named `test` with the `.mp4` extension is force-added and committed
- **THEN** the commit is rejected with the rule id and a masked location, and nothing is committed

#### Scenario: Footage under unity/Assets is still blocked
- **WHEN** a `.mov` file is staged under `unity/Assets/`
- **THEN** the commit is rejected

#### Scenario: Camera-style file name is blocked
- **WHEN** a file whose base name follows a camera-export pattern (a camera-maker or phone prefix followed by a frame or date number) is staged, whatever its extension
- **THEN** the commit is rejected

#### Scenario: Anything under reference is blocked
- **WHEN** any file under `reference/` is force-added and committed
- **THEN** the commit is rejected

### Requirement: No footage names inside text
Staged Markdown, text, YAML and JSON files, and every commit message, SHALL contain no camera-export or messenger-export file names and no `<name>.<video extension>` tokens. When a local `reference/` folder exists, every staged text file and every commit message SHALL also contain no stem of a file in it, for stems of at least 8 characters that contain a digit, `_`, `-` or a space. Those stems are read at scan time only, and are never stored, cached or printed.

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

### Requirement: Media only where assets live
Image and audio files SHALL be committed only under `assets-src/` or `unity/Assets/`.

#### Scenario: Image in docs is blocked
- **WHEN** a `.png` or `.jpg` is staged under `docs/`
- **THEN** the commit is rejected

#### Scenario: Texture in unity/Assets passes
- **WHEN** a `.png` is staged under `unity/Assets/_Project/Art/`
- **THEN** the media rule does not reject it

### Requirement: No OPSEC strings in text
Staged text files and commit messages SHALL contain no messenger names. Files under `docs/` and `openspec/`, root Markdown files and commit messages SHALL contain no coordinate-like strings inside the regional box (latitude 40–60°, longitude 15–50°, which covers Ukraine and its neighbours with margin): decimal pairs in either order, with a point or a decimal comma; degree-minute-second values with or without a hemisphere letter; hemisphere-prefixed decimals; MGRS grid references. Any staged text file SHALL contain no labelled latitude or longitude value (`lat`, `lon`, `lng`, `latitude`, `longitude` keys with a number).

#### Scenario: Messenger name is blocked
- **WHEN** a staged Markdown file contains the name of a common messenger app, in Latin or Cyrillic script, in any letter case
- **THEN** the commit is rejected and the message names the file and line number

#### Scenario: Ordinary word that is also a messenger name passes
- **WHEN** a staged file uses, in its ordinary sense, an English word that is also a messenger's name
- **THEN** the messenger rule does not reject it, because such names match only in unambiguous forms (a domain, or followed by "messenger" or "app")

#### Scenario: Regional decimal coordinates are blocked
- **WHEN** a staged file under `docs/` or `openspec/` contains a decimal pair inside the regional box (built at test time, never written into the repo)
- **THEN** the commit is rejected

#### Scenario: Labelled coordinate in JSON is blocked
- **WHEN** a staged JSON file contains a `"lat"` key with a numeric value
- **THEN** the commit is rejected

#### Scenario: Physics coefficients pass
- **WHEN** a staged design contains a pair of 4-decimal numbers outside the regional box
- **THEN** the coordinate rule does not reject it

#### Scenario: The hook's own blocklist does not trip the hook
- **WHEN** the hook's own source files are staged
- **THEN** they pass, because the blocklist is not stored as plain text

### Requirement: No dates in notes
Lines in `docs/reference-notes/` and `docs/object-sheets/` SHALL contain no calendar dates, in ISO, dotted, slashed, compact (`YYYYMMDD`) or month-name form (English, Ukrainian or Russian), unless the line cites a decision id (`D-###`) or the date directly follows an attribution to the pilot (`pilot, <date>`, optionally preceded by "the" and in brackets).

#### Scenario: Sortie-like date in an object sheet is blocked
- **WHEN** a staged object sheet contains `seen on 2025-03-14`
- **THEN** the commit is rejected

#### Scenario: Date on a line that merely mentions the pilot is blocked
- **WHEN** a staged note contains `the pilot flew there on 14 March 2025`
- **THEN** the commit is rejected

#### Scenario: Attributed decision date is allowed
- **WHEN** a staged note contains `(the pilot, 2026-10-17)`
- **THEN** the date rule does not reject it

### Requirement: Git LFS keeps working
The versioned hooks folder SHALL carry git-lfs's `pre-push`, `post-checkout`, `post-commit` and `post-merge` hooks, so that LFS objects are still uploaded and checked out when `core.hooksPath` points at `.githooks/`.

#### Scenario: Push uploads LFS objects
- **WHEN** `core.hooksPath` is `.githooks` and a commit with an LFS-tracked file is pushed to a local bare remote
- **THEN** the remote's LFS store holds the object

#### Scenario: No untracked hook files appear
- **WHEN** an LFS-tracked file is added and committed in a worktree with `core.hooksPath` set
- **THEN** `git status` shows no new files under `.githooks/`

### Requirement: Clean commits pass quickly
A commit with none of the above SHALL pass, and the scan itself (excluding the `sh` and `python` start-up, which costs 0.25–1.1 s on the dev machine, measured) SHALL finish in under 2 s for 200 staged text files. The rules SHALL report zero findings over the files already on `unity/main`.

#### Scenario: Ordinary code commit passes
- **WHEN** only `.cs`, `.py` and `.md` files without violations are staged
- **THEN** both hooks exit 0 and the commit is created

#### Scenario: Existing tree is clean
- **WHEN** every file tracked on `unity/main` is scanned
- **THEN** there are zero findings
