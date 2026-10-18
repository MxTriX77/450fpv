# Review: p1-fortifications-sheet (task 2.1)

**Reviewer:** qa-engineer, a fresh agent that did none of tasks 1.1–1.4 (D-034: spec scenarios plus plan conformance) · **Branch:** `unity/ref/p1-fortifications-sheet` at `e588799`; merge base with `origin/unity/main` is `c570d68` (the proposal's merge), so `origin/unity/main...HEAD` is this change alone · **Requirements:** AST-01, AST-02, AST-04, AST-05, AST-06, SEC-01, SEC-02 (as amended by D-037), WLD-01 · **Decisions:** D-003, D-024, D-028, D-032, D-035, D-037

**Verdict: CHANGES REQUESTED** (R1–R3 below). The sheet is complete, its geometry and physics hold up under an independent recomputation, and both readings are folded in. What blocks the merge is wording and labels:
- the placement rules describe the fortifications tactically, which SEC-02 now forbids for F01–F04;
- five "published" figures are not what their cited source says;
- F01's LOD2 still thins the turns, against the buildability ruling.

Each is a one-pass edit by the reference analyst.

## Scope

The diff `origin/unity/main...HEAD` touches 5 files (1,450 insertions, 9 deletions):
- `docs/object-sheets/fortifications.md` (new, 606 lines)
- `docs/asset-list.md` §12 (the Key size and Physics cells)
- this folder's `review-physics.md`, `review-buildability.md` and `tasks.md`

I read `CLAUDE.md`, `AGENTS.md`, `docs/workflow.md`, the proposal, the design, the tasks, the spec delta, both readings with their "Closed in 1.4" sections, `openspec/specs/object-sheets/spec.md` and the whole sheet. I also read the requirements as they stand on `origin/unity/main`, because D-037 (merged after this branch was cut) amends SEC-02 and names F01–F04.

The branch is 11 commits behind `origin/unity/main`. None of those commits touches this change's files, so it merges cleanly.

I did not open `reference/`. No letter shows these objects, so there is no side-by-side sheet or review sheet to look at (REAL-03 and the 1.5/10/50/200 m sheets apply to the asset changes that build them). Every check below was run by me from this worktree. The scripts are throwaway files in my scratchpad, not committed. Line numbers are at `e588799`.

## Checks run

```
$ python tools/hygiene/precommit.py --tree HEAD
519 files scanned, 0 findings                                   exit=0

$ openspec validate p1-fortifications-sheet --strict
Change 'p1-fortifications-sheet' is valid                       exit=0

$ git log --format='%h|%an|%ae|%s|body=[%b]|trailers=[%(trailers)]' origin/unity/main..HEAD
e588799|Reference Analyst|reference@450fpv.local|docs(openspec): record fortification rework closures and tick 1.4|body=[]|trailers=[]
a409dbd|Reference Analyst|reference@450fpv.local|docs(ref): rework teeth, hedgehog and checkpoint rows from readings|body=[]|trailers=[]
edab4e2|Reference Analyst|reference@450fpv.local|docs(ref): rework concertina turn model, snag records and coil|body=[]|trailers=[]
042f8a9|Asset Artist|assets@450fpv.local|docs(openspec): add buildability review of fortifications sheet|body=[]|trailers=[]
9712bbd|Physics Engineer|physics@450fpv.local|docs(physics): add physics reading of the fortifications sheet|body=[]|trailers=[]
c520ea6|Reference Analyst|reference@450fpv.local|docs(ref): pin fortification key sizes in the asset list|body=[]|trailers=[]
05dde8e|Reference Analyst|reference@450fpv.local|docs(ref): correct tooth face angles and source labels in sheet|body=[]|trailers=[]
b9b2c3c|Reference Analyst|reference@450fpv.local|docs(ref): add hedgehogs and checkpoint kit to fortifications sheet|body=[]|trailers=[]
57aa3a1|Reference Analyst|reference@450fpv.local|docs(ref): add fortifications sheet with concertina wire and teeth|body=[]|trailers=[]
(9 commits, 0 merges, longest subject 67 characters; every identity is the role's from docs/team.md)

$ git diff --name-only origin/unity/main...HEAD | grep -iE '^reference/|\.(png|jpe?g|gif|webp|tiff?|mp4|mov|exr|hdr)$|Library/|Temp/|Logs/'
(no output)                                                     exit=1

$ python -I coverage.py .        # IDs in asset-list §1–10 and §12 against "## <ID> " headings in the seven sheets
sheets: 7 ['earthworks-and-surfaces.md', 'fortifications.md', 'houses-and-yard.md', 'lines-roads-debris.md', 'urban-edge.md', 'vegetation.md', 'vehicles.md']
asset-list IDs (sec 1-10, 12): 107 distinct: 107 per section: {1: 13, 2: 7, 3: 21, 4: 6, 5: 19, 6: 6, 7: 4, 8: 8, 9: 9, 10: 10, 12: 4}
sheet ## <ID> headings: 107 distinct: 107
missing: []
extras: []
duplicates: {}
per sheet: {'earthworks-and-surfaces.md': 20, 'fortifications.md': 4, 'houses-and-yard.md': 25, 'lines-roads-debris.md': 17, 'urban-edge.md': 10, 'vegetation.md': 21, 'vehicles.md': 10}
section 12 -> [('F01', 'fortifications.md'), ('F02', 'fortifications.md'), ('F03', 'fortifications.md'), ('F04', 'fortifications.md')]

$ python -I frows.py docs/object-sheets/fortifications.md   # Seen in, template sections, label census, "measured"
F01: seen-in='R: research only.' research-sources=True missing-sections=[]
     dimension rows 21 label census {'published': 13, 'derived': 10, 'assumed': 7, 'measured': 0} rows without a label word: []
     colour rows 5 without a label: []
     lines containing 'measured': []
F02: seen-in='R: research only.' research-sources=True missing-sections=[]
     dimension rows 15 label census {'published': 9, 'derived': 6, 'assumed': 5, 'measured': 0} rows without a label word: []
     colour rows 5 without a label: []
     lines containing 'measured': []
F03: seen-in='R: research only.' research-sources=True missing-sections=[]
     dimension rows 16 label census {'published': 9, 'derived': 9, 'assumed': 6, 'measured': 0} rows without a label word: []
     colour rows 5 without a label: []
     lines containing 'measured': [(62, '- **between a pair at 2.0 m centres:** the throat polygon lies in the vertical plane through both centres, its')]
F04: seen-in='R: research only.' research-sources=True missing-sections=[]
     dimension rows 26 label census {'published': 15, 'derived': 7, 'assumed': 16, 'measured': 0} rows without a label word: []
     colour rows 9 without a label: []
     lines containing 'measured': []

$ python -I gentable.py docs/object-sheets/fortifications.md  # type, range, default, effect; "from damage" rows
F01: 33 params; rows with an empty cell or no range: []; range written in the Type cell (house style): 4; 'from damage' rows 4, without I and D values: []
F02: 22 params; rows with an empty cell or no range: []; range written in the Type cell (house style): 5; 'from damage' rows 3, without I and D values: []
F03: 22 params; rows with an empty cell or no range: []; range written in the Type cell (house style): 2; 'from damage' rows 1, without I and D values: []
F04: 40 params; rows with an empty cell or no range: []; range written in the Type cell (house style): 6; 'from damage' rows 5, without I and D values: []
removed 'loop_lean_deg': True
removed 'clips_per_turn': True
removed 'collision_boxes': True
removed 'trampled_spans': True
```

The one "measured" hit (F03, fortifications.md:388) is the prose "measured along the line of centres", a direction, not a label. No number in the sheet is labelled measured.

## Spec scenarios

| Requirement | Scenario | Result | Evidence |
|---|---|---|---|
| One sheet per class group, every row covered (MODIFIED) | Coverage check | **PASS** | 107 of 107 IDs in asset-list §1–10 and §12, each exactly once, no extras, no duplicates; F01–F04 only in `fortifications.md`; all four rows carry the nine template sections plus Seen in and Research sources |
| Research-only sheets (ADDED) | Research-only row | **FAIL** (R2) | Every F row says "Seen in: R"; no number is labelled measured; every published row names its source. But 6 of the 35 checks against opened sources fail (the five R2 items). No source identifies the pilot's places, units or dates (OPSEC below) |
| Measured, labelled numbers | Label present | **PASS**, with R2 | 78 dimension rows, all labelled (published 46, derived 32, assumed 34, measured 0; a row can carry two labels); 24 colour rows, all labelled |
| Materials and colours resolve to the pipeline | Material resolves | **PASS** | 10 visual sets, all on Poly Haven or ambientCG (CC0), each id confirmed live through the sites' APIs; physics ids `steel`, `concrete`, `timber` from the closed list, `razor_wire` and `sandbag` with the physics reading's §4 rows; no part uses `fabric` (it appears in the id list, as the friction `sandbag` borrows, and where `sandbag` replaces it) |
| Physics role in the pipeline's terms | Gap stated against the drone | **PASS** | Every F02–F04 opening has its narrowest width and a passes / does not pass verdict against 0.574 m and 0.706 m; recomputed below, 0 mismatches; F01 states no gap with the reason |
| OPSEC-clean | Hygiene scan passes | **FAIL** (R1) | The scan reports 0 findings, and no source names the pilot's places. But the placement rules describe the fortifications tactically, against SEC-02 as D-037 amended it |
| Buildable | Buildability review recorded | **PASS**, sheet partly reflects it (R3) | `review-buildability.md` lists per row what is buildable and what was sent back; all 7 sent-back items are closed; the review's ruling on F01's LODs is not (R3) |

## Research-only rows: the sources opened

I opened the cited pages or files that load from here. Each was checked against the row that cites it.

| # | Row | Figure in the sheet | Source opened | Result |
|---|---|---|---|---|
| 1 | F01 Tape profile | NATO-22: 16 mm across the barbs, clusters 22 mm long at a 35 mm pitch | the NATO-22 flyer (its drawing) | match |
| 2 | F01 Tape thickness | 0.50 mm | flyer drawing | match (the 0.6 mm end cites a listing that refused me, HTTP 429) |
| 3 | F01 Core wire | 2.5 mm | flyer drawing | match |
| 4 | F01 Coil diameter | rows of 450, 600, 720, 750, 900, 980, 1000 mm | flyer table and catalogue | match |
| 5 | F01 Turns per coil | 56 "in every concertina row" | catalogue | **no**: the catalogue's military BTC row at 900 mm has 50 loops (R2 b) |
| 6 | F01 Yield | 15 m for "0.6–1.0 m coils", 11 m for 0.45 m | flyer and catalogue | **no** for 0.6 m: 11 m (36 ft) at 600 mm; 15 m from 720 mm up (R2 a) |
| 7 | F01 Loop extent | 0.533 m for "0.6–1.0 m coils", 0.406 m for 0.45 m | flyer and catalogue | **no** for 0.6 m: 406 mm (R2 a) |
| 8 | F01 Clips | 3 a circle (0.45–0.6 m), 5 (0.72–1.0 m), 5–7 (0.98 m) | flyer and catalogue | match (720 mm takes 3 or 5) |
| 9 | F01 Yield, tight-clipped option | 4.3 m | flyer | match |
| 10 | F01 Coil mass | 8 kg (0.45 m), 16 kg (0.90 m), 17 kg (1.0 m) | catalogue product selector | match |
| 11 | F01 Picket length and width | 0.81, 1.52, 1.83, 2.44 m; 76 mm wide | catalogue, fence pickets | match |
| 12 | F01 Angle picket | 50 × 50 × 5: 4.8 cm², 3.77 kg/m | EN 10056-1 table | match |
| 13 | F01 Wire spiral, rows and tiers | 0.7–1.3 m, 10 m long, 0.9 m high; 2–3 rows by 1–2 tiers | ru encyclopedia | match |
| 14 | F01 Construction | "the encyclopedia's 'three staples' for a self-supporting coil" | ru encyclopedia | **no**: it gives 3 to 7 rivets joining the turns in a staggered pattern (R2 c) |
| 15 | F02 Height | 0.9–1.2 m | en encyclopedia | match |
| 16 | F02 Height | 2 ft 8 in to 3 ft | the bulletin | match |
| 17 | F02 Rows and spacing | three or four staggered rows; 6–8 ft in a row and between rows | the bulletin | match |
| 18 | F02 Base proportion | a frustum 3 ft square by 2 ft high; a triangular pyramid with 6 ft sides, 5 ft high | the heritage thesaurus, both terms | match (base / height 1.5 and 1.2) |
| 19 | F02, F04 Density | 2400 kg/m³ | ГОСТ 13579-78 note | match |
| 20 | F03 Member length, mass, angle, bolts | 1.8 m and 2.1 m; 198 kg; L 140/140/13; third member on M20 bolts | en encyclopedia | match |
| 21 | F03 Height relation, members, joints, setting | h = a√6/3 with L = a√2; about 1 m; up to 2 m, about 1.2 m; I-beams No. 25–40; riveted gussets; asphalt best, concrete slides | ru encyclopedia | match |
| 22 | F03 Member: rail | Р50 "published (encyclopedias, the seller's datasheet)" | both encyclopedias | **no**: neither names Р50 (R2 d) |
| 23 | F03 Р50 section | 152 high; web 83 × 16; head 72; foot 132; foot edge 10.5 mm | the ГОСТ Р 51685 table | match |
| 24 | F03 Р50 head depth | 42 mm | the rail maker's table | the value is there, in a column the page does not label (S3) |
| 25 | F03 Р50 area, mass, shares | 65.99 cm², 51.8 kg/m; head 38 %, web 24 %, foot 37 % | ГОСТ 7174-75 | match (38.12 / 24.46 / 37.42 %) |
| 26 | F03 Р65 section | 180; web 105 × 18; head 75; foot 150; edge 11.2 mm; 82.65 cm², 64.9 kg/m | the same table; ГОСТ 8161-75 | match (64.88 kg/m after the standard's amendment) |
| 27 | F03 `angle_100` | 15.5 cm², 12.2 kg/m, root 12 mm, toe 6 mm | EN 10056-1 table | match |
| 28 | F03 `channel_100` | 100 deep, flanges 46 × 7.6, web 4.5, root 7 mm, 10.9 cm², 8.59 kg/m | ГОСТ 8240-97 | match |
| 29 | F04 ФБС blocks | 2380 × 600 × 580 mm, 0.815 m³, 1.96 t, and the three other sizes; 1.46–2.36 kg of steel | ГОСТ 13579-78 | match |
| 30 | F04 Tolerances | ±13 mm length, 8 mm width and height, 3 mm flatness | ГОСТ 13579-78 | match |
| 31 | F04 Lifting loops | the flush option 300 mm from the ends | ГОСТ 13579-78 | match |
| 32 | F04 Sandbag | 0.36 × 0.66 to 0.43 × 0.81 m; polypropylene or hessian; half to two thirds full; laid like brickwork; 20 in (0.51 m) of sand proof | en encyclopedia | match |
| 33 | F04 Barrier | 1067 mm overall; arms 3–8 m; red and white or yellow and black; end rest | the manufacturer's datasheet | match |
| 34 | F04 Counterweights and rope | counterweights; a stabilising rope from 5.5 m | the manufacturer's page | match |
| 35 | F04 Swinging support | "arms 5.5 m and longer need a swinging support" | the manufacturer's page | **no**: there the support is required from 4.0 m (R2 e) |

Not opened:
- the barbed-tape listing (HTTP 429 three times);
- the rail seller's datasheet (HTTP 403);
- the training pamphlet (a 6 MB scan, which feeds the derived 3.8 m picket spacing);
- the clip patent, the second Р50 standard page and the ru encyclopedia on teeth rows.

## Plan conformance (D-034)

**Proposal.** Every "What Changes" item is delivered:
- the sheet, with every number labelled;
- the physics role in pipeline terms;
- placement rules;
- asset-list §12 pinned;
- both readings.

All non-goals hold: no generator, no model, no layout, no edit to `assets-src/physics_materials.json`.

**Tasks.** 1.1–1.4 are ticked. The commits show the order: the sheet (1.1), then the two readings (1.2, 1.3), then the rework (1.4).

**Design decisions:**

| # | Decision | Conforms | Note |
|---|---|---|---|
| 1 | Generic sources | yes | All are standards, datasheets, manuals, encyclopedias and a heritage thesaurus; no news, no social media, nothing about the pilot's region (OPSEC below) |
| 2 | Labels | partly | Every figure has a range, a default and a label. Five published labels are wrong (R2) |
| 3 | F01's snag model | yes | The tape section, barb length and pitch, coil diameter, stretched pitch, clips, picket section and spacing, and the sag and tear are all given. One `edge` per turn at the tape's half-width carries the barb pitch. Porosity is derived; no gap |
| 4 | Gaps against the drone | yes | F02 at 0, 0.3, 0.5 and 1.0 m; F03's windows, node and pair; F04's arm, lane, joints, entrance and ports |
| 5 | Materials | yes | `concrete`, `steel`, `razor_wire` and `sandbag` (both accepted by the physics reading), and CC0 visual sets |
| 6 | States | yes | Every D item is physical and parameter-driven (a gap in S8) |
| 7 | Placement from published descriptions only | **no** | Most placement figures are labelled assumed ("the usual doctrine; no primary manual could be reached"). The wording is tactical (R1). Dropping those figures loses nothing published |
| 8 | Work split | yes | One analyst, two parallel readings, one rework, QA last |

## Gaps against the drone (recomputed)

The drone's yaw window is θmax = asin((W − 0.254) / 0.4525) − 45°.

| Gap | Sheet | My recomputation | Agrees |
|---|---|---|---|
| F02 in a row, w(z) = 1.2 − 0.9 z | 0.80 / 1.07 / 1.25 / 1.70 m; inscribed circle 1.0 m | 2.0 − w: 0.80, 1.07, 1.25, 1.70. The circle is limited by the 1.0 m height: its centre stands 0.570 m from each 65.8° face | yes |
| F02 straight across the rows | closed below 0.86 m; any yaw from 1.01 m; ±35° at the tops | 1.0 − w = 0.574 at z 0.86 and 0.706 at z 1.007; W 0.70 gives ±35.3° | yes |
| F02 diagonal lane at 63.4° | 0.18 / 0.54 / 0.78 / 1.39 m; nose-first from 0.33 m, any yaw from 0.44 m | lattice (2, 0), (1, 2): line spacing 4/√5 = 1.789 m, less 1.342 w: 0.179, 0.541, 0.783, 1.386; z 0.327 and 0.437 | yes |
| F03 lower windows | 0.44–0.45 m clear, in a plane 35.3° from vertical | right isosceles window, legs 0.9 m: (L/2 − 2d)(2 − √2) = 0.450 at d 0.066 and 0.438 at d 0.076. The window plane is a cube face's, its normal at acos(1/√3) = 54.7° from vertical | yes |
| F03 under the node | 0.44 m | 0.52 − 0.076 = 0.444 m | yes |
| F03 pair at 2.0 m | worst 0.40 / 0.97 / 1.87 / 1.08 / 0.40 m; any yaw for a prop plane at 0.11–0.93 m; 0.18–0.86 m at 1.8 m centres | the gap is 2 × (0.2 + 1.4135 z) below the node, mirrored above: 0.40, 0.97, 1.87, 1.08, 0.40. The inscribed circle is 1.04 m: its centre is 0.540 m from the waist line, more than its radius. The ≥ 0.706 m band is 0.108–0.932 m (0.179–0.861 m at 1.8 m) | yes |
| F04 under the arm | 1.0 × 3.8 m, any yaw | 0.45 m over the 0.55 m envelope | yes |
| F04 entrance | 0.70 m clear between the bulges, ±35°; nominal 0.74–0.78 m | ±35.3°, 63 mm a side; 0.70 + 2 × (0.02–0.04) | yes (the physics reading's §5 correction is in) |

The worst-case pair columns take the worst orientation at each height separately: a foot and a tip cannot both point at the other hedgehog. That is conservative, and the verdicts stand.

## F01's turn model (checked)

```
$ python -I turns.py            # excerpt; section headers and the 36 ft yield rows left out
D 0.9 yield/turns     p 0.2679  lean 36.53 deg  end-on width 0.723  same-lean gap 2p cos 0.430  crossing line y 0.181
D 0.9 rounded p       p 0.2680  lean 36.55 deg  end-on width 0.723  same-lean gap 2p cos 0.431  crossing line y 0.181
D 0.9 loop spacing/2  p 0.2665  lean 36.31 deg  end-on width 0.725  same-lean gap 2p cos 0.429  crossing line y 0.181
   tape pi D 2.827 m; turns x loop spacing 29.85 vs 2 x yield 30.00
D 0.45 yield/turns     p 0.1964  lean 60.81 deg  end-on width 0.219  same-lean gap 2p cos 0.192  crossing line y 0.055
D 0.45 rounded p       p 0.1960  lean 60.59 deg  end-on width 0.221  same-lean gap 2p cos 0.193  crossing line y 0.055
D 0.45 loop spacing/2  p 0.2030  lean 64.45 deg  end-on width 0.194  same-lean gap 2p cos 0.175  crossing line y 0.049
   tape pi D 1.414 m; turns x loop spacing 22.74 vs 2 x yield 22.00
atan model: ellipse 0.45, 0.5237: perimeter 3.063 m vs pi D 2.827 (+8.3 %)
crossing: in-plane offset s 0.2250 = D/4 0.2250; z = +-0.390; angle 60.0 deg; next other-lean loop at 3p: s 0.675 vs R 0.45
clips per metre: 7.47 (0.90), 10.18 (0.45)
0.7 D: in-plane width 1.134 (1.26 D); end-on 0.63 x 0.999
0.8 D: in-plane width 1.063 (1.18 D); end-on 0.72 x 0.919
0.9 D: in-plane width 0.986 (1.10 D); end-on 0.81 x 0.827
0.90 stretched: a 0.165  silhouette 0.954  axis 0.942
0.45 fence: a 0.742  silhouette 0.938  axis 0.922
0.90 bunched: a 0.365  silhouette 0.882  axis 0.852
mat: a 0.399  across 1.41 0.755  through 0.15 0.970
I 0.90: one circle 44 mm; two circles r 0.357 c 0.083: ellipse->union 14 mm, union->ellipse 14 mm; best box 70 mm
I 0.45 (0.22): one circle 58 mm; two circles r 0.120 c 0.086: ellipse->union 21 mm, union->ellipse 27 mm; best box 26 mm
I 0.45 (0.19): one circle 64 mm; two circles r 0.110 c 0.092: ellipse->union 24 mm, union->ellipse 36 mm; best box 24 mm
sag 0.7: one circle 92 mm; two circles r 0.322 c 0.150: ellipse->union 31 mm, union->ellipse 31 mm; best box 68 mm
sag 0.8: one circle 49 mm; two circles r 0.355 c 0.088: ellipse->union 15 mm, union->ellipse 15 mm; best box 70 mm
sag 0.9: one circle 6 mm; two circles r 0.403 c 0.014: ellipse->union 2 mm, union->ellipse 3 mm; best box 71 mm
round 0.90: worst sagitta 3.85 mm vs half the 8 mm reach 4.0 mm
sag 0.7 loop: worst sagitta 4.76 mm vs half the 8 mm reach 4.0 mm
```

**The model holds as written:**
- **Tape:** each turn keeps π D (2.83 m; 158 m over 56 turns).
- **Lean:** λ = asin(2p/D), because a flat loop reaches D sin λ along the run.
- **Section:** end-on it is D high and D cos λ = √(D² − 4p²) wide.
- **Where turns meet:** neighbours of opposite lean meet on a vertical line at x = p/2, (D/4) cos λ off the axis, at ±(√3/2)(D/2), 60° from each loop's horizontal at any pitch. The next loop of the other lean would need s = 3D/4, outside the loop. So each turn has exactly 2 crossings per neighbour and 4 clip points.
- **Side view:** same-lean loops are 2p cos λ apart.
- **Sag:** an ellipse of perimeter π D reproduces the 1.26 / 1.18 / 1.10 D widths and the end-on sections.
- **Porosity:** reproduced to the third decimal.
- **The atan alternative:** needs 8.3 % more tape.

**The cut-or-hold threshold:** T_c = EI σ_f² / (4 (E r)² sin²(θ/4)) gives 0.034 N at 90° on the 0.69 GPa floor and 1.04 N at 3.8 GPa, matching the sheet's "0.034–1.0 N".

**Three details the script turned up,** all non-blocking (S2, S4, S5):
- the fence-top lean depends on which published figure fixes the pitch;
- one sentence about boxes is wrong for the fence-top coil;
- the 24-point rule misses its own bound on the most-sagged loop by 0.8 mm.

## OPSEC (the reviewer's read)

**Clear on the hard rules.** The sheet has:
- no reference file name, letter-specific detail, OSD value, person, plate or coordinate;
- no source about the pilot's country's present front, its places or its units.

The sources are:
- manufacturers' datasheets and catalogues;
- national and European section standards;
- encyclopedias;
- a historical bulletin and a historical pamphlet (public-domain manuals, design 1);
- a foreign heritage thesaurus.

Publication years of historical manuals are not dates of the references.

**Judged and accepted, not blocking:**
- The en and ru encyclopedia articles on the hedgehog mention historical and present-day uses: a city photo caption, and a railway operator making hedgehogs from track. The sheet takes only class facts from them, and D-032 allows encyclopedias.
- The heritage thesaurus is a county council's record, and one scope note names a historical army command (S7).

**Not clear: SEC-02 as amended by D-037.** It forbids "fortifications described tactically, … direction, extent or relation between positions", and names F01–F04. These lines break it (R1):

| Line | Wording | What it is |
|---|---|---|
| 86 | "tied … to a picket every 3.8 m on the home side" | threat-relative direction |
| 158 | "In front of trenches and firing positions …: a run parallel to the trench line, 30–100 m out, beyond hand-grenade throw and inside small-arms range" | direction, extent and relation between positions, and the tactical reason |
| 159 | "to tie the dragon's-teeth rows (F02) and the checkpoint (F04) into the ditches and belts so they cannot be bypassed" | tactical purpose |
| 163 | "along the defended line, pickets on the home side" | threat-relative direction |
| 186 | `picket_side` values `home, enemy` | threat-relative direction |
| 209, 605 | "the stand-off of protective wire (30–100 m)" | extent between positions |
| 287 | "parallel to the defended line, … so it cannot be driven round (assumed doctrine)" | direction and tactical purpose |
| 288 | "a field 20–100 m in front of the trench" | direction, extent and relation between positions |
| 290 | "Wire (F01) often runs in front of or among the rows" | direction |
| 466 | "(a breastwork to fire over standing)" | tactical use |
| 467, 481, 527 | "open at the rear" | threat-relative direction |
| 534 | "its open side away from the approach, against cover" | threat-relative direction and tactical siting |
| 535 | "so the kit cannot be driven round" | tactical purpose |

None of these figures is published (the sheet labels them assumed), so removing them costs no sourced fact. Class-level placement stays allowed, as the template asks: where an object occurs (along field and belt edges, beside trench lines, across roads at settlement edges, on fence and wall tops), how often, in what lengths, and with which other objects of a kit.

**Outside this branch (for the orchestrator):** earthworks-and-surfaces.md:278 (E04) has the same kind of wording on `unity/main`.

## Both readings, item by item

All 17 sent-back items are closed where the "Closed in 1.4" lines say:

| Reading item | Closed at | Checked |
|---|---|---|
| Physics 1: the snag records | :141–152, generator :173–174, open point :206 | `radius_m` 0.008 is the reach, `edge_radius_m` 0.00025, `barb_pitch_m` 0.035, `material` `razor_wire`; clips, tie wire and pickets are `steel` |
| Physics 2: cut or hold | :154, :205 | the formula, 0.034–1.0 N, the two cases, abrasion |
| Physics 3: collision and landing | :119–120, :155 | 60 N/m per part, about 240 N/m on four legs; 60 · (0.45/R)³, 480 N/m; the fiber ignores `razor_wire` shapes; sinks 0.20 m and 25 mm; the `steel` fallback sentence is gone |
| Physics 4: the lean | :49, :64, :178 | asin(2p/D); `loop_lean_deg` removed; `pitch_m` sets it |
| Physics 5: F02 collision | :266–268, :322 | one `convex` hull; `collision_boxes` removed |
| Physics 6: F03 landable | :401, :385 | 54.7° from vertical, above atan 0.35 |
| Physics 7: F04 sandbags | :515, :517, :529, :587 | `sandbag` at 900 N/m, ζ 0.9, edge 0.05 m; 13 and 28 mm; "rigid", "80 mm" and "Until …" are gone |
| Physics 8: F04 entrance | :468, :525, :559, :515 | the 0.70 m clear width is the gap's `narrowest_m`; nominal 0.74–0.78 m; one box per bag in every course near the entrance |
| Physics 9: the ids table | :590–597 | both accepted, §4's values, `razor_wire` band 40–110, labelled derived |
| Physics 10: open points | :599–606 | closed with pointers; the rest kept with owners |
| Buildability 1: the turn model | :48–51, :62–81, :85, :123, :141, :178 | one geometry, 2.83 m a turn, 2 clips a pair at the crossings, two capsules, no box; `loop_lean_deg` and `clips_per_turn` removed |
| Buildability 2: the box count | :267 | 0.268/n at the arrises; 14 boxes for ±2 cm and 6 for ±5 cm; a spall deeper than 2 cm |
| Buildability 3: sections | :54, :346–349, :182 | Р50 and Р65 in full, `angle_100`, `channel_100`, U picket 76 × 38 × 3 mm |
| Buildability 4: "arm" | F03 throughout, :412, :418, :425 | member and half-arm; `wire_holes` per half-arm (S6: the asset list still says "arms 1.8 m") |
| Buildability 5: the pair throat | :388 | in the vertical plane through both centres |
| Buildability 6: "from `damage`" | :19, every damage row | defined once; all 13 such rows give I and D values (gentable.py above) |
| Buildability 7: missing parameters | :195–197, :189, :577, :572 | `trampled_count`, `trampled_length_m`, `picket_lean_deg`, `support_width_m`, `cracked_blocks`, `post_bend_deg` |

**Not reflected:** the buildability review's ruling "Every turn is kept at every LOD" (F01, "Budgets"). Sheet line 202 still reads "LOD2 the coil as a sparse set of loop strips" (R3).

## The analyst's own flags

**1. Flat loops give 2 clips a pair; the catalogue and the encyclopedia give 3–7 a circle, staggered. Acceptable for the sheet.**
- The flat loop is the only model on the table that keeps the published tape length, diameter and loop spacing at once. The staggered 5-clip zigzag needs 3.89 m of tape a turn (buildability review), and the atan loop needs 3.06 m.
- The clips are 25 mm parts, drawn at LOD0 only.
- For the fiber, the props and the legs, the turn records' 8 mm reach already covers every clip.
- The porosity, the volume and the "never passable" verdict do not depend on the clip count.
- What is not acceptable is the sentence "the published clips beyond two a pair have nowhere to sit". They sit on the real coil, whose loops kink at them. The sheet should call this a simplification and keep it as a G2 look point (S1).

**2. 60.6° comes from the rounded pitch. Immaterial.**
- The exact yield/turns pitch gives 60.8°, the published 36 ft yield 60.6°, and the published 406 mm loop spacing 64.4°.
- The fence-top coil's end-on width is 0.19–0.22 m across those figures.
- The fence-top coil has no gap and is not landable, and its two-capsule volume holds ±5 cm at either width (21–36 mm).
- The generator derives the lean from `pitch_m`, so the printed figure is only a description (S2).

**3. A weak source for two Р50 numbers.**
- The head depth (42 mm) comes from a rail maker's table whose column letters are unlabelled.
- The foot root (27 mm) is derived from it.
- Both are corroborated by the area check: the sheet's outline gives 66.4 cm², and my own integration of the same outline gives 65.9 cm², against the standard's 65.99. Accept (S3).
- The weaker label is a different one: Р50 itself as the hedgehog's rail, which neither encyclopedia states (R2 d).

**4. "LOD2 sparse loop strips" against the buildability review's every turn at every LOD. The review is right.**
- A sparse LOD2 thins the haze the feed shows at 16–28 m.
- Making up for it would mean thickening the remaining strips, which the sheet's own line 21 forbids.
- At 36 triangles a turn nothing needs dropping (R3).

## Requirements

| ID | Status | Note |
|---|---|---|
| AST-01 | met (sheet stage) | F01–F04 classes, variants and states defined; the build is later |
| AST-02 | met | an object sheet with sources, dimensions, construction, colours, damage and physical role |
| AST-04 | partly met | dimensions inside the published ranges and labelled; five published labels are wrong (R2) |
| AST-05 | met | collision in primitives (with the `convex` kind ruled and owned), ids, gaps against the drone, wind volumes with porosity, snags, landable surfaces |
| AST-06 | met | every D state is physical and parameter-driven |
| SEC-01 | met | nothing from `reference/`; the diff carries no media |
| SEC-02 | partly met | clips by letter (none) and no places; but the wording is tactical (R1) |
| WLD-01 | partly met | the placement rules feed phase 3, but must be reworded (R1) |

## Required changes

- **R1 (reference-analyst, OPSEC, SEC-02 as amended by D-037).** Reword the 16 lines in the OPSEC table to class-level, neutral placement. Keep where each object occurs, how often, its lengths and its kit neighbours. Drop:
  - the threat-relative directions ("home side", "enemy", "defended line", "in front of", "rear", "away from the approach");
  - the stand-offs between positions (30–100 m, 20–100 m), which the layout sets;
  - the tactical reasons ("beyond hand-grenade throw", "inside small-arms range", "to fire over", "so it cannot be bypassed / driven round", "against cover").

  For example:
  - `picket_side` becomes `left, right, inside, alternate`, relative to the run's drawing direction;
  - "open at the rear" becomes "open on one side";
  - line 158 becomes "along trench lines (E01–E04), field and belt edges, and across road shoulders and verges".
- **R2 (reference-analyst, labels).** Make each published label say what its source says:
  - **(a)** :46 and :49: the 0.6 m coil yields 11 m at a 0.406 m loop spacing; 15 m and 0.533 m start at 0.72 m (0.72 m has both options). Fix the ranges and the `coil_diameter_m` effect (:176) that derives the yield default from them. Built from the sheet as written, a 0.6 m coil leans 63° and stands 0.27 m wide; from the source, 41° and 0.45 m.
  - **(b)** :45: drop "(56 in every concertina row)"; the military BTC 900 mm row has 50 loops at a 0.600 m loop spacing.
  - **(c)** :86: the encyclopedia gives 3 to 7 rivets joining the turns in a staggered pattern, not "three staples" for a self-supporting coil. Cite it for the clips (it backs the catalogue's 3–7) and label the ground staples assumed.
  - **(d)** :345: "Р50" as the hedgehog's rail is assumed. The encyclopedias give rail generically, and a rail seller cannot say what hedgehogs are made of. The section figures in :346–347 stay published.
  - **(e)** :477 and the `arm_length_m` effect (:565): the cited manufacturer requires the swinging support from 4.0 m and adds the stabilising rope from 5.5 m. The datasheet gives neither threshold.
- **R3 (reference-analyst, buildability).** :202: replace "LOD2 the coil as a sparse set of loop strips" with the buildability review's plan: every turn at every LOD (LOD1 a 4-sided tube on 12 points a turn, LOD2 3-sided on 8, LOD3 3-sided on 6).

## Suggestions (not blocking)

- **S1** (:51, :85). Call the flat loop a simplification of the published 3–7 staggered clips. Add a G2 look point to F01's open points: whether the coil reads right with 4 clip points a turn. A `clips_per_pair` parameter can follow if it does not.
- **S2** (:49, :64, :141). Print 36.5° (15/56 m) rather than 36.6°. For the fence-top coil, give "about 61° (60.6–64.4° over the published yield and loop spacing), end-on 0.19–0.22 m wide".
- **S3** (:346). Say that the head depth is read from the maker's table and confirmed by the area check, so a later reader knows what holds it.
- **S4** (:80). "A box misses every one of these sections by at least 70 mm" is false for the 0.45 m fence-top coil: the best box is within 24–26 mm. At 0.7 D it is 68 mm. Two capsules still fit better (21 mm), so only the sentence changes. The capsules' outline also stands up to 27 mm (0.22 m width) and 36 mm (0.19 m) off the ellipse on the fence-top coil. That is inside ±5 cm; quote the two-way figure.
- **S5** (:141). The sagitta rule is derived for round loops. On the 0.7 D sagged ellipse, 24 points spaced by angle give 4.8 mm, over the 4.0 mm bound. Sample by arc length, or apply the rule at the tightest curvature.
- **S6** (asset-list.md:203). The F03 Key size says "arms 1.8 m"; the sheet now calls that bar a member.
- **S7** (:217). The heritage-thesaurus terms are fine as a class source, but a page without a county and a historical command in it would be cleaner. The two proportions it supplies only bound a derived default.
- **S8** Three smaller gaps:
  - Nothing drives the fence-top festoon: no count, and the drop is only stated in the D text. Add it, or say that `damage` and `seed` place it.
  - F04's Physics role does not list the spill cones' collision. The buildability review gives it: 2–3 stacked `surface:crater_spoil` cylinders.
  - F02's and F03's Outputs lines differ from the buildability review's LOD tables. The "box impostor row" is the world's merged LOD of a field, not the tooth's.
- **S9** (the base spec, for the orchestrator at archive). `object-sheets` "Buildable" still says "WHEN the six sheets are done". Workflow lesson L13 would carry it in a MODIFIED delta.
- **S10** (the physics reading's N-4). The two phase-7 tether scenarios (draped run: no cut above 1.35 GPa over 20 seeds; reversal round a barb: a cut logged with mechanism `bend` and the record index) go into QA's phase-7 test plan.
