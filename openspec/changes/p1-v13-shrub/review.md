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

## Re-review (after 1.5 and 1.6)

**Reviewer:** qa-engineer · **Tip reviewed:** `731e86a` · **Rework range:** `a3120e9..731e86a` (10 commits, 21 files) on top of the first pass's `5fdfcfc..bbbeed5`
**Judged against:** R1, P-1 and P-2 above; `review-physics.md` (§2 tolerance, §3 pieces, §4 records, §5 wind, PR1–PR5, S1–S2); design decision 7 and its "as built"; the amended spec deltas.

Every check was run again by QA in its own worktree: `git lfs checkout` (22 objects, all `*`), a cold `unity/Library`, Unity 6000.3.26f1 with Unity Hub running, Blender 5.2.2, every Unity call piped so the exit codes are real. The shell, the records, the wind box, the band fill, the back faces and the mip coverage were measured with QA's own scripts, kept in QA's scratch folder (deleted afterwards). QA's standoff does not reuse `lib/audit.py`: it samples each piece's surface every 2 cm, keeps the points above the ground and outside the other pieces, measures the distance to the nearest LOD0 leaf or twig triangle through its own grid, then hill-climbs from the best samples. The meshes come from the generator, and the second build below shows they equal the committed FBX. **Power:** the laptop ran on battery (`Win32_Battery` status 1, discharging), Balanced scheme. No performance claim is made, and the times below are not evidence. The reference material was not reopened: no render input changed (see the scenario table).

### Checks

```
$ python -m pytest tools -q                       -> 1 failed, 594 passed in 604.16s (0:10:04)   exit=1
  FAILED tools/hygiene/tests/test_precommit.py::test_200_files_scan_in_under_2_s   (scan 2.094, 2.342, 3.276 s)
$ python -m pytest tools/hygiene/tests/test_precommit.py::test_200_files_scan_in_under_2_s -q -s   (alone, three times)
  scan 3.012, 3.770, 2.485 s -> failed;  scan 1.655, 2.076, 1.594 s -> failed;  scan 1.623, 2.040, 1.710 s -> failed
  The test builds its own 200 synthetic files; this change touches nothing under tools/hygiene or .githooks
  (git diff --name-only 5fdfcfc..HEAD -- tools/hygiene .githooks: 0 files). It passed in the first pass on the same
  code; here it runs on battery at 2.04-2.08 s against 2.0 s. Not a finding against this change (N-4).
$ python -m pytest tools/blender/tests/test_audit.py tools/blender/tests/test_shell.py tools/blender/tests/test_shrub.py -v
  68 passed in 219.40s, among them:
  test_two_overlapping_spheres_hugging_the_mass_pass_and_print_both_ways PASSED
  test_a_piece_30cm_too_large_fails_its_standoff PASSED
  test_a_twig_vertex_15cm_outside_fails_naming_the_slot_the_distance_and_the_tolerance PASSED
  test_a_card_corner_35cm_outside_fails_and_25cm_passes[0.35-True] PASSED / [0.25-False] PASSED
  test_surface_under_the_ground_is_ignored[0.0] PASSED / [0.5] PASSED
  test_a_shell_without_a_slot_and_a_slot_without_a_shell_are_refused PASSED
  test_the_distance_transform_matches_brute_force PASSED; test_no_faces_of_the_slots_no_pieces PASSED
  test_leaf_masses_round_a_twig_are_fitted_with_pieces_that_pass_the_shell_rule PASSED
  test_metadata_carries_capsules_and_a_shell_of_pieces[V13_low|mid|tall] PASSED
  test_stem_records_run_along_the_capsules_from_the_ground_to_the_tip[V13_low|mid|tall] PASSED
  test_twig_records_follow_the_tubes[V13_low|mid|tall] PASSED
  test_wind_box_spans_the_leafy_mass_from_its_lowest_leaf_or_twig[V13_low|mid|tall] PASSED
  test_low_variant_passes_the_collision_audit PASSED; test_the_first_bounding_sphere_fails_its_standoff PASSED
$ python tools/assets/fetch.py --verify           -> OK   exit=0
$ python tools/hygiene/precommit.py --tree HEAD   -> 510 files scanned, 0 findings   exit=0
$ openspec validate p1-v13-shrub --strict         -> Change 'p1-v13-shrub' is valid   exit=0

$ & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile - | Out-Host
  exit=0 (cold Library), "Test run completed. Exiting with code 0 (Ok)"
  unity/Logs/editmode.xml: result=Passed total=20 passed=20 failed=0 skipped=0 inconclusive=0 (the same 20 as the first pass)
  git status after the run: clean

$ pwsh tools/build_asset.ps1 V13      (run 1)   -> build_asset: OK (V13)   exit=0
  V13_low:  audit twig shell: 6 pieces (6 capsules)
  V13_low:  audit collision[42..47] capsule (twig shell): largest standoff 89.649 / 97.118 / 94.432 / 96.150 / 97.220 / 91.260 mm, tolerance 100 mm
  V13_low:  audit slot leaf (cards, twig shell): largest outside distance 268.672 mm at (0.0959, 0.1741, 0.6917) over 7352 vertices, allowance 300 mm
  V13_low:  audit slot twig (geometry, twig shell): largest outside distance 94.867 mm at (-0.276, 0.6845, -0.4331) over 1110 vertices, tolerance 100 mm
  V13_mid:  audit twig shell: 15 pieces (15 capsules); largest standoff per piece 71.651-97.750 mm (collision[42] 97.750 at (0.0998, 1.2692, 0.2578))
  V13_mid:  audit slot leaf (cards, twig shell): largest outside distance 275.507 mm at (-0.0865, 1.5096, 0.9675) over 12772 vertices, allowance 300 mm
  V13_mid:  audit slot twig (geometry, twig shell): largest outside distance 97.836 mm at (0.2701, 0.8899, 0.041) over 930 vertices, tolerance 100 mm
  V13_tall: audit twig shell: 24 pieces (23 capsules, 1 sphere); largest standoff per piece 72.483-97.461 mm (collision[53] 97.461 at (0.2202, 1.3043, 0.109))
  V13_tall: audit slot leaf (cards, twig shell): largest outside distance 282.587 mm at (0.6282, 1.5809, -0.9737) over 15708 vertices, allowance 300 mm
  V13_tall: audit slot twig (geometry, twig shell): largest outside distance 99.679 mm at (0.7103, 2.2, 0.7118) over 1602 vertices, tolerance 100 mm
  capsule->mesh, 42 lines per variant (min / max): low 5.660 / 11.372, mid 6.403 / 11.371, tall 9.948 / 12.541 mm, the first pass's numbers
  mesh->collision: 5.715 / 8.238 / 11.698 mm, the first pass's numbers
  tris 5850 / 2202 / 6, 8380 / 3558 / 6, 10520 / 4292 / 6; sizes [1.5439, 1.2066, 1.4422], [2.0933, 2.0277, 2.0933], [2.4864, 3.004, 2.4313] m
  [CatalogImporter] V13_low 48 colliders, V13_mid 57, V13_tall 66; LOD heights as in the first pass
  [Review] six sheets, 4096x4096, GPU NVIDIA GeForce RTX 4050 Laptop GPU (Direct3D12), High (URP-High), LOD BIAS 1, Linear
  git status --short --untracked-files=all: 0 lines
$ pwsh tools/build_asset.ps1 V13      (run 2)   -> build_asset: OK (V13)   exit=0
  all 186 variant lines identical to run 1; the six review sheets byte-identical (SHA-256); git status: 0 lines
$ pwsh tools/build_asset.ps1 K01                -> build_asset: OK (K01)   exit=0
  K01_whole: box->mesh 3.288 mm, mesh->collision 1.869 mm, tris 44 / 12; git status: 0 lines
```

### R1: closed

**What changed** (`git diff a3120e9..HEAD`): the generator replaces the bounding sphere with `shell.fit` (`tools/blender/generators/shrub.py:159`), adds per-stem and per-twig records (`:173`, `:177-189`) and builds the wind box from the leaf and twig vertices (`:160`). The audit holds the shell both ways (`tools/blender/lib/audit.py:111-166`, tolerances at `:40-41`), telling cards from geometry by `catalog.card_visuals` (`lib/catalog.py:51`, passed in by `build.py:44`). In the committed metadata only `collision`, `snag` and `wind_volumes` changed. `bounds`, `lods`, `materials`, `gaps` and `params_sha256` are unchanged, the 42 bark capsules are identical, and no FBX, params file, recipe, texture, shader or importer file changed since the look task (`git diff --name-only bbbeed5..HEAD` over those paths is empty).

**Measured by QA, against `review-physics.md` §2–§5 and the amended scenario** (`specs/assets-vegetation/spec.md:24`):

| | V13_low | V13_mid | V13_tall | Limit |
|---|---|---|---|---|
| Pieces (kinds) | 6 (6 capsules) | 15 (15 capsules) | 24 (23 capsules, 1 sphere) | ≤ 24, spheres or capsules, `twig`, not landable |
| Piece radii | 84–307 mm | 43–313 mm | 46–326 mm | |
| Worst standoff (a), QA sampler / build audit | 97.45 / 97.22 mm | 97.80 / 97.75 mm | 97.50 / 97.46 mm | 100 mm |
| Twig vertices outside (b), QA / audit | 94.87 / 94.87 mm | 97.84 / 97.84 mm | 99.68 / 99.68 mm | 100 mm |
| Leaf vertices outside (c), QA / audit | 268.67 / 268.67 mm | 275.51 / 275.51 mm | 282.59 / 282.59 mm | 300 mm |
| Lowest piece surface; lowest leaf or twig | 0.041; 0.008 m | 0.892; 0.816 m | 1.267; 1.251 m | no piece more than 0.1 m below the mass |
| Records (stem `edge` / stem `hook` / twig `edge` / twig `hook`) | 28 / 14 / 62 / 123 = 227 | 28 / 14 / 52 / 103 = 197 | 28 / 14 / 89 / 178 = 309 | 227 / 197 / 309 |
| Wind box y; porosity | 0.0076–1.2066 m; 0.35 | 0.8159–2.0277 m; 0.35 | 1.2506–3.0040 m; 0.35 | from the lowest leaf or twig |

- **Standoff (a).** Over all 45 pieces, QA's value minus the audit's runs from −0.69 mm to +0.28 mm. Where QA is lower, the sampler missed the true peak, so it is a lower bound. Where QA is higher, it is inside the audit's stated 0.5 mm precision (`SHELL_PRECISION_M`). The worst exposed point of every variant stands 2.2–2.8 mm inside the tolerance.
- **Bare-stem zone.** No piece reaches below its lowest leaf or twig. Read in QA's own orthographic plots (front and side, pieces as stadiums over the leaf and twig vertices) and in the build's overlay tiles (both lights): the pieces sit on the leaf mass, and the stems below 0.82 m (mid) and 1.25 m (tall) carry only their capsules and stem records. On `V13_low`, which is leafy to the ground, a capsule of 84 mm radius runs down among the stems to about 0.2 m. Its worst point (y 0.19 m) stands 96.2 mm off a face, because the low cards reach 0.01 m, so it is not an invisible wall.
- **Records.** QA compared every record with the skeleton (generator spy) and the committed capsules.
  - Each stem has `edge`, `edge`, `hook` on its capsules' chords (end points within 2 µm), at the capsules' radii (8.333 / 7.0 / 5.667 mm). The chain starts at y 0, runs continuously and ends at the stem tip.
  - Each twig has one record from `p0` to `p1` at (r0 + r1)/2 (3.25 mm first order, 2.0 mm second order). It is an `edge` exactly when another twig starts at its end (QA: distance < 1 nm). The single first-order hook on low is the twig with no children.
  - The prefabs carry the same colliders: low 48 capsules, mid 57 capsules, tall 65 capsules and 1 sphere.
- **Wind box.** It spans the LOD0 leaf and twig vertices to within 1 µm on every side.

**Known-wrong models fail, on the real meshes:**
- Task 1.2's single sphere on `V13_mid`: QA's sampler measures 744.1 mm (at its bottom, y 0.15 m, among the bare stems) and fails (a). The test `test_the_first_bounding_sphere_fails_its_standoff` (`test_shrub.py:322`) passes with the audit inside 700–800 mm.
- **(a) at its threshold**, `V13_mid` collision[42], audit / QA:

  | Radius change | Audit | QA | Result |
  |---|---|---|---|
  | as built | 97.750 mm | 97.796 mm | pass |
  | +5 mm | 98.550 mm | 98.653 mm | pass |
  | +20 mm | 100.503 mm | 100.485 mm | FAIL / FAIL |

  The worst point's nearest faces lie partly outward, so the standoff grows by less than the radius. The two measures agree within 0.1 mm. In the same way, `V13_tall` collision[53] grown by 5 mm still passes the full audit.
- **(b) at its threshold.** On `V13_tall`, the piece nearest the worst twig vertex, shrunk by 1 mm, fails with `slot twig (geometry, twig shell): largest outside distance 100.679 mm at (0.7103, 2.2, 0.7118) exceeds the tolerance 100 mm`.
- **(c).** On `V13_tall`, one leaf corner moved to 310 mm outside fails with `slot leaf (cards, twig shell): largest outside distance 310.000 mm ... exceeds the allowance 300 mm`.
- **The card/geometry split matters.** With `card_visuals` empty, the committed `V13_tall` leaf slot is held to 100 mm and fails at 282.587 mm.

**Verdict on R1:** closed. The shell follows the leaf and twig mass within the physics engineer's derived 0.1 m. The bare stems carry only their capsules. The audit measures both directions, with tests and with real-data threshold behaviour. The asset-pipeline delta is amended (`specs/asset-pipeline/spec.md:20-37`). The physics reading that R1's alternative asked for is recorded, with its derivation (`review-physics.md` §2).

### P-1 and P-2, as tightened

QA's probe ran in Unity batchmode with graphics, from an editor script placed in `Assets/QaScratch/` for the run and then removed (git status 0 lines afterwards).
- **The setup.** The day profile's sun (elevation 45°, azimuth 135°, intensity 1, soft shadows) and ambient are set as the review rig sets them. The camera sits 0.5 m back along the sun's beam, with the sun behind it. The card is atlas cell 0 at 0.2 m tall (0.089 m wide). It is shown front, then turned 180° about its own vertical axis. The target is a 512² ARGBFloat linear RT with no MSAA and no post. The opaque pixels are those not on the magenta clear: 25 507.

| Model | Front, sRGB of the mean | Back, sRGB of the mean | Back / front per channel | Luminance back / front |
|---|---|---|---|---|
| `Fpv450/Leaf` (as built) | (90.0, 93.4, 66.2) | (82.5, 86.3, 60.2) | 0.917, 0.924, 0.909: **pass** (15 %) | 0.845 |
| URP/Lit, cull off (known wrong) | (90.0, 93.4, 66.2) | (91.9, 95.3, 93.2) | 1.021, 1.020, **1.408: fails** | 1.078 (would pass the old luminance metric) |
| `Fpv450/Leaf`, sun off | (48.8, 52.7, 43.0) | (48.6, 52.5, 42.7) | 0.996, 0.997, 0.994 | 0.994 |
| URP/Lit, sun off | (48.8, 52.7, 43.0) | (91.9, 95.3, 93.2) | 1.88, 1.81, 2.17 | 3.38 |

- **Results.** The mean of the per-pixel sRGB gives the same verdicts (leaf 0.915 / 0.920 / 0.906). The tightened scenario (`specs/asset-pipeline/spec.md:12`) passes with a 5.9-point margin on the weakest channel, and the known-wrong model fails it on blue. This reproduces the first pass, with 0.845 here against 0.863 there, since the card is framed larger. The table records the setup, and the roughly 15 % of luminance the back loses under the sun is still unexplained against task 1.4's 0.999 (non-blocking).
- **P-2, coverage, measured on a readable, uncompressed RGBA32 copy of the base map with the committed import settings.** The committed importer has `mipMapsPreserveCoverage` True, `alphaTestReferenceValue` 0.5, sRGB, alpha from input, box filter and max 2048. Opaque share (alpha ≥ 128) per mip:
  - **preservation on:** 2048 px 20.35 %, 64 px 20.70 %, 8 px 21.88 %, **4 px 25.00 %**, 2 px 25.00 %, 1 px 100 %
  - **preservation off:** 64 px 20.61 %, 8 px 18.75 %, **4 px 0.00 %**, 2 px 0.00 %, 1 px 0.00 %

  The tightened scenario (`:16`) passes ("every mip down to 4 px keeps opaque texels"), and the known-wrong model fails it, as the scenario says.

**P-1 and P-2: closed.** Neither has an automated test of the measured criterion (suggestion 11).

### Code

- **`lib/shell.py`** is a plain function of a mesh and its slot names, so V01 can call it per limb.
  - The mass is a 5 cm grid of cells within 85 mm of a face. The distance transform (Felzenszwalb–Huttenlocher) is tested against brute force.
  - Candidates are spheres and capsules at the transform radius. The greedy cover uses a lazy heap with a deterministic tie-break. The swap pass and the shrink pass (`_hold`) re-measure each piece with the audit's own `standoff`, and the shrink pass is conservative because no other piece hides any of it.
  - All orderings are sorted or index-keyed. The second build reproduced every line and every byte.
  - When the cap does not cover every target, `fit` returns its pieces anyway and the audit refuses the build, which is the right split: the audit is the gate.
- **The audit's rule (a)** is a sound branch and bound.
  - Each cell's bound is the lesser of two, plus the patch's sag: the distance at the nearest vertex plus the longest edge, which holds because the distance is 1-Lipschitz, and the convex distance to one candidate face at the worst vertex.
  - The radial sag (r less the centre's distance to the flat triangle) is exact. The wall sag r·Δθ²/2 is conservative.
  - The ground and the other pieces drop only cells that lie wholly beyond the sag.
  - `FaceIndex` registers each face in every cell it can meet and stops the ring search once the best distance is under the searched radius. This is correct, because a point lies at least k cells from anything outside k rings. Far points fall back to every face, which is slow but correct.
- **Card slots by `alpha_clip`** (`catalog.card_visuals`) use the same test the importer uses for clipping. A clipped recipe on a geometry slot would get the card allowance, which V01 should keep in mind.
- **`shell.py` calls the private `audit._face_distance`** (`shell.py:94`), and `ROUNDS` serves both the outer loop and the swap rounds. These are minor (suggestion 9).
- **Build-time cost.** The rebuild took 5 min 34 s and 4 min 59 s on battery. The audit alone took 41–47 s per run on `V13_tall` in QA's checks. The shrub, shell and audit tests take 3 min 39 s. This is acceptable for a three-variant asset. V01's crowns and V13's planned autumn variants will add to it.

### Findings (non-blocking)

- **N-1 `V13_tall` sits at the cap, and the planned autumn state does not fit.**
  - The build is seeded and deterministic, so the 0.32 mm twig margin has no false-failure rate. It cannot drift between runs on this machine: it is a fragility to parameter changes.
  - QA built the three variants at `leaf_cover` 0.4, the value `p1-v01-locust` task 1.4 plans for `V13_*_autumn` (design 7 there), and ran the fit and audit unchanged:
    - `V13_low`: 9 pieces, passes
    - `V13_mid`: 21 pieces, passes
    - **`V13_tall`: 24 pieces, fails.** A twig vertex lies 227.4 mm outside (tolerance 100) and a leaf vertex 341.5 mm outside (allowance 300).
  - So that task will hit `review-physics.md` §3's report-and-stop clause. The orchestrator should have the physics engineer revisit the cap, or the fitter's coverage of sparse twig mass, before `p1-v01-locust` 1.4, and before V01's crowns, which are sparser still.
- **N-2 Twig records outside the shell.** Measured by QA:

  | Variant | Twig records | With a point outside the union of the pieces | Farthest point outside |
  |---|---|---|---|
  | low | 185 | 58 | 93.8 mm |
  | mid | 155 | 83 | 95.2 mm |
  | tall | 267 | 162 | 98.8 mm |

  `review-physics.md` §4 resolves a record's material from its capsule, "else of the shell that encloses it", which leaves these without one. The phase-7 compiler should take the slot's physics material (`twig`) instead. This goes to the physics engineer (N4) and the world-builder.
- **N-3 Overlay legibility.** The shell pieces share the collision colour with the bark capsules, and 197–309 snag crosses cover the crown in the overlay tile. QA had to plot its own orthographic views to read the pieces against the bare stems.
- **N-4 The hygiene timing test** fails on battery at 2.04–3.8 s against 2.0 s. It is outside this change, and QA owns it: its 2 s bound on the maximum of three runs is not sized by a false-failure rate (handoff).

### The H findings after the rework

The rework does not touch the look: FBX, params, materials and shader are identical, and the sheets reproduce. **H-1…H-7 stand as written**, with this coverage from `p1-v01-locust` task 1.4 on `unity/main` (design 7, the "H autumn belt state" requirement):

| Finding | Status |
|---|---|
| **H-1 Season and state** | **covered**: `V13_*_autumn` at `leaf_cover` 0.4 with the autumn leaf tint, compared against H |
| **H-3 Twig mesh hidden** | **covered**: stems and twigs are kept while leaves thin out, and the "Matches H" scenario asks for dark stems and twigs showing through at 1–2 m. N-1 blocks `V13_tall_autumn` as the cap stands |
| **H-4 Colour** | **partly covered**: the autumn tints come in task 1.4. The comparison through the feed stays open (tech-artist) |
| **H-5 Domes or a wall** | **partly covered**: task 1.4 renders a row of `V13_mid_autumn` and measures the 1–2 m band for sky. Placement in the map stays the world-builder's |
| **H-2 / F-1 Stem structure** | **open**: task 1.4 keeps V13's stems unchanged, and V01's skeleton growth (its design 1) is the tree's |
| **H-6 Far-card value** | **open** |
| **H-7 Low base** | **open** |

### Spec scenarios (updated)

| # | Scenario | Result | Evidence |
|---|---|---|---|
| V13-1 | Built from params by one command | **PASS** | Runs 1 and 2: exit 0, 186 identical lines, sheets byte-identical, `git status` 0 lines |
| V13-2 | Sizes inside the sheet | **PASS** | Bounds unchanged from the first pass (metadata `bounds` identical); the tall variant's lowest leaf or twig is at 1.251 m |
| V13-3 | Reads as a wall at the belt edge | **PASS** (colour at albedo only, as before) | QA's band fill re-measured on the new `V13_mid` 10 m overcast tile, with camera (−6.64, 4.43, −6.63) as logged, rows 427–513, thresholds 8 / 12 / 20. The fill of the silhouette span is **0.934 / 0.934 / 0.931**, 0.49 of the bounds width, colour #5b603d. Day light gives 0.934. At 50 m: **0.924**. All are the first pass's values within its row rounding |
| V13-4 | Stems and shell carry the contact (amended) | **PASS** | The R1 table above: capsules ≤ 12.5 mm from their tubes, records per stem and per twig, ≤ 24 pieces within 0.1 m / 0.1 m / 0.3 m, the bare-stem zone left to the capsules, the wind box from the lowest leaf or twig at 0.35, `gaps` empty, nothing landable |
| V13-5 | Four LODs under budget | **PASS** | Unchanged: 5 850 / 8 380 / 10 520 LOD0 triangles, halving; the textures unchanged |
| AP-1 | Leaf card material | **PASS** | EditMode `LeafRecipe_ClipsAtHalfAndCullsNoFace`; the probe reads `Fpv450/Leaf`, cull 0, cutoff 0.5 |
| AP-2 | Back faces lit as leaves (tightened) | **PASS** | Per channel 0.917 / 0.924 / 0.909; URP/Lit with cull off fails at 1.408 on blue |
| AP-3 | Coverage kept at distance (tightened) | **PASS** | 4 px mip 25.00 % with preservation, 0.00 % without |
| AP-4 | Pieces hugging the mass pass | **PASS** | `test_two_overlapping_spheres_hugging_the_mass_pass_and_print_both_ways`; all three real variants pass with every piece and slot printed |
| AP-5 | Shell too large for its mass | **PASS** | `test_a_piece_30cm_too_large_fails_its_standoff` (380 mm named); the real `V13_mid` piece +20 mm fails at 100.503 mm; the first sphere fails at 700–800 mm (QA: 744 mm) |
| AP-6 | Twig outside the shell | **PASS** | `test_a_twig_vertex_15cm_outside_fails_...` (150.000 mm named); real `V13_tall` fails at 100.679 mm with one piece 1 mm smaller |
| AP-7 | Card outside the shell | **PASS** | `test_a_card_corner_35cm_outside_fails_and_25cm_passes`; the real `V13_tall` corner at 310 mm fails, as built 282.587 mm passes |

### Requirements (updated)

| ID | Status | Notes |
|---|---|---|
| AST-02 | **met** | Unchanged |
| AST-03 | **met** | Unchanged; the rebuild is byte-identical with the shell fit in the loop |
| AST-05 | **met** | The shell follows the visual within the derived 0.1 m. A compliant 30 N/m shell is held to 0.1 m, not the ±5 cm of rigid shapes, on `review-physics.md` §2's derivation (3 N, inside the stiffness's own spread) and the sheet's group rule. Capsules are within 12.5 mm. Records follow the sheet's rule. The wind volume covers the leafy mass only |
| AST-07 | **met** | Unchanged |
| AST-08 | **met** | Unchanged |
| AST-09 | **partly met** | Summer state; the autumn state is planned in `p1-v01-locust` 1.4, and N-1 blocks its tall variant |
| AST-12 | **met** | Sheets read again, including the overlay tiles; QA's own orthographic overlays added |
| REAL-01 | **partly met** | Unchanged: H-2, H-6 and H-7 are open, and the season goes to `p1-v01-locust` |
| REAL-03 | **met as a process** | Unchanged |
| REAL-05 | **met** for this asset | No invisible contact volume beyond 0.1 m of a leaf or twig face; the bare stems are capsules only; no openings; stems and twigs are geometry with capsules or records |
| SEC-04 | **met** | No pin changed; `--verify` is OK |

### Hygiene (rework)

```
$ git log --format="%h|%an <%ae>|%cn <%ce>|%s|%b|%(trailers:only)" a3120e9..HEAD
731e86a Orchestrator <orchestrator@450fpv.local>   docs(openspec): bring the v13 proposal in line with the shell as built (70)
98a5cfa Asset Artist <assets@450fpv.local>         docs(openspec): record the v13 shell as built and tick task 1.6 (63)
f7b5732 Asset Artist <assets@450fpv.local>         art(vegetation): rebuild v13 with shell pieces, records and wind box (68)
defc9cf Asset Artist <assets@450fpv.local>         art(vegetation): v13 wind box over the leafy mass only (54)
2f2d490 Asset Artist <assets@450fpv.local>         art(vegetation): v13 stem and twig snag records (47)
8893fe8 Asset Artist <assets@450fpv.local>         feat(assets): fit crown shells as pieces on the leaf mass (57)
126f52c Asset Artist <assets@450fpv.local>         feat(assets): audit shells both ways with a card allowance (58)
06fbdc9 Orchestrator <orchestrator@450fpv.local>   docs(openspec): carry the v13 physics reading into the spec (59)
aaff29d Physics Engineer <physics@450fpv.local>    docs(physics): read the v13 shell, hook and snag metadata (57)
abd2e6c Orchestrator <orchestrator@450fpv.local>   docs(openspec): answer the v13 qa pass with a physics reading and r1 (68)
  every body and trailer list empty; author = committer; subject lengths in brackets (≤ 72)
```
- **Commits and files.** The identities are the roles from `docs/team.md`, and there is one logical step per commit. The 21 files lie under `tools/blender/`, `openspec/changes/p1-v13-shrub/`, the three V13 metadata files and the three V13 prefabs. Nothing comes from `reference/`, `Library/`, `Temp/`, `Logs/`, `UserSettings/` or `review-sheets/`, and no media changed. The OPSEC scan finds 0, and the new text names letters only.
- **No new third-party file.** `tools/assets/manifest.json` and `CREDITS.md` are untouched.

### Suggestions (non-blocking)

Carried from the first pass:
- 1 (stem structure)
- 2 (H-6)
- 3 (H-7)
- 5 (belt check, now partly in `p1-v01-locust` 1.4)
- 6: the facing test's comment still says "0.77–0.82 as tuned", at `test_shrub.py:278`
- 7: the scenario title still says "Four LODs"

Suggestion 4 is resolved by PR3 and PR4.

New:
8. **Requirement text carries review history.** The asset-pipeline requirement's last sentence ("This relaxes QA's first scenario …") is review history. Move it to the design at the archive sync, so the main spec states the rule only.
9. **Make `audit._face_distance` public,** or have `shell.py` use `FaceIndex`, and split `ROUNDS` into its two uses.
10. **Give shell pieces their own overlay colour** in the review capture, and draw the twig records thinner or under a toggle (N-3).
11. **Add EditMode checks for P-1 and P-2's measured criteria** (tech-artist). For P-2, assert the base map's 4 px mip keeps opaque texels; it is cheap. For P-1, a per-channel back/front check on one card under the day light.
12. **Before `p1-v01-locust` 1.4,** the physics engineer revisits the 24-piece cap, or the asset-artist adds fringe capsules along sparse twigs to the fitter, with `V13_tall` at `leaf_cover` 0.4 as the failing case (N-1).

Verdict: APPROVED

R1 is closed as `review-physics.md` and the amended deltas define it, and P-1 and P-2 are closed. Everything the first pass verified still holds, re-run by QA: the builds, sizes, wall reading, leaf material, budgets, FBX and LODs, K01 and hygiene. N-1 does not block this change, which builds the summer state, but it must be planned before `p1-v01-locust` task 1.4.
