# Object sheet: fortifications (F01–F04)

**Owner:** reference-analyst. **Reviewed for buildability by:** asset-artist (task 1.2, `openspec/changes/p1-fortifications-sheet/review-buildability.md`). **Physics reading:** physics-engineer (task 1.3, `review-physics.md` in the same folder).

**OPSEC:** cite clips and photos by letter only. Describe classes, never a specific identifiable building, place, date or person. No file names, and no OSD values.

**Requirements:** AST-01, AST-02, AST-04, AST-05, AST-06, SEC-01, SEC-02, WLD-01 · **Decisions:** D-024, D-028, D-032, D-035 · **Change:** `p1-fortifications-sheet`, task 1.1.

**Rows:** F01–F04 from [asset-list.md](../asset-list.md) §12, in the pilot's words (D-035): concertina wire ("єгоза"), dragon's teeth, a couple of anti-tank hedgehogs, a checkpoint kit, "built as the real thing (researched: sizes, steel sections, concrete, how they stand, sag and fall), never as game props".

## How this sheet was made

- **Research only.** No letter shows a fortification, so every row says **Seen in: R** and `reference/` was not opened for this sheet. If a letter later shows one, a follow-up adds the letter and its measurements.
- **Sources are generic** (design decision 1, D-032): manufacturers' datasheets and product pages, standards, public-domain manuals and bulletins, encyclopedias and a heritage thesaurus. Each source is cited by URL in the row it supports. No news, no social media, nothing tied to a named place, unit or date; where a fact had only such a source, it is labelled assumed instead.
- **Labels:** every number is **published** (its source in the row), **derived** (the derivation in the row) or **assumed**. No row is measured.
- **The drone against an opening** is as in [houses-and-yard.md](houses-and-yard.md) ("How to read this sheet"): **0.574 m nose-first, 0.706 m on the diagonal**; an opening of clear width W between the two passes only within `θmax = asin((W − 0.254) / 0.4525) − 45°` of square (0.60 m → ±5°, 0.65 m → ±16°, 0.70 m → ±35°); the vertical envelope with legs is about **0.55 m**; the legs are a 0.24 m square under the frame (assumed, as in the other sheets, until `drone.json` fixes them in phase 5). The legs are narrower than the prop span, so in the gap tables below **the height given is that of the prop plane**, the widest part of the drone.
- **AST-05 tolerances:** collision within ±5 cm of the visual wherever the drone can touch, ±2 cm around openings and landing surfaces.
- **Physics ids** are the closed list in [physics_materials.json](../../assets-src/physics_materials.json) (`steel`, `concrete`, `fabric`, `timber` and the rest). `proposed:<id>` marks a new id for physics sign-off; the proposals are collected at the end of the sheet. Snag features use the pipeline's two kinds: `edge` (a polyline the fiber bends over, with its radius) and `hook` (a free-ended member from base to tip).
- **Porosity** is the open-area fraction a wind volume lets through (0 solid, 1 free air). For a volume of thin elements (the wire coil) it is derived from the elements' area per volume: `β = exp(−G · a · s)`, with `a` the one-sided element area per unit volume, `s` the path length through the volume and `G = 0.5` the mean projection of randomly oriented flat elements (the canopy gap relation; the orientation is assumed random, see F01).
- **How thin parts read on the feed:** about 270 px/rad on the feed and 935 px/rad on the 1080 px render (derived in [lines-roads-debris.md](lines-roads-debris.md)), so a part of width D is one feed pixel wide at 270 · D metres and one render pixel at 935 · D. Never thicken a part to make it visible.

## Reference views (REAL-02)

None. No letter shows these objects, so no reference view exists and no side-by-side sheet (REAL-03, `reference/_compare/`) can be made for them. Until a letter shows one, a row is judged against the published proportions in this sheet and by the pilot's look at G2. Nothing here is a recreated scene.

## F01 Concertina wire (єгоза)

**Seen in:** R: research only.
**Research sources:**
- barbed-tape profile, coil and clip tables of a manufacturer (BTC NATO-22 flyer and the consolidated catalogue): https://dam.atkore.com/api/public/content/razor-ribbon-nato-flyer-1 , https://images.salsify.com/images/p2wqonv1fxunbvbs61fg/Razor%20Ribbon%20Consolidated%20Catalog.pdf
- the BTO-22 and CBT-65 profiles in a manufacturer's listing: https://www.globalsources.com/Razor-wire/Concertina-Razor-Wire-1232373559p.htm
- the wire spiral and the name "єгоза" for razor-tape spirals (encyclopedia): https://ru.wikipedia.org/wiki/Спираль_Бруно ; concertina wire generally: https://en.wikipedia.org/wiki/Concertina_wire
- the triple concertina fence with angle-iron pickets (a public-domain training pamphlet of 1939): https://wartimecanada.ca/sites/default/files/documents/Dannert%20Concertina%20Wire%2021A.pdf
- the clip that joins adjacent loops (a patent): https://patents.google.com/patent/US5074529

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Tape profile | BTO-22: 15 mm across the barbs, barb clusters 22 mm long at a 34–36 mm pitch; NATO-22: 16 mm, 22 mm at 35 mm; CBT-65 (long barb): 21 mm, 65 mm at 100–101 mm | medium barb, NATO-22 class: **16 mm across, clusters 22 mm long at a 35 mm pitch** | published: the NATO-22 flyer (drawing: 22, 16, 35 mm); the BTO-22 and CBT-65 figures from the manufacturer's listing |
| Tape thickness | 0.5–0.6 mm | 0.50 mm | published, same sources |
| Core wire | 2.5 mm high-tensile steel, galvanised or stainless | 2.5 mm galvanised | published, same sources |
| Projected tape width (mean along the tape) | 7–9 mm | **8 mm** | derived two ways. Geometry: the crimped band about 3.5 mm (the 2.5 mm core in 2 × 0.5 mm of tape) plus four barbs a cluster, each a triangle about 11 × 6.25 mm, per 35 mm: 3.5 + 3.9 = 7.4 mm. Mass: 16 kg over 158 m of tape is 0.101 kg/m; less the core (0.0385 kg/m at 7850 kg/m³) and the clips (about 0.005 kg/m) leaves 0.057 kg/m of strip, 7.3 mm² or 14.6 mm of 0.5 mm strip, of which about 9.4 mm (π × 3 mm) wraps the core: 14.6 − 9.4 + 3.5 = 8.7 mm |
| Coil diameter | 0.45–1.0 m for barbed-tape coils (rows of 450, 600, 720, 750, 900, 980 and 1000 mm); 0.7–1.3 m for the older barbed-wire spiral | **0.90 m** on pickets and double-stacked; **0.45 m** on a fence top | published: the catalogue's product selector; the encyclopedia for the wire spiral |
| Turns per coil | 31–56 (56 in every concertina row) | 56 | published, catalogue |
| Yield (stretched length of one coil) | 11 m (0.45 m coil), 15 m (0.6–1.0 m coils); 4.3 m in a tight-clipped option; the wire spiral 10 m and 0.9 m high | 15 m (0.90 m), 11 m (0.45 m) | published, catalogue and flyer; encyclopedia for the spiral |
| Turn pitch, stretched | 0.15–0.40 m | **0.268 m** (0.90 m coil), 0.196 m (0.45 m) | derived: yield / turns (15 / 56, 11 / 56); the range assumed for runs stretched more or less than the yield |
| Loop extent along the run and lean | a loop spans two pitches: 0.533 m (0.6–1.0 m coils), 0.406 m (0.45 m) | lean **31°** from the cross-section plane (0.90 m), 42° (0.45 m); alternate loops lean the opposite way | published "loop spacing" (catalogue); derived: in every concertina row turns × loop spacing = 2 × yield, so each loop reaches the next-but-one; lean = atan(extent / D) |
| Clips | 3 a turn (0.45–0.6 m coils), 5 (0.72–1.0 m), 5–7 (0.98 m) | 5 (0.90 m), 3 (0.45 m) | published, catalogue |
| Clip | a steel strip crimped round two adjacent cores, 20–30 mm long | 25 × 8 × 1.2 mm | assumed (the patent gives the form, not the size) |
| Coil mass | 8 kg (0.45 m) to 17 kg (1.0 m) | 16 kg (0.90 m) = **1.07 kg per metre of run** | published, catalogue; per metre derived |
| Picket type | U-section steel pickets 76 mm wide with wire slots; angle-iron pickets; locally steel angle, rebar or timber stakes | steel angle 50 × 50 × 5 mm | published types (U picket: catalogue; angle iron: the pamphlet); the default section assumed (common stock) |
| Picket length | 0.81, 1.52, 1.83, 2.44 m (U pickets) | 1.52 m single run (about 1.0 m standing); 2.44 m double-stacked (about 1.85 m standing); 0.81 m anchor pickets at the run ends | published lengths (catalogue); the 0.5–0.6 m embedment assumed |
| Picket spacing | 3–5 m | **3.8 m** | derived: the pamphlet's stores for 50 yards (45.7 m) of triple fence are 26 long pickets in two rows, 13 a row, 12 intervals: 45.7 / 12 = 3.81 m |
| Rows and tiers | 1–3 coils side by side, 1–2 tiers | single: 1 × 1; double-stacked: 1 × 2 | published (the encyclopedia: spirals set 2–3 rows wide and 1–2 tiers high; the pamphlet's triple fence is 2 + 1) |
| Double-stacked height | 1.5–1.8 m | 1.62 m | derived: 2 × 0.90 m less 0.18 m where the upper coil nests into the lower (nesting 0.1 D each, assumed) |
| Fence-top coil | a 0.45–0.6 m coil on a fence or wall 1.5–2.5 m high | 0.45 m, resting on the fence top, tied at every post or every 1.0–1.5 m on a wall | coil size published; the mounting assumed |
| Tie wire | 1.5–3 mm soft wire | 2 mm, two turns at each picket top | assumed |

### Construction and materials
- **The tape:** a 0.5 mm steel strip punched into barb clusters (four points to a cluster, two each side) and crimped round a 2.5 mm high-tensile core wire. The blades lie in the plane of the loop, so the barbs point into and out of the coil (published: the manufacturers' product photographs on the pages above).
- **The coil:** the taped core is wound into 56 turns and adjacent turns are clipped together at 3 or 5 points round the circumference (published); successive pairs are clipped at points offset by half the clip interval (assumed: the usual concertina pattern), so when the coil is pulled out along its axis the loops lean alternately and cross, forming the diamond lattice of a concertina. Delivered closed (about 0.3–0.5 m long, assumed), it is pulled out to its yield on site and tied.
- **Single run on pickets (variant 1):** coils laid end to end along the line, the coil ends twisted together with tie wire, every coil tied at its top to a picket every 3.8 m on the home side, and an anchor picket at each end of the run holding the stretch. Coils can also lie loose on the ground pinned by short stakes or staples (the encyclopedia's "three staples" for a self-supporting coil).
- **Double-stacked (variant 2):** a second coil of the same size laid on top of the first, nested a little into it, both tied to long pickets at every picket position and to each other between.
- **Fence or wall top (variant 3):** a 0.45 m coil laid along a fence top (the yard fences of houses-and-yard.md, the farm and compound fences of urban-edge.md) or a masonry wall coping, tied to each post top or to the top wire, stretched to about the 0.196 m pitch.
- **The older class:** a spiral of plain barbed wire (published 0.7–1.3 m, 10 m long) exists too; it is not the default.
- **Materials:**
  - tape, core and clips: a plain metallic material (galvanised zinc: base colour, roughness 0.35–0.6) with a derived mask for dull zinc, white rust and rust along the loop; no texture set is needed, because the 16 mm tape is one render pixel wide at 15 m and the 3.5 mm band one feed pixel at about 0.95 m (derived from 935 and 270 px/rad). The feed shows a coil beyond a few metres as a faint grey haze of crossing lines, never as a solid shape.
  - pickets: Poly Haven `rusty_metal_02` or `rust_coarse_01` (candidates, CC0: https://polyhaven.com/a/rusty_metal_02 , https://polyhaven.com/a/rust_coarse_01 ); a timber stake uses `rough_wood` (https://polyhaven.com/a/rough_wood ).
  - every turn is a real tube along the core with real blade geometry at LOD0 (REAL-05); never a card, a texture or a line shader standing in for the wire.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Galvanised tape, new | #c4c8ca | — (no letter) | assumed |
| Galvanised tape, weathered (dull zinc, white rust) | #9a9d9c | — | assumed |
| Rusted tape (D, long exposure) | #6f4b34 | — | assumed |
| Steel angle picket, rusted | #5e4a3a | — | assumed (as the rusted steel of lines-roads-debris.md) |
| Tie wire, rusted | #5a4636 | — | assumed |

### Variants and states
- **Variants:** 1 single coil on pickets; 2 double-stacked on long pickets; 3 a coil along a fence or wall top. Each takes the profile, coil size, pitch and picket type as parameters.
- **I (taut):** pulled out to its yield, loops evenly spaced and leaning alternately, ties sound, the zinc dull; a little dry grass grown up through the bottom loops.
- **D (torn, sagging, trampled flat in places):**
  - **cut:** a fragment cuts the core of 1–3 turns; the loops on each side spring back toward the nearest picket ties and bunch to a pitch of 0.05–0.10 m, leaving a breach 1–4 m long with the cut ends curled 0.2–0.5 m out of it (assumed);
  - **sagging:** where ties have broken the coil slumps to 0.7–0.9 of its diameter and spreads to keep its loop length: 0.7 D high is 1.26 D wide, 0.8 D is 1.18 D, 0.9 D is 1.10 D (derived: an ellipse of the circle's perimeter); a double-stacked upper coil slides off to one side and lies against the lower;
  - **trampled flat:** over 1–5 m lengths a vehicle or crossing has pressed the loops into a mat 0.10–0.25 m high and up to π D / 2 = 1.41 m wide for the 0.90 m coil (derived: a loop of perimeter π D flattened into two layers);
  - **fence top:** a span pulled down off the fence face, hanging in a festoon 0.5–1.5 m below the top;
  - **rust and litter:** rust patches along the tape, dry weeds, film and bags caught in the loops (visual only).

### Damage pattern
The soft parts fail first: tie wire and clips break, then fragments cut the tape and core where they cross it. A cut stretched coil recoils toward its ties and leaves a breach; pickets lean 5–20° toward the pull or are flattened by vehicles. Nothing is thrown about: the debris is the wire itself, curled cut ends, loose clips, and litter caught in the loops (assumed).

### Physics role
- **Collision:**
  - the coil body as one compliant contact volume per span between pickets: a cylinder (a capsule along the run) of the coil's diameter, material `proposed:razor_wire`, not landable; it stands for the push the airframe feels as it enters the coil (about 200 N/m, derived under the proposed ids at the end of this sheet);
  - the turns themselves carry no rigid collision; they are the snag records below (props, legs and the fiber);
  - pickets: an angle as two thin boxes (its two legs, exact), a U picket as three, rebar or a stake as a capsule of its own radius; material `steel` (`timber` for a stake);
  - D: the sagged volume follows the slumped ellipse within ±5 cm; a trampled span is a low box (the mat) of the same material; a breach has no volume.
- **Material ids:** `proposed:razor_wire` for the coil volume and the tape; `steel` for pickets, clips and tie wire. If the physics reading maps the tape to `steel` instead, the fiber bends over steel's 1 mm edge radius rather than the 0.25 mm blade (open point).
- **Gaps:** none. A coil is never passable: seen from the side, the crossing loops leave openings about one pitch across (0.27 m) and at most two pitches (0.54 m) between loops of the same lean, under the drone's 0.574 m, and the far side of every loop closes the path again (derived). The drone passes **over** a run (0.90 m single, 1.62 m double-stacked, the fence top plus 0.45 m) or through a **D breach**, which is open ground and written as no gap polygon: its clear width is the breach length less the curled ends (0.2–0.5 m each), so a 1–4 m breach clears 0–3.6 m (derived).
- **Wind volumes:** the coil volume of each span, porosity **0.95** (I). Derivation: one turn holds π D of tape, so the one-sided tape area per coil volume is `a = 4 w / (D p)` with `w` the 8 mm projected width and `p` the pitch; across the coil the mean chord is π D / 4, so `β = exp(−(π / 2) · w / p)` averaged over the silhouette and `exp(−2 w / p)` straight through the axis, both independent of D:

  | Case | Pitch | a (m²/m³) | β, silhouette mean | β, through the axis |
  |---|---|---|---|---|
  | 0.90 m coil, stretched (I) | 0.268 m | 0.133 | **0.95** | 0.94 |
  | 0.45 m fence-top coil (I) | 0.196 m | 0.362 | 0.94 | 0.92 |
  | 0.90 m coil, bunched by a cut (D) | 0.10 m | 0.356 | 0.88 | 0.85 |
  | trampled mat 0.15 m high, 1.41 m wide (D) | 0.268 m | 0.398 | 0.75 along the 1.41 m path across it | 0.97 through its height |

  The range with w 6–10 mm and p 0.20–0.30 m is 0.92–0.97; with the loops leaning ±31° the true mean projection could sit anywhere in G 0.3–0.7, the same band. A coil barely shades the wind: it is not a windbreak.
- **Snag hazards (the core of this row):**

  | Record | Value | Label |
  |---|---|---|
  | Kind | one `edge` per turn | design decision 3 |
  | Polyline | the turn's core centreline, a closed loop through the clip points, leaning ±31° alternately; at least 24 points a turn for the 0.90 m coil and 17 for the 0.45 m (default 24 for both) | derived: the chord's sagitta R(1 − cos(π / n)) stays within half the radius, n ≥ π / acos(1 − r / 2R) |
  | `radius_m` | **0.008** (the tape's half-width across the barbs; 0.0075–0.0105 over the profiles) | published (half of 15–21 mm) |
  | Barb pitch, recorded with each edge | **0.035 m** (0.034–0.101) | published |
  | Barb cluster length | 0.022 m (0.022–0.065) | published |
  | Blade edge (half the tape thickness) | **0.00025 m** (0.00025–0.0003) | derived from the published 0.5–0.6 mm |
  | Records per metre of run | **3.7** (0.90 m coil), 5.1 (0.45 m), 7.5 double-stacked | derived: 56 turns per 15 m or 11 m of yield |
  | Points per metre of run | about 90 (single, 0.90 m) | derived: 3.7 × 24 |
  | Clips | 5 (or 3) per turn: short `edge` records (25 mm, radius 1 mm) where they join two turns | published count; size assumed |
  | Pickets | the picket top and the tie-wire tails: `hook`, radius 1–2.5 mm | assumed |

  Props and legs strike every turn they touch (a logged prop strike per turn); the fiber laid over a run touches 3–4 turns per metre of run it crosses and every one of them is a snag. **Whether a barb cuts the fiber or only holds it is not ruled here:** it is the physics reading's question (task 1.3). The inputs it needs are above: a 0.5 mm blade (edge 0.25 mm), four points to a cluster 22 mm long, clusters every 35 mm, on a core that will not move under the drone (a 2.5 mm high-tensile core breaks at about 7 kN, derived at an assumed 1500 MPa).
- **Landable surfaces:** none. A drone set down on a coil sinks into it (about 0.25 m under its 49 N at the derived 200 N/m) and is held by the barbs.

### Placement rules (for the world)
- **In front of trenches and firing positions** (E01–E04, placed by the layout): a run parallel to the trench line, 30–100 m out, beyond hand-grenade throw and inside small-arms range (assumed: the usual doctrine; no primary manual could be reached to cite it).
- **Along belt edges and field edges** that a trench line follows, and **across road shoulders and verges** to tie the dragon's-teeth rows (F02) and the checkpoint (F04) into the ditches and belts so they cannot be bypassed (assumed).
- **On fences and wall tops** of compounds in the town: the farm complex, a utility or industrial yard, the edge of a garage block (variant 3) (assumed).
- **Lengths:** a run is whole coils joined end to end: 15 m (11 m on fence tops) per coil, tens to a few hundred metres per run (assumed).
- **Mix:** single runs most often; double-stacked on road approaches and around positions; fence tops in the town. The D state where the run crosses a track, a field road or a shelled area: breaches and trampled spans where vehicles and people cross (assumed).
- **Orientation:** along the defended line, pickets on the home side.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `layout` | enum | single, double_stacked, fence_top | single | Variant: tiers, picket length, mounting |
| `rows`, `tiers` | int, int | 1–3, 1–2 | from `layout` (1, 1) | Coils side by side and stacked (published 2–3 rows by 1–2 tiers) |
| `profile` | enum | bto22, nato22, cbt65 | nato22 | Preset of the four tape values below |
| `tape_width_m` | m | 0.015–0.021 | 0.016 | Width across the barbs; the snag `radius_m` is half |
| `barb_length_m` | m | 0.022–0.065 | 0.022 | Length of a barb cluster along the tape |
| `barb_pitch_m` | m | 0.034–0.101 | 0.035 | Cluster spacing; written into every snag record |
| `tape_thickness_m` | m | 0.0005–0.0006 | 0.0005 | Blade thickness; its half is the blade edge |
| `core_diameter_m` | m | 0.0025 | 0.0025 | Core wire (fixed by the published profiles) |
| `coil_diameter_m` | m | 0.45–1.0 | 0.90 (fence top 0.45) | Coil size; sets the yield and clip count defaults |
| `turns` | int | 31–56 | 56 | Turns per coil |
| `clips_per_turn` | int | 3–7 | 5 (3 under 0.7 m) | Clip points; the lattice pattern |
| `pitch_m` | m | 0.10–0.40 | yield / turns (0.268; 0.196) | Stretched turn pitch; sets snag density, lean and porosity |
| `loop_lean_deg` | deg | 20–45 | atan(2 · pitch / D) (31°; 42°) | Lean of each loop, alternating |
| `run_length_m` | m | 2–500 | from placement | Run length, cut into whole coils plus a part coil |
| `nest_fraction` | 0–0.3 | — | 0.1 | How far an upper coil sinks into the lower |
| `picket_type` | enum | u_steel, angle, rebar, timber, staples | angle | Section and look |
| `picket_section_m` | m | 0.016–0.10 | 0.05 | Angle leg, bar or stake diameter |
| `picket_length_m` | m | 0.6–2.44 | 1.52 (2.44 double-stacked) | Picket length |
| `picket_embed_m` | m | 0.3–0.7 | 0.5 | Driven depth |
| `picket_spacing_m` | m | 3.0–5.0 | 3.8 | Tie points along the run |
| `picket_side` | enum | home, enemy, inside, alternate | home | Where the pickets stand against the coil |
| `anchor_pickets` | bool | — | true | 0.81 m pickets holding each run end |
| `support` | enum | fence_posts, top_wire, wall_top | fence_posts | Fence-top mounting |
| `tie_spacing_m` | m | 1.0–3.0 | post spacing | Fence-top tie points |
| `finish` | enum | galvanised_new, galvanised_dull, rusted | galvanised_dull | Material preset |
| `damage` | 0–1 | — | 0 (I) / 0.6 (D) | I below 0.25, D above; the four items below default from it |
| `cuts` | int | 0–3 per coil | from damage | Breaches: the coil springs back to the next tie, cut ends curl |
| `sag` | 0–0.3 | — | from damage | Height lost where ties broke; the width grows to keep the loop length |
| `trampled_spans` | list | 0–5 m each | from damage | Flattened mats 0.10–0.25 m high, up to π D / 2 wide |
| `litter_caught` | 0–1 | — | 0.2 | Dry weeds, film and bags in the loops (visual only) |
| `points_per_turn` | int | 16–48 | 24 | Polyline resolution of each turn's snag record |
| `seed` | int | — | — | Every random choice |

Outputs: LOD0 with every turn as a tube along the core and the blade clusters as real flat geometry; LOD1 the turns as tubes of the tape's projected width without clusters (a 16 mm tape drops under two render pixels at about 7.5 m, derived); LOD2 the coil as a sparse set of loop strips; collision, wind volume and the snag records as above.

### Open points
- **Cut or hold:** whether a barb (a 0.25 mm blade edge, four points per cluster every 35 mm) cuts the fiber or only holds it: physics reading, task 1.3.
- **Which radius the fiber bends over:** the brief sets each turn's `radius_m` to the tape's half-width (8 mm, the reach of the barbs from the core). In `physics_materials.json` a snag's `radius_m` is the fiber's bend radius and overrides the material's, so an 8 mm radius hides the 0.25 mm blade. Whether the record carries the half-width for contact and the blade edge and barb pitch as extra fields (a schema change), or another split: physics reading, then the asset change.
- **`proposed:razor_wire`** (values at the end of the sheet), or a mapping to `steel`: physics reading.
- **Snag budget:** a 100 m single run carries about 370 turn records and 9,000 points (derived); whether `StaticContacts` takes them per turn or needs a coarser record per span: physics engineer and world-builder, phase 3.
- **The stand-off of protective wire** (30–100 m) is assumed; the layout may set it from the trench layout. Not a pilot question.

## F02 Dragon's teeth (зуби дракона)

**Seen in:** R: research only.
**Research sources:**
- height, the truncated reinforced-concrete pyramid, staggered rows: https://en.wikipedia.org/wiki/Dragon%27s_teeth_(fortification)
- rows, row spacing and spacing in a row (a public-domain US War Department bulletin, Tactical and Technical Trends no. 35, on concrete teeth and steel obstacles): https://lonesentry.com/articles/ttt07/coastal-antitank-obstacles.html
- the proportions of the class: a truncated pyramid 3 ft square by 2 ft high, and a triangular pyramid with 6 ft sides and 5 ft high (a heritage thesaurus): https://her.nottinghamshire.gov.uk/ThesaurusTerm/160305 , https://her.nottinghamshire.gov.uk/ThesaurusTerm/160309
- rows in a set order, gaps left as dog-leg exits (encyclopedia): https://ru.wikipedia.org/wiki/Надолбы
- the density of heavy concrete, 2400 kg/m³ (the note to the mass table of ГОСТ 13579-78): https://dnaop.com/html/47138/doc-%D0%93%D0%9E%D0%A1%D0%A2_13579-78_

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Height | 0.8–1.2 m | **1.0 m** | published: 0.9–1.2 m (encyclopedia); 2 ft 8 in–3 ft, 0.81–0.91 m (bulletin) |
| Shape | a truncated square pyramid (a triangular one exists) | square frustum | published (encyclopedia, thesaurus) |
| Base | 0.9–1.5 m square | **1.2 m** | derived: the published proportions give base / height 1.2 (the triangular pyramid, 6 ft by 5 ft) to 1.5 (the frustum, 3 ft by 2 ft); the default takes 1.2 × the height |
| Top | 0.2–0.4 m square | 0.3 m | assumed |
| Face angle | 61–72° from horizontal | 65.8° | derived: atan(h / ((b − t) / 2)) |
| Volume and mass | 0.35–1.16 m³, 0.84–2.8 t | 0.630 m³, **1.51 t** | derived: V = h / 3 · (b² + b·t + t²), at 2400 kg/m³ (published) |
| Centre of mass | — | 0.32 m above the base | derived (frustum centroid) |
| Reinforcement | 4–8 bars Ø 10–12 mm with links Ø 6 mm at 0.2–0.3 m, 30 mm cover | 4 corner bars Ø 12 mm | assumed (published only as "reinforced") |
| Lifting loop | a bar loop Ø 10–12 mm, 0.08–0.15 m proud of the top; or none | Ø 12 mm, 0.10 m | assumed (precast practice) |
| Rows | 3–5, staggered | **4** | published: three or four staggered rows (bulletin), "often a second and third, sometimes a fourth" (encyclopedia); 5 from the asset list |
| Spacing in a row (centres) | 1.8–2.4 m | **2.0 m** | published (bulletin: 6–8 ft) |
| Row spacing (centres) | 1.8–2.4 m | **2.0 m** | published (bulletin: 6–8 ft); the asset list's "rows 1–2 m apart" is the clear distance, 2.0 − 1.2 = 0.8 m at the base (derived) |
| Stagger | half the spacing in a row | 1.0 m | published "staggered"; the half assumed |
| Depth of a field | 4–11 m | 7.2 m (4 rows) | derived: (rows − 1) × row spacing + base |
| Setting | on the ground on a sand bed, or 0.1–0.3 m into it | on the ground, 0.05 m sand bed | assumed |

### Construction and materials
- Precast heavy concrete cast in steel forms as a truncated square pyramid, lightly reinforced, with a lifting loop at the top; set by crane on a levelled strip in staggered rows. Faces smooth with form-joint lines along the arrises and small blowholes; the base edge chipped from handling. A cast-in-place variant shows board marks and rougher arrises (assumed).
- **Materials:** Poly Haven `concrete_wall_003` or `rough_concrete` with a derived mask (algae and lichen on the shaded side near the ground, a dirt splash band 0.1–0.3 m at the foot, rust bleed under the loop, chips) (candidates, CC0: https://polyhaven.com/a/concrete_wall_003 , https://polyhaven.com/a/rough_concrete ); broken faces Poly Haven `concrete_debris` (https://polyhaven.com/a/concrete_debris ); bars `rusty_metal` (https://polyhaven.com/a/rusty_metal ).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Fresh precast concrete | #b2afa6 | — (no letter) | assumed |
| Weathered concrete | #8e8b83 | — | assumed (close to the weathered pole of lines-roads-debris.md, #9b9890) |
| Algae and lichen, shaded foot | #6b6e58 | — | assumed |
| Splash dirt at the foot | #5a4e40 | — | assumed (the moist spoil range of the earthworks sheet) |
| Fresh fracture (D) | #a8a49a | — | assumed |

### Variants and states
- **Variants:** 1 the square frustum (default); 2 the triangular pyramid of the same height (the published class). Size by the parameters.
- **I:** in rows, square to the row, weathered, grass grown up round the feet.
- **D:**
  - **chipped:** spalls 0.05–0.2 m off arrises and corners on the blast side, the corner bars exposed 0.05–0.3 m where the spall reaches them;
  - **toppled:** 5–15 % of the teeth pushed over onto a face by a blast or a vehicle, lying with the base standing at 66° and the opposite face at 48° from horizontal (a toppled tooth stands about 1.1 m high, 1.2 × sin 65.8°, and covers about 1.2 × 1.1 m) (derived), or tilted 10–30° on a crater lip;
  - **shifted:** rotated 5–30° about the vertical or moved 0.2–1 m out of the grid;
  - **buried:** part of a row half-covered by a pushed earth ramp, the published way of crossing such rows (encyclopedia: earth pushed over the teeth so tanks could cross).

### Damage pattern
Concrete spalls and cracks; it does not splinter. Chips and corner pieces 0.05–0.3 m lie round the foot; a crater in the row leaves a tooth tilted on its lip with its sand bed exposed. Bars show only at deep spalls (assumed light reinforcement).

### Physics role
- **Collision:** a frustum is not one of the pipeline's primitives (box, sphere, capsule, cylinder). As stacked boxes matched to the faces at each box's mid-height, n boxes stay within 0.205 / n m of the sloping face (derived): **11 boxes** meet AST-05's ±2 cm round the openings between teeth (18.7 mm), 5 meet only the ±5 cm elsewhere (41 mm). A frustum or convex-hull primitive would make each tooth one exact shape (open point). The loop and exposed bars are capsules of their own radius. Material `concrete`, `steel` for loop and bars. **Not landable** (a 0.3 m top).
- **Gaps** (default field, teeth square to the rows; the generator writes each from the actual placement):

  | Prop-plane height | Tooth width | In a row, between neighbours | Along the rows, between two rows | Straight across the rows | Diagonal lane at 63° to the rows |
  |---|---|---|---|---|---|
  | 0 (ground) | 1.20 m | 0.80 m: any yaw | 0.80 m: any yaw | closed (the next row overlaps by 0.20 m) | 0.18 m: no |
  | 0.3 m | 0.93 m | 1.07 m: any yaw | 1.07 m: any yaw | 0.07 m: no | 0.54 m: no |
  | 0.5 m | 0.75 m | 1.25 m: any yaw | 1.25 m: any yaw | 0.25 m: no | 0.78 m: any yaw |
  | 1.0 m (top) | 0.30 m | 1.70 m: any yaw | 1.70 m: any yaw | 0.70 m: within ±35° | 1.39 m: any yaw |

  - **In a row:** the gap polygon is the vertical trapezoid in the row's plane between the facing faces of two neighbours, 0.80 m wide at the ground and 1.70 m at the tops, closed at the height of the tops; its largest inscribed circle is **1.0 m** (limited by the height). Passable at any yaw at every height.
  - **Straight across a field** (perpendicular to the rows) the staggered rows close the line below 0.86 m (nose-first passes from 0.86 m, any yaw from 1.01 m, that is over the tops) (derived: clear width 1.0 − w(z) with the teeth every 1.0 m across).
  - **The diagonal lane** through the stagger, at atan(2.0 / 1.0) = 63° to the rows, opens above the base: nose-first from a prop plane of 0.33 m, any yaw from 0.44 m (derived: lane spacing s·r / √((s/2)² + r²) = 1.79 m, less the tooth's width across the lane, w · (cos 63° + sin 63°)).
  - So a drone crosses a field low by weaving through the in-row gaps and along the rows, or straight along the 63° diagonal with its props above 0.44 m; it never flies straight across the rows below the tops. A toppled, shifted or buried tooth changes the neighbouring gaps; the generator recomputes them.
- **Wind volumes:** each tooth solid (porosity 0); a field is a sparse array (teeth cover 0.36 of the field's plan area at the base and 0.02 at the top, derived), so the grid sees it as a low porous band; the solver works from the tooth volumes.
- **Snag hazards:** the 4 sloping arrises and the 4 top edges of every tooth (`edge`, concrete's 3 mm); the lifting loop (`edge` along the bar, radius 6 mm); exposed bars (`hook`, radius 6 mm); a spalled arris (`edge`, 1 mm, assumed for a fresh fracture).
- **Landable surfaces:** none. The intact top is 0.3 m square; a toppled tooth's upturned face slopes at 48° (derived).

### Placement rules (for the world)
- **Across road approaches** to the settlements and **across the gaps between tree belts**, in fields of 3–5 staggered rows parallel to the defended line, each field's ends tied into an obstacle (a belt, a ditch, a trench line or a wire run, F01) so it cannot be driven round (assumed doctrine).
- **Along trench lines and earth banks**, a field 20–100 m in front of the trench (the encyclopedia notes lines beside berms and anti-tank trenches; the distance assumed).
- **On roads**, a lane left open as a dog-leg exit (published: gaps left as dog-leg exits), closable with hedgehogs (F03) or blocks (F04).
- **Length:** 20–300 m per field (assumed). Wire (F01) often runs in front of or among the rows (the encyclopedia's barbed wire with teeth).
- Not inside the villages (assumed). D teeth where the field crosses a shelled area or a track.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `shape` | enum | square_frustum, triangular_pyramid | square_frustum | Variant |
| `height_m` | m | 0.8–1.2 | 1.0 | Tooth height |
| `base_m` | m | 0.9–1.5 | 1.2 | Base side |
| `top_m` | m | 0.0–0.4 | 0.3 | Top side (0 for a sharp pyramid) |
| `rows` | int | 1–5 | 4 | Rows |
| `teeth_per_row` | int | 2–150 | from length | Teeth in a row |
| `spacing_m` | m | 1.8–2.4 | 2.0 | Centres in a row; sets the in-row gaps |
| `row_spacing_m` | m | 1.8–2.4 | 2.0 | Centres between rows; sets the lanes |
| `stagger` | 0–1 | — | 0.5 | Offset of alternate rows as a fraction of the spacing |
| `yaw_jitter_deg` | deg | 0–30 | 3 | Rotation of each tooth about the vertical |
| `position_jitter_m` | m | 0–0.3 | 0.05 | Placement scatter |
| `bed_m` | m | 0–0.3 | 0.05 | Sand bed under the base, or burial depth |
| `bars` | int | 0–8 | 4 | Corner bars (exposed only at deep spalls) |
| `bar_d_m` | m | 0.010–0.012 | 0.012 | Bar diameter |
| `lifting_loop` | bool | — | true | Loop on the top |
| `collision_boxes` | int | 5–16 | 11 | Stacked boxes per tooth (±2 cm at 11) |
| `damage` | 0–1 | — | 0 / 0.5 | I below 0.25, D above; the items below default from it |
| `spalls` | int | 0–6 per tooth | from damage | Chipped arrises and corners, 0.05–0.2 m |
| `toppled_fraction` | 0–0.3 | — | from damage | Teeth lying on a face or tilted on a crater lip |
| `shifted_fraction` | 0–0.3 | — | from damage | Teeth rotated or moved off the grid |
| `buried_span_m` | m | 0–20 | 0 | A pushed earth ramp over part of the rows |
| `weathering` | 0–1 | — | 0.6 | Algae, lichen, splash dirt, rust bleed |
| `seed` | int | — | — | Every random choice |

Outputs: one tooth mesh per variant and state, instanced by the field generator with the gaps written per pair of neighbours; LOD0 with chips and the loop; LOD1 the plain frustum; LOD2 a box impostor row beyond 300 m (assumed).

### Open points
- **A frustum (or small convex-hull) collision primitive:** 11 stacked boxes per tooth meet AST-05; one exact shape would be cheaper for a field of 50–200 teeth. Asset change and physics engineer.
- **The base proportion** (1.2 × the height) is derived from two published classes; the pilot's look at G2 settles whether today's teeth read as squatter or slimmer (a look, not a number: D-024).
