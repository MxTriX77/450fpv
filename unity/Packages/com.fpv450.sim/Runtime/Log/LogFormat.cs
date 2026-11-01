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
        /// </summary>
        public const int Version = 12;

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
        /// 16: p6-flight-core 1.6b (a skid capsule on the face its support spheres stand on is their support, and leaving a
        ///    compliant shape is no tunnel; a flight on the stub's floor steps as in 15).
        /// </summary>
        public const int ModelVersion = 16;

        public const ushort StepBlock = 1, EventBlock = 2, EndBlock = 3;

        /// <summary>A block's kind and payload length, u16 each.</summary>
        public const int BlockHeaderSize = 4;

        /// <summary>The step record: 352 B of 1.1–1.3's fields, then the flight controller's 69 doubles and flags (1.4),
        /// then the gust, the air's force and moment and the ESCs' limit (1.5), then the contacts' force and moment, the
        /// legs' loads and compressions and the touching pairs (1.6), then the controller's filter outputs and battery
        /// (1.4b), then the rotors' angles (p7b-airframe 1.1), then the vortex rings (1.2), then the rotors' surfaces and
        /// re-ingested wash (1.3), then the airframe's modes (1.4).</summary>
        public const int StepSize = 1656, EventSize = 72, EndSize = 48;

        // What the step record covers, checked against the structs by the suite: a field added to one of them must be
        // logged (a new layout) or the suite fails.
        public static readonly string[] StateFieldsLogged =
            { "Step", "Position", "Velocity", "Attitude", "BodyRate", "Armed", "RotorSpeed", "EscCommand", "PackCharge0", "PackCharge1", "Fc", "Gust", "EscLimited", "Crashed", "RotorAngle",
              "RingInflow", "RingThrust", "RingLatched", "WashInflow", "ModeShape", "ModeRate" };
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
                "ContactForce", "ContactMoment", "LegLoad", "LegCompression", "ContactsTouching", "GroundDistance", "CeilingDistance",
            };

        static readonly string[] CornerNames = { "rear_right", "front_right", "rear_left", "front_left" };

        public static readonly LogField[] StepFields = BuildStepFields();
        public static readonly LogField[] EventFields = BuildEventFields();
        public static readonly LogField[] EndFields = BuildEndFields();

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
            b.Vec("gust", "m/s, world, the stand-in wind's gust over its mean");
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
            return b.Done(StepSize);
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
            w.V3(s.Gust);
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
            s.Gust = r.V3();
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
