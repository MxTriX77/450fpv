# Spec Delta

## Purpose

What a strike does to the props and motors and how the drone flies on afterwards: each blade's structure and the material it loses, chips, bends or breaks; a damaged rotor's thrust, torque and once-per-revolution shake; a new prop's small factory imbalance; where a blade meets a wall, a pole, a trunk, a car, a conductor or the ground, and which of the two yields; the bell scraped, dented and rubbing, the shaft bent, the bearings brinelled, the motor bound; the ESC's and the OSD's current that show it; and the log, summary and replay that keep it for the flight. Every effect comes from the strike's energy, geometry and materials, never from a scripted loss or noise on the pose.

## ADDED Requirements

### Requirement: The blade's structure and state
Each of the eight blades SHALL carry its own state along 40 stations from 0.1 R to its tip (the tip radius, the chord share left, the pitch and flap sets, the mass lost and its moments), built from `drone.json`'s `props.blade` block (the sheet's chord and thickness, the section angles, and the glass-filled nylon's density, flexural and crush strength, fracture energy, proportional limit and hardness, each labelled), with the two blades and the hub summing to the prop's published mass; every change SHALL be a logged `BladeDamaged` event naming its mode and its cause's strike.

#### Scenario: The blade's tables are labelled and add up
- **WHEN** `drone.json` is loaded with its `props.blade` block, and a copy without one material value
- **THEN** every value carries a label and a rationale, the blades and the hub weigh the prop's 16 g within 0.1 g, the blade's inertia about the hub is within drone.json's rotor inertia band, and the copy is refused naming the missing field

#### Scenario: A new blade is whole
- **WHEN** a flight arms
- **THEN** every blade's tip radius is 1, every station's chord share is 1, every set is 0 but the factory pitch draw, and no `BladeDamaged` event exists

### Requirement: A damaged rotor's thrust and torque
A rotor's thrust and torque SHALL follow blade-element momentum theory on what is left of its blades (the chords, the tip radii with Sissingh's tip loss, the pitch sets), as ratios to the intact sheet blade on the published table's advance ratios, recomputed only when its blades change; a sound rotor without the factory pitch SHALL be exactly the published table.

#### Scenario: A trimmed blade loses thrust from its outer span
- **WHEN** one blade is trimmed by 5, 12.7 and 25 mm, and both blades by 5 mm, at 9000 rpm in still air
- **THEN** the rotor's thrust ratio equals the BEMT's integral within 1e-9 relative, the loss is larger than the removed share of the span for every trim, a 5 mm one-blade trim costs between 2 and 5 % of the rotor's thrust, and a 160-station run agrees with the 40-station tables within 0.5 % of the thrust at 6000, 9000 and 13 000 rpm

#### Scenario: The intact blade is the published table
- **WHEN** a sound rotor with the factory pitch off flies the powered golden flight
- **THEN** its thrust and torque are the published table's bit for bit and the golden keeps its bits

#### Scenario: The trimmed prop is bracketed by the published diameters
- **WHEN** both blades are trimmed to 0.9 R and run at 6000, 9000 and 12 000 rpm in still air
- **THEN** the thrust lies between the nearest smaller MR prop's published static thrust and the 10x4.5MR's at each speed

#### Scenario: A symmetric trim speeds its rotor up
- **WHEN** the drone hovers 10 s in still air with both blades of one rotor trimmed by 10 mm, and again with one blade of that rotor trimmed by 10 mm
- **THEN** with the symmetric trim the damaged rotor's hover speed is higher than the others' by at least 2 % and the camera's 50–200 Hz band stays within twice the sound drone's, while the one-blade trim raises that band above the factory band's 99.5th percentile, its spectrum peaking within 2 % of the damaged rotor's rotation rate (Pose et al. 2024's symmetric and one-tip findings)

### Requirement: The once-per-revolution loads
Each rotor SHALL pull its hub with the vector sum of its factory unbalance and its blades' lost mass, U·ω², and SHALL load its hub with the blades' unequal lift and drag (a moment and an in-plane force) and with the centrifugal moment of mass out of the disc's plane (a blade's flap set, a bent shaft's tilt), all turning with `RotorAngle`, summed at each propulsion sub-step into the rigid body, and so into the gyro, the frame's modes, the camera's pose and `VibrationBands`; nothing SHALL be added to the pose.

#### Scenario: The loads follow their forms
- **WHEN** a rotor with 12.7 mm off one blade spins at 3000, 9000 and 15 000 rpm on a body held fixed
- **THEN** the hub's in-plane force over a turn is U·ω² within 1e-9 relative, with U the lost mass's first moment plus the factory draw, the aerodynamic moment is the blades' thrust difference times their centroids within 1e-9 relative, both turn at the rotor's rate, and both vanish on a sound prop with the factory draws off

#### Scenario: The loop is not fooled, the camera is
- **WHEN** the drone hovers 10 s with one blade trimmed by 12.7 mm, with the builder's filter chain
- **THEN** the loop stays stable (attitude peak to peak in the second 5 s under 2°), the gyro's 1/rev after the RPM filter is at least 10 times weaker than before it, and the camera's 50–200 Hz band is above the factory band's 99.5th percentile

### Requirement: A new prop's factory imbalance
Each flight SHALL start every prop with its factory mass unbalance (`prop.unbalance`, ISO 21940-11's G 6.3, as built) and each blade's factory pitch offset (`prop.blade_pitch`, uniform within ±0.25°), drawn from named seeded streams and logged with their bounds, as the baseline once-per-revolution shake and as a per-rotor thrust offset; PHY-07's band SHALL be recomputed with them.

#### Scenario: The pitch draws cover their bounds
- **WHEN** the draws of 10 000 seeds are taken
- **THEN** every Δθ lies within ±0.25°, both ends are approached within 0.5 % of the bound, and each flight's header lists its eight draws with their bound

#### Scenario: The baseline shake is bounded away from imperceptible
- **WHEN** the drone hovers 10 s in still air on 20 000 seeds' draws, and with both factory draws off
- **THEN** the camera's 50–200 Hz RMS band over the seeds (0.5–99.5 %) is printed, its 0.5th percentile lies above the draws-off hover's, and the mass and aerodynamic shares are each at least a fifth of the total at the median

#### Scenario: PHY-07's band moves and still holds
- **WHEN** `AsymmetryScenario` predicts the 30 s hands-off hover's drift band with the pitch draws, and the five-seed hover flies with asymmetry on and off
- **THEN** the new band is printed beside the old, every seed's correction rate lies inside the new band, and with asymmetry off the drift is at least halved

### Requirement: Where a blade meets a shape
Each disc pair SHALL take its shape's exact geometry from the world (a runtime object at its pose; the ground as its local plane), stay alive while the shape cuts the disc's slab, and meet each blade pass in the step at the smallest radius the blade's swept azimuths reach inside the shape inflated by the blade's half-thickness, with the face's normal, the blade's normal speed and the face's tilt to the disc there.

#### Scenario: The meeting radius is exact
- **WHEN** a disc turning at 9000 rpm meets a vertical box face 1, 5 and 20 mm inside its rim, a 20 mm rod 60 mm inside it, a sphere, a cylinder, a conductor and the plane under a disc tilted 30°, each on a body held still
- **THEN** each pass's meeting radius equals the closed-form intersection within 1 µm, each blade meets the shape once per pass across it, and the rod, once inside the rim, is still met on every pass

#### Scenario: A runtime object is met where it is
- **WHEN** a prop meets the launch stand's post after the stand has moved
- **THEN** the meeting uses the post's geometry at the stand's current pose

### Requirement: Who yields, and the hard target's modes
A face harder than the blade (by the rows' `hardness_pa`, Rabinowicz's rule) SHALL damage the blade: it SHALL crush the blade back to the meeting radius, or break it at the first station whose bending passes its strength, or, when the face is tilted to the disc and bending clears it for less force than crushing, slap and chip it, bend it past its proportional limit or slide over it and wear it by Archard's law; each pass's energy and the face's push SHALL follow from the mode.

#### Scenario: Grazing a wall trims the tip and pushes the drone off
- **WHEN** a hovering drone drifts at 0.3 m/s into `H01_D`'s south wall until a prop's rim is 1 mm in, the disc level
- **THEN** the struck blades are crushed back to the wall in their passes (`BladeDamaged` crush, the length within 1 µm of the geometry, or less when the energy runs out), the rotor's speed dips in each strike's step by √(ω² − 2E/J) within 1e-9 relative, the face's push moves the drone away from the wall, and the energies and impulses per pass are printed

#### Scenario: A tilted disc slaps rather than crushes
- **WHEN** the same drift meets the wall with the drone pitched 20° toward it, and the plane ground under a disc tilted 30°
- **THEN** the blades bend over the face (no crush event) unless a slap's peak moment passes a section's strength, every break or set is logged at its station with its peak moment, and the friction work and the worn volume are printed per pass

#### Scenario: A head-on rod breaks the blade
- **WHEN** a disc at 9000 rpm meets a 12 mm steel rod 40 mm inside its rim
- **THEN** the blade breaks at a station inside the rod's radius, the fragment's mass and energy leave the rotor, and with a known-wrong model that crushes instead of breaking, the blade's loss is the rod's depth only, so it fails the break

### Requirement: Soft targets and the ground
A face softer than the blade SHALL be carved at its own crushing pressure over the volume the blade sweeps, the blade flinging the mass it meets (E = p·V + m_f·v²), the blade unharmed unless the cutting force breaks a section; in soil the pressure SHALL come from the row's cohesion (c·N_c) and its sand SHALL wear the blade; compliant thin rows (straw, foliage, twig shells, fabric, mesh) SHALL take no blade damage from this change.

#### Scenario: A prop in a trunk sticks a bit
- **WHEN** a prop at hover speed is pushed 1, 5 and 20 mm into V01_L's trunk (bark) and `L01_straight` (timber)
- **THEN** at 1 mm each pass costs the carve's energy and the rotor recovers between passes, at 20 mm the rotor stops on the wood (`RotorJammed`) and turns again as the motor drives it, and the blade is broken only where its cutting force passes a section's strength, each printed

#### Scenario: Props that touch the ground bog down
- **WHEN** the drone tips over on landing on the dirt track and on S04's bare soil, its props reaching 2 and 10 mm into the ground
- **THEN** each pass's energy is p·V + m_f·v² within 1e-9 relative, the flung mass dominates, the 10 mm dig stops the rotor within its first two passes, and the blades' leading edges show Archard wear

#### Scenario: Who yields is decided by the material
- **WHEN** the same 5 mm entry at hover speed meets concrete, steel, masonry, timber, bark, rubber and S04's straw boxes
- **THEN** concrete, steel and masonry damage the blade, timber, bark and rubber are carved, the straw boxes leave it whole, and with a known-wrong model that damages by impact speed alone the straw boxes damage it as concrete does, so it fails

### Requirement: The strike in its step, and no crash by a prop
Each pass SHALL take its energy from the rotor in the strike's step (a dip, or a stop when the energy exceeds the rotor's, its damage then cut short by the energy left), give the body the opposite angular impulse, the tangential impulse at the meeting point and the face's push, and leave its damage to act from the next step; a prop strike SHALL never by itself end the flight, which ends on the body's acceleration crash as before.

#### Scenario: The strike's impulses balance
- **WHEN** any strike on a body held free in space
- **THEN** the angular momentum about the rotor's axis lost by the rotor is the body's gain within 1e-9 relative, the energy the rotor loses equals the pass's work within 1e-9 relative, and the damage first changes the rotor's coefficients in the next step

#### Scenario: The drone flies on after a wall touch
- **WHEN** the drone grazes `H01_D`'s wall at 2 m/s with a prop, and again flies into it at 10 m/s
- **THEN** the graze ends no flight and the drone hovers after it, damaged; the 10 m/s flight ends in a crash by acceleration named by a body part, with the prop's strike and damage logged before it

### Requirement: The vegetation seam
Each element strike that `p7a-ground` writes to `FlightState.BladeStrikes` SHALL be read in its step with its cut energy, its diameter and its radius on the blade, its cutting force (the cut energy over the element's diameter) SHALL be checked against each inner station's edgewise strength, and only a section it breaks SHALL be damaged; shape strikes SHALL be added to the same slot.

#### Scenario: Straw never chips a prop
- **WHEN** the drone descends at 0.3 m/s into S04's stems and hovers with its discs among them for 10 s, and a known-wrong model that chips a blade by every strike's energy
- **THEN** no blade is damaged, the strikes are logged by 7a, and the known-wrong model chips the blades, so it fails

#### Scenario: A thick twig can
- **WHEN** a disc at hover speed meets a 20 mm green twig in V01_L's crown
- **THEN** the cutting force and each station's margin are printed, and the blade chips or breaks where the margin is passed, logged with the twig's `Id`

### Requirement: The motor's bell, its gap and binding
A contact on a turning bell SHALL slide on the bell's moving surface, its kinetic friction slowing the rotor and pushing the drone along the face; a peak normal force past the bell ring's plastic collapse load SHALL dent it, narrowing the magnetic gap (the torque constant from the gap's flux) and, past the gap, rubbing the stator with a drag torque that wears in and thins the magnet; a rub torque beyond the motor's torque at its ESC's current limit SHALL bind it (`MotorBound`).

#### Scenario: A scraped bell draws more current
- **WHEN** a hovering drone presses one motor's bell on `H01_D`'s wall at 5 and 20 N while holding its height, and the same with a known-wrong motor whose damage is a flat efficiency loss
- **THEN** the rotor feels μ_k·F_n·r_bell within 1e-9 relative, the drone is pushed along the wall in the bell's surface direction, that motor's current and the total current rise with the push, and the known-wrong motor's current rise does not grow with the push, so it fails

#### Scenario: A dented bell rubs and can bind
- **WHEN** a motor bell is struck sideways at 1, 3 and 6 m/s on concrete with the rotor at hover speed
- **THEN** each dent past the collapse load is logged with its depth, a dent short of the gap changes Kt by the flux form within 1e-9 relative, a dent past it rubs with Roark's force and its torque and wears in over the printed time, and a rub past 0.42 N·m with the losses binds the motor with its ESC held at 40 A

### Requirement: The shaft and the bearings
The hub's strike loads SHALL bend the shaft past its yield moment (a run-out and a disc tilt fixed in the rotating frame, so a mass and a couple unbalance and a tilted thrust), break it past its ultimate (the prop lost), and brinell a bearing past its static rating (ISO 76: a drag torque, and a ball-pass vibration published to `CameraVibration` as its RMS, never stepped in the 1 kHz body).

#### Scenario: A bent shaft shakes
- **WHEN** a motor's bell takes side hits whose moment at the upper bearing is 0.8, 1.2 and 2 times the yield moment, then the drone hovers 10 s
- **THEN** the first leaves the shaft straight, the others log a bend with its run-out and tilt, the 1/rev hub loads grow with the bend as their forms give within 1e-9 relative, and the camera's 50–200 Hz band rises with them

#### Scenario: A brinelled bearing
- **WHEN** a bearing's radial load from a hit passes 276 N
- **THEN** the dent is 1e-4·D_w·(R/C0r)² within 1e-9 relative, its drag torque and ball-pass frequencies are logged, the rigid body's state is unchanged by its vibration bit for bit, and `CameraVibration` gains its RMS in the bands its frequencies fall in

### Requirement: The ESC and the current show the damage
Each motor's damage SHALL enter the existing motor and ESC model only as its torque constant, its added drag torques and its rotor's inertia, so the current, the current limit and the bus respond as built, and the OSD's current element (D-057) SHALL show it through `MotorCurrentTotalA` with no new element.

#### Scenario: A dragging motor meets its limit sooner
- **WHEN** the drone with one rubbing motor and the sound drone fly the same full punch from a 5 m/s descent on the same seed
- **THEN** the rubbing motor's ESC reaches its 40 A limit earlier and holds it longer, and the step's total current is its motors' sum within 1e-9 relative

#### Scenario: A trim raises the hover's current
- **WHEN** the drone hovers with one blade trimmed by 12.7 mm and sound, on the same seed and pack charge
- **THEN** the total current is higher with the trim, and `MotorCurrentTotalA` carries it to the OSD

### Requirement: Breakage
A blade broken at a station, a blade lost at its root, a prop lost with its shaft, a rotor jammed and a motor bound SHALL each be a logged event with its cause's strike, and the physics SHALL fly the result with nothing scripted.

#### Scenario: A lost blade throws the drone over
- **WHEN** a hovering drone loses one blade at its root
- **THEN** `BladeLost` is logged, the rotor's unbalance and thrust follow the remaining blade, and the drone cannot hold its attitude (its tilt passes 45° within 1 s, printed) with the builder's PIDs and the sticks centred

### Requirement: Damage persists, is logged and replays
The damage SHALL persist for the flight in preallocated tables, the step record SHALL carry each rotor's damage aggregates, every change SHALL be an event, the header SHALL carry the blade and motor tables with `drone.json`'s hash and the new draws, a `DamageReport` SHALL list each rotor's final state and its events in order for the post-flight summary and forensics, and a headless replay SHALL rebuild all of it bit for bit.

#### Scenario: Replay is bit for bit through damage
- **WHEN** a 120 s flight with wall grazes, a bell scrape, a trunk strike, a ground strike and a bind is recorded and replayed headless
- **THEN** every step, event and the `DamageReport` match bit for bit, a log with one bit of the `props.blade` block flipped is refused, and the log is within LOG-01's 50 MB a minute after compression

#### Scenario: A crash names its prior damage
- **WHEN** a damaged drone crashes
- **THEN** the `DamageReport` lists, before the crash, each strike that damaged it with its time, object, shape, material and mode, so forensics can tell a physics bug from pilot error

### Requirement: The pilot's account of a scratch
Scripted scrapes of the props and the motors along the range's wall SHALL make the drone worse on the counts the pilot can feel (more throttle on the damaged rotor and more current at the hover after any scrape; more shake in the camera's 50–200 Hz band than the factory imbalance gives once a scrape leaves the blades unequal), more so for a deeper scrape, and each known-wrong model SHALL fail a target. A claim the physics does not keep on every flight SHALL be stated as a rate (D-053): "nearly every" is at least 95 % of a cell's 40 seeds.

#### Scenario: Worse after a scratch
- **WHEN** `flightbench --damage` flies along `H01_D`'s wall at 1, 2 and 4 m/s with a rim 1, 3 and 5 mm in for 0.3 s, and with a bell pressed at 5 and 20 N, then hovers 10 s, on 40 seeds a cell, and the same with damage off
- **THEN** on every seed of every cell the struck rotor's hover command and the hover's total current exceed the undamaged flight's; on nearly every seed of the 3 and 5 mm cells the camera's 50–200 Hz RMS exceeds the factory band's 99.5th percentile (a graze trims both blades alike, so the 1 mm cells' and the bell cells' rates are printed, not claimed: Pose et al.'s symmetric case); the cells' medians rise with depth; and each cell's medians, spreads and the blades' length difference are printed

#### Scenario: Known-wrong models fail the account
- **WHEN** the same cells fly with no damage, a scripted thrust multiplier per strike, damage by impact speed alone, a fixed chip per strike, and a flat motor efficiency loss
- **THEN** no damage fails every "worse"; the multiplier fails the camera band; speed alone and the fixed chip fail the straw and material cases; the efficiency loss fails the bell's push dependence

### Requirement: Damage's randomness, determinism and budget
The only new draws SHALL be each blade's factory pitch (`prop.blade_pitch`) and each blade's strength factor (`prop.blade_strength`, nearly normal, σ 5 %, clamped ±12 %), both logged with their bounds; nothing about a strike, the world or the damage SHALL be drawn. Every new state SHALL be preallocated (0 B in steady state), use only + − × ÷ √ and `DetMath`, and keep PLT-04's step budget.

#### Scenario: The strength draws cover their bounds and set the chip
- **WHEN** the strength draws of 10 000 seeds are taken, and one scripted head-on strike flies on 1000 seeds
- **THEN** every factor lies within ±12 % with both clamps reached, and the strike's broken length varies across the seeds only through the drawn strength, within the range its bounds give

#### Scenario: The budget holds
- **WHEN** `flightbench --bench` runs on AC with the power mode printed: the props scraping `H01_D`'s wall for 2 s, a hover with a broken blade, a strike burst into V01_L's trunk, and a prop in the ground on a tipped landing
- **THEN** every case's p99 step is at most 300 µs with 0 B allocated, and the damage's share of the step and the queries' share are printed
