# Object sheet: fortifications (F01–F04)

**Owner:** reference-analyst. **Reviewed for buildability by:** asset-artist (task 1.2, `openspec/changes/p1-fortifications-sheet/review-buildability.md`). **Physics reading:** physics-engineer (task 1.3, `review-physics.md` in the same folder).

**OPSEC:** cite clips and photos by letter only. Describe classes, never a specific identifiable building, place, date or person. No file names, and no OSD values.

**Requirements:** AST-01, AST-02, AST-04, AST-05, AST-06, SEC-01, SEC-02, WLD-01 · **Decisions:** D-024, D-028, D-032, D-035 · **Change:** `p1-fortifications-sheet`, task 1.1, reworked in task 1.4 from both readings.

**Rows:** F01–F04 from [asset-list.md](../asset-list.md) §12, in the pilot's words (D-035): concertina wire ("єгоза"), dragon's teeth, a couple of anti-tank hedgehogs, a checkpoint kit, "built as the real thing (researched: sizes, steel sections, concrete, how they stand, sag and fall), never as game props".

## How this sheet was made

- **Research only.** No letter shows a fortification, so every row says **Seen in: R** and `reference/` was not opened for this sheet. If a letter later shows one, a follow-up adds the letter and its measurements.
- **Sources are generic** (design decision 1, D-032): manufacturers' datasheets and product pages, standards, public-domain manuals and bulletins, encyclopedias and a heritage thesaurus. Each source is cited by URL in the row it supports. No news, no social media, nothing tied to a named place, unit or date; where a fact had only such a source, it is labelled assumed instead.
- **Labels:** every number is **published** (its source in the row), **derived** (the derivation in the row) or **assumed**. No row is measured.
- **The drone against an opening** is as in [houses-and-yard.md](houses-and-yard.md) ("How to read this sheet"): **0.574 m nose-first, 0.706 m on the diagonal**; an opening of clear width W between the two passes only within `θmax = asin((W − 0.254) / 0.4525) − 45°` of square (0.60 m → ±5°, 0.65 m → ±16°, 0.70 m → ±35°); the vertical envelope with legs is about **0.55 m**; the legs are a 0.24 m square under the frame (assumed, as in the other sheets, until `drone.json` fixes them in phase 5). The legs are narrower than the prop span, so in the gap tables below **the height given is that of the prop plane**, the widest part of the drone.
- **AST-05 tolerances:** collision within ±5 cm of the visual wherever the drone can touch, ±2 cm around openings and landing surfaces.
- **Physics ids** are the closed list in [physics_materials.json](../../assets-src/physics_materials.json) (`steel`, `concrete`, `fabric`, `timber` and the rest), plus two new ids this sheet proposed and the physics reading accepted, `razor_wire` and `sandbag` (their rows are at the end of the sheet; the asset change adds them to the file). Snag features use the pipeline's two kinds: `edge` (a polyline the fiber bends over, with its radius) and `hook` (a free-ended member from base to tip). F01's turn records also carry the two optional fields the physics reading added, `edge_radius_m` and `barb_pitch_m`.
- **"From `damage`"** in a generator table means: the item takes its I value at `damage` 0, its D value (given in brackets in the Default cell) at the row's D default (0.6 for F01, 0.5 for F02–F04) and the top of its range at `damage` 1, linearly between; where the D value is a range, `seed` draws a point in it and that point is interpolated; an integer item rounds down; any item can be set explicitly, and then `damage` no longer drives it. Whatever has no number (masks, wear, poses) reads as I below 0.25 and as D above. This is the houses sheet's rule ([houses-and-yard.md](houses-and-yard.md), "Shared house generator parameters") for a group with two states.
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
| Turns per coil | 31–56 over the catalogue's clipped concertina coils of 0.45–1.0 m (31, 50, 51 or 56 by product: every NATO-22 row 56, the military BTC row at 0.90 m 50) | 56 | published, catalogue |
| Yield (stretched length of one coil) | 11 m (0.45 and 0.6 m coils), 15 m (0.72–1.0 m coils; the 0.72 m coil also comes at 11 m); 4.3 m in a tight-clipped option; the wire spiral 10 m and 0.9 m high | 15 m (0.90 m), 11 m (0.45 m; 0.6 m) | published, catalogue and flyer (the NATO-22 rows: 36 ft, 11 m at 450 and 600 mm; 50 ft, 15 m from 720 mm); encyclopedia for the spiral |
| Turn pitch, stretched | 0.15–0.40 m | **0.268 m** (0.90 m coil), 0.196 m (0.45 m; 0.6 m) | derived: yield / turns (15 / 56, 11 / 56); the range assumed for runs stretched more or less than the yield |
| Tape a turn | π D | **2.83 m** (0.90 m coil), 1.41 m (0.45 m) | derived: π D, which is the mass row's 158 m of tape over 56 turns |
| Loop extent along the run and lean | a loop spans two pitches: 0.533 m (0.72–1.0 m coils), 0.406 m (0.45 and 0.6 m coils; the 0.72 m coil also), 0.600 m (the military BTC row at 0.90 m, 50 turns) | lean **36.6°** from the cross-section plane (0.90 m), 60.6° (0.45 m), 40.9° (0.6 m); alternate loops lean the opposite way | published "loop spacing" (catalogue and flyer); derived: in every concertina row turns × loop spacing = 2 × yield, so each loop reaches the next-but-one; a flat loop of the coil's diameter reaches D sin(lean), so lean = asin(2p / D) (the turn model below) |
| Standing section (end-on) | an ellipse D high and √(D² − 4p²) wide | **0.90 m high × 0.72 m wide** (0.90 m coil), 0.45 × 0.22 m (0.45 m), 0.60 × 0.45 m (0.6 m) | derived (the turn model below) |
| Clips | 3 a circle (0.45–0.6 m coils), 5 (0.72–1.0 m), 5–7 (0.98 m), on the closed coil | 2 to each neighbour, at the two points where the loops cross: 4 clip points a turn | published counts (catalogue; the encyclopedia gives 3 to 7 rivets joining the turns in a staggered pattern); the model's count derived (the turn model below): flat loops meet a neighbour at two points only. This is a simplification: on the real coil the loops kink at each of the 3–7 staggered clips, which the model leaves out; whether 4 clip points a turn read right is a G2 look point (Open points) |
| Clip | a steel strip crimped round two adjacent cores, 20–30 mm long | 25 × 8 × 1.2 mm | assumed (the patent gives the form, not the size) |
| Coil mass | 8 kg (0.45 m) to 17 kg (1.0 m) | 16 kg (0.90 m) = **1.07 kg per metre of run** | published, catalogue; per metre derived |
| Picket type | U-section steel pickets 76 mm wide with wire slots; angle-iron pickets; locally steel angle, rebar or timber stakes | steel angle 50 × 50 × 5 mm (4.8 cm², 3.77 kg/m); a U picket 76 mm wide, 38 mm deep, 3 mm wall (4.4 cm², 3.4 kg/m) | published types (U picket: catalogue; angle iron: the pamphlet) and the 76 mm width; the 50 × 50 × 5 row published (EN 10056-1 table: https://roymech.org/Useful_Tables/Sections/AngleA_dim_prop.html ); the default section, the U's depth and wall assumed (common stock), its area and mass derived |
| Picket length | 0.81, 1.52, 1.83, 2.44 m (U pickets) | 1.52 m single run (about 1.0 m standing); 2.44 m double-stacked (about 1.85 m standing); 0.81 m anchor pickets at the run ends | published lengths (catalogue); the 0.5–0.6 m embedment assumed |
| Picket spacing | 3–5 m | **3.8 m** | derived: the pamphlet's stores for 50 yards (45.7 m) of triple fence are 26 long pickets in two rows, 13 a row, 12 intervals: 45.7 / 12 = 3.81 m |
| Rows and tiers | 1–3 coils side by side, 1–2 tiers | single: 1 × 1; double-stacked: 1 × 2 | published (the encyclopedia: spirals set 2–3 rows wide and 1–2 tiers high; the pamphlet's triple fence is 2 + 1) |
| Double-stacked height | 1.5–1.8 m | 1.62 m | derived: 2 × 0.90 m less 0.18 m where the upper coil nests into the lower (nesting 0.1 D each, assumed) |
| Fence-top coil | a 0.45–0.6 m coil on a fence or wall 1.5–2.5 m high | 0.45 m, resting on the fence top, tied at every post or every 1.0–1.5 m on a wall | coil size published; the mounting assumed |
| Tie wire | 1.5–3 mm soft wire | 2 mm, two turns at each picket top | assumed |

**The turn model** (derived from the rows above; one geometry for the generator, the snag records, the clips and the volume). D is the coil diameter, p the stretched pitch, λ the lean.
1. **Tape.** Each turn keeps the coil's own tape, π D: 2.83 m a turn for the 0.90 m coil (158 m / 56) and 1.41 m for the 0.45 m.
2. **Shape.** A turn is a flat circle of diameter D, in a vertical plane turned λ from the cross-section plane, alternate turns turned the other way, centres on the run's axis one pitch apart. A flat loop reaches D sin λ along the run, and the published loop spacing is two pitches, so **λ = asin(2p / D)**: **36.6°** (0.90 m, p 0.268 m), **60.6°** (0.45 m, p 0.196 m) and 40.9° (0.6 m, p 0.196 m: its published 11 m yield over 56 turns; the 0.406 m loop spacing gives p 0.203 m and 42.6°). The earlier atan(2p / D) would need a loop of 3.06 m of tape, 8 % more than the coil holds.
3. **Standing section.** End-on, every loop projects to the same ellipse, D high and D cos λ = √(D² − 4p²) wide: **0.90 × 0.72 m**, **0.45 × 0.22 m** and 0.60 × 0.45 m (0.44 m at p 0.203 m). The height is unchanged, so the drone still clears a single run above 0.90 m (Physics role, Gaps).
4. **Where turns meet.** Two neighbouring loops lie in planes 2λ apart. The planes cross on a vertical line half a pitch along the run and (D / 4) cos λ off the axis (0.18 m; 0.055 m). Both loops pass through that line at ±(√3 / 2)(D / 2) above and below the axis (±0.39 m; ±0.195 m), which is 60° either side of each loop's in-plane horizontal, at any pitch. A loop meets no other turn: the line it would share with the next loop of the other lean lies 1.5 D / 2 from its centre, outside it. So each turn meets each neighbour at exactly two points: **2 clips a pair, 4 clip points a turn**, at 60°, 120°, 240° and 300° round the loop, and 7.5 clips per metre of run (10.2 on the 0.45 m coil).
5. **Side view.** Loops of one lean stand 2p cos λ apart (0.43 m; 0.19 m), and a loop of the other lean crosses every such opening: the diamond lattice of a concertina.
6. **Sag (D).** A slumped loop keeps its tape: an ellipse of perimeter π D in its own plane, 0.7 D high and 1.26 D wide, 0.8 D and 1.18 D, 0.9 D and 1.10 D. It still reaches two pitches along the run, so its lean becomes asin(2p / W) for its in-plane width W, and its end-on section √(W² − 4p²) wide: **0.63 × 1.00 m, 0.72 × 0.92 m, 0.81 × 0.83 m** for the 0.90 m coil.
7. **Bunched (D, a cut).** The same rule at a 0.05–0.10 m pitch: the coil stands nearly round, 0.90 × 0.88–0.89 m.
8. **The volume.** Two capsules along the run, offset along the end-on ellipse's long axis, fitted to it (the largest distance between the capsules' outline and the ellipse, sampled every 1.5°; derived):

   | Section | Ellipse (high × wide) | One capsule (r = the mean semi-axis) | Two capsules | Fit |
   |---|---|---|---|---|
   | I, 0.90 m coil | 0.90 × 0.72 m | 44 mm | r 0.358 m, centres ±0.079 m above and below the axis | **13 mm** |
   | I, 0.45 m fence-top coil | 0.45 × 0.22 m | 57 mm: over ±5 cm | r 0.121 m, ±0.083 m above and below | **21 mm** from the ellipse to the capsules, 27 mm back (24 and 36 mm at the 0.19 m width the 0.406 m loop spacing gives) |
   | D, sagged to 0.7 D | 0.63 × 1.00 m | 92 mm | r 0.321 m, ±0.149 m side by side | **30 mm** |
   | D, sagged to 0.8 D | 0.72 × 0.92 m | 50 mm | r 0.356 m, ±0.089 m side by side | **15 mm** |
   | D, sagged to 0.9 D | 0.81 × 0.83 m | 4 mm | r 0.408 m, ±0.002 m (one in effect) | **4 mm** |

   All are inside ±5 cm. The best-sized box misses the 0.90 m coil's sections by 67–71 mm, over ±5 cm, but comes within 24–26 mm of the fence-top coil's, as close as its two capsules are both ways (27 mm; 36 mm at 0.19 m wide), so a box would hold ±5 cm there too (derived the same way, both directions). The sheet keeps two capsules on every coil, one shape for every state; only the trampled mat is a box.
9. **Limit.** The model needs 2p < D; the generator's `check` refuses a pitch above 0.47 D (a lean above 70°).

### Construction and materials
- **The tape:** a 0.5 mm steel strip punched into barb clusters (four points to a cluster, two each side) and crimped round a 2.5 mm high-tensile core wire. The blades lie in the plane of the loop, so the barbs point into and out of the coil (published: the manufacturers' product photographs on the pages above).
- **The coil:** the taped core is wound into 56 turns and adjacent turns are clipped together (published: 3, 5 or 7 clips a circle on the closed coil, catalogue; 3 to 7 rivets joining the turns in a staggered pattern, encyclopedia), so when the coil is pulled out along its axis the loops lean alternately and cross, forming the diamond lattice of a concertina. The sheet builds each stretched turn as a flat loop clipped to each neighbour at their two crossings (the turn model above), a simplification: the real loops kink at every one of their staggered clips. Delivered closed (about 0.3–0.5 m long, assumed), it is pulled out to its yield on site and tied.
- **Single run on pickets (variant 1):** coils laid end to end along the line, the coil ends twisted together with tie wire, every coil tied at its top to a picket every 3.8 m on one side of the run (`picket_side`), and an anchor picket at each end of the run holding the stretch. Coils can also lie loose on the ground pinned by short stakes (published: the encyclopedia's spirals are tied together with wire and fixed to the ground with stakes) or staples (assumed).
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
  - **sagging:** where ties have broken the coil slumps to 0.7–0.9 of its diameter and each loop spreads in its own plane to keep its tape: 0.7 D high is 1.26 D wide in the plane, 0.8 D is 1.18 D, 0.9 D is 1.10 D (derived: an ellipse of the circle's perimeter), so the 0.90 m coil stands 0.63 × 1.00 m, 0.72 × 0.92 m or 0.81 × 0.83 m end-on (the turn model, item 6); a double-stacked upper coil slides off to one side and lies against the lower;
  - **trampled flat:** over 1–5 m lengths a vehicle or crossing has pressed the loops into a mat 0.10–0.25 m high and up to π D / 2 = 1.41 m wide for the 0.90 m coil (derived: a loop of perimeter π D flattened into two layers);
  - **fence top:** a span pulled down off the fence face, hanging in a festoon 0.5–1.5 m below the top beside the face: its axis lies half the support's width plus the coil's 0.11 m end-on half-width out from the support's centre line (derived);
  - **rust and litter:** rust patches along the tape, dry weeds, film and bags caught in the loops (visual only).

### Damage pattern
The soft parts fail first: tie wire and clips break, then fragments cut the tape and core where they cross it. A cut stretched coil recoils toward its ties and leaves a breach; pickets lean 5–20° toward the pull or are flattened by vehicles. Nothing is thrown about: the debris is the wire itself, curled cut ends, loose clips, and litter caught in the loops (assumed).

### Physics role
- **Collision:**
  - the coil body as one compliant contact volume per span between pickets: **two capsules along the run** fitted to the end-on ellipse within ±5 cm (13 mm for the 0.90 m coil, 21 mm for the 0.45 m; the turn model, item 8), material `razor_wire`, not landable. It stands for the push the airframe feels as it enters the coil: **60 N/m per touching part** (one loop under a leg, an arm or the body; about 240 N/m on four legs). A coil of radius R other than 0.45 m writes the shape's own `stiffness_n_per_m` = 60 · (0.45 / R)³: **480 N/m** for the 0.45 m fence-top coil, 44 N/m for a 1.0 m coil (physics reading §4);
  - **the fiber ignores `razor_wire` shapes** (the coil volume, a sagged coil, a trampled mat) and reads only the turn records; the airframe feels the shapes, and the props, legs and fiber meet the records;
  - the turns themselves carry no rigid collision; they are the snag records below (props, legs and the fiber);
  - pickets: an angle as two thin boxes (its two legs, exact), a U picket as three, rebar or a stake as a capsule of its own radius; material `steel` (`timber` for a stake);
  - D: the sagged volume as two capsules side by side fitted to the slumped ellipse within ±5 cm (30 / 15 / 4 mm at 0.7 / 0.8 / 0.9 D, the turn model, item 8); a trampled span is a low box (the mat) of the same material; a breach has no volume.
- **Material ids:** `razor_wire` (accepted, physics reading §4) for the coil volume and every turn record; `steel` for pickets, clips and tie wire, and for their records.
- **Gaps:** none. A coil is never passable: seen from the side, loops of one lean stand 2p cos λ = 0.43 m apart (0.19 m on the 0.45 m coil), under the drone's 0.574 m, a loop of the other lean crosses every such opening, and the far side of every loop closes the path again (derived: the turn model, item 5). The drone passes **over** a run (0.90 m single, 1.62 m double-stacked, the fence top plus 0.45 m) or through a **D breach**, which is open ground and written as no gap polygon: its clear width is the breach length less the curled ends (0.2–0.5 m each), so a 1–4 m breach clears 0–3.6 m (derived).
- **Wind volumes:** the coil volume of each span (the same two capsules), porosity **0.95** (I), accepted by the physics reading (§3). Derivation: one turn holds π D of tape and the end-on section is an ellipse of area π D² cos λ / 4, so the one-sided tape area per coil volume is `a = 4 w / (D p cos λ)` with `w` the 8 mm projected width and `p` the pitch; across the coil the mean chord is π D cos λ / 4, so the lean cancels and `β = exp(−(π / 2) · w / p)` averaged over the silhouette and `exp(−2 w / p)` straight through the axis, both independent of D and λ:

  | Case | Pitch | a (m²/m³) | β, silhouette mean | β, through the axis |
  |---|---|---|---|---|
  | 0.90 m coil, stretched (I) | 0.268 m | 0.165 | **0.95** | 0.94 |
  | 0.45 m fence-top coil (I) | 0.196 m | 0.739 | 0.94 | 0.92 |
  | 0.90 m coil, bunched by a cut (D) | 0.10 m | 0.365 | 0.88 | 0.85 |
  | trampled mat 0.15 m high, 1.41 m wide (D) | 0.268 m | 0.398 | 0.75 along the 1.41 m path across it | 0.97 through its height |

  The range with w 6–10 mm and p 0.20–0.30 m is 0.92–0.97; with the loops leaning ±36.6° the true mean projection could sit anywhere in G 0.3–0.7, the same band (the physics reading's orientation check gives 0.96). A coil barely shades the wind: it is not a windbreak.
- **Snag hazards (the core of this row):**

  | Record | Value | Label |
  |---|---|---|
  | Kind | one `edge` per turn | design decision 3 |
  | Polyline | the turn's core centreline: the flat loop of the turn model, leaning ±36.6° alternately (±60.6° on the 0.45 m coil); 24 points a turn, every 15° from the loop's in-plane horizontal, so the four clip points (60°, 120°, 240°, 300°) are vertices; closed, written as 25 points with the first repeated (schema 1 has no closed flag); at least 24 points a turn for the 0.90 m coil and 17 for the 0.45 m (default 24 for both) | derived: the chord's sagitta R(1 − cos(π / n)) stays within half the radius, n ≥ π / acos(1 − r / 2R) |
  | `radius_m` | **0.008**: the barbs' reach from the core, half the tape's width across the barbs (0.0075–0.0105 over the profiles). Props, legs and the fiber touch the record within it, and it inflates the record's bounds | published (half of 15–21 mm) |
  | `edge_radius_m` (optional field) | **0.00025**: the radius the fiber bends over at the record, half the blade's thickness (0.00025–0.0003); the generator writes `tape_thickness_m` / 2 | derived from the published 0.5–0.6 mm; field ruled by the physics reading §1 |
  | `barb_pitch_m` (optional field, `edge` only) | **0.035** (0.034–0.101): a fiber pressed onto the record is locked along it to within half a pitch of where it touched, and can still slip along its own length | published; field ruled by the physics reading §1 |
  | `material` | `razor_wire` on every turn record | physics reading §1 |
  | Barb cluster length | 0.022 m (0.022–0.065): not recorded; the sim treats the whole tape as blade (conservative) | published; physics reading §1 |
  | Records per metre of run | **3.7** (0.90 m coil), 5.1 (0.45 m), 7.5 double-stacked | derived: 56 turns per 15 m or 11 m of yield |
  | Points per metre of run | about 90 (single, 0.90 m) | derived: 3.7 × 24 |
  | Clips | 2 to each neighbour at the loops' crossings (the turn model, item 4): 2-point `edge` records 25 mm long centred on the crossing, radius 1 mm, `steel`; 7.5 per metre of run (0.90 m coil) | positions derived; size assumed |
  | Pickets | the picket top and the tie-wire tails: `hook`, radius 1–2.5 mm, `steel` | assumed |

  The default turn record reads `{"kind": "edge", "points": [25 points], "radius_m": 0.008, "edge_radius_m": 0.00025, "barb_pitch_m": 0.035, "material": "razor_wire"}` (physics reading §1). Props and legs strike every turn they touch (a logged prop strike per turn; the strike law takes the thin-wire class from `razor_wire`'s 2.5 mm core, never from the 8 mm reach); the fiber laid over a run touches 3–4 turns per metre of run it crosses and every one of them is a snag.

  **Cut or hold (ruled by the physics reading, §2):** a barb holds the line while its tension stays below T_c = EI·σ_f² / (4 (E·r)² sin²(θ/4)) and is a probable cut above it, where θ is the line's turn at the barb and σ_f the flight's drawn fiber strength. At a 90° turn T_c is **0.034–1.0 N** (σ_f 0.69–3.8 GPa), against the 0.03–0.5 N a line carries at a barb in flight: a line pulled round a barb (a turn, a reverse, a drag along the run) is a probable cut, and a line draped over a run (15–45° a turn) holds on all but the weakest draws. A barb also abrades the coating wherever the line rubs. The core never moves under the drone (it breaks at about 7.4 kN, derived at an assumed 1500 MPa).
- **Landable surfaces:** none. A drone set down on a coil sinks about **0.20 m** on four legs (25 mm on the fence-top coil) and is held by the barbs: a barb hooked into a printed leg lets go at about 50 N, so a leg on two barbs stays held at full throttle (physics reading §2.5).

### Placement rules (for the world)
- **Along trench lines** (E01–E04, placed by the layout) **and along field and belt edges**: a run beside the line, following it (assumed).
- **Across road shoulders and verges**, from the road edge to the ditch or the belt edge, often meeting the end of a dragon's-teeth field (F02) or of a checkpoint kit (F04) (assumed).
- **On fences and wall tops** of compounds in the town: the farm complex, a utility or industrial yard, the edge of a garage block (variant 3) (assumed).
- **Lengths:** a run is whole coils joined end to end: 15 m (11 m on fence tops) per coil, tens to a few hundred metres per run (assumed).
- **Mix:** single runs most often; double-stacked across roads at settlement edges and along trench lines; fence tops in the town. The D state where the run crosses a track, a field road or a shelled area: breaches and trampled spans where vehicles and people cross (assumed).
- **Orientation:** along the trench line, edge or fence it follows, or across the verge; the pickets on one side of the coil or inside it (`picket_side`), set per run.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `layout` | enum | single, double_stacked, fence_top | single | Variant: tiers, picket length, mounting |
| `rows`, `tiers` | int, int | 1–3, 1–2 | from `layout` (1, 1) | Coils side by side and stacked (published 2–3 rows by 1–2 tiers) |
| `profile` | enum | bto22, nato22, cbt65 | nato22 | Preset of the four tape values below |
| `tape_width_m` | m | 0.015–0.021 | 0.016 | Width across the barbs; every turn record's `radius_m` (the barbs' reach) is half |
| `barb_length_m` | m | 0.022–0.065 | 0.022 | Length of a barb cluster along the tape |
| `barb_pitch_m` | m | 0.034–0.101 | 0.035 | Cluster spacing; written into every turn record as `barb_pitch_m` |
| `tape_thickness_m` | m | 0.0005–0.0006 | 0.0005 | Blade thickness; its half is every turn record's `edge_radius_m` |
| `core_diameter_m` | m | 0.0025 | 0.0025 | Core wire (fixed by the published profiles) |
| `coil_diameter_m` | m | 0.45–1.0 | 0.90 (fence top 0.45) | Coil size: each turn's loop diameter and tape (π D); sets the yield default (published: 11 m below 0.72 m, 15 m from 0.72 m; the 0.72 m coil also comes at 11 m) and with it `pitch_m`'s, and the volume's `stiffness_n_per_m` = 60 · (0.45 / R)³ when R ≠ 0.45 m |
| `turns` | int | 31–56 | 56 | Turns per coil |
| `pitch_m` | m | 0.10–0.40, at most 0.47 · `coil_diameter_m` | yield / turns (0.268; 0.196) | Stretched turn pitch; sets the lean asin(2p / D) (36.6°; 60.6°), the end-on width √(D² − 4p²), the clip points, snag density and porosity (the turn model) |
| `run_length_m` | m | 2–500 | from placement | Run length, cut into whole coils plus a part coil |
| `nest_fraction` | 0–0.3 | — | 0.1 | How far an upper coil sinks into the lower |
| `picket_type` | enum | u_steel, angle, rebar, timber, staples | angle | Section and look (the U and angle sections in the Dimensions table) |
| `picket_section_m` | m | 0.016–0.10 | 0.05 | Angle leg (its thickness a tenth of it), the U picket's width (0.076; depth half of it, 3 mm wall), bar or stake diameter |
| `picket_length_m` | m | 0.6–2.44 | 1.52 (2.44 double-stacked) | Picket length |
| `picket_embed_m` | m | 0.3–0.7 | 0.5 | Driven depth |
| `picket_spacing_m` | m | 3.0–5.0 | 3.8 | Tie points along the run |
| `picket_side` | enum | left, right, inside, alternate | left | Where the pickets stand against the coil, seen along the run's drawing direction (the order of the layout's polyline): beside it on the left or on the right, inside the loops on the axis, or alternating left and right |
| `anchor_pickets` | bool | — | true | 0.81 m pickets holding each run end |
| `support` | enum | fence_posts, top_wire, wall_top | fence_posts | Fence-top mounting |
| `support_width_m` | m | 0.02–0.6 | 0.05 (fence_posts, top_wire); 0.25 (wall_top) | Width of the fence top or wall coping the coil sits on (a top rail to a coping, assumed); the coil sits centred on it, and a festoon hangs beside its face, its axis half this width plus the coil's end-on half-width out from the centre line |
| `tie_spacing_m` | m | 1.0–3.0 | post spacing | Fence-top tie points |
| `finish` | enum | galvanised_new, galvanised_dull, rusted | galvanised_dull | Material preset |
| `damage` | 0–1 | — | 0 (I) / 0.6 (D) | I below 0.25, D above; drives the items marked "from `damage`" ("How this sheet was made") |
| `cuts` | int | 0–3 per coil | from `damage` (I 0, D 1) | Breaches: the coil springs back to the next tie and bunches to a 0.05–0.10 m pitch, cut ends curl |
| `sag` | 0–0.3 | — | from `damage` (I 0, D 0.1–0.3) | Height lost where ties broke, as a fraction of D; each loop widens in its plane to keep its tape (the turn model, item 6) |
| `trampled_count` | int | 0–3 per coil | from `damage` (I 0, D 1) | Flattened mats 0.10–0.25 m high, up to π D / 2 wide, at positions along the coil drawn by `seed` |
| `trampled_length_m` | m | 1–5 | drawn by `seed` in 1–5 | Length of each mat along the run |
| `picket_lean_deg` | deg | 0–20 | from `damage` (I 0, D 5–20, drawn per picket) | Each picket leans toward the pull; a picket inside a trampled mat lies flat under it |
| `litter_caught` | 0–1 | — | 0.2 | Dry weeds, film and bags in the loops (visual only) |
| `points_per_turn` | int | 18–48, a multiple of 6 | 24 | Polyline resolution of each turn's snag record; a multiple of 6 keeps the four clip points as vertices |
| `seed` | int | — | — | Every random choice |

Outputs: **every turn at every LOD** (the buildability review: dropping turns would thin the haze the feed shows, and thickening the rest is forbidden, "How thin parts read on the feed"). LOD0 with every turn as a tube along the core and the blade clusters as real flat geometry; LOD1 each turn as a 4-sided tube of the tape's projected width on 12 points a turn, without clusters, clips or tie wire (a 16 mm tape drops under two render pixels at about 7.5 m, derived); LOD2 a 3-sided tube on 8 points a turn (from about 16 m); LOD3 a 3-sided tube on 6 points a turn (from about 28 m) to the cull (derived in the buildability review); collision, wind volume and the snag records as above.

### Open points
- **Cut or hold: closed** by the physics reading (§2): a barb holds below T_c and is a probable cut above it (Physics role). The sim's per-step barb contact (lock, capstan, bend, abrasion) is the phase-7 tether change's.
- **Which radius the fiber bends over: closed** by the physics reading (§1): `radius_m` is the barbs' reach, the new optional `edge_radius_m` the blade the fiber bends over, `barb_pitch_m` the lock. The asset change carries both fields through `meta.validate`, the importer and `SnagFeature` (they are lost in the prefab today).
- **`razor_wire`: closed**, accepted as a new id at 60 N/m per touching part (physics reading §4; the row at the end of the sheet). The asset change adds the row, the per-shape `stiffness_n_per_m` override and an envelope audit for a compliant volume.
- **Snag budget:** ruled by the physics reading (§7): the records never go through a per-step scan but through the binned 2 m cache, 2.4–4.8 µs a step on the densest default stack. Built in phase 3 by the world-query spec (world-builder, physics sign-off): a spatial index over shapes, records and gaps, a record query feeding the cache, a cap of 2,048 record segments per 2 m cube, and a `wire_runs` bench.
- **Where each run lies** is the layout's (phase 3): this sheet gives only what a run stands on and beside, how it is laid out and how long. Not a pilot question.
- **Clips, a look at G2 (D-024):** the flat-loop model gives 4 clip points a turn, a simplification of the published 3–7 staggered clips at which the real loops kink (Dimensions, Clips). Whether the coil reads right with 4 a turn is a G2 look point; if it does not, the asset change adds a `clips_per_pair` parameter (asset-artist).

## F02 Dragon's teeth (зуби дракона)

**Seen in:** R: research only.
**Research sources:**
- height, the truncated reinforced-concrete pyramid, staggered rows: https://en.wikipedia.org/wiki/Dragon%27s_teeth_(fortification)
- rows, row spacing and spacing in a row (a public-domain US War Department bulletin, Tactical and Technical Trends no. 35, on concrete teeth and steel obstacles): https://lonesentry.com/articles/ttt07/coastal-antitank-obstacles.html
- the proportions of the class: a truncated pyramid 3 ft square by 2 ft high, and a triangular pyramid with 6 ft sides and 5 ft high (a national monument-type thesaurus, the two terms' scope notes): https://heritagedata.org/live/schemes/eh_tmt2/concepts/160305.html , https://heritagedata.org/live/schemes/eh_tmt2/concepts/160309.html
- rows in a set order, gaps left as dog-leg exits (encyclopedia): https://ru.wikipedia.org/wiki/Надолбы
- the density of heavy concrete, 2400 kg/m³ (the note to the mass table of ГОСТ 13579-78): https://dnaop.com/html/47138/doc-%D0%93%D0%9E%D0%A1%D0%A2_13579-78_

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Height | 0.8–1.2 m | **1.0 m** | published: 0.9–1.2 m (encyclopedia); 2 ft 8 in–3 ft, 0.81–0.91 m (bulletin) |
| Shape | a truncated square pyramid (a triangular one exists) | square frustum | published (encyclopedia, thesaurus) |
| Base | 0.9–1.5 m square | **1.2 m** | derived: the published proportions give base / height 1.2 (the triangular pyramid, 6 ft by 5 ft) to 1.5 (the frustum, 3 ft by 2 ft); the default takes 1.2 × the height |
| Top | 0.2–0.4 m square | 0.3 m | assumed |
| Face angle | 57–68° from horizontal | 65.8° | derived: atan(h / ((b − t) / 2)), over base 1.2–1.5 × the height and a 0.2–0.4 m top |
| Volume and mass | 0.35–1.16 m³, 0.84–2.8 t | 0.630 m³, **1.51 t** | derived: V = h / 3 · (b² + b·t + t²), at 2400 kg/m³ (published) |
| Centre of mass | — | 0.32 m above the base | derived (frustum centroid) |
| Reinforcement | 4–8 bars Ø 10–12 mm with links Ø 6 mm at 0.2–0.3 m, 30 mm cover | 4 corner bars Ø 12 mm | assumed (published only as "reinforced") |
| Lifting loop | a bar loop Ø 10–12 mm, 0.08–0.15 m proud of the top; or none | Ø 12 mm, 0.10 m | assumed (precast practice) |
| Rows | 3–5, staggered | **4** | published: three or four staggered rows (bulletin); a first row, often a second and third, sometimes a fourth (encyclopedia); 5 from the asset list |
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
  - **buried:** part of a row half-covered by a pushed earth ramp (published: earth pushed over the teeth, encyclopedia).

### Damage pattern
Concrete spalls and cracks; it does not splinter. Chips and corner pieces 0.05–0.3 m lie round the foot; a crater in the row leaves a tooth tilted on its lip with its sand bed exposed. Bars show only at deep spalls (assumed light reinforcement).

### Physics role
- **Collision:** **one `convex` hull per tooth**: the frustum's 8 corners (4–6 points for the triangular pyramid), material `concrete`, exact in every state (a toppled or shifted tooth is the same hull moved; a spall cut on a plane keeps it convex) (physics reading §6). The loop and exposed bars are capsules of their own radius, `steel`. **Not landable** (a 0.3 m top).
  - **Why not boxes** (derived; physics reading §6, buildability review): stacked boxes stand off the sloping face by 0.205 / n m at a face and 0.268 / n m at an arris corner, where two faces meet, so a tooth would need 14 boxes for AST-05's ±2 cm round the openings and 6 for ±5 cm; they leave ledges a foot can rest on (32 mm wide at 14 boxes, 90 mm at 5) where the real face slopes at 65.8°, they cannot follow a spall deeper than 2 cm, and they cannot build the triangular variant.
  - The `convex` kind is new to the pipeline: the asset change adds it (`meta.SHAPES`, validator, audit, importer) and the phase-3 world-query spec its contact and raycast (open points).
- **Gaps** (default field, teeth square to the rows; the field's placement recipe writes each from the actual placement: in the world the world-builder's compiler in phase 3, in a gallery field module its own metadata):

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
- **Across roads at settlement edges** and **across the gaps between tree belts**: a field of 3–5 staggered rows running across the road or the gap, its ends meeting a belt, a ditch, a trench line or a wire run (F01) (assumed).
- **Along trench lines and earth banks**: a field beside the trench or the bank, its rows parallel to it (assumed).
- **On roads**, a lane left open as a dog-leg exit (published: gaps left as dog-leg exits), closable with hedgehogs (F03) or blocks (F04).
- **Length:** 20–300 m per field (assumed). Wire (F01) often runs beside or among the rows (the encyclopedia's barbed wire with teeth).
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
| `damage` | 0–1 | — | 0 / 0.5 | I below 0.25, D above; drives the items marked "from `damage`" ("How this sheet was made") |
| `spalls` | int | 0–6 per tooth | from `damage` (I 0, D 2–4, assumed) | Chipped arrises and corners, 0.05–0.2 m, each a planar cut so the hull stays convex |
| `toppled_fraction` | 0–0.3 | — | from `damage` (I 0, D 0.05–0.15) | Teeth lying on a face or tilted on a crater lip |
| `shifted_fraction` | 0–0.3 | — | from `damage` (I 0, D 0.05–0.15, assumed) | Teeth rotated or moved off the grid |
| `buried_span_m` | m | 0–20 | 0 | A pushed earth ramp over part of the rows |
| `weathering` | 0–1 | — | 0.6 | Algae, lichen, splash dirt, rust bleed |
| `seed` | int | — | — | Every random choice |

Outputs: one tooth mesh per variant and state, instanced by the field generator with the gaps written per pair of neighbours; LOD0 with chips and the loop; LOD1 the plain frustum; LOD2 a box impostor row beyond 300 m (assumed).

### Open points
- **The `convex` collision kind:** ruled by the physics reading (§6). The asset change adds the kind (4–16 points, every point a hull vertex within 1 mm, a volume above zero) to `meta.SHAPES`, the validator, the audit and the importer; the phase-3 world-query spec (world-builder, physics sign-off) adds its `StaticContacts` and `Raycast`, the golden cases and a `teeth_field` bench case.
- **The gaps between teeth** lie between separately placed objects: the world-builder's compiler writes them in phase 3 from the placed geometry (the buildability review's placement recipe).
- **The base proportion** (1.2 × the height) is derived from two published classes; the pilot's look at G2 settles whether today's teeth read as squatter or slimmer (a look, not a number: D-024).

## F03 Anti-tank hedgehog (протитанковий їжак)

**Seen in:** R: research only.
**Research sources:**
- the form, member sections, joints, the octahedron geometry and its height relation, where it stands best (encyclopedia): https://ru.wikipedia.org/wiki/Противотанковый_ёж
- member lengths and masses, the industrial angle section, feet, wire notches, the bolted third member, rail in recent use (encyclopedia): https://en.wikipedia.org/wiki/Czech_hedgehog
- the Р50 rail section (a seller's datasheet; the profile standard is ГОСТ 7174-75): https://www.railway.supply/uk/rejki-r50-r60-r65-r75-prodazh-ta-postachannya-v-ukra%d1%97ni-ta-%d1%94vropi/ , https://prg.kz/m/amp/document/30983366
- the Р50 and Р65 sections in full: ГОСТ 7174-75 (Р50: area, mass and the head, web and foot shares): https://dnaop.com/html/64676/doc-%D0%93%D0%9E%D0%A1%D0%A2_7174-75 ; ГОСТ 8161-75 (Р65: the same): https://dnaop.com/html/70510/doc-%D0%93%D0%9E%D0%A1%D0%A2%0A8161%E2%80%9475 ; the cross-section table of ГОСТ Р 51685 (height, web height and thickness, head and foot width, foot edge): https://www.baurum.ru/_library/?cat=rails&id=245 ; a rail maker's tables (head height): https://rails.arcelormittal.com/types-rails/transport-rails/russian-standards/rail-r50 , https://rails.arcelormittal.com/types-rails/transport-rails/russian-standards/rail-r65
- the light sections: equal angles to EN 10056-1 (a reference table): https://roymech.org/Useful_Tables/Sections/AngleA_dim_prop.html ; channels to ГОСТ 8240-97: https://ru.wikisource.org/wiki/%D0%93%D0%9E%D0%A1%D0%A2_8240%E2%80%9497

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Member length | 1.2–2.1 m | **1.8 m**: a **member** is one of the three straight bars through the node; a **half-arm** is a member's half from the node to an end (0.9 m) | published: 1.8 m for the original pattern and 2.1 m later (en encyclopedia); up to 2 m, and about 1.2 m in some manuals (ru encyclopedia) |
| Height, standing | 0.69–1.21 m | **1.04 m** | derived: three members at right angles through one point, standing on three ends (an octahedron on a face), H = L / √3; the ru encyclopedia gives the same relation (h = a√6 / 3 with L = a√2) and about 1 m, the height of a tank's front plate |
| Across the feet (corners) | 0.98–1.72 m | **1.47 m** | derived: the six ends project onto a regular hexagon of circumradius (L / 2) · √(2/3) = 0.735 m |
| Across flats | 0.85–1.49 m | 1.27 m | derived |
| Node height | 0.35–0.61 m | 0.52 m | derived (H / 2) |
| Spread of the feet | the three feet form an equilateral triangle of side L / √2 | 1.27 m | derived |
| Member: rail | Р50; Р65 for heavier | **Р50** | rail as a member published (both encyclopedias, which name no rail type); the types assumed (two common heavy types; their sections below are published) |
| Р50 section (`rail_r50`) | — | **152 mm high; head 72 mm wide and 42 mm deep; web 16 mm thick and 83 mm high; foot 132 mm wide, 10.5 mm thick at the edge and 27 mm at the root; 65.99 cm², 51.8 kg/m** (head 38 %, web 24 %, foot 37 % of the area) | published: height, web, head and foot widths and the foot edge (ГОСТ Р 51685 table), the head depth (the rail maker's table), area, mass and shares (ГОСТ 7174-75; the seller's datasheet lists 50.4 kg/m); the foot root derived (152 − 42 − 83) |
| Р65 section (`rail_r65`) | — | 180 mm high; head 75 mm wide and 45 mm deep; web 18 mm thick and 105 mm high; foot 150 mm wide, 11.2 mm at the edge and 30 mm at the root; 82.65 cm², 64.9 kg/m | published (the same tables; ГОСТ 8161-75 for the area and mass); the foot root derived (180 − 45 − 105) |
| Rail outline drawn by the generator | — | the head's underside slopes 1:4 down to the web, its crown is an arc of R 300 mm and its top corners are rounded R 13 mm (Р50) or R 15 mm (Р65); the foot's top runs straight from the root to the edge; no fillets. It comes to **66.4 cm²** for Р50 (+0.6 % on the standard's 65.99) and **81.7 cm²** for Р65 (−1.2 % on 82.65); the generator's `check` holds its outline within ±3 % of the standard's area | the slope, crown and corner radii assumed (typical of the class); the areas derived |
| Member: angle or channel | L 140 × 140 × 13 mm in the industrial pattern; I-beams No. 25–40 in older ones; lighter angle or channel 75–100 mm in improvised ones | variant 2: L 140 × 140 × 13 (`angle_140`). Variant 3: L 100 × 100 × 8 (`angle_100`: root radius 12 mm, toe radius 6 mm, 15.5 cm², 12.2 kg/m; 10 and 12 mm walls are rolled too) or channel No. 10 (`channel_100`: 100 mm deep, flanges 46 mm wide and 7.6 mm thick, web 4.5 mm, root radius 7 mm, 10.9 cm², 8.59 kg/m) | published (en and ru encyclopedias; the EN 10056-1 table for the 100 mm angle; ГОСТ 8240-97 for the channel); the choice of these two light sizes for variant 3 assumed |
| Joint | gusset plates riveted or bolted, the third member bolted with M20 bolts; welded with or without gussets when improvised | welded, three gussets 0.25 × 0.25 × 0.012 m | published forms; gusset size assumed |
| Feet | square plates on the member ends against sinking | 0.15 × 0.15 × 0.010 m | published (en encyclopedia); size assumed |
| Wire holes or notches | for lashing barbed wire | 2 per half-arm, 0.15 m and 0.35 m from its end: 4 a member, 12 a hedgehog | published (both encyclopedias); position assumed |
| Mass | 0.15–0.30 t | rail: about **0.29 t**; angle: 0.20 t | derived: 3 × 1.8 m × 51.8 kg/m (ГОСТ 7174-75) = 280 kg plus gussets and feet; published: 198 kg for the 1.8 m angle pattern (en encyclopedia) |
| Pair spacing (centres) | 1.8–3.0 m | 2.0 m | assumed |

### Construction and materials
- **The form:** three straight members of equal length crossing at their middles, each at right angles to the other two, like the axes of a cube; the six ends make an octahedron, so it stands on any three ends with the other three up, and stands the same however it is rolled (published). A vehicle that tries to push it rolls it under its belly and is lifted off its wheels or tracks (published), which is why it is not anchored.
- **Default (variant 1), welded rail:** three 1.8 m pieces of Р50 rail. Rails cannot all pass through one point, so two are notched half-depth where they cross and welded, the third is cut in two and its halves welded to the node, and three triangular gussets close the joint (assumed fabrication). Square feet welded to the ends, holes for wire, weld beads ground or left rough.
- **Variant 2, the industrial angle pattern:** three L 140 × 140 × 13 angles with riveted gusset plates at the node, the third member bolted on (published).
- **Variant 3, improvised light:** angle 75–100 mm or channel, welded without gussets, the members welded side by side rather than through one point (assumed).
- **Setting:** unanchored, on asphalt or hard ground (published: asphalt is best, concrete is unsuitable because the hedgehog slides on it); a pair may be chained or cabled together (published).
- **Materials:** Poly Haven `rusty_metal` and `rust_coarse_01` (candidates, CC0: https://polyhaven.com/a/rusty_metal , https://polyhaven.com/a/rust_coarse_01 ), with derived masks for the worn running band of a used rail head, the weld beads and rust streaks below the node; a painted variant uses `rusty_painted_metal` (https://polyhaven.com/a/rusty_painted_metal ).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Rail, rusted | #5e4030 | — (no letter) | assumed |
| Heavy flaking rust (D) | #7a4f33 | — | assumed |
| Angle with mill scale, newer | #3e3c3a | — | assumed |
| Worn running band of a used rail head | #8a8580 | — | assumed |
| Weld bead | #6a6560 | — | assumed |

### Variants and states
- **Variants:** 1 welded rail (default); 2 industrial angle with riveted gussets; 3 improvised light angle or channel. Each a single or one of a pair (with or without a chain).
- **I:** upright on three feet sunk 0–0.05 m, even rust.
- **D (bent, rusted):** one or two half-arms bent 5–30° at 0.3–0.6 m from the node by a vehicle; a cracked weld with a half-arm sagging at the node; rolled onto other ends (the height unchanged, published), pushed to the verge and tilted 10–30° on the ditch slope, or sunk 0.1–0.3 m into mud on soft ground; heavy flaking rust.

### Damage pattern
Steel bends rather than breaks; the welds crack before the members; the hedgehog rolls before it fails (published). Nothing scatters round it: rust flakes, a loose chain, tyre marks where it was shoved (assumed).

### Physics role
- **Collision:** each member as boxes following its section (a rail as head, web and foot, three boxes from the dimensioned section; an angle as its two legs, two boxes; a channel as its web and two flanges, three boxes: exact, inside ±2 cm), each gusset and foot a thin box; material `steel`. A bent half-arm is two boxed segments meeting at the bend.
- **Material id:** `steel`.
- **Gaps** (default 1.8 m members of Р50, about 0.14 m across the section):
  - **the three lower windows:** each the triangle between two lower half-arms and the ground, in a plane leaning 35.3° from vertical (its normal 54.7° from vertical, derived); a right isosceles triangle with 0.9 m legs, largest inscribed circle 0.53 m between the member centrelines and **0.44–0.45 m** clear of the rail (derived: (L/2 − 2d) · (2 − √2) with d the half-section 0.066–0.076 m): **not passable**;
  - **under the node:** 0.44 m clear below the node's members, closed round by the three lower half-arms: not passable (the drone's envelope is 0.55 m);
  - **the three upper notches:** open-topped 90° vees between two upper half-arms; not gaps; the drone clears a hedgehog only over its top (1.04 m plus about 0.08 m of section);
  - **between a pair at 2.0 m centres:** the throat polygon lies in the vertical plane through both centres, its sides each hedgehog's silhouette projected onto that plane along its normal (the houses sheet's rule for a gap through a thick wall), and its widths are measured along the line of centres. It is waisted: narrowest at the ground and at the tips' height, widest at the node height. Worst case (a foot or tip of each pointing at the other) to best case (the ends turned 60° away):

    | Prop-plane height | Clear width, worst | Clear width, best | Passes |
    |---|---|---|---|
    | 0 (ground) | 0.40 m | 1.13 m | worst: no |
    | 0.2 m | 0.97 m | 1.42 m | any yaw |
    | 0.52 m (node) | 1.87 m | 1.87 m | any yaw |
    | 0.8 m | 1.08 m | 1.47 m | any yaw |
    | 1.04 m (tips) | 0.40 m | 1.13 m | worst: no |

    (derived: a lower half-arm reaches 0.735 · (1 − z / 0.52) m from its centre at height z, an upper half-arm 0.735 · (z − 0.52) / 0.52 m, less 0.13 m of section.) The worst-case throat's largest inscribed circle is **1.04 m**, centred at the node height and limited by the ground (derived). So the drone passes between a pair at any yaw with its prop plane between 0.11 and 0.93 m even in the worst orientation (the 0.24 m legs fit the narrower gap below them); at 1.8 m centres the worst-case gap at the ground and tips drops to 0.20 m and the band narrows to 0.18–0.86 m (derived: clear width ≥ 0.706 m).
- **Wind volumes:** the envelope (1.47 m across, 1.04 m high) as one volume, porosity **0.6** (derived: three 1.8 m members 0.14 m across, at a mean projection of π / 4, present about 0.59 m² against a 1.53 m² silhouette: 0.61 open).
- **Snag hazards:** every member end (`edge` round the cut end's perimeter, steel 1 mm); the rail foot's flange tips along every member (`edge`, 1 mm) and the head's corners (`edge`, about 13 mm, the corner radius); the gusset edges (`edge`, 1 mm); the foot plates (`edge`, 1 mm); a chain or lashed barbed wire (`edge`, its own radius). An angle's leg tips or a channel's flange tips along each member (`edge`, 1 mm).
- **Landable surfaces:** none: members 0.07–0.15 m wide, every one at **54.7° from vertical** (35.3° from horizontal, derived: a cube's axes against its body diagonal), steeper than the 19.3° on which a foot holds on steel (atan 0.35), so a foot slides off (physics reading §8, item 6).

### Placement rules (for the world)
- "A couple" in the world (the pilot, D-035): 2–6 in all (assumed), as **singles and pairs at road approaches and at the checkpoint** (asset list): on the asphalt or a hard verge (published), narrowing the carriageway to a dog-leg, closing the open lane of a dragon's-teeth field (F02), or standing in the spare width of the checkpoint chicane (F04).
- Pairs 1.8–3.0 m apart, sometimes chained; wire (F01) often lashed between them and the verge (assumed).
- Orientation random (any stance looks the same); D ones pushed onto the verge or into the ditch.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `member` | enum | rail_r50, rail_r65, angle_140, angle_100, channel_100 | rail_r50 | Section (each dimensioned in the Dimensions table), mass, look |
| `member_length_m` | m | 1.2–2.1 | 1.8 | Length L of each member; a half-arm is L / 2. Size: height L / √3, across the feet 0.816 L |
| `node_offset_m` | m | 0–0.10 | 0 | Members welded side by side instead of through one point (variant 3) |
| `joint` | enum | gusset_riveted, gusset_welded, welded_bare, bolted_third | gusset_welded | Node construction |
| `gusset_m` | m | 0.15–0.35 | 0.25 | Gusset leg |
| `feet` | bool | — | true | Square foot plates |
| `foot_plate_m` | m | 0.10–0.25 | 0.15 | Foot plate side |
| `wire_holes` | int | 0–3 per half-arm | 2 | Holes or notches for lashing wire on each half-arm (2: 4 a member, 12 a hedgehog) |
| `stance` | int | 0–7 | from seed | Which three ends it stands on |
| `yaw_deg` | deg | 0–120 | from seed | Rotation about the vertical |
| `pair` | bool | — | false | A second hedgehog beside it |
| `pair_spacing_m` | m | 1.8–3.0 | 2.0 | Centres of a pair; sets the pair's gap |
| `chain` | bool | — | false | A chain or cable between a pair |
| `damage` | 0–1 | — | 0 / 0.5 | I below 0.25, D above; drives the items marked "from `damage`" ("How this sheet was made") |
| `bent_half_arms` | int | 0–2 | from `damage` (I 0, D 1–2) | Half-arms bent by a vehicle |
| `bend_deg` | deg | 5–30 | 15 | Bend angle |
| `bend_at_m` | m | 0.3–0.6 | 0.45 | Bend position along the half-arm, from the node |
| `cracked_weld` | bool | — | false | One half-arm sagging at the node |
| `sink_m` | m | 0–0.3 | 0.02 | Feet sunk into soft ground |
| `tilt_deg` | deg | 0–30 | 0 | Tilt on a verge or ditch slope |
| `rust` | 0–1 | — | 0.6 | Rust and flaking |
| `seed` | int | — | — | Every random choice |

Outputs: LOD0 with welds, holes and feet; LOD1 the three members and the node; LOD2 three boxes; collision, gaps (the lower windows, and the pair's throat when paired), wind and snag as above.

### Open points
- Which member section reads as today's at G2 (rail or angle): a look, not a number (D-024); the generator carries all three.

## F04 Checkpoint kit (concrete road blocks, sandbag positions, a lifting barrier)

**Seen in:** R: research only.
**Research sources:**
- the solid concrete blocks ФБС, their sizes, volumes, masses, loops and tolerances (ГОСТ 13579-78): https://dnaop.com/html/47138/doc-%D0%93%D0%9E%D0%A1%D0%A2_13579-78_
- sandbag sizes, materials, how walls are laid, the bag thickness proof against bullets and fragments (encyclopedia): https://en.wikipedia.org/wiki/Sandbag
- a manual arm barrier's height, arm lengths and colours (a manufacturer's datasheet): https://www.bsfg.co.uk/bsfg/product-datasheet/ddf3dcada2d4467eb5a822269b93752a
- counterweighted manual barriers, long-arm supports and stripes (a manufacturer's page and its brochure): https://www.elka.eu/en/products/access-control/manual-barriers/1614 , https://www.elka.eu/Dokumente/Prospekte%20-%20EN/Handschranken/Manual%20barriers%20Brochure.pdf
- the colour of mixed spoil used as fill: [earthworks-and-surfaces.md](earthworks-and-surfaces.md) E01

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Block ФБС 24.6.6 | 2380 × 600 × 580 mm, 0.815 m³, 1.96 t | **default** | published (ГОСТ 13579-78, table 2) |
| Other ФБС sizes | 24.5.6: 2380 × 500 × 580, 1.63 t; 24.4.6: 2380 × 400 × 580, 1.30 t; 12.6.6: 1180 × 600 × 580, 0.96 t | variants | published, same |
| Tolerances | ±13 mm in length, ±8 mm in width and height, 3 mm flatness | — | published, same |
| End grooves | one vertical groove in each end face | 0.11 × 0.10 m over the full height | derived: the full box is 0.828 m³ against the published 0.815 m³, leaving 0.013 m³, 0.0065 m³ a groove; its shape assumed |
| Reinforcement | none but the loops: 1.46–2.36 kg of steel a block | — | published (the table's steel column); so a broken ФБС shows no bars |
| Lifting loops | two loops of smooth bar; or flush in recesses 300 mm from the ends | raised, Ø 12 mm, 0.08 m proud, 0.4 m from the ends | published (the flush option and the bar class); the diameter, rise and position assumed |
| Concrete | heavy concrete, 2400 kg/m³, class B7.5–B15 | — | published, same |
| Chicane lane | 3.0–3.5 m | 3.2 m | assumed (a car 1.6–1.9 m wide must slow to weave; a 2.5 m truck only just passes) |
| Chicane lines | 2–3 lines of 1–4 blocks from alternate road edges, 6–10 m apart along the road | 3 lines of 2 blocks, 8 m apart | assumed |
| Joints between blocks in a line | 0–0.3 m | 0.1 m | assumed |
| Sandbag, empty | 0.36 × 0.66 m to 0.43 × 0.81 m | 0.36 × 0.66 m | published (encyclopedia: the common sizes) |
| Sandbag, filled | 0.45–0.50 × 0.28–0.31 × 0.10–0.13 m, 22–26 kg | **0.48 × 0.30 × 0.12 m, 24 kg** | derived: the 0.36 m lay-flat width gives a 0.72 m section perimeter, filled to t thick as a rounded slab its width is (0.72 − π t) / 2 + t = 0.29–0.30 m; the length 0.66 m less a 0.18 m tied fold (assumed); mass at 1600 kg/m³ for moist sand or soil (assumed) |
| Fill level | one half to two thirds | two thirds | published (encyclopedia, for flood bags; the same assumed here) |
| Sandbag wall thickness | 0.30–0.75 m | 0.48 m (one header) | derived from the bag; published: 20 in (0.51 m) of sand in bags is proof against armour-piercing bullets and fragments (encyclopedia) |
| Sandbag wall height | 1.0–1.6 m | 1.2 m = 10 courses | assumed; courses derived (1.2 / 0.12) |
| Position plan | U-shaped, ring, L or straight; 1.5–3.5 m inside | U, 2.5 × 2.5 m inside, open on one side | assumed |
| Entrance | 0.5–1.0 m clear | 0.7 m clear between the bulging bag faces, so 0.74–0.78 m between the wall ends' nominal faces | the clear width assumed; the nominal derived (plus a 20–40 mm bulge each side) |
| Firing ports | 0–4, 0.15–0.30 × 0.10–0.15 m | 2, 0.20 × 0.12 m | assumed |
| Bags in a position | 150–400 | about 250 | derived: 7.5 m of U wall, 3.3 headers a metre, 10 courses |
| Barrier arm | 3–8 m long | **4.0 m** | published (datasheet: 3, 4, 5, 6, 7, 8 m) |
| Barrier height | 1.067 m overall | arm underside **1.0 m** above the road | published overall height (datasheet); the underside derived (less a 60–80 mm arm) |
| Arm section | a tube or box 60–90 mm | tube Ø 76 mm | assumed |
| Post | steel, 0.10–0.15 m square, to about 1.1 m | 0.12 m, 1.1 m | assumed |
| Counterweight | weights on a short tail behind the pivot | 0.4 m tail | published (counterweights: the manufacturer's page); the length assumed |
| End rest | a fork post at the far end | 0.95 m high | published (an end rest: datasheet); the height assumed |
| Stripes | red and white (or yellow and black); from 4.0 m an arm needs a support at its far end, swinging or fixed, and from 5.5 m a stabilising rope | red and white, 0.5 m | published (datasheet; the manufacturer's page and brochure); the stripe length assumed |

### Construction and materials
- **Concrete road blocks:** ФБС foundation blocks for basement walls (ГОСТ 13579-78), the commonest heavy precast block to hand: plain heavy concrete, unreinforced but for the two lifting loops, a vertical groove in each end face, laid on the road by crane singly, end to end in lines, or two high (assumed). Arrises chipped from handling, rust bleed under the loops.
- **Sandbag positions:** woven polypropylene bags (most modern military bags) or hessian (published), filled two thirds with sand or the local soil, laid like brickwork (published), headers across the wall and each course offset half a bag (published for flood walls; the same assumed here), tamped flat. A U-shaped breastwork open on one side with two firing ports; a variant roofed with timbers and a course of bags (assumed).
- **Lifting barrier:** a manual counterweighted arm on a steel post: the pivot at about 1.0 m, a 4 m tube arm striped red and white resting in a fork post on the far side, counterweights on a short tail (published construction). An improvised variant: a steel pipe on a welded pivot, hand-painted in stripes, with a block of concrete or a sandbag as the counterweight (assumed).
- **Materials:**
  - blocks: Poly Haven `concrete_wall_003` or `rough_concrete` with a derived mask (chipped arrises, splash dirt, algae, rust bleed under the loops) (CC0: https://polyhaven.com/a/concrete_wall_003 , https://polyhaven.com/a/rough_concrete ); fresh fractures `concrete_debris` (https://polyhaven.com/a/concrete_debris ); loops `rusty_metal` (https://polyhaven.com/a/rusty_metal ).
  - bags: the houses sheet's derived woven surface, Poly Haven `rough_linen` tinted to the bag colour (https://polyhaven.com/a/rough_linen ), with a derived UV-fade and dust mask; spilled fill the earthworks sheet's spoil surfaces.
  - barrier: ambientCG `PaintedMetal006` tinted red and white and Poly Haven `rusty_painted_metal` for the worn paint (CC0: https://ambientcg.com/view?id=PaintedMetal006 , https://polyhaven.com/a/rusty_painted_metal ).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Block, weathered | #9d9a92 | — (no letter) | assumed (as the concrete of lines-roads-debris.md) |
| Block, fresh | #b4b1a8 | — | assumed |
| Green polypropylene bag, faded | #5b6a47 | — | assumed |
| White polypropylene bag, aged and dusty | #c9c4b6 | — | assumed |
| Hessian bag | #9b8460 | — | assumed |
| Spilled fill, dry mixed spoil | #8a7660 | — | derived (earthworks-and-surfaces.md E01, 10YR 5/3) |
| Barrier red, weathered | #a8352d | — | assumed |
| Barrier white, weathered | #d8d6ce | — | assumed |
| Post, galvanised | #9a9d9c | — | assumed |

### Variants and states
- **Kit pieces:** the block (ФБС 24.6.6 default; 24.5.6, 24.4.6, 12.6.6), the sandbag position (open U default; ring, L, straight; roofed), the barrier (manufactured default; improvised pipe). The arrangement is a layout recipe (Placement rules).
- **I:** blocks square to the road, clean-edged; bags whole, UV-faded; the barrier arm down on its rest.
- **D:**
  - **blocks** shoved 0.2–1 m and rotated 5–20° by vehicles, arrises chipped 0.05–0.2 m, one cracked through on a clean plain-concrete fracture (no bars);
  - **bags:** 10–30 % burst, the fill spilled below them in cones at 30–35° (assumed angle of repose), the top 2–4 courses slumped where bags burst below, sun-rotted polypropylene shredded to fibres (published: bags degrade in the sun);
  - **barrier:** the arm snapped (the outer part on the road, the stub hanging from the pivot) or torn off and lying beside the lane, the post bent 10–30°.

### Damage pattern
Blocks chip at the arrises and crack across without bars; bags burst and spill, and a wall slumps from the burst course up; the barrier arm, the weakest part, snaps first. Around the kit: spilled fill, bag shreds, concrete chips, a broken arm (assumed).

### Physics role
- **Collision:**
  - each block a box (inside the published ±13 mm), material `concrete`, **landable** top 2.38 × 0.60 m; the 0.10 m end grooves exceed AST-05's ±5 cm, so the body box stops at the groove's depth and each end adds two prong boxes either side of the groove (5 boxes a block, derived); each loop three capsules of radius 6 mm, `steel`;
  - the sandbag wall as one box per straight segment per 3–4 courses (bag faces bulge 20–40 mm, inside ±5 cm); the top course one box per bag, each box about 10 mm inside the bag's sides (a box of the full 0.29 m stadium stands 25 mm off its rounded shoulders, over the landable ±2 cm; inset 10 mm a side it holds 18 mm, derived in the buildability review); within 0.3 m of the entrance and of each firing port (both gap polygons), one box per bag in every course, following each course's bulge, to hold ±2 cm; material `sandbag`;
  - the barrier: the post a box, the arm a capsule along the tube (or a box), the counterweight a box, the end rest a capsule with a box fork; material `steel`.
- **Material ids:** `concrete`, `steel`, and `sandbag` for the bags (accepted by the physics reading, §4; the row at the end of the sheet): 900 N/m per touching part, ζ 0.9, edge 0.05 m. A 15 mm foot presses at 67 kPa, far above loose fill's bearing capacity, so it punches in and the fill behaves like tamped soil: a leg sinks 13 mm at rest and 28 mm under 25 N, 39–79 mm in a 3–6 g landing (derived there). The asset list named `fabric`; `sandbag` replaces it for its damping and its edge: `fabric`'s 5 mm hem would put a cut risk on a line hauled over the wall top, a bag's 50 mm shoulder none.
- **Gaps:**

  | Opening | Polygon | Narrowest | Passes |
  |---|---|---|---|
  | Under the barrier arm | the vertical rectangle from the road to the arm's underside, between the post and the end rest | 1.0 m high × about 3.8 m (inscribed circle 1.0 m) | **yes, any yaw**, with 0.45 m over the drone's 0.55 m envelope |
  | The chicane lane | an open channel between a block line's end and the road edge or the next line, open above | 3.2 m | **yes, any yaw** (no polygon: open channel) |
  | Joints between blocks in a line | the slot between two block ends, 0.58 m high | 0.1 m (0–0.3 m) | no |
  | Sandbag position entrance | the slot between the wall ends, open above the 1.2 m wall (with a roof, 0.7 × 1.1 m); its sides are the bulging bag faces | **0.70 m clear between the bulges** (`entrance_m`, the gap's `narrowest_m`); the wall ends' nominal faces stand 0.74–0.78 m apart | **yes, nose-first and within ±35° of square** (63 mm a side), derived from the houses sheet's yaw formula. Were 0.70 m the nominal width, the clear 0.62–0.66 m would pass only within ±9–19° (physics reading §5) |
  | Firing ports | the slot left by a missing bag | 0.20 × 0.12 m | no |
- **Wind volumes:** blocks solid (0); sandbag walls solid (0), the position's interior a sheltered cavity open on one side; the barrier none (thin, below the 2 m grid).
- **Snag hazards:** block arrises (`edge`, concrete 3 mm) and lifting loops (`edge` along the bar, 6 mm); bag ties and ears (`hook`, 2 mm cord stubs 0.05–0.10 m), torn bag fabric and fibres in D (`hook`, 1 mm); the barrier arm's ends, the counterweight frame and the pivot bracket (`edge`, steel 1 mm), the rest fork's tines (`hook`, 8 mm).
- **Landable surfaces:** block tops (2.38 × 0.60 m, flat within 3 mm, published); the sandbag wall top (0.48 m wide, crowns within ±2 cm, compliant: `sandbag`, a leg sinks 13 mm at rest); not the barrier.

### Placement rules (for the world)
- **One checkpoint on the main road into the town and into each village** (F04 is P2; assumed), on a straight 50–200 m outside the first houses or at a junction on the settlement edge.
- **The chicane:** 2–3 lines of blocks from alternate road edges, 6–10 m apart along the road, leaving one 3.0–3.5 m lane (assumed).
- **The barrier** across the lane at the middle line; **a sandbag position** on the verge beside it, next to a building corner, a tree or a belt edge where one stands; a second position on the other verge where it is wide enough (assumed).
- **With it:** 1–2 hedgehogs (F03) on the verges or in the lane's spare width; wire (F01) along both verges, from the road edge to the ditch or the belt edge (assumed).
- State follows the settlement (WLD-05): D in the frontline town, I or D at the villages' edges (assumed).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `block_type` | enum | fbs_24_6_6, fbs_24_5_6, fbs_24_4_6, fbs_12_6_6 | fbs_24_6_6 | Block size and mass (ГОСТ 13579-78) |
| `end_groove` | bool | — | true | The groove in each end face |
| `loops` | enum | raised, flush, cut | raised | Lifting loops: raised, flush in recesses, cut off |
| `loop_d_m` | m | 0.010–0.014 | 0.012 | Loop bar diameter; its snag radius is half |
| `stack` | int | 1–2 | 1 | Recipe: blocks one or two high |
| `lines` | int | 2–3 | 3 | Recipe: chicane lines |
| `blocks_per_line` | int | 1–4 | 2 | Recipe: blocks in a line |
| `lane_m` | m | 3.0–3.5 | 3.2 | Recipe: the chicane lane |
| `line_offset_m` | m | 6–10 | 8 | Recipe: distance between lines along the road |
| `joint_m` | m | 0–0.3 | 0.1 | Recipe: joints between blocks in a line |
| `bag_size` | enum | mil_36x66, pp_50x80 | mil_36x66 | Empty bag; the filled size follows by the derivation above |
| `fill_fraction` | 0.5–0.75 | — | 0.66 | Fill; sets the bag thickness and mass |
| `fill` | enum | sand, local_soil | local_soil | Fill colour and the spill surface |
| `bag_material` | enum | pp_green, pp_white, pp_olive, hessian | pp_green | Bag look |
| `plan` | enum | u_shape, ring, l_shape, straight | u_shape | Position plan |
| `inner_size_m` | m, m | 1.5–3.5 | 2.5, 2.5 | Inside dimensions |
| `wall_height_m` | m | 0.8–1.6 | 1.2 | Courses = height / bag thickness |
| `wall_thickness_m` | m | 0.30–0.75 | 0.48 | One header, or a header and a stretcher |
| `entrance_m` | m | 0.5–1.0 | 0.7 | Clear entrance width between the bulging bag faces: the gap's `narrowest_m`; the wall ends are laid twice the bulge (0.04–0.08 m) further apart |
| `firing_ports` | int | 0–4 | 2 | Ports left in the top courses |
| `port_size_m` | m, m | 0.15–0.30, 0.10–0.15 | 0.20, 0.12 | Port width and height |
| `roof` | bool | — | false | Timbers and a course of bags over the position |
| `uv_age` | 0–1 | — | 0.5 | Fading and fibre shredding |
| `barrier_kind` | enum | manufactured, improvised_pipe | manufactured | Barrier construction |
| `arm_length_m` | m | 3.0–8.0 | 4.0 | Arm length; from 4.0 m the arm needs a support at its far end, swinging or fixed, so `check` refuses `end_rest` none there; from 5.5 m the stabilising rope is added |
| `arm_height_m` | m | 0.9–1.1 | 1.0 | Underside of the arm above the road; the gap's height |
| `arm_d_m` | m | 0.06–0.09 | 0.076 | Arm tube diameter |
| `stripe_m` | m | 0.3–0.6 | 0.5 | Stripe length |
| `counterweight_tail_m` | m | 0.3–0.6 | 0.4 | Tail behind the pivot |
| `end_rest` | enum | fork, lockable, none | fork | The far-side rest |
| `arm_state` | enum | down, up, snapped, torn_off | down | Arm pose; snapped and torn_off are D |
| `post_bend_deg` | deg | 0–30 | from `damage` (I 0, D 10–30) | The post bent at its foot away from the impact; the pivot, arm and counterweight move with it, and the gap under the arm is written from the bent pose |
| `hedgehogs` | int | 0–2 | 1 | Recipe: F03 placed with the kit |
| `wire_verges` | bool | — | true | Recipe: F01 runs along the verges |
| `damage` | 0–1 | — | 0 / 0.5 | I below 0.25, D above; drives the items marked "from `damage`" ("How this sheet was made") |
| `shifted_blocks` | 0–1 | — | from `damage` (I 0, D 0.2–0.4, assumed) | Recipe: fraction of blocks shoved 0.2–1 m and rotated 5–20° |
| `cracked_blocks` | 0–0.3 | — | from `damage` (I 0, D 0.17: one block of the default six, assumed) | Recipe: fraction of blocks placed cracked. The block generator cuts such a block through on one plane across it, inside its middle third and turned up to 20° from square (drawn by `seed`), with a rough plain-concrete fracture and no bars, the halves parted 0–0.05 m; each half is its own body box with its own landable top |
| `chips` | int | 0–6 per block | from `damage` (I 0, D 1–3, assumed) | Chipped arrises and corners |
| `burst_bags` | 0–0.3 | — | from `damage` (I 0, D 0.1–0.3) | Fraction of bags burst, with spill cones and slumped courses |
| `seed` | int | — | — | Every random choice |

Rows marked **Recipe** are the chicane's placement recipe for the layout (phase 3, the world-builder's compiler), not prefab parameters: they place the blocks, hedgehogs and wire, and write the joints between blocks as gaps from the placed geometry (the lane is an open channel with no polygon). The other rows build the three prefabs.

Outputs: the block, the position and the barrier as separate prefabs with their own LODs, collision, gaps and snags; the chicane recipe writes their placement and the lane.

### Open points
- **`sandbag` against `fabric`: closed** by the physics reading (§4): `sandbag` accepted as a new id at 900 N/m per touching part (the row at the end of the sheet).
- Whether the improvised pipe barrier or the manufactured one reads as today's at G2: a look (D-024).

## Proposed physics ids: accepted by the physics reading (task 1.3)

Both ids are **accepted** as new ids with the rows below (`review-physics.md` §4). Each stiffness is the contact law's own: a part's local stiffness, applied once per touching part, not per drone. Neither is added to `physics_materials.json` here (non-goal of the change): the asset change adds both rows, the `fields.edge_radius_m` sentence for a record's own edge radius, and the optional per-shape `stiffness_n_per_m`.

| Id | Used by | μs / μk | Stiffness | ζ | Edge radius | Break | Label and derivation |
|---|---|---|---|---|---|---|---|
| `razor_wire` | F01: the coil volume, a sagged coil, a trampled mat; the material of every turn record | 0.35 / 0.28 | **60 N/m per touching part** (40–110) for the 0.90 m coil; a coil of radius R writes its shape's own `stiffness_n_per_m` = 60 · (0.45 / R)³, 480 N/m for the 0.45 m coil | 0.3 | **0.00025 m** (a turn record carries it as `edge_radius_m`) | none (the core takes about 7.4 kN) | derived; accepted. Friction as `steel` (assumed): the barbs hold through the turn records, not μ. Stiffness: one loop under a part as a ring under a diametral load, k = f · EI / ((π/4 − 2/π) R³), with the 2.5 mm core (EI 0.383 N·m² at an assumed 200 GPa) and f = 2 for the crimped tape (assumed, 2–3.8): 57 N/m for a 0.90 m coil, so a drone on four legs meets about 240 N/m and sinks 0.20 m. ζ assumed for loop-on-loop friction and the clips. Edge: half the published 0.5 mm blade. Break: 2.5 mm core at an assumed 1500 MPa. The fiber ignores `razor_wire` shapes and bends over the turn records' `edge_radius_m` |
| `sandbag` | F04: filled bags | 0.40 / 0.30 | **900 N/m per touching part** | 0.9 | **0.05 m** | none | derived; accepted. Friction as `fabric` (assumed: a polymer foot on woven polypropylene or hessian). Stiffness: a 15 mm foot at 67 kPa punches into loose fill (surface bearing capacity 2–4 kPa), so the fill acts as a loose-soil row: tamped fill at 5e6 N/m³ (assumed, between `tilled` and `meadow_sod`) under the 15 mm reference foot, k = bearing · A = 884 N/m, so a leg sinks 13 mm at rest and 28 mm at 25 N. ζ from those rows' damping at a quarter of 4.8 kg, 0.94. Edge: the rounded shoulder of a 0.12 m bag (derived), so a line over a wall top never cuts. Elastic, so no footprint stays (a deviation from the soil law, accepted). No wet adhesion on polypropylene |

## Open points across the sheet

- **Closed by the physics reading (task 1.3):** cut or hold (F01, §2); which radius the fiber bends over and the snag fields `edge_radius_m` and `barb_pitch_m` (F01, §1); `razor_wire` and `sandbag`, accepted (§4); the teeth's collision, one `convex` hull per tooth (F02, §6); the snag budget, through the binned cache (§7).
- **For the asset change and the pipeline** (asset-artist, physics sign-off): the `convex` collision kind in `meta.SHAPES`, the validator, the audit and the importer (F02, §6); the optional snag fields through `meta.validate`, the importer and `SnagFeature` (F01, §1); the two material rows, the `fields.edge_radius_m` sentence and the per-shape `stiffness_n_per_m` (§4); an envelope audit for the compliant coil volume (F01).
- **For the phase-3 world-query spec** (world-builder, physics sign-off): the `convex` contact, raycast, golden cases and `teeth_field` case (§6); a spatial index over shapes, records and gaps, a record query feeding the cache, the 2,048-segment cap per 2 m cube and the `wire_runs` bench (§7).
- **For the world-builder's compiler (phase 3):** the gaps between separately placed objects, written from the placed geometry by the placement recipes: the in-row and between-row throats of a teeth field (F02) and the joints of a block line (F04). A gallery module built as one asset carries its own; a hedgehog pair (F03) is one asset, so its throat stays in its metadata.
- **For the asset change** (asset-artist, from QA's review S5 and S8; physics sign-off on the first):
  - **F01's points per turn (S5):** the sagitta rule n ≥ π / acos(1 − r / 2R) is derived for round loops; on the loop sagged to 0.7 D, 24 points spaced by angle leave a 4.8 mm sagitta, over the 4.0 mm bound. Sample the turn by arc length, or apply the rule at the loop's tightest curvature.
  - **F01's fence-top festoon (S8):** nothing drives it yet (no count; its drop is only in the D text). Add a parameter, or state that `damage` and `seed` place it.
  - **F04's spill cones (S8):** the Physics role does not list their collision; the buildability review gives it: 2–3 stacked `surface:crater_spoil` cylinders within ±5 cm.
  - **F02's and F03's Outputs lines (S8)** differ from the buildability review's LOD tables (F02 LOD0–LOD2 and cull; F03 LOD0–LOD3 and cull); the "box impostor row" is the world's merged LOD of a field, not the tooth's. To reconcile in the asset change.
- **For the layout (phase 3):** where wire runs lie, where the checkpoints stand and how many hedgehogs there are: assumed here, set by the layout.
- **For the pilot, at G2 only, as a look (D-024):** which forms read as today's: coils loose on the ground or on pickets, bright or rusted; teeth squat or slim; hedgehogs of rail or angle; a manufactured or an improvised barrier. No number is asked.
