# Review: p6-test-range (task 2.1), with G1's code

**Reviewer:** qa-engineer, fresh (did none of the work, D-034) · **Branch:** `unity/world/p6-test-range` · **Tip under review:** `7d47968e` · **Range:** `bf94f75..7d47968e` (53 commits, 5 of them merges; 289 files)
**Scope:** `p6-test-range` 1.1–1.3 and the G1 code stacked under it (`p1-g1-look-lock-gallery` 1.1, 1.2, 1.4 and 1.3's R01 part: `LookSetup`, the noclip camera, the gallery scene and builder), which lands with it after this one pass (D-046, D-047). G1's gate is closed as seen and its remaining tasks are dropped (D-046): its look misses are recorded, not re-judged.
**Read:** `CLAUDE.md`, both changes' proposal, design, spec deltas and tasks with every as-built note; D-040, D-044, D-046, D-047; PLT-02, PLT-06, PLT-07, WLD-04; on `refs/godot/main` at `8340138`: `game/assets/catalog.json`, `game/src/world/Catalog.cs`, `game/maps/README.md` (shapes, gaps, the Euler order), `tools/blender/assetkit.py` (`rotation`), `blender/vegetation/tree_dead.py`, `blender/structures/house_adobe.py`, `tools/map/make_uat1_gallery.py` (`ADOBE_YARD`); the code under review (`godot_port.py`, `make_test_range.py` and its test, `TestRangeScene.cs`, `TestRangeBoot.cs`, `LookSetup.cs`, `LookModel.cs`, `ViewpointKeys.cs`, `LightPresets.cs`, `GalleryBoot.cs`, `Build.cs`, the EditMode tests).

**How QA ran it.** Everything below ran in the change's worktree after `git lfs checkout`; nothing from a handoff was reused. QA's scripts are plain Python or PowerShell in the scratchpad, never committed, and share no code with `tools/`. Unity 6000.3.26f1 with Hub running, every call piped; AC power checked before every Unity run (`BatteryStatus.PowerOnline` True), Windows' **Balanced** plan with the **best power efficiency** mode (overlay `961cc777…`). Other agents' Unity, simtests and pytest runs shared the machine throughout (CPU 49–92 % busy around the bench): the frame-rate numbers are a lower bound.

## Verdict: CHANGES REQUESTED

The build is sound. Every collision shape, gap, wind volume, porosity and snag flag of the thirteen stand-ins matches Godot's catalog to 0.0000 mm and under 0.0001°. The map's data regenerates byte for byte. The scene regenerates from its builder with no semantic difference. The 731 placed instances match the file to 0.0000 mm. The player builds, smoke-tests clean, loads in 6.7–7.8 s warm and holds 182–220 fps at the 1 % low on every leg.

What blocks is text and one test that does not test what its scenario says:
- **B1:** the posadka-shape scenario fails as written. The trunks span **9.79 m** against "10–30 m", and the test measures every plant instead.
- **B2:** three `test-range` spec or design lines the build does not meet as written.
- **B3:** G1's tasks and spec do not record what D-046 dropped, so they claim an H01 station, a continuous 200 m band and a frame-rate reading the code does not have.

None needs a rebuild of an asset.

## Blocking items

### B1. The posadka's width is measured on every plant, and the trunks span 9.79 m
The scenario reads: "the posadka's **trunks** lie within a strip 10–30 m wide … the nearest-neighbour distance between **trunks** has its median within 1–3 m". `test_the_posadkas_shape` takes every row of group `posadka`, the 150 bushes included, for both lines. On the trunks alone (the 505 trees: Z08–Z12 and V01):
```
trunk strip: x -139.711..159.497 (length 299.208 m), z -4.895..4.895 (width 9.790 m)
principal axis -0.018 deg: length 299.207 m, width 9.799 m
trunk nearest-neighbour: median 1.993 m, min 1.504, max 2.955
```
Four rows 3 m apart with 0.4 m of jitter cannot reach 10 m (9 + 0.8 at most). The 12.7 m that `tasks.md` and `docs/test-range.md` quote is the planted width with the edge bushes. A known-wrong belt passes the test too: put every trunk on one line and keep the edge bushes, and the width still reads 12.7 m. **Fix, one of:**
- **(a)** widen the trunk rows to at least 10 m (a fifth row, or rows 3.5 m apart) and make the test measure the trees;
- **(b)** the orchestrator amends the scenario to the belt's planted width ("the posadka's plants, bushes included"), which is what WLD-04's "10–30 m wide" means for a belt, and the test stays.

Either way the nearest-neighbour line and its test must name the same set. On trunks the median is 1.993 m and on plants 1.72 m: both pass.

### B2. Spec and design lines the build does not meet as written
1. **"a yard … with grass round it"** (Contents). There is no S04 round the yard; its ground is the floor's straw texture. Task 1.2 records the reason: the patches read as tiles from the air. Design 8's "S04 instanced round the yard, the start points and along the posadka's edges" is stale the same way. Amend both to the verges, or place the grass.
2. **"every collision shape … and the porosity, snag flag and LOD distances match"** (The metadata survives the port). The switches are Godot's except its last; Godot's last level is not ported (a tree's far card, a structure's coarsest mesh); and the cull is 600 m, not Godot's last switch (55–200 m). Ports 1.1 and 1.2 explain each choice and QA agrees with them (a stand-in culled at 150 m left holes in a 530 m view). The scenario must say so, for example: "the LOD switches match Godot's but its last, which was its far level's; the cull is 600 m, past the map's sight lines".
3. **Design 2 and 3** describe a different port: "imports it with Blender's glTF importer", "lower LODs come from the pipeline's own decimation, with the last LOD's cull at Godot's last lod_switch_m", and "a Godot material name without a Unity physics material maps to the nearest one". As built, the port reads the glb in plain Python, ports Godot's own LOD meshes, decimates nothing and maps nothing. Update the design to the as-built notes.

### B3. G1's tasks and spec do not record D-046
`p1-g1-look-lock-gallery/tasks.md` still shows 1.3, 2.1 and 2.2 as open boxes with no word of D-046. Its spec claims what its code does not have, so archiving it would write those claims into the main specs:
- **Six stations** with "H01 in I, D and X side by side": the H01 station is an empty marker (gallery smoke log: `station 3 H01 at (100, 0, 0): empty marker`).
- **Belt at 200 m** and **The 200 m belt view**: "the band is continuous, not a row of separate cards". Task 1.1 measured "three sparse crowns, not a band" (V01's crown net, D-040).
- **Frame rate** (gallery): never measured, because 2.1 was dropped.

**Fix (orchestrator, text only):** mark 1.3's H01 part, 2.1 and 2.2 as dropped by D-046 in `tasks.md`. Amend or annotate those three scenarios so they say what was dropped and what stays open as V01's look miss.

## Non-blocking findings
- **N1. `c91d0cfa`, the merge with a `# Conflicts` body.**
  - **What it is:** the Tech Artist's merge of `origin/unity/main` into G1's `--look` branch. It kept git's template body (`# Conflicts:` / `#	openspec/changes/p1-g1-look-lock-gallery/tasks.md`) under git's default 95-character merge subject. That breaks the subject-only rule. The hook exempts the default merge subject (`MERGE_SUBJECT`) and skips `#` lines, so it passed.
  - **Why QA does not ask for a rewrite:** the commit is in the history of nine remote branches: `p1-g1-look-lock-gallery` and `--look`, `p6-test-range`, `p6-world-query-gallery`, `p4-feed` and `--last-field`, `p6-input-app--player`, `p6-flight-core--range` and `qa/p6-input-app--review`. A message-only rewrite changes its id and every descendant's. Unless all nine are rebuilt in one pass, the trunk would end up holding both the old commit and its copy, which is worse than the two template lines. Several of these branches have live agents on them. The trunk already holds five merges with the same body (`ad72c0e`, `098a9f0`, `b363eae`, `e50d941`, `d18389d`; V01's review rated it S8, non-blocking). The flight core's `162e7dd` was rewritten because it sat on one branch only.
  - **Verdict:** accept as is and record it. The one safe moment for a rewrite is a coordinated pass over all nine branches before the first of them merges, and QA does not think two template lines are worth that.
- **N2. `Gallery_LookLock.unity` is one field behind its builder.** Re-running `Fpv450.GalleryScene.Build` changes one document: `ViewpointKeys` gains `views: []`. The field arrived with `0ae66ba` after the gallery scene was last built. It changes nothing at run time (empty means the gallery's table). Re-run the builder and commit the scene alongside the B fixes (world-builder). `TestRange.unity` is current.
- **N3. The bench reports less than PLT-07 lists.** `-bench` logs the average, the 1 % low (1 / the 99th-percentile frame) and the worst frame. It does not log CPU and GPU ms, draw calls, triangles or VRAM, and the player prints neither clocks nor power mode. QA printed the power state and the clocks around it and sampled `nvidia-smi`. **Suggested:** `FrameTimingManager` and the render `ProfilerRecorder` counters in `-bench`, so the next reading is the full protocol.
- **N4. The first launch after a build is slow.** Warm launches take 6.7–7.8 s from launch to the first frame. The first launch after QA's incremental build took 19.1 s from launch to exit, with the player's own clock at 3.50 s at its first frame. So about 14.6 s passed before the engine's clock started: Windows' first open of the build's 437 MB of new files, which the code does not control. Task 1.2 saw 11.9 s. The pilot's first double-click of each new build is such a launch, and PLT-06 asks for 10 s. **Suggested:** `tools/build_latest.ps1` runs one `-smoketest` after building, so the pilot's first launch is warm.
- **N5. The snag flag is kept but nothing reads it.** The port writes Godot's `snag_hazard` into the `stand_in` record and leaves `snag` empty. The importer (`CatalogImporter` reads `snag`), `AssetPhysicsMeta` and the sim never read `stand_in`. The spec line is met (the flag is kept), but the 7d fiber change and `p6-world-query-gallery` 1.4 must read it, or no stand-in tree, fence or gate will snag the fiber.
- **N6. The stand-in trees have no crowns to hit (G7 expectation).** Godot's catalog gives each tree a trunk cylinder and 3–5 limb capsules; the leaf cards and twigs have no collision. The port keeps that (D-040, and the audit is not asked of it). As placed, 447 of the posadka's 505 trees are stand-ins, so the drone will pass through most crowns and stop only on trunks and main limbs; V01, with 184–466 capsules, is the exception. That is not what "flying among dense trees" (VISION §2) will feel like. **Suggested:** say it on the pilot's page (`docs/test-range.md`) before G7, so a pass through a crown is read as a stand-in, not a physics bug.
- **N7. Stand-in looks for the pilot at G7 (D-040, no action now).**
  - **Z12 `tree_dead`:** green leaf cards, although its Godot generator says "no leaves at all" (`leaves=0`). The pinned glb itself carries a `foliage_belt` primitive, 1640 vertices on LOD0, in the same green as the other trees, so the port is faithful to the glb. The map's 35 dead trees read as leafy trees.
  - **Z01's walls:** a chequer of two clay tones (Godot's render patches, `house_adobe.py`).
  - **The S04 verges:** pale tiles with dark pits from 2 m (S04's own, seen at G1).
- **N8. Role scope.** The World Builder changed the stand-ins' params, the generator's default, its test, the metadata and the prefabs (`96de0c7`, `63514a5`), which are asset-artist files. The outputs are what the generator writes (see the rebuild below), so this is a note, not a fix.
- **N9. Before the PR:** `unity/main` is 102 commits ahead of the base, with no file in common with this branch's 289; merge it with `-m`.

## Spec scenarios: `test-range`
| Scenario | Result | Evidence |
|---|---|---|
| Everything placed | **passed** | Smoke log: 29 variants with counts, `731 prefabs placed in 8 groups, 29 variants, 0 missing meshes, materials, textures or unsupported shaders`; every list item present, H01_I and H01_D included; QA's 24 captures (6 views × 4 presets) show no pink and no missing mesh |
| No text, sign or name | **passed** | No `TextMesh` or `m_Text` in either scene; the only text is the preset's label |
| The posadka's shape | **failed as written (B1)** | Length 299.2 m, nearest-neighbour median 1.993 m, V01 or V13 24.58 %, a gap of 13.96 m between plants and **10.84 m clear of every collision shape at any height** all pass; the trunks' width, 9.79 m, does not |
| One command rebuilds a stand-in | **passed** | All 13 exit 0 and leave `git status` clean, Z01 twice (rebuild section); stronger than asked, since the first run from the committed state is already a no-op |
| The metadata survives the port | **passed except the LOD line (B2.2)** | 278 shapes, 18 wind volumes, 11 gaps, porosity, materials and snag flags at 0.0000 mm and under 0.0001°; switches equal Godot's but its last; cull 600 m; the audit puts every shape within the LOD0 bounds + 5 cm (farthest +0.0000 m) |
| The door and gate are gaps | **passed** | Z01's 8, Z02's, Z03's and Z05's gaps: corners within 0.0000 mm of Godot's centre, width, height and yaw; `narrowest_m` the smaller side; normals along Godot's ±Z |
| Pose parity | **passed for the scene; the sim half is not testable here** | 731 instances against 731 rows, worst 0.0000 mm and 0.00003°; no sim loader is on this branch (`p6-world-query-gallery` 1.4 must prove its half) |
| Start points listed | **passed** | `track` legs on, heading 90°, 7.12 m from the nearest shape (fence) and 5.74 m in plan from the nearest wire; `field` legs off with the stand, heading 180° towards the belt, 25.6 m from the nearest shape |
| Presets switch | **passed, by proxy** | EditMode `PresetKeys_SelectTheirPresetAndTheLightSwitchesBeforeTheFrameEnds`; in the player, `-capture` logged each preset as it applied and its frames show the four lights. A real key press cannot be driven in batch (task 1.2's note) |
| Smoke test | **passed** | Exit 0; the log lists the scene and every prefab |
| Frame rate | **passed** | Yard 315.4 average and **192.3** 1 % low; posadka at 2 m 315.3 and **182.3**; fields at 50 m 411.8 and **219.7** (worst frames 13.96, 17.31 and 7.36 ms) |
| Load ≤ 10 s (PLT-06) | **passed warm**; first launch over (N4) | 6.74–7.84 s from launch to the first-frame line in three runs |

The yard against `ADOBE_YARD`, mapped (dx, dz, yaw) → (dx, −dz, −yaw) about (−170, −57): all 14 rows exact. The noclip start (−194, 25, −79), yaw 69.4°, pitch 27°, stands beside the yard's south-west corner. It frames the yard below and the belt beyond (capture 1), so "above the yard looking along the posadka" is met in spirit.

## Spec scenarios: G1 (`look-setup`, `look-lock-gallery`)
| Scenario | Result | Evidence |
|---|---|---|
| Equal illuminance of day and overcast | **passed** | EditMode `DayAndOvercast_LightAHorizontalGreyWithinFivePercentOfTheReviewRigsDay`; the player's log: day and overcast both 0.972 |
| Night silhouette | **passed (model), not re-judged by eye** | EditMode `Night_SkyAboutSrgb93AtTheHorizonOverNearBlackGround`; QA's night captures show dark poles against a grey sky over near-black ground |
| Belt at 200 m | **failed, closed as seen (B3, D-046)** | Task 1.1: three sparse crowns, not a band |
| Stations present | **partly (B3)** | 5 of 6; H01 is an empty marker |
| Nothing added by hand | **passed by construction** | Stations are prefab instances by GUID |
| Jump to a viewpoint | **passed** | EditMode `EveryNumberKey_LandsOnItsViewpointAndNoclipKeepsIt` |
| The 200 m belt view | **failed, closed as seen (B3)** | as above |
| Switching presets | **passed** | as for the test range; label shown for 1 s (`LookSetup.OnGUI`) |
| Build and smoke test | **passed** | `Fpv450.Build.Gallery` exit 0 (338 MB); `450fpv-gallery.exe -smoketest` exit 0, the scene and its stations logged |
| Frame rate (gallery) | **not run** | dropped with 2.1 (D-046) |

The G1 code against its spec lines:
- `LookModel.SunPosition` puts noon at 45.0° due south at 45° latitude and the low sun at 5.3° and 265°, as the presets claim (QA's hand check: sin el = cos 45° · cos 82.5°).
- Fog stripping keeps the exponential variant only, the one mode `LookSetup` sets; no scene or script uses another.
- `NoclipCamera.MoveTo` clamps the pitch and continues mouse look from the pose.
- `Build.OneScene` restores the project's player settings in `finally`.

## Requirements
- **PRD-07** (noclip): met.
- **WLD-04** (belts): partly. Bushes among the trees, a 12 m gap and trunks 1.5–3 m apart are met; the trunks' 9.8 m width is B1.
- **WLD-05** (a yard): met at the Godot yard's scope.
- **WLD-08** (start points): met. The stand itself comes with the flight session.
- **REAL-05** (collision follows the visual): partly. The structures follow; the stand-in crowns are pass-through (N6, D-040).
- **AST-12** (review sheets): met. 13 sheets, each read below.
- **PLT-02** (noclip row): met with margin.
- **PLT-06** (load times): met warm (N4).
- **PLT-07** (benchmark protocol): partly; the counters are missing (N3).
- **D-027** (port, never merge): met. `8340138` is not an ancestor of the branch, and the branch shares no history with the Godot line. All 13 catalog entries carry `"stand_in": true` and commit `8340138…`; each params file pins the model's LFS id, SHA-256 checked.

## The stand-ins: metadata, shape by shape
`qa_port_meta.py` works as follows:
- Godot's Euler [yaw, pitch, roll] becomes R = Ry·Rx·Rz, as in its `assetkit.rotation` and `Catalog.cs`.
- The frame change is S = diag(1, 1, −1), applied as S·p to points and S·R·S to rotations.
- Each asset quaternion is turned back into a matrix with the textbook formula, and the angle between the two matrices is measured.
- Gaps are rebuilt from Godot's centre, width, height and yaw ("faces ±Z turned by yaw") and matched corner to corner.
- Wind follows Godot's own rule: its `wind_volume`, else its collision; porosity ≥ 1 means none (`MapSelfTest.cs`).
```
Z01 house_adobe   shapes 157 worst pos 0.0000 mm size 0.0000 mm angle 0.00009 deg | wind 1 por 0.11 | gaps 8 worst 0.0000 mm | snag True | Godot [45.0, 130.0] | asset [0.0, 45.0] cull 600.0
Z02 shed_plank    shapes  35 angle 0.00007 deg | wind 1 por 0.04 | gaps 1 0.0000 mm | snag True | Godot [25.0, 70.0] | asset [0.0, 25.0] cull 600.0
Z03 cellar_head   shapes  11 angle 0.00006 deg | wind 11 por 0.0 | gaps 1 0.0000 mm | snag True | Godot [22.0, 60.0] | asset [0.0, 22.0] cull 600.0
Z04 cellar_shaft  shapes  10 angle 0.00000 deg | wind 0 por 1.0 | gaps 0 | snag False | Godot [] | asset [0.0] cull 600.0
Z05 gate_plank    shapes  14 angle 0.00007 deg | wind 1 por 0.62 | gaps 1 0.0000 mm | snag True | Godot [20.0, 55.0] | asset [0.0, 20.0] cull 600.0
Z06 fence_planks  shapes  19 angle 0.00005 deg | wind 1 por 0.35 | gaps 0 | snag True | Godot [] | asset [0.0] cull 600.0
Z07 tree_fruit    shapes   5 angle 0.00008 deg | wind 1 por 0.62 | snag True | Godot [26.0, 70.0, 170.0] | asset [0.0, 26.0, 70.0] cull 600.0
Z08 tree_young    shapes   1 | wind 1 por 0.62 | snag True | Godot [22.0, 60.0, 150.0] | asset [0.0, 22.0, 60.0] cull 600.0
Z09 tree_maple    shapes   5 angle 0.00007 deg | wind 1 por 0.45 | snag True | Godot [30.0, 80.0, 200.0] | asset [0.0, 30.0, 80.0] cull 600.0
Z10 tree_acacia   shapes   5 angle 0.00006 deg | wind 1 por 0.55 | snag True | Godot [30.0, 80.0, 200.0] | asset [0.0, 30.0, 80.0] cull 600.0
Z11 tree_ash      shapes   6 angle 0.00006 deg | wind 1 por 0.5  | snag True | Godot [30.0, 80.0, 200.0] | asset [0.0, 30.0, 80.0] cull 600.0
Z12 tree_dead     shapes   4 angle 0.00007 deg | wind 1 por 0.8  | snag True | Godot [30.0, 80.0, 200.0] | asset [0.0, 30.0, 80.0] cull 600.0
Z13 shrub_belt    shapes   1 | wind 1 por 0.5  | snag True | Godot [15.0, 40.0, 95.0] | asset [0.0, 15.0, 40.0] cull 600.0
total mismatches: 0
```
The Euler order was checked against the data, not taken from the port's note. Z05's leaf boards step 0.64 m in x and 0.448 m in z, tan 35°, so Godot's first angle is a yaw.

**The look at the sheets** (`review-sheets/Z0n/`, each 10 m panel and collision overlay read in a montage). There is no mirror and no gap on the wrong wall. Z01's eight magenta gaps sit in its door and window openings; Z05's boxes lie on its open leaf's boards; every tree's trunk cylinder and limb capsules lie on the drawn trunk and limbs; Z04's walls and steps sit inside its boxes. The look notes are N6 and N7.

## The map's data, the scene and the builds
- `make_test_range.py`, run twice to scratch files: both `a3718564…63e7`, byte-identical to the committed `placements.json`.
- `-batchmode -nographics -quit -executeMethod Fpv450.TestRangeScene.Build`: exit 0 in 56 s; every span's conductors on their hooks (0.00–2.53 mm, the stub's). The rewritten `TestRange.unity` differs in bytes only because Unity draws new fileIDs. `qa_scene_diff.py` replaces every local fileID with the referenced object's name and script and compares the documents as multisets: **0 of 1 505 differ**. It does flag a 1 mm edit planted in one instance's x (the known-wrong check). The working copy was restored after.
- `Fpv450.GalleryScene.Build`: exit 0 in 63 s; one document differs (N2). Restored.
- `Fpv450.Build.TestRange`: exit 0 in 70 s, `Succeeded, 0 errors, 0 warnings, 437523921 bytes`.
- `Fpv450.Build.Gallery`: exit 0 in 59 s, `Succeeded, 0 errors, 0 warnings, 338257838 bytes`.

## The stand-ins rebuilt
QA's `rebuild_standins.ps1` ran `pwsh tools/build_asset.ps1 Z0n` for all thirteen, then Z01 a second time, each after at least 2.5 GB was free. It recorded the exit code and `git status --porcelain` after each run:
```
== Z01 exit 0 in 99 s (AC True, 4118 MB free at start); git status: clean
== Z02 exit 0 in 42 s (AC True, 5651 MB free at start); git status: 1 lines      <- QA's own review.md, uncommitted at that moment
== Z03 exit 0 in 43 s ... clean      == Z04 exit 0 in 39 s ... clean      == Z05 exit 0 in 89 s ... clean
== Z06 exit 0 in 61 s ... clean      == Z07 exit 0 in 49 s ... clean      == Z08 exit 0 in 54 s ... clean
== Z09 exit 0 in 40 s ... clean      == Z10 exit 0 in 41 s ... clean      == Z11 exit 0 in 44 s ... clean
== Z12 exit 0 in 37 s ... clean      == Z13 exit 0 in 43 s ... clean
== Z01 exit 0 in 74 s (AC True, 3135 MB free at start); git status: clean      <- the second run
```
Every committed FBX, `.assetmeta.json`, prefab and material is exactly what one command writes from the pinned commit. QA read the montages above from the sheets the 600 m build left (each legend reads "CULL 600 M"). These runs rewrote the sheets from the same, unchanged inputs.

The audit lines, from the Blender step alone (`build.py -- Z01 … Z13`: exit 0, `git status` clean after):
```
Z01 157 shapes, LOD0 bounds [-5.300, -0.081, -4.708]..[7.501, 5.358, 3.809] + 0.05 m: farthest +0.0000 m, every one within
Z02  35 shapes +0.0000 m | Z03 11 +0.0000 m | Z04 10 +0.0000 m | Z05 14 +0.0000 m | Z06 19 +0.0000 m
Z07   5 shapes -0.0127 m | Z08 1 -0.0079 m | Z09 5 -0.0227 m | Z10 5 -0.0092 m | Z11 6 -0.0088 m | Z12 4 -0.0165 m | Z13 1 -0.0044 m
11 gap lines (narrowest_m against the inscribed circle)          build.py: OK
```
**Does the audit fail a wrong port?** QA mirrored each structure's collision in z, the frame error the design names, and measured it against the same LOD0 bounds. Z01 lands +0.900 m past them, Z02 +0.545 m and Z05 +0.678 m, so the audit fails all three. Z03, which is nearly symmetric in z (+0.0004 m), passes it. For Z03 the overlay is the check, and the overlay shows the head's boxes on its walls.

## Hygiene
- No path under `reference/`, `Library/`, `Temp/`, `Logs/`, `Builds/` or `review-sheets/` in the range.
- `precommit.py --tree HEAD`: 2 115 files, 0 findings. `fetch.py --verify`: OK. The stand-ins embed no texture, and the palettes are generated, so the manifest is unchanged.
- Commits: 25 World Builder, 11 Asset Artist, 9 Tech Artist, 8 Orchestrator, each under its role's identity, subject-only, 41–72 characters; no `Co-Authored-By` anywhere. The one exception is `c91d0cfa` (N1).
- OPSEC: no place, coordinate, date or OSD value in the docs, the map or the presets. The sun arc's 45° latitude is labelled assumed. No sign or name is in the map.

## Commands and output
```
$ python -m pytest tools -q -p no:cacheprovider
1451 passed in 1605.91s (0:26:45)                                   exit 0
$ python tools/assets/fetch.py --verify
OK                                                                   exit 0
$ python tools/hygiene/precommit.py --tree HEAD
2115 files scanned, 0 findings                                       exit 0
$ openspec validate p6-test-range --strict
Change 'p6-test-range' is valid                                      exit 0
$ openspec validate p1-g1-look-lock-gallery --strict
Change 'p1-g1-look-lock-gallery' is valid                            exit 0
$ & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults <scratch>/qa_editmode.xml ...
exit=0 seconds=179; result Passed, total 835, passed 835, failed 0, skipped 0
  (GalleryTests 2, LookSetupTests 4, TestRangeTests 4: all passed)
$ & $env:UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.TestRange ...
[Build] TestRange: Succeeded, 0 errors, 0 warnings, 437523921 bytes in 00:00:53.97      exit=0 seconds=70
$ 450fpv-test-range.exe -smoketest -windowed      (AC True, Balanced, CPU 3201 MHz, RTX 4050 driver 617.14)
[TestRange] TestRange loaded, first frame 3.50 s after start: ... 1920x1080 Windowed, quality High, LOD bias 1, vsync 1
[TestRange] E01 E01_curving x 1 ... [TestRange] Z13 Z13_shrub_belt x 47          (29 lines)
[TestRange] 731 prefabs placed in 8 groups, 29 variants, 0 missing meshes, materials, textures or unsupported shaders
exit code 0, wall clock from launch to exit 19.13 s      (the first launch after the build, N4)
$ load_time.ps1 -Runs 3   (450fpv-test-range.exe -smoketest -windowed, the log watched every 50 ms)
run 1 : exit 0; first-frame line at 7.84 s after launch (player's own clock: 3.38 s), exited at 8.79 s
run 2 : exit 0; first-frame line at 6.74 s after launch (player's own clock: 3.47 s), exited at 7.74 s
run 3 : exit 0; first-frame line at 6.83 s after launch (player's own clock: 3.53 s), exited at 7.90 s
$ 450fpv-test-range.exe -bench -vsync 0 -windowed
AC power: True; power scheme: Balanced; overlay 961cc777-2547-4f9d-8174-7d86181b8a7a (best power efficiency)
CPU: AMD Ryzen 7 7735HS, current 3201 MHz of max 3201 MHz (before and after); GPU: P5, 2055 MHz, max 3105 MHz
[TestRange] bench yard: 9462 frames over 30.0 s, average 315.4 fps, 1 % low 192.3 fps (99th-percentile frame 5.20 ms), worst frame 13.96 ms, vsync 0, 1920x1080
[TestRange] bench posadka 2 m: 9460 frames over 30.0 s, average 315.3 fps, 1 % low 182.3 fps (99th-percentile frame 5.49 ms), worst frame 17.31 ms, vsync 0, 1920x1080
[TestRange] bench fields 50 m: 12353 frames over 30.0 s, average 411.8 fps, 1 % low 219.7 fps (99th-percentile frame 4.55 ms), worst frame 7.36 ms, vsync 0, 1920x1080
nvidia-smi over the run (92 samples): graphics clock 840-2700 MHz, mean 1770, VRAM used peak 518 MiB, GPU utilisation mean 55 %, pstates P0 P3 P4 P5
exit code 0, wall clock 103.47 s; machine CPU 49 % busy before, 92 % after (other agents)
$ 450fpv-test-range.exe -capture <scratch>/capture -windowed
exit code 0; [Look] day, low_sun (5.3° at 265°, 0.575), overcast (0.972, sun 0), night (0.065) each applied; 24 files
$ & $env:UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.Gallery ...
[Build] Gallery: Succeeded, 0 errors, 0 warnings, 338257838 bytes in 00:00:38.89      exit=0 seconds=59
$ 450fpv-gallery.exe -smoketest -windowed
[Gallery] station 1 S04 ... station 3 H01 at (100, 0, 0): empty marker ... station 6 L01 L03 ...
[Gallery] 16 prefabs placed, 0 missing meshes, materials, textures or unsupported shaders           exit 0
$ git merge-base --is-ancestor 8340138 HEAD    -> not an ancestor
```

# Second pass at `937bcafc` (task 2.1: B1–B3, N2 and N9 re-checked)

**Verdict: approved.** B1, B2 and B3 are fixed, N2 and N9 are done, and nothing blocks. The map stack can land.
- **Unity EditMode** on the merged tree: 885 of 885 passed (GalleryTests 2, LookSetupTests 4, TestRangeTests 4).
- **`Fpv450.Build.TestRange`**: Succeeded, 0 errors, 0 warnings. **`-smoketest`** exits 0 twice: 731 prefabs in 8 groups, 29 variants, 0 missing meshes, materials, textures or unsupported shaders.
- Three new findings, none blocking (N10–N12 below). N10 is a weak statistic in the posadka test, not a fault in the map.

**Scope:** `unity/world/p6-test-range` at `937bcafc`, range `b1d0e863..937bcafc`. That is four World Builder commits (`7ad533f1`, `4149b81d`, `e913c251`, `937bcafc`) and the trunk merge `28ab7179`. Since the first pass, the map's data, generator, scenes' builders, scripts and stand-ins are unchanged: `placements.json` is still `a3718564…63e7`. Only the posadka test, the two changes' texts and the gallery scene changed, plus what the merge brought: the flight core, `drone.json`, `physics_materials.json` and `test_meta.py`.

**How QA ran it:** in the change's worktree after `git lfs checkout` (230 of 230 objects present), with Unity 6000.3.26f1 and Hub running. Every Unity call ran on AC power (`BatteryStatus.PowerOnline` True) and waited for at least 2.5 GB free first. Other agents' jobs shared the machine. QA's scripts are plain Python and PowerShell in the scratchpad, never committed, and share no code with `tools/`.

## The findings, answered

| # | Finding | Fix | Verdict |
|---|---|---|---|
| B1 | The posadka's width was measured on every plant, and the trunks span 9.79 m | (b): the scenario's 10–30 m strip is the planted width, trees and edge bushes. The scenario adds "its trunks stand in four rows 3 m apart". `check_trunks` measures the trunks alone (four rows, 3 m apart within the 0.4 m jitter, median nearest neighbour 1–3 m). A known-wrong test proves it fails a belt with every trunk on one line | **Fixed.** Both lines now name their sets, and the test measures what each line says. QA's own script (below) agrees and fails four known-wrong belts. WLD-04 is met |
| B2.1 | "with grass round it" | Contents: "on the floor's straw-and-soil ground with no S04 patch round it". Design 8: S04 as the posadka's two verges only | **Fixed.** That matches the map (46 S04, all in the verges) |
| B2.2 | "LOD distances match" | Requirement: "LOD switches but the last". Scenario: switches equal Godot's but its last, that level not ported, cull 600 m | **Fixed.** That matches the first pass's measurement and the port's test |
| B2.3 | Design 2 and 3 described another port | Design 2–4 rewritten to the build | **Fixed.** QA checked each claim against the code. `lib/glb.py` reads the buffers, so no glTF importer is involved. Godot's own LOD meshes are ported, none decimated, and `lod.cull_m` defaults to 600. In `lib/godot.py`: every shape `landable=False`; a gap rectangle becomes a polygon with `narrowest_m` its smaller side; the wind volume is Godot's `wind_volume`, else the collision, and none at porosity 1. `snag_hazard` goes in the `stand_in` record and `snag` stays `[]`. Design 4's materials match the catalog's 13 stand-ins: 9 `bark_locust` and 6 `brick_red` slots, and 12 `godot_*` palette recipes |
| B3 | G1's tasks and spec did not record D-046 | 1.3: R01's part as built, H01's part "Dropped by D-046". 2.1 and 2.2 ticked "**Dropped by D-046.**", each with a note. Spec: H01's station is a numbered marker only; "Stations present" names five stations and the empty marker; "The 200 m belt view" becomes the camera pose; "Belt at 200 m" keeps the luminance and records the gap in the band as V01's open miss; "Frame rate" is removed, and the build requirement says no frame rate is claimed | **Fixed, and the change completes honestly** (below) |
| N2 | `Gallery_LookLock.unity` one field behind its builder | `937bcafc`: the builder re-run and its scene committed | **Done.** QA re-ran the builder on `937bcafc` (below): 0 of 67 documents differ |
| N9 | Merge `unity/main` with `-m` | `28ab7179` merges `de8d7399` | **Done.** Subject only, with an empty body. Its tree `6e17a1e2` is exactly `git merge-tree --write-tree e913c251 de8d7399`'s, so it is clean and nothing was hand-resolved. The trunk has since moved 2 commits (`bf722cc1`, `docs/decisions.md` only, D-054), and `git merge-tree` merges them clean. No second merge is needed |

**Does marking G1's 1.3, 2.1 and 2.2 make the change complete honestly? Yes.**
- **D-046 drops exactly these three.** It reads: "1.3 H01's station, the QA pass for the gate, the pilot's look". It keeps the code and its one QA pass, which is this review.
- **Every ticked box shows what it did.** Each either did its work or opens with "Dropped by D-046" in bold, and names what did not happen: the H01 placement, viewpoint 3 against a facade, key 9, the letter-read captures, the 200 m band and the gallery's frame rate.
- **2.2's note is true.** The pilot flew the five-station preview (`docs/style-rules.md`, "G1 look-lock"), and D-001 is closed at G1 by D-042.
- **The amended scenarios match the code:**
  - The 200 m pose is `GalleryStations.Viewpoints[7]`, "V01 V13 200 M" at (40, 2, −200), yaw 0, pitch 0: 2 m up and level, 200 m south of the row at z 0, which runs across x 33–50.
  - The empty marker is the gallery smoke log's `station 3 H01 at (100, 0, 0): empty marker` (first pass).
- **An archive will not write a false claim into the main specs.** No scenario left claims H01, a continuous band or a gallery frame rate.
- **The only cost:** OpenSpec counts a dropped box as done (`openspec list`: "✓ Complete"). The bold first words carry the record.

## Runs on the merged tree

**The posadka, by QA's own script** (`qa_trunks.py`; trunks = Z08–Z12 and V01; rows split where the sorted z steps 1.5 m or more):
```
as built:                       plants 655 strip 12.708 x 299.208 m; trunks 505; rows 4 at z [-4.473, -1.526, 1.523, 4.508], apart [2.947, 3.049, 2.985]; trunk NN median 1.993 -> PASS
known-wrong, trunks on one line: rows 1;                                   trunk NN median 0.259 -> FAIL
known-wrong, rows 2 m apart:     rows 1 (the rows run together)            trunk NN median 1.831 -> FAIL
known-wrong, rows 4 m apart:     rows 4, apart [3.929, 4.065, 3.98]         trunk NN median 2.013 -> FAIL
known-wrong, the north row gone: rows 3                                    trunk NN median 1.984 -> FAIL
known-wrong, trunks 3x sparser:  rows 4, apart [2.911, 3.002, 3.129]        trunk NN median 2.899 -> PASS   (N10)
```

**The gallery scene, from its builder** (`qa_scene_cmp.py`: Unity YAML documents compared as multisets, with every local fileID replaced by the referenced object's name, class, script or prefab source):
```
937bcafc^ against 937bcafc (the committed rebuild):   67 and 67 documents; 1 differs: ViewpointKeys gains `views: []`
$ & $env:UNITY -batchmode -nographics -quit -projectPath unity -executeMethod Fpv450.GalleryScene.Build ...
AC True, free 4469 MB; exit=0 seconds=41
rebuilt against 937bcafc:                             67 and 67 documents; only in A 0, only in B 0   (bytes differ: new fileIDs)
known-wrong, 1 mm planted in one m_LocalPosition.x:   only in A 1, only in B 1
git checkout -- unity/Assets/_Project/Scenes                                  -> git status clean
```

```
$ & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults <scratch>/qa_editmode.xml -logFile <scratch>/qa_editmode.log | Out-Host
AC True, free 5932 MB at start; exit=0 seconds=345
<test-run testcasecount="885" result="Passed" total="885" passed="885" failed="0" inconclusive="0" skipped="0" duration="277.8">
  GalleryTests Passed 2 | LookSetupTests Passed 4 | TestRangeTests Passed 4; 0 "error CS" in the log
$ & $env:UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.TestRange -logFile <scratch>/qa_build_testrange.log | Out-Host
AC True, free 2599 MB; exit=0 seconds=67
[Build] TestRange: Succeeded, 0 errors, 0 warnings, 437559457 bytes in 00:00:42.43
git status after the build: clean
$ unity/Builds/Windows-TestRange/450fpv-test-range.exe -smoketest -windowed -logFile <scratch>/qa_smoke_1.log      (the first launch after the build)
AC True; exit=0 wall=18.04 s
[TestRange] TestRange loaded, first frame 3.32 s after start: GPU NVIDIA GeForce RTX 4050 Laptop GPU (Direct3D12), 1920x1080 Windowed, quality High, LOD bias 1, vsync 1
[TestRange] <29 variant lines, E01 ... Z13>
[TestRange] 731 prefabs placed in 8 groups, 29 variants, 0 missing meshes, materials, textures or unsupported shaders
$ 450fpv-test-range.exe -smoketest -windowed -logFile <scratch>/qa_smoke_2.log      (warm)
AC True; exit=0 wall=6.16 s
[TestRange] TestRange loaded, first frame 3.17 s after start: ... (as above)
[TestRange] 731 prefabs placed in 8 groups, 29 variants, 0 missing meshes, materials, textures or unsupported shaders
$ python -m pytest tools -q -p no:cacheprovider -rfE
1452 passed in 1387.44s (0:23:07)                                    exit 0
  (the first pass's 1451 plus the known-wrong belt test; tools/map alone: 8 passed)
$ python tools/hygiene/precommit.py --tree HEAD
2158 files scanned, 0 findings                                       exit 0
$ openspec validate p6-test-range --strict
Change 'p6-test-range' is valid                                      exit 0
$ openspec validate p1-g1-look-lock-gallery --strict
Change 'p1-g1-look-lock-gallery' is valid                            exit 0
$ openspec list
p6-test-range 3/4 tasks (2.1, this review) | p1-g1-look-lock-gallery ✓ Complete
```

**The first pytest run exited 1 and its failure is unknown.** It ran alongside QA's Unity builds and other agents' jobs. While it ran, another process cleared the session's shared scratchpad, which deleted its log. The re-run on the same tree passed 1452 of 1452, and `tools/map`, `tools/hygiene`, `tools/reference` and `tools/assets` passed alone. That leaves `tools/blender`. Its `run_build` gives Blender 300 s (`test_build_blender.py`), the one deadline QA found in the tests. That suggests a test bound by machine load, but it is not proven. This change touches no file under `tools/blender`. If it recurs, keep the log.

**Requirements:** WLD-04 (belts) **met**:
- planted width 12.7 m and length 299.2 m;
- trunks 1.501–2.993 m apart along each row (median 2.31 m), and the nearest-neighbour median 1.99 m;
- bushes among and along the trees, and a 12 m gap.

PLT-02, PLT-06 and PLT-07 are as the first pass measured them: no runtime file changed. The smoke run's first frame came at 3.17–3.32 s on the player's own clock.

**Hygiene:**
- The five commits are under World Builder's identity, subject only, 42–68 characters, no body and no `Co-Authored-By`.
- Nothing under `reference/`, `Library/`, `Temp/`, `Logs/` or `Builds/` in the range.
- No new third-party file.
- The OPSEC scan finds 0 findings.

## New findings (non-blocking)
- **N10. The trunk nearest-neighbour median barely bounds the spacing along a row.**
  - **The hole:** rows 3 m apart leave every trunk a neighbour within about 3 m, so the 1–3 m median can hardly exceed 3 m.
  - **The proof:** a belt thinned to every third trunk passes at 2.90 m. Its along-row steps then have a median of 5.32 m and reach 9.96 m, so WLD-04's "1–3 m apart" fails.
  - **The map is fine:** the generator draws `trunk_step_m` 1.5–3 m, and the steps measured 1.501–2.993 m.
  - **Suggested** (world-builder): `check_trunks` also asks the median step along each row within 1–3 m, skipping the gap, and a known-wrong test fails the sparse belt.
- **N11. `docs/gallery-g1.md` still describes six stations**, with "3 H01: the adobe house, intact and damaged side by side". No gallery build goes to the pilot (D-046). **Suggested:** one line saying station 3 is an empty marker and H01 stands in the test range.
- **N12. `godot_port.py`'s docstring is stale** (lines 12–15, and `lod.cull_m`'s effect text). It still gives "the design's rule" as the cull at Godot's last `lod_switch_m`; design 2 now says 600 m. The code is correct; only the comment is stale.
- **Role scope (as N8):** the World Builder committed the spec amendments. `tasks.md` records B1 (b) as the orchestrator's call, so this is a note, not a fix.
