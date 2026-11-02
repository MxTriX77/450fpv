# Spec Delta

## Purpose

Everything that happens where the drone meets the ground and the plants on it: the soil under each skid, the straw, the stems and the crowns' twigs and leaves that push it and load its props, the holes and furrows a leg drops into or rocks across, the rain that makes it all softer and stickier, the launch stand it lifts off from, and the bounce, wobble and stick the pilot feels. Every effect comes from the world's ground and the physics, never from a draw about the ground; the pilot's account (legs stick "sometimes, a little, more on soft ground, most of the time in rain") is the target.

## ADDED Requirements

### Requirement: The surfaces table
The physics SHALL read every ground surface's mechanics from one physics-owned table, `assets-src/surfaces.json`: for each of the thirteen surfaces a dry, a wet and a saturated set (soil moduli, unload ratio, maximum sink, damping, friction on the drone's parts, adhesion and its release separation, the soil lip's edge radius) and, per cover kind (straw, stem, twig), the modulus, the hollow share, the hook release range, the specific cut energy and the roller's cross-axis friction. Every value SHALL carry its label (published, derived or assumed) and its source or reason. The rows in use and the file's hash SHALL be written to the log header, and a replay SHALL build from the header and refuse a file whose hash differs.

#### Scenario: Every row labelled and loaded
- **WHEN** the table is loaded, and again with one value's label removed
- **THEN** the first loads all thirteen surfaces with three sets each and every cover kind, and the second is refused naming the surface, the field and the missing label

#### Scenario: The reference foot's sinks are kept
- **WHEN** a flat 15 mm round foot (a disc, displacing its area times its sink) is loaded with 25 N on each dry surface of K§7.2 through the new law
- **THEN** its static sink equals the value derived from the row within 1 %, and the K§7.2 sinks (meadow_sod 20 mm, dry_crust 2.8, crater_spoil 51, belt_straw 30, belt_bare 10, tilled 35, yard_litter 4.0, rubble 1.4, weeds 10) within 10 %, or the row's rationale says why it moved

#### Scenario: A replay refuses another table
- **WHEN** a recorded flight is replayed with one value of `surfaces.json` changed
- **THEN** the replay is refused before its first step, naming both hashes

### Requirement: Elasto-plastic soil under a part
A part on soil (a half-skid, a support or coil sphere, an arm, the stand's base) SHALL meet Winkler's foundation, a force of the soil's modulus times the volume the part displaces, with Wong's load and unload rule over the groove the part has pressed (applied to every soil column under the part, so an unload ratio of 1 is elastic), a firm layer past the maximum sink and viscous damping; each leg SHALL be in series with its soil at equal force, solved each step by a fixed number of Newton steps, and the law SHALL apply to the ground function and every soil shape alike (`Surface != 0`), using only + − × ÷ √.

#### Scenario: A resting drone sinks as the rows say
- **WHEN** the drone (legs on, 5.18 kg) rests disarmed for 3 s on each surface of the test range, dry, with wind and asymmetry off
- **THEN** each half-skid's sink at its load centroid equals the value derived from its row and the corner's load within 2 %, the leg's deflection times its stiffness equals the soil's force within 1e-9 N, and no contact event happens in the last second

#### Scenario: Soil keeps a plastic set
- **WHEN** a flat 15 mm disc is loaded to 25 N on crater_spoil and unloaded, three times in the same print, and a half-skid the same way
- **THEN** for both the sink recovered on each unload is smaller than the sink applied, the retained set grows on the first loading and grows by less than 5 % of it on the third; the disc's second and third reloadings are stiffer than its first loading by the surface's unload ratio within 1e-9 relative, and the half-skid's follow Wong's rule column by column, the virgin law at z_m − ratio·(z_m − z), within 1e-9 relative

#### Scenario: Loose soil swallows a fast landing
- **WHEN** the drone lands at 1.5 m/s with the throttle cut at the touch on the shelled patch's `crater_spoil` and on the track's `dirt_track`, dry, and the same with a known-wrong elastic soil (the unload ratio 1)
- **THEN** on crater_spoil the skids sink further, the stop takes longer and its peak acceleration is lower than on dirt_track, and a set remains; the elastic soil keeps no set and returns more energy on both, so it fails

#### Scenario: The approximations are bounded
- **WHEN** a half-skid's soil force is taken by the three-point rule against a 32-point rule, and its section's area against the circular segment, at the resting sinks and tilts of every surface
- **THEN** the three-point rule is within 1 % of the 32-point rule, and the section within 15 % of the circular segment up to the tube's radius, both printed

### Requirement: Friction, ploughing and footprints
Each half-skid SHALL have a stick-point friction anchor whose holding limit is the soil's static friction times the normal force, plus the interface adhesion over the wetted footprint, plus the groove's passive resistance in the direction of the stretch (half the soil's modulus times the deepest sink squared times the skid's frontal width that way), sliding at the kinetic friction with the same additions. A ring of recent grooves SHALL let a part landing in its own prints start from their set.

#### Scenario: A sunk skid resists sideways more than along
- **WHEN** a half-skid resting 2 mm deep in dry belt_straw is pushed slowly across its axis and then along it
- **THEN** it breaks loose across at a force larger than along by the derived ratio within 2 %, and on a rigid floor (no groove) both break at μs·Fn within 1e-9 N

#### Scenario: The glide landing stumbles on soft ground
- **WHEN** the drone lands on the pilot's glide (0.5 m/s down, 0.3 m/s forward, the sticks held to the touch then cut) on the tilled field across its furrows and on the dirt track, and the same with a known-wrong model without ploughing
- **THEN** the tilled landing pitches the nose down further than the dirt track's, and without ploughing the two differ by less than half that, so it fails

#### Scenario: A re-landing finds its prints
- **WHEN** the drone lands, lifts off and lands again on the same spot of crater_spoil, and then lands 0.3 m aside
- **THEN** the second landing on the spot starts from the first's set and sinks less than the first, and the landing aside sinks as the first did within 2 %

### Requirement: Uneven ground
The ground SHALL not be the ground the pilot sees from above: the test range's zones add micro-relief, directional furrows and placed pitfalls, ruts and small craters, read through `SampleGround` from the ground function the floor is drawn from, and S04's pits lie under its draped straw. A part over a pitfall SHALL find no support until the pit's floor, furrows SHALL rock the drone across them and not along, and the log SHALL name each pitfall by its feature id and depth. Penetration of more than 0.25 m into the ground function outside a hole SHALL log `WorldBug terrain_deep`, and a support that falls 2 m below the plane over a hole with no soil shape found SHALL log `WorldBug bottomless`.

#### Scenario: A leg drops into a hole
- **WHEN** the drone lands with one skid end over a pitfall on the shelled patch, and over one of S04's hidden pits, and the same landings on a known-wrong ground without features
- **THEN** that corner passes the surrounding ground's level with no support at that point, the drone tips toward it, and the log's `PitfallEntered` names the feature's id and its depth there; without features the corner stops at the ground's level

#### Scenario: Furrows depend on heading
- **WHEN** the drone lands vertically at 20 spots of the tilled field with its skids across the furrows and at the same 20 spots along them, and the same on a known-wrong isotropic relief with the furrows' RMS height
- **THEN** across the furrows the resting pitch varies over the spots more than along them, and along them the resting roll more than across, as the ridge spacing and the skids' geometry predict; on the isotropic relief the two headings' spreads differ by less than their sampling error, so it fails

#### Scenario: Per-rotor ground effect rolls the drone over uneven ground
- **WHEN** the drone holds a liftoff hover 0.1 m up with two rotors over a furrow's crest and two over its trough, and the same over flat ground
- **THEN** the rotors' ground-effect thrusts differ by Cheeseman and Bennett's ratio at each rotor's own height within 2 %, giving a rolling moment, and over flat ground they are equal

#### Scenario: A world bug is a bug
- **WHEN** a support is driven 0.3 m into the ground function outside a hole, and a support falls through a hole band whose fillers are removed
- **THEN** the first logs `WorldBug terrain_deep` and the second `WorldBug bottomless`, each once, with the corner and the place

### Requirement: An asset's own soil shapes merge
A soil contact whose point lies inside another soil shape of the same object by more than 1 µm SHALL be buried and never act; of the remaining soil contacts on one half-skid, those whose normals lie within 45° of the deepest's SHALL merge into it, the deepest setting the surface and the depth; a soil face more than 45° off that is not buried SHALL act as its own pair.

#### Scenario: E01's buried rings never act
- **WHEN** the drone rests and lands on E01's berm over its rings and lip boxes, dry, and the same with a known-wrong model in which every soil contact acts
- **THEN** every support takes the berm's `belt_straw` surface and its depth, no `belt_bare` top sets a surface outside the cut, no sideways force comes from a buried face; the known-wrong model pushes a skid sideways or sets `belt_bare`, so it fails

#### Scenario: A real trench wall still pushes
- **WHEN** a skid is driven sideways into E01's cut wall
- **THEN** the wall's face acts as its own pair along its normal

### Requirement: Compliant layers by area, stroke and slender members
A compliant material row SHALL be applied per touching part by its area law (`mat`: the 2:1 spread at the layer's depth; `punch`: the footprint's equivalent diameter; `winkler`: the footprint's area), from the row's reference part and depth. A compliant pair SHALL compress at most 80 % of the shape's thickness along its normal, past which the layer is solid and carries the part onto the soil or rigid shape found beneath it, or onto the part's own spring when there is none. A `bark` or `deadwood` capsule with a cantilever record SHALL bend as a cantilever from its root, 3EI/x³ at the chain length x to the contact, and be rigid past ten times the part's own spring.

#### Scenario: A skid meets the straw by its area
- **WHEN** a half-skid and a 15 mm reference foot each press S04's 0.125 m straw box, and the same with a known-wrong model without the area law
- **THEN** the half-skid's stiffness is 3.03 times the foot's 490 N/m within 1 %, and without the law it is the foot's, so a resting drone sinks about three times deeper, and fails

#### Scenario: A hard landing on a mattress stops at its stroke
- **WHEN** a corner lands at 6 g on a 0.15 m `fabric` box over a rigid floor, and on a 0.125 m `straw` box over S04's soil box, and the same with a known-wrong model without the stroke limit
- **THEN** no fabric pair compresses more than 0.12 m, the straw's further sink past its 0.1 m goes into the soil by the soil's law, and without the limit the fabric pair passes 0.2 m, so it fails

#### Scenario: A limb bends from its root
- **WHEN** a skid rests on a V01 limb at chain lengths of 0.3, 0.6 and 1.0 m from its root, with a known-wrong rigid limb
- **THEN** the limb's deflection under the load equals F·x³/(3EI) within 2 % at each, and the rigid limb does not deflect, so it fails

### Requirement: Wet ground in rain
In Rain every surface SHALL take its wet set and in Severe rain its saturated set (Sunny is dry, D-021), the rain level SHALL be in the setup and the log header, and a part that has loaded wet soil SHALL be held as it lifts by a cohesive pull over its wetted footprint, falling to nothing over the release separation; wet straw SHALL be softer and cling, and wet stems and straws SHALL be tougher to break and to cut.

#### Scenario: A wet skid is held as it lifts
- **WHEN** a half-skid is pressed to its resting load in wet and saturated belt_straw and in dry belt_straw, then lifted at 10 mm/s
- **THEN** in the wet soils the pull rises to σ_a times the wetted footprint within 1 % and falls to zero by the release separation, and the work done equals ½·σ_a·δ_a times the footprint within 1 %; in the dry soil no pull acts

#### Scenario: Rain softens the ground
- **WHEN** the drone rests on each range surface dry, in Rain and in Severe rain
- **THEN** each wetter set sinks the skids deeper than the drier, and the log header names the rain level and the rows

### Requirement: Vegetation acts on the body and legs
The elements the physics reads SHALL be the ones drawn (S04's top-layer straws, standing stems and twigs; V01's and V13's crown twigs and leaves), returned by `MicroDetailNear` with stable ids and their mechanics from the surfaces table, each element's value drawn by its own id within its row's range, never by the flight. Standing stems and crown twigs SHALL push as rooted cantilevers capped at slide-off and buckling, so the body and legs feel drag and jitter in weeds, straw and crowns (D-059); where a crown has elements its `twig` shell SHALL no longer push; a twig under a skid SHALL roll across its axis; an anchored element that lies over a part at their first touch SHALL be able to hook it, pulling back toward its base until its release force, keyed by its id across cache swaps. Every force SHALL be an element's own; nothing SHALL be added to the pose. The element cache SHALL refresh on the worker at a fixed step after its request, so a late worker changes only the logged hitches.

#### Scenario: Grass jitter at touchdown
- **WHEN** the drone sets down at 0.3 m/s on S04's verge and on the dirt track, with wind and asymmetry off, and on S04 with a known-wrong model without elements
- **THEN** over the 0.3 s after the first contact the S04 touchdown's step-to-step change of the ground force is larger than the track's, the stems' forces make at least half of it, and each stem contact is named in the log by its id; without elements the stems' share is zero, so it fails

#### Scenario: Skimming weeds drags
- **WHEN** the drone flies level at 1 m/s with its skids 0.2 m into S04's standing stems
- **THEN** a drag against the motion acts on the skids, equal to the sum of the stems' cantilever forces within 1e-9 N, and no stem pushes more than its slide-off or buckling cap

#### Scenario: A crown drags and shakes the body
- **WHEN** the drone flies level at 3 m/s through the edge of V01_M's and V13_mid's crowns, and the same with a known-wrong model in which the `twig` shell's spring alone acts
- **THEN** the body meets each twig's cantilever force in turn and each twig that slides off releases its force within one step, each named by its id; the drag and the body rate's step-to-step change are printed; the shell no longer pushes; the known-wrong shell's force never changes in a step by more than its stiffness times the closing speed times the step (derived), so it has no release and fails

#### Scenario: Which side the straw lies on decides the hook
- **WHEN** the drone lands vertically at 0.3 m/s and on the pilot's glide at the same 50 spots of S04's verge, then lifts off
- **THEN** every hook attaches only to an element whose closest point lay above the part's core at their first touch; the glide landings hook more elements than the vertical ones; each hook pulls toward its element's base and lets go at its release force within 1 %, logged with the element's id, the part, the force and the cause

#### Scenario: The ground is the world's, not the flight's
- **WHEN** the same liftoff is flown from the same S04 spot on ten flight seeds
- **THEN** the same elements lie under the same skids with the same release forces every time, and every difference between the flights traces to a logged flight draw

#### Scenario: A hook survives a cache swap
- **WHEN** a skid is hooked while the cache is swapped, with the worker on time, forced early and forced late
- **THEN** the hook holds across the swap by its id, and the three flights' records and events are identical byte for byte except the late worker's hitches

### Requirement: Prop strikes on vegetation
An element (a straw, a stem, a twig or a leaf) entering a turning prop disc SHALL be struck at the first blade pass and cost the rotor its own mechanics' energy, its cut energy (the specific cut energy times its section) plus the mass the blade flings times the blade's speed squared; the rotor SHALL take that as a torque spike and an rpm dip in the strike's step, recovered by the motor model alone; the body SHALL take the opposite angular and in-plane impulse; the element SHALL stay cut for the rest of the flight; and a rotor without the energy SHALL stop on it a moment and strike it again as the motor turns it (D-059: "props getting stuck a bit"). `PropStrike` SHALL log the element's id, kind and diameter, the cut energy, the flung mass, the rotor's energy before, its speed after, the strike's radius and the blade. This change SHALL never damage a blade: each strike SHALL be written in its step to `FlightState.BladeStrikes`, per rotor and blade, for `p7f-damage` to read.

#### Scenario: A descent into weeds cuts them
- **WHEN** the drone descends at 0.3 m/s into S04's standing stems and hovers with its discs among them, and the same with a known-wrong model that logs the strikes and takes no energy
- **THEN** every element entering a disc is struck once and stays cut, each rotor's speed after a strike is √(ω² − 2E/J) within 1e-9 relative, and the known-wrong rotors' speeds are unchanged by the strikes, so it fails

#### Scenario: A straw makes the rotor dip and the drone shake
- **WHEN** a hovering drone's front-right disc meets one 3 mm straw, then a tuft of ten stems within 30 mm, with asymmetry and wind off
- **THEN** each strike's energy is E_cut + m_f·v² within 1e-9 relative, the rotor's speed dips in that step and returns to within 1 % of its hover speed over the motor's own response (about three time constants, printed), the body's yaw and roll rates change in that step by the strikes' impulses within 1e-9 relative, the tuft's dip is larger than the straw's, and the attitude disturbance each gives is printed

#### Scenario: A crown's leaves load the props
- **WHEN** the drone hovers with one disc 0.1 m inside the edge of V01_M's crown for 2 s, and the same against the known-wrong `twig` shell without elements
- **THEN** each leaflet and twig the blades meet is struck once and logged with its mass and energy, the rotor's speed shows each dip, the sum of the strikes' energies equals the rotor's energy taken within 1e-9 relative, and the known-wrong shell takes no energy from the rotor, so it fails

#### Scenario: A thick twig stops a blade
- **WHEN** a disc meets a 20 mm twig whose strike energy exceeds the rotor's energy
- **THEN** the rotor stops on it with a logged `RotorJammed`, the twig keeps its remaining cut energy, and later strikes cut it when the motor has turned the rotor up again

#### Scenario: The strike reaches the damage seam
- **WHEN** any strike happens
- **THEN** in that step `FlightState.BladeStrikes` holds, for that rotor and blade, the strike's energy, impulse, element kind and diameter, the blade's geometry and thrust are unchanged by this change, and the slot is cleared at the next step

### Requirement: The launch stand is a free body
With the legs off, the stand SHALL be a rigid body on the soil, stepped with the drone at 1 kHz: its base on the ground by the soil law, the drone's parts on its posts as steel contacts whose reactions it takes, and its pose moved in the world after each step, so every query sees it where it is. It SHALL be able to tip and to drag, and the spool SHALL clear its base.

#### Scenario: The stand rests and the spool clears
- **WHEN** the drone rests on the stand at the field start for 3 s, dry and in Severe rain
- **THEN** the stand's base sinks as its row says, every post contact is steel, the coil's lowest point stays above the base and the ground, and the stand moves less than 1 mm

#### Scenario: An uneven push tips the horizon
- **WHEN** the drone lifts off the stand on the liftoff ramp on ten seeds
- **THEN** each seed's last post to let go, its time after the first and the attitude's turn toward it are printed, the turn is toward the last post on every seed, and the stand stays upright (its tilt under 5°, assumed)

#### Scenario: A caught stand drags or tips
- **WHEN** the drone lifts off with a 2 m/s sideways drift while an arm still rests on a post, on dry soil and on saturated soil
- **THEN** the stand slides (`StandSlid`) or tips (`StandTipped`) as the arm's friction and the base's friction and moment decide, the pose the world reports follows the stand's body within 1e-12 m, and no tunnel is logged

### Requirement: Liftoff stick and shake
A leg SHALL be able to stay held as the drone lifts off, so the airframe turns about it and the horizon tips until it lets go, from physical causes only: the anchor and the groove, wet adhesion and suction, and hooks. While a leg is held the ground SHALL carry part of the weight, and at its release the drone SHALL climb faster than the pilot's input alone gives. Over many spots the rates SHALL follow the pilot's account, read as: on dry firm ground on some liftoffs (at least one spot and at most a third) with smaller tips than on soft ground; on dry soft ground more often than on firm (by more than the two rates' 95 % binomial margin) and under half; in Rain on more than half of the soft ground's liftoffs, on firm ground more often than dry, and in Severe rain at least as often as in Rain. Each target SHALL fail a known-wrong model.

#### Scenario: The pilot's account over many spots
- **WHEN** `flightbench --ground` lifts off from 100 spots of each ground class (dry firm: the dirt track; dry soft: the plane's belt_straw, the tilled field and S04's verges), in Calm and Windy, dry, in Rain and in Severe rain, half after a vertical landing and half after the pilot's glide, with the held-leg rule of design 13
- **THEN** every target above holds in each wind level, and the harness prints each cell's rate, the hold times and tips, and each hold's cause

#### Scenario: Known-wrong models fail the account
- **WHEN** the same harness runs a dice roll at the dry firm rate everywhere, the dry rows in rain, K§7.3's touch rule for hooks, and Coulomb friction without anchor, adhesion or ploughing
- **THEN** the dice roll fails "more often on soft" and "most of the time in rain", the dry rows fail "most of the time in rain", the touch rule fails "under half on dry soft ground", and the memoryless friction fails "at least one spot" and "most of the time in rain"

#### Scenario: Effective weight changes as it unsticks
- **WHEN** a liftoff with one held corner in wet belt_straw is logged
- **THEN** while the corner is held the ground's normal force is above zero and the collective at which the other corners rise is below the free hover's, and at the release the vertical acceleration jumps above what the pilot's input gives on a rigid floor at the same collective

#### Scenario: A held leg tips the horizon and lets go
- **WHEN** a held leg's liftoff is logged
- **THEN** the attitude turns toward the held corner during the hold, `LegReleased` names the corner, the hold time, the peak pull and its cause (the anchor, the groove, adhesion, or a hook's id), and the hold ends unless a hook's release force exceeds the thrust's margin, which flips the drone and is logged as such

### Requirement: The landing bounce on real ground
Holding the sticks after touchdown SHALL throw the drone back into the air where the legs' stored energy and the residual thrust exceed what the ground takes, and cutting the throttle SHALL keep it down, from the leg's compliance, the soil's set and damping and the residual thrust alone: no restitution coefficient anywhere.

#### Scenario: Hold or cut on each ground
- **WHEN** the drone lands on the pilot's glide on the dirt track, the plane's belt_straw, S04's straw and the tilled field, dry and in Rain, the sticks held through the touch, and again cut within 100 ms of it
- **THEN** with the sticks held the drone leaves the ground again on the dry dirt track and the dry plane's belt_straw, the first rebound's height is printed per ground and set, and it is lower on the tilled field than on the dirt track; with the throttle cut it never leaves the ground by more than 5 mm and rests within 0.5 s

#### Scenario: A restitution coefficient fails
- **WHEN** the same landings run on a known-wrong ground that returns a fixed share of the touch speed on every surface
- **THEN** its bounce heights are equal across the grounds within 5 %, so it fails the falling order

### Requirement: The landing's wobble
A touchdown on uneven or covered ground SHALL rock the drone from its causes (an uneven sink, a skid end over a hollow, stem pushes, the per-rotor ground effect over the relief), never from a disturbance added to the pose.

#### Scenario: Wobble on the verge, not on a level floor
- **WHEN** the drone lands vertically at 0.3 m/s with the throttle cut at the touch at 20 spots of S04's verge and of the tilled field, and on a known-wrong flat elastic ground with the same mean stiffness
- **THEN** the median over the spots of the body rate's RMS in the 0.5 s after the touch is larger on the verge and on the tilled field than on the flat ground by more than the medians' 95 % margins, and each landing's log names its causes; the flat ground's median falls below both, so it fails

### Requirement: The ground's randomness, determinism, budget and log
The only new draw SHALL be each leg's stiffness, from the named stream `leg.stiffness`, within ±10 % and logged; nothing about the ground or the cover SHALL be drawn. Every new state SHALL be preallocated (0 B in steady state), use only + − × ÷ √, replay bit for bit, and keep PLT-04's step budget; the log SHALL answer which corner, which soil, which pitfall, which element and which cause, so QA can say physics bug or pilot error.

#### Scenario: The leg draws cover their bounds
- **WHEN** the leg draws of 10 000 seeds are taken
- **THEN** every draw is inside ±10 %, both bounds are reached by the clamp, and the header of every flight lists the four draws with their bounds

#### Scenario: Replay is bit for bit in rain
- **WHEN** a 120 s flight with landings on every range ground, hooks, strikes and the stand is recorded in Severe rain and replayed headless
- **THEN** every step and event matches bit for bit, a log with one bit of its surfaces hash flipped is refused, and the log is within LOG-01's 50 MB a minute after compression

#### Scenario: The budget holds
- **WHEN** `flightbench --bench` runs on AC with the power mode printed: resting in S04's straw with the element cache, a landing into S04's stems, a hover with the props in V01_L's crown, the field stand's launch with the free stand, and a landing on the tilled field
- **THEN** every case's p99 step is at most 300 µs with 0 B allocated, and the queries' share, calls and p99 a step are printed against PLT-04's 60 µs line
