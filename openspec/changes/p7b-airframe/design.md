# Design: p7b-airframe

## Context
- `p6-flight-core` has the Betaflight controller with the builder's filter chain (D-045), the DC motors on APC's curves with the 40 A ESC limit, two packs with sag, drag, the stand-in wind and contacts. Every rotor is identical and its inflow sees only the wind.
- Randomness rule: only a bounded perturbation of a named physical parameter, from a named seeded stream, logged; never noise on the pose.

## Goals / Non-Goals
**Goals:** each PHY-07…12 mechanism physical, seeded where random, logged, with a computed band and a known-wrong model that fails it; determinism and the budget kept.
**Non-Goals:** the proposal's non-goals.

## Decisions
1. **Asymmetry from tolerances** (PHY-07): per motor Kv spread, ESC timing jitter and prop imbalance, each drawn once per flight within published or researched tolerances (labelled), named streams. The band of drift and correction rate with wind off is computed from them; with asymmetry off the drone is measurably steadier; it never buzzes or diverges.
   - **As built (1.1):** `FlightSetup.Asymmetry` (`MotorAsymmetry`, from `DroneSpec.BuildAsymmetry()`; null is off, for Calm's grading). Three streams, 16 draws at the flight's start, in the header with their bounds:
     - `motor.kv`: Kv × (1 + δ), δ nearly normal, σ 2 % (assumed: no lot spread is published), clamped to ±10 % (published: maxon's constants hold to about ±10 %). Ke = Kt moves with it.
     - `esc.lag`: τ_esc + Δ, Δ uniform in ±0.08 ms (derived: the winding's L/R at ±10 %, ±30 µs, and AM32's 20 kHz loop and 24 kHz PWM phase against the frame, ±46 µs, which drifts update by update below the 250 µs sub-step and is carried as one offset per flight, assumed).
     - `prop.unbalance`: U uniform in [0, U_per] at a uniform angle, U_per = m·G/Ω = 0.107 g·mm (G 6.3, published, Tyto Robotics; the 16 g prop; Ω at the props' 9000 rpm, assumed). Each rotor pulls its hub with U·ω² at its angle (`FlightState.RotorAngle`, logged).
   - **The band** is the linear steady state at the hover (`AsymmetryScenario.Predict`, 20 000 seeds' draws, 0.5–99.5 %): roll 1.5e-4–6.8e-2 °/s, yaw 1.2e-4–4.5e-2 °/s over a 30 s hands-off hover (yanks of 0.005–2.0° and 0.003–1.4°), the vibration 8.9e-5–4.2e-4 rad/s per step. The ESC lag and the unbalance make no steady moment: the Kv spread is the yank, the unbalance the never-still vibration.
2. **The per-rotor inflow seam** is shared with `p7c-wind`: one function gives each rotor its inflow (wind, own and neighbours' wash, ground and ceiling terms). Both changes write into it; this change defines it first.
   - **As built (1.1):** `Propulsion.RotorInflow(in FlightState s, Double3[] windAtHubs)`, called by `BeginStep` before the rotors' loads. Its outside input is the wind at each hub, world m/s in `Corners` order (null for still air); 7c's field plugs in where `FlightCore.Propel` fills `_hubWind`. It writes `Propulsion.Inflow` (axial, m/s, positive down through the disc) and `InPlane(i)` (body m/s). The own wash and VRS (1.2), the neighbours' wash and the ground and ceiling (1.3) are added inside it, from the state and the rotors' loads. The refactor left every golden bit for bit.
3. **Ground and ceiling effect** (PHY-08): Cheeseman–Bennett per rotor from a ray straight down the rotor axis, combined with `SampleGround`'s relief and mat top; a `Hole` switches the ground term off. The same form under a roof for the ceiling effect. Recirculation in a close hover: a bounded, physical re-ingestion of the own wash that keeps a close hover never still.
4. **Vortex ring state** (PHY-09): onset from the descent rate normalised by the induced velocity at the current thrust, so thrust against weight drives it; hysteresis between entry and exit; thrust loss and an asymmetric, wandering moment while inside. Entry and exit are logged events.
   - **As built (1.2):** `FlightSetup.VortexRing` (`VortexRingSpec`, from `DroneSpec.BuildVortexRing()`; null is off), per rotor inside `Propulsion.RotorInflow`, its state moved once a step by `Propulsion.AdvanceRing`:
     - **The scale:** each rotor's v_h = √(T/2ρA) at its own thrust then (momentum theory); η = −V_a/v_h along its axis, μ = |V_∥|/v_h in its plane.
     - **In and out** (Johnson 2005, NASA/TP-2005-213477, Table 4, published): in at η ≥ 0.45 (VzN, the flight tests' stability boundary) with μ < 0.95 (VxM); out below η 0.2 (VzD, where his VRS inflow rejoins momentum theory), past η 2.0 (VzE, the windmill brake state) or at μ ≥ 0.95. Using D as the exit is assumed. Events `VortexRingEntered` and `VortexRingLeft` (12, 13) carry η, μ and v_h.
     - **The mean loss:** the ring's inflow through the disc is Johnson's measured mean, momentum theory at D, 0.85 v_h at N, 1.25 v_h at X (η 1.5), linear between (assumed), back to the baseline at E. It is taken over the props' held descent branch (J −0.05, η 0.19, inflow 0.909 v_h, derived), so it costs thrust from η 0.61 (derived; where Newman et al.'s axial onset, a wake transport velocity of 0.74 v_h matched to Drees and Hendal, published, falls on momentum theory: 0.61) to 2.0, at most 0.34 v_h at X. It enters the thrust table as an advance ratio, 2δ/((1 − r)nD), r = −s·√(2C_T/π) = 0.25 from the table's static slope (derived, linearised at hover); the torque keeps the freestream's (Washizu; Azuma and Obata: little torque change, published). Lagged by Johnson's τ = 0.7/λ_h revolutions = 4.4 R/v_h (published).
     - **The wobble:** each rotor's thrust × (1 + x), x a first-order process with the corner f·R/v_h = 0.2 (the low band of Johnson's Table 2, published) and σ rising from 0 at D to 0.10 at X and to 0 at E (Shetty and Selig 2011, AIAA 2011-1254: 10-inch APC props, ±30 % at ±3σ at the minimum thrust, published; linear between, assumed), bounded ±0.30; its unit draws from the named stream `vrs.thrust`, counter-based on the step and the corner, so the four rings wander apart. Never on the pose.
     - **Logged:** `FlightState.RingInflow`, `RingThrust`, `RingLatched` in the step record (`LogFormat.Version` 7), the spec in the header.
5. **The throttle-punch shake** (PHY-10): the frame's first bending mode and the payload and spool pendulum as small physical modes, excited by wash re-ingestion after an arrested fast descent. Its occurrence and duration (0.3–0.7 s) come out of descent rate, input abruptness and payload; the filter chain (D-045) shapes what reaches the loop. It feeds `CameraVibration`.
6. **Battery and inertia checks** (PHY-11, 12): the heavy corner (6.5 kg, low charge) shown to lose its thrust margin; a low-voltage signal for the OSD; the inertia ratios and yaw saturating first, from the real `drone.json`.
7. **Labels and bands:** every new number pilot / measured / published / derived / assumed; every assumed one a named, tunable parameter; each mechanism's scenario fails its known-wrong model.

## Risks / Trade-offs
- **The shake can't be tuned to a number** (the pilot: it "doesn't happen much"). Its check is that it arises from its causes and is rare, not that it hits a rate.
- **Ground effect needs the world's rays over the test range** (`p6-world-query-gallery` 1.4). The stub floor carries the first scenarios.
- **Each mechanism moves the goldens.** One re-record per task, audited.

## Open Questions
- None for the pilot. The pilot's feel is judged at G7.
