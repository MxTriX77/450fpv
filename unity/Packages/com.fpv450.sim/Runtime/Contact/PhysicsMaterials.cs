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
    /// p7a-ground 1.1 adds labelled fields (LabelledReader's form): a layer under a spread load (AreaRows) names its
    /// area_law and area_reference_depth_m, a slender member's row (BendingRows) its elastic_modulus_pa, and a row may
    /// carry a wet block (its stiffness_ratio and the derived stiffness_n_per_m on a compliant row, μs, μk, adhesion_pa
    /// and adhesion_release_m).
    /// </summary>
    public static class PhysicsMaterials
    {
        public const string RelativePath = "assets-src/physics_materials.json";

        /// <summary>The rows whose stiffness comes from a layer under a spread load: each must name its area law.</summary>
        public static readonly string[] AreaRows = { "straw", "fabric", "sandbag" };

        /// <summary>The rows whose members bend as cantilevers from their root: each must carry its modulus.</summary>
        public static readonly string[] BendingRows = { "bark", "deadwood" };

        static readonly string[] AreaLaws = { "mat", "punch", "winkler" };

        /// <summary>The file's rows in file order, and its hash. Throws FormatException naming every problem.</summary>
        public static MaterialRow[] Parse(string json, out ulong hash)
        {
            JsonNode root = DetJson.Parse(json);
            var labelled = new LabelledReader();
            var problems = labelled.Problems;
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
                        EdgeRadius = EdgeRadius(m, path, problems),
                    };
                    if (row.KineticFriction > row.StaticFriction) problems.Add(path + ": friction_kinetic is above friction_static");
                    bool hasK = m.Member("stiffness_n_per_m") != null, hasZ = m.Member("damping_ratio") != null;
                    if (hasK != hasZ || hasK && !(row.Stiffness > 0))
                        problems.Add(path + ": stiffness_n_per_m (above 0) and damping_ratio come together; both absent is rigid");
                    ReadLayerFields(m, path, ref row, labelled);
                    rows.Add(row);
                }
            }
            if (problems.Count > 0) throw new FormatException("physics_materials.json is not valid:\n  " + string.Join("\n  ", problems));
            hash = DroneSpec.HashText(json);
            return rows.ToArray();
        }

        // p7a-ground's labelled fields: the area law, the slender member's modulus and the wet block.
        static void ReadLayerFields(JsonNode m, string path, ref MaterialRow row, LabelledReader r)
        {
            if (m.Kind != JsonKind.Object) return;
            string name = row.Name;
            bool lawRow = Array.IndexOf(AreaRows, name) >= 0;
            if (lawRow || m.Member("area_law") != null || m.Member("area_reference_depth_m") != null)
            {
                string law = r.Word(m, "area_law", path, AreaLaws, out _);
                row.AreaLaw = law == "mat" ? AreaLaw.Mat : law == "punch" ? AreaLaw.Punch : law == "winkler" ? AreaLaw.Winkler : AreaLaw.None;
                row.AreaReferenceDepth = r.Number(m, "area_reference_depth_m", path, out _, 0, true);
                if (row.Rigid) r.Fail(path + ".area_law", "a rigid row has no area law: stiffness_n_per_m is what it scales");
            }
            if (Array.IndexOf(BendingRows, name) >= 0 || m.Member("elastic_modulus_pa") != null)
                row.ElasticModulus = r.Number(m, "elastic_modulus_pa", path, out _, 0, true);
            if (m.Member("crown") != null) row.Crown = ReadCrown(m.Member("crown"), path + ".crown", r);

            JsonNode wet = m.Member("wet");
            if (wet == null) return;
            string wp = path + ".wet";
            if (!r.IsObject(wet, wp)) return;
            row.HasWet = true;
            if (row.Rigid)
                r.Members(wet, wp, "friction_static", "friction_kinetic", "adhesion_pa", "adhesion_release_m");
            else
            {
                r.Members(wet, wp, "stiffness_ratio", "stiffness_n_per_m", "friction_static", "friction_kinetic", "adhesion_pa", "adhesion_release_m");
                row.WetStiffnessRatio = r.Number(wet, "stiffness_ratio", wp, out _, 0, true);
                row.WetStiffness = r.Number(wet, "stiffness_n_per_m", wp, out string lk, 0, true);
                r.Derived(lk, row.WetStiffness, row.Stiffness * row.WetStiffnessRatio, wp + ".stiffness_n_per_m", "stiffness_n_per_m × wet.stiffness_ratio");
            }
            row.WetStaticFriction = r.Number(wet, "friction_static", wp, out _, 0, true);
            row.WetKineticFriction = r.Number(wet, "friction_kinetic", wp, out _, 0, true);
            if (row.WetKineticFriction > row.WetStaticFriction) r.Fail(wp + ".friction_kinetic", "is above friction_static");
            row.WetAdhesion = r.Number(wet, "adhesion_pa", wp, out _);
            row.WetAdhesionRelease = r.Number(wet, "adhesion_release_m", wp, out _);
            if (row.WetAdhesion > 0 && !(row.WetAdhesionRelease > 0)) r.Fail(wp + ".adhesion_release_m", "must be above 0 where adhesion_pa is");
        }

        // A crown row's twigs and leaves (w9-village-physics 2.3): every value labelled; the leaf area densities by variant.
        static CrownRow ReadCrown(JsonNode c, string path, LabelledReader r)
        {
            if (!r.IsObject(c, path)) return null;
            r.Members(c, path, "about", "twig_length_per_volume_m_per_m3", "twig_diameter_m", "twig_free_length_m", "twig_rupture_stress_pa",
                "twig_elastic_modulus_pa", "leaf_mass_per_area_kg_m2", "leaf_dry_matter_content", "leaf_work_to_shear_j_per_m", "leaf_critical_speed_m_s",
                "leaf_cell_m", "leaf_area_density_m2_per_m3");
            var crown = new CrownRow
            {
                TwigLength = r.Number(c, "twig_length_per_volume_m_per_m3", path, out _, 0, true),
                TwigDiameter = r.Number(c, "twig_diameter_m", path, out _, 0, true),
                TwigFreeLength = r.Number(c, "twig_free_length_m", path, out _, 0, true),
                TwigRupture = r.Number(c, "twig_rupture_stress_pa", path, out _, 0, true),
                TwigModulus = r.Number(c, "twig_elastic_modulus_pa", path, out _, 0, true),
                LeafMassPerArea = r.Number(c, "leaf_mass_per_area_kg_m2", path, out _, 0, true),
                LeafDryMatter = r.Number(c, "leaf_dry_matter_content", path, out _, 0, true, 1),
                LeafWork = r.Number(c, "leaf_work_to_shear_j_per_m", path, out _, 0, true),
                LeafCriticalSpeed = r.Number(c, "leaf_critical_speed_m_s", path, out _, 0, true),
                LeafCell = r.Number(c, "leaf_cell_m", path, out _, 0, true),
            };
            JsonNode lad = c.Member("leaf_area_density_m2_per_m3");
            string lp = path + ".leaf_area_density_m2_per_m3";
            if (!r.IsObject(lad, lp)) return crown;
            crown.Variants = new string[lad.Keys.Count];
            crown.LeafAreaDensity = new double[lad.Keys.Count];
            for (int i = 0; i < lad.Keys.Count; i++)
            {
                crown.Variants[i] = lad.Keys[i];
                crown.LeafAreaDensity[i] = r.Number(lad, lad.Keys[i], lp, out _, 0, true);
            }
            return crown;
        }

        // edge_radius_m: required, a number above 0 or null (read as 0: round or homogenised members; p7d-tether 1.7).
        static double EdgeRadius(JsonNode o, string path, List<string> problems)
        {
            JsonNode n = o.Member("edge_radius_m");
            if (n == null)
            {
                problems.Add(path + ".edge_radius_m: missing");
                return 0;
            }
            if (n.Kind == JsonKind.Null) return 0;
            if (n.Kind != JsonKind.Number || !(n.Number > 0))
            {
                problems.Add(path + ".edge_radius_m: must be a number above 0 or null");
                return 0;
            }
            return n.Number;
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
