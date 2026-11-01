# Review: p1-h01-adobe-house (task 2.1)

**Reviewer:** qa-engineer, fresh (did none of the work, D-034) · **Branch:** `unity/art/p1-h01-adobe-house` · **Tip:** `f8171f7` · **Range:** `46d9660..f8171f7` (55 commits, 3 of them merges; 242 files)
**Requirements cited:** AST-02, AST-03, AST-04, AST-05, AST-06, AST-07, AST-08, AST-12, REAL-01, REAL-03, REAL-05, SEC-04, PR-5 · **Decisions:** D-003, D-004, D-020, D-028, D-032, D-040, D-041
**Scope (D-040):** the I and D states and the K02/K03 kits. X and task 1.4b are reviewed with their own work after G1. A look miss is a G1 stand-in; a finding on physics, tests, metadata or spec text blocks.
**Read:** `CLAUDE.md`, `AGENTS.md`, `docs/workflow.md`, `docs/team.md`, the `karpathy-guidelines` skill; D-040 and D-041; the whole change folder (proposal, design with its 1.2 note, the three spec deltas, `tasks.md` with every as-built note of 1.1–1.6); `houses-and-yard.md` §Shared and §H01; `lines-roads-debris.md` §Shared debris kit, §K02, §K03; `requirements.md` for every cited ID; the R01 and V01 reviews for the format; the code each check leans on (`lib/joinery.py`'s door and gaps, `lib/roof.py`'s `_bays` and `_pick_breaks`, `lib/walls.py`'s constants and U loop, `lib/rubble.py`'s constants, `lib/audit.py`'s gap check, `generators/house.py`'s `verdict` and report, `ReviewCapture.cs` and `ReviewMeta.cs` at `221f83b`) and the tests behind each scenario.

**How QA ran it.** Everything below ran in the change's worktree after `git lfs checkout`. Nothing from a handoff was reused.
- **QA's own scripts** (scratchpad, never committed; plain Python on the shipped metadata, no code shared with `tools/blender`): `qa_door.py` (the leaf's throat), `qa_gaps.py` (a segment along each gap's normal on a 10 mm grid: clear inside the polygon, blocked 8 mm outside it), `qa_roof_open.py` (every opening through the three slopes that is not a gap), `qa_sizes.py` (the AST-04 sizes), `qa_heap.py` (heaps, snag materials, kit records), `qa_spread_wrong.py` (the spread test against wrong models), `qa_lod_holes.py` (Blender, on the shipped FBX: each opening per LOD, the ridge), `qa_tiles.py` (sheet crops), `qa_mask_exp.ps1` (the `render_loss` experiment).
- **The look:** QA read the local comparison sheet task 1.5 left in `reference/_compare/` (E 1, 7, 10, 14, 15, 17 and J, each against task 1.4's and task 1.5's builds). QA's crops of it went into that folder and were deleted after; QA opened no clip and no contact sheet. Colour means were read from the sheet in memory.
- **Tools:** Unity 6000.3.26f1 (Hub running, every call piped), Blender 5.2.2 LTS, Python 3.14.6 with Pillow 12.3, pwsh 7.
- **Power and memory:** AC before every Blender and Unity run (`BatteryStatus.PowerOnline` True), at least 2.5 GB free before each start. Other agents' EditMode runs overlapped part of the H01 build. **No performance claim is made.**

## Re-check at `be14dd4` (2026-11-01): APPROVED for I, D, K02 and K03

**Reviewer:** qa-engineer, fresh, for B1 and B2 only · **Range:** `f8171f7..be14dd4` (6 commits: the first pass `94c3a45`, the orchestrator's spec lines `ecce570`, the artist's fix `5e05d9d`, the rebuild `7ee0abf` and two notes) · **Read:** `CLAUDE.md`, the first pass below, `ecce570`'s three lines, task 1.6's notes, `joinery.door` and `_throat`, the new tests, `Rubble.place`, `walls.outer_and_liner`, `roof._far_cover`, and on `origin/unity/main` `IWorldQuery.cs`, `AssetPhysicsMeta.cs`, the `asset-pipeline` spec and `p6-world-query-gallery`.

**How QA ran it.** The orchestrator lent the GPU to the pilot mid-task, so **no Blender and no Unity ran**: no build, audit or LOD probe was re-run. That leaves no hole here. `git diff f8171f7 be14dd4` changes only the door gap in both states' `.assetmeta.json` and prefabs (34 lines), plus `joinery.py`, `test_house.py` and the change's docs. So the FBX, masks, materials and every shape, snag and other gap are the ones the first pass built, audited and probed. QA's new scripts (scratchpad, never committed; plain Python on the shipped files, no code from `tools/blender`):
- `qa_door2.py`: the leaf's board box and the far jamb's arris from the collision, the exact distance between them, the widest sphere through the doorway over every shape (a max-min path on a 10 mm grid), and the gap and the prefab against both
- `qa_door_probe.py`: the first pass's gap probe on the new polygons
- `qa_door_axis.py`: straight lines along the gap's normal and the wall's

AC power, 3.6 GB free.

**Verdict:**
- **B1 is closed.** Both door gaps are now the throat from the far jamb's room-side arris to the leaf, within 1 µm of QA's own distance. The shipped verdicts read I "no" (0.368 m) and D "passes at any yaw, 7 mm a side on the diagonal" (0.720 m). The tests derive the throat from the leaf's box and fail the old 0.80 m rule at every swing.
- **B2 is met by the amended text, all three items.** None needs a fix or more wording.
- **The new normal breaks nothing.** No code on the trunk reads a gap's normal, and the trunk's contract already defines a gap as the throat, with a normal of free sign.
- **Task 2.1 is ticked.** Left for the change: X and task 1.4b (N6), and the pre-PR merge of the trunk (N7: now 206 commits ahead, no file in common with the branch's 61).

### B1: the door's gap is its throat
**QA's distance against the shipped gap** (`qa_door2.py`):
```
== H01_I: leaf centre [-0.046, 1.005, -2.364], axis [0.866, 0.0, 0.5] (30.00 deg from the wall), far jamb arris x 0.400 z -2.550
  arris to leaf box, min over the threshold..head: 0.368000 m; by hand 0.8 sin 30 - 0.032 = 0.368000
  gap: 2 points on the arris, leaf distance per point [0.368136, 0.01, 1e-06, 0.368], width 0.368000, height 1.900 (0.05..1.95), narrowest_m 0.368
  normal [-0.866025, 0.0, -0.5]: . leaf axis -1.000000, . leaf face normal -0.000000, . throat segment +1.00e-07, |n| 1.000000, the wall's normal [0,0,-1] . n +0.5000
  QA's verdict from narrowest_m: no; nose-first -103.0 mm a side
  prefab's door gap equals the metadata's: True
== H01_D: leaf centre [-0.245, 1.0, -2.171], axis [0.342, -0.013, 0.94] (70.00 deg from the wall), far jamb arris x 0.400 z -2.550
  arris to leaf box, min over the threshold..head: 0.719754 m; by hand 0.8 sin 70 - 0.032 = 0.719754
  gap: 2 points on the arris, leaf distance per point [0.719784, 0.00656, 0.003292, 0.719761], width 0.719754, height 1.900 (0.05..1.95), narrowest_m 0.719754
  normal [-0.34202, 0.0, -0.939693]: . leaf axis -0.999922, . leaf face normal -0.000001, . throat segment +3.17e-07, |n| 1.000000, the wall's normal [0,0,-1] . n +0.9397
  QA's verdict from narrowest_m: passes at any yaw, 7 mm a side on the diagonal; nose-first 72.9 mm a side
  prefab's door gap equals the metadata's: True
```
- **Where the feet sit.** The two feet lie on the leaf's outer board face. They are 1–10 mm off the box only where the 1.89 m leaf stops short of the frame's 0.05 m threshold and 1.95 m head (D's sag tilts it 0.013), and that sliver is free space.
- **The normal** lies along the leaf (dot −1.000), square to the throat, so it meets the contract's "perpendicular to the polygon".
- **Nothing narrower than the throat.** The widest sphere from outside the door into the hall, over every shape, at mid-height:
  - I: 0.356 m at every height from 0.3 to 1.7 m
  - D: 0.709 m from 0.6 to 1.4 m

  Both are about 11 mm under the throat, which is the 10 mm grid's resolution. At D's narrowest cell (x 0.06, z −2.45), the nearest shapes are the far jamb (0.354 m) and the leaf (0.366 m). D's 0.500 m at 0.3 and 1.7 m is the sphere meeting the threshold and the head, 0.25 m away, not a pinch in plan.
- **The first pass's probe on the new polygons** (`qa_door_probe.py`; segments ±0.10 m along the normal, 10 mm grid):
  ```
  H01_I door: width 0.3680 x 1.900 m; inside 0/6615 blocked; outside blocked: arris side 189/189, leaf side 189/189, below the threshold's top 35/35, above the head's underside 5/35
  H01_D door: width 0.7198 x 1.900 m; inside 0/13230 blocked; outside blocked: arris side 189/189, leaf side 189/189, below the threshold's top 70/70, above the head's underside 27/70
  ```
  The throat stands in the hall, just inside the wall's inner face, and the hall is open above it to the 2.4 m ceiling. So its top edge is tight only near the arris, where the lintel is. That is the safe side: the frame's 1.90 m head limits anything that comes through the door before it reaches the throat.
- **The look.** The sheets were re-captured with the rebuild (2026-11-01 01:43; new SHA-256 `fc5506598cf5bed9` D day, `673f2da7a0fc31c3` I day). QA projected each shipped polygon through the overlay tile's camera (10 m, 20° down, az 225, 60° vertical, framed on `bounds.lod`). In both states the magenta quadrilateral is drawn there, running into the hall from the far jamb to the leaf: narrow in I, near-square to the wall in D.
- **Tests.** `test_ajar_door_gap_is_the_throat_its_leaf_leaves` and `test_hanging_door_gap_is_the_throat_its_leaf_leaves` take the throat from the leaf's collision box and the arris, not from `_throat`. They hold the gap to it (2 mm, the polylabel's precision) with two points on the arris, and the verdicts "no" and "any yaw, 7 mm" with 73 mm nose-first. `test_door_gap_narrows_with_its_leaf` sweeps 10–90°: the old rule (0.80 m for every open leaf) fails `narrowest_m < 0.80 − 5 mm` at every swing, and the pass flips between 45° and 50°. By hand, 0.8 sin θ − 0.032 = 0.574 at 49.2°.
- **The scenario** (`ecce570`): "a 0.720 m throat … 73 mm a side nose-first, 7 mm on the diagonal". QA reads 0.719754 m, 72.9 mm and 6.9 mm: **passed**. The task note's "16.000 mm at the leaf" in the audit's 2 cm zone was not re-run (no Blender). The code explains it: five 32 mm boards with 3 mm joints, on the same faces as the leaf's one board box, so a joint's side face lies 16 mm inside the box, under the 20 mm zone and too narrow to pass.

### B2 against the amended lines (`ecce570`)
| Item | Amended line | Shipped | Judgement |
|---|---|---|---|
| B2.1 the adobe set | "the wall sets at most 2K (the adobe set `dirt_floor` is 1K, 495 px/m)" | `manifest.json`: `dirt_floor`, `resolution` 1k, CC0. Its four maps are 1024 × 1024 (diff, nor_gl, rough, ao). `adobe.json` and `facade_h01.json` tile it at 2.07 m, so 1024 / 2.07 = 494.7 px/m | **met by the amended text.** It also sits inside AST-07's "about 512 px/m" near openings (3.4 % under) and AST-08's "2K at most" |
| B2.2 the LODs | "the openings kept through LOD2 (LOD3, the far shell, closes them, and the flue closes from LOD2; collision is unaffected)" | The FBX is unchanged since the first pass's probe: every opening is open at LOD0–2, and LOD3 closes them at the `interior` faces; the flue is closed from LOD2 at the chimney's box. By design, `walls.outer_and_liner` gives LOD3 its outer faces with the openings cut, over a dark liner per room, "so an opening shows the inside, not the sky". `roof._far_cover` draws a stripped roof only where tiles remain, so D's roof holes stay open down to the liner. The prefab carries 4 047 `BoxCollider`/`CapsuleCollider` entries and no `MeshCollider` (`grep -c`), so collision never comes from an LOD | **met by the amended text** |
| B2.3 the piece count | "at least one shape per piece (two for a cambered tile; a timber piece also carries its splinter and nail capsules) … and the build prints the piece count" | `house.py:659` prints `rubble pieces: {counts}; {shapes}, {snags}`, and D's line reads adobe 173, clay 48, tile 340, timber 119 (680 pieces); 2 025 shapes, 1 665 snags. `Rubble.place` adds every shape of each kit piece and counts it once: adobe and clay one box each, a K02 shard one box and a whole tile two (`test_debris_tile`), a K03 piece its box plus a capsule per splinter and two per nail. The first pass's audit holds each within 41.5 mm | **met by the amended text.** The WHEN's "300 tile shards and 40 timber pieces" is the scenario's example; D composes more |

### The world queries and the door's new normal
The door gap's normal now lies along the leaf: I's is 60° off the wall's normal, D's 20° (dot with [0, 0, −1]: 0.500 and 0.940). **No code assumes the wall's normal, because nothing on the trunk consumes a gap at all:**
- `GapsNear` exists only as a name in `IWorldQuery.cs`'s header ("not in this change"). `p6-world-query-gallery` lists it as a non-goal, and D-047's world-query tasks 1.4 and 1.5 do not include it.
- The flight's contacts read primitives, never gaps.
- `AssetPhysicsMeta.Gap` holds the polygon, normal and `narrowestM` as data.
- No Python on the trunk reads `gaps` outside `tools/blender`'s writer, audit and tests.

**The contract already says what the build does.** The trunk's `asset-pipeline` spec: "a gap `polygon` is the throat of the opening … with a unit `normal` of free sign"; the audit refuses a normal that is not perpendicular. `requirements.md`'s metadata line: "planar polygons in any orientation".

**What the first consumer must not assume** (`qa_door_axis.py`, at y 1.0, ±1.5 m along each line):
```
H01_I along the gap normal [-0.866, 0.0, -0.5] through (0.308, -2.391): min clearance 0.000 m (a 0.000 m disc) at t -0.94 m, to a masonry box at [1.0, 1.2, -2.05]; drone nose-first 0.574 does not clear
H01_I along the wall normal [0.0, 0.0, -1.0] through (0.000, -2.600): min clearance 0.000 m (a 0.000 m disc) at t -0.28 m, to a timber box at [-0.046, 1.005, -2.364]; drone nose-first 0.574 does not clear
H01_D along the gap normal [-0.342, 0.0, -0.94] through (0.062, -2.427): min clearance 0.313 m (a 0.626 m disc) at t +0.72 m, to a masonry box at [-1.29, 1.175, -2.96]; drone nose-first 0.574 clears
H01_D along the wall normal [0.0, 0.0, -1.0] through (0.000, -2.600): min clearance 0.093 m (a 0.187 m disc) at t -0.80 m, to a timber box at [-0.245, 1.0, -2.171]; drone nose-first 0.574 does not clear
```
- **The wall's normal is wrong for D.** A straight line in through the frame's centre meets the hanging leaf (0.187 m); D's own normal clears the whole doorway (0.626 m, tightest at the wall's outer reveal, not the throat).
- **A gap's normal is no straight path either.** I's runs into the hall's partition 0.94 m in; I is "no", so that is moot.
- **For `GapsNear` (WLD-11) and the wind's jets through gaps (`p7c-wind`):** carry the polygon and normal as the asset gives them, rotated by the placement, never re-derived from the host wall. AST-05's capsule acceptance should fly through the polygon, from either side of the normal, rather than along a fixed line past the wall.

### Suggestions (non-blocking)
- **S1.** No test holds a `gone` door's gap (the frame's 0.80 × 1.90 m, `joinery.py:103-105`). Neither I nor D builds it, so nothing shipped depends on it.
- **S2.** `test_rubble_pieces_carry_their_own_collision_and_the_houses_colours` holds the aggregate (`len(rb.collision) >= sum(rb.counts.values())`), and the heaps' boxes sit in the same list. A per-piece count in `Rubble.place` would hold the scenario's "at least one shape per piece" directly. Today it holds by construction.
- **S3.** The door scenario's "in every leaf state" reads best as every open state. A closed leaf leaves no gap, and a gone one the frame's opening (the docstring says so).
- N1–N5 from the first pass are unchanged by the rebuild.

### Commands and output
```
> git diff --stat f8171f7 HEAD            # the rebuild touched only the door gap
 openspec/changes/p1-h01-adobe-house/review.md      | 265 +++
 .../specs/asset-pipeline/spec.md                   |   2 +-
 .../specs/assets-houses-and-yard/spec.md           |   4 +-
 openspec/changes/p1-h01-adobe-house/tasks.md       |   1 +
 tools/blender/lib/joinery.py                       |  23 +-
 tools/blender/tests/test_house.py                  |  73 +++++-
 .../Art/Models/houses/H01/H01_D.assetmeta.json     |  22 +-       (the door gap's 4 points, normal, narrowest_m)
 .../Art/Models/houses/H01/H01_I.assetmeta.json     |  22 +-       (the same)
 unity/Assets/_Project/Prefabs/houses/H01_D.prefab  |  12 +-       (the same)
 unity/Assets/_Project/Prefabs/houses/H01_I.prefab  |  12 +-       (the same)

> python -m pytest tools/blender/tests/test_house.py -q -p no:cacheprovider     # PowerOnline True, 3.57 GB free
41 passed in 58.98s

> BLENDER=<missing> python -m pytest tools -q -p no:cacheprovider --ignore=tools/blender/tests/test_build_blender.py -rs   # PowerOnline True, 2.38 GB free; another agent's pytest overlapped
SKIPPED [1] tools\blender\tests\test_xsign_probe.py:34: the pinned Blender is not installed ($BLENDER or the default path)
1352 passed, 1 skipped in 1705.12s (0:28:25)
# reconciled with the first pass's 1 356: test_house.py 34 -> 41 collected (the old door test out, 2 + 6 in) = 1 363,
# less test_build_blender.py's 10 (collected, not run) = 1 353 = 1 352 + the skipped probe

> python tools/hygiene/precommit.py --tree HEAD
1574 files scanned, 0 findings
> openspec validate p1-h01-adobe-house --strict
Change 'p1-h01-adobe-house' is valid

> per commit in f8171f7..be14dd4: subject length, body, case, author / committer
be14dd45 len=60 body_bytes=1 upper=0 Asset Artist <assets@450fpv.local> / Asset Artist <assets@450fpv.local>
7ee0abf3 len=69 body_bytes=1 upper=0 Asset Artist <assets@450fpv.local> / Asset Artist <assets@450fpv.local>
7a04f1bd len=68 body_bytes=1 upper=0 Asset Artist <assets@450fpv.local> / Asset Artist <assets@450fpv.local>
5e05d9d8 len=63 body_bytes=1 upper=0 Asset Artist <assets@450fpv.local> / Asset Artist <assets@450fpv.local>
ecce570c len=72 body_bytes=1 upper=0 Orchestrator <orchestrator@450fpv.local> / Orchestrator <orchestrator@450fpv.local>
94c3a450 len=57 body_bytes=1 upper=0 QA Engineer <qa@450fpv.local> / QA Engineer <qa@450fpv.local>
> git log --format=%B f8171f7..HEAD | grep -ci 'co-authored|claude|anthropic'  -> 0
> git diff --name-only f8171f7 HEAD | grep reference/, Library/, Temp/, Logs/, UserSettings, .log  -> 0;  git ls-files reference -> 0
> git merge-base HEAD origin/unity/main -> 46d96609; trunk ahead 206, branch ahead 61; files changed on both sides: 0
```

## First pass at `f8171f7`: CHANGES REQUESTED

Two blocking items, neither a look item (D-040):
- **B1. The door's gap ignores its own leaf** (metadata, test). H01_I's ajar leaf leaves a 0.368 m throat, but its gap says 0.80 m and "passes at any yaw", and the test asserts it. In D the verdict holds, by 7 mm a side on the diagonal at the leaf, not the 47 mm the build prints and the scenario states.
- **B2. Three spec lines the build does not meet as written** (spec text, the orchestrator's or the artist's): the adobe wall set is 1K where the Budget scenario says the wall sets are 2K; LOD3 closes every opening where it says "kept at every LOD"; the audit does not print the piece count the composed-pieces scenario asks of it.

Everything else passes:
- three builds (K02, K03, H01) exit 0 and leave `git status` clean; H01's four review sheets are byte-identical to 1.6's (SHA-256)
- the AST-04 sizes read from the shipped metadata: door, windows, sill, bricks, walls, thickness, ridge, battens, rafters
- the audit: 0 problems; every gap's `narrowest_m` equals its inscribed circle; every window, the breach, the doorways, the flue and the 26 ceiling holes are clear inside and tight outside, the 13 roof bays clear to within 7.4 mm of their edges; no roof opening wider than a 0.283 m batten slot is missing from the gaps
- the D damage against the sheet: roof 80 % stripped, 2 rafters off, the hanging leaf, the crumbled windows, the back breach, heaps 0.30–0.31 m deep and 1.0–2.0 m wide, the ceiling 45.6 % down, one cable, rubble −0.09 % of the 1.86 rule
- REAL-05: every tile, batten, rafter, brick, crumbled course, splinter, nail, shard and rubble piece is geometry with collision; nothing with a physical role is texture-only
- no chequerboard or seam on the facade at 10 m; U continuous across the corners (one restart, N1)
- the budget's triangle counts, halving and cull; textures 2K or less
- the mask bound per variant, and `render_loss` 0.3 → 0.6 changing only the mask and the params hash (AST-03)
- 1.6: every snag names its material; `bounds.lod` frames the LOD group (10.612 m) and the shots; R01's sheets framed on its body
- `pytest tools` 1 356 passed, EditMode 114/114, `fetch.py --verify`, the OPSEC scan, `openspec validate --strict`, the commit hygiene

The look misses are **G1 stand-ins** (14 lines below), not blocking items, per D-040. Task 2.1 stays unticked.

## Blocking items

### B1. The door's gap ignores its own leaf: H01_I's ajar door claims a passage its leaf blocks
**Measured** (`qa_door.py`: the door gap and the leaf's board box from the shipped metadata; the throat is the plan distance from the far jamb's room-side corner to the leaf):
```
== H01_I: door gap x -0.400..0.400 at z -2.600, narrowest 0.8; leaf-like boxes 1
  box size [0.8, 1.89, 0.032] center [-0.046, 1.005, -2.364]
  clear throat from the far jamb's inner corner to the leaf: 0.368 m (drone 0.574 nose-first, ~0.706 diagonal)
== H01_D: door gap x -0.400..0.400 at z -2.600, narrowest 0.8; leaf-like boxes 1
  box size [0.8, 1.89, 0.032] center [-0.245, 1.0, -2.171]
  clear throat from the far jamb's inner corner to the leaf: 0.720 m (drone 0.574 nose-first, ~0.706 diagonal)
```
By hand: 0.8 sin 30° − 0.032 = 0.368 m; 0.8 sin 70° − 0.032 = 0.720 m. The same in the shipped FBX (`qa_lod_holes.py`, a ray along the door's normal through its centre): `H01_I: door: LOD0 CLOSED at -0.281 m door, LOD1 CLOSED at -0.281 m door, LOD2 CLOSED at -0.310 m door`, the leaf. The build's own line: `H01_I: gap door: 0.800 x 1.900 m, narrowest 0.800 m: passes at any yaw, 47 mm a side on the diagonal`.

**Why it blocks:**
- `joinery.door` (`tools/blender/lib/joinery.py:119-120`) records the frame's clear 0.80 × 1.90 m for every leaf state but closed; its docstring (`:20-22`) leaves the leaf out as "collision of its own". The I door is ajar (`assets-src/houses/H01_I.json`, 30°), so its gap reads "passes at any yaw" while its leaf leaves 0.368 m, which no drone passes (0.574 m nose-first).
- AST-05: gaps are "measured from the real clear space", and its acceptance flies a 0.574 m capsule through every gap. The requirement: "every opening's clear space a gap polygon". A probe through I's door meets the leaf.
- The test `test_door_gap_is_the_clear_opening` (`tools/blender/tests/test_house.py:219-229`) builds H01_I, ajar, and asserts `"any yaw"`, so it blesses the miss.
- D, hanging at 70°: "passes at any yaw" holds, but the margins at the leaf are 73 mm a side nose-first and 7 mm on the diagonal (0.720 − 0.706), not the "113 mm … 47 mm" of the build's line and of the scenario "Openings against the drone in the D state".

**To close it** (asset-artist; the scenario's wording is the orchestrator's):
- an open leaf's door gap takes its narrowest clear section, the throat between the leaf and the far jamb (or `narrowest_m` and the verdict from it), so I's door reads "no" and D's "any yaw, 7 mm a side on the diagonal". If the look wants I's door passable, the sheet's I state allows closed or ajar, and only a wider swing passes;
- the test derives the throat from the leaf's box and holds the gap to it, for ajar and for hanging;
- the scenario's margins restated as the throat's.

### B2. Three spec lines the build does not meet as written
1. **"the wall sets are 2K"** (H01's Budget scenario). The facade's exposed adobe, the wall set most of D's facade shows, is `polyhaven/dirt_floor` pinned at **1K** (`tools/assets/manifest.json`: `dirt_floor_diff_1k.png` …; `assets-src/materials/adobe.json`, `facade_h01.json`, tile 2.07 m, so 495 px/m). Task 1.5 chose it by eye over `rough_plaster_brick_02` (2K). Texel-wise it is about AST-07's 512 px/m near openings, so either pin `dirt_floor` at 2K (one `fetch.py --add`) or amend the line to "2K or less, about 512 px/m on the walls (AST-07)".
2. **"with the openings kept at every LOD"** (same scenario). `qa_lod_holes.py`, a ray along each opening's normal through its centre in the shipped FBX, a hit within the wall's 0.45 m counting as closed:
   ```
   QA H01_D: door: LOD0 open, LOD1 open, LOD2 open, LOD3 CLOSED at -0.050 m interior
   QA H01_D: window_w1: LOD0 open, LOD1 open, LOD2 open, LOD3 CLOSED at -0.270 m interior
   QA H01_D: breach_b1: LOD0 open, LOD1 open, LOD2 open, LOD3 CLOSED at -0.225 m interior
   QA H01_D: doorway_left: LOD0 open, LOD1 open, LOD2 open, LOD3 CLOSED at -0.125 m interior
   QA H01_D: flue: LOD0 open, LOD1 open, LOD2 CLOSED at +0.150 m brick, LOD3 CLOSED at +0.150 m brick
   ```
   (every window of both states the same; I's door is closed by its leaf, B1). LOD3 (400 m to the 2 340 m cull) cuts the openings in its outer faces but closes them with the hull's `interior` faces, so a window reads as a recess; the flue is closed from LOD2. Design 8 meant LOD3 as "a hull"; the test `test_lods_halve_and_keep_the_openings` checks only the door against facade faces in the front wall's plane, so it misses the `interior` face. Either LOD3 drops the inner faces behind its openings, or the line reads "kept to LOD2, recessed at LOD3".
3. **"the audit prints the piece count it checked"** (`asset-pipeline`, "Rubble pieces audited with the house"). The audit prints a line per primitive (4 047 for D) and none per piece; the piece count is the generator's line, `H01_D: rubble pieces: adobe 173, clay 48, tile 340, timber 119; 2025 shapes, 1665 snags`. The same scenario's "one shape per piece (two for a cambered tile)" does not hold for timber either: a K03 piece carries its box plus a capsule per splinter and two per nail, as K03's own requirement asks (2 025 shapes for 680 pieces). Either the audit prints the composed pieces' count, or the scenario reads "the build prints the piece count, and the audit checks every shape of every piece", with "one box per piece (two for a cambered tile) and a capsule per splinter and nail".

## Non-blocking findings

- **N1. The facade's U loop restarts at one corner with a phase step in the tiled sets.** `lib/walls.py:11-13`: U runs once round the 32 m perimeter from the north-east corner and restarts there; the mask is periodic and meets itself, but the sets do not: 32 m is 17.8 tiles of the whitewash's and render's 1.8 m and 15.5 of the adobe's 2.07 m. Not seen in either state's turntable 2/8 (az 45, 16.1 m), the one view of that corner, at day; the rig has no close shot of it. Each room's loop restarts the same way. A tile size that divides the loop (1.778 m, 2.0 m), or a per-loop scale, closes it.
- **N2. D breaks 40.4 % of the spans that can break.** `H01_D: damage: 42 of 104 batten spans between commons broken (batten_break 0.4)`: `round(0.4 × 104)` = 42 is one span past the sheet's 20–40 %, over the regular bays the generator counts. Over all the side slopes' spans (8 bays × 13 rows a slope) it is about 20 %. The orchestrator says which denominator the sheet means; 41 spans sit inside either way.
- **N3. The review stage lifts H01_D by 24 mm.** `bounds.min.y` is −0.024 m (a rubble piece below grade), and `ReviewCapture.cs:334` lifts the instance by the whole bounds' foot, so D's house stands 24 mm proud of the stage ground in its sheets. Cosmetic.
- **N4. Two pinned sets are no longer used.** `rough_plaster_brick_02` and `worn_cracked_plaster` (2K, 21.5 and 56.3 MB in LFS) are pinned and credited, and no recipe, generator or material references them since task 1.5 (`git grep`: 0 files). Licences are fine; dropping the pins saves 78 MB a clone.
- **N5. H01's audit times are not a quiet machine's.** `H01_I: audit took 755.8 s`, `H01_D: audit took 2056.7 s`. Two other agents' EditMode runs overlapped I's audit, and QA's own `pytest tools` (13 min) overlapped D's.
- **N6. X is in the spec and not built** (D-040). The H01 requirement, "Built from params by one command" ("exits 0 for the three states") and the K02/K03 scenario ("H01_X's rubble") are not testable until task 1.4b. The change stays open until then, or X moves to its own change before this one archives.
- **N7. The branch is behind the trunk.** `origin/unity/main` is at `7e76df3` (the flight core and handovers); the 142 trunk files and the branch's 242 share none, so the pre-PR merge is mechanical.

## Spec scenarios

**`assets-houses-and-yard`, H01 (6):**
| Scenario | Result | Evidence |
|---|---|---|
| Built from params by one command | **passed** for I and D; X not testable (D-040, N6) | `build_asset.ps1 H01` exit 0, FBX, metadata, masks, prefabs and four sheets written; `git status --short` empty after; sheets byte-identical to 1.6's |
| Exact sizes | **passed** | door 0.8000 × 1.9000 m; windows' rough opening 0.80 × 1.00 m (frame clear 0.6600 × 0.8600, lowest 0.9700, so the sill at 0.900 m); 444 brick boxes, 0.0 mm off 250 × 120 × 65; walls to 2.40 m, 0.45 m thick, outer faces at 3.0000 / 5.0000 m; ridge (the rafters' apex) 5.0003 m, the gable apex 5.0003 m in the FBX; battens 0.333 m apart (10 pitches); rafters 1.00 m (the gable bay 0.45) |
| Openings against the drone in the D state | **failed** on the door's margins (B1); the rest passed; a frame-gone window and a fully stripped bay not testable in D (none built: every window keeps its frame, no bay loses all its battens; the first is held by `test_framed_window_gap_and_the_other_states`) | windows 0.66 × 0.86 m, ±18°, 43 mm; 13 roof bays, 11 pass, 2 do not (0.452, 0.510); `qa_gaps.py`: no batten or rafter inside any bay polygon deeper than 7.4 mm from its edge (stub splinters and collars), and 72–100 % of the probes 8 mm outside each bay blocked (the rest where the polygon stays inside the clear space, the safe side); `qa_roof_open.py`: every non-gap opening on the front, back and hip is at most 0.28 m (a batten slot); breach 0.826 m clear and tight; `narrowest_m` equals the audit's circle for all 50 gaps |
| The E facade | **passed** for no tiling, chequerboard or seam at 10 m and upright continuous UVs (N1 aside); the "match by eye" items are G1 stand-ins (below) | the sheets' 10 m and turntable tiles, the comparison sheet against E 1, 7, 10, 14, 15, 17 |
| Rubble in the house's colours | **passed** for D; X not testable | pieces tile 340, timber 119, adobe 173, clay 48, each with its shapes inside the audit's 41.5 mm; slots only tile, tile_fracture, timber, timber_fracture, iron, adobe, clay, heap (the test), none near-white (tints: tiles #a0613f, adobe #7d6146, render #97754f, heap #906143, fresh fracture #b09a78); heaps 0.300–0.310 m deep, 1.00–2.00 m out |
| Budget | **failed** on two lines (B2.1, B2.2); the rest passed | D LOD0 101 763 < 150 000; 45 575 / 1 523 / 291, each at most half; cull 2 340 m = 935 × 5.0 / 2; textures: wall sets 2K but the adobe 1K, tiles and timber 1K, mask 2048 × 1024 |

**`assets-houses-and-yard`, K02 and K03 (1):**
| Scenario | Result | Evidence |
|---|---|---|
| Kits built and composed | **passed** for the five pieces and D's heap; X not testable | five pieces: box → mesh at most 17.8 mm, mesh → collision at most 15.9 mm; snags shard 1 edge (`masonry`), whole tile none (no fracture rim), batten 14, board 8, rafter 11; D's tile and timber pieces placed by the spread rule (λ 1.0 m tiles, 2.0 m timber, × 1.75 on the lee side, capped at 3 λ), held by `test_house_rubble_spreads_by_exp_per_class_and_widens_away_from_the_burst` |

**`asset-pipeline` (3):**
| Scenario | Result | Evidence |
|---|---|---|
| Mask atlas bound | **passed** | `H01_I_damage.png` 2048 × 1024 RGBA, `textures` role `damage_mask` with `channels` [render_depth, soot, dirt, base_band] for the facade and interior slots; `H01_I_facade_layered.mat` and `_interior_layered.mat` bind its GUID to `_DamageMask`, D's likewise |
| Mask changes with the params | **passed** | `qa_mask_exp.ps1`: H01_I's `render_loss` 0.3 → 0.6 (the catalog narrowed to H01_I for the run), `build_asset.ps1 H01` exit 0; R at full depth 1 438 382 → 1 537 319 px; changed: the mask, the metadata's `params_sha256` and the prefab's one copy of it (`paramsSha256`); the FBX, materials and every other prefab line unchanged; the turntable shows less whitewash and more bare adobe. Restored from git, `git status` clean but this file |
| Rubble pieces audited with the house | **failed** as written (B2.3); the substance passed | every piece's shapes audited within 5 cm (primitive → mesh 41.533 mm, mesh → collision 48.892 mm) |

**`assets-lines-roads-debris` (1):** Kit pieces listed in the catalog: **passed** (`assets-src/catalog.json`: K02 `debris_tile` [`K02_tile_shard`, `K02_tile_whole`], K03 `debris_timber` [`K03_batten_broken`, `K03_board`, `K03_rafter_broken`]; both build).

## Requirements

| ID | Status | Why |
|---|---|---|
| AST-02 | met | §H01, §K02, §K03 give every number used; the build cites them |
| AST-03 | met | one command per asset; QA changed `render_loss`, rebuilt and saw the change in the prefab's sheet with no hand step |
| AST-04 | met | every exact size within 5 mm (table above); walls 2.40 m inside D-032's 2.3–2.6 m; the 1.8 m bar in every sheet |
| AST-05 | partly met | B1 (the door's clear space); everything else: shapes within 5 cm, 2 cm zones at 17.5 mm, materials, snags with materials, gaps, wind volumes (walls 0, slopes 0.62 in D, the cavity), no landable |
| AST-06 | met for I and D | stripped roof, broken battens with splinters, crumbled openings, breach, ceiling down, rubble; damage carries collision; X after G1 |
| AST-07 | met | upright continuous UVs, V up in metres; no tiling or chequerboard at 10 m; one phase step at the loop's restart corner (N1) |
| AST-08 | partly met | triangles, halving and cull pass; B2.1, B2.2 |
| AST-12 | met | 1.5 / 10 / 50 / 200 m, turntable, overlay, day and overcast, for every variant |
| REAL-01, REAL-03 | met (process) | the side-by-side exists against E and J and was read; its misses are listed as stand-ins |
| REAL-05 | met | the geometry audit below |
| SEC-04 | met | five new sets CC0 with author and SHA-256, `CREDITS.md` regenerated; `fetch.py --verify` OK |
| PR-5 | met | jagged openings as geometry, their clear space the gap (door aside, B1) |

## The geometry audit (REAL-05)
- **Collision overlay:** the sheets' overlay tiles show boxes hugging the walls, plate, rafters, battens, tile courses and heaps, capsules on splinters, nails and the cable, and snag crosses on batten stubs, tails, the leaf, frames and shards.
- **Every opening probed** (`qa_gaps.py`, 10 mm grid, a segment ±0.225 m through the wall, ±0.20 m through the roof and ceiling):
  ```
  window_w1: narrowest 0.660; inside 0/5676 blocked; outside 150/150 blocked (tight)
  breach_b1: narrowest 0.826; inside 0/10473 blocked; outside 244/244 blocked (tight)
  doorway_left: narrowest 0.800; inside 0/15200 blocked; outside 270/270 blocked (tight)
  roof_front_bay2_row7: narrowest 0.612; inside 127/13190 blocked deepest (mm from edge, x, y, shape) (7.2, -0.285, 0.265, 'Capsule', 'timber', ...)
  roof_back_bay5_row1: narrowest 0.616; inside 878/11446 blocked deepest (5.0, ...'Box', 'timber', [0.15, 2.475, 2.775])
  ceiling_room_right_x6.89_z3.40: narrowest 0.628; inside 0/4030 blocked; outside 118/126 blocked (tight)
  ```
  (excerpt; all 50 of D and 40 of I ran). Inside a roof bay, a stub's splinter or a collar's face reaches at most 7.4 mm in from the edge, inside the 2 cm zone; at ±0.20 m the eaves rows also see the wall plate below the rafters (at ±0.10 m only 2 points), so the plate is no part of the bay's section. I's panes are clear and tight round their corner shards. The door: B1.
- **Every opening a gap:** `qa_roof_open.py`, the front, back and hip planes on a 20 mm grid, ±0.10 m: every open cluster outside the gaps is at most 0.28 m across (`front: 93 clusters, largest inscribed square 0.28 m`; back 132, 0.28; hip 46, 0.28): batten slots, not passable, as the sheet rules.
- **Nothing physical texture-only:** tiles are meshes with course boxes (299 in I, 73 in D) and the ridge and hip tiles their own; battens 50 × 50 boxes with K03 stubs and splinters; every brick a box; the crumble round windows and the breach blocks are geometry with their own boxes; ceiling boards and clay are separate layers of boxes; the cable is 16 capsules and a hook; the glass shards in I's panes are `glass` boxes with edge snags; the rubble's 680 pieces carry 2 025 shapes. The render's edge (15–30 mm, a stand-in below) and the band are paint, under the 1–2 cm line.
- **The first attempt's failure:** no chequerboard and no seam at 10 m in either state, by day and overcast; U continues across three corners within 1 mm (`test_facade_u_continues_across_every_corner`) and restarts once (N1); V is up in metres; the reveals fold the face's UV.

## Statistics
The spread test (`_bands_hold`, each metre band within 20 % of exp(−d / λ)) fails known-wrong models and holds the generator's, which is stratified, so it has no random false failure:
```
lam 1.0: the generator's own spread -> (True, 'all bands')
  spread at 1.5 lam: (False, 'band 0: 154 vs 199.6')
  spread at 0.7 lam: (False, 'band 0: 240 vs 199.6')
  uniform over 0..cap: (False, 'band 0: 98 vs 199.6')
  unstratified exp draws (iid, seed 1..5): holds 4 of 5
lam 2.0: the generator's own spread -> (True, 'all bands')
  spread at 1.5 lam: (False, 'band 0: 90 vs 124.2')
  ...
  unstratified exp draws (iid, seed 1..5): holds 1 of 5
```
An iid sampler would fail the 20 % band 1–4 times in 5 at about 300 pieces, so the band test is only valid on the stratified spread, as built. The rubble volume (−0.09 % against 5 %) and the mask shares (5 %) are deterministic.

## The look
**Review sheets, read at full resolution** (`review-sheets/H01/`, `K02/`, `K03/`, day and overcast):
- **H01_I:** 1.5 m: the front wall's whitewash in large patches, the tan render rim round the bare adobe, the band at the foot, a double casement with its bars and corner shards, the rafter tails and plate. 10 m: every tile and course, the 43 holes with battens behind, no seam or tiling on the walls; about one tile in six pink-grey and weathered. 50 m LOD1, 200 m LOD2.
- **H01_D:** 1.5 m: the front wall's window frame in a crumbled opening stepped in courses and blocks, render patches feathered, the plate a dark line. 10 m: the stripped lattice with the chimney, two tile clusters on the front's lower slope, the ceiling's clay seen through the bays, shards and battens at the foot, the heap as flat sloped mats. 50 m and 200 m read as the house.
- **K02/K03:** the shard a 14 mm orange slab with ragged rims on two sides (a broken-off rectangle), the whole tile cambered with its nib; the batten and rafter splintered pale at the break, the board split; grain along each piece.
- **1.6's additions:** D's legend reads "SHOTS FRAMED ON BOUNDS.LOD 10.612 X 5.590 X 7.024 M", turntable at 16.13 m; R01_F's sheet "4.063 X 1.380 X 1.624 M", the car framed on its body (R01 QA's T1).

**Side-by-side (REAL-03), the local sheet against E and J, read by eye:** matches as task 1.5 lists them (the house's shape, hip and gable ends, chimney right of centre, the stripped upper slopes with sky through them, the hanging leaf with the far window through the door, the crumbled empty windows, the blue interior with its cable, the foot's rubble in the house's colours). Box means on the E 7 row: the roof's tiles #9d735c in E against #8e6652 built; lit adobe #84623a against #775f4a.

## G1 stand-ins (look misses, D-040)
From task 1.5, confirmed by QA:
1. The wall plate: E's is a massive pale timber flush with the wall face; ours is a dark line on the wall's centre line.
2. Loose tiles: E's lower slope is strewn with tiles slid askew; ours lie in their courses.
3. Render flakes: E's are many, small and crisp-edged; ours fewer, larger and feathered, with no thickness.
4. Wall relief: E's adobe is lumpy with strong light and shade; ours smoother and even.
5. The door: E's leaf and frame are red-brown paint; ours a dark leaf in a grey frame.
6. The battens: J's lattice has runs missing and pieces hanging; ours is a regular grid with broken spans.
7. The hall's floor: E 16–17 show boards and plaster on it; ours is clear (`interior_debris` not built).
8. The light: the rig's flat ambient lights the rooms, dark in E (the gallery's lighting).
9. The band: E shows none; ours shows wherever render survives.
10. The surroundings: E's weeds, shed and fence are other assets.

Added by QA:

11. Weathered tiles: about one tile in six is pink-grey and mottled, evenly sprinkled over both states' roofs at 10 m, not E 7's "odd weathered one" that 1.5 reports.
12. The heap: flat, sharp-edged sloped mats along the foot, not E's shards heaped in the weeds.
13. The crumble round the windows is cut in square 0.2 × 0.25 m courses and blocks; E's edges are ragged clay.
14. The adobe reads a shade greyer than E 7's lit ochre (#775f4a against #84623a).

## Commands and output
```
> git lfs checkout                                    # in the worktree, before anything else
> pwsh -NoProfile -File qa_build.ps1                  # build_asset.ps1 K02, K03, H01 in turn, git status after each
[K02] start freeGB=4.16 PowerOnline=True
[K02] exit=0 elapsed_s=88.1
--- git status after K02
[K03] start freeGB=2.65 PowerOnline=True
[K03] exit=0 elapsed_s=197.1
--- git status after K03
[H01] start freeGB=3.09 PowerOnline=True
[H01] exit=0 elapsed_s=2923.6
--- git status after H01
K02_tile_shard: audit collision[0] box: box->mesh 11.016 mm ...; audit mesh: mesh->collision 9.903 mm
K02_tile_whole: audit collision[1] box: box->mesh 17.783 mm ...; audit mesh: mesh->collision 15.887 mm
K03_batten_broken: audit mesh: mesh->collision 8.816 mm ...   (K03_board 12.180, K03_rafter_broken lines alike)
H01_I: audit mesh: mesh->collision 48.890 mm at (4.1035, 3.4189, 1.9646) nearest collision[734], tolerance 50 mm; near gaps/landable 16.893 mm ..., tolerance 20 mm
H01_I: audit took 755.8 s
H01_I: roof: pitch 38.4 deg, ridge (the rafters' apex) 5.0003 m, eaves 0.4 m, rafters 0.1 x 0.15 m at 1 m (bays 0.450-1.000 m centre to centre, the narrow one at the gable)
H01_I: tiles: 0.4 x 0.22 m overall, 0.333 x 0.2 m covering (ГОСТ 1808-71), head lap 67 mm, ...; 1253 whole and 125 cut laid, 43 gone (roof_strip 0.03), 299 course boxes
H01_I: gap door: 0.800 x 1.900 m, narrowest 0.800 m: passes at any yaw, 47 mm a side on the diagonal
H01_I: ... tris 60070 / 15815 / 506 / 183  1207 collision shapes  fbx 1.8 MB  metadata written
H01_D: audit mesh: mesh->collision 48.892 mm at (5.1881, 2.4481, 3.3295) nearest collision[923], tolerance 50 mm; near gaps/landable 16.000 mm ..., tolerance 20 mm
H01_D: audit took 2056.7 s
H01_D: tiles: ...; 265 whole and 19 cut laid, 1137 gone (roof_strip 0.8), 73 course boxes
H01_D: damage: 42 of 104 batten spans between commons broken (batten_break 0.4), 2 rafters off the plate; ceiling down 21.18 of 46.41 m2 (interior_ceiling_down 0.45)
H01_D: gap door: 0.800 x 1.900 m, narrowest 0.800 m: passes at any yaw, 47 mm a side on the diagonal
H01_D: gap window_w1: 0.660 x 0.860 m, narrowest 0.660 m: passes within +-18 deg yaw, 43 mm a side nose-first   (w2-w6 the same)
H01_D: gap breach_b1: 1.100 x 1.286 m, narrowest 0.826 m: passes at any yaw, 60 mm a side on the diagonal
H01_D: gap roof_back_bay4_row1: 0.900 x 0.616 m, narrowest 0.452 m: no
H01_D: gap roof_back_bay7_row4: 0.900 x 1.262 m, narrowest 0.510 m: no
       (11 other bays pass, 0.596-1.282 m; 26 ceiling holes, 16 pass at 0.628-0.650 m; flue 0.270 no)
H01_D: rubble: removed 6.682 m3 solid (adobe 1.554, clay 3.177, tile 1.210, timber 0.740) x 1.86 = 12.429 m3; laid 12.418 m3 (heaps 9.061 + 1.86 x pieces 1.805), -0.09 %
H01_D: rubble pieces: adobe 173, clay 48, tile 340, timber 119; 2025 shapes, 1665 snags
H01_D: ... tris 101763 / 45575 / 1523 / 291  4047 collision shapes  fbx 4.8 MB  metadata written
build_asset: OK (H01)
# QA's summary of the 5 254 audit lines: H01_I 1207 primitive lines, max primitive->mesh 41.533 mm, 40 gap lines with
# narrowest_m = circle exactly, 0 problem lines; H01_D 4047 primitive lines, max 41.533 mm, max zone 17.518 mm,
# 50 gap lines exact, 0 problem lines

> Get-FileHash review-sheets/H01/*.png               # before and after the rebuild: identical
FAF4F20EBED9F7E7 H01_D_day.png / 56BF95A3E0C0A513 H01_D_overcast.png / E66D5EDF8A88FE92 H01_I_day.png / 5A16F68A2F93FCCF H01_I_overcast.png

> python -m pytest tools -q -p no:cacheprovider       # FreeGB=5.25 PowerOnline=True
1356 passed in 802.02s (0:13:22)

> & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/editmode_qa_h01.xml -logFile - | Out-Host
Test run completed. Exiting with code 0 (Ok). Run completed.
total=114 passed=114 failed=0 skipped=0 result=Passed

> python tools/assets/fetch.py --verify
OK
> python tools/hygiene/precommit.py --tree HEAD
1573 files scanned, 0 findings
> openspec validate p1-h01-adobe-house --strict
Change 'p1-h01-adobe-house' is valid
> openspec validate --all --strict
Totals: 19 passed, 0 failed (19 items)

> git log 46d9660..HEAD: body, length > 72, upper case, Claude/Anthropic or co-author per commit
commits with issues: 0      # 55 commits: Asset Artist, Tech Artist, Orchestrator, each its role's identity (docs/team.md)
> git diff --name-only 46d9660...HEAD | grep reference/, Library/, Temp/, Logs/, UserSettings, .log
(none)      # and git ls-files reference: 0

> pwsh -NoProfile -File qa_mask_exp.ps1               # render_loss 0.3 -> 0.6 on H01_I, then restored
before: R at full depth (>=192): 1438382 px; R>=64: 1639931
[exp] build exit=0 elapsed_s=624.4
after: R at full depth (>=192): 1537319 px; R>=64: 1710980
 M unity/Assets/_Project/Art/Models/houses/H01/H01_I.assetmeta.json   (params_sha256 only)
 M unity/Assets/_Project/Art/Models/houses/H01/H01_I_damage.png
 M unity/Assets/_Project/Prefabs/houses/H01_I.prefab                   (1 line: paramsSha256)
--- git status after the restore
?? openspec/changes/p1-h01-adobe-house/review.md

> blender --background --factory-startup --python qa_lod_holes.py   # FreeGB=4.83 PowerOnline=True
QA H01_I: frame map (-x, -z, y)b (chimney top box 32.5 mm from LOD0)
QA H01_I: window_w1_pane1: LOD0 open, LOD1 open, LOD2 open, LOD3 CLOSED at -0.347 m interior   (every pane the same)
QA H01_I: highest LOD0 vertex per slot clay 5.5900, brick 5.5900, tile 5.1109, tile_fracture 5.0384, timber 5.0146, facade 5.0003, interior 5.0003, ...
(D's lines under B2.2)
```
The scripts' other outputs are quoted where each finding uses them.
