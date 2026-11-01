using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

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

    /// <summary>One named number of the header (the wind preset's parameters).</summary>
    public readonly struct NamedValue
    {
        public readonly string Name;
        public readonly double Value;

        public NamedValue(string name, double value)
        {
            Name = name;
            Value = value;
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
    /// - the propulsion: the motors, the props with their curves, the packs (none for a bench's free body)
    /// - the airframe's drag (none for a bench's body), and the wind: its model ("stand-in", PHB-06), its level and
    ///   the level's preset by name (its per-flight draws are the randomness's)
    /// - the flight controller's settings, every field by its CLI name
    /// - the contacts (1.6): drone.json's contact shapes at the loadout with their thresholds, the legs' spring, and the
    ///   physics materials' rows with physics_materials.json's hash (none for a flight without contacts)
    /// - the runtime objects the flight placed, in order, each shape in world space (the launch stand: K§6.10's asset,
    ///   pose and order; their material ids are the world's, named by the world entry)
    /// - the fiber's table (p7d-tether 1.1): tether.json's every row with its label, and the file's hash (none for a
    ///   flight without the line)
    /// - the start state with the packs' charge, and the record layouts (so a reader needs no code of ours)
    /// - the input: where the sticks came from (a keyboard flight is for development, never for tuning), the device's
    ///   product name and its report intervals before the flight (p6-input-app 1.1); the host µs of step 0, so a step's
    ///   stick age is derived from the records, and the latency test's figures when the flight was one (1.5)
    /// Sim data only (LOG-05): no user, machine, path or clock.
    ///
    /// On disk: Magic, the format version and the header's length (u32), then entries of a u16 tag, a u32 length and
    /// the payload. A reader skips an entry it does not know and lists it in Unknown. Text is a u16 byte count and UTF-8.
    /// </summary>
    public sealed class LogHeader
    {
        /// <summary>The FlightSetup fields the header records, checked against FlightSetup by the suite.</summary>
        public static readonly string[] SetupFieldsLogged =
        {
            "Seed", "Settings", "World", "Mass", "Propulsion", "StartPosition", "StartAttitude", "StartVelocity", "StartBodyRate",
            "StartPackCharge", "Aero", "Wind", "Contacts", "RuntimeObjects", "Asymmetry", "VortexRing", "GroundEffect",
            "Flapping", "Modes", "Tether",
        };

        /// <summary>The tether table's fields the header records (its rows whole, and the file's hash), checked by the suite.</summary>
        public static readonly string[] TetherFieldsLogged = { "ContentHash", "Rows" };
        public static readonly string[] TetherRowFieldsLogged = { "Name", "Label", "Kind", "Values" };

        /// <summary>The modes' fields the header records, checked against AirframeModesSpec by the suite.</summary>
        public static readonly string[] ModesFieldsLogged =
        {
            "ArmModulus", "ArmWidth", "ArmThickness", "ArmLength", "ArmTipShare", "ArmDamping", "MountStiffness", "MountDamping",
            "Camera", "TipMass", "PayloadPart", "CoilPart",
        };

        /// <summary>The ground effect's fields the header records, checked against GroundEffectSpec by the suite.</summary>
        public static readonly string[] GroundEffectFieldsLogged =
        {
            "Reach", "LowestHeight", "CeilingAlpha", "RecirculationOnset", "RecirculationSigma", "RecirculationBound",
            "RecirculationLoop", "AirDensity",
        };

        /// <summary>The flapping's fields the header records, checked against RotorFlappingSpec by the suite.</summary>
        public static readonly string[] FlappingFieldsLogged =
        {
            "LiftSlope", "ZeroLiftAngle", "BendingFrequency", "Southwell", "SkewGain", "Radius", "Blades", "BladeInertia",
            "Stations", "Chords", "Angles", "AirDensity",
        };

        /// <summary>The contacts' fields the header records, checked against the types by the suite.</summary>
        public static readonly string[] ContactFieldsLogged =
        {
            "Margin", "CrashAccel", "CrashPropSpeed", "ImpactWindow", "ImpactSpeed", "FrictionStiffnessRatio", "DampingMassShare",
            "GroundStaticFriction", "GroundKineticFriction", "LegStiffness", "LegDamping", "RingSegments", "Shapes", "Materials", "MaterialsHash",
        };
        public static readonly string[] ContactShapeFieldsLogged =
            { "Name", "Part", "Role", "Corner", "Material", "A", "B", "Radius", "HalfThickness", "Stiffness", "StiffnessUp", "DampingRatio" };
        public static readonly string[] MaterialRowFieldsLogged = { "Name", "StaticFriction", "KineticFriction", "Stiffness", "DampingRatio" };
        public static readonly string[] RuntimeObjectFieldsLogged = { "Name", "Shapes" };
        public static readonly string[] ShapeGeometryFieldsLogged =
            { "Kind", "Center", "AxisX", "AxisY", "AxisZ", "HalfExtents", "Radius", "HalfLength", "Material", "Surface", "SpanA", "SpanB", "Sag" };

        /// <summary>The DronePart fields the header records, checked against DronePart by the suite.</summary>
        public static readonly string[] PartFieldsLogged =
            { "Name", "Kind", "Material", "Mass", "Position", "Size", "Diameter", "Length", "Axis" };

        /// <summary>The propulsion's fields the header records, checked against the structs by the suite.</summary>
        public static readonly string[] PropulsionFieldsLogged = { "Motors", "Props", "Packs" };
        public static readonly string[] MotorFieldsLogged =
            { "Position", "Clockwise", "Kv", "Resistance", "NoLoadCurrent", "RotorInertia", "MagnetPoles", "EscTau", "EscCurrentLimit" };
        public static readonly string[] PropsFieldsLogged =
        {
            "Diameter", "Blades", "Pitch", "ThrustCoefficient", "TorqueRatio", "ReferenceRpm", "StaticRpm", "StaticThrustRatio",
            "StaticTorqueRatio", "InflowAdvanceRatio", "InflowThrustRatio", "InflowTorqueRatio", "HForce",
        };
        public static readonly string[] AeroFieldsLogged = { "AirDensity", "BodyArea", "BodyCenterOfPressure", "DragCoefficient", "CoilPart" };
        public static readonly string[] WindFieldsLogged = { "Level", "SteadySpeed", "SteadyFromDeg", "SteadyOnsetSeconds" };
        public static readonly string[] AsymmetryFieldsLogged = { "KvBound", "KvSigma", "EscLagBound", "BalanceGrade", "PropMass", "ServiceRpm" };
        public static readonly string[] VortexRingFieldsLogged =
        {
            "EntryRate", "ExitRate", "PeakRate", "EndRate", "EntryInflow", "PeakInflow", "EscapeInPlane", "InflowLag", "FluctuationSigma",
            "FluctuationBound", "FluctuationFrequency", "AirDensity", "FixedInducedVelocity",
        };
        public static readonly string[] PackFieldsLogged = { "Part", "Series", "Parallel", "CellCapacity", "CellResistance", "Ocv" };

        const ushort TagFlight = 1, TagHost = 2, TagWorld = 3, TagDrone = 4, TagBody = 5, TagSettings = 6,
            TagRandomness = 7, TagStart = 8, TagLayouts = 9, TagPropulsion = 10, TagAero = 11, TagWind = 12, TagContacts = 13,
            TagRuntimeObjects = 14, TagInput = 15, TagAsymmetry = 16, TagVortexRing = 17, TagGroundEffect = 18, TagFlapping = 19,
            TagModes = 20, TagTether = 21;

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

        /// <summary>The motors, props and packs; null for a body without propulsion.</summary>
        public PropulsionSpec Propulsion;

        public SettingValue[] Settings = new SettingValue[0];

        /// <summary>The airframe's drag; null for none.</summary>
        public AeroSpec Aero;

        /// <summary>The wind: its model's name, its setup, and its level's preset by name (empty for still or steady air).</summary>
        public string WindModel = WindPreset.Model;
        public WindSetup Wind;
        public NamedValue[] WindParameters = new NamedValue[0];

        /// <summary>The contacts; null for none.</summary>
        public ContactSpec Contacts;

        /// <summary>The motors' asymmetry's tolerances; null for identical rotors (its draws are in Draws).</summary>
        public MotorAsymmetry Asymmetry;

        /// <summary>The rotors' vortex rings; null for none (their stream is in the randomness entry).</summary>
        public VortexRingSpec VortexRing;

        /// <summary>The rotors' ground and ceiling effect; null for none (the wash's stream is in the randomness entry).</summary>
        public GroundEffectSpec GroundEffect;

        /// <summary>The rotors' flapping; null for none.</summary>
        public RotorFlappingSpec Flapping;

        /// <summary>The airframe's modes; null for a rigid airframe.</summary>
        public AirframeModesSpec Modes;

        /// <summary>tether.json's rows and hash; null for a flight without the line. A replay builds its table from these
        /// rows once its own file's hash matches (FlightReplay).</summary>
        public TetherTable Tether;

        /// <summary>The runtime objects the flight placed, in order, and the world's object id each got.</summary>
        public RuntimeObject[] RuntimeObjects = new RuntimeObject[0];
        public int[] RuntimeIds = new int[0];

        public string[] Streams = new string[0];
        public ParameterDraw[] Draws = new ParameterDraw[0];

        public Double3 StartPosition, StartVelocity, StartBodyRate;
        public Quat StartAttitude = Quat.Identity;
        public double StartPackCharge = 1;

        /// <summary>
        /// Where the sticks came from, the device's product name, and the histogram of its report intervals over the
        /// window the input layer measured before the flight (null when none was measured: a keyboard or a bench).
        /// </summary>
        public InputSource Input;
        public string InputDevice = "";
        public ReportIntervals InputIntervals;

        /// <summary>
        /// HostClock µs of step 0 (FlightScheduler.StartUs), the clock the sticks' HostUs are in, so step k's stick age is
        /// StartHostUs + k·1000 − its HostUs; 0 when not recorded (a bench, a replay, a log before 1.5).
        /// </summary>
        public long StartHostUs;

        /// <summary>The latency test's figures when this flight was one (LatencyTest, p6-input-app 1.5), else null.</summary>
        public InputLatency InputLatency;

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
                Propulsion = setup.Propulsion,
                Settings = SettingsOf(core.Settings),
                StartPosition = setup.StartPosition,
                StartAttitude = setup.StartAttitude,
                StartVelocity = setup.StartVelocity,
                StartBodyRate = setup.StartBodyRate,
                StartPackCharge = setup.StartPackCharge,
                Aero = setup.Aero?.Clone(),
                Wind = setup.Wind,
                WindParameters = PresetOf(setup.Wind.Level),
                Contacts = setup.Contacts?.Clone(),
                Asymmetry = setup.Asymmetry?.Clone(),
                VortexRing = setup.VortexRing?.Clone(),
                GroundEffect = setup.GroundEffect?.Clone(),
                Flapping = setup.Flapping?.Clone(),
                Modes = setup.Modes?.Clone(),
                Tether = setup.Tether,
                RuntimeObjects = (RuntimeObject[])(setup.RuntimeObjects ?? new RuntimeObject[0]).Clone(),
                RuntimeIds = (int[])core.RuntimeIds.Clone(),
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

        /// <summary>A pilot's level's preset by name, as the header records it; none for still or steady air.</summary>
        public static NamedValue[] PresetOf(WindLevel level)
        {
            if (level == WindLevel.Still || level == WindLevel.Steady) return new NamedValue[0];
            WindPreset p = WindPreset.Of(level);
            return new[]
            {
                new NamedValue("mean_lo_m_s", p.MeanLo), new NamedValue("mean_hi_m_s", p.MeanHi),
                new NamedValue("intensity_sigma_over_u", WindPreset.Intensity), new NamedValue("vertical_over_horizontal_sigma", WindPreset.VerticalRatio),
                new NamedValue("horizontal_scale_m", WindPreset.HorizontalScale), new NamedValue("vertical_scale_m", WindPreset.VerticalScale),
                new NamedValue("gust_bound_sigma", WindPreset.GustBound),
            };
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
                Propulsion = Propulsion,
                StartPosition = StartPosition,
                StartAttitude = StartAttitude,
                StartVelocity = StartVelocity,
                StartBodyRate = StartBodyRate,
                StartPackCharge = StartPackCharge,
                Aero = Aero?.Clone(),
                Wind = Wind,
                Contacts = Contacts?.Clone(),
                Asymmetry = Asymmetry?.Clone(),
                VortexRing = VortexRing?.Clone(),
                GroundEffect = GroundEffect?.Clone(),
                Flapping = Flapping?.Clone(),
                Modes = Modes?.Clone(),
                Tether = Tether,
                RuntimeObjects = (RuntimeObject[])RuntimeObjects.Clone(),
            };
        }

        /// <summary>The runtime object's name of a world object id, or null when the flight placed none by that id.</summary>
        public string RuntimeObjectName(int id)
        {
            for (int i = 0; i < RuntimeIds.Length && i < RuntimeObjects.Length; i++)
                if (RuntimeIds[i] == id) return RuntimeObjects[i].Name;
            return null;
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
            Entry(w, TagPropulsion, e =>
            {
                e.Write(Propulsion != null);
                if (Propulsion == null) return;
                foreach (DroneMotor m in Propulsion.Motors)
                {
                    V3(e, m.Position);
                    e.Write(m.Clockwise);
                    e.Write(m.Kv);
                    e.Write(m.Resistance);
                    e.Write(m.NoLoadCurrent);
                    e.Write(m.RotorInertia);
                    e.Write(m.MagnetPoles);
                    e.Write(m.EscTau);
                    e.Write(m.EscCurrentLimit);
                }
                DroneProps p = Propulsion.Props;
                e.Write(p.Diameter);
                e.Write(p.Blades);
                e.Write(p.Pitch);
                e.Write(p.ThrustCoefficient);
                e.Write(p.TorqueRatio);
                e.Write(p.ReferenceRpm);
                foreach (double[] curve in new[] { p.StaticRpm, p.StaticThrustRatio, p.StaticTorqueRatio, p.InflowAdvanceRatio, p.InflowThrustRatio, p.InflowTorqueRatio })
                    Numbers(e, curve);
                e.Write(p.HForce);
                foreach (DronePack k in Propulsion.Packs)
                {
                    e.Write(k.Part);
                    e.Write(k.Series);
                    e.Write(k.Parallel);
                    e.Write(k.CellCapacity);
                    e.Write(k.CellResistance);
                    Numbers(e, k.Ocv);
                }
            });
            Entry(w, TagAero, e =>
            {
                e.Write(Aero != null);
                if (Aero == null) return;
                e.Write(Aero.AirDensity);
                V3(e, Aero.BodyArea);
                V3(e, Aero.BodyCenterOfPressure);
                e.Write(Aero.DragCoefficient);
                e.Write(Aero.CoilPart);
            });
            Entry(w, TagWind, e =>
            {
                Text(e, WindModel);
                e.Write((byte)Wind.Level);
                e.Write(Wind.SteadySpeed);
                e.Write(Wind.SteadyFromDeg);
                e.Write(Wind.SteadyOnsetSeconds);
                e.Write((ushort)WindParameters.Length);
                foreach (NamedValue v in WindParameters)
                {
                    Text(e, v.Name);
                    e.Write(v.Value);
                }
            });
            Entry(w, TagContacts, e =>
            {
                e.Write(Contacts != null);
                if (Contacts == null) return;
                ContactSpec c = Contacts;
                foreach (double v in new[] { c.Margin, c.CrashAccel, c.CrashPropSpeed, c.ImpactWindow, c.ImpactSpeed, c.FrictionStiffnessRatio,
                    c.DampingMassShare, c.GroundStaticFriction, c.GroundKineticFriction, c.LegStiffness, c.LegDamping })
                    e.Write(v);
                e.Write(c.RingSegments);
                e.Write((ushort)c.Shapes.Length);
                foreach (ContactShape s in c.Shapes)
                {
                    Text(e, s.Name);
                    e.Write(s.Part);
                    e.Write((byte)s.Role);
                    e.Write(s.Corner);
                    Text(e, s.Material);
                    V3(e, s.A);
                    V3(e, s.B);
                    e.Write(s.Radius);
                    e.Write(s.HalfThickness);
                    e.Write(s.Stiffness);
                    e.Write(s.StiffnessUp);
                    e.Write(s.DampingRatio);
                }
                e.Write((ushort)c.Materials.Length);
                foreach (MaterialRow m in c.Materials)
                {
                    Text(e, m.Name);
                    e.Write(m.StaticFriction);
                    e.Write(m.KineticFriction);
                    e.Write(m.Stiffness);
                    e.Write(m.DampingRatio);
                }
                e.Write(c.MaterialsHash);
            });
            Entry(w, TagAsymmetry, e =>
            {
                e.Write(Asymmetry != null);
                if (Asymmetry == null) return;
                e.Write(Asymmetry.KvBound);
                e.Write(Asymmetry.KvSigma);
                e.Write(Asymmetry.EscLagBound);
                e.Write(Asymmetry.BalanceGrade);
                e.Write(Asymmetry.PropMass);
                e.Write(Asymmetry.ServiceRpm);
            });
            Entry(w, TagVortexRing, e =>
            {
                e.Write(VortexRing != null);
                if (VortexRing == null) return;
                VortexRingSpec v = VortexRing;
                foreach (double x in new[]
                         {
                             v.EntryRate, v.ExitRate, v.PeakRate, v.EndRate, v.EntryInflow, v.PeakInflow, v.EscapeInPlane, v.InflowLag,
                             v.FluctuationSigma, v.FluctuationBound, v.FluctuationFrequency, v.AirDensity, v.FixedInducedVelocity,
                         })
                    e.Write(x);
            });
            Entry(w, TagGroundEffect, e =>
            {
                e.Write(GroundEffect != null);
                if (GroundEffect == null) return;
                GroundEffectSpec g = GroundEffect;
                foreach (double x in new[]
                         {
                             g.Reach, g.LowestHeight, g.CeilingAlpha, g.RecirculationOnset, g.RecirculationSigma, g.RecirculationBound,
                             g.RecirculationLoop, g.AirDensity,
                         })
                    e.Write(x);
            });
            Entry(w, TagFlapping, e =>
            {
                e.Write(Flapping != null);
                if (Flapping == null) return;
                RotorFlappingSpec f = Flapping;
                foreach (double x in new[] { f.LiftSlope, f.ZeroLiftAngle, f.BendingFrequency, f.Southwell, f.SkewGain, f.Radius, f.BladeInertia, f.AirDensity })
                    e.Write(x);
                e.Write(f.Blades);
                e.Write((ushort)f.Stations.Length);
                for (int i = 0; i < f.Stations.Length; i++)
                {
                    e.Write(f.Stations[i]);
                    e.Write(f.Chords[i]);
                    e.Write(f.Angles[i]);
                }
            });
            Entry(w, TagModes, e =>
            {
                e.Write(Modes != null);
                if (Modes == null) return;
                AirframeModesSpec m = Modes;
                foreach (double x in new[]
                         {
                             m.ArmModulus, m.ArmWidth, m.ArmThickness, m.ArmLength, m.ArmTipShare, m.ArmDamping, m.MountStiffness,
                             m.MountDamping, m.Camera.X, m.Camera.Y, m.Camera.Z,
                             m.TipMass[0], m.TipMass[1], m.TipMass[2], m.TipMass[3],
                         })
                    e.Write(x);
                e.Write(m.PayloadPart);
                e.Write(m.CoilPart);
            });
            Entry(w, TagTether, e =>
            {
                e.Write(Tether != null);
                if (Tether == null) return;
                e.Write(Tether.ContentHash);
                e.Write((ushort)Tether.Rows.Length);
                foreach (TetherRow row in Tether.Rows)
                {
                    Text(e, row.Name);
                    e.Write(row.Label);
                    e.Write((byte)row.Kind);
                    e.Write((ushort)row.Values.Length);
                    foreach (double x in row.Values) e.Write(x);
                }
            });
            Entry(w, TagRuntimeObjects, e =>
            {
                e.Write((ushort)RuntimeObjects.Length);
                for (int i = 0; i < RuntimeObjects.Length; i++)
                {
                    RuntimeObject o = RuntimeObjects[i];
                    e.Write(i < RuntimeIds.Length ? RuntimeIds[i] : -1);
                    Text(e, o.Name);
                    e.Write((ushort)o.Shapes.Length);
                    foreach (ShapeGeometry g in o.Shapes)
                    {
                        e.Write((byte)g.Kind);
                        V3(e, g.Center);
                        V3(e, g.AxisX);
                        V3(e, g.AxisY);
                        V3(e, g.AxisZ);
                        V3(e, g.HalfExtents);
                        e.Write(g.Radius);
                        e.Write(g.HalfLength);
                        e.Write(g.Material);
                        e.Write(g.Surface);
                        V3(e, g.SpanA);
                        V3(e, g.SpanB);
                        e.Write(g.Sag);
                    }
                }
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
                e.Write(StartPackCharge);
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
                if (r != null)
                {
                    foreach (long n in r.Counts) e.Write(n);
                    e.Write(r.Count);
                    e.Write(r.SumUs);
                    e.Write(r.MinUs);
                    e.Write(r.MaxUs);
                }
                // 1.5, appended: a reader of 1.1's entry stops before it.
                e.Write(StartHostUs);
                InputLatency l = InputLatency;
                e.Write(l != null);
                if (l == null) return;
                e.Write(l.Steps);
                Spread(e, l.FirstStep);
                Spread(e, l.Motor);
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
                    case TagPropulsion:
                        Propulsion = r.ReadBoolean() ? ReadPropulsion(r) : null;
                        break;
                    case TagAero:
                        Aero = r.ReadBoolean()
                            ? new AeroSpec
                            {
                                AirDensity = r.ReadDouble(), BodyArea = V3(r), BodyCenterOfPressure = V3(r), DragCoefficient = r.ReadDouble(),
                                CoilPart = r.ReadInt32(),
                            }
                            : null;
                        break;
                    case TagWind:
                        WindModel = Text(r);
                        Wind = new WindSetup { Level = (WindLevel)r.ReadByte(), SteadySpeed = r.ReadDouble(), SteadyFromDeg = r.ReadDouble(), SteadyOnsetSeconds = r.ReadDouble() };
                        WindParameters = new NamedValue[r.ReadUInt16()];
                        for (int i = 0; i < WindParameters.Length; i++) WindParameters[i] = new NamedValue(Text(r), r.ReadDouble());
                        break;
                    case TagContacts:
                        Contacts = r.ReadBoolean() ? ReadContacts(r) : null;
                        break;
                    case TagAsymmetry:
                        Asymmetry = r.ReadBoolean()
                            ? new MotorAsymmetry
                            {
                                KvBound = r.ReadDouble(), KvSigma = r.ReadDouble(), EscLagBound = r.ReadDouble(), BalanceGrade = r.ReadDouble(),
                                PropMass = r.ReadDouble(), ServiceRpm = r.ReadDouble(),
                            }
                            : null;
                        break;
                    case TagVortexRing:
                        VortexRing = r.ReadBoolean()
                            ? new VortexRingSpec
                            {
                                EntryRate = r.ReadDouble(), ExitRate = r.ReadDouble(), PeakRate = r.ReadDouble(), EndRate = r.ReadDouble(),
                                EntryInflow = r.ReadDouble(), PeakInflow = r.ReadDouble(), EscapeInPlane = r.ReadDouble(), InflowLag = r.ReadDouble(),
                                FluctuationSigma = r.ReadDouble(), FluctuationBound = r.ReadDouble(), FluctuationFrequency = r.ReadDouble(),
                                AirDensity = r.ReadDouble(), FixedInducedVelocity = r.ReadDouble(),
                            }
                            : null;
                        break;
                    case TagGroundEffect:
                        GroundEffect = r.ReadBoolean()
                            ? new GroundEffectSpec
                            {
                                Reach = r.ReadDouble(), LowestHeight = r.ReadDouble(), CeilingAlpha = r.ReadDouble(),
                                RecirculationOnset = r.ReadDouble(), RecirculationSigma = r.ReadDouble(), RecirculationBound = r.ReadDouble(),
                                RecirculationLoop = r.ReadDouble(), AirDensity = r.ReadDouble(),
                            }
                            : null;
                        break;
                    case TagFlapping:
                        if (!r.ReadBoolean())
                        {
                            Flapping = null;
                            break;
                        }
                        Flapping = new RotorFlappingSpec
                        {
                            LiftSlope = r.ReadDouble(), ZeroLiftAngle = r.ReadDouble(), BendingFrequency = r.ReadDouble(),
                            Southwell = r.ReadDouble(), SkewGain = r.ReadDouble(), Radius = r.ReadDouble(),
                            BladeInertia = r.ReadDouble(), AirDensity = r.ReadDouble(), Blades = r.ReadInt32(),
                        };
                        int stations = r.ReadUInt16();
                        Flapping.Stations = new double[stations];
                        Flapping.Chords = new double[stations];
                        Flapping.Angles = new double[stations];
                        for (int i = 0; i < stations; i++)
                        {
                            Flapping.Stations[i] = r.ReadDouble();
                            Flapping.Chords[i] = r.ReadDouble();
                            Flapping.Angles[i] = r.ReadDouble();
                        }
                        break;
                    case TagModes:
                        if (!r.ReadBoolean())
                        {
                            Modes = null;
                            break;
                        }
                        Modes = new AirframeModesSpec
                        {
                            ArmModulus = r.ReadDouble(), ArmWidth = r.ReadDouble(), ArmThickness = r.ReadDouble(), ArmLength = r.ReadDouble(),
                            ArmTipShare = r.ReadDouble(), ArmDamping = r.ReadDouble(), MountStiffness = r.ReadDouble(),
                            MountDamping = r.ReadDouble(), Camera = new Double3(r.ReadDouble(), r.ReadDouble(), r.ReadDouble()),
                            TipMass = new[] { r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadDouble() },
                            PayloadPart = r.ReadInt32(), CoilPart = r.ReadInt32(),
                        };
                        break;
                    case TagTether:
                        if (!r.ReadBoolean())
                        {
                            Tether = null;
                            break;
                        }
                        ulong tetherHash = r.ReadUInt64();
                        var rows = new TetherRow[r.ReadUInt16()];
                        for (int i = 0; i < rows.Length; i++)
                        {
                            rows[i] = new TetherRow { Name = Text(r), Label = r.ReadByte(), Kind = (TetherValueKind)r.ReadByte() };
                            rows[i].Values = new double[r.ReadUInt16()];
                            for (int k = 0; k < rows[i].Values.Length; k++) rows[i].Values[k] = r.ReadDouble();
                        }
                        Tether = new TetherTable(rows, tetherHash);
                        break;
                    case TagRuntimeObjects:
                        RuntimeObjects = new RuntimeObject[r.ReadUInt16()];
                        RuntimeIds = new int[RuntimeObjects.Length];
                        for (int i = 0; i < RuntimeObjects.Length; i++)
                        {
                            RuntimeIds[i] = r.ReadInt32();
                            string name = Text(r);
                            var shapes = new ShapeGeometry[r.ReadUInt16()];
                            for (int k = 0; k < shapes.Length; k++)
                            {
                                shapes[k] = new ShapeGeometry
                                {
                                    Kind = (ShapeKind)r.ReadByte(), Center = V3(r), AxisX = V3(r), AxisY = V3(r), AxisZ = V3(r), HalfExtents = V3(r),
                                    Radius = r.ReadDouble(), HalfLength = r.ReadDouble(), Material = r.ReadUInt16(), Surface = r.ReadByte(),
                                    SpanA = V3(r), SpanB = V3(r), Sag = r.ReadDouble(),
                                };
                            }
                            RuntimeObjects[i] = new RuntimeObject(name, shapes);
                        }
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
                        StartPackCharge = r.ReadDouble();
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
                        if (binUs != 0)
                        {
                            var intervals = new ReportIntervals(binUs, bins);
                            for (int i = 0; i < bins; i++) intervals.Counts[i] = r.ReadInt64();
                            intervals.Count = r.ReadInt64();
                            intervals.SumUs = r.ReadInt64();
                            intervals.MinUs = r.ReadInt64();
                            intervals.MaxUs = r.ReadInt64();
                            InputIntervals = intervals;
                        }
                        if (r.BaseStream.Position >= end) break; // 1.1's entry
                        StartHostUs = r.ReadInt64();
                        if (!r.ReadBoolean()) break;
                        InputLatency = new InputLatency { Steps = r.ReadInt64(), FirstStep = Spread(r), Motor = Spread(r) };
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

        static ContactSpec ReadContacts(BinaryReader r)
        {
            var c = new ContactSpec
            {
                Margin = r.ReadDouble(), CrashAccel = r.ReadDouble(), CrashPropSpeed = r.ReadDouble(), ImpactWindow = r.ReadDouble(),
                ImpactSpeed = r.ReadDouble(), FrictionStiffnessRatio = r.ReadDouble(), DampingMassShare = r.ReadDouble(),
                GroundStaticFriction = r.ReadDouble(), GroundKineticFriction = r.ReadDouble(), LegStiffness = r.ReadDouble(), LegDamping = r.ReadDouble(),
                RingSegments = r.ReadInt32(),
            };
            c.Shapes = new ContactShape[r.ReadUInt16()];
            for (int i = 0; i < c.Shapes.Length; i++)
            {
                c.Shapes[i] = new ContactShape
                {
                    Name = Text(r), Part = r.ReadInt32(), Role = (ContactRole)r.ReadByte(), Corner = r.ReadInt32(), Material = Text(r), A = V3(r), B = V3(r),
                    Radius = r.ReadDouble(), HalfThickness = r.ReadDouble(), Stiffness = r.ReadDouble(), StiffnessUp = r.ReadDouble(), DampingRatio = r.ReadDouble(),
                };
            }
            c.Materials = new MaterialRow[r.ReadUInt16()];
            for (int i = 0; i < c.Materials.Length; i++)
                c.Materials[i] = new MaterialRow
                {
                    Name = Text(r), StaticFriction = r.ReadDouble(), KineticFriction = r.ReadDouble(), Stiffness = r.ReadDouble(), DampingRatio = r.ReadDouble(),
                };
            c.MaterialsHash = r.ReadUInt64();
            return c;
        }

        static PropulsionSpec ReadPropulsion(BinaryReader r)
        {
            var spec = new PropulsionSpec { Motors = new DroneMotor[4], Packs = new DronePack[2] };
            for (int i = 0; i < 4; i++)
            {
                spec.Motors[i] = new DroneMotor
                {
                    Position = V3(r), Clockwise = r.ReadBoolean(), Kv = r.ReadDouble(), Resistance = r.ReadDouble(),
                    NoLoadCurrent = r.ReadDouble(), RotorInertia = r.ReadDouble(), MagnetPoles = r.ReadInt32(), EscTau = r.ReadDouble(),
                    EscCurrentLimit = r.ReadDouble(),
                };
            }
            spec.Props = new DroneProps
            {
                Diameter = r.ReadDouble(), Blades = r.ReadInt32(), Pitch = r.ReadDouble(), ThrustCoefficient = r.ReadDouble(),
                TorqueRatio = r.ReadDouble(), ReferenceRpm = r.ReadDouble(),
                StaticRpm = Numbers(r), StaticThrustRatio = Numbers(r), StaticTorqueRatio = Numbers(r),
                InflowAdvanceRatio = Numbers(r), InflowThrustRatio = Numbers(r), InflowTorqueRatio = Numbers(r),
                HForce = r.ReadDouble(),
            };
            for (int k = 0; k < 2; k++)
            {
                spec.Packs[k] = new DronePack
                {
                    Part = r.ReadInt32(), Series = r.ReadInt32(), Parallel = r.ReadInt32(), CellCapacity = r.ReadDouble(),
                    CellResistance = r.ReadDouble(), Ocv = Numbers(r),
                };
            }
            return spec;
        }

        static void Numbers(BinaryWriter w, double[] xs)
        {
            w.Write((ushort)xs.Length);
            foreach (double x in xs) w.Write(x);
        }

        static double[] Numbers(BinaryReader r)
        {
            var xs = new double[r.ReadUInt16()];
            for (int i = 0; i < xs.Length; i++) xs[i] = r.ReadDouble();
            return xs;
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

        static void Spread(BinaryWriter w, in LatencySpread s)
        {
            w.Write(s.MinUs);
            w.Write(s.P50Us);
            w.Write(s.P99Us);
            w.Write(s.MaxUs);
            w.Write(s.MeanUs);
        }

        static LatencySpread Spread(BinaryReader r) => new LatencySpread
        {
            MinUs = r.ReadInt64(), P50Us = r.ReadInt64(), P99Us = r.ReadInt64(), MaxUs = r.ReadInt64(), MeanUs = r.ReadDouble(),
        };

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
