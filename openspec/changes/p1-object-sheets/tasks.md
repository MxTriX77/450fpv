# Tasks: p1-object-sheets

## 1. Sheets (one fresh reference-analyst per sheet; at most two in parallel)

- [x] 1.1 [reference-analyst] `docs/object-sheets/vegetation.md` (V01–V21), from letters F, H, G, D, C, A, L plus research; verify every V row is present with labelled dimensions, materials, colours, physics role and a generator table, and the commit passes the hooks
- [x] 1.2 [reference-analyst] `docs/object-sheets/houses-and-yard.md` (H01–H06, O01–O06, C01–C02, Y01–Y11), from E, J, C, D, I plus research; same checks, plus every opening's clear width against the drone
- [x] 1.3 [reference-analyst] `docs/object-sheets/earthworks-and-surfaces.md` (E01–E07, S01–S13), from A, B, F, G, H, L, P and terrain.md; same checks, plus the surface table rows cited
- [x] 1.4 [reference-analyst] `docs/object-sheets/lines-roads-debris.md` (L01–L05, RD01–RD03 roads, K01–K06, K08–K10), from C, D, E, H, J, K, L, M, O plus research; same checks, plus wire diameters and snag geometry
- [x] 1.5 [reference-analyst] `docs/object-sheets/vehicles.md` (R01–R09, K07), from M, N, O plus published specs; same checks, plus the A/B/F state kits and debris fields
- [x] 1.6 [reference-analyst] `docs/object-sheets/urban-edge.md` (U01–U03, P01–P06), from I, K plus research; same checks, plus the block's damage kit and rebar
- [x] 1.7 [asset-artist] Buildability review of all six sheets, written to `openspec/changes/p1-object-sheets/review-buildability.md`; sheets fixed where sent back; verify every generator table has ranges and effects
- [x] 1.8 [reference-analyst] Add the U02b row (collapsed timber roof pile, letter I) to `docs/object-sheets/urban-edge.md` with every template section, consistent with U02 and the debris kit; verify the coverage check passes for every ID in asset-list §1–10 and the commit passes the hooks
- [x] 1.9 [reference-analyst] QA rework R1–R5: generic research sources only, regional framing and device wording removed, the 29 unlabelled and 7 letterless rows labelled, D-032 applied (measured sizes beat AST-04's assumed ones; H06 keeps its door)
- [x] 1.10 [asset-artist] QA rework R6: a U02b entry (the `roof_pile` generator) in `review-buildability.md`, and its two stale "sent back" items closed

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios (coverage, labels, OPSEC, buildability recorded); first pass CHANGES REQUESTED (`review.md` R1–R6), re-review after 1.9 and 1.10
