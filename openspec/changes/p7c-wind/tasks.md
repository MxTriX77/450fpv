# Tasks: p7c-wind

Complex tier (D-033). Branch `unity/physics/p7c-wind` off `unity/main` once `p6-flight-core` has landed its 1.6, or off the flight core's tip with its sub-branches merged in. Task 1.1 starts at once (plain Python). 1.2 follows 1.1. 1.3 (world-builder) needs `p6-world-query-gallery` 1.4. 1.4 needs 1.2 and uses a synthetic belt until 1.3 lands. 1.5 follows 1.4 and 1.3, 1.6 runs with 1.5, and 1.7 and 1.8 come last.

## 1. Build

- [x] 1.1 [physics-engineer] The statistics tools (design 1): `wind_stats.py`, `wind_history_fixture.py` and `rain_stats.py` from `godot-final` ported to plain Python under `tools/wind/` (port, never merge, D-027), with their self-checks; verify the "tools reject noise and schedules" scenario and `python -m pytest tools/wind -q`
  - **As built** (standard library only: numpy is not installed; each file names its `godot-final` source and lists its departures):
    - **`wind_stats.py`:** every K§8.6 field from a log in the tracker's columns (`--csv` / `--letter`, `--segment a-b`, `--json`), T0c on wind histories (`--wind-log`), and the Gaussian check (`--gauss N`), which now returns a verdict and exit code. `--published` builds that check's noise from P's published numbers (`clip_p.py`: wind.md §1.6, §2, §3.1 flat per band, §4.4) for machines without `reference/`. On P's own log it reproduces wind.md's point values to the printed digits (residuals 0.707 / 0.311 / 0.826°, field 0.505 / 0.276°, T12 CV 0.17 / 0.23 and smallest 0.65 / 0.62, 14.2 events/min, gap CV 0.73); the intervals move in the third digit (other bootstrap draws).
    - **`wind_history_fixture.py`:** K§8.3's model, its unsmoothed reading and 12 controls (`--check N`, `--write`), with a verdict: the model and its reading pass every set, and every control fails every set.
    - **`rain_stats.py`:** wind.md §7 (R1–R8), with `track_attitude.py`'s decoder, mask, horizon and lens. ffmpeg replaces Blender's sequencer and the clips come from `letters.md`. On P, f1–f1417 line up with `attitude.csv` frame for frame (20 sampled: within 0.21° roll and 0.19° pitch), and the mask is 14.19 %. ffmpeg decodes one more frame at the end (1418, at 29.938 fps against 29.917). It takes about 14 s per native frame, so a full run is a long job and has not been run.
    - **Self-checks (TST-03):** noise with P's spectra, 5 logs per family, P's log / published numbers. The stationary family fails T9b (3.11 / 3.03; band 3.3–5.5) and passes T12 (CV 0.19 / 0.14), as in wind.md §6.5. The calm/busy family fails T9a (4.23 / 4.25; band 2.2–3.5) and T12 (CV 0.38 / 0.42 against ≤ 0.32). Both fail T10b (r ≈ 0). T0c on 40 sets × 40 seeds × 120 min: the model passes 40 of 40 (peak 1.38–2.08 against ≤ 2.4, gap CV 0.78–0.90, 0.151–0.168 shifts/min, side keep 85.9–88.2 %). Every schedule fails 40 of 40 on the peak (3.18 at least, from 10 min without the lag; 20.8–21.9 every 5 min). One direction fails on the draws; the fixed vectors fail on spread, rate, gap CV and side.
    - **Departures:** Python's `random.Random` with the original seeds instead of numpy's PCG64 (the same procedure, not the same numbers); a radix-2 FFT (noise is built at the next power of two and cut to length); `math.fsum`/`sumprod` sums; JSON per-frame files instead of `.npz`; plain argparse with `main(argv)`.
    - **Verify:** `python tools/wind/wind_stats.py --published --gauss 5` and `--letter P --segment 121-330 --gauss 5` (local), `python tools/wind/wind_history_fixture.py --check 40` (8 min), and `python -m pytest tools/wind -q` (59 tests, about 2 min; T0c on 40 sets and the Gaussian check included).
- [x] 1.2 [physics-engineer] The harness (design 2): K§8.5's simulated pilot and test flight in full, its variants, the 29.917 Hz measurement, flown headless over a synthetic belt until the test range's queries land; verify it reproduces 1.5's stand-in numbers and that its measurement round-trips through 1.1's tools
  - **As built** (`Tests/{HarnessPilot,WindHarness}.cs`, `Runtime/Wind/StandInWind.cs`'s `SyntheticBelt`, `tools/flightbench --wind-harness`):
    - **The pilot** (`HarnessPilot`, K§8.5 / wind.md §6.1): rate mode through the core's controller, its sticks at the TX12's 250 Hz.
      - It sees camera frames at 29.917 Hz, each `Delay` after it was taken (0.10 s; variants 0 and 0.2).
      - It wants e* = ē − v/(g·τ_v), τ_v 0.5 s (variants 0.3 and 1.0), with ē̇ = −v/(g·τ_v·3 s), and splits it with s = 0.64 (variants 1 and 0); k = −sin θ_ref.
      - It holds θ_ref = −22.7° and commands (ref − angle)/τ with τ 0.30 / 0.17 / 0.27 s, turned into p, q, r by the exact ZYX relations and into sticks through the controller's own ACTUAL curve.
      - Height (held within 0.07–0.10 m on average): a climb rate (h_ref − h)/2 s wanted, its error on the throttle at 0.05 per m/s plus an integral over 2 s, from 0.55 (all three assumed).
    - **The tracker's angles** (`TrackerAngles`): roll atan2(−u_x, −u_y), pitch asin(u_z), the heading the optical axis's azimuth, which are exactly the ZYX angles. They use an atan2 from the basic operations, so the pilot flies the same bits on every runtime.
    - **The test flight** (`WindHarness.Fly`): the repository drone (p5-drone, D-043) at its nominal loadout: 4.832 kg, inertia roll / pitch / yaw 0.0562 / 0.0529 / 0.0623 kg·m². K§8.5 assumed 4.8 kg and 0.044 / 0.054 / 0.050, the first attempt's component model, which the bootstrap fixture still carries. The harness grades the drone the sim flies, so roll and yaw inertia are about 27 % and 24 % above K§8.5's.
      - Armed, then released level at θ_ref and 13 m/s along the course (1.5's trim of 22.7°).
      - The course is laid with the seed's drawn wind abeam from the left (bearing + 90°). Runs are 39 s, the first 5 s discarded (1017 frames), at heights spread evenly over 20–50 m (23 / 29 / 35 / 41 / 47 m for 5 seeds).
      - The start rule (`StartSecond`, 90° of the drawn direction for the whole run) runs on the seed's prevailing history. The stand-in's never moves, so the start is 0. A late start needs 1.4's field to take a start time, and the harness refuses one until then.
    - **The synthetic belt** (`SyntheticBelt`, opt-in through `WindSetup.Belt`; every other flight is bit for bit unchanged, and so are the goldens):
      - A labelled region across the course, 9 s of flight past the release, 10 H long with H = 9.1 m (10 H in 7 s at 13 m/s). It is not a wake, and it has no height dependence.
      - Inside, smooth 1 H ramps scale both of its effects:
        - the mean wind is cut by 0.2;
        - a frozen field of 24 divergence-free plane waves (wavelengths 5–20 m, clip P's eddies; stream `wind.belt`, counter-based) adds √(2² − 1)·σ_u per component, twice the open field's intensity.
      - All are assumed and named. The header's wind entry records it (only when present), and the summary prints it.
    - **The measurement:** per run, a CSV in `track_attitude.py`'s 19 columns, which is the input `wind_stats.py`'s `load` reads.
      - Frames are numbered from 1, with time_s = (frame − 1)/29.917, roll and pitch in degrees, and yaw_rate_dps the frame-to-frame heading change × 29.917.
      - conf_* is 1, dup is 0 and flag is ok; the tracker's diagnostics are left empty.
      - `harness.json` lists each run with its `--segment` (the belt's frames, from the frames' positions), its draws, height, mass and means.
      - `--history N` writes N seeds × 120 min of the prevailing direction at 1 Hz in `--wind-log`'s columns.
    - **The tool:** `flightbench --wind-harness calm|windy|severe [--seeds N] [--out DIR] [--pilots nominal|all] [--belt on|off] [--history N]`. Seeds and pilots fly in parallel: 35 flights take about 2 s in Release.
    - **Numbers** (Severe stand-in, seeds 1–5, nominal pilot):
      - Lean into the wind: 1.76–1.85° per m/s, with the belt or without it. 1.5's own cruise lean at 13 m/s, taken as the tracker's roll (tan φ = tan(lean)·cos θ), is 1.866, so the runs are 1.1–5.5 % under it and inside K§6.5's 0.95–2.6.
      - Mean roll −5.97 to −11.15°, pitch −22.70°, airspeed 12.97–13.37 m/s, belt 6.8–7.5 s.
      - The known-wrong model without drag leans 0.000° per m/s and fails.
    - **Verified:**
      - `WindHarnessTests`: the ZYX angles, the lean, the CSV format and the start rule.
      - `SyntheticBeltTests`: off by default, the deficit and σ_b inside, no divergence with vorticity, the seed, the header's round trip, the bounds.
      - `tools/flightbench/tests/test_wind_harness_csv.py`: the original's `load` / `analyse` / `load_wind` logic reads the files.
    - **Open:**
      - **d = 0.2 s cannot hold pitch.** That variant sits at the pure-delay limit of the pitch loop (D/τ < π/2: 0.27 s at τ 0.17 s), and the frame hold and the controller already take it there. It oscillates: pitch σ 8.4°, roll σ 14.6°, airspeed about 8 m/s. Whether the variant scales τ with d, or is reported but not scored, is for 1.5.
      - The stand-in's T0c history is constant, so it fails T0c, as it must until 1.4.
      - **The committed round trip through 1.1's ported `wind_stats.py` waits for 1.1 to land.**
        - A provisional run through 1.1's in-progress port (its local `c2253f8`, not on origin yet) read every Severe run: 1017 of 1017 frames usable at 29.917 Hz, and every target's field present.
        - T0c ran on 40 seeds × 120 min and fails, as it must: spread 0°, no shifts.
      - **The stand-in through that port** (Severe, nominal, 5 seeds; 1.5 grades the real field):
        - T0 −6.0 to −11.1° (|mean| 8.6°, inside 9 ± 3).
        - T1 0.11–0.29°, under 0.35–0.70: the stand-in has no 2–4 Hz energy, and 1.4 adds it.
        - T3 0.47–1.06° over the synthetic belt.
        - Field kurtosis 8–36: the field is quiet next to the belt's transients.
        - T10b −0.13 to +0.29 (P: +0.57). With s = 0 it falls to −0.62 to −0.76: the drone's yaw lags the pilot's roll compensation, so the yaw's own horizon roll, −0.39·ψ̇, shows. In still air a 10° heading step overshoots 48 %, with the horizon within ±1.7°.
      - The belt's eddies are frozen (1.4 advects them), and the region has no wake physics (1.3).
- [ ] 1.3 [world-builder] The wake grid (design 4): per direction on the 2 m grid from the placed assets' wind volumes and porosity over the test range, recomputed on a worker; verify a belt's deficit and boost against K§6.7's functions, recirculation behind a solid house, a jet through the posadka's gap, and the worker never stalling the step
- [x] 1.4 [physics-engineer] The field (design 3, 5): the mean profile, von Kármán turbulence, the wakes, vorticity and per-rotor gradients, the direction model, replacing the stand-in; verify the "same seed, same wind" and the per-rotor gradient scenarios, the goldens re-recorded and audited, replay bit for bit
  - **As built** (`Runtime/Wind/{WindField,WindSetup}.cs`, `DetMath.Log/Exp`; `LogFormat.Version` 6, `ModelVersion` 6; header model `field`):
    - **The field** is a pure function of the flight's time and a point, given the ground under the body (one `SampleGround` a step at the body's origin, pilot's levels only); no wind state in `FlightState`. Sampled at each hub, the centre of pressure, the coil and the camera; the record carries the wind at the body's origin (the stand-in's gust slot) and at the four hubs (96 B appended, 1392 B).
    - **Profile:** U(z) = U·ln((z − d)/z0)/ln((z_ref − d)/z0), the drawn strength U at z_ref 35 m (assumed: the 20–50 m test band's middle), cover 0.3 m (assumed: stubble), z0 0.03 m and d 0.20 m (K§6.7's rules), held above 100 m (assumed), 0 at d + z0 and below; a hole is sheltered until 1.3's cavity.
    - **Turbulence:** von Kármán E(k) as 128 random Fourier modes (assumed) in log bands from 0.25/(1.339·L_u) to 2π/1 m (assumed), streams `wind.turbulence` (5 draws a mode, counter-based), each mode across its wave vector (vorticity), frozen and advected with the mean's drift, tapered over the first 1 m above d (assumed), each component bounded by its modes' summed amplitudes. σ_u/U 0.135 (assumed; the surface layer's 2.4·u* gives 0.136 at 35 m), σ_w/σ_u 0.594, L_u 167 m, L_v = L_u/2, L_w 35 m (derived: MIL-HDBK-1797 at 35 m). The same for every level; the centres are the stand-in's (Calm 0.8, Windy 2.5, Severe 5.0 m/s ± 25 %) until 1.5.
    - **Direction:** `wind_history_fixture.py`'s model step for step at 1 Hz on `wind.meander` (Irwin–Hall normals) and `wind.shift` (exponential gaps, size, side, ramp), from 10 τ before second 0, for 2 h after `WindSetup.StartSecond` (assumed horizon; it holds after); the mean is linear between seconds, its drift exact. `WindHarness.PrevailingFromDeg` reads it, and the harness starts each run at its start second.
    - **Belt** (1.2's) kept, its eddies now advected; **`GustFront`** (opt-in, header-recorded) is the scenario's stimulus, with `Uniform` as its known-wrong field.
    - **Verified** (suite 206 of 206; `--scenarios` PASS; `--golden` PASS; pytest tools/wind and the harness CSV 62 of 62):
      - Same seed, same wind: Severe held at 30 m, 20 000 of 20 000 steps bit-identical at all 4 rotors and the body; an unseeded field matches 0 of 5000; 400 bearings χ² 5.5.
      - Per-rotor gradients: a front (4 m/s sideways, 1.5 m/s down, 0.3 m ramp, 6 m/s) crossing tail to nose gives pitch 5.5 °/s at +129 ms and yaw 10.7 °/s at +67 ms; the uniform field 0.11 and 0.67 °/s.
      - Spectrum at 13 m/s (5 seeds): 2–4 Hz variance 0.87 of von Kármán's (Severe 0.064 m/s RMS), 1–2 Hz 1.29; a field cut below 1 Hz 0.02, white noise 0.39. σ_u/U 0.130–0.132, σ_w/σ_u 0.632–0.640.
      - T0c (`--history 1600`, `wind_history_fixture.py --grade`): 40 of 40 sets of 40 × 120 min; spread 46.5–51.9° (every seed ≥ 32°), 0.150–0.169 shifts/min, gap CV 0.77–0.93, peak 1.29–1.83, side 85–89 %.
      - Goldens audited against model 5 (35c6432): in still air and in a steady 5 m/s both models fly every record and event bit for bit over model 5's 1296 bytes (steady less the wind slot); Windy and Calm move from step 1. Powered `0xBC85ED28D55D2986`, contact `0x3D6092D3121E468A`.
      - Replay: a 60 s Severe flight, 60 000 steps and 150 events bit for bit. Bench on AC: the powered step p50 21.6 µs, p99 128.8 µs (model 5: 9.3, 60.4), 0 B.
    - **First readings, ungraded** (Severe, nominal pilot, 5 seeds, `wind_stats.py`): T0 −2.5 to −12.3°; T1 0.16–0.35°, T2 0.02–0.08°; T3 0.67–1.32°, T4 0.11–0.53°; T5 0.32–0.69° / 1.6–4.8°; T6a 84–100 %, T6b 0.011–0.026°; T7 11–18/min, 83–100 % in the belt; T9a 3.3–11.5, T9b 2.8–4.7; T10b −0.10 to +0.14; T11a 0.97–2.48° / 0.21–0.42°; T12 CV 0.16–0.57. Lean 1.69–1.83° per m/s (1.2's check passes).
    - **Open:** the wakes and the trench cavity wait for 1.3; the cover from the world too. For 1.5: T1, T2, T6b and T10b sit low at 13 m/s and the field kurtosis runs high on two seeds; the belt takes 5–9.4 s, as the wind along the course moves within the start rule's 90°. For 1.8: the field doubles the step's p99.
- [ ] 1.5 [physics-engineer] The levels against the targets (design 2): Severe tuned until T0 passes, Windy and Calm from it, every K§8.6 target on five seeds, T0c on 40 × 120 min, each target failing its known-wrong model; verify the "Severe meets every target", "direction history" and "trade-off" scenarios
- [ ] 1.6 [physics-engineer] Cruise in wind (design 6): the drag re-derived for clip P's pitch at 17–22 m/s, superseding 1.5's 13 m/s calibration in `drone.json`'s `aero` block; verify the "clip P's pitch flies the cruise" scenario and T0 at that airspeed
- [ ] 1.7 [physics-engineer] Rain (design 7): the rain rate per level, its drag and wet mass, the camera's air velocity; verify the "rain without wind" scenario and `rain_stats.py`'s targets for clip P
- [ ] 1.8 [physics-engineer] The budget (design 8): `flightbench --bench` over the posadka in Severe on AC; verify the "cost of the wind" scenario

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios: every `wind-turbulence` scenario run by QA on its own seeds; the ported tools' self-checks; each known-wrong model failing; replay and determinism with the field; the budget on AC; the port's provenance (D-027); hygiene
