# Review: p1-e01-trench (task 2.1)

**Reviewer:** qa-engineer, fresh (did none of the work, D-034) · **Branch:** `unity/art/p1-e01-trench` · **Tip:** `1a7f3ca` · **Range:** `7d54230..1a7f3ca` (27 commits, 98 files)
**Requirements cited:** AST-02, AST-03, AST-05, AST-07, AST-08, AST-10, AST-12, REAL-01, REAL-03, REAL-05, SEC-04, PR-9 · **Decisions:** D-003, D-004, D-020, D-021, D-028, D-038
**Read:** `CLAUDE.md`, `AGENTS.md`, `docs/workflow.md`; the whole change folder (proposal, design, the as-built notes of tasks 1.1–1.5, the three spec deltas); `docs/object-sheets/earthworks-and-surfaces.md` (shared rules, F-1…F-8, the trench-family table, §E01); `docs/requirements.md` for every cited ID; D-038 on the trunk; the full diff of `generators/trench.py`, `lib/{holes,audit,meta,strip}.py`, `build.py`, `tools/assets/derive.py`, `CatalogImporter.cs`, `AssetPhysicsMeta.cs`, `ReviewCapture.cs`, the params, recipes, catalog and manifest, and every new or changed test.

Every check below was run by QA in its own worktree (`git lfs checkout` first, 76/76 objects; no `unity/Library/` before the first Unity run). Nothing from a handoff was reused. The hole contract, the gaps, the clipped audit, the mutations and the extra views came from QA's own scripts in its scratchpad; the extra Unity views from an uncommitted editor script, copied into `unity/Assets/_Project/Editor/QaScratch/` for its run and deleted after (`git status` clean). The side-by-side was composed in `reference/_compare/` and deleted after. Tools: Unity 6000.3.26f1 (Hub running, every call piped), Blender 5.2.2 LTS, Python 3.14.6 with Pillow 12.3.0, pwsh 7.6.7. Every Unity render ran on AC (`BatteryStatus.PowerOnline` True before each, Balanced plan) on the RTX 4050 (`Renderer: NVIDIA GeForce RTX 4050 Laptop GPU` in each log). **No performance claim is made.**

## Verdict: CHANGES REQUESTED

Two blocking items:
- **R1. The look (REAL-03, D-038, "The G look").** In sunlight the parapets read as fields of faceted rock, not grass, and the lip as a regular row of pale pegs. Every automated number matches G (dark share, spread, luminance); the eye does not. This is the chequerboard-walls failure mode.
- **R2. The top slot's gaps misstate the opening (AST-05, REAL-05).** 34 of the 49 slot polygons record `narrowest_m` 0.297–0.504 m, under the drone's 0.574 m, where the opening is 0.79–0.94 m lip to lip. The 0.30 m arc pieces of task 1.5 cut them shorter than the trench is wide.

Everything else passes:
- two builds, byte-identical, `git status` clean after each, and the review sheets byte-identical between them
- F-1…F-8, re-derived by QA's own code on three ray grids, and the five of six mutations the build catches
- the clipped audit against the unclipped one: 715 lines, at most 0.142 mm apart, no problem in either
- the 12 other variants' 464 audit lines identical with the trunk's audit
- the `holes` record, its validation and the component
- the earthwork framing, and K01 and S04 sheets byte-identical with the trunk's capture code
- EditMode 55/55, `pytest` 823 passed, both `test_existing_tree_is_clean` targets, `fetch.py --verify`, the OPSEC scan, `openspec validate --strict`, the commit hygiene

Task 2.1 stays unticked.

## Blocking items

### R1. In sunlight the parapets read as rubble and the lip as a row of pegs
**What QA saw** (review sheet tiles, and six extra views on the same stage and lights: pilot height mid-run on LOD0, inside the trench at 0.7 m above the floor, a lip close-up, a parapet close-up, and two views from a G-like vantage, higher and behind the run's start, looking along it):
- **The hummocks** (`_hummock`, 394 of them, 24 flat-shaded facets each) read as **faceted rocks or stumps** under the day sun: hard ridges, sunlit facets beside black ones, flat crowns. On the 10 m tile and from the G-like vantage, each parapet is a band of scattered rock clusters on a flat sandy mat. In overcast the same hummocks almost vanish: the flat ambient shades them like the ground under them, and only their stretched map shows.
- **The lip tongues** (`_tongue`, 251) read as a **regular row of pale pegs**: a crenellated comb on the far lip at 10 m (overcast most of all), and pale upright flags against the sky from inside the trench. Under them the soil lip is the 1–10 cm crumble walk, a clean line from any distance.
- **The berm**, level and relief-free by the contract, reads as a **bare pale path** along the lip between the comb and the first hummocks.
- **G 3, 6 and 9** (and G 1–12): one continuous golden mat of lodged, combed dry grass over the parapets, up to the lip and over it, soft and fibrous. Its shadows are crevices in a mat, not facets of solids. The lip is ragged and irregular, with dark soil notches. No path along the lip, and no rock anywhere.

**Why it fails:**
- **The spec:** the scenario "The G look" asks for "two broad pale **grassed** parapets" and lips "crumbled, never a clean edge".
- **D-038:** the recreated view must feel like the reference, "not only contain its parts". It holds the parts (a dark narrow slot winding between broad pale parapets 2–4 times its width, S-bends), but in sun it feels like a rubble field. G is flown in low sun, which will harden the facets further.
- **Task 1.5's own numbers:** the dark share (10.9 % and 12.6 % against G's 4.6–15.5 %) and the spread match G. They were the target, and they pass with the wrong shapes.

**Before (task 1.2):** a smooth sand-like mat. Neither version reads as G's grass. This one adds a feature G does not have.

**To close it** (asset-artist; QA judges again on the same tiles and the G-like vantage in both lights):
- **The parapets read as dry grass at 1.5 m and 10 m in sun.** No facet edge may read as a solid's. Some directions:
  - shade the hummocks smoothly, or with normals biased up, as grass masses are shaded; this needs the pipeline to carry vertex normals
  - lower, broader and merged swells, with the crevices carried by the cover's gaps
  - S04's kit at the density the budget allows, over a lodged mat
- **The lip reads ragged:**
  - irregular tongue spacing, with clusters and bare runs
  - colour broken towards the cover's dark gaps
  - the hanging edge not a straight tab
- **The berm and the lip line:** the grass of the mat runs up to the lip, inside the berm's 2 cm landable zone. The visual-only micro mesh allows that.

If the orchestrator rules that the continuous mat must wait for AST-10's micro-detail generator (phase 3), the stand-in for G1 still must not read as rock. That is the minimum this item asks.

### R2. 34 of 49 top-slot gaps record a `narrowest_m` under the drone's span
**Measured** (the build's audit lines; QA's `qa_holes.py` agrees):
```
slots on straights:     narrowest_m 0.7917-0.9399   (the lip-to-lip width)
slots on the four arcs:  narrowest_m 0.2968-0.3386   (32 polygons, 0.30-0.34 m long)
slot_3.66, slot_15.97:   narrowest_m 0.5044, 0.5039 (the 0.5 m pieces beside the niches)
slots under the drone's 0.574 m: 34 of 49
cross-section clear width: lip 0.857-0.945 m, so the opening above every slot is wider than the drone
```
**Why it fails:**
- **AST-05:** gaps are "measured from the real clear space, with the narrowest width recorded".
- **REAL-05:** every visible opening is a gap "measured as the real clear space".
- **The pipeline spec:** the polygon is "the throat of the opening".
- **The cause:** a 0.30 m slice of a 0.9 m wide slot is no throat. Its inscribed circle is its length. Its acceptance, a probe flying a 0.574 m nose-first capsule "through every gap", cannot pass 32 of these polygons as written.
- **Since when:** the slices come from the per-segment slot ("plus the top slot per segment", this change's delta and the sheet's "one polygon per filler segment"). Task 1.5's 3.7 m bend radius cut the arc segments to 0.30 m to keep `JOINT_DEG` 5°.

**To close it:**
- **The orchestrator** amends the spec's and the sheet's wording: a slot polygon per stretch at least as long as the cut is wide (about 1 m, which the sheet's "≤ 1 m on curves" already allows), independent of the filler segments. Every slot lies at y = 0 on the asset's level patch, so planarity is no reason to cut it finer.
- **The asset-artist** merges consecutive arc pieces into slot polygons of about 1 m (concave allowed), and prints the slots' `narrowest_m` range in the build.
- **A test** that no slot's `narrowest_m` falls under its own lip-to-lip width less 5 mm.

## Suggestions (not blocking)
- **S1. LOD0 is all but unseen, and never reviewed.**
  - **The cause:** LODs switch by distance to the LOD group's reference point, the 35 m patch's centre (`LOD heights 1.0000 / 0.1333 / 0.0200`, size 9.24 m). LOD0 shows only within 8 m of mid-run.
  - **The effect:** no review tile shows the floor's 483 straws (QA's mid-run views on LOD0 do: pale thin lines on the floor, acceptable). A pilot flying the run sees them pop in once within 8 m of the patch's centre, about a fifth of the way along it.
  - **The fix:** LOD0 and LOD1 share one mesh and differ by those straws. Drawing them on LOD1 too keeps LOD1 at 58,897 triangles, under the 60,000, and removes the pop.
  - **Phase 3:** per-part LOD for long linear assets is the world's, when runs are placed or chunked.
  - **So:** a defect of review coverage for this asset, and a phase-3 matter for the world.
- **S2. Record the as-built corridor verdicts.** The handoff never listed the sheet's verdicts, which "The corridor against the drone" asks for. QA measured them on the 48 cross-sections.
  - nose-first: 13 mm a side at the floor (0.600 m), 43–54 mm at 0.35 m (0.660–0.681 m), 141–186 mm at the lip (0.857–0.945 m)
  - yawed 45° (0.706 m): fits from the lip down to 0.88–1.04 m below it, in every section
  - The sheet's default row says 113 mm and 0.7 m: deviation 1's crumble leans the whole wall back (batter 82.4–85.8°, inside the sheet's 80–90°), not only its top. PR-9 still holds. The orchestrator either accepts these numbers or asks for a crumble confined to the wall's top.
- **S3. F-5's 0.2 m overlap is enforced only by `pytest`.** The generator's joint check compares against its own `OVERLAP_M` (mutation M3 builds clean with 0.1 m overlaps), and `lib/holes` checks thickness only. An absolute 0.2 m threshold belongs in `lib/holes` or `build.py`.
- **S4. The berm's contact surface.** Under the berm the collision is the lip box's top, `surface:belt_bare`, not landable. The visual is grassed spoil (slot physics `belt_straw`) with tussocks on it. For the physics engineer: accept (a trampled lip) or cap the berm with `belt_straw`.
- **S5. Niches carry no gap polygon.** They are recesses 0.8 × 0.6 × 0.7 m, not throats. For the physics engineer: confirm that a dead-end recess needs none.
- **S6. Floor clods** read as smooth pale loaves on the 1.5 m tile (the spill band's #8a7660 on 10-vertex rings). They will matter less in G's shade; recheck under the G1 light.
- **S7. Stale artefacts** for the orchestrator at archive:
  - the proposal's 30 × 16 m patch and "built from S04's `ground_patch` pieces"
  - design 1's 0.1 m lip cells and design 2's jagged inset
  - design 7's 2048 × 256 strip
  - design 8's "LOD1 without micro"
  - design 4's 0.3 m lip boxes
  The as-built notes say each, but the design still reads otherwise.
- **S8. The trench-family table.** The sheet's `width_jitter_m` (spade-dug width, 0–0.15 m) is not built. The generator's parameter names differ from the sheet's table (`top_w_m`/`top_width_m`, `niche_every_m`/`niches_per_10m`, `bend_*`/`sinuosity`). Reconcile before E02–E04.
- **S9. AST-08's per-class budget fails no build.** E01's 60,000 is a `pytest` assertion, and no earthworks row exists in a build-time table. This was there before this change.
- **S10. Hole ids are asset-local** (`run`, `niche_0`, `niche_1`). The world-builder's compiler must namespace them per placement.
- **S11.** A `reference/_compare/qa_e01/` folder from an earlier session is still on disk (local only, ignored). Whoever made it should delete it.

## 1. Spec scenarios

| Scenario | Result | Evidence |
|---|---|---|
| E01: built from params by one command | **Passed** | Two runs of `pwsh tools/build_asset.ps1 E01`: exit 0 (21:14 and 20:42). Each writes the FBX, metadata, prefab and both sheets. `git status --short` is empty after each. The 776 generator, audit, hole and joint lines are identical, and the sheets byte-identical (day `1D9BE6BA…`, overcast `4A6C7707…`) |
| E01: the corridor against the drone | **Partly met** | Every cross-section runs floor (y −1.5) to lip (y 0) at the batter (test). Each `narrowest_m` equals the audit's inscribed circle to 4 decimals, 0.7864–0.8419 (48 lines). With no crumble, 0.75 ± 5 mm (`test_default_section_inscribes_0_75`). The handoff did not list the verdicts; QA's are in S2 |
| E01: fillers under the hole contract | **Passed** | §2 |
| E01: the G look | **Failed** | R1 |
| E01: parapets and clods are ground | **Passed** | 324 `belt_straw` landable (320 boxes, 4 cylinders), 24 `crater_spoil` spheres, 269 `belt_bare` not landable; `wind_volumes` and `snag` empty. The holes' centre line is the cut's own polyline (50 points, ≤ 1 m apart), `top_width_m` 0.8, `depth_m` 1.5 |
| Pipeline: a trench writes its hole | **Passed** | `run` (50 points, band 2.3 m), `niche_0` and `niche_1` (band 2.1 m, `depth_m` 1.35). F-2 holds at 0.65 m with the crumbled lip and the niches. Importer: `3 holes (run 50 points, band 2.3 m, niche_0 2 points, band 2.1 m, niche_1 2 points, band 2.1 m)`. `Meta_ExposesTheRunsHoleThenTheNiches` passed |
| Pipeline: hole validated | **Passed** | `test_holes_validated`, 6 cases: one point, band under the top width, 2-vectors, a missing key, zero depth, a duplicate id |
| Review sheet: pilot-height tile of a trench | **Passed** | The tile shows the cut running away between its parapets, with the bar in frame. The log line is `[Review] earthwork pilot pose: camera (0.133, 3.350, -0.377), 1.500 m above the lip, 0.400 m to the field side (right) ..., forward (0.8865, -0.3420, 0.3117), 20.00° down ... bar ... in frame True, top in frame True` |
| Review sheet: other classes unchanged | **Passed** | K01 and S04 captured with the branch's `ReviewCapture.cs`, then with the trunk's (`7d54230`, with its test, restored after). All four sheets are byte-identical (K01 day `FC2869B6…`, overcast `7666B5E4…`; S04 day `08BB321E…`, overcast `1C19E00C…`) |

**Requirement IDs:**
- **Met:** AST-02, AST-03 (one command, twice; QA did not repeat the parameter-change acceptance in Unity), AST-12, SEC-04, D-003.
- **Partly met:**
  - AST-05: R2's slot gaps.
  - AST-07: UVs continuous, the ground map laid once (no tiling at 10 m); 76 px/m along the wall strip, carried near by the 2.07 m detail layer.
  - AST-08: LOD0 58,897 under 60,000 by test, not by the build (S9); textures 2K at most.
  - AST-10: the grass is a baked visual-only stand-in until the shared generator.
  - REAL-01, REAL-03: R1, and no feed yet, so G is compared on the review stage.
  - REAL-05: the geometry audit in §4, and R2.
  - PR-9: met as built (S2).

## 2. The hole contract F-1…F-8, run by QA

### The build's own lines (QA's first build; the second is identical)
```
E01_curving: holes F-1 sealed below: 41813 vertical rays on a 5 cm grid over the bands and 0.71 m past their edges, 0 miss every filler
E01_curving: holes F-2 the cavity inside the bands: 138022 samples of the cut's faces (slots wall), the least 0.6500 m inside the bands at (3.1912, -1.3707, 1.6522), at least 0.65 m
E01_curving: holes F-3 level lips: 1286 samples along the bands' edges, the fillers' top within 0.0 mm of the ground, tolerance 30 mm
E01_curving: holes F-4 nothing proud: the highest filler point at y = 0.0008 m, at most 0.01 m
E01_curving: holes F-5 thick: 269 fillers, every box at least 0.300 m across, at least 0.3 m
E01_curving: holes F-7 surfaces: 269 fillers surface:belt_bare, 0 landable; 324 surface:belt_straw, 324 landable; 24 surface:crater_spoil, 0 landable
E01_curving: holes F-8 no wind obstacle: 0 wind volumes
48 F-5 joint lines, overlap 0.200-0.200 m                      (QA's count over the log)
F-6: 715 audit lines; worst 38.758 mm (mesh->collision at (27.75, 0.0388, -4.25), a margin's relief over its column, tolerance 50 mm);
     worst in the 2 cm zone 16.067 mm (collision[434] near (16.1876, 0.0001, 0.1861), tolerance 20 mm)
```

### QA's own check (`qa_holes.py`, written apart from `lib/holes.py` and `lib/audit.py`)
It reads the shipped `E01_curving.assetmeta.json`. A vertical line meets a box when its plan point lies in the convex hull of the box's eight corners in plan; a filler's top comes from bisection on point-in-box; the band distance is its own. LOD0 is rebuilt in plain Python only for F-2's faces, and its collision, holes and gaps are first checked identical to the shipped file.
```
shapes 617, filler boxes 269, filler non-boxes 0, holes [('run', 50, 0.8, 1.5, 2.3), ('niche_0', 2, 0.8, 1.35, 2.1), ('niche_1', 2, 0.8, 1.35, 2.1)]
F-1 5 cm: 41813 rays within 0.71 m past the bands, 0 miss every filler box
F-1 5 cm +2.5 cm: 41833 rays within 0.71 m past the bands, 0 miss every filler box
F-1 2 cm: 261366 rays within 0.71 m past the bands, 0 miss every filler box
F-3 band edges: 859 samples, filler top worst 0.00 mm off y = 0
F-4: highest filler corner y = 0.00077 m
F-5: thinnest filler box 0.3000 m
F-7: belt_bare not landable 269 boxes; belt_straw landable 320 boxes + 4 cylinders; crater_spoil not landable 24 spheres
F-8: wind volumes 0 snag 0
rebuilt in plain Python (12 s): collision, holes and gaps identical to the shipped file: True
F-2: 15180 samples of the wall slot's faces, the least 0.65000 m inside the bands at (3.1867, -1.2697, 1.6651)
F-2 on the lip vertices (y = 0): least 0.65009 m
F-2 below 0.1 m (niches, walls): least 0.65000 m
F-6 spot check on 2640 random wall points: farthest outside every box 5.8 mm; deepest inside a box 18.2 mm at (17.104, -0.837, 1.717)
```
- **F-1** holds on the contract's 5 cm grid, on a grid offset by half a step and on a 2 cm grid. No sliver opens between boxes at the mitred joints or the backing's corners.
- **F-2** reaches exactly 0.65 m at both edge cases the brief names: the widest crumbled lip (0.65009 m) and a niche (0.65000 m, `niche_1`'s side at y −1.27).
- **F-6, cross-checked against the new clipped audit.**
  - QA's deepest point: a wall point 17.6–18.2 mm inside `collision[202]`, on `niche_1`'s far side face, inside the next segment's lip box. That box reaches 0.117 m back past the joint at that depth, because its frame follows the lip line.
  - The audit finds the same point: `collision[202] box: box->mesh 17.798 mm at (17.0823, -0.8383, 1.711), tolerance 50 mm`.
  - It lies 0.687 m from the nearest gap, outside the 2 cm zone.

### The clipped audit against the unclipped one
`audit_e01_noclip.py` runs E01's audit with `clip_boxes=False`, the refine-to-precision path of every other asset, in 1427 s.
```
clipped 715 lines, unclipped 715 lines, problems in unclipped: 0
identical 514, differing 201; largest difference of a measurement 0.142 mm
  collision[268] ... near gaps/landable 12.038 mm (clipped) || 12.180 mm (unclipped), tolerance 20 mm
```
The exact box clipping gives the same verdict line for line. The largest gap, 0.142 mm, is of the order of the search's 0.1 mm precision plus the zone-edge guard's 1 mm cells.

### Mutations (`qa_mutations.py`: the generator changed, the build's checks run)
```
M0 unmutated: NOT CAUGHT at build time                      (expected: no problem)
M1 band margin 0.60 (F-2): CAUGHT by lib.holes: F-2 ... the least 0.6000 m inside the bands
M2 backing 0.65 past the band (F-1): CAUGHT by lib.holes: F-1 ... 590 miss every filler, the first at (-1.810, -0.410)
M3 overlap 0.05 a side (F-5 overlap): NOT CAUGHT at build time
M4 fillers 0.25 thick (F-5 thickness): CAUGHT by lib.holes: F-5 thick: ... every box at least 0.250 m across
M5 lip boxes 4 cm proud (F-3, F-4): CAUGHT by lib.holes: F-3 ... within 40.0 mm ... | F-4 ... y = 0.0400 m
M6 the first floor box removed (F-1): CAUGHT by lib.holes: F-1 ... 153 miss every filler, the first at (0.040, 0.240)
M3 under pytest: CAUGHT by test_neighbours_overlap_at_every_joint: joint 1 at 0.89 m, probe (...): 0 boxes straddle it
```
Five of six are caught at build time. The sixth, F-5's overlap, is caught only by `pytest` (S3).

## 3. The gaps against the sheet's width table and the drone verdicts
- **48 cross-sections** (`xs_0.89` … `xs_23.21`), one per sample:
  - every ≤ 1 m on the straights, every 0.28–0.32 m on the arcs, one at each arc's middle (the bend vertex), one at each niche's segment ends
  - `narrowest_m` 0.7864–0.8419, the crumble widening the default section's 0.75
- **49 slots:** R2.
- **The verdicts** against the sheet's default row (0.60 / 0.65 / 0.80 m, nose-first 13 / 113 mm, yawed in the top 0.7 m) are in S2. Nose-first everywhere holds, with the floor's 13 mm a side exactly the sheet's. A bend is turnable yawed in the top 0.88–1.04 m, so "only near the lip" holds more loosely than the sheet's 0.7 m.
- **The visual-only grass and the corridor:** no grass vertex stands within 0.287 m of the centre line below the lip (test). The lip tongues reach 1–4 cm past the lip, inside the 141–186 mm a side.

## 4. The look (REAL-03, D-038) and the geometry audit (REAL-05)
- **Read by eye:**
  - the sheets' 1.5 m, 10 m, 50 m and 200 m tiles in both lights, the turntable and the overlay
  - the artist's `reference/_compare/E01.png` (G 1–12, G 3/6/9 enlarged, tasks 1.2 and 1.5 tiles)
  - G 1–12 from its contact sheet, G 3, 6 and 9 as single stills, B 1–6 from its contact sheet, B 1–2 as single stills
  - QA's own local side-by-side of G 3/6/9 and B 1–2 over its renders
- **What matches G:**
  - a dark narrow slot winding away between two broad pale parapets, each 2–4 times the cut's width
  - alternate S-bends
  - at 50 m and 200 m a thin dark winding line in a pale block
  - the inside dark on its shaded side (the sheet's 45° sun lights the far wall mid-brown; G's low sun puts it in shade)
- **What does not match G:** R1, and the G1 stand-ins below.
- **B 1–6:** B shows only a dark cut at the foot of a bank with fresh pale lumpy spoil, E07's look and E01's unbuilt `age` 0. Nothing in E01 contradicts B, and nothing in it copies B.
- **The geometry audit (REAL-05):**
  - **Lips crumbled as geometry: partly.** The crumble is geometry, a per-side walk of 1–10 cm that leans each wall's top back, and the collision follows it within 1 cm. It reads as a clean line. Only the visual-only tongues break it, and they read as pegs (R1).
  - **Lip grass and tussocks as visual-only micro meshes with no collider: yes.**
    - metadata `micro`: `E01_curving_LOD0_straw` (483), `E01_curving_LOD0_grass` and `E01_curving_LOD1_grass` (645 each)
    - the audit skips them
    - `Grass_DrawsOnLod0AndLod1_WithoutColliders` passed
    - 617 colliders for 617 primitives
  - **Clods as geometry: yes.** 24 low sphere caps, each with its `crater_spoil` sphere.
  - **Fillers follow the faces: yes** (§2's F-6). The overlay shows the slab and crest boxes on the parapets, the trapezoids of the gaps and the margin columns.
  - **Nothing physical texture-only.** The spade facets are a normal map under 2 cm (`relief_m` capped at 0.02 by `derive.py`), as the sheet asks. The root fringe and topsoil band are colour (roots deferred, X-10).

## 5. Budgets (AST-08)
```
E01_curving: ... size [35.25, 2.8171, 13.75] m  tris 39273 / 39273 / 1840  micro E01_curving_LOD0_straw 483 straw 5796 tris
             micro E01_curving_LOD0_grass 645 grass 13828 tris  micro E01_curving_LOD1_grass 645 grass 13828 tris  617 collision shapes  fbx 4.0 MB
```
- **LOD0:** 39,273 + 5,796 + 13,828 = **58,897**, against the design's 60,000. **LOD1** 53,101, **LOD2** 1,840, cull 400 m.
- **The floor straws** went from 4 segments to 2 to make room (11,784 → 5,796).
- **Textures:** `trench_wall` 2048 × 1024, `grassed_spoil` 2048², `dirt_floor` at 1k. Everything 2K at most.
- **Margin:** 1,103 triangles. R1's fix must fit inside them, or trade against the floor straws, which LOD0 almost never shows (S1).

## 6. Code outside the proposal's file list
- **`lib/audit.py`:**
  - **Bounding-box pruning** (`_boxes_meet`, `_ShapeIndex`, `_zone_distance` with box lower bounds): exact by construction.
  - **The 12 other variants:** QA ran their audit with the trunk's `audit.py` (from `7d54230`) and with the branch's, on identical inputs (`audit_lines.py`). K01, L01 ×4, L03 ×3, S04 and V13 ×3 give **464 lines, 0 differences, 0 problems** either way.
  - **The exact box clip** (`clip_boxes`, only for assets with `holes`): sound. It is a Sutherland–Hodgman split by each box's six grown half-spaces, slivers under `COVERED_M` dropped, and the bound taken over the uncovered parts. QA confirmed it against the unclipped audit (§2).
  - **The zone-edge guard** (`ZONE_EDGE_CELL_M` 1 mm): a cell straddling the zone's edge stops at its bound, so the guard can only over-report. Its effect on the 12 variants is nil (above).
- **`tools/assets/derive.py`:**
  - **The patch-free cover:** a recipe whose `patches` name only the lodging gets `COVER_FORMULA`, a null layout, no relief tone, and `cover.smoothness_scale`, refused on a non-cover. Sound.
  - **`belt_straw`** keeps `GROUND_FORMULA`. Its mask is `round((255 − v) × 1.0)`, the same integers.
  - **Re-derived from scratch** (QA deleted the `belt_straw`, `grassed_spoil` and `trench_wall` stamps and ran `derive.py --all`): all three byte-identical, `git status` clean.
    ```
    belt_straw_albedo.png: derived 2048x2048 ground ... mean luminance 0.1638 (target 0.1638, +0.0 %), bare 8.1 % ...
    grassed_spoil_albedo.png: derived 2048x2048 ground ... mean luminance 0.3452 (target 0.3452, -0.0 %), a cover: no bare patch, green strip or relief tone, gaps 36.6 % ...
    trench_wall_albedo.png: derived 2048x1024 wall strip ... walls #4c463d (luminance 0.0622 ...), floor #4a463f
    derive.py: OK
    ```

## 7. The design deviations task 1.1 lists
| Deviation | QA's view |
|---|---|
| The crumble as a smooth walk leaning the wall's top back, not a 0.1 m jagged inset | **Sound for the physics:** boxes cannot follow a 0.1 m jag within the lips' 2 cm, and the walk's twist stays within 1 cm. **Two consequences:** the corridor's verdicts move (S2), and the soil lip reads clean (R1). Design 1 and 2 need updating (S7). No spec change, as long as R1 makes the lip read crumbled |
| Lip boxes at full depth as the backing | **Sound.** F-1 holds on every grid: they reach 1.90 m (field) and 2.88 m (belt) from the centre line, against the 1.86 m and 2.84 m (a niche band's cap) that the bands plus 0.71 m need. Each is ≥ 0.3 m in every dimension. Design 4's text needs updating only |
| Margin columns | **Sound.** `belt_straw`, landable, overlapping by 5 cm, stopping 1 m from the centre line inside the lip boxes' reach. F-6's worst line (38.8 mm, tolerance 50) is a margin's ±4 cm relief over its column top |
| The 12.5 cm and 6.25 cm grid | **Sound** once the crumble is a walk. The lip is cut exactly by the slot polygon, and the parapets' creases are followed within 2 cm (F-6 in the zone 16.1 mm) |
| `lib/ground_patch` not reused | **Sound** (the ground patch's grid steps for S04's bare patches). The proposal's Impact line is stale (S7) |
| Hole ids `run`, `niche_0`, `niche_1` | **Sound:** design 6 itself writes `"id": "run"`, and the validator holds them unique per asset. Namespacing per placement is the compiler's (S10) |

## 8. LOD distance from the patch centre
- **Task 1.5 is right:** Unity measures from the LOD group's reference point. QA's mid-run views report `3.73 m to the LOD reference point, relative height 2.146, LOD0`; from the G-like vantage, `17.04 m ..., LOD1`.
- **For this asset:** LOD0 and LOD1 are the same mesh and differ only by the floor's straws, so it is not a look defect. It is a review-coverage gap with an easy fix (S1).
- **For the world:** the distance semantics of long patches is phase 3's (placement, chunking, AST-10's micro-detail).

## 9. Commands and hygiene
```
git fetch origin; git checkout -B qa-e01 origin/unity/art/p1-e01-trench; git lfs checkout   -> Checking out LFS objects: 100% (76/76)
(Get-CimInstance -Namespace root/wmi -ClassName BatteryStatus).PowerOnline                  -> True (before every Unity run); powercfg: Balanced
pwsh tools/build_asset.ps1 E01   (twice)                                                     -> build_asset: OK (E01), exit=0 (21:14, 20:42); git status --short empty after each
& $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode ...  -> total=55 passed=55 failed=0 skipped=0 result=Passed
python -m pytest tools -q                                                                    -> 823 passed in 1535.27s
python -m pytest tools/hygiene -k test_existing_tree_is_clean -rA                            -> PASSED [HEAD]; PASSED [origin/unity/main merged with HEAD]
python tools/assets/fetch.py --verify                                                        -> OK, exit 0
python tools/hygiene/precommit.py --tree HEAD                                                -> 826 files scanned, 0 findings, exit 0
openspec validate p1-e01-trench --strict                                                     -> Change 'p1-e01-trench' is valid
git merge-tree --write-tree origin/unity/main HEAD                                           -> a tree, no conflict (the trunk moved only in docs/ and openspec/ since 7d54230)
```
- **Commits:** 27, each subject-only and lower case, at most 72 characters, no body, no trailer.
  - Identities: Asset Artist `assets@450fpv.local`; Tech Artist `techart@450fpv.local` (task 1.3); Orchestrator `orchestrator@450fpv.local` (the task-text commits and the merge of task 1.3's sub-branch).
- **Committed paths:** nothing under `reference/`, `Library/`, `Temp/` or `Logs/`. The new PNGs (derived maps, the CC0 set) and the FBX go through LFS (`filter: lfs`).
- **Licences (D-003):**
  - Poly Haven `dirt_floor` 1k is CC0, pinned by sha256 and byte size in `manifest.json`, and credited in `CREDITS.md`.
  - The author `eye-candy.xyz` and the 2.07 m tile match Poly Haven's API (`authors`, `dimensions` 2070 mm).
  - `grassed_spoil` reuses the pinned `brown_mud_02` and `ground_0018`.
- **OPSEC:** no file names, places, dates or OSD values in the branch (the scan) or in this review.

## 10. The G1 stand-ins: what E01 does not yet copy from G
1. **The continuous lodged grass mat** over the parapets, the berm and up to and over the lip: one combed, fibrous golden mat. E01 has a flat cover with discrete hummocks, which R1 must at least make read as grass. The mat at G's density is AST-10's (phase 3).
2. **The lip:** grass lodged over it irregularly, with **dark notches cut into the lip soil** and crumbs. E01 has regular tongues over a smooth 1–10 cm walk.
3. **Colour and light:** G's golden-orange straw and warm brown inside, the inside in deep shade under a low sun. The sheet's neutral 45° sun gives beige parapets and a lit far wall. This is the look setup's (G1) and the feed's (phase 4).
4. **The trench's own irregularity:**
   - G's width varies along the run, and its bends are tighter and less regular
   - E01 has a constant width (no `width_jitter_m`, S8) and regular 3.7 m arcs, the least `check` allows with 2.5 m parapet bases
5. **Weeds and shrubs** on and beside the parapets (G 4, 8, 11, 12: green patches among the straw). Not in E01.
6. **The setting:** the belt's trees and shrubs on one side and the field beyond the other parapet. These are the world's (phase 3); E01's margins are plain cover.
7. **The floor:** unseen in G's shade. E01's pale clods and floor straws need a recheck under the G1 light (S6).

---

# Re-check of task 1.6

**Reviewer:** qa-engineer, fresh (did none of the work, D-034) · **Tip:** `0269a1f` · **Range:** `5c8baff..0269a1f` (8 commits, 20 files)
**Read:** this review's R1–R2 and S1–S11; task 1.6 and its as-built note; design decisions 1–11 (8 amended, 11 new); the proposal and spec delta as amended; the full diff of `generators/trench.py`, `lib/holes.py`, `lib/strip.py`, `build.py`, `tools/assets/derive.py`, the params, the `grassed_spoil` recipe, `test_trench.py`, `test_ground.py` and `TrenchPrefabTests.cs`; the sheet's trench-family table; the `belt_bare` and `belt_straw` rows of the legacy surface table.

QA ran every check below in its own worktree (`git lfs checkout`: 76/76 objects; no `unity/Library/` before the first Unity run). Nothing from the handoff was reused.
- **QA's own scripts** (scratchpad, never committed): `qa_meta.py` reads the shipped metadata only (slots, mouths, corridor, berm boxes, a top-surface map); `qa_build.py` rebuilds E01 in plain Python (F-2 by its own band distance, the mat's corridor, the relief tone, the S3 mutation through `build.build_variant`).
- **Extra Unity views:** an uncommitted editor script on the review stage and lights, copied into `unity/Assets/_Project/Editor/QaScratch/` for its run and deleted after (`git status` clean).
- **The side-by-side:** composed in `reference/_compare/` from G's contact sheets and deleted after.
- **Power and GPU:** every Unity run on AC (`BatteryStatus.PowerOnline` True before each, Balanced plan), on the RTX 4050 (`GPU NVIDIA GeForce RTX 4050 Laptop GPU (Direct3D12)` in each log). **No performance claim is made.**

## Verdict: CHANGES REQUESTED

R1 and R2 are closed. One new blocking item, from the overlap task 1.6 reports:
- **R3. Past each run end, a contact sees `belt_bare` under the lodged mat (AST-05, REAL-05).** 3.73 m² of level grassed ground there has a `belt_bare` box as its only top, 3.22 m² of it inside the run's band. Another 7.86 m² has a `belt_bare` top co-planar with a `belt_straw` one. S4 fixed the same mismatch on the berm; it stops where the berm turns round the run's ends.

Task 2.1 stays unticked.

## The items

| Item | Result | Evidence |
|---|---|---|
| R1 the look | **Closed** at the minimum R1 asked | §R1 below: no rock, no pegs, no path; the lips broken by grass in clusters, at 1.5 m, 10 m and two G-like vantages, day and overcast |
| R2 the slots | **Closed** | 21 slots, `narrowest_m` 0.903–0.981, each at least its own least lip-to-lip width; mouths 0.6038 and 0.6031; tests pass |
| S1 bake on LOD0, LOD1 at 24 m | **Sound alternative**, with a caveat (S12) | the review's views and a pilot over the run see LOD0; the switch at 24 m shows (S12) |
| S2 corridor verdicts | **Done**, QA's numbers agree | design 5's figures to the millimetre (below) |
| S3 joint overlap fails the build | **Done**, mutation caught | `build.build_variant` refuses `OVERLAP_M` 0.05 on "F-5 overlap"; caveat in S13 |
| S4 berm boxes `belt_straw` | **Done on the berm**; the run's ends are R3 | 98 berm boxes; no hole in the berm's top |
| S5 niche mouths | **Done** | `mouth_3.16`, `mouth_16.97`, planar, in the wall's plane |
| S6 clods | **Done** | dark lumps of the wall's soil on the floor, both lights |
| S7 design text | **Done**, with a residue (S15) | decisions 1, 2, 4, 6, 7, 8 state the as-built; the proposal is the orchestrator's (`0269a1f`) |
| S8 `width_jitter_m` and names | **Done** | 0.05, only widens (QA: floor ≥ 0.6, lip ≥ 0.8 at every section); every row of the sheet's table is named |
| S9, S10 | Open, outside task 1.6 | unchanged |
| S11 | **Resolved** | `reference/_compare/qa_e01/` is gone |

### R1: what QA saw
**Views** (all 1024 px, review stage, day and overcast):
- the sheet's 1.5 m, 10 m, 50 m and 200 m tiles, the turntable and the overlay
- six of QA's own: pilot height mid-run on LOD0, inside the trench, the far lip and the near lip from 1–2 m, a parapet close-up, and two G-like vantages, higher and behind the run's start, looking along it (16.0 m and 20.8 m from the LOD reference point, LOD0)
- G 1–12 from its contact sheets; G 3, 6 and 9 beside the 1.5 m tile, the 10 m tile and the nearer G-like vantage in the local side-by-side

**The parapets and the berm** read as a lodged dry-grass mat at every distance up to LOD1, in both lights:
- straws combed down the slopes; no facet, no solid; no rock, no stump
- the berm is covered to the lip; there is no pale path
- task 1.5's hummocks and tongues are gone (`_hummock` and `_tongue` deleted; the micro list holds `straw` and `mat` only)

**The lips** are broken by grass:
- clusters of straws jut over the edge and hang down the wall, with bare runs between them where the dark wall shows; no regular comb
- from 1–2 m the clusters read as a fringe hanging from the lip, and between them the soil edge is a clean, straight line (G1 stand-in 2)
- at 10 m in overcast the hanging straws read as fine pale ticks, irregular and clustered, never pegs

**The floor (S6):** the clods are dark rounded lumps of the wall's soil, read as clods in shade. The floor's straws are pale thin lines.

**The numbers:** the handoff's dark shares on the 1.5 m day tile (0.0 % and 5.0 %) are now under G's 4.6–15.5 %; QA did not repeat them. QA judged by eye, as R1 asked. The softer crevices are the 45° review sun's and go to G1.

### R2 and S5: the slots and the mouths (QA's `qa_meta.py`, `qa_slots.py`)
```
48 cross-sections, 21 slots, 2 mouths
slot starts 0.00 ... 22.86, chainage steps 1.14-1.15 m (24 m / 21 = 1.143 m)
narrowest_m 0.903018-0.980621; QA's brute-force inscribed circle (6 mm grid) within 3.2 mm of every slot's
least (narrowest_m - its least lip-to-lip width): +0.5 mm (slot_3.43); slots under 0.574 m: 0
consecutive slots sharing their end edge: 20 of 20; their area 22.5717 m2 = the opening QA's 5 cm grid finds (22.57 m2)
mouth_3.16  sides 0.8 x 0.6038, planarity 5.0e-07, narrowest 0.6038 (QA 0.6038)
mouth_16.97 sides 0.8 x 0.6031, planarity 2.1e-07, narrowest 0.6031 (QA 0.6031)
```
The slots tile the opening exactly; each one's circle is the opening's width, not a piece's length. `test_slots_are_the_openings_throat` and `test_niche_mouths_are_gaps` pass in QA's `pytest` run.

### S1: the bake on LOD0, LOD1 at 24 m
- **What it fixes:** the review's 1.5 m and 10 m views (10–12 m from the LOD reference point) and a pilot anywhere over the run see LOD0. The floor's straws no longer pop at 8 m. The FBX holds the bake once (18.8 MB).
- **What it costs:** LOD0, 39 172 + 411 276 triangles, draws within 24 m of the patch's centre, which is the whole run and some metres past its ends. That is S04's precedent (design 8, amended).
- **The switch** (QA's views from beyond the run's start, 4 m up, at 22.0, 23.5, 24.5 and 26.0 m from the reference point): LOD0 at 23.5 m, LOD1 at 24.5 m, as logged.
  - Straws more than about 15 m from the camera alias into a pale grainy band over the parapets. The URP asset's MSAA is 1, per the capture log.
  - Just inside 24 m most of the parapets are that far, and at 24 m the band gives way to LOD1's smooth shaded humps, in both lights.
  - It reads as dry stubble, not rock, but the change shows (S12). From the two G-like vantages (16.0 m and 20.8 m) the parapets still read as straw.
- **So:** a sound alternative for this asset; the pop moved from 8 m to 24 m and changed kind.

### S2: the corridor, from the 48 cross-sections in their planes
```
    floor: 0.6118-0.6435 m, nose-first 19-35 mm a side
at 0.35 m: 0.6775-0.7168 m, nose-first 52-71 mm a side
      lip: 0.8905-0.9750 m, nose-first 158-200 mm a side
yawed 45 (0.706 m) fits from the lip down to 0.998-1.200 m
batter 82.40-85.80 deg; floor >= 0.6: True; lip >= 0.8: True
```
Design 5's numbers hold. Its 19–34 mm rounds 34.75 mm down. PR-9 holds.

### S3: the joint check, run as a mutation through `build.py`
`qa_build.py` imports `build.py` with a stub `bpy` and calls `build.build_variant` for E01 as the build does, with the collision audit stubbed (17 minutes, and F-6 has nothing to do with the overlap):
```
OVERLAP_M 0.1 (as built): passes every check and reaches the export (the stub bpy stops it there)
OVERLAP_M 0.05: CAUGHT by build_variant: ...F-5 overlap: 48 joints, the boxes along the run overlap 0.100-0.100 m,
                at least 0.2 m; 48 short, the first joint 1 at 0.89 m overlapping 0.100 m
```
The build itself prints `holes F-5 overlap: 48 joints, ... 0.200-0.200 m, at least 0.2 m` and one line per joint. Caveat in S13.

### S4 and the top of the collision (QA's top-surface map)
On a 5 cm grid over the patch, QA takes the highest shape top of every vertical line outside the opening, and the shapes whose tops lie within 0.5 mm of it:
```
98 level belt_straw boxes 0.3 thick, top at y = 0, within 0.6 m of the centre line, all landable
outside the opening: no top below -5 cm anywhere (no hole in the berm's top)
along the sides: belt_bare is the top only on the lip's edge (0.010 m2: a wall box's corner, 0.8 mm proud, F-4),
                 and ties within 0.5 mm only along the lip's edge and the parapets' inner toe (0.375 m2 of slivers)
past the run ends: belt_bare the only top   3.730 m2 (1.725 / 2.015 per end), 3.223 m2 inside the run's band
                   belt_bare tied with belt_straw within 0.5 mm   7.855 m2, 0.165 m2 inside the band
```
The berm is grassed ground to the lip along the whole run. R3 is the run's ends.

### Code outside the proposal
- **`lib/holes.py`, the joint check:** sound. It takes `JOINT_OVERLAP_M` 0.2, its own constant, and prints a line per joint and a summary. A joint short by more than 1 µm is a problem that `build.py` refuses. The generator's `_reach` measures the boxes it emits, the ones in the metadata.
- **`tools/assets/derive.py`, a cover with its asset's relief:** sound.
  - `patches.relief` names a params file and a generator; `relief_inputs(params)` gives the same `lib/mat` relief the top's mesh and the mat are shaped by.
  - The stamp records the relief inputs and the params path, so a change to either re-derives.
  - The refusals are tested: relief on a non-cover, a missing key, an unknown generator, one without `relief_inputs`, params that do not give it.
- **The tone sits under the mesh's own relief.** `qa_build.py` reads 3 231 top vertices on the plain margins (relief at full) and samples the shipped albedo at their UVs as the importer tiles it (1/40, offset 0.5):
  ```
  their UVs are metres from the relief's centre within 0.00e+00 m
  correlation of the albedo's luminance with the mesh's relief: as Unity samples it +0.368; v flipped -0.015; u and v swapped -0.010
  ```
- **`lib/strip.spill_uv`** is gone with its last caller (`git grep` finds none).
- **The rebuild in plain Python** is identical to the shipped metadata in `collision`, `gaps`, `holes`, `lods`, `micro`, `materials` and `bounds`. F-2 by QA's own band distance: 139 022 samples, the least 0.65000 m inside the bands.
- **The mat's corridor:** away from the ends, the nearest mat vertex below the lip stands 0.3256 m from the centre line (the drone's half-span 0.287 m). Of 12 117 vertices below the berm's sink and within 5 cm of the lip, 72 lie more than 2 mm behind their wall's face (straw ends at the lip's corner); the hanging straws are in front of their walls.

## R3. Past each run end, a contact sees `belt_bare` under the lodged mat
**Measured** (QA's top-surface map above, from the shipped metadata):
- **The defect:** past each end wall the berm turns round the run's end as level ground under the mat. There the top of the collision is the end block's top, `surface:belt_bare` and not landable.
  - 1.725 m² and 2.015 m² at the two ends, within 1.56 m of the end point
  - 3.22 m² of it inside the run's band, the hole cells where the asset's own collision is the only ground in the world (design 6)
- **The ambiguity:** past the parapet caps, out to the end block's far edge 1.925 m past the end, the margin columns' `belt_straw` tops lie on the end block's `belt_bare` top within 0.5 mm, over another 7.86 m².
  - The metadata contract does not say which surface a contact takes when tops tie, so the answer depends on the contact's resolution order. The end blocks come before the columns in the shape list.

**Why it matters:**
- **The surface rows differ where it counts for a landing:**
  - `belt_bare`: bearing 1.4e7 N/m³, sink at most 3 cm, cover of twigs and litter
  - `belt_straw`: 4.7e6 N/m³, 8 cm, a straw mat with hooks (0.15) and grass
- **Where it is:** a heavy drone set down on the visible mat just past a trench's end would sink less, slide on loam, and never meet the straw that hooks props.
- **AST-05** wants "a material id per shape" that is the ground's. **REAL-05:** the physics must follow what the drone sees.
- **The spec scenario** "Parapets and clods are ground" names the parapets and margins as `belt_straw`. S4 made the berm `belt_straw` for the same reason, and the run's end is the same berm.
- **Since when:** the end blocks date from task 1.1, and task 1.6's as-built note records them ("stays the end block's `belt_bare` top (F-3 needs a filler top at the band's end)").

**To close it** (asset-artist, with the physics engineer's ruling where needed):
- under every level grassed ground past the run's ends, a landable `belt_straw` top, as on the berm
- no `belt_bare` top co-planar with a `belt_straw` one under the mat
- F-3 still met at the band's edge. Two ways:
  - keep a filler's top at y = 0 only on F-3's ring
  - or the physics engineer rules that F-3 may read a `belt_straw` top there, and `lib/holes` follows
- **Or** the physics engineer accepts `belt_bare` past the ends (a trampled trench mouth) and sets the rule for tied tops, and the orchestrator records both in design 4.

**QA re-checks with the same map:** outside the opening, `belt_bare` is the top only on the cut's own edges, and on F-3's ring if the rule keeps it there.

## Spec scenarios (updated)
| Scenario | Result | Evidence |
|---|---|---|
| E01: built from params by one command | **Passed** | two runs (18:04, 18:19), exit 0; the FBX (`ec11183a…`, the committed LFS object), metadata, prefab, derived maps and both review sheets byte-identical between them; 946 audit, hole and joint lines identical; `git status --short` empty after each |
| E01: the corridor against the drone | **Passed**, with design 5's accepted numbers | 48 sections, floor to lip at the batter; each `narrowest_m` on the audit's circle (48 lines, 0.8134–0.8705); verdicts in S2 |
| E01: fillers under the hole contract | **Passed** | F-1…F-8 below; R3 is a surface, not a filler, defect |
| E01: the G look | **Passed for G1**, with the stand-ins below | R1 |
| E01: parapets and clods are ground | **Partly met** | parapets, margins, berm and caps `belt_straw` landable; clods `crater_spoil`; `wind_volumes` and `snag` empty; the run's ends R3 |
| Pipeline: a trench writes its hole | **Passed** | `run` 50 points, band 2.35 m; `niche_0`, `niche_1` 2.1 m; importer line `3 holes (run 50 points, band 2.35 m, ...)`; EditMode `Meta_ExposesTheRunsHoleThenTheNiches` passed |
| Pipeline: hole validated | **Passed** | `test_holes_validated` in QA's `pytest` run |
| Review sheet: pilot-height tile of a trench | **Passed** | `[Review] earthwork pilot pose: camera (0.133, 3.350, -0.377), 1.500 m above the lip, 0.400 m to the field side (right) ... in frame True, top in frame True` |
| Review sheet: other classes unchanged | **Not re-run** | `ReviewCapture.cs` is outside task 1.6's diff (`git diff 5c8baff..0269a1f` touches no capture code) |

**The build's hole lines** (both runs):
```
holes F-1 sealed below: 42388 vertical rays on a 5 cm grid over the bands and 0.71 m past their edges, 0 miss every filler
holes F-2 the cavity inside the bands: 139022 samples of the cut's faces (slots wall), the least 0.6500 m inside the bands at (3.1826, -0.9245, 1.6769), at least 0.65 m
holes F-3 level lips: 1290 samples along the bands' edges, the fillers' top within 0.0 mm of the ground, tolerance 30 mm
holes F-4 nothing proud: the highest filler point at y = 0.0008 m, at most 0.01 m
holes F-5 thick: 367 fillers, every box at least 0.300 m across, at least 0.3 m
holes F-5 overlap: 48 joints, the boxes along the run overlap 0.200-0.200 m, at least 0.2 m
holes F-7 surfaces: 367 fillers surface:belt_bare, 0 landable; 422 surface:belt_straw, 422 landable; 24 surface:crater_spoil, 0 landable
holes F-8 no wind obstacle: 0 wind volumes
F-6: 813 collision lines, 0 problems; worst box->mesh 38.699 mm (collision[788], tolerance 50 mm); near gaps/landable 16.067 mm (collision[630], tolerance 20 mm)
71 gap lines, each narrowest_m on the audit's circle
E01_curving: ... size [35.25, 2.3838, 13.75] m  tris 39172 / 39172 / 1835  micro E01_curving_LOD0_straw 483 straw 5796 tris
  micro E01_curving_LOD0_mat 63109 mat 405480 tris  813 collision shapes  fbx 18.8 MB  metadata written
```

**Requirement IDs (updated):**
- **Met:** AST-02, AST-03 (one command, twice), AST-12, SEC-04, D-003, PR-9.
- **Partly met:**
  - AST-05: R2 is closed and the mouths are recorded; R3 is a wrong surface past the run's ends.
  - AST-07: as in the first review.
  - AST-08: the ground 39 172 under 60 000 and the bake 411 276 under 450 000 by test, not by the build (S9); textures 2K at most.
  - AST-10: the mat is a baked stand-in until the shared generator.
  - REAL-01, REAL-03: the G1 stand-ins below; no feed yet.
  - REAL-05: R3.

## Commands (QA's runs)
```
git fetch origin; git checkout -B qa-e01b origin/unity/art/p1-e01-trench; git lfs checkout   -> 76/76 LFS objects
(Get-CimInstance -Namespace root/wmi -ClassName BatteryStatus).PowerOnline                    -> True before every Unity run; Balanced
pwsh tools/build_asset.ps1 E01   (twice)                                                       -> build_asset: OK (E01), exit=0 (18:04, 18:19); git status --short empty after each
& $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode ...    -> testcasecount="55" result="Passed" passed="55" failed="0"; exit 0
python -m pytest tools -q                                                                      -> (running)
python -m pytest tools/hygiene -k test_existing_tree_is_clean -rA                              -> (running)
python tools/assets/fetch.py --verify                                                          -> OK, exit 0
python tools/hygiene/precommit.py --tree HEAD                                                  -> 827 files scanned, 0 findings, exit 0
openspec validate p1-e01-trench --strict                                                       -> Change 'p1-e01-trench' is valid
git merge-tree --write-tree origin/unity/main HEAD                                             -> a tree, no conflict
```
- **Commits** `5c8baff..0269a1f`: 8, each subject-only, lower case, 55–70 characters, no body, no trailer.
  - Identities: Asset Artist `assets@450fpv.local` (code, art, task text) and Orchestrator `orchestrator@450fpv.local` (task text, proposal).
- **Committed paths:** nothing under `reference/`, `Library/`, `Temp/` or `Logs/`. The changed PNGs and the FBX go through LFS (`filter: lfs`).
- **Licences:** no new third-party file. `grassed_spoil` keeps the pinned sets.
- **OPSEC:** no file names, places, dates or OSD values in the branch (the scan) or in this review.

## New suggestions (not blocking)
- **S12. The LOD switch at 24 m shows.**
  - Straws more than about 15 m from the camera alias into a pale grainy band over the parapets. Seen from just inside 24 m of the patch's centre, at 24 m that band gives way to smooth shaded humps.
  - For the tech-artist at G1: judge it through the feed (its blur may hide it), or cross-fade the LODs.
  - AST-10's near-ring fade replaces the bake in phase 3.
- **S13. F-5's overlap is checked only when the generator reports joints.** An asset with `holes` whose generator returns no `joints` passes without a line. Either `lib/holes` requires joints when there are holes, or it measures the overlaps from the metadata's boxes itself.
- **S14. `grassed_spoil` carries E01_curving's relief.** Its `patches.relief` names one variant's params, so the map's tone fits that patch alone. E02–E04, or a second E01 variant, need their own cover recipe or a per-asset derivation.
- **S15. Stale text, for archive:**
  - design 10 still lists `bend_deg` 25 (built 35) and omits `bend_radius_m` and `leg_jitter_m`
  - `trench.py`'s module docstring still calls `grassed_spoil` a cover with "no ... relief tone"
  - `TrenchPrefabTests`' class summary says the mat draws "on LOD0 and LOD1"
  - Risks still say 85 px/m (76 as built)
  - the names list leaves out the range differences: the sheet's `top_width_m` 0.6–1.2 against `top_w_m` 0.6–1.0, and `bottom_width_m` 0.5–1.0 against 0.5–0.8
  - the run's ends carry no test like `test_the_berm_is_grassed_ground` (R3)

## The G1 stand-ins (updated): what E01 does not yet copy from G
1. **The mat's mass.** G's grass is a deep, tufted golden mat 0.1–0.4 m thick, with strong crevices. E01's is a thin layer of fine straws over a mottled map: hay strewn on a pale ground at 1–2 m, dry stubble beyond. It is AST-10's density (phase 3).
2. **The lip's soil edge.** G's lip is hidden under grass over most of its length, with dark notches and crumbs. E01's grass breaks it in clusters, but between them the soil edge is the 1–10 cm crumble walk, a clean straight line from 1–2 m.
3. **Colour and light:**
   - G's golden-orange straw and dark inside under a low sun; the sheet's 45° sun gives beige parapets and a lit far wall
   - E01's tussock tone shows a regular dappled mottle on the margins at 10 m and more, which G does not have
   - The look setup's (G1) and the feed's (phase 4).
4. **The trench's irregularity:**
   - width jitter of 0–2.5 cm a wall is now built
   - G's bends are tighter and less regular than the regular 3.7 m arcs
   - the walls read smooth and plaster-like at 1.5 m, where G's are rough and in shade
5. **Weeds and shrubs** on and beside the parapets (G 4, 8, 11, 12). Not in E01.
6. **The setting:** the belt's trees and shrubs on one side and the field on the other. These are the world's (phase 3).
7. **The floor:** dark clods and pale floor straws, unseen in G's shade. Recheck under the G1 light.
8. **Far views:** the LOD switch at 24 m (S12), and the run's squared-off ends on the patch, which the world's placement hides.
