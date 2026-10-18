# Design: p1-v13-shrub

## Context
The pipeline after `p1-pipeline-hardening` (ranges, the two-sided audit, `derive.py`, `bark` and `twig` in `physics_materials.json` with `twig` at 30 N/m ζ 0.2 edge 2.5 mm, `bark` rigid). The sheet §V13 gives: heights 1–1.5 / 1.5–2.5 / 2.5–3.5 m (defaults 1.2 / 2 / 3), widths 1–2 / 1.5–3 / 2–3 m (1.5 / 2.2 / 2.5), 5–20 stems (10), stem diameter 10–30 mm (18), branching from 0.3–1 m (0.6; the tall variant bare to 1 m), twigs 3–8 mm, leaf 2–6 cm (4), porosity 0.35 summer / 0.5 autumn / 0.7 bare, foliage albedo #58602c (assumed) against H's #817651 feed value, stems #7a7466. The vegetation generator of V01 (lane B, next) reuses the skeleton and card modules written here.

## Goals / Non-Goals
**Goals:** a shrub that copies the H edge clump and the F understory from three params files; the skeleton and card modules V01 will grow; a crown shell the audit can judge.
**Non-Goals:** see proposal; no baked impostor, no procedural species, no per-leaf geometry.

## Decisions
1. **Skeleton** (`lib/skeleton.py`): `stems` stems from a base disc of radius `base_r_m` 0.1–0.25 m (assumed), each a tapered tube rising with `arch` 0.1–0.3 (the sheet: the horizontal drift of the tip as a share of height), branching twice above `bare_base_m` into twigs of 3–8 mm; stems ≥ 10 mm are 6-sided tubes at LOD0, 4-sided at LOD1, dropped at LOD2; twigs are 3-sided at LOD0 only. Seeded by `seed`.
2. **Card mass** (`lib/cards.py`): leaf clusters as double-sided quads (cards) of `card_m` 0.15–0.3 m (assumed: a 4 cm leaf atlas reads at 1.5 m as a cluster of 10–20 leaves per card) placed along the twig ends with an outward bias, `cards_per_m3` giving about 600–900 cards at LOD0 for the mid variant (derived from the 12 000-triangle budget: 2 triangles a card, the stems taking about 2 000), halved at LOD1, LOD2 three crossed cards of the whole clump using the same atlas scaled up (assumed, the usual far card), cull where the clump's height drops under 2 px (935 px/rad: 3 m → about 1 400 m; capped at 800 m, assumed, since the belt at that range is V01's band). The atlas UV picks a leaf cluster cell per card from the LeafSet by seed. `leaf_cover` 0–1 scales the card count, `autumn` 0–1 lerps the tint towards the sheet's yellow.
3. **Materials.** Stems: `bark_brown_02` (Poly Haven, 1k is enough for an 18 mm stem; derived) with the sheet's grey-green tint. Leaves: one ambientCG LeafSet with small oval leaves (the honeysuckle look), `alpha_clip` 0.5, `two_sided`, tinted so the mean lit albedo sits at #58602c; a second LeafSet (small leaflets) only if the artist judges the caragana look necessary against H, within the 2K budget. The importer gains `alpha_clip` and `two_sided` in the recipe schema (additive). Alternative rejected: a custom leaf shader with translucency (tech-artist, after G1 if the look asks for it).
4. **Collision and the shell.** Every stem ≥ 10 mm: a capsule per straight segment (2–3 per stem, so 20–60 capsules), material `bark`, within 5 cm of its tube. The leafy mass: one `twig` sphere (or two for the tall variant: a sphere per leafy band) whose radius encloses every card; the audit's shell rule (one-sided, inside or within 5 cm) makes this checkable. Snag: a `hook` from base to tip per stem, `radius_m` half the stem's base diameter (the sheet: stems at leg height and the twig mesh; the mesh is the shell's `twig` contact). Gaps: none. Wind: one box around the clump with porosity by state (0.35 summer). Landable: none.
5. **Params files** (`assets-src/vegetation/V13_{low,mid,tall}.json`): `seed`, `height_m`, `width_m`, `stems`, `stem_d_mm`, `arch`, `bare_base_m`, `leaf_cover`, `autumn`, `spines`, `fruit_tufts`, `card_m`, `cards_per_m3`, `lod` { `lod1_switch_m`, `lod2_switch_m`, `cull_m` } with ranges and effects in `PARAMS`; `check` rejects `bare_base_m` ≥ `height_m` and a card budget over 12 000 triangles.

## Risks / Trade-offs
- [Cards look like cards at 1.5 m] → the atlas cell choice and `card_m` are params; judged at G1 against H; a leaf shader with translucency is the fallback (tech-artist).
- [The two-sided audit over tubes and capsules] → capsules follow the straight segments; a curved stem's tube deviates at most sag ≈ `arch` × height / segments², under 5 cm for 3 segments on a 3 m stem (derived).
- [One LeafSet for three species looks] → accepted for G1; the species share is the sheet's open point for the look-lock.
- [Alpha clipping and MSAA in the review capture] → alpha-to-coverage is not required; clipping alone is judged at G1.

## Open Questions
- Which LeafSet cell layout gives the small-oval look; decided at build by the comparison sheet.
