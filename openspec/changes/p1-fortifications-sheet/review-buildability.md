# Buildability review: p1-fortifications-sheet (task 1.2)

**Reviewer:** asset-artist, the role that will write the F01–F04 generators. **Reviewed:** `docs/object-sheets/fortifications.md` at `c520ea6` (565 lines). The sheet itself is not edited here; what needs changing is sent back to task 1.4, citing its line or cell. `reference/` was not opened: no letter shows these objects.

**Measured against the pipeline as it stands:**
- **On `unity/main`:**
  - `tools/blender/lib/`: `geometry`, `meta`, `audit`, `skeleton`, `cards`, `shell` and `params`
  - the generators `debris_brick` (K01) and `shrub` (V13)
  - `openspec/specs/asset-pipeline/spec.md`
  - `assets-src/physics_materials.json` (the closed id list)
  - `unity/Assets/_Project/Scripts/World/AssetPhysicsMeta.cs`
- **On the S04 branch:** `lib/straw.py`, `lib/patches.py`, `lib/mat.py` and `generators/ground_patch.py`.
- **Planned in the open change `p1-l01-l03-pole-wires`:** the `wire_span` catenary, the `wires` record, the wire audit and `ThinWire.shader`. None of these is built yet.

**How the numbers were checked:** with throwaway scripts in my scratchpad, not committed. One measures stacked boxes against the frustum and the turn-length budget of the coil. Another fits capsules to the sagged ellipse, and a third lists the generator-table rows by type, range and default.

## How each row was judged

These are the criteria of the earlier review (`archive/2026-10-18-p1-object-sheets/review-buildability.md`):
- **Parameters:** every parameter has a type, a range, a default and an effect. The sheets' house style writes a unitless fraction's range in the Type cell with "—" under Range (68 rows across the sheets, accepted then). That reads as `float` in that range, and is not sent back.
- **Damage:** every D item is geometry a generator can make (cut, move, bend, remove or add parts), and a parameter drives it.
- **Physics in schema 1:**
  - collision: box, sphere, capsule or cylinder, with an id from the closed list (or a flagged proposal) and `landable`
  - gaps: planar polygons, with `narrowest_m` the inscribed circle
  - wind volumes: the same shapes, with a porosity
  - snags: `edge` or `hook`, with 2 or more points and `radius_m`
  - the audit's ±5 cm, or ±2 cm within 0.3 m of a gap polygon or a landable face
- **Editability (D-004):** nothing is hand-sculpted, and every section is dimensioned.
- **LOD distances** are derived, as the earlier review fixed: a feature of size s drops under 2 render pixels at d = 935 · s / 2.

## Two pipeline facts that shape every row

1. **Gaps between separately placed objects are not one asset's metadata.**
   - Schema 1 holds gaps in one asset's own space. The in-row and between-row throats of a dragon's-teeth field (F02), the joints of a block line and the chicane (F04) all lie between objects that the layout places one by one.
   - **I would build the single object as the asset** (a tooth, a block, a hedgehog, a span of wire). A **placement recipe** writes the instances and the throats between neighbours from the actual placed geometry, through a shared plane-section helper.
   - For the gallery, one fixed field or chicane module is built as an asset variant, so its gaps sit in its own metadata. In the world, the same helper runs in the world-builder's compiler (phase 3).
   - A hedgehog **pair** is one asset (the `pair` param), so its throat stays in its metadata.
2. **Proposed ids cannot be emitted.** `meta.validate` refuses `proposed:razor_wire` and `proposed:sandbag` until the physics engineer adds them or maps them (task 1.3). F01's coil volume and F04's bags wait for that ruling. Every other id the sheet uses (`steel`, `concrete`, `timber`, `surface:<id>`) exists.

## F01 Concertina wire

**Verdict: sent back** (items 1, 6 and 7 below). The turn geometry, the row's core, is not buildable as written. Everything else is.

**The turn geometry contradicts itself:**
- The sheet describes a turn two ways:
  - line 116: "a closed loop through the clip points, leaning ±31° alternately"
  - line 61: adjacent turns clipped at 3 or 5 points, successive pairs offset by half the clip interval
- The two cannot both hold, and neither keeps the coil's own tape length. The sheet's mass row (line 42) gives 158 m of tape for 56 turns, which is **2.82 m a turn** (= π · 0.90 m).
- **A planar loop leaning ±31°:**
  - It meets its oppositely leaning neighbour at **two points only**: on a vertical line 0.18 m off the axis, at ±0.39 m (derived). So it can carry two clips per neighbour pair, not five.
  - A loop whose end-on view is the 0.90 m circle and which reaches 2p = 0.536 m along the run (line 47's atan rule, 30.8°) is an ellipse with a **3.06 m** perimeter, 8.6 % more tape than the coil holds (derived).
  - A planar loop that keeps 2.82 m and reaches 0.536 m leans asin(0.536 / 0.90) = **36.5°**, and then stands **0.90 m high × 0.72 m wide** end-on (derived). It is not round.
- **Five clips to each neighbour, alternating:**
  - Each turn must zigzag one pitch (0.268 m) along the run between clips πD / 10 = 0.283 m apart round the circumference.
  - That needs at least **3.89 m** of tape a turn against 2.82 m. At the published pitch the coil would close to about **0.29 m** across (0.25 m for the 0.45 m coil with 3 clips) (derived).
  - So either the clip positions step round the coil from pair to pair, or the published yield goes with a smaller standing diameter.
- **What follows from the turn model:** the generator's turn curve, the clip positions, the standing height and width, the "passes over at 0.90 m" rule (line 100), the porosity's chord (line 101), and the collision volume. Line 95's "cylinder of the coil's diameter" stands 0.09 m off a 0.72 m wide envelope, over ±5 cm.

**Buildable as written:**
- **The tape:**
  - the profile presets
  - the barb cluster: four points, 22 mm at 35 mm, a 0.5 mm blade on a 3.5 mm crimped band round the 2.5 mm core
  - the clips
- **The pickets:** angle, rebar or timber stake. The U picket lacks its depth and thickness (item 3).
- **The three variants as construction.**
- **The I state.**
- **The D items:**
  - cut and recoil, to a 0.05–0.10 m pitch, with curled ends
  - sag, as the ellipse of equal perimeter
  - the trampled mat
  - the festoon
- **Litter**, visual only and labelled so.

**Reuse:**
- **The turn as a parametric curve.** The closed coil is a helix of `turns` at the wound diameter. It is stretched into the turn model item 1 fixes, sampled at `points_per_turn` for the snag record, and densely for LOD0.
- **The tape band is a tube** swept along the turn with `skeleton._tube`: parallel transport, metre UVs U round and V along. It needs to become a public helper; that is a small refactor in the asset change.
- **The axis of a festoon** (variant 3 D) and the hang of a cut end follow the **`wire_span` catenary sampler** of the pole-and-wire change. Turns are placed along the curved axis on a parallel-transport frame.
- **LOD1 and below render with `ThinWire.shader`** of the same change:
  - The 8 mm tape is under one render pixel from 7.5 m, and under one feed pixel from 2.2 m (270 · 0.008).
  - The importer must assign the thin-wire material **by material recipe** (for example a `thin_wire: true` flag on the tape recipe), not only through a `wires` record. The turns are snags, not map wires.
- **Capsules** come from `meta.capsule` and `rotation_from_y`.
- **New code:**
  - the turn model
  - the barb-cluster mesher
  - the clip mesher
  - a profile extruder for the pickets, shared with F03 (`lib/profile.py`)
  - the D solvers: recoil, sag, mat, festoon
  - the **span module** described next
- **Build unit: one span between pickets (3.8 m, about 14 turns), not a run.**
  - Unity's LOD group switches on an object's bounds. A 15 m coil switches by its centre, so its near end would drop to LOD1 while the drone touches it.
  - A span module keeps the switch within 1.9 m of the nearest turn.
  - Each turn belongs to the span holding its centre, so no turn is cut at a seam.
  - Module variants: plain span, coil-end span (twisted ends), anchor span, and the D modules (breach, sagged, trampled).
  - `run_length_m` becomes the layout's input: whole coils of four spans, chained along the layout's polyline over the terrain.

**Generator table check (lines 138–171):**
- **`trampled_spans` is typed `list`.** `lib.params` knows int, float, str and bool only (item 7).
- **`loop_lean_deg` is a free parameter for a quantity that follows from the turn model** (item 1). With the tape per turn fixed, it must be derived or dropped.
- **No parameter drives two items:**
  - the pickets' lean in the damage pattern (line 91: 5–20°, or flattened)
  - the width of the fence or wall the festoon hangs beside (line 87); without it the festoon cuts through a coping or floats off a thin fence

  Both are item 7.
- **"From `damage`" is the default of `cuts`, `sag` and `trampled_spans`, and is defined nowhere** (item 6).
- **Not sent back, these are rules for the generator's `check`:**
  - `profile` is a preset of the four tape values; `check` refuses tape values that disagree with it
  - `core_diameter_m` is a constant
  - `run_length_m` is a layout input, as above

**Budgets (AST-08):**

| | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---|---|---|---|
| What it holds | band (one segment per cluster, or two), barbs as closed thin blades, clips, tie wire | band as a 4-sided tube on 12 points a turn; clusters, clips and tie wire dropped | 3-sided tube, 8 points a turn | 3-sided tube, 6 points a turn |
| From (derived) | 0 m | 7.5 m (a 16 mm cluster under 2 px) | 16 m (8 points: the chord's sagitta, 34 mm, under 2 px) | 28 m (6 points: 60 mm) to the cull, about 300 m (assumed, set at the 200 m tile) |
| Per turn | 1 850–4 000 | 96 | 48 | 36 |
| Per span (14.2 turns, with picket and tie wire) | **26 k–57 k** | 1.4 k | 0.7 k | 0.5 k |
| Per 15 m coil | 104 k–224 k | 5.4 k | 2.7 k | 2.0 k |
| Per 100 m single run (373 turns) | 0.69 M–1.5 M (never all at once) | 36 k | 18 k | 13 k |

- **LOD0 range:** the low end is a 3-sided band, one segment per cluster, barbs of 4 triangles. The high end is a 4-sided band, two segments per cluster, barbs as closed 8-triangle blades.
- **In view at once:** about 5–6 spans within 7.5 m + 1.9 m are at LOD0, 130 k–340 k triangles, up to about two H01 houses at LOD0. Double-stacked runs double that.
- **Every turn is kept at every LOD.** The sheet's "LOD2 … a sparse set of loop strips" (line 173) would thin the haze the pilot sees, and making up for it means thickening the survivors, which the sheet forbids (line 20). At 36 triangles a turn there is no need to drop turns.
- **Risk for the tech-artist and the PLT-07 bench:** a run seen end-on stacks hundreds of alpha-blended loops in the same pixels (overdraw). The review sheet must include an end-on 100 m run.

**Texture plan:**
- **The tape** uses a plain metallic material, and its finish presets (new, dull, rusted) are rows of the group's trim sheet (below). Rust and white rust along a turn run along the tube's V. The sheet is right that no texture set is needed (line 67).
- **Pickets and tie wire** use the trim sheet's rusted-steel rows.

**Physics metadata I can emit:**
- **Collision:**
  - **Pickets: emittable.** An angle as two boxes, a U as three, rebar or a stake as a capsule, `steel` or `timber`.
  - **The coil volume: not emittable as written**, for three reasons:
    1. its id is a proposal (above)
    2. its shape waits on the turn model (item 1)
    3. the audit has no rule for a compliant volume over sparse geometry
  - **Why the audit fails it:** the rigid-primitive audit measures the volume's surface against the nearest tape face, and finds about 0.13 m in the lattice's openings (half the sheet's 0.27 m opening, line 100). The shell rule (`SHELL_MATERIALS`, twig and foliage only) has a 0.1 m standoff that fails the same way.
  - **What I would add to the pipeline, in my asset change, after the physics reading:**
    - audit the tape and clip tubes against their own snag polylines, as the L03 wire audit does (vertex within radius + 5 mm; those vertices leave the primitive pass)
    - audit the coil volume against the turns' envelope (the polylines' radial extent from the volume's axis) within ±5 cm
  - **D sag:** two capsules side by side meet ±5 cm (best fit 48 / 33 / 19 mm at 0.7 / 0.8 / 0.9 D, derived). Line 98's "(or a box)" misses by **169–185 mm** at the corners (item 1). The trampled mat as a low box is fine.
- **Snags:**
  - **One `edge` per turn:** a closed polyline written as 25 points, the first repeated (schema 1 has no `closed` flag), with `radius_m` 0.008. Clips are 2-point `edge` records at 1 mm. Picket tops and tie-wire tails are `hook`.
  - **Barb pitch, cluster length and blade edge** can go on each record as additive fields (`barb_pitch_m`, `barb_length_m`, `blade_edge_m`):
    - `meta.validate` passes unknown keys today, so they reach the JSON
    - but `CatalogImporter` and `AssetPhysicsMeta.SnagFeature` keep only kind, points and radius, so **they are lost in the prefab**
    - the asset change adds them as optional, validated fields, once the physics reading names them (line 177)
  - **Per span module:** 14 turn records (about 350 points), 71 clip records and the picket hooks, about 500 points. A 100 m run has about 13 000 points with the clips. Whether `StaticContacts` takes that or a coarser record per span is the world-builder's and the physics engineer's (line 179).
- **Gaps:** none, an empty list. A breach is open ground with no polygon. Emittable.
- **Wind:** one capsule per span at porosity 0.95, its radius from the turn envelope. Emittable.
- **Landable:** none.

## F02 Dragon's teeth

**Verdict: sent back** (item 2, the box count, and item 6). Everything else is buildable as written.

**Buildable as written:**
- the square frustum and the triangular pyramid
- the reinforcement and the lifting loop
- the four D items:
  - chipped: spalls with bars exposed at the deep ones
  - toppled: on a face, or tilted on a crater lip
  - shifted
  - buried
- the gap rule ("the generator writes each from the actual placement")

**Reuse:**
- **The frustum mesher** extends `debris_brick`'s chamfered box, whose corners each draw their own chamfer, to sloping faces. `geometry.uv_upright` keeps V up on a 65.8° face, whose normal is mostly horizontal.
- **The loop and bars** are `skeleton._tube`.
- **New code:**
  - spalls as **planar cuts** through an arris, with jagged fracture relief within ±2 cm of the cut plane, so the chipped tooth stays convex
  - the field recipe and the neighbour-gap writer (fact 1)
- **The buried ramp is a ground feature:** earth pushed over the rows (the earlier review's convention 7). It is the world-builder's terrain, or `surface:crater_spoil` filler shapes, not tooth geometry.
- **The "box impostor row beyond 300 m"** (line 289) is a world-side merged LOD of a whole field, not the tooth's.

**The collision box count is wrong (line 237 and the `collision_boxes` cell, line 280):**
- The sheet's 0.205 / n m bound is the deviation at the middle of a face. At a box's corner, on the arris, it is 1.31 times that, **0.268 / n**, because the corner stands off two faces at once (derived; checked against the arris line).
- **11 boxes:** 18.7 mm on the face, **24.4 mm at the arrises**, so they fail the ±2 cm the sheet wants round the openings.
- **±2 cm needs 14** (19.2 mm). **±5 cm needs 6** (44.7 mm), not 5 (53.7 mm).

**What I would build for collision:**
- **A `convex` primitive kind:** the vertices plus the precomputed face planes, at most 32 vertices.
  - the validator checks convexity
  - the audit's signed distance comes from the planes and edges
  - the importer makes a convex MeshCollider
  - the sim core's contact for it is the physics engineer's (line 292)
- **One shape per tooth is exact,** and it stays exact for every state:
  - a toppled tooth is the same hull rotated
  - a planar-cut spall keeps the hull convex
- **With stacked boxes, a spall deeper than 2 cm leaves a box corner standing in empty space.** The chipped D tooth then fails the audit next to every gap, unless spalls stay under 2 cm deep.
- **Until the convex kind lands:** 14 boxes for I, and the D tooth waits for it.
- **Interim count:** 14 boxes for each of 200 teeth in a 4 × 50 field is 2 800 boxes, against 200 hulls of 6 planes.

**Generator table check (lines 263–287):**
- Every row has a type, a range and an effect. `bed_m` doubles as the sand bed and the burial depth, which is acceptable.
- **Field-recipe parameters, not the tooth's:** `rows`, `teeth_per_row`, `spacing_m`, `row_spacing_m`, `stagger`, `yaw_jitter_deg`, `position_jitter_m`, `toppled_fraction`, `shifted_fraction` and `buried_span_m`. The build splits the table that way; this is not sent back.
- "From `damage`" (item 6).

**Budgets:**

| Per tooth | LOD0 | LOD1 | LOD2 | Cull |
|---|---|---|---|---|
| I | about 170 (chamfered frustum 44, loop about 120) | 44 (loop dropped) | 12 (plain frustum) | about 470 m (the 1.0 m height under 2 px) |
| D | about 500 (6 spalls, 4 bars) | about 200 (spall cuts kept) | 12 | |
| From | 0 | 5.6 m (the 12 mm bar under 2 px) | 23 m (a 0.05 m chip under 2 px) | |

A 4 × 50 field is 34 k–100 k triangles if every tooth were at LOD0, and 2.4 k at LOD2.

**Texture plan:** the concrete tiling set at 1K, about 512 px/m over a 2 m tile; on a 1.2 m face nothing repeats.
- **Per-variant RGBA mask at 512 px**, in a unique UV2: R chips and fractures, G algae and lichen on the shaded foot, B splash band, A rust bleed.
- **Neighbours must not show the same pattern:**
  - 4–6 seeded tooth meshes with different UV offsets
  - or a per-instance offset in the shader (the tech-artist's choice)

**Physics metadata I can emit:**
- **Collision:** boxes or the convex kind as above; the loop and bars as capsules at 6 mm. `concrete` and `steel`, not landable.
- **Gaps** come from the placement recipe (fact 1):
  - **one polygon per in-row neighbour pair**, the vertical section through both centres, sides at the facing faces, closed at the teeth's top height (the sheet's convention; inscribed circle 1.0 m)
  - **one per neighbouring pair across adjacent rows** (the two nearest teeth of the next row)
  - all planar by construction; about 490 for a 4 × 50 field
  - the sheet's "straight across" and "63° diagonal" columns are passabilities through these throats, not extra polygons
  - the ±2 cm zone then covers every face, so the tooth's collision must meet ±2 cm everywhere even when it is built alone
- **Wind:** one box (or the hull) per tooth, porosity 0. Emittable.
- **Snags:** 8 `edge` records per tooth (the arrises at 3 mm); the loop at 6 mm; exposed bars as `hook` at 6 mm; spalled arrises at 1 mm. Emittable.

## F03 Anti-tank hedgehog

**Verdict: sent back** (items 3, 4, 5 and 6). The form, the joints, the states and the physics plan are buildable once every member section is dimensioned.

**Buildable as written:**
- the octahedron geometry (H = L / √3, hexagon circumradius 0.735 m)
- the three joint kinds
- the feet
- the stances and yaw
- the pair, with or without a chain
- every D item:
  - bent arms: two segments meeting at a bend ring
  - a cracked weld with a sagging arm
  - rolled, tilted on a verge, sunk
  - rust

**Reuse:**
- **One `member` routine** in the shared `lib/profile.py`: a profile extruded along a segment, with a half-depth notch, a half-member cut at the node and a bend.
  - It also serves F01's pickets and F04's post and arm.
  - Its profiles are rail Р50 and Р65, equal angle and channel.
- **Welds** are `skeleton._tube` beads along the joint lines; gussets and feet are thin boxes.
- **The gaps** use the same plane-section helper as F02.

**Sections are not dimensioned (lines 312–313 and the `member` enum, line 375):**
- **The Р50 rail** gives height 152, foot about 125–132 and head about 70–72 mm. To draw the profile and its three collision boxes, the generator also needs:
  - the head's depth
  - the web's thickness
  - the foot's edge and root thickness
  - (published in the profile standard the sheet cites)
- **Check:** the section's area must come to about 64 cm², which is 50.4 kg/m at 7850 kg/m³ (derived).
- **Other enum values:** `rail_r65` has no dimensions at all. `angle_100` lacks its thickness, and `channel_100` its flange and thickness.
- **Line 51 (F01):** the U picket has its 76 mm width but no depth or thickness.

**"Arm" means two things (item 4):**
- **The 1.8 m member:** line 306 and `arm_length_m` (line 377).
- **The half-member from the node to an end:**
  - "lower arms", "upper arms"
  - `bent_arms` and `bend_at_m` "from the node" (lines 389–391)
  - wire holes "0.15 m and 0.35 m from each end" (line 316); `wire_holes` "per arm" (line 382) is then 2 or 4 holes a member, depending on the reading

**The pair's throat plane is unbounded as worded (line 352):**
- "The vertical plane midway between them" is a plane that neither hedgehog reaches: each reaches at most 0.81 m from its centre at 2.0 m spacing.
- The table's widths are measured along the line of centres. So the polygon lies in **the vertical plane through both centres**, its sides each hedgehog's silhouette projected along that plane's normal. This is the houses sheet's rule for a gap through a thick wall.
- "Under the node" (line 350) is a space behind the lower windows, not a throat. It gets no polygon, which the sheet's "not passable" already implies.

**Budgets (rail, one hedgehog; a pair doubles them):**

| | LOD0 | LOD1 | LOD2 | LOD3 | Cull |
|---|---|---|---|---|---|
| What | 28-vertex rail outline with fillets, notches, 12 wire holes, gussets, 6 feet, weld beads | weld beads dropped | 12-vertex simplified section, no holes or notches | three boxes | |
| Triangles | about 2.9 k (I), about 3.3 k (D, two bent arms) | about 1.2 k | about 230 | 36 | |
| From (derived) | 0 | 3.7 m (an 8 mm bead under 2 px) | 14 m (a 30 mm hole, size assumed) | 70 m (the 0.15 m section) | about 490 m (the 1.04 m height) |

**Texture plan:** the group's trim sheet for the members (below):
- the rusted-steel rows
- the worn running band of the rail head
- the weld row

Rust streaks below the node are a per-variant mask in UV2, run down by gravity in asset space.

**Physics metadata I can emit, once the sections exist:**
- **Collision:** about 21 boxes: the head, web and foot of each member piece (4 pieces and 2 notches), 3 gussets and 6 feet. A bent arm adds 3 more. All `steel`, within a few millimetres of the profile.
- **Gaps:**
  - the three lower windows: the triangle node–foot–foot, inset by the members' silhouettes in its plane; planar by construction
  - the pair's throat (item 5), closed at the tips' height
- **Wind:** a cylinder of radius 0.735 m and height 1.04 m at porosity 0.6.
- **Snags:** `edge` loops round each cut end (24 points at 1 mm), flange tips and head corners along every member, gusset and foot-plate edges, and a chain if present.

## F04 Checkpoint kit

**Verdict: sent back** (items 6 and 7). The block, the bag position and the barrier are buildable as written; the sandbag build waits for task 1.3's `sandbag` ruling (F04 is P2).

**Buildable as written:**
- **The ФБС block:** the published sizes and tolerances, the end grooves (0.11 × 0.10 m, derived from the volume), the loops (raised, flush or cut).
- **Its collision:** 5 boxes and 6 loop capsules, exact. The landable top passes the audit.
- **The bag:** a stadium section 0.29 × 0.12 m, derived from the 0.72 m perimeter, and 0.48 m long, laid in half-offset courses, the U plan, ports, the entrance and the optional roof.
- **The barrier:** post, 4 m arm, counterweight tail, fork rest, the 5.5 m support rule, and the arm states.
- **The D items:** shoved and rotated blocks; burst bags with spill cones at 30–35° and slumped courses; the snapped or torn arm.

**Reuse:**
- **The block** extends `debris_brick`, with chamfered and chipped corners.
- **Loops, the arm, the post and the fork** come from `lib/profile.py` and `skeleton._tube`.
- **Spill cones** are ground: 2–3 stacked cylinders of `surface:crater_spoil` within ±5 cm. The `surface:<id>` rule exists.
- **The bag is new:** a stadium lattice with seeded lumps and a tied fold. So is the course layout of the position.
- **The chicane** (`lines`, `blocks_per_line`, `lane_m`, `line_offset_m`, `joint_m`, `hedgehogs`, `wire_verges`, `shifted_blocks`) is a placement recipe, as the sheet's Outputs line says (line 544). The build splits the table into block, position, barrier and recipe. Joints between blocks are recipe gaps (fact 1).

**Generator table check (lines 503–542):**
- Every row has a type, a range and an effect. `inner_size_m` and `port_size_m` become dotted pairs (`.x`, `.z` and `.w`, `.h`).
- **Two D items have no parameter:**
  - the block "cracked through on a clean plain-concrete fracture" (line 469)
  - the barrier "post bent 10–30°" (line 471)

  Both are item 7.
- "From `damage`" (item 6).

**Budgets:**

| Piece | LOD0 | LOD1 | LOD2 | Cull |
|---|---|---|---|---|
| Block | about 450 (grooves, chips, two loops) | about 110 from 5.6 m (the 12 mm loop bar) | 12 from 47 m (the 0.1 m groove) | about 270 m (the 0.58 m height) |
| U position, about 250 bags | about 25 k (about 100 a bag); about 30 k for D with cones, slumps and shreds | about 4 k from 14 m (a 30 mm bulge): outer and top faces only, coarse | about 100 from 56 m (the 0.12 m course): one box per wall segment | about 560 m (the 1.2 m wall) |
| Barrier | about 500 (12-sided arm, bracket, counterweight frame, fork) | about 250 from 5 m (bolts and brackets simplified) | about 40 from 35 m (the 76 mm arm) | about 500 m (the 1.1 m post) |

**Texture plan:**
- **Blocks** share F02's concrete set and mask layout.
- **Bags** use the woven set the houses sheet already names, tinted per `bag_material`, with a UV-fade and dust mask.
- **The barrier stripes** are a row of the group's trim sheet: one red and one white 0.5 m stripe, tiled along U. They are paint, with no physical role, so a texture is right.

**Physics metadata I can emit:**
- **Blocks:** as above.
- **Bags:**
  - **Top course, one box per bag:** each box must sit about 10 mm inside the bag's sides. A box of the full 0.29 m stadium stands **25 mm** off its rounded shoulders, over the landable ±2 cm. Inset 10 mm a side, it holds **18 mm** (derived).
  - **Elsewhere, one box per segment per 3–4 courses,** as the sheet says. Within 0.3 m of the entrance and of each firing port (both gap polygons) the boxes go one per bag, to hold ±2 cm.
  - Material `proposed:sandbag`, which waits for 1.3.
- **Barrier:** a box post, a capsule arm, a box counterweight, the fork as capsules and a box; `steel`. The gap under the arm (post to rest, road to arm) sits in the barrier's own metadata, since the rest is part of the prefab.
- **Wind:** blocks and walls solid, the barrier none. The "sheltered cavity" (line 491) is what the solver works out, not a record (the earlier review's convention 5).
- **Snags:** as listed (line 492). Emittable.

## Across the group

- **Texture budget (AST-08): not one 1K trim sheet for everything.** The large faces need tiling sets; the thin steel suits one trim sheet.
  - **Tiling sets at 1K:**
    - concrete, for F02 and the F04 blocks
    - rusted steel, for the pickets, F03, loops and post; L01 already pins `rough_concrete` and `rusty_metal`, so pick those over the alternatives where they match
    - woven bag, for F04
    - painted metal, for the barrier and F03's painted variant
  - **One derived 1K steel trim sheet,** built by script from those sets, holding:
    - the zinc tape (new, dull, rusted)
    - rusted angle
    - rail with its worn band
    - weld bead
    - tie wire
    - the red and white stripe
  - **Masks:** 512 px per variant.
  - Nothing needs 2K.
- **Micro-detail:**
  - The dry grass through the bottom loops (F01 I) and round the teeth's feet (F02 I) is AST-10 micro-detail, placed with the object in the world.
  - For the gallery, S04's `lib/straw.py` stems and blades can be baked as a visual-only near mesh, as `ground_patch` does today.
- **`lib/shell.py`** fits spheres and capsules to a mass. It could fit F01's D volumes (sagged, bunched, flattened) automatically, once the physics reading gives `razor_wire` an envelope tolerance.
- **For the physics reading (task 1.3)**, where the sheet's plan is not emittable as written:
  - `razor_wire` and `sandbag`
  - the snag fields: barb pitch, cluster length, blade edge
  - what a compliant coil volume means for contact, and the audit rule it implies
  - whether the 5 clip records per turn add anything over the turn's own edge at 8 mm
  - the convex primitive's sim side
  - F02's ±2 cm on every face

  I do not rule any of these here.

## Build order after G1

Each step is a Standard-tier unit on established code (D-033), one agent each, at most four in parallel (D-036).

1. **Pipeline unit A (asset-artist): `lib/profile.py` and the plane-section gap helper.** Small, tested in plain Python. F03, F02, F04 and F01's pickets all need it.
2. **F03 hedgehog** (after item 3 is closed). It needs only `steel` and boxes, so it is the smallest real generator. It proves the member routine, the lower-window gaps and the pair throat.
3. **Pipeline unit B (asset-artist with the physics engineer): the `convex` collision kind.** Validator, audit, importer and the sim contact. In parallel with step 2.
4. **F02 dragon's teeth.** The I tooth can ship on 14 boxes. The D tooth and the gallery field module (gaps per neighbour pair) follow once B lands. The field recipe goes to the world-builder for phase 3.
5. **Pipeline unit C (asset-artist): snag fields and the compliant-volume audit.**
   - It starts after task 1.3 rules on `razor_wire` and the snag fields, and after `p1-l01-l03-pole-wires` merges (catenary, `ThinWire.shader`, the wire audit).
   - It covers the optional snag fields through the validator, importer and `SnagFeature`; the tube-against-polyline audit for snag-carried tubes; the envelope audit for a compliant volume; and the importer's thin-wire material by recipe.
6. **F01 concertina** (after item 1 is closed and C lands):
   - span modules for variants 1 and 2, I then D
   - variant 3 on a fence top, once a yard fence (houses sheet Y01–Y04) exists to sit on
   - the review sheet adds an end-on 100 m run for overdraw
7. **F04 checkpoint kit** (P2, after `sandbag` is ruled): the block, the barrier, then the bag position. The chicane recipe goes to the world-builder.

**Lanes:** A runs 1 → 2 → 4 → 7, B runs 3 → 5 → 6. F01 is the largest and has the most dependencies. If phase 3's layout needs F01's footprint early, a collision-and-snag-only F01 (pickets, volume and turn records, with LOD1 tubes as its look) can go in before the LOD0 barbs.

## Sent back (for task 1.4)

1. **F01 turn model.** Lines 47, 61, 95, 98, 116 and the `loop_lean_deg` row (line 152).
   - State one turn geometry that keeps the coil's own 2.82 m of tape a turn (line 42), with clip positions where turns actually meet. The source picks the model: planar loops tilted alternately (two crossings per pair), or a clip pattern that steps round the coil.
   - Derive from it the standing height and width, the lean (an output, not a free parameter), and the coil volume's shape within ±5 cm. A 0.90 m capsule misses a 0.72 m wide envelope.
   - Drop "(or a box)" from the sagged volume (line 98): 169–185 mm off at the corners; two capsules fit within 48 / 33 / 19 mm.
2. **F02 collision box count.** Line 237 and the `collision_boxes` cell (line 280).
   - The arris corners stand 0.268 / n m off, not 0.205 / n: **14 boxes for ±2 cm** (19.2 mm; 11 gives 24.4 mm), **6 for ±5 cm** (44.7 mm; 5 gives 53.7 mm).
   - Also note that a spall deeper than 2 cm cannot be followed by stacked boxes; that is the convex primitive's case (line 292).
3. **Member and picket sections.**
   - F03 line 312: Р50's head depth, web thickness and foot thicknesses, checked against about 64 cm² (50.4 kg/m).
   - Every other `member` value (line 375): `rail_r65` in full, `angle_100`'s thickness, `channel_100`'s flange and thickness. Or drop the values left undimensioned.
   - F01 line 51: the U picket's depth and thickness.
4. **F03 "arm".** Lines 306, 316, 377, 382 and 389–391.
   - Name the 1.8 m bar a member (`member_length_m`) and the node-to-end half an arm, or the reverse, once.
   - State `wire_holes` per end or per member.
5. **F03 pair throat.** Line 352: the polygon lies in the vertical plane through both centres, its sides the hedgehogs' silhouettes projected along its normal. "Midway between them" reaches neither hedgehog.
6. **"From `damage`".** Define it once in "How this sheet was made", as houses-and-yard.md line 74 does. For example: an item takes its I value at `damage` 0 and its D value at the row's D default, interpolates between them, a range in the states text is drawn by `seed`, and any item may be set explicitly. Read with the "I below 0.25, D above" rule. It is used in:
   - F01 lines 166–168
   - F02 lines 282–284
   - F03 line 389
   - F04 lines 539–541
7. **Missing parameters for stated items:**
   - F01 `trampled_spans` (line 168) is a list, which `lib.params` cannot hold. Use `trampled_count` (int, 0–3 per coil) and `trampled_length_m` (1–5 m), positions drawn by seed.
   - F01 picket lean in D (line 91): `picket_lean_deg` 0–20, and flattened.
   - F01 variant 3: the support's width (fence board to wall coping, about 0.02–0.6 m), so the festoon (line 87) hangs beside the face instead of through it.
   - F04 `cracked_blocks` (line 469).
   - F04 `post_bend_deg` 0–30 (line 471).

Nothing here is a pilot question.
