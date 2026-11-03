# Design: p7c-wind

## Context
- `p6-flight-core` 1.5 flies a stand-in: a mean wind per level (Calm 0.8, Windy 2.5, Severe 5.0 m/s), a first-order Gauss–Markov gust per world axis (σ 0.135·U), per-flight direction and strength draws, logged. Drag acts on the body, the coil and each rotor (H-force), with the wind taken off each rotor's inflow.
- The 1.5 calibration flies clip P's 22.7° pitch at 13 m/s. PHY-17a asks for the published 17–22 m/s cruise instead (the pilot flies wind at normal cruise). The two readings differ in drag by about 1.7–2.9×.
- The wind targets (K§8.6) are closed-loop: they are graded with K§8.5's simulated pilot, never on the open-loop gust response.

## Goals / Non-Goals
**Goals:** the field PHY-13 describes, the K§8.6 targets met under the simulated pilot, every target failing a known-wrong model, determinism and the budget kept.
**Non-Goals:** the proposal's non-goals.

## Decisions
1. **Tools first** (D-037). The three Blender scripts are ported to plain Python before any tuning, and their self-checks must pass first: a model is never tuned against a tool that would pass noise.
2. **The harness is the acceptance.** K§8.5's pilot, test flight and measurement exactly, at least five seeds, the first 5 s discarded; its variants (delay, τ_v, the split s) for T11b. A target passes only when it also fails its known-wrong model (as 1.5's wind-field tests do). D-060 adds three things to it: the split per stretch from P's measured roll shares (K§8.4), the pilot's stick noise from the optimal-control model's motor noise (the noiseless pilot its known-wrong), and the recorder's field pick in the logged frames; T6d is graded against P's real motion above 4 Hz, not its tracker floor.
3. **The field's layers** (K§6.7), sampled at each rotor, the body and the coil:
   - the mean profile, a log law with d from the cover height (assumed, labelled) and z0 from the landscape's roughness class (D-060: Davenport–Wieringa class 4, 0.10 m, published)
   - von Kármán turbulence with σ/U (derived from z0: the surface layer's σ_u = 2.4·u*, 0.164 at 35 m), L_u, L_v and L_w per level, energy reaching 2–4 Hz at the encounter frequency and the discs' 0.5 m (D-060), generated as a deterministic, seeded field (never noise on the pose)
   - the wakes from the wake grid (decision 4)
   - the canopy flow of wooded strips (p7c-wind 1.5, belt-geometry-p.md: clip P's belt is a wide strip with the wind along it, crossed at 0.45–1.0 H inside its canopy layer): Raupach (1994)'s displacement, roughness and u*/U(h) from the strip's height and frontal area index, the exponential in-canopy profile, the roughness sublayer and the log law to a blending height, the canopy-top σ/u* with eddies of the canopy's scale convected at 1.8·U(h); K§8.5's belt stretch is flown through it at 0.5–1.0 H
   - vorticity and per-rotor gradients (PR-3), so a gust front crossing the drone gives coupled pitch-and-yaw events
4. **The wake grid** (world-builder): per wind direction on the 2 m grid, marching upwind up to 30 H (capped at 300 m) to the nearest obstacle; each obstacle's height, base and path porosity from the placed assets' `wind_volumes` and porosity (the stand-ins carry Godot's). Porous-fence behaviour for β > 0.3, bluff-body recirculation for β < 0.1, the speed-up over the top, a jet under crowns with a base, jets through gaps. Recomputed on a worker when the direction moves more than 5°; the step only samples it.
5. **Direction** (K§8.3): the prevailing direction drawn per flight, an Ornstein–Uhlenbeck meander (τ 2 min, σ 30°, smoothed by a 30 s lag), rare shifts (45–120°, ramping over 10–30 s, Poisson about 1 per 5 min) relaxing back. Every draw named, bounded and logged.
6. **Cruise and drag** (PHY-17a): the drag areas and the H-force are re-derived so clip P's pitch at about 4.8 kg gives 17–22 m/s. The crosswind lean per m/s then follows from the physics at that airspeed and is checked against T0 (the graded target), not against K§6.5's 13 m/s band. The physics engineer computes it; nothing is asked (D-024).
7. **Rain** (PHY-17, D-021): the rain rate per level is independent of the wind level; its drag and wet mass act on the body; the air velocity at the camera, already a feed signal, carries the rain for FEED-13.
8. **The budget:** the field's cost per step within PLT-04's share; the wake grid's precompute off the step.
9. **The map's wind** (D-059, D-060; `WindSetup.ForMap`, `MapWind`): a flight over a map carries the map's own obstacles, and its tree belt is counted once.
   - **The obstacles:** every placed object's `wind_volumes` with its porosity (houses, cars, fences, poles, single trees) go to the wake grid as placed (a volume tilted off the vertical as the upright box bounding it), over the map's extent and its 2 m cells. Derived from the world, never drawn.
   - **The belt:** the rows of the placement group `posadka`, split into sections where a clear stretch of their axis is wider than a 2 m cell (the field track's gap stays open). Each section is read twice, never as its plants:
     - **as a wooded strip** for the canopy flow (decision 3): its footprint's extent along and across its principal axis; λ = Σ(1 − β)·D·(top − base) / plan area, each plant counted by the light it stops (derived); h the frontal-area-weighted mean top (assumed: the tall trees carry the drag)
     - **as one porous obstacle** for its lee: an upright box over the strip, top h, its path porosity across the belt the aerodynamic α = β_belt^0.4 (recalled: Guan et al. 2003, p7c-wind 1.5's porosity rule), with β_belt = e^(−λ·W/h) the optical porosity across its width (derived: Beer–Lambert over crowns of frontal density λ/h). The plants' own volumes never enter the wake grid, so the product rule's 0.003–0.02 (1.3's open question) never reaches the belt's wake.
   - **The rule at a point:** inside a strip and below its blending height, the strip's plan weight w (0 at its edge, 1 a canopy height inside) is the canopy's share and 1 − w the wake grid's: the mean is U·(w·U_c(z)/U + (1 − w)·P(z)·s), P the open profile and s the wake grid's speed; the open modes fall with the canopy's σ as before; the canopy's eddies carry √w of their excess and the wake eddies √(1 − w) of theirs. Outside every strip, or above its blending height, the wake grid alone. Behind the belt the wake grid's nearest-obstacle rule makes the belt's porous wake the lee's, and an obstacle upwind of the belt never adds to the canopy.
   - **The strips share** the stream `wind.canopy` (counter-based, each at its own scales); the log header holds the layout and every strip (format 14).
   - **Not modelled:** the canopy's adjustment where the wind enters a narrow belt (the plan weight's edge ramp stands for it: the test range's 17–18 m belt never passes w ≈ 0.94), the slow-down upwind of the belt, and an obstacle's wake under the belt's.
10. **The wind inside a cut** (D-071; PHY-13, WLD-16; QA's S5 in `review-integration.md`). Model 20 restarts the open profile at a cut's floor. So in the map's Severe wind E01's run blows 0.47–1.20 of the open field at the same height over its floor (QA, seeds 1–3), and a body crossing its edge sees the hubs' mean in Calm go from about 0.2 to 0.6 m/s as the ground under it drops to the floor (QA called it one step; it follows the walls' slope at 1 mm a step, at most 0.007 m/s a step in Calm and 0.047 in Severe). The cut's own flow replaces that rule.
   - **The cuts on the test range** (`maps/test-range/placements.json`'s `ground.holes`, the assets' metadata and params; measured off the files):
     - **E01's run:** 24.0 m of centre line in legs of about 5 m with 35° bends (heading −23° to +19°), 1.5 m deep, a spade top of 0.8 m and a floor of 0.6 m (`top_w_m`, `bottom_w_m`), 0.90–0.98 m across at the crumbled lip (its slot gaps; `lip_crumble_m` 0.1). Parapets of 0.45 m (field side) and 0.3 m (belt side) stand 0.3 m back from the lip. H/W is 1.5–1.9 at the lip and 2.3 at the floor.
     - **Its two niches:** cubbies 0.8 × 0.6 × 0.7 m at the floor, roofed by the fillers (records 1.35 m deep, 1.1 m of centre line).
     - **Z04's cellar shaft** (stand-in): a 0.9 × 2.5 m opening (its walls' inner faces) over a stair from 0.3 to 1.8 m deep, 2.0 m of centre line.
     - **S04's patches** are ground patches, not cuts: model 20's soil under the straw stays.
   - **What the measurements give:**
     - **Regimes,** by a driving component's streamwise length L over the depth D. Hotta, Hatanaka, Tanaka and Horikawa (1998, Proc. 26th ICCE, pp. 2533–2547; read) put trenches in a wind tunnel: H 0.2 m, B/H 2–10 and a step, a log-law layer 1.5 H deep over the lip, Re 0.7–2 × 10⁵. Their results (measured):
       - L/D < 5: one circulation cell (no reattachment at 4, unclear at 5).
       - L/D ≥ 6: the shear layer from the upwind lip reattaches on the floor, at 5.2 D (L/D 6), 5.8 D (8), 6.8 D (10) and 7.2 D behind a step (the literature's 7.0–7.5 D). The reattached flow runs onto the downwind wall, its return flow "violent".
       - Thin boundary layers put the open–transitional bound at L/h 6–8, and closed flow at about 13 (Plentovich and Stallings 1993, NASA TP-3358; abstract only). Roshko (1955) saw reattachment on the floor below d/b ≈ 0.1. A trench's thick layer closes earlier, so Hotta's bounds are the field's.
     - **The cell:** a square cavity holds one stable vortex, in solid-body rotation, separated from the walls by thin friction layers. Its maxima are u/U∞ 0.27 by the upstream wall, 0.41 by the downstream wall and 0.30 over the floor (Roshko 1955, NACA TN 3488; read). The single vortex first forms at d/b 0.87. From d/b 1.5 a main vortex fills the upper part over a weaker small-scale structure, weaker as d/b grows. The flow switches intermittently at d/b 0.5–0.87 and above 2.
     - **Deep cuts at full scale:** reduced-scale canyons of H/W ≥ 1.5 show two stacked counter-rotating vortices (Baik et al. 2000, water channel). Full-scale ones show one: the critical Reynolds number is 57–87 × 10³ at H/W 2 (Chew, Aliabadi and Norford 2018, Environ. Fluid Mech. 18; abstract only). E01's U_o·D/ν is 2.9 × 10⁵ in Severe and 1.5 × 10⁵ in Windy (derived below), so one vortex.
     - **Turbulence:** inside canyons of W/H 1–2 the streamwise variance is nearly an order of magnitude below the flow's above them (Di Bernardino, Monti, Leuzzi and Querzoli 2015, BLM 155, water channel; read).
     - **Oblique wind:** the along-street flux scales with the external wind's component along the street, for most incidences, with spiral streamlines (Soulhac, Perkins and Salizzoni 2008, BLM 126; abstract only). The cross and along components each drive their own flow.
     - **Not the canyon regimes:** Oke (1987/1988)'s skimming (H/W > 0.7), wake interference (0.3–0.7) and isolated roughness (< 0.3) describe arrays of buildings. A cut in open ground is a cavity in a wall, so Hotta's regimes apply.
   - **The opening is the world's own** (per step, at the body's origin). The origin lies in a cut's opening where it is within the cut's reach and the ground under it lies below that cut's lip there. The ground is the one the field already reads: `SampleGround`'s height, and over a hole the soil under the body (the core's `SoilUnder`, model 20).
     - The reach is W/2 + 0.1 m of the centre line, which runs on past its end points by the cut's ends (`CutFrame.OpeningReach`, assumed: E01's `lip_crumble_m`). It only bounds the search. The test range's own opening reaches 0.049 m past E01's W/2 at most (0.520 m from its line, read every 1 cm), its crumbled lip a few centimetres off the centre line, and Z04's box walls exactly W/2.
     - So the edge falls where the world's ground meets the lip. Inside, the field takes the lip as its ground; outside, the world's ground is the lip. The field's ground is continuous there, with no smoothing band.
     - A strip within W/2 of the centre line (the mean lip width) misses the world's opening by up to 5 cm. Where the strip ends, the ground under the body is already the wall's slope (down to −0.38 m), and the field steps. That strip is the edge scenario's known-wrong.
     - Where no soil lies under the origin (−∞, inside a shape), no opening holds it.
   - **The frame** is the nearest centre-line segment of such a cut, an open cut before a covered one. It gives:
     - the axis t̂ and the across direction n̂
     - the lip height y_lip (the centre line's height there)
     - the opening's width W at the lip
     - the origin's arc length s, and the cut's length S (centre line plus its ends)
     - the depth D: the cut's own, its floor at the centre line under the lip (the record's `depth_m`), never the soil under the origin. A rotor over a battered or crumbled wall then reads the cell, not the still air below the wall's local depth.

     Every point of the step uses this frame (assumed: the hubs lie within 0.4 m of the origin, and E01's bends have a 3.7 m radius). Only a pilot's level has it: Still and Steady are uniform air and never read the ground.
   - **The drive:** Ū_o, the field's own mean at (x₀, y_lip + h_o, z₀), horizontal, with its wakes and canopy share, at h_o = min(W, 1.5·D).
     - h_o is derived from the two experiments' references: Roshko's free stream stood above a floor layer thinner than the breadth, and Hotta's log layer ended 1.5 H over the lip. Using the field's own mean at that height is assumed: in the field's thick layer, the cell answers to the flow at its own scale above the lip.
     - A slit narrower than the field's still cover layer (d + z0, 0.3 m) has no drive.
     - Its components are U_⊥ = Ū_o·n̂ and U_∥ = Ū_o·t̂ (Soulhac et al.).
   - **One 2-D cavity per component:** C(L, D, ξ, η, U) gives u along the component and w upward, at ξ from the upwind wall (by the component's sign) and η = y_lip − y below the lip.
     - **Across:** L = W, ξ = W/2 + n·sign(U_⊥), n the point's offset along n̂.
     - **Along:** L = S, ξ = s or S − s, times e^(−πη/W). This is the side walls' hold on the along flow (derived: Laplace's first harmonic of a shear-driven flow in a slot of width W with a uniform eddy viscosity falls by e^−π per width of depth). It is 1 for W ≫ D.
     - **Open, L/D < 5** (Hotta): one cell.
       - ψ = A·(D/π)·sin(πξ/L)·sin(πη/D)·e^(−a·η), u = ∂ψ/∂η, w = ∂ψ/∂ξ (divergence-free).
       - A = c_v·U, with c_v 0.33 (published: the mean of Roshko's three maxima).
       - a = π·max(0, D − L)/(L·D) (derived: Roshko's square cell on top, and below it the one full-scale vortex (Chew et al.) falling by the slot's e^−π per width, e^(−aD) = e^(−π(D − L)/L)).
       - The top moves with the drive, down the downwind wall, back along the floor and up the upwind wall. In E01 across, the flow turns back 0.41 m below the lip, its return peaks at −0.22 A 0.81 m below it, and it reads −0.06 A on the floor.
     - **Closed, L/D ≥ 6:**
       - ξ < X_R is a bubble: the same cell with L = X_R and a = 0. X_R/D is 5.2, 5.8 and 6.8 at L/D 6, 8 and 10 (measured, Hotta, linear between), and 7.2 − 4/(L/D) beyond 10 (derived: through 6.8 at 10 to the step's 7.2). X_R never exceeds L.
       - ξ ≥ X_R takes the floor's own profile: u = U·P(D − η)/P(D + h_o), P the field's profile shape (its d and z0) over the floor, so it meets the drive at h_o over the lip.
       - It grows in over one more bubble length, × min(1, (ξ − X_R)/X_R) (assumed: the step literature's recovery takes tens of heights; it is kept short so a long cut's floor reads the open profile model 20 gives).
       - Within D of the downwind wall it is × (L − ξ)/D (assumed: a wall's stagnation spans about its own height; Hotta's L/D 6 has the wall 0.8 D past reattachment).
       - w comes from continuity (∫P has a closed form).
     - **Transitional, 5 ≤ L/D < 6:** (1 − f)·open + f·closed, f = L/D − 5 (derived from Hotta's bounds). The closed share holds X_R at L/D 6's 5.2 D, never past L (assumed: Hotta measured no reattachment below 6, and 5.2 D is the first measured point).
   - **The mean at P:** Λ·(u_⊥·n̂ + u_∥·t̂ + (w_⊥ + w_∥)·ŷ) + (1 − Λ)·(the field at the lip).
     - Λ = min(1, η/δ_s), δ_s = W/7.2 (derived: the separated layer's spread, one depth in Hotta's 7.2).
     - The field at the lip is still: the cover's d + z0 is the field's own layer. The cut is continuous with it.
   - **The turbulence at P:** the field's own fluctuation at P × τ·Λ. The open modes, the wake eddies and a canopy's eddies each take their taper, boost and share at the reference height h_o, and are evaluated at P itself, so the per-rotor gradients stay.
     - In an open cell τ = τ_in·e^(−a·η), τ_in = √0.1 = 0.32 (published: Di Bernardino et al.'s order of magnitude in variance; assumed for v and w and for the fall with the cell).
     - In a closed cut's bubble and after reattachment τ = 1 (assumed: the reattaching layer brings the outer eddies to the floor; Hotta's "violent" return flow).
     - In the transitional band τ blends as the mean does, and the along component's τ also takes e^(−πη/W).
     - At P, τ² = (U_⊥²·τ_⊥² + U_∥²·τ_∥²)/(U_⊥² + U_∥²) (assumed: each component's share is its drive's energy).
   - **Above the opening** (y ≥ y_lip) the field takes the lip as its ground (the profile, the wakes and the canopy), never the floor: the drone over the opening flies the field over the plane. Below the lip outside the opening (in the walls) the field is 0, as below any ground.
   - **Covered cuts** (the niches): below their roof the air is still (assumed: a 0.6 m mouth onto a cell whose own floor flow is 0.06 A). Where an open cut and a covered one both hold a point, the open one takes it.
   - **Quasi-steady:** the cell follows the mean within the step (derived: its turnover π·L/A is about 2.6 s in Severe, against the meander's 2 min). The cavity adds no draw: its randomness is the field's.
   - **First readings** (derived from the model at Severe's centre of 9.5 m/s, without wakes; P over z0 0.10 m and d 0.20 m; `CavityTests`):
     - **At W 0.8 m** (the spade's top width, the spec's synthetic section, 1.5 m deep):
       - E01's U_o is 2.9 m/s at h_o 0.8 m, so A is 0.96 m/s across.
       - With the wind across, at 1.0 and 0.5 m over the floor in the opening's middle: 0.10 and 0.19 m/s. The plane gives 3.37 and 1.78 m/s at those heights, so 0.03 and 0.11 of it.
       - At any bearing and anywhere in the opening, as the formulas give it: at most 0.278 m/s at 1.0 m and 0.19 m/s at 0.5 m (0.083 and 0.11 of the plane). 0.1 m below the lip, up to 1.441 m/s with the wind along the run. The first draft printed 0.24 and 1.2 m/s, which the formulas do not give. The spec's bound of 0.1 of the plane at 1.0 m holds.
     - **At the map's W 0.9425 m** (E01's lip width, 1.10a's record; what the map flies): h_o is W, U_o 3.25 m/s and A 1.07 m/s across. In the opening's middle, 0.007 m/s at 1.0 m over the floor (the cell turns there) and 0.31 m/s at 0.5 m, the return 0.29 A (0.20 A at 0.8 m); 0.002 and 0.18 of the plane.
     - Model 26 reads 0.47–1.20 (QA).
     - Across cuts 1.0 m deep, the flow 0.4 m over the floor stays reversed up to L/D 5.5, and turns forward at 5.2, 5.8, 6.8 and 7.0 D for L/D 6, 8, 10 and 20.
   - **What the world gives** (1.10a): per hole record, held by `MapWorld` beside its wind volumes and read by `MapWind` into `WindSetup.Cuts`:
     - the centre line with its lip height at each point (the placement file's y)
     - the opening's width at the lip, and the depth
     - the opening's ends past the first and last points
     - whether fillers roof it

     There is no per-direction precompute and no march on the 0.5 m `holes` layer. The regimes use the cut's own W, S and D, and the wind enters as two components, so nothing is rebuilt when the direction moves.
   - **Not a march along the wind:** one 2-D cavity on the wind's line, L = W/sin θ, calls a 0.8 m slot closed within a few degrees of its axis and reads the floor's open profile there. The slot's walls hold its along flow (the decay above; Soulhac et al.). That model is the oblique scenario's known-wrong.
   - **Cost:** only over a cut.
     - Per step: the cut lookup (a bounding box per cut, then at most E01's 49 segments) and one more mean at the reference (one `DetMath.Log`, one wake-grid sample, the strips' weights).
     - Per point in a cut: two `SinCos`, three `Exp` and at most one `Log` in place of the profile's `Log`; the modes as before.
     - About 5 µs a step at most (estimated); elsewhere one pass over the boxes.
     - `DetMath` only, no FMA, and 0 B in steady state: the cuts and their boxes are built at setup. The frame lives in the step's frame (`Begin`), not in `FlightState`, so replay recomputes it.
   - **Logged:** the header lists each cut (`LogFormat.Version` + 1), and `ModelVersion` goes up by one. The record's wind at each hub already shows the cell. A flight that never enters a cut's opening steps as in 26.
   - **Not modelled:**
     - the world's opening below the lip: the cavity's section is the W-wide slot about the centre line, so a point below the lip more than W/2 off it is in the walls (still air) even where E01's crumbled lip leaves air there; the world's opening sets only where the frame holds the body
     - the parapets' own lee over the opening (E01's mounds carry no wind volumes)
     - the cell's intermittent switching (Roshko: d/b 0.5–0.87 and above 2)
     - the two-vortex state below the critical Reynolds number (E01 in Calm, 4.4 × 10⁴, where the cut's flow stays under 0.05 m/s)
     - a bend's own separation (the along flow follows the axis)
     - the cover's still layer meeting the opening at the lip's edge
     - the drone's own wash between a cut's walls (the airframe's ground effect, `p7b-airframe`)
     - relief shelter (banks, slopes), until the final map's heightfield

## Risks / Trade-offs
- **Tuning against closed-loop targets is slow:** each target needs five seeds of 39 s flights, and T0c 40 × 120 min of history. The harness runs headless and in parallel seeds.
- **The drag re-derivation moves other numbers:** the crosswind lean and the H-force calibration, and every golden. Each re-record is audited.
- **The wake grid depends on the world queries over the test range** (`p6-world-query-gallery` 1.4). Until then the harness uses a synthetic belt of the same height and porosity.
- **The cavity's constants come from a laboratory cavity and a wind tunnel, not a field trench** (none measured was found). c_v comes from a 4-inch cavity under a thin layer, τ_in from a water channel's canyons, and h_o bridges both references to the field's thick layer. The scenarios check the model's structure and Hotta's reattachment. Each constant is a named parameter.

## Open Questions
- None for the pilot. The pilot's feel is judged at G7.
