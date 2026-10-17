# Spec Delta

## Purpose

Everything between the pilot's sticks and the air: the rate controller, the mixer, the ESC and motor lag, the pack that sags under load, the thrust and torque each rotor makes, the slight asynchrony between four supposedly identical motors, and the inflow effects that change what a heavy quad does near the ground and in descent. This is where manifesto §2.4's "motors working a tad bit asynchronously" becomes a force the pilot has to answer.

## ADDED Requirements

### Requirement: Rate mode only
The flight controller SHALL be rate (acro) mode: the sticks command body angular rates through a per-axis PID, and there SHALL be no self-levelling, angle mode, horizon mode, altitude hold or position hold anywhere in the loop (`wind.md` Q3, pilot).
The controller SHALL run inside the fixed step, at the step rate or an integer division of it.

#### Scenario: Sticks released does not level the drone
- **WHEN** the drone is rolled to 40° in still air and every stick is returned to centre
- **THEN** the roll rate returns to zero but the roll attitude stays at 40° ± 2° until the drone's own aerodynamics or a pilot input change it, and it never returns toward level on its own

#### Scenario: Rate command is followed
- **WHEN** a step roll-rate command is given in still air, well inside the drone's authority
- **THEN** the achieved body rate reaches the commanded rate within its stated settling time and holds it within 5 %

### Requirement: Mixer with honest saturation
Motor commands SHALL come from a standard X-quad mix of collective, roll, pitch and yaw, with the motors 0.160 m from the roll and pitch axes for the 452 mm wheelbase (derived, `wind.md` §5.5). Yaw SHALL come from the rotor drag-torque difference between the two counter-rotating pairs.
When any motor's command would leave its physical range, the mixer SHALL clip it and desaturate by reducing the collective, SHALL NOT let any motor exceed its limit, and SHALL log a `mixer_saturation` event naming the axis that lost authority.

#### Scenario: Yaw runs out first
- **WHEN** increasing yaw demand is applied at a collective near hover, at the nominal loadout
- **THEN** yaw saturation is reached at a differential thrust share 4–8 × smaller than the roll demand that saturates roll (derived, `wind.md` §5.5), and each saturation is logged

#### Scenario: Saturation is not silently exceeded
- **WHEN** simultaneous maximum collective, roll, pitch and yaw are demanded
- **THEN** no logged motor command or rotor thrust exceeds the motor's derived maximum, the collective is reduced to make room for the attitude axes, and the event log names the axis

### Requirement: ESC and motor lag
A command SHALL reach the rotor through at least two first-order lags: an ESC command filter and the motor–rotor mechanical time constant derived from rotor inertia and thrust. Rotor speed SHALL be an integrated state, never a direct function of the command.

#### Scenario: Thrust lags a step command
- **WHEN** a step from hover collective to full collective is applied
- **THEN** rotor thrust rises with the combined first-order response of the two lags, reaching 63 % of the change within one combined time constant ± 10 %, and never instantaneously

#### Scenario: Rotor speed is a state
- **WHEN** the collective is stepped to zero from full
- **THEN** rotor speed decays continuously, thrust follows it, and neither reaches its new value within a single step

### Requirement: Thrust and torque curves
Per rotor, thrust SHALL follow T = k_T · n² and rotor drag torque Q = κ · T, with κ in 0.015–0.02 N·m per newton (assumed, `wind.md` §5.5). k_T SHALL be set so that a motor at full command on a loaded pack produces its derived maximum thrust.
Thrust SHALL be derated by axial inflow, so a rotor in fast forward flight produces less thrust at the same speed than in hover.

#### Scenario: Maximum thrust matches the derived airframe
- **WHEN** all four motors are commanded to maximum on a fully charged pack at rest
- **THEN** total static thrust is 9.0–12.5 kgf (derived, `wind.md` §5.5), and per motor 2.3–3.1 kgf

#### Scenario: Hover throttle matches the derived margin
- **WHEN** the drone hovers at the nominal 4.8 kg loadout on a mid-charge pack, out of ground effect
- **THEN** the collective command is 0.70–0.80 of full, consistent with thrust going as the square of the command at a thrust margin of about 1.8 (derived, `wind.md` §5.5)

#### Scenario: Forward flight costs thrust
- **WHEN** the drone is trimmed in level flight at 13 m/s with the discs tilted forward
- **THEN** each rotor's thrust at a given rotor speed is derated to 0.80–0.95 of its static value (assumed, `wind.md` §5.5)

### Requirement: Battery sag
Pack voltage SHALL be V = 6 · (OCV(state of charge) − I_cell · R_cell), with OCV 3.4–4.1 V per cell across the usable charge (assumed, `wind.md` §5.5). Available thrust SHALL fall with loaded voltage, and state of charge SHALL integrate the drawn current over the flight.

#### Scenario: Thrust falls as the pack empties
- **WHEN** the same maximum-collective command is applied at full charge and near the end of usable charge
- **THEN** total thrust at the end is measurably lower, and at the heavy corner of the loadout range (6.5 kg on a sagged pack) the thrust margin reaches 1.0 or less, so the drone can no longer climb

#### Scenario: A hard pull sags the pack
- **WHEN** the collective is stepped from hover to maximum
- **THEN** logged pack voltage drops immediately with current and recovers when the current falls, and the drop is consistent with the logged current and the cell resistance within 5 %

### Requirement: Motors are slightly asynchronous
Each motor SHALL carry its own per-flight perturbations of real physical parameters, drawn once at arm time from the flight seed within bounded ranges and written to the log header: a **Kv offset**, an **ESC timing offset** and a **prop thrust imbalance**. These SHALL be parameters of the motor and rotor model, and SHALL NOT be forces, moments or rate offsets added to the body.
The resulting disturbance SHALL be a slow wander that the rate controller partly but never fully removes, so the pilot must correct with roll and yaw (manifesto §2.4).

#### Scenario: A hands-off hover drifts and needs correction
- **WHEN** the drone hovers in a world with wind disabled, on flat ground out of ground effect, with all sticks at neutral, over 30 s, on at least 10 seeds
- **THEN** on every seed the attitude wanders away from its start and requires correction to hold, with the mean absolute drift rate inside a stated band, and no seed either stays within measurement noise or diverges to an uncontrolled attitude within the 30 s

#### Scenario: Turning the asymmetry off is visibly steadier
- **WHEN** the same seeds are flown with the per-motor perturbations disabled
- **THEN** the roll and pitch residual standard deviations fall by at least a factor of 3 compared with the perturbed runs, which demonstrates that the wander comes from the motors and not from the integrator or the controller

#### Scenario: The disturbance scales with throttle
- **WHEN** hands-off hover runs are flown at low and high collective on the same seed
- **THEN** the yaw and roll bias grows with collective, because a Kv offset is a proportional error, so a correction trimmed at hover is wrong in a climb

#### Scenario: Asymmetry is a parameter, not noise
- **WHEN** the propulsion code is reviewed and the log header is read
- **THEN** every per-motor perturbation appears as a named parameter with its drawn value in the header, and no random number is drawn inside the stepped propulsion path for these effects

### Requirement: Ground effect per rotor
Each rotor's thrust SHALL rise as it approaches a surface below it, using its own height from the downward raycast that `world-query` provides per rotor, so the four rotors can be at four different heights. The effect SHALL be negligible above a rotor height of about two rotor radii, and SHALL apply over objects (a roof, a rubble pile) as well as terrain. A surface above a rotor SHALL produce a ceiling effect by the same mechanism.

#### Scenario: Extra lift close to the ground
- **WHEN** the drone hovers at decreasing heights over flat ground at a fixed collective
- **THEN** total thrust rises monotonically as height falls, is within 2 % of the out-of-ground-effect value at two rotor radii, and is materially higher at a quarter of a rotor radius

#### Scenario: Uneven ground rolls the drone
- **WHEN** the drone hovers with two rotors over flat ground and the other two over ground 0.5 m lower, at a fixed collective and with all sticks neutral
- **THEN** the rotors over the higher ground produce more thrust, and the drone rolls away from them without any pilot input

#### Scenario: Ground effect over an object
- **WHEN** a rotor passes 0.3 m above a sheet-metal roof
- **THEN** that rotor's thrust rises, driven by the raycast hit on the roof object rather than on the terrain beneath it

### Requirement: Vortex ring state
A rotor descending into its own wake SHALL lose thrust and gain unsteadiness, over a band of descent rate normalised by induced velocity, with hysteresis so the condition does not chatter at its boundary. Entry and exit SHALL be logged as events.

#### Scenario: A vertical descent sinks unexpectedly
- **WHEN** the drone is held at a fixed collective that hovers, and then descends vertically at increasing descent rates
- **THEN** within the onset band the achieved thrust falls below the out-of-band value at the same rotor speed, the descent accelerates without any change of stick, and a `vrs_enter` event is logged with the descent rate

#### Scenario: No chatter at the boundary
- **WHEN** the drone descends at a rate held exactly at the onset threshold for 10 s
- **THEN** the logged entry and exit events number no more than 2, and the thrust multiplier does not oscillate between its in-band and out-of-band values at the step rate

#### Scenario: Forward flight clears it
- **WHEN** the drone in the vortex-ring condition is given forward speed
- **THEN** the condition clears, a `vrs_exit` event is logged, and thrust returns to its normal value

### Requirement: Prop wash between rotors
In close hover each rotor SHALL see an inflow perturbation from the other rotors' recirculating wakes, scaled by proximity to the ground, so hover near the ground is never perfectly still even with the wind and the motor asymmetry disabled.

#### Scenario: Close hover is less steady than high hover
- **WHEN** the drone hovers at a quarter of a rotor radius above flat ground and then well out of ground effect, both with wind and motor asymmetry disabled
- **THEN** the close hover shows a measurably larger roll and pitch residual standard deviation than the high hover
