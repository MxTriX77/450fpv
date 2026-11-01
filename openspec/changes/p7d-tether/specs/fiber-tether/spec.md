# Spec Delta

## Purpose

The fiber-optic line the drone flies on: the coil beneath paying out one way, the span hanging, trailing and lying behind it over the ground, the crowns and the wires, the pull it puts on the drone, the bends and rubs that may or may not cut it, the optical margin those bends cost, and what the pilot sees and the flight controller does when it breaks. Every outcome comes from the line's mechanics, the drone's motion, the air and the world; the only randomness is the strength of the line and the stickiness of its pack, named, bounded and logged. The pilot's account is the target: smooth orbits are safe, a sharp reverse, a snag, a very sharp liftoff or backward momentum after liftoff may cut it.

## ADDED Requirements

### Requirement: The tether table
The physics SHALL read every number of the fiber, the spool, the link and the wires' fittings from one physics-owned table, `assets-src/tether.json`, each value carrying its label (published, derived or assumed) and its source or reason. The rows in use and the file's hash SHALL be written to the log header, and a replay SHALL build from the header and refuse a file whose hash differs.

#### Scenario: Every value labelled and loaded
- **WHEN** the table is loaded, and again with one value's label removed
- **THEN** the first loads the fiber, strength, spool, link, fittings and launch blocks, and the second is refused naming the block, the field and the missing label

#### Scenario: A replay refuses another table
- **WHEN** a recorded flight is replayed with one value of `tether.json` changed
- **THEN** the replay is refused before its first step, naming both hashes

### Requirement: A cable chain stepped implicitly
Each line (the fiber, an active conductor) SHALL be a chain of lumped nodes with tension-only segments, stepped in the 1 kHz step by linearly implicit Euler through one block-tridiagonal solve using only + − × ÷ √, so its axial stiffness never limits the step; nodes on a contact SHALL let material slide through them only by the capstan law (holding while the larger tension is at most e^(μs·θ) times the smaller, sliding at e^(μk·θ)); nodes SHALL be inserted and merged by fixed rules on the state alone, keeping momentum. The chain SHALL never gain energy beyond the work done on it in the step.

#### Scenario: A hanging span is a catenary
- **WHEN** 40 m of a test line with the fiber's linear mass and EA hangs between two fixed points 30 m apart at the same height, from rest, until it settles
- **THEN** every node lies within 1 mm of the elastic catenary through the two points, and its horizontal tension equals the catenary's within 1 %

#### Scenario: The capstan holds and slips
- **WHEN** a line is wrapped 90° and 180° round a fixed rod with μ 0.30/0.25 and one end is loaded slowly while the other is held at 0.1 N
- **THEN** it holds until the loaded end passes 0.1 × e^(0.30·θ) N within 1 %, then slides with the ratio e^(0.25·θ) within 1 %

#### Scenario: Energy is never gained
- **WHEN** 10 000 random-start chains (stiff and slack, on and off contacts, two members on one node) are stepped with no external work
- **THEN** no step's total energy exceeds the previous step's by more than 1e-12 relative, no value is non-finite, and a forced breach is logged as `TetherWedge`

### Requirement: One-way payout from the coil beneath
The coil SHALL pay line out through the rear cap's exit only while the tension at the exit, less the exit lip's capstan, exceeds the pack's resistance (its rate-dependent peel energy times the section's irregularity, the line's momentum flux and its balloon's inertia), and SHALL never take line back. The resistance SHALL be logged every step as `payout_tension_n`, and the paid-out length SHALL be state.

#### Scenario: Line pays out as the drone flies away
- **WHEN** the drone flies straight away from its launch point at 5, 10 and 20 m/s for 20 s in Calm
- **THEN** the paid-out length rises monotonically and is at least the straight-line distance from the anchor, and the logged exit tension at steady speed equals the resistance's law at that speed times the lip's capstan within 2 %

#### Scenario: Line never comes back
- **WHEN** the drone flies 100 m out and then straight back toward its launch point
- **THEN** the paid-out length never decreases, the exit tension falls to the slack span's own load, and the slack shows in the span's shape and the trail

#### Scenario: A known-wrong two-way spool
- **WHEN** the same flight is flown with a spool that takes line back at the payout tension
- **THEN** the paid-out length falls on the return, and the sharp-reverse target (requirement "The pilot's accounts") fails with it

### Requirement: The span and the laid trail
The span SHALL be at most 32 active nodes from the exit to where the line comes to rest, spaced finely near the drone and more coarsely beyond, carrying gravity, drag on the relative wind by its Reynolds number, the wind field, the rotors' inflow and wash, and in rain its wet film; a node at rest on the ground or an object, held by its friction, SHALL freeze into a laid trail, and the trail SHALL lift back into the chain, last frozen first, when the tension at the touchdown exceeds its holding friction. The world SHALL store no line.

#### Scenario: A falling line settles at its terminal speed
- **WHEN** a horizontal 10 m length of fiber is released 50 m up in still air
- **THEN** it falls at 1.4 m/s within 5 % once it has accelerated (C_d by White's fit at Re 24), and lands after 35 s within 5 %

#### Scenario: The line lies on what is under it
- **WHEN** the drone hovers 30 m from its anchor over the range's plane, S04's straw and E01's run, after 60 s of flying
- **THEN** the line from the touchdown back to the anchor lies on the ground, the straw's top or the trench's floor and walls within 2 mm, its trail points never inside a rigid shape, and none of it floats

#### Scenario: A pulled trail lifts
- **WHEN** the trail is held by a snag 50 m behind the drone and the drone climbs and flies on
- **THEN** trail points between the snag and the touchdown unfreeze in order, the chain stays at 32 nodes or fewer, and the paid-out length grows only once the span between the exit and the snag is taut at the payout tension

#### Scenario: Rain makes it heavier and draggier
- **WHEN** the same 30 s cruise is flown dry, in Rain and in Severe rain
- **THEN** the airborne line's mass per metre and diameter follow the table's films, and it settles faster and trails lower in rain than dry

### Requirement: The rotors' air moves the line
Each rotor's air SHALL act on fiber nodes near it from its own thrust: inflow toward the disc above it (the vortex cylinder's axial law inside its radius, a sink of the disc's flux outside), its wake below it, and a radial wall jet when the disc is near the ground.

#### Scenario: A line under a punch is lifted and drawn
- **WHEN** the drone sits legs-on on the field start with the line laid under it and goes to full throttle from rest, and again at hover thrust
- **THEN** at full throttle the line beside the skids is lifted by the wall jet and moves toward the nearest disc's edge faster than at hover, the logged drag on each node matches the field's law at its position within 1 %, and a node that crosses a turning disc cuts the line

#### Scenario: A known-wrong still air near the props
- **WHEN** the very-sharp-liftoff set of "The pilot's accounts" is flown with the rotors' air removed from the line
- **THEN** that target fails

### Requirement: The line pulls the drone
The tension at the exit SHALL act on the drone at the exit point, below and behind the centre of mass, as a force and a moment, and every contact of the line with a drone part SHALL act on the drone at its point; the line SHALL never act through the centre of mass alone.

#### Scenario: The exit's pull and its moment
- **WHEN** the drone hovers with the line trailing taut behind it at the payout tension, and the line is then held fast 20 m behind
- **THEN** the force and moment on the drone equal the logged exit tension along the first segment at the logged exit point within 1e-9 relative, and once held the exit tension rises until the drone is pulled back and pitched toward the hold or the line breaks

#### Scenario: A line round a skid tethers the drone
- **WHEN** the line is wrapped once round the rear skid and held fast on the ground behind, and the drone climbs at hover + 0.2
- **THEN** the skid's contact carries the line's load, the drone is pulled down and back at the skid and rolls or pitches toward it, and the line breaks by tension or bend, or slips round the skid by the capstan, whichever comes first, logged with its mechanism

### Requirement: The coil gets lighter
The coil's mass SHALL fall by the line paid out (0.069 kg/km), and the mass model, the inertia and `AirframeModes`' coil mass SHALL be rebuilt at each 10 m of payout with the angular momentum kept.

#### Scenario: The drone gets lighter
- **WHEN** a flight pays out 2 km
- **THEN** the total mass falls by 0.138 kg within 1 g, the centre of mass rises as the mass build gives for that coil mass within 1e-9 m, and no rebuild changes the angular momentum

### Requirement: The fiber's contacts
The line SHALL meet the ground and its cover, every shape except the homogenised `twig`, `foliage` and `razor_wire` shells, p7a's elements (twigs as capstans of their own radius, standing stems as cantilevers, lying straws as hooks by the over-side rule), snag records (a capstan of their edge radius; with a barb pitch, locked along the record within half a pitch of the first touch while free to slip along its own length), every conductor (a capstan of its strand), the drone's own parts and the launch stand. A node touching two members SHALL take both contacts in the same solve.

#### Scenario: A line draped over a coil run holds
- **WHEN** a line is draped over a synthetic concertina run (the fortifications reading's records: `edge_radius_m` 0.00025, `barb_pitch_m` 0.035) while the drone flies on, on 20 seeds
- **THEN** the line holds on every seed whose section strength exceeds the logged stress at the barbs, never cuts on a seed whose strength there is above 1.35 GPa, and each contact point moves along its record by at most 17.5 mm

#### Scenario: A reverse round a snagged barb cuts
- **WHEN** the drone reverses round a line snagged on one barb, turning it through 90–180°, on 20 seeds
- **THEN** every seed whose section strength at the barb is below the logged stress cuts, logged with mechanism `bend`, the object, the record index, θ, T, R_eff, σ and the strength

#### Scenario: Compliant shells are skipped
- **WHEN** a line is dragged through V01_L's crown
- **THEN** it meets the crown's twig elements and limb capsules, never its `twig` shell pieces, and the log names each contact's element `Id` or shape

#### Scenario: The wedge of a fork
- **WHEN** a line is pulled down into the fork of two limbs until one node touches both
- **THEN** both contacts act in the same step, the tension rises without bound only as the drone pulls, nothing is non-finite, and the line breaks by tension or bend or holds

### Requirement: Cut risk, never certainty
The line SHALL break, logged with its mechanism and place, when a segment crosses a turning prop disc (`prop`), when the tension stress on the span reaches the flight's span strength (`tension`), when the local stress at a bend (the elastica's bend over an edge, the exit lip or the pack's peel front, plus the tension) reaches the strength of the fiber section there (`bend`, or `peel` at the pack), or when a section's coating is worn through by rubbing and its abraded strength is then exceeded (`abrasion`). The span strength SHALL be one draw a flight and each 0.1 m section's strength a draw indexed by its position along the line, each bounded and logged when first read.

#### Scenario: The same manoeuvre can go either way
- **WHEN** an identical sharp reverse is flown from the same spot on 40 flight seeds in Windy
- **THEN** some seeds break the line and some do not, every break names its mechanism, its place and the strength it met, and each seed gives the same outcome on every replay

#### Scenario: Each mechanism on its own
- **WHEN** a tension spike (the line held on the drone, the drone climbing), a tight bend at low tension (a line pulled round a 0.3 mm sheet-metal edge at 0.1 N), a long rub (a line sawing over a steel arris in wind at 0.3 N) and a line drawn into a disc are each run
- **THEN** each can break the line on its own, the logged mechanism is the one exercised, and the abrasion break comes only after the logged wear passes the coating's thickness

#### Scenario: The bend is the elastica's
- **WHEN** a line at 0.05, 0.2, 0.5 and 2 N turns 15°, 45°, 90° and 180° over a point edge and over 2 mm and 6 mm radii
- **THEN** the logged bend radius equals max(R_edge + c, √(EI/T)/(2·sin(θ/4))) within 1e-9 relative and the stress E·r/R_eff + T/A_g likewise

#### Scenario: A break leaves a flyable drone
- **WHEN** the line breaks
- **THEN** the far piece stays where it lies, the drone keeps the piece hanging from its exit (still loaded by its own weight and drag), the line's force on the drone is that piece's, and the link state is Broken in that step

### Requirement: The link margin
The link's margin SHALL be the optical budget less the attenuation of the paid-out length, the macrobend loss at every bend (the table's law fitted through ITU-T G.657.A2's limits, times the wrap over a full turn) and the microbend loss at loaded contacts, each step; a dip SHALL be the margin falling below the receiver's threshold, logged with its start, end, minimum and the bend or contact with the largest loss. The margin SHALL never read the attitude rate or rain on the lens.

#### Scenario: A free line costs only its length and its lip
- **WHEN** the drone flies rolls and flips at 600 °/s for 30 s with the line hanging free in Calm, 2 km paid out, and the same lip angles are then imposed on a drone at rest
- **THEN** the margin equals the budget less 0.42 dB and the exit lip's logged bend loss within 0.01 dB in both runs, step for step, and no dip is logged

#### Scenario: A snag dips the link
- **WHEN** the line is snagged round a crown twig and the drone flies on in Windy
- **THEN** the margin falls by the twig's bend loss as the line wraps it, varies as the wrap flutters in the wind, and each dip names the twig's `Id`

#### Scenario: The bend loss follows the law
- **WHEN** a line is wrapped one full turn round rods of 15, 10, 7.5, 5 and 3 mm radius
- **THEN** the losses are 0.004, 0.1, 0.5, 2.5 and 9.1 dB within 1 %

### Requirement: The break loses the picture and the flight controller fails safe
On a break the link state SHALL be Broken and the margin −∞ in the same step, so the feed plays its loss within a field; the flight controller SHALL then run Betaflight 4.5's failsafe stages by their CLI names (`failsafe_delay`, `failsafe_procedure`, `failsafe_throttle`, `failsafe_off_delay`) and disarm, each stage logged as `Failsafe`.

#### Scenario: From the break to the disarm
- **WHEN** the line is cut by a disc 20 m up in a hover
- **THEN** `TetherBreak` and the link's Broken state share a step, the sticks stop reaching the controller from that step, stage 1 and stage 2 follow at the configured times within one step, the drone disarms, and the feed's loss begins within one field of the break

### Requirement: The link's signals reach the feed
The exit tension, the minimum bend radius, the link state and the margin SHALL be published to the feed as 50 Hz field means and maxima, replacing their stubs, with `TetherBreak` and `LinkDip` as loss events.

#### Scenario: Every link signal is live
- **WHEN** a flight's feed signals are read field by field
- **THEN** the four signals come from the physics (the review overlay shows `physics`), each field's mean and maximum equals the steps' within 1e-9 relative, and every dip and the break appear as events in their field

### Requirement: The fiber's randomness
The fiber's randomness SHALL be the named streams `fiber.span_strength`, `fiber.section_strength`, `spool.peel` and `spool.irregularity`, each bounded as the table gives, counter-based where indexed by section so that the order of use never shifts a draw, and logged; nothing about where the line lies or what it touches SHALL be drawn.

#### Scenario: The draws cover their bounds
- **WHEN** each stream is drawn over 10 000 seeds (and 10 000 sections of one seed)
- **THEN** every draw lies inside its bounds, the lowest and highest 1 % of each band are reached, and the same seed and index give the same value whatever was drawn before

#### Scenario: The line is not drawn
- **WHEN** two flights with different seeds fly the same scripted course in Calm with the strength streams pinned
- **THEN** the line's nodes, contacts and trail are identical bit for bit

### Requirement: The fiber's look
The trailing line SHALL be drawn through the physics' active nodes and its laid trail (DRN-05), at its real diameter with the thin-wire coverage rule (a line weighing 270·D/d of a pixel, fading, never widened to show), with the coil's wound pack shrinking as it pays out.

#### Scenario: The line in the chase view
- **WHEN** the chase view follows a 20 m/s cruise and a reverse at 1.5, 10 and 50 m
- **THEN** the drawn line passes within 1 mm of the physics' nodes and trail points published for that frame, its coverage follows the rule within 10 %, and it never sparkles or breaks into dashes across four quarter-pixel camera shifts

### Requirement: The pilot's accounts
`tools/flightbench --tether` SHALL fly the simulated pilot through each account on its seeds and grade it: a sharp reverse cuts on some seeds and not others, smooth orbits and a gentle 180° turn never do, a pass through a crown snags the line on most seeds and cuts it on some, a very sharp liftoff cuts on some and a gentle one never, backward momentum after liftoff cuts on some, and 10 km paid out straight at cruise cuts on fewer than 1 in 20. Each target SHALL fail at least one known-wrong model.

#### Scenario: The accounts over many seeds
- **WHEN** each account is flown on 40 seeds (20 for the rated payout) as design 16 sets
- **THEN** each target passes, and every cut names its mechanism and place

#### Scenario: Known-wrong models fail
- **WHEN** the accounts are flown with a two-way spool, the line as a spring through the centre of mass, one strength for every check, no rotors' air on the line, and a cut drawn from the attitude rate
- **THEN** each fails the target design 16 names for it

### Requirement: Determinism, the budget and the log for the line
The fiber's state SHALL be preallocated with 0 B in steady state; a recorded flight with the line SHALL replay bit for bit without the engine, Unity's EditMode (Mono) giving the same goldens as .NET; the step SHALL stay within PLT-04 (p99 ≤ 300 µs) on physics' pattern with the fiber's queries printed against the 60 µs line (D-054); and a segment SHALL be queried only while its clearance certificate requires it.

#### Scenario: The budget holds
- **WHEN** `flightbench --bench` times the stand's launch with the line laid, a 20 m/s cruise trailing from 50 m, a sharp reverse into its own slack, the line through V01_L's crown and a strike on `L03_medium`, on AC with the power mode printed
- **THEN** each case's p99 step is at most 300 µs with 0 B, and its queries' p50, p99 and maximum a step are printed against the 60 µs line

#### Scenario: Replay is bit for bit with the line
- **WHEN** a 120 s flight with payout, a snag, a dip and a break is recorded and replayed headless
- **THEN** every step record and event matches bit for bit, and the log's size per minute is printed against LOG-01's 50 MB

#### Scenario: A segment far from everything is not queried
- **WHEN** the line trails 50 m up in the open at 20 m/s
- **THEN** each segment is queried at most once per 25 steps, never after its certificate has run out, and a shape placed 0.4 m from a segment is met in the step the segment first reaches it
