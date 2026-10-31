using System;
using System.Collections.Generic;
using System.Text;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// One motor and its rotor, at a corner in Betaflight's Quad X order (Corners): a DC motor seen through its ESC
    /// (Propulsion), whose first-order lag τ_m = J / (Kt·Ke/R + ∂Q/∂ω) follows from these constants and the load.
    /// </summary>
    public struct DroneMotor
    {
        /// <summary>The prop's hub, where its thrust acts, body m.</summary>
        public Double3 Position;

        /// <summary>Clockwise seen from above: a positive rotation about body +y by DetMath's rule.</summary>
        public bool Clockwise;

        /// <summary>rpm per volt, unloaded.</summary>
        public double Kv;

        /// <summary>The winding's, the ESC's and the leads' resistance in the motor's DC model, Ω.</summary>
        public double Resistance;

        /// <summary>The no-load current, A: the bearings' and the iron's losses as a torque Kt·I0.</summary>
        public double NoLoadCurrent;

        /// <summary>The bell, shaft and prop's moment of inertia about the shaft, kg·m².</summary>
        public double RotorInertia;

        /// <summary>The rotor's magnet poles (electrical frequency = rpm / 60 × poles / 2).</summary>
        public int MagnetPoles;

        /// <summary>The ESC's command filter τ_esc, s.</summary>
        public double EscTau;

        /// <summary>The ESC's current limit, A: the motor's current never exceeds it while it drives (Propulsion).</summary>
        public double EscCurrentLimit;
    }

    /// <summary>
    /// The four props, alike: k_T and κ static at ReferenceRpm, the static coefficients' change with rpm, and the
    /// coefficients' fall with the advance ratio J = V_a / (n·D) of the axial inflow, as ratio tables.
    /// </summary>
    public struct DroneProps
    {
        public double Diameter;
        public int Blades;

        /// <summary>The geometric pitch, m per revolution.</summary>
        public double Pitch;

        /// <summary>k_T in T = k_T·n², N per (rad/s)², static at ReferenceRpm.</summary>
        public double ThrustCoefficient;

        /// <summary>κ in Q = κ·T, N·m per N, static at ReferenceRpm.</summary>
        public double TorqueRatio;

        /// <summary>The rpm k_T and κ hold at.</summary>
        public double ReferenceRpm;

        /// <summary>Static rpm points, and C_T and C_P there over their values at ReferenceRpm.</summary>
        public double[] StaticRpm, StaticThrustRatio, StaticTorqueRatio;

        /// <summary>Advance-ratio points from 0, and C_T and C_P there over their static values.</summary>
        public double[] InflowAdvanceRatio, InflowThrustRatio, InflowTorqueRatio;

        /// <summary>The rotor's in-plane drag (the H-force), N per m/s of the hub's in-plane airspeed at ReferenceRpm;
        /// it grows in proportion to the rotor's speed (Propulsion.HForce).</summary>
        public double HForce;
    }

    /// <summary>One battery pack: its mass part and its cells.</summary>
    public sealed class DronePack
    {
        /// <summary>The pack's part, an index into DroneSpec.Parts.</summary>
        public int Part;

        public int Series, Parallel;

        /// <summary>One cell's capacity, A·h.</summary>
        public double CellCapacity;

        /// <summary>One cell's internal resistance, Ω.</summary>
        public double CellResistance;

        /// <summary>One cell's open-circuit voltage, V, at states of charge 0, 1/(n−1), …, 1.</summary>
        public double[] Ocv;
    }

    /// <summary>The legs: their parts, one per corner in Betaflight's order, and each foot's spring and damper.</summary>
    public sealed class DroneLegs
    {
        /// <summary>The legs' parts, indices into DroneSpec.Parts, in Corners order. A leg's length is its part's.</summary>
        public int[] Parts;

        /// <summary>Each leg's own spring k, N/m, in series with the ground.</summary>
        public double Stiffness;

        /// <summary>Each leg's damper c, N·s/m.</summary>
        public double Damping;

        public double FootRadius;
    }

    /// <summary>The legs-off build (D-043): the parts that come off with the legs, and the parts that move.</summary>
    public sealed class DroneLegsOff
    {
        /// <summary>Indices into DroneSpec.Parts: every leg part, and whatever comes off with them (feet, skids).</summary>
        public int[] Removed;

        /// <summary>Indices into DroneSpec.Parts of the parts that move, and each one's centre there, body m.</summary>
        public int[] Moved;
        public Double3[] MovedTo;
    }

    /// <summary>The fiber spool: its part, whose mass falls as the line pays out.</summary>
    public struct DroneCoil
    {
        public int Part;

        /// <summary>The empty spool and housing, kg.</summary>
        public double SpoolMass;

        /// <summary>The line on a full spool, kg.</summary>
        public double FiberMass;

        /// <summary>The line's length on a full spool, m.</summary>
        public double FiberLength;
    }

    /// <summary>The payload: its part, whose mass is the pilot's payload setting.</summary>
    public struct DronePayload
    {
        public int Part;
        public double MinMass, MaxMass;
    }

    /// <summary>The legs-off launch stand (D-022, D-043): four posts on the motor diagonals.</summary>
    public struct DroneStand
    {
        public string Material;

        /// <summary>Each post's axis from the stand's centre, m.</summary>
        public double PostRadius;

        /// <summary>The post tops above the ground, m.</summary>
        public double PostHeight;

        public double PostDiameter;
        public double Mass;
    }

    /// <summary>The pilot's settings that touch the mass build (K§6.3), and the coil's payout.</summary>
    public struct Loadout
    {
        /// <summary>The payload, kg (APP-03: 0–2.0).</summary>
        public double Payload;

        /// <summary>Legs on; off is drone.json's legs-off build (the legs' parts off, the spool moved), started on the stand.</summary>
        public bool Legs;

        /// <summary>Line paid out, m: 0 at take-off.</summary>
        public double CoilPaidOut;
    }

    /// <summary>drone.json refused: every problem the validator found, each with its place in the file.</summary>
    public sealed class DroneSpecException : Exception
    {
        public readonly IReadOnlyList<string> Problems;

        public DroneSpecException(List<string> problems)
            : base("drone.json is not valid:\n  " + string.Join("\n  ", problems)) => Problems = problems;
    }

    /// <summary>
    /// drone.json (DRN-03, p6-flight-core design 3): the drone's one component spec, which the physics' mass build
    /// and the visual model's generator both read. Parse validates the whole file and refuses it with every problem
    /// listed; a parsed spec is read-only by convention. The format, field by field, is the file's own "fields" block.
    ///
    /// Every number is labelled: {"value": …, "label": "pilot" | "measured" | "published" | "derived" | "assumed",
    /// "note": "…"}, the note optional. Unknown members are refused, so a misspelt field never passes as absent.
    /// </summary>
    public sealed class DroneSpec
    {
        public const int Schema = 1;

        public static readonly string[] Labels = { "pilot", "measured", "published", "derived", "assumed" };

        /// <summary>The drone part materials (a closed list; physics signs off additions).</summary>
        public static readonly string[] Materials = { "carbon", "aluminium", "steel", "polymer", "li_ion", "electronics" };

        public string Name;

        /// <summary>The file's identity, which the flight log records and a replay checks (HashText).</summary>
        public ulong ContentHash;

        /// <summary>True for p6-flight-core's bootstrap file, which p5-drone replaces.</summary>
        public bool Bootstrap;

        public DronePart[] Parts;

        /// <summary>The four motors, in Betaflight's Quad X order: rear right, front right, rear left, front left.</summary>
        public DroneMotor[] Motors;

        public DroneProps Props;

        /// <summary>The two packs (PHB-03).</summary>
        public DronePack[] Packs;

        public DroneLegs Legs;
        public DroneLegsOff LegsOff;
        public DroneCoil Coil;
        public DronePayload Payload;
        public DroneStand Stand;

        /// <summary>The airframe's drag; its coil part is the coil's.</summary>
        public AeroSpec Aero;

        /// <summary>The contact shapes and thresholds (p6-flight-core 1.6), the shapes at the file's positions; null when the
        /// file has no contacts block (the bootstrap fixture). BuildContacts places them at a loadout.</summary>
        public Contact.ContactSpec Contacts;

        /// <summary>The drone side's contact materials (a closed list; physics signs off additions): the sheet's
        /// carbon_composite for the arms, plates, legs and skids (p5-drone 1.2), polymer, aluminium.</summary>
        public static readonly string[] ContactMaterials = { "carbon_composite", "polymer", "aluminium" };

        /// <summary>The file's own loadout: its payload and coil masses, legs on.</summary>
        public Loadout Nominal => new Loadout
        {
            Payload = Parts[Payload.Part].Mass,
            Legs = true,
            CoilPaidOut = (Coil.SpoolMass + Coil.FiberMass - Parts[Coil.Part].Mass) / Coil.FiberMass * Coil.FiberLength,
        };

        /// <summary>The coil's mass with paidOut metres of line gone.</summary>
        public double CoilMass(double paidOut) => Coil.SpoolMass + Coil.FiberMass * (1 - paidOut / Coil.FiberLength);

        /// <summary>
        /// The mass model at a loadout: the payload, the legs and the coil set on the file's parts. Legs off is the
        /// file's legs_off build: its removed parts weigh nothing and its moved parts move. The payload part is the
        /// file's body at the file's payload mass; another mass scales it uniformly by the cube root of the ratio,
        /// growing away from the arm plane (DRN-03: the face toward the frame stays where the file puts it).
        /// </summary>
        public MassModel BuildMass(in Loadout loadout)
        {
            if (!(loadout.Payload >= Payload.MinMass && loadout.Payload <= Payload.MaxMass))
                throw new ArgumentOutOfRangeException(nameof(loadout), "the payload is outside " + Payload.MinMass + "–" + Payload.MaxMass + " kg");
            if (!(loadout.CoilPaidOut >= 0 && loadout.CoilPaidOut <= Coil.FiberLength))
                throw new ArgumentOutOfRangeException(nameof(loadout), "the paid-out line is outside 0–" + Coil.FiberLength + " m");
            var parts = (DronePart[])Parts.Clone();
            if (!loadout.Legs)
            {
                foreach (int part in LegsOff.Removed)
                    parts[part].Mass = 0;
                for (int i = 0; i < LegsOff.Moved.Length; i++)
                    parts[LegsOff.Moved[i]].Position = LegsOff.MovedTo[i];
            }
            parts[Coil.Part].Mass = CoilMass(loadout.CoilPaidOut);
            ref DronePart payload = ref parts[Payload.Part];
            ScaleAwayFromArmPlane(ref payload, DetMath.Cbrt(loadout.Payload / payload.Mass));
            payload.Mass = loadout.Payload;
            return new MassModel(parts);
        }

        /// <summary>The flight's propulsion: copies of the motors, the props and the packs.</summary>
        public PropulsionSpec BuildPropulsion()
        {
            var packs = new DronePack[Packs.Length];
            for (int i = 0; i < packs.Length; i++)
            {
                DronePack p = Packs[i];
                packs[i] = new DronePack
                {
                    Part = p.Part, Series = p.Series, Parallel = p.Parallel, CellCapacity = p.CellCapacity,
                    CellResistance = p.CellResistance, Ocv = (double[])p.Ocv.Clone(),
                };
            }
            DroneProps props = Props;
            props.StaticRpm = (double[])Props.StaticRpm.Clone();
            props.StaticThrustRatio = (double[])Props.StaticThrustRatio.Clone();
            props.StaticTorqueRatio = (double[])Props.StaticTorqueRatio.Clone();
            props.InflowAdvanceRatio = (double[])Props.InflowAdvanceRatio.Clone();
            props.InflowThrustRatio = (double[])Props.InflowThrustRatio.Clone();
            props.InflowTorqueRatio = (double[])Props.InflowTorqueRatio.Clone();
            return new PropulsionSpec { Motors = (DroneMotor[])Motors.Clone(), Props = props, Packs = packs };
        }

        /// <summary>The flight's airframe drag: a copy of the aero block.</summary>
        public AeroSpec BuildAero() => Aero.Clone();

        /// <summary>The motors' asymmetry at its researched tolerances (MotorAsymmetry), the props' mass from the parts
        /// (the rear right prop's, "prop_rr"; the four are alike) and the service speed at the props' reference rpm.</summary>
        public MotorAsymmetry BuildAsymmetry()
        {
            var a = new MotorAsymmetry { ServiceRpm = Props.ReferenceRpm };
            foreach (DronePart p in Parts)
                if (p.Name == "prop_rr") a.PropMass = p.Mass;
            return a;
        }

        /// <summary>The rotors' vortex rings at the published boundaries (VortexRingSpec), in the aero block's air.</summary>
        public VortexRingSpec BuildVortexRing() => new VortexRingSpec { AirDensity = Aero.AirDensity };

        /// <summary>The rotors' ground and ceiling effect and recirculation (GroundEffectSpec), in the aero block's air.</summary>
        public GroundEffectSpec BuildGroundEffect() => new GroundEffectSpec { AirDensity = Aero.AirDensity };

        /// <summary>The airframe's modes (AirframeModesSpec, p7b-airframe 1.4): each arm's tip mass from its corner's motor,
        /// prop and arm parts ("motor_rr", "prop_rr", "arm_rr", …), and the payload's and the coil's parts.</summary>
        public AirframeModesSpec BuildModes()
        {
            var m = new AirframeModesSpec { PayloadPart = Payload.Part, CoilPart = Coil.Part };
            string[] corners = { "rr", "fr", "rl", "fl" };
            for (int i = 0; i < 4; i++)
                m.TipMass[i] = Parts[PartIndex("motor_" + corners[i])].Mass + Parts[PartIndex("prop_" + corners[i])].Mass
                    + m.ArmTipShare * Parts[PartIndex("arm_" + corners[i])].Mass;
            return m;
        }

        /// <summary>
        /// The rotors' flapping (RotorFlappingSpec) on the props' sheet, in the aero block's air: the radius and blades from
        /// the props; a blade's flap inertia from the prop's part ("prop_rr") as a uniform rod, m/N_b·R²/3 (derived, as the
        /// mass build's rod); the chord at 19 stations from 0.1 R to the tip, linear through the sheet's 18 mm at 0.1 R
        /// (assumed), 26 mm at 0.3 R and 8 mm at the tip (the sheet, docs/object-sheets/drone.md); the angle there the
        /// props' helix less the zero-lift angle.
        /// </summary>
        public RotorFlappingSpec BuildFlapping()
        {
            double radius = Props.Diameter / 2, mass = 0;
            foreach (DronePart p in Parts)
                if (p.Name == "prop_rr") mass = p.Mass;
            var f = new RotorFlappingSpec
            {
                AirDensity = Aero.AirDensity, Radius = radius, Blades = Props.Blades,
                BladeInertia = mass / Props.Blades * radius * radius / 3,
                Stations = new double[19], Chords = new double[19], Angles = new double[19],
            };
            for (int k = 0; k < 19; k++)
            {
                double x = 0.1 + 0.05 * k;
                f.Stations[k] = x;
                f.Chords[k] = x < 0.3 ? 0.018 + (x - 0.1) / 0.2 * 0.008 : 0.026 - (x - 0.3) / 0.7 * 0.018;
                f.Angles[k] = f.HelixAngle(Props.Pitch, x);
            }
            return f;
        }

        /// <summary>
        /// The flight's contact model at a loadout, with physics_materials.json's rows and hash (PhysicsMaterials.Parse):
        /// each shape follows its part through BuildMass, so a part at 0 kg (legs off, no payload) has no shapes, a moved
        /// part's shapes move with it, and the payload's scale with it. Null when the file has no contacts block.
        /// </summary>
        public Contact.ContactSpec BuildContacts(in Loadout loadout, Contact.MaterialRow[] materials, ulong materialsHash)
        {
            if (Contacts == null) return null;
            if (materials == null) throw new ArgumentNullException(nameof(materials));
            MassModel model = BuildMass(loadout);
            Contact.ContactSpec c = Contacts.Clone();
            var shapes = new System.Collections.Generic.List<Contact.ContactShape>();
            foreach (Contact.ContactShape s in Contacts.Shapes)
            {
                DronePart file = Parts[s.Part], now = model.Part(s.Part);
                if (!(now.Mass > 0)) continue;
                double scale = file.Kind == PartKind.Box ? now.Size.X / file.Size.X : now.Length / file.Length;
                Contact.ContactShape placed = s;
                if (scale != 1 || now.Position.X != file.Position.X || now.Position.Y != file.Position.Y || now.Position.Z != file.Position.Z)
                {
                    placed.A = now.Position + (s.A - file.Position) * scale;
                    placed.B = now.Position + (s.B - file.Position) * scale;
                    placed.Radius = s.Radius * scale;
                    placed.HalfThickness = s.HalfThickness * scale;
                }
                shapes.Add(placed);
            }
            c.Shapes = shapes.ToArray();
            c.LegStiffness = Legs.Stiffness;
            c.LegDamping = Legs.Damping;
            c.Materials = (Contact.MaterialRow[])materials.Clone();
            c.MaterialsHash = materialsHash;
            return c;
        }

        // A part scaled uniformly by s about the middle of its face toward the arm plane: its sizes times s, and its
        // centre moved away from the plane by its half-extent along y times (s − 1). s = 1 leaves every bit as it was.
        static void ScaleAwayFromArmPlane(ref DronePart p, double s)
        {
            double half;
            if (p.Kind == PartKind.Box)
            {
                half = p.Size.Y / 2;
                p.Size = p.Size * s;
            }
            else
            {
                double ay = DetMath.Abs(p.Axis.Y);
                half = p.Length / 2 * ay + p.Diameter / 2 * DetMath.Sqrt(1 - ay * ay);
                p.Diameter *= s;
                p.Length *= s;
            }
            double away = half * (s - 1);
            if (p.Position.Y < 0) p.Position.Y -= away;
            else if (p.Position.Y > 0) p.Position.Y += away;
        }

        /// <summary>The index of the part named name, or −1.</summary>
        public int PartIndex(string name)
        {
            for (int i = 0; i < Parts.Length; i++)
                if (string.Equals(Parts[i].Name, name, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        /// <summary>Reads and validates drone.json's text. Throws DroneSpecException listing every problem.</summary>
        public static DroneSpec Parse(string json)
        {
            JsonNode root;
            try
            {
                root = DetJson.Parse(json);
            }
            catch (FormatException e)
            {
                throw new DroneSpecException(new List<string> { e.Message });
            }
            var reader = new Reader();
            DroneSpec spec = reader.Read(root);
            if (reader.Problems.Count > 0) throw new DroneSpecException(reader.Problems);
            spec.ContentHash = HashText(json);
            return spec;
        }

        /// <summary>
        /// drone.json's identity: WorldHash's rule over the text's UTF-8 bytes, CR LF read as LF, so a CRLF and an LF
        /// checkout of one file hash alike and any other edit changes it.
        /// </summary>
        public static ulong HashText(string json) => WorldHash.HashFile(WorldHash.Offset, Encoding.UTF8.GetBytes(json), true);

        // The validator: it reads everything it can and records a problem for anything missing, misspelt, unlabelled or
        // out of range, so one run lists them all. A value that could not be read is NaN (or −1 for a part reference),
        // and the checks that depend on it stay quiet, so each fault is reported once.
        sealed class Reader
        {
            public readonly List<string> Problems = new List<string>();

            // Betaflight's Quad X corners and their sides (+x right, +z forward).
            static readonly string[] Corner = { "rear right", "front right", "rear left", "front left" };
            static readonly int[] Side = { +1, +1, -1, -1 }, End = { -1, +1, -1, +1 };

            void Fail(string path, string what) => Problems.Add(path + ": " + what);

            public DroneSpec Read(JsonNode root)
            {
                var spec = new DroneSpec
                {
                    Parts = new DronePart[0],
                    Motors = new DroneMotor[4],
                    Packs = new DronePack[2],
                    Legs = new DroneLegs { Parts = new int[0] },
                    LegsOff = new DroneLegsOff { Removed = new int[0], Moved = new int[0], MovedTo = new Double3[0] },
                    Coil = new DroneCoil { Part = -1 },
                    Payload = new DronePayload { Part = -1 },
                    Aero = new AeroSpec(),
                };
                const string top = "drone.json";
                if (!IsObject(root, top)) return spec;
                Members(root, top, "schema", "owner", "name", "bootstrap", "about", "fields",
                    "parts", "motors", "props", "packs", "legs", "legs_off", "coil", "payload", "stand", "aero", "contacts");

                JsonNode schema = Required(root, "schema", top);
                if (schema != null && !(schema.Kind == JsonKind.Number && schema.Number == Schema))
                    Fail(top + ".schema", "must be " + Schema);
                Text(root, "owner", top, null, true);
                Text(root, "about", top, null, true);
                JsonNode fields = root.Member("fields");
                if (fields != null && IsObject(fields, top + ".fields"))
                    for (int i = 0; i < fields.Items.Count; i++)
                        if (fields.Items[i].Kind != JsonKind.String && fields.Items[i].Kind != JsonKind.Object)
                            Fail(top + ".fields." + fields.Keys[i], "must describe the field in words");
                spec.Name = Text(root, "name", top, null);
                JsonNode bootstrap = Required(root, "bootstrap", top);
                if (bootstrap != null)
                {
                    if (bootstrap.Kind == JsonKind.Bool) spec.Bootstrap = bootstrap.Bool;
                    else Fail(top + ".bootstrap", "must be true or false");
                }

                JsonNode n;
                if ((n = Required(root, "parts", top)) != null) spec.Parts = ReadParts(n);
                if ((n = Required(root, "motors", top)) != null) ReadMotors(n, spec.Motors);
                if ((n = Required(root, "props", top)) != null) spec.Props = ReadProps(n);
                if ((n = Required(root, "packs", top)) != null) ReadPacks(n, spec);
                if ((n = Required(root, "legs", top)) != null) ReadLegs(n, spec);
                if ((n = Required(root, "coil", top)) != null) spec.Coil = ReadCoil(n, spec);
                if ((n = Required(root, "payload", top)) != null) spec.Payload = ReadPayload(n, spec);
                if ((n = Required(root, "legs_off", top)) != null) ReadLegsOff(n, spec);
                if ((n = Required(root, "stand", top)) != null) spec.Stand = ReadStand(n);
                if ((n = Required(root, "aero", top)) != null) ReadAero(n, spec);
                if ((n = root.Member("contacts")) != null) spec.Contacts = ReadContacts(n, spec);
                return spec;
            }

            static readonly string[] CornerIds = { "rr", "fr", "rl", "fl" };

            // After the parts, the motors, the props and the legs, which it checks against.
            Contact.ContactSpec ReadContacts(JsonNode p, DroneSpec spec)
            {
                var c = new Contact.ContactSpec();
                const string at = "contacts";
                if (!IsObject(p, at)) return c;
                Members(p, at, "margin_m", "crash_accel_m_s2", "crash_prop_speed_m_s", "impact_window_s", "impact_speed_m_s",
                    "friction_stiffness_ratio", "damping_mass_share", "ground_friction_static", "ground_friction_kinetic", "ring_segments", "shapes");
                c.Margin = Positive(p, "margin_m", at);
                c.CrashAccel = Positive(p, "crash_accel_m_s2", at);
                c.CrashPropSpeed = Positive(p, "crash_prop_speed_m_s", at);
                c.ImpactWindow = Positive(p, "impact_window_s", at);
                c.ImpactSpeed = Positive(p, "impact_speed_m_s", at);
                c.FrictionStiffnessRatio = Positive(p, "friction_stiffness_ratio", at);
                c.DampingMassShare = Positive(p, "damping_mass_share", at);
                c.GroundStaticFriction = NonNegative(p, "ground_friction_static", at);
                c.GroundKineticFriction = NonNegative(p, "ground_friction_kinetic", at);
                if (c.GroundKineticFriction > c.GroundStaticFriction) Fail(at + ".ground_friction_kinetic", "is above ground_friction_static");
                c.RingSegments = Count(p, "ring_segments", at, 6);
                JsonNode list = Required(p, "shapes", at);
                if (list == null || !IsArray(list, at + ".shapes", 1)) return c;
                c.Shapes = new Contact.ContactShape[list.Items.Count];
                for (int i = 0; i < c.Shapes.Length; i++)
                {
                    JsonNode s = list.Items[i];
                    string path = at + ".shapes[" + i + "]";
                    ref Contact.ContactShape shape = ref c.Shapes[i];
                    shape.Part = -1;
                    shape.Corner = -1;
                    if (!IsObject(s, path)) continue;
                    JsonNode nameNode = s.Member("name");
                    if (nameNode != null && nameNode.Kind == JsonKind.String) path += " (" + nameNode.Text + ")";
                    Members(s, path, "name", "part", "role", "corner", "material", "a_m", "b_m", "radius_m", "half_thickness_m",
                        "stiffness_n_per_m", "stiffness_up_n_per_m", "damping_ratio");
                    shape.Name = Text(s, "name", path, null);
                    for (int j = 0; j < i; j++)
                        if (shape.Name != null && string.Equals(c.Shapes[j].Name, shape.Name, StringComparison.Ordinal))
                            Fail(path + ".name", "is used twice");
                    shape.Part = PartRef(s, "part", path, spec);
                    string role = Text(s, "role", path, new[] { "support", "skid", "body", "disc" });
                    shape.Role = role == "support" ? Contact.ContactRole.Support : role == "skid" ? Contact.ContactRole.Skid
                        : role == "disc" ? Contact.ContactRole.Disc : Contact.ContactRole.Body;
                    shape.Material = Text(s, "material", path, ContactMaterials);
                    bool cornered = role == "support" || role == "skid" || role == "disc";
                    string corner = Text(s, "corner", path, CornerIds, !cornered);
                    if (corner != null && !cornered) Fail(path + ".corner", "only a support, a skid or a disc has a corner");
                    shape.Corner = corner == null ? -1 : System.Array.IndexOf(CornerIds, corner);
                    Vector(s, "a_m", path, out shape.A);
                    bool capsule = s.Member("b_m") != null;
                    if (capsule) Vector(s, "b_m", path, out shape.B);
                    else shape.B = shape.A;
                    if (capsule && role != "skid" && role != "body") Fail(path + ".b_m", "a support and a disc are points: a_m only");
                    if (!capsule && role == "skid") Fail(path + ".b_m", "a skid is a capsule: it needs b_m");
                    shape.Radius = Positive(s, "radius_m", path);
                    bool disc = role == "disc", body = role == "body";
                    if (disc) shape.HalfThickness = Positive(s, "half_thickness_m", path);
                    else if (s.Member("half_thickness_m") != null) Fail(path + ".half_thickness_m", "only a disc has a thickness");
                    if (body)
                    {
                        shape.Stiffness = Positive(s, "stiffness_n_per_m", path);
                        shape.StiffnessUp = s.Member("stiffness_up_n_per_m") != null ? Positive(s, "stiffness_up_n_per_m", path) : shape.Stiffness;
                        shape.DampingRatio = NonNegative(s, "damping_ratio", path);
                    }
                    else if (s.Member("stiffness_n_per_m") != null || s.Member("stiffness_up_n_per_m") != null || s.Member("damping_ratio") != null)
                        Fail(path + ".stiffness_n_per_m", "only a body shape has its own compliance: the legs carry supports and skids");
                    // The consistency drone.json's other blocks fix.
                    if (role == "support" && !double.IsNaN(shape.Radius) && shape.Radius != spec.Legs.FootRadius)
                        Fail(path + ".radius_m", "a support is the foot: legs.foot_radius_m");
                    if (disc && shape.Corner >= 0)
                    {
                        Double3 hub = spec.Motors[shape.Corner].Position;
                        if (!(shape.A.X == hub.X && shape.A.Y == hub.Y && shape.A.Z == hub.Z)) Fail(path + ".a_m", "a disc sits at its motor's position_m");
                        if (!double.IsNaN(shape.Radius) && shape.Radius != spec.Props.Diameter / 2) Fail(path + ".radius_m", "a disc is the flown prop: props.diameter_m / 2");
                    }
                }
                return c;
            }

            DronePart[] ReadParts(JsonNode list)
            {
                if (!IsArray(list, "parts", 1)) return new DronePart[0];
                var parts = new DronePart[list.Items.Count];
                for (int i = 0; i < parts.Length; i++)
                {
                    JsonNode p = list.Items[i];
                    string path = "parts[" + i + "]";
                    ref DronePart part = ref parts[i];
                    part.Mass = double.NaN;
                    if (!IsObject(p, path)) continue;
                    JsonNode nameNode = p.Member("name");
                    if (nameNode != null && nameNode.Kind == JsonKind.String) path += " (" + nameNode.Text + ")";
                    Members(p, path, "name", "kind", "material", "mass_kg", "position_m", "size_m", "diameter_m", "length_m", "axis");

                    part.Name = Text(p, "name", path, null);
                    for (int j = 0; j < i; j++)
                        if (part.Name != null && string.Equals(parts[j].Name, part.Name, StringComparison.Ordinal))
                            Fail(path + ".name", "is used twice");
                    string kind = Text(p, "kind", path, new[] { "box", "cylinder" });
                    part.Material = Text(p, "material", path, Materials);
                    part.Mass = Positive(p, "mass_kg", path);
                    Vector(p, "position_m", path, out part.Position);
                    if (kind == "box")
                    {
                        part.Kind = PartKind.Box;
                        if (Vector(p, "size_m", path, out part.Size) && !(part.Size.X > 0 && part.Size.Y > 0 && part.Size.Z > 0))
                            Fail(path + ".size_m", "every extent must be above 0");
                        foreach (string other in new[] { "diameter_m", "length_m", "axis" })
                            if (p.Member(other) != null) Fail(path + "." + other, "a box has size_m only");
                    }
                    else if (kind == "cylinder")
                    {
                        part.Kind = PartKind.Cylinder;
                        part.Diameter = Positive(p, "diameter_m", path);
                        part.Length = Positive(p, "length_m", path);
                        if (Vector(p, "axis", path, out Double3 axis))
                        {
                            double norm = axis.Length();
                            if (norm > 0) part.Axis = axis * (1 / norm);
                            else Fail(path + ".axis", "must not be zero");
                        }
                        if (p.Member("size_m") != null) Fail(path + ".size_m", "a cylinder has diameter_m, length_m and axis");
                    }
                }
                return parts;
            }

            void ReadMotors(JsonNode list, DroneMotor[] motors)
            {
                if (!IsArray(list, "motors", 4)) return;
                if (list.Items.Count != 4) Fail("motors", "a quad has 4 motors, in Betaflight's Quad X order");
                var spins = new string[4];
                for (int i = 0; i < 4 && i < list.Items.Count; i++)
                {
                    JsonNode m = list.Items[i];
                    string path = "motors[" + i + "] (" + Corner[i] + ")";
                    if (!IsObject(m, path)) continue;
                    Members(m, path, "position_m", "spin", "kv_rpm_per_v", "resistance_ohm", "no_load_current_a",
                        "rotor_inertia_kg_m2", "magnet_poles", "esc_tau_s", "esc_current_limit_a");
                    ref DroneMotor motor = ref motors[i];
                    if (Vector(m, "position_m", path, out motor.Position)) AtCorner(motor.Position, i, path + ".position_m");
                    spins[i] = Text(m, "spin", path, new[] { "cw", "ccw" });
                    motor.Clockwise = spins[i] == "cw";
                    motor.Kv = Positive(m, "kv_rpm_per_v", path);
                    motor.Resistance = Positive(m, "resistance_ohm", path);
                    motor.NoLoadCurrent = NonNegative(m, "no_load_current_a", path);
                    motor.RotorInertia = Positive(m, "rotor_inertia_kg_m2", path);
                    motor.MagnetPoles = Count(m, "magnet_poles", path, 2);
                    if (motor.MagnetPoles % 2 != 0) Fail(path + ".magnet_poles", "poles come in pairs");
                    motor.EscTau = Positive(m, "esc_tau_s", path);
                    motor.EscCurrentLimit = Positive(m, "esc_current_limit_a", path);
                }
                // The diagonal pairs spin alike and the two pairs oppositely, so the rotor torques cancel at hover.
                if (System.Array.IndexOf(spins, null) < 0
                    && !(spins[0] == spins[3] && spins[1] == spins[2] && spins[0] != spins[1]))
                    Fail("motors", "the diagonal pairs must spin alike and the two pairs oppositely");
            }

            DroneProps ReadProps(JsonNode p)
            {
                var props = new DroneProps();
                if (!IsObject(p, "props")) return props;
                Members(p, "props", "diameter_m", "blades", "pitch_m", "k_t_n_per_rad2_s2", "kappa_m", "reference_rpm",
                    "static_rpm", "static_thrust_ratio", "static_torque_ratio",
                    "inflow_advance_ratio", "inflow_thrust_ratio", "inflow_torque_ratio", "h_force_n_per_m_s");
                props.Diameter = Positive(p, "diameter_m", "props");
                props.Blades = Count(p, "blades", "props", 2);
                props.Pitch = Positive(p, "pitch_m", "props");
                props.ThrustCoefficient = Positive(p, "k_t_n_per_rad2_s2", "props");
                props.TorqueRatio = Positive(p, "kappa_m", "props");
                props.ReferenceRpm = Positive(p, "reference_rpm", "props");
                props.StaticRpm = Numbers(p, "static_rpm", "props", 2);
                props.StaticThrustRatio = Numbers(p, "static_thrust_ratio", "props", 2);
                props.StaticTorqueRatio = Numbers(p, "static_torque_ratio", "props", 2);
                props.InflowAdvanceRatio = Numbers(p, "inflow_advance_ratio", "props", 2);
                props.InflowThrustRatio = Numbers(p, "inflow_thrust_ratio", "props", 2);
                props.InflowTorqueRatio = Numbers(p, "inflow_torque_ratio", "props", 2);
                props.HForce = NonNegative(p, "h_force_n_per_m_s", "props");
                Curve("props.static", props.StaticRpm, props.StaticThrustRatio, props.StaticTorqueRatio, false);
                Curve("props.inflow", props.InflowAdvanceRatio, props.InflowThrustRatio, props.InflowTorqueRatio, true);
                // The ratios are 1 where k_T and κ hold: at the reference rpm, and static (J = 0).
                if (props.StaticRpm.Length > 1 && props.StaticRpm.Length == props.StaticThrustRatio.Length
                    && props.StaticRpm.Length == props.StaticTorqueRatio.Length && !double.IsNaN(props.ReferenceRpm))
                {
                    int at = System.Array.IndexOf(props.StaticRpm, props.ReferenceRpm);
                    if (at < 0) Fail("props.reference_rpm", "must be one of static_rpm's points");
                    else if (props.StaticThrustRatio[at] != 1 || props.StaticTorqueRatio[at] != 1)
                        Fail("props.static_thrust_ratio", "both ratios must be 1 at the reference rpm");
                }
                if (props.InflowThrustRatio.Length > 0 && props.InflowTorqueRatio.Length > 0
                    && (props.InflowThrustRatio[0] != 1 || props.InflowTorqueRatio[0] != 1))
                    Fail("props.inflow_thrust_ratio", "both ratios must be 1 at J = 0, the static point");
                return props;
            }

            // A curve's points: increasing x (from 0 for the advance ratio) and non-negative ratios, one per point.
            void Curve(string path, double[] x, double[] thrust, double[] torque, bool fromZero)
            {
                if (thrust.Length != x.Length || torque.Length != x.Length)
                {
                    Fail(path, "the ratios need one value per point");
                    return;
                }
                for (int k = 0; k < x.Length; k++)
                {
                    if (k > 0 && !(x[k] > x[k - 1])) Fail(path, "the points must increase");
                    if (!(thrust[k] >= 0 && torque[k] >= 0)) Fail(path, "a ratio is not negative");
                }
                if (x.Length > 0 && (fromZero ? x[0] != 0 : !(x[0] > 0))) Fail(path, fromZero ? "the advance ratio starts at 0" : "rpm is above 0");
            }

            void ReadPacks(JsonNode list, DroneSpec spec)
            {
                if (!IsArray(list, "packs", 2)) return;
                if (list.Items.Count != 2) Fail("packs", "the drone carries two packs (PHB-03)");
                for (int i = 0; i < 2 && i < list.Items.Count; i++)
                {
                    JsonNode p = list.Items[i];
                    string path = "packs[" + i + "]";
                    var pack = spec.Packs[i] = new DronePack { Part = -1, Ocv = new double[0] };
                    if (!IsObject(p, path)) continue;
                    Members(p, path, "part", "series", "parallel", "cell_capacity_ah", "r_cell_ohm", "ocv_v");
                    pack.Part = PartRef(p, "part", path, spec);
                    if (i == 1 && pack.Part >= 0 && pack.Part == spec.Packs[0].Part) Fail(path + ".part", "both packs name one part");
                    pack.Series = Count(p, "series", path, 1);
                    pack.Parallel = Count(p, "parallel", path, 1);
                    pack.CellCapacity = Positive(p, "cell_capacity_ah", path);
                    pack.CellResistance = Positive(p, "r_cell_ohm", path);
                    pack.Ocv = Numbers(p, "ocv_v", path, 2);
                    for (int k = 0; k < pack.Ocv.Length; k++)
                    {
                        if (!(pack.Ocv[k] >= 2.5 && pack.Ocv[k] <= 4.4))
                            Fail(path + ".ocv_v", "a Li-ion cell's voltage is 2.5–4.4 V, not " + pack.Ocv[k]);
                        if (k > 0 && pack.Ocv[k] < pack.Ocv[k - 1])
                            Fail(path + ".ocv_v", "the voltage must not fall as the charge rises");
                    }
                }
            }

            void ReadLegs(JsonNode p, DroneSpec spec)
            {
                DroneLegs legs = spec.Legs;
                if (!IsObject(p, "legs")) return;
                Members(p, "legs", "parts", "stiffness_n_per_m", "damping_n_s_per_m", "foot_radius_m");
                JsonNode names = Required(p, "parts", "legs");
                if (names != null && IsArray(names, "legs.parts", 4))
                {
                    if (names.Items.Count != 4) Fail("legs.parts", "one leg per corner, 4, in Betaflight's Quad X order");
                    legs.Parts = new int[names.Items.Count];
                    for (int i = 0; i < legs.Parts.Length; i++)
                    {
                        string path = "legs.parts[" + i + "]";
                        legs.Parts[i] = -1;
                        JsonNode n = names.Items[i];
                        if (n.Kind != JsonKind.String) { Fail(path, "must name a part"); continue; }
                        int index = spec.PartIndex(n.Text);
                        if (index < 0) { Fail(path, "no part is named '" + n.Text + "'"); continue; }
                        if (System.Array.IndexOf(legs.Parts, index) >= 0) Fail(path, "names a leg twice");
                        legs.Parts[i] = index;
                        if (i < 4) AtCorner(spec.Parts[index].Position, i, path + " (" + n.Text + ")");
                    }
                }
                legs.Stiffness = Positive(p, "stiffness_n_per_m", "legs");
                legs.Damping = NonNegative(p, "damping_n_s_per_m", "legs");
                legs.FootRadius = Positive(p, "foot_radius_m", "legs");
            }

            DroneCoil ReadCoil(JsonNode p, DroneSpec spec)
            {
                var coil = new DroneCoil { Part = -1 };
                if (!IsObject(p, "coil")) return coil;
                Members(p, "coil", "part", "spool_mass_kg", "fiber_mass_kg", "fiber_length_m");
                coil.Part = PartRef(p, "part", "coil", spec);
                coil.SpoolMass = Positive(p, "spool_mass_kg", "coil");
                coil.FiberMass = Positive(p, "fiber_mass_kg", "coil");
                coil.FiberLength = Positive(p, "fiber_length_m", "coil");
                double m = coil.Part >= 0 ? spec.Parts[coil.Part].Mass : double.NaN;
                if (!double.IsNaN(m + coil.SpoolMass + coil.FiberMass) && !(m >= coil.SpoolMass && m <= coil.SpoolMass + coil.FiberMass))
                    Fail("coil", "the coil part's mass, " + m + " kg, is outside the empty spool and the full one");
                return coil;
            }

            DronePayload ReadPayload(JsonNode p, DroneSpec spec)
            {
                var payload = new DronePayload { Part = -1 };
                if (!IsObject(p, "payload")) return payload;
                Members(p, "payload", "part", "mass_min_kg", "mass_max_kg");
                payload.Part = PartRef(p, "part", "payload", spec);
                payload.MinMass = NonNegative(p, "mass_min_kg", "payload");
                payload.MaxMass = Positive(p, "mass_max_kg", "payload");
                if (payload.MaxMass < payload.MinMass) Fail("payload", "mass_max_kg is below mass_min_kg");
                double m = payload.Part >= 0 ? spec.Parts[payload.Part].Mass : double.NaN;
                if (!double.IsNaN(m + payload.MinMass + payload.MaxMass) && !(m >= payload.MinMass && m <= payload.MaxMass))
                    Fail("payload", "the payload part's mass, " + m + " kg, is outside the setting's range");
                return payload;
            }

            // After the legs, the coil and the payload, which it checks against.
            void ReadLegsOff(JsonNode p, DroneSpec spec)
            {
                DroneLegsOff off = spec.LegsOff;
                if (!IsObject(p, "legs_off")) return;
                Members(p, "legs_off", "removed", "moved");
                JsonNode removed = Required(p, "removed", "legs_off");
                if (removed != null && IsArray(removed, "legs_off.removed", 1))
                {
                    off.Removed = new int[removed.Items.Count];
                    for (int i = 0; i < off.Removed.Length; i++) off.Removed[i] = -1;
                    for (int i = 0; i < off.Removed.Length; i++)
                    {
                        string path = "legs_off.removed[" + i + "]";
                        JsonNode n = removed.Items[i];
                        if (n.Kind != JsonKind.String) { Fail(path, "must name a part"); continue; }
                        int index = spec.PartIndex(n.Text);
                        if (index < 0) { Fail(path, "no part is named '" + n.Text + "'"); continue; }
                        if (System.Array.IndexOf(off.Removed, index) >= 0) { Fail(path, "names a part twice"); continue; }
                        if (index == spec.Coil.Part || index == spec.Payload.Part)
                            Fail(path, "the coil and the payload are the loadout's to set, not the legs'");
                        off.Removed[i] = index;
                    }
                    foreach (int leg in spec.Legs.Parts)
                        if (leg >= 0 && System.Array.IndexOf(off.Removed, leg) < 0)
                            Fail("legs_off.removed", "must take off the leg '" + spec.Parts[leg].Name + "'");
                }
                JsonNode moved = Required(p, "moved", "legs_off");
                if (moved != null && IsObject(moved, "legs_off.moved"))
                {
                    int count = moved.Keys.Count;
                    off.Moved = new int[count];
                    off.MovedTo = new Double3[count];
                    for (int i = 0; i < count; i++)
                    {
                        string name = moved.Keys[i], path = "legs_off.moved." + name;
                        int index = off.Moved[i] = spec.PartIndex(name);
                        if (index < 0) Fail(path, "no part is named '" + name + "'");
                        else if (System.Array.IndexOf(off.Removed, index) >= 0) Fail(path, "is removed as well");
                        Vector(moved, name, "legs_off.moved", out off.MovedTo[i]);
                    }
                }
            }

            DroneStand ReadStand(JsonNode p)
            {
                var stand = new DroneStand();
                if (!IsObject(p, "stand")) return stand;
                Members(p, "stand", "material", "post_radius_m", "post_height_m", "post_diameter_m", "mass_kg");
                stand.Material = Text(p, "material", "stand", Materials);
                stand.PostRadius = Positive(p, "post_radius_m", "stand");
                stand.PostHeight = Positive(p, "post_height_m", "stand");
                stand.PostDiameter = Positive(p, "post_diameter_m", "stand");
                stand.Mass = Positive(p, "mass_kg", "stand");
                return stand;
            }

            // After the coil, whose part carries the coil's own drag.
            void ReadAero(JsonNode p, DroneSpec spec)
            {
                AeroSpec aero = spec.Aero;
                aero.CoilPart = spec.Coil.Part;
                if (!IsObject(p, "aero")) return;
                Members(p, "aero", "air_density_kg_m3", "body_area_m2", "body_center_of_pressure_m", "drag_coefficient");
                aero.AirDensity = Positive(p, "air_density_kg_m3", "aero");
                if (Vector(p, "body_area_m2", "aero", out aero.BodyArea)
                    && !(aero.BodyArea.X > 0 && aero.BodyArea.Y > 0 && aero.BodyArea.Z > 0))
                    Fail("aero.body_area_m2", "every silhouette must be above 0");
                Vector(p, "body_center_of_pressure_m", "aero", out aero.BodyCenterOfPressure);
                aero.DragCoefficient = Positive(p, "drag_coefficient", "aero");
                if (spec.Coil.Part >= 0 && spec.Parts[spec.Coil.Part].Kind != PartKind.Cylinder)
                    Fail("coil.part", "the coil's drag needs its part to be a cylinder");
            }

            // ---- the building blocks ----

            void AtCorner(Double3 position, int corner, string path)
            {
                if (!(position.X * Side[corner] > 0 && position.Z * End[corner] > 0))
                    Fail(path, "is not at the " + Corner[corner] + " corner (Betaflight's Quad X order)");
            }

            bool IsObject(JsonNode n, string path)
            {
                if (n.Kind == JsonKind.Object) return true;
                Fail(path, "must be an object");
                return false;
            }

            bool IsArray(JsonNode n, string path, int min)
            {
                if (n.Kind != JsonKind.Array) { Fail(path, "must be a list"); return false; }
                if (n.Items.Count < min) { Fail(path, "must hold at least " + min); return false; }
                return true;
            }

            void Members(JsonNode o, string path, params string[] allowed)
            {
                foreach (string key in o.Keys)
                    if (System.Array.IndexOf(allowed, key) < 0)
                        Fail(path + "." + key, "is not a field of the schema");
            }

            JsonNode Required(JsonNode o, string key, string path)
            {
                JsonNode n = o.Member(key);
                if (n == null) Fail(path + "." + key, "missing");
                return n;
            }

            string Text(JsonNode o, string key, string path, string[] oneOf, bool optional = false)
            {
                JsonNode n = optional ? o.Member(key) : Required(o, key, path);
                if (n == null) return null;
                if (n.Kind != JsonKind.String || n.Text.Length == 0) { Fail(path + "." + key, "must be a word"); return null; }
                if (oneOf != null && System.Array.IndexOf(oneOf, n.Text) < 0)
                {
                    Fail(path + "." + key, "'" + n.Text + "' is not one of " + string.Join(", ", oneOf));
                    return null;
                }
                return n.Text;
            }

            int PartRef(JsonNode o, string key, string path, DroneSpec spec)
            {
                string name = Text(o, key, path, null);
                if (name == null) return -1;
                int index = spec.PartIndex(name);
                if (index < 0) Fail(path + "." + key, "no part is named '" + name + "'");
                return index;
            }

            // A labelled value, {"value": …, "label": one of Labels, "note": "…"}: the value node, or null after a problem.
            JsonNode Labelled(JsonNode o, string key, string path)
            {
                JsonNode n = Required(o, key, path);
                if (n == null) return null;
                path += "." + key;
                if (n.Kind != JsonKind.Object)
                {
                    Fail(path, "a number carries its label: {\"value\": …, \"label\": …}");
                    return null;
                }
                Members(n, path, "value", "label", "note");
                bool ok = true;
                JsonNode label = n.Member("label");
                if (label == null) { Fail(path + ".label", "missing: every number is labelled " + string.Join(", ", Labels)); ok = false; }
                else if (label.Kind != JsonKind.String || System.Array.IndexOf(Labels, label.Text) < 0)
                {
                    Fail(path + ".label", "must be one of " + string.Join(", ", Labels));
                    ok = false;
                }
                JsonNode note = n.Member("note");
                if (note != null && note.Kind != JsonKind.String) Fail(path + ".note", "must be words");
                JsonNode value = n.Member("value");
                if (value == null) { Fail(path + ".value", "missing"); ok = false; }
                return ok ? value : null;
            }

            double Number(JsonNode o, string key, string path)
            {
                JsonNode v = Labelled(o, key, path);
                if (v == null) return double.NaN;
                if (v.Kind != JsonKind.Number) { Fail(path + "." + key + ".value", "must be a number"); return double.NaN; }
                return v.Number;
            }

            double Positive(JsonNode o, string key, string path)
            {
                double x = Number(o, key, path);
                if (!double.IsNaN(x) && !(x > 0)) { Fail(path + "." + key, "must be above 0"); return double.NaN; }
                return x;
            }

            double NonNegative(JsonNode o, string key, string path)
            {
                double x = Number(o, key, path);
                if (!double.IsNaN(x) && !(x >= 0)) { Fail(path + "." + key, "must not be negative"); return double.NaN; }
                return x;
            }

            int Count(JsonNode o, string key, string path, int min)
            {
                double x = Number(o, key, path);
                if (double.IsNaN(x)) return 0;
                if (!(x >= min && x <= 1000 && x == (int)x)) { Fail(path + "." + key, "must be a whole number from " + min); return 0; }
                return (int)x;
            }

            double[] Numbers(JsonNode o, string key, string path, int min)
            {
                JsonNode v = Labelled(o, key, path);
                if (v == null || !IsArray(v, path + "." + key + ".value", min)) return new double[0];
                var xs = new double[v.Items.Count];
                for (int i = 0; i < xs.Length; i++)
                {
                    if (v.Items[i].Kind == JsonKind.Number) xs[i] = v.Items[i].Number;
                    else Fail(path + "." + key + ".value[" + i + "]", "must be a number");
                }
                return xs;
            }

            bool Vector(JsonNode o, string key, string path, out Double3 v)
            {
                v = new Double3(double.NaN, double.NaN, double.NaN);
                JsonNode n = Labelled(o, key, path);
                if (n == null) return false;
                if (!(n.Kind == JsonKind.Array && n.Items.Count == 3
                      && n.Items[0].Kind == JsonKind.Number && n.Items[1].Kind == JsonKind.Number && n.Items[2].Kind == JsonKind.Number))
                {
                    Fail(path + "." + key + ".value", "must be [x, y, z]");
                    return false;
                }
                v = new Double3(n.Items[0].Number, n.Items[1].Number, n.Items[2].Number);
                return true;
            }
        }
    }
}
