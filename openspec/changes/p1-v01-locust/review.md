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
