# Physics sign-off: the world-query interface, with a contact probe

**Reviewer:** physics-engineer · **Task:** 1.2 · **Branch:** `unity/physics/p6-world-query-gallery--signoff` · **From:** `afee3da` (`unity/main`)
**Against:**
- PHB-05
- K§7.5, K§10.4 and K§10.5
- `docs/legacy/map-and-world/api-review.md` §2.4 and §4, and `surfaces-review.md` §6 (my earlier reviews)
- this change's design 1 and 4–6, with 1.1's as-built
- `p6-flight-core` design 7 and task 1.6, with its notes from `p5-drone` 1.2 and from QA's `review-base.md` finding 5

**Probe:** `tools/flightbench/ContactProbeCase.cs`, run with `dotnet run -c Release --project tools/flightbench -- --probe [--break latch|margin|refresh|spheres|disc]`. It uses the public calls of `IWorldQuery` only, on `FloorWorld` with runtime probe shapes.

## Verdict

**Signed off for `p6-flight-core` 1.6.** The calls and result fields carry everything 1.6's contacts need:
- legs on `SampleGround`
- the stand and objects through `StaticContacts` under the Time < 1 latch
- prop strikes
- crash naming and tunnel events
- the log's world identity

No call or field changes for 1.6, and the probe passes on the stub. Eight findings go to the world-builder (§5), for 1.3–1.5 or later; none blocks 1.6 on the stub. Three of them are musts for 1.4: wires (W1), holes under a capsule (W3) and patch ground answered once (W4).

The probe also corrects my own latch rule (api-review §4, K§7.5). Under the rule as written, an arm went through a 20 mm tube on 32 of 48 runs with no tunnel event. That correction is 1.6's work and K§7.5's text (§7 P1), not the world's.

## 1. The probe drone

Built from `assets-src/drone/drone.json` (hash `0xA3015F07E918911F`) at L4, its own loadout. Legs on it weighs 4.832 kg, legs off 4.716 kg (the mass build). Positions are in body axes (DetMath.cs). A cylinder part becomes the capsule of its diameter and overall length.

| Part | Contact shape | From |
|---|---|---|
| Skids | 4 segments, each skid split at its middle and carried by its corner's leg. Each segment is a capsule from the overhang's core end (z ±0.220) to the middle, r 0.005 m, bottom at y −0.253. It also has support spheres at the overhang end and at the T-joint (z ±0.099). | `skid_right`, `skid_left` (measured, assumed length); `legs.foot_radius_m` |
| Arms | 4 capsules on the diagonals from r 0.0555 to 0.2145 m, r 0.0105 m | `arm_*` (the mass cylinder, derived) |
| Coil | a capsule along z, r 0.0415 m; legs on at y +0.087, legs off at y −0.053 | `coil`, `legs_off.moved` |
| Prop discs | R 0.127 m (the flown prop) at the hubs, half-thickness 0.004 m. Each is a bounding sphere of R + 0.004, plus a ring of 12 capsules round the rim, swept only when that sphere meets something other than the ground. | `props.diameter_m` (measured); `prop_*` length (published); `motors[].position_m` |
| Legs | k 10 kN/m and c 45 N·s/m each, in series with the ground | `legs` (assumed, derived) |

The probe's own numbers:

| Number | Value | Label |
|---|---|---|
| Frame part on a rigid material, series stiffness | 1e6 N/m | assumed (the first attempt's frame on its rails, `surfaces-review.md` §1) |
| Its damping ratio | 0.1 | assumed |
| Query margin | 0.05 m | derived. It covers the deepest stop less what a part needs to clear the shape: two arms at 30 m/s, 30·√(4.83/2e6) = 0.047 m; skids onto a 9 mm rod at 2 m/s, 2·√(4.83/2e4) − 0.019 = 0.012 m. |
| Ring segments | 12 | derived: the chords stay within R(1 − cos 15°) = 4.3 mm of the rim |
| Stand-in wire | 9.0 mm, `cable` | published (L03_medium's A-50, `wires[0]`) |
| Stand | posts of 20 mm square tube 0.25 m high at r 0.200 m, an X base of the same tube, steel | `stand` (pilot, derived, assumed) |

The body is translation-only. The probe checks what the world answers, not how the drone flies. Its law is api-review §4's latch, as corrected in §7 P1, with a spring-damper along the latched normal.

## 2. What it drove, and what it found

| Scenario | Result |
|---|---|
| **Resting**, legs on, at yaw 0 and 0.644 rad | All 8 support spheres meet the floor 1.185 mm deep (m·g/4k): object −1, shape 0, `belt_bare`, Time 1, WireParam −1. `SampleGround` and `StaticContacts` agree within 7.8e-18 m. |
| Resting, the support span | z −0.220 to +0.220 m at both ±1 µrad of pitch. A capsule alone keeps one core end per skid: 0.000 to 0.220 m nose down, −0.220 to 0.000 m nose up. |
| Resting, pitched 15° nose up | Only the rear overhang ends touch. The rear T-joints are 31.2 mm clear, so four point feet would let the skids 31.3 mm into the floor first (`p5-drone`'s note). |
| Resting, across a 20 mm tube between the spheres | The capsule carries it, 2.000 mm deep at z 0.1595 m. No sphere meets it. |
| Resting, rotor rays | Down: the floor at the hub height, 0.2868 m. Up: a miss (+∞, object −1, surface 0). |
| **Sliding** 1 s at 2.24 m/s, yawed, rocking ±0.02 rad at 2 Hz | 3,024 sphere-steps were in contact at both ends of a step, and every one was reported as one Time = 1 contact. The planes agree with `SampleGround` within 0 m. Each corner's support moment changes by at most 0.323 N·m a step; a capsule alone jumps 2.606 N·m. |
| **Dropping** 0.30 m onto the floor, throttle cut | The first touch has Time 0.852172 against an analytic 0.852172, and Distance 0. Its latched plane equals `SampleGround`'s depth. At rest after 3 s: 1.1846 mm deep against 1.1846 mm, \|v\| 2.3e-14 m/s. Peak leg force 19.0 g, then an 82.7 mm hop. |
| **Landing on a 9 mm rod** at 2 m/s across the front skids | They come to rest on it, 7.13 mm above its axis (analytic 7.13 mm). 10 first touches latched, the deepest 28.8 mm, 0 tunnel events. |
| **Stand, legs off**, resting | One contact per arm, on its own post: object 0 `stand`, shape = corner, steel, Time 1, 11.6 µm deep. The coil hangs 0.1460 m above the base. |
| Stand, after `ResetRuntimeObjects` and a second add | 4 arm contacts, object 0. The first attempt's F2 holds. |
| Stand, set down from 5 cm, then lifted 1 m at 2 m/s | At rest 11.56 µm deep against 11.56 µm. 48 first touches latched, 0 tunnel events, 0 pairs held after the lift. |
| Bare floor, legs off | The coil is the first part to touch (D-043: it cannot stand on the spool). |
| **Arm sweeps** into a 20 mm timber board and a 20 mm steel tube (3 mm off the arm plane), at 10, 20 and 30 m/s, 16 phases each | Every first touch is one Time < 1 contact in the right step, Distance under 1 µm, with the obstacle's object, its material and the entry normal. Time is conservative within 4.0e-15 (board) and 7.6e-5 (tube, conservative advancement). **0 of 96 runs tunnel.** The deepest plane penetration is 43.2 mm, which puts 1,822 g on the body. |
| **Prop disc**, edge-on at 20 m/s into a wire | Struck in step 3 at Time 0.370000 against an analytic 0.370000, at the rim's leading point. Object `wire`; Geometry gives a capsule of 9.0 mm in `cable`. |
| Prop disc, climbing at 5 m/s into a wire over a chord 0.06 m off the hub | Struck in step 39 at Time 0.300000 against 0.300000, from below. The broadphase sphere opened the ring 24 steps early. |
| Prop disc, under a wire 0.11 m above the prop plane | No strike. The bounding sphere met the wire on 7 steps, so a sphere alone would call it a strike. |
| Prop disc, a 20 mm ball rising through the disc's face at the hub | The ring misses it, and the sphere sees it (§7 P4). Information only, not a pass criterion. |
| Rotor ray past a wire | The ray meets the wire at 0.0955 m. Cast again from the wire's axis, it starts inside, skips it and reaches the floor at 1.9350 m. |

Each known-wrong piece turns the probe red (exit 1):

| `--break` | Checks that fail |
|---|---|
| `latch` (each contact used as reported) | Board: 11 of 48 runs through. Tube: 32 of 48. The rod: the skids end 996 mm below it. Stand: 0 latches. |
| `margin` (1 mm) | The rod: 2 tunnel events, and the drone falls through it |
| `refresh` (api-review §4 as written) | Tube: 32 of 48 runs through, with **0 tunnel events** |
| `spheres` (each skid on its capsule's one contact) | The support span jumps 0.22 m; the support moment jumps 2.606 N·m a step |
| `disc` (each disc as its bounding sphere) | A false strike under the wire; the climb struck in step 16 at the wrong Time |

**Cost** (`--bench`: AC, Balanced plan, best power efficiency mode, CPU 3201/3201 MHz). Each step makes 12 ground samples, 21 swept queries (4 skid capsules, 8 support spheres, 4 arms, the coil and 4 disc spheres) and 8 rays:
- clear: p50 3.90 µs, p99 43.20 µs
- near the wire, with two rings swept (45 swept queries): p50 10.50 µs, p99 32.00 µs
- 0 B in steady state in both

That is on the stub's linear scan; the gallery's numbers are 1.5's.

## 3. PHB-05's lessons, checked

1. **Load-bearing parts are spheres.** Confirmed on the floor: a skid capsule's one contact sits at a core end and jumps 0.22 m with a micro-radian of pitch (the lesson's rocking, here 2.6 N·m a step). Refined by the probe:
   - spheres alone miss a bar between them: a 20 mm tube under the skid, 2 mm deep, gave 0 sphere contacts
   - so each skid needs its capsule for crossings and its spheres for faces
   - a capsule contact within two skid radii of a touching sphere's point is the same support, and is dropped (§7 P3)
2. **The damping ratio is ignored on rigid materials.** The interface returns no stiffness or damping at all, only the material id. In `physics_materials.json` a rigid material now has neither field, so there is no ratio to ignore. One correction to my A3: the series formula 1/(1/k_part + 1/∞) does not return k_part's bits. 13.8 % of 100,000 stiffnesses in 1e1–1e8 N/m come back one ulp off (490 → 489.99999999999994). 1.6 branches on rigid instead (§7 P7).
3. **Surface and material parameters are read-only to physics.** This holds by construction: the interface hands out ids and names (`MaterialName`, `SurfaceName`, `MaterialId`), never a parameter object. Physics reads its parameters from `assets-src/physics_materials.json` into its own table. That file is outside the world's content hash (design 6 hashes `stations.json` and the `.assetmeta.json` files), so physics logs its hash beside `drone.json`'s (§7 P8).

PHB-05's other items:
- **Legs as spring-dampers on `SampleGround`:** resting, sliding and dropping all pass.
- **Stand and object contacts through `StaticContacts` with the latch:** they hold, with §7 P1's correction.
- **Crash detection:** every contact names its object (`ObjectName`), its shape (`Geometry`) and its material. A crash names the part and the object (flight-core design 7).
- **Tunnel events:** they are logged. Classing the "past the centre" case uses `Geometry`'s centre; for wires that is W2.

## 4. Every call and field

| Call / field | What 1.6 uses it for | Verdict |
|---|---|---|
| `ContentHash`, `QueryVersion` | the log header; replay refuses a mismatch | sufficient. Runtime objects stay outside the hash, and physics logs them (QA `review-base.md` 5; §7 P8). |
| `SampleGround`: `Height` | each support sphere's depth, δ = (H − (y − r))·n.y (K§7.1 step 2) | sufficient: it agrees with `StaticContacts` to 7.8e-18 m |
| `Normal` | the support's normal, and δ's n.y | sufficient |
| `Surface` | the soil's parameters (rigid at G7, soil in 7a) | sufficient; W4 says which primitive answers on a patch |
| `Flags` (`Hole`) | switches the feet to the fillers in `StaticContacts` | sufficient for points; W3 for capsules |
| `StaticContacts(previous, current, margin)` | the skids against objects, the arms, the coil, the prop rings, the stand, the fillers | sufficient. One call per part, swept, with physics' margin (0.05 m). |
| `Point`, `Normal` | the latched plane | sufficient |
| `Distance` | at Time 1, the depth. At Time < 1 it is about 0, and the depth comes from the latched plane. It also feeds the refresh guard (core inside the shape when Distance < −r) | sufficient. It must stay the true signed distance deep inside every kind, wires included (W1). |
| `Time` | the latch; the strike's timing | sufficient: conservative, analytic within 1 µm of travel |
| `Object`, `Shape` | the pair's identity; crash and tunnel naming; `Geometry` | sufficient |
| `Material` | μ, stiffness and edge radius from `physics_materials.json` | sufficient |
| `Surface` (contact) | soil shapes: fillers, patches | sufficient |
| `WireParam` | the wire's local tension and stiffness (phase 7), and the strike's place on the span | **untested**: the stub holds no wire (W1) |
| the true count, canonical order | buffer sizing; an overflow logged as a bug | sufficient |
| `Raycast` | rotor ground and ceiling effect (p7b); the coil's clearance | sufficient. A re-cast skips a wire (W6). |
| `Geometry` | the tunnel's side test (the shape's centre), the fiber's bend radius, the log's runtime objects | sufficient for primitives; W2 for wires |
| `AddObject`, `ResetRuntimeObjects` | the stand, once per flight | sufficient: one contact per post after a reset, the same id. Static: W8. |
| `ObjectName`, `MaterialName`, `MaterialId`, `SurfaceName` | log names; table lookups | sufficient |
| The frame (D-019) | body → world by `Quat` (Unity's components) | consistent: a yaw of 0.644 rad gives the same depths to 7.8e-18 m |

## 5. Findings for the world-builder

| # | Severity | For | Finding | Asked |
|---|---|---|---|---|
| W1 | must | 1.4 | **Wires are untested.** The stub can't hold one: `AddObject` refuses `Wire`, and `ShapeMath.Closest` returns +∞ for it. So `WireParam`, the capsule chain and a wire's `Distance` deep inside were never queried. | `Wire` in `StaticContacts`, `Raycast` and `Geometry`, with `WireParam` by length along the span and `Distance` the true signed distance deep inside, as for every kind. Scenarios: K§10.4's 15 mm sphere at 60 mm a step across a 5 mm wire, and this probe's wire cases on a real L03 span (§6). |
| W2 | should | 1.4; phase 7 | **`Geometry` can't rebuild a wire.** `SpanA`, `SpanB` and `Sag` don't reproduce the 0.5 m point list that the chain is tested against. `Center` has no meaning for a span, so the tunnel's side test and the fiber's bend over a conductor have no point to use. Phase 7 (PHY-22, 7d) also needs the record's `mass_kg_per_m`, `axial_stiffness_n` and `breaking_force_n` by (object, shape). | For 1.4: the chain segment at a `WireParam` (its two points), or the point list, by (object, shape). For phase 7: the three mechanical fields. |
| W3 | must | 1.4 | **The ground under a capsule that straddles a hole band's edge** is undefined: a skid half over E01's run. `SampleGround` is per point, and "the ground absent over a hole" in `StaticContacts` doesn't say over which point. | Decide per contact point (the ground answers where its point lies outside the band), and say so in `IWorldQuery`. |
| W4 | must | 1.4 | **Patch ground is counted twice.** Design 5 lets S04's and E01's top primitives answer `SampleGround`, and they also come back from `StaticContacts` as soil shapes. The feet read both, so a patch top would carry a foot twice. Two more gaps:<br>• **The answering primitive.** S04's straw boxes (top y 0, material `straw`) sit over its soil box (`surface:belt_straw`, top −0.125 m), so it is unclear which one answers, and with what surface.<br>• **The floor plane.** It must be absent over S04, or the floor at y 0 holds the feet above the straw. | **(a), recommended:** treat each patch's footprint like a hole band. `SampleGround` reports `Hole`, and the soil shapes are the only ground there. No interface change; the hole rule generalised.<br>**(b):** `GroundSample` names the (object, shape) that answered, which is a struct change and a superseding decision.<br>Either way, the straw boxes stay compliant material shapes in `StaticContacts`. |
| W5 | should | 1.4 (docs) | **A line contact's point is unstated.** A capsule lying along a face reports its point at the overlap's end nearest A: on the plane, the lower core end (A on a tie); on a box face, the flat region's start, or A if A is over it. The probe's skid rule relies on it. | State it in `IWorldQuery`'s Time-rule block and keep it for the gallery's shapes. |
| W6 | should | 1.4 | **Rays and wires.** Rotor rays must skip wires (the L01/L03 reading §7), and `Raycast` returns the first hit only. A re-cast from inside the wire works on the stub ("a ray that starts inside a shape does not hit it"): 0.0955 m, then 1.9350 m to the floor. | Keep that rule for `Wire` and make it a scenario. No ray mask is needed. |
| W7 | should | 1.5 | **Budgets at physics' pattern.** Physics queries with a 0.05 m margin, not 0. Its pattern is 12 samples, 21 swept queries and 8 rays, plus 12 capsules for each disc whose sphere meets something (up to 48). | Bench V01's crown, the house and the test range's posadka at that margin and that pattern, beside K§10.5's batch. |
| W8 | could | phase 7 | **The stand is static.** Runtime objects are fixed once added, but K§6.6 lets the unanchored stand tip or drag. G7 accepts a static stand. | Phase 7 picks either a pose update callable on the physics thread between steps (logged, outside the hash) or physics-owned body–body contact on `Geometry`'s shapes. |

## 6. What 1.4 must add

- **W1, W3 and W4,** and W5 in the docs.
- **The launch stand as a runtime object with reset,** as planned. It is exercised here: one contact per post, the same id after a reset.
- **The probe's cases over the gallery and the test range.** I switch the probe to the loaded world when 1.4 lands (1.6's work), and these must pass there:
  - a skid resting in E01's run: both spheres on the floor filler, with its surface, and `Hole` at the points
  - a skid resting on S04: the soil under the straw boxes, the straw as compliant shapes, each counted once
  - an arm swept into `L01_straight` at 10 m/s: the spec's scenario uses a point-size 0.02 m capsule, so this adds the drone's own
  - a prop ring against an L03 conductor, edge-on and climbing
  - a rotor ray re-cast past a conductor
  - a skid landing across a conductor at 2 m/s

## 7. For 1.6 (physics' own; no world change)

- **P1, the latch correction** (K§7.5 and api-review §4: for the orchestrator to update K§7.5's "Keep or refresh"):
  - **Refresh.** A same-side Time = 1 contact refreshes the plane only while the part's core is outside the shape (Distance > −r). Past that, the closest-point normal is no longer the contact's. An arm driven 3 mm off a 20 mm tube's axis saw it turn through −y, under 90° a step, so the plane followed the arm through or under the tube. With the old rule that happened on 32 of 48 runs, with no tunnel event.
  - **Tunnel event.** A latched pair that leaves the result while its plane still penetrates is a tunnel event when it had flipped, or when the part's deepest core end is past the shape's centre along its entry normal.
- **P2, the margin.** At least the deepest stop less what the part needs to clear the shape: 0.05 m for the probe's cases. With 1 mm, the drone fell through a 9 mm rod and the latch logged 2 tunnel events.
- **P3, the skids.** Per corner: the segment's capsule, plus spheres at the overhang end and the T-joint. A capsule contact within two skid radii of a touching sphere's point is dropped as the same support. Pitched landings meet the overhang ends 31 mm before the T-joints.
- **P4, the prop discs.**
  - Each step: the disc's bounding sphere. When it meets a non-ground shape: the 12-capsule ring, exact against the world's own geometry.
  - A sphere alone strikes a wire 0.11 m above the disc.
  - The ring misses a compact shape inside the rim (a branch end rising into the face). Two capsules at the rotor angle, the blades, close that gap physically: a strike lands when a blade passes, within 1/300 s at hover rpm.
  - The ring adds up to 48 queries, so K§10.5's "4 prop discs" become 4 spheres plus rings on demand.
- **P5, contact shapes in `drone.json`.** Mass shapes are not contact shapes:
  - **The arms.** Each is a 25 × 6 mm plate; the Ø21 mm mass cylinder puts its underside 7.5 mm low, hence the coil's 0.146 m against the note's 0.135 m.
  - **The rest.** The motor cans lead the arms head-on, and the packs and payload are boxes.
  - **Each part's compliance.** The plate arm bends at the post at about 1.8e4 N/m (3EI/L³ over 0.155 m, with E 50 GPa assumed), against the probe's assumed 1e6. The deepest stop and the margin follow from it.
  - **The material.** `carbon_composite`, as `p5-drone` noted.
- **P6, the bounce.** `drone.json`'s legs on a rigid floor give a restitution of about 0.53 (ζ 0.2): a free 0.30 m drop with the throttle cut hops 82.7 mm. "Cut the throttle and it stays" (PHY-02) then needs the soil's loss (7a) or more leg damping; 1.6's bounce scenario decides.
- **P7, rigid materials.** Branch on a rigid material (k_part alone, no ζ), never 1/(1/k_part + 1/∞): 13.8 % of stiffnesses don't survive it bit for bit.
- **P8, the log.** The stand can be logged as the name and world-space shapes given to `AddObject`, read back through `Geometry`, with material and surface names, and replayed in order after `ResetRuntimeObjects`. `physics_materials.json`'s hash goes beside `drone.json`'s.

## 8. Commands

```
dotnet run -c Release --project tools/flightbench -- --probe                      exit 0, contact-probe: PASS
dotnet run -c Release --project tools/flightbench -- --probe --break latch        exit 1, FAIL (4 checks)
dotnet run -c Release --project tools/flightbench -- --probe --break margin       exit 1, FAIL (1 checks)
dotnet run -c Release --project tools/flightbench -- --probe --break refresh      exit 1, FAIL (1 checks)
dotnet run -c Release --project tools/flightbench -- --probe --break spheres      exit 1, FAIL (2 checks)
dotnet run -c Release --project tools/flightbench -- --probe --break disc         exit 1, FAIL (2 checks)
dotnet run -c Release --project tools/flightbench -- --bench                      exit 0 (AC), contact-probe clear p50 3.90 / p99 43.20 µs, near p50 10.50 / p99 32.00 µs, 0 B
```
