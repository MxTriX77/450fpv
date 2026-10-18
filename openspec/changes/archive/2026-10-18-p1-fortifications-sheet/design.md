# Design: p1-fortifications-sheet

## Context
Four objects (asset-list §12) that the pilot named from today's villages (D-035). No footage or photo in `reference/` shows them. The sheet must still let an asset-artist build them as the real thing and the physics engineer judge their snags and gaps, so it rests on published sources, labelled as such.

## Decisions
1. **Sources are generic.** Allowed: published standards and specifications (for example ГОСТ block sizes), manufacturers' product pages and datasheets for razor wire and its tape profiles, public-domain field manuals that describe obstacle construction, encyclopedias. Each is cited by URL in the row it supports. Not allowed: news or social-media imagery tied to a named place, unit or date; anything that identifies a location. The sheet describes classes, never a site (SEC-01, SEC-02).
2. **Labels.** Every dimension has a range and a chosen default, labelled published (source named in the row), derived (derivation in the row) or assumed. "Published" binds only where the row cites its source (D-032 amended). No row is measured.
3. **F01's snag model is the sheet's core.** The sheet gives the tape section (width, thickness), the barb length, the barb pitch along the tape, the coil diameter, the turn pitch when the coil is stretched to its run, the clips between turns, the picket section and spacing, and how a coil sags and lies when torn (D). The physics role states every turn of the coil as an `edge` polyline at the tape's half-width with the barb pitch recorded, the coil as a wind-porous volume (porosity derived from the tape's area per coil volume), and no gap (a coil is never passable). Whether a barb can cut the fiber or only hold it is a physics question the sheet raises and the physics reading answers (task 1.3), not the analyst.
4. **Gaps against the drone.** For F02 the clear space between neighbouring teeth at the height bands the drone flies (ground, 0.5 m, the tooth's top); for F03 the openings between the arms of a standing hedgehog and between a pair; for F04 the barrier arm's clearance and the spaces between blocks. Each gap gives its polygon in words, the narrowest clear width, and whether the drone passes (0.574 m nose-first, 0.706 m diagonal).
5. **Materials.** `concrete` (F02, F04 blocks), `steel` (F03, pickets, the barrier), `fabric` (sandbags), and for the razor tape either `steel` or a proposed id (for example `razor_wire`) flagged for physics sign-off when its contact differs from plain steel. Visual sets are CC0 or CC-BY candidates with their source pages (D-003).
6. **States.** I and D per asset-list §12. D says what changes physically: torn and sagging coils, trampled flat runs, toppled or chipped teeth with exposed rebar if the published construction has it, bent or rusted hedgehogs, a broken barrier arm, burst sandbags.
7. **Placement.** From published descriptions only: how many rows of teeth and at what spacing, where hedgehogs stand (road approaches, checkpoints), where wire runs (in front of trenches, along fences and wall tops), what a checkpoint kit holds. These feed phase 3's layout with D-035's settlement mix and never describe a real site.
8. **Work split.** One analyst writes the whole sheet (four rows, one unit). The buildability review and the physics reading then run in parallel, each writing its own file in this folder. A rework task exists only if either sends something back. QA is last.

## Risks / Trade-offs
- [Published razor-wire figures vary by maker and profile] → the sheet gives the range across the cited profiles and picks one default profile, labelled; the generator takes the profile as a parameter.
- [Research-only objects drift from what the pilot sees] → the pilot's words in D-035 are the frame; the sheet's open points list what only a look at G2 can settle, asked at that gate, never before (D-024).

## Open Questions
- None that change the spec.
