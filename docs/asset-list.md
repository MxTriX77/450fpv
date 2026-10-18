# Asset list: what the gallery holds

The pilot asked for "not too many versions of the same objects, but not very few either", enough to fill **three adjacent Ukrainian villages** and their land richly and ULTRA realistically. This list is the phase-1 work order and the gallery's station list.

## How to read it

- **ID** is stable. Generators, params files, catalog entries and gallery stations all use it.
- **Variants:** different params files from one generator. **States:** damage levels, applied by parameters (AST-06).
  - Buildings: **I** intact-weathered (abandoned, worn), **D** damaged (roof stripped, windows blown, holes), **X** destroyed (collapsed walls, rubble).
  - Vehicles: **A** abandoned (fragment holes, flat tyres, crazed glass, dust and rust), **B** blast-wrecked, **F** burnt-out.
- **Ref:** footage and photo letters ([knowledge.md](knowledge.md) §9.1), or **R** for research only (not in the footage; the pilot confirms at G2).
- **Pri:** **P1** is required for the gallery release (G2). **P2** comes before the world (G3).
- **Physics:** the metadata it needs beyond collision and material (AST-05).
  - `gap` means it has fly-through openings.
  - `snag` means it has snag hazards.
  - `wind` means a non-trivial wind volume (porosity per part).
  - `land` means it has landable surfaces.
- Every row also gets an object sheet (AST-02) and a review sheet (AST-12).

## 1. Ground surfaces (13)

Each surface is a visual material plus a physical row in the surface table ([knowledge.md](knowledge.md) §7.2). Starting values for the first ten come from the first attempt.

| ID | Surface | Ref | Look | Pri |
|---|---|---|---|---|
| S01 | meadow_sod | A, L | mottled olive-khaki sod, green and straw-dry mixed; pale matted patches (PR-8); small dark scorch spots | P1 |
| S02 | dry_crust | B | sun-baked loam crust, dust, dead tufts | P1 |
| S03 | crater_spoil | B, F | loose fresh spoil, darker or lighter than its surroundings | P1 |
| S04 | belt_straw | H, G | lodged straw mat 5–20 cm over dark chernozem; standing stems with seed heads | P1 |
| S05 | belt_bare | F | fine dusty loam beaten bare, fallen twigs | P1 |
| S06 | tilled | G | dark loose furrows 0.3–0.5 m apart, straw streaks in rows; ridge direction is a parameter | P1 |
| S07 | burnt_field | pilot, A | black char, grey-white ash, char stubble 5–15 cm, wavy fire-front edges, unburnt islands | P1 |
| S08 | weeds | D | ground under tall forbs 0.5–1.5 m | P1 |
| S09 | yard_litter | C, E | compacted soil, leaf litter, brick and tile bits | P1 |
| S10 | rubble | E, I–K, N | brick, tile, plaster and timber debris ground | P1 |
| S11 | asphalt | M, O | worn, dusty, cracked village asphalt; potholes | P1 |
| S12 | concrete_slab | PR-8 | road plates (about 6 × 2 m) laid as field tracks, with gaps and weeds | P1 |
| S13 | dirt_track | L, R | rutted earth track, dry, with a grass median | P1 |

## 2. Earthworks and terrain features

| ID | Asset | Variants | Ref | Key size | Physics | Pri |
|---|---|---|---|---|---|---|
| E01 | Trench, open zigzag with spoil parapets | 3 shapes (straight, zigzag, curving) | G, B | 0.6–1.0 m wide, 1.2–1.8 m deep, parapets 0.3–0.5 m | gap (corridor), wind (shelter), land (no) | P1 |
| E02 | Trench, revetted (boards, wattle, posts) | 2 | R | as E01 | snag | P1 |
| E03 | Trench, collapsed and overgrown | 1 | R | as E01 | — | P1 |
| E04 | Dugout entrance (log-roofed, steps down) | 1 | R | opening about 0.8 × 1.6 m | gap, holes | P2 |
| E05 | Craters (generator) | small 0.3–1 m scorch disc; medium 1–2 m; large 3–4 m with a loose rim | A, B | depth 0.3–1 m, rim 0.1–0.3 m | pitfall, surface | P1 |
| E06 | Round pit with branches over it | 1 | F | 2–3 m across, 1–1.5 m deep | snag | P1 |
| E07 | Spoil bank / earth bank | 2 lengths | B | about 1 m high | — | P1 |

## 3. Vegetation

The phase-1 research confirms the species against the footage and typical Ukrainian shelterbelt planting.

| ID | Asset | Variants | Ref | Key size | Physics | Pri |
|---|---|---|---|---|---|---|
| V01 | Belt tree: black locust (робінія / "акація") | 3 sizes | F, H, G | crown 6–15 m, trunk 5–30 cm | wind (crown porosity), snag | P1 |
| V02 | Belt tree: ash | 3 sizes | F, H | as V01 | wind, snag | P1 |
| V03 | Belt tree: maple (incl. box elder) | 3 sizes | F, H | as V01 | wind, snag | P1 |
| V04 | Belt tree: elm | 2 sizes | R | as V01 | wind, snag | P1 |
| V05 | Poplar (roadside, tall) | 2 sizes | R | 15–25 m | wind, snag | P2 |
| V06 | Oak | 2 sizes | R | 10–18 m | wind, snag | P2 |
| V07 | Damaged tree, broken top | 2 | B | 4–10 m | snag | P1 |
| V08 | Dead tree, bare and leaning | 2 | D | 4–10 m, bare fans 3–6 m | snag | P1 |
| V09 | Shell-split trunk / charred stump | 2 | R | — | — | P2 |
| V10 | Yard fruit tree: apple | 2 | C, J | 4–7 m, low spreading crown from 1.5 m | snag, wind | P1 |
| V11 | Yard fruit tree: cherry / apricot | 2 | C | 3–6 m | snag, wind | P1 |
| V12 | Walnut (large yard tree) | 1 | R | 10–15 m | snag, wind | P2 |
| V13 | Belt understory shrub | 3 | F, H, PR-10 | 1–3 m | snag, wind | P1 |
| V14 | Elder / lilac clump | 2 | R | 2–4 m | snag, wind | P1 |
| V15 | Blackthorn / rose-hip thicket | 2 | R | 1–2.5 m | snag | P2 |
| V16 | Self-seeded saplings | 2 | D | 1–3 m | snag | P1 |
| V17 | Tall weeds: mixed forbs | 3 | D, E, C | 0.5–1.5 m, stalks 5–15 mm | snag (fiber drag), drag | P1 |
| V18 | Burdock, nettle, thistle with seed heads | 2 | R | 0.5–1.5 m | snag | P2 |
| V19 | Fallen branch set | 4 pieces | F, H | 1–4 m, 2–8 cm | snag, uneven support | P1 |
| V20 | Brush pile | 2 | J | 1–2 m high, 2–4 m across | snag | P1 |
| V21 | Fallen log | 1 | R | 4–8 m, 20–40 cm | land | P2 |
| — | Far impostors | one per tree variant | — | at 200 m+ a belt reads as a dark band | — | P1 |
| — | Micro-detail (grass, straw, twigs, litter) | generator | A, H, C | K§7, K§10.4 | physics parity | P1 |

## 4. Village houses (6 types × 3 states)

Plaster recipe from research: weathered lime whitewash #c4bfb1 over clay render #97754f over adobe #7d6146, with a red-clay base band 0.42 m high. Roughly 30 % of the whitewash is gone, around openings, corners, the foot and the wall head.

| ID | House | Ref | Key size | States | Physics | Pri |
|---|---|---|---|---|---|---|
| H01 | Adobe (саман) house, lime-whitewashed, gable roof, clay tiles | E | 8–12 × 5–7 m, walls 2.3–2.6 m, 0.4–0.5 m thick, ridge about 5 m, door 0.8 × 1.8, windows 0.8 × 1.0 | I / D / X | gap (door, windows, roof bay, gable breach), snag (battens, rafters, hanging cable), wind (solid walls, open roof), land (none) | P1 |
| H02 | Adobe house, hip roof, asbestos-cement slate (шифер) | J, R | as H01 | I / D / X | gap, snag, wind | P1 |
| H03 | Silicate-brick house, slate roof, painted window frames, satellite dish | J | 8–10 × 6–8 m, ridge 5–6 m | I / D / X | gap, snag, wind | P1 |
| H04 | Red-brick 1980s house, larger, with veranda, metal or slate roof | R | 10–12 × 8–10 m | I / D / X | gap, snag, wind | P1 |
| H05 | Small old clay hut (мазанка), whitewashed, low roof | R | 6–8 × 4–5 m | I / D / X | gap, snag | P2 |
| H06 | Two-storey brick house | I | 9–11 × 8–10 m, 2 storeys | I / D / X (roof and upper floor gone) | gap, snag, wind, land | P1 |

**Damage in D and X is physical (PR-5):** jagged brick around openings, missing courses, broken and irregular battens, a shelled tile or slate field, soot above burnt windows, and rubble coloured from the house's own materials.

## 5. Outbuildings, cellars and yard elements

| ID | Asset | Variants or states | Ref | Key size | Physics | Pri |
|---|---|---|---|---|---|---|
| O01 | Plank shed | I / D | E, J | 3–6 × 2–4 × 2–2.5 m | gap (door), snag | P1 |
| O02 | Brick or block shed with a small window | I / D | C | as O01 | gap, snag | P1 |
| O03 | Summer kitchen (літня кухня), small brick | I / D / X | R | 4–5 × 3–4 m | gap | P2 |
| O04 | Barn or hayloft, timber | I / D | R | 6–10 × 4–6 m | gap, snag, wind | P2 |
| O05 | Outdoor toilet, wooden | I / D | R | 1 × 1.2 × 2.2 m | — | P1 |
| O06 | Garage, brick or sheet metal | I / D | R | 3.5 × 6 m | gap (door) | P2 |
| C01 | Cellar entrance, brick head with plank door, steps down (hole) | 1 | C | head 1.2–1.5 × 1.8–2.0 × 2–3 m, **door 0.70 × 1.6 m** | gap (narrowest fly-in), holes, wind (funnel), snag (tarp) | P1 |
| C02 | Earth-mound cellar with a concrete or brick portal | 1 | R | door about 0.75 × 1.7 m | gap, holes | P2 |
| Y01 | Plank fence (grey weathered) | 2 heights, gaps | E | 1–2 m, boards 0.10–0.15 m | snag, wind (porous) | P1 |
| Y02 | Picket fence, painted | 1 | R | 1.2 m | snag, wind | P1 |
| Y03 | Chain-link mesh on metal posts | 1 | R | 1.5 m | snag (fiber), wind (very porous) | P1 |
| Y04 | Brick or concrete-panel fence | 1 | I | 1.5–2 m | — | P2 |
| Y05 | Sheet-metal gate with wicket | I / D | R | 3 m | snag (torn edges) | P1 |
| Y06 | Plank gate | I / D | E | 2.5–3 m | gap | P1 |
| Y07 | Well (криниця) with a small roof and crank | 1 | R | 1 × 1 × 2 m | — | P2 |
| Y08 | Woodpile | 2 | R | 2–4 m long | land (uneven) | P1 |
| Y09 | Straw or hay stack | 1 | R | 3–5 m | — | P2 |
| Y10 | Greenhouse frame (film torn) | 1 | R | 3 × 6 m | snag (film), gap | P2 |
| Y11 | Yard props: bench, laundry line, barrel, bucket, washbasin | set | R | — | snag (line) | P2 |

## 6. Village public buildings and infrastructure

| ID | Asset | States | Ref | Physics | Pri |
|---|---|---|---|---|---|
| P01 | Village shop (small brick, **invented** sign) | I / D / X | R | gap | P2 |
| P02 | Farm complex: long brick cowshed with asbestos roof, partly collapsed | D / X | R | gap (large openings), wind, snag | P2 |
| P03 | Water tower (Rozhnovsky type, steel tank on a column) | I / D | R | snag, landmark at night | P2 |
| P04 | Bus stop (Soviet concrete shelter) | I / D | R | land | P2 |
| P05 | Transformer kiosk / pole-mounted transformer | I / D | R | snag | P2 |
| P06 | Village name sign (**invented name**), road signs | 2 | R | — | P2 |

## 7. Urban edge

| ID | Asset | Ref | Key size | Physics | Pri |
|---|---|---|---|---|---|
| U01 | **Stalin-era five-storey block, destroyed (exactly one)** | I, K | 16–19 m to the eaves (five storeys of 3.0–3.4 m, AST-04) plus a 3–4 m roof; 12–15 m deep; 40–80 m long in 3–5 stairwell sections; brick walls 0.5–0.65 m; windows 1.3 × 1.8 m. Damage kit: one end with its facade torn off and rooms open on every floor; hanging and sagging slabs; **protruding rebar**; burnt windows with soot plumes; roof down to its timbers; contents spilling out | gap (open rooms about 3–4 × 3 m; window openings 1.3 × 1.8 m), snag (rebar, cables, fabric, balcony rails), wind (strong wake, recirculation 1.5–2.5 heights, gusty to 10), land (slabs) | P1 |
| U02 | Rubble mound of the block | K | 2–4 m high, spreading 15–20 m | pitfalls (voids), snag (timbers) | P1 |
| U02b | Collapsed timber roof pile (the roof of a house or wing fallen as one heap) | I | 3–4 m high, 6–10 m across | pitfalls (voids), snag (rafters, battens, sheet) | P2 |
| U03 | Row of garages (cooperative), damaged | R | 4–8 units, 3.5 × 6 m each | gap (open doors) | P2 |

## 8. Lines, poles and roads

| ID | Asset | Variants | Ref | Physics | Pri |
|---|---|---|---|---|---|
| L01 | Wooden pole | straight; leaning 5–15° with brace | H, J, L | snag, night landmark | P1 |
| L02 | Concrete pole with crossarm and insulators | straight; leaning | R | snag | P1 |
| L03 | Wires (generator: catenary between poles, sag) | **thin 3–6 mm, medium 7–12 mm, thick 13–32 mm cable** (classes from the lines sheet; a bundled insulated main line is 26–32 mm) | C, D, J, K | **PR-6 strike physics by diameter**; nearly invisible in the feed | P1 |
| L04 | Fallen or sagging wire across a yard | 2 | D | snag | P1 |
| L05 | Hanging cables (in ruins) | 2 | E, K | snag | P1 |
| RD01 | Asphalt road kit: straight, curve, junction; potholes, crater decals, cracked edges | kit | M, O | surface | P1 |
| RD02 | Concrete road plates (about 6 × 2 m), laid as a track | kit with gaps and tilt | PR-8 | surface, uneven joints | P1 |
| RD03 | Dirt track ruts (decal plus surface) | kit | L, R | surface | P1 |

## 9. Vehicles (9 models × 3 states)

Sizes from [knowledge.md](knowledge.md) §9.2. Every model is built once, as a clean base plus state kits: A abandoned, B blast-wrecked, F burnt-out (sits on its rims 15–25 cm lower).

| ID | Model | Ref | Key size (L × W × H) | Pri |
|---|---|---|---|---|
| R01 | ВАЗ 2101–2107 classic sedan (roof rack option) | M | 4.07–4.17 × 1.61 × 1.44 m | P1 |
| R02 | ВАЗ 2109 hatchback | R | 4.0 × 1.65 × 1.40 m | P2 |
| R03 | ВАЗ 2121 Niva | R | 3.74 × 1.68 × 1.64 m | P1 |
| R04 | Daewoo Lanos sedan | N (class) | 4.24 × 1.68 × 1.43 m | P1 |
| R05 | Chevrolet Aveo | R | 3.92 to 4.31 × 1.68–1.71 × 1.50 m | P2 |
| R06 | Chery Amulet | R | 4.39 × 1.68 × 1.42 m (published) | P2 |
| R07 | VW Passat B3/B4 (or Golf II) | R | 4.58–4.68 × 1.71–1.74 × 1.43–1.46 m | P2 |
| R08 | ЗИЛ-130 flatbed truck | R | 6.68 × 2.50 × 2.40 m | P1 |
| R09 | Урал-4320 with canvas tilt (burnt: bare hoops) | O | 7.35 × 2.50–2.69 × 2.64–3.0 m | P1 |

- **Physics for every vehicle:** a few convex pieces plus thin capsules for torn parts; snag (torn sheet edges, sprung doors, hoops); wind (bluff body; a burnt shell is porous); land (roofs, truck beds); no gaps for cars (window openings 0.4–0.5 m are too small); the burnt Урал's bare bows leave real fly-through bays (vehicles sheet).
- **Debris field per state:** 5–20 m of glass crumbs, body fragments, trim. Burnt wrecks also get a 2–6 m soot patch.

## 10. Debris kit (shared)

| ID | Pieces | Ref | Physics | Pri |
|---|---|---|---|---|
| K01 | Bricks: whole, halves, chunks; brick clusters | C, E, D, I, K | rigid small obstacles; tilt at liftoff | P1 |
| K02 | Clay tile shards; slate (шифер) fragments | E, J | as K01 | P1 |
| K03 | Planks, beams, broken rafters | C, E, I, J, K | rock and slide under the legs; snag | P1 |
| K04 | Corrugated sheet, bent sheet metal | N | sharp edge (cuts fiber), sheet_metal material | P1 |
| K05 | Tarp, draped and torn | C, K | soft, snag, flutters (visual) | P1 |
| K06 | Concrete chunks with **protruding rebar** | K, R | snag, rigid | P1 |
| K07 | Glass crumbs (decal plus micro) and vehicle fragments (doors, bumpers, wheels) | M, N | slippery layer, rigid pieces | P1 |
| K08 | Household items: fridge, wardrobe, mattress, radiator, chair | K, I | mostly visual; large ones are obstacles | P2 |
| K09 | Tyres, metal barrel, bucket, crates | R | obstacles | P2 |
| K10 | Fabric and cables hanging from ruins | K | snag | P2 |

## 11. Drone (phase 5, listed for completeness)

| ID | Asset | States | Pri |
|---|---|---|---|
| D01 | **Svinoriz 10 Opto**: 10" X frame, 3-blade props, coil underneath, two battery packs, generic payload on top, 0° camera, legs | legs on / legs off (with launch stand) | phase 5 |
| D02 | Launch stand (D-022): an X-shaped steel base with four posts about 0.25 m high, one under each motor (on the diagonals, 0.226 m from the centre), sized from `drone.json`; unanchored | — | phase 5 |

## 12. Fortifications (added by D-035; an object sheet `docs/object-sheets/fortifications.md` comes first)

The pilot (2026-10-18): the villages of today carry concertina wire ("єгоза"), dragon's teeth, a couple of anti-tank hedgehogs, and trenches in the fields. Built as the real thing (researched: sizes, steel sections, concrete, how they stand, sag and fall), never as game props. Sizes below are starting ranges for the sheet to confirm from published sources.

| ID | Asset | Variants or states | Ref | Key size | Physics | Pri |
|---|---|---|---|---|---|---|
| F01 | Concertina wire (єгоза): a single coil run on pickets, a double-stacked run, a coil along a fence or wall top | I (taut) / D (torn, sagging, trampled flat in places) | R | coil Ø 0.90 m on pickets, 0.45 m on a fence top (published); 56 turns at a 0.268 m pitch stretched (derived); tape 16 mm across, barbs every 35 mm (published); pickets every 3.8 m (derived) | the fiber's and the props' worst snag: every strand an `edge` with the barb pitch recorded, coils as wind-porous volumes, gaps none (a coil is never passable) | P1 (world) |
| F02 | Dragon's teeth (зуби дракона): concrete pyramids in 3–5 staggered rows, some toppled or chipped | I / D | R | 1.0 m tall (published 0.8–1.2 m), 1.2 m base (derived), 0.3 m top (assumed), 1.5 t (derived); 4 staggered rows, 2.0 m centres in and between rows (published) | `concrete` collision, not landable, `edge` snags on arrises, the spaces between teeth are real gaps measured against the drone | P1 (world) |
| F03 | Anti-tank hedgehog (протитанковий їжак): welded angle iron or rail, singles and pairs at road approaches and checkpoints | I / D (bent, rusted) | R | arms 1.8 m (published); 1.04 m tall, 1.47 m across the feet (derived); Р50 rail (published); about 0.29 t (derived) | `steel` collision, `edge` snags on every member end, the spaces between arms as gaps | P1 (world) |
| F04 | Checkpoint kit: concrete road blocks, sandbag positions, a lifting barrier | I / D | R | ФБС 24.6.6 blocks 2.38 × 0.60 × 0.58 m, 1.96 t (published, ГОСТ 13579-78); sandbags 0.48 × 0.30 × 0.12 m filled, 24 kg (derived); barrier arm 4.0 m (published), underside 1.0 m (derived) | `concrete`, `fabric` (sandbags), landable block tops | P2 |

Trenches and firing positions in the fields are E01–E04 placed by the layout (WLD-01), not new assets.

## Totals

| Group | Base assets | Variants and states (approx.) |
|---|---|---|
| Surfaces | 13 | 13 (plus tilled directions) |
| Earthworks | 7 | 13 |
| Vegetation | 21 (plus impostors, micro-detail) | 45 |
| Houses | 6 | 18 |
| Outbuildings and yard | 19 | 30 |
| Public | 6 | 13 |
| Urban edge | 3 | 3 |
| Lines and roads | 8 | 14 |
| Vehicles | 9 | 27 |
| Debris | 10 kits | about 40 pieces |
| **Total** | **about 100** | **about 215** |
| **P1 subset (gallery G2)** | **about 65** | **about 140** |

**Look-lock order** (G1, before mass production): one asset per class first:
1. S04 belt_straw ground
2. V01 black locust belt section with V13
3. H01 adobe house in all three states
4. R01 ВАЗ in A and F
5. E01 trench
6. L01/L03 pole and wires

The pilot takes a short look in the gallery. Then the rest is produced in the locked style.
