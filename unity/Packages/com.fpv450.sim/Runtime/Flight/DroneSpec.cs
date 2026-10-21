using System;
using System.Collections.Generic;

namespace Fpv450.Sim.Flight
{
    /// <summary>One motor and its rotor, at a corner in Betaflight's Quad X order (Corners).</summary>
    public struct DroneMotor
    {
        /// <summary>The prop's hub, where its thrust acts, body m.</summary>
        public Double3 Position;

        /// <summary>Clockwise seen from above: a positive rotation about body +y by DetMath's rule.</summary>
        public bool Clockwise;

        /// <summary>rpm per volt, unloaded.</summary>
        public double Kv;

        /// <summary>The motor and rotor's first-order lag τ_m, s.</summary>
        public double Tau;

        /// <summary>The ESC's command filter τ_esc, s.</summary>
        public double EscTau;
    }

    /// <summary>The four props, alike.</summary>
    public struct DroneProps
    {
        public double Diameter;
        public int Blades;

        /// <summary>The geometric pitch, m per revolution.</summary>
        public double Pitch;

        /// <summary>k_T in T = k_T·n², N per (rad/s)².</summary>
        public double ThrustCoefficient;

        /// <summary>κ in Q = κ·T, N·m per N.</summary>
        public double TorqueRatio;
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

        /// <summary>Legs on; off means the legs' parts weigh nothing and the flight starts on the stand.</summary>
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

        /// <summary>True for p6-flight-core's bootstrap file, which p5-drone replaces.</summary>
        public bool Bootstrap;

        public DronePart[] Parts;

        /// <summary>The four motors, in Betaflight's Quad X order: rear right, front right, rear left, front left.</summary>
        public DroneMotor[] Motors;

        public DroneProps Props;

        /// <summary>The two packs (PHB-03).</summary>
        public DronePack[] Packs;

        public DroneLegs Legs;
        public DroneCoil Coil;
        public DronePayload Payload;
        public DroneStand Stand;

        /// <summary>The file's own loadout: its payload and coil masses, legs on.</summary>
        public Loadout Nominal => new Loadout
        {
            Payload = Parts[Payload.Part].Mass,
            Legs = true,
            CoilPaidOut = (Coil.SpoolMass + Coil.FiberMass - Parts[Coil.Part].Mass) / Coil.FiberMass * Coil.FiberLength,
        };

        /// <summary>The coil's mass with paidOut metres of line gone.</summary>
        public double CoilMass(double paidOut) => Coil.SpoolMass + Coil.FiberMass * (1 - paidOut / Coil.FiberLength);

        /// <summary>The mass model at a loadout (the payload, the legs and the coil set on the file's parts).</summary>
        public MassModel BuildMass(in Loadout loadout)
        {
            if (!(loadout.Payload >= Payload.MinMass && loadout.Payload <= Payload.MaxMass))
                throw new ArgumentOutOfRangeException(nameof(loadout), "the payload is outside " + Payload.MinMass + "–" + Payload.MaxMass + " kg");
            if (!(loadout.CoilPaidOut >= 0 && loadout.CoilPaidOut <= Coil.FiberLength))
                throw new ArgumentOutOfRangeException(nameof(loadout), "the paid-out line is outside 0–" + Coil.FiberLength + " m");
            var parts = (DronePart[])Parts.Clone();
            parts[Payload.Part].Mass = loadout.Payload;
            parts[Coil.Part].Mass = CoilMass(loadout.CoilPaidOut);
            if (!loadout.Legs)
                foreach (int leg in Legs.Parts)
                    parts[leg].Mass = 0;
            return new MassModel(parts);
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
            return spec;
        }

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
                    Coil = new DroneCoil { Part = -1 },
                    Payload = new DronePayload { Part = -1 },
                };
                const string top = "drone.json";
                if (!IsObject(root, top)) return spec;
                Members(root, top, "schema", "owner", "name", "bootstrap", "about", "fields",
                    "parts", "motors", "props", "packs", "legs", "coil", "payload", "stand");

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
                if ((n = Required(root, "stand", top)) != null) spec.Stand = ReadStand(n);
                return spec;
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
                    Members(m, path, "position_m", "spin", "kv_rpm_per_v", "tau_s", "esc_tau_s");
                    ref DroneMotor motor = ref motors[i];
                    if (Vector(m, "position_m", path, out motor.Position)) AtCorner(motor.Position, i, path + ".position_m");
                    spins[i] = Text(m, "spin", path, new[] { "cw", "ccw" });
                    motor.Clockwise = spins[i] == "cw";
                    motor.Kv = Positive(m, "kv_rpm_per_v", path);
                    motor.Tau = Positive(m, "tau_s", path);
                    motor.EscTau = Positive(m, "esc_tau_s", path);
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
                Members(p, "props", "diameter_m", "blades", "pitch_m", "k_t_n_per_rad2_s2", "kappa_m");
                props.Diameter = Positive(p, "diameter_m", "props");
                props.Blades = Count(p, "blades", "props", 2);
                props.Pitch = Positive(p, "pitch_m", "props");
                props.ThrustCoefficient = Positive(p, "k_t_n_per_rad2_s2", "props");
                props.TorqueRatio = Positive(p, "kappa_m", "props");
                return props;
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
