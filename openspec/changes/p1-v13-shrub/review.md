# Review: p1-v13-shrub (task 2.1)

**Reviewer:** qa-engineer · **Branch:** `unity/art/p1-v13-shrub` · **Tip reviewed:** `bbbeed5` · **Range:** `5fdfcfc..bbbeed5` (22 commits, 85 files; `5fdfcfc` is the merge base with `unity/main`)
**Requirements cited:** AST-02, AST-03, AST-05, AST-07, AST-08, AST-09, AST-12, REAL-01, REAL-03, REAL-05, SEC-04 · **Decisions:** D-003, D-004, D-020, D-028
**Inputs judged against:** `docs/object-sheets/vegetation.md` §V13 and its group rules, the change's two spec deltas, design decisions 1–6 with their "as built" and "as tuned" notes, the local side-by-side sheet for V13 under `reference/_compare/` and the H (stills 1–21) and F (stills 1–12) contact sheets.

Every check below was run by QA in its own worktree: `git lfs checkout` first (22 LFS objects, `git lfs ls-files` all `*`), no `unity/Library/` before the first Unity run (cold import), Unity 6000.3.26f1 with Unity Hub running and no editor on the project, Blender 5.2.2 LTS, Python 3.14.6, pwsh 7.6.7, every Unity call piped so the exit codes are real. Nothing from the handoffs was reused: the sizes, the shell, the band fill, the colours, the back-face luminance and the mip coverage were re-measured with QA's own scripts, kept in QA's scratchpad and deleted from the project afterwards (the tree was clean after every step). No performance claim is made here, so the power state does not matter. The reference material was read in place and nothing was copied out of it.

## Full checks

```
$ python -m pytest tools -q                       -> 577 passed in 420.10s (0:07:00)   exit=0
$ python tools/assets/fetch.py --verify           -> OK   exit=0
$ python tools/hygiene/precommit.py --tree HEAD   -> 506 files scanned, 0 findings   exit=0
$ openspec validate p1-v13-shrub --strict         -> Change 'p1-v13-shrub' is valid   exit=0

$ & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile - | Out-Host
  exit=0, "Test run completed. Exiting with code 0 (Ok)" (cold Library)
  unity/Logs/editmode.xml: result=Passed total=20 passed=20 failed=0 skipped=0 inconclusive=0
  Passed  Fpv450.Sim.Tests.DetMathTests.Sqrt_IsCorrectlyRounded  (x3)
  Passed  Fpv450.Tests.EditMode.AlphaClippedMaterialTests.LeafRecipe_BaseMapKeepsItsCoverageAtTheCutoff      (new)
  Passed  Fpv450.Tests.EditMode.AlphaClippedMaterialTests.LeafRecipe_ClipsAtHalfAndCullsNoFace               (new)
  Passed  Fpv450.Tests.EditMode.AlphaClippedMaterialTests.OpaqueRecipe_KeepsUrpLit("bark_shrub")            (new)
  Passed  Fpv450.Tests.EditMode.AlphaClippedMaterialTests.OpaqueRecipe_KeepsUrpLit("brick_red")             (new)
  Passed  Fpv450.Tests.EditMode.CatalogImporterTests.{BoxCollider_MatchesTheMetadataBox, LodGroup_HasTheTwoLodMeshes,
          Material_PacksTheDerivedMaskIntoMetallicGlossAndOcclusion, Material_UsesThePinnedBrickDiffuse, PhysicsMeta_CarriesMasonry}
  Passed  Fpv450.Tests.EditMode.CylinderColliderTests (x2), NoclipCameraTests (x1), ReviewCaptureTests (x3), XSignProbeTests (x2)
  git status after the run: clean (the coverage test edits the base map's import settings and the build restores them)

$ pwsh tools/build_asset.ps1 V13      (run 1)   -> build_asset: OK (V13)   exit=0
  V13_low:  audit mesh: mesh->collision 5.715 mm at (-0.0011, 0.1894, 0.1054) nearest collision[9], tolerance 50 mm
  V13_low:  audit collision[42] sphere (twig shell): largest outside distance 0.000 mm over 8462 vertices of slots leaf, twig, tolerance 50 mm
  V13_low:  ...V13_low.fbx  size [1.5439, 1.2066, 1.4422] m  tris 5850 / 2202 / 6  metadata written
  V13_mid:  audit mesh: mesh->collision 8.238 mm at (0.1249, 0.2928, 0.103) nearest collision[3], tolerance 50 mm
  V13_mid:  audit collision[42] sphere (twig shell): largest outside distance 0.000 mm over 13702 vertices of slots leaf, twig, tolerance 50 mm
  V13_mid:  ...V13_mid.fbx  size [2.0933, 2.0277, 2.0933] m  tris 8380 / 3558 / 6  metadata written
  V13_tall: audit mesh: mesh->collision 11.698 mm at (-0.1841, 0.469, -0.133) nearest collision[24], tolerance 50 mm
  V13_tall: audit collision[42] sphere (twig shell): largest outside distance 0.000 mm over 17310 vertices of slots leaf, twig, tolerance 50 mm
  V13_tall: ...V13_tall.fbx  size [2.4864, 3.004, 2.4313] m  tris 10520 / 4292 / 6  metadata written
  capsule->mesh, 42 lines per variant (126 in all), min/max:  low 5.660 / 11.372 mm, mid 6.403 / 11.371 mm, tall 9.948 / 12.541 mm (tolerance 50 mm)
  derive.py: bark_shrub_mask, brick_red_mask, leaf_shrub_mask, leaf_shrub_base: up to date
  [CatalogImporter] V13_low:  ...V13_low.prefab,  3 LODs, 43 colliders, stem -> bark_shrub, twig -> bark_shrub, leaf -> leaf_shrub, LOD heights 0.1783 / 0.0143 / 0.0024
  [CatalogImporter] V13_mid:  ...V13_mid.prefab,  3 LODs, 43 colliders, ..., LOD heights 0.2417 / 0.0194 / 0.0023
  [CatalogImporter] V13_tall: ...V13_tall.prefab, 3 LODs, 43 colliders, ..., LOD heights 0.3469 / 0.0278 / 0.0033
  [Review] V13: 3 variant(s), lights day/overcast, GPU NVIDIA GeForce RTX 4050 Laptop GPU (Direct3D12), quality High (URP-High), LOD BIAS 1, colour space Linear
  [Review] six sheets written, review-sheets/V13/V13_{low,mid,tall}_{day,overcast}.png, 4096x4096
  git status --short --untracked-files=all: clean
$ pwsh tools/build_asset.ps1 V13      (run 2)   -> build_asset: OK (V13)   exit=0, every audit line identical to run 1
  git status --short --untracked-files=all: clean; the six review sheets are byte-identical to run 1 (SHA-256 compared)
$ pwsh tools/build_asset.ps1 K01                -> build_asset: OK (K01)   exit=0
  K01_whole: audit collision[0] box: box->mesh 3.288 mm ..., mesh->collision 1.869 mm ..., tris 44 / 12
  git status --short --untracked-files=all: clean (K01's FBX, metadata, prefab, material and textures unchanged)
```

During run 1, a QA scratch script sat in `Editor/` for under a minute (moved out at once; Unity removed its stray `.meta`). It touched no V13 or K01 input, and run 2 on a clean tree reproduced run 1 byte for byte, so run 2 is the evidence for the "second run" clause and run 1's outputs equal it.

## Spec scenarios

| # | Scenario | Result | Evidence |
|---|---|---|---|
| V13-1 | Built from params by one command | **PASS** | Runs 1 and 2 above: exit 0 for all three variants; FBX, metadata, prefabs and six review sheets written; the second run leaves `git status` clean and the sheets identical. The committed metadata equals a plain-Python rebuild of each variant (`json` compare: True ×3) |
| V13-2 | Sizes inside the sheet | **PASS** | Metadata bounds (h × w × d): low 1.207 × 1.544 × 1.442 m (sheet h 1–1.5, w 1–2); mid 2.028 × 2.093 × 2.093 m (1.5–2.5, 1.5–3); tall 3.004 × 2.486 × 2.431 m (2.5–3.5, 2–3); `min.y` 0 for all. The tall variant's lowest leaf-card vertex is at **1.251 m** (≥ 0.9). Stems 14 (sheet 5–20), base 18 mm (10–30), twigs 8 → 3 mm (3–8; `TWIG1_R`, `TWIG2_R`), leaves 2–5.5 cm at `card_m` 0.2 (2–6; design 2's measurement of the atlas, consistent with the 1.5 m crop) |
| V13-3 | Reads as a wall at the belt edge | **PASS** for opacity and leaf scale; colour **testable at albedo only** | QA's own measurement of the 10 m overcast tile of `V13_mid`: rows of the 1–2 m band projected from the logged camera (−6.64, 4.43, −6.63) through the bounds centre (60° FOV, 1024 px, rows 427–512), a pixel "not ground" when any channel differs from its row's ground median by more than 12 (8 and 20 give the same to ±0.003): **0.94** of the silhouette span filled (task 1.3: 0.94), 0.69 of the projected bounds width (0.49 of the diagonal box width, task 1.3's 0.48); the weakest rows are the band's ends (0.62 at 2.0 m, the crown top; 0.74 at 1.0 m, the crown's lower edge). The day tile gives the same 0.94. At 50 m: **0.92** (task 1.3: 0.92). At 1.5 m (full-resolution crop) the leaves read as small oval leaves on sprigs, 2–5.5 cm; twigs show between the leaves at the crown's underside and edges, not inside the mass. Foliage colour, mean sRGB of the leaf pixels: **#5a603d** at 10 m overcast, #5f6541 at 1.5 m, against the sheet's photo albedo #58602c; the H 9 feed value #817651 cannot be matched on this stage because the stage has no feed (REAL-03 compares clips through the feed); by eye V13 is greener (r < g) than H's warmer, autumn-mixed band (r > g), see H-4. "Opaque from the ground up at 1–2 m" is read as "across the 1–2 m band", as task 1.3 and the orchestrator read it: the mid variant is bare stems below 0.82 m by design (`bare_base_m` 0.6) |
| V13-4 | Stems and shell carry the contact | **PASS as written**; see **R1** | 42 capsules per variant (14 stems × 3 segments), all `bark`, none landable, radii 5.7–8.3 mm; the audit's two-sided capsule ↔ stem-tube distance is at most 12.5 mm (tolerance 50); QA's own check of each capsule axis against the stem's sampled centre curve: at most 4.7 / 7.6 / 11.7 mm (low / mid / tall). 14 snags per variant, all `hook`, `radius_m` 0.009 = half the 18 mm base diameter, each running base (y 0) to tip along its stem's segment ends. One `twig` sphere per variant enclosing every LOD0 leaf and twig vertex: largest outside distance 0.000 mm (signed −1.0 mm, the 1 mm pad). Wind: one box, porosity **0.35**. `gaps` empty. Nothing landable |
| V13-5 | Four LODs under budget | **PASS** | Tris low 5 850 / 2 202 / 6, mid 8 380 / 3 558 / 6, tall 10 520 / 4 292 / 6: LOD0 < 12 000, each LOD ≤ half the previous. Switches 7.5 m (an 8 mm twig under 1 px: 0.008 × 935 = 7.48) and 93.5 m (a 0.2 m card under 2 px). Cull 561 / 800 / 800 m (height × 467.5: 561 / 935 / 1 402, capped at 800; the built heights give 564 / 948 / 1 404). Textures: every new map ≤ 2048 px (LeafSet002 and the derived leaf maps 2048², bark 1024²), `maxTextureSize` 2048 in every `.meta`. The title says four LODs; there are three plus the cull (suggestion 7) |
| AP-1 | Leaf card material | **PASS** | EditMode `LeafRecipe_ClipsAtHalfAndCullsNoFace`: `Fpv450/Leaf`, `_ALPHATEST_ON`, `_AlphaClip` 1, `_Cutoff` 0.5, `_Surface` 0, queue 2450, `_Cull` Off, double-sided GI, base map = the derived `leaf_shrub_base.png` with its alpha (sRGB, alpha from input, alpha is transparency). The committed `leaf_shrub.mat` carries the same. The turntables show every card's silhouette from all eight sides, both lights |
| AP-2 | Back faces lit as leaves | **PASS**, with a thin margin and a weak metric (finding P-1) | QA's own render: one sprig card (atlas cell 0, 0.2 m) under the day profile's sun and ambient, camera on the sun's line so the sun is behind it, card turned to show its front, then its back (180° about its own vertical axis), mean linear luminance over its 14 857 leaf pixels: **front 0.1056, back 0.0911, back / front 0.863** (inside 15 %, not task 1.4's 0.999). The back is green, sRGB (83, 86, 60) against the front's (89, 93, 66). Known-wrong model, the same card on URP/Lit with cull off: back 0.1128, ratio 1.069, which also passes the 15 % luminance test, but it is a neutral grey sheen, sRGB (92, 95, 93), and with the sun off it is 3.4× its front (all of it reflection at a back-facing normal). The shader's back path negates the normal and the whole tangent, so the TBN, the geometric and the mapped normal all flip; the shadow caster biases along the face the light sees |
| AP-3 | Coverage kept at distance | **PASS**, with a weak metric (finding P-2) | Imported base map: `mipMapsPreserveCoverage` True, `alphaTestReferenceValue` 0.5 (`.meta` and the EditMode test, which first breaks both and sees the build restore them). Opaque share (alpha ≥ 128) per mip of an uncompressed readable copy with the same settings: 2048 px **20.35 %**, 64 px **20.70 %** (within 1.7 %; task 1.4: 20.35 / 20.73). Known-wrong model, coverage preservation off: 64 px 20.61 %, which also passes; the two part only at 8 px and below (on 21.88 / 25.00 / 25.00 %, off 18.75 / 0.00 / 0.00 % at 8 / 4 / 2 px) |
| AP-4 | Shell encloses the cards | **PASS** | The build prints each variant's shell with largest outside distance 0.000 mm (above); `test_cards_inside_the_shell_pass_with_zero_printed`; QA on the real `V13_mid`: as built 0.000 mm, no problem |
| AP-5 | Card outside the shell | **PASS** | `test_a_card_corner_8cm_outside_fails_naming_the_shell_the_distance_and_the_tolerance`; QA on the real `V13_mid`: one leaf corner moved 8 cm outside → `collision[42] sphere (twig shell): largest outside distance 80.000 mm at (0.524, 2.0256, 0.7539) exceeds the tolerance 50 mm`; the sphere shrunk by 4 cm → 39 mm, passes; shrunk by 8 cm → 79 mm, fails |

## The look (REAL-01, REAL-03, AST-12)

Read by eye: all six review sheets (1.5 / 10 / 50 / 200 m, the turntables and the overlays, day and overcast), full-resolution crops of `V13_mid`'s 1.5, 10 and 50 m tiles, the local side-by-side sheet for V13 (H 3, 9, 16 at 10 m; H 9, 20 at 1.5 m; F 8, 11 in day light; the before-tuning row) and the H (1–21) and F (1–12) contact sheets.

**What matches.** Clump heights and widths against the pole and trunk scale of H; the leaf scale at 1.5 m (a fine mass of small leaves, the honeysuckle and caragana look; the before-tuning fronds are gone); the 1–2 m band is opaque from 10 m to 50 m, as PR-10 asks of the belt edge; the overcast foliage value sits near H's bush band; F's mid-ground dense dark-green clumps match V13's mass and value in day light. The 50 m tile no longer thins out and the 200 m clump is still drawn.

**Findings against H** (each is a REAL-03 difference, to become a task, fixed or explained):
- **H-1 Season and state.** H's understory is autumn: mixed green and yellow, part-bare, with dry stems standing in it. V13 is built in the summer state. Out of this change's scope (proposal non-goals; G1 shows summer); the orchestrator plans the belt's H autumn state (V13 and V01 together) in `p1-v01-locust`.
- **H-2 Structure: domes on stilts versus a stem-rich thicket.** H's bush layer is a continuous thicket of many thin stems of mixed thickness and lean, forking and crossing, visible through a fine twig mesh, with sky gaps above about 2 m. Each V13 clump is a closed dome over a bundle of 14 near-parallel, straight, identical stems (all 18 mm, from a 0.22 m disc, tips drifting 0.15 × height): the mid variant shows bare stilts 0–0.8 m and the tall 0–1.25 m. No stem forks, leans or crosses, and none is dead. At 1.5 m the bundle reads as poles. This belongs to the shared skeleton V01 grows (stem lean and spread, forks, mixed diameters, Leonardo's rule), so it is cheapest to fix there.
- **H-3 Twig mesh hidden.** At 10 m the V13 crown is a uniform leaf mass. In H the dark stems and twigs are the dominant texture of the 1–2 m band. At 1.5 m V13 shows its twigs only at the crown's underside. This follows the summer `leaf_cover` 1.0 and mostly goes with H-1.
- **H-4 Colour.** V13 foliage #5a603d (10 m, overcast, photo albedo, no feed) is green-olive (r < g). The H 9 feed value #817651 is warmer and brighter (r > g). Part of that is the season and part the feed, which the stage does not render, so it is judged again through the feed (tech-artist).
- **H-5 Isolated clumps read as domes.** H's wall is continuous. Whether V13 clumps placed 1–4 m apart make a wall with H's ragged top line, rather than a row of domes, is a placement question (world-builder) to check in the first belt view.
- **H-6 LOD2 brighter than LOD1 under overcast.** Foliage mean sRGB #585d3b at 50 m (LOD1) against #676e42 at 200 m (LOD2), +18 % luminance. 49 pixels at 200 m, edge-blended with the lighter ground, so this is an upper bound; task 1.3 saw about 10 %. The crossed far cards should be matched to LOD1's value under both lights.
- **H-7 Low variant not quite leafy to the ground.** 17 of 1 838 cards have centres below 0.2 m; the 0–0.3 m band of the 10 m tile is 0.58 filled across its silhouette span (0.17 of the bounds width); at 1.5 m there are 10–15 cm of bare stems under the dome. The requirement says "leafy to the ground". H hides 0–0.5 m in straw, so this is minor, but the fix is cheap (twigs at the lowest nodes that leave level or droop).

**Findings against F.**
- **F-1** F's shrubs under the canopy are crooked, fanning multi-stems with open, twiggy crowns and irregular dark leaf clumps. `V13_tall` is a round dome on straight parallel stilts (H-2 again).
- **F-2** F's backlight through the crowns and its dust haze belong to the light and atmosphere, not to this asset.

**Judgement on the orchestrator's scope reading.** This change builds the summer state. On that reading, the summer build meets the "Reads as a wall" scenario: opacity 0.94 / 0.92, leaf scale right, colour only judgeable at albedo until the feed renders the stage. H-1 and H-3 go to `p1-v01-locust`'s autumn task as planned. H-2 / F-1 (stem structure), H-6 (far-card value) and H-7 (low base) are look differences beyond the season. They need tasks, preferably in V01's skeleton work and before G1, but they do not block this change's scenarios.

## REAL-05 geometry audit

- **Collision overlay** (overlay tiles, both lights): the 42 capsules follow the 14 stems and the snag polylines run along them. The `twig` sphere, however, stands well outside the leaf mass, around the bare stems (R1).
- **Openings:** none. QA measured the largest clear space from any stem to its nearest neighbour, over 21 heights: 0.22 / 0.26 / 0.37 m (low / mid / tall). No opening reaches 0.5 m, so no gap is written, as the sheet says. The drone-capsule probe has nothing to fly.
- **Physical features, none texture-only.** Stems ≥ 10 mm are tapered 6-sided tubes at LOD0 (4-sided at LOD1) with capsules. Twigs of 3–8 mm are 3-sided tubes at LOD0, carried by the shell. Leaves are cards, which REAL-05 allows for foliage mass. Bark furrows are the only normal-mapped detail.
- **Collision follows the visual: not for the shell.** QA measured how far the shell's surface stands from the nearest LOD0 leaf or twig vertex, sampled over the sphere above the ground:

  | Variant | Shell centre y, radius | Shell spans y | Leaves and twigs span y | Over the top | Below the lowest leaf | Worst surface-to-crown gap |
  |---|---|---|---|---|---|---|
  | low | 0.514, 0.775 m | −0.26…1.29 | 0.01…1.21 | 0.08 m | 0.27 m (below ground) | **0.47 m** |
  | mid | 1.257, 1.107 m | 0.15…2.36 | 0.82…2.03 | 0.34 m | **0.67 m** | **0.78 m** |
  | tall | 1.875, 1.272 m | 0.60…3.15 | 1.25…3.00 | 0.14 m | **0.65 m** | **0.75 m** |

  The sheet's group rule (Geometry) is that the crown shell's outer surface follows the card envelope within 0.1 m. REAL-05 says no invisible walls. The sheet's physical role is that the drone pushes 0.2–0.5 m into a clump before the stems stop it. Against that, a drone at 0.3–0.8 m beside `V13_mid`, or at 0.6–1.2 m beside `V13_tall`, meets the 30 N/m `twig` shell up to 0.6 m before reaching anything visible: in the zone the sheet and the spec call bare, where only the stems' own capsules belong. The shell rule passes because the spec delta deliberately checks enclosure only.

## Recorded deviations, judged

- **3 193 cards on `V13_mid` against the design's 600–900 estimate: accepted.** The estimate was labelled assumed. The tuning measured why it was wrong: the band fill went from 0.52 to 0.94, and QA reproduces 0.94. The budget holds at 8 380 / 12 000. The tall variant has the least headroom, at 10 520. The card count follows `cards_per_m3`, and `check` refuses a count over budget (tested).
- **Facing-test threshold lowered from 0.8 to 0.75: accepted.** QA recomputed the test's three cameras: 0.821 / 0.801 / 0.846 of the near cards show their front. With the card normals randomly flipped (the known-wrong model) the shares are 0.499 / 0.489 / 0.499, so the test still fails it by a wide margin. The build is seeded and deterministic, so the threshold has no false-failure rate to size. The test's comment says "0.77–0.82 as tuned", slightly off what the tip measures (suggestion 6). Since the leaf shader lights back faces, facing now only buys coverage, not correctness.
- **Cards leave their twig at the fork angle instead of lying along it: accepted.** It is recorded in design decision 2 ("as tuned") and tested by `test_sprigs_leave_their_twig_at_the_fork_angle` (40–70°, both sides, 0 lays a card along its twig). The comparison sheet's before row shows the fronds it removed. At 1.5 m the result reads as a fine leafy mass.

## Hygiene

```
$ git log --format='%h %an <%ae> [%s] body=[%b]' 5fdfcfc..HEAD
bbbeed5 Asset Artist <assets@450fpv.local> [docs(openspec): record the v13 tuning and tick task 1.3] body=[]
9b0b04a Asset Artist <assets@450fpv.local> [art(vegetation): tune v13 against h and f] body=[]
4042530 Asset Artist <assets@450fpv.local> [art(materials): matte shrub leaf at smoothness 0.2] body=[]
e7688b4 Asset Artist <assets@450fpv.local> [feat(blender): shrub fork angle param and sprig cards off their twig] body=[]
480bdfc Tech Artist <techart@450fpv.local> [docs(openspec): tick p1-v13-shrub task 1.4] body=[]
b63d943 Tech Artist <techart@450fpv.local> [art(vegetation): reimport the shrub leaf on the leaf shader] body=[]
2e6af46 Tech Artist <techart@450fpv.local> [feat(importer): leaf shader and mip coverage for clipped recipes] body=[]
20c398b Tech Artist <techart@450fpv.local> [feat(shaders): add a two-sided leaf shader] body=[]
7229c18 Orchestrator <orchestrator@450fpv.local> [docs(openspec): add the leaf shading task to p1-v13-shrub] body=[]
4aaa3f5 Asset Artist <assets@450fpv.local> [docs(openspec): tick p1-v13-shrub task 1.2] body=[]
d44ee04 Asset Artist <assets@450fpv.local> [art(vegetation): build v13 low, mid and tall] body=[]
834e00a Asset Artist <assets@450fpv.local> [fix(vegetation): face leaf cards outward, far cards on sprig cores] body=[]
e78055c Asset Artist <assets@450fpv.local> [fix(assets): shell rule counts only slots with faces] body=[]
f98c6f0 Asset Artist <assets@450fpv.local> [art(vegetation): add the shrub generator and v13] body=[]
c92fd13 Asset Artist <assets@450fpv.local> [feat(assets): audit compliant shells one-sided] body=[]
a60b851 Asset Artist <assets@450fpv.local> [art(vegetation): add the skeleton and card modules] body=[]
1b89d17 Asset Artist <assets@450fpv.local> [test(importer): alpha-clipped two-sided material] body=[]
150a626 Asset Artist <assets@450fpv.local> [art(vegetation): add the shrub bark and leaf recipes] body=[]
fed2991 Asset Artist <assets@450fpv.local> [feat(importer): alpha-clipped two-sided recipes] body=[]
2d9bfe5 Asset Artist <assets@450fpv.local> [art(vegetation): pin bark_brown_02 and LeafSet002 for the shrub] body=[]
1dea903 Asset Artist <assets@450fpv.local> [feat(assets): derive the base map of an alpha-clipped recipe] body=[]
215aa19 Asset Artist <assets@450fpv.local> [feat(assets): pin the opacity map of ambientcg alpha atlases] body=[]
```
- **Commits:** 22. Every one is subject-only (empty body, 0 trailers, no co-author line), every subject is at most 72 characters, and every identity is a role from `docs/team.md`: Asset Artist for 1.1–1.3, Tech Artist for 1.4, Orchestrator for the planning edit. One logical step per commit. The asset-artist's importer and EditMode-test commits for 1.1 were assigned to it by `tasks.md`.
- **Forbidden paths:** none. The diff (85 files) has nothing under `reference/`, `Library/`, `Temp/`, `Logs/`, `UserSettings/` or `review-sheets/`, and no media outside `assets-src/` and `unity/Assets/`. The precommit scan of the tree finds 0 findings, and the change's text carries no place, date, OSD value or footage name (letters and still numbers only).
- **Licences (D-003, SEC-04):** Poly Haven `bark_brown_02` at 1k (4 maps) and ambientCG `LeafSet002` at 2k (5 maps; the zip pinned by its own SHA-256 and each map by its own) are both CC0, with author, page, URL and SHA-256 in `tools/assets/manifest.json`. `CREDITS.md` is regenerated with both rows, and `fetch.py --verify` OK. The derived maps live under `Art/Materials/` with recipe stamps, never under `ThirdParty/`.
- **LFS:** all 22 LFS objects are real content (`*`). The change's 15 LFS files (12 PNG, 3 FBX) are 0.13–21.9 MB each, none a pointer.
- **`.meta`:** every added file and folder under `unity/Assets` has its `.meta`, and there are no orphan `.meta` files (QA script over `git diff --diff-filter=A`).
- **OpenSpec:** `validate --strict` valid. Tasks 1.1, 1.2, 1.4 and 1.3 are ticked by their owners. The design records every tuning with the measurement behind it.

## Requirements

| ID | Status | Notes |
|---|---|---|
| AST-02 | **met** | §V13 is the input. Every params value traces to its range, sizes and counts sit inside it, and the colours trace to the sheet's albedo with the tints derived from measured set means |
| AST-03 | **met** | One generator plus three params files, with ranges, effects and `check` refusals (tested); one command builds, imports and captures; a rebuild is byte-identical (FBX, metadata, prefabs, review sheets). QA did not re-run the param-edit round trip in Unity; the pipeline's round trip was accepted in `setup-asset-pipeline` and nothing in this change bypasses it |
| AST-05 | **partly met** | Capsules, `bark` / `twig` ids, hooks, wind porosity by `leaf_cover`, no gaps, nothing landable and LOD distances are all in place and measured. Collision does not follow the visual at the shell (R1) |
| AST-07 | **met** for this asset | Stem UVs in metres along and around the member, upright; the bark tiles at its published 1 m (1 024 px/m); cards map atlas cells upright; no tiling visible at 10 m |
| AST-08 | **met** | Three LODs plus the cull, under 12 000 and halving; textures 2K at most. No impostor here (V01 brings it) |
| AST-09 | **partly met** | Belts can now carry bushes (PR-10) in the summer state. The late-summer to autumn mix (H-1) is planned in `p1-v01-locust`, and sway is VIS-05's |
| AST-12 | **met** | Sheets at 1.5 / 10 / 50 / 200 m, the turntable and the overlay at day and overcast, read by QA; the side-by-side against H and F exists locally and was read |
| REAL-01 | **partly met** | Leaf scale, size and band opacity copy H. Stem structure (H-2, F-1), season (H-1) and far-card value (H-6) do not yet |
| REAL-03 | **met as a process** | The local sheet exists and was judged by eye. Its differences are listed above (H-1…H-7, F-1, F-2), and each needs a task, fixed or explained |
| REAL-05 | **partly met** | Stems and twigs are real geometry, stems carry capsules, leaves are cards, and no physical feature is texture-only. The shell is an invisible contact volume up to 0.78 m off the visual (R1) |
| SEC-04 | **met** | Both sets are CC0, pinned and credited, and `--verify` is OK |

## Required changes

**R1. The `twig` shell must follow the leaf and twig mass, not bound it with one sphere.**
- **What is wrong.** Measured, the shell's surface stands up to 0.47 / 0.78 / 0.75 m from the nearest leaf or twig (low / mid / tall). It reaches 0.67 m (mid) and 0.65 m (tall) below the lowest leaf, into the bare-stem zone. Against it stand:
  - the sheet's group rule: the crown shell's outer surface follows the card envelope within 0.1 m
  - REAL-05: no invisible walls
  - the sheet's physical role: the drone pushes 0.2–0.5 m into a clump
- **Accepted when** each variant's shell is made of pieces that follow the leafy envelope, and the bare-stem zone is left to the capsules. Design decision 4 already allowed "a sphere per leafy band", and the sheet's tree outputs say "convex pieces".
- **The audit.** The shell rule also measures the other direction: it prints, for each shell, the largest distance from its surface to its slots' vertices, and fails beyond the sheet's 0.1 m or a tolerance the physics engineer sets and the design labels. This means amending the asset-pipeline delta's "SHALL NOT require the shell's surface to lie near the mesh" and adding a scenario and a test: a sphere standing 0.2 m clear of the cards fails.
- **Who.** asset-artist for the generator and the audit, orchestrator for the spec delta.
- **The alternative.** If the orchestrator decides that a bounding shell is wanted after all, record a `docs/decisions.md` entry superseding the sheet's 0.1 m rule, with the physics engineer's reading of what a 0.6 m soft margin round bare stems does to the drone. The proposal names the physics engineer as reviewer of the shell and hook metadata, and that reading is not recorded anywhere in the change.

## Findings on the pipeline scenarios (non-blocking)

- **P-1 The back-face metric passes the known-wrong model.** In QA's setup, URP/Lit with cull off, the bug the leaf shader fixes, gives a back / front luminance of 1.069, inside the 15 % bound, because its grey reflection sheen happens to be as bright as the lit front. Only the scenario's words "not the pale sky sheen" catch it. A per-channel or chroma criterion would make the scenario fail the wrong model; for example, each sRGB channel within 15 %: the leaf shader's back is (83, 86, 60) against (89, 93, 66), URP/Lit's is (92, 95, 93). QA also measures 0.863, not task 1.4's 0.999, at a thin 1.3-point margin. The difference is the direct term only: with the sun off, front and back match at 0.998. The tech-artist should write down the measuring setup, so the number can be reproduced and the 14 % explained.
- **P-2 The coverage metric passes the known-wrong model.** With coverage preservation off, the 64 px mip still holds 20.61 % against 20.35 %, inside the 10 % bound. Preservation matters only at 8 px and below (0 % at 4 px without it), which is where a 0.2 m card at 50 m and beyond samples (about 3.5 px tall, mip 7–8). The importer flag is set and tested, and the 50 m tile is filled (0.92), so the feature works. The scenario's 64 px figure just does not show it. Name the 8 px or 4 px mip, or "every mip down to 4 px", in the scenario.

## Suggestions (non-blocking)

1. **Stem structure for the shared skeleton (H-2, F-1).** Give stems lean and spread (a fanning base, not a parallel bundle), mixed base diameters within the sheet's 10–30 mm, a stem-level fork or two, and Leonardo's rule at nodes. The sheet makes Leonardo's rule a group rule and `lib/skeleton.py` does not apply it yet. V01 grows this module, so the fix lands once. Owner: asset-artist.
2. **Far cards' value (H-6).** Match LOD2's crossed cards to LOD1's mean under both lights. A darker tint or occlusion on the far cards would do it. Owner: asset-artist with the tech-artist.
3. **Low base (H-7).** Twigs at the lowest nodes leave level or droop, so the low variant is leafy to the ground as its requirement says.
4. **Snag records against the sheet.** The sheet's shared rule makes every segment under 30 mm an `edge` and every free tip a `hook`. The build writes one `hook` per stem (design 4, the spec). Have the physics engineer confirm, with R1's reading, that a base-to-tip hook stands for the 18 mm stems' edges too.
5. **Belt check (H-5).** In the first belt view, check that clumps 1–4 m apart make H's continuous, ragged wall rather than a row of domes.
6. **The facing test's comment** says "0.77–0.82 as tuned". The tip measures 0.80–0.85. Update the comment.
7. **Scenario title.** "Four LODs under budget" describes three LODs plus the cull. Rename it at the archive sync, or say "LODs and cull".

Verdict: CHANGES REQUESTED

- R1 — the `twig` shell follows the leaf and twig mass within the sheet's 0.1 m (or a physics-set, labelled tolerance), the shell rule checks the surface-to-mass direction with a failing test, and the asset-pipeline delta is amended to match; or a recorded decision supersedes the 0.1 m rule with the physics engineer's reading.

Every other scenario passes, re-run by QA with its own inputs: the one-command build twice, sizes, wall opacity and leaf scale, capsules, hooks, wind and gaps, LODs and budget, the leaf material, back faces, coverage, and the shell rule both ways. Task 2.1 stays open until R1 is resolved and re-reviewed.
