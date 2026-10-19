# Tasks: p1-letter-s-village-street

Branch `unity/ref/p1-letter-s-village-street` off `unity/main`. Tier: Standard (D-033). One fresh agent per task, on Opus 5.5.

## 1. Read

- [x] 1.1 [reference-analyst] Read letter S thoroughly, for its look and feel (D-038), from its three contact sheets in `reference/_frames/S/sheets/` (50 stills at 2 per second), and from single stills where a detail needs them:
  - write S's section in `docs/reference-notes/terrain.md`
  - add S to the reference views and the "Seen in" lines of every object sheet it touches, with still numbers
  - refine the numbers S measures better, labelled
  - add rows for objects no sheet has, or list them
  - write the implications list for S04, V13, V01, L01, L03, K01, E01, H01, R01 and the vehicles
  Verify that `python tools/hygiene/precommit.py --tree HEAD` passes and `openspec validate p1-letter-s-village-street --strict` is valid.

  **As built:**
  - **Notes:** `docs/reference-notes/terrain.md` §12 holds S's reading:
    - the camera: about 12.5 m (11–14 m), pitched about 30° down, 10–12 m/s; all derived from the poles and a two-storey building through the FEED-02 intrinsics;
    - the street's cross-section: road about 6 m, verge 7–9 m, poles 3–5 m from the road, house fronts 12–16 m back, houses 20–25 m apart;
    - what stands where, look and feel, a 25-row colour table, and the recreated view.
    - S is also in the at-a-glance table, in the class entries' clip lines, in §9 and in §10.
  - **Sheets:** S is in the reference views and the "Seen in" lines, with stills, of `houses-and-yard.md`, `lines-roads-debris.md`, `vegetation.md` and `earthworks-and-surfaces.md`, and S-measured numbers and feed values are added there.
  - **Not touched:** `urban-edge.md`, `vehicles.md` and `fortifications.md`: S shows none of their rows.
  - **New objects** with no row are listed in `implications.md`; they need asset-list IDs first.
  - **Implications:** `implications.md`, per asset.

- [x] 1.2 [reference-analyst] QA's R1–R4 and S1–S6, S8 (`review.md`):
  - R1: S's whole sequence and path (§12.6, `implications.md`) out of the world's instructions; keep the cross-section, classes, states and look, recreate its views as scene types, and leave the square and the building footprint to be invented (REAL-04)
  - R2: S03 `crater_spoil` cites S (spoil discs, S 29–37) with its shallow-spoil feed value
  - R3: L01's 0.5–1.5 m placement rule against S's 3–5 m stated in the sheet, the merged L01 left as built and the difference routed in `implications.md`
  - R4: poles on both sides where S 33 and S 42 show them
  - the suggestions S1–S6 and S8 (S7, the requirement rows, is the orchestrator's)
  Verify the hygiene scan and `openspec validate p1-letter-s-village-street --strict`

  **As built:**
  - **R1:** §12.6 and the world-builder item in `implications.md` copy S's cross-section, classes, states and look, and turn its views into six scene types (S 47, 10, 18, 33–36, 40, 50), each placed by the composite map. The order, the curve, the turn and the relative positions stay out; the square and the building footprint are invented. §12.2 keeps the path as the reading of the viewpoint only.
  - **R2:** S03 cites S 29–37 (spoil discs 3.5–6.5 m, derived S 36), and its shallow-spoil feed cell holds #5b4f5c (S 36).
  - **R3:** L01's placement rules state the conflict with S's 3–5 m; `implications.md` routes one rule for the typical village's line. L01 as merged is unchanged: placement isn't a generator parameter.
  - **R4:** terrain.md §12.3–12.4, L02's position row, RD01's placement and the L01 item: the line in the house-side verge (S 43–50), with poles on the green side by the square (S 33, 42).
  - **S1:** §12.2 notes the pole reads give 13–14.5 m (S 46–47) and the building 12–13 m. The 12.5 m default stays, and the S 47 side-by-side settles it.
  - **S2:** the grass values are marked as pure-grass picks; whole-patch means #648b55–#708657, down to #617c4e (S 24, 36), are the check.
  - **S3:** the dead trees are re-derived at 5.7–7.5 m (S 41, 43; S 42 not used).
  - **S4:** S's "(by eye)" values now read "measured, rough".
  - **S5:** the red-brick annex is placed at one end of the street side (S 47, 49).
  - **S6:** the ceiling is "mostly intact, holed" (S 50).
  - **S8:** Y04 variant 3 is back to 1.5–2 m until S's wall is classed.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the delta's scenarios:
  - S's reading is complete: scene, viewpoint, layout, objects, look and feel, colours labelled
  - every S citation in a sheet carries its still numbers, and every number taken from S is labelled
  - the implications list is concrete, each item with its stills
  - the sheets stay consistent: material ids, labels, IDs against the asset list
  - OPSEC: no place, no OSD value, no reference file name, nothing from `reference/` committed
