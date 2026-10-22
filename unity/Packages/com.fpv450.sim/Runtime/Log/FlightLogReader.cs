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
            if (h.Aero != null)
                w.WriteLine("  aero: air " + G(h.Aero.AirDensity) + " kg/m3, body silhouette side/top/front " + V(h.Aero.BodyArea, "0.0000")
                    + " m2 at " + V(h.Aero.BodyCenterOfPressure, "0.000") + " m, C_d " + G(h.Aero.DragCoefficient) + ", coil "
                    + (h.Aero.CoilPart >= 0 ? PartName((ushort)h.Aero.CoilPart) : "none"));
            else
                w.WriteLine("  aero: none (a bench body)");
            w.WriteLine("  randomness: " + h.Streams.Length + " streams" + (h.Streams.Length > 0 ? " (" + string.Join(", ", h.Streams) + ")" : "")
                + ", " + h.Draws.Length + " per-flight draws");
            foreach (ParameterDraw d in h.Draws)
                w.WriteLine("    " + d.Stream + " / " + d.Parameter + " = " + G(d.Value) + " in [" + G(d.Lo) + ", " + G(d.Hi) + "] (draw " + d.Index + ")");
            w.WriteLine("  start: position " + V(h.StartPosition, "0.000") + " m, velocity " + V(h.StartVelocity, "0.000") + " m/s, body rate "
                + V(h.StartBodyRate, "0.000") + " rad/s");
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
                    + V(t.AeroForce, "0.000") + " N, " + V(t.AeroMoment, "0.0000") + " N.m; gust " + V(s.Gust, "0.000") + " m/s"
                    + (s.EscLimited != 0 ? "; ESCs at their current limit: " + Limited(s.EscLimited) : ""));
                w.WriteLine("  stick roll " + G(k.Roll) + ", pitch " + G(k.Pitch) + ", yaw " + G(k.Yaw) + ", throttle " + G(k.Throttle) + ", arm "
                    + (k.Arm ? "on" : "off") + " (host " + k.HostUs + " us)");
            }

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
                    return PartName(e.Part) + " touches " + Where(e) + " at " + V(e.Point, "0.000") + " m, " + G(e.A) + " m/s";
                case EventKind.ContactBroken:
                    return PartName(e.Part) + " leaves " + Where(e) + " after " + G(e.A) + " s, impulse " + G(e.B) + " N.s";
                case EventKind.PropStrike:
                    return "prop " + CornerName(e.Index) + " struck by " + Where(e) + " at " + V(e.Point, "0.000") + " m, " + G(e.A) + " m/s";
                case EventKind.MixerSaturation:
                    return "mixer saturated: " + (e.Index <= 3 ? ((MixerAxis)e.Index).ToString().ToLowerInvariant() : "axis " + e.Index)
                        + " lost " + G(e.A * 100) + " % of its demand";
                case EventKind.Crash:
                    return "CRASH: " + PartName(e.Part) + " on " + Where(e) + " at " + V(e.Point, "0.000") + " m, " + G(e.A) + " m/s, " + G(e.B)
                        + " N.s, peak " + G(e.C) + " m/s2";
                case EventKind.Tunnel:
                    return "TUNNEL, a physics bug: " + PartName(e.Part) + " through " + Where(e) + " at " + V(e.Point, "0.000") + " m, " + G(e.A) + " m deep";
                case EventKind.Hitch:
                    return e.Index == (byte)HitchCause.LogFull
                        ? "hitch: the log's ring was full and the flight thread waited " + G(e.A) + " x 1 ms"
                        : "hitch: the scheduler ran " + G(e.A) + " steps to catch up, the first " + G(e.B) + " us late";
                case EventKind.EventsLost:
                    return G(e.A) + " events lost: the step's event buffer was full (a producer bug)";
                case EventKind.CurrentLimit:
                    return e.A > 0
                        ? "ESC " + CornerName(e.Index) + " holds its motor at its " + G(e.B) + " A current limit, taking " + G(e.A * 100) + " % of its duty"
                        : "ESC " + CornerName(e.Index) + " lets go of its " + G(e.B) + " A current limit";
                default:
                    return "event kind " + (int)e.Kind + ": part " + e.Part + ", index " + e.Index + ", " + Where(e) + ", a " + G(e.A) + ", b "
                        + G(e.B) + ", c " + G(e.C);
            }
        }

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

        string Where(in FlightEvent e)
        {
            string what = e.Object == WorldIds.Ground ? "the ground" : "object " + e.Object + " shape " + e.Shape;
            if (e.Surface != 0) what += " (surface " + Header.SurfaceName(e.Surface) + ")";
            else if (e.Material != 0) what += " (material " + Header.MaterialName(e.Material) + ")";
            return what;
        }

        // The wind in words: its model, level and draws (PHB-06: a stand-in, labelled as such).
        static string WindLine(LogHeader h)
        {
            string text = h.WindModel + ", " + h.Wind.Level.ToString().ToLowerInvariant();
            if (h.Wind.Level == WindLevel.Steady)
                text += " " + G(h.Wind.SteadySpeed) + " m/s from " + G(h.Wind.SteadyFromDeg) + " deg";
            foreach (ParameterDraw d in h.Draws)
                if (d.Stream == "wind.direction") text += "; from " + G(d.Value) + " deg";
                else if (d.Stream == "wind.strength") text += ", mean " + G(d.Value) + " m/s in [" + G(d.Lo) + ", " + G(d.Hi) + "]";
            foreach (NamedValue v in h.WindParameters) text += ", " + v.Name + " " + G(v.Value);
            if (h.WindModel == WindPreset.Model) text += " (p7c-wind replaces it)";
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
