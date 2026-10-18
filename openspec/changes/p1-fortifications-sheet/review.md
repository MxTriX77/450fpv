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

## Closed in 1.5

The reference analyst closed each item in `docs/object-sheets/fortifications.md` (branch `unity/ref/p1-fortifications-sheet--qa`; line numbers after the edit):

- **R1** → every line of the OPSEC table reworded to what an object stands on or beside, how it is laid out and how many: F01 :86, :158–159, :162–163, `picket_side` :186 (`left, right, inside, alternate` against the run's drawing direction, default `left`), open point :209; F02 :288–289, :291; F04 :467–468, :482, :528, :535–536; across the sheet :611. The 30–100 m and 20–100 m figures and every tactical reason are gone. Same kind, found on the way: :162 ("around positions"), :261 (the buried D state's reason) and :341 ("a target of"). `git grep` for R1's words finds nothing.
- **R2 (a)** → the 0.6 m coil at 11 m and a 0.406 m loop spacing, 15 m and 0.533 m from 0.72 m (which also comes at 11 m): Dimensions :46, :47, :49, :50; the turn model items 2 and 3 (:64–65: p 0.196 m, lean 40.9°, end-on 0.60 × 0.45 m; 42.6° and 0.44 m at the loop spacing's p 0.203 m); `coil_diameter_m` :176.
- **R2 (b)** → :45: "56 in every concertina row" dropped; 31–56 over the catalogue's clipped concertina coils (31, 50, 51 or 56 by product; the military BTC row at 0.90 m 50).
- **R2 (c)** → :51 and :85: the encyclopedia cited for 3 to 7 rivets joining the turns in a staggered pattern; :86: the ground stakes published (the encyclopedia's spirals fixed to the ground with stakes), the staples assumed; "three staples" gone.
- **R2 (d)** → :346: rail as a member published, Р50 and Р65 assumed (the encyclopedias name no type); the sections at :347–348 stay published. `docs/asset-list.md` §12 F03: "Р50 rail (assumed)".
- **R2 (e)** → :478 and `arm_length_m` :566: from 4.0 m a support at the arm's far end, swinging or fixed (the manufacturer's page lists the swinging support as required from 4.0 m; its brochure, added at :447, allows a fixed support instead), from 5.5 m the stabilising rope. The default 4.0 m arm keeps its fork end rest, and `check` refuses `end_rest` none from 4.0 m.
- **R3** → :202: every turn at every LOD, with the buildability review's LOD1–LOD3 (4-sided on 12 points, 3-sided on 8, 3-sided on 6).
- **S1** → :51 and :85 call the flat loops a simplification of the 3–7 staggered clips; F01 open point :210, a G2 look point with `clips_per_pair` to follow if needed.
- **S4** → :80: the best box misses the 0.90 m coil's sections by 67–71 mm and the fence-top coil's by 24–26 mm, as close as its two capsules both ways; :75 quotes the two-way fit (21 / 27 mm; 24 / 36 mm at 0.19 m wide).
- **S6** → `docs/asset-list.md` §12 F03 Key size: "members 1.8 m".
- **S7** → :218: the county record replaced by the national monument-type thesaurus (heritagedata.org), which carries no county. The tetrahedron term's scope note there still names a historical command; it stays because no other source found gives the triangular class's proportion, which bounds the base default.
- **S5, S8** → recorded for the asset change with their owner (asset-artist; physics sign-off on S5) at :606–610.

## Re-check after 1.5

**Reviewer:** qa-engineer, a fresh agent that did none of tasks 1.1–1.5 (D-034) · **Branch:** `unity/ref/p1-fortifications-sheet` at `27723f6` · Line numbers below are at `27723f6`. I read `CLAUDE.md`, `AGENTS.md`, SEC-02 and D-037 as they stand on `origin/unity/main`, this review, `tasks.md`, the whole sheet and the asset list's §12. I did not open `reference/`. Every source below was opened by me; the scripts are throwaway files in my scratchpad, not committed.

**Verdict: APPROVED**, with the five fix-at-merge lines at the end applied by the orchestrator before the merge. R1–R3 are closed; nothing left blocks. The fix-at-merge lines are exact wording: one SEC-02 phrase the rework left in F03 and the asset list, two labels that still say a little more than their source, and two record slips.

### Checks run

```
$ git log --format='%h|%an|%ae|%s|body=[%b]|trailers=[%(trailers)]' c2a0290..HEAD
27723f6|Reference Analyst|reference@450fpv.local|docs(openspec): record qa closures for fortifications and tick 1.5|body=[]|trailers=[]
b43efaf|Reference Analyst|reference@450fpv.local|docs(ref): name hedgehog members and label р50 assumed in asset list|body=[]|trailers=[]
32b4d58|Reference Analyst|reference@450fpv.local|docs(ref): close qa's r1-r3 and line edits in the fortifications sheet|body=[]|trailers=[]
(3 commits, subject only, no trailers, the role's identity from docs/team.md)

$ git diff --name-only c2a0290 HEAD
docs/asset-list.md
docs/object-sheets/fortifications.md
openspec/changes/p1-fortifications-sheet/review.md
openspec/changes/p1-fortifications-sheet/tasks.md
(nothing outside the sheet, the asset list and the change folder; no media, nothing from reference/, Library/, Temp/ or Logs/)

$ git diff --name-only $(git merge-base HEAD origin/unity/main) origin/unity/main -- docs/object-sheets/fortifications.md docs/asset-list.md openspec/changes/p1-fortifications-sheet
(no output: the 17 commits on unity/main since c570d68 touch none of these files)

$ git diff c2a0290 HEAD -- docs/object-sheets/fortifications.md docs/asset-list.md | grep -E '^[-+]#{2,3} |^[-+]\| F0'
(only the F03 row of the asset list; no heading added or removed, so coverage stays 107 of 107)

$ python tools/hygiene/precommit.py --tree HEAD
520 files scanned, 0 findings                                   exit=0

$ openspec validate p1-fortifications-sheet --strict
Change 'p1-fortifications-sheet' is valid                       exit=0
```

### 1. R1: wording under SEC-02 (D-037)

**Every line of the OPSEC table is now neutral and class-level:**

| Was | Now | Reads |
|---|---|---|
| 86 "on the home side" | :86 "every 3.8 m on one side of the run (`picket_side`)" | neutral |
| 158 "In front of trenches …, 30–100 m out, beyond hand-grenade throw …" | :158 "Along trench lines … and along field and belt edges: a run beside the line, following it" | neutral |
| 159 "… so they cannot be bypassed" | :159 "Across road shoulders and verges, from the road edge to the ditch or the belt edge, often meeting the end of" F02 or F04 | kit neighbours only |
| 163 "along the defended line, pickets on the home side" | :163 "along the trench line, edge or fence it follows, or across the verge" | neutral |
| 186 `home, enemy` | :186 `left, right, inside, alternate` against the layout polyline's order, default `left` | neutral |
| 209, 605 stand-off 30–100 m | :209, :611 "Where each run lies is the layout's" | neutral |
| 287 "… so it cannot be driven round" | :288 across roads at settlement edges and across gaps between belts, its ends meeting a belt, ditch, trench line or wire run | neutral |
| 288 "20–100 m in front of the trench" | :289 "a field beside the trench or the bank, its rows parallel to it" | neutral |
| 290 "in front of or among the rows" | :291 "beside or among the rows" | neutral |
| 466 "(a breastwork to fire over standing)" | :467 "assumed" | neutral |
| 467, 481, 527 "open at the rear" | :468, :482, :528 "open on one side" | neutral |
| 534 "away from the approach, against cover" | :535 "on the verge beside it, next to a building corner, a tree or a belt edge where one stands" | what it stands beside |
| 535 "so the kit cannot be driven round" | :536 "wire (F01) along both verges, from the road edge to the ditch or the belt edge" | neutral |

The analyst's three extra finds are gone too (:162 "around positions", :261 the crossing reason, :341 "a target of").

**The R1 grep finds 19 lines, not none as "Closed in 1.5" says.** I read each one:
- **Acceptable:**
  - "position" as a place on a part: picket position :87, positions along the coil :149 and :195, `position_jitter_m` :307, hole and loop positions :353, :428, :458.
  - "sandbag position(s)": :440, :468, :471, :482, :503, :526, :528, :535, :556, :563, :585. This is the asset list's own name for the F04 piece (D-035), and the sheet describes it only by construction: plan, inside size, courses, entrance and ports.
- **Reword:** :405 "road approaches" (flag a below; fix at merge 1).

**A wider scan,** for front, attack, tank, firing, fire, cover, fragment, bullet, military, defence, obstacle, army, command, doctrine, stand-off and "cannot be", read hit by hit. All acceptable:
- **:341, "about 1 m, the height of a tank's front plate":** the ru encyclopedia's published design height for the class. The sheet drops the source's "enemy". It states no direction, extent or relation.
- **:358, a pushing vehicle "rolls it under its belly":** the published physical mechanism, and the reason the sim leaves it unanchored.
- **Damage causes:** :108 and :115 (a fragment cuts the core), :162 and :292 (a shelled area), :258 (the blast side).
- **:537, "the frontline town":** WLD-01 and WLD-05's own term for the settlement class.
- **:482, "most modern military bags":** a material fact.
- **:217, the bulletin's title:** a source title, not wording.

**The analyst's open flags:**
- **a. "road approaches", F03 :405 and `docs/asset-list.md:203` (F03's description): reword.**
  - "Approach" ties the object to a settlement that a road leads to: a relation, and in military usage a direction.
  - The rework already changed the same phrase at F02 (:288, "road approaches to the settlements" → "across roads at settlement edges"), so F03 and the asset list should match it.
  - The asset-list wording is the orchestrator's from D-035, not the pilot's quote (:197 does not contain it).
  - Neutral wording: "on roads at settlement edges and at the checkpoint" (fix at merge 1).
- **b. `firing_ports` (:470, :482, :516, :527, :561): acceptable.**
  - Each port is a physical opening: a 0.20 × 0.12 m slot left by a missing bag, a gap polygon with a "no" verdict, and the reason one box per bag is laid within 0.3 m of it.
  - REAL-05 requires the sim to know it.
  - SEC-02 bars firing positions *described tactically*. The construction term with its size says nothing of siting, direction, extent or use against anyone.
  - The project uses "firing positions" as a neutral class noun itself (asset list :206).
- **c. "20 in (0.51 m) of sand in bags is proof against armour-piercing bullets and fragments" (:466, source line :445): acceptable.**
  - It is a published physical property of a thickness of fill, and the anchor of the wall-thickness range.
  - It is class-level and says nothing of siting or use.
- **d. The national thesaurus note at :218: acceptable.**
  - Opened: FISH Thesaurus of Monument Types on heritagedata.org, "Anti Tank Pimple" (160305) and "Anti Tank Tetrahedron" (160309). No county on either page; the spatial coverage is the country.
  - The tetrahedron's scope note names a historical army command of that country in the 1940s (I leave its name out of this file).
  - The sheet does not quote it, and it identifies nothing of the pilot's places, units or dates.
  - It is the only source found for the triangular class's proportion, which bounds a derived default. D-032 admits such sources.

### 2. R2: the five figures against their sources

| Item | Sheet now | Source opened | Result |
|---|---|---|---|
| (a) the 0.6 m coil | :46 11 m (0.45 and 0.6 m), 15 m from 0.72 m, the 0.72 m also at 11 m; :49 0.406 m (0.45 and 0.6 m; 0.72 m also), 0.533 m (0.72–1.0 m); :176 the yield default | NATO-22 flyer, p. 2 table: 600 mm, 56 loops, 3 clips, 16 in / 406 mm, 36 ft / 11 m; 720 mm, 3 (5) clips, 533 (406) mm, 50 (36) ft, 15 (11) m; 750–1000 mm 533 mm, 15 m. The catalogue's selector repeats the NATO-22 rows | **match** |
| (a) derived | :47 p 0.196 m; :49 and :64 lean 40.9°, 42.6° at p 0.203 m; :50 and :65 0.60 × 0.45 m, 0.44 m at p 0.203 m | my recomputation (below) | **match** |
| (b) the turns | :45 "31–56 over the catalogue's clipped concertina coils of 0.45–1.0 m (31, 50, 51 or 56 by product: every NATO-22 row 56, the military BTC row at 0.90 m 50)" | catalogue, "Military Tactical Barrier Concertina Coil": 900 mm, **50** loops, 5 clips, 24 in / 600 mm, 50 ft / 15 m. Selector: Maze 31, military BTC 56/50, NATO-22 56, Supermaze and NATO Barrier **51 (81)**, Detainer Hook 31 (51) | the military row and the 56 **match**; the list leaves out the selector's 81-turn option of two dense maximum-security coils at 0.75–1.0 m. The default and the generator's 31–56 stand, but the label should say the 81 is left out (fix at merge 2) |
| (c) the clips | :51, :85 "3 to 7 rivets joining the turns in a staggered pattern"; :86 stakes published, staples assumed | ru encyclopedia (Спираль Бруно): "заклёпки от 3 до 7 штук для соединения витков … в шахматном порядке"; "связываются между собой проволокой и крепятся кольями к земле"; no staples on the page | **match** |
| (d) the rail type | :346 rail as a member published, Р50 and Р65 assumed; :347–348 sections published; `docs/asset-list.md:203` "Р50 rail (assumed)" | ru encyclopedia: rails named only generically ("швеллера, уголка, рельса"; I-beams No. 25–40); en encyclopedia: angle L 140/140/13, rail only as "new tracks" in recent use; neither names Р50, Р65 or any rail type | **match** |
| (e) the barrier support | :478 from 4.0 m a support at the far end, swinging or fixed; from 5.5 m the rope; :566 the same, `check` refuses `end_rest` none from 4.0 m | the manufacturer's page: pendulum support "optional" for 2.5–3.5 m, "required" from the 4.0 m model; stabilisation rope "included for 5,500mm boom length and longer". The brochure: from 4,000 mm effective boom length "a swinging support or a fixed support is mandatory"; the rope "From 5,500mm effective boom length included in delivery" | **match** |

**The 0.6 m coil, recomputed:**
- p = 11 / 56 = 0.1964 m.
- λ = asin(2p / D) = asin(0.6548) = **40.90°**.
- End-on width √(D² − 4p²) = √(0.36 − 0.1543) = **0.4535 m**.
- At the loop spacing's p = 0.203 m: 42.58° and 0.4418 m.
- The `check` limit 0.47 D = 0.282 m is far above both.
- 56 × 0.406 = 22.7 m against 2 × 11 = 22 m, as at 0.45 m.

```
$ python -I (inline)
0.6 yield/turns        p 0.1964  lean 40.90 deg  end-on 0.60 x 0.4535  2p/D 0.6548  check 0.47D 0.282
0.6 loop spacing/2     p 0.2030  lean 42.58 deg  end-on 0.60 x 0.4418  2p/D 0.6767  check 0.47D 0.282
0.9                    p 0.2679  lean 36.53 deg  end-on 0.90 x 0.7232  2p/D 0.5952  check 0.47D 0.423
0.45                   p 0.1964  lean 60.81 deg  end-on 0.45 x 0.2195  2p/D 0.8730  check 0.47D 0.211
0.6 m at the old 15 m / 0.533 m: p 0.2679 lean 63.2 width 0.270
```

### 3. R3: every turn at every LOD

**Closed.** :202 reads "**every turn at every LOD**":
- LOD1: a 4-sided tube on 12 points a turn from about 7.5 m.
- LOD2: 3-sided on 8 points from about 16 m.
- LOD3: 3-sided on 6 points from about 28 m to the cull.

This is the buildability review's Budgets table (its lines 112–123) row for row. The grep for "sparse", "loop strips" and dropped turns finds only F02's "sparse array" (:283, the teeth field's plan, unrelated).

### 4. S1, S4, S5–S8 and the barrier `check`

- **S1:** closed.
  - :51 and :85 call the flat loops a simplification of the 3–7 staggered clips.
  - :210 is the G2 look point, with `clips_per_pair` to follow (asset-artist).
- **S4:** closed.
  - :80 says the best box misses the 0.90 m coil's sections by 67–71 mm and comes within 24–26 mm of the fence-top coil's.
  - :75 gives the capsules' two-way fit, 21 / 27 mm (24 / 36 mm at 0.19 m).
  - My own two-way best-box search (1.5° sampling, half-sizes in 0.4 % steps) gives 69 / 69 / 70 / 71 mm on the I and sagged 0.90 m sections, and 28 / 26 mm on the fence-top coil.
  - Both claims hold either way: over ±5 cm on the 0.90 m coil, inside it on the fence-top coil. The 2–3 mm differences come from the fitting method.

    ```
    I 0.90     best box 0.584 x 0.763: worst 69.4 mm (ellipse->box 69.4, box->ellipse 69.4)
    sag 0.7    best box 0.864 x 0.504: worst 69.1 mm (ellipse->box 68.0, box->ellipse 69.1)
    sag 0.8    best box 0.780 x 0.582: worst 69.9 mm (ellipse->box 69.9, box->ellipse 69.9)
    sag 0.9    best box 0.691 x 0.671: worst 71.3 mm (ellipse->box 69.7, box->ellipse 71.3)
    fence 0.22 best box 0.176 x 0.394: worst 27.9 mm (ellipse->box 27.9, box->ellipse 27.9)
    fence 0.19 best box 0.155 x 0.398: worst 26.4 mm (ellipse->box 26.1, box->ellipse 26.4)
    ```
- **S6:** closed. `docs/asset-list.md:203` reads "members 1.8 m (published)". The same row's Physics cell still says "the spaces between arms" (fix at merge 5).
- **S7:** closed. The county record is replaced by the national thesaurus (1d above).
- **S5 and S8:** recorded as open points for the asset change at :606–610. Owner asset-artist, physics sign-off on S5. Each item is stated with its fix: arc-length sampling; a festoon parameter or the `damage`/`seed` rule; the spill cones' `surface:crater_spoil` cylinders; the LOD lines.
- **The barrier `check`:** consistent.
  - `arm_length_m` 3.0–8.0, default 4.0 (:566); `end_rest` fork, lockable or none, default fork (:571).
  - The default arm therefore passes with its fork, the source's fixed support.
  - Arms of 3.0–3.9 m may have none, which the manufacturer makes optional below 4.0 m.
  - :477 (a fork post at the far end) and :478 agree.
- **Not in 1.5's scope, still open as suggestions:** S2 (36.6° printed; 36.5° exact), S3 (the head depth's provenance at :347), S9 and S10 (for the orchestrator and the phase-7 test plan).

### 5. The analyst's suggestion: F02 :290

**Relabel.**
- The ru encyclopedia's "оставляя местами коленчатые выходы" is in its paragraph on the old rows of wooden stakes, not on concrete teeth. So "published" at :290 transfers a fact across classes.
- "Exit" also names a way out of a position, which SEC-02 would rather not say.
- The same page's earth pushed over the teeth (the buried D state, :261) is about concrete rows, so that label stands.

Fix at merge 3 relabels :290 as assumed and drops "exit". The source line :219 follows it.

### Requirements, after the re-check

| ID | Status | Note |
|---|---|---|
| AST-04 | met, with fix at merge 2 and 3 | all five R2 figures match their sources; two labels narrowed |
| SEC-02 | met, with fix at merge 1 | every R1 line neutral; "road approaches" the last relational phrase |
| WLD-01 | met | placement now says only what each object stands on and beside, how it is laid out and how many |
| AST-01, AST-02, AST-05, AST-06, SEC-01 | met | unchanged by 1.5 |

### Fix at merge (orchestrator, before `gh pr merge --merge`)

1. **SEC-02, "road approaches".**
   - `docs/object-sheets/fortifications.md:405`: replace "as **singles and pairs at road approaches and at the checkpoint** (asset list)" with "as **singles and pairs on roads at settlement edges and at the checkpoint** (asset list)".
   - `docs/asset-list.md:203`: replace "singles and pairs at road approaches and checkpoints" with "singles and pairs on roads at settlement edges and at checkpoints".
2. **R2 (b), the turns.** `docs/object-sheets/fortifications.md:45`:
   - In the Range cell, replace "the military BTC row at 0.90 m 50)" with "the military BTC row at 0.90 m 50; the selector's 81-turn option of two denser maximum-security coils left out)".
   - In the Label cell, replace "published, catalogue" with "published, catalogue; leaving out the 81-turn option assumed".
3. **The dog-leg label.**
   - `docs/object-sheets/fortifications.md:290`: replace "a lane left open as a dog-leg exit (published: gaps left as dog-leg exits)," with "a lane left open through the rows as a dog-leg (assumed; the encyclopedia's dog-leg gaps are in the older rows of wooden stakes),".
   - `docs/object-sheets/fortifications.md:219`: replace "rows in a set order, gaps left as dog-leg exits (encyclopedia)" with "rows in a set order, and dog-leg gaps in the older rows of wooden stakes (encyclopedia)".
4. **The record.** In this file's "Closed in 1.5" first paragraph, replace "branch `unity/ref/p1-fortifications-sheet--qa`" with "branch `unity/ref/p1-fortifications-sheet`". No `--qa` branch exists on origin; the three commits are on this branch.
5. **S6's rename, followed through.** `docs/asset-list.md:203`, the Physics cell: replace "the spaces between arms as gaps" with "the spaces between half-arms as gaps".
