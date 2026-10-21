# Proposal: p1-letter-s-village-street

**Owner:** reference-analyst · **Reviewer:** qa-engineer
**Requirements:** REAL-01, REAL-02, REAL-03, REAL-04, WLD-01, WLD-14, AST-02, SEC-01, SEC-02 · **Decisions:** D-028, D-038 · **Tier:** Standard (D-033)

## Why
The pilot added a village street seen from their own flight (letter S, local only). They asked that the typical village look like it, with cars added, and that every reference be read thoroughly for its look and feel (D-038). The reference notes and the object sheets don't know S yet. The assets merged (S04, V13, L01, L03, K01), in flight (V01, E01) and next (H01, R01) were all built from the other letters.

## What Changes
- **S's reading in `docs/reference-notes/terrain.md`:**
  - the scene, and the camera's path and height, estimated from the scene
  - the street's layout and widths
  - what stands where: houses, yards, fences and gates, verges and ditches, poles and wires, trees and shrubs, the road surface, clutter, damage
  - its look and feel: composition, light and season, colours (sampled and labelled), wear, the density of clutter and vegetation, and what reads at 5, 20 and 50 m
- **S in the object sheets:** every sheet whose objects S shows gains S in its reference-view table and in each such row's "Seen in", with still numbers.
  - Numbers S measures better are refined and labelled measured, with the still.
  - An object S shows that no sheet has gets a row, or a line in the hand-back if its row belongs to a later change.
- **What S implies for each asset**, built, in flight or next: S04, V13, V01, L01, L03, K01, E01, H01, R01 and the vehicles. Each is a concrete difference with its stills, for the orchestrator to route as tasks.
- **S's viewpoint and framing** in the reference notes, so the gallery and the world (WLD-14) can set S's recreated view.

## Capabilities
### Modified Capabilities
- `object-sheets`: a letter added later is read into the notes and the sheets for its look and feel, not only its objects.

## Non-goals
- No models, textures or generators. No changes to merged assets: their follow-ups are routed as tasks after this change.
- No world layout (phase 3). S is a pattern, never a place.
- No more Stalin-era blocks: U01 stays one (D-038).

## Impact
Docs only: `docs/reference-notes/terrain.md` and the sheets under `docs/object-sheets/`. Nothing from `reference/` is committed; S's stills stay in the ignored `reference/_frames/S/`.
