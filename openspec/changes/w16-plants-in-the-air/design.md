# Design: w16-plants-in-the-air

## 1.2 The law of the plants in the air (physics engineer)

The plants bend in the sim's own air: the wind field the drone flies in and the props' wash. The law is engine-free in the sim core (`Runtime/Wind/`: `PlantLaw.cs`, `PlantAir.cs`, `PlantWash.cs`, `WindField.Plants.cs`), deterministic (DetMath only, no FMA), allocation-free after construction, and runs on the drawn side's frame or a helper, **never in the flight's 1 kHz step**. Nothing in it moves a flight: the only change on the flight's side is one published field (`PoseFrame.RotorThrust`, §7.3).

- **Sources:** [plants-in-the-air.md](../../../docs/reference-notes/plants-in-the-air.md) (cited "note §n"), p7d-tether design 19 and 20 (the wash), w9-village-physics 2.12–2.13 (the stems' lean law).
- **Hand numbers:** `tools/flightbench/handcalc/plants_air.py`. It is independent arithmetic: the full law with the drag on the air relative to the moving stem, RK4 at 0.1 ms, and design 19's air as `stem_wash.py` works it. Its output is quoted below.
- **Labels:** pilot / measured / published / derived / assumed / numerical. Every assumed number is a named constant in the code.

### 1. One bending mode per element

**The link.** Each plant element is a rigid link of length L from its base, on a rotational spring k. Its lean is a horizontal vector b = φ·ê (rad), and its axis is t = (sin φ·ê, cos φ). This is w9-village-physics 2.12's lean law, the one `PropStems` flies:

- a stem's k = 4EI/L, the cantilever's tip slope under a uniform load (derived);
- its weight's moment is W·sin φ with W = μgL²/2 (derived);
- the drag a metre on the air normal to it is c(|v|)·|v|·v with c = ½ρ(C_d·d + C_f·a·R(|v|)).

**The inertia** is chosen so the link swings at the element's own first mode: I = k/ω₁².
- For a stem, a twig or a branch, ω₁ = 1.875²·√(EI/μL⁴), the cantilever's (published; PropStems' 1.875).
- For a tree, ω₁ is the published or assumed f₀ (§1.2).
- A rigid link with the rod's own inertia μL³/3 would swing within 1.6 % of the cantilever (derived), so this choice only removes that 1.6 %.

**The motion:**

> I·b̈ + c_s·ḃ + k·(b − b₀) − W·sin φ·ê = Q(b, ḃ, u)

- **Structural damping:** c_s = 2ζ_s·√((k − W)·I).
- **The air's force:** Q is the generalised force ∫ f·s·∂t/∂b ds over the drag span. Along the lean it is Q_∥ = ∫ c|v|·v_n·s ds, with v_n = (u·ê)·cos φ − u_y·sin φ. Across it is Q_⊥ = (sin φ/φ)·∫ c|v|·v_⊥·s ds.
- **The air's damping** follows from the drag acting on the air relative to the moving element: v_n − s·φ̇_∥ and v_⊥ − s·(sin φ/φ)·φ̇_⊥ (quasi-steady). Its slope in the rate, linearised at rest, is the note's ζ_a = ρC_d·dU/(2μω).
- **b₀**, the natural lean, is used only in the wash grid (§4.2).

**One step** of dt holds the air (zero-order hold) and integrates each axis exactly, linearised at the step's start (derived; `PlantLaw.Exact`):
- **Along the lean:** the tangent stiffness k − W·cos φ − ∂Q_∥/∂φ, with the drag's part by a forward difference of 10⁻⁴ rad.
- **Across it:** k − W·sin φ/φ (the drag's part is nil to first order).
- **The drag's slope in the rate** joins c_s; the rest of the drag is held. Each axis steps toward its Newton target.
- **Stability:** the step is unconditionally stable. A tangent is floored at 0.01·k, so a stem whose weight passes its stiffness runs over in a few steps.
- **The ground:** the link stops at |b| = π/2.

#### 1.1 Stems from the cover rows (one set of numbers with the props' stems)

`PlantMechanics.Stem` reads each class from `surfaces.json`'s cover rows, the numbers `PropStems` flies:
- d = d₁·L^(1/1.53) (Niklas 1995, published);
- the hollow section from the row's wall share;
- E is the middle of the row's band (PropStems' field stems);
- ρ is the row's;
- C_d is `PropStrikes.StemDrag` (1.15, published);
- ρ_air is the line's (tether.json, 1.225).

| Class (`PlantKind`) | Row | Labels beyond the row |
|---|---|---|
| GreenCulm | grass_blade | ζ_s 0.08 (published: a wheat shoot, Sterling et al. 2003; note §7) |
| DryCulm (standing straw) | dry_culm | ζ_s 0.08 (published, as above) |
| BurntCulm | burnt_culm | ζ_s 0.08 (published, as above) |
| WeedStalk | stem | d₁ 8 mm (assumed: 5–15 mm, stem-properties.md); its leaves double μ (assumed, note §3.2); ζ_s 0.1 (assumed: the cover rows' damping_ratio) |

The classes from the hand calculation, which the suite reproduces to 10⁻⁴ (`TheClassesAreTheHandCalculations`). In the table, "f, with its weight" is in Hz, and ζ_a is the air's damping ratio upright at 2 / 5 / 10 m/s.

| Element | d (mm) | k (N·m/rad) | f, with its weight (Hz) | ζ_a at 2 / 5 / 10 m/s |
|---|---|---|---|---|
| green culm 0.45 m | 1.07 | 2.54e-3 | 2.16 | 0.29 / 0.73 / 1.45 |
| green culm 0.67 m | 1.39 | 4.83e-3 | 1.15 | 0.42 / 1.05 / 2.11 |
| green culm 0.97 m | 1.77 | 8.78e-3 | 0.55 | 0.69 / 1.73 / 3.46 |
| dry culm 0.2 m (stubble) | 1.57 | 8.12e-3 | 15.3 | 0.07 / 0.18 / 0.35 |
| dry culm 0.9 m | 4.19 | 9.20e-2 | 1.92 | 0.21 / 0.52 / 1.05 |
| weed stalk 0.5 m | 5.09 | 0.572 | 4.05 | 0.01 / 0.04 / 0.07 |
| leafy twig 5 mm × 0.5 m | 5 | 2.43 | 6.21 | 0.03 / 0.04 / 0.05 |
| branch 40 mm × 3.5 m, leafy | 40 | 1422 | 0.99 | 0.16 / 0.16 / 0.17 |

The tallest drawn green culm (1.34 m) stands with W/k = 0.98: the cover row's E is the least that lets it stand (surfaces.json, derived there). It sways at 0.07 Hz and is overdamped by any air.

#### 1.2 Twigs, branches and trees

- **Woody elements.** `PlantMechanics.Woody` builds a leafy twig or a branch:
  - **The wood:** solid green wood of the twig row, E the middle of 7–12.8 GPa (published, Wood Handbook) and ρ 950.
  - **The foliage:** its mass a metre is assumed from the note's leafy-to-bare frequencies (§3.2): about 20 g/m on a 5 mm twig and 1.2 kg/m on a 40 mm branch, with the leaves' mass equal to the wood's.
  - **Its area:** that mass over the leaf row's 0.2 kg/m² fresh (derived).
  - **Its drag coefficient:** C_f 0.3 on the one-sided area (assumed, within Katul et al.'s 0.1–0.5, the note's [20]).
  - **Its reconfiguration:** Vogel −1 (published: leaves rolled into cones and dense crowns, Gosselin 2019) from U_c 4 m/s (assumed: the hardwood tunnels' lowest speed, Vollsinger et al. 2005).
  - **Damping:** ζ_s 0.086, a crown in leaf (published, Jackson et al. 2019).
- **Trees.** `PlantMechanics.Tree` builds a whole tree's sway:
  - **The link:** the tree to its top. Its k = 3EI/h_c is the trunk's cantilever loaded at the crown's centre of pressure, at the drawn diameter (derived).
  - **f₀:** a pine's comes from Moore and Maguire's regression as restated (published); a broadleaf's is 0.45 Hz (assumed, 0.3–0.6, note §7).
  - **ζ_s:** 0.086 for a broadleaf (published); 0.05 for a pine (published as an upper bound).
  - **The crown's drag:** its silhouette, C_f 0.3 times its width a metre over its depth, with Vogel −1.
  - **A gap:** no measured sway amplitude was found. A trunk as thick as the drawn IslandTree (1.1 m) barely moves, and the visible motion is the branches' and twigs'.
- **Leaf flutter** is 6 Hz (published: a cherry leaf's torsional mode, 6 ± 1, Tadrist et al. 2018). It runs on each leaf's own phase, from the drawn instance's own hash (world-derived). Its amplitude law was not found (a gap, §10).

#### 1.3 The two regimes in one law

ζ = ζ_s + ζ_a(|v_n|) comes out of the drag on the relative air; there is no switch between regimes.
- **Overdamped grass.** A 0.67 m green culm is overdamped upright from about 4 m/s (ζ 1.13 at 5 m/s). It follows a gust with a lag and creeps back.
- **Leaning lowers its damping.** Leaning sees less air across it: at 5 m/s and 0.79 rad its ζ is 0.83 (`GrassIsOverdampedByTheAirAndTwigsRing`).
- **Thicker elements ring.** Twigs, branches and weed stalks have ζ under 0.3 at 10 m/s.

**The drag's reconfiguration past Cauchy 1:**
- **Stems: the geometry does it.** The link's projected drag c·U²·cos²φ balanced by kφ gives an effective Vogel exponent of −4φ·tan φ/(1 + 2φ·tan φ) (derived): −0.5 at 0.4 rad, −0.7 at 0.5 rad and −0.9 at 0.6 rad.
  - This is the published −2/3 for blades and filaments (Gosselin 2019), where a culm passes Cauchy 1 (1.3 m/s for 0.67 m).
  - It tends to −2 lying flat, where a real stem keeps a short upright base and goes on as U^(4/3). That affects the force, which 1.5 owns, and not the drawn angle, which is flat either way.
- **Foliage: an explicit factor.** It reconfigures by R(|v|) = (|v|/U_c)^−1 on top of that.

#### 1.4 Hand calculation: a culm and a branch under a 1 s gust

The gusts are uniform air at the load, stepping u₀ → u₁ for 1 s and back, starting from rest at u₀'s lean.

| Element and gust | ζ at u₀ / u₁ | Peak | After the gust | Within 5 % of rest |
|---|---|---|---|---|
| green culm 0.67 m, 5 → 9 → 5 m/s | 1.13 / 1.98 | 1.162 rad at 0.21 s (the step 1.082 → 0.785) | creeps back: a single dip 16 % past rest at 1.30 s, as it leans out of its own damping | from 1.43 s |
| green culm 0.67 m, 2 → 6 → 2 m/s | 0.50 / 1.34 | 1.018 rad at 0.29 s | swings 16 % past rest at 1.46 s | from 1.67 s |
| branch 40 mm × 3.5 m, 2 → 6 → 2 m/s | 0.24 / 0.25 | 0.179 rad at 0.52 s | rings: 33 % past rest at 1.52 s, then 0.038, 0.025 … at its 1.0 Hz | from 2.69 s |

`AGustBendsAndTheyReturnAsTheHandCalculation` checks the law's step against these samples at 0.1, 0.25, 0.5, 1, 1.25, 1.5, 2 and 3 s:
- at 1 ms: within 0.0001 rad and 0.001 rad/s of the hand's RK4 (one law, two integrators);
- as the wash grid steps it (40 ms ticks, 20 ms sub-steps): within 0.02 rad.

### 2. Where the air comes from

| Air | Source | How |
|---|---|---|
| The wind field | the flight's `WindField`: the direction's meander and shifts, the profile, the hills, the belts' canopy strips, the wakes, a step's lee | read at any time t without the flight's step frame (§3) |
| The props' wash | the line's law, `RotorAir` (p7d-tether design 19): the near columns, the merged jet, the wall jets, the fountain, the ground vortex | an instance of its own, begun from the published pose (§4) |

### 3. The wind's part, in closed form (`PlantAir.Wind`)

**Why closed form.**
- The field is frozen modes drifting with the mean at the reference height (Taylor, p7c-wind 1.4). They are the open modes, a strip's eddies at their convection and the wake eddies at the obstacle's scale.
- At a fixed plant, each mode is a sinusoid A·cos θ with θ falling at its encounter rate Ω = κ·V.
- The link's response to each mode is the steady response of I·ẍ + c·ẋ + K·x = F·cos θ:
  - x = F·(P·cos θ − cΩ·sin θ)/(P² + c²Ω²) and ẋ = F·Ω·(P·sin θ + cΩ·cos θ)/(P² + c²Ω²), with P = K − IΩ² (derived);
  - this is exact for the linear link at every time: the history of the frozen field at the point is the sum of these sinusoids.
- So a sample is a pure function of the frame's time, the place and the plant. The drawn side keeps no state, and seeking a replay needs no warm-up.
- Each gust reaches each plant when the field brings it there, from its own direction. The plants lean along the local wind, which meanders and shifts.

**The mean lean and the gains, statistically linearised** (Caughey 1963; Atalik and Utku 1976).
- **Why:** near the ground the turbulence is as strong as the mean (σ/U up to about 0.9 at a culm's load point: the field's σ_u = 0.164·U_ref against its log profile's 0.19·U_ref at 0.5 m).
- **The mean lean:** solves E[Q(φ)] + W·sin φ = k·φ over the normal air's Gaussian spread along the mean. The spread is from the field's own modes at the point, their covariance Σ aᵢaⱼ/2.
- **The gains:** the expected slopes.
- **For a bare stem**, in closed form (Borgman 1967, published): E[|v|v] = (μ² + σ²)·erf(μ/√2σ) + μσ·√(2/π)·e^(−μ²/2σ²).
- **With foliage**, by 12-point Gauss–Hermite (published nodes).
- `TheExpectationIsGaussian` checks both.

**Checked against the link's own response.** `TheClosedFormIsTheLinksOwnResponse` drives the link step by step (1 ms) in the field's own air (`WindField.At`) and compares PlantAir over 60 s in Windy:

| Element | Correlation | rms ratio | Mean lean |
|---|---|---|---|
| branch, its base 5 m up | 0.993 | 1.035 | −0.0414 against −0.0432 rad |
| green culm 0.97 m | 0.986 | 1.04 | −0.205 against −0.199 rad |

**The plants read the flight's field.** `ThePlantsReadTheFlightsField` checks 1200 points over the farmland in Windy (its hills, belts and wakes). They match `WindField.At` within 1.2e-12 m/s.

#### 3.4 Left out of the plants' reading of the field

- **A cut's cavity** is the drone's frame: a plant in a crater takes the open field over its own ground.
- **Above a step's lip in its lee**, the plants keep the floor's fluctuation under the blended mean.
- **A gust front** (a test scenario only) is taken as part of the mean: quasi-static.
- **The wake grid** is the flight's. It is read through `WakeGridWorker.Sample`, whose fade state the flight thread writes: a torn read in a 2 s fade is possible, a visual glitch at worst, never a flight's.

### 4. The wash's part: the grid (`PlantWash`)

#### 4.1 The air

- **The inputs.** Each tick begins its own `RotorAir` with the line's inputs, read from the published pose at that tick's own flight step (`PoseBuffer` history):
  - the hubs;
  - the rotors' mean thrust (`PoseFrame.RotorThrust`, new);
  - the field's air at each hub less the hub's velocity (`PlantAir.AirAt`);
  - the ground under each.
- **The fountain:** the drone's body over it is left out. It shapes only the air within a body's depth under the drone, where stems meet the legs, which is 1.5's.
- **The tick:** 40 flight steps (25 Hz), each with 2 sub-steps of 20 ms for the stands (numerical).
- **The drawn side** reads between the last two ticks, interpolated linearly.
- **The air is skipped** while the wash where it lands is under 3 m/s (`MinWash`; published: no visible change in a crop under 3 m/s, the note's [21]). That is from about 12 m up for this drone's hover; the stands then relax in still air.

#### 4.2 The stands

**The grid:**
- each node is a stand of every band the drawn side asks for (`PlantMechanics[]`);
- it sits on a world-anchored grid that follows the drone by whole cells;
- defaults: 32 × 32 nodes at 0.15 m, a 4.8 m window.

**Each node:**
- **The air:** the node samples it on its vertical at 11 heights over its ground (0.008, 0.02, 0.98·h_j, 1.02·h_j, 0.08, 0.15, 0.27, 0.45, 0.7, 1.0, 1.4, 1.9 m, to the tallest band). h_j is the near jets' top-hat thickness, 4.5 cm.
- **How a stand reads it:** each element takes the air at its own height y = s·cos φ, so Q = (1/cos²φ)·∫ g(y)·y dy, by the trapezoid (`PlantLaw.Profile`).
- **The natural lean** is PropStems' hash mean (7.5°), its way by the node's world hash (world-derived, as PropStems' stems).
- **The bend** is the stand's lean less its rest under its weight.

**Entering and leaving.** A node entering the window starts at rest. One leaving it is dropped; this is the window's truncation (§4.4).

#### 4.3 Hand calculation: a hover over grass at 0.5, 1, 2 and 4 m

The hover is 4.735 kg (the stem scenarios'), in still air, over flat ground. The hand works the bend of a green 0.6 m and a dry 0.9 m culm by radius along the diagonal through a hub, from rest, outward.

| Hubs up | The merged jet lands (share, U_g, r_i) | Green 0.6 m | Dry 0.9 m |
|---|---|---|---|
| 0.5 m | 0.07, 14.1 m/s, 0.51 m | folded under each column (1.40 rad at 0.2 m); 0.10 rad at 0.4 m, 0.04 at 0.6, 0.01 at 1.2 | folded under the columns; ≤ 0.012 elsewhere |
| 1 m | 0.81, 12.2 m/s, 0.59 m | folded under the columns (1.34); 0.07 at 0.6 m, 0.04 at 1.2, 0.02 at 4 | folded under the columns; ≤ 0.008 elsewhere |
| 2 m | 1.0, 9.5 m/s, 0.76 m | pressed in the landing (0.95 rad at its centre, 0.82 at 0.2 m); 0.06–0.08 out to 2 m, 0.04 at 4 | 0.08 at the centre; ≤ 0.01 elsewhere |
| 4 m | 1.0, 6.7 m/s, 1.09 m | 0.17 at the centre; 0.07–0.08 at 1–1.4 m, 0.04 at 4 | ≤ 0.02 |

**The patch shrinks with height,** and the ring hardly does:
- at 0.5–1 m it is the four columns' footprints, each 18 cm across;
- at 2 m it is the merged jet's landing, pressed, not flat;
- by 4 m it is nothing.

The leaning ring reaches as far from almost any height, because the wall jet carries the jet's momentum flux (the note §4.2).

**Checked against the law:**
- `AHoverBendsTheGrassAsTheHandCalculation`: the law on the grid's own 11 heights matches these within 0.03 rad where the bend is small, and 0.05 where the columns fold it (the hand integrates 300 heights).
- `AHoverFlattensItsPatchAndLeansItsRing`, over the grids:
  - flat 0.10 m² under the discs at 0.5 and 1 m, on a 5 cm grid;
  - pressed (≥ 0.5 rad) 0.26 m² within 0.3 m at 2 m;
  - none at 4 m;
  - leaning (≥ 0.05 rad) out to 0.53, 1.04, 2.69 and 3.08 m.

**Against clip H and the note's §4.2 (not met; traced):**
- **What the law gives low down.** At a creep 0.3–0.6 m up, design 19's mean air folds only the columns' footprints. The ring beyond leans 0.02–0.1 rad.
  - Clip H shows standing stalks fluttering within about 1 m.
  - The note's §4.2 estimate had the outwash over the whole stalk.
- **Two causes:**
  - The near wall jets are a top-hat 4.5 cm deep (h_j = 0.354·R, from mass conservation), so a culm takes their 8–12 m/s on its lowest few centimetres.
  - The law carries the jets' mean only: their turbulence and the tip vortices landing (the note's [25]) are what flutter the stalks.
- **Both belong to the near field that w9-village-physics 2.14 owns** (the near wake's spread and turbulence; D-111).
- **A third, for the same task:** over a dense stand the wall jet runs on the canopy's displacement height, not on the soil. The sim's wash takes the soil as its ground.
- **This law takes the air as it is:** when 2.14 changes it, the plants follow with no change here.

#### 4.4 The window

- **The size bounds the trail.** A node leaving the window loses what it held. A fast low pass leaves a trail longer than 2.4 m behind the drone, and the stands at the window's edge return to rest at once.
- **The residual there is small.** The wall jet at 2.4 m from a low hover bends a culm about 0.05 rad.
- **The column needs the fine grid.** The near columns (0.0898 m radius) are finer than the default cell: a fine grid (5 cm, 48 nodes) resolves their footprints.
- **Two instances.** The drawn side can run both a fine and a coarse grid (§7).

#### 4.5 Plants above the ground (a crown near the discs)

- **No state for them.** A twig or a branch the grid does not integrate takes the wash at its load point as a steady air on its mean lean (quasi-static).
- **Why that holds at a creep.** Their modes (1–15 Hz) are fast beside the wash's changes at a creep.
- **What is lost:** the ringing a fast pass would leave is not resolved.

#### 4.6 The wind's gusts under the wash

- **A grid stand's lean:**
  - the wind's mean lean, plus the grid's bend through the wash in full;
  - and the wind's gusts, linearised about that whole lean in the wind and the wash at its load point, so the wash's own damping holds them.
- **Two approximations, both small where the wash dominates:**
  - The mean is superposed: the wind's mean lean plus the wash's bend.
  - The wash's air for the gains is read at one point.

### 5. The dust (`DustLaw`, `PlantWash.DustAt`)

**The threshold.** Dust is knocked loose by saltating sand-sized grains, so its threshold is the saltation threshold on the wall jet's friction velocity u* (Kok et al. 2012, published; the note §5).

**u\*** is the larger of two laws (the transitional wall lies between them and tends to the larger). Both take the wall jet's peak speed along the ground, from the node's samples, at its own peak height 0.165·δ:
- **the smooth wall:** u_m·√(c_f/2), c_f = 0.06·(u_m·δ/ν)^−0.3·(r/H)^−0.16 (Poreh et al. 1967, published);
- **the fully rough wall:** u_m/(5.75·log₁₀(y_m/k_s) + 8.5) (Nikuradse, published).

**δ:**
- the merged jet's, δ = c·R·(R/H)^e, R = max(r, r_i) (RotorAir's own law and rows), where the merged jet carries the landing;
- elsewhere the near jets' top-hat, its thickness taken as δ (assumed).

**Wet ground lifts no dust (derived; the orchestrator's decision, 2026-11-14).**
- **What the sim calls wet.** The flight's rain sets the moisture (`SurfaceTable.MoistureOf`): Rain gives the wet sets, Severe rain the saturated ones. No surface carries a moisture of its own.
- **How wet that is.** The wet set holds a loam's field capacity: 25 % water by volume (`water_content_wet`, assumed there; Saxton and Rawls 2006's 0.2–0.35). Over the surfaces' dry bulk densities that is 15.6–23.8 % by mass.
- **Why no grain moves.** At field capacity the topsoil's grains are bound in its cohesion. The sim's own wet sets carry 0.6–18 kPa (`cohesion_pa`: crater_spoil 0.6, tilled 1.2, belt_bare 3.6, dry_crust 12, dirt_track 18), and the saturated sets 0.3–9 kPa. The wash's shear on the ground is ρu*² ≤ 7.6 Pa: u* reaches 2.49 m/s at most, 0.25–0.3 m up. That is 40 to 2400 times below what would part the grains.
- **Where Fécan's factor still applies.** Fécan et al.'s √(1 + 1.21·(w − w′)^0.68) (published) is kept only up to 10 % water by mass (`DustLaw.FecanMaximum`, assumed: the band the reference note reads the law over, ×1.5–2.5 at 5–10 %). Past it, no dust. Every wet set of the sim lies past it, so in a flight Rain and Severe rain lift none.
- **Covered surfaces** lift none (published in kind): their chaff is 1.4's.
- **u\* is still reported everywhere the wash blows** (`DustSample.Friction`); only the flux needs a finite threshold.

**The flux:** past the threshold it is White's Q = 2.61·(ρ/g)·u*³·(1 − r²)·(1 + r) (White 1979, recalled).

| Surface | u*_t dry (label) | Dust from, dry | Dust from, wet | Note §5.3's band |
|---|---|---|---|---|
| crater_spoil | 0.19 (published band) | 10 m | none | 6–10 m |
| dirt_track (film) | 0.22 (published band) | 8 m | none | 5–10 m |
| belt_bare | 0.27 (assumed) | 6 m | none | 3–6 m |
| burnt_field | 0.25 (published, ash) | 8 m | none | 3–10 m |
| tilled | 0.375 (published) | 4 m | none | 1.5–4 m |
| dry_crust | 0.75 (assumed) | 1.5 m | none | 1–3 m |
| rubble (film) | 0.37 (assumed) | 4 m | none | 1.5–5 m |
| asphalt, concrete_slab (film) | 0.25 (assumed) | 6 m | none | 5–10 m |
| meadow_sod, weeds, belt_straw, yard_litter | — | none | none | none |

**Notes on the table:**
- **k_s:** 2 mm on soils and 0.5 mm on paving (assumed).
- **A paved film's bulk density:** 1300 kg/m³ (assumed).
- **Supply is not modelled.** A film surface's loose layer runs out in reality ("short whiffs, then clean"); the law flags it (`DustSample.Film`).
- **The grid matches the hand.** `DustRisesAsTheDroneComesLower` gives the same onset heights as the hand, over the grid's own nodes, for every surface, dry and wet.
- **The rain check, in the same test:** a hover 0.25 m over bare loam (belt_bare) lifts dust at 656 nodes dry and at none in Rain or Severe rain, with u* up to 2.49 m/s in each.

**Clip H's damp dark soil and the spec's "a landing … in rain … no dust": met.**
- In a flight with rain, the wet topsoil lifts nothing at any height.
- Clip H's soil looks damp on a dry day, which the sim does not represent: the sim's moisture follows the rain alone. If the pilot flies H's scene with Sunny, its bare loam lifts dust from 6 m. That is the dry case, F's veil.

### 6. One law with the props' stems (`PropStems`)

- **The same static law.** The rigid link on 4EI/L, its weight's moment, and the drag normal to it with C_d 1.15 on the cover rows' sections. A stand in the grid rests where a PropStems stem in the same air would.
- **The dynamics differ.**
  - PropStems moves its lean by φ' = −m/τ, τ = 1/ω₁: first order, a link with no inertia whose damping is fixed at ζ = 0.5.
  - This law is second order, with ζ = ζ_s + ζ_a(U) from the drag on the relative air.
- **What would move if PropStems took this law** (so it stays as it is; a later model number, the orchestrator's call):
  - **The fold under a disc's column would be slower,** by about ζ_a/0.5. A 0.6 m green culm in a 19 m/s column has ζ_a of 3–4, so it stands 6–8 times longer in the disc's plane. That means more fresh cuts, and the strike impulses and the shake that follow from them.
  - **A released stem would overshoot** past its rest in still air (ζ about 0.1–0.5) and could swing back into a disc.
  - The cut counts, the stems' log episodes and the step-budget figures of w9-village-physics 2.12–2.14 would all move.
- **The air under the discs also differs, as in flight today.**
  - PropStems flies 2.12's undiminished column under each disc (D-111).
  - The drawn plants take design 19's air, the line's. Under the discs low down the two agree (the near column, 19 m/s). Higher up 2.12 keeps T/A where design 19's jet has merged and weakened (D-111's own numbers).
  - When the stems' wash flies (2.14), the two read one air.

### 7. The interface for 1.3, 1.4 and 1.5

#### 7.1 Per frame (the drawn side)

Construct once per flight:
- `var plants = new PlantAir(flight.Wind);`
- optionally one or two wash grids: `new PlantWash(tetherTable, propRadius, motorsBodyFrame, world, plants, bands, surfaces, SurfaceTable.MoistureOf(rain), size, cell)`. A coarse grid (32 × 0.15 m) shows the ring; a fine one (32–48 × 0.05 m) shows the columns' patch.

Each frame:
1. `wash.Advance(poseBuffer)`: ticks to the newest published step.
2. `plants.Begin(frameTimeSeconds)`: the frame's flight time, any fraction of a step.
3. Per plant class, size band and place: `plants.Sample(mechanics, band, wash, basePoint, groundHeight, out PlantBend bend)`.
   - band is the mechanics' index in that wash grid, or −1 for a plant the grid does not integrate.
   - For a grid band the sampler reads the fine grid first and then the coarse one.
   - Without a wash use `plants.Wind(...)`.
4. Dust: `wash.DustAt(x, z, out DustSample d)` gives u*, the threshold, whether dust lifts, White's flux (kg/m/s), whether the surface is a film, and the surface id.

`PlantBend` is the bend from the drawn rest (rad, world X and Z: the top's way times the angle) and its rate (rad/s). It never passes π/2.

**Threads.** A grid's `Advance` and the samples that read it run on one thread. A helper that ticks the grid while the render thread samples it needs its own double buffer (1.3's). `PlantAir` itself holds only the frame's time, mean and drift.

**Mechanics** come from `PlantMechanics.Stem(kind, length, coverSet, airDensity)`, `.Woody(...)` or `.Tree(...)`. The drawn side buckets each class's sizes: f₁ scales as L^−1.35 for culms.

**The cost on this shared machine** (idle priority, half the CPUs, not on AC power, so indicative only):
- a sample is about 10–20 µs on the CPU (the field's 233 modes: 138 open, 71 canopy and 24 wake);
- a 32 × 32 grid of 4 bands ticks in about 8 ms: the air 3.5 ms, each band 1.2 ms. At 25 Hz that is about 20 % of one core, on a helper thread, only while the wash lands at 3 m/s or more;
- 0 B per tick and per sample (AllocProbe).

**A dense field texture needs the GPU.** A texture of plants around the camera cannot be sampled at 10–20 µs a texel. The closed form ports to a compute shader as it stands:
- per mode: κ, the amplitude at the texel's height, the phase κ·p + φ − κ·X(t) and the rate Ω = κ·V(t);
- per texel: the plant's K, I and c from a coarse CPU texture of the mean lean and the means, sampled at low rate (the mean is smooth over metres).

1.3 owns that port. `PlantAir.Sample` is its reference.

#### 7.2 For 1.5 (the drone feeling the plants)

1.5 can use these pieces:
- **For the drag and the stems' state:** `PlantLaw.Uniform`, `PlantLaw.Profile` (the forces and damping at a lean and rate), `PlantLaw.Step` and `PlantMechanics`.
- **For a crown's air:** `PlantAir.Expect`, the Gaussian expectation of a drag law.
- **What 1.5 must add itself:** the contact (a stem pushed by a leg as a cantilever, 3EI·δ/L³, the note §6.2) and the logging, which belong in the flight step.

#### 7.3 The one change on the flight's side

- **The field:** `PoseFrame.RotorThrust` carries each rotor's mean thrust over the step's sub-steps (`FlightCore._rotorThrust`, already computed) to the drawn side.
- **Its cost:** one 32-byte copy into the published pose a step.
- **Nothing reads it in a flight,** and nothing in a flight's result depends on it.

### 8. Randomness, logging, determinism

- **No draw.** A stand's natural way comes from the node's world-cell hash (world-derived, as PropStems'). Leaf phases come from the drawn instance's own hash.
- **Nothing is logged:** this is the drawn side's state. 1.5's forces will be logged.
- **Deterministic.**
  - The same flight gives the same bends bit for bit (`ThePlantsAreDeterministicAndAllocateNothing`, `TheWashIsDeterministicAllocatesNothingAndItsCostIsKnown`).
  - The wash grid reads each tick's pose by its own step, so a replay drives it the same way.
  - A render that falls more than 128 steps behind resumes at the newest tick.

### 9. The tests

| Test | Checks |
|---|---|
| `PlantAirTests.TheClassesAreTheHandCalculations` | k, I, f and ζ_a per class against plants_air.py |
| `PlantAirTests.GrassIsOverdampedByTheAirAndTwigsRing` | the regimes |
| `PlantAirTests.AGustBendsAndTheyReturnAsTheHandCalculation` | the 1 s gusts against RK4 (§1.4) |
| `PlantAirTests.TheExpectationIsGaussian` | Borgman's closed form and Gauss–Hermite |
| `PlantAirTests.ThePlantsReadTheFlightsField` | the field's reading against `WindField.At` over the farmland |
| `PlantAirTests.TheClosedFormIsTheLinksOwnResponse` | the closed form against the time-domain link in the field |
| `PlantAirTests.ThePlantsAreDeterministicAndAllocateNothing` | bit-identical, 0 B |
| `PlantWashTests.AHoverBendsTheGrassAsTheHandCalculation` | §4.3 by radius and height |
| `PlantWashTests.AHoverFlattensItsPatchAndLeansItsRing` | the patch and ring on the grids |
| `PlantWashTests.DustRisesAsTheDroneComesLower` | §5's onset per surface, dry and wet; covered and saturated none |
| `PlantWashTests.TheWashIsDeterministicAllocatesNothingAndItsCostIsKnown` | bit-identical grids, 0 B, the tick's cost |

### 10. Still not met, and for whom

- **Clip H's flutter within about 1 m at a creep:** the wash's near field (§4.3). For w9-village-physics 2.14.
- **Damp ground on a dry day** (clip H's soil) is not a state the sim has: its moisture follows the rain alone (§5).
- **Supply depletion on films:** short whiffs (§5). For 1.4 to draw, or a later law.
- **Leaf flutter's amplitude:** no published law was read (note §7). It is assumed nowhere: 1.3 needs one.
- **A tree's sway amplitude:** derived from the drawn trunk, unchecked.
- **The window's truncation** behind a fast low pass (§4.4).
- **PropStems' dynamics** against this law's (§6). For a later model number.
- **The dense drawn field needs a GPU port** of the closed form (§7.1). For 1.3.

## 1.3 The drawn plants in the air (tech artist)

The friend's grass, foliage and wood move by 1.2's law in the flight's own air, and by nothing else. The sim core's results do not move: 1.3 reads the law, the wind field, the published poses and the world. `PlantAir.Field.cs` exposes the law's halves and the modes' tables (`PlantAir.Mean`, `PlantAir.Bend`, `PlantAir.Settle`, `OpenModes`, `StripModes`, `WakeModes`, `FrameMean`, `FrameDrift`); `PlantAir.Sample` hashes to the same bits before and after the split.

### 1. How the drawn plants are driven

- **The drawing (`FarmlandPlantDrawing`).** Whenever the FarmLand scene loads, every material of the friend's three plant shaders gets a runtime copy on the project's own shader for it: `Fpv450/Plant Grass`, `Plant Leaves`, `Plant Bark` (`unity/Assets/_Project/Shaders/`).
  - They are the friend's lighting as he wrote it, with his wind taken out. His files and the store stay as they are (D-089); the replacement is the project's own code.
  - The copies reach the terrain's grass and trees through prototype copies on a runtime copy of each tile's terrain data (D-095). This supersedes `FarmlandTreeWood`.
  - Each copy is told what it draws: a clump's culms (green, dry, burnt) and its mesh's top; a tree's or a bush's sway band, its drawn height, its twigs and branches.
  - Wood and impostor cards move by the whole plant's sway alone (the pilot: the leaves breathe, "but not the pillars").
  - `-plants 0` leaves the friend's shaders and wind (the frame rate's before).
- **The field (`PlantField`, `FarmlandPlants`).** In a flight on the farmland, each drawn frame computes every band's bend at the frame's flight time (the PAL field's reference instant in the FPV view, `FlightScene.DrawnStep`) on five levels. The shaders read them (`PlantBend.hlsl`). Without a flight (noclip, the stills) no level is bound and the plants stand as drawn.
- **The drawn bend (derived).**
  - A culm, a trunk or a bush turns about its base as a uniformly loaded cantilever. Each point keeps its distance from the base and turns by the chord's angle at its height: φ·(s³ − 4s² + 6s)/3 of the link's lean φ, s the share of the plant's height.
  - A crown's foliage and twigs also ride their branch, read where the leaf hangs: its deflection (s⁴ − 4s³ + 6s²)/3 along 1.2's 3.5 m from the trunk.
  - They also ride their twig: its 0.5 m tip's deflection.
  - A clump takes its class's bands interpolated in the log of its drawn length (the mesh's top at the instance's scale).
  - Nothing bends through the ground (π/2).
- **The leaves' flutter** is the twig band's own mode (6.2 Hz with its weight, inside the cherry leaf's published 6 ± 1 Hz), driven by the field's eddies at each leaf's place. No separate flutter amplitude is assumed (§10's gap stays open).

### 2. The bands (`PlantBands`)

| Id | Element | Mechanics | Base over the ground | Labels |
|---|---|---|---|---|
| 0–2 | green culms 0.5, 0.8, 1.3 m | `PlantMechanics.Stem`, grass_blade | 0 | the lengths measured from the drawn clumps (0.46–1.34 m) |
| 3–5 | dry culms 0.6, 1.0, 1.7 m | `Stem`, dry_culm | 0 | measured (0.59–1.77 m) |
| 6–8 | burnt culms 0.6, 1.0, 1.7 m | `Stem`, burnt_culm | 0 | measured (0.53–1.77 m) |
| 9 | a crown's leafy twig | 1.2's 5 mm × 0.5 m, 20 g/m | 6 m | the elevation assumed (the drawn crowns span 2–13 m; the profile changes under 10 % from 4 to 8 m) |
| 10 | a bush's leafy twig | the same | 1 m | assumed (the drawn bushes are 1.6–5 m) |
| 11 | a crown's branch | 1.2's 40 mm × 3.5 m, 1.2 kg/m | 4 m | assumed (the island tree's crown from about 4 m) |
| 12 | the island tree | `Tree`, broadleaf 12.9 m, dbh 1.1 m, crown 12.8 × 8.9 m | 0 | measured from the drawn tree; f₀ 1.2's assumed 0.45 Hz |
| 13–15 | small, mid and large pines | `Tree`, pine 6.5 / 11.7 / 17.5 m, dbh 0.11 / 0.27 / 0.55 m | 0 | heights and widths measured from the drawn bounds; dbh derived from the note's f₀; the crown 0.6 of the height, assumed |
| 16 | a leafy bush's stems | `Woody`, 2.5 m × 15 mm, 1.2 kg/m × (d/40 mm)² of leaves | 0 | measured (GreenBush 2.0 m, LeafyBush_P2 3.2 m, twigs 9–18 mm); the leaves derived (1.2's two loads both lie on d², the pipe model's) |
| 17 | a bare bush's stems | `Woody`, 3.0 m × 20 mm, no leaves | 0 | measured (DeadBush 3.0–5.0 m, twigs 10–44 mm) |

### 3. The GPU port (`PlantField.compute`)

Respond's two halves, split where §7.1 says:
- **On the CPU, at a low rate:** `PlantAir.Mean` (the mean air at the load point, the fluctuation's covariance, the mean lean by Settle) on four grids round the camera, on a helper thread. The grids (numerical):
  - the grass near the camera, 26 × 2 m, a snapshot every 4 s of flight time;
  - the grass out to the mid level, 24 × 8 m, every 8 s;
  - the crowns' twigs and branch, 24 × 8 m, every 4 s;
  - the far level's whole plants and branch, 12 × 48 m, every 8 s.

  Each snapshot is computed ahead of the frame and centred where the camera will be. The near grass grid is 2 m because the tallest culms barely stand (W/k 0.93 for the 1.3 m green culm), so their mean lean follows a wake's mean within metres.
- **The wake eddies, from the wake grid itself.** Their phase is anchored at the world's origin and their wave number is their scale over the obstacle's height, so an interpolated height would scramble them (a 1 % error turns the phase by about 10 rad 500 m out).
  - The grids near the camera carry the wake grid's own cells (`WakeGrid.SampleCell`: the obstacle height and the turbulence at each band's load height).
  - The kernel takes the eddies' share √(T² − 1)·σ_u·taper and their scale at the point itself, as `WakeGrid.Sample` and `WindField.PlantOpen` do.
  - The far grid takes the nearest node's.
- **On the GPU, every drawn frame:**
  - The mean is interpolated between a grid's four nodes and its two snapshots about the frame's time.
  - Where no wash grid integrates the plant, the wash's steady air at its load point joins the mean and the lean is settled again in float (`PlantAir.Sample`'s quasi-static path, `PlantAir.Settle`, §4.5).
  - Then `PlantAir.Bend`'s arithmetic in float: the gusts are linearised about the whole lean (the wind's mean lean plus the wash grid's bend) in the mean air plus the wash's air, with Borgman's closed form for a bare stem and 12-point Gauss–Hermite with foliage.
  - Each mode's steady response F(P cos θ − cΩ sin θ)/(P² + c²Ω²) is summed: the 138 open modes at the band's height, a strip's eddies, and the 24 wake eddies at the place's obstacle scale.
  - The phases are reduced on the CPU in double about an origin by the camera. The tangent's difference is 10⁻³ rad (PlantAir's 10⁻⁴ in double loses digits in float; the truncation at 10⁻³ is about 10⁻⁶ of it).
- **The wash (`PlantWash`).** A fine (24 × 5 cm) and a coarse (32 × 15 cm) grid integrate each class's middle culm (bands 1, 4, 7), ticked at 25 Hz from the published poses on a second helper thread.
  - Each tick's stands go to the GPU, whose wash levels' texels are the nodes: the bend now and at the tick before, and the wash's air at the load point.
  - The other culms, and every plant beyond the grids, take the line's law as `PlantAir.Sample` does for a band no grid holds: `RotorAir.At` from the fine grid's own instance. It is sampled each tick on a polar grid round the drone at the first twelve bands' load heights: 33 rings from 0.1 m, each 1.2 times the last, to 33 m, and 16 angles.
  - A plant takes it where it passes 0.005 m/s for a culm (under which its lean moves under 0.01 rad) or 0.3 m/s for a twig or a branch (under which a twig's moves under 10⁻⁴ rad, derived from 1.2's twig).
  - Nine integrated culms took the helpers past a whole core in the Mono player; three keep each class's dynamics (the spring back) and leave the shortest and the tallest quasi-static under the drone.
- **The levels** (texel centres on whole cells, so a grid's nodes are texels):

  | Level | Texels × cell | Round | Bands |
  |---|---|---|---|
  | fine wash | 24 × 5 cm | the drone | grass |
  | coarse wash | 32 × 15 cm | the drone | grass |
  | near | 96 × 0.5 m | the camera | grass, twigs |
  | mid | 96 × 2 m | the camera | grass, twigs, branch |
  | far | 64 × 8 m | the camera | branch, the whole plants |

  A point takes the finest level holding its band, faded into the next over the level's last four texels.

### 4. The check against the CPU (`PlantFieldGpuTests`, EditMode with a GPU)

Over the farmland in Windy, a hover 1 m over the fields south of the village for 8 s (published poses, the wash grids ticked from them). The field is drawn frame by frame; at 8.0 and 8.52 s every level and band is read back:

| Check | Texels | Result |
|---|---|---|
| The port: each texel against `PlantAir.Bend` on the mean the kernel interpolates there, with the wash grid's node and the steady air as the kernel takes them, settled again by `PlantAir.Settle` | 6176 | within 1.5·10⁻³ rad (float against double; the float Settle's last bits) |
| A plant on a grid's node at a snapshot's time, out of the wash, against `PlantAir.Wind` | 1225 | within 6.7·10⁻⁵ rad |
| A plant within the steady air's 33 m against `PlantAir.Sample` | 1900 | median 2.2·10⁻⁴, 95th percentile 3.9·10⁻³, worst 0.051 rad |
| A plant on the wash's levels against `PlantAir.Sample` | 776 | median 2.3·10⁻⁴, 95th percentile 0.026, worst 1.54 rad: a culm the grids do not integrate, at the near columns, where the polar grid's 16 angles do not resolve an 18 cm column (§7) |
| Every plant round the camera against `PlantAir.Sample` | 5366 | median 5.5·10⁻⁴, 95th percentile 0.018, worst 0.51 rad: the 1.3 m green culm 100 m out, whose mean lean, interpolated over 8 m, differs from its own (§7) |

- `PlantsLeaveTheFlightTests`: the same headless flight over the farmland in Windy (seed, keys, clock), alone and with the plants' CPU side reading it on two threads all the while (the wash grids from its poses, the wind's mean round the drone). The two logs are the same byte for byte.
- `FarmlandPlantDrawingTests`: every plant material the terrain draws is on the project's shaders with the friend's textures and settings, and nothing draws his wind. His assets and the store's terrain data are untouched, and the grass keeps every instance he painted.

### 5. Cost

- **The helpers' CPU,** measured in the EditMode suite (Mono, the editor):
  - the wind's mean costs 94 ms a second of flight;
  - the first player run (Mono, on battery) logged 1.4 s a second of flight and 190 ms a wash tick before the cuts above (nine integrated culms and denser means).

  That run's flight stepped badly: p99 4 ms, 1758 of 20422 steps over 1 ms. Whether the plants' helpers caused it needs the A/B on AC.
- **The frame rate through the feed** (PLT-02, PLT-07): `-plantbench` flies Village from the village's start (PlantPilot on the development keyboard). It holds 2 m at the start, hovers 4, 2, 1 and 0.5 m over the fields' grass, makes a low pass 1 m up and holds 4 m before a tree belt, timing each held leg. The A/B is that bench with and without `-plants 0`. It waits for AC power: the dev machine has been on battery since the first run (27 % down to 15 %), where no figure counts (PLT-07).

### 6. Clip H

The captures (the hovers through the feed and from behind, the pass, the belt in Windy) are the bench's shots, compared by letter with clip H's 17.2–19.6 s (§1.5's target: standing stalks within about 1 m fluttering about a held lean, the mats down). They wait with the bench.

### 7. Left out, and for whom

- **The wash's steady air beyond 33 m** (the polar grid's reach): 1.2's law still leans the barely standing 1.3 m green culm there (0.22 rad at 45 m from a 1 m hover in the suite, under 0.1 m/s). The other culms move under 0.01 rad.
- **The columns at the polar grid's resolution:** a culm the wash grids do not integrate takes the steady air from 16 angles, which blurs the 18 cm near columns (worst 1.54 rad against `Sample` at a column's edge). The next step (uncommitted): the wash grids' own node samples at each band's height (a read-only `PlantWash.AirAt`).
- **The softest culm's mean lean** between the 8 m nodes (worst 0.51 rad 100 m out). Settling every grass texel's lean on the GPU from the interpolated air removes it, at about 0.2 ms of GPU a frame; not taken until the frame rate's A/B says there is room.
- **The crowns in the wash:** a twig or a branch takes the wash quasi-statically (1.2's §4.5) on its band's fixed elevation (6 m, 4 m, 1 m), so a crown's leaves above or below that height take the wash there.
- **Leaf flutter's own amplitude:** not assumed (the twig's mode carries the flutter); a law is still not read (§10).
