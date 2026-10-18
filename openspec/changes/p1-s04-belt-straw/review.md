# Review: p1-s04-belt-straw (task 2.1)

**Reviewer:** qa-engineer · **Branch:** `unity/art/p1-s04-belt-straw` · **Tip:** `81d54e2` · **Range:** `b16fd15..81d54e2` (26 commits, 90 files, merge base with `unity/main`)
**Requirements cited:** AST-02, AST-03, AST-05, AST-07, AST-08, AST-10, AST-11, AST-12, REAL-01, REAL-03, REAL-05, SEC-04 · **Decisions:** D-003, D-004, D-020, D-028, D-032
**Sources read:** `proposal.md`, `design.md` (decisions 1–8, 9 as built, 10 as tuned), `tasks.md`, the three spec deltas, §S04 of `docs/object-sheets/earthworks-and-surfaces.md` and T4 of `docs/reference-notes/terrain.md` (both from `origin/unity/main`; the branch predates their merge), `assets-src/physics_materials.json`, and the full diff of `tools/assets/{fetch,derive}.py`, `tools/blender/{build.py,lib/straw.py,lib/audit.py,lib/meta.py,generators/ground_patch.py}`, the recipes and params, `CatalogImporter.cs`, `ModelPostprocessor.cs`, `ReviewCapture.cs`, `AssetPhysicsMeta.cs` and every new or changed test.

Every check below was run by QA in its own fresh worktree: no `unity/Library/` before the first Unity run, `git lfs checkout` first, `git lfs ls-files` showing `*` for all 22 LFS files. Nothing from the handoffs was reused: the colour targets, the no-tiling statistic, the derived maps, the union audit and the K01 tiles were re-measured with QA's own scripts in QA's scratchpad (deleted at the end). Unity 6000.3.26f1 with Unity Hub running and no editor on the project; Blender 5.2.2 LTS; Python 3.14.6 with Pillow 12.3.0; pwsh 7.6.7. Every Unity call was piped, so the exit codes are real. This review makes no performance claim (the laptop read `BatteryStatus 1`, Balanced, during the session; no frame time is quoted anywhere below).

## Full checks

```
$ python -m pytest tools -q
  1 failed, 576 passed in 370.57s (0:06:10)
  FAILED tools/hygiene/tests/test_precommit.py::test_200_files_scan_in_under_2_s
         200 staged files: scan 1.497, 2.114, 1.464 s   (assert max(times) < 2.0)
  -> run while QA's autocorrelation script, a derive run and a cold Unity import loaded the CPU; re-run alone:
$ python -m pytest tools/hygiene/tests/test_precommit.py::test_200_files_scan_in_under_2_s -q
  200 staged files: scan 1.237, 1.895, 1.232 s; full commit through both hooks 5.438 s
  1 passed in 16.71s
  (the hygiene scanner is untouched by this change; the timing test is load-sensitive, see Findings F6)

$ python tools/assets/fetch.py --verify           -> OK   exit 0
$ python tools/assets/derive.py --all
  unity/Assets/_Project/Art/Materials/belt_soil_mask.png: up to date
  unity/Assets/_Project/Art/Materials/belt_straw_albedo.png: up to date
  unity/Assets/_Project/Art/Materials/brick_red_mask.png: up to date
  unity/Assets/_Project/Art/Materials/straw_albedo.png: up to date
  derive.py: OK                                   exit 0
  belt_straw.recipe.json "measured": mean linear RGB 0.1946/0.1619/0.0908, mean luminance 0.1637 vs target 0.1638
  (-0.05 %), bare 0.0783, green 0.1554, gaps 0.2175, soil mean #433f37, soil gain 0.723/0.816/0.948,
  straw gain 0.472/0.602/1.056, final gain 1.051/1.03/1.001, detail gain 1.374 (grey); maps 2048x2048, detail 2048x2048
$ python tools/hygiene/precommit.py --tree HEAD   -> 491 files scanned, 0 findings   exit 0
$ openspec validate p1-s04-belt-straw --strict    -> Change 'p1-s04-belt-straw' is valid   exit 0

$ & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/editmode.xml ... | Out-Host
  exit 0 (cold Library); unity/Logs/editmode.xml: result=Passed total=24 passed=24 failed=0 skipped=0
  Passed Fpv450.Sim.Tests.DetMathTests.Sqrt_IsCorrectlyRounded (x3)
  Passed Fpv450.Tests.EditMode.CatalogImporterTests.{BoxCollider_MatchesTheMetadataBox, LodGroup_HasTheTwoLodMeshes,
         Material_PacksTheDerivedMaskIntoMetallicGlossAndOcclusion, Material_UsesThePinnedBrickDiffuse, PhysicsMeta_CarriesMasonry}
  Passed Fpv450.Tests.EditMode.CylinderColliderTests.{Cylinder_BecomesAConvexMeshColliderWithinFiveMillimetres,
         Cylinder_SecondBuildWithTheSameInputsChangesNoFile}
  Passed Fpv450.Tests.EditMode.GroundMaterialTests.DerivedGroundMaps_ImportByTheirRole                       (new)
  Passed Fpv450.Tests.EditMode.GroundMaterialTests.GroundMaterial_CarriesTheDetailLayerAtTheTileSize           (new)
  Passed Fpv450.Tests.EditMode.GroundMaterialTests.GroundMaterial_UsesTheDerivedMapsAsItsBase                   (new)
  Passed Fpv450.Tests.EditMode.GroundPatchPrefabTests.Lod0_RendersTheSlabAndTheMicroMesh_AndTheOtherLodsOnlyTheSlab (new)
  Passed Fpv450.Tests.EditMode.GroundPatchPrefabTests.LodGroup_SwitchesAtTheMetadataDistances                  (new)
  Passed Fpv450.Tests.EditMode.GroundPatchPrefabTests.MicroMesh_HasNoCollider_AndTheMetaListsIt                (new)
  Passed Fpv450.Tests.EditMode.NoclipCameraTests.Step_DistanceIsFrameRateIndependent
  Passed Fpv450.Tests.EditMode.ReviewCaptureTests.PilotPose_K01_IsThePoseFromBeforeTheSurfaceFraming            (new)
  Passed Fpv450.Tests.EditMode.ReviewCaptureTests.PilotPose_Surface_StandsOnThePatch_LooksDown35DegreesTowardsPlusX_WithTheBarInFrame (new)
  Passed Fpv450.Tests.EditMode.ReviewCaptureTests.{RequireRenderable_NamesTheMissingMeshAndTexture,
         RequireRenderable_PassesAMeshWithATexturedMaterial, RequireRenderable_RefusesATextureThatIsNotAnAsset}
  Passed Fpv450.Tests.EditMode.XSignProbeTests.{ImportedBounds_RunFromTheOriginToThePositiveSize, Triangles_WindOutward}
  (the log's only "Exception" lines are the warning stack for the empty Fpv450.Feed assembly, as before)
$ git status --short                              -> (clean)

$ pwsh tools/build_asset.ps1 S04                  (first run; exit 0, 116 s)
build_asset: [1/4] Blender build of S04
S04_patch: audit: skipped micro mesh S04_patch_LOD0_straw (12144 straw)
S04_patch: audit: surface shapes collision[0], collision[1], collision[2], collision[3] are landed on over their whole top, held to 50 mm there
S04_patch: audit collision[0] box: box->mesh 39.908 mm at (4, 0, 4.5), tolerance 50 mm
S04_patch: audit collision[1] sphere: sphere->mesh 35.225 mm at (4.7936, 0, 0.2957), tolerance 50 mm
S04_patch: audit collision[2] sphere: sphere->mesh 33.798 mm at (0.3955, 0, 0.3532), tolerance 50 mm
S04_patch: audit collision[3] sphere: sphere->mesh 35.237 mm at (-1.0784, 0, 1.454), tolerance 50 mm
S04_patch: audit mesh: mesh->collision 40.000 mm at (4, -0.04, 4.5) nearest collision[0], tolerance 50 mm
S04_patch: unity\Assets\_Project\Art\Models\surfaces\S04\S04_patch.fbx  size [12.0, 1.342, 12.0] m  tris 6014 / 6014 / 26  micro S04_patch_LOD0_straw 12144 straw 294732 tris  fbx 10.2 MB  metadata written
build.py: OK
build_asset: [2/4] Derived textures of every material recipe       (all four "up to date")
build_asset: [3/4] Unity import of S04
[CatalogImporter] S04_patch: Assets/_Project/Prefabs/surfaces/S04_patch.prefab, 3 LODs, S04_patch_LOD0_straw (12144 straw, LOD0) micro, 4 colliders, materials ground -> belt_straw, soil -> belt_soil, straw -> straw, LOD heights 1.0000 / 0.1333 / 0.0200 (size 9.237604 m, fov 60 deg)
build_asset: [4/4] Review sheet of S04
[Review] S04: 1 variant(s), lights day/overcast, GPU NVIDIA GeForce RTX 4050 Laptop GPU (Direct3D12), quality High (URP-High), LOD BIAS 1, colour space Linear
[Review] surface pilot pose: camera (0.000, 1.700, 0.000), 1.500 m above the patch centre (0.000, 0.200, 0.000), forward (0.8192, -0.5736, 0.0000), 35.00° down towards +X; bar base (2.500, 0.200, 0.000), 2.50 m ahead, in frame True
[Review] warm-up: one discarded render of S04_patch before its first tile          (once for the stage; none before overcast)
[Review] tile 1.5 M PILOT HEIGHT ON SURFACE 35° DOWN  BAR LOWER BANDS: camera (0.00, 1.70, 0.00), 1.000 m to the look-at point, lower-left patch 120/108/89
[Review] tile 10 M SLANT 20° DOWN AZ 225: camera (-6.64, 4.09, -6.64), 10.000 m to the look-at point, lower-left patch 123/110/90
[Review] S04_patch day: review-sheets/S04/S04_patch_day.png (4096x4096, 6.2 MB)
[Review] S04_patch overcast: review-sheets/S04/S04_patch_overcast.png (4096x4096, 6.0 MB)
build_asset: OK (S04)
$ git status --short                              -> (clean)

$ pwsh tools/build_asset.ps1 S04                  (second run; exit 0, 80 s; the same audit lines, counts and pose)
$ git status --short                              -> (clean)
  S04_patch.fbx sha256 5ebea84d… (= the LFS oid) and S04_patch.prefab 9fcee9ce… both runs; both review sheets byte-identical
  between the runs (cmp)

$ pwsh tools/build_asset.ps1 K01                  (exit 0)
K01_whole: audit collision[0] box: box->mesh 3.288 mm at (-0.125, 0, 0.06), tolerance 50 mm
K01_whole: audit mesh: mesh->collision 1.869 mm at (-0.1231, 0.0019, 0.058) nearest collision[0], tolerance 50 mm
K01_whole: unity\Assets\_Project\Art\Models\debris\K01\K01_whole.fbx  size [0.25, 0.065, 0.12] m  tris 44 / 12  fbx 0.0 MB  metadata written
[CatalogImporter] K01_whole: Assets/_Project/Prefabs/debris/K01_whole.prefab, 2 LODs, 1 colliders, materials brick -> brick_red, LOD heights 0.0722 / 0.0018 (size 0.25 m, fov 60 deg)
[Review] warm-up: one discarded render of K01_whole before its first tile
build_asset: OK (K01)
$ git status --short                              -> (clean)
$ git diff --stat b16fd15 -- unity/Assets/_Project/Art/Models/debris/K01/ unity/Assets/_Project/Prefabs/debris/
  K01_whole.assetmeta.json | 1 +    ("micro": [])
  K01_whole.prefab         | 1 +    (micro: [])
$ git rev-parse b16fd15:…/K01_whole.fbx HEAD:…/K01_whole.fbx  -> fa32a3c0… fa32a3c0…   (same blob; content sha256 5bf2b054… both)
```

## Spec scenarios

| Capability / scenario | Result | Evidence |
|---|---|---|
| assets-earthworks-and-surfaces · S04 · **Built from params by one command** | **PASS** | Two runs of `build_asset.ps1 S04`, exit 0, `S04_patch.fbx`, `.assetmeta.json`, prefab and both sheets written; `git status` clean after each; FBX, prefab and sheets byte-identical between runs. |
| … · **Straws read as geometry at the pilot's height** | **FAIL** (the by-eye clause; the geometry clause passes) | Geometry: **PASS.** LOD0 carries one micro mesh `S04_patch_LOD0_straw`, 12 144 instances, 294 732 triangles, rendered with LOD0 (EditMode test, and seen in both 1.5 m tiles as separate tapered straws, stems with seed heads and a few twigs); the count is 312 /m² × 38.6 m², the smoothstep-feathered disc of radius 4 m (QA's integral: 9π + 2π·1.65 = 38.64 m² → 12 056 expected, 12 144 built, +0.7 %). By eye against H 1–21 and G 1–12 (QA read `reference/_compare/S04.png` and both contact sheets): **colour family matches; straw density and loft do not; bare patches and green strips only partly** (see REAL-03 below). R4, R5. |
| … · **No visible tiling at 10 m** | **PASS** | By eye on QA's full-resolution crops of the 10 m and 50 m tiles, day and overcast: no repeat of the straw or soil set, fine straw grain at 10 m. Measured with QA's own script (luminance high-passed at 0.5 m, normalised autocorrelation over every shift of 0.4–4 m): the derived albedo peaks at **+0.16 at 3.25 m** beyond 1 m (256 px over 12 m), the φ-scaled cover's own period 2 × 1.618 m, against **+1.00 at 2.0 m** for the same cover tiled plainly at 2 m (the known-wrong model fails the statistic). With URP's ×2 detail layer applied in linear at 4 cm texels (about the mip seen at 10 m): +0.16 at 3.22 m, no peak at the detail's 2 m period. |
| … · **Collision follows the mat** | **PASS** | The audit lines above: micro mesh skipped and named with its count; every shape within 50 mm (box 39.9, spheres 35.2 / 33.8 / 35.2, mesh 40.0); the surface-top line printed; 3 mounds as `surface:crater_spoil` spheres and the 12 × 0.2 × 12 m box as `surface:belt_straw`, all `landable: true`; `gaps`, `wind_volumes`, `snag` empty (the straw hooks a stated deviation to AST-10). |
| … · **Colour inside the sheet's range** | **PASS** | QA's measurement of `belt_straw_albedo.png`: linear mean 0.1946/0.1619/0.0908, luminance 0.1637 against #7a7055's 0.1638 (**−0.05 %**, limit ±10 %). Soil target #433f37: luminance 0.0502 inside 10YR 2/1 (#33302b, 0.0299) – 3/1 (#4a463f, 0.0620), and inside both per channel. Straw: the palette's three shades #cdb685 / #b49c6a / #977f55 average #b49d6e in linear light, **+1.45 %** luminance against the sheet's #b49c6a. The day and overcast tiles sit beside H 16 / H 12 and G 5 in `S04.png`. |
| asset-pipeline · Ground recipes · **Ground recipe derived and wired** | **PASS** | `belt_straw_{albedo,normal,mask,detail_albedo}.png` 2048² with `belt_straw.recipe.json` beside them; QA re-derived every recipe into an empty scratch root from the pinned sets: all ten outputs and stamps **byte-identical** to the committed ones. Importer wiring by the three `GroundMaterialTests` (base 1/12 with offset 0.5, detail ×6 = every 2 m, `_DETAIL_MULX2`, detail normal the set's own, albedos sRGB, normal and mask linear). The detail albedo is the cover's diffuse made grey (design 2, as built); read as satisfying "albedo of the cover set". |
| … · **Mean albedo hits the target** | **PASS** | −0.05 %; bare 7.83 % (8 % ± 2 pp), green 15.54 % (15 % ± 2 pp). The same recipe at seed 5 in QA's scratch root: −0.0 %, bare 7.5 %, green 15.1 %. |
| asset-pipeline · Micro meshes · **Micro meshes skipped by the audit** | **PASS** | S04's first audit line names `S04_patch_LOD0_straw (12144 straw)`; the box passes against the slab alone; LOD0 renders both meshes (`Lod0_RendersTheSlabAndTheMicroMesh…`); `test_micro_mesh_far_from_any_primitive_is_skipped_and_named` passes. |
| … · **Micro meshes carry no collision** | **PASS** | `MicroMesh_HasNoCollider_AndTheMetaListsIt`: no collider under the micro child, 4 colliders = 4 primitives, `micro[0]` = {lod 0, `S04_patch_LOD0_straw`, straw, 12 144}. |
| asset-pipeline · Flat palette recipes · **Straw palette** | **FAIL as written** | The behaviour passes (`straw_albedo.png` 64 × 16, one block per colour; bound as `_BaseMap`; the capture's empty-stage guard passed), but the scenario says the map has "those two columns" and the build writes four (`straw_pale`, `straw`, `straw_dark`, `twig`) since task 1.3. The delta no longer describes what is built. R2. |
| asset-pipeline · Union audit · **Mound spheres sunk in the mat box** | **PASS** | S04: three `crater_spoil` spheres sunk in the `belt_straw` box over ±4 cm relief pass at ≤ 35.2 mm with the 50 mm line printed. `test_primitives_form_a_union_and_surface_tops_are_held_to_5cm` passes. QA's mutation: a 3 cm cap sunk in a 2 m slab passes with the union (sphere 30.000 mm, box 0.000 mm); with `_covered` and `_cell_covered` stubbed to "never covered" the same case fails on the buried half (`sphere->mesh 99.977 mm`), so the test exercises the union code. |
| asset-pipeline · Pinnable sources · **A set from a new source** | **PASS** | `test_add_texturecan_reads_the_page_and_pins_the_zip` (local server) passes; `--verify` OK over the pinned zip hash (`bef43ce2…`, 63 105 063 bytes) and the four extracted maps. Live: the details page still links the pinned `/downloads/ground_0018/ground_0018_2k_krQ65t.zip`, and the terms page puts every texture under CC0 1.0. QA did not re-download the 63 MB zip (a download needs the user's go-ahead); task 1.1's two identical downloads stand. |
| … · **A source that cannot be pinned** | not testable | No such source arose: TextureCan was pinnable, so no substitute was needed and the sheet's material line stays. |
| asset-review-sheet · **Pilot-height tile of a ground patch** | **PASS** | Log: camera 1.500 m above the patch centre, forward (0.8192, −0.5736, 0) = 35.00° down towards +X, bar base 2.50 m ahead, in frame; `PilotPose_Surface_…` passes; both 1.5 m tiles show the mat from under the camera to the far edge with the bar's lower bands in frame. |
| … · **Other classes unchanged** | **PASS** | QA captured K01 with the branch code and with the merge base's `ReviewCapture.cs` (checked out from `b16fd15` with its test file, restored after): the overcast sheet is **byte-identical**; the day sheet differs only inside the session's first tile (1.5 m day, box (434, 434)–(589, 590), max 36 levels), where the merge-base render is the cold first render the warm-up fixes: the brick's normal-map relief is missing in it and present in the branch tile (QA's crops side by side). Every other tile is identical. `PilotPose_K01_IsThePoseFromBeforeTheSurfaceFraming` passes. |

## Task-level and brief checks

- **Ground recipe regenerates on a recipe change (pipeline delta):** in QA's scratch root an unchanged second run prints `up to date`; seed 4 → 5 prints `derived …` and changes the albedo (`3acd4552…` → `86fc2d47…`).
- **But not on a formula change (R1).** The stamp hashes `GROUND_FORMULA` (a text naming `STRAW_M`, `LOBE_M` and `GAP_LOBES` by name and the feathers by the literal "0.3 m"/"0.5 m"), the recipe, the sources and the Pillow version; the module constants' values are not in it. QA's experiment in the scratch root, `derive.STRAW_M` set from 0.05 to 0.10 and `derive.main(["--root", <scratch>, "belt_straw"])`:
  ```
  STRAW_M = 0.1
  unity/Assets/_Project/Art/Materials/belt_straw_albedo.png: up to date      <- stale
  derive.py: OK
  after removing the stamp: derived 2048x2048 ground …                       -> albedo f34758fc… (committed 3acd4552…)
  ```
  So tuning any of `STRAW_M`, `LOBE_M`, `BARE_FEATHER_M`, `GREEN_FEATHER_M`, `GAP_LOBES`, `SHARE_TOLERANCE` (the numbers G1 tuning will touch) leaves the committed maps stale while `derive.py --all` and `build_asset.ps1` report success. The module docstring promises "a changed recipe, source or formula regenerates". The committed maps are consistent with the code today (the byte-identical re-derivation above).
- **The micro mesh's density and feather (1.2, 1.3):** 12 144 instances, 312 /m² over the 38.6 m² feathered disc (above); 294 732 triangles + slab 6 014; FBX 10.2 MB. The feather shows in the 1.5 m tiles as straws thinning out between about 3 and 4 m, no rim.
- **AST-03 acceptance (one parameter, rebuild, the change in Unity):** QA set `mounds_per_m2` 0.02 → 0.04 in `assets-src/surfaces/S04_patch.json` and ran `pwsh tools/build_asset.ps1 S04` (exit 0): six mound spheres, all within 50 mm (the new three at 29.9 / 29.2 / 31.7 mm), LOD0 6 974 triangles, 12 138 straws, `[CatalogImporter] … 7 colliders`, the prefab with 6 `SphereCollider`s (HEAD: 3), the review sheet re-rendered; no hand step. Params restored with `git checkout HEAD -- assets-src/surfaces/S04_patch.json` and rebuilt: `git status` clean again, the FBX, metadata and prefab back to HEAD's bytes.
- **Assumed numbers of task 1.3** (design §10), each judged:

| Number | Where | Label in design §10 | Range | QA's judgement |
|---|---|---|---|---|
| `stems_per_tuft` 10 | params | assumed | PARAMS 1–50 | plausible for H 3–9 tussocks |
| tuft radius 0.15 m | `ground_patch.TUFT_R_M` (constant, not a param) | assumed | none | 0.3 m tussocks, inside the code's own H 3–9 note (0.2–0.4 m) |
| `stem_tilt_deg` 60 | params | assumed (H 3–9) | PARAMS 0–80 | leans 15–60°; plausible |
| `gap_share` 0.25 | recipe | **no label** | derive 0–0.5 | plausible; measured 21.7 % outside bare; label missing (R3) |
| bare feather 0.3 m | `derive.BARE_FEATHER_M` | **no label** (code: "measured by eye", H 9–14) | none | plausible; label missing (R3) |
| lobe noise 0.4 m | `derive.LOBE_M` | **no label** (code: assumed) | none | plausible; label missing (R3) |
| high-pass 5 cm | `derive.STRAW_M` | **no label** | none | a processing constant tied to straw width; label missing (R3) |
| two extra straw shades #cdb685, #977f55 | `straw.json` | assumed | — | mean within +1.45 % of #b49c6a; fine |

  Also unlabelled in §10: the soil target #433f37 (inside the sheet's 10YR range, chosen against H 12 / H 14), the olive #7f8a4e (between the sheet's assumed #6a7a3a and H 16's feed #959763), `bake_feather_m` 1.0, `seed_head_share` 0.35 and `variants` 12 (inside the sheet's ranges, tuned by eye). R3.
- **Budgets (AST-08):** every texture 2K at most (derived maps 2048², palette 64 × 16, third-party 2K); 3 LODs (6 014 + 294 732 micro / 6 014 / 26 triangles) switching at 8 / 60 / 400 m (EditMode test); FBX 10.2 MB. No per-class triangle budget exists yet in the phase-1 brief to check LOD0 against (design 4's own estimate was about 400 k; built 300 k).
- **Statistics:** the no-tiling statistic fails the known-wrong model (plain 2 m tiling +1.00); the shares hold at another seed (7.5 / 15.1 %).
- **Test-collection clash (task 1.3's report):** `python -m pytest tools/assets tools/blender -q --collect-only` fails on this branch (3 errors: `test_derive.py`, `test_fetch.py`, `test_ground.py` import `conftest` and get `tools/blender/tests/conftest.py`), and fails the same way on `origin/unity/main` and at `b16fd15` (2 errors, the two older files; QA ran it on `git archive` copies). Pre-existing, not introduced here; this change adds one more file with the same `from conftest import …` pattern. `python -m pytest tools` collects and runs everything. F5.

## REAL-03 side-by-side and REAL-05 geometry audit

QA read `reference/_compare/S04.png` (task 1.3's sheet: the 1.5 m and 10 m tiles beside H 2, 5, 8, 12, 16, 20 and G 5), the H contact sheet (stills 1–21) and the G contact sheet (stills 1–12), and QA's own full-resolution crops of this build's 1.5 m, 10 m and 50 m tiles, day and overcast. Nothing was copied out of `reference/`.

**Matches**
- The parts: a straw mat over dark soil with bare soil patches, short green strips, small earth mounds, standing stems with seed heads; at the pilot's height the straws are separate meshes, never a texture.
- The colour family: a khaki-straw mat at the sheet's carried mean (the same value as H 16's feed sample), darker grey soil, a pale-to-dark spread of straw shades as in H 12–21.
- No tiling at 10 m and 50 m; fine straw grain at 10 m.
- Bare soil at 1.5 m with straws strewn over its edge (H 9–14).

**Does not match** (the first five are task 1.3's list, confirmed; the rest are QA's)
1. The feed's saturation and contrast (H's straw reads gold against dark gaps): the feed stage's work; the review renders have no feed.
2. H's near straws lie in locally parallel swathes; ours cross at random with the global 0.3 bias.
3. The review's 35° look-down from 1.5 m against H's low forward view (about 0.3–0.5 m, near level): not a like-for-like frame for density.
4. G's low warm sun is not a review light.
5. A light sheen on the base under overcast.
6. **Loft.** H's mat is a deep heaped tangle (the sheet's 5–20 cm), piled into ridges, with no soil showing outside the bare patches; ours is one sparse layer of straws (250 /m², the sheet's physics density) on a flat slab with ±4 cm relief, and the base shows between the straws everywhere. At 10 m (LOD1, no straws) the mat is a flat textured carpet, where G 1–12 at 10–13 m shows lumpy tussock relief with shadow.
7. **The straws themselves** are straight-ish thin tubes of one flat shade each; H's straws are bent, bundled and shaded along their length. The tussocks of stems barely read from the 35° look-down.
8. **Bare patches read as stains.** They are a colour change on the flat mat top with soft edges, no step down through the mat; at 10 m and in the turntable they read as dark ink blots on a carpet. In H 9–14 a bare patch is soil lying below the mat's edge.
9. **Green strips** read as a green tint over the straw texture, without the short blades of H 16 and H 19–21.
10. Beyond the 4 m bake the 1.5 m tile shows only the base texture, which is most of its upper half (the camera's frame reaches about 17 m); a stated G1 stand-in until AST-10.

**REAL-05 list of physical features** (none may be texture-only)

| Feature | Physical role | As built | Status |
|---|---|---|---|
| mat top and relief (±4 cm) | landing surface | slab geometry; box `surface:belt_straw`, landable, within 40 mm | geometry |
| earth mounds (3) | uneven support | raised cells with the soil slot; one `surface:crater_spoil` sphere each, within 35 mm | geometry |
| lying straws, standing stems, twigs | hooks on legs and fiber | meshes (visual only), no colliders; hooks are AST-10's (stated G1 deviation) | geometry, physics deferred (stated) |
| bare soil patches (8 %) | no mat: legs on soil, below the mat top; "soft when moist" (T4) | **albedo, normal and mask only**; the belt_straw box and mat physics over them | **texture-only, not stated** (R4) |
| green regrowth strips | none beyond the mat's | texture | acceptable |
| holes under the mat | pitfalls | the surface row's (`0.02 /m²`), not the asset's | acceptable |

## Hygiene

```
$ git log --format='%h %an <%ae> [%s] body=[%b]' b16fd15..HEAD      (26 commits, every body empty, no trailers)
81d54e2 Asset Artist <assets@450fpv.local> [docs(openspec): tick s04 task 1.3] body=[]
7be7818 Asset Artist <assets@450fpv.local> [docs(openspec): record s04 task 1.3 as tuned] body=[]
57f7fa8 Asset Artist <assets@450fpv.local> [art(surface): tune s04 against h and g] body=[]
8eb69e0 Asset Artist <assets@450fpv.local> [art(surface): shade the straw kit pale, mid and dark by variant] body=[]
df548e1 Asset Artist <assets@450fpv.local> [feat(blender): feather the s04 bake, bare mounds and stem tussocks] body=[]
7850c6a Asset Artist <assets@450fpv.local> [feat(assets): straw gaps, soil target and grey detail in ground recipes] body=[]
08a84cc Tech Artist <techart@450fpv.local> [docs(openspec): tick s04 task 1.4] body=[]
2444dd0 Tech Artist <techart@450fpv.local> [feat(review): shoot surface assets from on the surface] body=[]
76f26c9 Tech Artist <techart@450fpv.local> [fix(review): warm up the renderer before each stage's first tile] body=[]
928153f Orchestrator <orchestrator@450fpv.local> [docs(openspec): record s04 task 1.2 as built] body=[]
09a862f Asset Artist <assets@450fpv.local> [docs(openspec): tick s04 task 1.2] body=[]
154c020 Asset Artist <assets@450fpv.local> [art(surface): build s04 on the design's 0.25 m grid] body=[]
ff58eaa Asset Artist <assets@450fpv.local> [test(unity): give the cylinder probe metadata its empty micro list] body=[]
5a28eb2 Asset Artist <assets@450fpv.local> [art(debris): rebuild k01 with its empty micro list] body=[]
77c355e Asset Artist <assets@450fpv.local> [art(surface): build the s04 belt straw patch and its prefab] body=[]
9204cbe Asset Artist <assets@450fpv.local> [fix(unity): keep lod switch distances of assets taller than the screen] body=[]
6a42b79 Asset Artist <assets@450fpv.local> [feat(unity): import micro meshes with their lod and palette materials] body=[]
d17e2c6 Asset Artist <assets@450fpv.local> [art(surface): add the ground_patch generator and s04] body=[]
0718f89 Asset Artist <assets@450fpv.local> [feat(assets): micro-detail meshes and a union audit in the pipeline] body=[]
58fe5a4 Asset Artist <assets@450fpv.local> [art(surface): add the straw instance kit] body=[]
21ae564 Asset Artist <assets@450fpv.local> [feat(assets): add palette recipes for flat tinted materials] body=[]
f4b844b Asset Artist <assets@450fpv.local> [docs(openspec): record s04 task 1.1 as built and tick it] body=[]
fa8808f Asset Artist <assets@450fpv.local> [feat(unity): wire ground recipes with a detail layer in the importer] body=[]
615460e Asset Artist <assets@450fpv.local> [feat(assets): derive ground recipes with a detail layer] body=[]
f8de5b2 Asset Artist <assets@450fpv.local> [art(surface): pin the belt_straw soil and straw sets at 2k] body=[]
334532a Asset Artist <assets@450fpv.local> [feat(assets): add the texturecan source to fetch.py] body=[]
```
- **Identities** as `docs/team.md`: Asset Artist (22), Tech Artist (3), Orchestrator (1); author = committer on every commit. Longest subject 71 characters; every subject matches `type(scope): lower-case summary`.
- **Nothing ignored is tracked:** no path under `reference/`, `unity/{Library,Temp,Logs,UserSettings}/` or `review-sheets/`, and no video file, in `git ls-files` (491 files).
- **LFS:** all 22 LFS files real (`*`); the new derived maps (6.5–10 MB), the FBX (11 MB) and the third-party maps go through LFS.
- **`.meta` files:** every new file and folder under `unity/Assets/` in the range has its `.meta`, and no `.meta` is orphaned (QA's pairing check over `git diff --name-only` against `git ls-files`).
- **Licences (D-003, SEC-04):** `polyhaven/brown_mud_02` (2k, 4 maps, CC0, Rob Tuytel) and `texturecan/ground_0018` (2k, the zip pinned by SHA-256 and byte count plus its 4 maps, CC0, TextureCan) in `manifest.json`; `CREDITS.md` regenerated with both rows; `--verify` OK; TextureCan's terms page read by QA: CC0 1.0.
- **OPSEC:** the scan finds nothing; the change's text cites letters and still numbers only.

## Requirements

| ID | Status | Why |
|---|---|---|
| AST-02 | met | §S04 of the object sheet drives every number; the params sit in its ranges (densities 250 / 60 / 2 per m², lengths, diameters, shares, colours). |
| AST-03 | met | One command builds S04 end to end, twice with no diff; QA's one-parameter change (mounds) rebuilt into Unity with no hand step, and the restore rebuilt HEAD's bytes. |
| AST-05 | partly met | Primitives within 50 mm (box 39.9, spheres ≤ 35.2, mesh 40.0), a material per shape, landable flags, LOD distances; the ±2 cm landing-face rule is widened to 5 cm for `surface:` shapes by this change's own delta (accepted: a compliant mat over ±4 cm relief, recorded in the audit's line and in the delta that will land in `openspec/specs`); straw and twig snags deferred to AST-10 (stated); bare patches carry no shape or surface of their own (R4). |
| AST-07 | met | No visible tiling at 10 m (eye and statistic); UVs in metres in asset space; near texel density from the detail layer at 1 024 px/m (base 171 px/m, the straws there being geometry). |
| AST-08 | partly met | 2K textures and 3 LODs met; no per-class triangle budget exists yet to test LOD0's 300 k against. |
| AST-10 | not in scope | The bake is a stated G1 stand-in; no near-ring fade or far density layer (non-goals). |
| AST-11 | met for belt_straw | `belt_straw` visual material paired with `surface:belt_straw`; feathering into neighbours is phase 3. |
| AST-12 | met | 1.5 / 10 / 50 / 200 m, turntable and overlay, day and overcast; surface framing; side-by-side exists locally. |
| REAL-01 | partly met | Judged against H and G first; the parts and colour family match; loft, straw form, bare-patch depth and strips do not yet (REAL-03 list). |
| REAL-03 | partly met | The sheet exists and was read; differences 6–10 are neither fixed nor explained in the change (R5). |
| REAL-05 | partly met | Straws, stems, twigs and mounds are geometry; collision follows the mat; the bare patches are texture-only without a stated deviation (R4). |
| SEC-04 | met | Both sets pinned by URL and SHA-256 (zip and maps), CC0, credited; `--verify` OK. |

## Findings

- **F1 (R1).** The ground stamp ignores the formula constants: changing `STRAW_M` (or `LOBE_M`, `BARE_FEATHER_M`, `GREEN_FEATHER_M`, `GAP_LOBES`, `SHARE_TOLERANCE`) leaves the committed maps stale while every command reports success (experiment above).
- **F2 (R2).** The "Straw palette" scenario describes two columns; the built palette has four.
- **F3 (R3).** Nine numbers task 1.3 added or tuned carry no pilot / measured / published / derived / assumed label in design §10 (table above), against `docs/workflow.md` step 2.
- **F4 (R4).** Bare soil patches have a physical role (legs on soil below the mat, T4's "soft when moist") and are texture-only: no step through the mat, mat physics over them. Nothing in the design states it as a deviation the way the straw hooks are.
- **F5 (pre-existing).** `pytest tools/assets tools/blender` fails to collect on `unity/main` too: both test folders import a module named `conftest`.
- **F6 (pre-existing).** `test_200_files_scan_in_under_2_s` failed once under CPU load (2.114 s) and passed alone at 1.895 s, close to its limit; a wall-clock limit of 2 s is load-sensitive.
- **F7.** Differences 6–10 of the side-by-side are recorded nowhere in the change (R5).

## Suggestions (non-blocking)

1. **A low forward review view for surfaces** (camera about 0.4 m up, 5–10° down, as H's frames are), beside the 35° look-down, so density and loft compare like for like at G1 (REAL-02's view for H).
2. **Visual density against physics density:** 250 straws /m² in one layer reads sparse beside H's 5–20 cm mat; ask the reference analyst for the visual layering (straws per m² through the mat depth) and let AST-10's generator draw the deep layers visually while simulating the top ones.
3. **Bare patches as geometry:** stamp them in the generator from the same seed as the recipe (or the recipe from the generator), lower the slab top by up to the audit's 5 cm there and give those triangles the soil slot, as the mounds already do; their physics then waits for the world's surface mask.
4. **Straw shading:** a per-vertex shade ramp along each straw (darker at the base, paler at the tip) and bent, bundled instances in swathes along a local direction field would take the "pick-up sticks" look off the 1.5 m tile.
5. **Per-patch LOD:** the whole 12 m patch switches by its centre, so a viewer 8 m from the centre loses straws 4 m away; acceptable for a gallery station, replaced by AST-10's near ring.
6. **The conftest clash:** rename the two `conftest` helpers into packages (or `tools/<x>/tests/helpers_<x>.py`) so the folders collect together; owner of `tools/` tests.
7. **LFS weight:** TextureCan's 16-bit normal PNG is 27 MB; an 8-bit copy derived by script would be a quarter of that if the LFS budget tightens.
8. **Physics reading:** the proposal names the physics engineer as a reader of the surface metadata, and no such reading is on the branch. QA accepted the 5 cm surface-top tolerance (AST-05 row above); a line from the physics engineer on it, and on the mounds' `crater_spoil` row, would close the loop before the physics gate.

## Required changes

- **R1** (asset-artist, `tools/assets/derive.py`): put the values of every constant the ground and palette formulas use (`STRAW_M`, `LOBE_M`, `BARE_FEATHER_M`, `GREEN_FEATHER_M`, `GAP_LOBES`, `SHARE_TOLERANCE`, `PHI`, `PALETTE_BLOCK_PX`, …) into the stamp's hashed inputs, and add a test that changing one regenerates the maps; the committed maps must stay byte-identical except for the stamp.
- **R2** (orchestrator, `specs/asset-pipeline/spec.md`): bring the "Straw palette" scenario to the built recipe (the shades and the twig, a column each), and validate `--strict`.
- **R3** (asset-artist or orchestrator, `design.md` §10): label every number listed under F3.
- **R4** (orchestrator with asset-artist, REAL-05): either build the bare patches as geometry (suggestion 3), or state in design §6 that the bare patches' step and soil surface are deferred, with the follow-up that delivers them (AST-10 / the phase-3 surface mask) named in `tasks.md` or the change that owns it.
- **R5** (orchestrator, REAL-03): add differences 6–10 above to design §10's "Not matched" list, each with where it is fixed (the feed stage, AST-10, the sheet's visual density, a review view) or why it is accepted for G1; and settle the "Straws read as geometry" scenario, whose by-eye clause (density, bare patches, strips match H) the build does not meet: either improve the look until it does, or narrow the clause to what a G1 stand-in can show and name the change that delivers the rest.

Verdict: CHANGES REQUESTED

## Re-review (after 1.5–1.7)

**Reviewer:** qa-engineer · **Branch:** `unity/art/p1-s04-belt-straw` · **Tip:** `347d08d` · **Range since the first pass:** `a930afc..347d08d` (18 commits: the physics reading 1.5, the rework 1.6 and 1.7, the orchestrator's records and rulings)
**Read:** `review-physics.md` (PR1–PR8, O1–O4, N1–N9), `design.md` (decisions 6, 9–14), `tasks.md`, the three spec deltas as amended, the full diff of `tools/assets/derive.py`, `tools/blender/lib/{patches,mat,straw,audit}.py`, `tools/blender/generators/ground_patch.py`, `tools/blender/build.py`, `assets-src/{surfaces/S04_patch.json,materials/belt_straw.json,materials/straw.json,physics_materials.json}`, `GroundPatchPrefabTests.cs` and every new or changed test; §S04 of `docs/object-sheets/earthworks-and-surfaces.md` from `origin/unity/main`.

Run by QA in its own fresh worktree (no `unity/Library/` before the first Unity run; `git lfs checkout`; all 22 LFS files real). Unity 6000.3.26f1 with Unity Hub running, Blender 5.2.2 LTS, Python 3.14.6 with Pillow 12.3.0, every Unity call piped. QA's scripts lived in its scratchpad and were deleted at the end; nothing was copied out of `reference/`. No performance claim is made (AC power, `BatteryStatus 2`, Balanced; no frame time is quoted). Other agents' pytest and a QA sweep ran on the machine at the same time; every run below ended normally.

### Checks

```
$ python -m pytest tools -q -p no:cacheprovider
  1 failed, 623 passed in 817.58s (0:13:37)
  FAILED tools/hygiene/tests/test_precommit.py::test_200_files_scan_in_under_2_s
         200 staged files: scan 4.998, 3.913, 1.972 s   (assert max(times) < 2.0)
$ python -m pytest tools/hygiene/tests/test_precommit.py::test_200_files_scan_in_under_2_s -q -s     (alone, three times)
  scan 4.995, 2.703, 2.687 s -> failed;  2.519, 2.656, 2.254 s -> failed;  2.531, 2.753, 2.703 s -> failed
  (another agent's sweep and its own run of this same test were running each time)
$ git diff --stat HEAD origin/unity/main -- tools/hygiene .githooks      -> (empty: the scanner and its test are unity/main's)
  -> pre-existing and load-sensitive (F6 of the first pass); not caused by this branch
$ python tools/assets/fetch.py --verify           -> OK   exit 0
$ python tools/assets/derive.py --all             -> belt_soil_mask, belt_straw_albedo, brick_red_mask, straw_albedo: up to date; derive.py: OK  exit 0
$ python tools/hygiene/precommit.py --tree HEAD   -> 497 files scanned, 0 findings   exit 0
$ openspec validate p1-s04-belt-straw --strict    -> Change 'p1-s04-belt-straw' is valid   exit 0

$ & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile - | Out-Host
  exit 0 (cold Library, 197 s); result=Passed total=25 passed=25 failed=0 skipped=0
  the first pass's 24, plus GroundPatchPrefabTests.Collision_IsTheSoilThenTheStrawMatThenTheMounds (new: the soil box's top at -0.125,
  the mat as straw boxes from -D to 0, then the spheres, every rotated box's collider within 0.01 deg of its rotation)
$ git status --short                              -> (clean)

$ pwsh tools/build_asset.ps1 S04                  (first run: exit 0, 348 s)
S04_patch: audit: skipped micro mesh S04_patch_LOD0_straw (20737 straw)
S04_patch: audit: soft tops collision[0] surface:belt_straw, collision[1..150] straw, collision[151..153] surface:crater_spoil are landed on over their whole top, held to 50 mm there
S04_patch: audit collision[0] box: box->mesh 27.463 mm at (-0.375, -0.125, -1.25), tolerance 50 mm
S04_patch: audit collision[1..150] box: box->mesh 0.000 .. 39.899 mm (largest collision[2] at (-4.8772, 0, -3.3738)), tolerance 50 mm   [150 lines, condensed]
S04_patch: audit collision[151] sphere: sphere->mesh 14.545 mm at (-4.641, 0, -4.171), tolerance 50 mm
S04_patch: audit collision[152] sphere: sphere->mesh 19.873 mm at (-3.8342, 0.0001, -2.7094), tolerance 50 mm
S04_patch: audit collision[153] sphere: sphere->mesh 18.029 mm at (-0.1168, 0, 1.5543), tolerance 50 mm
S04_patch: audit mesh: mesh->collision 40.000 mm at (-4.875, 0.04, -3.375) nearest collision[2], tolerance 50 mm
S04_patch: unity\Assets\_Project\Art\Models\surfaces\S04\S04_patch.fbx  size [12.0, 1.5787, 12.0] m  tris 23792 / 23792 / 26  micro S04_patch_LOD0_straw 20737 straw 409656 tris  154 collision shapes  fbx 15.7 MB  metadata written
build.py: OK
build_asset: [2/4] … belt_soil_mask, belt_straw_albedo, brick_red_mask, straw_albedo: up to date; derive.py: OK
[CatalogImporter] S04_patch: Assets/_Project/Prefabs/surfaces/S04_patch.prefab, 3 LODs, S04_patch_LOD0_straw (20737 straw, LOD0) micro, 154 colliders, materials ground -> belt_straw, soil -> belt_soil, straw -> straw, LOD heights 1.0000 / 0.1333 / 0.0200 (size 9.237604 m, fov 60 deg)
[Review] surface pilot pose: camera (0.000, 1.925, 0.000), 1.500 m above the patch centre (0.000, 0.425, 0.000), forward (0.8192, -0.5736, 0.0000), 35.00° down towards +X; bar base (2.500, 0.425, 0.000), 2.50 m ahead, in frame True
[Review] warm-up: one discarded render of S04_patch before its first tile          (once for the stage)
[Review] S04_patch day: review-sheets/S04/S04_patch_day.png (4096x4096, 6.4 MB); overcast 6.2 MB
build_asset: OK (S04)
$ git status --short                              -> (clean)
$ pwsh tools/build_asset.ps1 S04                  (second run: exit 0, 243 s; all 155 audit lines identical to the first run's)
$ git status --short                              -> (clean)
  S04_patch.fbx sha256 51da1546… (= its LFS oid), .assetmeta.json 9ea14385…, prefab 27cf08d2…, the same after both runs;
  both sheets byte-identical between the runs. (The pose's y = 0.425 is the mat's top on the stage: the slab is now 0.425 m
  deep, so the camera stands 1.5 m above the mat.)

$ pwsh tools/build_asset.ps1 K01                  (exit 0, 46 s)
K01_whole: audit collision[0] box: box->mesh 3.288 mm at (-0.125, 0, 0.06), tolerance 50 mm
K01_whole: audit mesh: mesh->collision 1.869 mm at (-0.1231, 0.0019, 0.058) nearest collision[0], tolerance 50 mm
[Review] warm-up: one discarded render of K01_whole before its first tile
build_asset: OK (K01)
$ git status --short                              -> (clean)
$ git rev-parse b16fd15:…/K01_whole.fbx HEAD:…/K01_whole.fbx  -> fa32a3c0… fa32a3c0…
$ git diff --stat 81d54e2 HEAD -- unity/Assets/_Project/Art/Models/debris unity/Assets/_Project/Prefabs/debris unity/Assets/_Project/Editor
  -> (empty: K01, CatalogImporter.cs and ReviewCapture.cs unchanged since the first pass)
```

**AST-03 again, on the shared layout.** QA set `bare.share` 0.08 → 0.12 in `assets-src/surfaces/S04_patch.json` and ran `pwsh tools/build_asset.ps1 S04` (exit 0): the one parameter moved the map and the mesh together, `derived 2048x2048 ground … bare 11.6 % (26 outlines from assets-src/surfaces/S04_patch.json; soil shown 9.9 %) … mean luminance 0.1638 (-0.0 %)`, 385 collision shapes all within 50 mm (largest 34.13 mm), `[CatalogImporter] … 385 colliders`, the sheets re-rendered, no hand step. Restored with `git checkout HEAD -- assets-src/surfaces/S04_patch.json` and rebuilt (exit 0): the FBX, the metadata, the derived maps and their stamp came back to HEAD's bytes and the prefab did not (F11); `git checkout HEAD --` of the prefab left the tree clean.

### R1–R5

**R1 closed.** The ground stamp hashes the value of every named constant (`derive.py:123-128` `GROUND_CONSTANTS` and `PALETTE_CONSTANTS`; `:238-241` `_constants`; `:527-535` the stamp's `constants` and `layout`, the latter with `patches.constants()` and `mat.constants()` and their code versions, `patches.py:29,45-46` and `mat.py:28,41-48`), and the formula texts carry versions (`ground v4`, `palette v2`). The fix commit `60b2fe9` changed the two stamps and no map, as R1 asked. Tests: `test_ground.py:315` (12 constants across the three modules, each re-derives) and `test_derive.py:154` (the palette). QA's repeat of the first pass's experiment, in a throwaway root holding copies of the recipes, the manifest, the two pinned sets and the committed maps and stamps:
```
committed stamps, unchanged code       -> belt_straw_albedo.png: up to date; straw_albedo.png: up to date
derive.STRAW_M 0.05 -> 0.10             -> belt_straw_albedo.png: derived 2048x2048 ground … (albedo cc9d218e…, committed 0b2fa410…)
STRAW_M restored                        -> derived again; albedo back to the committed bytes: True
derive.PALETTE_HEIGHT_PX 64 -> 32       -> straw_albedo.png: derived 128x32 palette …; restored -> derived 128x64
lib.mat.SWELL_SHARE 0.5 -> 0.4          -> derived; restored -> derived
lib.patches.EPS 1e-9 -> 1e-3            -> up to date             (not in CONSTANTS: F8, a nit)
stamps removed, everything from scratch -> belt_straw.recipe.json, _albedo, _detail_albedo, _mask, _normal, straw.recipe.json,
                                           straw_albedo.png: all byte-identical to the committed files
```

**R2 closed.** The scenario (`specs/asset-pipeline/spec.md:34-36`) names six gradients padded to eight. The committed `straw_albedo.png` is 128 × 64 with stamp `columns` straw_pale, straw, straw_dark, twig, blade, litter; QA read every column (bottom / middle / top): #aa976d #ccb685 #ead099 · #776745 #b39b6a #ddc084 · #645337 #967e55 #bb9e6a · #5a4c34 #6b5a3e #796647 · #53602c #6a7a3a #7c8e45 · #28231b #3a3227 #4f4535, and columns 7 and 8 repeat the litter column. The kit's `PALETTE` and `COLUMNS` (`straw.py:28-29`) match.

**R3 closed.** Every number §10 added carries a label and a reason (`design.md:54-66`), and so does every number §12 and §13 add (`design.md:68-80`, `:91-107`): carried, derived, measured or assumed, each with the frames or the rule behind it. A few code constants the design does not cite carry no label there: `WALL_V` (0.08, 0.9) and `TWIG_LAYER` (0.2, 0.8) in `ground_patch.py:80,86` and `V_LIMITS` in `straw.py:32`; `BARE_LOBES` and `BLADE_LEAN` say "assumed" in their code comments only. Non-blocking (F9).

**R4 closed, with the physics reading as built:**

| Item | As built | Evidence |
|---|---|---|
| PR1 the soil one mat depth down | `collision[0]` box 12 × 0.3 × 12 m, centre y −0.275 (top −0.125, bottom −0.425), `surface:belt_straw`, landable; slab D + `SOIL_M` = 0.425 m | committed metadata; `ground_patch.py:75,299-300`; EditMode `Collision_IsTheSoil…`; audit 27.463 mm |
| PR2 the mat as `straw` on the soil | 150 boxes `straw`, landable, centre y −0.0625, height 0.125 (30 fill + 10 × 12 edge boxes); the params `mat_material` and `mat_depth_m` with the 490 N/m note (`ground_patch.py:115-118,187-190`); the `straw` slot's physics is `straw`; the `straw` rationale replaced word for word as PR2 signed it (`physics_materials.json:114`) | metadata; `test_shapes_come_soil_fill_edges_mounds` |
| PR3 the step | floors are the relief lowered by D; vertical wall quads exactly on the outline edges (drop width 0, design §12); convex outlines with turns ≤ 90° enforced at draw (`patches.py:185-202`) | `test_bare_patches_step_down_by_the_mat_depth_along_their_outlines` (the walls' widths sum to each perimeter, D tall) |
| PR4 the mat opened over every floor | window = the outline's box grown by 0.3 m; one edge box per edge, its inner face on the edge, reaching ≥ 0.3 m and 0.05 m past the window; fill by a sweep with 0.05 m overlaps; placement by rejection; the 2 cm self-check runs in every build and raises on a miss (`ground_patch.py:253-257`) | `test_patches.py:46`; QA's four builds raised nothing |
| PR4 at 12 corners (ruling 14b) | `bare.outline_vertices` 12, `RADIAL_JITTER` 0.35; the self-check and the convexity and turn rules unchanged; 154 shapes | accepted: PR3's rules hold and the reading set no cap |
| PR5 one seeded definition | `lib/patches.py` and `lib/mat.py` (plain Python), imported by `derive.py:78` and by `ground_patch`; the recipe names the params file; the map's soil is the outlines times a ramp of each core, 0 outside | QA's measurement on the committed albedo: mean linear luminance 0.0660 inside the outlines shrunk by the feather (soil target 0.0620), 0.1007 at the outline ± 5 cm, 0.1733 from 0.6 m outside; mounds ≥ 0.5 m from every window (`test_mounds_keep_clear_of_the_bare_patches`) |
| PR6 the soft-top bound | `SOFT_SURFACES` and ≤ 1000 N/m (`audit.py:45-55,60-67`); the line names the soft shapes (`:82-85`); every other landable top keeps 2 cm | `test_audit.py:261,294` (asphalt and masonry fail the 2 cm zone, straw and belt_straw pass at 5 cm); the S04 line above |
| PR7 (a) | 23 359 floor samples over 10 patches: no shape above any floor, the soil under each | `test_no_shape_stands_above_a_floor` (`test_ground_patch.py:109`), QA's run |
| PR7 (b) | one 12 m straw box in place of the mat: `audit collision[1] box: box->mesh 144.573 mm` | `test_a_mat_box_left_whole_fails_the_audit_by_the_step` (`:135`), QA's run |
| PR7 (c) | 86 922 pixels inside the outlines shrunk by the feather, all ≥ 128; all 334 758 soil pixels within 5 cm of a floor | `test_the_bare_mask_and_the_step_outlines_agree` (`test_ground.py:335`), QA's run |
| PR7 (d) | every shape within 50 mm and "154 collision shapes" printed | the build above |
| PR8 the order | soil, 30 fill boxes, edge boxes by patch, 3 spheres; stated in the docstring (`ground_patch.py:55-64`) | `test_shapes_come_soil_fill_edges_mounds`, the EditMode test |
| O1–O4 | the spec requirement and scenario, the union requirement and design §6 carry the reading | the spec deltas, `design.md:22` |

*The soft-top list and `foliage`.* By the rule (a reference sink of 25 mm or more, k ≤ 1000 N/m) `foliage` at 200 N/m is soft; the reading's enumeration did not name it, and §12 records it. It reads as intended: a 20 mm branch at 200 N/m deflects 125 mm under the reference foot, so a 5 cm top is no visible float. A line from the physics engineer at the next reading would close it (suggestion 5).

*The audit's two new rules (task 1.6).* `COVERED_M` 2 µm (twice the metadata's 1 µm rounding) and "a cell no longer than `SEAM_CELL_M` 1 mm with each vertex inside some other primitive is covered" (`audit.py:52-55,298-313`). QA's experiments: two straw boxes on a soil box under a flat slab, with a gap between them (the author's seam test, varied), and the real S04 with one fill box pulled back:
```
gap 0.5 µm, 1 µm                   -> problems 0 (the rounding the 2 µm absorbs)
gap 3 µm, 10 µm, 0.1 mm            -> problems 2: collision[1], collision[2] box->mesh 124.939 mm (each box's face into the gap)
gap 0.5 mm, 1 mm, 2 mm             -> problems 3: also collision[0] box->mesh 125.000 mm (the soil exposed in the gap)
mutation COVERED_M 1e-9, gap 0.5 µm -> problems 2 (124.939 mm): the old tolerance fails the rounding
mutation SEAM_CELL_M 0, gap 0       -> problems 0 in 80.7 s (4.2 s with the rule)
real S04, fill box collision[1] pulled back 99 mm (1 mm overlap left)  -> 0 of 199 probes in no shape; problems 0
real S04, pulled back 101 mm (a 1 mm gap: 168 of 199 probes in no shape) -> problems 6, box->mesh 125.3–155.9 mm
```
So neither rule can hide a real gap in a stacked mat: from 3 µm up, the faces of the boxes on either side stand exposed through the mat's whole depth and fail by about D. The seam rule does drop the buried soil strip inside a sub-millimetre gap (the 10 µm–0.1 mm cases report only the boxes' faces), which is harmless while those faces are measured; and on the synthetic case it is a 19× speed-up rather than what ends the search (the search ended without it). The reasoning in the docstring holds; N1's metric (min |sdf|) is unchanged and still to come.

**R5 partly closed.** The look was reworked rather than only narrowed (loft, tussock relief, swathes, straws shaded along their length, blades, ragged lips, bare patches as geometry); §10's "Not matched" list was rewritten with 11 items (`design.md:42-53`); and ruling 14c narrowed the by-eye clause to what a G1 stand-in can show, with "§10 lists the rest with where each is fixed". Nine items name their fix or their acceptance. Two do not:
- **item 7** (`design.md:49`): the bare patches at 10 m read as pits; it records the cause ("a non-convex or sloped edge needs another box decomposition (physics)") but names no owner, no change and no reason G1 accepts it;
- **item 11** (`design.md:53`): the overcast sheen and the mounds' darker tint, no owner or acceptance.

Item 7 is the largest look gap left (REAL-03 below). R6.

### Spec scenarios (updated)

| Capability / scenario | Result | Evidence |
|---|---|---|
| assets-earthworks-and-surfaces · **Built from params by one command** | **PASS** | two runs exit 0, every output written, `git status` clean after each; FBX, metadata, prefab and both sheets byte-identical between the runs |
| … · **Straws read as geometry at the pilot's height** (as narrowed, ruling 14c) | **PASS, with one reservation (R6)** | the 1.5 m tiles, day and overcast, read at full size: separate lying straws in layers, crossing and resting on each other, standing stems with seed heads, twigs, and green blades where a strip enters the bake (the left edge), all meshes (20 737 instances, 409 656 triangles); beside H 5, 12, 14, 17 and 20 in the side-by-side, the colour family, the tangle and the strewn floors match; tussocks read weakly from this view and the straws are uniform thin tubes (§10 items 2, 4–6). Reservation: the near bare patch reads as soil under strewn straws (H 12), the far one, in the bake's feather 3–4 m out, as a cut hole with a dark wall (§10 item 7) |
| … · **No visible tiling at 10 m** | **PASS** | the 10 m and 50 m tiles by eye: no repeat, straw grain near. QA's statistic (luminance at 4.7 cm, high-passed at 0.5 m, autocorrelation normalised over the overlap at every shift of 0.4–4 m): the derived albedo peaks at +0.192 at 0.40 m (the 0.5 m tussock lattice) and at **+0.145 at 2.01 m** beyond 1 m; the known-wrong model, the cover tiled plainly at 2 m, scores **+0.924** (at 3.98 m) |
| … · **Collision follows the mat** | **PASS** | every shape ≤ 39.899 mm, mesh→collision 40.000 mm, the micro mesh named as skipped, the soft-tops line printed |
| … · **Bare patches are ground, not stains** | **PASS** | a step of D = 0.125 m along convex outlines (the walls sum to each perimeter); no shape above a floor (PR7 a); the floor is the `surface:belt_straw` soil box exposed through the opened `straw` mat; the map and the mesh from one seeded layout (PR7 c, QA's albedo measurement, and the AST-03 run that moved both together) |
| … · **Colour inside the sheet's range** | **PASS** | the stamp and QA's re-derivation: mean luminance 0.1638 vs 0.1638 (−0.0 %), linear 0.1946/0.1620/0.0908; soil #4a463f, the top of 10YR 2/1–3/1 (inclusive); the straw shades' middle stops within 3 % of #b49c6a (`test_straw_shades_average_to_the_sheets_dry_straw`); the day tiles beside H and G in the side-by-side |
| asset-pipeline · **Ground recipe derived and wired** | **PASS** | four maps 2048², the recipe stamped beside them, byte-identical from scratch; the three `GroundMaterialTests` pass |
| … · **A formula constant changes** | **PASS** | R1 above |
| … · **Mean albedo hits the target** | **PASS** | −0.0 %; bare 7.94 % (on the outlines), green 15.46 %, both within ±2 pp |
| … · **Micro meshes skipped by the audit** / **carry no collision** | **PASS** | the audit's first line; EditMode `Lod0_RendersTheSlabAndTheMicroMesh…` and `MicroMesh_HasNoCollider…`; 154 colliders for 154 primitives |
| … · **Straw palette** | **PASS** | R2 above; bound as `_BaseMap` (the importer is unchanged); the capture's empty-stage guard passed for both sheets |
| … · **Mound spheres sunk in the mat box** | **PASS** | `test_primitives_form_a_union_and_soft_tops_are_held_to_5cm`; S04's spheres at 14.5 / 19.9 / 18.0 mm (33.8–35.2 mm before the deeper slab) |
| … · **A hard top keeps 2 cm** | **PASS** | `test_audit.py:294`, four cases |
| … · **A set from a new source** | **PASS** | unchanged since the first pass; `--verify` OK |
| … · **A source that cannot be pinned** | not testable | as before: no such source arose |
| asset-review-sheet · **Pilot-height tile of a ground patch** | **PASS** | the pose line above; the bar's lower bands in frame in both 1.5 m tiles |
| … · **Other classes unchanged** | **PASS** | `ReviewCapture.cs`, `CatalogImporter.cs` and K01's inputs are unchanged since the first pass, which compared K01 pixel for pixel; K01's build leaves the tree clean with its audit numbers unchanged. QA did not re-render K01 with the merge-base capture this time |

### REAL-03 side-by-side and REAL-05 geometry audit

QA read the local side-by-side sheet rebuilt in task 1.7 (`reference/_compare/S04.png`: the 1.5 m and 10 m tiles beside H 5, 8, 12, 14, 17, 20 and G 5, 8), the H contact sheets (stills 1–21) and the G contact sheet (stills 1–12) in place, and full-size crops of QA's own 1.5 m, 10 m, 50 m, turntable and overlay tiles, day and overcast.

**Matches (improved since the first pass):** the colour family; a layered tangle at 1.5 m instead of one sparse layer (H 17); stems leaning out of tussocks, with seed heads (H 3–9); soil floors with straws strewn over them and straws hanging over the near floor's lip (H 12, H 14); blades where a strip enters the bake (H 19–21, sparse); lumpy tussock relief with darker hollows at 10 m (G 5, G 8, in form); no tiling.

**Does not match (beyond what §10 accepts):**
1. **Bare patches as pits.** At 10 m (LOD1, no straws) and in every turntable the ten patches read as clean, round, evenly scattered holes with vertical walls, the walls the darkest band in the scene (the litter column, #28231b–#4f4535) and the floors much darker than the mat. In H 9–14 a bare patch is irregular, path-like soil at the foot of a ragged, heaped mat edge, and G 1–12 at 10–13 m shows a continuous lumpy mat with no pits. The vertical step and the convex outline are what PR3 and PR4 allow; the wall's colour, the lip at LOD1, the floors' value (§10 item 8) and the patches' spacing are not bound by them. §10 item 7 records the gap without an owner (R6).
2. The three mounds read as dark bristly balls at 10 m: the soil slot ends on the refined grid's triangle edges, a jagged rim (F10).
3. At 1.5 m the straws render one pixel wide and aliased (the capture's MSAA 1), part of §10 items 2 and 6.

**REAL-05 list of physical features:**

| Feature | Physical role | As built | Status |
|---|---|---|---|
| mat top and tussock relief (±4 cm) | landing surface | slab top on `lib/mat.py`'s relief; 150 `straw` boxes, landable, flat tops within 39.9 mm | geometry (the slope is visual only, N4) |
| bare soil patches (8 %) | legs on soil one mat depth down | stepped floors in the mesh, the soil box exposed through the opened mat | **geometry** (R4 closed) |
| earth mounds (3) | uneven support | raised cells in the soil slot; `surface:crater_spoil` spheres within 19.9 mm | geometry |
| lying straws (top 250 /m², body 90 /m²), stems, twigs, blades | hooks; the body is the mat's compliance | visual-only micro mesh; lofted up to 75 mm above the straw boxes' top on the crowns, lip straws overhanging floors by up to 0.15 m | geometry, physics deferred to AST-10 (stated; the loft accepted by N7) |
| the lip's soil between straws (1.8 % of the patch) | none beyond the mat's | map only, the full-depth mat box under it | acceptable (the mat's cover, not its depth) |
| green regrowth strips | none beyond the mat's | map tint plus blades | acceptable |

**Ruling 14a against AST-10's parity.** AST-10 asks that drawn micro-detail come from the generator physics uses, at 0 mm parity. The G1 bake has no parity for any straw (its stated deviation, decisions 4 and 5, deleted when AST-10 lands), so a visual-only body layer adds no new deviation for G1, and its label (assumed, under the sheet's 250 /m² that physics reads) is honest. Accepted for G1. The risk comes after G1: the pilot will judge a mat of 340 straws/m², and an AST-10 generator drawing only the sheet's 250 at parity would thin it. The micro-detail change must either carry the body layer at parity (physics knowing those straws, even as non-hooking ones) or have the reference analyst's layering row (ruling 14d) settle the density first (suggestion 3).

### Hygiene

- 18 new commits since the first pass, 45 over `b16fd15..347d08d`: Asset Artist 35, Orchestrator 5, Tech Artist 3, Physics Engineer 1, QA Engineer 1; author = committer on every one; every body empty, no trailers; every subject matches `type(scope): lower-case summary`, the longest 71 characters.
- Nothing ignored is tracked: no path under `reference/`, `review-sheets/` or `unity/{Library,Temp,Logs,UserSettings}/` and no video file in `git ls-files` (497 files); every new file under `unity/Assets/` has its `.meta`.
- LFS: all 22 files real; the rebuilt maps and the FBX go through LFS.
- Licences: no new third-party file in the rework; `--verify` OK.
- OPSEC: the scan finds nothing; the rework's text cites letters and still numbers only.

### Requirements (updated)

| ID | Status | Why |
|---|---|---|
| AST-02 | met | §S04 drives every number; the new ones sit in its ranges or are labelled where it has none |
| AST-03 | met | one command, twice clean; `bare.share` moved the map and the mesh together with no hand step |
| AST-05 | met | 154 primitives within 50 mm, a material per shape, landable flags; the 5 cm top bounded to soft tops as the physics reading set; the bare patches carry their own shapes; the straw hooks deferred to AST-10 (stated) |
| AST-07 | met | no visible tiling (by eye, by the statistic and against its known-wrong model); UVs in metres |
| AST-08 | met for the task's budget | textures 2K at most (maps 2048², palette 128 × 64), 3 LODs at 8 / 60 / 400 m, LOD0 433 448 triangles under the task's 450 k, FBX 15.7 MB; no per-class phase-1 budget exists yet |
| AST-10 | not in scope | the bake is the stated G1 stand-in; ruling 14a accepted for G1 (suggestion 3) |
| AST-11 | met for belt_straw | the visual material paired with `surface:belt_straw`; the mat is `straw` per the physics reading |
| AST-12 | met | every tile, the turntable and the overlay, day and overcast; the overlay shows the opened mat around every floor |
| REAL-01 | partly met | the tangle, stems, strewn floors and relief now follow H and G; the bare patches' look at 10 m does not |
| REAL-03 | partly met | the sheet exists and was read; §10 items 7 and 11 name no fix or acceptance (R6) |
| REAL-05 | met | every physical feature is geometry; collision follows the mat, its floors and its mounds; the micro-detail's physics is AST-10's (stated) |
| SEC-04 | met | unchanged; `--verify` OK |

### Findings

- **F1–F4 closed** (R1–R4); **F7 partly closed** (R5, R6).
- **F5, F6 (pre-existing, open).** `pytest tools/assets tools/blender` still fails to collect (3 errors, as on `unity/main`); the hygiene timing test failed in all four runs under the other agents' load (largest scan 2.66–5.0 s) with code identical to `unity/main`.
- **F8 (nit, R1).** `lib.patches.EPS` (1e-9, used by the placement tests) is not in `CONSTANTS`, so changing it reports "up to date" (experiment above). It is a numerical guard rather than a tuned value.
- **F9 (nit, R3).** `WALL_V`, `TWIG_LAYER` and `V_LIMITS` are not labelled in the design; `BARE_LOBES` and `BLADE_LEAN` are labelled only in code.
- **F10 (look).** The mounds' soil slot ends on a jagged triangle rim: a bristly dark ball at 10 m.
- **F11 (importer, pre-existing).** `CatalogImporter` updates the prefab in place, so a collider whose kind changes at an index gets a new random fileID (`CatalogImporter.cs:504-509`): after the `bare.share` round trip (151–153 became boxes and spheres again) the prefab differed from HEAD by six fileID lines with identical values. Two builds from the same state stay byte-identical; a round trip does not. Not caused by the rework.
- **F12 (test margin).** `test_baked_straws_over_a_floor_lie_on_it` passes at a median of −0.090 m against −0.125 ± 0.04 (5 mm of margin), because the lip straws held at the mat's top within 0.15 m of a wall count among the floor's vertices; with `bare.share` 0.12 (26 smaller patches) it fails at −0.084 m. Measuring only vertices deeper than `LIP_OVERHANG_M` would test what its name says with a robust margin; the known-wrong model (straws left at the mat's height) fails either way.

### Suggestions (non-blocking)

1. (F8, F9) Add `EPS` to `patches.CONSTANTS`, and label `WALL_V`, `TWIG_LAYER`, `V_LIMITS`, `BARE_LOBES` and `BLADE_LEAN` in design §13 the next time it is touched.
2. (F12) Leave the lip zone out of the floor-straw test.
3. (Ruling 14a) Put the body layer into the micro-detail change's proposal: at parity, or settled first by the reference analyst's layering row.
4. (F11) If prefab churn matters for reviews, give each collider kind its own host name (`Collision151_sphere`), so a change of kind replaces a child rather than a component of a reused one.
5. (`foliage`) A line from the physics engineer confirming `foliage` among the soft tops.
6. The first pass's suggestions 1 (a low forward review view), 6 (the conftest clash) and 7 (LFS weight, now a 15.7 MB FBX) still stand.

### Required changes

- **R6** (orchestrator, with the asset-artist if the look changes; REAL-03, the rest of R5): §10 items 7 and 11 name no owner, change or G1 acceptance, against R5 and ruling 14c, and item 7 is the largest look gap left: the bare patches read as clean round pits at 10 m and in the turntables, and as a cut hole on the far side of the 1.5 m tile. Either improve the 10 m look within PR3 and PR4 and re-judge it against H 9–14 and G 1–12 (for example: the step walls in the mat's straw tones instead of the litter column's near-black; the ragged lip carried to LOD1; the floors' value once the reference analyst re-measures the soil, ruling 14d; patches gathered like H's paths rather than spread evenly), or rule it accepted for G1 and name in items 7 and 11 the owner and the change that fixes each. A re-review of R6 alone needs §10, plus the S04 build and its sheets only if the look changes.

Verdict: CHANGES REQUESTED

## Short pass after 1.8

**Reviewer:** qa-engineer (a fresh agent; none of the work is its own) · **Tip:** `8ee0917` · **Range:** `f0a12aa..8ee0917` (5 commits, task 1.8: R6 with F8 and F12)
**Read:** `design.md` decision 15 and §10 items 7, 8 and 11, `tasks.md`, the spec deltas, `review-physics.md` PR1–PR8 and N7–N8, the full diff of `tools/blender/lib/patches.py`, `tools/blender/generators/ground_patch.py`, `tools/assets/derive.py`, the params, recipes and catalog, and every changed test; §S04 of the object sheet from `origin/unity/main`; TST-06 and D-037.

Run by QA in its own worktree: `git lfs checkout` first (all 22 LFS files real), no `unity/Library/` before the first Unity run, Unity Hub running, no editor on the project, every Unity call piped. QA's scripts and crops live in its scratchpad; the side-by-side was built by QA from the H and G contact sheets into the git-ignored `reference/_compare/` and is not committed. No performance claim is made (the laptop read `BatteryStatus 1` at the start of the session).

### Checks

```
$ pwsh tools/build_asset.ps1 S04                       (first run: EXIT 0, 440 s)
S04_patch: audit: skipped micro mesh S04_patch_LOD0_straw (20329 straw)
S04_patch: audit: soft tops collision[0] surface:belt_straw, collision[1..195] straw, collision[196..198] surface:crater_spoil are landed on over their whole top, held to 50 mm there
S04_patch: audit collision[0] box: box->mesh 32.334 mm at (-1.5625, -0.125, -2.875), tolerance 50 mm
S04_patch: audit collision[1..195] box: box->mesh 0.000 .. 39.564 mm (largest collision[1] at (-5.9594, 0, 2.7539)), tolerance 50 mm   [195 lines, condensed by QA]
S04_patch: audit collision[196] sphere: sphere->mesh 15.876 mm at (4.1127, 0.0001, 0.0746), tolerance 50 mm
S04_patch: audit collision[197] sphere: sphere->mesh 23.369 mm at (5.0988, 0.0001, 1.4007), tolerance 50 mm
S04_patch: audit collision[198] sphere: sphere->mesh 17.652 mm at (2.2931, 0.0001, -2.3318), tolerance 50 mm
S04_patch: audit mesh: mesh->collision 40.000 mm at (-6, 0.04, 2.75) nearest collision[1], tolerance 50 mm
S04_patch: unity\Assets\_Project\Art\Models\surfaces\S04\S04_patch.fbx  size [12.0, 1.636, 12.0] m  tris 25029 / 25029 / 26  micro S04_patch_LOD0_straw 20329 straw 405792 tris  199 collision shapes  fbx 15.8 MB  metadata written
build_asset: [2/4] … belt_soil_mask, belt_straw_albedo, brick_red_mask, straw_albedo: up to date; derive.py: OK
[CatalogImporter] S04_patch: Assets/_Project/Prefabs/surfaces/S04_patch.prefab, 3 LODs, S04_patch_LOD0_straw (20329 straw, LOD0) micro, 199 colliders, materials ground -> belt_straw, soil -> belt_soil, straw -> straw, edge -> belt_straw, LOD heights 1.0000 / 0.1333 / 0.0200 (size 9.237604 m, fov 60 deg)
[Review] surface pilot pose: camera (0.000, 1.925, 0.000), 1.500 m above the patch centre (0.000, 0.425, 0.000), forward (0.8192, -0.5736, 0.0000), 35.00° down towards +X; bar base (2.500, 0.425, 0.000), 2.50 m ahead, in frame True
[Review] S04_patch day: review-sheets/S04/S04_patch_day.png (4096x4096, 6.5 MB); overcast 6.2 MB
build_asset: OK (S04)
$ git status --short                                   -> (clean)
$ pwsh tools/build_asset.ps1 S04                       (second run: EXIT 0, 281 s; all 202 audit lines identical to the first run's)
$ git status --short                                   -> (clean)
  sha256 of both sheets, S04_patch.fbx (f4a9bf1c… = its LFS oid), .assetmeta.json and the prefab: identical after both runs
  LOD0 25 029 + 405 792 = 430 821 triangles (< 450 k); 199 shapes = soil 1 + fill 39 + edge 156 (13 × 12) + mounds 3

$ python -m pytest tools -q -p no:cacheprovider -rf
  FAILED tools/hygiene/tests/test_precommit.py::test_existing_tree_is_clean[origin/unity/main merged with HEAD]
         AssertionError: this branch does not merge cleanly into unity/main  (git merge-tree returncode 1)
  1 failed, 634 passed in 752.98s (0:12:32)
$ git merge-tree --write-tree --name-only origin/unity/main HEAD
  conflicts: CREDITS.md, assets-src/catalog.json, tools/assets/{derive.py, fetch.py, manifest.json, tests/test_derive.py},
  tools/blender/{build.py, lib/audit.py, tests/test_audit.py}, unity/Assets/_Project/Editor/Assets/{CatalogImporter.cs, ModelPostprocessor.cs}
  -> the only failure, and it is the S04/V13 merge the handover's §4 lists (all eleven files are named there or are
     their tests; it is more than the two .cs files). The timing test (F6) passed this run.
$ python tools/assets/fetch.py --verify                -> OK   exit 0
$ python tools/assets/derive.py --all                  -> all four "up to date"; derive.py: OK   exit 0
$ python tools/assets/derive.py --root <QA scratch root: copies of assets-src, the manifest and the pinned sets> --all
  -> every recipe derived from scratch; all 11 outputs and stamps byte-identical to the committed files (cmp)
$ python tools/hygiene/precommit.py --tree HEAD        -> 497 files scanned, 0 findings   exit 0
$ openspec validate p1-s04-belt-straw --strict         -> Change 'p1-s04-belt-straw' is valid   exit 0
$ & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile - | Out-File …
  EXIT 0 (32 s); result=Passed total=25 passed=25 failed=0 skipped=0 (the re-review's 25)
$ git status --short                                   -> (clean)
$ python -m pytest -q -s tools/blender/tests/test_patches.py tools/blender/tests/test_ground_patch.py -k "lie_along_paths or gather or lip_stays or baked_straws_over_a_floor or step_down"
  S04: 6 paths, 11 of 13 patches on paths of two or more
  mean nearest gap 1.24 m scattered, 1.03 m gathered; within 1.2 m 51 % and 79 %      (deterministic: the same twice)
  905 teeth, droop 0.023-0.105 m, median 0.065 m
  bare.share 0.08: median straw vertex 0.012 m above the floor (3135), 0.026 m above the mat's top (67675)
  bare.share 0.12: median straw vertex 0.011 m above the floor (2904), 0.026 m above the mat's top (55653)
  6 passed
```

QA's own measurements (scripts in its scratchpad):
- **The layout** (`patches.layout` on the params): 13 patches, share 0.0807; lengths 0.79–1.98 m, widths 0.53–1.17 m, aspect 1.50–2.48 (inside the sheet's 0.5–2 m and the params' 1.5–2.5); paths of 3, 3, 3, 2, 1, 1 patches with 7 bridges; nearest outline gaps 0.76–1.29 m.
- **The albedo by zone** (every 4th pixel of the committed `belt_straw_albedo.png`, linear luminance; "dark" = below 0.09):

  | Zone | Area | Mean | Dark share |
  |---|---|---|---|
  | floors (inside the outlines) | 11.6 m² | 0.0851 | 0.61 |
  | path bridges (the hulls of two consecutive cores, outside the outlines) | 4.0 m² | **0.0944** | **0.53** |
  | lip ring (0–0.4 m outside an outline, off the bridges) | 22.7 m² | 0.1679 | 0.13 |
  | mat (beyond 0.6 m) | 89.5 m² | 0.1738 | 0.10 |
  | *what-if: the same derivation with the bridges left out of the lip layer* | | bridges 0.1719, lip ring 0.1682, mat 0.1707, floors 0.0847 | bridges 0.11 |

  The stamp's `lip_soil_share` (the soil the map shows outside the floors, over the mat) rose from 0.0179 (task 1.7) to 0.0528; without the bridges it is 0.0288. The floors' own soil (`bare_soil_share`) is 0.0668: the map now shows soil over a full-depth mat on 79 % as much area as it shows soil on the stepped floors (26 % after task 1.7).
- **Floor against mat in H's feed** (QA's boxes on H 12 and H 14, linear luminance): soil 0.136 against mat 0.209 in H 12 (0.65), soil 0.135–0.143 against mat 0.198 in H 14 (0.68–0.72); the asset's albedo 0.0851 against 0.1738 (0.49). The artist's 0.57–0.74 holds.
- **`bare.share` above 0.12:** 0.08, 0.10 and 0.12 lay out for seeds 1–8 (10–15, 13–20 and 21–30 patches; 4, 19 and 129 s for the eight); 0.14 raises `ValueError` after 118 s (seed 1, stuck at 0.1201) and 128 s (seed 27, 0.1253).

### R6: the bare patches against H 9–14 and G 1–12

QA read the review sheets at full size (every tile, day and overcast), its own crops of the 1.5 m, 10 m, 50 m, turntable and overlay tiles, and its side-by-side of H 9–14 and G 1, 5, 8, 12 beside them.

| Tile | What QA sees | Against the references |
|---|---|---|
| 1.5 m overcast | the near floor is soil under strewn straws a step down, the lip's straws over its edge | H 12, H 14 in kind: **matches** (G1 stand-in for the view and the straw form, §10 items 2, 6) |
| 1.5 m day | the same, the far wall a shadowed step crossed by straws; the mounds still dark bristly balls (F10) | **matches** for the floor; the mounds do not |
| 10 m overcast | elongated dark strips in rows, gathered along paths, the walls a fine comb at the far side | H 10, H 11, H 14 seen in plan (trodden soil strips through the mat): **matches in reading**; but the strips are continuous only because the bridges between the floors are dark too (R7) |
| 10 m day (45° sun) | every floor a crisp oval pit, its vertical wall lit or shadowed and topped by the lip's teeth, which read as a palisade; between the pits of a path, **dark smears at the mat's own height** | H's edges are heaped and slope onto the soil: the pit read is §10 item 7's accepted step; the smears are stains (R7) |
| 50 m, turntables | day: rows of small oval pits joined by dark trails ("footprint trails"); overcast: dark trails | as the 10 m tiles |
| overlay | the edge boxes ring every floor; the bridges lie under full-depth `straw` boxes | as built (PR4) |
| G 1–12 | no bare soil in the mat (the dark channel is a trench, E01) | the 8 % is H's; §10 item 7 records it: accepted |

**Ruling: R6 is closed for G1, except the path bridges, which become R7.** What R6 asked within PR3 and PR4 is done and seen: the walls carry the mat's map instead of the near-black litter band, the lip stays at LOD1 (905 teeth in the slab, `lod0 is lod1`), the outlines are path-like (aspect 1.50–2.48) and the patches gather along paths (11 of 13 on paths; mean nearest gap 1.03 m against 1.24 m scattered), and the floor colour was checked against H 12 and H 14 inside 10YR 2/1–3/1. What still differs is accepted for G1 exactly as design 15 and §10 items 7 and 8 state, each with an owner: the vertical 12.5 cm step that outlines each floor in the 45° sun (PR3; the physics engineer's box decomposition after G1, with item 4's heaps), the gathering bounded by PR4's windows (same owner), and the floor-to-mat contrast (0.49 in the albedo against 0.65–0.72 in H's feed; the reference analyst's soil re-measurement, ruling 14d). The bridges are not part of that acceptance: they are a new texture-only soil over a full-depth mat (below). After R7 the 10 m overcast tile is re-judged; floors in rows with mat bridges between them is what design 15 itself describes ("one trodden strip broken by mat bridges") and is acceptable for G1.

### The open questions

1. **Map-only thinning over a full-depth mat.**
   - *The lip ring: acceptable* (as the re-review ruled at 1.8 % of the patch). It is the mat's ragged cover within about 0.2 m of a real step; over 0–0.4 m outside the outlines the map reads 0.168 against the mat's 0.174 (13 % dark against 10 %), so it does not show ground where there is none. The lip's teeth are geometry within PR3's 0.05 m (`LIP_REACH_M` 0.01–0.04 m, the audit's mesh line at 40.0 mm) and carry the mat's physics (`edge` slot → `straw`): acceptable.
   - *The path bridges: not acceptable as built (R7).* Over 4.0 m² the map shows soil as a floor does (mean 0.094 against the floors' 0.085 and the mat's 0.174; 53 % dark against 61 % and 10 %), while the mesh stays at the mat's top and the shapes are full-depth `straw`. That is the "stain" R4 removed: a leg lands on 12.5 cm of straw where the map shows trodden soil, and in the day tiles the bridges read as dark smears on the mat, next to floors that visibly step down. It also fails the scenario "Bare patches are ground, not stains" on its own terms: where the derived map shows bare soil, the mesh does not step down.
   - *For the physics engineer, after G1:* whether a trodden bridge gets geometry and physics (a thinner `straw` mat on its own boxes, or one merged floor), together with design 15 item 7's decomposition.
2. **199 shapes against 154: within every budget there is.** No collision-shape budget exists: AST-08's per-class table counts triangles, and the physics reading set no cap (review-physics §3: `StaticContacts` pays per nearby shape; a tree carries 150–300 capsules). 199 shapes (1.4 per m²) sit inside that span. A per-asset shape budget is the physics engineer's to set when `StaticContacts` is specified; not blocking.
3. **`bare.share` above about 0.12: yes, narrow the range** to (0, 0.12) in `PARAMS`, with the effect text naming PR4's windows as the bound. Measured above: 0.12 lays out for seeds 1–8, 0.14 raises after about two minutes of search for both seeds tried (0.1201 and 0.1253 placed). Today `check` accepts 0.3 and the build fails late; the sheet's measured share is 0.08. Non-blocking (the error is explicit, not a wrong asset); best done with R7.
4. **The `litter` palette column: drop it.** No face takes it since task 1.8 (`grep`: only `straw.json`, `straw.PALETTE` and the docstring name it). Removing it touches `assets-src/materials/straw.json`, `straw.PALETTE`, the "Straw palette" scenario (six gradients → five) and re-derives `straw_albedo.png`; it is the orphan this change made (karpathy-guidelines §3). Non-blocking: kept, the scenario stays true; best done with R7.

### F8, F12

- **F8 closed.** `EPS` and the six new layout constants (`PATH_GAP_M`, `PATH_GAP_STEP_M`, `PATH_TURN_DEG`, `PATH_SIDE`, `CLUSTER`, `NEAR_M`) are in `patches.CONSTANTS` and in the committed stamp's `layout.constants` (tuples as lists), `FLOOR_STRAWS` is in `GROUND_CONSTANTS`, and the code versions moved (`bare patches v2`, `ground v5`). Every constant the layout reads is listed (`SAMPLE_M` feeds only the self-check). `test_a_changed_formula_constant_re_derives` covers `EPS`, `CLUSTER`, `PATH_GAP_M` and `FLOOR_STRAWS`, and fails when `EPS` is taken out of `CONSTANTS` (mutation M4 below). The ground-patch constants (`LIP_*`, `EDGE_SLOT`) shape the mesh, which every build rewrites, so they need no stamp.
- **F12 closed.** `test_baked_straws_over_a_floor_lie_on_it` now measures heights above the local floor for straw vertices deeper inside an outline than `LIP_OVERHANG_M` + 0.05 m: median 0.012 m (3 135 vertices) at the sheet's 8 % and 0.011 m (2 904) at 0.12, inside (−0.005, 0.03) with 18–19 mm to the upper bound and 16–17 mm to the lower. The known-wrong model fails it: with the floor branch of `rest()` switched off (M3), the medians are 0.131 and 0.130 m and both cases fail.

### TST-06: mutations of task 1.8's tests

Each mutation applied in the worktree, the test run, and the file restored with `git checkout --` (`git status` clean after each):

| # | Mutation | Test | Result |
|---|---|---|---|
| M1 | `_lip`: the tip's `min(0.0, …)` clamp removed, the tip 3 cm higher | `test_the_lip_stays_at_lod1_ragged_and_inside_the_step_allowance` | **fails** (`0.0055 <= 0.0`: a tip above the mat boxes' top) |
| M1b | `LIP_REACH_M` (0.01, 0.06) | the same | **fails** on PR3's literal (`0.06 < 0.05`) |
| M2 | `derive`: the floors' strewn straws dropped (`bare = ImageChops.multiply(bare, scatter)` removed) | `test_straws_lie_strewn_over_the_floors` | **passes: the test cannot fail** (R8) |
| M3 | `rest()`: floor straws left at the mat's height | `test_baked_straws_over_a_floor_lie_on_it[None, 0.12]` | **both fail** (0.131 / 0.130 m against 0.03) |
| M4 | `"EPS"` out of `patches.CONSTANTS` | `test_a_changed_formula_constant_re_derives[lib.patches-EPS-0.001]` | **fails** |
| M5 | new paths never start near the others (`CLUSTER` unused) | `test_patches_lie_along_paths`; `test_paths_gather_the_patches` | the first **fails** (5 of 11 on paths); the second passes (paths alone gather: 1.10 m against 1.30 m) |

**Why M2 survives.** `bare, strewn = measured(0.0), measured(derive.FLOOR_STRAWS)` evaluates `derive.FLOOR_STRAWS` after the first call has monkeypatched it to 0, so both derivations run at 0 and the test asserts |0 − 0| ≤ 0. Its own print says so: `floors' soil 90.6 % -> 90.6 %: straws over 0.0 % of it (FLOOR_STRAWS 0 %)`, on the unmutated code too. With the share captured first (QA's local probe, reverted), the mechanism covers **10.2 %** of the floors' soil on the test's cover against `FLOOR_STRAWS` 0.2, passing `abs(covered − 0.2) <= 0.1` by 0.2 percentage points, and M2 then fails it (`0.2 <= 0.1`). So design 15's "straws over about a fifth of the soil" is half delivered on that cover; a likely cause, for the asset-artist to confirm, is that `soil_mask = lighter(bare, lip)` lets the lip layer, which covers the floors at `LIP_LEVEL`, put soil back where the floor's straws were strewn. The test also compares the effect with the constant under test (`FLOOR_STRAWS`), not design 15's literal 0.2, and `test_patches_lie_along_paths` does the same with `PATH_GAP_M` (design 15: 0.3–1.5 m).

The other numbers asserted by 1.8's tests are literals or known-wrong models: PR3's 0.05 m and y = 0 in the lip test, the floor test's 0.03 m against D = 0.125 m, the scattered layout in the gathering test (margins 0.13 m against its 0.08 m and 8 percentage points against its 20).

### Spec scenarios (the ones task 1.8 touches; the rest are unchanged since the re-review: 1.8 changed no pipeline, importer or capture code)

| Scenario | Result | Evidence |
|---|---|---|
| S04 · Built from params by one command | **PASS** | two runs, EXIT 0, `git status` clean after each, every output byte-identical |
| S04 · Straws read as geometry at the pilot's height (as narrowed) | **PASS** | both 1.5 m tiles: 20 329 instances as meshes; the near floor reads as ground under strewn straws |
| S04 · No visible tiling at 10 m | **PASS** | by eye on the 10 m and 50 m tiles; `cover_field` and the cover's tiling are untouched since the re-review's statistic |
| S04 · Collision follows the mat | **PASS** | 199 shapes ≤ 39.564 mm, mesh 40.000 mm, the micro mesh named as skipped, the soft-tops line |
| S04 · Bare patches are ground, not stains | **FAIL on the bridges** (the floors pass) | floors: the step of D along convex outlines (`test_bare_patches_step_down…`), no shape above a floor (PR7 a), the soil box exposed, one seeded layout (PR7 c; byte-identical re-derivation). Bridges: the map shows soil (0.094 against the floors' 0.085) where the mesh does not step down (R7) |
| S04 · Colour inside the sheet's range | **PASS** | mean luminance +0.01 %; soil #4a463f, the top of 10YR 2/1–3/1; the mounds' `belt_soil` tint lands within a few codes of it (QA: the pinned set's sRGB-averaged mean × the tint, #4d463c) |
| pipeline · Ground recipe derived and wired; A formula constant changes; Mean albedo hits the target | **PASS** | from-scratch re-derivation byte-identical; M4; bare 8.07 %, green 16.05 % (±2 pp) |

### Requirements (changed since the re-review)

| ID | Status | Why |
|---|---|---|
| AST-03 | met | one command, twice clean; every new layout number is a param with a range (but see question 3) |
| AST-05 | met | 199 shapes within 50 mm; the lip within PR3's 0.05 m; the `edge` slot carries `straw` |
| AST-08 | met for the task's budget | LOD0 430 821 < 450 k; 2K maps; 3 LODs; FBX 15.8 MB |
| REAL-03 | partly met | R6 closed for G1 except the bridges (R7) |
| REAL-05 | **partly met** | the floors, walls and lip are geometry; the path bridges show soil on the map over a full-depth mat (R7) |
| TST-06 | **partly met** | four of five mutations caught; the floor-straw test cannot fail (R8) |

### Hygiene

- 5 commits, all `Asset Artist <assets@450fpv.local>` as author and committer; subject only (45–68 characters, `type(scope): lower-case summary`), no body, no trailer.
- 21 files changed, none new (no `.meta` to pair); nothing under `reference/`, `review-sheets/` or `unity/{Library,Temp,Logs,UserSettings}/` is tracked (497 files).
- LFS: all 22 files real; the rebuilt maps and FBX go through LFS. No new third-party file; `--verify` OK. OPSEC: 0 findings; the change's text cites letters and frames only.

### Findings

- **F13 (R7).** The path bridges are map-only soil over a full-depth `straw` mat: 4.0 m² reading as a floor in the map, dark smears in the day tiles; the scenario "Bare patches are ground, not stains" fails there.
- **F14 (R8).** `test_straws_lie_strewn_over_the_floors` is vacuous (evaluation order), and measured correctly the mechanism delivers 10.2 % of the 20 % it claims; it and `test_patches_lie_along_paths` assert the constant under test instead of design 15's literals.
- **F15 (record).** Design 15 records the gathering as 1.09 m against 1.24 m and 78 % against 46 %; the committed test prints 1.03 m against 1.24 m and 79 % against 51 %, the same on two runs.
- **F16 (look).** In the 45° sun the lip's teeth along a lit or shadowed wall read as a palisade of even stakes at 10 m and in the turntables.
- **F10 (look, open)** and **F11 (importer, pre-existing)** stand; F5 and F6 are unchanged (`unity/main`'s).

### Suggestions (non-blocking)

1. (Questions 3 and 4) Narrow `bare.share` to (0, 0.12) and drop the `litter` column with its scenario text, in the same round as R7.
2. (F15) Correct design 15's gathering figures to the test's own output.
3. (F16) Break the palisade: more spread in `LIP_TOOTH_M` and the droop, or teeth that lean out over the floor rather than hang straight; judge it in the day 10 m tile.
4. (M5) `CLUSTER`'s own effect is untested: the gathering test passes without it. A case comparing `CLUSTER` 0.85 with 0 at `path_patches` 3 would pin it.
5. (F10) The mounds still read as dark bristly balls at 1.5 m and 10 m; the asset-artist's, with the look-lock.

### Required changes

- **R7** (asset-artist; REAL-05, the scenario "Bare patches are ground, not stains"): the map may show soil over the full-depth mat only as the lip ring does. Take the path bridges out of `lip_layer` (or give them the mat's own look, flattened straw, never soil), so that QA's zone measurement reads the bridges within 10 % of the mat's mean linear luminance (now 0.094 against 0.174; QA's what-if without them: 0.172 against 0.171) and the stamp's `lip_soil_share` falls back to the ring's (0.029 in the what-if); adjust `test_a_floors_lip_shows_soil_just_outside_its_outline` and the design text (decision 15 "Paths and gathering", §10 item 7). If trodden bridges are wanted later as ground, they need geometry and shapes: the physics engineer's, after G1. Then rebuild S04 (twice clean) and re-capture; the next QA pass re-judges the 10 m tiles.
- **R8** (asset-artist; TST-06): fix `test_straws_lie_strewn_over_the_floors` so it can fail (capture the share before the first `measured()` call) and assert design 15's literal 0.2 with a tolerance that a removed mechanism fails; then either make the mechanism deliver about a fifth (QA measured 10.2 %) or restate design 15's figure and label it to what it measures. Assert `PATH_GAP_M`'s literal 0.3–1.5 m in `test_patches_lie_along_paths` likewise. Show M2 failing the fixed test in the handoff.

2.1 stays open: the pass on the merge with `unity/main` (handover §4) is still to come.

Verdict: CHANGES REQUESTED
