# Tasks: p1-letter-s-village-street

Branch `unity/ref/p1-letter-s-village-street` off `unity/main`. Tier: Standard (D-033). One fresh agent per task, on Opus 5.5.

## 1. Read

- [ ] 1.1 [reference-analyst] Read letter S thoroughly, for its look and feel (D-038), from its three contact sheets in `reference/_frames/S/sheets/` (50 stills at 2 per second), and from single stills where a detail needs them:
  - write S's section in `docs/reference-notes/terrain.md`
  - add S to the reference views and the "Seen in" lines of every object sheet it touches, with still numbers
  - refine the numbers S measures better, labelled
  - add rows for objects no sheet has, or list them
  - write the implications list for S04, V13, V01, L01, L03, K01, E01, H01, R01 and the vehicles
  Verify that `python tools/hygiene/precommit.py --tree HEAD` passes and `openspec validate p1-letter-s-village-street --strict` is valid.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the delta's scenarios:
  - S's reading is complete: scene, viewpoint, layout, objects, look and feel, colours labelled
  - every S citation in a sheet carries its still numbers, and every number taken from S is labelled
  - the implications list is concrete, each item with its stills
  - the sheets stay consistent: material ids, labels, IDs against the asset list
  - OPSEC: no place, no OSD value, no reference file name, nothing from `reference/` committed
