## ADDED Requirements

### Requirement: A Betaflight 4.5 flight controller in acro
The controller SHALL mirror Betaflight 4.5 (D-023): ACTUAL rates, the throttle curve (`thr_mid`, `thr_expo`), deadband, RC smoothing, feedforward, PID with Betaflight's scaling, D min, I-term relax and throttle boost, in acro only with no self-levelling. It SHALL run at its real 4 kHz, sub-stepped deterministically inside the 1 kHz step, and read its settings from one record named by Betaflight CLI names. The defaults SHALL be the pilot's profiles (D-023) and the real builder's PIDs (roll 65/20/70, pitch 60/20/65, yaw I 30).

#### Scenario: ACTUAL rates as Betaflight computes them
- **WHEN** the rate curve is evaluated at stick positions 0, 0.25, 0.5, 0.75 and 1 for the default settings
- **THEN** each setpoint equals Betaflight 4.5's ACTUAL formula within 0.1 °/s

#### Scenario: The builder's PIDs hold a stable loop
- **WHEN** the bootstrap drone at nominal mass takes a 200 °/s roll-rate step at hover throttle
- **THEN** the rate settles within 5 % of the setpoint, overshoots by less than 25 % and shows no sustained oscillation over 2 s

### Requirement: An X mixer that saturates honestly
The mixer SHALL map collective, roll, pitch and yaw to four motor commands for an X quad, clip each command to [0, 1] and then desaturate the collective, so that yaw saturates first, as on the real drone (K§5.3).

#### Scenario: Yaw saturates past three-quarter collective
- **WHEN** full yaw is commanded at collectives from 0.5 to 0.9 on the bootstrap drone
- **THEN** the achieved yaw moment falls below the commanded one once the collective passes about 0.72 (K§5.3, derived), and roll and pitch authority are kept

### Requirement: ESC, motor and rotor
Each rotor SHALL follow its command through an ESC filter and the motor-rotor first-order lag, updated exactly without transcendental functions, and produce thrust T = k_T·n² and torque Q = κ·T (κ 0.015–0.02, assumed). The thrust SHALL be derated by the axial inflow through the tilted disc from momentum theory, never by a flat factor (PHB-03).

#### Scenario: Hover and liftoff as the real drone
- **WHEN** the bootstrap drone is flown at nominal 4.8 kg on a full pack, and again loaded at 6.5 kg
- **THEN** the maximum total thrust lies in K§5.3's 9.0–12.5 kgf (derived), and the loaded drone lifts off between 50 and 65 % motor power (pilot: about 62 % on a real heavy quad)

#### Scenario: Fast flight loses thrust to inflow
- **WHEN** the drone holds 25 m/s level with its disc tilted 22.7°
- **THEN** the axial inflow through the disc is 9.6 m/s within 10 %, and the available thrust is momentum theory's at that inflow (PHB-03)

### Requirement: Battery sag
Two packs SHALL supply the motors with a loaded voltage V = n_s·(OCV(SoC) − I_cell·R_cell), with R_cell ≈ 15 mΩ (assumed) and n_s from `drone.json` (6S or 8S Li-ion, set by `p5-drone`). The state of charge SHALL be integrated over the flight, and the motors' maximum speed SHALL follow V.

#### Scenario: A full-collective pull on a sagged pack
- **WHEN** full collective is held for 3 s at 20 % state of charge
- **THEN** the loaded pack voltage is V = n_s·(OCV(0.2) − I_cell·R_cell) within 1 %, and the maximum thrust is T_full at full charge scaled by (V / V_full)² within 1 %
