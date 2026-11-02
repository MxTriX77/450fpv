using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Log
{
    /// <summary>A step record, decoded.</summary>
    public struct LoggedStep
    {
        public FlightState State;
        public StickSample Stick;
        public StepTrace Trace;
    }

    /// <summary>An event record, decoded, with the step that made it.</summary>
    public struct LoggedEvent
    {
        public long Step;
        public FlightEvent Event;
    }

    /// <summary>
    /// Reads a flight log block by block (design 10, LOG-02). A log cut anywhere (the game killed, the disk full) reads
    /// to its last whole block: Next then returns false with CutAt at the first byte it could not use and CutReason
    /// saying why. A log closed properly ends with its end block (Complete, End). A block kind this build does not know
    /// is skipped by its length. The header must be whole and of this format version, or the constructor throws
    /// FlightLogException.
    /// </summary>
    public sealed class FlightLogReader : IDisposable
    {
        readonly Stream _stream;
        readonly bool _ownsStream;
        readonly byte[] _head = new byte[LogFormat.BlockHeaderSize];
        readonly byte[] _payload = new byte[ushort.MaxValue];
        int _length;
        long _next;

        /// <summary>Opens a log file, also one still being written.</summary>
        public static FlightLogReader Open(string path) =>
            new FlightLogReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16), true);

        /// <summary>Reads the header from stream, which starts at the log's first byte.</summary>
        public FlightLogReader(Stream stream, bool ownsStream = false)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _ownsStream = ownsStream;
            try
            {
                Header = LogHeader.Read(stream);
                if (Header.LayoutsDiffer(out string which))
                    throw new FlightLogException("the log's " + which + " record layout is not the one of format " + LogFormat.Version);
            }
            catch
            {
                if (ownsStream) stream.Dispose();
                throw;
            }
            _next = Header.Length;
        }

        public LogHeader Header { get; }

        /// <summary>The kind of the block Next just read (LogFormat.StepBlock or EventBlock).</summary>
        public ushort Kind { get; private set; }

        /// <summary>The byte offset where the block Next just read starts.</summary>
        public long Position { get; private set; }

        /// <summary>The payload of the block Next just read.</summary>
        public ReadOnlySpan<byte> Payload => new ReadOnlySpan<byte>(_payload, 0, _length);

        /// <summary>The end block was read: the log was closed properly. End holds its counts.</summary>
        public bool Complete { get; private set; }

        public LogEnd End { get; private set; }

        /// <summary>−1, or where a log without its end block stops being readable (its length if it ends between blocks).</summary>
        public long CutAt { get; private set; } = -1;

        public string CutReason { get; private set; }

        /// <summary>Blocks of a kind this build does not know, skipped.</summary>
        public long UnknownBlocks { get; private set; }

        /// <summary>The next step or event block; false at the end block or where the log is cut.</summary>
        public bool Next()
        {
            while (true)
            {
                if (Complete || CutAt >= 0) return false;
                long start = _next;
                int got = LogHeader.ReadFully(_stream, _head, LogFormat.BlockHeaderSize);
                if (got == 0) return Cut(start, "the log ends after a whole block, without its end block");
                if (got < LogFormat.BlockHeaderSize) return Cut(start, "the log is cut inside a block's header");
                ushort kind = BinaryPrimitives.ReadUInt16LittleEndian(_head);
                int length = BinaryPrimitives.ReadUInt16LittleEndian(_head.AsSpan(2));
                int expected = kind == LogFormat.StepBlock ? LogFormat.StepSize
                    : kind == LogFormat.EventBlock ? LogFormat.EventSize
                    : kind == LogFormat.EndBlock ? LogFormat.EndSize : -1;
                if (kind == 0) return Cut(start, "a block of kind 0: the rest of the file is not log data");
                if (expected >= 0 && length != expected)
                    return Cut(start, "a " + KindName(kind) + " block of " + length + " bytes, not " + expected);
                got = LogHeader.ReadFully(_stream, _payload, length);
                if (got < length)
                    return Cut(start, "the log is cut inside a " + KindName(kind) + " block, " + got + " of its " + length + " bytes there");
                _next = start + LogFormat.BlockHeaderSize + length;
                _length = length;
                Position = start;
                Kind = kind;
                if (kind == LogFormat.EndBlock)
                {
                    LogFormat.DecodeEnd(Payload, out LogEnd end);
                    End = end;
                    Complete = true;
                    return false;
                }
                if (expected < 0)
                {
                    UnknownBlocks++;
                    continue;
                }
                return true;
            }
        }

        public void ReadStep(out LoggedStep step)
        {
            if (Kind != LogFormat.StepBlock) throw new InvalidOperationException("the current block is not a step record");
            LogFormat.DecodeStep(Payload, out step.State, out step.Stick, out step.Trace);
        }

        public void ReadEvent(out LoggedEvent e)
        {
            if (Kind != LogFormat.EventBlock) throw new InvalidOperationException("the current block is not an event");
            LogFormat.DecodeEvent(Payload, out e.Step, out e.Event);
        }

        public void Dispose()
        {
            if (_ownsStream) _stream.Dispose();
        }

        bool Cut(long at, string reason)
        {
            CutAt = at;
            CutReason = reason;
            return false;
        }

        static string KindName(ushort kind) =>
            kind == LogFormat.StepBlock ? "step" : kind == LogFormat.EventBlock ? "event" : kind == LogFormat.EndBlock ? "end" : "kind " + kind;
    }

    /// <summary>
    /// A flight's summary from its log (design 10, LOG-02): the API the app's post-flight summary calls
    /// (p6-input-app) and what QA reads first. Read never fails on a cut log: it holds the flight up to the last whole
    /// record and says where the log was cut. Only a log whose header is unreadable has no flight (Header null,
    /// HeaderProblem says why).
    /// </summary>
    public sealed class FlightSummary
    {
        /// <summary>A label for Print: the file's name.</summary>
        public string Source = "";

        public LogHeader Header;
        public string HeaderProblem;

        /// <summary>Whole step records read.</summary>
        public long Steps;

        public bool HasSteps;
        public LoggedStep First, Last;

        /// <summary>Steps missing between records; 0 in a sound log.</summary>
        public long Gaps;

        /// <summary>Every event in log order: the timeline.</summary>
        public readonly List<LoggedEvent> Events = new List<LoggedEvent>();

        /// <summary>
        /// The first step of each failsafe (INP-05): its stick's HostUs is when the input layer lost the radio, and the
        /// step is the one it disarmed.
        /// </summary>
        public readonly List<LoggedStep> Failsafes = new List<LoggedStep>();

        /// <summary>The log ended with its end block; End holds its counts.</summary>
        public bool Complete;
        public LogEnd End;

        /// <summary>−1 for a complete log; else the byte where it stops being readable, and why.</summary>
        public long CutAt = -1;
        public string CutReason;

        /// <summary>The flight's time to the last whole record, s.</summary>
        public double Seconds => HasSteps ? Last.State.Step * FlightCore.Dt : 0;

        public int Count(EventKind kind)
        {
            int n = 0;
            foreach (LoggedEvent e in Events)
                if (e.Event.Kind == kind)
                    n++;
            return n;
        }

        public static FlightSummary Read(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16))
            {
                FlightSummary s = Read(file);
                s.Source = Path.GetFileName(path);
                return s;
            }
        }

        public static FlightSummary Read(Stream stream)
        {
            var s = new FlightSummary();
            FlightLogReader reader;
            try
            {
                reader = new FlightLogReader(stream);
            }
            catch (FlightLogException e)
            {
                s.HeaderProblem = e.Message;
                return s;
            }
            s.Header = reader.Header;
            while (reader.Next())
            {
                if (reader.Kind == LogFormat.StepBlock)
                {
                    reader.ReadStep(out LoggedStep step);
                    if (step.Stick.Failsafe && !(s.HasSteps && s.Last.Stick.Failsafe))
                        s.Failsafes.Add(step);
                    if (!s.HasSteps)
                        s.First = step;
                    else if (step.State.Step != s.Last.State.Step + 1)
                        s.Gaps += step.State.Step - s.Last.State.Step - 1;
                    s.Last = step;
                    s.HasSteps = true;
                    s.Steps++;
                }
                else
                {
                    reader.ReadEvent(out LoggedEvent e);
                    s.Events.Add(e);
                }
            }
            s.Complete = reader.Complete;
            s.End = reader.End;
            s.CutAt = reader.CutAt;
            s.CutReason = reader.CutReason;
            return s;
        }

        /// <summary>The summary as text, with units on every value; at most maxEvents lines of the timeline.</summary>
        public void Print(TextWriter w, int maxEvents = 200)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            w.WriteLine("flight log " + Source);
            if (Header == null)
            {
                w.WriteLine("  unreadable: " + HeaderProblem);
                return;
            }
            LogHeader h = Header;
            w.WriteLine("  format " + h.FormatVersion + ", model " + h.ModelVersion + ", seed 0x" + h.Seed.ToString("X16", c));
            w.WriteLine("  build " + h.Build + "; runtime " + h.Runtime + "; isa " + h.Isa);
            w.WriteLine("  world '" + h.WorldId + "': content 0x" + h.WorldHash.ToString("X16", c) + ", query version " + h.QueryVersion
                + "; surfaces " + Names(h.Surfaces) + "; materials " + Names(h.Materials));
            if (h.DroneHash != 0)
                w.WriteLine("  drone '" + h.DroneName + "': drone.json 0x" + h.DroneHash.ToString("X16", c) + "; payload " + h.Loadout.Payload.ToString("0.000", c)
                    + " kg, legs " + (h.Loadout.Legs ? "on" : "off") + ", coil " + h.Loadout.CoilPaidOut.ToString("0.0", c) + " m paid out");
            else
                w.WriteLine("  drone: a bench body, not from drone.json");
            Sym3 i = h.Body.Inertia;
            w.WriteLine("  body: " + h.Parts.Length + " parts, " + h.Body.Mass.ToString("0.000", c) + " kg, CoM " + V(h.Body.CenterOfMass, "0.0000")
                + " m, inertia xx " + G(i.XX) + " yy " + G(i.YY) + " zz " + G(i.ZZ) + " xy " + G(i.XY) + " xz " + G(i.XZ) + " yz " + G(i.YZ) + " kg.m2");
            w.WriteLine("  settings: " + h.Settings.Length + " FcSettings fields by CLI name");
            w.WriteLine("  wind: " + WindLine(h));
            w.WriteLine("  rain: " + h.Rain.ToString().ToLowerInvariant() + (h.RainParameters.Length > 0 ? " (" + h.RainParameters.Length + " preset values)" : "")
                + "; camera lens at " + h.CameraPosition.X.ToString("R", CultureInfo.InvariantCulture) + ", "
                + h.CameraPosition.Y.ToString("R", CultureInfo.InvariantCulture) + ", " + h.CameraPosition.Z.ToString("R", CultureInfo.InvariantCulture) + " m");
            if (h.Aero != null)
                w.WriteLine("  aero: air " + G(h.Aero.AirDensity) + " kg/m3, body silhouette side/top/front " + V(h.Aero.BodyArea, "0.0000")
                    + " m2 at " + V(h.Aero.BodyCenterOfPressure, "0.000") + " m, C_d " + G(h.Aero.DragCoefficient) + ", coil "
                    + (h.Aero.CoilPart >= 0 ? PartName((ushort)h.Aero.CoilPart) : "none"));
            else
                w.WriteLine("  aero: none (a bench body)");
            if (h.Contacts != null)
                w.WriteLine("  contacts: " + h.Contacts.Shapes.Length + " shapes, margin " + G(h.Contacts.Margin) + " m, legs " + G(h.Contacts.LegStiffness)
                    + " N/m and " + G(h.Contacts.LegDamping) + " N.s/m, a crash past " + G(h.Contacts.CrashAccel) + " m/s2 or a prop strike past "
                    + G(h.Contacts.CrashPropSpeed) + " m/s; physics_materials.json 0x" + h.Contacts.MaterialsHash.ToString("X16", c) + " (" + h.Contacts.Materials.Length + " rows)"
                    + (h.Contacts.Surfaces != null
                        ? "; surfaces.json 0x" + h.Contacts.Surfaces.Hash.ToString("X16", c) + " (" + h.Contacts.Surfaces.Surfaces.Length + " surfaces, "
                          + h.Contacts.Surfaces.Cover.Length + " cover kinds)"
                        : ""));
            else
                w.WriteLine("  contacts: none");
            if (h.Asymmetry != null)
                w.WriteLine("  motor asymmetry: Kv sigma " + G(h.Asymmetry.KvSigma) + " within " + G(h.Asymmetry.KvBound) + ", ESC lag within "
                    + G(h.Asymmetry.EscLagBound) + " s, props at balance grade " + G(h.Asymmetry.BalanceGrade) + " m/s ("
                    + G(h.Asymmetry.PropMass) + " kg at " + G(h.Asymmetry.ServiceRpm) + " rpm)");
            else
                w.WriteLine("  motor asymmetry: off (identical rotors)");
            if (h.VortexRing != null)
                w.WriteLine("  vortex rings: in at eta " + G(h.VortexRing.EntryRate) + ", out below " + G(h.VortexRing.ExitRate) + " or past "
                    + G(h.VortexRing.EndRate) + ", swept away at mu " + G(h.VortexRing.EscapeInPlane) + "; inflow " + G(h.VortexRing.EntryInflow)
                    + " to " + G(h.VortexRing.PeakInflow) + " v_h at its peak eta " + G(h.VortexRing.PeakRate) + ", lag " + G(h.VortexRing.InflowLag)
                    + " R/v_h; thrust sigma " + G(h.VortexRing.FluctuationSigma) + " within " + G(h.VortexRing.FluctuationBound) + " at f R/v_h "
                    + G(h.VortexRing.FluctuationFrequency) + "; air " + G(h.VortexRing.AirDensity) + " kg/m3"
                    + (h.VortexRing.FixedInducedVelocity > 0 ? "; KNOWN-WRONG: scaled by a fixed " + G(h.VortexRing.FixedInducedVelocity) + " m/s" : ""));
            else
                w.WriteLine("  vortex rings: off");
            if (h.GroundEffect != null)
                w.WriteLine("  ground effect: Cheeseman-Bennett below, Hsiao-Chirarattananon alpha0 " + G(h.GroundEffect.CeilingAlpha)
                    + " above, rays " + G(h.GroundEffect.Reach) + " R, held at " + G(h.GroundEffect.LowestHeight) + " R; wash sigma "
                    + G(h.GroundEffect.RecirculationSigma) + " v_h within " + G(h.GroundEffect.RecirculationBound) + " below "
                    + G(h.GroundEffect.RecirculationOnset) + " R, loop " + G(h.GroundEffect.RecirculationLoop) + " (z + R)/v_h; air "
                    + G(h.GroundEffect.AirDensity) + " kg/m3" + (h.GroundEffect.RecirculationSigma > 0 ? "" : "; KNOWN-WRONG: no recirculation"));
            else
                w.WriteLine("  ground effect: off");
            if (h.Modes != null)
                w.WriteLine("  modes: arms " + G(h.Modes.ArmWidth * 1000) + " x " + G(h.Modes.ArmThickness * 1000) + " mm plate of " + G(h.Modes.ArmModulus / 1e9)
                    + " GPa over " + G(h.Modes.ArmLength) + " m, " + G(h.Modes.ArmStiffness) + " N/m, damping " + G(h.Modes.ArmDamping) + "; mounts "
                    + G(h.Modes.MountStiffness) + " N/m, damping " + G(h.Modes.MountDamping) + "; camera at (" + G(h.Modes.Camera.X) + ", "
                    + G(h.Modes.Camera.Y) + ", " + G(h.Modes.Camera.Z) + ") m; payload part " + h.Modes.PayloadPart + ", coil part " + h.Modes.CoilPart);
            else
                w.WriteLine("  modes: off (a rigid airframe)");
            if (h.Flapping != null)
                w.WriteLine("  flapping: blade-element, lift slope " + G(h.Flapping.LiftSlope) + "/rad, zero lift " + G(h.Flapping.ZeroLiftAngle)
                    + " rad, bending " + G(h.Flapping.BendingFrequency) + " Hz, Southwell " + G(h.Flapping.Southwell) + ", skew gain "
                    + G(h.Flapping.SkewGain) + ", " + h.Flapping.Blades + " blades of " + G(h.Flapping.BladeInertia) + " kg m2 on R "
                    + G(h.Flapping.Radius) + " m, " + h.Flapping.Stations.Length + " stations; air " + G(h.Flapping.AirDensity) + " kg/m3");
            else
                w.WriteLine("  flapping: off");
            for (int n = 0; n < h.RuntimeObjects.Length; n++)
                w.WriteLine("  runtime object " + (n < h.RuntimeIds.Length ? h.RuntimeIds[n] : -1) + " '" + h.RuntimeObjects[n].Name + "': "
                    + h.RuntimeObjects[n].Shapes.Length + " shapes");
            w.WriteLine("  randomness: " + h.Streams.Length + " streams" + (h.Streams.Length > 0 ? " (" + string.Join(", ", h.Streams) + ")" : "")
                + ", " + h.Draws.Length + " per-flight draws");
            foreach (ParameterDraw d in h.Draws)
                w.WriteLine("    " + d.Stream + " / " + d.Parameter + " = " + G(d.Value) + " in [" + G(d.Lo) + ", " + G(d.Hi) + "] (draw " + d.Index + ")");
            w.WriteLine("  start: position " + V(h.StartPosition, "0.000") + " m, velocity " + V(h.StartVelocity, "0.000") + " m/s, body rate "
                + V(h.StartBodyRate, "0.000") + " rad/s");
            w.WriteLine("  input: " + InputLine(h));
            if (h.InputLatency != null) w.WriteLine("  latency test: " + LatencyLine(h.InputLatency));
            foreach (string u in h.Unknown) w.WriteLine("  not known to this reader: " + u);

            string length = Steps + " step records, " + Seconds.ToString("0.000", c) + " s";
            if (Complete)
                w.WriteLine("flight: " + length + "; complete (end block: " + End.Steps + " steps, " + End.Events + " events, " + End.Hitches
                    + " hitches, " + End.LogWaits + " log waits, " + End.SticksDropped + " stick reports dropped, " + End.EventsLost + " events lost)");
            else
                w.WriteLine("flight: " + length + "; CUT at byte " + CutAt + ": " + CutReason + "; read to the last whole record"
                    + (HasSteps ? ", step " + Last.State.Step : ""));
            if (Gaps > 0) w.WriteLine("  " + Gaps + " steps are missing between records");

            if (HasSteps)
            {
                ref readonly FlightState s = ref Last.State;
                ref readonly StickSample k = ref Last.Stick;
                ref readonly StepTrace t = ref Last.Trace;
                w.WriteLine("last state, step " + s.Step + " (t = " + Seconds.ToString("0.000", c) + " s):");
                w.WriteLine("  position " + V(s.Position, "0.000") + " m, velocity " + V(s.Velocity, "0.000") + " m/s, armed " + (s.Armed ? "yes" : "no"));
                w.WriteLine("  attitude (" + G(s.Attitude.X) + ", " + G(s.Attitude.Y) + ", " + G(s.Attitude.Z) + ", " + G(s.Attitude.W)
                    + "), body rate " + V(s.BodyRate, "0.0000") + " rad/s");
                w.WriteLine("  rotor speed " + C4(s.RotorSpeed) + " rad/s, command " + C4(t.RotorCommand) + ", esc " + C4(s.EscCommand));
                w.WriteLine("  packs " + G(t.PackVolts0) + " / " + G(t.PackVolts1) + " V, " + G(t.PackAmps0) + " / " + G(t.PackAmps1) + " A, charge "
                    + G(s.PackCharge0) + " / " + G(s.PackCharge1));
                w.WriteLine("  net force " + V(t.NetForce, "0.000") + " N, net moment " + V(t.NetMoment, "0.0000") + " N.m; the air's "
                    + V(t.AeroForce, "0.000") + " N, " + V(t.AeroMoment, "0.0000") + " N.m; wind " + V(t.Wind, "0.000") + " m/s, at the hubs " + V(t.RotorWind[0], "0.000") + ", "
                    + V(t.RotorWind[1], "0.000") + ", " + V(t.RotorWind[2], "0.000") + ", " + V(t.RotorWind[3], "0.000") + " m/s"
                    + (s.EscLimited != 0 ? "; ESCs at their current limit: " + Limited(s.EscLimited) : ""));
                w.WriteLine("  contacts: " + t.ContactsTouching + " touching, force " + V(t.ContactForce, "0.000") + " N, moment " + V(t.ContactMoment, "0.0000")
                    + " N.m; legs " + C4(t.LegLoad) + " N, compressed " + C4(t.LegCompression) + " m" + (s.Crashed ? "; CRASHED" : ""));
                w.WriteLine("  stick roll " + G(k.Roll) + ", pitch " + G(k.Pitch) + ", yaw " + G(k.Yaw) + ", throttle " + G(k.Throttle) + ", arm "
                    + (k.Arm ? "on" : "off") + " (host " + k.HostUs + " us)");
            }

            foreach (LoggedStep f in Failsafes)
                w.WriteLine("FAILSAFE: the radio was lost at host " + f.Stick.HostUs + " us; step " + f.State.Step + " (t = "
                    + (f.State.Step * FlightCore.Dt).ToString("0.000", c) + " s) disarmed");

            w.WriteLine("events: " + Events.Count + ByKind());
            for (int n = 0; n < Events.Count && n < maxEvents; n++)
            {
                LoggedEvent e = Events[n];
                w.WriteLine("  t = " + (e.Step * FlightCore.Dt).ToString("0.000", c) + " s, step " + e.Step + ": " + Describe(e.Event));
            }
            if (Events.Count > maxEvents) w.WriteLine("  ... " + (Events.Count - maxEvents) + " more");
        }

        /// <summary>One event in words, with its units.</summary>
        public string Describe(in FlightEvent e)
        {
            switch (e.Kind)
            {
                case EventKind.ContactMade:
                    return ShapeName(e) + " touches " + Where(e) + " at " + V(e.Point, "0.000") + " m, " + G(e.A) + " m/s";
                case EventKind.ContactBroken:
                    return ShapeName(e) + " leaves " + Where(e) + " after " + G(e.A) + " s, impulse " + G(e.B) + " N.s, peak " + G(e.C) + " N";
                case EventKind.PropStrike:
                    return "prop " + CornerName(e.Index) + " struck by " + Where(e) + " at " + V(e.Point, "0.000") + " m, " + G(e.A) + " m/s";
                case EventKind.MixerSaturation:
                    return "mixer saturated: " + (e.Index <= 3 ? ((MixerAxis)e.Index).ToString().ToLowerInvariant() : "axis " + e.Index)
                        + " lost " + G(e.A * 100) + " % of its demand";
                case EventKind.Crash:
                    return "CRASH (" + (e.Index == 1 ? "acceleration" : e.Index == 2 ? "prop strike" : "cause " + e.Index) + "): " + PartName(e.Part) + " on "
                        + Where(e) + " at " + V(e.Point, "0.000") + " m, " + G(e.A) + " m/s; so far " + G(e.B) + " N.s, peak " + G(e.C) + " m/s2";
                case EventKind.Tunnel:
                    return "TUNNEL, a physics bug: " + ShapeName(e) + " through " + Where(e) + " at " + V(e.Point, "0.000") + " m, " + G(e.A) + " m deep at "
                        + G(e.B) + " m/s";
                case EventKind.Impact:
                    return "impact: " + PartName(e.Part) + " on " + Where(e) + " at " + G(e.C) + " m/s: peak " + G(e.A) + " m/s2 (" + G(e.A / 9.80665)
                        + " g), impulse " + G(e.B) + " N.s over its window";
                case EventKind.ContactsLost:
                    return G(e.A) + " contacts lost by " + ShapeName(e) + ": its table or the query buffer was full (a bug)";
                case EventKind.Hitch:
                    return e.Index == (byte)HitchCause.LogFull
                        ? "hitch: the log's ring was full and the flight thread waited " + G(e.A) + " x 1 ms"
                        : e.Index == (byte)HitchCause.Pause
                        ? "PAUSED for " + G(e.B) + " us: no step ran, and the later steps run that much later"
                        : "hitch: the scheduler ran " + G(e.A) + " steps to catch up, the first " + G(e.B) + " us late";
                case EventKind.EventsLost:
                    return G(e.A) + " events lost: the step's event buffer was full (a producer bug)";
                case EventKind.CurrentLimit:
                    return e.A > 0
                        ? "ESC " + CornerName(e.Index) + " holds its motor at its " + G(e.B) + " A current limit, taking " + G(e.A * 100) + " % of its duty"
                        : "ESC " + CornerName(e.Index) + " lets go of its " + G(e.B) + " A current limit";
                case EventKind.VortexRingEntered:
                case EventKind.VortexRingLeft:
                    return "rotor " + CornerName(e.Index) + (e.Kind == EventKind.VortexRingEntered ? " enters" : " leaves") + " its vortex ring at eta "
                        + G(e.A) + ", mu " + G(e.B) + " (v_h " + G(e.C) + " m/s" + (e.C > 0 ? ")" : ": the rotor stopped)");
                default:
                    return "event kind " + (int)e.Kind + ": part " + e.Part + ", index " + e.Index + ", " + Where(e) + ", a " + G(e.A) + ", b "
                        + G(e.B) + ", c " + G(e.C);
            }
        }

        static string InputLine(LogHeader h)
        {
            if (h.Input == InputSource.None) return "none recorded (a bench or a test)";
            if (h.Input == InputSource.Keyboard) return "KEYBOARD, development only: never tune from this flight";
            string line = (h.Input == InputSource.Radio ? "radio" : "joystick") + " '" + h.InputDevice + "'";
            ReportIntervals r = h.InputIntervals;
            if (r == null || r.Count == 0) return line + "; report interval not measured";
            CultureInfo c = CultureInfo.InvariantCulture;
            return line + "; report interval over " + (r.SumUs / 1e6).ToString("0.0", c) + " s before the flight: " + r.Count + " intervals, mean "
                + ((double)r.SumUs / r.Count / 1000).ToString("0.000", c) + " ms, min " + Ms(r.MinUs) + ", p50 " + Ms(r.PercentileUs(50)) + ", p99 "
                + Ms(r.PercentileUs(99)) + ", max " + Ms(r.MaxUs) + " ms";
        }

        static string Ms(long us) => (us / 1000.0).ToString("0.000", CultureInfo.InvariantCulture);

        static string LatencyLine(InputLatency l) =>
            l.Steps + " stick steps, from each step report's arrival: to the first step that uses it " + Spread(l.FirstStep)
            + "; to the motor command (the ESCs' duty halfway through the change) " + Spread(l.Motor);

        static string Spread(in LatencySpread s) =>
            "p50 " + Ms(s.P50Us) + ", p99 " + Ms(s.P99Us) + " ms (min " + Ms(s.MinUs) + ", max " + Ms(s.MaxUs) + ", mean "
            + (s.MeanUs / 1000).ToString("0.000", CultureInfo.InvariantCulture) + ")";

        string ByKind()
        {
            var kinds = new List<EventKind>();
            foreach (LoggedEvent e in Events)
                if (!kinds.Contains(e.Event.Kind))
                    kinds.Add(e.Event.Kind);
            if (kinds.Count == 0) return "";
            var parts = new List<string>();
            foreach (EventKind k in kinds) parts.Add(k + " " + Count(k));
            return " (" + string.Join(", ", parts) + ")";
        }

        string PartName(ushort part) =>
            part == FlightEvent.NoPart ? "the drone" : part < Header.Parts.Length ? "part '" + Header.Parts[part].Name + "'" : "part #" + part;

        string ShapeName(in FlightEvent e) =>
            Header.Contacts != null && e.Index < Header.Contacts.Shapes.Length ? "'" + Header.Contacts.Shapes[e.Index].Name + "' of " + PartName(e.Part) : PartName(e.Part);

        string Where(in FlightEvent e)
        {
            string name = Header.RuntimeObjectName(e.Object);
            string what = e.Object == WorldIds.Ground ? "the ground" : name != null ? "'" + name + "' shape " + e.Shape : "object " + e.Object + " shape " + e.Shape;
            if (e.Surface != 0) what += " (surface " + Header.SurfaceName(e.Surface) + ")";
            else if (e.Material != 0) what += " (material " + Header.MaterialName(e.Material) + ")";
            return what;
        }

        // The wind in words: its model, level, start second and draws.
        static string WindLine(LogHeader h)
        {
            string text = h.WindModel + ", " + h.Wind.Level.ToString().ToLowerInvariant();
            if (h.Wind.Level == WindLevel.Steady)
            {
                text += " " + G(h.Wind.SteadySpeed) + " m/s from " + G(h.Wind.SteadyFromDeg) + " deg";
                if (h.Wind.SteadyOnsetSeconds != 0) text += ", stepping in at " + G(h.Wind.SteadyOnsetSeconds) + " s";
            }
            if (h.Wind.StartSecond != 0) text += ", from second " + h.Wind.StartSecond + " of its history";
            foreach (ParameterDraw d in h.Draws)
                if (d.Stream == "wind.direction") text += "; from " + G(d.Value) + " deg";
                else if (d.Stream == "wind.strength") text += ", mean " + G(d.Value) + " m/s in [" + G(d.Lo) + ", " + G(d.Hi) + "]";
            foreach (NamedValue v in h.WindParameters) text += ", " + v.Name + " " + G(v.Value);
            WakeLayout b = h.Wind.Wakes;
            if (b != null)
                text += "; wakes (p7c-wind 1.5, wake grid version " + WakeGrid.Version + "): " + b.Volumes.Length + " wind volumes on "
                    + b.NX + " × " + b.NZ + " cells of 2 m from " + G(b.OriginX) + ", " + G(b.OriginZ) + " m";
            GustFront f = h.Wind.Front;
            if (f != null)
                text += "; a gust front (p7c-wind 1.4's scenario" + (f.Uniform ? ", the known-wrong uniform field" : "") + "): from " + G(f.Start.X) + ", "
                    + G(f.Start.Z) + " m along " + G(f.Normal.X) + ", " + G(f.Normal.Z) + " at " + G(f.Speed) + " m/s, " + V(f.Delta, "0.000")
                    + " m/s behind it over " + G(f.Width) + " m";
            CanopyStrip c = h.Wind.Canopy;
            if (c != null)
                text += "; a wooded strip's canopy (p7c-wind 1.5): " + G(c.Height) + " m tall, λ " + G(c.FrontalAreaIndex) + ", " + G(c.Length) + " × "
                    + G(c.Width) + " m about " + G(c.CentreX) + ", " + G(c.CentreZ) + " m, its axis at " + G(c.AxisDeg) + " deg";
            return text;
        }

        static string Limited(byte bits)
        {
            var parts = new List<string>();
            for (byte i = 0; i < 4; i++)
                if ((bits & (1 << i)) != 0)
                    parts.Add(CornerName(i));
            return string.Join(", ", parts);
        }

        static string CornerName(byte corner) =>
            corner == 0 ? "rear right" : corner == 1 ? "front right" : corner == 2 ? "rear left" : corner == 3 ? "front left" : "#" + corner;

        static string Names(NamedId[] names)
        {
            if (names.Length == 0) return "none";
            var parts = new List<string>();
            foreach (NamedId n in names) parts.Add(n.Id + " " + n.Name);
            return string.Join(", ", parts);
        }

        static string G(double v) => v.ToString("G6", CultureInfo.InvariantCulture);

        static string V(Double3 v, string format) =>
            "(" + v.X.ToString(format, CultureInfo.InvariantCulture) + ", " + v.Y.ToString(format, CultureInfo.InvariantCulture) + ", "
            + v.Z.ToString(format, CultureInfo.InvariantCulture) + ")";

        static string C4(in Corners c) => "(" + G(c.RearRight) + ", " + G(c.FrontRight) + ", " + G(c.RearLeft) + ", " + G(c.FrontLeft) + ")";
    }
}
