# Tasks: p7c-wind

Complex tier (D-033). Branch `unity/physics/p7c-wind` off `unity/main` once `p6-flight-core` has landed its 1.6, or off the flight core's tip with its sub-branches merged in. Task 1.1 starts at once (plain Python). 1.2 follows 1.1. 1.3 (world-builder) needs `p6-world-query-gallery` 1.4. 1.4 needs 1.2 and uses a synthetic belt until 1.3 lands. 1.5 follows 1.4 and 1.3, 1.6 runs with 1.5, and 1.7 and 1.8 come last.

## 1. Build

- [ ] 1.1 [physics-engineer] The statistics tools (design 1): `wind_stats.py`, `wind_history_fixture.py` and `rain_stats.py` from `godot-final` ported to plain Python under `tools/wind/` (port, never merge, D-027), with their self-checks; verify the "tools reject noise and schedules" scenario and `python -m pytest tools/wind -q`
- [ ] 1.2 [physics-engineer] The harness (design 2): K§8.5's simulated pilot and test flight in full, its variants, the 29.917 Hz measurement, flown headless over a synthetic belt until the test range's queries land; verify it reproduces 1.5's stand-in numbers and that its measurement round-trips through 1.1's tools
- [ ] 1.3 [world-builder] The wake grid (design 4): per direction on the 2 m grid from the placed assets' wind volumes and porosity over the test range, recomputed on a worker; verify a belt's deficit and boost against K§6.7's functions, recirculation behind a solid house, a jet through the posadka's gap, and the worker never stalling the step
- [ ] 1.4 [physics-engineer] The field (design 3, 5): the mean profile, von Kármán turbulence, the wakes, vorticity and per-rotor gradients, the direction model, replacing the stand-in; verify the "same seed, same wind" and the per-rotor gradient scenarios, the goldens re-recorded and audited, replay bit for bit
- [ ] 1.5 [physics-engineer] The levels against the targets (design 2): Severe tuned until T0 passes, Windy and Calm from it, every K§8.6 target on five seeds, T0c on 40 × 120 min, each target failing its known-wrong model; verify the "Severe meets every target", "direction history" and "trade-off" scenarios
  - **From 1.2's harness (the orchestrator's calls):** the d = 0.2 s pilot variant scales its τ on each axis to keep the nominal pilot's delay-to-τ ratio (0.10 / 0.17 on pitch), since a pure delay of 0.2 s with τ 0.17 s is past the loop's stability limit and no real pilot flies that way; the harness grades the drone the sim flies (the repository `drone.json`, roll / pitch / yaw 0.0562 / 0.0529 / 0.0623 kg·m²), not K§8.5's first-attempt inertia; T10b going negative with s = 0 (the yaw's own horizon roll) is a reading for this task, not a bug.
- [ ] 1.6 [physics-engineer] Cruise in wind (design 6): the drag re-derived for clip P's pitch at 17–22 m/s, superseding 1.5's 13 m/s calibration in `drone.json`'s `aero` block; verify the "clip P's pitch flies the cruise" scenario and T0 at that airspeed
- [ ] 1.7 [physics-engineer] Rain (design 7): the rain rate per level, its drag and wet mass, the camera's air velocity; verify the "rain without wind" scenario and `rain_stats.py`'s targets for clip P
- [ ] 1.8 [physics-engineer] The budget (design 8): `flightbench --bench` over the posadka in Severe on AC; verify the "cost of the wind" scenario

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios: every `wind-turbulence` scenario run by QA on its own seeds; the ported tools' self-checks; each known-wrong model failing; replay and determinism with the field; the budget on AC; the port's provenance (D-027); hygiene
