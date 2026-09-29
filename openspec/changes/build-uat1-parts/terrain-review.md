# Physics review: `burnt_field` and terrain holes

**Change:** `build-uat1-parts` · **Task:** 2.2 · **Reviewer:** Physics Engineer · **Date:** 2026-09-29
**Inputs:**
- this change's proposal, design, tasks and `specs/terrain-holes/spec.md`
- `define-map-format`'s `map-format` and `world-query` specs and my `surfaces-review.md` (not archived on this branch yet)
- `game/maps/surfaces.json` at `a93aeca`, `game/maps/README.md`
- `game/src/world/{WorldQuery,Contacts,Catalog,WorldObjects,WindGrid,MapPng,HeightmapTerrain}.cs`
- reference notes T3b, W2, W4 and §7–§8

## Verdict

- **`burnt_field`: approved.** Every physics value stands as a starting value. The one required change is to remove the entry's own `"status"` line, which goes stale on sign-off (B-1).
- **Terrain holes: approved with changes.** The design is sound: a hole layer, render discard, fillers made of primitives, `surface:<id>` materials and QueryVersion 3. The spec, though, leaves undefined what the flight model reads over a hole, and it has no contract for the filler geometry. Task 2.3 may proceed once the orchestrator folds TH-1 to TH-12 (§4) into the spec and design. §2 and §3 give the physics basis for each point.

## 1. `burnt_field` values

**Basis:** the reference contact of the other 9 surfaces is 25 N on a 15 mm foot (141 kPa), with a 2.55 kg share per leg. It gives foot stiffness = bearing × A, static sink = 25 N / k, and ζ = c / (2√(k·m)).

**Notes T3b:** firm loam, unchanged under the char. Sink is 0–1 cm, the charred stubs are stiff but brittle, and the "brittle crunch" gives short damping spikes and a high ash plume.

| Field | Value | Check | Verdict |
|---|---|---|---|
| `soil.bearing_n_per_m3` | 2.5e7 | 4,418 N/m at the foot; static sink 5.7 mm | ✓ It sits between yard_litter (4.0 mm) and belt_bare (10 mm). Fire dries the soil and doesn't loosen it. |
| `soil.damping_ns_per_m3` | 3.6e5 | ζ = 0.30 | ✓ The same as yard_litter. Dry firm loam bounces less than crust (0.15). |
| `soil.friction_static` / `kinetic` | 0.60 / 0.50 | dry firm loam | ✓ The ash's slip belongs to the mat (0.45 / 0.35). At the mean 6.5 mm mat, the 10 mm blend gives ≈ 0.50 / 0.40, the grip of belt_bare's dusty top. This follows the yard_litter ruling. |
| `soil.max_sink_m` | 0.02 | 5.7 mm static | ✓ A hard landing reaches the firm layer at 2 cm, and from there the leg's own spring sets the bounce. |
| `soil.unload_stiffness_ratio` | 2 | 50 % of the loading energy goes into plastic set | ✓ Compacted firm soil, like yard_litter. The crunch belongs to the mat, not the soil. |
| `micro_relief` | 0.015 m at 0.6 m | peaks ≈ 3 cm | ✓ A harvested or grazed field is smooth, and fire doesn't reshape the ground. |
| `pitfalls` | 0.006 /m²; depth 0.03–0.15; radius 0.03–0.25 | 0.045 % per foot, 0.18 % per 4-leg landing | ✓ The same class as dry_crust: burrows and burnt-out root holes. A 3 cm radius is a vole burrow, and a 15 mm foot does fall into it. See X-9 for the scorch-spot crater. |
| grass (stubs) | 0.05–0.15 m; 100 /m²; 3–5 mm; 40 N/m | implied solid-section E = 1.06 GPa | ✓ About 30 % of intact straw tissue (belt_straw 3.7 GPa), which is right for charred straw. It is still about 400× stiffer at the tip than meadow grass, so it reads as "stiff". |
| grass hooks | 0.02; release 0.05–0.4 N | a charred hollow stub (4 mm, 0.4 mm wall, σ 5–10 MPa) snaps at 0.2–0.7 N when pulled between mid-height and tip | ✓ The release range doubles as the snap force of a stub caught on the fiber or a foot. |
| litter mat | 3–10 mm; 2e4 Pa; ζ 1.0; μ 0.45 / 0.35 | 1.0–1.7 kN/m under the foot; bottoms out at 4–8 N, then soil | ✓ **This is the notes' crunch.** It is a thin, critically damped first contact with no rebound, followed by firm soil. It is stiffer than thatch (3–8 kPa), which fits a char crust. |
| litter elements | 150 /m², 1–4 cm flakes | visual only | ✓ The mat carries the physics, as on the other surfaces. |
| `dust_emission` | 0.95 | the highest in the table | ✓ Loose ash. |
| `material.*` | — | — | Not reviewed (tech-artist). |

**Stubble needs no break-force field.** A stub gives at most about 0.7 N against at least 25 N per leg, so the pilot feels the burnt field through the mat's crunch and the firm soil, not through the stubs. There are 100 stubs/m² against a real 200–500 survivors. That undercount is accepted, as ruling #6 did for the other covers.

**B-1 (world artist, with 2.3):** delete `"status": "starting values, physics to sign off"` from the `burnt_field` entry. The table's top-level status already covers every surface. Do it in the same commit series as 2.3's golden re-record, so the content hash moves once.

## 2. What the flight model needs over a hole

### 2.1 `SampleGround` over a hole

| Field | Value where the point's own cell is a hole | Why |
|---|---|---|
| `Flags` | `Hole` (a new bit, 2) | |
| `TerrainHeight` | The triangle height as if there were no hole: finite, the lip level | The log gives "depth below the lip" as `TerrainHeight − y`. The wind grid measures "above terrain" from it. Renderer parity is unchanged. |
| `GroundHeight`, `SupportTop` | **−∞** | Fail-safe. `(SupportTop − y)·n.y` = −∞ ≤ 0, so code that forgets the flag sees "no ground here", which is the truth in a hole. The height above ground is +∞, so there is no terrain ground effect. |
| `Normal` | (0, 1, 0) | A valid unit vector that nothing uses |
| `MatDepth`, `CoverDensity` | 0 | No mat and no stems |
| `Feature`, `FeatureId`, `FeatureDepth` | none, 0, 0 | Pitfalls don't apply inside a hole |
| `Surface`, `BlendSurface`, `BlendWeight` | the layer values, as today | For the log only. Physics ignores them under `Hole`. |
| `OutsideMap` | never set together with `Hole` | A point outside the map continues the edge cell as ground |

**Rejected: NaN.** Every comparison with NaN is false. So `if (δ <= 0) return;` falls through, and the force becomes NaN. One NaN ends the replay, and nothing in the log can explain it. NaN is Jolt's hole encoding, and it must never leave `HeightmapTerrain`.

**Rejected: the trench floor.** `SampleGround` is 2-D, so it can't choose between a head roof, a stair and a floor stacked in one column (W4). It can't express walls either. And physics would count the floor twice, once from `SampleGround` and once from `StaticContacts` on the same box.

In a hole, the ground comes only from the fillers' shapes, through `StaticContacts` and `Raycast`. Rays skip the terrain triangles over hole cells (TH-3).

### 2.2 Soil materials on contacts, rays and geometry

- **`StaticContact` gains `byte Surface`.** A shape whose material is `surface:<id>` reports `Surface` = that surface's index and `Material = Catalog.NoMaterial`. Every other shape reports `Surface = 0` and its catalog material, as today.
- **`RayHit`:** a hit on a soil shape gives `Object` = the filler, `Material = NoMaterial` and `Surface` = the index. Two rules then hold everywhere:
  - `Surface ≠ 0` means soil (terrain or shape)
  - `Object < 0` means terrain
- **`ShapeGeometry` gains `Surface`.** The loader tags the shape's Jolt node with `material = "surface:<id>"`, `material_id = 65535` and `surface = <index>`.
- **Physics dispatch:** `c.Surface != 0 ? SoilLaw(world.Surface(c.Surface), …) : ObjectLaw(world.Material(c.Material), …)`.
- **Rejected: encoding surfaces as a material id range** (for example 0xFE00 + index). Any code that forgets the range would read that value as a valid-looking catalog id.

### 2.3 Legs, arms and the body on a filler

These rules belong to the physics flight-model change. They are listed here because the geometry contract in §3 is derived from them.

- **Sink.** The same elasto-plastic Winkler law as on terrain (`surfaces-review.md` §1), acting along the contact normal:
  - the penetration is `−Distance` of the foot sphere
  - bearing ×(b_ref/b)^0.3, the unload ratio, damping, and `max_sink` followed by the firm layer
  - on a wall it acts sideways, so a foot pushed into a wall dents it and leaves a plastic set

  Bearing is treated as isotropic for now. A free face lowers the capacity near the lip, which is within flight-tuning tolerance.
- **Friction and ploughing.** The bristle anchor uses the surface's soil μ. A sunk foot dragged along a wall or floor ploughs: `Fp = kl·b·δs²` against its velocity along the face.
- **No stem hooks on a trench wall.** Filler faces have no mat, relief, pitfalls, stems or hooks.
  - A leg on a wall holds by `μs·Fn` plus ploughing, and only while it is pressed in. It lets go as soon as the pilot pulls away.
  - Stem hooks exist on terrain only. Roots sticking out of cut walls are deferred (X-10).
- **Footprints.** The plastic set is keyed by (object, shape, contact point) in the same ring of recent footprints.
- **Merge at the lip.** Per foot, the terrain contact and any soil-shape contacts whose normals are within 45° of each other count as one contact, and the deepest wins. Contacts more than 45° apart, such as the floor and a wall, both act. This stops a double push when a foot straddles the hole boundary.
- **Guard.** A terrain penetration deeper than 0.25 m can only come from a cavity that reaches outside its hole cells, which F-2 forbids. Physics drops that terrain contact and logs `WORLD_BUG terrain_deep`.

### 2.4 Tether and fiber at a trench edge

- The fiber's segment capsules get ordinary contacts with the filler's lip boxes. `Geometry` gives a box, which has radius 0, plus `Surface`.
- **Lip radius.** The soil lip radius is a physics constant, **0.02 m**. A spade-cut lip crumbles to centimetres, so it is not a data field (X-11).
- **Effective radius:** `R_eff = max(0.02 m, T/(p_y·d_f), √(EI/T))`.
  - `p_y = bearing × max_sink`, the pressure at the firm layer, 336–560 kPa across the 10 soils. The soil yields under the fiber's line load T/R, so the fiber cuts a groove until `R_eff` carries it.
  - `d_f` is the fiber's outer diameter.
- **Result:** since `R_eff` ≥ 0.02 m, the glass bending stress `E·r/R_eff` stays at or below 0.22 GPa at any tension (for example 0.11 GPa at 10 N for a 0.5 mm fiber on burnt_field). That is well under the ~0.7 GPa proof level, so **a soil lip never breaks the fiber by bending**.
- **The lip's danger is friction.**
  - The capstan factor over the lip is `e^(μθ)`. Over 90° it is 1.9× at μ 0.4 and 3.5× at μ 0.8.
  - A fiber dragged back over a trench lip multiplies both the pull on the drone and the tension at the spool.
  - At a trench, "may or may not cut off" comes from that tension, and from sharp things in the trench: revetment timber (2 mm edge), rubble, wire.
- The fiber on soil uses the surface's soil friction. The table's μ values are defined for the polymer parts and the fiber coating (`surfaces-review.md`, Assumptions).

### 2.5 Rotors, ground effect and dust

- `Hole` switches off the terrain ground effect under a rotor.
- The rotor rays (±thrust axis, cast whenever an object is within 2 m) hit the filler shapes, and `RayHit.Surface` gives the `dust_emission` for prop-wash dust inside the trench.
- Recirculation between close walls comes from those rays, in the physics change.

### 2.6 Wind

- The 2 m wind grid can't resolve a 0.6–1.0 m trench, and a trench lies below the terrain, so it is not an obstacle.
- Below the lip in a hole (`TerrainHeight − y > 0`), physics will apply a cavity model: skimming flow with one recirculating vortex, a mean speed that is a small fraction of the lip wind, and turbulence from the lip's shear layer. It needs only `Hole`, `TerrainHeight` and the rays, so no world change is needed.
- **The loader must keep fillers out of the grid (TH-8).** Today `AddWind` gives a buried shape `TopM = 0` with porosity below 1. That creates an obstacle of height 0, and the wake model scales by x/H.

### 2.7 What is logged

- **Header:** the content hash (now including `holes.png`), QueryVersion 3 and format 1.1.
- **Per step, per contact:**
  - the source (terrain, soil shape, object or wire)
  - the object, shape, and surface or material
  - the penetration, split into elastic sink and plastic set
  - the normal, the force, and stick or slip
- **Per step, per sampled point:** the `GroundSample` flags (`Hole`, `OutsideMap`).
- **Events:**
  - a part enters or leaves a hole cell (row, column, `TerrainHeight − y`)
  - a first contact with a filler (object, asset id, shape, surface, sweep `Time`)
  - a fiber edge contact (object, shape, surface, wrap angle, tension in and out, `R_eff`, bend stress, break-draw index and value)
- **Guards:**
  - `WORLD_BUG terrain_deep` (§2.3)
  - `WORLD_BUG bottomless`: a part more than 0.5 m below `TerrainHeight` in a hole cell, where one downward ray finds no filler within 5 m

  A crash near a trench is then either traced to a named object and shape, or flagged as a world bug.
- **No new random draws.** Holes are data, and the fiber break draw is already logged.

## 3. Hole-filler contract (world artist, for 2.3's test trench, 3.2's cellar and 3.5's trench section)

The filler is the placed catalog object, or objects, whose collision describes the opening. The format has no mesh collision, so "the trench mesh" in the design means its primitives.

| # | Rule | Physics reason | Check |
|---|---|---|---|
| F-1 | **Sealed below.** Every point of a hole cell has filler collision under it. | Otherwise a part falls through the world. | A downward ray from `TerrainHeight + 0.5 m` on a 5 cm grid over every hole cell hits a filler shape. |
| F-2 | **The cavity stays inside the hole.** Every face of the cavity below `TerrainHeight` lies at least **0.25 m** horizontally inside the hole cells. | The deepest sink (0.12 m, crater_spoil), plus the firm-layer overshoot, the foot radius and the contact margin, comes to ≈ 0.15 m. A part pressed into a wall must stay over hole cells. Otherwise the terrain model finds it up to a metre deep in soil and pushes it up. | Every point within 0.25 m of the hole boundary, on the hole side, from `TerrainHeight` down to the cavity floor, on a 5 cm grid, is inside a filler shape. |
| F-3 | **The lip is level.** On the hole side of the boundary, the filler's top is within ±0.03 m of `TerrainHeight`. | No false step where a foot slides off the terrain onto the lip. | Sample along every boundary edge. |
| F-4 | **Buried outside.** Outside its hole cells, no filler shape rises above `TerrainHeight + 0.01 m`. | A buried box inside the relief is soil against soil, and the merge rule takes the deepest contact. A shape that sticks out is a ledge nobody can see. | A 5 cm grid over a 1 m band around each hole. |
| F-5 | **Thick and overlapping.** A shape that carries an open face is at least 0.3 m thick behind it. Shapes overlap by at least 0.2 m at every joint: floor under walls, walls into the floor. | `StaticContacts` reports the nearest face. A part sunk up to 0.15 m must stay nearest to the face it entered through, or the normal flips. | Asset review (5.1). |
| F-6 | **Open faces follow the visual within 0.05 m.** | The catalog's general 0.15 m could take 0.3 m out of a 0.6–1.0 m trench for a 0.57 m drone. | Horizontal rays across the cavity at 3 heights, as well as the downward rays of "Collision follows the visual". |
| F-7 | **Surfaces.** | Cut walls are undisturbed soil and the floor is trampled: firm loam, so `belt_bare` is the starting surface. `crater_spoil` is the loose spoil of the parapets. The parapets stay terrain (1 m samples raised 0.3–0.5 m, with cover), so they keep their dry grass, mat and hooks (notes W2). | Catalog review. |
| F-8 | **No wind obstacle.** A filler's buried shapes add nothing to the wind grid (TH-8). | §2.6 | The wind grid is unchanged by the test trench. |

Authoring advice (not blocking): paint the hole cells with the filler's surface and cover 0. The terrain's mat and relief blend over the 4 nearest cell centres, so they then taper toward the bare lip instead of stepping at it.

## 4. Spec and design changes (for the orchestrator)

**TH-1 · terrain-holes, "Holes in the heightfield".** Replace "through an optional hole layer or hole shapes" (two mechanisms, and "hole shapes" is never defined) with:
> A map package SHALL be able to mark terrain cells as holes with an optional layer `holes.png`: 8-bit greyscale on the surface-cell grid (`cells_per_side`² pixels, so a cell is `surface.resolution_m`, 0.5 m by default), 0 = ground, 255 = hole, any other value invalid. It has an `importer="keep"` sidecar like the other layers. Holed terrain SHALL be skipped by the renderer (including its depth and shadow passes) and by terrain collision, and each hole SHALL be filled by placed catalog objects that meet the hole-filler requirement. The layer is format 1.1. The content hash SHALL include `holes.png`, after `cover.png`, when it exists.

Add these scenarios:
- the validator rejects a value other than 0 or 255, naming the first pixel
- the validator rejects a size mismatch
- flipping one hole pixel changes the content hash, while a map without `holes.png` keeps its hash

The design's "1-bit per 0.5 m cell" becomes "0/255 on the surface grid", so `MapPng` reads it unchanged. In memory, keep it as one bit per cell (8.4 MB for a 4 km map).

**TH-2 · terrain-holes, "World query knows holes".** Replace its first sentence with:
> For a point inside the map whose own cell is a hole, `SampleGround` SHALL set `Hole`, keep `TerrainHeight` as the triangle height (the lip level), return −∞ for `GroundHeight` and `SupportTop`, +Y for `Normal`, 0 for `MatDepth` and `CoverDensity`, and no feature. The ground in a hole is given only by its fillers' shapes, through `StaticContacts` and `Raycast`.

Scenario *Sample over a hole:* on a grid across the test trench, lip-band and cavity points report exactly these values, and points 1 mm outside the boundary report ordinary ground.

**TH-3 · Raycast.** Add:
> A ray SHALL NOT hit the terrain where it crosses the triangles over a hole cell.

Scenario: a ray cast down into the trench, and one at 45°, hits the floor or a wall with that shape's `Surface`, and never the terrain.

**TH-4 · Soil materials.** Add:
> A collision shape's material MAY be `surface:<id>`, naming a surface of `surfaces.json`. Its contacts and ray hits carry `Surface` = that surface's index and `Material` = `Catalog.NoMaterial`, and `Geometry` gives the same `Surface`. All other contacts carry `Surface` = 0. The validator SHALL reject an unknown surface id, naming the asset and the id.

Rewrite scenario *Trench wall is soil*: "its `Surface` is the trench's surface (e.g. `belt_bare`), its `Material` is `NoMaterial`, and `Surface(index)` gives that surface's soil parameters". Replace `crater_spoil` with `belt_bare` there too (F-7).

**TH-5 · Micro-detail.** Add:
> `MicroDetailNear` SHALL NOT return an element rooted in a hole cell, nor a lying element whose tip lies in one. The renderer's near ring and far layer (task 2.5) SHALL draw nothing in hole cells.

Scenario: no element is returned over the test trench. Every other element keeps its `Id` and values.

**TH-6 · New requirement "Hole fillers".** Add F-1 to F-6 of §3 as the requirement, with the checks in §3 as its scenarios. They run on 2.3's test trench, and again on 3.2's cellar and 3.5's trench section.

**TH-7 · Existing world-query scenarios.** Add one sentence:
> The jump, normal, relief-RMS, density and lying-element scenarios exclude hole cells. "Matches the rendered surface" still holds for `TerrainHeight` everywhere, and for Jolt rays outside hole cells.

**TH-8 · Wind obstacles.** Add:
> A wind-volume shape adds nothing to a cell where its top is at or below the terrain.

This is a one-line guard in `AddWind`. Scenario: placing the test trench leaves the wind grid bit-identical.

**TH-9 · Jolt holes (design).** A NaN sample removes the heightfield triangles that use it, so the hole is vertex-granular on the 1 m grid and can't follow a 0.5 m mask. Confirm this on 4.7.2 in the selftest. Proposed design text:
> Terrain collision sets NaN on every corner sample of a 1 m quad that overlaps a hole cell, and adds a `ConcavePolygonShape3D` of the removed triangles clipped to the non-hole cells (Sutherland–Hodgman, as `WindGrid.ClippedArea` does), so Jolt matches the rendered ground exactly.

If that is skipped, the spec must say that Jolt terrain has gaps up to 1 m wide around holes. The flight model is unaffected either way, because it never reads Jolt (D-010).

**TH-10 · Golden.** Rewrite the *Golden re-recorded* scenario:
> A test trench (a straight section and a 30° section, 0.8 m wide and 1.5 m deep, a placeholder asset of boxes with `surface:belt_bare`) is added to `sample_patch` with its `holes.png`, its objects appended to `objects.json` so existing indices don't move. New golden cases: `SampleGround` across both sections (cavity, lip band, boundary ± 1 mm), rays down, at 45° and grazing, foot-sphere contacts on the floor, a wall and the inner corner, `MicroDetailNear` straddling a boundary, and the wind grid. Every case away from the trench keeps its recorded output. The content hash and `QueryVersion` 3 are the only other changes. `--golden` passes in Debug and Release.

(`burnt_field`'s golden update already landed in `a93aeca`, so "only cases that touch the new surface" no longer applies.)

**TH-11 · Performance.** Add to "Physics-grade performance":
> With holes present, the composite step (< 60 µs, zero allocations) and the micro-detail limits still hold.

`SampleGround`, `RayTerrain` and the micro-detail generator each add one bit test.

**TH-12 · Proposal and design wording.**
- The proposal's "Modified Capabilities: _None_" is right for `burnt_field`, but not for holes. They extend `map-format` (an optional layer, 1.1, `surface:<id>`) and `world-query` (`Hole`, `Surface` on contacts, the content hash, micro-detail, wind). Either list both as modified, with MODIFIED deltas, or state that `terrain-holes` extends them, so that the archive doesn't leave "five files" and the old field lists stale.
- In the design, "hands the ground over to the placed trench or cellar mesh" becomes "to its fillers' collision primitives".

## 5. Deferred (not needed for UAT-1)

- **X-9 · Placed pitfalls.** An `objects.json` entry with a centre, radius and depth, using the same wall law and id. The small form of T3b is a 2–6 m scorch disc around a 0.3–1 m crater. That crater can't be placed by the 1 m heightfield or by hashed pitfalls, and a hole filler is heavy for it. This belongs to `add-map-authoring`.
- **X-10 · Roots in cut walls.** Hook elements on filler faces near tree belts, from the same hash generator.
- **X-11 · `soil.edge_radius_m`.** An optional surface field, only if a lip other than loam needs one (for example a rubble lip with sharp brick edges).
- **X-12 · Thermals over dark fields** (notes T3b). The table's albedo is enough input. This belongs to the wind change, and needs no field.
- **Stub break force** (`break_n`). Only if flight tests show that stubble should be felt, which the numbers in §1 say it shouldn't.

## 6. Risks

- **Lip steps.** The bare lip band is 0.25–0.75 m wide and has no mat, relief or stems, while the terrain beside it has relief of ±2–11 cm and up to 20 cm of mat on belt_straw. A foot crossing the boundary can therefore step by a few centimetres. That is plausible for a trodden lip, and the authoring advice in §3 halves it. It is felt only when landing right at a trench.
- **Estimates, not measurements.** The soil lip radius (0.02 m), `p_y = bearing × max_sink`, isotropic wall bearing and the 0.25 m inset are my estimates from soil mechanics. They are physics constants, tuned in flight.
- **The burnt field has no close-up reference** (notes §9). The values follow the pilot's description and soil mechanics. The crunch and ash plume are judged at UAT-1 (look) and MVP (feel).
- **Jolt's NaN behaviour** on Godot 4.7.2 is from the Jolt design and is unverified here (TH-9).
