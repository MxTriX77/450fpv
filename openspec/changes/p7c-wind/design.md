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

## Risks / Trade-offs
- **Tuning against closed-loop targets is slow:** each target needs five seeds of 39 s flights, and T0c 40 × 120 min of history. The harness runs headless and in parallel seeds.
- **The drag re-derivation moves other numbers:** the crosswind lean and the H-force calibration, and every golden. Each re-record is audited.
- **The wake grid depends on the world queries over the test range** (`p6-world-query-gallery` 1.4). Until then the harness uses a synthetic belt of the same height and porosity.

## Open Questions
- None for the pilot. The pilot's feel is judged at G7.
