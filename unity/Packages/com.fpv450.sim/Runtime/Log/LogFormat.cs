using System;
using System.Buffers.Binary;
using System.Globalization;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Log
{
    /// <summary>A record field's type: little-endian, F64 as its IEEE 754 bits, Pad as zero bytes.</summary>
    public enum LogFieldType : byte { F64 = 1, I64 = 2, I32 = 3, U16 = 4, U8 = 5, Pad = 6 }

    /// <summary>One field of a fixed-layout record, as the log header lists it (so a reader needs no code of ours).</summary>
    public readonly struct LogField
    {
        public readonly string Name, Unit;
        public readonly LogFieldType Type;
        public readonly int Offset, Size;

        public LogField(string name, string unit, LogFieldType type, int offset, int size)
        {
            Name = name;
            Unit = unit;
            Type = type;
            Offset = offset;
            Size = size;
        }

        public bool SameAs(in LogField o) =>
            Name == o.Name && Unit == o.Unit && Type == o.Type && Offset == o.Offset && Size == o.Size;
    }

    /// <summary>
    /// The flight log's binary format (design 10, LOG-01), version 10 (1.4 appended the flight controller's state to
    /// the step record; 1.5 the wind's gust, the air's force and moment and the ESCs' current limit, and the header's
    /// aero and wind entries; 1.6 the contacts' force, moment and legs, the crash flag, and the header's contacts and
    /// runtime objects; 1.4b, after them, the controller's filter outputs and battery; p6-input-app the stick's
    /// failsafe flag and the header's input entry; p7b-airframe the rotors' angles, vortex rings, surfaces and
    /// re-ingested wash, and the header's motor asymmetry, ring, ground effect and flapping; 11: p7b-airframe 1.4 the
    /// airframe's modes, and the header's modes). One file per flight:
    /// - the prologue: Magic, then the format Version and the header's length (u32 each), then the header (LogHeader)
    /// - blocks to the end of the flight, each a u16 kind and a u16 payload length, then the payload:
    ///   - a step record (StepBlock) after every step: the integrated state, the stick sample in force during the step,
    ///     and the step's trace (StepFields, StepSize bytes)
    ///   - the step's events after its record, hitches before the steps they delayed (EventBlock, EventFields)
    ///   - the end block (EndBlock, EndFields) when the log is closed; a log without one was cut
    /// Every number is little-endian, a double as its IEEE 754 bits, so a record replays bit for bit. The layouts are
    /// fixed for a format version: a field a later task fills is zero until then, and a new field is a new version
    /// that appends it (the flight controller's state, 1.4; the contact anchors, 1.6). A body without propulsion
    /// logs zeros for the rotors, the packs and the controller.
    /// A kind a reader does not know is skipped by its length.
    /// </summary>
    public static class LogFormat
    {
        /// <summary>"FPV450LG".</summary>
        public static readonly byte[] Magic = { 0x46, 0x50, 0x56, 0x34, 0x35, 0x30, 0x4C, 0x47 };

        /// <summary>
        /// The format: the prologue, the header's entries and the record layouts below. 1–5: p6-flight-core. 6 on
        /// unity/main: the stick's failsafe flag in the step record's flags (bit 3) and the header's input entry
        /// (p6-input-app 1.1; its step-0 host time and the latency test's figures appended by 1.5, before 6 reached
        /// unity/main). 6–9 on p7b-airframe's branch: 6 the rotors' angles and the header's motor asymmetry (1.1), 7 the
        /// rotors' vortex rings and the header's ring (1.2), 8 the rotors' surfaces and re-ingested wash and the header's
        /// ground effect (1.3), 9 the header's flapping and the steady wind's onset (1.3c). 10: the two merged, the input
        /// entry at tag 15 and the airframe's entries after it. 11: the airframe's modes in the step record (p7b-airframe
        /// 1.4: each mode's coordinate and rate) and the header's modes entry. 12: the modes entry without the slings' fields
        /// (D-050: both loads on strapped mounts; their stiffness, swing damping, the bottom plate and gravity dropped).
        /// 6–9 on p7c-wind's branch: 6 the wind field's header entry and the record's wind at the body's origin (where the
        /// stand-in's gust was) and at each hub (1.4), 7 the header's wake layout (1.5), 8 the header's rain and camera and
        /// the record's rain force, film and air at the camera (1.7), 9 the header's canopy strip and the presets by name
        /// (1.5). 13: the two merged, p7c-wind's record fields before the airframe's. 14: the header's canopy strips as a
        /// count and the strips (p7c-wind, the map's wind: a map's belt has one a section), where 13 had a flag and one.
        /// 14 on p7b-airframe--rotor-wake: the rotors' wakes in the step record (p7b-airframe 1.3d: each rotor's added
        /// inflow from the others' wakes and its fluctuation) and the header's wake entry.
        /// 14 on p7b-airframe--micro (D-064): the record's rotor wakes at its end, the header's rotor wake entry, the
        /// asymmetry's motor dynamics and the recirculation's reference height.
        /// 15: the physics integration, the three 14s together: the header's canopy strips; the record's rotor-rotor
        /// interference (64 bytes after model 19's 1808) and then each rotor's own wake (32), 1904 bytes; the header's
        /// interference entry (tag 22) and own-wake entry (tag 23), the asymmetry's motor dynamics and the recirculation's
        /// reference height.
        /// 15 on p7b-airframe--power: the header's build placement (tag 22, D-066: BuildTolerance's bounds and parts; its
        /// draws with the others).
        /// 16: p7b-airframe--power's 15 merged onto the integration's 15: the build placement's header entry at tag 24, after
        ///    the interference's 22 and the own wake's 23; the record is 15's 1904 bytes.
        /// 14 on p7a-ground (off 13): p7a-ground 1.1, the contacts entry's material rows with their area law, slender
        ///    member's modulus and wet block, and surfaces.json's table and hash after its flag (the record layouts are 13's).
        /// 13–14 on p7d-tether (off p7c-wind's 13): 13 p7d-tether 1.1's header entry for the fiber's table (tether.json's
        ///    rows, labels and hash) at tag 21, which p7c-wind's rain had taken; 14 the same entry at tag 22, after the rain,
        ///    on its merge onto p7c-wind's 13.
        /// 17: p7a-ground's 14 merged onto 16: the contacts entry's material fields and surfaces.json's table with 16's
        ///    entries; the record is 16's 1904 bytes.
        /// 18: p7d-tether's 14 merged onto the integration's 16: the tether entry at tag 25, after the placement's 24; the
        ///    record is 16's 1904 bytes.
        /// 20: p7c-wind 1.10 (design 10, D-071; assigned by D-072): the wind entry's cuts after its canopy strips, a count (0
        ///    for none) and each cut's roof flag, opening at the lip, depth, ends and centre line; the record is 16's 1904
        ///    bytes.
        /// 21: p7d-tether 1.6, the line in the step: the record's tether block after the rotors' own wakes (104 bytes: the
        ///    payout, the pack's resistance payout_tension_n, the exit's tension and lip, the irregularity at the peel, the
        ///    line's force and moment on the drone, its nodes, flags, extra solves and trail), 2008 bytes; the tether entry
        ///    gains the line's launch (its coil part, its spool's mass, its layout, the two-way switch); the events Payout,
        ///    TetherWedge and WorldBug.
        /// 22: assigned to p7a-ground (unused: its 1.12 and 1.13 changed no layout; D-072).
        /// 24: the physics integration 2 (unity/physics/physics-integration-2, assigned by D-072): p7c-wind's 20, p7a-ground's
        ///    17 and p7d-tether's 21 merged onto 16: the wind entry's cuts (20's), the contacts entry's material fields and
        ///    surfaces.json's table (17's), and the tether entry at tag 25 with the line's launch, the record's tether block
        ///    and the events Payout, TetherWedge and WorldBug (21's) together; the record is 21's 2008 bytes.
        /// 19, 23, 25 and 26 on p7f-damage (off 16):
        /// 19: p7f-damage 1.1b, the header's drone internals entry (tag 25): drone.json's hash, its props.blade (the stations'
        ///    chord, thickness, section angle and fill, the hub and the blade's material) and its motors[].internals; the
        ///    records are 16's.
        /// 23: p7f-damage 1.2, the header's propulsion entry with the props' blades as elements (PropulsionSpec.Rotor: the blade's
        ///    table, the sections' lift slope, zero-lift angle and profile drag) and the asymmetry entry with the blades' pitch
        ///    bound and count (MotorAsymmetry.BladePitchBound, Blades; the draws are the randomness entry's); the records are 19's.
        /// 25: p7f-damage 1.1a, the contacts entry's material rows with what a blade meets (MaterialRow.Hardness, CrushStrength,
        ///    Density); the records are 23's.
        /// 26: p7f-damage 1.3 (D-072's number), the step record's blade strikes after the rotors' own wakes (FlightState.BladeStrikes:
        ///    per rotor and blade its kind, count, material, energy, impulse, diameter, cut energy and radius; 384 bytes, 2288 in
        ///    all), the contacts entry without the prop strike's crash speed (retired) and with the strikes' switch and parameters
        ///    (ContactSpec.BladeStrikes … FlapSouthwell), and the events BladeStrike (17), BladeDamaged (18) and RotorJammed (19,
        ///    D-074's kinds).
        /// 27–29: assigned elsewhere (D-072); 29, the buried face's, unused (model 38 changed no layout).
        /// 30: the physics integration 2 with p7f-damage (unity/physics/physics-integration-2--damage, assigned by D-072):
        ///    p7f-damage's 26 merged onto 24: the record is 24's 2008 bytes (through the fiber's block), then 26's blade strikes
        ///    (384 bytes), 2392 in all; the contacts entry's material rows carry 17's fields, then 25's what a blade meets, then
        ///    the hash and surfaces.json's table, with 26's strikes' switch and parameters and no crash speed; the tether entry
        ///    stays at tag 25 and the drone internals entry (19's tag 25) moves to tag 26; the events Payout, TetherWedge and
        ///    WorldBug (14–16, p7d-tether's) and BladeStrike, BladeDamaged and RotorJammed (17–19, p7f-damage's; 20–22 stay
        ///    reserved for p7f-damage, D-074).
        /// 31: assigned elsewhere (D-072).
        /// 32: the ESC's disarm on 24 (p6-flight-core 1.6c, model 42; assigned by D-072): each motor of the propulsion entry gains
        ///    its ESC's disarm rule (EscDisarm, an int32) after its current limit; the record is 21's 2008 bytes.
        /// 33: p7f-damage 1.1a's soil half on the physics integration 2 (unity/physics/physics-integration-2--soil-carve,
        ///    on 30; assigned by D-072): surfaces.json's table in the contacts entry gains what a blade meets: the sand's
        ///    hardness, the grains' and water's densities after the firm layer; the cohesion's wet and saturated factors
        ///    and the wet set's water after the wetting's values; and each set's cohesion, bulk density and sand share (a
        ///    soil's) and hardness, crush strength and density (a rigid set's) after its lip; the records are 30's.
        /// 34: the physics integration 2 with the ESC's disarm (unity/physics/physics-integration-2--disarm, assigned by
        ///    D-072): 32 merged onto 30: each motor of the propulsion entry carries its ESC's disarm rule (32's) after its
        ///    current limit; the drone internals entry stays at tag 26 and the tether entry at 25; the record is 30's 2392
        ///    bytes; no new event kind.
        /// 27 and 31 on p7g-legs (off 16):
        /// 27: p7g-legs 1.2 (D-072's number): the legs' state in the step record after 16's 1904 bytes (each leg's state
        ///    byte, its set, its stiffness factor and its clamp's moment, 2072 bytes), event kinds 23–27 (LegSet, LegCracked,
        ///    LegBroken, LegDetached, SkidLoose; D-074), and the header's contacts entry with the legs' side give and their
        ///    failure law's data after the materials' hash.
        /// 31: p7g-legs 1.3 (D-072's number): each corner's state in the legs' 4 reserved bytes (CornerState), then after 27's
        ///    2072 bytes each leg's side give this step, its jaws' turn and its hanging skid half's fold (2296 bytes); the side
        ///    record of the loose pieces, a LooseBlock at 50 Hz (LoosePieces.RecordSize each); event kind 28 (PieceDropped,
        ///    D-074); and the header's contacts entry with the side give's parts and the legs' skids, bores and loose pieces.
        /// 35: the soils' stones (p7f-damage 1.3's tip slide on the physics integration 2,
        ///    unity/physics/physics-integration-2--tip, on 33; assigned by D-072): each soil set of surfaces.json's table
        ///    in the contacts entry gains its stone fraction and diameter after its sand share, and the contacts entry the
        ///    tip slide's switch (ContactSpec.TipSlide) after the strikes'; the event StoneStrike (20, p7f-damage's,
        ///    D-074); the records are 30's.
        /// 36: the physics integration 2 with p7g-legs (unity/physics/physics-integration-2--legs, assigned by D-072): p7g-legs'
        ///    31 merged onto 34: the record is 34's 2392 bytes (through the blades' strikes), then 31's legs block (each leg's
        ///    state byte and its corner's, its set, its stiffness factor, its clamp's moment, its side give, its jaws' turn and its
        ///    hanging skid half's fold; 392 bytes), 2784 in all; the side record of the loose pieces (LooseBlock, kind 4, at 50
        ///    Hz); the contacts entry carries 34's fields, then 31's legs' side give, failure law, skids, bores and loose pieces;
        ///    the tether entry stays at tag 25 and the drone internals entry at 26; the events 14–16 (p7d-tether's), 17–19
        ///    (p7f-damage's; 20–22 reserved for it) and 23–28 (LegSet, LegCracked, LegBroken, LegDetached, SkidLoose,
        ///    PieceDropped: p7g-legs', D-074).
        /// 37: the fountain and the ground vortex on the line (p7d-tether 1.6b, D-075;
        ///    unity/physics/physics-integration-2--fountain, on 34; assigned by D-074): the tether entry gains the
        ///    known-wrong design 4 air's switch (FlightSetup.TetherDesign4Air, a bool) after the two-way spool's;
        ///    tether.json's rotor_air block gains its ten rows (the entry writes every row, so its layout is otherwise
        ///    34's); the record is 34's 2392 bytes; no new event kind.
        /// 38: the physics integration 2 with the tip slide and the fountain (unity/physics/physics-integration-2--int48,
        ///    assigned by D-072): 35 and 37 merged onto 36: surfaces.json's table in the contacts entry carries 33's and 35's
        ///    fields (what a blade meets, the soils' stones), the contacts entry 35's tip slide switch after the strikes' and
        ///    then 36's legs' fields, and the tether entry 37's design 4 air switch after the two-way spool's (tether.json's
        ///    rotor_air rows with it); the record is 36's 2784 bytes and the loose pieces' side record 36's; the events 14–16
        ///    (p7d-tether's), 17–20 (p7f-damage's: StoneStrike 20, 35's; 21–22 reserved for it) and 23–28 (p7g-legs').
        /// 39: p7g-legs 1.4 on the physics integration 2 (unity/physics/physics-integration-2--legs-harness, assigned by D-072):
        ///    after 36's 2784 bytes each leg's lean on its lever (leg_lean, the foot's offset across the tube; 32 bytes, 2816 in
        ///    all), and the contacts entry's legs' side give with the lever's length (ContactSpec.LegLeverLength) after the tube's
        ///    EA/L; no new event kind.
        /// 40: the physics integration 2 with the legs' lever (unity/physics/physics-integration-2--int50, assigned by D-072):
        ///    39 merged onto 38: the record is 39's 2816 bytes (38's 2784 and leg_lean), the contacts entry 38's (surfaces.json's
        ///    table with 33's and 35's fields, 35's tip slide switch, 36's legs' fields) with 39's LegLeverLength after the tube's
        ///    EA/L, the tether entry 38's (37's design 4 air switch, tether.json's rotor_air rows); the loose pieces' side record
        ///    36's; the events as 38's (14–16 p7d-tether's, 17–20 p7f-damage's, 23–28 p7g-legs'); no new event kind.
        /// 41: assigned elsewhere (D-072: p7g-legs' hanging skid half, model 51).
        /// 42: p7f-damage 1.3b on the physics integration 2 (unity/physics/physics-integration-2--props-rest, assigned by D-072):
        ///    after 40's 2816 bytes each resting prop's blades' load and deflection (blade_load_0/1, blade_bend_0/1; 128 bytes,
        ///    2944 in all); the contacts entry's shapes with each prop's blade edges (ContactRole.Blade, after every other shape)
        ///    and, after LegLeverLength, the rest's switch and speed (ContactSpec.BladeRest, BladeRestSpeed); no new event kind.
        /// </summary>
        public const int Version = 42;

        /// <summary>
        /// The flight model's version: raised with every code change that alters a step's result for the same header
        /// and inputs, when the flightbench golden is re-recorded (audited case by case), so a replay refuses a log of
        /// another model rather than report a divergence at its first step. WorldHash.QueryVersion is the world's.
        /// 1: p6-flight-core tasks 1.1–1.3 (the free rigid body).
        /// 2: task 1.4 (the flight controller and propulsion; a body without propulsion steps as in 1).
        /// 3: task 1.5 (the air: the stand-in wind, the rotors' inflow from it and their in-plane drag, the body's and
        ///    the coil's drag, the rotors' gyroscopic moment, the ESCs' current limit; a free body steps as in 1).
        /// 4: task 1.6 (the contacts: the legs on the ground, the frame's parts on the ground and the world's objects under
        ///    the Time &lt; 1 latch, friction, prop strikes, crashes; a flight that touches nothing steps as in 3), and
        ///    separately task 1.4b (the controller's gyro and D-term filter chain on the builder's profile, its 8 kHz gyro
        ///    samples, battery sag compensation; a free body steps as in 1). Two models shared the number on two branches.
        /// 5: 1.6 and 1.4b merged: the contacts and the filter chain together (a flight that touches nothing steps as 1.4b's
        ///    model 4, a flight with every filter off as 1.6's; a free body steps as in 1).
        /// 6–15 on unity/main:
        /// 6: task 1.7 (the rigid body turns at the step's midpoint rate, RigidBody.MidpointPasses, which keeps the
        ///    energy; with no passes a flight steps as in 5, and a body with isotropic inertia steps as in 1).
        /// 7: p7b-airframe 1.0 (drone.json's ESC command lag τ_esc from 5 ms, assumed, to 0.4 ms, derived; the code is
        ///    6's, so a flight with τ_esc at 5 ms steps as in 6, and a free body as in 1).
        /// 8: p7b-airframe 1.1 (the motors' asymmetry, MotorAsymmetry: each motor's Kv, each ESC's lag and each prop's
        ///    unbalance drawn per flight; the rotors' angles; a flight without asymmetry steps as in 7).
        /// 9: p7b-airframe 1.2 (the rotors' vortex rings, VortexRingSpec: the onset from the descent over the induced velocity
        ///    at the present thrust, the hysteresis, the ring's inflow and the thrust's fluctuation; a flight without the
        ///    ring steps as in 8).
        /// 10: p7b-airframe 1.3 (the rotors' ground and ceiling effect from their rays and SampleGround, and the re-ingested
        ///    wash in a close hover, GroundEffectSpec; a flight without them steps as in 9).
        /// 11: p7b-airframe 1.3c (the rotors' edgewise-flow moments, RotorFlappingSpec: each blade's flap and its hub moment,
        ///    the dissymmetry of lift and the back flapping, from blade-element theory on the props' sheet; a flight without
        ///    them steps as in 10).
        /// 12: p7b-airframe 1.5 (drone.json's packs to the P50B's 2.5 V cut-off: the OCV table re-mapped over 2.5–4.2 V with
        ///    its low knee, the voltage falling past empty; a pack on model 11's table steps as in 11).
        /// 13: p7b-airframe 1.4 (the airframe's elastic modes, AirframeModesSpec: the arms' bending and the payload's and the
        ///    coil's mounts about the rigid body, the frame's elastic turn in the gyro, each hub's elastic velocity in its
        ///    rotor's inflow; a flight without them steps as in 12).
        /// 14: D-050's simpler drone (drone.json: the coil beneath, a bottle strapped on top, the legs-off build moving
        ///    nothing; fitted to the drone model: the plates' stack, the legs, clamps and T-joints, the camera's lens) and
        ///    both loads on strapped mounts in the modes, no sling; on model 13's drone.json a flight without the modes steps
        ///    as in 13).
        /// 15: the electronics fitted between the plates (drone.json: the stack's box 0.005–0.025 m, the bottle on the upper
        ///    plate at 0.068 m, the centre of pressure re-derived; on model 14's fit a flight steps as in 14).
        /// 6–10 on p7c-wind's branch, off model 5:
        /// 6: p7c-wind 1.4 (the wind field replaces the stand-in: the log-law profile, von Kármán modes frozen and advected,
        ///    the direction's meander and shifts, per point; still and steady air step as in 5).
        /// 7: p7c-wind 1.6 (drone.json's rotor in-plane drag re-derived from blade-element theory, 0.225 → 0.044 N per m/s,
        ///    so clip P's pitch flies the published cruise; the code is model 6's, and its drone flies model 6's bits).
        /// 8: p7c-wind 1.5 (the levels' centres from the cruise, Severe 7.2, Windy 3.6, Calm 1.08 m/s; the wakes of a
        ///    setup's obstacles from 1.3's wake grid, which replace 1.2's synthetic belt; a flight without obstacles at
        ///    model 7's centres steps as in 7).
        /// 9: p7c-wind 1.7 (rain, a level of its own: the drops' momentum flux on the airframe's silhouettes and their water
        ///    film in the mass model; a Sunny flight steps as in 8).
        /// 10: p7c-wind 1.5 (a wooded strip's canopy flow, CanopyFlow: its mean profile, its σ and its own eddies; a flight
        ///    without a canopy strip steps as in 9).
        /// 16: p6-flight-core 1.6b on its range branch, off model 15 (a skid capsule on the face its support spheres stand
        ///    on, or on a piece coplanar with it, is their support; a sphere's contact on a seam of coplanar shapes is the
        ///    face's, ContactModel.Seam; leaving a compliant shape is no tunnel; a flight on the stub's floor steps as in 15).
        /// 17: p7c-wind and unity/main merged (main's model 15 with p7c-wind's field, wakes, canopy and rain and its
        ///    derived rotor in-plane drag, 0.044 N per m/s, over D-050's silhouette; the steady wind's onset carried into
        ///    the field; the camera's lens one point, drone.json's camera.lens_m, for the modes and the air at the lens).
        /// 18: p7c-wind 1.5's re-grade on the merge (Severe's centre tuned until T0 passes, 7.2 → 9.5 m/s at 35 m; Windy 4.75
        ///    and Calm 1.425 from it; a flight at model 17's centres steps as in 17).
        /// 19: 1.6b's range branch and unity/main merged (model 18's wind field, wakes, canopy and rain with model 16's
        ///    contacts: the skids on soil faces, the seam rule, no tunnel out of a compliant shape; the code is the two
        ///    parents' together, so a flight on the stub's floor steps as in 18).
        /// 20: the wind over a hole (FlightWorld.SoilUnder): where SampleGround reports Hole the field's profile, roughness and
        ///    wakes read the soil a ray straight down meets (S04's straw and soil, E01's fillers), not a sheltered −∞; a
        ///    flight that never flies over a hole, or flies in still or steady air, steps as in 19).
        /// 21: p7c-wind--d060 merged onto 20 (D-060's open field, p7c-wind 1.5, which that branch logged as 19): z0 from the
        ///    landscape's class (Davenport–Wieringa class 4, 0.10 m, not the stubble's 0.03 m) and σ_u/U derived from it (0.164
        ///    for 0.135), and the turbulence carried to the discs' 0.5 m (138 open modes, 71 canopy modes; the 1 m cut had 128
        ///    and 64), with 20's wind over a hole; a flight with z0 0.03 m, σ_u/U 0.135 and the 1 m cut steps as in 20, and a
        ///    flight on the stub's floor as on p7c-wind--d060's 19.
        /// 21 on p7c-wind--map-wakes (off 19, before 20's hole rule and D-060's field): the map's wind (MapWind,
        ///    WindSetup.ForMap): canopy strips together with wakes, each point shared between a strip's canopy (its plan weight
        ///    w) and the wake grid (1 − w), and several strips sharing wind.canopy's draws; a flight with strips or wakes alone
        ///    steps as in 19, and one with neither too.
        /// 22: p7c-wind--map-wakes merged onto 21 (the code is the parents' together): the map's wind with 20's wind over a
        ///    hole and D-060's field; a flight with strips or wakes alone, or neither, steps as in 21.
        /// 19–21 on p7b-airframe--rotor-wake (off p7c-wind--d060's 19, D-060's open field): 20 left to the range branch; 21
        ///    p7b-airframe 1.3d (the rotors in each other's wake, RotorWakeSpec there: Coleman's skewed vortex cylinder behind
        ///    each rotor, its downwash on the rotors behind and its upwash beside and ahead, and its own fluctuation from the
        ///    stream wake.rotor; a flight without the wake steps as in 19).
        /// 19–23 on p7b-airframe--micro (off p7c-wind--d060's 19): 20–22 left to the other lines; 23 D-064's micro-motion
        ///    (p7b-airframe 1.10: each rotor's own wake unsteadiness, RotorWakeSpec, from the stream rotor.wake; the re-ingested
        ///    wash on published numbers, Dekker et al. 2023's onset and loop and Healy et al. 2021's level, answering in the
        ///    torque too; and each motor's resistance, friction and inertia drawn per flight; a flight without the wake, with
        ///    the motors' new spreads at 0 and model 19's recirculation steps as in 19). p7a-ground's branch also took 23.
        /// 24: the physics integration: p7b-airframe--rotor-wake's 21 and p7b-airframe--micro's 23 merged onto 22 (the code
        ///    is the parents' together): the rotor-rotor interference (RotorInterferenceSpec, its fluctuation from the stream
        ///    rotor.interference, renamed from wake.rotor) and D-064's micro-motion with the map's wind. A flight without the
        ///    interference steps as on the micro branch's 23 with 22's wind; one without D-064 (no own wake, the motors'
        ///    spreads at 0, model 19's recirculation) steps as in 22 with the interference; one without either as in 22.
        /// 23 on p6-flight-core--landing (off 22): 1.6c (D-067): the friction's stick spring stretches with the touching
        ///    material's slip (its point velocity on the contact's tangent plane over the step), not with the contact point's
        ///    move, so a rounded part rolls where it rolled as slip before: the coil rocks about its contact instead of pivoting
        ///    on its axis; a flight that touches nothing steps as in 22.
        /// 25: p6-flight-core--landing's 23 merged onto 24 (the code is the parents' together): 1.6c's stick spring under the
        ///    interference and D-064's micro-motion. A flight with the stick anchor at the contact point steps as in 24; one
        ///    that touches nothing steps as in 24 too.
        /// 23–24 on p7b-airframe--power (off 22): 23 D-066, the drone's power and mass from published builds of its class
        ///    (drone.json: the class's ESC, which senses no current, so no current limit, "none", for the assumed 40 A; the
        ///    motors' published 1.4 A no-load current for the assumed 1.0; the packs 0.92 → 0.88 kg and the electronics 0.25 →
        ///    0.24 kg from published cells, packs and the stack); on model 22's drone a flight steps as in 22. 24 the build's
        ///    placement (BuildTolerance, PHY-24): the bottle, the packs, the coil and the leads where the pilot's hands set them
        ///    this flight, drawn from "build.placement", so the centre of mass sits a few millimetres off the exact build's; a
        ///    flight without it steps as in 23.
        /// 26: p7b-airframe--power's 24 merged onto 25 (the code is the parents' together): D-066's power and mass and the
        ///    build's placement with the interference, D-064's micro-motion and 1.6c's stick spring. On model 22's drone (the
        ///    40 A limit, 1.0 A no-load, 0.92 kg packs, 0.25 kg electronics) and without the placement a flight steps as in 25.
        /// 19–22 on p7a-ground (off 18): taken by the lines above. 23 p7a-ground 1.10 (the soil under the half-skids:
        ///    Winkler's elasto-plastic law with Wong's rule per column, in series with the leg, the groove's anchor and the
        ///    footprint ring, surfaces.json's sets at the flight's rain; the surface's own friction on the ground; a flight
        ///    without surfaces.json steps as in 18).
        /// 27: p7a-ground's 23 merged onto 26 (the code is the parents' together): 1.10's soil under 1.6b's seam rule, the
        ///    landing's stick spring (D-067), the interference, D-064's micro-motion and D-066's power, mass and placement.
        ///    A flight without surfaces.json steps as in 26.
        /// 28: p7d-tether (on model 18: 1.1's fiber table in the header only, 1.5's cable chain outside the step) merged onto
        ///    26 (the code is the parents' together), with 1.1's fdlibm DetMath.Atan2 in place of the line's series one in
        ///    the map's wind (MapWind's belt bearing, MapWorldWind's volume yaw). A flight steps as in 26 wherever the two
        ///    give the same bits; on the test range every call does (815 in worldbench's goldens, 817 in flightbench's, none
        ///    differing), so every golden of 26 holds.
        /// 30: p7c-wind 1.10, the wind inside a cut (design 10, D-071; assigned by D-072; Cavity, CutFrame, WindSetup.Cuts,
        ///    MapWind.Cuts): where the body's origin lies in a cut's opening, the field takes the lip as its ground above it,
        ///    an open cut's own cavity flow below it (the field's mean at h_o over the lip across and along the cut, one 2-D
        ///    cavity each by its length over its depth, the field's fluctuation × τ·Λ), and still air under a covered cut's
        ///    roof, in place of model 20's open profile restarted at the floor. A flight that never enters a cut's opening,
        ///    or flies without cuts or in still or steady air, steps as in 26.
        ///    Changed on its branch before any landing (D-072 keeps 30): the opening is the world's own, the origin within
        ///    the cut's reach (W/2 + CutFrame.OpeningReach) with the ground under it below the lip, in place of the strip
        ///    within W/2 of the centre line; and D is the cut's own depth at its centre line, never the lip less the soil
        ///    under the origin.
        /// 31: p7d-tether 1.6 (on 28): the fiber in the step (FiberLine): the coil paying out one way through the exit by the
        ///    pack's law over the lip's capstan, the span and its laid trail, the line's pull at the exit, the coil's mass and
        ///    the airframe's coil mode rebuilt each 10 m of payout (AirframeModes.Rebuild), drag by Reynolds number, the wind
        ///    field, the rotors' air and rain's film on the line. A flight without the line (FlightSetup.Tether null) steps as in
        ///    28: every golden flown without it is model 28's bit for bit over the first 1904 bytes, the tether block zero.
        /// 32: p7a-ground 1.13 (wet ground: a wet groove's cohesive pull on its half-skid as it lifts, Soil.Cohesion, in the
        ///    leg's series solve as a tension; wet straw's row, softer, gripping harder and clinging; the cover kinds' wet
        ///    sets for the elements). A Sunny flight, and any flight without surfaces.json, steps as in 27.
        /// 33: p7a-ground 1.12's compliant layers (design 6): a layer row (straw, fabric, sandbag) meets each touching part by
        ///    its footprint, by the row's area law, and compresses at most Layer.StrokeShare of its thickness, past which it is
        ///    solid and carries the half-skid onto its own object's soil beneath, or leaves the part's own spring. A flight
        ///    that touches no layer steps as in 32.
        /// 34: the physics integration 2 (unity/physics/physics-integration-2, assigned by D-072; the code is the parents'
        ///    together): p7c-wind's 30 (the wind inside a cut), p7a-ground's 33 (the soil under a part, wet ground and the
        ///    compliant layers' area laws and stroke) and p7d-tether's 31 (the fiber line in the step, with 28's fdlibm
        ///    DetMath.Atan2) merged onto 26. Each parent's mechanisms act only where the flight meets them: one that never
        ///    enters a cut's opening, or flies without cuts, has no cavity; one without surfaces.json no soil and no wet
        ///    ground; one that touches no layer row no area law or stroke; one without the line (FlightSetup.Tether null) no
        ///    line, its record block zero. With none of them a flight steps as in 28 (as in 26 on the test range, where the
        ///    two Atan2 forms give the same bits).
        /// 36: Betaflight's yaw I (on 34; reference-notes/rate-tracking.md §6): the controller multiplies yaw's Ki by 2.5 after
        ///    the scaling, as pid_init.c's pidInitConfig does without integrated yaw. A flight on i_yaw just under 12, whose
        ///    Ki is 34's ITERM_SCALE × 30 bit for bit (GoldenFlights.Model34YawI), steps as in 34; the record is unchanged.
        /// 37: assigned to p7g-legs (D-072; below).
        /// 38: the buried face (on 36; ContactModel.Buried): a layer's contact (a row with an area law) whose point lies inside
        ///    another shape of the same object and the same material is a face between two pieces of one layer, and carries
        ///    nothing (S04's overlapping straw tiles: a part sunk in one lay inside a neighbour's side face, and the legs took
        ///    that face's horizontal normal for a contact). A flight whose parts never meet such a face, and any flight
        ///    without the layers' area laws, steps as in 36; the record is unchanged.
        /// 29 and 35 on p7f-damage (off 26):
        /// 29: p7f-damage 1.2 (D-072's number): the damaged rotor (DamagedRotor: blade-element momentum theory on what is left of
        ///    each blade, the thrust and torque through η, the rotor's J less the material lost, the lost material's pull, the
        ///    blades' unequal lift and drag and the flap sets' couple at each sub-step) and each blade's factory pitch offset
        ///    (prop.blade_pitch, ±0.25°), with the flapping's blade inertia from props.blade's table (2.12e-5 kg·m², not the
        ///    uniform rod's 4.3e-5). A flight on a drone.json without props.blade (no rotor spec, no pitch draws, the rod)
        ///    steps as in 26; so does one with the pitch bound at 0 and the flapping's inertia at the rod.
        /// 35: p7f-damage 1.3 (D-072's number): the blades against the shapes their discs cut (PropStrikes: the pass at the nearest
        ///    point of the shape's cut by the disc's slab, BladeCut; who yields by hardness, the hard face's crush, break, bend and
        ///    wear, the shape inside the rim's sever or break, the soft face's carve; the pass's work off the rotor, its stop on
        ///    the face, the body's reaction, tangential impulse and push), each blade's strength drawn from prop.blade_strength,
        ///    the disc's pairs while their shapes cut its slab within the rim, and no crash by a prop strike (ContactSpec's 3 m/s
        ///    retired). A flight whose discs cut nothing steps as in 29; one with the strikes off (ContactSpec.BladeStrikes) as in
        ///    29 but for the crash and the pairs the slab keeps.
        /// 39: assigned to p7g-legs (D-072; below).
        /// 40: the physics integration 2 with p7f-damage (unity/physics/physics-integration-2--damage, assigned by D-072; the code
        ///    is the parents' together): p7f-damage's 35 (the damaged rotor, the blades' factory pitch and the flapping's blade
        ///    inertia from props.blade, 29's; the blades' strikes on the shapes their discs cut, the strength draws and the prop
        ///    crash retired, 35's) merged onto 38 (the wind inside a cut, the soil, wet ground, the layers' area laws and stroke,
        ///    the buried face, the fiber line, Betaflight's yaw I). A flight on a drone.json without props.blade steps as in 38;
        ///    one without the line's mechanisms (no surfaces.json, no area laws, no line, model 34's yaw I; no cut entered)
        ///    steps as in 35. A blade strike on surfaces.json's soil is still a Glance (p7f-damage 1.1a's soil half and 1.3's
        ///    carve against its rows are open).
        /// 41: assigned to p7g-legs (D-072; below).
        /// 42: the ESC's disarm (on 38; p6-flight-core 1.6c, Propulsion, EscDisarm): disarmed, the class's BLHeli_S stays in its
        ///    run loop at zero power with damped light's complementary PWM, so the motor brakes as the DC model at d = 0 (i =
        ///    −Ke·ω/R, no supply current) until about 1220 eRPM, then coasts; Bluejay alike to about 1330 eRPM; brake on stop
        ///    then shorts the three phases. A motor whose spec names no rule (EscDisarm.Freewheel) coasts at once as in 38, and
        ///    a flight that never disarms with its rotors turning steps as in 38; the record is unchanged.
        /// 43: the blades on the ground (p7f-damage 1.1a's soil half and 1.3's soil carve on the physics integration 2,
        ///    unity/physics/physics-integration-2--soil-carve, on 40; assigned by D-072): with surfaces.json a disc's pass
        ///    over the ground or a soil shape meets its surface's set at the flight's moisture: a soil is carved at c·N_c
        ///    (Prandtl's 2 + π) with its bulk density's fling, its sand wearing the leading edge by Archard in the severe
        ///    class (the pass's share, and a jammed carve's grind by the work it pays, put on at its end); a rigid surface
        ///    is a face by its own hardness (asphalt's aggregate, the slab's concrete: hard targets). A flight whose discs
        ///    never cut the ground or a soil shape, or any flight without surfaces.json, steps as in 40.
        /// 44: the physics integration 2 with the ESC's disarm (unity/physics/physics-integration-2--disarm, assigned by
        ///    D-072; the code is the parents' together): 42's disarm merged onto 40. Disarmed, every ESC keeps its firmware's
        ///    rule whatever its blades: a damaged rotor (40's DamagedRotor) brakes by the same short, i = −Ke·ω/R, against its
        ///    own J less the material lost and its loads through η, and a strike that slows it below the run loop's minimum
        ///    speed lets its motor coast as the firmware does. A flight that never disarms with its rotors turning steps as
        ///    in 40, and so does one whose motors name no rule (EscDisarm.Freewheel); one on a drone.json without props.blade
        ///    steps as in 42. 42's landing harness (the steady approach, a touch judged alone) is test code. The record is
        ///    40's.
        /// 37, 39 and 41 on p7g-legs (off 26):
        /// 37: p7g-legs 1.1 (D-072's number), the class's legs as data (drone.json's legs block): rigid PVC-U legs and skids,
        ///    10/8 mm (10.2 and 18.3 g, from 11 and 20 g of CFRP), each leg's spring from its section, 52.3 kN/m along it (the
        ///    tube's EA/L, the arm under the clamp, the skid's ring in the T-joint, in series) for the assumed 10 kN/m, its damper
        ///    103 N·s/m at the same ζ 0.2 (from 45); the leg shapes keep 6600 N/m. On model 26's drone.json a flight steps as in
        ///    26.
        /// 39: p7g-legs 1.2 (D-072's number): each leg a spring along its tube and across it at the foot (its side give from
        ///    the section, 262 N/m, in series with the stick anchor; its damper at the legs' ζ), so a gripping foot loads the
        ///    leg through the tube and a tilted leg is a strut (ContactSpec.LegSideStiffness); and the legs' failure law
        ///    (LegDamage: the clamp's slip and twist, buckling, the edge's crack and set, parting by strain, the T-joint), each
        ///    leg's strengths drawn from leg.strength.&lt;leg&gt;. Without the side give and the law (LegSideStiffness 0, Legs null)
        ///    a flight steps as in 37; with the side give alone, as with the law until a leg passes a capacity.
        /// 41: p7g-legs 1.3 (D-072's number), a damaged leg changes the drone: a set turns the leg's shapes and its skid half about
        ///    its clamp's edge (its held feet's anchors carried with them), a crack's factor flies on the tube's stiffnesses, a
        ///    broken leg keeps its stub, a parted or detached piece leaves the contacts and the build (rebuilt exactly, the
        ///    airframe's modes solved again) and flies loose, and a skid half whose leg let go hangs from the other T-joint on
        ///    its cantilever, folding past its first yield; the axial load softens the side give (P·Δ, 12P/(π²L), never below
        ///    0) and the clamp's jaws hold it by their friction (μ·P·D), their bed turning only as it slips. On 1.2's chain
        ///    (no side give's parts) and with no leg past a capacity, a flight steps as in 39 but for the T-joint's socket
        ///    moment's rounding.
        /// 45: the tip slide (p7f-damage 1.3 on the physics integration 2, unity/physics/physics-integration-2--tip, on 43;
        ///    assigned by D-072): a carve meets the face at the blade's speed through it, its own and the body's there, so
        ///    the body's motion loads or unloads the pass (the attack, the frontal height, the swept volume and the fling)
        ///    and pays its share, and the pass's axial push is its leading face's, c·cos θ/h, not cot θ of the whole drag;
        ///    a jammed carve's held blade ploughs the soil as the body moves it (Coulomb's clamp) and breaks where its held
        ///    load passes a section's strength, and logs a BladeStrike only on a step that pays the rotor's work or breaks
        ///    it; the disc's part inside a carved face makes no thrust or air torque (its blades' shares,
        ///    DamagedRotor.Immersed, from the next step); a soil pass meets a stone with the probability its swept volume
        ///    and the soil's stone fraction give ("soil.stones"), a hard target by quartz's hardness that breaks or notches
        ///    the leading edge by 1.3's head-on rule, held to the stone's mass (StoneStrike, 20). A flight whose discs
        ///    never cut a softer face steps as in 43.
        /// 46: the physics integration 2 with p7g-legs (unity/physics/physics-integration-2--legs, assigned by D-072; the code is
        ///    the parents' together): p7g-legs' 41 (the PVC-U legs from their section, 37's; the side give in series with the
        ///    stick anchor and the failure law, 39's; a damaged leg's geometry, stiffness, stub and loose pieces, the exact
        ///    rebuild, P·Δ and the jaws' friction, 41's) merged onto 44 (the wind inside a cut, the soil, wet ground, the layers'
        ///    area laws and stroke, the buried face, the fiber line, Betaflight's yaw I, the blades' strikes, the ESC's disarm).
        ///    On surfaces.json's soil the legs' side give and law act too (ContactModel.SoilTrial): the leg's push on its
        ///    half-skid's soil is the series solve's along the soil's normal with the leg's own stiffness that way (its tube's
        ///    along and its side give across, by its tilt), the groove's anchor across in series with the side give, the force
        ///    at the foot, judged by the law, its relief taken off; a set carries the half-skid's nodes and its anchor with the
        ///    turned shapes. A hanging skid half meets soil as its points on the soil's rigid surface. On model 26's legs
        ///    (drone.json without legs.tube, clamp and tjoint, the 10 kN/m spring) a flight steps as in 44; with the line's
        ///    mechanisms off (no surfaces.json, no area laws, no line, no props.blade, model 34's yaw I, the ESCs freewheeling)
        ///    as in 41.
        /// 47: the fountain and the ground vortex on the line (p7d-tether 1.6b, D-075,
        ///    unity/physics/physics-integration-2--fountain, on 44; RotorAir): where two rotors' wall jets meet, their
        ///    normal momentum (fountain_momentum_ratio of it) rises as a self-similar plane jet from the ground to the
        ///    discs' plane, fading out of the hover-like regime; each landing wake in a stream along the ground rolls a
        ///    ground vortex ahead of it (a straight Scully vortex and its image, placed and sized from Wolf et al. 2022's
        ///    flight test), unless another rotor's landing shadows it. Only the line's air changes: a flight without the
        ///    line steps as in 44, and so does one with it on design 4's field (FlightSetup.TetherDesign4Air).
        /// 48: the physics integration 2 with the tip slide and the fountain (unity/physics/physics-integration-2--int48,
        ///    assigned by D-072; the code is the parents' together): 45's blades on the ground (43's carve, 45's tip slide and
        ///    stones) and 47's fountain and ground vortex merged onto 46. With surfaces.json a disc that cuts the ground or a
        ///    soil shape carves it (it glanced in 46); a disarmed rotor in the soil brakes by its ESC's rule (44's) against the
        ///    carve, and a carve's body push and plough reach the legs' contacts (46's); the line's air has 47's fountain and
        ///    ground vortex, on the legs' liftoff. A flight whose discs never cut a softer face and that flies no line, or flies
        ///    it on design 4's field (FlightSetup.TetherDesign4Air), steps as in 46; with the tip slide off
        ///    (ContactSpec.TipSlide) a carve is 43's; without surfaces.json every pass on the ground glances (40's).
        /// 49: p7g-legs 1.4 (on 46; the physics integration 2's legs harness, assigned by D-072): the leg across its tube as a
        ///    lever (ContactSpec.LegLeverLength, the pseudo-rigid-body model's link of π²L/12 from its pivot to the foot, its
        ///    torsional spring keeping 1.1's 3EI/L³), so the axial load's softening is the link's statics at each point's push this
        ///    step, in place of 1.3's linear k_t·(1 − P/P_sway) on the step before's load clamped at 0: past the sway load a leg's
        ///    give across is negative (it vaults the body over its held foot) and at a large lean it stiffens; a leaning foot
        ///    rises on the link's circle, which unloads the leg's axial spring; a set leg's lean is measured from its own turned
        ///    axis. On the soil the foot's rise is an axial rest of the series solve and the lever's secant its give across. With
        ///    LegLeverLength 0 a flight steps as in 46 over its 2784 bytes, the lean 0; on model 26's legs (no tube) as in 44.
        /// 50: the physics integration 2 with the legs' lever (unity/physics/physics-integration-2--int50, assigned by D-072; the
        ///    code is the parents' together): 49's lever merged onto 48. The legs' side give is the lever's on 48's line (the tip
        ///    slide's carve, its stones and the fountain's air), so a flight whose legs touch steps as in 49 where its discs never
        ///    cut a softer face and it flies no line (or flies it on design 4's field); with LegLeverLength 0 a flight steps as in
        ///    48 over its 2784 bytes, the lean 0; a flight whose legs never touch the ground steps as in 48.
        /// 51: assigned elsewhere (D-072: p7g-legs' hanging skid half).
        /// 52: p7f-damage 1.3b, props are parts at rest (on 50; unity/physics/physics-integration-2--props-rest, assigned by
        ///    D-072): a rotor turning slower than ContactSpec.BladeRestSpeed carries its blades as contact shapes at their angle
        ///    (each blade's edges along its span segments, ContactRole.Blade), bearing load through the contact model on the
        ///    blade's bending in series with its motor's mount, their moment about the shaft turning the rotor (Propulsion's
        ///    shaft load, implicit), their sections bending or breaking by design 5's laws (PropStrikes.RestDamage); a resting
        ///    rotor strikes nothing, a faster one strikes as at 50; a jammed head-on crush inside the rim notches the leading edge
        ///    by its share of the chord (it trimmed the tip by it at 50). A flight whose props never rest on anything steps as in
        ///    50 over its 2816 bytes, the new block zero; with ContactSpec.BladeRest off, as in 50 but for a jammed head-on crush.
        /// </summary>
        public const int ModelVersion = 52;

        public const ushort StepBlock = 1, EventBlock = 2, EndBlock = 3;

        /// <summary>The side record (p7g-legs 1.3): one loose piece's state after a step whose index is a multiple of LooseSteps,
        /// after that step's record and events, a block a flying or resting piece (LoosePieces.Encode, LooseFields).</summary>
        public const ushort LooseBlock = 4;

        /// <summary>The side record's period, steps: 50 Hz.</summary>
        public const int LooseSteps = 20;

        /// <summary>A block's kind and payload length, u16 each.</summary>
        public const int BlockHeaderSize = 4;

        /// <summary>The step record: 352 B of 1.1–1.3's fields, then the flight controller's 69 doubles and flags (1.4),
        /// then the wind at the body's origin, the air's force and moment and the ESCs' limit (1.5; the wind, p7c-wind
        /// 1.4, where the stand-in's gust was), then the contacts' force and moment, the legs' loads and compressions and
        /// the touching pairs (1.6), then the controller's filter outputs and battery (1.4b), then the wind at each hub
        /// (p7c-wind 1.4), then the rain's force and film and the air at the camera (p7c-wind 1.7), then the rotors' angles
        /// (p7b-airframe 1.1), then the vortex rings (1.2), then the rotors' surfaces and re-ingested wash (1.3), then the
        /// airframe's modes (1.4), then the rotors' interference (1.3d), then each rotor's own wake (D-064), then the fiber's
        /// line (p7d-tether 1.6), then the blades' strikes (p7f-damage 1.3), then the legs' state (p7g-legs 1.2), then each
        /// leg's side give, jaws and hanging fold (1.3), then each leg's lean (1.4), then the resting blades' loads and
        /// deflections (p7f-damage 1.3b).</summary>
        public const int StepSize = 2944, EventSize = 72, EndSize = 48;

        /// <summary>The record before the resting blades' loads (models 49–50's, formats 39–40: through the legs' lean), over which a
        /// flight whose props never rest on anything keeps model 50's bits.</summary>
        public const int Model50StepSize = 2816;

        /// <summary>The record before the fiber's block (model 28's, format 18), over which a flight without the line keeps
        /// model 28's bits; it is also p7f-damage's model 29 record, before the blades' strikes, and p7g-legs' model 37 record,
        /// before the legs' block.</summary>
        public const int Model28StepSize = 1904;

        /// <summary>The record before the blades' strikes (models 31–38's, formats 21–24: through the fiber's block), over which
        /// a flight without props.blade keeps model 38's bits.</summary>
        public const int Model38StepSize = 2008;

        /// <summary>The record before the legs' block (models 40–44's, formats 30–34: through the blades' strikes), over which a
        /// flight on model 26's legs keeps model 44's bits.</summary>
        public const int Model44StepSize = 2392;

        /// <summary>The record before the legs' lean (models 46's, format 36: through the legs' block), over which a flight with
        /// the lever off keeps model 46's bits.</summary>
        public const int Model46StepSize = 2784;

        /// <summary>The legs' block (p7g-legs 1.2 and 1.3: its 2296 bytes less model 26's 1904), at Model44StepSize here and at
        /// Model28StepSize in p7g-legs' format 31.</summary>
        public const int LegBlockSize = 392;

        // What the step record covers, checked against the structs by the suite: a field added to one of them must be
        // logged (a new layout) or the suite fails.
        public static readonly string[] StateFieldsLogged =
            { "Step", "Position", "Velocity", "Attitude", "BodyRate", "Armed", "RotorSpeed", "EscCommand", "PackCharge0", "PackCharge1", "Fc", "EscLimited", "Crashed", "RotorAngle",
              "RingInflow", "RingThrust", "RingLatched", "WashInflow", "ModeShape", "ModeRate", "InterferenceShare", "WakeInflow", "BladeStrikes" };
        public static readonly string[] FcFieldsLogged =
        {
            "RcCommand", "RcThrottle", "RcDataThrottle", "RawSetpoint", "FfDeltaPrev", "FfSpeedPrev", "FfAccelPrev", "FfRaw",
            "Setpoint1", "Setpoint2", "Setpoint", "Throttle1", "Throttle2", "Throttle", "Ff1", "Ff2", "Ff", "ITerm", "GyroPrev",
            "RelaxLpf", "DMinRange1", "DMinRange", "DMinLpf1", "DMinLpf", "YawP", "BoostLpf", "AntiGravity1", "AntiGravity",
            "ThrottlePrev", "TpaFactor", "MotorMixRange", "Flags", "GyroFiltered", "DtermGyro", "DynNotch1", "DynNotch2",
            "DynNotch3", "DynNotch4", "DynNotch5", "DynLpfThrottle", "DynLpfLoops", "SagVolts", "Cells",
        };
        public static readonly string[] StickFieldsLogged = { "HostUs", "Roll", "Pitch", "Yaw", "Throttle", "Arm", "Failsafe" };
        public static readonly string[] TraceFieldsLogged =
            {
                "RotorCommand", "PackVolts0", "PackVolts1", "PackAmps0", "PackAmps1", "NetForce", "NetMoment", "AeroForce", "AeroMoment",
                "ContactForce", "ContactMoment", "LegLoad", "LegCompression", "ContactsTouching", "Wind", "RotorWind",
                "RainForce", "FilmMass", "CameraAir", "GroundDistance", "CeilingDistance", "InterferenceInflow", "Tether",
                "LegState", "LegSet", "LegStiffnessFactor", "LegMoment", "CornerState", "LegSideGive", "LegJaw", "SkidFold", "LegLean",
                "BladeLoad0", "BladeLoad1", "BladeBend0", "BladeBend1",
                "Loose", // the side record's (LooseBlock)
            };
        public static readonly string[] TetherTraceFieldsLogged =
        {
            "PaidOut", "PayoutSpeed", "PayoutTension", "ExitTension", "LipAngle", "Irregularity", "Force", "Moment", "Nodes", "Trail",
            "Flags", "Corrections",
        };

        static readonly string[] CornerNames = { "rear_right", "front_right", "rear_left", "front_left" };

        public static readonly LogField[] StepFields = BuildStepFields();
        public static readonly LogField[] EventFields = BuildEventFields();
        public static readonly LogField[] EndFields = BuildEndFields();
        public static readonly LogField[] LooseFields = BuildLooseFields();

        // After the tables: static fields initialise in this order.
        static readonly int StickAt = OffsetOf(StepFields, "stick.host_us"), FlagsAt = OffsetOf(StepFields, "flags");

        static int OffsetOf(LogField[] fields, string name)
        {
            foreach (LogField f in fields)
                if (f.Name == name)
                    return f.Offset;
            throw new InvalidOperationException("no field " + name);
        }

        static LogField[] BuildStepFields()
        {
            var b = new Layout();
            b.Add("step", "steps since the start; the state's time is step x 1 ms", LogFieldType.I64);
            b.Vec("position", "m, world");
            b.Vec("velocity", "m/s, world");
            b.Add("attitude.x", "body to world", LogFieldType.F64);
            b.Add("attitude.y", "body to world", LogFieldType.F64);
            b.Add("attitude.z", "body to world", LogFieldType.F64);
            b.Add("attitude.w", "body to world", LogFieldType.F64);
            b.Vec("body_rate", "rad/s, body");
            b.Corners("rotor_speed", "rad/s");
            b.Corners("esc_command", "0..1");
            b.Add("pack_charge0", "0..1", LogFieldType.F64);
            b.Add("pack_charge1", "0..1", LogFieldType.F64);
            b.Add("stick.host_us", "us, host clock", LogFieldType.I64);
            b.Add("stick.roll", "-1..1, +1 right", LogFieldType.F64);
            b.Add("stick.pitch", "-1..1, +1 forward", LogFieldType.F64);
            b.Add("stick.yaw", "-1..1, +1 right", LogFieldType.F64);
            b.Add("stick.throttle", "0..1", LogFieldType.F64);
            b.Corners("rotor_command", "0..1");
            b.Add("pack_volts0", "V", LogFieldType.F64);
            b.Add("pack_volts1", "V", LogFieldType.F64);
            b.Add("pack_amps0", "A", LogFieldType.F64);
            b.Add("pack_amps1", "A", LogFieldType.F64);
            b.Vec("net_force", "N, world, weight included");
            b.Vec("net_moment", "N.m, body, about the CoM");
            b.Add("flags", "bit 0 armed, bit 1 stick.arm, bit 2 crashed, bit 3 stick.failsafe", LogFieldType.U8);
            b.Add("reserved", "zero", LogFieldType.Pad, 7);
            // The flight controller's state (FcState), roll, pitch and yaw in the pilot's directions.
            b.Axes("fc.rc_command", "us from the stick centre after the deadband");
            b.Add("fc.rc_throttle", "us, rcCommand[THROTTLE] from the curve", LogFieldType.F64);
            b.Add("fc.rc_data_throttle", "us, the throttle channel", LogFieldType.F64);
            b.Axes("fc.raw_setpoint", "deg/s");
            b.Axes("fc.ff_delta_prev", "us");
            b.Axes("fc.ff_speed_prev", "deg/s2");
            b.Axes("fc.ff_accel_prev", "deg/s2");
            b.Axes("fc.ff_raw", "deg/s2");
            b.Axes("fc.setpoint1", "deg/s, PT3 stage");
            b.Axes("fc.setpoint2", "deg/s, PT3 stage");
            b.Axes("fc.setpoint", "deg/s, smoothed");
            b.Add("fc.throttle1", "us, PT3 stage", LogFieldType.F64);
            b.Add("fc.throttle2", "us, PT3 stage", LogFieldType.F64);
            b.Add("fc.throttle", "us, smoothed", LogFieldType.F64);
            b.Axes("fc.ff1", "deg/s2, PT3 stage");
            b.Axes("fc.ff2", "deg/s2, PT3 stage");
            b.Axes("fc.ff", "deg/s2, smoothed");
            b.Axes("fc.iterm", "PID units");
            b.Axes("fc.gyro_prev", "deg/s");
            b.Axes("fc.relax_lpf", "deg/s");
            b.Axes("fc.dmin_range1", "deg/s2, PT2 stage");
            b.Axes("fc.dmin_range", "deg/s2");
            b.Axes("fc.dmin_lpf1", "D min factor, PT2 stage");
            b.Axes("fc.dmin_lpf", "D min factor");
            b.Add("fc.yaw_p", "PID units", LogFieldType.F64);
            b.Add("fc.boost_lpf", "0..1", LogFieldType.F64);
            b.Add("fc.anti_gravity1", "1/s, PT2 stage", LogFieldType.F64);
            b.Add("fc.anti_gravity", "1/s", LogFieldType.F64);
            b.Add("fc.throttle_prev", "0..1", LogFieldType.F64);
            b.Add("fc.tpa_factor", "0..1", LogFieldType.F64);
            b.Add("fc.motor_mix_range", "0..", LogFieldType.F64);
            b.Add("fc.flags", "FcFlags", LogFieldType.U16);
            b.Add("fc.reserved", "zero", LogFieldType.Pad, 6);
            // The air (1.5).
            b.Vec("wind", "m/s, world, the wind field at the body's origin");
            b.Vec("aero_force", "N, world: body, coil and rotor in-plane drag");
            b.Vec("aero_moment", "N.m, body, about the CoM");
            b.Add("esc_limited", "bit per corner: held at its current limit", LogFieldType.U8);
            b.Add("air.reserved", "zero", LogFieldType.Pad, 7);
            // The contacts (1.6).
            b.Vec("contact_force", "N, world: every touching pair's normal force and friction");
            b.Vec("contact_moment", "N.m, body, about the CoM");
            b.Corners("leg_load", "N, each leg's spring force");
            b.Corners("leg_compression", "m, each leg's deepest point");
            b.Add("contacts_touching", "touching pairs that pushed", LogFieldType.U16);
            b.Add("contact.reserved", "zero", LogFieldType.Pad, 6);
            // The controller's filter chain and battery (1.4b).
            b.Axes("fc.gyro_filtered", "deg/s, gyroADCf after the gyro's filters");
            b.Axes("fc.dterm_gyro", "deg/s, after the D term's filters");
            for (int p = 1; p <= 5; p++) b.Axes("fc.dyn_notch" + p, "Hz, the dynamic notch's centre; 0 past its count");
            b.Add("fc.sag_volts", "V, the pack through the sag filter", LogFieldType.F64);
            b.Add("fc.cells", "the battery's cell count; 0 not yet found", LogFieldType.U8);
            b.Add("fc.dyn_lpf_throttle", "hundredths of throttle the dynamic LPFs follow", LogFieldType.U8);
            b.Add("fc.dyn_lpf_loops", "loops since they last moved, to 20", LogFieldType.U8);
            b.Add("fc.filters.reserved", "zero", LogFieldType.Pad, 5);
            // The wind at each hub (p7c-wind 1.4).
            foreach (string c in CornerNames) b.Vec("rotor_wind." + c, "m/s, world, the wind field at the hub");
            // The rain and the camera (p7c-wind 1.7).
            b.Vec("rain_force", "N, world, the drops' momentum flux on the airframe (inside aero_force); 0 when sunny");
            b.Add("film_mass", "kg, the rain's water film after the step; 0 when sunny", LogFieldType.F64);
            b.Vec("camera_air", "m/s, camera (body) axes, the air at the lens at the step's start");
            // The rotors' angles (p7b-airframe 1.1).
            b.Corners("rotor_angle", "rad, 0..2 pi, turned since the start");
            // The rotors' vortex rings (p7b-airframe 1.2).
            b.Corners("ring_inflow", "m/s, the ring's added inflow through the disc");
            b.Corners("ring_thrust", "the ring's thrust fluctuation, a share of the thrust");
            b.Add("ring_latched", "the rotors in their ring, bit 1 << corner", LogFieldType.U8);
            b.Add("ring.reserved", "zero", LogFieldType.Pad, 7);
            // The rotors' surfaces and re-ingested wash (p7b-airframe 1.3).
            b.Corners("ground_distance", "m along the axis to the surface below at the step's start; inf none, 0 without the effect");
            b.Corners("ceiling_distance", "m along the axis to the surface above at the step's start; inf none, 0 without the effect");
            b.Corners("wash_inflow", "the re-ingested wash's inflow through the disc over v_h");
            // The airframe's modes (p7b-airframe 1.4), lowest first; 0 past the body's count.
            for (int m = 0; m < AirframeModes.MaxModes; m++) b.Add("mode_shape" + m, "m kg^0.5, mass-normalised", LogFieldType.F64);
            for (int m = 0; m < AirframeModes.MaxModes; m++) b.Add("mode_rate" + m, "m kg^0.5/s", LogFieldType.F64);
            // The rotors in each other's wake (p7b-airframe 1.3d).
            b.Corners("interference_inflow", "m/s, the other rotors' wakes' added inflow through the disc, + down; 0 without the interference");
            b.Corners("interference_share", "the wakes' fluctuation, a share of that inflow");
            // Each rotor's own wake (D-064).
            b.Corners("wake_inflow", "the rotor's own wake inflow through the disc over v_h");
            // The fiber's line (p7d-tether 1.6); all zero for a flight without it.
            b.Add("tether.paid_out", "m, the line paid out this flight", LogFieldType.F64);
            b.Add("tether.payout_speed", "m/s, the balloon's speed: line leaving the exit", LogFieldType.F64);
            b.Add("tether.payout_tension_n", "N, the pack's resistance T_r (the peel at the section, the momentum flux, the balloon); its static peel while it holds", LogFieldType.F64);
            b.Add("tether.exit_tension", "N, the exit segment's tension at the step's end", LogFieldType.F64);
            b.Add("tether.lip_angle", "rad, the exit lip's wrap: the canister's aft axis to the first segment", LogFieldType.F64);
            b.Add("tether.irregularity", "the pack's irregularity at the section at the peel (spool.irregularity)", LogFieldType.F64);
            b.Vec("tether.force", "N, world, the line's pull on the drone this step, in net_force");
            b.Vec("tether.moment", "N.m, body, its moment about the CoM, in net_moment");
            b.Add("tether.nodes", "the chain's nodes after the step", LogFieldType.U16);
            b.Add("tether.flags", "bit 0 paying out, 1 coil empty, 2 clipped, 3 safe step, 4 wedge, 5 two-way spool", LogFieldType.U8);
            b.Add("tether.corrections", "the chain's extra solves this step", LogFieldType.U8);
            b.Add("tether.trail", "the laid trail's points after the step", LogFieldType.I32);
            // The blades' strikes this step (p7f-damage 1.3), per rotor and blade.
            for (int c = 0; c < 4; c++)
            {
                for (int blade = 0; blade < 2; blade++)
                {
                    string at = "blade_strike." + CornerNames[c] + "." + blade;
                    b.Add(at + ".kind", "StrikeKind: 0 none, 1 shape, 2 element, 3 wire", LogFieldType.U8);
                    b.Add(at + ".count", "strikes this step", LogFieldType.U8);
                    b.Add(at + ".material", "world material id of the last; 0 none", LogFieldType.U16);
                    b.Add(at + ".reserved", "zero", LogFieldType.Pad, 4);
                    b.Add(at + ".energy", "J taken from the rotor", LogFieldType.F64);
                    b.Add(at + ".impulse", "N.s on the drone, the strikes' sum of magnitudes", LogFieldType.F64);
                    b.Add(at + ".diameter", "m, an element's or a wire's; 0 for a shape", LogFieldType.F64);
                    b.Add(at + ".cut_energy", "J, an element's cut; 0 for a shape", LogFieldType.F64);
                    b.Add(at + ".radius", "m, the smallest strike radius on the blade", LogFieldType.F64);
                }
            }
            // The legs (p7g-legs 1.2).
            foreach (string c in CornerNames) b.Add("leg_state." + c, "LegFlags: 1 set, 2 cracked, 4 broken, 8 detached, 16 bowed, 32 twisted, 64 skid loose", LogFieldType.U8);
            foreach (string c in CornerNames) b.Add("corner_state." + c, "CornerState: 1 its skid half hanging, 2 its skid gone, 4 its tube a stub, 8 its tube gone, 16 folded", LogFieldType.U8);
            foreach (string c in CornerNames) b.Vec("leg_set." + c, "rad, body: x and z the bend at the clamp (the foot's way times the set angle), y the twist in the clamp");
            b.Corners("leg_stiffness", "the leg's stiffness over an intact one's by its state");
            b.Corners("leg_moment", "N.m, the moment at the clamp's edge the legs' law judged; 0 without it");
            // A damaged leg (p7g-legs 1.3).
            b.Corners("leg_side_give", "N/m, the side give with the anchor in series this step: the lever's secant (negative past the sway load; the tube's less P.delta at model 46), or a hanging half's cantilever");
            foreach (string c in CornerNames) b.Vec("leg_jaw." + c, "m, body, the jaws' turn at the foot: the side spring's rest moved by their friction slipping");
            foreach (string c in CornerNames) b.Vec("skid_fold." + c, "rad, body rotation vector, a hanging skid half's fold about the other leg's bore");
            b.Corners("leg_lean", "m, the leg's lean on its lever: its foot's offset across the tube from its rest, its points' shares; 0 off the lever or the ground");
            // A resting prop's blades (p7f-damage 1.3b).
            b.Corners("blade_load_0", "N, the contact force on blade 0 of a resting prop this step; 0 for a turning prop");
            b.Corners("blade_load_1", "N, the contact force on blade 1 of a resting prop this step; 0 for a turning prop");
            b.Corners("blade_bend_0", "m, blade 0's own deflection at its strongest contact, along its normal (its bending's share of the series)");
            b.Corners("blade_bend_1", "m, blade 1's own deflection at its strongest contact, along its normal (its bending's share of the series)");
            return b.Done(StepSize);
        }

        static LogField[] BuildLooseFields()
        {
            var b = new Layout();
            b.Add("step", "the step it is after", LogFieldType.I64);
            b.Add("slot", "the piece's slot (its PieceDropped event's index)", LogFieldType.U8);
            b.Add("at_rest", "1 once it lies still and stops stepping", LogFieldType.U8);
            b.Add("parts", "LegChange mask: bit c a whole tube, 4 + c below a break, 8 + c a T-joint, 12 right skid, 13 left", LogFieldType.U16);
            b.Add("reserved", "zero", LogFieldType.Pad, 4);
            b.Vec("position", "m, world, the piece's frame (the drone's body frame as it left)");
            b.Add("attitude.x", "frame to world", LogFieldType.F64);
            b.Add("attitude.y", "frame to world", LogFieldType.F64);
            b.Add("attitude.z", "frame to world", LogFieldType.F64);
            b.Add("attitude.w", "frame to world", LogFieldType.F64);
            b.Vec("velocity", "m/s, world, of the frame's origin");
            b.Vec("body_rate", "rad/s, the frame's axes");
            return b.Done(Contact.LoosePieces.RecordSize);
        }

        static LogField[] BuildEventFields()
        {
            var b = new Layout();
            b.Add("step", "the step that made it", LogFieldType.I64);
            b.Add("kind", "EventKind", LogFieldType.U16);
            b.Add("part", "drone part index; 65535 none", LogFieldType.U16);
            b.Add("object", "world object; -1 ground", LogFieldType.I32);
            b.Add("shape", "shape of the object", LogFieldType.I32);
            b.Add("material", "world material id; 0 none", LogFieldType.U16);
            b.Add("surface", "world surface id; 0 none", LogFieldType.U8);
            b.Add("index", "corner, axis or cause by kind; 255 none", LogFieldType.U8);
            b.Vec("point", "m, world");
            b.Add("a", "by kind", LogFieldType.F64);
            b.Add("b", "by kind", LogFieldType.F64);
            b.Add("c", "by kind", LogFieldType.F64);
            return b.Done(EventSize);
        }

        static LogField[] BuildEndFields()
        {
            var b = new Layout();
            b.Add("steps", "step records written", LogFieldType.I64);
            b.Add("events", "event records written, hitches included", LogFieldType.I64);
            b.Add("hitches", "scheduler hitches", LogFieldType.I64);
            b.Add("log_waits", "times the flight thread waited for a full ring", LogFieldType.I64);
            b.Add("sticks_dropped", "stick reports refused by a full inbox", LogFieldType.I64);
            b.Add("events_lost", "events refused by a full step buffer", LogFieldType.I64);
            return b.Done(EndSize);
        }

        /// <summary>One step's record: the new state, the stick sample in force during the step, the step's trace.</summary>
        public static void EncodeStep(Span<byte> dst, in FlightState s, in StickSample k, in StepTrace t)
        {
            var w = new Writer(dst.Slice(0, StepSize));
            w.I64(s.Step);
            w.V3(s.Position);
            w.V3(s.Velocity);
            w.F64(s.Attitude.X);
            w.F64(s.Attitude.Y);
            w.F64(s.Attitude.Z);
            w.F64(s.Attitude.W);
            w.V3(s.BodyRate);
            w.C4(s.RotorSpeed);
            w.C4(s.EscCommand);
            w.F64(s.PackCharge0);
            w.F64(s.PackCharge1);
            w.I64(k.HostUs);
            w.F64(k.Roll);
            w.F64(k.Pitch);
            w.F64(k.Yaw);
            w.F64(k.Throttle);
            w.C4(t.RotorCommand);
            w.F64(t.PackVolts0);
            w.F64(t.PackVolts1);
            w.F64(t.PackAmps0);
            w.F64(t.PackAmps1);
            w.V3(t.NetForce);
            w.V3(t.NetMoment);
            w.U8((byte)((s.Armed ? 1 : 0) | (k.Arm ? 2 : 0) | (s.Crashed ? 4 : 0) | (k.Failsafe ? 8 : 0)));
            w.Zero(7);
            ref readonly FcState f = ref s.Fc;
            w.A3(f.RcCommand);
            w.F64(f.RcThrottle);
            w.F64(f.RcDataThrottle);
            w.A3(f.RawSetpoint);
            w.A3(f.FfDeltaPrev);
            w.A3(f.FfSpeedPrev);
            w.A3(f.FfAccelPrev);
            w.A3(f.FfRaw);
            w.A3(f.Setpoint1);
            w.A3(f.Setpoint2);
            w.A3(f.Setpoint);
            w.F64(f.Throttle1);
            w.F64(f.Throttle2);
            w.F64(f.Throttle);
            w.A3(f.Ff1);
            w.A3(f.Ff2);
            w.A3(f.Ff);
            w.A3(f.ITerm);
            w.A3(f.GyroPrev);
            w.A3(f.RelaxLpf);
            w.A3(f.DMinRange1);
            w.A3(f.DMinRange);
            w.A3(f.DMinLpf1);
            w.A3(f.DMinLpf);
            w.F64(f.YawP);
            w.F64(f.BoostLpf);
            w.F64(f.AntiGravity1);
            w.F64(f.AntiGravity);
            w.F64(f.ThrottlePrev);
            w.F64(f.TpaFactor);
            w.F64(f.MotorMixRange);
            w.U16((ushort)f.Flags);
            w.Zero(6);
            w.V3(t.Wind);
            w.V3(t.AeroForce);
            w.V3(t.AeroMoment);
            w.U8(s.EscLimited);
            w.Zero(7);
            w.V3(t.ContactForce);
            w.V3(t.ContactMoment);
            w.C4(t.LegLoad);
            w.C4(t.LegCompression);
            w.U16((ushort)t.ContactsTouching);
            w.Zero(6);
            w.A3(f.GyroFiltered);
            w.A3(f.DtermGyro);
            w.A3(f.DynNotch1);
            w.A3(f.DynNotch2);
            w.A3(f.DynNotch3);
            w.A3(f.DynNotch4);
            w.A3(f.DynNotch5);
            w.F64(f.SagVolts);
            w.U8(f.Cells);
            w.U8(f.DynLpfThrottle);
            w.U8(f.DynLpfLoops);
            w.Zero(5);
            for (int i = 0; i < 4; i++) w.V3(t.RotorWind[i]);
            w.V3(t.RainForce);
            w.F64(t.FilmMass);
            w.V3(t.CameraAir);
            w.C4(s.RotorAngle);
            w.C4(s.RingInflow);
            w.C4(s.RingThrust);
            w.U8(s.RingLatched);
            w.Zero(7);
            w.C4(t.GroundDistance);
            w.C4(t.CeilingDistance);
            w.C4(s.WashInflow);
            for (int m = 0; m < AirframeModes.MaxModes; m++) w.F64(s.ModeShape[m]);
            for (int m = 0; m < AirframeModes.MaxModes; m++) w.F64(s.ModeRate[m]);
            w.C4(t.InterferenceInflow);
            w.C4(s.InterferenceShare);
            w.C4(s.WakeInflow);
            ref readonly Tether.TetherTrace l = ref t.Tether;
            w.F64(l.PaidOut);
            w.F64(l.PayoutSpeed);
            w.F64(l.PayoutTension);
            w.F64(l.ExitTension);
            w.F64(l.LipAngle);
            w.F64(l.Irregularity);
            w.V3(l.Force);
            w.V3(l.Moment);
            w.U16((ushort)l.Nodes);
            w.U8(l.Flags);
            w.U8(l.Corrections);
            w.I32(l.Trail);
            for (int i = 0; i < BladeStrikes.Slots; i++)
            {
                BladeStrike x = s.BladeStrikes[i];
                w.U8((byte)x.Kind);
                w.U8(x.Count);
                w.U16(x.Material);
                w.Zero(4);
                w.F64(x.Energy);
                w.F64(x.Impulse);
                w.F64(x.Diameter);
                w.F64(x.CutEnergy);
                w.F64(x.Radius);
            }
            for (int c = 0; c < 4; c++) w.U8((byte)(t.LegState >> (8 * c)));
            for (int c = 0; c < 4; c++) w.U8((byte)(t.CornerState >> (8 * c)));
            for (int c = 0; c < 4; c++) w.V3(t.LegSet[c]);
            w.C4(t.LegStiffnessFactor);
            w.C4(t.LegMoment);
            w.C4(t.LegSideGive);
            for (int c = 0; c < 4; c++) w.V3(t.LegJaw[c]);
            for (int c = 0; c < 4; c++) w.V3(t.SkidFold[c]);
            w.C4(t.LegLean);
            w.C4(t.BladeLoad0);
            w.C4(t.BladeLoad1);
            w.C4(t.BladeBend0);
            w.C4(t.BladeBend1);
        }

        public static void DecodeStep(ReadOnlySpan<byte> src, out FlightState s, out StickSample k, out StepTrace t)
        {
            var r = new Reader(src.Slice(0, StepSize));
            s = default;
            k = default;
            t = default;
            s.Step = r.I64();
            s.Position = r.V3();
            s.Velocity = r.V3();
            s.Attitude = new Quat(r.F64(), r.F64(), r.F64(), r.F64());
            s.BodyRate = r.V3();
            s.RotorSpeed = r.C4();
            s.EscCommand = r.C4();
            s.PackCharge0 = r.F64();
            s.PackCharge1 = r.F64();
            k.HostUs = r.I64();
            k.Roll = r.F64();
            k.Pitch = r.F64();
            k.Yaw = r.F64();
            k.Throttle = r.F64();
            t.RotorCommand = r.C4();
            t.PackVolts0 = r.F64();
            t.PackVolts1 = r.F64();
            t.PackAmps0 = r.F64();
            t.PackAmps1 = r.F64();
            t.NetForce = r.V3();
            t.NetMoment = r.V3();
            byte flags = r.U8();
            s.Armed = (flags & 1) != 0;
            k.Arm = (flags & 2) != 0;
            s.Crashed = (flags & 4) != 0;
            k.Failsafe = (flags & 8) != 0;
            r.Skip(7);
            ref FcState f = ref s.Fc;
            f.RcCommand = r.A3();
            f.RcThrottle = r.F64();
            f.RcDataThrottle = r.F64();
            f.RawSetpoint = r.A3();
            f.FfDeltaPrev = r.A3();
            f.FfSpeedPrev = r.A3();
            f.FfAccelPrev = r.A3();
            f.FfRaw = r.A3();
            f.Setpoint1 = r.A3();
            f.Setpoint2 = r.A3();
            f.Setpoint = r.A3();
            f.Throttle1 = r.F64();
            f.Throttle2 = r.F64();
            f.Throttle = r.F64();
            f.Ff1 = r.A3();
            f.Ff2 = r.A3();
            f.Ff = r.A3();
            f.ITerm = r.A3();
            f.GyroPrev = r.A3();
            f.RelaxLpf = r.A3();
            f.DMinRange1 = r.A3();
            f.DMinRange = r.A3();
            f.DMinLpf1 = r.A3();
            f.DMinLpf = r.A3();
            f.YawP = r.F64();
            f.BoostLpf = r.F64();
            f.AntiGravity1 = r.F64();
            f.AntiGravity = r.F64();
            f.ThrottlePrev = r.F64();
            f.TpaFactor = r.F64();
            f.MotorMixRange = r.F64();
            f.Flags = (FcFlags)r.U16();
            r.Skip(6);
            t.Wind = r.V3();
            t.AeroForce = r.V3();
            t.AeroMoment = r.V3();
            s.EscLimited = r.U8();
            r.Skip(7);
            t.ContactForce = r.V3();
            t.ContactMoment = r.V3();
            t.LegLoad = r.C4();
            t.LegCompression = r.C4();
            t.ContactsTouching = r.U16();
            r.Skip(6);
            f.GyroFiltered = r.A3();
            f.DtermGyro = r.A3();
            f.DynNotch1 = r.A3();
            f.DynNotch2 = r.A3();
            f.DynNotch3 = r.A3();
            f.DynNotch4 = r.A3();
            f.DynNotch5 = r.A3();
            f.SagVolts = r.F64();
            f.Cells = r.U8();
            f.DynLpfThrottle = r.U8();
            f.DynLpfLoops = r.U8();
            r.Skip(5);
            for (int i = 0; i < 4; i++) t.RotorWind[i] = r.V3();
            t.RainForce = r.V3();
            t.FilmMass = r.F64();
            t.CameraAir = r.V3();
            s.RotorAngle = r.C4();
            s.RingInflow = r.C4();
            s.RingThrust = r.C4();
            s.RingLatched = r.U8();
            r.Skip(7);
            t.GroundDistance = r.C4();
            t.CeilingDistance = r.C4();
            s.WashInflow = r.C4();
            for (int m = 0; m < AirframeModes.MaxModes; m++) s.ModeShape[m] = r.F64();
            for (int m = 0; m < AirframeModes.MaxModes; m++) s.ModeRate[m] = r.F64();
            t.InterferenceInflow = r.C4();
            s.InterferenceShare = r.C4();
            s.WakeInflow = r.C4();
            ref Tether.TetherTrace l = ref t.Tether;
            l.PaidOut = r.F64();
            l.PayoutSpeed = r.F64();
            l.PayoutTension = r.F64();
            l.ExitTension = r.F64();
            l.LipAngle = r.F64();
            l.Irregularity = r.F64();
            l.Force = r.V3();
            l.Moment = r.V3();
            l.Nodes = r.U16();
            l.Flags = r.U8();
            l.Corrections = r.U8();
            l.Trail = r.I32();
            for (int i = 0; i < BladeStrikes.Slots; i++)
            {
                var x = new BladeStrike { Kind = (StrikeKind)r.U8(), Count = r.U8(), Material = r.U16() };
                r.Skip(4);
                x.Energy = r.F64();
                x.Impulse = r.F64();
                x.Diameter = r.F64();
                x.CutEnergy = r.F64();
                x.Radius = r.F64();
                s.BladeStrikes[i] = x;
            }
            for (int c = 0; c < 4; c++) t.LegState |= (uint)r.U8() << (8 * c);
            for (int c = 0; c < 4; c++) t.CornerState |= (uint)r.U8() << (8 * c);
            for (int c = 0; c < 4; c++) t.LegSet[c] = r.V3();
            t.LegStiffnessFactor = r.C4();
            t.LegMoment = r.C4();
            t.LegSideGive = r.C4();
            for (int c = 0; c < 4; c++) t.LegJaw[c] = r.V3();
            for (int c = 0; c < 4; c++) t.SkidFold[c] = r.V3();
            t.LegLean = r.C4();
            t.BladeLoad0 = r.C4();
            t.BladeLoad1 = r.C4();
            t.BladeBend0 = r.C4();
            t.BladeBend1 = r.C4();
        }

        /// <summary>The stick sample of a step record, without decoding the rest.</summary>
        public static void DecodeStick(ReadOnlySpan<byte> src, out StickSample k)
        {
            var r = new Reader(src.Slice(StickAt, 40));
            k = default;
            k.HostUs = r.I64();
            k.Roll = r.F64();
            k.Pitch = r.F64();
            k.Yaw = r.F64();
            k.Throttle = r.F64();
            k.Arm = (src[FlagsAt] & 2) != 0;
            k.Failsafe = (src[FlagsAt] & 8) != 0;
        }

        /// <summary>The step a step record holds.</summary>
        public static long StepOf(ReadOnlySpan<byte> record) => BinaryPrimitives.ReadInt64LittleEndian(record);

        public static void EncodeEvent(Span<byte> dst, long step, in FlightEvent e)
        {
            var w = new Writer(dst.Slice(0, EventSize));
            w.I64(step);
            w.U16((ushort)e.Kind);
            w.U16(e.Part);
            w.I32(e.Object);
            w.I32(e.Shape);
            w.U16(e.Material);
            w.U8(e.Surface);
            w.U8(e.Index);
            w.V3(e.Point);
            w.F64(e.A);
            w.F64(e.B);
            w.F64(e.C);
        }

        public static void DecodeEvent(ReadOnlySpan<byte> src, out long step, out FlightEvent e)
        {
            var r = new Reader(src.Slice(0, EventSize));
            e = default;
            step = r.I64();
            e.Kind = (EventKind)r.U16();
            e.Part = r.U16();
            e.Object = r.I32();
            e.Shape = r.I32();
            e.Material = r.U16();
            e.Surface = r.U8();
            e.Index = r.U8();
            e.Point = r.V3();
            e.A = r.F64();
            e.B = r.F64();
            e.C = r.F64();
        }

        /// <summary>The end block's counts.</summary>
        public static void EncodeEnd(Span<byte> dst, in LogEnd end)
        {
            var w = new Writer(dst.Slice(0, EndSize));
            w.I64(end.Steps);
            w.I64(end.Events);
            w.I64(end.Hitches);
            w.I64(end.LogWaits);
            w.I64(end.SticksDropped);
            w.I64(end.EventsLost);
        }

        public static void DecodeEnd(ReadOnlySpan<byte> src, out LogEnd end)
        {
            var r = new Reader(src.Slice(0, EndSize));
            end = new LogEnd
            {
                Steps = r.I64(),
                Events = r.I64(),
                Hitches = r.I64(),
                LogWaits = r.I64(),
                SticksDropped = r.I64(),
                EventsLost = r.I64(),
            };
        }

        /// <summary>A block's kind and length, before its payload.</summary>
        public static void EncodeBlockHeader(Span<byte> dst, ushort kind, int length)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(dst, kind);
            BinaryPrimitives.WriteUInt16LittleEndian(dst.Slice(2), (ushort)length);
        }

        /// <summary>The index of the field holding byte offset, or −1.</summary>
        public static int FieldAt(LogField[] fields, int offset)
        {
            for (int i = 0; i < fields.Length; i++)
                if (offset >= fields[i].Offset && offset < fields[i].Offset + fields[i].Size)
                    return i;
            return -1;
        }

        /// <summary>A field's value in a record, as text: a double with its bits, so two that print alike still differ.</summary>
        public static string FormatField(in LogField f, ReadOnlySpan<byte> record)
        {
            ReadOnlySpan<byte> b = record.Slice(f.Offset, f.Size);
            CultureInfo c = CultureInfo.InvariantCulture;
            switch (f.Type)
            {
                case LogFieldType.F64:
                    long bits = BinaryPrimitives.ReadInt64LittleEndian(b);
                    return BitConverter.Int64BitsToDouble(bits).ToString("R", c) + " (0x" + bits.ToString("X16", c) + ")";
                case LogFieldType.I64: return BinaryPrimitives.ReadInt64LittleEndian(b).ToString(c);
                case LogFieldType.I32: return BinaryPrimitives.ReadInt32LittleEndian(b).ToString(c);
                case LogFieldType.U16: return BinaryPrimitives.ReadUInt16LittleEndian(b).ToString(c);
                case LogFieldType.U8: return b[0].ToString(c);
                default:
                    var text = new System.Text.StringBuilder("0x");
                    for (int i = 0; i < b.Length; i++) text.Append(b[i].ToString("X2", c));
                    return text.ToString();
            }
        }

        sealed class Layout
        {
            readonly System.Collections.Generic.List<LogField> _fields = new System.Collections.Generic.List<LogField>();
            int _at;

            public void Add(string name, string unit, LogFieldType type, int size = 0)
            {
                if (size == 0) size = type == LogFieldType.F64 || type == LogFieldType.I64 ? 8 : type == LogFieldType.I32 ? 4 : type == LogFieldType.U16 ? 2 : 1;
                _fields.Add(new LogField(name, unit, type, _at, size));
                _at += size;
            }

            public void Vec(string name, string unit)
            {
                Add(name + ".x", unit, LogFieldType.F64);
                Add(name + ".y", unit, LogFieldType.F64);
                Add(name + ".z", unit, LogFieldType.F64);
            }

            public void Corners(string name, string unit)
            {
                foreach (string corner in CornerNames)
                    Add(name + "." + corner, unit, LogFieldType.F64);
            }

            public void Axes(string name, string unit)
            {
                Add(name + ".roll", unit, LogFieldType.F64);
                Add(name + ".pitch", unit, LogFieldType.F64);
                Add(name + ".yaw", unit, LogFieldType.F64);
            }

            public LogField[] Done(int size)
            {
                if (_at != size) throw new InvalidOperationException("a log layout is " + _at + " bytes, not " + size);
                return _fields.ToArray();
            }
        }

        ref struct Writer
        {
            readonly Span<byte> _b;
            int _at;

            public Writer(Span<byte> b)
            {
                _b = b;
                _at = 0;
            }

            public void F64(double v) => I64(BitConverter.DoubleToInt64Bits(v));

            public void I64(long v)
            {
                BinaryPrimitives.WriteInt64LittleEndian(_b.Slice(_at), v);
                _at += 8;
            }

            public void I32(int v)
            {
                BinaryPrimitives.WriteInt32LittleEndian(_b.Slice(_at), v);
                _at += 4;
            }

            public void U16(ushort v)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(_b.Slice(_at), v);
                _at += 2;
            }

            public void U8(byte v) => _b[_at++] = v;

            public void Zero(int n)
            {
                _b.Slice(_at, n).Clear();
                _at += n;
            }

            public void V3(in Double3 v)
            {
                F64(v.X);
                F64(v.Y);
                F64(v.Z);
            }

            public void C4(in Corners c)
            {
                F64(c.RearRight);
                F64(c.FrontRight);
                F64(c.RearLeft);
                F64(c.FrontLeft);
            }

            public void A3(in Axes3 a)
            {
                F64(a.Roll);
                F64(a.Pitch);
                F64(a.Yaw);
            }
        }

        ref struct Reader
        {
            readonly ReadOnlySpan<byte> _b;
            int _at;

            public Reader(ReadOnlySpan<byte> b)
            {
                _b = b;
                _at = 0;
            }

            public double F64() => BitConverter.Int64BitsToDouble(I64());

            public long I64()
            {
                long v = BinaryPrimitives.ReadInt64LittleEndian(_b.Slice(_at));
                _at += 8;
                return v;
            }

            public int I32()
            {
                int v = BinaryPrimitives.ReadInt32LittleEndian(_b.Slice(_at));
                _at += 4;
                return v;
            }

            public ushort U16()
            {
                ushort v = BinaryPrimitives.ReadUInt16LittleEndian(_b.Slice(_at));
                _at += 2;
                return v;
            }

            public byte U8() => _b[_at++];

            public Double3 V3() => new Double3(F64(), F64(), F64());

            public Corners C4() => new Corners { RearRight = F64(), FrontRight = F64(), RearLeft = F64(), FrontLeft = F64() };

            public Axes3 A3() => new Axes3 { Roll = F64(), Pitch = F64(), Yaw = F64() };

            public void Skip(int n) => _at += n;
        }
    }

    /// <summary>The end block: what the recorder wrote and what the flight lost, counted at the close.</summary>
    public struct LogEnd
    {
        public long Steps, Events, Hitches, LogWaits, SticksDropped, EventsLost;
    }
}
