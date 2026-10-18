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
