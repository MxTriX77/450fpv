# Design: p1-object-sheets

## Context
`reference/` holds the pilot's clips and photos (letters A–P, knowledge.md §9.1); `reference/_frames/<letter>/` already holds stills from the first attempt. `docs/reference-notes/terrain.md` and `knowledge.md` §9 carry the measured sizes, colours and surface table. `tools/reference/` makes stills and contact sheets. The hygiene hooks block file names, dates and coordinates.

## Decisions
1. **Six groups, mapped to the asset list:**
   | Sheet | IDs | Letters |
   |---|---|---|
   | `vegetation.md` | V01–V21 | F, H, G, D, C, A, L |
   | `houses-and-yard.md` | H01–H06, O01–O06, C01–C02, Y01–Y11 | E, J, C, D, I |
   | `earthworks-and-surfaces.md` | E01–E07, S01–S13 | A, B, F, G, H, L, P |
   | `vehicles.md` | R01–R09 (vehicles), K07 | M, N, O |
   | `urban-edge.md` | U01–U03, P01–P06 | I, K |
   | `lines-roads-debris.md` | L01–L05, RD01–RD03 (roads), K01–K06, K08–K10 | C, D, E, H, J, K, L, M, O |
2. **Stills:** each analyst re-extracts its letters with `extract_frames.py` at 1 fps (clips) and tiles 24-still contact sheets with `contact_sheet.py`; photos are read as single stills. Reading happens only inside the subagent; nothing from `reference/` is copied anywhere else.
3. **The letter map** (letter → clip) is kept only in `reference/_frames/letters.md` (ignored). An analyst that needs it reads it there; one that finds it missing rebuilds it from the descriptions in knowledge.md §9.1 and writes it there.
4. **Reuse before re-deriving:** terrain.md sizes, colours and the surface table are cited, not re-measured, unless a still shows something the note lacks.
5. **Research** for R rows: public, non-identifying sources (typical construction, vehicle specs, war-damage patterns). Vehicle sizes are published figures.
6. **Physics ids** come from `assets-src/physics_materials.json`; a needed new id (soil, straw, glass crumbs, rubber, fabric, plastic are likely) is proposed in the sheet's open points for the physics engineer, never invented silently.
7. **Order and parallelism:** vegetation and houses-and-yard first (the look-lock assets V01, V13, H01 come from them), then earthworks-and-surfaces (S04, E01) and lines-roads-debris (L01, L03, R01), then vehicles and urban-edge. At most two analysts at a time.

## Risks / Trade-offs
- [PAL stills are coarse] → sizes come from known objects in frame (doors, bricks, poles) and from published figures; measured values carry a range.
- [A sheet drifts into specifics] → the hygiene scan plus the buildability and QA reviews read for OPSEC, not just content.
