# Physics review: S04's surface metadata

**Reviewer:** physics-engineer · **Task:** 1.5 · **Branch:** `unity/art/p1-s04-belt-straw` · **From tip:** `ccb5106`
**Against:** AST-05, AST-10, REAL-05, PHY-01, PHY-03, PHY-17, D-021, D-024; design 6, 9, 10 and 11; `review.md` R4 and suggestions 3 and 8; the spec deltas "Bare patches are ground, not stains" and "The collision audit measures the union of the primitives"
**Read:** the proposal, design, tasks, review and the three spec deltas of this change; §S03, §S04, §S05 and the group's shared rules of `docs/object-sheets/earthworks-and-surfaces.md` (from `origin/unity/main`); T4 of `docs/reference-notes/terrain.md` (from `origin/unity/main`); `docs/legacy/map-and-world/surfaces.json`, `surfaces-review.md` §1–§4, `terrain-holes-review.md` §2–§3; the archived `p1-pipeline-hardening` change's `review-materials.md` §1–§2 (the materials sign-off); `assets-src/physics_materials.json`; `unity/Assets/_Project/Art/Models/surfaces/S04/S04_patch.assetmeta.json`, `assets-src/surfaces/S04_patch.json`, `tools/blender/generators/ground_patch.py`, `tools/blender/lib/audit.py`, `tools/blender/lib/meta.py`, `tools/assets/derive.py`

I read this metadata as its consumer: the phase-7 contact law (feet, arms, body, coil and fiber nodes against the asset's primitives through `StaticContacts`) and the log that must let QA say which shape a leg stood on. Every number is labelled pilot / measured / published / derived / assumed; "carried" means a starting value carried from the first attempt's tables. The arithmetic and the two audit experiments are in Evidence.

## Summary

- **The 5 cm surface-top tolerance and the union: accepted, bounded.** The union is right and catches the state QA found. The 5 cm top is right for ground a foot visibly settles into, and wrong for hard ground. It is bounded to landable tops where the reference foot sinks at least 25 mm (PR6). S04's numbers do not change.
- **A bare patch is the mat's absence, not another soil.**
  - The soil lies one mat depth (0.125 m) below the mat top, under the mat and under the patches alike. The patch shows it: `surface:belt_straw`, not `surface:belt_bare`.
  - The mat over that soil is the object id `straw`, as my own rule 2 already says for a straw layer built on a shape. It is open over every patch.
- **One whole mat box can never be opened.** Primitives are convex, and a union only adds. So a mat with holes is a set of boxes: about 85–103 for S04 (PR4). That is the main finding of this reading. The step that fits inside the 5 cm instead ("lower the top by up to 5 cm", QA suggestion 3) is rejected: it leaves the mat's support over a visible soil floor, so the patch would still be a stain to physics.
- **The mound spheres: accepted.** They follow the slab when it deepens.
- **Three trips besides:**
  - The 0.2 m slab is too thin for F-5: a sinking foot meets the bottom face's normal (PR1).
  - The phase-7 law needs a union filter and a per-object merge (N3).
  - A patch placed flush with terrain loses its floors to the terrain contact (N5).

## 1. What the ground law reads from these shapes

- **A `surface:<id>` shape** (rule 2 of the materials sign-off, TH-4) carries its row's soil block only: elasto-plastic Winkler sink from the shape's face, an unload ratio, `max_sink` then the firm layer, damping, μ and ploughing. It has no mat, no cover and no stems. Its contacts carry `Surface` = the row with `Material` = `NoMaterial`.
- **Ground contacts merge.** Per foot, the terrain contact and the soil-shape contacts within 45° of each other count as one, and the deepest wins (TH §2.3).
- **A compliant object** (`straw`: 490 N/m, ζ 0.3, μ 0.45/0.35) is a spring-damper in series with the leg. Object contacts and ground contacts both act; they never merge with each other.
- **At the reference foot** (25 N on 15 mm; foot area 1.767e-4 m², carried), the rows this asset names give:

| Contact | Stiffness | Static sink | Label |
|---|---|---|---|
| `surface:belt_straw` soil | 830.6 N/m | 30.1 mm (max sink 80 mm; wet 128 mm) | derived from the carried row |
| `surface:crater_spoil` soil | 494.8 N/m | 50.5 mm (max sink 120 mm; wet 192 mm) | derived from the carried row |
| `surface:belt_bare` soil | 2474 N/m | 10.1 mm | derived from the carried row |
| the belt_straw mat at 0.125 m, terrain path | km = 4 kPa · π(b/2 + D/2)² / D = 492.6 N/m | 50.8 mm (locks at 100 mm) | derived |
| mat in series with soil, terrain path | 309.2 N/m | 80.9 mm | derived |
| `straw` object | 490 N/m | 51.0 mm | derived (the table's row, the same mat at the same depth) |

The wet figures use the sheet's D-021 proposal: bearing × 0.4 and max sink × 1.6, both assumed.

## 2. The 5 cm surface-top tolerance and the union audit

**The union: accepted.**
- A face buried inside another primitive is no surface, for the audit and for the contact law alike. The law mirrors it in N3.
- Measured: a stepped floor left under a whole mat box fails `box->mesh` at 125.000 mm (Evidence E1). So the audit catches exactly the state QA found.
- In that same case `mesh->collision` reads the floor as 0 mm, because the floor lies on a buried primitive face. So box→mesh is the direction to assert on (PR7 b). N1 has the metric fix.

**The 5 cm top: accepted, bounded (PR6).**
- *Why 5 cm is right on soft ground.* A soil shape's face is where the ground law starts the foot's sink, not where the foot stops. On belt_straw the reference foot settles 30 mm, and its print grows to 80 mm under load cycles. On crater_spoil it settles 51 mm. Under a straw mat it settles 51 mm into the straw. A geometric offset of up to 5 cm on such a top moves the foot's rest within the depth it settles anyway. The visual ground (a straw mat, loose spoil) hides it.
- *Why 2 cm stays on hard ground.* AST-05's 2 cm is there because a foot on a hard face rests at the face. On asphalt, a slab, crust or rubble the sink is 1.4–5.7 mm (derived), so 5 cm would be a visible float and a false 5 cm step between legs. The current rule holds every `surface:` shape at 5 cm, including those four. E2 shows a `surface:asphalt` box passing at 39 mm under a ±4 cm relief.
- *The bound.* The 5 cm top applies to a landable shape whose contact sinks at least **25 mm** under the reference foot (assumed: a round figure under the 3 cm the wider tolerance adds, so a small tuning of a row near it does not flip its class). That is:
  - the surfaces belt_straw, crater_spoil and tilled (30.1, 50.5 and 35.4 mm, derived)
  - the materials with `stiffness_n_per_m` ≤ 1000 N/m: `straw`, `fabric`, `mesh` and `twig` (derived from the 25 mm)
  - every other landable top keeps the 2 cm zone.
- *Why the bound is needed now, not later.* The mat becomes `straw` (§3), and E2 shows a `straw` top failing the 2 cm zone at 39 mm under S04's ±4 cm relief.

**What the 5 cm does not buy.** The flat tops follow the ±4 cm relief's height, not its slope. On the asset path a drone sits level where the visual mat would tilt it. The relief's physical twin is the row's micro-relief, 0.04 m @ 0.7 m (carried), which the terrain path carries (N4, N6).

## 3. Bare patches

**The surface id: `surface:belt_straw`** (derived, from T4 and the sheet's rows).
- The patches are the moist dark loam under the mat, exposed:
  - T4: "Bare soil patches are soft when moist."
  - The sheet's soil colour for them is 10YR 2/1–3/1, the moist topsoil of the belt_straw row.
- belt_bare is a different ground (S05: fine dusty loam beaten bare inside dense belts, 10YR 4/2–5/3). At the reference foot it is 3× stiffer (2474 against 831 N/m) and 9× dustier (0.9 against 0.1). It would make the patch the firmest, dustiest spot of a moist mat, the opposite of T4.
- The same id is what the terrain path gives, too. There a bare patch is the straw cover channel at 0: MatDepth 0, no straw elements, `GroundHeight` and the surface row unchanged. So the asset path and the terrain path agree.

**The step: the mat's full uncompressed depth, D = 0.125 m** (carried: the sheet's default mat depth, T4; it is also the depth the `straw` row is derived at).
- The asset's y = 0 is the mean top of the uncompressed mat. The soil lies one mat depth below it everywhere, and a patch is where the mat is missing.
- *Not the compressed figures.* Under the reference foot the mat compresses 51 mm, which leaves its loaded top 74 mm above the soil. Compression is the contact law's: the foot sinks into the mat by the law. A floor placed at either figure would show soil only 5–7 cm below an uncompressed mat edge, and the terrain path would compress the mat a second time.
- *Not a step within the 5 cm.* The mat's collision would then still cover the floor.

**The loaded step a leg feels between mat and patch** (derived, reference foot):

| Model | On the mat | On a patch | Step |
|---|---|---|---|
| terrain path (row law) | −80.9 mm | −155.1 mm | 74.2 mm |
| asset path, mat as `straw` (this ruling) | −51.0 mm | −155.1 mm | 104.1 mm |
| asset path, mat as `surface:belt_straw` soil at the mat top (as built) | −30.1 mm | −155.1 mm | 125.0 mm |

- The ruling halves the error of the built state.
- It makes the mat springy, as T4 and the sheet's physics row ask, instead of plastic.
- It keeps soil from standing at two heights 0.125 m apart with nothing between them.

**The shapes.**
- *The soil:* one `surface:belt_straw` box under the whole patch, its top at −D. It shows only on the floors (PR1).
- *The mat:* `straw` boxes standing on it, from −D to 0, open over every floor (PR2, PR4).
- *The stack is a layer, not a joint.* The straw boxes stand on the soil and are not sunk into it. F-5's 0.2 m overlap is for joints of one material. The straw–soil interface is where a foot driven through the mat meets the soil.
- *Stand-off.* Every shape's surface stays within 5 cm of the mesh (the bounded soft rule). The flat floor top holds the floor's ±4 cm relief, and the box step faces hold the mesh's step.

**Where a leg straddles mat and patch.**
- A foot on the mat's edge touches the straw box's top arris, is pushed up and outward, and slides off into the patch: what a foot on a straw edge does.
- A foot sunk deeper into the straw than its distance from the step wall meets the wall's face as nearest, and is pushed sideways into the patch: the edge gives way.
- The soil floor, 0.125 m lower, acts only once the foot is down there.
- The fiber over a step bends over the straw arris (edge radius 0.02 m, the table's row), so it never breaks by bending there. Its risk at a step is friction (capstan μ 0.45).

**Why the mat must be many boxes.**
- Every primitive is convex, and the audit and the law take their union. A union can't subtract, so no single box can have a hole.
- The mat around a patch must therefore be built from boxes that stop at the patch's outline within 5 cm.
- The visual step must be the same outline. A round mesh step against a polygonal opening misses by 76 mm per metre of radius at N = 8 (derived).
- The construction in PR4 meets this with one box per outline edge plus a rectilinear fill: about 9 patches (8 % of 144 m² at about 1.23 m² each, derived), so 85 shapes at N = 6 and 103 at N = 8.
- Physics sets no limit on that count. `StaticContacts` pays per nearby shape, and a tree already carries 150–300 capsules (materials sign-off §5).

## 4. The mound spheres

**Accepted.**
- `surface:crater_spoil` is the row the materials sign-off maps fresh mounds and loose spoil to. A fresh burrow heap is loose spoil.
- The spheres follow the visual within 35.2 / 33.8 / 35.2 mm (measured, task 1.2). They are landable, and their caps are the only exposed part of them.
- A foot on a mound sinks 51 mm at the reference load and up to 120 mm (dry, carried), flattening the heap towards the mat. That is a loose heap's behaviour.
- *Change:* only what follows from PR1. Each radius is capped at (SLAB_M + h)/2, so with the deeper slab the spheres can take the rim-and-apex radius, and their bottoms stay inside the soil slab. PR5 keeps every mound on the mat.
- *The wet case.* Wet crater_spoil sinks up to 192 mm. Adding about 30 mm of overshoot, foot radius and margin (legacy F-2), 222 mm can pass a sphere's centre (radius 159–173 mm as built; derived). That reversal is the contact law's to absorb with its latch (N3), not the asset's.

## 5. Other trips

- **F-5 thickness.**
  - The slab box is 0.2 m thick, so its bottom face becomes nearer than its top beyond 100 mm of sink.
  - belt_straw's max sink plus about 30 mm is 110 mm dry and 158 mm wet (derived). Past that, a sinking foot gets the bottom face's normal, which pushes it down through the slab.
  - F-5 (0.3 m behind an open face, carried) fixes the dry case. The law's latch covers the remaining 8 mm of the wet case (N3, N9). Fixed by PR1.
- **Slot physics.** It is informational (the shapes carry the law), but it should not contradict them.
  - The `straw` micro slot names `surface:belt_straw`; it should be `straw`.
  - The `ground` slot stays `surface:belt_straw`, the asset's default ground; the mat shapes override it (PR2).
- **The forensic order.** The log names (object, shape). With a fixed order (PR8), QA can say "leg 2 on a bare-patch floor" or "leg 3 on the mat" from the shape index alone.

## Required changes for task 1.6 (asset-artist)

- **PR1 · The soil, one mat depth down.**
  - `collision[0]` is one box, `surface:belt_straw`, landable, 12 × 0.3 × 12 m, with its top at y = −D = −0.125 m and its bottom at −0.425 m. The 0.3 m is F-5 behind the floors' top.
  - The slab mesh deepens to match: `SLAB_M` = D + 0.3 = 0.425 m (derived), so the audit holds the box's sides and bottom.
  - The mound radius cap follows `SLAB_M` unchanged.
  - *Test:* the S04 audit line for `collision[0]` is within 50 mm.
- **PR2 · The mat as `straw`, standing on the soil.**
  - Every shape whose top is the mat's top takes the object id `straw`, landable, with its top at y = 0 and its bottom at −D. Its bottom is on the soil box's top, not sunk into it.
  - New param `mat_material` (`straw`), used by these boxes and by the `straw` micro slot.
  - New param `mat_depth_m` (0.125, carried; range 0.05–0.20 from the sheet). Its `effect` says that `straw`'s 490 N/m is derived at 0.125 m, so another depth needs a physics re-derivation.
  - `physics_material` stays `surface:belt_straw` for the soil box and the `ground` slot.
  - In `assets-src/physics_materials.json`, replace the `straw` rationale's last sentence "Straw over ground is surface:belt_straw, not this id" with: "Straw lying on terrain is the surface's mat (surface:belt_straw's cover), never this id; a straw layer over a shape, a surface shape standing in for ground included, is this id (p1-s04-belt-straw review-physics §3)." This is my sign-off of that text; no value changes.
- **PR3 · The step in the mesh.**
  - Over each patch, the top drops by D from the mat surface to the floor, which is the mat surface lowered by D, relief included.
  - The drop runs along the patch's step outline: a convex polygon of N vertices (N = 6–8, assumed, a param), radially jittered while convex.
  - The drop is vertical, or at most 0.05 m wide horizontally (derived). The measured audit deviation at a 90° corner is 34 mm for a 0.025 m drop and 47 mm for 0.10 m, before the mat's ±4 cm relief (E1). The 2-D bound for a straight edge is 0.167 m.
  - Floor faces take the `ground` slot. The step walls are the mat's cut edge.
- **PR4 · The mat open over every floor.**
  - *Windows.* A patch's window is its outline's axis-aligned bounding rectangle grown by 0.3 m (F-5).
  - *Edge boxes.* Per patch, one `straw` box per outline edge:
    - its inner face lies on the edge's line
    - outward, it reaches at least 0.3 m (F-5) and as far as the window's farthest corner
    - along the edge, it extends past both vertices by that same reach
  - *Why this works.* For a convex outline, a box on an edge's outer side never enters the outline, and the edge boxes together cover the window minus the outline. A self-check samples the window every 2 cm and asserts that every sample outside the outline lies in one of the patch's edge boxes.
  - *Fill.* Axis-aligned `straw` boxes fill the slab minus the windows: a sweep over the windows' edges.
  - *Placement.* Patches are placed so that no edge box enters another patch's outline or leaves the slab (a rejection test, as for the mounds).
  - Any other decomposition is fine if it passes PR7. This one gives about 85–103 shapes.
- **PR5 · One seeded definition** (design 11, made concrete).
  - One module, imported by `derive.py` and by `ground_patch`, returns the patches from (seed, size_m, share, size range, lodging azimuth and bias): for each, its centre, angle, step outline and the blob parts the albedo stamps inside it.
  - The outline encloses the blob grown by the bare feather (0.3 m, as tuned), so the strewn straws lie on the floor. The bare mask is 0 outside the outline.
  - The outline's size follows the sheet's 0.5–2 m, and the bare share (8 %) is measured on the outlines' area.
  - Mounds keep `MOUND_GAP_M` (0.5 m) from every window, so a mound always stands on the mat.
- **PR6 · The soft-top bound in `lib/audit.py`.**
  - The "no 2 cm zone, 5 cm over the whole top" rule applies to a landable shape whose material is:
    - `surface:belt_straw`, `surface:crater_spoil` or `surface:tilled`: a constant list, derived from `surfaces.json` at the 25 mm reference sink; the phase-3 compiler recomputes it from the map's table
    - or a plain id with `stiffness_n_per_m` ≤ 1000 N/m in `physics_materials.json`
  - Every other landable top keeps the 2 cm zone.
  - The audit line names these soft shapes, instead of "surface shapes".
  - *Tests:* under a ±4 cm relief, a `surface:asphalt` box fails at 2 cm and a `straw` box passes at 5 cm. S04's line names the soil box, the straw boxes and the mounds.
- **PR7 · Tests for "Bare patches are ground, not stains".**
  - (a) *No shape above a floor.* For each patch of the built S04 metadata, at 2 cm samples inside the outline shrunk by 0.06 m:
    - every shape's signed distance is positive at y = −D + 0.06, −0.03, 0 and +0.05
    - the soil box's signed distance is negative at y = −D − 0.01
    - This is physics' own check, independent of the audit.
  - (b) *The old state fails.* The same build with the straw boxes replaced by one 12 m `straw` box fails `box->mesh` by at least 120 mm. Assert on that direction (§2).
  - (c) *Mask and geometry agree.* The bare mask `derive.py` stamps, before the straw gaps are merged in, is:
    - at least 128 at samples inside each outline shrunk by the feather
    - 0 at samples outside each outline grown by 0.05 m
  - (d) *The build passes.* `build_asset.ps1 S04` prints every shape within 50 mm and the shape count.
- **PR8 · Shape order.**
  - The order is: `collision[0]` the soil box, then the fill boxes, then each patch's edge boxes in patch order, then the mound spheres.
  - `ground_patch`'s docstring states the order, so a logged (object, shape) reads as floor, mat or mound.

## For the orchestrator (spec and design text)

- **O1 · The S04 requirement.** It reads "the mat as a landable `surface:belt_straw` shape". It becomes: "the soil as a landable `surface:belt_straw` shape one mat depth below the mat top, and the mat over it as landable `straw` shapes open over every bare patch".
- **O2 · The scenario "Bare patches are ground, not stains".**
  - The step depth becomes "the mat depth D = 0.125 m".
  - The shape becomes "the `surface:belt_straw` soil box, exposed through the opened `straw` mat".
- **O3 · The union requirement of the asset-pipeline delta.** "A shape whose material is `surface:<id>` SHALL be held at 5 cm over its whole top" becomes PR6's soft-top rule. AST-05's ±2 cm for landing surfaces gets a note that soft landable tops (a reference sink of at least 25 mm) are held at 5 cm.
- **O4 · Design 6.** It is rewritten from §3 and PR1–PR4.

## Non-blocking notes

- **N1 · The audit metric.** `mesh->collision` takes min |sdf| over the primitives. A visible face inside the union that lies on a buried primitive face therefore reads 0 mm: in E1, the floor under a whole mat box sits on the soil box's top, 125 mm inside the mat box. Inside the union, the depth is at least max over i of (−sdf_i), and measuring that would close the gap. box→mesh catches every realistic case meanwhile.
- **N2 · Audit runtime.** `_covered` loops over every other shape for each point. At about 100 shapes, an AABB grid over the shapes keeps S04's audit near its 10 s.
- **N3 · The phase-7 contact law (mine), which S04 relies on:**
  - *A union filter:* drop a contact whose point lies strictly inside another shape of the same object (by more than about 1 µm, so the straw–soil interface stays a face).
  - *A per-object merge:* contacts of one object within 45° merge, and the deepest wins, as TH §2.3 does for ground; S04's overlapping straw boxes need it at their seams.
  - *The latch* (knowledge §7.5: a reversed normal keeps the latched plane). It covers a foot past a box's mid-plane (the wet F-5 margin of 8 mm) and past a mound sphere's centre (wet crater_spoil, 222 mm against radii of 159–173 mm).
  - *The wet `straw` rule.* The object table has no wet variant, so the law applies the sheet's mat rule to the `straw` id when wet: stiffness × 0.6 → 294 N/m, μ 0.35/0.28 (assumed, the D-021 proposal).
  - *The parallel case.* A foot driven through the straw meets the soil box, and the two then act in parallel. The terrain path's mat locks in series instead. At the reference load the foot does not reach the soil (51 of 125 mm).
- **N4 · Deviations of the asset path from the row** (the terrain path carries all of these):
  - no mat compliance in series with the soil: the rest on the mat is 51 mm, not 81 mm
  - no mat-depth variation (5–20 cm, hashed at 0.7 m)
  - flat tops: the ±4 cm relief's slope is visual only
  - no pitfalls: 0.02 /m² × 144 m² ≈ 3 expected (derived)
  - no straw hooks: AST-10, already stated
- **N5 · Gallery placement (phase 2).** A ground patch must stand where the terrain lies at or below its soil box's bottom, or in hole cells. Flush with terrain, the terrain contact (deepest wins within 45°) puts ground back over every floor.
- **N6 · The terrain path (phase 3, world-builder).**
  - The patch layout's real home is the terrain law: inside an outline, the straw cover channel is 0 and `GroundHeight` is unchanged.
  - A 0.5 m cover grid cannot hold a 0.5–2 m outline. So the shared definition becomes a layer-2 analytic feature that `SampleGround` evaluates and the renderer draws (REAL-05: the ground from one source).
  - The asset's relief noise and the row's micro-relief (0.04 m @ 0.7 m) should likewise become one function.
- **N7 · Task 1.7's tussock relief.** It must stay within 5 cm of the `straw` tops, or carry its own `straw` shapes as the mounds carry theirs. More visual loft (straw layers) changes no physics.
- **N8 · The bake.** Lying straws and stems over a patch must rest on the floor: the bake's `height()` includes the step. This is visual, and it is the parity AST-10 will need.
- **N9 · F-5 under D-021.** Wet max sinks raise the thickness F-5 needs to 2 × (1.6 · max_sink + 0.03 m): 0.32 m for belt_straw and 0.44 m for crater_spoil (derived). The contract's 0.3 m holds together with the law's latch. Recorded for the phase-3 hole-filler contract.

## Evidence

The scripts were run with `python -I` from the scratch folder against this worktree's `tools/blender`, and deleted afterwards.

**E1 · A 2 m slab with a 1 m square patch stepped down 0.125 m.** The soil box is 0.3 m thick with its top at −0.125. Four `surface:` mat boxes are open over the patch. The mesh is a heightfield on a 0.025 m grid. The lines are condensed.

```
--- opened mat, step drop over w = 0.025 m centred on the box face
  audit collision[0] box: box->mesh 34.031 mm at (-0.4999, -0.125, 0.4999), tolerance 50 mm
  audit collision[1..4] box: box->mesh 34.017-34.062 mm (the patch's corners)
  audit mesh: mesh->collision 20.801 mm at (-0.4792, -0.104, 0.4792) nearest collision[4], tolerance 50 mm
  problems: 0
--- opened mat, step drop over w = 0.1 m centred on the box face
  box->mesh 47.102-47.134 mm at the corners; mesh->collision 31.250 mm; problems: 0
--- opened mat, step drop over w = 0.2 m centred on the box face
  box->mesh 58.637-58.663 mm at the corners; mesh->collision 41.626 mm; problems: 5
--- mat box left whole over the stepped floor (the old state with a floor added)
  audit collision[0] box: box->mesh 0.000 mm at (-1, -0.425, -1), tolerance 50 mm
  audit collision[1] box: box->mesh 125.000 mm at (-0.25, 0, -0.25), tolerance 50 mm
  audit mesh: mesh->collision 62.500 mm at (-0.5, -0.0625, -0.5) nearest collision[1], tolerance 50 mm
  problems: 2
2-D bound, a straight edge with the box face centred in a drop of width w: (w/2)·D/sqrt(w^2 + D^2)
  w 0.05 -> 23.2 mm, 0.10 -> 39.0 mm, 0.1667 -> 50.0 mm, 0.20 -> 53.0 mm; largest w at 50 mm: 0.1667 m
```

**E2 · One landable box under a ±4 cm relief** (a 2 m heightfield, 0.05 m grid), with the current audit:

```
--- material surface:belt_straw  box->mesh 38.997 mm, mesh->collision 38.997 mm, tolerance 50 mm; problems: 0
--- material straw               box->mesh 38.997 mm ...; near gaps/landable 38.997 mm, tolerance 20 mm; problems: 2
--- material surface:asphalt     box->mesh 38.997 mm, mesh->collision 38.997 mm, tolerance 50 mm; problems: 0
```

**E3 · The arithmetic.**

```
foot area 1.7671e-04 m2
  belt_straw k 830.6 N/m, sink 30.1 mm, wet max sink 128 mm | belt_bare 2474.0, 10.1 mm | crater_spoil 494.8, 50.5 mm, wet 192 mm
  meadow_sod 1237.0, 20.2 | dry_crust 8835.7, 2.8 | tilled 706.9, 35.4 | yard_litter 6185.0, 4.0 | rubble 17671.5, 1.4
  weeds 2474.0, 10.1 | burnt_field 4417.9, 5.7 | asphalt, concrete_slab 17671.5, 1.4 | dirt_track 5301.4, 4.7
mat 0.050 m: km 265.5 N/m, compression 94.2 mm (lock 40 mm) -> compacts through, loaded top 10 mm above the soil
mat 0.125 m: km 492.6 N/m, compression 50.8 mm (lock 100 mm), loaded top 74 mm above the soil
mat 0.200 m: km 726.1 N/m, compression 34.4 mm (lock 160 mm), loaded top 166 mm above the soil
series mat+soil 309.2 N/m
rest on the mat: row law -80.9 mm, straw box -51.0 mm, belt_straw soil box -30.1 mm; on a bare patch -155.1 mm
  loaded step: row law 74.2 mm, straw box 104.1 mm, soil box 125.0 mm
wet bare patch: k 332.2 N/m, static sink 75.3 mm, max sink 128 mm
F-5 belt_straw: dry 0.220 m, wet 0.316 m | crater_spoil: dry 0.300 m, wet 0.444 m | belt_bare: dry 0.120, wet 0.156
S04 slab now 0.200 m: bottom face nearest beyond 100 mm; belt_straw max sink + 30 mm = 110 mm dry, 158 mm wet
mound r 0.166 / 0.159 / 0.173: apex 132 / 119 / 147 mm; apex-to-centre against dry 150 / wet 222 mm
bare 11.5 m2 at about 1.23 m2 each -> about 9 patches; N = 6: 85 shapes; N = 8: 103 shapes
polygon sagitta: N 6 134.0, N 8 76.1, N 12 34.1 mm per metre of radius
```
