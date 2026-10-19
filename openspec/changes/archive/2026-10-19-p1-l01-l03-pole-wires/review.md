# Review: p1-l01-l03-pole-wires (task 2.1)

**Reviewer:** qa-engineer, fresh (did none of the work, D-034) · **Branch:** `unity/art/p1-l01-l03-pole-wires` · **Tip:** `cf778bb` · **Range:** `5ef4206..cf778bb` (52 commits, 197 files; merge base with `unity/main`)
**Requirements cited:** AST-02, AST-03, AST-05, AST-06, AST-07, AST-08, AST-12, REAL-01, REAL-03, REAL-05, SEC-04, PHY-22 (inputs) · **Decisions:** D-003, D-004, D-007, D-020, D-028
**Read:** `CLAUDE.md`, `AGENTS.md`, `docs/workflow.md`; the whole change folder (proposal, design with every "As built" note of tasks 1.1–1.8, tasks, `review-physics.md` with its §8, both spec deltas); `docs/object-sheets/lines-roads-debris.md` §How thin objects read, §Shared, §L01, §L03; `docs/requirements.md` for every cited ID and TST-06; the full diff of `tools/blender/{build.py,lib/meta.py,lib/audit.py,lib/geometry.py,lib/export.py,generators/pole.py,generators/wire_span.py}`, `CatalogImporter.cs` (thin wire, ports, wires), `AssetPhysicsMeta.cs`, `ReviewCapture.cs`, `Shaders/ThinWire.shader`, the params, recipes, catalog and manifest, and every new or changed test.

Every check below was run by QA in its own worktree (no `unity/Library/` before the first Unity run; `git lfs checkout` first, 63/63 objects). Nothing from a handoff was reused. The brace gap, the catenary, the wire numbers, the planted rule, the wire tube both ways, the log's 10 mm on a leaned pole, the night silhouette's row coverage, the thin wire's column coverage and the side-by-side were re-derived or re-rendered with QA's own scripts in QA's scratchpad. The Unity views came from an uncommitted editor script, copied into `unity/Assets/_Project/Editor/QaScratch/` only for its run and deleted after (`git status` clean). Tools: Unity 6000.3.26f1 (Hub running, no editor on the project, every call piped), Blender 5.2.2 LTS, Python 3.14.6 with Pillow 12.3.0, pwsh 7.6.7. **No performance claim is made:** the laptop was on battery (`BatteryStatus 1`, Balanced) during the session.

## Verdict: CHANGES REQUESTED

Two blocking items, both small:
- **R1.** `L01_leaning`'s log misses the spec's "within 10 mm of the visual": 10.269 mm at its ground line. The change's own 10 mm test skips the leaned variants, where it fails.
- **R2.** The branch does not merge cleanly into `unity/main` (`tools/blender/tests/test_meta.py`), so `test_existing_tree_is_clean[origin/unity/main merged with HEAD]` fails.

Everything else passes:
- the builds, byte-identical on a second run, and their audit lines
- the brace gap at 1.611 m
- ports and wires through to the component
- the planted rule both ways
- the catenary at 5.055 m
- the night silhouette at 200 m and 1 km over quarter-pixel shifts
- the thin wire's fade
- every physics §8 item
- five of five mutations caught
- the hygiene

Letter L reads right in QA's side-by-side. J and D read as the sheet's targets, within the stand-ins listed in §7. Task 2.1 stays unticked until R1 and R2 are closed and re-checked.

## Blocking items

### R1. `L01_leaning`'s log is 10.27 mm off its collision; the 10 mm test skips the leaned variants
- **The requirement** ("L01 wooden pole", this change's delta, from the sheet's §L01 physics role): the physics metadata SHALL carry the log as tapering capsules within 10 mm of the visual.
- **Measured** with QA's `qa_lean.py`: the audit as `test_the_log_collision_is_within_10mm_of_the_log_both_ways` runs it, on the log's faces and segments alone, planted. The 9° case is `L01_leaning`'s log (lean 9° at azimuth 90).
  ```
  lean 9.0 deg: 3 segments
     audit collision[0] capsule: capsule->mesh 9.798 mm at (-0.0003, 0, -0.1128), tolerance 50 mm
     audit collision[1] capsule: capsule->mesh 8.193 mm at (0.0962, 4.9425, 0.7563), tolerance 50 mm
     audit collision[2] cylinder: cylinder->mesh 7.942 mm at (0.0204, 7.4208, 1.0902), tolerance 50 mm
     audit mesh: mesh->collision 10.269 mm at (0, 0, -0.1232) nearest collision[0], tolerance 50 mm
  lean 15.0 deg (inside the params' 0-15 range; check passes):
     audit collision[0] capsule: capsule->mesh 12.308 mm at (0.0001, 0, -0.1128), tolerance 50 mm
     audit mesh: mesh->collision 13.119 mm at (0, 0, -0.126) nearest collision[0], tolerance 50 mm
  ```
- **By hand:** on the trailing (−Z) side the leaned log's surface reaches the ground 19.3 mm below the foot along the axis (r·tan 9°), inside the lowest capsule's buried cap. The mesh corner there stands 0.121648 m from the axis. The cap's centre is the foot, its radius 0.112895 m: √(0.121648² + 0.019267²) − 0.112895 = **10.269 mm**, the audit's number.
- **The full build audit hides it.** Its mesh line reports the worst point of the whole pole (the insulator, 19.947 mm) against the 50 mm tolerance.
- **Why it escaped:**
  - The test checks `L01_straight` and `L01_stub` only.
  - `Pole.segments()` sizes the segments for the taper and ignores the lean.
  - The physics reading's planting bound (§5: terrain at most 5 mm under y = 0, from √(8.6² + δ²) ≤ 10 mm) assumed the upright pole's 8.6 mm. The leaned pole has no margin left.
- **To close it** (asset-artist):
  - Start the lowest segment far enough under the foot that its cylinder, not its cap, meets the ground on every side of a leaned log: about r·tan(lean) more, all of it covered by the planted rule. Or account for the lean in `segments()`.
  - Extend the 10 mm test to all four variants and to a 15° lean, printing the margin.
  - Rebuild L01. No other output should move.

### R2. The branch does not merge cleanly into `unity/main`
- **The failure:** `python -m pytest tools -q` gives 1 failed, 765 passed. The failure is `test_existing_tree_is_clean[origin/unity/main merged with HEAD]`: `CONFLICT (content): Merge conflict in tools/blender/tests/test_meta.py`.
- **The cause:** `unity/main` gained `p1-materials-per-part` (#90, #91), which adds `razor_wire` and `sandbag` to `REVIEW_MATERIALS`, on the lines this branch extends with `WIRE_FIELDS` and `WIRE_POSITIVE`. The `[HEAD]` target passes.
- **QA's trial merge** (a scratch worktree, never committed):
  - Keeping both sides (main's two ids and this branch's two constants) resolves it.
  - The merged tree's hygiene scan reads `768 files scanned, 0 findings`.
  - `test_meta.py`, `test_audit.py`, `test_pole.py` and `test_wire_span.py` pass on it: `159 passed`.
  - `test_audit.py` auto-merges (main's soft-material list beside this branch's planted test).
- **To close it** (orchestrator or asset-artist): merge `origin/unity/main` into the branch with that resolution, under a role identity (S9), and re-run `python -m pytest tools -q` with both `test_existing_tree_is_clean` targets green.

## 1. Spec scenarios

| # | Capability: scenario | Result | Evidence (QA's own run) |
|---|---|---|---|
| 1 | asset-pipeline: Ports written and read | **pass** | `L01_straight.assetmeta.json`: `hook_0`–`hook_3` at 7.355 / 6.855 / 6.355 / 5.855 m, each 19.000 mm off its insulator's axis and 75.0 mm up its base, direction ±Z outward, `max_diameter_m` 0.014. The leaning, braced and stub poles list four too. The prefab's component exposes them: EditMode `PolePrefabTests.StraightPole_ExposesFourOutwardPortsAtItsInsulatorNecks` passed |
| 2 | asset-pipeline: Wires validated | **pass** | `test_meta.py::test_convention_violations_name_the_rule`, cases `material must be 'cable', got 'steel'` and `needs a point list of at least 2 points, got 1` (and the missing EA or T_b, the slot, the damage). M3 shows the validator's edge-radius rule can fail |
| 3 | asset-pipeline: A buried capsule end on a planted pole | **pass** | QA ran the audit on the real `L01_straight` and `L01_stub` builds both ways. Planted: 0 problems. Unplanted: `collision[0] capsule: capsule->mesh 162.876 mm at (0, -0.1129, 0) exceeds the tolerance 50 mm` (stub: the capsule at 177.858 mm and `collision[4] box: box->mesh 311.664 mm at (0.2153, -0.3, -0.0152)`). The problem names the end, by its point, and its distance. M2 shows the test fails without the rule. Wording: S5 |
| 4 | asset-pipeline: Tube follows the polyline | **pass** | Build: `L03_medium: audit wires[0..3] 9 mm (slot aluminium): largest deviation 0.000 mm ... over 486 vertices, tolerance 4.5 mm`. L03 has no primitive. `test_wire_tubes_stay_out_of_the_primitive_audit`: K01's brick beside the span still measures 3.288 mm |
| 5 | asset-pipeline: Tube off its polyline | **pass** | `test_polyline_audit_fails_a_vertex_20_mm_off_naming_the_wire`: `wires[2] 9 mm (slot aluminium): largest deviation 20.000 mm at (20, ...) over 486 vertices exceeds the tolerance 4.5 mm`. M1 shows the min(5 mm, D/2) test fails at a flat 5 mm |
| 6 | L01: Built from params by one command | **pass** | `pwsh tools/build_asset.ps1 L01`: exit 0 twice (4:45 cold, then 2:29). The four variants' FBX, metadata, prefabs and review sheets. `git status` clean after each run. All 48 outputs (16 model files, 24 prefab files, 8 review sheets) sha256-identical between the runs |
| 7 | L01: Brace gap against the drone | **pass** | `L01_braced`: one gap, `narrowest_m` 1.610885; audit `largest inscribed circle 1.6107, tolerance 5 mm`. QA's own construction from the params: r(0) 0.119474 m, the brace's inner foot 2.184980 m, the apex 4.48006 m up, incircle 4A/P **1.610885 m** (0.115 mm from 1.611, 0.03 µm from the metadata). Clear below 2.9488 m at any yaw and 3.2351 m nose-first, which is the id's 2.95 / 3.24 m (2.9124 / 3.1952 m leaned 9°: the 2.91 / 3.20 m of the leaned test pole) |
| 8 | L01: Night silhouette kept to 1 km | **pass** | Distances: LOD1 35 m (0.075 · 935 / 35 = 2.0 px), LOD2 120 m, a 6-sided prism of the log alone (16 triangles, slot `wood` only), cull 1 200 m. QA's render: `L01_leaning` on L 1's sky #5d5e5d at 935 px/rad, MSAA 1, half-float target, across and along the line, four quarter-pixel shifts each. LOD2 active at 200 m and 1 km. **No empty row** in any shift at 200 m (31–32 rows) or 1 km (3–4 rows). Row sums 1.000–1.017 of 935 · 2r / d. Culled at 1 250 m, as the 1 200 m cull asks. **The check can fail:** with the coverage keyword off the same views give 22 of 32 rows empty (across, 200 m), 5–7 empty (along, 200 m), and nothing in three of four shifts at 1 km. The far LODs carry the thin-wire inputs: `test_the_far_lods_carry_the_thin_wire_inputs`, and EditMode `FarLods_DrawWithTheThinWireCoverageAndLodZeroStaysLit` on all four prefabs. The ends along the axis alias: S4 |
| 9 | L01: Matches letter L | **pass (by eye)** | QA's side-by-side `reference/_compare/L01-L03_qa.png` (local, never committed): L 1–3 beside `L01_leaning` at night, along the line from 16 m and 30° off from 18 m, at the feed's 450 px. **Matches:** the lean (about 9° in both), the short crossarm high on the pole (1.5 m under the 7.5 m top, 20 % of the visible pole, inside L's 16–28 %), the taper, no brace, the black core on the brighter sky. Seen from the other side the asset leans the other way, so L's top-left lean is a choice of view. **Differs, all feed or context (§7):** L's pole is wider (feed blur), bowed (the lens's barrel distortion) and noisy. The weathered log reads grey-silver in daylight: mean #827c74 over the overcast 1.5 m tile, against the sheet's #8a8478 albedo |
| 10 | L03: Built from params by one command | **pass** | `pwsh tools/build_asset.ps1 L03`: exit 0 twice (1:12, 1:13). `git status` clean; all 24 outputs sha256-identical. One tube per conductor: `L03_thin` 968 = 80 segments × 6 sides × 2 + 8 cap triangles; `L03_medium` 3 872 = 4 × 968; `L03_thick` 8 656 = 2 × (360 × 12 + 8), the drop's two cores each its own tube |
| 11 | L03: Catenary and clearance | **pass** | Committed records: lowest points 6.555000 / 6.055000 / 5.555000 / 5.055000 m, each 0.000 mm from its end height less 0.8 m (81 points; the mid-span sample is the vertex). The lowest is **5.055 m**; arc − chord 42.63 mm. `check` refuses a span under its floor (`conductor 1's lowest point 2.700 m is under the 3.5 m clearance of bare_al`). M5 (a 2 % larger sag) fails the 10 mm test. Record against tube: every LOD0 vertex lies 4.499–4.500 mm from the point list, so the points are the centre line, well within 5 mm. Clearance uses the centre line: S6 |
| 12 | L03: Nearly invisible, never shimmering | **pass** | QA's render: `L03_thin` broadside, black on J's sky #d6dfe7, four quarter-pixel shifts at each distance. **No empty column** at 10 m (1 080 columns), 50 m (673) or 200 m (169). Column sums 0.998–1.005 / 0.991–1.012 / 0.962–1.019 of 935 · D / d (0.468 / 0.094 / 0.023 px), totals steady within 0.05 / 0.05 / 0.20 % over the shifts. **The check can fail:** coverage off gives 486–680 / 577–617 / 155–169 empty columns. Lit under overcast against J's sky, the darkest pixel is #999798 at 2.2 m, #babec3 at 11 m, #cfd7df at 50 m, #d4dde5 at 200 m: a faint continuous line, fainter, then gone. The review sheet's 1.5 / 10 / 50 m tiles show one continuous faint line, with no breaks at 4096 px. J: QA's 15 mm drop 22 m off is darkest #9ea4aa (26.5 % under the sky), 28 m off #a9b0b6 (21 %). The sheet's J pick is #afb5be on #d6dfe7 (18 %); QA's own pick on J's drop is 28 %. Read side by side, the drop is J's 1–2 px faint grey line |

## 2. The physics reading's §8

| Item | Asked | As built (QA's check) | Closed |
|---|---|---|---|
| 1 | `axial_stiffness_n`, `breaking_force_n` on every record, required, positive | in `WIRE_KEYS`, `WIRE_NUMBERS`, validated (tests `pop`/`0.0`/negative). Values recomputed by hand: 5 mm solid steel 200 GPa × 19.6350 mm² = **3 926 990.8 N** and 390 N/mm² × 19.6350 mm² = **7 657.63 N** (reading 3.93e6 / 7 660); A-50 63 GPa × 49.5 mm² = 3 118 500 N, 8 198 N; the 2 × 16 drop 2 016 000 N and 8 198 / 49.5 × 32 = 5 299.72 N. Records match to the newton | yes (the 2 × 35 row superseded by item 2) |
| 2 | `L03_thick` 0.244 kg/m (2 × 35 mm²) | superseded by task 1.7: the sheet's 2 × 16 mm² drop, 15 mm, **0.1285 kg/m**, the published coil the reading itself calibrated on (§2.4); the reading's 20 mm conductor is gone, so its 0.244 no longer applies | superseded |
| 3 | wire `edge_radius_m` (bare A D/6, SIP core, steel none) + validator rule | A-50 0.0015, the drop 0.00375 (its 7.5 mm core), none on steel; validator optional, positive, ≤ `radius_m` (M3 caught) | yes |
| 4 | ports `max_diameter_m` 0.014, validated; `L03_thick` waits for a clamp | 0.014 on every port, validated positive; the drop's 15 mm > 14 mm is noted in design 3 and tested | yes |
| 5 | gap id in heights above the ground | upright `L01_braced` 2.95 / 3.24 m (equal in plane); leaned 9° test pole 2.91 / 3.20 m (QA: 2.9124 / 3.1952); M4 caught | yes |
| 6 | hooks and pins 8.5 mm; bands on the strap's centre line, 3 mm | 0.0085 on every hook and pin record (= their capsules' radius); bands 3.015 mm out of the outline at the 32-gon corners, 3 mm | yes |
| 7 | insulator rings at the drip edge | r 0.0325 m, 0.005 m up, 5 mm, glass; flush with the cylinder's side and bottom (test) | yes |
| 8 | wire tolerance min(5 mm, D/2), printed per wire | `tolerance 2.5 mm` / `4.5 mm` / `5 mm` in the build lines; M1 caught | yes |

**Two numbers recomputed** (g 9.80665):
- T = w·L²/(8·sag) on `L03_medium`: 0.135 × 9.80665 × 40² / (8 × 0.8) = **331.0 N** (the reading's 331.1 at g 9.81), k_mid 4T/L 33.10 N/m.
- `L03_thin`: 377.6 N. The drop: 157.5 N, k 31.5 N/m, matching the sheet's 2 × 16 row (160 N, 32 N/m).
- The breaking forces are recomputed in item 1.

**N-5** (the record no thicker than the tube) was checked by QA both ways:
- `L03_thin` and `L03_medium`: every vertex on its record's radius (2.500 / 4.500 mm).
- `L03_thick`: the cores reach 7.743 mm against the 7.5 mm record (0.243 mm outside, inside the 5 mm tolerance); the record stands nowhere beyond the tube.

There is no guard for it in the audit yet (S1).

## 3. The code

**`lib/meta.py`:**
- `PORT_KEYS` and `WIRE_KEYS` sit in schema order, and both lists are written empty when absent.
- The validator names the record and the rule.
- The `edge_radius_m` rule follows the fortifications reading's.
- K01, S04 and V13 gain only `"ports": []` and `"wires": []` (their FBX are unchanged).
- Sound.

**`lib/audit.py`:**
- **The planted rule:** `_above_ground` clips each primitive's cells at y = 0 before the search, so a buried cap or box is covered and nothing else changes. QA ran it both ways on the real poles (scenario 3).
- **The wire rule:**
  - Wire slots leave the primitive pass.
  - Each vertex goes to the wire of its slot it lies nearest outside of.
  - It is one-sided by design (the reading's §3), with the tolerance min(5 mm, D/2).
  - A wire with no vertices fails.
- Sound; S1 is the missing other side.

**`generators/pole.py`:**
- Readable, every constant labelled; `check` refuses every unbuilt state and idle key.
- The lean is a rigid rotation, and braced poles lean only across the brace.
- The brace triangle is the real clear space; the id is placed with the lean.
- One defect, R1: `segments()` ignores the lean, so the leaned log's lowest cap stands proud of the 10 mm.
- Minor: `L01_braced`'s wind box covers the log, not the brace log (S11).

**`generators/wire_span.py`:**
- The catenary is in the cancellation-free difference form, solved by bisection on log c.
- The ends sit on the ports within 1 µm (QA: 0.00–1.00 µm on all six ends).
- `MECHANICS` gives EA and T_b per type with labels; `check` refuses types without data.
- The cull follows the coverage.
- Sound; S6.

**`CatalogImporter.cs`:**
- **Thin wire:** `EnsureThinWireMaterial` copies the recipe's surface into `<recipe>_thin_wire.mat` (byte-stable on a second run: EditMode `SecondBuild_ChangesNoFile`, and QA's hashes of the five `*_thin_wire.mat` files and their metas, unchanged across both rebuilds).
- **Wires:** a wire slot must carry uv2 on every vertex or the import fails; a renderer drawing only wires casts no shadow; no collider stands for a wire (EditMode).
- **Ports and wires** are read into `AssetPhysicsMeta`.
- The thin-wire choice for other slots is inferred from uv2 being present (S2).
- `NamesMissingTexture` clears a dangling texture reference. That is fine, and outside this change's spec.

**`Shaders/ThinWire.shader`:**
- **Coverage:** the vertex stage widens the tube by (r + 1 px) / cos 45°, so the tent's support always rasterises. Per pixel the fragment takes T(x + h) − T(x − h) of a unit tent, a partition of unity, so the column sums equal the true width (QA measured 0.96–1.02 at 0.023 px, scenario 12).
- **The lit normal** is the round conductor's at that offset.
- **The shadow caster** keeps the texels within the member's true radius from uv2 along the light. It takes URP's depth bias, and its normal bias as a 0.6 inset. Wires are `ShadowCastingMode.Off` from the importer.
- The forward pass is transparent with no depth write; that is fine for wires and has consequences for the far pole (S3).

**TST-06, the tests are tested.** QA mutated the code under review five times; every mutation was caught, and each was reverted with `git checkout -- <file>` (`git diff --quiet` → 0):

| | Mutation | Test that failed |
|---|---|---|
| M1 | `lib/audit.py`: wire tolerance a flat 5 mm (`tolerance = WIRE_TOLERANCE_M`) | `test_wire_span.py::test_the_tolerance_is_half_a_thin_wires_diameter` |
| M2 | `lib/audit.py`: planted rule off (`if False:` before `_above_ground`) | `test_audit.py::test_a_planted_log_buried_below_the_ground_is_no_wall_and_unplanted_it_is` |
| M3 | `lib/meta.py`: `edge_radius_m` upper bound dropped (`0 < edge`) | `test_meta.py::test_convention_violations_name_the_rule[... at most radius_m 0.0045, got 0.005]` |
| M4 | `generators/pole.py`: gap id from the in-plane heights, not placed with the lean | `test_pole.py::test_the_brace_gaps_id_gives_its_heights_above_the_ground` (`...3.24m` ≠ `...3.20m`) |
| M5 | `generators/wire_span.py`: sag × 1.02 | `test_wire_span.py::test_medium_span_hangs_from_l01_ports_and_sags_08_m` (`0.016 <= 0.01`) |

The tests assert the spec's literal numbers (1.611 m, 5.055 m, 0.014 m, the EA and T_b figures) and print margins. The gap they leave is R1's: no variant-wide 10 mm check.

## 4. The whole tree

| Check | Result |
|---|---|
| `python -m pytest tools -q` | 1 failed, 765 passed (21:26 under load); the failure is R2. `test_existing_tree_is_clean[HEAD]` passes |
| EditMode (`-runTests -testPlatform EditMode`) | exit 0; `result=Passed total=45 passed=45 failed=0 skipped=0`, including `PolePrefabTests` (7), `WireSpanPrefabTests` (3), `ThinWireMaterialTests` (4), `ReviewCaptureTests.StageBuild_*` (2), `CylinderColliderTests` (2) |
| `pwsh tools/build_asset.ps1 K01` / `S04` / `V13` | exit 0 each (0:59, 5:02, 5:38); `git status` clean after them, so their committed FBX, metadata and prefabs are unchanged. On the branch their only output change against `unity/main` is the empty `ports` / `wires` lists (+2 lines per metadata and prefab). Their review sheets were not compared with a `unity/main` build: no baseline in this session. Their render inputs are unchanged: the same meshes and materials, no uv2, and the stage's lift unchanged for bounds at or below y = 0 |
| `python tools/assets/fetch.py --verify` | `OK`, exit 0; three CC0 Poly Haven sets pinned at 1k (`rough_wood`, `rough_concrete`, `rusty_metal`), `CREDITS.md` generated with them, `weathered_brown_planks` and `metal_plate` gone from the tree, the manifest and the credits |
| `python tools/hygiene/precommit.py --tree HEAD` | `762 files scanned, 0 findings`, exit 0 (the trial-merged tree: 768, 0) |
| `openspec validate p1-l01-l03-pole-wires --strict` | `Change 'p1-l01-l03-pole-wires' is valid`, exit 0 |
| Commits | 52 in range, every one subject-only (empty body) and every non-merge under a role identity: Asset Artist, Tech Artist, Physics Engineer, Orchestrator. **Two** merge commits (not one) are under the local git user `SanityEclipse000 <max@SANITYECLIPSE00>`: `88e43b6` (the reading's sub-branch) and `e4a84d6` (the thin-wire sub-branch), both pushed; S9 |
| Tracked paths | nothing from `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/`; no OPSEC term (the scan); no reference file name anywhere in the change |

**The two merge commits do not block.** Their subjects are git's own merge subjects, which the commit-msg hook exempts (`MERGE_SUBJECT`). The repository owner's identity is already on `unity/main` through its 59 GitHub PR merges. The orchestrator does not rewrite pushed history. The rule they break is the role identity; S9 asks for it on every later merge, R2's included.

## 5. Requirements

| ID | Status | Note |
|---|---|---|
| AST-02 | met | the lines sheet's §L01 / §L03 drive every number; dimensions inside its ranges |
| AST-03 | met | one command per asset, params files per variant; a parameter edit was not re-run end to end this time (the path is unchanged; the generators' tests cover params to outputs) |
| AST-05 | **partly met** | primitives, materials, snags, gap, wind, no landable, LOD distances all present; the log's 10 mm on `L01_leaning` missed (R1) |
| AST-06 | partly met | the damage dial and its items are params, range-checked and refused until built (a proposal non-goal for G1) |
| AST-07 | met | UVs in metres, the wood tile closes round the log, no chequerboard at 10 m in the sheets |
| AST-08 | partly met | textures 1k (≤ 2K); L01 three LODs (892–1 206 / 22–38 / 16 tris), L03 two (968–8 656 / 324–2 576); no per-class budget table exists in the pipeline to fail over (S8) |
| AST-12 | met | sheets at 1.5 / 10 / 50 / 200 m, turntable, overlay, day and overcast for all seven variants; side-by-side made (local) |
| REAL-01, REAL-03 | met for G1 | L, J, D read by eye in QA's side-by-side; differences listed in §7 |
| REAL-05 | partly met | wires are tubes of their real diameter, hooks, insulators, bands and the brace are geometry, the opening is the real clear space; collision follows the visual except R1's 0.27 mm; the brace gap is checked by its inscribed circle (1.611 m against the drone section's 0.895 m circle), not yet flown by the gap probe (S12) |
| SEC-04 | met | `fetch.py --verify` OK; CC0 only |
| PHY-22 (inputs) | met | D, mass, sag, EA, T_b and the point list per conductor, all read through to the component |

## 6. Suggestions (non-blocking)

- **S1, the wire audit both ways** (asset-artist): add the reading's N-5 guard. Each wire's farthest tube vertex must lie within its tolerance of the record's envelope, so a record thicker than its tube (an invisible wall around a drawn wire) fails. The built spans pass it today.
- **S2, an explicit thin-wire flag** (tech-artist): the importer picks the thin-wire material for any slot whose vertices all carry uv2.x > 0, and the shader reads TEXCOORD1 as the radius. uv2 is Unity's lightmap channel, so an asset that ever gains lightmap UVs would silently draw as a thin wire. Name the thin-wire LODs or slots in the metadata instead.
- **S3, the far pole in the transparent queue** (tech-artist): LOD1 and LOD2 draw transparent with no depth write. So:
  - depth-based effects (and the feed, if phase 4 reads depth) do not see the pole beyond 35 m
  - it sorts per object against other transparents
  - inside the LOD1 renderer the crossarm and log faces overlap in triangle order
  
  Harmless in today's views; confirm before the feed.
- **S4, the far pole's ends alias along the axis** (tech-artist): the coverage filters across the member only. At 1 km the whole silhouette's coverage moves 4.3 % (across) to 16 % (along) over quarter-pixel shifts as the top row pops, while the interior rows hold within 0.5 %. Consider extending the tent past the top cap.
- **S5, the spec's wording** (orchestrator, at archive): the planted scenario's "a capsule that ends 0.17 m below the ground" describes the cap's distance from the open mesh (r√2; QA measured 0.163 m). The cap itself ends 0.113 m down.
- **S6, clearance at the underside** (asset-artist): `check` compares the centre line with the ПУЕ figures. Comparing `lowest − radius` costs nothing; the difference is up to 16 mm.
- **S7, the component's snag record** (asset-artist): `AssetPhysicsMeta.SnagFeature` drops `material` (since V01's rule) and now `edge_radius_m`. Expose both before anything in Unity reads snags.
- **S8, AST-08's table** (orchestrator): no per-class triangle budget exists for the build to fail over, for lines or for any earlier class. Schedule the table; this change's numbers are in §5.
- **S9, merge identity** (orchestrator): pass `-c user.name="Orchestrator" -c user.email="orchestrator@450fpv.local"` on every merge, including R2's. `88e43b6` and `e4a84d6` stay as they are.
- **S10, stale as-built lines** (orchestrator, at archive), superseded but still in the text:
  - Design 4's task 1.7 note and task 1.7's own note say the far pole casts no shadow; task 1.8 added the caster.
  - The proposal still puts the brace on `L01_leaning`.
- **S11, the brace's wind volume** (asset-artist): `L01_braced`'s solid wind box covers the log only. The 0.16 m brace log has none. It is negligible under the 2 m grid.
- **S12, the gap probe** (world-builder or physics, phase-1 step 8): fly the drone-sized capsule through `L01_braced`'s triangle to close REAL-05's probe for this opening.

## 7. What the pilot sees at G1 that does not yet copy the references

1. **Every pole is the I state.** Weathered and intact: no fragment gouges, broken insulators, cut or hanging conductors (D), and no snapped or charred pole (X). L's line is a war-torn one; only its lean is copied.
2. **One log look.** `weathering` is fixed at 0.5 (the recipe's grey-silver tint). There is no creosote variant and no derived grain or damage mask; `rough_wood`'s checks stand in.
3. **No feed.** L and D are feed clips; G1 shows noclip renders, with no PAL noise, blur, 450 px resolution or lens distortion.
   - The night pole is a clean black on a flat #5d5e5d sky. L 1 is #050505–#151513 on a clouded, noisy sky.
   - The pole reads thinner than L 1's 3.5–4.7 feed px until the feed's blur exists.
   - The pole stays straight where L's bows with the lens.
4. **The SIP drop has no fittings.** `L03_thick` (15 mm) hangs from a TF-20 hook (14 mm groove) with no clamp, and its house end hangs in the air at 3.5 m (no house wall port, no `sip_bracket`).
5. **Wires are intact and static.** No wind sway (an open point for phase 7d), no cut or down states. D's dark cable is L04's slack cable; QA compared against the intact drop, which reads lighter at 3.5 m (#42413f on #adaaa4 against D 12's #1e1816).
6. **Conductors are flat colours.** Aluminium #8e9193, steel #5e4a3a, SIP #1f2022. There is no stranding normal, rust texture or oxidised tie; beyond a few metres the coverage, not the albedo, sets the look.
7. **Not built:** a lamp bracket, glass insulators, the A-frame brace, the 10 kV crossarm and the concrete pole (L02).
8. **Standalone stations.** Two poles and a span are not yet a gallery station (phase 2). The context of L (crest, bush, lone tree) and of J (houses, debris) is absent.

## Commands run (outputs above, condensed)

```
git fetch origin; git switch unity/art/p1-l01-l03-pole-wires (cf778bb); git lfs checkout   -> 63/63 objects
pwsh tools/build_asset.ps1 L01   (x2)   -> build_asset: OK (L01), exit 0; git status clean; 48/48 files identical
pwsh tools/build_asset.ps1 L03   (x2)   -> build_asset: OK (L03), exit 0; git status clean; 24/24 files identical
pwsh tools/build_asset.ps1 K01 | S04 | V13 -> OK each, exit 0; git status clean
python -m pytest tools -q -p no:cacheprovider -> 1 failed, 765 passed (R2)
Unity -batchmode -nographics -runTests -testPlatform EditMode -> Passed 45/45, exit 0
python tools/assets/fetch.py --verify -> OK
python tools/hygiene/precommit.py --tree HEAD -> 762 files scanned, 0 findings
openspec validate p1-l01-l03-pole-wires --strict -> valid
python -I qa_checks.py   (ports, gap, planted, catenary, wire numbers, N-5, port seats)
python -I qa_lean.py     (R1)
Unity -batchmode -executeMethod Fpv450.QaScratch.QaViews.Run   (night rows, wire columns, look renders; script deleted)
python compose.py -> reference/_compare/L01-L03_qa.png (2699 x 2811, local only)
git merge-tree --write-tree origin/unity/main HEAD -> CONFLICT tools/blender/tests/test_meta.py
trial merge in a scratch worktree -> precommit 768 files, 0 findings; 159 passed (test_meta, test_audit, test_pole, test_wire_span)
mutations M1-M5 -> each caught, each reverted (git diff --quiet: 0)
```

## Re-check of task 1.9

**Reviewer:** qa-engineer, fresh (did none of 1.9, D-034) · **Tip:** `39cedee` · **Range:** `f1f8c51..39cedee`, the merge `4bbeccd` read against its first parent · **Read:** this review's R1, R2 and S1–S12; task 1.9 and its as-built note; design 3, 4, 7 and 10's "As built (task 1.9)" notes; both spec deltas as 1.9 left them; the diff of `pole.py`, `wire_span.py`, `lib/audit.py`, `lib/meta.py`, `CatalogImporter.cs`, `AssetPhysicsMeta.cs`, every changed test, and every changed metadata and prefab.

Every check was run by QA in its own worktree (`git lfs checkout` first, 63/63 objects). QA's scripts (`qa_r1.py`, `qa_s1.py`, `qa_flags.py`, `qa_hash.py`) ran from the scratchpad. The one scratch worktree, for the old-code test run, was removed after (`git worktree list` shows it gone). Tools: Unity 6000.3.26f1 (Hub running, no editor on the project, every call piped), Blender 5.2.2 LTS, Python 3.14.6. **No performance claim is made** (the laptop was on AC, `BatteryStatus 2`, but nothing here is timed).

### Verdict: APPROVED

R1 and R2 are closed. S1, S2, S6, S10 and S11 each close the item asked, and nothing else. S5 was closed by the orchestrator in `f1f8c51`. Three non-blocking notes for the orchestrator are listed below (N1–N3), plus one for the V01 branch (N4). Task 2.1 can be ticked.

### R2: closed
- `git merge-base --is-ancestor origin/unity/main HEAD` gives exit 0 (`35ea833`). `git merge-tree --write-tree origin/unity/main HEAD` gives exit 0, and its tree `22cff0c` is HEAD's own tree.
- `git show --remerge-diff 4bbeccd` names one hand-resolved file, `tools/blender/tests/test_meta.py`. The resolution keeps main's `"razor_wire", "sandbag"` and the branch's `WIRE_FIELDS` and `WIRE_POSITIVE`, both sides whole. Nothing else was edited in the merge.
- `git diff --stat f1f8c51 4bbeccd` is the same as `git diff --stat 5ef4206 35ea833` (16 files, 602 insertions, 33 deletions): the merge brings in main's change exactly.
- The merge is under `Asset Artist <assets@450fpv.local>`, author and committer, subject only, as task 1.9 asked.
- Both hygiene targets pass: `test_existing_tree_is_clean[HEAD]` and `[origin/unity/main merged with HEAD]`.

### R1: closed
**The depth, re-derived.** In the upright frame, the 12-gon's corner on the trailing side lies at axial −s, g·r(−s) out, with g = 2 / (1 + cos 15°) = 1.017332 and r(−s) = r(0) − slope·s (slope signed, negative up the log). After the lean θ, its height is −s cos θ + g r(−s) sin θ. Setting it to 0 gives s = g r(0) tan θ / (1 + g·slope·tan θ), which is `Pole.sink()`. QA solved it independently by fixed-point iteration from the params alone, and measured it on the built mesh (the lowest axial coordinate of the log's ground vertices). The three agree to 0.1 µm: **19.2671 mm** at 9° (QA's earlier 0.019267 m) and **32.6145 mm** at 15°. At azimuths between the 12-gon's corners (15°, 45°, 200°), the mesh meets the ground less deep (31.50–32.12 mm) than `sink()` (32.61 mm). Starting deeper is safe: the planted rule covers everything below y = 0, and the log stays within 8.68 mm.

**The log alone, both ways** (QA's `qa_r1.py`, the first review's `qa_lean.py` method: the log's 13 LOD0 faces against its segments, planted). The full build audit gave 0 problems in every case.

| Case | Segments | Sink (mm) | Worst (mm) | Before 1.9 (mm) |
|---|---|---|---|---|
| `L01_straight`, `L01_braced` (upright) | 3 | 0 | 8.649 | 8.649 |
| `L01_stub` (upright) | 4 | 0 | 8.503 | 8.503 |
| `L01_leaning` 9° (and straight, braced at 9°) | 3 | 19.267 | **8.669** | 10.269 |
| any of `straight` / `leaning` / `braced` at 15°, az 90 | 3 | 32.615 | **8.681** | 13.119 (straight, leaning) |
| `L01_stub` at 9° / 15° | 4 | 20.969 / 35.502 | 8.524 / 8.537 | not run |
| `L01_leaning` 15° at az 0 / 15 / 45 / 200 / 270 | 3 | 32.615 | 8.674–8.681 | not run |

All four variants hold the 10 mm at 9° and at 15°, with at least 1.319 mm of margin. The worst point is now the log's own corner at the ground, (0, 0, −0.1232), 8.669 mm from the lowest capsule's side. Before, it was 10.269 mm from that capsule's cap.

**The test.** `test_the_log_collision_is_within_10mm_of_the_log_both_ways` is parametrized over six cases: the four variants, and `L01_straight` and `L01_leaning` at 15°. Its numbers are literal:
- the segment counts 3 / 4
- the sinks 0.019267 / 0.032615 m (to 1 µm)
- the 10.0 mm bound
- the lowest segment's foot at `place((0, −sink, 0))` (to 2 µm)

QA ran it against the old `pole.py` (from `4bbeccd`, in a scratch worktree): **6 failed**. Three of those failures are the real check: the leaned cases fail the 10 mm at 10.269, 13.119 and 13.119 mm. The three upright cases fail only on `AttributeError: 'Pole' object has no attribute 'sink'`. They passed the 10 mm before, as they should. So "6 of 6" is true, and 3 of 6 is the meaningful count. The test leaves out the braced and stub variants at 15°. QA ran both: 8.681 and 8.537 mm. Not blocking.

**Outputs.** Only `L01_leaning`'s three log records moved. For example, the lowest capsule moved from r 0.112895 to 0.112979 m and from h 2.725789 to 2.732381 m. Its cylinder asset was renamed to `r0.086596_h2.506422`, which is (7.5 + 0.019267) / 3. Every other L01 change is S2's flag or S11's box.

### S1, S2, S6, S10, S11
- **S1, the wire audit both ways: closed as asked.**
  - Each wire's farthest vertex must reach the record's radius within min(5 mm, D/2), and the line prints the reach. The build lines: 2.500 of 2.5, 4.500 of 4.5, 7.743 of 7.5 mm.
  - The new test fails a 20 mm record on the 9 mm tube and passes an 18 mm one, both by literal message.
  - Read with QA's `qa_s1.py`, the guard holds the invisible wall to the reading's 5 mm drone bound. It cannot catch a doubled record on a wire of 10 mm or less. On a 5 mm tube a 12 mm record passes, and on the 9 mm tube anything up to 19 mm passes (19.1 mm fails). That is N-5's own wording. The committed records are pinned by literal diameters in `test_wire_span.py` (0.009, 0.015) and in EditMode (uv2 4.5 mm). See N2.
- **S2, an explicit thin-wire flag: closed.**
  - `meta.assemble` writes `thin_wire` on every LOD entry. It is false unless the generator returns `"thin_wire"`; `pole` sets `[F, T, T]` and `wire_span` `[T, T]`.
  - `validate` refuses a missing or non-boolean flag, and `assemble` refuses a list of the wrong length.
  - The importer reads it with `Json.Bool`, which is required. `MeshChild` picks `EnsureThinWireMaterial` by `thinWire` alone. `CarriesRadius` is now only a check inside a flagged LOD, and micro meshes always pass `false`. No other `uv2` read picks a material (grep over `unity/Assets/_Project`).
  - EditMode `ThinWireFlag_NotTheUv2_PicksTheMaterial` passed. With its LODs unflagged, the real `L03_medium` FBX draws URP/Lit while uv2 still holds the radius, and with its wires the import fails, naming `slot aluminium`. Its throwaway folder is gone after the run (`git status` clean).
  - **K01, S04 and V13 change by the field alone.** Over their 5 metadata and 5 prefab files, the only added lines are 14 × `"thin_wire": false` and 14 × `thinWire: 0`, plus the trailing commas on the `switch_distance_m` lines they follow. Their FBX, materials and review inputs are untouched. Every committed metadata carries the flag: K01, S04, V13 false; L01 `[F, T, T]`; L03 `[T, T]`.
- **S6, clearance at the underside: closed.** `check` subtracts `diameter_mm / 2000` from the lowest centre. The literal test refuses the 5 mm wire at 4.3015 m (underside 3.499 m) and builds it at 4.303 m. The changed message (2.695 m) is updated. The committed spans keep their margins.
- **S10, stale as-built lines: closed.** Design 4 and 10, and tasks 1.4 and 1.7, mark the "no shadow caster" and brace-on-`L01_leaning` lines superseded, and name the task that superseded them. The proposal was fixed by the orchestrator in `f1f8c51`, along with S5's 0.113 / 0.163 m. A grep of the change folder finds no unmarked line left.
- **S11, the brace's wind volume: closed.** `L01_braced` has a second solid box on the brace capsule. QA recomputed it:
  - centre (1.136625, 2.4375, 0), the midpoint of the foot (4.875 tan 25°, 0) and (0, 4.875)
  - size 0.16 × 5.378967 × 0.16 m (4.875 / cos 25°)
  - rotation 25° about +Z (sin 12.5° = 0.21644)
  
  The log's box is unchanged, and the EditMode braced-pole test checks both.

### The spec wording (the orchestrator's)
Both deltas match the code. The "Record wider than its tube" and "The flag, not uv2" scenarios pass, and the "a solid wind volume per log" and underside lines hold. Two points:
- **N1, the flag is misplaced.** `thin_wire` is a field of every LOD entry of every asset, and it drives the importer for the pole's far LODs, which have no wires. It sits under "Wires are audited against their polyline", a collision-audit requirement. Move the sentence and its scenario before archive. They belong under a MODIFIED "Asset metadata contract" ("LODs with triangle counts, switch distances and a `thin_wire` flag"), with the material rule beside "Import to a prefab", or under an ADDED requirement of their own, such as "Thin-wire LODs are named in the metadata". The rule itself is right.
- **N2, one parenthetical claims too much.** "(so no record is wider than the wire drawn)" should read "so no record stands more than that tolerance beyond the wire drawn". As built, and as N-5 asked, a record of 10 mm or less always passes the reach check.

### Other notes (non-blocking)
- **N3, a typo from `f1f8c51`** (proposal, Physics metadata): "`glass` (porcelain, the physics sign-off) and `glass`" names glass twice.
- **N4, what `p1-v01-locust` must do when it merges.**
  - It is not yet up to date with `unity/main`. A trial `git merge-tree` of it into this tip conflicts in:
    - `build.py`, `lib/audit.py`, `lib/export.py`, `lib/geometry.py`, `lib/meta.py`, `test_meta.py`
    - `CatalogImporter.cs`
    - all three `V13_*.assetmeta.json`
  - Its `V01_*.assetmeta.json` LOD entries carry no `thin_wire`, so after this change the importer refuses them (`"thin_wire" must be true or false`).
  - Neither it nor its `--farcards` or `--impostor2` sub-branches use a second UV (grep), so `false` everywhere is right for V01 and V13.
  - **When it merges:**
    - keep this change's `thin_wire` lines in `meta.py` (the default, the validator, the `lods` entry) and `CatalogImporter.cs` (`MeshChild`'s `thinWire`, with `false` for micro meshes)
    - pass the LOD's flag or `false` in any new renderer path of its own (far cards, impostors)
    - **never hand-resolve the metadata JSON:** rebuild V13 and V01 with `pwsh tools/build_asset.ps1` on the merged tree, so each LOD gains `"thin_wire": false` and each prefab `thinWire: 0`, and nothing else moves on V13 beyond V01's own changes
  - Its QA re-check should diff V13's prefabs for exactly that.

### Commands run (key lines)
```
git fetch origin; git checkout -B qa-l01-recheck origin/unity/art/p1-l01-l03-pole-wires (39cedee); git lfs checkout -> 63/63 objects
git merge-base --is-ancestor origin/unity/main HEAD -> exit 0
git merge-tree --write-tree origin/unity/main HEAD -> 22cff0c6e1029d5e1e325fc542159df030c0b368 (= HEAD^{tree}), exit 0
git show --remerge-diff 4bbeccd -> one file, tools/blender/tests/test_meta.py, both sides kept
python -I qa_r1.py -> sink code = QA's = mesh (19.2671 / 32.6145 mm); log-only worst 8.669 (9 deg), 8.681 (15 deg), 8.524 / 8.537 (stub); full audit 0 problems everywhere
old pole.py (4bbeccd) under the new test -> 6 failed: 10.269, 13.119, 13.119 mm (leaned) and 3 x AttributeError 'sink' (upright)
python -I qa_s1.py -> thin 12 mm record passes, medium 19 mm passes / 19.1 mm fails, thick 25.1 mm passes (the 5 mm bound)
python -m pytest tools -q -p no:cacheprovider -> 775 passed in 1545.17s, exit 0
python -m pytest tools -q -k test_existing_tree_is_clean -rA -> PASSED [HEAD], PASSED [origin/unity/main merged with HEAD]
pwsh tools/build_asset.ps1 L01 (x2) -> build_asset: OK (L01), exit 0 both (2:35, 2:19); git status clean after each;
    146 files (L01 models, prefabs, every material, review sheets) sha256-identical run 1 -> run 2;
    L01_braced gaps[0] narrowest_m 1.6109, largest inscribed circle 1.6107; importer "thin wire L01_*_LOD1 wood, L01_*_LOD2 wood"
pwsh tools/build_asset.ps1 L03 (once) -> build_asset: OK (L03), exit 0 (1:01); git status: only this review.md modified;
    L03_thin wires[0] 5 mm: largest deviation 0.000 mm ..., its tube reaches 2.500 mm of the record's 2.5 mm radius, tolerance 2.5 mm
    L03_medium wires[0..3] 9 mm: 0.000 mm, reaches 4.500 mm of 4.5 mm, tolerance 4.5 mm
    L03_thick wires[0] 15 mm: 0.243 mm, reaches 7.743 mm of 7.5 mm, tolerance 5 mm
    importer: "thin wire L03_*_LOD0 <slot>, L03_*_LOD1 <slot>", "wires without colliders (... -> Fpv450/ThinWire)"
    L03's second build skipped: L01's two runs match, and L03's outputs are unchanged since 31ff8a1, inside 1.9's own double build
pwsh tools/build_asset.ps1 K01 (spot check) -> build_asset: OK (K01), exit 0; git status: only this review.md modified (no "thin wire" on K01)
Unity -batchmode -nographics -runTests -testPlatform EditMode -testResults Logs/editmode.xml -> exit 0;
    result="Passed" total="46" passed="46" failed="0" skipped="0", incl. ThinWireFlag_NotTheUv2_PicksTheMaterial, BracedPole_*, FarLods_* (4)
python tools/assets/fetch.py --verify -> OK, exit 0
python tools/hygiene/precommit.py --tree HEAD -> 769 files scanned, 0 findings, exit 0
openspec validate p1-l01-l03-pole-wires --strict -> Change 'p1-l01-l03-pole-wires' is valid, exit 0
git log 1fb2499..39cedee --first-parent -> 8 commits, all subject-only, no trailers; Asset Artist (7), Orchestrator (f1f8c51)
git diff --name-only f1f8c51..39cedee | reference/ Library/ Temp/ Logs/ UserSettings/ -> 0 paths
```
