# Proposal: setup-repo-hygiene

**Owner:** qa-engineer · **Reviewers:** a fresh qa-engineer agent, orchestrator
**Requirements:** SEC-01, SEC-02, SEC-06, PRC-10 · **Decisions:** D-013 · **VISION:** OPSEC
**Lessons:** knowledge.md §14.2 (three OPSEC slips that a hook would have stopped)

## Why
The repo is public and the pilot's footage sits next to it on disk. The first attempt leaked a reference file name, a messenger name and other slips into commits. A pre-commit hook makes these mistakes impossible to commit by accident, for every agent and every worktree.

## What Changes
- A versioned pre-commit hook in `.githooks/pre-commit` that runs `tools/hygiene/precommit.py` on the staged files.
- The scan blocks: media files outside `assets-src/` and `unity/Assets/`; footage extensions anywhere; footage-like camera and export file names anywhere; anything under `reference/`; messenger names in staged text; coordinate-like strings in `docs/`; dates in the notes (`docs/reference-notes/`, `docs/object-sheets/`) unless the line cites a decision or the pilot.
- pytest tests that plant each violation in a throwaway repo and prove it is blocked, plus a clean commit that passes.
- The activation one-liner (`git config core.hooksPath .githooks`), documented in `docs/environment.md`.

## Capabilities
### New Capabilities
- `repo-hygiene`: what the pre-commit scan blocks and allows.

### Modified Capabilities
- none

## Non-goals
- No server-side or CI scan (no CI in phase 0).
- No history rewrite of the Godot attempt's old slips (the pilot chose not to).
- No escape hatch inside the hook. `--no-verify` stays forbidden by `AGENTS.md`.

## Impact
New `.githooks/`, `tools/hygiene/`. Every later commit in every worktree passes through it once `core.hooksPath` is set (shared repo config).
