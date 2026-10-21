using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Log
{
    /// <summary>A log that cannot be read: not a flight log, a format this build does not know, or a broken header.</summary>
    public sealed class FlightLogException : Exception
    {
        public FlightLogException(string message) : base(message)
        {
        }
    }

    /// <summary>A world id and its name, as the header lists the world's surfaces and materials.</summary>
    public readonly struct NamedId
    {
        public readonly int Id;
        public readonly string Name;

        public NamedId(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    /// <summary>One flight-controller setting as logged: its CLI name, its type (1 double, 2 bool, 3 enum) and its bits.</summary>
    public readonly struct SettingValue
    {
        public const byte Double = 1, Bool = 2, Enum = 3;

        public readonly string Name;
        public readonly byte Type;
        public readonly long Bits;

        public SettingValue(string name, byte type, long bits)
        {
            Name = name;
            Type = type;
            Bits = bits;
        }

        public override string ToString() => Type == Double
            ? BitConverter.Int64BitsToDouble(Bits).ToString("R", CultureInfo.InvariantCulture)
            : Type == Bool ? (Bits != 0 ? "on" : "off") : Bits.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The log header (design 10, LOG-01): everything that fixes a flight before its first step, so a replay rebuilds
    /// the flight from it and the logged sticks alone. It holds:
    /// - the flight: the model version and the seed (with the streams opened and every per-flight draw)
    /// - the host: the build, the runtime and the ISA flags (for a cross-machine check, never a refusal: LOG-04)
    /// - the world: the id a replay opens it by, its content hash and QueryVersion, its surface and material names
    /// - the drone: drone.json's hash (0 for a body not read from it), its name and the pilot's loadout
    /// - the body the flight started with: the mass model's parts and the mass, CoM and inertia they built
    /// - the flight controller's settings, every field by its CLI name
    /// - the start state, and the record layouts (so a reader needs no code of ours)
    /// - the input: where the sticks came from (a keyboard flight is for development, never for tuning), the device's
    ///   product name and its report intervals before the flight (p6-input-app)
    /// Sim data only (LOG-05): no user, machine, path or clock.
    ///
    /// On disk: Magic, the format version and the header's length (u32), then entries of a u16 tag, a u32 length and
    /// the payload. A reader skips an entry it does not know and lists it in Unknown. Text is a u16 byte count and UTF-8.
    /// </summary>
    public sealed class LogHeader
    {
        /// <summary>The FlightSetup fields the header records, checked against FlightSetup by the suite.</summary>
        public static readonly string[] SetupFieldsLogged =
            { "Seed", "Settings", "World", "Mass", "StartPosition", "StartAttitude", "StartVelocity", "StartBodyRate" };

        /// <summary>The DronePart fields the header records, checked against DronePart by the suite.</summary>
        public static readonly string[] PartFieldsLogged =
            { "Name", "Kind", "Material", "Mass", "Position", "Size", "Diameter", "Length", "Axis" };

        const ushort TagFlight = 1, TagHost = 2, TagWorld = 3, TagDrone = 4, TagBody = 5, TagSettings = 6,
            TagRandomness = 7, TagStart = 8, TagLayouts = 9, TagInput = 10;

        public int FormatVersion = LogFormat.Version;
        public int ModelVersion = LogFormat.ModelVersion;
        public ulong Seed;

        public string Build = "", Runtime = "", Isa = "";

        public string WorldId = "";
        public ulong WorldHash;
        public int QueryVersion;
        public NamedId[] Surfaces = new NamedId[0];
        public NamedId[] Materials = new NamedId[0];

        public ulong DroneHash;
        public string DroneName = "";
        public Loadout Loadout;

        public DronePart[] Parts = new DronePart[0];
        public MassProperties Body;

        public SettingValue[] Settings = new SettingValue[0];

        public string[] Streams = new string[0];
        public ParameterDraw[] Draws = new ParameterDraw[0];

        public Double3 StartPosition, StartVelocity, StartBodyRate;
        public Quat StartAttitude = Quat.Identity;

        /// <summary>
        /// Where the sticks came from, the device's product name, and the histogram of its report intervals over the
        /// window the input layer measured before the flight (null when none was measured: a keyboard or a bench).
        /// </summary>
        public InputSource Input;
        public string InputDevice = "";
        public ReportIntervals InputIntervals;

        public LogField[] StepFields = LogFormat.StepFields;
        public LogField[] EventFields = LogFormat.EventFields;
        public LogField[] EndFields = LogFormat.EndFields;

        /// <summary>Entries and settings this reader did not know.</summary>
        public readonly List<string> Unknown = new List<string>();

        /// <summary>The prologue and header's length on disk.</summary>
        public long Length { get; private set; }

        /// <summary>
        /// The header of a flight: its setup and the core made from it, read right after the core's construction (the
        /// per-flight draws are made by then). The host sets Build, Isa and WorldId, and SetDrone for a drone.json body.
        /// </summary>
        public static LogHeader For(FlightSetup setup, FlightCore core)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (core == null) throw new ArgumentNullException(nameof(core));
            var h = new LogHeader
            {
                Seed = setup.Seed,
                Runtime = CurrentRuntime(),
                WorldHash = core.World.ContentHash,
                QueryVersion = core.World.QueryVersion,
                Body = core.Body,
                Settings = SettingsOf(core.Settings),
                StartPosition = setup.StartPosition,
                StartAttitude = setup.StartAttitude,
                StartVelocity = setup.StartVelocity,
                StartBodyRate = setup.StartBodyRate,
            };
            var surfaces = new List<NamedId>();
            for (int id = 1; id < 255; id++)
            {
                string name = core.World.Query.SurfaceName((byte)id);
                if (name != null) surfaces.Add(new NamedId(id, name));
            }
            var materials = new List<NamedId>();
            for (int id = 1; id < ushort.MaxValue; id++)
            {
                string name = core.World.Query.MaterialName((ushort)id);
                if (name != null) materials.Add(new NamedId(id, name));
            }
            h.Surfaces = surfaces.ToArray();
            h.Materials = materials.ToArray();
            h.Parts = new DronePart[setup.Mass.PartCount];
            for (int i = 0; i < h.Parts.Length; i++)
                h.Parts[i] = setup.Mass.Part(i);
            h.Streams = new string[core.Random.Opened.Count];
            for (int i = 0; i < h.Streams.Length; i++)
                h.Streams[i] = core.Random.Opened[i];
            h.Draws = new ParameterDraw[core.Random.Draws.Count];
            for (int i = 0; i < h.Draws.Length; i++)
                h.Draws[i] = core.Random.Draws[i];
            return h;
        }

        /// <summary>This process's runtime and architecture, as the header records them (".NET 10.0.x X64", "Mono …").</summary>
        public static string CurrentRuntime() => RuntimeInformation.FrameworkDescription + " " + RuntimeInformation.ProcessArchitecture;

        /// <summary>The body came from drone.json at this loadout.</summary>
        public void SetDrone(DroneSpec spec, in Loadout loadout)
        {
            DroneHash = spec.ContentHash;
            DroneName = spec.Name ?? "";
            Loadout = loadout;
        }

        /// <summary>
        /// The flight's setup again, on world (which a replay opened for WorldId and checked against WorldHash). The
        /// settings must match this build's FcSettings field for field; problems lists what does not.
        /// </summary>
        public FlightSetup ToSetup(FlightWorld world, List<string> problems)
        {
            ReadSettings(out FcSettings settings, problems);
            return new FlightSetup
            {
                Seed = Seed,
                Settings = settings,
                World = world,
                Mass = new MassModel(Parts),
                StartPosition = StartPosition,
                StartAttitude = StartAttitude,
                StartVelocity = StartVelocity,
                StartBodyRate = StartBodyRate,
            };
        }

        /// <summary>The surface or material name of an id, or the id itself when the header has no name for it.</summary>
        public string SurfaceName(int id) => NameOf(Surfaces, id);

        public string MaterialName(int id) => NameOf(Materials, id);

        static string NameOf(NamedId[] names, int id)
        {
            foreach (NamedId n in names)
                if (n.Id == id)
                    return n.Name;
            return "#" + id.ToString(CultureInfo.InvariantCulture);
        }

        // ---- settings: every public field of FcSettings by name, in ordinal order ----

        static FieldInfo[] SettingsFields()
        {
            FieldInfo[] fields = typeof(FcSettings).GetFields(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            return fields;
        }

        public static SettingValue[] SettingsOf(in FcSettings settings)
        {
            object boxed = settings;
            FieldInfo[] fields = SettingsFields();
            var values = new SettingValue[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                object v = f.GetValue(boxed);
                if (f.FieldType == typeof(double))
                    values[i] = new SettingValue(f.Name, SettingValue.Double, BitConverter.DoubleToInt64Bits((double)v));
                else if (f.FieldType == typeof(bool))
                    values[i] = new SettingValue(f.Name, SettingValue.Bool, (bool)v ? 1 : 0);
                else if (f.FieldType.IsEnum)
                    values[i] = new SettingValue(f.Name, SettingValue.Enum, Convert.ToInt64(v, CultureInfo.InvariantCulture));
                else
                    throw new InvalidOperationException("the log cannot record FcSettings." + f.Name + " of type " + f.FieldType.Name);
            }
            return values;
        }

        /// <summary>The logged settings as this build's FcSettings; a field either side lacks goes into problems.</summary>
        public void ReadSettings(out FcSettings settings, List<string> problems)
        {
            object boxed = default(FcSettings);
            FieldInfo[] fields = SettingsFields();
            var found = new bool[fields.Length];
            foreach (SettingValue v in Settings)
            {
                int i = Array.FindIndex(fields, f => f.Name == v.Name);
                if (i < 0)
                {
                    problems.Add("the log has the setting " + v.Name + ", which this build's FcSettings lacks");
                    continue;
                }
                FieldInfo f = fields[i];
                byte expected = f.FieldType == typeof(double) ? SettingValue.Double
                    : f.FieldType == typeof(bool) ? SettingValue.Bool : SettingValue.Enum;
                if (v.Type != expected)
                {
                    problems.Add("the setting " + v.Name + " has another type in the log");
                    continue;
                }
                found[i] = true;
                object value = v.Type == SettingValue.Double ? BitConverter.Int64BitsToDouble(v.Bits)
                    : v.Type == SettingValue.Bool ? (object)(v.Bits != 0) : System.Enum.ToObject(f.FieldType, v.Bits);
                f.SetValue(boxed, value);
            }
            for (int i = 0; i < fields.Length; i++)
                if (!found[i])
                    problems.Add("the log has no value for the setting " + fields[i].Name);
            settings = (FcSettings)boxed;
        }

        // ---- on disk ----

        /// <summary>The prologue and the header, as the recorder writes them before the first step.</summary>
        public byte[] Encode()
        {
            var body = new MemoryStream();
            var w = new BinaryWriter(body);
            Entry(w, TagFlight, e =>
            {
                e.Write(ModelVersion);
                e.Write(Seed);
            });
            Entry(w, TagHost, e =>
            {
                Text(e, Build);
                Text(e, Runtime);
                Text(e, Isa);
            });
            Entry(w, TagWorld, e =>
            {
                Text(e, WorldId);
                e.Write(WorldHash);
                e.Write(QueryVersion);
                e.Write((ushort)Surfaces.Length);
                foreach (NamedId n in Surfaces)
                {
                    e.Write((byte)n.Id);
                    Text(e, n.Name);
                }
                e.Write((ushort)Materials.Length);
                foreach (NamedId n in Materials)
                {
                    e.Write((ushort)n.Id);
                    Text(e, n.Name);
                }
            });
            Entry(w, TagDrone, e =>
            {
                e.Write(DroneHash);
                Text(e, DroneName);
                e.Write(Loadout.Payload);
                e.Write(Loadout.Legs);
                e.Write(Loadout.CoilPaidOut);
            });
            Entry(w, TagBody, e =>
            {
                e.Write((ushort)Parts.Length);
                foreach (DronePart p in Parts)
                {
                    Text(e, p.Name);
                    e.Write((byte)p.Kind);
                    Text(e, p.Material);
                    e.Write(p.Mass);
                    V3(e, p.Position);
                    V3(e, p.Size);
                    e.Write(p.Diameter);
                    e.Write(p.Length);
                    V3(e, p.Axis);
                }
                e.Write(Body.Mass);
                e.Write(Body.InverseMass);
                V3(e, Body.CenterOfMass);
                S3(e, Body.Inertia);
                S3(e, Body.InverseInertia);
            });
            Entry(w, TagSettings, e =>
            {
                e.Write((ushort)Settings.Length);
                foreach (SettingValue v in Settings)
                {
                    Text(e, v.Name);
                    e.Write(v.Type);
                    e.Write(v.Bits);
                }
            });
            Entry(w, TagRandomness, e =>
            {
                e.Write((ushort)Streams.Length);
                foreach (string s in Streams) Text(e, s);
                e.Write((ushort)Draws.Length);
                foreach (ParameterDraw d in Draws)
                {
                    Text(e, d.Stream);
                    Text(e, d.Parameter);
                    e.Write(d.Lo);
                    e.Write(d.Hi);
                    e.Write(d.Value);
                    e.Write(d.Index);
                }
            });
            Entry(w, TagStart, e =>
            {
                V3(e, StartPosition);
                e.Write(StartAttitude.X);
                e.Write(StartAttitude.Y);
                e.Write(StartAttitude.Z);
                e.Write(StartAttitude.W);
                V3(e, StartVelocity);
                V3(e, StartBodyRate);
            });
            Entry(w, TagLayouts, e =>
            {
                Layout(e, LogFormat.StepBlock, StepFields);
                Layout(e, LogFormat.EventBlock, EventFields);
                Layout(e, LogFormat.EndBlock, EndFields);
            });
            Entry(w, TagInput, e =>
            {
                e.Write((byte)Input);
                Text(e, InputDevice);
                ReportIntervals r = InputIntervals;
                e.Write(r == null ? 0 : r.BinUs);
                e.Write((ushort)(r == null ? 0 : r.Counts.Length));
                if (r == null) return;
                foreach (long n in r.Counts) e.Write(n);
                e.Write(r.Count);
                e.Write(r.SumUs);
                e.Write(r.MinUs);
                e.Write(r.MaxUs);
            });
            w.Flush();

            var file = new MemoryStream();
            var p0 = new BinaryWriter(file);
            p0.Write(LogFormat.Magic);
            p0.Write((uint)FormatVersion);
            p0.Write((uint)body.Length);
            p0.Write(body.GetBuffer(), 0, (int)body.Length);
            p0.Flush();
            Length = file.Length;
            return file.ToArray();
        }

        /// <summary>The prologue's length: Magic, the version and the header's length.</summary>
        public const int PrologueSize = 16;

        /// <summary>
        /// Reads the prologue and the header from stream. Throws FlightLogException naming the byte where a cut log
        /// ends, or what is wrong with it.
        /// </summary>
        public static LogHeader Read(Stream stream)
        {
            var prologue = new byte[PrologueSize];
            int got = ReadFully(stream, prologue, PrologueSize);
            if (got < PrologueSize)
                throw new FlightLogException("the log is cut inside its prologue, at byte " + got);
            for (int i = 0; i < LogFormat.Magic.Length; i++)
                if (prologue[i] != LogFormat.Magic[i])
                    throw new FlightLogException("not a flight log (no FPV450LG magic)");
            uint version = BinaryPrimitives.ReadUInt32LittleEndian(prologue.AsSpan(8));
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(prologue.AsSpan(12));
            if (version != LogFormat.Version)
                throw new FlightLogException("the log's format is version " + version + "; this build reads version " + LogFormat.Version);
            if (length > 64u << 20)
                throw new FlightLogException("the header's length (" + length + " bytes) is not plausible");
            var bytes = new byte[length];
            got = ReadFully(stream, bytes, (int)length);
            if (got < length)
                throw new FlightLogException("the log is cut inside its header, at byte " + (PrologueSize + got)
                    + " of " + (PrologueSize + length));
            var h = new LogHeader { FormatVersion = (int)version, Length = PrologueSize + length };
            try
            {
                h.Parse(new BinaryReader(new MemoryStream(bytes)), length);
            }
            catch (EndOfStreamException)
            {
                throw new FlightLogException("the header is malformed: an entry runs past its end");
            }
            return h;
        }

        void Parse(BinaryReader r, uint length)
        {
            bool sawLayouts = false;
            while (r.BaseStream.Position < length)
            {
                ushort tag = r.ReadUInt16();
                uint size = r.ReadUInt32();
                long end = r.BaseStream.Position + size;
                if (end > length) throw new FlightLogException("the header is malformed: entry " + tag + " runs past it");
                switch (tag)
                {
                    case TagFlight:
                        ModelVersion = r.ReadInt32();
                        Seed = r.ReadUInt64();
                        break;
                    case TagHost:
                        Build = Text(r);
                        Runtime = Text(r);
                        Isa = Text(r);
                        break;
                    case TagWorld:
                        WorldId = Text(r);
                        WorldHash = r.ReadUInt64();
                        QueryVersion = r.ReadInt32();
                        Surfaces = new NamedId[r.ReadUInt16()];
                        for (int i = 0; i < Surfaces.Length; i++) Surfaces[i] = new NamedId(r.ReadByte(), Text(r));
                        Materials = new NamedId[r.ReadUInt16()];
                        for (int i = 0; i < Materials.Length; i++) Materials[i] = new NamedId(r.ReadUInt16(), Text(r));
                        break;
                    case TagDrone:
                        DroneHash = r.ReadUInt64();
                        DroneName = Text(r);
                        Loadout = new Loadout { Payload = r.ReadDouble(), Legs = r.ReadBoolean(), CoilPaidOut = r.ReadDouble() };
                        break;
                    case TagBody:
                        Parts = new DronePart[r.ReadUInt16()];
                        for (int i = 0; i < Parts.Length; i++)
                        {
                            Parts[i] = new DronePart
                            {
                                Name = Text(r),
                                Kind = (PartKind)r.ReadByte(),
                                Material = Text(r),
                                Mass = r.ReadDouble(),
                                Position = V3(r),
                                Size = V3(r),
                                Diameter = r.ReadDouble(),
                                Length = r.ReadDouble(),
                                Axis = V3(r),
                            };
                        }
                        Body = new MassProperties
                        {
                            Mass = r.ReadDouble(),
                            InverseMass = r.ReadDouble(),
                            CenterOfMass = V3(r),
                            Inertia = S3(r),
                            InverseInertia = S3(r),
                        };
                        break;
                    case TagSettings:
                        Settings = new SettingValue[r.ReadUInt16()];
                        for (int i = 0; i < Settings.Length; i++) Settings[i] = new SettingValue(Text(r), r.ReadByte(), r.ReadInt64());
                        break;
                    case TagRandomness:
                        Streams = new string[r.ReadUInt16()];
                        for (int i = 0; i < Streams.Length; i++) Streams[i] = Text(r);
                        Draws = new ParameterDraw[r.ReadUInt16()];
                        for (int i = 0; i < Draws.Length; i++)
                            Draws[i] = new ParameterDraw(Text(r), Text(r), r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadUInt64());
                        break;
                    case TagStart:
                        StartPosition = V3(r);
                        StartAttitude = new Quat(r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadDouble());
                        StartVelocity = V3(r);
                        StartBodyRate = V3(r);
                        break;
                    case TagLayouts:
                        sawLayouts = true;
                        for (int k = 0; k < 3; k++)
                        {
                            ushort kind = r.ReadUInt16();
                            LogField[] fields = Layout(r);
                            if (kind == LogFormat.StepBlock) StepFields = fields;
                            else if (kind == LogFormat.EventBlock) EventFields = fields;
                            else if (kind == LogFormat.EndBlock) EndFields = fields;
                        }
                        break;
                    case TagInput:
                        Input = (InputSource)r.ReadByte();
                        InputDevice = Text(r);
                        int binUs = r.ReadInt32();
                        int bins = r.ReadUInt16();
                        if (binUs == 0) break;
                        var intervals = new ReportIntervals(binUs, bins);
                        for (int i = 0; i < bins; i++) intervals.Counts[i] = r.ReadInt64();
                        intervals.Count = r.ReadInt64();
                        intervals.SumUs = r.ReadInt64();
                        intervals.MinUs = r.ReadInt64();
                        intervals.MaxUs = r.ReadInt64();
                        InputIntervals = intervals;
                        break;
                    default:
                        Unknown.Add("header entry " + tag + " (" + size + " bytes)");
                        break;
                }
                if (r.BaseStream.Position > end) throw new FlightLogException("the header is malformed: entry " + tag + " overran");
                r.BaseStream.Position = end;
            }
            if (!sawLayouts) throw new FlightLogException("the header has no record layouts");
        }

        /// <summary>True when a layout the log declares is not the one this build writes (field for field).</summary>
        public bool LayoutsDiffer(out string which)
        {
            which = Differs(StepFields, LogFormat.StepFields) ? "step" : Differs(EventFields, LogFormat.EventFields) ? "event"
                : Differs(EndFields, LogFormat.EndFields) ? "end" : null;
            return which != null;
        }

        static bool Differs(LogField[] a, LogField[] b)
        {
            if (a.Length != b.Length) return true;
            for (int i = 0; i < a.Length; i++)
                if (!a[i].SameAs(b[i]))
                    return true;
            return false;
        }

        static void Entry(BinaryWriter w, ushort tag, Action<BinaryWriter> write)
        {
            var payload = new MemoryStream();
            var e = new BinaryWriter(payload, new UTF8Encoding(false));
            write(e);
            e.Flush();
            w.Write(tag);
            w.Write((uint)payload.Length);
            w.Write(payload.GetBuffer(), 0, (int)payload.Length);
        }

        static void Layout(BinaryWriter w, ushort kind, LogField[] fields)
        {
            w.Write(kind);
            w.Write((ushort)fields.Length);
            foreach (LogField f in fields)
            {
                Text(w, f.Name);
                Text(w, f.Unit);
                w.Write((byte)f.Type);
                w.Write((ushort)f.Offset);
                w.Write((ushort)f.Size);
            }
        }

        static LogField[] Layout(BinaryReader r)
        {
            var fields = new LogField[r.ReadUInt16()];
            for (int i = 0; i < fields.Length; i++)
                fields[i] = new LogField(Text(r), Text(r), (LogFieldType)r.ReadByte(), r.ReadUInt16(), r.ReadUInt16());
            return fields;
        }

        static void Text(BinaryWriter w, string s)
        {
            byte[] b = Encoding.UTF8.GetBytes(s ?? "");
            if (b.Length > ushort.MaxValue) throw new ArgumentException("a logged text is too long");
            w.Write((ushort)b.Length);
            w.Write(b);
        }

        static string Text(BinaryReader r)
        {
            int n = r.ReadUInt16();
            byte[] b = r.ReadBytes(n);
            if (b.Length < n) throw new EndOfStreamException();
            return Encoding.UTF8.GetString(b);
        }

        static void V3(BinaryWriter w, Double3 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
            w.Write(v.Z);
        }

        static Double3 V3(BinaryReader r) => new Double3(r.ReadDouble(), r.ReadDouble(), r.ReadDouble());

        static void S3(BinaryWriter w, Sym3 s)
        {
            w.Write(s.XX);
            w.Write(s.YY);
            w.Write(s.ZZ);
            w.Write(s.XY);
            w.Write(s.XZ);
            w.Write(s.YZ);
        }

        static Sym3 S3(BinaryReader r) =>
            new Sym3(r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadDouble());

        /// <summary>Reads up to count bytes; fewer only at the stream's end.</summary>
        internal static int ReadFully(Stream s, byte[] buffer, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = s.Read(buffer, got, count - got);
                if (n <= 0) break;
                got += n;
            }
            return got;
        }
    }
}
