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
