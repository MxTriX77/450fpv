# Proposal: p1-l01-l03-pole-wires

**Owner:** asset-artist · **Reviewers:** tech-artist (its own task, the thin-wire rendering), physics-engineer (the `ports`, `wires` and gap metadata, by reading), world-builder (the `wires` record it will compile into the map, by reading), qa-engineer
**Requirements:** AST-02, AST-03, AST-05, AST-06, AST-07, AST-08, AST-12, REAL-01, REAL-03, REAL-05, SEC-04, PHY-22 (its inputs only) · **Decisions:** D-003, D-004, D-007, D-020, D-028 · **VISION:** §1 (a copy of the references), §2 (wires nearly invisible in the feed, the pole as a night landmark, strike by diameter)
**Source:** `docs/object-sheets/lines-roads-debris.md` §Shared (village lines, conductors, sag and tension, the shared pole table), §L01, §L03 (the sheet is the input), `openspec/changes/p1-object-sheets/review-buildability.md` (build order step 2: small generators that exercise every snag kind and the 1 km LOD, and give the tech-artist the wire to tune), PR-6.

## Why
Lane A's second step. The pole and its wires are small generators that touch every piece of the metadata contract the big assets need (capsules, boxes, cylinders, a gap polygon, `edge` and `hook` snags, a 1 km silhouette LOD) and they are the look-lock station where the pilot judges the one thing the feed barely shows: a wire that is nearly invisible, never shimmering, never a drawn line. The wire must be real geometry of its real diameter (REAL-05) and its points must already be what the map and PHY-22 will read.

## What Changes
- **CC0 sets pinned** (D-003): the log set the sheet names (Poly Haven `rough_wood` or `weathered_brown_planks`, chosen at the comparison), `rough_concrete` (the stub), `rusty_metal` (hooks, bands, steel wire), `metal_plate` (bare aluminium, tinted); insulators and SIP as plain tinted materials. `CREDITS.md` regenerated.
- **The `pole` generator** (the shared pole table of the sheet, `pole_kind` wood_log and wood_on_stub here; concrete_sv is L02's later task on the same generator) and params `L01_straight`, `L01_leaning` (9°, a single brace), `L01_stub`; LOD0 with hooks, insulators and bands, LOD1 the pole, stub, brace and crossarm, LOD2 a tapered prism kept to 1 km (the night landmark, L); the `damage` dial with scars, broken insulators and the snapped X state as parameters (G1 shows I).
- **The `wire_span` generator** and params `L03_thin`, `L03_medium`, `L03_thick`: a catenary span of its real diameter as a tube mesh between two attachment heights, with the sheet's conductor presets, arrangement, spacing, sag ratio and per-conductor damage; it writes each conductor's point list, diameter, mass per metre and sag into the metadata as a `wires` record, which the map compiler later copies into `objects.json` (D-007) and PHY-22 reads for tension and stiffness.
- **The pipeline contract grows (additive):** `ports` (named attachment points a pole writes for the wires) and `wires` (point lists with diameter, mass and material `cable`) in the metadata; the collision audit checks a wire's tube against its polyline (within 5 mm plus the radius) and does not expect primitives for it; the importer renders a wire's tube with the thin-wire material and creates no collider for it.
- **Thin-wire rendering** (tech-artist): a material for wire tubes that keeps coverage-weighted visibility when the tube is thinner than a pixel, so a 5 mm wire fades with distance as the sheet says and never shimmers, pops or shows a minimum width.
- **Physics metadata** (AST-05): the log as stacked capsules tapering with the real radius, the stub a box, the brace a capsule, each hook a capsule, each insulator a cylinder; `timber`, `concrete`, `steel`, `masonry` (porcelain, until a ceramic id exists) and `glass`; the brace triangle as a gap polygon with its narrowest width; a solid wind volume; `hook` snags on hooks, `edge` snags on insulator necks, crossarm ends, bands and the grounding wire; no landable surface. Wires: material `cable`, no gap, no wind volume, the whole wire a snag.
- **The catalog entries L01 and L03** and the side-by-side comparison against letter L (the leaning pole silhouette at night), J (a drop against the sky) and D (a slack cable against haze) in a local `reference/_compare/` sheet, never committed (REAL-03).

## Capabilities
### New Capabilities
- `assets-lines-roads-debris`: the asset requirements of the lines, roads and debris object sheet (L01–L05, RD01–RD03, K01–K10), one requirement per asset; this change adds L01 and L03.

### Modified Capabilities
- `asset-pipeline`: ADDED requirements for `ports` and `wires` in the metadata, the wire audit, and the importer's handling of wires.

## Non-goals
- No L02 concrete pole (its own change on the same generator), no L04 fallen wire or L05 hanging cables (they are states of this generator's conductors, built when their changes come), no D or X build for G1 (the parameters exist and are range-checked).
- No strike physics (PHY-22, phase 7), no wind sway of the wire, no map compiler (phase 3): the `wires` record is written now so nothing is re-derived later.
- No street placement rules; the gallery station is two poles and one span.

## Impact
`tools/assets/manifest.json`, `CREDITS.md`, `assets-src/materials/{pole_wood,pole_concrete,pole_steel,wire_aluminium,wire_steel,wire_sip,insulator_porcelain}.json`, `assets-src/lines/L01_*.json`, `assets-src/lines/L03_*.json`, `assets-src/catalog.json`, `tools/blender/generators/{pole,wire_span}.py`, `tools/blender/lib/{meta,audit,export}.py`, `unity/Assets/_Project/Editor/Assets/CatalogImporter.cs`, `unity/Assets/_Project/Shaders/ThinWire.shader` (tech-artist), `unity/Assets/_Project/Scripts/World/AssetPhysicsMeta.cs` (`ports`, `wires`), `unity/Assets/_Project/Art/{ThirdParty,Materials,Models/lines}`, `unity/Assets/_Project/Prefabs/lines/`. Branch: `unity/art/p1-l01-l03-pole-wires` off `unity/main` after `p1-s04-belt-straw` merges (lane A is sequential; it needs nothing from S04 but the pipeline it hardened).
