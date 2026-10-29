# Tasks: p7b-airframe

Complex tier (D-033). Branch `unity/physics/p7b-airframe` off `unity/main` once `p6-flight-core` has landed its 1.6, or off the flight core's tip. Task 1.1 defines the per-rotor inflow seam shared with `p7c-wind` (design 2) and lands first. 1.2–1.5 follow it, one at a time (each re-records the goldens). 1.3 needs `p6-world-query-gallery` 1.4 for the test range's rays; the stub carries its first scenarios.

## 1. Build

- [ ] 1.0 [physics-engineer] The ESC's command lag (from `p6-flight-core` 1.7's plant check, D-045): τ_esc is an assumed 5 ms, the plant's biggest lever on the filtered step's overshoot (1 ms gives 31 % against 41 %), while first principles put it at about 1 ms (a 4 kHz DShot frame is 250 µs, PWM 24–48 kHz, L/R about 0.3 ms). Research the command latency and any rate limit of BLHeli_32, AM32 and Bluejay (e.g. BLHeli_32's maximum acceleration default), set `esc_tau_s` from it, labelled, and re-measure D-045's step; verify the step scenario's band re-stated if it moves, the goldens re-recorded and audited, replay bit for bit
- [ ] 1.1 [physics-engineer] The per-rotor inflow seam and motor asymmetry (design 1, 2): the shared inflow function, then Kv spread, ESC jitter and prop imbalance per motor from named streams within labelled tolerances, the computed band; verify the "asymmetry on and off" scenario, the goldens re-recorded and audited, replay bit for bit
- [ ] 1.2 [physics-engineer] Vortex ring state (design 4): the onset from the normalised descent rate, hysteresis, thrust loss and the wandering moment, logged entry and exit; verify the "heavy on low throttle" scenario and a known-wrong model (descent speed alone) failing it
- [ ] 1.3 [physics-engineer] Ground and ceiling effect, recirculation (design 3): per rotor from its ray, `SampleGround` and holes, the ceiling under roofs, the close hover never still; verify the "cushion and the hole" scenario on the stub, then over the test range once its queries land
- [ ] 1.4 [physics-engineer] The throttle-punch shake (design 5): the frame mode and the payload and spool pendulum, excited by wash re-ingestion, through the filter chain, into `CameraVibration`; verify the "shake only when its causes meet" scenario
- [ ] 1.5 [physics-engineer] Battery margin and inertia character (design 6): the heavy corner's lost margin, the low-voltage signal, the inertia ratios and yaw saturating first; verify the "heavy corner at low charge" scenario and PHY-12's ratios on the seven loadouts
- [ ] 1.6 [physics-engineer] The budget: `flightbench --bench` with every mechanism on, on AC with the power mode printed; verify p99 ≤ 300 µs and 0 B

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios: every `airframe-effects` scenario run by QA on its own seeds; each known-wrong model failing; every new draw named, bounded and logged, never noise on the pose; replay bit for bit; the budget on AC; hygiene
