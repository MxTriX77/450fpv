# Review: p1-v01-locust (task 2.1)

**Reviewer:** qa-engineer, fresh (did none of the work, D-034) · **Branch:** `unity/art/p1-v01-locust` · **Tip:** `45c5626` · **Range:** `18201d6..45c5626` (118 commits not on `unity/main`, 7 of them merges; 247 files)
**Requirements cited:** AST-02, AST-03, AST-05, AST-06, AST-07, AST-08, AST-09, AST-12, REAL-01, REAL-03, REAL-05, SEC-04 · **Decisions:** D-003, D-004, D-020, D-028, D-038
**Read:**
- `CLAUDE.md`, `AGENTS.md`, `docs/workflow.md`, `docs/team.md`, the `karpathy-guidelines` skill
- the whole change folder: the proposal, design 1–9 with every "as built" and "measured" note, `review-physics.md` with "After task 1.1", tasks 1.0–1.7 as built, both spec deltas
- `docs/object-sheets/vegetation.md`: the group rules, the tree table, §V01, §V13 and the reference-view rows of F, H and G
- `docs/requirements.md` for every cited ID, and D-038
- the E01 review, for the format
- the 1.7 merge `0fc2889` against both parents and against git's own automatic merge
- the code each check rests on: `lib/{geometry,export,audit,meta,impostor}.py`, `generators/tree.py` (`_records`), `CatalogImporter.EnsureLods`, `Review.Rig.Render`, `LodParity`'s judge, and the tests behind each scenario

**How QA ran it.** Every check below ran in QA's own worktree, after `git lfs checkout` (111/111 objects), with no `unity/Library/` before the first Unity run. Nothing from a handoff was reused.
- **QA's own scripts** (scratchpad, never committed, plain Python, no code shared with `tools/blender`):
  - `qa_meta.py` reads the shipped metadata only: sizes, trunk diameters, forks, colliders, rule (d) recounted exactly, every gap's own inscribed circle and clear space, wind overlaps, snag records
  - `qa_hooks.py`: which bark hooks have a free tip
  - `qa_rerecord.py`: V13's re-record, key by key
  - `qa_parity_z.py`: the parity margins in standard errors
  - `qa_png.py`: texture sizes
- **The look:** QA read the contact sheets of F, H and G and the two comparison sheets already in `reference/_compare/`. QA's crops of them went to a folder of its own under `reference/_compare/`, deleted after. QA opened no clip, no index and no letter list.
- **Tools:** Unity 6000.3.26f1 (Hub running, every call piped), Blender 5.2, Python 3.14 with Pillow, pwsh 7.
- **Power and GPU:** every Blender and Unity run on AC (`BatteryStatus.PowerOnline` True before each, and at both ends of each parity run; Balanced plan), on the RTX 4050 (`Renderer: NVIDIA GeForce RTX 4050 Laptop GPU` in every Unity log). Another agent's Blender ran beside some of these runs, always with 6 GB or more free.
- **No performance claim is made.**

## Verdict: CHANGES REQUESTED

Five blocking items:
- **R1. The M and L locusts do not read as F's and H's** (REAL-01, REAL-03, D-038, the V01 requirement, AST-09). Each is a young tree of four long, nearly straight limbs with a few side branches and sparse leaf tufts. F's crowns are dense, scraggly nets of fine branches and zigzag twigs. Every automated number passes: the chequerboard failure mode again.
- **R2. The H belt edge does not read as H** (scenario "Matches H"). It is an even olive-yellow leaf band, closed from about 1.3 m up, over thin parallel stems. H's wall is a picket of thin dark stems with sparse, mostly green leaves.
- **R3. The V01 requirement still states a 0.35 m card allowance.** The build uses 0.361 m, and V01_L's leaves stand 358.3 mm out of the shell. This is the edit review-physics §A.6 item 11 asked QA to confirm, made in one delta only.
- **R4. The orphan path has no test of its own.** Review-physics §A.1 accepted it on that condition, and §A.6 item 8 asks QA to request the test if it is missing.
- **R5. Each trunk's top segment is written as a `hook`,** though the limbs leave at its tip. The sheet's rule is "every free tip a hook".

Everything else passes:
- two builds each of V13 and V01, byte-identical, `git status` clean after each; all 26 review sheets byte-identical between the two builds
- the twelve V01 impostor atlases re-rendered from scratch byte for byte
- K01, S04, L01, L03 and E01 rebuilt with no diff
- the parity measure: V01 0 of 44 and V13 0 of 42 judged rows outside the limits, every row at least 4.4 standard errors inside its limit
- QA's own metadata audit: sizes, trunks, forks, the capsule count, rule (d), gaps, wind, snag materials
- V13's re-record: only `material` added
- the 1.7 merge
- budgets, the D-003 pins (matched against Poly Haven's own hashes), the OPSEC scan, `openspec validate`, the commit hygiene
- EditMode 95 of 95; `pytest` 987 passed, both `test_existing_tree_is_clean` targets green

Task 2.1 stays unticked.

## Blocking items

### R1. The M and L locusts do not read as the belt locusts of F and H
**What QA saw:**
- **The letters.** F 1–12 and H 1–21, from the contact sheets, read for their look and feel at the pilot's 0.5–2 m.
  - F's locusts are scraggly crowns built from a dense net of fine branches and zigzag twigs over the whole crown, crossing one another, with dark leaf tufts in clusters along them. The sky reads through a fine mesh.
  - F's trunks are thin, often two to four from one base, and curve and lean.
  - H's belt canopy is dense and green over many thin trunks.
- **The renders.** QA's own review sheets of V01_M and V01_L (1.5 m, 10 m, the turntables, by day and overcast), the artist's `reference/_compare/V01.png` (rows of three at pilot height, the H row of five V01_L, the 200 m row), and the H sheet's V01_M_autumn rows.
  - Each M and L tree is a straight trunk to a fork at 3 m (M) or 4 m (L), then four long, nearly straight limbs in a vase.
  - The limbs carry two or three side branches each. The twigs are hairlines that do not read as a net at 1.5 m or 10 m. The leaf sprigs are spaced along the limbs.
  - The sky reads through the crown, but through empty space between a few limbs, not through a twig net. The overall read is a young planted sapling.
  - At 50 m the crown is wiry. At 200 m the impostor is a sparse speckle of light green-grey texels, not "a dark green-grey mass".
- **V01_S is the exception.** Its two stems with tufts at their tips read close to G's openwork strip. G's crowns spread wider and hold their tufts level; task 1.3 lists that.

**Why it fails:**
- **The V01 requirement:** the prefabs "copy the belt locust of letters F, H and G", with "an open scraggly crown the sky reads through".
- **AST-09:** "fine twig crowns that read at distance".
- **D-038:** the view must feel like the reference, not only contain its parts.
- **The numbers pass with the wrong shapes:** 64 % sky within the crowns' span, sizes, forks and widths all inside the sheet, every parity row in. That is this change's chequerboard.
- **The artist's own list** (task 1.3, "not matched"): F's dense network of fine twigs, the long bare limbs with few side branches, G's level tufts.

**The causes, as built** (design 3 "The look in task 1.3"):
1. **The branching rule.** `lib/skeleton.py` gives every member 2–3 children whatever its length. F branches every few decimetres.
2. **Rule (d).** Every bare twig is a rule (b) target. Task 1.3 tried `orders` 6 with `children_per_order` 3 (1 056 twigs on V01_M): every limb failed at 64 pieces and split, and (d) rose to 68 at the committed cover and 89 at 0.3.

**To close it** (the orchestrator first, then the asset-artist with a physics ruling; QA judges again on the same sheets and on a pilot-height corridor view like F's):
- **The physics engineer rules how a twig net of F's density is carried.** The options are already on record:
  - per-member contact through the records (N-a), which the reading put in phase 7
  - rule (b) relaxed for a twig's free outer part, beyond the cantilever bound of §3.3
  - the sheet's own geometry rule: 3–10 mm twigs as cards inside the crown, their contact the shell's and their snag records on their axes (vegetation sheet, "Geometry"). This conflicts with REAL-05's "no card fakes for anything with a physical role", so it needs the ruling.
  - (d) raised only after the `veg_shells` bench (§A.3, N-j)
- **The asset-artist then grows the crown F shows:**
  - side branches along the long members every few decimetres, not 2–3 per member
  - the zigzag twig net over the whole crown
  - curving, leaning trunks, and F's two- to four-stem bases where the sheet allows them (1–4 stems per base; `trunk_sinuosity` is 0.6, and the trunks still read straight)
  - re-measured: parity, rule (d) at the committed cover and at 0.3
- **The minimum QA will accept for G1,** if the orchestrator rules that F's full density waits for phase 7: from F's corridor view and the 10 m tile, the crown reads as a scraggly net of branches, not as four poles with tufts. The orchestrator records the remaining gap as a G1 stand-in, so the pilot sees it named.

### R2. The H belt edge does not read as H
**What QA saw:**
- **H 1–21, from the contact sheets:**
  - the wall is a dense picket of thin, dark, mostly vertical stems from the ground into the canopy, and the bush layer runs into the trees with no line between them
  - the foliage is mostly green, with a few yellow and rust leaves
  - under overcast the stems read darker than the leaves
- **The artist's `reference/_compare/V01-H.png`** (task 1.5's build), at full resolution:
  - **The understory** is a continuous olive-yellow leaf band with an even, rounded top at 1.8–2 m, closed from about 1.3 m up. Thin, light, parallel stems stand only below it.
  - **The trees behind** are a regular comb of identical V-shaped forks (R1).
  - **H 7's band against the render's band** shows it most plainly: stems with sparse leaves against a leaf mass on stilts.
- **QA's own V13 sheets** show the same clump form: a leafy dome over a bare fan of stems. Since task 1.5, tasks 1.6–1.7 changed only which cards V13's LOD1 and V01's LOD1 and LOD2 keep, the impostor's smoothness and the metadata's `thin_wire` flags. The structure R2 is about rests on none of them.

**Why it fails, clause by clause of the scenario:**
- **"the understory reads as a twiggy, part-bare wall with stems showing through": not met.** The band is leaves with the stems below it. Task 1.5's own note says so: "ours is a leafy wall from about 1.3 m up in which the stems show only in the lower half".
- **"the foliage mixes green and yellow as H does": partly met.**
  - It mixes, but leans yellow-olive where H leans green.
  - By task 1.5's own measure, the canopy's r/g is 1.008 against H's 0.67–0.96: outside H.
  - The band's r/g of 1.026 was matched against H's band "with its straw" (task 1.4: "the straw in front included"). That compares the leaves with the straw.
- **"the 1–2 m band shows no sky": not met as measured.** Task 1.5 reads 3.60 % open at 1.8–2.0 m (QA did not repeat it). The rays pass over the 2 m clumps' tops, and through the trunks to the scene's open back past 12 m. Part of that is staging: the scene is two rows deep where H's belt goes on.

**To close it** (asset-artist; QA judges a fresh H sheet by eye, H 1–21):
- **The 1–2 m band as H's:** many thin stems showing through sparse leaves, not a closed leaf mass over bare stems.
  - Leaves along the stems through the band, not only at the twig tips.
  - The stems' colour checked against H: the sheet's #7a7466 is assumed, and reads light against H's dark stems.
  - If this asks for V13 cover under 0.3, report and stop (PR9, N-h).
- **The foliage hue measured on the leaves alone,** against H's foliage, not against a band that includes the straw.
- **The belt staged deep enough** that the band's sky is the assets', not the scene's back: a third row, or the belt's own depth. The band measured again. Or the orchestrator amends the scenario to the staging it can measure.
- **R1's crowns** for the trees behind.

### R3. The V01 requirement states 0.35 m where the build uses 0.361 m
**Measured** (QA's build log; the same in both builds):
```
V01_L: audit slot leaf (cards, twig shell): largest outside distance 358.277 mm at (0.9481, 7.6002, -2.9228) over 3724 vertices, allowance 361.026 mm
V01_M: audit slot leaf (cards, twig shell): largest outside distance 338.177 mm ..., allowance 361.026 mm
```
**Why it fails:**
- **The assets-vegetation delta,** V01 requirement: "leaf and pod cards within 0.35 m". V01_L stands 8.3 mm over that, so the requirement as written is missed.
- **The asset-pipeline delta** carries the 0.361 m rule (c), and **review-physics §A.3** set it: the 0.25 m card's far corner reaches 0.261 m, plus the 0.1 m tolerance.
- **Review-physics §A.6 item 11** lists "both deltas: 0.361 m in place of the tree's 0.35 m" for QA to confirm. Only one carries it.
- **Design 4** still reads "a 0.35 m card allowance".

**To close it** (orchestrator, Quick): "within 0.35 m" becomes "within the tree's card allowance, 0.361 m (the card's far corner plus 0.1 m)" in the V01 requirement and in design 4, then `openspec validate p1-v01-locust --strict`.

### R4. The orphan path has no test of its own
**What QA found:**
- `lib/shell.py` builds orphan candidates: `_Cover.orphans`, `mass.on`, `ORPHAN_SPAN_M`.
- No test names or exercises them. `grep -i orphan tools/blender/tests` finds nothing.
- `test_shell.py` holds six tests: the distance transform, empty slots, a leaf mass round a twig, the cap escalation, the 64 cap and the 16-round retry. None places a twig that no lattice candidate reaches.
- The half "with no orphan the candidates are the lattice's" is covered only indirectly, by V13's shells coming back piece for piece (`test_shrub`).

**Why it fails:** review-physics §A.1 rules "accepted as built … QA confirms that the orphan path has a test of its own (§A.6, item 8)", and item 8 reads "If none exists, request it". The orphans carry the only force a bare twig gives (§A.1).

**To close it** (asset-artist, in `test_shell.py`):
- a mass with a bare twig that no lattice candidate reaches gets its orphan sphere or capsule, and passes (b) within 0.1 m
- a mass with no orphan returns exactly the lattice's candidates

### R5. A hook on each trunk's top, where the limbs leave
**Measured** (QA's `qa_hooks.py` on the shipped metadata: a bark hook whose tip lies within 3 cm of another bark capsule's end):
```
V01_S, V01_S_autumn: (38.2 mm radius, tip at y 2.5 m, 3 limbs leaving) x 2 stems
V01_M, V01_M_autumn: (72.6 mm, y 3.0 m, 4 limbs)
V01_L, V01_L_autumn: (109.6 mm, y 4.0 m, 4 limbs)
(and one 10.1 mm branch tip at y 11.67 m with another capsule's end within 3 cm: a child near its tip or a crossing, not counted here)
```
**The cause:** `tree._records` writes `hook` on "the top segment of every member (all end free)". For a trunk that is the first fork, where the limbs start (design 9: "the trunk's top the crotch the limbs leave"). A limb or branch's children leave at 0.95 of its length at most, so its tip is free. A trunk's is not.

**Why it fails:**
- **The V01 requirement and the sheet's rule:** "every segment under 30 mm an `edge` …, every free tip a `hook`". A 76–219 mm trunk is no edge, and its top is no free tip, so the sheet writes no record there.
- **What the record says:** a hook says the fiber can catch on, and slip off, a free end. At a crotch it can do neither: the limbs go on.

**To close it** (physics engineer's ruling, then the asset-artist):
- **Either** no hook on a member whose children leave at its tip, with a test that every bark hook's tip is free.
- **Or** the physics engineer rules that a crotch is a hook (or a kind of its own), and the requirement and the sheet say so.

## Suggestions (not blocking)
- **S1. The per-variant material scenario's example is stale.** "Autumn leaf on one variant" maps `V13_mid_autumn`'s `leaf` slot. Since task 1.5, the autumn variants use the asset-level `leaf_autumn` and `far_autumn` slots, and the only override is `V13_mid_yellowing` (`leaf` and `far` to the autumn recipes). QA tested the requirement on that variant (passed, §1). Rewrite the example.
- **S2. The V01 budget scenario names two atlases.** "the impostor's albedo and normal atlases are 1k each" leaves out the smoothness atlas of task 1.6b, which the impostor delta names. All three are 1k (§5).
- **S3. The proposal and design text are stale for the archive:**
  - the proposal's "nothing else in V13 changes": V13 was rebuilt by tasks 1.2b, 1.2g, 1.2j, 1.4, 1.5 and 1.6 (far cards, the thicket, autumn variants)
  - the proposal's "G1 builds the late-summer part-bare state of F at 0.6 and the summer state at 0.9": no 0.9 variant is built (the spec delta does not ask for one)
  - the proposal's "pods … in the autumn and bare states": built in the autumn variants only; there is no bare state
  - design 3's "a 2-card cross": the as-built note says one camera-facing quad
  - design 4's 0.35 m: R3
- **S4. The impostor and leaf shaders were never read by their owner as shipped.**
  - The asset-artist wrote all ten commits to `Shaders/` (`Impostor.shader` 493 lines, `CoverageAlpha.hlsl`, the `Leaf.shader` edit), as tasks 1.2–1.6b assigned. `Shaders/` is the tech-artist's (team.md).
  - The tech-artist read only task 1.2a's first version, before the normal atlas, the coverage read, occlusion, the vMF lighting and the smoothness map.
  - **Suggested:** a tech-artist read before G1: the closed-form Fresnel and Hill's fit, and the extra alpha fetch per pixel, which task 1.2l leaves to their performance pass.
- **S5. The world-builder reading the proposal names is not on record.** Review-physics §A.4 and N-j cover the collider count, and phase 3 carries it. The orchestrator records it as covered, or asks for it.
- **S6. The overlay draws shell pieces in the capsules' green.**
  - V13 QA's N-3 asked for a colour of their own "when V01's crowns make the overlay crowded"; design 8 carries it.
  - On V01_L's 147 pieces over 364 capsules the overlay cannot be read for the shell.
- **S7. `lib/export.add_lod` has a latent ordering trap.**
  - With per-vertex `normals`, the vertex block resets every polygon's `use_smooth`. A mesh that also had `smooth` slots would lose their smoothing.
  - No current asset mixes the two: the trees and shrubs carry no vertex normals, and the poles and wires no `smooth`.
  - **Suggested:** refuse the mix in `geometry.Mesh` or `add_lod`, with a test, as the docstring already does for vertex and corner normals.
- **S8. Merge messages.** Four merges keep git's default subjects. `precommit.py` exempts these on purpose (`MERGE_SUBJECT`), and the trunk has the same. Two of them (`ad72c0e`, `098a9f0`) store git's `# Conflicts:` template lines as a body. **Suggested:** `git commit --cleanup=strip` after a conflicted merge.
- **S9. AST-08's tree row fails no build.** V01_L's 40 000 and V13's 12 000 are `pytest` assertions (`test_lods_under_budget_and_halving`). The same as E01's S9, and there before this change.
- **S10. Four working folders from earlier tasks are left in `reference/_compare/`** (local only, ignored). Whoever made them should delete them when done.

## 1. Spec scenarios

| Scenario | Result | Evidence |
|---|---|---|
| V01: built from params by one command | **Passed** | §3: two runs of `pwsh tools/build_asset.ps1 V01`, exit 0, `git status --short` empty after each; FBX, metadata, atlases, prefabs written; 12 sheets byte-identical; the impostor atlases re-rendered from scratch byte for byte |
| V01: sizes and crown inside the sheet | **Passed** | §4: heights 6.05 / 9.75 / 13.39 m (4–7 / 8–12 / 12–16); trunks 7.6 / 15.8 / 27.3 cm at 1.3 m (5–10 / 12–20 / 20–35); crowns 2.88 × 3.03 / 5.89 × 5.54 / 7.56 × 6.73 m (2–4 / 3–6 / 5–8); first fork 2.52 / 3.02 / 4.02 m; V01_M prints `256 bark capsules` (150–300) |
| V01: openings against the drone | **Passed** | §4 and §6: V01_M's four gaps planar, `narrowest_m` 0.6987 / 0.7413 / 0.9122 / 0.7484, the audit's circle within 0.5 mm and QA's own within 0.7 mm; g0 nose-first, g1–g3 any yaw, as task 1.3's handoff lists |
| V01: the sky reads through the crown | **Partly met** | Open, the sky through it, grey-brown bark: yes. But through a few limbs, not F's twig net (R1); the trunk reads straight against F's curving, leaning stems; the leaf colour against the F feed value is not testable without the feed (phase 4) |
| V01: a dark band at 200 m | **Partly met** | No pop and no card edge between LOD2 and the impostor in the 50 m and 200 m tiles (QA's sheets; the parity rows, §7). The crowns read as a sparse light speckle, not a dark mass (R1); the band itself is the belt's depth (phase 3) |
| V01: budget | **Passed** | §5: V01_L LOD0 15 538 (< 40 000), each LOD at most half the previous; the three impostor atlases 1k each; every other texture 2K or less; the cull 2 500 m, the height's 2 px capped (design 3) |
| H: autumn variants built | **Passed** | the six autumn variants build in both runs, every audit line inside its tolerance, every parity row in, budgets as their summer variants' |
| H: matches H | **Failed** | R2 |
| V13 (modified): built from params by one command | **Passed** | §3: two runs, exit 0, `git status` empty after each, 591 build lines identical, 14 sheets byte-identical |
| V13 (modified): sizes inside the sheet | **Passed** | §4: heights 1.25 / 2.06 / 3.01 m, widths 1.56 × 1.54 / 2.21 × 2.26 / 2.51 × 2.54 m; `test_shrub` holds V13_tall's lowest leaf at 0.9 m or more |
| V13 (modified): reads as a wall at the belt edge | **Passed by eye, as V13's own review judged it** | QA's V13_mid sheet: closed and leafy from about 0.9 m to the top. Its belt-edge reading is R2's |
| V13 (modified): stems and shell carry the contact | **Passed** | §4: every capsule `bark`, the worst capsule→mesh 19.3 mm in the build; one record per stem capsule (V13_mid 32 edges and 16 hooks on 48 capsules), twig records `twig`; shells 6 / 19 / 40 pieces (autumn 12 / 29 / 62, yellowing 17), rule (d) as the pieces; over all seven variants the standoff at most 98.0 mm, (b) at most 97.8 mm, (c) at most 297.2 mm of 300; porosity 0.35 summer; no gap, nothing landable |
| V13 (modified): LODs and cull under budget | **Passed** | LOD0 5 902 / 8 670 / 10 708 (< 12 000), halving; the cull at the 2 px rule capped at 800 m (V13's own rule); textures 2K |
| Pipeline: autumn leaf on one variant | **Passed in substance** (S1) | the importer's lines: `V13_mid_yellowing: ... leaf -> leaf_shrub_autumn, far -> leaf_shrub_autumn_far`; every other variant `leaf -> leaf_shrub`; two rebuilds with no diff; `VariantMaterialsTests` (EditMode) |
| Pipeline: impostor built and switched | **Passed** | `<variant>_impostor.png`, `_normal`, `_smoothness` at 1 024² for V01_M, V01_L and their autumn twins; the impostor LOD last, `thin_wire` false; the importer's `4 LODs`, LOD heights `0.9028 / 0.0722 / 0.0367 / 0.0034` (V01_M); `ImpostorLodTests` (EditMode) |
| Pipeline: no LOD switch pops | **Passed** | §7: V01 0 of 44, V13 0 of 42 judged rows outside the limits, every standard error under 1 % |
| Pipeline: metadata convention violations, snag without material, unknown material | **Passed** | `test_meta.py` (`snag[0]: needs the material of the member it follows`, `material 'unobtainium' is not in ...`), in QA's `pytest` run |
| Pipeline: frame probe, collision follows the visual, frame is Unity's | **Passed** | unchanged by this change; EditMode and `pytest` |
| Pipeline: collision drift is caught; buried caps of a capsule chain | **Passed** | `test_mesh_grown_beyond_its_box_fails_the_audit_and_writes_nothing`; `test_a_chain_of_capsules_is_measured_outside_its_buried_caps`, `test_a_capsule_off_its_tube_by_7cm_still_fails`, `test_a_shell_piece_hides_no_rigid_primitive` |
| Pipeline: compliant shells (hugging, too large, twig outside, card outside, allowance follows the card, too many in a cube) | **Passed** | `test_audit.py`, one test per scenario: `test_two_overlapping_spheres_hugging_the_mass_pass_and_print_both_ways`, `test_a_piece_30cm_too_large_fails_its_standoff`, `test_a_twig_vertex_15cm_outside_fails_naming_the_slot_the_distance_and_the_tolerance`, `test_a_card_corner_35cm_outside_fails_and_25cm_passes`, `test_card_allowance_follows_the_card`, `test_65_pieces_in_one_2m_cube_fail_and_spread_along_8m_pass`, plus `test_the_neighbourhood_count_is_exact` against brute force; the build prints 361.026 mm (tree) and 300 mm (shrub) |

**Requirement IDs:**
- **Met:** AST-02, AST-03 (one command, twice; QA did not repeat the parameter-change acceptance in Unity), AST-07 (UVs per member on their own bark patch; QA saw no tiling at 10 m), AST-12, SEC-04, D-003, D-004.
- **Partly met:**
  - AST-05: R5's hook, and R3's text.
  - AST-06: no damaged state here; V07 and V08 are their own changes.
  - AST-08: the budgets hold, by test (S9).
  - AST-09: R1, R2; the autumn mix is built; the 200 m band is phase 3's.
  - REAL-01 and REAL-03: R1, R2; no feed yet.
  - REAL-05: §6.

## 2. Review-physics §A.6, QA's items
| Item | Result |
|---|---|
| 7. Rule (d) 64 or less per variant, at the committed cover and at 0.3 (V01_S at its named cover) | **Met**, QA's exact recount of the shipped boxes: V01_S 58, V01_M 34, V01_L 31; V01_S_autumn (0.5) 60, V01_M_autumn 37, V01_L_autumn 31; V13 6 / 19 / 40, autumn 12 / 29 / 62, yellowing 17. Each equals the build's line |
| 8. The orphan path has a test of its own | **Not met**: R4 |
| 9. The capsule-chain test, and a rigid surface inside a shell piece still measured | **Met**: `test_a_chain_of_capsules_is_measured_outside_its_buried_caps`, `test_a_shell_piece_hides_no_rigid_primitive` |
| 10. Allowance 0.361 m for the tree, 0.3 m for the shrub; rule (c) passes | **Met**: `allowance 361.026 mm` on every V01 card slot (worst 358.277 mm, V01_L); `allowance 300 mm` on every V13 card slot (worst 297.232 mm, V13_mid_yellowing) |
| 11. The deltas' three edits | **Partly met**: §A.2's sentence and scenario are in the asset-pipeline delta; V01's autumn cover from 0.3 with V01_S at its floor is in the H requirement; 0.361 m is in one delta of two (R3) |

## 3. Builds, twice, and the impostor re-bake
```
pwsh tools/build_asset.ps1 V13   run 1: exit=0  11:57:29-12:06:15  git status --short: empty
pwsh tools/build_asset.ps1 V13   run 2: exit=0  12:06:50-12:13:00  git status --short: empty
  591 V13 build lines identical (times masked); 14 review sheets byte-identical (SHA-256)
pwsh tools/build_asset.ps1 V01   run 1: exit=0  12:13:58-12:23:46  git status --short: empty
pwsh tools/build_asset.ps1 V01   run 2: exit=0  12:24:15-12:34:05  git status --short: empty
  2423 V01 build lines identical (fit and audit times masked); 12 review sheets byte-identical; problem lines: 0
the 12 V01 impostor atlases moved out of the tree, then blender --background ... build.py -- V01:
  V01_M: impostor rendered in 4.5 s   V01_L: 5.7 s   V01_M_autumn: 4.6 s   V01_L_autumn: 4.7 s
  blender exit=0; git status --short: empty; all 12 atlases byte-identical with the moved-out ones
```
- **Why the re-bake:** an atlas whose hashes match is not rendered again, so the two builds alone prove nothing about the bake. With them gone, the bake reproduced them from the params and materials, so none is hand-edited (D-004, the impostor delta).
- **The importer's lines** name every slot: the three autumn slots (`leaf_autumn -> leaf_locust_autumn`, `pod -> pod_locust`), 4 LODs for M and L and 3 for S.

## 4. The metadata, read by QA's own script
| | V01_S | V01_M | V01_L | V01_S_autumn | V01_M_autumn | V01_L_autumn |
|---|---|---|---|---|---|---|
| Size x × y × z (m) | 2.88 × 6.05 × 3.03 | 5.89 × 9.75 × 5.54 | 7.56 × 13.39 × 6.73 | 2.82 × 5.99 × 2.94 | 5.65 × 9.66 × 5.45 | 7.61 × 13.30 × 6.65 |
| Trunk(s) at 1.3 m | 2 × 0.076 m | 0.158 m | 0.273 m | 2 × 0.076 m | 0.158 m | 0.273 m |
| More capsules than stems from | 2.52 m | 3.02 m | 4.02 m | 2.52 m | 3.02 m | 4.02 m |
| Bark capsules / twig pieces (capsules, spheres) | 130 / 70 (54, 16) | 256 / 130 (86, 44) | 364 / 147 (102, 45) | 130 / 72 (52, 20) | 256 / 160 (77, 83) | 364 / 174 (77, 97) |
| Rule (d), exact | 58 | 34 | 31 | 60 | 37 | 31 |
| LOD triangles | 8 334 / 2 920 / 1 324 | 13 084 / 4 110 / 1 868 / 2 | 15 538 / 4 790 / 2 136 / 2 | 8 292 / 2 900 / 1 286 | 12 558 / 3 850 / 1 706 / 2 | 14 754 / 4 398 / 1 914 / 2 |
| Wind: crown porosity; least separation | 0.75; 1.00 mm | 0.75; 1.00 mm | 0.75; 1.00 mm | 0.775; 1.00 mm | 0.825; 1.00 mm | 0.825; 1.00 mm |
| Snag: bark edge / bark hook / twig hook | 64 / 39 / 184 | 111 / 60 / 194 | 142 / 87 / 145 | 64 / 39 / 184 | 111 / 60 / 194 | 142 / 87 / 145 |

- **Every record** names a listed material (0 without).
- **Twig ring spacing** is at most 0.1000 m (PR6), and the largest bark `edge` radius 15.00 mm (under 30 mm).
- **Nothing is landable**, and every collider is a capsule or a sphere.
- **The wind volumes** are separated by 1.00 mm, the build's "1 mm under the crown box".
- **Porosity** follows the sheet's points: 0.75 at 0.6, 0.775 at 0.5, 0.825 at 0.3.
- **The build's own worst lines:** capsule→mesh 4.4 / 7.7 / 13.8 mm (V01_S / M / L), and mesh→collision 4.3 / 7.6 / 13.3 mm at the trunks' tops (y 2.51 / 3.02 / 4.02 m), inside the 20 mm zone round the gaps there. The shell standoff is at most 97.99 mm, (b) at most 97.8 mm, the pods' (c) at most 174.4 mm.
- **Task 1.7's three near-gap clauses** read as it says: V01_S capsule 28 0.889 mm, V01_M capsule 112 1.702 mm, V01_L capsule 37 2.116 mm.

**V13's re-record (task 1.1, `a6234ad`),** QA's `qa_rerecord.py` against its parent:
```
V13_low  top-level keys changed: ['snag']  snag 227 -> 227; gained only material: 227; otherwise changed: 0
V13_mid  top-level keys changed: ['snag']  snag 197 -> 197; gained only material: 197; otherwise changed: 0
V13_tall top-level keys changed: ['snag']  snag 309 -> 309; gained only material: 309; otherwise changed: 0
  (bark on the 28 stem edges and 14 stem hooks of each, twig on every twig record)
```
V13 changed later in this change by design: the far cards, the thicket and the autumn variants (S3). Its current files read as the V13 table above, every record with its material.

## 5. Budgets (AST-08) and textures
- **Triangles:**
  - V01_L LOD0 15 538 against the 40 000; V01 at most 15 538.
  - V13 LOD0 at most 10 708 against the 12 000.
  - Every LOD at most half the previous, autumn variants included.
- **Textures** (QA's `qa_png.py` over every PNG this change adds; IHDR sizes):
  - the leaf, bark and autumn base and mask maps 2048²; the pod strip 128 × 256
  - the pinned `bark_brown_01` maps 2048²
  - the twelve impostor atlases (albedo, normal, smoothness × 4 variants) 1024²
  - nothing over 2K
- **Colliders per tree:** 200 / 386 / 511 (S / M / L), 538 for V01_L_autumn. Review-physics §A.4 rules them within budget with a binned index (N-j, phase 3).

## 6. The gaps against the drone, and the geometry audit (REAL-05)
**Gaps** (the build's line, then QA's own inscribed circle on a 1 cm grid refined to 0.16 mm, planarity, and every point 1 cm inside the polygon on a 2 cm lattice tested against every collider and record):

| Variant | Gap | `narrowest_m` | QA's circle | Planar within | Clear-space samples blocked | Verdict |
|---|---|---|---|---|---|---|
| V01_S (and autumn) | g0, the two stems from the ground | 0.7058 | 0.7060 | 0.001 mm | 0 of 3 011 | nose-first (0.7058 < 0.706) |
| | g1, limbs 4–2 | 0.5304 | 0.5308 | 0.000 mm | 0 of 853 | too narrow for the drone (written as the sheet asks, ≥ 0.5 m) |
| V01_M (and autumn) | g0, limbs 4–1 | 0.6987 | 0.6991 | 0.000 mm | 0 of 1 652 | nose-first |
| | g1, limbs 1–2 | 0.7413 | 0.7417 | 0.001 mm | 0 of 1 988 | any yaw |
| | g2, limbs 2–3 | 0.9122 | 0.9128 | 0.001 mm | 0 of 2 620 | any yaw |
| | g3, limbs 3–4 | 0.7484 | 0.7491 | 0.001 mm | 0 of 1 935 | any yaw |
| V01_L (and autumn) | g0–g3, the four limbs | 1.3612 / 1.4883 / 1.3182 / 1.2654 | 1.3618 / 1.4892 / 1.3189 / 1.2659 | ≤ 0.002 mm | 0 of 24 757 | any yaw |
| | g4, branch 2–7; g5, branch 3–9 | 0.6584 / 0.6638 | 0.6588 / 0.6647 | ≤ 0.002 mm | 0 of 4 983 | nose-first |

- Every normal is a unit vector.
- QA's circles agree with `narrowest_m` within 0.9 mm, and the build's audit within 0.6 mm.
- No piece, capsule or record reaches into any polygon (PR7).
- The inter-limb openings under 0.5 m are not written, by the sheet's rule (`test_an_opening_of_half_a_metre_is_kept_and_one_of_0_4_m_dropped`).

**The geometry audit:**
- **Trunk, limbs and branches are tubes with capsules: yes.**
  - Every segment of 10 mm or more is a `bark` capsule on its chord: 130 / 256 / 364, the trunk in 1.0–1.5 m pieces (V01_L's 4 m in 3).
  - The capsule→mesh worst is 13.8 mm, inside both tolerances.
  - The buried caps at the joints are hidden by the union rule (§A.2).
  - The overlay shows capsules along every limb (S6 for the shell's colour).
- **Twigs are tubes: yes.** 3-sided zigzags ringed at every kink at most 0.1 m apart, each with a polyline `twig` record through its rings.
- **Cards sit inside the shells: yes.**
  - Every leaf and pod card vertex is inside the union or within 0.361 m of it (worst 358.3 mm).
  - Every twig vertex is within 0.1 m (worst 97.8 mm).
  - Every piece stands within 0.1 m of the faces it carries (worst 98.0 mm).
  - Rule (d) holds everywhere (§2).
- **Nothing physical is texture-only.** The bark's furrows are the set's normal map, as design 1 states and the sheet allows: the fiber does not read them. The pods are cards with `twig` physics inside the shell (N-f).
- **R5's trunk-top hooks** are the one record that does not follow the sheet's rule.

## 7. LOD parity (`Fpv450.LodParity.Measure`)
```
& $env:UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.LodParity.Measure -asset V01 -logFile -
  AC True at start and end; Renderer: NVIDIA GeForce RTX 4050 Laptop GPU; exit 0; 12:46:50-12:54:52
  [LodParity] V01_S: PASS, 0 of 6 judged rows outside the limits, 0 not judgeable; 12 info rows; 0 of 6 tiles on 16x16; process peak working set 1755 MB
  [LodParity] V01_M: PASS, 0 of 8 ...; 2 of 8 tiles on 16x16; ... 1770 MB
  [LodParity] V01_L: PASS, 0 of 8 ...; 2 of 8 tiles on 16x16; ... 1805 MB
  [LodParity] V01_S_autumn: PASS, 0 of 6 ...   V01_M_autumn: PASS, 0 of 8 ...   V01_L_autumn: PASS, 0 of 8 ...
  [LodParity] V01: PASS, 0 of 44 judged rows outside the limits, 0 not judgeable; 120 info rows      (222 [LodParity] lines)
& $env:UNITY ... -asset V13 ...
  AC True at start and end; RTX 4050; exit 0; 12:55:27-12:59:27
  [LodParity] V13: PASS, 0 of 42 judged rows outside the limits, 0 not judgeable; 84 info rows      (184 [LodParity] lines)
```
- **The impostor rows read as task 1.6b's table, to the digit**, against LOD2:
  - V01_L_autumn overcast at 290 m +3.41 % (margin 2.59), V01_L +3.09 %
  - V01_M by day at 230 m −2.51 %, overcast +0.02 %
  - V01_M_autumn −0.75 / −1.27 %, V01_L_autumn by day −0.05 %, V01_L by day −0.65 %
  - coverage −5.87 % at most
- **V13's worst row** is V13_low_autumn's LOD1 at 50 m by day, −4.27 % (task 1.6's figure).
- **Statistics** (QA's `qa_parity_z.py`: each judged row's margin to its limit over √(se₁² + se₂²) of the pair):
  - the least margin over V01's 44 rows is 5.9 standard errors (V01_S_autumn's LOD2 switch by day, +1.54 %), and over V13's 42 rows 4.4 (V13_low_autumn above)
  - no verdict rests on noise: a 4.4 σ margin errs with a probability under 10⁻⁵, and over all 86 judged rows the chance of one wrong verdict is still under 10⁻⁵, dominated by that row
  - the test's length (64 offsets, 256 where any error reaches 1 %) is set by the 1 % standard-error rule, not by a stated false-failure rate; at today's margins that rule is more than enough
  - the target fails known-wrong models, on the record with this measure: V13's crossed far cards at +12.24 % (task 1.2f), the impostor's mean normals at −9.06 % (task 1.3), V01_S_autumn's every-fourth-card pick at +6.52 % (task 1.5)
  - `LodParityTests` hold the judge at its limits with literal numbers

## 8. The look (REAL-03, D-038)
**Read by eye:**
- **QA's own sheets** of all 13 variants, at 1.5 / 10 / 50 / 200 m with the turntables and overlays, day and overcast. V01_M and V01_L were enlarged at 1.5, 10, 50 and 200 m.
- **The artist's comparison sheets:**
  - `reference/_compare/V01.png` (task 1.3): F, G and H rows and the 200 m row of three V01_L, by day and overcast, at 2° and 20°
  - `V01-H.png` (task 1.5): the belt edge at H 13, from 1.5 m at H 5, the see-through render, H 7's band, H 16's bushes and task 1.4's edge
- **The letters:** F 1–12 and 1–24, H 1–12, 10–21 and 1–24, G 1–12 and 1–15, from their contact sheets.

**What matches:**
- **The leaves:** locust-like pinnate sprigs, the right size and green.
- **The bark:** grey-brown, mottled, each member on its own patch of the tile.
- **V01_S's two-stemmed openwork tree** with tufts at its tips (G).
- **The canopy's value overcast** (H; task 1.3's measure).
- **The autumn mix,** a few yellow leaves in the locust and half of them in the shrubs.
- **The thicket's fanning, leaning, crossing stems** of mixed thickness.
- **No pop between any two LODs** at any range, and no card edge at 200 m.

**What does not match:** R1 (the M and L crowns), R2 (the belt edge). Beyond those, these are the world's or a later change's, and not counted against this change:
- **F's dust haze and backlight,** and H's dark stems against a bright sky: the feed's and the light's, phases 2 and 4.
- **H's straw, weeds and saplings:** S04, V17, V16.
- **The belt's depth of rows:** phase 3.

## 9. The 1.7 merge (`0fc2889`, parents `968c812` and `18201d6`)
- **What git could not merge.** QA recomputed the merge (`git merge-tree --write-tree 968c812 18201d6`): 12 files conflict, as task 1.7 says.
- **What the merge commit changed by hand** (it against the automatic tree):
  - the 12 conflicted files
  - the asset-pipeline delta's `thin_wire` sentence
  - `lib/impostor.py` (`"thin_wire": False` on the impostor LOD, with its docstring)
  - `test_impostor.py` (the flag asserted false; `update(thin_wire=True)` refused with "the impostor's thin_wire must be false")
  - `ImpostorLodTests.cs` (`thin_wire` on both LOD entries, and the empty `ports`, `wires` and `holes`)
  - `test_shrub.py` and `test_tree.py` (`normals` → `corner_normals`, three lines)
  - V13's three prefabs, taken from the branch side as task 1.7 says and rebuilt in `ef196a3`
  - Each is the smallest change both sides' rules need. `meta.validate` refuses an impostor whose `thin_wire` is not `False`.
- **The rename in the shared `lib/geometry.py`.**
  - The trunk side keeps `normals` and `uv2` per vertex (L01's wires). The branch's per-corner map is now `corner_normals`, in the class, in `face` and in `lib/export.add_lod`, which applies the corner block first and the vertex block after.
  - `grep` finds no reader of the old meaning left: `cards.Card.normals` is the card's own list and goes into `face(normals=...)`. `impostor.inputs_sha256` hashes neither.
  - No FBX changed: the rebuilds of all seven assets leave `git status` empty.
  - S7 is the one latent trap.
- **One `audit.run(meta, lod0, *, soft, card_visuals, card_reach_m, planted, clip_boxes)`:**
  - **The branch's rules:** rule (d) (`SHELL_NEIGHBOURHOOD_MAX` 64 via `_audit_shells` → `neighbourhood`), the card allowance from `card_reach_m`, the shell pieces hiding nothing.
  - **The trunk's rules:** the wire rule (`_audit_wires`, wire slots out of the primitive audit), `planted` (`_above_ground`), `clip_boxes` (`_uncovered`), the zone-edge cell, `_ShapeIndex`, `_GapPolygon`.
  - **The branch's bounding-sphere prefilter gave way** to the trunk's box test (`_boxes_meet`), which `_covered` applies anyway.
  - **The lines bear it out:** V13's 591 lines identical between runs and as task 1.7 counts them; V01's three near-gap clauses as task 1.7 explains them (§4).
- **One `EnsureLods`.** An impostor LOD builds its card set and nothing else, and every other LOD its mesh child (`thin_wire` from the metadata, the wire-slot and uv2 checks) plus its micro meshes. `DeleteImpostorAssets` runs when no LOD is an impostor.
- **`ReviewCapture.cs`:**
  - **E01's framing stands:** the `EarthworkRun` record, `EarthworkLookDownDeg`, the bar beside the lip, the pilot pose's log line.
  - **Beside it, task 1.2m's leak fix:** `Rig.Render` reads back with no `Apply` (the only behaviour change).
  - **Task 1.4's legend wrap:** `Wrap`, in `Legend`.
  - **Task 1.2h's members** made internal or lifted into functions (`TurntableDistance`, `ShadowDistance`, `WarmUp`, `HideBar`), the numbers unchanged.
  - **E01's capture** after QA's rebuild logs the pilot pose the archived E01 review recorded, number for number: `[Review] earthwork pilot pose: camera (0.133, 3.350, -0.377), 1.500 m above the lip, 0.400 m to the field side (right) ..., forward (0.8865, -0.3420, 0.3117), 20.00° down ... bar base (3.029, 1.850, 0.429), 3.00 m along the run, in frame True, top in frame True`.
- **K01, S04, L01, L03 and E01 rebuilt** with `build_asset.ps1`, one after another, AC on, 6.9 GB or more free:
  ```
  K01 exit=0  12:59:48-13:00:15  git status --short: empty
  S04 exit=0  13:00:16-13:01:34  git status --short: empty
  L01 exit=0  13:01:34-13:02:27  git status --short: empty
  L03 exit=0  13:02:27-13:03:00  git status --short: empty
  E01 exit=0  13:03:01-13:10:38  git status --short: only ?? openspec/changes/p1-v01-locust/review.md (this review, not yet committed)
  E01 holes F-1 ... 0 miss every filler; F-2 least 0.6500 m inside the bands; F-4 highest filler point at y = 0.0000 m; F-5 383 fillers, every box at least 0.300 m; every joint 0.200 m
  ```
- **The branch merges into today's trunk** with no conflict (`git merge-tree --write-tree origin/unity/main HEAD`, exit 0). The trunk has not moved since `18201d6`.

## 10. Commands and hygiene
```
git fetch origin; git checkout unity/art/p1-v01-locust (45c5626); git lfs checkout        -> Checking out LFS objects: 100% (111/111)
(Get-CimInstance -Namespace root/wmi -ClassName BatteryStatus).PowerOnline                -> True before every Blender and Unity run; powercfg: Balanced
pwsh tools/build_asset.ps1 V13 (twice), V01 (twice), K01, S04, L01, L03, E01                -> §3, §9
& $env:BLENDER --background --factory-startup --python tools/blender/build.py -- V01      -> the forced re-bake, §3
& $env:UNITY ... -executeMethod Fpv450.LodParity.Measure -asset V01 / -asset V13           -> §7
& $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode ... -> total=95 passed=95 failed=0 skipped=0 result=Passed
python -m pytest tools -q                                                                  -> 987 passed in 941.46s (0:15:41)
python -m pytest tools/hygiene -k test_existing_tree_is_clean -rA                          -> PASSED [HEAD]; PASSED [origin/unity/main merged with HEAD]
python tools/assets/fetch.py --verify                                                      -> OK, exit 0
python tools/hygiene/precommit.py --tree HEAD                                              -> 1032 files scanned, 0 findings, exit 0
openspec validate p1-v01-locust --strict                                                   -> Change 'p1-v01-locust' is valid
openspec validate --all --strict                                                           -> Totals: 14 passed, 0 failed
git merge-tree --write-tree origin/unity/main HEAD                                         -> a tree, exit 0
```
- **Commits:** 118 in the range; 111 non-merge, every one a subject only, `type(scope): summary`, lower case, at most 72 characters, no body, no trailer, no co-author line.
  - Identities: Asset Artist 81, Orchestrator 20, Tech Artist 15 (tasks 1.2a, 1.2c, 1.2d, 1.2f, 1.2h, 1.2k), Physics Engineer 2 (task 1.0 and its reading after 1.1).
  - The 7 merges are the Orchestrator's and the Asset Artist's; S8 for their messages.
- **Committed paths:** nothing under `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/`. Every one of the 40 binaries this change adds or changes (PNG, FBX) is under LFS.
- **Licences (D-003):**
  - Poly Haven `bark_brown_01` at 2k is pinned in `manifest.json` with each file's URL, SHA-256 and size, and credited in `CREDITS.md` ("CC0 1.0 | Rob Tuytel").
  - QA checked it against Poly Haven's own API. The author is `{"Rob Tuytel": "All"}`, and the four PNGs' MD5s and byte sizes equal the local files' (`69ae3d63…` diffuse, `a77b22b7…` nor_gl, `bd1e6740…` rough, `cfb5e12d…` ao). Poly Haven publishes every asset under CC0.
  - The leaves reuse the pinned `LeafSet002`. The pods are painted by `derive.py`.
- **OPSEC:** the scan passes over the tree. QA wrote no file name, place, date or OSD value into this review.

## Re-check (task 2.2)

**Reviewer:** qa-engineer, fresh (did none of the work and not QA 2.1, D-034) · **Tip:** `356ce41` · **Range:** `0dd9ce5..356ce41`, 20 commits on the branch's own line, one of them the 1.12 merge of `unity/main` at `2dc129f`
**Scope (D-041):** a re-check of what QA 2.1 left blocking, not a second review. It covers R3–R5, §B.6's first group, S4, S6, S7, the 1.12 merge and the repeats. R1 and R2 are G1 stand-ins (D-040): read for their wording only.
**Read:** `CLAUDE.md`, `AGENTS.md`, D-040, D-041; QA 2.1 above; `review-physics.md` §B; `review-techart.md`; `tasks.md` 1.8, 1.9, 1.11, 1.12, 2.1's note, 2.2; the code and tests each check rests on.
**How it ran:**
- QA's own worktree, detached at `356ce41` (the branch was checked out in another worktree), `git lfs checkout` (163/163), and no `unity/Library/` before the first Unity run.
- AC on (`BatteryStatus.PowerOnline` True) at the start and end of every Blender and Unity run, Balanced plan. Every Unity log names `NVIDIA GeForce RTX 4050 Laptop GPU`.
- Free RAM was checked before each run. Twice it was under 2.5 GB:
  - before the first build, QA waited for another agent's Unity to exit (then 4.3 GB free)
  - before the parity runs, QA set a wait on another agent's Blender and checked again when its own `pytest` ended (3.8 GB free)
  - That Blender ran beside the parity runs, with 3.0 GB or more free.
- QA's scripts are plain Python in the scratchpad, never committed, and share no code with `tools/blender`. No performance claim is made.

### Verdict: APPROVED

R3, R4 and R5 close, and so do S4, S6 and S7. §B.6's first group is in the deltas, the sheet and PHY-18. The 1.12 merge keeps both sides' rules, and every repeat reads as QA 2.1 and task 1.12 left it. R1 and R2 stay G1 stand-ins, and `docs/gallery-g1.md` names them as QA sees them. There is no new look miss. Tasks 2.1 and 2.2 are ticked.

### Items
| Item | Result | Evidence |
|---|---|---|
| R3, the card allowance | **Closed** | The V01 requirement reads "within the tree's card allowance, 0.361 m (the 0.25 m card's far corner plus 0.1 m)". Design 4 reads "a 0.361 m card allowance (the 0.25 m leaf card's far corner at 0.261 m plus 0.1 m, review-physics §A.3)" (`b362116`). "0.35 m" is left only as a twig's length (design, skeleton) and in the shrub's "Card outside the shell" scenario (0.35 m against 0.3 m), both right. The build prints `allowance 361.026 mm`, worst 358.277 mm (V01_L). |
| R4, the orphan path | **Closed** | `test_shell.py` holds `test_a_bare_twig_no_lattice_candidate_reaches_gets_its_orphan_pieces_and_passes_rule_b` and `test_a_mass_with_no_orphan_fits_from_exactly_the_lattices_candidates` (`3a4156c`). Both pass. **Mutation 1** (`fit` passes no target to `mass.on`): the first test fails at `assert passes`. **Mutation 2** (one lattice-reached target given orphan candidates anyway): both fail, the second showing "Left contains one more item". Reverted with `git checkout`, then 8 of 8 pass. |
| R5, the hooks | **Closed** | Below: QA's own check on the shipped metadata. |
| §B.6, first group (`e06d90e`) | **Closed** | Each new text is present exactly once, and each old text is gone: edit 1 (asset-pipeline conventions), edit 2 (V01's snag clause), edits 3 and 4 (the sheet's snag rule and §V01's snag line), edit 5 (PHY-18's wedge). `e06d90e` touches only those four files. The second group waits for R1 and R2's work (§B.5). |
| S4, the shader reading | **Closed** | `review-techart.md` (`e47c70d`): "Sound for G1. No blocking finding." `docs/plan.md` phase 3 opens with "Before the belts are placed", citing findings 1–4: the Leaf shadow pass sets up its instance first, the impostor reads its own SH with the spread and widens its reflection, `LodParity` runs again under G1's day and overcast from 2 m and 30 m, and the cost is measured per PLT-07 at 1080p and at the feed's size (`af3b141`, merged through #110). |
| S6, the overlay | **Closed** | `ReviewMeta.ShellColor` (255, 220, 0) for `twig` and `foliage` pieces, and the legend key "SHELL PIECES (n, materials): kinds" apart from "COLLISION" (`f81d6a4`). EditMode `Overlay_DrawsShellPiecesInAColourOfTheirOwn` passes. On QA's V01_L sheet the overlay tile holds 23 230 pixels of (255, 220, 0) beside 22 089 green. By eye the 147 pieces stand apart from the 364 capsules, and the legend reads "COLLISION (364): 364 CAPSULE" and "SHELL PIECES (147, TWIG): 102 CAPSULE, 45 SPHERE". |
| S7, mixed normals | **Closed** | `add_lod` raises before touching Blender on per-vertex `normals` with `corner_normals`, or with `smooth` slots (`dedbce3`). `test_export.py` has both refusals, and either kind alone goes on to Blender. All pass. |
| 1.12's merge (`129f788`) | **Closed** | Below. |
| The repeats | **Closed** | Below. |
| R1, R2 (G1 stand-ins) | **Named as QA sees them** | Below. |

### R5, by QA's own check of the shipped metadata
`qa_hooks22.py` reads only the shipped metadata. It takes the capsules' axis ends from centre, height and rotation (every bark `edge` matches a capsule's two ends within 0.2 mm together, 64 / 111 / 142 of 64 / 111 / 142, which proves the frame). For every bark hook it lists every other segment end (capsule ends, bark record ends, twig records' first points) at the tip and within 3 cm of it. It walks each trunk's capsule chain from the ground to its first fork.
```
V01_S (and autumn): 285 records (64 bark edge, 37 bark hook, 184 twig hook); 2 trunks, top at y 2.500 m, r 38.2 mm, 3 segments leave each top
V01_M (and autumn): 364 records (111 / 59 / 194); trunk top y 3.000 m, r 72.6 mm, 4 leave
V01_L (and autumn): 373 records (142 / 86 / 145); trunk top y 4.000 m, r 109.6 mm, 4 leave
every variant: bark records lying on a trunk capsule 0; hooks ending at a trunk top 0; bark hooks with another segment's end at the tip (0.1 mm) 0
within 3 cm of a tip, not at it:
  V01_L  hook r 10.1 mm, tip y 11.672 m, segment 205.0 mm: capsule r 5.5 mm and its bark hook start 20.6 mm short, 0.000 mm off the hook's axis at t 0.899
  V01_M  hook r 6.1 mm, tip y 7.232 m: twig start 20.3 mm short, 0.001 mm off the axis at t 0.898; its own child twig 11.0 mm away, on that twig's axis
  V01_M  hook r 7.6 mm, tip y 5.391 m: twig start 29.1 mm away, on another branch (27.9 mm off this axis)
  V01_S  hook r 4.6 mm, tip y 4.862 m: twig start 29.6 mm short, on the axis at t 0.738
  V01_S  hook r 3.9 mm, tip y 4.249 m: twig start 27.6 mm away, on another twig (11.6 mm off this axis)
FAILURES 0
```
- **Every hook on all 13 variants is on a free tip.** `qa_twighooks22.py` runs the same check on every hook, bark and twig, over V01 and V13: nothing leaves any hook's tip. The converse holds too: every V13 twig `edge` (60 / 59 / 91 / 59 / 91 / 60 / 78) has a record leaving its end.
- **The metadata diff since QA 2.1's tip `45c5626`** (`qa_snagdiff22.py`): exactly the trunk-top hooks are removed, two on V01_S (r 38.2 mm, to y 2.500 m) and one each on V01_M (72.6 mm, y 3.000 m) and V01_L (109.6 mm, y 4.000 m), the same on the autumn twins. Nothing is added. The only other key changed is task 1.12's `"textures": []`.
- **The exemption is right by §B.3.** §B.3 defines a free tip as "a member end from which no other member leaves", and children leave "at 0.2–0.95 of its length". V01_L's 205 mm branch has its child on its own axis (0.000 mm) at 0.899 of its length, 20.6 mm short of the end, so it leaves the side, not the end. The test's 3 cm check skips only a capsule that starts at the hook's base (its own) or on the hook's own axis (within 2 µm). Its first assertion, that no hook's tip lies within 1 mm of any member's first point, holds for that branch with 20 mm to spare. The two other on-axis side children QA found (V01_M at 0.898, V01_S at 0.738) are twigs, which the capsule check does not list, and they are free tips by the same reading.

### The 1.12 merge (`129f788`, parents `e47c70d` and `2dc129f`)
- **`git show --remerge-diff 129f788`:** three files conflicted (`build.py`, `meta.py`, `CatalogImporter.cs`), as task 1.12 says.
  - In `build.py`, R01's stale rule ("every `<variant>_*.png` not written") gives way to `listed_textures` (read before the metadata is replaced) and `write_textures`, which removes, with its `.meta`, only `listed - written`.
  - `meta.py`'s docstring carries both sides.
  - `CatalogImporter.cs` keeps the branch's `EnsureLods` call with R01's `LodBounds` frame, and the second `SetVector` is removed. One definition is left (line 556).
  - Outside the conflicts, the merge's own hand edits are R01's and K07's snag materials (`kits.emit_patch` and `_petal` take `material`; `vehicle`'s `_body_side`, `_notch_side`, `_flap`, `_ends`, `_crazed_pane`, `_snags` and `_base_snags`; the three `debris_car` pieces) with their tests, and V13's three metadata files taken from the branch and rebuilt in `f9f84b2`.
- **The rule, by a mutation:** `test_a_build_removes_only_the_textures_its_old_metadata_listed` passes. With R01's old rule put back it fails (`FileNotFoundError ... V01_M_impostor.png`). Reverted, it passes again.
- **V01's 24 atlases and `.meta` files** are unchanged against `e47c70d` and against QA 2.1's `45c5626` (`git diff --stat`, empty). Against `e47c70d`, V01's and V13's 13 metadata files gain one line each (`"textures": []`). No FBX, prefab, material or atlas changed.
- **R01 and K07 against `unity/main` (`qa_rerecord22.py`):**
  - only `snag` changes, and every record gains `material` alone: R01_A 67 (40 `sheet_metal`, 1 `glass`, 26 `steel`), R01_F 86 `steel`, and each K07 piece its one record, `steel`
  - no R01 or K07 FBX, prefab or material differs from `unity/main`
  - K01, S04, L01, L03 and E01's outputs equal `unity/main`'s
- **R01 rebuilt by QA** (Blender, `build.py -- R01`, exit 0, 21:35–21:42, `git status` clean after it):
  - plans 212 and 224, by group as the tests' `A_COUNTS` and `F_COUNTS`; the clean base's 110 is `test_collision_plan`'s, which passes in QA's run
  - A: box→mesh 49.590 mm, in the zone 19.646 mm; mesh→collision 40.188 mm (18.974)
  - F: 49.589 mm (19.539); 40.187 mm (17.160)
  - triangles 54 467 / 12 335 / 3 460 / 72 + 4 000 crumbs, and 41 102 / 8 642 / 2 964 / 72
  - These equal R01's archived re-check and task 1.12's note.

### The repeats
```
pwsh tools/build_asset.ps1 V01   run 1: exit=0 21:14:26-21:25:21  git status --short: empty
pwsh tools/build_asset.ps1 V01   run 2: exit=0 21:25:47-21:34:56  git status --short: empty
  2624 build, importer and review lines identical between the runs (times masked); 2425 of them Blender's; problem lines 0
  12 review sheets byte-identical (SHA-256)
  records lines: V01_S 285, V01_M 364, V01_L 373 (each autumn twin the same); near-gap clause "V01_S capsule 28 ... 0.889 mm" as task 1.7 explains it
& $env:UNITY ... -executeMethod Fpv450.LodParity.Measure -asset V01   exit 0, 21:58:13-22:05:34, 222 [LodParity] lines
  V01: PASS, 0 of 44 judged rows outside the limits, 0 not judgeable; 120 info rows
  worst per variant -2.20 / -2.51 / +3.09 / +3.15 / +2.37 / +3.41 % (S / M / L / S_autumn / M_autumn / L_autumn), as task 1.9's
  the eight impostor rows to the digit as QA 2.1's §7 (V01_L_autumn overcast +3.41 %, coverage -5.87 % at most)
& $env:UNITY ... -asset V13                                          exit 0, 22:05:59-22:09:32, 184 [LodParity] lines
  V13: PASS, 0 of 42 judged rows outside the limits, 0 not judgeable; 84 info rows; worst V13_low_autumn -4.27 % (LOD1 at 50 m by day)
& $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode ...   total=109 passed=109 failed=0 skipped=0 result=Passed
python -m pytest tools -q                                            1077 passed in 1282.04s (0:21:22)
python -m pytest tools/hygiene -k test_existing_tree_is_clean -rA    PASSED [HEAD]; PASSED [origin/unity/main merged with HEAD]
python tools/assets/fetch.py --verify                                OK, exit 0
python tools/hygiene/precommit.py --tree HEAD                        1314 files scanned, 0 findings, exit 0
openspec validate p1-v01-locust --strict                             Change 'p1-v01-locust' is valid
openspec validate --all --strict                                     Totals: 14 passed, 0 failed
```
- **Statistics** (`qa_parity22.py`: each judged row's margin to its limit, over √(se₁² + se₂²) of the pair):
  - the least margin is 5.9 standard errors over V01's 44 rows (V01_S_autumn's LOD2 switch by day, +1.54 %) and 4.4 over V13's 42 (V13_low_autumn above), QA 2.1's figures
  - no verdict rests on noise
  - the known-wrong models QA 2.1 lists still stand as the target's failures on record
- **Commit hygiene** (the 20 commits):
  - subject only, `type(scope): summary`, lower case, 37–70 characters, no body, no trailer, no co-author line
  - authors are the Orchestrator, the Physics Engineer, the Asset Artist and the Tech Artist, each on their own work
  - `git diff --name-only` holds nothing under `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/`, and every changed PNG and FBX is under LFS
  - one committer slip, S11

### R1 and R2, the G1 stand-ins (`docs/gallery-g1.md` on `origin/unity/world/p1-g1-look-lock-gallery`, `cce941f`)
- **Station 2's lines:**
  - "The locust crowns are a few long limbs with sparse leaf tufts; F's are a dense, scraggly net of fine zigzag twigs. At 200 m the belt reads as dotted crowns, not a dark band."
  - "The H edge reads as an olive leaf band on bare stems; H is a picket of thin dark stems through sparse, mostly green leaves."
  - "Only 20 m of belt stands, two rows deep."
- **What QA sees.**
  - QA's V01_M and V01_L sheets from the builds above, at 1.5, 10, 50 and 200 m, day and overcast: four long, nearly straight limbs in a vase with sparse leaf tufts, and at 200 m a sparse speckle of a crown.
  - The local H comparison sheet, read inside `reference/_compare/` and its crop deleted after: the understory is an olive leaf band over bare stems.
  - V13's clumps and V01's FBX are unchanged since that sheet's build, apart from task 1.6's LOD picks. So the wording matches R1 and R2 as QA 2.1 wrote them.
- **No new look miss.** R5's change moved records, not geometry, and the sheets show the same trees.

### Suggestions (not blocking)
- **S11. `89112e7`'s committer is the machine's default identity** (`SanityEclipse000`), with the Asset Artist as author, 27 s apart: an amend made without the role's `-c`. It is pushed, so it is not rewritten (S8's rule). The trunk already holds four such commits. Commit with `git -c user.name=… -c user.email=… commit --amend`, or not at all.
- **S12. Two lost spaces from the 1.12 merge:** `size =[round(` in `build.py`'s `build_variant` (the merge's own resolution), and `data =meta.assemble(` in `test_vehicle.py`. They are cosmetic. Fix them in the next change that touches either file.
- **S10 (QA 2.1) is still open, local only:** two of the earlier tasks' working folders are still in `reference/_compare/`.
