# Terrain reference notes

The written catalog of what the pilot's own flights show: terrain, vegetation, structures, debris and their physical meaning for the sim. The M1 map is built and judged against it.

- **Sources:** 9 flight clips (**A–H** and the night clip **L**, seen through the goggles feed) and 6 ground photos (**I–K**, and the vehicle photos **M–O**). Everything is cited by letter only.
- **Pilot's answers:** the pilot answered this file's open questions. Those answers are folded in and marked **(pilot)**. Where an answer differs from the first reading of the footage, the answer wins.
- **OPSEC:** the notes describe landscape *classes*, never places, positions, dates, text or plates seen in the images, people, or anything read from the on-screen display.
- **Owner:** world-artist. Physical-role wording feeds the map-format spec agreed with the physics engineer.

## How to read this

- **Sizes** are estimates from scale cues in frame: brick face 250 × 65 mm, door leaf about 0.8 × 1.9 m, one storey about 2.8 m, fence board 0.10–0.15 m, and tree trunks against those.
- **Colours** are mean sRGB of a hand-picked region, measured on the extracted stills and photos.
  - `feed` is the colour as it appears on the goggles feed. Sunlit scenes read over-warm and over-saturated (B, G), and overcast ones read flat and grey (A, H).
  - `photo` comes from the photos I–K and M–O and is closer to true albedo.
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
| L | clip | Night flight in black and white over open, gently rolling dark fields, with a tree line on the horizon. Poles, bushes and lone trees read only as silhouettes against the sky | night, overcast sky brighter than the ground | not readable |
| M | photo | Abandoned classic small sedan on an asphalt street: fragment holes, a flat tyre, a crazed windscreen | bright sun, hard shadows | late winter / early spring (bare trees) |
| N | photo | Blast-wrecked small hatchback in a wide field of strike debris | overcast | winter |
| O | photo | Burnt-out heavy bonneted truck on a slushy road, with snow all around | overcast, snow light | winter (snow) |

The tree belt is the backbone of the set. It appears in 7 of the 9 clips (A, B, D, F, G, H, L), either as the scene itself or as the horizon line.

**Night (L).** At night the picture is black and white (the feed side of this is the tech-artist's).
- The ground reads almost black (`feed` #0d0d0d–#191919). The overcast sky is mid-grey (`feed` #4e4f4e), so it is brighter than the ground.
- Only silhouettes against the sky can be read: the horizon, tree lines, poles, bushes and lone trees. Ground texture, tracks, pits and low obstacles are barely visible or invisible.
- For the map, this means skyline silhouettes are the pilot's only landmarks at night. The crests of rolling ground, lone trees, bushes, poles and belt crowns need clean outlines at distance, and their LODs must keep those outlines. Wires and low debris are invisible at night, just as they are nearly invisible by day.
- Textures need no night variant. The night look belongs to the video feed (tech-artist).

---

## 1. Terrain surfaces

### T1 Open steppe meadow
- **Clips:** A (the whole clip). L (the same kind of open, gently rolling land, at night). The same land type forms the horizon in B and G.
- **Size:** open ground kilometres across, gently rolling (estimated 2–5 m of relief over hundreds of metres). Grass 0.2–0.6 m.
- **Materials / colours:**
  - Continuous sod of mixed grasses and forbs, part green and part straw-dry. Seen from height it is a mottled olive-khaki.
  - Brighter yellow-green patches 2–5 m across (a different species).
  - Small dark patches 0.5–3 m (bare, scorched or hollowed ground).
  - `feed` near #807948, far #9b987b, green patch #696d1b.
- **Frequency:** the dominant open surface in A, continuous.
- **Physical role:**
  - `surface`: firm loam under the sod. Touching down sinks the legs only a little. Grass cushions the contact and adds small random side-loads (jitter).
  - The dark patches are hidden pitfalls where a leg can drop. The pilot's answer on burnt ground (strikes, and fallen drones that explode) makes small scorch spots over shallow craters the likely reading. They are built as the small form of T3b, which covers both readings.
  - `turbulence`: none locally. Full exposure to wind and gusts, with no shelter until the next belt.

### T2 Dry cratered field
- **Clips:** B (whole clip).
- **Size:**
  - Craters 1–4 m across and 0.3–1 m deep, with loose raised rims, spaced about 5–20 m apart.
  - Between them, crusted flat ground with sparse tufts 0.1–0.4 m.
  - An earth bank or cut about 1 m high along the left edge. **Resolved (pilot):** earthworks like this bank are real and common. It is built as a trench spoil bank (see W2).
- **Materials / colours:** dry loam and clay with a sun-baked crust, dust, and tufts of dead grass. `feed` sunlit #c38436, near ground #a36f37 (strongly warmed by the feed).
- **Frequency:** craters all over the field, several per frame.
- **Physical role:**
  - `surface`: the hard crust gives little damping, so legs bounce. This is the manifesto's glide-landing bounce case.
  - Crater rims are loose spoil. Legs sink there, and the slope tilts the drone on touchdown.
  - Crater bowls are sloped landing spots where the drone slides or tips.
  - Dry dust lifts under prop wash near the ground.

### T3 Bare dark field: tilled or burnt
**Resolved (pilot):** both kinds are common and the map has both. Some dark fields are **tilled**. Others are **burnt**, either by strikes or by FPV drones that fall and explode. G's field (pale straw streaks in rows over dark ground) fits either kind, so its look can seed both variants.

- **Clips:** G (the right side of the frame, beside the tree strip). No reference shows a burnt field close up.
- **Frequency:** one field in G. On the map, dark fields sit between the meadow, the cratered field and the tree belts (see §10).

**T3a Tilled field**
- **Size:** field-scale, hundreds of metres. Ploughed ridges 15–30 cm high and 0.3–0.5 m apart, with clods 5–20 cm. Disked or harrowed ground is finer, with ridges 5–15 cm (the estimate for G).
- **Materials / colours:** dark grey-brown loose loam, with straw residue in parallel streaks. `feed` #786751 (G).
- **Physical role:** `surface`. The soil is loose and porous, so legs sink 2–5 cm unevenly. A clod under one leg tilts the drone on liftoff, and landing across the furrows rocks it. Dry tilled soil gives off dust under prop wash.

**T3b Burnt field**
- **Size:** from a scorched disc 2–6 m across around a small crater 0.3–1 m (a fallen drone or a small strike) up to a whole field hundreds of metres across, once dry grass or stubble catches. Fire fronts leave wavy edges and unburnt islands and strips. Standing stubble burns down to charred stumps 5–15 cm high.
- **Materials / colours:** a black char layer and grey-white ash over soil that fire leaves unchanged underneath. Char is darker than the tilled soil, and the ash is lighter. Not measured, because no reference shows it close up, so the M1 check is the pilot's eye.
- **Physical role:**
  - `surface`: firm, with little sink (0–1 cm), because burning doesn't loosen the soil. Charred stubble is stiff but brittle. It snaps on contact, so it gives short damping spikes instead of the springy cushion of live grass.
  - Ash lifts as a dark plume under prop wash near the ground.
  - `turbulence` (a suggestion for physics): on sunny days both dark fields heat more than grass, so updrafts over them are stronger than over the meadow.

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
  - A and L: lines on the horizon (L at night, as a dark band under the sky).
- **Size:**
  - 10–30 m wide and hundreds of metres long.
  - Crowns 6–15 m high; young or damaged edges 3–8 m.
  - Trunks 5–30 cm across, spaced 1–3 m.
  - Shrub understory 1–3 m, with branches from about 1 m up.
- **Materials / colours:**
  - Grey-brown bark and many thin, whippy stems.
  - Crowns mixed green and yellow, many part-bare (dry or damaged).
  - `feed` canopy #5f6a60 (overcast, H), #877958 (sunlit, F).
- **Frequency:** the most frequent class in the set, in 7 of the 9 clips.
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
- **Clips:** B (many). The dark patches in A are likely small ones with scorched rims (see T1 and T3b). See also W3 in F.
- **Size:** 1–4 m across, 0.3–1 m deep, rims 0.1–0.3 m high.
- **Materials / colours:** loose fresh spoil, slightly darker or lighter than the crusted field around it.
- **Frequency:** dozens across the field in B.
- **Physical role:** `surface` hazard. Pitfalls, soft rims and sloped bowls that tilt the drone on landing. `turbulence`: negligible.

### W2 Trench
**Confirmed (pilot):** trenches are common in this war and are a terrain class of their own. Earthworks like B's bank are real.

- **Clips:** G (clear, the whole clip). B: the earth bank about 1 m high at the left edge, built as a spoil bank.
- **Size:** 0.6–1.0 m wide at the top and 1.2–1.8 m deep. It winds along the edge of a tree strip, with spoil parapets 0.3–0.5 m high on each side.
- **Materials / colours:** dark soil floor and walls (`feed` #493f32), with dry-grass parapets (`feed` #d2953f).
- **Frequency:** one long trench in G. Common on the map: several runs along tree belts and field edges (see §10).
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
- **Frequency:** the main building in E. **Map (pilot):** a couple of village houses with yards like the ones in the clips. This house with the C yard set is the first one.
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
- **Frequency:** two per frame in J, which reads as a typical village street. On the map it is the second of the pilot's couple of village houses, so the village isn't one house repeated.
- **Physical role:** as B1 (`obstacle`, `gap` through broken windows and roofs, `turbulence` wake, `snag` on battens and wires).

### B3 Sheds and outbuildings
- **Clips:** C (a brick or block shed with a small window, plus the cellar head), E (a plank shed), J.
- **Size:** 3–6 × 2–4 m, 2–2.5 m high.
- **Materials / colours:** brick, block, grey planks (`feed` #615e56 to #73623d), roofing felt, sheet metal.
- **Frequency:** two or three per yard.
- **Physical role:** `obstacle`. `turbulence` shelter at low level. `snag` on eaves and sheet edges. Small openings as `gap`s.

### B4 Five-storey Stalin-era apartment block, destroyed
**Map (pilot):** exactly one typical destroyed five-storey Stalin-era block, like the photos I and K.

- **Clips:** I, K (photos only, never in a clip).
- **Size:**
  - The Stalin-era type has taller storeys than later blocks: 3.0–3.4 m per storey, so 15–17 m to the eaves.
  - A pitched timber roof adds 3–4 m. Eaves overhang 0.5–0.8 m.
  - About 12–15 m deep and 40–80 m long, in 3–5 stairwell sections.
  - Load-bearing brick walls 0.5–0.65 m thick. Window openings about 1.3 × 1.8 m.
- **Materials / colours:**
  - K: brick, with the whole facade torn off to show rooms 3–4 m wide, concrete floor slabs, furniture, radiators and balconies. The roof is stripped to its timbers. The bare brick is dotted with old mortar dabs (`photo` brick #847b71).
  - I: a pale rendered block with soot plumes above burnt-out windows and no glass (pale cream by eye; the measured region is mostly windows, so it isn't used).
  - The type also has sheet-steel or asbestos-cement roofing and a plinth with small basement windows.
- **Damage kit (I, K):**
  - one end or section with its facade torn off and rooms open on every floor
  - hanging or sagging floor slabs
  - burnt-out windows with soot above them
  - the roof gone down to its timbers
  - a rubble mound 2–4 m high spreading 15–20 m (B5)
  - contents spilling out (O5)
- **Frequency:** one on the map (pilot). In the reference, only in the urban-edge photos.
- **Open:** the blocks in I and K look plainer than a textbook Stalin-era block, with no visible cornice or decoration. M1 shows the pilot the massing and damage first. Decoration and storey height are cheap to change afterwards.
- **Physical role:**
  - `obstacle`. It is the largest structure on the map.
  - Strong `turbulence`: separation at the roof edge, corner vortices, downwash on the lee side, and channelling between blocks. Expect a recirculation zone about 1.5–2.5 block heights behind it and a gusty wake to about 10 heights.
  - `gap`: open-faced rooms (K), 3–4 m wide and about 3 m high, so a slow fly-in fits. Blown windows (I, about 1.3 × 1.8 m) are a tight fly-in for a drone about 0.65 m across, and they let gusts through the building.
  - `snag`: rebar, cables, hanging fabric, balcony rails, roof timbers.

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
- **Clips:** E (a grey plank fence), I (a low masonry fence, and metal or timber posts), J (posts and fence remains), H (thin posts in the distance).
- **Size:** 1–2 m high. Boards 0.10–0.15 m. Posts 0.1–0.2 m.
- **Materials / colours:** grey weathered wood (`feed` #615e56), block or brick, and steel.
- **Frequency:** along every yard.
- **Physical role:** low `obstacle`s. Post tops `snag` the fiber in low flight.

## 5. Vehicles

No vehicle appears in any clip. This section keeps two sources apart, and every line says which one it comes from:
- **Observed** means seen in the vehicle photos M, N or O, always cited by letter. A small car far off in K is too small to add detail.
- **General** means general knowledge, not seen in any reference. Only the makes come from the pilot's list (pilot): ВАЗ, Chery, Chevrolet, VW, Daewoo Lanos, ЗИЛ and Урал. The specific models, their factory dimensions (length × width × height, rounded), the typical colours and how wrecks age are the World Artist's general knowledge. Check the dimensions against a spec sheet when modelling.
- **Colours:** the `photo` values were measured on M, N and O, so they are observed. Colours marked "typical" are general.
- **Build:** each model is built once as a clean base plus two state kits, so every model exists in both states (R0).

### R0 Vehicle states: abandoned vs destroyed by strikes
**Pilot:** some vehicles are abandoned and some are destroyed by strikes.

**Observed.** One photo of the abandoned state and two of the destroyed state.

| | M: abandoned, holed by fragments, not burnt | N: destroyed, blast-wrecked | O: destroyed, burnt out |
|---|---|---|---|
| Body | Paint intact and dusty. Fragment holes about 3–20 cm in the wings and doors, with orange rust at their edges. The bonnet is torn and bent up at one rear corner. | Panels soot-grey. Doors bulged outward and sprung, the bonnet torn up and folded back, the roof pushed down, panels peppered with small holes. | Cab and bonnet soot-black. The cab door hangs open. |
| Glass | Windscreen crazed, and holed where the bent bonnet edge meets it. No glass visible in the side windows. | None left. | No windscreen. |
| Wheels | A flat front tyre. Steel wheels with hubcaps. | Tyres still on the alloy wheels. | Not readable at the photo's size. |
| Interior | Seats in place. | Seats in place. A loose cable hangs across a door opening. | Not readable. |
| Around it | A small scatter of lamp and trim parts within about 2 m, on asphalt. | A wide debris field on paved ground: broken concrete blocks, planks, sheet metal, a fallen trunk, paper and plastic. | Dark wet slush around the truck, snow beyond. |
| Colours (`photo`) | dark green paint #38444a in shade to #697c82 lit, tyre #545455, dusty asphalt #b7b2aa | soot-grey panels #393c3d–#3a3d3f, debris ground #5a5955–#6d6e6b | burnt cab front #1d1e21, body side #3d3c40, snow #ccd4e1, slush #525256 |

**General (not seen in M–O).**
- **Abandoned:** paint fades, and rust spreads from the sills, the wheel arches and every hole edge. Leaves and litter collect under and inside. Off asphalt, weeds 0.3–1.5 m grow around and through the body (T7).
- **Flat tyres:** the flat corner sits 5–10 cm lower and the body leans 1–3°. This is an estimate for M's flat front tyre as well.
- **Glass:** a laminated windscreen crazes and sags in place. Tempered side and rear windows break into small crumbs.
- **Blast:** trim and wiring hang out of the torn body, and the boot lid can be torn open like the bonnet.
- **Burnt:** soot-black paint turns rust-brown within weeks. Plastic bumpers and trim melt first. Tyres burn down to the steel bead wire, so a burnt car sits on its rims, 15–25 cm lower. Interiors burn down to seat frames and springs. A truck's canvas tilt burns away and leaves bare steel hoops.
- **Around a wreck:** a strike leaves a debris field about 5–20 m across of glass, body fragments, trim and plastic, plus whatever else it threw. A burnt wreck sits in a dark soot or melt patch about 2–6 m across.

**Physical role (every vehicle; estimates from the general sizes in R1–R5):**
- `obstacle`: a rigid hull. Collision is a few convex pieces (body, cabin, wheels), with thin capsules for torn parts. The drone flies around or over it.
- `turbulence`: a bluff body. Expect a recirculation zone about 2–3 heights behind it and a gusty wind shadow to about 10 heights (a car at about 1.45 m: 3–4 m and about 15 m; a truck at 2.4–3 m: 6–8 m and about 30 m). A burnt or blast-opened shell with no glass is porous, because air passes through the cabin. Its shelter is weaker and its wake choppier than an intact vehicle's.
- `snag`: torn sheet-metal edges, sprung door skins, folded bonnets, roof racks (M), hanging wiring, burnt seat springs, and the bare tilt hoops of a burnt truck. Torn metal is sharp, so a fiber dragged across it should be more likely to cut than on branches (an input for the tether model).
- `surface`: an emergency landing spot on a roof. A car roof is about 1.2 × 1.1 m at 1.4–1.5 m, and a truck cab roof about 2.0 × 1.4 m at 2.4–2.7 m. Sheet steel is hard and slippery, and it flexes and pops under load. A dented or pushed-down roof (N) is uneven and may carry broken glass.
- `gap`: none. Car window openings are about 0.4–0.5 m high, too small for a drone about 0.65 m across. The clearance under a car (0.15–0.22 m) or a truck (0.27–0.40 m) is too low as well.
- The debris field around a wreck is an uneven `surface` with small rigid pieces (O3, O4, and glass crumbs in §7).

**Frequency on the map (a proposal, not the pilot's):** cars far outnumber trucks. Abandoned vehicles stand in yards, at roadsides and at field edges, often overgrown. Destroyed ones stand on roads, tracks and open fields, often near craters or scorched ground.

### R1 ВАЗ (Lada), classic rear-drive (2101–2107)
- **Observed (M):** a four-door sedan with twin round headlamps, a chrome bumper, steel wheels with hubcaps and a steel-tube roof rack. Its shape matches the first classic ВАЗ generation (identified from general knowledge).
- **Size (general):** 4.07–4.17 × 1.61–1.62 × 1.44 m, wheelbase 2.42 m, clearance 0.17 m, kerb mass 0.96–1.05 t. The estates (2102, 2104) have the same footprint and a longer roof.
- **Materials / colours:** a steel body with chrome (2101–2103, 2106) or black plastic (2105, 2107) bumpers (general). Flat, faded solid paints: M's dark green is measured (R0); typically also white, beige, red, sky blue and olive (general).
- **Frequency:** the pilot lists ВАЗ first. That the classic ВАЗ is the most common old car in villages is general knowledge. It is the first vehicle to build.

### R2 ВАЗ (Lada), front-drive and Niva
- **Observed:** none. The pilot lists ВАЗ; these models are general.
- **Size (general):**
  - 2108, 2109 and 21099: 4.01–4.21 × 1.65 × 1.40 m, about 0.9 t.
  - 2110 family: 4.27 × 1.68 × 1.42 m, about 1.0 t.
  - 2121 Niva: 3.74 × 1.68 × 1.64 m, clearance 0.22 m, about 1.15 t. A short, tall, boxy off-roader.
- **Materials / colours (general):** a steel body with black plastic bumpers. Typically white, silver, dark red, green or dark blue.
- **Frequency (general):** common in villages. The Niva suits tracks and field edges.

### R3 Low to mid-class cars: Daewoo Lanos, Chevrolet, Chery, VW
- **Observed (N):** a small five-door hatchback of this class, blast-wrecked, on alloy wheels. Its make can't be read.
- **Size (general; the models are common ones for each make on the pilot's list):**
  - Daewoo Lanos sedan: 4.24 × 1.68 × 1.43 m, about 1.0 t.
  - Chevrolet Aveo: hatch 3.92 m or sedan 4.31 m long, × 1.68–1.71 × 1.50 m. Lacetti: 4.52 × 1.73 × 1.45 m. About 1.1–1.2 t.
  - Chery Amulet: 4.27 × 1.69 × about 1.45 m. Tiggo SUV: 4.29 × 1.77 × 1.72 m.
  - VW Golf: 4.0–4.15 × 1.70–1.74 × 1.43 m. Passat: 4.58–4.68 × 1.71–1.74 × 1.43–1.46 m.
- **Materials / colours (general):** a steel body with plastic bumpers and trim, which melt or tear away first, on steel or alloy wheels. Typically metallic silver, grey, black, dark blue or red. N's own colour can't be read under the soot (observed).
- **Frequency (general):** the common modern cars. One or two bodies (a hatchback and a sedan) cover the class.

### R4 ЗИЛ trucks (130, 131)
- **Observed:** none. The pilot lists ЗИЛ; these models are general.
- **Size (general):**
  - ЗИЛ-130 (4×2, bonneted cab, flatbed): 6.68 × 2.50 × 2.40 m, empty about 4.3 t, wheels about 0.97 m across, clearance 0.27 m.
  - ЗИЛ-131 (6×6): 7.04 × 2.50 × 2.48 m at the cab and 2.98 m with a canvas tilt, empty about 6.1–6.7 t, wheels about 1.08 m across, clearance 0.33 m.
- **Materials / colours (general):** a steel cab and frame, a timber-plank drop-side bed on the 130, and a canvas tilt on steel hoops on the 131. Typically a faded blue or green cab (130) or olive (131).
- **Frequency (general):** rare. There are far fewer trucks than cars.
- **Physical role:** as R0 at truck scale. The bed and the cab roof are landing spots. A burnt tilt leaves bare steel hoops over the bed, a cage-like `snag`.

### R5 Урал trucks (375, 4320)
- **Observed (O):** a burnt-out heavy truck with a bonneted cab and a vertical-slat grille. The cab door hangs open, the windscreen is gone and the cab is soot-black. Its shape matches the Урал (identified from general knowledge). The axles and wheels can't be read at the photo's size.
- **Size (general):** a 6×6, 7.35–7.37 × 2.50–2.69 m. 2.64–2.68 m at the cab and about 2.9–3.0 m with a tilt. Empty about 8.0–8.5 t. Wheels about 1.1 m across and 0.4 m wide, clearance 0.40 m.
- **Materials / colours (general):** a steel cab, a long steel bonnet, a steel frame and a canvas tilt on hoops. Typically olive. Burnt: see R0 (O, observed).
- **Frequency (general):** rare, like R4.
- **Physical role:** as R4. It is the tallest and heaviest vehicle, so it has the largest wind shadow of any vehicle.

## 6. Small objects, debris and lines

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
- **Clips:** H (a straight grey pole about 20–25 cm across and about 7–9 m tall at the belt edge, with thin posts in the distance), J (wooden posts), L (poles as silhouettes against the night sky).
- **Size:** as above.
- **Materials / colours:** grey weathered wood, or possibly concrete. Some poles carry a crossarm.
- **Frequency:** a line of them along the belt in H. Single poles and posts elsewhere (J, L).
- **Physical role:** `obstacle`, and `snag` together with their wires. Poles may lean 5–15°, so leaning variants are part of the class. At night a pole is one of the few readable landmarks (see Night (L)). **Uncertain:** it is not clear whether the H pole is wood or concrete, and whether it still carries wires.

### O3 Loose masonry and roof debris
- **Clips:** C, D, E, I, K. N (broken blocks scattered around the wreck).
- **Size:** bricks 250 × 120 × 65 mm, tile shards 0.1–0.4 m, stone slabs 0.3–0.8 m.
- **Materials / colours:** red brick (#b97541 `feed`), clay tile (#825f44 `feed`), grey stone and concrete.
- **Frequency:** clustered at wall feet and in yards.
- **Physical role:** rigid small `obstacle`s under the legs (see T6).

### O4 Timber, sheets, tarps and fragments
- **Clips:** planks in C, E, I, J, K and N. Sheet metal in N. Tarps and plastic in C (light blue) and K (white). In B, a light-coloured fragment about 1 m long lying in the field (a plank or sheet).
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

## 7. Micro-detail that must interact physically

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
| Glass crumbs and body fragments | M, N | 5–50 mm | scattered 2–20 m around wrecks | clear glass, grey metal, black plastic | Rigid and rollable under the legs. Tempered-glass crumbs make a slippery layer on hard ground. |
| Charred stubble and ash | pilot (T3b) | stumps 5–15 cm, ash layer 0–1 cm | continuous on burnt fields | black char, grey-white ash | Stiff but brittle: snaps on contact, giving short damping spikes. Ash lifts in a dark plume under prop wash (visual). |

## 8. Surface data proposal (input to the map-format spec)

Porosity values are typical for the soil type, not measured from the footage. The physics engineer and the map-format spec settle the final fields.

| Surface id | Clips | Soil | Bearing | Porosity (typical) | Cover | Cover height | Leg behaviour |
|---|---|---|---|---|---|---|---|
| `meadow_sod` | A | loam under sod | firm | 0.50–0.60 | dense mixed grass | 0.2–0.6 m | sinks 1–3 cm, cushioned, jitter |
| `dry_crust` | B | dry crusted loam/clay | hard | 0.40–0.50 | sparse dry tufts | 0.1–0.4 m | little sink, bounce, dust |
| `crater_spoil` | B, F | loose spoil | soft | 0.50–0.60 | none | — | sinks 3–8 cm, slope tilt |
| `belt_straw` | H, G | moist dark loam | soft, springy | 0.50–0.60 | lodged straw and stems | mat 5–20 cm, stems to 1.2 m | springy, uneven, legs hooked |
| `belt_bare` | F | dry fine loam | firm, loose top | 0.45–0.50 | none | — | slides, dust |
| `tilled` | G | loose furrowed soil | soft | 0.55–0.65 | straw streaks | < 0.2 m | uneven sink 2–5 cm |
| `burnt_field` | pilot (T3b), A (small scorch spots) | firm loam, unchanged under the char | firm | 0.45–0.55 | charred stubble, ash | stumps 5–15 cm | little sink (0–1 cm), brittle crunch, ash plume |
| `asphalt` | M, O (N under debris) | asphalt over a gravel base | rigid | 0.03–0.08 | dust, glass crumbs, debris | < 5 cm | no sink, hard bounce, slides on glass crumbs |
| `yard_litter` | C, E | compacted soil | hard | 0.35–0.45 | leaves, shards | 1–3 cm | slides, uneven |
| `rubble` | E, I, J, K, N | brick/tile/timber | rigid, irregular | n/a | debris | 5–50 cm | tilts, snags |
| `weeds` | D | loam | firm | 0.50 | dense tall forbs | 0.5–1.5 m | no landing (legs go deep), drag and snag |

## 9. Gaps: expected classes missing from the footage

- **Vehicles in the clips.** No vehicle appears in any clip. §5 now defines the classes from the photos M–O and the pilot's list. There is still no reference image of a ЗИЛ, a front-drive ВАЗ or a Niva, and none of a vehicle in a field or an overgrown yard, so that look is extrapolated from T7.
- **Roads and tracks.** No asphalt road in any clip. Asphalt appears only in the vehicle photos (M, O, and paved ground under N's debris). Dirt field tracks show only faintly at night (L), too dark to measure. A track may run along the tree strip in G (uncertain).
- **Burnt fields close up.** Confirmed by the pilot (T3b), but no reference shows one at flying height.
- **Power lines on pylons.** Only single wires and poles appear (C, D, H, J, L).
- **Standing crops and gardens.** No sunflower, wheat or maize field, no orchard in leaf, no vegetable rows.
- **Water and mud.** No pond, stream, puddle or wet mud. The only wet ground is the slush around the truck in O.
- **Winter ground.** No snow or frozen ground in the clips. Snow appears only in O, and bare winter trees in the photos I, J, M and O.
- **Dusk.** Night is now covered by L (see Night (L)). There is still no dusk or dawn clip. Time of day is a user setting.
- **Metal debris close up.** Torn body panels appear in the photos M and N, but no rebar, corrugated sheet or barbed wire is seen at flying range in a clip.
- **Unreadable footage.** The last second of G is too smeared by the feed to classify.

## 10. Map composition

**Pilot:** the map is diverse. It has a couple of village houses with yards like the ones in the clips, and one typical destroyed five-storey Stalin-era block like the photos. The pilot will change the map later, so the map format and the models must make edits smooth. That is planned as its own authoring change.

These are the parts to include, each tied to its reference. This is a content list, not a layout. The layout belongs to the map-format and world-build changes.

| Part | Built from | Reference |
|---|---|---|
| Open rolling meadow, the main cruise ground, with small scorch spots | T1, T3b (small form) | A, L |
| Tree belts crossing the fields, several with a trench along them | V1, V2, T4, W2 | F, G, H (D; B, A and L on the horizon) |
| Dark fields, both tilled and burnt | T3a, T3b | G, pilot |
| Dry cratered field | T2, W1 | B |
| Village: two houses with yards (adobe and brick), sheds, a cellar, fruit trees, weeds, fences, wires and poles | B1, B2, B3, W4, V3, V4, T6, T7, B6, O1, O2 | C, D, E, J |
| Urban edge: the one destroyed Stalin-era block with its rubble mound | B4, B5, T8, O5 | I, K |
| Vehicles, abandoned and destroyed, on roads and tracks, in yards and at field edges | R0–R5 | M, N, O, pilot |

---

## 11. Ranked M1 build list

1. **Tree-belt edge patch.** A belt about 20 m wide with trunks, understory and part-bare crowns, a straw-mat floor over dark soil, and a grass margin. Reason: it is in F, G, H and D and on every horizon (A, B, L), and it packs the most snag, wind-shadow and landing physics into one piece.
2. **Straw-mat and stem micro-detail layer.** Lodged straw, standing stems and twigs over dark soil, with instanced physical stems near the drone. Reason: H is a long low flight over exactly this, the manifesto's straw-level landing jitter.
3. **Damaged adobe house with a stripped roof, an enterable door and a far window.** Reason: E flies in through the door and sees daylight through the far window. It is the core fly-in structure and the first of the pilot's couple of village houses.
4. **Trench along a narrow tree strip.** Reason: G flies it low and fast, and the pilot confirms trenches are common. It is the only narrow ground corridor in the clips.
5. **Vehicle kit, first pass: a classic ВАЗ sedan (R1) and a Урал truck (R5), each in the abandoned and the destroyed state (R0).** Reason: the pilot names both makes as typical, and M and O show one of each. They add a new class of obstacle with a wind shadow and torn-metal snags that no clip covers.
6. **Open rolling meadow patch.** About 500 m of sod and mixed grass with hidden pitfall and scorch patches, and belts on the horizon. Reason: A is the main open cruise-and-descend surface, L shows the same land at night, and M1 noclip needs the wide view.
7. **Dry cratered field patch.** Craters 1–4 m with loose rims on crusted loam with sparse tufts. Reason: B shows it end to end, and it is the hard-surface bounce and crater-tilt case.
8. **Dark field patch, tilled and burnt variants.** Reason: G shows one, and the pilot confirms both kinds are common. It is the soft-furrow landing case and the brittle-char landing case.
9. **Village yard set: fruit tree, brick cellar entrance, shed, leaf litter, fence.** Reason: C ends by flying into the cellar door, the narrowest fly-in gap in the footage, and the pilot wants the houses with yards like in the clips.
10. **Destroyed five-storey Stalin-era block with its rubble mound.** Reason: the pilot wants exactly one, like I and K. It is the largest obstacle and turbulence source on the map, and its open rooms are fly-ins.
11. **Core texture set.** Dark chernozem, dry loam, char and ash, adobe render, red brick, silicate brick, clay tile, weathered grey wood, dry straw, faded car paint, soot-black burnt metal, rust, asphalt. Reason: every clip and photo is built from these. Author them at photo albedo and check them through the feed against the `feed` hex values above.
12. **Debris and rubble kit.** Bricks, tile shards, planks, sheets, tarp, branches, glass crumbs, body fragments, a rubble mound. Reason: it lies around every structure in C, D, E, I, J and K and every wreck in M and N, and it drives uneven landings and snags.
13. **Wires and poles, including leaning variants.** Reason: C, D, E and J all have thin lines that are nearly invisible on the feed, the main surprise fiber-snag hazard. Poles are also night landmarks (L).
14. **Brick village house variant.** Silicate brick, asbestos roof, painted frames. Reason: J shows it is as common as adobe, and it is the second of the pilot's couple of houses, so the village isn't a single repeated house.
15. **Vehicle kit, second pass: front-drive ВАЗ and Niva (R2), Lanos, Chevrolet, Chery and VW class cars (R3), ЗИЛ trucks (R4).** Reason: these are on the pilot's list, and N shows the small-hatchback class. They give the variety the pilot asked for once the first-pass states are approved.
