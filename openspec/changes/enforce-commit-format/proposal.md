# Proposal: enforce-commit-format

**Owner:** qa-engineer · **Reviewers:** orchestrator
**Requirements:** PRC-03, SEC-06 · **Decisions:** D-029 · **VISION:** —
**Lesson:** four subjects over 72 characters reached `unity/main` through `setup-asset-pipeline` (#44) because only eyes checked the rule.

## Why
`AGENTS.md` fixes the commit format (`type(scope): summary`, lower case, ≤ 72 characters, subject only, no trailers, no co-author). Agents keep breaking the length by a few characters, and QA catches it only after the branch is pushed. The `commit-msg` hook already exists; it should enforce the format so a bad subject never commits.

## What Changes
- The `commit-msg` hook rejects a message whose first line is longer than 72 characters, does not match `type(scope): summary` (or `type: summary`) with a known type, starts with an upper-case letter after the colon, or whose later lines carry a body, a trailer or a co-author line. Merge commits git writes itself (branch, branches, remote-tracking branch, tag, commit, pull request) pass.
- One test per rule in the hygiene suite.
- `AGENTS.md` mentions that the hook enforces it.

## Capabilities
### New Capabilities
- none

### Modified Capabilities
- `repo-hygiene`: one ADDED requirement, "Commit message format".

## Non-goals
- No server-side check (GitHub has no commit-message rules on this plan).
- No rewrite of the four existing long subjects.

## Impact
`tools/hygiene/precommit.py`, its tests, `AGENTS.md`.
