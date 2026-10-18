# Buildability review: p1-object-sheets (task 1.7)

**Reviewer:** asset-artist, as the role that writes the generators from these sheets. **Reviewed:** the six sheets at `2b06a65`, then fixed on this branch in six `docs(ref)` commits (below); U02b, added after it, was reviewed at `5fd5cd6` and fixed in a seventh. **Contract:** the spec's "Buildable" requirement, read against the pipeline as it exists: `tools/blender/lib/meta.py` (metadata schema 1), `assets-src/physics_materials.json` (the closed id list), `tools/blender/generators/debris_brick.py` and `openspec/specs/asset-pipeline/spec.md`. The footage was not re-read; `reference/` was not opened.

## How each row was judged

- **Parameters:** every generator parameter has a type, a range, a default and an effect, and the ranges fit the dimension table. Checked by reading, plus two throwaway scans (not committed): one lists Param-table rows with an empty type, range, default or effect; the other lists names used in the presets that no table defines.
- **Damage:** every state says what changes physically, as geometry a generator can make: cut, move, bend, remove or add parts. Prose on its own does not count.
- **Physics in schema 1:**
  - collision as box, sphere, capsule or cylinder, with a material id from the closed list (or a flagged proposal) and a `landable` bool
  - gaps as planar polygons with `narrowest_m`, the diameter of the largest inscribed circle
  - wind volumes as the same shapes, with a porosity in [0, 1]
  - snags as `edge` or `hook`, with at least 2 points and a radius
- **Editability (D-004):** no part needs hand-sculpting unless the sheet names a `.blend` source for it.
- **Materials:** CC0 or CC-BY candidates with source pages, and every derived texture named.

## Conventions this review fixes once for every generator

These are pipeline rules, so the sheets need not repeat them.

1. **LOD and cull distances** are derived, as `debris_brick` does today. A feature of size s drops under 2 render pixels at d = 935 · s / 2 (design decision 7). Trees add an impostor, and the poles keep a LOD to 1 km (L01). No sheet has to give LOD distances.
2. **`narrowest_m` is the inscribed circle** (the pipeline spec). For rectangles it equals min(width, height), so it matches the sheets' "clear width". For a trapezoid it does not: the trench cut is one case. There, the sheet's width-at-height table describes passability, and the gap probe (brief step 8) uses the polygon itself.
3. **Gaps through thick walls:** the polygon is the intersection of the clear outlines through the wall's depth, projected onto its mid-plane. A jag anywhere in the reveal narrows it, and the polygon stays planar.
4. **Snag records:** a member the fiber can bend over is an `edge` along its axis, with its own radius. A free-ended member (a twig, a splinter, a bar, a free cable end) is a `hook` from its base to its tip. "The whole crown", "the whole mesh" or "the whole pile" is not a record: it is a material behaviour (see the physics list).
5. **Wind:** the schema has only porous volumes. "Cavity", "funnel", "through-draught" and "wake" are what the wind solver works out from the solid and porous volumes, not tags. A porosity-1 volume adds nothing and is not written. That covers C01's funnel part and the "open leaves 1" lines.
6. **Proposed ids cannot be emitted.** `meta.validate` refuses any id that is not in `physics_materials.json`. A generator uses the fallback the sheet names, or waits for the id; the params file switches it later, with no code change.
7. **Map-side features are not asset metadata:**
   - wires as map objects (L03)
   - placed ground features: craters, potholes, ruts (E05, RD01, RD03, S13)
   - terrain holes with fillers: trenches, the pit, dugouts
   - micro-detail: stems, straw, crumbs

   The asset generator builds the visual geometry and writes the hand-off data. The world-builder's compiler and `MicroDetailNear` own the physics of these.

## Per sheet

### vegetation.md

**Buildable as written:**
- one tree generator: a Leonardo's-rule skeleton, segments of 10 mm and up as capsules, twigs and leaves as cards over a compliant crown shell
- species presets V01–V12
- the shrub and forb generator V13–V18
- the dead-wood generator V07–V09 and V19–V21

The physics plan maps cleanly onto schema 1: capsules, convex crown shells with a compliant material, one porosity per crown piece, landable flags on forks and logs. Materials are Poly Haven bark sets and ambientCG LeafSet atlases, with a wound mask and tints derived.

**Fixed (`158e724`):**
- Defined 19 parameter rows that the presets used but no table held:
  - tree: `dead_top_frac`, `twig_cards`, `spines`, `fruit_share`, `water_sprouts`, `suckers`, `epicormic_tufts`
  - shrub: `fork_angle_deg`, `bare_base_m`, `fruit_tufts`, `basal_leaf_m`, `char_height_m`, `species_mix`
  - dead wood: `length_m`, `butt_d_m`/`tip_d_m`, `forks`, `twig_share`, `stubs`, `char_share`
- Rewrote the presets to use those names. Preset counts that the sheet never gave are labelled assumed.
- Gave V20 a real parameter table.
- Wrote the rule for the gap polygon between two members, which the sheet asked for but never defined, and the snag-record mapping.
- Noted that schema 1's material table has no field for a break moment, a buckling load or a shell's hook rate.

**Open:**
- **Physics engineer:**
  - the ids `bark`, `twig`, `deadwood` and `forb_stem`, and the fields above
  - whether hollow elder stems break
  - the capsule budget per tree (an M tree has about 150–300 capsules)
- **World-builder:**
  - V17 and V18 stalks are micro-detail cantilevers near the drone, not asset collision
  - belt-level porosity classes come from the layout
- **Asset-artist (at build):** choose the LeafSet ids, measure the photo albedos on the fetched sets, check the Lombardy crown width.

**Not blocking:** V01 and V13 build now with the fallbacks the sheet names (`timber` for bark, `foliage` for the twig shell).

### houses-and-yard.md

**Buildable as written:**
- the shared house generator with per-row deltas (H01–H06), with a detailed failure order and a physical D and X per row
- the outbuildings, the cellars and the yard elements
- every gap worked against the drone, with materials and derived facade maps and damage masks named

Every id H01 needs (`masonry`, `timber`, `glass`, `cable`) already exists.

**Fixed (`9818e85`):**
- **"From `damage`"** was the default of 26 parameter rows but defined nowhere. It now means linear interpolation between the row's I, D and X values at `damage` 0, 0.55 and 1.0, with ranges drawn by `seed`.
- **`window_state` added.** The gap widths depend on frame, sash and shards, but no parameter carried them.
- **`burnt` added** (fire step 7 had no parameter).
- **`rubble_volume`:**
  - The range was 0–40 m³, too small for H06's lost upper floor. It is now 0–150 m³.
  - The default is now derived: the removed solid volume × 1.86, U02's bulking.
  - The heap is tied to the debris kit's mixture and its exp(−d/λ) spread.
- **Inheritance:** H04, H06 and O03 now inherit H03's brick additions (`brick_step_depth`, `rebar_out`). H06 gets `wall_thickness_upper` (0.51/0.38 was a pair in a scalar).
- **Arithmetic corrected:**
  - H03 door "frame on 0.66 → ±18°" treated the 0.8 m leaf like a window opening. Now about 0.77 m between the stops, any yaw, as H01.
  - H06 stairwell: 1.0 m passes at any yaw, not "nose-first".
  - Y05 wicket: "0.19 m spare" was the total; it is now 97 mm a side.
  - H02 `fragment_scatter` tied to the slate λ.

**Open:**
- **Physics engineer:**
  - the ids `asbestos_cement`, `soil`, `straw`, `fabric`, `plastic_film` and `mesh` (Y03, caught and wrapped)
  - tile and slate breakability
- **World-builder, with the asset-artist (pipeline):** below-grade parts need terrain holes, and schema 1 has no field to hand their footprint to the map. That covers C01's stair and chamber (2.2 m down), C02, the O05 pit, the Y07 shaft (5–15 m) and the U03 pits. I propose a `ground_holes` list of polygons with depths, in asset space, in the metadata or the catalog entry. That is a pipeline change (mine), agreed with the world-builder before C01 is built.
- **Tech-artist:** the URP side of the facade map and the RGBA damage mask (R render depth, G soot, B dirt, A base band); interior blue whitewash.
- **Settled at the side-by-side, not by the pilot:** the H01 roof end, the tile profile, the J window size.

### earthworks-and-surfaces.md

**Buildable as written:**
- S01–S13 as presets of one surface-material generator (CC0 sets, albedo targets, macro variation, feathered edges)
- the trench-family parameters (E01–E04), with the drone-width table derived correctly
- the E05 crater profile in integer powers, the E06 pit and the E07 bank

**Fixed (`ff1d2e4`):**
- S11 cited the road kit as "R01", which is the sedan. It now reads RD01; the summary's "R01–R03" now reads RD01–RD03.
- S11–S13 had their own road numbers that disagreed with the kits:
  - carriageway 5 m against 6.0/4.5 m
  - asphalt layer 4 cm against 50 mm
  - potholes 25–100 mm against 40–150 mm
  - plate joints 0.02–0.15 against 0.02–0.30 m
  - tilt 0–3° against 0–4°
  - plate mass 4.2 against 4.1 t
  - rut depth 0.08 against 0.12 m
  - median 0.9 against 0.8 m

  They now cite RD01–RD03 as the geometry's owner. The duplicate rut table moved to RD03.
- The proposed `asphalt` row no longer counts potholes twice (hashed pitfalls in the row plus RD01's placed ones).
- E01's top-slot gap is written per filler segment, so each polygon stays planar within 10 mm on rolling ground. A note says why `narrowest_m` differs from the width table.
- S04 gets a parameter table for its straw instance kit (lengths, diameters, bend, seed heads, variants, segments), which its 1.5 m look depends on.

**Open:**
- **E01 has no material it can validate with, and this blocks the look-lock.** Its fillers carry `surface:belt_bare`, which `meta.validate` refuses. One of two is needed:
  - the physics engineer adds `soil` (and `straw`)
  - the pipeline accepts `surface:<id>`, validated against the map's surface table (my change, after physics agrees)

  U02's top faces raise the same question.
- **E01 is a map-scale feature:** a hole band, parapet deltas in the heightfield, and fillers along a polyline. Proposed split: the generator takes a polyline and writes the visual walls, floor, berm and parapets, the filler primitives and the gap polygons; the world-builder's compiler writes `holes.png` and the parapet deltas. For G1 the gallery's E01 station is a fixed 20–30 m curving run on a gallery terrain tile, which the world-builder must be able to hole. Orchestrator and world-builder.
- **S04 at 1.5 m depends on the micro-detail generator,** which the phase-1 brief schedules after G1 (step 7 against step 5). Two options:
  - (a) for G1, a seeded, visual-only scatter of the instance kit at S04's densities, gallery only, replaced at step 7
  - (b) bring the straw part of step 7 forward

  Orchestrator's call; (a) is cheaper.
- **Physics engineer:**
  - the surface rows `asphalt`, `concrete_slab`, `dirt_track` and `stubble`
  - the `adhesion` field and the `wetness` factors
  - the cavity model
  - X-1 (clods) and X-8 (breakable lip)
- **World-builder:**
  - the X-9 placed features (craters 0.3–4 m, potholes, ruts)
  - the hole bands and their berm
  - the feathered edges
- **Tech-artist:**
  - the wet look
  - the trench reading as a dark line between pale parapets at 50–200 m
  - the night track at about 2.5× the field
- **Asset-artist:** lift `meadow_sod`'s carried albedo (0.14) on the fetched sets; check TextureCan's CC0 statement when pinning Ground 0018.

### lines-roads-debris.md

**Buildable as written:**
- the pole generator: its table is complete, with LOD reasoning, capsule and box collision, and snag records with radii
- the L03 conductor classes, with the derived PHY-22 inputs; L04 and L05
- the road kits RD01–RD03, which split ground from objects cleanly
- the debris kit, which extends `debris_brick` with `K01_whole` unchanged

**Fixed (`342b8a1`):**
- `crossarm_state` had no effect.
- `sag_ratio`, `crossfall` and `potholes_per_100m2` had no type.
- Pothole size and depth, and three seeds, had no effect.
- K10 `pattern`/`colour` had no type.
- K06's lintel piece was 0.22 m deep while citing the houses sheet's 0.25 m; it is now 0.25 m.
- K06's bars were 6–14 mm against U01's 5–14 mm at broken slab ends; they are now 5–14 mm.
- L05's diagonal stopped at 4 m. U01's cables run from a slab edge to the floor below across a 5.6 m bay, so it now goes to 6.5 m.
- RD03 takes `median_h_m` and `wander`, as the single owner of the rut feature.
- The K-mound mixture now points to U02's `piece_mix`. Two different reads of the same mound had stood in two sheets.

**Open:**
- **Physics engineer:**
  - the ids `rubber`, `plastic`, `fabric`, `asbestos_cement` and a ceramic id
  - `asphalt` is proposed both as an object id (RD01's lifted slabs) and as a surface row (S11). That is one name in two tables; choose one meaning, or rename the object id.
  - runtime objects that rock, slide and roll (planks, sheets, tyres, barrels)
  - L04's support anchors
  - wire sway
- **World-builder:**
  - L03 wires as `objects.json` points, from the same source as the tube mesh; agree the hand-off file
  - potholes, ruts and road edges in the map format
  - the λ placement of debris
  - whether a cable across an opening narrows its gap polygon (L05)
- **Tech-artist:** sub-pixel wire rendering (coverage-weighted, never thickened). G1's L03 station is judged on it.

### vehicles.md

**Buildable as written:**
- one `vehicle` generator: a clean base plus physical A, B and F kits, with debris fields and soot patches
- a collision plan of 20–35 primitives
- an honest gap table: no car gaps; R09's bare-bow cage has real bays
- materials and derived masks (the vehicle damage mask, the glass crack mask)

**Fixed (`ced52e9`):**
- The base preset said it loads "the side and plan profiles, the greenhouse", but no parameter or number defined them. Without them the clean base would be guessed or hand-sculpted. Added, with types and ranges: `overhang_front`/`overhang_rear`, `side_profile` (8–14 stations), `plan_profile`, `belt_line`, `sill_height`, `tumblehome` and `arch_clearance`. An open point records where the stations come from.
- The R06 size and the R09 gap notes to the orchestrator are now resolved in the asset list, and are marked so.

**Open:**
- **Asset-artist, as the first step of the R01 change:** derive R01's profile stations from public dimensioned views, labelled, and check them against M. This is the largest single unknown in the look-lock set.
- **Physics engineer:**
  - `rubber`: tyres. R01 A needs it before its metadata can validate.
  - `fabric`: seats and the R09 tilt. R01 A needs it too.
  - `plastic`: trim and later bumpers.
  - `glass_crumbs`: probably a surface cover rather than an object id.
  - sign-off for fragment holes of 0.25 m or less staying closed in collision, a stated deviation
  - the 12° landable bonnet limit
- **World-builder:** K07's crumb layer from the micro-detail generator plus its decal.
- **Tech-artist:** the glass shader with the crack mask; paint fade and soot.
- **R07's open question:** keep Golf II as a `body` option of R07. It shares every rule, and a separate preset costs nothing later.

### urban-edge.md

**Buildable as written:**
- U01 as a section-based generator: its table is complete, with every damage kit item in physical terms and the rebar sizes and density given
- the U02 mound, from a published rubble method with its volume derived
- U03 and P01–P06
- U02b (added later, task 1.8) as the thin `roof_pile` generator it names: the house generator's roof and wall builders at the source preset, a seeded collapse pose, U02's core builder and the K kit. It follows H01's house generator and U02, and needs nothing new but the pose. All 28 parameter rows have a range or a discrete choice and an effect. The members are K03's sections, the scatter is the kit's λ per class, the core is bulked by U02's 1.86, and the timber and core volumes recompute from H03's defaults. The collision plan is primitives (4–8 core boxes with the voids open, 60–150 member capsules, 20–60 thin boxes, inside a tree's budget), with snag records by convention 4

**Fixed (`8bb1971`):**
- The stale notes are updated: "lines sheet not on this branch", and the U01 eaves item, which is now in the asset list.
- U01's lintel bars read 10–12 mm while citing the houses sheet's 8–12 mm; they now read 8–12 mm.
- P05 put porcelain insulators on `glass`, while L01 and L02 use `masonry`. It now matches the pole rows.
- P05 wrote a gap between phase conductors, which breaks L01's rule that wire contacts bound that space. It no longer does.
- The K06, K10 and L05 cross-check is recorded.

**Fixed for U02b (`6c6b19e`):**
- The default source is H03 with `wall_material` red_brick. H03 is silicate, while I's wall end and core are red brick and the colour rows author `red_brick`.
- The pitch is H03's 25°, not 27°. The rafter is 3.86 m to the eaves line, which the timber count's 4.3 m rafters already assumed, and the high-end flank recomputes to 35°.
- The masonry core counted the ceiling's joists and boards (4.1 m³) that the timber row counts too. They are members in the collision plan, so the core is 50 m³, 0.6 m mean.
- `rafter_spread` set the rise even where a support holds the ridge, which put I's high-end rafter feet 1.1–1.5 m above the core. Spread now sets the ridge only where nothing holds it; a held ridge lets the feet hang to the core.
- Two gap verdicts now follow the formula. Battened slate bays of 0.60–0.70 m pass nose-first within ±5° to ±35° (U01's rule), not "nose-first only". Crossed-member openings of 0.574–0.6 m pass nose-first within ±5°, and those of 0.5 m and over are written as polygons (convention 3, the vegetation threshold).

**Open:**
- **U02b, no longer sent back:** the reference-analyst added its section (task 1.8, from I), so the coverage check passes, and it is judged above: buildable, with the fixes in `6c6b19e`. The physics sign-off on `p1-pipeline-hardening` covers every id it uses. `asbestos_cement`, `fabric` and `plastic` are added. `soil` maps to `surface:<id>`, so the ceiling fill stays `masonry` inside the core boxes, and `surface:rubble` on the core's top faces is accepted, as for U02. Still for the physics engineer: capsule rafters, which a leg slides off where a sawn face would hold, inside AST-05's ±5 cm.
- **Physics engineer:**
  - the ids `fabric`, `plastic`, `asbestos_cement`, EPS `foam` and ceramic
  - `surface:rubble` on U02's top faces
  - the ceiling effect in the 2.72 m rooms
- **Licence, for the orchestrator:** P01's sign and P06's plates and stele need lettering in a font under CC0 or CC-BY. D-003 allows nothing else, and SIL OFL is neither. Find one before P01 and P06 (both P2), or record a decision.
- **Asset-artist:** P04 variant 2's "sculpted form" is built as a parametric curved shell, so no `.blend` source is needed.

## Cross-sheet consistency

| Check | Finding | Action |
|---|---|---|
| K06 bars against U01 | 6–14 against 5–14 mm; the lintel piece 0.22 against 0.25 m; U01's lintel bars 10–12 against the houses sheet's 8–12 mm | fixed in both sheets |
| K10 and L05 cables against U01 | L05's 3–15 mm covers U01's 5–10 mm; diagonals were too short for storey spans | L05 extended to 6.5 m |
| Debris spread against houses' rubble | the λ table is anchored on H01's tile heap (0.2–0.4 m deep, 1–2 m wide), which agrees; the houses sheet never said how its rubble was laid; two mixtures stood for the K mound | houses now use the λ spread and U02's bulking; one K mixture (U02) |
| Vehicle debris (1/r^n from a point) against buildings (exp(−d/λ) from a wall) | different source geometry, both stated | none |
| Drone against openings | every worked value re-checked against w(θ) = 0.254 + 0.32·(\|cos θ\| + \|sin θ\|), among them H01 113/47 mm, H05 ±35°, C01 63 mm and ±35°, E01's three cuts, E02 83/17 mm, L01's brace triangle (r 0.89 m; any yaw below 3.0 m), U01's 7 mm interior door and R09's 62 mm bays; the 0.24 m leg square is used the same way in every sheet | 4 fixes: H03 door, H06 stairwell, Y05 wicket, P05 conductors |
| Road numbers, S11–S13 against RD01–RD03 | ten disagreements and a wrong id (R01) | fixed; the kits own the geometry |
| Porcelain | `masonry` in L01/L02, `glass` in P05 | P05 aligned |
| `soil` and `straw` | proposed by both houses and earthworks, with compatible intent | one id each; the earthworks values are the more derived |
| `asphalt` | an object id (lines) and a surface row (earthworks) | flagged for the physics engineer |

## Proposed physics ids and fields, collected once (for the physics engineer)

| Id | Proposed by | For | Suggested values (from the sheets) | Waiting on it |
|---|---|---|---|---|
| `bark` | vegetation | live trunk and limb capsules | μs 0.55, μk 0.45, rigid, edge = the capsule radius | nothing (fallback `timber`) |
| `twig` | vegetation | crown and clump contact shells | k 10–50 N/m, ζ 0.2, μs 0.5, plus a hook rate | nothing (fallback `foliage`) |
| `deadwood` | vegetation (earthworks for wattle) | dead trees, branches, brush, logs, wattle rods | μs 0.45, μk 0.35, rigid; break moment σ_b·πd³/32, σ_b 40 MPa (elder 20) | V07–V09, V19–V21 (P1), E02 |
| `forb_stem` | vegetation | weed stalks near the drone | k 1–3 N/m per stalk, buckles at 1 N | the micro-detail physics |
| `asbestos_cement` | houses (lines, urban) | slate sheets and fragments, the cowshed roof | friction near `masonry`, edge 3 mm, brittle | H02, H03 (P1); K02 slate |
| `soil` | houses, earthworks | cellar mounds, the clay bench; trench fillers unless `surface:<id>` | μs 0.5, μk 0.4, 2.5e3 N/m, ζ 0.5, edge 0.02 m | **E01 (look-lock)**, C01, C02 |
| `straw` | houses, earthworks | thatch, hay, the stack, straw layers on meshes | μs 0.45, μk 0.35, 490 N/m, ζ 0.3; hooks legs on liftoff | H05, O04, Y09 |
| `fabric` | houses, lines, vehicles, urban | tarps, curtains, mattresses, sofas, seats, the canvas tilt | soft, wraps, no bounce | **R01 A (look-lock)**, C01, K05, K08, K10, R09 |
| `rubber` | lines, vehicles | tyres | μs about 0.8–1.0, compliant | **R01 A (look-lock)**, every vehicle, K09 |
| `plastic` | lines, vehicles, urban | PVC frames, crates, housings, trim, bumper covers | μs 0.3–0.4, rigid thin shells, blunt edge 1–2 mm | R02–R07, K08, K09, U01 |
| `plastic_film` | houses | greenhouse film | soft, wraps the props | Y10 (P2) |
| `mesh` | houses | chain-link | catches and wraps, no bounce | Y03 (P1) |
| `glass_crumbs` | vehicles | the crumb layer | μ 0.30/0.20; perhaps a surface cover, not an object id | K07 |
| ceramic | lines, urban | porcelain insulators | hard, glazed, low friction, chips | nothing (`masonry` stand-in) |
| `foam` | urban | EPS remnants | very light, crushable | nothing (`plastic` stand-in) |
| `asphalt` (object) | lines | lifted asphalt slabs | near `concrete`, edge 3–5 mm | RD01 X; name clash with the surface row |

**Also for the physics engineer:**
- **A convention:** `surface:<id>` as the material of filler shapes (earthworks, preferred; U02's top faces).
- **New fields** the closed table cannot express today:
  - a break strength or moment (`deadwood`, elder)
  - a buckling load (`forb_stem`)
  - a shell's hook rate (`twig`)
  - `adhesion` for wet soils
  - breakability for tile, slate, glass and asbestos-cement (the sheets call this a later property)
- **Surface rows** (the map's surface table, not `physics_materials.json`): `asphalt`, `concrete_slab`, `dirt_track`, the `stubble` variant of `tilled`, the D-021 wet rules, and `wetness` 0.6 / 1.0.
- **Deviation sign-off:** vehicle fragment holes of 0.25 m or less stay closed in collision.

## The look-lock set: can it be built first?

| Asset | Buildable now? | Needed before its build | Needed before G1 |
|---|---|---|---|
| S04 belt_straw | **yes**: material generator, CC0 sets, carried surface row, instance kit now defined | — | a straw scatter for the 1.5 m view (interim visual scatter, or step 7 brought forward) |
| V13 + V01 | **yes**, with the fallback ids | LeafSet choice | `bark` and `twig` if the look should carry the final contact feel (not required for a look) |
| H01 in I/D/X | **yes**: every id exists | K02 tile shards and K03 battens and planks for its rubble, built in or just before the H01 change | the tech-artist's facade-map and damage-mask material |
| L01 + L03 | **yes** | — | sub-pixel wire rendering (tech-artist); L03's map record can follow |
| R01 in A/F | **no** | profile stations (asset-artist, first step); `rubber` and `fabric` for A. F needs no new id: tyres gone, steel seat frames, glass gone | — |
| E01 | **no** | `soil` or `surface:<id>` (physics plus pipeline); the trench split and a gallery tile that can be holed (world-builder) | — |

## Recommended build order for the look-lock assets

1. **S04 belt_straw.** It is the cheapest, it is the ground under every other station, and it proves the CC0 fetch, the tinting and "no visible tiling at 10 m" first.
2. **L01 pole, then L03 wires.** Small generators. They exercise every snag kind and the 1 km LOD, and give the tech-artist the wire to tune.
3. **V13 shrub, then V01 locust.** The shrub is the tree generator's skeleton and cards at a small scale, so the shared code is written once; V01 adds the impostor.
4. **H01 in I/D/X.** The largest look risk (facade maps, upright continuous UVs, damage masks, jagged openings) and the first-attempt lesson. Build it with K02 and K03.
5. **R01 F, then R01 A.** F needs only existing ids, so it can be built once the profile stations exist; A follows when `rubber` and `fabric` land.
6. **E01** follows the material decision and the trench split.

**Two lanes for the two-agent limit:** lane A S04 → L01/L03 → E01; lane B V13 → V01 → H01 → R01. The three decisions that gate R01 and E01 should start now, so that steps 5 and 6 do not wait:
- the physics ids `rubber`, `fabric`, and `soil` or `surface:<id>`
- the trench and hole hand-off with the world-builder
- the micro-detail timing for S04 (orchestrator)

## Sent back against fixed, in one place

**Fixed on this branch:**

| Commit | Sheet | What |
|---|---|---|
| `158e724` | vegetation | 19 undefined parameters, the V20 table, the gap rule, the snag mapping |
| `9818e85` | houses-and-yard | the damage dial, `window_state`, `burnt`, the rubble rule and range, inheritance, 4 arithmetic fixes |
| `ff1d2e4` | earthworks-and-surfaces | the RD ids, 10 road numbers, double-counted potholes, E01 gap planarity, the S04 instance kit |
| `342b8a1` | lines-roads-debris | 9 missing types and effects, K06 bar and lintel sizes, L05 length, RD03 rut parameters, the K mixture |
| `ced52e9` | vehicles | the body profile parameters, the resolved notes |
| `8bb1971` | urban-edge | stale notes, lintel bars, P05 insulators and conductor gaps |
| `6c6b19e` | urban-edge (U02b) | source brick, pitch, core volume, `rafter_spread`, two gap verdicts |

**Sent back, by owner:**

| Owner | Item |
|---|---|
| Physics engineer | the ids and fields above |
| Physics engineer, with the pipeline | `surface:<id>` against `soil` |
| World-builder, with the asset-artist (pipeline) | `ground_holes` hand-off |
| World-builder | the trench split and a gallery hole tile; X-9 placed features; L03 `objects.json` hand-off; micro-detail (S04, V17, K07) |
| Tech-artist | facade and damage-mask material; sub-pixel wires; wet look; glass crack shader |
| Orchestrator | micro-detail timing for G1; a CC0 or CC-BY font for P01 and P06 |
| Asset-artist, at build | R01 profile stations; LeafSet ids; albedo re-measures; the TextureCan licence check |

Nothing here is a pilot question.
