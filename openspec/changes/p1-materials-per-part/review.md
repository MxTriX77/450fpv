# Review: p1-materials-per-part (task 2.1)

**Reviewer:** qa-engineer (did none of the work, D-034) · **Branch:** `unity/physics/p1-materials-per-part` · **Tip:** `7eee1ee` · **Range:** `origin/unity/main...7eee1ee` (3 commits, 6 files)
**Requirements cited:** AST-05, PHY-01, PHY-03, PHY-05 · **Decisions:** D-024, D-028
**Sources read:** `CLAUDE.md`, `AGENTS.md`, `docs/workflow.md`; this change's proposal, tasks and spec delta; `openspec/changes/archive/2026-10-18-p1-fortifications-sheet/review-physics.md` (§4, §9 N-1, §10); `openspec/changes/archive/2026-10-19-p1-s04-belt-straw/review-physics.md` (PR6, the soft-top bound); `tools/blender/lib/audit.py` (`soft_materials`, `_zone`), `tools/blender/lib/meta.py` (the table validator); `docs/knowledge.md` §5.2, §5.4, §7.1–§7.5; every object sheet line that names `fabric`, `mesh`, `straw`, `sandbag` or `razor_wire`, or quotes a stiffness or a sink.

Every check below was run by QA in its own worktree: `git lfs checkout` first (37 of 37 LFS files checked out), no `unity/Library/` before the first Unity run. The unity/main baseline was built in a second, detached worktree of `origin/unity/main` (`4d80a8d`) in QA's scratchpad. The arithmetic ran in plain Python scripts in that scratchpad, with `python -I`. They are not committed. No performance claim is made (none is needed here).

## Verdict

**CHANGES REQUESTED.** Two small blocking items, R1 and R2. The rows are physically sound. The two new rows are byte-identical to the reading. `fabric`, `mesh` and `straw` recompute. The builds and audits are unchanged, and the hygiene checks are clean. What remains:
- **R1:** one object-sheet sink is still read for the whole drone, the error this change exists to fix.
- **R2:** two rows do not give the sink or deflection that the change's own requirement asks for.

## Checks

### Diff scope

```
$ git diff --stat origin/unity/main...HEAD
 assets-src/physics_materials.json               | 60 ++++++++++++++++---------
 docs/object-sheets/houses-and-yard.md           |  2 +-
 openspec/changes/p1-materials-per-part/tasks.md |  2 +-
 openspec/changes/p1-r01-vaz-sedan/design.md     |  2 +-
 tools/blender/tests/test_audit.py               |  5 ++-
 tools/blender/tests/test_meta.py                |  6 ++-
 6 files changed, 49 insertions(+), 28 deletions(-)
```

As briefed:
- the table
- the two tests (`test_audit.py`'s soft-id set gains `razor_wire` and `sandbag`; `test_meta.py`'s closed list gains them too)
- the three quoted values:
  - R01's design: `fabric` 160 N/m per touching part
  - Y03's open point: `mesh` 430 N/m per part, "the sign-off's 800 N/m was the whole drone"
  - the `about` path, now `openspec/changes/archive/2026-10-18-p1-pipeline-hardening/review-materials.md` (`Test-Path` → True; the old path → False)
- task 1.1's tick

Nothing is under `reference/` or `unity/{Library,Temp,Logs}/`.

### Commits

```
$ git log --format='%h | %an <%ae> | %s | body:[%b]' origin/unity/main..HEAD
7eee1ee | Orchestrator <orchestrator@450fpv.local>         | docs: follow the per-part fabric and mesh values where they are quoted | body:[]
f2ab937 | Physics Engineer <physics@450fpv.local>          | feat(physics): add razor_wire and sandbag material rows               | body:[]
a5b396a | Physics Engineer <physics@450fpv.local>          | feat(physics): read every material row per touching part              | body:[]
subject lengths 70 / 55 / 56 (limit 72); author = committer on all three
```

Each commit is subject-only, under its role's identity from `docs/team.md`, and matches the hook's `type(scope): summary` or `type: summary`.

### The two new rows against the reading's §4

```
$ python -I new_rows.py <review-physics.md> assets-src/physics_materials.json
razor_wire: keys ['friction_static', 'friction_kinetic', 'stiffness_n_per_m', 'damping_ratio', 'edge_radius_m', 'label', 'rationale']; identical to §4 (values and key order): True
sandbag:    keys [... same ...]; identical to §4 (values and key order): True
```

The script parses the reading's two ```` ```json ```` blocks and compares them with the table's rows, key by key. Both are exact, rationale included.

### Recomputed: `fabric`, `mesh`, `straw`, and the two new rows

```
$ python -I rows.py assets-src/physics_materials.json
foot share 11.772 N, foot area 1.7671e-04 m2, pressure 66.6 kPa, 25 N 141.5 kPa
fabric
  50-100 mm at the share: 117.7-235.4 N/m
  sink at 160: 73.6 mm; loadouts 3.6/6.5 kg: 55.2 / 99.6 mm
  four feet 640 N/m; old 600 per foot: 19.6 mm
  punch E 10 kPa nu 0.3: 164.8 N/m
  ILD 10 lbf -> E 5.5 kPa ; ILD 30 lbf -> E 16.5 kPa          (50 in² indenter at 25 %)
  slack tarp T 100, a 0.5 m, r 7.5 mm: 149.6 N/m
straw
  km 492.6 N/m; share sink 24.0 mm; 25 N 51.0 mm; cap 80 % 0.100 m
sandbag
  bearing 4.0e+06: k 707 N/m, 25 N sink 35.4 mm
  bearing 5.0e+06: k 884 N/m, 25 N sink 28.3 mm
  bearing 7.0e+06: k 1237 N/m, 25 N sink 20.2 mm
  900: share 13.1 mm, 25 N 27.8 mm;  c 61.9 N s/m -> zeta 0.941
razor_wire
  R 0.45: bare 28.3, f=2 56.6 N/m ; R 0.225: 60*(0.45/R)^3 480 ; R 0.5: 44
  EI 0.3835; four legs 240 N/m sink 0.196 m

$ python -I conformal.py      # Fourier series of the Dirichlet problem, pure Python
square 1 x 1: 0.53935        (exact: 1 / (2 * 0.92704) = 0.53935, the disk-to-square map)
rectangle 3 x 1.5: 0.9478 m  (strip limit 0.9549)
  membrane T 200, r 0.05: 427 N/m
  membrane T 200, r 0.025: 346 N/m
  membrane T 200, r 0.3: 1092 N/m
rectangle 1 x 1 tarp (a = 0.539): T 100, r 7.5 mm 147 N/m
```

**`fabric`** (160 N/m, a 74 mm sink) holds:
- 118–235 N/m is the band from the sheets' 50–100 mm at a foot's 11.8 N
- 74 mm at that share; 55–100 mm over the 3.6–6.5 kg loadouts (knowledge §5.2)
- four feet meet 640 N/m; at 600 a foot sank 19.6 mm
- the foam punch gives 165 N/m (ν 0.3), and the ILD grades give 5–17 kPa
- the slack tarp gives 147–150 N/m

**`mesh`** (430 N/m) holds:
- Y03's 3 m post bay and 1.5 m roll height (houses sheet, Y03)
- conformal radius 0.948 m
- 427 N/m for an arm or the body plates (r 0.05 m); 346 for a foot on one cell; 1092 for the whole drone as one load
- 47 N bows the bay 0.11 m; three parts meet 1290 N/m
- the old 800 implied a = 1.44 m at r 0.3 m

**`straw`** (490 N/m, 24 mm at 11.8 N) holds:
- km 492.6 N/m; 24.0 mm at the share and 51.0 mm at 25 N, under the 0.1 m cap
- S04's `mat_depth_m` is 0.125 (`assets-src/surfaces/S04_patch.json`), and the earthworks sheet's §S04 says "the mat depth and `straw`'s 490 N/m stand"

**`sandbag`** and **`razor_wire`** reproduce §10 of the reading.

### Every row: part, sink or deflection, label

Read row by row; I spot-recomputed each figure:
- `steel`: a 20 × 1.5 tube over 1 m gives 6.1e4
- `sheet_metal`: 0.59 / 1.25 mm
- `timber`: punch 5–16e6; batten 1.45e5
- `masonry`: ≥ 8e7
- `concrete`: ≈ 4e8
- `glass`: plate 1.35e5, 0.087 mm, series −12.5 %
- `cable`: 4T/L = 55.5 N/m
- `foliage`: 236 N/m, 59 mm
- `bark`: 1.19e3
- `twig`: 28.8 N/m, 0.39–0.67 m
- `deadwood`: 3.93 N·m, 7.9 N
- `asbestos_cement`: 0.012 mm
- `rubber`: 8.8e4, 0.24 mm
- `plastic`: 67 / 141 kPa

Seventeen of the 19 rows name the part and give its sink or deflection with a label. Two do not (R2):
- **`cable`** gives the per-part span stiffness (55 N/m, derived) but not the deflection.
- **`plastic`** says "rigid under a foot or an arm" with no number and no label.

Every row carries a row `label`, and `meta.load_physics_materials` accepts the table (the pytest run below).

### `fields.stiffness_n_per_m`

It now reads "contact compliance of the material under one touching part of the drone (a leg's foot, an arm, the body, the coil), in series with that part's own compliance; the contact law applies it to each part that touches, so four feet on it meet four times this; …". **Per touching part: yes.**

### The soft-top line (1000 N/m, PR6)

From `rows.py`, the 25 N sink at each compliant row:

```
sheet_metal 20000 N/m  1.25 mm  soft False   |  rubber 50000  0.50 mm  soft False
foliage       200     125 mm   soft True     |  twig     30   833 mm   soft True
straw         490      51 mm   soft True     |  fabric  160   156 mm   soft True
mesh          430      58 mm   soft True     |  razor_wire 60 417 mm   soft True
sandbag       900      27.8 mm soft True
```

No row crosses the line:
- `fabric` (600 → 160) and `mesh` (800 → 430) were soft before and stay soft.
- The two new ids join the soft set (the test's new expectation).
- No rigid or stiff row changed its value.

### Consumers: the three builds against unity/main

```
$ pwsh tools/build_asset.ps1 S04   -> exit 0, 426 s (cold Library)
$ pwsh tools/build_asset.ps1 V13   -> exit 0, 269 s
$ pwsh tools/build_asset.ps1 K01   -> exit 0, 47 s
  [CatalogImporter] S04_patch: ... 3 LODs, S04_patch_LOD0_straw (20329 straw, LOD0) micro, 199 colliders, ...
  [CatalogImporter] V13_low / V13_mid / V13_tall: 48 / 57 / 66 colliders
  [CatalogImporter] K01_whole: ... 2 LODs, 1 colliders, materials brick -> brick_red
  [Review] S04, V13, K01: every tile rendered (1.5 / 10 / 50 / 200 m), no error or exception line
  S04_patch: audit: soft tops collision[0] surface:belt_straw, collision[1..195] straw, collision[196..198] surface:crater_spoil are landed on over their whole top, held to 50 mm there
$ git status --short               -> (clean)

baseline: blender build.py -- <ID> in a detached worktree of origin/unity/main (4d80a8d)
  main S04 exit 0 302 s ; main V13 exit 0 255 s ; main K01 exit 0 9 s ; that worktree's git status clean
compare every 'audit' line (Blender's version banner, which lands mid-line from stderr, stripped):
  S04 main 202 / branch 202 audit lines, differing 0, sha256 69327C8A5BBB2C4E / 69327C8A5BBB2C4E
  V13 main 183 / branch 183 audit lines, differing 0, sha256 40C345DA484B21B9 / 40C345DA484B21B9
  K01 main 2 / branch 2 audit lines, differing 0, sha256 7A14F63D0B86E80D / 7A14F63D0B86E80D
```

The materials the built assets use are listed below. Of these, only `straw` is compliant, and its value is unchanged:
- K01: `masonry`
- S04: `straw` and two surfaces
- V13: `bark`, `twig`

So identical audits are the expected result.

### Consumers: the sheets

Grepped every sheet line naming the changed ids or quoting a stiffness or a sink. Current:
- `houses-and-yard.md` Y03, now 430 per part
- `lines-roads-debris.md` K08, "mattress and sofa `fabric` (soft, sinks 50–100 mm)": a per-foot 74 mm is inside it
- `fortifications.md` F01: 60 N/m per part, 240 on four legs, 0.20 m; 25 mm on the fence-top coil (4 × 480 N/m → 24.5 mm)
- `fortifications.md` F04: 900 N/m; 13 / 28 / 39–79 mm
- `earthworks-and-surfaces.md` §S04: 490 N/m, 12.5 cm
- `vegetation.md`: 236 / 29 / 200 / 30 N/m
- R01's design: 160 N/m per touching part

**Stale: `vehicles.md` "Physics, shared" (line 124), R1.** Not a quoted value but a disagreement: Y09 (S2).

### Gate checks

```
$ python -m pytest tools -q
692 passed in 1128.36s (0:18:48)          exit 0
$ python tools/hygiene/precommit.py --tree HEAD
598 files scanned, 0 findings             exit 0
$ openspec validate p1-materials-per-part --strict
Change 'p1-materials-per-part' is valid   exit 0
$ python tools/assets/fetch.py --verify
OK                                        exit 0
```

## Spec scenarios and requirements

| Item | Result |
|---|---|
| Scenario "A soft row read per part" (`fabric`) | **Passed.** The stiffness is one 15 mm foot's (160 N/m, assumed, cross-checked by a foam punch and a slack tarp). The rationale gives one foot's sink under its share (74 mm at 11.8 N). Four feet each sink 74 mm (55–100 mm over the loadouts), as K08 says (50–100 mm). |
| Requirement "Physics material rows are per touching part" | **Partly met:** 17 of 19 rows are complete; `cable` and `plastic` lack the sink or deflection (R2). |
| AST-05 (a material id per shape from the closed list) | **Met** for this change's scope: two ids added with physics sign-off, the validator and the tests accept them, and no built asset's audit moved. |
| PHY-01, PHY-03, PHY-05 | **Not testable here:** the behaviour is phase 7. The data they will read is consistent with K§7.1 (`straw`'s mat law, `twig`/`foliage` cantilevers). |

## R items (blocking)

- **R1 · A sheet still reads `sheet_metal` for the whole drone.**
  - **Where:** `docs/object-sheets/vehicles.md`, "Physics, shared", Landable surfaces: "`sheet_metal` gives 2e4 N/m, so the drone's 49 N weight (5 kg class) dents a roof about 2.5 mm before it holds (derived from the material row)".
  - **What is wrong:** the sentence puts 49 N on one contact. That is the whole-drone reading this change replaces. Per touching part, each foot's quarter (12.3 N at 5 kg) dents the roof about **0.6 mm**, the row's own new rationale (0.6 mm at 11.8 N). 49 / 2e4 = 2.45 mm; 12.3 / 2e4 = 0.61 mm.
  - **Fix:** make that clause per part.
  - **Owner:** a docs fix (the orchestrator, as for the three quotes in `7eee1ee`).
- **R2 · Two rows give no sink or deflection for the part.**
  - **Why it blocks:** the change's requirement says a rationale "SHALL name the part it was derived for and the sink or deflection it gives that part, with its label".
  - **`cable`:** add the deflection. One foot's 11.8 N share at mid-span deflects the 30 m span P·L/(4T) = 11.8 × 30 / (4 × 416) = **0.21 m** (derived), and the whole drone's 47 N about 0.85 m.
  - **`plastic`:** give "rigid under a foot or an arm" a number and its label, as the other rigid rows do (a foot on a PVC profile wall or a crate rib). The 3 mm bumper cover's 1e2 N/m also needs its label.
  - **Owner:** the physics engineer. Text only; no value changes, so the tests and audits stand.

## S items (suggestions, non-blocking)

- **S1 · The `sandbag` soft top (the physics engineer's flagged risk): acceptable, with a note for F04's asset change.**
  - **The ruling:** by PR6's own definition the row is soft. The 25 N reference foot sinks 27.8 mm ≥ 25 mm, the same footing as `surface:belt_straw` (30.1 mm). The bag visibly gives (13 mm at rest, 39–79 mm in a 3–6 g landing), so the 3 cm the wider tolerance adds stays within the settle, as PR6 argues.
  - **What the rule relaxes:** a scratch probe of `audit._zone` shows that a landable `sandbag` box drops only its own landing faces from the 2 cm zone. The scene had a sandbag box, a concrete block and a gap: 0 landing faces against 32 when the box is `concrete`. The concrete block's faces (60) and the gap polygon stay. So F04's entrance and firing ports keep ±2 cm through their gaps, and the block tops keep it too. What relaxes is the wall top and the bulging faces within 0.3 m below it, which the sheet's 20–40 mm bulge would otherwise force into bulge-following boxes.
  - **F04's asset change should say:**
    - (a) the class sits 2.8 mm inside the line, and the reading's own bearing band (4–7e6 N/m³) gives 707–1237 N/m, straddling 1000; a re-tune of `sandbag` above 1000 N/m moves F04's wall tops into the 2 cm zone and must rebuild F04;
    - (b) the audit line naming the soft tops is expected;
    - (c) the 1.5 m review tile should show the wall top: at the 13 mm static sink, a top box 5 cm off the crown can read as a float of up to about 37 mm.
- **S2 · Y09's "a leg sinks 5–15 cm" against `straw`.**
  - **Where:** `houses-and-yard.md` Y09, Material ids. The row gives 24 mm at rest and 51 mm at 25 N, capped at 0.1 m (80 % of the 0.125 m mat).
  - **Why it is not R:** the value did not change here, and the sentence is the analyst's reading, not a quote of the row. But the row's mat law is derived for a 0.125 m mat (deeper mats come out stiffer under it, a 20 cm mat ≈ 730 N/m), and a stack is not a mat.
  - **Ruling:** at Y09's (and H05's, O04's) asset change, the physics engineer rules whether a stack's shape writes its own `stiffness_n_per_m`, as the `razor_wire` precedent allows, or the sheet's text follows the row.
- **S3 · "a prop guard" in the proposal and the spec delta.** The Svinoriz 10 Opto has none: knowledge §5 and the reading's parts are 4 legs, 4 arms, 4 prop discs, the body and the coil. The `fields` text already says "a leg's foot, an arm, the body, the coil". Fix the spec delta's parenthesis before archive, since it lands in `openspec/specs/asset-pipeline/spec.md`.
- **S4 · A linear compliant row has no stroke limit (phase 7, physics engineer).** At 160 N/m, a 3–6 g landing drives a foot 0.22–0.44 m (35.3 / 70.6 N), deeper than a 0.15–0.2 m mattress. The mat law caps at 80 % of D; an object row has no cap. The latch (knowledge §7.5) and whatever shape lies beneath then decide the outcome. The phase-7 contact law should bound a compliant shape's stroke by its own thickness, or the asset should model what lies beneath.
- **S5 · Carry into F01's asset change.** Still to come there, as the reading says:
  - the `fields.edge_radius_m` sentence of the reading's §1 ("unless the record carries its own `edge_radius_m`")
  - the per-shape `stiffness_n_per_m` override with its `meta.validate` rule
  - the record fields `edge_radius_m` and `barb_pitch_m`

  The `razor_wire` rationale already refers to the first two; today the validator does not check them.
- **S6 · At archive.** `docs/plan.md` phase 1 step 7 still describes this sign-off as pending (orchestrator).

## Re-check of R1 and R2

**Reviewer:** a fresh qa-engineer (did none of the work, D-034) · **Tip:** `624f024` · **Range:** `35a61c6..624f024` (3 commits, 6 files)
**Sources read:** `CLAUDE.md`, `AGENTS.md`; the review above (R1, R2, S1–S6); this change's proposal, spec delta and tasks; the `cable`, `plastic`, `sheet_metal`, `timber`, `glass`, `steel` and `rubber` rows; `docs/object-sheets/lines-roads-debris.md` (the conductor table and L03); `docs/object-sheets/vehicles.md`; `docs/knowledge.md` §7.4; `tools/blender/lib/meta.py`.

No build was run. No value changed since the builds above (check 3 proves it by script), so the S04, V13 and K01 audit comparisons still hold. The arithmetic ran in plain Python scripts (no numpy) in QA's scratchpad, with `python -I`. They are not committed.

### Verdict

**APPROVED.** R1 and R2 are fixed, and so is S3:
- Every rationale now names its part and gives that part's sink or deflection, with a label.
- The cable's two deflections recompute. The downward one is also confirmed by an exact elastic catenary, not only by Irvine's linear theory.
- The diff holds only what the review asked for, and no value or key moved.

There are three new suggestions, S7–S9. None blocks.

### Check 1 · R1: the roof dent per foot (`f2dde94`)

```
$ git diff 35a61c6..HEAD -- docs/object-sheets/vehicles.md      (line 124, Landable surfaces)
- `sheet_metal` gives 2e4 N/m, so the drone's 49 N weight (5 kg class) dents a roof about 2.5 mm before it holds (derived from the material row)
+ `sheet_metal` gives 2e4 N/m per touching part, so each foot's quarter of the drone's 49 N weight (5 kg class, 12.3 N) dents a roof about 0.6 mm before it holds (derived from the material row, `p1-materials-per-part`)
49 / 4 = 12.25 N ; 12.25 / 2e4 = 0.61 mm
sheet_metal row: "a foot's 11.8 N share sinks 0.6 mm (1.2 mm at 25 N)"     (11.8 / 2e4 = 0.59 mm)
```

**Met.** The clause is now per part and agrees with the row. Its 5 kg class and the row's 4.8 kg differ by 4 %, so both round to 0.6 mm.

### Check 2 · R2: `cable` and `plastic` (`49d91c9`)

**`cable`.** The rationale now gives:
- the reference span, a 12 mm bare stranded aluminium conductor on a 30 m span at 2 % sag;
- the sideways deflection: 4T/L ≈ 55 N/m, and 0.21 m at a foot's share (0.20 m with the span's fixed length);
- the downward stiffness: ≈ 200 N/m at small load (Irvine), stiffening, so the share sinks 0.044 m.

Each figure carries its label. I checked the span against L03:
- 12 mm is the top of its 7–12 mm medium class;
- 30 m is the short end of its 30–45 m street spans;
- its default sag is 2 %.

The recompute:

```
$ python -I cable.py              # excerpt: the P = 1 and 5 N lines and the inextensible block's other lines left out
w = 2.2187 N/m -> 0.2262 kg/m -> area 83.8 mm2 (Al 2700); 7-strand 12 mm 88.0, 19-strand 86.0 mm2
T = w L^2/(8 sag) = 416.0 N
sideways 4T/L = 55.47 N/m ; P L/(4T) = 0.2122 m at P = 11.772 N
Irvine E 6e+10: lambda^2 309.2, k 199.5 N/m, linear sink 0.0590 m, h/H 0.255
Irvine E 5.5e+10: lambda^2 283.4, k 197.8 N/m, linear sink 0.0595 m, h/H 0.255
Irvine E 6e+09: lambda^2 30.9, k 120.7 N/m, linear sink 0.0976 m, h/H 0.191
inextensible limit 16T/L = 221.9 N/m

exact catenary, E 6e+10: S0 30.029494 m, unloaded H 416.19 N, sag 0.6000 m
  down P   0.100 N: sink 0.0005 m, secant k  200.2 N/m, H 417.1 N (+0.2 %)
  down P  11.772 N: sink 0.0437 m, secant k  269.4 N/m, H 525.1 N (+26.2 %)
  down P  23.544 N: sink 0.0696 m, secant k  338.1 N/m, H 636.7 N (+53.0 %)
  down P  47.088 N: sink 0.1011 m, secant k  465.6 N/m, H 859.9 N (+106.6 %)
  side P   0.100 N: swing 0.0018 m, secant k   55.5 N/m, mid rises 0.0000 m, H 416.2 N
  side P  11.772 N: swing 0.2032 m, secant k   57.9 N/m, mid rises 0.0253 m, H 434.6 N
exact catenary, E 1e+30: S0 30.031976 m, unloaded H 416.22 N, sag 0.6000 m
  down P  11.772 N: sink 0.0373 m, secant k  316.0 N/m, H 530.5 N (+27.4 %)
  side P  11.772 N: swing 0.2028 m, secant k   58.0 N/m, mid rises 0.0263 m, H 435.4 N
```

How the script works:
- **Irvine:** λ² = (wL/H)²·L/(H·Le/EA), and k = (T/L)/(1/4 − (3/16)/(1 + 12/λ²)). I derived the formula from the linearised cable equation with a mid-span point load and the compatibility condition; it is the row's formula.
- **Exact model:** Irvine's parametric elastic catenary. Each half-span is solved by bisection, with the unstretched length fixed by the unloaded 0.6 m sag. For the sideways case, each half lies in its own vertical plane, with Hy = P/2 at mid-span.

**Rulings on `cable`:**
- T ≈ 416 N, 4T/L = 55.5 N/m and 0.21 m all recompute.
- **0.20 m with the fixed length:** the exact catenary gives 0.203 m.
- **λ² ≈ 310:** 309.2 at 84 mm² and E 60 GPa.
- **The downward figure is sound.** Irvine's small-load 199.5 N/m matches the exact catenary's 200.2 N/m. The share's 0.044 m is the nonlinear result (exact 0.0437 m). The linear 0.059 m would be wrong here: the share raises H by 26 %, beyond linear theory's small-h assumption (h/H 0.26). So "stiffening with load" is right, and the row gives the right number for it.
- **E barely matters:** 55 GPa gives 197.8 N/m. The inextensible limit is 222 N/m with a 0.037 m sink. Only a tenfold softer E (6 GPa) would drop it to 121 N/m.
- **For phase 7, which reads compliance from each wire's geometry:**
  - λ² does not depend on the conductor's section. It is (8d/L)²·8(d/L)·E/(ρ·g·L)·L/Le.
  - So every aluminium conductor at 2 % sag on 30 m has λ² ≈ 310. Its downward stiffness is 3.6 times its sideways one, and both scale with the wire's weight.

The knowledge §7.4 line (`624f024`) states the same 55 N/m sideways, about 200 N/m pressed down, stiffening with load. **Met.**

**`plastic`.** The rationale now gives:
- the reference part (assumed): a 3 mm PP or ABS wall, 50 mm across between its ribs or webs;
- one 15 mm foot on it: 1.2–4.9e5 N/m (published E 1.3–2.4 GPa, derived), a sink of 0.02–0.1 mm at the share, and a 4–15 % series difference with the leg, as on glass;
- the local punch, 2–4e7 N/m, about 1 % off;
- the bumper cover, moved out of the row as the car asset's own shape `stiffness_n_per_m`: 1.2–4e3 N/m, sinking 3–10 mm, derived. This follows the `timber` batten precedent, whose rationale reads "a batten asset may override with ≈ 1e5 N/m".

The rationale also explains the old 1e2 N/m as a 0.1 m beam strip. The recompute:

```
$ python -I plastic.py            # excerpt; circle: point-load deflection averaged over a 7.5 mm patch (reciprocity); square: Navier, equal-area patch
wall 50 mm across
  E 1.3 GPa nu 0.35: circle SS   1.2e+05 clamped  3.25e+05 | square SS  1.28e+05 clamped ~ 3.01e+05 N/m
  E 2.4 GPa nu 0.35: circle SS  2.21e+05 clamped  6.01e+05 | square SS  2.36e+05 clamped ~ 5.55e+05 N/m
  E 2.4 GPa nu 0.40: circle SS  2.36e+05 clamped  6.27e+05 | square SS  2.46e+05 clamped ~  5.8e+05 N/m
wall 500 mm across
  E 1.3 GPa nu 0.35: circle SS  1.08e+03 clamped  2.69e+03 | square SS  1.15e+03 clamped ~ 2.39e+03 N/m
  E 1.8 GPa nu 0.40: circle SS   1.6e+03 clamped  3.89e+03 | square SS  1.67e+03 clamped ~ 3.46e+03 N/m
point load, no patch, 50 mm: square SS nu 0.4 E 1.3 = 1.2e+05 ; circle clamped nu 0.35 E 2.4 = 4.95e+05 N/m
row band 1.2e+05-4.9e+05 N/m: sink 0.024-0.098 mm; series with the leg's 2e4 differs 3.9-14.3 %
  punch E b/(1-nu^2), E 1.3 nu 0.35: 2.22e+07 N/m; relief on 1.2e+05: 0.54 %, on 4.9e+05: 2.21 %
  punch E b/(1-nu^2), E 2.4 nu 0.4: 4.29e+07 N/m; relief on 1.2e+05: 0.28 %, on 4.9e+05: 1.14 %
five times the leg's spring (1e5 N/m) is reached at a span of 56 mm (SS, E 1.3) to 114 mm (clamped, E 2.4)
bumper strip 0.1 m x 3 mm over 0.5 m, SS beam 48EI/L^3 at E 1.3: 112 N/m
  bumper 1.2e+03 N/m: share sinks 9.8 mm
  bumper 4e+03 N/m: share sinks 2.9 mm
```

**Rulings on `plastic`:**
- The row's band is the point-load band, and it recomputes (1.20e5 and 4.95e5).
- With the foot as a 15 mm patch, the clamped end is stiffer: 5.6–6.3e5. So the band errs soft at the top, and the conclusion "rigid per part, as glass" only gets stronger.
- The sink, the series difference, the punch and the bumper figures all recompute.
- **Met.** One band edge is noted in S8.

### Check 3 · The diff since the review

```
$ git diff --stat 35a61c6..HEAD
 assets-src/physics_materials.json                                   | 4 ++--
 docs/knowledge.md                                                   | 2 +-
 docs/object-sheets/vehicles.md                                      | 2 +-
 openspec/changes/p1-materials-per-part/proposal.md                  | 2 +-
 openspec/changes/p1-materials-per-part/specs/asset-pipeline/spec.md | 2 +-
 openspec/changes/p1-materials-per-part/tasks.md                     | 2 +-
 6 files changed, 7 insertions(+), 7 deletions(-)

$ python -I table_diff.py <35a61c6 table> <HEAD table>      # order-preserving walk (object_pairs_hook), every key and value
rows: 19 ids: ['steel', 'sheet_metal', 'timber', 'masonry', 'concrete', 'glass', 'cable', 'foliage', 'bark', 'twig', 'deadwood', 'asbestos_cement', 'straw', 'fabric', 'rubber', 'plastic', 'mesh', 'razor_wire', 'sandbag']
paths that differ: ['/materials/cable/rationale', '/materials/plastic/rationale']
every differing path is a rationale: True

$ grep -rni "prop guard" openspec/changes/p1-materials-per-part/ assets-src/physics_materials.json docs/knowledge.md
openspec/changes/p1-materials-per-part/review.md:249   (S3's own text only)
```

**As briefed.** Every file and line in the diff is allowed:
- **The table:** only the two rationales changed. No value moved, and no key changed its order or set.
- **`vehicles.md`:** one line, R1.
- **The change folder:** one line each in the proposal, the spec delta and task 1.1. "a prop guard" became "the body or the coil", which closes S3.
- **`knowledge.md` §7.4:** the wire line only.

Nothing is under `reference/` or `unity/{Library,Temp,Logs}/`.

### Check 4 · Gates and commits

```
$ python -m pytest tools/blender/tests/test_meta.py tools/blender/tests/test_audit.py -q
85 passed in 35.26s                          exit 0
$ python tools/hygiene/precommit.py --tree HEAD
599 files scanned, 0 findings                exit 0
$ openspec validate p1-materials-per-part --strict
Change 'p1-materials-per-part' is valid      exit 0
$ git log --format='%h | %an <%ae> | %cn <%ce> | %s | body:[%b]' 35a61c6..HEAD
624f024 | Orchestrator <orchestrator@450fpv.local> | Orchestrator <orchestrator@450fpv.local> | docs(knowledge): a wire span is stiffer pressed down than pushed aside | body:[]
49d91c9 | Physics Engineer <physics@450fpv.local> | Physics Engineer <physics@450fpv.local> | docs(physics): give cable and plastic the deflection one part makes | body:[]
f2dde94 | Orchestrator <orchestrator@450fpv.local> | Orchestrator <orchestrator@450fpv.local> | docs: the roof dent per foot (r1), no prop guard in the change | body:[]
subject lengths 70 / 67 / 62 (limit 72)
```

All three commits pass:
- each is subject-only, with no body and no trailer;
- each is under its role's identity from `docs/team.md`, with author equal to committer;
- each matches the hook's format;
- each was made by the owner the review named: the orchestrator for the docs fix, the physics engineer for the rows.

### Spec scenarios and requirements (updated)

| Item | Result |
|---|---|
| Scenario "A soft row read per part" (`fabric`) | **Passed** (unchanged since the review; `fabric` is untouched by this range). |
| Requirement "Physics material rows are per touching part" | **Met:** all 19 rows name the part and give its sink or deflection with a label. |
| AST-05 | **Met** (unchanged). |
| PHY-01, PHY-03, PHY-05 | **Not testable here** (phase 7), as above. |

### S items (new, non-blocking)

- **S7 · `vehicles.md`'s tyre sink against `rubber`.**
  - **Where:** "Open points for the whole sheet", `rubber`: "a leg on a tyre sinks a few millimetres".
  - **The row:** 5e4 N/m, 0.24 mm at a foot's share. Even the whole drone's 47 N on one foot gives 0.94 mm.
  - **Why it is not R:** the value is unchanged on this branch, and the line is the analyst's reading, as in S2.
  - **Fix:** the sheet line follows the row (orchestrator, docs). If a soft or deflated tyre is meant, the R asset's change writes that tyre's own shape stiffness.
- **S8 · `plastic`'s five-times span.**
  - **The gap:** "past 55–100 mm between ribs a wall falls below five times the leg's spring" recomputes to 56–114 mm. The row's own 4.9e5 at 50 mm, scaled by 1/a², gives 111 mm.
  - **Effect today:** none on the row.
  - **When it matters:** if the five-times line becomes the rule for when a shape writes its own stiffness, record it with its recomputed band in the `fields` text or knowledge §7.4. That falls to the physics engineer, at the first asset that uses it.
- **S9 · The bumper cover needs S5's per-shape override.**
  - **The gap:** the rationale says a car asset with a bumper cover writes its own shape `stiffness_n_per_m`. `meta.validate` has no per-shape rule today: `meta.py` line 60 only pairs a row's stiffness with its damping.
  - **Fix:** whichever comes first, F01 (S5) or the first R asset with a bumper cover, adds the rule.
  - **Audit impact:** none. 1.2–4e3 N/m is above the 1000 N/m soft line, so that asset's audit zone does not change.
- **Still open from the review:** S1, S2, S4, S5 and S6 (S6 at archive). S3 is done.
