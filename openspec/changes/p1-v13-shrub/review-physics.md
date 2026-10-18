# Physics review: the V13 shell, hook and snag metadata

**Reviewer:** physics-engineer · **Task:** 1.5 · **Branch:** `unity/art/p1-v13-shrub` · **From tip:** `abd2e6c`
**Against:** AST-05, REAL-05, PHY-05, PHY-18, PHY-19, PHY-24; the vegetation sheet's group rules (Geometry, Snag records) and §V13 (physics role); the `twig` and `bark` rows; QA's R1 and suggestion 4; the asset-pipeline delta "Compliant shells in the collision audit"
**Read:** this change's proposal, design (4, 7), tasks, `review.md` and both spec deltas; `docs/object-sheets/vegetation.md` (group rules, §V13); `assets-src/physics_materials.json` and the archived `p1-pipeline-hardening` change's `review-materials.md` (§1 rules 1–5, §3); the archived `setup-asset-pipeline` design decision 6; `V13_{low,mid,tall}.assetmeta.json`; `tools/blender/generators/shrub.py`, `tools/blender/lib/{skeleton,cards,audit}.py`; knowledge §5–§8, §10; `docs/legacy/map-and-world/surfaces-review.md` (§6.4, X-2)

I am the consumer of these numbers: the 1 kHz contact law (4 feet, 4 arms, 4 prop discs, body and coil against the shell pieces and the capsules), the fiber's catch, bend and cut-risk model (PHY-18, PHY-19), prop strikes (PHY-05), the wind grid that reads the wind volume, and the log. Every number is labelled **pilot / measured / published / derived / assumed**. "Measured" here means measured on the rebuilt V13 geometry: a plain-Python rebuild from the committed params equals the committed collision for all three variants. The scripts lived in a scratch folder, now deleted; §8 has the commands' output.

## Verdict

The capsules are right: `bark`, one per stem chord, at each segment's mean radius. So are `gaps: []` and "nothing landable". Four things are wrong, and task 1.6 fixes them (§6):
- the one bounding sphere (QA's R1)
- the single base radius on each stem's hook
- the 3–8 mm twigs, which have no snag records
- the wind box, which reaches down to the ground

What this reading settles:
1. **Envelope tolerance: 0.1 m** (derived, §2). It is measured from the shell's surface to the nearest **face** of its slots. The enclosure direction changes too: the delta's "every leaf and twig vertex within 5 cm" cannot hold together with 0.1 m on these crowns. Twig geometry stays within 0.1 m of the shell, and leaf cards within 0.3 m of it.
2. **Pieces: spheres and capsules only, at most 24 per variant, overlaps allowed** (§3). Hidden interior pieces are allowed too. The bare-stem zone follows from rule 1 and needs no rule of its own. V01 uses the same rules.
3. **Snag records** (§4):
   - **Stems:** an `edge` per stem segment and a `hook` on the top segment, each at its capsule's radius.
   - **Twigs:** every twig gets a record, an `edge` where other twigs leave its end and a `hook` where its end is free, at its mean radius.
   - **The sheet's open point is settled: records, not a material behaviour.**
4. **Wind box over the leafy mass only** (§5). Porosity 0.35 stays.

## 1. What reads this metadata

| Consumer | Reads | From V13 |
|---|---|---|
| Contact law, drone parts | collision shapes; a `twig` piece is a spring at 30 N/m, ζ 0.2, on the depth into the piece; a `bark` capsule is rigid, in series with the part's compliance | 42 capsules, the shell pieces |
| Fiber (≤ 32 nodes) | capsules (the line bends over the capsule's own radius); snag records (catch, slide off a free end, bend radius, capstan); the wind volume (drag in a porous mass) | capsules, records, wind box; **not the shell** (§4) |
| Prop strikes | any capsule or record entering a prop disc | capsules, records |
| Wind grid | the wind volume's base, top and porosity (`BaseM` gives the jet under a crown) | one box |
| Log | object, shape index, material, record index for hooks and strikes | all |

## 2. The envelope tolerance: 0.1 m (derived)

A shell surface standing a distance s off the visible mass pushes the drone with k·s before anything visible is touched. k is the `twig` row's 30 N/m (derived: one 10 mm green twig, 0.8 m long). The drone weighs 47.1 N (4.8 kg nominal, derived, knowledge §5.2).

| Standoff s | Force at the visible surface | Share of weight | Same as a lean of | Yaw moment on one prop (0.16 m arm) |
|---|---|---|---|---|
| 0.05 m | 1.5 N | 3.2 % | 1.8° | 0.24 N·m |
| **0.10 m** | **3.0 N** | **6.4 %** | **3.6°** | 0.48 N·m |
| 0.20 m | 6.0 N | 12.7 % | 7.3° | 0.96 N·m |
| 0.78 m (`V13_mid` as built) | 23.4 N | 49.7 % | 26.4° | 3.74 N·m |

What sets 0.1 m:
- **The invisible-wall threshold.** Under a steady push F the drone stops at F/k − s inside the leaves. It stops outside them, against an invisible wall, whenever F < k·s.
  - At s = 0.1 m that threshold is 3 N, a 3.6° lean.
  - The sheet's "0.2–0.5 m into a clump" is 6–15 N at 30 N/m, a 7–18° lean. So every deliberate push into a bush ends inside the leaves.
  - Example: a 15° push (12.6 N) stops 0.32 m into the leaves at s = 0.1 m. On `V13_mid` as built, the same push stops 0.36 m outside them.
- **The stiffness's own spread.** The sheet gives the twig band as 10–50 N/m. At its shallowest push depth (0.2 m) that spread is ±20 N/m × 0.2 m = ±4 N. A boundary error is a force error of 30·s, so it stays inside that spread for s ≤ 0.13 m. A tighter tolerance buys nothing the stiffness itself can hold.
- **Buildable:** §3 measures it.

**Why R1 is severe, in the drone's terms.** A shell force on one prop disc is a yaw moment, and yaw is the drone's weak axis. Full yaw authority at hover is 0.71–0.94 N·m (κ 0.015–0.02 N·m/N, assumed, knowledge §5.3). The as-built 0.78 m standoff gives 3.7 N·m at the first visible touch: four times the authority, from air.

**What is measured, and the enclosure direction:**
- **Standoff, measured to faces.** It runs over the union of the pieces' surfaces above the ground (a point inside another piece of the same material is not surface), to the nearest **face** of the shell's slots: card triangles and twig tubes.
  - Faces, not vertices: a twig tube 0.3–0.9 m long has vertices only at its two ends. A piece that hugs the middle of a twig would read 0.2–0.4 m off by the vertex metric.
  - The audit already computes point-to-face distances for the mesh direction.
  - On the bounding shapes below, faces and vertices give the same worst value.
- **The delta's two directions conflict.** "Every leaf and twig vertex inside or within 5 cm" and "every surface point within 0.1 m" cannot both hold on these crowns.
  - The crowns' undersides are ragged. In the two lowest 0.2 m bands, the outer radius varies by up to 0.61 / 0.77 / 0.73 m between 30° sectors (low / mid / tall, measured). In the bands above, it varies by 0.06–0.30 m, mostly about 0.1 m.
  - Even the generator's own envelope (the ellipsoid the twigs grow to), scaled until it encloses every vertex, stands **0.43 / 0.34 / 0.43 m** off the mass at worst.
  - Unions that enclose everything do no better. The 24 enclosing spheres of spatial clusters stand 0.38 m off (`V13_mid`).
- **Physics decides which direction gives way.** A standoff is a force where nothing is. A leaf sprig outside the shell is foliage mass with no contact role: REAL-05 says the crown's physics is "its wind volume plus twig and branch snags". Its twig is a record (§4). So:
  - **geometry slots** of a shell material (opaque recipes; here the `twig` slot, the 3–8 mm tubes): every vertex inside the union or within **0.1 m** outside. The stiff mass is held to the same tolerance as the standoff.
  - **card slots** (alpha-clipped recipes; here `leaf`): every vertex inside or within **0.3 m** outside. That is `card_m` 0.2 m plus 0.1 m: a sprig never hangs farther than its own length off the twig mass.
  - The rule still fails the known-wrong models:
    - the bounding sphere fails the standoff (0.74 m)
    - a shell that misses one side, or a small core, leaves twig vertices far beyond 0.1 m outside
    - a sprig whose twig was left out lies beyond 0.3 m

## 3. Pieces

**Accepted shapes: spheres and capsules,** material `twig`, not landable.
- Their surface normals are continuous, so the push of a soft shell turns smoothly as a part moves inside it.
- Their distance tests are the two cheapest closed forms.
- A box or cylinder would give the drone a flipping normal at its edges and the fiber an arris (`edge_radius_m`), and a crown has neither.

**At most 24 pieces per variant** (derived from assumed costs; the phase-3 worldbench measures them).
- The budget:
  - The world-query share of a worst-case step is 60 µs. The first attempt measured 34.7 µs median for the batch (knowledge §6.11), which leaves about 25 µs.
  - Shells near the drone get about a third of that, 8–9 µs (assumed allocation).
- The load:
  - A drone inside a belt has about 3 clumps in reach (clumps 1–4 m apart and 1.5–2.5 m wide, per the sheet's placement).
  - 14 drone parts test each clump. The fiber does not read V13 shells (§4).
- The cost:
  - per (part, piece): a box reject of about 5 ns, plus about 30 ns for each of the about 3 pieces that overlap a part (assumed)
  - per step: 3 × 14 × (5K + 90) ns, which gives 8.8 µs at K = 24
- The 42 stem capsules already cost about as much.
- If `V13_tall` cannot meet §2 within 24, the asset-artist reports the counts and stops. The physics engineer then revisits the cap, not the tolerance.

**Overlaps are allowed.** The contact law (phase 7) takes, for each object and drone part, the deepest `twig` piece only.
- The maximum of the pieces' depths is continuous, so the force has no jump at a seam.
- Summing the pieces would double the stiffness where they overlap.

**Hidden interior pieces are allowed.** They lie inside the union, so the standoff rule never sees them. They close interior holes, where a drone part deep in the clump would otherwise feel nothing.

**The bare-stem zone needs no separate rule.** No shell surface may stand more than 0.1 m off a leaf or twig face, so no piece reaches more than 0.1 m below the lowest one. The zone below is the capsules' alone. A piece may run under the ground (the low variant): only its surface above the ground is measured.

**How to fit them** (guidance, not a rule):
- Twig groups do not make good clusters. Each first-order twig, with its second-order twigs and cards, is a radial frond from the stem to the envelope, so clusters of fronds give clump-sized pieces. Measured: 4–16 such spheres or capsules stand 0.30–0.91 m off.
- Spatial clusters of the leaf and twig mass work. Each piece is moved inward and then cut until its exposed surface keeps to 0.1 m, and the vertices still uncovered are covered greedily.
- Measured with 24 such spheres:

| Variant | Standoff, vertex metric (max) | Twig ends within 0.1 m | Leaf corners within 0.3 m |
|---|---|---|---|
| low | 0.115 m | 100 % | 100 % |
| mid | 0.120 m | 95.5 % (max 0.24 m) | 100 % |
| tall | 0.115 m | 82 % (max 0.43 m) | 99.2 % (max 0.62 m) |

  - What this crude fit shows, and what it does not:
    - It holds the standoff near 0.1 m but not under it: 0.115–0.120 m at 1 200 samples per piece, because its own check used only 400 directions. That is by the vertex metric; the face metric reads no more.
    - Rule (c) of PR2 holds on low and mid. On tall it reaches 99.2 % (max 0.62 m).
    - Rule (b) holds on low. On mid it reaches 95.5 % (max 0.24 m), and on tall 82 % (max 0.43 m).
  - About 40 % of the tall's misses are interior holes this fitter leaves; hidden core pieces close them. The rest is the ragged underside: hanging twig groups, which a capsule along each one covers in both directions.
  - So the rules are tight for mid and tall. 1.6 needs a better fitter than this one (core pieces, capsules on the fringe), and the cap carries the report-and-stop clause above.

**V01's crown.** The same tolerance, measure, shapes, overlap rule and hidden pieces apply.
- Pieces go per main limb, split along the limb as the tolerance needs.
- The cost bound is per neighbourhood: at most 24 pieces whose boxes meet any 2 m cube.
- V01's own reading checks its fit.

## 4. Snag records

### Stems
**As built:** 14 `hook`s per variant, base (y 0) to tip through the segment ends, `radius_m` 9 mm (half the 18 mm base).

**The trip is the radius.** A snag's `radius_m` overrides the shape's own (`review-materials.md` §1 rule 4). So the fiber reads 9 mm along the whole stem, while the capsules are 8.33 / 7.0 / 5.67 mm and the tip is 5 mm (measured). The bend stress is E·r/(R + coating), with E 73 GPa, r 62.5 µm and coating 62.5 µm (published; the legacy formula):

| Where on the stem | R | Bend stress | Against the 0.69 GPa proof test (published) |
|---|---|---|---|
| hook as built | 9.0 mm | 0.50 GPa | below |
| capsule 1 | 8.33 mm | 0.54 GPa | below |
| capsule 2 | 7.0 mm | 0.65 GPa | below |
| capsule 3 | 5.67 mm | 0.80 GPa | **above** |
| tip | 5.0 mm | 0.90 GPa | **above** |
| a 30 mm member | 15 mm | 0.30 GPa | below |

The record moves the stem's top third from above proof to below it. On V13 the line could never be at risk from a stem by bending, yet the real top third does put it at risk. The table also confirms the sheet's 30 mm edge threshold: thicker members stay far below proof.

**Ruling: the sheet's rule as written.**
- Each stem segment below the top is an `edge` along its capsule's chord, with that capsule's radius.
- The top segment, which ends free, is a `hook` from its lower end to the stem tip, with its capsule's radius.
- The records share their end points, so the chain from the root on the ground to the tip survives (§7, N3 needs it).
- One hook per stem cannot carry a stem that tapers from 18 to 10 mm, because a hook has one radius. A radius per point would need a schema change for what per-segment records already carry.

### Twigs (3–8 mm)
**As built:** no records. The skeleton has 185 / 155 / 267 twigs (low / mid / tall; first-order 63 / 52 / 89, second-order 122 / 103 / 178), built as 3-sided LOD0 tubes and carried only by the shell.

**The open point, settled: records, not the `twig` material's hook behaviour.**
- REAL-05 makes the crown's physics "its wind volume plus twig and branch snags". These twigs are visible geometry, so the line must catch on the twigs the pilot sees.
- A material behaviour would put catches where no twig is: either drawn, which PHY-24 forbids for the world, or hashed, which the renderer does not show.
- `review-materials.md` §3 already declined a hook-rate field for `twig` and left crown hooking to the records.
- They are the fiber's worst case. Second-order twigs (5 → 3 mm, mean radius 2.0 mm) give 2.21 GPa and first-order ones (8 → 5 mm) 1.38 GPa, both above proof. The stem hook as built gives 0.50 GPa.
- PHY-05 needs them: a record that enters a prop disc is a logged strike. Without them, a drone pushing into a clump strikes nothing but stems.

**The rule:** every twig gets one record, from its base to its end.
- A twig from whose end other twigs leave (a first-order twig with children) is an `edge`.
- A twig with a free end is a `hook`. That means every second-order twig, and any first-order twig with none (1 on low, 0 on mid and tall).
- `radius_m` is the twig's mean radius, (r0 + r1)/2. A capsule takes its segment's mean the same way.
  - Derived. At the tip of a 5 → 3 mm twig, the true stress is 1.33× what the mean gives (2.92 against 2.21 GPa).
  - That gap is inside what the per-flight strength draw spans.

**Counts:** stems 28 edges + 14 hooks, plus the twigs. That is **227 / 197 / 309 records** (low / mid / tall).

**How the consumer reads them** (phase 7; recorded so 1.6 builds the right thing):
- Records add no contact force to drone parts; the shell stands for the twigs' bulk. Records give hooks on legs and on the line, the line's bends and capstans, and prop strikes.
- **The line takes no contact from a V13 shell.** Every twig it can catch is a record, and its drag inside the clump is the wind volume's porosity (`review-materials.md` §3). The `twig` row's 2.5 mm edge stays for crowns whose fine twigs are cards. V01's reading decides how the line reads such a shell.
- A record takes the material of the capsule whose axis it lies on, else of the shell that encloses it (`twig` here). The compiler resolves this, so no field is needed.

## 5. The rest of the metadata

- **Capsules:** right.
  - `bark`, at most 12.5 mm off their tubes (QA).
  - Each first capsule's cap reaches its radius (8.6–8.9 mm, measured) under the ground. **Keep it.** A real stem enters the soil, and a cap resting on the ground would leave a notch the line could slip under. Slopes: N1.
- **Wind box: required change.**
  - As built, it runs from y 0 to the top at porosity 0.35. So the bare-stem zone (0–0.82 m on mid, 0–1.25 m on tall) blocks wind like leaves.
  - The grid takes a crown's base from its volume, and the wake model's jet under a crown needs that base. B reads 0 instead of 0.82 / 1.25 m.
  - The stem zone on its own: 14 stems of about 17 mm across a 2.1 m clump give solidity 0.11, porosity 0.89 (derived). That is close to no obstacle, so leaving it out is accepted.
- **Landable: none, right.** The whole weight on a 30 N/m shell sinks 1.57 m; the legs drop through, as the sheet says. Rigid stems can still perch the drone: N3.
- **Gaps: none, right.** QA measured the largest clear space inside a clump at 0.22–0.37 m, under the 0.5 m writing threshold.
- **Slot physics** (`twig` on the `twig` and `leaf` slots, `bark` on `stem`): right.

## 6. Required changes for task 1.6

- **PR1. Shell pieces** (`lib/cards.py` / `lib/skeleton.py` or a shell module shared with V01).
  - Replace the one `twig` sphere with `twig` pieces:
    - spheres and capsules only, at most 24 per variant, none landable
    - overlaps and hidden interior pieces allowed
    - pieces may run under the ground
  - They must meet PR2's three rules, so fit them to the spatial clusters of the leaf and twig mass, not to the twig groups (§3).
  - The build prints each variant's piece count and kinds.
  - Test: the count is at most 24, the kinds are in {sphere, capsule}, and every piece is `twig` and not landable.
- **PR2. The shell rule in `lib/audit.py`,** for each shell material:
  - **(a) Standoff:** every point of the union's surface above the ground (not inside another piece of the material) lies within `ENVELOPE_TOLERANCE_M` = 0.1 m of the nearest face of the material's slots.
  - **(b) Twig geometry:** every LOD0 vertex of a geometry slot (opaque recipe) lies inside the union or within 0.1 m outside it.
  - **(c) Cards:** every LOD0 vertex of a card slot (alpha-clipped recipe) lies inside the union or within `CARD_ALLOWANCE_M` = 0.3 m outside it.
  - Print each piece's largest standoff and where it is, and per slot the largest outside distance. Fail beyond each tolerance, naming the piece or slot, the distance and the tolerance.
  - Tests:
    - a piece 0.3 m too large fails (a)
    - a twig vertex 0.15 m outside fails (b)
    - a card corner 0.35 m outside fails (c), and one 0.25 m outside passes
    - two overlapping spheres that hug a mass pass
    - surface under the ground is ignored
    - the real `V13_mid` with its committed sphere fails (a)
- **PR3. Stem records.** Per stem, one record per capsule segment on the same chord:
  - the lower segments are `edge`s, and the top one is a `hook` from its lower end to the stem tip
  - `radius_m` equals that capsule's radius
  - Test: per stem, the records' end points equal the capsules' axis end points (as rounded), their radii equal the capsule radii, the first point lies on the ground, and the last record is a `hook` ending at the tip.
- **PR4. Twig records.** One record per twig from `p0` to `p1`:
  - an `edge` if another twig leaves its end, a `hook` otherwise
  - `radius_m` = (r0 + r1)/2
  - Test: the record count is 3 × stems + twigs (227 / 197 / 309 at the committed params), and each twig's record matches its tube's axis, kind and radius.
- **PR5. Wind volume.**
  - The box spans the LOD0 vertices of the `twig` and `leaf` slots: horizontally their extent, vertically from their lowest vertex to the top.
  - Porosity stays 0.7 − 0.35 · `leaf_cover`.
  - Test: the box's bottom equals the lowest leaf or twig vertex (0.82 m on `V13_mid` and 1.25 m on `V13_tall` at the committed params; measured).

**Spec deltas, orchestrator** (the code follows the spec):
- **S1, assets-vegetation (V13 requirement and its "Stems and shell carry the contact" scenario).**
  - "a `hook` snag from base to tip" / "a `hook` snag of half the stem's diameter" becomes PR3's stem records plus PR4's record per twig.
  - "enclose every leaf and twig vertex within 5 cm and whose outer surface stands within 0.1 m" becomes PR2's (a), (b) and (c).
  - Add: the wind volume spans the leafy mass from its lowest leaf or twig.
- **S2, asset-pipeline ("Compliant shells in the collision audit").**
  - The enclosure clause becomes (b) and (c).
  - The surface-to-mass distance is measured to the nearest face, over the union's surface.
  - The tolerance is 0.1 m, labelled derived in this review.
  - The scenario "Card outside the shell" (8 cm fails) becomes two: a twig vertex 0.15 m outside fails, and a card corner 0.35 m outside fails.

## 7. Notes (non-blocking), by owner

- **N1, world-builder (phase-3 placement).** Rooted assets on slopes: a stem at the edge of the 0.15–0.25 m base disc stands up to 44 mm above the ground on the downhill side of a 10° slope (derived). That is a stem on air, which the line can pass under. Sink rooted assets by base radius × tan(slope), or snap stems to the ground.
- **N2, world-builder (phase-3 compiler).** Take each object's broadphase box from its collision shapes and records, not from `bounds`. Shell pieces stand up to 0.1 m beyond the mesh, and capsule caps go 9 mm under the ground.
- **N3, physics-engineer (materials row and phase-7 contact law).**
  - Slender `bark` members are not rigid. A tapered 18 → 10 mm stem with E 10 GPa (published 8–12) is a cantilever of:
    - 1 075 N/m at 0.5 m
    - 114 N/m at 1.0 m and 28 N/m at 1.5 m (mid)
    - 13 N/m at 2.0 m (tall)
  - So at prop height a stem is as soft as the shell (derived). As rigid capsules, the stems stop the drone dead and can perch it on their tips. One leg on a mid tip (17.5 N/m) under a quarter of the weight would fold the stem over by 0.67 m.
  - The fix: `elastic_modulus_pa` on `bark`, and the contact law treats a member under 30 mm as a cantilever from its root, found through PR3's record chain. Nothing more is asked of the asset.
- **N4, physics-engineer (phase-7 fiber law).** §4's reading rules: records add no part force; the line ignores V13 shells; a record's material comes from its capsule, else its shell. V01's card-twig shells are V01's reading.
- **N5, physics-engineer (phase-3 wind grid).**
  - Porosity on a box over a dome: the grid reads porosity side-on of the volume. A dome fills about π/4 of its box, so a 0.35 dome is a 0.49 box (derived).
  - The sheet's 0.35 on the box agrees with QA's measured 0.69 fill of the projected width at 1–2 m on `V13_mid` (porosity 0.31), so it stays. Settle the definition with the grid.
- **N6, orchestrator (`p1-v01-locust` design 4).**
  - Change "a `hook` per capsule under 30 mm" to the sheet's rule (PR3): an `edge` per segment under 30 mm and a `hook` per free tip.
  - Shells follow this reading.
  - Its crown wind pieces ("a box per main limb") must not overlap, because the grid multiplies porosities where volumes overlap. Use non-overlapping parts or one volume.
- **N7, world-builder and physics-engineer (phase-3 world-query API).** About 200–300 records per clump reach the step. Serve them like micro-detail, prefetched into the 0.25 m local grid near the drone. The meadow already tests 100–300 elements per primitive (surfaces-review §6.4).

## 8. Evidence

```
$ git checkout unity/art/p1-v13-shrub && git lfs checkout && git log --oneline -1
Checking out LFS objects: 100% (22/22), 52 MB | 0 B/s, done.
abd2e6c docs(openspec): answer the v13 qa pass with a physics reading and r1

$ python -I facts.py          # plain-Python rebuild of the committed params, against the committed metadata (excerpt)
== V13_low: stems 14, twigs L1 63 L2 122, cards 1838, capsules 42, shells 1, snags 14
  rebuild matches committed collision: True
  capsule radii mm [5.667, 7.0, 8.333]; lowest capsule point ~-8.6 mm (axis starts at y 0)
  snag kinds ['hook'], radius_m [0.009]
  single sphere: centre y 0.514 r 0.775, spans y -0.26..1.29; leaves+twigs y 0.01..1.21; worst surface-to-mass 0.435 m
  wind box y 0.00..1.21, porosity 0.35
== V13_mid: stems 14, twigs L1 52 L2 103, cards 3193, capsules 42, shells 1, snags 14
  rebuild matches committed collision: True
  single sphere: centre y 1.257 r 1.107, spans y 0.15..2.36; leaves+twigs y 0.82..2.03; worst surface-to-mass 0.743 m; below lowest leaf 0.67 m
  wind box y -0.00..2.03, porosity 0.35
== V13_tall: stems 14, twigs L1 89 L2 178, cards 3927, capsules 42, shells 1, snags 14
  rebuild matches committed collision: True
  single sphere: centre y 1.875 r 1.272, spans y 0.60..3.15; leaves+twigs y 1.25..3.00; worst surface-to-mass 0.712 m; below lowest leaf 0.65 m
  wind box y 0.00..3.00, porosity 0.35
(QA's own sampling: 0.47 / 0.78 / 0.75 m; the same defect)

$ python -I floor.py          # the generator's envelope, scaled to enclose every vertex
V13_low: envelope rw 0.79 up 0.76 down 0.65 (waist y 0.48); to vertices max 0.425 p99 0.369 p95 0.302; to faces max 0.425 p99 0.369 p95 0.302 m
V13_mid: envelope rw 1.16 up 0.88 down 0.36 (waist y 1.16); to vertices max 0.343 p99 0.311 p95 0.286; to faces max 0.343 p99 0.311 p95 0.286 m
V13_tall: envelope rw 1.29 up 1.24 down 0.70 (waist y 1.80); to vertices max 0.430 p99 0.386 p95 0.325; to faces max 0.430 p99 0.386 p95 0.325 m

$ python -I where.py          # outer radius per 0.2 m band and 30-degree sector; summary of the spreads it printed
V13_low  spread by band, bottom up: 0.61 0.46 0.09 0.06 0.13 0.09 0.14
V13_mid  spread by band, bottom up: 0.77 0.44 0.13 0.09 0.10 0.12 0.33 (the top band holds a few vertices)
V13_tall spread by band, bottom up: 0.68 0.73 0.30 0.17 0.13 0.14 0.13 0.13 0.20 0.19

$ python -I pieces.py 1,4,8,12,16      # enclosing spheres / capsules of twig-group clusters (V13_mid, two lines of 30)
  K 12 sphere : pieces 12, radius 0.46-0.72 m, outside   0.0 mm, surface-to-mass max 0.451 p99 0.402 p95 0.367 m, below the lowest leaf 0.30 m
  K 12 capsule: pieces 12, radius 0.45-0.96 m, outside   0.0 mm, surface-to-mass max 0.794 p99 0.736 p95 0.622 m, below the lowest leaf 0.58 m
  (over all variants and K 4-16 the max runs 0.297-0.906 m)
$ python -I pieces2.py 8,12,16,24 V13_mid   # enclosing spheres of spatial clusters (one line of 4)
V13_mid K 24: radius 0.31-0.47, max 0.379 p99 0.304 p95 0.229 p90 0.194, share > 0.1 m 0.49; ...
$ python -I pieces6.py 12 24 0.1       # spatial clusters, moved then cut to a 0.1 m standoff, greedy cover, 24 spheres
  twig ends outside: within 5 cm 0.995, within 0.1 m 1.000, p99 0.033, max 0.062; leaf corners: within 0.2 m 0.997, within 0.3 m 1.000, p99 0.153, max 0.233 m
V13_low K0 12 pieces 24 T 0.10: radius 0.36-0.50, surface-to-mass max 0.115 p99 0.105; outside: within 5 cm 0.943 (of the rest 0.01 interior), within 0.2 m 0.997, p99 0.148, max 0.233 m
  twig ends outside: within 5 cm 0.932, within 0.1 m 0.955, p99 0.200, max 0.243; leaf corners: within 0.2 m 0.996, within 0.3 m 1.000, p99 0.155, max 0.285 m
V13_mid K0 12 pieces 24 T 0.10: radius 0.12-0.47, surface-to-mass max 0.120 p99 0.095; outside: within 5 cm 0.919 (of the rest 0.21 interior), within 0.2 m 0.996, p99 0.164, max 0.285 m
  twig ends outside: within 5 cm 0.753, within 0.1 m 0.824, p99 0.397, max 0.432; leaf corners: within 0.2 m 0.973, within 0.3 m 0.992, p99 0.282, max 0.619 m
V13_tall K0 12 pieces 24 T 0.10: radius 0.04-0.54, surface-to-mass max 0.115 p99 0.095; outside: within 5 cm 0.795 (of the rest 0.40 interior), within 0.2 m 0.969, p99 0.292, max 0.619 m

$ python -I arith.py                   # excerpt
  stem base, hook as built     R  9.000 mm  sigma  0.50 GPa  below proof 0.69
  stem seg 3 capsule           R  5.667 mm  sigma  0.80 GPa  ABOVE proof 0.69
  stem tip (10 mm)             R  5.000 mm  sigma  0.90 GPa  ABOVE proof 0.69
  L1 twig mean 8->5 mm         R  3.250 mm  sigma  1.38 GPa  ABOVE proof 0.69
  L2 twig mean 5->3 mm         R  2.000 mm  sigma  2.21 GPa  ABOVE proof 0.69
  L2 tip 3 mm                  R  1.500 mm  sigma  2.92 GPa  ABOVE proof 0.69
  30 mm member                 R 15.000 mm  sigma  0.30 GPa  below proof 0.69
  fiber stiffness radius sqrt(EI/T) at T  5.0 N: 0.42 mm
  mid L 1.70 m: h 0.3 m  5276.5 N/m, h 0.5 m  1075.1 N/m, h 1.0 m   114.2 N/m, h 1.5 m    27.8 N/m
  tall L 2.55 m: ... h 2.0 m    12.6 N/m, h 2.5 m     5.6 N/m
  steady push at 15 deg lean:  12.6 N -> depth 0.42 m
  standoff 0.10 m: force at the visible surface   3.0 N =  6.4 % of weight, = lean  3.6 deg
  standoff 0.78 m: force at the visible surface  23.4 N = 49.7 % of weight, = lean 26.4 deg
  standoff 0.78 m on one prop: yaw moment 3.74 N m; 95th-pct severe-wind yaw demand 0.039-0.094 N m; full yaw authority at hover 0.71-0.94 N m
drone weight on the shell alone sinks 1.57 m (the clump is 1.2-3 m tall: no landing on it)
  one leg on a mid stem tip: k 17.5 N/m, quarter weight 11.8 N -> 0.67 m (small-deflection bound: it folds)
  K 24 pieces, 3 clumps, 14 parts:  8.8 us per step (5 ns box reject, 3 x 30 ns narrowphase per part, assumed)
ellipse in its box, side-on fill 0.785; box porosity for a 0.35 mass: 0.49
```

The hygiene scan and `openspec validate` output are in the handoff.
