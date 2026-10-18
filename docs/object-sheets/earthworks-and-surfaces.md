# Object sheet: earthworks and surfaces (E01–E07, S01–S13)

**Owner:** reference-analyst. **Reviewed for buildability by:** asset-artist (task 1.7, `openspec/changes/p1-object-sheets/review-buildability.md`).

**OPSEC:** cite clips and photos by letter only. Describe classes, never a specific identifiable building, place, date or person. No file names, and no OSD values.

**Requirements:** AST-02, AST-04, AST-05, AST-06, AST-10, AST-11, REAL-01…05, WLD-02, WLD-03, WLD-06, SEC-01, SEC-02, PRC-06 · **Decisions:** D-013, D-020, D-021, D-024, D-028 · **Change:** `p1-object-sheets`, task 1.3.

## How this sheet was made

- **Letters read,** through contact sheets only: A (16 stills at 1/s, 1–15 usable), B (24 at 2/s, re-extracted; 1–16 used), F (24 at 2/s; 1–17 usable), G (15 at 4/s; 1–12 used), H (24 at 1/s; 1–21 usable), L (14 at 2/s), P (48 at 1/s on two sheets, re-extracted). Single stills for measurement and colour: A 1, A 7; B 1, B 4, B 5, B 10, B 12; F 16, F 17; G 3, G 9; H 16; L 2; P 12, P 23, P 40. Colour regions were also sampled on A 4, A 12, B 16, F 4, F 8, F 10, H 10, H 12, H 14, P 3, P 36 and P 47 without viewing them. The stills not used show signal loss or nothing of this group.
- **Letters cited, not re-read:** C, D, E, I–K, M–O (surfaces S08–S11). Their values come from `docs/reference-notes/terrain.md` (T6–T8, B5, R0) and `docs/object-sheets/vegetation.md`; the houses-and-yard and lines-roads-debris analysts read those letters.
- **Reused, not re-measured:** terrain.md §1 (T1–T8), §3 (W1–W3), §7 (micro-detail) and §8 (surface ids); knowledge.md §7.1–7.4 (the contact law, the surface table, stems and hooks, object materials), §9.2 and §10.1–10.6 (map package, queries, holes); the first attempt's `docs/legacy/map-and-world/surfaces.json` (cover blocks, albedos), `surfaces-review.md` and `terrain-holes-review.md` (the hole-filler contract F-1…F-8, the soil lip, the trench cavity wind model). Where this sheet gives a number terrain.md or knowledge.md already gives, they are the source.
- **Scale cues:** the drone's own arms and props in the lower corners of every still (0.574 m tip to tip), the trench width (0.6–1.0 m, terrain.md W2) in G, the light fragment about 1 m long in B (terrain.md S4), the 3–8 cm branches over the pit in F (vegetation.md V19), the 15 cm drill rows of a grain stubble (published, below) in P, thin poles and belt trunks on the horizon.
- **Labels:** every number is **measured** (from a letter, with still numbers), **published** (with its source), **derived** (with the derivation), **assumed**, or **carried** (a starting value from the first attempt's surface table, K§7.2, which the physics engineer tunes; it counts as assumed).
- **Feed check values** are mean sRGB of hand-picked regions on the extracted stills (this sheet, terrain.md, vegetation.md). They carry the feed's shift:
  - **B and G** are low sun through a warm, saturated feed: a pale grey-brown crust reads #c78534.
  - **A and H** are overcast and flat.
  - **P** was flown in rain under overcast. Its frames are dark (frame luma about half the dry clips', K§8.7 R5) and saturated red-brown. P values are the **wet check**, never a dry albedo.
  - **L** is black and white.
  - Textures are authored at photo-like albedo; the feed (tech-artist) adds the shift; the REAL-03 sheets compare the result against these values.

### Research used across the group (published)

| Topic | Finding used here | Source |
|---|---|---|
| Topsoil and subsoil | Chernozem: a black, humus-rich soil on loess, its dark layer from several centimetres up to 1.5 m thick; the dark surface sits over a brown or cinnamon subsoil with secondary carbonates, where filled burrows show as black spots; moist chroma ≤ 2 in the dark horizon | https://en.wikipedia.org/wiki/Chernozem · https://www.isric.org/sites/default/files/major_soils_of_the_world/set8/ch/chernoze.pdf |
| Moist against dry soil colour | Soil colour is read moist and dry; the moist colour is 1–2 Munsell value steps darker | USDA Soil Survey Manual, ch. 3: https://www.nrcs.usda.gov/resources/guides-and-instructions/soil-survey-manual |
| Angle of repose | Soil 30–45°; wet excavated clay 15° | https://en.wikipedia.org/wiki/Angle_of_repose |
| Explosion craters | Depth about a quarter of the apparent diameter, varying with soil; the apparent rim stands above the old surface; ejecta settles back into the bowl | https://amcaonline.org.ar/ojs/index.php/mc/article/download/569/542 · https://armamentresearch.com/an-introduction-to-crater-analysis/ |
| Field trenches | Slit: 1.0–1.2 m wide at the top, 0.8 m at the bottom, about 1.5 m deep, straight legs no longer than 4–5 m, zigzag, an entrance of 5–6 steps at 90°; a battle trench about 0.75 m wide and 1.5 m deep; recent ones about 2 m deep and about 1 m wide, revetted with panels of sawn timber and steel mesh (75 mm) over geotextile | https://dic.academic.ru/dic.nsf/ruwiki/1589715 · https://thinkdefence.co.uk/?p=67262 |
| Revetment | Hurdles of woven saplings about 0.9–1.2 m high and 1.8–2.7 m long between upright poles, anchored by pickets driven into the parapet | https://tnsos.net/TSLA/cwsourcebook/glossary.php?alph=h |
| Dugouts | A covered shelter against plunging fire, its cover on timber or earth supports; a frame of round timber 16–22 cm across, a roof of one or more layers of logs, roofing felt or polyethylene film over them, then an earth cushion; the entrance turns 90° | https://booksite.ru/fulltext/1/001/007/012/12818.htm · https://ru.wikipedia.org/wiki/Блиндаж |
| Trench flow | A narrow cavity under a cross-wind skims: one primary vortex fills it, with a secondary vortex under it at depth/width ≥ 2 | https://en.wikipedia.org/wiki/Street_canyon |
| Field tillage | Mouldboard bodies 0.30–0.50 m wide (the furrow pitch), working depth 0.15–0.35 m; grain drill rows 15 cm on a 3.6 m drill; a combine's straw falls through a 1.5 m window into a windrow behind a 5–6 m header; wheat stubble 10–20 cm (13–35 cm in trials) | https://agro-masz.eu/en/strona-glowna/555-pj-single-beam-plough.html · https://learn.ztu.edu.ua/mod/resource/view.php?id=234541 · https://rep.bsatu.by/bitstream/doc/16458/1/2009_6.1.opredelenie.pdf · https://rae.agriculturejournals.cz/artkey/rae-201503-0004_evaluation-of-wheat-stubble-management-practices-in-terms-of-the-fuel-consumption-and-field-capacity.php · https://cigrjournal.org/index.php/Ejounral/article/view/3360 |
| Stubble burning | Stubble burns to gases and light-coloured ash; windrows burn hotter than the stubble between them | https://eupdate.agronomy.ksu.edu/article/factors-to-consider-before-baling-or-burning-wheat-residue-289 · https://www.agric.wa.gov.au/grains/windrow-burning-tips-experts |
| Road plates | ПАГ-14 airfield plate 6.0 × 2.0 × 0.14 m (ПАГ-18: 0.18 m), heavy concrete, riffled top at least 1.5 mm deep; ПДН road plate 6.0 × 2.0 × 0.14 m, for permanent and temporary roads | https://ru.wikipedia.org/wiki/Аэродромная_плита · https://productcenter.ru/products/92973/pag-14 · https://productcenter.ru/products/121633/dorozhnyie-plity-1p-2p-pd-pdn |
| Rural road widths | Category IV: carriageway 6.0 m, shoulders 2.0 m; category V: one 4.5 m lane, shoulders 1.75 m | SNiP 2.05.02-85, table 4: https://zakonbase.ru/content/part/384684 |
| Potholes | At least 150 mm across; severity by depth: ≤ 25 mm low, 25–50 mm moderate, > 50 mm high | https://pmc.ncbi.nlm.nih.gov/articles/PMC9029655/table/sensors-22-03044-t001 · https://faapaveair.faa.gov/Help/Documents/levelsofseverityforpotholes.htm |
| Wheel gauges (ruts) | Classic sedan track 1.30–1.35 m; bonneted truck 1.80 m; wheeled tractor rear track 1.5–2.1 m, rear tyres about 0.47 m wide | https://en.wikipedia.org/wiki/VAZ-2101 · https://en.wikipedia.org/wiki/ZIL-130 · https://www.prom.uz/ads/traktor-belarus-mtz-801/ |
| Broadband albedo | Fresh asphalt 0.04, worn asphalt 0.12, bare soil 0.17, green grass 0.25, new concrete 0.55 | https://en.wikipedia.org/wiki/Albedo |
| Soil strength and moisture | Cone index falls with moisture content (a decreasing, near-linear fit across soils) | https://cigrjournal.org/index.php/Ejounral/article/download/1413/1557/5559 |

### Reference views for this group (REAL-02)

Viewpoint heights are estimated from the scene (the drone's props, tussocks, crater rings, trunks), never from the display.

| Letter | Scene type | Viewpoint | Framing | Light | Time of day | Weather |
|---|---|---|---|---|---|---|
| A | open rolling steppe meadow, belts as thin lines on the far horizon, small dark hollows in the sod | tens of metres, descending toward the grass (a 2–5 m colour patch spans about a fifth of the frame in A 7; single tussocks resolve in A 13–15) | forward-down, the horizon in the upper third, the belt line at the left edge | overcast, soft, no shadows | daytime | overcast, dry, late summer to autumn |
| B | a dry sun-baked field pocked with shallow craters, a lumpy earth bank with saplings on it along the left edge, a sparse strip of thin broken trees on the horizon | 1–3 m, slow forward (crater rings 2–4 m read at 5–30 m; the 1 m fragment in B 10–14) | forward, 0° tilt, the horizon at mid-frame, the bank and the strip at the left edge (B 1–6, B 9–16), the field filling the lower half | low warm sun from the side and behind, long shadows in the crater lips | early evening or morning | clear, dry, dusty |
| F | the floor of a dense belt: bare dusty loam with twigs, then a round dug pit with branches across it | 1–2 m along the corridor (F 1–14), then 0.5–1.5 m over the pit, looking down into it (F 15–17) | forward, then forward-down into the pit | low sun through the branches, brown dust haze at 0.5–3 m | afternoon | dry, dusty |
| G | a narrow winding trench with broad grassed parapets, between an openwork tree strip and a dark field with straw streaks | 2–5 m above the parapets, fast | forward-down; the trench from the bottom centre to the horizon, the strip on the left, the dark field on the right (G 1–12) | low warm sun, long shadows, saturated straw | early evening or morning | clear, dry |
| H | a belt edge over a lodged straw mat on dark soil, bare soil patches and a short-grass strip | 0.3–1.5 m, slow | forward, 0° tilt; the straw mat fills the lower half, the belt wall the upper (H 1–21) | overcast, flat | daytime | overcast, dry, autumn |
| L | open rolling fields at night; a pale dirt track running ahead; a lone tree, a bush and a pole as silhouettes on a crest | 1–3 m (the bush and the tree on the crest at 50–150 m) | forward, 0° tilt, the horizon mid-frame, the track from the bottom centre (L 1–8) | none; the sky is brighter than the ground | night | overcast |
| P | wet stubble fields with straw windrows and drill rows, a dirt track along a belt, green patches of new growth | tens of metres, lower in P 30–42 where drill rows resolve | forward-down, the horizon in the top quarter and strongly bent by the lens | overcast, dim | daytime | rain and severe wind, wet ground |

The REAL-03 side-by-side sheets for these views are made in `reference/_compare/` once the first surfaces and earthworks are built; nothing exists to compare yet.

## Rules shared by every item in this group

### Ground is geometry (REAL-05, AST-05, D-028)

Every earthwork and every surface relief is geometry that the renderer and the physics read from the same source (K§10.1, §10.4). Four layers do it, and each asset says which it uses:

1. **The heightfield** (a 1 m vertex grid in the first attempt's gallery): relief, banks, parapets and craters of 4 m and more.
2. **Sub-grid analytic features** that `SampleGround` evaluates and the renderer draws from the same function: micro-relief, directional ridges (furrows), hashed pitfalls, and **placed features** (the first attempt's deferred X-9: a centre, a radius, a depth and a profile) for craters 0.3–4 m, ruts and potholes. Profiles use integer powers only (the determinism rule: no `Pow`).
3. **Holes and fillers** (`holes.png` on the 0.5 m surface grid; filler primitives whose material is `surface:<id>`) for anything with steep or vertical walls or an overhang: trenches, the pit, dugout entrances. The first attempt's hole-filler contract applies unchanged (terrain-holes-review §3):
   - F-1 sealed below; F-2 the cavity at least 0.25 m inside the hole cells; F-3 the lip level within ±0.03 m; F-4 nothing proud of the terrain outside the hole; F-5 shapes at least 0.3 m thick behind an open face and overlapping 0.2 m; F-6 open faces within 0.05 m of the visual; F-7 surfaces per face; F-8 no wind obstacle.
   - **Consequence for the look (derived):** with 0.5 m cells and the 0.25 m margin, a 0.8 m trench needs a hole band 1.5–2.0 m wide, so a level berm 0.25–0.6 m wide lies between each lip and its parapet's inner toe. Real trenches have that berm (it keeps the spoil from sliding back in), so the contract and the look agree.
4. **Micro-detail** (stems, straw, twigs, litter) from the shared deterministic generator at 0 mm parity (AST-10): instanced visuals everywhere, physical elements within a few metres of the drone.

Normal maps, parallax and decals carry only detail below 1–2 cm with no physical role: crust cracks, aggregate, ash film, stains, fragment scars on asphalt.

### Soil: one profile under every surface

- **Topsoil** (published, research table): chernozem-type loam, dark grey when dry, near-black when moist; the dark layer ranges from centimetres to 1.5 m (published); default 0.5 m, range 0.3–1.0 m for the generator (assumed: eroded field crests thinner, belts and hollows thicker).
- **Subsoil** (published): brown to cinnamon loess with secondary carbonates, darker filled burrows.
- **Spoil from a cut** mixes the two in proportion to the cut depth (derived): a 0.3 m scrape throws dark spoil; a 1.5 m trench or a deep crater throws about one third dark and two thirds pale. **Fresh spoil from deep cuts is paler than the field**, shallow spoil is darker; both weather to the field's colour in a season. B shows pale, lumpy spoil on its bank (B 1–6) and pale crater rims (B 4–12); F's pit spoil is the belt floor's colour (F 16).
- **Munsell to sRGB** (derived, ±5 per channel; for photo albedo targets):

| Munsell | sRGB | Where |
|---|---|---|
| 10YR 2/1 | #33302b | moist topsoil, trench walls after rain |
| 10YR 3/1 | #4a463f | dry topsoil, tilled field |
| 10YR 3/2 | #514535 | moist spoil, wet track |
| 10YR 4/2 | #6b5e4f | dry trampled topsoil, belt floor |
| 10YR 5/3 | #8a7660 | dry crust, compacted track |
| 10YR 5/4 | #8d7457 | moist subsoil, fresh deep spoil (wet) |
| 10YR 6/3 | #a39280 | dry crust in sun, dust |
| 10YR 6/4 | #a88f70 | dry subsoil, fresh deep spoil |
| 10YR 7/3 | #bcab93 | dust film, dry carbonate-rich spoil |

- **Wet darkening** (published, USDA): moist colour is 1–2 value steps darker, so a wet surface's albedo is about 0.5–0.7 of its dry albedo in luminance, a little more saturated (derived from the table above: 10YR 4/2 → 3/2 is 0.66 in luminance).
- **Slopes:** loose soil stands at 30–45° (published); spoil heaps and crater walls are built at 35–40°, fresh spade-cut walls at 75–90°, and walls of collapsed trenches at 30–40° (derived from the repose range).

### Physics: surface rows, wet variants, ids

- **Surface rows** are the physics engineer's (K§7.2, `surfaces.json` in the map package). Each S row below cites its starting values and the first attempt's cover block (carried). The three missing rows (S11 asphalt, S12 concrete slab, S13 dirt track) are **proposed** with their derivations.
- **Stability bound** (derived from K§7.1): the stiffest soil branch at the reference foot must stay at or below 26.5 kN/m (ω·dt ≤ 0.10 at 1 kHz). Bearing × unload ratio × foot area: 1.0e8 × 1.5 × 1.77e-4 m² = 26.5 kN/m. Hard surfaces (asphalt, concrete) therefore sit at the rubble cap (bearing 1.0e8, unload 1.5) and let the leg's own spring set the bounce, as K§7.4 does for rigid objects.
- **Wet variants (D-021).** "Rain" and "Severe rain" make the soils softer, stickier and the mats heavier. Proposed (assumed factors, guided by the published fall of cone index with moisture; the physics engineer tunes them):

| Field | Wet rule (applied × `wetness`) | Why |
|---|---|---|
| `bearing`, `damping` | × 0.4 (soils); × 0.6 (sod, roots carry); × 0.25 (dirt track, crater spoil: water ponds in ruts and bowls); × 1 (asphalt, slab, rubble) | soil strength falls with moisture |
| `max_sink` | × 1.6 (soils); × 1 (hard) | softer ground, deeper prints |
| `unload_ratio` | × 1.5 | wet loam is more plastic: prints stay |
| `μs / μk` (soil) | → 0.45/0.35 on loam; 0.40/0.30 on crust turned to a slick film; asphalt 0.50/0.40; slab 0.45/0.38 | a water film on a fine surface |
| **`adhesion`** (accepted as the surface table's field `adhesion_pa`, phase 3: the physics sign-off, review-materials.md §3) | pull-off per foot 2–10 N, default 4 N, released over 10–30 mm of lift (derived: wet-loam adhesion and suction of 2–10 kPa, assumed, over the 1.6e-3 m² that a 15 mm foot sunk 30 mm wets on its sole and sides) | the pilot's "legs stick most of the time in rain" (D-021); K's deferred X-5 |
| mats (straw, thatch, litter) | modulus × 0.6, μ → 0.35/0.28 (straw, thatch), 0.25/0.18 (wet leaves), hook release × 1.5 | wet straw softens and clings; wet leaves slide |
| `dust_emission` | → 0 | no dust from wet ground |
| visual | albedo × 0.55–0.7 in luminance, +10–20 % saturation, roughness −0.2 on soils, a thin wet sheen on asphalt and slab | wet darkening, above; P's wet ground stays dark and keeps its colour (K§8.7 R4) |

  `wetness` is proposed as 0.6 for Rain and 1.0 for Severe rain (assumed). No reference shows standing water (terrain.md §9), so the wet variant has no puddles; ruts and crater bottoms go to mud through their lower factors.
- **Physics ids for earthwork shapes** (settled by the physics sign-off, review-materials.md §1, §6, in the archived change `p1-pipeline-hardening`):
  - **`surface:<id>`** is the material of every shape that stands in for ground (the first attempt's TH-4): a filler wall then follows the same elasto-plastic soil law, sink and ploughing as the terrain, with the 0.02 m soil lip radius as a physics constant.
  - **`soil` is mapped, not added:** cut walls, floors and stairs `surface:belt_bare` (F-7); loose spoil and fresh mounds `surface:crater_spoil`; a grassed mound `surface:meadow_sod`.
  - **`straw` is an object id** for straw carried by a mesh or a shape: windrows built as geometry, straw on a dugout roof, straw spread in a trench (490 N/m, belt_straw's mat at 12.5 cm under the reference foot, surfaces-review §3.1). Straw over ground is `surface:belt_straw`.

  Timber revetment, posts, boards and logs use `timber`; wattle rods use `deadwood`; welded mesh uses `steel`; a hanging tarp or blanket uses `fabric`.

### Feathered transitions (PR-11, WLD-03, AST-11)

No surface area is a polygon with a hard edge. Every outline is noise-displaced, and both the visual splat and the physical surface and cover layers blend across it. The physical blend is at least the 4-cell bilinear blend of `SampleGround` (0.5–1 m); the visual feather is the width below.

| Edge | Visual feather | Outline noise (amplitude @ wavelength) | What the edge looks like (reference or derivation) |
|---|---|---|---|
| meadow ↔ belt_straw (belt edge) | 2–5 m | 1–3 m @ 10–30 m | sod grades into lodged straw under the outer crowns; tall stems thicken toward the belt (H 1–21, G 1–12) |
| meadow ↔ tilled, or ↔ stubble | 0.3–1 m | 0.5–2 m @ 20–60 m | a field edge is ploughed straight but ragged in detail; a headland strip 2× the implement width (6–12 m, derived from the 3–6 m implements) where the furrows or drill rows turn 90° |
| tilled ↔ burnt | 0.2–1 m char gradient | 2–10 m @ 10–50 m | a wavy fire front with unburnt islands 1–20 m and tongues along windrows (terrain.md T3b; windrows burn hotter) |
| meadow ↔ dry_crust | 3–10 m | 2–5 m @ 20–50 m | sod thins into crust with tufts (B 1–16 has no sharp edge) |
| any ↔ crater_spoil | 0.5–2 m | lobed, 0.2–0.5 × R | the ejecta apron thins outward to 1.5–2.5 crater radii; rays on fresh craters |
| belt_straw ↔ belt_bare | 1–3 m | 1–2 m @ 5–15 m | the straw mat stops where the crowns close and the floor is shaded and trampled (F 1–14) |
| yard_litter ↔ weeds ↔ meadow | 0.5–2 m | 0.5–1 m @ 3–10 m | trampled paths and wall feet (terrain.md T6, T7) |
| asphalt ↔ shoulder | 0.1–0.5 m broken edge | 0.1–0.3 m @ 1–5 m | a ravelled edge breaking into a dirt or sod shoulder 1.75–2.0 m wide (published widths) |
| concrete_slab ↔ ground | none on the slab (it is a plate with real edges), 0.3–1 m of disturbed soil and weeds beside it | per plate | plates are geometry; the soil beside them is trampled, with weeds in every joint |
| dirt_track ↔ meadow | 0.3–1 m | 0.3–1 m @ 5–20 m | the verge of flattened grass; a grass median between the ruts (P 11–12, L 1–8) |
| rubble ↔ yard or trampled ground | 1–5 m | lobed by the collapse | rubble thins outward from the wall foot (terrain.md B5) |

### Materials: CC0 candidates, group-wide

No ground set is in `tools/assets/manifest.json` yet (it holds `red_brick` only). Candidates, all CC0, for the asset-artist to fetch and pin; the match is by look, then retinted to the photo albedo targets in each row:

| Use | Candidate set (CC0) | Source page | Derived textures |
|---|---|---|---|
| dry grass thatch, matted pale grass, lodged straw | TextureCan Ground 0018 (dry straw grass / hay) | https://www.texturecan.com/details/190/ (licence: https://www.texturecan.com/terms/) | straw tint; a lodging-direction mask |
| green sod patches (far layer only) | ambientCG `Grass001`, `Grass004` | https://ambientcg.com/view?id=Grass001 · https://ambientcg.com/view?id=Grass004 | desaturated olive tint to the A values |
| dark moist soil under straw and sod | Poly Haven `brown_mud_02`, `brown_mud_03`, `brown_mud_leaves_01` | https://polyhaven.com/a/brown_mud_02 (and siblings) | 10YR 2/1–3/1 tint |
| dry crust, cracked loam | Poly Haven `dry_ground_01`, `mud_cracked_dry_03` | https://polyhaven.com/a/dry_ground_01 · https://polyhaven.com/a/mud_cracked_dry_03 | 10YR 5/3–6/3 tint; crack scale to 0.1–0.5 m polygons |
| loose spoil, clods | Poly Haven `brown_mud_dry`, `brown_mud_rocks_01`, `dirt_aerial_02` | https://polyhaven.com/a/brown_mud_dry (and siblings) | topsoil/subsoil mix mask |
| beaten bare loam, dusty floor | Poly Haven `dirt_floor`, `forest_ground_04`, `park_dirt` | https://polyhaven.com/a/dirt_floor · https://polyhaven.com/a/forest_ground_04 · https://polyhaven.com/a/park_dirt | dust film mask |
| ploughed and farm soil | Poly Haven `dry_mud_field_001`, `aerial_mud_1` | https://polyhaven.com/a/dry_mud_field_001 · https://polyhaven.com/a/aerial_mud_1 | furrow-direction mask; straw streak mask |
| burnt ground | Poly Haven `burned_ground_01` (charred soil, ash, scorched earth) | https://polyhaven.com/a/burned_ground_01 | char / ash / unburnt mask |
| leaf litter | Poly Haven `leaves_forest_ground`, `dry_decay_leaves`; ambientCG `ScatteredLeaves009` | https://polyhaven.com/a/leaves_forest_ground · https://ambientcg.com/view?id=ScatteredLeaves009 | leaf silhouettes come from the vegetation leaf cards (AST-10, K§14.4) |
| rubble ground | Poly Haven `concrete_debris`; ambientCG `Ground111` (rubble, brick debris), `Ground107` (brick rubble path) | https://polyhaven.com/a/concrete_debris · https://ambientcg.com/view?id=Ground111 · https://ambientcg.com/view?id=Ground107 | brick/tile/plaster colour mix from the houses sheet |
| worn and damaged asphalt | Poly Haven `road_damaged`, `road_damaged_2`, `asphalt_02` | https://polyhaven.com/a/road_damaged · https://polyhaven.com/a/road_damaged_2 · https://polyhaven.com/a/asphalt_02 | crack / patch / dust / pothole-rim mask |
| weathered concrete plates | Poly Haven `worn_concrete_floor`, `concrete_floor_worn_001`, `cracked_concrete` | https://polyhaven.com/a/worn_concrete_floor (and siblings) | a riffled-top normal map (derived, under 2 mm deep); stain and moss mask |
| dirt track with a grass median | Poly Haven `grass_path_2`, `grass_path_3` | https://polyhaven.com/a/grass_path_2 · https://polyhaven.com/a/grass_path_3 | rut / median / verge mask |

### Shared generator: surface material (S01–S13)

One material generator per surface; the map paints its weight and the cover channels (K§10.1). Ranges are for review only; each S row gives its preset.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `base_sets` | list of set ids | from the table above | per surface | the CC0 layers blended |
| `albedo_target` | sRGB | — | per surface | tint so that the mean matches the photo albedo |
| `macro_variation` | amplitude, scale | 0–0.3, 5–100 m | 0.1, 30 m | large, low-frequency brightness and hue wander so no area reads flat at 50–200 m (AST-07) |
| `patch_mask` | share, size | 0–1, 0.5–20 m | per surface | the secondary layer (green patches, bare spots, ash, litter) |
| `detail_scale` | m per tile | 1–4 | 2 | the texture tile; never repeating visibly at 10 m (AST-07) |
| `texel_density` | px/m | 256–512 | 512 near flight paths | AST-07 |
| `directional` | azimuth, spacing | 0–180°, 0.15–8 m | per surface | rows, furrows, windrows, ruts, lodging direction |
| `wetness_response` | albedo ×, roughness Δ | 0.55–1, −0.3–0 | 0.65, −0.2 | the D-021 wet look |
| `cover_channels` | 4 × 0–1 | — | per surface | grass, straw, twigs, litter multipliers (cover.png) |
| `feather_m` | float | 0.1–10 | per edge | the visual feather (table above) |
| `outline_noise` | amplitude, wavelength | 0.1–10 m, 1–60 m | per edge | the organic outline (PR-11) |

## Earthworks

### Shared generator: the trench family (E01–E04)

One generator builds the trench along a map polyline; E02 and E03 are its parameters, and E04 is a fitting placed on it.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `path` | polyline | from the map | — | the trench axis |
| `shape` | enum | straight, zigzag, curving | curving | how the axis is rebuilt between the path points (E01 variants) |
| `leg_m` | float | 1.5–5 | 3 | zigzag leg length |
| `turn_deg` | float | 30–90 | 60 | zigzag turn at each corner |
| `sinuosity` | amplitude, wavelength | 0–2 m, 3–15 m | 0.6 m, 8 m | the curving variant's wander |
| `top_width_m` | float | 0.6–1.2 | 0.8 | lip to lip (the cut, before any revetment) |
| `bottom_width_m` | float | 0.5–1.0 | 0.6 | the floor |
| `depth_m` | float | 1.2–2.0 | 1.5 | below the lip |
| `width_jitter_m` | float | 0–0.15 | 0.05 | spade-dug irregularity along the run, drawn per metre |
| `berm_m` | float | 0.2–0.6 | 0.3 | lip to the parapet's inner toe; never less than the hole margin |
| `parapet_h_m` | per side | 0–0.6 | 0.45 field side, 0.3 belt side | parapet height |
| `parapet_w_m` | per side | 1–3.5 | from the spoil volume | parapet base width; derived from `swell` unless set |
| `swell` | float | 1.1–1.4 | 1.25 | loose spoil volume per cut volume |
| `age` | float | 0–1 | 0.8 | 0 fresh: bare loose spoil, sharp lip; 1 established: parapets grassed with a straw mat, the lip crumbled 0.05–0.1 m, clods at the wall foot (G) |
| `topsoil_m` | float | 0.3–1.0 | 0.5 | the dark band in the cut wall, over the paler subsoil |
| `niches_per_10m` | float | 0–2 | 1 | side niches (E01) |
| `revetment` | enum | none, boards, wattle, mesh | none | E02 |
| `revet_height_m` | float | 0.8–depth | depth | lined height, from the lip down |
| `collapse` | float | 0–1 | 0 | E03: walls slump toward 35°, the floor fills, the parapets flatten |
| `overgrowth` | float | 0–1 | 0 | E03: weeds and saplings in and over it |
| `dugouts_per_100m` | float | 0–3 | 1.5 | E04 fittings along the belt side |
| `seed` | int | any | per run | every draw |

Outputs: the hole-band mask (`holes.png` cells), the filler mesh and its primitives with `surface:<id>`, the parapet height deltas for the heightfield and their cover weights, gap polygons every metre and at every bend, the revetment parts with their collision and snag flags, and the LOD ladder. At 50–200 m the trench must read as G shows it: a dark winding line 1–3 px wide on the feed between two pale parapet bands (G 1–12).

## E01 Trench, open, with spoil parapets

**Seen in:** G (1–12: a narrow trench winding along the field side of an openwork strip, broad grassed parapets on both sides); B (1–6: a dark cut at the foot of the earth bank, E07). Trenches are common and a terrain class of their own (pilot, terrain.md W2). Look-lock asset (phase-1 brief).
**Research sources:** the field-trench, angle-of-repose and trench-flow rows of the research table.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| top width, lip to lip | 0.6–1.0 m | 0.8 m | measured (terrain.md W2, G); published 0.75 m (battle trench) to 1.0–1.2 m (slit) |
| bottom width | 0.5–0.8 m | 0.6 m | published (slit 0.8 m); default derived from a near-vertical spade cut |
| depth below the lip | 1.2–1.8 m (recent runs to 2.0 m) | 1.5 m | measured (terrain.md W2, G); published 1.5 m, about 2 m for recent ones |
| wall batter | 80–90° | 86° | derived: (0.8 − 0.6)/2 = 0.1 m of batter over 1.5 m |
| berm, lip to parapet toe | 0.2–0.6 m | 0.3 m | derived (the hole margin, shared rules) and assumed (spoil kept off the lip) |
| parapet height | 0.3–0.5 m | 0.45 m field side, 0.3 m belt side | measured (terrain.md W2, G 1–12); the side split is assumed |
| parapet base width, each side | 1.5–3.0 m | 2.5 m | derived: a cut of 1.05 m²/m × swell 1.25 (assumed) = 1.31 m³/m, half per side, at 0.4 m high with 22° grassed slopes → 2.6 m; measured (G 3, G 9: each parapet reads 2–4 times wider than the trench) |
| zigzag legs | 1.5–5 m | 3 m | published (straight legs no longer than 4–5 m) |
| zigzag turns | 30–90° | 60° | assumed |
| curving wander | bends of 15–40° every 3–8 m | 25° every 5 m | measured (G 1–12) |
| run length | 20–300 m | 80 m | assumed (G's run outlasts the clip, more than 50 m) |
| side niches | 0.6–1.0 m wide, 0.5–0.7 m high, 0.5–1.0 m deep, sill 0.1–0.2 m above the floor | 0.8 × 0.6 × 0.7 m, 1 per 10 m | assumed |
| spade facets on the walls | 0.2–0.3 m | — | assumed (a spade blade); normal map only (below 2 cm of relief) |
| lip | crumbled 0–0.1 m, soil lip radius 0.02 m | — | assumed: a physics constant (terrain-holes-review §2.4) |

### Construction and materials
- Cut by spade or excavator into the field edge. The walls are undisturbed soil: a dark topsoil band (`topsoil_m`, 0.5 m) over the paler subsoil. G's walls are in shade and read uniformly dark; the band shows only in a sunlit or fresh wall (derived from the soil profile).
- The floor is trampled soil with spilled clods, blown-in straw and a few twigs.
- The spoil lies on both sides as broad, low parapets beyond a narrow berm. In G they are grassed over with dry grass and a lodged mat, so G's trench is established, a season old or more (measured, G 1–12). A fresh trench has bare, lumpy spoil (S03) with clods 0.1–0.4 m.
- **Materials:** walls and floor: the S05 belt_bare material retinted to the cut-wall colours below, with a wall mask (topsoil band, root fringe in the top 0.2 m, clod spill at the foot) and a spade-facet normal map; parapets: S04 belt_straw when established, S03 crater_spoil when fresh.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| inside, walls and floor in shade | #4a463f (10YR 3/1, derived) | #56432e (G 3), #4d4231 (G 9); terrain.md #493f32 | measured on G |
| parapet dry grass, sunlit | #b49c6a (assumed, dry straw) | #d39641 (G 3, field side), #c89049 (G 3, belt side); terrain.md #d2953f | measured on G |
| subsoil band in a fresh or sunlit wall | #a88f70 (10YR 6/4, derived) | — | derived |
| fresh spoil, mixed | #8a7660 (10YR 5/3, derived) | #bc853d (B 1, bank spoil, sunlit), #9b6f39 (B 1, its shaded side) | measured on B |
| wet walls (D-021) | #33302b (10YR 2/1, derived) | — | derived |

### Variants and states
- **Variants:** straight, zigzag, curving (asset list); G is curving.
- **States:** fresh (`age` 0), established (`age` 0.8, G), revetted (E02), collapsed and overgrown (E03); wet (D-021): darker walls, mud on the floor, sticky.

### Damage pattern
- A crater (E05) on or beside the trench cuts the parapet and slumps the walls over 1–4 m; the corridor fills to about half depth with loose spoil there, and a gap opens in the parapet (derived from the crater sizes). Burnt parapets where a field fire reached the trench (S07).

### Physics role
- **Collision:** a hole band along the trench, filled with filler primitives per 1–2 m straight segment (≤ 1 m on curves, so the faces stay within 0.05 m of the visual): a floor box, two wall boxes at the batter, level lip boxes, each at least 0.3 m thick behind its face and overlapping 0.2 m (F-1…F-6; F-7 and F-8 below; the asset's audit prints all eight, AST-05). The asset carries a `holes` record (centre line, top width, depth, band width: the p1-e01-trench design, decision 6). The parapets stay terrain: the heightfield raised 0.3–0.5 m on its 1 m samples, with the surface's cover (F-7).
- **Material ids:** walls and floor `surface:belt_bare` (F-7), its wet variant in rain; parapets `belt_straw` or `crater_spoil` terrain (the physics sign-off's mapping of soil, review-materials.md §6).
- **Gaps:** the corridor: a cross-section gap polygon every metre and at every bend (the clear trapezoid from the floor to the lip), and the open top slot along the run, written as one polygon per filler segment (1–2 m, ≤ 1 m on curves) so that each stays planar within the pipeline's 10 mm on rolling ground. A polygon's `narrowest_m` is its inscribed circle (the pipeline convention), so the table below, not `narrowest_m`, gives the drone's clear width at each height. The drone's side-to-side span equals its nose-first span, 0.574 m, for a square X frame; its yawed span reaches 0.706 m at 45°.

| Cut (top / bottom / depth) | Width at the floor / at 0.35 m (props of a landed drone) / at the lip | Along it, nose-first | Yawed 45°, or through a zigzag corner |
|---|---|---|---|
| narrow 0.6 / 0.5 / 1.5 m | 0.50 / 0.52 / 0.60 m | only in the top 0.39 m below the lip, 13 mm a side at the lip: barely | never below the lip |
| **default 0.8 / 0.6 / 1.5 m** | 0.60 / 0.65 / 0.80 m | everywhere: 13 mm a side at the floor, 113 mm at the lip | only in the top 0.7 m |
| wide 1.0 / 0.8 / 1.8 m | 0.80 / 0.84 / 1.00 m | everywhere | everywhere, 47 mm a side at the floor |

  (Derived: width at height h = bottom + (top − bottom)·h/depth.) The default is PR-9's "a little bit wider than the drone" when flown inside, and its zigzag corners can be turned only near the lip.
- **Wind volumes:** none (F-8). Inside the cut, physics applies its cavity model (terrain-holes-review §2.6): depth/width 1.5–2.5 is a skimming cavity with one primary vortex, and a secondary vortex under it once depth/width ≥ 2 (published, street-canyon flow). The parapets are terrain, so the wind grid sees them as terrain shelter: a calmer strip about 2–5 m wide on the lee side of a 0.45 m parapet (derived: 5–10 heights, the belt rule of terrain.md V1 applied to a solid ridge).
- **Snag hazards:** no stem hooks on the walls (legacy §2.3); roots in cut walls are deferred (X-10). The lip is the fiber's hazard: dragged back over it, the fiber's tension grows by the capstan factor e^(μθ), 1.9–3.5 times over 90° (legacy §2.4).
- **Landable surfaces:** none flagged (asset list). The floor is soil ground, but a landed drone's props clear the default walls by 13–40 mm a side: a crash case, not a landing spot. The parapets are ordinary ground (S04 or S03).

### Placement rules (for the world)
- Along the field side of belts and narrow strips, 1–5 m out from the outer trunk line, following the belt's line with its own wander (G; terrain.md §10). Also along field edges and between belts (assumed).
- Runs of 50–300 m, several per belt system, with dugouts (E04) on the belt side every 30–100 m and side niches facing the open field (assumed).
- The parapets blend into the local surface over 1–2 m; their cover is the local meadow or straw.

### Generator parameters
The shared trench-family table, with `revetment` none, `collapse` 0, `overgrowth` 0; the three shapes are `shape` straight, zigzag, curving.

### Open points
- Floors of boards or pallets in long-held runs: assumed (research names sawn-timber revetment panels); not visible in G at its height. Kept as an E02 option (`revetment` boards also floors the run), settled at the G2 review if the pilot says otherwise.
- The cavity model's mean in-cavity wind fraction: the physics engineer's (legacy §2.6 leaves it to tuning).
- Whether the topsoil band reads in a sunlit wall at flying height: settled at the REAL-03 review against G.

## E02 Trench, revetted (boards, wattle, posts)

**Seen in:** **R** (no letter shows a lined wall; G is too far and too fast to see one).
**Research sources:** the revetment and field-trench rows (hurdles between upright poles; sawn-timber and steel-mesh panels over geotextile); https://en.wikipedia.org/wiki/EUR-pallet (pallet 1200 × 800 × 144 mm, for the pallet option).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| cut width (vertical walls) | 0.9–1.2 m | 1.0 m | assumed: cut wider so the lined corridor stays near the open trench's |
| depth | as E01 | 1.5 m | measured, as E01 (terrain.md W2, G) |
| posts | 80–120 mm round or 100 × 100 mm sawn, every 1.0–1.5 m, driven 0.5–0.8 m below the floor, tops 0–0.3 m proud of the lip | 100 mm every 1.2 m, tops 0.1 m proud | assumed |
| boards | 25–40 × 100–200 mm, horizontal behind the posts, gaps 0–30 mm | 30 × 150 mm, 10 mm gaps | assumed |
| pallet option | 1.2 × 0.8 × 0.144 m, stood on end | — | published (EUR pallet) |
| wattle panels | 0.9–1.2 m high, 1.8–2.7 m long; pickets 50–80 mm every 0.4–0.6 m; rods 15–30 mm woven | 1.0 m high, 2.0 m long, pickets 60 mm every 0.5 m, rods 20 mm | published (hurdles) + assumed (picket pitch, rods) |
| wattle thickness | 0.06–0.11 m | 0.08 m | derived: a picket plus a rod |
| mesh option | welded mesh, 50–100 mm cells, 3–5 mm wire, over geotextile | 100 mm, 4 mm | published (mesh and geotextile) + assumed (sizes) |
| clear width, boards | 0.64–0.88 m | **0.74 m** | derived: 1.0 − 2 × (0.10 post + 0.03 board) |
| clear width, wattle | 0.78–0.98 m | 0.84 m | derived: 1.0 − 2 × 0.08 |

### Construction and materials
- **Boards and posts:** posts driven at the wall foot inside the cut, boards laid horizontally behind them and backfilled; the posts hold the boards against the soil. A variant uses pallets on end.
- **Wattle and pickets:** hurdles of woven rods between pickets, set against the wall, the pickets anchored into the parapet (published). Rod ends stick out 0.05–0.2 m at the panel tops and ends (assumed).
- **Mesh option:** welded mesh pinned over geotextile, held by posts.
- The revetment lines the full depth or only the top 0.8–1.2 m in firm soil (assumed).
- **Materials:** boards, posts, pallets: the weathered-plank set of the houses-and-yard sheet (fences) with a fresh-pale variant; wattle: the vegetation sheet's dead-wood bark sets; mesh: a rust tint over a steel albedo; geotextile: a grey-black fabric tint (assumed).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| weathered boards and posts | #8a8478 (assumed) | #615e56 (grey weathered wood, terrain.md B6) | terrain.md (fence wood, E) |
| fresh boards | #c8b08a (assumed) | — | assumed |
| wattle rods | #6e6559 (assumed) | — | vegetation.md dead wood |
| rusted mesh | #7a5a40 (assumed) | — | assumed |

### Variants and states
- **Variants:** boards and posts; wattle and pickets (asset list: 2). Mesh and pallets are options of the boards variant.
- **States:** intact; sagging (boards bowed 0.05–0.15 m into the corridor, posts leaning 5–15°); broken (boards splintered, a post or a panel fallen across the corridor). The states change the clear width, which the generator re-measures.

### Damage pattern
- Blast and slumping push the lining inward: bowed boards, a panel torn off, a post across the corridor. Splintered board ends are geometry (REAL-05).

### Physics role
- **Collision:** the E01 fillers and `holes` record behind the lining, under the same contract F-1…F-8 (F-5's thickness behind the revetment face); posts as capsules, boards and pallets as thin boxes per board or per panel, wattle panels as one box per panel for the contact plane plus capsules for the protruding rod ends, mesh as one box.
- **Material ids:** `timber` (posts, boards, pallets), `deadwood` (wattle rods), `steel` (mesh), the E01 fillers' `surface:belt_bare` behind.
- **Gaps:** the clear width between the lining faces, written per metre; vertical walls make it constant with height. Boards (0.74 m): nose-first 83 mm a side, yawed 45° only 17 mm a side: PR-9's "barely" at the diagonal. Wattle (0.84 m): 133 and 67 mm. A leaning post or a fallen panel narrows it locally; the generator writes the narrowest width.
- **Wind volumes:** none (as E01).
- **Snag hazards:** post tops proud of the lip and wattle rod ends (the fiber, at the lip), splintered board ends, cut mesh wires at the top edge (steel). A fiber dragged along boards meets `timber`'s 2 mm arris.
- **Landable surfaces:** none.

### Placement rules
- Runs that are held long: near dugouts, at belt corners, in soft or sandy ground (assumed). 20–40 % of trench length (assumed), the rest open (E01).

### Generator parameters
The shared trench-family table with `revetment` boards or wattle (or mesh), `revet_height_m`, plus:

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `post_d_m` | float | 0.08–0.12 | 0.10 | post diameter |
| `post_pitch_m` | float | 1.0–1.5 | 1.2 | post spacing |
| `post_proud_m` | float | 0–0.3 | 0.1 | post tops above the lip |
| `board` | w × t, gap | 0.10–0.20 × 0.025–0.04 m, 0–0.03 m | 0.15 × 0.03, 0.01 | board size and gaps |
| `wattle_picket_pitch_m` | float | 0.4–0.6 | 0.5 | picket spacing |
| `wattle_rod_d_m` | float | 0.015–0.03 | 0.02 | rod diameter |
| `sag` | float | 0–1 | 0.2 | bowing and leaning toward the sagging state |
| `broken` | float | 0–1 | 0 | share of panels broken or fallen in |

### Open points
- The share of revetted runs and which lining dominates: research gives sawn timber, mesh and geotextile for recent work and wattle as the classic; the pilot confirms the mix at G2 (a qualitative look question, logged by the orchestrator only if the review sheet raises doubt).

## E03 Trench, collapsed and overgrown

**Seen in:** **R**. H's small earth mounds and bare soil hollows at the belt edge (H 1–4, H 9–14) are of this scale, but no letter shows an abandoned trench line.
**Research sources:** the angle-of-repose row (soil 30–45°).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| depth left | 0.4–1.0 m | 0.7 m | assumed (half-filled) |
| top width | 1.2–2.5 m | 1.8 m | derived: 0.7 m deep with 35° walls and a 0.2 m floor → 2.2 m; the default trims the rounded lips |
| wall slope | 30–40° | 35° | published (repose of soil) |
| parapets | 0.1–0.3 m, spread 2–4 m | 0.2 m | derived (slumped from E01's 0.3–0.5 m) |
| weeds in it | 0.5–1.5 m | 1.0 m | measured, as S08 (terrain.md T7, D) |
| saplings | 1–3 m, 0–1 per 5 m | 0.3 per 5 m | assumed (two or more seasons) |
| remains of lining | 0–30 % of the run | 10 % | assumed |

### Construction and materials
- An E01 or E02 run left for seasons: the lips crumble, the walls slump to the angle of repose, rain washes soil into the floor, and weeds, straw and saplings take it. Lining boards lean in or lie in the bottom.
- **Materials:** S08 weeds ground in the bottom, S04 belt_straw on the slopes, S03 crater_spoil where fresh slumps show; boards as E02 (weathered, grey).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| slopes under dead grass | #7a7055 (carried, belt_straw) | #777158 (H 16, straw mat, overcast) | measured on H |
| bare slumped soil | #4a463f (10YR 3/1, derived) | #67675c (H 14), #686257 (H 12), overcast | measured on H |

### Variants and states
- One variant; `collapse` 0.5–1 and `overgrowth` 0.3–1 set the state; wet as the soils.

### Damage pattern
- It is the end state; a crater in it reads as a wider pit in the line.

### Physics role
- **Collision:** the 35° slopes still need the hole and filler method: a 1.8 m trough is too narrow for the 1 m heightfield. Fillers and the `holes` record as E01, under F-1…F-8, with the walls at 35°.
- **Material ids:** `surface:weeds` in the bottom, `surface:belt_straw` on the slopes.
- **Gaps:** none: 0.4–1.0 m deep with weeds is too shallow to fly in below ground.
- **Wind volumes:** none.
- **Snag hazards:** weeds (stem hooks, 5–30 N, K§7.2 weeds row), fallen boards.
- **Landable surfaces:** none flagged; a hidden pitfall under the weeds for a landing (a leg drops 0.4–1.0 m on one side).

### Placement rules
- Older positions away from the current lines: along belts and field edges, 10–30 % of all trench length (assumed); often beside an E01 run that replaced it.

### Generator parameters
The shared trench-family table with `collapse` 0.5–1, `overgrowth` 0.3–1, `age` 1.

### Open points
- None.

## E04 Dugout entrance (log-roofed, steps down)

**Seen in:** **R**. P2.
**Research sources:** the dugout row (frame timber 16–22 cm, log layers, film, an earth cushion; small); the slit-trench row (an entrance of 5–6 steps at 90°).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| opening (clear, in the frame) | 0.7–0.9 × 1.5–1.8 m | 0.8 × 1.6 m | assumed (asset list, R: research only) |
| steps | 5–6, rise 0.20–0.25 m, tread 0.25–0.30 m | 6 × 0.22 / 0.28 m | published (5–6 steps) + assumed (rise, tread) |
| stair corridor | 0.8–1.0 m wide, 1.6–1.8 m clear, about 40° down | 0.85 m, 1.7 m | derived from the steps (1.3 m down over 1.7 m) |
| frame | two post logs and a lintel log, 0.12–0.20 m | 0.15 m | assumed |
| roof logs | 0.20–0.30 m, 2–3 layers | 0.25 m, 2 layers | assumed (a little over the published 16–22 cm frame timber; layers) |
| cover | film, then earth 0.3–0.8 m, grassed | 0.5 m | published (film, earth cushion) + assumed (thickness: a field dugout, thinner than a full shelter's 1–2.5 m) |
| mound above the field | 0.3–0.6 m, 3–5 m across | 0.4 m, 4 m | assumed |
| chamber | 2.0–3.0 × 2.0–2.5 m, 1.7–2.0 m clear | 2.5 × 2.2 × 1.8 m | assumed: small (published) and a real room so that the fly-in ends somewhere (REAL-05) |
| stove pipe or vent | 0.10–0.15 m, 0.3–0.6 m above the mound | 0.12 m | assumed (the asset list's "holes") |

### Construction and materials
- A pit dug off the trench (belt side) and roofed with logs on log walls or on the soil shoulders; a film over the logs, then earth (published), leaving a low grassed mound (assumed). The entrance leaves the trench at about 90° (published) down 5–6 steps through a log frame. A tarp or a blanket hangs in the doorway (assumed). The steps are cut in soil, some edged with a board or a log.
- **Materials:** logs: the vegetation sheet's bark sets (grey-brown, weathered) with sawn ends; film: a dark olive plastic tint at the edges; the mound: S04 or S01 cover; stair: S05 soil; tarp: the debris kit's tarp (K05).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| weathered logs | #6f6352 (assumed) | — | assumed |
| sawn log ends | #a08e70 (assumed) | — | assumed |
| film at the edges | #3a3d33 (assumed) | — | assumed |
| doorway interior | near black | — | the cellar door in C reads near black (terrain.md W4) |

### Variants and states
- One variant; states intact and damaged (the frame's lintel cracked and sagging 0.1–0.3 m, earth spilled on the steps, the tarp torn).

### Damage pattern
- A hit near the entrance: the lintel and the first roof logs break, earth slides onto the steps, the clear height drops to 1.0–1.3 m (assumed).

### Physics role
- **Collision:** the trench fillers continue down the stair: step boxes, wall boxes, a ceiling box under the roof; frame logs as capsules (`timber`); roof logs only where they show at the entrance face (their ends, capsules); the chamber as a box room. **Holes:** a `holes` record of its own, a footprint over the stair and the chamber, under F-1…F-8 as the cellar C01's (houses-and-yard.md, Holes): the chamber lies below the ground, so its cells are holes too and the ground over them is filler (`surface:meadow_sod`, or `surface:belt_straw` under the crowns); the mound is heightfield only outside the band.
- **Material ids:** `surface:belt_bare` (stair walls and steps, floor), `timber` (frame, logs, boards), `fabric` (the doorway tarp).
- **Gaps:** the doorway polygon in the frame plane, 0.8 × 1.6 m: nose-first 113 mm a side, yawed 45° 47 mm a side, so passable at any yaw, barely on the diagonal; the stair corridor's cross-sections every 0.5 m (0.85 × 1.7 m, descending about 40°); the chamber door if any. A damaged state's sagging lintel lowers the doorway; the generator re-measures it. The vent is not passable.
- **Wind volumes:** none; a doorway draft is the physics engineer's (as the cellar door, terrain.md W4).
- **Snag hazards:** log ends at the entrance face, the tarp (soft, flutters), splintered lintel ends in the damaged state.
- **Landable surfaces:** the mound top (ordinary ground).

### Placement rules
- On the belt side of E01 runs, every 30–100 m (assumed), the mound partly under the outer crowns.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `door_w_m`, `door_h_m` | float | 0.7–0.9, 1.5–1.8 | 0.8, 1.6 | clear opening in the frame |
| `steps` | int | 4–7 | 6 | stair length and depth |
| `rise_m`, `tread_m` | float | 0.18–0.25, 0.25–0.32 | 0.22, 0.28 | step size |
| `log_d_m` | float | 0.20–0.30 | 0.25 | roof logs |
| `roof_layers` | int | 1–3 | 2 | log layers |
| `cover_m` | float | 0.3–0.8 | 0.5 | earth over the film |
| `chamber` | w × l × h | 2–3 × 2–2.5 × 1.7–2.0 m | 2.5 × 2.2 × 1.8 | the room the stair leads to |
| `doorway` | enum | open, tarp | tarp | what hangs in the opening |
| `damaged` | bool | — | false | sagging lintel, spilled earth |

### Open points
- The chamber's inside (bunks, stove) is out of scope: a bare room, visual only beyond its walls. If the gallery shows a fly-in, the asset-artist may add a few K-kit items.

## E05 Craters (generator)

**Seen in:** B (1–16: shallow, weathered craters 1–5 m over the crusted field, pale rims catching the low sun; B 12: a ring about 4–5 m across, four to five times the length of the 1 m fragment); A (1, 6–7: dark grassed hollows 1–3 m in the sod: the old, overgrown state and the small scorch spots, terrain.md T1, T3b). The pilot names strikes and fallen drones that explode as the source of scorched ground (terrain.md T3).
**Research sources:** the explosion-crater row of the research table (depth about a quarter of the apparent diameter).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| apparent diameter D, small | 0.3–1.0 m | 0.6 m | assumed (asset list; terrain.md T3b, the pilot's small scorched craters); measured A fits it (terrain.md T1: dark patches 0.5–3 m) |
| D, medium | 1.0–2.0 m | 1.5 m | asset list; measured (B 4–12) |
| D, large | 3.0–4.0 m (B shows up to about 5 m) | 3.5 m | asset list; measured (B 12) |
| depth d | 0.2–0.35 D | 0.25 D | published (about a quarter); terrain.md W1 0.3–1 m |
| rim height h above the old surface | 0.03–0.1 D | 0.05 D | derived from terrain.md W1 (rims 0.1–0.3 m on 1–4 m craters, middle values) |
| ejecta apron radius Re | 1.5–2.5 R | 2 R | assumed (the apron thins outward; fresh rays further) |
| scorch disc, small class | 1–6 m | 3 m | assumed (terrain.md T3b: 2–6 m); measured A sets the low end (terrain.md T1: dark patches 0.5–3 m) |
| clods on a fresh apron | 0.05–0.3 m | 10–30 per crater | assumed |
| spacing in a cratered field | 5–20 m | 12 m | terrain.md T2; measured (B 4–12) |
| steepest wall | 35–45° | 42° | derived from the profile below; the generator clamps it at 45° (the top of soil's repose range) |

**Profile (derived; integer powers only, so `SampleGround` and the renderer can evaluate it under the determinism rule).** With s = r/R inside the rim and t = (r − R)/(Re − R) outside:
- inside: z(s) = −d + (d + h)·s²·(3 − 2s): a flat floor, a rounded rim, and the steepest wall at s = 0.5 with slope 1.5·(d + h)/R (42° for the defaults d = 0.25 D, h = 0.05 D; 45° for d = 0.3 D, h = 0.03 D);
- outside: z(t) = h·(1 − t²·(3 − 2t)), the apron falling smoothly to the old surface.
- The floor is narrow: in a 2 m crater the slope is under 15° only within 0.08 m of the centre, so the legs (about 0.12 m from the centre, K§5.4) of a centred landing stand on about 21° slopes; in a 3.5 m crater, about 13° (derived).

### Construction and materials
- The bowl's surface is ejecta fallen back and loose spoil; the rim and apron are thrown spoil over the old surface. Spoil colour follows depth against `topsoil_m` (shared rules): small and medium craters throw dark topsoil, large ones a pale mix (B's pale rims).
- Fresh: no growth, sharp rim, clods, rays of spoil, scorch on dry grass. Weathered (B): a crust over the rim, tufts regrowing on it, the bowl slumped 20–40 % shallower. Old (A): a grassed hollow, the rim almost gone, darker and greener than the sod around it or bare dark soil.
- **Materials:** S03 crater_spoil for the bowl and apron, blending into the host with `age`; S07 burnt_field material for the scorch disc; host surface beyond.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| weathered crater floor on crust, low sun | #8a7660 (10YR 5/3, derived) | #b87728 (B 12) against crust #be7e34 | measured on B |
| weathered rim with tufts, sunlit | #a39280 (10YR 6/3, derived) | #de9a43 (B 12) | measured on B |
| old grassed hollow in sod, overcast | #4a463f under grass (derived) | #3c4213 (A 1) against sod #848321 | measured on A |
| fresh deep spoil (pale mix) | #a88f70 (10YR 6/4, derived) | #bc853d (B 1, bank spoil) | measured on B |
| fresh shallow spoil (dark) | #514535 (10YR 3/2, derived) | — | derived |
| scorch | as S07 | — | S07 |

### Variants and states
- **Classes:** small (with its scorch disc), medium, large with a loose rim (asset list).
- **States:** `age` 0 fresh, 0.5 weathered (B), 1 old and grassed (A); wet: mud in the bowl, softer and slicker (D-021), no standing water (no reference shows it).
- **Hosts:** meadow, crust, tilled, straw, track; on asphalt and slabs the crater is a road-kit variant (task 1.4): the same profile, its rim of broken asphalt or concrete plates.

### Damage pattern
- It is the damage. Around fresh large craters: lodged or stripped grass and broken stalks pointing outward over 2–6 m (vegetation.md V17 note; not visible in B at its resolution), fragments of the munition (visual, under 0.1 m).

### Physics role
- **Collision:** craters of 4 m and more are carried by the heightfield (1 m samples) with the profile as a placed feature on top, so the sub-grid shape stays exact; smaller ones are placed features only (the first attempt's X-9: a centre, R, d, h, Re and the profile), evaluated by `SampleGround` and drawn by the renderer from the same function as a displaced patch. No mesh collision.
- **Material ids:** `crater_spoil` in the bowl and the apron, blending to the host surface with `age`; the scorch disc carries `burnt_field`.
- **Gaps:** none.
- **Wind volumes:** none (below the wind grid's resolution).
- **Snag hazards:** none.
- **Landable surfaces:** ground. A bowl is the manifesto's sloped landing: crater_spoil's μs 0.8 holds a foot to about 39° dry, so the steepest walls (40–45°) slide even dry; wet (μs 0.45) every slope over 24° slides (derived, K§7.3). Rims are loose: legs sink 3–8 cm (terrain.md §8).

### Placement rules (for the world)
- **Cratered field (B type):** 1 per 100–400 m² (derived from the 5–20 m spacing); size mix medium 50 %, small 30 %, large 20 % (assumed from B 4–12); most weathered.
- **Meadow (A type):** small craters with scorch discs and old hollows, 1 per 1,000–5,000 m² (assumed: one to three dark hollows per A frame).
- Clusters near belts, trench lines, tracks and yards (assumed: where the targets are); craters cut parapets (E01) and tracks.
- Elongated craters orient downrange by draw (assumed for oblique hits).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `diameter_m` | float | 0.3–5 | by class | apparent diameter D |
| `depth_ratio` | float | 0.2–0.35 | 0.25 | d/D |
| `rim_ratio` | float | 0.03–0.1 | 0.05 | h/D |
| `apron_ratio` | float | 1.5–2.5 | 2.0 | Re/R |
| `ellipticity` | float, azimuth | 0–0.3, 0–360° | 0 | elongation of an oblique hit |
| `age` | float | 0–1 | 0.5 | fresh → weathered → grassed: slump, rim softening, crust, regrowth |
| `scorch_m` | float | 0–6 | 3 (small class), 0 otherwise | scorch disc diameter |
| `rays` | int | 0–12 | 6 when fresh | spoil rays on the apron |
| `clods` | int | 0–40 | 20 when fresh | clods 0.05–0.3 m (visual; physics in the micro-relief) |
| `topsoil_m` | float | 0.3–1.0 | 0.5 | spoil colour mix by depth |
| `host` | surface id | any | from the map | what the apron blends into |
| `seed` | int | any | per crater | every draw |

### Open points
- The placed-feature mechanism (X-9) is the world-builder's and the physics engineer's; this sheet gives the profile.
- Blast-lodged stalks around fresh craters (vegetation.md asks): B shows none at its resolution; kept as an assumption.

## E06 Round pit with branches over it

**Seen in:** F (13–17: the corridor ends at a round pit with a clean lip; F 15–16 seen from 1–2 m, F 17 looking down into it).
**Research sources:** none needed for the shape (measured); vegetation.md V19 for the branches.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| diameter at the lip | 2–3 m | 2.5 m | measured (F 15–17: the lip spans the frame at 1–2 m; terrain.md W3) |
| depth | 1.0–1.5 m | 1.25 m | terrain.md W3; measured (F 16: the far wall drops below the lip by about half the pit's width) |
| wall | 80–90°, near vertical | 85° | measured (F 16–17) |
| lip | a pale band 3–8 cm thick (a dry crust and root mat), overhanging 0–0.1 m | 5 cm, 0.05 m | measured (F 17) |
| floor | flat, a little fallen soil and twigs | — | measured (F 17) |
| spoil mound | 0.3–0.8 m high, 1–2 m wide, along about half the rim | 0.5 m | measured (F 16: the ground rises behind the pit) + derived: π·1.25²·1.25 m³ × swell 1.25 ≈ 7.7 m³, part heaped, part spread |
| branches | 3–6 pieces, 2–4 m long, 3–8 cm thick, across the opening and fallen in | 4 | measured (F 15–17); vegetation.md V19 |
| openings between branches | 0.3–1.2 m | — | measured (F 15–17) |

### Construction and materials
- A dug, cylindrical pit, not a crater: its walls are vertical and its lip is clean and overhangs, where a crater has a 35–45° bowl and a raised apron (E05) (measured, F 15–17). Its use is not stated (class only).
- Spoil heaped on one side as a low mound, the rest spread over the belt floor. Dead branches lie across the opening and on the floor.
- **Materials:** S05 belt_bare for the walls, floor and mound; a lip mask (the pale crust band and a root fringe); branches from V19.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| pit floor | #6b5e4f (10YR 4/2, derived) | #817461 (F 17), #8d765c (F 16, the far inside) | measured on F |
| spoil mound | #8a7660 (10YR 5/3, derived) | #927e61 (F 16) | measured on F |
| belt floor beside the lip, sunlit litter | as S05 | #aa9e8c (F 17) | measured on F |
| branches, bark-stripped | as V19 | #a79a86 (F 17) | measured on F |

### Variants and states
- One variant; branch count and layout by seed; wet: walls darker, floor mud.

### Damage pattern
- None beyond the branches; a slumped state reuses E05's `age` slump if needed.

### Physics role
- **Collision:** a hole (about 3–3.5 m of cells, with the F-2 margin) filled with a ring of 12–16 wall boxes, each at least 0.3 m thick and within 0.05 m of the visual (F-5, F-6), a floor box and level lip boxes; the mound is heightfield; branches as V19 capsules.
- **Material ids:** `surface:belt_bare`; branches `deadwood`.
- **Gaps:** the opening (2–3 m: passable at any yaw); the branch grille: every opening of 0.5 m or more is written as a gap polygon, passable nose-first where at least 0.574 m and at any yaw where at least 0.706 m.
- **Wind volumes:** none. Inside, a cylindrical cavity (depth/diameter 0.4–0.75) holds one recirculating vortex (derived from the cavity model); rotor rays see the walls, so wall and ground effect in a pit come from the physics rays.
- **Snag hazards:** the branches over and in it.
- **Landable surfaces:** the floor is ground (2–3 m across, the props clearing the walls by 0.7–1.2 m); the lip is a pitfall: a leg over it drops 1.0–1.5 m.

### Placement rules
- On the bare floor of belts (F type), at corridor ends and clearings; 1 per 100–300 m of belt (assumed); always with V19 pieces nearby.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `diameter_m` | float | 2–3 | 2.5 | lip diameter |
| `depth_m` | float | 1.0–1.5 | 1.25 | depth |
| `wall_deg` | float | 80–90 | 85 | wall angle |
| `lip_overhang_m` | float | 0–0.1 | 0.05 | the crust band's overhang |
| `mound` | height, arc | 0.3–0.8 m, 90–270° | 0.5 m, 180° | the spoil heap |
| `branches` | int | 0–6 | 4 | V19 pieces across and in |
| `seed` | int | any | per pit | every draw |

### Open points
- Whether the overhanging lip should break under a leg (breakable soil): deferred with the first attempt's X-8 (breakables); the lip stays rigid soil for now.

## E07 Spoil bank / earth bank

**Seen in:** B (1–6: a lumpy, pale bank along the left edge of the field with thin saplings on it and a dark cut at its foot). Banks like this are real and common (pilot, terrain.md T2); it is built as a trench spoil bank.
**Research sources:** the angle-of-repose row.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height | 0.6–1.2 m | 1.0 m | terrain.md T2; measured (B 1–6) |
| side slopes | 30–40° | 35° | published (repose of soil) |
| crest width | 0.3–0.6 m, lumpy | 0.5 m | measured (B 1, B 4) |
| base width | 3–4 m | 3.4 m | derived: 0.5 + 2 × 1.0/tan 35° |
| cross-section | 1.5–2.5 m² | 1.93 m² | derived; the spoil of a 1.0 × 1.5 m cut, all thrown to one side (× swell 1.25) |
| crest clods | 0.1–0.4 m | — | measured (B 1, B 4) |
| lengths (2 variants) | 10–20 m; 40–80 m | 15 m; 60 m | assumed (B's bank runs out of frame, more than 30 m) |
| the cut at its foot | 0–1.0 m wide, 0–1.5 m deep | 0.8 m, as an E01 run or a ditch | measured (B 1–6, the dark line) |
| saplings on the crest | 2–4 m, 1 per 3–8 m | 1 per 5 m | measured (B 1–6) |

### Construction and materials
- Excavated spoil heaped along the field edge beside the cut it came from; the crest is lumpy with clods; dry tufts and self-seeded saplings grow on it once it has stood a season (measured, B 1–6).
- **Materials:** S03 crater_spoil (fresh) turning to S02 dry_crust with tufts (weathered), the pale-spoil tint for a deep cut.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| sunlit spoil | #a88f70 (10YR 6/4, derived) | #bc853d (B 1) | measured on B |
| shaded side | — | #9b6f39 (B 1) | measured on B |

### Variants and states
- Two lengths (asset list); `age` fresh to weathered; with or without the foot cut.

### Damage pattern
- A crater (E05) breaches it: a 2–4 m gap with spoil fanned out.

### Physics role
- **Collision:** heightfield (a 3–4 m wide, 1 m high bank spans 3–4 samples), with the clods as micro-relief (0.05–0.1 m at 0.3–0.6 m); the foot cut, if present, is an E01 run.
- **Material ids:** `crater_spoil` (fresh) or `dry_crust` (weathered) terrain.
- **Gaps:** none.
- **Wind volumes:** none as an object; the heightfield gives terrain shelter: a separation bubble about 3 m and a calmer zone 5–10 m downwind of a 1 m bank (derived: 3 heights and 5–10 heights for a solid ridge, assumed).
- **Snag hazards:** the saplings (V16, vegetation sheet).
- **Landable surfaces:** ground. The crest (0.3–0.6 m) is wider than the legs' 0.24 m footing (legs about 0.12 m from the centre, K§5.4) but lumpy, so a crest landing rocks; the 35° slopes hold a foot dry (crater_spoil μs 0.8, 39°) and slide wet (μs 0.45, 24°) (derived).

### Placement rules
- Along field edges beside trench runs and ditches, and at belt corners; in B type fields along one edge (assumed). Organic outline, its toe feathered 1–2 m into the host surface.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `path` | polyline | from the map | — | the bank's line |
| `length_m` | float | 10–80 | 15 or 60 | the two variants |
| `height_m` | float | 0.6–1.2 | 1.0 | crest height |
| `crest_w_m` | float | 0.3–0.6 | 0.5 | crest width |
| `slope_deg` | float | 30–40 | 35 | side slopes |
| `lumpiness` | amplitude, wavelength | 0.05–0.15 m, 0.3–1 m | 0.08, 0.5 | crest clods |
| `foot_cut` | enum | none, ditch, trench | trench | the cut beside it (E01 run) |
| `age` | float | 0–1 | 0.6 | fresh spoil → crust with tufts and saplings |
| `saplings_per_10m` | float | 0–4 | 2 | self-seeded saplings on the crest |
| `seed` | int | any | per bank | every draw |

### Open points
- Clods 0.15–0.4 m as rigid small pieces rather than micro-relief: the physics engineer's call (the first attempt's X-1 debris chunks).

## Surfaces

How each surface row reads:
- **Physics** cites the K§7.2 row and the first attempt's cover block (carried), as `bearing N/m³ · damping N·s/m³ · μs/μk · max sink · unload ratio · micro-relief · pitfalls`, then the cover, then the reference foot (25 N on 15 mm). The wet variant follows the shared D-021 table unless the row says otherwise.
- **Albedo check:** the photo albedo's luminance is compared with the published broadband albedo of its class (research table). A value that misses is flagged for the asset-artist to re-measure on the chosen set.
- **Micro-detail** (AST-10) gives sizes and densities for the shared generator; the near ring carries physical elements, the far layer reaches 60 m or more.

## S01 meadow_sod

**Seen in:** A (1–15: mottled olive-khaki sod, green and straw-dry mixed, pale matted patches, small dark hollows), L (1–14: the same land at night). The far fields of B and G. PR-8's pale matted grass with green showing through.
**Research sources:** the albedo row (green grass 0.25); the material table (TextureCan Ground 0018, ambientCG Grass001/004, Poly Haven brown_mud_leaves_01, burned_ground_01).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| relief | 2–5 m over hundreds of metres | — | measured (terrain.md T1, A); WLD-02 |
| grass height | 0.2–0.6 m | 0.35 m | terrain.md T1; carried |
| stems | 2–4 mm, 400 per m² | — | carried |
| thatch mat | 1–4 cm | 2.5 cm | carried |
| tussocks | clumps 0.1–0.3 m, 0.2–0.6 m apart | 0.2 m | measured (A 13–15) |
| straw-dry share of the sod | 50–70 % | 60 % | measured (A 4–12) |
| pale matted patches | 3–15 m | 8 m | measured (A 4–6); PR-8 |
| yellow-green patches (another species) | 2–5 m | 3 m | measured (A 7, A 12); terrain.md T1 |
| dark hollows and scorch spots | 0.5–3 m | 1.5 m | measured (A 1, A 6–7); terrain.md T1; built as E05 (small, old) |

### Construction and materials
- Root-bound loam under a continuous sod of grasses and forbs; a thatch of dead stems over it; living stems green to straw; patches of other species and of matted pale grass; a few dark hollows.
- **Materials:** TextureCan Ground 0018 for the thatch and the pale patches; ambientCG `Grass001`/`Grass004`, desaturated to the A olive, for the green share at distance; Poly Haven `brown_mud_leaves_01` in hollows; E05's scorch for scorch spots. Derived: a patch mask (R straw-dry share, G yellow-green patches, B bare and scorched).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| sod near, overcast | #6f6b45 (carried); see the check below | #7f7c49 (A 12), #8b8c6e (A 7), #948651 (A 4); terrain.md #807948 | measured on A |
| sod far | — | #9da094 (A 7); terrain.md #9b987b | measured on A |
| yellow-green patch | #6a7a2a (assumed) | #7a8332 (A 7); terrain.md #696d1b | measured on A |
| pale matted grass | #a89a6a (assumed) | #948651 (A 4) | measured on A |
| dark hollow | under-sod soil #4a463f (derived) | #3c4213 (A 1), sod around it #848321 | measured on A |
| night ground | — | #0c0c0c–#0d0e0d (L 2), sky #4d4e4c | measured on L |

**Albedo check:** green grass is 0.25 (published) and dry grass is lighter still, so the sod's mean luminance should sit at 0.20–0.28. The carried #6f6b45 is 0.14 (derived), too dark: re-measure on the chosen sets and lift it (flagged).

### Variants and states
- Late summer to autumn mix (default, A); PR-8's matted pale state (straw-dry share 70–85 %); wet (D-021: thatch heavy and slick, the sod softer, ×0.6). No night variant (the feed does night).

### Damage pattern
- Small craters with scorch discs and old grassed hollows (E05); tyre tracks (S13 in its faint form); burnt edges (S07).

### Physics role
- **Row `meadow_sod`** (carried): 7.0e6 · 3.7e5 · 0.55/0.45 · 0.05 m · 3 · 0.02 m @ 0.4 m · 0.01 /m², 0.05–0.3 m deep, r 0.05–1.0 m. Cover grass 0.2–0.6 m, 400 /m², 2–4 mm, tip stiffness 0.1 N/m, hook p 0.02 releasing at 0.5–3 N; thatch mat 1–4 cm, 8 kPa, ζ 0.5, μ 0.50/0.40. Dust 0.05. Foot 1237 N/m, 20 mm sink: grass cushions the touchdown and adds side jitter (terrain.md T1).
- **Wet:** bearing and damping × 0.6 (roots), the thatch heavy and slick (μ 0.35/0.28), adhesion as the shared table.
- **Gaps, wind volumes:** none; full exposure to wind (terrain.md T1).
- **Snag hazards:** stem hooks (rare). **Landable:** yes.

### Micro-detail (AST-10)
- Stems: 400 /m², 2–4 mm, 0.2–0.6 m, 60 % straw-dry (measured A), 10–30 % with seed heads (assumed); tussock clumping 0.1–0.3 m.
- Thatch: lodged dead stems as the mat, drawn as short lying stems near the camera (1–4 cm layer).

### Placement rules (for the world)
- The main cruise ground between belts and fields, on the rolling relief; its edges feather into belt straw (2–5 m), crust (3–10 m), fields (0.3–1 m) and tracks (0.3–1 m) as the shared table gives.

### Generator parameters
Preset over the shared surface generator: `base_sets` TextureCan Ground 0018 + ambientCG Grass004 + Poly Haven brown_mud_leaves_01; `albedo_target` #8a845a (the lifted target, assumed until measured); `patch_mask` dry share 0.6, green patches 0.15 at 2–5 m, bare 0.02 at 0.5–3 m; `macro_variation` 0.12 at 40 m; `cover_channels` grass 1, straw 0.3 (thatch), twigs 0, litter 0.

### Open points
- The carried albedo is too dark against the published grass albedo; the asset-artist settles it on the fetched sets.

## S02 dry_crust

**Seen in:** B (1–16: sun-baked loam crust with sparse dead tufts, dust, shallow craters).
**Research sources:** the albedo row (bare soil 0.17); the material table (Poly Haven dry_ground_01, mud_cracked_dry_03).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| crust | 5–20 mm thick over loose loam | 10 mm | assumed |
| crack polygons | 0.1–0.5 m | 0.25 m | assumed (below B's resolution); normal map plus albedo only |
| tufts | 0.1–0.4 m high, clumps 0.1–0.3 m; stems 1–3 mm, 40 per m² | 0.25 m | terrain.md T2; carried; measured (B 10–16) |
| bare share | 60–80 % | 70 % | measured (B 4–16) |
| loose dust | 1–5 mm on the crust | 2 mm | assumed; visual only (terrain.md S6) |

### Construction and materials
- A rain-sealed, sun-baked skin of fine loam over looser soil; dead grass tufts in clumps; dust drifts in the lee of tufts.
- **Materials:** Poly Haven `dry_ground_01` and `mud_cracked_dry_03`, crack scale set to 0.1–0.5 m, tinted to 10YR 5/3–6/3; tufts from TextureCan Ground 0018 colours on the stem cards.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| crust | #9a7d5a (carried); 10YR 5/3–6/3 #8a7660–#a39280 (derived) | #c78534 (B 5), #ce913c (B 10), #be7e34 (B 12, with tufts), far #b7772c (B 12); terrain.md #c38436, near #a36f37 | measured on B |

**Albedo check:** #9a7d5a is 0.22 in luminance (derived), a pale dry crust above bare soil's 0.17: plausible.

### Variants and states
- Dry (default); wet (D-021): the crust turns to a thin slick film over firm soil (bearing × 0.4, μ 0.40/0.30, adhesion), and darkens 1–2 value steps.

### Damage pattern
- Craters (E05, B), tyre marks (S13 faint), scattered light fragments (debris kit, task 1.4).

### Physics role
- **Row `dry_crust`** (carried): 5.0e7 · 2.5e5 · 0.7/0.6 · 0.01 m · 1.5 · 0.01 m @ 0.8 m · 0.004 /m², 0.03–0.15 m deep, r 0.05–0.3 m. Cover grass 0.1–0.4 m, 40 /m², 1–3 mm, 0.4 N/m, hook p 0.03 at 0.5–2 N. Dust 0.8. Foot 8836 N/m, 2.8 mm: the manifesto's glide-landing bounce (terrain.md T2).
- **Snag:** none. **Landable:** yes, with bounce. **Dust:** lifts under prop wash.

### Micro-detail (AST-10)
- Tufts: 40 stems /m² in clumps of 10–30, 1–3 mm, 0.1–0.4 m, straw-dry; a few pebbles and dry clods 1–3 cm (visual).

### Placement rules
- The cratered-field zone (B type) and dry high ground; feathers into meadow over 3–10 m.

### Generator parameters
Preset: `base_sets` dry_ground_01 + mud_cracked_dry_03; `albedo_target` #9a7d5a; `patch_mask` tufts 0.3 at 0.1–0.3 m clumps; `macro_variation` 0.1 at 20 m; `cover_channels` grass 0.4, straw 0, twigs 0, litter 0.

### Open points
- None.

## S03 crater_spoil

**Seen in:** B (1–6: pale lumpy spoil of the bank; 4–12: crater rims and bowls), F (16: the spoil mound beside the pit).
**Research sources:** the soil-profile and repose rows.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| loose layer | 0.05–0.3 m | 0.12 m (the max sink) | carried |
| clods | 0.02–0.3 m | — | measured (B 1, B 4: lumps on the bank) + assumed (size spread) |
| slopes it forms | 30–45° | 38° | published (repose) |

### Construction and materials
- Loose, crumbly soil thrown from a cut: dark topsoil for shallow cuts, a pale mix of subsoil for deep ones (shared rules); clods; no vegetation when fresh, crust and tufts with age.
- **Materials:** Poly Haven `brown_mud_dry`, `brown_mud_rocks_01` (clods), `dirt_aerial_02` at distance; a mix mask (topsoil ↔ subsoil) driven by the cut depth.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| deep spoil, pale mix | #a88f70 (10YR 6/4, derived) | #bc853d sunlit, #9b6f39 shaded (B 1) | measured on B |
| shallow spoil, dark | #514535 (10YR 3/2, derived); carried #6e5a44 | — | derived |
| belt-floor spoil | #8a7660 (10YR 5/3, derived) | #927e61 (F 16) | measured on F |
| weathered crater floor | as S02 | #b87728 (B 12) | measured on B |

### Variants and states
- Mix (pale ↔ dark) by cut depth; `age` (fresh → crusted); wet: mud (bearing × 0.25, adhesion high).

### Damage pattern
- It is the damage's surface.

### Physics role
- **Row `crater_spoil`** (carried): 2.8e6 · 3.2e5 · 0.8/0.65 · 0.12 m · 8 · 0.05 m @ 0.5 m · 0.02 /m², 0.05–0.2 m deep, r 0.1–0.4 m. No cover. Dust 0.6. Foot 495 N/m, 51 mm: legs sink and the slope tilts the drone (terrain.md T2, W1); steep ground under one foot 31–42° (K§7.3).
- **Snag:** none. **Landable:** yes; soft and sloped.

### Micro-detail (AST-10)
- Clods 2–30 mm as visual instances on fresh spoil, 50–200 /m² (assumed); larger clods are micro-relief.

### Placement rules
- Crater bowls and aprons (E05), fresh parapets (E01), spoil banks (E07), the pit's mound (E06); lobed edges feathering 0.5–2 m.

### Generator parameters
Preset: `base_sets` brown_mud_dry + brown_mud_rocks_01; `albedo_target` by mix; `patch_mask` clods 0.4; `cover_channels` all 0 when fresh.

### Open points
- None.

## S04 belt_straw

**Seen in:** H (1–21: a lodged mat of dry grass over dark soil along a belt edge, standing stems with seed heads, bare soil patches and a strip of short green grass mid-clip), G (1–12: the same mat on the trench parapets in low sun), F (margins). Look-lock asset (phase-1 brief).
**Research sources:** the material table (TextureCan Ground 0018, Poly Haven brown_mud_02); the soil-profile row.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| mat depth | 5–20 cm | 12.5 cm | terrain.md T4; carried |
| lying straws | 0.3–1.0 m long, 2–5 mm, crosswise, 250 per m² | — | terrain.md T4; carried; measured (H 1–21) |
| standing stems with seed heads | 0.5–1.2 m, 2–5 mm, 60 per m² | 0.8 m | terrain.md T4; carried; measured (H 3–9) |
| twigs in the mat | 5–20 mm, 0.3–1.0 m, 2 per m² | — | carried; measured (H 13–21) |
| bare soil patches | 0.5–2 m | 1 m | terrain.md T4; measured (H 9–14) |
| small earth mounds | 0.3–0.5 m across | 0.4 m | terrain.md T4; measured (H 1–4) |
| short green regrowth | 10–30 % of the area, strips 1–3 m | 15 % | measured (H 16) |
| uneven support | one leg on a 5 cm mat, another on 20 cm | — | derived (legacy §3.1: a 12.5 cm mat compresses about 5 cm under the reference load, a 20 cm mat about 3 cm, a 5 cm mat through to the soil) |

### Construction and materials
- Moist dark loam (chernozem type) under a mat of last season's grass lodged in all directions, with standing stems through it, a few purple flowers and green regrowth, twigs from the belt, and holes hidden under the mat.
- **Materials:** Poly Haven `brown_mud_02` (soil, 10YR 2/1–3/1) under TextureCan Ground 0018 (straw); the near mat is geometry: lying-straw instances from the micro-detail generator; a lodging-direction mask.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| mat, mean | #7a7055 (carried) | #777158 (H 16), terrain.md #797258, overcast | measured on H |
| dry straw itself | #b49c6a (assumed; 0.35 in luminance, derived) | #d39641 (G 3, low sun); terrain.md #d2953f | measured on G |
| soil patches | #33302b–#4a463f (10YR 2/1–3/1, derived) | #67675c (H 14), #686257 (H 12), #4a4841 (H 10, a damp dark path) | measured on H |
| short green strip | #6a7a3a (assumed) | #959763 (H 16) | measured on H |
| wet straw | — | #9d7020 (P 23, windrow straw in rain) | measured on P |

**Albedo check:** a mat of dry straw over dark soil sits at 0.15–0.25 (derived from its parts); the carried mean #7a7055 is 0.17: plausible.

### Variants and states
- Dry autumn (default, H); low-sun look (G); wet (D-021): the mat heavier and slicker (modulus × 0.6, μ 0.35/0.28, hooks hold × 1.5), the soil × 0.4, dark straw (P 23).

### Damage pattern
- Blast-flattened and torn mat around craters, fire-blackened edges at burnt fields (S07), trampled tracks into the belt (H 9–10: a dark path).

### Physics role
- **Row `belt_straw`** (carried): 4.7e6 · 1.8e5 · 0.5/0.4 · 0.08 m · 5 · 0.04 m @ 0.7 m · 0.02 /m², 0.1–0.3 m deep, r 0.05–0.3 m (holes hidden under the mat). Cover: straw 0.3–1.0 m, 250 /m², 2–5 mm, 0.3 N/m, hook p 0.15 at 1–5 N, mat 5–20 cm, 4 kPa, ζ 0.3, μ 0.45/0.35; grass 0.5–1.2 m, 60 /m², 0.08 N/m, hook p 0.05 at 1–4 N; twigs 0.3–1.0 m, 2 /m², 5–20 mm, 100 N/m, hook p 0.1 at 0.3–3 N. Dust 0.1. Foot 831 N/m, 30 mm.
- The manifesto's uneven-landing case: legs rest at different heights on one mat, straws hook a leg for 50–200 ms at liftoff (the first attempt measured 21.5 % of landing legs hooked on flat straw: a tuning point, K§7.3 O-7).
- **Snag:** straws and twigs hook legs and the fiber. **Landable:** yes; springy and uneven.

### Micro-detail (AST-10)
- Lying straws 250 /m², 0.3–1.0 m, 2–5 mm, crosswise with a weak lodging direction; standing stems 60 /m², 0.5–1.2 m, seed heads on 30–60 % (assumed from H); twigs 2 /m²; litter none. The near ring draws and simulates every one; the far layer keeps the mat's texture to 60 m and more. This is the H look at 0.3–1.5 m: the mat must read as separate straws, never as a texture (REAL-05).
- **The instance kit** (asset-artist; the scatter and its densities above are the shared micro-detail generator's, AST-10): one small generator builds the meshes the generator places, each a tapered tube of a few segments.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `seed` | int | any | per kit | every draw (bend, length, head shape) |
| `straw_len_m` | range | 0.3–1.0 | 0.3–1.0 | lying straw length, drawn per instance mesh |
| `straw_d_mm` | range | 2–5 | 2–5 | straw diameter (the physics stem radius is the same number) |
| `straw_bend` | float | 0–1 | 0.3 | curvature and kinks along a lying straw (0 straight) |
| `stem_h_m` | range | 0.5–1.2 | 0.5–1.2 | standing stem height |
| `seed_head_share` | float | 0–1 | 0.45 | share of standing stems with a seed head (H: 30–60 %) |
| `twig_len_m`, `twig_d_mm` | range | 0.3–1.0, 5–20 | as the cover row | the belt twigs in the mat (≥ 10 mm are rigid rollers, K§7.3) |
| `variants` | int | 4–16 | 8 | distinct meshes per kind, so no repeat reads at 1.5 m |
| `segments` | int | 2–6 | 4 | tube segments per straw at LOD0; LOD1 a 2-segment tube; beyond the near ring the far layer is the texture |

### Placement rules
- Every belt edge, 2–10 m out from the trunks and under the outer crowns (H, G); trench parapets (E01); feathers into meadow over 2–5 m and into belt_bare over 1–3 m.

### Generator parameters
Preset: `base_sets` brown_mud_02 + TextureCan Ground 0018; `albedo_target` #7a7055; `patch_mask` bare soil 0.08 at 0.5–2 m, green 0.15 in 1–3 m strips; `directional` lodging azimuth (weak, 0.3 bias); `cover_channels` grass 0.5, straw 1, twigs 0.5, litter 0.

### Open points
- The hook rate (21.5 % of legs) may read as "every time"; flight tuning with the pilot at the physics gate (K§7.3).

## S05 belt_bare

**Seen in:** F (1–17: fine dusty loam beaten bare in a dense belt, fallen twigs and branches, brown dust haze; the pit's surroundings).
**Research sources:** the material table (Poly Haven dirt_floor, forest_ground_04, park_dirt).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| bare patches | the corridor width, 2–10 m | — | measured (terrain.md T5, F) |
| loose top layer | 2–10 mm of dust over firm loam | 5 mm | assumed |
| twigs and sticks | 5–30 mm, 0.3–2 m, 1 per m² | — | carried; measured (F 13–17) |
| leaf litter | 3–8 cm leaves, 40 per m² | — | carried |
| dust haze | to 2–4 m high in dry scenes | — | measured (terrain.md S6, F) |

### Construction and materials
- Shaded, root-filled loam with no herb layer (measured F, terrain.md T5: the floor is beaten bare inside dense belts), a dusty top, dry leaves and twigs; exposed roots at trunk bases (assumed).
- **Materials:** Poly Haven `dirt_floor`, `forest_ground_04`, `park_dirt` tinted to 10YR 4/2–5/3; a dust-film mask; leaf instances from the belt trees above (AST-10).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| floor | #7e6a55 (carried); 10YR 4/2–5/3 #6b5e4f–#8a7660 (derived) | #a68152 (F 8), #a28051 (F 4), #987f5f (F 10, in haze); terrain.md #8a7156 | measured on F |
| sunlit, litter-strewn floor | — | #aa9e8c (F 17) | measured on F |

**Albedo check:** #7e6a55 is 0.15 in luminance (derived), against bare soil's 0.17: plausible.

### Variants and states
- Dry and dusty (default); wet (D-021): dark, sticky, no dust.

### Damage pattern
- Pits (E06), branch litter (V19), dug spots.

### Physics role
- **Row `belt_bare`** (carried): 1.4e7 · 3.5e5 · 0.5/0.4 · 0.03 m · 3 · 0.015 m @ 1.0 m · 0.005 /m². Cover twigs 0.3–2.0 m, 1 /m², 5–30 mm, 100 N/m, hook p 0.08 at 0.3–5 N; litter 40 /m², no hooks. Dust 0.9. Foot 2474 N/m, 10 mm; the loose top lets legs slide on contact at speed (terrain.md T5).
- Also the surface of every cut wall and floor (E01–E04, E06; F-7).
- **Snag:** twigs. **Landable:** yes.

### Micro-detail (AST-10)
- Twigs 1 /m² (5–30 mm: those of 10 mm or more are rigid rollers under a foot and capstans for the fiber, K§7.3); leaves 40 /m² from the tree species above.

### Placement rules
- Inside dense belts (F type, blow-through), in corridors and under closed crowns; feathers into belt_straw over 1–3 m at the edges.

### Generator parameters
Preset: `base_sets` dirt_floor + forest_ground_04; `albedo_target` #7e6a55; `patch_mask` litter 0.3, dust film 0.5; `cover_channels` grass 0, straw 0, twigs 1, litter 0.4.

### Open points
- None.

## S06 tilled

**Seen in:** G (1–12: a bare dark field with pale straw streaks in rows beside the trench), P (1–48: wet stubble fields with drill rows, straw windrows, wheel lines and green patches of new growth). Dark fields are both tilled and burnt (pilot, terrain.md T3).
**Research sources:** the field-tillage row (plough bodies, drill rows, windrows, stubble height); the soil-profile row.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| furrow pitch, ploughed | 0.30–0.50 m | 0.35 m | published (body widths); terrain.md T3a |
| ridge height, ploughed | 0.15–0.30 m | 0.20 m | assumed (terrain.md T3a: a ploughed field; G shows the disked form) |
| clods, ploughed | 0.05–0.20 m | — | assumed (terrain.md T3a: a ploughed field; G shows the disked form) |
| ridges, disked or harrowed | 0.05–0.15 m | 0.08 m | measured (terrain.md T3a, G) |
| straw streaks, disked stubble | 0.2–1 m wide, every 1–5 m, along the work direction | 0.5 m every 3 m | measured (G 1–12) |
| drill rows, stubble | 0.15 m | 0.15 m | published (a 3.6 m drill with 15 cm rows); measured (P 33–42, fine parallel rows) |
| stubble | 0.10–0.20 m standing, 3–4 mm, 400–700 per m² | 0.15 m, 500 /m² | published (cutting height); density assumed (about 4–5 million plants per hectare, 1–1.5 stems each) |
| windrows | 0.8–2.5 m wide, every 5–9 m | 1.5 m every 6 m | measured (P 21–31, against the 15 cm rows) + derived (one windrow per 5–6 m header pass through a 1.5 m window, published) |
| wheel lines (tramlines) | a pair 1.5–2.0 m apart, every 18–36 m | — | measured presence (P 40); spacing assumed |
| headland | 6–12 m, rows turned 90° | 9 m | derived (2 × the 3–6 m implements) |

### Construction and materials
- Three states of one field: **ploughed** (furrows and clods, dark loose topsoil), **disked** (fine ridges, straw residue in streaks: G), **stubble** (untilled after harvest: standing stubble in drill rows, straw windrows, wheel lines: P). Late in the season green new growth shows in patches (P 43–48).
- **Materials:** Poly Haven `dry_mud_field_001` (farm soil with track marks) and `aerial_mud_1` at distance, `brown_mud_02` for the wet state, tinted 10YR 3/1 dry and 10YR 2/1 wet; furrows and ridges are geometry (directional ridges in `SampleGround`), the albedo only adds the clods' light tops; a straw-streak and windrow mask along `directional`.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| dry dark field | #5e5446 (carried); #4a463f (10YR 3/1, derived) | #746450 (G 3), #7a6850 (G 9); terrain.md #786751 | measured on G |
| wet stubble field | #33302b (10YR 2/1, derived) | #4c3119 (P 40), #453423 (P 36), #562802 (P 3) | measured on P |
| straw in windrows, wet | #b49c6a dry (assumed) | #9d7020 (P 23) | measured on P |
| soil between windrows, wet | — | #674826 (P 23) | measured on P |
| green new growth | #4f6a2e (assumed) | #4c3812 (P 47; the rain feed mutes green) | measured on P |

**Albedo check:** #5e5446 is 0.09 in luminance (derived), right for a dark chernozem field below bare soil's 0.17.

### Variants and states
- **Variants:** ploughed, disked (G), stubble (P); `directional` azimuth per field.
- **States:** dry; wet (D-021: soft, sticky, dark, P); burnt → S07.

### Damage pattern
- Craters, burnt patches, wheel ruts across it.

### Physics role
- **Row `tilled`** (carried): 4.0e6 · 3.3e5 · 0.75/0.6 · 0.10 m · 8 · 0.02 m @ 0.3 m, plus ridges 0.05 m every 0.5 m at the field's azimuth · 0.01 /m². Cover straw 0.1–0.3 m, 80 /m², 2–4 mm, 5 N/m, hook p 0.05 at 0.5–2 N. Dust 0.4. Foot 707 N/m, 35 mm: soft, uneven sink 2–5 cm; a clod under one leg tilts the drone at liftoff and landing across the furrows rocks it (terrain.md T3a).
- **Ridge parameters by variant:** ploughed 0.10 m half-height every 0.35 m; disked 0.04 m every 0.5 m; stubble none.
- **Stubble variant** (a `tilled` variant, not its own surface row: the physics sign-off, review-materials.md §2, which refuses `surface:stubble`; the variant lives in the surface table's cover block, phase 3): untilled firm soil (bearing 1.4e7, max sink 0.03 m, as belt_bare), stubble cover 0.10–0.20 m, 500 /m², 3–4 mm, 5 N/m (the dry-straw stiffness of the carried tilled row), hook p 0.05; windrows as a straw mat 0.05–0.15 m deep (belt_straw's mat values) where the windrow mask is set. Derived and assumed.
- **Snag:** stubble and straw hooks (weak). **Landable:** yes.

### Micro-detail (AST-10)
- Ploughed: clods 5–20 cm as relief, a few straws. Disked: straw pieces 0.1–0.3 m, 80 /m², in streaks. Stubble: standing stubble 500 /m² in 15 cm rows (the near ring draws the rows; at distance the rows read as a fine stripe), windrow straw 0.3–1 m lying along the windrow.

### Placement rules
- Field blocks of hundreds of metres between belts, with a headland at each end; the dark fields sit between the meadow, the cratered field and the belts (terrain.md §10). Edges feather 0.3–1 m into meadow; the work direction follows the field's long axis, parallel to a belt.

### Generator parameters
Preset: `base_sets` dry_mud_field_001 + aerial_mud_1 (+ brown_mud_02 wet); `albedo_target` #4a463f; `directional` azimuth, spacing by variant (0.35 / 0.5 / 0.15 m; windrows 6 m); `patch_mask` straw streaks 0.2 (disked), windrows 0.25 (stubble), green patches 0–0.3; `cover_channels` straw 0.3–1 by variant.

### Open points
- The `stubble` row (or a stubble mode of `tilled`): the physics engineer's; P shows stubble is the commonest field in the clip set.

## S07 burnt_field

**Seen in:** the pilot (terrain.md T3: burnt by strikes and by fallen drones that explode); A (1, 6–7) for the small scorch spots only. No letter shows a burnt field close up (terrain.md §9).
**Research sources:** the stubble-burning row; the material table (Poly Haven burned_ground_01).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| extent | a 2–6 m scorch disc up to whole fields | — | assumed (terrain.md T3b; no letter shows a burnt field close up) |
| char stubble | 5–15 cm stumps, 3–5 mm, 100 per m² | 10 cm | terrain.md T3b; carried |
| ash layer | 0–1 cm | 0.5 cm | terrain.md T3b; carried (mat 3–10 mm) |
| ash lines where windrows burned | 0.5–2 m wide, every 5–9 m | as the field's windrows | derived (windrows burn hotter, published; their spacing from S06) |
| fire front | wavy, 2–10 m amplitude | 5 m | terrain.md T3b; assumed amplitude |
| unburnt islands and strips | 1–20 m | 5 m | assumed (terrain.md T3b) |
| char gradient at the edge | 0.2–1 m | 0.5 m | assumed |

### Construction and materials
- The soil is unchanged under the char (terrain.md T3b). Black char between, grey-white ash where fuel was thick (windrows, tufts), scorched straw-brown at the margins, unburnt islands. After weeks the char greys as ash blows away and rain washes it; green shoots return (assumed).
- **Materials:** Poly Haven `burned_ground_01` (charred soil, ash, scorched earth) over the S06 or S01 base; a char / ash / scorched / unburnt mask with the wavy front.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| char, mean | #46403a (carried); black char #3f3d3a (assumed, about 0.05) | none close up | assumed |
| ash | #a5a29b (assumed, about 0.35) | — | assumed |
| scorched margin | #6b5640 (assumed) | — | assumed |
| small scorch spot | as char | #3c4213 (A 1, a dark hollow; the reading as scorch is uncertain) | measured on A |

### Variants and states
- Fresh (black, ash lines), aged (grey, ash thinned, shoots), wet (black paste: ash smears, μ 0.40/0.30, no plume). Host: burnt stubble (default) or burnt meadow.

### Damage pattern
- It is the damage: the fire reaches belt edges and leaves their lower crowns charred (vegetation.md `char_height_m`).

### Physics role
- **Row `burnt_field`** (carried): 2.5e7 · 3.6e5 · 0.6/0.5 · 0.02 m · 2 · 0.015 m @ 0.6 m · 0.006 /m², 0.03–0.15 m deep, r 0.03–0.25 m. Cover char stubble 0.05–0.15 m, 100 /m², 3–5 mm, 40 N/m (stiff, brittle), hook p 0.02 at 0.05–0.4 N; ash litter mat 3–10 mm, 20 kPa, ζ 1.0, μ 0.45/0.35. Dust 0.95: an ash plume under prop wash. Firm, little sink (0–1 cm); the stubble snaps and gives short damping spikes (terrain.md T3b).
- **Snag:** none. **Landable:** yes.

### Micro-detail (AST-10)
- Char stumps 100 /m² in the stubble's 15 cm rows, black; ash flakes as a mat; unburnt straw pieces in islands.

### Placement rules
- Whole fields and patches of fields, burnt meadow around scorch discs, belt-edge strips; the front's outline is noisy (2–10 m), with tongues along windrows.

### Generator parameters
Preset: `base_sets` burned_ground_01 + the host's; `albedo_target` #3f3d3a char, #a5a29b ash; `patch_mask` ash 0.15 (more along windrows), unburnt islands 0.1 at 1–20 m; `outline_noise` 5 m at 10–50 m; `cover_channels` grass 1 (as char stubble), litter 1 (ash).

### Open points
- No close reference: the M1 check is the pilot's eye (terrain.md T3b). Logged by the orchestrator as a G2 look question only if the review sheet leaves doubt.

## S08 weeds

**Seen in:** D (dense tall forbs on an overgrown plot; cited from terrain.md T7 and vegetation.md V17–V18, not re-read here). Also C and E foregrounds, B margins.
**Research sources:** vegetation.md V17, V18 (the forbs themselves).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| forbs (the plants) | 0.5–1.5 m, stalks 5–15 mm, 60 per m² | 1.0 m | terrain.md T7; carried; vegetation.md V17 |
| ground under them | dark loam with dead stalks and seed-head litter 1–3 cm | — | assumed (the ground is hidden in D) |
| hidden rubble | brick and stone 5–40 cm, 0–2 per m² in yards | 0.5 /m² in yards | terrain.md T7 (rubble hides underneath) + assumed density |

### Construction and materials
- The ground surface under V17/V18 stands: shaded moist loam, last year's stalks lying, seed heads, sometimes yard rubble beneath. The plants are vegetation assets; this row is their soil and their physical stalks near the drone.
- **Materials:** Poly Haven `forest_ground_04`, `dry_decay_leaves` tinted dark; lying-stalk instances.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| ground under forbs | #4a463f (10YR 3/1, derived) | not visible in D | derived |
| the stand seen from the drone | #6d6a38 (carried) | #817831 (terrain.md, D); #86723e (D 4, vegetation.md) | terrain.md, vegetation.md |

### Variants and states
- Summer, late summer (default), autumn-dry; wet.

### Damage pattern
- Trampled paths, fire, blast-flattened patches.

### Physics role
- **Row `weeds`** (carried): 1.4e7 · 4.4e5 · 0.6/0.5 · 0.04 m · 3 · 0.03 m @ 0.5 m · 0.02 /m². Cover stalks 0.5–1.5 m, 60 /m², 5–15 mm, 1.5 N/m, hook p 0.2 at 5–30 N. Dust 0.05. Foot 2474 N/m, 10 mm. Not a landing surface: the legs go deep, rubble hides underneath, and the stalks resist and cling (terrain.md T7); a hook of 5–30 N can flip the drone (K§7.3).
- **Snag:** stalks hook legs and the fiber; props clipping the tops add drag. **Landable:** no.

### Micro-detail (AST-10)
- Physical stalks within 3 m of the drone from the shared micro-detail generator, the cover's own stems (`MicroDetailNear`; vegetation.md V17, where `forb_stem` was declined as an id); lying dead stalks 0.3–1 m, 20 /m² (assumed).

### Placement rules
- Abandoned plots, yard margins, wall feet, crater fields' margins, collapsed trenches (E03); feathers 0.5–2 m into yard and meadow.

### Generator parameters
Preset: `base_sets` forest_ground_04 + dry_decay_leaves; `albedo_target` #4a463f; `cover_channels` grass 1 (stalks), straw 0.3 (lying stalks), twigs 0, litter 0.3.

### Open points
- The houses-and-yard analyst (task 1.2, letters C, D, E) may add the ground colour where D shows it.

## S09 yard_litter

**Seen in:** C (leaf litter and brick bits in the yard), E (tile shards and planks); cited from terrain.md T6, not re-read here.
**Research sources:** the material table (leaf-litter sets).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| yard ground | compacted soil, 10–30 m yards | — | measured (terrain.md T6, C, E) |
| leaf litter | 1–3 cm, leaves 4–12 cm, 300 per m² | 2 cm | terrain.md T6; carried |
| loose items | bricks, tile shards, planks, stones 5–40 cm | 0.5–3 per m² near walls | terrain.md T6; density assumed |
| twigs | 5–20 mm, 0.2–0.8 m, 1 per m² | — | carried |

### Construction and materials
- Trampled, compacted yard soil under fallen leaves from the fruit trees above; brick and tile bits at the wall feet; paths beaten bare.
- **Materials:** Poly Haven `leaves_forest_ground`, `dry_decay_leaves`, `brown_mud_leaves_01`; ambientCG `ScatteredLeaves009`; leaf instances from the yard trees' own leaf cards (AST-10, K§14.4); bricks and shards from the debris kit (K01, K02, task 1.4).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| leaf litter | #8a6d4c (carried) | #a67f4d (C; terrain.md T6) | terrain.md |
| tile rubble in it | — | #825f44 (E; terrain.md T6) | terrain.md |
| beaten path | #6b5e4f (10YR 4/2, derived) | — | derived |

### Variants and states
- Autumn litter (default, C), sparse litter (summer), wet (D-021: wet leaves μ 0.25/0.18).

### Damage pattern
- Tile and brick spill from damaged roofs and walls toward the wall feet (houses sheet).

### Physics role
- **Row `yard_litter`** (carried): 3.5e7 · 4.2e5 · 0.6/0.5 · 0.015 m · 2 · 0.015 m @ 0.3 m · 0.005 /m². Cover litter 1–3 cm, 300 /m², 4–12 cm leaves, no hooks, mat 1–3 cm, 3 kPa, ζ 0.8, μ 0.35/0.25; twigs 1 /m², hook p 0.05 at 0.3–3 N. Dust 0.2. Foot 6185 N/m, 4 mm: hard and uneven; leaves slide (terrain.md T6). Bricks and shards are rigid pieces from the debris kit (the first attempt's X-1), not relief.
- **Snag:** twigs. **Landable:** yes.

### Micro-detail (AST-10)
- Leaves 300 /m² with leaf silhouettes (never flat chips); twigs 1 /m²; brick bits and shards as debris-kit instances.

### Placement rules
- Yards, under fruit trees and along walls; feathers 0.5–2 m into weeds and meadow.

### Generator parameters
Preset: `base_sets` brown_mud_leaves_01 + leaves_forest_ground; `albedo_target` #8a6d4c; `cover_channels` twigs 0.5, litter 1.

### Open points
- The houses-and-yard analyst (task 1.2) may refine litter colour and debris density from C and E.

## S10 rubble

**Seen in:** E (tile heaps), I, J, K (rubble fields and the mound), N (debris ground around the wreck); cited from terrain.md B5, T8 and R0, not re-read here.
**Research sources:** the material table (Poly Haven concrete_debris; ambientCG Ground111, Ground107).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| pieces | 0.05–3 m: brick, tile, plaster, concrete, timber, sheet | — | measured (terrain.md B5, I, K) |
| rubble mound | 2–4 m high, spreading 15–20 m | — | measured (terrain.md B5, K) |
| ground between pieces | trampled soil with fragments 0.05–1 m | — | measured (terrain.md T8, I, K) |

### Construction and materials
- The ground surface of a collapse: crushed brick and plaster fines with pieces in it. The pieces and mounds themselves are the debris kit and the houses and urban-edge assets; this row is the fine ground that carries them.
- **Materials:** Poly Haven `concrete_debris`; ambientCG `Ground111`, `Ground107`; tinted to the brick, tile and plaster mix of the houses sheet.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| rubble ground | #8d8172 (photo K; terrain.md B5) | brick #b97541, tile #825f44 (terrain.md S3) | terrain.md |
| trampled ground with debris | #867c6a (photo K, I; terrain.md T8) | — | terrain.md |
| debris ground by a wreck | #5a5955–#6d6e6b (photo N; terrain.md R0) | — | terrain.md |

### Variants and states
- Brick-and-tile (village), concrete-and-brick (block), with or without soot; wet (darker, dust 0).

### Damage pattern
- It is the damage.

### Physics role
- **Row `rubble`** (carried): 1.0e8 · 2.4e5 · 0.7/0.6 · 0.005 m · 1.5 · 0.12 m @ 0.6 m · 0.05 /m², 0.1–0.4 m deep, r 0.1–0.4 m. Cover jutting timbers 0.5–2.0 m, 0.3 /m², 20–80 mm, 1000 N/m, hook p 0.15 at 10–60 N. Dust 0.5. Foot 17671 N/m, 1.4 mm. Smooth noise relief makes slopes of 40–50°, so feet slide into troughs; real rubble has flat faces and sharp edges: the debris kit's chunks fix that (the first attempt's X-1), after which the relief comes down.
- **Snag:** timbers, rebar (K06). **Landable:** a very uneven ground.

### Micro-detail (AST-10)
- Brick and plaster fragments 2–10 cm, 50–300 /m² (assumed), visual; larger pieces are debris-kit objects.

### Placement rules
- At the foot of every damaged building and around the block (terrain.md B5, T8), thinning outward over 1–5 m.

### Generator parameters
Preset: `base_sets` concrete_debris + Ground111; `albedo_target` #8d8172; `cover_channels` twigs 0.3 (timbers), litter 0.

### Open points
- The urban-edge analyst (task 1.6) and houses analyst (task 1.2) set the piece mix; this row only carries the fines.

## S11 asphalt

**Seen in:** M (dusty asphalt under the sedan), O (slush on the road), N (paved ground under the debris); cited from terrain.md R0, not re-read here. No clip shows asphalt (terrain.md §9).
**Research sources:** the road-width, pothole and albedo rows; the material table (Poly Haven road_damaged, road_damaged_2, asphalt_02).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| carriageway | 3.5–7.0 m: side street 3.5–5.5, main street 5.5–7.0 (roads between villages 4.5–6.0) | 6.0 m main, 4.5 m side | published (RD01 in lines-roads-debris.md, the road kit's owner: ДБН lane widths; SNiP 2.05.02-85 categories V and IV for the roads between villages) |
| shoulders | 0.5–2.0 m, soil or sod (1.75–2.0 m on roads between villages) | 1.0 m | RD01 (assumed); published SNiP for the roads between villages |
| asphalt layer | 40–80 mm over 150–250 mm crushed stone | 50 mm | assumed (RD01) |
| potholes | 0.2–2.0 m across, 40–150 mm deep, often holding water | 0.6 m, 70 mm | RD01 (published threshold of at least 0.15 m and the depth bands; measured M) |
| cracks | edge and longitudinal cracks; alligator cells 0.1–0.3 m | — | assumed (the damaged-road sets show them) |
| ravelled edge | 0.1–0.5 m broken into the shoulder | 0.3 m | assumed |
| patches | 0.5–3 m, darker | — | assumed |

### Construction and materials
- A thin, old village asphalt: grey, worn to its aggregate, cracked, patched darker, its edges breaking into a soil shoulder with grass creeping in, dust and soil film along the edges, potholes. Craters in it and their rims of broken asphalt are the road kit (RD01, task 1.4).
- **Materials:** Poly Haven `road_damaged`, `road_damaged_2`, `asphalt_02`; derived masks: cracks, patches, dust film, pothole rims, glass crumbs near wrecks (K07).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| worn asphalt | #616161 (derived: albedo 0.12, published) | — | derived |
| fresh patch | #383838–#454545 (derived: albedo 0.04–0.06) | — | derived |
| dusty asphalt, sunlit (photo) | #b7b2aa (M) | — | terrain.md R0 (photo, exposure included) |
| wet slush on a road (photo) | #525256 (O) | — | terrain.md R0 |

### Variants and states
- Worn (default), cracked and potholed (damage level), wet (D-021: a sheen, darker, μ 0.50/0.40), dusty near fields.

### Damage pattern
- Potholes, crater holes with broken plates of asphalt (RD01), fragment scars (texture only: under 1–2 cm, no physical role), burnt patches under wrecks (2–6 m, terrain.md R0).

### Physics role
- **Proposed row `asphalt`** (the id accepted by the physics sign-off, review-materials.md §2; the values are signed off with `surfaces.json` in phase 3): bearing 1.0e8, damping 2.4e5, μ 0.65/0.55 dry (assumed: hard polymer on coarse aggregate, as `masonry`), max sink 0.001 m, unload 1.5, micro-relief none (aggregate is below 1–2 cm), pitfalls: the potholes are RD01's placed pitfall features (0–5 per 100 m² by damage, 0.2–2.0 m, 40–150 mm deep), so the row's own hashed pitfalls are 0 and nothing is counted twice; cover: dust (μ 0.55/0.45 where the dust mask is set), glass crumbs near wrecks (a slippery layer, μ 0.30/0.20, assumed). Dust 0.1. Foot 17.7 kN/m, 1.4 mm: hard bounce set by the leg (derived, the stability bound). The row matches the terrain.md §8 proposal (rigid, slides on glass crumbs).
- **Snag:** none. **Landable:** yes, hard.

### Micro-detail (AST-10)
- Grit and glass crumbs 5–50 mm near wrecks (K07), dry grass tufts in edge cracks.

### Placement rules
- The village roads (WLD-06); shoulders feather into soil and sod over 0.1–0.5 m of broken edge.

### Generator parameters
Preset: `base_sets` road_damaged + asphalt_02; `albedo_target` #616161; `patch_mask` patches 0.1, cracks by damage, dust film 0.3 at the edges; `cover_channels` litter 0.1 (glass and grit near wrecks).

### Open points
- The proposed row's friction values: the physics engineer's, with `surfaces.json` in phase 3 (the id is accepted, review-materials.md §2); the lines-roads-debris analyst (task 1.4) owns the road kit's geometry.

## S12 concrete_slab

**Seen in:** PR-8 (a clip the pilot shared: concrete-slab tracks across the fields; not a lettered reference). **R** for the plates.
**Research sources:** the road-plate row (ПАГ-14, ПАГ-18, ПДН); the albedo row (new concrete 0.55).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| plate | 6.0 × 2.0 × 0.14 m (ПАГ-14, ПДН); 0.18 m thick (ПАГ-18) | 6.0 × 2.0 × 0.14 m | published |
| plate mass | about 4.1 t | — | derived: 1.68 m³ × 2450 kg/m³ (RD02) |
| track | one row lengthwise (2 m) or two rows side by side (4 m) | 2 rows | assumed (PR-8 "tracks") |
| joints | 0.02–0.30 m, weeds in them | 0.06 m | assumed (RD02, the plates' owner) |
| edge steps between plates | 0–0.08 m | 0.02 m | assumed (settlement) |
| plate tilt | 0–4° | 1° | assumed (RD02 "tilt") |
| riffled top | at least 1.5 mm deep | — | published; normal map only |
| spalled corners | 0.1–0.4 m | 1 per 3 plates | assumed |

### Construction and materials
- Precast plates laid on the field soil end to end, settling unevenly; joints fill with soil and weeds; soil, straw and mud tracked onto them from the fields; dark lichen and mud stains; corners spalled. The plates themselves (geometry, collision, gaps, tilt) are RD02 (task 1.4); this row is their surface material and the ground of the joints.
- **Materials:** Poly Haven `worn_concrete_floor`, `concrete_floor_worn_001`, `cracked_concrete`; a derived riffle normal map (under 2 mm), stain and mud masks.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| weathered plate top | #8f8f8a–#a29f98 (derived: albedo 0.25–0.35, weathered below new concrete's 0.55) | — | derived |
| joint weeds and soil | as S08, S13 | — | — |

### Variants and states
- Weathered (default), muddy (soil carried on), wet (darker, μ 0.45/0.38).

### Damage pattern
- Cracked and spalled plates, a plate tipped or broken by a hit (RD02), a crater beside the track.

### Physics role
- **Plates** are RD02 objects: boxes with `concrete` (μ 0.60/0.50, 3 mm arris), landable flags on their tops, the joints and steps real geometry.
- **Proposed row `concrete_slab`** (for terrain cells under or flush with plates, so a sample at a joint or a plate end reads hard ground): bearing 1.0e8, damping 2.4e5, μ 0.60/0.50, max sink 0.001 m, unload 1.5, no relief, no pitfalls; cover: soil and straw tracked on (μ 0.50/0.40 under the mud mask). Dust 0.2.
- **Snag:** none. **Landable:** yes, hard and flat, a step or tilt at the joints.

### Micro-detail (AST-10)
- Weeds 0.1–0.5 m in the joints (meadow stems, 100 /m² of joint, assumed); straw and clods on the plates.

### Placement rules
- Field tracks between villages and across fields (PR-8, WLD-06); 0.3–1 m of trampled soil and weeds beside the plates.

### Generator parameters
Preset: `base_sets` worn_concrete_floor + cracked_concrete; `albedo_target` #9a978f; `patch_mask` mud 0.2, lichen 0.1; `cover_channels` grass 0.3 (in joints only).

### Open points
- Whether the plates are ПАГ (riffled) or ПДН (smooth) in the PR-8 clip: a look detail settled at G2 from that clip, not a pilot question.

## S13 dirt_track

**Seen in:** L (1–8: a pale track running ahead at night, the only ground feature that reads), P (4–5: a track across a field; 11–12: a track along a belt, two ruts and a grassed median). **R** for the dimensions.
**Research sources:** the wheel-gauge row; the material table (Poly Haven grass_path_2, grass_path_3, dry_mud_field_001).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| track width | 2.5–3.5 m | 3.0 m | assumed (RD03, the ruts' owner) |
| rut centre spacing | 1.3–2.0 m | 1.6 m | published (sedan 1.30–1.35 m, truck 1.80 m, tractor 1.5–2.1 m gauges) |
| rut width | 0.25–0.5 m | 0.35 m | published (tyres 0.15–0.47 m) + assumed wear |
| rut depth | 0.05–0.30 m | 0.12 m (dry, used), deeper wet and under heavy use | assumed (RD03) |
| median | 0.6–1.2 m wide, grass 0.1–0.4 m, raised 0.03–0.1 m | 0.8 m wide, grass 0.25 m, raised 0.05 m | measured (P 11–12, L 1–8) + assumed sizes (RD03) |
| verges | 0.3–1 m of flattened grass | 0.5 m | assumed |

### Construction and materials
- Compacted loam worn into two ruts by wheels, a grassed median, flattened verges; dust in dry weather, mud and water-filled ruts in rain. Runs along belt edges (P 11–12) and across fields (P 4–5, L).
- **Materials:** Poly Haven `grass_path_2`, `grass_path_3` (compacted dirt with grass tufts), `dry_mud_field_001` (track marks); a rut / median / verge mask; the ruts are geometry.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| dry compacted rut | #857a6b–#8a7660 (10YR 5/2–5/3, derived) | — | derived |
| wet track | #514535 (10YR 3/2, derived) | #6f5327 band mean, #7b603d–#7e5f3b rut floors (P 12) | measured on P |
| at night | — | #202221 (L 2) against the field #0c0c0c: about 2.5 times the field | measured on L |

### Variants and states
- Dry (dusty, default), wet (D-021: bearing × 0.25, mud, the stickiest surface); faint (a seldom-used track: ruts 0.03 m, median merging into the meadow).

### Damage pattern
- Craters on and beside the track; deep ruts where heavy vehicles passed in mud (0.2–0.3 m, assumed).

### Physics role
- **Proposed row `dirt_track`** (the id accepted by the physics sign-off, review-materials.md §2; the values are signed off with `surfaces.json` in phase 3): bearing 3.0e7 (assumed, between yard_litter's 3.5e7 and dry_crust's 5.0e7), damping 3.5e5, μ 0.65/0.55, max sink 0.01 m, unload 2, micro-relief 0.01 m @ 0.3 m, pitfalls 0.005 /m². Cover: median grass 0.1–0.4 m, 200 /m² (assumed) with the meadow's stem values. Dust 0.6. Foot 5.3 kN/m, about 5 mm.
- **Ruts are geometry:** a placed linear feature along the track's polyline (two rut troughs and the raised median, an integer-power profile like E05's), evaluated by `SampleGround` and drawn from the same function; the 1 m heightfield can't hold a 0.35 m rut. For the world-builder and the physics engineer (open point).
- **Snag:** median grass (weak). **Landable:** yes; a leg in a rut tilts the drone 12–27° for 0.05–0.12 m ruts (derived: atan(depth / 0.24), as RD03).

### Micro-detail (AST-10)
- Median and verge stems from the meadow generator at 200 /m² and flattened; pebbles and clods 1–5 cm in the ruts (visual).

### Placement rules
- Along belts, across fields, into villages; the RD03 rut kit (task 1.4) lays them; verges feather 0.3–1 m into meadow, tilled or stubble.

### Generator parameters
Preset: `base_sets` grass_path_2 + dry_mud_field_001; `albedo_target` #857a6b; `directional` along the track; `patch_mask` median 0.3, ruts 0.25; `cover_channels` grass 0.5 (median and verges only).

The rut feature's geometry has one owner, the RD03 rut kit (lines-roads-debris.md): `rut_spacing_m`, `rut_width_m`, `rut_depth_m`, `median_h_m`, `median_grass_m`, `wander` and `braid_width_m` are its parameters, with the defaults of the dimension table above. This row carries only the surface preset.

### Open points
- The rut feature in `SampleGround`: the world-builder's and the physics engineer's (a sibling of the first attempt's X-9).

## Open points for other roles (summary)

- **Physics engineer:**
  - `surface:<id>` on earthwork filler shapes: adopted, `soil` mapped to it and `straw` added (settled by the physics sign-off, review-materials.md §1, §6);
  - the rows `asphalt`, `concrete_slab`, `dirt_track`: the ids are accepted, their values signed off with the map's `surfaces.json` in phase 3; `stubble` is a variant of `tilled`, not a row (settled, review-materials.md §2);
  - the D-021 wet rules and the `adhesion` field (accepted as the surface table's `adhesion_pa`, review-materials.md §3; pull-off 2–10 N per foot); `wetness` 0.6 / 1.0 for Rain / Severe rain: phase 3, with the surface table;
  - the trench cavity model's in-cavity wind fraction; whether the pit's overhanging lip breaks (X-8);
  - clods as rigid pieces or micro-relief (X-1).
- **World-builder:** placed features for craters 0.3–4 m (the X-9 mechanism with E05's profile), the rut feature (S13), trench hole bands with the berm they imply, crater and trench placement densities, feathered edges per the shared table.
- **Asset-artist:** fetch and pin the CC0 sets; re-measure the photo albedos on them; lift `meadow_sod`'s carried albedo (#6f6b45, 0.14) toward 0.20–0.28; build E01 and S04 first (look-lock).
- **Tech-artist:** the wet look (darkening, sheen) and P's wet check values; the trench must read as a dark line between pale parapets at 50–200 m (G); the night track reads at about 2.5 times the field (L).
- **Other analysts:** lines-roads-debris (task 1.4) can reuse this sheet's road widths, pothole bands, plate sizes and wheel gauges for RD01–RD03 (done: the road geometry numbers now live in RD01–RD03 and S11–S13 cite them); houses-and-yard (task 1.2) may refine S08 and S09 from C, D, E.
- **Vegetation sheet's asks of B:** B 9–16 shows about ten thin stems over an estimated 60–100 m of strip on the horizon, most bare or broken (at least half), but the resolution does not allow a count per 100 m; no blast-lodged weeds are visible at the craters. The vegetation assumptions stand.
- **Pilot:** nothing. Every open point is settled by research, the review sheets or a role decision.
