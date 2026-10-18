# Object sheet: lines, roads and debris (L01–L05, RD01–RD03, K01–K06, K08–K10)

**Owner:** reference-analyst. **Reviewed for buildability by:** asset-artist (task 1.7, `openspec/changes/p1-object-sheets/review-buildability.md`).

**OPSEC:** cite clips and photos by letter only. Describe classes, never a specific identifiable building, place, date or person. No file names, and no OSD values.

**Requirements:** AST-02, AST-04, AST-05, AST-06, REAL-01…05, PHY-22, PHY-23, SEC-01, SEC-02, PRC-06 · **Decisions:** D-007, D-013, D-024, D-028 · **Change:** `p1-object-sheets`, task 1.4.

**Rows:** L01–L05 and the road kits RD01–RD03 from [asset-list.md](../asset-list.md) §8, and the debris kit K01–K06, K08–K10 from §10 (K07 is in the vehicles sheet). The road kits were renamed from R01–R03 to RD01–RD03 so they no longer share IDs with the vehicles R01–R09 of §9.

## How this sheet was made

- **Letters read**, through contact sheets only: C 19 stills at 2/s, D 21 at 2/s, E 24 at 2/s, H 24 at 1/s, L 14 at 2/s; the photos J, K, M, O as single stills. Native-size crops for thin objects and colour sampling: C 1, 3, 5, 7, 8, 12; D 1, 3, 7, 9–14; E 7, 10, 15–18; H 1–3; L 1–2; and regions of J, K, M, O.
- **Reused, not re-measured:** terrain.md §4 B5–B6, §6 S1–S5, §7 (micro-detail), §8 (the `asphalt`, `rubble`, `yard_litter` surface rows), §9; knowledge.md §7.4 (object materials), §9.2, §10 (map format: wires are map objects with point lists, sag and diameter), §14.4; [houses-and-yard.md](houses-and-yard.md) (brick, tile, slate and lintel sizes, the blast-failure order, the C01 tarp, the Y11 line and barrel, the Y03 top wire); [vegetation.md](vegetation.md) (V19–V20 brush, the `deadwood` proposal).
- **Scale cues:** brick 250 × 120 × 65 mm, door leaf 0.8 × 1.9 m, storey 2.8 m (AST-04); the car wheel in M (a 165/80 R13 tyre is 0.59 m across, derived from the size code: 13 × 25.4 + 2 × 0.8 × 165 mm); a slate sheet 1.13 m wide (J); the drone's own prop line (0.574 m) in the lower corners of every clip still.
- **Labels:** every number is **measured** (from a letter, with still numbers), **published** (with its source), **derived** (with the derivation) or **assumed**.
- **Feed check values** are mean sRGB of hand-picked regions on the extracted stills, as in the other sheets: warm and saturated in sun (C, D, E), flat and grey in overcast (H), black and white at night (L). `photo` values come from J, K, M, O and are closer to albedo. Textures are authored at photo albedo; the feed effect adds the shift.
- **Physics ids** are the 17 of [physics_materials.json](../../assets-src/physics_materials.json), as the physics sign-off settled them (`review-materials.md` of the archived change `p1-pipeline-hardening`): `steel`, `sheet_metal`, `timber`, `masonry`, `concrete`, `glass`, `cable`, `foliage`, `bark`, `twig`, `deadwood`, `asbestos_cement`, `straw`, `fabric`, `rubber`, `plastic`, `mesh`. A shape that stands in for ground takes `surface:<id>`, one of the thirteen surface ids of the earthworks sheet; `asphalt` is a surface id only. `proposed:<id>` marks an id not in the table, for that sign-off. Snag features use the pipeline's two kinds: `edge` (a polyline the fiber bends over, with its radius) and `hook` (a free-ended member from base to tip, radius half its thickness).
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

A real wire does none of this. **Consequence:** C shows no wire for certain; terrain.md S1 and the C01 row of houses-and-yard.md (its "Wire above" line and C's reference view) now say so. The real wires in this sheet's letters are: the low slack cable from the dead trunk in D (D 9–12), the cable looping from the ceiling in E (E 16–17), the service drop and a farther line in J, the hanging cables and the cable loop on the ground in K, and the leaning pole in L (its wires are invisible at night). A yard can still carry a service drop as a placement choice (L03); it is just not measured from C.

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
| SIP main, 3 × 35 + 1 × 54.6 mm² | 26–32 mm | 0.55–0.61 kg/m | — | thick | published, two datasheets: https://dn.ru/storage/certificates/ludinivikabel/dop_mat_sip2.pdf (26.2 mm) and https://storage.mtender.gov.md/get/d886941c-18a5-441a-9bfc-f1c23940c575-1679407095103 (32 mm) |

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
| `crossarm_state` | enum | intact, twisted, hanging, gone | from damage | Crossarm pose: twisted 10–45° about the pole, hanging from one bolt, or gone (its insulators and wires follow; snags and `wire_ports` move with it) |
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
- **Collision:** the log as 2–3 stacked capsules with their real radius tapering 0.13 → 0.08 m (within 10 mm of the visual), the stub as a box, the brace as a capsule, the crossarm as a box, each hook a capsule of radius 8–9 mm, each insulator a cylinder r 0.0375 m × 0.108 m. Material: `timber` (log, brace, crossarm), `concrete` (stub), `steel` (hooks, bands), `glass` for the insulators, porcelain or glass (the physics sign-off maps porcelain to `glass`, review-materials.md §2).
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
- Porcelain's physics id: settled by the physics sign-off (review-materials.md §2): `glass`, since a glaze has glass's friction and an insulator is a cylinder, so the fiber bends over its own radius.

## L02 Concrete pole with crossarm and insulators

**Seen in:** H 1–4 (a straight grey pole passing 3–4 m to the right: two flat faces meeting at a sharp vertical arris, a clear taper from the butt, and a round through-hole on the lit face at mid-frame, H 2–3; so a vibrated rectangular СВ-type pole, not a round log). O shows a lamp post with a curved arm (the urban class). **R** for the head.
**Research sources:** the СВ-95 pole: 9.5 m long, 0.36 m³, 0.9 t, for lines from 0.4 kV (published, https://productcenter.ru/products/124963/stoika-sv95-3 ); the СВ-105 stand: 10.5 m long, 0.47 m³, 1.18 t, for lines up to 10 kV (published, https://productcenter.ru/products/124965/sv105-5 ); ПУЕ clauses above.

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
- **Collision:** the stand as 2–3 stacked boxes following the taper within 10 mm (`concrete`, its 3 mm arris is the fiber's bend edge); the crossarm a box (`steel`); pins and hooks capsules (`steel`); insulators cylinders (`glass`, porcelain or glass: the physics sign-off); the lamp bracket a capsule (`steel`) and the fitting a box. X state: the two pole pieces as boxes and every exposed bar a capsule with its real radius (`steel`).
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
| `sag_ratio` | float | 0.005–0.3 | 0.02 | Sag = ratio × span; above 0.05 only on damaged spans |
| `attach_heights_m` | m, m | 2.75–8 | from the poles or the house | The two ends |
| `twist_pitch_m` | m | 0.3–0.6 | 0.45 | SIP bundle twist (visual) |
| `damage` | enum per conductor | intact, cut, down | intact | Cut ends hang (L05 form) or drop (L04 form) |
| `points_per_m` | 1/m | 1–4 | 2 | Points written to the map per metre |
| `seed` | int | — | — | Cut positions, strand splay |

### Open points
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
| Diagonal (between two edges) | 1–6.5 m | 2.5 m | measured K (1–3 m between slab edges); up to 6.5 m from a slab edge to the floor below across a 5.6 m bay (urban-edge U01, derived) |
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
| `length_m` | m | 0.3–6.5 | 1.5 | Cable length (diagonals in U01 to 6.5 m) |
| `attach` | points | 1–2 | from the host asset | Where it hangs from |
| `tip_height_m` | m | 0–0.5 | 0.2 | Free end above the floor |
| `seed` | int | — | — | Curl, end splay |

### Open points
- Whether a cable hanging across an opening is written into that opening's gap polygon (it narrows a doorway the drone could otherwise pass): world-builder and physics engineer; the cautious default is yes for diagonals, no for loops above head height.

## Shared: roads (RD01–RD03)

- **The surfaces belong to the surfaces sheet:** `asphalt` (S11), `concrete_slab` (S12) and `dirt_track` (S13) are rows of the surface table (terrain.md §8; the earthworks-and-surfaces sheet). The road kits here are the **geometry and features** laid on them: the road outline, crossfall, edges, potholes, joints, ruts, crater rings, plates.
- **What is ground and what is an object:** whatever the heightfield and the surface layers can hold (crossfall, the carriageway outline, ruts as sub-grid relief, potholes as pitfall features) is written into the map by the map compiler; whatever is a separate solid (a concrete plate, a lifted asphalt slab, a kerb stone) is a catalog asset with its own collision. The road kits produce both, from one parameter set.
- **Landing:** roads are the flattest, most landable ground on the map; their realism is in where they are not flat.

## RD01 Asphalt road kit: straight, curve, junction; potholes, crater decals, cracked edges

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
| Kit pieces | straight 10 and 20 m; curves of radius 15–40 m; T and X junctions; an end that crumbles into a dirt track (RD03) | — | assumed (asset list) |

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
- **Material ids:** the ground uses the S11 surface row; lifted slabs `concrete`; kerbs `concrete`.
- **Gaps:** none.
- **Wind volumes:** none.
- **Snag hazards:** the edges of lifted slabs (`edge`, radius 3 mm); nothing on intact asphalt.
- **Landable surfaces:** the carriageway (rigid, flat to 2 %). A pothole under one leg tilts the drone by atan(depth / 0.24): 16° for a 70 mm hole (derived); a 0.6 m hole can take two legs. A water-filled hole looks like flat road on the feed (M).

### Placement rules
- One asphalt main street through each village, joining the next village; side streets are dirt or gravel (RD03); the asphalt ends in a crumbled edge where it meets a field road.
- Houses 5–15 m back behind their fences; L01/L02 poles along one side; abandoned and destroyed vehicles on and beside it (the vehicles sheet), with K07 debris around them; damage concentrated near the village centre and the junctions (assumed).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `piece` | enum | straight, curve, t_junction, x_junction, end_to_dirt | straight | Kit piece |
| `length_m` | m | 10–200 | 20 | Straight length (the map tiles longer runs) |
| `curve_radius_m` | m | 15–40 | 25 | Curve |
| `width_m` | m | 3.5–7.0 | 6.0 | Carriageway |
| `shoulder_m` | m | 0.5–2.0 | 1.0 | Shoulders |
| `crossfall` | float | 0.015–0.03 | 0.02 | Camber |
| `layer_mm` | mm | 40–80 | 50 | Edge drop and slab thickness |
| `edge_break_m` | m | 0–0.6 | 0.3 | Ravelled edge width |
| `patch_fraction` | 0–1 | 0–0.3 | 0.1 | Patches |
| `crack_density` | 0–1 | — | 0.4 | Crack network (decal and normal; cracks have no physical role below 20 mm) |
| `potholes_per_100m2` | float | 0–5 | 0.5 (I), 2 (D) | Pitfall features |
| `pothole_size_m`, `pothole_depth_mm` | m, mm | 0.2–2.0, 40–150 | 0.6, 70 | Pothole size and depth, drawn per hole around the defaults; written to the map as pitfall features (outline, depth, wall slope) |
| `pothole_water` | 0–1 | — | 0.3 | Fraction holding water (a flat reflective top) |
| `crater_spacing_m` | m | 0 or 20–100 | 0 (X: 50) | Craters with slab rings (with E05) |
| `debris_density` | 0–1 | — | from the state | Branches, glass, K-kit pieces on the road |
| `wetness`, `snow` | 0–1 | — | 0, 0 | Weather state (O: 0.8, 0.6) |
| `kerb` | bool | — | false | Urban kerb |
| `seed` | int | any | per piece | Every random choice (cracks, patches, potholes, edge breaks) |

### Open points
- `concrete` for lifted slabs and broken edges (friction near `concrete`, edge radius 3–5 mm from the aggregate): the physics sign-off's mapping of the proposed `asphalt`, which is a surface id only.
- Pitfall features for potholes and the sub-grid relief for crossfall and edges: world-builder (map format).

## RD02 Concrete road plates (about 6 × 2 m), laid as a track

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
| `seed` | int | any | per track | Every random choice (joints, steps, tilts, missing and broken plates) |

### Open points
- Which layout dominates locally is not seen in any letter; the generator holds all three and the world uses double_row as the default. Settled by the world review, not by the pilot.

## RD03 Dirt track ruts (decal plus surface)

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
| `median_h_m` | m | 0–0.1 | 0.05 | Median rise above the verges (the earthworks sheet's S13, which now defers to this table) |
| `wander` | amplitude, wavelength | 0–0.5 m, 10–50 m | 0.2 m, 30 m | Sway of the ruts about the centreline |
| `wet` | 0–1 | — | 0 | Puddles, mud |
| `braid_width_m` | m | 0–15 | 0 | Detour braiding around an obstacle |
| `seed` | int | any | per track | Every random choice (rut wander, puddles, braiding) |

### Open points
- Sub-grid ruts in the map format (a relief feature like the tilled furrows): world-builder.

## Shared: the debris kit (K01–K06, K08–K10)

### Rules every piece follows
- **Colour from its source** (knowledge.md §14.4): rubble takes the colours of the building it came from, never near-white. A heap at a house is that house's brick or adobe, tile or slate, render, timber and contents.
- **Geometry threshold:** every piece of 50 mm and larger is geometry with collision, placed by the world or by a host asset; smaller crumbs and shards are the `rubble` and `yard_litter` surfaces' micro-detail (terrain.md §7, `MicroDetailNear`), so no shard exists twice. Thin members over 2 mm (bars, nails, splinters, cables) are capsules or snag features at their real radius whatever the piece's size (REAL-05).
- **Size distribution:** a heap or scatter draws piece sizes from a power law, the number of pieces larger than s ∝ s⁻² (assumed: many small, few large), clipped to each row's range.
- **Spread around a damaged building:** the count density falls off from the wall as exp(−d / λ), 1.5–2 times wider on the side away from the burst (assumed):

  | Class | λ | Anchor |
  |---|---|---|
  | Tile shards (K02) | 1.0 m; a heap 0.2–0.4 m deep and 1–2 m wide along the foot | measured E 7, 10 (houses-and-yard H01) |
  | Bricks, halves, clusters (K01) | 1.5 m, centred on breaches and collapsed walls | measured J, K |
  | Slate fragments (K02) | 4 m; fragments 5–15 m out | measured J |
  | Planks, battens, rafters (K03) | 2 m; leaning on walls, lying across heaps | measured E, J, K |
  | Corrugated and flat sheet (K04) | 6 m, out to 30 m (light; they fly) | assumed |
  | Concrete with bars (K06) | 2 m, only at concrete structures | measured K |
  | Household items (K08) | 3 m, in front of open rooms and windows | measured K |
  | Tarps, fabric, film, cables (K05, K10) | attached, or within 2 m | measured C, K |

- **Mixture by volume:** the K mound is urban-edge U02's, whose `piece_mix` is the one source (brick 55 %, bonded chunks 8 %, concrete 8 %, timber 12 %, PVC 7 %, contents 7 %, textiles 3 %; this sheet's own first read of K, by eye, was brick about 50 %, furniture 15 %, plastic 10 %, concrete 10 %, timber 10 %, fabric 5 %). A village house's heap (E, J, assumed from both): tile or slate 40 %, adobe or brick 35 %, timber 20 %, glass and contents 5 %.
- **Landable pieces (derived):** a piece is landable only if its top face holds the 0.24 m leg square with 5 cm to spare each side (at least 0.35 × 0.35 m) within 15° of level; the generator sets `landable` from the resting pose.
- **Tilt under one leg (derived, atan(h / 0.24) along a side / atan(h / 0.34) on the diagonal):** tile shard 15 mm: 3.6° / 2.5°; flat brick 65 mm: 15° / 11°; brick on edge 120 mm: 27° / 19°; board 30 mm: 7° / 5°; a 0.2 m chunk: 40° / 30° (the drone slides off).
- **LOD:** a piece of size s drops under 2 render px at d = 935 · s / 2 (K01: 0.25 m → 117 m, the 120 m cull of `K01_whole`); a 0.1 m chunk culls at about 47 m and the surface micro-detail and textures carry the heap beyond.
- **Static or dynamic:** phase 1 builds every piece as a static catalog instance. Pieces that "rock and slide" (planks, sheets, tyres, barrels) need runtime objects in the flight loop (Open points).
- **One generator family:** `debris_*` generators share the K01 parameter set (`seed`, `size_m`, `physics_material`, `lod`) and add their own; every piece is rebuilt from params, never hand-edited.

## K01 Bricks: whole, halves, chunks; brick clusters

**Seen in:** C 1–8 (red brick bits at the foot of the cellar head's piers); D 12–14 (a pile of flat pale stone slabs with red brick beside them, the pile D's flight dives onto); E (the chimney's brick, the adobe heap is H01's); J (silicate bricks and blocks heaped along the right house's wall, pieces 0.1–0.3 m with many smaller); K (the mound's main material: sandy-red bricks, many of them perforated, single and in mortared clusters 0.5–0.8 m); I (terrain.md B5).
**Research sources:** AST-04 (brick 250 × 120 × 65 mm), the thickened 88 mm brick (published, houses-and-yard.md shared section); the existing generator `tools/blender/generators/debris_brick.py` and `assets-src/debris/K01_whole.json`.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Whole brick | 250 × 120 × 65 mm (88 mm thickened) | 250 × 120 × 65 mm | AST-04; published |
| Corner wear (chamfer) | 2–3 mm | per corner, seeded | assumed (`bevel_mm` of K01_whole) |
| Half | 120–130 mm long, the fracture face rough by 3–8 mm | 125 mm | derived; roughness assumed |
| Three-quarter | 180–190 mm | 185 mm | derived |
| Chunk | 40–150 mm, irregular | 90 mm | assumed; measured J, K for the size spread |
| Cluster | 2–20 bricks still mortared, 0.3–1.0 m | 2 courses × 3 bricks | measured K (blocks 0.5–0.8 m) |
| Mortar | joints 10–12 mm; a crust 0–10 mm on faces | 10 mm | derived (houses); crust assumed |
| Perforation | none, or 19–32 round voids 12–16 mm through the bed faces | none | measured K (perforated bricks in the mound); sizes assumed |
| Rubble-stone slab | 0.2–0.5 × 0.15–0.4 × 0.04–0.12 m | 0.35 × 0.25 × 0.07 m | measured D 12–14 (flat pale slabs, stacked, scaled from the bricks beside them) |

### Construction and materials
- Fired red clay brick (orange-red to sandy-red), silicate brick (pale grey-white, J), perforated ceramic brick (K), rubble-stone slabs (limestone or sandstone flags from foundations and walls, D). Faces worn, arrises chipped; fracture faces paler and sharper than weathered faces; a grey mortar crust on some faces.
- **Materials:** red brick: manifest `red_brick` (already pinned; visual material `brick_red`); silicate: Poly Haven `white_bricks` (candidate, CC0: https://polyhaven.com/a/white_bricks ); perforated: `red_brick` with the voids as geometry; stone slabs: Poly Haven `white_sandstone_bricks` or `rough_block_wall` ( https://polyhaven.com/a/white_sandstone_bricks , https://polyhaven.com/a/rough_block_wall ); mortar crust: `rough_plaster_03` ( https://polyhaven.com/a/rough_plaster_03 ). **Derived:** a fracture-face map (rougher, 10–15 % lighter) and a soot mask. UVs in asset space and upright (knowledge.md §14.4, PR-5's upside-down bricks).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Red brick, sunlit | as `red_brick` | #b97541 | terrain.md S3 |
| Red brick in backlight and flare | — | #a16f66 | measured D 14 |
| Sandy-red brick with mortar, in rubble | #988a78 | — | measured K |
| Brick wall face with mortar dabs | #8d8174 | — | measured K; terrain.md #847b71 |
| Silicate brick and plaster rubble, overcast | #7a6f66 | — | measured J |
| Rubble-stone slab | #b5aa9c | #998d82 | assumed albedo; measured D 14 |
| Fresh fracture face | 10–15 % lighter than the face | — | assumed |

### Variants and states
- Pieces: whole (the existing `K01_whole`), half, three_quarter, chunk, cluster, stone_slab. Types: red, silicate, perforated. States: clean, mortar-crusted, sooted.

### Damage pattern
Bricks come out whole or halved along the bed joints where mortar is weak (houses-and-yard.md, step 5), stay in clusters where it is strong, and shatter into chunks where a fragment or a fall hits them.

### Physics role
- **Collision:** whole, half, three-quarter: one box (the existing generator's plain box, within the chamfer of LOD0); chunk: one box along its principal axes within ±10 mm (chunks are generated as convex near-boxes so one box holds); cluster: one box per course, or per brick where the course is stepped; stone slab: one box. Perforation voids (12–16 mm) have no collision of their own.
- **Material ids:** `masonry` for every piece (as `K01_whole.json`).
- **Gaps / wind volumes:** none.
- **Snag hazards:** a whole brick none (as `K01_whole`); halves and chunks carry an `edge` along the fracture rim, radius 1 mm (sharper than the worn 2 mm arris, as the physics review foresaw); clusters an `edge` along their stepped ends (2 mm).
- **Landable surfaces:** single pieces no (as `K01_whole`); a cluster or stone slab with a top face of at least 0.35 × 0.35 m within 15°: yes. A flat brick under one leg tilts the drone 15° along a side, on edge 27° (the asset list's "tilt at liftoff").

### Placement rules
- At wall feet, breaches and collapsed walls of brick houses, outbuildings and the block; around the cellar head and sheds (C); stone slabs in piles at old foundations and in overgrown plots (D); clusters in X-state heaps (K).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `seed` | int | any | per variant | The wear (as today) and every new random choice |
| `size_m` | {length, width, height} | as above | 0.250, 0.120, 0.065 | Whole-brick size (as today); halves and three-quarters cut it |
| `bevel_mm` | {min, max} | 1–4 | 2.0, 3.0 | Corner chamfer (as today) |
| `physics_material` | id | `masonry` | `masonry` | Collision material (as today) |
| `lod` | {lod1_switch_m, cull_m} | — | 3.0, 120.0 for the whole brick; cull scaled as 935 · s / 2 for smaller pieces | LOD switch and cull (as today) |
| `piece` | enum | whole, half, three_quarter, chunk, cluster, stone_slab | whole | Which piece; `whole` is today's generator unchanged |
| `brick_type` | enum | red, silicate, perforated | red | Visual material and voids |
| `fracture_roughness_mm` | mm | 2–10 | 5 | Relief of fracture faces |
| `chunk_size_m` | m | 0.04–0.15 | 0.09 | Chunk bounding size |
| `cluster_courses`, `cluster_bricks` | int | 1–5, 2–20 | 2, 6 | Cluster layout |
| `cluster_step` | 0–1 | — | 0.5 | How ragged the cluster's ends are |
| `mortar_mm` | mm | 0–12 | 10 | Joint thickness in clusters |
| `mortar_cover` | 0–1 | — | 0.3 | Fraction of faces with a crust |
| `voids` | {count, d_mm} | 0–32, 12–16 | 0 | Perforation (geometry) |
| `stone_size_m` | {length, width, height} | as above | 0.35, 0.25, 0.07 | Stone slab |
| `soot` | 0–1 | — | 0 | Burnt state |

### Open points
- The existing generator builds `whole` only; the other pieces are additions to `debris_brick` (asset-artist), with `K01_whole` unchanged so its committed metadata keeps matching.
- If stone slabs prove too numerous as catalog pieces, the small ones move to the `rubble` micro-detail (world-builder).

## K02 Clay tile shards; slate (шифер) fragments

**Seen in:** E 7, 10 (tile shards 0.1–0.35 m heaped along the wall foot, a few nearly whole tiles among them; more inside, E 16); J (asbestos-cement slate: whole and half sheets about 1.1 m wide stacked and leaning in the debris, fragments 0.1–0.6 m scattered between the houses and the street).
**Research sources:** houses-and-yard.md shared section (tile and slate sizes, the blast fracture of asbestos-cement sheet).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Whole tile | 0.33–0.40 × 0.20–0.24 m, 12–16 mm thick, a nib at the head, slight camber | 0.36 × 0.22 × 0.014 m | houses-and-yard H01 (assumed from the batten pitch); thickness assumed |
| Tile shards | 0.05–0.35 m, 2–6 pieces per tile | 0.15 m | measured E 7, 10; terrain.md S3 (0.1–0.4 m) |
| Ridge tile pieces | half-round, 0.40 × 0.20 × 0.10 m | — | assumed |
| Slate sheet | 1.75 × 1.13 m, 5.2–5.8 mm, 8 waves at 150 mm, 40 mm high | — | published (houses-and-yard) |
| Slate fragments | 0.1–1.13 m; straight breaks along the wave troughs, jagged across | 0.3 m | measured J |
| Heap along a wall | 0.2–0.4 m deep, 1–2 m wide | 0.3 m | measured E (houses-and-yard H01) |

### Construction and materials
- Tiles: fired clay as H01, broken faces paler and rougher. Slate: asbestos-cement, grey, lichen and moss on the old upper face, paler underneath and on the breaks, nail holes at the crests.
- **Materials:** tiles Poly Haven `clay_roof_tiles_02` ( https://polyhaven.com/a/clay_roof_tiles_02 , as H01); slate as the houses sheet's slate surface (`concrete_wall_003` tinted #736767, https://polyhaven.com/a/concrete_wall_003 ). **Derived:** a fracture-face map; the wave profile is geometry.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Tile shards at the foot | about #a0613f (as H01 tiles) | #825e3f (E 10), #805c43 (E 7), #825f44 (terrain.md) | measured E |
| Slate, weathered upper face | #736767 | — | houses-and-yard (J) |
| Slate stack in shade | #5a5048 | — | measured J |

### Variants and states
- Tile: shard (seeded outline), whole, ridge piece. Slate: whole sheet, half sheet, fragment (seeded outline), a stack of 2–6. States: clean, mossy, sooted.

### Damage pattern
Tiles lift, slide and break at the eaves and the foot; slate shatters and scatters 5–15 m on the street side (houses-and-yard.md, step 2).

### Physics role
- **Collision:** tile shard: one thin box (14 mm); whole curved tile: 2 boxes; slate sheet or fragment: one box over the wave envelope (40 mm plus the sheet) so the legs meet the crests at the right height; a large fragment lying across rubble: 2–3 boxes following its tilt.
- **Material ids:** tiles `masonry`; slate `asbestos_cement`.
- **Gaps / wind volumes:** none.
- **Snag hazards:** slate rims (`edge`, radius 3 mm, half the sheet), tile fracture rims (`edge`, 2 mm).
- **Landable surfaces:** a whole or half slate sheet lying flat (at least 0.87 × 1.13 m): yes, the legs on crests 150 mm apart; it may crack under a hard landing (a later material property). Tile shards: no.

### Placement rules
- Tile heaps along the foot of tiled houses and inside them (E); slate fragments fanned 5–15 m on the street side of slate houses, whole sheets leaning on fences and debris (J).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `piece` | enum | tile_shard, tile_whole, ridge_piece, slate_sheet, slate_half, slate_fragment, slate_stack | tile_shard | Which piece |
| `size_m` | m | 0.05–1.75 | by piece | Outline size |
| `fracture_count` | int | 2–6 | 3 | Breaks per tile or sheet |
| `slate_waves` | int | 1–8 | by size | Waves on a fragment |
| `stack_count` | int | 2–6 | 3 | Sheets in a stack |
| `moss`, `soot` | 0–1 | — | 0.3, 0 | Surface state |
| `physics_material` | id | `masonry`, `asbestos_cement` | by piece | Collision material |
| `seed`, `lod` | — | — | — | As K01 |

### Open points
- Tile and slate breakability under a leg (a later material property): physics engineer, as in the houses sheet.

## K03 Planks, beams, broken rafters

**Seen in:** E 7 (the plank lean-to: grey boards and a board panel broken off and leaning; battens and rafters on the stripped roof); E 16 (ceiling boards 0.1–0.2 m wide and 0.5–1.5 m long on the floor among plaster slabs); J (a beam about 0.15–0.18 m wide and about 2.8 m long leaning at 60° against the debris, scaled from the 0.8 m door; planks, posts and poles among the slate); K (timber in the mound and a cut log in two sections 0.25–0.3 m across); C 7, 12 (the cellar head's boards).
**Research sources:** sawn softwood: boards 16–44 mm and beams 50–100 mm thick, widths from 75 mm in 25 mm steps (published, the ГОСТ 24454 and 26002 classes, https://dnaop.com/html/66566/doc-%D0%93%D0%9E%D0%A1%D0%A2_24454-80 ); roof timber sizes from houses-and-yard.md.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Board | 25–40 × 100–200 mm × 0.3–6 m | 32 × 150 mm × 1.5 m | published classes; lengths assumed |
| Batten | 50 × 50 mm (or 25 × 100) × 0.3–4 m | 50 × 50 mm × 1.2 m | published, as houses-and-yard (shared roofs: battens 50 × 50 or 25 × 100 mm); lengths assumed |
| Rafter | 50 × 150 or 100 × 150 mm, or round 0.10–0.16 m, × 1–6 m | 100 × 150 mm × 3 m | assumed, as houses-and-yard (rafters 0.10–0.15 m thick) |
| Beam (plate, сволок) | 150 × 150 to 200 × 250 mm × 2–6 m | 180 × 180 mm × 3 m | houses-and-yard; measured J (0.15–0.18 m, about 2.8 m) |
| Board panel (door leaf, fence or shed section) | 0.8–2 × 1.5–2 m, boards on 2–3 ledges | 0.8 × 1.9 m | AST-04; measured E 7 |
| Splinters at a break | 0.05–0.4 m long, 5–30 mm thick | 0.2 m | assumed (the V07 style) |
| Nails | 3–4 mm × 70–120 mm, 20–80 mm proud, often bent | 3.5 mm, 50 mm proud | assumed |
| Log section | 0.2–0.35 m across × 0.3–1 m | 0.3 × 0.6 m | measured K |

### Construction and materials
- Pine and spruce sawn timber: grey-weathered outside, pale yellow at fresh breaks, dark old roof timber, charred in burnt houses; rusted nails; board panels on ledges with a brace.
- **Materials:** Poly Haven `raw_plank_wall`, `weathered_brown_planks`, `old_planks_02`, `rough_wood`, `roof_planks` (candidates, CC0: https://polyhaven.com/a/raw_plank_wall , https://polyhaven.com/a/weathered_brown_planks , https://polyhaven.com/a/old_planks_02 , https://polyhaven.com/a/rough_wood , https://polyhaven.com/a/roof_planks ). **Derived:** grain along each piece's length, a fracture-end map, a char mask.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Weathered shed boards | #8a8478 | #605c56 | assumed albedo; measured E 7 (terrain.md B6 #615e56) |
| Leaning board panel | — | #66604e | measured E 7 |
| Plank, overcast | #6f6555 | — | measured J |
| Fallen ceiling boards and plaster, in shade | — | #5e5c61 | measured E 16 |
| Cut log, bark in shade | #2c2721 | — | measured K |
| Fresh fracture | #b09a78 | — | assumed |
| Charred | #2a2622 | — | assumed |

### Variants and states
- Pieces: board, batten, sawn rafter, round rafter, beam, panel, log section. States: whole, broken at one or both ends (splintered), charred, with nails; panels whole, with boards missing, or hanging by one hinge (on a host).

### Damage pattern
Battens and rafters snap at mid-span or at the plate with long splinters (houses-and-yard.md, step 4); boards split along the grain; panels lose boards; burnt timber chars to a black crust and breaks short.

### Physics role
- **Collision:** a box per sawn piece, a capsule per round rafter or log section (real radius), capsules for splinters ≥ 10 mm thick; a panel one box (boards flush) or a box per board where boards are missing (gaps of 0.1–0.4 m, not passable).
- **Material ids:** `timber` (sawn timber; the vegetation sheet's `deadwood` is for natural dead wood).
- **Gaps / wind volumes:** none (a leaning panel at a wall is part of its host's wind volume).
- **Snag hazards:** splinters (`hook`, radius half their thickness), nails (`hook`, radius 1.75 mm), broken ends (`edge`, 2 mm).
- **Landable surfaces:** a board panel lying flat (0.8 × 1.9 m): yes; a plank (≤ 0.2 m wide) or a beam: no. A plank across a brick rocks under the legs like a see-saw (the asset list's "rock and slide"): static in phase 1 (Open points).

### Placement rules
- Around every D or X house and shed: battens and rafters inside and along the foot, panels leaning on walls (E), beams leaning on heaps (J), boards across heaps; brush piles (V20) beside them in yards (J).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `piece` | enum | board, batten, rafter_sawn, rafter_round, beam, panel, log_section | board | Which piece |
| `section_m` | {w, h} or d | as above | by piece | Cross-section |
| `length_m` | m | 0.3–6 | by piece | Length |
| `break_ends` | int | 0–2 | 1 | Splintered ends |
| `splinter_len_m`, `splinter_count` | m, int | 0.05–0.4, 2–10 | 0.2, 5 | Break geometry |
| `nails`, `nail_proud_mm` | int, mm | 0–10, 20–80 | 2, 50 | Protruding nails |
| `panel_missing_boards` | 0–1 | — | 0 | Panel state |
| `char`, `weathering` | 0–1 | — | 0, 0.6 | Surface state |
| `seed`, `lod` | — | — | — | As K01 |

### Open points
- Which planks rock and slide under the legs (runtime objects) and which are fixed in heaps: physics engineer and world-builder.

## K04 Corrugated sheet, bent sheet metal

**Seen in:** N per the asset list (the vehicles sheet; terrain.md S4). In this sheet's letters no steel sheet is certain: the wavy grey sheets in J read as asbestos-cement slate (K02). **R** for the rest.
**Research sources:** corrugated galvanised iron from about 0.3 mm upward, today nearly always at a 76 mm pitch (published, https://en.wikipedia.org/wiki/Corrugated_galvanised_iron ); corrugated panels fail at 7–14 kPa (published, houses-and-yard.md's blast thresholds).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Wavy corrugated sheet | 0.3–0.8 mm, pitch 76 mm, depth 15–20 mm, 0.8–1.1 × 1.5–3 m | 0.5 mm, 76 / 18 mm, 1.0 × 2.0 m | published pitch and gauge; depth and size assumed |
| Trapezoidal profiled sheet (C8, C20 class) | 0.4–0.5 mm, ribs 8 or 20 mm, 1.15–1.2 m wide, 2–6 m long | C20, 0.45 mm, 1.15 × 2.5 m | assumed (typical sold sizes) |
| Flat roofing sheet with folded seams | 0.5–0.7 mm, panels 0.7–1.25 m wide | 0.55 mm | assumed (houses-and-yard H04, O06) |
| Bends | 1–4 folds of 20–170°, or crumpled | 1 fold of 60° | assumed |
| Holes and tears | fragment holes 10–150 mm with petals; tears 0.1–1 m | 5 holes | assumed; car panels in M show holes 30–200 mm (terrain.md R0) |

### Construction and materials
- Galvanised or painted steel (green, red-brown, grey), rusting from cut edges, holes and folds. The corrugation is geometry at LOD0 (18 mm deep: the legs land on crests), the texture carries only paint, zinc and rust.
- **Materials:** Poly Haven `corrugated_iron`, `corrugated_iron_02`, `rusty_corrugated_iron`, `worn_corrugated_iron`, `rusty_metal_sheet`, `painted_metal_shutter` (candidates, CC0: https://polyhaven.com/a/corrugated_iron , https://polyhaven.com/a/rusty_corrugated_iron , https://polyhaven.com/a/rusty_metal_sheet , https://polyhaven.com/a/painted_metal_shutter ).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Galvanised | #9a9a95 | — | houses-and-yard (Y11) |
| Rust | #6e5a48 | — | houses-and-yard (Y03) |
| Painted green / red-brown | #4e6a4a / #6e3b2c | — | assumed |
| Sooted | #2a2828 | — | assumed |

### Variants and states
- Profiles: wavy, C8, C20, flat. States: flat, bent, crumpled, torn, holed, rusted, painted.

### Damage pattern
Panels tear off their battens at a few kPa and fly 5–30 m downwind, wrapping round posts, trees and fences; fragments punch petalled holes; folds crease sharply and rust along the crease.

### Physics role
- **Collision:** one thin box per flat facet between folds (a bent sheet 2–6 facets), as thick as the corrugation envelope (18–20 mm wavy and C20, 8 mm C8, 2 mm flat) so the legs meet the crests.
- **Material ids:** `sheet_metal` (20 kN/m compliance: it drums when landed on).
- **Gaps:** none.
- **Wind volumes:** a standing or leaning sheet over 1 m² is one solid part (porosity 0).
- **Snag hazards:** every cut, torn and hole edge is an `edge` with the material's 0.3 mm radius: **it cuts the fiber**; petals and torn corners are `hook`s.
- **Landable surfaces:** a flat or gently bent sheet of 1 m or more lying on the ground: yes, springy; a crumpled sheet: no.

### Placement rules
- Around sheds, garages (O06), the cowshed (P02) and sheet-metal gates (Y05); wrapped on posts, fences and trees downwind of damaged roofs, 5–30 m out.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `profile` | enum | wavy, c8, c20, flat | wavy | Section |
| `thickness_mm` | mm | 0.3–0.8 | 0.5 | Gauge (visual edge, mass) |
| `size_m` | {w, l} | 0.7–1.2 × 1–6 | 1.0 × 2.0 | Sheet size |
| `folds` | list of angles | 0–4 of 20–170° | [60] | Bends |
| `crumple` | 0–1 | — | 0 | Crumpled state |
| `holes` | {count, size range} | 0–20, 10–150 mm | 5, 20–80 | Fragment holes with petals |
| `tears` | int | 0–3 | 0 | Torn edges |
| `rust`, `paint` | 0–1, colour | — | 0.4, galvanised | Surface |
| `seed`, `physics_material`, `lod` | — | — | `sheet_metal` | As K01 |

### Open points
- Sheets lifted and moved by strong wind or prop wash (a loose sheet flips in a 15–20 m/s gust): physics engineer, phase 7 (visual only before).

## K05 Tarp, draped and torn

**Seen in:** C 5–9 (a light-blue woven tarp hanging down the cellar head's right flank from the roof edge to the ground, about 2 × 3 m, sunlit blue above, a dirty grey-white lower half, folded at the corner); K (white plastic film draped over a concrete beam in the mound, white sacks among the rubble); I (sheets and plastic, terrain.md S5).
**Research sources:** polyethylene tarps: a loosely woven polyethylene-strip core laminated with polyethylene sheet; blue light-duty about 0.13–0.15 mm, silver heavy-duty about 0.28–0.30 mm; grommets every 0.46–1.5 m (published, https://en.wikipedia.org/wiki/Tarpaulin ).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Tarp | 2 × 3 to 6 × 8 m | 3 × 4 m | assumed; measured C (about 2 × 3 m, houses-and-yard C01) |
| Thickness | 0.13–0.30 mm | 0.15 mm | published |
| Grommets | every 0.46–1.5 m on a rope-hemmed edge | 1.0 m | published |
| Film (building, greenhouse) | 0.1–0.2 mm, 1.5–6 m | 0.15 mm, 3 × 3 m | assumed; measured K (a white sheet of about 1 × 1.5 m on the beam) |
| Tears | rips 0.2–2 m, holes 0.05–0.5 m, frayed edges | 2 rips | assumed |
| Weights | bricks, tyres, planks on the edges | 3 bricks | assumed, as houses-and-yard (C01, Y09) |

### Construction and materials
- Woven polyethylene, blue (also green, silver, white), sun-faded on the exposed side, dirt in the folds and along the bottom, frayed where it rubbed. Film: a single clear-to-milky sheet that shreds into strips.
- **Materials:** the houses sheet's derived woven surface (Poly Haven `rough_linen` tinted #8aa0c0, https://polyhaven.com/a/rough_linen ); film a plain translucent material (no set needed).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Blue tarp, sunlit | #8aa0c0 | #768bb0 (C 7), #767f9a (C 5), #8695b2 (C 9) | houses-and-yard albedo; measured C |
| Blue tarp, dirty lower part | #a3a6a6 | #95999b | assumed albedo; measured C 7 |
| White film | #b7bdc2 | — | measured K |

### Variants and states
- Modes: draped over a host (a roof edge, a cellar head, a pile, a woodpile), hanging from one fixed edge, lying crumpled. Material: tarp or film. States: whole, torn, shredded.

### Damage pattern
Tarps tear at the grommets first, then along the weave; film shreds into strips that wrap round anything they touch.

### Physics role
- **Collision:** where the tarp lies on its host the host's collision stands; where it spans a void or hangs free it is a thin compliant shell (boxes 5 mm thick) with `fabric`; crumpled on the ground it is part of the ground's cover.
- **Material ids:** `fabric`.
- **Gaps:** none; a tarp hanging across an opening does not close the opening's gap (the drone pushes through), but it is a snag.
- **Wind volumes:** a free-hanging tarp: porosity 0 while taut, 0.3–0.6 when loose (assumed); a draped tarp adds nothing to its host.
- **Snag hazards:** free corners and torn flaps (`hook`), the hem and grommet line and the rips (`edge`, radius 2 mm): **the fiber catches the hem**. Flutter in wind and prop wash is visual in phase 1.
- **Landable surfaces:** as whatever lies under it; over a void, no (it sags).

### Placement rules
- On cellar heads, woodpiles, hay stacks and roofs as repairs (houses-and-yard), on the sunny flank (C); film and sacks in the block's rubble (K); about one tarp per 3–5 yards (assumed).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `size_m` | {w, l} | 2–8 | 3 × 4 | Sheet size |
| `material` | enum | tarp, film | tarp | Surface and tear style |
| `colour` | enum | blue, green, silver, white, clear | blue | Albedo |
| `mode` | enum | draped, hanging, crumpled | draped | Shape |
| `host` | asset | — | — | What it lies on (its collision is reused) |
| `fixed_edge` | edge | — | top | Hanging mode |
| `tears`, `shred` | int, 0–1 | 0–5 | 1, 0 | Damage |
| `grommet_spacing_m` | m | 0.46–1.5 | 1.0 | Hem detail and snag points |
| `weights` | int | 0–6 | 3 | Bricks or tyres on the edges (K01, K09) |
| `dirt`, `fade` | 0–1 | — | 0.4, 0.5 | Surface |
| `seed` | int | — | — | Drape, tears |

### Open points
- `fabric` (soft, wraps, no bounce): settled by the physics sign-off (review-materials.md §2). Physical flutter (a tarp that wraps a prop) is phase 7.

## K06 Concrete chunks with protruding rebar

**Seen in:** K (two precast concrete beams lying in the mound, about 3–4 m long and 0.3–0.5 m deep, one with a recessed side panel; a bar about 0.35 m long sticking out of one beam end; scaled from the bricks beside them); **R**.
**Research sources:** precast lintels with 2–4 bars of 8–12 mm (houses-and-yard.md); hollow-core floor slabs 1ПК, 220 mm thick with round voids 159 mm across (published, ГОСТ 9561, https://meganorm.ru/mega_doc/norm/gost_gosudarstvennyj-standart/11/gost_9561-91_plity_perekrytiy_zhelezobetonnye_mnogopustotnye.html ); 12 mm prestressed bars in road plates (published, RD02).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Lintel piece | 0.12–0.14 × 0.25 × 0.5–2 m, 2–4 bars of 8–12 mm | 0.12 × 0.25 × 1.2 m, 2 bars of 10 mm | assumed, as houses-and-yard (lintels: 0.12–0.14 × 0.25 m per leaf, standard practice) |
| Hollow-core slab piece | 0.22 m thick, voids 159 mm, pieces 0.3–1.5 m | 0.22 × 1.0 × 0.8 m | published |
| Precast beam | 3–4 m × 0.3–0.5 m deep | 3.5 × 0.4 × 0.25 m | measured K |
| Screed or thin slab piece | 0.05–0.15 m thick, 4–5 mm mesh at 0.1–0.2 m | 0.08 m, mesh 0.15 m | assumed |
| Bars | 5–14 mm (the lower end is the wire urban-edge U01 lists at broken slab ends); 0.05–0.8 m proud; straight, bent 30–90° or hooked; 2–6 per break | 10 mm, 0.35 m | assumed range; measured K (about 0.35 m) |

### Construction and materials
- Precast concrete elements of the brick houses and the block (lintels, floor slabs, beams, balcony slabs) and monolithic screeds; broken across, the concrete crushed at the break, bars bent out and rusting, fracture faces paler and rough with aggregate.
- **Materials:** Poly Haven `concrete_wall_003`, `rough_concrete`, `concrete_debris`, and `rebar_reinforced_concrete` for fracture faces; bars `rusty_metal` (candidates, CC0: https://polyhaven.com/a/concrete_debris , https://polyhaven.com/a/rebar_reinforced_concrete , https://polyhaven.com/a/rusty_metal ).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Precast beam, weathered | #96958f | — | measured K |
| Fresh fracture | #a9a69e | — | assumed |
| Bars, rusted | #6e5a48 | — | houses-and-yard (rust) |

### Variants and states
- Pieces: lintel, hollow_core, beam, screed, balcony slab edge. States: clean break, crushed end, sooted.

### Damage pattern
Lintels crack and drop when the brick above them goes; slabs crack at their supports and hang on their bars (houses-and-yard H06); in a collapse everything breaks into pieces whose bars bend out of the breaks.

### Physics role
- **Collision:** a chunk is 1–3 boxes (`concrete`); a hollow-core piece's webs are boxes so its voids are real (a leg drops into an open 159 mm tube at a broken end); every bar a capsule at its real radius (`steel`).
- **Material ids:** `concrete`, `steel`.
- **Gaps / wind volumes:** none (below 2 m).
- **Snag hazards:** every bar a `hook` from its root to its tip, radius half the bar (PR-5's "rebar sticking out", physical); fracture rims `edge` (3 mm, the concrete arris); mesh pieces `hook`s.
- **Landable surfaces:** a chunk or slab piece with a top face of at least 0.35 × 0.35 m within 15°, bars excluded from the face: yes.

### Placement rules
- At the block (U01, U02), at the lintels of brick houses (H03, H04, H06), at concrete garages and sheds, with broken road plates (RD02); in K-type mounds 10 % by volume.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `piece` | enum | lintel, hollow_core, beam, screed, balcony_edge | lintel | Which element |
| `size_m` | {l, w, h} | as above | by piece | Piece size |
| `fracture_roughness_mm` | mm | 5–40 | 15 | Break relief |
| `bars` | int | 0–8 | by piece | Bars at each break |
| `bar_d_mm` | mm | 5–14 | 10 | Bar diameter; the collision capsule and the snag take half of it as their radius |
| `bar_proud_m` | range | 0.05–0.8 | 0.1–0.4 | How far bars stick out |
| `bar_bend_deg` | range | 0–90 | 0–45 | Bending |
| `mesh_pitch_m` | m | 0 or 0.1–0.2 | 0 | Screed mesh |
| `spall`, `soot` | 0–1 | — | 0.3, 0 | Surface |
| `seed`, `physics_material`, `lod` | — | — | `concrete` | As K01 |

### Open points
- None beyond the shared ones.

## K08 Household items: fridge, wardrobe, mattress, radiator, chair

**Seen in:** K (two-door chipboard wardrobes in the open rooms and a brown wardrobe panel in the mound, shelving, a cast-iron radiator under a window, a sofa back, dark appliance housings, PVC window frames, a TV); I (sheets and plastic, terrain.md S5).
**Research sources:** a cast-iron sectional radiator of the МС-140 type is about 585 mm high with 500 mm between the connections (published listing, https://catalog.onliner.by/radiators/llmz ) and 140 mm deep (from the type name).

### Dimensions
| Item | Range | Chosen default | Label |
|---|---|---|---|
| Fridge | 0.55–0.60 × 0.60–0.65 × 1.4–1.65 m, steel shell 0.5–0.8 mm | 0.57 × 0.60 × 1.5 m | assumed |
| Wardrobe | 0.55–0.60 × 0.8–1.6 × 1.8–2.2 m, chipboard 16 mm | 0.58 × 1.2 × 2.0 m | measured K (two-door wardrobes about 1.0–1.6 m wide, about two thirds of the room height); board assumed |
| Mattress | 1.9–2.0 × 0.8–1.6 × 0.10–0.20 m, spring core | 1.9 × 0.9 × 0.15 m | assumed |
| Radiator | sections 0.585 m high, 0.14 m deep, at an 80 mm pitch, 6–12 sections | 8 sections, 0.64 m long | published height and depth; pitch and count assumed |
| Chair | 0.42–0.45 × 0.45–0.50 × 0.85–0.95 m | 0.43 × 0.47 × 0.90 m | assumed |
| Also seen | sofa 2.0 × 0.9 × 0.9 m; shelving; appliance housings 0.4–0.9 m; PVC window frames, white profiles 60–70 mm deep; a TV | — | measured K by eye (scaled from bricks); profile depth assumed |

### Construction and materials
- Chipboard furniture with wood-look veneer or laminate (light and dark brown), enamelled steel appliances, cast-iron radiators painted white or cream, spring mattresses in fabric, upholstered sofas, white PVC window frames with steel cores.
- **Materials:** chipboard: Poly Haven `oriented_strand_board` for broken cores, a plain veneer tint for faces (https://polyhaven.com/a/oriented_strand_board ); upholstery: `fabric_pattern_05`, `dirty_carpet` (https://polyhaven.com/a/fabric_pattern_05 , https://polyhaven.com/a/dirty_carpet ); enamel and PVC plain tinted materials; radiator `rusty_painted_metal` (https://polyhaven.com/a/rusty_painted_metal ).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Light veneer wardrobe | #997d61 | — | measured K |
| Dark brown wardrobe panel | #45322b | — | measured K |
| PVC frame | #dcd8c8 | — | measured K |
| Fridge enamel | #d9d6cc | — | assumed |
| Radiator paint | #cfcac0 | — | assumed |

### Variants and states
- Items as listed. States: in place (inside a room), fallen, on its back, broken into panels (wardrobe), door off (fridge), torn with springs out (mattress), torn off the wall with pipe stubs (radiator), sooted.

### Damage pattern
Contents spill out of open rooms and down the rubble slope (K); light items (cushions, sheets, plastic) fly furthest; heavy items (fridges, radiators) stay near the wall foot.

### Physics role
- **Collision:** boxes per item part; radiator sections as separate boxes (the 0.04 m gaps between sections at the top can wedge a leg); a broken wardrobe one box per panel.
- **Material ids:** fridge `sheet_metal`; wardrobe, shelving, chair `timber`; mattress and sofa `fabric` (soft, sinks 50–100 mm); radiator `steel`; PVC frames, appliance housings, TV `plastic`.
- **Gaps / wind volumes:** none (a standing wardrobe in an open room is part of the host's interior).
- **Snag hazards:** mattress springs when torn (`hook`, 1.5 mm), broken panel edges (`edge`, 2 mm), chair legs (`hook`), PVC frames' broken profiles and their steel cores (`edge`), radiator pipe stubs (`hook`).
- **Landable surfaces:** a fridge on its back (0.57 × 1.5 m): yes, springy; a wardrobe lying flat: yes; a mattress: soft-landable (the legs sink); a chair seat (0.42 m): only centred within ±9 cm (derived: 0.42 − 0.24); a radiator: no.

### Placement rules
- Only at the block and the two-storey houses in quantity (terrain.md S5); a few items in X-state village houses (a fridge, a bed frame, a wardrobe); in front of open rooms 0–8 m out (λ 3 m).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `item` | enum | fridge, wardrobe, mattress, radiator, chair, sofa, shelving, pvc_frame, appliance | fridge | Which item |
| `size_m` | {w, d, h} | as above | by item | Size |
| `sections` | int | 6–12 | 8 | Radiator |
| `pose` | enum | upright, fallen, on_back, on_side | upright | Resting pose |
| `broken` | 0–1 | — | 0 | Panels off, doors off, torn |
| `colour` | enum | light_veneer, dark_veneer, white, cream | by item | Surface |
| `soot` | 0–1 | — | 0 | Burnt state |
| `seed`, `physics_material`, `lod` | — | — | by item | As K01 |

### Open points
- `plastic` (PVC, housings, crates): settled by the physics sign-off (review-materials.md §2).

## K09 Tyres, metal barrel, bucket, crates

**Seen in:** **R** per the asset list; K shows a blue plastic crate about 0.6 × 0.45 m in the mound; the intact barrel and bucket are the yard props of houses-and-yard.md Y11.
**Research sources:** tyre sizes from their size codes (derived); the 200 l drum and the bucket as in Y11; tyres burn down to the bead wire (terrain.md R0).

### Dimensions
| Item | Range | Chosen default | Label |
|---|---|---|---|
| Car tyre | 0.57–0.60 m × 0.155–0.175 m, a 13-inch (0.33 m) bead | 165/80 R13: 0.59 × 0.165 m | derived from the size code |
| Truck tyre | 1.0–1.22 m × 0.26–0.40 m | 1.0 × 0.26 m | derived from the size codes of the ЗИЛ and Урал class (the vehicles sheet holds the published specs) |
| Burnt tyre | two bead rings of 5–8 mm steel-wire bundles plus a tangle of 0.3–1 mm cord wire | — | assumed |
| Barrel | 0.585 × 0.88 m, 200 l steel drum | — | assumed, as houses-and-yard Y11 (the standard drum) |
| Bucket | 0.28–0.32 × 0.27–0.30 m, 10–12 l | 0.30 × 0.28 m | assumed, as houses-and-yard Y11 |
| Wooden crate | 0.5 × 0.3 × 0.3 m, slatted | — | assumed |
| Plastic crate | 0.6 × 0.4 × 0.3 m | — | assumed; measured K (about 0.6 × 0.45 m) |

### Construction and materials
- Black rubber tyres with a tread, alone, stacked or weighting tarps and roofs; burnt tyres reduced to their steel. Steel drums and buckets, rusted, dented and holed. Slatted wooden vegetable crates; ribbed plastic crates (blue, green, grey).
- **Materials:** tyres a plain rubber material with a tread normal (no set needed); barrel and bucket as Y11 (`rusty_painted_metal`, `rusty_metal`); wooden crates `raw_plank_wall` (https://polyhaven.com/a/raw_plank_wall ); plastic crates a plain tinted material.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Tyre rubber, dusty | #3a3a3a | — | assumed; terrain.md tyre #545455 (M, lit) |
| Blue plastic crate | #174893 | — | measured K |
| Barrel, bucket | as Y11 | — | houses-and-yard |

### Variants and states
- Tyres: car, truck; lying, upright, stacked (2–6), burnt. Barrel: upright, on its side, dented, holed. Bucket: upright, tipped, crushed. Crates: wooden or plastic, whole or broken.

### Damage pattern
Fire leaves tyres as bead rings and wire tangles in a black ash patch; fragments hole drums and buckets with petalled holes; crates split.

### Physics role
- **Collision:** a lying tyre is a ring of 8 capsules of radius 0.08 m (half the section) around its 0.21 m centreline, so the 0.33 m hole stays real and a leg drops into it (derived); an upright tyre a cylinder; a burnt tyre two capsule rings (`steel`, radius 3 mm) and its cord tangle as `hook` snags; barrel and bucket cylinders; crates boxes.
- **Material ids:** tyres `rubber`; burnt tyre steel `steel`; barrel and bucket `sheet_metal`; wooden crate `timber`; plastic crate `plastic`.
- **Gaps / wind volumes:** none.
- **Snag hazards:** burnt-tyre cord wire (`hook`, 0.5 mm: the worst fiber trap in the kit), petals on holed drums (`edge`, 0.3 mm), broken crate slats (`hook`).
- **Landable surfaces:** an upright barrel's top (0.585 m) when centred within ±15 cm (houses-and-yard Y11); a plastic crate upright (0.6 × 0.4 m); a lying tyre: no (the leg corners, 0.17 m from the centre, sit just outside the 0.165 m bead hole: effectively not landable, derived); a barrel on its side: no, it rolls.

### Placement rules
- Tyres on shed and garage roofs and on tarps as weights (houses-and-yard), stacked at garages, burnt in yards and at roadsides; barrels at downpipes and garages, tipped in debris; crates in yards, cellars and summer kitchens; all within the vehicle debris fields too (K07's area, the vehicles sheet).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `item` | enum | car_tyre, truck_tyre, barrel, bucket, wooden_crate, plastic_crate | car_tyre | Which item |
| `pose` | enum | lying, upright, on_side, stacked | lying | Resting pose |
| `stack_count` | int | 1–6 | 1 | Tyre stack |
| `burnt` | bool | — | false | Tyre reduced to steel; drum sooted |
| `dents`, `holes` | 0–1, int | 0–10 | 0.2, 0 | Drum and bucket damage |
| `colour` | enum | blue, green, grey (plastic) | blue | Crate colour |
| `seed`, `physics_material`, `lod` | — | — | by item | As K01; barrel and bucket reuse the Y11 meshes |

### Open points
- `rubber` (high friction, compliant) and `plastic`: settled by the physics sign-off (review-materials.md §2). Rolling barrels and tyres: runtime objects (shared open point).

## K10 Fabric and cables hanging from ruins

**Seen in:** K (patterned wallpaper strips sagging in swags across a top-floor room, about 3–4 m; a purple fabric hanging about 0.5 m over a slab edge; curtains and sheets in the open rooms; pale cables hanging 1–3 m from slab edges; white film on the rubble).
**Research sources:** none; sizes are everyday objects (assumed). The cables are L05's.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Curtain | 1.4–2.6 m drop × 1–3 m wide; tulle (sheer) or heavy | 2.4 × 1.5 m | assumed |
| Wallpaper strip | 0.53 m wide × 0.5–2.7 m, hanging or in swags | 0.53 × 1.5 m | assumed (the common roll width) |
| Swag across a room | 2–4 m span, 0.3–1 m sag | 3 m, 0.6 m | measured K |
| Wall carpet | 1.5 × 2 to 2 × 3 m, 5–10 mm thick | 2 × 3 m | assumed |
| Fabric over a slab edge | 0.3–1.5 m drop | 0.5 m | measured K |
| Insulation and film | 0.5–2 m pieces | 1 m | assumed; measured K (film) |
| Hanging cables | as L05 | — | assumed, as L05 |

### Construction and materials
- Household textiles and wall finishes torn loose by blast and fire: curtains blowing out of window openings, wallpaper peeling in strips and swags, wall carpets hanging by one corner, bedding and clothes over slab edges, mineral wool and film from roofs; all dusty, many sooted.
- **Materials:** Poly Haven `crepe_georgette` (tulle), `floral_jacquard` (curtains), `dirty_carpet` (wall carpets) (candidates, CC0: https://polyhaven.com/a/crepe_georgette , https://polyhaven.com/a/floral_jacquard , https://polyhaven.com/a/dirty_carpet ); wallpaper a plain patterned tint with a paper backing; film as K05.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Purple fabric over a slab edge | #90808e | — | measured K |
| White film | #b7bdc2 | — | measured K |
| Wallpaper, faded | #c8b89a | — | assumed |

### Variants and states
- Pieces: curtain, tulle, wallpaper strip, wallpaper swag, wall carpet, sheet over an edge, insulation, film; each with 0–3 L05 cables added. States: clean, dusty, sooted, shredded.

### Damage pattern
Blast pulls curtains out through the window openings and strips wallpaper from the walls; fire chars the lower edges; wind keeps them moving.

### Physics role
- **Collision:** heavy pieces (carpets, sheets over edges) a thin compliant shell (boxes 10 mm); light pieces (tulle, wallpaper, film) no collision of their own, only snags (they cannot stop the drone; a card is never the only thing that stops it, and here nothing should).
- **Material ids:** `fabric`; cables `cable` (L05).
- **Gaps:** a curtain in a window opening does not close the opening's gap; the opening's polygon stays the real clear space.
- **Wind volumes:** a heavy curtain or carpet hanging free: porosity 0.3–0.6 (assumed); light pieces none.
- **Snag hazards:** every free edge and corner (`hook`), swags (`edge` along the lower hem, 2 mm): **fabric wraps props and the fiber**.
- **Landable surfaces:** none.

### Placement rules
- At window openings of burnt and blasted rooms (curtains out), at the open floors and slab edges of the block (K), inside D and X village houses (wall carpets, curtains), on roof timbers (insulation, film).

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `piece` | enum | curtain, tulle, wallpaper_strip, wallpaper_swag, wall_carpet, sheet_over_edge, insulation, film | curtain | Which piece |
| `size_m` | {w, drop} | as above | by piece | Size |
| `attach` | points or edge | — | from the host | Rail, slab edge, nail points |
| `swag_sag_m` | m | 0.3–1 | 0.6 | Swags |
| `blow_out` | 0–1 | — | 0 | How far it hangs out of an opening |
| `tears`, `shred` | int, 0–1 | — | 1, 0 | Damage |
| `pattern`, `colour` | enum, sRGB | per piece: plain, floral, striped, wallpaper print | by piece | Surface print and tint |
| `soot`, `dust` | 0–1 | — | 0, 0.5 | Surface |
| `cables` | int | 0–3 | 1 | L05 cables added with it |
| `seed` | int | — | — | Drape, tears |

### Open points
- Cloth simulation of the snag (a curtain wrapping a prop) is phase 7; phase 1 is static drape plus snag features.

## Open points for the whole sheet

**Physics material ids: settled** by the physics sign-off (review-materials.md §2, §6; none is invented in a generator):
- lifted asphalt slabs and broken edges (RD01): `concrete`; `asphalt` is a surface id only.
- `rubber`: tyres (K09); high friction on the polymer feet, compliant.
- `plastic`: crates, PVC frames, appliance housings (K08, K09); rigid thin shells.
- `fabric` and `asbestos_cement`: tarps, film, curtains, carpets, mattresses and sofas (`fabric`) and slate (`asbestos_cement`).
- Porcelain insulators (L01, L02): `glass`.

**Physics engineer:** the PHY-22 inputs are in "Sag, tension and sway" (D, mass, sag → T, k, sway); tension-free lines (L04, L05) wrap whatever their diameter; whether spans sway with the wind; which debris pieces become runtime objects that rock, slide and roll (planks, sheets, tyres, barrels); L04's support points (anchors with a release force, or friction only).

**World-builder:** wires as map objects written by L03 (D-007); potholes as pitfall features and ruts and road edges as sub-grid relief; the debris spread model (the λ table); hanging cables across openings and the gap polygons (L05).

**Tech-artist:** sub-pixel wire rendering (L03): coverage-weighted, fading as 270·D / d, never thickened; the J and D feed values for wires.

**Other sheets:** V20 (vegetation): J's brush pile is about 1.0–1.5 m high and 3–4 m long with branches 20–60 mm across and 1–3 m long, which matches V20's defaults (no change). Vehicles sheet: the published tracks feed RD03's rut spacing; K07 sits beside the K kit on roads.

**Pilot:** nothing. Every open point here is settled by research, the side-by-side sheets or a role decision.
