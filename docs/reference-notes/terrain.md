# Terrain reference notes

The written catalog of what the pilot's own flights show: terrain, vegetation, structures, debris and their physical meaning for the sim. The M1 map is built and judged against it.

- **Sources:** 8 flight clips (**A–H**, seen through the goggles feed) and 3 ground photos (**I–K**). Everything is cited by letter only.
- **OPSEC:** the notes describe landscape *classes*, never places, positions, dates or anything read from the on-screen display.
- **Owner:** world-artist. Physical-role wording feeds the map-format spec agreed with the physics engineer.

## How to read this

- **Sizes** are estimates from scale cues in frame: brick face 250 × 65 mm, door leaf about 0.8 × 1.9 m, one storey about 2.8 m, fence board 0.10–0.15 m, and tree trunks against those.
- **Colours** are mean sRGB of a hand-picked region, measured on the extracted stills and photos.
  - `feed` is the colour as it appears on the goggles feed. Sunlit scenes read over-warm and over-saturated (B, G), and overcast ones read flat and grey (A, H).
  - `photo` comes from the photos I–K and is closer to true albedo.
  - Textures should be authored at photo-like albedo. The analog feed effect (tech-artist) adds the feed's shift, and M1 checks the result against the `feed` values.
- **Physical role** uses these tags: `surface` (landing/liftoff), `obstacle`, `turbulence` (wind shadow or wake), `snag` (catches the fiber or the legs), `gap` (fly-through opening), or `visual only`.

## The reference set at a glance

| Letter | Kind | What it shows | Light | Season cue |
|---|---|---|---|---|
| A | clip | Wide, gently rolling steppe meadow flown over high, then a long descent to just above the grass. Tree lines only on the horizon. | overcast, flat | late summer–autumn (green and dry grass mixed) |
| B | clip | Dry, sun-baked field pocked with craters, with sparse thin broken trees and an earth bank along one edge | low warm sun, long shadows | dry season |
| C | clip | Village yard: an old fruit tree, a brick-and-plank cellar entrance, sheds and leaf litter. The flight ends inside the cellar door. | bright sun | autumn (fallen leaves) |
| D | clip | Overgrown yard or wasteland: dense tall weeds, young and dead trees, stone rubble, a house roof edge and a sagging cable | sun ahead, strong backlight and flare | late summer |
| E | clip | War-damaged clay (adobe) house with its tile roof stripped to the battens. The flight enters through the door. | soft clear light | late summer |
| F | clip | Inside a dense tree belt: thin trunks, bare dusty ground, dust haze, and a round pit with fallen branches | sun through branches | dry season |
| G | clip | A trench cut along a narrow tree strip, next to a bare dark field. Fast, low pass. | low warm sun | dry season |
| H | clip | Slow, low flight along a tree-belt edge over lodged dry grass and dark soil, past a pole | overcast | autumn |
| I | photo | Urban edge: a burnt five-storey block, a collapsed timber roof pile, a damaged two-storey house, a rubble field and a low masonry fence | low sun | winter / early spring (bare trees) |
| J | photo | Village street: two damaged single-storey brick houses with stripped roofs, an overhead wire and brush piles | overcast | winter / early spring |
| K | photo | Five-storey brick block with its facade torn off, a large rubble mound and bare trampled ground | overcast, dark | early spring |

The tree belt is the backbone of the set. It appears in 6 of the 8 clips (A, B, D, F, G, H), either as the scene itself or as the horizon line.

---

## 1. Terrain surfaces

### T1 Open steppe meadow
- **Clips:** A (the whole clip). The same land type forms the horizon in B and G.
- **Size:** open ground kilometres across, gently rolling (estimated 2–5 m of relief over hundreds of metres). Grass 0.2–0.6 m.
- **Materials / colours:**
  - Continuous sod of mixed grasses and forbs, part green and part straw-dry. Seen from height it is a mottled olive-khaki.
  - Brighter yellow-green patches 2–5 m across (a different species).
  - Small dark patches 0.5–3 m (bare, scorched or hollowed ground).
  - `feed` near #807948, far #9b987b, green patch #696d1b.
- **Frequency:** the dominant open surface in A, continuous.
- **Physical role:**
  - `surface`: firm loam under the sod. Touching down sinks the legs only a little. Grass cushions the contact and adds small random side-loads (jitter).
  - The dark patches are hidden pitfalls where a leg can drop.
  - `turbulence`: none locally. Full exposure to wind and gusts, with no shelter until the next belt.

### T2 Dry cratered field
- **Clips:** B (whole clip).
- **Size:**
  - Craters 1–4 m across and 0.3–1 m deep, with loose raised rims, spaced about 5–20 m apart.
  - Between them, crusted flat ground with sparse tufts 0.1–0.4 m.
  - An earth bank or cut about 1 m high along the left edge. It may be a trench line (uncertain).
- **Materials / colours:** dry loam and clay with a sun-baked crust, dust, and tufts of dead grass. `feed` sunlit #c38436, near ground #a36f37 (strongly warmed by the feed).
- **Frequency:** craters all over the field, several per frame.
- **Physical role:**
  - `surface`: the hard crust gives little damping, so legs bounce. This is the manifesto's glide-landing bounce case.
  - Crater rims are loose spoil. Legs sink there, and the slope tilts the drone on touchdown.
  - Crater bowls are sloped landing spots where the drone slides or tips.
  - Dry dust lifts under prop wash near the ground.

### T3 Bare dark field (tilled or burnt stubble)
- **Clips:** G (the right side of the frame, beside the tree strip).
- **Size:** field-scale. Surface ridges estimated at 5–15 cm.
- **Materials / colours:** dark grey-brown bare soil with pale straw streaks in parallel lines. `feed` #786751.
- **Frequency:** one field in G.
- **Physical role:** `surface`. Loose furrowed soil, so legs sink unevenly and the drone tilts on liftoff. **Uncertain:** it is not clear whether this is tilled soil or burnt stubble. The user should confirm.

### T4 Tree-belt floor: lodged straw over dark soil
- **Clips:** H (the whole clip, at low height). Also the G trench parapets and the F belt margins.
- **Size:**
  - A mat of lodged dry grass 5–20 cm deep, with straws 0.3–1 m long lying crosswise.
  - Standing stems 0.5–1.2 m with seed heads.
  - Bare soil patches 0.5–2 m, and small earth mounds 0.3–0.5 m.
- **Materials / colours:** dark moist loam (chernozem-like) under straw-coloured dead grass, with some green regrowth and a few purple flowers. `feed` soil #68645b, straw #797258 (overcast), sunlit dry grass #d2953f (G).
- **Frequency:** continuous along every belt edge in H and G.
- **Physical role:**
  - `surface`, the manifesto's uneven-landing case. The mat is springy and uneven, so one leg rests higher than another. Straws hook around the legs and hold for a moment on liftoff. Holes hide under the mat.
  - Bare soil patches are soft when moist.
  - Standing stems brush the drone at low height: small drag, and visual jitter in the feed.

### T5 Bare dusty loam under trees
- **Clips:** F.
- **Size:** patches the width of the belt corridor, 2–10 m.
- **Materials / colours:** fine dry loam, beaten bare, with fallen twigs. Thick brown dust haze hangs among the trunks. `feed` #8a7156.
- **Frequency:** most of the ground in F.
- **Physical role:** `surface`. The ground is firm but has a loose top layer, so legs can slide on contact at speed. It is a dust source under prop wash (see O6).

### T6 Yard ground with litter
- **Clips:** C (leaf litter and brick bits), E (roof-tile shards and planks), D (stone slabs).
- **Size:** yard-scale, 10–30 m. A litter layer 1–3 cm deep. Loose items 5–40 cm.
- **Materials / colours:** compacted soil under fallen leaves, whole and broken bricks, clay tile shards, planks and stones. `feed` leaf litter #a67f4d, tile rubble #825f44.
- **Frequency:** around every building in the clips.
- **Physical role:**
  - `surface`: hard and uneven. A brick or shard under one leg tilts the drone at liftoff, and a plank can rock.
  - Leaves are slippery, so the drone slides on landing.

### T7 Tall weeds and overgrowth
- **Clips:** D (dominant). Also the foreground in E and C, and the margins in B.
- **Size:** tall forbs and weeds 0.5–1.5 m with stalks 5–15 mm thick, plus self-seeded saplings 1–3 m.
- **Materials / colours:** green-yellow leafy stalks and dry seed heads. `feed` #817831 (sunlit, backlit).
- **Frequency:** fills every abandoned plot in D. Patches along the walls in C and E.
- **Physical role:**
  - `obstacle` and `snag`. The stalks resist the drone and cling at low height, and the fiber drags through them.
  - Not a landing surface: legs go deep, and rubble hides underneath.
  - Props clipping the tops add drag and visual noise.

### T8 Trampled bare ground with scattered debris
- **Clips:** K, I (photos only).
- **Size:** open ground 20–50 m around damaged blocks. Debris 0.05–1 m.
- **Materials / colours:** trampled dry soil with brick fragments, wood, plastic, cables and glass. `photo` soil #867c6a.
- **Frequency:** everywhere around the damaged blocks.
- **Physical role:** `surface`. Firm but littered, with uneven support and snagging bits.

## 2. Vegetation

### V1 Tree belt (посадка)
- **Clips:**
  - F and H: inside and along the edge.
  - G: a narrow strip.
  - D: patchy.
  - B: thinned and broken on the horizon.
  - A: lines on the horizon.
- **Size:**
  - 10–30 m wide and hundreds of metres long.
  - Crowns 6–15 m high; young or damaged edges 3–8 m.
  - Trunks 5–30 cm across, spaced 1–3 m.
  - Shrub understory 1–3 m, with branches from about 1 m up.
- **Materials / colours:**
  - Grey-brown bark and many thin, whippy stems.
  - Crowns mixed green and yellow, many part-bare (dry or damaged).
  - `feed` canopy #5f6a60 (overcast, H), #877958 (sunlit, F).
- **Frequency:** the most frequent class in the set, in 6 of the 8 clips.
- **Physical role:**
  - `obstacle`: trunks and main branches.
  - `snag`: the densest snag hazard in the footage. A fiber trailing over or through the crowns catches on the thin branches.
  - `turbulence`: a porous windbreak. Expect a sheltered, gusty zone about 5–10 belt heights downwind, flow speeding up over the crown line, and calm inside the belt with sudden gusts through gaps.
  - `gap`: corridors between trunks 1–3 m wide (F), and clearings inside the belt (H).

### V2 Dead and damaged trees
- **Clips:**
  - B: thin trees with broken tops.
  - D: a bare, leaning dead trunk in the middle of the weeds.
  - G.
  - I and J: bare street trees.
- **Size:** 4–10 m tall, trunks 10–30 cm, bare branch fans 3–6 m wide.
- **Materials / colours:** grey, bark-stripped or leafless wood, a dark silhouette against the sky.
- **Frequency:** a few per scene. They stand out because they are the tallest thing in open ground.
- **Physical role:** `obstacle` and `snag` (bare twigs are hard to see on the feed). Visually a key silhouette.

### V3 Yard fruit trees
- **Clips:** C. A similar bare yard tree appears in J.
- **Size:** 4–7 m tall, trunk 20–40 cm, a low spreading crown 5–8 m wide with branches from about 1.5 m.
- **Materials / colours:** dark gnarled bark, sparse autumn leaves.
- **Frequency:** one or two per yard.
- **Physical role:** `obstacle` and `snag` at low height, right where a yard approach is flown. A small wind shadow.

### V4 Fallen branches and brush piles
- **Clips:** F (branches lying across the ground and over the pit), J (piles of cut branches in the yard), H (twigs in the straw).
- **Size:** branches 1–4 m long and 2–8 cm thick. Piles 1–2 m high and 2–4 m across.
- **Materials / colours:** grey-brown dead wood.
- **Frequency:** scattered through belts, and piled in yards.
- **Physical role:** `snag` (legs and fiber). Uneven `surface`. Ground-level `obstacle`.

## 3. Earthworks and ground damage

### W1 Shell craters
- **Clips:** B (many). The dark patches in A may be small ones (uncertain). See also W3 in F.
- **Size:** 1–4 m across, 0.3–1 m deep, rims 0.1–0.3 m high.
- **Materials / colours:** loose fresh spoil, slightly darker or lighter than the crusted field around it.
- **Frequency:** dozens across the field in B.
- **Physical role:** `surface` hazard. Pitfalls, soft rims and sloped bowls that tilt the drone on landing. `turbulence`: negligible.

### W2 Trench
- **Clips:** G (clear, the whole clip). B possibly, at the left edge (uncertain).
- **Size:** 0.6–1.0 m wide at the top and 1.2–1.8 m deep. It winds along the edge of a tree strip, with spoil parapets 0.3–0.5 m high on each side.
- **Materials / colours:** dark soil floor and walls (`feed` #493f32), with dry-grass parapets (`feed` #d2953f).
- **Frequency:** one long trench in G.
- **Physical role:**
  - `gap`: a narrow corridor to fly along. Landing inside puts the props close to the walls.
  - `turbulence`: sheltered at the bottom, with recirculation at the lip.
  - `obstacle`: walls and parapets.

### W3 Round pit
- **Clips:** F (the flight ends diving into it).
- **Size:** 2–3 m across and about 1–1.5 m deep, with a clean circular lip.
- **Materials / colours:** dusty loam (`feed` #897156), with dead branches lying over it.
- **Frequency:** one in F.
- **Physical role:** `surface` hazard (pitfall). The walls are `obstacle`s and the branches are a `snag`. **Uncertain:** it is not clear whether it was dug or is a crater.

### W4 Cellar entrance
- **Clips:** C (the flight ends inside it).
- **Size:**
  - A small head structure over steps going down: 1.2–1.5 m wide, 1.8–2.0 m high, 2–3 m long.
  - A doorway of about 0.7 × 1.6 m opening onto a dark stairwell.
- **Materials / colours:**
  - Red brick with patchy lime render (`feed` #b97541).
  - Weathered plank door and frame (`feed` #73623d).
  - A sloped slab roof with dark roofing felt.
  - A light-blue tarp draped beside it (`feed` #899cc2).
- **Frequency:** one per yard (a typical village yard feature).
- **Physical role:**
  - `gap`: the narrowest fly-in in the clips, dark inside.
  - The inner walls are `obstacle`s. The tarp is soft and a `snag`.
  - `turbulence`: flow funnels through the doorway.

## 4. Buildings

### B1 Clay (adobe) village house, damaged
- **Clips:** E. Similar stripped roofs in J.
- **Size:**
  - Footprint 8–12 × 5–7 m. Walls 2.3–2.6 m high and 0.4–0.5 m thick.
  - Gable ridge about 5 m. Brick chimney about 0.5 m square.
  - Door 0.8 × 1.8 m. Window openings about 0.8 × 1.0 m.
- **Materials / colours:**
  - Clay-and-straw walls with lime-clay render falling off in patches (`feed` #755f40 in shade to #9f713b lit).
  - A roof stripped to the timber battens and rafters, with orange-red clay tiles left in places (region mix with battens #967056).
  - Tile shards piled at the wall foot (#825f44).
  - A reddish-brown plank door (#7c5848). A grey plank shed and fence attached (#615e56).
- **Interior (E):** a dark room with fallen boards and debris, a cable hanging from the ceiling, and daylight through a window opening on the far side.
- **Frequency:** the main building in E.
- **Physical role:**
  - `obstacle`.
  - `gap`: the door, window openings, the room behind, and the far window as an exit. Holes in the roof only where battens are broken, because intact battens are 0.3–0.4 m apart, too narrow to pass.
  - `turbulence`: building wake, plus flow through the opened roof.
  - `snag`: rafters, battens, the hanging cable.
  - Rubble around the walls makes landing uneven.

### B2 Brick village house, damaged
- **Clips:** J (two houses).
- **Size:** single storey, footprint 8–10 × 6–8 m, ridge 5–6 m.
- **Materials / colours:**
  - Pale silicate brick (`photo` #a79c8f).
  - Corrugated asbestos-cement roof sheets on timber battens, partly collapsed (`photo` #736867).
  - Wooden window frames and shutters painted blue or turquoise (by eye), a satellite dish, an overhead wire to the house.
- **Frequency:** two per frame in J, which reads as a typical village street.
- **Physical role:** as B1 (`obstacle`, `gap` through broken windows and roofs, `turbulence` wake, `snag` on battens and wires).

### B3 Sheds and outbuildings
- **Clips:** C (a brick or block shed with a small window, plus the cellar head), E (a plank shed), J.
- **Size:** 3–6 × 2–4 m, 2–2.5 m high.
- **Materials / colours:** brick, block, grey planks (`feed` #615e56 to #73623d), roofing felt, sheet metal.
- **Frequency:** two or three per yard.
- **Physical role:** `obstacle`. `turbulence` shelter at low level. `snag` on eaves and sheet edges. Small openings as `gap`s.

### B4 Five-storey apartment block, damaged
- **Clips:** I, K (photos only, never in a clip).
- **Size:** about 15 m tall (5 storeys), about 12 m deep, 50–80 m long. Window openings about 1.2 × 1.4 m.
- **Materials / colours:**
  - K: brick, with the whole facade torn off to show rooms 3–4 m wide, concrete floor slabs, furniture, radiators and balconies (`photo` brick #847b71).
  - I: a pale rendered block with soot above burnt-out windows and no glass (pale cream by eye; the measured region was unreliable).
- **Frequency:** only in the urban-edge photos.
- **Physical role:**
  - `obstacle`.
  - Strong `turbulence`: separation at the roof edge, corner vortices, downwash on the lee side, and channelling between blocks.
  - `gap`: open-faced rooms (K) and blown windows (I).
  - `snag`: rebar, cables, hanging fabric, balcony rails.

### B5 Collapsed structures and rubble mounds
- **Clips:**
  - I: a collapsed timber roof pile 3–4 m high, and a two-storey brick house with its roof and upper floor gone.
  - K: a rubble mound 2–4 m high spreading 15–20 m.
  - J: roof collapse and debris.
  - E: tile heaps.
- **Size:** as above. Pieces 0.05–3 m.
- **Materials / colours:** brick, concrete slabs and lintels, rafters, planks, sheet metal, asbestos sheet, insulation, furniture and plastic (`photo` #8d8172 for K rubble).
- **Frequency:** at the foot of every damaged building.
- **Physical role:** a very uneven `surface`, jutting timbers as `snag`s, and voids as pitfalls.

### B6 Fences, gates and posts
- **Clips:** E (a grey plank fence), I (a low masonry fence with a decorative panel, and metal or timber posts), J (posts and fence remains), H (thin posts in the distance).
- **Size:** 1–2 m high. Boards 0.10–0.15 m. Posts 0.1–0.2 m.
- **Materials / colours:** grey weathered wood (`feed` #615e56), block or brick, and steel.
- **Frequency:** along every yard.
- **Physical role:** low `obstacle`s. Post tops `snag` the fiber in low flight.

## 5. Small objects, debris and lines

### O1 Wires and cables
- **Clips:**
  - C: an overhead wire above the shed roof, about 3–5 m up.
  - D: a cable sagging low across the weeds, about 0.5–2 m up.
  - E: a cable hanging inside the house.
  - J: an overhead wire across the street.
  - K: cables hanging from the open floors.
- **Size:** 5–15 mm across, spans 10–40 m with visible sag.
- **Materials / colours:** dark, one or two pixels wide on the feed even at close range.
- **Frequency:** present near every building clip (C, D, E) and in J and K.
- **Physical role:** `snag` and `obstacle`. Nearly invisible on the feed, so they need real collision (a thin capsule) and must be able to catch the fiber. The main surprise hazard in the set.

### O2 Poles
- **Clips:** H (a straight grey pole about 20–25 cm across and about 7–9 m tall at the belt edge, with thin posts in the distance), J (wooden posts).
- **Size:** as above.
- **Materials / colours:** grey weathered wood, or possibly concrete.
- **Frequency:** a line of them along the belt in H.
- **Physical role:** `obstacle`, and `snag` together with their wires. **Uncertain:** it is not clear whether the H pole is wood or concrete, and whether it still carries wires.

### O3 Loose masonry and roof debris
- **Clips:** C, D, E, I, K.
- **Size:** bricks 250 × 120 × 65 mm, tile shards 0.1–0.4 m, stone slabs 0.3–0.8 m.
- **Materials / colours:** red brick (#b97541 `feed`), clay tile (#825f44 `feed`), grey stone and concrete.
- **Frequency:** clustered at wall feet and in yards.
- **Physical role:** rigid small `obstacle`s under the legs (see T6).

### O4 Timber, sheets, tarps and fragments
- **Clips:** planks in C, E, I, J and K. Tarps and plastic in C (light blue) and K (white). In B, a light-coloured fragment about 1 m long lying in the field (a plank or sheet).
- **Size:** planks 1–3 m, sheets up to 2 × 1 m.
- **Materials / colours:** grey or brown wood, blue or white plastic.
- **Frequency:** a few per damaged site.
- **Physical role:** planks rock and slide under the legs. Tarps are soft, flutter in wind and prop wash, and `snag`.

### O5 Household contents
- **Clips:** K (wardrobes, shelving, radiators, appliances), I (sheets and plastic).
- **Size:** 0.3–2 m.
- **Materials / colours:** chipboard, metal and textiles.
- **Frequency:** only at the apartment-block sites.
- **Physical role:** mostly `visual only`. Large pieces inside rubble act as `obstacle`s.

### O6 Airborne dust
- **Clips:** F (a thick brown haze in the belt), B (dry dust).
- **Size:** fills the belt corridor to 2–4 m high.
- **Materials / colours:** brown loam dust matching T5.
- **Frequency:** in dry scenes.
- **Physical role:** `visual only` (reduces visibility). Its source is a surface property: dry loam emits dust under prop wash near the ground.

## 6. Micro-detail that must interact physically

This is the straw-and-stick level the manifesto asks for. To keep it within budget, the proposal is instanced visuals everywhere, plus simplified physical stems only within a few metres of the drone.

| Element | Clips | Size | Density | Material / colour | What it should do |
|---|---|---|---|---|---|
| Standing grass stems and seed heads | A, H, D, B | 2–5 mm across, 0.2–1.2 m tall | continuous in A, hundreds per m² | green to straw (#807948 / #797258 `feed`) | Bend under prop wash (visual). Give elastic contact on the legs, meaning small random side forces and extra damping at touchdown (jitter). Dry stems are stiffer than green ones. |
| Lodged straw mat | H, G | 5–20 cm deep, straws 0.3–1 m | continuous at belt edges | straw #797258 / #d2953f `feed` | Act as a spring-damper cushion with uneven support, so one leg sinks further and the drone tilts at liftoff. Straws hook legs for a moment. |
| Twigs and sticks | F, H, D | 5–30 mm across, 0.3–2 m | patchy | grey-brown | Rigid, thin and rollable under the legs. Snag the fiber. |
| Leaf litter | C | layer 1–3 cm | continuous in yards | #a67f4d `feed` | Slight cushion and low friction, so legs slide. |
| Brick bits, tile shards, stones | C, E, D, I, K | 5–40 cm | clustered at walls | #b97541 / #825f44 `feed` | Rigid uneven support that tilts the drone at liftoff. |
| Small holes, burrows, bare pits | A, H | 0.1–3 m | scattered | dark soil | Pitfalls where a leg drops without warning. |
| Loose dust | F, B | — | dry surfaces | #8a7156 `feed` | Particles under prop wash near the ground. Visual only. |

## 7. Surface data proposal (input to the map-format spec)

Porosity values are typical for the soil type, not measured from the footage. The physics engineer and the map-format spec settle the final fields.

| Surface id | Clips | Soil | Bearing | Porosity (typical) | Cover | Cover height | Leg behaviour |
|---|---|---|---|---|---|---|---|
| `meadow_sod` | A | loam under sod | firm | 0.50–0.60 | dense mixed grass | 0.2–0.6 m | sinks 1–3 cm, cushioned, jitter |
| `dry_crust` | B | dry crusted loam/clay | hard | 0.40–0.50 | sparse dry tufts | 0.1–0.4 m | little sink, bounce, dust |
| `crater_spoil` | B, F | loose spoil | soft | 0.50–0.60 | none | — | sinks 3–8 cm, slope tilt |
| `belt_straw` | H, G | moist dark loam | soft, springy | 0.50–0.60 | lodged straw and stems | mat 5–20 cm, stems to 1.2 m | springy, uneven, legs hooked |
| `belt_bare` | F | dry fine loam | firm, loose top | 0.45–0.50 | none | — | slides, dust |
| `tilled` | G | loose furrowed soil | soft | 0.55–0.65 | straw streaks | < 0.2 m | uneven sink 2–5 cm |
| `yard_litter` | C, E | compacted soil | hard | 0.35–0.45 | leaves, shards | 1–3 cm | slides, uneven |
| `rubble` | E, I, J, K | brick/tile/timber | rigid, irregular | n/a | debris | 5–50 cm | tilts, snags |
| `weeds` | D | loam | firm | 0.50 | dense tall forbs | 0.5–1.5 m | no landing (legs go deep), drag and snag |

## 8. Gaps: expected classes missing from the footage

- **Vehicles.** No car, truck, tractor or burnt-out wreck appears in any clip. There is only a small car far in the background of K. This needs more reference, or the user's guidance on typical types and condition.
- **Roads and tracks.** No asphalt road and no clear dirt track. A track may run along the tree strip in G (uncertain).
- **Power lines on pylons.** Only single wires and poles appear (C, D, H, J).
- **Standing crops and gardens.** No sunflower, wheat or maize field, no orchard in leaf, no vegetable rows.
- **Water and mud.** No pond, stream, puddle or wet mud.
- **Winter ground.** No snow or frozen ground in the clips. Winter appears only in the photos, as bare trees.
- **Dusk and night.** Not in the reference. Time of day is a user setting.
- **Metal debris close up.** No rebar, corrugated sheet or barbed wire seen at flying range.
- **Unreadable footage.** The last second of G is too smeared by the feed to classify.

---

## 9. Ranked M1 build list

1. **Tree-belt edge patch.** A belt about 20 m wide with trunks, understory and part-bare crowns, a straw-mat floor over dark soil, and a grass margin. Reason: it is in F, G, H and D and on every horizon (A, B), and it packs the most snag, wind-shadow and landing physics into one piece.
2. **Straw-mat and stem micro-detail layer.** Lodged straw, standing stems and twigs over dark soil, with instanced physical stems near the drone. Reason: H is a long low flight over exactly this, the manifesto's straw-level landing jitter.
3. **Open rolling meadow patch.** About 500 m of sod and mixed grass with hidden pitfall patches, and belts on the horizon. Reason: A is the main open cruise-and-descend surface, and M1 noclip needs the wide view.
4. **Damaged adobe house with a stripped roof, an enterable door and a far window.** Reason: E flies in through the door and sees daylight through the far window. It is the core fly-in structure.
5. **Dry cratered field patch.** Craters 1–4 m with loose rims on crusted loam with sparse tufts. Reason: B shows it end to end, and it is the hard-surface bounce and crater-tilt case.
6. **Trench along a narrow tree strip.** Reason: G flies it low and fast. It is the only narrow ground corridor in the clips.
7. **Village yard set: fruit tree, brick cellar entrance, shed, leaf litter.** Reason: C ends by flying into the cellar door, the narrowest fly-in gap in the footage.
8. **Core texture set.** Dark chernozem, dry loam, adobe render, red brick, silicate brick, clay tile, weathered grey wood, dry straw. Reason: every clip and photo is built from these. Author them at photo albedo and check them through the feed against the `feed` hex values above.
9. **Debris and rubble kit.** Bricks, tile shards, planks, sheets, tarp, branches, a rubble mound. Reason: it lies around every structure in C, D, E, I, J and K, and it drives uneven landings and snags.
10. **Wires and poles.** Reason: C, D, E and J all have thin lines that are nearly invisible on the feed. They are the main surprise fiber-snag hazard.
11. **Brick village house variant.** Silicate brick, asbestos roof, painted frames. Reason: J shows it is as common as adobe, and it keeps the village from being a single repeated house.
12. **Damaged five-storey block.** Reason: it is only in I and K (no clip). It is a large turbulence obstacle for later, and the lowest M1 priority.
