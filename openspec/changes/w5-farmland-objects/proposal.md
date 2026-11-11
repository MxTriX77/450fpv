# Proposal: w5-farmland-objects

**Owner:** world-builder (placement, the hole mask, footprints, applying them at load) · **Also building:** asset-artist (E01's filler band) · **Reviewers:** tech-artist (the drawn hole edge), qa-engineer
**Requirements:** WLD-05, WLD-12, WLD-16, AST-05 · **Decisions:** D-088, D-089, D-090, D-092, D-094, D-095 · **VISION:** §2 (the trench and the village the pilot flies over) · **Plan:** `docs/final-world-plan.md` §4, unit W5 · **Tier:** Standard

## Why
D-090 adds our own objects to the farmland: one `E01_curving` trench in each field F1–F11, and the houses `H01_I` and `H01_D` and the cars `R01_A` and `R01_F` in the village. A trench is cut into the ground, so it needs terrain holes, the sim's hole mask, and its own fillers as the only ground there (WLD-16). Houses and cars stand level, so the terrain under their footprints is flattened.

The terrain lives in the shared local store and can't be edited in place or pushed. So both edits are data in `maps/farmland/`, applied at load to the sim and to a runtime copy of the drawn terrain (D-095).

W2's design §4 found that E01's filler band (2.35 m on the run, 2.1 m at the niches) is too narrow for the farmland's 0.977 m hole cells. Rules 2 and 3 there, no naked hole, need a band of at least 3.56 m. Otherwise a cut leaves either a fall through the world or sod roofing part of the trench.

## What Changes
- **E01's filler band** (asset-artist) widens to at least 3.6 m. It is rebuilt with its metadata, review sheet and trench physics rows.
- **Placement** (world-builder): a seeded random spot and yaw per field, fully inside the field with a margin. The village objects go on free ground, clear of the ruins, debris, trees and the start point (D-092). The rows go into the map package.
- **The hole mask and the footprints** (world-builder):
  - derived from the placed trenches and objects, written to `maps/farmland/`;
  - `validate.py` recomputes the mask and requires equality, and checks that every point of every hole cell lies inside a filler band;
  - Unity's inverted hole sense is checked with a one-cell probe.
- **Applied at load** (world-builder): the sim's heightfield takes the mask and the flattened footprints, and the player edits a runtime copy of the drawn terrain the same way. The store's terrain stays untouched.

## Non-goals
- No change to the friend's ruins, debris or vegetation.
- No new asset kinds. The placement count stays as D-090 sets it.

## Impact
`assets-src/earthworks/E01_curving.json` and its metadata; `maps/farmland/` (placements, `holes/`, footprints) and `tools/map/validate.py`; the sim package's World folder (the mask and footprints at load); the player's farmland terrain setup.
