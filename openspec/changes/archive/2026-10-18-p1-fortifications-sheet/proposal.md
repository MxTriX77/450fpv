# Proposal: p1-fortifications-sheet

**Owner:** reference-analyst · **Reviewers:** asset-artist (buildability), physics-engineer (the physics roles, by reading), qa-engineer
**Requirements:** AST-01, AST-02, AST-04, AST-05, AST-06, SEC-01, SEC-02, WLD-01 · **Decisions:** D-024, D-028, D-035 · **VISION:** realism as a copy of the real thing; the fiber's snags are part of the flying
**Source:** `docs/asset-list.md` §12 (F01–F04, starting ranges to pin), D-035 (the pilot's words on concertina wire, dragon's teeth, hedgehogs and checkpoints), `docs/object-sheets/_template.md`, `docs/handover-2026-10-18.md` §5.3.

## Why
D-035 adds fortifications to the world: concertina wire, dragon's teeth, anti-tank hedgehogs and a checkpoint kit. They are P1 for the world, so phase 3's layout needs them sized and their physics roles settled. No reference letter shows them, so the sheet is research only, from generic published sources. F01 is the fiber's and the props' worst snag, and the spaces between teeth and hedgehog arms are gaps the drone may try, so the sheet must state them as numbers the asset and the sim can use.

## What Changes
- `docs/object-sheets/fortifications.md` with F01–F04, every template section, every number labelled published (with its source), derived (with the derivation) or assumed. No row is labelled measured, because no letter shows these objects.
- The physics role in the pipeline's terms: every strand of F01 an `edge` snag with the barb pitch and the tape section; the coil's wind porosity; the clear spaces between dragon's teeth and between hedgehog arms measured against the drone (0.574 m nose-first, 0.706 m diagonal); material ids from `assets-src/physics_materials.json`, or proposed ids flagged for physics sign-off.
- Placement rules for the world from generic doctrine and published descriptions (rows, spacing, which approaches), feeding phase 3's layout (WLD-01, D-035).
- `docs/asset-list.md` §12: the "Key size" cells replaced by the sheet's pinned defaults.
- A buildability review (`review-buildability.md`) and a physics reading (`review-physics.md`) in this change folder.

## Capabilities
### New Capabilities
- none

### Modified Capabilities
- `object-sheets`: the coverage requirement covers asset-list §12 as a seventh sheet; ADDED: a research-only sheet's sources and labels.

## Non-goals
- No generators, models or textures (later Standard-tier asset changes, after G1).
- No layout placement (phase 3).
- No reference analysis: no letter shows fortifications. If the pilot's references later show one, a follow-up adds the letter.
- No edit to `assets-src/physics_materials.json` (a new id is only proposed here; the physics sign-off adds it in the asset change).

## Impact
`docs/object-sheets/fortifications.md` (new), `docs/asset-list.md` §12, `openspec/specs/object-sheets/spec.md` (on archive). Branch `unity/ref/p1-fortifications-sheet` off `unity/main`.
