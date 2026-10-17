# Physics review: surface table, object materials, world-query requirements

**Change:** `define-map-format` · **Task:** 2.2 (plus early input for 3.x; the formal API review is 3.5)
**Reviewer:** Physics Engineer · **Date:** 2026-09-23
**Inputs:** `game/maps/surfaces.json`, `game/maps/README.md`, `game/assets/catalog.json`, the three spec deltas, `design.md`, D-002/D-008/D-009, reference notes §6–7, manifesto §2 items 1, 2, 5 and 6 and §3 (legs / rails).

## Verdict

**Changes requested. The field set is approved as the base, and the values are approved as starting values, except for the corrections in §4.**

Every existing field has an SI unit and a physical range. The elastic Winkler core (bearing, damping, friction, max sink) is the right starting point for a 1 kHz, 4-leg contact.

Four physical effects that the manifesto and the notes ask for have no field yet:
- plastic sink (legs settle and absorb landing energy)
- the straw and leaf mat (the support surface sits 1–20 cm above the soil)
- the pull force of a hooked stem (how hard a stuck leg holds)
- directional furrows

Objects have no contact material, so the rails, roofs, bricks and wires can't be modelled.

Sign-off follows when the World Artist has applied §3 and §5 and the orchestrator has amended the specs per §7. Format 1.0 is not frozen yet, so **everything folds into 1.0**. The "minor / breaking" column says what each change would have been after the freeze.

## Assumptions

- **The drone** weighs 4–12 kg on four plastic legs. The table's reference contact (25 N on a 15 mm diameter foot, 141 kPa) stays as the calibration point. The real foot size, leg stiffness and leg damping are drone parameters, which physics owns.
- **The step** is fixed at 1 kHz. Per leg, the contact is a closed-form 1-D series solve (mat, then soil, then firm layer) with no iteration, so it costs microseconds for 4 legs.
- **Friction values** are for the drone's polymer parts: PETG/TPU/nylon feet, printed skids, and the fiber's acrylate coating, dry. Carbon frame parts are close enough, and physics may apply a per-part factor later.
- **No water or mud** is in the reference (notes §8), so soil adhesion and suction are left out.

## 1. The leg–ground contact these fields feed

Each step, for each leg (the foot tip at `p`, foot diameter `b`, foot area `A = πb²/4`):

1. **Sample.** `SampleGround(p.xz)` gives the ground height `Hg` (terrain + relief + ridges + pitfalls), the normal `n`, the per-kind mat depths `Dmat`, the support top `Hs = Hg + ΣDmat`, and the blend weights of the surfaces around the point.
2. **Penetration below the mat top.** `δ = (Hs − p.y)·n.y`. When δ ≤ 0 there is no ground force, though stems can still act.
3. **Soil**, as an elasto-plastic Winkler model (Wong's load/unload rule):
   - Virgin loading: `kl = bearing·(b_ref/b)^0.3`. The exponent 0.3 is a physics constant, taken from Bekker's k_c/b + k_φ over foot and body widths of 15–150 mm (0.1 for sandy loam, 0.3 for clayey loam).
   - Unloading and reloading: `ku = unload_stiffness_ratio·kl` from the per-leg plastic set `zp`. So `p = min(ku(δs − zp), kl·δs)`, and `zp` is updated whenever the kl branch is active.
   - Past `max_sink_m`, a firm layer at 1e8 N/m³ (a physics constant) takes over.
   - Viscous damping `damping_ns_per_m3·A·δ̇s` acts on the soil. The total force is clamped at ≥ 0.
4. **Mat**, in series with the soil at equal force:
   - The load spreads over `Am = π(b/2 + D/2)²`, where D is the mat depth, so the mat's stiffness is `km = modulus·Am/D` (N/m).
   - It compresses up to 80 % of D and then carries load as a solid (a physics constant).
   - Its damping is `2ζ√(km·m_eff)`, with m_eff the rigid-body effective mass at the contact.
   - The solve is piecewise-linear, with at most 3 branches.
5. **Friction** uses a stick-point (bristle) anchor with tangential stiffness `0.5×` the current normal stiffness. It holds up to `μs·Fn` and slides at `μk·Fn`. μ blends from the soil's to the mat's over the first 10 mm of mat depth.
6. **Ploughing:** a sunk foot pushes soil ahead of it at bearing pressure, `Fp = kl·b·δs²`, against the foot's lateral velocity. This is what trips a drone that lands on soft ground with forward speed (manifesto item 6) without any new field.
7. **Footprints.** `zp` belongs to the footprint. It resets when the foot lands elsewhere, and a ring of recent footprints makes a re-landing in the same prints find compacted soil. The world stores no marks (design non-goal kept).
8. **The leg itself** is a drone-side spring-damper in series. It dominates the bounce on hard ground (manifesto item 6).

**Stability.** Every soil and mat branch at the reference foot stays at or below 26.5 kN/m (the stiffest is rubble unloading, 1.5 × 17.7 kN/m), so ω·dt ≤ 0.10 at 1 kHz for a 2.5 kg leg share. Body contacts (arm on rubble 3.7e5 N/m, coil on the firm layer 8.9e5 N/m, frame on the rails ~1e6 N/m) give ω·dt ≤ 0.7, which is stable with semi-implicit Euler. Rigid materials never enter the step directly, because physics puts the drone part's compliance in series.

Checked against the current values (25 N, 15 mm foot, 2.55 kg share):

| Surface | Foot stiffness N/m | Static sink | Damping ratio | Notes §7 |
|---|---|---|---|---|
| meadow_sod | 1,237 | 20 mm | 0.58 | 1–3 cm, cushioned ✓ |
| dry_crust | 8,836 | 2.8 mm | 0.15 | little sink, bounce ✓ |
| crater_spoil | 495 | 51 mm | 0.80 | 3–8 cm ✓ |
| belt_straw (soil) | 831 | 30 mm | 0.35 | soft moist loam ✓ |
| belt_bare | 2,474 | 10 mm | 0.39 | firm ✓ |
| tilled | 707 | 35 mm | 0.69 | 2–5 cm ✓ |
| yard_litter | 6,185 | 4.0 mm | 0.30 | hard ✓ |
| rubble | 17,671 | 1.4 mm | 0.10 | rigid, bounce ✓ |
| weeds | 2,474 | 10 mm | 0.49 | firm ✓ |

## 2. Rulings on the seven raised issues

| # | Issue | Ruling |
|---|---|---|
| 1 | The soil is elastic only, and the modulus depends on foot size | **Accepted, change.** Add `soil.unload_stiffness_ratio`. The virgin modulus is still `bearing_n_per_m3`, so static sinks don't change. Add a table-level `soil_reference_diameter_m` (0.015) so the values say what foot they are for. Physics scales for other widths with exponent 0.3. A yield-pressure model was rejected: it needs 2 more fields per soil for the same landing behaviour. The loaded 141 kPa is at or near the bearing failure of soft loam (c 20–50 kPa × N_c ≈ 6), so plastic sink is real at this load. |
| 2 | No straw-mat depth | **Accepted, change.** The mat moves the support surface 5–20 cm above the soil, which is more than the soil sink itself, and it is what makes "one leg rests higher than another". Add an optional `mat` block per cover entry. Its depth is scaled by that cover's `cover.png` channel, so painted straw and physical straw agree. The mat drapes into pitfalls and does not bridge them (bridging depends on hole size versus straw length, which is not worth a model). The hole stays hidden because it isn't rendered. |
| 3 | Tilled furrows are directional, the relief isotropic | **Accepted, change.** Add an optional `micro_relief.ridges` block (amplitude, spacing, azimuth). A heading across the furrows rocks the drone and a heading along them doesn't. That is a pilot-visible effect on a reference surface. Each field direction is a surface variant (`tilled_000`, `tilled_045`, …), so no new layer is needed. |
| 4 | Rubble porosity 0.40 was picked | **Remove `soil.porosity` from every surface.** The contact law does not use it. "Loose / porous soil" is carried by bearing, unload ratio and max sink, and rubble voids are pitfalls. A field that does nothing invites tuning that changes nothing. The notes' porosities can stay in the README rationale. (0.40 is plausible for brick debris, but that's moot.) |
| 5 | Lateral stiffness is at the tip, so physics must scale it | **Accepted as defined, plus a clarification.** At contact height h, physics uses `k(h) = 3EI/h³` with `EI = k_tip·L³/3`. It caps the force when the stem slides off (push > h/2 or past the tip) and at buckling `π²EI/(4L²)` for axial pushes. **Clarify:** the table value is the tip stiffness of an element of *mean* length and *mean* diameter. The generator scales each element by `(d/d̄)⁴·(L̄/L)³`, so all elements of a cover type share one tissue modulus. The current values imply a solid-section E of 0.5–4 GPa for grass and straw and 2–11 GPa for twigs, all plausible except tilled straw (§4). |
| 6 | Densities count only stems that touch a leg | **Accepted, no change now.** Meadow is 400 elements/m² against about 2,000–4,000 real stems/m², so the mean lateral grass force on a leg is about 5–10× low. That force is well under 1 N on this drone (≈ 0.1 N for a 15 mm leg), so flight behaviour doesn't change, and the jitter is somewhat spikier. The cushion that grass gives comes from the mat (issue 2), not from elements. Weeds (60/m², 5–15 mm stalks) are counted fully, and that is where stems matter. If flight tests show grass jitter reads wrong, add an optional `stems_per_element` weight (default 1) as a minor bump. |
| 7 | Objects have no contact properties | **Accepted, change.** Add a catalog `materials` table and a `material` on every colliding asset (§5). **No restitution field:** on rigid materials the drone's leg and frame compliance sets the bounce, and on compliant objects the bounce emerges from stiffness plus damping ratio. Add `edge_radius_m`, which the fiber model needs (§5). |

## 3. Field changes, exact

"Owner" is who applies each change. After a freeze, *minor* would mean an optional field with a default that old readers ignore, and *breaking* would mean new data fails an old validator.

### 3.1 `surfaces.json`

| Field | Unit | Range | Default | How the contact model uses it | Class |
|---|---|---|---|---|---|
| `soil_reference_diameter_m` (top level, next to `surfaces`) | m | 0.005–0.1 | 0.015 | The foot diameter at which `bearing_n_per_m3` and `damping_ns_per_m3` are defined. The modulus for a contact of diameter b is ×(b_ref/b)^0.3. | minor |
| `soil.unload_stiffness_ratio` | — | 1–100 | 1.0 (purely elastic, the current meaning) | `ku = ratio·kl`. The fraction of loading energy kept as plastic sink per cycle is 1 − 1/ratio. | minor |
| `soil.porosity` | — | — | — | **Removed.** Not used. | breaking (free now) |
| `soil.bearing_n_per_m3` | N/m³ | unchanged | — | **Definition:** the virgin-loading modulus at the reference diameter | wording |
| `soil.max_sink_m` | m | unchanged | — | **Definition:** the total sink (elastic + plastic) at which the firm layer takes over | wording |
| `micro_relief.ridges` (optional block) | — | — | absent = none | Adds a C¹ periodic ridge profile to the ground height, with no transcendental functions (see W-13) | minor |
| `ridges.amplitude_m` | m | 0–0.3 | — | Half the crest-to-trough height | minor |
| `ridges.spacing_m` | m | 0.1–5 | — | Crest to crest | minor |
| `ridges.azimuth_deg` | deg | 0–180 | — | The direction the crests run. 0 = along X (east–west). Positive turns toward −Z, as object yaw does. | minor |
| `cover[].mat` (optional block) | — | — | absent = no mat | A compressible layer on the soil. Depth = hash noise in [min, max] at the surface's relief wavelength × this cover's channel. The mats of all covers stack in series. | minor |
| `mat.depth_m` | [min, max] m | 0–0.5, min ≤ max | — | Uncompressed depth at channel 1.0. Gives `Hs`. | minor |
| `mat.modulus_pa` | Pa | 10–1e6 | — | Compressive modulus (pressure per unit strain). `km = modulus·Am/D`. | minor |
| `mat.damping_ratio` | — | 0–2 | — | `c = 2ζ√(km·m_eff)` | minor |
| `mat.friction_static`, `mat.friction_kinetic` | — | 0–2, kinetic ≤ static | — | Foot friction on the mat, blended in over its first 10 mm of depth | minor |
| `cover[].hook_release_n` | [min, max] N | 0–500, min ≤ max | [0.5, 2.0] | The pull at which a hooked element lets go (it slips off, pulls out or breaks). Drawn per element from its hash. | minor |
| `cover[].lateral_stiffness_n_per_m` | N/m | unchanged | — | **Definition:** the tip stiffness at mean length and mean diameter. Scaled per element (issue 5). | wording |
| `cover[].height_m`, `cover[].diameter_m` | m | lower bound **> 0** when `stems_per_m2 > 0` | — | Physics divides by the means | validator |

Proposed values for the new fields:

| Surface | unload ratio | mat (on the cover entry) | hook_release_n per cover | ridges |
|---|---|---|---|---|
| meadow_sod | 3 (root-bound sod, springy) | grass: depth [0.01, 0.04], 8e3 Pa, ζ 0.5, μ 0.50/0.40 | grass [0.5, 3] | — |
| dry_crust | 1.5 (hard crust, mostly elastic, the bounce case) | — | grass [0.5, 2] | — |
| crater_spoil | 8 (loose spoil, a thud with no bounce) | — | — | — |
| belt_straw | 5 (soft moist loam) | straw: depth [0.05, 0.20], 4e3 Pa, ζ 0.3, μ 0.45/0.35 | straw [1, 5], grass [1, 4], twigs [0.3, 3] | — |
| belt_bare | 3 | — | twigs [0.3, 5], litter [0, 0] | — |
| tilled | 8 (loose) | — | straw [0.5, 2] | amplitude 0.05, spacing 0.5, azimuth 0 |
| yard_litter | 2 (compacted) | litter: depth [0.01, 0.03], 3e3 Pa, ζ 0.8, μ 0.35/0.25 | litter [0, 0], twigs [0.3, 3] | — |
| rubble | 1.5 (rigid pieces settling) | — | twigs [10, 60] (jutting timbers) | — |
| weeds | 3 | — | grass [5, 30] (rooted 5–15 mm stalks) | — |

What the mats do under the reference foot:
- **Meadow thatch and yard litter** compact fully under 25 N, so they are a soft first 1–4 cm (≈ 150–480 N/m) before the soil. This is "grass cushions the contact" and "leaves slide".
- **Belt straw 12.5 cm deep** compresses about 5 cm and carries the leg (≈ 490 N/m). At 20 cm it compresses about 3 cm, and at 5 cm it compacts through to the soil. So legs on one mat rest at different heights, which is the manifesto's uneven-liftoff case.

Why the hook forces are sized this way: 5 N at a 0.3 m arm, held for 50–200 ms at liftoff, tilts a 10 kg drone by about 1–4° and leaves it with a roll rate the pilot has to catch (manifesto item 1). Weeds and rubble timbers can hold harder than the excess thrust, so the drone flips, as it does in real life.

### 3.2 `catalog.json`: see §5

## 4. Value sanity

Only the values that are clearly off are changed. Everything else in the table is plausible and goes to flight tuning.

| Value | Now | Proposed | Why |
|---|---|---|---|
| `tilled` cover straw `lateral_stiffness_n_per_m` | 0.3 | **5** | It is the same straw tissue as belt_straw (solid-equivalent E ≈ 3.7 GPa), but it implies E ≈ 0.2 GPa. At 0.2 m and 3 mm, a stem of the same tissue is (0.65/0.2)³·(3/3.5)⁴ ≈ 18× stiffer at the tip, which gives about 5.5 N/m. |
| `yard_litter` `soil.friction_static` / `kinetic` | 0.45 / 0.35 | **0.60 / 0.50** | The leaf slipperiness moves to the litter mat (0.35 / 0.25). Where the artist paints no leaves, compacted yard soil grips like dry loam. *Only valid together with the mat.* |
| `tilled` `micro_relief` | 0.035 m @ 0.7 m | **0.02 m @ 0.3 m**, plus ridges | The ridges now carry the furrows (5–15 cm ridges, notes T3), and the isotropic part is clods. The total RMS is 0.041 m. |
| `soil.porosity` (all 9) | present | **remove** | Issue 4 |

Checked, fine, and left as they are:
- all bearing and damping values (the table in §1)
- all friction values except yard_litter
- all max sinks
- the pitfall ranges (meadow ≈ 1 % chance per foot per landing, about 4 % per landing, which is "unexpected")
- the stem geometry and densities
- the hook probabilities

Tuning notes:
- **belt_straw straw at hook probability 0.15:** about 3 lying straws touch each foot, so a leg is hooked about 39 % of the time and about 1.6 legs per liftoff. That may read as "every time" rather than "sometimes". At 0.05 it would be about 14 % per leg. This is for flight tuning, so it is not changed here.
- **rubble relief 0.12 m at 0.6 m** gives RMS slopes of about 40–50°, steeper than the friction angle, so feet mostly slide into troughs. Real rubble has flat brick faces with sharp edges. That is a representation gap (smooth noise), not a wrong number. Revisit with debris chunks (deferred item X-1).
- **Viscous damping:** on the soft plastic soils (spoil, tilled, belt_straw) the viscous damping now overlaps with the plastic loss. Expect to lower it in tuning, not now.

## 5. Object contact materials (catalog proposal)

Add a top-level `materials` object to `catalog.json`. Every asset that has collision names a `material`, and any collision shape may override it with its own `material`. The validator rejects unknown ids.

| Field | Unit | Range | Default | Use |
|---|---|---|---|---|
| `friction_static` | — | 0–2 | required | Stick-point limit for feet, skids and fiber on this material |
| `friction_kinetic` | — | 0–2, ≤ static | required | Sliding |
| `stiffness_n_per_m` | N/m | 1e2–1e8 | absent = rigid | The object's local stiffness under a point load (panel flex, a batten bending), in series with the drone part |
| `damping_ratio` | — | 0–2 | 0.05 | With `stiffness_n_per_m` only, as `c = 2ζ√(k·m_eff)` |
| `edge_radius_m` | m | 1e-4–0.05 | 0.002 | The radius of box edges where the fiber bends over (see below). Cylinders, capsules and wires use their own radius. |

Why edge radius matters: the 125 µm glass of the fiber bent to radius R carries a stress of `E·r/R`. That is ≈ 12.6 GPa over a 0.3 mm sheet edge, 4.3 GPa over a 1 mm edge, 2.2 GPa over a 2 mm edge and 0.75 GPa over 6 mm. The effective radius is `max(R_edge + coating, √(EI/T))`. A seeded strength draw per snag event then decides whether it breaks. That is the manifesto's "may or may not cut off", driven by a physical parameter.

Starting values:

| id | μs / μk | stiffness | ζ | edge radius | Rationale |
|---|---|---|---|---|---|
| `steel` (rails, bars, gates, posts) | 0.35 / 0.28 | rigid | — | 0.001 | Polymer or carbon on dry mild steel with light rust. Rolled or sawn edges. |
| `sheet_metal` (corrugated roofing, sheets) | 0.30 / 0.25 | 2e4 | 0.03 | 0.0003 | 0.5 mm corrugated sheet over a ~1 m purlin span bends at ≈ 5.8e4 N/m at mid-span, and local crest denting softens that further. It rings (low ζ), so landings on it drum and bounce. Its half-thickness edge cuts the fiber. |
| `timber` (planks, beams, logs, poles, and trunks for now) | 0.45 / 0.35 | rigid | — | 0.002 | Weathered sawn wood. A batten asset can take `stiffness_n_per_m` ≈ 1e5 later (50 × 25 mm over 0.6 m). |
| `masonry` (brick, block, concrete, render) | 0.65 / 0.55 | rigid | — | 0.002 | Polymer on brick or concrete 0.5–0.7. A worn arris; fresh breaks are sharper. |
| `cable` (overhead wires) | 0.30 / 0.25 | rigid locally | — | — | PVC/PE insulation or weathered aluminium. Lateral compliance comes from span tension, `T = w·L²/(8·sag)`, which physics derives assuming 2,000 kg/m³ over the wire diameter. A 12 mm, 30 m span with 0.6 m sag gives T ≈ 416 N and about 55 N/m at mid-span. |

Placeholder assignments:
- `house_box` → masonry
- `shed_box` → timber
- `tree_proxy` → timber
- `pole` → timber (notes O2: wood or concrete, uncertain)
- `gate_frame` → steel
- `cable` → cable
- `household_junk` → none (visual only)

Two more catalog needs:
- **`wind_volume`** (optional list of shapes, in the same schema as `collision`; default = the collision shapes). `tree_proxy` collides only as a 0.1 m trunk, but its crown is a 3 m-radius sphere at y = 7 m. Derived from collision, a tree belt, the notes' main turbulence source, would be invisible to the wind. For `tree_proxy`, use a sphere of radius 3.0 at [0, 7, 0]. Define `wind_porosity` as the optical porosity of the wind volume seen side-on.
- **`launch_rails`** (legs = FALSE, manifesto §3): two parallel steel bars, material `steel`. Their dimensions come from the user (question Q1). They need to exist as a catalog asset, placed either in the map at a start point or at runtime (W-11).

## 6. World-query requirements for phase B

### 6.1 Calls, rates and batches

| Call | Caller | When | Max per step | Max per second |
|---|---|---|---|---|
| `SampleGround(ReadOnlySpan<Vector2> xz, Span<GroundSample> out)` | 4 feet, 4 rotor centres (ground effect), ≤ 4 low body points, ≤ 32 fiber nodes | every 1 kHz step, for points within 3 m of terrain (fiber nodes within 0.5 m) | 44 | 44,000 |
| `StaticContacts(in Capsule c, float margin, Span<StaticContact> out)` (a sphere is a zero-length capsule) | 4 feet, 4 arms, 4 prop disks, 2 body/coil, ≤ 32 fiber segments. Each is **swept** from the previous step's pose. | every step; an internal broadphase returns 0 quickly when nothing is within the margin | 46 | 46,000 |
| `Raycast(ReadOnlySpan<Ray> rays, float maxDist, Span<RayHit> out)` against terrain and objects | 4 rotors × ±thrust axis (ground, roof and ceiling effect) | every step while an object is within 2 m (terrain-only is covered by SampleGround) | 8 | 8,000 |
| `MicroDetailNear(Vector3 center, float radius, KindMask kinds, Span<MicroElement> out)` | the drone (r = 2 m) and fiber touch points (≤ 2, r = 1 m) | when a centre has moved > 0.5 m since its last query and it is below the maximum cover height + 1 m. Physics caches the results. Typically ≤ 40 Hz. | — | ≤ 120 |
| `GapsNear(Vector3 center, float radius, Span<Gap> out)` | turbulence (jets through openings), logging | ≤ 20 Hz, within 20 m of an object that has gaps | — | 20 |
| `WindGrid` (read-only span plus dimensions) | turbulence precompute | at load, when the wind direction changes by > 5°, and when the precompute window moves | — | rare |
| `GetSurface(byte)`, `GetMaterial(ushort)`, `SoilReferenceDiameter`, `ContentHash` | contact model, telemetry | at load | — | — |

Budget: one worst-case step (44 ground samples + 46 swept capsules + 8 rays) under **60 µs** on the dev machine. That is ≤ 6 % of the 1 ms step, with zero managed allocation.

### 6.2 What each call returns (world frame, Y up, metres)

**`GroundSample`**
- `TerrainHeight` (m): the rendered triangle at this point
- `GroundHeight` (m): terrain + relief + ridges + pitfalls. This is the soil surface.
- `Normal` (unit vector): of the `GroundHeight` function on the rendered facet
- `MatDepth` (float4, m): uncompressed mat per kind (grass, straw, twigs, litter)
- `SupportTop` (m): `GroundHeight + ΣMatDepth`
- `CoverDensity` (float4, elements per m²): effective values (table × channel)
- `Surface` (byte): this point's own cell
- `BlendSurface[4]` (byte) and `BlendWeight[4]` (—): the bilinear blend over the 4 nearest cell centres, which is also used for the relief (W-2). Physics blends the soil and mat parameters with the same weights.
- `Feature`: none or pitfall (debris later)
- `FeatureId` (uint32) and `FeatureDepth` (m, pitfall depth at this point), for the log
- `Flags`: `OutsideMap`

**`MicroElement`** (blittable, about 56 bytes)
- `Id` (uint64): stable, from cell, kind, slot and seed. Keys hook state and the log.
- `Kind` (byte)
- `Surface` (byte)
- `Base` (m): the root. Standing grass roots at `GroundHeight`; lying straw and twigs rest on `SupportTop`.
- `Direction` (unit vector): from base to tip
- `Length` (m) and `Diameter` (m)
- `TipStiffness` (N/m at its own length, scaled per issue 5)
- `HookRelease` (N)
- `Hooks` (bool)

**`StaticContact`**
- `Point` (m)
- `Normal` (unit vector, out of the object)
- `Distance` (m, signed; negative = penetration, positive = within the margin)
- `Material` (ushort)
- `Object` (int: the index in `objects.json`, or a runtime object id)
- `Shape` (ushort)
- `WireParam` (0–1 along the polyline, or −1)

**`RayHit`**
- `Distance` (m, or +∞)
- `Point`, `Normal`
- `Material`
- `Object` (−1 for terrain)
- `Surface` (for terrain hits)

**`WindCell`** (2 m grid)
- `TopM` (m above terrain)
- `BaseM` (m above terrain; the crown base, 0 for walls)
- `Porosity` (—): defined in W-9

**`Gap`**
- `Center` (m)
- `Normal`, `Up` (unit vectors)
- `Width`, `Height` (m)
- `Object`, `Name`

All variable-length results go into caller-provided spans in a canonical order and return the true count, so an overflow is reported and never silently dropped.

### 6.3 How terrain, relief and pitfalls feed a leg

The triangulated terrain height is the macro surface the pilot sees. Micro-relief, ridges and pitfalls add sub-grid shape on top of it, and the mat adds a compressible layer above that. The leg runs the §1 solve against `SupportTop`, with its penetration split between the mat and the soil.

- **Pitfalls** lower `GroundHeight`, and the mat follows them down, so a foot over a hole finds nothing until the hole's floor. That is the drop "without warning".
- **Slopes and creases** come from the normal. The rendered facet's crease can be up to about 30° on crater rims. Relief and ridges tilt the foot's support, so the drone tilts and slides unevenly at touchdown and liftoff.
- **`FeatureId`** in the log lets QA say "leg 3 dropped into pitfall 0x…, 0.21 m deep" rather than guess.

Point sampling is valid while relief wavelength ≥ 10 × foot diameter. All current wavelengths are ≥ 0.3 m.

### 6.4 How micro-detail becomes forces and snags

Physics bins the cached elements into a local 0.25 m grid and tests each drone primitive only against the cells its bounding box overlaps. That is about 100–300 element tests per primitive in meadow.

- **Standing grass and weeds** (rooted cantilevers): a push-through distance d at height h gives a lateral force `3EI/h³·d` on the drone at the contact. It is capped at slide-off and at buckling (§2 issue 5). This produces the touchdown jitter, the drag when skimming weeds, and the fiber dragging through stalks.
- **Lying straw** (lodged, rooted): a light cantilever from its root. Its main role is hooking, and its bulk is the mat.
- **Twigs** (loose rods): under a foot, a twig is a rigid roller. The support is the rod's top with its own normal, rolling across its axis has μ ≈ 0.05, and sliding along it uses normal friction. A loaded foot rolls or slides off. Twigs snag the fiber like capstans of radius d/2.
- **Litter** is visual only; the mat carries its physics. Physics passes a `KindMask` without litter to skip those elements.
- **Hooks:** an element with `Hooks` that touches a leg or the fiber attaches at the contact point. It pulls back toward its base with a spring that reaches `HookRelease` over 30 mm (a physics constant), then lets go. This is deterministic and replay-exact, it is keyed by `Id` so it survives cache refreshes, and it is logged.
- **Props:** an element that enters a prop disk is logged as a strike. The strike's torque and damage model comes in the physics change, and may later want a cut energy per cover type (X-6).

### 6.5 What the wind-obstacle grid must give

The turbulence model is precomputed per wind direction on the 2 m grid, in a window around the drone.

1. For each cell, march upwind up to 30 H (cap 300 m) and record the nearest obstacle: its distance x, top H, base B and path porosity β (the product of the cell porosities along its crossing). Terrain shelter (trenches, banks) comes from the heightfield directly.
2. **Mean profile:** a log law with z0 and d that physics derives from the surfaces and cover. Roughly z0 ≈ 0.1 h_cover and d ≈ 0.67 h_cover; for bare ground, z0 comes from the relief amplitude.
3. **Wakes:** the speed deficit and the turbulence boost are functions of x/H, z/H and β. Porous-fence behaviour applies for β > 0.3. Solid objects (β < 0.1) get bluff-body recirculation, with reverse flow within about 2–3 H below H.
4. **Around obstacles:** speed-up over the top at about 1.2 H, a jet under the crowns when B > 0, exponential attenuation inside a canopy, and jets through `GapsNear` openings.
5. **Gusts:** seeded Dryden/von Kármán (the seed is logged), with intensity from u* and the local wake boost.

Each step is then a bilinear lookup at the rotors, the body and the fiber nodes. So the grid needs `TopM`, `BaseM`, a path-composable `Porosity`, and whole-grid access (W-9).

### 6.6 Determinism, threading, logging

These must hold for any flight to be replayed and explained:
- The flight model may run on its own thread, and QA replays flights headless on another machine.
- `SampleGround`, `MicroDetailNear`, `StaticContacts` and `Raycast` must be pure C#, reentrant, free of scene-tree state and allocation-free.
- Results must be bit-identical across x64 machines (W-13).
- The world's content hash goes into every log header.

## 7. Spec gaps (for the orchestrator)

### `map-format` spec

- **MF-1 · Requirement "Surface table".**
  - Replace "porosity (0–1)" with "an unload stiffness ratio (≥ 1) for plastic sink".
  - Add "a table-level soil reference contact diameter (m) at which the soil moduli are defined".
  - Add "optional directional ridges: amplitude (m), spacing (m), azimuth (deg)".
  - Per cover entry, add "a hook release force range (N)" and "an optional mat: depth range (m), compressive modulus (Pa), damping ratio, static and kinetic friction".
  - State: "lateral stiffness is the tip stiffness of an element of mean length and mean diameter".
- **MF-2 · Scenario "Required fields".** Add the out-of-range examples unload ratio < 1, mat depth min > max, and kinetic > static friction in a mat or a material.
- **MF-3 · Requirement "Asset catalog and objects".**
  - Add "a shared material table: static and kinetic friction, optional local contact stiffness (N/m) and damping ratio, and edge radius (m)".
  - Add "every asset with collision names a material, and a collision shape may override it".
  - Add "an optional wind volume (shapes), used instead of the collision shapes to derive wind obstacles".
  - Define `wind_porosity` as "the optical porosity of the wind volume seen side-on".
- **MF-4 · New scenario "Unknown material".** When an asset or shape names a material id missing from the table, the validator exits non-zero, naming the asset and the id.

### `world-query` spec

- **W-1 · Ground sample.** A batch call that returns the §6.2 `GroundSample` fields with their units. It replaces the separate height, normal, surface and cover lookups as the physics path (the single-point calls may stay for tools).
- **W-2 · Continuity.** `GroundHeight`, `MatDepth` and relief parameters are C⁰ across surface-cell borders: they blend bilinearly over the 4 nearest cell centres. Pitfalls are never clipped by the border of the cell or surface they extend into.
  - *Scenario:* walking every surface border of `sample_patch` in 1 mm steps, no two adjacent samples of `GroundHeight` differ by more than 1 mm.
- **W-3 · Normal.** The unit normal of `GroundHeight` on the rendered facet, including the relief, ridge and pitfall slope. Pitfall walls have a finite slope (for example a smoothstep over the outer 25 % of the radius).
  - *Scenario:* at 10,000 points away from creases, it agrees with a 1 mm central difference within 1e-3 rad.
- **W-4 · Scenario "Micro-relief bounded".** The expected RMS is `√(amplitude² + ridge-profile RMS²)`, and pitfalls stay excluded.
- **W-5 · MicroDetailNear.**
  - The centre is 3-D.
  - "In the radius" means **the element's segment (with its radius) meets the query sphere**, not just its base. A 1 m lying straw rooted outside the sphere but crossing under a leg must be returned, so the scan widens by the maximum element length per kind.
  - A `KindMask` parameter.
  - The canonical order is (cell z, cell x, kind, slot).
  - The element struct is as in §6.2, with the base Y rules.
  - The results go into a caller buffer, with overflow reported.
  - The budget stays at < 0.25 ms for r = 2 m on the densest surface, with the widened scan.
- **W-6 · Per-element scaling.** `TipStiffness = table × (d/d̄)⁴ × (L̄/L)³`, where d̄ and L̄ are the midpoints of the ranges. `HookRelease` is drawn from the cover's range by the element hash.
- **W-7 · New requirement "Static contacts".** Sphere and capsule queries with a margin against every catalog shape and every wire, returning `StaticContact` (§6.2) in canonical order (object, shape), allocation-free.
  - *Scenario (tunnelling):* a 15 mm sphere swept 60 mm per step across a 5 mm wire is always reported.
  - *Benchmark:* 100,000 capsule queries near the `sample_patch` objects in < 100 ms.
  - **Decision needed on D-002:** implement this in pure C# over the catalog primitives rather than through Godot/Jolt queries. There are four reasons:
    - Godot's C# `PhysicsDirectSpaceState3D` calls return allocating `Dictionary`/`Array` objects.
    - Those calls are unsafe off the physics thread.
    - Headless replay needs to run without Godot.
    - We control the cross-machine determinism.

    The cost is about 200–300 lines of closest-point code. The Jolt fallback is `PhysicsServer3D.BodyTestMotion` with reused parameter and result objects, measured in 3.4. Jolt still serves render-side and gameplay collision.
- **W-8 · New requirement "Raycast".** A batch of rays against terrain and objects with a maximum distance, returning `RayHit`, allocation-free. *Benchmark:* 100,000 rays of ≤ 2 m in < 100 ms.
- **W-9 · Requirement "Wind obstacles".**
  - Add `BaseM`.
  - Define `Porosity` as **the optical porosity of a 2 m horizontal path through the cell**, so a path through n cells has porosity β₁·β₂·…·βₙ.
  - The loader derives it from each wind volume as `β_cell = 1 − f·(1 − β_obj^(2 m / D_obj))`, where f is the covered fraction of the cell and D_obj is the volume's mean horizontal extent. Overlaps multiply, top = max and base = min.
  - Expose the whole grid read-only, with row 0 north and cell centres at −size/2 + (i + 0.5)·2 m.
  - Scenarios: the house scenario adds `BaseM = 0`. A new tree scenario: a `tree_proxy` reports `BaseM ≈ 4 m` and `TopM ≈ 10 m` under its crown, and a path across the crown has porosity ≈ its `wind_porosity`.
- **W-10 · New requirement "Gaps near a point".** World-space gap rectangles from the catalog, for turbulence at openings and for logs.
- **W-11 · Runtime objects.** The game can add catalog objects (the `launch_rails`) before a flight starts. Once added they are static and included in `StaticContacts` and `Raycast`. Alternatively, the map places rails at start points. Either way, the map has no start points yet (a game-developer item).
- **W-12 · Content hash.** A 64-bit hash of the package files plus `surfaces.json` and `catalog.json`. Physics logs it, and replay refuses to run on a mismatch.
- **W-13 · Cross-machine determinism.** World-query math uses only correctly rounded IEEE operations (+ − × ÷ √): no `Math.Sin/Cos/Exp/Pow` in the ground function or the generator, and no FMA unless it is explicit. The current "Replay identity" scenario only covers two processes on one machine.
  - *Scenario:* a committed golden file of inputs and output hashes, which QA re-checks on a second machine.
- **W-14 · Threading.** All physics-path queries are reentrant, have no shared scratch state, and need no scene tree. Physics may call them from its own thread while the renderer uses the generator.
- **W-15 · Outside the map.** Terrain and surface clamp to the edge cell, and `OutsideMap` is set. The query never throws.
- **W-16 · Performance.** Add the W-7 and W-8 benchmarks and the §6.1 composite step (< 60 µs, zero allocations) to the "Physics-grade performance" requirement.

Suggested task mapping:
- 3.1: W-1–W-4 and W-15
- 3.2: W-5 and W-6
- 3.3: W-9 and W-10
- 3.4: W-16
- a new task for W-7, W-8 and W-11 (static contacts and raycasts)
- 4.1: tagging colliders with materials, and wind volumes

## 8. Deferred (not phase B, listed so they aren't lost)

- **X-1 Debris chunks** (brick bits, tile shards, stones; notes §6): an optional per-surface `debris` block that adds hashed box bumps to `GroundHeight`, using the catalog material `masonry`. It is a minor bump that lands with the M1 yard and rubble patches, and at that point the rubble relief can come down.
- **X-2 Tree crowns as porous snag volumes.** A `crown` volume whose twigs come from the same hash generator in 3-D cells, for the fiber and the props in tree belts: "the densest snag hazard in the footage". Crowns have no contact representation today. It lands with the tree-belt assets.
- **X-3 `stems_per_element` weight**, only if grass jitter reads wrong in flight.
- **X-4 Soil sink exponent** (Bekker n), only if hard landings on soft soil read wrong. The model is linear now.
- **X-5 Soil adhesion**, if mud surfaces are ever added.
- **X-6 Stem cut energy** per cover type, for prop strikes in tall grass and weeds.
- **X-7 Terrain holes and trenches:** a `Hole` flag in `GroundSample`, and object materials that point at a surface (`"surface:<id>"`) so that trench floors and walls behave as soil.
- **X-8 Breakable materials** (asbestos sheet, tiles, branches) and a roof porosity separate from the walls (stripped roofs).

## 9. Risks and questions

- **Q1 (user):** The launch bars for legs = FALSE:
  - their spacing, height and length
  - their profile (round, square or angle) and whether they are bare, painted or rusty steel
  - which part of the drone rests on them (the bottom plate, the arms or printed skids)

  Without this, the rails-liftoff case can't be built true to life.
- **Q2 (orchestrator):** the D-002 decision in W-7: pure-C# static contacts, or Jolt through `BodyTestMotion`.
- **Q3 (later, in the physics change):** the user's real leg and foot design (material, foot diameter and shape). It drives hooks and ploughing. The format doesn't need it, because of `soil_reference_diameter_m`.
- **Risk:** the mat's load-spread rule, the linear loading, the unload ratios and the hook forces are my estimates from soil mechanics and plant biomechanics, not measurements. They are all data or physics constants, tuned in flight.
- **Risk:** until `wind_volume` lands, trees are invisible to the wind grid, and until X-2 they are invisible to contacts. The belts are the main turbulence and snag source in the reference.
- **Risk:** relief that isn't rendered (up to 12 cm on rubble) makes legs look like they float or clip in any third-person view. The FPV camera rarely shows the feet.
