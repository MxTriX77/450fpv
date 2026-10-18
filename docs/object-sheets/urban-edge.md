# Object sheet: urban edge (U01–U03) and village public buildings (P01–P06)

**Owner:** reference-analyst. **Reviewed for buildability by:** asset-artist (task 1.7, `openspec/changes/p1-object-sheets/review-buildability.md`).

**OPSEC:** cite clips and photos by letter only. Describe classes, never a specific identifiable building, place, date or person. No file names, and no OSD values. Every sign, name or lettering on these assets is **invented** (SEC-03): no text is ever copied from a reference or a research image.

**Rows:** U01–U03 and U02b (urban edge, [asset-list.md](../asset-list.md) §7) and P01–P06 (village public buildings and infrastructure, §6). **Requirements:** AST-02, AST-04, AST-05, AST-06, REAL-01…05, SEC-01, SEC-02, SEC-03, PRC-06, PRC-09 · **Decisions:** D-013, D-024, D-028 · **Change:** `p1-object-sheets`, tasks 1.6 and 1.8 (U02b).

## How this sheet was made

- **Letters read:** the two urban-edge photos I and K, each as a single still, plus local zoom crops kept in the ignored `reference/_frames/<letter>/zoom/`: in I the block's long facade and its near end at about 3.5×, and the roof pile (U02b) at 2.5–5×; in K the open floors, the roof line, the stacked balconies, the ground floor with its wall stub, the rubble mound, the concrete pieces in it and the ground in front. No clip shows any row of this sheet (terrain.md §9), so there are no contact sheets and **no feed check values**: the REAL-03 comparison for this group is a noclip view against the photo.
- **Reused, not re-measured:** terrain.md §4 B4 (the block: storeys, depth, length, sections, wall thickness, windows, damage kit, wake) and B5 (the mound: 2–4 m high, spreading 15–20 m, the K rubble colour #8d8172), B6 and T8 (fence, trampled ground), §6 S1, S3–S5 (cables, masonry debris, timber and tarps, household contents); knowledge.md §9.2 and §14.4 (rubble coloured from the building's own materials, windows are empty frames or broken glass, damage masks); the houses-and-yard sheet's shared construction (brick sizes, lintels, slate, blast thresholds, the failure order) and its drone-against-an-opening arithmetic; the earthworks-and-surfaces sheet's four geometry layers and its S10 `rubble` row, which leaves the piece mix to this sheet. Where this sheet gives a number terrain.md already gives, terrain.md is the source.
- **Shared with `lines-roads-debris.md`** (task 1.4): the debris kit **K06** (concrete chunks with protruding rebar), **K08** (household items) and **K10** (fabric and cables hanging from ruins), and the cables of **L05**, are defined there; this sheet only says where the block uses them and how densely, and gives the block-specific bar sizes, which K06 now matches.
- **Scale cues in the photos:** the storey (AST-04: 3.0–3.4 m for this block); in K the slab edges (one per storey), the cast-iron radiator under a window (about 0.5–0.6 m high, assumed), the room doors in the back walls (about 0.8 × 2.0 m), the wardrobes (about 2.0 m, assumed), the balcony rails (about 1.0 m, assumed); in I the window rows (one per storey) and the entrance canopies.
- **Labels:** every number is **measured** (from a letter), **published** (with its source), **derived** (with the derivation) or **assumed**. Measured values from I and K are rough: I shows the block at 60–80 m in low sun, K is a portrait photo looking up 15–25°, so perspective shortens the upper floors.
- **Colours** are mean sRGB of hand-picked regions on the two photos (`photo` values). I is lit by a low warm sun, so its lit reads are warmer than albedo and its shade reads are blue; K is overcast and dark, so its interior reads are deep shade. Each row says what to author.

### Research used across the group (published)

| Topic | Finding used here | Source |
|---|---|---|
| Stalin-era apartment blocks | Exterior brick walls about 2.5 bricks (65 cm), "60–70 cm" overall; interior bearing walls 1–1.5 bricks; ceilings 2.9–3.2 m in ordinary blocks, more in elite ones; floors of timber (spans under 6 m), reinforced concrete or mixed; stairwell walls brick; red ceramic brick before the war, silicate brick later | https://en.wikipedia.org/wiki/Stalinka |
| Hollow-core floor slabs | Type 1ПК: 220 mm thick with round voids 159 mm across, supported on two sides | GOST 9561-91: https://meganorm.ru/mega_doc/norm/gost_gosudarstvennyj-standart/11/gost_9561-91_plity_perekrytiy_zhelezobetonnye_mnogopustotnye.html |
| Rebar classes | А-I: hot-rolled smooth round bar, 6–40 mm; every higher class (А-III and up) has a periodic (ribbed) profile | https://ru.wikipedia.org/wiki/Арматура_железобетонных_конструкций |
| How masonry blocks fail | Load-bearing masonry walls fail out of plane under explosions and can trigger progressive collapse; the loss of one exterior wall panel can bring down the floors above it in a chain (a corner collapse); masonry bearing-wall buildings range from good to poor resistance to progressive collapse | https://researchgate.net/profile/Fulvio-Parisi/publication/301673947_Out-of-plane_blast_capacity_of_load-bearing_masonry_walls/links/5720c79408aefa6488a03e5e/Out-of-plane-blast-capacity-of-load-bearing-masonry-walls.pdf · https://en.wikipedia.org/wiki/Progressive_collapse · https://nvlpubs.nist.gov/nistpubs/Legacy/IR/nbsir74-526.pdf |
| Blast thresholds | Glass 1–7 kPa; partial collapse of walls and roofs about 14 kPa (the order of failure) | the houses-and-yard sheet, after https://safteng.net/?p=2300 |
| Flames out of windows | Externally venting flames carry fire to the floors above; with wide windows the flame clings to the wall above instead of projecting | https://knowledge.lancashire.ac.uk/id/eprint/31936/1/31936%20Asimakopoulou%2BEleni%2BGeometrical%2Bcharacteristics%2Bof%2Bexternally%2Bventing%2Bflames%20%281%29.pdf · https://www.atlantis-press.com/article/11190.pdf |
| External insulation | EPS boards are bonded by a perimeter bead and central dabs (or full troweling), at least 40 % contact; then mesh and a thin render | https://www.environdec.com/library/epd27710 |
| Rubble of a collapsed building | Debris spread l = H/4 to H/3 (the obelisk's sides at the angle of repose); rubble height h = γ·H / (100 + k·H) with γ = 40 for residential buildings (rubble m³ per 100 m³ of building) and k = 0.5; height falls linearly to the edge, h(x) = h·(1 − x/l); rubble volume V = γ·A·B·H / 100 | https://portal.tpu.ru/SHARED/s/SVETANAZ/academics/Tab5/%D1%80%D0%B0%D1%81%D1%87%D0%B5%D1%82%20%D0%B7%D0%B0%D0%B2%D0%B0%D0%BB%D0%BE%D0%B2.pdf (a civil-protection practical on building rubble) |
| Cooperative garages | Boxes in a cooperative, each closed by its own gate; long brick blocks and rows of welded metal boxes both occur (assumed: common practice) | https://www.zr.ru/content/articles/932350-garazhi-v-sssr-daleko-ne-tolk/ |
| Inspection pits | 0.75–0.80 m wide, 1.7–1.8 m deep, 1.8–2.0 m long or the car's length plus about 1 m | https://news.ru/family/poleznye-sovety/smotrovaya-yama-v-garazhe-odin-raz-sdelali-godami-ekonomite-na-servise |
| Rozhnovsky water towers | A steel tank on a steel support with a lid and inspection hatch, an external ladder with a safety cage; 10–150 m³; a standard design for steel Rozhnovsky towers of 15, 25 and 50 m³ on supports 12, 15 and 18 m high; towers 9–25 m overall; a platform only from 20 m; a 15 m³ tower on a support pipe 1020 or 1220 mm across with a 10 mm base plate, 12.5–18.5 m overall | https://ru.wikipedia.org/wiki/Водонапорная_башня · https://fb.ru/article/339172/bashnya-rojnovskogo-harakteristiki-tsena · https://productcenter.ru/products/86711/vodonapornaia-bashnia-15-m3-obshchiei-vysotoi-17-mietrov |
| Soviet bus stops | Built to standard designs or the local authority's own, usually commissioned by road departments or collective farms; open-fronted concrete boxes are common | https://en.wikipedia.org/wiki/Soviet_bus_stops |
| Pole-mounted substations | A mast substation of 25–250 kVA: equipment on steel frames on reinforced-concrete poles of the 10.5 m class, high enough to need no fence, with a service platform, fuses and surge arresters | https://ena.lpnu.ua/items/8df83abf-ffbf-480b-89da-789a90f3ee8c/full · https://www.prom.uz/ads/komplektnye-transformatornye-podstantsii-ktpm-moshchnostyu-250-2500kva/ |
| Road signs | Round signs 600 / 700 mm (size I / II), triangles 700 / 900 mm, rectangles 600 / 700 mm with a 40 mm border; the settlement-end signs carry a red stripe 0.4 × the capital-letter height wide | DSTU 4100-2002: https://dnaop.com/html/43922_3.html · https://dnaop.com/html/43922_6.html |
| Vortex shedding | A circular cylinder sheds vortices at a Strouhal number of about 0.2, f = St·U/D | https://en.wikipedia.org/wiki/Strouhal_number |

### Reference views for this group (REAL-02)

Viewpoint heights and distances are estimated from the scene (storeys, slab edges, the fence, the trees), never from the display. One block serves both views: I looks at its long facade from a yard, K at its torn-off end (see U01, placement).

| Letter | Scene type | Viewpoint | Framing | Light | Time of day | Weather |
|---|---|---|---|---|---|---|
| I | The urban edge: the burnt five-storey block across the background, a collapsed timber roof pile in the middle, a damaged two-storey house at the right, a rubble field and a low masonry fence with a lattice top in front | Standing height, 1.5–1.7 m, in a yard or street; the fence 5–8 m away, the roof pile 20–30 m, the two-storey house 25–35 m, the block 60–80 m (as the houses sheet) | Landscape. The block's long facade spans the left two thirds, seen 15–30° off square and receding to the right, its roof line at about a quarter of the frame height from the top; bare trees in front of it; the roof pile centre; the house right; rubble, white sheet and timber in the lower third; the sky the upper third | Low, warm sun from the left: the facade lit, long shadows to the right, blue shade in the foreground | Morning or late afternoon | Clear, dry, cold; winter or early spring (bare trees) |
| K | The torn-off end of the block, rooms open on every floor, the rubble mound at its foot, bare trampled ground in front | Standing height, 1.5–1.7 m, 18–22 m from the end (derived: the 13–14 m end fills about two thirds of the frame width at a camera field of about 50° in portrait, the photo's lens, assumed), tilted up 15–25° | Portrait. The block's end fills the middle two thirds of the width from about 17 % to 62 % of the height; the mound across the whole width from about 55 % to 80 %; bare ground with cable loops and fragments in the bottom fifth; bare trees left and right with a few mistletoe clumps; dark overcast sky in the top fifth | Overcast, dark and diffuse, no shadows; the interiors in deep shade | Daytime | Overcast, threatening, dry ground; early spring (bare trees) |

The REAL-03 side-by-side sheets for I and K are made in `reference/_compare/` once U01 and U02 exist; nothing exists to compare yet. P01–P06 and U03 are research only (R), so their check is the noclip review sheet against this sheet, and the pilot sees them at G2.

## Rules shared by this group

### Geometry, not texture (REAL-05, PR-5, AST-05)
- **Geometry:** every wall with its real thickness and every opening through it; window reveals and frames, mullions, sills and grilles; slab edges with their real thickness; broken slab ends with exposed voids and bars; stepped brick at every breach (65 mm courses plus 10–12 mm joints, 125 or 250 mm bricks jutting 0.1–0.4 m, as the houses sheet); protruding rebar and wire as bent rods; balcony slabs and rails; hanging fabric and cables (K10 shapes); roof timbers, battens and the attic fill layer; furniture, radiators and appliances in the open rooms; every piece of the rubble mound larger than about 0.1 m; insulation boards still hanging (50–100 mm).
- **Texture, normal or decal only** (below 1–2 cm, no physical role): the adhesive dabs left on bare brick, mortar joints, render cracks, wallpaper print, soot film and plumes (mask G), rust streaks, fragment pocks under 30 mm (the vehicles sheet's rule), paint fade.
- **Facade maps** in asset space, upright and continuous across the whole facade (knowledge.md §14.4), with the damage mask: R render or insulation loss, G soot, B dirt and streaks, A painted bands and the plinth.
- **Rubble is coloured from the building's own materials** (knowledge.md §14.4): buff brick with white dabs, grey slab concrete, white plaster, white PVC, brown furniture board, grey-brown timber. Never near-white overall.

### Physics ids
The closed list in [physics_materials.json](../../assets-src/physics_materials.json): `steel`, `sheet_metal`, `timber`, `masonry`, `concrete`, `glass`, `cable`, `foliage`. Ids written `proposed:<id>` need physics sign-off (Open points at the end); four of them were already proposed by the houses and vehicles sheets.

### The drone against an opening
The houses-and-yard sheet's arithmetic, used unchanged: prop tip to prop tip **0.574 m nose-first, 0.706 m on the diagonal**; footprint at yaw θ `w(θ) = 0.254 + 0.32·(|cos θ| + |sin θ|)` m; an opening of clear width W passes nose-first when W > 0.574, at **any yaw** when W ≥ 0.706, and in between only up to `θmax = asin((W − 0.254) / 0.4525) − 45°`. Vertical envelope with legs about 0.55 m (assumed until phase 5). Every gap below is the **real clear space** after frames, jags, bars and hanging items, recorded as a polygon by the generator.

### One damage dial per part
As the houses sheet: a master `damage` (0–1) per section or part sets defaults for every damage item, each overridable; I below 0.25, D 0.25–0.8, X above 0.8. Every damage item is a physical edit (cut, move, bend, remove, add), and collision, gaps, wind volumes and snags are rebuilt from the edited geometry. The same seed and params always give the same ruin.

## U01 Stalin-era five-storey block, destroyed (exactly one)

**Seen in:** I (the long facade: burnt windows, soot plumes, a pale render with a painted lower band, entrance canopies, the parapet line), K (the torn-off end: five floors of open rooms, the spine wall, the end piers of the long walls in bare brick with adhesive dabs, the slab edges, stacked balconies on the long facade, the roof down to its timbers, the ground-floor wall stub, the mound).
**Research sources:** the research table (Stalin-era blocks, hollow-core slabs, rebar, masonry failure, venting flames, external insulation, rubble).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Storeys | 5 | 5 | measured (I, K: five floors); asset list |
| Storey height (floor to floor) | 3.0–3.4 m | **3.0 m** | AST-04 (3.0–3.4 m for the Stalin-era block). 3.0 is chosen because K's depth reads 12.5–14.5 m at 3.0 m storeys (measured K, inside B4's 12–15 m) and the eaves then sit at 16.5 m (below) |
| Plinth above grade | 0.8–1.5 m | 1.0 m | measured K (a grey masonry plinth, partly under the mound) and I (a dark band under the ground-floor windows); rough |
| Eaves height | 16.2–19.1 m | 16.5 m | derived: plinth + 5 × storey + a wall head of 0.4–0.6 m. **asset-list's 15–17 m holds only for storeys of 3.0–3.1 m**; 3.4 m storeys give 18–19 m (Open points) |
| Roof rise | 3.0–4.0 m | 3.0 m | measured (terrain.md B4, I, K: a pitched roof adds 3–4 m); asset list. Ridge at 19.5 m (derived) |
| Roof pitch | 25–31° | 25° | derived: atan(rise / half the depth) = atan(3.0 / 6.5) |
| Eaves overhang | 0.5–0.8 m | 0.6 m | measured (terrain.md B4, I, K) |
| Depth | 12–15 m | 13 m | terrain.md B4; measured K 12.5–14.5 m (the end with its spine wall, at 3.0 m storeys) |
| Length | 40–80 m in 3–5 sections | 60 m: 4 sections × 15 m | measured (terrain.md B4, I, K); I shows at least three entrance bays along the visible length (rough) |
| Section length | 13–20 m | 15 m | derived: length / sections |
| Exterior walls | 0.51–0.65 m | 0.64 m (2.5 bricks) | published (en.wikipedia: 2.5 bricks, 65 cm); terrain.md B4 |
| Spine wall (longitudinal, bearing) | 0.38–0.51 m | 0.51 m | published (1–1.5 bricks for interior walls); measured K: about 0.5 m in section |
| Cross walls (section ends, stair cells) | 0.38 m | 0.38 m | published (stairwell walls brick); assumed 1.5 bricks |
| Partitions | 0.08–0.12 m | 0.10 m | assumed (plastered panels or half-brick) |
| Floor slab | 0.22 m hollow-core, 1.2 m wide, plus 0.05–0.08 m screed and floor | 0.28 m build-up | published (GOST 9561: 220 mm, Ø 159 mm voids); measured K: slab edges 0.2–0.3 m; slab width 1.2 m assumed |
| Clear room height | 2.72–3.12 m | 2.72 m | derived: storey − build-up |
| Slab span | 5.5–6.0 m | facade to spine, 5.6 m | derived: (13 − 2 × 0.64 − 0.51) / 2 = 5.6 m |
| Open bay at the torn end, per floor | 5.0–6.0 × 2.7 m | 5.6 × 2.72 m | derived (span × clear height); measured K: two bays per floor, 5–6 m each |
| Room length along the block | 3–4 m | 3.5 m | measured K: the back wall with its door 3–5 m behind the slab edge; asset list (open rooms 3–4 m) |
| Windows | 1.3 × 1.8 m (w × h) | 1.3 × 1.8 m, sill 0.85 m | asset-list (as given). **I reads wider than tall:** three-leaf windows about 1.8 × 1.35 m and two-leaf about 1.3 × 1.35 m (measured I, rough); kept as a variant (Open points) |
| Window pitch along the facade | 3.0–3.6 m | 3.3 m | derived: one window per room bay |
| Window frame | 60–80 mm box, mullion 70–80 mm | timber double frame or white PVC | assumed; K's rubble is full of white PVC profiles (measured K) |
| Stair cell (inside) | 2.4–2.8 × 5.0–6.0 m | 2.6 × 5.5 m | derived: two flights 1.2 m + a 0.2 m well; 2 flights per storey of 10 risers × 150 mm, going 9 × 0.30 = 2.7 m, plus two landings 1.4 m |
| Stair window | 1.2 × 1.5 m at the half-landings | 1.2 × 1.5 m | assumed (I: windows offset by half a storey in the entrance bays) |
| Section entrance | 1.2–1.5 × 2.1–2.2 m, double door | 1.3 × 2.1 m | assumed |
| Entrance canopy | slab 1.5 × 2.0–2.5 m at 2.4–2.8 m | 1.5 × 2.2 m at 2.6 m | measured I (a canopy over each entrance); size assumed |
| Balconies | slab 0.9–1.2 × 2.4–3.2 m, 0.12–0.16 m thick, rail 1.0–1.1 m | 1.0 × 2.8 × 0.14 m, rail 1.05 m | measured K (stacked on the long facade next to the torn end); published (from the 2nd floor up on the front facade); sizes assumed |
| Balcony door | 0.75 × 2.1 m opening | 0.75 × 2.1 m | assumed |
| Interior doors | 0.80 × 2.05 m opening, 40 mm casing | clear 0.72 m | published (GOST 6629-88: the 21-8 door mark, its opening 2.1 × 0.8 m, https://meganorm.ru/Data1/3/3481/index.htm ); K (doors in the back walls); casing assumed |
| Flat entrance doors | 0.9 × 2.1 m opening, steel leaf | clear 0.80 m | published (GOST 6629-88: the 21-9 door mark, its opening 2.1 × 0.9 m); the steel leaf assumed |
| Rafters | 0.10 × 0.18 m at 1.0–1.2 m | 1.1 m | assumed; K (the timbers along the roof line) |
| Attic fill on the top slab | 0.15–0.30 m slag or expanded clay | 0.25 m | measured K (a thick dark layer between the top ceiling and the timbers); material assumed |
| Vent stacks | brick, 0.5 × 0.8–1.0 m, 0.5–1.0 m above the roof | 4 per section | measured I (small boxes on the roof line); sizes assumed |
| Ground-floor wall stub at the torn end | 0.8–1.5 m | 1.2 m above the ground-floor slab | measured K |

### Construction and materials
- **Structure (longitudinal bearing walls):** the two long facades and the spine wall carry the hollow-core slabs, which span 5.6 m across from facade to spine (derived). The end walls (gables), the section cross walls and the stair-cell walls are brick but carry little floor load. **Consequence for damage (derived):** an end wall can be blown off while the slabs behind it stay level (exactly K); a slab drops or hangs only where a long wall or the spine below its support is breached.
- **Walls:** brick in lime-cement mortar, 2.5 bricks outside. The look has two finishes on one block, both from the references:
  - the facades **rendered** pale cream, with a painted band (yellow in I) over the lowest one or two storeys of some sections, and a dark plinth (I);
  - around the torn end and big breaches, **bare buff brick with white adhesive dabs** about 0.10–0.15 m across on a 0.25–0.35 m grid (K). The dabs read as the adhesive left after an external insulation layer was blown off (interpreted from the published bead-and-dab method, assumed). So the default build puts that layer on every facade: EPS boards 50–100 mm, mesh and a thin render, which is the smooth pale render I shows; where it is lost, the dabs show on bare brick, and remnant boards hang at the patch edges (geometry).
- **Floors:** hollow-core slabs 1.2 m wide side by side along the block, with a cement screed and linoleum or boards; precast lintels over openings (as the houses sheet: 0.12–0.14 × 0.25 m per leaf, 2–4 bars of 8–12 mm); a cast ring beam with 4 bars of 12 mm at the wall head (assumed). Timber-beam floors (the older Stalin-era alternative, published) are a generator option.
- **Stairs:** precast flights and landings in the brick stair cell, steel balusters with a timber or PVC handrail, the section entrance under a canopy, a vestibule and 6–7 risers up to the ground-floor landing (derived from the 1.0 m plinth).
- **Roof:** hipped (assumed), rafters on purlins carried by posts on the spine wall, battens, cover of painted sheet steel 0.5–0.7 mm or asbestos-cement slate (terrain.md B4: the type has both; houses sheet), an attic over the top slab with its fill layer, small dormer vents; gutters and downpipes of galvanised sheet.
- **Openings:** windows with timber double frames painted white, many replaced by white PVC; balcony doors; radiators (cast iron, 0.5–0.6 m high) under every window; ground-floor windows with steel grilles in places (K: one on the long facade).
- **Services:** heating risers (steel pipe 20–32 mm), wiring in the walls, TV and phone cables on the facade, gas pipe on the yard facade (assumed).
- **Contents (K):** wardrobes and shelving of chipboard, beds and mattresses, sofas and armchairs, tables, appliances and TVs, curtains and sheets, carpets, radiators; wallpaper and painted plaster in pink, blue, green, cream and red (K).
- **Materials:**
  - brick: manifest `red_brick` re-tinted to the K buff (#8d8174 read) for the bare zones and the section cuts, plus a derived dab layer in the facade map (texture: the dabs are under 2 cm)
  - render: Poly Haven `plastered_wall_02` and `white_stucco_02` tinted pale cream ( https://polyhaven.com/a/plastered_wall_02 , https://polyhaven.com/a/white_stucco_02 ); painted band from the same set tinted yellow
  - plinth: Poly Haven `concrete_wall_003` or `rough_block_wall` ( https://polyhaven.com/a/concrete_wall_003 , https://polyhaven.com/a/rough_block_wall )
  - slabs, lintels, balconies, stairs, broken ends: Poly Haven `concrete_wall_003`, `concrete_debris` ( https://polyhaven.com/a/concrete_debris )
  - interior plaster and paint: Poly Haven `worn_cracked_plaster`, `blue_plaster_weathered`, `grey_plaster_02` tinted per room ( https://polyhaven.com/a/worn_cracked_plaster , https://polyhaven.com/a/blue_plaster_weathered , https://polyhaven.com/a/grey_plaster_02 ); wallpaper a derived print map over the plaster (assumed)
  - roof timber, battens, boards: Poly Haven `rough_wood` ( https://polyhaven.com/a/rough_wood ), charred by mask G
  - sheet steel roof and gutters: Poly Haven `rusty_metal_sheet`, `rusty_painted_metal` ( https://polyhaven.com/a/rusty_metal_sheet , https://polyhaven.com/a/rusty_painted_metal ); slate option as the houses sheet H02
  - attic fill: Poly Haven `brown_mud_dry` tinted grey-brown ( https://polyhaven.com/a/brown_mud_dry )
  - furniture board: ambientCG `Chipboard001`, Poly Haven `plywood` ( https://ambientcg.com/view?id=Chipboard001 , https://polyhaven.com/a/plywood ); textiles ambientCG `Fabric030` ( https://ambientcg.com/view?id=Fabric030 ); PVC frames ambientCG `Plastic006` tinted white ( https://ambientcg.com/view?id=Plastic006 )
  - balcony rails and grilles: Poly Haven `rusty_metal_02` ( https://polyhaven.com/a/rusty_metal_02 ); balcony aprons `worn_corrugated_iron` ( https://polyhaven.com/a/worn_corrugated_iron )
  - **Derived:** one facade map per facade (4 per block) in asset space, upright and continuous; the damage mask (R render or insulation loss, G soot, B dirt and streaks, A painted band and plinth); a per-room interior palette map. Rubble uses the same albedos.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Render, lit by the low sun | read #d0c9aa–#e9cb94; author about #d6cdb8 | — (photos only) | measured I; the warm low sun lifts the read |
| Render, the end in shade | read #a68c6b | — | measured I |
| Painted band, lit | read #beac6a; author about #c8b878 | — | measured I |
| Soot plume over render, lit | read #684c31–#865f37 at the plume core; author the soot about #2e2a26 at full density, fading to the render | — | measured I; mask G |
| Plinth | read #454e52 (blue shade, I), #8e8e88 (overcast, K); author about #84847e | — | measured I, K |
| Bare brick with adhesive dabs | #8d8174 (lit lower wall #aea496) | — | measured K; terrain.md B4 #847b71 |
| Spine wall in section | #8d8479 | — | measured K |
| Slab edge, concrete | #70675e | — | measured K |
| Room ceilings in deep shade | read #46433a; author white-grey plaster about #d4d0c8 | — | measured K |
| Painted room wall, red, deep shade | read #402923; author per the room palette | — | measured K |
| Chipboard furniture, lit at the edge | #967a61 | — | measured K |
| Attic fill and charred timber, shade | #282622 | — | measured K |
| Sky (for the view, not the asset) | #7b97b0 (K, overcast) | — | measured K |

### Variants and states
- **Exactly one block on the map, in one destroyed state** (asset-list). The generator's dials at 0 give the **pre-war look** (for checking proportions and the review sheet's turntable, not shipped): a rendered block with a painted band, white frames, glazed balconies, an intact hipped roof.
- **The shipped kit (the default params), composed so one block serves both views:**
  - the **torn end** (K): the end wall of the end section gone from 1.2 m above the ground-floor slab to the roof, every floor open, the slabs level, the long walls' end piers and the spine standing in bare brick with stepped edges;
  - **burnt sections** (I): 60–80 % of the windows of two middle sections burnt out, with soot plumes; the roof over them burnt down to the wall head (charred stubs, which is why I's roof line reads flat; see Open points);
  - the **roof down to its timbers** over the torn-end section (K): cover gone, battens mostly gone, rafters standing or splayed;
  - **one bearing-wall breach** on the long facade that neither reference view sees, one or two bays in from the torn end, at floors 3–5, 2–4 m wide, with **1–2 hanging slabs** above it (the asset list's kit item; K's own slabs are level, I does not show that end);
  - **contents spilling out** of the open floors and the mound (U02) at the torn end;
  - glass gone everywhere, frames out in the burnt sections and near the torn end, left in place (crazed, holed or empty) elsewhere.

### Damage pattern (the damage kit as physical geometry)
How such a block fails, in order (research table, the houses sheet's thresholds, derived from the load path):
1. **Glass** goes everywhere; frames are blown in near the burst and burnt out in the fire sections.
2. **The end wall** fails out of plane and falls outward (it carries no slab), leaving the slabs level and the rooms open (K). Its brick lands as the mound at its foot (U02); a stub 0.8–1.5 m stays at the bottom, buttressed by the debris (K: 1.2 m).
3. **Bearing-wall breaches** under a slab support: the slab loses that end and drops; a broken slab hinges at its crack.
4. **Fire** runs up through the flats: flames out of the windows lick the wall above, soot plumes rise, the roof timbers burn to the wall head over the burnt sections.
5. **Partial collapse** only where a long wall goes over several floors (an X section, a generator option, not in the default kit).

Each kit item, what it is physically:
- **Torn end:** the end wall removed; the end faces of the long walls and the spine left as **stepped brick edges** (courses jutting 0.1–0.4 m, half-bricks hanging, a bonded chunk 0.5–1.2 m leaning out on a few floors, K); each floor's slab edge exposed along the end: a straight concrete edge where the end slab survived (K), or a broken edge with the round voids opened and the bottom bars out where it cracked; screed and floor finishes overhanging 0.05–0.2 m; partitions torn at the edge; the ground-floor stub with its top course ragged (K); the insulation layer gone from the long walls 2–6 m back from the corner, with dabs on bare brick and EPS remnants at the patch edge.
- **Hanging and sagging slabs:** a **hollow-core slab** that lost one support drops that end until it lands on the floor below or on debris, hanging at **20–60°** from its other support, held at the joint anchors (assumed); a **cracked slab** hinges on its bottom bars and sags **5–30°** at the free edge (the H06 rule); its broken face shows the Ø 159 mm voids (published) and bars. On timber-beam floors (option) the beams hang from the remaining wall pocket, the boards split.
- **Protruding rebar (PR-5):**
  - at broken slab ends: the bottom bars or wires, Ø 5–14 mm, ribbed (А-III class) or smooth wire, 0.1–0.6 m out and bent, one per 0.15–0.3 m of broken edge (derived from the bar pitch), 30–70 % of them visible (the rest snapped flush, assumed); the screed's mesh Ø 4–5 mm at 0.10–0.20 m;
  - at broken lintels over blown windows: 2–4 bars Ø 8–12 mm bent out 0.2–0.5 m (houses sheet);
  - at broken balcony slabs: the top bars Ø 10–12 mm at 0.15–0.20 m, a slab hanging on them at 20–80°;
  - at the ring beam where the wall head broke: 4 bars Ø 12 mm, up to 1 m exposed;
  - in the mound: K06 concrete chunks with bars (lines-roads-debris sheet).
- **Burnt windows with soot plumes (I):** frame gone or charred stubs in the reveal, the reveal and the lintel blackened, the room inside black (walls, ceiling, a charred floor, no contents left but metal: bed frames, radiators); a plume from the window head **1–2 storeys high (3–6 m), widening upward to 2–3 window widths**, plumes merging into a band below the parapet where several windows burnt (measured I); with wide windows the flame clings to the wall above (published), so the soot sits on the facade, not beside it. Soot is mask G (texture); the charred frames, the fallen ceiling plaster and the burnt furniture skeletons are geometry.
- **Roof down to its timbers (K):** sheet cover gone over 80–100 % of the torn section, battens in 20–40 % of spans with splintered ends, rafters standing or splayed, a few broken and leaning out over the eaves (K: timbers along the roof line, a few pointing up); the attic fill exposed on the top slab; cables strung across from the roof (K). Over the burnt sections: rafters burnt to stubs 0.2–0.8 m on the wall plate, sheets fallen into the attic, ash on the fill.
- **Contents spilling out (K):** furniture in place but shifted toward the open edge; on 1–3 floors a wardrobe or shelving tipped at the edge; a curtain or sheet spanning a room (K: one across a 4th-floor room); textiles over slab edges; on the lower floors furniture fallen into the mound; cables and pipes hanging diagonally across the open bays (K: several pale lines from slab edges to lower floors); radiators still on the walls.
- **Balconies:** rails bent outward, glazing frames hanging, corrugated aprons torn, one balcony slab cracked and hanging on its top bars; fabric caught in the rails (K: a red cloth).

### Physics role
- **Collision** (±2 cm around openings and landable surfaces, ±5 cm elsewhere):
  - walls as boxes per segment between openings at their real thickness; stepped breach edges as stacked boxes per 1–3 courses;
  - each slab as a box per span (0.28 m) with its broken edge as a stepped box; hanging slabs as oriented boxes; void openings on broken faces as notches only where they are wider than 0.1 m;
  - rebar as capsules r 0.003–0.007 m, wire r 0.002–0.0025 m, each bent bar as 1–3 segments;
  - window frames as four thin boxes plus the mullion; grilles as capsules;
  - balcony slab a box, rails as capsules, aprons thin boxes (`sheet_metal`);
  - roof: rafters and purlins boxes, battens boxes 0.05 × 0.05, remaining sheets thin boxes, the attic fill a low box;
  - stairs: each flight a sloped box with stepped visual (the drone flies, it does not walk), landings boxes, balusters capsules;
  - contents: furniture as boxes, radiators as boxes, mattresses and sofas as boxes with `proposed:fabric`, hanging cables and fabric per K10.
- **Material ids:** brick, render, plinth, stair-cell walls: `masonry`; slabs, lintels, balconies, flights, ring beam, canopies: `concrete`; rebar, rails, grilles, radiators, pipes, bed frames: `steel`; roof sheet, gutters, balcony aprons: `sheet_metal` (cuts fiber); rafters, battens, timber frames, boards, chipboard furniture: `timber`; glass remnants: `glass`; hanging cables: `cable`; curtains, sheets, mattresses, sofas, carpets: `proposed:fabric`; PVC frames and profiles, appliances, plastic sheet, EPS remnants: `proposed:plastic`; slate option: `proposed:asbestos_cement`.
- **Gaps** (each recorded as the real clear polygon):

  | Opening | Narrowest clear width | Passes? |
  |---|---|---|
  | Open bay at the torn end, per floor | 5.0–6.0 × 2.72 m (minus hanging cables, fabric and pipes, which are snags, not walls) | **yes, any yaw**; the slow fly-in of B4 |
  | Window, frame gone | 1.3 m minus jags 0–0.2 m a side: 0.9–1.3 m | **any yaw** (97 mm a side on the diagonal at 0.9 m) |
  | Window, frame in place, mullion gone | 1.16 m | **any yaw**, 227 mm a side on the diagonal |
  | Window, frame and mullion in place | two leaves of 0.54 m | **no** (0.54 < 0.574): the frame must burn or break first |
  | I-variant three-leaf window 1.8 × 1.35, frame in place | three leaves of about 0.50 m | **no**; frame gone 1.8 m: any yaw |
  | Ground-floor window with a grille | bars at 0.12–0.15 m | **no** |
  | Balcony door, frame gone | 0.75 m | **any yaw**, 22 mm a side on the diagonal |
  | Balcony door, frame in place | 0.65 m | nose-first, within ±16°, 38 mm a side |
  | Interior door (casing in place) | 0.72 m | **any yaw, 7 mm a side on the diagonal**, 73 mm nose-first: the narrowest indoor passage, as tight as the cellar door (PR-9) |
  | Flat entrance door, leaf open | 0.80 m | any yaw, 47 mm a side on the diagonal |
  | Section entrance | 1.3 m | any yaw |
  | Stair flight (along it) | 1.05–1.2 m minus the 0.05 m baluster line; headroom 2.0–2.2 m over the pitch line | **yes**: the indoor route between floors, turning 180° on landings 1.4 × 2.6 m |
  | Stair well (straight down between flights) | 0.1–0.3 m | **no** vertical route |
  | Stair window | 1.2 m, frame gone | any yaw |
  | Roof bay between rafters, battens gone | 0.9–1.0 m | any yaw |
  | Roof bay with battens in place | 0.25–0.35 m (sheet steel); 0.5–0.75 m (slate option) | **no** for sheet battens; slate battens: no below 0.574 m, nose-first at 0.6–0.7 m (±5° to ±35°), any yaw at 0.75 m |
  | Wedge under a hanging slab | as generated: a triangular opening from 0 to the drop height | recorded per slab; passes where the polygon admits 0.574 × 0.55 m |
  | Shell hole through a slab | 0.5–1.5 m | passes only once the mesh is bent out; with the mesh across it, **no** |
  | Wall breach | as generated, stepped | passes when ≥ 0.6 m clear after jags and bars |
- **Wind volumes:**
  - the block as one solid box per section (porosity 0) where the windows are intact; **long walls with blown windows 0.15–0.25** (derived: a 1.3 × 1.8 m window in a 3.3 × 3.0 m bay is 0.24 of the wall; 0.19 with the frame left), so a floor with windows out on both facades carries a through-draught;
  - each open bay at the torn end a **cavity** with an open face of porosity about 0.85 (derived: slab edges 0.28 of 3.0 m, the piers and the spine), dead-ended 3–4 m in at the first partition unless its back window is out;
  - the roof skeleton 0.8–0.9 (as H06), sheet remnants 0, the attic a cavity between the fill and the skeleton;
  - balconies with frames 0.3–0.6;
  - **the wake** (terrain.md B4): roof-edge separation and corner vortices; a recirculation zone 1.5–2.5 block heights behind it (with the 19.5 m ridge, **29–49 m**, derived) and a gusty wake to about 10 heights (**about 195 m**, derived); downwash on the lee face; channelling between the block and the garages (U03). The physics engineer's wind grid reads the block's solid and cavity boxes; the wake is the flow solution, not a hand-placed volume.
- **Snag hazards:** rebar and wire at every broken slab, lintel, balcony and ring beam; the screed mesh; hanging cables (K10) and the cables strung from the roof; curtains, sheets and textiles (K10, `proposed:fabric`); balcony rails, glazing frames and torn aprons; window grilles; frame remnants and charred frames; splayed rafters and broken battens; heating risers; EPS remnants with their mesh.
- **Landable surfaces:**
  - **each open floor at the torn end:** the slab 5.6 × 3–4 m per bay, hard, flat, `concrete`, landable from 0.5–1 m behind the edge (the sagging edge is not); furniture and fallen plaster on it make it uneven; the 2.72 m clear height leaves room to hover (the ceiling effect is the physics engineer's to judge);
  - **balcony slabs:** 1.0 × 2.8 m inside a 1.05 m rail, entered from above (97 mm a side on the diagonal inside a 0.9 m clear rail box, derived) or through the balcony door;
  - **the attic fill** between the rafters over the torn section: soft and uneven (`rubble`-like, assumed), entered through a roof bay;
  - **the entrance canopies** (1.5 × 2.2 m slabs at 2.6 m): hard and flat;
  - the hipped roof's sheet remnants: **no** (25° slope, it slides).

### Placement rules (for the world)
- **Exactly one, at the urban edge** (pilot, terrain.md B4, §10): on the edge of the largest of the three villages, toward the fields, long axis along the street, 20–40 m back from it, the entrance facade to the yard.
- **Both reference views must fit:** the long facade needs 60–80 m of open yard and street in front for the I view, with H06 (one or two), Y04 (the lattice-topped fence), the roof pile (U02b) and a rubble field between; the torn end needs 20–25 m of clear trampled ground (T8, S10 fines) in front of the mound for the K view.
- **Neighbours:** the mound U02 at the torn end; the garage row U03 within 50–150 m; trees (bare in the reference season) along the yard; poles and wires (L rows) along the street; a transformer kiosk or pole transformer (P05) by the yard; abandoned vehicles (R rows) in the yard and street.
- **Orientation:** the torn end faces open ground (the K view), the burnt sections face the yard (the I view). No wind constraint: the wind direction is a setting, and the wake follows it.

### Generator parameters (section-based, with the damage kit)
The block is built from a **section list**: each section is one stair cell with its flats on both sides of the spine, so length, section count and per-section damage are parameters, not modelling.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `sections` | int | 3–5 | 4 | Number of stair sections; the block length follows |
| `section_length` | m | 13–20 | 15 | Length of each section (window bays follow from `window_pitch`) |
| `depth` | m | 12–15 | 13 | Block depth; the slab span = (depth − 2 × wall − spine) / 2 |
| `storeys` | int | 5 | 5 | Fixed for this asset (exposed for checking only) |
| `storey_height` | m | 3.0–3.4 | 3.0 | Floor to floor (AST-04); eaves height follows |
| `plinth_height` | m | 0.8–1.5 | 1.0 | Plinth above grade; the entrance risers follow |
| `wall_thickness` | m | 0.51–0.65 | 0.64 | Exterior walls: reveal depth, jag depth, breach section |
| `spine_thickness` | m | 0.38–0.51 | 0.51 | The longitudinal bearing wall |
| `floor_type` | enum | hollow_core, timber_beam | hollow_core | Slab build-up, failure (drop and hinge vs hanging beams), rebar or nails |
| `roof_type` | enum | hipped, gable, flat_attic | hipped | Roof form (flat_attic gives a parapet with small vents, the I silhouette) |
| `roof_rise` | m | 3.0–4.0 | 3.0 | Ridge height; pitch follows |
| `roof_cover` | enum | sheet_steel, slate | sheet_steel | Cover geometry, batten pitch, failure style, material id |
| `rafter_pitch` | m | 1.0–1.2 | 1.1 | Roof bay width once the battens are gone |
| `facade_finish` | enum per facade | bare, render, etics | etics | Finish; etics adds the 50–100 mm board layer, its thin render and the dab pattern where it is lost; render is lime-cement on brick (lost patches show plain brick) |
| `render_colour`, `band_colour` | sRGB | — | #d6cdb8, #c8b878 | Render and painted-band tints |
| `band_zones` | list | 0–4 of (section, storeys) | 1 section, 2 storeys | Where the painted band is (I) |
| `window_w`, `window_h` | m | 1.2–1.9, 1.3–1.9 | 1.3, 1.8 | Window size (variant I: 1.8 × 1.35 three-leaf) |
| `window_leaves` | int | 2–3 | 2 | Mullions; with frames in place each leaf must exceed 0.574 m to pass |
| `window_pitch` | m | 3.0–3.6 | 3.3 | Window spacing along the facade |
| `frame_mix` | 0–1 | 0–1 | 0.5 | Fraction of white PVC frames vs timber frames |
| `balconies` | list | per facade, from storey, columns | yard facade, from 2, 2 columns per section | Balcony slabs and rails (K: stacked by the torn end) |
| `ground_grilles` | 0–1 | 0–0.5 | 0.2 | Fraction of ground-floor windows with grilles |
| `entrance_canopy` | bool | — | on | Canopy slab per section entrance |
| `vent_stacks` | int | 2–6 per section | 4 | Brick stacks on the roof |
| `torn_end` | enum | none, left, right | right | Which end wall is blown off |
| `torn_from_storey`, `stub_height` | int, m | 0–2, 0–1.5 | 0, 1.2 | Where the end wall starts to be gone and the stub left |
| `torn_depth` | m | 0–4 | 0 | 0 removes only the end wall; more removes the first room row too (open bays deeper than one room) |
| `insulation_loss_radius` | m | 0–8 | 4 | Bare brick with dabs around the torn end and breaches |
| `breaches` | list | 0–4 of (facade, section, storeys, width) | 1: the long facade the I view does not see, torn section, storeys 3–5, 3 m | Bearing-wall breaches; slabs above them lose support |
| `hanging_slabs` | int | 0–4 | from `breaches` (2) | Slabs dropped at one end; `slab_hang_deg` 20–60 |
| `slab_sag_deg` | deg | 0–30 | 10 | Sag of cracked slab edges at the torn end and breaches |
| `slab_holes` | int | 0–4 | 1 | Shell holes through slabs, 0.5–1.5 m, with their mesh |
| `rebar_density` | /m | 3–7 | 5 | Bars per metre of broken concrete edge (30–70 % visible) |
| `rebar_out` | m | 0.1–1.0 | 0.4 | Mean length of protruding bar; bend random within ±60° |
| `burnt_sections` | list | 0–all | 2 middle sections | Sections with fire damage |
| `burnt_windows` | 0–1 | 0–1 | 0.7 | Fraction of windows burnt out in a burnt section (upper floors favoured) |
| `soot_height` | storeys | 0.5–2 | 1.5 | Plume height above the window head (mask G) |
| `soot_spread` | × window width | 1–3 | 2.5 | Plume width at its top |
| `roof_strip` | 0–1 per section | 0–1 | 1.0 torn, 0.3 others | Fraction of roof cover gone |
| `roof_burnt` | 0–1 per section | 0–1 | 1.0 burnt sections | Rafters burnt to stubs (0.2–0.8 m) on the plate |
| `batten_break` | 0–1 | 0–0.6 | 0.4 | Broken batten spans, ends irregular (never a regular grid) |
| `glass_left` | 0–1 | 0–0.3 | 0.05 | Fraction of panes left (crazed, holed) |
| `frames_out` | 0–1 | 0–1 | 1.0 torn and burnt sections, 0.3 others | Frames gone (their leaves in the rooms or the mound) |
| `contents_density` | 0–1 | 0–1 | 0.7 | Furniture per room (K08 pieces) |
| `contents_spill` | 0–1 | 0–1 | 0.4 | Items tipped at or over the open edges, and into the mound |
| `hanging_cables` | int | 0–20 | 10 | K10 cables across the open bays and from the roof |
| `hanging_fabric` | int | 0–8 | 4 | K10 curtains and sheets over edges and rails |
| `balcony_damage` | 0–1 | 0–1 | 0.5 | Rails bent, frames hanging, one slab hanging on its bars at 1.0 |
| `collapse_sections` | list | 0–1 | none | An X section: floors pancaked to a mound of h = γ·H/(100 + k·H) (published), not in the default kit |
| `mound` | seed | — | U02 default | The rubble mound generated at the torn end from the removed wall volume |
| `seed` | int | — | — | Every random choice above |

### Open points
- **Eaves height:** asset-list §7 gives 15–17 m, which holds for 3.0–3.1 m storeys on a 1 m plinth; 3.4 m storeys (AST-04's top) give 18–19 m. The default 3.0 m keeps both; with a 3 m roof rise the ridge is at 19.5 m (derived). The asset list now reads "16–19 m to the eaves".
- **Windows:** the given 1.3 × 1.8 m (taller than wide) is the default; I reads wider than tall (1.8 × 1.35 m three-leaf). Both pass the drone only with the frame gone. Settled at the I side-by-side, not by the pilot.
- **I's flat roof line:** from the I viewpoint a 25° hip would show its ridge 1–2° above the eaves line (derived: eaves 16.5 m and ridge 19.5 m at 70–76 m), and none shows. The default explains it with the roof burnt down over the burnt sections; `roof_type` flat_attic is the alternative. Settled at the I side-by-side.
- **The white dabs** on K's brick are interpreted as insulation adhesive; if the side-by-side reads them as something else (tile adhesive, render keys), only the facade map changes.
- **Storey count from K:** five floors of open rooms read above the plinth, matching the type; no attic storey.
- The ceiling effect in a 2.72 m room (the drone hovering under a slab) is the physics engineer's call, not a sheet number.

## U02 Rubble mound of the block

**Seen in:** K (the mound at the torn end, from the ground-floor stub out over the trampled ground; its pieces); I (a rubble field of the same class in the foreground, for the scatter look).
**Research sources:** the rubble method (spread, height, volume, profile) in the research table; the S10 row of the earthworks sheet (the fines under it).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Height (peak, at the wall) | 2–4 m | 3.2 m | terrain.md B5 (K); derived below |
| Spread out from the wall | 4–10 m | 8 m | measured K 6–10 m; published gravity collapse H/4–H/3 = 4.1–5.5 m (H 16.5 m) as the minimum, the blast throws it further |
| Width along the end | 15–20 m | 18 m | terrain.md B5; derived: the 13 m end plus spill round both corners |
| Volume | 180–300 m³ | 230 m³ | derived: the removed end wall 13 × 14.3 × 0.64 = 119 m³ less 10 % openings, plus partitions and contents 10–20 m³, ≈ 125 m³ solid, × a bulking of 1.86 (published γ = 40 m³ per 100 m³ of building over a solid fraction of 0.215, derived for this block's walls and slabs) |
| Profile | linear wedge, h(x) = h·(1 − x/l) | — | published; the peak follows from the volume: h = 2V / (l × W) = 2 × 230 / (8 × 18) = 3.2 m |
| Void fraction | 0.40–0.50 | 0.46 | derived: 1 − 1/1.86 |
| Scatter beyond the toe | 5–15 m | 10 m | measured K (bricks, white plastic bits and cable loops out to the bottom of the frame); I (the rubble field) |
| Pieces | 0.05–3 m | — | measured (terrain.md B5, K) |
| Pitfall voids | 0.3–1.0 m across, 0.2–0.8 m deep | 1 per 8 m² | assumed (cavities under a leaning slab or between large pieces) |

### Construction and materials
- **Composition** (measured K, by eye, as fractions of the visible surface): broken and whole bricks 50–60 % (buff with white dabs, and orange-red; many perforated bricks); bonded brickwork chunks 0.3–1.2 m, 5–10 % (K: one leaning chunk about 1.2 × 1.0 m against the stub, one on the mound); concrete pieces 5–10 % (K: a precast element about 3 × 0.35 × 0.3 m with a recessed panel face, slab and lintel chunks, K06 with bars); timber 10–15 % (planks, laths, roof timbers, chipboard panels, a sawn trunk section about 0.35 m across from a felled tree); white PVC window frames and profiles 5–10 %; furniture and appliances 5–10 % (wardrobe carcasses, a brown wardrobe side, an armchair, a black chair, a screen, a blue plastic box); textiles, plastic sheet and bags (white), cables; fine crushed brick and plaster between (S10).
- **Structure:** a core of stacked block primitives with flat faces and sharp edges (the earthworks sheet's S10 note: smooth noise relief makes 40–50° troughs that real rubble does not have), the large pieces resting on it, the small pieces (K01, K02, K03, K06, K08) instanced on its surface, the fines (S10) on the ground around and between. Part of the mound runs over the stub into the ground-floor rooms (K).
- **Materials:** as U01 (brick `red_brick` tinted buff with the dab layer, `concrete_debris`, `rough_wood`, `Chipboard001`, `Plastic006` white, `Fabric030`); the fines Poly Haven `concrete_debris`, ambientCG `Ground111` and `Ground107` tinted to the mix (earthworks sheet, S10).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Mound, overall mean | #81796d | — | measured K; terrain.md B5 #8d8172 |
| Brick field on the mound's right flank | #978a78 | — | measured K |
| Trampled ground in front | #766e5d | — | measured K; terrain.md T8 #867c6a |
| Rubble field in blue shade (the I foreground) | read #415669; albedo as the K mix | — | measured I |

### Variants and states
- Variant 1: the K mound (brick-rich, at the torn end). Variant 2: the X-section mound (`collapse_sections`), sized by h = γ·H/(100 + k·H) = 40 × 16.5 / 108.25 ≈ 6.1 m inside the section footprint and spread H/4–H/3 (published), concrete-rich with slabs leaning.
- States: none (it is the damage). Weathering: fresh (dust, white plaster) or settled (weeds in the fines, rain-darkened).

### Damage pattern
- It is the damage. The brick lies where the wall fell: densest at the wall foot, thinning outward; light things (plastic, textiles) blown further; large pieces at the toe from the upper floors (they fall furthest, derived from the drop height).

### Physics role
- **Collision:** the core as a set of boxes and wedges following the visual within ±5 cm (±2 cm on faces the drone can land on); each large piece (≥ 0.3 m) its own box or capsule; small pieces via the debris kit's own collision; voids are open cavities in the core, never filled.
- **Material ids:** core and brick pieces `masonry` (the walkable top faces may carry `surface:rubble`, the earthworks filler convention; the physics engineer decides); concrete pieces `concrete`; bars `steel`; timber and furniture board `timber`; PVC and plastic `proposed:plastic`; textiles and mattresses `proposed:fabric`; cables `cable`.
- **Gaps:** none to fly through; the voids are **pitfalls**: a leg drops 0.2–0.8 m into a 0.3–1.0 m cavity without warning (terrain.md B5).
- **Wind volumes:** solid (0); its roughness shelters the ground in front from the wake's downwash only locally.
- **Snag hazards:** jutting timbers 0.5–2 m (the S10 cover: 0.3 /m²), bars on concrete pieces (K06), cables and textiles, PVC frames.
- **Landable surfaces:** very uneven: the flat top faces of bonded chunks, slabs and the precast element (≥ 0.6 × 0.6 m) are landable but tilt; brick fields tilt the drone at liftoff (S10 behaviour).

### Placement rules
- Against U01's torn end (and any `collapse_sections`), oriented by the wall it came from; its fines feather into the surrounding T8 ground over 1–5 m (earthworks sheet, feathered transitions).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `source_volume` | m³ | 50–400 | from U01 (125) | Solid volume of the fallen masonry and contents |
| `bulking` | × | 1.6–2.0 | 1.86 | Rubble volume per solid volume (void fraction follows) |
| `spread` | m | 4–10 | 8 | Distance from the wall to the toe; peak height follows from the volume |
| `width` | m | 13–22 | 18 | Length along the wall (the end plus the spill round both corners) |
| `profile` | enum | wedge, obelisk | wedge | Published linear fall-off; obelisk for an X section |
| `piece_mix` | fractions | brick, chunk, concrete, timber, pvc, contents, textile | 0.55, 0.08, 0.08, 0.12, 0.07, 0.07, 0.03 | What the surface is made of (K) |
| `max_piece` | m | 1–3.5 | 3.0 | Largest piece (the precast element) |
| `void_density` | /m² | 0–0.25 | 0.12 | Pitfall cavities per m² of top surface |
| `void_size` | m | 0.3–1.0 | 0.6 | Mean void opening |
| `timber_jut` | /m² | 0–0.5 | 0.3 | Timbers jutting 0.5–2 m (S10 cover) |
| `rebar_chunks` | int | 0–20 | 8 | K06 pieces with bars |
| `scatter_radius` | m | 5–15 | 10 | Thin scatter of bricks, plastic and cable loops beyond the toe |
| `into_building` | m | 0–4 | 2 | How far the mound runs into the ground-floor rooms |
| `seed` | int | — | — | Every random choice above |

### Open points
- Whether the mound's top faces use `surface:rubble` from the surface table or plain `masonry` on asset shapes is the physics engineer's call (the earthworks sheet leaves the same question for fillers).

## U02b Collapsed timber roof pile (the roof of a house or wing fallen as one heap)

**Seen in:** I (the pile in the middle of the view: a tangle of sawn roof timbers lying on a brick rubble core, one red-brick wall end still standing at its right side with a rafter leaning on its face, dark cavities under the timbers at the front toe, a few grey-green sheet pieces, a bent steel grid and a blue-green item in the brick at the front). Read on the single still plus local zooms at 2.5–5×; no clip shows it (terrain.md B5). The earlier states of the same failure (stripped roofs, rafters off the plate) are the houses sheet's E and J, not re-read here.
**Research sources:** the houses sheet's shared construction (roof carpentry, tile and slate, the ceiling with its fill, the failure order) and its rubble rule; U02's bulking (1.86); the debris kit (K01–K04, K08, K10) and its spread; collapse patterns of timber and brick-and-joist buildings and the voids each leaves (pancake: pockets only where contents interrupt the stack; lean-to: one end still held, a triangular void; V-shape; A-frame or tent: held up in the middle, voids near the centre), from general rescue-training references: https://www.designingbuildings.co.uk/wiki/Types_of_collapse , https://emberly.fireengineering.com/firefighter-training/collapse-search-and-rescue-operations-tactics-and-procedures-7 .

**How I was measured (derived, rough, about ±15 %):** one landscape still 800 px wide; a horizontal field of about 69° (the photo's lens, assumed: the same lens K's 50° portrait field implies), so f ≈ 580 px; eye height 1.6 m (the REAL-02 row). The horizon sits at about 62 % of the frame height from the top, read off the block's left end: its five 3.0 m storeys, plinth and parapet span 43 % of the frame height there, so 1.6 m is 4 % of the frame at that distance, and the horizon lies that far above the block's foot. A point at image row y and distance d then stands H = 1.6 + (y_horizon − y) · d / f above the ground. The pile's front toe, behind the fence at 71–74 % of the frame height, comes out at 17–22 m, so its body centre is at about 24 m (22–26 m), inside the 20–30 m of the reference-view row.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Source roof | a single-storey house of the H01–H04 classes (6–12 × 5–8 m), or a wing of one (4–6 × 4–6 m) | H03's defaults with `wall_material` red_brick (I): 9 × 7 m, walls 0.38 m to 2.9 m on a 0.4 m plinth, pitch 25°, rafters at 1.0 m, battens at 0.5 m | assumed, as the houses sheet (H03's defaults); red brick measured I (the wall end and the core) |
| Heap body footprint | 6–11 × 5–9 m | 10 × 8 m | derived: the frame's plan, 9.8 × 7.8 m (H03's 9 × 7 m plus the shared 0.4 m overhangs), with the feet's spread and the rubble at its toe, rounded. Measured I: 11.8–13.9 m across the view, which a 10 × 8 m body shows when seen 30–45° to its long axis (10 cos θ + 8 sin θ = 12.7–12.8 m). The asset list's 6–10 m across holds |
| Frame surface at the high end | 2.5–4.0 m | 3.2 m | measured I: 2.9–3.5 m (the frame's outline at 52–54 % of the frame height, at 22–26 m); derived: the standing wall end's top (3.0 m) plus the frame's depth (0.15–0.3 m) |
| Member tips above the frame | 0.3–1.2 m | 0.8 m (tips to 4.0 m) | measured I: broken members pointing up to 3.8–4.4 m |
| Frame at the low end | 0.8–2.0 m | 1.3 m | measured I: 1.1–1.7 m at the left end (rough) |
| Flank slope | 10–35°, varying along the length | 30° at the high end, 10–15° at the low end | measured I: 20–35° on the projected outline (rough; a projected outline reads steeper than the true slope). Derived: rafters 3.86 m long (3.5 m / cos 25°) falling from the ridge at 3.2 m to the eaves lines on the core at about 1.0 m: atan(2.2 / √(3.86² − 2.2²)) = 35° |
| Standing wall end | 0–2 ends, 1–3 m long, to 0.6–1.0 of the wall height | 1 end, 2.0 m long, top at 3.0 m (0.9 of 2.9 + 0.4 m) | measured I: one red-brick end about 2.0 m long (1.9–2.2 m), its top at 2.9–3.2 m, its foot in rubble 0.4–0.6 m deep, the top broken and rounded |
| Wall stubs elsewhere | 0–1.5 m | 0.5 m | assumed (the houses X state's 0.5–1.5 m; none shows in I above the rubble) |
| Masonry core | 0.3–1.5 m deep | 0.6 m mean; 1.0–1.4 m along the wall lines, 0.4–0.6 m mid-house | derived: about 21 m³ of brick removed from H03's walls (30.5 m of wall × 0.38 m × 2.4 m above the stubs, less 4.9 m³ of openings and the 1.8 m³ standing end) × 1.86 = 40 m³, plus the ceiling's fill (51 m² × 0.2 m = 10 m³; its joists and boards are frame members, counted under timber): 50 m³ over 80 m² |
| Timber in the heap | 5–9 m³ | 6.6 m³ | derived from H03's defaults: rafters 20 × 4.3 m × 0.015 m² = 1.3; battens 173 m × 0.0025 m² = 0.4; plates, ridge and collar ties 0.8; ceiling joists 2.5; ceiling boards 1.6 |
| Void fraction of the timber tangle | 0.85–0.95 | 0.9 | derived: 6.6 m³ of timber through a tangle about 1 m thick over the 80 m² body: 1 − 6.6 / 80 = 0.92 |
| Members | rafters 100 × 150 mm or round 0.10–0.16 m, 1–4.3 m; battens 50 × 50 mm, 0.3–4 m; plates 150 × 150 mm; ceiling joists 150 × 200 mm; boards 25–40 × 100–200 mm | as K03 | assumed, as K03 (its battens published); houses sheet |
| Cavities under the frame | 0.5–2 m wide, 0.3–0.9 m high, 1–3 m deep | 2 cavities, 1.4 × 0.5 m, 1.5 m deep | measured I: a dark cavity about 1.4 × 0.5 m at the front toe (rough); depth assumed |
| Openings between crossed members | 0.1–0.6 m | — | measured I: dark gaps of 0.2–0.6 m over the whole frame (rough) |
| Pitfall voids in the core | 0.3–1.0 m across, 0.2–0.8 m deep | 1 per 8 m² | as U02 (assumed) |
| Scatter beyond the toe | per class, the debris kit's λ | tile shards λ 1 m, bricks 1.5 m, timber 2 m, slate 4 m, sheet 6 m | assumed, as lines-roads-debris "Shared: the debris kit" (λ anchored on measured E, J, K) |

### Construction and materials
- **What fell, layer by layer** (the source house's own parts, so nothing is invented for the pile):
  - **floor and wall stubs:** the house's floor and the wall lines cut down to `wall_stub_h`, one wall end standing (I) with a broken, stepped top;
  - **the masonry core:** the walls' brick (or adobe) fallen in and out, thickest along the wall lines, with the broken lintels as K06 pieces (bars 8–12 mm, the houses sheet), whole and halved bricks and clusters (K01), and the stove or chimney stump mid-house; S10 fines between;
  - **the ceiling:** joists and boards down on the floor and the core, split, with the 0.15–0.25 m clay-straw or slag fill spilled as a crumbly layer (houses sheet, shared section); contents (K08) crushed under it and propping pockets;
  - **the roof frame on top:** rafter pairs still joined at the ridge and by their collar ties over part of the length, wall plates lying along the old wall lines, battens still nailed in part of the spans, rafters snapped at mid-span or at the collar with splintered ends, some springing up out of the heap (I: tips to about 4 m);
  - **the cover:** mostly not on the pile. Slate shatters and tiles slide before and during the drop (houses sheet, steps 2 and 6), so the cover lies in the fields around by the kit's λ; a few clusters stay on the battens at the low eaves, and whole or half slate sheets lie across the frame. I shows almost no cover on the pile: a few grey-green sheet pieces whose kind is not resolved at this size.
- **Materials** (the K-kit candidates, CC0):
  - frame timber: Poly Haven `rough_wood`, `raw_plank_wall`, `weathered_brown_planks`, `old_planks_02`, `roof_planks` ( https://polyhaven.com/a/rough_wood , https://polyhaven.com/a/raw_plank_wall , https://polyhaven.com/a/weathered_brown_planks , https://polyhaven.com/a/old_planks_02 , https://polyhaven.com/a/roof_planks ), as K03. The faces that were inside the roof are tinted aged pine (#a98762, below); the faces that were outside (rafter tails, gable boards) the K03 weathered grey
  - brick: manifest `red_brick` (pinned) for red brick, Poly Haven `white_bricks` for silicate ( https://polyhaven.com/a/white_bricks ), as K01; adobe as H01
  - ceiling fill: Poly Haven `brown_mud_dry` ( https://polyhaven.com/a/brown_mud_dry ) tinted to the clay render #97754f (houses sheet), or grey for slag
  - tile: Poly Haven `clay_roof_tiles_02` ( https://polyhaven.com/a/clay_roof_tiles_02 ); slate: `concrete_wall_003` tinted #736767 ( https://polyhaven.com/a/concrete_wall_003 ), as K02
  - sheet metal: Poly Haven `corrugated_iron`, `rusty_corrugated_iron`, `rusty_metal_sheet` ( https://polyhaven.com/a/corrugated_iron , https://polyhaven.com/a/rusty_corrugated_iron , https://polyhaven.com/a/rusty_metal_sheet ), as K04
  - fines: S10 (`concrete_debris`, ambientCG `Ground111`, `Ground107`)
  - **Derived:** grain along each member in asset space; K03's fracture-end map and char mask; the standing wall end takes the source house's facade map (upright and continuous) and damage mask.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Sawn faces that were inside the roof, lit | read #ae8555; author about #a98762 | — (photos only) | measured I; the read corrected for the warm low sun by the ratio U01's lit render needs (read about #dcca9f, authored #d6cdb8) |
| Timber in shade | read #3d3834–#493a30; author the K03 weathered grey #8a8478 on outside faces, #a98762 on inside faces | — | measured I (blue shade); K03 |
| Fresh breaks | #b09a78 | — | K03 (assumed) |
| Charred (`burnt` only) | #2a2622 | — | K03 (assumed) |
| Red-brick wall end, shade face | read #2d2426; author as `red_brick` | — | measured I |
| Brick core at the front, part lit | read #514b4f; author as the source house's brick | — | measured I |
| Cavities under the frame | read #0d0e11 | — | measured I (a check for the side-by-side, not a texture) |
| Pile overall, voids included | read #4e453f | — | measured I; the houses sheet's read of a part-lit region of the same pile is #5c4f43 |

### Variants and states
- **Cover, from the source preset:** tile (H01: battens 0.33 m, shards heaped at the eaves lines at λ 1 m, a few clusters left at the low eaves); slate (H02, H03: battens 0.5–0.58 m, fragments fanned at λ 4 m, whole and half sheets lying across the frame); sheet metal (H04: sheets torn and folded over the frame, K04).
- **With or without sheet-metal fragments:** `sheet_fragments` 0 (I, the default) or 3–12 K04 pieces over the frame and around it: torn, folded, from the roof itself or from a porch or shed roof nearby.
- **One wing or a whole house:** `extent` whole (I, 10 × 8 m) or wing (a 4–6 × 4–6 m annex or house end; its body about 6–7 × 5–6 m, its high end at most 2.5–3 m).
- **Collapse pattern (`pattern`):** lean-to (I: one end held up by the standing wall end, the other down on the core); A-frame (the ridge held mid-house by the stove or an interior wall stump at 1.0–1.5 m, both slopes down to the eaves lines); V-shape (the middle down, both ends held at 1.5–3 m); pancake (nothing standing: the frame flat on the core, 1.0–1.5 m high, below the asset list's 3–4 m, so not the default).
- **Wall material:** brick (stepped wall end, brick core, K01 and K06) or adobe (a crumbled wall end, adobe blocks and clay in the core, #7d6146).
- **States:** none; the pile is the end state past a house's X. It has no `damage` dial: its pose comes from the collapse parameters. Weathering: fresh (pale breaks, dust) or settled (grey timber, weeds through the core); `burnt` on either.

### Damage pattern
How a roof comes down as one heap, in order (the houses sheet's failure order, with the collapse patterns above):
1. **The cover goes first:** slate shatters and tiles slide, so most of it lands around the house, not in the pile.
2. **The walls fail** out of plane or lose their heads; the wall plates lose their bearing and **the frame drops as a unit**. A nailed lattice of rafters, battens and collar ties is stiff in its own plane, so it holds together.
3. **Where something still stands, the frame hangs on it:** a wall end gives a lean-to (I); the stove or an interior wall gives an A-frame; the end walls give a V; with nothing standing it pancakes.
4. **On impact:** the rafter feet slide out on the rubble (`rafter_spread`); rafters snap at mid-span or at the collar with long splinters (K03); battens break in 30–60 % of the spans; broken members spring up out of the heap (I).
5. **Under it:** the ceiling comes down first or with it, the fill spills, and the contents are crushed into pockets that become the voids (the pancake pattern's pockets).
6. **The walls' brick** lands in and out: the core, with lintels broken and their bars out (K06).
- **What lies around:** the debris kit's spread from the house's walls, exp(−d / λ) per class (tile shards 1 m, bricks 1.5 m, timber 2 m, slate 4 m, sheet 6 m), 1.5–2 times wider on the side away from the burst, coloured from the house's own materials. Cables from the house's services hang over the frame (K10, L05).

### Physics role
- **Collision** (±2 cm on landable faces and around gap polygons, ±5 cm elsewhere):
  - **the core:** 4–8 oriented boxes following its slopes (a few convex primitives), with the voids left open (U02's rule, never filled); pieces of 0.3 m and larger (K01 clusters, K06 lintels, the stove stump) are their own boxes; small pieces use the kit's own collision;
  - **the standing wall end:** 1–3 boxes, stepped per 1–3 courses at its broken top (U01's breach rule);
  - **the frame's members as capsules with their real section:** a 100 × 150 mm rafter r 0.065 m (within ±15 mm of its faces); a round rafter its own radius, 0.05–0.08 m; a 50 × 50 mm batten r 0.028 m; a 150 × 150 mm plate r 0.085 m; a broken member 1–3 segments; splinters 10 mm thick and over as capsules (K03). Capsules are the cheap, robust primitive for many crossing long members. The visual stays sawn and square.
  - **boards and board panels:** thin boxes (K03: a capsule cannot hold a 150 mm wide board);
  - **cover remnants:** thin boxes over the wave envelope (slate, K02; sheet, K04), tile clusters as thin boxes in rows (H01);
  - **contents** (K08) and **cables** (K10) with the kit's own shapes;
  - **budget (derived):** 20 rafters × 1–3 segments plus 20–60 batten pieces, the plates, ridge, collar ties and exposed joists give 60–150 capsules and 20–60 boxes, below a tree's 150–300 capsules (vegetation sheet).
- **Material ids:** frame, boards, ceiling joists: `timber`; core, wall end, tile remnants: `masonry` (whether the core's top faces carry `surface:rubble` is the physics engineer's call, as U02); slate: `proposed:asbestos_cement` (fallback `masonry`, as H02); sheet remnants and fragments: `sheet_metal`; lintel pieces: `concrete` with `steel` bars; cables: `cable`; contents per K08 (`timber`, `steel`, `proposed:fabric`, `proposed:plastic`). The ceiling fill is part of the core boxes (`masonry` until `soil` exists, the houses and earthworks proposal).
- **Gaps** (each written as the real clear polygon; the drone as in "The drone against an opening"):

  | Opening | Narrowest clear width | Passes? |
  |---|---|---|
  | Rafter bay in a frame plane, battens gone | 0.74–1.1 m (rafters at 0.9–1.2 m less 0.10–0.16 m; 0.84–0.90 m at H03's 1.0 m) | in plan any yaw (≥ 0.706 m); it opens into the cavity under the plane, 0.3–2 m deep: **fly-in, no through-route** |
  | Rafter bay with its battens in place | tile 0.25–0.35 m; slate 0.45–0.70 m (0.45 m at H03's 0.5 m) | **no** for tile and for slate below 0.574 m; slate battens at 0.65–0.75 m (0.60–0.70 m clear): nose-first within ±5° to ±35°, 13–63 mm a side (U01's rule) |
  | Openings between crossed members | 0.1–0.6 m (I: 0.2–0.6 m) | **no** below 0.574 m; at 0.574–0.6 m nose-first only (±5° at 0.6 m, 13 mm a side), into the tangle (fly-in). Those of 0.5 m and over are written as polygons (the clear outline through the members' depth, the buildability review's convention 3; the vegetation sheet's 0.5 m threshold); smaller ones are left to collision |
  | Lean-to mouth at the high end, gable-ended source | a triangle about 6.5 m wide at the core and 2 m high, less the wall end and the members across it (the open triangle's inscribed circle is 1.8 m) | where the polygon admits 0.574 × 0.55 m; **dead-ended** toward the low end. A hip-ended source closes this end with the hip's rafter bays (as above) |
  | Cavity at the frame's edge | 0.5–2 × 0.3–0.9 m (I: about 1.4 × 0.5 m at the front toe) | **no** below 0.55 m high (I's); a mouth 0.6–0.9 m high admits the drone, **fly-in only**, dead-ended 1–3 m in |
  | Through-route under the frame | none in the default heap (I shows none: the far side is the core and the wall end) | the generator writes one only if its cavities happen to join; the gap probe then judges it |

- **Pitfalls (the asset list's "voids"):** open cavities in collision, never written as gaps or tags. A leg on the frame drops between two members to the next member or the core, 0.1–0.9 m lower (derived from the frame's depth and the cavities). A leg on the core drops 0.2–0.8 m into a 0.3–1.0 m void, as U02.
- **Wind volumes:** the frame as 1–3 porous volumes following its envelope, porosity 0.6–0.8 (default 0.7). Derived: one battened plane is 0.77 open (battens 50 mm in 0.33 m block 15 %, rafters 0.10 m in 1.0 m block 10 %, 0.85 × 0.90), two crossing layers 0.59, and with most battens gone about 0.85. Cover clusters and sheet pieces of 1 m² and more are solid parts (K04's rule). The core and the wall end are solid (0). The flow through and under the frame is the solver's result from these volumes, not a tag (the buildability review's convention 5).
- **Snag hazards** (the asset list's rafters, battens and sheet; records as the review's convention 4, never "the whole pile"):
  - an `edge` along each exposed member: rafters, battens, plates, collar ties and joists, at the timber arris (2 mm) for sawn members or their own radius for round ones. Exposed means at least 30 % of its length is visible from outside the heap (the generator's ray test from above and the sides), so buried members carry none;
  - a `hook` from base to tip on every free end pointing out of the heap: tips up, rafter tails, splinters (radius half their thickness, K03), nails 20–80 mm proud on exposed members (1.75 mm, K03);
  - sheet metal: an `edge` along every cut and torn edge at 0.3 mm, **which cuts the fiber** (K04); petals and torn corners are `hook`s;
  - slate rims are `edge`s at 3 mm and tile rims at 2 mm (K02); the wall end's broken top and steps are `edge`s at 2 mm (K01 clusters); hanging cables per K10 and L05;
  - **budget (derived):** 40–120 `edge` and 10–40 `hook` records.
- **Landable surfaces:**
  - **no:** members, battened planes and tips. Members are 0.05–0.16 m wide, and the 0.24 m leg square needs at least 0.35 × 0.35 m within 15° (the debris kit's rule). Also no: the wall end's top (0.38 m wide, but broken and rounded in I).
  - **yes, set from the resting pose by the kit's rule:** a deck of 0.35 × 0.35 m or more within 15°, made by a board panel, a run of ceiling boards, slate sheets on battens or a flat sheet piece. It is uneven and springy, and slate may crack (a later material property). Such decks occur at a lean-to's low end and on a pancake. In I the low end reads as a flatter layer of boards 1.2–1.6 m up; whether it is a continuous deck is not resolved at this size.
  - **yes, with tilt:** the core's flat faces, as U02.

### Placement rules (for the world)
- **On the plot and footprint of the house whose roof it was.** That plot's H-row instance is replaced by a U02b with `source` set to the row's preset, so its size, cover and brick match its neighbours. The yard set stays: fences, gate, sheds, cellar, trees.
- **Orientation:** as the source house, long axis along the street (the houses rule). A lean-to's high end is at the standing wall end. The scatter is wider on the side away from `burst_dir`.
- **In the I composition:** centre of the view at 22–26 m from the viewpoint, its high end and wall end toward the two-storey house (H06) and about 3–6 m from it, the lattice-topped fence (Y04) in front of its left half, the block (U01) 60–80 m behind it (measured I, rough).
- **In the villages:** on X plots among D and X houses. How often a destroyed plot shows a pile rather than an H-row in X is the world-builder's layout call; one in three to five is assumed until G3's world review.

### Generator parameters
**A new, thin `roof_pile` generator, not a U02 variant and not a house-generator state.**
- **Why not U02:** U02 builds a mound of loose pieces with no linked structure. The pile's defining part is the frame: rafters still joined at the ridge, battens nailed on, an end hanging on a wall. That is the house generator's carpentry.
- **Why not the house's X state:** the damage dial edits a standing house (walls cut to 0.5–1.5 m, rafters across them). The pile is the next step, where the walls are down and the roof came down as a unit, so it needs a collapse pose the dial does not have. It also has its own ID, catalog entry, review sheet and place in the I composition.
- **So `roof_pile` composes, and builds nothing new but the collapse:**
  1. the house generator's roof and wall builders at the source preset's params, so timber sizes, pitches, covers and materials match H01–H04 exactly;
  2. a deterministic, seeded collapse transform (no physics simulation): support, spread, breaks, tips;
  3. U02's core builder for the masonry core and its voids;
  4. the K kit for the loose pieces and their exp(−d / λ) scatter.
- Every random choice comes from `seed`; the same params give the same heap.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `source` | preset id | H01–H04 presets | H03, `wall_material` red_brick (I) | Loads the source house's `length`, `width`, `wall_height`, `wall_thickness`, `wall_material`, `roof_type_*`, `roof_pitch`, `roof_cover`, `rafter_pitch`, `batten_pitch`, `eaves_overhang`; any can be overridden here |
| `extent` | enum | whole, wing | whole | wing builds the roof of a 4–6 × 4–6 m annex or house end only |
| `wing_length` | m | 4–6 | 5 | The wing's length along the ridge when `extent` is wing |
| `pattern` | enum | lean_to, a_frame, v_shape, pancake | lean_to | What holds the frame up: one end on a standing wall end (I), the middle on the stove or an interior wall stump, both ends with the middle down, or nothing. Sets the pose and where the large voids are |
| `standing_wall_ends` | int | 0–2 | 1 | Wall ends left standing (gable or corner piers); lean_to needs 1, v_shape 2 |
| `standing_wall_len` | m | 1–3 | 2.0 | Length of each standing end |
| `standing_wall_h` | × wall height plus plinth | 0.6–1.0 | 0.9 | Its height (3.0 m for H03); the lean-to's high end rests on it; the top is broken, stepped over 1–3 courses |
| `mid_support_h` | m | 0.8–1.5 | 1.2 | Height of the stove or interior-wall stump under the ridge in a_frame; ignored otherwise |
| `wall_stub_h` | m | 0–1.5 | 0.5 | Height left of the other wall lines |
| `fall_bias` | −1–1 | −1 to 1 | 0.2 | Share of the walls' brick falling outboard (+1) or inboard (−1): outboard widens the toe, inboard fills under the frame |
| `rubble_volume` | m³ | 10–120 | removed solid × 1.86 (40 for H03) | The masonry core's volume, laid by U02's core builder (the houses rule) |
| `ceiling` | enum | boards_clay, boards_slag, none | boards_clay | The ceiling layer under the frame: joists, boards and a 0.15–0.25 m fill as a crumbly layer |
| `rafter_spread` | m | 0–1.0 | 0.3 | How far the rafter feet slide out past the eaves line where nothing holds the ridge up (a lean-to's low end, the middle of a V, a pancake): the ridge then sits √(L² − (b + spread)²) above the feet, L the rafter from ridge to eaves line and b its run (3.86 and 3.5 m for H03), never below the core under it. Where a support holds the ridge, the feet hang to the core instead and the run follows as √(L² − rise²) (the 35° flank at I's high end) |
| `ridge_breaks` | int | 0–3 | 1 | Breaks along the ridge; v_shape needs at least 1 mid-length |
| `rafters_broken` | 0–1 | 0–0.8 | 0.4 | Fraction of rafters snapped at mid-span, at the collar or at the foot, with K03 splinter ends |
| `tips_up` | int | 0–8 | 4 | Broken members springing up 30–70° out of the frame, 0.3–1.2 m above it (I) |
| `batten_keep` | 0–1 | 0–0.8 | 0.4 | Fraction of batten spans still nailed; the broken ones irregular, never a grid |
| `cover_keep` | 0–1 | 0–0.5 | 0.05 | Fraction of the cover still on the battens, clustered at the low eaves; the rest goes to the scatter |
| `sheet_fragments` | int | 0–12 | 0 | K04 pieces folded over the frame and lying around it |
| `cavities` | int | 0–4 | 2 | Cavity mouths kept open under the frame's edges (I: one at the front toe) |
| `cavity_size` | {w, h, depth} m | 0.5–2, 0.3–0.9, 1–3 | 1.4, 0.5, 1.5 | Cavity mouth and depth; a mouth over 0.55 m high becomes a fly-in gap |
| `void_density`, `void_size` | /m², m | 0–0.25, 0.3–1.0 | 0.12, 0.6 | Pitfall voids in the core, as U02 |
| `contents` | 0–1 | 0–1 | 0.3 | K08 items under and in the heap; each props a pocket |
| `hanging_cables` | int | 0–3 | 1 | Service cables over the frame (K10, L05) |
| `burst_dir` | deg | 0–360 | seeded | The side the scatter favours, 1.5–2 times wider away from the burst (the debris kit) |
| `burnt` | 0–1 | 0–1 | 0 | Char on the frame; at 1 the members are burnt to short pieces 0.2–1.5 m with an ash layer on the core (houses step 7) |
| `weathering` | 0–1 | 0–1 | 0.3 | Fresh pale breaks and dust (0), or grey timber and weeds through the core (1) |
| `seed` | int | — | — | Every random choice above |

### Open points
- **Size from I depends on the assumed lens and horizon** (±15 %). Settled at the I side-by-side: the pile's silhouette against the block and its height against the two-storey house are the check. Not a pilot question.
- **The cover:** I shows almost none on the pile, and the grey-green pieces are not resolved (slate or painted sheet). The default H03 slate with `cover_keep` 0.05 matches the look; settled at the side-by-side.
- **Capsules for the frame against K03's boxes for loose sawn pieces:** the frame uses capsules (this row's choice, within AST-05's tolerance). The physics engineer confirms that a leg on a capsule rafter, which slides off where a flat face would hold, is acceptable for a heap that is not landable anyway.
- **Ids:** `proposed:asbestos_cement`, `fabric`, `plastic`, `soil`, and `surface:rubble` on the core's top faces, all already proposed (the buildability review's list).
- **Static in phase 1:** members that would roll or shift under a leg are static catalog geometry, as K03. Runtime objects come later (the physics engineer and the world-builder).
- **How often a destroyed plot shows a pile:** the world-builder's layout call (assumed one in three to five).

## U03 Row of garages (cooperative), damaged

**Seen in:** R (research only; the pilot confirms at G2).
**Research sources:** cooperative garage blocks; inspection pits (research table); the houses sheet's O06 (a single garage box) and slab sizes.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Units | 4–8 | 6 | assumed (asset list, R: research only) |
| Box, outside | 3.5 × 6 m | 3.5 × 6.0 m | assumed (asset list, R: research only) |
| Box, inside | 3.2–3.3 × 5.5–5.8 m | 3.25 × 5.6 m | derived: party walls 0.25 m, front and back 0.25–0.38 m |
| Inside height | 2.4–2.9 m | 2.6 m | assumed (O06 2.4 m for a metal box; brick rows taller) |
| Party walls | 0.25 m (one brick) or 0.19–0.20 m block | 0.25 m | assumed |
| Roof | hollow-core slabs 0.22 m spanning the 3.25 m between party walls, felt on top, a fall of 1–3 % to the back, a 0.2–0.4 m parapet at the front | 5 slabs 1.2 m wide per box | published slab section (GOST 9561); layout assumed |
| Gates | steel double leaf, 2.4–2.7 × 2.0–2.2 m | 2.5 × 2.1 m | assumed (matches O06) |
| Wicket in one leaf | 0.6–0.7 × 1.6–1.8 m | 0.65 × 1.7 m | assumed |
| Inspection pit | 0.75–0.80 × 1.7–1.8 m deep × 2.0–4.0 m | 0.78 × 1.75 × 3.0 m | published |
| Gate lintel | steel angle or precast lintel over the gate | 0.25 m | assumed |

### Construction and materials
- A single-storey row of brick or block boxes on a strip foundation, party walls carrying slab roofs, felt roofing with a parapet and drip at the front, steel gates of 2–3 mm sheet on an angle frame with strap hinges welded to a steel jamb frame, a padlock hasp; a concrete floor with the pit, its walls brick or concrete with a steel angle edge; sometimes a cellar under the pit (assumed).
- **Materials:** brick manifest `red_brick` or Poly Haven `white_bricks` ( https://polyhaven.com/a/white_bricks ); block `rough_block_wall`; slabs `concrete_wall_003`; felt Poly Haven `bitumen` ( https://polyhaven.com/a/bitumen ); gates `painted_metal_shutter`, `rusty_painted_metal`, `rusty_metal_sheet` ( https://polyhaven.com/a/painted_metal_shutter ); floor `worn_concrete_floor` ( https://polyhaven.com/a/worn_concrete_floor ).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Gates, faded paint | grey-green, brown or blue-grey about #5a6a5e, rust #6b3f2a | — | assumed (as O06) |
| Brick | as `red_brick` or silicate #a0968b | — | houses sheet H03 |
| Felt | near-black #2b2a28, weathered grey | — | assumed |

### Variants and states
- Variants: brick row (default) or block row; 4, 6 or 8 boxes; gates painted per box (owners differ).
- **I:** gates shut and padlocked, one or two open, rust at the foot, felt patched.
- **D (the listed state):** 2–4 gates blown in or out (bent 20–90° or off), one box with a slab fallen in (one end dropped, hanging into the pit), one box burnt out with soot above its gate lintel and a burnt car inside (an R row in its F state), a party wall breached 0.6–1.5 m, felt peeled and flapping, glass and gate fragments in front.

### Damage pattern
- Gates fail first (thin sheet: bowed, torn at the hinges, curled edges); slabs crack and drop where a party wall is breached (hollow-core, no top steel: they drop rather than hang, held only at the joint anchors, assumed); brick breaches stepped as the houses sheet.

### Physics role
- **Collision:** walls boxes; slabs boxes per slab; gates thin boxes at their angles; the pit as a cavity in the floor (walls and floor boxes), its steel edge a box; fallen slab an oriented box; car per its R row.
- **Material ids:** walls `masonry`; slabs, floor, pit walls `concrete` (the felt is a few millimetres on the slab and takes the slab's id); gates, frames `sheet_metal` (torn edges cut fiber), jamb frames and pit edge `steel`.
- **Gaps:**
  - both leaves open, 2.5 × 2.1 m: any yaw;
  - one leaf open, 1.2 m clear after the frame: any yaw;
  - the wicket 0.65 × 1.7 m: nose-first within ±16°, 38 mm a side;
  - **the pit, 0.78 m wide: any yaw, 37 mm a side on the diagonal**, a vertical fly-down to 1.75 m and a pitfall for the legs;
  - a fallen slab's gap 1.2 × 3.25 m: any yaw (the box entered from above);
  - inside a box, 3.25 × 5.6 × 2.6 m: the drone can turn.
- **Wind volumes:** the row solid (0); each open box a cavity through its gate.
- **Snag hazards:** torn gate edges, hinge pins, the hasp, felt flaps, rebar at a broken slab end, cables.
- **Landable surfaces:** **the roof**: flat, hard, felt on slabs, 6 × 21 m for six boxes at 2.9–3.2 m; the best large landing pad at the urban edge. Sagging over a cracked slab: not.

### Placement rules
- At the urban edge within 50–150 m of U01, the gates facing a shared drive 6–8 m wide; one row per map, occasionally two facing each other across the drive (assumed from the class).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `units` | int | 4–8 | 6 | Boxes in the row |
| `box_width`, `box_length` | m | 3.2–4.0, 5.5–7.0 | 3.5, 6.0 | Box size |
| `height` | m | 2.4–2.9 | 2.6 | Inside height |
| `wall_material` | enum | brick, block | brick | Texture set, breach style |
| `gate_size` | m | 2.4–2.7 × 2.0–2.2 | 2.5 × 2.1 | Gate opening |
| `gate_states` | list | per unit: shut, one_open, both_open, bent, off | 2 shut, 1 one_open, 2 bent, 1 off | Gate leaves and the clear widths |
| `pit` | bool per unit | — | on | Inspection pit |
| `fallen_slabs` | int | 0–3 | 1 | Slabs dropped into a box |
| `burnt_units` | int | 0–2 | 1 | Burnt box, soot and a burnt car |
| `wall_breaches` | int | 0–2 | 1 | Party-wall breaches |
| `damage` | 0–1 | — | 0 / 0.6 | I / D |
| `seed` | int | — | — | Random choices |

### Open points
- Garage contents beyond a car (tyres, shelves, canisters) come from K09; their density is a world-pass choice.

## P01 Village shop (small brick, invented sign)

**Seen in:** R.
**Research sources:** the houses sheet's brick construction; the class is a common single-storey village shop (assumed from the class); the sign is invented (SEC-03).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Footprint | 8–14 × 6–10 m | 10 × 8 m | assumed |
| Wall height | 3.0–3.6 m | 3.3 m | assumed (a commercial storey) |
| Walls | 0.38–0.51 m brick | 0.38 m | derived, as the houses sheet (1.5 or 2 bricks) |
| Roof | flat slab with felt and a parapet, or gable slate | flat | assumed |
| Display windows | 1.5–2.4 × 1.4–1.6 m, sill 0.8 m, 1–3 on the front | 2 × (2.0 × 1.5 m) | assumed |
| Window grilles | steel bars Ø 12–16 at 0.12–0.15 m | on | assumed |
| Door | 0.9 × 2.0 m opening, steel-clad leaf, porch of 2–3 steps with a canopy | — | assumed |
| Back door | 0.9 × 2.0 m to the store room | — | assumed |
| Sign board | 3–5 × 0.6–0.8 m over the door | 4 × 0.7 m | assumed |

### Construction and materials
- Silicate or red brick, rendered and painted (pale blue, green or cream) or bare; precast lintels; a slab roof with felt; a sales room 6–8 × 5–6 m with counters and wall shelving, a store room behind; steel grilles on the windows; a painted steel sign board with a generic class word or an invented name, chosen by the map's sign table (SEC-03).
- **Materials:** brick as H03/H04; render `plastered_wall_02`; slab and steps `concrete_wall_003`; felt `bitumen`; door and grilles `painted_metal_shutter`, `rusty_metal_02`; shelving `Chipboard001`; sign board `rusty_painted_metal` with a derived lettering map.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Painted render | pale blue about #a9bcc4, pale green about #b5c2a5 or cream about #d6cdb8 | — | assumed (the U01 render for cream) |
| Sign board | faded paint with rust | — | assumed |

### Variants and states
- Variants: 1 flat roof rendered; 2 gable slate roof, bare brick.
- **I:** windows grilled, glass partly gone, the door shut, the sign faded.
- **D:** glass gone, a grille torn out, the door blown in, the roof slab holed 0.5–1.5 m, shelving tipped, goods and packaging strewn inside and in front.
- **X:** front wall down to 0.5–1.5 m, the roof slabs fallen into the sales room, rubble 1–2 m.

### Damage pattern
- As the houses sheet for brick (stepped breaches, lintel bars out); slabs drop where a wall goes. Rubble: brick, render, slab chunks with bars (K06), shelving, packaging.

### Physics role
- **Collision:** walls boxes per segment, slabs boxes, grilles capsules, counters and shelving boxes.
- **Material ids:** walls `masonry`; slabs, steps, lintels `concrete`; grilles, door, sign frame `steel`; door skin and sign board `sheet_metal`; shelving `timber`; glass `glass`; packaging `proposed:plastic`.
- **Gaps:** door 0.8 m clear: any yaw, 47 mm a side on the diagonal; display window with its grille: **no** (bars at 0.12–0.15 m); grille torn out: 2.0 m, any yaw; back door as the front; roof hole ≥ 0.6 m after the mesh: passes.
- **Wind volumes:** solid; the sales room a cavity once windows and door are out.
- **Snag hazards:** grille bars, the sign board's edges and brackets, roof-hole bars, hanging cables.
- **Landable surfaces:** the flat roof (10 × 8 m at 3.3 m): hard, good; the porch canopy.

### Placement rules
- One per village, in the centre on the main street, the front to the street 3–10 m back behind a trampled or paved forecourt, near the bus stop (P04).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `length`, `width`, `height` | m | 8–14, 6–10, 3.0–3.6 | 10, 8, 3.3 | Size |
| `roof_type` | enum | flat, gable | flat | Roof form and cover |
| `finish` | enum | render, bare | render | Wall finish and colour |
| `display_windows` | int | 1–3 | 2 | Windows on the front |
| `grilles` | 0–1 | 0–1 | 1 | Fraction of windows grilled |
| `sign_text` | id | invented list | from the map's sign table | Lettering on the board (never free text, SEC-03) |
| `damage` | 0–1 | — | 0 / 0.55 / 1.0 | I / D / X |
| `seed` | int | — | — | Random choices |

### Open points
- none.

## P02 Farm complex: long brick cowshed with asbestos roof, partly collapsed

**Seen in:** R.
**Research sources:** the houses sheet (asbestos-cement sheets, blast fracture); the class sizes are assumed from the common Soviet collective-farm cowshed (a 6 m structural bay is the usual module, assumed); no typical-project figures were found in a public source.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Span (width) | 12–21 m | 18 m | assumed |
| Length | 60–96 m in 6 m bays | 72 m (12 bays) | assumed |
| Wall height to the eaves | 2.6–3.3 m | 3.0 m | assumed |
| Walls | brick 0.38 m with pilasters 0.51 × 0.51 m at 6 m | — | assumed |
| Roof | precast RC beams or timber trusses at 6 m, purlins at 1.0–1.5 m, corrugated asbestos-cement sheets | pitch 20°, ridge 6.3 m | assumed; ridge derived: 3.0 + 9 × tan 20° |
| Windows | a band of 1.2–1.5 × 0.9–1.2 m at sill 1.6–1.8 m, 1–2 per bay per side, steel or timber frames of small panes | 1.2 × 1.0 m, 2 per bay | assumed |
| End gates | 3.0 × 3.0 m (tractor access), sliding or swing | 3.0 × 3.0 m | assumed |
| Side doors | 1.2–1.5 × 2.1–2.2 m | 1.2 × 2.1 m, 2 per side | assumed |
| Ridge vents | timber boxes 0.6 × 0.6 m | every 12 m | assumed |
| Floor | concrete, a feed alley 2.0–3.6 m down the middle, stalls each side, manure channels 0.3–0.6 wide × 0.2–0.4 m deep | — | assumed |

### Construction and materials
- Brick walls with pilasters, a concrete floor with channels, steel-tube stall partitions and feed barriers (pipe 50–60 mm), precast beams or timber trusses, timber purlins, corrugated asbestos-cement sheets (houses sheet: 1750 × 1130 × 5.8 mm, 8 waves; larger farm profiles exist), plastered and whitewashed inside to 1.5 m, gates of planks on a steel frame.
- **Materials:** brick `red_brick` or `white_bricks`; slate as the houses sheet H02 (`concrete_wall_007` or `Concrete034` on a true wave mesh); beams `concrete_wall_003`; timber `rough_wood`, gates `weathered_brown_planks` ( https://polyhaven.com/a/weathered_brown_planks ); stall pipes `rusty_metal_02`; floor `worn_concrete_floor`; whitewash `white_stucco_02`.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Slate roof, weathered | #736767 | — | houses sheet (J) |
| Red brick | as `red_brick` | — | manifest |
| Inner whitewash, dirty | about #b8b2a4 | — | assumed |

### Variants and states
- Variants: span and length; trusses timber or concrete.
- **D:** 30–60 % of the roof sheets gone in patches with shards on the floor, 1–3 bays' purlins sagging, gates gone or hanging, all windows out, one wall breach 1–3 m.
- **X (partly collapsed, the asset-list wording):** 3–6 contiguous bays with the roof down inside (trusses or beams fallen to the floor, sheets in heaps), a gable end down to 1–2 m, the long walls broken to 0.5–1.5 m along those bays; the rest of the shed as D.

### Damage pattern
- Asbestos-cement shatters first (published: readily fractured by blast, houses sheet) into 0.1–0.6 m fragments; purlins break and drop; trusses go when a pilaster goes; brick breaches stepped. Rubble: grey-white sheet fragments (the dominant colour), brick, timber, bent stall pipes.

### Physics role
- **Collision:** walls boxes per bay, pilasters boxes, beams or truss members boxes, purlins boxes, sheets thin boxes per sheet (shards as debris), stall pipes capsules, channels as slots in the floor.
- **Material ids:** walls `masonry`; beams, floor `concrete`; trusses, purlins, gates `timber`; sheets `proposed:asbestos_cement`; stall pipes `steel`.
- **Gaps:** end gates 3 × 3 m: any yaw; side doors 1.2 m: any yaw; windows with their small-pane frames: **no** (panes about 0.4 m); frames gone 1.2 × 1.0 m: any yaw; roof bays between purlins with sheets gone, 0.85–1.4 m clear: any yaw; collapsed bays: open. Inside: a hall 18 m wide and 3–6 m high, the longest indoor flight on the map, crossed by stall pipes at 1.0–1.3 m.
- **Wind volumes:** walls 0 with the window band about 0.13 (derived: two 1.2 × 1.0 m windows in a 6 × 3 m bay); the stripped roof 0.6–0.9; open gates make the hall a channel along its length.
- **Snag hazards:** hanging sheets and sheet edges, purlin ends, stall pipes and chains, cables, plank gates.
- **Landable surfaces:** the concrete floor (clear between stalls); roof sheets: **no** (they crack: `asbestos_cement` breakability, open point); fallen beams' tops: hard, narrow.
- **Channels:** the manure channels 0.3–0.6 m wide are leg traps (a pitfall 0.2–0.4 m deep).

### Placement rules
- One farm complex per map, 200–800 m outside one village on a track: 2–4 sheds parallel 20–40 m apart, with the water tower (P03), a transformer (P05), a few vehicles (R rows) and weeds (V17) between.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `span` | m | 12–21 | 18 | Width |
| `bays` | int | 10–16 | 12 | Length in 6 m bays |
| `eaves_height` | m | 2.6–3.3 | 3.0 | Wall height |
| `roof_pitch` | deg | 15–25 | 20 | Ridge height |
| `truss_type` | enum | concrete_beam, timber_truss | concrete_beam | Roof structure and failure |
| `purlin_pitch` | m | 1.0–1.5 | 1.25 | Roof bay width once sheets are gone |
| `windows_per_bay` | int | 1–2 | 2 | Window band |
| `roof_strip` | 0–1 | 0–1 | D 0.45 | Fraction of sheets gone, patchy |
| `collapsed_bays` | (start, count) | 0–6 bays | X: 4 bays | The collapsed run |
| `gable_down` | 0–1 | 0–1 | X 0.7 | One gable's missing fraction |
| `stalls` | bool | — | on | Steel stalls and barriers |
| `damage` | 0–1 | — | 0.55 / 1.0 | D / X |
| `seed` | int | — | — | Random choices |

### Open points
- The span, length and wall height are assumed from the class; settled by the asset-artist against public photographs of the class at G2, not by the pilot.

## P03 Water tower (Rozhnovsky type, steel tank on a column)

**Seen in:** R.
**Research sources:** the Rozhnovsky rows in the research table.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Tank volume | 15–50 m³ | 25 m³ | published (standard sizes 15, 25, 50) |
| Support (column) height | 12–18 m | 15 m | published (12, 15, 18) |
| Column diameter | 1.02–1.22 m | 1.22 m | published (a 15 m³ tower's support pipe) |
| Tank diameter | 3.0–4.0 m | 3.0 m | assumed |
| Tank height | 2.1–7.1 m | 3.5 m | derived: volume / (π × r²), 25 / 7.07 |
| Transition | a cone from the column to the tank | 1.0 m tall | assumed |
| Roof | a shallow cone 15–20°, an inspection hatch 0.6 × 0.6 m | — | published (lid with hatch); pitch assumed |
| Overall height | 9–25 m | 19.5 m | published range; derived: 15 + 1.0 + 3.5 |
| Ladder | external, with a safety cage from 2.5–3 m up, hoops Ø 0.7 m at 0.8–1.0 m, 5 straps | — | published (ladder with a guard); sizes assumed |
| Base | a concrete pad 3 × 3 × 0.5 m, a base plate 10 mm | — | published (plate); pad assumed |

### Construction and materials
- Welded steel plate (6–10 mm, assumed) column and tank, painted (silver-grey, light blue or green, faded), rusting at the seams and the foot; a valve chamber or pipe into the ground at the base; a ladder of steel flat and round bar with its cage; a railing round the hatch on taller towers (a platform only from 20 m, published).
- **Materials:** Poly Haven `rusty_painted_metal`, `rust_coarse_01`, ambientCG `PaintedMetal006` ( https://polyhaven.com/a/rust_coarse_01 , https://ambientcg.com/view?id=PaintedMetal006 ); pad `concrete_wall_003`; a derived rust-streak mask down from every seam and hole.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Paint, faded | silver-grey about #9a9c98, light blue about #8fa5b0 | — | assumed |
| Rust | #6b3f2a to #8a5232 | — | assumed (as O06) |

### Variants and states
- Variants: 15, 25 or 50 m³ on a 12, 15 or 18 m column.
- **I:** paint faded and flaking, rust streaks, the hatch shut.
- **D:** 10–40 fragment holes 20–100 mm in the tank and column (petalled, rust-haloed), one hole 0.3–0.8 m with curled edges, the ladder cage bent at one level, the hatch lid off, a dried water-stain streak below each hole.

### Damage pattern
- Steel holes and petals rather than collapsing; a large hole curls the plate; the ladder bends where struck. No rubble; a few torn plate pieces and cage straps at the foot.

### Physics role
- **Collision:** column a cylinder, cone and tank cylinders and a cone, the ladder as capsules (rails and rungs), the cage hoops and straps as capsules, the hatch a box, the pad a box.
- **Material ids:** shell, ladder, cage `steel` (hole petals `sheet_metal`, they cut fiber); pad `concrete`.
- **Gaps:** none to fly through (the cage's openings between straps are 0.3–0.5 m: **no**).
- **Wind volumes:** solid cylinders; vortex shedding at f = 0.2·U/D (published St): 0.33–1.0 Hz behind the 3.0 m tank and 0.8–2.5 Hz behind the 1.22 m column for 5–15 m/s (derived).
- **Snag hazards:** the ladder and its cage, the hatch railing, hole petals.
- **Landable surfaces:** none (the roof cone is 15–20°, 3 m across, with a hatch and railing). **Landmark at night:** its silhouette is one of the few the L view reads (terrain.md, night); its LODs keep the outline.

### Placement rules
- At the farm complex (P02) or on the highest ground of a village, 1–3 per map; a track to its base.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `volume` | m³ | 15, 25, 50 | 25 | Tank height (with the diameter) |
| `column_height` | m | 12, 15, 18 | 15 | Support height |
| `column_d`, `tank_d` | m | 1.02–1.22, 3.0–4.0 | 1.22, 3.0 | Diameters |
| `paint` | sRGB | — | silver-grey | Paint tint |
| `holes` | int | 0–40 | I 0, D 20 | Fragment holes (≥ 30 mm geometry, smaller decals) |
| `big_hole` | m | 0–0.8 | D 0.5 | One large curled hole |
| `cage_damage` | 0–1 | 0–1 | D 0.3 | Bent cage section |
| `damage` | 0–1 | — | 0 / 0.5 | I / D |
| `seed` | int | — | — | Random choices |

### Open points
- Tank diameter is assumed (3.0–4.0 m); a 50 m³ tank on 3.0 m would be 7 m tall, so 50 m³ defaults to 4.0 m (4.0 m tall). Settled by the asset-artist from public photographs of the class.

## P04 Bus stop (Soviet concrete shelter)

**Seen in:** R.
**Research sources:** Soviet bus stops (research table: standard or local designs, often commissioned by collective farms, open-fronted concrete boxes).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Length | 3.0–6.0 m | 4.0 m | assumed |
| Depth | 1.5–2.2 m | 1.8 m | assumed |
| Clear height | 2.3–2.8 m | 2.5 m | assumed |
| Walls | precast or cast concrete 0.12–0.20 m, or brick 0.25 m rendered | 0.15 m concrete | assumed |
| Roof slab | 0.15–0.20 m, overhanging 0.3–0.6 m at the front | 0.18 m, 0.5 m overhang | assumed |
| Bench | concrete or timber slats on steel, seat 0.45 m high, 0.35–0.4 m deep | timber slats | assumed |

### Construction and materials
- Three walls (back and two sides) and a roof slab, the front open; often a decorative panel (mosaic, relief or painted) on the side or back; a concrete plinth or a few steps; on rural roads a small paved lay-by. Variant 2 a curved-shell or sculpted form (the class's variety, published).
- **Materials:** `concrete_wall_003`, `concrete_wall_007`; a derived mosaic or painted panel map (assumed); bench `weathered_brown_planks` and `rusty_metal_02`.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Concrete, weathered | about #9a978f | — | the earthworks sheet's S12 slab target |
| Paint or mosaic panel | faded blue, ochre, red | — | assumed |

### Variants and states
- Variants: 1 box shelter with a flat slab; 2 a shelter with a curved roof shell.
- **I:** weathered, graffiti-free (no text), moss at the roof edge, litter.
- **D:** fragment pocks (decals under 30 mm, geometry from 30 mm), a side wall holed 0.3–0.8 m with its mesh and bars out, the roof slab cracked and sagging 5–15° at one end, the bench broken.

### Damage pattern
- Concrete spalls and cracks, bars exposed (K06 pieces at the foot), the slab hinges at a crack.

### Physics role
- **Collision:** walls and slab boxes, bench boxes, bars capsules.
- **Material ids:** `concrete`; bars `steel`; bench `timber`.
- **Gaps:** the open front (4.0 × 2.5 m): any yaw; a wall hole ≥ 0.6 m after the mesh: passes.
- **Wind volumes:** walls and slab solid; the inside a sheltered cavity open to the front.
- **Snag hazards:** bars at broken edges and holes.
- **Landable surfaces:** **the roof slab** (4.0 × 1.8 m at 2.7 m, flat, hard): a good pad; sagging at its cracked end: not.

### Placement rules
- On the main road at each village's entry and centre (1–2 per village), the open side to the road, 1–3 m from the carriageway edge; near the shop (P01) in the centre.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `length`, `depth`, `height` | m | 3–6, 1.5–2.2, 2.3–2.8 | 4.0, 1.8, 2.5 | Size |
| `form` | enum | box, shell | box | Variant |
| `panel` | enum | none, mosaic, painted | mosaic | Decorative panel map (no lettering) |
| `wall_holes` | int | 0–2 | D 1 | Holes with bars |
| `slab_sag` | deg | 0–15 | D 8 | Sag at one end |
| `damage` | 0–1 | — | 0 / 0.5 | I / D |
| `seed` | int | — | — | Random choices |

### Open points
- none.

## P05 Transformer kiosk / pole-mounted transformer

**Seen in:** R.
**Research sources:** pole-mounted substations (research table).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| **Variant 1, pole-mounted:** poles | two RC poles of the 10.5 m class, 8.0–8.5 m above ground | 1.6 m apart | published class; embedment 2.0–2.5 m and spacing assumed |
| Transformer | 25–160 kVA: tank about 1.0 × 0.7 × 1.3 m with cooling tubes, 3 high-voltage bushings on top | 1.0 × 0.7 × 1.3 m | assumed |
| Transformer platform | steel frame at 3.0–4.5 m | 3.5 m | assumed |
| Service platform | 0.8 × 1.6 m steel grating with a rail, at 3.0 m | — | published (has one); size assumed |
| Fuses and arresters | on a crossarm at 6–7 m | 6.5 m | published (has them); height assumed |
| High-voltage conductors | 3 phases on a crossarm, 0.6–0.8 m apart | 0.7 m | assumed |
| Low-voltage board | 0.6 × 0.4 × 0.8 m box at 1.5–2.0 m | 1.8 m | assumed |
| **Variant 2, kiosk:** cabinet | steel, 2.0–2.5 × 1.5–2.0 × 2.2–2.5 m, on a 0.3–0.5 m plinth | 2.2 × 1.8 × 2.4 m | assumed |
| Kiosk entry | a portal or mast on the roof with insulators, 2–3 m above it | — | assumed |

### Construction and materials
- V1: two prestressed concrete poles with steel channel frames and crossarms, the transformer on its frame, fuses, arresters, porcelain or glass insulators, the board with its cables down the pole. V2: a welded steel cabinet with louvres and padlocked doors, an incoming mast or portal, outgoing low-voltage cables.
- **Materials:** poles `concrete_wall_003` (spun-concrete look via the derived map); steel `rusty_painted_metal`, `green_metal_rust` ( https://polyhaven.com/a/green_metal_rust ); transformer `PaintedMetal006` tinted grey; insulators a derived glazed map.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Concrete pole | about #9a978f | — | assumed (as P04) |
| Transformer and cabinet paint | grey about #7d817c, rust #6b3f2a | — | assumed |
| Oil stain (D) | near-black #262320 on the ground and the tank | — | assumed |

### Variants and states
- Variants: 1 pole-mounted, 2 kiosk.
- **I:** paint faded, rust, conductors and cables taut.
- **D:** fragment holes in the tank with an oil stain down it and on the ground, a bushing broken, conductors cut and hanging to the ground (L rows), the board door open; V2 doors blown open, the cabinet holed; one pole of V1 snapped at 1–2 m with the frame tilted (an X-like option at full damage).

### Damage pattern
- Holes and leaks rather than collapse; conductors fall in long sags (PR-6); a snapped pole leaves its bars bent out at the break.

### Physics role
- **Collision:** poles cylinders, frames and crossarms boxes, transformer and board boxes, cooling tubes as one box, insulators and bushings capsules, conductors and cables per the L rows, grating a thin box.
- **Material ids:** poles `concrete`; frames, tank, cabinet `steel` (cabinet skin and louvres `sheet_metal`); conductors and cables `cable`; insulators `masonry` for porcelain and `glass` for glass, as the pole rows L01 and L02 do, until a ceramic id exists.
- **Gaps:** between the two poles under the platform (1.6 × 3.5 m): any yaw; between phase conductors no gap polygon is written (the L01 rule: the wire contacts bound that space), though their 0.7 m spacing admits the drone nose-first within ±35°, 63 mm a side, and every contact is a wire strike (PR-6); kiosk doors open 0.8–1.0 m: any yaw.
- **Wind volumes:** poles and tank solid; the grating 0.6–0.8.
- **Snag hazards:** every conductor and cable (the main hazard), crossarms, fuse holders, the grating rail, a snapped pole's bars.
- **Landable surfaces:** the kiosk roof (2.2 × 1.8 m, flat, steel) under its entry portal: tight; the transformer platform: no (cluttered).

### Placement rules
- V1 every 200–400 m along a village's street lines, at a pole of the low-voltage network (L rows) near a cluster of yards, 2–4 per village; V2 at the farm complex (P02) and by U01.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `variant` | enum | pole, kiosk | pole | Form |
| `pole_height` | m | 8.0–8.5 | 8.3 | Above-ground pole height |
| `platform_height` | m | 3.0–4.5 | 3.5 | Transformer height |
| `kva` | enum | 25, 63, 100, 160 | 63 | Tank size (scales 0.8–1.2) |
| `phase_spacing` | m | 0.6–0.8 | 0.7 | Conductor spacing on the crossarm |
| `cut_conductors` | int | 0–3 | D 1 | Conductors cut and hanging |
| `oil_leak` | bool | — | D on | Stain on the tank and ground |
| `pole_snapped` | bool | — | off | V1 at full damage |
| `damage` | 0–1 | — | 0 / 0.5 | I / D |
| `seed` | int | — | — | Random choices |

### Open points
- Porcelain insulators share `masonry` with L01 and L02; if physics wants a separate brittle ceramic, it is one more id (whole-sheet open points).

## P06 Village name sign (invented name), road signs

**Seen in:** R.
**Research sources:** DSTU 4100-2002 sign sizes (research table); the settlement-end sign's red stripe; names invented (SEC-03).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Settlement sign plate | 0.5–0.7 × 1.0–2.5 m, by name length | 0.6 × 1.6 m | assumed (the standard sizes the plate to the lettering) |
| Settlement-end stripe | red diagonal, 0.4 × the capital height wide | — | published |
| Round sign | Ø 0.60 / 0.70 m | 0.60 m | published (size I / II) |
| Triangle | side 0.70 / 0.90 m | 0.70 m | published |
| Rectangle | 0.60 / 0.70 m, border 40 mm | 0.60 m | published |
| Posts | steel pipe Ø 57–76 mm, one per small sign, two for a wide plate | Ø 76 mm | assumed |
| Plate bottom above ground | 1.5–2.5 m | 2.0 m | assumed |
| **Variant 2:** village stele | concrete, 2–4 m high, 1.5–3 m wide, relief or applied letters | 3.0 × 2.0 × 0.4 m | assumed (the Soviet-era roadside name monument) |

### Construction and materials
- V1: galvanised or painted sheet plates (1–2 mm) with a folded rim, retroreflective film, white field with black letters and a black border (the entrance sign), the end sign with a red diagonal stripe; generic regulatory and warning signs on single posts. V2: a cast or rendered concrete stele with relief letters and a painted emblem-free panel.
- **Lettering:** from the invented-name list only (SEC-03); no real place name, no copied text, no emblem.
- **Materials:** plates `rusty_painted_metal` with a derived sign-face map; posts `rusty_metal_02`; stele `concrete_wall_007`.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Sign white field, weathered | about #d8d8d2 | — | assumed |
| Red stripe and borders, faded | about #a8443a | — | assumed |
| Stele concrete | about #9a978f | — | assumed (as P04) |

### Variants and states
- Variants (the asset list's 2): 1 the metal sign pair (entrance and end) with a set of road signs; 2 the concrete stele.
- Damage (no listed states, a parameter): fragment holes (decals under 30 mm, cut holes from 30 mm), a bent post, a plate missing or hanging by one bolt.

### Damage pattern
- Sheet plates hole and bend; posts bend at the foot; the stele chips (bars only if it is reinforced, assumed not).

### Physics role
- **Collision:** posts cylinders, plates thin boxes, the stele a box.
- **Material ids:** posts `steel`; plates `sheet_metal` (their edges cut fiber); stele `concrete`.
- **Gaps:** between the two posts under a wide plate, 1.4 × 2.0 m: any yaw.
- **Wind volumes:** plates solid (a sign is a small flat plate; no volume needed below 2 m², assumed).
- **Snag hazards:** plate edges and corners, a hanging plate.
- **Landable surfaces:** none (the stele's top is 0.4 m wide).

### Placement rules
- On the main road at each village's entry and exit, 50–150 m outside the first and last houses, on the right verge 0.5–2 m from the carriageway; the end sign on the back of the entrance post or a separate post opposite; generic road signs at junctions, the bus stop (P04) and the farm turn-off; the stele at the main entry of one or two villages.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `variant` | enum | signs, stele | signs | Form |
| `name_id` | id | invented list | per village | The lettering (never free text, SEC-03) |
| `plate_size` | m | 0.5–0.7 × 1.0–2.5 | from the name | Plate size |
| `sign_set` | list | regulatory, warning, information | per placement | Which road signs |
| `size_class` | enum | I, II | I | DSTU size |
| `holes` | int | 0–20 | 3 | Fragment holes |
| `bent` | 0–1 | 0–1 | 0.2 | Post bend |
| `seed` | int | — | — | Random choices |

### Open points
- The invented-name list itself is the world-builder's (SEC-03), not this sheet's.

## Open points for the whole sheet

**Proposed physics material ids** (none invented in a generator; the physics engineer adds them to `physics_materials.json` with values; the first three were already proposed by the houses and vehicles sheets):
- `fabric`: curtains, sheets, carpets, mattresses and sofas in U01 and the mound; soft, wraps, no bounce; a hanging curtain is a soft snag (K10).
- `plastic`: white PVC window frames and profiles, appliance housings, packaging, plastic sheet (U01, U02, P01). The houses sheet's `plastic_film` covers thin sheet if physics keeps them apart.
- `asbestos_cement`: the cowshed roof (P02) and the slate option of U01; brittle, breaks under a leg (P02's roof is therefore not landable).
- **EPS foam** (insulation remnants on U01 and white chunks in the mound): very light and crushable; `plastic` unless physics wants a separate `foam`.
- Insulators (P05) use `masonry` for porcelain and `glass` for glass, as L01 and L02; a `ceramic` id only if physics wants porcelain apart.

**For the orchestrator:**
- **Asset-list §7, U01 eaves (resolved):** the asset list now reads 16–19 m to the eaves.
- **The I roof pile (resolved):** U02b now has its section (task 1.8). It is a thin `roof_pile` generator that composes the house generator's roof and wall builders, a seeded collapse transform, U02's core builder and the debris kit.
- **K10, K06 and L05 (checked):** L05's cables (3–15 mm, diagonals to 6.5 m) cover this sheet's 5–10 mm cables across the open bays; K06's bars are now 5–14 mm, matching the slab bars here; lintel bars are 8–12 mm as the houses sheet gives, balcony bars 10–12 mm.

**Settled by side-by-side or by the asset-artist, not by the pilot:** the window proportion and the roof silhouette in I, the dab interpretation in K, the storey height within AST-04's range, the P02 span and length, the P03 tank diameter. **No pilot question** comes out of this sheet: sizes are published, derived or measured, and P01–P06 and U03 are R rows the pilot sees at G2 like every other R row.
