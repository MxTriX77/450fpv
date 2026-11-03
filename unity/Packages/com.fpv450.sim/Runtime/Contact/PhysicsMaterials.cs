using System;
using System.Collections.Generic;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Contact
{
    /// <summary>
    /// assets-src/physics_materials.json, the physics material ids every collision shape names (AST-05): physics reads
    /// its own table from it (review-physics §3: the world hands out ids, never parameters). Only the contact law's
    /// fields and what a blade meets (p7f-damage design 5, each labelled) are read, by DetJson (the same bits on every
    /// runtime); the world-builder's validator checks the rest.
    /// The file lies outside the world's content hash, so the log records its hash beside drone.json's (P8).
    /// </summary>
    public static class PhysicsMaterials
    {
        public const string RelativePath = "assets-src/physics_materials.json";

        /// <summary>The file's rows in file order, and its hash. Throws FormatException naming every problem.</summary>
        public static MaterialRow[] Parse(string json, out ulong hash)
        {
            JsonNode root = DetJson.Parse(json);
            var problems = new List<string>();
            var rows = new List<MaterialRow>();
            JsonNode materials = root.Member("materials");
            if (materials == null || materials.Kind != JsonKind.Object)
                problems.Add("materials: missing or not an object");
            else
            {
                for (int i = 0; i < materials.Keys.Count; i++)
                {
                    string name = materials.Keys[i], path = "materials." + name;
                    JsonNode m = materials.Items[i];
                    var row = new MaterialRow
                    {
                        Name = name,
                        StaticFriction = Number(m, "friction_static", path, problems, true),
                        KineticFriction = Number(m, "friction_kinetic", path, problems, true),
                        Stiffness = Number(m, "stiffness_n_per_m", path, problems, false),
                        DampingRatio = Number(m, "damping_ratio", path, problems, false),
                        Hardness = Labelled(m, "hardness_pa", path, problems, false),
                        CrushStrength = Labelled(m, "crush_strength_pa", path, problems, true),
                        Density = Labelled(m, "density_kg_m3", path, problems, false),
                    };
                    if (row.KineticFriction > row.StaticFriction) problems.Add(path + ": friction_kinetic is above friction_static");
                    bool hasK = m.Member("stiffness_n_per_m") != null, hasZ = m.Member("damping_ratio") != null;
                    if (hasK != hasZ || hasK && !(row.Stiffness > 0))
                        problems.Add(path + ": stiffness_n_per_m (above 0) and damping_ratio come together; both absent is rigid");
                    rows.Add(row);
                }
            }
            if (problems.Count > 0) throw new FormatException("physics_materials.json is not valid:\n  " + string.Join("\n  ", problems));
            hash = DroneSpec.HashText(json);
            return rows.ToArray();
        }

        // A plain number ≥ 0; an absent optional one is 0 (rigid).
        static double Number(JsonNode o, string key, string path, List<string> problems, bool required)
        {
            JsonNode n = o.Member(key);
            if (n == null)
            {
                if (required) problems.Add(path + "." + key + ": missing");
                return 0;
            }
            if (n.Kind != JsonKind.Number || !(n.Number >= 0))
            {
                problems.Add(path + "." + key + ": must be a number of at least 0");
                return 0;
            }
            return n.Number;
        }

        // What a blade meets (p7f-damage 1.1a), each labelled: {"value": a number above 0 (or null where nullable, read as 0),
        // "label": one of DroneSpec.Labels, "note": its source or reason}.
        static double Labelled(JsonNode o, string key, string path, List<string> problems, bool nullable)
        {
            string at = path + "." + key;
            JsonNode n = o.Member(key);
            if (n == null)
            {
                problems.Add(at + ": missing");
                return 0;
            }
            JsonNode value = n.Member("value"), label = n.Member("label"), note = n.Member("note");
            if (n.Kind != JsonKind.Object || n.Keys.Count != 3 || value == null || label == null || note == null)
            {
                problems.Add(at + ": must be {\"value\", \"label\", \"note\"}");
                return 0;
            }
            if (label.Kind != JsonKind.String || Array.IndexOf(DroneSpec.Labels, label.Text) < 0)
                problems.Add(at + ".label: must be one of " + string.Join(", ", DroneSpec.Labels));
            if (note.Kind != JsonKind.String || note.Text.Length == 0)
                problems.Add(at + ".note: missing: a value here carries its source or its reason");
            if (nullable && value.Kind == JsonKind.Null) return 0;
            if (value.Kind != JsonKind.Number || !(value.Number > 0))
            {
                problems.Add(at + ".value: must be a number above 0" + (nullable ? " or null" : ""));
                return 0;
            }
            return value.Number;
        }
    }
}
