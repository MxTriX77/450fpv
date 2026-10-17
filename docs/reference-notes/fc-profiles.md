# Flight-controller profiles: real heavy fiber quads

The sources are the pilot's own tool **FpvPrep** (`https://github.com/MxTriX77/FpvPrep`, local copy `C:\FPV_TEST`) and its saved profiles, read 2026-10-17. The pilot's instruction (verbatim in [../pilot-brief-2026-10-17.md](../pilot-brief-2026-10-17.md), answer 4):

> use those used by me while leaning a little bit towards those sharpy original settings, especially throttle - you dont always get the chance to set comfortable throttle when in combat env. HOWEVER, it's important that you make room for flexibility on this one coz in the future I MAY ask you to introduce betaflight rates, throttle curves, and other fly control related settings you will have seen in this FpvPrep

**OPSEC:** drone hardware ids from the profiles are deliberately not copied here.

## 1. The quads (an OTU batch, 10-inch heavy fiber class)
- **Firmware:** Betaflight **4.5.0** on an STM32F405 board with an ICM-42688-P gyro.
- **Loop rates:** gyro at **8 kHz**, `pid_process_denom = 2`, so the **PID loop runs at 4 kHz**. RC link at **250 Hz**.
- **Motors:** **DShot300, bidirectional** (with dshot_burst), and the **RPM filter on** (`rpm_filter_min_hz = 50`). On the idle test (command 1050), all four motors ran at 1186–1214 rpm.
- **Battery: 8S**, reading 33.6–33.8 V full (4.2 V per cell). Cell thresholds are **2.80 V minimum and 3.00 V warning, so Li-ion**. The pilot's own packs (6S4P/6S5P) are the other end of the class. **The class spans 6S–8S Li-ion.**
- **Throttle and arming:** `min_throttle = 1070`; `failsafe_throttle = 1400`; `small_angle = 100` (arms at any angle).
- **The builder's PIDs**, the same on every drone in the batch, and never overwritten:

  | Axis | P | I | D | D min | Feedforward |
  |---|---|---|---|---|---|
  | roll | 65 | 20 | 70 | 60 | 80 (factory) / 40 (pilot) |
  | pitch | 60 | 20 | 65 | 60 | 80 / 40 |
  | yaw | BF default | 30 | 0 | — | 0 |

  Also `iterm_relax_cutoff = 8`, simplified tuning off, `throttle_boost = 5` (default).
- **Filters (heavy, hand-set by the builder):**
  - gyro LPF1 dynamic 35–90 Hz (biquad) and LPF2 static 90 Hz (biquad)
  - a gyro notch at 50 Hz (cutoff 35)
  - a dynamic notch at 60–200 Hz (Q 500)
  - D-term LPF1 dynamic 35–80 Hz (biquad, static 63) and LPF2 80 Hz (biquad)
  - a D-term notch at 60 Hz (cutoff 45)

  **What this means physically:** strong airframe and arm resonance in the 50–60 Hz region on these frames, and a lot of filter phase delay. Both are part of why a heavy quad feels "behind the sticks", and part of the PR-1 throttle-punch shake.

## 2. Rate profiles

Betaflight ACTUAL rates: the centre sensitivity and the full-stick rate are in °/s, and expo runs 0–1.

| Setting (Betaflight CLI) | OTU factory ("sharp") | **Pilot's flown set ("ideal")** | FpvPrep standard heavy-quad set | **Sim default** |
|---|---|---|---|---|
| `rates_type` | ACTUAL | ACTUAL | ACTUAL | ACTUAL |
| roll / pitch centre (`*_rc_rate` × 10) | 70 | 50 | 50 | **55** |
| roll / pitch full stick (`*_srate` × 10) | 150 | 250 | 220 | **225** |
| roll / pitch expo (`*_expo` / 100) | 0 | 0.45 | 0.45 | **0.34** |
| yaw centre | 70 | 90 | 80 | **85** |
| yaw full stick | 150 | 300 | 240 | **260** |
| yaw expo | 0 | 0.20 | 0.20 | **0.15** |
| `thr_mid` | 50 | 50 | builder's | **50** |
| `thr_expo` | 0 | 25 | builder's | **10** |
| `deadband` / `yaw_deadband` | 0 / 0 | 3 / 3 | 4 / 5 | **2 / 2** |
| `rc_smoothing_auto_factor` | 30 | 60 | 80 | **50** |
| `f_roll` / `f_pitch` (feedforward) | 80 | 40 | builder's, with `feedforward_transition 40`, `jitter_factor 12`, `boost 0` | **50** |
| `f_yaw` | 0 | 0 | — | **0** |
| `vbat_sag_compensation` | 0 | 0 | 100 | **0** (offered as an option) |

**How the sim default was set:** the pilot's flown set moved **25 % of the way toward the factory set**, and **throttle moved 60 %**, because the pilot stressed it: "especially throttle - you dont always get the chance to set comfortable throttle". These are named, tunable defaults. The pilot adjusts them after MVP-1.

## 3. Other class data from FpvPrep's notes
- **Liftoff and hover power.** A loaded heavy quad lifted off at **about 62 % motor power**. Its builder had shaped the throttle as `thr_mid 100, thr_expo 100`, so power = 1 − (1 − stick)³, which spreads the last 10 % of power over almost half the stick. FpvPrep had calculated hover at 31–52 %; the real drone needed more. **Use this as a plant-realism check:** a loaded Svinoriz should lift off around 50–65 % motor power.
- **Rate reasoning (FpvPrep standard set).** A pilot closing the attitude loop through video works at a crossover of about 2.4–2.8 rad/s (the wind study measured about 3.3 rad/s). A 1° error needs about 4.5 % stick, so the centre is about 50–58 °/s. The look-ahead pitch-up needs 100–150 °/s; turns live below 72 °/s of yaw. Yaw authority on these sizes is only **about 500–800 °/s²**.
- **The radio is a TX12 MKII on EdgeTX 2.8.0.** A real failure mode the pilot hit: stick calibration with two axes of nearly zero span (79 / 1 and 85 / 0, against healthy spans of 683–903), which then behave like switches. The sim's calibration wizard must detect this and tell the pilot to recalibrate the radio (INP-02).
- **Payload and PIDs:** a payload adds 40–100 % roll and pitch inertia, against about 35 % more motor gain at the higher hover throttle, leaving the loop 4–32 % weaker than when tuned empty. That is why FpvPrep scales PIDs only in 10 % steps, and why the sim should behave the same way with payload.

## 4. What this means for the sim (decision D-023)
- **The flight-controller model mirrors Betaflight 4.5:**
  - ACTUAL rates
  - the throttle curve (`thr_mid`, `thr_expo`, Betaflight's 11-point table)
  - deadband
  - RC smoothing (auto factor)
  - feedforward (transition, jitter, boost)
  - PID with Betaflight's internal scaling, plus D min and I-term relax
  - throttle boost
  - battery sag compensation
  - optionally the gyro and D-term filter chain (the phase delay matters for feel)
- **The config uses Betaflight CLI names,** so a real `diff all` can be imported later.
- **The first UI** (Calibrate) exposes rates and the throttle curve, like FpvPrep's `set controls -m` and `throttle -m`. **Room is left** for later pilot requests: feedforward, smoothing, deadband, PID stiffness steps, sag compensation, throttle softer/stiffer.
- **Two checks of the physics plant** (phase 6):
  1. The real builder PIDs above give a stable, well-damped loop on the sim plant.
  2. A loaded drone lifts off near 50–65 % motor power.
- **Battery:** the class spans 6S–8S Li-ion. The pack voltage and arrangement for "Svinoriz 10 Opto" (two packs) is researched in phase 5.
