# Physics reading: the pole and wire contracts (L01, L03)

**Reviewer:** physics-engineer · **Task:** 1.5 · **Branch:** `unity/physics/p1-l01-l03-pole-wires--reading` · **From tip:** `2df9d0a` (`unity/art/p1-l01-l03-pole-wires`)
**Against:** AST-05, PHY-19, PHY-22, PHY-23, PR-6, PR-9, D-007, D-024; knowledge §5.2–§5.4, §6.8, §7.3–§7.5, §10; the archived `p1-fortifications-sheet/review-physics.md` §1 (the snag-record fields, `edge_radius_m`) and §7 (the snag budget); the V01 reading's N-2 (every record names the material of the member it follows)
**Read:** `CLAUDE.md`, `AGENTS.md`, `docs/workflow.md`; this change's proposal, design (with 1.1's and 1.2's "As built"), tasks and both spec deltas; `docs/object-sheets/lines-roads-debris.md` §Shared, §L01, §L03; `docs/requirements.md` PHY-19, PHY-22, AST-05; `assets-src/physics_materials.json`; `tools/blender/generators/{pole,wire_span}.py`, `tools/blender/lib/{meta,audit}.py`; the six built `L01_*` and `L03_*` metadata files

I consume these contracts: the wire contact and strike (PHY-22), the fiber's bend, capstan and cut risk over a wire and a pole head (PHY-18, PHY-19), the gap probe and the simulated pilot (PHY-23, `GapsNear`), the rotor's ground and ceiling rays, and the log. I rule by calculation (D-024) and do not edit the generators. Every number is labelled **published / derived / assumed**; every assumed number is a named, tunable parameter. The arithmetic ran in a plain Python script in my scratch folder (not committed); §10 has its output. No Unity, no Blender.

## Verdict

**The contracts are sound and the built metadata matches them; eight additions or corrections go back to the asset-artist (§8).** None changes a mesh or the look; two add fields the phase-7 wire model cannot do without.

1. **Ports:** enough geometry. The conductor's end at position + r · direction is exact. The phase-3 compiler adds the pairing rule, `ends` and a 5 mm match for prebuilt spans. The port gains `max_diameter_m`, 0.014 m for the TF-20 groove (§1).
2. **The `wires` record:** enough for the tension (378 / 331 / 282 N), the stiffness along the span, the catch radius and the fiber's capstan. **Not enough for the stop.** A span's 42.7 mm of slack is gone after 0.92 m of sideways push. Past that, the conductor's stretch stops the drone or breaks the span, and a strike at cruise carries 694–1,162 J against the 299–431 J that break a rigidly held span. **Add `axial_stiffness_n` and `breaking_force_n`.** The masses: 0.154 kg/m right (derived), 0.135 right (published), 0.23 becomes **0.244 kg/m** (§2).
3. **The wire audit:** right as a one-sided test, because the record is the physics. It measured 0.000 mm. The tolerance becomes **min(5 mm, D/2)**, so a drawn thin wire always overlaps its physical one (§3).
4. **The brace gap:** **1.611 m is right**: the real clear space, 4A/P = 1.6109 m by hand. The heights follow at zero margin in the triangle's own plane (2.949 and 3.235 m). Above the ground, on the 9° lean, they are **2.91 and 3.20 m**, and the id should carry those (§4).
5. **`planted`:** physically right. The map places a planted asset at the **lowest hard-terrain height over every member's ground crossing**, so the terrain there is never more than **5 mm** below the asset's y = 0 (derived from the log's 10 mm). Terrain above y = 0 is covered (§5).
6. **Snag records:** every record names its member's material (V01 N-2 holds). Four radius fixes:
   - wires gain `edge_radius_m`: the strand (A-50: 1.5 mm, which raises the fiber's bend-stress cap from 1.00 to 2.92 GPa) or the core (SIP: 5 mm)
   - hooks 8.5 mm, not 8
   - bands on the strap's centre line at 3 mm
   - the neck rings move out of the insulator's cylinder, where no fiber can reach them (§6)
7. **Clearance:** the flight model reads none of it. The compiler rechecks it against the terrain under each span. The flight model needs three things:
   - its rotor rays must skip wires
   - the swept test holds (22 mm a step against the 60 mm it was built for)
   - the zig-zag conductors leave no level passage (0.491 m against the 0.55 m envelope), only a diagonal one that the contacts bound, so "no gap" is right (§7)

## Constants this reading uses

| Quantity | Value | Label |
|---|---|---|
| Drone | 4.8 kg nominal; 0.574 m nose-first, 0.706 m diagonal; vertical envelope 0.55 m | derived (knowledge §5.2, §5.4); assumed (envelope, houses sheet) |
| Cruise | 17–22 m/s, so 694–1,162 J of kinetic energy at 4.8 kg | derived (PHY-17a) |
| Motor torque at full thrust | 0.38–0.51 N·m (κ 0.015–0.02 N·m per N at 25.5 N) | derived from assumed κ (knowledge §5.3) |
| Motor hub (bell) radius | 0.015 m | assumed (`motor_hub_radius_m`) |
| Fiber | E·r = 4.56e6 N/m, coating 62.5 µm, EI 8.75e-7 N·m²; strength draw 0.69–5.0 GPa | published / derived / assumed as in the fortifications reading |
| A-50 (ГОСТ 839-80 Table 2, marks A and АКП) | 49.5 mm², 9.0 mm, 135 kg/km, ≥ 8,198 N | published, https://dnaop.com/html/45891_4.html |
| A-16 (same table) | 15.9 mm², 5.1 mm, 43 kg/km, ≥ 3,021 N | published, same |
| Strands | A-50 7 × 3.00 mm (7 · π · 1.5² = 49.48 mm², 3 × 3.0 = 9.0 mm); A-16 7 × 1.70 mm | derived from the published section and diameter |
| Steel wire, 4.5–6.0 mm, unannealed | 390–830 N/mm², so ≥ 7.66 kN for 5 mm | published, ГОСТ 3282-74 Table 2, https://dnaop.com/html/42651/doc-%D0%93%D0%9E%D0%A1%D0%A2_3282-74 |
| SIP-4 2 × 16 | 12.85 kg per 100 m, so 128.5 kg/km | published (a retail coil listing, which may include packing), https://enex.market/en/product/provod-sip-4-2kh16-0-6-1-bukhta-100m/ |
| Leg drop under the prop plane | 0.30 m, the rest of the 0.55 m envelope above it | assumed (`leg_drop_m`; legs 0.25–0.35 m, knowledge §5.4) |
| SIP insulation, 16–35 mm² | 1.3 mm nominal | published, https://www.ruscable.ru/info/wire/mark/sip-4/ |
| E, steel / hard-drawn aluminium | 200 GPa / 63 GPa | assumed (`e_steel_pa`, `e_aluminium_pa`; textbook 200–210 and 60–70 GPa, stranding lowers aluminium) |
| SIP core strength | 166 MPa on the aluminium section (A-50's rated 8,198 N / 49.5 mm²) | assumed (`sip_core_strength_pa`), derived from the published A-50 |
| TF-20 profile | neck groove r 0.019 m from 0.068 to 0.082 m, lips r 0.026 m, skirt r 0.0375 m | assumed (`pole.py` INSULATOR_PROFILE) |

## 1. Ports

**Ruling: the record is enough geometry; the compiler adds five rules and the port adds one field.**

What holds:
- **The end.** A conductor's centre at position + r · direction is exact for a conductor running square to the direction, as every span along the line does. `L03_*` lands on `L01_straight`'s ports within 1.3 µm (design 5, as built). On the leaning pole the direction leans with it: (0, −0.156, 0.988). The 9 mm wire's end then sits 0.7 mm lower than on an upright port, which is right because the groove leans too.
- **The id.** `hook_<n>` from the top, the crossarm's two pins first. Position gives the height rank, direction the side. That is all a pairing needs.

What the phase-3 compiler must add (world-builder):
1. **Pairing by height rank with matching sides, not by id.** A line's end pole faces its pull (+X toward the span). Its zig-zag then mirrors its neighbour's, so id pairing would cross the conductors. The rule:
   - pair the n-th highest port on one side of the span with the n-th highest on the same side of the next pole, the side being the sign of direction · the chord's horizontal normal
   - refuse a pair whose directions face opposite sides
   - where a mirrored end pole's heights do not match, it needs a mirrored zig-zag (hook_0 on −Z), a pole param phase 3 asks for, never a crossed span
2. **Ends from the placed ports.** Transform position and direction by the placement, add r · direction, and solve the catenary for the actual horizontal span and end heights with the span's sag ratio. The match is then 0 mm by construction.
3. **A prebuilt span placed as an object** (the gallery station) matches its ports within **5 mm** at each end. Derived: the groove is 7 mm deep (0.026 − 0.019 m), and 5 mm keeps the conductor in it.
4. **`ends` per compiled wire:** [(object, port id), (object, port id)], or a house's wall port. The log names a struck span by its two poles. A snapped pole drops the spans it carries (L04). The phase-7 model reads its end conditions from it (§2.2).
5. **No chord back across its own pole:** chord · direction ≥ −sin 5° (assumed, `port_back_angle_deg`). A drop leaves from a port facing its house; an angle pole's head is not built.

What the asset adds (§8 item 4):
- **`max_diameter_m` on every port:** the largest conductor its seat takes, so the compiler refuses a conductor that cannot sit in it. The TF-20 neck groove is 0.014 m wide (0.068–0.082 m up the insulator, derived from the profile).
- `L03_medium`'s 9 mm and `L03_thin`'s 5 mm fit. **`L03_thick`'s 20 mm bundle does not:** it hangs from `hook_2` with 3 mm a side over the lips. That is visual only (a wire has no collider), so it is acceptable for G1. A SIP belongs on a clamp port (the `sip_bracket` head, not built).

## 2. The `wires` record

### 2.1 Tension and stiffness: enough

The span L is the horizontal distance between the first and last points, so no field is needed. w = mass · g, and T = w·L²/(8·sag) is the horizontal component.

| Variant | w (N/m) | L, sag (m) | T (N) | Catenary c (m) | Slack, arc − chord | Sideways push that uses it up |
|---|---|---|---|---|---|---|
| `L03_thin` 5 mm steel | 1.511 | 40, 0.8 | **377.7** | 250.0 | 42.7 mm (record: 42.63) | 0.92 m |
| `L03_medium` 9 mm A-50 | 1.324 | 40, 0.8 | **331.1** | 250.0 | 42.7 mm | 0.92 m |
| `L03_thick` 20 mm SIP | 2.256 | 20, 0.4 | **282.0** | 125.0 | 21.3 mm | 0.46 m |

All values are derived.
- **The drop is inclined** (6.355 m → 3.5 m). T = w·L²/8f is its horizontal component, with f measured vertically from the chord, which is what the record's `sag_m` is. The pole end carries 288.5 N (catenary, derived): 2 % more.
- **Stiffness varies along the span:** k(x) = T·L / (x (L − x)), with 4T/L only at mid-span (derived, a taut string under a point load). On `L03_medium` that is 33.1 N/m at mid-span, 44.1 at 10 m, 75.7 at 5 m and **174 N/m at 2 m from a pole**. The strike model takes the local form.
- **One bundle, one record** is right for the strike: the twist binds the cores. The catch radius and the mass are the bundle's. The fiber reads the cores (§6).

### 2.2 The stop: not enough

**Ruling: add `axial_stiffness_n` (EA) and `breaking_force_n` (the rated minimum) to every record.**

PHY-22 says "wire compliance comes from span tension". That holds only until the slack is used up, which takes **0.92 m** of sideways push at mid-span on a 40 m span. Past that, the conductor must stretch.
- The stretch travels at c_L = √(EA/μ) = **4.4–5.1 km/s**, and a pole is 4.6–8.3 ms away and back.
- An impact lasts tenths of a second. So a rigidly held span is loaded quasi-statically.
- It stores T²·S / (2 EA) when it reaches its breaking force T_b.

| Variant | EA | T_b | Energy to break, rigid ends | Breaks at (sideways, mid-span) | Speed whose energy that is |
|---|---|---|---|---|---|
| 5 mm steel | 3.93 MN | 7.66–16.3 kN | 299–1,353 J | 1.56 m (at 7.66 kN) | 11.2–23.7 m/s |
| 9 mm A-50 | 3.12 MN | 8.20 kN | 431 J | 1.72 m | 13.4 m/s |
| 20 mm SIP 2 × 35 | 4.41 MN | 11.6 kN | 305 J | 0.86 m | 11.3 m/s |

All values are derived, from the published T_b and the assumed E above.

**What this means:**
- **A strike at cruise (17–22 m/s, 694–1,162 J) carries more energy than breaks a rigidly held A-50 or SIP span.** Stopping 20 m/s within the break deflections takes a mean 0.56–1.1 kN.
- **The sheet's "a strike never breaks a live line" is not derived.** It compares the conductor's strength with the drone's pull (thrust of 88–123 N), not with its impact energy.
- **The outcome is the model's:** stop, break, or an end letting go. Whatever stretches between the two anchors decides it:
  - the tie slipping
  - the pin bending
  - the insulator breaking
  - the pole's top deflecting
  - the neighbouring spans taking up length through slipping ties
  - the house anchor pulling out of the wall on a drop
- **The record must carry EA and T_b** so that nothing re-derives them from a conductor name. The anchors are phase 7's (§9 N-2).
- **Labels:** T_b is published for A and steel (the ГОСТ minimum and the band floor) and assumed for SIP. EA is assumed through E.
- **Randomness:** phase 7 may draw T_b above the rated minimum as a named, bounded perturbation (PHY-24). The record holds the minimum.

### 2.3 Thin against thick: what the record already carries

**The props cannot wind an intact span.** Winding takes T · r_hub = 4.2–5.7 N·m, against 0.38–0.51 N·m at full thrust (derived), 8–15 times more. So PR-6's "thin wire wraps and tangles in the props" comes from two sources:
- the frame tumbling over a taut thin wire, which needs contact geometry and friction (diameter and `cable` μ, present)
- slack wire (cut or down, L04 and L05, or a span the strike has broken)

The thin-to-thick difference for an intact span is carried by numbers already in the record:
- the diameter against the drone's parts
- the wire's impedance 2√(T μ): the first-instant force at 20 m/s is 267 N (A-50), 305 N (5 mm steel) and 322 N (SIP), and 1.0 kN for the sheet's SIP main (1,140 N, 0.58 kg/m)

The bending stiffness EI is not needed for the intact spans built here. Their bending lengths √(EI/T) of 7–13 cm are alike across the three classes (derived; strands free). It becomes needed for the slack states and comes with them (§9 N-2).

### 2.4 The masses (task 1.2's derived values)

| Record | Value | Check | Ruling |
|---|---|---|---|
| 5 mm steel | 0.154 kg/m | 7,850 · π · 0.0025² = 0.1541 kg/m; the real class is a single-strand steel line conductor | **right** (derived) |
| 9 mm A-50 | 0.135 kg/m | ГОСТ 839-80 Table 2: 135 kg/km; bulk 2,122 kg/m³ is a 0.778 aluminium fill | **right** (published) |
| 20 mm SIP drop | 0.23 kg/m | A 20 mm two-core bundle is 2 × 35 mm² (19.0 mm from the construction). Its mass by construction (2 × (35 mm² aluminium at 2,703 kg/m³ plus 1.3 mm XLPE at 950 kg/m³)) is 0.253 kg/m. Calibrated on the published 2 × 16 (construction 0.133 against 0.1285 kg/m, ×0.966), that is **0.244 kg/m**. The (20/15)² scaling of the sheet's 0.13 assumes a fixed fill, but the insulation stays 1.3 mm while the core grows | **0.244 kg/m** (derived), not 0.23 (6 % light: T 300 N, not 282). §8 item 2 |

## 3. The wire audit

**Ruling: right for contact, one-sided by design, with the tolerance tied to the wire.**
- **The record is the physics.** StaticContacts sweeps the parts against the point list as a capsule chain of radius D/2 (knowledge §10.4), and nothing re-reads the mesh (design 6). So the audit's job is to keep the drawn tube from standing outside the physical envelope: a wire drawn where the drone passes through it. That is "distance to the point list less the radius, zero inside", and it measured **0.000 mm** on every built tube.
- **The inside is harmless and need not be measured.** The physical envelope stands outside the drawn tube by:
  - 0.33 / 0.60 / 1.34 mm at the 6-sided faces (5 / 9 / 20 mm)
  - up to 6.18 mm on the flat side of the two-core bundle (an envelope point 90° off the core plane against the nearer 5 mm core)

  Both are far inside AST-05's ±2 cm, and the fiber's finer contact is `edge_radius_m` (§6).
- **The record's own sampling:** the 0.5 m chords stand 0.13 mm off the true catenary (design 5). That is immaterial.
- **The tolerance:** 5 mm is right as a bound for the drone (below any part's own scale). For a thin wire it is wrong, because a 3–9 mm wire drawn 5 mm off its point list would not overlap its physical self: at 0.3 m in front of the camera, 5 mm is 4.5 feed pixels (270 px/rad, derived). **Make it min(5 mm, D/2)**: 2.5 mm at 5 mm, 4.5 mm at 9 mm, 5 mm at 10 mm and above. The built tubes pass unchanged (§8 item 8).
- **Per vertex to the nearest wire of its slot** is right while a slot's wires stand apart (0.5 m on `L03_medium`).
- **The swept test catches every built wire.** At the 22 m/s cruise top a part moves 22 mm a 1 ms step, under the 60 mm at which the query was proven to catch a 5 mm wire with a 15 mm sphere (knowledge §10.4).

## 4. The brace gap

**Ruling: 1.611 m is right; the heights follow in the triangle's plane at zero margin; the id should give them above the ground.**

**Recomputed by hand** (derived, `L01_leaning` defaults):
- r(0) = 0.119474 m
- the brace's inner line meets the ground at 2.184980 m
- the inner surfaces meet at 4.48006 m up the plane
- the base is 2.06551 m
- inscribed circle: 4A/P = **1.610885 m**, the metadata's `narrowest_m` exactly
- the sheet's 1.78 m is the triangle of the two axes at a rounded 4.9 m attachment (1.7784 m; 1.7693 m at 4.875 m), not clear space

**Against the drone:**
- The clear width in the plane is w(y) = 2.06551 − 0.46104 y.
- It passes the 0.706 m diagonal up to y = **2.9488 m**, and 0.574 m nose-first up to **3.2351 m**. In between, the houses sheet's yaw rule applies: ±16° at 3.07 m, ±5° at 3.18 m.
- These heights are for the prop plane, the drone's widest part, at zero margin, as the sheet's are. The legs and the payload are narrower.
- The drone's whole section (0.706 × 0.55 m) fits a 0.895 m circle, so the 1.611 m circle passes it with room.
- A horizontal slice of the leaned plane is a line along X at the same height, so the slice rule needs no correction for the lean.
- **The tolerances hold.** The log's capsules are within 8.6 mm and the brace's capsule is at its own radius, both inside the ±2 cm that AST-05 asks within 0.3 m of an opening.

**The name:**
- `brace_triangle_any_yaw_below_2.95m_nose_first_below_3.24m` rounds the in-plane heights correctly, but they are measured along a plane leaned 9°.
- The sheet's rule reads "above the ground", which is y · cos 9° = **2.912 and 3.195 m**.
- The id should read `brace_triangle_any_yaw_below_2.91m_nose_first_below_3.20m`: the in-plane height times √(1 − n_y²) of the gap's normal (§8 item 5).
- On an upright braced pole the two agree.

## 5. The planted rule

**Ruling: physically right; the map must place every planted asset on its lowest ground crossing.**

The terrain covers the half-space under the asset's y = 0 only where the terrain is at or above y = 0. Then no drone part, foot or fiber node can reach a primitive's buried surface, because contact keeps them above the terrain and a part below it is a `tunnel` bug (knowledge §7.5). So the buried capsule caps (0.113 m on the log) and the stub's 0.3 m box are no invisible walls. Not drawing below y = 0 is right.

What the map must guarantee (world-builder, phase 3):
- **Where:** at **every member's ground crossing**, not only the pole's foot: the log's foot outline, the brace's foot (2.27 m out on `L01_leaning`), the stub's footprint and the grounding wire's foot. The terrain there must lie **at most 5 mm below** the asset's y = 0.
  - Derived: the log's capsule is already 8.6 mm off the mesh. A band of exposed capsule δ high under the open cut adds δ at right angles, and √(8.6² + δ²) stays within the sheet's 10 mm only while δ ≤ 5.1 mm.
  - The same bound shows no open tube end at 1.5 m.
- **How:** set the asset's y = 0 at the **lowest terrain height** over those outlines, never at the axis.
  - Use the exact rendered terrain triangle of `SampleGround`, not the mat's support top: the pole stands in the soil, and the straw lies around it.
  - On a 5 % slope the log's own 0.24 m foot spans 12 mm and the brace's foot 114 mm. At the axis height both would float.
- **Terrain above y = 0** buries a member deeper. That is harmless to contact (covered) and to the look.
- **The brace gap:** where the terrain under the brace triangle rises more than AST-05's 2 cm above its bottom edge, the compiler clips the gap polygon at the terrain and re-measures `narrowest_m`.
- **The clearance** is rechecked against the terrain (§7).

## 6. Snag records

**Ruling: the materials are consistent (V01 N-2); four radii need the fortifications reading's rule.**

| Record | Material | Radius as built | Ruling |
|---|---|---|---|
| Wire, `edge`, the whole conductor | `cable` | D/2 | `radius_m` D/2 (the reach) right; **add `edge_radius_m`** where the fiber bends over something sharper than the conductor (§8 item 3) |
| Hook and pin, `hook` | `steel` | 0.008 | **0.0085**, the bar's half thickness (the `hook` convention and the capsule's radius). At 8 mm the record's envelope lies 0.5 mm inside its own capsule (§8 item 6) |
| Insulator neck ring, `edge` | `glass` | 0.005 | **Unreachable:** the ring at r 0.019 with its 5 mm reaches 0.024 m, 13.5 mm inside the insulator's rigid 0.0375 m cylinder, which the fiber meets first. Move it to the skirt's drip edge: a ring through the corner's centre of curvature at r 0.0325, 0.005 m up, radius 0.005, so its envelope is flush with the cylinder's side and bottom (§8 item 7) |
| Crossarm end, `edge` | `timber` | 0.002 | right (timber's own arris) |
| Band, `edge` | `steel` | 0.002, on the strap's inner outline | the drawn strap stands 6 mm proud, the record 2 mm: **put it on the strap's centre line**, BAND_T_M / 2 out, radius 0.003 (6 turns of 5 mm wire, flush with the outer face) (§8 item 6) |
| Grounding wire, `edge` | `cable` | 0.003 | right (its own radius) |

**`edge_radius_m` on a wire:**
- **Bare stranded:** the fiber pulled across a stranded conductor conforms to the outer strands' crests once its tension passes EI/R², 0.36 N at R = 1.5 mm (derived). It is then bent at the strand's radius, not the conductor's:
  - A-50's 7 × 3.00 mm strands give `edge_radius_m` **0.0015**, and the bend-stress cap E·r/(R + c) rises from **1.00 GPa** (the 4.5 mm conductor) to **2.92 GPa**
  - A-16 (1.70 mm strands) gives 0.00085 and 5.0 GPa, at the top of the strength band
  - So for a line dragged over a bare conductor, the cap rises from 7 % to 52 % of the way up the 0.69–5.0 GPa strength band (PHY-19)
- **SIP:** the fiber crossing a twisted bundle bends over one core. `L03_thick` gets **0.005**, the generator's core radius, so the cap rises from 0.45 to 0.90 GPa.
- **Solid steel wire:** absent, its own 2.5 mm (1.78 GPa).

The field is the fortifications reading's: optional, positive, at most `radius_m`. This is its first user, so the validator rule of that reading §1 lands here.

**The snag budget** (fortifications §7) is not touched. A 2 m cube meets about five 0.5 m segments per conductor, 20 for `L03_medium`'s four, and a pole head adds fewer than 60. That is against the 2,048-segment cap (derived).

## 7. Clearance and what the flight model needs

**Ruling:**
- **The flight model reads no clearance figure.** It meets wires as geometry.
- **The figures are a placement guarantee:**
  - 5.055 m lowest on `L03_medium`
  - the generator's floors of 3.5 m bare, 2.5 m SIP and 2.75 m for a drop, the lowest the ПУЕ allows anywhere (published via the sheet)
- **The compiler rechecks them against the terrain** under each compiled span: min over the points of (wire height − r − terrain height) ≥ the type's figure. The generator's check assumes level ground between two feet at y = 0.

What the flight model needs from these wires:
1. **The rotor's up and down rays skip wires** (knowledge §10.5, 8 rays for ground and ceiling effect). A conductor across a 10″ disc covers 2.5 % (5 mm), 4.5 % (9 mm) or 10 % (20 mm) of its area (derived). A ray that hits it must not read as a ceiling or a floor, so `Raycast` for the rotor rays filters `cable` wires out.
2. **The swept contact catches every built wire** at cruise (§3).
3. **No level passage between the zig-zag conductors; a diagonal one exists.**
   - At `L03_medium`'s mid-span the +Z plane carries 6.555 and 5.555 m, the −Z plane 6.055 and 5.055 m, and the planes are 0.513 m apart, under the drone's 0.574 m.
   - **Level:** the band clear in both planes is 5.5595–6.0505 m, **0.491 m against the 0.55 m envelope**, so a level pass is impossible.
   - **Diagonal:** the legs and payload sit inside the 0.24 m square, and only the thin prop plane straddles both planes. So a drone that changes height while crossing does pass. Coming from +Z it descends: 0.06 m at zero margin, or about 0.26 m with margins near 0.1 m, over the 0.513 m between the planes. From −Z it climbs the same. This assumes the envelope's split (`leg_drop_m` 0.30 under the props, the payload above them).
   - **No gap record is right, as the sheet says.** This is a volume between four lines, not a planar opening, and the wire contacts bound it exactly. The gap probe and the simulated pilot learn it from `StaticContacts`, not from `GapsNear`. The other ways past are over 6.56 m and under 5.05 m.

## 8. Sent back to the asset-artist

1. **`wires`: add `axial_stiffness_n` and `breaking_force_n`** (§2.2).
   - Write them from a per-type table in `wire_span.py`: E (steel 200 GPa, aluminium 63 GPa), the metallic section by type (solid steel π D²/4; bare A by the published section, a 0.778 fill; SIP the cores' aluminium) and the rated strength.
   - Values: `L03_thin` 3.93e6 N and 7,660 N; `L03_medium` 3.12e6 N and 8,198 N; `L03_thick` 4.41e6 N and 11,600 N.
   - Labels: published T_b for A and steel; assumed E and the SIP strength.
   - `meta.validate` requires both, positive.
2. **`L03_thick`: `mass_kg_per_m` 0.244** (derived, §2.4), the conductor named 2 × 35 mm² in the params' comment and design 10.
3. **Wire snag records: `edge_radius_m`** (§6): bare A the outer strand's radius (D/6 for a 7-strand conductor: 0.0015 on A-50), SIP the core's radius (0.005 on `L03_thick`), solid steel none. In `meta.validate`, the fortifications reading's rule: optional, positive, ≤ `radius_m`.
4. **Ports: `max_diameter_m`**: the largest conductor the seat takes, 0.014 m for the TF-20 neck groove. Validated positive. Design 5 notes that `L03_thick`'s 20 mm bundle exceeds it, so it waits for a clamp port (§1).
5. **The brace gap's id in heights above the ground:** the in-plane heights times √(1 − n_y²) of the gap's normal, so `L01_leaning` reads `brace_triangle_any_yaw_below_2.91m_nose_first_below_3.20m`. The spec scenario and design 3, which quote 2.95 / 3.24, follow (§4).
6. **Hook and band records:**
   - hooks and pins `radius_m` HOOK_D_M / 2 = 0.0085
   - bands along the strap's centre line (BAND_T_M / 2 out of the outline) with `radius_m` BAND_T_M / 2 = 0.003 (§6)
7. **Insulator rings at the skirt's drip edge:** r INSULATOR_R_M − 0.005, 0.005 m up, `radius_m` 0.005, `glass`, so the fiber can reach them (§6).
8. **The wire audit's tolerance min(5 mm, D/2)**, printed per wire. The test that fails at 20 mm and passes under it keeps its cases (§3).

Items 1, 3 and 4 add fields to the delta "Ports and wires in the metadata", and item 5 changes the L01 scenario's text. Those spec edits are the orchestrator's.

## 9. Notes for later owners (non-blocking)

- **N-1, world-builder (the phase-3 compiler):**
  - the pairing, ends, `ends` record, 5 mm prebuilt match and back-angle rule of §1
  - the planted placement, gap clipping and terrain clearance of §5 and §7
  - the wire's `edge` snag rewritten from the same points as its record
  - house assets writing wall ports (≥ 2.75 m, the drop's ПУЕ figure) with a `max_diameter_m`
- **N-2, physics-engineer (the phase-7 wire model):**
  - Use k(x) = T·L/(x(L − x)), not the mid-span constant.
  - Take the slack from the points; past it, the stretch through EA; break at `breaking_force_n`.
  - End conditions per `ends`: the tie's slip, the pin and the insulator, the pole top's compliance, the house anchor's pull-out, which is probably a drop's weakest link (the sheet's "torn off the house").
  - The wire's inertia through 2√(T μ).
  - EI and a free end's curl when cut and down are built (L04, L05).
  - QA scenarios: a 20 m/s strike at mid-span and at 2 m from a pole (33 against 174 N/m) on each variant, each logged as stop, break or anchor, with the energy split.
- **N-3, reference-analyst (the lines sheet):**
  - The conductor table's breaking forces are the АН alloy's (ГОСТ 839 Table 3: 3,734 … 10,662 N), not the A marks' (Table 2: A-16 3,021, A-25 4,500, A-35 5,913, A-50 8,198 N).
  - "Every wire is far stronger than anything the drone can pull … a strike never breaks a live line" should become: "a strike at cruise carries the energy that breaks a rigidly held span; whether it stops, breaks or pulls an end is PHY-22's model (review-physics §2.2)".
- **N-4, physics-engineer (the rotor rays):** the `cable` filter of §7 item 1 goes into the rotor's ray batch when the flight model lands.
- **N-5, qa-engineer:** a cheap guard on `diameter_m` is that each wire's farthest tube vertex lies within the tolerance of its envelope, so the record is as thick as the tube. The bundle reaches 9.762 of 10 mm.

## 10. Evidence

Script `l03_reading.py` in my scratch folder, run with `python -I` (not committed):

```
== 4. brace gap (L01_leaning defaults) ==
r0 0.119474 base 0.119474..2.184980 = 2.06551 apex 4.48006 incircle 1.610885
axes triangle at 4.875: 1.7693; at 4.9: 1.7784
clear width w(y) = 2.06551 - 0.46104 y
any_yaw: w(y)=0.706 at 2.9488 m in plane; above ground on a 9 deg lean 2.9124
nose_first: w(y)=0.574 at 3.2351 m in plane; above ground on a 9 deg lean 3.1952
drone section circumscribed (0.706 x 0.55): 0.895 m
  W 0.65: at 3.070 m, yaw up to 16.1 deg
  W 0.6: at 3.179 m, yaw up to 4.9 deg
== 5. planted tolerance ==
log radial deviation 8.6 mm, sheet 10 mm: exposed band <= 5.10 mm
terrain rise across the log foot on 5 % slope: 11.9 mm; across brace foot 2.273 m: 114 mm
== 2. spans ==
L03_thin 5 mm steel: w 1.511 N/m T 377.7 N k_mid 37.8 N/m c 250.0 m slack 42.7 mm slack used at 0.924 m sideways; wave 49.5 m/s Z 7.63 kg/s F0@20 305 N; EA 3.93 MN Tb 7.66-16.30 kN E_break 299-1353 J (v 11.2-23.7 m/s); wind-up torque T*r_hub 5.7 N m
    fiber bend cap over own radius 2.50 mm: 1.78 GPa
    rigid ends: breaks at 1.56 m sideways at mid-span (stretch 78 mm), mean force to stop 20 m/s there 617 N; c_L 5050 m/s, round trip to the nearer pole 7.9 ms
L03_medium 9 mm A-50: w 1.324 N/m T 331.1 N k_mid 33.1 N/m c 250.0 m slack 42.7 mm slack used at 0.924 m sideways; wave 49.5 m/s Z 6.69 kg/s F0@20 267 N; EA 3.12 MN Tb 8.20-8.20 kN E_break 431-431 J (v 13.4-13.4 m/s); wind-up torque T*r_hub 5.0 N m
    fiber bend cap over own radius 4.50 mm: 1.00 GPa
    fiber bend cap over strand/core 1.50 mm: 2.92 GPa
    rigid ends: breaks at 1.72 m sideways at mid-span (stretch 105 mm), mean force to stop 20 m/s there 558 N; c_L 4806 m/s, round trip to the nearer pole 8.3 ms
L03_thick 20 mm SIP 2x35: w 2.256 N/m T 282.0 N k_mid 56.4 N/m c 125.0 m slack 21.3 mm slack used at 0.462 m sideways; wave 35.0 m/s Z 8.05 kg/s F0@20 322 N; EA 4.41 MN Tb 11.59-11.59 kN E_break 305-305 J (v 11.3-11.3 m/s); wind-up torque T*r_hub 4.2 N m
    fiber bend cap over own radius 10.00 mm: 0.45 GPa
    fiber bend cap over strand/core 5.00 mm: 0.90 GPa
    rigid ends: breaks at 0.86 m sideways at mid-span (stretch 53 mm), mean force to stop 20 m/s there 1115 N; c_L 4379 m/s, round trip to the nearer pole 4.6 ms
drone KE at 17.0 m/s: 694 J
drone KE at 20.0 m/s: 960 J
drone KE at 22.0 m/s: 1162 J
k along the span (L03_medium) at x = 2, 5, 10, 20 m: [174.3, 75.7, 44.1, 33.1]
drop: tension at the pole end (catenary): 288.5
== masses ==
steel 5 mm solid: 0.1541 kg/m
A-50: 7 x 3.0 mm = 49.48 mm2, d = 3 x 3.0 = 9.0 mm; A-16 strand 1.70 mm
A-50 bulk density 2122 kg/m3; Al fill 0.778
2x16 construction 0.1331 kg/m bundle 14.6 mm; published 0.1285 -> factor 0.966
2x35 construction 0.2528 kg/m bundle 19.0 mm; calibrated 0.2442
L03_thick at 0.245: T 300.4 N, k 60.1
== 7. zig-zag clear band at mid-span (L03_medium) ==
level band through both planes: 5.5595..6.0505 = 0.491 m vs envelope 0.55; planes 0.513 m apart vs 0.574
   rotor line blockage, D 5 mm across a 10 in disc: area share 2.5 %
   rotor line blockage, D 9 mm across a 10 in disc: area share 4.5 %
   rotor line blockage, D 20 mm across a 10 in disc: area share 10.0 %
groove: lips r 0.026, neck r 0.019 -> depth 0.007 m; width 0.068..0.082 -> 0.014 m
neck record envelope out to 0.024 m, inside cylinder r 0.0375 by 13.5 mm
6-sided tube face inset: 5 mm 0.33 mm, 9 mm 0.60 mm, 20 mm 1.34 mm
2-core bundle: envelope point 90 deg off the core plane stands 6.18 mm off the nearest core
swept step at 22 m/s: 22 mm per 1 ms step (query guarantee built at 60 mm)
```

The built metadata, read with `python -I` from `unity/Assets/_Project/Art/Models/lines/`:
- `L01_leaning`: gap `narrowest_m` 1.610885, polygon [[0.119474, 0, 0], [2.18498, 0, 0], [0.095894, 4.424901, 0.700835]], normal [0, −0.156434, 0.987688]
- `L01_straight` ports: `hook_0`–`hook_3` at 7.355 / 6.855 / 6.355 / 5.855 m, 0.250579–0.258474 m out
- `L03_medium`: four records of 81 points, lowest 6.555 / 6.055 / 5.555 / 5.055 m, arc − chord 42.63 mm each; `L03_thick` one record of 41 points from 6.355 m to 3.5 m
- every snag record carries `material`
