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
