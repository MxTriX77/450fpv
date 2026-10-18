# Physics review: material ids and surface materials

**Reviewer:** physics-engineer · **Task:** 1.5 · **Branch:** `unity/art/p1-pipeline-hardening` · **From tip:** `64f11e8`
**Against:** AST-05, AST-10, PHY-01, PHY-03, PHY-05, PHY-17, PHY-19, PHY-22, D-021, D-024, knowledge §7, the spec delta "Surface materials on shapes and the material list grows only with sign-off"
**Read:** `openspec/changes/p1-object-sheets/review-buildability.md` (the collected proposals, at `origin/unity/ref/p1-object-sheets`) and the six sheets' physics and open-point sections; `docs/legacy/map-and-world/surfaces-review.md` §1, §5; `terrain-holes-review.md` §2–§3 (TH-4, F-1…F-8, the lip); `surfaces.json` (the mat values); my `review-physics.md` §5 R3 and P3-5; `assets-src/physics_materials.json`; `tools/blender/lib/meta.py`; `tools/blender/tests/test_meta.py`

I am the consumer of every number here: the 1 kHz contact law (feet, arms, body, coil, ≤ 32 fiber nodes), the fiber's bend-and-snag model, and the log that lets QA name the material in a crash. Each proposal is judged against what those consumers will read, and every number is labelled pilot / measured / published / derived / assumed. Every assumed number sits in the table, where it is tunable.

## Summary

- **The table grows from 8 to 17 ids.** Added: `bark`, `twig`, `deadwood`, `asbestos_cement`, `straw`, `fabric`, `rubber`, `plastic`, `mesh`. Mapped to an existing id: `plastic_film` → `fabric`, EPS `foam` → `plastic`, ceramic (porcelain) → `glass`, `asphalt` as an object → `concrete`, `glass_crumbs` → the `asphalt` surface's cover. Mapped to a surface: `soil` → `surface:belt_bare` (cut and trampled), `surface:crater_spoil` (loose spoil, fresh mounds) or `surface:meadow_sod` (a grassed mound). Declined: `forb_stem` (micro-detail, not an asset shape).
- **One field added:** `break_stress_pa`, optional, for members that snap (`deadwood` carries it). `adhesion` is accepted as a field of the **surface** table (phase 3), not of this table. The buckling load, the hook rate and plate breakability are derived or deferred, each with its reason in §3.
- **`surface:<id>`** is accepted on shapes and slots for exactly the thirteen S01–S13 names; the validator names the rule on anything else. **`asphalt` is a surface id only.** `stubble` is a variant of `tilled`, not a fourteenth id. A plain id never shares a surface's name, and the loader enforces that.

## 1. Rules this sign-off fixes

1. **Two namespaces.** A plain id is a row of `physics_materials.json`: an object's contact material. `surface:<id>` names a row of the map's surface table (knowledge §7.2): the shape then follows the ground law of that surface (elasto-plastic Winkler soil, sink, unload ratio, ploughing, footprints, the wet rules of D-021 and the adhesion of §3), and its contacts carry `Surface` = that index with `Material` = `NoMaterial` (TH-4). A plain id never shares a surface's name, so a logged contact is never ambiguous between ground and object.
2. **What takes `surface:<id>`:** a shape that stands in for ground. Hole fillers under the first attempt's contract F-1…F-8 (trench walls and floors, the pit, dugout stairs), cut banks, cellar and dugout mounds, the walkable top faces of a rubble mound (U02: `surface:rubble`, yes), the slopes and bottom of a collapsed trench (E03: `surface:belt_straw`, `surface:weeds`). **Never** a lifted or loose piece (an asphalt slab, a clod, a tile): those are objects with plain ids. A surface shape carries the row's **soil block only**: it has no mat, cover or stems of its own (a straw layer built on a shape is the object id `straw`; the terrain's cover blends over a flush lip as the hole contract's authoring advice says).
3. **The soil lip** is a physics constant, 0.02 m (TH-4, X-11): no field. A fiber over a soil lip never breaks by bending (legacy §2.4); its danger is the capstan factor.
4. **Edge radius on round shapes.** `edge_radius_m` is the arris radius of a **box**. A capsule, cylinder or sphere always bends the fiber over its own radius, and a snag's `radius_m` overrides both. `null` is allowed only for a material that is never put on a box (`cable`, `foliage`, `bark`, `deadwood`). A generator must not put a null-edge material on a box; the collision audit can check that cheaply once it reads the table (suggestion, not done here).
5. **Labels.** One label per row, for the headline number; the rationale says which parts are published, derived or assumed.

## 2. The proposals, one row each

Values are μs / μk; stiffness N/m with ζ (absent = rigid, in series with the drone part's compliance); edge radius; `break_stress_pa` where it applies.

| Id | Proposed by | Verdict | Values | Label | Why |
|---|---|---|---|---|---|
| `bark` | vegetation | **added** | 0.55 / 0.45; rigid; edge own radius | assumed | Live trunks and limbs are capsules. Furrowed and plated bark interlocks with a printed foot's layer ridges, so above sawn `timber` (0.45 / 0.35); the published 0.2–0.5 of polymers on wood is for planed faces. Its own id so the log says "tree", not "plank" |
| `twig` | vegetation | **added** | 0.50 / 0.40; 30 N/m, ζ 0.2; edge 2.5 mm | derived | The crown contact shell over the 5–10 mm cards. One 10 mm green twig 0.8 m long: E ≈ 10 GPa (published 8–12), I = πd⁴/64 = 4.91e-10 m⁴, k = 3EI/L³ ≈ 29 N/m (the sheet's arithmetic, reused). A 20 N push goes through the shell to the branch capsules: the shell cushions, the capsules stop. ζ assumed lower than `foliage` for bare twigs. The fiber bends over the thinnest twig the shell stands for (2.5 mm): E·r/R ≈ 1.8 GPa, above the ≈ 0.7 GPa proof, so a fiber hauled hard through a crown may break, as the pilot says tree snags do |
| `deadwood` | vegetation, earthworks (wattle) | **added** | 0.45 / 0.35; rigid; edge own radius; `break_stress_pa` 4.0e7 | assumed | Friction as weathered timber. Snaps at M_b = σ_b·π·r³/4 from the capsule's own radius: a 10 mm dead twig at 3.9 N·m, 8 N at a 0.5 m tip (the sheet's figure). σ_b 40 MPa assumed under the published 60–110 MPa rupture of dry hardwood, for checking and decay. Hollow elder and pithy stems (V14 `dry`) use the same row: the solid-section formula overstates a half-diameter pith by (1 − (rᵢ/rₒ)⁴)⁻¹ ≈ 1.07, inside the σ_b spread, so yes, they break |
| `forb_stem` | vegetation | **declined** | — | — | Tall weed stalks within 3 m are micro-detail cantilevers (AST-10, terrain.md §7): `MicroDetailNear` returns them from the surface's cover block (`weeds`: 5–15 mm, 0.5–1.5 m, hook 5–30 N; knowledge §7.3) at 0 mm parity with the visual. k = 3EI/h³ and the buckling cap π²EI/(4L²) are derived from the stem's E, d and L, not a table row. No asset shape ever carries it; a V17/V18 clump asset writes no stem collision |
| `asbestos_cement` | houses, lines, urban | **added** | 0.60 / 0.50; rigid; edge 3 mm | assumed | A cement-bound surface under a polymer foot, as `concrete`. A 5.8 mm 8-wave sheet (published 1750 × 1130 × 5.8 mm, 40 mm waves) is rigid under a foot: E ≈ 15 GPa, I ≈ t·a²/2 ≈ 1.16e-6 m⁴ per metre, a 0.3 m strip over a 0.6 m batten span gives 48EI/L³ ≈ 1.2e6 N/m, so the leg sets the bounce. Edge = half thickness (derived). Breakage is a crash case, §3 |
| `soil` | houses, earthworks | **mapped to `surface:<id>`** | — | — | A soil shape must follow the same elasto-plastic law, sink, ploughing, footprints and wet rules as the terrain; one stiffness and ζ cannot express plasticity, `max_sink` or D-021's wet variant, and two soil models of one soil would diverge. Cut walls, floors, stairs: `surface:belt_bare` (F-7); loose spoil and fresh mounds: `surface:crater_spoil`; a grassed cellar mound: `surface:meadow_sod`. E01 is unblocked by the validator change |
| `straw` | houses, earthworks | **added** | 0.45 / 0.35; 490 N/m, ζ 0.3; edge 20 mm | derived | Thatch, hay, a stack, straw carried by a mesh or a roof. The `belt_straw` mat (modulus 4 kPa, `surfaces.json`) 0.125 m deep under the 15 mm reference foot: km = modulus·π(b/2 + D/2)²/D = 4000 · 0.01539 / 0.125 ≈ 490 N/m (the sheet's figure, reused). μ and ζ carried from the mat. The fiber sinks into a straw edge, so the bend radius is the soil-lip figure (assumed). Hooking on liftoff comes from the `hook` snags the generator writes for bundle ends, released in the surface table's 1–5 N band. Straw **over ground** is `surface:belt_straw`, not this id |
| `fabric` | houses, lines, vehicles, urban | **added** | 0.40 / 0.30; 600 N/m, ζ 0.8; edge 5 mm | assumed | Tarps, curtains, mattresses, sofas, seats, the canvas tilt. Friction above plastic on plastic (published 0.2–0.4) for the weave. Stiffness from the sheets' 50–100 mm sink of a mattress or seat under the drone's 49 N: 490–980 N/m. ζ 0.8: a cushion that does not bounce. Edge: a hem or a fold. Wrapping a prop is a phase-7 prop-strike rule by material class, not a field |
| `plastic_film` | houses | **mapped to `fabric`** | — | — | Greenhouse film: the same behaviour the sheets name (soft, wraps), a film span's stiffness set by its frame like a tarp's, no cutting edge. The 0.1 of friction between them is inside the assumption. The log names the object, so the id need not |
| `rubber` | lines, vehicles | **added** | 0.70 / 0.60; 5e4 N/m, ζ 0.3; edge 10 mm | assumed | Tyres. A polymer foot on tread rubber, inside the published 0.6–0.9 of rubber on dry solids. Tread rubber (E ≈ 5 MPa, 10 mm) under the 15 mm foot compresses at EA/t ≈ 9e4 N/m and the carcass flexes under it: 5e4 N/m together. A tyre is a cylinder, so the fiber bends over its own radius; 10 mm stands for a rubber box's rounded shoulder. A flat tyre is softer; one row for now |
| `plastic` | lines, vehicles, urban | **added** | 0.30 / 0.25; rigid; edge 1.5 mm | assumed | PVC frames, crates, housings, trim, bumper covers, packaging. Polymer on polymer, published 0.2–0.4. Profiles and crates are rigid under a foot; a 3 mm PP bumper cover flexes (≈ 1e2 N/m over 0.5 m) but has the steel beam behind it. A moulded edge 1–2 mm |
| EPS `foam` | urban | **mapped to `plastic`** | — | — | EPS yields at 70–100 kPa (published grades), under the 141 kPa of the 25 N reference foot, so a foot crushes it; modelling the crush needs a yield model no phase-6/7 requirement asks for, and the remnants are small. The deviation is recorded here: a remnant resists like a rigid plastic shell. Revisit if a crushable id is ever wanted |
| `mesh` | houses (Y03) | **added** | 0.35 / 0.28; 800 N/m, ζ 0.5; edge 1 mm | derived | Chain-link bays as thin boxes. `cable` is rigid locally and wrong for a bay: a 2 mm galvanised mesh strung at ≈ 200 N/m (assumed) over a 3 m bay answers a drone-sized point load as a membrane, k ≈ 2πT/ln(a/r) = 2π·200/ln(1.5/0.3) ≈ 780 N/m, so the drone is caught, not bounced. μ as `steel`. The fiber bends over a 1 mm wire (≈ 4 GPa): a cut risk, unlike a tarp, which is why it is not `fabric` |
| `glass_crumbs` | vehicles, earthworks | **mapped to the `asphalt` surface's cover** | μ 0.30 / 0.20 under the crumb mask (surface table) | assumed | A 10 mm crumb is smaller than a foot; the layer is a cover of the ground it lies on (S11's crumb cover, the same values) plus K07's decal and micro-detail. Not an object id. Crumbs on a roof or a seat stay visual (a deviation: a landing on a crumb-covered roof does not slide more) |
| ceramic (porcelain) | lines, urban | **mapped to `glass`** | — | — | Insulators are cylinders, so the fiber bends over their own radius and `glass`'s blade edge never applies; a glaze's friction is glass's. No physical difference remains that a row would carry. L01, L02 and P05 move from `masonry` to `glass` for porcelain (the sheets' `masonry` stand-in gave a glaze the grip of brick) |
| `asphalt` (object) | lines | **mapped to `concrete`; `asphalt` is a surface id only** | — | — | Lifted slabs and broken edges: friction and a 3 mm arris against a 4 mm aggregate edge are inside `concrete`'s assumption. One name in two tables would make a logged "asphalt" contact ambiguous and give the phase-3 compiler two rows of one name; the loader now refuses a plain id that is a surface name |
| `stubble` (surface) | earthworks | **a `tilled` variant, not an id** | — | — | The soil is `tilled`'s; only the cover differs (standing stubble 500 /m², windrows as a straw mat). Variants of one row are the surface table's cover block (phase 3). `surface:stubble` is rejected by the validator |
| `asphalt`, `concrete_slab`, `dirt_track` (surface rows) | earthworks | **accepted as ids now; values at phase 3** | — | — | The names are the thirteen the spec fixes. Their rows live in the map's `surfaces.json` and get their sign-off with it. Preliminary check: the proposed bearings (1e8, 1e8, 3e7) give 17.7, 17.7 and 5.3 kN/m at the reference foot, all under the 26.5 kN/m stability bound (knowledge §7.1) |

## 3. Fields

| Proposal | Decision | What reads it |
|---|---|---|
| `break_stress_pa` (the sheets' break moment on `deadwood`) | **added, optional.** The bending rupture stress of a member. The contact law derives the break moment from the capsule's or cylinder's own radius, M_b = σ_b·π·r³/4; past it the shape stops resisting for the rest of the flight and a `break` event is logged with the object, shape index and material; a `hook` snag of that material lets go at the same moment. Absent means the member never breaks | The phase-7 contact law (PHY-05) and the fiber's snag release (PHY-19). Added now rather than later because the sheets already derived the number and a rigid dead twig would be an immovable bar; it costs the assets nothing, since the field sits on the row |
| plate breakability (tile, slate, glass, the pit's lip X-8) | **deferred** (P3-6 stands). A plate's rupture depends on its span and thickness, which are the shape's box dimensions, not the material. When the plate model comes, the same `break_stress_pa` serves on boxes (σ = M/Z from the box's thinnest side and its longest span), so no schema change is needed then. Checked: a landing loads a sound slate sheet to ≈ 0.4 MPa against a published rupture ≥ 16 MPa, so breakage is a crash case (an arm at 5 m/s: 7–14 MPa), and P02's roof may be flagged landable where sheets are whole | the phase-7 contact law, later |
| `adhesion` (earthworks, D-021) | **accepted as a surface-table field, phase 3,** not of this table: `adhesion_pa` on the soil block, acting only wet. Pull-off per foot = adhesion_pa × the wetted area (sole plus the sunk side wall, 1.6e-3 m² for a 15 mm foot sunk 30 mm) × `wetness`, released over 10–30 mm of lift. The sheet's 4 N default is 2.5 kPa, assumed, inside the few-kPa soil–metal adhesion of terramechanics. No object material sticks to a foot: wet straw clings through the mat's hook-release factor | the phase-7 leg solve (PHY-01, PHY-03, PHY-17): "legs stick most of the time in rain" |
| wet factors, `wetness` 0.6 / 1.0 | **phase 3,** with the surface table | PHY-17 |
| buckling load (`forb_stem`) | **declined:** derived from E, d and L of the cover's stems | micro-detail (AST-10) |
| a shell's hook rate (`twig`) | **declined:** the crown's hooking comes from the snag records (every capsule under 30 mm an `edge`, every free tip a `hook`), and the fiber's drag inside a crown from the co-located wind volume's porosity (solidity × the twig radius), which the phase-7 fiber model reads | PHY-19 |
| tile and slate `landable` | informational, as before; the contact law ignores the flag | — |

## 4. `surface:<id>` in the validator

- Accepted, on `collision[].material` and `materials[].physics`: exactly `surface:meadow_sod`, `dry_crust`, `crater_spoil`, `belt_straw`, `belt_bare`, `tilled`, `burnt_field`, `weeds`, `yard_litter`, `rubble`, `asphalt`, `concrete_slab`, `dirt_track`. Anything else with the prefix is rejected naming the rule: `surface material 'surface:nonsense' is not one of the thirteen surface ids surface:{…}`. Plain ids are checked against the table as before.
- `meta.SURFACE_IDS` holds the names until the phase-3 map package has `surfaces.json`; then the compiler checks the same names against that file and the two lists must be equal.
- **For the phase-3 compiler:** copy `physics_materials.json` into the package and hash its bytes (P3-1); emit `Surface` = index for a `surface:` shape and `Material` = `NoMaterial` (TH-4); author the three new rows with the earthworks sheet's values and the wet rules; keep F-1…F-8 for every filler.
- **For the phase-6/7 contact law:** a surface shape uses the soil law of its row with no mat and no stems; at a lip, contacts within 45° of each other merge and the deepest wins (TH §3); the lip radius is 0.02 m; the log carries `Surface` so QA can tell "trench wall" from "parapet".

## 5. Other sign-offs the sheets asked for, settled by calculation

- **Vehicle fragment holes ≤ 0.25 m stay closed in collision:** accepted. One prop disc is 0.254 m, the inner panel sits 80–120 mm behind, so nothing but a blade tip enters; the petals stay `edge` snags (r 0.35 mm, a cutting edge).
- **The 12° landable bonnet limit:** a foot holds statically up to atan(μs) = 16.7° on `sheet_metal` (0.30), so 12° leaves margin dry; the flag is informational and the contact law decides in any case.
- **Ceiling effect in a 2.72 m room:** with R = 0.127 m, a hover at mid-height is 9.4 R below the slab; the effect is measurable within about 2 R (0.25 m) of it and grows fast there (PHY-08 covers it). Rooms are flyable; hugging the slab is not.
- **Capsule budget per tree (150–300):** no limit from physics. The per-step batch (knowledge §10.5) is 46 swept capsules against the near set, so the cost is the world's broadphase, not the count; sign-off at the phase-3 API review.
- **Runtime objects** that rock, slide and roll (planks, sheets, tyres, barrels): no phase-6/7 requirement asks for them; everything is static for now. An orchestrator decision if wanted later.
- **Wire sway with the wind, L04's anchors, the cavity wind fraction, clods as pieces (X-1):** mine, in phase 3 and 7d; recorded, not decided here.

## 6. Mapping for the generators

| The sheets write | The generator writes |
|---|---|
| `proposed:soil` (cellar mounds, clay bench, dugout covers, trench fillers) | `surface:belt_bare` (cut, trampled), `surface:crater_spoil` (loose), `surface:meadow_sod` (grassed) |
| `proposed:straw` (thatch, hay, stack) | `straw` |
| `proposed:fabric`, `proposed:plastic_film` | `fabric` |
| `proposed:rubber` | `rubber` |
| `proposed:plastic`, EPS remnants | `plastic` |
| `proposed:asbestos_cement` | `asbestos_cement` |
| chain-link mesh and its top wire (Y03) | `mesh` for the bay panels; `cable` for the top wire capsule |
| porcelain insulators (`masonry` stand-in) | `glass` |
| `proposed:asphalt` on lifted slabs | `concrete` |
| `proposed:glass_crumbs` | nothing on the asset: the ground's `asphalt` cover (phase 3) plus K07's decal |
| `bark`, `twig`, `deadwood` | themselves |
| `forb_stem` | nothing: micro-detail from the surface cover |
| `surface:rubble` (U02 top), `surface:weeds`, `surface:belt_straw` (E03) | as written |

## 7. Evidence

```
$ git switch unity/art/p1-pipeline-hardening && git log --oneline -1
64f11e8 docs(openspec): add physics sign-off and surface ids to the change

$ python -m pytest tools/blender -q            # before any change
78 passed in 42.51s

$ python -I -c "... meta.validate on K01's metadata with the shape material swapped ..."
table ids: 17 ['asbestos_cement', 'bark', 'cable', 'concrete', 'deadwood', 'fabric', 'foliage', 'glass', 'masonry', 'mesh', 'plastic', 'rubber', 'sheet_metal', 'steel', 'straw', 'timber', 'twig']
surface:belt_bare: accepted
surface:nonsense: rejected: collision[0]: surface material 'surface:nonsense' is not one of the thirteen surface ids surface:{meadow_sod, dry_crust, crater_spoil, belt_straw, belt_bare, tilled, burnt_field, weeds, yard_litter, rubble, asphalt, concrete_slab, dirt_track}
surface:stubble: rejected: collision[0]: surface material 'surface:stubble' is not one of the thirteen surface ids surface:{...}
masonry: accepted
```

The full-suite run and the K01 byte comparison after the change are in the handoff.
