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

## After task 1.1

**Reviewer:** physics-engineer · **Task:** owner reading after 1.1 (Complex tier, D-033) · **Branch:** `unity/physics/p1-v01-locust--reading` · **From tip:** `d7931e8`
**Read:** `CLAUDE.md`, `AGENTS.md`; this change's design 9 "as built" and `tasks.md`; `tools/blender/lib/shell.py` (3fa6d76) and `lib/audit.py` (5dec350) with their tests; `generators/tree.py`, `lib/cards.py`; the committed `V01_{S,M,L}.assetmeta.json`; `unity/Assets/_Project/Scripts/World/AssetPhysicsMeta.cs`; `openspec/specs/asset-pipeline/spec.md` ("Collision audit at build time"); knowledge §7.5, §10.4, §10.5; `api-review.md` §2.3–§2.4.

"Measured" here means measured in plain Python on the committed V01 metadata, or on the committed generator and fitter run from the committed params with nothing written. The scripts lived in a scratch folder outside the repo; §A.8 has their output. The timings stay this reading's assumptions (§2.2): 5 ns per box reject, 30 ns per static narrow test, 50 ns for a part's bins, and the shells' 8.3 µs share. Sizes are S / M / L throughout.

### Verdict

1. **Orphan candidates: accepted as built** (§A.1).
   - They change the fitter's candidate set, not a rule. Every piece still passes (a)–(d), and they give force to bare twigs that would otherwise give none.
   - On the committed trees they are 1–3 of 61–70 pieces.
   - They lower neither PR9's floor nor N-h.
2. **Buried caps hidden: physically right** (§A.2). The drone meets an object's rigid primitives as their union.
   - Only rigid primitives may hide a surface; a shell piece hides nothing.
   - The requirement needs a sentence (written in §A.2), and the phase-7 law needs one matching condition (N-i).
3. **Rule (d) stays at 64 for a tree's crown** (§A.3). The cost model has room, but three things keep it there:
   - crowns interleave in a belt, and a V01_S belt already passes the map's 96
   - the timings are still assumed
   - the counts that fail come from a tube network, where the shell is the wrong model

   **The binding state is autumn.**
   - At `leaf_cover` 0.3, today's V01_S fails (d) whatever the grouping: 72 fitted per limb, 67 per stem, 68 as one whole crown.
   - V01_M and V01_L pass with room (28 and 30).

   **What task 1.3 does:**
   - It thickens with leaves before twigs, and adds no twigs to V01_S.
   - It either thins V01_S until 0.3 passes, or V01_S_autumn takes the lowest cover that passes: 0.5 today (PR9 amended).

   **The card allowance stands as a rule, but its number for the tree is 0.361 m.** The card's far corner reaches 1.044 × `leaf_m` from its petiole.
4. **200 / 232 / 330 colliders per tree fit the budget** with a shape-level, two-level, binned index (§A.4).
   - The worst step in a dense V01 belt costs at most 5.6 µs.
   - No per-tree cap and no physics LOD.
   - Phase 3 counts density as narrow tests, not boxes (N-j).
5. **`SnagFeature.material`: not now, and never the sim's source** (§A.5).
   - The core reads snag records from the `.assetmeta.json`, through the compiled package.
   - The field joins the component with its first Unity-side reader, phase 7 at the latest.

### A.1 Orphan-target candidates

**What the code does** (`lib/shell.py`: `fit`, `_Cover.orphans`, `_Mass.on`):
- **Which targets are orphans.** After the lattice candidates are built and the twig targets added, the targets that no candidate reaches are the orphans. Card targets never make orphans.
- **What each orphan gets:**
  - a sphere at the mass's inscribed radius at its cell
  - for every other orphan within 0.6 m, a capsule at the least inscribed radius along it

  Each is kept only when that radius is 40 mm or more.
- **They are ordinary candidates.** They come from the same mass (the cells within 85 mm of a shell face) and go through the same greedy, swap and step-5 shrink against the audit's own standoff. Nothing downstream can tell them from lattice candidates.
- **No-op elsewhere.** The step is deterministic: the targets are sorted and the pairs taken in order. With no orphan, the candidate list is the lattice's, and V13's three shells come back piece for piece (`test_shrub`).

**Physics:**
- **The contact law reads the pieces, not how they were found.** Under PR1–PR3 the only question is whether the pieces pass (a)–(d), and the tree's audit says they do:

  | Rule | Measured, S / M / L | Limit |
  |---|---|---|
  | (a) worst standoff | 97.9 / 97.9 / 97.8 mm | 100 mm |
  | (b) twig outside | 96.7 / 96.5 / 93.7 mm | 100 mm |
  | (c) leaf outside | 298.0 / 339.9 / 346.9 mm | 350 mm |
  | (d) pieces per 2 m cube | 58 / 20 / 22 (recounted exactly from the metadata) | 64 |

- **An orphan piece is a soft volume round a bare twig** (measured on the metadata):
  - S has 1 sphere, M has 2 spheres and 1 capsule, and L has none.
  - Their radii are 46–62 mm, round twigs of 2.4–2.9 mm radius.
  - A part that reaches the twig has pushed at most 59 mm into the piece: 1.8 N at 30 N/m (derived). That is inside the 3 N the 0.1 m tolerance allows.
- **Without them the twig gives no force at all.** Task 1.1 measured V01_M limb 1 leaving a twig 221 mm outside its shell at 18 pieces. Records add no part force (V13 reading §4), so a part would pass through a real twig untouched. The orphan step moves the force toward the twig's.
- **It does not license bare crowns.**
  - As cover falls, the step turns the shell into a tube network round the bare twigs, at about one piece per twig: in `scattered_twigs`, 30 bare twigs 0.8 m apart need more than 24 pieces.
  - That is where 30 N/m is wrong by orders of magnitude (5 N/m at a twig's tip, kN/m at its root, §3.3), and where (d) fails first.
  - N-h and PR9's floor stand: `V13_tall_autumn` keeps `leaf_cover` ≥ 0.3, and bare variants wait for N-a.
- **No cost rise.** V01's shells give T 68 / 40 / 55 at the drone's worst spot (measured, §A.4), against 85–193 for V13 (§2.2).

**Ruling (derived): accepted as built under PR1–PR3, and nothing changes.** QA confirms that the orphan path has a test of its own (§A.6, item 8).

### A.2 Capsule chains: buried caps hidden

**What the code does** (`lib/audit.py`: `_hider`, `_directed_max`):
- The primitive → mesh direction skips the points of a body primitive's surface that lie inside another body primitive (signed distance below 0).
- It drops the cells that lie wholly deeper than `CHORD_M` in one primitive, and closes in on the edge of the exposed surface.
- Shell pieces are not body primitives, so they hide nothing.
- The mesh → primitive direction already measured to the union.

**Physics:**
- **The drone meets an object's rigid primitives as their union.** A cap buried in the next capsule lies inside the union, and a part can reach it only by passing the neighbour's rigid surface first.
- **A part goes at most one step's travel into a rigid shape:** ≤ 22 mm at cruise and ≤ 40 mm at 40 m/s (§2.1; K§7.5).
  - The latch holds the plane of first touch, and that plane lies on the union's boundary.
  - The trunk's buried pole, 78 mm in, can never be touched.
  - A cap buried less than a step's travel can be crossed in an impact step, but by then the latch already sits on the outer surface.
- **So an invisible wall is a property of the union's boundary.** Measuring it per primitive counted interior surface as walls, and both directions now measure the union. That is right (derived), on two conditions:
  - **Only rigid primitives hide.** A shell piece can be pushed into by 0.1 m or more, so a rigid surface inside it can be touched and must be measured. This is how it is built.
  - **The law must see the union too** (N-i, phase 7).
    - `StaticContacts` returns one contact per (object, shape) (K§10.4). A part at a trunk joint therefore gets one contact from each capsule, both on the same surface.
    - Summed as two springs, they would double the force at every joint, as the doubled rails did in `api-review.md` F2.
    - Per (object, part), the earliest touch latches, and contacts within a few millimetres of each other with near-parallel normals count as one.
    - A part wedged between two limbs, with the normals apart, keeps both contacts.
- **A wholly buried primitive** prints "no surface outside the other primitives". It can never be the first touch, so it adds nothing to the contact, and it does no harm. The printed line is enough.

**The requirement needs a sentence.**
- "Collision audit at build time" measures "for every collision primitive, the two-sided deviation".
- Read as written, a capsule chain fails at 78 mm, and the build no longer does what the text says.
- The MODIFIED requirement below goes into this change's asset-pipeline delta (the orchestrator's edit); the new sentence is the bold one:

> The build SHALL measure, for every collision primitive, the two-sided deviation from the LOD0 surface it stands for, and fail beyond ±5 cm, or ±2 cm within 0.3 m of a gap polygon or a landable face; it SHALL also check that `narrowest_m` equals the largest inscribed circle of each gap polygon within 5 mm. **The primitive-to-mesh direction SHALL measure only the part of a primitive's surface that lies outside every other rigid (non-shell) primitive of the asset, since the drone meets the union of the rigid primitives and a surface buried in another, such as a capsule's cap inside the next capsule of a chain, can never be touched; a shell piece hides nothing, and the mesh-to-primitive direction measures to the union.**
>
> #### Scenario: Buried caps of a capsule chain
> - **WHEN** a trunk is two 80 mm capsules along a domed 12-sided tube, each one's cap at the joint buried in the other
> - **THEN** the audit passes, measuring only the exposed surface (within 10 mm of the tube), and a capsule moved 7 cm off its tube still fails

The existing scenario "Collision drift is caught" stays.

### A.3 Margins for task 1.3

**Rule (d): V01_S at 58 of 64.**
- **What the cap stands for.** Rule (d) is a box count standing in for T, the narrow tests at the drone's worst spot (§2.4). With the binned cache, which §A.4 requires anyway, a step costs T × 30 ns + 0.7 µs.
- **V01's pieces are cheaper than V13's, piece for piece** (measured):
  - V01_S: 58 pieces in a cube and T 68
  - `V13_tall` at 0.3: 60 pieces and T 121

  So the cost model has room: V01_S's shells cost 2.7 µs binned, a third of their share.
- **It still does not rise in this change:**
  - The timings are assumed until the phase-3 bench (§2.5).
  - A tree's crown is never alone in a belt; crowns interleave. Take a 4 × 3 patch of V01_S, 1.5 m apart in the row and 2.75 m between rows (the sheet's placement, assumed). There a 2 m cube meets 120 shell pieces (measured), already over the map's 96 at today's 58 per tree. At 3 m × 3 m it meets 60.
  - Raising one tree's cap raises every belt cube with it.

  The cap moves only with the bench, which then rules on a limit counted in T (N-j). V01's numbers favour that limit.
- **How 1.3 thickens inside 64:**
  - **Leaves first.** Cards fill the mass, so the fitter takes fewer, larger pieces: on `V13_tall` the count fell from 60 at `leaf_cover` 0.3 to 40 at 0.4 and 24 at 1.0 (measured, §3.2). More leaves per member (`LEAF_STEP_M`) and `leaf_m` within the sheet's 0.15–0.36 m are free under (d). So yes: more leaves per twig, not more twigs.
  - **Twigs second, with (d) printed after each step.** `children_per_order`, `orders` and the upturn add twigs. At `leaf_cover` 0.6, two in five of the last two orders' members are bare. Every bare twig is a (b) target that a piece must reach within 0.1 m, and that is what raises the count.
  - **The binding state is autumn, not the committed one.** PR9 takes the tuned crown to `leaf_cover` 0.3, where seven in ten of those members are bare. Today's crowns, fitted per limb at 0.3 (measured):
    - V01_M: 79 pieces, (d) 28
    - V01_L: 79 pieces, (d) 30
    - V01_S: 83 pieces, (d) **72**

    So M and L have room to thicken their twigs, and S has none.
  - **V01_S already fails at 0.3, whatever the grouping.** Fitting the limbs together is invisible to the contact, because the audit holds the union and the law takes the deepest piece. It still does not rescue S. Measured on today's crown (pieces, then (d)):

    | Fit | 0.6 (committed) | 0.5 | 0.4 | 0.3 |
    |---|---|---|---|---|
    | Per limb (as built) | 70, 58 | 74, 61 | 78, **65** | 83, **72** |
    | Per stem | 68, 59 | | | 81, **67** |
    | Whole crown, caps up to 64 | 56, 47 (296 s) | | | 64, fails (b) and (c) |
    | Whole crown, caps 72–96 | | | | 80, **68** (447 s) |

    - **The crown is crowded.** It holds 184 twigs, 2.3 times V01_M's and 4 times V01_L's, in the smallest crown: the two stems' six limbs share one 3 m crown.
    - **At 0.3 the shell is a tube network.** 42 of its 83 pieces are spheres, against 16 of 70 at 0.6. That is the regime where the shell is the wrong model (N-h). Raising (d) there would stretch a cost proxy to keep a wrong model, so (d) does not move.
  - **What 1.3 does with V01_S.** It adds no twigs to S. Then it either:
    - thins S's twig crowd against F (`orders` or `children_per_order` for S) until (d) at 0.3, fitted per limb, is 64 or less, or
    - keeps the crown, and **PR9 is amended**: V01_S_autumn takes the lowest cover at which (d) holds per limb, 0.5 on today's crown. That is inside the delta's 0.2–0.5 range for V01.

    The choice is the look's, against F and H; physics accepts either.
    - V01_M_autumn, the variant H measures, stays at 0.3.
    - No V01 variant goes below 0.3: the delta's 0.2 floor for V01 has no shell to stand on (N-h, as for V13; not measured on V01).
  - **Joint fits are a lever only where limbs interleave.**
    - On S at 0.6, one fit for the whole crown gives (d) 47, against 58 per limb.
    - But one fit is capped at 64 pieces and takes 5–9 minutes.
    - On S at 0.3 it does not help. On M and L at 0.3 it fails (b) and (c) at 64, where the per-limb fits pass with 79 pieces each.

    Per-limb fits stay the default.

**Card allowance: the rule stands, and its number for the tree is 0.361 m (derived).**
- **The card reaches past its length.** `cards._card` lays a card `leaf_m` long from its petiole, half its cell's aspect wide on either side.
  - Its far corners stand √(`leaf_m`² + half-width²) from the petiole.
  - Over LeafSet002's six cells (aspects 0.443–0.600) that is 1.024–1.044 × `leaf_m`: 0.261 m at the 0.25 m leaf.
- **The rule's own derivation** (card reach plus 0.1 m, §5) then gives 0.361 m. L's 346.9 mm is 3.1 mm under the 0.35 m in use and 14.1 mm under that bound.
- **The margin costs pieces, not physics.**
  - No physics rides on it: cards have no contact role.
  - A corner between 0.35 and 0.361 m makes the fitter spend pieces chasing leaf tips, and those pieces come out of the (d) headroom that V01_S lacks.
- **So `card_reach_m` becomes the card's farthest corner from its petiole,** computed from `CELLS_UV`, not typed. It follows `leaf_m` if 1.3 tunes it.
- **The shrub keeps `card_m`.** Its 0.3 m is stricter than its corner's 0.309 m, and a stricter allowance only costs pieces, so V13's committed shells stand.
- **The spec text.** The delta's "0.35 m for the tree's 0.25 m leaf cards" becomes "0.361 m". The scenario "Card allowance follows the card" (0.33 m) is unchanged.

### A.4 Collider count

**Who tests what:**
- The 14 drone parts test the bark capsules (rigid, swept, latched) and the shell pieces (soft, static, N-c).
- The fiber's ≤ 32 segments test the bark capsules and the snag records, never the shells (§5).

**Narrow tests at the worst spot** (measured): T over the 14 parts of knowledge §5.2, each part's box grown by 30 mm, at yaw 0° and 45°, over drone positions on a 0.1 m lattice. Belt patches are 4 × 3 trees (crown overlap assumed, as in §A.3).

| | V01_S | V01_M | V01_L |
|---|---|---|---|
| Colliders: bark capsules, shell pieces | 130, 70 | 171, 61 | 267, 63 |
| One tree: T for shells, bark, both | 68, 50, 99 | 40, 52, 63 | 55, 58, 86 |
| Belt at 1.5 m × 2.75 m: T for shells and bark | ≤ 162 | ≤ 131 | ≤ 141 |
| Belt at 3 m × 3 m | ≤ 117 | ≤ 98 | ≤ 110 |
| Binned cost, one tree / dense belt | 3.7 / 5.6 µs | 2.6 / 4.6 µs | 3.3 / 4.9 µs |

- **Binned, it fits.** The worst V01 belt step costs at most 5.6 µs.
  - The budget is the shells' 8.3 µs plus an equal share for rigid vegetation: 16.6 µs. Both shares are assumed: the 25 µs left after the measured 34.7 µs, split in thirds among the shells, the rigid vegetation and the fiber's vegetation contacts.
  - The swept Time search runs only on the few pairs that touch, not on all T (assumed; the bench measures it).
- **An object-level scan does not fit, whatever the per-tree count.**
  - It costs 14 × 200–330 × 5 ns = 14–23 µs per tree in reach, before any narrow test.
  - A cube in a belt is within reach of 3–6 crowns: 42–139 µs, over the whole 60 µs budget.
  - Even a per-tree cap of 150 leaves 42 µs at four crowns. A binned index needs no cap.
- **Memory decides the index's shape** (derived).
  - World-space copies cost 200–330 × 64 B per tree (two Double3 ends, a radius and ids; assumed layout), 13–21 KB. Ten thousand trees take 130–210 MB before their records.
  - A two-level index costs only the variants' few hundred KB plus about 100 B per instance: instances sit in a world grid with their transform, and each variant's shapes and bins are shared in its own frame.
- **No physics LOD:**
  - Contact cannot depend on the camera: the core is engine-free and replays headless.
  - Keeping only what is near the drone is already the broadphase's job.
  - The far field of a crown is the wind grid.
  - A fiber lying over far trees needs their real branches.
- **The sheet's fallback** (10–15 mm segments folded into the shells) is not needed.

**Ruling (derived from assumed timings, on measured counts): within budget, with a shape-level, two-level, binned index. There is no per-tree cap, and the build keeps printing the per-tree count, as it does now.** N-j carries this to phase 3.

### A.5 `SnagFeature` and `material`

- **Who reads a record's material:** the fiber, in phase 7d. It takes the capstan friction, `break_stress_pa` where present, and N3's modulus from it, all inside the engine-free core.
- **The core reads the world through the package.**
  - The phase-3 compiler builds the package from the `.assetmeta.json` files, which carry `material`, checked against the closed list.
  - The headless replay must reproduce a flight without Unity, so the core may never read physics metadata from a prefab component. If it did, the live flight and the replay could read different copies.
  - This withdraws the class comment's "the sim reads it through an adapter later". The sim's path is the package.
- **`AssetPhysicsMeta` is a Unity-side view** for noclip and the review overlays. Its `sourceJson` holds the whole file. Nothing in Unity reads a snag's material today, and no physics ever will.

**Ruling (derived): not now, and not as a physics requirement in phase 7.**
- The field joins `SnagFeature`, with an importer test, together with its first Unity-side reader: a snag overlay coloured by material, or phase 7's forensics viewer at the latest.
- The condition falls on phase 3: the compiler reads snag records, with their `material`, from the `.assetmeta.json`, never from the prefab (N-j).

### A.6 What task 1.3 and QA 2.1 must do or check

**Task 1.3:**
1. Thicken with leaves first (`LEAF_STEP_M`, and `leaf_m` within 0.15–0.36 m), then with twigs (`children_per_order`, `orders`, the upturn). After each change, print rule (d) at the committed `leaf_cover` and at 0.3 (an uncommitted override: 1.4's state), and paste both lines per variant (§A.3).
2. Keep rule (d) ≤ 64 at both covers for V01_M and V01_L. Today they stand at 20 and 22 at the committed cover, and 28 and 30 at 0.3.
3. Add no twigs to V01_S. Either thin its twig crowd until its per-limb (d) at 0.3 is 64 or less, or keep the crown and state in the handoff that V01_S_autumn takes the lowest cover that passes (0.5 today, measured per limb). Paste the line that shows it.
4. If (d) fails at a committed cover, try one whole-crown fit before thinning (V01_S: 47 at 0.6, measured). If nothing holds, report and stop.
5. Set `card_reach_m` to the card's farthest corner from its petiole: `leaf_m` × the largest √(1 + (aspect/2)²) over `CELLS_UV`. At the 0.25 m leaf that is 0.261 m, an allowance of 0.361 m. Add a test, and leave the shrub unchanged.
6. Leave the orphan step and the buried-cap hiding as built.

**QA 2.1:**

7. Rule (d) is 64 or less per variant in the committed audit lines and in 1.3's lines at 0.3, or for V01_S at the cover 1.3 names.
8. The orphan path has a test of its own: a twig that no lattice candidate reaches is covered, and with no orphan the candidates are the lattice's. If none exists, request it.
9. The capsule-chain test exists, plus a test that a rigid surface inside a shell piece is still measured (only rigid primitives hide).
10. The printed allowance is 0.361 m for the tree's 0.25 m leaf and 0.3 m for the shrub, and the rule (c) lines pass.
11. The deltas carry three edits (the orchestrator's; `openspec validate --strict`):
    - asset-pipeline: §A.2's sentence and scenario
    - both deltas: 0.361 m in place of the tree's 0.35 m
    - assets-vegetation, "The H autumn belt state": V01's autumn cover from 0.3, and V01_S's at its measured floor (§A.3)

### A.7 Notes (non-blocking), by owner

- **N-i, physics-engineer (phase 7 contact law):** an object's rigid primitives act as one surface per part (§A.2).
  - The earliest touch latches.
  - Coincident contacts at a joint merge: points within a few millimetres, normals near-parallel.
  - A wedge keeps both contacts.
- **N-j, world-builder (phase 3), sharpening N-b:**
  - **A two-level shape index** for the parts and the fiber alike: instances in a world grid, and each variant's shapes and bins shared in its own frame.
  - **`veg_shells` runs the V01 belt patches of §A.4.**
  - **Once that bench confirms the timings, the compiler's density check counts narrow tests, not boxes:** at the 14-part layout, over rigid and shell shapes. The 96-piece box cap refuses a 1.5 m V01_S belt (120 pieces per cube) whose shells cost only 3.6 µs binned (T 97). Rule (d) follows the same measurement.
  - **Snag records come from the `.assetmeta.json`** (§A.5).

### A.8 Evidence

```
$ git switch -c unity/physics/p1-v01-locust--reading origin/unity/art/p1-v01-locust
Switched to a new branch 'unity/physics/p1-v01-locust--reading'

$ python -I v01density.py <repo> SML belt      # committed metadata: shape sizes, rule (d) exact, T at the 14-part layout, belt patches
V01_S: shapes 200 (bark 130, twig 70)
  twig: radius 43-155 mm (median 62); axis length 0.10-1.20 m (median 0.30)
  bark: radius 3-42 mm (median 9); axis length 0.03-1.27 m (median 0.30)
  twig: most boxes meeting a 2 m cube 58 at (-1.24, 3.1, -1.07); T 68 at (-0.25, 4.77, -0.63, 'yaw0')
  bark: most boxes meeting a 2 m cube 104 at (-1.2, 2.77, -0.86); T 50 at (-0.26, 4.04, -0.37, 'yaw0')
  all: most boxes meeting a 2 m cube 160 at (-1.2, 3.04, -0.88); T 99 at (-0.25, 4.74, -0.63, 'yaw0')
  orphan-step pieces (capsules off the lattice directions, spheres on a twig ring): 0 capsules, 1 spheres
V01_M: shapes 232 (bark 171, twig 61)
  twig: radius 43-155 mm (median 70); axis length 0.08-1.00 m (median 0.40)
  bark: radius 3-85 mm (median 11); axis length 0.07-1.01 m (median 0.32)
  twig: most boxes meeting a 2 m cube 20 at (-2.04, 7.15, -1.96); T 40 at (-0.91, 9.05, -0.46, 'yaw0')
  bark: most boxes meeting a 2 m cube 47 at (-1.11, 5.04, -0.42); T 52 at (0.38, 3.0, 0.06, 'yaw0')
  all: most boxes meeting a 2 m cube 58 at (-1.7, 5.51, -0.58); T 63 at (0.99, 7.3, 2.14, 'yaw0')
  orphan-step pieces (capsules off the lattice directions, spheres on a twig ring): 1 capsules, 2 spheres
V01_L: shapes 330 (bark 267, twig 63)
  twig: radius 42-125 mm (median 56); axis length 0.15-1.00 m (median 0.45)
  bark: radius 2-136 mm (median 14); axis length 0.14-1.34 m (median 0.33)
  twig: most boxes meeting a 2 m cube 22 at (-2.37, 10.0, -0.65); T 55 at (-2.12, 11.01, 1.03, 'yaw0')
  bark: most boxes meeting a 2 m cube 66 at (-1.75, 8.34, -3.05); T 58 at (-0.83, 8.84, -2.05, 'yaw0')
  all: most boxes meeting a 2 m cube 82 at (-1.72, 8.69, -2.96); T 86 at (-1.02, 8.94, -2.17, 'yaw0')
  orphan-step pieces (capsules off the lattice directions, spheres on a twig ring): 0 capsules, 0 spheres
  belt V01_S 4x3, 1.5 m in the row, rows 2.75 m: 2 m cube twig 120, bark 200; T twig 97, bark 65, together <= 162
  belt V01_S 4x3, 3.0 m in the row, rows 3.0 m: 2 m cube twig 60, bark 104; T twig 67, bark 50, together <= 117
  belt V01_M 4x3, 1.5 m in the row, rows 2.75 m: 2 m cube twig 73, bark 172; T twig 63, bark 68, together <= 131
  belt V01_M 4x3, 3.0 m in the row, rows 3.0 m: 2 m cube twig 40, bark 92; T twig 49, bark 49, together <= 98
  belt V01_L 4x3, 1.5 m in the row, rows 2.75 m: 2 m cube twig 75, bark 215; T twig 70, bark 71, together <= 141
  belt V01_L 4x3, 3.0 m in the row, rows 3.0 m: 2 m cube twig 43, bark 125; T twig 52, bark 58, together <= 110

$ python -I orphans.py <repo>                  # the orphan-step pieces and what they stand round
S orphan sphere r 62 mm on a twig of radius 2.5 mm, 0.0 mm off its ring
M orphan sphere r 62 mm on a twig of radius 2.9 mm, 3.3 mm off its ring
M orphan capsule r 46 mm, axis 0.08 m
M orphan sphere r 62 mm on a twig of radius 2.4 mm, 0.7 mm off its ring

$ python -I -c "...cards.CELLS_UV..."          # each cell's aspect and its far corner over leaf_m
0.443 1.0242 / 0.577 1.0408 / 0.462 1.0264 / 0.461 1.0262 / 0.6 1.0441 / 0.481 1.0285

$ python -I jointfit.py <repo> V01_S V01_S@0.3 V01_M@0.3 V01_L@0.3    # the committed generator and fitter; nothing written
V01_S leaf_cover 0.6: as built: shell tree: 70 pieces (54 capsules, 16 spheres); most meeting one 2 m cube 58, limit 64
  joint: shell stem 0: 32 pieces (24 capsules, 8 spheres) at cap 32, passes (b) and (c); most meeting one 2 m cube 31; fit 20.4 s
  joint: shell stem 1: 36 pieces (26 capsules, 10 spheres) at cap 40, passes (b) and (c); most meeting one 2 m cube 32; fit 32.8 s
  joint: shell whole crown: 56 pieces (39 capsules, 17 spheres) at cap 56, passes (b) and (c); most meeting one 2 m cube 47; fit 296.1 s
V01_S leaf_cover 0.3: as built: shell tree: 83 pieces (41 capsules, 42 spheres); most meeting one 2 m cube 72, limit 64
  (limbs 12, 9, 18, 12, 8, 24 pieces, all at cap 24)
  joint: shell stem 0: 39 pieces (16 capsules, 23 spheres) at cap 40, passes (b) and (c); most meeting one 2 m cube 33; fit 20.3 s
  joint: shell stem 1: 42 pieces (19 capsules, 23 spheres) at cap 48, passes (b) and (c); most meeting one 2 m cube 37; fit 55.7 s
  joint: shell whole crown: 64 pieces (41 capsules, 23 spheres) at cap 64, FAILS (b) and (c); most meeting one 2 m cube 54; fit 553.1 s
V01_M leaf_cover 0.3: as built: shell tree: 79 pieces (40 capsules, 39 spheres); most meeting one 2 m cube 28, limit 64
  (limbs 23 at cap 24, 40 at cap 40, 16 at cap 24)
  joint: shell stem 0: 64 pieces (31 capsules, 33 spheres) at cap 64, FAILS (b) and (c); most meeting one 2 m cube 22; fit 365.4 s
V01_L leaf_cover 0.3: as built: shell tree: 79 pieces (36 capsules, 43 spheres); most meeting one 2 m cube 30, limit 64
  (limbs 37 at cap 40, 10 at cap 24, 32 at cap 32)
  joint: shell stem 0: 64 pieces (27 capsules, 37 spheres) at cap 64, FAILS (b) and (c); most meeting one 2 m cube 26; fit 462.3 s

$ python -I stemfit.py <repo> V01_S V01_S@0.3   # rule (d) over the union of the two stems' fits
V01_S leaf_cover 0.6: per-stem union: 68 pieces; most meeting one 2 m cube 59
V01_S leaf_cover 0.3: per-stem union: 81 pieces; most meeting one 2 m cube 67

$ python -I widefit.py <repo> wide V01_S 0.3 72,80,96
V01_S leaf_cover 0.3 whole crown, caps (72, 80, 96): 80 pieces at cap 80, passes (b) and (c); most meeting one 2 m cube 68; fit 447 s

$ python -I widefit.py <repo> limbs V01_S 0.4 ; python -I widefit.py <repo> limbs V01_S 0.5
V01_S leaf_cover 0.4 per limb: shell tree: 78 pieces (45 capsules, 33 spheres); most meeting one 2 m cube 65, limit 64
V01_S leaf_cover 0.5 per limb: shell tree: 74 pieces (51 capsules, 23 spheres); most meeting one 2 m cube 61, limit 64
```

Fit times are plain Python on the dev machine with the power state not recorded, so they are not performance claims (N-e). The hygiene scan and `openspec validate` output are in the handoff.

## After QA 2.1 (§B: task 1.8, the rulings for R1, R5 and R2)

**Reviewer:** physics-engineer · **Task:** 1.8 · **Branch:** `unity/art/p1-v01-locust` · **From tip:** `2fa7f20`
**Against:** QA 2.1's R1, R2 and R5 (`review.md`); AST-05, AST-06, AST-09, REAL-05, PHY-05, PHY-18, PHY-19, PR-10; this reading's §3.3, PR9, N-a, N-h, §A.1, §A.3, §A.4, N-i and N-j
**Read:** `CLAUDE.md`, `AGENTS.md`, VISION §2; `review.md` (R1, R2 and R5 in full); this file; design 3, 4 and 9 and tasks 1.3–1.5 as built; both deltas; the sheet's group rules, §V01 and §V13; `docs/requirements.md` for the IDs above; `tools/blender/lib/{shell,skeleton,audit,cards}.py`, `generators/{tree,shrub}.py`; the committed V01, V13, L01 and L03 metadata; the archived `p1-v13-shrub` review-physics §4 and N3; knowledge §5.2–§5.4, §6.8, §7.3 and §10.4–§10.5; `surfaces-review.md` §5 and §6.4; `api-review.md` §7; `docs/plan.md` phase 7

"Measured" here is plain Python on the committed generators, skeleton, cards, fitter and audit, from the committed params with the overrides named and nothing written, or on the committed metadata. The scripts lived in a scratch folder outside the repo; §B.8 has their output.
- **The timings stay §2.2's assumptions:** 5 ns per box reject, 30 ns per narrow test, 50 ns for a part's bins, the shells' 8.3 µs share.
- **No bench was run.** The rulings rest on counts of one kind of narrow test (a part against a segment or a piece, closed form), not on its nanoseconds. The laptop was on battery for most of this reading and shared with other agents' jobs throughout, so a timing would not have counted anyway. N-m carries the timing to phase 3's `veg_shells`.
- **T** is §2.2's measure. Over drone positions on a 0.1 m lattice, at yaw 0° and 45°, it is the sum over the 14 parts of the shapes whose boxes meet the part's box grown by 30 mm, at knowledge §5.2's assumed layout. Rebuilt here, it gives the committed V01_S shell 65–67, against §A.4's 68 on 1.1's crown.
- **The dense crown is task 1.3's denser try on today's V01_M params,** `orders` 6 and `children_per_order` 3 ("try13" below).
  - On seed 11 it grows 1 238 twigs (1.3's note counts 1 056 on the seed of that day): 294 m of twig in 3 834 ring segments, and 2 899 leaves at `leaf_cover` 0.6.
  - Its LOD0 is 37 558 triangles (measured, no pods), against the tree's 40 000, so it is about the densest net the budget lets R1's crown work build. On V01_L the same rule gives 44 988, over the budget.
  - A bound past the budget: children every 0.3 m along every member ("spaced") grows 7 068 twigs and 2 124 m of twig, 279 346 triangles.

### Verdict

1. **(i) A twig net of F's density is carried member by member, by its records, and V01's crown carries no shell** (§B.2).
   - Twigs stay geometry with one record each, as built. No twig becomes a card: in the sheet's 3–10 mm band there is no X below which a twig loses a physical role (§B.1).
   - **A shell is the wrong model for an open twig net, even over its leaves alone.** On try13 at 0.3, a shell over the leaf mass only takes 275 pieces and rule (d) 85 of 64 (measured): a piece or more per leaf tuft, the tube network again. At the committed 0.6, three of its four limbs alone already meet 78. Holding every twig took 68 and 89 (task 1.3, on its seed).
   - **So V01 drops its `twig` shell.** Its crown's contact is its bark capsules and its twig records. Its leaves have no contact role (§A.3), and its wind volume stays as built.
   - **Every `twig` record carries `radii_m`,** its member's taper, because the cantilever it stands for goes as d⁴.
   - **Phase 7 (7a, PHY-05) pushes the parts with the records** as rooted, tapered cantilever chains (N-a, now a requirement), and the props strike every record. Its narrow phase is of the shells' order: T 166 for try13's records and 219 in a dense belt (measured), against 47–105 for today's shells and up to 193 for V13's in §2.2, 7.3 µs binned at the assumed 30 ns, inside the shells' 8.3 µs.
   - **Where a shell remains** (a closed leaf mass, V13's clumps), rule (b) holds only the twigs in its leaf mass, and the rest are their records'. Rule (d) stays at 64.
   - Physics needs no stand-in: F's full net fits the triangle budget and these rules whenever it is built. D-040 moves it to after G1, so this ruling lands with that work, and ruling (ii) with task 1.9 at G1 (§B.5).
2. **(ii) A crotch is no record: not a `hook`, and no kind of its own** (§B.3).
   - A line pressed into a fork is held by both members at once, as a wedge. The members' own capsules and records already give the two contacts, and the phase-7 fiber law resolves them together.
   - A `hook` goes only on a free tip, a member end from which nothing leaves. V01's trunk tops lose theirs.
3. **(iii) R2's sparser band meets no shell floor** (§B.4).
   - With rule (b) on the leaf mass, `V13_tall_autumn`'s shell passes at every cover down to the sheet's bare 0.1, rule (d) at most 55 of 64 (at 0.2): a margin of 9 pieces (14 %). With no leaves there is no shell at all.
   - Under the committed rule the floor was PR9's 0.3, and today's thicket at 0.4 already takes 62 of 64.
   - V01 has no shell, so its covers have no floor either: `V01_S_autumn` may come back to 0.3 if the look asks.

### B.1 What one twig does to the drone, and what carries it

Each twig of the sheet's band at the generator's length, 70 times its base diameter (`SLENDERNESS`):
- **The push:** a cantilever 3EI/s³ at its base diameter (E 10 GPa, published 8–12; derived).
- **The fiber:** the bend stress E·r/(R + coating) (E 73 GPa, r 62.5 µm, coating 62.5 µm, published) against the 0.69 GPa proof test.
- **A prop:** one cut, and a twig fed into the disc (assumed: a rotor of 1.0 × 10⁻⁴ kg·m² at 8 200 rpm in the hover, from 2.6 kgf at 12 000 rpm with thrust as n²; wood across the grain 10⁴ J/m², the published order).

| Twig | 3 mm × 0.21 m | 5 mm × 0.35 m | 8 mm × 0.56 m | 10 mm × 0.70 m | Carried by |
|---|---|---|---|---|---|
| Push: k at the tip; at mid-length | 13; 103 N/m | 22; 172 N/m | 34; 275 N/m | 43; 343 N/m | the shell's uniform 30 N/m, or the record |
| The fiber bent over it | 2.92 GPa, 4.2 × proof | 1.78 GPa, 2.6 × | 1.12 GPa, 1.6 × | 0.90 GPa, 1.3 × | the record |
| A prop: one cut (of the rotor's 37 J); fed in, a cut every blade pass | 0.07 J (0.19 %); 29 W | 0.20 J (0.53 %); 81 W | 0.50 J (1.4 %); 207 W | 0.79 J (2.1 %); 322 W | the record (PHY-05) |
| The wind | the crown box's porosity | | | | the wind volume |

What it shows:
- **Every twig of the band has a physical role at every diameter, and the thinnest is the fiber's worst.** A 3 mm twig bends the line to 4.2 times the proof stress, and the stress falls under proof only past 13 mm (0.30 GPa at the sheet's 30 mm edge threshold).
- **A prop that meets a twig strikes it.** One cut costs the rotor 0.2–2 % of its energy. A twig fed into the disc at the drone's speed is cut at every blade pass, 410 a second: 29–322 W of the motor's 870 W at 40 A on the loaded pack. A shell cannot make a strike: it is a soft volume, not a member in the disc.
- **The shell's 30 N/m is a twig's tip.** Halfway along, every twig of the band is 3–11 times stiffer. At a 2 cm push, one step at cruise, a 5 mm twig's middle already gives 3.4 N, past the shell's 3 N tolerance.
- **So the record carries every role a lone twig has:** the fiber's capstan, hook and bend, the prop's strike and, given its taper, the push. It carries them only because it lies on the axis of the tube the pilot sees.

### B.2 Ruling (i): a twig net of F's density

**The dense net, measured** (V01_M; T at the worst spot, yaw 0° / 45°; the belt a 4 × 3 patch of the same tree at 1.5 × 2.75 m, each a quarter turn on from its neighbour):

| | Committed, 0.6 | try13, 0.6 | try13, 0.3 | spaced, 0.6 (a bound) |
|---|---|---|---|---|
| Twigs; twig length; ring segments | 194; 62 m; 721 | 1 238; 294 m; 3 834 | the same | 7 068; 2 124 m; 25 821 |
| Leaves; LOD0 triangles | 817; 13 084 | 2 899; 37 558 | 1 412; fewer | 16 582; 279 346 |
| Twig length with a ring more than 0.1 m from every leaf face | 8.8 m (14 %) | 13.7 m (5 %) | 69.5 m (24 %) | 114 m (5 %) |
| T, every twig-record segment | 74 / 70 | 166 / 137 | 166 / 137 | 231 / 204 |
| T, the segments outside the leaf mass | 34 / 36 | 44 / 37 | 79 / 71 | 55 / 48 |
| T, bark capsules | 56 / 55 | 56 / 55 | 56 / 55 | 101 / 93 |
| Belt: T for every record; for bark | 87; 76 | 219; 68 | | 595; 207 |
| Record segments meeting one 2 m cube: tree; belt | 149; 477 | 883; 2 215 | | 4 372; 15 421 |
| A 1 m fiber segment through the densest metre: record segments | 11 | 18 | | |
| The shell, every twig held (committed rule (b)) | 130 pieces, (d) 34 | (d) 68, every limb split (task 1.3, 1 056 twigs) | (d) 89 (the same) | |
| The shell over the leaf mass only (below) | | three limbs of four: 202 pieces, (d) **78** or more | 275 pieces, (d) **85** | |

On the same layout, the committed shells give T 47–75 (V01) and 100–105 (V13_tall and its autumn twin), and V13_tall's twig records 146.

**The shell over the leaf mass, measured.** The committed fitter on try13's V01_M, per main limb, a limb that 64 pieces do not pass split into its child subtrees as `tree._shells` does. (b)'s targets are the twig ring stretches within 0.1 m of a leaf face of their fit's members, and the cards are held under the 0.361 m allowance. A last fit covers the ring centres that lie within 0.1 m of another limb's leaves, with the leaves near them as mass only. Pods are left out (a summer crown).
- **At 0.3:** limbs 1 and 4 pass whole (58 and 64 pieces), limbs 2 and 3 fail at 64 and split (69 and 68 pieces), and the last fit adds 16: **275 pieces, rule (d) 85**: a third over the limit, against 89 with every twig held on 1.3's seed.
- **At 0.6,** the committed cover and F's part-bare state: limbs 1 and 4 pass at 64 pieces each, and limb 3 fails and splits (74). Those three alone meet **78** in one 2 m cube, so the tree's (d) is at least that. Limb 2's fit was stopped there, since it can only add.
- **Today's V01_S at 0.3 passes the same way** (49 pieces, (d) 44, where every twig held took 83 and 72). So the density of the net decides, not the variant, and F's is past what a shell carries under (d). V01_S drops its shell too: there it would pass, but it would add nothing the records do not carry, and a second contact law on one asset.
- **Why:** on F's net the leaves follow the twigs. They are tufts along 294 m of twig through the whole crown, not a closed mass. Holding them within 0.1 m takes 64 pieces or more per limb, and four limbs' pieces crowd the crown's middle cubes. At 0.3 the tufts stand apart, and at 0.6 there are more of them; either way the fit becomes a web of pieces round the net. That is N-h's regime, with leaves in it.

**The options on record:**

1. **Per-member contact through the records (N-a): adopted, for the whole of V01's twig net.** V01 drops its shell.
   - **It is force-faithful at any cover** by construction. Each segment pushes with its own cantilever through its taper, 13–343 N/m over the band (§B.1), in series with its parent at its first point. A shell gives 30 N/m whatever the twig.
   - **Its cost is of the shells' order.**
     - Every record of try13 together gives T 166 for the tree and 219 in a dense belt: 219 × 30 ns + 0.7 µs = 7.3 µs binned, inside the shells' 8.3 µs (assumed timings). Today's shells give 47–105 on the same layout.
     - The triangle budget stops the net before the cost does: "spaced" reaches 595 only at seven times the budget.
     - The fiber meets at most 18 record segments in a metre of line through try13's densest metre, against 11 today.
   - **It is the law the micro-detail stems already have** (knowledge §7.3, PHY-05): a rooted cantilever pushed through and capped at slide-off, served from the prefetched binned cache by `Id`. `surfaces-review.md` §6.4 counts 100–300 such element tests per part in a meadow.
   - **The records are complete data.** If phase 7's bench ever wants a coarser model, the compiler can derive it from the records with no asset rebuilt; a shell cannot give the records back.
   - **What it needs from the asset, so what G1 carries:**
     - the axis: the ring centres, as built
     - the material, as built
     - the radius at each point, `radii_m` (new). The mean radius is not enough: for a 2:1 taper, a cantilever at the mean radius is 1.6 times too soft at the tip and 3.0 times at a tenth of its length (derived).
     - the parent: the member whose axis passes through its first point. The generator starts every child on its parent's axis, so the compiler finds it without a field. R1's crown work tests that one member's does, within 0.01 mm, and no other's.
   - **What waits for phase 7** (7a):
     - the law, and `elastic_modulus_pa` on `twig` and `bark` (N3, N-a)
     - a side latch at first touch: a 3–10 mm twig is thinner than the 22 mm a part moves in a step at cruise, so a static depth would let it through. The test is the part's swept volume against the segment, the same closed form.
     - the slide-off cap
     - prop strikes with the cut energy (X-6)
   - **Nothing is counted twice where a shell remains.** A record segment with both ends inside the shell or within its 0.1 m tolerance is the shell's, every other segment the record's, a static split per variant. On V01 every segment is the record's.
2. **Rule (b) narrowed to the leaf mass: adopted for the shells that remain, and not enough for V01.** A bare twig away from every leaf is its record's wherever there is a shell (this reading's own first form of the ruling). It keeps V13's closed clumps in the shell's regime (§B.4), but on F's net it still gives (d) 85 (above).
3. **Rule (b) relaxed beyond §3.3's cantilever bound: not adopted.** At try13's own taper, 3 N bends only 28 % of its twig length (83 of 294 m) more than 0.1 m. The shell would still chase the other 72 %, so the network stays, at the shell's wrong stiffness.
4. **Twigs as cards inside the crown: not adopted. There is no X.**
   - Every twig of the band has a role (§B.1), and the thinnest is the fiber's worst.
   - The record carries those roles only because it lies on the tube the pilot sees. A card is a picture of a twig, and edge-on it is gone, so the line and the props would meet a member the pilot cannot see there. That is what REAL-05 bars ("no alpha cards for anything with a physical role"), and it breaks AST-05's collision following the visual.
   - On the audit, cards would only move the twigs from rule (b) to (c), from 0.1 m to 0.361 m: a weaker form of option 2, bought with a visual fake, and the measured (d) shows option 2 is not enough anyway.
5. **Rule (d) raised after the `veg_shells` bench: not adopted.** At F's density the shell is a piece-per-tuft network (275 pieces), the regime where 30 N/m is wrong by orders of magnitude. Raising (d) past 85 would stretch a cost proxy, on assumed timings, to keep a wrong model. (d) stays at 64 for the shells that remain, and N-j's re-ruling of (d) in T terms after the bench stands.

**What V01's metadata carries with F's net** (after G1 by D-040; at G1 V01 keeps today's crowns and shells, which pass today's rules):
- every twig a 3-sided tube ringed at every kink at most 0.1 m apart, with one polyline record through its rings (as built), and `radii_m` on every `twig` record (new)
- every limb and branch of 10 mm or more a `bark` capsule, as built
- no crown shell, so the shell rule's (a)–(d) have nothing to audit on V01, and no fit runs (task 1.3's dense limbs took 10–20 min a fit)
- gaps bounded by capsules and twig records, as the gap search already draws them in, and the wind volumes as built
- so the net's whole contact is in the metadata, and nothing about it waits for a rebuild

**What waits:** the law and its rows (above) in 7a, the fiber's wedge (§B.3) in 7d, and a records case for `veg_shells` in phase 3 (N-m).

**Requirements:**
- **Kept:**
  - REAL-05: twigs stay geometry, and "the crown's physics is its wind volume plus twig and branch snags" now holds for V01 to the letter.
  - AST-05: the metadata the law reads is authored with the asset.
  - AST-09, "fine twig crowns that read at distance": the net is no longer bounded by (d).
  - The V01 requirement's look, F's net.
- **Changed** (§B.6 has the text):
  - the V01 requirement's shell clause (no shell; gaps bounded by capsules and records) and its record clause (`radii_m`)
  - the metadata contract (`radii_m`)
  - the shell rule (b) and its scenario, for the shells that remain
  - PHY-05: the records as stems
  - the sheet's Geometry rule (twigs are geometry, not cards; a shell only over a closed leaf mass) and V01's physics role
- **Superseded:**
  - N-h's "bare variants wait for N-a", for the metadata: a bare crown has no leaf mass, so no shell, and its records carry it. Its contact law waits for phase 7, as every contact law does.
  - §A.3's V01 margins and §A.4's shell columns, for V01: it has no shell pieces left to count.

### B.3 Ruling (ii): a crotch

**Where.** V01's trunk tops, where the limbs leave the trunk's last point (`skeleton.branches` starts every limb at `trunk.points[-1]`): 76, 145 and 219 mm wide (S, M, L; QA's R5).
- Wood and twig children leave their parent at 0.2–0.95 of its length, so a limb's, a branch's or a twig's tip is free.
- V13's first-order twigs whose children leave at their end are already `edge`s, so V13 has no crotch hook.

**What the line meets there** (measured on the committed skeletons): neighbouring limbs leave a trunk top 40–56° apart, half-angles 20–28°, and wood children leave their parent at 19–47°.
- **A line pressed into a fork is held by both walls.** At half-angle α each wall gives it a normal force P/(2 sin α), so the friction holding it is μP/sin α: 2.1–2.9 times a single member's at these angles (the V-belt effect). Over a quarter wrap on bark (μs 0.55, assumed) the capstan's e^(μθ) of 2.4 becomes 6–12.
- **It jams when μ > tan α.** Bark's 0.55 is above tan 28° = 0.53, so every V01 trunk crotch and wood fork holds a line pressed into it until the line is drawn back out the way it came. That is one of the pilot's "snagging on trees" (PHY-19).

**Ruling (derived): no record.**
- **Not a `hook`.** A hook is a free tip: the line catches near the end and can slide off over it. At a crotch the members go on, so there is nothing to slide off, and a hook would release a line that should stay wedged. On V01 it also makes the trunk's top 1.0–1.3 m a hook at 38–110 mm radius.
- **No kind of its own.** The wedge is two contacts at once, and the metadata already holds both: the members' capsules (10 mm or more) or their records (twigs). A third kind would duplicate them, and double the force where they meet (N-i). Every fork can be recovered from the metadata, since a member starts on its parent's axis, so 7d's compiler can list the apexes if its node placement wants them.
- **The trunk's top takes the record its width gives:** an `edge` under 30 mm, none at 30 mm or more. On V01 that is none, so each trunk-top hook goes (two on S, one on M and on L, and the same on the autumn twins) and nothing else changes. V13 is unchanged.
- **Phase 7d:** a line node touching two members at once resolves both normals and frictions together, the fiber's form of N-i's wedge rule (N-k).

**Requirements.**
- **Kept:** the V01 requirement's and the sheet's "every free tip a `hook`", with the free tip now defined as a member end from which nothing leaves.
- **Changed:** PHY-18 names the wedge (§B.6).

**The test (task 1.9):** every `hook`'s last point is no member's first point, and no trunk carries a hook.

### B.4 Ruling (iii): R2's floor

**What R2 asks of V13:** a 1–2 m band of many thin dark stems through sparse leaves, with leaves along the stems. That is fewer leaves in the band and more bare twig there.

**The floor under the committed rule (b) is PR9's.**
- `V13_tall_autumn` never below 0.3: 60 pieces at 0.3, and at 0.2 a plateau failing (b), measured on the skeleton before task 1.5 (§3.2).
- Today's thicket takes 62 pieces at the committed 0.4, (d) 62 (measured: the build's own fit, reproduced): a margin of 2 pieces (3 %).
- So under the committed rule R2 had no room. Every bare twig the band shows is one more (b) target, and N-h's tube network starts there.

**With rule (b) on the leaf mass (ruling (i), option 2), a sparser band takes no more pieces.** V13's clumps are a closed leaf mass, the shell's own regime, so V13 keeps its shell under that rule. Measured on `V13_tall_autumn`: the committed shrub generator at each cover, one fit over the clump with every face in the mass, (b)'s targets the twig vertices within 0.1 m of a leaf face, and each of them checked afterwards as the audit would.

| `leaf_cover` | 0.4 (built) | 0.3 | 0.2 | 0.1 |
|---|---|---|---|---|
| Leaf faces | 1 571 | 1 178 | 785 | 393 |
| Twig vertices in the leaf mass, of 1 638 | 1 502 | 1 413 | 1 257 | 740 |
| Pieces (capsules, spheres) at cap | 47 (43, 4) at 48 | 48 (40, 8) at 48 | 55 (49, 6) at 56 | 37 (34, 3) at 40 |
| Rule (d), of 64 | 47 | 48 | 55 | 37 |
| Farthest leaf-mass twig vertex outside the union | 97.4 mm | 97.6 mm | 97.9 mm | 95.8 mm |
| The committed rule, every twig held | 62, (d) 62 | 60 before the thicket | fails (before the thicket) | |

**Ruling (derived): the band meets no shell floor.**
- With rule (b) on the leaf mass the shell passes (a)–(d) at every cover down to the sheet's bare state, 0.1. Rule (d) peaks at 55 at 0.2: the margin is 9 pieces (14 %). A clump with no leaves has no shell, and its stems and records carry it.
- The floors left are not the shell's:
  - PR-10 and the scenario "Matches H", no sky through the 1–2 m band: the look's, and the staging's
  - the wind porosity, 0.7 − 0.35 × `leaf_cover` (the sheet's points), which follows the cover by itself
- **R2's work (after G1, D-040) builds V13 under rule (b) on the leaf mass** if its band needs more bare twig than the committed fit can hold; at the committed rule it has 2 pieces of room. It reports and stops only if a variant's (d) then passes 64, not at a cover.
- **V01 has no floor at all,** since it has no shell (§B.2). So `V01_S_autumn` may come back to H's 0.3 if the look asks, and §A.3's 0.5 for it lapses.

**Requirements:** PR-10 and the scenario "Matches H" are kept. The H autumn requirement's covers change (§B.6, edit 17). PR9's floor and N-h are superseded for the shell.

### B.5 When each ruling lands, and what QA 2.2 checks

D-040 moves R1 (the crown net) and R2 (the H belt edge) to after the pilot's G1 look, with these rulings (`tasks.md`). So:

**At G1, task 1.9 (R5, ruling (ii)):**
1. Put a `hook` only on a free tip. Test that every `hook`'s last point is no member's first point, and that no trunk carries one.
2. V01 keeps today's crowns and their shells, which pass today's rules. Physics needs no other change for G1.

**With R1's crown, after G1 (ruling (i)):**
1. Grow F's net inside the triangle budget. try13 puts V01_M's LOD0 at 37 558 triangles and V01_L's at 44 988, over the 40 000.
2. Fit no shell on V01. The tree generator keeps the shell as an option for a closed crown (the sheet's V03 maple), switched off for V01, and the V01 audit then runs no shell rule. Gaps are bounded by capsules and twig records.
3. Write `radii_m` on every `twig` record, one radius per point (the ring radii). `meta.validate` refuses a `twig` record without it, so V13 is rebuilt with it in the same change.
4. Test that every twig record's first point lies within 0.01 mm of its parent member's axis (its record, and its capsule when the parent is wood) and of no other member's.
5. Print per V01 variant the twig records, their segments and the most segments meeting one 2 m cube, the numbers phase 3's cache and N-m read.

**With R2's belt edge, after G1 (ruling (iii)):**
1. Rule (b) on the leaf mass in `lib/audit.py` and `lib/shell.py`, with §B.6's two scenarios (items 11 and 12) as tests. V13's committed shells pass it unchanged, since they hold more than it asks.
2. V13's shell takes that rule if the band needs more bare twig than the committed fit holds (§B.4). The work reports and stops only if a variant's (d) passes 64, not at a cover.

**QA 2.2 (at G1):** R5's free hooks and their test, and §B.6's first group in the deltas and the sheet. The rest goes with the work after G1: V01 with no shell and its gaps bounded by capsules and records, the `radii_m` refusal, the unique parents, V13's (b) lines and their print of what is left to the records, and (d) per variant.

### B.6 Edits for the orchestrator (the code follows the spec)

Each edit gives the file, the old text and the new text. Then run `openspec validate p1-v01-locust --strict`. They come in two groups, so that the spec says what each build does: the first with task 1.9 at G1, the second with R1's and R2's work after G1 (D-040).

**First group, with task 1.9 (ruling (ii)):**

1. Asset-pipeline delta (`openspec/changes/p1-v01-locust/specs/asset-pipeline/spec.md`), "Asset metadata contract", the conventions.
   - Old: `` `snag[].kind` is `edge` or `hook`, each with at least two points, ``
   - New: `` `snag[].kind` is `edge` or `hook` (a `hook` is a free tip: no other member leaves its last point), each with at least two points, ``
2. Assets-vegetation delta (`openspec/changes/p1-v01-locust/specs/assets-vegetation/spec.md`), "V01 black locust belt tree", the snag clause.
   - Old: `every free tip a `hook`, one polyline record per twig through its rings),`
   - New: `every free tip (a member end from which no other member leaves) a `hook` and a crotch, such as a trunk's top where the limbs leave, no record of its own, one polyline record per twig through its rings),`
3. Vegetation sheet (`docs/object-sheets/vegetation.md`), Geometry, the snag rule (its open point was settled by `p1-v13-shrub` review-physics §4).
   - Old: `every free tip (a twig end, a splinter, a broken stub) is a `hook` from its base to its tip. The "whole crown shell" as a snag is not a record: it is the `twig` material's hook behaviour (open point for the physics engineer).`
   - New: `every free tip (a twig end, a splinter, a broken stub: a member end from which no other member leaves) is a `hook` from its base to its tip. A crotch, where members leave a member's end (a trunk's top at the first fork), is no record of its own: the members' capsules and records meet there, and a line pressed into it is held by both at once, a wedge the phase-7 fiber law resolves (`p1-v01-locust` review-physics §B.3). A crown shell is no snag: every twig the line can catch is a record (`p1-v13-shrub` review-physics §4).`
4. Vegetation sheet, §V01, Physics role, the snag line.
   - Old: `- **Snag hazards:** every capsule under 30 mm and the whole crown shell; thorns on suckers are visual only (cards).`
   - New: `- **Snag hazards:** every segment under 30 mm (an `edge`), every free tip (a `hook`) and every twig (one record through its rings); a crotch, such as the trunk's top, is no record of its own (Geometry, snag rule); thorns on suckers are visual only (cards).`
5. Requirements (`docs/requirements.md`), PHY-18.
   - Old: `ground and vegetation contact (hooks, twig capstans).`
   - New: `ground and vegetation contact (hooks, twig capstans, and the wedge of a fork: a node touching two members at once takes both contacts together).`

**Second group, with R1's crown and R2's belt edge after G1 (rulings (i) and (iii)):**

6. Asset-pipeline delta, "Asset metadata contract", the field list.
   - Old: `snag records, each with a kind, at least two points, a radius and the physics material of the member it follows.`
   - New: `snag records, each with a kind, at least two points, a radius and the physics material of the member it follows, and a record in the `twig` material also with the radius at each point.`
7. Asset-pipeline delta, "Asset metadata contract", the conventions (after edit 1).
   - Old: `a `radius_m` and a `material` (`bark` on stems, limbs and branches, the twig slot's physics on twigs);`
   - New: `a `radius_m` and a `material` (`bark` on stems, limbs and branches, the twig slot's physics on twigs), and a `twig` record also carries `radii_m`, one positive radius per point, its member's taper, which the phase-7 cantilever law reads (`p1-v01-locust` review-physics §B.2);`
8. Asset-pipeline delta, "Asset metadata contract", a new scenario after "A snag record without its material is rejected":
   ```
   #### Scenario: A twig record without its radii is rejected
   - **WHEN** a `twig` snag record carries no `radii_m`, or one whose length is not its points' or with a radius of 0 or less
   - **THEN** the build's validator rejects it, naming the record and the rule
   ```
9. Asset-pipeline delta, "Compliant shells in the collision audit", rule (b).
   - Old: `(b) every LOD0 vertex of a geometry slot (an opaque recipe, such as twig tubes) to lie inside the union or within 0.1 m outside it;`
   - New: `(b) every LOD0 vertex of a geometry slot (an opaque recipe, such as twig tubes) within the envelope tolerance of a card face of the shell's slots (the leaf mass) to lie inside the union or within 0.1 m outside it (a geometry vertex farther than that from every card face is bare wood, which the snag record on its member's axis carries: `p1-v01-locust` review-physics §B.2);`
10. Asset-pipeline delta, "Compliant shells in the collision audit", the print.
    - Old: `It SHALL print each piece's largest standoff and where it is, per slot the largest outside distance, and the largest count of pieces meeting one 2 m cube and where,`
    - New: `It SHALL print each piece's largest standoff and where it is, per slot the largest outside distance and the number of geometry vertices left to their records, and the largest count of pieces meeting one 2 m cube and where,`
11. Asset-pipeline delta, "Compliant shells in the collision audit", scenario "Twig outside the shell".
    - Old: `- **WHEN** one twig vertex lies 0.15 m outside the `twig` shell`
    - New: `- **WHEN** one twig vertex within 0.1 m of a leaf face lies 0.15 m outside the `twig` shell`
12. Asset-pipeline delta, "Compliant shells in the collision audit", a new scenario after "Twig outside the shell":
    ```
    #### Scenario: A bare twig is left to its record
    - **WHEN** every vertex of a bare twig lies 0.3 m or more from every leaf face and 0.25 m outside the `twig` shell
    - **THEN** rule (b) passes and the audit prints the twig's vertices as left to their records
    ```
13. Assets-vegetation delta, "V01 black locust belt tree", the crown's contact.
    - Old: `a `twig` shell of sphere and capsule pieces fitted per main limb (the fewest of 24 to 64 per fit, a limb split by its child subtrees when 64 do not pass) and audited per tree under the shell rule (standoff within 0.1 m, twigs within 0.1 m, leaf and pod cards within the tree's card allowance, 0.361 m (the 0.25 m card's far corner plus 0.1 m), at most 64 pieces meeting any 2 m cube), every twig a tube ringed at each kink at most 0.1 m apart, every inter-limb opening of 0.5 m or more as a gap polygon bounded by capsules and shell pieces and crossed by none,`
    - New: `no crown shell, since an open twig net is carried member by member by its records (`p1-v01-locust` review-physics §B.2: a shell over F's net, even over its leaves alone, takes 275 pieces and 85 meeting one 2 m cube at 0.3, and 78 or more at 0.6), every twig a tube ringed at each kink at most 0.1 m apart, every inter-limb opening of 0.5 m or more as a gap polygon bounded by capsules and twig records and crossed by none,`
14. Assets-vegetation delta, "V01 black locust belt tree", the twig record (after edit 2).
    - Old: `one polyline record per twig through its rings),`
    - New: `one polyline record per twig through its rings with its radius at each ring),`
15. Assets-vegetation delta, "V13 belt understory shrub" (MODIFIED), the record and shell clauses.
    - Old: `one snag record per twig (an `edge` where other twigs leave its end, a `hook` where its end is free, at its mean radius), the leafy mass as a `twig` shell of sphere or capsule pieces that follows the leaf and twig mass (the fewest`
    - New: `one snag record per twig (an `edge` where other twigs leave its end, a `hook` where its end is free, at its mean radius, with its radius at each point), the leafy mass as a `twig` shell of sphere or capsule pieces that follows the leaves and the twigs among them, every other twig left to its record (the fewest`
16. Assets-vegetation delta, "V13 belt understory shrub", scenario "Stems and shell carry the contact", two clauses.
    - Old: `each twig one record (`edge` where other twigs leave its end, `hook` where it is free, at its mean radius);`
    - New: `each twig one record (`edge` where other twigs leave its end, `hook` where it is free, at its mean radius, with its radius at each point);`
    - Old: `with every twig vertex inside it or within 0.1 m outside and every leaf-card vertex inside it or within 0.3 m outside,`
    - New: `with every twig vertex within 0.1 m of a leaf face inside it or within 0.1 m outside, every other twig left to its record, and every leaf-card vertex inside it or within 0.3 m outside,`
17. Assets-vegetation delta, "The H autumn belt state", the covers.
    - Old: `` with `leaf_cover` 0.3–0.5 for V01 and 0.3–0.5 for V13 (`V13_tall` cannot be shelled below 0.3; `V01_S` sits at the lowest cover its crown passes rule (d) at, measured and printed, unless task 1.3 thins it to pass at 0.3; `p1-v01-locust` review-physics §A.3) ``
    - New: `` with `leaf_cover` 0.3–0.5 for V01 and 0.1–0.5 for V13 (V01 has no shell; V13's shell over its leaf mass passes rule (d) at each cover, `V13_tall_autumn` 37–55 pieces at 0.1–0.4 against 62 at 0.4 when it held every twig, each variant's pieces and rule (d) printed; `p1-v01-locust` review-physics §B.4) ``
18. Vegetation sheet, Geometry, the twig rule.
    - Old: `- **Twigs 3–10 mm and leaves are cards** (leaf and twig atlases) inside the crown; they have no capsules. Their physics is the crown volume: a porous wind volume plus a compliant contact shell (`foliage` or `twig`, below). A card must never be the only thing that stops the drone.`
    - New: `- **Twigs 3–10 mm are geometry, leaves are cards.** A twig is a 3-sided tube ringed at every kink at most 0.1 m apart, with no capsule and one snag record through its rings with its radius at each ring: the record carries the fiber's bend and hook, the props' strike and, from phase 7, its push on the drone (`p1-v01-locust` review-physics §B.1–§B.2). Leaf cards sit inside the crown. The crown's other physics is a porous wind volume, and, only where the leaves make a closed mass (V13's clumps, a dense crown such as V03's), a compliant contact shell (`foliage` or `twig`, below) over the leaves and the twigs among them; an open twig net (V01) has no shell, and a bare twig is always its record's. A card must never be the only thing that stops the drone.`
19. Vegetation sheet, Geometry, the taper.
    - Old: `the last 10 mm segment ends in cards.`
    - New: `the last 10 mm segment ends in twigs.`
20. Vegetation sheet, Shared generator: trees, the `twig_threshold_mm` row.
    - Old: `below this a segment is a card (REAL-05 fixes 10; the range is for review only)`
    - New: `below this a segment is a twig: a 3-sided tube with one snag record and no capsule (REAL-05 fixes 10; the range is for review only)`
21. Vegetation sheet, Shared generator: trees, the outputs.
    - Old: `capsules for every segment ≥ 10 mm; a crown shell (convex pieces per main limb) with `twig` contact; wind volume parts (trunk column, crown pieces) with porosity; snag flags on every capsule under 30 mm;`
    - New: `capsules for every segment ≥ 10 mm; a crown shell (pieces per main limb) with `twig` contact only for a closed crown, none for an open twig net (V01); wind volumes (a trunk column under one crown box, never overlapping) with porosity; snag records by the rule above, one per twig with its radius at each ring;`
22. Vegetation sheet, §V01, Physics role, the collision and material lines.
    - Old: `crown shell per main limb with `twig` contact.`
    - New: `no crown shell: the open twig net is carried member by member by its records (`p1-v01-locust` review-physics §B.2).`
    - Old: `- **Material ids:** `bark` on trunk and limbs, else `timber`; crown shells `twig` or `foliage`.`
    - New: `- **Material ids:** `bark` on trunk, limbs and branches, else `timber`; `twig` on the twig records.`
23. Requirements, PHY-05.
    - Old: `Cantilever forces giving touchdown jitter and drag when skimming weeds; twigs as rollers; prop strikes logged, with the stem's cut energy (deferred X-6, decided in 7a's design).`
    - New: `Cantilever forces giving touchdown jitter and drag when skimming weeds; twigs as rollers; prop strikes logged, with the stem's cut energy (deferred X-6, decided in 7a's design). The same law carries an asset's twig records wherever no shell does (every record of an asset with no shell, such as V01; outside a shell's tolerance elsewhere): each a tapered cantilever through its `radii_m` from its parent at its first point, in series with the parent's chain; a record segment in a prop disc is a strike (`p1-v01-locust` review-physics §B.2).`

Design 4 and 9 and the proposal still describe V01's shell. They are the orchestrator's to bring in line with edits 2, 13 and 14 when those land, and the after-G1 work's as-built note replaces design 9's.

### B.7 Notes (non-blocking), by owner

- **N-k, physics-engineer (7d, the fiber): a crotch is a wedge, not a record** (§B.3).
  - A line node touching two members at once resolves both normals and frictions together, so the holding friction comes to μP/sin α and the line jams below α = atan μ (28.8° on bark).
  - Every V01 crotch measured here jams. The forks are recoverable from the metadata if the node placement wants apexes.
- **N-l, physics-engineer (7a, PHY-05): the twig records as stems** (§B.2).
  - Each record segment outside a shell's tolerance (on V01, every one) is a tapered cantilever from its parent, latched on its side at first touch by the part's swept volume and capped at slide-off, its `Id` keying the state.
  - A segment in a prop disc is a strike, with the cut energy (§B.1, X-6).
  - 7a also decides whether V13's shell gives way to the same law (§3.3 measured 4.7–11.6 N inside its tolerance band).
- **N-m, world-builder (phase 3, `veg_shells`, sharpening N-j): add a records case.**
  - The per-member narrow test (a part's swept volume against a twig-record segment) along a path through a belt of try13-density V01_M, against T 219 and the shells' 8.3 µs.
  - The compiler's density check counts record segments beside the shell pieces: 883 per 2 m cube in one try13 tree, 2 215 in its belt.
- **N-n, asset-artist (R1's crown, wind):** the sheet's crown porosity, 0.75 part-bare, is assumed, and F's net lowers it. Print the crown box's side-on coverage from LOD0 next to the porosity written (the wind grid reads porosity side-on, V13 reading N5), so the denser crown's wind volume follows what it grew.

### B.8 Evidence

```
$ git checkout unity/art/p1-v01-locust                       # 2fa7f20; no LFS content read (no FBX, no texture)
$ (Get-CimInstance -Namespace root/wmi -ClassName BatteryStatus).PowerOnline; powercfg /getactivescheme
False
Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)        # False at every check before the fits, True for a while
                                                                            # during them, other agents' jobs throughout: no timing taken

$ python -I net.py <repo> committed 0.6 belt      # the committed generator's skeleton and leaves, no fit; T at the 14-part layout
committed V01_M cover 0.6: members 254, twigs 194, wood 60, leaves 817 on 131 members          (the build's own counts)
  twig length 61.9 m, record segments 721; base diameters (mm: count) 3: 27, 4: 48, 5: 41, 6: 29, 7: 22, 8: 11, 9: 14, 10: 2
  twig ring centres beyond 0.1 m of every leaf face: 108 of 915; segments with an end there 101 (8.8 m of 61.9 m)
  bark capsules 256
  twig records, all segments: T 74 (yaw 0), 70 (yaw 45); outside the leaf mass: T 34, 36; bark capsules: T 56, 55
  twig record segments meeting one 2 m cube: 149
  belt 4x3 at 1.5 x 2.75 m: twig records T 87, 81; bark T 76, 74; record segments meeting one 2 m cube 477
$ python -I net.py <repo> try13 0.6 belt ; net.py <repo> try13 0.3      # orders 6, children_per_order 3
try13 V01_M cover 0.6: members 1313, twigs 1238, wood 75, leaves 2899 on 705 members
  twig length 293.9 m, record segments 3834; base diameters (mm: count) 3: 914, 4: 129, 5: 80, 6: 38, 7: 28, 8: 22, 9: 22, 10: 5
  twig ring centres beyond 0.1 m of every leaf face: 160 of 5072; segments with an end there 166 (13.7 m of 293.9 m)
  bark capsules 317
  twig records, all segments: T 166, 137; outside the leaf mass: T 44, 37; bark capsules: T 56, 55
  twig record segments meeting one 2 m cube: 883
  belt: twig records T 218, 219; bark T 66, 68; record segments meeting one 2 m cube 2215
try13 V01_M cover 0.3: leaves 1412 on 344 members
  twig ring centres beyond 0.1 m of every leaf face: 989 of 5072; segments with an end there 890 (69.5 m of 293.9 m)
  twig records, all segments: T 166, 137; outside the leaf mass: T 79, 71
$ python -I net.py <repo> spaced 0.6 belt          # children every 0.3 m along every member, orders 6 (a bound only)
spaced V01_M cover 0.6: members 7974, twigs 7068, wood 906, leaves 16582 on 3556 members
  twig length 2124.0 m, record segments 25821
  twig records, all segments: T 231, 204; outside the leaf mass: T 55, 48; bark capsules: T 101, 93
  twig record segments meeting one 2 m cube: 4372; belt: twig records T 595, 526; bark T 207, 205; per 2 m cube 15421

$ python -I -c "...tree_tubes + cards.add_to at LOD0..."     # LOD0 triangles, no pods
committed V01_M 13084, V01_L 15538 (the build's); try13 V01_M 37558, V01_L 44988; spaced V01_M 279346, V01_L 1097598

$ python -I metaT.py <repo> V01_S V01_M V01_L V01_M_autumn V13_tall V13_tall_autumn    # the committed metadata
V01_S: shell pieces 70: T 65, 67; twig record segments 521: T 110, 99
V01_M: shell pieces 130: T 58, 54; twig record segments 721: T 74, 69
V01_L: shell pieces 147: T 75, 71; twig record segments 592: T 54, 50
V01_M_autumn: shell pieces 160: T 47, 46
V13_tall: shell pieces 40: T 105, 103; twig record segments 273: T 146, 146
V13_tall_autumn: shell pieces 62: T 103, 100

$ python -I -c "...a 1 m segment along x, y, z against the twig records' boxes..."
committed V01_M: 8 / 8 / 11; try13: 18 / 15 / 18

$ python -I arith.py <repo>
1. d 3 mm, L 0.21 m: k tip 12.9 N/m, mid 103 N/m; s* 0.158 m, free beyond it 25% of L
   d 5 mm, L 0.35 m: k tip 21.5 N/m, mid 172 N/m; s* 0.313 m, 11%
   d 8 mm, L 0.56 m: k tip 34.3 N/m, mid 275 N/m; s* 0.586 m, 0%
   d 10 mm, L 0.70 m: k tip 42.9 N/m, mid 343 N/m; s* 0.789 m, 0%
2. fiber over d 3 / 5 / 8 / 10 / 30 mm: 2.92 / 1.78 / 1.12 / 0.90 / 0.30 GPa (4.2 / 2.6 / 1.6 / 1.3 / 0.4 x proof)
3. rotor energy 37 J, tip speed 109 m/s, blade passes 410/s
   d 3 mm: one cut 0.07 J = 0.19%; fed in 29 W      d 5 mm: 0.20 J = 0.53%; 81 W      d 10 mm: 0.79 J = 2.13%; 322 W
   (d 8 mm: 0.50 J = 1.36%; 207 W)
4. try13, 1238 twigs, 293.9 m: 82.6 m (28%) lies where 3 N bends the twig more than 0.1 m (taper kept, parent rigid)
5. V01_S: trunk top, each limb to its nearest neighbour 42-56 deg; wood forks, child off its parent 19-47 deg
   V01_M: 40-47 deg; 23-47 deg          V01_L: 49-55 deg; 23-47 deg
   half-angle 20 / 24 / 28.8 deg: tan 0.36 / 0.45 / 0.55; 1/sin 2.92 / 2.46 / 2.08; a quarter wrap on bark 2.37 -> 12.50 / 8.37 / 6.01
$ python -I -c "...a linear taper against a uniform cantilever at its mean radius..."
taper 0.5: at s = 0.1 L the mean-radius cantilever is 3.00 x too soft; at 0.5 L 2.37 x; at L 1.58 x
taper 0.6: 2.34 x; 1.95 x; 1.46 x

$ python -I snags.py <repo>         # which committed assets carry snag records, and their fields
L01 9-11 (edge, hook; cable, glass, steel, timber), L03 1-4 (edge; cable; some with edge_radius_m), V01 287-374, V13 226-321;
K01, E01, S04 none; every record: kind, material, points, radius_m
$ python -I narrow.py <repo> try13 0.3        # the committed fitter per limb, (b) on the leaf mass, split as tree._shells
limb 1: 301 leaves, 655 twig segments in the leaf mass, 383 twig ring centres left to their records; 58 pieces (47 capsules, 11 spheres) at cap 64, passes (b) and (c); most meeting one 2 m cube 34; fit 199 s
limb 2: 383 leaves, 752 twig segments in the leaf mass, 301 twig ring centres left to their records; 64 pieces (53 capsules, 11 spheres) at cap 64, FAILS (b) and (c); most meeting one 2 m cube 37; fit 721 s
limb 2: 64 pieces do not pass; split into its child subtrees
limb 2 child 8: 133 leaves, 245 twig segments in the leaf mass, 169 twig ring centres left to their records; 27 pieces (20 capsules, 7 spheres) at cap 32, passes (b) and (c); most meeting one 2 m cube 27; fit 4 s
limb 2 child 9: 123 leaves, 240 twig segments in the leaf mass, 94 twig ring centres left to their records; 24 pieces (16 capsules, 8 spheres) at cap 24, passes (b) and (c); most meeting one 2 m cube 24; fit 2 s
limb 2 child 10: 127 leaves, 267 twig segments in the leaf mass, 38 twig ring centres left to their records; 18 pieces (16 capsules, 2 spheres) at cap 24, passes (b) and (c); most meeting one 2 m cube 18; fit 2 s
limb 3: 410 leaves, 741 twig segments in the leaf mass, 227 twig ring centres left to their records; 64 pieces (51 capsules, 13 spheres) at cap 64, FAILS (b) and (c); most meeting one 2 m cube 49; fit 1641 s
limb 3: 64 pieces do not pass; split into its child subtrees
limb 3 child 11: 187 leaves, 305 twig segments in the leaf mass, 74 twig ring centres left to their records; 32 pieces (25 capsules, 7 spheres) at cap 32, passes (b) and (c); most meeting one 2 m cube 25; fit 16 s
limb 3 child 12: 120 leaves, 224 twig segments in the leaf mass, 127 twig ring centres left to their records; 18 pieces (16 capsules, 2 spheres) at cap 24, passes (b) and (c); most meeting one 2 m cube 18; fit 3 s
limb 3 child 13: 103 leaves, 212 twig segments in the leaf mass, 26 twig ring centres left to their records; 18 pieces (15 capsules, 3 spheres) at cap 24, passes (b) and (c); most meeting one 2 m cube 18; fit 4 s
limb 4: 318 leaves, 688 twig segments in the leaf mass, 206 twig ring centres left to their records; 64 pieces (51 capsules, 13 spheres) at cap 64, passes (b) and (c); most meeting one 2 m cube 52; fit 555 s
try13 V01_M cover 0.3: tree 259 pieces (206 capsules, 53 spheres); rule (d) 77 of 64; 3150 s
  ring centres in the tree's leaf mass: farthest outside the union 241.8 mm; 47 beyond 0.1 m
  residual pass: 16 pieces (11 capsules, 5 spheres) at cap 24, passes (b); fit 14 s; tree now 275 pieces, rule (d) 85 of 64; leaf-mass ring centres beyond 0.1 m 0, farthest 96.3 mm
$ python -I narrow.py <repo> try13 0.6 V01_M 1,2 / 3 / 4    # one process per limb, the pieces saved; limb 2 stopped
limb 1: 713 leaves, 924 twig segments in the leaf mass, 62 twig ring centres left to their records; 64 pieces (57 capsules, 7 spheres) at cap 64, passes (b) and (c); most meeting one 2 m cube 33; fit 789 s
limb 3: 734 leaves, 912 twig segments in the leaf mass, 35 twig ring centres left to their records; 64 pieces (55 capsules, 9 spheres) at cap 64, FAILS (b) and (c); most meeting one 2 m cube 48; fit 1922 s
limb 3: 64 pieces do not pass; split into its child subtrees
limb 3 child 11: 296 leaves, 357 twig segments in the leaf mass, 20 twig ring centres left to their records; 32 pieces (25 capsules, 7 spheres) at cap 32, passes (b) and (c); most meeting one 2 m cube 24; fit 42 s
limb 3 child 12: 247 leaves, 321 twig segments in the leaf mass, 13 twig ring centres left to their records; 27 pieces (26 capsules, 1 spheres) at cap 32, passes (b) and (c); most meeting one 2 m cube 27; fit 26 s
limb 3 child 13: 191 leaves, 234 twig segments in the leaf mass, 2 twig ring centres left to their records; 15 pieces (14 capsules, 1 spheres) at cap 24, passes (b) and (c); most meeting one 2 m cube 15; fit 6 s
limb 4: 709 leaves, 855 twig segments in the leaf mass, 21 twig ring centres left to their records; 64 pieces (59 capsules, 5 spheres) at cap 64, passes (b) and (c); most meeting one 2 m cube 55; fit 981 s
$ python -I -c "...tree._neighbourhood over the saved pieces of limbs 1, 3 and 4..."
limbs 1, 3, 4 at 0.6: 202 pieces; rule (d) over their union 78 of 64
$ python -I narrow.py <repo> committed 0.3 V01_S
committed V01_S cover 0.3: tree 44 pieces (25 capsules, 19 spheres); rule (d) 39 of 64
  ring centres in the tree's leaf mass: farthest outside the union 264.6 mm; 19 beyond 0.1 m
  residual pass: 5 pieces (4 capsules, 1 spheres) at cap 24, passes (b); tree now 49 pieces, rule (d) 44 of 64; leaf-mass ring centres beyond 0.1 m 0, farthest 95.8 mm

$ python -I v13.py <repo> V13_tall_autumn 0.4 committed        # the build's fit reproduced
V13_tall_autumn leaf_cover 0.4: LOD0 twig faces 819, leaf faces 1571
  committed rule (b), every twig: 62 pieces (45 capsules, 17 spheres) at cap 64, passes (b) and (c); rule (d) 62; fit 423 s
$ for c in 0.4 0.2 0.1 0.3: python -I v13.py <repo> V13_tall_autumn $c narrowed     # (b) on the leaf-mass vertices
0.4: 1502 of 1638 twig vertices in the leaf mass; 47 pieces (43 capsules, 4 spheres) at cap 48, passes (b) and (c); rule (d) 47; fit 166 s; farthest outside 97.4 mm
0.2: 1257 of 1638; 55 pieces (49 capsules, 6 spheres) at cap 56, passes; rule (d) 55; fit 288 s; farthest outside 97.9 mm
0.1: 740 of 1638; 37 pieces (34 capsules, 3 spheres) at cap 40, passes; rule (d) 37; fit 21 s; farthest outside 95.8 mm
0.3: 1413 of 1638; 48 pieces (40 capsules, 8 spheres) at cap 48, passes; rule (d) 48; fit 199 s; farthest outside 97.6 mm
```
