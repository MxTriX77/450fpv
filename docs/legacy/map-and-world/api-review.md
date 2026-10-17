# Physics review: the world-query API for the flight model

**Change:** `define-map-format` · **Task:** 3.7
**Reviewer:** Physics Engineer · **Date:** 2026-09-24
**Inputs:**
- my earlier review, `surfaces-review.md` (§3–§6)
- D-010
- `design.md`, including the accepted 3.6 deviation and `StaticContact.Time`
- the world-query and map-format spec deltas
- `game/src/world/*.cs`
- `surfaces.json`, `catalog.json` and `game/maps/README.md`
- `tools/worldbench`
- a contact probe I wrote against the real API (§5; it is not committed)

## Verdict

**Changes requested.** Three gaps must be closed before the flight model builds on this API: F1, F2 and F3 in §3.
- **F1:** lying straw floats up to 0.55 m above the mat.
- **F2:** runtime objects can't be removed.
- **F3:** there is no wire or shape geometry for wire compliance and fiber bending.

Five small items (F4–F8) can ride along with them.

Everything else is fit for a 1 kHz flight model:
- signatures, units and return structs
- batches
- zero allocation
- reentrancy
- prefetching micro-detail on a worker thread
- the golden file

Review §3–§5 is **fully applied**: nothing is wrong and nothing is missing.

Only F1 changes query results, so only the micro-detail cases of the golden file need re-recording. F2–F8 are lookups, lifecycle and documentation, and they leave every hash as it is. F1 should land before 4.1, because the renderer draws near-camera micro-detail from the same generator, so the pilot would see the floating straws in noclip.

## 1. Review §3–§5: applied?

Paths are relative to the repository root. `validate` means `tools/map/validate_map.py`.

| Asked for (review) | Status | Where |
|---|---|---|
| §3.1 `soil_reference_diameter_m` = 0.015 at the top level | applied | `game/maps/surfaces.json:3`, `README.md:113`, `validate:118`, loaded at `WorldQuery.cs:131-132` |
| §3.1 `soil.unload_stiffness_ratio`, all 9 values (3, 1.5, 8, 5, 3, 8, 2, 1.5, 3) | applied as proposed | `surfaces.json:8,21,33,43,58,71,83,97,109`, `README.md:126`, `validate:33`, `SurfaceTable.cs:54` |
| §3.1 and §4: remove `soil.porosity` | applied: no occurrence left, and the rationale is kept | `README.md:172` |
| §3.1 wording for bearing and max sink | applied | `README.md:121,125` |
| §3.1 `micro_relief.ridges`, with tilled at 0.05 m / 0.5 m / 0° | applied. The profile is C², better than the C¹ I asked for. RMS 0.697 × amplitude. The azimuth convention matches. | `surfaces.json:72`, `README.md:129-132`, `WorldQuery.cs:170-172,433-442`, `validate:144-145` |
| §3.1 `cover[].mat` with the three proposed mats (meadow grass, belt_straw straw, yard litter) | applied, values exact. The depth is noise at the relief lattice × the channel, and it drapes into pitfalls. | `surfaces.json:13,48,88`, `README.md:152-156,168`, `WorldQuery.cs:277,392-414`, `SurfaceTable.cs:79-87` |
| §3.1 `cover[].hook_release_n`, all 12 cover entries | applied as proposed | `surfaces.json:12,25,47,49,50,62,63,75,87,89,101,113`, drawn at `MicroDetail.cs:275` |
| §3.1 lateral stiffness defined at mean length and diameter, with per-element scaling (W-6) | applied | `README.md:149`, `MicroDetail.cs:256,274` |
| §3.1 validator: element length and diameter min > 0 when a cover has elements | applied | `README.md:146,148`, `validate:164-169` |
| §4 tilled straw lateral stiffness 0.3 → 5 | applied | `surfaces.json:75` |
| §4 yard_litter soil friction 0.45/0.35 → 0.60/0.50 (only valid together with the litter mat) | applied, with the mat | `surfaces.json:83,88` |
| §4 tilled relief 0.035 m @ 0.7 m → 0.02 m @ 0.3 m, plus ridges | applied | `surfaces.json:72` |
| §5 `materials` table: steel, sheet_metal, timber, masonry and cable, with the proposed values | applied, values exact | `game/assets/catalog.json:2-8` |
| §5 optional stiffness (absent = rigid), damping ratio default 0.05, edge radius default 0.002 m, no restitution | applied | `Catalog.cs:79-81`, `README.md:204-208`, `validate:51,180-192` |
| §5 every colliding asset names a material, any shape may override it, and an unknown id is rejected | applied | `catalog.json:13,22,31,40,50,62,89`, `Catalog.cs:63,92,137`, `validate:212-213,244-247` |
| §5 placeholder assignments: house masonry, shed / tree / pole timber, gate steel, cable cable, junk visual only | applied | `catalog.json:13,22,31,40,50,89,81` |
| §5 `wind_volume` (tree crown: a sphere of radius 3 m at y 7 m), defaulting to the collision shapes; `wind_porosity` defined as optical porosity side-on | applied | `catalog.json:33`, `Catalog.cs:101`, `README.md:220-221` |
| §5 `launch_rails` in steel | applied, with derived dimensions (0.26 m apart, 0.25 m high, 25 mm tube). User question Q1 is still unanswered. The design accepts that risk. | `catalog.json:60-77`, `README.md:250-273` |

The spec deltas carry MF-1 to MF-4 and W-1 to W-16 from review §7. I checked this by reading them.

## 2. Fit for the 1 kHz flight model

### 2.1 Calls against review §6.1

| §6.1 asked for | API | Verdict |
|---|---|---|
| `SampleGround(ReadOnlySpan<Vector2>, Span<GroundSample>)` | `SampleGround(ReadOnlySpan<XZ>, Span<GroundSample>)`, `WorldQuery.cs:207` | Accept. Double x and z are better at 4 km. |
| `StaticContacts(in Capsule, float margin, Span<StaticContact>)`, swept from the previous step's pose | `int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact>)`, `Contacts.cs:81`, plus an unswept overload at `:65` | Accept. Both poses are explicit, which is clearer. |
| `Raycast(ReadOnlySpan<Ray>, float, Span<RayHit>)` | `Raycast(ReadOnlySpan<Ray>, double, Span<RayHit>)`, `Contacts.cs:180` | Accept |
| `MicroDetailNear(Vector3, float, KindMask, Span<MicroElement>)` | `int MicroDetailNear(Double3, double, KindMask, Span<MicroElement>)`, `MicroDetail.cs:71` | Accept |
| `GapsNear(...)` | `int GapsNear(Double3, double, Span<Gap>)`, `WorldObjects.cs:319` | Accept |
| `WindGrid`, a read-only span plus dimensions | `ReadOnlySpan<WindCell> WindGrid`, `WindCells`, `WindCellSize`, `WindGrid.cs:15-26` | Accept |
| `GetSurface(byte)`, `GetMaterial(ushort)`, `SoilReferenceDiameter`, `ContentHash` | `Surface(byte)`, `Material(ushort)`, `SoilReferenceDiameter`, `ContentHash`, `ContentHashOf(...)`, at `WorldQuery.cs:200-204` and `ContentHash.cs:21,25` | **Accept the names without `Get`.** See F4 and F5 for their behaviour. |

Units are SI throughout (m, N, N/m, Pa). The only angle that crosses the API is the yaw given to `AddObject`, in degrees.

### 2.2 Return structs against review §6.2

Every field I asked for is present, with the unit I asked for:
- `GroundSample`: `WorldQuery.cs:52-67`
- `MicroElement`: `MicroDetail.cs:7-19`, 64 B and blittable
- `StaticContact`: `Contacts.cs:19-29`, plus `Time`
- `RayHit`: `Contacts.cs:43-51`
- `WindCell`: `WindGrid.cs:4-9`
- `Gap`: `WorldObjects.cs:56-64`. Its `Name` is a string, which is fine off the step path.

The one thing missing is shape and wire geometry for a contact (F3). That gap is in my own §6.2, not in the implementation.

### 2.3 Batches, allocation, reentrancy and threads

- **Batches.** The step makes three kinds of call:
  - one `SampleGround` call for all 44 points
  - 46 `StaticContacts` calls, one per capsule, as §6.1 asked
  - one `Raycast` call for all 8 rays

  The orchestrator measured the composite step at 34.7 µs median (p99 72.9 µs, 0 B) at 2.1–2.5 GHz, against a 60 µs budget (`design.md:67-71`). It fits the 1 ms step.
- **Allocation.** The probe ran 1,000 steps of that shape near the rails and allocated **0 B** on the physics thread. All scratch space lives on the stack: `Drawn` and `CellGround` at `MicroDetail.cs:84-85`, and locals in `Contacts.cs` and `Shapes.cs`. Local functions capture into struct closures.
- **Reentrancy.** The only mutable instance field a query reads is the test-only `ScalarReference` (`MicroDetail.cs:43`), and the game never sets it. The concurrency check ran 10 s in this review (§6) with 0 differing results.
- **Threads.** The flight model can run on its own thread, with micro-detail prefetched on a worker (probe §5.5):
  - The worker's belt_straw cache (4,297 elements) was bit-identical to a synchronous query.
  - The query itself took 0.90 ms warm.
  - `AddObject` and `AddWire` are not thread-safe. The world is set up before the flight thread starts, which is acceptable (see A7).
- **`StaticContact.Time`:** the decision is in §4.
- **Rigid materials report stiffness +∞.** Accepted (A3).

### 2.4 Accepted as is (no request)

- **A1 Signatures.** Double-precision `XZ` and `Double3` rather than `Vector2` and `Vector3`, explicit previous and current capsules, and one call per capsule.
- **A2 Naming.** `Surface()` and `Material()` rather than `GetSurface()` and `GetMaterial()`.
- **A3 Rigid = +∞ stiffness.** The IEEE series combination `1/(1/k_part + 1/k_mat)` drops it cleanly. The probe's steel bar left the leg spring alone. Physics must ignore `DampingRatio` when the stiffness is infinite, because it defaults to 0.05 even for rigid materials.
- **A4 One contact per (object, shape).** A capsule lying along a bar gets one point, at an end, and it jumps to the other end when the far end dips 0.5 mm (probe 2c). That is correct for the contract, but a line contact rocks between its ends.
  - Physics models load-bearing parts (feet, arm undersides, skids, the spool bottom) as sets of spheres, and uses capsules only for sweeping thin parts (fiber segments, prop disks).
  - A sphere is also cheaper here, because it skips the golden-section search.
- **A5 `GroundSample.Normal`** is the normal of `GroundHeight`, not of the mat top. That is right for a compressive mat.
- **A6 `Raycast`** sees only the terrain triangles, not relief, mat or pitfalls. Physics combines it with `SampleGround` at the rotor. In the probe, relief moved the ground up to ±10 cm on rubble, and on belt_straw the mat top was 9–17 cm above the ground.
- **A7 `AddObject`** runs before the flight thread starts. The renderer's micro-detail never reads objects, so adding objects at load time while it draws is safe.
- **A8 The 3.6 deviation:** micro-detail is prefetched at ≤ 40 Hz on a worker.
- **A9 `SurfaceParams` and `MaterialParams`** are mutable classes shared with the renderer. Physics treats them as read-only, because a write would change results under the same content hash. Making their fields `readonly` is optional (World Artist).

## 3. Findings

| # | Severity | Where | Finding | Requested change | Owner |
|---|---|---|---|---|---|
| F1 | **must** | `game/src/world/MicroDetail.cs:333-348` (`Lying`) | Lying straw and twigs are laid in the ground's tangent plane at their base. A 0.5–1 m straw doesn't follow a 0.7 m-wavelength relief (or a pitfall wall), so it points into the air or into the ground. Measured on flat belt_straw, 3,807 straws: **42 % float more than 5 cm above the mat top somewhere along their length, 9 % more than 20 cm, and the worst reaches 0.55 m. Others are buried down to −0.61 m.** On tilled it is 22 % more than 5 cm, max 0.24 m. Those straws would hook legs in mid-air, snag the fiber, take prop strikes, and show in the renderer. | Lay each lying element straight, from `SupportTop` at its base to `SupportTop` at its tip. With that chord the probe measured a maximum float of 0.21 m and 0 % above 20 cm. What remains is bridging over dips and pits, which is physical. It costs one more ground evaluation per lying element (a height and mat without the normal is enough), on the physics worker at ≤ 40 Hz. Re-record the micro-detail golden cases. | World Artist (code, golden). Orchestrator (spec, "Deterministic micro-detail": lying elements lie straight from the mat top at the base to the mat top at the tip. New scenario: at its own (x, z), each lying tip is within 1 cm of `SupportTop`.) |
| F2 | **must** | `WorldObjects.cs:110-118` | Runtime objects can only be added. A second legs-off flight at the same start adds a second set of rails. In the probe, a foot on bar 2 then got **2 contacts (objects 10 and 11), which doubles the support force.** A legs-on flight after a legs-off one keeps the rails. | Add `ResetRuntimeObjects()`, which returns to the `objects.json` state: primitives, gaps, wire points, wind cells and the broadphase. The game calls it before each flight, never while queries run. The flight log records the runtime objects added after it. | World Artist (code). Orchestrator (spec, "Runtime objects": the objects are removable between flights. Scenario: after a reset, adding the rails again gives one contact per bar.) |
| F3 | **must** | `Contacts.cs:19-29`, `WorldObjects.cs:214-248` | Physics can't derive two things it needs from the API: the wire's compliance, `T = w·L²/(8·sag)`, because the cable material is rigid locally (review §5); and the fiber's bend radius over round shapes. A cable contact gives the object, `WireParam` 0.499 and material `cable` (stiffness +∞), but **no span end points, sag or diameter, and no shape kind or radius**. This gap is in my own §6.2. | Add a read-only, allocation-free lookup by (object, shape) that returns: the kind; the radius (sphere, capsule, cylinder, wire); the box half-size; and, for a wire, the span that contains a given `WireParam`, with its two attachment points, sag, diameter and the parameter within the span. Keep it off `StaticContact`, so the query path and the golden file stay as they are. | Orchestrator (spec, "Static contacts"). World Artist (code). |
| F4 | should | `WorldQuery.cs:131-132,204` | `SoilReferenceDiameter` is set only by `Load`. A world built in memory (the golden file's `flat:<surface>` worlds, and physics unit tests) reports 0, so the soil modulus scale `(0/b)^0.3` is 0 and a foot falls through the ground. | Read it in `SurfaceParams.ParseTable`, or take it as a constructor parameter. | World Artist |
| F5 | should | `WorldQuery.cs:202` | `Material(Catalog.NoMaterial)` throws `IndexOutOfRangeException`, and every terrain `RayHit` carries `NoMaterial`. | Return `null` for `NoMaterial`, or document the guard in the XML comment. | World Artist |
| F6 | should | `ContentHash.cs:4-21` | The content hash covers the data, not the query math. After an optimisation re-records the golden file, an old log replays wrongly under the same hash. | Add `public const int QueryVersion` and bump it with every golden re-record. Physics logs it next to `ContentHash`. The golden file may store it, so that `--golden` fails when hashes change but the version doesn't. | World Artist (code). Orchestrator (spec note under "Content hash"). |
| F7 | nit | `DetMath.cs:88-90` | The comment on `Pow` says b ∈ [0, 1]. Physics needs `(b_ref/b)^0.3` with b < b_ref. The probe found it bit-identical to `Math.Pow` at b = 1.5, 2 and 3. | Widen the documented domain to b > 0 (and test it), so physics can reuse `DetMath` for W-13-safe math. | World Artist |
| F8 | nit | `Contacts.cs:57-61,118-125` | When conservative advancement gives up after 64 steps, a sweep that grazed a face and then left it reports `Time` < 1 with `Distance` > 0. Probe 2d: a fiber node 5 µm above a bar at 6 m/s, off its end, reported Time 0.0533 and Distance 5.0 µm. §4 makes this inert. | In `README.md` "Contacts", say that `Time` < 1 with `Distance` > 0 is a graze, not a pass-through. | World Artist (docs) |

## 4. Decision: the contact response to `StaticContact.Time` < 1

**Time < 1 is an impact that began during the step. The flight model does not rewind or sub-step. It latches the contact plane at the first touch and pushes back against that plane.** The orchestrator copies this into the physics change's `design.md` when that change is proposed.

Per drone part and (object, shape) pair:

1. **Latch.** On a contact with `Time` < 1, store its `Point` P, `Normal` n and `Time`. Its `Distance` is at the first touch, so it is not the penetration.
2. **Depth.** Every step, the penetration is `δ = r − min(n·(A − P), n·(B − P))` for the part's capsule A–B of radius r (a sphere has A = B). If δ ≤ 0 there is no force.
3. **Force.** Use the same normal law as any contact:
   - The part's compliance is in series with the material: `1/k = 1/k_part + 1/k_mat`. A rigid material, with k_mat = +∞, drops out, and so does its damping ratio.
   - Damping acts on the normal speed along the latched n.
   - Friction uses the stick anchor in the latched plane.
4. **Keep or refresh.** While latched:
   - A `Time` = 1 contact of the same pair whose normal agrees with the latch (n·n′ > 0) refreshes the plane, and the contact becomes an ordinary one.
   - A flipped normal, or another `Time` < 1, keeps the latch. Probe 2b shows why: the step after a pass-through reports `Time` 1 with the far-side normal and −4.75 mm, and a physics that trusted it would push the part on through.
5. **Release.** Release the latch when the plane distance exceeds the margin, or when the pair is absent from the result. If it is absent while the latched plane still shows penetration after a flipped normal, log a `tunnel` event with the part, the pair, the speed and the depth. That means the penalty failed to stop the part, which is a physics bug and never pilot error.
6. **Grazes.** A `Time` < 1 contact with `Distance` > 0 (F8), whose latched plane gives δ ≤ 0, creates no force and no impact record.
7. **Log.** Every latch is an impact record: step, `Time`, part, object, shape, material, point, normal and normal speed. For the fiber it is a wrap or snag point, with a bend radius of `EdgeRadius` on boxes and the shape's own radius elsewhere. The radius needs F3.

**Why not rewind?**
- Placing the impact at the end of the step moves it by less than 1 ms.
- On the first step the depth is at most (1 − Time)·v·dt, which is ≤ 20 mm at 20 m/s. With the frame's series stiffness (~1e6 N/m, 10 kg) that is ω·dt ≈ 0.3, which is stable (review §1).
- A rewind would re-integrate the whole body with about 50 contacts, could cascade over several contacts in one step, and would make the step's cost unbounded, for no difference a pilot can feel at 1 kHz.

**Revisit** if QA crash replays show `tunnel` events, or energy gain at impacts.

## 5. Contact probe

This is a C# console program, `Probe.csproj` plus `Probe.cs` in the session scratchpad (`probe37/`). It is not committed. It compiles `game/src/world/*.cs` unchanged, without Godot, as `tools/worldbench` does. It built with only the expected "`ScalarReference` is never assigned" warning, ran to exit 0 on `sample_patch` (content hash `fdd49d4e2fea9f22`) and left the worktree clean.

The probe drone is 10 kg, a 450 mm X-frame, with 15 mm feet. Its contact law is review §1 with virgin loading only. These are probe assumptions, not tuned values.

1. **Four feet on the ground.** Each step makes one `SampleGround` call for the 4 feet. The soil and mat parameters are blended with `BlendSurface` and `BlendWeight` through `Surface()`. A level 3-DOF body settles over 3 s at 1 kHz.
   - **belt_straw:** mats 9–15 cm deep. The feet sink 59–101 mm (mat 35–64, soil 23–38), which leaves the drone at a 10.6° roll. This is manifesto item 1: one leg rests higher than another.
   - **meadow:** 25–43 mm.
   - **yard:** 18–22 mm on the leaf mat, μs 0.35.
   - **Across a meadow/belt_straw border:** blended soil stiffness 1,163 and 905 N/m.
   - **Pitfall:** one foot hangs over pitfall `0x7fe27fa9` (0.106 m deep) with F = 0, and the drone rests at 13.4° pitch. The log can name the hole.
   - **rubble:** the drone sits on 2 diagonal feet at 22°.

   Everything came from the API. No gaps apart from F4.
2. **Launch rails.** `AddObject("launch_rails", …, 90)` gave object 10.
   - **(a) Glide landing.** Descent at 0.5 m/s with the sticks held at 0.95 of the weight, onto bar 2. The first contact has `Time` 1, material `steel` with stiffness +∞, so the leg spring (20 kN/m, ζ 0.1) acts alone. Peak force 106 N, then **the leg bounces the drone 157 mm back up**: manifesto item 6, from the API as it is.
   - **(b) Fiber whip.** A 60 mm step across a bar gave `Time` 0.2875, normal +z and `Distance` ≈ 0 (5e-16 m). The latched-plane depth at the current pose was 42.75 mm. The next step reported `Time` 1 with the flipped normal (−z, −4.75 mm), and the step after that, nothing. That is the case the latch in §4 exists for.
   - **(c) A capsule along the bar:** A4.
   - **(d) Grazes.** A foot at 3 m/s gave `Time` 1. A fiber node gave `Time` 0.0533, `Distance` 5 µm (F8).
   - **(e) A cable contact:** F3.
3. **Rotor ground effect.** `Raycast` from 4 rotors, compared with `SampleGround` at the rotor.
   - 0.30 m over meadow, belt_straw and rubble: the ray reads 0.20–0.39 m because of relief, and the mat top on belt_straw is 9–17 cm higher than the ground. T_IGE/T_OGE is 1.007–1.026.
   - 0.50 m over the house roof: the ray hits object 0, `masonry`, at 0.5000 m.
   - On the rails, the rays pass the bars and hit the terrain.
4. **Micro-detail and hooks.**
   - The 2 m caches held 4,233 elements on belt_straw (Grass 1,330, Straw 2,892, Twigs 11) and 4,918 on meadow.
   - Landing legs touched 0–3 elements each. One was hooked by straw `0x28c52ff057f0fa04`, which lets go at 4.3 N.
   - Over 400 legs on flat belt_straw, **2.0 straws touch a leg and 21.5 % of legs are hooked** (review §4 estimated 39 %).
   - The lying-geometry numbers in F1 come from this part.
5. **Threads and allocation.** See §2.3: 0 B over 1,000 steps, and the worker prefetch was identical to a synchronous query (0.90 ms query, 6.7 ms from request to ready, including starting the thread pool, well inside a 20-step deadline).
6. **Odds and ends.** `DetMath.Pow` (F7), `Material(NoMaterial)` (F5), and the second set of rails (F2).

**What felt wrong**, in order: F1, F2, F3, F4, F5.
- Blending the mat parameters takes 16 lookups per foot and a "corners that have this mat" weighting. That is fine, and physics will precompute per surface.
- `Double3` has no scalar-first multiply or normalise, and normals are `float` `Vector3` next to `Double3` points. That's minor: physics converts at its boundary.

## 6. Determinism for replay

I re-ran `dotnet run -c Release --project tools/worldbench -- --golden` on this machine: **ALL PASS**.
- Release build on .NET 10.0.9 (the net8.0 target rolls forward), Windows 10.0.26200, x64, AVX2 and FMA supported.
- The content hash `fdd49d4e2fea9f22` matches the recorded one.
- All 39 cases match, both cold and warm (tier 1). Every batched ground, ray and micro-detail case is identical to its single-point or scalar reference.
- Concurrency: 10 s, 7,257 physics-thread and 4,549 calling-thread cases, 0 differing.

The content-hash rule (FNV-1a 64 over the seven files, with CRLF read as LF, `ContentHash.cs`, `README.md:275-295`) is sound for the data.

**Is it enough for a headless replay? It covers the world side of it, but a replay needs these as well:**

1. **Runtime objects** (asset, pose, order) are not in the hash. The log records every `AddObject` after the reset (F2), and the replay re-applies them. *(Physics/telemetry.)*
2. **The query math version** (F6). *(World Artist.)*
3. **The runtime and CPU.** The log header records the .NET version and the ISA flags, as the golden header already prints them. The golden file has run only on .NET 10.0.9 on this machine. The spec scenario "on any other machine" is still open. *(QA 5.2 or later: run it on a second machine and on the runtime the Godot build actually loads.)*
4. **Physics-side obligations**, for the physics change:
   - Micro-detail prefetch is requested at a step chosen from state (the centre has moved more than 0.5 m). The cache is swapped in at a fixed later step (for example +20). If the worker is late, the flight thread waits: that is a logged hitch, never a different result.
   - Hook state is keyed by element `Id`, so it survives a cache swap.
   - The flight model's own math follows W-13 (`DetMath`, or physics' own functions built only from correctly rounded IEEE operations): no `Math.Sin`, `Cos`, `Pow` or `Exp`, and no implicit FMA.
   - Every seeded draw is logged.

## 7. Tuning notes for the physics change (not requests)

- The belt_straw hook rate is 21.5 % per landing leg, which is about 0.9 hooked legs per lift-off. Re-measure after F1, because floating straws currently add touches.
- The ground slope under a single foot is steep:
  - crater_spoil: 31–42°
  - rubble: 21–37°
  - yard_litter: up to 24°, against μs 0.35 on the leaf mat

  Feet will slide on those slopes. Rubble is expected (review §4, until debris chunks arrive, X-1). Yard and crater spoil need a look in the first flight tests.

## 8. Risks and questions

- **Risk:** F1 adds one ground evaluation per lying element. That widens the accepted 3.6 micro-detail deviation on belt_straw. Physics accepts the extra cost on its worker at ≤ 40 Hz. The renderer's budget is the World Artist's call, and a per-cell cache would help.
- **Risk:** the contact law, leg stiffness, drone mass and foot size in the probe are assumptions. The numbers show the API is usable, not how the drone will fly.
- **Question (orchestrator):** should F1–F3 land in this change, as a new task before 4.1, or in a follow-up change ahead of the physics change? I recommend this change, before 4.1, for the renderer reason in the verdict.
- **Question (user, still open from review Q1):** the real launch-bar dimensions and which part of the drone rests on them. The derived values stand until then.
