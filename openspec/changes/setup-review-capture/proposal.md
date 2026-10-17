# Proposal: setup-review-capture

**Owner:** tech-artist · **Reviewers:** asset-artist, qa-engineer
**Requirements:** AST-12, AST-04 (scale bar), AST-05 (collision overlay), REAL-03 groundwork · **Decisions:** D-020, D-028 · **VISION:** realism judged by eye
**Lessons:** knowledge.md §14.1 ("look at every asset": the chequerboard walls passed every automated check)

## Why
Every asset must be looked at before it enters the gallery. The review sheet makes that cheap and identical for all 100 assets: the same distances, the same light, the same overlay.

## What Changes
- `Fpv450.Review.Capture`: a Unity batchmode method that renders the AST-12 sheet for one asset id: shots at 1.5, 10, 50 and 200 m, an 8-view turntable and a collision overlay, each in day and overcast light, with a 1.8 m human scale bar.
- `tools/build_asset.ps1` gains the review step, so one command goes params → FBX → prefab → sheet.

## Capabilities
### New Capabilities
- `asset-review-sheet`: the content, layout and location of the automated asset review sheet.

### Modified Capabilities
- none

## Non-goals
- No final look (sky, post, time of day): phase 1 look setup. The review lighting is a fixed neutral rig.
- No side-by-side with references: those are local-only sheets made in phase 1 (REAL-03).
- No through-the-feed view: phase 4.

## Impact
New editor code under `unity/Assets/_Project/Editor/Review/` and a review lighting rig under `unity/Assets/_Project/Settings/Review/`; one added step in `tools/build_asset.ps1`. Sheets go to the ignored `review-sheets/`.
