# Proposal: p1-object-sheets

**Owner:** reference-analyst · **Reviewers:** asset-artist (buildability), qa-engineer
**Requirements:** AST-02, AST-04, AST-05, AST-06, SEC-01, SEC-02, PRC-06, PRC-09 · **Decisions:** D-013, D-024, D-028 · **VISION:** realism as a copy of the references
**Brief:** [docs/briefs/phase-1-assets.md](../../../docs/briefs/phase-1-assets.md), flow step 1

## Why
Every asset in `docs/asset-list.md` is rebuilt to match the pilot's references (D-028). Before a generator is written, someone has to read the footage and photos once, class by class, and write down dimensions, construction, materials, colours, damage, physics role and parameters. Doing it now, in six narrow passes, means no later phase opens the footage again.

## What Changes
- Six object sheets under `docs/object-sheets/`, one per class group, following `_template.md`, covering every row of the asset list (P1 and P2) in that group.
- The stills behind them: re-extracted with `tools/reference/extract_frames.py` and tiled with `contact_sheet.py` into the ignored `reference/_frames/`; the letter-to-clip map lives only there.
- A buildability review of all six sheets by the asset-artist, recorded in the change folder.

## Capabilities
### New Capabilities
- `object-sheets`: what an object sheet must contain and how it stays OPSEC-clean.

### Modified Capabilities
- none

## Non-goals
- No models, textures or generators (the following changes).
- No new reference analysis of the feed or the wind (already measured).
- No drone research (phase 5).

## Impact
`docs/object-sheets/*.md`; local-only frames and sheets under `reference/_frames/`. The sheets are the input to every phase-1 asset change.
