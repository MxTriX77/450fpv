# Design: w21-rain-on-camera

## 1.1 The lens's water (physics engineer)

The rain on the FPV camera's lens, computed from the flight (D-126, FEED-13). Engine-free in the sim core (`Runtime/Wind/LensWater.cs`, beside `Rain.cs`), deterministic (DetMath only, no FMA), allocation-free after construction, and run on the drawn side or a helper once a PAL field, **never in the flight's 1 kHz step**. It reads the poses the flight publishes, as `PlantWash` does, and never feeds back.

- **Sources:** [lens-drops.md](../../../docs/reference-notes/lens-drops.md) (cited "note §n"), with every number's label and source.
- **Hand numbers:** `tools/flightbench/handcalc/lens_water.py`, independent arithmetic; its output is quoted in the note.
- **Labels:** pilot / measured / published / derived / assumed / numerical. Every assumed number is a named field of `LensSpec`.

### 1. What it reads

- **The poses** at each field's start, middle and end (steps 20k, 20k + 10, 20k + 20) from `PoseBuffer`'s history: position, attitude, velocity, body rate, the rotors' speeds.
- **The air at the lens**, a new render-side field `PoseFrame.AirVelocityAtCamera`: the same value as the feed signal of that name (the field's air relative to the lens, in the camera's axes), set in `FlightCore.GetPose` by the function `GetSignals` already uses. It is the only change on the flight's side. The pose is not logged, so no model number moves and the goldens stay byte for byte. Without it, a field's air would come only from the newest signals, at whatever step the render happened to read.
- **At construction:** the drone (`DroneSpec`: the lens's position, the motors, the props and their blade), the airframe as built (`MassModel`: its parts shelter the lens), the flight's rain rate (`FeedSignals.RainRateMmPerHour`, `RainField`'s draw; 0 when Sunny) and the flight's seed.

### 2. The lens

- **Camera axes** are the body's (0° uptilt): +x right, +y up, +z forward, the lens facing +z.
- **The front glass** is a spherical dome: apex at drone.json's `camera.lens_m`, clear radius a_c 4.5 mm, radius of curvature R_c 8 mm (note §1, assumed). A drop's place is its (X, Y) across the axis, in m; the glass lies `Sag(X, Y)` behind the apex there; its outward normal is (X, Y, √(R_c² − X² − Y²))/R_c. A flat lens is R_c = ∞.
- **The barrel's front face**, radius a_b 7 mm (assumed), shapes the air over the glass (§3.4).
- **Facets** (numerical): the dome cut into 19 patches, a centre disc and rings of 6 and 12 sectors over thirds of the clear radius. Each has its centre's normal and its true area (projected area / n_z).

### 3. One field (20 ms)

#### 3.1 The rain arriving

- **The drops:** Marshall–Palmer's slope Λ = 4.1·R^−0.21 mm⁻¹ over 12 bins from 0.1 to 6 mm (note §2). The intercept is set so the bins carry exactly the flight's rate, Σ n_D·V(D)·vol_D = R (derived: 6 800–7 400 against the published 8 000). Each bin keeps its number density, its geometric-mid diameter's fall speed V (Atlas et al.) and its mean drop volume (the truncated exponential's exact ∫D³).
- **The drops' velocity relative to the camera**, per bin: u_D = AirVelocityAtCamera + R_cam⁻¹·(0, −V(D), 0), at the field's middle pose.
- **The expected arrivals** per facet k and bin D: λ = n_D·max(0, −u_D·n_k)·A_k·T_kD·dt, with T the shelter's transmission (§3.2). Collection efficiency 1 (derived: Stokes number ≥ 25).
- **Published per field:** `ArrivalRate` (Σλ/dt, drops/s), `WaterRate` (m³/s) and `Sheltered` (1 − Σλ / Σλ with T = 1).
- **The draws:** the count is Poisson(Σλ), by inversion on one uniform, at most 8 a field (numerical: Σλ ≤ 0.2 in the Severe rain's fast flight). Each arrival takes its facet and bin by the cumulative λ, its place uniform over the facet's area, its diameter from the bin's truncated exponential (three uniforms). All come from the stream "lens.rain", keyed by `FlightRandom.StreamKey(seed, "lens.rain")`. Field k reads draws 64k to 64k + 63 by index (`SeedStream.BitsAt`), so a field's draws are its own whether or not other fields ran. The stream is outside the flight's log: the flight's own `FlightRandom` never opens it.

#### 3.2 The airframe's shelter

From each facet's centre, a ray runs back along −u_D toward where that bin's drops come from.
- **A solid part** it crosses shades that facet for that bin (T = 0): every box and solid cylinder of the airframe as built, except a part of mass 0 (off) and the props' mass shapes.
- **A prop's swept disk** (its hub from `motors[].position_m`, the props' radius, the disk across body y) passes the drop with 1 − P, the blade-hit share of note §1. P is taken at 0.75 R from the blade table and the rotor's speed in the pose, for the drop's speed along the rotor's axis.

Hand: nothing shades drops arriving from ahead. A tailwind's steep drops onto the dome's upper rim pass over the payload's front end, which shades some of them.

#### 3.3 An impact

- **The water left:** each arrival deposits its volume less the splashed share We/(We + 10⁶) on the normal speed (note §3, published).
- **The spread:** the lamella's radius is Laan et al.'s D_max/2 on the normal speed. Every drop whose patch reaches within that radius merges into the new one (§3.7).
- **At rest:** the drop is a spherical cap at the receding angle θ_R (derived, note §3).
  - Patch radius r = ∛(6V/(π·t·(3 + t²))) with t = tan(θ_R/2); apex height h = r·t.
  - Side silhouette A_f = (r/sin θ_R)²·(θ_R − sin θ_R·cos θ_R).
- **A full lens** (Capacity drops): the arrival merges into the nearest drop, so no water is lost.

#### 3.4 The forces on a drop

**The specific force in the lens's frame** comes from the three poses. The lens's world velocity is v_L = v + R·(ω×r_L), with r_L the lens's body position. The field's mean acceleration is a_L = (v_L(end) − v_L(start))/20 ms, turned into the camera's axes at the middle pose. With ω and ω̇ = (ω_end − ω_start)/20 ms, a drop at body offset ρ from the lens's apex, moving at v over the glass, feels per kg:

> f = g − a_L − ω̇×ρ − ω×(ω×ρ) − 2ω×v

- g is gravity in the camera's axes: the drone's tilt.
- The along-glass part, f − (f·n)n, pushes the drop.
- The off-glass part, f·n > 0, pulls it off (§3.8).

**The air at the drop.** The stream relative to the lens is split into u_n = −u·ẑ (onto the face) and u_t (the edge-on part). Over the face at the drop's (X, Y), at radius s:

> U_e = u_t + (2/π)·max(u_n, 0)·(X, Y)/√(a_b² − s²)

This is potential flow onto a disc broadside (published) plus the edge-on stream undisturbed (assumed); a face in its own lee keeps only u_t (assumed). It is projected onto the glass's tangent at the drop.

- **Boundary layer** (published constants): δ = min(√(ν/k)/1.312, √(ν·a_b/|u_t|)/0.332), with k = (2/π)·u_n/a_b, Homann's and Blasius's linear sublayers.
- **Drag** (derived): F_a = ½·ρ_air·C_D·A_f·Φ(h/δ)·|U_e|·U_e, with Φ(x) = x²/3 under 1 and 1 − 2/(3x) over it (the mean of u² over the drop's height). C_D = 1 is assumed; it reproduces the published depinning thresholds in a boundary layer and in a jet (note §5).

The drive is F = m·f_t + F_a. ρ_air is drone.json's `aero.air_density_kg_m3`.

#### 3.5 Pinned or sliding

- **Retention:** F_ret = k·γ·w·(cos θ_R − cos θ_A), with w the patch's width across the drive (2r/√e for elongation e), k = 1 (published), θ_A 50° and θ_R 20° (assumed, note §4).
- **Pinned:** |F| ≤ F_ret. The drop stays; its velocity and elongation drop to 0 and 1.
- **Sliding:** the speed is Le Grand et al.'s quasi-steady law, U = s·(|F| − F_ret)/(μ·V^(1/3)), along F, with s = 0.006 (derived, note §6). Quasi-steady holds because the drop's inertial time ρ_w·s·V^(2/3)/μ is 5 ms at 1 mm³ and 25 ms at 10 mm³ (derived): at or under a field. The drop moves U·dt across the glass (its X, Y by the tangent's components).
  - **Elongation:** e = 1 + Ca/Ca* while sliding, at most 3, with Ca = μU/γ and Ca* = θ_R³/(9·15) (assumed form and cap, published threshold; note §6). The long axis is the way it moves. A narrower drop is held by less (w = 2r/√e), so a drop once under way runs on: a sliding drop's dynamic retention below its static one.
  - **The Coriolis term** −2ω×v takes the previous field's velocity (explicit; numerical).

#### 3.6 Trails

A drop sliding faster than Ca* (2 cm/s) lays a film behind it: thickness h_f = 0.94·h·Ca^(2/3) (Landau–Levich–Derjaguin with the drop's height as the meniscus, note §6), over its width W and the field's path Δs.
- The drop loses that water (at most half of itself in a field; numerical).
- It appends a trail point at its rear (its centre less half its length along its axis): width W, film h_f, the field's last step.
- A drop keeps its newest `TrailPoints` points, oldest first. A merged drop keeps the larger's trail. A drop whose water falls under V_min (a 50 µm cap; numerical) has gone into its trail and is removed.

The trail's film neither evaporates nor is swept up again (note §7: about 100 s at the lens in rain).

#### 3.7 Merging

Two drops whose round patches touch (centre distance under r₁ + r₂) coalesce within the field (derived: milliseconds).
- **Volume:** the sum.
- **Place:** the volume-weighted centre.
- **Velocity:** the volume-weighted velocity.
- **Kept from the larger drop:** its Id, its trail and its axis.

A merge grows the patch, so the pass repeats until no two touch (at most Capacity passes; numerical).

#### 3.8 Shedding

A drop is shed when:
- its centre passes the clear radius: it is on the barrel, out of the picture;
- its off-glass pull m·(f·n) exceeds 2π·r·γ (assumed, note §4). A crash's deceleration does this; flight does not (hand: 24 g for a 5 mm³ drop).

`Arrived`, `Merged` and `Shed` count the flight's drops. `ArrivedWater`, `ShedWater`, `TrailWater` and `Water` keep the water's books: arrived = on the lens + shed + in trails.

### 4. Determinism, cost and bounds

- **Determinism:** DetMath and IEEE basic operations only; the counter-based stream (§3.1); fixed loop orders; no dictionary or sort on floats.
- **No allocation** after the constructor: fixed arrays for drops, trails, facets, bins and shelter shapes.
- **Bounds:** at most `Capacity` = 96 drops and `TrailPoints` = 16 points each (numerical). The steady count is set by coalescence well under that (hand: about 2 arrivals a second in fast flight, and large impacts sweep the whole glass).
- **Cost a field:** 12 bins × 19 facets rays against about 25 shapes (6 000 slab or cylinder tests), plus at most 96 drops and their pairs: well under a millisecond.
- **A render that falls behind** the history (128 steps) resumes at the newest whole field; the skipped fields are lost, as in `PlantWash`, and each later field's draws are unchanged.

### 5. The public shape (committed first, eb376009)

Per field, `LensWater.Drops` holds `LensDrop`s:
- **Id:** unique over the flight; a merged drop keeps the larger's.
- **X, Y:** lens m from the axis, camera axes, +X right and +Y up as the pilot sees the picture.
- **Volume:** m³.
- **Radius:** the patch's equal-area radius, m.
- **Height:** the cap's apex, m.
- **Elongation:** length over width, ≥ 1.
- **AxisX, AxisY:** the long axis, a unit vector.
- **VelocityX, VelocityY:** m/s over the glass.
- **Sliding.**
- **TrailStart, TrailCount:** into `Trail`.

`TrailOf(i)` gives drop i's `LensTrailPoint`s oldest first: X, Y (m), Width (m), Film (m), Step. The lens's size for drawing is `ClearRadius`, with `Sag(x, y)`. `Field` is the newest field stepped. The model adds `Spec` (the lens's numbers), `Place` (a drop set on the glass for a test or a bench) and the water's books. Nothing else is published.

### 6. The spec's scenarios as tests (`tools/simtests`, `LensWaterTests`)

| Scenario | Test |
|---|---|
| Forward flight wets the lens more than a hover | the same level lens, flat and domed, in a 5 m/s headwind hover and at 20 m/s: `ArrivalRate` against the same sum written again with System.Math. The flat lens's ratio is exactly the normal components' (4) |
| Wind direction matters | a level hover in a 2 m/s wind into the lens and from behind: into wets more; from behind wets only the dome's upper rim, as the fall allows, less `Sheltered` |
| Sunny keeps the lens dry | rate 0: no arrival and no drop over many fields |
| Tilt moves the drops | a held 30° bank (constant velocity, still air relative to the lens): a drop past gravity's threshold slides toward the low side; a 1 mm³ drop stays |
| Drops stay in fast flight and travel with the airflow | 20 m/s pitched 25°: a 0.1 mm³ drop stays; a 10 mm³ drop slides down the glass, and stays pinned in the same pose without the air |
| Goldens unchanged | flightbench and worldbench `--golden`; a rain flight's final state the same with the lens stepping beside it; two lenses on the same poses identical bit for bit |
| Bounds and 0 B | a long Severe rain flight: count ≤ Capacity, trails ≤ TrailPoints, the water's books balance; `Advance` allocates 0 B in steady state |

## 1.3 The wet look (tech artist)

The rain on the world's surfaces (VIS-03, D-126). **One input:** the sim's moisture for the flight's rain (`SurfaceTable.MoistureOf`: Sunny Dry, Rain Wet, Severe rain Saturated). Every lit shader reads it through the global keyword `FPV450_WET` and three globals (`Shaders/WetLaw.hlsl`), with the numbers `WetOptics` computes (`Scripts/Feed/Core`, tested in `WetOpticsTests`). Nothing is tuned per asset: each surface darkens from its own drawn albedo.

- **Labels:** published / derived / assumed / numerical, as in 1.1.
- **Hand numbers:** `WetOptics` itself, checked against the paper's printed values; the extinction against the lens's spectrum (3.1).

### 1. The darkening: Lekner and Dorf 1988

A wetted rough surface lies under water whose top is the air. Light the grains reflect diffusely meets that top from inside; a share p goes back down to the grains, which absorb a share again (Lekner and Dorf, Appl. Opt. 27, 1278, 1988, published). Per colour channel, with A the surface's drawn diffuse albedo (URP's `BRDFData.diffuse`, a Lambert shader's albedo) and a_d = 1 − A:

> a_w = a_d·(c·(1 − a_d) + a_d) (the grains under water absorb more, LD eq. 10–11)
>
> D = T·(1 − a_w)·(1 − p)/(1 − p·(1 − a_w)) (the light that leaves the water, LD eq. 1 without the top's own reflection)

| Number | Value | Label |
|---|---|---|
| n_l, water's index | 4/3 | published (1.333 at 589 nm, 20 °C; LD take 4/3) |
| n_r, the grains' index | 1.5 | assumed (quartz 1.54, feldspars 1.52–1.58, calcite 1.49–1.66, cellulose about 1.53: published ranges) |
| R̄(n) | ∫₀¹ R(x, n) dx, x = sin²θ | published (LD eq. 7); numerical (midpoint rule, 2¹⁶ steps) |
| T = 1 − R̄(n_l) | 0.9335 | derived |
| p = 1 − T/n_l² | 0.4749 | derived (LD print 0.475; Ångström's 1 − 1/n² is 0.437); equals R̄(1/n_l) to 1e-9 |
| c = (1 − R̄(n_r/n_l))/(1 − R̄(n_r)) | 1.0677 | derived (LD print 1.07, 1.08 and 1.10 at n_r 1.5, 2 and 2.5; reproduced) |
| Water's F0 = ((n_l − 1)/(n_l + 1))² | 1/49 = 0.0204 | derived |

| Dry A | 0.05 | 0.10 | 0.15 | 0.20 | 0.30 | 0.50 |
|---|---|---|---|---|---|---|
| Wet D | 0.0235 | 0.0482 | 0.0743 | 0.1019 | 0.1621 | 0.3073 |
| D/A | 0.469 | 0.482 | 0.495 | 0.509 | 0.540 | 0.615 |

- **Checks:** Ångström's sand, 0.182 dry, gives 0.092 wet against the 0.091 he measured (LD's table III). His black mould, 0.141, gives 0.070 against 0.084: the law's known shortfall on some soils (Zhang and Voss, Appl. Opt. 2006).
- **The colour holds:** D/A rises with A, so darker channels darken more (K§8.7 R4, "dark ground that keeps its colour").
- **A metal** (A = 0) has no body to darken; its specular stays its own.
- **Where:** every lit surface in the world, plants included (assumed: a flight's 10–20 min under rain wets everything; a roof's shelter is not modelled).

### 2. The top

The wetted surface shows the sky through water: the dielectric part of its specular takes water's F0. Its smoothness stays the material's own: the film, 0.1–0.3 mm (`RainPreset`'s film), is thinner than the relief of soil, bark, render or concrete and takes its slopes (assumed). So a wet surface is glossier by the darker body under the same reflection of the sky, not by a smoothing tuned by eye. Lambert shaders (grass, leaves) have no specular term, so they only darken.

### 3. Standing water (Severe rain)

Severe rain (the sim's Saturated set) stands water on hard, flat faces, deeper than their texture, so the water's top is what is seen.

- **Hard:** the sim's rigid surfaces. These are `physics_materials.json`'s rows without a stiffness (steel, timber, masonry, concrete, glass, cable, bark, deadwood, asbestos cement, plastic, carbon composite) and `surfaces.json`'s rigid sets (asphalt, the concrete slab). Each asset's `AssetPhysicsMeta` names a slot's physics by its visual material. A renderer drawing a rigid slot's material carries rendering-layer bit 7 while it rains (`WetSurfaces.Hard`, `WetLook.HardLayer`).
- **Flat:** the triangle's own plane (screen-space derivatives of the position, not the normal map), within the slope that drains a pavement: 2 % (published practice: carriageways are built at a 2–2.5 % cross-fall; assumed as the threshold), a normal's y of at least 0.99980.
- **The water's top:** that plane, at smoothness 0.766. GGX α = √0.003 = 0.055 comes from calm water's mean square slope (Cox and Munk 1954, the zero-wind intercept, published; assumed for standing water, whose fetch is too short for wind waves). The rain's own ripples are not modelled.

### 4. The rain's extinction

Koschmieder's law over the drops between the camera and each opaque pixel: c' = c·T + air·(1 − T), T = e^(−β·d), d the pixel's distance along its ray.

- **β from the drop spectrum** at the flight's rate (RainField's draw): Marshall and Palmer's N(D) = N0·e^(−ΛD) with `RainPreset`'s D0, N0 set so the spectrum carries the rate at Atlas et al.'s fall speed (as the lens's bins, 3.1), then β = Q·π·N0/(2Λ³) (derived).
- **Q = 1** (derived): Q_ext = 2 for drops far larger than the wavelength (van de Hulst 1957, published), but the diffracted half stays within λ/D, 0.03° for a 1 mm drop, far under the feed's pixel of about 0.2°.
- **The air** is the fog's colour, the horizon the scene's light gives the haze. On top of the scene's own fog the two transmissions multiply, which is the law for two media under one light.

| Rate, mm/h | 2.5 | 6.25 (Rain's middle) | 10 | 30 (Severe's middle) | 50 |
|---|---|---|---|---|---|
| N0, m⁻³ mm⁻¹ | 6 748 | 6 807 | 6 876 | 7 142 | 7 321 |
| β, 1/km | 0.274 | 0.492 | 0.668 | 1.387 | 1.961 |
| T at 200 m | 0.947 | 0.906 | 0.875 | 0.758 | 0.676 |
| Visibility (2 % contrast) | 14 km | 8.0 km | 5.9 km | 2.8 km | 2.0 km |

- **Rain as well as Severe rain:** one law, the rate decides. Rain's is weak, as K§8.7 R3 asks for clip P ("weak rain extinction"); VIS-03 names it for Severe rain, where it shows.
- **Drawn** by `Shaders/RainExtinction.shader`: a full-screen pass on the scene's cameras after the transparents, never on the feed's output camera. It reads the camera's own depth buffer, not the depth texture: the latter comes from SSAO's depth-normals prepass, which the farmland's grass and leaves shaders have no pass for, so their pixels read the ground behind them (seen in the first captures: crowns hazed like the far ground). The sky keeps its own colour.

### 5. How it is drawn

- **The project's and the farmland's shaders** (`LayeredSurface`, `Leaf`, `Impostor`, `Plant Bark`, `Plant Leaves`, `Plant Grass`, `Farm Land/Grass`, `Tree Bark`, `Tree Leaves`) declare `#pragma multi_compile_fragment _ FPV450_WET`. Their PBR passes include `WetLit.hlsl` before URP's `Lighting.hlsl`; it wraps URP's `InitializeBRDFData` (the body and the top) and `UniversalFragmentPBR` (the standing water). Their Lambert passes call `Fpv450WetAlbedo`.
- **URP/Lit and URP's Terrain/Lit,** which the project can't edit, have wet copies (`Shaders/Wet/`): URP 17.3's forward pass verbatim with `WetLit.hlsl`, every other pass URP's own by `UsePass`. While it rains, `WetLook` swaps each renderer's URP/Lit material, and each terrain's template, for a twin on the copy with the same properties, keywords, queue and passes. Sunny puts the originals back.
- **Sunny touches nothing.** `WetLook.Apply(Dry)` only undoes an earlier rain, and the keyword is never set, so each edited shader draws its keyword-off variant: its old source, token for token.
- **The player** carries the copies and their keyword sets through `Settings/Wet/Resources/WetShaders.asset` and `WetVariants.shadervariants`, written by `WetVariants` before every build. `WetLookTests` holds the collection to the project's materials.
- **The flight** applies the look in `FlightScene.Look`, after the light, at the flight's rate (the class's middle in the setup's preview). The farmland keeps its own light (D-088) and gets wet under it.

### 6. Evidence (`WetStills`, 2026-11-28)

`Fpv450.WetStills.Run -out <folder>`: each map's still view (`MapStills.json`'s pose, the farmland's Explore spawn), Sunny, Rain and Severe rain at the same light, before the feed. The test range and ХАНТЕР-09 use the overcast preset, the rain's; the farmland uses its own light. The stills stay local (scratch, never committed).

**The law on a ground patch** (the frame's lower middle, 29 440 pixels; fog, extinction and post off). The dry albedo per pixel is A = L_dry/I, from the render with the specular off (probe 1) and the diffuse light alone (probe 2). The law's wet light is Body(A)·I, against the wet render's.

| Map | Patch | Mean drawn albedo | Rain: measured / law | Severe rain: measured / law | With the specular (Rain) |
|---|---|---|---|---|---|
| Test range | the yard's ground (the gallery floor, URP/Lit's twin) | 0.1208 | 0.4887 / 0.4887 (per pixel ≤ 0.08 %) | 0.4887 / 0.4887 | 0.507 |
| Farmland | bare ground before the stand (the terrain's wet copy) | 0.1305 | 0.4933 / 0.4930 | 0.4933 / 0.4930 | 0.495 |
| ХАНТЕР-09 | the meadow (terrain and grass) | 0.2473 | 0.5619 / 0.5612 | 0.5612 / 0.5612 | 0.561 |

- **Within the capture's error:** at most 0.13 %; the meadow's grass sways between renders.
- **The dry reference through the wet shaders at identity**, against the true Sunny render: on the test range 1 pixel of 921 600 differs, by 0.0015 (linear). On the farmland and ХАНТЕР-09 the grass and leaves sway on the editor's clock between renders, so most pixels differ there.

**The cost** (8 renders a sync at 1920 × 1080, Sunny, Rain and Severe rain interleaved over 15 rounds, medians). It was measured on battery: the machine ran on its Radeon 680M iGPU, so this is not PLT-07 evidence.

| Map | Sunny | Rain | Severe rain | p10–p90 |
|---|---|---|---|---|
| Test range | 7.19 ms | +1.94 ms | +1.04 ms | 3.0–3.3 ms |
| Farmland | 64.13 ms | +1.96 ms | +1.21 ms | 8.3–12.4 ms |
| ХАНТЕР-09 | 56.79 ms | +2.27 ms | +1.44 ms | 8.0–10.0 ms |

### 7. Limits

- **Hard is per renderer:** a renderer is hard if any slot it draws is rigid. A slot is matched by its visual material's name, so a layered material composed from a slot (the houses' facades, the car's paint) is not matched.
- **No standing water on some surfaces:**
  - the farmland's village carries no `AssetPhysicsMeta`, so none of its faces is hard;
  - the sim's contact table makes sheet metal compliant, so car bodies and roof sheets are not hard.
- **Everything is wet:** interiors and undersides are wetted too; a roof's shelter is not modelled.
- **The extinction:**
  - its distance ignores an off-axis projection's shift;
  - with the scene's fog off it still applies, as its own medium.
