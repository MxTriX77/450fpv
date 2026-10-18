# Object sheet: lines, roads and debris (L01–L05, R01–R03 roads, K01–K06, K08–K10)

**Owner:** reference-analyst. **Reviewed for buildability by:** asset-artist (task 1.7, pending).

**OPSEC:** cite clips and photos by letter only. Describe classes, never a specific identifiable building, place, date or person. No file names, and no OSD values.

**Requirements:** AST-02, AST-04, AST-05, AST-06, REAL-01…05, PHY-22, PHY-23, SEC-01, SEC-02, PRC-06 · **Decisions:** D-007, D-013, D-024, D-028 · **Change:** `p1-object-sheets`, task 1.4.

**Rows:** L01–L05 and the road kits R01–R03 from [asset-list.md](../asset-list.md) §8, and the debris kit K01–K06, K08–K10 from §10 (K07 is in the vehicles sheet). The road kits share their IDs with the vehicles R01–R03 of §9; here they are always written "R01 (road)" and so on (see Open points).

## How this sheet was made

- **Letters read**, through contact sheets only: C 19 stills at 2/s, D 21 at 2/s, E 24 at 2/s, H 24 at 1/s, L 14 at 2/s; the photos J, K, M, O as single stills. Native-size crops for thin objects and colour sampling: C 1, 3, 5, 7, 8, 12; D 1, 3, 7, 9–14; E 7, 10, 15–18; H 1–3; L 1–2; and regions of J, K, M, O.
- **Reused, not re-measured:** terrain.md §4 B5–B6, §6 S1–S5, §7 (micro-detail), §8 (the `asphalt`, `rubble`, `yard_litter` surface rows), §9; knowledge.md §7.4 (object materials), §9.2, §10 (map format: wires are map objects with point lists, sag and diameter), §14.4; [houses-and-yard.md](houses-and-yard.md) (brick, tile, slate and lintel sizes, the blast-failure order, the C01 tarp, the Y11 line and barrel, the Y03 top wire); [vegetation.md](vegetation.md) (V19–V20 brush, the `deadwood` proposal).
- **Scale cues:** brick 250 × 120 × 65 mm, door leaf 0.8 × 1.9 m, storey 2.8 m (AST-04); the car wheel in M (a 165/80 R13 tyre is 0.59 m across, derived from the size code: 13 × 25.4 + 2 × 0.8 × 165 mm); a slate sheet 1.13 m wide (J); the drone's own prop line (0.574 m) in the lower corners of every clip still.
- **Labels:** every number is **measured** (from a letter, with still numbers), **published** (with its source), **derived** (with the derivation) or **assumed**.
- **Feed check values** are mean sRGB of hand-picked regions on the extracted stills, as in the other sheets: warm and saturated in sun (C, D, E), flat and grey in overcast (H), black and white at night (L). `photo` values come from J, K, M, O and are closer to albedo. Textures are authored at photo albedo; the feed effect adds the shift.
- **Physics ids** are the closed list in [physics_materials.json](../../assets-src/physics_materials.json): `steel`, `sheet_metal`, `timber`, `masonry`, `concrete`, `glass`, `cable`, `foliage`. `proposed:<id>` marks a new id for physics sign-off; `fabric` and `asbestos_cement` are already proposed by the houses sheet and reused here. Snag features use the pipeline's two kinds: `edge` (a polyline the fiber bends over, with its radius) and `hook` (a free-ended member from base to tip, radius half its thickness).
- **The drone against an opening:** 0.574 m nose-first, 0.706 m on the diagonal, the yaw formula and the 0.55 m vertical envelope are as in houses-and-yard.md ("How to read this sheet"). **The legs** are taken as a 0.24 m square under the frame (assumed, as in the houses and vegetation sheets, until `drone.json` fixes it in phase 5). A piece of height h under one leg tilts the drone by atan(h / 0.24) along a side and atan(h / 0.34) along the diagonal (derived).

### How thin objects read on the feed (derived, used by every L row)

- The footage is about 450 × 286 px (PAL as recorded). With the 60° vertical FOV of the asset pipeline (design decision 7) that is about **270 px/rad on the feed**, against 935 px/rad on the 1080 px render (derived).
- So a wire of diameter D is **one feed pixel wide at d = 270 · D**, and fainter beyond, its contrast falling as 270 · D / d:

  | Diameter | 3 mm | 5 mm | 7.5 mm | 10 mm | 15 mm | 20 mm | 30 mm |
  |---|---|---|---|---|---|---|---|
  | One feed pixel at | 0.8 m | 1.4 m | 2.0 m | 2.7 m | 4.1 m | 5.4 m | 8.1 m |

- **Measured:** in J (overcast, a photo at far better resolution than the feed) the service drop is 1–2 px wide and its darkest pixels are only about 15 % darker than the sky (#afb5be on #d6dfe7). In D 12 (hard backlight) the slack cable is a dark silhouette line (#1e1816 on #adaaa4). A wire is a dark line only against a bright sky, and invisible in front light beyond a few metres. **This is the realism target: never thicken or brighten a wire to make it visible.**
- A pole (0.2 m) is one feed pixel wide at about 54 m and stays a readable silhouette at night against the brighter sky (L): the L 1 pole is 3.5–4.7 feed px wide, so 12–15 m away if it is 0.2 m thick, and its height in frame (about 7.5 m over 21° of the picture) gives about 20 m: 12–20 m (derived).

## Reference views (REAL-02)

Viewpoint heights are estimated from the scene, never from the display. The side-by-side sheets (REAL-03, `reference/_compare/`, never committed) are made once the assets exist; nothing exists to compare yet.

| Letter | Scene type | Viewpoint | Framing for this sheet | Light, time, weather |
|---|---|---|---|---|
| C | A village yard with a cellar head | A hover 1–1.5 m up, 6–8 m from the cellar head (C 1–8) | The light-blue tarp hangs down the cellar head's right flank from the roof edge to the ground, filling the right quarter of the frame (C 5–9); brick bits and leaf litter at the foot; the fruit tree left | Bright sun from the front-right; autumn; dry |
| D | An overgrown plot with a slack cable and a stone pile | A hover 1–2 m up through the weeds (D 1–12), then a dive onto the pile (13–14) | The dead trunk centre; a dark cable leaves the trunk at about 1.5–2.5 m and arcs right over the shrubs (D 9–12); pale stone slabs and red brick lower right (D 12–14) | Hard backlight and flare; late summer; dry |
| E | A stripped adobe house with debris at its foot and inside | A hover about 1 m up, 8–10 m from the facade (E 1–12), then inside (15–18) | Battens and tile clusters on the roof, tile shards along the wall foot, the plank shed's leaning board panel at the left (E 7, 10); inside, boards and plaster on the floor and a cable looping from the ceiling (E 16–17) | Soft clear light; late summer; dry |
| H | A belt edge with a concrete pole | 0.5–1.5 m over lodged straw (H 1–4) | The pole passes 3–4 m to the right, filling the frame height (H 1–3); the belt behind | Overcast, flat; autumn |
| J | A village street with a service drop and debris | Standing height on the verge, the houses 15–25 m away | The drop runs from the top centre to the left house's roof; a fainter line crosses behind the right house; slate sheets, a leaning beam, posts, silicate rubble and a brush pile between the camera and the houses | Overcast; late winter or early spring |
| K | A rubble mound at the foot of a block with its facade torn off | Standing height, the mound 5–15 m away | Brick and brick clusters, two precast concrete beams, a cut log, PVC frames, furniture panels, a blue crate, white film; fabric and pale cables hanging at the open floors; a cable loop and a perforated strip on the trampled ground in front | Overcast, dark; early spring |
| L | A night crest with a pole | 2–5 m up, the pole 12–20 m away (L 1–3) | The pole in the left third, leaning about 9°, a short crossarm near the top; a bush and a lone tree on the crest | Night; the sky brighter than the ground; black-and-white feed |
| M | An asphalt street with an abandoned car | Standing height (1.5 m), 4–6 m from the car | Pale dusty asphalt in the lower half; a shallow water-filled depression behind the car; fine cracks; a kerb, a verge with litter and a fence at the back | Bright sun, hard shadows; late winter or early spring (bare trees) |
| O | A slushy road at the snowy edge of a settlement | Standing height on the snowy verge, the road 5–30 m away | Dark wet asphalt with slush and dark debris on it, snow banks, bare trees, a lamp post | Overcast, snow light; winter |

## A correction: the straight dark lines high in C and D are OSD marks, not wires

The houses analyst found that the white bars at mid-height are the OSD's horizon bars. **The straight dark lines in the upper sky of C and D are OSD marks too** (measured, C 1, 3, 5, 8 and D 1, 3, 7, 9–14):
- they come in parallel pairs and triples, perfectly straight and tapered at both ends;
- their inner ends sit at the same screen columns in every still of both clips (the left ones end about 18–26 % of the width in from the left edge, the right ones start about 15–25 % in from the right edge), symmetric about the centre, with mirrored slopes;
- they move up and down together with the drone's pitch, never across.

A real wire does none of this. **Consequence:** terrain.md S1's "C: an overhead wire above the shed roof, about 3–5 m up" and the C01 row's "Wire above … measured C 1–8" in houses-and-yard.md rest on these marks; C shows no wire for certain. The real wires in this sheet's letters are: the low slack cable from the dead trunk in D (D 9–12), the cable looping from the ceiling in E (E 16–17), the service drop and a farther line in J, the hanging cables and the cable loop on the ground in K, and the leaning pole in L (its wires are invisible at night). A yard can still carry a service drop as a placement choice (L03); it is just not measured from C. (Open points: the two notes to fix.)

## Shared: village power and wire lines (L01–L05)

### What a village line is
- **The street line:** a 0.4 kV (0.38 kV) line on poles along one side of each street, 4–5 conductors (three phases, the neutral, often a street-lighting wire), and a **service drop** from a pole to every house. Since the ПУЕ revision the rule is that new lines up to 1 kV are built "as a rule" with self-supporting insulated wire, СІП (published: Ukrainian ПУЕ cl. 2.4.11, https://dnaop.com/html/41440_2.html ), so a village shows both: old bare wires on hooks and newer black SIP bundles.
- **The feeder:** a 10 kV line between villages along roads, belts and field edges, on taller poles with a crossarm and three pin insulators (L02 variant 2).
- **Wired broadcast and phone:** thin steel or bimetal wires hung on the same 0.38 kV poles below the power wires (published: the rules for hanging broadcast wires on 0.38 kV lines exist as a separate document, RD 34.20.515-91, https://meganorm.ru/mega_doc/norm/pravila/1/rd_34_20_515-91_pravila_ispolzovaniya_vozdushnykh_liniy.html ).
- **Pole types allowed for lines up to 1 kV:** reinforced concrete, wood, wood on reinforced-concrete stubs (приставки), metal; free-standing, braced (підкоси) or guyed (published: Ukrainian ПУЕ cl. 2.4.45–2.4.47, https://dnaop.com/html/41440_3.html ).

### Conductors (the L03 classes)
| Conductor | Diameter | Mass | Breaking force | L03 class | Source |
|---|---|---|---|---|---|
| Steel or bimetal broadcast or phone wire | 3–4 mm | 0.055 kg/m (3 mm) | — | thin | assumed diameter; mass derived (7850 kg/m³) |
| Bare aluminium A-16 | 5.1 mm | 0.043 kg/m | 3.7 kN | thin | published, ГОСТ 839 table, https://dnaop.com/html/45891_5.html |
| Bare aluminium A-25 | 6.4 mm | 0.068 kg/m | 5.4 kN | thin | published, same table |
| Bare aluminium A-35 | 7.5 mm | 0.094 kg/m | 7.4 kN | medium | published, same table |
| Bare aluminium A-50 | 9.0 mm | 0.135 kg/m | 10.7 kN | medium | published, same table |
| Steel-aluminium AC-35/6.2 and 50/8.0 (10 kV) | 8.4 and 9.6 mm | 0.148 and 0.195 kg/m | 13.5 and 17.1 kN | medium | published, same table |
| SIP service drop, 2 × 16 mm² | about 15 mm | about 0.13 kg/m | — | thick | derived: two 16 mm² aluminium cores of about 7.5 mm over their insulation, twisted; aluminium 43 g/m per core plus about 22 g/m of insulation per core |
| SIP main, 3 × 35 + 1 × 54.6 mm² | 26–32 mm | 0.55–0.61 kg/m | — | thick (above the list's 15–20 mm) | published, two datasheets: https://dn.ru/storage/certificates/ludinivikabel/dop_mat_sip2.pdf (26.2 mm) and https://storage.mtender.gov.md/get/d886941c-18a5-441a-9bfc-f1c23940c575-1679407095103 (32 mm) |

Minimum sections by the ПУЕ: SIP service drops 16 mm², SIP mains 25–35 mm², bare aluminium 25–35 mm² (published, cl. 2.4.13, https://dnaop.com/html/41440_2.html ). Old village lines still carry A-16 and steel wires below those minimums (assumed).

### Heights, spacing and spans
| Item | Value | Label |
|---|---|---|
| Bare wires above ground and the roadway, at maximum sag | ≥ 6.0 m (3.5 m in hard-to-reach terrain) | published, Ukrainian ПУЕ cl. 2.4.50, https://dnaop.com/html/41440_3.html |
| SIP above ground and the roadway, at maximum sag | ≥ 5.0 m (2.5 m hard-to-reach) | published, same clause |
| SIP over footpaths at a street crossing with service drops | ≥ 3.5 m | published, same clause |
| Service drop at the house entry | ≥ 2.75 m above ground | published, same clause |
| Service drop span | ≤ 25 m, else an extra pole | published, cl. 2.4.17, https://dnaop.com/html/41440_2.html |
| Spacing between bare wires, at the pole and in the span | ≥ 0.6 m for sag up to 1.2 m, more in proportion beyond (UA); 0.4 m in the older Soviet-lineage text for light-ice districts | published, cl. 2.4.24 (UA); cl. 2.4.29 of the chapter 2.4 text at https://legalacts.ru/doc/prikaz-minenergo-rf-ot-20052003-n-187/ |
| Bare wires to trees and bushes | ≥ 1 m each side (none required for SIP) | published, cl. 2.4.6 (UA) |
| Street line span | 30–45 m | assumed (a typical village span; not fixed by the ПУЕ, which computes it) |
| 10 kV feeder span | 50–80 m | assumed |
| Height of the top conductor at the pole | 6.5–7.5 m | derived: a 9.5 m pole buried 2.0–2.5 m, the top hook 0.2–0.4 m below the top |

### Sag, tension and sway (derived; the input to PHY-22)
Sag f = ratio × span, with an installed ratio of 1.5–3 % (assumed; it keeps a 7 m attachment above 6 m at mid-span). Tension T = w·L² / (8 f), w = mass × 9.81. The mid-span stiffness to a sideways push is k = 4T / L (knowledge.md §7.4's form). Wind blow-out angle θ = atan(q / w) with q = ½ ρ U² C_d D (ρ 1.2, C_d 1.2, U 10 m/s). The first sway mode is about f₁ = √(T/m) / (2L).

| Case | D | L | f | T | k mid-span | θ at 10 m/s, mid-span swing | f₁ |
|---|---|---|---|---|---|---|---|
| Broadcast steel wire | 3 mm | 40 m | 0.8 m | 136 N | 14 N/m | 22°, 0.30 m | 0.6 Hz |
| Bare A-35, street line | 7.5 mm | 40 m | 0.8 m | 230 N | 23 N/m | 30°, 0.40 m | 0.6 Hz |
| SIP drop 2 × 16 | 15 mm | 20 m | 0.4 m | 160 N | 32 N/m | 40°, 0.26 m | 0.9 Hz |
| SIP main 3 × 35 + 54.6 | 28 mm | 40 m | 1.0 m | 1140 N | 114 N/m | 20°, 0.33 m | 0.55 Hz |
| 10 kV AC-50/8 | 9.6 mm | 70 m | 1.2 m | 975 N | 56 N/m | 20°, 0.41 m | 0.5 Hz |

Every wire is far stronger than anything the drone can pull (3.7 kN for the weakest aluminium conductor, published above): a strike never breaks a live line; the drone wraps, deflects or is stopped (PHY-22), and only the fiber breaks.

### How lines fail in a shelled village (assumed from the blast order in houses-and-yard.md and the classes seen in D, J, K)
- **Conductors** are cut by fragments first: cut ends hang from the pole (L05-like) or the span drops across the street, a yard or the weeds (L04, D); SIP drops are torn off the house and hang from the eaves or the pole.
- **Poles** lean when a fallen span pulls them (5–15°, L), wooden poles splinter or burn at the base, concrete poles are crushed at 1–2 m and fold over on their bars, so the top lies on the ground or hangs at an angle.
- **Crossarms** twist or hang by one bolt; porcelain insulators shatter to a stub on the hook with shards below.
- Fragments scar wooden poles (10–80 mm gouges on one side) and spall concrete poles (20–150 mm, bars showing).

### Shared pole generator parameters (L01, L02)

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `pole_kind` | enum | wood_log, wood_on_stub, concrete_sv | per row | Construction, material, round or rectangular section |
| `length_m` | m | 8.5–11 | 9.5 | Overall length |
| `burial_m` | m | 1.5–2.5 | 2.0 | Depth in the ground; height above ground = length − burial |
| `top_section_m` | m | wood 0.14–0.20 diameter; concrete 0.165 × 0.165 | per kind | Top section |
| `butt_section_m` | m | wood 0.22–0.32 diameter; concrete 0.165 × 0.25–0.28 | per kind | Butt section; linear taper between |
| `stub_length_m`, `stub_above_m` | m | 3.3–4.3, 1.2–2.0 | 4.3, 1.6 | wood_on_stub: the concrete stub and how much of it shows |
| `bands` | int | 2–3 | 2 | Wire bands binding the log to the stub |
| `lean_deg`, `lean_azimuth_deg` | deg | 0–15, 0–360 | 0, 0 | Lean of the whole pole about its foot |
| `brace` | enum | none, single, a_frame | none | Brace logs |
| `brace_angle_deg`, `brace_attach` | deg, 0–1 | 15–35, 0.5–0.8 | 25, 0.65 | Brace angle from the pole and attachment as a fraction of the height above ground; sets the brace gap |
| `head` | enum | hooks_0p4, crossarm_0p4, sip_bracket, crossarm_10kv, none | per row | What carries the wires |
| `hooks` | int | 0–6 | 4 | Hooks with pin insulators, zig-zag down the pole |
| `crossarm_len_m` | m | 0.6–2.2 | by head | Crossarm length |
| `insulator` | enum | porcelain, glass, none | porcelain | Insulator material and colour |
| `lamp` | bool | — | false | A street-lamp bracket 1–1.5 m long with a fitting |
| `wire_ports` | list | — | from head | Attachment points written out for the L03 generator |
| `damage` | 0–1 | — | 0 / 0.5 / 1.0 | I below 0.25, D 0.25–0.8, X above 0.8; the items below default from it |
| `shrapnel_scars` | int | 0–40 | from damage | Wood: gouges 10–80 mm on one side; concrete: spalls 20–150 mm with bars showing |
| `broken_insulators` | 0–1 | — | from damage | Fraction shattered to a stub, shards at the foot |
| `crossarm_state` | enum | intact, twisted, hanging, gone | from damage | |
| `snap_height_m` | m | 0 or 0.3–3 | 0 | X: where the pole broke; wood splinters, concrete crushed with its bars bent |
| `snap_hang_deg` | deg | 0–90 | 60 | X concrete: the angle the top hangs at on its bars |
| `char_height_m` | m | 0–2 | 0 | Burnt base (wood) |
| `weathering` | 0–1 | — | 0.5 | Grey-silver checking (wood); streaks, lichen, rust stains (concrete) |
| `seed` | int | — | — | Every random choice |

Outputs: LOD0 with hooks, insulators and bands; LOD1 the pole, stub, brace and crossarm only (an insulator 0.075 m drops under 2 render px at about 35 m, derived from 935 px/rad); LOD2 a tapered prism of the pole alone, kept to at least 1 km so the night silhouette (L) never pops; collision, gaps, wind, snag and the `wire_ports` as below.

## L01 Wooden pole

**Seen in:** L 1–3 (a pole silhouette leaning about 9° with a short crossarm near the top; its material can't be read at night). H's pole reads as concrete and is L02's. J shows no pole, only a drop from one out of frame. **R** for the construction.
**Research sources:** pole types and braces (Ukrainian ПУЕ cl. 2.4.45–2.4.47, https://dnaop.com/html/41440_3.html ); reinforced-concrete stubs for wooden poles exist as standard products of the ПТ series (published listing, https://satu.kz/Pt-43.html ); pin insulator ТФ-20 108 mm high, 75 mm across (published table, https://dnaop.com/html/54346_2.html ).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Log length | 8.5–11 m (whole log); 6.5–8.5 m on a stub | 9.5 m; 7.5 m on a stub | assumed |
| Top / butt diameter | 0.14–0.20 / 0.22–0.32 m | 0.16 / 0.26 m | assumed; consistent with L 1 (3.5–4.7 feed px at 12–20 m is 0.16–0.35 m, derived) |
| Height above ground | 6.5–8.5 m | 7.5 m | derived (length − burial) |
| Concrete stub (ПТ series) | 3.3–4.3 m long, about 0.16–0.18 × 0.18–0.22 m, 1.2–2.0 m above ground | 4.3 m, 1.6 m | length from the ПТ-33/ПТ-43 designations (assumed reading of the name), section assumed |
| Log-to-stub overlap and bands | 1.4–2.0 m, 2 bands of 4–6 mm wire, 6–10 turns each | 1.6 m, 2 bands | assumed |
| Brace (підкос) | a log 0.14–0.20 m at 15–35° to the pole, attached at 0.5–0.8 of the height, foot on a buried sill | 0.16 m, 25°, 0.65 | assumed |
| Crossarm | a wooden bar 0.10 × 0.10 × 0.6–1.2 m or none (hooks screwed into the log) | none (hooks) | measured L 1: a short crossarm about 3–4 pole widths long, so 0.6–0.8 m; variant |
| Hooks and insulators | 4–5 hooks of 16–18 mm round bar in a zig-zag 0.4–0.6 m apart; porcelain pin insulators 0.108 m high, 0.075 m across | 4 hooks, 0.5 m | published (insulator, the bar size from the КН-16/18 designation); spacing from the ПУЕ table above |
| Lean | 0–15° | 0 (variant 2: 9°) | measured L 1–3 (8–10°, the asset list's 5–15°) |

### Construction and materials
- A pine log, pressure-treated (creosote dark brown when new, weathering to grey-silver with deep checking), set 1.5–2.5 m in the ground, or a shorter log banded to a concrete stub so the wood never touches the soil. Hooks screwed into the log in a zig-zag (or a short wooden crossarm), white porcelain or green-blue glass pin insulators, a grounding wire stapled down one side. Angle, end and leaning poles get a brace log on the side away from the pull.
- **Materials:**
  - log: Poly Haven `rough_wood` and `weathered_brown_planks` for the grey checked surface (candidates, CC0: https://polyhaven.com/a/rough_wood , https://polyhaven.com/a/weathered_brown_planks ); derived: a grain-along-the-pole map and a damage mask (R gouges, G char, B weathering streaks)
  - stub: Poly Haven `rough_concrete` (https://polyhaven.com/a/rough_concrete )
  - hooks, bands: Poly Haven `rusty_metal` (https://polyhaven.com/a/rusty_metal )
  - insulators: a plain glazed material, white or green-blue (no texture set needed)

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Weathered log, grey-silver | #8a8478 | — | assumed |
| Newer log, creosote | #4a3a2c | — | assumed |
| Concrete stub | #9a978e | — | assumed |
| Pole silhouette at night | — | #050505–#151513 against the sky #5d5e5d | measured L 1 (pole core and the sky beside it) |
| Insulators | white #d8d6cf or glass #6f9c95 | — | assumed |

### Variants and states
- Variant 1: straight log, hooks. Variant 2: leaning 5–15° with a brace (the asset-list variant; L 1–3 copies 9°). Variant 3: log on a concrete stub. Any variant takes the short crossarm (`head`).
- **I:** weathered, 0–3° lean, insulators whole, wires attached.
- **D:** 5–15° lean toward a fallen span, 5–20 fragment gouges on one side, 1–3 insulators broken, one or more conductors cut and hanging (L05) or down (L04).
- **X:** snapped at 0.3–2 m with a splintered break (splinters 0.1–0.5 m standing up, the V07 style) and the top lying in the direction of the pull with its wires; or burnt to a charred stump 0.5–1.5 m (the stub variant survives as a bare concrete post).

### Damage pattern
Fragments gouge the log on the blast side; a near miss snaps a dry log at the ground line or at the stub's top band, where the section changes. The fallen top keeps its hooks and drags its wires across whatever lies downhill of it. Around a broken pole: porcelain shards, splinters, cut wire ends.

### Physics role
- **Collision:** the log as 2–3 stacked capsules with their real radius tapering 0.13 → 0.08 m (within 10 mm of the visual), the stub as a box, the brace as a capsule, the crossarm as a box, each hook a capsule of radius 8–9 mm, each insulator a cylinder r 0.0375 m × 0.108 m. Material: `timber` (log, brace, crossarm), `concrete` (stub), `steel` (hooks, bands), `masonry` for porcelain until a ceramic id exists (glass insulators `glass`).
- **Gaps:** the triangle between the pole, the brace and the ground (variant 2): with the defaults (attachment 4.9 m up, 25°) the base is 2.3 m wide and the largest inscribed circle 1.78 m; the clear width passes the drone **at any yaw below about 3.0 m above the ground and nose-first up to about 3.3 m**, closed above (derived: clear width 2.28 · (1 − y / 4.9) − 0.18 m). The generator writes this triangle as a gap polygon. Between conductors: no gap is written; the wire contacts bound that space.
- **Wind volumes:** the pole a single solid part (porosity 0); it is far below the 2 m wind grid and the grid dilutes it (knowledge.md §10.3).
- **Snag hazards:** every hook (`hook`, radius 8 mm), the insulator necks (`edge`, radius 5 mm), the crossarm ends (`edge`, timber 2 mm), the bands on the stub (`edge`, 2 mm), splinters on a snapped pole (`hook`), the grounding wire (`edge`, 3 mm).
- **Landable surfaces:** none (a 0.16 m top).

### Placement rules
- Street lines: one side of the street, 0.5–1.5 m in from the road edge or along the fence line, every 30–45 m; brace or guy at every angle and at the line ends; a service drop from each pole or every other pole to the houses on both sides.
- Wood is the older class: on side streets, lanes and field edges; mixed with L02 along the main street (about one wooden pole in three, assumed).
- Field lines: in a row along a belt edge or a field road, 40–60 m apart, often leaning (L).
- Night landmarks: keep at least one pole on a skyline crest near each start point and along each field road (L): a pole is one of the few silhouettes readable at night.

### Generator parameters
Shared pole table, with these defaults: `pole_kind` wood_log, `length_m` 9.5, `top_section_m` 0.16, `butt_section_m` 0.26, `head` hooks_0p4, `hooks` 4. Variant 2: `lean_deg` 9, `brace` single. Variant 3: `pole_kind` wood_on_stub.

### Open points
- The L pole's crossarm and lean are copied; its material is unreadable at night. Settled at the L side-by-side, not by the pilot.
- A ceramic physics id for porcelain (hard, glazed, low friction, chips): physics sign-off; `masonry` until then.

## L02 Concrete pole with crossarm and insulators

**Seen in:** H 1–4 (a straight grey pole passing 3–4 m to the right: two flat faces meeting at a sharp vertical arris, a clear taper from the butt, and a round through-hole on the lit face at mid-frame, H 2–3; so a vibrated rectangular СВ-type pole, not a round log). O shows a lamp post with a curved arm (the urban class). **R** for the head.
**Research sources:** the СВ-95 pole: 9.5 m long, 0.36 m³, 0.9 t, for lines from 0.4 kV (published, https://productcenter.ru/products/124963/stoika-sv95-3 ); vibrated stands of 9.5 and 10.5 m for 0.38 kV SIP lines (published registry abstract, https://nrat.ukrintei.ua/searchdoc/0305U001101 ); ПУЕ clauses above.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Length | 9.5 m (СВ-95), 10.5–11 m (СВ-105, СВ-110 for 10 kV and crossings) | 9.5 m | published |
| Section | 0.165 × 0.165 m at the top to 0.165 × 0.25–0.28 m at the butt | 0.165 × 0.165 → 0.165 × 0.28 m | assumed; consistent with the published 0.36 m³ over 9.5 m (derived mean section 0.165 × 0.23 m); measured H 1–3: the visible width ratio top to bottom is about 0.8 over the frame height |
| Height above ground | 7.0–7.5 m (9.5 m pole) | 7.3 m | derived (2.0–2.5 m burial, assumed) |
| Through-holes | round, 20–25 mm, along the pole at intervals for bolts and grounding | 25 mm every 0.5 m in the top 2 m, one at 3.5 m | assumed; measured H 3: one hole at mid-frame |
| 0.4 kV head (variant 1) | a steel angle crossarm 63 × 63 × 6 mm, 0.8–1.2 m, with 4–5 pin insulators; or a SIP bracket with an anchor clamp 0.3–0.5 m below the top | crossarm 1.0 m, 4 insulators | assumed; insulator size published (L01) |
| 10 kV head (variant 2) | a steel crossarm 1.5–2.2 m at 0.3 m below the top, three pin insulators about 0.14 m across and 0.13 m high (two on the arm, one on a top pin) | 1.8 m | assumed |
| Street lamp | a 1.0–1.5 m bracket at 6–6.5 m on every 2nd–3rd pole | none | assumed; O shows the urban form |
| Lean | 0–15° | 0 (variant 2 of the asset list: leaning 8°) | asset list; measured L 1 for the magnitude |

### Construction and materials
- Vibrated reinforced concrete (B30, published), cast as a tapered rectangular stand with longitudinal bars, through-holes for the crossarm bolts and the grounding conductor, a grounding wire or strip down one face. Steel angle crossarms bolted through, pin insulators on steel pins or hooks, SIP fittings on a bracket. Grey with rust streaks below every steel part, lichen on the shaded face, chipped arrises at the foot.
- **Materials:** Poly Haven `concrete_wall_003` or `rough_concrete` with a derived taper-aligned map and a streak mask (rust below fittings, lichen, chips) (candidates, CC0: https://polyhaven.com/a/concrete_wall_003 , https://polyhaven.com/a/rough_concrete ); crossarm and pins `rusty_metal_02` (https://polyhaven.com/a/rusty_metal_02 ); insulators plain glazed.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Pole, weathered concrete | #9b9890 | shaded face #4a4640, lit face #5b5c5a, against the overcast sky #e3e4e2 | assumed albedo; measured H 2–3 |
| Rust streaks under fittings | #7a5a42 | — | assumed |
| Crossarm, rusted steel | #5e4a3a | — | assumed |

### Variants and states
- Variant 1: 0.4 kV (crossarm or SIP bracket, optional lamp). Variant 2: 10 kV crossarm with three pin insulators. Each straight or leaning (the asset-list pair).
- **I:** streaked, upright.
- **D:** leaning 5–15° toward a fallen span; spalls on the blast side with bars showing; the crossarm twisted; insulators broken; wires cut.
- **X:** crushed at 0.5–2 m: the concrete spalled off over 0.3–0.6 m, the bars bent, the top folded over and hanging at 30–90° or lying with its head on the ground; or snapped clean at the ground with the stump standing 0.2–0.5 m.

### Damage pattern
Concrete does not splinter: it spalls in flakes 20–150 mm and crushes at the break, leaving 6–10 longitudinal bars of 10–14 mm (assumed) bent through the gap. The folded pole is the characteristic shape: an arch of bars a drone can fly into. Fragments: grey concrete chips at the foot, porcelain shards, a twisted crossarm.

### Physics role
- **Collision:** the stand as 2–3 stacked boxes following the taper within 10 mm (`concrete`, its 3 mm arris is the fiber's bend edge); the crossarm a box (`steel`); pins and hooks capsules (`steel`); insulators cylinders (`masonry` until a ceramic id); the lamp bracket a capsule (`steel`) and the fitting a box. X state: the two pole pieces as boxes and every exposed bar a capsule with its real radius (`steel`).
- **Gaps:** none intact; in X the space under a folded-over pole is open where it is ≥ 0.574 m (generated from the actual fold, written as a polygon if it is a fly-under).
- **Wind volumes:** one solid part, porosity 0 (as L01).
- **Snag hazards:** crossarm ends (`edge`, 1 mm, steel angle), pins and hooks (`hook`), insulator necks (`edge`, 5 mm), the lamp bracket (`edge`), the grounding strip, exposed bars at spalls and breaks (`hook`, radius 5–7 mm).
- **Landable surfaces:** none (a 0.165 m top, an angle-iron crossarm).

### Placement rules
- The main street's line, 30–45 m apart, the lamp on every 2nd–3rd pole; the 10 kV feeder along the road out of the village and along belt edges and field roads (H), 50–80 m apart, with a pole-mounted transformer (P05, the urban-edge sheet) at the village end.
- Mixed with L01 on the same street where a line was repaired pole by pole.

### Generator parameters
Shared pole table, with these defaults: `pole_kind` concrete_sv, `length_m` 9.5 (variant 2: 10.5), `top_section_m` 0.165 × 0.165, `butt_section_m` 0.165 × 0.28, `head` crossarm_0p4 (variant 2: crossarm_10kv).

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `holes` | list | 0–8 heights | top 2 m at 0.5 m, one at 3.5 m | Through-holes (real geometry, 25 mm) |
| `bars_exposed` | int | 0–10 | from damage | Bars drawn at spalls and the break |

### Open points
- The H pole's head is out of frame (only its lower and middle part shows): whether it carried a 0.4 kV or a 10 kV head along that belt is left to placement. Settled at the H side-by-side.

## L03 Wires (generator: catenary between poles, sag)

**Seen in:** J (a service drop from a pole out of frame to the left house's roof, nearly straight over its visible length, 1–2 px wide; a fainter line crossing behind the right house; a low wire leaving the left house toward the left edge); D 9–12 (the slack cable, L04's); K (thin lines across the sky beyond the block). Not in C (see the correction above).
**Research sources:** the conductor, height, spacing and span tables above (ГОСТ 839; the SIP datasheets; Ukrainian ПУЕ cl. 2.4.13–2.4.50).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Thin class | 3–6 mm | 5 mm | published conductors (A-16 5.1 mm, A-25 6.4 mm); assumed 3–4 mm steel wire |
| Medium class | 7–12 mm | 9 mm | published (A-35 7.5, A-50 9.0, AC 8.4–9.6 mm) |
| Thick class | 13–32 mm | 20 mm (drop 15 mm, main 28 mm) | derived and published (SIP 2 × 16 about 15 mm; SIP mains 26–32 mm) |
| Conductors per span | bare street line 4–5; SIP 1 bundle; 10 kV 3; broadcast 1–2 | 4 | assumed |
| Vertical spacing, bare | 0.4–0.6 m | 0.5 m | published minimums (above) |
| Span | 10–25 m (drops), 30–45 m (street), 50–80 m (feeder) | 40 m | published (drop ≤ 25 m), assumed (others) |
| Sag | 1.5–3 % of the span intact; 5–30 % on a damaged line | 2 % | assumed; checked against the 6 m clearance (derived) |
| Height at the pole | 5.5–7.5 m (street), 2.75–4 m at the house end of a drop | 7.0 m top, 0.5 m steps down | published minimum at the house; derived at the pole |
| SIP twist pitch | 0.3–0.6 m | 0.45 m | assumed |

### Construction and materials
- Bare aluminium: stranded (7 strands), dull grey, oxidised white at the ties. Steel broadcast wire: single strand, rusty brown. SIP: 2–4 black XLPE-insulated aluminium cores twisted around each other (mains around a bare or insulated neutral carrier), hung by clamps.
- **Materials:** bare aluminium: Poly Haven `metal_plate` tinted grey with a derived stranding normal along the wire (https://polyhaven.com/a/metal_plate ); steel: `rusty_metal` (https://polyhaven.com/a/rusty_metal ); SIP: a plain black rough plastic material (no set needed). Every wire is a tube mesh of its real diameter (REAL-05): never a line shader standing in for geometry.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Bare aluminium, weathered | #8e9193 | — | assumed |
| Steel wire, rusted | #5e4a3a | — | assumed |
| SIP bundle | #1f2022 | — | assumed |
| A drop against an overcast sky | — | darkest #afb5be against #d6dfe7 (1–2 px, photo) | measured J |
| A slack cable against backlit haze | — | #1e1816 against #adaaa4 | measured D 12 |

### Variants and states
- The three classes (thin, medium, thick) as presets of one generator; each takes any conductor type, count and arrangement.
- **States** per conductor: intact; cut once (both halves hang from their poles as L05 free ends, or one end drops to the ground as L04); down (the whole span lies on the ground, fences and shrubs, L04).

### Damage pattern
See "How lines fail" above. A cut aluminium conductor unravels at the end (strands splayed 50–200 mm); a SIP drop torn from a house leaves its bracket on the wall and the bundle hanging from the pole.

### Physics role
- **Map objects, not catalog shapes (D-007, knowledge.md §10.1):** the generator writes each conductor as a point list with its diameter and sag into the map's `objects.json`; the world query sees it as a wire (`StaticContacts`, `Geometry` with span, sag and diameter). The visual tube mesh is built from the same points.
- **Material id:** `cable`, edge radius = the wire's own radius.
- **Strike model (PHY-22):** diameter, mass per metre and the derived tension and mid-span stiffness per span (table above) are the inputs; the generator writes D, mass and sag, the physics derives T and k.
- **Gaps:** none.
- **Wind volumes:** none (wires are not wind obstacles, knowledge.md §10.3); the wire itself sways in wind (θ and f₁ above): physics-engineer's decision whether the strike model moves the wire with the wind (open point).
- **Snag hazards:** the whole wire, for props, legs and the fiber; the fiber dragged over a wire wraps a capstan of the wire's radius (`cable` μ 0.30).
- **Landable surfaces:** none.

### Placement rules
- From the poles' `wire_ports`, pole to pole, and from a pole to each house (a roof bracket or a wall hook at ≥ 2.75 m, one drop per house, ≤ 25 m, crossing the street diagonally).
- Spans run straight between poles; at an angle pole the line turns.
- Damage per span follows the street's damage level: in a D street 20–50 % of spans have a conductor cut, 5–20 % are down (assumed).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `class` | enum | thin, medium, thick | medium | Preset for diameter, mass and colour |
| `diameter_mm` | mm | 3–32 | 5 / 9 / 20 by class | Tube radius; the PHY-22 input |
| `mass_kg_per_m` | kg/m | 0.04–0.62 | by type | Sag–tension relation |
| `conductor_type` | enum | steel_wire, bare_al, acsr, sip_drop, sip_main, insulated | by class | Surface, colour, mass |
| `conductors` | int | 1–5 | 4 (bare), 1 (SIP) | Wires per span |
| `arrangement` | enum | vertical_zigzag, horizontal, top_triangle, bundle | from the pole head | Positions at the pole |
| `spacing_m` | m | 0.4–1.0 | 0.5 | Between conductors |
| `span_m` | m | 10–90 | from placement | Pole to pole |
| `sag_ratio` | — | 0.005–0.3 | 0.02 | Sag = ratio × span; above 0.05 only on damaged spans |
| `attach_heights_m` | m, m | 2.75–8 | from the poles or the house | The two ends |
| `twist_pitch_m` | m | 0.3–0.6 | 0.45 | SIP bundle twist (visual) |
| `damage` | enum per conductor | intact, cut, down | intact | Cut ends hang (L05 form) or drop (L04 form) |
| `points_per_m` | 1/m | 1–4 | 2 | Points written to the map per metre |
| `seed` | int | — | — | Cut positions, strand splay |

### Open points
- The asset list's thick class is 15–20 mm; a SIP main is 26–32 mm. Proposed: the thick class covers 13–32 mm (the generator takes the real diameter anyway). Orchestrator decision.
- Whether the strike model moves the wire with the wind (sway 0.3–0.4 m at mid-span in a 10 m/s wind, 0.5–0.9 Hz): physics engineer, phase 7d.
- Rendering sub-pixel wires (tech-artist): coverage-weighted thin lines so a wire fades as 270·D / d and never shimmers or pops; never a minimum width that makes it visible.

## L04 Fallen or sagging wire across a yard

**Seen in:** D 9–12 (a dark cable leaving the dead trunk at about 1.5–2.5 m and arcing right over the shrubs, smooth and even in thickness over about 6–10 m of its visible length, highest over the shrubs and dropping toward the trunk and into the weeds: a slack line draped over the vegetation, not a tensioned span).
**Research sources:** none beyond the L03 tables.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Diameter | 3–15 mm | 8 mm | assumed (a drop or a bare conductor; D's line is a few pixels on the upscaled stills, read as thin to medium) |
| Length across the yard | 5–25 m | 12 m | assumed; measured D 9–12 for the visible 6–10 m |
| High end | 0.5–6 m (a trunk, a pole, a house eave, a fence post) | 2.0 m | measured D 9–12 (1.5–2.5 m at the trunk) |
| Height over the yard | 0–2.5 m, resting on shrubs, fence tops, weeds | 0.5–2 m | measured D 9–12; terrain.md S1 |
| Slack | 5–40 % more length than the straight line | 15 % | assumed |
| Loops off the ground | 0–3 loops 0.1–0.5 m high | 1 | assumed |

### Construction and materials
- One conductor (L03 surface by type) with no tension: it follows what it rests on. Variant 1 drapes over shrubs and a fence from a high end (D); variant 2 lies mostly on the ground in the weeds with loops and one end rising to a broken pole or a house.
- **Materials:** as L03.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Cable in backlight | as L03 by type | #1e1816 against #adaaa4 | measured D 12 |

### Variants and states
- 2 variants (the asset list): draped (D), down. No damage states: it is itself a damage state of L03.

### Damage pattern
The end of a cut span or a torn drop; the far end may still be live on its pole (the wire runs from height down to the yard), or both ends lie loose.

### Physics role
- **Collision:** inside an asset or the yard kit it is a capsule chain at the wire's radius, segments ≤ 0.5 m, laid over its supports by a relaxation in the generator (the points rest on the collision of the shrubs, fence and ground under them). As a map object it is a wire with its points (L03) and zero tension.
- **Material id:** `cable`.
- **Strike (PHY-22):** with no tension the line has no stiffness of its own; whatever its diameter it is pulled into the props and wraps, then drags its supports (shrubs, a fence) or lifts off them. The physics engineer may want the support points as anchors with a release force.
- **Gaps:** none; where it sags 0.6–1.2 m over a clearing the space under it is open air (passable when the clear height is ≥ 0.55 m plus the wire's sway).
- **Wind volumes:** none.
- **Snag hazards:** the whole line, at the height of the weeds: exactly the low flight of D.
- **Landable surfaces:** none.

### Placement rules
- In D-state and X-state yards: from a drop's house end or a broken pole across the yard, over shrubs, fences and the weeds (V17); 0–2 per damaged yard, 1 per 3–5 yards in a damaged street (assumed).
- Always where the low flight path crosses a yard: it is the main surprise hazard of the set (terrain.md S1).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `variant` | enum | draped, down | draped | Shape |
| `diameter_mm` | mm | 3–15 | 8 | Radius, class preset |
| `high_end` | point | 0.5–6 m up | 2.0 m | The attached end |
| `low_end` | point or free | — | free, on the ground | The other end |
| `slack` | 0–0.4 | — | 0.15 | Extra length over the straight line |
| `loops` | int | 0–3 | 1 | Loops lifted off the ground |
| `relax_iterations` | int | 50–500 | 200 | Drape relaxation over the supports |
| `seed` | int | — | — | Loop positions, end splay |

### Open points
- Whether a support point (a shrub branch, a fence post top) holds the wire with a release force or is a frictional contact only: physics engineer.

## L05 Hanging cables (in ruins)

**Seen in:** E 16–17 (one pale cable looping from the plank ceiling into the room, a drop of about 0.5–1 m); K (pale cables hanging diagonally 1–3 m from slab edges across the open floors, and a loop of pale cable about 0.5–0.8 m across lying on the trampled ground in front of the mound).
**Research sources:** none needed beyond L03; interior cable classes assumed (flat two-core aluminium wiring, round cable, TV coax, phone pairs) and the SIP drop (L03).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Diameter | 3–15 mm (phone pair 3–4, coax 6–7, round power 8–12, flat twin 4 × 8, SIP drop 15) | 7 mm | assumed |
| Loop (both ends fixed) | span 0.5–3 m, drop 0.3–1.5 m | 1.5 m span, 0.8 m drop | measured E 16–17 (0.5–1 m drop) |
| Free end | 0.5–3 m long, the tip 0–0.5 m above the floor or lying on it | 1.5 m | assumed |
| Diagonal (between two edges) | 1–4 m | 2.5 m | measured K (1–3 m between slab edges) |
| Coil on the ground | 0.4–1.0 m across | 0.6 m | measured K |

### Construction and materials
- House wiring torn from its clips or rollers, TV and phone cables, a SIP drop ripped off the eaves; white, grey or black insulation, bare copper or aluminium at the torn ends.
- **Materials:** plain insulation materials by type (white, grey, black); copper ends a plain metal.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Pale cable inside a dark room | white-grey #c9c7c0 | #808080 against #535253 | assumed albedo; measured E 17 |
| Pale cables at the slab edges | #c9c7c0 | — | measured by eye on K (thin, not sampled) |

### Variants and states
- 2 variants (the asset list): loop, free end; the diagonal and the ground coil are the same generator with other end conditions.

### Damage pattern
A cable hangs wherever a ceiling, slab edge or eave was torn open: one per room with a ceiling down (H01 `hanging_cables`), several per open floor of a block (U01).

### Physics role
- **Collision:** a capsule chain at the cable's radius, segments ≤ 0.2 m (they are short), `cable`.
- **Snag:** a loop is an `edge` polyline with its radius; a free end is a `hook` from the attachment to the tip; a coil on the ground is an `edge` lying on the support top.
- **Strike (PHY-22):** a free end has no tension at all and wraps into the props; a loop has only its own weight (T ≈ w·L² / 8f, a few newtons): thin-class behaviour whatever the diameter. Phase 1 builds it static; phase 7d decides whether it becomes a dynamic rope (physics engineer).
- **Gaps:** none of its own; a cable hanging in a doorway or a window shrinks that opening's clear polygon only if the asset's gap generator includes it (open point).
- **Wind volumes:** none.
- **Landable surfaces:** none.

### Placement rules
- Inside D- and X-state houses (from the ceiling), at the eaves of houses whose drop was torn off, at the slab edges and window heads of the block (U01), lying on rubble and on trampled ground near it (K). Part of K10's hanging kit.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `form` | enum | loop, free_end, diagonal, coil | loop | End conditions |
| `type` | enum | phone_pair, coax, round, flat_twin, sip_drop | round | Diameter, cross-section shape, colour |
| `diameter_mm` | mm | 3–15 | by type | Radius |
| `length_m` | m | 0.3–4 | 1.5 | Cable length |
| `attach` | points | 1–2 | from the host asset | Where it hangs from |
| `tip_height_m` | m | 0–0.5 | 0.2 | Free end above the floor |
| `seed` | int | — | — | Curl, end splay |

### Open points
- Whether a cable hanging across an opening is written into that opening's gap polygon (it narrows a doorway the drone could otherwise pass): world-builder and physics engineer; the cautious default is yes for diagonals, no for loops above head height.

## Shared: roads (R01–R03 roads)

- **The surfaces belong to the surfaces sheet:** `asphalt` (S11), `concrete_slab` (S12) and `dirt_track` (S13) are rows of the surface table (terrain.md §8; the earthworks-and-surfaces sheet). The road kits here are the **geometry and features** laid on them: the road outline, crossfall, edges, potholes, joints, ruts, crater rings, plates.
- **What is ground and what is an object:** whatever the heightfield and the surface layers can hold (crossfall, the carriageway outline, ruts as sub-grid relief, potholes as pitfall features) is written into the map by the map compiler; whatever is a separate solid (a concrete plate, a lifted asphalt slab, a kerb stone) is a catalog asset with its own collision. The road kits produce both, from one parameter set.
- **Landing:** roads are the flattest, most landable ground on the map; their realism is in where they are not flat.

## R01 (road) Asphalt road kit: straight, curve, junction; potholes, crater decals, cracked edges

**Seen in:** M (a pale, dusty, worn asphalt street with fine dark cracks, scattered gravel and twigs, a lighter patched area, a shallow irregular depression holding water behind the car, a kerb with a painted face at the back); O (dark wet asphalt under slush, snow banks on both sides, dark debris on the carriageway). No asphalt appears in any clip (terrain.md §9).
**Research sources:** lane widths 3.0 m on main village streets and roads and 2.75 m on local streets (published: ДБН В.2.3-5:2018 as reported at https://www.kmu.gov.ua/news/v-ukrayini-vzhe-mozhna-zvuzhuvati-dorogi-pri-yih-rekonstrukciyi-ta-budivnictvi-lev-parchaladze ); a maintained road may not carry a hole deeper than 4 cm (higher categories) or 6 cm (the rest) (published: ДСТУ 3587 cl. 3.1.2, https://dnaop.com/html/60501/doc-%D0%94%D0%A1%D0%A2%D0%A3_3587-97 ), so the holes of an unmaintained frontline road are deeper.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Carriageway, main village street | 5.5–7.0 m | 6.0 m | published (2 × 3.0 m lanes); older streets narrower (assumed) |
| Carriageway, side street | 3.5–5.5 m | 4.5 m | published 2.75 m lanes; single-lane assumed |
| Shoulder (soil and grass) | 0.5–2.0 m | 1.0 m | assumed |
| Asphalt layer | 40–80 mm | 50 mm | assumed (light-duty village pavement) |
| Crushed-stone base | 150–250 mm | 200 mm | assumed |
| Crossfall | 1.5–3 % | 2 % | assumed |
| Edge drop to the shoulder | 0–100 mm | 50 mm | derived (the layer thickness where the shoulder has eroded) |
| Ravelled edge | 0.1–0.6 m into the lane, broken into plates 0.1–0.4 m | 0.3 m | assumed; M shows a broken edge at the verge |
| Patches | 0.5–3 m rectangles, 5–20 % of the area, a shade darker or lighter | 10 % | measured M (a lighter patched area), sizes assumed |
| Cracks | 2–20 mm wide, in cells of 0.3–1.5 m and along the edges | — | measured M (fine dark cracks), sizes assumed |
| Potholes | 0.2–2.0 m across, 40–150 mm deep, often holding water | 0.6 m, 70 mm | published threshold (above); measured M: a shallow depression about 1.3 m across the view and 1.5–2.2 m along it (scaled from the 0.59 m wheel: about 2.2 wheel widths) |
| Crater in asphalt (with the E05 crater) | 1–4 m across, the asphalt lifted in slabs 0.2–0.8 m around the rim, radial cracks to 1–3 radii | 2 m | E05 in the earthworks sheet; ring assumed |
| Fragment pits | 10–60 mm across, 5–30 mm deep, within 5–15 m of a crater | — | assumed |
| Kerb (urban edge only) | stones 0.15 × 0.30 × 1.0 m, 0.10–0.15 m above the carriageway | — | measured M (a kerb at the back, urban); village streets have none (assumed) |
| Kit pieces | straight 10 and 20 m; curves of radius 15–40 m; T and X junctions; an end that crumbles into a dirt track (R03) | — | asset list |

### Construction and materials
- Hot asphalt on crushed stone on the compacted soil; no kerbs in the village, grass and soil shoulders, a ditch on one or both sides on the road out of the village. Worn pale and dusty in dry weather (M), dark and wet under slush in winter (O); dust, mud, gravel, twigs and glass crumbs on it.
- **Materials:** Poly Haven `worn_asphalt`, `road_damaged`, `road_damaged_2`, `asphalt_snow` (the O state), `gravel_road` for the shoulders (candidates, CC0: https://polyhaven.com/a/worn_asphalt , https://polyhaven.com/a/road_damaged , https://polyhaven.com/a/road_damaged_2 , https://polyhaven.com/a/asphalt_snow , https://polyhaven.com/a/gravel_road ); ambientCG `Road007` (https://ambientcg.com/view?id=Road007 ). Derived: one road map per kit piece in asset space plus a damage mask (R crack and ravel depth, G scorch, B dust and mud, A wetness).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Dusty asphalt, dry, near | #b2aca2 | — (no clip) | measured M |
| Dusty asphalt, dry, mid-distance | #bdbab5 | — | measured M; terrain.md #b7b2aa |
| Water in a depression | #919094 | — | measured M |
| Wet asphalt under slush | #59595e | — | measured O; terrain.md #525256 |
| Snow on the verge | #cbd3e0 | — | measured O; terrain.md #ccd4e1 |

### Variants and states
- Kit pieces as listed; any piece takes a state and a weather.
- **I:** worn, patched, cracked, dusty, the edges ravelled.
- **D:** potholes, edge breaks, fragment pits, mud from tracks, branches and glass on the road.
- **X:** a crater every 20–100 m of road with its ring of lifted slabs and ejecta (crushed stone and soil, darker), scorch, debris across the carriageway (K-kit, vehicle debris).
- **Weather:** dry (M), wet, slush and snow (O).

### Damage pattern
Asphalt fails by cracking (fatigue and frost), then the cracked cells lose their stone where water stands and become potholes; the unsupported edge crumbles into plates. A shell bursting on the road throws out the asphalt and its base, lifts the layer in slabs around the rim and cracks it radially; tracked vehicles scuff it (decal only); fragments pit it around craters.

### Physics role
- **Collision:** the carriageway is ground: the `asphalt` surface over the heightfield with the crossfall and the edge drop in the relief; each pothole is a pitfall feature (outline, depth, wall slope) written into the map; each crater ring's lifted slabs are catalog debris pieces (boxes, 50 mm thick, tilted 10–40°). Kerb stones are boxes (`concrete`).
- **Material ids:** the ground uses the S11 surface row; lifted slabs `proposed:asphalt` (else `concrete`); kerbs `concrete`.
- **Gaps:** none.
- **Wind volumes:** none.
- **Snag hazards:** the edges of lifted slabs (`edge`, radius 3 mm); nothing on intact asphalt.
- **Landable surfaces:** the carriageway (rigid, flat to 2 %). A pothole under one leg tilts the drone by atan(depth / 0.24): 16° for a 70 mm hole (derived); a 0.6 m hole can take two legs. A water-filled hole looks like flat road on the feed (M).

### Placement rules
- One asphalt main street through each village, joining the next village; side streets are dirt or gravel (R03); the asphalt ends in a crumbled edge where it meets a field road.
- Houses 5–15 m back behind their fences; L01/L02 poles along one side; abandoned and destroyed vehicles on and beside it (the vehicles sheet), with K07 debris around them; damage concentrated near the village centre and the junctions (assumed).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `piece` | enum | straight, curve, t_junction, x_junction, end_to_dirt | straight | Kit piece |
| `length_m` | m | 10–200 | 20 | Straight length (the map tiles longer runs) |
| `curve_radius_m` | m | 15–40 | 25 | Curve |
| `width_m` | m | 3.5–7.0 | 6.0 | Carriageway |
| `shoulder_m` | m | 0.5–2.0 | 1.0 | Shoulders |
| `crossfall` | — | 0.015–0.03 | 0.02 | Camber |
| `layer_mm` | mm | 40–80 | 50 | Edge drop and slab thickness |
| `edge_break_m` | m | 0–0.6 | 0.3 | Ravelled edge width |
| `patch_fraction` | 0–1 | 0–0.3 | 0.1 | Patches |
| `crack_density` | 0–1 | — | 0.4 | Crack network (decal and normal; cracks have no physical role below 20 mm) |
| `potholes_per_100m2` | — | 0–5 | 0.5 (I), 2 (D) | Pitfall features |
| `pothole_size_m`, `pothole_depth_mm` | m, mm | 0.2–2.0, 40–150 | 0.6, 70 | |
| `pothole_water` | 0–1 | — | 0.3 | Fraction holding water (a flat reflective top) |
| `crater_spacing_m` | m | 0 or 20–100 | 0 (X: 50) | Craters with slab rings (with E05) |
| `debris_density` | 0–1 | — | from the state | Branches, glass, K-kit pieces on the road |
| `wetness`, `snow` | 0–1 | — | 0, 0 | Weather state (O: 0.8, 0.6) |
| `kerb` | bool | — | false | Urban kerb |
| `seed` | int | — | — | |

### Open points
- `proposed:asphalt` for lifted slabs and broken edges (friction near `concrete`, edge radius 3–5 mm from the aggregate): physics sign-off.
- Pitfall features for potholes and the sub-grid relief for crossfall and edges: world-builder (map format).

## R02 (road) Concrete road plates (about 6 × 2 m), laid as a track

**Seen in:** **R**; the pilot's own description of concrete-slab tracks crossing a field (PR-8, a clip that is not a lettered reference).
**Research sources:** the plate standard (published: ГОСТ 25912 technical requirements, https://dnaop.com/html/59443_2.html ); the ПАГ-14 plate 6000 × 2000 × 140 mm (published listing, https://productcenter.ru/products/92973/pag-14 ).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Plate | 6.0 × 2.0 m × 0.14 m (ПАГ-14) or 0.18 m (ПАГ-18); a smooth ПДН of the same plan | 6.0 × 2.0 × 0.14 m | published |
| Surface | rhombic corrugation ≥ 1.5 mm deep, the long diagonal across the plate | — | published |
| Grip recesses | 20 × 100 × 200 mm on the long edges | 2 per long edge | published |
| Joint steel items | steel plates or loops at the edges, set ≥ 1.5 m from the ends (ПАГ-18) | 2 per long edge | published |
| Bars | prestressed, 12 mm (ПАГ-14) | — | published |
| Mass | about 4.1 t | — | derived (6 × 2 × 0.14 m × 2450 kg/m³) |
| Joint gap | 0.02–0.30 m | 0.06 m | assumed (plates laid loose on sand, not welded) |
| Step between plates | 0–80 mm | 20 mm | assumed (settlement) |
| Tilt | 0–4° | 1° | assumed |
| Missing plates | 0–10 % | 3 % | assumed |
| Track width | 2 m (one row lengthwise), 4 m (two rows), 6 m (plates crosswise) | 4 m | assumed |

### Construction and materials
- Precast prestressed concrete plates dropped onto a sand bed by a crane and left unwelded: the joints open, the plates settle unevenly and rock, weeds and soil fill the joints and the edges, mud and straw from field machinery cover the plates. Broken plates crack across with bars showing; chipped arrises.
- **Materials:** Poly Haven `concrete_floor_worn_001`, `grooved_concrete_driveway` (for a corrugated top), `concrete_moss` (candidates, CC0: https://polyhaven.com/a/concrete_floor_worn_001 , https://polyhaven.com/a/grooved_concrete_driveway , https://polyhaven.com/a/concrete_moss ); derived: a per-plate map with a rhombic normal, joint dirt and edge chips (mask: R chips, G soil, B straw and mud, A wet).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Plate, weathered | #9e9b93 | — | assumed |
| Soil and straw in the joints | as S13 / S04 | — | the surfaces sheet |

### Variants and states
- The kit: single plate, broken plate (2–3 pieces), the track layouts (one row, two rows, crosswise), a turn (plates fanned), an end where the track meets a road.
- **I:** settled, joints 2–10 cm, weeds in the joints. **D:** gaps to 30 cm, steps to 8 cm, a plate missing, a crater beside the track tipping 1–3 plates. **X:** plates broken and thrown, a crater in the track (E05).

### Damage pattern
Plates rarely break in place; a burst beside the track tips and lifts plates, a burst on it breaks one across with bars bent out. Field traffic pushes plates apart and dumps soil on them.

### Physics role
- **Collision:** each plate a box 6 × 2 × 0.14 m, landable, `concrete`; a broken plate 2–3 boxes plus its bars as capsules r 6 mm (`steel`). The joints are real: the gap between two boxes lets a leg drop to the sand bed 0.14 m below.
- **Gaps:** none to fly through.
- **Wind volumes:** none (at ground level).
- **Snag hazards:** the joint steel items (`hook`, radius 6 mm, 0–30 mm proud), bars of broken plates (`hook`), weeds in the joints (micro-detail).
- **Landable surfaces:** every plate top: the flattest hard ground in the fields. Hard cases (derived): a leg on a step of h tilts the drone atan(h / 0.24), 18° for 80 mm; a leg in a 6 cm joint wedges; a joint ≥ 0.1 m swallows a leg foot and tilts the drone toward it.

### Placement rules
- Field tracks from each village to the farm complex (P02), across fields and along belts (PR-8); 200–2000 m long, following the relief; weeds 0.2–0.8 m along both edges; vehicles' mud on the plates near the field gates.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `plate_type` | enum | pag14, pag18, pdn | pag14 | Thickness, surface |
| `layout` | enum | single_row, double_row, crosswise | double_row | Track width and plate orientation |
| `length_m` | m | 10–2000 | — | From the map |
| `joint_gap_m` | range | 0.02–0.30 | 0.02–0.10 | Gap per joint |
| `step_mm` | range | 0–80 | 0–30 | Height step per joint |
| `tilt_deg` | range | 0–4 | 0–1.5 | Tilt per plate |
| `missing` | 0–1 | 0–0.1 | 0.03 | Missing plates |
| `broken` | 0–1 | 0–0.2 | 0.02 | Broken plates |
| `soil_cover` | 0–1 | — | 0.3 | Mud and straw on the plates (decal) |
| `joint_weeds` | 0–1 | — | 0.6 | Weed density in the joints (micro-detail) |
| `seed` | int | — | — | |

### Open points
- Which layout dominates locally is not seen in any letter; the generator holds all three and the world uses double_row as the default. Settled by the world review, not by the pilot.

## R03 (road) Dirt track ruts (decal plus surface)

**Seen in:** L (faint field tracks at night, too dark to measure; terrain.md §9); **R**.
**Research sources:** none; vehicle tracks are in the vehicles sheet (published specs there).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Track width overall | 2.5–3.5 m | 3.0 m | assumed |
| Rut centre spacing | 1.3–2.0 m | 1.6 m | assumed (car tracks about 1.3–1.4 m, trucks and tractors 1.8–2.0 m; the vehicles sheet holds the published values) |
| Rut width | 0.25–0.50 m | 0.35 m | assumed (a tyre 0.17–0.40 m wide, widened by wear) |
| Rut depth | 0.05–0.30 m | 0.12 m | assumed (dry, used; deeper where wet) |
| Median ridge | 0.6–1.2 m wide, grass 0.1–0.4 m tall | 0.8 m, 0.25 m | assumed |
| Puddles | in the rut bottoms, 0.5–5 m long | 0 when dry | assumed |

### Construction and materials
- Two compacted wheel ruts in the soil with a grassy median and grassy outer edges; dry, the rut walls are hard and crumbly; wet, the ruts hold water and mud. A track branches off every field road and runs along belts and field edges.
- **Materials:** the S13 `dirt_track` surface set (surfaces sheet); tyre-tread marks as a decal (no physical role).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Rut bottom, dry | as S13 | — | the surfaces sheet |
| At night | — | not readable (L) | terrain.md |

### Variants and states
- Kit: straight, curve, fork, a crossing of two tracks, a widened turning place; states: dry, wet; fresh (bare ruts) or old (grass in the ruts).

### Damage pattern
Craters in the track (E05), tracks detouring around them in a widened, braided band 5–15 m wide.

### Physics role
- **Collision:** ground: the S13 surface with the ruts as sub-grid relief (a 0.35 m rut does not fit a 1 m heightfield), written by the map compiler from the track centreline.
- **Material ids:** the surface row.
- **Gaps / wind:** none.
- **Snag hazards:** the median grass (micro-detail).
- **Landable surfaces:** yes, unevenly (derived): a drone landing across a rut edge with two legs in the rut tilts atan(depth / 0.24), 27° for 0.12 m; landing in one rut along it is level but the props clear the median grass by its height only.

### Placement rules
- Off every field road and along belts and field edges; a braided detour band around craters; tracks into yards and gates from the street.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `centreline` | polyline | — | from the map | Track path |
| `rut_spacing_m` | m | 1.3–2.0 | 1.6 | Gauge |
| `rut_width_m`, `rut_depth_m` | m | 0.25–0.5, 0.05–0.3 | 0.35, 0.12 | Rut cross-section |
| `median_grass_m` | m | 0–0.4 | 0.25 | Median cover height (cover layer) |
| `wet` | 0–1 | — | 0 | Puddles, mud |
| `braid_width_m` | m | 0–15 | 0 | Detour braiding around an obstacle |
| `seed` | int | — | — | |

### Open points
- Sub-grid ruts in the map format (a relief feature like the tilled furrows): world-builder.
