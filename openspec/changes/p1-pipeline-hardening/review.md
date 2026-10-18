# Review: p1-pipeline-hardening (task 2.1)

**Reviewer:** qa-engineer · **Branch:** `unity/art/p1-pipeline-hardening` · **Tip:** `ad14747` · **Range:** `e6ee5d6..ad14747` (21 commits, 38 files, merge base with `unity/main`)
**Requirements cited:** AST-03, AST-05, AST-07, AST-12 · **Decisions:** D-004, D-028 · **Physics sign-off:** `review-materials.md` (task 1.5)
**Answers:** the archived `setup-asset-pipeline` review (suggestions 1–3) and its `review-physics.md` (P1-1, P1-2, P3-5), and the archived `setup-review-capture` review (the empty-stage capture)

Every check below was run by QA in its own fresh worktree (no `unity/Library/` before the first Unity run; `git lfs checkout` done first, `git lfs ls-files` showing `*` for all 7 LFS files). Nothing from any handoff was reused: the audit numbers, the mask channels, the ground brightness and the negative scenarios were re-measured with QA's own scripts, kept in QA's scratchpad. Unity 6000.3.26f1 with Unity Hub running (PID 4128; the `unity.exe` seen in the process list is the Hub's own `serve` helper, not an editor; no `unity/Temp/UnityLockfile`), Blender 5.2.2 LTS, Python 3.14.6, pwsh 7.6.7. Every Unity call was piped, so the exit codes are real. This review makes no performance claim, so the power state is irrelevant (`Win32_Battery.BatteryStatus` read 1 during the run; the capture ran on the RTX 4050 regardless). `reference/` was not opened: the REAL-03 side-by-side is outside this change (see Requirements).

## Full checks

```
$ python -m pytest tools -q                       -> 543 passed in 258.45s (0:04:18)   exit=0
                                                     (phase 0 had 431; the change adds 112 cases: params, audit,
                                                      derive, meta surface ids and table rules, headless negatives,
                                                      brick UVs; the Blender-backed tests ran, nothing skipped)
$ python tools/assets/fetch.py --verify           -> OK   exit=0
$ python tools/hygiene/precommit.py --tree HEAD   -> 373 files scanned, 0 findings   exit=0
$ openspec validate p1-pipeline-hardening --strict -> Change 'p1-pipeline-hardening' is valid   exit=0

$ & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile - | Out-Host
  EDITMODE exit=0 elapsed=00:02:50 (cold Library)
  unity/Logs/editmode.xml: result=Passed total=16 passed=16 failed=0 skipped=0 inconclusive=0
  Passed  Fpv450.Sim.Tests.DetMathTests.Sqrt_IsCorrectlyRounded  (x3)
  Passed  Fpv450.Tests.EditMode.CatalogImporterTests.BoxCollider_MatchesTheMetadataBox
  Passed  Fpv450.Tests.EditMode.CatalogImporterTests.LodGroup_HasTheTwoLodMeshes
  Passed  Fpv450.Tests.EditMode.CatalogImporterTests.Material_PacksTheDerivedMaskIntoMetallicGlossAndOcclusion   (new)
  Passed  Fpv450.Tests.EditMode.CatalogImporterTests.Material_UsesThePinnedBrickDiffuse
  Passed  Fpv450.Tests.EditMode.CatalogImporterTests.PhysicsMeta_CarriesMasonry
  Passed  Fpv450.Tests.EditMode.CylinderColliderTests.Cylinder_BecomesAConvexMeshColliderWithinFiveMillimetres   (new)
  Passed  Fpv450.Tests.EditMode.CylinderColliderTests.Cylinder_SecondBuildWithTheSameInputsChangesNoFile        (new)
  Passed  Fpv450.Tests.EditMode.NoclipCameraTests.Step_DistanceIsFrameRateIndependent
  Passed  Fpv450.Tests.EditMode.ReviewCaptureTests.RequireRenderable_NamesTheMissingMeshAndTexture              (new)
  Passed  Fpv450.Tests.EditMode.ReviewCaptureTests.RequireRenderable_PassesAMeshWithATexturedMaterial           (new)
  Passed  Fpv450.Tests.EditMode.ReviewCaptureTests.RequireRenderable_RefusesATextureThatIsNotAnAsset            (new)
  Passed  Fpv450.Tests.EditMode.XSignProbeTests.ImportedBounds_RunFromTheOriginToThePositiveSize
  Passed  Fpv450.Tests.EditMode.XSignProbeTests.Triangles_WindOutward

$ pwsh tools/build_asset.ps1 K01                  (warm Library, after the EditMode import)
build_asset: [1/4] Blender build of K01
K01_whole: audit collision[0] box: box->mesh 3.288 mm at (-0.125, 0, 0.06), tolerance 50 mm
K01_whole: audit mesh: mesh->collision 1.869 mm at (-0.1231, 0.0019, 0.058) nearest collision[0], tolerance 50 mm
K01_whole: unity\Assets\_Project\Art\Models\debris\K01\K01_whole.fbx  size [0.25, 0.065, 0.12] m  tris 44 / 12  metadata written
build.py: OK
build_asset: [2/4] Derived textures of every material recipe
unity/Assets/_Project/Art/Materials/brick_red_mask.png: up to date
derive.py: OK
build_asset: [3/4] Unity import of K01
[CatalogImporter] K01_whole: Assets/_Project/Prefabs/debris/K01_whole.prefab, 2 LODs, 1 colliders, materials brick -> brick_red, LOD heights 0.0722 / 0.0018 (size 0.25 m, fov 60 deg)
build_asset: [4/4] Review sheet of K01
[Review] K01: 1 variant(s), lights day/overcast, GPU NVIDIA GeForce RTX 4050 Laptop GPU (Direct3D12), quality High (URP-High), LOD BIAS 1, colour space Linear
[Review] tile 1.5 M PILOT HEIGHT AZ 225  BAR LOWER BANDS: camera (-0.22, 1.50, -0.22), 1.500 m to the look-at point, lower-left patch 120/120/119
[Review] tile 10 M SLANT 20° DOWN AZ 225: ... lower-left patch 120/120/119        (50 m and 200 m, culled: 120/120/119)
[Review] K01_whole day: review-sheets/K01/K01_whole_day.png (4096x4096, 0.6 MB)
[Review] tile 1.5 M PILOT HEIGHT AZ 225  BAR LOWER BANDS: ... lower-left patch 122/121/120   (overcast; every other tile 122/121/120)
[Review] K01_whole overcast: review-sheets/K01/K01_whole_overcast.png (4096x4096, 0.4 MB)
build_asset: OK (K01)
BUILD_ASSET exit=0 elapsed=00:00:58
$ git status --short                              -> (clean)
$ git status --porcelain --untracked-files=all    -> (empty)
SHA-256 after the build = HEAD: K01_whole.fbx 5bf2b054… (= the LFS oid, 18588 bytes), K01_whole.assetmeta.json ec893beb…,
K01_whole.prefab acaf84b9…, brick_red.mat 152561a6…, brick_red_mask.png 41e39278… (= its LFS oid), brick_red_mask.recipe.json 828c05a9…
```

## Spec scenarios

| Capability / scenario | Result | Evidence |
|---|---|---|
| asset-pipeline · Parameter sanity · **Out-of-range bevel refused** | **PASS** | QA's scratch root (a copy of `assets-src/` plus the committed K01 outputs), `bevel_mm.max` 20: `ERROR: K01_whole: params assets-src\debris\K01_whole.json: 'bevel_mm.max' = 20 is outside [0.0, 10.0]`, exit 1, no `FBX export` and no `audit` line, FBX and metadata SHA-256 unchanged. A `check` violation the same way: `bevel_mm.min` 3.5 → `'bevel_mm.min' = 3.5 exceeds 'bevel_mm.max' = 3.0`, exit 1, outputs unchanged. The committed test uses 200; also in plain Python: `bevel_mm.max` 10 with height 0.02 → `must stay under half the smallest size, 10 mm`; an unknown key is refused naming the nine keys the generator knows; the committed K01 params conform |
| asset-pipeline · Collision audit at build time · **Collision drift is caught** | **PASS** | Through the build (the generator monkey-patched to push the +X half of LOD0 out by 7 cm, as the committed test does with 6 cm): prints `audit collision[0] box: box->mesh 32.500 mm at (0.125, 0.0325, 0), tolerance 50 mm`, then `ERROR: K01_whole: mesh: mesh->collision 70.000 mm at (0.195, 0.0623, -0.0573) nearest collision[0] exceeds the tolerance 50 mm`, exit 1, no `FBX export`, the scratch outputs untouched. The other direction in plain Python: the box 7 cm wider than the mesh on each Z side → `collision[0] box: box->mesh 70.116 mm at (-0.125, 0, 0.13) exceeds the tolerance 50 mm`; 4 cm per side passes the 50 mm rule and fails it the moment the shape is `landable` (`near gaps/landable 40.202 mm` on the box, `32.500 mm` on the mesh, tolerance 20 mm). The K01 lines print the measured deviation both ways (3.288 mm, the analytic 2·w_max/√3 at a chamfered corner; 1.869 mm at a corner triangle's centroid, within the search's 0.1 mm of the analytic 1.898 mm). `narrowest_m`: a 0.9 × 2.0 m door at 0.900 prints `largest inscribed circle 0.9000, tolerance 5 mm`; 0.906 is refused (`they differ by more than 5 mm`), 0.904 accepted. K01 itself has no gap, so its build prints no narrowest line (correct) |
| asset-pipeline · Derived textures by script · **Packed mask for a material** | **PASS** | `Art/Materials/brick_red_mask.png` exists (RGBA 1024², through LFS, oid 41e39278…). Channels re-derived by QA from the pinned 16-bit sources: R = 0 everywhere (metallic 0), B = 0, **G − AO(8-bit) max abs diff 0, A − (255 − roughness(8-bit)) max abs diff 0** over all 1 048 576 pixels. `brick_red.mat`: `_MetallicGlossMap` and `_OcclusionMap` both → the mask (guid d78360e2…), `_METALLICSPECGLOSSMAP` and `_OCCLUSIONMAP` on, `_SmoothnessTextureChannel 0`, `_Smoothness 1` (the alpha unscaled), `_Metallic 0`; the mask imports as Default, linear (`sRGBTexture 0`), by `ArtImportSettings` over `Art/Materials/`; `Material_PacksTheDerivedMaskIntoMetallicGlossAndOcclusion` asserts it in Unity. The recipe is recorded beside it (`brick_red_mask.recipe.json`: formula, mask, each source's name and SHA-256, `inputs_sha256`). Regeneration: unchanged recipe → `up to date`; recipe edited (metallic 0.5, smoothness `roughness`) → `derived 1024x1024 …`, new PNG and stamp (pixel (512,512) = (128, 224, 0, 240)); recipe restored (`git checkout`) → derived again, PNG and stamp byte-identical to HEAD, `git status` clean. The one command runs it as step [2/4] |
| asset-pipeline · Cylinder primitives import · **Cylinder in metadata** | **PASS** | The two `CylinderColliderTests` pass in Unity (a 0.1 m × 0.4 m cylinder rotated 90° about Z over the probe fixture: one convex `MeshCollider`, centre and rotation as the metadata, bounds ±5 mm, every vertex within 5 mm of the true surface, ≤ 255 vertices, a second build changes no file). Code read: N = ⌈π / acos(1 − 0.005 / r)⌉ rounded up to a multiple of 4, clamped to 8…64, so the sagitta r·(1 − cos(π/N)) stays ≤ 5 mm up to r ≈ 4.1 m (r 0.1 → 12 segments, 3.4 mm; r 1 → 32, 4.8 mm; r 4 → 64, 4.8 mm); 2·64 = 128 vertices under PhysX's 255; caps fanned from each ring's first vertex (convex, fine). Beyond r ≈ 4.15 m the importer warns instead of failing (suggestion 1). The sibling `.asset` (`<variant>_cylinder_r<r>_h<h>.asset` beside the prefab, shared per radius and height, rewritten only when vertices change, stale ones deleted) is the right choice: a prefab cannot carry an added sub-asset, an unsaved mesh cannot be serialised into one, and the deterministic name plus the vertex comparison keep rebuilds idempotent (proved by the second test) |
| asset-pipeline · Surface materials … · **Surface material accepted** | **PASS** | `meta.validate` on K01's metadata with `collision[0].material` swapped: `surface:belt_bare` accepted; `surface:nonsense` → `collision[0]: surface material 'surface:nonsense' is not one of the thirteen surface ids surface:{meadow_sod, dry_crust, crater_spoil, belt_straw, belt_bare, tilled, burnt_field, weeds, yard_litter, rubble, asphalt, concrete_slab, dirt_track}`; `surface:stubble` rejected the same way; `surface:asphalt` accepted and plain `asphalt` rejected (`is not in assets-src\physics_materials.json`); the slot path (`materials[0].physics`) names the rule too. `test_every_surface_id_is_accepted_on_a_shape_and_a_slot` covers all thirteen |
| asset-pipeline · Surface materials … · **Proposed ids signed off** | **PASS** | Every row of the object sheets' collected table (`review-buildability.md` on `unity/ref/p1-object-sheets`: bark, twig, deadwood, forb_stem, asbestos_cement, soil, straw, fabric, rubber, plastic, plastic_film, mesh, glass_crumbs, ceramic, foam, asphalt-as-object) and every proposed field (break strength, buckling load, hook rate, adhesion, plate breakability) has a verdict with a reason in `review-materials.md` §2–§3: 9 added, 6 mapped, 1 declined; the `surface:<id>` convention, the three surface rows and the `stubble` variant settled in §4 and §2. `assets-src/physics_materials.json` holds **17 ids**, each with `friction_static`, `friction_kinetic`, `edge_radius_m` (number or null), stiffness with damping or neither, a `label` from the five and a non-empty `rationale` (43–466 characters); `deadwood` carries `break_stress_pa` 4.0e7 and is the only row that does; no plain id shares a surface name, and `load_physics_materials` refuses one that does (`asphalt`, `surface:soil`), a missing label and a non-positive `break_stress_pa` (parametrised test) |
| asset-review-sheet · An empty stage fails the capture · **LFS pointer instead of an FBX** | **PASS** | `K01_whole.fbx` overwritten with a 130-byte LFS pointer text, then `Fpv450.Review.Capture -asset K01`: Unity logs `Couldn't read file …/K01_whole.fbx`, then `[Review] FAILED: K01_whole: no mesh on K01_whole_LOD0, no mesh on K01_whole_LOD1 (an LFS pointer on disk? run git lfs checkout)`, **exit 1** after 19.7 s; the two sheets kept their mtimes and SHA-256 (c56f157e…, 7666b5e4…), no file written. `git checkout -- …K01_whole.fbx` restored the 18588-byte binary (hash 5bf2b054…), `git status` clean. The check runs before the scene is built, so an unknown texture asset fails the same way (`RequireRenderable_RefusesATextureThatIsNotAnAsset`) |

## Task-level checks (tasks.md 1.1–1.5, beyond the scenarios)

- **Validate before export (1.1, phase-0 suggestion 2):** `build_variant` now runs `params.validate` → `generator.build` → `meta.assemble` → `meta.validate` → `audit.run` before any file, exports to `<variant>.staged.fbx`, checks it against the metadata and only then `os.replace`s it and writes the metadata; the staged file and a freshly made folder are removed on failure. Both negatives above left the scratch outputs byte-identical and no stray file under the scratch `unity/`.
- **K01 UVs (1.2), looked at** (QA crops of both sheets at 3–4× nearest-neighbour): the 1.5 m pilot tile shows the brick's bed face as **one continuous brick surface, mottled mid-red, no mortar line anywhere** (0 pale near-grey pixels inside the brick's 155 × 155 px box in either light; phase 0 saw two light joints in a T here); the overlay and turntable 1/8 and 3/8 views show the stretcher and header faces the same way, each a single surface, the chamfers continuing their faces without a visible seam; the side faces are upright (`test_every_face_maps_into_the_brick_cell_at_a_uniform_centred_fit` asserts the top edge has the larger V, and the bed face reads the same up close). The cell is used at 0.63 of real scale on the stretcher and 0.34 on the bed face (the set's bricks are a smaller format), so the bed face is the photo magnified about 3×: it reads a little soft at 1.5 m, which is the price of one brick's surface from a wall scan and is acceptable for a loose brick. The bed face measures #966958 mean sRGB on the neutral stage in both lights; the object sheet's sunlit feed check value is #b97541 (an orange-red through the analog feed in sun), so the colour question stays with the K01 look-lock against the reference letters, not with this change.
- **lodBias (1.4):** `QualitySettings.asset` lines 36 / 89 / 142: `lodBias: 1` on Low, Medium and High (was 0.4 / 0.7 on Low / Medium); `ProjectSetup.ApplyQualityLevels` sets `lodBias` 1 on every level with the reason in a comment, so a re-run of the setup cannot bring the template values back. The capture prints the bias it ran with in its first line (`quality High (URP-High), LOD BIAS 1`); that line was already there in phase 0, so nothing was lost or added here.
- **The dark pilot tile (1.4):** `BuildGround` sets `shadowCastingMode = Off` on the 400 m ground plane (it receives, never casts; nothing lies beneath it). Measured on QA's sheets: the day pilot tile's ground reads 120/120/119 at the capture's lower-left patch, at the lower-right, mid-left, mid-right and under the label (119–121 along row 880 and down column 60), **equal to the 10 m tile's 120/120/119**; overcast 122/121/120 in both tiles everywhere. The 200 × 200 px corners of the pilot tile read 120–121 except the top-left (112), which is the scale bar's shadow, visibly so. The 61 the handover describes did not appear in this worktree either (cold Library, first capture, and again warm), consistent with the earlier QA pass that never saw it; the fix removes the only 400 m caster, so a near-cascade self-shadow of the ground has nothing left to come from. Said plainly: not reproducible by QA before or after, and the stage is correct after.
- **Physics sign-off (1.5) left K01 alone:** in `e6ee5d6..HEAD` the only K01 output that changed is `K01_whole.fbx`, by `3bbc034` (task 1.2's UV change, 18556 → 18588 bytes); `K01_whole.assetmeta.json` and `K01_whole.prefab` are untouched; `b67d084`, `b0583f0`, `bd33d96` touch only `physics_materials.json`, `meta.py`, `test_meta.py` and the change folder. A rebuild in QA's scratch root from the committed params is byte-identical to HEAD (FBX and metadata), and the end-to-end build left `git status` empty.

## Hygiene

```
$ git log --format='%h %an <%ae> [%s] body=[%b]' e6ee5d6..HEAD      (21 commits, every body empty)
ad14747 Asset Artist      test(unity): cover cylinder import on a throwaway metadata                    58
245e4a4 Asset Artist      feat(importer): import cylinder primitives as convex mesh colliders           67
f3fb714 Tech Artist       docs(openspec): tick task 1.4 of p1-pipeline-hardening                         54
5e2df15 Tech Artist       test(unity): cover the review capture's empty-stage check                      57
a8ad7e4 Tech Artist       fix(review): stop the stage ground casting shadows                             50
62e9910 Tech Artist       feat(review): fail the capture on an empty stage                               48
09eb86f Tech Artist       chore(unity): hold lodBias at 1 on every quality level                         54
aa0fc07 Orchestrator      docs(openspec): add the dark pilot tile to the capture task                    59
58703ac Asset Artist      docs(openspec): tick task 1.2 of p1-pipeline-hardening                         54
2f977e8 Asset Artist      test(unity): assert the brick_red mask feeds metallic-gloss and AO             66
3bbc034 Asset Artist      art(k01): map every brick face into one clean cell of the red_brick set        71
5aadccc Asset Artist      feat(assets): wire the derived brick_red mask into URP/Lit and the build       72
b360a68 Asset Artist      feat(assets): derive packed mask maps from material recipes                    59
bd33d96 Physics Engineer  docs(openspec): record the material sign-off and tick task 1.5                 62
b0583f0 Physics Engineer  feat(blender): accept surface:<id> materials for the thirteen surfaces         70
b67d084 Physics Engineer  feat(physics): sign off nine material ids and a break stress field             66
64f11e8 Orchestrator      docs(openspec): add physics sign-off and surface ids to the change             66
9a2d88f Asset Artist      docs(openspec): tick task 1.1 of p1-pipeline-hardening                         54
6a8e47d Asset Artist      feat(blender): validate params and audit before the fbx is staged              65
27cbe23 Asset Artist      feat(blender): add the collision audit against the lod0 mesh                   60
b026317 Asset Artist      feat(blender): declare generator params with ranges and refuse strays          69
```
- **Identities** as `docs/team.md`: Asset Artist `<assets@450fpv.local>` (11), Tech Artist `<techart@450fpv.local>` (5), Physics Engineer `<physics@450fpv.local>` (3), Orchestrator `<orchestrator@450fpv.local>` (2); each role's commits touch its own paths (the tech-artist `ReviewCapture.cs`, `ProjectSetup.cs`, `QualitySettings.asset` and the tests it wrote; the physics engineer `physics_materials.json`, `meta.py`, `test_meta.py`, `review-materials.md`; the asset-artist the rest), on the one change branch in sequence, as the workflow allows. Subjects 48–72 characters, imperative, lower case, no body, no trailer, no co-author line.
- **Staging:** every commit's file list is its subject's scope; nothing from `reference/`, `unity/Library/`, `Temp/`, `Logs/`, `UserSettings/` or `review-sheets/` (filtered `git diff --name-only`: none); `review-sheets/` and `unity/[Ll]ogs/` stay ignored (status empty after four Unity runs).
- **LFS:** all 7 LFS files real (`*`); the new `brick_red_mask.png` is under `filter: lfs` and is a pointer in git (oid 41e39278…, 1 214 440 bytes); `K01_whole.fbx`'s pointer matches its bytes. Every new file under `unity/Assets` has its `.meta` (mask PNG, recipe stamp, `AssemblyInfo.cs`, the two test files).
- **Third-party:** `fetch.py --verify` OK; the derived mask lives under `Art/Materials/`, not `Art/ThirdParty/`, so the "nothing unlisted under ThirdParty" rule stands without an exception, and its provenance is the recipe stamp's source hashes (D-004).
- **OPSEC:** `precommit.py --tree HEAD` 373 files, 0 findings; the diff carries no place, date, OSD value or footage name (the object sheets are cited by their public ids; letters only in `review-buildability.md`, which is not on this branch).
- **OpenSpec:** `openspec validate p1-pipeline-hardening --strict` valid; tasks 1.1–1.5 ticked by their owners with the evidence in their commits; there is no `design.md` for this change (the proposal's "What Changes" carries the design, and the new numbers, 5 mm cylinder tolerance, the 64-segment cap, 0.1 mm audit precision, 0.4 mm chord, are labelled only in code comments). Acceptable for a hardening change; noted for the archive.
- **Outside work:** none. The physics sign-off is a project role's review; its two code claims (`surface:<id>` acceptance, the table's 17 rows and `break_stress_pa`) were re-verified above rather than taken from the review.

## Requirements

| ID | Status | Notes |
|---|---|---|
| AST-03 | **met** (for this change's claim) | A params file outside its declared ranges, an unknown key or a `check` violation fails the build before anything is written, naming the key and the range; the one command now runs params → Blender → derived textures → import → sheet; a rebuild from unchanged params is byte-identical |
| AST-05 | **met** for the audit, the material list and the cylinder import; partly met overall, as cited | Collision follows the visual is now measured at every build (two-sided, ±5 cm, ±2 cm near gaps and landable faces, `narrowest_m` within 5 mm of the inscribed circle), the list grows only through physics sign-off with labelled values, shapes may stand for ground as `surface:<id>`, cylinders import. The drone-capsule gap probe is its own change (proposal non-goals) |
| AST-07 | **met** for the single-brick surface and for roughness and AO now reaching the shader; partly met overall, as cited | One brick's surface per face, upright, no joint; the packed mask by script with its recipe. The 1.4 m tile size is still an assumed, unlabelled number in `brick_red.json`, and the 10 m tiling check on a wall-sized asset belongs to the first wall |
| AST-12 | **met** | The sheet renders every tile with the asset present or fails loudly naming what is missing; looked at (1.5 / 10 / 50 / 200 m, turntable, overlay, both lights) and the ground is uniform. The side-by-side against the reference letters (REAL-03) is not part of this change and was not done here |
| D-004 | **met** | Derived textures made by script from pinned sources, the recipe recorded beside each; assets rebuilt, never hand-edited: the committed outputs equal a rebuild byte for byte |
| D-028 | **met** for "collision follows the visual" (now automated); the "copy of the references" judgement is **not testable in this review** (no reference access, and the K01 look-lock is a separate change) |

## Suggestions (non-blocking)

1. **Cylinders above r ≈ 4.15 m only warn.** With the 64-segment cap the sagitta exceeds 5 mm from about that radius and `CylinderMesh` logs a warning while the spec says "within 5 mm". No asset in the sheets has such a radius; when one comes (a silo, a water tower), either fail the import or accept the deviation explicitly in the metadata. Owner: asset-artist.
2. **`debris_brick.PARAMS` ranges vs the object sheet.** The generator declares `bevel_mm` 0–10 mm; the K01 sheet's generator table (on `unity/ref/p1-object-sheets`) says 1–4. `params.py` says the tables mirror each other; align one of them when K01's variants (half, chunk, cluster) land. Owner: asset-artist with the reference analyst.
3. **`brick_red.json`'s `tile_size_m` 1.4 and `CELL_PX`** are still assumed numbers without a label (phase-0 suggestion 3's second half); the generator's own comment says the cell belongs to the texture set and should move into the recipe keyed by `brick_type` when the silicate set arrives. Do that then, and label the tile size.
4. **Audit cost on large meshes.** `_nearest_face` is linear in the face count and the mesh→collision pass seeds a cell per triangle, so a house LOD0 with thousands of faces costs O(n²) point-face tests before the branch and bound prunes. Fine for debris; measure on the first house (H01) and add a face grid if the build time is felt. Owner: asset-artist.
5. **The capture's brightness probe is one 64 × 64 patch.** The turntable 7/8 day tile read 115 there only because the bar's 1.8 m shadow crosses the patch; a shadow or an asset in that corner would masquerade as the dark-tile bug. A median over the four corners would make the log line trustworthy. Owner: tech-artist.
6. **`test_k01_measures_as_the_physics_review_did`** holds the mesh→box figure to 1.898 ± 0.1 mm; the search returns 1.869 (its 0.1 mm precision). 0.07 mm of margin is thin if `PRECISION_M` ever moves; assert the analytic value within the precision constant instead.
7. **Review stage colour.** The brick reads #966958 on the neutral stage against the sheet's sunlit feed check #b97541; the K01 look-lock change should compare the brick through the feed's light and response, not on this stage, before calling the material done (REAL-03).

All 7 spec scenarios pass, each re-run by QA with its own inputs; the phase-0 suggestions 1–3 and the physics review's P1-1, P1-2 and P3-5 are closed and verified; the empty-stage failure the review-capture re-review asked for is in. K01's outputs are unchanged except for the intended UV change, and a rebuild reproduces them byte for byte.

Verdict: APPROVED
