## ADDED Requirements

### Requirement: A Betaflight 4.5 flight controller in acro
The controller SHALL mirror Betaflight 4.5 (D-023): ACTUAL rates, the throttle curve (`thr_mid`, `thr_expo`), deadband, RC smoothing, feedforward, PID with Betaflight's scaling, D min, I-term relax and throttle boost, in acro only with no self-levelling. It SHALL run at its real 4 kHz, sub-stepped deterministically inside the 1 kHz step, and read its settings from one record named by Betaflight CLI names. The defaults SHALL be the pilot's profiles (D-023) and the real builder's PIDs (roll 65/20/70, pitch 60/20/65, yaw I 30).

#### Scenario: ACTUAL rates as Betaflight computes them
- **WHEN** the rate curve is evaluated at stick positions 0, 0.25, 0.5, 0.75 and 1 for the default settings
- **THEN** each setpoint equals Betaflight 4.5's ACTUAL formula within 0.1 °/s

#### Scenario: The builder's PIDs hold a stable loop
- **WHEN** the drone at nominal mass takes a 200 °/s roll-rate step at hover throttle
- **THEN** the rate settles within 5 % of the setpoint, overshoots by less than 25 % and shows no sustained oscillation over 2 s

### Requirement: An X mixer that saturates honestly
The mixer SHALL map collective, roll, pitch and yaw to four motor commands for an X quad as Betaflight 4.5's does: limit the PID sums, scale the mix into range, then desaturate the collective so that no motor clips, so that yaw saturates first, as on the real drone (K§5.3).

#### Scenario: Yaw saturates first and the collective yields
- **WHEN** the wind clip's maximum yaw and roll demands (288 and 412 °/s², wind.md §5.5) are set against the nominal drone's full thrust, and full yaw is commanded at mixer throttles from 0.45 to 0.85
- **THEN** yaw runs out of mix past a collective of about 0.81 of full thrust and roll only past about 0.98 (K§5.3's 0.72 re-derived with κ = 0.0144 m and a fixed yaw moment; full thrust at the ESCs' 40 A limit), and above a mixer throttle of about 0.625 (1 less the yaw P term's 0.375 of the mix) the collective delivered falls below the commanded one while yaw, roll and pitch keep their authority, with a MixerSaturation event naming the collective

### Requirement: ESC, motor and rotor
Each rotor SHALL follow its command through an ESC filter and the motor's first-order lag, a DC motor whose lag follows from its resistance, its rotor's inertia and the prop's load, updated without transcendental functions, and produce thrust T = k_T·n² and torque Q = κ·T with k_T and κ from `drone.json`'s prop, varying with rpm as its published table says (κ = 0.0144 m, derived, for the APC 10x4.5MR). The thrust SHALL be derated by the axial inflow through the tilted disc from momentum theory, never by a flat factor (PHB-03), the inflow being each hub's velocity through the air (the wind at the hub taken off). Each ESC SHALL hold its motor's current at `drone.json`'s current limit while it drives (the policy of task 1.5: the motor's published 40 A as the ESC's current protection), logging when it begins and ends.

#### Scenario: The ESC holds the motor at its current limit
- **WHEN** every ESC steps from idle to full duty on a full pack
- **THEN** no motor's current passes 40 A, an ESC without a limit draws over 1.5 × that in the spin-up, and a CurrentLimit event names each ESC as it begins to hold

#### Scenario: The motor's lag is its physics
- **WHEN** one motor's duty steps by 0.02 near idle, at hover and near full (below the current limit)
- **THEN** its speed reaches 63 % of the change within τ_m + τ_esc ± 10 %, with τ_m = J / (Kt·Ke/R + ∂Q/∂ω) from `drone.json` (about 40 ms at hover, inside design 5's 40–60 ms), and never within one step

#### Scenario: Hover and liftoff as the real drone
- **WHEN** the drone is held at a hover through the Betaflight throttle path at its nominal 4.8 kg and loaded at 6.2 and 6.5 kg, on a full and a half pack
- **THEN** the loaded drone lifts off between 50 and 65 % motor power (pilot: about 62 % on a real heavy quad), at 6.2 kg on both packs and at 6.5 kg on the full one, and the maximum total thrust is the motor model's steady state at full duty within 1 % (derived from n_max on the published bench and the prop's table, held at the ESCs' 40 A limit: about 11.9 kgf on a full pack, inside K§5.3's 9.0–12.5 kgf; 14.2 kgf with no limit)

#### Scenario: Fast flight loses thrust to inflow
- **WHEN** the drone holds 25 m/s level with its disc tilted 22.7°
- **THEN** the axial inflow through the disc is 9.6 m/s within 10 %, and the available thrust is momentum theory's at that inflow within 10 % (PHB-03), where design 5's assumed linear derate misses by more

### Requirement: Battery sag
Two packs SHALL supply the motors with a loaded voltage V = n_s·(OCV(SoC) − I_cell·R_cell), with R_cell and n_s from `drone.json` (6S4P at 12 mΩ a cell under load, assumed: 18 mΩ for the pack, set by `p5-drone`). The state of charge SHALL be integrated over the flight, and the motors' maximum speed SHALL follow V.

#### Scenario: A full-collective pull on a sagged pack
- **WHEN** full collective is held for 3 s at 20 % state of charge
- **THEN** the loaded pack voltage is V = n_s·(OCV(SoC) − I_cell·R_cell) within 1 %, and the maximum thrust is the motor model's steady state at that loaded voltage within 1 %, above (V / V_full)² of T_full (the motor's own I·R drop falls with its current)
