using System;
using System.Collections.Generic;
using System.Globalization;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Contact
{
    /// <summary>
    /// Reads the physics tables' labelled values, drone.json's form: {"value": …, "label": "pilot" | "measured" |
    /// "published" | "derived" | "assumed", "note": "…"}, the note required (surfaces.json, physics_materials.json's
    /// p7a-ground fields). It records a problem for anything missing, misspelt, unlabelled or out of range, so one run
    /// lists them all; a value that could not be read is NaN and the checks that depend on it stay quiet. A value
    /// labelled derived whose field has a formula must meet it (Derived), within 1e-9 of it, or exactly 0.
    /// </summary>
    internal sealed class LabelledReader
    {
        public static readonly string[] Labels = { "pilot", "measured", "published", "derived", "assumed" };

        public readonly List<string> Problems = new List<string>();

        public void Fail(string path, string what) => Problems.Add(path + ": " + what);

        /// <summary>Refuses every member of o not in allowed.</summary>
        public void Members(JsonNode o, string path, params string[] allowed)
        {
            foreach (string key in o.Keys)
                if (Array.IndexOf(allowed, key) < 0)
                    Fail(path + "." + key, "is not a field of the schema");
        }

        public bool IsObject(JsonNode n, string path)
        {
            if (n != null && n.Kind == JsonKind.Object) return true;
            Fail(path, n == null ? "missing" : "must be an object");
            return false;
        }

        /// <summary>The value node of the labelled o.key and its label, or null after a problem (or absent and optional).</summary>
        public JsonNode Value(JsonNode o, string key, string path, out string label, bool optional = false)
        {
            label = null;
            path += "." + key;
            JsonNode n = o.Member(key);
            if (n == null)
            {
                if (!optional) Fail(path, "missing");
                return null;
            }
            if (n.Kind != JsonKind.Object)
            {
                Fail(path, "a value carries its label: {\"value\": …, \"label\": …, \"note\": …}");
                return null;
            }
            Members(n, path, "value", "label", "note");
            bool ok = true;
            JsonNode l = n.Member("label");
            if (l == null)
            {
                Fail(path + ".label", "missing: every value is labelled " + string.Join(", ", Labels));
                ok = false;
            }
            else if (l.Kind != JsonKind.String || Array.IndexOf(Labels, l.Text) < 0)
            {
                Fail(path + ".label", "must be one of " + string.Join(", ", Labels));
                ok = false;
            }
            else label = l.Text;
            JsonNode note = n.Member("note");
            if (note == null || note.Kind != JsonKind.String || note.Text.Trim().Length == 0)
            {
                Fail(path + ".note", "missing: every value says its source, its reason or its formula");
                ok = false;
            }
            JsonNode v = n.Member("value");
            if (v == null)
            {
                Fail(path + ".value", "missing");
                ok = false;
            }
            return ok ? v : null;
        }

        /// <summary>The labelled number o.key, at least min (above it when open); NaN after a problem, 0 when optional and absent.</summary>
        public double Number(JsonNode o, string key, string path, out string label, double min = 0, bool open = false, double max = double.PositiveInfinity,
            bool optional = false)
        {
            JsonNode v = Value(o, key, path, out label, optional);
            if (v == null) return optional && o.Member(key) == null ? 0 : double.NaN;
            if (v.Kind != JsonKind.Number)
            {
                Fail(path + "." + key + ".value", "must be a number");
                return double.NaN;
            }
            double x = v.Number;
            if (!(open ? x > min : x >= min) || !(x <= max))
            {
                Fail(path + "." + key, "must be " + (open ? "above " : "at least ") + G(min) + (max < double.PositiveInfinity ? " and at most " + G(max) : ""));
                return double.NaN;
            }
            return x;
        }

        /// <summary>The labelled range o.key, [min, max] with 0 &lt; min ≤ max; false (both NaN) after a problem.</summary>
        public bool Range(JsonNode o, string key, string path, out double lo, out double hi, out string label)
        {
            lo = hi = double.NaN;
            JsonNode v = Value(o, key, path, out label);
            if (v == null) return false;
            if (!(v.Kind == JsonKind.Array && v.Items.Count == 2 && v.Items[0].Kind == JsonKind.Number && v.Items[1].Kind == JsonKind.Number
                  && v.Items[0].Number > 0 && v.Items[0].Number <= v.Items[1].Number))
            {
                Fail(path + "." + key + ".value", "must be [min, max] with 0 < min ≤ max");
                return false;
            }
            lo = v.Items[0].Number;
            hi = v.Items[1].Number;
            return true;
        }

        /// <summary>The labelled word o.key, one of oneOf; null after a problem.</summary>
        public string Word(JsonNode o, string key, string path, string[] oneOf, out string label)
        {
            JsonNode v = Value(o, key, path, out label);
            if (v == null) return null;
            if (v.Kind != JsonKind.String || Array.IndexOf(oneOf, v.Text) < 0)
            {
                Fail(path + "." + key + ".value", "must be one of " + string.Join(", ", oneOf));
                return null;
            }
            return v.Text;
        }

        /// <summary>A value labelled derived meets its formula: within 1e-9 of it, or exactly 0 when the formula is 0.</summary>
        public void Derived(string label, double value, double formula, string path, string formulaText)
        {
            if (label != "derived" || double.IsNaN(value) || double.IsNaN(formula)) return;
            if (Meets(value, formula)) return;
            Fail(path, "is labelled derived, but is " + G(value) + " where " + formulaText + " is " + G(formula));
        }

        public static bool Meets(double value, double formula) =>
            formula == 0 ? value == 0 : DetMath.Abs(value - formula) <= 1e-9 * DetMath.Abs(formula);

        public static string G(double x) => x.ToString("R", CultureInfo.InvariantCulture);
    }
}
