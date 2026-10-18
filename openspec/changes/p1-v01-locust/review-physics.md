# Physics review: the shell piece cap for crowns and sparse cover

**Reviewer:** physics-engineer · **Task:** 1.0 · **Branch:** `unity/art/p1-v01-locust` · **From tip:** `be50a95`
**Against:** AST-05, AST-09, REAL-05, PHY-05, PHY-18, PHY-19, WLD-11; the archived `p1-v13-shrub` review-physics (§2 the 0.1 m tolerance, §3 pieces and the 24 cap, §4 records, N3, N4, N6, N7) and QA's re-review there (N-1, N-2, the shell as built); this change's design 4, 7 and 8 and its two deltas; the vegetation sheet's group rules and §V01
**Read:** `CLAUDE.md`, `docs/workflow.md`, `AGENTS.md`; this change's proposal, design, tasks and deltas; the archived V13 change's `review-physics.md`, `review.md` and design decision 7 "as built"; `openspec/specs/asset-pipeline/spec.md` ("Compliant shells in the collision audit", the metadata requirement) and `openspec/specs/assets-vegetation/spec.md`; `docs/object-sheets/vegetation.md` (group rules, §V01); `tools/blender/lib/{shell,audit,skeleton}.py`, `tools/blender/generators/shrub.py`; `assets-src/physics_materials.json` (`twig`, `bark`, `foliage`); knowledge §5–§7 and §10; `docs/legacy/map-and-world/surfaces-review.md` §6 and `api-review.md`

I consume these numbers: the 1 kHz contact law (4 legs, 4 arms, 4 prop discs, body and coil against shell pieces and capsules), the fiber's catch, bend and cut-risk model (PHY-18, PHY-19), prop strikes (PHY-05), the wind grid and the log. Every number is labelled **pilot / measured / published / derived / assumed**. "Measured" means measured on V13 rebuilt in plain Python from the committed params with the committed fitter (`lib/shell.py`) and audit (`lib/audit.py`), at the `leaf_cover` and piece cap named. The rebuild at `leaf_cover` 1.0 and cap 24 reproduces the committed `V13_tall` shell (24 pieces, 99.7 mm and 282.6 mm outside), and at 0.4 it reproduces QA's N-1 (227.4 mm and 341.5 mm). The scripts lived in a scratch folder, now deleted; §9 has their output. No Blender, no Unity.

## Verdict

1. **The cap is per neighbourhood, not per clump or per tree** (§2).
   - The step tests the drone's parts only against the pieces around the drone. A per-tree total costs nothing at step time once a broadphase removes everything outside the drone's neighbourhood, and V01 needs that broadphase anyway for its 150–300 capsules per tree.
   - **At most 64 shell pieces of one asset whose boxes meet any 2 m cube** (derived). The build audits it as a new rule (d).
   - **At most 96 summed over all objects** in any 2 m cube (derived): the phase-3 compiler checks it.
   - **Per fit call** (a V13 clump, a V01 main limb): the fewest of 24, 32, …, 64 pieces that pass. A V01 limb that 64 cannot pass is split along the limb. **Per tree:** no cap of its own; rule (d) binds where limbs meet.
   - The timings behind it are the V13 reading's assumptions. The phase-3 worldbench case `veg_shells` (§2.5) confirms them.
2. **Sparse cover keeps the three rules and the fitter; it takes more pieces** (§3).
   - The twigs do not change with `leaf_cover`, so the shell stands for the same twig crowd.
   - `V13_tall` needs **40 pieces at 0.4** and **60 at 0.3** (measured; the full audit passes both).
   - **At 0.2 the fitter plateaus** at 63 pieces with a twig vertex 119 mm out. So `V13_tall_autumn` keeps `leaf_cover` ≥ 0.3; the design's 0.4 is inside that.
   - Not adopted, each measured or argued in §3.3: a looser card allowance, capsules along twig groups, twigs left to their records, rule (b) on faces.
3. **N-2: a record takes the material of the member it follows.** The generator writes it on the record (`material`): `twig` for a twig, `bark` for a stem or branch. The V13 reading's "else of the shell that encloses it" is withdrawn: whether a record lies inside a shell never changes its material (§4).
4. **What V01's crowns change** (§5):
   - the card allowance follows the card: 0.35 m for V01's 0.25 m leaf
   - twig tubes carry a ring at every kink, at most 0.1 m apart
   - gaps are bounded by capsules and shell pieces, and no piece crosses a gap
   - one crown wind box sits above the trunk column
   - shells are tested statically, from a binned cache
   - the fiber ignores V01 shells, because records carry every twig
5. **Found on the way:** QA's N-2 counted twig end points only. Rule (b) holds vertices, and V13's straight twigs have rings only at their ends, so along their axes the committed V13 twigs stand up to **238 mm** outside the shell (measured).
   - V13 is not changed for it: holding (b) on faces costs 40 pieces at summer and buys no force accuracy (§3.3).
   - V01's kink rings close the gap. N-a carries the force-faithful fix to phase 7.

## 1. What this reading starts from

- **The V13 cap.** 24 pieces per variant came from a linear scan: 3 clumps in reach × 14 parts × (5 ns × K box rejects + 3 × 30 ns narrow tests) = 8.8 µs at K = 24, against a shells' share of about 8.3 µs. That share is assumed: a third of the 25 µs left of the 60 µs world-query budget after the 34.7 µs measured in the first attempt.
- **N-1, reproduced.** At `leaf_cover` 0.4 and cap 24, `V13_tall` leaves a twig vertex 227.4 mm and a leaf vertex 341.5 mm outside. `V13_mid` (21 pieces) and `V13_low` (9) pass.
- **N-2, extended.** The twig records with a point outside every piece number 58 / 83 / 162 (low / mid / tall) when only their end points are counted, which reproduces QA's numbers. Along their axes at 1 cm, the numbers are:

  | | Records with an axis point outside | Farthest outside | Records beyond 0.1 m (both ends within 0.1 m) | Twig axis beyond 0.1 m |
  |---|---|---|---|---|
  | low | 64 of 185 | 100.3 mm | 1 | 0.01 of 46.9 m |
  | mid | 93 of 155 | 148.6 mm | 3 | 0.18 of 52.5 m |
  | tall | 179 of 267 | 238.2 mm | 32 | 4.63 of 116.8 m |

  The twig tubes are 3-sided with rings at their two ends only (`lib/skeleton.py`). A straight twig 0.3–0.9 m long can span a concavity between two pieces with both ends inside the tolerance (§3.3, §5).

## 2. The cap

### 2.1 What the narrow phase sees

- **The consumer.** Each drone part tests the `twig` pieces near it. The contact law takes the deepest piece per (object, part) (V13 reading §3). The fiber does not read V13 or V01 shells: every twig it can catch is a record (§5).
- **Shells need no sweep** (derived).
  - The swept test and the Time < 1 latch exist so that a part cannot pass through a thin *rigid* shape within a step.
  - At cruise (60–80 km/h, published) a part moves at most 22 mm per 1 ms step, and 40 mm at twice that speed. The smallest piece is 80 mm across (the fitter's 40 mm minimum radius), so no part crosses a piece in one step.
  - At 30 N/m the force changes by 0.67 N per step at cruise and 1.2 N at 40 m/s.
  - So a shell test is a static depth at the current pose: a closed-form point–segment or segment–segment distance.
- **The neighbourhood is a 2 m cube** (derived). Shells are served like micro-detail and the records (V13 reading N7): prefetched on a worker at ≤ 40 Hz (knowledge §10.5) into a local list around the drone.
  - The drone spans 0.706 m tip to tip (derived, knowledge §5.4).
  - It moves about 0.5 m between refreshes at cruise (20–22 m/s at 40 Hz).
  - 0.706 + 2 × 0.55 = 1.8 m, so a 2 m cube centred on the drone holds every piece a part can touch before the next refresh.

### 2.2 Cost and measured density

Assumed timings, as in the V13 reading: 5 ns per box reject and 30 ns per narrow test. A part's bins in a 0.25 m local grid cost 50 ns (assumed). The shells' share is 8.3 µs per step (assumed).
- **N** is the largest number of an asset's pieces whose boxes meet a 2 m cube (measured over cube centres on a 0.1 m grid).
- **T** is the worst step's narrow tests: the sum over the 14 parts of the pieces whose boxes meet the part's swept box (+30 mm on every side), over drone positions on a 0.1 m grid through the crown, at the assumed part layout of knowledge §5.2.
- **Unbinned:** every cached piece is box-tested by every part, 14·N·5 ns + T·30 ns. **Binned:** T·30 ns + 14·50 ns.

| Fit (measured) | N | T (most per part) | Unbinned | Binned |
|---|---|---|---|---|
| `V13_tall` 1.0, as built | 24 | 132 (12) | 5.6 µs | 4.7 µs |
| `V13_tall` 1.0, cap 64 (31 pieces taken) | 31 | 193 (19) | 8.0 µs | 6.5 µs |
| `V13_tall` 0.4, 40 pieces | 40 | 90 (9) | 5.5 µs | 3.4 µs |
| `V13_tall` 0.4, 47 pieces | 47 | 146 (14) | 7.7 µs | 5.1 µs |
| `V13_tall` 0.3, 60 pieces | 60 | 121 (12) | 7.8 µs | 4.3 µs |
| `V13_mid` 0.4 / 0.3 | 21 / 26 | 105 / 86 | 4.6 / 4.4 µs | 3.9 / 3.3 µs |
| `V13_low` 0.4 / 0.3 | 9 / 11 | 87 / 85 | 3.2 / 3.3 µs | 3.3 / 3.3 µs |

What the table shows:
- **T is set by how many piece boxes overlap a part, not by the piece count.** It runs 85–193 for N from 9 to 60: 7–19 boxes per part at the worst spot. Large diagonal capsules have loose boxes, so the 31-piece summer fit is the most expensive one.
- **The fewest pieces that pass are also the cheapest.** The summer fit at cap 64 took 31 pieces where 24 pass, and its T rose by 46 % (193 against 132) (PR1).
- **One asset at its worst spot fits the share** in either mode, with the 60-piece autumn `V13_tall` at 7.8 µs unbinned.

### 2.3 Per tree, per clump, a belt in view

- **A whole tree with no broadphase**, every shape box-tested by every part:
  - a V01_M tree with 64 pieces on each of its 3 limbs costs 14 × 192 × 5 ns = 13.4 µs in reach, before any narrow test
  - its 150–300 capsules add 10.5–21 µs
  - two to six crowns overlap in a belt (5 m crowns, 1–2 m apart in the row, rows 2.5–3 m apart: the sheet's placement, assumed)
  
  So an object-level scan cannot hold a belt, whatever the shell cap is. A shape-level index or the prefetched cache is required (N-b), and with it the per-tree total drops out of the step cost.
- **What the broadphase removes:**
  - The 2 m cache removes everything outside the drone's neighbourhood: the whole belt but one tree or two, and a tree but the cubes the drone is in.
  - Binning the cache also removes the 14·N reject term. That leaves T, which geometry bounds (above).
- **A belt in view costs memory, not time.** The catalog's shapes are per variant and shared by instances. Even a world-space copy per instance of a 1 000-tree belt section × 192 pieces × 32 B is about 6 MB (derived).
- **Per clump against per tree.** A V13 clump (1.5–2.5 m) fits in one 2 m cube, so its cap is the neighbourhood cap. A tree spans many cubes, so the cap is per cube and the tree's total is printed, not capped.

### 2.4 The numbers

- **Per asset: N ≤ 64** (rule (d), derived).
  - It holds the measured need: `V13_tall` at 0.3 takes 60.
  - At 64 pieces with the measured T of 121–146, the worst-spot cost is 8.1–8.9 µs unbinned, about the 8.3 µs share for the asset alone.
  - Binned, it is 4.3–5.1 µs.
- **Per map: ≤ 96 in any 2 m cube** over all objects (derived).
  - That is one asset at its cap plus half a neighbour. Clumps stand with 0.5–1.5 m gaps between them (the sheet), so a 2 m cube takes at most part of two clumps' cores.
  - Binned: 1.5 × 146 × 30 ns + 0.7 µs = 7.3 µs.
  - Unbinned, two assets at their worst would reach 15.5 µs, so **the consumer bins the cache** (N-b).
- **Per fit call: 24, 32, …, 64, the first that passes.** The first step is the committed call, so every committed V13 summer shell (6 / 15 / 24 pieces) comes back unchanged. On `V13_tall` it gives 40 at 0.4 and 60 at 0.3.
- **Report and stop** if a V01 limb still fails after splitting, or if rule (d) fails. The physics engineer then revisits, with the phase-3 timings if they exist.

### 2.5 Which benchmark confirms it

**`tools/worldbench --bench`, case `veg_shells`** (world-builder, phase 3; WLD-11):
- **Static part–piece test:** ns per test, sphere–segment and segment–segment, against the assumed 30 ns.
- **Box reject:** ns per reject, against 5 ns.
- **A step through an H belt edge:** the 14 parts at 1 kHz along a path through the committed `V13_*_autumn` clumps 1–4 m apart before a `V01_M_autumn` row. Shell queries from the binned 2 m cache, µs per step (median and p99, AC power, power mode printed) against 8.3 µs.
- **The compiler's largest pieces per 2 m cube over the map,** against 96.

The share is an allocation inside the 60 µs world-query budget of a 1 000 µs step. Missing it moves microseconds between shares; it does not threaten the 1 kHz step.
- **If the measured step exceeds the share,** the map limit comes down first, since it costs the assets nothing.
- **Rule (d) comes down only after that,** in proportion. The counts above show how far it can fall before sparse `V13_tall` stops fitting: 60 pieces at 0.3, 40 at 0.4.

## 3. Sparse cover

### 3.1 Why the count rises

`leaf_cover` scales only the card count. Every twig stays (`shrub.py`; V01 design 2), and the twigs carry the shell's stiffness, so the shell should follow the same crowd at any cover.
- **The fitter's mass** is the cells within 85 mm of any twig or leaf face.
- **In summer the cards fill it solid,** so large inscribed balls are found.
- **In autumn the outer layer is twig ends about 0.27 m apart.** On `V13_tall`, 178 second-order tips spread over about 13 m² of envelope, one per 0.07 m² (derived). The mass is porous there, the candidates are small, and covering every twig end within 0.1 m takes more pieces. Rule (a) keeps any piece from bridging the gaps.

### 3.2 Measured fits (`V13_tall`, the committed fitter)

| `leaf_cover` | Cap | Pieces (capsules / spheres) | (b) twig vertex outside | (c) leaf vertex outside | Full audit |
|---|---|---|---|---|---|
| 1.0 | 24 | 24 (23 / 1) | 99.7 mm | 282.6 mm | as committed |
| 0.4 | 24 | 24 | **227.4 mm** | **341.5 mm** | fails (N-1) |
| 0.4 | 32 | 30 | **262.6 mm** | **376.1 mm** | fails |
| 0.4 | 40 | 40 (36 / 4) | 98.6 mm | 286.3 mm | **0 problems**, worst standoff 97.97 mm |
| 0.4 | 48 | 47 (42 / 5) | 96.7 mm | 271.8 mm | **0 problems**, 97.97 mm |
| 0.3 | 48 | 47 | **171.2 mm** | **321.3 mm** | fails |
| 0.3 | 64 | 60 (51 / 9) | 97.1 mm | 246.6 mm | **0 problems**, 97.97 mm |
| 0.2 | 48 | 48 | **245.4 mm** | 277.6 mm | fails |
| 0.2 | 64 / 80 | 63 (52 / 11), plateau | **119.0 mm** | 277.6 mm | fails |

- **`V13_mid`:** 21 pieces at 0.4 and 26 at 0.3. **`V13_low`:** 9 and 11. All pass (b) and (c).
- **The fitter is not monotone in its cap.** At 0.4, cap 32 took 30 pieces and failed, while cap 40 took 40 and passed. Hence PR1's steps.
- **At 0.2 it stops at 63 pieces** with three twig vertices beyond 0.1 m, whatever the cap. Below 0.3 the twig network alone sets the shell: the fit becomes a tube network around twigs, and the shell is the wrong model (N-h).

### 3.3 The alternatives, and why not

- **A looser card allowance: no.** Rule (c) is never the binding rule once (b) holds (246.6–286.3 mm against 300). Loosening it fixes nothing (b) leaves out.
- **Capsules along twig groups: no gain expected.** The fitter already offers capsules in 13 directions at three radii, and took 36–51 capsules in the passing fits. The V13 reading measured capsules fitted to twig groups at 0.30–0.91 m standoff.
- **Twigs left to their records outside the shell: little gain, more audit machinery.** I sketched it physically.
  - A point of a twig at arc length s from its base may stand outside by max(0.1 m, 3 N · s³ / (3EI)). That is where the twig's own cantilever (E 10 GPa, published 8–12; base diameter; series compliance of the parent ignored, so the bound is conservative) pushes less than the 3 N the tolerance allows (derived).
  - This frees only the outer part of thin twigs: the allowance passes 0.1 m beyond s = 0.31 m for a 5 mm twig and 0.59 m for an 8 mm one.
  - Fitted with that allowance, holding the rest on the axes, `V13_tall` at 0.4 still left 62 of 267 twigs out at 24 pieces, and 40 at 32 with the cards kept. At 1.0 it still left 7 twigs out at 32 pieces, where the vertex rule passes with 24. Holding the stiff parts on their axes costs more than the free ends save.
  - The audit would need E, the base radius and the arc length of every record.
- **Rule (b) on faces: not now.**
  - With the fitter targeting points every 5 cm along the twig faces, `V13_tall` needs about 40 pieces at summer (104.4 mm, between targets). At 0.4 it still fails at 62 pieces (124.3 mm on the faces) after a 761 s fit, and at 48 pieces (148.6 mm) after 1 010 s (measured).
  - It buys no force accuracy. The force a visible twig outside the shell would really push with is k_chain · o, with the stem's tapered cantilever at the twig's node in series with the twig's own (translation only, an upper bound; derived).
  - **Inside the tolerance band** (o ≤ 0.1 m, at least 0.1 m from the stem) that force already reaches **11.6 N on `V13_mid`** and 4.7 N on `V13_tall`. On `V13_low` it reaches 318 N: a first-order twig 0.1 m from its node at 0.12 m, 63 mm outside, where an 8 mm twig root on the foot of its stem is nearly rigid (5 kN/m).
  - **The vertex loophole** reaches **11.1 N on `V13_tall`** (a first-order twig 0.19 m from its node at 1.3 m, 214 mm out, k 52 N/m).
  - The 0.1 m rule is a geometric tolerance on a 30 N/m homogenisation of a structure that runs from about 5 N/m at a twig's tip to kN/m at a twig root low on a stem (derived), so tightening the geometry does not make the force right. N-a carries the fix to phase 7.
  - For V01 the gap closes geometrically through the kink rings (PR6).

### 3.4 The rule for tasks 1.1 and 1.4

The same rules (a), (b) and (c), the same fitter, and the cap by steps (PR1). Rule (d) is the cost gate (PR2), and `V13_tall_autumn` has `leaf_cover` ≥ 0.3 (PR9).

## 4. N-2: the material of a record

**The ruling:** a snag record takes the physics material of the member it follows.
- A record on a capsule's axis (a stem, limb or branch ≥ 10 mm) takes that capsule's material: `bark`.
- A twig record takes the material of the twig slot's physics: `twig` on V13 and V01.
- Whether its points lie inside or outside a shell piece never matters. The twig is the same twig 1 mm either side of the shell surface.

**The generator writes it on the record** as `material`, a physics id from the closed list (PR4). The compiler then resolves nothing from geometry. It could not anyway: the compiler reads the metadata, not the mesh, so it cannot tell which slot a free record runs along. A rule such as "the shell's material, else …" breaks on the first asset whose free records are not twigs:
- `deadwood` twigs on V08, whose records must snap with `break_stress_pa`
- torn sheet edges on houses

**What a record reads from its material:**
- the friction (the capstan, PHY-18)
- `break_stress_pa` where present (the record lets go when its member snaps)
- the modulus of N3's cantilever law, in phase 7

It reads neither the row's `stiffness_n_per_m` (the shell's homogenised 30 N/m) nor its `edge_radius_m`; its own `radius_m` overrides that (materials review rule 4). Records still add no part force (V13 reading §4); N-a revisits that in phase 7.

## 5. What V01's crowns change in the shell rule

- **Card allowance = card reach + 0.1 m** (derived, the V13 rule's own derivation: a sprig hangs at most its length off the twig mass).
  - V13's 0.3 m is its 0.2 m `card_m` plus 0.1 m. V01's card is a pinnate leaf of 25 cm from its petiole on the twig (design 2; the sheet's chosen default), so **0.35 m**.
  - Held at 0.3 m, a V01 leaf on a twig at the tolerance would fail (c). That would push the shell outward to chase leaf tips, which have no contact role.
  - Pods (2-card strips, 7 cm) are card slots with physics `twig` under the same allowance.
- **Twig rings at every kink, at most 0.1 m apart** (design 1's 15° kink per 0.1 m), and the twig's record a polyline through the same points.
  - Then rule (b) on vertices bounds the faces too: the distance to the union is 1-Lipschitz, so a face between two rings stands at most 0.05 m beyond the worse ring.
  - The fiber bends over the visible zigzag. A chord would sit up to about 7 mm off it (derived: 0.05 m × tan 7.5°).
- **Shells per main limb, audited per tree.** Each limb's twig, leaf and pod faces are fitted on their own (PR1, PR5). The audit holds the tree's union per material, as it does for V13.
  - Pieces of different limbs may overlap: the law takes the deepest.
  - Rule (a) is measured against all the tree's shell faces, so a per-limb fit that passes on its own limb's faces passes in the tree.
- **Limbs and branches ≥ 10 mm stay `bark` capsules inside the shell.** The shell's slots stay the twig, leaf and pod slots: a bare branch's faces do not count for rule (a), so no piece may stand on bare wood alone.
  - The 10–15 mm branches run from as soft as the shell to 20 times stiffer (derived: 15–600 N/m at 0.5–1 m from their base, E 10 GPa). As rigid capsules they over-stiffen the crown until N3's cantilever law lands. That is phase 7's to fix (N3), not the asset's: the capsules also give the fiber the real bend radius.
- **Gaps are bounded by capsules and shell pieces.** The sheet's convention closes a gap "where the crown shell … begins", so the opening search runs after the fit and treats the pieces as solid, and no piece may cross a written polygon (PR7).
  - Measured to the pieces, `narrowest_m` is what the drone's props feel. A piece reaching into a 0.6 m opening would be a soft wall inside a passable gap.
  - The current fitter cannot cross an opening by more than its 85 mm mass margin, but the test guards it.
- **One crown wind box above the trunk column.** The grid multiplies porosities where volumes overlap (knowledge §10.3; V13 reading N6).
  - Three limbs ascending from one fork interleave their leaf masses, so boxes per limb over those masses overlap. At 0.75 each, three overlapping boxes read 0.42 (derived).
  - The 2 m grid cannot resolve a limb anyway: a 5 m crown is 2–3 cells.
  - So: one box over the tree's twig, leaf and pod vertices from the lowest to the top. The trunk column (porosity 0, width dbh) runs from the ground to that box's base, and nothing overlaps (PR8).
- **The fiber ignores V01 shells,** as on V13. Every twig is geometry with a record (design 1, 4), and the line's drag in the crown is the wind volume's porosity (materials review §3).

## 6. Required changes for tasks 1.1 and 1.4

- **PR1 (1.1, `lib/shell.py`): the cap by steps.**
  - `MAX_PIECES` becomes 64 (this review §2.4).
  - `fit` tries max_pieces 24, 32, 40, 48, 56 and 64 in turn. It returns the first fit whose pieces hold every geometry vertex within the tolerance and every card vertex within the allowance, else the 64-piece fit (the audit then names what stays out). Deterministic.
  - Tests: the committed V13 summer shells are reproduced unchanged (6 / 15 / 24 pieces); a mass that 24 do not cover escalates; no fit returns more than 64 pieces.
- **PR2 (1.1, `lib/audit.py`): rule (d), the neighbourhood.**
  - For each shell material, print the largest number of its pieces whose axis-aligned boxes (asset space) meet one axis-aligned 2 m cube, and where, over cube positions on a 0.1 m grid (or exactly).
  - Fail beyond `SHELL_NEIGHBOURHOOD_MAX` = 64, naming the count, the cube and the limit.
  - Tests: 65 spheres of 0.1 m radius inside one 2 m cube fail; the same 65 spread along 8 m pass; the real V13 variants print 6 / 15 / 24.
- **PR3 (1.1, `lib/audit.py`, `build.py`, the tree generator): the card allowance follows the card.**
  - Rule (c)'s allowance is the generator's stated card reach plus `ENVELOPE_TOLERANCE_M`: 0.3 m for `shrub` (`card_m` 0.2 m) and 0.35 m for `tree` (the 0.25 m leaf card).
  - Tests: a card corner 0.33 m outside passes at 0.35 m and fails at 0.3 m; V13 is unchanged.
- **PR4 (1.1, `lib/meta.py`, `shrub.py`, `tree.py`): `material` on every snag record.**
  - The record carries the physics id of the member it follows (§4): `bark` on stems, limbs and branches, the twig slot's physics (`twig`) on twigs.
  - `meta.validate` requires it and checks it against the closed list.
  - The V13 metadata is re-emitted. Only the snag entries change, each gaining `material`.
  - Tests: a record without `material`, or with an unknown id, is refused; every record lying on a capsule's axis carries that capsule's material; every V13 and V01 twig record is `twig`.
- **PR5 (1.1, `tree.py`): V01 shells per main limb.**
  - Each main limb's twig, leaf and pod faces are fitted with PR1's steps. A limb that 64 pieces do not pass is split along the limb (by its child subtrees) into fits of ≤ 64 each.
  - The tree's union is audited per material under (a)–(d).
  - The build prints per limb and per tree the count and kinds and rule (d)'s count. If a split limb still fails or rule (d) fails, report the counts and stop.
- **PR6 (1.1, `tree.py`): twig rings and polyline records.**
  - Every twig tube has a ring at each kink, consecutive ring centres at most 0.1 m apart, and the twig's record is a polyline through the ring centres.
  - Tests: the ring spacing along every twig, and the record points equal to the ring centres.
- **PR7 (1.1, `tree.py`): gaps after the shell.**
  - The opening search treats capsules and shell pieces as solid, and `narrowest_m` is measured to both.
  - Tests: no shell piece meets a written polygon's interior (points 1 cm inside its boundary, on a 2 cm lattice, lie outside every piece); the inscribed circle agrees with `narrowest_m` within 5 mm, as now.
- **PR8 (1.1, `tree.py`): wind volumes.**
  - One crown box over the LOD0 twig, leaf and pod vertices of the whole tree, from the lowest of them to the top, at the state's porosity.
  - The trunk column (porosity 0, width dbh) runs from the ground to the crown box's base.
  - Tests: no two volumes overlap; the crown box's bottom equals the lowest twig, leaf or pod vertex.
- **PR9 (1.4): the autumn variants under PR1–PR8.**
  - `V13_tall_autumn` at `leaf_cover` 0.4 (40 pieces measured), and never below 0.3 (60 pieces measured; at 0.2 the fitter plateaus failing). `V13_low_autumn` and `V13_mid_autumn` take 9–11 and 21–26.
  - `V01_*_autumn` at 0.3 per PR5.
  - The handoff lists per variant (and per limb) the piece count, rule (d)'s count, the worst standoff, and the twig and card outside distances.
  - If the H comparison asks for `V13_tall` below 0.3, report and stop (N-a, N-h).

## 7. Spec deltas, for the orchestrator (the code follows the spec)

- **S1, asset-pipeline "Compliant shells in the collision audit"** (MODIFIED in this change's delta):
  - Add (d): at most 64 pieces of a shell material whose boxes meet any 2 m cube, printed and failing beyond, labelled derived in this review.
  - (c)'s allowance becomes the asset's card reach plus the 0.1 m tolerance.
  - Scenarios: "Too many pieces in one neighbourhood" (65 in one 2 m cube fail, spread they pass); "Card allowance follows the card" (0.33 m passes at a 0.25 m card and fails at a 0.2 m card).
- **S2, asset-pipeline metadata requirement** (MODIFIED): "snag-hazard features" become "snag records, each with a kind, at least two points, a radius and the physics material of the member it follows". Scenario: a record without a material, or with an unknown one, is refused.
- **S3, assets-vegetation V13 requirement and "Stems and shell carry the contact"** (MODIFIED):
  - "a `twig` shell of at most 24 sphere or capsule pieces" becomes "a `twig` shell of sphere or capsule pieces, the fewest of 24 to 64 that pass, at most 64 of them meeting any 2 m cube".
  - Each record names its material.
- **S4, this change's V01 requirement:**
  - shells per main limb under (a)–(d) with the 0.35 m card allowance
  - twig rings at most 0.1 m apart with polyline records
  - gaps bounded by capsules and shell pieces, crossed by none
  - "a solid trunk wind column and crown wind pieces" becomes "a solid trunk column below one crown wind volume, never overlapping"
- **S5, this change's H autumn requirement:** `V13_tall_autumn` at `leaf_cover` 0.3–0.5, not 0.2 (design 7 keeps 0.4).
- **S6, design 4 and 8, and the proposal:**
  - Design 4's "the piece cap per tree … from V13's 24 per clump" and design 8's N-2 become the rulings here.
  - Design 4's "a box per main limb" becomes one crown box.
  - The proposal's "`hook` snags on every capsule under 30 mm" still reads the old way. Design 4 already has the sheet's rule (V13 reading N6).

## 8. Notes (non-blocking), by owner

- **N-a, physics-engineer (phase 7 contact law): per-member contact through the records.**
  - The shell is a 30 N/m homogenisation of a crown whose members run from 5 N/m at a twig's tip to kN/m at a twig root near the foot of its stem. Measured on the committed V13, the tolerance band alone lets a visible twig push up to 11.6 N on `V13_mid`, and 318 N at a twig root low on `V13_low`, where the shell gives nothing (§3.3). Twigs under 10 mm get no capsule, so nothing else catches those roots.
  - The records already carry each member's axis, radius, kind and (with PR4) material, and the chain to the ground can be recovered from them. On V13 a twig's base lies within the stem chord's sag (≤ 11 mm) of its parent, and on V01 at a parent's point.
  - So phase 7 should weigh twig and branch contact as cantilever chains from the records (N3 extended to twigs: `elastic_modulus_pa` 10 GPa on `bark` and `twig`), served from the same binned cache.
  - It must not double count against the shell: shared stems are coupled springs, not independent chains.
  - It is force-faithful at any cover, including bare. A `parent` index on records would make the chain exact; that is a schema proposal for that change.
- **N-b, world-builder (phase 3 world query and compiler):**
  - `StaticContacts` indexes shapes, not objects: a V01 tree's 150–300 capsules cost 10.5–21 µs per tree in reach with an object-level scan.
  - Shells are served statically from the prefetched, binned 2 m cache, like micro-detail and the records (V13 reading N7).
  - The compiler checks ≤ 96 shell pieces per 2 m cube over the map.
  - Register `veg_shells` in worldbench (§2.5).
- **N-c, physics-engineer (phase 7):** shells take a static depth test, with no sweep and no latch (§2.1). The Time < 1 rule stays for rigid shapes.
- **N-d, qa-engineer:** V13's N-2 numbers count end points (58 / 83 / 162). Along the axes it is 64 / 93 / 179 records, up to 100.3 / 148.6 / 238.2 mm (§1). The ruling in §4 does not depend on them.
- **N-e, asset-artist (build time):**
  - Measured on this machine (power state not recorded, so not a performance claim): `V13_tall` fits take 41–125 s per cap, and the full audit 35–69 s.
  - PR1's steps can run up to six fits on a sparse variant, and V01 limbs are larger, so budget the build and print the time per fit.
  - The face-target fit took 1 010 s; it is not adopted.
- **N-f, asset-artist:** pods are a card slot with physics `twig`, under (c)'s allowance.
- **N-g, asset-artist (wind porosity by state):** the sheet gives 0.55 (summer, 0.9), 0.75 (part-bare, 0.6) and 0.9 (bare, 0) for the crown (assumed). Piecewise linear through those points, V01's autumn 0.3 is 0.825 (derived from assumed values). The box-against-crown fill (V13 reading N5) is settled with the grid in phase 3.
- **N-h, physics-engineer:** a bare state (`leaf_cover` 0) cannot be shelled by this fitter: `V13_tall` plateaus at 0.2. Bare variants wait for N-a.

## 9. Evidence

```
$ git checkout -b unity/art/p1-v01-locust be50a95 && git lfs checkout
Switched to a new branch 'unity/art/p1-v01-locust'
Checking out LFS objects: 100% (22/22), 52 MB | 0 B/s, done.

$ python -I fitcap.py V13_tall 1.0 24        # committed fitter, cap 24: reproduces the committed V13_tall shell
V13_tall cover 1.0 cap 24: pieces 24 (23 capsules, 1 spheres), radii 46-326 mm, fit 65 s; cards 3927, twigs 267
  (b) twig vertices outside max 99.7 mm, beyond 100 mm 0 of 1602
  (c) leaf vertices outside max 282.6 mm, beyond 300 mm 0 of 15708
  density: max pieces meeting a 2 m cube 24; max narrow tests per step (14 parts) 132; max per part 12

$ python -I fitcap.py V13_tall 0.4 24,32,40,48     # N-1 reproduced, then the caps
V13_tall cover 0.4 cap 24: pieces 24 (22 capsules, 2 spheres) ... (b) max 227.4 mm, beyond 100 mm 127 of 1602; (c) max 341.5 mm, 4 of 6284 beyond 300 mm
V13_tall cover 0.4 cap 32: pieces 30 (26 capsules, 4 spheres) ... (b) max 262.6 mm, 86 beyond; (c) max 376.1 mm, 6 beyond
V13_tall cover 0.4 cap 40: pieces 40 (36 capsules, 4 spheres) ... (b) max 98.6 mm, 0 beyond; (c) max 286.3 mm, 0 beyond; 2 m cube 40; T 90; per part 9
V13_tall cover 0.4 cap 48: pieces 47 (42 capsules, 5 spheres) ... (b) max 96.7 mm, 0 beyond; (c) max 271.8 mm, 0 beyond; 2 m cube 47; T 146; per part 14

$ python -I fitcap.py V13_tall 0.4 40 audit        # the full build audit (meta.assemble + audit.run) on the fit
  audit (35 s): problems 0; worst standoff 97.972 mm
$ python -I fitcap.py V13_tall 0.4 48 audit
  audit (58 s): problems 0; worst standoff 97.972 mm
$ python -I fitcap.py V13_tall 0.3 64 audit
V13_tall cover 0.3 cap 64: pieces 60 (51 capsules, 9 spheres), radii 43-204 mm, fit 47 s; cards 1178, twigs 267
  (b) twig vertices outside max 97.1 mm, beyond 100 mm 0 of 1602
  (c) leaf vertices outside max 246.6 mm, beyond 300 mm 0 of 4712
  density: max pieces meeting a 2 m cube 60; max narrow tests per step (14 parts) 121; max per part 12
  audit (69 s): problems 0; worst standoff 97.965 mm
$ python -I fitcap.py V13_tall 0.3 48   -> pieces 47; (b) max 171.2 mm, 20 beyond; (c) max 321.3 mm, 1 beyond
$ python -I fitcap.py V13_tall 0.2 48,64 ; python -I fitcap.py V13_tall 0.2 80
  cap 48: pieces 48; (b) max 245.4 mm, 27 beyond
  cap 64: pieces 63 (52 capsules, 11 spheres); (b) max 119.0 mm, 3 beyond; (c) max 277.6 mm
  cap 80: pieces 63, identical (the fitter plateaus)
$ python -I fitcap.py V13_mid 0.4 24,48 ; V13_mid 0.3 64 ; V13_low 0.4 24,48 ; V13_low 0.3 64
  V13_mid 0.4: 21 pieces at cap 24 and 48 (identical); (b) 95.7 mm; (c) 263.3 mm; 2 m cube 21; T 105
  V13_mid 0.3: 26 pieces; (b) 94.6 mm; (c) 268.8 mm; 2 m cube 26; T 86
  V13_low 0.4:  9 pieces at cap 24 and 48 (identical); (b) 97.1 mm; (c) 279.8 mm; 2 m cube 9; T 87
  V13_low 0.3: 11 pieces; (b) 94.0 mm; (c) 247.3 mm; 2 m cube 11; T 85
$ python -I fitcap.py V13_tall 1.0 64              # an unconstrained summer fit takes more pieces than it needs
V13_tall cover 1.0 cap 64: pieces 31 (30 capsules, 1 spheres) ... (b) 97.5 mm; (c) 282.6 mm; 2 m cube 31; T 193; per part 19

$ python -I records.py                             # committed V13 twig records against the committed shells
V13_low: twig records 185 (123 hook); pieces 6
  end points outside the union: 58 records, max 93.8 mm
  any axis point outside: 64 records, max 100.3 mm; beyond 0.1 m: 1 records (1 with both ends within 0.1 m), axis length beyond 0.1 m 0.01 m of 46.9 m
V13_mid: twig records 155 (103 hook); pieces 15
  end points outside the union: 83 records, max 95.2 mm
  any axis point outside: 93 records, max 148.6 mm; beyond 0.1 m: 3 records (3 with both ends within 0.1 m), axis length beyond 0.1 m 0.18 m of 52.5 m
V13_tall: twig records 267 (178 hook); pieces 24
  end points outside the union: 162 records, max 98.8 mm
  any axis point outside: 179 records, max 238.2 mm; beyond 0.1 m: 32 records (32 with both ends within 0.1 m), axis length beyond 0.1 m 4.63 m of 116.8 m

$ python -I chain.py committed                     # k_chain x o along the committed twigs (stem and twig tapered cantilevers in series)
V13_low committed:  beyond 0.1 m: max 12.26 N (0.01 m of twig); within 0.1 m (0.1 m or more from the stem): max 318.11 N (level 1, node 0.12 m, s 0.101 m, o 62.8 mm, k 5069 N/m)
V13_mid committed:  beyond 0.1 m: max 11.82 N; within 0.1 m: max 11.55 N (level 1, node 0.88 m, s 0.121 m, o 69.8 mm, k 165.6 N/m)
V13_tall committed: beyond 0.1 m: max 11.05 N (level 1, node 1.3 m, s 0.19 m, o 214.4 mm, k 51.6 N/m), p95 6.91 N, 1.92 m of twig over 3 N; within 0.1 m: max 4.66 N

$ python -I fitcap.py V13_tall 1.0 24,32,48,64 faces   # rule (b) on faces: targets every 5 cm along the twig faces
  cap 24: 23 pieces, faces max 209.0 mm; cap 32: 32, 118.1 mm; cap 48 and 64: 40, 104.4 mm (between targets)
$ python -I fitcap.py V13_tall 0.4 48,64,96 faces
  cap 48: 48 pieces, faces max 148.6 mm, twig vertices 137.1 mm, fit 1010 s
  cap 64: 62 pieces, faces max 124.3 mm, twig vertices 117.3 mm, fit 761 s; 2 m cube 62; T 164
  (cap 96 stopped: the two steps show the trend)

$ python -I fit_b.py V13_tall 0.4 16,24,32 cards ; fit_b.py V13_tall 0.4 16,24 ; fit_b.py V13_tall 1.0 24,32 cards   # free-twig allowance
  0.4, cards kept: 16 pieces 96 twigs failing (b'); 24 -> 73; 32 -> 40 (worst excess 126.5 mm)
  0.4, cards not targeted: 16 -> 93; 24 -> 62 (cards then up to 438 mm out)
  1.0, cards kept: 24 -> 18 failing (85.6 mm); 32 -> 7 (11.3 mm)

$ python -I arith.py                               # excerpt
tolerance force 3.0 N = 6.4 % of weight
the allowance reaches 0.1 m (k = 30 N/m) at s* = (3EI/30)^(1/3): d 5.0 mm: s* 0.31 m; d 8.0 mm: s* 0.59 m; d 10.0 mm: s* 0.79 m
tapered twig 5->3 mm, 0.5 m: k 4.42 N/m; uniform base 7.36: tapered/base 0.60
static shell test: 20 m/s: 20 mm per step; force change at 30 N/m 0.60 N per step   (80 km/h: 22 mm, 0.67 N)
10-30 mm branches: d 10 mm: L 0.5 m 117.8 N/m, L 1.0 m 14.7 N/m; d 15 mm: L 0.5 m 596.4 N/m, L 1.0 m 74.6 N/m
V01 bark capsules, one object-level scan: K 150: 10.5 us per tree in reach; K 300: 21.0 us
wind: three overlapping limb boxes at 0.75: 0.422
porosity by leaf_cover, piecewise linear through the sheet's points: 0.3: 0.825
cost: map cap 96 unbinned, T 2 x 146: 15.48 us; one asset at worst + half a neighbour, binned: 7.27 us; 64 cached unbinned + T 146: 8.86 us
```

The hygiene scan and `openspec validate` output are in the handoff.
