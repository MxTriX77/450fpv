using System;
using System.Collections.Generic;
using System.Globalization;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Tether
{
    /// <summary>The shape of a tether.json value, as the schema fixes it per field.</summary>
    public enum TetherValueKind : byte
    {
        /// <summary>A number above 0.</summary>
        Positive = 0,

        /// <summary>Any finite number.</summary>
        Signed = 1,

        /// <summary>[lo, hi] with 0 &lt; lo ≤ hi: a stream's bounds, or a law's published range.</summary>
        Band = 2,

        /// <summary>[x, y, z], body metres.</summary>
        Vector = 3,

        /// <summary>[[p, value], …]: a piecewise-linear quantile table, p rising from 0 to 1, values above 0 and never
        /// falling, so a draw u in [0, 1] maps to a value by + − × ÷ only.</summary>
        Quantiles = 4,
    }

    /// <summary>One value of tether.json: "block.field", its label (an index into DroneSpec.Labels), its kind and its
    /// numbers (a quantile table's pairs flattened p0, v0, p1, v1, …). The rationale stays in the file, whose hash the
    /// log records.</summary>
    public struct TetherRow
    {
        public string Name;
        public byte Label;
        public TetherValueKind Kind;
        public double[] Values;
    }

    public sealed class TetherTableException : Exception
    {
        public readonly IReadOnlyList<string> Problems;

        public TetherTableException(List<string> problems)
            : base("tether.json is not valid:\n  " + string.Join("\n  ", problems)) => Problems = problems;
    }

    /// <summary>
    /// assets-src/tether.json (p7d-tether design 1, PHY-18…21): every number of the fiber, its strength, the spool, the
    /// optical link, the wires' end fittings and the launch, physics-owned. Each value is {"value": …, "label": "pilot" |
    /// "measured" | "published" | "derived" | "assumed", "rationale": "its source or reason"}; a block or field the schema
    /// lacks, a missing one, a missing label or rationale, or a value of the wrong shape refuses the file with every
    /// problem listed. A value labelled derived must equal its derivation from the table's own values (within 1e-3, the
    /// file's rounding), so an edit to an input cannot leave its consequence stale. Load time only: it allocates.
    ///
    /// The log header records the rows and the file's hash; a replay builds its table from the header's rows and refuses
    /// a tether.json of another hash (FlightReplay).
    /// </summary>
    public sealed class TetherTable
    {
        public const string RelativePath = "assets-src/tether.json";
        public const int Schema = 1;

        /// <summary>The blocks, in the file's and the log's order.</summary>
        public static readonly string[] Blocks = { "fiber", "strength", "spool", "link", "fittings", "launch" };

        // The schema: every field of every block and its shape, in order (design 1, 3, 5, 7, 8).
        static readonly (string Name, TetherValueKind Kind)[] Fields =
        {
            ("fiber.glass_diameter_m", TetherValueKind.Positive),
            ("fiber.coating_diameter_m", TetherValueKind.Positive),
            ("fiber.coating_thickness_m", TetherValueKind.Positive),
            ("fiber.mass_per_m_kg", TetherValueKind.Positive),
            ("fiber.weight_per_m_n", TetherValueKind.Positive),
            ("fiber.youngs_modulus_pa", TetherValueKind.Positive),
            ("fiber.glass_area_m2", TetherValueKind.Positive),
            ("fiber.axial_stiffness_n", TetherValueKind.Positive),
            ("fiber.bending_stiffness_n_m2", TetherValueKind.Positive),
            ("fiber.proof_stress_pa", TetherValueKind.Positive),
            ("fiber.proof_force_n", TetherValueKind.Positive),
            ("fiber.coating_modulus_pa", TetherValueKind.Positive),
            ("fiber.coating_poisson_ratio", TetherValueKind.Positive),
            ("fiber.coating_hardness_pa", TetherValueKind.Positive),
            ("fiber.archard_wear_coefficient", TetherValueKind.Positive),
            ("fiber.section_length_m", TetherValueKind.Positive),
            ("fiber.drag_cross_base", TetherValueKind.Positive),
            ("fiber.drag_cross_coefficient", TetherValueKind.Positive),
            ("fiber.drag_cross_exponent", TetherValueKind.Signed),
            ("fiber.drag_cross_reynolds", TetherValueKind.Band),
            ("fiber.drag_axial", TetherValueKind.Positive),
            ("fiber.air_kinematic_viscosity_m2_s", TetherValueKind.Positive),
            ("fiber.wet_film_rain_m", TetherValueKind.Positive),
            ("fiber.wet_film_severe_m", TetherValueKind.Positive),
            ("fiber.water_density_kg_m3", TetherValueKind.Positive),
            ("strength.span_pa", TetherValueKind.Quantiles),
            ("strength.flaw_spacing_m", TetherValueKind.Positive),
            ("strength.flaw_pa", TetherValueKind.Quantiles),
            ("strength.intact_pa", TetherValueKind.Quantiles),
            ("strength.abraded_pa", TetherValueKind.Quantiles),
            ("spool.peel_energy_j_m", TetherValueKind.Band),
            ("spool.peel_rate_exponent", TetherValueKind.Positive),
            ("spool.peel_reference_rate_m_s", TetherValueKind.Positive),
            ("spool.irregularity", TetherValueKind.Band),
            ("spool.balloon_length_m", TetherValueKind.Positive),
            ("spool.lip_radius_m", TetherValueKind.Positive),
            ("spool.lip_friction", TetherValueKind.Positive),
            ("spool.exit_position_m", TetherValueKind.Vector),
            ("link.wavelength_m", TetherValueKind.Positive),
            ("link.budget_db", TetherValueKind.Positive),
            ("link.connector_loss_db", TetherValueKind.Positive),
            ("link.launch_margin_db", TetherValueKind.Positive),
            ("link.attenuation_db_per_m", TetherValueKind.Positive),
            ("link.macrobend_loss_a_db", TetherValueKind.Positive),
            ("link.macrobend_radius_a_m", TetherValueKind.Positive),
            ("link.macrobend_loss_b_db", TetherValueKind.Positive),
            ("link.macrobend_radius_b_m", TetherValueKind.Positive),
            ("link.macrobend_decay_per_m", TetherValueKind.Positive),
            ("link.microbend_smooth_db_per_n", TetherValueKind.Positive),
            ("link.microbend_rough_db_per_n", TetherValueKind.Positive),
            ("link.dip_threshold_db", TetherValueKind.Signed),
            ("link.dip_hysteresis_db", TetherValueKind.Positive),
            ("fittings.tie_hold_n", TetherValueKind.Band),
            ("fittings.tie_unseat_slip_m", TetherValueKind.Positive),
            ("fittings.sip_clamp_hold_ratio", TetherValueKind.Positive),
            ("fittings.house_anchor_hold_n", TetherValueKind.Band),
            ("fittings.conductor_strength_ratio", TetherValueKind.Band),
            ("launch.anchor_behind_start_m", TetherValueKind.Positive),
        };

        const double DerivedTolerance = 1e-3;

        /// <summary>The file's identity, which the flight log records and a replay checks (DroneSpec.HashText's rule).</summary>
        public readonly ulong ContentHash;

        /// <summary>The rows in the schema's order.</summary>
        public readonly TetherRow[] Rows;

        /// <summary>A table from rows and a hash as they stand: the log header's, which a replay builds its table from
        /// once it has checked the hash against its own tether.json.</summary>
        public TetherTable(TetherRow[] rows, ulong contentHash)
        {
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            ContentHash = contentHash;
        }

        /// <summary>The row named "block.field". Throws ArgumentException for a name the table lacks.</summary>
        public TetherRow Row(string name)
        {
            foreach (TetherRow r in Rows)
                if (string.Equals(r.Name, name, StringComparison.Ordinal))
                    return r;
            throw new ArgumentException("tether.json has no value " + name, nameof(name));
        }

        /// <summary>The number of a Positive or Signed row.</summary>
        public double Number(string name) => Row(name).Values[0];

        /// <summary>The file's rows, validated (the class summary). Throws TetherTableException naming every problem.</summary>
        public static TetherTable Parse(string json)
        {
            JsonNode root;
            try
            {
                root = DetJson.Parse(json);
            }
            catch (FormatException e)
            {
                throw new TetherTableException(new List<string> { e.Message });
            }
            var problems = new List<string>();
            var rows = new TetherRow[Fields.Length];
            if (root.Kind != JsonKind.Object)
                problems.Add("tether.json: must be an object");
            else
                Read(root, rows, problems);
            if (problems.Count == 0) CheckDerived(rows, problems);
            if (problems.Count > 0) throw new TetherTableException(problems);
            return new TetherTable(rows, DroneSpec.HashText(json));
        }

        static void Read(JsonNode root, TetherRow[] rows, List<string> problems)
        {
            for (int i = 0; i < root.Keys.Count; i++)
            {
                string key = root.Keys[i];
                if (key != "schema" && key != "owner" && key != "about" && key != "fields" && Array.IndexOf(Blocks, key) < 0)
                    problems.Add("tether.json." + key + ": is not a block of the schema");
            }
            JsonNode schema = root.Member("schema");
            if (schema == null || schema.Kind != JsonKind.Number || schema.Number != Schema)
                problems.Add("tether.json.schema: must be " + Schema);
            foreach (string key in new[] { "owner", "about" })
            {
                JsonNode n = root.Member(key);
                if (n == null || n.Kind != JsonKind.String || n.Text.Length == 0) problems.Add("tether.json." + key + ": must be words");
            }

            foreach (string block in Blocks)
            {
                JsonNode b = root.Member(block);
                if (b == null || b.Kind != JsonKind.Object)
                {
                    problems.Add(block + ": missing or not an object");
                    continue;
                }
                for (int i = 0; i < b.Keys.Count; i++)
                    if (IndexOf(block + "." + b.Keys[i]) < 0)
                        problems.Add(block + "." + b.Keys[i] + ": is not a field of the schema");
            }

            for (int f = 0; f < Fields.Length; f++)
            {
                string name = Fields[f].Name;
                int dot = name.IndexOf('.');
                JsonNode b = root.Member(name.Substring(0, dot));
                if (b == null || b.Kind != JsonKind.Object) continue;
                rows[f] = new TetherRow { Name = name, Kind = Fields[f].Kind, Values = new double[0] };
                JsonNode n = b.Member(name.Substring(dot + 1));
                if (n == null) { problems.Add(name + ": missing"); continue; }
                if (n.Kind != JsonKind.Object) { problems.Add(name + ": a value carries its label: {\"value\": …, \"label\": …, \"rationale\": …}"); continue; }
                for (int i = 0; i < n.Keys.Count; i++)
                    if (n.Keys[i] != "value" && n.Keys[i] != "label" && n.Keys[i] != "rationale")
                        problems.Add(name + "." + n.Keys[i] + ": is not a member of a value (value, label, rationale)");
                JsonNode label = n.Member("label");
                int labelIndex = label != null && label.Kind == JsonKind.String ? Array.IndexOf(DroneSpec.Labels, label.Text) : -1;
                if (label == null) problems.Add(name + ".label: missing: every value is labelled " + string.Join(", ", DroneSpec.Labels));
                else if (labelIndex < 0) problems.Add(name + ".label: must be one of " + string.Join(", ", DroneSpec.Labels));
                else rows[f].Label = (byte)labelIndex;
                JsonNode rationale = n.Member("rationale");
                if (rationale == null || rationale.Kind != JsonKind.String || rationale.Text.Trim().Length == 0)
                    problems.Add(name + ".rationale: missing: every value cites its source or gives its reason");
                JsonNode value = n.Member("value");
                if (value == null) problems.Add(name + ".value: missing");
                else rows[f].Values = Values(value, Fields[f].Kind, name + ".value", problems);
            }
        }

        // The value's numbers in its kind's shape, or none after a problem.
        static double[] Values(JsonNode v, TetherValueKind kind, string path, List<string> problems)
        {
            var xs = new List<double>();
            switch (kind)
            {
                case TetherValueKind.Positive:
                case TetherValueKind.Signed:
                    if (v.Kind != JsonKind.Number) { problems.Add(path + ": must be a number"); break; }
                    if (kind == TetherValueKind.Positive && !(v.Number > 0)) { problems.Add(path + ": must be above 0"); break; }
                    xs.Add(v.Number);
                    break;
                case TetherValueKind.Band:
                case TetherValueKind.Vector:
                    int count = kind == TetherValueKind.Band ? 2 : 3;
                    if (!NumberList(v, count, xs)) { problems.Add(path + (kind == TetherValueKind.Band ? ": must be [lo, hi]" : ": must be [x, y, z]")); break; }
                    if (kind == TetherValueKind.Band && !(xs[0] > 0 && xs[0] <= xs[1])) { problems.Add(path + ": must be [lo, hi] with 0 < lo ≤ hi"); xs.Clear(); }
                    break;
                case TetherValueKind.Quantiles:
                    bool ok = v.Kind == JsonKind.Array && v.Items.Count >= 2;
                    for (int i = 0; ok && i < v.Items.Count; i++)
                        ok = NumberList(v.Items[i], 2, xs);
                    if (!ok) { problems.Add(path + ": must be a quantile table [[p, value], …] of at least two pairs"); xs.Clear(); break; }
                    int n = xs.Count / 2;
                    bool shape = xs[0] == 0 && xs[2 * n - 2] == 1 && xs[1] > 0;
                    for (int i = 1; shape && i < n; i++)
                        shape = xs[2 * i] > xs[2 * i - 2] && xs[2 * i + 1] >= xs[2 * i - 1];
                    if (!shape) { problems.Add(path + ": p must rise from 0 to 1 and the values, above 0, never fall"); xs.Clear(); }
                    break;
            }
            return xs.ToArray();
        }

        static bool NumberList(JsonNode v, int count, List<double> xs)
        {
            if (v.Kind != JsonKind.Array || v.Items.Count != count) return false;
            foreach (JsonNode x in v.Items)
                if (x.Kind != JsonKind.Number) return false;
            foreach (JsonNode x in v.Items) xs.Add(x.Number);
            return true;
        }

        static int IndexOf(string name)
        {
            for (int i = 0; i < Fields.Length; i++)
                if (Fields[i].Name == name) return i;
            return -1;
        }

        // Each value labelled derived against its derivation (design 1, 7, 8); the strength bands meet where design 1
        // ties them: the span's and the flaws' floor is the proof stress, the flaws reach the intact floor, and abraded
        // glass reaches up to the proof stress.
        static void CheckDerived(TetherRow[] rows, List<string> problems)
        {
            double V(string name) => rows[IndexOf(name)].Values[0];
            const double Pi = 3.141592653589793;
            double dg = V("fiber.glass_diameter_m"), e = V("fiber.youngs_modulus_pa"), area = Pi / 4 * dg * dg;
            Derived(rows, "fiber.coating_thickness_m", (V("fiber.coating_diameter_m") - dg) / 2, "(coating_diameter_m − glass_diameter_m)/2", problems);
            Derived(rows, "fiber.weight_per_m_n", V("fiber.mass_per_m_kg") * -FlightCore.Gravity.Y, "mass_per_m_kg·g", problems);
            Derived(rows, "fiber.glass_area_m2", area, "π·glass_diameter_m²/4", problems);
            Derived(rows, "fiber.axial_stiffness_n", e * area, "youngs_modulus_pa·glass_area", problems);
            Derived(rows, "fiber.bending_stiffness_n_m2", e * Pi * dg * dg * dg * dg / 64, "youngs_modulus_pa·π·glass_diameter_m⁴/64", problems);
            Derived(rows, "fiber.proof_force_n", V("fiber.proof_stress_pa") * area, "proof_stress_pa·glass_area", problems);
            Derived(rows, "link.launch_margin_db", V("link.budget_db") - V("link.connector_loss_db"), "budget_db − connector_loss_db", problems);
            Derived(rows, "link.macrobend_decay_per_m",
                DetMath.Ln(V("link.macrobend_loss_a_db") / V("link.macrobend_loss_b_db")) / (V("link.macrobend_radius_b_m") - V("link.macrobend_radius_a_m")),
                "ln(macrobend_loss_a_db/macrobend_loss_b_db)/(macrobend_radius_b_m − macrobend_radius_a_m)", problems);

            double proof = V("fiber.proof_stress_pa");
            double[] span = rows[IndexOf("strength.span_pa")].Values, flaw = rows[IndexOf("strength.flaw_pa")].Values,
                intact = rows[IndexOf("strength.intact_pa")].Values, abraded = rows[IndexOf("strength.abraded_pa")].Values;
            Meets("strength.span_pa", "starts", span[1], "fiber.proof_stress_pa", proof, problems);
            Meets("strength.flaw_pa", "starts", flaw[1], "fiber.proof_stress_pa", proof, problems);
            Meets("strength.flaw_pa", "ends", flaw[flaw.Length - 1], "strength.intact_pa's first value", intact[1], problems);
            Meets("strength.abraded_pa", "ends", abraded[abraded.Length - 1], "fiber.proof_stress_pa", proof, problems);
        }

        static void Derived(TetherRow[] rows, string name, double expected, string how, List<string> problems)
        {
            TetherRow r = rows[IndexOf(name)];
            if (DroneSpec.Labels[r.Label] != "derived") return;
            if (!Close(r.Values[0], expected))
                problems.Add(name + ": " + G(r.Values[0]) + " is labelled derived but " + how + " = " + G(expected));
        }

        static void Meets(string name, string end, double value, string other, double expected, List<string> problems)
        {
            if (!Close(value, expected)) problems.Add(name + ": " + end + " at " + G(value) + ", not at " + other + " " + G(expected));
        }

        static bool Close(double a, double b) => DetMath.Abs(a - b) <= DerivedTolerance * DetMath.Abs(b);

        static string G(double x) => x.ToString("G6", CultureInfo.InvariantCulture);
    }
}
