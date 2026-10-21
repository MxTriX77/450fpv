# Review: p1-r01-vaz-sedan (task 2.1)

**Reviewer:** qa-engineer, fresh (did none of the work, D-034) · **Branch:** `unity/art/p1-r01-vaz-sedan` · **Tip:** `d4d5805` · **Range:** `884a5f2..d4d5805` (41 commits, 309 files; forked at `e21cb50`, the trunk merged in at `337ff78`)
**Requirements cited:** AST-02, AST-03, AST-05, AST-06, AST-07, AST-08, AST-12, REAL-01, REAL-03, REAL-05, SEC-02, SEC-04 · **Decisions:** D-003, D-004, D-020, D-028, D-032, D-038, D-040
**Read:** `CLAUDE.md`, `AGENTS.md`, `docs/workflow.md`, the `karpathy-guidelines` skill; D-040; the whole change folder (proposal, design with every as-built note, both spec deltas, `tasks.md` with the as-built notes of 1.1–1.5, the notes for 1.4 and the note under 2.1); `docs/object-sheets/vehicles.md` (the shared rules, the gap table, the shared physics, colours, debris, the `vehicle` table, §R01); `docs/requirements.md` for every cited ID; the E01 review for the format; the diff of `generators/{vehicle,debris_car}.py` (the parts this review leans on: `Kit`, the plan builders, `_micro`, `_snags`, `_glass`, `_rack`), `lib/{kits_vehicle,meta}.py`, `build.py`, `CatalogImporter.cs`, the shaders' property blocks, every recipe, the params, the catalog and the manifest, and every new test.

Every check below was run by QA in its own worktree (`git lfs checkout`: 132/132 objects; no `unity/Library/` before the first Unity run). Nothing from a handoff was reused. QA's own scripts lived in its scratchpad and were never committed: `qa_r01_meta.py` (the shipped metadata against the generator's named plan), `qa_roof.py` and `qa_settle.py` (the lean), `qa_holes.py` (a ray per hole per LOD), `qa_k07.py`, `qa_gain.py` (the tint encoding of every recipe), `qa_colour.py` and `qa_colour_f.py` (rendered colours), `qa_crops.py` (full-resolution crops of the sheets). The side-by-side was composed in the worktree's git-ignored `reference/_compare/` from M's still and zoom crops and O's truck crop, read, and deleted. Tools: Unity 6000.3.26f1 (Hub running, every call piped), Blender 5.2.2 LTS, Python 3.14.6 with Pillow. Every Blender and Unity run started on AC (`BatteryStatus.PowerOnline` True before each, Balanced plan; the session began on battery and waited for AC), on the RTX 4050 (`GPU NVIDIA GeForce RTX 4050 Laptop GPU (Direct3D12)` in each capture log). **No performance claim is made.**

## Verdict: CHANGES REQUESTED

Four blocking items, none of them a look item (D-040):
- **R1. The flat-tyre lean is not a sprung pose, and misses the spec's numbers** (physics, spec, tests). The body turns rigidly about the diagonal, so the rear-right corner rises the full 0.05 m. The across lean is 2.12° against the requirement's 1–2°, and the roof stands 1.449 m against the scenario's 1.40–1.44 m. The tests allow 2.2° and read the roof on the clean base only.
- **R2. A's LOD0 draws 78 467 triangles against the 60 000 budget** (AST-08). The car's 54 467 plus the crumbs' 24 000: a micro mesh renders with its LOD, and the E01 review counted its micro meshes into the same budget.
- **R3. The intact side panes collide as `sheet_metal`, not `glass`** (AST-05 metadata). They sit inside the greenhouse's band boxes, in A (the far side) and in the clean base (all eight). The proposal, the design and the sheet ask for glass boxes for every pane still in place.
- **R4. Four spec lines the build does not meet as written** (spec text, the orchestrator's): the cull against the pixel rule; "every face within 2 cm" against AST-05's zone; K07's "boxes or capsules … and edge or hook snags" against the bowl's sphere with no snag; and "the body's bounds" read from metadata whose bounds include the scatter.

Everything else passes:
- two builds of R01 and of K07, the logs' generator and audit lines identical, the review sheets byte-identical, `git status` clean after each
- K07's shared chrome moves only its three chrome pieces' sheets (§8)
- K01, S04, L01, L03 and E01 rebuilt with `git status` clean
- the shipped metadata equal to the generator's named plan, 188 (A) and 224 (F), pinned by group
- the landable flags, the snag kinds, the materials apart from R3, the holes as geometry at LOD0 and LOD1
- the crack and damage masks bound, the car's colours following its recipes
- EditMode 66/66 on AC, `pytest` 908 passed, both `test_existing_tree_is_clean` targets, `fetch.py --verify`, the OPSEC scan, `openspec validate --strict`, the commit hygiene

The look misses are **G1 stand-ins** (21 lines below), not blocking items, per D-040.

Task 2.1 stays unticked.

## Blocking items

### R1. The flat-tyre lean turns the body rigidly about a diagonal; a sprung car settles otherwise
**Measured** (`qa_settle.py`, `qa_roof.py`; the body's own points over the four hubs, R01_A's params):
```
as built (rigid about the diagonal): body over the hubs front_left -0.0500, front_right +0.0000, rear_left +0.0000, rear_right +0.0484;
                                     across 2.12 deg, along 1.16 deg, tilt 2.42 deg; roof top 1.4491 m
four equal springs (heave -d/4, half turn): body over the hubs front_left -0.0375, front_right -0.0125, rear_left -0.0125, rear_right +0.0117;
                                     across 1.06 deg, along 0.58 deg, tilt 1.21 deg; roof top 1.4273 m
roof boxes (landable) as built: top corners up to 1.4612 m (roof_2_right), 1.4528 m (roof_1)
```
**Why it fails:**
- **The physics.** `kits.lean` turns the body about the front-right to rear-left diagonal until the flat corner's hub is 0.05 m down. That leaves the rear-right corner 0.048 m up over a wheel still on the ground, so its spring is unloaded by k × 0.048 m and nothing takes the load back. No car on four springs comes to rest there. With equal corner rates the support's 0.05 m drop splits into a heave of −d/4 plus a roll and a pitch of d/2 each (the warp is taken by the springs). The flat hub still drops the tyre's 0.05 m.
- **The requirement:** "one flat front tyre dropping that corner 0.04–0.08 m with a 1–2° lean". As built it is 2.12° across, and the plane tilts 2.42°.
- **The scenario "Published dimensions":** "the roof at 1.40–1.44 m above the ground on the unflat corners". The skin reaches 1.449 m over the rear-right quarter, and the landable roof boxes reach 1.461 m.
- **The tests:** `test_a_flat_corner_drop_and_lean` bounds both angles by 2.2°, wider than the spec's 2°. `test_published_dimensions` reads the roof of the clean base, not R01_A's, which is what the scenario reads.

**To close it** (asset-artist; QA recommends the physics):
- **Either** settle the body on its springs: `lean` turns through half the angle and heaves by −d/4, and the flat wheel sits on its hub at −d. With equal rates that is tilt 1.21° and the roof at 1.427 m. Then rebuild with the audit, and test R01_A's roof against 1.40–1.44 m and its tilt against 1–2°.
- **Or** the orchestrator amends the requirement and the scenario to the rigid model's numbers (about 2.1° across, the roof to 1.45 m), and states why.

### R2. A's LOD0 draws 78 467 triangles; the budget is 60 000
**Measured** (both builds' lines):
```
R01_A: ... tris 54467 / 12335 / 3460 / 72  micro R01_A_LOD0_glass_crumbs 2000 glass_crumbs 24000 tris  188 collision shapes
[CatalogImporter] R01_A: ... 4 LODs, R01_A_LOD0_glass_crumbs (2000 glass_crumbs, LOD0) micro, ...
```
**Why it fails:**
- **The Budget scenario:** "LOD0 is under 60 000 triangles".
- **The pipeline:** "the prefab's LOD0 renders both meshes" (`asset-pipeline`, visual-only micro meshes).
- **The class's reading:** the E01 review counted E01's straws and grass into the same 60 000 (39 273 + 5 796 + 13 828 = 58 897).

R01_A draws 54 467 + 24 000 = **78 467**, 31 % over. The crumbs are 12-triangle boxes 10 × 10 × 4 mm, whose sides are 4 mm tall.

**To close it** (either):
- **The asset-artist fits it.** For example, crumbs as 2-triangle chips (2 000 × 2 = 4 000, LOD0 58 467, 1 533 under), or fewer and cheaper crumbs. The crumbs are visual only and are the `asphalt` surface's cover in the sheet, so nothing physical changes. G1 stand-in 10 (the crumb carpet) can be eased in the same pass.
- **Or the orchestrator amends the scenario** to give micro meshes an allowance of their own, and says how E01's reading changes.

### R3. The intact side panes collide as `sheet_metal`
**Measured** (`qa_r01_meta.py` on the shipped metadata):
```
R01_A glass: windscreen_0_left .. windscreen_2_right (6), rear_screen_0 .. rear_screen_2 (3)            - no side pane
R01_A far side (intact glass): band_a_right [0.583 x 0.32 x 0.018], band_mid_right [1.262 x 0.443 x 0.018],
                               band_drop_right [0.298 x 0.429 x 0.018], band_c_right [0.491 x 0.264 x 0.018]   all sheet_metal
clean base: band_a/mid/drop/c _left/_right, 0.12 m deep, sheet_metal, over all eight side panes
```
**Why it fails:**
- **What the change asks for:**
  - the proposal: "glass boxes while in place"
  - the sheet's shared physics: "glass that remains as thin boxes (0.005 m)", and "remaining glass panes and the crazed windscreen: `glass`"
  - AST-05: a material id per shape
- **What is built:** the windscreen and the rear screen have their glass boxes. The side panes are covered by the bands' boxes, which hold pillars and panes alike.
- **Why it matters:**
  - A drone touching A's far-side window, or any window of a clean car, meets `sheet_metal` (0.30 / 0.25, 2·10⁴ N/m compliant) instead of rigid `glass` (0.40 / 0.30).
  - The breakable glass the deferred X-8 will need cannot find these panes.
  - The generator is R02–R09's template, so the miss would carry to every car.

**To close it** (asset-artist): on each side whose panes are in, the band becomes its frame members in `sheet_metal`, as the open side's `band_post_*`, `band_sill_*` and `band_top_*` already are, plus a 5 mm `glass` box per pane. Then rebuild with the audit, and pin the new counts by group (clean base, A, F).

### R4. Spec lines the build does not meet as written
For the orchestrator (one line each, at its call; none needs a rebuild unless it prefers the build to change):
- **(a) The cull.** The Budget scenario says "the cull distance follows the pixel rule from the car's height". The rule (`lines-roads-debris.md`: a size s drops under 2 px at 935 · s / 2) gives 673 m from 1.44 m, or 725 m with the rack. The build culls at 1 500 m (0.90 px), and `test_lod_budget` pins 1 500. 1 500 m is the better choice for a long, low object, since at 673 m a 2 × 5.7 px car would pop out. So QA suggests writing the reason into the scenario, for example 2 px of its length (1 904 m) or 1 px of its height (1 346 m), rather than moving the build.
- **(b) "Every face the drone can touch within 2 cm (roof, bonnet, boot lid, door edges, pillars, the flap)".** The audit holds 2 cm within 0.3 m of the landing faces and 5 cm elsewhere, which is AST-05's rule. Outside that zone its worst lines are 49.6 mm: `R01_A collision[56]` at (−1.002, 0.780, −0.755), and `[7]` and `[13]` at (0.654, 0.807, −0.769) and (0.654, 0.863, 0.729). That passes AST-05 but not the sentence. Amend it to AST-05's terms.
- **(c) K07: "with their own boxes or capsules … and `edge` or `hook` snags".** `K07_lamp_bowl` collides as a sphere and carries no snag, as design 9 specifies (a hemisphere, `steel`). Amend it to "primitives" and "snags where the piece has an edge or a free end".
- **(d) "WHEN R01_A's metadata is read THEN the body's bounds are 4.073 × 1.611 m".** The metadata's bounds hold the scatter (6.631 × 1.592 × 3.773 m). The body's own mesh without its pieces is 4.076 × 1.635 m, which passes (§1). Amend it to say what is read.

## G1 stand-ins (D-040: read out to the pilot at G1, not a rework round)
**The A car against M:**
1. The holes' halos read as round orange rings with long rust drips. M's are ragged pale primer-and-rust flakes with few streaks.
2. M's torn hole high on the front wing behind the headlamp is not built. Holes keep clear of the bonnet's landing zone.
3. M's tear at the front door's leading edge is not built.
4. There are two extra large holes, low on the front door and on the rear door, and a large wing hole where M has only a small hit.
5. The folded bonnet flap is a low flat triangle. M's stands tall against the windscreen and curls at its torn edge.
6. The engine bay under the flap is a dark block. M shows rusty orange parts.
7. By day the windscreen reads clear with faint cracks. M's is frosted pale across, with dark hit marks.
8. The wheels read as flat pale discs. M's show a steel rim ring round a smaller domed cap.
9. The chrome bumpers read as flat light-grey plastic. M's are dusty chrome with reflections.
10. The glass crumbs form a bright rectangular carpet on the ground beside the car. M shows almost none there.
11. The scatter's trim chips read as flat pale paper squares. M's are small irregular chips round one plate.
12. The paint is a touch greener than M's blue-teal, and the dusty bonnet and roof read neutral grey where M's read blue-grey.
13. The roof rack is four straight tubes on straight posts. M's has curved brackets and a ladder-sided rear basket.
14. There is no door mirror.
15. The flat tyre reads only slightly squashed. M's sidewall is folded and bulging at the ground.
16. The grille is a coarse square lattice. M's is a fine bright mesh.

**The F car against O and the sheet:**

17. The soot patch is a ring of hard-edged black polygons, not a feathered patch (the "Burnt look" scenario).
18. The edges of the bare steel on the bonnet and roof step in visible blocks at 1.5 m.
19. The front wheel arches show pale blue-grey liners inside the sooted shell.
20. The shell reads dark grey rather than O's soot-black (linear luminance 0.041–0.048 against O's 0.030).
21. The roof's sag does not show, the doors stay closed where O's cab door hangs open, and the rack does not sag.

**Deliberate, not stand-ins:** the plates are blank (SEC-02), and the ground is the gallery's, not M's dusty asphalt or O's snow.

## Suggestions (not blocking)
- **S1. `build.py`'s stale-texture rule will delete V01's impostors at the V01/R01 merge.** It removes every `<variant>_*.png` beside the metadata that the generator did not list under `textures`. V01's `build.py` writes `<variant>_impostor.png`, `_impostor_normal.png` and `_impostor_smoothness.png` there, outside `textures`, so a merged rebuild would delete them and their `.meta`, and with them their GUIDs. The merge task must count them as written. Better, the rule should remove only the files the previous metadata's `textures` listed.
- **S2. The LOD group and the near tile are framed on the scatter.**
  - **The LOD group:** R01_A's bounds hold the K07 pieces, so the reference point lies 1.68 m from the car's centre towards the struck corner (`m_LocalReferencePoint {x: -1.28, y: 0.80, z: -1.08}`, size 6.631 m). LOD switches are measured from there.
  - **The review sheet:** the "1.5 m" tile is taken 4.6 m from that centre, so the car's near look is never reviewed closer than about 4 m.
  - **The fix:** frame both on the body's bounds, as E01's S1 did for its patch.
- **S3. The scatter-density test is sized at about 2σ.**
  - **The tolerances:** at n = 80 the mean of ln r has σ 0.067 and the share within √8 m has σ 0.056. The test's 0.12 and 0.12 are 1.8σ and 2.1σ.
  - **The risk:** the pinned seed passes, but the next change to the RNG stream fails a correct build about one time in ten.
  - **The wrong models:** both targets do reject them. Uniform areal density gives a mean ln r of 1.61, and uniform in r gives 1.38, against 1.04.
  - **The fix:** widen to 3σ (0.20 and 0.17), which still rejects both, or raise n.
- **S4. `PaintedMetal006` is pinned (9.1 MB in LFS) and used by no recipe.** Unpin it, or note it for R02–R09.
- **S5. The rack is 1.2 m across, outside the sheet's assumed 0.9–1.1 m footprint.** M beats the assumed range (D-032). Update R01's row in the sheet, and design 2's "1.3 × 1.0 m", at archive.
- **S6. F keeps a `glass_hull` slot (opaque `car_glass`, physics `glass`) on its LOD2–3 hulls,** where every pane is gone. The dark plane is a fair stand-in for the empty cabin beyond 80 m, but its physics id names glass on a car with none. Name it for what it is.
- **S7. Stale text at archive:**
  - `meta.py`'s "a damage_mask waits for its layered shader" (1.3b bound it)
  - design 3's 0.1 × 0.2 m windscreen hole (built as a 0.20 × 0.03 m slit)
  - design 5's paint tint #2f4a40 with fade 0.3 (built as #30494c, `paint_fade` 0)
  - design 6's opaque far LODs (LOD0–1 now transparent, LOD2–3 `glass_hull`)
  - the sheet's R01 colour row against the retuned recipes

  The as-built notes say each of these, but the design text still reads otherwise.
- **S8. No test checks the tint encoding.** A test that recomputes each set recipe's drawn mean (the tint linearised the way Unity does, times the set's linear mean) against its documented target would have caught task 1.4's bug at build time. `qa_gain.py` is that computation. Hand it to G1 task 1.4.
- **S9. The paint target is more saturated than M.** The recipe's linear R : G : B is 0.44 : 1 : 1.08, while M's three readings run 0.66–0.78 : 1 : 1.09–1.23. The render softens it (the flank measures 0.65–0.79 : 1 : 1.02–1.06), but its blue stays under M's. This is behind stand-in 12, for G1's look pass.
- **S10. AST-08's per-class table still fails no build** (E01's S9). The vehicle row's 60 000 is a `pytest` assertion only.

## 1. Spec scenarios

| Scenario | Result | Evidence |
|---|---|---|
| R01: built from params by one command | **Passed** | Two runs of `pwsh tools/build_asset.ps1 R01`: exit 0 both (19 min including the first import, then 7.5 min). Each writes both states' FBX, metadata, the three masks, prefabs, layered and crazed materials and four sheets. `git status --short` empty after each. The logs' generator and audit lines identical (414 audit lines each). Sheets byte-identical (A day `60C70C59767FA429…`, A overcast `55D9D2C2C835FE6F…`, F day `FF4AF7A862379D6B…`, F overcast `2CEAAA3FE8CB0DFC…`) |
| R01: published dimensions | **Partly met** | §3: the body's mesh 4.076 × 1.635 m in plan (within 2 %), the wheelbase 2.424 m, the tracks 1.348 / 1.305 m, the three unflat wheels 0.594 × 0.165 m (the flat one 0.529 × 0.165 m, flattened to the ground), the flat hub 0.050 m down. **The roof 1.449 m: R1. The metadata's bounds hold the scatter: R4(d)** |
| R01: matches M | **Failed by eye** | §7. Under D-040 these misses are G1 stand-ins 1–16, not blocking items |
| R01: collision and landing surfaces | **Partly met** | §3: plans pinned by group (clean 84, A 188, F 224); landables as asked, the flap not; holes ≤ 0.25 m closed, their petals `edge` snags; tyres `rubber`, seats `fabric` in A; F 0.170 m down on rims of radius 0.183 m. **"Within 2 cm" on every touchable face: R4(b). Side panes not `glass`: R3** |
| R01: burnt look | **Partly met** | Soot-black matte shell (#39393b–#3e3d3f against O's #303033), bare grey-white steel over the bonnet and roof (#9c948d against the sheet's #9a958d), seat frames and springs, a bead-wire ring at each wheel. **The soot patch's hard-edged polygons: G1 stand-in 17** |
| R01: budget | **Failed** | Each further LOD at most half (54 467 → 12 335 → 3 460 → 72; F 41 102 → 8 642 → 2 964 → 72). The five holes of 0.08 m and more are open at LOD1 (§4). Textures 2K at most. **LOD0 draws 78 467: R2. The cull against the pixel rule: R4(a)** |
| K07: pieces built and composed | **Passed** | §8: shapes within 5 mm. R01_A's scatter is a bowl, a ring, a plate and nine chips of it, 1.00–2.53 m from the struck corner, 9 of 12 within 2 m, none in the footprint. R01_F has eight bead-wire rings, two per wheel. **The requirement's wording: R4(c)** |
| Pipeline: crazed windscreen | **Passed** | `R01_A_windscreen_cracks.png` (512 × 1024) under `textures` as `crack_mask` on slot `windscreen` (UVs −0.9615…−0.6015 × −0.62279…0.62279 m). `R01_A_windscreen_crazed.mat`: shader `Fpv450/CrazedGlass` (guid match), queue 3000, `_CrackMask` bound to the mask's guid, the mask imported linear and clamped. The slit is cut from the pane mesh (`_crazed_pane`'s patch) and its rim is an `edge` snag |
| Pipeline: intact pane | **Passed** | The rear screen and A's far side draw with the plain `glass`. No mask is written for them, and none for F (all glass gone). EditMode `PaneWithAMask_GetsItsOwnTransparentMaterial_AndAnIntactPaneThePlainGlass` passed |

**The pipeline delta's other parts:**
- **`textures`:** written on every variant; `[]` on the 17 rebuilt metadata files of seven assets (`a73dc18` adds that one line to each and nothing else).
- **`meta.validate`'s texture rule:** role, slot, `.png`, the corners, one per role and slot.
- **The `damage_mask` binding:** R01_A's `paint` binds `R01_A_paint_layered` (`Fpv450/LayeredSurface`) and R01_F's `soot` binds `R01_F_soot_layered`, the masks imported linear and clamped.
- **The transparent recipe kind:** `glass.json`. A non-palette, another shader or `alpha_clip` is refused (code read; EditMode `TransparentRecipe_BuildsThePlainGlass`, `CrackMaskOnAnOpaqueRecipe_IsRefused`, `UnknownTextureRole_IsRefused`, `MalformedLayers_AreRefused` passed).

**Requirement IDs:**
- **Met:**
  - AST-02 (the sheet is the input, and the sheet's collision line was updated with the ruling)
  - AST-03 (one command, twice)
  - AST-06 (the A and F states are physical edits that carry their collision)
  - AST-07 (UVs in metres, with the paint's panel groups offset by whole 2 m tiles into one canvas; no tiling at 10 m on the turntables; textures 1k, masks 2K)
  - AST-12 (sheets at 1.5 / 10 / 50 / 200 m, turntable and overlay, both lights; S2 on the near tile's framing)
  - SEC-02 (blank plates, front and rear; nothing from `reference/` named)
  - SEC-04 / D-003 (§9)
- **Partly met:**
  - AST-05: R3, R4(b)
  - AST-08: R2
  - REAL-01 and REAL-03: the stand-ins, and no feed yet, so M and O are compared on the review stage
  - REAL-05: §4, the side panes' material (R3)

## 2. Builds, determinism and the other assets
```
R01_1  exit=0 in 19 min    git status lines: 0     R01_2  exit=0 in 7.5 min   git status lines: 0
K07_1  exit=0 in 0.7 min   git status lines: 0     K07_2  exit=0 in 0.7 min   git status lines: 0
K01    exit=0 in 0.5 min   git status lines: 0     S04    exit=0 in 1.4 min   git status lines: 0
L01    exit=0 in 1 min     git status lines: 0     L03    exit=0 in 0.6 min   git status lines: 0
E01    exit=0 in 8.5 min   git status lines: 0
R01 sheets: build 1 == build 2 (4 of 4 byte-identical); K07 sheets: build 1 == build 2 (8 of 8)
```
- **Determinism:** the two R01 builds' generator lines, kit summaries and all 414 audit lines are identical.
- **The shipped metadata:** equal to the generator's named plan within 10⁻⁶ for both states (`qa_r01_meta.py`).
- **The logs:** no compile error or exception. The only stack frames are Unity's warnings about empty asmdefs (Feed, UI).

## 3. Metadata: dimensions, collision, landables, materials, snags, wind
```
clean: 84   lower body 15, tops 4, shoulder rolls 10, greenhouse 23, wheels 6, bumpers 10, interior 8, rack 8
A:    188   lower body 39, tops 11, shoulder rolls 13, greenhouse 50, wheels 6, bumpers 10, interior 19, rack 8, flap 13, scatter 19
F:    224   lower body 53, tops 5, shoulder rolls 8, greenhouse 54, wheels 4, bumpers 10, interior 18, rack 8, bead wires 64
landable clean: bonnet, boot_lid, rack_tube_1, rack_tube_3 (the side rails), roof_front, roof_mid, roof_rear
landable A:     bonnet_front, bonnet_side, bonnet_tear, boot_lid, rack_tube_1, rack_tube_3, roof_0..2 _left/_right
                (flap_0..12 and scuttle_hinge not; bonnet_tear lies on the sheet beside the tear, not over the opening)
landable F:     bonnet, boot_lid, rack_tube_1, rack_tube_3, roof_0..3 _left/_right (the sagged roof, four rows)
body LOD0 without pieces: clean 4.0730 x 1.6238 m (1.611 m body, the handles proud), A 4.0763 x 1.6348 m, F 4.0630 x 1.6238 m
wheels A: front_left r 0.2645 (flat, to the ground) at (-1.4210, -0.6736); front_right / rear r 0.2970 h 0.165 at x -1.4215 / 1.0025
          wheelbase 2.4240 m, tracks 1.3481 / 1.3050 m; F: rims r 0.1830 h 0.130 steel, the body 0.170 m down, roof sag 0.040 m
materials A: shells sheet_metal, bumpers/rack/scatter steel, seats fabric, dashboard and steering wheel plastic, tyres rubber,
             windscreen and rear screen glass (side panes: R3); F: all steel or sheet_metal, soot patch surface:burnt_field
snag A: edge r 0.4 mm x41 (5 rims, 31 petals, the flap's tear and side, the lamp tear's two edges, the windscreen's slit),
        edge 0.5 mm x10 (trim plates), rack 10 mm x8, bumper ends 6 mm x4 + the bezel ring, hook 5 mm x2 (wipers)
snag F: hook 2 mm x58 (seat springs), hook 3 mm x8 (bead wires), edge 3 mm x8 (rim flanges), rack x8, bumper ends x4
wind: lower body 0, greenhouse 0.35 (A) / 0.55 (F); gaps none (doors closed); holes none
```
**The audit** (the build's own lines, both builds identical):

| | Worst primitive → mesh | Worst in the 2 cm zone | Worst mesh → collision | Problems |
|---|---|---|---|---|
| R01_A | 49.591 mm, `collision[56]` | 19.645 mm, `collision[151]` (115 zone lines) | 41.191 mm (the flat tyre's bulge); in the zone 19.858 mm | 0 |
| R01_F | 49.589 mm, `collision[25]` | 19.539 mm (116 zone lines) | 40.187 mm (the open front arch); in the zone 17.160 mm | 0 |

- **Within AST-05:** 13 (A) and 8 (F) primitives stand 45–50 mm off the mesh at their worst point. Those points lie outside the zone, since inside it the audit would have failed them at 20 mm. AST-05 allows them, and R4(b) is about the scenario's wording.

## 4. The geometry audit (REAL-05)
- **Holes as geometry, per LOD** (`qa_holes.py`: a ray across the car at each hole's centre, counting the burst side's skin faces):
  ```
  d 0.150 / 0.118 / 0.089 / 0.086 / 0.086 m (cut): LOD0 0 skin faces, LOD1 0 skin faces   - open at both
  d 0.024 ... 0.010 m (10 decal-only):             LOD0 2 faces (the skin both sides), LOD1 1  - closed, the mask's
  ```
  Each cut hole has a jagged rim and 5–7 petals bent 10–30 mm in. `test_a_petals_reach_the_open_centre` holds each petal's tip within 45 mm of the open centre, so the closed collision stays within AST-05's 5 cm (the stated deviation).
- **The flap:** geometry, folded 146.4° about the bonnet's rear edge until it meets the windscreen. Its collision is 13 thin strips, not landable, with its tear and side as `edge` snags. The opening under it is real: the deck's box lies 0.045 m under the bonnet's mean line.
- **The crazed pane:** a 5 mm pane bulged 12 mm round a 0.20 × 0.03 m slit cut from its mesh, the slit's rim an `edge` snag. The crazing is the mask, as the sheet allows for lines.
- **The struck corner's lamp tear:** cut from the wing as geometry (a dark socket on the tiles), its torn edges `edge` snags.
- **Side glass gone:** the frames are real (`band_post_*`, `band_sill_*`, `band_top_*` on the open side). The crumbs are visual only and the audit skips them (`skipped micro mesh R01_A_LOD0_glass_crumbs (2000 glass_crumbs)`).
- **The overlays** (both states, day and overcast): the collision follows the body, the arches, the flap, the rack, the bumpers and the pieces. In F the seat frames, springs, rims and the eight bead wires all show their snags.
- **Nothing physical is texture-only.** Holes under 30 mm, halos, primer chips, dust, soot and burnt-off steel are masks; the tread is a normal map. The one physical miss is R3's material, not a shape.

## 5. Budgets (AST-08)
```
R01_A: tris 54467 / 12335 / 3460 / 72  + micro LOD0 glass_crumbs 24000      -> LOD0 draws 78467  (R2)
R01_F: tris 41102 / 8642 / 2964 / 72   + micro soot_patch 799 / 447 / 193    -> LOD0 draws 41901
K07:   lamp_bowl 1056/112, bezel_ring 512/128, trim_plate 516/24, bead_wire 512/128
cull 1500 m (R4(a)); switches 20 / 80 / 300 m; textures: sets at 1k, damage masks 2048 x 1024, crack mask 512 x 1024
```

## 6. Colour encoding: the car follows its recipes; one other recipe holds a linear gain
**The car's recipes are sRGB-encoded.** QA took each set's mean albedo in linear light, applied each recipe's tint the way Unity does (exact sRGB below 1, pow 2.2 at 1 and above), and encoded the result:

```
car_paint  drawn #30494c (target #30494c)    car_burnt drawn #303033 (O #303033)    car_soot drawn #3a4043 (sheet #3a4043)
chrome     drawn #9a9fa3 (#9a9fa3)           chrome_burnt #333436 (#333436)         car_bare drawn #9a958d (sheet #9a958d)
tyre       drawn #34383c (sheet #34383c)     seat_fabric #6a7375 (sheet #6d7678)    soot_patch #2d2a27 (#302d2a)
had the tints been gains: car_paint #384b3e, chrome #cbcdcd, chrome_burnt #7b7c7d, car_bare #cbc7c1 (none of them their target)
```

**On screen** (`qa_colour.py`, mean sRGB of small boxes on the 1.5 m tile, beside a neutral ground at #7a7978):

```
recipe car_paint #30494c hue 186.4    the old draw (task 1.4) #204652 hue 194.4, linear B/G 1.38
A flank overcast #465455 hue 183.5 linear 0.69 : 1 : 1.02     A flank day #3b4a4c hue 187.7 linear 0.65 : 1 : 1.06
M shade #3d4b53 hue 201.8 (0.66 : 1 : 1.23)   M lit bonnet #6d7c81 hue 195.0   M roof #8696a2 hue 205.7
F soot flank day #39393b / overcast #3e3d3f   F bare bonnet day #9c948d (sheet #9a958d)   F rim #3d3e40
```

- **The flank's hue is the recipe's (184–188° against 186°), not the old draw's (194°, blue 1.38).**
  - Its lower saturation is the glossy paint reflecting the grey sky, plus the dust film.
  - The colours follow the recipes. How far the recipe itself sits from M is stand-in 12 and S9.

**Other recipes in `assets-src/materials/` that still hold a linear-gain tint (for G1 task 1.4, not a blocker here):**
- **`glass`: tint 0.15 (rgb), the only one.** It multiplies the palette's #a9bdb4, but it is stored as an sRGB colour, so the GPU gets 0.15^2.2 ≈ 0.019 and the pane's body scatters about an eighth of what was written. Its alpha 0.02 is not converted and is right. The `CrazedGlass` shader's default `_BaseColor (0.15, 0.15, 0.15, 0.02)` holds the same value.
- **Cleared, already sRGB-encoded.** QA recomputed each tint from its documented set mean and target, and the drawn means land on the targets within 1–2 levels:
  - `pole_wood`: target #8a8478, drawn #888276
  - `pole_concrete`: target #9a978e, drawn #9a978e
  - `pole_steel`: target #5e4a3a, drawn #5e4a39
  - `belt_soil`: target #4a463f, drawn #4a463e
  - `bark_shrub`: [1.21, 1.261, 1.401] is the sRGB encoding of the gain from #645b47 to #7a7466
  - `leaf_shrub`: [0.709, 0.676, 0.663] is the encoding of the gain from #7f9147 to #58602c
- **The rest** are palettes or ground recipes at tint 1, whose colours live in their derived maps.

## 7. The look (REAL-03, D-038)
- **Read by eye:**
  - both states' sheets in both lights at full resolution: the 1.5, 10, 50 and 200 m tiles, the eight turntable views and the overlays, with crops of the front, the side, the tyre and the ground
  - M's still and its zoom crops (front, rear, debris); O's still and its truck crop (photo letters: no contact sheets)
  - the artist's local comparison sheet, and QA's own side-by-side of M against the A tiles and of O against the F tile, composed and deleted
- **What matches M:**
  - the 2101 at once: the silhouette, the long bonnet, the stance, the crisp shoulder with its highlight, the twin round lamps with the struck corner's lamp gone to a dark socket
  - the barred grille, the bumpers with overriders, the amber indicators, the blank plates front and rear
  - the near side's glass gone with the vent frames in, the seats and the far side's windows seen through the car
  - holes on the front and rear doors with the small hits around them, nothing large on the rear quarter
  - the dark teal-green paint, glossy, with a light dust on the tops
  - the lean onto the flat front corner
  - the bowl, the ring and the plate ahead of and beside the struck corner
  - at 50 m a dark green car with the rack's dots and the holes as dark specks; at 200 m a car-sized speck; the far side clean; no tiling at 10 m
- **What matches O and the sheet:**
  - a matte soot-black shell on dark rims sitting low, with no glass
  - seat frames and springs inside
  - bare grey-white steel over the bonnet and the cabin roof, from the sheet; O shows only fresh soot
  - sooted chrome and rack
  - at 50 m a dark car with pale tops
- **Task 1.4's list, checked against what QA sees:**
  - **Its misses all hold** (stand-ins 2–6, 13, 14 and the deliberate items).
  - **Three of its matches do not hold:**
    - "halos as flaked primer-and-rust blotches": they still read as round orange rings with drips (stand-in 1)
    - "a windscreen crazed pale across": pale in overcast, clear by day (stand-in 7)
    - "domed hubcaps": flat pale discs (stand-in 8)
  - **QA adds** stand-ins 9–12 and 15–21.

## 8. K07 and its shared chrome
```
K07 own files since 1.3b (9e445f4): no change (FBX, metadata, prefabs, params); shared since then: chrome.json, chrome.mat
K07 sheets at HEAD:                      bead_wire C43DAB0F / D913EE9A  bezel_ring 05F54098 / 817A9A2F  lamp_bowl 8F8E1AFC / D329A698  trim_plate D8D1EB89 / 0EBBEBF6
with 9e445f4's chrome.mat (capture only): bead_wire C43DAB0F / D913EE9A  bezel_ring 8E1EB3E3 / 31E94D06  lamp_bowl 386C09E5 / 1DA4DC67  trim_plate B3959260 / 93AA87F6
HEAD's chrome.mat restored and recaptured: the HEAD hashes again; git status clean
```
- **Only the sheets move.** The three chrome pieces' sheets change with the shared chrome, the bead wire's (`car_rust`, unchanged since 1.3b) does not, and no committed K07 file changes.
- **The pieces' sizes** (K07 metadata bounds):
  - bowl 0.170 × 0.085 × 0.170 m, one `steel` sphere, no snag (R4(c))
  - ring 0.180 × 0.012 m, 8 `steel` capsules, an `edge` 6 mm
  - plate 0.350 × 0.011 × 0.200 m (1 mm and its 10 mm bow), a `steel` box, an `edge` 0.5 mm
  - bead wire 0.365 × 0.006 × 0.375 m (mean outer Ø 0.37 m, out of round by its seeded 10 mm), 8 `steel` capsules, a `hook` 3 mm
- **The audit:** at most 14.1 mm, 0 problems.

## 9. Commands and hygiene
```
git fetch origin; git checkout unity/art/p1-r01-vaz-sedan (d4d5805); git lfs checkout      -> Checking out LFS objects: 100% (132/132)
(Get-CimInstance -Namespace root/wmi -ClassName BatteryStatus).PowerOnline                  -> False at the start; waited; True before every run; powercfg: Balanced
pwsh tools/build_asset.ps1 R01 (twice), K07 (twice), K01, S04, L01, L03, E01                -> build_asset: OK, exit=0 each; git status --short empty after each
& $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode ... -> testcasecount="66" result="Passed" passed="66" failed="0" (on AC)
python -m pytest tools -q                                                                    -> 908 passed in 830.04s
python -m pytest tools/hygiene -k test_existing_tree_is_clean -rA                            -> PASSED [HEAD]; PASSED [origin/unity/main merged with HEAD]
python tools/assets/fetch.py --verify                                                        -> OK, exit 0
python tools/hygiene/precommit.py --tree HEAD                                                -> 1118 files scanned, 0 findings, exit 0
openspec validate p1-r01-vaz-sedan --strict                                                  -> Change 'p1-r01-vaz-sedan' is valid
& $env:UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Review.Capture -asset K07 (old chrome, then HEAD's) -> exit 0 twice; git status clean
```
- **Commits:** 41, each subject-only and lower case, at most 72 characters, no body, no trailer, no co-author line.
  - Identities: Asset Artist `assets@450fpv.local` (22), Tech Artist `techart@450fpv.local` (9), Orchestrator `orchestrator@450fpv.local` (10: task text, the sub-branch merges and the trunk merge).
- **Committed paths:** nothing under `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/`, and no review sheet (git-ignored). All 56 PNG and FBX files on the branch go through LFS (`filter: lfs`).
- **Licences (D-003):**
  - The nine new sets are pinned in `manifest.json` with each file's sha256 and size, and credited in `CREDITS.md`: ambientCG `PaintedMetal006`, `Rust007`, `Metal046B`, `Metal049A`, `Rubber004`, `Fabric030`, `Leather014`, and Poly Haven `green_metal_rust`, `burned_ground_01`.
  - All are CC0 at 1k. `PaintedMetal006` is unused (S4). No paid or Asset Store content.
- **OPSEC:**
  - no file names from `reference/`, places, dates or OSD values in the branch (the scan) or in this review
  - M's photographed plate characters appear nowhere: the built plates are blank
  - the side-by-side was local and is deleted
