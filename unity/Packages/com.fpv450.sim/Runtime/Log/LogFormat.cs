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
        /// 41: the hanging skid half and the broken stub (p7g-legs on the line, unity/physics/physics-integration-2--skid-half,
        ///    assigned by D-072): the contacts entry gains the hanging half's switch (ContactSpec.HangingGive, a bool) after the
        ///    lever's length, and its legs' data the stub's contact spring across and along the legs' axis (LegDamageSpec.
        ///    StubStiffness, StubStiffnessUp) after the loose pieces' rest; the record is 40's 2816 bytes; no new event kind.
        /// 42: p7f-damage 1.3b on the physics integration 2 (unity/physics/physics-integration-2--props-rest, assigned by D-072):
        ///    after 40's 2816 bytes each resting prop's blades' load and deflection (blade_load_0/1, blade_bend_0/1; 128 bytes,
        ///    2944 in all); the contacts entry's shapes with each prop's blade edges (ContactRole.Blade, after every other shape)
        ///    and, after LegLeverLength, the rest's switch and speed (ContactSpec.BladeRest, BladeRestSpeed); no new event kind.
        /// 43: the fiber's contacts (p7d-tether 1.7, unity/physics/physics-integration-2--fiber-contacts, on 40; assigned by
        ///    D-072 and D-074): the contacts entry's material rows gain each row's edge_radius_m (MaterialRow.EdgeRadius) after
        ///    its density; the tether entry gains the switch FlightSetup.TetherWithoutContacts after the design 4 air's, and
        ///    tether.json's contact block (13 rows; the entry writes every row); the events FiberContact (30) and FiberSlip (31),
        ///    and WorldBug's causes 2–6; the record is 40's 2816 bytes (the bend's radius goes into the record with 1.13's
        ///    fields: here it is FiberContact's and FiberContacts.MinBendRadius).
        /// 44: the skid's frame fore and aft (p7g-legs on the line, unity/physics/physics-integration-2--legs-frame, assigned by
        ///    D-072): the contacts entry gains the frame leg's give, sway load and lever (ContactSpec.LegFrameSideStiffness,
        ///    LegFrameSwayLoad, LegFrameLeverLength) after the hanging half's switch, and its legs' data the frame's shares and
        ///    self-stiffness (LegDamageSpec.FrameTopShare, FrameJointShare, FrameSelfStiffness) after the stub's spring; the record
        ///    is 41's 2816 bytes; no new event kind (a frame leg's socket edge names its T-joint's part; its parting is SkidLoose
        ///    with LegMode.Bending).
        /// 45: the physics integration 2 with the fiber's contacts (unity/physics/physics-integration-2--int55, assigned by
        ///    D-072): 43 merged onto 41: the contacts entry is 41's (the hanging half's switch after the lever's length, the
        ///    stub's springs in the legs' data) with 43's EdgeRadius in each material row after its density; the tether entry
        ///    43's (TetherWithoutContacts after the design 4 air's switch, tether.json's contact block); the events 43's
        ///    (FiberContact 30, FiberSlip 31, WorldBug's causes 2-6); the record is 40's 2816 bytes.
        /// 46: the fiber's cut risk, the link and the break (p7d-tether 1.8, unity/physics/physics-integration-2--fiber-cut, on
        ///    45; assigned by D-072 and D-074): the tether entry gains the switches FlightSetup.TetherWithoutCuts and
        ///    TetherOneStrength after TetherWithoutContacts; the settings gain failsafe_delay, failsafe_procedure,
        ///    failsafe_throttle and failsafe_off_delay (FcSettings, by name); the draws gain fiber.span_strength's; the events
        ///    TetherBreak (32), LinkDip (33), FiberSection (34) and Failsafe (35), WorldBug's cause 7 (FiberSectionsFull); the
        ///    tether block's flags gain Broken (64) and Dip (128); the record is 45's 2816 bytes (the margin, the state and the
        ///    least bend radius go into the record with 1.13's fields: here they are the feed's signals and the events).
        /// 47: the physics integration 2 with the props at rest (unity/physics/physics-integration-2--int57, assigned by D-072):
        ///    42 merged onto 45: the record is 42's 2944 bytes (45's 2816 and the resting blades' block); the contacts entry 45's
        ///    (41's hanging half's switch after the lever's length, the stub's springs in the legs' data, 43's EdgeRadius in each
        ///    material row) with 42's blade edges in its shapes and BladeRest, BladeRestSpeed after the hanging half's switch; the
        ///    tether entry 45's (TetherWithoutContacts, tether.json's contact block); the events 45's (FiberContact 30, FiberSlip
        ///    31); no new event kind.
        /// 48: p7f-damage 1.5, the motors' bells (unity/physics/physics-integration-2--motor-bell, assigned by D-072 and D-074): after
        ///    47's 2944 bytes each motor's bell scrape, rub and Kt ratio (bell_scrape, bell_rub, bell_kt_change; 96 bytes, 3040 in all); the
        ///    contacts entry gains, after BladeRestSpeed, the bells' switch, their laws' coefficients and the four motors'
        ///    internals (ContactSpec.MotorBell, BellRubFriction, BellWearCoefficient, MagnetHardness, StatorHardness, BellDentArc,
        ///    Motors), and the internals (in both entries) the stator's band and diameter and the can's and back iron's moduli;
        ///    the events MotorDamaged (21) and MotorBound (22).
        /// 49: the physics integration 2 with the cuts (unity/physics/physics-integration-2--int59, assigned by D-072): 46 merged
        ///    onto 47: the record is 47's 2944 bytes (45's 2816 with 46's tether flags Broken (64) and Dip (128), and 42's resting
        ///    blades' block); the contacts entry 47's (BladeRest, BladeRestSpeed after the hanging half's switch, the blade edges
        ///    in its shapes); the tether entry 46's (TetherWithoutContacts, then TetherWithoutCuts and TetherOneStrength); the
        ///    settings 46's failsafe fields; the draws 46's fiber.span_strength; the events 46's (TetherBreak 32, LinkDip 33,
        ///    FiberSection 34, Failsafe 35, WorldBug's cause 7); no new event kind.
        /// 50: the physics integration 2 with the skid's frame (unity/physics/physics-integration-2--int60, assigned by D-072):
        ///    44 merged onto 49: the record is 49's 2944 bytes; the contacts entry 49's (BladeRest, BladeRestSpeed after the
        ///    hanging half's switch, the blade edges in its shapes) with 44's frame give, sway load and lever (ContactSpec.
        ///    LegFrameSideStiffness, LegFrameSwayLoad, LegFrameLeverLength) after BladeRestSpeed, and its legs' data 44's frame
        ///    shares and self-stiffness (LegDamageSpec.FrameTopShare, FrameJointShare, FrameSelfStiffness) after the stub's
        ///    springs; the tether entry, the settings, the draws and the events 49's; no new event kind.
        /// 53: the physics integration 2 with the bells (unity/physics/physics-integration-2--int63, assigned by D-072): 48
        ///    merged onto 50: the record is 48's 3040 bytes (50's 2944 with 48's bells' block after it); the contacts entry
        ///    50's with 48's bells' switch, coefficients and the motors' internals after the frame's three (LegFrameLeverLength),
        ///    and the internals (in both entries) 48's; the tether entry, the settings and the draws 50's; the events 50's with
        ///    48's MotorDamaged (21) and MotorBound (22). (51 and 52 are the parallel integrations'.)
        /// 51: the drone's body over the fountain (p7d-tether, unity/physics/physics-integration-2--fountain-body, on 49;
        ///    assigned by D-072): the tether entry gains the known-wrong switch FlightSetup.TetherFountainThroughBody after
        ///    TetherOneStrength; tether.json's rotor_air block gains fountain_impingement_depth_ratio and
        ///    body_lee_length_over_diameter (rows, so the entry's layout is otherwise 49's); the record is 49's 2944 bytes; no
        ///    new event kind.
        /// 54: D01's canister in the physics (unity/physics/physics-integration-2--canister, assigned by D-072): 51 merged onto
        ///    53: the record is 53's 3040 bytes; the contacts entry 53's; the tether entry 53's with 51's known-wrong switch
        ///    FlightSetup.TetherFountainThroughBody after TetherOneStrength; tether.json's rotor_air block 51's and its spool block
        ///    gains exit_direction (rows, so the entry's layout is otherwise 53's); the modes' entry gains CoilHousingPart after
        ///    CoilPart and the placement's after PackParts (drone.json's coil.housing, the canister); the contacts entry gains
        ///    ContactSpec.BladeRoot after the motors' internals; the settings, the draws and the events 53's; no new event kind.
        /// 52: the line held between the drone's parts (p7d-tether 1.7's part slots, on 49; assigned by D-072): the tether entry
        ///    gains FlightSetup.TetherZeroForceHold (the known-wrong 0-force hold) after TetherOneStrength; the record is 49's 2944
        ///    bytes; no new event kind.
        /// 55: the physics integration 2 with the fiber's chain fixes (unity/physics/physics-integration-2--int65, assigned by
        ///    D-072): 52 merged onto 54: the record is 54's 3040 bytes; the tether entry 54's with 51's
        ///    FlightSetup.TetherFountainThroughBody and then 52's FlightSetup.TetherZeroForceHold after TetherOneStrength; the
        ///    contacts entry, the modes', the settings, the draws and the events 54's; no new event kind.
        /// 56: p7f-damage 1.6, the shafts and the bearings (unity/physics/physics-integration-2--motor-shaft, on 53; assigned by D-072
        ///    and D-074; 54 and 55 are the parallel lines'): after 53's 3040 bytes each motor's shaft bend (p, q), its bearings' drag
        ///    and a brinelled bearing's outer ball-pass frequency (shaft_bend_p, shaft_bend_q, bearing_drag, ball_pass_outer; 128
        ///    bytes, 3168 in all); the contacts entry gains, after the internals, the shafts' switch and their laws' coefficients
        ///    (ContactSpec.MotorShaft, ShaftHingeShare, BearingFriction, BearingDentDrag), and the internals (in both entries) the
        ///    shaft's modulus and the bell's mass and centre; MotorDamaged's bands 2 (the shaft), 3 and 4 (the upper and lower
        ///    bearings); the events BladeLost (36) and PropLost (37).
        /// 58: the physics integration 2 with the motor's shaft and bearings (unity/physics/physics-integration-2--int68, assigned
        ///    by D-072; 57 is a parallel line's): 56 merged onto 55: the record is 56's 3168 bytes (55's 3040, then the shafts'
        ///    block); the contacts entry 55's with 54's ContactSpec.BladeRoot and then 56's MotorShaft, ShaftHingeShare,
        ///    BearingFriction and BearingDentDrag after the motors' internals, and the internals (in both entries) 56's; the tether
        ///    entry, the modes', the settings and the draws 55's; the events 55's with 56's BladeLost (36) and PropLost (37).
        /// 60: p7f-damage 1.6b, the motors' heat and dirt (unity/physics/physics-integration-2--motor-heat, on 58; assigned by D-072
        ///    and D-074; 59 is a parallel line's): after 58's 3168 bytes each motor's winding and stator temperatures, its Kt's
        ///    change by heat, its dirt's mass and drag and its state (winding_temperature, stator_temperature, heat_kt_change,
        ///    dirt_mass, dirt_drag, motor_state; 192 bytes, 3360 in all); the contacts entry gains, after 58's BearingDentDrag, the
        ///    heat's switch and its laws' numbers (ContactSpec.MotorHeat, AirTemperature, CopperSpecificHeat,
        ///    CopperTemperatureCoefficient, IronSpecificHeat, AirConductivity, AirViscosity, AirPrandtl, ArmConductance,
        ///    WindingTimeConstant, ScrapeWearCoefficient), and the internals (in both entries) the winding's resistance, copper,
        ///    conductance and end turns, the slots, the insulation's rating and breakdown, the magnets' maximum and coefficient, the
        ///    emissivity, the openings and the motor's mass; MotorDamaged's bands 5 to 8; no new event kind.
        /// 63: the physics integration 2 with D-086's controller (unity/physics/physics-integration-2--int73, assigned by D-072; 61 and
        ///    62 are parallel lines'): 69's controller (format 55's layout, no record, header or event change) merged onto 60: the
        ///    record is 60's 3360 bytes, the header 60's (the settings carry airmode as before), the events 60's; no new event kind.
        /// 62: p7f-damage 1.6, the losses, a lost prop and a lost blade graded (unity/physics/physics-integration-2--prop-loss, on 58; assigned
        ///    by D-072; 59–61 are the parallel lines'): after 58's 3168 bytes each arm's bend across the arm and twist with their
        ///    rates, its load, the gyro's peak before its range and the arms broken (arm_bend, arm_bend_rate, arm_twist,
        ///    arm_twist_rate, arm_load, gyro_peak, arm_broken; 176 bytes, 3344 in all); the modes' entry gains, after the camera's
        ///    parts, ArmInPlane, ArmShearModulus, ArmFlexuralStrength, ArmShearStrength and each corner's HubHeight, TwistMoment and
        ///    TwistInertia; the setup entry FlightSetup.GyroRange after TetherZeroForceHold; the event ArmBroken (38).
        /// 65: the physics integration 2 with the losses (unity/physics/physics-integration-2--int75, assigned by D-072; 64 is a
        ///    parallel line's): 62's arms merged onto 63: after 63's 3360 bytes (58's 3168, then the heat's 192) 62's arms' block
        ///    (176 bytes, 3536 in all), so 63's record is this one's first 3360 bytes and 62's is its first 3168 and its last 176; the
        ///    header both sides' (the contacts entry 60's, the modes' and the setup entries 62's); the events 63's with 62's ArmBroken (38).
        /// 66: the climb after a pitch-back (unity/physics/physics-integration-2--climb, on 65; assigned by D-072): the flapping's
        ///    entry gains, after its stations, RotorFlappingSpec.EdgewiseThrust and ProfileDrag. The record (3536 bytes) and the
        ///    events are 65's.
        /// 67: model 77, the fiber's chain solves its segments to backward Euler's own step (unity/physics/physics-integration-2--chain-spikes,
        ///    assigned by D-072; 64–66 are parallel lines'): the header's tether entry gains FlightSetup.TetherLinearStep (the
        ///    known-wrong linear step, model 74's chain) after TetherZeroForceHold; the record is 63's 3360 bytes and the events
        ///    63's (the Newton solves are not logged: a replay recounts them bit for bit, CableChain.NewtonSolves).
        /// 68: the physics integration 2 with the fiber's Newton step and its cut criteria (unity/physics/physics-integration-2--int78,
        ///    assigned by D-072; 66 is a parallel line's): 67's tether entry (FlightSetup.TetherLinearStep after TetherZeroForceHold)
        ///    merged onto 65: the record is 65's 3536 bytes (63's 3360, then 62's arms' block), so 67's record is its first 3360
        ///    bytes; the header both sides' (the setup entry 62's GyroRange and 67's TetherLinearStep, the tether entry 74's rows);
        ///    the events 65's; no new event kind.
        /// 69: the physics integration 2 with the climb's edgewise thrust and the fiber's Newton step (unity/physics/physics-integration-2--int79,
        ///    assigned by D-072): 68's tether entry (FlightSetup.TetherLinearStep) merged onto 66's flapping entry
        ///    (RotorFlappingSpec.EdgewiseThrust and ProfileDrag); the record is 65's 3536 bytes, the events 65's; no new event kind.
        /// 72: p7h-step-budget 1.1, the step's own clock (unity/physics/physics-integration-2--step-budget, on 69; assigned by the
        ///    orchestrator; 70 and 71 are parallel lines'): the end block gains, after 69's six counts, the steps timed, the overruns
        ///    past 1 ms, the p50, p99 and p99.9 and the max of each step's advance on the host clock (StepTimeSummary; 48 bytes, 96
        ///    in all). The record, the header and the events are 69's, so this build still reads and replays a format-69 log
        ///    (Format69: its end block is Format69EndSize bytes, without the times).
        /// </summary>
        public const int Version = 72;

        /// <summary>The format before the step times, read as well (72's layouts, its end block the first Format69EndSize bytes).</summary>
        public const int Format69 = 69;

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
        /// 51: the hanging skid half and the broken stub (p7g-legs on the line, unity/physics/physics-integration-2--skid-half,
        ///    assigned by D-072). A hanging half's whole give is geometry (ContactSpec.HangingGive): its shapes stand out of what
        ///    they touch by its contact spring's share alone, the cantilever's give by its turn about the holding leg's bore and
        ///    the rest of its chain (the holding leg's give across its tube, the stick anchor in series with it, and the
        ///    cantilever's share no turn about the bore gives) by its move along the deepest point's normal; it springs back as
        ///    its skid leaves. At 50 only the cantilever's share moved its shapes, so under a few newtons the rest sank a half
        ///    into a rigid limb past its centre (the crown in Windy's Tunnel through V01_L, 0.115 m deep). A broken leg's stub
        ///    takes the clamp's mount as its contact spring (drone.json's mount_stiffness_n_per_m and mount_side_stiffness_n_per_m,
        ///    the plate arm under the clamp out of and in its plane), never the free tube's 6.6 kN/m (50's stub passed H01_D's
        ///    masonry, 44 mm). With HangingGive off and the stub's spring 0 a flight steps as in 50; a flight whose legs never
        ///    part steps as in 50.
        /// 52: p7f-damage 1.3b, props are parts at rest (on 50; unity/physics/physics-integration-2--props-rest, assigned by
        ///    D-072): a rotor turning slower than ContactSpec.BladeRestSpeed carries its blades as contact shapes at their angle
        ///    (each blade's edges along its span segments, ContactRole.Blade), bearing load through the contact model on the
        ///    blade's bending in series with its motor's mount, their moment about the shaft turning the rotor (Propulsion's
        ///    shaft load, implicit), their sections bending or breaking by design 5's laws (PropStrikes.RestDamage); a resting
        ///    rotor strikes nothing, a faster one strikes as at 50; a jammed head-on crush inside the rim notches the leading edge
        ///    by its share of the chord (it trimmed the tip by it at 50). A flight whose props never rest on anything steps as in
        ///    50 over its 2816 bytes, the new block zero; with ContactSpec.BladeRest off, as in 50 but for a jammed head-on crush.
        /// 53: the fiber's contacts (p7d-tether 1.7, design 6 and 12; on 50, assigned by D-072): the line meets the plane with its
        ///    surface's own friction (surfaces.json's set at the flight's moisture), over a hole the shapes there; every shape but
        ///    the twig, foliage and razor_wire shells under each segment's clearance certificate; the elements, snag records and
        ///    conductors of three caches swapped in 20 steps after their request; the drone's parts, their reactions on the drone
        ///    at the contact points in the next step; a node put where a member meets a segment, two members on a node in one
        ///    solve; a capstan's hold slips across the line by Coulomb (CableChain). A flight without the line steps as in 50,
        ///    and so does one with it and FlightSetup.TetherWithoutContacts.
        /// 54: the skid's frame fore and aft (p7g-legs on the line, unity/physics/physics-integration-2--legs-frame, assigned by
        ///    D-072). The two legs on one skid are joined by its T-joints, so along the skid each sways with its foot held square:
        ///    its give at the foot 12(1 + β)/(4 + β)·EI/L³ and its sway-buckling load from u·cos u + β·sin u = 0 (DroneLegs.Frame;
        ///    β = R·L/EI from the T-joint's socket bed, the skid's bending between its bores and its pitch on the two legs: 784 N/m
        ///    and 146.6 N against the cantilever's 268 and 47.1), on its own lever in that plane (ContactSpec.LegFrameLeverLength),
        ///    and the sway's moment parted between the clamp's edge and the socket's, each of which cracks, yields and parts
        ///    (LegDamage.FrameEdges). Across the skid the cantilever's values hold. A broken or detached leg or a T-joint that lets
        ///    its skid go breaks the frame: its sibling is a cantilever both ways. With the frame's data 0 (ContactSpec's three,
        ///    LegDamageSpec.FrameTopShare) a flight steps as in 51; a flight whose legs never touch steps as in 51.
        /// 55: the physics integration 2 with the fiber's contacts (unity/physics/physics-integration-2--int55, assigned by
        ///    D-072; the code is the parents' together): 53's fiber contacts merged onto 51's hanging half and stub. A flight
        ///    without the line, or with it and FlightSetup.TetherWithoutContacts, steps as in 51; one whose legs never part (no
        ///    hanging half, no stub) steps as in 53, and so does any flight with ContactSpec.HangingGive off and the stub's
        ///    spring 0.
        /// 56: the fiber's cut risk, the link and the break (p7d-tether 1.8, design 7, 8, 14; on 55, assigned by D-072): the span's
        ///    strength and each section's (fiber.section_strength, counter-based), Archard's wear at the contacts, the prop's,
        ///    tension's, bend's, abrasion's and peel's checks each step, the break (the far piece left where it lies, the near
        ///    piece hanging from the exit), the link's margin and dips, and the controller's Betaflight 4.5 failsafe on the break
        ///    (no frame reaches it from the step after). A flight without the line, or with it and FlightSetup.TetherWithoutCuts,
        ///    steps as in 55; one with the line whose checks never break it steps as in 55 too (the state; the record but its tether
        ///    flags' Dip bit while a dip is on), its log gaining FiberSection and LinkDip events.
        /// 57: the physics integration 2 with the props at rest (unity/physics/physics-integration-2--int57, assigned by D-072;
        ///    the code is the parents' together): 52's resting props merged onto 55's fiber contacts, hanging half and stub. The
        ///    fiber meets a resting rotor's blade shapes as it meets every other part (FiberContacts reads ContactModel.Shape; a
        ///    turning rotor's blades are off the set, as for the ground). A flight whose props never rest on anything steps as in
        ///    55 over its 2816 bytes, the new block zero, and so does one with ContactSpec.BladeRest off but for a jammed head-on
        ///    crush; a flight without the line, or with FlightSetup.TetherWithoutContacts, whose legs never part steps as in 52.
        /// 58: p7f-damage 1.5, the motors' bells (on 57; unity/physics/physics-integration-2--motor-bell, assigned by D-072):
        ///    with drone.json's internals, a contact on a motor sphere's turning band (Contact.MotorBell) slides on the bell's
        ///    surface while its rotor turns (kinetic, no anchor), its moment against the rotor and the magnets' rub on the stator
        ///    (past the gap under a load, and a dent's interference) a drag in Propulsion (SetMotor: a Coulomb drag turning, a hold
        ///    at rest, with Kt·I0 held too), the gap's flux its Kt; the ring dents past its collapse load (MotorDamaged); a driven
        ///    rotor its drag holds at rest is bound (MotorBound). A flight whose motors' bands never touch anything while it flies
        ///    steps as in 57 over its 2944 bytes, the new block zero; with ContactSpec.MotorBell off, as in 57.
        /// 59: the physics integration 2 with the cuts (unity/physics/physics-integration-2--int59, assigned by D-072): 56's
        ///    cuts, link and failsafe merged onto 57's props at rest on the fiber's contacts. The code is the parents' together
        ///    but for one rule of the composition: the fiber's prop check (FiberLine.Prop) skips a resting rotor
        ///    (ContactModel.Resting: slower than ContactSpec.BladeRestSpeed, no carve holding it), as PropStrikes does, so a
        ///    resting blade is a part the line meets (its contact's bend and wear), never a pass that cuts it; an armed rotor at
        ///    idle or faster turns a disc and cuts at the next blade pass, as at 56. A flight with FlightSetup.TetherWithoutCuts
        ///    steps as in 57; one with ContactSpec.BladeRest off steps as in 56 over its 2816 bytes (but for 52's jammed head-on
        ///    crush), the block zero.
        /// 60: the physics integration 2 with the skid's frame (unity/physics/physics-integration-2--int60, assigned by D-072;
        ///    the code is the parents' together): 54's frame fore and aft merged onto 59's cuts, link and failsafe on the props at
        ///    rest and the fiber's contacts. A resting prop's blades and a hanging half both bear through the leg corners' own
        ///    shapes and the frame changes only a holding leg's give and law, so they compose without new code. A flight with
        ///    the frame's data 0 (ContactSpec's three, LegDamageSpec.FrameTopShare) steps as in 59; one with
        ///    FlightSetup.TetherWithoutContacts (so without the cuts) and ContactSpec.BladeRest off steps as in 54 over its 2816
        ///    bytes, the block zero (but for 52's jammed head-on crush); a flight whose legs never touch steps as in 59. One fix
        ///    the merge's cause table found: a line without its contacts (FlightSetup.TetherWithoutContacts) broken by tension,
        ///    bend or abrasion threw at its FiberSection event (no names to place it by, FiberCuts.SectionEvent; at 56–59 too);
        ///    it is placed as FiberLine.Place places it. No flight that ran at 59 steps otherwise.
        /// 63: the physics integration 2 with the bells (unity/physics/physics-integration-2--int63, assigned by D-072; the
        ///    code is the parents' together): 58's motors' bells merged onto 60's frame on the cuts, the failsafe and the props at
        ///    rest. A resting rotor's hold and a bell's drag share Propulsion's rest path (SetShaftLoad, Kt·I0 held only when a
        ///    drag is set), so a flight whose bells never touch rests as at 60. A flight with ContactSpec.MotorBell off steps as
        ///    in 60 over its 2944 bytes, the new block zero; one with the frame's data 0, FlightSetup.TetherWithoutContacts
        ///    (so without the cuts) and the hanging give off steps as in 58. (61 and 62 are the parallel integrations'.)
        /// 61: the drone's body over the fountain (p7d-tether, unity/physics/physics-integration-2--fountain-body, on 59;
        ///    assigned by D-072): RotorAir's fountain meets the drone's own contact shapes (every shape but the discs and a turning
        ///    rotor's blades, at the step's pose) instead of rising through them: no air inside a shape or in its lee, and under
        ///    its underside the stagnation (the rise falling linearly to nothing over the impingement layer, the jet's or the
        ///    body's depth, the smaller) and the deflection across the sheet (continuity, at most the jet's centreline speed). A
        ///    flight without the line, or with it and FlightSetup.TetherFountainThroughBody, or on design 4's field
        ///    (TetherDesign4Air), steps as in 59.
        /// 64: the physics integration 2 with the body over the fountain and D01's canister (unity/physics/physics-integration-2--
        ///    canister, assigned by D-072): 61's body over the fountain merged onto 63's bells, frame, cuts and failsafe (they
        ///    compose without new code: 61 changes only RotorAir's field), and the canister of the drone model in place of the old
        ///    coil beneath the frame (drone.json's canister part, coil.housing, its body, cone and nozzle the coil, coil_aft,
        ///    coil_fore, coil_cone and coil_nozzle contacts). The line leaves at the nozzle's tip (tether.json's
        ///    spool.exit_position_m) along the nozzle (spool.exit_direction, a new row): the lip's wrap is measured from it, not
        ///    from the body's aft axis, and the launch lays the line from the anchor to where that lead meets the ground, then up
        ///    along it to the tip (beneath the exit for a level or rising lead). The coil's drag is its housing's cylinder (the
        ///    pack inside is hidden), its mount in the modes carries the pack and the housing, and its play in the build's
        ///    tolerances moves both, along the coil's axis. A flight on model 63's drone.json (no coil.housing: the fixture
        ///    Model63Drone, with tether.json's three spool rows at model 63's values, GoldenFlights.Model63Tether) and with
        ///    FlightSetup.TetherFountainThroughBody steps as in 63, but for one fix the canister's damage bench found: a blade's
        ///    pass whose meeting point lies inside the blade's root (PropStrikes.Pass) is no pass, the disc there the hub's and
        ///    the bell's; it took the rotor's lost angular momentum over a vanishing lever (lost/r_c) as a tangential impulse. With
        ///    ContactSpec.BladeRoot off a flight passes as in 63.
        /// 62: the line held between the drone's parts (p7d-tether 1.7's part slots, on 59; assigned by D-072; CableChain): a
        ///    drone part holds the line only by what presses it. A part's slot with nothing pressing the node (N 0 at a pass's
        ///    end: a crotch of two parts, the 2 mm band) holds nothing for the rest of the step (Unstuck); a speculative slot whose
        ///    spring ends the first pass pulling the node toward the part is let go (Released). And the energy check's
        ///    safe pass solves a spool segment that paid nothing on the elastic segment's majorizer, not the paying model's
        ///    constant pull (S04's TetherWedges). A pressed slot keeps 1.7's hold. A flight without the line, or with
        ///    FlightSetup.TetherZeroForceHold (the known-wrong: model 59's chain), steps as in 59.
        /// 65: the physics integration 2 with the fiber's chain fixes (unity/physics/physics-integration-2--int65, assigned by
        ///    D-072; the code is the parents' together): 62's chain (a drone part holds the line only by what presses it:
        ///    Unstuck, Released; the safe pass's elastic non-paying spool segment and SafeMissed; FiberContacts meeting a shape
        ///    at its own nearest point) merged onto 64's canister, nozzle exit, body over the fountain, bells, frame, cuts and
        ///    failsafe. 62 changes only CableChain and FiberContacts and 64 the drone, the exit and RotorAir's field, so they
        ///    compose without new code. A flight with FlightSetup.TetherZeroForceHold steps as in 64; one on model 63's
        ///    drone.json (the fixture Model63Drone, GoldenFlights.Model63Tether) with FlightSetup.TetherFountainThroughBody
        ///    and ContactSpec.BladeRoot off steps as in 62 (its chain on 63's drone); a flight without the line steps as in 64.
        /// 66: p7f-damage 1.6, the shafts and the bearings (on 63; unity/physics/physics-integration-2--motor-shaft, assigned by D-072;
        ///    64 and 65 are the parallel lines'): with drone.json's internals and props.blade (Contact.MotorShaft), a blade pass's
        ///    face impulse over its contact time, a resting prop's blades' load and a bell ring's load bend the shaft past its yield
        ///    moment at the upper bearing (a plastic hinge in the rotating frame: Propulsion.SetShaft's unbalance, couples and tilted
        ///    thrust; the bell's run-out rubbing past the gap), break it past its ultimate (PropLost: Propulsion.LoseProp), and
        ///    brinell a bearing past C0r (a drag beside the bell's, a ball-pass line into CameraVibration, an output only); a blade
        ///    broken at its root logs BladeLost. A flight whose shafts never pass their yield and whose bearings never pass C0r steps
        ///    as in 63 over its 3040 bytes, the new block zero; with ContactSpec.MotorShaft off, as in 63.
        /// 68: the physics integration 2 with the motor's shaft and bearings (unity/physics/physics-integration-2--int68, assigned
        ///    by D-072; 67 is a parallel line's; the code is the parents' together): 66's shafts and bearings merged onto 65's
        ///    canister, nozzle exit, chain fixes, body over the fountain and blade root. The shaft takes its loads where 65's
        ///    passes and rests put them (a pass inside the blade's root, ContactSpec.BladeRoot, is none, so it loads no shaft). A
        ///    flight whose shafts never pass their yield and whose bearings never pass C0r steps as in 65 over its 3040 bytes, the
        ///    new block zero; with ContactSpec.MotorShaft off, as in 65; on model 63's drone.json (the fixture Model63Drone,
        ///    GoldenFlights.Model63Tether), through the body, with FlightSetup.TetherZeroForceHold and ContactSpec.BladeRoot
        ///    off, as in 66.
        /// 70: p7f-damage 1.6b, the motors' heat and dirt (on 68; unity/physics/physics-integration-2--motor-heat, assigned by D-072;
        ///    69 is a parallel line's): with drone.json's internals (Contact.MotorHealth), each motor's windings take I²R from its
        ///    real current and its stator and bell its losses, cooled by its own spin's air, the airspeed, natural convection,
        ///    radiation and its arm; the copper's heat raises the chain's resistance (Propulsion.SetWinding), the magnets' lowers
        ///    Kt, the insulation's rating shorts a coil (Kt and R down, a loop's drag and heat) and its breakdown fails the motor
        ///    (Propulsion.Fail: no drive, a three-phase short's brake); soil a turning bell scrapes or a blade carves next to it
        ///    enters the gap, drags by its cohesion and, packed, binds. Every flight with the bells heats, so every golden moves;
        ///    with ContactSpec.MotorHeat off a flight steps as in 68 over its 3168 bytes, the new block zero.
        /// 69: D-086's controller (unity/physics/physics-integration-2--esc-airmode, on 65; assigned by D-072): FcSettings.Default
        ///    flies without Betaflight's airmode (the pilot's answer): below half throttle the mixer keeps 0.5 + throttle of the PID
        ///    mix, and under min_check the I term is held at zero. The settings ride in the header as before, so a flight with
        ///    airmode on steps as in 65. The ESC's disarm is unchanged: drone.json's BLHeli_S already has brake on stop off (its
        ///    damped light brakes in the run loop, then every FET off). The record, the header and the events are 65's (format 55).
        /// 73: the physics integration 2 with D-086's controller (unity/physics/physics-integration-2--int73, assigned by D-072;
        ///    71 and 72 are parallel lines'; the code is the parents' together): 69's controller (no airmode in FcSettings.Default,
        ///    MixerSaturation counting only the mix's range and the PID-sum limits) merged onto 70's heat and dirt, shafts,
        ///    bearings, bells, canister, cuts and chain fixes. 69 changes only the controller's settings and the mixer's loss and 70
        ///    the contacts and Propulsion, so they compose without new code. A flight with airmode on steps as in 70; one with
        ///    ContactSpec.MotorHeat off whose shafts never pass their yield (every golden) steps as in 69 over 63's 3040 bytes.
        /// 72: p7f-damage 1.6, the losses, a lost prop and a lost blade graded (unity/physics/physics-integration-2--prop-loss, on 68; assigned
        ///    by D-072; 69–71 are the parallel lines'): each arm in its plane (Flight.ArmFlex, AirframeModesSpec.ArmInPlane): the tip's
        ///    bend across the arm and the arm's twist carry the rotor's in-plane 1/rev to the body, the twist tilts the thrust and
        ///    turns the spinning rotor, and an arm past its strength breaks (ArmBroken, its prop lost); the gyro's range clips each
        ///    axis the controller reads at ±FlightSetup.GyroRange (2000 °/s). With ArmInPlane off and GyroRange 0 (the clean sensors) a
        ///    flight steps as in 68 over its 3168 bytes, the new block zero.
        /// 75: the physics integration 2 with the losses (unity/physics/physics-integration-2--int75, assigned by D-072; 74 is a
        ///    parallel line's; the code is the parents' together): 72's arms in their plane, the gyro's range and Propulsion.StopEsc
        ///    merged onto 73's heat and dirt and D-086's controller. The arms carry the rotors' in-plane loads, the heat reads the
        ///    windings' current and sets their resistance and Kt, the controller reads the clipped gyro; a stopped ESC and a failed
        ///    motor both leave the bus (no drive), a failed one alone brakes. With the clean sensors (ArmInPlane off, GyroRange 0) a
        ///    flight steps as in 73 over its 3360 bytes; with airmode on and ContactSpec.MotorHeat off as in 72 over 68's 3168 bytes
        ///    and the arms' block.
        /// 74: p7d-tether 1.8, the fiber cuts where the pilot says (D-086, D-087; on 70; unity/physics/physics-integration-2--fiber-cut2,
        ///    assigned by D-072; 71–73 are parallel lines'): the nozzle bent at its collar so the line leaves its hole 27.5° down
        ///    (drone.json's coil_nozzle contact, the canister still at 15°; tether.json's exit), the hole's sharp edge
        ///    (spool.lip_radius_m 0.3 mm for 64's 4 mm flared lip) under the exit's tension as the one-way spool lets it out (its law
        ///    paying, its hold else, never a segment stretched past it in the step's end), and its wear (FiberCuts.WearLip: the line
        ///    paying out over it and its lead turning round it, at spool.lip_archard_wear_coefficient beyond the angle the bore
        ///    passes it free), and the peel's rate law referred to the published payout rate (spool.peel_reference_rate_m_s 100 m/s
        ///    for 1). Three new
        ///    rows in the tether entry, which writes every row (the format unchanged). On model 70's drone.json (the fixture
        ///    Model70Drone) with model 70's exit, lip and peel (GoldenFlights.Model70Tether; its hole's radius 0, so no lead turns
        ///    round it) a flight steps as in 70.
        /// 76: the climb after a pitch-back (D-087; unity/physics/physics-integration-2--climb, on 75; assigned by D-072): each rotor's
        ///    thrust and torque take the edgewise flow through its disc (RotorFlappingSpec.EdgewiseThrust): the props' measured axial
        ///    tables times the blade elements' ratio in combined flow with Glauert's inflow, η(J, μ) (translational lift, and the
        ///    climb's damping in forward flight). A flight without flapping, or with EdgewiseThrust off, steps as in 75.
        /// 77: the physics integration 2 with the fiber's cut at the hole's edge (unity/physics/physics-integration-2--chain-spikes,
        ///    assigned by D-072; 75 and 76 are parallel lines'): 74's bent nozzle, sharp edge, its wear and the peel's reference
        ///    merged onto 73's controller (they touch disjoint code); with airmode on the merge steps as in 74. Then p7d-tether 1.8's
        ///    finding 2, the span's tension spikes at the drone's parts, at their cause: the chain solved its step on the start's
        ///    linearisation alone. A taut segment's model was its tangent at the start, so a segment the solve rotated by δ ended
        ///    stretched by δ²/(2ℓ), which no force in the solve made (4.7 N read for 0.03 N applied on a 10 mm segment turned 1 mm),
        ///    and one modelled slack that ended taut once the correction passes were spent carried tension nothing applied; the cuts
        ///    broke the line on both. While a segment ends taut with its tension more than CableChain.NewtonTolerance (1 mN) from its
        ///    model's, the step is solved again from its start with its exact tangent at the last solve's end (CableChain.Relinearise:
        ///    Newton on the segments, after the active set, one way, slack to taut, at most CableChain.MaxNewton 8 solves a step; the
        ///    spool's segment keeps its own model; the safe solve its start's majorizers). With FlightSetup.TetherLinearStep (and with
        ///    FlightSetup.TetherZeroForceHold) a flight steps linearly: as the merge (and with airmode on too as in 74); every flight
        ///    without the line steps as before.
        /// 78: the physics integration 2 with the fiber's Newton step and its cut criteria (unity/physics/physics-integration-2--int78,
        ///    assigned by D-072; 76 is a parallel line's; the code is the parents' together): 77's bent nozzle, sharp edge, its wear,
        ///    the peel's reference and CableChain's Newton step merged onto 75's arms in their plane, the gyro's range, the heat and
        ///    D-086's controller. The chain reads the drone's pose and parts, the arms and the heat the rotors: they compose without
        ///    new code. With FlightSetup.TetherLinearStep, model 70's exit, lip and peel and the clean sensors a flight steps as in
        ///    73 (and so, with the line, as 77's linear step on 73's controller); with the clean sensors as in 77 over its 3360
        ///    bytes; with the linear step, 70's exit, lip and peel as in 75; every flight without the line steps as in 75.
        /// 79: the physics integration 2 with the climb's edgewise thrust and the fiber's Newton step (unity/physics/physics-integration-2--int79,
        ///    assigned by D-072; the code is the parents' together): 76's edgewise flow through each disc (RotorFlappingSpec.EdgewiseThrust)
        ///    merged onto 78's bent nozzle, sharp edge, its wear, the peel's reference and CableChain's Newton step. The rotors read the
        ///    air and the chain the drone's pose and parts: they compose without new code. With EdgewiseThrust off a flight steps as
        ///    in 78; with FlightSetup.TetherLinearStep and model 70's drone.json, exit, lip and peel as in 76; every flight without the
        ///    line steps as in 76.
        /// 82: the flight on hills (w3-farmland-ground 2.2, on 79; unity/physics/physics-integration-2--w3-ground; 80 and 81 are parallel
        ///    lines'): on a terrain (Geometry(Ground, 0) false) a support takes its ground from its own sweep's contact (a facet, a step's
        ///    face, the lip between them), a skid's sweep leaves the ground out, a prop's blades meet the ground as the plane of their
        ///    disc's contact (ContactModel.GroundPlane), and a fiber node behind a step's face takes the face; a sample's overlap is
        ///    r − n_y·(y − h), the same bits on a level plane; a certified fiber segment's ends move the margin less KeepGap, not less the
        ///    line's radius; and a leg's anchor takes no slide along a slip it holds no stiffness along (ContactModel.NilStiffness). With
        ///    w3-farmland-ground 2.6 (unity/physics/physics-integration-2--w3-blade) a crash creates no energy: a prop's blade that
        ///    comes back to rest inside what it touches after the strikes judged its rotor's passes (a glance through a ground the
        ///    strikes do not know, a bend over a face) meets it unloaded, its pairs' overlap there their slack, which only shrinks
        ///    (A4's crash into R01_A: a blade 85 mm inside the floor gave the body 490 J in one step); a rotor back at rest before any
        ///    pass presses as before. Every flight on a plane whose certified segments no shape came within KeepGap of, whose legs
        ///    never slid along a nil stiffness, and whose blades never came back to rest inside anything after a pass, steps as in 79.
        ///    With 2.5 (the same change, unmerged; no model-82 log of a terrain was kept): on a terrain the fiber lies
        ///    over its crests (FiberLine.Crests): a segment whose chord passes more than InsertGap under the ground takes a node at its
        ///    deepest point, on the ground there, and a node the ground holds over the chord between its neighbours is never merged
        ///    away; the chain holds FiberLine.CrestNodes such nodes besides the spacing law's. A flight on a plane steps as before.
        /// 84: the wind over the farmland's hills (w7-farmland-wind 1.1, on 82; unity/physics/physics-integration-2--w7-wind; 83 is
        ///    W3's merge-back, assigned by D-072): every wind query takes its profile over the ground under its own point
        ///    (WindField.At(point, ground)): each rotor, the body, the coil and the camera SampleGround's height under it (over a hole
        ///    the step's ground and cut frame, model 20's soil under the origin), each fiber node and loose piece its own; and a
        ///    pilot's level over a terrain carries its hills (HillFlow: Jackson and Hunt's linear theory in Hunt et al.'s layers,
        ///    spectral, from the world's heights; a step taller than the cover's still layer separates its lee, Cavity's bubble
        ///    behind a step). On a plane a flight steps as in 82 while its body's origin, its fiber's nodes and its loose pieces lie
        ///    over the plane itself, not a hole: the flight goldens are 82's to the bit.
        ///    Extended by w3-farmland-ground 2.4 (on unity/physics/physics-integration-2--w3-ground; the orchestrator renumbers it at
        ///    the merge, D-072): the laid line rests and freezes. A node is at rest on a support it presses or lies within InsertGap
        ///    of while its holds stick there (no Coulomb slip, no line sliding through) or it moves slower than
        ///    trail.freeze_speed_m_s, and node 1 at rest trail.freeze_seconds freezes whatever air it bears, keeping that air and its
        ///    tension; the trail's last point lifts back only when the chain's pull at it across, up or back over its laid run passes
        ///    the static friction of the line it lumps, or along the run passes its held tension by more than the run's friction
        ///    takes before a trail point it reaches would move, or the air at it passes what it held at rest by the ground's
        ///    friction; a point lifted back lies where it rested, and a laid node the ground holds across its chord (the line's turn)
        ///    is held against the spacing law as a crest is. Over a hole a node's soil is cast once a step (the wind's posting and
        ///    the ground pass share it). The header's tether entry gains FlightSetup.TetherSlidingTrail (the known-wrong trail before
        ///    2.4) after TetherLinearStep; a flight with it steps as 84 did, and every golden but the tether's is 84's to the bit.
        /// 85: the pilot's farmland preview (unity/physics/physics-integration-2--w3-preview, D-072): 84 with w3-farmland-ground
        ///    2.6 (a crash creates no energy, in 82's note above), 2.4 (the laid line rests, in 84's note above) and 2.7 (below)
        ///    merged in. A farmland log from the preview names this core. 83 was reserved for W3's merge-back and never used: W7
        ///    1.1's 84 merged first. With w3-farmland-ground 2.7 (unity/physics/physics-integration-2--w3-contact), contact with the
        ///    farmland's objects: a part's new pair on a rigid box that begins inside it with no first touch
        ///    takes the face the line from the body's origin to its core enters by, not the query's nearest (a 2 cm skin's far face
        ///    pushed the part through); a resting blade on a box its hub's line enters by another face lies in the slot it cut (past
        ///    the far face no touch, inside the root's face, its overlap slack); a box whose cut holds the tip's circle beside the
        ///    blade's line is a face the tip meets, though the line leaves it inside the tip, and a carve sweeps its strip only
        ///    (PropStrikes, BladeCut.Wide); a shape holds 24 pairs, not 8; a rotor turning in a crown's twig shell with surfaces.json
        ///    cuts the twigs its disc's motion brings in at their cost (PropStrikes.Twigs), never a glance; without surfaces.json a
        ///    blade at rest passes the ground and the soil shapes as its passes glance through them, never bearing on a floor its rotor
        ///    sank into; and a prop lost in a step loads no shaft from its resting blades.
        /// 86: the anchors' reach (w9-village-physics 2.4, on 85; unity/physics/physics-integration-2--w9-anchors, shared by
        ///    the change's 2.6 and 2.7 until a pilot build ships it, D-072): a leg point's anchor stretch (the foot's move from the
        ///    body's point to where the ground holds it) is held within what the leg reaches by turning its lever square
        ///    (ContactSpec.LegLeverLength across the tube, LegFrameLeverLength along the skid's frame, by the square of the way's
        ///    cosine to the frame), in a soil corner's trial and after the cone's slide of a soil foot or a leg point; past it the
        ///    ground has let the foot go and the hold starts again where the foot is (the stretch 0). Before it a slide past a
        ///    spent side give divided the trial's whole excess by the
        ///    stretch's own stiffness (a lever's statics and dampers it cannot relieve) and ran the stretch, and the moment's
        ///    lever with it, to 19–87 m in a few steps (1,455 N·m on 41.5 N at 9.98° weeds). ContactModel.AnchorReach off (unlogged, the suite's) is 85's
        ///    slide, the known-wrong. A flight whose stretches stay within the reach steps as 85 did.
        ///    Also under 86 (w9-village-physics 2.7): a resting blade's load radius is held to its tip, and no blade breaks at a
        ///    station at or past its tip. The stems on the props (2.2, PropStems) flew on this line under 86 and 88 until model
        ///    89 named them together with 2.12's stalks (87 was never used).
        /// 88: the wind over the farmland's belts and ruins (w7-farmland-wind 1.2, on 86; built on
        ///    unity/physics/physics-integration-2--w7-wind as 84 extended, merged onto the line by
        ///    unity/physics/physics-integration-2--w9-wind; assigned by D-072, 87 is the stems' line): the wake grid's version 2
        ///    (WakeGrid.Version, logged per flight in the wind entry as WakeLayout.Version; a log of version 1 replays its own
        ///    grid). The farmland has no shelterbelt: its trees stand in groves and short rows, its bushes in hedges, its ruins are
        ///    thin-walled shells, and version 1 read them wrong. Its cell rule diluted a 0.24 m wall or missed it between samples,
        ///    multiplied a wall's stacked courses and a trunk under its crown as if in series, and the grid kept the nearest wake
        ///    alone, so a hedge 14 m behind a tree row ended the row's shelter. Now an obstacle's porosity is its frontal porosity
        ///    across each lane from its volumes' geometry (WakeObstacles.Transmission: solids opaque however thin, porous parts by
        ///    Beer–Lambert over their chord, parts at different heights side by side; a cell is an obstacle where a volume's
        ///    footprint overlaps it), each cell carries the overlying wake of the nearest taller obstacle upwind (speeds multiply,
        ///    excess variances add), and the porous boost scales with the obstacle's deficit (1 − β). The range golden moved
        ///    (re-pinned; with version 1 it is model 76's pin), and every kept audit of an older model flies version 1. A flight
        ///    with no wind volumes, or one flown on version 1, steps as 86 did.
        /// 89: the stems on the props (w9-village-physics 2.2 and 2.12, on 88; unity/physics/physics-integration-2--w9-cut and
        ///    --w9-straw): a rotor's disc meets the map's stem field (IWorldQuery.StemsNear, QueryVersion 9) and the range's
        ///    straw. Field stems are placed one by one by the world (each from its own hash: its place, its natural lean, its
        ///    drawn length), sized by allometry from that length (d = d₁·L^(1/1.53)), and folded by the slipstream at their own
        ///    pace (a vertical jet loads only the part that leans; a folded stalk is an arch). A blade cuts a stem only where its
        ///    speed across the stem's axis reaches the stem's critical cutting speed, and each cut is a discrete hit: its edge work
        ///    and fling from the rotor, its push on the blade and the arm (AirframeModes.Strike), a stem episode in the log (event
        ///    39). A strike along a rotor's axis loads its arm's mode at the tip (2.2's G8). A flight that meets no stem and no
        ///    axial strike steps as 88 did.
        /// 90: each leg's own stiffness, and the anchors' reach in the header (w9-village-physics 2.9, on 89;
        ///    unity/physics/physics-integration-2--w9-liftoff): a drawn build's four legs each take a stiffness factor 1 + δ
        ///    from the stream leg.stiffness (σ 3 %, within ±10 %, assumed; D-086's hand-made legs), which scales the tube's axial
        ///    and bending springs, the frame's give and the buckling cap in the contacts and in the legs' failure law. It is
        ///    drawn only where the build asks (LegDamageSpec.DrawStiffness, as the placement and the motors' asymmetry are), so
        ///    a flight that does not ask steps as 89 did. ContactSpec.AnchorReach replaces ContactModel's unlogged switch and is
        ///    written in the header's ground entry (TagGround) with the spread's σ and bound; a log without the entry reads as
        ///    its model flew.
        /// 91: the crowns' twigs and leaves, and the reach on every hold (w9-village-physics 2.3, on 90;
        ///    unity/physics/physics-integration-2--w9-crowns): a crown's twig shell holds a part only as its twigs can. A part
        ///    meets L_V·p/4 twigs a metre of travel (stereology on its silhouette's perimeter), each breaking at its own bending
        ///    strength, so a pair's stiffness is the twigs' mean work over their mean deflection, it has no damper, its plane
        ///    yields with the part past that deflection, only a part's deepest crown pair carries, and a leg's push on twigs is
        ///    capped at its half-skid's hold. A 4.7 kg drone falls through twigs and rests only on a branch or the ground; a
        ///    resting blade is no longer held, so the props turn in a crown and PropStrikes.Twigs cuts what they meet. The
        ///    crown's leaves are flung by the blades (cut only above their critical speed) and stay cleared where a disc has
        ///    passed. A leg's anchor lets go past the leg's reach on every hold (the ground, a soil shape, a rigid shape, a crown),
        ///    where 86 released the ground's only. The step's event buffer is sized from what a step can raise, so no event is
        ///    refused. The crown block is written in the header's crown entry (TagCrown); a log without it reads as its model
        ///    flew. A flight that touches no crown, no leaf and no object's hold past the reach steps as 90 did.
        /// </summary>
        public const int ModelVersion = 91;

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
        /// deflections (p7f-damage 1.3b), then the motors' bells (1.5), then the shafts and the bearings (1.6), then the motors'
        /// heat and dirt (1.6b), then the arms in their plane, the gyro's peak and the arms broken (1.6, the losses).</summary>
        public const int StepSize = 3536, EventSize = 72, EndSize = 96;

        /// <summary>A format-69 log's end block: the six counts, without the step times.</summary>
        public const int Format69EndSize = 48;

        /// <summary>The record before the arms' block (models 70–73's, formats 60–63: through the heat's block), over which a flight
        /// with the clean sensors (its arms stiff in their plane, no gyro range) keeps model 73's bits.</summary>
        public const int Model73StepSize = 3360;

        /// <summary>The record before the motors' heat (models 66–68's, formats 56–58: through the shafts' block), over which a flight
        /// without the heat and with the clean sensors keeps model 68's bits; model 72's record is these bytes and the arms' block
        /// (from Model73StepSize on).</summary>
        public const int Model68StepSize = 3168;

        /// <summary>The record before the shafts (models 58–63's, formats 48–53: through the bells' block), over which a flight whose
        /// shafts and bearings never yield keeps model 63's bits.</summary>
        public const int Model63StepSize = 3040;

        /// <summary>The record before the motors' bells (models 52–57's, formats 42–47: through the resting blades' block), over which
        /// a flight whose bells never touch keeps model 57's bits.</summary>
        public const int Model57StepSize = 2944;

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
              "RingInflow", "RingThrust", "RingLatched", "WashInflow", "ModeShape", "ModeRate", "InterferenceShare", "WakeInflow", "BladeStrikes",
              "ArmBend", "ArmBendRate", "ArmTwist", "ArmTwistRate", "ArmBroken" };
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
                "BladeLoad0", "BladeLoad1", "BladeBend0", "BladeBend1", "BellScrape", "BellRub", "BellKtChange",
                "ShaftBendP", "ShaftBendQ", "BearingDrag", "BallPassOuter",
                "WindingTemperature", "StatorTemperature", "HeatKtChange", "DirtMass", "DirtDrag", "MotorState",
                "ArmLoad", "GyroPeak",
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
        public static readonly LogField[] EndFields = BuildEndFields(true);
        public static readonly LogField[] Format69EndFields = BuildEndFields(false);
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
            // The motors' bells (p7f-damage 1.5).
            b.Corners("bell_scrape", "N.m, the bell's scrape against its turning rotor (at rest its static friction's hold); 0 without the bells");
            b.Corners("bell_rub", "N.m, the magnets' rub on the stator's teeth (the press past the gap, a dent's interference)");
            b.Corners("bell_kt_change", "the motor's Kt over the drawn one less 1, from its gap's flux (0 for a gap as new, and without the bells)");
            // The shafts and the bearings (p7f-damage 1.6).
            b.Corners("shaft_bend_p", "rad, the shaft's plastic hinge along its rotor's blade 0 (a rotation vector's part, the rotor's frame); 0 straight");
            b.Corners("shaft_bend_q", "rad, the shaft's plastic hinge across blade 0 in the disc (q = p x up)");
            b.Corners("bearing_drag", "N.m, the brinelled bearings' added drag at the rotor's thrust; 0 for sound bearings");
            b.Corners("ball_pass_outer", "Hz, a brinelled bearing's outer-race ball-pass frequency at the rotor's speed; 0 for sound bearings");
            // The motors' heat and dirt (p7f-damage 1.6b).
            b.Corners("winding_temperature", "degC, the windings' temperature; 0 without the motors' heat");
            b.Corners("stator_temperature", "degC, the stator and bell's temperature (the magnets')");
            b.Corners("heat_kt_change", "the motor's Kt factor by heat less 1 (the magnets' remanence and a shorted coil)");
            b.Corners("dirt_mass", "kg, the soil in the motor's gap");
            b.Corners("dirt_drag", "N.m, the dirt's drag on the rotor (its cohesion bridged, packed past the gap)");
            b.Corners("motor_state", "0 sound, 1 its magnets past their maximum, 2 a coil shorted, 3 failed");
            // The arms in their plane and the gyro's range (p7f-damage 1.6, the losses).
            b.Corners("arm_bend", "m, the arm's tip bent across the arm in the arm plane (along arm x up); 0 without the arms in their plane");
            b.Corners("arm_bend_rate", "m/s, its rate");
            b.Corners("arm_twist", "rad, the arm's twist about its own axis (outward from the centre) at its tip");
            b.Corners("arm_twist_rate", "rad/s, its rate");
            b.Corners("arm_load", "the arm root's largest stress over the step over the plate's strength (bend or twist, the larger); past 1 it broke");
            b.Add("gyro_peak", "deg/s, the gyro's largest axis this step before its range clips it", LogFieldType.F64);
            b.Add("arm_broken", "the arms broken at their clamp, bit 1 << corner", LogFieldType.U8);
            b.Add("arm.reserved", "zero", LogFieldType.Pad, 7);
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

        static LogField[] BuildEndFields(bool times)
        {
            var b = new Layout();
            b.Add("steps", "step records written", LogFieldType.I64);
            b.Add("events", "event records written, hitches included", LogFieldType.I64);
            b.Add("hitches", "scheduler hitches", LogFieldType.I64);
            b.Add("log_waits", "times the flight thread waited for a full ring", LogFieldType.I64);
            b.Add("sticks_dropped", "stick reports refused by a full inbox", LogFieldType.I64);
            b.Add("events_lost", "events refused by a full step buffer", LogFieldType.I64);
            if (!times) return b.Done(Format69EndSize);
            // Format 72: each step's advance on the host clock (FlightScheduler.Times).
            b.Add("step_times", "steps timed on the host clock", LogFieldType.I64);
            b.Add("step_overruns", "steps whose advance took over 1 ms", LogFieldType.I64);
            b.Add("step_p50_us", "us, nearest rank in 1 us bins to 10 ms, then the max", LogFieldType.I64);
            b.Add("step_p99_us", "us, nearest rank in 1 us bins to 10 ms, then the max", LogFieldType.I64);
            b.Add("step_p999_us", "us, nearest rank in 1 us bins to 10 ms, then the max", LogFieldType.I64);
            b.Add("step_max_us", "us, the slowest step's advance", LogFieldType.I64);
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
            w.C4(t.BellScrape);
            w.C4(t.BellRub);
            w.C4(t.BellKtChange);
            w.C4(t.ShaftBendP);
            w.C4(t.ShaftBendQ);
            w.C4(t.BearingDrag);
            w.C4(t.BallPassOuter);
            w.C4(t.WindingTemperature);
            w.C4(t.StatorTemperature);
            w.C4(t.HeatKtChange);
            w.C4(t.DirtMass);
            w.C4(t.DirtDrag);
            w.C4(t.MotorState);
            w.C4(s.ArmBend);
            w.C4(s.ArmBendRate);
            w.C4(s.ArmTwist);
            w.C4(s.ArmTwistRate);
            w.C4(t.ArmLoad);
            w.F64(t.GyroPeak);
            w.U8(s.ArmBroken);
            w.Zero(7);
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
            t.BellScrape = r.C4();
            t.BellRub = r.C4();
            t.BellKtChange = r.C4();
            t.ShaftBendP = r.C4();
            t.ShaftBendQ = r.C4();
            t.BearingDrag = r.C4();
            t.BallPassOuter = r.C4();
            t.WindingTemperature = r.C4();
            t.StatorTemperature = r.C4();
            t.HeatKtChange = r.C4();
            t.DirtMass = r.C4();
            t.DirtDrag = r.C4();
            t.MotorState = r.C4();
            s.ArmBend = r.C4();
            s.ArmBendRate = r.C4();
            s.ArmTwist = r.C4();
            s.ArmTwistRate = r.C4();
            t.ArmLoad = r.C4();
            t.GyroPeak = r.F64();
            s.ArmBroken = r.U8();
            r.Skip(7);
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
            w.I64(end.Times.Count);
            w.I64(end.Times.Overruns);
            w.I64(end.Times.P50Us);
            w.I64(end.Times.P99Us);
            w.I64(end.Times.P999Us);
            w.I64(end.Times.MaxUs);
        }

        /// <summary>An end block of EndSize bytes, or a format-69 one of Format69EndSize (its Times stay zero).</summary>
        public static void DecodeEnd(ReadOnlySpan<byte> src, out LogEnd end)
        {
            bool times = src.Length >= EndSize;
            var r = new Reader(src.Slice(0, times ? EndSize : Format69EndSize));
            end = new LogEnd
            {
                Steps = r.I64(),
                Events = r.I64(),
                Hitches = r.I64(),
                LogWaits = r.I64(),
                SticksDropped = r.I64(),
                EventsLost = r.I64(),
            };
            if (!times) return;
            end.Times = new StepTimeSummary
            {
                Count = r.I64(),
                Overruns = r.I64(),
                P50Us = r.I64(),
                P99Us = r.I64(),
                P999Us = r.I64(),
                MaxUs = r.I64(),
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

    /// <summary>The end block: what the recorder wrote and what the flight lost, counted at the close, and the step times
    /// (format 72; zero in a format-69 log).</summary>
    public struct LogEnd
    {
        public long Steps, Events, Hitches, LogWaits, SticksDropped, EventsLost;
        public StepTimeSummary Times;
    }
}
