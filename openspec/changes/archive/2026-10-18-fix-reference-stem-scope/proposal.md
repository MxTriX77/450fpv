# Proposal: fix-reference-stem-scope

**Owner:** qa-engineer · **Reviewers:** orchestrator
**Requirements:** SEC-01, SEC-06 · **Decisions:** D-013
**Found:** 2026-10-18, by the houses-and-yard analyst: `precommit.py --tree HEAD` flags an archived review line that names the contact-sheet output pattern.

## Why
The `reference-stem` rule walks every file under `reference/`, including `reference/_frames/`, which holds the stills, contact sheets and index files our own tools write, named by letter and sequence only (a letter, an underscore and a four-digit number, or a range of two). After the phase-1 analysts re-extracted the letters, 1,317 such files exist, and their stems match legitimate text (a review quoting the tool's output). The rule is meant for the pilot's files, not the tools' outputs.

## What Changes
- The stem walk skips `reference/_frames/` (and any folder under `reference/` that starts with `_`, which the tools own).
- A test: a fake root with a pilot-style file under `reference/` (still caught) and a tool-style file under `reference/_frames/X/` (not caught).

## Capabilities
### New Capabilities
- none

### Modified Capabilities
- `repo-hygiene`: the "No footage names inside text" requirement excludes the tools' own output folders from the stem walk.

## Non-goals
- No change to any other rule.

## Impact
`tools/hygiene/precommit.py`, its tests. Unblocks the `--tree` checks every QA review runs.
