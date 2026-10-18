# Object sheet: houses and yard

**Owner:** reference-analyst. **Reviewed for buildability by:** asset-artist.

**OPSEC:** cite clips and photos by letter only. Describe classes, never a specific identifiable building, place, date or person. No file names, and no OSD values.

**Rows:** H01–H06 (village houses), O01–O06 (outbuildings), C01–C02 (cellars), Y01–Y11 (yard elements), from [asset-list.md](../asset-list.md) §4–5. **Letters read:** E, J, C, D, I (contact sheets E 24 stills, C 19, D 21; photos J and I as single stills; a few native stills for measurements). Research fills what the footage can't show; every URL is a general, non-identifying source.

## How to read this sheet

- **Labels:** measured (from a letter, with the still number), published (with its source), derived (with the derivation), assumed. Exact sizes fixed by AST-04 are used as given: brick 250 × 120 × 65 mm, doors 0.8 × 1.8–1.9 m, storeys 2.8 m.
- **Reuse:** sizes and colours already in [terrain.md](../reference-notes/terrain.md) §3 W4 and §4 B1–B6 are cited, not re-measured. `feed` colours are mean sRGB of the goggles feed (warm and saturated in sun); `photo` colours are from the photos J and I and are closer to albedo. Textures are authored at photo albedo; the feed effect adds the shift.
- **The drone against an opening** (used by every "Gaps" line): prop tip to prop tip **0.574 m nose-first, 0.706 m on the diagonal** (knowledge.md §5.4). The footprint across an opening at yaw θ is `w(θ) = 0.254 + 0.32·(|cos θ| + |sin θ|)` m (derived: 10″ props on motors 0.226 m from the centre, 0.16 m in x and y), so an opening of clear width W passes nose-first when W > 0.574, passes at **any yaw** when W ≥ 0.706, and in between only up to `θmax = asin((W − 0.254) / 0.4525) − 45°`. Worked values: 0.60 m → ±5° and 13 mm a side; 0.65 m → ±16°, 38 mm a side; **0.70 m → ±35°, 63 mm a side (PR-9)**; 0.75 m → any yaw, 22 mm a side on the diagonal; 0.80 m → any yaw, 47 mm a side on the diagonal. The vertical envelope with legs is about **0.55 m** (assumed: 0.25–0.35 m legs under a frame with the payload on top; phase 5 fixes it from `drone.json`), so a clear height of 0.7 m is comfortable and 0.6 m is tight.
- **Physics ids** are the closed list in [physics_materials.json](../../assets-src/physics_materials.json): `steel`, `sheet_metal`, `timber`, `masonry`, `concrete`, `glass`, `cable`, `foliage`. Ids written as `proposed:<id>` are new and need physics sign-off (see Open points at the end).
- **Porosity** is the open-area fraction a wind volume lets through (0 solid, 1 free air).

## Reference views (REAL-02)

These are the scenes the gallery and the world recreate for the side-by-side sheets (REAL-03, `reference/_compare/`, never committed). The comparison is made once the assets exist; the view is fixed here.

| Letter | Scene type | Viewpoint | Framing | Light, time, weather |
|---|---|---|---|---|
| E | A damaged adobe house seen from its own yard, then entered | A hover 1–1.5 m up, 8–10 m from the facade (E 1–12); the approach to the door at 1 m (13–15); inside the room looking at the far window (16–18) | The facade fills the width: stripped tile roof, chimney right of centre, the door centre, a window right, a grey plank lean-to shed and fence at the left corner, tall dry weeds in the foreground, tile shards at the wall foot | Soft clear light, sun behind and left of the camera, no hard shadows; late summer; dry |
| J | A village street with two damaged single-storey brick houses | Standing height on the road verge, the houses 15–25 m away at 30–40° to the street line | Left house: slate roof partly down, plank gable, light-blue frames and door; right house: roof stripped to the rafters, teal frames, a satellite dish; brush piles, slate fragments and timber between camera and houses; an overhead wire crossing | Overcast, flat; late winter or early spring (bare trees, dead grass) |
| C | A village yard with a cellar entrance, then entered | A hover 1–1.5 m up, 6–8 m from the cellar head (C 1–8); the approach into the doorway (9–12) | The old fruit tree left, the cellar head centre-right with its brick piers and the tarp on its right flank, leaf litter below, a grey shed wall far left, an overhead wire crossing the sky | Bright sun from the front-right, shadows toward the camera; autumn; dry |
| D | An overgrown plot: weeds, a dead tree, a stone pile | A hover 1–2 m up moving through the weeds (D 1–12), then a dive onto the stone pile (13–14) | Looking into the sun: the dead trunk centre, weeds to 1.5 m, young trees and a belt behind, a thin dark wire from the dead trunk, a pile of pale stones right | Strong backlight and lens flare; late summer; dry |
| I | The urban edge: a burnt five-storey block, a collapsed roof pile, a damaged two-storey house, a low masonry fence | Standing height in a yard or street, the fence 5–8 m away, the roof pile 20–30 m, the two-storey house 25–35 m, the block 60–80 m | The block left and far, the roof pile centre, the two-storey house right with its roof skeleton and open upper floor, rubble between, the lattice-topped fence in the near foreground | Low sun from the left, long shadows, blue shade; winter or early spring (bare trees) |

Note for everyone reading C, D and E: the bright white bars at mid-height left and right of the picture are the OSD's artificial-horizon bars, not wires. The real wires are the thin dark lines.

## Shared: how the village houses are built

Everything in this section is reused by H01–H06 and by the brick or timber outbuildings; each row states only its deltas.

### Adobe (саман) and clay (мазанка) walls
- **Foundation:** none or a rubble-stone or brick strip 0.3–0.5 m deep, with a clay bench (призьба) 0.3–0.5 m high and 0.3–0.5 m wide at the wall foot on older houses (assumed from tradition; E shows no distinct bench, the foot is simply eroded).
- **Wall:** adobe blocks about **250 × 400 × 200 mm, 10–12 kg dry** (published: https://repo.snau.edu.ua/bitstream/123456789/7568/1/ , a Ukrainian agrarian university text on clay building), laid in clay mortar; historical adobe walls **0.35–0.60 m** thick (published: https://booksite.ru/fulltext/1/001/007/029/29036.htm ); **0.4–0.5 m** measured in E (terrain.md B1). A мазанка (H05) can instead be wattle-and-daub: posts 0.12–0.15 m at 1–1.5 m, woven withies, clay daub 0.25–0.35 m total (assumed from tradition).
- **Render:** clay-straw render 15–30 mm (assumed), then lime whitewash, renewed every spring, often tinted blue inside (published general: https://novyny.live/neruhomist/khata-mazanka-iak-nashi-predki-zvodili-zhitlo-shcho-stoyit-vikami-254449.html ; the blue inner wall shows in E 16–17). The research recipe in asset-list.md §4: whitewash #c4bfb1 over clay render #97754f over adobe #7d6146, red-clay base band (підводка) **0.42 m** high, about 30 % of the whitewash gone around openings, corners, the foot and the wall head.
- **Floor and ceiling:** a floor near grade (E: the threshold sits at the yard level, measured still 7); a main beam (сволок) 0.20 × 0.25 m across the room with 0.15–0.20 m joists, boards above, and a clay-straw layer 0.15–0.25 m on the boards as insulation (assumed from tradition). Room height 2.2–2.6 m (derived from the 2.3–2.6 m wall).
- **Interior (E 16–17, measured):** whitewashed walls with a blue tint, plaster peeling in 0.3–1 m flakes, a ceiling still in place over the door room, fallen boards and plaster on the floor, one cable looping down from the ceiling, daylight through the far window.

### Brick walls (silicate and red)
- **Bricks:** 250 × 120 × 65 mm single, 250 × 120 × 88 mm thickened (published: https://dnaop.com/html/60081_2.html , the standard's size table). Silicate brick is pale grey-white; red clay brick is orange-red.
- **Walls:** 1.5 bricks (0.38 m) or 2 bricks (0.51 m) with 10–12 mm joints (derived from the brick size); a rubble or concrete strip foundation with a rendered plinth (цоколь) 0.3–0.6 m above grade, often cement-grey or tar-black; corners, bands and window surrounds in red brick as decoration on silicate walls (J left house shows a red patch under a window; assumed as a common 1960s–80s pattern); a corbelled cornice of 2–4 courses stepping out 30–60 mm each at the wall head (measured J, both houses).
- **Lintels:** precast reinforced concrete, 0.12–0.14 × 0.25 m per leaf, 2–4 bars of 8–12 mm (assumed from standard practice): **the rebar that protrudes when a lintel breaks (PR-5).** Sills: brick on edge or a concrete slab with a mortar wash.
- **Floors (H06):** precast hollow-core slabs 0.22 m thick, 1.2 m wide, spanning 4.8–6 m (assumed standard sizes; I shows a slab edge in the open upper floor).

### Roofs
- **Carpentry:** a wall plate (мауерлат) on the wall head (visible in E 7 as a pale timber along the eaves), rafters 0.10–0.15 m thick at **0.9–1.2 m** (measured E 7 and J right house: the rafter count across the front), ridge board, collar ties; eaves overhang 0.3–0.5 m with the rafter tails open or boxed with boards. Gable ends are brick, adobe or vertical boards (J left house: dark vertical boards with a small attic opening).
- **Clay tile (H01):** battens 50 × 50 or 25 × 100 mm at **0.30–0.35 m** (published for strip tiles: https://www.nexe.hr/wp-content/uploads/2022/07/Katalog-zbirni-A4-2021-Ruski.pdf , batten pitch 30–35 cm; measured E: 0.3–0.4 m, terrain.md B1). Tiles are pressed or strip clay tiles about 0.33–0.40 × 0.20–0.24 m hung by a nib, head lap 70–90 mm, half-round ridge and hip tiles (assumed from the batten pitch and tile types at https://bigenc.ru/c/cherepitsa-2ae19a ). Pitch 35–40° (derived: ridge 5 m over a 6 m depth with 2.4 m walls).
- **Asbestos-cement slate, шифер (H02–H05, sheds):** 8-wave sheets **1750 × 1130 × 5.8 mm** (5.2 mm older), wave pitch **150 mm**, wave height **40 mm**, about 20–26 kg (published: https://poradu.pp.ua/dim/print:page,1,33280-rozmr-lista-shiferu-8-hvilovogo-rozrahunok-materalu.html and https://epicentrk.ua/shop/shifer/ ), laid with one-wave side laps and 0.15–0.2 m end laps on battens 50 × 50 mm at **0.5–0.75 m** (3–4 battens per sheet; assumed from the sheet length), nailed through wave crests. Pitch 20–30°. Weathered colour `photo` #736767 (J).
- **Sheet metal (H04 option, O06):** galvanised or painted corrugated or seamed sheet 0.5–0.7 mm on battens at 0.5–0.6 m; it dents and tears instead of shattering; `sheet_metal`.
- **Chimney:** brick, 0.5 × 0.5 m, 0.5–0.8 m above the ridge with a 2-course cap (measured E 7: 3–4 courses show above the tiles, red brick `feed` #94625a).

### Joinery
- **Doors:** plank doors (дощаті на планках): 3–5 boards 25–40 × 120–200 mm, two ledges and a diagonal brace, strap hinges, in a 0.10 × 0.08 m frame; leaf **0.8 × 1.8–1.9 m** (AST-04); threshold 0.1–0.15 m. E 7 shows a reddish-brown leaf with its brace, hanging inward on one jamb (`feed` #7a5647). Brick houses have panelled doors painted light blue or green (J left: `photo` #bad3d7).
- **Windows:** adobe houses **0.8 × 1.0 m** (AST-04 for H01), sill 0.8–1.0 m above the floor; brick houses **1.2–1.5 wide × 1.4–1.5 m high** (measured J: taller than wide, about 1.0–1.3 × 1.4 m), sill about 0.9 m. Double frames (summer and winter), 60–70 mm timber, two casements, glazing bars making 2 × 3 panes, painted light blue, turquoise or white (J: `photo` #bad3d7 and #6a766d). In the sim a window is an empty frame or broken glass, never a blue plane (knowledge.md §14.4). Shutters (віконниці) optional, 25 mm boards, same paint.

### How they fail (the D and X states)
Published side-on overpressure thresholds ( https://safteng.net/?p=2300 , after Lees): glass 1–7 kPa (0.15–1 psi); minor house damage about 5 kPa; corrugated panels fail and wood panels are blown in at 7–14 kPa; partial collapse of walls and roofs at about 14 kPa. Flat asbestos-cement sheet "is readily fractured by blast" ( https://www.nature.com/articles/146435a0.pdf ). So, with distance from a burst, the order is:
1. **Glass** goes everywhere within tens of metres: crumbs inside and out, shards left in the frames.
2. **Roof cover:** slate shatters into 0.1–0.6 m fragments that slide off and scatter 5–15 m (J); clay tiles lift, slide and break at the eaves and the wall foot (E: shards piled at the foot, `feed` #805c43); sheet metal tears and folds.
3. **Joinery:** sashes and doors blown in (E: the door leaf hangs inward; J: the sash hangs bent from the frame), frames torn from their jambs.
4. **Timbers:** battens broken at 10–40 % of spans with splintered ends, 1–3 rafters knocked off the plate or snapped (E, J right, I), the gable boards gone (J left).
5. **Masonry:** adobe cracks and sheds its render in 0.5–2 m patches, corners and wall heads crumble into blocks; brick comes out whole or halved along the bed joints, so a breach edge is **stepped**: 65 + 12 mm courses and 125 or 250 mm bricks jutting 0.1–0.4 m (PR-5, J right house's central opening); lintels crack and drop, rebar bends out 0.2–0.5 m.
6. **Collapse:** a gable falls in, then the roof sits on the floor (I: the roof skeleton still up, the upper facade gone); walls stand to 0.5–1.5 m with sloped rubble inboard and outboard.
7. **Fire** (optional on any state): soot plumes 0.5–1.5 m above openings, roof timbers burnt to charred stubs, adobe survives, brick spalls and reddens, the whitewash blackens.

Rubble is always coloured from the house's own materials (knowledge.md §14.4): adobe tan, clay render, whitewash flecks, red tile, pale silicate or red brick, grey mortar, grey-brown timber. Never near-white.

### Shared house generator parameters
Every H row uses this set; a row's table lists only additions and changed defaults.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `length`, `width` | m | 6–12, 4–10 | per row | Footprint. Rooms are laid out from it: сіни (hall) at the door, 2–3 rooms |
| `wall_height` | m | 2.2–3.0 | per row | Eaves height; the room height follows |
| `wall_thickness` | m | 0.25–0.60 | per row | Wall mass, reveal depth, how deep an opening's jag can go |
| `wall_material` | enum | adobe, wattle, silicate, red_brick | per row | Texture set, rubble colour, breach edge style (crumbled vs stepped) |
| `render_loss` | 0–1 | 0–0.8 | 0.3 | Fraction of render gone (patches around openings, corners, foot, head), writes the damage mask R |
| `whitewash_loss` | 0–1 | 0–1 | 0.3 | Fraction of whitewash gone on the remaining render, mask B |
| `base_band_height` | m | 0–0.6 | 0.42 | Red-clay or tar band at the foot, mask A |
| `roof_type_front`, `roof_type_back` | enum | gable, hip, half_hip, boarded_gable | per row | End shape per end (E reads hipped at one end) |
| `roof_pitch` | deg | 18–45 | per row | Slope; sets the ridge height |
| `roof_cover` | enum | clay_tile, slate, sheet_metal, none | per row | Cover geometry (every tile and sheet a mesh), batten pitch, failure style |
| `batten_pitch` | m | 0.30–0.75 | by cover | Batten spacing; intact battens block the drone (< 0.574 m) |
| `rafter_pitch` | m | 0.8–1.2 | 1.0 | Rafter spacing; the roof bay width once battens are gone |
| `eaves_overhang` | m | 0.2–0.6 | 0.4 | Overhang; snag edge of the rafter tails |
| `chimney` | pos or none | — | near the ridge, 0.3–0.4 L | Chimney position |
| `door_side`, `door_pos` | enum, 0–1 | — | front, 0.5 | Where the door is along the wall |
| `windows` | list | 2–8 of (side, pos, w, h) | per row | Opening positions and sizes |
| `veranda` | size or none | 2–3 × 3–6 m | none (H04: on) | A closed glazed porch at the door |
| `damage` | 0–1 | — | 0 / 0.55 / 1.0 | The master dial: I below 0.25, D 0.25–0.8, X above 0.8; every item below defaults from it and can be overridden |
| `roof_strip` | 0–1 | 0–1 | from `damage` | Fraction of cover gone, patchy (the remaining cover clusters at the eaves and the lee slope, as in E) |
| `batten_break` | 0–1 | 0–0.6 | from `damage` | Fraction of batten spans broken, ends irregular (never a regular grid, knowledge.md §14.4) |
| `rafters_broken` | int | 0–4 | from `damage` | Rafters snapped or off the plate |
| `opening_jag` | m | 0–0.4 | from `damage` | How far broken edges jut into each opening (the gap polygon is the real clear space) |
| `wall_breaches` | list | 0–3 of (side, pos, w, h) | from `damage` | Blast holes through walls, 0.5–2 m, jagged |
| `gable_breach` | 0–1 | 0–1 | from `damage` | How much of a gable is missing |
| `collapse` | list | 0–2 walls to a height | from `damage` | Walls down to 0.5–1.5 m, with their rubble |
| `soot_openings` | int | 0–all | 0 | Openings with soot plumes, mask G |
| `rubble_volume` | m³ | 0–40 | from `damage` | Rubble at the foot and inside, from the house's own materials |
| `interior_debris` | 0–1 | — | from `damage` | Boards, plaster, tile and furniture on the floor |
| `hanging_cables` | int | 0–3 | from `damage` | Cables looping from the ceiling or eaves (snag) |
| `seed` | int | — | — | Every random choice above |

## H01 Adobe (саман) house, lime-whitewashed, gable roof, clay tiles

**Seen in:** E (whole clip; facade in 1–12, door approach 13–15, interior 16–18). Similar stripped roofs in J.
**Research sources:** the shared section above (adobe blocks, wall thickness, whitewash, tile batten pitch, blast thresholds).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Footprint | 8–12 × 5–7 m | 10 × 6 m | measured E (terrain.md B1) |
| Wall height to the plate | 2.3–2.6 m | 2.4 m | measured E 7: the door head 1.9 m plus about 0.5 m to the plate |
| Wall thickness | 0.4–0.5 m | 0.45 m | measured E (B1); published 0.35–0.60 |
| Ridge height | 4.6–5.4 m | 5.0 m | measured E (B1); derived pitch 38° |
| Rafter pitch | 0.9–1.2 m | 1.0 m | measured E 7 |
| Batten pitch | 0.30–0.40 m | 0.33 m | measured E (B1); published 0.30–0.35 |
| Chimney | 0.5 × 0.5 m, 0.5–0.8 m above the ridge | 0.5 × 0.5 × 0.6 m | measured E 7 |
| Door | 0.8 × 1.8–1.9 m | 0.8 × 1.9 m | AST-04; E 7 |
| Windows | 0.8 × 1.0 m, sill 0.9 m | 3 front, 1 back, 1 per gable end | AST-04; E 7 (one right of the door, one left behind the shed) |
| Eaves overhang | 0.3–0.5 m | 0.4 m | assumed |
| Base band | 0.42 m | 0.42 m | research (asset-list §4) |
| Interior room depth | 4–6 m | 5 m | derived from the footprint; E 16 shows the far window 4–6 m in |

### Construction and materials
- Adobe wall, clay-straw render, lime whitewash, red-clay base band (shared section). Wall plate, rafters, battens, pressed clay tiles, brick chimney. Plank door with a brace. Timber window frames, empty.
- **Materials:**
  - adobe (exposed): Poly Haven `rough_plaster_brick_02` or ambientCG `Plaster006`, tinted #7d6146, with straw flecks added in the derived facade map (candidates, CC0: https://polyhaven.com/a/rough_plaster_brick_02 , https://ambientcg.com/view?id=Plaster006 )
  - clay render: Poly Haven `worn_cracked_plaster` tinted #97754f ( https://polyhaven.com/a/worn_cracked_plaster )
  - whitewash: Poly Haven `white_stucco_02` tinted #c4bfb1 ( https://polyhaven.com/a/white_stucco_02 ); blue interior whitewash: `blue_plaster_weathered` ( https://polyhaven.com/a/blue_plaster_weathered )
  - clay tiles (each tile a mesh; the texture is the clay surface): Poly Haven `clay_roof_tiles_02` or ambientCG `RoofingTiles013A` ( https://polyhaven.com/a/clay_roof_tiles_02 , https://ambientcg.com/view?id=RoofingTiles013A )
  - roof timber and door: Poly Haven `rough_wood`, `raw_plank_wall`, `rough_pine_door` ( https://polyhaven.com/a/rough_wood , https://polyhaven.com/a/raw_plank_wall , https://polyhaven.com/a/rough_pine_door )
  - chimney: manifest `red_brick` (Poly Haven, already pinned)
  - **Derived:** one facade map per wall in asset space, upright and continuous (knowledge.md §14.4), plus the damage mask (R render depth, G soot, B dirt and streaks, A base band). Rubble uses the same albedos.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Whitewash, weathered | #c4bfb1 | — (none left on E's facade) | research recipe |
| Clay render remnant | #97754f | #896b43 | recipe; measured E 7 |
| Exposed adobe, lit | #7d6146 | #936938 (lit), #755f40 shade to #9f713b lit | recipe; measured E 7; terrain.md B1 |
| Base band (red clay) | about #8a4a33 | — | assumed from the recipe's wording |
| Clay tiles on the roof | about #a0613f | #8a684f (mixed with battens: #967056) | assumed; measured E 7; B1 |
| Tile shards at the foot | as tiles | #805c43, B1 #825f44 | measured E 7 |
| Wall plate, rafters, battens | grey-brown | #866c4d | measured E 7 |
| Chimney brick | as `red_brick` | #94625a | measured E 7 |
| Door leaf | dark red-brown paint | #7a5647, B1 #7c5848 | measured E 7 |
| Interior wall (blue whitewash, shade) | about #a7b0b8 | #454347 (deep shade), jamb #918c91 | measured E 16 |
| Interior floor debris | timber and plaster | #5f5e60 | measured E 16 |

### Variants and states
- Variants: 1 = the E house (hip at the right end as read in E 1–7, gable at the left, tiles); 2 = gable both ends, 8 × 5 m; 3 = 12 × 7 m with a back extension (сіни annex 3 × 4 m, lean-to roof).
- **I:** render loss 0.2–0.4, whitewash loss 0.3, roof intact with 2–5 % tiles slipped, windows empty frames or glass shards in the corners, door closed or ajar, weeds and litter at the foot.
- **D (the E state):** roof stripped 60–90 % with tiles left in clusters on the lower slope and the lee end, battens broken in 20–40 % of spans, 1–2 rafters off, the door leaf blown inward and hanging on one hinge, all windows out with crumbled edges 0.1–0.3 m, 0–1 wall breach 0.6–1.2 m on the back wall, tile shards piled 0.2–0.4 m deep along the foot (1–2 m wide), interior with ceiling boards and plaster down over 30–60 % of the floor, one cable hanging, soot on 0–1 openings.
- **X:** roof gone, rafters lying across the walls and inside, the gable and one long wall down to 0.5–1.5 m, chimney a stump, rubble 1–2 m high inboard and 1 m outboard, the floor plan readable.

### Damage pattern
Tiles first (they lift and slide), then the door and sashes, then battens and rafters, then adobe corners and wall heads shedding blocks; the render falls in 0.5–2 m patches wherever the wall flexed. Rubble: adobe blocks and crumbs (#7d6146), render (#97754f), whitewash flecks, tile shards (#825f44), battens and boards, a little glass. Soot only if burnt.

### Physics role
- **Collision:** walls as boxes per wall segment between openings (thickness 0.45 m), the plate and each rafter as boxes, each batten as a box 0.05 × 0.05 m, remaining tiles as thin boxes in rows, the chimney a box, the door leaf a box 0.04 m thick at its hung angle, the window frames as four thin boxes each, interior floor debris as a few boxes and capsules, the tile heap as a low sloped box (`masonry`). Collision follows the jagged visual within ±2 cm around openings (AST-05).
- **Material ids:** adobe, render, tiles, chimney, heap: `masonry` (tile breakability later); timbers and door: `timber`; glass shards in frames: `glass`; the hanging cable: `cable`.
- **Gaps:**
  - door 0.80 × 1.9 m clear (leaf open inward): **passes at any yaw**, 113 mm a side nose-first, 47 mm on the diagonal; the room behind is 5 × 4 m, 2.3 m high
  - each window 0.80 × 1.0 m with the frame gone; with the frame in place 0.66 × 0.86 m: passes within ±18° yaw, 43 mm a side; with a sash remnant, not passable until broken
  - the far window as the exit (E 16–18): same as a window
  - roof bay between two rafters with battens gone: 0.9–1.1 × the slope length, **passes**; with intact battens (0.33 m), **no**
  - gable breach: as generated, recorded from the real clear polygon
  - wall breach 0.6–1.2 m: passes nose-first when ≥ 0.6 m clear after jags
- **Wind volumes:** walls solid (porosity 0); intact roof 0; stripped roof field 0.7–0.85 (battens and rafters block 15–30 %); remaining tile clusters 0; the interior is a sheltered cavity once the roof is open, with flow through the door and the far window (a through-draught).
- **Snag hazards:** batten ends, rafter tails at the eaves, the chimney cap, the hanging door leaf's edge, window frame remnants, the interior cable, nails in fallen boards.
- **Landable surfaces:** none (slopes slide, rubble tilts). The tile heap and interior debris are uneven `rubble` ground.

### Placement rules
One or two per village plot, long side along the street, 5–15 m back from the fence line, the door to the yard side; a yard set around it (O01/O02, C01, Y01/Y06, V10/V11, S09 litter, V17 weeds). Variants mixed so the village isn't one house repeated (terrain.md B2).

### Generator parameters
Shared table, with these defaults: `wall_material` adobe, `wall_height` 2.4, `wall_thickness` 0.45, `roof_type_front` gable, `roof_type_back` hip (variant 1), `roof_pitch` 38, `roof_cover` clay_tile, `batten_pitch` 0.33, `windows` 0.8 × 1.0, `base_band_height` 0.42.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `tile_cluster_bias` | 0–1 | 0–1 | 0.7 | How strongly the surviving tiles cluster at the eaves and the lee end instead of scattering |
| `shard_heap_depth` | m | 0–0.5 | from `damage` | Depth of the tile heap along the foot |
| `door_leaf_state` | enum | closed, ajar, hanging, inside, gone | hanging | Where the leaf is and its collision |
| `interior_ceiling_down` | 0–1 | 0–1 | from `damage` | Fraction of ceiling boards and clay fallen |

### Open points
- The E roof end: the right end reads hipped in E 1–7 and the left gabled; the generator's per-end `roof_type` covers both, and variant 1 copies E. Settled at the E side-by-side.
- Tile size and profile are assumed from the batten pitch; settled by the look-lock (G1), not by the pilot.
- Tile breakability under the legs (a tile cracks, a shard slides) needs a material property beyond `masonry`: physics sign-off (see the end of the sheet).

## H02 Adobe house, hip roof, asbestos-cement slate (шифер)

**Seen in:** J (the roofs and the stripped rafters, on brick walls), R for the adobe-plus-slate combination.
**Research sources:** shared section (slate sheet sizes, batten pitch, blast fracture of asbestos-cement).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Footprint, walls | as H01 | 9 × 6 m, 2.4 m, 0.45 m | as H01 |
| Roof pitch | 20–30° | 25° | published practice for slate; J roofs read 20–25° (measured J, rough) |
| Ridge height | 3.6–4.2 m | 3.8 m | derived: 2.4 + 3 × tan 25° |
| Slate sheet | 1750 × 1130 × 5.8 mm, wave 150 / 40 mm | as published | published |
| Batten pitch | 0.5–0.75 m | 0.58 m (3 battens per sheet) | assumed from the sheet length |
| Door, windows | as H01 | as H01 | AST-04 |

### Construction and materials
- As H01 for the walls; a four-slope hip roof (вальмовий) of шифер on battens, with a sheet-metal or asbestos ridge cap; eaves 0.2–0.3 m; a brick chimney through the slope with a lead or cement flashing.
- **Materials:** walls as H01; slate: a true wave mesh with a grey cement surface, Poly Haven `concrete_wall_007` or ambientCG `Concrete034` tinted #736767, lichen and moss streaks in the derived map ( https://polyhaven.com/a/concrete_wall_007 , https://ambientcg.com/view?id=Concrete034 ); ridge cap `sheet_metal` with Poly Haven `rusty_metal_sheet` ( https://polyhaven.com/a/rusty_metal_sheet ).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Weathered slate | #736767 | — (no slate roof in a clip) | measured J (terrain.md B2) |
| Slate fragments on the ground | #81776e | — | measured J |
| Walls | as H01 | as H01 | — |

### Variants and states
- Variants: 1 = 9 × 6 hip; 2 = 11 × 7 hip with a веранда 2.5 × 4 m.
- **I:** slate grey-green with lichen, 1–3 sheets cracked, a slipped ridge cap; walls as H01-I.
- **D:** 40–80 % of sheets shattered and gone, the rest cracked and holed (holes 0.1–0.5 m with jagged edges), fragments 0.1–0.6 m scattered 5–15 m downwind and piled at the eaves, battens exposed and 10–30 % broken, joinery as H01-D.
- **X:** as H01-X with slate fragments in the rubble instead of tiles.

### Damage pattern
Slate shatters at low overpressure (published): it goes first, in fragments, not in sheets, and it scatters further than tiles. Rubble: adobe and render as H01, grey slate fragments (#736767) with their wave profile, timber.

### Physics role
- **Collision:** as H01; each remaining sheet a thin box following the wave (collision box 0.04 m deep, the wave is visual within AST-05's 5 cm), fragments on the ground as thin boxes.
- **Material ids:** slate and fragments: `proposed:asbestos_cement` (brittle: cracks and breaks under a landing leg or a strike; until signed off, `masonry`); the rest as H01.
- **Gaps:** as H01; holes in sheets 0.1–0.5 m **do not pass**; a bay with the sheets and battens gone 0.9–1.1 m wide **passes**.
- **Wind volumes:** walls 0; a slate field 0 when intact, 0.3–0.6 when holed, 0.7–0.85 when stripped to battens.
- **Snag hazards:** broken sheet edges (sharp but thick, 6 mm: a fiber bends over them rather than cutting), batten ends, the ridge cap edge, rafter tails.
- **Landable surfaces:** none.

### Placement rules
As H01; slate is the commoner roof in the pilot's villages (J: both houses), so H02 and H03 together outnumber H01.

### Generator parameters
Shared table with `roof_type_*` hip, `roof_pitch` 25, `roof_cover` slate, `batten_pitch` 0.58.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `sheet_holes` | int per sheet | 0–3 | from `damage` | Jagged holes 0.1–0.5 m in surviving sheets |
| `fragment_scatter` | m | 2–15 | 8 | Radius of the fragment field downwind |
| `ridge_cap` | enum | asbestos, sheet_metal | asbestos | Cap material and its snag edge |

### Open points
- `asbestos_cement` material id: physics sign-off.

## H03 Silicate-brick house, slate roof, painted window frames, satellite dish

**Seen in:** J (two houses; the right house is the model, the left house gives the plank gable, the light-blue door and the red-brick band).
**Research sources:** shared section (brick sizes, cornice, lintels, slate).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Footprint | 8–10 × 6–8 m | 9 × 7 m | measured J (terrain.md B2) |
| Wall height to the cornice | 2.7–3.1 m | 2.9 m | derived: 2.8 m storey plus the cornice |
| Wall thickness | 0.38–0.51 m | 0.38 m | derived from the brick (1.5 bricks) |
| Plinth | 0.3–0.6 m above grade | 0.4 m | assumed |
| Ridge height | 5–6 m | 5.5 m | measured J (B2); derived pitch 25–30° |
| Cornice | 2–4 courses, 30–60 mm steps | 3 courses | measured J |
| Rafter pitch | 0.9–1.0 m | 1.0 m | measured J right house |
| Batten pitch | 0.35–0.6 m | 0.5 m | measured J right house (rough) |
| Windows | 1.2–1.5 × 1.4–1.5 m, sill 0.9 m | 1.3 × 1.45 m, 3 front, 2 side, 1 back | measured J, rough; AST-04 brick |
| Door | 0.8 × 1.9 m, panelled, painted | — | AST-04; J left |
| Satellite dish | 0.6 m offset dish on a 0.3 m bracket at the eaves | 0.6 m | measured J (against the window) |
| Gable (variant 2) | vertical boards 25 × 120–150 mm, attic opening 0.4 × 0.5 m | — | measured J left house |

### Construction and materials
- Silicate brick walls 1.5 bricks thick, a rendered plinth, red-brick bands or surrounds, concrete lintels, a corbelled cornice, a hip roof of slate (variant 1, J right) or a two-slope roof with a boarded gable (variant 2, J left); painted double windows with 2 × 3 panes; a panelled door; a lean-to porch roof of grey corrugated sheet on timber posts at the door (J right, right side); an eaves gutter optional; a dish on the street corner.
- **Materials:** silicate brick: Poly Haven `white_bricks` tinted #a0968b ( https://polyhaven.com/a/white_bricks ); red bands: manifest `red_brick`; plinth: Poly Haven `concrete_wall_003` ( https://polyhaven.com/a/concrete_wall_003 ); slate as H02; frames and door: Poly Haven `blue_painted_planks` (light blue) and `green_rough_planks` (teal) ( https://polyhaven.com/a/blue_painted_planks , https://polyhaven.com/a/green_rough_planks ); gable boards: Poly Haven `dark_planks` ( https://polyhaven.com/a/dark_planks ); porch sheet: Poly Haven `worn_corrugated_iron` ( https://polyhaven.com/a/worn_corrugated_iron ); dish: `rusty_painted_metal` ( https://polyhaven.com/a/rusty_painted_metal ). Derived facade maps as H01 (brick courses upright and continuous; the first attempt's bricks were upside down, PR-5).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Silicate brick, lit | #a0968b (B2 #a79c8f) | — | measured J left house |
| Silicate brick, shade | #6d6e64 | — | measured J right house |
| Slate roof | #736767 | — | measured J |
| Light-blue frames and door | #bad3d7 | — | measured J left |
| Teal frames | #6a766d | — | measured J right |
| Gable boards | #645d59 | — | measured J left |
| Rubble at the foot (brick, mortar, plaster) | #958c83 | — | measured J right |
| Brush pile in the yard | #615a50 | — | measured J (V20's colour) |

### Variants and states
- Variants: 1 = hip roof, teal frames, dish (J right); 2 = boarded gable, light-blue frames and door, a red-brick band under the windows (J left); 3 = variant 1 with a веранда.
- **I:** brick sound, mortar washed, a few cracked slates, frames with peeling paint, glass shards in the corners, the dish in place, a lean-to porch.
- **D (J):** slate 60–100 % gone (J right: fully stripped, rafters and battens whole in the middle, a few sheets left at the top edge; J left: the gable half of the roof collapsed), window sashes bent and hanging, the central door opening and one window **widened by broken brick**: a stepped jagged edge 0.1–0.4 m deep, bricks out whole and halved, the lintel cracked with rebar showing, rubble of pale brick and plaster at the foot 0.2–0.5 m deep over 1–2 m, a timber beam leaning on the wall, the porch roof partly down, interior dark with a wire hanging, soot on 0–2 openings.
- **X:** the gable and one long wall collapsed, walls to 1–2 m, rubble of silicate brick (#a0968b) and grey mortar with slate fragments and timbers, the cornice in pieces.

### Damage pattern
Glass → slate shattered and scattered → sashes bent out of the frames → battens broken → bricks out around openings in steps along the courses, lintels cracked, rebar out 0.2–0.5 m → the gable down. Rubble: pale silicate brick and halves, grey mortar lumps, plaster, slate fragments, timber, glass crumbs. Fire adds soot plumes and a reddened brick face.

### Physics role
- **Collision:** as H01, brick walls 0.38 m; the jagged opening edges as stacked boxes following the stepped brick (±2 cm); lintel rebar as capsules 10 mm; the dish a thin cylinder on a capsule; porch posts capsules, porch sheet a thin box.
- **Material ids:** brick, plinth, cornice: `masonry`; lintels: `concrete`; rebar, dish bracket: `steel`; porch sheet and ridge cap: `sheet_metal` (cuts fiber); slate: `proposed:asbestos_cement`; frames, gable boards, rafters: `timber`; glass remnants: `glass`; hanging wire: `cable`.
- **Gaps:**
  - door 0.8 × 1.9 m: passes at any yaw (frame off: 0.8; frame on: 0.66 → ±18°)
  - windows 1.3 × 1.45 m frame gone: **passes at any yaw** with 0.3 m a side; frame on (1.16 × 1.3): any yaw; with a bent sash hanging (J): the clear polygon is the frame minus the sash, typically 0.6–0.9 m wide, recorded as generated
  - the widened opening (J right centre): about 1.5–2.0 × 2.0 m after jags: passes; its jags are the snag edge
  - roof bays 0.9–1.0 m with battens gone: pass; the attic (variant 2) through the gable opening 0.4 × 0.5 m: **no**
- **Wind volumes:** walls 0; roof 0 / 0.7–0.85 stripped; the porch roof 0; the dish negligible.
- **Snag hazards:** rebar, the dish and its bracket, batten ends, bent sashes with glass, the porch sheet edge (`sheet_metal`), the leaning beam, the hanging wire.
- **Landable surfaces:** none on the house; the porch roof (2 × 1.5 m, 10°) is a slide.

### Placement rules
Street-facing, 3–8 m behind the fence, the dish on the street side at the eaves; the second of the pilot's couple of houses (terrain.md B2); in J two stand 15–25 m apart along one street with a lean-to shed between them and brush piles (V20) in the yard.

### Generator parameters
Shared table with `wall_material` silicate, `wall_height` 2.9, `wall_thickness` 0.38, `roof_cover` slate, `roof_pitch` 25, `windows` 1.3 × 1.45.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `red_bands` | list | 0–3 bands or surrounds | 1 band under the sills | Red-brick decoration courses |
| `cornice_courses` | int | 2–4 | 3 | Corbel depth at the wall head |
| `frame_paint` | colour | light blue, teal, white, green | teal | Frame and door paint |
| `dish` | pos or none | eaves corner | on | The satellite dish and its bracket |
| `porch` | size or none | 1.5–2.5 × 1.5–3 m | on | Lean-to porch roof on posts |
| `brick_step_depth` | m | 0.1–0.4 | 0.25 | Depth of the stepped jag at broken openings |
| `rebar_out` | m | 0–0.5 | 0.3 | Length of exposed lintel bars |

### Open points
- Window size is read from the photo against a 0.8 m door and brick courses; ±0.1 m. Settled at the J side-by-side.

## H04 Red-brick 1980s house, larger, with veranda, metal or slate roof

**Seen in:** R. The brick construction is as H03; the veranda and sheet roof are research.
**Research sources:** shared section; sheet-metal roof practice (assumed standard).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Footprint | 10–12 × 8–10 m | 11 × 9 m | asset-list §4 (assumed) |
| Wall height | 2.9–3.1 m | 3.0 m | derived: 2.8 m storey plus a plinth step |
| Wall thickness | 0.51 m | 0.51 m | derived: 2 bricks |
| Roof | hip, 25–30°, ridge 5.5–6.5 m | 28°, ridge 6.0 m | derived |
| Veranda | 2.5–3 × 4–6 m, lean-to 12–18° | 2.5 × 5 m | assumed (typical closed porch) |
| Windows | 1.3–1.5 × 1.4–1.5 m | 1.4 × 1.45 m, 4 front, 3 side | assumed, as H03 |
| Veranda glazing | panes 0.4 × 0.5 m in a 1.2 m high band | — | assumed |
| Roof sheet | 0.5–0.7 mm, corrugated or seamed | corrugated | assumed |

### Construction and materials
- Red clay brick 2 bricks thick with a cement-rendered plinth, concrete lintels and sills, a hip roof of painted corrugated or seamed sheet steel (variant 1) or slate (variant 2), a glazed veranda along the yard side with a lean-to sheet roof, a brick chimney, eaves gutters. Windows and a panelled door painted white or brown.
- **Materials:** manifest `red_brick`; plinth `concrete_wall_003`; painted roof sheet: Poly Haven `rusty_painted_metal` or `corrugated_iron_02` ( https://polyhaven.com/a/corrugated_iron_02 ); veranda frames: Poly Haven `distressed_painted_planks` ( https://polyhaven.com/a/distressed_painted_planks ); slate as H02.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Red brick | as `red_brick` (about #9a5a44) | — | assumed from the set |
| Painted roof sheet | faded red-brown #7a4a3a or green #4f6a4a | — | assumed (typical paints) |
| Frames | white #d8d5cc | — | assumed |

### Variants and states
- Variants: 1 = sheet roof, veranda on the yard side; 2 = slate roof, veranda at the gable end.
- **I:** paint faded, rust streaks from the sheet laps, gutter sagging, veranda glass partly gone.
- **D:** sheet roof torn and folded back in 1–3 places (each 2–4 m), sheets flapping or lying in the yard, battens exposed under them; veranda glazing all gone and its light frame skewed; windows out with stepped brick jags; one wall breach 1–1.5 m; rubble of red brick at the foot.
- **X:** as H03-X in red brick; folded sheets in the rubble.

### Damage pattern
Sheet metal tears at fixings and folds instead of shattering: long sharp edges (the `sheet_metal` fiber cut, knowledge.md §7.4); the veranda, light and glazed, goes first. Brick jags as H03. Rubble: red brick (#9a5a44), mortar, sheet strips, veranda frames, glass crumbs.

### Physics role
- **Collision:** as H03; torn sheets as thin boxes at their folded angles; veranda frame as thin boxes.
- **Material ids:** brick `masonry`; lintels `concrete` with `steel` rebar; roof and torn sheets `sheet_metal`; veranda frames `timber`; glass `glass`.
- **Gaps:** door 0.8 × 1.9 any yaw; windows 1.4 × 1.45 any yaw; the veranda doorway 0.9 × 2.0 any yaw and the veranda interior 2.5 m deep; a torn-sheet roof opening 2–4 m: passes, its edges snag and cut; breach 1–1.5 m: passes.
- **Wind volumes:** walls 0; roof 0 where sheets remain, 0.7–0.85 where torn; the veranda 0.5–0.9 once unglazed.
- **Snag hazards:** torn sheet edges (cutting), rebar, batten ends, gutter brackets, veranda frame bars.
- **Landable surfaces:** the veranda roof when intact (2.5 × 5 m at 12–18°: slides) is not landable; none.

### Placement rules
The best house on a street, set back 5–10 m with a fenced front garden (Y02) and a metal gate (Y05); one or two per village.

### Generator parameters
Shared table with `wall_material` red_brick, `wall_thickness` 0.51, `roof_cover` sheet_metal, `veranda` 2.5 × 5.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `sheet_tears` | int | 0–4 | from `damage` | Torn and folded sheet areas, 2–4 m each |
| `sheet_fold_angle` | deg | 30–180 | 120 | How far a torn sheet folds back |
| `veranda_glazing_loss` | 0–1 | — | from `damage` | Panes gone |
| `gutter` | bool | — | on | Eaves gutter and brackets (snag) |

### Open points
- No reference letter: the pilot confirms the class at G2 (asset-list rule for R rows). Not a question to ask now.

## H05 Small old clay hut (мазанка), whitewashed, low roof

**Seen in:** R (the construction is the adobe family's; the hut class is the pre-war look the brief asks for).
**Research sources:** hut area 40–50 m², three rooms, thatch tradition, whitewash and blue tint ( https://www.unian.ua/society/hata-mazanka-yak-buduvali-hatu-mazanku-v-ukrajini-13413618.html , https://novyny.live/neruhomist/khata-mazanka-iak-nashi-predki-zvodili-zhitlo-shcho-stoyit-vikami-254449.html , https://osvita.ua/vnz/reports/culture/11521/ ).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Footprint | 6–8 × 4–5 m (24–40 m²) | 7 × 4.5 m | published 40–50 m² including the hall; asset-list |
| Wall height | 2.0–2.3 m | 2.1 m | assumed (low, "присадкувата") |
| Wall thickness | 0.3–0.45 m | 0.35 m | published 0.35–0.60; wattle 0.25–0.35 |
| Roof | hip, 40–50°, ridge 4.0–4.6 m | 45°, ridge 4.3 m | assumed: steep for thatch, kept under slate |
| Eaves | 0.5–0.7 m overhang, 1.8–2.0 m above grade | 0.6 m | assumed |
| Door | 0.8 × 1.8 m | — | AST-04 |
| Windows | 0.6–0.8 × 0.7–0.9 m, 2–3 | 0.7 × 0.8 m | assumed (small hut windows) |
| Призьба (clay bench) | 0.3–0.4 m high, 0.3–0.5 m wide | 0.35 × 0.4 m | assumed from tradition |

### Construction and materials
- Wattle-and-daub or adobe walls on a призьба, clay render, whitewash with the red-clay base band, a steep hip roof originally thatched and now slate (variant 1) or still reed-thatched and rotten (variant 2), a wide eaves overhang, a small brick or clay chimney, tiny windows with four panes, a plank door.
- **Materials:** as H01; thatch: Poly Haven `reed_roof_03` or ambientCG `ThatchedRoof001A` ( https://polyhaven.com/a/reed_roof_03 , https://ambientcg.com/view?id=ThatchedRoof001A ), built as bundles (geometry), not a card.

### Colour
As H01's whitewash, render and adobe; thatch grey-brown about #6f6552 (assumed).

### Variants and states
- Variants: 1 = slate over the old roof; 2 = reed thatch, grey and sagging.
- **I:** render cracked, whitewash patchy, the bench eroded, the roof sagging at the ridge, windows empty.
- **D:** the roof half gone (slate shattered or thatch burnt and blown), rafters bare, the door in, a corner crumbled to 1 m.
- **X:** the roof down inside the walls, two walls to 0.5–1 m, a tan rubble mound.

### Damage pattern
As H01; thatch burns completely and leaves charred bundles and a black ring; adobe survives fire.

### Physics role
- **Collision, ids:** as H01; thatch bundles `proposed:straw` (soft: sinks under a leg, snags).
- **Gaps:** door 0.8 × 1.8 any yaw; windows 0.7 × 0.8 clear: **±35° yaw, 63 mm a side**, height 0.8 m is tight (0.55 m envelope leaves 12 cm); roof bays as H01.
- **Wind volumes:** walls 0; thatch 0 when intact, 0.5 when burnt through.
- **Snag hazards:** thatch bundle ends, rafter tails at the low eaves (1.8–2.0 m, at flying height), the door leaf.
- **Landable surfaces:** none; the призьба is a 0.4 m shelf (too narrow for the legs at 0.24 m spacing plus the tip envelope: no).

### Placement rules
Rare: one per village at the old end of a street, behind a plank fence, often with a walnut (V12); abandoned before the war (state I is its normal look).

### Generator parameters
Shared table with `wall_material` wattle or adobe, `wall_height` 2.1, `wall_thickness` 0.35, `roof_pitch` 45, `roof_cover` slate or thatch, `base_band_height` 0.42, plus:

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `bench` | size or none | 0.3–0.4 × 0.3–0.5 m | on | The clay bench at the foot |
| `thatch_sag` | m | 0–0.4 | 0.15 | Ridge and eaves sag of the old roof |
| `thatch_burn` | 0–1 | — | from `damage` | Fraction burnt away |

### Open points
- `straw` material id for thatch (shared with Y09): physics sign-off.

## H06 Two-storey brick house

**Seen in:** I (the house at the right: roof skeleton standing, upper facade blown out, ground-floor openings widened, rubble in front).
**Research sources:** shared section (brick, slabs, roofs); the steep attic roof is read from I.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Footprint | 9–11 × 8–10 m | 10 × 9 m | asset-list; I (rough, against the 2.8 m storeys) |
| Storeys | 2 × 2.8 m | 5.6 m to the upper ceiling, 6.0 m to the eaves | AST-04 |
| Wall thickness | 0.38–0.51 m | 0.51 m ground, 0.38 m upper | derived |
| Floor slab | 0.22 m hollow-core, 1.2 m wide | — | assumed standard |
| Roof | gable or half-hip, 35–45°, ridge 9–10 m | 40°, ridge 9.7 m | measured I: a steep roof skeleton about 0.6 × the wall height |
| Windows | 1.3–1.5 × 1.4–1.5 m, 3–4 per floor per long side | 1.4 × 1.45 m | measured I (rough) |
| Door | 0.9 × 2.0 m | — | assumed (a wider later door) |
| Balcony (variant 2) | 1.0 × 3 m slab with a 1.0 m rail | — | assumed |

### Construction and materials
- Silicate or red brick walls with concrete lintels, precast floor slabs, a concrete stair, an attic under a steep timber roof (slate or sheet), rendered pale outside (I: pale grey-beige render with an exposed silicate corner), tiled bathroom walls inside (I: a blue-white tiled patch on the ground floor), plastered rooms painted blue or green.
- **Materials:** render: Poly Haven `plastered_wall_02` ( https://polyhaven.com/a/plastered_wall_02 ); brick as H03 (`white_bricks`) or `red_brick`; slabs and stair: Poly Haven `concrete_wall_003`, broken slab ends `concrete_debris` ( https://polyhaven.com/a/concrete_debris ); roof timbers `rough_wood`; interior tiles: a derived 0.15 m blue-white tile map (assumed); frames `blue_painted_planks`.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Render (mixed lit and shade, low sun) | #6b584a | — | measured I; a shadow-heavy read, author lighter at about #b8ad9c |
| Exposed corner brick (deep shade) | #282d32 | — | measured I; a shadow read, albedo as H03 |
| Rubble field in front (shade) | #3a5062 | — | measured I; blue shade, albedo as H03 rubble |
| Roof timber pile (nearby, part lit) | #5c4f43 | — | measured I |

### Variants and states
- Variants: 1 = rendered, half-hip roof; 2 = bare red brick with a balcony.
- **I:** render stained, roof intact, windows empty frames.
- **D:** roof cover gone with the full rafter skeleton standing (I), the upper-floor street facade blown out over 3–5 m width showing the slab edge, interior walls and furniture; ground-floor openings widened with stepped jags; the slab sagging 0.1–0.3 m at its free edge with rebar out; the corner brick exposed where the render fell; rubble 0.5–1.5 m deep in front over 4–8 m; a lean-to annex collapsed at one side.
- **X:** the roof down into the upper floor, the upper floor's walls to 1 m, the slab broken and hanging into the ground floor, a rubble mound 2–3 m high.

### Damage pattern
As H03, plus the slab: it cracks at its support, hangs at 10–30° with its bars out, and lies on the lower room's debris in X. Rubble: brick, render, slab chunks with rebar (K06), tiles, roof timbers, furniture (K08).

### Physics role
- **Collision:** as H03 per floor; the slab as a box per span (0.22 m) with its broken edge as a stepped box; the hanging slab at its angle; roof skeleton rafters as boxes; rubble as a sloped mesh-approximating set of boxes.
- **Material ids:** brick and render `masonry`; slabs, stair `concrete`; rebar `steel`; timbers `timber`; glass `glass`.
- **Gaps:** door 0.9 × 2.0 any yaw; windows 1.4 × 1.45 any yaw; the open upper floor 3–5 × 2.5 m: passes, with the slab edge and bars as the lower snag; roof bays between rafters 0.9–1.0 m: pass; the attic as a cavity 9 × 3 m under the skeleton; the stairwell 1.0 × 2.5 m between floors: passes nose-first (the flight down a stair is the sim's narrowest vertical route).
- **Wind volumes:** walls 0; the open upper floor is a cavity with strong through-flow; the roof skeleton 0.8–0.9; the rubble mound 0.
- **Snag hazards:** rebar at the slab edge and lintels, the roof skeleton's purlins, hanging cables, bent frames, furniture edges.
- **Landable surfaces:** **the intact part of the upper slab** (hard, flat, `concrete`, 1–2 m from the free edge; debris on it is uneven), and the ground-floor slab where the floor is clear. The sagging edge is not.

### Placement rules
One or two per village, on the main street or at the urban edge near U01 (I shows it in that setting), with a low masonry fence (Y04) in front and the rubble of its upper floor spreading 5–10 m.

### Generator parameters
Shared table with `storeys` 2, `wall_thickness` 0.51/0.38, `roof_pitch` 40, plus:

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `storeys` | int | 1–2 | 2 | Floors; the slab between them |
| `facade_blowout` | (side, w, h) | 0–6 m wide | from `damage` | The open upper-floor section, with its slab edge and bars |
| `slab_sag` | deg | 0–30 | from `damage` | Hang angle of the broken slab edge |
| `roof_skeleton` | 0–1 | — | from `damage` | Fraction of rafters still standing after the cover is gone |
| `annex` | size or none | 2–3 × 3–4 m | on | A one-storey lean-to at one side |
| `balcony` | bool | — | variant 2 | Balcony slab and rail (snag) |

### Open points
- The house in I is read at 25–35 m in a low-sun photo; its proportions are ±1 m. Settled at the I side-by-side.
- Landing on the upper slab: the surface flag is proposed here; the physics engineer confirms that `concrete` with debris on it needs no extra field.

## Shared: the yard

A village plot is 20–30 m wide along the street and 60–100 m deep (assumed, a typical 0.15–0.25 ha plot). From the street inward: the street fence (Y01, Y03 or Y04) with the gate (Y05 or Y06) at one side; a front garden (палісадник) with a picket fence (Y02) and 1–2 fruit trees (V10, V11); the house (H) 5–15 m back, long side along the street; the summer kitchen (O03) 3–8 m from the house door; sheds (O01, O02) along a side boundary; the well (Y07) near the house; the cellar (C01 or C02) 10–15 m behind the house; the woodpile (Y08) against a shed; the garage (O06) at the street side next to the gate; the toilet (O05) at a far corner 15–25 m from the house; the greenhouse (Y10) and the haystack (Y09) at the back; a laundry line (Y11) between the house and a tree; leaf litter (S09) under the trees, weeds (V17) along every wall. The E house with the C yard set is the first yard (terrain.md B1); abandoned plots become D (weeds, dead trees, a stone pile).

## O01 Plank shed

**Seen in:** E (the grey plank lean-to at the house's left corner, stills 1–12), J (a lean-to between the two houses, its sheet roof down). terrain.md B3.
**Research sources:** none needed beyond B3 and B6.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Footprint | 3–6 × 2–4 m | 4 × 2.5 m | measured E, J (B3) |
| Height | 2–2.5 m at the high side, 1.7–2.0 m at the low | 2.2 / 1.8 m | measured E 7 (against the 2.4 m wall) |
| Boards | 0.10–0.15 m wide, 20–25 mm thick, gaps 5–30 mm | 0.12 m | measured E (B6) |
| Posts | 0.10–0.15 m at 1.5–2 m | 0.12 m | assumed |
| Door | 0.8 × 1.8 m, plank | — | AST-04 |
| Window | 0.4 × 0.5 m or none | none | assumed |
| Roof | lean-to 10–15°, boards and felt, or slate or sheet | boards and felt | measured E (a sloping plank roof), J (sheet) |

### Construction and materials
- Posts set in the ground, horizontal boards (variant 1, E) or vertical boards on two rails (variant 2), a lean-to roof of boards with roofing felt or one row of slate or corrugated sheet, an earth floor, a plank door with strap hinges.
- **Materials:** Poly Haven `weathered_brown_planks`, `old_planks_02`, `dark_planks` ( https://polyhaven.com/a/weathered_brown_planks , https://polyhaven.com/a/old_planks_02 ); felt `bitumen` ( https://polyhaven.com/a/bitumen ); sheet `worn_corrugated_iron`; slate as H02.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Weathered grey boards | #645d59 (J gable boards, overcast) | #5e5a55 (E 7), B3 #615e56 to #73623d | measured J; measured E |
| Felt | near-black grey #3a3a38 | — | assumed |

### Variants and states
- Variants: 1 = horizontal boards, felt roof (E); 2 = vertical boards, slate roof.
- **I:** boards silver-grey, 1–3 missing, felt torn at the edge, the door ajar.
- **D:** the roof half down (boards hanging from the high side), one wall leaning 10–20° with boards on one nail, a sheet folded over the edge, boards and felt on the ground.

### Damage pattern
Light timber goes with the first blast: boards strip from the posts, the roof slides. Debris: grey boards with nails, felt strips, slate or sheet pieces.

### Physics role
- **Collision:** each board a thin box, posts capsules, roof boards thin boxes, the door a box; D boards at their hung angles.
- **Material ids:** `timber`; sheet `sheet_metal`; slate `proposed:asbestos_cement`.
- **Gaps:** door 0.8 × 1.8 m: any yaw. Missing-board slots 0.12–0.4 m: **no**. A D wall opening of 4+ boards (≥ 0.6 m): passes nose-first only; a fallen wall panel (1.5–2 m): any yaw. Interior 3.8 × 2.3 × 1.8–2.2 m: hover room, the low side is tight.
- **Wind volumes:** walls porosity 0.05–0.2 (board gaps), 0.3–0.5 with boards missing, 1 where down; the roof 0.
- **Snag hazards:** nails, board ends, the roof edge, felt flaps, the door edge.
- **Landable surfaces:** the roof when sound and ≤ 15° (4 × 2.5 m, timber, mu 0.45 holds a 15° slope); not when boards hang.

### Placement rules
Two or three per yard (B3): against the house's end wall (E), along a side boundary, or between two houses (J). Often with the woodpile (Y08) under its eaves.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `length`, `width`, `height_high`, `height_low` | m | 3–6, 2–4, 2–2.5, 1.7–2 | 4, 2.5, 2.2, 1.8 | Size and roof slope |
| `board_dir` | enum | horizontal, vertical | horizontal | Board layout and the gap pattern |
| `board_width`, `board_gap` | m | 0.10–0.15, 0.005–0.03 | 0.12, 0.015 | Porosity and the slot sizes |
| `boards_missing` | 0–1 | 0–0.5 | 0.05 | Fraction of boards gone, clustered |
| `roof_cover` | enum | felt, slate, sheet | felt | Roof material and its snag edge |
| `door` | side or none | — | front | Door position |
| `damage` | 0–1 | — | 0 / 0.6 | I below 0.3, D above: lean, hanging boards, roof down |
| `lean` | deg | 0–20 | from `damage` | Wall lean in D |
| `seed` | int | — | — | Random choices |

### Open points
- none.

## O02 Brick or block shed with a small window

**Seen in:** C (the grey low structure at the far left of stills 1–4, with a dark door opening and a flat roof about 2 m up). terrain.md B3.
**Research sources:** brick sizes (shared section); cinder block 390 × 190 × 188 mm (assumed standard).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Footprint | 3–6 × 2–4 m | 4 × 3 m | measured C (B3) |
| Height | 2–2.5 m | 2.2 m | measured C 1 (against the 1.5 m fruit-tree fork) |
| Wall | brick 0.25 m or block 0.20 m, cement render | block 0.20 m | assumed |
| Roof | slab 0.1 m or boards on joists, felt, 3–8° | boards and felt, 5° | assumed; C reads a flat top |
| Door | 0.8 × 1.8 m, plank | — | AST-04 |
| Window | 0.4 × 0.5 m, 1 | 0.4 × 0.5 m | asset-list (small window); assumed size |

### Construction and materials
- Block or brick walls with grey cement render, a flat roof of boards and roofing felt (or a thin slab), a plank door, one small window with a four-pane frame.
- **Materials:** render Poly Haven `grey_plaster_02` or `concrete_wall_003` ( https://polyhaven.com/a/grey_plaster_02 ); block `rough_block_wall` ( https://polyhaven.com/a/rough_block_wall ); brick `red_brick`; felt `bitumen`; door as O01.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Cement render | about #9a948a | #8c6e49 (lit, warmed by the feed) | assumed; measured C 1 |
| Felt | #3a3a38 | — | assumed |

### Variants and states
- Variants: 1 = block, rendered, felt roof; 2 = bare red brick, slab roof.
- **I:** render cracked and patchy, moss on the north face, the door ajar.
- **D:** the roof dropped at one corner (boards hanging, felt torn), one corner out with block halves on the ground, the door gone, the window frame out.

### Damage pattern
Blocks and bricks come out in stepped jags as H03; the light roof lifts first. Rubble: grey block halves and brick, render plates, boards, felt.

### Physics role
- **Collision:** walls as boxes per segment, the roof a box, the door a box, rubble boxes.
- **Material ids:** walls `masonry`; a slab roof `concrete`; board roof, door, frame `timber`.
- **Gaps:** door 0.8 × 1.8: any yaw. Window 0.4 × 0.5: **no**. A D corner breach 0.5–0.9 m: passes nose-first when ≥ 0.6 m clear. Interior 3.6 × 2.6 × 2.0 m: hover room.
- **Wind volumes:** solid (0); the interior a cavity through the door.
- **Snag hazards:** the felt edge, the door frame, the window frame, nails in a dropped roof.
- **Landable surfaces:** **the flat roof** (4 × 3 m, ≤ 8°, felt on boards or a slab, 2.2 m up): the yard's best rooftop target when sound.

### Placement rules
One per yard, along a side boundary or facing the cellar (C), 5–15 m from the house.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `length`, `width`, `height` | m | 3–6, 2–4, 2–2.5 | 4, 3, 2.2 | Size |
| `wall_material` | enum | block, brick | block | Texture, jag style, rubble |
| `render_loss` | 0–1 | — | 0.2 | Render patches gone |
| `roof` | enum | boards_felt, slab | boards_felt | Roof build and whether it is `concrete` |
| `window` | pos or none | — | side | The small window |
| `damage` | 0–1 | — | 0 / 0.6 | I / D as above |
| `seed` | int | — | — | Random choices |

### Open points
- none.

## O03 Summer kitchen (літня кухня), small brick

**Seen in:** R.
**Research sources:** shared section; the class is a small brick house with a stove, so H01/H03's construction applies.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Footprint | 4–5 × 3–4 m | 4.5 × 3.5 m | asset-list (assumed) |
| Wall height | 2.3–2.6 m | 2.4 m | assumed |
| Wall thickness | 0.25–0.38 m | 0.25 m | derived (1 or 1.5 bricks) |
| Roof | gable or hip, slate, 25°, ridge 3.4–3.8 m | gable, ridge 3.6 m | assumed |
| Chimney | 0.38 × 0.38 m, 0.5 m above the ridge | — | assumed (a stove inside) |
| Door, windows | 0.8 × 1.9 m; 1–2 of 0.8–1.0 × 1.0–1.2 m | 2 windows 0.9 × 1.1 m | AST-04; assumed |
| Porch | 1.5 × 2 m lean-to on two posts | on | assumed |

### Construction and materials
- Brick walls, whitewashed or bare, a slate gable roof, a brick chimney, a plank door, two windows, a small porch roof. Inside: a stove, a table, shelves.
- **Materials:** as H03 (`white_bricks` or `red_brick`, slate as H02, frames `blue_painted_planks`), whitewash `white_stucco_02`.

### Colour
As H03 or H01 by wall material; no reference colour of its own.

### Variants and states
- Variants: 1 = whitewashed brick; 2 = bare silicate with red bands.
- **I / D / X:** as H03 at a smaller scale: D strips the slate, breaks the sashes and jags one opening; X drops the gable and one wall.

### Damage pattern
As H03.

### Physics role
- **Collision, ids:** as H03; porch posts `timber` capsules.
- **Gaps:** door 0.8 × 1.9: any yaw; windows 0.9 × 1.1 frame gone: any yaw (0.9 ≥ 0.706); frame on (0.76 × 0.96): any yaw with 27 mm a side on the diagonal; interior 4 × 3 × 2.3 m.
- **Wind volumes:** walls 0; roof 0 / 0.7–0.85 stripped; the porch 0.
- **Snag hazards:** as H03; the chimney cap; the porch edge.
- **Landable surfaces:** none.

### Placement rules
3–8 m from the house door, facing the yard; one per lived-in yard.

### Generator parameters
Shared house table with `length` 4.5, `width` 3.5, `wall_thickness` 0.25, `roof_cover` slate, `windows` 0.9 × 1.1, `porch` on. No additions.

### Open points
- none.

## O04 Barn or hayloft, timber

**Seen in:** R.
**Research sources:** none found for sizes; assumed from the asset list and standard timber practice.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Footprint | 6–10 × 4–6 m | 8 × 5 m | asset-list (assumed) |
| Eaves height | 2.5–3.0 m | 2.7 m | assumed |
| Ridge | 4.5–5.5 m, pitch 25–30° | 5.0 m | derived |
| Posts | 0.15–0.20 m at 2–3 m | 0.18 m at 2.5 m | assumed |
| Wall boards | vertical, 0.12–0.15 m, gaps 10–30 mm | 0.15 m, 20 mm | assumed (ventilation gaps) |
| Double doors | 2.0–2.5 × 2.2 m | 2.4 × 2.2 m | assumed |
| Loft door | 0.8 × 1.0 m in the gable at 2.5 m | — | assumed |

### Construction and materials
- A post frame on stone pads, vertical boards with gaps, a slate gable roof on purlins, double plank doors, a loft floor of boards at 2.5 m over half the length with hay on it.
- **Materials:** boards as O01; slate as H02; hay `proposed:straw` with Poly Haven `hessian_380` tinted straw as a surface ( https://polyhaven.com/a/hessian_380 ); structure `rough_wood`.

### Colour
Weathered grey boards as O01; hay yellow-grey about #a8956a (assumed).

### Variants and states
- Variants: 1 = boarded, slate; 2 = open-fronted (a hay shelter, three walls).
- **I:** boards grey and gapped, the roof sagging, doors off one hinge.
- **D:** the roof stripped over half, purlins bare, one side wall down, hay spilled.

### Damage pattern
As O01 at a larger scale; hay burns to a black patch.

### Physics role
- **Collision:** posts capsules, boards thin boxes, purlins and rafters boxes, the loft a box, hay a soft box.
- **Material ids:** `timber`; slate `proposed:asbestos_cement`; hay `proposed:straw`.
- **Gaps:** double doors 2.4 × 2.2: any yaw; one leaf 1.2 m: any yaw; the loft door 0.8 × 1.0: any yaw, height tight (0.55 m envelope); interior 7.6 × 4.6 × 2.5 m with the loft edge at 2.5 m; roof bays once stripped.
- **Wind volumes:** walls porosity 0.1–0.3 (gaps); roof 0 / 0.75 stripped; the interior a large draughty cavity.
- **Snag hazards:** board ends, the loft edge, purlin ends, hay (hooks the legs), nails, the door hinges.
- **Landable surfaces:** the loft floor (boards, uneven under hay); hay is soft and sinks.

### Placement rules
Rare: one per two or three yards, at the back of the plot, near the haystack (Y09).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `length`, `width`, `eaves`, `pitch` | m, deg | 6–10, 4–6, 2.5–3, 25–30 | 8, 5, 2.7, 27 | Size |
| `open_front` | bool | — | off | Variant 2 |
| `board_gap` | m | 0.01–0.03 | 0.02 | Porosity |
| `loft_fraction` | 0–1 | 0–0.7 | 0.5 | Loft floor length |
| `hay_fill` | 0–1 | — | 0.4 | Hay volume in the loft and on the floor |
| `damage` | 0–1 | — | 0 / 0.6 | I / D |
| `seed` | int | — | — | Random choices |

### Open points
- `straw` id: physics sign-off.

## O05 Outdoor toilet, wooden

**Seen in:** R.
**Research sources:** typical plank construction (the search confirmed only that boards about 20 × 150 mm are used: https://uznayvse.ru/voprosyi/kak-postroit-tualet-na-dache-73933.html ); sizes assumed from the asset list.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Box | 1.0 × 1.2 m, 2.2 m high at the front, 2.0 at the back | as given | asset-list (assumed) |
| Door | 0.6–0.7 × 1.8 m, a 0.1 m gap at the top | 0.65 × 1.8 m | assumed |
| Frame | 50 × 50 mm, boards 20 × 150 mm | — | published boards; assumed frame |
| Roof | lean-to, boards and felt or one slate sheet | felt | assumed |
| Pit | 1 × 1 m, 1.5–2 m deep under the box | — | assumed |

### Construction and materials
- A plank box on a frame over a pit, a lean-to roof, a plank door with a wooden latch, a heart or diamond cut-out in the door (assumed, traditional), a seat box inside.
- **Materials:** boards as O01; felt `bitumen`.

### Colour
As O01's grey boards; sometimes painted green or blue (`green_rough_planks`).

### Variants and states
- Variants: 1 = grey boards; 2 = painted, gable roof.
- **I:** grey, leaning 2–5°, the door hanging.
- **D:** leaning 10–20°, the door off and lying, the roof gone, two boards out.

### Damage pattern
It tips or is pushed over whole; boards and the door scatter.

### Physics role
- **Collision:** boards thin boxes, frame boxes, the door a box, the seat a box; the pit a hole 1 × 1 m where the floor is gone (D).
- **Material ids:** `timber`.
- **Gaps:** the door **0.65 × 1.8 m: ±16° yaw, 38 mm a side**: the yard's second-narrowest fly-in after the cellar; the interior 0.95 × 1.15 m holds the drone (0.574 wide) with no room to turn: a dead end to back out of. The top gap 0.1 m: no.
- **Wind volumes:** solid; the door opening a slot.
- **Snag hazards:** the door edge and latch, board ends, nails, the roof edge.
- **Landable surfaces:** the roof (1.2 × 1.1 m, 10°, felt): yes when sound.

### Placement rules
One per yard, at a far corner 15–25 m from the house, behind the sheds, often next to the compost and nettles (V18).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `width`, `depth`, `height` | m | 0.9–1.2, 1.0–1.4, 2.0–2.4 | 1.0, 1.2, 2.2 | Size |
| `door_width` | m | 0.6–0.7 | 0.65 | The fly-in clear width |
| `lean` | deg | 0–20 | from `damage` | Tilt of the whole box |
| `door_state` | enum | closed, ajar, open, gone | ajar | Door position and collision |
| `damage` | 0–1 | — | 0 / 0.6 | I / D |
| `seed` | int | — | — | Random choices |

### Open points
- none.

## O06 Garage, brick or sheet metal

**Seen in:** R.
**Research sources:** a modern 3 × 6 m metal garage is 3.0 × 6.02 × 2.32 m ( https://dancovershop.com/en-at/products/metal-garage-3x6-02x2-32-m-18-06-m-proshed-anthracite ), a size check for the class; the welded-sheet Soviet garage is assumed.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Footprint | 3.2–4 × 5.5–6.5 m | 3.5 × 6 m | asset-list; published size check |
| Height | 2.2–2.6 m | 2.4 m | published check (2.32 m) |
| Walls | brick 0.25 m, or steel sheet 1.5–2 mm on an angle frame 40 × 40 mm | sheet | assumed |
| Roof | flat or 5–10° one-way, sheet or slab with felt | sheet, 6° | assumed |
| Gate | two leaves 1.2–1.3 m each, 2.0 m high; a wicket in one leaf optional | 2.5 × 2.0 m | assumed |

### Construction and materials
- Variant 1: welded sheet-steel panels on an angle frame, a sheet roof, a double gate, painted and rusting. Variant 2: brick walls with a slab roof and felt, a steel gate, a small window 0.4 × 0.5 at the back.
- **Materials:** steel: Poly Haven `rusty_painted_metal`, `rusty_metal_sheet`, `painted_metal_shutter` ( https://polyhaven.com/a/painted_metal_shutter ); brick as H03/H04; felt `bitumen`.

### Colour
Painted steel faded green or grey-blue about #5a6a5e with rust #6b3f2a (assumed); brick as H03.

### Variants and states
- **I:** paint faded, rust at the seams and the foot, the gate shut or one leaf open.
- **D:** the roof holed (0.5–1.5 m, curled edges), one gate leaf bent out 20–40° and the other off its hinges, a wall panel torn.

### Damage pattern
Sheet steel holes and curls (sharp edges); brick jags as H03. Debris: curled sheet strips, bricks, slab chunks.

### Physics role
- **Collision:** walls and roof as thin boxes (sheet) or boxes (brick); gate leaves as boxes at their angles; holes as polygons.
- **Material ids:** sheet walls, roof, gate: `sheet_metal` (cuts fiber); frame `steel`; brick `masonry`; slab `concrete`.
- **Gaps:** one leaf open 1.2–1.3 m: any yaw; both 2.5 m; the wicket 0.8 × 1.8: any yaw; interior 3.3 × 5.8 × 2.2 m: the drone can enter and turn; a roof hole 0.5–1.5 m: passes when ≥ 0.6 m clear, edges cut.
- **Wind volumes:** solid; the interior a cavity through the gate.
- **Snag hazards:** torn sheet edges, hinge pins, the gate's top bar, the roof edge.
- **Landable surfaces:** the roof (3.5 × 6 m, ≤ 10°): sheet flexes and pops (as a car roof, terrain.md R0); a slab roof is hard and good.

### Placement rules
At the street side next to the gate (Y05), the gate facing the street; one per two or three yards; rows of them become U03.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `length`, `width`, `height` | m | 5.5–6.5, 3.2–4, 2.2–2.6 | 6, 3.5, 2.4 | Size |
| `wall_material` | enum | sheet, brick | sheet | Build, material id, failure style |
| `gate_state` | enum | closed, one_open, both_open, bent, off | one_open | Gate leaves and the clear width |
| `roof_holes` | int | 0–3 | from `damage` | Holes with curled edges |
| `damage` | 0–1 | — | 0 / 0.6 | I / D |
| `seed` | int | — | — | Random choices |

### Open points
- none.

## C01 Cellar entrance, brick head with plank door, steps down (hole)

**Seen in:** C (stills 1–8 the head from the yard, 9–10 the doorway, 11–12 inside the passage). terrain.md W4, PR-9, knowledge.md §14.3.
**Research sources:** ventilation pipes 100–150 mm with the inlet 50–60 cm above the floor, drainage below the floor ( https://media.halvacard.ru/dacha/kak-postroit-pogreb ); the pit is usually under 3 m wide ( https://poradu.pp.ua/svoyimi-rukami/print:page,1,2782-yak-pobuduvati-loh-svoyimi-rukami-korisn-poradi-yak-zrobiti-zruchniy-nadyniy-pogrb.html ).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Head (погрібник) | 1.2–1.5 wide × 1.8–2.0 high × 2–3 m long | 1.4 × 1.95 × 2.5 m | measured C (W4) |
| Doorway | **0.70 × 1.6 m** | 0.70 × 1.6 m | **pilot (PR-9)**; measured C 9–10 about 0.7 × 1.6 |
| Brick piers | 0.25–0.38 m wide at each front corner | 0.25 m (one brick) | measured C 9: one brick plus render |
| Lintel | timber 0.12 × 0.15 m | — | measured C 9 (a dark beam over the frame) |
| Door frame | 0.10 × 0.08 m timber | — | measured C 9 |
| Roof | slab or boards on joists, felt, sloping back 5–10°, overhang 0.1–0.2 m with a plank edge | boards, 8° | measured C 9 (a pale plank edge under the felt) |
| Passage inside | 0.8–0.9 m wide, 1.8 m high normal to the stair | 0.85 m | measured C 11–12 (wider than the door) |
| Steps | treads 0.25–0.30, risers 0.20–0.25, 8–10 steps | 9 steps | assumed |
| Depth to the chamber floor | 2.0–2.5 m | 2.2 m | assumed (frost-proof depth) |
| Chamber | 2.5–3.5 × 2–3 m, 1.9–2.2 m high | 3 × 2.5 × 2.0 m | assumed; published pit under 3 m wide |
| Ceiling beam | round log 0.10–0.14 m across the passage | 0.12 m | measured C 12 |
| Tarp | 2 × 3 m draped on the right flank | — | measured C (W4) |
| Wire above | one wire 3–5 m up crossing the yard | — | measured C 1–8 (S1) |

### Construction and materials
- A brick head on a 0.3–0.6 m earth mound, red-brick corner piers with lime-rendered panels between them (the render peeling to show brick: C 9), a timber door frame and lintel with a plank door (absent in C; a param), a flat felt roof on boards with a plank edge, side walls brick and rendered; inside, a plank ceiling on a round log beam, lime-whitewashed walls peeling in flakes (C 12), brick or earth steps down to a brick-walled chamber with a vaulted or slab ceiling; a ventilation pipe 100–150 mm out of the mound; a tarp draped over one flank, weighted with bricks.
- **Materials:** piers `red_brick`; render Poly Haven `rough_plaster_brick_02` and `worn_cracked_plaster`; frame and ceiling boards `rough_pine_door`, `raw_plank_wall`; felt `bitumen`; slab `concrete_wall_003`; the tarp `proposed:fabric` with Poly Haven `rough_linen` tinted #8aa0c0 as a woven polyethylene surface ( https://polyhaven.com/a/rough_linen ; no CC0 tarp set was found, so this is derived); the mound: the S01 sod and S09 litter surfaces.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Pier brick | as `red_brick` | #b27142 (C 9), W4 #b97541 | measured C 9 |
| Lime render on the head | about #b5afa3 | #8f8676 | assumed; measured C 9 |
| Door frame, lintel | about #7a6a55 | #72583f, W4 #73623d | assumed; measured C 9 |
| Slab and plank edge | grey timber and felt | #6a645e | measured C 9 |
| Ceiling boards inside | pale timber | #75623c | measured C 9 |
| Inner whitewash, peeling | about #a9a08e | #6a644d | assumed; measured C 9 |
| Tarp | about #8aa0c0 | #8695b2, W4 #899cc2 | measured C 9 |
| Leaf litter around | — | #a88147 (C 1), T6 #a67f4d | measured C 1 |

### Variants and states
- One variant (asset-list). Optional parts: the door leaf (gone, open, half-open), the tarp, the pipe, the mound size.
- The head reads I (weathered, render peeling); no D or X state is asked for.

### Damage pattern
Not a damage asset. Weathering: render off the piers' edges, felt torn at the overhang, the plank edge rotten, the inner whitewash flaking in 0.1–0.5 m plates.

### Physics role
- **Collision:** piers and side walls as boxes (`masonry`), the lintel and frame as boxes, the roof a box with its plank edge, the ceiling boards and log as boxes and a capsule, each step a box, the chamber walls boxes, the tarp a thin soft surface, the mound a sloped box.
- **Material ids:** brick, render, steps, chamber `masonry`; frame, lintel, boards, log `timber`; roof `concrete` (slab) or `timber` (boards); felt as the roof's id; the tarp `proposed:fabric`; the pipe `steel`; the mound `proposed:soil`.
- **Gaps:**
  - **the doorway 0.70 × 1.6 m: passes nose-first with 63 mm a side, up to ±35° of yaw, the narrowest fly-in on the map (PR-9).** With a door leaf half open it drops to 0.35–0.5 m: no. Height 1.6 m is comfortable.
  - the passage 0.85 × 1.8 m, descending 2.2 m over about 2.5 m (a 40° slope): passes at any yaw within the walls, but the drone must pitch down the stair; the log beam at the ceiling narrows the height to 1.65 m for 0.12 m
  - the chamber 3 × 2.5 × 2.0 m: hover room, dark
  - the pipe 100–150 mm: no
- **Wind volumes:** the head solid (0); the doorway is a funnel part (porosity 1, W4 `turbulence`): gusts accelerate through it and the passage; the tarp flaps (visual) and is porosity 0 while it lies on the wall.
- **Snag hazards:** the tarp (soft, wraps), the wire above the yard, the plank edge of the roof, the frame and lintel edges, nails, the log beam, the pipe.
- **Landable surfaces:** the roof (1.4 × 2.5 m at 5–10°, felt, 2 m up): yes; the mound top behind: `soil`, soft and uneven.

### Placement rules
One per yard, 10–15 m behind the house, the doorway facing the house or the path, a fruit tree (V10/V11) beside it (C), a shed (O02) across the path, leaf litter (S09) around; the tarp on the sunny flank.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `door_width`, `door_height` | m | 0.65–0.80, 1.5–1.8 | **0.70, 1.6** | The fly-in; 0.70 is the pilot's number |
| `head_width`, `head_height`, `head_length` | m | 1.2–1.5, 1.8–2.0, 2–3 | 1.4, 1.95, 2.5 | Head size; the piers take 0.25–0.38 each side of the door |
| `pier_width` | m | 0.25–0.38 | 0.25 | Brick pier width |
| `render_loss` | 0–1 | — | 0.4 | Render gone on the head |
| `roof` | enum | boards_felt, slab_felt | boards_felt | Roof build and material id |
| `roof_slope` | deg | 3–10 | 8 | Back slope |
| `passage_width` | m | 0.8–0.9 | 0.85 | Inside clear width |
| `steps`, `depth` | int, m | 7–10, 2.0–2.5 | 9, 2.2 | Stair and the chamber floor level |
| `chamber` | m | 2.5–3.5 × 2–3 × 1.9–2.2 | 3 × 2.5 × 2.0 | The underground room |
| `door_leaf` | enum | gone, open_out, half, closed | gone | Leaf presence and the resulting clear width |
| `tarp` | side or none | — | right | The draped tarp |
| `mound_height` | m | 0.3–0.6 | 0.4 | The earth mound the head sits on |
| `pipe` | bool | — | on | Ventilation pipe |
| `seed` | int | — | — | Random choices |

### Open points
- `fabric` and `soil` ids: physics sign-off. The tarp's flutter is visual; its snag is what matters.
- The first attempt built the door at 0.78 m "to fit" and was corrected to 0.70 m (knowledge.md §14.3): the default is locked, not a question.

## C02 Earth-mound cellar with a concrete or brick portal

**Seen in:** R.
**Research sources:** as C01 (pipes, depth); the mound form is traditional (assumed).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Mound | 5–7 m across, 1.0–1.5 m high, grassed, flat top 1–2 m | 6 × 1.2 m | assumed |
| Portal | 1.0–1.2 wide × 1.9–2.1 high × 0.6–1.0 m deep, concrete or brick | 1.1 × 2.0 × 0.8 m | assumed |
| Door | about 0.75 × 1.7 m, plank or steel | 0.75 × 1.7 m | asset-list (assumed) |
| Steps, depth | 7–9 steps, 1.8–2.2 m | 8, 2.0 m | assumed |
| Chamber | 2.5–3 × 2–2.5 × 1.9–2.1 m, vaulted brick or slab | 3 × 2.5 × 2.0 m | assumed; published pit under 3 m wide |
| Pipe | 100–150 mm, 0.5–1 m out of the mound top | 120 mm | published size; assumed height |

### Construction and materials
- A chamber cut into the ground and covered with an earth mound, a short portal of cast concrete or brick at the foot of the mound, a door, steps, a vent pipe.
- **Materials:** portal `concrete_wall_003` or `red_brick`; door `rough_pine_door` or `rusty_painted_metal`; the mound the S01 sod surface; chamber as C01.

### Colour
Concrete grey about #8e8a82 (assumed); brick as `red_brick`; door as O01 or O06.

### Variants and states
- One variant (asset-list); the door leaf open or gone as a param.

### Damage pattern
Not a damage asset; weathering only (concrete spalling at the portal's arrises, grass over the mound).

### Physics role
- **Collision:** the mound a smooth sloped shape (a few sloped boxes or the heightfield), the portal boxes, the door a box, steps boxes, the chamber boxes.
- **Material ids:** portal `concrete` or `masonry`; door `timber` or `sheet_metal`; mound `proposed:soil`; pipe `steel`.
- **Gaps:** the door **0.75 × 1.7 m: any yaw, 88 mm a side nose-first, 22 mm on the diagonal**; the portal and stair 1.1 m wide; the chamber as C01.
- **Wind volumes:** the mound solid, a smooth bump the flow rides over; the doorway a funnel.
- **Snag hazards:** the pipe, the door edge, the portal's arrises.
- **Landable surfaces:** the mound top (1–2 m flat, `soil` under sod, soft) and its slopes (20–30°: slides).

### Placement rules
Alternative to C01 on one yard in three, further back in the plot (15–25 m), near the garden.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `mound_diameter`, `mound_height` | m | 5–7, 1.0–1.5 | 6, 1.2 | Mound size |
| `portal_material` | enum | concrete, brick | concrete | Portal build |
| `door_width`, `door_height` | m | 0.7–0.8, 1.6–1.8 | 0.75, 1.7 | The fly-in |
| `door_leaf` | enum | gone, open, closed | open | Leaf and clear width |
| `steps`, `depth`, `chamber` | as C01 | — | 8, 2.0, 3 × 2.5 × 2.0 | Underground part |
| `seed` | int | — | — | Random choices |

### Open points
- `soil` id: physics sign-off.

## Y01 Plank fence (grey weathered)

**Seen in:** E (horizontal grey boards at the left, beyond the shed, stills 1–12), J (fence remains and posts), H (thin posts far off). terrain.md B6.
**Research sources:** none needed.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Height | 1–2 m | 1.6 m (street), 1.2 m (between yards) | measured E, B6 |
| Boards | 0.10–0.15 m wide, 20–25 mm thick | 0.12 m | measured E, B6 |
| Gaps between boards | 5–30 mm (horizontal), 10–50 mm (vertical) | 15 mm | measured E (tight horizontal boards) |
| Posts | 0.10–0.20 m at 2–3 m | 0.14 m at 2.5 m | B6; assumed spacing |
| Rails (vertical variant) | 2 of 40 × 80 mm | — | assumed |

### Construction and materials
- Variant 1 (E): horizontal boards nailed to posts, tight, grey. Variant 2: vertical boards on two rails, gapped. Both with a run of missing or fallen boards and a leaning section.
- **Materials:** boards as O01 (`weathered_brown_planks`, `old_planks_02`, `dark_planks`); posts `rough_wood`.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Grey boards | #645d59 (J, overcast) to about #8b8780 (silvered, assumed) | #5e5a55 (E 7), B6 #615e56 | measured J; measured E |

### Variants and states
- Variants: 2 heights × 2 board directions; `gaps` as a param (asset-list: "2 heights, gaps").
- **I:** silvered boards, 2–5 % missing, one post leaning 5–10°.
- **D:** a 2–4 m run flattened (boards on the ground, posts snapped), boards hanging on one nail.

### Damage pattern
A blast lays a run flat away from the burst; boards stay nailed to the rails and lie as a panel.

### Physics role
- **Collision:** each board a thin box, posts capsules, rails boxes; a fallen panel a thin box on the ground.
- **Material ids:** `timber`.
- **Gaps:** slots from 1–3 missing boards (0.12–0.4 m): **no**; a 5-board slot (0.6 m): nose-first only; a fallen panel (2–3 m): any yaw. Over the top: the top edge at 1.2–1.6 m is in the low-flight band.
- **Wind volumes:** porosity 0.1–0.2 tight, 0.3–0.5 with boards missing, 1 where down; a 1.6 m fence gives a sheltered, gusty zone 5–10 heights downwind (as V1's windbreak at a smaller scale).
- **Snag hazards:** post tops, board tops, nails, a hanging board.
- **Landable surfaces:** none.

### Placement rules
Along every yard boundary (B6): the street side at 1.6–2 m, between yards at 1.2 m; the gate (Y06) in it; weeds (V17) along its foot.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `length` | m | 2–40 | 20 | Run length, built from post bays |
| `height` | m | 1–2 | 1.6 | Height |
| `board_dir` | enum | horizontal, vertical | horizontal | Board layout |
| `board_width`, `board_gap` | m | 0.10–0.15, 0.005–0.05 | 0.12, 0.015 | Porosity and slots |
| `boards_missing` | 0–1 | 0–0.3 | 0.03 | Missing boards, clustered |
| `post_lean` | deg | 0–15 | 5 | Max lean of posts |
| `fallen_runs` | list | 0–3 of (start, length) | from `damage` | Flattened sections |
| `damage` | 0–1 | — | 0 / 0.6 | I / D |
| `seed` | int | — | — | Random choices |

### Open points
- none.

## Y02 Picket fence, painted

**Seen in:** R.
**Research sources:** the stave gap is set at assembly, not by the product ( https://build.rozetka.com.ua/ua/498221154/p498221154/ ); sizes assumed from common practice.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Height | 1.0–1.3 m | 1.2 m | asset-list |
| Staves | 20 × 80–100 mm, pointed or rounded tops | 20 × 90 mm | assumed |
| Stave gap | 40–60 mm | 50 mm | assumed |
| Rails | 2 of 40 × 80 mm | — | assumed |
| Posts | 0.10 m at 2.5 m | — | assumed |

### Construction and materials
- Staves nailed to two rails between posts, painted white, blue or green, peeling.
- **Materials:** Poly Haven `distressed_painted_planks`, `blue_painted_planks`, `green_rough_planks`.

### Colour
Peeling paint over grey wood: white #d3cfc4, blue #8fb2c6, green #5f7a5a (assumed).

### Variants and states
- One variant with a colour param; **I** (peeling, 1–2 staves missing) and a `damage` dial for a flattened run.

### Damage pattern
As Y01, lighter: staves snap at the rail.

### Physics role
- **Collision:** staves thin boxes, rails and posts boxes.
- **Material ids:** `timber`.
- **Gaps:** stave gaps 50 mm: **no**; a missing-stave slot (0.19 m): no; a flattened run: any yaw. The top at 1.2 m is in the low-flight band.
- **Wind volumes:** porosity 0.35–0.45 (derived: 50 mm gaps at 140 mm pitch).
- **Snag hazards:** pointed stave tops catch the fiber in low flight; nails.
- **Landable surfaces:** none.

### Placement rules
Around the front garden between the house and the street fence, 5–8 m long runs; around a vegetable patch.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `length`, `height` | m | 2–20, 1.0–1.3 | 8, 1.2 | Size |
| `stave_width`, `stave_gap` | m | 0.08–0.10, 0.04–0.06 | 0.09, 0.05 | Porosity |
| `stave_top` | enum | pointed, rounded, flat | pointed | Snag shape |
| `paint` | colour | white, blue, green | white | Paint |
| `paint_loss` | 0–1 | — | 0.4 | Peeling |
| `staves_missing` | 0–1 | 0–0.2 | 0.05 | Missing staves |
| `damage` | 0–1 | — | 0 / 0.6 | I / flattened runs |
| `seed` | int | — | — | Random choices |

### Open points
- none.

## Y03 Chain-link mesh on metal posts

**Seen in:** R.
**Research sources:** 50 × 50 mm mesh, wire 1.7–2.5 mm, rolls 1.5 m high and 10 m long ( https://epicentrk.ua/shop/setka-rabitsa/ ).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Height | 1.5 m (roll height) | 1.5 m | published |
| Mesh | 50 × 50 mm, wire 1.7–2.5 mm | 50 mm, 2.0 mm | published |
| Posts | steel pipe 50–60 mm or concrete 0.10 × 0.10 m, at 2.5–3 m | pipe 57 mm at 3 m | assumed |
| Top wire | 3–4 mm tension wire | 3 mm | assumed |
| Sag | mesh sags 0.1–0.3 m between posts when loose | 0.15 m | assumed |

### Construction and materials
- Galvanised or rusted mesh stretched between posts, tied with wire, a tension wire along the top, sagging and torn in places, weeds grown through it.
- **Materials:** mesh as geometry (wire diamonds), Poly Haven `rusty_metal` for the wire surface ( https://polyhaven.com/a/rusty_metal ); posts `rusty_metal_02`.

### Colour
Rusted wire #6e5a48, galvanised #9a9a95 (assumed).

### Variants and states
- One variant; `rust` and `tears` as params.

### Damage pattern
Mesh tears and curls from a post; posts bend at the ground.

### Physics role
- **Collision:** the mesh as a thin compliant panel per bay (a thin box 1.5 m high), the top wire a capsule 3 mm, posts capsules; tears as polygons.
- **Material ids:** mesh and top wire `cable` (the physics engineer may want a mesh-specific response: see Open points); posts `steel`.
- **Gaps:** none when intact (50 mm); a tear ≥ 0.6 m: nose-first; a bay with the mesh down (3 m): any yaw. Over the top: the top wire at 1.5 m is a thin-wire snag at low height (PR-6's thin class).
- **Wind volumes:** porosity about 0.92 (derived: 2 mm wire in both directions at 50 mm gives 8 % solidity).
- **Snag hazards:** the whole mesh for the fiber and the legs (it wraps and holds), torn ends, post tops.
- **Landable surfaces:** none.

### Placement rules
Between yards and along gardens, 10–40 m runs; around the farm (P02).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `length`, `height` | m | 3–40, 1.2–2.0 | 20, 1.5 | Size |
| `mesh_size`, `wire_d` | mm | 40–60, 1.6–2.5 | 50, 2.0 | Geometry and porosity |
| `post_type` | enum | pipe, concrete | pipe | Posts |
| `sag` | m | 0–0.4 | 0.15 | Mesh sag between posts |
| `tears` | list | 0–3 of (pos, w, h) | from `damage` | Torn openings |
| `rust` | 0–1 | — | 0.6 | Surface |
| `damage` | 0–1 | — | 0 / 0.6 | Tears, bent posts, a bay down |
| `seed` | int | — | — | Random choices |

### Open points
- Mesh contact: a drone hitting chain-link is caught, not bounced (compliant, wrapping). Whether `cable` plus a snag flag covers it or a `mesh` material is needed: physics sign-off.

## Y04 Brick or concrete-panel fence

**Seen in:** I (a low brick fence with piers and a precast lattice top, soot-darkened, in the near foreground); D (a pile of pale stone blocks, stills 13–14, read as a dry-stone wall base or a rubble-stone foundation). terrain.md B6.
**Research sources:** brick sizes (shared); precast panel fences are common practice (assumed sizes).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Variant 1 (I): height | 1.2–1.5 m | 1.3 m | measured I (against the rubble and the house's 2.8 m storey) |
| Piers | 0.38 × 0.38 m at 2.5–3 m, a cap 0.45 × 0.45 × 0.06 m | 0.38 m at 2.8 m | measured I (about 1.5 bricks) |
| Brick base panel | 0.5–0.7 m high, 0.12–0.25 m thick | 0.6 m, 0.12 m | measured I |
| Lattice panel on top | 0.4–0.6 m high, balusters 0.05–0.08 m with 0.05 m gaps | 0.5 m | measured I |
| Variant 2: precast panels | 2.0 × 0.5 × 0.04–0.05 m, 3–4 high, posts 0.12 × 0.12 m | 2.0 × 0.5, 3 high (1.5 m) | assumed (common precast sizes) |
| Variant 3: brick wall | 1.5–2 m, 0.25 m thick, piers 0.38 m | 1.8 m | asset-list |
| Variant 4 (D): dry stone | blocks 0.3–0.5 m, a wall 0.6–1.0 m high or a pile 1 m high × 2–3 m | pile | measured D 14 |

### Construction and materials
- Variant 1: brick piers with caps, a low brick panel between them, a precast concrete lattice (balusters) on top, a concrete coping. Variant 2: precast concrete panels slotted between H-posts. Variant 3: a full brick wall. Variant 4: weathered pale stone blocks (shell-rock or limestone), rounded, mossy on the shade side, laid dry or heaped where a wall or foundation has gone.
- **Materials:** brick `red_brick` or `white_bricks`; precast `concrete_wall_003`, `concrete_wall_007`; stone Poly Haven `large_sandstone_blocks_01` or `rough_block_wall` tinted grey-beige ( https://polyhaven.com/a/large_sandstone_blocks_01 ).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Piers and base (I, in blue shade, sooted) | #1c3849 pillar, #283b4b base: shadow reads; author at about #7a766f with soot | — | measured I |
| Stone blocks (D, backlit) | about #a09486 | #8d766a | assumed; measured D 14 |

### Variants and states
- Four variants above; `damage` 0–1: panels cracked and missing (I shows several lattice panels broken and the piers chipped), a pier down, soot and dust over everything near a burnt block.

### Damage pattern
Lattice panels break first (thin concrete), then the brick panel in stepped jags, then piers topple whole. Debris: lattice pieces with their bar stubs, bricks, coping slabs.

### Physics role
- **Collision:** piers boxes, panels boxes, the lattice a box with the baluster gaps as visual only (50 mm: no gap), stones as boxes or spheres.
- **Material ids:** brick `masonry`; precast and lattice `concrete` (bar stubs `steel` snags); stone `masonry`.
- **Gaps:** none when intact; a missing lattice panel (2.4 × 0.5 m): the slot is 0.5 m high, **no** (0.55 m envelope); a missing precast panel row: no; a pier and panel down (2.8 m): any yaw.
- **Wind volumes:** solid below the lattice (0); the lattice 0.4; precast and brick walls 0; the stone pile 0.1.
- **Snag hazards:** lattice balusters and their broken stubs, pier caps, the coping edge, stone edges.
- **Landable surfaces:** the pier cap (0.45 m square) and the coping (0.12–0.25 m): the legs sit 0.24 m apart, so a cap holds the drone only if centred within ±10 cm: flagged `land` as an expert target; the coping no. The stone pile: uneven `rubble`.

### Placement rules
Variant 1 in front of H06 and street-side plots at the urban edge (I); variant 2 along newer plots; variant 3 around a shop or the farm; variant 4 on abandoned plots (D) and as old boundary walls in the south of the map.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `variant` | enum | brick_lattice, precast, brick_wall, stone | brick_lattice | Build |
| `length`, `height` | m | 3–40, 0.6–2.0 | 15, 1.3 | Size |
| `pier_pitch` | m | 2.5–3 | 2.8 | Pier spacing |
| `lattice_height` | m | 0.4–0.6 | 0.5 | Top panel |
| `panels_missing` | 0–1 | 0–0.5 | from `damage` | Missing or broken top panels |
| `piers_down` | int | 0–3 | from `damage` | Toppled piers with their rubble |
| `soot` | 0–1 | — | 0 | Darkening near a burnt building (mask G) |
| `stone_block` | m | 0.3–0.5 | 0.4 | Block size (variant 4) |
| `damage` | 0–1 | — | 0 / 0.5 | As above |
| `seed` | int | — | — | Random choices |

### Open points
- Landing on a pier cap: the landable flag is proposed; QA's probe decides whether it is worth keeping.

## Y05 Sheet-metal gate with wicket

**Seen in:** R.
**Research sources:** profiled sheet C8 has a working width of 1150 mm and an 8 mm wave, used on fences to 2 m ( https://toolfox.ru/tools/fence-calculator ); the welded-sheet Soviet gate is assumed.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Gate | 3 m wide, 1.8–2.0 m high, two leaves of 1.5 m | 3 × 1.9 m | asset-list; assumed height |
| Wicket | 0.85–0.95 × 1.8 m beside the gate | 0.9 × 1.8 m | assumed |
| Frame | 40 × 40 angle or 40 × 60 tube; sheet 1.5–2 mm steel (variant 1) or 0.45 mm profiled sheet C8 (variant 2) | variant 1 | assumed; published sheet width |
| Posts | 80 × 80–100 × 100 mm steel, or 0.38 m brick piers 2.2 m high | 100 × 100 steel | assumed |
| Top bar | the frame's top at 1.9 m, hinge pins 20 mm | — | assumed |

### Construction and materials
- Two steel leaves on heavy hinges between posts, a wicket beside them, painted green, blue or brown, rust at the foot and the welds; variant 2 in profiled sheet on a tube frame.
- **Materials:** Poly Haven `rusty_painted_metal`, `painted_metal_shutter`, `rusted_shutter` ( https://polyhaven.com/a/rusted_shutter ); profiled sheet `corrugated_iron_03`.

### Colour
Paint green #4a6a4f or blue #4f6f8a over rust #6b3f2a (assumed).

### Variants and states
- **I:** closed or one leaf open, paint peeling, rust streaks, the wicket ajar.
- **D:** one leaf blown off and lying flat 2–4 m inside the yard, the other bent 20–40° with the sheet torn and curled, the wicket hanging on one hinge, a post leaning.

### Damage pattern
The sheet tears from its frame at the welds and curls (sharp edges), the frame bends; hinges hold or shear.

### Physics role
- **Collision:** leaves boxes at their angles, the frame boxes, torn sheet as thin boxes at their curl, posts boxes, the fallen leaf a thin box on the ground.
- **Material ids:** sheet `sheet_metal` (cuts fiber); frame, posts, hinges `steel`; brick piers `masonry`.
- **Gaps:** one leaf open 1.5 m: any yaw; both open 3 m; the wicket 0.9 × 1.8: any yaw (0.19 m spare on the diagonal); a bent leaf leaves a tapered gap recorded from the real clear polygon; the fallen leaf on the ground is a hard, slippery sheet to land on (0.5 mm or 2 mm steel: flexes).
- **Wind volumes:** solid when shut (0); open leaves 1; a torn leaf 0.3–0.6.
- **Snag hazards:** torn curled edges, hinge pins, the top bar, the latch.
- **Landable surfaces:** the fallen leaf (1.5 × 1.9 m, sheet on the ground: pops and slides); none upright.

### Placement rules
In the street fence of every brick-house yard (H03, H04), beside the garage (O06); the wicket on the house side.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `width`, `height` | m | 2.5–4, 1.8–2.2 | 3, 1.9 | Size |
| `sheet_type` | enum | welded, profiled | welded | Material thickness and tear style |
| `wicket` | side or none | — | right | The wicket |
| `leaf_states` | 2 × enum | closed, open, bent, off | one open | Each leaf's state and angle |
| `posts` | enum | steel, brick | steel | Posts |
| `paint`, `rust` | colour, 0–1 | — | green, 0.4 | Surface |
| `damage` | 0–1 | — | 0 / 0.6 | I / D |
| `seed` | int | — | — | Random choices |

### Open points
- none.

## Y06 Plank gate

**Seen in:** E (the plank structures at the house's left corner: a lean-to and fence of the same grey boards; the gate class follows B6). 
**Research sources:** none needed.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Gate | 2.5–3 m wide, 1.5–2 m high, two leaves | 2.8 × 1.7 m | asset-list; B6 heights |
| Leaves | vertical boards 0.10–0.15 m on a Z-braced frame 40 × 100 mm | 0.12 m boards | measured E (board width), assumed frame |
| Wicket | 0.8–0.9 × 1.7 m | 0.85 m | assumed |
| Posts | 0.15–0.20 m, 2.0–2.2 m | 0.18 m | assumed |
| Hinges | strap hinges 0.3–0.5 m | — | assumed |

### Construction and materials
- Two Z-braced plank leaves on strap hinges hung from heavy posts, a wicket beside, grey and sagging so one leaf drags on the ground.
- **Materials:** as Y01 (`weathered_brown_planks`, `old_planks_02`); hinges `rusty_metal`.

### Colour
As Y01 (feed #5e5a55, B6 #615e56; photo #645d59).

### Variants and states
- **I:** sagging 5–10°, boards gapped, one leaf open and dragging.
- **D:** one leaf torn off and lying, the other hanging from the top hinge at 20–40°, boards broken out of the brace.

### Damage pattern
Boards snap at the brace; the leaf stays as a frame with gaps.

### Physics role
- **Collision:** boards thin boxes, frames and posts boxes, hinges capsules; leaves at their hung angles.
- **Material ids:** `timber`; hinges `steel`.
- **Gaps:** both leaves open: 2.8 m, any yaw; one leaf open: 1.4 m, any yaw; the wicket 0.85 × 1.7: any yaw (72 mm spare on the diagonal); a leaf hung at 30–60° leaves 0.6–1.2 m clear (recorded from the real polygon); a leaf with boards broken out leaves slots 0.12–0.36 m: no.
- **Wind volumes:** a closed gate as Y01 (0.1–0.2); open 1.
- **Snag hazards:** board tops, the brace ends, strap hinges, nails, the dragging leaf's bottom edge.
- **Landable surfaces:** none.

### Placement rules
In every plank fence (Y01) at the street, beside the house end; the wicket on the path side.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `width`, `height` | m | 2.5–3, 1.5–2 | 2.8, 1.7 | Size |
| `board_width`, `board_gap` | m | 0.10–0.15, 0.01–0.04 | 0.12, 0.02 | Porosity |
| `wicket` | side or none | — | left | The wicket |
| `leaf_states` | 2 × enum | closed, open, hanging, off | one open | Each leaf's angle and collision |
| `sag` | deg | 0–10 | 5 | Leaf sag |
| `boards_missing` | 0–1 | 0–0.4 | 0.05 | Broken-out boards |
| `damage` | 0–1 | — | 0 / 0.6 | I / D |
| `seed` | int | — | — | Random choices |

### Open points
- none.

## Y07 Well (криниця) with a small roof and crank

**Seen in:** R.
**Research sources:** concrete well ring KS 10-9: 1000 mm inner, 1160 mm outer, 900 mm high ( https://productcenter.ru/products/118548/koltso-zhieliezobietonnoie-ktsd-10-9 ); the head and roof are traditional (assumed).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Head | a concrete ring 1.0 m inner, 1.16 m outer, 0.9 m high, standing 0.8–1.0 m above grade; or a timber box 1 × 1 × 0.9 m | ring | published ring; assumed height above grade |
| Windlass | a drum 0.2 m on two posts 0.1 m at 1.6–1.8 m, a crank 0.4 m, a chain or rope, a bucket 0.3 m | — | assumed |
| Roof | a small gable 1.2 × 1.4 m of boards or sheet at 2.0 m | — | asset-list (1 × 1 × 2 m); assumed |
| Lid | boards 1.1 m across, hinged | — | assumed |
| Shaft | 1.0 m diameter, 5–15 m deep, water at 3–10 m | 8 m, water at 5 m | assumed |

### Construction and materials
- A ring or box head, two posts with the windlass and crank, a gable roof, a plank lid, a chain and bucket, a concrete apron 2 × 2 m around it.
- **Materials:** ring `concrete_wall_003`; posts and roof `rough_wood`, `dark_planks`; chain `rusty_metal`; bucket `rusty_metal_sheet`.

### Colour
Concrete grey #8e8a82, boards as O01, rust (assumed).

### Variants and states
- One variant; the lid open or closed as a param; `damage` tips the roof and posts.

### Damage pattern
The roof and posts go first; the ring stays.

### Physics role
- **Collision:** the ring a hollow cylinder (a ring of boxes), posts capsules, the roof thin boxes, the lid a box, the chain a capsule chain, the bucket a cylinder; the shaft open below the ring when the lid is open.
- **Material ids:** ring and apron `concrete`; timber `timber`; chain `cable`; bucket `steel`.
- **Gaps:** the shaft, **1.0 m across: the drone fits at any yaw and falls 5–8 m to the water**: a vertical trap flagged `holes` when the lid is open; the space under the roof above the ring (1.2 × 1.0 m): passes sideways between the posts.
- **Wind volumes:** solid ring; the roof a small lid; negligible.
- **Snag hazards:** the chain and crank (a thin-line snag at 1.0–1.8 m), the roof edge, the posts.
- **Landable surfaces:** the closed lid (1.1 m, boards, 0.9 m up): yes; the ring edge (0.08 m): no.

### Placement rules
One per two or three yards, 3–8 m from the house or at the street verge as a shared well.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `head` | enum | ring, timber_box | ring | Head build |
| `head_height` | m | 0.8–1.0 | 0.9 | Above grade |
| `roof` | bool | — | on | The small roof and posts |
| `lid` | enum | closed, open, gone | closed | Whether the shaft is open (holes flag) |
| `shaft_depth`, `water_depth` | m | 5–15, 3–10 | 8, 5 | The fall |
| `damage` | 0–1 | — | 0 | Roof and posts tipped |
| `seed` | int | — | — | Random choices |

### Open points
- Whether an open shaft is a hazard the world wants (a lost drone): a design choice for the world-builder, not a pilot question; the flag is there either way.

## Y08 Woodpile

**Seen in:** R.
**Research sources:** none needed; sizes are the asset list's plus firewood practice (assumed).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Pile | 2–4 m long, 1.2–1.8 m high, 0.5–1.0 m deep | 3 × 1.5 × 0.6 m | asset-list; assumed |
| Logs | split, 0.33–0.5 m long, 0.08–0.15 m across; or 1 m rounds 0.1–0.2 m | split 0.4 m | assumed |
| End posts | 0.1 m or a crossed stack | posts | assumed |
| Cover | a sheet or tarp on top, or none | sheet | assumed |

### Construction and materials
- Split logs stacked bark-out between posts or against a shed wall, loose logs at the foot, a sheet or tarp weighted with bricks on top.
- **Materials:** Poly Haven `bark_brown_01`, `bark_willow` for the log ends and bark ( https://polyhaven.com/a/bark_brown_01 ); split faces `rough_wood`; cover `worn_corrugated_iron` or the C01 tarp.

### Colour
Bark grey-brown #6a5e52, split faces #b39a72 weathering to grey (assumed).

### Variants and states
- Variants: 1 = split logs between posts under a sheet; 2 = metre rounds against a shed, no cover.
- `damage`: the pile part collapsed, logs scattered 1–3 m.

### Damage pattern
A blast scatters the top rows; the rest slumps.

### Physics role
- **Collision:** the stacked body a box per row with a bumpy top (protruding logs as cylinders), loose logs cylinders, the cover a thin box, posts capsules.
- **Material ids:** `timber`; cover `sheet_metal` or `proposed:fabric`.
- **Gaps:** none.
- **Wind volumes:** porosity 0.05–0.15.
- **Snag hazards:** log ends, the cover's edge, bricks on top.
- **Landable surfaces:** the top: **landable, uneven** (log ends 0.05–0.1 m proud tilt the drone and roll under a leg); the sheet cover is flat but pops.

### Placement rules
Against a shed (O01) or the house's back wall, under the eaves; one or two per yard.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `length`, `height`, `depth` | m | 2–4, 1.2–1.8, 0.5–1.0 | 3, 1.5, 0.6 | Size |
| `log_type` | enum | split, rounds | split | Log shape and end look |
| `log_length`, `log_diameter` | m | 0.33–1.0, 0.08–0.2 | 0.4, 0.12 | Log size |
| `top_roughness` | m | 0–0.15 | 0.07 | How far logs stand proud of the top |
| `cover` | enum | none, sheet, tarp | sheet | Cover and its material id |
| `loose_logs` | int | 0–20 | 5 | Logs on the ground |
| `damage` | 0–1 | — | 0 | Collapse and scatter |
| `seed` | int | — | — | Random choices |

### Open points
- none.

## Y09 Straw or hay stack

**Seen in:** R.
**Research sources:** none; traditional sizes assumed.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Stack | 3–5 m across, 2.5–4 m high, round or loaf-shaped | 4 × 3 m | asset-list; assumed |
| Crown | rounded, often under a tarp or film with tyres on it | tarp | assumed |
| Bale variant | bales 1.2 × 0.5 × 0.4 m stacked 3–5 high | — | assumed |

### Construction and materials
- Loose hay or straw built up around a pole, settled and grey outside, yellow where torn; or a stack of small bales.
- **Materials:** surface `proposed:straw` with Poly Haven `hessian_380` or the S04 belt-straw material tinted (derived); the tarp as C01.

### Colour
Weathered outside #8f8468, fresh inside #c9b273 (assumed; S04 `feed` straw #797258 as the check).

### Variants and states
- Variants: loose stack; bale stack. `damage`: torn open, part burnt to a black patch.

### Damage pattern
Burns completely; leaves a black circle 4–6 m and charred stubs.

### Physics role
- **Collision:** a rounded body (a few boxes and a sphere) with a soft contact layer 0.1–0.2 m.
- **Material ids:** `proposed:straw` (soft: a leg sinks 5–15 cm and the straw hooks it on liftoff, as T4); the tarp `proposed:fabric`.
- **Gaps:** none.
- **Wind volumes:** solid (0): a bluff body with a wake of 2–3 heights.
- **Snag hazards:** the whole surface for the legs and the fiber; the tarp and its tyres.
- **Landable surfaces:** the crown: **landable, soft** (sinks and rocks; slides off the sides above 20°).

### Placement rules
At the back of a plot or beside the barn (O04); one per three or four yards; rows of bale stacks at the farm (P02).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `diameter`, `height` | m | 3–5, 2.5–4 | 4, 3 | Size |
| `shape` | enum | round, loaf, bales | round | Build |
| `settle` | 0–1 | — | 0.5 | How slumped and grey it is |
| `cover` | enum | none, tarp, film | tarp | Cover and tyres |
| `burn` | 0–1 | — | 0 | Burnt fraction and the black patch |
| `seed` | int | — | — | Random choices |

### Open points
- `straw` id: physics sign-off.

## Y10 Greenhouse frame (film torn)

**Seen in:** R.
**Research sources:** none found with sizes; a common hobby size is 3 × 6 m (assumed, asset-list).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Frame | 3 × 6 m, 2.0–2.2 m at the ridge | 3 × 6 × 2.1 m | asset-list; assumed |
| Arches | steel pipe 20–25 mm or wood 40 × 40, at 1.0 m pitch | pipe 25 mm, 7 arches | assumed |
| Purlins | a ridge pipe and two side purlins | — | assumed |
| Film | polyethylene 0.1–0.15 mm, torn and flapping | — | assumed |
| Door | 0.7–0.8 × 1.8 m at one end, a film-covered frame | 0.75 m | assumed |

### Construction and materials
- Pipe arches on ground stakes, purlins, film clipped on and torn into strips, a door frame at one end, raised beds inside overgrown with weeds (V17).
- **Materials:** pipe `rusty_metal_02`; film `proposed:plastic_film` as a translucent sheet with a derived dirt map; wood frame `rough_wood`.

### Colour
Film milky #d9d9d0 with green algae streaks; pipe rust (assumed).

### Variants and states
- One variant; `film_loss` from 0.3 (I) to 1.0 (bare frame).

### Damage pattern
Film shreds in the first gust after a blast; the frame bends at a strike.

### Physics role
- **Collision:** arches and purlins capsules; film as thin soft panels where it remains; the door frame boxes.
- **Material ids:** frame `steel`; film `proposed:plastic_film` (soft, snags props and wraps); wood `timber`.
- **Gaps:** the door 0.75 × 1.8: any yaw; a bay between arches with the film gone (1.0 × 2.0 m): any yaw; the tunnel interior 2.8 × 5.8 × 2.0 m: fly-through end to end.
- **Wind volumes:** film intact 0 (a bluff tunnel); torn 0.5–0.9; bare frame 0.95.
- **Snag hazards:** film strips (the worst: they wrap the props), arch ends, clips and ties, the door frame.
- **Landable surfaces:** none.

### Placement rules
At the back of a lived-in plot, long axis north–south; one per three or four yards.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `length`, `width`, `height` | m | 4–8, 2.5–3.5, 1.8–2.4 | 6, 3, 2.1 | Size |
| `arch_pitch` | m | 0.8–1.2 | 1.0 | Arch spacing and bay width |
| `frame_material` | enum | pipe, wood | pipe | Frame look and id |
| `film_loss` | 0–1 | — | 0.6 | Fraction of film gone, in strips |
| `strip_length` | m | 0.3–2 | 1.0 | Hanging strip length (snag) |
| `door` | end or none | — | south | The door frame |
| `seed` | int | — | — | Random choices |

### Open points
- `plastic_film` id: physics sign-off.

## Y11 Yard props: bench, laundry line, barrel, bucket, washbasin

**Seen in:** R.
**Research sources:** none; sizes are everyday objects (assumed).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Bench | 1.5–2 × 0.35 × 0.45 m, a plank seat on two posts, with or without a back | 1.6 m | assumed |
| Laundry line | 3–4 mm wire or cord at 1.6–1.8 m, spans 5–10 m between posts, a post and a tree, or two trees | 8 m, 1.7 m | assumed |
| Barrel | a 200 l steel drum about 0.58 m across, 0.88 m high | — | assumed (the standard drum) |
| Bucket | 0.3 m across, 0.3 m high | — | assumed |
| Washbasin | a tin basin 0.3 m on a post 1.2 m, a basin 0.4 m below | — | assumed |
| Also | a dog kennel 0.8 × 1.0 × 0.8 m; a 1 m³ cube water tank on a stand; two chairs | — | assumed |

### Construction and materials
- Everyday steel and timber objects, rusted and faded.
- **Materials:** timber as O01; steel `rusty_metal`, `rusty_painted_metal`, `rusty_metal_sheet`; the line `cable`; a plastic tank: a plain tinted material (no CC0 set needed).

### Colour
Rust and faded paint (assumed); galvanised buckets #9a9a95.

### Variants and states
- A set; each prop placed by the yard layout; `tipped` per prop.

### Damage pattern
Props tip and scatter; the line breaks and lies in the weeds (then it is L04's class).

### Physics role
- **Collision:** bench boxes; barrel a cylinder; bucket a cylinder; basin and post a capsule and a cylinder; kennel boxes; the line a capsule 3–4 mm.
- **Material ids:** `timber`, `steel`, `sheet_metal` (bucket and basin), `cable` (line).
- **Gaps:** none; the kennel door 0.35 × 0.4: no.
- **Wind volumes:** negligible; the tank a 1 m cube (0).
- **Snag hazards:** **the laundry line at 1.6–1.8 m, nearly invisible on the feed: the yard's own thin-wire trap (PR-6 thin class)**; the basin post; chair backs.
- **Landable surfaces:** the barrel top (0.58 m, `steel`, 0.88 m up): the legs sit within 0.24 m, so it holds the drone when centred within ±15 cm, an expert target; the tank top (1 m, flat): yes; the bench seat (0.35 m): no; the kennel roof (0.8 × 1.0, felt): yes.

### Placement rules
The bench by the house door or the gate; the line between the house and a fruit tree or the shed; the barrel at a downpipe; the bucket and basin by the well; the kennel by the gate.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `props` | set | any of the list | all | Which props the yard gets |
| `line_span`, `line_height`, `line_sag` | m | 5–10, 1.5–1.9, 0.1–0.4 | 8, 1.7, 0.2 | The line's catenary (feeds L03's generator) |
| `line_laundry` | 0–1 | — | 0.2 | Fraction of the line with cloth on it (visual, fabric snag) |
| `tipped` | per prop | — | 0.2 | Fraction of props knocked over |
| `rust` | 0–1 | — | 0.5 | Surface |
| `seed` | int | — | — | Random choices |

### Open points
- none.

## Open points for the whole sheet

**Proposed physics material ids** (none invented in a generator; the physics engineer adds them to `physics_materials.json` with values):
- `asbestos_cement`: шифер sheets and fragments (H02–H05, O01, O03, O04). Brittle: cracks under a landing leg, breaks on a strike into shards; friction near `masonry`; edge radius about 3 mm (a 6 mm sheet's half thickness).
- `soil`: the cellar mounds (C01, C02) and the clay bench (H05). Soft, sinks 2–5 cm; the surface table's `crater_spoil` row is the nearest existing behaviour.
- `straw`: thatch (H05), hay (O04), the stack (Y09). Very soft, hooks the legs on liftoff (T4 behaviour as a material).
- `fabric`: tarps (C01, Y08, Y09) and laundry (Y11). Soft, wraps, no bounce.
- `plastic_film`: greenhouse film (Y10). Soft, wraps the props.
- Tile and slate breakability (a tile cracks under a leg, a shard slides) is a later material property, as physics review P3-6 says for glass; until then tiles are `masonry`.
- Chain-link (Y03): whether `cable` plus a snag flag reproduces being caught, or a `mesh` material is needed.

**Settled by side-by-side, not by the pilot:** the E roof end shape (H01), the J window size (H03), the I house proportions (H06), tile size (H01). Nothing in this sheet needs a pilot question.
